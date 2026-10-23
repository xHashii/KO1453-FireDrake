# Database: getting the dump across, converting it, and never importing manually

> **Status 2026-10-23: dump received and converted.** `1453db.sql` arrived
> via `db-drop` (240 MB → 3.5 MB zip), was converted and verified statement-
> for-statement, and the sanitized output is committed under
> `sql/mysql/kn_online/`. The transfer sections below are kept as the
> runbook for future dumps. What the real schema changed (single
> `KN_Online` DB, no item-expiration columns, real login-proc semantics, …)
> is documented in **[DB_SCHEMA_NOTES.md](DB_SCHEMA_NOTES.md)** — including
> the M3 single-database rewire checklist. The `db-drop` branch can now be
> deleted (`git push origin --delete db-drop`).

## TL;DR

1. **Do not paste the ~120 MB SQL dump into chat, and do not commit the raw
   `.sql` file to the rewrite branch.** GitHub blocks files over 100 MB in a
   normal git push, and the dump contains player account credentials
   (`TB_USER`) and character data.
2. **Selected transfer route: ZIP it and push it to a temporary `db-drop`
   branch with `tools/send_dump.ps1`.** The script commits the ZIP as one
   file when it is at most 95 MB; if the ZIP is still larger than that, it
   automatically splits it into 40 MB parts with a SHA-256 manifest.
3. After you tell me the push finished, I will fetch `db-drop`, extract the
   payload outside the tracked rewrite files, and run the converter. **I will
   not merge `db-drop` into `arena/a8daa4cb-ko1453-firedrake`.** Anything
   committed back to the rewrite branch must exclude account/character rows.
4. Once the real converted migrations are ready, there is **no manual import
   step**: `docker compose up -d` starts MariaDB, and the servers create and
   upgrade their databases on boot.

---

## 1. Getting the database to me

### What does NOT work

- **Pastebin**: the maximum paste size is 512 KB on the free tier and 10 MB
  for PRO, so it cannot carry a 120 MB dump. Pastebin's acceptable-use policy
  also bans password/login data. A Pastebin **raw** link is still useful for a
  small procedure, error log, or snippet.
- **Pasting the dump into chat**: far beyond any practical context limit.
- **Git LFS / GitHub Release assets**: these can work for humans, but their
  downloads are served from hosts the agent sandbox cannot reach, and
  `git-lfs` is not installed there.
- **GitHub web upload of the whole `.sql` file**: browser uploads are capped
  at 25 MB per file, while a normal git push blocks files over 100 MB.

### Selected route: ZIP + one git push (with split fallback)

Text SQL usually compresses extremely well. The helper script does the whole
transfer: ZIP, size check, temporary branch, commit, and push.

On your PC, use a clone of this repository and make sure this rewrite branch
is checked out first so `tools/send_dump.ps1` exists:

```powershell
git clone https://github.com/xHashii/KO1453-FireDrake.git
cd KO1453-FireDrake
git fetch origin
git checkout arena/a8daa4cb-ko1453-firedrake
git pull --ff-only

.\tools\send_dump.ps1 -DumpPath "C:\path\to\your-dump.sql"
```

If you already have the clone, skip the `git clone` and `cd` lines. If
PowerShell refuses to run the script because of execution policy, launch it
for this one invocation like this:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\send_dump.ps1 -DumpPath "C:\path\to\your-dump.sql"
```

Run the script **once per exported `.sql` file**. If your 120 MB export is a
single combined file, run it once; if you exported separate database files,
run it once for each file.

What the script does:

1. Checks that the input file exists and that it is running inside the git
   repository.
2. Creates or reuses the local `db-drop` branch, based on
   `origin/arena/a8daa4cb-ko1453-firedrake`.
3. Compresses the SQL file with `Compress-Archive` into `db-drop/`.
4. If the ZIP is **95 MB or smaller**, commits and pushes that single ZIP.
5. If the ZIP is **larger than 95 MB**, calls `tools/split_dump.py` and
   commits 40 MB `.partNNN` files plus a `.manifest.txt` instead.
6. Uses `git add -f db-drop` on the temporary branch. This matters because
   `/db-drop/` is ignored on the rewrite branch, so the raw payload cannot be
   accidentally committed there by a normal `git add`.

After it finishes, tell me: **“Dump pushed to branch db-drop”**, ideally with
the original filename(s) and whether the export contains one database or
both.

### Privacy notes for the selected route

- This repository was verified as **private**. Keep it private while the dump
  is present and remember that anyone with repository access can read the
  temporary branch.
- The payload goes only to `db-drop`; it must never be merged into the rewrite
  branch.
- Do **not** delete `db-drop` until I confirm that I fetched and extracted it.
- Deleting a branch removes the normal reference to the objects, but GitHub
  may retain unreachable objects for some time. This route is acceptable here
  because the repository is private and you selected it; if that retention is
  not acceptable, use the schema-only/local-conversion alternatives below.

After I confirm receipt/conversion, you can delete the temporary remote
branch:

```powershell
git push origin --delete db-drop
```

## 2. What I do after the push

I will:

1. Fetch `origin/db-drop` without switching away from or merging into the
   rewrite branch.
2. Extract the ZIP (or re-join and hash-verify the split parts) into a
   sandbox cache outside the tracked project files.
3. Inspect encoding and database boundaries (`USE ...`, `CREATE DATABASE`,
   object prefixes) so `KO_MAIN` and `KO_GAME` are not accidentally treated
   as one database.
4. Run `tools/mssql_to_mariadb.py` and inspect `manifest.json` plus
   `procedures_report.md`.
5. Commit only reviewable, migration-safe outputs to the rewrite branch. For
   committed conversion output I will use `--exclude-user-data` (plus
   `--exclude TABLE` for anything else sensitive discovered in the real
   schema), so account/character rows are not copied into deliverable SQL.

## 3. Converting locally instead (optional)

You can also run the converter on your own machine. Separate exports per
database are easiest. For output that is safe to share/commit, exclude user
data:

```powershell
python tools\mssql_to_mariadb.py "C:\path\to\KO_MAIN.sql" --database KO_MAIN -o "$env:TEMP\ko_main_converted" --exclude-user-data
python tools\mssql_to_mariadb.py "C:\path\to\KO_GAME.sql" --database KO_GAME -o "$env:TEMP\ko_game_converted" --exclude-user-data
```

The converter streams the file, so a 120 MB input is fine, including UTF-16
SSMS exports. Per database it produces:

| Output | Contents |
|---|---|
| `<DATABASE>_01_schema.sql` | Tables/indexes as MariaDB DDL |
| `<DATABASE>_02_data.sql`, `03`, … | INSERT data, chunked every 64 MB by default |
| `procedures_tsql/*.sql` | Stored procs/views/triggers in original T-SQL |
| `procedures_report.md` | Routines needing a manual port, with signatures |
| `manifest.json` | Row counts per table and conversion statistics |

For a **private local test database that must preserve real accounts and
characters**, the converter can be run without `--exclude-user-data`, but keep
that output outside the repository and never commit the generated player-data
SQL.

If the only export is one combined file for multiple databases, do not guess
which `--database` value applies to the whole file. Send it through the
selected route above or generate separate per-database exports; the combined
file needs its database sections identified/split first.

## 4. No manual import: how the servers use the DB

```bash
docker compose up -d          # MariaDB 11 on localhost:3306 (or use your own MariaDB)
dotnet run --project src/KnightOnline.LoginServer
```

- `docker-compose.yml` ships in the repo root (user `knight` / `changeme`,
  matching the sample settings).
- Source migrations live under `sql/mysql/ko_main/` and
  `sql/mysql/ko_game/`; project files copy them beside the executables as
  `sql/ko_main/` / `sql/ko_game/`.
- On startup the servers run **DatabaseMigrator**: it creates the database if
  missing, then applies pending `*.sql` files in order and records filename +
  SHA-256 checksum in `_ko_migrations`.
- The Admin Panel also auto-migrates both databases on boot.
- Converted dump files should be reviewed, split/renamed into ordered
  migration files, and only then placed in the migration folders. From that
  point the workflow is: add `000N_*.sql`, restart the server, done. HeidiSQL
  remains a browsing/editing tool, never a required import step.

## 5. What still needs a human (that's me, next milestone)

- **Stored procedures**: T-SQL bodies (`LOAD_USER_DATA`, `CREATE_NEW_CHAR`,
  `KING_ELECTION_PROC`, …) do not translate mechanically. The converter
  extracts them; I then port each one either to a MariaDB procedure or—where
  it hides game logic—into C# repository code.
- **Blob columns** (`USERDATA.strItem` etc.): format already reverse-engineered
  and implemented in `KnightOnline.Database/Items/ItemBlobs.cs`.
- **Real-schema confirmation**: until the dump arrives, the Admin Panel editor
  targets the standard KO schema behind runtime `INFORMATION_SCHEMA`
  probing. The received schema/procs will replace those assumptions.
