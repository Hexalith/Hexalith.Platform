from pathlib import Path
import hashlib,json,subprocess,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[5]
out=root/'_bmad-output/implementation-artifacts/evidence/2-5/final-workflow-review-20261025'
prior=root/'_bmad-output/implementation-artifacts/evidence/2-5/committed-review-fixes-20261025'
def assembly(name):
    result=ET.parse(out/name).getroot().find('assembly')
    assert result is not None
    return result
builds=assembly('final-builds-tests.xml')
assert all(builds.get(k)=='0' for k in ('failed','errors','skipped','not-run')),builds.attrib
tests=builds.findall('.//test')
assert len(tests)==int(builds.get('total')) and all(t.get('result')=='Pass' for t in tests)
passed={t.get('name') for t in tests}
old_findings=json.loads((prior/'review-findings-verification.json').read_text())
for item in old_findings.values(): assert set(item['cases'])<=passed
(out/'original-findings-verification.json').write_text(json.dumps(old_findings,indent=2)+'\n')
matrix=json.loads((prior/'matrix-audit.json').read_text())
assert len(matrix['rows'])==7
for row in matrix['rows']: assert row['passingTestCases'] and set(row['passingTestCases'])<=passed
matrix['buildsExecutedCases']=len(tests)
(out/'matrix-audit.json').write_text(json.dumps(matrix,indent=2)+'\n')
methods={
'F1':['UnverifiedLateObservationPreservesKnownLeftoversAsync'],
'F2':['ResultReaderRejectsWriterlessFifoWithoutBlockingAsync','CommandRecoversCommittedPassAfterOptionalRecordingFailureAsync'],
'F3':['OversizedAcquisitionPreservesReadableReceipt','OversizedClaimPayloadDoesNotPublishReceipt'],
'F4':['InvalidCleanupMetadataPreservesHealthyListingAsync'],
'F5':['SupportedHostProcessObservationControlsCleanupVerificationAsync','NativeEnvironmentTagMatchingPreservesOwnerBoundary'],
'F6':['LinkedResultOwnerCannotOverwriteOutsideSentinelAsync'],
'F9':['MissingNativeProcessCommitsPrerequisiteClassificationAsync'],
'F10':['TimeoutCallbackFailureRetriesOriginalCauseWithoutRenewingStoppingAsync','FailedStartupDoesNotRenewStoppingBudgetAsync'],
'F11':['DirectoryClaimRemainsVisibleUntilVerifiedDeletionAsync']}
findings={}
for key,names in methods.items():
    cases=[]
    for name in names:
        matched=[t.get('name') for t in tests if t.get('method')==name]
        assert matched,(key,name)
        cases+=matched
    findings[key]={'implemented':True,'cases':cases,'result':'passed'}
findings['F5']['nativeRuntimeQualification']='unavailable on Linux; native SID/UID paths compiled and reviewed against primary API documentation'
findings['F7']={'implemented':True,'result':'expected host-gated skip','artifact':'final-native-host-tests.xml','nativeRuntimeQualification':False}
findings['F8']={'implemented':True,'result':'stable peer-PID assertions compile; real-service case not run because installed Aspire differs from catalog','liveRuntimeQualification':False}
(out/'review-findings-verification.json').write_text(json.dumps(dict(sorted(findings.items())),indent=2)+'\n')
platform=assembly('platform-tests.xml')
assert platform.get('passed')=='1' and all(platform.get(k)=='0' for k in ('failed','errors','skipped','not-run'))
native=assembly('final-native-host-tests.xml')
assert native.get('total')=='1' and native.get('skipped')=='1' and native.get('failed')=='0' and native.get('errors')=='0'
source=json.loads((out/'source-snapshot-final.json').read_text())
for name,item in source.items():
    data=(root/name).read_bytes()
    assert hashlib.sha256(data).hexdigest()==item['sha256'] and len(data)==item['size'],name
(out/'source-content-identities.json').write_text(json.dumps(source,indent=2)+'\n')
pdb=[json.loads(line) for line in (out/'final-pdb-source.log').read_text().splitlines() if line.startswith('{')]
assert len(pdb)==3 and all(x['verified']==195 and not x['mismatches'] for x in pdb),pdb
(out/'pdb-source-bindings.json').write_text(json.dumps(pdb,indent=2)+'\n')
binaries={}
for name in ['references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/bin/Debug/net10.0/Hexalith.Builds.Tooling.dll','references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Tooling.dll','references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll','tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Builds.Tooling.dll','tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll']:
    data=(root/name).read_bytes();binaries[name]={'sha256':hashlib.sha256(data).hexdigest(),'size':len(data)}
assert binaries['references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/bin/Debug/net10.0/Hexalith.Builds.Tooling.dll']==binaries['references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Tooling.dll']
(out/'runtime-content-identities.json').write_text(json.dumps(binaries,indent=2)+'\n')
commands=json.loads((out/'commands.json').read_text())
by_log={row['log']:row for row in commands}
for path in out.glob('*.command.json'):
    row=json.loads(path.read_text());by_log[row['log']]=row
commands=list(by_log.values())
(out/'commands.json').write_text(json.dumps(commands,indent=2)+'\n')
for log in ['platform-build.log','builds-build-final.log','final-builds-tests.log','platform-tests.log','schema-parity.log','retained-evidence.log','final-pdb-source.log','final-native-host-tests.log']:
    assert by_log[log]['exitCode']==0,log
summary={'builds':dict(builds.attrib),'platform':dict(platform.attrib),'nativeHost':dict(native.attrib),'originalAcceptedFixes':13,'additionalReviewFixes':11,'matrixRowsCovered':7,'sourceFilesUnchangedDuringFinalRun':len(source),'pdbSourceDocumentsPerToolingRuntime':195,'individualDebugBuilds':'zero warnings/errors','schemaParity':'passed','retainedEvidenceAudit':'passed','sourceMode':'Debug','freshLiveQualification':False,'livePrerequisite':{'docker':'29.8.2','installedAspire':'13.6.1','requiredAspire':'13.6.3'},'packageQualification':'unavailable until authoritative release declarations are published','nativeWindowsMacOSQualification':False,'preExistingDeferrals':['D1 cancellation-aware claim transaction contention','CB3 cancellable workspace-tree traversal','CE1 Linux unreadable process environment discovery'],'superseded':['builds-tests.xml: 631/632 before correcting a stale HXT008 metadata-read proxy assertion','startup-budget-corrected.xml: test correction used a field not exposed by CLI output; corrected to inspect durable record and restored outcome'],'baselines':json.loads((out/'revisions-before-checks.json').read_text()),'currentRevisions':{label:subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True,stderr=subprocess.PIPE).strip() for label,repo in [('platform',root),('builds',root/'references/Hexalith.Builds')]},'runtimeIdentityNote':'Builds source/test Tooling bytes match; all three runtime PDBs match current source. Independently stamped Platform/Builds runtimes have separately retained hashes.','nativeApiSources':['https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocess','https://learn.microsoft.com/en-us/windows/win32/procthread/process-enumeration']}
(out/'verification.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps({k:summary[k] for k in ['originalAcceptedFixes','additionalReviewFixes','matrixRowsCovered','sourceFilesUnchangedDuringFinalRun']},indent=2))
print(json.dumps({k:builds.get(k) for k in ['total','passed','failed','errors','skipped','not-run','time']},indent=2))
