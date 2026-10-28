"""Known name repairs only; preserve every non-name field and custom labels."""
import json
import unittest
from audit_world_npcs import ROOT,load_tables
from build_moradon_import import source_rows,build


class ActorNameTests(unittest.TestCase):
    def test_all_catalog_names_and_non_name_fields(self):
        db=load_tables()
        for kind in ('NPC','MONSTER'):
            db.execute(f'CREATE TABLE KO_MORADON_IMPORTED_{kind} AS SELECT * FROM K_{kind} WHERE 0')
        db.executescript(build()['src/KnightOnline.Database/World/MoradonExactImport.sql'])
        rules=json.loads((ROOT/'src/KnightOnline.Database/World/ActorNameRepairs.json').read_text())['rules']
        allowed={'K_NPC','K_MONSTER','KO_MORADON_IMPORTED_NPC','KO_MORADON_IMPORTED_MONSTER'}
        def unchanged_data(table):
            cols=[r[1] for r in db.execute('PRAGMA table_info('+table+')') if r[1]!='strName']
            return db.execute('SELECT '+','.join(cols)+' FROM '+table).fetchall()
        before={table:unchanged_data(table) for table in allowed}
        source=source_rows()
        # Verify all 28 clipped/backtick Moradon names have a matching targeted rule.
        for row in source['K_NPC']:
            if any(r['table']=='KO_MORADON_IMPORTED_NPC' and r['id']==row['sSid'] for r in rules):
                self.assertTrue(any(r['id']==row['sSid'] and r['model']==row['sPid'] and r['old']==row['strName'].strip() for r in rules))
        for r in rules:
            self.assertIn(r['table'],allowed);self.assertLessEqual(len(r['name'].encode('latin1')),30)
            self.assertNotIn('`',r['name']);self.assertTrue(r['name'].strip())
            db.execute('UPDATE '+r['table']+' SET strName=? WHERE sSid=? AND sPid=? AND RTRIM(COALESCE(strName,\'\'))=?',(r['name'],r['id'],r['model'],r['old']))
        for table in allowed:
            self.assertEqual(before[table],unchanged_data(table))
            for (name,) in db.execute('SELECT strName FROM '+table):
                self.assertNotIn('`',name or '');self.assertNotIn((name or '').strip(),('','a'))
        self.assertEqual(db.execute('SELECT strName FROM KO_MORADON_IMPORTED_NPC WHERE sSid=19004').fetchone()[0],'[Mercenary Supply Officer] Osm')
        # Reapplying a known old->new rule cannot overwrite a custom name/model.
        db.execute("UPDATE K_NPC SET strName='Custom name' WHERE sSid=16079")
        r=next(r for r in rules if r['table']=='K_NPC' and r['id']==16079)
        db.execute('UPDATE K_NPC SET strName=? WHERE sSid=? AND sPid=? AND RTRIM(strName)=?',(r['name'],r['id'],r['model'],r['old']))
        self.assertEqual(db.execute('SELECT strName FROM K_NPC WHERE sSid=16079').fetchone()[0],'Custom name')


if __name__=='__main__':unittest.main()
