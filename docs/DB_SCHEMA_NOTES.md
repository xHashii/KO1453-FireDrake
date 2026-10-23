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
3. **Fixed-length types over 255** (ERROR 1074): MariaDB caps `CHAR` and
   `BINARY` at 255; SQL Server allows 8000. 17 columns in 13 tables were
   oversized — the item blobs (`USERDATA.strItem/strSerial` 584,
   `strQuest` 600, `WAREHOUSE.*` 1536, `BEGINNER_ITEM.*`) and
   `char(1000)` description/talk columns. Oversized `binary`/`char` were
   widened to `varbinary`/`varchar` — which turned out to be only half the
   fix, see the next section.

## Second live-import round (2026-10-23): portable types, self-healing migrator

The LoginServer's next run against a live database died with:

```
MySqlException: Column length too big for column 'strItem' (max = 255);
                use BLOB or TEXT instead
```

`max = 255` is the fingerprint of MariaDB's `Type_handler_string`, which ends
in `check_length(ER_TOO_BIG_FIELDLENGTH, MAX_FIELD_CHARLENGTH)`
(`sql/sql_type.cc`): **`CHAR`/`BINARY` are capped at 255 characters**, so a
`binary(600)` column is uncreateable on *every* MySQL-family server, while
`VARBINARY`/`VARCHAR` are capped at 65535 **bytes**
(`Column_definition::prepare_blob_field`, `sql/sql_table.cc`). Two things
followed.

**1. Types are now chosen to be portable, not merely legal on one server.**
`tools/mariadb_portability.py` holds the rules, used by the converter, by
`tools/check_migrations.py --fix` and documented there with their source
references:

| Source type | Becomes | Why |
|---|---|---|
| `binary(N>255)` | `blob`/`mediumblob` | ERROR 1074 on every server |
| `varbinary(N>255)` | `blob`/`mediumblob` | legal on modern servers, but only in row formats that store long columns off-page; BLOB works in every row format *and* engine, and is what `0004_admin_tables.sql` already used for the same blobs |
| `char(N>255)` | `varchar(N)` (or `text`) | ERROR 1074 |
| `varchar(N)` with `N*4 > 65535` | `text`/`mediumtext` | VARCHAR byte limit |
| `varchar(N)` with `N*4 > 8126` | `text`/`mediumtext` | InnoDB's inline row budget: `SN_NEWS` alone declared 50 KB of `varchar(4096)` |
| `USERDATA.strSkill`, `USERDATA_SKILLSHORTCUT.strSkillData`, `USER_ITEMS.nItemSerial` | `varbinary`/`blob` | packed bytes declared as `varchar` in the source (`EDIT_SKILLS` does `CAST(strSkill AS varbinary(10))`); utf8mb4 mangles every byte > 0x7F. This closes the TODO noted further down. |

Columns that already fit are left byte-for-byte alone, so no index, `DEFAULT`
or comparison changed. Applied to the committed artifact: 20 columns in 11
tables (`BEGINNER_ITEM`, `BEGINNER_ITEM2`, `USERDATA`, `WAREHOUSE`,
`USER_ITEMS`, `USERDATA_SKILLSHORTCUT`, `KING_CANDIDACY_NOTICE_BOARD`,
`SN_NEWS`, `SN_NEWS_MAIN`, `_Haber`, `_Aktivite`).

**2. `SN_NEWS.date` was not quoted.** SSMS brackets every identifier, and the
converter treated any bracketed *type keyword* as a type, so the column named
`date` came out bare. It only worked because `DATE` happens not to be reserved
in MariaDB, and it was invisible to identifier-based tooling (the new checker
first reported those 5 `SN_NEWS` INSERTs as naming an unknown column).
`bracket_to_backtick` now unbrackets a keyword only in type position —
`[varchar](20)`, `[col] [smallint]`, `CAST(x AS [int])` — and quotes it
everywhere else.

**3. A failed migration is now recoverable.** This is what actually stranded
the run above: MariaDB DDL is not transactional, so the failing `CREATE TABLE`
left ~30 tables behind while `_ko_migrations` still said "not applied", and
every retry then died on `Table '_Aktivite' already exists`. The old advice was
`docker compose down -v`. `DatabaseMigrator` now repairs a pending script
before applying it:

* tables the script creates are dropped **only if they are empty**;
* static data tables the script fills are truncated, so a retried data script
  cannot double 140k rows;
* player tables (`TB_USER`, `USERDATA`, `KNIGHTS*`, `MAIL_BOX`, …) are never
  touched — if one of them holds rows while its script is still pending, the
  migrator reports that and stops instead of guessing.

Startup failures also say which script and statement died, the MySQL error
number, and what to do (`HintFor`). Progress is logged every 25k statements so
the multi-minute data import does not look like a hang. `MIGRATION_REPAIR=0` in
`[DATABASE]` turns the repair off.

**4. `tools/check_migrations.py` gates all of it without a server.** It encodes
the limits above plus row size, keyless `AUTO_INCREMENT`, `DEFAULT` on
BLOB/TEXT, `CREATE DATABASE`/`USE` in a migration, INSERT arity/unknown
columns, column types MariaDB cannot parse at all, and per-table row counts
against `manifest.json`:

```bash
python3 tools/check_migrations.py           # sql/mysql/kn_online
python3 tools/check_migrations.py --fix     # rewrite non-portable types
python3 tools/test_converter.py             # regression tests for the tooling
pip install sqlglot                         # optional: parse the DDL for real
```

The regex checks are a stand-in for a parser, so with `sqlglot` installed the
gate additionally parses every DDL statement with a real MySQL grammar
(`--no-parse` skips it; the tool stays stdlib-only without it). Both layers
independently reject the `TINYINT UNSIGNED(1)` column described next — the
parser reports `Expecting ). Line 16`, the same statement and line MariaDB did.

Current state: **0 errors**, 140,559 INSERTs verified, 319 DDL statements
deep-parsed, and one warning that is a source-dump limitation rather than a bug
— see the note on `BEGINNER_ITEM2.strItem` at the end of this document.

**5. A type rule's own output was re-matched by the next rule.** Four columns
(`MAIL_BOX.bDeleted`, `PUS_ITEMLERI.ExtraItem`/`ItemVer`/`KomutCalistir`)
shipped as `TINYINT UNSIGNED(1)`. MariaDB reads the display width *before*
`UNSIGNED`, so `CREATE TABLE MAIL_BOX` died with ERROR 1064 at statement 62 of
320 and nothing after it existed. `convert_types` used to apply `TYPE_RULES`
one after another over the whole statement: `[bit]` → `TINYINT(1)` was then
picked up again by the `tinyint` rule, which inserted `UNSIGNED` in front of
the width it had just inherited. The rules are now one combined pass, so each
source token is replaced exactly once and no replacement can be rewritten.

Three guard rails went in with the fix: the checker rejects any declared type
that is not a spelling MariaDB parses (and `--fix` moves a misplaced
`UNSIGNED`/`ZEROFILL` behind the width), `make_column_types_portable`
normalises the same thing on the converter side, and `tools/test_converter.py`
pins all 30 source-type mappings plus this cascade specifically.

**6. `CAST` has a much smaller vocabulary than `CREATE TABLE`.** The next
failure was in the *data*: SSMS wraps every `numeric`/`decimal` value it
exports in a cast, so the single `SERVERS` row arrived as

```sql
INSERT `SERVERS` (...) VALUES (CAST(1 AS Numeric(18, 0)), 'Heros', '127.0.0.1', 'Heros');
```

`NUMERIC` is a perfectly good column type and is **not** one of the types
MariaDB accepts inside `CAST()` — `sql/sql_yacc.yy` lists `cast_type` as
`BINARY(n)`, `CHAR(n)`, `VARCHAR(n)`, `NCHAR(n)`, `INT`/`SIGNED`/`UNSIGNED`,
`DECIMAL(M,D)`, `FLOAT`, `DOUBLE(p)`, `DATE`, `TIME(fsp)`, `DATETIME(fsp)` and
nothing else. ERROR 1064 at statement 69998 of 70633 — and since the `VERSION`
rows sit 600 statements later, the login server came up with no version list at
all. `convert_dml` now unwraps a cast around a plain literal (the destination
column type does exactly the same job) and rewrites the target type of any cast
that still has an expression inside it: `Numeric(18,2)` → `DECIMAL(18,2)`,
`money` → `DECIMAL(19,4)`, `nvarchar(50)` → `CHAR(50)`, `varbinary(12)` →
`BINARY(12)`, `bigint` → `SIGNED`, `datetime2(3)` → `DATETIME`. Text inside a
string literal is never touched, which matters here: NPC dialogue in this dump
contains the words `money`, `CAST` and square brackets. The checker rejects any
cast type outside that grammar, in DDL and DML alike.

**7. Both gates now run in CI.** `.github/workflows/ci.yml` builds the solution,
runs the golden-vector verifier, runs `tools/test_converter.py` and checks the
committed migration scripts (with `sqlglot` installed, so the DDL is really
parsed) on every push and pull request. Findings 5 and 6 both reached a running
LoginServer before they were caught locally; neither can do that again.

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
  holding packed binary skill bytes — in MariaDB this column must be
  `varbinary(10)` (or be read/written as bytes with a binary-safe charset)
  or bytes >127 are mangled by utf8mb4 conversions. **Done:** the converter
  now maps it (and `strSkillData`, `USER_ITEMS.nItemSerial`) to a binary type;
  see the portability table above.
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
- `BEGINNER_ITEM`/`BEGINNER_ITEM2` starter templates converted ✓ — but note
  what *is* and *is not* byte-exact in the committed data files:
  - `BEGINNER_ITEM.strItem`/`WarehouseData` and `BEGINNER_ITEM2.strItem1` were
    `binary`/`varbinary` in the source, so SSMS scripted them as `0x…` and they
    are exact ✓ (`BEGINNER_ITEM` is the table `CREATE_NEW_CHAR` reads).
  - `BEGINNER_ITEM2.strItem` and `S_ITEM.strItem` were declared
    `varchar(1200)` in the source while holding the same packed bytes, so SSMS
    scripted them as quoted text through a UTF-16 file: **those 8 values are
    already mangled in `1453db.sql` itself** and no conversion can restore
    them. They stay `varchar` in the schema (faithful to the source) and import
    fine; `tools/check_migrations.py` reports them as the one open warning.
    Both tables are legacy 1290-era starter templates that no C# code reads —
    the live path is `BEGINNER_ITEM`.
  - A fresh conversion of a dump whose bytes did survive emits them as
    charset-independent hex automatically (`convert_binary_literals`,
    `--binary-literals hex|null`), which is what recovered `0x0A12F67E01` from
    a `'\n\x12ö~\x01'` literal in testing.
