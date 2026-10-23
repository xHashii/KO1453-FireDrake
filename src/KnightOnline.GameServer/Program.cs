using System.Net.Sockets;
using System.Reflection;
using System.Text;
using KnightOnline.Shared;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Network;
using KnightOnline.Shared.Protocol;

// Milestone 4 (game login / char select vertical slice) starts here.
//
// What is implemented is exactly the opening of CUser::HandlePacket
// (GameServer/User.cpp + LoginHandler.cpp): until crypto is enabled the session
// answers nothing but WIZ_VERSION_CHECK (0x2B) - uint16(__VERSION) followed by
// the JvCryption public key - and then turns encryption on. That is the packet
// a real 1453 client sends first ("opcode 0x2B, 2 byte(s): FF FF", the int16 -1
// the original's commented-out code used to read), and answering it is what
// moves the client past "Connecting. Please wait."
//
// Everything after the handshake is still RECORDED, not answered: post-crypto
// packets arrive decrypted (the framing/crypt layers are the ones
// KnightOnline.Verification checks against the C golden vectors), so the log is
// now a readable capture of what the client expects next - WIZ_LOGIN first.
// A client will therefore sit at "Connecting." again one step further on;
// that is M4's next increment, not a regression.

const int DefaultGamePort = 15001; // GameServer/GameServerDlg.cpp [SETTINGS] PORT

Console.Title = $"Game Server for Knight Online v{GameVersion.Version} (.NET)";
KoLog.ConfigureFile("Logs/GameServer.log");

var port = DefaultGamePort;
if (args.Length > 0 && int.TryParse(args[0], out var parsedPort) && parsedPort is > 0 and < 65536)
    port = parsedPort;

var server = new KoTcpServer<GameSession>(client => new GameSession(client));

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // let RunAsync unwind instead of hard-killing the process
    shutdown.Cancel();
    server.Stop();
};

KoLog.Warn($"GameServer prototype on port {port}: the WIZ_VERSION_CHECK handshake is answered, " +
           $"everything after it is logged and not answered yet (docs/CSHARP_REWRITE_PLAN.md, M4).");
KoLog.Info("Milestone order: wire core -> LoginServer (done) -> data layer (done) -> login/char-select vertical slice.");

try
{
    await server.RunAsync(port);
}
catch (Exception ex)
{
    KoLog.Error("GameServer failed to start.", ex);
    return 1;
}

KoLog.Info("GameServer shut down.");
return 0;

/// <summary>
/// The opening of a game-server session. Port of the state machine at the top
/// of CUser::HandlePacket: crypto first, then WIZ_LOGIN, then (M4) character
/// selection. Packets are logged in both directions so a client run doubles as
/// a protocol capture.
/// </summary>
internal sealed class GameSession : KoSession
{
    /// <summary>Bytes of each captured packet to dump; the opening ones are small.</summary>
    private const int DumpLimit = 128;

    public GameSession(TcpClient client)
        : base(client, maxPayloadSize: 8192)
    {
    }

    protected override bool HandlePacket(Packet packet)
    {
        KoLog.Info($"<- {RemoteIp} {Name(packet.Opcode)} ({packet.Length} byte(s)): {Hexdump(packet.Payload)}");

        if (!IsCryptoEnabled)
        {
            // CUser::HandlePacket: "If crypto's not been enabled yet, force the
            // version packet to be sent." Everything else is dropped, not
            // rejected - return true keeps the session alive either way.
            if (packet.Opcode == WizOpcode.VersionCheck)
                VersionCheck(packet);

            return true;
        }

        // Decrypted and decoded, but not implemented yet: WIZ_LOGIN is expected
        // here, then character selection. The hexdump in the log above is the
        // material the next increment is written from.
        return true;
    }

    /// <summary>Port of CUser::VersionCheck (GameServer/LoginHandler.cpp).</summary>
    private void VersionCheck(Packet packet)
    {
        var result = new Packet(WizOpcode.VersionCheck);
        result.WriteUInt16((ushort)GameVersion.Version);
        result.WriteUInt64(Crypto.GenerateKey());
        Send(result);

        EnableCrypto();
    }

    public override bool Send(Packet packet)
    {
        KoLog.Info($"-> {RemoteIp} {Name(packet.Opcode)} ({packet.Length} byte(s))");
        return base.Send(packet);
    }

    /// <summary>WIZ_* names from <see cref="WizOpcode"/>, for readable traces.</summary>
    private static string Name(byte opcode) =>
        WizNames.TryGetValue(opcode, out var name) ? name : $"opcode 0x{opcode:X2}";

    private static readonly Dictionary<byte, string> WizNames = BuildNames();

    private static Dictionary<byte, string> BuildNames()
    {
        var names = new Dictionary<byte, string>();
        foreach (var field in typeof(WizOpcode).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.IsLiteral && field.FieldType == typeof(byte))
                names.TryAdd((byte)field.GetValue(null)!, field.Name);
        }

        return names;
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
