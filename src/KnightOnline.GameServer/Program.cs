using System.Net.Sockets;
using System.Reflection;
using System.Text;
using KnightOnline.Database;
using KnightOnline.Database.Items;
using KnightOnline.Database.World;
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
KoLog.StartRun("GameServer");

var settings = GameServerSettings.Load();
if (!File.Exists(GameServerSettings.ConfigFileName))
    KoLog.Warn($"{GameServerSettings.ConfigFileName} not found - running on defaults; " +
               "copy GameServer.sample.ini and set [DATABASE] to your LogInServer.ini credentials.");

var characters = new MariaDbCharacterRepository(settings.Database);
try
{
    await characters.EnsureTravelSchemaAsync();
    await characters.EnsureCombatSchemaAsync();
    await characters.EnsureFoundationSchemaAsync();
    await characters.EnsurePotionDataAsync();
    var catalogRepair = await characters.RepairItemCatalogAsync();
    KoLog.Info($"Item catalog: restored {catalogRepair.Restored} missing ITEM row(s) from {catalogRepair.SourceCount} shipped definitions; existing definitions and saved inventory unchanged.");
}
catch (Exception ex)
{
    KoLog.Error("Game data initialization failed. Check database connectivity and ITEM/KO_WORLD_TRAVEL table permissions before retrying.", ex);
    return 1;
}

if (args.Length == 2 && args[0] == "--grant-gm")
{
    try
    {
        var count = await characters.GrantGameMasterAsync(args[1]);
        KoLog.Info($"GM promotion: account '{args[1]}', {count} owned character(s). Log out and reconnect to load authority 0.");
        return count > 0 ? 0 : 1;
    }
    catch (Exception ex)
    {
        KoLog.Error("GM promotion failed; no promotion was committed.", ex);
        return 1;
    }
}

var port = settings.Port;
if (args.Length > 0 && int.TryParse(args[0], out var parsedPort) && parsedPort is > 0 and < 65536)
    port = parsedPort;

WorldCatalog world;
try
{
    world = new WorldCatalog(await characters.LoadWorldDataAsync(), WorldCatalog.FindMapDirectory());
}
catch (Exception ex)
{
    KoLog.Error("World NPC/map loading failed. Fix the reported asset or data reference before starting.", ex);
    return 1;
}
var chat = new ChatHub();
var players = new PlayerHub();
var server = new KoTcpServer<GameSession>(client => new GameSession(client, characters, world, settings.CharacterPreviewEquipment, chat, players),maxSessionId:9999);

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // let RunAsync unwind instead of hard-killing the process
    shutdown.Cancel();
    server.Stop();
};

KoLog.Warn($"GameServer prototype on port {port}: version check, game login, nation select, the " +
           "character list, creation/selection, NPC/monster spawns, PvE attacks/skills/retaliation, XP/loot, NPC shops, warehouses, saved skill bars, nearby players, anvil windows and native warp gates are implemented; " +
           "repairs/upgrades, quests, full chasing AI, parties and unsupported skill types remain unported (docs/CSHARP_REWRITE_PLAN.md, M4/M5).");
KoLog.Info($"Build: {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}; NPC shop transactions enabled.");

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
internal sealed partial class GameSession : KoSession
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

    // MAX_PLAYER_HP (Define.h:22).
    private const int MaxPlayerHp = 14000;

    // Inventory geometry, shared/globals.h:226-251: 14 equipped slots, 28
    // inventory slots, 5 cospre, 2 magic bags of 12 - 73 slots in total,
    // 8 bytes each inside USERDATA.strItem (uint32 id, int16 durability,
    // int16 count; DBAgent::LoadUserData).
    private const int SlotMax = 14;
    private const int HaveMax = 28;
    private const int InventoryTotal = 73;
    private const int CospreStart = 42;   // SLOT_MAX + HAVE_MAX
    private const int CospreCount = 5;
    private const int MagicBagStart = 49; // + MBAG_COUNT
    private const int RightHand = 6;      // equipment position order, ItemBlobs.Describe
    private const int LeftHand = 8;

    private readonly MariaDbCharacterRepository _characters;
    private readonly bool _characterPreviewEquipment;
    private ushort _socketId;

    /// <summary>Port of m_strAccountID: empty until GAME_LOGIN accepts us.</summary>
    private string _accountId = string.Empty;

    /// <summary>Set by WIZ_SEL_CHAR: the LOAD_USER_DATA row and m_strUserID.</summary>
    private UserData? _user;
    private CharacterEditLease? _editLease;
    private Timer? _editLeaseTimer;
    private string _charId = string.Empty;

    /// <summary>GAMESTART opcode 2 flips CUser::m_state to GAME_STATE_INGAME.</summary>
    private bool _inGame;

    // Equipment state: the slots parsed from USERDATA.strItem (DBAgent::
    // LoadUserData) plus everything SetUserAbility derives from them.
    private ItemSlotData[] _slots = Array.Empty<ItemSlotData>();
    private int _slotCount;
    private IReadOnlyDictionary<uint, ItemProto> _protos = new Dictionary<uint, ItemProto>();
    private int _currentHp, _currentMp;
    private short _maxHp, _maxMp;
    private ushort _totalHit;
    private short _totalAc;
    private ushort _maxWeight;
    private int _itemWeight;
    private readonly short[] _statBonus = new short[5];
    private readonly short[] _resist = new short[6];

    // Mutable identity stats: the DB record is immutable, while the original
    // mutates m_bStats/m_sPoints in place and saves them on logout.
    private readonly byte[] _stats = new byte[5];
    private short _points;

    public GameSession(TcpClient client, MariaDbCharacterRepository characters, WorldCatalog world, bool characterPreviewEquipment, ChatHub chat, PlayerHub players)
        : base(client, maxPayloadSize: 8192)
    {
        _characters = characters;
        _chat = chat;
        _players = players;
        _characterPreviewEquipment = characterPreviewEquipment;
        _world = world;
    }

    protected override bool HandlePacket(Packet packet)
    {
        lock (_gameGate)
        {
            try { return HandleGamePacket(packet); }
            finally { UpdateChatPresence(); UpdatePlayerPresence(); }
        }
    }

    private bool HandleGamePacket(Packet packet)
    {
        if (_inventoryFaulted) return false;
        KoLog.Info($"<- {RemoteIp} {Name(packet.Opcode)} ({packet.Length} byte(s)): {PacketTrace.GamePayload(packet)}");

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
            if (_travelStage != 0 && packet.Opcode != WizOpcode.ZoneChange
                && packet.Opcode != WizOpcode.SpeedhackCheck)
                return true;

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
                case WizOpcode.Move:
                    WorldMove(packet);
                    break;
                case WizOpcode.Rotate:
                    PlayerRotate(packet); break;
                case WizOpcode.ReqUserin:
                    RequestedPlayers(packet); break;
                case WizOpcode.Skilldata:
                    SkillBar(packet); break;
                case WizOpcode.Warehouse:
                    Warehouse(packet); break;
                case WizOpcode.ObjectEvent:
                    WorldObjectEvent(packet);
                    break;
                case WizOpcode.NpcEvent:
                    WorldNpcEvent(packet);
                    break;
                case WizOpcode.ReqNpcin:
                    WorldNpcRequest(packet);
                    break;
                case WizOpcode.MagicProcess:
                    ProcessMagic(packet);
                    break;
                case WizOpcode.BundleOpenReq:
                    OpenLoot(packet);
                    break;
                case WizOpcode.ItemGet:
                    PickupLoot(packet);
                    break;
                case WizOpcode.SkillptChange:
                    AllocateSkillPoint(packet);
                    break;
                case WizOpcode.Regene:
                    RevivePlayer(packet);
                    break;
                case WizOpcode.Attack:
                    MeleeAttack(packet);
                    break;
                case WizOpcode.TargetHp:
                    TargetHp(packet);
                    break;
                case WizOpcode.ItemTrade:
                    ItemTrade(packet);
                    break;
                case WizOpcode.ItemRepair:
                    Send(new Packet(WizOpcode.ItemRepair).WriteByte(0).WriteUInt32((uint)_user.Gold));
                    TravelMessage("Repair processing is not implemented yet; no coins or items changed.");
                    break;
                case WizOpcode.ItemUpgrade:
                    RejectWorldUpgrade(packet);
                    break;
                case WizOpcode.StateChange:
                    BasicStateChange(packet);
                    break;
                case WizOpcode.Home:
                    Town();
                    break;
                case WizOpcode.Chat:
                    TravelChat(packet);
                    break;
                case WizOpcode.ChatTarget:
                    ChatTargetSelect(packet);
                    break;
                case WizOpcode.WarpList:
                    SelectTravel(packet);
                    break;
                case WizOpcode.ZoneChange:
                    FinishTravel(packet);
                    break;
                case WizOpcode.ItemMove:
                    ItemMove(packet);
                    break;
                case WizOpcode.PointChange:
                    PointChange(packet);
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

        var result = CharacterListPackets.Build(slots, _characterPreviewEquipment);
        KoLog.Info($"[ CHAR PREVIEW ] equipmentModels={(_characterPreviewEquipment ? "enabled" : "suppressed (recovery mode)")}; saved inventory unchanged");
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.CharacterId.Length == 0) continue;
            KoLog.Info($"[ CHAR PREVIEW ] slot={i} char={slot.CharacterId} race={slot.Race} class={slot.Class} level={slot.Level} face={slot.Face} hair={slot.Hair} zone={slot.Zone} " +
                $"savedEquipment={string.Join(",", slot.VisibleEquipment.Select(item => item?.ItemId ?? 0))}");
        }
        KoLog.Info($"[ CHAR PREVIEW ] reply={Convert.ToHexString(result.Payload)}");
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
    /// answer is the original's: result byte, uint8 zone, spawn X/Z/Y,
    /// old-victory flag. The warehouse/premium/saved-magic loads that gate
    /// the original collapse to the warehouse existence check inside
    /// <see cref="MariaDbCharacterRepository.LoadUserDataAsync"/>.
    /// </summary>
    private void SelChar(Packet packet)
    {
        var account = packet.ReadString();
        var charId = packet.ReadString();
        _ = packet.ReadByte(); // bInit: internal selection flag, not part of the success reply

        if (_user is not null) { Send(new Packet(WizOpcode.SelChar).WriteByte(0)); return; }
        UserData? user = null;
        if (string.Equals(account, _accountId, StringComparison.Ordinal) && charId.Length > 0)
        {
            try
            {
                if (_editLease is not null) throw new InvalidOperationException("Character already selected.");
                _editLease = _characters.AcquireEditLease(account, charId);
                user = _characters.LoadUserDataAsync(account, charId).GetAwaiter().GetResult();
                if (user is null) { _editLease.Dispose(); _editLease = null; }
                else _editLeaseTimer = new Timer(_ =>
                {
                    lock (_gameGate)
                    {
                        try { _editLease?.Ping(); }
                        catch (Exception ex) { KoLog.Error("Session protection connection lost; disconnecting.", ex); _inventoryFaulted = true; Disconnect(); }
                    }
                }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                try { _editLease?.Dispose(); } catch { }
                _editLease = null;
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

        _user = user;
        _savedPosition = new(user.Zone,user.Px,user.Pz,user.Py);
        _charId = charId;

        var serialBlob = user.Serials ?? Array.Empty<byte>();
        _slotCount = Math.Max(InventoryTotal, Math.Max((user.Items.Length + 7) / 8, (serialBlob.Length + 7) / 8));
        _slots = ItemBlobs.Parse(user.Items, serialBlob, ReadOnlySpan<byte>.Empty, _slotCount);
        _stats[0] = user.Str; _stats[1] = user.Sta; _stats[2] = user.Dex;
        _stats[3] = user.Int; _stats[4] = user.Cha;
        _points = user.Points;
        RecomputeStats(refillWhenEmpty: true);
        var inventoryAudit = ShopRules.MeasureInventory(_slots, _protos);
        LogInventoryIssues(inventoryAudit.Issues, "character load");
        var retainedTail = _slots.Select((slot, index) => (slot, index))
            .Count(entry => !_protos.ContainsKey(entry.slot.ItemId) && LegacyStarterTail.IsKnownUnusedSlot(entry.index, entry.slot));
        if (retainedTail != 0)
            KoLog.Info($"[ INVENTORY ] {_charId}: {retainedTail} recognized legacy starter-tail records retained unchanged; not counted as active items");
        KoLog.Info($"[ SEL CHAR - {DateTime.Now:HH:mm:ss} ] ID={_accountId} char={charId} " +
                   $"zone={user.Zone} spawn={user.Px},{user.Pz},{user.Py} level={user.Level} items={_protos.Count}");

        // GetSPosX() = uint16(world * 10) and the stored PX is world * 100.
        // SelectCharacter consumes bInit internally; it is NOT echoed on the wire.
        // GetZoneID() returns uint8 here, unlike ZoneChange's explicit uint16 cast.
        Send(TravelPackets.SelectCharacter(user.Zone, user.Px, user.Pz, user.Py));
    }

    /// <summary>
    /// Port of CUser::GameStart (CharacterSelectionHandler.cpp). Opcode 1 is
    /// the loading-screen pull: full self-info, start ACK, quest data, notices,
    /// time, weather, static nearby NPC records and an empty WIZ_GAMESTART ack.
    /// Nearby player visibility is synchronized after loading; merchants and full AI remain unported.
    /// Opcode 2 flips the
    /// session in game (respawn, blink, ability recalc and temple checks come
    /// with combat). The character name after the opcode is single-byte
    /// length; the original does not read it, and neither does the port.
    /// </summary>
    private readonly WorldEntryFlow _entryFlow = new();
    private void GameStart(Packet packet)
    {
        if (_user is null || packet.RemainingToRead < 1) return;
        switch (_entryFlow.Accept(packet.ReadByte()))
        {
            case WorldEntryAction.AcknowledgeOnly:
                Send(new Packet(WizOpcode.Gamestart));
                break;
            case WorldEntryAction.Bootstrap:
                // Native reference: MYINFO first, start ACK, then quest snapshot.
                // Pre-MYINFO quest state can be discarded by client initialization.
                SendMyInfo();
                Send(new Packet(WizOpcode.Gamestart));
                SendQuestData();
                SendNotice(); SendTime(); SendWeather(); SendWorldNpcs();
                break;
            case WorldEntryAction.Enter:
                _inGame = true;
                StartRecovery();
                RecomputeStats(refillWhenEmpty: false);
                UpdatePlayerPresence();
                Send(_players.RegionList(_socketId));
                break;
        }
    }

    private void SendQuestData()
    {
        Send(StarterQuestState.Snapshot(_user!.Quests, _user.QuestCount));
        KoLog.Info($"[ INTRO SNAPSHOT ] {_charId}: quest 500=2 sent after MYINFO/start ACK; repeated stage 1 will ACK only.");
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
        var maxExp = _characters.LoadLevelExpAsync(user.Level).GetAwaiter().GetResult();

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
            .WriteUInt16((ushort)_points)
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

        result.WriteInt16(_maxHp)
            .WriteUInt16((ushort)_currentHp)
            .WriteInt16(_maxMp)
            .WriteUInt16((ushort)_currentMp)
            .WriteUInt16(_maxWeight)
            .WriteUInt16((ushort)Math.Clamp(_itemWeight, 0, short.MaxValue))
            .WriteByte(_stats[0]).WriteByte(ClampByte(_statBonus[0]))
            .WriteByte(_stats[1]).WriteByte(ClampByte(_statBonus[1]))
            .WriteByte(_stats[2]).WriteByte(ClampByte(_statBonus[2]))
            .WriteByte(_stats[3]).WriteByte(ClampByte(_statBonus[3]))
            .WriteByte(_stats[4]).WriteByte(ClampByte(_statBonus[4]))
            .WriteUInt16(_totalHit)
            .WriteUInt16((ushort)_totalAc)
            .WriteByte(ClampByte(_resist[0]))
            .WriteByte(ClampByte(_resist[1]))
            .WriteByte(ClampByte(_resist[2]))
            .WriteByte(ClampByte(_resist[3]))
            .WriteByte(ClampByte(_resist[4]))
            .WriteByte(ClampByte(_resist[5]))
            .WriteUInt32((uint)user.Gold)
            .WriteByte(user.Authority)
            .WriteByte(0) // m_bKnightsRank
            .WriteByte(0); // m_bPersonalRank

        // m_bstrSkill: nine raw skill-point bytes (USERDATA.strSkill is varbinary(10)).
        for (var i = 0; i < 9; i++)
            result.WriteByte(i < user.Skill.Length ? user.Skill[i] : (byte)0);

        WriteSlots(result, 0, SlotMax, full: true);
        // Cospre: the original loops i = 43; i <= 46; i += 3 - slots 43 and 46 only.
        WriteSlots(result, 43, 1, full: false);
        WriteSlots(result, 46, 1, full: false);
        WriteSlots(result, SlotMax, HaveMax, full: true);

        result.WriteByte(0) // m_bAccountStatus
            .WriteByte(0) // m_bPremiumType
            .WriteUInt16(0) // m_sPremiumTime
            .WriteByte(0) // m_bIsChicken - CheckExistEvent(50, 1), quest system
            .WriteUInt32((uint)user.MannerPoint);

        Send(result);
    }

    private static byte ClampByte(short value) => (byte)Math.Clamp(value, (short)0, (short)255);

    /// <summary>
    /// Writes `slots` inventory slots starting at `firstSlot` in the wire
    /// format SendMyInfo uses: uint32 id, uint16 durability, uint16 count,
    /// uint8 flag, uint16 remaining rental time, [uint32 unknown - only in the
    /// "full" equipped/inventory format], uint32 expiration.
    /// </summary>
    private void WriteSlots(Packet result, int firstSlot, int slots, bool full)
    {
        for (var i = 0; i < slots; i++)
        {
            var slot = Slot(firstSlot + i);
            result.WriteUInt32(slot.ItemId)
                .WriteUInt16((ushort)slot.Durability)
                .WriteUInt16((ushort)slot.Count)
                .WriteByte(0) // bFlag (rented etc.) - rental system
                .WriteUInt16(0); // sRemainingRentalTime
            if (full)
                result.WriteUInt32(0); // unknown; always 0 in the original
            result.WriteUInt32(slot.ExpirationUnixTime);
        }
    }

    /// <summary>
    /// Port of CUser::SetSlotItemValue + CUser::SetUserAbility + SetMaxHp/
    /// SetMaxMp (User.cpp): item bonuses, weight and resistances from the
    /// equipped and cospre slots; weapon damage and hit coefficient from the
    /// two hand slots; the class-group AP formulas for total hit; the AC
    /// factor; and the derived HP/MP maxima over the COEFFICIENT row. Set
    /// items, weapon-type AC bonuses, elemental damage maps and passive-skill
    /// bonuses stay out until their tables/systems are ported.
    /// refillWhenEmpty carries the port's one documented deviation: the
    /// original CREATE_NEW_CHAR never writes current HP/MP, so fresh
    /// characters load at 0/0 - the port fills the bars instead of showing a
    /// dead character standing in the world.
    /// </summary>
    private void RecomputeStats(bool refillWhenEmpty)
    {
        _appearanceVersion++;
        var user = _user!;
        Array.Clear(_statBonus);
        Array.Clear(_resist);
        _itemWeight = 0;
        var maxWeightBonus = 0;
        short itemAc = 0, itemMaxHp = 0, itemMaxMp = 0, itemHitrate = 100, itemEvasionrate = 100;

        var ids = _slots.Select(slot => slot.ItemId).Where(id => id != 0).Distinct().ToList();
        _protos = ids.Count == 0
            ? new Dictionary<uint, ItemProto>()
            : _characters.LoadItemPrototypesAsync(ids).GetAwaiter().GetResult();

        for (var i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot.ItemId == 0 || !_protos.TryGetValue(slot.ItemId, out var proto))
                continue;

            // Bags increase max weight, they do not weigh anything.
            if (i == 47 || i == 48)
                maxWeightBonus += proto.Duration;
            else
                _itemWeight += proto.Weight * slot.Count;

            // Unequipped items (inventory, magic bags) grant nothing.
            if ((i >= SlotMax && i < CospreStart) || i >= MagicBagStart)
                continue;

            var ac = proto.Ac;
            if (slot.Durability == 0)
                ac /= 10;

            itemMaxHp += proto.MaxHpB;
            itemMaxMp += proto.MaxMpB;
            itemAc += ac;
            _statBonus[0] += proto.StrB;
            _statBonus[1] += proto.StaB;
            _statBonus[2] += proto.DexB;
            _statBonus[3] += proto.IntelB;
            _statBonus[4] += proto.ChaB;
            itemHitrate += proto.Hitrate;
            itemEvasionrate += proto.Evasionrate;
            _resist[0] += proto.FireR;
            _resist[1] += proto.ColdR;
            _resist[2] += proto.LightningR;
            _resist[3] += proto.MagicR;
            _resist[4] += proto.CurseR; // disease
            _resist[5] += proto.PoisonR;
        }

        var coefficient = _characters.LoadCoefficientAsync(user.Class).GetAwaiter().GetResult();
        if (coefficient is null)
            return;

        ushort itemDamage = 0;
        float hitCoefficient = 0f;
        var right = SlotProto(RightHand);
        if (right is not null)
        {
            hitCoefficient = WeaponCoefficient(coefficient, right.Group);
            itemDamage += (ushort)(Slot(RightHand).Durability == 0 ? right.Damage / 2 : right.Damage);
        }

        var left = SlotProto(LeftHand);
        if (left is not null)
        {
            var damage = Slot(LeftHand).Durability == 0 ? left.Damage / 2 : left.Damage;
            if (left.IsBow)
            {
                hitCoefficient = coefficient.Bow;
                itemDamage = (ushort)damage;
            }
            else
            {
                itemDamage += (ushort)(Slot(LeftHand).Durability == 0 ? damage / 2 : damage);
            }
        }

        if (itemDamage < 3)
            itemDamage = 3;

        int level = user.Level;
        int strTotal = _stats[0] + _statBonus[0], staTotal = _stats[1] + _statBonus[1],
            dexTotal = _stats[2] + _statBonus[2], intTotal = _stats[3] + _statBonus[3];

        var baseAp = strTotal > 150 ? strTotal - 150 : 0;
        if (strTotal == 160)
            baseAp--;

        int apStat;
        var additionalAp = 3;
        if (IsRogue())
        {
            apStat = dexTotal;
        }
        else
        {
            apStat = strTotal;
            additionalAp += baseAp;
        }

        _totalHit = 0;
        if (IsWarrior() || IsPriest())
            _totalHit = (ushort)(0.010f * itemDamage * (apStat + 40) + hitCoefficient * itemDamage * level * apStat);
        else if (IsRogue())
            _totalHit = (ushort)(0.007f * itemDamage * (apStat + 40) + hitCoefficient * itemDamage * level * apStat);
        else if (IsMage())
            _totalHit = (ushort)(0.005f * itemDamage * (apStat + 40) + hitCoefficient * itemDamage * level);
        _totalHit = (ushort)((_totalHit + additionalAp) * 100 / 100);

        _totalAc = (short)(coefficient.Ac * (level + itemAc));
        _maxWeight = (ushort)Math.Min(32767, (strTotal + level) * 50 + maxWeightBonus);

        _maxHp = (short)Math.Min(MaxPlayerHp,
            coefficient.Hp * level * level * staTotal + 0.1f * level * staTotal + staTotal / 5 + itemMaxHp + 20);
        _maxMp = (short)(coefficient.Mp != 0
            ? coefficient.Mp * level * level * (intTotal + 30) + 0.1f * level * 2 * (intTotal + 30) + (intTotal + 30) / 5 + itemMaxMp + 20
            : coefficient.Sp != 0
                ? coefficient.Sp * level * level * staTotal + 0.1f * level * staTotal + staTotal / 5 + itemMaxMp
                : 0);

        if (refillWhenEmpty)
        {
            _currentHp = user.Hp > 0 ? user.Hp : _maxHp;
            _currentMp = user.Mp > 0 ? user.Mp : _maxMp;
        }
        else
        {
            if (_currentHp > _maxHp)
                _currentHp = _maxHp;
            if (_currentMp > _maxMp)
                _currentMp = _maxMp;
        }
    }

    private ItemSlotData Slot(int index) =>
        index >= 0 && index < _slots.Length ? _slots[index] : ItemSlotData.Empty;

    private ItemProto? SlotProto(int index) =>
        Slot(index).ItemId != 0 && _protos.TryGetValue(Slot(index).ItemId, out var proto) ? proto : null;

    // Job groups (shared/globals.h ClassType + CUser::JobGroupCheck): the last
    // digit pair of the class id - 5/6 warrior, 7/8 rogue, 9/10 mage, 11/12 priest.
    private int JobGroup() => _user!.Class % 100;
    private bool IsWarrior() => JobGroup() is 5 or 6;
    private bool IsRogue() => JobGroup() is 7 or 8;
    private bool IsMage() => JobGroup() is 9 or 10;
    private bool IsPriest() => JobGroup() is 11 or 12;

    /// <summary>The COEFFICIENT column behind a weapon group (SetUserAbility's switch).</summary>
    private static float WeaponCoefficient(ClassCoefficient coefficient, int group) => group switch
    {
        1 => coefficient.ShortSword,
        2 => coefficient.Sword,
        3 => coefficient.Axe,
        4 or 18 => coefficient.Club,
        5 => coefficient.Spear,
        7 or 8 or 10 => coefficient.Bow,
        11 => coefficient.Staff,
        _ => 0f,
    };

    /// <summary>
    /// Port of CUser::ItemMove (ItemHandler.cpp): uint8 direction, uint32 item
    /// id, uint8 source, uint8 destination (ItemMovementType enum). The port
    /// covers equipping, de-equipping, inventory and cospre shuffles with the
    /// original's validation (prototype exists, the source really holds that
    /// item, level/stat requirements, slot position validity), swaps or
    /// overwrites, persists USERDATA.strItem and answers SendItemMove(1) plus
    /// the weight packet. Zone drop/pickup and magic-bag directions fail
    /// closed (SendItemMove(0)) until those systems exist.
    /// </summary>
    private void ItemMove(Packet packet)
    {
        if (!CanTravel() || packet.RemainingToRead != 7) { SendItemMove(0); return; }
        var previous = _slots;
        _slots = _slots.ToArray();
        var dir = packet.ReadByte();
        var itemId = packet.ReadUInt32();
        var src = packet.ReadByte();
        var dst = packet.ReadByte();

        var rejection = MoveRejection(dir, itemId, src, dst);
        if (rejection is not null)
        {
            _slots = previous;
            KoLog.Info($"[ ITEM MOVE - {DateTime.Now:HH:mm:ss} ] {_charId} rejected " +
                       $"dir={dir} item={itemId} {src} -> {dst}: {rejection}");
            SendItemMove(0);
            return;
        }

        var proposed = _slots;
        _slots = previous;
        try
        {
            if (!CommitInventory(proposed, _user!.Gold, shopReply: false)) return;
        }
        catch (Exception ex)
        {
            InventoryUnavailable(ex);
            return;
        }

        try { RecomputeStats(refillWhenEmpty: false); } // SetUserAbility(false)
        catch (Exception ex) { InventoryUnavailable(ex); return; }
        _appearanceVersion++;
        SendItemMove(1);
        SendItemWeight();
        KoLog.Info($"[ ITEM MOVE - {DateTime.Now:HH:mm:ss} ] {_charId} dir={dir} item={itemId} {src} -> {dst}");
    }

    /// <summary>Applies the move and returns null, or the reason it was rejected.</summary>
    private string? MoveRejection(byte dir, uint itemId, byte src, byte dst)
    {
        if (itemId == 0 || !_protos.TryGetValue(itemId, out var proto))
            return $"no prototype for item {itemId} ({_protos.Count} loaded)"; // C++: GetItemPtr(nullptr)

        int srcIndex, dstIndex;
        switch (dir)
        {
            case 1: // ITEM_INVEN_SLOT
                if (src >= HaveMax) return $"source {src} outside the inventory range";
                if (dst >= SlotMax) return $"destination {dst} outside the equipment range";
                if (!ItemEquipAvailable(proto)) return "level/rank/title/stat requirements not met";
                if (!IsValidSlotPos(proto, dst)) return $"slot type {proto.Slot} does not fit position {dst}";
                srcIndex = SlotMax + src;
                dstIndex = dst;
                break;
            case 2: // ITEM_SLOT_INVEN
                if (src >= SlotMax) return $"source {src} outside the equipment range";
                if (dst >= HaveMax) return $"destination {dst} outside the inventory range";
                srcIndex = src;
                dstIndex = SlotMax + dst;
                break;
            case 3: // ITEM_INVEN_INVEN
                if (src >= HaveMax || dst >= HaveMax) return $"inventory indices {src}/{dst} out of range";
                srcIndex = SlotMax + src;
                dstIndex = SlotMax + dst;
                break;
            case 4: // ITEM_SLOT_SLOT
                if (src >= SlotMax || dst >= SlotMax) return $"equipment indices {src}/{dst} out of range";
                if (!IsValidSlotPos(proto, dst)) return $"slot type {proto.Slot} does not fit position {dst}";
                srcIndex = src;
                dstIndex = dst;
                break;
            case 7: // ITEM_INVEN_TO_COSP
                if (src >= HaveMax || dst >= CospreCount) return $"cospre move indices {src}/{dst} out of range";
                srcIndex = SlotMax + src;
                dstIndex = CospreStart + dst;
                break;
            case 8: // ITEM_COSP_TO_INVEN
                if (src >= CospreCount || dst >= HaveMax) return $"cospre move indices {src}/{dst} out of range";
                srcIndex = CospreStart + src;
                dstIndex = SlotMax + dst;
                break;
            default:
                return $"direction {dir} not ported (zone/magic-bag moves)";
        }

        if (Slot(srcIndex).ItemId != itemId)
            return $"source slot {srcIndex} holds {Slot(srcIndex).ItemId}, not {itemId}";

        var source = Slot(srcIndex);
        var target = Slot(dstIndex);
        _slots[dstIndex] = source;
        _slots[srcIndex] = target.ItemId != 0 ? target : ItemSlotData.Empty;
        return null;
    }

    /// <summary>Port of CUser::ItemEquipAvailable: level/rank/title/stat requirements.</summary>
    private bool ItemEquipAvailable(ItemProto proto)
    {
        var user = _user!;
        return user.Level >= proto.ReqLevel
            && user.Level <= proto.ReqLevelMax
            && user.Rank >= proto.ReqRank
            && user.Title >= proto.ReqTitle
            && _stats[0] >= proto.ReqStr
            && _stats[1] >= proto.ReqSta
            && _stats[2] >= proto.ReqDex
            && _stats[3] >= proto.ReqIntel
            && _stats[4] >= proto.ReqCha;
    }

    /// <summary>
    /// Port of CUser::IsValidSlotPos: ItemSlotType enum values to equipment
    /// positions (the order ItemBlobs.Describe lists). Two-handers additionally
    /// require the opposite hand to be free; cospre types (>= 100) accept any
    /// cospre position in the port.
    /// </summary>
    private bool IsValidSlotPos(ItemProto proto, int dest)
    {
        switch (proto.Slot)
        {
            case 0: return dest == RightHand || dest == LeftHand;
            case 1: return dest == RightHand;
            case 2: return dest == LeftHand;
            case 3: return dest == RightHand && Slot(LeftHand).ItemId == 0;
            case 4: return dest == LeftHand && Slot(RightHand).ItemId == 0;
            case 5: return dest == 4;  // pauldron -> breast
            case 6: return dest == 10; // pads -> legs
            case 7: return dest == 1;  // helmet -> head
            case 8: return dest == 12; // gloves -> glove
            case 9: return dest == 13; // boots -> foot
            case 10: return dest == 0 || dest == 2; // earrings
            case 11: return dest == 3; // necklace
            case 12: return dest == 9 || dest == 11; // rings
            case 13: return dest == 5; // shoulder
            case 14: return dest == 7; // belt
            case 25: return dest == 47 || dest == 48; // the magic bags themselves
            default: return proto.Slot >= 100 && dest >= CospreStart && dest < CospreStart + CospreCount;
        }
    }

    /// <summary>
    /// Port of CUser::PointChange (User.cpp:1833): uint8 type, 1-based STAT
    /// order. Validates like the original (stat in range, a free point left,
    /// stat below STAT_MAX) and drops invalid requests in silence; a valid one
    /// spends the point, recomputes abilities and answers WIZ_POINT_CHANGE
    /// (type sub-opcode, new stat value, max HP/MP, total hit, max weight)
    /// plus the SendItemMove(1) stat refresh. The original keeps the change in
    /// memory until the logout save; the port persists it immediately so a
    /// restart cannot spend the same point twice.
    /// </summary>
    private void PointChange(Packet packet)
    {
        var type = packet.ReadByte();
        var stat = type - 1;
        if (stat > 4 || _points < 1 || _stats[stat] >= 255)
            return;

        _points--;
        var value = ++_stats[stat];
        try
        {
            _characters.SaveStatPointsAsync(_charId, _stats, _points).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            KoLog.Error($"Saving the stat change for '{_charId}' could not reach the database.", ex);
        }

        RecomputeStats(refillWhenEmpty: false); // SetUserAbility()

        var result = new Packet(WizOpcode.PointChange, type);
        result.WriteUInt16(value)
            .WriteInt16(_maxHp)
            .WriteInt16(_maxMp)
            .WriteUInt16(_totalHit)
            .WriteUInt16(_maxWeight);
        Send(result);
        SendItemMove(1);
        KoLog.Info($"[ POINT CHANGE - {DateTime.Now:HH:mm:ss} ] {_charId} stat={stat} -> {value}, {_points} point(s) left");
    }

    /// <summary>Port of CUser::SendItemMove: sub-opcode 0 = failure, else the refreshed stats.</summary>
    private void SendItemMove(byte subcommand)
    {
        var result = new Packet(WizOpcode.ItemMove, subcommand);
        if (subcommand != 0)
        {
            result.WriteUInt16(_totalHit)
                .WriteUInt16((ushort)_totalAc)
                .WriteUInt16(_maxWeight)
                .WriteInt16(_maxHp)
                .WriteInt16(_maxMp)
                .WriteUInt16((ushort)_statBonus[0])
                .WriteUInt16((ushort)_statBonus[1])
                .WriteUInt16((ushort)_statBonus[2])
                .WriteUInt16((ushort)_statBonus[3])
                .WriteUInt16((ushort)_statBonus[4])
                .WriteUInt16((ushort)_resist[0])
                .WriteUInt16((ushort)_resist[1])
                .WriteUInt16((ushort)_resist[2])
                .WriteUInt16((ushort)_resist[3])
                .WriteUInt16((ushort)_resist[4])
                .WriteUInt16((ushort)_resist[5]);
        }

        Send(result);
    }

    /// <summary>Port of CUser::SendItemWeight: WIZ_WEIGHT_CHANGE with the carried weight.</summary>
    private void SendItemWeight()
    {
        var result = new Packet(WizOpcode.WeightChange);
        result.WriteInt16((short)Math.Clamp(_itemWeight, 0, short.MaxValue));
        Send(result);
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
        var travelDump = packet.Opcode is WizOpcode.SelChar or WizOpcode.ZoneChange or WizOpcode.Warp
            ? $": {Hexdump(packet.Payload)}" : string.Empty;
        KoLog.Info($"-> {RemoteIp} {Name(packet.Opcode)} ({packet.Length} byte(s)){travelDump}");
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
