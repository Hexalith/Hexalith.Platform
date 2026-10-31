from pathlib import Path
import datetime,hashlib,json,shutil,xml.etree.ElementTree as ET
out=Path('/tmp/ext-protection-loop10-native-fixtures-44662c3e1c3a');root=Path('/home/administrator/projects/hexalith/platform');bundle=root/'docs/implementation/ext-protection-1-loop10/native-fixtures-44662c3e1c3a'
a=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
selected=json.loads((bundle/'selected-source.json').read_bytes())['files']
for entry in selected:
 p=out/'source/platform'/entry['path'];target=bundle/'source/native-five-01/platform'/entry['path'];target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target)
p=out/'source-bootstrap-02/tests/Hexalith.Platform.Protection.Tests/OpenBaoBootstrapIntegrationTests.cs';target=bundle/'source/bootstrap-02/platform/tests/Hexalith.Platform.Protection.Tests/OpenBaoBootstrapIntegrationTests.cs';target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target)
for filename in ('run_check.py','freeze.py','post_run_audit.py','bundle_final.py','finish_handback.py'):
 target=bundle/'evidence-scripts'/filename;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(out/filename,target)
results={p.name:json.loads(p.read_bytes()) for p in sorted(out.glob('*.result.json'))};final=results['native-five-final.result.json'];serialization=json.loads((bundle/'serialization.json').read_bytes());audit=json.loads((bundle/'post-run-audit.json').read_bytes())
records=json.loads((bundle/'commands.json').read_bytes());command=next(c for c in records if c['label']=='native-five-final');start=datetime.datetime.fromisoformat(command['startedUtc']);end=start+datetime.timedelta(seconds=command['elapsedSeconds'])
classes={}
for test in ET.parse(out/'transport-components.xml').iter('test'):
 name=test.get('name','').rsplit('.',1)[0];classes[name]=classes.get(name,0)+1
native=[]
for filename,v in results.items():
 native.append({'result':filename,'sha256':a(out/filename),'outcome':v['outcome'],'failure':v['failure'],'summary':v.get('nativeTestSummary'),'elapsedNativeSeconds':v.get('nativeSuiteElapsedSeconds'),'safeCases':v.get('safeCaseResults',[]),'safeStages':v.get('safeFailureStages',[]),'cleanup':v['cleanup'],'verifiedTenantPolicyRecords':len(v.get('runtimePolicies',[]))})
spec=root/'_bmad-output/implementation-artifacts/spec-ext-protection-1-production-engine.md';before=json.loads((bundle/'parent-preservation/before.json').read_bytes());spec_before=next(e['sha256'] for e in before['snapshots'] if e['path']==str(spec.relative_to(root)))
verification={'schema':'ext-protection-1-native-fixture-continuation/v1','status':'in-progress','reviewLoopIteration':10,'originalBaselines':{'platform':'231b0f2b57e96984a919ac1cdce97cc36df18542','eventstore':'a62241679854fa9c359c0f552114a42ca0f6a638'},'specUnchangedFromContinuationStart':a(spec)==spec_before,'specSha256':a(spec),'historicalFilesPreserved':49,'historicalPreservation':'historical-preservation.json','sourceInventory':'source-manifest.json','sourceFinalOverlay':'source-final-overlay.json','selectedSources':'selected-source.json','runtimeClosures':audit['runtimeClosures'],'runtimeGraphsAreDistinct':True,'runtimeDifferences':'final-freeze.json','commands':'commands.json','builds':{'initial':'0 warnings, 2 errors in concurrent duplicate local; preserved','successfulLabels':['build-protection-02','build-protection-03','build-protection-04'],'configuration':'Debug','projectReferences':True,'serializedBuildSettings':True,'nodeReuse':False,'warnings':0,'errors':0},'componentChecks':{'bootstrapAndPreparedShell':{'passed':12,'log':'bootstrap-unit.log'},'changedHarnessOffline':{'passed':7,'log':'harness-unit-final.log'},'directFrozenDll':{'passed':42,'failed':0,'skipped':0,'runtime':'runtime','actualClasses':classes,'log':'transport-components.log'}},'nativeAttempts':native,'finalNativeInterval':{'startedUtc':start.isoformat(),'finishedUtc':end.isoformat(),'elapsedSecondsIncludingHarness':command['elapsedSeconds'],'elapsedNativeSeconds':final['nativeSuiteElapsedSeconds'],'serialized':serialization['finalFiveCaseRunSerialized']},'initialSerializationDeviation':'serialization.json','nativeSourceAndWorkloadsUnchanged':json.loads((bundle/'selected-source.json').read_bytes())['nativeTestBodiesUnchanged'],'finalNativeCases':final.get('safeCaseResults',[]),'finalNativeGrowthGeometry':final.get('nativeGrowthGeometry',[]),'finalNativeOccurrenceGeometry':final.get('nativeOccurrenceGeometry',[]),'sourceGaps':'observed-source-gaps.json','allOwnedContainersAbsentNow':all(v['containerAbsent'] for v in audit['ownedAttempts']),'allOwnedTmpfsUnmounted':all(v['harnessCleanup']['containerTmpfsUnmounted'] for v in audit['ownedAttempts']),'nativeAllFivePassed':False,'shippedBootstrapPassed':False,'productionQualified':False,'installed41CaseQualified':False,'physicalCopyQualified':False,'liveExecuted':False,'agentsExecuted':False,'clusterOperationsExecuted':False,'productionOpenBaoOperationsExecuted':False,'gitMutationsOrRemoteOperationsExecuted':False,'artifactManifest':'artifact-manifest.json'}
(bundle/'verification.json').write_text(json.dumps(verification,indent=2)+'\n')
p=bundle/'README.md';s=p.read_text();s=s.replace('This handback is still being assembled while the owned native execution runs. Final counts and cleanup will be recorded in `verification.json`.','The fixture repairs are implemented. Verification exposes two source gaps: fresh shipped bootstrap refuses native KV-v2 defaults, and the rendered tenant policy rejects the actual minimum-version configuration request with HTTP 403. Production source remains unchanged. The final native suite executed all five unchanged cases and failed four; no new all-five pass is claimed. [verification.json](verification.json) binds the complete evidence.')
anchor='## Reproducible disposable prerequisites'
section='''## Executed verification and remaining source gaps

Three required Debug/project-reference fixture builds pass with zero warnings and errors after the separately recorded concurrent build repair. Bootstrap/prepared shell tests pass 12/12, changed-harness checks pass 7/7, and the frozen initial DLL executes 42 transport/custody tests with zero failures/skips. These component results belong to their recorded runtime and do not replace native evidence.

The final native suite runs serially from `2026-10-31T06:35:07.846652+00:00` to `2026-10-31T07:14:10.763652+00:00`, including harness setup/cleanup. Native execution takes **2330.957 seconds**: **5 executed, 1 passed, 4 failed, 0 errors/skips/not-run**. The [case results](native-five-final.result.json) retain exact safe outcomes and source locations:

| Unchanged native case | Outcome |
|---|---|
| Tenant/context substitution, ACL denials, archive/history/index geometry | Passed |
| Pin race | Failed at line 86 |
| Lost trim acknowledgement / exact lookup | Failed at line 97 |
| Leaked old ciphertext trim / survivor | Failed at line 34 |
| Complete 1,000-target manifest vector | Failed at line 63 |

The complete 1,000-root registration loop reaches its manifest assertion; terminal consumption, ordered vector and lookup are not established. The passed context/history/index case retains the actual 9MiB state, a **9,439,440-byte head**, both history geometries and **256 owners/occurrences**. All ten final tenant-policy records reproduce exactly from the shipped renderer. These are narrow primitive results with synthetic private eligibility, not installed qualification.

All six owned native attempts remain separate:

| Attempt | Executed outcome | Cleanup |
|---|---|---|
| Bootstrap 01 | 1 failed; generic refusal before runtime tokens | Verified |
| Bootstrap 02 | 1 failed; `Bootstrap-head-config` | Verified |
| Initial five-case 01 | 5 executed / 4 failed; initial overlap deviation retained | Verified |
| Bootstrap 03 | 1 failed; exact fresh KV defaults captured | Verified |
| Narrow lost-trim diagnostic | 1 failed; rotate 200, config 403 twice, no trim | Verified |
| Final serialized five-case | 5 executed / 4 failed | Verified |

[Independent post-run checks](post-run-audit.json) confirm all six exact container IDs absent, all harness tmpfs cleanup complete, and all three 257-file runtime closures unchanged. The [serialization record](serialization.json) preserves four initial overlap intervals: failed build/harness unit, bootstrap 01/transport components, initial native five/build 03, and initial native five/bootstrap 02. The final native interval overlaps no owned build/test. No initial attempt is relabelled as serialized or passing.

The [source-gap trace](observed-source-gaps.json) is concrete:

1. Shipped bootstrap's existing `GET erc-heads/config` returns HTTP 200 with `max_versions=0` and `cas_required=false` immediately after it creates the KV-v2 mount. `ensure_config` writes only when data is absent and otherwise refuses the mismatch with required `{1,true}`. Bootstrap therefore stops before key/policy/head installation and its runtime-owner/genesis/lifecycle checks. The fixture did not seed configuration or bypass this stop. A source repair must handle fresh/default configuration while preserving partial-run matching/conflict refusal and readback.
2. Under the exact rendered tenant policy, native rotation returns 200, then the store's minimum-version configuration request returns 403 during both consumption and exact lookup. No trim POST occurs. The existing allowlists remain exactly 1..64; tenant isolation and export/backup/restore/deletion guards remain intact. The type-level cause and equivalence with direct production HTTP remain unestablished because this owned fixture forwards through the existing `bao` CLI transport. A source/transport compatibility repair and renewed native verification remain necessary; no policy bound or production behavior was changed here.

The final runtime has distinct DLL/PDB hashes from the initial graph; [final-freeze.json](final-freeze.json) lists every difference. Native-relevant production source bytes remain unchanged, while test-only diagnostics and unrelated concurrent source activity are explicitly inventoried. Whole binaries are not claimed equal. Broad source inventories record build context and do not expand task scope.

'''
s=s.replace(anchor,section+anchor);p.write_text(s)
files=[]
for q in sorted(bundle.rglob('*')):
 if q.is_file() and q.name!='artifact-manifest.json':files.append({'path':str(q.relative_to(bundle)),'bytes':q.stat().st_size,'sha256':a(q)})
manifest={'schema':'ext-protection-1-fixture-artifacts/v1','generatedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'files':files};(bundle/'artifact-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps({'bundle':str(bundle),'artifactManifest':str(bundle/'artifact-manifest.json'),'artifactFiles':len(files),'artifactManifestSha256':a(bundle/'artifact-manifest.json'),'verificationSha256':a(bundle/'verification.json'),'specUnchanged':verification['specUnchangedFromContinuationStart'],'serializedFinal':verification['finalNativeInterval']['serialized']}))
