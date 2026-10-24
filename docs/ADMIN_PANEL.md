# Admin Panel / Editors — design notes

Goal: a modern GM/admin experience comparable to what the KO private-server
ecosystem already has — web panels like **KOWEB** (start/stop servers, send
console commands, account & character editors, starter-item editor, give item
by letter, NPC/monster/drop/upgrade editors, PUS editor, event editor)
[4](https://www.r10.net/oyun-sunucusu/2521644-knight-online-yonetim-editor-paneli-12xx-24xx.html),
the **Knight OnLine administration tool** for real-time server management from
the xmkg suite [3](https://github.com/xmkg/kol-projects-index), and the wider
tool catalogue at knightonline.dev (KO-AIOEditor, server administration
interface, TBL editors, packet logger) [9](https://knightonline.dev/).
Community GM-panel designs also stress searchable/paged item pickers and
"give directly vs. send by GM mail (safer)" flows
[13](https://forum.ragezone.com/threads/gm-admin-panel.1259122/).

## Proposed modules (build order is the owner's call)

### A. Character & inventory editor (the flagship)
- Search characters by name/account; view level/class/nation/stats/coins.
- **visual inventory grid**: equipment (14), inventory (28), cospre (5),
  magic bags, warehouse (192) — powered by `ItemBlobs.cs` (already written:
  parses `strItem`/`strSerial`/`strItemTime` exactly like DBAgent.cpp).
- Add item (paged item search over the `ITEM` table, icons later), remove,
  change count/durability/expiration, move between slots, clone.
- Give item **by letter** (MAIL_BOX) as the safe default; direct inventory
  edit only when the character is offline (see safety rules).

### B. Account & GM management
- Accounts: search, ban/unban (authority 255), mute, GM flags, premium
  type/time, Knight Cash balance, character list per account.
- Online list from `CURRENTUSER` (and later live from the GameServer API).

### C. Game-data editors (the tables the server loads at startup)
- Item browser (read-only first, edits later), drop editor (`K_NPC_ITEM`),
  NPC/monster browser + spawn positions (`K_NPCPOS`), upgrade rates
  (`ITEM_UPGRADE`), starter items, PUS products.
- Event/registration notices, king notices, login news (replaces INI edits).

### D. Server operations
- Dashboard: server status, online counts (from `CONCURRENT`), recent logins.
- Start/stop/restart LoginServer + GameServer, live log tail, send console
  commands (the ported GM console), scheduled notices.

## Safety rules (non-negotiable)

1. **Never rewrite the inventory blob of an online character** — the original
   saves the whole blob on logout and would clobber the edit (or dupe items).
   Editor refuses, or routes through the GameServer's admin API once it
   exists. Offline edits are safe.
2. Every mutating action writes an **audit log** (admin, target, before/after)
   and a **blob backup** so an edit can be undone.
3. Item serials are unique server-wide (`GenerateItemSerial`); adding items
   must allocate fresh serials the same way, or anti-dupe logic
   (`ITEM_FLAG_DUPLICATE`) will eat them.
4. Admin auth separate from game accounts; the panel binds to localhost by
   default. This is a root-level tool over the whole game database.

## Status (updated)

- ✅ Platform decided: **separate Blazor web app** (`src/KnightOnline.AdminPanel`).
- ✅ Module A (character & inventory editor) implemented: search, equipment /
  inventory / cospre+magic-bag / warehouse grids, slot edit, move/swap,
  add item with serial generation, give-by-letter, online-protection,
  optimistic concurrency, audit log + backups (`sql/mysql/kn_online/0004_admin_tables.sql`).
- ⏳ First live test still needs a local .NET build plus MariaDB/client
  UAT. The converted `USERDATA`/`ITEM` schema and static data are now
  committed, and the repository continues to probe `INFORMATION_SCHEMA`
  for optional columns.

## Architecture options (recorded)

| Option | Shape | Pros | Cons |
|---|---|---|---|
| **Separate web app (Blazor Server)** | `KnightOnline.AdminPanel`, shares `KnightOnline.Database` | Runs anywhere, phone-friendly, same stack as servers, easy auth | Another process to run |
| Built into servers | HTTP admin endpoints inside Login/Game | One deployment, live game state | Couples UI to game process; restart kills panel |
| Windows desktop (WPF/WinUI) | Classic KO tool style | Feels like the legacy editors, offline use | Windows-only, needs DB access from panel PC, updates per machine |

Shared building blocks are UI-agnostic and start now regardless:
`ItemBlobs` (inventory/warehouse), character/account repositories (M3),
audit-log tables.

## UI modernization for the servers themselves

The originals were bare consoles (the C++ even kept MFC dialog remnants,
`*Dlg.cpp`). The C# ports will keep clean consoles for operations, and the
"UI" investment goes into this panel: status dashboard, live logs and
editors — which is also how KOWEB-style tools present it.

## October 2026: character browsing, dashboard and drops

- Character browsing loads on entry; an empty Search browses **all** characters
  with Previous/Next (50 per page), rather than silently stopping at 100.
- Dashboard adds clans, nation character counts, monster definitions, spawn rows
  and drop-group counts. Definitions/rows are not counts of living runtime monsters.
- **Monster drops** edits existing `K_MONSTER_ITEM` rows, searchable by monster
  name or group ID. It does not create/delete groups or edit monster/spawn stats.
  Slots 1–2 use production codes (`<100`, MAKE_ITEM_GRADECODE) or
  MAKE_ITEM_GROUP (`>=100`, randomly selected among 30 entries); only slots 3–5
  are direct ITEM IDs. Independent chances are out of 10000, not out of 100.
  New/activated references are validated; existing broken source references can
  be disabled. Duplicate IDs and stale saves are rejected. Before/after JSON is
  retained in `_ko_admin_audit` in the same transaction as the update.
- Solo in-game loot now resolves this catalog (including procedural/group slots).
  There is no live reload. Restart GameServer after catalog edits. Stationary
  retaliation and selected single-target skills are implemented; full chasing AI,
  party rewards, buffs, DoTs and AoE remain unsupported.
- **Deploy the panel and GameServer from the same updated build.** The server
  holds MariaDB account/character advisory locks from character selection until
  its disconnect save finishes. Admin saves acquire the same locks, including
  the shared account warehouse, closing the old CURRENTUSER check race. The
  session's dedicated non-pooled connection is checked every 30 seconds; a lost
  connection disconnects the player instead of continuing unprotected play.
  Old C++ servers and older rewrite binaries do not participate: stop them
  before editing. CURRENTUSER is retained as a conservative legacy status hint.
- Character concurrency checks now include editable scalar stats/currencies and
  bank coins as well as blobs; duplicated character/warehouse rows are rejected.
  Stored inventory slots beyond the 73-slot visible editor are retained.
  Character item backups remain blob backups, not full database backups.

Before testing, back up the database, rebuild the solution and restart GameServer
and AdminPanel. Browse Characters with no search text, edit an offline character,
verify a second stale editor is rejected, then log in and verify character/account
edits are refused. Open Monster drops, lower a chance, save/reload, and inspect
`_ko_admin_audit`; then restart GameServer and use the end-to-end combat checklist
in `docs/COMBAT_AND_LOOT.md`.
