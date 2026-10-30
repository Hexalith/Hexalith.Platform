from pathlib import Path
import shutil,hashlib,json
base=Path('/home/administrator/hexalith-protection-worktrees/ext-protection-loop5-290f9f5f3d');runtime=Path('/tmp/ext-protection-loop5-qualification-runtime-v6');source=Path('/tmp/ext-protection-loop5-qualification-source-v6');evidence=Path('/tmp/ext-protection-loop5-evidence')
runtime.mkdir(exist_ok=False);source.mkdir(exist_ok=False)
rows=[]
for p in (base/'platform/tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0').rglob('*'):
 if p.is_file():
  relative=p.relative_to(base/'platform/tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0');dest=runtime/relative;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
  assert dest.read_bytes()==p.read_bytes();rows.append(dict(path=str(relative),sha256=hashlib.sha256(dest.read_bytes()).hexdigest()))
(runtime/'runtime-manifest.json').write_text(json.dumps({'files':sorted(rows,key=lambda x:x['path'])},indent=2)+'\n')
sources=[]
for name in ['platform','eventstore']:
 for p in (base/name).rglob('*'):
  if p.is_symlink() or not p.is_file():continue
  relative=p.relative_to(base/name)
  if any(part in ['.git','bin','obj','references','node_modules','__pycache__','.venv'] for part in relative.parts):continue
  if relative.parts[0] not in ('src','tests','eng','tools','deploy') and relative.name not in ('Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config','Hexalith.Platform.slnx','Hexalith.EventStore.slnx'):continue
  dest=source/name/relative;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
  assert dest.read_bytes()==p.read_bytes();sources.append(dict(path=str(Path(name)/relative),sha256=hashlib.sha256(dest.read_bytes()).hexdigest()))
(source/'source-manifest.json').write_text(json.dumps({'files':sorted(sources,key=lambda x:x['path'])},indent=2)+'\n')
pins={'runtime':str(runtime),'runtimeFiles':len(rows),'manifestSha256':hashlib.sha256((runtime/'runtime-manifest.json').read_bytes()).hexdigest(),'assemblySha256':hashlib.sha256((runtime/'Hexalith.Platform.Protection.Tests.dll').read_bytes()).hexdigest(),'source':str(source),'sourceFiles':len(sources),'sourceManifestSha256':hashlib.sha256((source/'source-manifest.json').read_bytes()).hexdigest()}
(evidence/'qualification-runtime-v6-pins.json').write_text(json.dumps(pins,indent=2)+'\n');print(json.dumps(pins,indent=2))
