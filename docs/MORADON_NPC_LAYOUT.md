# Moradon NPC layout (expanded map)

Owner requested that existing 1453 NPCs use the supplied 1534 Moradon locations.
The executable/protocol, NPC prototypes, shops, quests and service implementations
remain 1453. No donor NPC definitions, new rental/familiar services, monsters or
protocol extensions are imported.

## What moves

The reviewed coordinate manifest is [MORADON_NPC_PLACEMENTS.json](MORADON_NPC_PLACEMENTS.json).
It records source ZIP SHA256, all coordinates/directions, object positions and
unmatched IDs. Original source: owner-uploaded `1534-world-tables.zip`, recovered
from repository history before import cleanup. All 21 shared prototype IDs also
have matching trimmed names in both source tables.

- 21 existing K_NPC IDs use the donor's 28 authored spawn rows, including the
  field potion/sundries/Inn outposts. Only placement data is adopted.
- Guards 11021/21021 follow the donor's two positions per nation instead of the
  old formations. Counts consequently change; this is not one-for-one movement
  of every old guard instance. Main supported spawn population changes from 41
  to 28; total static K_NPCPOS population in Moradon changes from 57 to 44.
- Existing gates 4013/4014 and anvil 5001 move to their donor coordinates while
  retaining 1453 object indices, controls, types, nation and status.
- NPC Y still comes from active SMD terrain. Object Y uses donor object placement.
- NPC definitions, selling groups, stats, monster rows, other zones, characters,
  inventory and START_POSITION are not modified.

Key places (X,Z):

| Existing service | Position |
| --- | --- |
| Inn Neria | 770,648 |
| Inn Nia | 859,648 and field outpost 409,555 |
| Healing Potion Karpis | 855,561 (also 48,897 and 377,572) |
| Sundries Zarta | 868,530 (also 84,923 and 419,545) |
| Armor Hesta | 786,595 |
| Weapon Gargameth | 786,587 |
| Accessories Hera | 697,620 |
| Scrolls Charon | 821,607 |
| Blacksmith Heppa | 729,580 |
| Anvil | 816.136,607.173 |
| Karus gate (211) | 797.696,526.879 |
| Elmorad gate (212) | 837.360,526.757 |

### Deliberate unmatched remainder

Six existing IDs have no zone-21 NPC placement in the donor. Their 16 existing
spawn rows/instances are retained unchanged, not guessed, hidden or deleted:
521 (Arendil), 13003 (Clepio), 16073/16074 (hero statues), and legacy guard
variants 11020/21020 (six guards each). Donor-only NPC IDs are not added. Thus
this completes the exact matched layout, not an invented placement for every
legacy NPC. Those remaining IDs need a separate placement/removal decision.

## Deployment and safety

Update code, rebuild/publish with data/map, then restart GameServer. Startup runs
EnsureMoradonNpcLayoutAsync after selecting the expanded map and before loading
WorldCatalog. No manual SQL is required. It runs only for server 1 / zone 21's
`moradon_expanded.smd`; custom map references are skipped with a warning.

Migration 0012 supplies layout state and backup tables, and startup also creates
them if automatic migrations are disabled. Placement application uses an InnoDB
transaction and locked persistent sentinel, so simultaneous starts serialize and
backup/deletes/inserts/object updates/marker commit together. Failure rolls back
all those writes and fails startup rather than loading a partial layout.

Original zone-21 static spawns and object rows are preserved in
`KO_MORADON_NPCPOS_BACKUP` and `KO_MORADON_OBJECTPOS_BACKUP`. The one-time marker
in `KO_MORADON_LAYOUT` prevents later restarts from overwriting operator edits.
The upgrade replaces all current static placements for the 21 matched IDs; any
custom placements of those IDs are also backed up, then replaced intentionally.
Unmatched and monster rows are retained.

For rollback, stop all GameServers and restore zone-21 static K_NPCPOS and
K_OBJECTPOS rows from those backups in a transaction (with explicit matching
columns if the schema has since changed). Keep the applied marker, otherwise the
next startup applies the layout again. Restoring the full snapshot will also
remove any placements added after the upgrade, so back up current rows first.
These tables contain placement data only, never saved item/character data.

## Verification and native acceptance

CI checks every authored coordinate/direction/count against the extracted
manifest, full monster/other-zone/prototype/unmatched row preservation, and
terrain availability. MariaDB tests cover custom-map skip, injected insert failure
with rollback, concurrent startup/one backup, actual WorldCatalog loading and
preservation of post-upgrade operator edits.

After deploying, look for `[ MORADON NPC ] Applied expanded layout` on first start.
Use H-menu Town (816,543), check both gates and anvil, then walk to the shops/Inn
above. Verify their existing menus and warehouse still open. Check field outposts,
restart once, and ensure NPCs are not duplicated. Native-client acceptance and
stair collision remain unverified; moving NPCs does not add new NPC services.
