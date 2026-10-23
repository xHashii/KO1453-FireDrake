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
