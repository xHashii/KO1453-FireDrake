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

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import mariadb_portability as portability  # noqa: E402  (sibling module)

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

def _build_type_matcher():
    """Compile TYPE_RULES into a single regex + replacement table.

    The rules used to be applied one after another over the whole CREATE TABLE
    body, which let a later rule re-match an *earlier rule's output*: ``[bit]``
    became ``TINYINT(1)`` and was then hit by the ``tinyint`` rule, producing
    ``TINYINT UNSIGNED(1)`` -- which MariaDB rejects (the display width has to
    come *before* UNSIGNED), failing the whole script with error 1064.

    One combined alternation replaces each source type token exactly once, so a
    replacement can never be rewritten again.  Each rule is wrapped in its own
    named group, which also keeps rule-local ``|`` alternations from leaking
    into neighbouring rules.
    """
    parts = []
    table = {}
    groups_before = 0
    for index, (pattern, replacement) in enumerate(TYPE_RULES):
        name = f"rule{index}"
        parts.append(f"(?P<{name}>{pattern})")
        # A rule's own group 1 sits at groups_before + 2 in the combined
        # expression (group 1 of the rule is its wrapper), so shift every
        # \1-style backreference in the replacement accordingly.
        base = groups_before + 1
        table[name] = re.sub(r"\\([1-9])",
                             lambda m, b=base: "\\" + str(int(m.group(1)) + b),
                             replacement)
        groups_before += 1 + len(re.findall(r"(?<!\\)\((?!\?)", pattern))
    return re.compile("|".join(parts), re.IGNORECASE), table


TYPE_MATCHER, TYPE_REPLACEMENTS = _build_type_matcher()

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


def _bracket_is_type(sql: str, open_at: int, close_at: int) -> bool:
    """True when a bracketed token that *looks* like a type name is one.

    SSMS brackets every identifier, including columns whose name collides with
    a type keyword - the real KN_Online dump has `SN_NEWS.[date]`. Treating
    every bracketed type keyword as a type emitted a bare, unquoted `date`
    column (legal only because DATE happens not to be reserved in MariaDB, and
    it made the column invisible to identifier-based tooling). A bracketed
    keyword is a type only in type position:

      [varchar](20)          -> followed by "("
      [col] [smallint]       -> preceded by an identifier
      CAST(x AS [int])       -> preceded by AS
    """
    after = sql[close_at + 1:close_at + 2]
    if after == "(":
        return True

    before = sql[:open_at].rstrip()
    if not before:
        return False
    if before[-1] in ("]", "`", ")"):
        return True
    return re.search(r"(?i)\bAS$", before) is not None


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
                if name.lower() in TYPE_KEYWORDS and _bracket_is_type(sql, i, end):
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
    def replace(match: "re.Match[str]") -> str:
        return match.expand(TYPE_REPLACEMENTS[match.lastgroup])

    return TYPE_MATCHER.sub(replace, sql)


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


def _find_matching_paren(sql: str, open_index: int) -> int:
    depth = 0
    in_single = False
    in_backtick = False
    i = open_index
    while i < len(sql):
        ch = sql[i]
        if in_single:
            if ch == chr(92):  # backslash escape inside a string literal
                i += 2
                continue
            if ch == "'":
                if i + 1 < len(sql) and sql[i + 1] == "'":
                    i += 2
                    continue
                in_single = False
        elif in_backtick:
            if ch == "`":
                in_backtick = False
        else:
            if ch == "'":
                in_single = True
            elif ch == "`":
                in_backtick = True
            elif ch == "(":
                depth += 1
            elif ch == ")":
                depth -= 1
                if depth == 0:
                    return i
        i += 1
    return -1


def _key_columns(body: str) -> set[str]:
    cols: set[str] = set()
    norm = re.sub(r"\s+", " ", body)
    for m in re.finditer(
        r"(?:PRIMARY\s+KEY|UNIQUE(?:\s+(?:KEY|INDEX))?(?:\s+`[^`]+`)?|"
        r"(?:KEY|INDEX)(?:\s+`[^`]+`)?)\s*\(([^)]*)\)",
        norm,
        re.I,
    ):
        cols.update(c.lower() for c in re.findall(r"`([^`]+)`", m.group(1)))
    for line in body.splitlines():
        m = re.match(r"\s*`([^`]+)`.*\b(?:PRIMARY\s+KEY|UNIQUE)\b", line, re.I)
        if m:
            cols.add(m.group(1).lower())
    return cols


def ensure_auto_increment_key(sql: str) -> str:
    """Add a KEY for keyless AUTO_INCREMENT columns.

    SQL Server permits IDENTITY on a table with no key at all; MariaDB
    rejects that with ERROR 1075 ("there can be only one auto column and
    it must be defined as a key"). If a converted AUTO_INCREMENT column is
    not already part of a PRIMARY/UNIQUE/KEY definition, add a plain
    (non-unique) KEY for it. Non-unique is deliberate: it preserves
    IDENTITY semantics and cannot fail data loads on duplicate identity
    values that were legal in SQL Server.
    """
    # MariaDB allows AUTO_INCREMENT on integer types only (DECIMAL/NUMERIC
    # give ERROR 1063). SQL Server happily puts IDENTITY on numeric(18,0)
    # (e.g. SERVERS.sNum); scale-0 with precision <= 18 fits in a signed
    # BIGINT, so convert just those columns.
    sql = re.sub(
        r"(?im)^(\s*`[^`]+`\s+)(?:decimal|numeric)\(\s*(?:[1-9]|1[0-8])\s*,\s*0\s*\)(?=[^\n]*\bAUTO_INCREMENT\b)",
        r"\1bigint",
        sql,
    )

    m = re.search(r"(?is)CREATE\s+TABLE\s+`[^`]+`\s*\(", sql)
    if not m:
        return sql
    open_index = m.end() - 1
    close_index = _find_matching_paren(sql, open_index)
    if close_index < 0:
        return sql

    body = sql[open_index + 1:close_index]
    autos: list[str] = []
    for line in body.splitlines():
        cm = re.match(r"\s*`([^`]+)`.*\bAUTO_INCREMENT\b", line, re.I)
        if cm and cm.group(1).lower() not in [a.lower() for a in autos]:
            autos.append(cm.group(1))
    if not autos:
        return sql

    key_cols = _key_columns(body)
    missing = [c for c in autos if c.lower() not in key_cols]
    if not missing:
        return sql

    key_defs: list[str] = []
    for col in missing:
        index_name = ("ix_auto_" + col.replace("`", "``"))[:64].rstrip("`") or "ix_auto"
        key_defs.append(f"KEY `{index_name}` (`{col}`)")

    prefix = sql[:close_index].rstrip()
    sep = "\n " if prefix.endswith(",") else ",\n "
    insertion = sep + ",\n ".join(key_defs) + "\n"
    return prefix + insertion + sql[close_index:]


def make_column_types_portable(sql: str) -> str:
    """Rewrite column types that a MySQL-family server may refuse to create.

    SQL Server allows CHAR/BINARY/VARCHAR up to 8000 bytes; MySQL-family
    servers do not, and the limits depend on the row format the server ends up
    using (which we cannot fully control from DDL):

      * ERROR 1074 "Column length too big for column 'x' (max = 255)":
        InnoDB COMPACT/REDUNDANT and MyISAM cap *any* variable-length column -
        VARBINARY included - at 255 bytes. ROW_FORMAT=DYNAMIC only helps on
        servers that honour it.
      * ERROR 1118 "Row size too large": VARCHAR/CHAR count their full utf8mb4
        byte length toward the 65535-byte row limit (varchar(4096) = 16 KB!).
      * utf8mb4 conversion mangles packed binary stored in varchar columns
        (USERDATA.strSkill, BEGINNER_ITEM2.strItem, ...).

    BLOB/TEXT live off-page (20-byte pointer), so they are legal in every row
    format and engine, and VARBINARY keeps binary columns binary-safe.
    Columns that already fit in 255 bytes are left exactly as they are, so
    indexes, defaults and comparisons do not change. See mariadb_portability.py
    for the full rule set; every change is reported to stderr.
    """
    fixed, changes = portability.make_schema_portable(sql)
    for changed_table, table_changes in changes.items():
        for column, old, reason in table_changes:
            print(f"[converter] {changed_table}.{column}: {old} -> portable type ({reason})",
                  file=sys.stderr)
    # Belt and braces: convert_types() is a single pass now, so a rule cannot
    # rewrite another rule's output any more, but a misplaced UNSIGNED used to
    # slip through as `TINYINT UNSIGNED(1)` - which MariaDB rejects with ERROR
    # 1064 and which kills the whole CREATE TABLE.
    fixed, syntax_fixes = portability.normalize_type_syntax(fixed)
    for old, new in syntax_fixes:
        print(f"[converter] column type {old} -> {new} (MariaDB wants the width "
              f"before UNSIGNED)", file=sys.stderr)
    return fixed


# Old name kept for callers/notes that referenced the previous 255-only fix.
fix_oversized_fixed_types = make_column_types_portable


def convert_create_table(sql: str) -> str:
    sql = bracket_to_backtick(sql)
    sql = strip_table_options(sql)
    sql = convert_types(sql)
    sql = fix_oversized_fixed_types(sql)
    sql = fix_defaults(sql)
    # Named default constraints inline: CONSTRAINT `DF_x` DEFAULT 0 -> DEFAULT 0
    sql = re.sub(r"(?i)CONSTRAINT\s+`[^`]+`\s+(DEFAULT|PRIMARY\s+KEY|UNIQUE|CHECK)",
                 lambda m: m.group(1).upper(), sql)
    sql = re.sub(r"[ \t]+", " ", sql)
    # MariaDB does not accept asc/desc inside key column lists.
    sql = re.sub(r"(?i)\s+(ASC|DESC)(\s*[,\)])", r"\2", sql)
    sql = ensure_auto_increment_key(sql)
    # A column whose name is a type keyword (SN_NEWS.date in the real dump) can
    # survive bracket_to_backtick unquoted when it sits in type position. It
    # only works while the keyword happens not to be reserved, so quote it here
    # instead of leaving the repair to the checker.
    sql, _ = portability.quote_bare_column_names(sql)
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


# --------------------------------------------------------------------------
# Binary payloads that SQL Server stored in "string" columns
# --------------------------------------------------------------------------
# SSMS writes those bytes as a quoted literal, so after the UTF-16 -> text
# decode they arrive here as a str that is really a byte blob. Inserting that
# into a VARBINARY/BLOB column as '...' runs it through the connection charset
# (utf8mb4) and either mangles every byte > 0x7F or fails outright with
# "Incorrect string value". 0x.. hex literals are charset-independent.

_CONTROL_CHARS = frozenset(chr(c) for c in range(0x20) if c != 0x09) | {"\x7f"}

_INSERT_HEAD_RE = re.compile(
    r"(?is)^\s*INSERT\s+(?:INTO\s+)?(?:`[^`]+`\s*\.\s*)?`?(?P<table>[A-Za-z0-9_]+)`?\s*"
    r"(?:\((?P<columns>[^)]*)\))?")

#: "table.column" -> count of literals whose bytes the export had already lost.
BINARY_LITERAL_LOST: dict[str, int] = {}
#: "table.column" -> count of literals re-emitted as charset-independent 0x.. hex.
BINARY_LITERAL_HEXED: dict[str, int] = {}


def unescape_sql_literal(raw: str) -> str:
    """Resolve MySQL backslash escapes and doubled quotes inside a literal."""
    if "''" not in raw and "\\" not in raw:
        return raw
    out: list[str] = []
    i = 0
    while i < len(raw):
        ch = raw[i]
        if ch == "\\" and i + 1 < len(raw):
            nxt = raw[i + 1]
            out.append({"n": "\n", "r": "\r", "t": "\t", "0": "\0",
                        "b": "\b", "Z": "\x1a", "\\": "\\"}.get(nxt, nxt))
            i += 2
            continue
        if ch == "'" and raw[i + 1:i + 2] == "'":
            out.append("'")
            i += 2
            continue
        out.append(ch)
        i += 1
    return "".join(out)


def literal_is_binary(text: str) -> bool:
    """True when a string literal is really a byte blob, not text."""
    if any(ch in _CONTROL_CHARS for ch in text):
        return True
    if any("\u0080" <= ch <= "\u009f" for ch in text):
        return True
    # U+FFFD is what the lossy UTF-16 decode left behind for unmappable bytes.
    return "\ufffd" in text


def literal_to_bytes(text: str) -> bytes | None:
    """Best-effort recovery of the original bytes of a binary literal.

    The export was decoded as text, so byte values that are not representable
    as Latin-1 code points were already lost before we got here. Returns None
    when nothing faithful can be produced.
    """
    try:
        return text.encode("latin-1")
    except UnicodeEncodeError:
        return None


def _iter_string_literals(values: str):
    """Yield (start, end_exclusive, inner_text) for each top-level '...' literal."""
    i = 0
    n = len(values)
    while i < n:
        if values[i] != "'":
            i += 1
            continue
        j = i + 1
        while j < n:
            ch = values[j]
            if ch == "\\":
                j += 2
                continue
            if ch == "'":
                if values[j + 1:j + 2] == "'":
                    j += 2
                    continue
                break
            j += 1
        end = min(j, n - 1)
        yield i, end + 1, values[i + 1:end]
        i = end + 1


def convert_binary_literals(sql: str, policy: str) -> str:
    """Make binary payloads in an INSERT safe for VARBINARY/BLOB columns.

    policy="hex"  -> 0x.. when the original bytes survived the export, else
                     NULL (a mangled literal would silently poison the column).
    policy="null" -> NULL for every binary literal. Use for dumps whose text
                     export already destroyed the bytes (the committed
                     sql/mysql/kn_online artifacts are that case for
                     BEGINNER_ITEM2/S_ITEM.strItem).
    """
    head = _INSERT_HEAD_RE.match(sql)
    if not head or not head.group("columns"):
        return sql

    table = head.group("table")
    columns = [c.strip().strip("`") for c in head.group("columns").split(",")]

    values_at = sql.upper().find("VALUES", head.end())
    if values_at < 0:
        return sql
    open_paren = sql.find("(", values_at)
    if open_paren < 0:
        return sql

    body = sql[open_paren + 1:]
    # Everything up to and including "(" is kept verbatim; only the offending
    # literal is replaced.
    pieces: list[str] = [sql[:open_paren + 1]]
    cursor = open_paren + 1
    column_index = 0
    scanned = 0
    changed = False

    for start, end, inner in _iter_string_literals(body):
        # Count the separating commas *between* literals only, so a comma
        # inside a previous value cannot shift the column mapping.
        column_index += body[scanned:start].count(",")
        scanned = end
        if column_index >= len(columns):
            continue

        text = unescape_sql_literal(inner)
        if not literal_is_binary(text):
            continue

        column = columns[column_index]
        raw = literal_to_bytes(text)
        key = f"{table}.{column}"
        if raw is not None and policy == "hex":
            replacement = "0x" + raw.hex().upper()
            BINARY_LITERAL_HEXED[key] = BINARY_LITERAL_HEXED.get(key, 0) + 1
        else:
            replacement = "NULL"
            BINARY_LITERAL_LOST[key] = BINARY_LITERAL_LOST.get(key, 0) + 1

        abs_start, abs_end = open_paren + 1 + start, open_paren + 1 + end
        pieces.append(sql[cursor:abs_start])
        pieces.append(replacement)
        cursor = abs_end
        changed = True

    if not changed:
        return sql
    pieces.append(sql[cursor:])
    return "".join(pieces)


#: T-SQL CAST target type -> (MariaDB CAST target, keep the length argument).
#:
#: MariaDB's grammar (sql/sql_yacc.yy: `cast_type`, `cast_type_numeric`,
#: `cast_type_temporal`) accepts only BINARY(n), CHAR(n), VARCHAR(n), NCHAR(n),
#: INT, SIGNED, UNSIGNED, DECIMAL(M,D), FLOAT, DOUBLE(p), DATE, TIME(fsp) and
#: DATETIME(fsp). A name being a valid *column* type says nothing about CAST:
#: NUMERIC, MONEY, NVARCHAR, VARBINARY and DATETIME2 are all fine in CREATE
#: TABLE and all ERROR 1064 inside CAST() - which is how SSMS's
#: `VALUES (CAST(1 AS Numeric(18, 0)), ...)` stranded a data script.
CAST_TYPE_MAP: dict[str, tuple[str, bool]] = {
    "numeric": ("DECIMAL", True),
    "decimal": ("DECIMAL", True),
    "money": ("DECIMAL(19,4)", False),
    "smallmoney": ("DECIMAL(10,4)", False),
    "bit": ("UNSIGNED", False),
    "tinyint": ("UNSIGNED", False),
    "smallint": ("SIGNED", False),
    "bigint": ("SIGNED", False),
    "int": ("SIGNED", False),
    "integer": ("SIGNED", False),
    "real": ("DOUBLE", False),
    "float": ("DOUBLE", False),
    "nvarchar": ("CHAR", True),
    "nchar": ("CHAR", True),
    "varchar": ("CHAR", True),
    "char": ("CHAR", True),
    "character": ("CHAR", True),
    "ntext": ("CHAR", False),
    "text": ("CHAR", False),
    "xml": ("CHAR", False),
    "sql_variant": ("CHAR", False),
    "uniqueidentifier": ("CHAR(36)", False),
    "varbinary": ("BINARY", True),
    "binary": ("BINARY", True),
    "image": ("BINARY", False),
    "datetime2": ("DATETIME", False),
    "datetimeoffset": ("DATETIME", False),
    "smalldatetime": ("DATETIME", False),
    "datetime": ("DATETIME", False),
    "date": ("DATE", False),
    "time": ("TIME", False),
}

_CAST_RE = re.compile(
    r"(?is)\bCAST\s*\(\s*(?P<expr>.*?)\s+AS\s+"
    r"(?P<type>[A-Za-z_][A-Za-z0-9_]*)\s*(?:\(\s*(?P<args>[^()]*)\))?\s*\)")


def _map_outside_literals(sql: str, transform) -> str:
    """Apply `transform` to everything that is not a '...' string literal.

    Item names and NPC dialogue in this dump contain brackets, keywords and
    even the word CAST; rewriting inside a literal would corrupt player-visible
    text, so the literal spans are copied through untouched.
    """
    pieces = []
    cursor = 0
    for start, end, _ in _iter_string_literals(sql):
        pieces.append(transform(sql[cursor:start]))
        pieces.append(sql[start:end])
        cursor = end
    pieces.append(transform(sql[cursor:]))
    return "".join(pieces)


def convert_cast_types(sql: str) -> str:
    """Rewrite CAST target types to the vocabulary MariaDB's parser accepts."""

    def repl(m: re.Match[str]) -> str:
        mapping = CAST_TYPE_MAP.get(m.group("type").lower())
        if mapping is None:
            return m.group(0)  # DATE/TIME/SIGNED/... or something unknown
        target, keep_length = mapping
        args = re.sub(r"\s+", "", m.group("args") or "")
        # "(max)" and friends are not a CAST length; drop them (bare CHAR and
        # bare BINARY are both legal, VARCHAR is not - hence CHAR above).
        if keep_length and re.fullmatch(r"\d+(?:,\d+)?", args):
            target = f"{target}({args})"
        return f"CAST({m.group('expr').strip()} AS {target})"

    return _map_outside_literals(sql, lambda part: _CAST_RE.sub(repl, part))


def convert_dml(sql: str, binary_literals: str = "hex") -> str:
    sql = bracket_to_backtick(sql)
    # CAST(0xAB.. AS VarBinary(123)) -> 0xAB.. ; CAST(N'x' AS ...) -> 'x'
    sql = re.sub(r"(?i)CAST\s*\(\s*(0x[0-9A-Fa-f]+)\s+AS\s+[A-Za-z]+\s*(?:\(\s*[\d,\s]+\))?\s*\)", r"\1", sql)
    sql = re.sub(r"(?i)CAST\s*\(\s*(N?'(?:[^']|'')*')\s+AS\s+[A-Za-z]+\s*(?:\(\s*[\d,\s]+\))?\s*\)", r"\1", sql)
    # SSMS wraps every numeric/decimal value in a CAST to pin its exactness;
    # for a literal the column type already does that, so unwrap it. This is
    # what `INSERT SERVERS ... VALUES (CAST(1 AS Numeric(18, 0)), ...)` needed.
    sql = re.sub(r"(?i)CAST\s*\(\s*(-?\d+(?:\.\d+)?)\s+AS\s+[A-Za-z]+\s*(?:\(\s*[\d,\s]+\))?\s*\)", r"\1", sql)
    # Anything left has a real expression inside, so keep the CAST and only
    # translate its target type.
    sql = convert_cast_types(sql)
    # N'unicode' literal -> plain (MariaDB connection charset decides storage)
    sql = re.sub(r"\bN'((?:[^']|'')*)'", r"'\1'", sql)
    sql = convert_binary_literals(sql, binary_literals)
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
    ap.add_argument("--binary-literals", choices=("hex", "null"), default="hex",
                    help="How to emit binary payloads that the dump stored as quoted "
                         "string literals (BEGINNER_ITEM2.strItem, S_ITEM.strItem, ...). "
                         "'hex' (default) writes 0x.. so VARBINARY/BLOB columns keep the "
                         "exact bytes; 'null' writes NULL for every one of them - use it "
                         "when the text export already destroyed the bytes and you would "
                         "rather have an honest NULL than mangled data")
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
                        out = convert_dml(text, args.binary_literals)
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
        "binary_literals_to_hex": dict(sorted(BINARY_LITERAL_HEXED.items())),
        "binary_literals_unrecoverable": dict(sorted(BINARY_LITERAL_LOST.items())),
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
