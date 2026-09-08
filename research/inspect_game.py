"""Read-only Stoneshard package inspection; writes only the requested report.

Uses Python's standard library. Does not open process memory, inject, launch,
or modify the game, Steam configuration, or save files.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import mmap
from datetime import datetime, timezone
from pathlib import Path
import re
import struct


def u16(data, offset):
    return struct.unpack_from('<H', data, offset)[0]


def u32(data, offset):
    return struct.unpack_from('<I', data, offset)[0]


def cstring(data, offset):
    end = data.find(b'\0', offset, min(offset + 512, len(data)))
    return bytes(data[offset:end if end >= 0 else offset + 128]).decode('utf-8', 'replace')


def file_info(path):
    with path.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    return {'file': path.name, 'size': path.stat().st_size, 'sha256': digest}


def inspect_pe(path):
    result = file_info(path)
    with path.open('rb') as stream, mmap.mmap(stream.fileno(), 0, access=mmap.ACCESS_READ) as data:
        if data[:2] != b'MZ':
            raise ValueError('Not a PE executable')
        pe = u32(data, 0x3c)
        if data[pe:pe + 4] != b'PE\0\0':
            raise ValueError('Invalid PE signature')
        count, opt_size = u16(data, pe + 6), u16(data, pe + 20)
        result['machine'] = hex(u16(data, pe + 4))
        opt = pe + 24
        pe64 = u16(data, opt) == 0x20b
        result['is_64_bit'] = pe64
        sections = []
        for index in range(count):
            pos = opt + opt_size + index * 40
            sections.append((u32(data, pos + 12), max(u32(data, pos + 8), u32(data, pos + 16)), u32(data, pos + 20)))

        def rva_offset(rva):
            for start, size, raw in sections:
                if start <= rva < start + size:
                    return raw + rva - start
            raise ValueError(f'Unmapped RVA {rva:x}')

        imports_rva = u32(data, opt + (112 if pe64 else 96) + 8)
        imports = {}
        if imports_rva:
            descriptor = rva_offset(imports_rva)
            for _ in range(1024):
                original, stamp, forward, name, first = struct.unpack_from('<IIIII', data, descriptor)
                if not any((original, stamp, forward, name, first)):
                    break
                dll = cstring(data, rva_offset(name))
                thunk = rva_offset(original or first)
                names = []
                step = 8 if pe64 else 4
                for _ in range(10000):
                    value = struct.unpack_from('<Q' if pe64 else '<I', data, thunk)[0]
                    if not value:
                        break
                    if not value & (1 << (63 if pe64 else 31)):
                        names.append(cstring(data, rva_offset(value) + 2))
                    thunk += step
                imports[dll] = names
                descriptor += 20
        result['import_dlls'] = sorted(imports)
        pattern = re.compile(r'QueryPerformance|GetTickCount|timeGetTime|Sleep|WaitFor|CreateTimer|SetTimer|D3D|DXGI', re.I)
        result['timing_render_imports'] = {dll: [n for n in names if pattern.search(n)] for dll, names in imports.items() if any(pattern.search(n) for n in names)}
        # Search only concise identifiers, never export game code or localization.
        ident = re.compile(rb'[A-Za-z_][A-Za-z0-9_]{5,110}')
        keywords = re.compile(r'visor|camera|game_set_speed|room_speed|game_get_speed', re.I)
        result['relevant_identifiers'] = sorted({m.group().decode('ascii') for m in ident.finditer(data) if keywords.search(m.group().decode('ascii'))})[:180]
    return result


def inspect_win(path):
    result = file_info(path)
    with path.open('rb') as stream, mmap.mmap(stream.fileno(), 0, access=mmap.ACCESS_READ) as data:
        if data[:4] != b'FORM':
            raise ValueError('No GameMaker FORM signature')
        declared_end = u32(data, 4) + 8
        result['form_size_matches'] = declared_end == len(data)
        if declared_end > len(data):
            raise ValueError('Truncated FORM')
        chunks = []
        pos = 8
        while pos + 8 <= declared_end:
            name = data[pos:pos + 4].decode('ascii', 'replace')
            size = u32(data, pos + 4)
            if pos + 8 + size > declared_end:
                raise ValueError(f'Invalid {name} size')
            entry = {'name': name, 'offset': pos, 'size': size}
            if name in ('CODE', 'SCPT', 'OBJT', 'STRG') and size >= 4:
                entry['first_u32'] = u32(data, pos + 8)
            chunks.append(entry)
            pos += size + 8
        result['chunks'] = chunks
        result['chunks_end_matches'] = pos == declared_end
        code = next((c for c in chunks if c['name'] == 'CODE'), None)
        result['code_chunk_present'] = code is not None
        result['code_chunk_entry_count'] = code.get('first_u32') if code else None
        strings = next((c for c in chunks if c['name'] == 'STRG'), None)
        selected = []
        if strings:
            base = strings['offset'] + 8
            count = u32(data, base)
            if count * 4 + 4 > strings['size']:
                raise ValueError('STRG pointer table exceeds chunk')
            wanted = re.compile(r'visor|camera|game_set_speed|room_speed|game_get_speed|0\.9\.4\.25', re.I)
            for i in range(count):
                pointer = u32(data, base + 4 + 4 * i)
                if not base <= pointer <= base + strings['size'] - 4:
                    continue
                length = u32(data, pointer)
                if 0 < length <= 150 and pointer + 4 + length <= base + strings['size']:
                    value = bytes(data[pointer + 4:pointer + 4 + length]).decode('utf-8', 'replace')
                    if wanted.search(value) and re.fullmatch(r'[A-Za-z0-9_ .:/-]+', value):
                        selected.append(value)
        result['relevant_short_strings'] = sorted(set(selected))[:120]
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-dir', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    game = args.game_dir.resolve(strict=True)
    output = args.output.resolve()
    if output.is_relative_to(game):
        parser.error('Report output must be outside the game directory')
    report = {
        'observed_at_utc': datetime.now(timezone.utc).isoformat(),
        'method': 'Read-only disk inspection; no process-memory reads, hooks, inputs or game writes',
        'game_directory': str(game),
        'executable': inspect_pe(game / 'StoneShard.exe'),
        'data_win': inspect_win(game / 'data.win'),
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
