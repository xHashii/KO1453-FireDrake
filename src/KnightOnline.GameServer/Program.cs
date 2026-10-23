using System.Net.Sockets;
using System.Text;
using KnightOnline.Shared;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Network;
using KnightOnline.Shared.Protocol;

// Milestone 4+ will grow this into the port of GameServer/ + AIServer/
// (merged, per the rewrite plan). What is here now is deliberately NOT a game
// server: it is a handshake recorder. After the LoginServer authenticates a
// client, the client dials the game server - GameServerDlg.cpp reads
// [SETTINGS] PORT with a default of 15001 - and nothing in this repository was
// listening there. This accepts those connections, logs every packet the client
// sends and answers none of them, so M4 can be written against real bytes from
// a real client instead of guesses.
//
//   dotnet run --project src/KnightOnline.GameServer          # port 15001
//   dotnet run --project src/KnightOnline.GameServer -- 15002 # another port

const int DefaultGamePort = 15001; // GameServer/GameServerDlg.cpp

Console.Title = $"Game Server for Knight Online v{GameVersion.Version} (.NET)";
KoLog.ConfigureFile("Logs/GameServer.log");

var port = DefaultGamePort;
if (args.Length > 0 && int.TryParse(args[0], out var parsedPort) && parsedPort is > 0 and < 65536)
    port = parsedPort;

var server = new KoTcpServer<HandshakeRecorderSession>(client => new HandshakeRecorderSession(client));

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // let RunAsync unwind instead of hard-killing the process
    shutdown.Cancel();
    server.Stop();
};

KoLog.Warn($"Handshake recorder listening on port {port}. This is NOT the game server yet " +
           $"(docs/CSHARP_REWRITE_PLAN.md, milestone M4): packets are logged and never answered, " +
           $"so a client that connects here will wait and then give up.");
KoLog.Info("Milestone order: wire core -> LoginServer (done) -> data layer -> login/char-select vertical slice.");

try
{
    await server.RunAsync(port);
}
catch (Exception ex)
{
    KoLog.Error("Handshake recorder failed to start.", ex);
    return 1;
}

KoLog.Info("Handshake recorder shut down.");
return 0;

/// <summary>
/// Logs what a client sends to the game-server port and replies to nothing.
/// Returning true from <see cref="KoSession.HandlePacket"/> keeps the session
/// open, so the whole opening sequence is captured rather than just its first
/// packet. The framing, JvCryption and CRC layers underneath are the ones
/// KnightOnline.Verification checks byte-for-byte against the C golden vectors.
/// </summary>
internal sealed class HandshakeRecorderSession : KoSession
{
    /// <summary>Bytes of each packet to dump; the opening packets are small.</summary>
    private const int DumpLimit = 128;

    public HandshakeRecorderSession(TcpClient client)
        : base(client, maxPayloadSize: 8192)
    {
    }

    protected override bool HandlePacket(Packet packet)
    {
        KoLog.Info($"<- {RemoteIp} opcode 0x{packet.Opcode:X2}, {packet.Length} byte(s): {Hexdump(packet.Payload)}");
        return true; // stay connected: the next packet is the interesting one
    }

    private static string Hexdump(ReadOnlySpan<byte> payload)
    {
        var take = Math.Min(payload.Length, DumpLimit);
        var builder = new StringBuilder(take * 3 + 16);
        for (var i = 0; i < take; i++)
            builder.Append(payload[i].ToString("X2")).Append(' ');

        if (take < payload.Length)
            builder.Append($"... (+{payload.Length - take} more)");
        return builder.ToString().TrimEnd();
    }
}
