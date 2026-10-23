"""Data-backed regression tests for static NPCs and native gate sources."""
import struct
import unittest
from audit_world_npcs import ROOT, load_tables, warps


class WorldNpcTests(unittest.TestCase):
    def test_every_npc_spawn_has_prototype_zone_and_valid_rectangle(self):
        db = load_tables()
        zones = dict(db.execute('SELECT ZoneNo,strZoneName FROM ZONE_INFO'))
        prototypes = set(r[0] for r in db.execute('SELECT sSid FROM K_NPC'))
        count = 0
        for zone, sid, left, top, right, bottom, num in db.execute('SELECT ZoneID,NpcID,LeftX,TopZ,RightX,BottomZ,NumNPC FROM K_NPCPOS WHERE ActType>=100'):
            self.assertIn(sid, prototypes)
            # Some legacy spawn rows refer to maps not hosted by ZONE_INFO.
            if zone not in zones:
                continue
            with (ROOT / 'data/map' / zones[zone]).open('rb') as f:
                size, step = struct.unpack('<if', f.read(8))
            extent = (size - 1) * step
            self.assertTrue(all(0 <= v < extent for v in (left, top, right, bottom)), (zone, sid))
            self.assertGreaterEqual(num, 0)
            count += num
        self.assertEqual(count, 1436)

    def test_moradon_merchants_and_anvil(self):
        db = load_tables()
        self.assertEqual(db.execute('SELECT SUM(NumNPC) FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100').fetchone()[0], 57)
        merchants = db.execute('SELECT n.strName,n.byType,n.iSellingGroup FROM K_NPCPOS p JOIN K_NPC n ON n.sSid=p.NpcID WHERE p.ZoneID=21 AND p.ActType>=100 AND n.byType IN (21,22)').fetchall()
        self.assertEqual(len(merchants), 7)
        self.assertTrue(all(group > 0 for _, _, group in merchants))
        anvil = db.execute('SELECT o.sIndex,n.byType FROM K_OBJECTPOS o JOIN K_NPC n ON n.sSid=o.sIndex WHERE o.ZoneID=21 AND o.Type=8').fetchall()
        self.assertEqual(anvil, [(5001, 24)])

    def test_captured_moradon_gate(self):
        db = load_tables()
        # User's packet AD 0F D4 00: object index 4013, control/proxy 212.
        gate = db.execute('SELECT Type,ControlNpcID,Belong,PosX,PosZ FROM K_OBJECTPOS WHERE ZoneID=21 AND sIndex=4013').fetchone()
        self.assertEqual(gate[:3], (5, 212, 2))
        entries = warps(ROOT / 'data/map/moradon_20060124.smd')
        elmo = next(w for w in entries if w[0] == 2121)
        self.assertEqual(elmo, (2121, 2, 1595.0, 412.0, 5000, 2))
        self.assertEqual(len([w for w in entries if w[0] // 10 == gate[1]]), 5)

    def test_all_map_warp_records_parse(self):
        for path in (ROOT / 'data/map').glob('*.smd'):
            with self.subTest(map=path.name):
                for wid, zone, x, z, fee, nation in warps(path):
                    self.assertGreater(wid, 0)
                    self.assertGreater(zone, 0)
                    self.assertGreaterEqual(fee, 0)
                    self.assertIn(nation, (0, 1, 2))


if __name__ == '__main__':
    unittest.main()
