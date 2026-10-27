from pathlib import Path
import gzip, hashlib, json, re, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[5]
out=root/'_bmad-output/implementation-artifacts/evidence/2-5/delta-review-fixes-20261025'
summaries={}; tests=[]
for kind in ('module','evidence','platform'):
    assembly=ET.parse(out/f'{kind}-tests.xml').getroot().find('assembly')
    assert assembly is not None
    assert all(assembly.get(k)=='0' for k in ('failed','errors','skipped','not-run')), assembly.attrib
    cases=assembly.findall('.//test')
    assert len(cases)==int(assembly.get('total')) and all(t.get('result')=='Pass' for t in cases)
    summaries[kind]=dict(assembly.attrib); tests.extend(cases)
def cases(method, contains=None):
    found=[t.get('name') for t in tests if t.get('method')==method and (contains is None or contains in t.get('name',''))]
    assert found, (method,contains)
    return found
matrix={
 'Local success':cases('NativeOutcomeControlsLocalCleanupAsync','fails: False'),
 'Test/startup failure or timeout':cases('NativeOutcomeControlsLocalCleanupAsync','fails: True')+cases('PackageCommandFailureEstablishesLocalRetentionAsync')+cases('RetainedSurvivorsAreRecordedAfterStoppingBudgetExpiresAsync')+cases('ExhaustedExecutionLifetimeBeforeNativeReturnRecordsTimeoutCauseAsync'),
 'Cancellation before/after failure':cases('CancellationPreservesFirstOutcomeAndFailureRetentionAsync')+cases('ConcurrentDownCancelsStartingOwnerAsync'),
 'Retained or pending cleanup':cases('CliListingClassifiesPersistedRetentionAsync')+cases('UnverifiableObservationNamesOwnerAsync'),
 'Explicit down/retry':cases('DiscoveryFailureReportsClaimsAndRetryPreservesPeerAsync')+cases('OverlappingDownAndLateCleanupUpdatesPreserveCompletionAsync'),
 'Deletion/discovery/recording failure':cases('TrackingDeletionFailureDoesNotPublishCompleteAsync')+cases('CorruptClaimDiscoveryStaysIncompleteUntilOwnershipIsRestoredAsync')+cases('UnreadableFirstOutcomeCannotBeOverwrittenAsync')+cases('SessionlessStartupRejectsMalformedCommittedOutcomeAsync'),
 'Environment gone':cases('ImmutableReportsSurviveInterruptedAndRejectedUpdatesAsync')+cases('PartialStartupResultsRemainReadableAfterWorkspaceDeletionAsync'),
}
findings={
 'W1':cases('SupportedTerminalMetadataRestoresAndListsAsync'),
 'W2':cases('UnverifiableObservationNamesOwnerAsync','unreadable-'),
 'W3':cases('SessionlessStartupRejectsMalformedCommittedOutcomeAsync'),
 'W4':cases('MalformedTerminalMetadataRefusesRestorationAsync')+cases('NullTrackedReadinessReturnsStructuredUncertaintyAsync')+cases('DiscoveryFailuresPreventCompleteCleanupAsync')+cases('NullCommittedResourceEntryReturnsStructuredUncertaintyAsync'),
 'W5':cases('UnavailableResourcesRequireUnavailableReadiness'),
 'W6':cases('ExactDownEvidenceWithoutManifestRefusesAsync'),
 'W7':cases('CompletedSelectedEvidenceRequiresVerifiedCleanupAsync'),
 'W8':cases('NativeOutcomeControlsLocalCleanupAsync','fails: False, archiveFails: False, cleanupFails: False, evidenceFails: True'),
 'W9':cases('ExhaustedExecutionLifetimeBeforeNativeReturnRecordsTimeoutCauseAsync')+cases('CancellationPreservesFirstOutcomeAndFailureRetentionAsync'),
}
workflow={'B1':cases('SessionlessStartupPreservesCommittedOutcomeAsync','contended: True'),'B3':cases('CompletedSelectedEvidenceRequiresVerifiedCleanupAsync'),'B4':cases('SessionlessStartupPreservesCommittedOutcomeAsync'),'B6':cases('NativeOutcomeControlsLocalCleanupAsync','fails: False, archiveFails: False, cleanupFails: False, evidenceFails: True')}
for name in ('implementation-source-snapshot.json','parent-source-snapshot.json'):
    snapshot=json.loads((out/name).read_text())
    for path,sha in snapshot['sha256'].items():
        assert hashlib.sha256((root/path).read_bytes()).hexdigest()==sha,path
schemas={}
for name in ('hexalith.test-environment.v1.json','hexalith.test-result.v1.json'):
    data=(root/'docs/contracts/schemas'/name).read_bytes()
    assert data==(root/'references/Hexalith.Builds/schemas'/name).read_bytes()
    schemas[name]=hashlib.sha256(data).hexdigest()
for name in ('module-build-final','evidence-build','platform-build','module-tests','evidence-tests','platform-tests','retained-evidence','schema-parity'):
    cmd=json.loads((out/f'{name}.command.json').read_text())
    assert cmd['exitCode']==0,name
for name in ('module-build-final','evidence-build','platform-build'):
    log=out/f'{name}.log'
    text=log.read_text() if log.exists() else gzip.decompress(log.with_suffix('.log.gz').read_bytes()).decode()
    assert re.search(r'0 Warning\(s\).*0 Error\(s\)',text,re.S),name
summary={**summaries,'executedPassingCases':len(tests),'acceptedDeltaFixes':9,'workflowReviewFixes':4,'matrixRowsCovered':7,'schemaParity':schemas,'sourceUnchangedDuringVerification':True,'sourceFilesVerified':len(json.loads((out/'parent-source-snapshot.json').read_text())['sha256']),'freshLiveQualification':False,'nativeWindowsMacOSQualification':False,'qualificationLimits':{'installedAspire':'13.6.1','requiredAspire':'13.6.3','dockerServer':'29.8.2','package':'Authoritative published artifact qualification remains outside this resumed delta; no package qualification is claimed.'},'reviewScope':'W1-W9 resumed delta and B1/B3/B4/B6 workflow corrections; earlier reviewed implementation and owner decisions retained.'}
(out/'matrix-audit.json').write_text(json.dumps({'coveredRows':7,'rows':matrix},indent=2)+'\n')
(out/'review-findings-verification.json').write_text(json.dumps(findings,indent=2)+'\n')
(out/'workflow-review-findings-verification.json').write_text(json.dumps(workflow,indent=2)+'\n')
(out/'verification.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps({k:summary[k] for k in ('executedPassingCases','acceptedDeltaFixes','matrixRowsCovered','sourceUnchangedDuringVerification')},indent=2))
