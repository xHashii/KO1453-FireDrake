#!/usr/bin/env python3
"""Regression tests for the SQL Server -> MariaDB conversion tooling.

    python3 tools/test_converter.py
    python3 -m unittest discover -s tools -p 'test_*.py'

Every case here is a failure that actually reached a running server:

  * ``[bit]`` columns came out as ``TINYINT UNSIGNED(1)`` because the type
    rules were applied one after another and the ``tinyint`` rule re-matched
    the ``bit`` rule's own output.  MariaDB wants the display width *before*
    UNSIGNED, so ``CREATE TABLE MAIL_BOX`` died with ERROR 1064 at statement
    62 of 320 and nothing after it was created.
  * CHAR/BINARY columns over 255 bytes died with ERROR 1074 (max = 255).
  * Packed binary data smuggled into quoted string literals was silently
    re-encoded as text and poisoned VARBINARY/BLOB columns.
  * ``SN_NEWS.[date]`` survived conversion unquoted, which only works while
    the name happens not to be reserved.

The end-to-end case additionally pushes converter output through
check_migrations.py, so "the tools agree with each other" is tested too.
"""

from __future__ import annotations

import contextlib
import io
import os
import re
import shutil
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import check_migrations  # noqa: E402
import mariadb_portability as portability  # noqa: E402
import mssql_to_mariadb as converter  # noqa: E402

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
COMMITTED_SCHEMA = os.path.join(REPO_ROOT, "sql", "mysql", "kn_online", "0001_schema.sql")


def converted_type(source_type: str) -> str:
    """Run one MSSQL column type through CREATE TABLE conversion."""
    ddl = f"CREATE TABLE [dbo].[T](\n\t[c] {source_type} NOT NULL\n) ON [PRIMARY]"
    # The converter reports every portability change on stderr; keep it out of
    # the test output (the change itself is asserted by the caller).
    with contextlib.redirect_stderr(io.StringIO()):
        out = converter.convert_create_table(ddl)
    match = re.search(r"`c`\s+(.+?)\s+NOT NULL", out, re.DOTALL)
    if not match:  # pragma: no cover - makes a regression obvious
        raise AssertionError(f"column vanished from converted DDL:\n{out}")
    return match.group(1).strip()


# --------------------------------------------------------------------------
# Type mapping
# --------------------------------------------------------------------------
#: source spelling -> expected MariaDB spelling (one entry per TYPE_RULES rule,
#: plus the combinations that used to cascade into each other).
TYPE_CASES = [
    ("[bit]", "TINYINT(1)"),
    ("[tinyint]", "TINYINT UNSIGNED"),
    ("[smallint]", "smallint"),
    ("[int]", "int"),
    ("[bigint]", "bigint"),
    ("[nvarchar](50)", "VARCHAR(50)"),
    ("[nvarchar](max)", "TEXT"),
    ("[varchar](max)", "TEXT"),
    ("[nchar](10)", "CHAR(10)"),
    ("[ntext]", "TEXT"),
    ("[image]", "MEDIUMBLOB"),
    ("[varbinary](max)", "MEDIUMBLOB"),
    ("[binary](max)", "MEDIUMBLOB"),
    ("[datetime]", "datetime"),
    ("[datetime2]", "DATETIME"),
    ("[datetime2](3)", "DATETIME(6)"),
    ("[datetimeoffset]", "DATETIME"),
    ("[datetimeoffset](7)", "DATETIME"),
    ("[smalldatetime]", "DATETIME"),
    ("[money]", "DECIMAL(19,4)"),
    ("[smallmoney]", "DECIMAL(10,4)"),
    ("[uniqueidentifier]", "CHAR(36)"),
    ("[real]", "FLOAT"),
    ("[float](53)", "DOUBLE"),
    ("[sql_variant]", "TEXT"),
    ("[xml]", "TEXT"),
    ("[geography]", "TEXT"),
    ("[geometry]", "TEXT"),
    # already MariaDB types must pass through untouched
    ("[varchar](21)", "varchar(21)"),
    ("[decimal](19,4)", "decimal(19,4)"),
]


class TypeMappingTest(unittest.TestCase):
    def test_every_rule_maps_to_its_expected_type(self) -> None:
        for source, expected in TYPE_CASES:
            with self.subTest(source=source):
                self.assertEqual(expected, converted_type(source))

    def test_bit_output_is_not_rewritten_by_the_tinyint_rule(self) -> None:
        """The cascade: bit -> TINYINT(1) -> TINYINT UNSIGNED(1) (ERROR 1064)."""
        with contextlib.redirect_stderr(io.StringIO()):
            ddl = converter.convert_create_table(
                "CREATE TABLE [dbo].[MAIL_BOX](\n"
            "\t[nLetterID] [int] IDENTITY(1,1) NOT NULL,\n"
                "\t[bDeleted] [bit] NOT NULL,\n"
                "\t[byType] [tinyint] NOT NULL,\n"
                " CONSTRAINT [PK_MAIL_BOX] PRIMARY KEY CLUSTERED \n(\n\t[nLetterID] ASC\n)\n"
                ") ON [PRIMARY]")
        self.assertIn("`bDeleted` TINYINT(1) NOT NULL", ddl)
        self.assertIn("`byType` TINYINT UNSIGNED NOT NULL", ddl)
        self.assertNotRegex(ddl, r"UNSIGNED\s*\(", "modifier ended up before the width")
        self.assertTrue(portability.is_legal_type("TINYINT(1)"))

    def test_a_table_using_every_rule_yields_only_legal_types(self) -> None:
        """The shipped regression, end to end: all rules in one CREATE TABLE.

        Feeding converted text back through convert_types() is not a supported
        operation (the tinyint rule exists to rewrite SQL Server's 0..255
        tinyint), so the invariant that matters is the one the gate enforces:
        one pass over a real dump produces only types MariaDB can parse.
        """
        columns = [f"\t[c{i}] {source} NULL" for i, (source, _) in enumerate(TYPE_CASES)]
        ddl = ("CREATE TABLE [dbo].[EveryRule](\n" + ",\n".join(columns) +
               "\n) ON [PRIMARY]")
        with contextlib.redirect_stderr(io.StringIO()):
            out = converter.convert_create_table(ddl)
        self.assertNotRegex(out, r"UNSIGNED\s*\(", "modifier before the display width")
        rep = check_migrations.Report()
        check_migrations.check_schema_file("every_rule.sql", out, rep)
        self.assertEqual([], rep.errors)

    def test_bit_and_tinyint_coexist_in_one_pass(self) -> None:
        """[bit] -> TINYINT(1) must not be picked up again by the tinyint rule."""
        out = converter.convert_types("`a` bit NOT NULL, `b` tinyint NOT NULL")
        self.assertEqual("`a` TINYINT(1) NOT NULL, `b` TINYINT UNSIGNED NOT NULL", out)

    def test_rule_local_alternation_does_not_leak(self) -> None:
        # geography|geometry is one rule; without grouping, the bare `geometry`
        # alternative would apply to the rest of the statement.
        self.assertEqual("TEXT", converted_type("[geometry]"))
        self.assertEqual("TEXT", converted_type("[geography]"))

    def test_column_names_are_not_converted(self) -> None:
        with contextlib.redirect_stderr(io.StringIO()):
            ddl = converter.convert_create_table(
                "CREATE TABLE [dbo].[T](\n"
                "\t[nBitCount] [int] NOT NULL,\n"
                "\t[strMoney] [varchar](20) NOT NULL,\n"
                "\t[bDeleted] [bit] NOT NULL\n) ON [PRIMARY]")
        self.assertIn("`nBitCount` int", ddl)
        self.assertIn("`strMoney` varchar(20)", ddl)
        self.assertIn("`bDeleted` TINYINT(1)", ddl)

    def test_reserved_word_column_is_quoted(self) -> None:
        """SN_NEWS.date: a bare column name only works while it stays unreserved."""
        with contextlib.redirect_stderr(io.StringIO()):
            ddl = converter.convert_create_table(
                "CREATE TABLE [dbo].[SN_NEWS](\n"
            "\t[id] [int] NOT NULL,\n"
            "\t[haber] [nvarchar](100) NULL,\n"
                "\t[date] [datetime] NULL\n) ON [PRIMARY]")
        self.assertIn("`date`", ddl)
        self.assertNotRegex(ddl, r"(?m)^\s+date\s")


# --------------------------------------------------------------------------
# Portability (MariaDB length limits)
# --------------------------------------------------------------------------
class PortabilityTest(unittest.TestCase):
    def test_oversized_binary_becomes_blob(self) -> None:
        # ERROR 1074: CHAR/BINARY are capped at 255 bytes on every engine.
        self.assertEqual("blob", converted_type("[varbinary](600)"))
        self.assertEqual("blob", converted_type("[varbinary](1536)"))
        self.assertEqual("varbinary(255)", converted_type("[varbinary](255)"))

    def test_oversized_char_becomes_varchar_or_text(self) -> None:
        self.assertEqual("varchar(300)", converted_type("[char](300)"))
        self.assertEqual("text", converted_type("[varchar](4096)"))
        self.assertEqual("varchar(50)", converted_type("[varchar](50)"))
        self.assertEqual("char(21)", converted_type("[char](21)"))

    def test_documented_binary_columns_stay_binary(self) -> None:
        # USERDATA.strSkill holds packed bytes even though the dump says varchar;
        # utf8mb4 would mangle them, so the name is forced to a binary type.
        with contextlib.redirect_stderr(io.StringIO()):
            ddl = converter.convert_create_table(
                "CREATE TABLE [dbo].[USERDATA](\n\t[strSkill] [varchar](10) NULL\n) ON [PRIMARY]")
        self.assertIn("`strSkill` varbinary(10)", ddl)

    def test_normalize_type_syntax_moves_the_modifier(self) -> None:
        broken = "CREATE TABLE `T`(\n `bDeleted` TINYINT UNSIGNED(1) NOT NULL,\n `n` INT UNSIGNED(11) NULL\n)"
        fixed, changes = portability.normalize_type_syntax(broken)
        self.assertEqual(["TINYINT UNSIGNED(1)", "INT UNSIGNED(11)"], [old for old, _ in changes])
        self.assertEqual(["TINYINT(1) UNSIGNED", "INT(11) UNSIGNED"], [new for _, new in changes])
        again, more = portability.normalize_type_syntax(fixed)
        self.assertEqual(fixed, again)
        self.assertEqual([], more)

    def test_normalize_leaves_correct_and_character_types_alone(self) -> None:
        untouched = ("CREATE TABLE `T`(\n `a` bigint(20) unsigned NULL,\n"
                     " `b` varchar(21) NOT NULL,\n `c` DATETIME NULL\n)\n"
                     "INSERT INTO `T` (`a`) VALUES (1);\nSET FOREIGN_KEY_CHECKS=0;")
        fixed, changes = portability.normalize_type_syntax(untouched)
        self.assertEqual([], changes)
        self.assertEqual(untouched, fixed)


class TypeSyntaxTest(unittest.TestCase):
    LEGAL = [
        "SMALLINT", "INT", "BIGINT", "TINYINT UNSIGNED", "TINYINT(1)", "TINYINT(1) UNSIGNED",
        "int(11) unsigned", "CHAR(21)", "VARCHAR(50)", "DATETIME", "FLOAT", "TEXT", "BLOB",
        "MEDIUMBLOB", "VARBINARY(10)", "DECIMAL(19,4)", "double precision", "timestamp",
        "year", "mediumint", "boolean", "longtext", "tinyblob", "json",
        "BIGINT(20) UNSIGNED ZEROFILL", "character varying(30)",
    ]
    ILLEGAL = [
        "TINYINT UNSIGNED(1)", "INT UNSIGNED(11)", "BIGINT UNSIGNED(20)", "BIT UNSIGNED(1)",
        "DECIMAL UNSIGNED(19,4)",          # the width must come first
        "NVARCHAR(50)", "nvarchar(max)", "DATETIME2", "SQL_VARIANT", "MONEY",
        "UNIQUEIDENTIFIER", "ntext", "image",   # SQL Server spellings
        "VARCHAR(50) UNSIGNED",            # UNSIGNED is numeric-only
    ]

    def test_legal_spellings_are_accepted(self) -> None:
        for decl in self.LEGAL:
            with self.subTest(decl=decl):
                self.assertTrue(portability.is_legal_type(decl))

    def test_illegal_spellings_are_rejected(self) -> None:
        for decl in self.ILLEGAL:
            with self.subTest(decl=decl):
                self.assertFalse(portability.is_legal_type(decl))

    def test_checker_flags_the_unparsable_type(self) -> None:
        """check_migrations.py used to report 0 errors on this exact schema."""
        sql = ("CREATE TABLE `MAIL_BOX`(\n"
               " `nLetterID` int NOT NULL AUTO_INCREMENT,\n"
               " `bDeleted` TINYINT UNSIGNED(1) NOT NULL,\n"
               " PRIMARY KEY ( `nLetterID` )\n"
               ") ENGINE=InnoDB ROW_FORMAT=DYNAMIC;\n")
        rep = check_migrations.Report()
        check_migrations.check_schema_file("0001_schema.sql", sql, rep)
        self.assertEqual(1, len(rep.errors), rep.errors)
        self.assertIn("bDeleted", rep.errors[0])
        self.assertIn("TINYINT UNSIGNED(1)", rep.errors[0])

    def test_deep_parse_catches_what_the_regexes_cannot(self) -> None:
        """Optional layer: a real MySQL grammar instead of a type whitelist."""
        if check_migrations.sqlglot is None:
            self.skipTest("sqlglot is not installed (pip install sqlglot)")
        broken = ("CREATE TABLE `MAIL_BOX`(\n"
                  " `nLetterID` int NOT NULL AUTO_INCREMENT,\n"
                  " `bDeleted` TINYINT UNSIGNED(1) NOT NULL,\n"
                  " PRIMARY KEY ( `nLetterID` )\n"
                  ") ENGINE=InnoDB ROW_FORMAT=DYNAMIC;\n")
        rep = check_migrations.Report()
        self.assertEqual(1, check_migrations.deep_parse_ddl("0001_schema.sql", broken, rep))
        self.assertEqual(1, len(rep.errors), rep.errors)
        self.assertIn("MySQL parser rejects", rep.errors[0])

        rep = check_migrations.Report()
        repaired = broken.replace("TINYINT UNSIGNED(1)", "TINYINT(1)")
        self.assertEqual(1, check_migrations.deep_parse_ddl("0001_schema.sql", repaired, rep))
        self.assertEqual([], rep.errors)

    def test_deep_parse_ignores_dml_and_is_silent_without_sqlglot(self) -> None:
        rep = check_migrations.Report()
        dml = "INSERT INTO `T` (`a`) VALUES (1);\nUPDATE `T` SET `a` = 2;\n"
        self.assertEqual(0, check_migrations.deep_parse_ddl("0002_data.sql", dml, rep))
        self.assertEqual([], rep.errors)

    def test_checker_accepts_the_repaired_type(self) -> None:
        for decl in ("TINYINT(1)", "TINYINT(1) UNSIGNED"):
            sql = ("CREATE TABLE `MAIL_BOX`(\n"
                   " `nLetterID` int NOT NULL AUTO_INCREMENT,\n"
                   f" `bDeleted` {decl} NOT NULL,\n"
                   " PRIMARY KEY ( `nLetterID` )\n"
                   ") ENGINE=InnoDB ROW_FORMAT=DYNAMIC;\n")
            rep = check_migrations.Report()
            check_migrations.check_schema_file("0001_schema.sql", sql, rep)
            self.assertEqual([], rep.errors, decl)


# --------------------------------------------------------------------------
# Binary payloads inside quoted string literals
# --------------------------------------------------------------------------
class BinaryLiteralTest(unittest.TestCase):
    PAYLOAD = "\n\x12\xf6~\x01"  # bytes 0A 12 F6 7E 01, as a lossy text export

    def test_recovered_bytes_are_emitted_as_hex(self) -> None:
        stmt = (f"INSERT INTO `BEGINNER_ITEM2` (`nIndex`, `strItem`) "
                f"VALUES (1, '{self.PAYLOAD}')")
        out = converter.convert_binary_literals(stmt, "hex")
        self.assertIn("0x0A12F67E01", out)
        self.assertIn("VALUES (1, 0x0A12F67E01)", out)

    def test_unrecoverable_bytes_become_null_instead_of_mangled_text(self) -> None:
        stmt = ("INSERT INTO `S_ITEM` (`nIndex`, `strItem`) "
                "VALUES (1, '\ufffd\ufffd')")
        out = converter.convert_binary_literals(stmt, "hex")
        self.assertIn("VALUES (1, NULL)", out)

    def test_policy_null_never_emits_a_literal(self) -> None:
        stmt = (f"INSERT INTO `S_ITEM` (`nIndex`, `strItem`) "
                f"VALUES (1, '{self.PAYLOAD}')")
        self.assertIn("VALUES (1, NULL)", converter.convert_binary_literals(stmt, "null"))

    def test_text_literals_are_left_alone(self) -> None:
        stmt = ("INSERT INTO `TB_USER` (`strAccountID`, `strPasswd`) "
                "VALUES (N'player', N'Warrior   ')")
        self.assertEqual(stmt, converter.convert_binary_literals(stmt, "hex"))

    def test_semicolons_inside_literals_do_not_split_statements(self) -> None:
        batch = "INSERT `T` (`a`) VALUES (N'x;y');\nINSERT `T` (`a`) VALUES (N'z');"
        self.assertEqual(2, len([s for s in converter.split_statements(batch) if s.strip()]))


# --------------------------------------------------------------------------
# CAST: MariaDB's CAST vocabulary is much smaller than its column types
# --------------------------------------------------------------------------
class CastTest(unittest.TestCase):
    """SSMS wraps every numeric/decimal value in CAST(x AS Numeric(p, s)).

    NUMERIC is a fine *column* type in MariaDB and ERROR 1064 inside CAST(),
    which is what stopped 0003_data.sql at statement 69998 - one statement
    before the SERVERS row, and 600-odd before the VERSION rows the login
    server reads.
    """

    def dml(self, stmt: str) -> str:
        return converter.convert_dml(stmt)

    def test_literal_numeric_cast_is_unwrapped(self) -> None:
        out = self.dml("INSERT [dbo].[SERVERS] ([sNum], [sName]) "
                       "VALUES (CAST(1 AS Numeric(18, 0)), N'Heros');")
        self.assertEqual("INSERT `SERVERS` (`sNum`, `sName`) VALUES (1, 'Heros');", out)

    def test_hex_and_string_casts_are_unwrapped(self) -> None:
        self.assertIn("0x0A12", self.dml(
            "INSERT [dbo].[T] ([a]) VALUES (CAST(0x0A12 AS VarBinary(10)));"))
        self.assertIn("'2024-01-01'", self.dml(
            "INSERT [dbo].[T] ([a]) VALUES (CAST(N'2024-01-01' AS DateTime));"))

    def test_expression_casts_keep_the_cast_and_change_the_type(self) -> None:
        cases = [
            ("Numeric(18, 2)", "DECIMAL(18,2)"),
            ("Decimal(9, 4)", "DECIMAL(9,4)"),
            ("money", "DECIMAL(19,4)"),
            ("smallmoney", "DECIMAL(10,4)"),
            ("bigint", "SIGNED"),
            ("smallint", "SIGNED"),
            ("tinyint", "UNSIGNED"),
            ("bit", "UNSIGNED"),
            ("float", "DOUBLE"),
            ("nvarchar(50)", "CHAR(50)"),
            ("varchar(max)", "CHAR"),
            ("varbinary(12)", "BINARY(12)"),
            ("varbinary(max)", "BINARY"),
            ("uniqueidentifier", "CHAR(36)"),
            ("datetime2(3)", "DATETIME"),
            ("smalldatetime", "DATETIME"),
            ("xml", "CHAR"),
        ]
        for source, expected in cases:
            with self.subTest(source=source):
                out = self.dml(f"UPDATE [dbo].[T] SET [a] = CAST([b] AS {source});")
                self.assertEqual(f"UPDATE `T` SET `a` = CAST(`b` AS {expected});", out)
                self.assertEqual([], check_migrations.illegal_cast_types(out))

    def test_already_mariadb_cast_types_are_kept(self) -> None:
        for decl in ("DECIMAL(18,2)", "CHAR(30)", "BINARY(16)", "SIGNED", "UNSIGNED",
                     "DATE", "DATETIME", "TIME", "INT", "DOUBLE", "VARCHAR(20)"):
            with self.subTest(decl=decl):
                out = self.dml(f"UPDATE [dbo].[T] SET [a] = CAST([b] AS {decl});")
                self.assertEqual([], check_migrations.illegal_cast_types(out))

    def test_nested_expression_survives(self) -> None:
        out = self.dml("UPDATE [dbo].[T] SET [a] = CAST(DATE_ADD([b], INTERVAL 1 DAY) AS Date);")
        self.assertEqual("UPDATE `T` SET `a` = CAST(DATE_ADD(`b`, INTERVAL 1 DAY) AS DATE);", out)

    def test_text_inside_a_literal_is_never_rewritten(self) -> None:
        """NPC dialogue in this dump mentions money, brackets and CAST."""
        stmt = ("INSERT [dbo].[QUEST_TALK] ([nIndex], [strTalk]) VALUES "
                "(8740, 'Do you think money grows on trees? CAST(x AS Numeric) [Karus]');")
        self.assertEqual(
            "INSERT `QUEST_TALK` (`nIndex`, `strTalk`) VALUES "
            "(8740, 'Do you think money grows on trees? CAST(x AS Numeric) [Karus]');",
            self.dml(stmt))

    def test_checker_flags_the_tsql_cast_types(self) -> None:
        for bad in ("Numeric(18, 0)", "VarBinary(10)", "money", "bigint",
                    "nvarchar(50)", "datetime2", "VARCHAR"):
            with self.subTest(bad=bad):
                found = check_migrations.illegal_cast_types(
                    f"INSERT `T` (`a`) VALUES (CAST(`b` AS {bad}));")
                self.assertEqual(1, len(found), found)

    def test_checker_ignores_cast_in_a_literal(self) -> None:
        stmt = "INSERT `T` (`a`) VALUES ('a CAST(1 AS Numeric(18, 0)) in dialogue');"
        self.assertEqual([], check_migrations.illegal_cast_types(stmt))


# --------------------------------------------------------------------------
# End to end: a dump in, gate-clean migration scripts out
# --------------------------------------------------------------------------
DUMP = """USE [KN_Online]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MAIL_BOX](
\t[nLetterID] [int] IDENTITY(1,1) NOT NULL,
\t[bDeleted] [bit] NOT NULL,
\t[byType] [tinyint] NOT NULL,
\t[dtSendDate] [datetime] NULL,
\t[strSubject] [nvarchar](128) NULL,
\t[nCoins] [money] NULL,
\t[strSerial] [varbinary](600) NULL,
\t[nItemSerial] [varchar](586) NULL,
\t[tzOffset] [datetimeoffset](7) NULL,
 CONSTRAINT [PK_MAIL_BOX] PRIMARY KEY CLUSTERED 
(
\t[nLetterID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE TABLE [dbo].[SN_NEWS](
\t[id] [int] IDENTITY(1,1) NOT NULL,
\t[haber] [nvarchar](4096) NULL,
\t[date] [datetime] NULL,
 CONSTRAINT [PK_SN_NEWS] PRIMARY KEY CLUSTERED 
(
\t[id] ASC
) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE TABLE [dbo].[SERVERS](
\t[sNum] [numeric](18, 0) IDENTITY(1,1) NOT NULL,
\t[sName] [char](250) NULL,
\t[sIP] [char](15) NULL,
\t[DatabaseName] [char](100) NULL,
 CONSTRAINT [PK_SERVERS] PRIMARY KEY CLUSTERED 
(
\t[sNum] ASC
) ON [PRIMARY]
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[MAIL_BOX] ADD  CONSTRAINT [DF_MAIL_BOX_bDeleted]  DEFAULT ((0)) FOR [bDeleted]
GO
INSERT [dbo].[SERVERS] ([sNum], [sName], [sIP], [DatabaseName]) VALUES (CAST(1 AS Numeric(18, 0)), N'Heros                    ', N'127.0.0.1      ', N'Heros')
GO
INSERT [dbo].[MAIL_BOX] ([nLetterID], [bDeleted], [byType], [strSubject], [strSerial]) VALUES (1, 1, 2, N'hello', 0x0A12F67E01)
GO
INSERT [dbo].[SN_NEWS] ([id], [haber], [date]) VALUES (1, N'duyuru', CAST(N'2024-01-01' AS DATETIME))
GO
"""


class EndToEndTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.tmp = tempfile.mkdtemp(prefix="ko-converter-test-")
        cls.dump = os.path.join(cls.tmp, "dump.sql")
        cls.outdir = os.path.join(cls.tmp, "out")
        with open(cls.dump, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(DUMP)
        argv = sys.argv
        stdout, stderr = sys.stdout, sys.stderr
        sys.argv = ["mssql_to_mariadb.py", cls.dump, "--database", "KN_Online",
                    "-o", cls.outdir, "--binary-literals", "hex"]
        try:
            with contextlib.redirect_stdout(io.StringIO()) as out, \
                    contextlib.redirect_stderr(io.StringIO()) as err:
                cls.status = converter.main()
            cls.stdout, cls.stderr = out.getvalue(), err.getvalue()
        finally:
            sys.argv, sys.stdout, sys.stderr = argv, stdout, stderr

    @classmethod
    def tearDownClass(cls) -> None:
        shutil.rmtree(cls.tmp, ignore_errors=True)

    def schema(self) -> str:
        path = os.path.join(self.outdir, "KN_Online_01_schema.sql")
        self.assertTrue(os.path.exists(path), self.stdout + self.stderr)
        return open(path, encoding="utf-8").read()

    def test_conversion_succeeded(self) -> None:
        self.assertEqual(0, self.status)
        self.assertNotIn("WARNING: failed on statement", self.stderr)

    def test_converted_schema_passes_the_migration_gate(self) -> None:
        path = os.path.join(self.outdir, "KN_Online_01_schema.sql")
        rep = check_migrations.Report()
        tables = check_migrations.check_schema_file(path, self.schema(), rep)
        self.assertEqual([], rep.errors)
        self.assertEqual([], rep.warnings)
        self.assertIn("MAIL_BOX", tables)
        self.assertIn("bdeleted", tables["MAIL_BOX"])

    def test_converted_schema_has_no_unparsable_types(self) -> None:
        schema = self.schema()
        self.assertNotRegex(schema, r"UNSIGNED\s*\(")
        self.assertIn("`bDeleted` TINYINT(1)", schema)
        self.assertIn("`byType` TINYINT UNSIGNED", schema)
        self.assertIn("`strSerial` blob", schema)
        self.assertIn("`nItemSerial` blob", schema)
        self.assertIn("`haber` text", schema)
        self.assertIn("`date`", schema)

    def test_statements_split_the_way_the_migrator_splits_them(self) -> None:
        statements = [stmt for _, stmt in check_migrations.iter_statements(self.schema()) if stmt.strip()]
        self.assertTrue(any("CREATE TABLE `MAIL_BOX`" in s for s in statements))
        self.assertTrue(any(s.strip().upper().startswith("ALTER TABLE") for s in statements))

    def test_numeric_cast_is_unwrapped_in_the_data_file(self) -> None:
        """The statement that stranded 0003_data.sql at statement 69998."""
        path = os.path.join(self.outdir, "KN_Online_02_data.sql")
        data = open(path, encoding="utf-8").read()
        self.assertIn("INSERT `SERVERS` (`sNum`, `sName`, `sIP`, `DatabaseName`) "
                      "VALUES (1, 'Heros", data)
        self.assertNotIn("CAST(", data)
        self.assertEqual([], check_migrations.illegal_cast_types(data))

    def test_data_file_keeps_the_binary_literal(self) -> None:
        path = os.path.join(self.outdir, "KN_Online_02_data.sql")
        self.assertTrue(os.path.exists(path), self.stdout + self.stderr)
        data = open(path, encoding="utf-8").read()
        self.assertIn("0x0A12F67E01", data)
        self.assertNotRegex(data, r"(?i)\bGO\b\s*$")


class CommittedArtifactTest(unittest.TestCase):
    """The scripts the LoginServer actually runs must satisfy the gate."""

    @unittest.skipUnless(os.path.exists(COMMITTED_SCHEMA), "sql/mysql/kn_online not checked out")
    def test_committed_schema_is_clean(self) -> None:
        sql = open(COMMITTED_SCHEMA, encoding="utf-8").read()
        self.assertNotRegex(sql, r"UNSIGNED\s*\(", "modifier before the display width")
        rep = check_migrations.Report()
        check_migrations.check_schema_file("0001_schema.sql", sql, rep)
        self.assertEqual([], rep.errors)

    @unittest.skipUnless(os.path.exists(COMMITTED_SCHEMA), "sql/mysql/kn_online not checked out")
    def test_committed_schema_is_a_fixpoint(self) -> None:
        """--fix must not want to change an artifact that is already committed."""
        sql = open(COMMITTED_SCHEMA, encoding="utf-8").read()
        fixed, changes = portability.make_schema_portable(sql)
        fixed, reordered = portability.normalize_type_syntax(fixed)
        fixed, quoted = portability.quote_bare_column_names(fixed)
        self.assertEqual({}, changes)
        self.assertEqual([], reordered)
        self.assertEqual([], quoted)
        self.assertEqual(sql, fixed)


if __name__ == "__main__":
    unittest.main(verbosity=2)
