"""Read-only YYC variable-reference lookup for the locally installed build."""
from pathlib import Path
from game_path import game_directory
import bisect, json, re, struct, sys
sys.path.insert(0, str(Path(__file__).resolve().parent.parent / '.tools/python'))
import pefile

p = pefile.PE(str(game_directory() / 'StoneShard.exe'), fast_load=True)
data = bytes(p.__data__)
base = p.OPTIONAL_HEADER.ImageBase
text = next(s for s in p.sections if s.Name.startswith(b'.text'))
pd = next(s for s in p.sections if s.Name.startswith(b'.pdata'))
funcs = [(a, b) for a, b, _ in struct.iter_unpack('<III', pd.get_data()[:pd.Misc_VirtualSize//12*12]) if a]
starts = [a for a, b in funcs]
targets = {}
result = {name: {} for name in sys.argv[1:]}
for name in result:
    for m in re.finditer(rb'(?<=\x00)' + re.escape(name.encode()) + rb'(?=\x00)', data):
        string_rva = p.get_rva_from_offset(m.start())
        for ref in re.finditer(re.escape(struct.pack('<Q', base + string_rva)), data):
            pos = p.get_rva_from_offset(ref.start())
            if not text.VirtualAddress <= pos < text.VirtualAddress + text.Misc_VirtualSize:
                targets[pos + 8] = name
code = text.get_data()
for m in re.finditer(rb'\x8b[\x05\x0d\x15\x1d\x25\x2d\x35\x3d]', code):
    at = text.VirtualAddress + m.start()
    target = at + 6 + struct.unpack_from('<i', code, m.start()+2)[0]
    if target in targets:
        start, end = funcs[bisect.bisect_right(starts, at)-1]
        result[targets[target]].setdefault(hex(start), []).append(hex(at))
print(json.dumps(result, indent=2))
