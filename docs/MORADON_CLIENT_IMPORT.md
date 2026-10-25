# Bringing a different Moradon into this client and server

**Not generally drag-and-drop.** Client visuals and server geometry are separate.
Work on copies and use assets you are permitted to reuse. This guide does not
change your current map, database or characters. The donor client/build and its
actual Moradon files have not yet been supplied, so no extractor or conversion
compatibility has been verified for that donor.

## 1. Preserve a working baseline

1. Close both clients and stop GameServer before replacing runtime assets.
2. Back up your working client, donor client, database, server map directory and
   relevant configuration. Use separate folders for the experiment.
3. Record both client versions and take a donor Moradon screenshot. Do not replace
   your executable, whole Data folder, UI or item/skill tables as a shortcut.

## 2. Identify the donor's actual map entry

1. In the donor client's `Data` directory, locate its zone table (often named
   `Zones.tbl` or a localized/variant zone table). Use a **version-compatible KO
   table viewer** to inspect the Moradon entry. Table layout/encryption varies;
   do not edit it with a text editor or assume an arbitrary modern viewer works.
2. Record its zone ID and every referenced filename, not just files whose names
   contain "moradon". Our game uses **zone 21** for Moradon. The donor's numeric
   entry may differ; importing a filename does not change the network zone ID.
3. Client reference `Open-KO/KnightOnline`, `GameDef.h::__TABLE_ZONE`, documents
   terrain, color/light maps, object placement, minimap, sky and other resources.
   Its layout is useful evidence, not proof of this donor's exact table format.

Safe read-only listing in PowerShell (replace the example path):

```powershell
$donor = 'D:\KO-Donor'
Get-ChildItem "$donor\Data" -File | Where-Object Name -Match 'zone.*\.tbl$'
Get-ChildItem $donor -Recurse -File |
    Where-Object { $_.Name -match 'moradon|\.gtd$|\.opd$|\.smd$' } |
    Select-Object FullName, Length
```

This is discovery, **not an extraction/conversion script**. Do not send account,
launcher/server configuration, passwords, logs or the entire client as a first
step; send the versions, table row/file references and relative filenames.

## 3. Extract the complete client dependency set

- If those resources are ordinary files, copy them into a staging folder while
  retaining their original relative paths. Extraction is unnecessary for loose
  files. Do not rename them indiscriminately.
- If they are packed, first identify the actual archive format. A normal ZIP/7z
  may be extracted with a normal archiver; proprietary KO containers need a
  trusted extractor compatible with that format/build. Do not assume all KO
  packs use the same container or download/run an unidentified repack tool.
- Start from the zone row: terrain (`.gtd` in applicable clients), terrain
  textures/color/light maps (`.tct`/`.tlt` in some builds), object placements
  (`.opd`, possibly extensions), minimap, sky, effects/sound references, etc.
- Follow object/model dependencies recursively: placed shapes may reference
  meshes and textures elsewhere. Copy their dependency closure, not the entire
  donor Shape/Item/Character folders. Folder names/extensions depend on version.
- Never rename a `.gtd` or `.opd` to `.smd`: they are different binary formats.

## 4. Make the working client reference those assets

1. Copy the staged dependencies into the **test client** with the same relative
   paths. Avoid overwriting shared filenames where the donor differs; a shared
   texture/model replacement can unexpectedly change other zones.
2. With a compatible table editor, update only the working client's Moradon
   zone entry to the new resource references. Keep server/client numeric zone
   mapping consistent (21 here). Preserve unrelated rows and table schema.
3. A newer donor format may not load in the 1453 executable even when every file
   is present. Conversion/export or a compatible older asset set may be needed.
   Do not swap executables to conceal this: it changes the server protocol too.

## 5. Obtain matching server geometry — the critical part

This C# server loads **`.smd` files**, not the client `.gtd`/`.opd` visuals.
The current shipped Moradon is:

```text
zone: 21
ZONE_INFO.strZoneName: moradon_20060124.smd
repository asset: data/map/moradon_20060124.smd
```

Read-only check of the configured live game database:

```sql
SELECT ServerNo, ZoneNo, strZoneName, InitX, InitZ, InitY
FROM ZONE_INFO WHERE ZoneNo = 21;
```

Best option: obtain the **matching server `.smd` and spawn/object/warp data** from
that map's legitimate server/content pack. A client generally does not ship the
server SMD. If you only have client files, they may be insufficient: a compatible
N3/map editor with a real server-SMD exporter must regenerate the required height,
collision/object/event/warp structures. Whether such an exporter supports the
donor is unknown until its format is identified. There is no universal client-to-
SMD converter in this repository. Do not promise lossless conversion from just
one terrain file or generate fake empty collision/warp data to make it load.

Once a matching supported SMD exists:

1. Stage it under `data/map/` with a distinct filename. Update the configured
   zone-21 server map reference deliberately; do not rename the new map to the
   old name and assume coordinates/warps now match.
2. Check terrain size/spacing/heights and loadability in `SmdMap.cs`. This reader
   has format/dimension limits; a different generation may require parser work.
3. Update zone spawn/init positions and supported START_POSITION entries, NPC and
   monster placements (`K_NPCPOS`), objects/gates (`K_OBJECTPOS`), and destination
   warps/other map references. Preserve prototype IDs where appropriate, but do
   not preserve coordinates merely because their rows exist.
4. Coordinates differ by representation: persisted USERDATA/zone initialization
   uses world * 100, network positions world * 10, and NPC/map fields have their
   own source units. Do not apply one scaling factor indiscriminately.
5. Rebuild/publish and check the **actual deployed** map directory and configured
   database. The build copies runtime maps; editing only a development copy may
   not change a published server. Do not patch applied migration files—ship an
   explicit new migration once the replacement is accepted.

## 6. Validate before moving players

- Enter with a test character at a verified walkable coordinate. Check terrain
  alignment, ground height, walls/objects, minimap, textures, lighting and sound.
- Test the Inn, NPC shops, gates and every exit destination at the new coordinates.
  Use two clients to check matching positions and visibility.
- Reconnect and confirm saved positions remain valid. Check ordinary item/bank
  transfers and combat targets near the changed NPC/spawn locations.
- Existing characters may be saved inside a new wall/outside the new map. Plan a
  targeted relocation with a backup; do not mass-reset characters or inventory.
- Run repository checks, but understand their scope:

```powershell
python tools/check_data_assets.py
python tools/test_world_coordinates.py
python tools/test_world_npcs.py
```

These checks use **committed shipped SQL/assets**, not your live DB or donor's
client. Existing exact Moradon/warp fixtures intentionally refer to the original
map. Review/update those expectations as part of an accepted map migration;
a passing old fixture is not evidence that the new client geometry matches.
The current C# world also lacks full collision/pathfinding enforcement, so visible
alignment alone is not collision or anti-cheat certification.

## What to provide next

Send the donor/working client versions, screenshot, Moradon zone-table row, its
referenced relative filenames and whether a matching `.smd`/server pack exists.
Then we can identify the actual dependency set, choose a compatible extractor/
export path and prepare a controlled zone migration. Until then, **copying one
Moradon file into both client and server is not a safe installation procedure**.
