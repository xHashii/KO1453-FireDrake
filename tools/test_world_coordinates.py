"""Audit shipped zone IDs/coordinates against SMD bounds; test missing-only repair.
No MariaDB/.NET needed. SQLite executes the same UPDATE predicate/assignments.
Bounds checks do not establish terrain walkability or validate client assets.
"""
import pathlib
import re
import sqlite3
import struct
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


def load_world():
    db = sqlite3.connect(':memory:')
    db.create_function('IF', 3, lambda test, yes, no: yes if test else no)
    source = (ROOT / 'sql/mysql/kn_online/0003_data.sql').read_text()
    for table in ('ZONE_INFO', 'START_POSITION'):
        rows = [line for line in source.splitlines() if line.startswith(f'INSERT `{table}` ')]
        columns = re.search(r'(\(.*?\)) VALUES', rows[0])[1]
        db.execute(f'CREATE TABLE {table} {columns}')
        for row in rows:
            db.execute(row.replace('INSERT `', 'INSERT INTO `', 1))
    return db


def repair(db):
    sql = (ROOT / 'sql/mysql/kn_online/0009_zone_start_points.sql').read_text()
    sql = '\n'.join(line for line in sql.splitlines() if not line.startswith('--'))
    match = re.fullmatch(r'\s*UPDATE `ZONE_INFO` z\s+JOIN `START_POSITION` s ON s.ZoneID = z.ZoneNo\s+SET (.*?)\s+WHERE (.*);\s*', sql, re.S)
    assert match, 'Update shape changed: adapt this test explicitly.'
    db.execute('UPDATE ZONE_INFO AS z SET ' + match[1].replace('z.InitX =', 'InitX =').replace('z.InitZ =', 'InitZ =')
               + ' FROM START_POSITION AS s WHERE s.ZoneID = z.ZoneNo AND ' + match[2])


class WorldCoordinateTests(unittest.TestCase):
    def test_all_shipped_maps_and_spawns(self):
        db = load_world()
        repair(db)
        zones = db.execute('SELECT ZoneNo, strZoneName, InitX, InitZ FROM ZONE_INFO').fetchall()
        self.assertEqual(len(zones), 31)
        for zone, name, px, pz in zones:
            with self.subTest(zone=zone, map=name):
                with (ROOT / 'data/map' / name).open('rb') as stream:
                    size, spacing = struct.unpack('<if', stream.read(8))
                extent = (size - 1) * spacing
                self.assertGreater(extent, 0)
                self.assertTrue(0 <= px / 100 <= extent)
                self.assertTrue(0 <= pz / 100 <= extent)
                start = db.execute('SELECT sKarusX,sKarusZ,sElmoradX,sElmoradZ FROM START_POSITION WHERE ZoneID=?', (zone,)).fetchone()
                if start:
                    self.assertTrue(all(0 <= value <= extent for value in start))
        self.assertEqual(db.execute('SELECT strZoneName FROM ZONE_INFO WHERE ZoneNo=21').fetchone()[0], 'moradon_20060124.smd')
        self.assertEqual(db.execute('SELECT InitX,InitZ FROM ZONE_INFO WHERE ZoneNo=12').fetchone(), (52700, 54300))
        snapshot = db.execute('SELECT * FROM ZONE_INFO').fetchall()
        repair(db)
        self.assertEqual(db.execute('SELECT * FROM ZONE_INFO').fetchall(), snapshot)

    def test_expanded_moradon_reference_and_plaza(self):
        db = load_world()
        before = db.execute('SELECT * FROM START_POSITION').fetchall()
        sql = (ROOT / 'sql/mysql/kn_online/0011_moradon_expanded.sql').read_text()
        db.executescript(sql)
        self.assertEqual(db.execute('SELECT strZoneName FROM ZONE_INFO WHERE ZoneNo=21').fetchone()[0], 'moradon_expanded.smd')
        with (ROOT / 'data/map/moradon_expanded.smd').open('rb') as stream:
            size, spacing = struct.unpack('<if', stream.read(8))
        self.assertEqual((size - 1) * spacing, 1024)
        self.assertTrue(816 < (size - 1) * spacing and 543 < (size - 1) * spacing)
        self.assertEqual(db.execute('SELECT * FROM START_POSITION').fetchall(), before)
        db.execute("UPDATE ZONE_INFO SET strZoneName='custom.smd' WHERE ZoneNo=21")
        db.executescript(sql)
        self.assertEqual(db.execute('SELECT strZoneName FROM ZONE_INFO WHERE ZoneNo=21').fetchone()[0], 'custom.smd')

    def test_preserves_custom_coordinates(self):
        db = load_world()
        db.execute('UPDATE ZONE_INFO SET InitX=55000, InitZ=54000 WHERE ZoneNo=12')
        repair(db)
        self.assertEqual(db.execute('SELECT InitX,InitZ FROM ZONE_INFO WHERE ZoneNo=12').fetchone(), (55000, 54000))


if __name__ == '__main__':
    unittest.main()
