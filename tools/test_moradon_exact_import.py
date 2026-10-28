"""Independently compare the scoped import to the owner's current archive."""
import collections
import unittest
from audit_world_npcs import ROOT, load_tables
from build_moradon_import import source_rows, build, parse_values, corrected_name


class ExactImportTests(unittest.TestCase):
    def test_scoped_import_matches_archive_not_earlier_approximations(self):
        source=source_rows();db=load_tables()
        for kind in ('NPC','MONSTER'):
            db.execute(f'CREATE TABLE KO_MORADON_IMPORTED_{kind} AS SELECT * FROM K_{kind} WHERE 0')
        def snapshot(query):return collections.Counter(db.execute(query).fetchall())
        outside=snapshot('SELECT * FROM K_NPCPOS WHERE ZoneID<>21')
        objects_outside=snapshot('SELECT * FROM K_OBJECTPOS WHERE ZoneID<>21')
        npcs=snapshot('SELECT * FROM K_NPC');monsters=snapshot('SELECT * FROM K_MONSTER')
        sql=build()['src/KnightOnline.Database/World/MoradonExactImport.sql']
        self.assertEqual(sql,(ROOT/'src/KnightOnline.Database/World/MoradonExactImport.sql').read_text())
        db.executescript(sql)
        for table in ('K_NPCPOS','K_OBJECTPOS'):
            columns=[r[1] for r in db.execute('PRAGMA table_info('+table+')')]
            expected=collections.Counter(tuple(r.get(c,0) for c in columns) for r in source[table] if r['ZoneID']==21)
            self.assertEqual(snapshot('SELECT * FROM '+table+' WHERE ZoneID=21'),expected)
        self.assertEqual(outside,snapshot('SELECT * FROM K_NPCPOS WHERE ZoneID<>21'))
        self.assertEqual(objects_outside,snapshot('SELECT * FROM K_OBJECTPOS WHERE ZoneID<>21'))
        self.assertEqual(npcs,snapshot('SELECT * FROM K_NPC'));self.assertEqual(monsters,snapshot('SELECT * FROM K_MONSTER'))
        for table,private in [('K_NPC','KO_MORADON_IMPORTED_NPC'),('K_MONSTER','KO_MORADON_IMPORTED_MONSTER')]:
            columns=[r[1] for r in db.execute('PRAGMA table_info('+private+')')]
            original={r['sSid']:r for r in source[table]}
            for row in db.execute('SELECT * FROM '+private):
                values=dict(zip(columns,row));definition=original[values['sSid']]
                for c,value in definition.items():
                    if c in values and not (table=='K_MONSTER' and c=='sItem'):
                        expected_value=corrected_name(private,values['sSid'],values['sPid'],value) if c=='strName' else value
                        self.assertEqual(values[c],expected_value,(table,values['sSid'],c))
        self.assertEqual(db.execute('SELECT COUNT(*) FROM KO_MORADON_IMPORTED_NPC').fetchone()[0],39)
        self.assertEqual(db.execute('SELECT COUNT(*) FROM KO_MORADON_IMPORTED_MONSTER').fetchone()[0],37)
        self.assertEqual(db.execute('SELECT SUM(NumNPC) FROM K_NPCPOS WHERE ZoneID=21 AND ActType<100').fetchone()[0],375)
        self.assertEqual(db.execute('SELECT SUM(NumNPC) FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100').fetchone()[0],48)
        for row in source['K_NPCPOS']:
            if row['ZoneID']==21:self.assertTrue(all(0<=row[c]<1024 for c in ('LeftX','TopZ','RightX','BottomZ')))
        # No accounts, characters, item rows, original protocol or SQL-mode commands.
        self.assertNotIn('USERDATA',sql);self.assertNotIn('ACCOUNT',sql);self.assertNotIn('INSERT IGNORE',sql)

    def test_literal_parser_handles_source_escaping_without_executing_sql(self):
        self.assertEqual(parse_values("'O''Neil', 'a\\'b', NULL, 7E-05"),["O'Neil","a'b",None,.00007])


if __name__=='__main__':unittest.main()
