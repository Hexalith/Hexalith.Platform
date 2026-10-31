from pathlib import Path
import hashlib,json,shutil
root=Path('/home/administrator/projects/hexalith/platform'); out=Path('/tmp/ext-protection-loop10-native-fixtures-44662c3e1c3a'); runtime=out/'runtime'
shutil.copytree(root/'tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0',runtime)
entries=[{'path':str(p.relative_to(runtime)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(runtime.rglob('*')) if p.is_file()]
(runtime/'runtime-manifest.json').write_text(json.dumps({'files':entries},indent=2)+'\n')
source=[]
locations={'platform':root,'eventstore':root.parent/'eventstore','commons':root.parent/'eventstore/references/Hexalith.Commons','tenants':root.parent/'eventstore/references/Hexalith.Tenants','builds':root/'references/Hexalith.Builds'}
allowed={'.cs','.csproj','.props','.targets','.json','.py','.hcl','.yaml','.yml','.sh'}
for name,place in locations.items():
 paths=[]
 if name=='platform':
  for folder in ['src','tests','eng/ext-protection-1','deploy/erasure-root']:
   paths.extend((place/folder).rglob('*'))
  paths.append(place/'eng/ext-secrets-1/probe_raft_non_revival.py')
 else:
  paths.extend((place/'src').rglob('*'))
  if name=='eventstore': paths.extend((place/'tests/Hexalith.EventStore.Server.Tests/Security').glob('*.cs'))
 paths.extend(place.glob('*'))
 for p in sorted(set(paths)):
  if not p.is_file() or p.is_symlink() or p.suffix not in allowed or {'bin','obj','node_modules','__pycache__','references'} & set(p.relative_to(place).parts): continue
  relative=p.relative_to(place); data=p.read_bytes(); target=out/'source'/name/relative
  target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
  source.append({'repository':name,'path':str(relative),'sha256':hashlib.sha256(data).hexdigest()})
(out/'source-manifest.json').write_text(json.dumps({'roots':{k:str(v) for k,v in locations.items()},'files':source},indent=2)+'\n')
binding={'runtime':str(runtime),'runtimeFiles':len(entries),'manifestSha256':hashlib.sha256((runtime/'runtime-manifest.json').read_bytes()).hexdigest(),'assemblySha256':hashlib.sha256((runtime/'Hexalith.Platform.Protection.Tests.dll').read_bytes()).hexdigest(),'sourceFiles':len(source),'sourceManifestSha256':hashlib.sha256((out/'source-manifest.json').read_bytes()).hexdigest()}
(out/'freeze.json').write_text(json.dumps(binding,indent=2)+'\n'); print(json.dumps(binding))
