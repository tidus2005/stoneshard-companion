"""Create a separate test installation; never overwrite an existing target."""
from pathlib import Path
from game_path import game_directory
import shutil
import hashlib
import json
import os
root = Path(__file__).resolve().parent.parent
source = game_directory()
target = Path(os.environ['LOCALAPPDATA'])/'StoneshardCompanion/TestGame'
if target.exists():
    raise SystemExit('Test installation already exists; refusing to overwrite')
target.parent.mkdir(parents=True, exist_ok=True)
shutil.copytree(source, target)
data = bytearray((target/'data.win').read_bytes())
assert data[0x2518134:0x2518134+11] == b'StoneShard\0'
assert data[0x2518154:0x2518154+11] == b'Stoneshard\0'
data[0x2518134:0x2518134+10] = b'ShardTest0'
data[0x2518154:0x2518154+10] = b'ShardTest0'
(target/'data.win').write_bytes(data)
(target/'steam_appid.txt').write_text('625960',encoding='ascii')
# Copy only settings initially, never a real character. Test save creation is checked
# before any test play. Display is explicitly windowed for side-by-side validation.
test_saves = Path(os.environ['LOCALAPPDATA'])/'ShardTest0'
if test_saves.exists():
    raise SystemExit('Test save folder already exists; refusing to overwrite')
test_saves.mkdir()
settings=(Path(os.environ['LOCALAPPDATA'])/'StoneShard/settings.ini').read_text()
settings=settings.replace('fullscreenBorderless','windowed').replace('2560x1440','1280x720')
(test_saves/'settings.ini').write_text(settings)
manifest={'source':str(source),'test_installation':str(target),'expected_test_saves':str(test_saves),'test_data_sha256':hashlib.sha256(data).hexdigest(),'note':'Only project name/title strings changed in copied data.win; test save isolation must be verified at startup.'}
(root/'artifacts').mkdir(exist_ok=True)
(root/'artifacts/sandbox.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps(manifest,indent=2))
