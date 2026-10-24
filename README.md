# KO1453-FireDrake — Knight Online v1453 server

Server for **Knight Online v1453 (Reign of the Fire Drake)**, being rewritten
from the original C++/Win32/SQL Server implementation to **C# (.NET 10)** with
a **MariaDB** database. The rewrite is wire-compatible with the unmodified
1453 client.

Full plan, milestone list and the audit of the original codebase:
**[docs/CSHARP_REWRITE_PLAN.md](docs/CSHARP_REWRITE_PLAN.md)**

## Repository layout

| Path | What |
|---|---|
| `AIServer/`, `GameServer/`, `LogInServer/`, `shared/`, `N3BASE/`, `scripting/` | Original C++ sources — the reference spec for the port (do not edit) |
| `KnightOnlineServer.sln` | Original Visual Studio 2012 C++ solution |
| `src/` | The C# rewrite (see below) |
| `KnightOnline.Net.sln` | C# solution |
| `tests/KnightOnline.Verification` | Dependency-free console harness that checks the C# wire protocol byte-for-byte against the original C |
| `sql/mysql/` | Converted MariaDB migrations for the single configured game database, plus original T-SQL routines awaiting port |
| `docs/` | Rewrite plan and notes |

```text
src/
  KnightOnline.Shared      packets, JvCryption, LZF, CRC32, sockets, INI, logging
  KnightOnline.Database    MariaDB access (MySqlConnector), migrations, item blobs
  KnightOnline.LoginServer port of LogInServer/
  KnightOnline.GameServer  port of GameServer/ + AIServer/ (merged) - not started
  KnightOnline.AdminPanel  Blazor GM panel: dashboard, character & inventory editor
```

## Status

- ✅ **M1 — wire core**: framing, JvCryption, LZF and CRC32 ported and verified
  byte-for-byte against the original C code compiled unmodified.
- ✅ **M2 — LoginServer**: all 7 login opcodes ported (version, patch list,
  login, server list, news, encryption key exchange). _Not yet tested against
  a real client — that's the next step._
- ✅ **Admin panel v1**: dashboard, character search, visual inventory/warehouse
  editor, give-item-by-letter, audit log + backups (`src/KnightOnline.AdminPanel`).
- ✅ **Database conversion (M3, LoginServer/AdminPanel side)**: the real
  single-database SQL Server export has been converted to MariaDB migrations;
  the target database name comes from config, and legacy auto-register is
  opt-in (`AUTO_REGISTER=1`, default off).
- ⏳ Waiting on: a local .NET build + live client test, `map/*.smd`,
  `Quests/*.Lua` (+ `*.evt`), original `*.ini` configs.
- ⬜ GameServer (39k lines of C++) and the merged AI server.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
**VS 2022 cannot target `net10.0`** — the chosen local workflow is
VS 2022 as an editor plus dotnet CLI builds (VS 2026/VS Code also work).
Full step-by-step (build, DB, LoginServer,
AdminPanel, where `map/*.smd` / `Quests/*.Lua` go):
**[docs/BUILD_AND_RUN.md](docs/BUILD_AND_RUN.md)**.

```bash
dotnet build KnightOnline.Net.sln -c Release
dotnet run --project tests/KnightOnline.Verification   # protocol self-checks
```

## Trying the LoginServer

**No manual database import.** MariaDB via one Docker command; the server
creates the database named by `[DATABASE] DATABASE` in `LogInServer.ini`
and applies the migrations copied from `sql/mysql/kn_online/` on startup
(`AUTO_MIGRATE=1`). The old MSSQL name `KN_Online` is only the sample
default—rename it if the same name is used in Docker/AdminPanel config.
Full database story (incl. converting your SQL Server dump):
**[docs/DATABASE.md](docs/DATABASE.md)**.

```bash
docker compose up -d                                   # MariaDB on localhost:3306
cp src/KnightOnline.LoginServer/LogInServer.sample.ini LogInServer.ini   # then edit [DATABASE]
dotnet run --project src/KnightOnline.LoginServer
```

Point your 1453 client's login at port **15100**. Add a `TB_USER` row in
HeidiSQL for a test account, or temporarily set `AUTO_REGISTER=1` in
`LogInServer.ini` to restore the original first-login account creation
(default is off). No database at all yet? Set
`ALLOW_OFFLINE_START=1` — the server runs and talks to the client, logins
just fail with AUTH_ERROR.

## Admin panel

```bash
docker compose up -d
dotnet run --project src/KnightOnline.AdminPanel    # http://localhost:5100
```

Sign in with the credentials in `src/KnightOnline.AdminPanel/appsettings.json`
(default `admin` / `changeme` — change them, or set `KO_ADMIN_PASSWORD`).
It uses the same single configured database via
`ConnectionStrings:GameDatabase`. Design + safety rules:
[docs/ADMIN_PANEL.md](docs/ADMIN_PANEL.md).

## Porting rules

- The C++ tree is the spec: never edit it, always cite it
  (every C# file header names the C++ it ports).
- Protocol code ships with golden-vector checks — run the verification
  harness after touching `KnightOnline.Shared`.
- Original bugs are only fixed deliberately, and each fix is commented
  (e.g. the C++ login handler wrote passwords into the user log; the port
  intentionally does not).

### Boosted-character intro compatibility

The bundled `BEGINNER_ITEM` templates create level-80 characters. Until the
starter tutorial is ported, selecting a character above level 1 now persists
starter quest 500 at state 2 if it was absent or at state 1. This prevents the
initial tutorial flag from being offered on every login. Other quest entries,
blob padding and later states are preserved; genuine level-one characters are
unchanged. This is a tutorial-skip compatibility policy, not a complete quest
handler or a claim that the player finished the tutorial. The repair happens
on character selection (no manual SQL or migration required) and logs
`[ STARTER QUEST ]` when it changes a row.

### Prototype travel and H-menu actions

GameServer now handles H-menu Town (`WIZ_HOME`), standing/sitting and public
emotions (`WIZ_STATE_CHANGE`). It also accepts these normal-chat commands:

| Command | Action |
| --- | --- |
| `/help` | List implemented travel commands |
| `/town` | Return to the current zone's nation-specific START_POSITION; requires at least half HP |
| `/teleport` (or `/warp`, `/zone`) | Open the prototype travel list and print available zone IDs |
| `/zone <id or name>` | Travel to a listed public destination |
| `/moradon` | Travel to Moradon (21) |
| `/home` | Travel to your nation's homeland (1 or 2), not the same as `/town` |
| `/eslant` | Travel to your nation's Eslant (11 or 12), level 60+ |

Destination names include `moradon`, `home`, `eslant`, `ronark`/`cz`.
Ronark Land (71) requires level 60+. Enemy homelands/Eslant and event/war
zones are not exposed. These are **free prototype exploration routes**, not
a port of official warp fees, PvP/event entry rules or map warp IDs.
The travel list uses server-offered synthetic IDs and never accepts client
coordinates. Native map gate interactions are now supported by the static-world loader
described below; `/teleport` remains a free exploration alternative.

Cross-zone travel implements the original 3 -> client 1 -> server 2 -> client 2
handshake, with static NPC snapshots (other players remain unported).
Zone/position changes are saved transactionally before the reply is sent.
Walking positions are not saved by this travel slice.

New characters start at Moradon's ZONE_INFO init coordinates. On selection,
existing characters at the *exact* former nation-init coordinates are moved
to Moradon too; other saved locations are left alone. Startup creates the
small `KO_WORLD_TRAVEL` marker table (requires CREATE TABLE permission).
Explicit travel writes a marker so returning to the old nation spawn does
not trigger that compatibility repair on later logins. No SQL needs to be
run manually. Log messages `[ TRAVEL ] ... loaded zone ...` confirm the
handshake has completed.

### Recovery and explicit GM promotion

Normal recovery now runs every six seconds (the original `CUser::Update`
checks whole elapsed seconds **> 5**). `CUser::HPTimeChange` supplies the rates:
- Sitting HP: `floor(level * (1 + level / 30)) + 3` per tick.
- Sitting MP: `(maxMP * 5 / (level + 29) + 3)` with integer division.
- Standing restores only MP, using the original level-based formula.
- Mastered mages (110/210) below 30% MP receive the original 120% MP factor.
- GM characters refill both bars on a sitting tick, as in the C++ source.

Bars are clamped, dead/loading characters do not regenerate, moving cancels
sitting, and the timer is disposed on disconnect. Current HP/MP is saved on
session disconnect. Combat, transformations and special event-zone recovery
are still outside this prototype.

To promote account `test`, log its characters out and stop GameServer, then run:

```powershell
dotnet run --project src\KnightOnline.GameServer -- --grant-gm test
dotnet run --project src\KnightOnline.GameServer
```

The first command is an explicit offline operator action: it sets `Authority=0`
for that account's **existing owned characters** and exits. It does not grant
GM to every account, run on every startup or automatically promote future
characters. Reconnect to load the new authority. Database credentials never
need to be supplied in chat or command-line arguments.

The rewrite's `/moradon` is public, not GM-gated. Some client command parsers
may consume slash commands before sending chat. GM alternatives are
`+moradon`, `+zonechange 21` (also `+zone 21`), `+teleport`, and `+heal`.
`+` commands are checked server-side for Authority 0. `[ COMMAND ]` logs confirm
the text reached the server. This does not implement the entire original GM
command catalogue (item spawning, NPC spawning, war controls, etc.).

New character creation now defaults to player Authority **1**, not GM **0**.
Existing authority values are deliberately not bulk-demoted; earlier prototype
characters may already have Authority 0. Promotion reports owned character count
and is safe to repeat. Receiving GM authority does not itself fix a client-side
slash-command parser; use the plus-prefixed alternatives above.

### Map identity / selection packet correction

The old rewrite's selection success packet was incorrectly encoded as
`success, init, uint16 zone, X/Z/Y, victory` (11 bytes). The original
`CUser::SelectCharacter` reads `bInit` from its internal DB response but sends
only `success, uint8 zone, X/Z/Y, victory` (9 bytes). With init=1, the client
could read **Karus (1)** while the server believed it had selected Moradon
(21). MYINFO then supplied Moradon coordinates on the wrong client map.
This was not a reason to renumber the zone IDs or replace the map assets.

Selection now uses the original nine-byte layout. Cross-zone teleport keeps
its distinct **uint16** zone field (`ZoneChange` explicitly casts the ID).
Reconnect after updating; same-zone `/moradon` cannot repair a map already
loaded under the old packet layout. SelChar/Warp/ZoneChange reply payloads
are now hex-dumped for verification. Selecting Moradon at 312,402 yields
`01 15 30 0C B4 0F 00 00 00`.

`0009_zone_start_points.sql` also fixes shipped placeholder init coordinates
and the Eslant homeland-copy coordinates (which lay outside the Eslant SMD
bounds), using existing START_POSITION entries and the correct *100 storage
units. This repair is embedded and applied at startup for existing installs;
it does not renumber maps, overwrite custom non-placeholder coordinates or
bulk-reset saved character positions. Nation-specific travel continues to
use START_POSITION rather than the generic ZONE_INFO fallback.

`python tools/test_world_coordinates.py` audits all 31 shipped map references,
their repaired init points and both nations' start points against the actual
SMD terrain dimensions, and checks repair repeatability/custom preservation.
This verifies bounds, not collision walkability or a modified client's own
map tables. Zones without START_POSITION still need authored spawn data;
this change does not invent event mechanics or open restricted routes.

### Static NPC world, native gates and right-click browsing

GameServer now loads hosted zones from `ZONE_INFO`, prototypes from `K_NPC`,
**NPC** spawns from `K_NPCPOS` (`ActType >= 100`, as in AIServer), and map
objects from `K_OBJECTPOS`. Monster rows (`ActType < 100`) are deliberately
not turned into shop NPCs. All spawn counts (`NumNPC`) are preserved. Static
NPC groups are placed deterministically inside their authored rectangles;
patrol/guard AI, fighting and event scheduling are not simulated yet.

The shipped data has **1,436 NPC spawn instances across the 31 hosted zones**,
including **57 ordinary NPCs in Moradon**, plus object proxies such as its
anvil (object/prototype 5001). Runtime NPC IDs occupy a separate 10000–19999
band and are shared by sessions. NPCs are streamed on entry, zone changes,
same-map warps and walking, with same-zone request/interaction checks. The
startup `[ WORLD ] zone=... NPCs=... objects=... warps=...` lines report the
actual loaded data, and missing prototypes/out-of-bounds placements fail
startup rather than silently dropping spawn rows.

Maps are copied to `data/map` in build/publish output. The SMD loader reads
terrain heights and skips the original collision-cell structures to load
native warp records. NPC spawn heights are interpolated from terrain;
object heights come from K_OBJECTPOS. This is not a movement collision or
pathfinding implementation. Empty/missing world tables or map files cause
an actionable startup error.

Native warp gates now use `ObjectEvent` to identify the nearby authored gate,
its nation and its control group. The warp window lists that group's SMD
entries (names, exact destinations and coin fees), filtered by currently
supported public travel rules. Selections must have been offered, remain in
range of that same gate, match nation/level requirements and have sufficient
coins. Coin deduction and saved destination are committed in one transaction;
replayed or fabricated warp selections cannot reuse the cleared menu.
Moradon object **4013** (the captured `AD 0F D4 00` request) opens group **212**,
including warp **2121** to El Morad at **1595,412** for **5,000 coins**.
Other native object services (binds, war levers, artifacts) remain unported.

Right-click merchants/tinkers send the original `WIZ_TRADE_NPC` /
`WIZ_REPAIR_NPC` selling-group replies so the **client's item tables** display
what that merchant sells. Clicking the anvil sends `WIZ_ITEM_UPGRADE` request
1 with the validated runtime anvil ID. Ordinary NPC purchases/sales are supported
(see below). Repairs, upgrades and exchanges are explicitly rejected
without modifying items or coins. Quest/warehouse/clan/other service NPCs
are visible but report that their service is not implemented; they do not
pretend to provide a working transaction. Full skills/combat are also not
part of this change.

Data regression checks (no DB required):

```sh
python tools/test_world_npcs.py
python tools/test_world_coordinates.py
python tools/audit_world_npcs.py
```

The .NET verification project additionally checks NPC-info wire bytes and
parses every bundled SMD, including the native Moradon warp data.

### Shared Moradon NPC clicks and item-hover diagnostics

Shared Moradon services are now presented with the viewing player's nation.
The original K_NPC prototypes mix faction 1 and 2 (e.g. Charon is faction 1),
which can cause a legacy client to treat a service NPC as hostile and never
send the right-click request. This presentation rule applies to service NPCs
in zone 21 only, including initial snapshots, movement updates and explicit
NPC-info requests. It does **not** change stored factions, guards, homeland
NPCs or warp-gate nation checks. Unknown/out-of-range NPC requests now produce
`[ NPC REJECT ]` diagnostics instead of silently doing nothing. Valid merchant
requests still log `[ NPC ] ... shop=...` followed by TradeNpc/RepairNpc.
Quest, warehouse and other unported services are still not implemented.

The reported 5-FPS item-hover issue is **not established as fixed** by this
change. This repository has no Windows client renderer or tooltip sources.
The Open-KO client reference renders tooltips locally; it is not a guaranteed
match for a modified 1453 executable. Do not substitute another version's
packet layouts or apply random registry/DLL fixes based on the symptom alone.

To collect read-only client build/asset hashes and Windows/GPU/font metadata:

```powershell
powershell -ExecutionPolicy Bypass -File tools\collect_client_info.ps1 -ClientRoot "C:\path\to\your\client"
```

Review `ko-client-info.json` before sharing it. This script does not modify
client files or Windows settings and does not collect passwords/configuration
contents. For the next tooltip investigation, also record:
- whether every item or only specific item IDs causes the drop;
- whether FPS stays low while holding the cursor still over the item;
- whether it occurs in both inventory and merchant windows;
- the GameServer log while clicking a potion merchant and hovering an item.
If no packet arrives when hovering, that narrows the issue to local client
rendering/data/driver work, but does not by itself prove the server's initial
item data is correct. A matching client source/build or its diagnostic log is
needed to identify and validate a renderer fix.

### Character-screen crash recovery

If the connection closes immediately after `AllcharInfoReq`, before `SelChar`,
the in-world NPC handlers have not run. Do not reset NPC tables or inventories
based on that log alone. It identifies the stage, not the client's crash cause.

The default character preview now suppresses its eight equipped-model references
on the wire. This is a **reversible recovery/isolation measure for equipment
asset failures**, not proof that an item model caused the crash. Names, character
identity, face/hair, levels and zones are preserved, as is the three-slot packet
layout. Saved equipment/inventory and world-entry equipment are not modified.
The same serializer also safely handles null-filled empty-slot equipment lists.

To restore equipment models on the character screen after verifying client
assets, set this in the actual `GameServer.ini` used to launch the server:

```ini
[SETTINGS]
CHARACTER_PREVIEW_EQUIPMENT=1
```

The default is `0`, even if the setting is absent. Restart and reconnect to
apply it. `[ CHAR PREVIEW ]` logs state the active mode, original equipment IDs,
appearance fields and the exact response payload. If the client still crashes
with preview equipment suppressed, retain that log and obtain the client crash
log/Windows Application Error entry: the client renderer and assets are not in
this server repository, so this patch cannot claim to fix every client crash.


### NPC shop purchases and sales

Ordinary merchant/tinker shops now handle `WIZ_ITEM_TRADE` buy (1), sell (2)
and shop-window inventory swap (3), using `GameServer/NPCHandler.cpp` as the
wire reference. This is not player-to-player trading or player merchant stalls.

- Requires an alive, in-game character outside zone loading and a friendly shop
  in the current zone within 11 world units. Buying/selling validates the wire
  NPC ID and group; shop-window swaps require a previously opened nearby shop.
- Prices come from the server's `ITEM` table: purchases use `BuyPrice`; ordinary
  sales use `(BuyPrice / 6) * count`, or full `BuyPrice` for `SellPrice = 1`.
  The legacy selling-group variants 0, 1, 2 and 72 are checked for purchases.
  King/siege taxes and premium discounts are **not implemented**.
- Inventory positions are 0–27, counts 1–9999; non-stackable purchases/sales
  must have count 1. Occupied incompatible slots, excessive stacks, insufficient
  coins, overweight purchases, integer overflow and the 2.1-billion coin cap
  are checked before any mutation. No automatic slot allocation is implied.
- Restricted IDs/races and recorded sealed/rental items cannot be sold. Adding
  purchases to a recorded sealed/rental stack is also refused. Protection checks
  are conservative, including legacy serial-zero items. The shipped schema
  stores serials but not item expiration blobs; timed-item support is not added.
- Item IDs, durability/count, serials and coins are saved in **one conditional
  InnoDB UPDATE** before the session changes or success is sent. Normal item
  moves now use this same persistence boundary, preserving serials on swaps.
  Stale item/serial/coin snapshots are rejected, not overwritten. A stale state,
  protected-item rejection at save, or ambiguous database failure disconnects
  the session so reconnect reloads the authoritative state. Do not run the
  legacy C++ server concurrently against the same characters.
- The protocol has no request nonce: two otherwise-valid identical buy packets
  are two purchases, each charged. There is no server retry after an uncertain
  commit. This prevents free duplication; it is not exactly-once request replay.

No new migration or settings change is required. Keep a database backup before
first testing an economy feature. CI covers pure shop rules and packet vectors,
plus atomic save/rejection, concurrent writers, protection records, persistence
and SQL-failure rollback against a disposable MariaDB 11.4 service.

Manual acceptance test (real-client confirmation still required):

1. Open a potion merchant, buy a small quantity into a free inventory slot,
   then add to that stack. Check quantity and the charged coins.
2. Sell part of the stack, then the rest. Check coins and slot clearing.
3. Buy one ordinary weapon/armor item, move it between slots (including inside
   the shop window), then reconnect. Check inventory and coins persisted.
4. Try buying with insufficient coins or into an occupied incompatible slot;
   verify both inventory and coins stay unchanged.
5. Repeat at weapon, armor and sundries merchants. For any failure, retain the
   `[ SHOP ]`, `[ SHOP REJECT ]` or `[ INVENTORY ]` server lines and item/quantity.

Repair, anvil upgrades, item exchanges, warehouse, quests and monster AI/combat
remain separate milestones. This feature makes no change to tooltip rendering.
