#!/usr/bin/env python3
"""
check_migrations.py - static gate for the committed MariaDB migrations in
sql/mysql/<db>/. Run it after touching the converter or any *.sql artifact:

    python3 tools/check_migrations.py                 # check sql/mysql/kn_online
    python3 tools/check_migrations.py --fix           # rewrite portable types
    python3 tools/check_migrations.py --dir sql/mysql/kn_online --quick

No server needed: the checks encode the limits MariaDB itself enforces (see
mariadb_portability.py for the source references), which is how the
"Column length too big for column 'strItem' (max = 255)" class of failure -
discovered only when the LoginServer first talked to a live database - stays
discovered *before* the artifact is committed.

Checks
  schema (0001_*.sql and friends)
    1. column types a MySQL-family server refuses to create (CHAR/BINARY > 255,
       VARCHAR/VARBINARY > 65535 bytes, CHAR > 1024 bytes, character columns
       too wide to fit InnoDB's inline row budget, binary payloads declared as
       varchar)                      -> error, and --fix rewrites them
    2. row size over MariaDB's 65535-byte per-row limit      -> error
    3. AUTO_INCREMENT column without a key (ERROR 1075)      -> error
    4. DEFAULT on a BLOB/TEXT column (ERROR 1101)            -> error
    5. CREATE DATABASE / USE (the migrator picks the database)-> error
  data (000N_data.sql)
    6. INSERT whose value count != column count, or whose columns are not in
       the table                                          -> error
    7. string literals carrying raw control bytes (binary smuggled through a
       character column by the SSMS text export)           -> warning
    8. per-table INSERT counts vs manifest.json             -> error
  all files
    9. statements are splittable: balanced quotes, every statement terminated,
       no leftover SQL Server syntax ([dbo], IDENTITY(, NVARCHAR, GETDATE,
       CLUSTERED, ON [PRIMARY])                            -> error

Exit code 0 = clean (warnings allowed), 1 = something would break an import.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import mariadb_portability as portability  # noqa: E402  (sibling module)

MSSQL_LEFTOVERS = [
    r"\[dbo\]", r"\bIDENTITY\s*\(", r"\bNVARCHAR\b", r"\bGETDATE\b",
    r"\bCLUSTERED\b", r"ON\s+\[PRIMARY\]", r"(?<![\w'])N'", r"\bdatetime2\b",
    r"\buniqueidentifier\b", r"\bsmalldatetime\b",
]

_CONTROL_CHARS = frozenset(chr(c) for c in range(0x20) if c not in (0x09, 0x0A, 0x0D)) | {"\x7f"}

_INT_TYPES = {"tinyint": 1, "smallint": 2, "mediumint": 3, "int": 4, "integer": 4, "bigint": 8}


class Report:
    def __init__(self) -> None:
        self.errors: list[str] = []
        self.warnings: list[str] = []
        self.fixes: list[str] = []
        self.notes: list[str] = []

    def error(self, msg: str) -> None:
        self.errors.append(msg)

    def warn(self, msg: str) -> None:
        self.warnings.append(msg)


# --------------------------------------------------------------------------
# Streaming statement splitter (mirrors KnightOnline.Database's
# SqlScriptSplitter: '...' / "..." / `...` quoting, -- and # line comments,
# /* */ block comments, backslash escapes).
# --------------------------------------------------------------------------
def iter_statements(text: str):
    """Yield (start_offset, statement_without_delimiter)."""
    i, start, n = 0, 0, len(text)
    in_line = in_block = False
    quote = ""
    while i < n:
        ch = text[i]
        nxt = text[i + 1:i + 2]
        if in_line:
            if ch == "\n":
                in_line = False
            i += 1
            continue
        if in_block:
            if ch == "*" and nxt == "/":
                in_block = False
                i += 2
                continue
            i += 1
            continue
        if quote:
            if ch == "\\" and quote != "`":
                i += 2
                continue
            if ch == quote:
                if text[i + 1:i + 2] == quote:
                    i += 2
                    continue
                quote = ""
            i += 1
            continue
        if ch in ("'", '"', "`"):
            quote = ch
            i += 1
            continue
        if ch == "-" and nxt == "-":
            in_line = True
            i += 2
            continue
        if ch == "#":
            in_line = True
            i += 1
            continue
        if ch == "/" and nxt == "*":
            in_block = True
            i += 2
            continue
        if ch == ";":
            stmt = text[start:i]
            if stmt.strip():
                yield start, stmt
            i += 1
            start = i
            continue
        i += 1
    tail = text[start:]
    if quote or in_block:
        yield start, None  # type: ignore[misc]  # unbalanced -> caller reports
    elif tail.strip():
        yield start, tail


def split_values(values: str) -> list[str]:
    """Split one INSERT (...) tuple on top-level commas."""
    out, cur, i, n = [], [], 0, len(values)
    quote = ""
    depth = 0
    while i < n:
        ch = values[i]
        if quote:
            cur.append(ch)
            if ch == "\\":
                if i + 1 < n:
                    cur.append(values[i + 1])
                i += 2
                continue
            if ch == quote:
                if values[i + 1:i + 2] == quote:
                    cur.append(values[i + 1])
                    i += 2
                    continue
                quote = ""
            i += 1
            continue
        if ch in ("'", '"', "`"):
            quote = ch
            cur.append(ch)
            i += 1
            continue
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
        if ch == "," and depth == 0:
            out.append("".join(cur))
            cur = []
            i += 1
            continue
        cur.append(ch)
        i += 1
    if "".join(cur).strip():
        out.append("".join(cur))
    return out


INSERT_RE = re.compile(
    r"(?is)^\s*INSERT\s+(?:IGNORE\s+)?(?:INTO\s+)?(?:`[^`]+`\s*\.\s*)?`(?P<table>[^`]+)`\s*"
    r"\((?P<columns>[^)]*)\)\s*VALUES\s*\((?P<values>.*)\)\s*$")


# --------------------------------------------------------------------------
# Schema checks
# --------------------------------------------------------------------------
CREATE_TABLE_BODY_RE = re.compile(
    r"(?is)CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?`(?P<name>[^`]+)`\s*\((?P<body>.*?)\n\)\s*ENGINE")

# Column definitions, quoted (`col`) or bare (col) - a bare one is reported as a
# warning because it only works while the name is not a reserved word.
COLUMN_LINE_RE = re.compile(
    r"(?im)^[ \t]*(?P<q>`?)(?P<col>[A-Za-z_][A-Za-z0-9_$]*)(?P=q)[ \t]+"
    r"(?P<type>[A-Za-z]+(?:[ \t]+UNSIGNED)?(?:[ \t]*\([ \t]*[\d,]+[ \t]*\))?)")


def parse_schema_tables(sql: str) -> dict[str, dict[str, str]]:
    """table -> {lowercase column: declared type text} for every CREATE TABLE."""
    tables: dict[str, dict[str, str]] = {}
    for m in CREATE_TABLE_BODY_RE.finditer(sql):
        cols: dict[str, str] = {}
        for cm in COLUMN_LINE_RE.finditer(m.group("body")):
            name = cm.group("col")
            if name.lower() in portability._CONSTRAINT_WORDS:
                continue  # PRIMARY KEY / KEY / UNIQUE / ... lines
            cols[name.lower()] = cm.group("type").strip()
        tables[m.group("name")] = cols
    return tables


def check_schema_file(path: str, sql: str, rep: Report) -> dict[str, dict[str, str]]:
    tables = parse_schema_tables(sql)

    # 5. no CREATE DATABASE / USE - the migrator connects to the configured DB.
    for m in re.finditer(r"(?im)^\s*(CREATE\s+DATABASE|USE\s+`?\w+)", sql):
        rep.error(f"{path}: migration scripts must not contain '{m.group(0).strip()}' "
                  f"(DatabaseMigrator chooses the database from server config)")

    # 1. portable column types.
    _, changes = portability.make_schema_portable(sql)
    for table, cols in changes.items():
        for col, old, new in cols:
            rep.error(f"{path}: {table}.{col} {old} -> {new}")

    # 1b. identifiers that only work because the name is not reserved.
    for m in CREATE_TABLE_BODY_RE.finditer(sql):
        for cm in COLUMN_LINE_RE.finditer(m.group("body")):
            if cm.group("q"):
                continue
            name = cm.group("col")
            if name.lower() in portability._CONSTRAINT_WORDS:
                continue
            rep.warn(f"{path}: {m.group('name')}.{name} is not backquoted "
                     f"(works only while the name stays unreserved; --fix quotes it)")

    for table, body in re.findall(r"(?is)CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?`([^`]+)`\s*\((.*?)\n\)\s*ENGINE", sql):
        cols = tables.get(table, {})

        # 2. row size (VARCHAR/CHAR count fully in utf8mb4, LOBs ~12 bytes).
        row_bytes = 0
        for col, decl in cols.items():
            base = decl.lower().split("(")[0].strip()
            m = re.search(r"\((\d+)", decl)
            n = int(m.group(1)) if m else 0
            if base in ("varchar", "char", "nvarchar", "nchar"):
                row_bytes += n * portability.CHARSET_MAXLEN + 2
            elif base in ("varbinary", "binary"):
                row_bytes += n + 2
            elif base.endswith("text") or base.endswith("blob"):
                row_bytes += 12
            elif base in _INT_TYPES:
                row_bytes += _INT_TYPES[base]
            else:
                row_bytes += 8
        if row_bytes > portability.MAX_VARCHAR_BYTES:
            rep.error(f"{path}: {table} worst-case row is {row_bytes} bytes "
                      f"(MariaDB limit {portability.MAX_VARCHAR_BYTES})")

        # 3. AUTO_INCREMENT must be indexed (ERROR 1075).
        auto = [c for c, d in cols.items() if "auto_increment" in d.lower()]
        if auto:
            keyed = set()
            for km in re.finditer(r"(?im)(PRIMARY\s+KEY|UNIQUE\s+KEY|KEY|INDEX)[^(]*\(([^)]*)\)", body):
                for part in km.group(2).split(","):
                    keyed.add(part.strip().strip("`").split("(")[0].strip().lower())
            for col in auto:
                if col not in keyed:
                    rep.error(f"{path}: {table}.{col} is AUTO_INCREMENT but not indexed "
                              f"(ERROR 1075: incorrect table definition)")

        # 4. BLOB/TEXT cannot carry a DEFAULT (ERROR 1101).
        for col, decl in cols.items():
            base = decl.lower().split("(")[0].strip()
            if (base.endswith("text") or base.endswith("blob")) and re.search(
                    r"(?im)^\s*`" + re.escape(col) + r"`[^,\n]*\bDEFAULT\b", body):
                rep.error(f"{path}: {table}.{col} is {base.upper()} and has a DEFAULT "
                          f"(ERROR 1101)")

    # 9. SQL Server leftovers.
    for pat in MSSQL_LEFTOVERS:
        for m in re.finditer(pat, sql):
            line = sql.count("\n", 0, m.start()) + 1
            rep.error(f"{path}:{line}: leftover SQL Server syntax {m.group(0)!r}")
            break  # one report per pattern is enough

    return tables


def check_data_file(path: str, sql: str, tables: dict[str, dict[str, str]],
                    rep: Report, quick: bool) -> dict[str, int]:
    """Returns per-table INSERT counts."""
    counts: dict[str, int] = {}
    binary_literals: dict[str, int] = {}
    statements = 0
    unterminated = 0

    for offset, stmt in iter_statements(sql):
        if stmt is None:
            unterminated += 1
            continue
        statements += 1
        head = stmt.lstrip()
        if not head.upper().startswith("INSERT"):
            for pat in MSSQL_LEFTOVERS:
                if re.search(pat, head):
                    line = sql.count("\n", 0, offset) + 1
                    rep.error(f"{path}:{line}: leftover SQL Server syntax in {head[:60]!r}")
                    break
            continue

        m = INSERT_RE.match(head.strip())
        if not m:
            line = sql.count("\n", 0, offset) + 1
            rep.error(f"{path}:{line}: unparseable INSERT: {head.strip()[:100]!r}")
            continue

        table = m.group("table")
        counts[table] = counts.get(table, 0) + 1
        columns = [c.strip().strip("`").lower() for c in m.group("columns").split(",")]

        known = tables.get(table)
        if known is not None:
            missing = [c for c in columns if c not in known]
            if missing:
                line = sql.count("\n", 0, offset) + 1
                rep.error(f"{path}:{line}: INSERT into {table} names unknown column(s) {missing}")

        values = split_values(m.group("values"))
        if len(values) != len(columns):
            line = sql.count("\n", 0, offset) + 1
            rep.error(f"{path}:{line}: INSERT into {table} has {len(values)} value(s) "
                      f"for {len(columns)} column(s)")

        if quick:
            continue

        # 7. binary smuggled through a character column.
        for value, col in zip(values, columns):
            v = value.strip()
            if not v.startswith("'"):
                continue
            inner = v[1:v.rfind("'")] if v.endswith("'") else v[1:]
            if any(ch in _CONTROL_CHARS for ch in inner) or "\ufffd" in inner:
                key = f"{table}.{col}"
                binary_literals[key] = binary_literals.get(key, 0) + 1

    if unterminated:
        rep.error(f"{path}: {unterminated} statement(s) with unbalanced quotes/comments "
                  f"- the migrator's splitter would mis-parse them")

    for key, cnt in sorted(binary_literals.items()):
        rep.warn(f"{path}: {key} has {cnt} value(s) whose bytes the SSMS text export "
                 f"already mangled (control characters inside a quoted literal); a fresh "
                 f"conversion with --binary-literals hex preserves them")
    return counts


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dir", default="sql/mysql/kn_online",
                    help="migration folder to check (default: sql/mysql/kn_online)")
    ap.add_argument("--fix", action="store_true",
                    help="rewrite non-portable column types in the schema file(s)")
    ap.add_argument("--quick", action="store_true",
                    help="skip the per-literal binary scan of the big data files")
    ap.add_argument("--no-manifest", action="store_true",
                    help="skip the manifest.json row-count cross-check")
    args = ap.parse_args()

    if not os.path.isdir(args.dir):
        print(f"[check] no such folder: {args.dir}", file=sys.stderr)
        return 2

    rep = Report()
    files = sorted(f for f in os.listdir(args.dir) if f.endswith(".sql"))
    if not files:
        print(f"[check] no .sql files in {args.dir}", file=sys.stderr)
        return 2

    schema_files = [f for f in files if "schema" in f or "tables" in f]
    data_files = [f for f in files if f not in schema_files]

    tables: dict[str, dict[str, str]] = {}
    for name in schema_files:
        path = os.path.join(args.dir, name)
        sql = open(path, encoding="utf-8").read()

        if args.fix:
            # Repair first, then check what is actually on disk, so the report
            # never mixes pre-fix findings with the fixed file.
            fixed, changes = portability.make_schema_portable(sql)
            fixed, quoted = portability.quote_bare_column_names(fixed)
            if fixed != sql:
                open(path, "w", encoding="utf-8", newline="\n").write(fixed)
                rep.fixes.append(f"{path}: rewrote {sum(len(v) for v in changes.values())} "
                                 f"column type(s), quoted {len(quoted)} identifier(s)")
                sql = fixed

        tables.update(check_schema_file(path, sql, rep))

    counts: dict[str, int] = {}
    for name in data_files:
        path = os.path.join(args.dir, name)
        sql = open(path, encoding="utf-8").read()
        for table, n in check_data_file(path, sql, tables, rep, args.quick).items():
            counts[table] = counts.get(table, 0) + n

    manifest_path = os.path.join(args.dir, "manifest.json")
    if not args.no_manifest and os.path.exists(manifest_path):
        manifest = json.load(open(manifest_path, encoding="utf-8"))
        expected = manifest.get("tables_with_rows", {})
        for table, want in expected.items():
            got = counts.get(table, 0)
            if got != want:
                rep.error(f"manifest.json says {table} has {want} INSERT(s) but the data "
                          f"files contain {got}")
        for table, got in counts.items():
            if table not in expected:
                rep.error(f"{table} has {got} INSERT(s) but is not listed in manifest.json")

    print(f"[check] {args.dir}: {len(files)} file(s), {len(tables)} table(s), "
          f"{sum(counts.values())} INSERT(s)")
    for msg in rep.fixes:
        print(f"  FIXED   {msg}")
    for msg in rep.warnings:
        print(f"  WARN    {msg}")
    for msg in rep.errors[:60]:
        print(f"  ERROR   {msg}")
    if len(rep.errors) > 60:
        print(f"  ... and {len(rep.errors) - 60} more error(s)")

    if rep.errors and not args.fix:
        print(f"[check] FAIL: {len(rep.errors)} error(s), {len(rep.warnings)} warning(s). "
              f"Type errors are repairable with --fix.")
        return 1
    if rep.errors:
        print(f"[check] FAIL: {len(rep.errors)} error(s) remain after --fix.")
        return 1
    print(f"[check] OK ({len(rep.warnings)} warning(s))")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
