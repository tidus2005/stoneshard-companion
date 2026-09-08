"""Read-only, bounded static symbol-reference analysis for this installed build."""
import sys
from pathlib import Path
from game_path import game_directory
sys.path.insert(0, str(Path(__file__).resolve().parent.parent / '.tools/python'))
import pefile
import capstone
import re
import struct
import json
import bisect

path = (game_directory() / 'StoneShard.exe')
data = path.read_bytes()
pe = pefile.PE(data=data, fast_load=True)
base = pe.OPTIONAL_HEADER.ImageBase
text = next(s for s in pe.sections if s.Name.startswith(b'.text'))
pdata = next(s for s in pe.sections if s.Name.startswith(b'.pdata'))
funcs = [(a,b) for a,b,c in struct.iter_unpack('<III',pdata.get_data()[:pdata.Misc_VirtualSize//12*12]) if a and b]
starts = [f[0] for f in funcs]
md = capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_64)
names = sys.argv[1:] or ['game_set_speed','game_get_speed','camera_set_view_pos','camera_get_view_x','variable_global_get','variable_instance_get','asset_get_index','instance_find','room_get_name','gml_Object_o_cameraController_KeyPress_35','gml_Script_scr_item_helm_toggle_visor']
targets = {}
result = {}
for name in names:
    offsets = [m.start()+1 for m in re.finditer(b'\0'+re.escape(name.encode())+b'\0',data)]
    rvas = [pe.get_rva_from_offset(o) for o in offsets]
    result[name] = {'string_rvas':[hex(v) for v in rvas],'code_refs':[],'data_refs':[]}
    for rva in rvas:
        targets[rva] = name
        ptr = struct.pack('<Q',base+rva)
        for m in re.finditer(re.escape(ptr),data):
            try:
                pos=pe.get_rva_from_offset(m.start())
            except pefile.PEFormatError:
                continue
            if not text.VirtualAddress<=pos<text.VirtualAddress+text.Misc_VirtualSize:
                result[name]['data_refs'].append({'rva':hex(pos),'qwords':[hex(v) for v in struct.unpack_from('<QQQQ',data,m.start())]})
code=text.get_data()
for m in re.finditer(rb'[\x48\x4c]\x8d[\x05\x0d\x15\x1d\x25\x2d\x35\x3d]',code):
    at=text.VirtualAddress+m.start()
    target=at+7+struct.unpack_from('<i',code,m.start()+3)[0]
    if target not in targets:
        continue
    name=targets[target]
    i=bisect.bisect_right(starts,at)-1
    start,end=funcs[i] if i>=0 else (at,at+100)
    # Decode from a real runtime-function boundary; slicing arbitrary bytes would
    # produce misleading disassembly in the middle of instructions.
    instructions=list(md.disasm(pe.get_data(start,end-start),base+start))
    selected=[ins for ins in instructions if at-30 <= ins.address-base < at+75]
    asm=[f'{ins.address-base:x}: {ins.mnemonic} {ins.op_str}' for ins in selected]
    preceding=[ins for ins in instructions if at-24<=ins.address-base<at and ins.mnemonic=='lea' and ins.op_str.startswith('rdx, [rip')]
    entry={'rva':hex(at),'function':hex(start),'asm':asm}
    if preceding and code[m.start():m.start()+3]==b'\x48\x8d\x0d':
        ins=preceding[-1]
        entry['candidate_routine']=hex(ins.address-base+ins.size+struct.unpack('<i',ins.bytes[-4:])[0])
    result[name]['code_refs'].append(entry)
out=Path(__file__).resolve().parent.parent/'artifacts/native-references.json'
out.parent.mkdir(exist_ok=True)
out.write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))
