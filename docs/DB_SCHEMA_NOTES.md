# Real database received: what the source `KN_Online` actually looks like

Status as of 2026-10-23. Source: `1453db.sql` (240.2 MB, UTF-16LE SSMS
"Generate Scripts" export), delivered via the throwaway `db-drop` branch
(3.5 MB zip), converted in the agent sandbox with
`tools/mssql_to_mariadb.py --database KN_Online --exclude-user-data`.

Converted artifacts live in **`sql/mysql/kn_online/`**:

| File | Contents |
|---|---|
| `0001_schema.sql` (62 KB) | All 109 tables as MariaDB DDL + 208 translated column defaults |
| `0002_data.sql` (65 MB) + `0003_data.sql` (49 MB) | 140,559 static-data INSERTs, user data excluded (~2 MB total once git compresses them) |
| `0004_admin_tables.sql` | AdminPanel-owned `_ko_admin_backups` + `_ko_admin_audit` tables |
| `procedures_tsql/*.sql` (77 files) | Original T-SQL of every routine, to be ported |
| `procedures_report.md`, `manifest.json` | Routine signatures + per-table row counts |

The committed migration SQL intentionally contains no `CREATE DATABASE`
or `USE KN_Online`. `KN_Online` is the source MSSQL name / sample default
only; `DatabaseMigrator` creates and uses whatever MariaDB database name
the server configuration supplies, and the tables/data are imported there.

Verification (all scripted, re-runnable): per-table INSERT counts match the
source **exactly** (140,559 converted + 7,256 excluded user rows = 147,815
source statements); every INSERT is semicolon-terminated; 208/208 defaults
translated; scan for SQL Server leftovers (`[dbo]`, `IDENTITY(`, `CLUSTERED`,
`ON [PRIMARY]`, `NVARCHAR`, `GETDATE`, `CAST(0x`…) is clean.

The source DB contains real players (426 `TB_USER`, 418 `USERDATA` rows).
**None of that is in the committed output** — 19 account/character/log/clan
tables were excluded (see `manifest.json`). For a private full migration
including accounts, re-run the converter locally without
`--exclude-user-data` and keep that output out of git.

## Converter bugs the real dump exposed (fixed, commit `ae3cba6`)

1. **Statement splitting.** SSMS emits consecutive INSERT lines with *no*
   semicolons and wraps identity-table data in `SET IDENTITY_INSERT ON/OFF`
   inside one `GO` batch. Semicolon-only splitting glued whole tables into
   one invalid statement and dropped identity-table batches. Non-routine
   batches now also split on line-start DML/session keywords (string- and
   comment-aware; routine batches are still intercepted whole).
2. **DF defaults dropped.** All 208 defaults are standalone
   `ALTER TABLE … ADD CONSTRAINT DF_… DEFAULT (x) FOR col` statements, which
   the converter skipped. Without them, `NOT NULL` columns break runtime
   INSERTs (auto-register, `MAIL_BOX_SEND`) under MariaDB strict mode. They
   now become `ALTER … ALTER COLUMN … SET DEFAULT`.
3. **Exclusion list widened** from the real schema (e.g. `USER_SAVED_MAGIC`,
   `WEB_ITEMMALL_LOG`, `_Sifre_Degistir` — password-reset e-mails/hashes).

## First live MariaDB import fixes (2026-10-23)

The converted `0001_schema.sql` had never actually been executed until
the first LoginServer run against MariaDB 11.4. Two SQL Server-isms were
legal in the source but are hard errors in MariaDB — converter + committed
artifact fixed together:

1. **Keyless IDENTITY** (ERROR 1075): 9 tables (`_Anket`, `_DOWNLOADS`,
   `_IP_Engelliler`, `_Link`, `_Ticket`, `MAIL_ITEM`, `SN_NEWS`,
   `SN_NEWS_MAIN`, `TB_USER`) had IDENTITY columns with no key at all.
   MariaDB requires the auto column to be indexed; the converter now adds
   a plain non-unique `KEY ix_auto_<col>` (non-unique preserves IDENTITY
   semantics and cannot fail data loads on duplicate identity values).
2. **IDENTITY on `numeric(18,0)`** (ERROR 1063): `SERVERS.sNum`. MariaDB
   allows AUTO_INCREMENT on integer types only; scale-0 with precision
   ≤ 18 now converts to `bigint` (same value range).

Note: a failed first migration leaves partially created tables behind
(the migrator records a script only after it fully applies), so after a
schema fix, reset the dev database (`docker compose down -v`) before
retrying instead of re-running into "table already exists".

## The big structural finding: ONE database

There is no separate account/game DB. Everything — `TB_USER`, `USERDATA`,
`ITEM`, quests, plus Turkish website-panel tables (`_Haber`, `_Ticket`, …) —
lives in a single MSSQL database **`KN_Online`** (109 tables, 77 routines).

The C# rewrite now uses the same one-database shape, without hard-coding
the old ODBC-era name. `KN_Online` remains the sample default, but the
actual MariaDB name comes from `LogInServer.ini` / AdminPanel
`ConnectionStrings:GameDatabase` and should match Docker's
`MARIADB_DATABASE` when using the supplied compose file.

**M3 rewire completed 2026-10-23:**

- [x] One configurable MariaDB database for LoginServer, AdminPanel, and
      the future GameServer.
- [x] Projects point at `sql/mysql/kn_online/`; the draft account
      schema/routines were retired and `_ko_admin_*` moved to
      `0004_admin_tables.sql`.
- [x] Sample INI, AdminPanel appsettings, and Docker defaults collapsed to
      one database setting/connection string.
- [x] `ACCOUNT_LOGIN` + `ACCOUNT_PREMIUM` semantics implemented directly in
      `MariaDbAccountRepository`; auto-register is opt-in via
      `AUTO_REGISTER=1` (default off).
- [ ] Remaining M3 work is GameServer-side: static game-data caches and
      the remaining routines/repositories.

## Login/account procedures (real semantics)

The repo C++ LogInServer calls `ACCOUNT_LOGIN` / `ACCOUNT_PREMIUM`
(`LogInServer/DBProcess.cpp:89,105`). The C# repository now implements the
real bodies (`procedures_tsql/`) directly:

- **`ACCOUNT_LOGIN(@strAccountID varchar(21), @strPasswd varchar(28))`**
  - Unknown account → **auto-creates it** (`INSERT INTO TB_USER`) → `RETURN 1`
  - **Rewrite policy:** unknown accounts return `NotFound` unless
    `AUTO_REGISTER=1` is set in `[DATABASE]`; when enabled, C# inserts
    `strAccountID`/`strPasswd` and returns `Success`. After a password
    match, `strAuthority=255` returns `Banned` (the rule from
    `MAIN_LOGIN`); a successful non-banned login clears `CURRENTUSER`.
  - Wrong password → `RETURN 3`
  - Match → `DELETE FROM CURRENTUSER` → `RETURN 1`
  - Passwords are stored/compared **in plaintext**. `HashPasswordString`
    (the classic custom KO hash) exists but is called by *nothing* in the DB
    — it belongs to the website side. Keep plaintext-compatible for existing
    accounts; any future hashing must migrate rows deliberately.
- **`ACCOUNT_PREMIUM(@strAccountID char(20))`** → `RETURN` hours of premium
  left, or `-1`. C# computes the equivalent
  `TIMESTAMPDIFF(HOUR, NOW(), dtPremiumTime)` directly, so no MariaDB
  routine or T-SQL-style return code is needed.
- **`MAIN_LOGIN`** also exists (auto-register + ban check via
  `strAuthority=255`) but `EXEC`s `ACCOUNT_NATION_TRANSFER_CHECK`, which
  **does not exist in this DB** — it's broken/unused here. Ignore it for the
  LoginServer; keep the body for reference.
- **`GAME_LOGIN`** (GameServer): `-1` bad account/password, `0` no nation
  chosen yet, else `bNation`.
- **`TB_USER`** is much richer than our draft: `ID` (identity),
  `strSealPasswd char(8)` (DF default `12345678`), `bPremiumType`,
  `dtPremiumTime`, `sHours`, `CashPoint`, `strAuthority`, plus website fields
  (`Sepet`, `StrSoru`/`StrYanit`, `Nick`, `ulke`, `PremiumExpire`).

Note the DB has only **17 PRIMARY KEYs across 109 tables** and no FKs/unique
constraints — `TB_USER` itself has no PK. Uniqueness is application logic;
our repositories must not assume constraints the source never had.

## Character/item data: no expiration columns in this DB

- `USERDATA.strItem` / `strSerial` are `binary(584)` (73 slots × 8 B) as
  expected — but there is **no `strItemTime`**, and `WAREHOUSE` has **no
  `WarehouseDataTime`** (`WarehouseData`/`strSerial` `binary(1536)` ✓).
  `LOAD_USER_DATA` returns 38 fields ending at `LoyaltyMonthly`;
  `UPDATE_USER_DATA` has no `@strItemTime` parameter.
- The repo C++ *does* read/write expiration arrays (u32 unix per slot —
  292 B inventory / 768 B warehouse; the `* 8` C++ buffers are just
  over-allocated). So this DB comes from a slightly different server build
  than the repo sources. Consequences:
  - **AdminPanel editor fixed** to treat both expiration blobs as optional
    (probe-driven; expirations read 0 / aren't written when absent).
  - For the rewrite, add a compat migration later:
    `USERDATA.strItemTime binary(292)`, `WAREHOUSE.WarehouseDataTime binary(768)`
    (zero-filled = never expires = this DB's current behavior), and include
    them in the ported load/save paths.
- `LOAD_USER_DATA` side effects to preserve or deliberately drop when it
  becomes C# repository code: ownership check against `ACCOUNT_CHAR`;
  auto-promotion `Class = Class + 1` for skilled classes (105,107,…)
  at level > 59; and `EXEC` of three rank-update procs on every load.
- `USERDATA.Exp` is `int` (C++ reads i64 — fine), `HairRGB` is `tinyint`
  (C++ fetches it as the "Hair" byte), and **`strSkill` is `varchar(10)`**
  holding packed binary skill bytes — in MariaDB this column should become
  `varbinary(10)` (or be read/written as bytes with a binary-safe charset)
  or bytes >127 will be mangled by utf8mb4 conversions.
- **`ITEM` differs from the C++ recordset**: 61 columns — it has an extra
  `Droprate` and *lacks* `ItemClass`/`ItemExt` (`shared/database/ItemTableSet.h`).
  Always SELECT by column name; treat `ItemClass`/`ItemExt` as optional
  (default 0), ignore `Droprate`. The AdminPanel item search only uses
  columns that exist ✓.
- **`MAIL_BOX_SEND` arity mismatch**: C++ passes 10 args (incl. `nCoins`),
  the DB proc takes 9 (`MAIL_BOX.nCoins` exists with a DF default). Port the
  MariaDB proc *with* the `nCoins` parameter so both the game protocol path
  and the panel's give-by-letter work.

## Misc

- `VERSION` has 4 rows, `SERVERS` 1 row — the LoginServer's version/server
  list data is converted and present.
- Website content tables (`SN_NEWS`, `_Haber`, `_Link`, `_DOWNLOADS`) kept
  their rows; user-generated web data (tickets, complaints, votes, IP bans,
  password resets) was excluded.
- `BEGINNER_ITEM`/`BEGINNER_ITEM2` starter templates converted ✓ (their
  packed `Skill`/`strItem` blobs are in the data files as hex).
