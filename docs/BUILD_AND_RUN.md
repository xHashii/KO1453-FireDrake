# Building & running the C# port

Step-by-step for building the whole C# rewrite (LoginServer, GameServer stub,
AdminPanel website) and running it locally against MariaDB. Windows-oriented
(commands are PowerShell), same flow works on Linux/macOS with bash.

> **Current state:** LoginServer and the AdminPanel are real; the GameServer
> project builds but is still a stub (prints a milestone message and exits).
> A 1453 client can be pointed at the LoginServer (version check, server
> list, account login) — entering the game world comes with the GameServer
> milestones.

---

## 1. Prerequisites

| Tool | Why | Notes |
|---|---|---|
| **Git** | get the code | All C# work lives on branch **`arena/a8daa4cb-ko1453-firedrake`** — `main` only has the original C++ |
| **.NET 10 SDK** | build everything | <https://dotnet.microsoft.com/download/dotnet/10.0> — verify with `dotnet --version` → `10.x` |
| **VS 2022 as editor + dotnet CLI** (chosen) | edit + build/run | ⚠️ **Visual Studio 2022 cannot target `net10.0`** (its ceiling is net9.0, even with the .NET 10 SDK installed), so do not use VS 2022's Build/F5 for these projects. Use VS 2022 only as an editor and build/run with the `dotnet` commands below. VS 2026 Community or VS Code + C# Dev Kit also work if you later want IDE debugging |
| **Docker Desktop** | MariaDB via one command | Or your own MariaDB 11.x — then match host/port/user in the configs |
| **HeidiSQL** (optional) | browse/edit the DB | e.g. creating test accounts |

Open **`KnightOnline.Net.sln`** (the C# solution). Do **not** use
`KnightOnlineServer.sln` — that's the legacy C++ (VS2012) tree.

## 2. Get the code

```powershell
git clone https://github.com/xHashii/KO1453-FireDrake.git
cd KO1453-FireDrake
git checkout arena/a8daa4cb-ko1453-firedrake
git pull
```

(Already cloned? Just `git checkout arena/a8daa4cb-ko1453-firedrake; git pull`.)

## 3. Build everything

CLI (works everywhere):

```powershell
dotnet build KnightOnline.Net.sln -c Release
```

VS 2022 note: don't use Build Solution/F5 for this `net10.0` solution —
build and run from the CLI commands in this guide. (If you later install
VS 2026, opening `KnightOnline.Net.sln` and pressing `Ctrl+Shift+B` also
works.)

| Project | Type | What it is |
|---|---|---|
| `KnightOnline.Shared` | library | wire protocol, crypto, framing, config, sockets |
| `KnightOnline.Database` | library | MariaDB repositories + auto-migrator |
| `KnightOnline.LoginServer` | exe | port **15100** |
| `KnightOnline.GameServer` | exe | stub for now |
| `KnightOnline.AdminPanel` | Blazor web app | <http://localhost:5100> |
| `KnightOnline.Verification` (`tests/`) | exe | protocol golden-vector self-checks |

> Heads-up: this code has been syntax-checked but **never compiled on a real
> .NET SDK yet**. If the first build shows errors, copy the full error list —
> that's expected at this stage and they get fixed iteratively.

## 4. Run the protocol self-checks

```powershell
dotnet run --project tests\KnightOnline.Verification
```

Expect a list of `PASS` lines and exit code 0 (byte-for-byte comparisons
against the original C++).

## 5. Database (one command, no manual import)

Docker Desktop must be **running** first (Start menu → Docker Desktop;
wait until the tray whale icon settles). Then, from the repo root:

```powershell
docker compose up -d
docker compose ps        # wait until ko1453-mariadb shows "healthy"
```

That runs MariaDB 11.4 on **127.0.0.1:3306** with app user
**knight / changeme** (root: **rootpass**), pre-creates the database
`KN_Online`, and keeps data in the `ko1453-db` Docker volume.

**You never import SQL by hand.** On first start, LoginServer/AdminPanel
create the configured database if needed and apply
`sql/mysql/kn_online/0001_schema.sql` → `0002_data.sql` → `0003_data.sql` →
`0004_admin_tables.sql` automatically (~140k statements / 114 MB — **the
first start can take a few minutes**, later starts skip via the
`_ko_migrations` table).

Database-name rule — these three must always match (rename together if you
want a different name; nothing else cares):

- `MARIADB_DATABASE` in `docker-compose.yml`
- `DATABASE=` in `LogInServer.ini`
- `Database=...` in the AdminPanel `GameDatabase` connection string

HeidiSQL: host `127.0.0.1`, port `3306`, `knight`/`changeme`
(sees only `KN_Online`) or `root`/`rootpass` (sees everything).

Start-over-from-scratch (destroys all DB data): `docker compose down -v`,
then `docker compose up -d` again.

## 6. LoginServer

1. Create your local config from the sample (repo root):

   ```powershell
   Copy-Item src\KnightOnline.LoginServer\LogInServer.sample.ini LogInServer.ini
   ```

   Defaults already match the Docker setup (`127.0.0.1:3306`, `KN_Online`,
   `knight`/`changeme`). `LogInServer.ini` is gitignored — it's your local
   copy; don't commit real passwords. If the file is missing entirely, the
   server silently falls back to placeholder credentials
   (`username`/`password`) and DB login will fail.

2. Run it:

   ```powershell
   dotnet run --project src\KnightOnline.LoginServer
   ```

   VS 2026 alternative: right-click `KnightOnline.LoginServer` → *Set as
   Startup Project* → `F5`.

   Config and migration scripts are looked up in the **working directory
   first, then next to the executable** (the build copies `sql/kn_online/`
   beside the exe) — so both `dotnet run` from the repo root and launching
   the built exe from `bin\...\net10.0\` work.

3. It listens on **15100**, logs to console + `Logs\LoginServer.log`.
   If the DB is unreachable it exits with a clear error — or set
   `ALLOW_OFFLINE_START=1` in the INI to test the protocol without a DB
   (logins then fail with AUTH_ERROR).

4. Make a test account (pick one):
   - HeidiSQL: `INSERT INTO TB_USER (strAccountID, strPasswd) VALUES ('test','test');`
     (all other columns have defaults), or
   - set `AUTO_REGISTER=1` in `LogInServer.ini` temporarily (default is
     **off** — unknown accounts are rejected).

5. Point your 1453 client at `127.0.0.1` port **15100**. Expect version
   check / server list / account login to answer; character select & world
   entry arrive with the GameServer milestones.

## 7. AdminPanel (the website)

1. Config lives in `src\KnightOnline.AdminPanel\appsettings.json`:
   - `ConnectionStrings:GameDatabase` — defaults match Docker.
   - Panel login: `Admin:Username` / `Admin:Password`, default
     **`admin` / `changeme`** — change it, or override with the
     `KO_ADMIN_PASSWORD` environment variable.
2. Run it:

   ```powershell
   dotnet run --project src\KnightOnline.AdminPanel
   ```

   Then open **<http://localhost:5100>** (bound to localhost on purpose —
   this thing edits your database).
3. The panel auto-migrates on startup too, so panel-first works; if the DB
   is down it still starts and shows the database as offline.
4. Features today: dashboard, character search, visual inventory/warehouse
   editor, give-item-by-letter, audit log + blob backups. Safety rules:
   `docs/ADMIN_PANEL.md` (never edit an online character, etc.).

Run LoginServer + AdminPanel together in VS 2026: right-click the solution →
*Set Startup Projects…* → *Multiple startup projects* → set both to *Start*.

## 8. Runtime assets — where `.smd` maps and quest files go

Copy your server files here (keep original file names/casing):

| Files | Destination | Origin |
|---|---|---|
| `*.smd` zone maps | **`data\map\`** | original AIServer loaded `./map/<name>.smd` (`MAP_DIR`); names come from `ZONE_INFO.strZoneName` |
| `*.Lua` quest scripts | **`data\Quests\`** | original GameServer: `LUA_SCRIPT_DIRECTORY "./Quests/"` |
| `*.aievt`, `*.evt` event files | **`data\`** | AI/zone event data |

See `data/README.md`. Nothing in C# reads them yet — they're needed for
GameServer zone loading (M4+) and Lua quests (M8); having them in place now
unblocks that work (SMD parser, height/collision, warps, spawn events).

**These assets are committed directly on this branch** (owner decision
2026-10-23), so after copying the files into the folders above, push them
on `arena/a8daa4cb-ko1453-firedrake` for the GameServer work:

```powershell
git checkout arena/a8daa4cb-ko1453-firedrake
git status
git add data
git commit -m "Add server runtime assets (maps/quests/events)"
git push origin arena/a8daa4cb-ko1453-firedrake
```

Before pushing, check the largest single file: GitHub hard-blocks any
single file over 100 MB (and warns over 50 MB).

## 9. Ports at a glance

| Port | Service |
|---|---|
| 15100 | LoginServer (game client) |
| 5100 | AdminPanel (browser, localhost only) |
| 3306 | MariaDB in Docker (127.0.0.1 only) |

## 10. Troubleshooting

- **`NETSDK1045` / “net10.0 is not supported”** → you're building with
  VS 2022 or an older SDK. Use VS 2026 or the CLI; check
  `dotnet --list-sdks` shows a 10.x SDK.
- **“Could not load the version list from the configured database”** →
  DB down (`docker compose ps`), `LogInServer.ini` missing (= placeholder
  credentials), or passwords changed in one place but not the others.
- **First start looks hung** → it's applying 114 MB of migrations
  (one-time). The log prints a line per script plus progress every 25,000
  statements, so you can see it moving. Killing it mid-import is no longer
  fatal: the next start repairs the half-applied script (see below) — but let
  it finish if you can.
- **`failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine`**
  → Docker Desktop isn't running. Start it from the Start menu, wait for
  the tray whale to settle, verify with `docker ps`, then re-run
  `docker compose up -d`.
- **Docker: `ports are not available ... 127.0.0.1:3306 ... forbidden by
  its access permissions`** → Windows won't let Docker bind host port 3306.
  Find the owner with `netstat -ano | findstr :3306` (then
  `tasklist /FI "PID eq <pid>"`). If it's a local MySQL/MariaDB/XAMPP
  service, stop it and re-run `docker compose up -d`. If nothing is
  listening, check Windows reserved ranges with
  `netsh interface ipv4 show excludedportrange protocol=tcp`; if 3306 is
  inside one, remap instead: in `docker-compose.yml` change
  `"127.0.0.1:3306:3306"` to `"127.0.0.1:3307:3306"` (host port first), then
  set the same host port in `LogInServer.ini` (`PORT=3307`), the
  AdminPanel `GameDatabase` connection string (`Port=3307`), and HeidiSQL.
- **Panel opens but shows DB offline** → same DB checks; also confirm the
  database name matches in all three places (§5).
- **`Auto-migration failed` + `Table '…' already exists`** → an earlier run
  stopped halfway through a script (MariaDB DDL is not transactional, so the
  tables it had already created survive it while `_ko_migrations` still lists
  the script as pending). The migrator repairs this on the next start: it drops
  the *empty* tables that pending script creates and truncates the static data
  tables it fills, then applies it again — just start the server once more. It
  refuses (and says why) when a table involved holds rows, and it never
  truncates player tables (`TB_USER`, `USERDATA`, `KNIGHTS*`, `MAIL_BOX`, …).
  `[DATABASE] MIGRATION_REPAIR=0` turns the repair off.
- **`Auto-migration failed` + `Column length too big for column '…' (max = 255)`**
  (or `Row size too large`, `Incorrect table definition`) → a column type this
  server refuses to create; the log names the script, statement number and
  MySQL error number. Check the committed SQL with
  `python3 tools/check_migrations.py` and repair it with `--fix`: oversized
  `CHAR`/`BINARY`/`VARBINARY` become `BLOB` and over-wide `VARCHAR` becomes
  `TEXT`, which every MySQL-family server accepts in any row format. If your
  `bin/…/sql/kn_online/*.sql` copy is stale, rebuild (`dotnet build`) so the
  fixed scripts are copied next to the executable, then start again — the
  half-created tables are cleaned up automatically.
- **`Auto-migration failed` + `You have an error in your SQL syntax … (1064)`**
  → a statement the server cannot parse at all; the log names the script and
  the statement number (e.g. `statement 62/320`). Run
  `python3 tools/check_migrations.py`: it reports the offending column and
  line, and `--fix` repairs the type syntax. Then start the server again — the
  tables the aborted run created are cleaned up automatically.
  `python3 tools/test_converter.py` covers the converter rules that produce
  those statements.
- **Migration checksum error after a pull** → an already-applied script
  changed upstream. During development the cheap fix is a fresh volume:
  `docker compose down -v` → `up -d` (all test data is recreated from the
  converted seed on next start).
