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
