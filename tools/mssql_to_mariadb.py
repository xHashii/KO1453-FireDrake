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
  python3 mssql_to_mariadb.py KN_online.sql --database KO_GAME -o out/KO_GAME
  python3 mssql_to_mariadb.py KO_MAIN.sql   --database KO_MAIN -o out/KO_MAIN

Then either let the servers import automatically (DatabaseMigrator) or:
  mariadb -u root -p < out/KO_GAME/KO_GAME_01_schema.sql
  mariadb -u root -p < out/KO_GAME/KO_GAME_02_data.sql
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
        return None  # defaults are handled inline; standalone DF constraints skipped
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


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("input", help="SSMS-generated .sql dump")
    ap.add_argument("--database", required=True, help="Target MariaDB database name, e.g. KO_MAIN")
    ap.add_argument("-o", "--outdir", required=True, help="Output directory")
    ap.add_argument("--encoding", default=None, help="Force input encoding (default: auto-detect)")
    ap.add_argument("--chunk-mb", type=int, default=64, help="Split data output every N MB (default 64)")
    args = ap.parse_args()

    db = args.database
    os.makedirs(args.outdir, exist_ok=True)
    os.makedirs(os.path.join(args.outdir, "procedures_tsql"), exist_ok=True)

    encoding = detect_encoding(args.input, args.encoding)
    print(f"[converter] reading {args.input} as {encoding}")

    schema_path = os.path.join(args.outdir, f"{db}_01_schema.sql")
    schema = open(schema_path, "w", encoding="utf-8", newline="\n")
    schema.write("-- Auto-converted from SQL Server dump (mssql_to_mariadb.py). Review before production use.\n")
    schema.write(f"CREATE DATABASE IF NOT EXISTS `{db}`;\nUSE `{db}`;\n")
    schema.write("SET FOREIGN_KEY_CHECKS=0;\nSET SQL_MODE='NO_AUTO_VALUE_ON_ZERO';\n\n")

    chunk_no = 2
    chunk_bytes = 0
    chunk_limit = args.chunk_mb * 1024 * 1024
    data_files = []

    def new_data_file():
        nonlocal chunk_no, chunk_bytes
        path = os.path.join(args.outdir, f"{db}_{chunk_no:02d}_data.sql")
        fh = open(path, "w", encoding="utf-8", newline="\n")
        fh.write(f"USE `{db}`;\nSET FOREIGN_KEY_CHECKS=0;\nSET SQL_MODE='NO_AUTO_VALUE_ON_ZERO';\n\n")
        data_files.append(path)
        chunk_no += 1
        chunk_bytes = 0
        return fh

    data = new_data_file()
    routines = []
    row_counts: dict[str, int] = {}
    skipped = converted = errors = 0

    insert_re = re.compile(r"(?is)^\s*INSERT\s+(?:INTO\s+)?(?:`dbo`\.)?`?([A-Za-z0-9_]+)`?")

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

            for stmt in split_semicolons(batch):
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
    print(f"[converter] outputs in {args.outdir}: {os.path.basename(schema_path)}, "
          f"{len(data_files)} data file(s), procedures_report.md, manifest.json")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
