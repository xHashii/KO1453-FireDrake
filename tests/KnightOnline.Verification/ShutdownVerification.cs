using System.Net.Sockets;
using KnightOnline.Shared.Network;
using KnightOnline.Shared.Protocol;

namespace KnightOnline.Verification;

internal static class ShutdownVerification
{
    private sealed class SavingSession(TcpClient client, TaskCompletionSource accepted, TaskCompletionSource saving, TaskCompletionSource release) : KoSession(client)
    {
        protected override bool HandlePacket(Packet packet) => true;
        protected override void OnConnected() => accepted.TrySetResult();
        protected override void OnDisconnected()
        {
            saving.TrySetResult();
            release.Task.GetAwaiter().GetResult(); // Represents an outstanding logout DB save.
        }
    }
    public static async Task RunAsync(Action<string,bool> test)
    {
        var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saving = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new KoTcpServer<SavingSession>(client => new(client,accepted,saving,release));
        var running = server.RunAsync(0);
        using var connection = new TcpClient();
        Task? stopping = null;
        try
        {
            await connection.ConnectAsync("127.0.0.1",server.ListeningPort);
            await accepted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            stopping = Task.Run(server.Stop);
            await saving.Task.WaitAsync(TimeSpan.FromSeconds(10));
            test("server shutdown waits for logout persistence", !running.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            server.Stop();
            if (stopping is not null) await stopping.WaitAsync(TimeSpan.FromSeconds(10));
            await running.WaitAsync(TimeSpan.FromSeconds(10));
        }
        test("server shutdown completes after logout persistence", running.IsCompletedSuccessfully && server.SessionCount == 0);
    }
}
