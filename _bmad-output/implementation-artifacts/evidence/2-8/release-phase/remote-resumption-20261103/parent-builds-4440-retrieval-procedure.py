import hashlib,json,subprocess,zipfile,stat
from pathlib import Path,PurePosixPath
base=Path('/home/administrator/.cache/hexalith/story-2-8/builds-4440-official-20261104');base.mkdir(parents=True,exist_ok=True)
items=[(707664561,'g4-tool-package-inventory.json','04f97587db775b4aa2df30f903d66dabe45ab282418b661b5c69b443e2ecd894'),(707664519,'g4-tool-qualification-evidence.zip','224d0f6a1fa6ecc78c92c006fb6591529b1d6c0ccd184027240abf31e970f1af')]
for aid,name,h in items:
 p=base/name
 if not p.exists():p.write_bytes(subprocess.check_output(['gh','api','-H','Accept: application/octet-stream',f'repos/Hexalith/Hexalith.Builds/releases/assets/{aid}']))
 assert hashlib.sha256(p.read_bytes()).hexdigest()==h
 print(name,p.stat().st_size,h)
p=base/'g4-tool-qualification-evidence.zip'
with zipfile.ZipFile(p) as z:
 names=z.namelist();assert len(names)==len(set(names))
 for i in z.infolist():
  pp=PurePosixPath(i.filename);assert not pp.is_absolute() and '..' not in pp.parts and '\\' not in i.filename and not stat.S_ISLNK(i.external_attr>>16)
 z.extractall(base/'qualification')
 print('members',len(names))
d=json.loads((base/'g4-tool-package-inventory.json').read_bytes());print('inventoryKeys',list(d));print(str(d)[:7000])

