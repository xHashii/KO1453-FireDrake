# Real database received: what `KN_Online` actually looks like

Status as of 2026-10-23. Source: `1453db.sql` (240.2 MB, UTF-16LE SSMS
"Generate Scripts" export), delivered via the throwaway `db-drop` branch
(3.5 MB zip), converted in the agent sandbox with
`tools/mssql_to_mariadb.py --database KN_Online --exclude-user-data`.

Converted artifacts live in **`sql/mysql/kn_online/`**:

| File | Contents |
|---|---|
| `KN_Online_01_schema.sql` (62 KB) | All 109 tables as MariaDB DDL + 208 translated column defaults |
| `KN_Online_02_data.sql` (65 MB) + `KN_Online_03_data.sql` (49 MB) | 140,559 static-data INSERTs, user data excluded (~2 MB total once git compresses them) |
| `procedures_tsql/*.sql` (77 files) | Original T-SQL of every routine, to be ported |
| `procedures_report.md`, `manifest.json` | Routine signatures + per-table row counts |

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

## The big structural finding: ONE database

There is no separate account/game DB. Everything — `TB_USER`, `USERDATA`,
`ITEM`, quests, plus Turkish website-panel tables (`_Haber`, `_Ticket`, …) —
lives in a single MSSQL database **`KN_Online`** (109 tables, 77 routines).

Our C# code currently assumes two MariaDB databases (`ko_main` accounts /
`ko_game` game) with draft schemas. That layout is superseded. **M3 rewire
checklist (not yet done):**

- [ ] One MariaDB database `KN_Online` for LoginServer, GameServer, AdminPanel.
- [ ] Migration folders: point projects at `sql/mysql/kn_online/`; retire the
      draft `sql/mysql/ko_main/0001_account_schema_and_routines.sql`; move
      `sql/mysql/ko_game/0001_admin_tables.sql` (`_ko_admin_*`) into the
      `kn_online` sequence (after `KN_Online_03_data.sql`).
- [ ] `DatabaseSettings`/sample INIs/`appsettings.json`: single connection
      string (AdminPanel's dual strings collapse into one).
- [ ] Port `ACCOUNT_LOGIN` + `ACCOUNT_PREMIUM` to MariaDB routines (below)
      and align `MariaDbAccountRepository` with the *real* semantics.

## Login/account procedures (real semantics)

The repo C++ LogInServer calls `ACCOUNT_LOGIN` / `ACCOUNT_PREMIUM`
(`LogInServer/DBProcess.cpp:89,105`) — our C# picked the right procs, but
their assumed semantics were wrong. Real bodies (`procedures_tsql/`):

- **`ACCOUNT_LOGIN(@strAccountID varchar(21), @strPasswd varchar(28))`**
  - Unknown account → **auto-creates it** (`INSERT INTO TB_USER`) → `RETURN 1`
  - Wrong password → `RETURN 3`
  - Match → `DELETE FROM CURRENTUSER` → `RETURN 1`
  - Passwords are stored/compared **in plaintext**. `HashPasswordString`
    (the classic custom KO hash) exists but is called by *nothing* in the DB
    — it belongs to the website side. Keep plaintext-compatible for existing
    accounts; any future hashing must migrate rows deliberately.
- **`ACCOUNT_PREMIUM(@strAccountID char(20))`** → `RETURN` hours of premium
  left, or `-1`. (T-SQL proc *return codes* don't exist in MariaDB — port as
  `OUT` param or single-row result and adapt the repository.)
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
