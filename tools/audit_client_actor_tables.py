"""Audit supplied actor TBLs and build an optional, additive 1453-format patch.
Never overwrites input ZIPs or existing rows. --check verifies committed outputs.
Format/stream cipher: Open-KO src/N3Base/N3TableBase{,Impl}.{h,cpp}.
"""
import argparse
import hashlib
import json
import re
import struct
import zipfile
from pathlib import Path
from build_moradon_import import parse_values

ROOT = Path(__file__).resolve().parents[1]
FORMATS = {1:'b', 2:'B', 3:'h', 4:'H', 5:'i', 6:'I', 8:'f', 9:'d'}


def crypt(data, encrypt=False):
    key = 0x0816
    out = bytearray()
    for value in data:
        converted = value ^ (key >> 8)
        out.append(converted)
        key = (((converted if encrypt else value) + key) * 0x6081 + 0x1608) & 65535
    return bytes(out)


def read_table(data):
    plain = crypt(data)
    pos = 0
    def number(fmt):
        nonlocal pos
        result = struct.unpack_from('<' + fmt, plain, pos)[0]
        pos += struct.calcsize('<' + fmt)
        return result
    columns = number('i')
    if not 1 <= columns <= 128:
        raise ValueError('Invalid column count')
    types = [number('i') for _ in range(columns)]
    if types[0] != 6 or any(t not in (*FORMATS, 7) for t in types):
        raise ValueError('Unsupported schema')
    count = number('i')
    if not 0 <= count <= 100000:
        raise ValueError('Invalid row count')
    rows = []
    for _ in range(count):
        row = []
        for t in types:
            if t == 7:
                length = number('i')
                if not 0 <= length <= len(plain) - pos:
                    raise ValueError('Invalid string length')
                row.append(plain[pos:pos+length])
                pos += length
            else:
                row.append(number(FORMATS[t]))
        rows.append(row)
    if pos != len(plain) or len({r[0] for r in rows}) != len(rows):
        raise ValueError('Trailing bytes or duplicate IDs')
    return types, rows


def write_table(types, rows):
    out = bytearray(struct.pack('<i', len(types)))
    out.extend(struct.pack('<'+'i'*len(types), *types))
    out.extend(struct.pack('<i', len(rows)))
    for row in rows:
        if len(row) != len(types):
            raise ValueError('Row width mismatch')
        for t, value in zip(types, row):
            if t == 7:
                out.extend(struct.pack('<i', len(value)))
                out.extend(value)
            else:
                out.extend(struct.pack('<'+FORMATS[t], value))
    return crypt(out, True)


def display(row):
    result = []
    for v in row:
        if isinstance(v, bytes):
            try:
                v = v.decode('cp949')
            except UnicodeDecodeError:
                v = v.decode('latin1')
        result.append(v)
    return result


def build():
    imported = {'Npc': {}, 'Mob': {}}
    for line in (ROOT/'src/KnightOnline.Database/World/MoradonExactImport.sql').read_text().splitlines():
        match = re.fullmatch(r'INSERT INTO KO_MORADON_IMPORTED_(NPC|MONSTER) \((.*?)\) VALUES \((.*)\);', line)
        if match:
            row = dict(zip(re.findall(r'`(.*?)`', match[2]), parse_values(match[3])))
            imported['Npc' if match[1] == 'NPC' else 'Mob'][row['sSid']] = row
    report = {'inputs': {}, 'tables': {}, 'scope': 'Add missing Moradon entries only; existing rows byte-preserved. Not a verified model/asset fix.'}
    outputs = {}
    for kind in ('Npc', 'Mob'):
        versions = {}
        for version in ('1453', '1534'):
            archive = ROOT/(version+'tbl.zip')
            report['inputs'][archive.name] = hashlib.sha256(archive.read_bytes()).hexdigest()
            with zipfile.ZipFile(archive) as z:
                data = z.read(kind+'_us.tbl')
            types, rows = read_table(data)
            assert write_table(types, rows) == data, 'Input must round-trip byte-for-byte'
            versions[version] = (types, rows)
        old_types, old_rows = versions['1453']
        new_types, new_rows = versions['1534']
        assert old_types == ([6,7,6] if kind == 'Npc' else [6,7,6,6])
        assert new_types == ([6,7,6,6,6,6,6] if kind == 'Npc' else old_types)
        old = {r[0]:r for r in old_rows}
        new = {r[0]:r for r in new_rows}
        common = old.keys() & new.keys()
        missing = sorted(imported[kind].keys() - old.keys())
        additions = set(missing)
        if kind == 'Mob':
            additions.update({4081,4082,4083} - old.keys())
        added_rows = []
        for key in sorted(additions):
            row = list(new[key][:len(old_types)])
            if key in imported[kind]:
                assert row[2] == imported[kind][key]['sPid']
                # Match current server labels, not the donor's alternative translations.
                row[1] = imported[kind][key]['strName'].encode('ascii')
            added_rows.append(row)
        combined = old_rows + added_rows
        data = write_table(old_types, combined)
        check_types, check_rows = read_table(data)
        assert check_types == old_types and check_rows[:len(old_rows)] == old_rows
        assert check_rows[len(old_rows):] == added_rows
        assert len({r[0] for r in check_rows}) == len(check_rows)
        assert not (imported[kind].keys() - {r[0] for r in check_rows})
        outputs[kind+'_us.tbl'] = data
        report['tables'][kind] = {
            '1453_rows': len(old), '1534_rows': len(new),
            '1453_types': old_types, '1534_types': new_types,
            'donor_only': [display(new[k]) for k in sorted(new.keys()-old.keys())],
            '1453_only': [display(old[k]) for k in sorted(old.keys()-new.keys())],
            'changed_common': [{'id':k, '1453':display(old[k]), '1534':display(new[k])}
                               for k in sorted(common) if old[k] != new[k][:len(old_types)]],
            'changed_common_name_count': sum(old[k][1] != new[k][1] for k in common),
            'changed_common_appearance_count': sum(old[k][2] != new[k][2] for k in common),
            'changed_common_extra_integer_count': sum(old[k][3:] != new[k][3:len(old_types)] for k in common),
            'missing_imported_ids': missing,
            'imported_appearance_mismatches': [k for k in sorted(imported[kind].keys() & old.keys()) if imported[kind][k]['sPid'] != old[k][2]],
            'patch_additions': [display(r) for r in added_rows],
            'patched_rows': len(check_rows),
            'patched_sha256': hashlib.sha256(data).hexdigest(),
        }
    look_versions = {}
    for version in ('1453', '1534'):
        with zipfile.ZipFile(ROOT/(version+'tbl.zip')) as z:
            raw = z.read('NPC_Looks.tbl')
        types, rows = read_table(raw)
        assert write_table(types, rows) == raw
        look_versions[version] = (types, rows)
    types, old_rows = look_versions['1453']
    donor_types, donor_rows = look_versions['1534']
    assert types == donor_types and len(types) == 38
    old = {r[0]:r for r in old_rows}
    donor = {r[0]:r for r in donor_rows}
    wanted = {r['sPid'] for group in imported.values() for r in group.values()}
    additions = sorted((wanted - old.keys()) & donor.keys())
    added_rows = [donor[k] for k in additions]
    data = write_table(types, old_rows + added_rows)
    check_types, check_rows = read_table(data)
    assert check_types == types and check_rows == old_rows + added_rows
    assert len({r[0] for r in check_rows}) == len(check_rows)
    # Magic anvil is a map-object proxy, not a missing character appearance.
    unresolved = sorted(wanted - old.keys() - donor.keys())
    assert unresolved == [5001], unresolved
    outputs['NPC_Looks.tbl'] = data
    report['tables']['NPC_Looks'] = {
        '1453_rows': len(old), '1534_rows': len(donor), 'columns': len(types),
        'same_schema': True,
        'donor_only': [display(donor[k]) for k in sorted(donor.keys()-old.keys())],
        '1453_only': [display(old[k]) for k in sorted(old.keys()-donor.keys())],
        'changed_common': [{'id':k, '1453':display(old[k]), '1534':display(donor[k])}
                           for k in sorted(old.keys() & donor.keys()) if old[k] != donor[k]],
        'patch_additions': [display(r) for r in added_rows],
        'patched_rows': len(check_rows),
        'patched_sha256': hashlib.sha256(data).hexdigest(),
        'unresolved_map_object_not_character': unresolved,
        'referenced_asset_paths_to_verify': sorted({v.decode('ascii') for row in added_rows for v in row[2:17] if isinstance(v, bytes) and v}),
    }
    report['scope'] = 'Add missing Moradon records and character appearances only; preserve all existing fields. Asset presence and native rendering still require verification.'
    return report, outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    report, outputs = build()
    report_path = ROOT/'docs/CLIENT_ACTOR_TABLE_AUDIT.json'
    archive = ROOT/'client-patches/moradon-1453-tables.zip'
    text = json.dumps(report, indent=2, ensure_ascii=False)+'\n'
    if args.check:
        assert report_path.read_text() == text, 'Audit report drift'
        with zipfile.ZipFile(archive) as z:
            assert set(z.namelist()) == set(outputs)
            for name, data in outputs.items():
                assert z.read(name) == data, 'Patch drift: '+name
    else:
        report_path.write_text(text)
        archive.parent.mkdir(exist_ok=True)
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
            for name, data in outputs.items():
                info = zipfile.ZipInfo(name, date_time=(2026,10,28,0,0,0))
                info.compress_type = zipfile.ZIP_DEFLATED
                z.writestr(info, data)
    print('Verified original encrypted byte-roundtrips, schemas, unique IDs, preserved existing rows and complete imported-ID coverage.')
    for name, table in report['tables'].items():
        print(name, table['1453_rows'], '->', table['patched_rows'], 'rows;', len(table['patch_additions']), 'additions')


if __name__ == '__main__':
    main()
