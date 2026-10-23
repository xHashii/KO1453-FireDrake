#!/usr/bin/env python3
"""
mariadb_portability.py - one source of truth for "will this DDL actually run on
a MySQL-family server?", shared by the converter, the artifact repair tool and
the static validator:

  * tools/mssql_to_mariadb.py       (while converting a fresh MSSQL dump)
  * tools/repair_converted_sql.py   (re-fixing the already committed artifacts)
  * tools/validate_migrations.py    (gate over sql/mysql/**)

The limits below are the real ones from MariaDB's source, not folklore:

* CHAR / BINARY are handled by `Type_handler_string`, whose
  `Column_definition_set_attributes()` ends in
  `def->check_length(ER_TOO_BIG_FIELDLENGTH, MAX_FIELD_CHARLENGTH)`
  (sql/sql_type.cc). MAX_FIELD_CHARLENGTH is **255 characters**, so
  `binary(600)` / `char(1000)` - perfectly legal in SQL Server, which allows
  8000 - die with:

      ERROR 1074 (42000): Column length too big for column 'strItem'
                          (max = 255); use BLOB or TEXT instead

  That single statement aborts the whole migration script halfway through,
  which is exactly what the LoginServer's first live run produced.
* CHAR also has a hard 1024-**byte** ceiling (`prepare_stage1_string()` in
  sql/sql_table.cc): with utf8mb4 (mbmaxlen 4) a CHAR longer than 256
  characters is a strict-mode error even before the 255 rule.
* VARCHAR / VARBINARY go through `prepare_blob_field()`: the ceiling is
  MAX_FIELD_VARCHARLENGTH = **65535 bytes**, i.e. 16383 characters in utf8mb4.
  Over that, strict mode errors and non-strict mode silently rewrites the
  column to TEXT/BLOB.
* Independently of the server-level limits, InnoDB only stores up to ~8126
  bytes of a row inline. VARCHAR/VARBINARY count their full declared length
  there, while BLOB/TEXT columns leave a 20-byte pointer and spill off-page.
  A table with three `varchar(4096)` utf8mb4 columns (SN_NEWS) therefore
  depends on the row format the server actually honours - `ROW_FORMAT=DYNAMIC`
  in the DDL is only respected on servers that support it. LOB columns work in
  every row format and every engine, which is why the AdminPanel's own tables
  (0004_admin_tables.sql) already store item blobs as BLOB.
* Packed binary that SQL Server kept in a *character* column (USERDATA.strSkill
  varchar(10) - EDIT_SKILLS does `CAST(strSkill AS varbinary(10))`) must become
  VARBINARY/BLOB, or utf8mb4 mangles/rejects every byte > 0x7F.

Columns that already fit are left byte-for-byte alone, so indexes, DEFAULTs and
comparison semantics never change.
"""
from __future__ import annotations

import re

# Server-level ceilings (MariaDB sql/sql_type.cc + sql/sql_table.cc).
MAX_CHAR_LENGTH = 255          # CHAR / BINARY: characters
MAX_VARCHAR_BYTES = 65535      # VARCHAR / VARBINARY: bytes (MAX_FIELD_VARCHARLENGTH)
CHAR_OCTET_LIMIT = 1024        # CHAR: bytes (binlog fixed-string metadata limit)

# InnoDB inline row budget. Anything bigger must be able to spill off-page,
# which only the LOB families do in every row format.
INNODB_INLINE_LIMIT = 8126

# utf8mb4 is what docker-compose.yml configures and what DatabaseMigrator asks
# for in CREATE DATABASE, so it is the worst case for character byte math.
CHARSET_MAXLEN = 4

# Columns that hold packed binary even though SQL Server declared them
# varchar/char. Keyed by lowercase column name (KO names these consistently,
# and both the C++ and the C# readers treat them as byte arrays).
BINARY_COLUMN_NAMES: dict[str, str] = {
    # USERDATA.strSkill varchar(10): EDIT_SKILLS/VIEW_SKILLS do
    # CAST(strSkill AS varbinary(10)) and rebuild it with CONVERT(VARBINARY(1),..).
    "strskill": "varbinary",
    # USERDATA_SKILLSHORTCUT.strSkillData varchar(260): packed shortcut bytes.
    "strskilldata": "varbinary",
    # USER_ITEMS.nItemSerial varchar(586): parallel serial array, not text
    # (same shape as USERDATA.strSerial, which is binary in the source).
    "nitemserial": "varbinary",
}

# `col` <type>(<len>) - matches column definitions only, never key lists
# (a key line starts with KEY/PRIMARY/UNIQUE or is a bare "`col`" line).
_COLUMN_DEF_RE = re.compile(
    r"(?i)^(?P<head>\s*`(?P<name>[^`]+)`\s+)"
    r"(?P<type>char|varchar|binary|varbinary|nchar|nvarchar)"
    r"\s*\(\s*(?P<len>\d+)\s*\)"
    r"(?P<tail>.*)$"
)

_CREATE_TABLE_RE = re.compile(r"(?i)^\s*CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?`(?P<name>[^`]+)`")

# Lengths of the LOB families, for picking the smallest one that fits.
_BLOB_SIZES = ((65535, "blob"), (16777215, "mediumblob"))
_TEXT_SIZES = ((65535, "text"), (16777215, "mediumtext"))


def blob_type(byte_length: int) -> str:
    for limit, name in _BLOB_SIZES:
        if byte_length <= limit:
            return name
    return "longblob"


def text_type(byte_length: int) -> str:
    for limit, name in _TEXT_SIZES:
        if byte_length <= limit:
            return name
    return "longtext"


def portable_column_type(name: str, type_name: str, length: int) -> tuple[str, str | None]:
    """Return (replacement type, reason) for one column definition.

    `reason` is None when the column is already portable and must not change.
    """
    base = type_name.lower().split("(")[0].strip()
    is_binary = base in ("binary", "varbinary")

    # A character column that is documented to hold packed bytes is treated as
    # binary from here on (recursion terminates: the forced type is binary).
    forced = BINARY_COLUMN_NAMES.get(name.lower())
    if forced and not is_binary:
        # Pass a fully spelled type so the recursive "already portable" branch
        # cannot return a length-less keyword.
        new, reason = portable_column_type(name, f"{forced}({length})", length)
        if reason is None:
            return new, f"{type_name}({length}) holds packed binary data"
        return new, f"holds packed binary data; {reason}"

    if is_binary:
        if length <= MAX_CHAR_LENGTH:
            return type_name, None
        new = blob_type(length)
        if base == "binary":
            why = (f"{length} bytes over the {MAX_CHAR_LENGTH}-byte CHAR/BINARY "
                   f"limit (ERROR 1074)")
        else:
            why = (f"varbinary({length}) only fits row formats that store long "
                   f"columns off-page; {new} fits every row format and engine")
        return new, why

    if base in ("nchar", "char"):
        byte_len = length * CHARSET_MAXLEN
        if length <= MAX_CHAR_LENGTH and byte_len <= CHAR_OCTET_LIMIT:
            return type_name, None
        why = (f"char({length}) exceeds the {MAX_CHAR_LENGTH}-character CHAR limit"
               if length > MAX_CHAR_LENGTH else
               f"char({length}) = {byte_len} bytes over CHAR's {CHAR_OCTET_LIMIT}-byte limit")
        # VARCHAR keeps CHAR's semantics (same comparisons, still indexable)
        # and is legal to 65535 bytes; TEXT only when it would not fit inline.
        if byte_len <= INNODB_INLINE_LIMIT and byte_len <= MAX_VARCHAR_BYTES:
            return f"varchar({length})", why
        return text_type(byte_len), why

    # varchar / nvarchar
    byte_len = length * CHARSET_MAXLEN
    if byte_len <= MAX_VARCHAR_BYTES and byte_len <= INNODB_INLINE_LIMIT:
        return type_name, None
    if byte_len > MAX_VARCHAR_BYTES:
        return text_type(byte_len), (f"varchar({length}) = {byte_len} utf8mb4 bytes over the "
                                     f"{MAX_VARCHAR_BYTES}-byte VARCHAR limit")
    return text_type(byte_len), (f"varchar({length}) = {byte_len} utf8mb4 bytes over InnoDB's "
                                 f"~{INNODB_INLINE_LIMIT}-byte inline row budget")


def make_ddl_portable(body: str) -> tuple[str, list[tuple[str, str, str]]]:
    """Rewrite one CREATE TABLE body (line oriented) to portable types."""
    changes: list[tuple[str, str, str]] = []
    out: list[str] = []

    for line in body.split("\n"):
        m = _COLUMN_DEF_RE.match(line)
        if not m:
            out.append(line)
            continue
        name, old_type = m.group("name"), f"{m.group('type')}({m.group('len')})"
        new_type, reason = portable_column_type(name, m.group("type"), int(m.group("len")))
        if reason is None:
            out.append(line)
            continue
        changes.append((name, old_type, f"{new_type} ({reason})"))
        out.append(f"{m.group('head')}{new_type}{m.group('tail')}")

    return "\n".join(out), changes


def make_schema_portable(sql: str) -> tuple[str, dict[str, list[tuple[str, str, str]]]]:
    """Rewrite every CREATE TABLE in a schema script; report changes per table.

    Line oriented on purpose: it works on a single statement (converter) and on
    a whole committed file (repair tool) alike, and it never has to guess where
    a CREATE TABLE body ends - the nested "PRIMARY KEY\\n(\\n `id`\\n)" blocks in
    the converted artifacts make whole-statement regexes unreliable.
    """
    report: dict[str, list[tuple[str, str, str]]] = {}
    out: list[str] = []
    table = "<none>"

    for line in sql.split("\n"):
        head = _CREATE_TABLE_RE.match(line)
        if head:
            table = head.group("name")
            out.append(line)
            continue

        m = _COLUMN_DEF_RE.match(line)
        if not m:
            out.append(line)
            continue

        name, old_type = m.group("name"), f"{m.group('type')}({m.group('len')})"
        new_type, reason = portable_column_type(name, m.group("type"), int(m.group("len")))
        if reason is None:
            out.append(line)
            continue

        report.setdefault(table, []).append((name, old_type, f"{new_type} ({reason})"))
        out.append(f"{m.group('head')}{new_type}{m.group('tail')}")

    return "\n".join(out), report


# --------------------------------------------------------------------------
# Type syntax
# --------------------------------------------------------------------------
# MariaDB parses the display width *before* UNSIGNED: `TINYINT(1) UNSIGNED` is
# legal, `TINYINT UNSIGNED(1)` is ERROR 1064 and aborts the whole CREATE TABLE.
# The converter's rule table produced exactly that once (bit -> TINYINT(1), then
# the tinyint rule re-matched its own earlier output), so MAIL_BOX died at
# statement 62 of 320 and nothing after it was created. A gate that only checks
# *known* type names therefore has to check their spelling too.
_NUMERIC_TYPE_WORDS = (
    r"bigint|bit|dec|decimal|double(?:[ \t]+precision)?|fixed|float|int"
    r"|integer|mediumint|middleint|numeric|real|smallint|tinyint")
_OTHER_TYPE_WORDS = (
    r"binary|blob|bool|boolean|char(?:acter)?(?:[ \t]+varying)?|date|datetime"
    r"|geometry|json|longblob|longtext|mediumblob|mediumtext|set|text|time"
    r"|timestamp|tinyblob|tinytext|varbinary|varchar|year")
_TYPE_LENGTH = r"(?:[ \t]*\([ \t]*\d+(?:[ \t]*,[ \t]*\d+)?[ \t]*\))?"

LEGAL_TYPE_RE = re.compile(
    rf"(?i)^(?:(?:{_NUMERIC_TYPE_WORDS}){_TYPE_LENGTH}"
    rf"(?:[ \t]+unsigned)?(?:[ \t]+zerofill)?"
    rf"|(?:{_OTHER_TYPE_WORDS}){_TYPE_LENGTH}"
    rf"|enum[ \t]*\([^)]*\)|set[ \t]*\([^)]*\))$")

# UNSIGNED/ZEROFILL are numeric-only attributes, so the repair below never
# touches a character or LOB column: moving the modifier there would only
# produce a different kind of syntax error.
_MISPLACED_MODIFIER_RE = re.compile(
    rf"(?im)^(?P<head>[ \t]*`?[A-Za-z_][A-Za-z0-9_$]*`?[ \t]+)"
    rf"(?P<type>{_NUMERIC_TYPE_WORDS})"
    rf"(?P<mods>(?:[ \t]+(?:unsigned|zerofill))+)"
    rf"(?P<len>[ \t]*\([ \t]*\d+(?:[ \t]*,[ \t]*\d+)?[ \t]*\))"
    rf"(?P<tail>.*)$")


def is_legal_type(decl: str) -> bool:
    """True when a declared column type is a spelling MariaDB can parse."""
    return LEGAL_TYPE_RE.match(re.sub(r"\s+", " ", decl).strip()) is not None


def normalize_type_syntax(sql: str) -> tuple[str, list[tuple[str, str]]]:
    """Move UNSIGNED/ZEROFILL behind the display width where they belong.

    Purely syntactic and lossless - it keeps both the width and the modifier,
    it only puts them in the order MariaDB accepts.
    """
    fixed: list[tuple[str, str]] = []

    def repl(m: re.Match[str]) -> str:
        old = f"{m.group('type')}{m.group('mods')}{m.group('len')}"
        new = f"{m.group('type')}{m.group('len')}{m.group('mods')}"
        fixed.append((re.sub(r"\s+", " ", old).strip(), re.sub(r"\s+", " ", new).strip()))
        return f"{m.group('head')}{new}{m.group('tail')}"

    return _MISPLACED_MODIFIER_RE.sub(repl, sql), fixed


# --------------------------------------------------------------------------
# Identifier quoting
# --------------------------------------------------------------------------
# SSMS brackets every identifier. A column whose name is also a type keyword
# (SN_NEWS.`date` in the real dump) can arrive unquoted, which only works while
# the keyword happens not to be reserved. Quote it: same semantics, no luck
# involved, and identifier-based tooling can see the column.
_CONSTRAINT_WORDS = {
    "primary", "key", "unique", "index", "constraint", "foreign", "fulltext",
    "spatial", "check", "period", "with", "system", "engine",
}

_BARE_COLUMN_RE = re.compile(
    r"(?im)^(?P<indent>[ \t]+)(?P<name>[A-Za-z_][A-Za-z0-9_$]*)(?P<rest>[ \t]+[A-Za-z].*?)$"
)


def quote_bare_column_names(sql: str) -> tuple[str, list[str]]:
    """Backtick column definitions that were emitted without quotes."""
    quoted: list[str] = []

    def repl(m: re.Match[str]) -> str:
        name = m.group("name")
        if name.lower() in _CONSTRAINT_WORDS:
            return m.group(0)
        quoted.append(name)
        return f"{m.group('indent')}`{name}`{m.group('rest')}"

    return _BARE_COLUMN_RE.sub(repl, sql), quoted
