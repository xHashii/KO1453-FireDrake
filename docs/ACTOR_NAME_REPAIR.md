# NPC and monster display-name repair

Audit covers global K_NPC (511 rows), global K_MONSTER (790), imported Moradon
NPC definitions (39) and monster definitions (37). The uploaded SQL itself
contains clipped 20-character names and backticks; this is not a packet-length
problem. Existing actor name storage/wire limits support all restored names
(maximum 30 Latin-1 bytes); no column or protocol widening is needed.

`World/ActorNameRepairs.json` is a reviewed list of **78 guarded corrections**:
19 global NPC, 31 global monster and 28 imported NPC names. Imported monsters
were audited too; none of those 37 names requires a truncation/backtick fix.

- Role delimiters become brackets, e.g. `[Accessory Merchant]Hera`.
- Full names include Gargameth, Karpis, Kaishan, Sodern, Laidas, Berret, Lazer,
  Osm, Dualer and Porm rather than clipped role/name fragments.
- Possessive punctuation remains an apostrophe: `[Kaishan's Daughter] Menissiah`.
- Global blank/`a` placeholders are restored (Ego, Ultima, Dark Dragon etc.).
  Verified Wold/Wild bulcan and shilan/Silan spelling discrepancies are corrected.
- Full names come from the previously supplied full world-table archive, matched
  by SID and model. The current upload provides a secondary matching reference
  where necessary (e.g. the original Dark Dragon model). The JSON records archive
  provenance/hash and each old/new value. Wratch/Potion spelling is explicitly
  corrected. Internal reference labels such as sohu_npc are not invented anew.
- A different nonempty custom name, or a different model using the same SID, is
  never overwritten. Complete valid names are not blindly replaced just because
  another database version has a different label for that SID.

## Deployment

Pull/rebuild/restart GameServer, then reconnect clients. Startup repairs names
AFTER the exact import and BEFORE loading cached world actors. It also upgrades
already-imported databases without resetting layout markers or rerunning imports.
Fresh imports already contain corrected Moradon names because the build tool
uses the same vetted catalog. The original uploaded ZIP is not edited.

Schema 0017 creates KO_ACTOR_NAME_BACKUP; startup ensures it when automatic
migration is disabled. Actual matching old names are backed up before updates.
All name updates/backups commit together; failure rolls back. Row locks serialize
simultaneous starts. Repeated starts perform no changes once known defects are
fixed. Startup log: `[ ACTOR NAMES ] Repaired N known damaged NPC/monster name(s)`.

Only strName changes. No IDs/models, services, stats, rewards, coordinates,
characters or inventory are rewritten. NPC models/assets and unimplemented
services remain separate issues; this does not make rental/trainer menus work.

## Verification / rollback

Python checks all loaded catalogs for known backticks/blank/`a` placeholders,
name limits, preservation of non-name columns and custom-label guards. Existing
import drift checks now permit only the vetted name transformations. MariaDB
covers old-import upgrades, concurrent application, atomic failure/backup rollback,
full names/apostrophes, blank-monster recovery, custom names, unchanged stats/
placements/saved data, and restart idempotency.

For rollback stop GameServers, back up current data and restore only matching
NewName rows from KO_ACTOR_NAME_BACKUP using TableName/Sid/Model. Disable the name
repair first, otherwise startup correctly repairs those old damaged names again.
Do not restore full character/placement tables to undo a display-name correction.
Native visual confirmation remains pending after restart/reconnect.
