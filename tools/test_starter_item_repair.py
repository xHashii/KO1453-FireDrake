"""Verify the embedded missing-only starter repair against the shipped source."""
import pathlib
import re
import sqlite3
import struct
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


    def test_known_unused_tail_signatures_exactly_match_shipped_data(self):
        known_items = set()
        for path in (ROOT / 'sql/mysql/kn_online').glob('*data.sql'):
            known_items.update(int(m[1]) for m in re.finditer(r'INSERT `ITEM` .*? VALUES \((\d+),', path.read_text()))
        expected = set()
        source = (ROOT / 'sql/mysql/kn_online/0002_data.sql').read_text()
        for line in source.splitlines():
            if not line.startswith('INSERT `BEGINNER_ITEM` '):
                continue
            blob = bytes.fromhex(re.findall(r'0x([0-9a-fA-F]+)', line)[0])
            for index in range(42, len(blob) // 8):
                item_id, durability, count = struct.unpack_from('<Ihh', blob, index * 8)
                if item_id and item_id not in known_items:
                    expected.add((index, item_id, durability, count))
        code = (ROOT / 'src/KnightOnline.Database/Items/LegacyStarterTail.cs').read_text()
        actual = {tuple(map(int, m)) for m in re.findall(r'\((\d+), (\d+)u, (-?\d+), (-?\d+)\)', code)}
        self.assertEqual(actual, expected)
        self.assertGreater(len(actual), 0)
        self.assertTrue(all(index >= 42 for index, *_ in actual))


if __name__ == '__main__':
    unittest.main()
