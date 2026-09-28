"""Exact-build, reversible removal of the two LVL == 30 checks. No save editing."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess

ORIGINAL = 'D45797505F04B9112C5FBB8E85C0F8CDA2FA60AEE954B4E108BAD8A0DC514AFA'
RVAS = (0x3EAF5BC, 0x42B9DD0)
OLD = b'\x48\xb8' + struct.pack('<d', 30)
# Valid character levels are positive. An unreachable sentinel disables only
# these equality checks; no new level ceiling, XP formula or rewards are added.
NEW = b'\x48\xb8' + struct.pack('<d', -1)

def digest(data):
    return hashlib.sha256(data).hexdigest().upper()

def offsets(data):
    pe = struct.unpack_from('<I', data, 0x3c)[0]
    assert data[pe:pe+4] == b'PE\0\0'
    count = struct.unpack_from('<H', data, pe+6)[0]
    opt = struct.unpack_from('<H', data, pe+20)[0]
    result = []
    for rva in RVAS:
        for i in range(count):
            section = pe + 24 + opt + 40*i
            size, address, rawsize, raw = struct.unpack_from('<IIII', data, section+8)
            if address <= rva and rva + len(OLD) <= address + rawsize:
                result.append(raw + rva - address)
                break
        else:
            raise ValueError('Patch RVA is not backed by a file section')
    return result

def transform(data, restore=False):
    output = bytearray(data)
    source, target = (NEW, OLD) if restore else (OLD, NEW)
    for offset in offsets(data):
        if data[offset:offset+10] != source:
            raise ValueError('Unexpected instruction bytes; refusing patch')
        output[offset:offset+10] = target
    result = bytes(output)
    if digest(result if restore else data) != ORIGINAL:
        raise ValueError('Unsupported executable; refusing patch')
    return result

def require_closed():
    result = subprocess.run(['powershell.exe', '-NoProfile', '-Command',
        'if (Get-Process StoneShard -ErrorAction SilentlyContinue) { exit 1 }'], capture_output=True)
    if result.returncode:
        raise RuntimeError('Exit Stoneshard before modifying its executable')

def tree_hashes(root):
    return {str(p.relative_to(root)): digest(p.read_bytes()) for p in root.rglob('*') if p.is_file()}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['check', 'apply', 'restore'])
    parser.add_argument('--game', type=Path, required=True)
    parser.add_argument('--backup', type=Path)
    parser.add_argument('--saves', type=Path)
    args = parser.parse_args()
    data = args.game.read_bytes()
    original = digest(data) == ORIGINAL
    output = transform(data, restore=not original)
    patched_hash = digest(output if original else data)
    report = dict(original_hash=ORIGINAL, patched_hash=patched_hash,
                  state='original' if original else 'uncapped', offsets=offsets(data))
    if args.mode == 'check' or (args.mode == 'apply' and not original) or (args.mode == 'restore' and original):
        print(json.dumps(report, indent=2)); return
    require_closed()
    if args.backup is None:
        raise ValueError('--backup is required for a mutation')
    args.backup.mkdir(parents=True, exist_ok=False)
    shutil.copy2(args.game, args.backup / 'StoneShard.exe')
    if digest((args.backup / 'StoneShard.exe').read_bytes()) != digest(data):
        raise IOError('Executable backup verification failed')
    if args.mode == 'apply':
        if args.saves is None or not (args.saves / 'characters_v1').is_dir():
            raise ValueError('A verified --saves directory is required')
        before = tree_hashes(args.saves)
        shutil.copytree(args.saves, args.backup / 'saves')
        if not before or before != tree_hashes(args.backup / 'saves') or before != tree_hashes(args.saves):
            raise IOError('Save backup changed or verification failed')
        report['save_hashes'] = before
    report['game'] = str(args.game)
    (args.backup / 'manifest.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    require_closed()
    # Hold an exclusive Windows file handle during the actual write; this fails
    # if the game has launched in the meantime. Rewrite only the two immediates.
    import ctypes
    from ctypes import wintypes
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.CreateFileW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
    kernel.CreateFileW.restype = wintypes.HANDLE
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = kernel.CreateFileW(str(args.game), 0xC0000000, 0, None, 3, 0, None)
    if handle == ctypes.c_void_p(-1).value:
        raise ctypes.WinError(ctypes.get_last_error())
    import msvcrt
    fd = msvcrt.open_osfhandle(handle, os.O_RDWR | os.O_BINARY)
    with os.fdopen(fd, 'r+b') as file:
        if file.read() != data:
            raise IOError('Executable changed after backup')
        try:
            for offset in offsets(data):
                file.seek(offset); file.write(output[offset:offset+10])
            file.flush(); os.fsync(file.fileno())
            file.seek(0)
            if file.read() != output:
                raise IOError('Patched executable verification failed')
        except BaseException:
            for offset in offsets(data):
                file.seek(offset); file.write(data[offset:offset+10])
            file.flush(); os.fsync(file.fileno())
            raise
    print(json.dumps(dict(report, save_hashes='verified', backup=str(args.backup), result=args.mode), indent=2))

if __name__ == '__main__':
    main()
