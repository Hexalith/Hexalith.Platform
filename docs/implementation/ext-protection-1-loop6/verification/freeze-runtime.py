import pathlib,shutil,json,hashlib,uuid,sys
p=pathlib.Path('/home/administrator/hexalith-protection-worktrees/ext-protection-loop6-07bd8b0dea/platform');e=p.parent/'eventstore';r=pathlib.Path('/tmp/ext-protection-loop6-'+sys.argv[1]+'-'+uuid.uuid4().hex);r.mkdir();b=p/'tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0'
for f in b.rglob('*'):
 if f.is_file():
  d=r/f.relative_to(b);d.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,d)
h=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
m=r/'runtime-manifest.json';m.write_text(json.dumps({'files':[{'path':str(f.relative_to(r)),'sha256':h(f)} for f in sorted(r.rglob('*')) if f.is_file()]},sort_keys=True,indent=2))
s=r/'source';s.mkdir();entries=[]
for name,base in [('platform',p),('eventstore',e)]:
 for top in ['src','tests','eng/ext-protection-1','deploy/erasure-root']:
  for f in sorted((base/top).rglob('*')):
   rel=f.relative_to(base)
   if f.is_file() and not any(v in ['bin','obj','__pycache__'] for v in rel.parts):
    d=s/name/rel;d.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,d);entries.append({'repository':name,'path':str(rel),'sha256':h(f)})
sm=r/'source-manifest.json';sm.write_text(json.dumps({'files':entries},sort_keys=True,indent=2))
x={'root':str(r),'manifest':str(m),'manifestSha256':h(m),'sourceManifest':str(sm),'sourceManifestSha256':h(sm),'assemblySha256':h(r/'Hexalith.Platform.Protection.Tests.dll')};pathlib.Path('/tmp/ext-protection-loop6-evidence/'+sys.argv[2]).write_text(json.dumps(x,indent=2));print(json.dumps(x))
