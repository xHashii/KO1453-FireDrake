"""Verify the embedded missing-only starter repair against the shipped source."""
import pathlib
import re
import sqlite3
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class StarterItemRepairTests(unittest.TestCase):
    def test_seed_is_authentic_missing_only_and_repeatable(self):
        source = (ROOT / 'sql/mysql/kn_online/0002_data.sql').read_text()
        repairs = (ROOT / 'src/KnightOnline.Database/Items/StarterItemRepair.sql').read_text()
        db = sqlite3.connect(':memory:')
        rows = [line for line in repairs.splitlines() if line.startswith('INSERT INTO')]
        self.assertGreater(len(rows), 3)
        columns = re.search(r'`ITEM` (\(.*?\)) SELECT', rows[0])[1]
        db.execute('CREATE TABLE ITEM ' + columns)
        for row in rows:
            match = re.fullmatch(r'INSERT INTO `ITEM` (\(.*?\)) SELECT (.*) WHERE NOT EXISTS \(SELECT 1 FROM `ITEM` WHERE `Num` = (\d+)\);', row)
            self.assertIsNotNone(match)
            self.assertIn(f'INSERT `ITEM` {match[1]} VALUES ({match[2]});', source)
            db.execute(row)
        count = db.execute('SELECT COUNT(*) FROM ITEM').fetchone()[0]
        for item in (330620433, 320310413, 340610373):
            self.assertEqual(db.execute('SELECT COUNT(*) FROM ITEM WHERE Num=?', (item,)).fetchone()[0], 1)
        db.execute("UPDATE ITEM SET strName='custom definition' WHERE Num=330620433")
        for row in rows:
            db.execute(row)
        self.assertEqual(db.execute('SELECT COUNT(*) FROM ITEM').fetchone()[0], count)
        self.assertEqual(db.execute('SELECT strName FROM ITEM WHERE Num=330620433').fetchone()[0], 'custom definition')


if __name__ == '__main__':
    unittest.main()
