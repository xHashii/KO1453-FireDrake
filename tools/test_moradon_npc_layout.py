"""Compare the applied 1453 layout with the extracted donor coordinate manifest."""
import collections
import json
import math
import struct
import unittest
from audit_world_npcs import ROOT, load_tables


class MoradonLayoutTests(unittest.TestCase):
    def test_exact_placements_and_scope(self):
        db = load_tables()
        layout = json.loads((ROOT / 'docs/MORADON_NPC_PLACEMENTS.json').read_text())
        ids = {r['id'] for r in layout['spawns']}
        def snapshot(query):
            return collections.Counter(db.execute(query).fetchall())
        prototypes = snapshot('SELECT * FROM K_NPC')
        monsters = snapshot('SELECT * FROM K_NPCPOS WHERE ActType<100')
        outside = snapshot('SELECT * FROM K_NPCPOS WHERE ZoneID<>21')
        objects_outside = snapshot('SELECT * FROM K_OBJECTPOS WHERE ZoneID<>21')
        unmatched = snapshot('SELECT * FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100 AND NpcID NOT IN (' + ','.join(map(str, ids)) + ')')
        db.executescript((ROOT / 'src/KnightOnline.Database/World/MoradonNpcPlacements.sql').read_text())
        expected = collections.Counter((r['id'],r['x'],r['z'],r['right'],r['bottom'],r['direction'],r['count']) for r in layout['spawns'])
        actual = snapshot('SELECT NpcID,LeftX,TopZ,RightX,BottomZ,byDirection,NumNPC FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100 AND NpcID IN (' + ','.join(map(str, ids)) + ')')
        self.assertEqual(actual, expected)
        self.assertEqual(len(ids), 21)
        self.assertEqual(sum(actual.values()), 28)
        self.assertEqual(prototypes, snapshot('SELECT * FROM K_NPC'))
        self.assertEqual(monsters, snapshot('SELECT * FROM K_NPCPOS WHERE ActType<100'))
        self.assertEqual(outside, snapshot('SELECT * FROM K_NPCPOS WHERE ZoneID<>21'))
        self.assertEqual(objects_outside, snapshot('SELECT * FROM K_OBJECTPOS WHERE ZoneID<>21'))
        self.assertEqual(unmatched, snapshot('SELECT * FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100 AND NpcID NOT IN (' + ','.join(map(str, ids)) + ')'))
        self.assertEqual(db.execute('SELECT SUM(NumNPC) FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100').fetchone()[0], 44)
        for o in layout['objects']:
            row = db.execute('SELECT Type,ControlNpcID,PosX,PosY,PosZ FROM K_OBJECTPOS WHERE ZoneID=21 AND sIndex=?',(o['sIndex'],)).fetchone()
            self.assertEqual(row, (o['Type'],o['ControlNpcID'],o['PosX'],o['PosY'],o['PosZ']))
        # Check terrain availability at every placement, not just broad map bounds.
        with (ROOT / 'data/map/moradon_expanded.smd').open('rb') as f:
            size, step = struct.unpack('<if',f.read(8))
            heights = struct.unpack('<'+'f'*(size*size),f.read(size*size*4))
        for r in layout['spawns']:
            self.assertTrue(0 <= r['x'] < (size-1)*step and 0 <= r['z'] < (size-1)*step)
            self.assertTrue(math.isfinite(heights[int(r['z']/step)*size+int(r['x']/step)]))

    def test_remaining_legacy_npcs_all_move_without_population_changes(self):
        db = load_tables()
        db.executescript((ROOT / 'src/KnightOnline.Database/World/MoradonNpcPlacements.sql').read_text())
        layout = json.loads((ROOT / 'docs/MORADON_REMAINING_PLACEMENTS.json').read_text())
        sql = (ROOT / 'src/KnightOnline.Database/World/MoradonRemainingPlacements.sql').read_text()
        before = list(db.execute('SELECT * FROM K_NPCPOS WHERE ZoneID<>21 OR ActType<100'))
        total = db.execute('SELECT SUM(NumNPC) FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100').fetchone()
        db.executescript(sql)
        for r in layout['spawns']:
            old = db.execute('SELECT COUNT(*) FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100 AND NpcID=? AND LeftX=? AND TopZ=? AND RightX=? AND BottomZ=?', (r['id'],*r['old'])).fetchone()[0]
            self.assertEqual(old, 0)
            new = db.execute('SELECT NumNPC FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100 AND NpcID=? AND LeftX=? AND TopZ=?', (r['id'],r['x'],r['z'])).fetchall()
            self.assertEqual(new, [(1,)])
        self.assertEqual(len(layout['spawns']),16)
        self.assertEqual(total,db.execute('SELECT SUM(NumNPC) FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100').fetchone())
        self.assertEqual(before,list(db.execute('SELECT * FROM K_NPCPOS WHERE ZoneID<>21 OR ActType<100')))
        snapshot = list(db.execute('SELECT * FROM K_NPCPOS'))
        db.executescript(sql)
        self.assertEqual(snapshot,list(db.execute('SELECT * FROM K_NPCPOS')))

    def test_donor_anchored_alignment_updates_v2_only(self):
        db = load_tables()
        for name in ('MoradonNpcPlacements','MoradonRemainingPlacements'):
            db.executescript((ROOT / f'src/KnightOnline.Database/World/{name}.sql').read_text())
        layout = json.loads((ROOT / 'docs/MORADON_DONOR_ALIGNMENT.json').read_text())
        ids = '521,11020,13003,16073,16074,21020'
        scope = f'ZoneID<>21 OR ActType<100 OR NpcID NOT IN ({ids})'
        before = list(db.execute(f'SELECT * FROM K_NPCPOS WHERE {scope}'))
        sql = (ROOT / 'src/KnightOnline.Database/World/MoradonDonorAlignment.sql').read_text()
        db.executescript(sql)
        for r in layout['spawns']:
            row = db.execute('SELECT LeftX,TopZ,RightX,BottomZ,byDirection,NumNPC FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100 AND NpcID=? AND LeftX=? AND TopZ=?',(r['id'],r['x'],r['z'])).fetchall()
            self.assertEqual(row,[(r['x'],r['z'],r['x'],r['z'],r['direction'],1)])
        self.assertEqual(before,list(db.execute(f'SELECT * FROM K_NPCPOS WHERE {scope}')))
        donor = json.loads((ROOT / 'docs/MORADON_NPC_PLACEMENTS.json').read_text())
        for r in layout['spawns']:
            if r['id'] in (11020,21020):
                reference_id = 11021 if r['id']==11020 else 21021
                anchors = [a for a in donor['spawns'] if a['id']==reference_id]
                self.assertTrue(any(a['x']==r['x'] and r['z']-a['z'] in (4,8,12) and a['direction']==r['direction'] for a in anchors))
        potion = next(a for a in donor['spawns'] if a['id']==13005 and a['x']==855)
        clepio = next(r for r in layout['spawns'] if r['id']==13003)
        self.assertEqual((clepio['x'],clepio['z'],clepio['direction']),(potion['x']-5,potion['z'],potion['direction']))
        snapshot = list(db.execute('SELECT * FROM K_NPCPOS'))
        db.executescript(sql)
        self.assertEqual(snapshot,list(db.execute('SELECT * FROM K_NPCPOS')))


if __name__ == '__main__':
    unittest.main()
