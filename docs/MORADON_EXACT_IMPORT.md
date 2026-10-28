# Owner-uploaded Moradon definitions and exact layout

This experiment supersedes the earlier coordinate-only/adapted layouts. Source:
root `0002_data.zip`, member `0002_data.sql`, SHA256 recorded in
MORADON_EXACT_IMPORT.json. The upload is a converted 1534 database, not the earlier
world-table archive: its Moradon content is different. The full dump is NEVER
executed. No imported accounts, passwords, characters, inventory, ITEM tables,
procedures, SQL-mode directives or protocol changes are applied.

## Applied scope

- Zone 21: **48 static NPC spawn rows / 48 instances / 38 IDs**.
- **75 monster rectangles / 375 instances / 37 IDs**.
- **33 object rows**, including exact nation-gate/anvil placement and source map
  annotations. Type-50/index-0 annotations do not become fake NPCs. Four object
  controls 1019..1022 have no source NPC definition and remain without a proxy.
- 39 NPC definitions (38 spawn IDs plus anvil 5001) and 37 monster definitions
  are stored privately for Moradon. Model, name, size, faction/type, stats and
  runtime-loaded AI properties use uploaded source values, subject to existing
  runtime safety caps. Names already truncated in the upload are not invented.
- Full supplied spawn rows replace ALL earlier zone-21 spawns. No old guard,
  statue, boss or adapted variant is kept unless present in this upload. The
  previous mixed population of 646 is intentionally replaced by 375.
- Neither global K_NPC nor K_MONSTER is overwritten. WorldCatalog selects private
  definitions ONLY for zone 21 on moradon_expanded.smd. Shared IDs elsewhere keep
  their original models, stats and services. Runtime actor IDs can change across
  a restart because the world allocation depends on population; they are ephemeral.

## Explicit compatibility limits

This is a world-content import, not a 1534 executable/protocol conversion. NPC
services still use existing handlers. Newly placed rental, trainer, dispatch,
scarecrow and quest NPCs may display but do not gain functional rental/pet/quest/
training systems merely by importing their definitions. Unsupported types retain
an explanatory service message; no rental item is sold as a permanent item.
Client models/textures still must exist in the deployed client. Adding map objects
alone does not prove every NPC model or client menu is available.

Existing 1453 ITEM/drop-generation tables remain authoritative. Shared monster
IDs retain those existing drop-group references. Four new IDs use explicit legacy
family fallbacks: 1056->1052 (Battalion), 1057->1003 (Rotten Eyes), 1058->1054
(undying), 1180->1150 (Skeleton). These sItem adaptations are documented in the
manifest; all other supplied compatible definition columns are imported unchanged.
The old schema's extra sLightR field, absent from source, defaults to 0. No source
monster spells or new services are implemented by this import. Map/AI collision
and client asset limitations from BASIC_MONSTER_AI.md remain.

## Deployment and safety

Pull, rebuild/publish, stop old GameServer processes and restart. No manual SQL
is required. Startup runs independent layout stage **5** after prior layouts, then
loads the world. Schema 0016 is also ensured when LoginServer automatic migrations
are disabled. Custom map filenames are skipped; only moradon_expanded.smd applies.

The first import replaces custom zone-21 placement rows too, with complete backups
in KO_MORADON_PREIMPORT_NPCPOS and KO_MORADON_PREIMPORT_OBJECTPOS. A locked marker
serializes concurrent starts. Private definitions, both backups, both placement
tables and marker commit atomically. Failure rolls back and stops startup. Later
starts preserve edits and never reapply earlier approximations.

Expected log: `[ MORADON IMPORT ] Applied owner upload: 48 NPCs, 375 monsters,
33 object rows`. Objects 1019..1022 can produce missing-proxy warnings as noted
above; those warnings do not mean a database account/item row was missing.

## Rollback

Stop ALL GameServers and take a current database backup. In a transaction, restore
zone-21 K_NPCPOS and K_OBJECTPOS from the two PREIMPORT backups, then clear BOTH
KO_MORADON_IMPORTED_NPC and KO_MORADON_IMPORTED_MONSTER catalogs. Keep stage 5's
applied marker: the loader treats both empty catalogs as rollback, and startup
will not reimport. Do not reset markers 1..4. Use explicit columns if schemas have
changed since import. Restart to rebuild the cached world. Restoring a full
snapshot removes edits made after import, so save current rows first.

## Verification / native acceptance

`tools/build_moradon_import.py --check` regenerates only allowlisted world rows
from the archive and rejects drift. Python tests compare every placement/object
and compatible definition field directly to the uploaded member, plus global/
other-zone preservation. MariaDB tests cover custom-map skip, partial-failure
rollback, concurrent application, backups, actual private definitions in runtime,
new NPC/monster instantiation, loot resolution, other-zone runtime content,
character preservation, operator edits and complete rollback.

On the actual client check: Kate 887,477; Helard 816,626; scarecrows around 758..776,
390..422; both Inns, shops, gates/anvil; Kecoon fields around 606,334. Kill a few
monsters, inspect XP/loot/respawn and reconnect. Check at least one other zone.
Native visual/service compatibility is pending, not certified by CI.
