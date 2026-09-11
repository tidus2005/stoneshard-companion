"""Read-only check of the bridge catalog against the installed native CSV rows.

Does not run the game. Reports only material identifiers and fodder yields.
"""
import json
import mmap
import re
from pathlib import Path
from game_path import game_directory

root = Path(__file__).resolve().parent.parent
with (game_directory() / 'StoneShard.exe').open('rb') as source:
    with mmap.mmap(source.fileno(), 0, access=mmap.ACCESS_READ) as data:
        start = data.find(b'id;;Price;EffPrice;tier;Cat;Subcat;Material;Weight;')
        if start < 0:
            raise SystemExit('Native consumable table header not found')
        end = data.find(b'\0', start)
        header = data[start:end].decode('ascii').split(';')
        rows = []
        for raw in data[end:end + 1024 * 1024].split(b'\0'):
            parts = raw.decode('utf-8', 'replace').split(';')
            if len(parts) != len(header):
                continue
            row = dict(zip(header, parts))
            if row['Subcat'] in ('herb', 'berry') and row['fodder'].isdigit() and int(row['fodder']) > 0:
                rows.append({'key': 'o_inv_' + row['id'], 'yield': int(row['fodder'])})
catalog = set(re.findall(r'"(o_inv_[a-z_]+)"', (root / 'native/Bridge/fodder_catalog.h').read_text()))
protected = {'o_inv_rhubarb', 'o_inv_lentil'}
expected = {row['key'] for row in rows} - protected
if not expected or catalog != expected:
    raise SystemExit(f'Catalog mismatch: missing={expected-catalog}, unexpected={catalog-expected}')
print(json.dumps(rows, indent=2))
print(f'{len(expected)} automatic defaults match the native table after excluding protected cooking ingredients.')
