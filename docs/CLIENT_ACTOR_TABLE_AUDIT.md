# Supplied 1453 / 1534 client actor tables

## Findings

Inputs are the owner's updated `1453tbl.zip` and `1534tbl.zip` (three files each).
Exact hashes and every added/removed/changed record are recorded in
[CLIENT_ACTOR_TABLE_AUDIT.json](CLIENT_ACTOR_TABLE_AUDIT.json). Inputs are unmodified.

| File | 1453 rows | 1534 rows | Donor-only | 1453-only | Shared IDs changed |
|---|---:|---:|---:|---:|---:|
| Npc_us.tbl | 547 | 582 | 74 | 39 | 90 |
| Mob_us.tbl | 823 | 855 | 32 | 0 | 823 |
| NPC_Looks.tbl | 260 | 285 | 25 | 0 | 12 |

For shared Npc/Mob IDs, **no appearance-reference integer changes** were found.
The NPC changes are labels; the Mob changes include Korean-to-English labels
and 32 changes to the fourth integer. The meaning of that integer is not assumed
and existing values are not overwritten.

NPC_Looks shared changes are numeric fields only: IDs 6601, 14003–14006 and
26001–26007. Their model path fields are unchanged. The patch deliberately keeps
all original 1453 values instead of importing those unrelated donor differences.

### Current imported Moradon actors

- **13 missing NPC records:** 19001–19006 (Laidas/Berret/Lazer/Osm/Mekin/Dualer),
  19067–19072 (six scarecrows), 29001 (Porm).
- **4 missing monster records:** 1056 Battalion, 1057 Rotten Eyes, 1058 undying,
  1180 Skeleton. Their appearance IDs 1000/1100 already exist in 1453 NPC_Looks.
- Also absent in Mob_us: 4081–4083, the donor's three scarecrow monster records.
- **7 missing character appearances:** 4081–4083, 19067–19069, and 19005.
  These are the six scarecrow variants plus the fortune reader.
- Every imported NPC/monster with an existing 1453 Npc/Mob record has the same
  appearance integer as the server definition. No guessing/remapping was needed.
- Appearance 5001 is absent from BOTH NPC_Looks versions: the magic anvil is a
  map-object proxy, not a character appearance to fabricate.

The 1453 NPC_Looks has **no scarecrow rows**. Its first/minimum-ID row is 100,
Kecoon. This is consistent with the observed Kecoon fallback when lookup fails.
The Open-KO reference `GameProcMain.cpp::MsgRecv_NPCIn` explicitly falls back to
`GetIndexedData(0)` for a missing appearance. That reference is not certification
of the user's exact binary, but the missing entries themselves are confirmed.

Npc_us/Mob_us hold names and appearance references, not actual model filenames.
NPC_Looks holds joints, animations, character/part paths and numeric settings.
The previous suggestion that Npc_us/Mob_us alone might fix rendering was incomplete.

## Direct editing is possible — no manual TBLeditor work needed

Both NPC_Looks files have **the same 38-column type sequence**. Mob_us has the
same four-column layout. Npc_us differs: 1453 has **3 columns**, 1534 has **7**.
Therefore replacing the entire 1453 NPC file with the donor would be unsafe.
The missing donor records can be projected to the existing three-column layout;
the four donor-only fields are NOT imported into the 1453 client.

An optional additive patch is prepared at:

`client-patches/moradon-1453-tables.zip`

| Output | Added rows | Final rows |
|---|---:|---:|
| Npc_us.tbl | 13 | 560 |
| Mob_us.tbl | 7 (4 imported mobs + 3 scarecrows) | 830 |
| NPC_Looks.tbl | 7 | 267 |

Every existing 1453 row/field, including raw name bytes, remains unchanged.
New imported-actor labels match the current server's repaired labels rather than
alternative donor translations. New appearance rows are copied unchanged from
1534. No other donor-only entries, existing numeric flags or server data change.
The original uploaded archives remain untouched. This patch is not auto-installed.

### Installation / rollback

1. Close the client. Back up its current `Data/Npc_us.tbl`, `Data/Mob_us.tbl` and
   `Data/NPC_Looks.tbl` together.
2. Extract the three files from `moradon-1453-tables.zip` into the client's `Data`
   folder, replacing those files. Do NOT extract the whole donor archive instead.
3. Restart the client and check all six scarecrow variants and the fortune reader.
4. Restore the three backed-up files to undo the patch.

No server restart, SQL or TBLeditor entry-by-entry work is required for this patch.
If the locally installed files differ from the supplied 1453 archive, rebase the
patch onto those files first rather than overwriting other local custom changes.

### Assets still need checking

The patch repairs confirmed missing table records. It does not supply model files
or prove that the 1453 executable can render every donor asset. New scarecrow rows
reference these stems under `chr/`, each with `.n3joint` and `.n3chr`:

- `npc_mora_training_suit_low_2007`
- `npc_mora_training_suit_middle_2007`
- `npc_mora_training_suit_high_2007_mb`
- `npc_mora_training_move_low_2007`
- `npc_mora_training_move_middle_2007`
- `npc_mora_training_move_high_2007_mb`

They also reference `chr/default.n3anim`. The fortune reader uses
`chr/npc_mo_caron.n3joint`, `chr/npc_mo_caron.n3anim` and
`chr/npc_mo_shaman2007.n3chr`. Character files may reference further parts/skins/
textures; merely having the named joint/character files does not prove all nested
assets are present. These assets were not included in the TBL archives.

## Reproducibility / validation

Run `python tools/audit_client_actor_tables.py --check`.
Without `--check`, the tool regenerates the report and optional patch only.
It decrypts/parses all six input files, checks complete consumption and unique
IDs, then re-encrypts them **byte-for-byte identical** before building the patch.
Outputs are decrypted/reparsed and checked for unchanged schemas, preservation of
every old record, exact intended additions and imported-ID coverage. Native client
rendering remains a separate pending check.

Cipher/layout reference: Open-KO/KnightOnline `src/N3Base/N3TableBase.h`,
`N3TableBaseImpl.cpp` and `N3TableBaseImpl.h`. No wholesale donor replacement,
version switch, SQL import or executable patch is involved.
