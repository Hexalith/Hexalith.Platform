from pathlib import Path
import datetime,hashlib,importlib.util,json,shutil
out=Path('/tmp/ext-protection-loop10-native-fixtures-44662c3e1c3a');root=Path('/home/administrator/projects/hexalith/platform');bundle=root/'docs/implementation/ext-protection-1-loop10/native-fixtures-44662c3e1c3a'
a=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def save(path,value):
 target=bundle/path;target.parent.mkdir(parents=True,exist_ok=True);target.write_text(json.dumps(value,indent=2)+'\n')
def copy(source,relative):
 target=bundle/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
records=[json.loads(p.read_bytes()) for p in sorted(out.glob('*.command.json'))]
records.sort(key=lambda v:v['startedUtc']);save('commands.json',records)
for p in sorted(out.glob('*')):
 if p.is_file() and (p.suffix in ('.log','.xml') or p.name.endswith('.result.json') or p.name in ('freeze.json','bootstrap-02-freeze.json','final-freeze.json','source-manifest.json','source-final-overlay.json','observed-source-gaps.json','post-run-audit.json')):copy(p,Path(p.name))
for variant in ('runtime','runtime-bootstrap-02','runtime-final'):copy(out/variant/'runtime-manifest.json',Path(variant+'-manifest.json'))
selected=['tests/Hexalith.Platform.Protection.Tests/OpenBaoBootstrapIntegrationTests.cs','tests/Hexalith.Platform.Protection.Tests/OpenBaoNativeCustodyFixture.cs','tests/Hexalith.Platform.Protection.Tests/OpenBaoDisposableNativeHandler.cs','tests/Hexalith.Platform.Protection.Tests/OpenBaoTenantNativeStore.cs','tests/Hexalith.Platform.Protection.Tests/OpenBaoShippedPolicyRenderer.cs','tests/Hexalith.Platform.Protection.Tests/OpenBaoErasureRootIntegrationTests.cs','tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj','eng/ext-protection-1/probe_erc_native.py','eng/ext-protection-1/test_probe_erc_native.py','deploy/erasure-root/bootstrap.py','deploy/erasure-root/custody-policy.hcl','src/Hexalith.Platform.Protection/OpenBaoErasureRootStore.cs']
source=[]
for relative in selected:
 p=out/'source-final-overlay/platform'/relative
 if not p.is_file():p=out/'source/platform'/relative
 if not p.is_file():raise SystemExit('Missing frozen source: '+relative)
 if a(p)!=a(root/relative):raise SystemExit('Selected source changed: '+relative)
 copy(p,Path('source/platform')/relative);source.append({'path':relative,'sha256':a(p)})
copy(out/'source/eventstore/src/Hexalith.EventStore.DomainService/DelegatedOwnerQueryClient.cs',Path('source/eventstore/src/Hexalith.EventStore.DomainService/DelegatedOwnerQueryClient.cs'))
parent=Path('/tmp/ext-protection-loop10-fixtures-parent-79s72umz')
for name in ('before.json','build-unblock.json','DelegatedOwnerQueryClient.before.cs'):copy(parent/name,Path('parent-preservation')/name)
before=json.loads((parent/'before.json').read_bytes());historical=[]
for entry in before['historicalEvidence']:
 p=root/entry['path'];historical.append(dict(entry,verified=p.is_file() and a(p)==entry['sha256']))
if not all(e['verified'] for e in historical):raise SystemExit('Historical evidence changed')
save('historical-preservation.json',{'files':historical})
save('selected-source.json',{'files':source,'nativeTestBodiesUnchanged':a(root/selected[5])==a(out/'source/platform'/selected[5])})
def interval(record):
 start=datetime.datetime.fromisoformat(record['startedUtc']);return start,start+datetime.timedelta(seconds=record['elapsedSeconds'])
def relevant(record):return record['label'].startswith(('build-','native-')) or record['label'] in ('bootstrap-unit','harness-unit','harness-unit-final','transport-components')
work=[r for r in records if relevant(r)];overlaps=[]
for index,left in enumerate(work):
 for right in work[index+1:]:
  a1,a2=interval(left);b1,b2=interval(right)
  if max(a1,b1)<min(a2,b2):overlaps.append({'left':left['label'],'right':right['label'],'overlapSeconds':round((min(a2,b2)-max(a1,b1)).total_seconds(),3)})
final=next(r for r in work if r['label']=='native-five-final')
save('serialization.json',{'initialExecutionDeviation':True,'overlaps':overlaps,'finalFiveCaseRunSerialized':not any('native-five-final' in (e['left'],e['right']) for e in overlaps),'intervalScope':'Own build/test commands; unrelated concurrent sessions were preserved.'})
probe_spec=importlib.util.spec_from_file_location('bundled_policy_renderer',root/'deploy/erasure-root/bootstrap.py');renderer=importlib.util.module_from_spec(probe_spec);probe_spec.loader.exec_module(renderer)
policy_files=[]
for result in sorted(out.glob('*.result.json')):
 data=json.loads(result.read_bytes())
 for policy in data.get('runtimePolicies',[]):
  enrollment={'transitMount':policy['transitMount'],'headMount':policy['headMount'],'nativeKeys':{'tenant-a':'epoch-a','tenant-b':'epoch-b'}}
  text=renderer.render_policy(enrollment,policy['tenantId'],policy['scope']);digest=hashlib.sha256(text.encode()).hexdigest()
  if digest!=policy['renderedPolicySha256']:raise SystemExit('Policy changed')
  relative=Path('rendered-policies')/result.stem/(policy['transitMount']+'-'+policy['tenantId']+'.hcl');target=bundle/relative;target.parent.mkdir(parents=True,exist_ok=True);target.write_text(text)
  policy_files.append({'path':str(relative),'sha256':digest,'tenantId':policy['tenantId'],'scope':policy['scope']})
save('rendered-policies.json',{'files':policy_files})
print(json.dumps({'commands':len(records),'selectedSources':len(source),'historicalFiles':len(historical),'overlaps':overlaps,'renderedPolicies':len(policy_files)}))
