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

KoLog.Warn($"GameServer prototype on port {port}: version check, game login, nation select, the " +
           "character list, creation/selection and the world-entry info dump (MYINFO) are implemented; " +
           "deeper world systems are logged and not answered yet (docs/CSHARP_REWRITE_PLAN.md, M4/M5).");
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

    // STARTER_SEED_QUEST (Define.h:224) and MAX_PLAYER_HP (Define.h:22).
    private const ushort StarterSeedQuest = 500;
    private const int MaxPlayerHp = 14000;

    // Inventory geometry, shared/globals.h:226-251: 14 equipped slots, 28
    // inventory slots, 5 cospre, 2 magic bags of 12 - 73 slots in total,
    // 8 bytes each inside USERDATA.strItem (uint32 id, int16 durability,
    // int16 count; DBAgent::LoadUserData).
    private const int SlotMax = 14;
    private const int HaveMax = 28;
    private const int InventoryTotal = 73;

    /// <summary>Session ids handed out like the C++ socket slots (1000+).</summary>
    private static int _lastSocketId = 1000;

    private readonly MariaDbCharacterRepository _characters;
    private readonly ushort _socketId;

    /// <summary>Port of m_strAccountID: empty until GAME_LOGIN accepts us.</summary>
    private string _accountId = string.Empty;

    /// <summary>Set by WIZ_SEL_CHAR: the LOAD_USER_DATA row and m_strUserID.</summary>
    private UserData? _user;
    private string _charId = string.Empty;

    /// <summary>GAMESTART opcode 2 flips CUser::m_state to GAME_STATE_INGAME.</summary>
    private bool _inGame;

    public GameSession(TcpClient client, MariaDbCharacterRepository characters)
        : base(client, maxPayloadSize: 8192)
    {
        _characters = characters;
        _socketId = unchecked((ushort)Interlocked.Increment(ref _lastSocketId));
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

        if (_user is not null)
        {
            // Past WIZ_SEL_CHAR: the in-game half of CUser::HandlePacket's switch.
            switch (packet.Opcode)
            {
                case WizOpcode.Gamestart:
                    GameStart(packet);
                    break;
                case WizOpcode.ServerIndex:
                    SendServerIndex();
                    break;
                case WizOpcode.SpeedhackCheck:
                    // CUser::SpeedHackTime: a server-side check only, never answered.
                    break;
                case WizOpcode.Rental:
                    // CUser::RentalSystem reads the sub-opcode, but the premium/PvP
                    // rental handling is commented out in the original: no answer.
                    break;
                default:
                    KoLog.Warn($"Unhandled {Name(packet.Opcode)} in game - that system is not ported yet (M5+).");
                    break;
            }

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
            case WizOpcode.SelChar:
                SelChar(packet);
                break;
            case WizOpcode.SpeedhackCheck:
                // CUser::SpeedHackTime returns immediately while the user is
                // not in game yet, so at this gate the original drops the
                // packet in silence - and so do we. The trace line above still
                // records it (uint8 flag + float client time).
                break;
            default:
                KoLog.Warn($"Unhandled {Name(packet.Opcode)} at the current gate " +
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

    /// <summary>
    /// Port of CUser::ReqSelectCharacter + the answer half of
    /// CUser::SelectCharacter (DatabaseThread.cpp). One deliberate deviation:
    /// the 1453 client prefixes the character name with the account name
    /// (captured: string account, string char, uint8 init), which the C++
    /// source - written against a later client - does not read; the capture
    /// wins, and the account is verified against the session. The success
    /// answer is the original's: result byte, init echo, zone, spawn X/Z/Y,
    /// old-victory flag. The warehouse/premium/saved-magic loads that gate
    /// the original collapse to the warehouse existence check inside
    /// <see cref="MariaDbCharacterRepository.LoadUserDataAsync"/>.
    /// </summary>
    private void SelChar(Packet packet)
    {
        var account = packet.ReadString();
        var charId = packet.ReadString();
        var init = packet.ReadByte();

        UserData? user = null;
        if (string.Equals(account, _accountId, StringComparison.Ordinal) && charId.Length > 0)
        {
            try
            {
                user = _characters.LoadUserDataAsync(account, charId).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                KoLog.Error($"Loading user data for '{charId}' could not reach the database.", ex);
            }
        }

        var result = new Packet(WizOpcode.SelChar);
        if (user is null)
        {
            KoLog.Info($"[ SEL CHAR - {DateTime.Now:HH:mm:ss} ] ID={_accountId} char={charId} rejected");
            result.WriteByte(0);
            Send(result);
            return;
        }

        KoLog.Info($"[ SEL CHAR - {DateTime.Now:HH:mm:ss} ] ID={_accountId} char={charId} " +
                   $"zone={user.Zone} spawn={user.Px},{user.Pz},{user.Py} level={user.Level}");

        _user = user;
        _charId = charId;

        // GetSPosX() = uint16(world * 10) and the stored PX is world * 100.
        result.WriteByte(1)
            .WriteByte(init)
            .WriteUInt16(user.Zone)
            .WriteUInt16((ushort)(user.Px / 10))
            .WriteUInt16((ushort)(user.Pz / 10))
            .WriteUInt16((ushort)(user.Py / 10))
            .WriteByte(0); // g_pMain->m_byOldVictory
        Send(result);
    }

    /// <summary>
    /// Port of CUser::GameStart (CharacterSelectionHandler.cpp). Opcode 1 is
    /// the loading-screen pull: quest data, the full self-info dump, notices,
    /// time, weather and an empty WIZ_GAMESTART ack. The UserInOutForMe /
    /// MerchantUserInOutForMe / NpcInOutForMe pushes (the region system:
    /// nearby players, merchants and the NPC spawn tables) are the next
    /// increment - until then the world renders empty. Opcode 2 flips the
    /// session in game (respawn, blink, ability recalc and temple checks come
    /// with combat). The character name after the opcode is single-byte
    /// length; the original does not read it, and neither does the port.
    /// </summary>
    private void GameStart(Packet packet)
    {
        if (_inGame || _user is null)
            return; // CUser::GameStart: if (isInGame()) return;

        var opcode = packet.ReadByte();
        if (opcode == 1)
        {
            SendQuestData();
            SendMyInfo(); // calls QuestDataRequest first in the original; hoisted for clarity
            SendNotice();
            SendTime();
            SendWeather();
            Send(new Packet(WizOpcode.Gamestart));
        }
        else if (opcode == 2)
        {
            _inGame = true;
        }
    }

    /// <summary>
    /// Port of CUser::QuestDataRequest: WIZ_QUEST sub-opcode 1, uint16 entry
    /// count, then (uint16 quest id, uint8 state) per entry - std::map order,
    /// so sorted by id. Entries come from USERDATA.strQuest as 3-byte records
    /// (DBAgent::LoadUserData); the original also seeds STARTER_SEED_QUEST at
    /// state 1 when it is missing.
    /// </summary>
    private void SendQuestData()
    {
        var user = _user!;
        var quests = new SortedDictionary<ushort, byte>();
        var count = Math.Min(user.QuestCount, (short)(user.Quests.Length / 3)); // QUEST_LIMIT guard
        for (var i = 0; i < count; i++)
        {
            var id = (ushort)(user.Quests[i * 3] | (user.Quests[i * 3 + 1] << 8));
            quests[id] = user.Quests[i * 3 + 2];
        }

        if (!quests.ContainsKey(StarterSeedQuest))
            quests[StarterSeedQuest] = 1;

        var result = new Packet(WizOpcode.Quest, 1);
        result.WriteUInt16((ushort)quests.Count);
        foreach (var (id, state) in quests)
            result.WriteUInt16(id).WriteByte(state);
        Send(result);
    }

    /// <summary>
    /// Port of CUser::SendMyInfo (User.cpp:883) - the "character data" the
    /// loading bar waits for. The derived maxima follow SetMaxHp/SetMaxMp and
    /// SetUserAbility's base weight formula using the COEFFICIENT and LEVEL_UP
    /// rows; everything the item system would contribute (stat bonuses, total
    /// hit/AC, resistances, item weight) stays 0 until equipment parsing is
    /// ported. The clan branch always takes the null-knights triple (uint64 0,
    /// uint8 0, uint16 0xFFFF) - loading KNIGHTS rows is a later increment.
    /// Serials, rental flags and expirations are 0: DBAgent pairs those from
    /// strSerial and the rental tables, which the port does not consult yet.
    /// </summary>
    private void SendMyInfo()
    {
        var user = _user!;
        var coefficient = _characters.LoadCoefficientAsync(user.Class).GetAwaiter().GetResult();
        var maxExp = _characters.LoadLevelExpAsync(user.Level).GetAwaiter().GetResult();

        int level = user.Level, str = user.Str, sta = user.Sta, intel = user.Int;
        var maxHp = (int)user.Hp;
        var maxMp = (int)user.Mp;
        if (coefficient is not null)
        {
            maxHp = (int)Math.Min(MaxPlayerHp,
                coefficient.Hp * level * level * sta + 0.1f * level * sta + sta / 5 + 20);
            maxMp = (int)(coefficient.Mp != 0
                ? coefficient.Mp * level * level * (intel + 30) + 0.1f * level * 2 * (intel + 30) + (intel + 30) / 5 + 20
                : coefficient.Sp != 0
                    ? coefficient.Sp * level * level * sta + 0.1f * level * sta + sta / 5
                    : 0);
        }

        var maxWeight = (ushort)Math.Min(32767, (str + level) * 50);

        var result = new Packet(WizOpcode.Myinfo);
        result.SByte(); // the character name has a single byte length
        result.WriteUInt16(_socketId)
            .WriteString(_charId)
            .WriteUInt16((ushort)(user.Px / 10))
            .WriteUInt16((ushort)(user.Pz / 10))
            .WriteUInt16((ushort)(user.Py / 10))
            .WriteByte(user.Nation)
            .WriteByte(user.Race)
            .WriteUInt16(user.Class)
            .WriteByte(user.Face)
            .WriteByte(user.Hair)
            .WriteByte(0) // m_bRank - the king system recomputes it; not ported
            .WriteByte(user.Title)
            .WriteByte(user.Level)
            .WriteUInt16((ushort)user.Points)
            .WriteUInt32((uint)maxExp)
            .WriteUInt32((uint)user.Exp)
            .WriteUInt32((uint)user.Loyalty)
            .WriteUInt32((uint)user.LoyaltyMonthly)
            .WriteByte(user.City)
            .WriteUInt16((ushort)user.Knights);

        // The original's null-knights branch (clan loading is a later increment).
        result.WriteUInt64(0)
            .WriteByte(0)
            .WriteUInt16(0xFFFF);

        result.WriteInt16((short)maxHp)
            .WriteUInt16((ushort)user.Hp)
            .WriteInt16((short)maxMp)
            .WriteUInt16((ushort)user.Mp)
            .WriteUInt16(maxWeight)
            .WriteUInt16(0) // m_sItemWeight - item system
            .WriteByte(user.Str).WriteByte(0)
            .WriteByte(user.Sta).WriteByte(0)
            .WriteByte(user.Dex).WriteByte(0)
            .WriteByte(user.Int).WriteByte(0)
            .WriteByte(user.Cha).WriteByte(0)
            .WriteUInt16(0) // m_sTotalHit - item system
            .WriteUInt16(0) // m_sTotalAc - item system
            .WriteByte(0).WriteByte(0).WriteByte(0) // fire, cold, lightning resist
            .WriteByte(0).WriteByte(0).WriteByte(0) // magic, disease, poison resist
            .WriteUInt32((uint)user.Gold)
            .WriteByte(user.Authority)
            .WriteByte(0) // m_bKnightsRank
            .WriteByte(0); // m_bPersonalRank

        // m_bstrSkill: nine raw skill-point bytes (USERDATA.strSkill is varbinary(10)).
        for (var i = 0; i < 9; i++)
            result.WriteByte(i < user.Skill.Length ? user.Skill[i] : (byte)0);

        WriteItemSlots(result, user.Items, 0, SlotMax, full: true);
        // Cospre: the original loops i = 43; i <= 46; i += 3 - slots 43 and 46 only.
        WriteItemSlots(result, user.Items, 43, 1, full: false);
        WriteItemSlots(result, user.Items, 46, 1, full: false);
        WriteItemSlots(result, user.Items, SlotMax, HaveMax, full: true);

        result.WriteByte(0) // m_bAccountStatus
            .WriteByte(0) // m_bPremiumType
            .WriteUInt16(0) // m_sPremiumTime
            .WriteByte(0) // m_bIsChicken - CheckExistEvent(50, 1), quest system
            .WriteUInt32((uint)user.MannerPoint);

        Send(result);
    }

    /// <summary>
    /// Writes `slots` inventory slots starting at `firstSlot` in the wire
    /// format SendMyInfo uses: uint32 id, uint16 durability, uint16 count,
    /// uint8 flag, uint16 remaining rental time, [uint32 unknown - only in the
    /// "full" equipped/inventory format], uint32 expiration. Slots read from
    /// USERDATA.strItem's 8-byte records; missing blob data stays zeroed.
    /// </summary>
    private static void WriteItemSlots(Packet result, byte[] blob, int firstSlot, int slots, bool full)
    {
        for (var i = 0; i < slots; i++)
        {
            var slot = firstSlot + i;
            uint id = 0;
            ushort duration = 0, count = 0;
            if (slot < InventoryTotal && blob.Length >= (slot + 1) * 8)
            {
                id = BitConverter.ToUInt32(blob, slot * 8);
                duration = BitConverter.ToUInt16(blob, slot * 8 + 4);
                count = BitConverter.ToUInt16(blob, slot * 8 + 6);
            }

            result.WriteUInt32(id)
                .WriteUInt16(duration)
                .WriteUInt16(count)
                .WriteByte(0) // bFlag (rented etc.) - rental system
                .WriteUInt16(0); // sRemainingRentalTime
            if (full)
                result.WriteUInt32(0); // unknown; always 0 in the original
            result.WriteUInt32(0); // nExpirationTime - strItemTime blob, not ported
        }
    }

    /// <summary>
    /// Port of CUser::SendNotice: WIZ_NOTICE, style 2, uint8 count, then
    /// (title, message) pairs. The original fills the list from the notice
    /// file plus AppendExtraNoticeData's event bars (server resource strings);
    /// neither exists in the port yet, so the list goes out empty.
    /// </summary>
    private void SendNotice()
    {
        var result = new Packet(WizOpcode.Notice);
        result.WriteByte(2).WriteByte(0);
        Send(result);
    }

    /// <summary>Port of CUser::SendTime: the server clock as five uint16 fields.</summary>
    private void SendTime()
    {
        var now = DateTime.Now;
        var result = new Packet(WizOpcode.Time);
        result.WriteUInt16((ushort)now.Year)
            .WriteUInt16((ushort)now.Month)
            .WriteUInt16((ushort)now.Day)
            .WriteUInt16((ushort)now.Hour)
            .WriteUInt16((ushort)now.Minute);
        Send(result);
    }

    /// <summary>Port of CUser::SendWeather: fine weather (0), no amount.</summary>
    private void SendWeather()
    {
        var result = new Packet(WizOpcode.Weather);
        result.WriteByte(0).WriteInt16(0);
        Send(result);
    }

    /// <summary>
    /// Port of CUser::SendServerIndex: uint16(1) plus the server number.
    /// Every ZONE_INFO row of this database is ServerNo 1, so the port says 1.
    /// </summary>
    private void SendServerIndex()
    {
        var result = new Packet(WizOpcode.ServerIndex);
        result.WriteUInt16(1).WriteUInt16(1);
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
