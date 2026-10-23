using System.Net.Sockets;
using System.Reflection;
using System.Text;
using KnightOnline.Database;
using KnightOnline.GameServer;
using KnightOnline.Shared;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Network;
using KnightOnline.Shared.Protocol;

// Milestone 4 (game login / char select vertical slice).
//
// This is a port of the state machine at the top of CUser::HandlePacket
// (GameServer/User.cpp), one gate at a time:
//
//   1. until crypto is on, only WIZ_VERSION_CHECK (0x2B) is answered -
//      uint16(__VERSION) + the JvCryption public key, then EnableCrypto()
//      (CUser::VersionCheck, LoginHandler.cpp);
//   2. until the account is authenticated, only WIZ_LOGIN (0x01) is answered -
//      GAME_LOGIN semantics: int8 nation, -1 on bad credentials, 0 when the
//      account has no nation yet (CUser::LoginProcess + ReqAccountLogIn);
//   3. until a character is selected: WIZ_SEL_NATION (0x05, NATION_SELECT) and
//      WIZ_ALLCHAR_INFO_REQ (0x0C, GetAllCharID + LoadCharInfo) - the latter is
//      what puts the character-select / create screen on the client.
//
// Anything else is logged and ignored, with the same "unhandled prior to
// selecting character" note the original only left in a TRACE. So a client run
// doubles as a protocol capture: the next packet the log shows (WIZ_NEW_CHAR
// once you create a character) is the spec for the next increment.

Console.Title = $"Game Server for Knight Online v{GameVersion.Version} (.NET)";
KoLog.ConfigureFile("Logs/GameServer.log");

var settings = GameServerSettings.Load();
if (!File.Exists(GameServerSettings.ConfigFileName))
    KoLog.Warn($"{GameServerSettings.ConfigFileName} not found - running on defaults; " +
               "copy GameServer.sample.ini and set [DATABASE] to your LogInServer.ini credentials.");

var characters = new MariaDbCharacterRepository(settings.Database);

var port = settings.Port;
if (args.Length > 0 && int.TryParse(args[0], out var parsedPort) && parsedPort is > 0 and < 65536)
    port = parsedPort;

var server = new KoTcpServer<GameSession>(client => new GameSession(client, characters));

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // let RunAsync unwind instead of hard-killing the process
    shutdown.Cancel();
    server.Stop();
};

KoLog.Warn($"GameServer prototype on port {port}: version check, game login, nation select and the " +
           "character list are implemented; everything after that is logged and not answered yet " +
           "(docs/CSHARP_REWRITE_PLAN.md, M4).");
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
/// One game-server session: the crypto gate, then the login gate, then the
/// pre-character-select gate - the opening of CUser::HandlePacket. Packets are
/// logged in both directions so a client run doubles as a protocol capture.
/// </summary>
internal sealed class GameSession : KoSession
{
    /// <summary>Bytes of each captured packet to dump; the opening ones are small.</summary>
    private const int DumpLimit = 128;

    private const int MaxIdSize = 20; // MAX_ID_SIZE, shared/globals.h
    private const int MaxPwSize = 28; // MAX_PW_SIZE, shared/globals.h

    // NEWCHAR_* result codes, shared/globals.h:263-274.
    private const byte NewcharNoMore = 1;
    private const byte NewcharInvalidDetails = 2;
    private const byte NewcharExists = 3;
    private const byte NewcharDbError = 4;
    private const byte NewcharStatTooLow = 11;

    private readonly MariaDbCharacterRepository _characters;

    /// <summary>Port of m_strAccountID: empty until GAME_LOGIN accepts us.</summary>
    private string _accountId = string.Empty;

    public GameSession(TcpClient client, MariaDbCharacterRepository characters)
        : base(client, maxPayloadSize: 8192)
    {
        _characters = characters;
    }

    protected override bool HandlePacket(Packet packet)
    {
        KoLog.Info($"<- {RemoteIp} {Name(packet.Opcode)} ({packet.Length} byte(s)): {Hexdump(packet.Payload)}");

        if (!IsCryptoEnabled)
        {
            // CUser::HandlePacket: "If crypto's not been enabled yet, force the
            // version packet to be sent." Everything else is dropped, not
            // rejected - returning true keeps the session alive either way.
            if (packet.Opcode == WizOpcode.VersionCheck)
                VersionCheck(packet);

            return true;
        }

        if (_accountId.Length == 0)
        {
            // "If we're not authed yet, force us to be before we can do anything else."
            if (packet.Opcode == WizOpcode.Login)
                LoginProcess(packet);

            return true;
        }

        // "If we haven't logged in yet, don't let us hit in-game packets."
        switch (packet.Opcode)
        {
            case WizOpcode.SelNation:
                SelNation(packet);
                break;
            case WizOpcode.AllcharInfoReq:
                AllCharInfo();
                break;
            case WizOpcode.NewChar:
                NewChar(packet);
                break;
            case WizOpcode.SpeedhackCheck:
                // CUser::SpeedHackTime returns immediately while the user is
                // not in game yet, so at this gate the original drops the
                // packet in silence - and so do we. The trace line above still
                // records it (uint8 flag + float client time).
                break;
            default:
                KoLog.Warn($"Unhandled {Name(packet.Opcode)} prior to selecting a character " +
                           "(the original only traced this and carried on).");
                break;
        }

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

    /// <summary>
    /// Port of CUser::LoginProcess + CUser::ReqAccountLogIn
    /// (LoginHandler.cpp / DatabaseThread.cpp): validate the names, ask the
    /// database (GAME_LOGIN), answer with the nation as int8 - -1 rejects,
    /// 0 sends a fresh account to nation select.
    /// </summary>
    private void LoginProcess(Packet packet)
    {
        var result = new Packet(WizOpcode.Login);
        var account = packet.ReadString();
        var password = packet.ReadString();

        if (account.Length == 0 || account.Length > MaxIdSize
            || password.Length == 0 || password.Length > MaxPwSize
            || !WordGuardSystem(account))
        {
            result.WriteSByte(-1);
            Send(result);
            return;
        }

        sbyte nation;
        try
        {
            // The original queued this on its database thread and answered from
            // there; blocking here matches what the LoginServer port does today.
            nation = _characters.GameLoginAsync(account, password).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            KoLog.Error($"Game login for '{account}' could not reach the database.", ex);
            nation = -1;
        }

        if (nation >= 0)
            _accountId = account;

        KoLog.Info($"[ GAME LOGIN - {DateTime.Now:HH:mm:ss} ] ID={account} " +
                   (nation >= 0 ? $"nation={nation}" : "rejected (unknown account or wrong password)"));

        result.WriteSByte(nation);
        Send(result);
    }

    /// <summary>
    /// Port of CUser::SelNationToAgent + CUser::ReqSelectNation
    /// (CharacterSelectionHandler.cpp / DatabaseThread.cpp): only Karus (1) or
    /// El Morad (2) are acceptable; the echo is the nation on success, 0 on
    /// failure (enum Nation, shared/globals.h).
    /// </summary>
    private void SelNation(Packet packet)
    {
        const byte karus = 1;
        const byte elmorad = 2;

        var result = new Packet(WizOpcode.SelNation);
        var nation = packet.ReadByte();
        if (nation != karus && nation != elmorad)
        {
            result.WriteByte(0);
            Send(result);
            return;
        }

        byte selected;
        try
        {
            selected = _characters.NationSelectAsync(_accountId, nation).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            KoLog.Error($"Nation select for '{_accountId}' could not reach the database.", ex);
            selected = 0;
        }

        KoLog.Info($"[ NATION - {DateTime.Now:HH:mm:ss} ] ID={_accountId} nation={selected}");

        result.WriteByte(selected);
        Send(result);
    }

    /// <summary>
    /// Port of CUser::AllCharInfoToAgent + CUser::ReqAllCharInfo: uint8(1), then
    /// the account's three character slots - zero-filled when the account has
    /// no characters yet, which is what opens the create-character screen.
    /// The extra uint8 some later protocols carry (1920+) is not present in 1453.
    /// </summary>
    private void AllCharInfo()
    {
        IReadOnlyList<CharacterSelectInfo> slots;
        try
        {
            slots = _characters.LoadCharacterSelectInfoAsync(_accountId).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            KoLog.Error($"Character list for '{_accountId}' could not reach the database.", ex);
            slots = new[] { CharacterSelectInfo.Empty, CharacterSelectInfo.Empty, CharacterSelectInfo.Empty };
        }

        var result = new Packet(WizOpcode.AllcharInfoReq);
        result.WriteByte(1);
        foreach (var slot in slots)
        {
            result.WriteString(slot.CharacterId)
                .WriteByte(slot.Race)
                .WriteUInt16(slot.Class)
                .WriteByte(slot.Level)
                .WriteByte(slot.Face)
                .WriteByte(slot.Hair)
                .WriteByte(slot.Zone);

            foreach (var item in slot.VisibleEquipment)
                result.WriteUInt32(item.ItemId).WriteUInt16(item.Durability);
        }

        Send(result);
    }

    /// <summary>
    /// Port of CUser::NewCharToAgent + CUser::ReqCreateNewChar
    /// (CharacterSelectionHandler.cpp / DatabaseThread.cpp): the client-side
    /// gates (slot index, class coefficient, 300 stat points, 50 per stat)
    /// run here exactly in the original's order; everything else - nation
    /// coherence, name uniqueness, the inserts - is CREATE_NEW_CHAR's job in
    /// <see cref="MariaDbCharacterRepository.CreateCharacterAsync"/>. The answer
    /// is the single NEWCHAR_* byte the original sends.
    /// </summary>
    private void NewChar(Packet packet)
    {
        var index = packet.ReadByte();
        var name = packet.ReadString();
        var race = packet.ReadByte();
        var characterClass = packet.ReadUInt16();
        var face = packet.ReadByte();
        var hair = packet.ReadByte();
        var strong = packet.ReadByte();
        var sta = packet.ReadByte();
        var dex = packet.ReadByte();
        var intel = packet.ReadByte();
        var cha = packet.ReadByte();

        var statTotal = strong + sta + dex + intel + cha;

        byte code;
        if (index > 2)
            code = NewcharNoMore;
        else if (statTotal > 300)
            code = NewcharInvalidDetails;
        else if (!_characters.HasCoefficientAsync(characterClass).GetAwaiter().GetResult())
            code = NewcharInvalidDetails;
        else if (strong < 50 || sta < 50 || dex < 50 || intel < 50 || cha < 50)
            code = NewcharStatTooLow;
        else
        {
            try
            {
                code = _characters
                    .CreateCharacterAsync(_accountId, index, name, race, characterClass, hair, face,
                        strong, sta, dex, intel, cha)
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                KoLog.Error($"Character creation for '{name}' could not reach the database.", ex);
                code = NewcharDbError;
            }
        }

        KoLog.Info($"[ NEW CHAR - {DateTime.Now:HH:mm:ss} ] ID={_accountId} char={name} " +
                   $"race={race} class={characterClass} stats={statTotal} code={code}");

        var result = new Packet(WizOpcode.NewChar);
        result.WriteByte(code);
        Send(result);
    }

    public override bool Send(Packet packet)
    {
        KoLog.Info($"-> {RemoteIp} {Name(packet.Opcode)} ({packet.Length} byte(s))");
        return base.Send(packet);
    }

    /// <summary>Port of CUser::WordGuardSystem - ASCII alphanumerics only.</summary>
    private static bool WordGuardSystem(string word)
    {
        const string allowed = "qwertyuopasdfghjklizxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM1234567890";
        foreach (var c in word)
        {
            if (!allowed.Contains(c))
                return false;
        }

        return word.Length > 0;
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
