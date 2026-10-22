using System.Binary;
using KnightOnline.Shared.Compression;
using KnightOnline.Shared.Crypto;

namespace KnightOnline.Shared.Protocol;

/// <summary>
/// The wire format, ported from KOSocket::OnRead / Send / SendCompressed and
/// KOSocket::DecryptPacket (shared/KOSocket.cpp). Extracted from the socket
/// so it can be unit-tested without any networking.
///
/// Frame layout (both directions):
///   AA 55 | uint16 LE payloadLen | payload | 55 AA
///
/// Plain payload:      [opcode][data...]
/// Encrypted, client-&gt;server payload (before JvEncryptionFast):
///   [uint32 sequence][opcode][data...][uint32 crc32(seq+opcode+data, start=0xFFFFFFFF)]
/// Encrypted, server-&gt;client payload (before JvEncryptionFast):
///   [uint16 0x1EFC][uint16 sequence][0x00][opcode][data...]
/// (Yes, the directions really do use different wrappers, and the server
/// only sends the low 16 bits of its sequence counter - ported as-is.)
/// </summary>
public static class KoPacketCodec
{
    public const byte HeaderByte1 = 0xAA;
    public const byte HeaderByte2 = 0x55;
    public const byte FooterByte1 = 0x55;
    public const byte FooterByte2 = 0xAA;

    /// <summary>Total bytes around the payload: 2 header + 2 length + 2 footer.</summary>
    public const int FrameOverhead = 6;

    /// <summary>
    /// Builds a complete framed packet ready to write to the socket.
    /// <paramref name="sequence"/> is the session's current incoming sequence
    /// counter - the original server echoes it back without incrementing.
    /// </summary>
    public static byte[] Encode(Packet packet, JvCryption? crypto, bool useCrypto, uint sequence)
    {
        byte[] payload;

        if (useCrypto && crypto is not null)
        {
            payload = new byte[packet.Length + 1 + 5];
            BinaryPrimitives.WriteUInt16LittleEndian(payload, 0x1EFC);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), (ushort)sequence);
            payload[4] = 0;
            payload[5] = packet.Opcode;
            packet.Payload.CopyTo(payload.AsSpan(6));

            crypto.EncryptFast(payload);
        }
        else
        {
            payload = new byte[packet.Length + 1];
            payload[0] = packet.Opcode;
            packet.Payload.CopyTo(payload.AsSpan(1));
        }

        var frame = new byte[payload.Length + FrameOverhead];
        frame[0] = HeaderByte1;
        frame[1] = HeaderByte2;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), (ushort)payload.Length);
        payload.CopyTo(frame.AsSpan(4));
        frame[^2] = FooterByte1;
        frame[^1] = FooterByte2;
        return frame;
    }

    /// <summary>
    /// Port of KOSocket::SendCompressed. Packets below 500 payload bytes are
    /// returned unchanged (the caller sends them normally); this returns null
    /// in that case. Otherwise returns a WIZ_COMPRESS_PACKET packet wrapping
    /// the LZF-compressed [opcode][data] blob, to be sent normally.
    /// </summary>
    public static Packet? CompressIfNeeded(Packet packet)
    {
        if (packet.Length < 500)
            return null;

        var buffer = new byte[packet.Length + 1];
        buffer[0] = packet.Opcode;
        packet.Payload.CopyTo(buffer.AsSpan(1));

        var compressed = Lzf.CompressToArray(buffer);
        if (compressed.Length == 0)
            return null; // fall back to uncompressed (original assumed success)

        var crc = KoCrc32.Compute(buffer, 0);

        var result = new Packet(WizOpcode.CompressPacket, compressed.Length + 12);
        result.WriteUInt32((uint)compressed.Length);
        result.WriteUInt32((uint)buffer.Length);
        result.WriteUInt32(crc);
        result.WriteBytes(compressed);
        return result;
    }

    /// <summary>
    /// Decrypts and unwraps one received payload (the bytes between the frame
    /// length field and the footer). Port of KOSocket::DecryptPacket.
    /// On success <paramref name="sequence"/> is advanced to the next expected
    /// sequence value, exactly like the original's ++m_sequence check.
    /// Returns null when the packet is malformed (the original disconnects).
    /// </summary>
    public static Packet? DecodePayload(ReadOnlySpan<byte> payload, JvCryption? crypto, bool useCrypto, ref uint sequence)
    {
        if (payload.Length == 0)
            return null;

        var buffer = payload.ToArray(); // decryption is in-place

        if (useCrypto)
        {
            if (crypto is null)
                return null;

            // Minimum: 4 sequence + 1 opcode + 4 CRC. (The C++ only checked
            // >= 4 and could underflow m_remaining - we are stricter here.)
            if (buffer.Length < 9)
                return null;

            var plainLength = crypto.DecryptWithCrc32(buffer);
            if (plainLength < 0)
                return null;

            var clientSequence = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
            if (clientSequence != sequence + 1)
                return null;
            sequence = clientSequence;

            var opcode = buffer[4];
            return Packet.FromReceived(opcode, buffer.AsSpan(5, plainLength - 5));
        }

        return Packet.FromReceived(buffer[0], buffer.AsSpan(1));
    }
}
