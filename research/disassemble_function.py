"""Developer-only local ABI inspection. Disassembly stays in ignored artifacts."""
from pathlib import Path
from game_path import game_directory
import sys,struct,bisect
sys.path.insert(0,str(Path(__file__).resolve().parent.parent/'.tools/python'))
import pefile,capstone
p=pefile.PE(str(game_directory() / 'StoneShard.exe'),fast_load=True)
base=p.OPTIONAL_HEADER.ImageBase
md=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_64);md.detail=True
pdata=next(s for s in p.sections if s.Name.startswith(b'.pdata'))
fs=[(a,b) for a,b,c in struct.iter_unpack('<III',pdata.get_data()[:pdata.Misc_VirtualSize//12*12]) if a]
starts=[a for a,b in fs]
Path('artifacts').mkdir(exist_ok=True)
for argument in sys.argv[1:]:
 a,b=fs[bisect.bisect_right(starts,int(argument,16))-1];out=[]
 for i in md.disasm(p.get_data(a,b-a),base+a):
  note=''
  for o in i.operands:
   if o.type==capstone.x86.X86_OP_MEM and o.mem.base==capstone.x86.X86_REG_RIP:
    target=i.address+i.size+o.mem.disp-base
    if 0<target<p.OPTIONAL_HEADER.SizeOfImage:
     try:
      q=struct.unpack('<Q',p.get_data(target-8,8))[0]-base
      if 0<q<p.OPTIONAL_HEADER.SizeOfImage:
       s=p.get_data(q,100).split(b'\0')[0]
       if len(s)>1 and all(32<=ch<127 for ch in s):note+=' VAR:'+s.decode()
     except (ValueError,struct.error):pass
  out.append(f'{i.address-base:x}: {i.mnemonic} {i.op_str}{note}')
 Path(f'artifacts/function-{a:x}.txt').write_text('\n'.join(out))
 print(hex(a),b-a,'bytes','\n'.join(s for s in out if 'VAR:' in s))
