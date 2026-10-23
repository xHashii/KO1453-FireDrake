# Database: getting the dump across, converting it, and never importing manually

## TL;DR

1. **Do not commit the raw 240 MB SSMS dump.** GitHub rejects files > 100 MB,
   and a full dump contains **player account passwords** (`TB_USER`) — it
   should never land in a git history, especially a public one.
2. What I need first is a **schema-only** export (a few MB at most). Commit
   that to `sql/mssql/`.
3. The full dump stays on **your** machine. Run the converter there, commit
   only its output (schema + static game-data SQL).
4. From then on there is **no manual import**: `docker compose up -d` starts
   MariaDB, and the servers create/upgrade their databases on boot.

---

## 1. Getting the database to me

### What does NOT work

- **Pastebin**: max paste size is 512 KB (10 MB for PRO) — a 120 MB file
  cannot go there, and Pastebin's acceptable-use policy bans password/login
  data (a KO dump contains account passwords). Links to *small* files (a
  procedure, an error log) are fine — I can read those; send the **raw** link.
- **Pasting the dump into chat**: far beyond any context limit. Don't.
- **Git LFS / GitHub Release assets**: work for humans, but their downloads
  are served from hosts my sandbox cannot reach, so I couldn't retrieve them.
- **GitHub web upload of the whole file**: capped at 25 MB per file; normal
  git pushes block anything over 100 MB. Your ~120 MB text file fits neither.

### What DOES work — pick one

**Option A (recommended): schema-only export, a few MB.**
Tables + column definitions + stored-procedure bodies are 90% of what the
port needs. In SSMS, per database: right-click DB → **Tasks → Generate
Scripts…** → Select all objects → **Advanced**: *Types of data to script* =
**Schema only**, *Script Indexes/Primary Keys* = True. Upload the result
with the GitHub web UI (under the 25 MB browser cap) to `sql/mssql/` on any
branch, or push it with git. Player rows stay converting locally via §2.

**Option B: the full file, split into parts, on a throwaway branch.**
This repo is currently **private** (verified) — keep it that way while the
dump is on it. Parts are plain git objects, which I can fetch reliably:

```powershell
# On your PC (in a clone of this repository):
git fetch origin
git switch -c db-drop origin/arena/a8daa4cb-ko1453-firedrake   # base it on the rewrite branch so tools/ exists
python tools\split_dump.py split "C:\path\to\your-dump.sql" db-drop --part-size-mb 40
git add db-drop
git commit -m "DB dump parts (temporary, do not merge)"
git push -u origin db-drop
```

(Zip the .sql first and split the .zip instead if you prefer — either works;
40 MB parts need the git CLI, 25 MB parts also fit the browser uploader.)

Then tell me it's pushed. I will `git fetch origin db-drop`, extract and
re-join the parts **without merging anything into the rewrite branch**, run
the converter here, and commit only the converted outputs. Afterwards you
can delete the `db-drop` branch on GitHub. Note that deleting a branch does
not instantly purge objects from GitHub's storage — fine for a private repo;
if that bothers you, use Option A + §2 instead.

**Tip before splitting**: check how small a plain .zip gets. If it's ≤ 95 MB
you can push that single file with the git CLI without splitting at all.

## 2. Converting the full dump (runs on your machine)

```bash
python3 tools/mssql_to_mariadb.py "C:\path\to\KN_online.sql" --database KO_GAME -o sql/mysql/ko_game
python3 tools/mssql_to_mariadb.py "C:\path\to\KO_MAIN.sql"   --database KO_MAIN -o sql/mysql/ko_main
```

The converter streams the file (240 MB is fine, UTF-16 SSMS files included)
and produces per database:

| Output | Contents |
|---|---|
| `KO_GAME_01_schema.sql` | Tables/indexes as MariaDB DDL |
| `KO_GAME_02_data.sql`, `03`, … | INSERT data, chunked every 64 MB |
| `procedures_tsql/*.sql` | Stored procs/views/triggers, original T-SQL |
| `procedures_report.md` | Every routine that needs a manual port, with signature |
| `manifest.json` | Row counts per table, conversion stats |

⚠️ Before committing converted output, sanity-check the user-data tables
(`USERDATA`, `WAREHOUSE`, `TB_USER`, `ACCOUNT_CHAR`, …): converted or not,
**real player data should stay out of a public repo**. The static game-data
tables (`ITEM`, `MAGIC`, `K_NPC`, drops, …) are safe and are what the server
loads at startup anyway. Ask me and I'll add an `--exclude-user-tables`
option tailored to your actual table names.

## 3. No manual import: how the servers use the DB

```bash
docker compose up -d          # MariaDB 11 on localhost:3306 (or use your own MariaDB)
dotnet run --project src/KnightOnline.LoginServer
```

- `docker-compose.yml` ships in the repo root (user `knight` / `changeme`,
  matching `*.sample.ini`).
- On startup each server runs **DatabaseMigrator** (`AUTO_MIGRATE=1` in the
  INI): it creates its database if missing and applies, in order, every
  `*.sql` under its migrations folder (`sql/ko_main/` for the LoginServer)
  that isn't yet recorded in the `_ko_migrations` table.
- So the workflow for schema changes is: **drop a new `000N_*.sql` file in
  the folder → restart the server → done.** HeidiSQL stays your tool for
  *browsing/editing*, never a required import step.
- Converted dumps slot straight in: name the converter output
  `0001_*.sql`, `0002_*.sql`, … inside the right folder and they will be
  applied on first boot. (Very large data files can alternatively be loaded
  once with `mariadb < file.sql` and then marked applied — I'll document the
  exact split once your converted files exist.)

## 4. What still needs a human (that's me, next milestone)

- **Stored procedures**: T-SQL bodies (`LOAD_USER_DATA`, `CREATE_NEW_CHAR`,
  `KING_ELECTION_PROC`, …) don't translate mechanically. The converter
  extracts them; I then port each one either to a MariaDB procedure or —
  where it hides game logic — into C# repository code.
- **Blob columns** (`USERDATA.strItem` etc.): format already reverse-engineered
  and implemented in `KnightOnline.Database/Items/ItemBlobs.cs`.
