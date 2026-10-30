from pathlib import Path
import json,hashlib,xml.etree.ElementTree as ET,shutil,re
base=Path('/home/administrator/hexalith-protection-worktrees/ext-protection-loop5-290f9f5f3d');root=base/'platform';ev=Path('/tmp/ext-protection-loop5-evidence');packet=root/'docs/implementation/ext-protection-1-loop5'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,x):p.write_text(json.dumps(x,indent=2)+'\n')
commands={}
for directory in ['final-v7','final-v8']:
 for path in sorted((ev/directory).glob('commands-*.json')):
  for row in json.loads(path.read_text()):
   row=dict(row,path=directory+'/'+row['path']);assert row['exitCode']==0 and sha(ev/row['path'])==row['sha256'];commands[row['name']]=row
assert len(commands)==18
commands=list(commands.values());write(packet/'final-commands.json',commands)
counts={}
for name,directory,file in [('protection','final-v8','protection-final.xml'),('lifecycle','final-v7','custody-lifecycle.xml'),('eventstore','final-v7','eventstore-final.xml'),('contracts','final-v7','contracts-final.xml'),('payload','final-v7','payload-final.xml')]:
 a=ET.parse(ev/directory/file).getroot().find('assembly');counts[name]={k:int(a.get(k)) for k in ['total','passed','failed','errors','skipped','not-run']};counts[name]['seconds']=float(a.get('time'));assert not counts[name]['failed'] and not counts[name]['errors'] and not counts[name]['not-run']
for n,c in [('offline-verifier-final',116),('harness-final',12)]:
 assert re.search(r'Ran '+str(c)+r' tests? in ',(ev/'final-v8'/(n+'.log')).read_text())
for r in commands:
 if 'build' in r['name']:assert '0 Warning(s)' in (ev/r['path']).read_text() and '0 Error(s)' in (ev/r['path']).read_text()
spec=root/'_bmad-output/implementation-artifacts/spec-ext-protection-1-production-engine.md';original=base/'preserved/platform/_bmad-output/implementation-artifacts/spec-ext-protection-1-production-engine.md'
def frozen(p):return re.search(b'<frozen-after-approval[^>]*>.*?</frozen-after-approval>',p.read_bytes(),re.S).group()
assert frozen(spec)==frozen(original)
for kind in ['native-runtime-v7','qualification-runtime-v8']:
 pins=json.loads((ev/(kind+'-pins.json')).read_text())
 for key,manifest,digest in [('runtime','runtime-manifest.json','manifestSha256'),('source','source-manifest.json','sourceManifestSha256')]:
  p=Path(pins[key]);assert sha(p/manifest)==pins[digest]
  for row in json.loads((p/manifest).read_text())['files']:assert sha(p/row['path'])==row['sha256']
  if kind.startswith('qualification') and key=='source':
   for row in json.loads((p/manifest).read_text())['files']:assert sha(base/row['path'])==row['sha256']
result=ev/'native-full-five-v7.json';native=json.loads(result.read_text()) if result.exists() else None
if native is not None:assert native['phase']=='Completed'
state=native['outcome'] if native else 'Running'
if native and state=='NativeCasesPassed':
 assert native['failure'] is None and native['nativeTestSummary']==dict(errors=0,failed=0,notRun=0,skipped=0,total=5)
 assert all(native['cleanup'].get(k) is True for k in ['complete','containerAbsent','containerTmpfsUnmounted'])
 assert len(native['runtimePolicies'])==5 and all(r['rootUsedOnlyForBootstrap'] for r in native['runtimePolicies'])
 assert len(native['nativeGrowthGeometry'])==2 and len(native['nativeOccurrenceGeometry'])==1
 assert native['testAssemblySha256']==json.loads((ev/'native-runtime-v7-pins.json').read_text())['assemblySha256']
v=dict(schema='EXT-PROTECTION-1.loop5-local-implementation.v1',status='in-progress',nativeStatus=state,localQualification='Passed',counts=counts,offlineTests=116,harnessBootstrapTests=12,sourceCases=41,liveExecuted=False,agentsExecuted=False,productionQualified=False,fullCopyInventoryQualified=False,acceptedTarget=None,acceptedProfile=None,acceptedIntegrationDate=None,functionalSourceFiles=267,loop5DeltaFiles=64,platformBaseline='231b0f2b57e96984a919ac1cdce97cc36df18542',eventstoreBaseline='a62241679854fa9c359c0f552114a42ca0f6a638',frozenIntentSha256=hashlib.sha256(frozen(spec)).hexdigest(),restoration='verification/restoration.json',commands=commands,nativePins=json.loads((ev/'native-runtime-v7-pins.json').read_text()),qualificationPins=json.loads((ev/'qualification-runtime-v8-pins.json').read_text()),nativeQualificationEquivalence='verification/native-v7-qualification-v8-equivalence.json',nativeResult='verification/native-full-five-v7.json' if native else None,nativeExpectedCases=5,nativeExpectedTargets=1000,nativeExpectedOccurrenceOwners=256,nativeObservedResult=native,recoveryLimit='Exact native originals recover journal loss. A read result lost before separate native-original retention remains Unknown if no atomic independent original exists; current values and redispatch never substitute for it.',externalInputs='Physical A1/all-copy acceptance and full independently installed41 remain external/unestablished; parent merge/audit/review/source-candidate capture follows.')
write(packet/'verification.json',v)
for name in ['ext-protection-loop5-finalize.py','ext-protection-loop5-package-draft.py']:
 shutil.copy2(Path('/tmp')/name,packet/'verification-tools'/name)
if native:
 for p in ev.rglob('*'):
  if p.is_file():
   d=packet/'verification'/p.relative_to(ev);d.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,d);assert p.read_bytes()==d.read_bytes()
 rows=[dict(path=str(p.relative_to(packet)),sha256=sha(p),bytes=p.stat().st_size) for p in sorted(packet.rglob('*')) if p.is_file() and p.name not in ['artifact-inventory.json','SHA256SUMS']]
 write(packet/'artifact-inventory.json',rows)
 sums=[*rows,dict(path='artifact-inventory.json',sha256=sha(packet/'artifact-inventory.json'))]
 (packet/'SHA256SUMS').write_text(''.join(r['sha256']+'  '+r['path']+'\n' for r in sorted(sums,key=lambda r:r['path'])))
print(json.dumps(dict(finalCommands=len(commands),counts=counts,native=state,nativeFailure=native.get('failure') if native else None,packet=str(packet))))
