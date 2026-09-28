"""Read-only price/coverage parity check against the installed game's gem table."""
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
            raise SystemExit('Supported native item table not found')
        end = data.find(b'\0', start)
        header = data[start:end].decode('ascii').split(';')
        gems = {}
        for raw in data[end:end + 1024 * 1024].split(b'\0'):
            parts = raw.decode('utf-8', 'replace').split(';')
            if len(parts) != len(header):
                continue
            row = dict(zip(header, parts))
            if row['Cat'] == 'valuable' and row['Subcat'] == 'gem':
                gems['o_inv_' + row['id']] = int(row['Price'])

native = (root / 'native/Bridge/refund_material_policy.h').read_text(encoding='utf-8-sig')
managed = (root / 'src/Core/LiveBuild.cs').read_text(encoding='utf-8-sig')
native_keys = re.findall(r'"(o_inv_\w+)"', re.search(r'br_gem_keys[^=]*=\{([^}]+)', native)[1])
native_values = [int(n) for n in re.search(r'br_gem_values[^=]*=\{([^}]+)', native)[1].split(',')]
managed_keys = re.findall(r'"(o_inv_\w+)"', re.search(r'GemKeys=\[([^]]+)', managed)[1])
managed_values = [int(n) for n in re.search(r'GemValues=\[([^]]+)', managed)[1].split(',')]
assert native_keys == managed_keys and native_values == managed_values, 'Wire order/value mismatch'
assert len(native_keys) == len(native_values) == 14, 'Unexpected gem catalog shape'
assert dict(zip(native_keys, native_values)) == gems, f'Refund catalog differs from installed gems: {gems}'
assert gems['o_inv_diamond'] == 600
print(json.dumps({'passed': True, 'gems': gems, 'threshold': 600, 'gameAccess': 'static file read only'}, indent=2))
