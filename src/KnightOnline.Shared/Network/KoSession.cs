using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using KnightOnline.Shared.Crypto;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Protocol;

namespace KnightOnline.Shared.Network;

/// <summary>
/// Port of KOSocket (shared/KOSocket.cpp) on top of async sockets instead of
/// Win32 IOCP. One instance per connected client.
///
/// Preserved behaviour:
///  - Exact framing / JvCryption handling (via <see cref="KoPacketCodec"/>);
///  - Malformed header/footer, oversized packet, bad CRC or a wrong crypto
///    sequence number all drop the connection;
///  - A packet handler returning false drops the connection (Release builds
///    in the original; kept always-on here);
///  - <see cref="Send"/> / <see cref="SendCompressed"/> are fire-and-forget
///    for callers (the original queued into a write buffer too).
///
/// Differences (deliberate):
///  - The original disconnected clients whose packets fragmented across more
///    than 4 reads ("probably up to something bad"). TCP fragmentation is
///    normal off-LAN, so we simply accumulate - capped by MaxPayloadSize.
///  - Send ordering: a single writer loop drains a channel, so packets are
///    written in the order Send() was called, from any thread.
/// </summary>
public abstract class KoSession : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly Channel<byte[]> _outbound;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _sendGate = new(); // keeps framing/crypto state consistent
    private int _disposed;

    protected KoSession(TcpClient client, int maxPayloadSize = 32_768)
    {
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
        _outbound = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
        });
        MaxPayloadSize = maxPayloadSize;
        RemoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
        LastResponseUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Manager-assigned socket id (C++ m_socketID).</summary>
    public ushort SessionId { get; internal set; }

    /// <summary>Game-logic target id (C++ m_targetID), set by server code once in-world.</summary>
    public ushort TargetId { get; set; }

    public IPEndPoint? RemoteEndPoint { get; }

    public string RemoteIp => RemoteEndPoint?.Address.ToString() ?? "?";

    public bool IsConnected => _disposed == 0 && _client.Connected;

    /// <summary>Last time a valid packet was received (C++ m_lastResponse).</summary>
    public DateTimeOffset LastResponseUtc { get; private set; }

    public int MaxPayloadSize { get; }

    protected JvCryption Crypto { get; } = new();

    public bool IsCryptoEnabled { get; private set; }

    private uint _sequence;

    /// <summary>Port of KOSocket::EnableCrypto().</summary>
    public void EnableCrypto()
    {
        lock (_sendGate)
        {
            Crypto.Init();
            IsCryptoEnabled = true;
        }
    }

    protected virtual void OnConnected()
    {
        KoLog.Info($"Connection received from {RemoteIp}:{RemoteEndPoint?.Port}");
    }

    protected virtual void OnDisconnected()
    {
        KoLog.Info($"Connection closed from {RemoteIp}:{RemoteEndPoint?.Port}");
    }

    /// <summary>Return false to disconnect the client (original HandlePacket semantics).</summary>
    protected abstract bool HandlePacket(Packet packet);

    /// <summary>Port of KOSocket::Send().</summary>
    public virtual bool Send(Packet packet)
    {
        if (!IsConnected)
            return false;

        byte[] frame;
        lock (_sendGate)
        {
            frame = KoPacketCodec.Encode(packet, Crypto, IsCryptoEnabled, _sequence);
        }

        return _outbound.Writer.TryWrite(frame);
    }

    /// <summary>Port of KOSocket::SendCompressed().</summary>
    public virtual bool SendCompressed(Packet packet)
    {
        var compressed = KoPacketCodec.CompressIfNeeded(packet);
        return Send(compressed ?? packet);
    }

    public void Disconnect()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _shutdown.Cancel();
        _outbound.Writer.TryComplete();
        try { _client.Close(); } catch { /* already gone */ }
    }

    /// <summary>Runs the read+write loops until the connection ends. Called by the server.</summary>
    internal async Task RunAsync(CancellationToken serverShutdown)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(serverShutdown, _shutdown.Token);
        var token = linked.Token;

        OnConnected();
        try
        {
            var writerTask = WriteLoopAsync(token);
            await ReadLoopAsync(token).ConfigureAwait(false);
            Disconnect();
            await writerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Disconnect();
        }
        catch (Exception ex)
        {
            KoLog.Error($"Session {SessionId} ({RemoteIp}) failed: {ex.Message}");
            Disconnect();
        }
        finally
        {
            OnDisconnected();
        }
    }

    private async Task WriteLoopAsync(CancellationToken token)
    {
        try
        {
            await foreach (var frame in _outbound.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                await _stream.WriteAsync(frame, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception) { Disconnect(); }
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        // Accumulator: bytes received but not yet consumed as a full frame.
        var accumulator = new List<byte>(8192);
        var readBuffer = ArrayPool<byte>.Shared.Rent(8192);

        try
        {
            while (!token.IsCancellationRequested)
            {
                var read = await _stream.ReadAsync(readBuffer.AsMemory(), token).ConfigureAwait(false);
                if (read == 0)
                    return; // client closed

                accumulator.AddRange(readBuffer.AsSpan(0, read).ToArray());

                if (!DrainFrames(accumulator))
                    return; // malformed input: disconnect (original error_handler)
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
        }
    }

    /// <summary>Consumes every complete frame in the accumulator. False = disconnect.</summary>
    private bool DrainFrames(List<byte> accumulator)
    {
        while (true)
        {
            // Need at least header(2) + length(2) + opcode(1) before deciding anything.
            if (accumulator.Count < 5)
                return true;

            if (accumulator[0] != KoPacketCodec.HeaderByte1 || accumulator[1] != KoPacketCodec.HeaderByte2)
            {
                KoLog.Warn($"{RemoteIp}: got packet without header 0xAA55");
                return false;
            }

            var payloadLength = accumulator[2] | (accumulator[3] << 8);
            if (payloadLength == 0)
            {
                KoLog.Warn($"{RemoteIp}: got packet without an opcode");
                return false;
            }

            if (payloadLength > MaxPayloadSize)
            {
                KoLog.Warn($"{RemoteIp}: packet of {payloadLength} bytes exceeds maximum of {MaxPayloadSize}");
                return false;
            }

            var frameLength = payloadLength + KoPacketCodec.FrameOverhead;
            if (accumulator.Count < frameLength)
                return true; // wait for the rest of the frame

            if (accumulator[frameLength - 2] != KoPacketCodec.FooterByte1 ||
                accumulator[frameLength - 1] != KoPacketCodec.FooterByte2)
            {
                KoLog.Warn($"{RemoteIp}: invalid packet footer");
                return false;
            }

            var payload = accumulator.GetRange(4, payloadLength).ToArray();
            accumulator.RemoveRange(0, frameLength);

            var sequence = _sequence;
            Packet? packet;
            lock (_sendGate)
            {
                packet = KoPacketCodec.DecodePayload(payload, Crypto, IsCryptoEnabled, ref sequence);
            }
            if (packet is null)
            {
                KoLog.Warn($"{RemoteIp}: failed to decrypt packet");
                return false;
            }
            _sequence = sequence;

            LastResponseUtc = DateTimeOffset.UtcNow;

            if (!HandlePacket(packet))
            {
                KoLog.Warn($"{RemoteIp}: handler for packet 0x{packet.Opcode:X2} returned false");
                return false;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        Disconnect();
        _client.Dispose();
        _shutdown.Dispose();
        return ValueTask.CompletedTask;
    }
}
