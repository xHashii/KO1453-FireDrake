#!/usr/bin/env python3
"""
mssql_to_mariadb.py - Convert an SSMS "Generate Scripts" SQL Server dump of a
Knight Online database (KO_MAIN / KO_GAME) into MariaDB-compatible SQL.

WHY THIS RUNS ON YOUR MACHINE: a full KO dump contains player account
passwords (TB_USER) and is far too large for git. Run the converter locally,
then commit only its output: schema + static game data (see docs/DATABASE.md).

What it does (streaming, so 240 MB+ files are fine):
  * CREATE TABLE      -> MariaDB DDL (types, IDENTITY->AUTO_INCREMENT,
                         [brackets]->`backticks`, index options stripped)
  * INSERT / other DML-> translated into the *_NN_data.sql file(s)
  * PRIMARY KEY / INDEX / FOREIGN KEY (basic forms)
  * CREATE PROC/FUNCTION/TRIGGER/VIEW -> NOT auto-translated (T-SQL logic
                         needs a human port); the original text is extracted
                         to procedures_tsql/<NAME>.sql and listed in
                         procedures_report.md with its parameter signature.
  * Produces manifest.json with per-table row counts.

Usage:
  python3 mssql_to_mariadb.py 1453db.sql --database KN_Online -o out/KN_Online

(--database only names the output files/manifest; the SQL itself contains
no CREATE DATABASE/USE, so it imports into whatever database your server
config points at.)

Then either let the servers import automatically (DatabaseMigrator) or pick
the target database yourself (output is name-agnostic on purpose):
  mariadb -u root -p YOUR_DB_NAME < out/KN_Online/KN_Online_01_schema.sql
  mariadb -u root -p YOUR_DB_NAME < out/KN_Online/KN_Online_02_data.sql
"""
import argparse
import json
import os
import re
import sys

# --------------------------------------------------------------------------
# Type mapping (applied inside CREATE TABLE bodies only)
# --------------------------------------------------------------------------
TYPE_RULES = [
    # order matters: longer / more specific patterns first
    (r"\bnvarchar\s*\(\s*max\s*\)", "TEXT"),
    (r"\bvarchar\s*\(\s*max\s*\)", "TEXT"),
    (r"\bvarbinary\s*\(\s*max\s*\)", "MEDIUMBLOB"),
    (r"\bbinary\s*\(\s*max\s*\)", "MEDIUMBLOB"),
    (r"\bnvarchar\s*\(\s*(\d+)\s*\)", r"VARCHAR(\1)"),
    (r"\bnchar\s*\(\s*(\d+)\s*\)", r"CHAR(\1)"),
    (r"\bntext\b", "TEXT"),
    (r"\bimage\b", "MEDIUMBLOB"),
    (r"\bdatetime2\s*\(\s*\d+\s*\)", "DATETIME(6)"),
    (r"\bdatetimeoffset\b(\s*\(\s*\d+\s*\))?", "DATETIME"),
    (r"\bdatetime2\b", "DATETIME"),
    (r"\bsmalldatetime\b", "DATETIME"),
    (r"\bsmallmoney\b", "DECIMAL(10,4)"),
    (r"\bmoney\b", "DECIMAL(19,4)"),
    (r"\buniqueidentifier\b", "CHAR(36)"),
    (r"\bbit\b", "TINYINT(1)"),
    (r"\btinyint\b", "TINYINT UNSIGNED"),  # SQL Server tinyint is 0..255
    (r"\breal\b", "FLOAT"),
    (r"\bfloat\s*\(\s*53\s*\)", "DOUBLE"),
    (r"\bsql_variant\b", "TEXT"),
    (r"\bxml\b", "TEXT"),
    (r"\bgeography\b|\bgeometry\b", "TEXT"),
]

COMPILED_TYPES = [(re.compile(p, re.IGNORECASE), r) for p, r in TYPE_RULES]

DROP_STATEMENT_PREFIXES = (
    "SET ANSI_NULLS", "SET QUOTED_IDENTIFIER", "SET ANSI_PADDING",
    "SET IDENTITY_INSERT", "SET ARITHABORT", "SET CONCAT_NULL_YIELDS_NULL",
    "SET NUMERIC_ROUNDABORT", "SET XACT_ABORT", "SET ANSI_WARNINGS",
    "SET NOCOUNT", "SET TEXTSIZE", "SET DATEFORMAT", "SET LANGUAGE",
    "SET LOCK_TIMEOUT", "USE ", "USE[", "EXEC SYS.SP_", "EXEC SP_",
    "IF EXISTS", "IF NOT EXISTS", "PRINT ", "ALTER DATABASE",
    "CREATE DATABASE", "DENY ", "REVOKE ",
)

# Bracketed type names ([varchar](20)) must stay bare keywords, not `varchar`.
TYPE_KEYWORDS = {
    "bigint", "binary", "bit", "char", "date", "datetime", "datetime2",
    "datetimeoffset", "decimal", "float", "geography", "geometry", "image",
    "int", "money", "nchar", "ntext", "numeric", "nvarchar", "real",
    "smalldatetime", "smallint", "smallmoney", "sql_variant", "text", "time",
    "tinyint", "uniqueidentifier", "varbinary", "varchar", "xml",
}


def bracket_to_backtick(sql: str) -> str:
    """[dbo].[Table] -> `Table`; [Col] -> `Col` (outside string literals)."""
    out = []
    in_str = False
    i = 0
    while i < len(sql):
        ch = sql[i]
        if in_str:
            out.append(ch)
            if ch == "'":
                if sql[i + 1:i + 2] == "'":
                    out.append("'")
                    i += 2
                    continue
                in_str = False
            i += 1
            continue
        if ch == "'":
            in_str = True
            out.append(ch)
            i += 1
        elif ch == "[":
            end = sql.find("]", i)
            if end == -1:
                out.append(ch)
                i += 1
            else:
                name = sql[i + 1:end].replace("]]", "]")
                if name.lower() in TYPE_KEYWORDS:
                    out.append(name)
                else:
                    out.append("`" + name.replace("`", "``") + "`")
                i = end + 1
        else:
            out.append(ch)
            i += 1
    result = "".join(out)
    result = result.replace("`dbo`.", "")  # schema qualifier has no MariaDB equivalent
    return result


def convert_types(sql: str) -> str:
    for pat, rep in COMPILED_TYPES:
        sql = pat.sub(rep, sql)
    return sql


def fix_defaults(sql: str) -> str:
    # DEFAULT ((0)) / DEFAULT (0) / DEFAULT ('x') -> DEFAULT 0 / 'x'
    sql = re.sub(r"(?i)DEFAULT\s*\(\s*\((-?\d+(?:\.\d+)?)\)\s*\)", r"DEFAULT \1", sql)
    sql = re.sub(r"(?i)DEFAULT\s*\(\((-?\d+(?:\.\d+)?)\)\)", r"DEFAULT \1", sql)
    sql = re.sub(r"(?i)DEFAULT\s*\(\s*(-?\d+(?:\.\d+)?)\s*\)", r"DEFAULT \1", sql)
    sql = re.sub(r"(?i)DEFAULT\s*\(\s*(N?'(?:[^']|'')*')\s*\)", r"DEFAULT \1", sql)
    sql = re.sub(r"(?i)\bgetdate\s*\(\s*\)", "CURRENT_TIMESTAMP", sql)
    sql = re.sub(r"(?i)\bgetutcdate\s*\(\s*\)", "CURRENT_TIMESTAMP", sql)
    sql = re.sub(r"(?i)\bnewid\s*\(\s*\)", "(UUID())", sql)
    return sql


def strip_table_options(sql: str) -> str:
    """Remove SSMS storage noise: TEXTIMAGE_ON, ON filegroup, WITH (...), etc."""
    sql = re.sub(r"(?i)\bTEXTIMAGE_ON\s+\S+", "", sql)
    # filegroup after a closing paren: ) ON `PRIMARY` / ) ON `fg`(col)
    sql = re.sub(r"(?i)\)\s*ON\s+`[^`]+`(\s*\([^)]*\))?", ")", sql)
    # WITH (PAD_INDEX = OFF, ...) possibly nested one level
    sql = re.sub(r"(?is)\bWITH\s*\((?:[^()]|\([^()]*\))*\)", "", sql)
    sql = re.sub(r"(?i)\b(CLUSTERED|NONCLUSTERED)\b", "", sql)
    sql = re.sub(r"(?i)\bIDENTITY\s*\(\s*\d+\s*,\s*\d+\s*\)", "AUTO_INCREMENT", sql)
    sql = re.sub(r"(?i)\bROWGUIDCOL\b", "", sql)
    sql = re.sub(r"(?i)\bNOT\s+FOR\s+REPLICATION\b", "", sql)
    sql = re.sub(r"(?i)\bSPARSE\b", "", sql)
    return sql


def convert_create_table(sql: str) -> str:
    sql = bracket_to_backtick(sql)
    sql = strip_table_options(sql)
    sql = convert_types(sql)
    sql = fix_defaults(sql)
    # Named default constraints inline: CONSTRAINT `DF_x` DEFAULT 0 -> DEFAULT 0
    sql = re.sub(r"(?i)CONSTRAINT\s+`[^`]+`\s+(DEFAULT|PRIMARY\s+KEY|UNIQUE|CHECK)",
                 lambda m: m.group(1).upper(), sql)
    sql = re.sub(r"[ \t]+", " ", sql)
    # MariaDB does not accept asc/desc inside key column lists.
    sql = re.sub(r"(?i)\s+(ASC|DESC)(\s*[,\)])", r"\2", sql)
    sql = sql.rstrip().rstrip(";")
    if not re.search(r"(?i)ENGINE\s*=", sql):
        sql += " ENGINE=InnoDB ROW_FORMAT=DYNAMIC"
    return sql + ";"


def convert_alter(sql: str) -> str | None:
    sql_bt = bracket_to_backtick(sql)
    upper = sql_bt.upper()
    # ADD CONSTRAINT pk PRIMARY KEY (cols) -> ADD PRIMARY KEY (cols)
    m = re.search(r"(?is)ADD\s+CONSTRAINT\s+`[^`]+`\s+(PRIMARY\s+KEY|UNIQUE)\s*(\([^)]*\))",
                  sql_bt)
    if m:
        table = re.search(r"(?is)ALTER\s+TABLE\s+(`[^`]+`)", sql_bt)
        if table:
            cols = re.sub(r"(?i)\s+(ASC|DESC)(\s*[,\)])", r"\2", m.group(2))
            return f"ALTER TABLE {table.group(1)} ADD {m.group(1).upper()} {cols};"
        return None
    if "FOREIGN KEY" in upper:
        sql_bt = re.sub(r"(?i)WITH\s+(NO)?CHECK\b", "", sql_bt)
        return strip_table_options(sql_bt).rstrip(";") + ";"
    if "CHECK CONSTRAINT" in upper or "NOCHECK" in upper:
        return None  # enable/disable constraint toggles: no-op for import
    if "DEFAULT" in upper:
        # Standalone DF constraint - how SSMS scripts nearly all defaults:
        #   ALTER TABLE `T` ADD CONSTRAINT `DF_..` DEFAULT (<expr>) FOR `col`
        #   ALTER TABLE `T` ADD DEFAULT (<expr>) FOR `col`
        # -> ALTER TABLE `T` ALTER COLUMN `col` SET DEFAULT <expr>;
        # These used to be dropped, losing ALL 122 defaults of the real
        # KN_Online dump; NOT NULL columns then break runtime INSERTs
        # (ACCOUNT_LOGIN auto-register, MAIL_BOX_SEND, ...) under MariaDB
        # strict mode.
        m = re.search(r"(?is)ALTER\s+TABLE\s+(`[^`]+`)\s+ADD\s+(?:CONSTRAINT\s+`[^`]+`\s+)?"
                      r"DEFAULT\s+(.+?)\s+FOR\s+(`[^`]+`)\s*$",
                      sql_bt.rstrip("; \t\r\n"))
        if not m:
            return None
        expr = fix_defaults("DEFAULT " + m.group(2).strip())[len("DEFAULT "):]
        expr = expr.replace("(CURRENT_TIMESTAMP)", "CURRENT_TIMESTAMP")
        return f"ALTER TABLE {m.group(1)} ALTER COLUMN {m.group(3)} SET DEFAULT {expr};"
    return strip_table_options(sql_bt).rstrip(";") + ";"


def convert_create_index(sql: str) -> str:
    sql = bracket_to_backtick(sql)
    sql = re.sub(r"(?i)\b(CLUSTERED|NONCLUSTERED)\b", "", sql)
    sql = re.sub(r"(?is)\bINCLUDE\s*\([^)]*\)", "", sql)          # MariaDB has no INCLUDE
    sql = re.sub(r"(?is)\bWITH\s*\((?:[^()]|\([^()]*\))*\)", "", sql)
    sql = sql.rstrip(";").rstrip()
    sql = re.sub(r"(?i)\)\s*ON\s+`[^`]+`\s*$", ")", sql)          # trailing filegroup
    sql = re.sub(r"(?i)\s+(ASC|DESC)(\s*[,\)])", r"\2", sql)
    return sql.rstrip(";") + ";"


def convert_dml(sql: str) -> str:
    sql = bracket_to_backtick(sql)
    # CAST(0xAB.. AS VarBinary(123)) -> 0xAB.. ; CAST(N'x' AS ...) -> 'x'
    sql = re.sub(r"(?i)CAST\s*\(\s*(0x[0-9A-Fa-f]+)\s+AS\s+[A-Za-z]+\s*(?:\(\s*[\d,\s]+\))?\s*\)", r"\1", sql)
    sql = re.sub(r"(?i)CAST\s*\(\s*(N?'(?:[^']|'')*')\s+AS\s+[A-Za-z]+\s*(?:\(\s*[\d,\s]+\))?\s*\)", r"\1", sql)
    # N'unicode' literal -> plain (MariaDB connection charset decides storage)
    sql = re.sub(r"\bN'((?:[^']|'')*)'", r"'\1'", sql)
    return sql.rstrip(";") + ";"


ROUTINE_RE = re.compile(
    r"(?is)CREATE\s+(PROCEDURE|PROC|FUNCTION|TRIGGER|VIEW)\s+"
    r"(?:\[?dbo\]?\.)?\s*\[?([A-Za-z0-9_]+)\]?")


def routine_signature(sql: str) -> str:
    m = re.search(r"(?is)CREATE\s+(?:PROCEDURE|PROC|FUNCTION)\s+\S+\s*(.*?)\bAS\b", sql)
    sig = (m.group(1) if m else "").strip()
    return re.sub(r"\s+", " ", sig)[:400]


def split_batches(lines):
    """Yield statements split on GO batch separators (SSMS style)."""
    buf = []
    for line in lines:
        if re.match(r"^\s*GO\s*(\d+)?\s*(--.*)?$", line, re.IGNORECASE):
            if buf:
                yield "".join(buf)
                buf = []
        else:
            buf.append(line)
    if buf:
        yield "".join(buf)


def split_semicolons(batch: str):
    """Split a batch on top-level semicolons (respecting strings/comments)."""
    stmts, cur, i, in_str, in_line, in_block = [], [], 0, False, False, False
    while i < len(batch):
        ch = batch[i]
        nxt = batch[i + 1:i + 2]
        if in_line:
            cur.append(ch)
            if ch == "\n":
                in_line = False
        elif in_block:
            cur.append(ch)
            if ch == "*" and nxt == "/":
                cur.append(nxt)
                i += 1
                in_block = False
        elif in_str:
            cur.append(ch)
            if ch == "'":
                if nxt == "'":
                    cur.append(nxt)
                    i += 1
                else:
                    in_str = False
        elif ch == "'":
            in_str = True
            cur.append(ch)
        elif ch == "-" and nxt == "-":
            in_line = True
            cur.append(ch)
        elif ch == "/" and nxt == "*":
            in_block = True
            cur.append(ch)
        elif ch == ";":
            cur.append(ch)
            stmts.append("".join(cur))
            cur = []
        else:
            cur.append(ch)
        i += 1
    tail = "".join(cur).strip()
    if tail:
        stmts.append("".join(cur))
    return stmts


# A new statement also starts at a line beginning with one of these keywords:
# SSMS "Generate Scripts" emits consecutive INSERT lines with NO semicolons
# between them, and wraps identity-table data in SET IDENTITY_INSERT ON/OFF
# inside a single GO batch. Semicolon-only splitting therefore glued whole
# tables into one invalid statement (and dropped identity-table batches,
# which start with SET). Routine batches never reach this splitter - they
# are intercepted whole in main() before any splitting happens.
_DML_LINE_RE = re.compile(
    r"^\s*(?:INSERT|UPDATE|DELETE|MERGE)\b"
    r"|^\s*SET\s+(?:IDENTITY_INSERT|ANSI_NULLS|ANSI_PADDING|ANSI_WARNINGS|"
    r"QUOTED_IDENTIFIER|ARITHABORT|CONCAT_NULL_YIELDS_NULL|NUMERIC_ROUNDABORT|"
    r"NOCOUNT|XACT_ABORT|TEXTSIZE|DATEFORMAT|LANGUAGE|LOCK_TIMEOUT)\b",
    re.IGNORECASE)


def _scan_line_state(line: str, in_str: bool, in_block: bool):
    """Track string/block-comment state across one line (for the splitter)."""
    i = 0
    while i < len(line):
        ch, nxt = line[i], line[i + 1:i + 2]
        if in_block:
            if ch == "*" and nxt == "/":
                in_block = False
                i += 2
                continue
        elif in_str:
            if ch == "'":
                if nxt == "'":
                    i += 2
                    continue
                in_str = False
        else:
            if ch == "'":
                in_str = True
            elif ch == "-" and nxt == "-":
                break
            elif ch == "/" and nxt == "*":
                in_block = True
                i += 2
                continue
        i += 1
    return in_str, in_block


def split_dml_lines(piece: str):
    """Split a semicolon-free piece on line-start DML/session keywords."""
    stmts, buf, buf_has_text = [], [], False
    in_str = in_block = False
    for line in piece.splitlines(keepends=True):
        if (not in_str and not in_block and buf_has_text
                and _DML_LINE_RE.match(line)):
            stmts.append("".join(buf))
            buf, buf_has_text = [], False
        buf.append(line)
        if line.strip():
            buf_has_text = True
        in_str, in_block = _scan_line_state(line, in_str, in_block)
    if buf_has_text:
        stmts.append("".join(buf))
    return stmts


def split_statements(batch: str):
    """Full splitter for non-routine batches: semicolons + DML line starts."""
    out = []
    for piece in split_semicolons(batch):
        out.extend(split_dml_lines(piece))
    return out


def leading_keyword(stmt: str) -> str:
    s = stmt.strip()
    while True:
        if s.startswith("--"):
            nl = s.find("\n")
            s = s[nl + 1:].lstrip() if nl != -1 else ""
        elif s.startswith("/*"):
            end = s.find("*/")
            s = s[end + 2:].lstrip() if end != -1 else ""
        else:
            break
    m = re.match(r"([A-Za-z]+)", s)
    return m.group(1).upper() if m else ""


def detect_encoding(path: str, forced: str | None) -> str:
    if forced:
        return forced
    with open(path, "rb") as fh:
        magic = fh.read(4)
    if magic.startswith(b"\xff\xfe"):
        return "utf-16"          # SSMS default for script files
    if magic.startswith(b"\xef\xbb\xbf"):
        return "utf-8-sig"
    if magic[1:2] == b"\x00" and magic[3:4] == b"\x00":
        return "utf-16-le"
    return "utf-8"


# Tables whose rows are player accounts/characters. Their schema is still
# converted, but --exclude-user-data drops their INSERTs so the output can be
# committed safely even to a public repo.
USER_DATA_TABLES = {
    "TB_USER", "ACCOUNT_CHAR", "CURRENTUSER", "USERDATA", "WAREHOUSE",
    "USERDATA_SKILLSHORTCUT", "MAIL_BOX", "SEALED_ITEMS", "RENTAL_ITEM",
    "WEB_ITEMMALL", "USER_DAILY_OP", "USER_KNIGHTS_RANK", "USER_PERSONAL_RANK",
    "KNIGHTS_USER", "FRIEND_LIST", "REMOVED_USERDATA",
    # Added after seeing the real KN_Online schema (1453db.sql): character
    # state, purchase/payment logs, and Turkish web-panel user tables
    # (_SIFRE_DEGISTIR holds password-reset e-mails/hashes!).
    "USER_SAVED_MAGIC", "USER_ITEMS", "USER_RENTAL_ITEM", "RENTAL_ITEM_LIST",
    "WEB_ITEMMALL_LOG", "PUS_PAYPAL_LOG", "KING_BALLOT_BOX",
    "KING_ELECTION_LIST", "_ANKET_OYLAYANLAR", "_IP_ENGELLILER", "_MAIL",
    "_SIFRE_DEGISTIR", "_SIKAYET", "_TICKET", "_TICKET_CEVAP",
    # Clan world-state: with USERDATA/KNIGHTS_USER excluded these rows are
    # orphaned player clans; a fresh server regenerates them via gameplay.
    "KNIGHTS", "KNIGHTS_CAPE", "KNIGHTS_RATING", "KNIGHTS_ALLIANCE",
    "KNIGHTS_SIEGE_WARFARE", "MAIL_ITEM", "QUEST_LOG",
}


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("input", help="SSMS-generated .sql dump")
    ap.add_argument("--database", required=True, help="Target MariaDB database name, e.g. KO_MAIN")
    ap.add_argument("-o", "--outdir", required=True, help="Output directory")
    ap.add_argument("--encoding", default=None, help="Force input encoding (default: auto-detect)")
    ap.add_argument("--chunk-mb", type=int, default=64, help="Split data output every N MB (default 64)")
    ap.add_argument("--exclude-user-data", action="store_true",
                    help="Skip INSERTs for account/character tables (TB_USER, USERDATA, WAREHOUSE, ...) so output is safe to commit")
    ap.add_argument("--exclude", action="append", default=[], metavar="TABLE",
                    help="Additionally skip INSERTs for this table (repeatable)")
    args = ap.parse_args()

    db = args.database
    os.makedirs(args.outdir, exist_ok=True)
    os.makedirs(os.path.join(args.outdir, "procedures_tsql"), exist_ok=True)

    encoding = detect_encoding(args.input, args.encoding)
    print(f"[converter] reading {args.input} as {encoding}")

    schema_path = os.path.join(args.outdir, f"{db}_01_schema.sql")
    schema = open(schema_path, "w", encoding="utf-8", newline="\n")
    schema.write("-- Auto-converted from SQL Server dump (mssql_to_mariadb.py). Review before production use.\n")
    # Deliberately NO "CREATE DATABASE / USE <name>" here: the target database
    # is chosen by the server configuration (INI/appsettings) and created by
    # DatabaseMigrator, which already connects to the right database. The
    # original MSSQL name (e.g. KN_Online) is provenance only - see
    # manifest.json. For a manual import pick the database on the CLI:
    #   mariadb -u root -p <your-db-name> < this-file.sql
    schema.write("-- Target database: chosen by server config / the importing connection (not by this file).\n")
    schema.write("SET FOREIGN_KEY_CHECKS=0;\nSET SQL_MODE='NO_AUTO_VALUE_ON_ZERO';\n\n")

    chunk_no = 2
    chunk_bytes = 0
    chunk_limit = args.chunk_mb * 1024 * 1024
    data_files = []

    def new_data_file():
        nonlocal chunk_no, chunk_bytes
        path = os.path.join(args.outdir, f"{db}_{chunk_no:02d}_data.sql")
        fh = open(path, "w", encoding="utf-8", newline="\n")
        fh.write("-- Target database: chosen by server config / the importing connection (not by this file).\nSET FOREIGN_KEY_CHECKS=0;\nSET SQL_MODE='NO_AUTO_VALUE_ON_ZERO';\n\n")
        data_files.append(path)
        chunk_no += 1
        chunk_bytes = 0
        return fh

    data = new_data_file()
    routines = []
    row_counts: dict[str, int] = {}
    skipped = converted = errors = 0

    excluded_tables = {t.upper() for t in args.exclude}
    if args.exclude_user_data:
        excluded_tables |= USER_DATA_TABLES
    excluded_rows: dict[str, int] = {}

    insert_re = re.compile(r"(?is)^\s*INSERT\s+(?:INTO\s+)?(?:`dbo`\.)?`?([A-Za-z0-9_]+)`?")
    dml_table_re = re.compile(r"(?is)^\s*(?:UPDATE|DELETE\s+FROM)\s+(?:`dbo`\.)?`?([A-Za-z0-9_]+)`?")

    with open(args.input, "r", encoding=encoding, errors="replace") as fh:
        for batch in split_batches(fh):
            # Routines arrive as whole GO batches; never semicolon-split them.
            rm = ROUTINE_RE.search(batch)
            if rm and leading_keyword(batch) in ("CREATE", "ALTER"):
                kind, name = rm.group(1).upper(), rm.group(2)
                kind = "PROCEDURE" if kind == "PROC" else kind
                path = os.path.join(args.outdir, "procedures_tsql", f"{name}.sql")
                with open(path, "w", encoding="utf-8", newline="\n") as rf:
                    rf.write(batch.strip() + "\n")
                routines.append({"type": kind, "name": name, "signature": routine_signature(batch)})
                continue

            for stmt in split_statements(batch):
                text = stmt.strip()
                if not text:
                    continue
                upper_head = text.upper()
                if any(upper_head.startswith(p) for p in DROP_STATEMENT_PREFIXES):
                    skipped += 1
                    continue
                kw = leading_keyword(text)
                try:
                    if kw == "CREATE" and "TABLE" in upper_head[:40]:
                        schema.write(convert_create_table(text) + "\n\n")
                    elif kw == "CREATE" and ("INDEX" in upper_head[:60]):
                        schema.write(convert_create_index(text) + "\n")
                    elif kw == "ALTER" and "TABLE" in upper_head[:40]:
                        out = convert_alter(text)
                        if out:
                            schema.write(out + "\n")
                        else:
                            skipped += 1
                            continue
                    elif kw in ("INSERT", "UPDATE", "DELETE", "MERGE"):
                        bt = bracket_to_backtick(text)
                        tm = insert_re.search(bt) or dml_table_re.search(bt)
                        if tm and tm.group(1).upper() in excluded_tables:
                            excluded_rows[tm.group(1)] = excluded_rows.get(tm.group(1), 0) + 1
                            skipped += 1
                            continue
                        out = convert_dml(text)
                        data.write(out + "\n")
                        chunk_bytes += len(out) + 1
                        if kw == "INSERT":
                            m = insert_re.search(bracket_to_backtick(text))
                            if m:
                                row_counts[m.group(1)] = row_counts.get(m.group(1), 0) + 1
                        if chunk_bytes >= chunk_limit:
                            data.close()
                            data = new_data_file()
                    else:
                        skipped += 1
                        continue
                    converted += 1
                except Exception as exc:  # keep going; dump conversion is best-effort
                    errors += 1
                    print(f"[converter] WARNING: failed on statement starting "
                          f"{text[:80]!r}: {exc}", file=sys.stderr)

    schema.write("\nSET FOREIGN_KEY_CHECKS=1;\n")
    schema.close()
    data.write("\nSET FOREIGN_KEY_CHECKS=1;\n")
    data.close()

    manifest = {
        "database": db,
        "source": os.path.basename(args.input),
        "input_encoding": encoding,
        "schema_file": os.path.basename(schema_path),
        "data_files": [os.path.basename(p) for p in data_files],
        "statements_converted": converted,
        "statements_skipped": skipped,
        "statement_errors": errors,
        "tables_with_rows": dict(sorted(row_counts.items())),
        "excluded_tables_rows_skipped": dict(sorted(excluded_rows.items())),
        "routines_extracted": routines,
    }
    with open(os.path.join(args.outdir, "manifest.json"), "w", encoding="utf-8") as mf:
        json.dump(manifest, mf, indent=2)

    with open(os.path.join(args.outdir, "procedures_report.md"), "w", encoding="utf-8", newline="\n") as rf:
        rf.write(f"# Routines in {db} needing a manual T-SQL -> MariaDB/C# port\n\n")
        rf.write("Original T-SQL in `procedures_tsql/`. Port each as a MariaDB procedure\n"
                 "or lift it into C# repository code (see docs/CSHARP_REWRITE_PLAN.md).\n\n")
        rf.write("| Type | Name | Signature (truncated) |\n|---|---|---|\n")
        for r in routines:
            sig = r["signature"].replace("|", "\\|")
            rf.write(f"| {r['type']} | {r['name']} | `{sig}` |\n")
        if not routines:
            rf.write("| - | (none found) | - |\n")

    print(f"[converter] done: {converted} statements converted, {skipped} skipped, "
          f"{errors} errors, {len(routines)} routines extracted, "
          f"{len(row_counts)} tables with data ({sum(row_counts.values())} inserts).")
    if excluded_rows:
        print(f"[converter] excluded {sum(excluded_rows.values())} statement(s) for {len(excluded_rows)} table(s): "
              + ", ".join(sorted(excluded_rows)))
    print(f"[converter] outputs in {args.outdir}: {os.path.basename(schema_path)}, "
          f"{len(data_files)} data file(s), procedures_report.md, manifest.json")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
