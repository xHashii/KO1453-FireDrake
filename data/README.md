# Runtime assets drop zone

Put the original server **runtime files** here. Keep the original file
names and casing exactly as they are — the database (`ZONE_INFO`) and the
quest tables reference them by name.

| Files you have | Put them in | Why |
|---|---|---|
| Zone maps `*.smd` | `data/map/` | Original AIServer loaded `./map/<name>.smd` (`shared/globals.h` `MAP_DIR`); the file name for each zone is `ZONE_INFO.strZoneName` in the DB |
| Quest scripts `*.Lua` | `data/Quests/` | Original GameServer loaded from `./Quests/` (`LUA_SCRIPT_DIRECTORY` in `GameServer/LuaEngine.h`) |
| Event files `*.aievt`, `*.evt` | `data/` | AI/zone event data used by the merged AI/GameServer |

```
data/
  map/          <- *.smd
  Quests/       <- *.Lua
  *.aievt       <- event files at this level
```

Notes:

- Nothing in the C# servers reads these yet — they're inputs for the
  GameServer milestones (zone loading, heights/collision, warps, spawns)
  and the Lua quest engine.
- These assets are **committed directly on
  `arena/a8daa4cb-ko1453-firedrake`** (owner decision 2026-10-23). After
  dropping files in, run `git status`, then `git add data`, commit, and
  push this branch so the GameServer work can use them. Before pushing,
  check the largest single `.smd`: GitHub blocks single files over 100 MB
  and warns over 50 MB.
- The build will later copy from here next to the GameServer executable
  (same pattern as the `sql/kn_online` migrations).

## Filling it in, step by step

1. **Pick the right content version.** This server source runs quests as
   **Lua scripts**: `GameServer/LuaEngine.h` sets `LUA_SCRIPT_DIRECTORY` to
   `./Quests/`, and `QUEST_HELPER.strLuaFilename` names the script each NPC
   event runs. Of two content packs, use the one that **ships the `.lua`
   quest scripts** - a pack without them belongs to a server generation this
   Lua engine cannot drive. Maps exist in both packs; only one set matches
   this database, and step 3 tells you which.
2. **Copy, do not rename**, keeping casing exactly:
   - zone maps `*.smd` -> `data/map/`
   - quest scripts `*.lua` -> `data/Quests/`
   - event files `*.aievt`, `*.evt` -> `data/` (this level)
3. **Verify before committing:**

   ```powershell
   python tools\check_data_assets.py
   ```

   It compares `data/` against what the database references and prints
   `MISSING` (a zone the database knows but you did not drop in), `STRAY`
   (a file no zone references - usually the wrong pack) and `CASE` (git and
   Linux keep the case Windows ignores). Exit code 0 means it lines up;
   while `data/map/` is empty it exits 0 too, which is the fresh-clone/CI
   state.
4. **Commit and push** so the assets travel with the branch (owner decision,
   see above):

   ```powershell
   git add data
   git commit -m "data: zone maps and quest scripts"
   git push
   ```

5. **What is still dormant today:** no C# server reads these files yet -
   zone loading arrives with the world-entry milestone - and the converted
   database ships **zero `QUEST_HELPER` rows**, so Lua scripts stay unused
   until quest table data is imported. If your `.lua` pack also ships quest
   table SQL (QUEST_HELPER / QUEST_MENU / QUEST_TALK / QUEST_MONSTER rows),
   keep it: that is the import that wakes the scripts up.

## Getting your content zips to the agent

Two routes, in order of preference:

1. **Chat attachment.** Drag the zip(s) straight into the Arena chat. They
   land in the agent's sandbox, where they are unpacked, compared and placed
   for you. Best when every zip is comfortably under ~50 MB.
2. **Throwaway drop branch** (the `db-drop` pattern, for anything bigger).
   From your checkout, once per zip - runs on the same branch accumulate:

   ```powershell
   .\tools\send_dump.ps1 -DumpPath "C:\path\to\first.zip"  -Branch content-drop -BaseBranch arena/7eda6ad8-ko1453-firedrake
   .\tools\send_dump.ps1 -DumpPath "C:\path\to\second.zip" -Branch content-drop -BaseBranch arena/7eda6ad8-ko1453-firedrake
   git push   # the script already pushed; this is a no-op safety net
   ```

   Then tell the agent the drop is complete. It fetches `content-drop`,
   unpacks, chooses, commits the result to `data/` on the working branch and
   tells you when to delete `content-drop` on GitHub. The script re-zips what
   you give it (nested zip is expected and handled) and splits payloads over
   95 MB automatically.

Either way, send **every** candidate version - the choice between packs is
made by comparing them, not by guessing:

- how many of the 24 `ZONE_INFO.strZoneName` maps each pack covers
  (`python tools/check_data_assets.py` after unpacking answers exactly this);
- whether quest `*.lua` scripts are present and how many, plus any
  `*.aievt` / `*.evt` event files;
- whether the pack smuggles in client-only files that do not belong here.

The agent unpacks into a scratch area first, runs the checker against each
pack, reports the comparison, and only then copies the winner into
`data/map/`, `data/Quests/` and `data/` and commits.

## Intake record (2026-10-23)

Three candidate packs arrived through the `content-drop` branch (SHA-256
checked against the dropped MANIFEST.txt). Comparison:

| Pack | Maps `.smd` | Quests `.lua` | Events | Verdict |
|---|---|---|---|---|
| Maps & Quests.7z | 24 - exact `ZONE_INFO` match, casing included | 114 (`01_main.lua` + `NPCID_Name.lua`) | 8 `.aievt` room events (what `AIServer/MAP.cpp` loads) | **adopted** |
| MAP.7z | 22 of an older generation (`karus_0730.smd`, `battle1_1111.smd`...) - no database match | 0 | 8 legacy `.evt` | rejected |
| QUESTS.7z | 17 of the same older generation | 3 zone-numbered legacy scripts | 19 legacy `.evt` | rejected |

Placed from the adopted pack: `Map/*.smd` -> `data/map/`,
`Quests/*.lua` -> `data/Quests/`, `Map/*.aievt` -> `data/`. Excluded:
`Map/map.rar` and `Map/WarpGateEditor.exe` (authoring tools, not server
content). The two rejected packs belong to an older content generation whose
map filenames match nothing in this database, and legacy `.evt` files have no
loader anywhere in this server source - only `%d.aievt` is read - so nothing
usable was left behind by rejecting them.
