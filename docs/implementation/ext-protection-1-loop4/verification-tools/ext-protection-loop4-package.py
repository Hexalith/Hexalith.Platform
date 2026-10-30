from pathlib import Path
import json,hashlib,shutil,xml.etree.ElementTree as ET,re
base=Path('/home/administrator/hexalith-protection-worktrees/ext-protection-loop4-00722d7d17');platform=base/'platform';out=Path('/tmp/ext-protection-loop4-evidence');packet=platform/'docs/implementation/ext-protection-1-loop4'
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def xml_summary(name):
 a=ET.parse(out/name).getroot().find('assembly');return {key:int(a.attrib[key]) for key in ['total','passed','failed','skipped','errors','not-run']}|dict(seconds=float(a.attrib['time']),artifact='attempts/'+name)
for f in out.iterdir():
 if f.is_file():
  dest=packet/'attempts'/f.name;dest.parent.mkdir(exist_ok=True);shutil.copy2(f,dest);assert f.read_bytes()==dest.read_bytes()
freezes=[]
for version,pinsname in [('v1','final-runtime-pins.json'),('v2','final-runtime-v2-pins.json'),('v3','final-runtime-v3-pins.json'),('v4','final-runtime-v4-pins.json')]:
 pins=json.loads((out/pinsname).read_text());frozen=packet/'freezes'/version;frozen.mkdir(parents=True,exist_ok=True)
 runtime=Path(pins['runtime']);source=Path(pins['source']);row=dict(version=version,**pins,active=version=='v4',persistentMirrors={})
 for label,root,name in [('runtime',runtime,'runtime-manifest.json'),('source',source,'source-manifest.json')]:
  manifest=root/name;shutil.copy2(manifest,frozen/name);entries=json.loads(manifest.read_text())['files']
  for entry in entries:assert digest(root/entry['path'])==entry['sha256'],str(root/entry['path'])
  row[label+'VerifiedFiles']=len(entries)
  for mirror in Path('/home/administrator/hexalith-protection-evidence/ext-protection-1/source-recovery').glob('loop4-'+('native' if label=='runtime' else 'source')+'-final-*'):
   if (mirror/name).is_file() and digest(mirror/name)==digest(manifest):row['persistentMirrors'][label]=str(mirror)
 freezes.append(row)
current=freezes[-1];testbin=platform/'tests/Hexalith.Platform.Protection.Tests/bin/Debug/net10.0'
for entry in json.loads((Path(current['runtime'])/'runtime-manifest.json').read_text())['files']:assert digest(testbin/entry['path'])==entry['sha256'],'Current test binary differs: '+entry['path']
componentGrowth=[];observerGrowth=[]
for line in (out/'protection-final-v5.log').read_text().splitlines():
 if line.startswith('{'):
  row=json.loads(line)
  if 'geometry' in row:componentGrowth.append(row)
  if 'observerUnrelatedSameTargetBatches' in row:observerGrowth.append(row)
final=out/'native-final-five-4.json';native=json.loads(final.read_text()) if final.is_file() else None
attempts=[]
for index in range(1,5):
 f=out/('native-final-five-'+str(index)+'.json')
 if f.exists():
  v=json.loads(f.read_text());attempts.append(dict(artifact='attempts/'+f.name,outcome=v.get('outcome'),failure=v.get('failure'),cleanup=v['cleanup'],summary=v.get('nativeTestSummary'),seconds=v.get('nativeSuiteElapsedSeconds'),runtimeManifestSha256=v['frozenRuntimeManifestSha256']))
verification=dict(schema='EXT-PROTECTION-1.loop4-implementation.v1',status='in-progress-for-parent-audit-review',baselines=json.loads((out/'invariants.json').read_text())['baselines'],invariants=json.loads((out/'invariants.json').read_text()),restoration='attempts/restoration.json',functionalInventory='functional-source-inventory.json',deltaInventory='loop4-delta-inventory.json',originalBaselinePatch='original-baseline-task-delta.patch',finalCommands='attempts/final-v5-complete-commands.json',tests={
 'protection':xml_summary('protection-final-v5.xml'),'eventstoreUnion':xml_summary('eventstore-final-tests-v3.xml'),'contractsAnchors':xml_summary('contracts-final-tests-v3.xml'),'standalonePayload':xml_summary('payload-final-tests-v3.xml'),'existingCustodyLifecycle':xml_summary('custody-lifecycle-v3.xml'),'focusedLatest':xml_summary('targeted-09.xml'),
 'offlineVerifier':dict(total=109,failed=0,skipped=0,artifact='attempts/offline-verifier-final-v3.log'),'harnessBootstrap':dict(total=11,failed=0,skipped=0,artifact='attempts/harness-final-v3.log')},sourceMode=json.loads((out/'source-final-v3.log').read_text()),builds=dict(configuration='Debug',projectReferences=True,nodeReuse=False,allRequiredZeroWarningsErrors=True,protectionBuild='attempts/build-16-command.json',otherBuilds='attempts/final-v5-complete-commands.json',custodyBuild='attempts/custody-lifecycle-v3-commands.json'),growth=dict(component=componentGrowth,observer=observerGrowth,nativePrecheck=json.loads((out/'native-v4-geometry-precheck.json').read_text())['nativeGrowthGeometry'],measurementScope='Cumulative JSON through common production transition authority; separate actual source/registry/owner regressions cover adapters, not independent writer benchmarks.'),freezes=freezes,finalFunctionalFreezeEquivalence='attempts/final-v4-source-equivalence.json',native=dict(state='complete' if native else 'running',finalArtifact='attempts/native-final-five-4.json',outcome=native.get('outcome') if native else None,summary=native.get('nativeTestSummary') if native else None,seconds=native.get('nativeSuiteElapsedSeconds') if native else None,cleanup=native.get('cleanup') if native else None,geometry=native.get('nativeGrowthGeometry') if native else None,attempts=attempts,geometryPrecheck='attempts/native-v4-geometry-precheck.json',holderRecoveryPrecheck='attempts/native-v3-trim-precheck.json',privateEligibility='ExplicitSyntheticTestFixture',unchangedTargetCount=1000),acceptance=dict(replacementTargetsProposed=False,acceptedDateOrProfileProposed=False,liveExecuted=False,agentsExecuted=False,physicalCopyQualified=False,completeInstalled41CaseRun=False,productionDestructionIndependentlyGated=True),scope=dict(functionalFiles={repo:len(rows) for repo,rows in json.loads((packet/'functional-source-inventory.json').read_text()).items()},runtimeChangesAfterV4Freeze=False,postFreezeChanges='Handback/evidence/owner/spec documents and forensic evidence attributes only.',forbiddenOperationsPerformed=False))
(packet/'verification.json').write_text(json.dumps(verification,indent=2)+'\n')
rows=[]
for f in sorted(packet.rglob('*')):
 if f.is_file() and f.name not in ('artifact-inventory.json','SHA256SUMS'):rows.append(dict(path=str(f.relative_to(packet)),bytes=f.stat().st_size,sha256=digest(f)))
(packet/'artifact-inventory.json').write_text(json.dumps(rows,indent=2)+'\n');(packet/'SHA256SUMS').write_text(''.join(row['sha256']+'  '+row['path']+'\n' for row in rows));print(json.dumps(dict(files=len(rows),bytes=sum(row['bytes'] for row in rows),native=verification['native']['state'],runtimeMirrors=current['persistentMirrors']),indent=2))
