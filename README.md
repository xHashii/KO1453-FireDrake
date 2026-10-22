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
  KnightOnline.Database    MariaDB access (MySqlConnector)
  KnightOnline.LoginServer port of LogInServer/
  KnightOnline.GameServer  port of GameServer/ + AIServer/ (merged) - not started
```

## Status

- ✅ **M1 — wire core**: framing, JvCryption, LZF and CRC32 ported and verified
  byte-for-byte against the original C code compiled unmodified.
- ✅ **M2 — LoginServer**: all 7 login opcodes ported (version, patch list,
  login, server list, news, encryption key exchange). _Not yet tested against
  a real client — that's the next step._
- ⏳ Waiting on: SQL Server → MariaDB database export (`.sql`, both DBs),
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

1. Create the account DB: run `sql/mysql/ko_main_draft.sql` in HeidiSQL
   (or `mariadb < sql/mysql/ko_main_draft.sql`), and uncomment the test
   account insert at the bottom if you want `test/test`.
2. Copy `src/KnightOnline.LoginServer/LogInServer.sample.ini` to
   `LogInServer.ini` next to the built executable and fill in `[DATABASE]`.
   (The rest of the file matches the original C++ `LogInServer.ini`.)
   No MariaDB yet? Set `ALLOW_OFFLINE_START=1` — the server starts and talks
   to the client, logins just fail with AUTH_ERROR.
3. Point your 1453 client's login at this machine, port **15100**.

```bash
dotnet run --project src/KnightOnline.LoginServer
```

## Porting rules

- The C++ tree is the spec: never edit it, always cite it
  (every C# file header names the C++ it ports).
- Protocol code ships with golden-vector checks — run the verification
  harness after touching `KnightOnline.Shared`.
- Original bugs are only fixed deliberately, and each fix is commented
  (e.g. the C++ login handler wrote passwords into the user log; the port
  intentionally does not).
