from pathlib import Path
import json,hashlib,re
base=Path('/home/administrator/hexalith-protection-worktrees/ext-protection-loop5-290f9f5f3d');ev=Path('/tmp/ext-protection-loop5-evidence');packet=base/'platform/docs/implementation/ext-protection-1-loop5'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
verified=[]
for prefix,preservation in [('native-runtime-v7','native-v7-persistent-preservation.json'),('qualification-runtime-v8','qualification-v8-persistent-preservation.json')]:
 pins=json.loads((ev/(prefix+'-pins.json')).read_text());mirror=json.loads((ev/preservation).read_text())
 for kind,manifest,expected in [('runtime','runtime-manifest.json','manifestSha256'),('source','source-manifest.json','sourceManifestSha256')]:
  active=Path(pins[kind]);persistent=Path(mirror['mirrors'][kind]['path']);assert sha(active/manifest)==pins[expected] and (active/manifest).read_bytes()==(persistent/manifest).read_bytes()
  rows=json.loads((active/manifest).read_text())['files']
  for r in rows:assert sha(active/r['path'])==r['sha256'] and (active/r['path']).read_bytes()==(persistent/r['path']).read_bytes()
  if prefix.startswith('qualification') and kind=='source':
   for r in rows:assert sha(base/r['path'])==r['sha256']
  verified.append(dict(graph=prefix,kind=kind,active=str(active),persistent=str(persistent),manifestSha256=pins[expected],files=len(rows)+1))
scoped=json.loads((packet/'functional-source-inventory.json').read_text())
for r in scoped:assert sha(base/r['repository']/r['path'])==r['sha256']
spec=base/'platform/_bmad-output/implementation-artifacts/spec-ext-protection-1-production-engine.md';prior=base/'preserved/platform/_bmad-output/implementation-artifacts/spec-ext-protection-1-production-engine.md'
def frozen(p):return re.search(b'<frozen-after-approval[^>]*>.*?</frozen-after-approval>',p.read_bytes(),re.S).group()
assert frozen(spec)==frozen(prior)
native=json.loads((ev/'native-full-five-v7.json').read_text());assert native['outcome']=='NativeCasesPassed' and native['nativeTestSummary']==dict(errors=0,failed=0,notRun=0,skipped=0,total=5)
assert all(native['cleanup'].values()) and native['failure'] is None
v=json.loads((packet/'verification.json').read_text());assert len(v['commands'])==18
for r in v['commands']:assert r['exitCode']==0 and sha(ev/r['path'])==r['sha256']
result=dict(schema='EXT-PROTECTION-1.loop5-final-boundary-audit.v1',frozenGraphs=verified,currentSourceMatchesQualificationV8=True,scopedSourceFiles=len(scoped),loop5DeltaFiles=len(json.loads((packet/'loop5-delta-inventory.json').read_text())),frozenIntentUnchanged=True,frozenIntentSha256=hashlib.sha256(frozen(spec)).hexdigest(),nativeResultSha256=sha(ev/'native-full-five-v7.json'),nativeOutcome=native['outcome'],nativeCleanup=native['cleanup'],finalCommandSelections=18,allFinalCommandsPassed=True,externalQualificationEstablished=False,sharedRootsMutatedByImplementation=False,gitMutationsByImplementation=False)
(ev/'final-boundary-audit.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
