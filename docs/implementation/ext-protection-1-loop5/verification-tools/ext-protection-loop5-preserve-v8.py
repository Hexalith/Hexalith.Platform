from pathlib import Path
import shutil,hashlib,json,uuid
b=Path('/tmp/ext-protection-loop5-evidence');pins=json.loads((b/'qualification-runtime-v8-pins.json').read_text());target=Path('/home/administrator/hexalith-protection-evidence/ext-protection-1/source-recovery')/('loop5-implementation-qualification-v8-'+uuid.uuid4().hex);target.mkdir()
mirrors={}
for kind,manifest,pin in [('runtime','runtime-manifest.json','manifestSha256'),('source','source-manifest.json','sourceManifestSha256')]:
 source=Path(pins[kind]);dest=target/kind;shutil.copytree(source,dest)
 assert hashlib.sha256((source/manifest).read_bytes()).hexdigest()==pins[pin]
 expected=json.loads((source/manifest).read_text())['files'];assert len(list(dest.rglob('*')))>len(expected)
 assert len([p for p in dest.rglob('*') if p.is_file()])==len(expected)+1
 for row in expected:
  assert hashlib.sha256((source/row['path']).read_bytes()).hexdigest()==row['sha256'];assert (source/row['path']).read_bytes()==(dest/row['path']).read_bytes()
 assert (source/manifest).read_bytes()==(dest/manifest).read_bytes()
 mirrors[kind]=dict(path=str(dest),files=len(expected)+1,manifestSha256=pins[pin])
result=dict(activePins=pins,persistentRoot=str(target),mirrors=mirrors);(b/'qualification-v8-persistent-preservation.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
