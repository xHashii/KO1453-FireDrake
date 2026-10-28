"""Expanded Moradon placements: exact supplied rows and auditable family adaptations."""
import collections
import json
import math
import struct
import unittest
from audit_world_npcs import ROOT, load_tables


class MoradonMonsterLayoutTests(unittest.TestCase):
    def test_layout_scope_population_and_all_hosted_map_bounds(self):
        db = load_tables()
        manifest = json.loads((ROOT / 'docs/MORADON_MONSTER_PLACEMENTS.json').read_text())
        def snapshot(query):
            return collections.Counter(db.execute(query).fetchall())
        untouched = snapshot('SELECT * FROM K_NPCPOS WHERE ZoneID<>21 OR ActType>=100')
        npcs = snapshot('SELECT * FROM K_NPC')
        monsters = snapshot('SELECT * FROM K_MONSTER')
        objects = snapshot('SELECT * FROM K_OBJECTPOS')
        original = snapshot('SELECT * FROM K_NPCPOS WHERE ZoneID=21 AND ActType<100')
        original_ids = {r[0] for r in db.execute('SELECT NpcID FROM K_NPCPOS WHERE ZoneID=21 AND ActType<100')}
        columns = [r[1] for r in db.execute('PRAGMA table_info(K_NPCPOS)')]
        db.executescript((ROOT / 'src/KnightOnline.Database/World/MoradonMonsterPlacements.sql').read_text())
        expected = collections.Counter(tuple(r['spawn'][c] for c in columns) for r in manifest['spawns'])
        self.assertEqual(snapshot('SELECT * FROM K_NPCPOS WHERE ZoneID=21 AND ActType<100'), expected)
        self.assertEqual(untouched,snapshot('SELECT * FROM K_NPCPOS WHERE ZoneID<>21 OR ActType>=100'))
        self.assertEqual(npcs,snapshot('SELECT * FROM K_NPC'))
        self.assertEqual(monsters,snapshot('SELECT * FROM K_MONSTER'))
        self.assertEqual(objects,snapshot('SELECT * FROM K_OBJECTPOS'))
        self.assertEqual((manifest['exactRows'],manifest['exactPopulation']),(161,457))
        self.assertEqual((manifest['adaptedRows'],manifest['adaptedPopulation']),(34,189))
        self.assertEqual(sum(r['spawn']['NumNPC'] for r in manifest['spawns']),646)
        self.assertTrue(original_ids <= {r['spawn']['NpcID'] for r in manifest['spawns']})
        # Each preserved variant keeps its old row's population, respawn and identity.
        exact = [r['spawn'] for r in manifest['spawns'] if r['kind']=='exact']
        for r in manifest['spawns']:
            if r['kind']!='adapted':
                continue
            before,after = r['original'],r['spawn']
            self.assertIn(tuple(before[c] for c in columns),original)
            self.assertEqual([before[c] for c in ('NpcID','NumNPC','RegTime','ActType')],
                             [after[c] for c in ('NpcID','NumNPC','RegTime','ActType')])
            anchor = next(a for a in exact if a['NpcID']==r['anchorId'] and
                          [a[c] for c in ('LeftX','TopZ','RightX','BottomZ')]==r['anchorRectangle'])
            for c,offset in [('LeftX',r['offset'][0]),('RightX',r['offset'][0]),('TopZ',r['offset'][1]),('BottomZ',r['offset'][1])]:
                self.assertEqual(after[c],anchor[c]+offset)
        # Audit every hosted zone, not just Moradon; non-hosted legacy rows stay untouched.
        zones = dict(db.execute('SELECT ZoneNo,strZoneName FROM ZONE_INFO'))
        zones[21] = 'moradon_expanded.smd'
        prototypes = {r[0] for r in db.execute('SELECT sSid FROM K_MONSTER')}
        maps = {}
        for zone,sid,left,top,right,bottom,count in db.execute('SELECT ZoneID,NpcID,LeftX,TopZ,RightX,BottomZ,NumNPC FROM K_NPCPOS WHERE ActType<100'):
            self.assertIn(sid,prototypes)
            if zone not in zones:
                continue
            name=zones[zone]
            if name not in maps:
                with (ROOT / 'data/map' / name).open('rb') as f:
                    size,step=struct.unpack('<if',f.read(8))
                    heights=struct.unpack('<'+'f'*(size*size),f.read(size*size*4))
                maps[name]=(size,step,heights)
            size,step,heights=maps[name]
            self.assertTrue(all(0<=v<(size-1)*step for v in (left,top,right,bottom)),(zone,sid))
            for i in range(count):
                u=.5 if count==1 else (i+.5)/count
                v=.5 if count==1 else (i*.61803398875+.5)%1
                x=min(left,right)+abs(right-left)*u
                z=min(top,bottom)+abs(top-bottom)*v
                # SMD uses x-major samples. WorldCatalog performs triangle interpolation.
                ix,iz=int(x/step),int(z/step)
                self.assertTrue(all(math.isfinite(heights[(ix+dx)*size+iz+dz]) for dx,dz in ((0,0),(1,0),(0,1),(1,1))))


if __name__=='__main__':
    unittest.main()
