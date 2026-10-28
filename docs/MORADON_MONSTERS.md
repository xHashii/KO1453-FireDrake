# Expanded Moradon monster placements

Current source/protocol direction is unchanged. This upgrade applies the supplied
expanded-Moradon spawn geography to our existing monster definitions. Other hosted
maps keep their authored source placements; automated bounds/prototype/terrain
checks cover their monster rows too. No whole-database import or client change.

## Data and population

`MORADON_MONSTER_PLACEMENTS.json` records the source ZIP SHA256, full applied rows,
exact/adapted provenance and each adaptation's original row, family anchor and
coordinate offset. Source: the owner's previously supplied world-table SQL,
recovered from repository history without restoring obsolete protocol code or
large import archives.

- **161 exact supplied rectangles, 52 existing K_MONSTER IDs, 457 monsters.**
  Rectangle endpoints, count, direction, respawn and spawn flags match the supplied
  Moradon rows. The unused donor `Room` column is omitted (all these rows have 0).
- **34 adapted legacy rectangles, 12 additional monster IDs, 189 monsters.**
  Preserve every legacy Moradon monster type, including the four existing
  Kekurikekukaka instances. Each missing variant is placed near the corresponding
  new-map monster family, with small explicit offsets to avoid directly stacking
  on the donor rectangle. These are NOT claimed exact donor coordinates.
- **Total: 195 rectangles, 64 types, 646 monsters**, versus the previous 87 rows /
  474 monsters. The population increase is deliberate: preserve legacy-only
  content while adopting the complete new-map layout, rather than silently
  deleting species or thinning the supplied counts.

Adapted family pairs: Kecoon fighter -> warrior; Kecoon raider/Kekurikekukaka ->
captain; Bulky bulcan -> Giant bulcan; Small bulcan -> Bulcan; Fury gavolt ->
Giant gavolt; Earth worm -> Worm; Carrion crawler/Snatcher -> Blood worm; Wild
Bandicoot -> Bandicoot; Rabid/Cannibal Bandicoot -> Scavenger Bandicoot. Existing
variant/boss population and respawn intervals are retained. New exact rows use
their supplied 30-second interval. Existing monster stats, model IDs, names,
loot/XP definitions and combat behavior are not imported or rewritten. Two source
name spelling differences (251,257) do not rename our monster definitions.

WorldCatalog distributes each authored count inside its spawn rectangle using
the existing deterministic placement rule, calculates Y from active terrain and
preserves the respawn interval. This is spawn geography, **not wandering AI**.
Patrol, proactive aggro, chasing, leash/return, navigation/collision and boss event
scheduling remain separate work. Existing four boss instances/32000-second
interval are retained source configuration, not a new certified retail schedule.

## Apply and recover

Update/rebuild/restart GameServer. After NPC layout stages, startup independently
applies monster layout stage 4, only if server 1 / zone 21 uses
`moradon_expanded.smd`. Custom map references are skipped with a warning. No
manual SQL is required; migration 0015's schema is also ensured at startup when
LoginServer automatic migrations are disabled.

A locked persistent sentinel serializes simultaneous starts. Original zone-21
monster rows are saved once in `KO_MORADON_MONSTERPOS_BACKUP`; delete, insert,
definition validation, backup and applied marker commit in one transaction.
Failure rolls back and stops startup. Marker 4 is separate from NPC stages and
ensures future restarts preserve operator changes. This intentionally replaces
all current Moradon monster spawn rows, including custom ones; those are backed
up before replacement. NPCs, objects, other zones and character data are untouched.

For rollback, stop GameServers, save current rows and transactionally replace
only zone-21 ActType<100 rows from `KO_MORADON_MONSTERPOS_BACKUP`. Keep marker 4
applied to prevent immediate reapplication. Use explicit columns if the schema
has changed; original coordinates will again be the pre-upgrade geography.
Never delete the NPC or character tables to repair a placement problem.

## Verification and client acceptance

CI checks every applied row against the manifest, all original species retained,
adaptation population/respawn preservation, all hosted maps' bounds and finite
terrain samples. MariaDB tests exercise custom-map skip, injected insert failure,
rollback, concurrent application, exact original backups, untouched NPC/object/
monster-definition/character rows, real WorldCatalog loading and restart edits.

First successful startup logs `[ MORADON MONSTERS ] Applied 195 rectangles / 646
monsters`. Repeated starts must not add another copy. Client renders only nearby
monsters, not the entire map at once. Suggested field checks (approximate centers):
Kecoon 606,334; Worm 633,395; Bandicoot 612,455; Bulcan 544,374; Gavolt 586,261.

Test on a disposable character: approach several fields, confirm terrain landing
and appearance, attack/kill, verify XP/loot, wait for respawn and reconnect.
Also check NPCs/gates/Inn remained at their new positions. Bounds and finite Y do
not certify collision, model availability, terrain walkability or visual placement.
Native-client acceptance is pending.
