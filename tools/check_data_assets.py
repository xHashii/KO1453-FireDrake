#!/usr/bin/env python3
"""Cross-check the data/ runtime-asset drop zone against the database.

The GameServer milestones read their world from files dropped into data/
(see data/README.md): zone maps under data/map/, Lua quest scripts under
data/Quests/, AI/zone events at data/ level. The database references those
files by name - ZONE_INFO.strZoneName for maps, QUEST_HELPER.strLuaFilename
for quests - so a misplaced or renamed file only fails later, at runtime,
in a zone the tester happened to enter. This tool fails early instead:

  * zone maps referenced by ZONE_INFO but absent from data/map/
  * files in data/map/ that no zone references (stale or wrong-pack content)
  * case mismatches (Windows does not care, git and the Linux side do)
  * quest scripts referenced by QUEST_HELPER but absent from data/Quests/
  * single files above GitHub's comfortable push sizes (warn 50 MB, block 100 MB)

Exit status is 0 while data/map/ holds no .smd at all - that is the state of
a fresh clone and of CI, where the assets were never dropped in - and 1 once
assets exist but do not line up with the database.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]

# Keep both original assets (rollback) and names introduced by explicit map migration.
DATA_SQL_GLOB = ("sql/mysql/kn_online/0002_data.sql", "sql/mysql/kn_online/0003_data.sql",
                 "sql/mysql/kn_online/0011_moradon_expanded.sql")

# Quoted literals ending in .smd / .lua. Data files are huge, so this is a
# line-oriented scan rather than a parse; map and quest filenames only ever
# appear as quoted literals in ZONE_INFO / QUEST_HELPER inserts.
SMD_RE = re.compile(r"'([^'\n]+\.[sS][mM][dD])'")
LUA_RE = re.compile(r"'([^'\n]+\.[lL][uU][aA])'")

WARN_SIZE = 50 * 1024 * 1024    # GitHub starts warning here
BLOCK_SIZE = 100 * 1024 * 1024  # GitHub rejects pushes above this


def referenced_names(root: Path, pattern: re.Pattern[str]) -> dict[str, str]:
    """lowercased name -> name as spelled in the database."""
    found: dict[str, str] = {}
    for relative in DATA_SQL_GLOB:
        path = root / relative
        if not path.exists():
            continue
        with path.open(encoding="utf-8", errors="replace") as handle:
            for line in handle:
                for name in pattern.findall(line):
                    found.setdefault(name.lower(), name)
    return found


def dropped_files(directory: Path, suffix: str) -> dict[str, Path]:
    if not directory.is_dir():
        return {}
    return {p.name.lower(): p for p in sorted(directory.iterdir()) if p.suffix.lower() == suffix}


def report(label: str, referenced: dict[str, str], dropped: dict[str, Path], where: str) -> list[str]:
    problems: list[str] = []
    missing = sorted(set(referenced) - set(dropped))
    stray = sorted(set(dropped) - set(referenced))
    miscased = [n for n in sorted(set(referenced) & set(dropped)) if referenced[n] != dropped[n].name]

    print(f"{label}: {len(referenced)} referenced by the database, {len(dropped)} present in {where}")
    if not referenced:
        # QUEST_HELPER ships empty in the converted database: nothing references
        # quest scripts yet, so every dropped script would look "stray". Report
        # the count and validate nothing until quest rows are imported.
        print(f"  no database references yet - {len(dropped)} file(s) accepted unvalidated")
        return []
    for name in missing:
        problems.append(f"  MISSING  {referenced[name]}  (referenced by the database, not in {where})")
    for name in stray:
        problems.append(f"  STRAY    {dropped[name].name}  (in {where}, no database row references it)")
    for name in miscased:
        problems.append(f"  CASE     {dropped[name].name}  (database spells it {referenced[name]})")
    for line in problems:
        print(line)
    if not problems:
        print("  all referenced files are present with matching names")
    return problems


def size_warnings(directory: Path) -> list[str]:
    problems: list[str] = []
    if not directory.is_dir():
        return problems
    for path in sorted(directory.rglob("*")):
        if not path.is_file():
            continue
        size = path.stat().st_size
        if size > BLOCK_SIZE:
            problems.append(f"  TOO BIG  {path.name}  ({size // (1024 * 1024)} MB; GitHub rejects files over 100 MB)")
        elif size > WARN_SIZE:
            problems.append(f"  BIG      {path.name}  ({size // (1024 * 1024)} MB; GitHub warns over 50 MB)")
    for line in problems:
        print(line)
    return problems


def main(argv: list[str]) -> int:
    root = REPO_ROOT
    if len(argv) > 1 and argv[1] == "--root":
        root = Path(argv[2]).resolve()

    data = root / "data"
    map_dir, quest_dir = data / "map", data / "Quests"

    if not data.is_dir():
        print(f"No {data} directory - see data/README.md for where runtime assets go.")
        return 0

    maps_referenced = referenced_names(root, SMD_RE)
    quests_referenced = referenced_names(root, LUA_RE)
    maps_dropped = dropped_files(map_dir, ".smd")
    quests_dropped = dropped_files(quest_dir, ".lua")

    if not maps_dropped and not quests_dropped:
        print(f"{data} exists but holds no maps or quests yet - nothing to check.")
        print("See data/README.md for the drop-zone layout; this is also the state CI expects.")
        return 0

    problems: list[str] = []
    problems += report("Zone maps", maps_referenced, maps_dropped, "data/map/")
    problems += report("Quest scripts", quests_referenced, quests_dropped, "data/Quests/")
    problems += size_warnings(data)

    if problems:
        print(f"\n{len(problems)} problem(s): fix the names above, then re-run "
              f"`python tools/check_data_assets.py` before committing data/.")
        return 1

    print("\ndata/ lines up with the database; safe to `git add data` and push.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
