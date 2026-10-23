"""Audit NPC/object references and SMD warp records from the shipped SQL/assets."""
import pathlib
import re
import sqlite3
import struct

ROOT = pathlib.Path(__file__).resolve().parents[1]


def load_tables():
    db = sqlite3.connect(':memory:')
    tables = ('ZONE_INFO', 'K_NPC', 'K_NPCPOS', 'K_OBJECTPOS')
    sql = (ROOT / 'sql/mysql/kn_online/0003_data.sql').read_text()
    for table in tables:
        rows = [l for l in sql.splitlines() if l.startswith(f'INSERT `{table}` ')]
        columns = re.search(r'(\(.*?\)) VALUES', rows[0])[1]
        db.execute(f'CREATE TABLE {table} {columns}')
        for row in rows:
            db.execute(row.replace('INSERT `', 'INSERT INTO `', 1))
    return db


def warps(path):
    with path.open('rb') as f:
        def number(): return struct.unpack('<i', f.read(4))[0]
        def skip(n):
            assert n >= 0
            f.seek(n, 1)
        size = number()
        unit = struct.unpack('<f', f.read(4))[0]
        skip(size * size * 4)
        width, length, faces = struct.unpack('<ffi', f.read(12))
        assert width == length == (size - 1) * unit
        skip(faces * 36)
        for _ in range(round(width / 16) * round(length / 16)):
            if number():
                skip(number() * 2)
                for _ in range(16): skip(number() * 12)
        skip(number() * 24)  # object events (DB is authoritative)
        skip(size * size * 2)
        skip(number() * 20)  # regeneration rectangles
        count = number()
        assert 0 <= count < 10000
        result = []
        for _ in range(count):
            b = f.read(320)
            if len(b) < 320: break  # original loader permits a truncated final warp
            wid = struct.unpack_from('<h', b)[0]
            pay, zone = struct.unpack_from('<Ih', b, 292)
            x, y, z, radius, nation = struct.unpack_from('<ffffh', b, 300)
            if wid: result.append((wid, zone, x, z, pay, nation))
        return result


if __name__ == '__main__':
    db = load_tables()
    for zone, name in db.execute('SELECT ZoneNo,strZoneName FROM ZONE_INFO'):
        npc = db.execute('SELECT COALESCE(SUM(p.NumNPC),0) FROM K_NPCPOS p JOIN K_NPC n ON n.sSid=p.NpcID WHERE p.ZoneID=? AND p.ActType>=100', (zone,)).fetchone()[0]
        objects = db.execute('SELECT COUNT(*) FROM K_OBJECTPOS WHERE ZoneID=?', (zone,)).fetchone()[0]
        entries = warps(ROOT / 'data/map' / name)
        print(f'zone={zone:2} NPC spawns={npc:3} objects={objects:2} warps={len(entries):3}')
    missing = db.execute('SELECT DISTINCT p.ZoneID,p.NpcID FROM K_NPCPOS p LEFT JOIN K_NPC n ON n.sSid=p.NpcID WHERE p.ActType>=100 AND n.sSid IS NULL').fetchall()
    print('Missing NPC prototype references:', missing)
    mora = warps(ROOT / 'data/map/moradon_20060124.smd')
    print('Moradon gate group 212:', [w for w in mora if w[0] // 10 == 212])
