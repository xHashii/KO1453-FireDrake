# KO1453-FireDrake — Knight Online v1453 server

Server for **Knight Online v1453 (Reign of the Fire Drake)**, being rewritten
from the original C++/Win32/SQL Server implementation to **C# (.NET 10)** with
a **MariaDB** database. The rewrite is wire-compatible with the unmodified
1453 client.

Full plan, milestone list and the audit of the original codebase:
**[docs/CSHARP_REWRITE_PLAN.md](docs/CSHARP_REWRITE_PLAN.md)**

## Repository layout

| Path | What |
|---|---|
| `AIServer/`, `GameServer/`, `LogInServer/`, `shared/`, `N3BASE/`, `scripting/` | Original C++ sources — the reference spec for the port (do not edit) |
| `KnightOnlineServer.sln` | Original Visual Studio 2012 C++ solution |
| `src/` | The C# rewrite (see below) |
| `KnightOnline.Net.sln` | C# solution |
| `tests/KnightOnline.Verification` | Dependency-free console harness that checks the C# wire protocol byte-for-byte against the original C |
| `sql/mysql/` | MariaDB schema/procedures (currently a draft of the account DB) |
| `docs/` | Rewrite plan and notes |

```text
src/
  KnightOnline.Shared      packets, JvCryption, LZF, CRC32, sockets, INI, logging
  KnightOnline.Database    MariaDB access (MySqlConnector), migrations, item blobs
  KnightOnline.LoginServer port of LogInServer/
  KnightOnline.GameServer  port of GameServer/ + AIServer/ (merged) - not started
  KnightOnline.AdminPanel  Blazor GM panel: dashboard, character & inventory editor
```

## Status

- ✅ **M1 — wire core**: framing, JvCryption, LZF and CRC32 ported and verified
  byte-for-byte against the original C code compiled unmodified.
- ✅ **M2 — LoginServer**: all 7 login opcodes ported (version, patch list,
  login, server list, news, encryption key exchange). _Not yet tested against
  a real client — that's the next step._
- ✅ **Admin panel v1**: dashboard, character search, visual inventory/warehouse
  editor, give-item-by-letter, audit log + backups (`src/KnightOnline.AdminPanel`).
- ⏳ Waiting on: the compressed SQL Server export (attached here by the owner),
  `map/*.smd`, `Quests/*.Lua` (+ `*.evt`), original `*.ini` configs.
- ⬜ GameServer (39k lines of C++) and the merged AI server.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
(or Visual Studio with the .NET 10 workload).

```bash
dotnet build KnightOnline.Net.sln -c Release
dotnet run --project tests/KnightOnline.Verification   # protocol self-checks
```

## Trying the LoginServer

**No manual database import.** MariaDB via one Docker command; the server
creates the database and applies `sql/mysql/ko_main/*.sql` on startup
(`AUTO_MIGRATE=1`). Full database story (incl. converting your SQL Server
dump): **[docs/DATABASE.md](docs/DATABASE.md)**.

```bash
docker compose up -d                                   # MariaDB on localhost:3306
cp src/KnightOnline.LoginServer/LogInServer.sample.ini LogInServer.ini   # then edit [DATABASE]
dotnet run --project src/KnightOnline.LoginServer
```

Point your 1453 client's login at port **15100**. Test account (optional):
uncomment the `INSERT INTO TB_USER ... ('test', 'test')` at the bottom of
`sql/mysql/ko_main/0001_account_schema_and_routines.sql` *before first run*,
or add the row in HeidiSQL afterwards. No database at all yet? Set
`ALLOW_OFFLINE_START=1` — the server runs and talks to the client, logins
just fail with AUTH_ERROR.

## Admin panel

```bash
docker compose up -d
dotnet run --project src/KnightOnline.AdminPanel    # http://localhost:5100
```

Sign in with the credentials in `src/KnightOnline.AdminPanel/appsettings.json`
(default `admin` / `changeme` — change them, or set `KO_ADMIN_PASSWORD`).
Design + safety rules: [docs/ADMIN_PANEL.md](docs/ADMIN_PANEL.md).

## Porting rules

- The C++ tree is the spec: never edit it, always cite it
  (every C# file header names the C++ it ports).
- Protocol code ships with golden-vector checks — run the verification
  harness after touching `KnightOnline.Shared`.
- Original bugs are only fixed deliberately, and each fix is commented
  (e.g. the C++ login handler wrote passwords into the user log; the port
  intentionally does not).
