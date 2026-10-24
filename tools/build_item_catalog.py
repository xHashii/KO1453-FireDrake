"""Build the embedded, authoritative missing-only ITEM catalog from shipped SQL.
No guessed defaults, item aliases, or changes to the original data files.
"""
import argparse
import gzip
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'src/KnightOnline.Database/Items/ItemCatalog.sql.gz'


def catalog():
    columns = None
    rows = {}
    for path in sorted((ROOT / 'sql/mysql/kn_online').glob('*data.sql')):
        for line in path.read_text(encoding='utf-8').splitlines():
            if not line.startswith('INSERT `ITEM` '):
                continue
            match = re.fullmatch(r'INSERT `ITEM` (\(.*?\)) VALUES (\((\d+), .*\));', line)
            if match is None:
                raise ValueError(f'Unrecognized ITEM insert in {path}')
            if columns is None:
                columns = match[1]
            if columns != match[1] or int(match[3]) in rows:
                raise ValueError('ITEM column mismatch or duplicate source ID')
            rows[int(match[3])] = match[2]
    if not rows:
        raise ValueError('No source ITEM definitions')
    return (columns + '\n' + '\n'.join(rows[key] for key in sorted(rows)) + '\n').encode('utf-8'), len(rows)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    data, count = catalog()
    if args.check:
        if gzip.decompress(OUTPUT.read_bytes()) != data:
            raise SystemExit('Embedded ITEM catalog differs from shipped source. Run tools/build_item_catalog.py')
    else:
        OUTPUT.write_bytes(gzip.compress(data, compresslevel=9, mtime=0))
    print(f'ITEM catalog: {count} authentic full definitions; {OUTPUT.stat().st_size} compressed bytes')
