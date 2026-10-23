using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using KnightOnline.Shared.Diagnostics;

namespace KnightOnline.Shared.Network;

/// <summary>
/// Port of KOSocketMgr/SocketMgr (shared/KOSocketMgr.h, SocketMgr.cpp):
/// accepts connections, hands out socket ids, and runs each session.
/// Replaces the IOCP worker-thread pool with async accept + per-session tasks.
/// </summary>
public sealed class KoTcpServer<TSession> where TSession : KoSession
{
    private readonly Func<TcpClient, TSession> _sessionFactory;
    private readonly ConcurrentDictionary<ushort, TSession> _sessions = new();
    private readonly CancellationTokenSource _shutdown = new();
    private TcpListener? _listener;
    private int _nextSessionId;

    /// <param name="idleTimeout">
    /// Disconnect sessions with no valid packet for this long (the original
    /// used KOSOCKET_TIMEOUT = 30s on the game server). Null = disabled.
    /// </param>
    public KoTcpServer(Func<TcpClient, TSession> sessionFactory, TimeSpan? idleTimeout = null)
    {
        _sessionFactory = sessionFactory;
        IdleTimeout = idleTimeout;
    }

    public TimeSpan? IdleTimeout { get; }

    public int SessionCount => _sessions.Count;

    public IEnumerable<TSession> Sessions => _sessions.Values;

    public async Task RunAsync(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        KoLog.Info($"Listening on port {port}...");

        var sweeper = IdleTimeout is null ? null : SweepIdleSessionsAsync(_shutdown.Token);

        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_shutdown.Token).ConfigureAwait(false);
                var session = _sessionFactory(client);
                session.SessionId = AllocateSessionId();
                _sessions[session.SessionId] = session;
                _ = RunSessionAsync(session);
            }
        }
        catch (OperationCanceledException) { /* Stop() called */ }
        finally
        {
            foreach (var session in _sessions.Values)
                session.Disconnect();
            if (sweeper is not null)
                await sweeper.ConfigureAwait(false);
        }
    }

    public void Stop()
    {
        _shutdown.Cancel();
        _listener?.Stop();
    }

    private ushort AllocateSessionId()
    {
        // Original assigns lowest free id from its pool; a wrapping counter
        // with collision skip is equivalent for our purposes.
        while (true)
        {
            var id = (ushort)Interlocked.Increment(ref _nextSessionId);
            if (id != 0 && !_sessions.ContainsKey(id))
                return id;
        }
    }

    private async Task RunSessionAsync(TSession session)
    {
        try
        {
            await session.RunAsync(_shutdown.Token).ConfigureAwait(false);
        }
        finally
        {
            _sessions.TryRemove(session.SessionId, out _);
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task SweepIdleSessionsAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                var cutoff = DateTimeOffset.UtcNow - IdleTimeout!.Value;
                foreach (var session in _sessions.Values)
                {
                    if (session.LastResponseUtc < cutoff)
                    {
                        KoLog.Info($"Session {session.SessionId} ({session.RemoteIp}) timed out.");
                        session.Disconnect();
                    }
                }
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }
}
