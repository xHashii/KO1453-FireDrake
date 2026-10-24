# Knight Online v1453 — C++ → C# Rewrite Plan

> Current status: **partial implementation**, not planning-only. The original design
> below is historical. For the audited live scope and next milestones, see
> [Implementation status and roadmap](IMPLEMENTATION_STATUS_AND_ROADMAP.md) (2026-10-25).
> Goal: a C# server that is **wire-compatible with the unmodified v1453 (Reign of the Fire Drake) client**
> and can run against a **MySQL/MariaDB** database converted from the current SQL Server one.

---

## 1. What exists today (audit of this repo, Oct 2026)

A Visual Studio 2012, Win32-only C++ solution (`KnightOnlineServer.sln`), ~57,000 lines
of first-party C++ (plus a vendored copy of Lua 5.x in `scripting/Lua/src`).

| Project | Role | Size | Notes |
|---|---|---|---|
| `LogInServer/` | Login, version/patch list, server list, key exchange | 11 files / ~0.8k LOC | 7 opcodes (`LS_*`), reads `LogInServer.ini`, DSN `KO_MAIN` |
| `GameServer/` | All gameplay | 65 files / ~39k LOC | The monster: `User.cpp` alone is 6,014 lines |
| `AIServer/` | Monster/NPC AI, pathfinding, room/zone events | 28 files / ~8.5k LOC | `Npc.cpp` is 3,961 lines; talks to GameServer over TCP (port 10020) |
| `shared/` | IOCP networking, packets, crypto, ODBC layer, SMD loader | 122 files / ~8.2k LOC | 54 ODBC "recordset" classes in `shared/database/` |
| `N3BASE/` | Collision/height data (`N3ShapeMgr`) embedded in `.smd` | 4 files / ~0.6k LOC | Also a stray `N3BASE.rar` |
| `scripting/` | Stock Lua interpreter source | vendor code | **Not ported** — replaced by a NuGet package |

Key facts driving the rewrite:

- **Protocol**: 198 defines in `shared/packets.h` (~150 `WIZ_*` client opcodes), packets framed
  `AA 55 … 55 AA`, payload run through **JvCryption** (`WIZ_CRYPTION` 0x2C key exchange) and
  **LZF + CRC32** (`shared/JvCryption.*`, `shared/KOSocket.cpp`, `shared/lzf.c`).
  The client is not being rewritten, so this must stay **byte-for-byte identical**.
- **Networking**: hand-rolled Win32 **IOCP** (`SocketWin32.cpp`, `SocketMgr.cpp`,
  `ListenSocketWin32.h`). No direct C# equivalent needed — `SocketAsyncEventArgs` /
  `System.IO.Pipelines` is the modern replacement.
- **Databases**: two SQL Server DBs over ODBC — **`KO_MAIN`** (accounts) and **`KO_GAME`** (game).
  ~40 tables are loaded into memory at startup (`LoadServerData.cpp`), and there are
  **60+ SQL statements plus many stored procedures** (`GAME_LOGIN`, `LOAD_USER_DATA`,
  `UPDATE_USER_DATA`, `CREATE_NEW_CHAR`, `CREATE_KNIGHTS`, `KING_ELECTION_PROC`,
  `MAIL_BOX_*`, …) whose bodies live **in the database, not in this repo**.
- **Binary blobs**: inventory/warehouse/skill/quest data is stored as raw byte arrays
  (`USERDATA.strItem`, `WAREHOUSE.WarehouseData`, … — item id + durability + count + serial
  per slot). The byte layout must be preserved exactly or migrated deliberately.
- **Runtime assets loaded at startup** (not in repo yet):
  - `./map/*.smd` — terrain heights, tile event grid, object/regene events, warps, embedded collision
  - `./Quests/*.Lua` — quest + NPC scripts (`LUA_SCRIPT_DIRECTORY`)
  - `*.aievt` files — AI server room/zone events (`AIServer/MAP.cpp`)
  - `GameServer.ini`, `LogInServer.ini`, AIServer ini — ports, DSNs, battle schedule, rates
- **Global state everywhere**: `g_pMain` (the `CGameServerDlg` singleton) is referenced
  **1,114 times** in GameServer alone. `CUser` is a 1,419-line header "god object".
  A literal 1:1 translation would carry all of this into C# — we will translate faithfully
  *in behavior* but restructure into services (dependency injection).
- **Repo hygiene issues**: a 45 MB Visual Studio IntelliSense DB (`KnightOnlineServer.sdf`),
  `*.vcxproj.user` files, `N3BASE.rar`. These should be dropped from the new tree.

## 2. Non-negotiables (or the client breaks)

1. Packet framing `AA55 … 55AA`, little-endian fields, opcode numbers, field order.
2. JvCryption key generation + fast/CRC variants, LZF compression, CRC32 — verified by
   **golden-vector tests** generated from the C++ code before porting (§6).
3. Login flow ordering: version check → patch/server list → login → public key → game login
   (`WIZ_VERSION_CHECK`, `WIZ_CRYPTION`).
4. DB blob layouts (item/warehouse/skill/quest byte arrays) — or an explicit one-time migration.
5. Tick/timing semantics the client assumes (1 s movement packets, magic timers, regen ticks).

## 3. Proposed new solution layout

```
/                           (repo root)
├── legacy/                 old C++ tree, kept read-only as the reference spec
├── docs/                   this plan, protocol notes, DB conversion notes
├── src/
│   ├── KnightOnline.Shared          Packet/ByteBuffer, JvCryption, LZF, CRC32,
│   │                                opcodes, config, logging, SMD/EVT parsers
│   ├── KnightOnline.Database        entities + repositories (MySQL), game-data cache
│   ├── KnightOnline.LoginServer     ~200-line port of LogInServer
│   ├── KnightOnline.GameServer      handlers, users, parties, Knights, king, magic…
│   ├── KnightOnline.AiServer        NPC AI, pathfinding, room events   [or merged — §10]
│   └── KnightOnline.Tools.*         SmdInspector, DbMigrator (MSSQL→MySQL), PacketSniffer
├── tests/                  crypto/packet golden tests, SMD parser tests, handler tests
├── sql/mysql/              converted schema + procedures (once DB files arrive)
└── data/                   map/ (.smd), Quests/ (.Lua), *.aievt   [git-lfs or ignored? §10]
```

## 4. Subsystem mapping (old → new)

| C++ | C# |
|---|---|
| `shared/Packet.h`, `ByteBuffer.h` | `PacketWriter`/`PacketReader` over `Span<byte>`/`ArrayBufferWriter` |
| `shared/KOSocket*`, `SocketMgr` (IOCP) | `SocketAsyncEventArgs` + `System.Threading.Channels` packet queue |
| `shared/JvCryption`, `lzf.c`, `crc32.c` | Direct ports (small, well-tested) — verify with golden vectors |
| `shared/STLMap.h`, `ReferenceObject` | `ConcurrentDictionary`, normal GC references |
| `shared/Thread`, `Condition`, `RWLock` | `Task`, `PeriodicTimer`, `Channel<T>`, `SemaphoreSlim` |
| `shared/Ini.cpp` + `*.ini` | `appsettings.json` + `IOptions<T>` (or keep INI — §10) |
| `shared/database/Odbc*` + 54 `*Set.h` | EF Core (Pomelo MySQL) or Dapper — §10 |
| `GameServer/DBAgent.cpp` (2,019 LOC) | Repositories + a batched "database agent" `Channel` worker |
| `GameServer/DatabaseThread.cpp` | Background service draining a job queue |
| `GameServer/*Handler.cpp` (19 files) | One handler class per opcode group, `UserSession` partials gone |
| `GameServer/User.cpp/h` (7.4k LOC) | `Player` entity + services (inventory, magic, party, social…) |
| `GameServer/Magic*.cpp` (4.2k LOC) | `MagicEngine` service, data from `MAGIC`/`MAGIC_TYPE1..9` cache |
| `GameServerLuaEngine` + `lua_bindings` | MoonSharp (pure C# Lua) or NLua (native) — §10 |
| `AIServer/Npc*.cpp`, `PathFind.cpp` | `NpcBrain` state machine + A* on SMD grid; per-zone update loops |
| `shared/SMDFile.cpp`, `N3ShapeMgr` | `SmdMap` parser (heights, warps, object/regene events, collision) |
| `GameEvent`, `RoomEvent`, `.aievt` | Event scheduler + EVT parser (same binary format) |
| `TRACE`/console prints | Serilog (console + rolling file) |
| `ConsoleInputThread` GM commands | Same console commands, later maybe an admin API |

## 5. Database conversion (SQL Server → MySQL/MariaDB) — received and converted

1. Need from owner: a `.bak` **or** (much better) full `.sql` script export (schema **+ data**)
   of both DBs from SSMS, or direct `.sql` dumps per table. Stored-procedure bodies included.
2. Automated conversion pass: types (`tinyint unsigned`, `smallint`, `varbinary(n)`→`VARBINARY/BLOB`,
   `datetime`, `[Level]`→`` `Level` ``), `IDENTITY`→`AUTO_INCREMENT`, `TOP n`→`LIMIT`, etc.
3. **Stored procedures**: each T-SQL proc is either (a) re-written as a MySQL procedure, or
   (b) lifted into C# repository code (preferred for anything with game logic — §10).
4. Byte-blob columns (`strItem`, `WarehouseData`, `strSkill`, `strQuest`, serial arrays)
   verified round-trip with a converter + tests before any server code uses them.
5. Deliverable: `sql/mysql/` with `01_schema.sql`, `02_procedures.sql`, `03_seed_data.sql`
   that loads cleanly in HeidiSQL/MySQL Workbench, plus a repeatable migrator tool.

## 6. Milestones (each ends with something testable)

- **M0 — Prep**: new `.gitignore`; crypto/packet golden vectors captured by compiling
  the original `crc32.c`/`lzf.c`/JvCryption unmodified ✅. (Moving the C++ tree into
  `legacy/` and dropping `.sdf`/`.rar` deliberately deferred to its own commit, so this
  milestone's diff stayed reviewable; the C++ remains untouched in place as the spec.)
- **M1 — Skeleton + wire core** ✅: `KnightOnline.Net.sln` with Shared/Database/
  LoginServer/GameServer/Verification. Packet, JvCryption, LZF, CRC32, framing and
  socket/session layer ported; `KnightOnline.Verification` checks them byte-for-byte
  against the C golden vectors (all green in the Python mirror of the same algorithms;
  first `dotnet` build still to be run on a machine with the SDK).
- **M2 — LoginServer end-to-end** ✅ code / ⏳ client test: all 7 `LS_*` opcodes ported
  incl. news rendering, server-list cache and the crypto handshake; MariaDB account
  repository now implements the real single-database login/premium semantics
  so a real 1453 client can be pointed at it once built locally. Not yet
  verified against a live client.
- **M3 — Data layer** ⏳ in progress: real MSSQL export converted to MariaDB and LoginServer/AdminPanel rewired to one configurable database; ~40 game-data tables load into cache at startup
  (items, magic 1–9, NPCs, drops, quests, zones…); account/char/USERDATA CRUD.
- **M4 — GameServer vertical slice** ⏳ in progress: the pre-character-select
  part of `CUser::HandlePacket` is ported and client-verified gate by gate —
  `WIZ_VERSION_CHECK` (0x2B, handshake + crypto), `WIZ_LOGIN` (0x01,
  `GAME_LOGIN`), `WIZ_SEL_NATION` (0x05, `NATION_SELECT`) and
  `WIZ_ALLCHAR_INFO_REQ` (0x0C, `GetAllCharID` + `LoadCharInfo`), with
  `MariaDbCharacterRepository` holding the SQL. Packets beyond that gate are
  captured decrypted for the increments below. WIZ_NEW_CHAR (character
  creation) is ported too - client gates plus CREATE_NEW_CHAR, with migration
  0005 restoring the BEGINNER_ITEM.Skill byte the varchar conversion mangled.
  WIZ_SEL_CHAR is answered too (LOAD_USER_DATA port, spawn point from
  ZONE_INFO, migrations 0005/0006 fixing the skill byte and zone-0 spawns) -
  the doorway into the world. WIZ_GAMESTART answers the loading pull with the
  WIZ_MYINFO dump, and the equipment subset is live too: WIZ_ITEM_MOVE slot
  shuffles persist USERDATA.strItem and refresh SetUserAbility-derived stats.
  Subsequent increments now implement MYINFO/world entry, source NPC/monster
  pushes, validated movement/travel, logout/checkpoint saves, general/private
  chat, nearby player IN/OUT, movement/rotation/state routing and appearance
  refresh. Remaining M4 work includes character delete and complete original
  spatial-region/time/weather synchronization rather than prototype snapshots.
- **M5 — Core gameplay** ⏳ in progress: basic R attacks, corpse/respawn state,
  stationary retaliation, supported single-target skills, solo XP/loot, skill
  allocation, consumables and atomic NPC shops are implemented. The latest
  foundation increment adds persistent shortcut bars and transactional account
  warehouses (items/coins, stack/page moves, NPC/range/protection checks).
  Full combat/magic, trade/player merchants, repairs/upgrades and parties remain
  unported. See `COMBAT_AND_LOOT.md` and `MULTIPLAYER_AND_BANKING.md` for exact
  supported scope and real-client acceptance checks.
- **M6 — Social & meta**: Knights (clans) + alliances + siege, king system, friends, letters,
  rentals/seals, premium, shopping mall (PUS).
- **M7 — AI + world**: AIServer port — spawns from `K_NPCPOS`, aggro/chase/attack, NPC magic,
  pathfinding, deaths/respawns, object & room/zone events from `.smd`/`.aievt`.
- **M8 — Quests & events**: quest v2 (`QUEST_HELPER`/`QUEST_MONSTER`) + Lua scripts in
  `./Quests/`, war/CSW scheduling from config, monster challenges, arena.
- **M9 — Hardening**: GM console commands, logging, rates config, Dockerfiles,
  run-it-with-real-client soak test, perf pass on hot paths.

Effort honesty: ~57k lines of gnarly C++ (three files over 3k lines each) makes this a
**multi-session project**, not a one-shot. We go milestone by milestone; after each one the
server should build and, from M2 on, talk to a real client.

## 7. Main risks

- **Stored procedures** hide real game logic (char creation, king elections, mail) that only
  exists in the DB — the export has now been received, but each routine still
  needs a deliberate MariaDB/C# port before M3/M6 can finish.
- **Blobs**: one wrong offset in `strItem` corrupts inventories. Dedicated (de)serializers + tests.
- **`CUser` god object + `g_pMain`**: behavior (incl. bugs the client tolerates) must be
  preserved while restructuring — port first, refactor second.
- **Lua quest scripts** call ~100 C++-bound functions (`lua_bindings.cpp`); the same API
  surface must exist in C# so existing `.Lua` files run unchanged.
- **String encoding**: KO strings are Korean codepage/ASCII, not UTF-16 — needs an explicit
  `Encoding` choice everywhere (CP949-compatible), confirmed against the client in M2.

## 8. Files needed from the owner

1. **Databases**: ✅ received — one `.sql` script export (schema + data +
   procedures) of the combined source database named `KN_Online`, converted
   under `sql/mysql/kn_online/`.
2. **Maps**: the whole `map/` folder (`*.smd`).
3. **Quests**: the whole `Quests/` folder (`*.Lua`) and any `*.evt` / `*.aievt` files.
4. **Configs**: `GameServer.ini`, `LogInServer.ini`, AIServer ini (sanitized passwords OK).
5. A note of **which client** you test with (clean 1453 vs. a private-server client with
   custom patches/TBL), since packet quirks may differ.

> Large/binary assets: use Git LFS or keep them out of git with a `data/` drop folder (§10).

## 9. Working rules for the port

- C++ in `legacy/` is the reference spec — never edited, always consulted.
- Every ported file header cites its C++ origin (e.g. `// Port of GameServer/ChatHandler.cpp`).
- No subsystem is "done" without tests or a client-verifiable milestone.
- Original names kept where sensible (`WizOpcodes.Chat = 0x10`) so cross-referencing is trivial.

## 10. Decisions pending (owner)

1. Target framework & hosting: **.NET 10 (LTS, cross-platform)** vs .NET 8 LTS vs .NET Framework 4.8.
2. Process topology: keep **3 separate servers** vs merge AIServer into GameServer vs all-in-one.
3. Approach/order: **milestone vertical slices (recommended)** vs file-by-file translation.
4. Database: **MariaDB vs MySQL 8** (+ HeidiSQL works with both); data access:
   **EF Core vs Dapper vs raw ADO.NET**; stored procs: port to MySQL vs lift into C#.
5. Lua: **MoonSharp** (pure C#, some perf/compat caveats) vs **NLua** (native Lua, exact compat).
6. Config: **appsettings.json** vs keeping `.ini` files 1:1.
7. Assets in git: normal git vs Git LFS vs external `data/` folder.
