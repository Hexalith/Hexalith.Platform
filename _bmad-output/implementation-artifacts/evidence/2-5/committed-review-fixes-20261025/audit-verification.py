from pathlib import Path
import hashlib,json,subprocess,xml.etree.ElementTree as ET
root=Path('/home/administrator/projects/hexalith/platform')
evidence=root/'_bmad-output/implementation-artifacts/evidence/2-5/committed-review-fixes-20261025'
xml=evidence/'final-builds-tests.xml'
assembly=ET.parse(xml).getroot().find('assembly')
assert assembly is not None
assert all(assembly.get(key)=='0' for key in ['failed','errors','skipped','not-run']),assembly.attrib
tests=assembly.findall('.//test')
assert len(tests)==int(assembly.get('total'))
assert all(test.get('result')=='Pass' for test in tests)
findings={
'R1':['CommandRecoversCommittedPassAfterOptionalRecordingFailureAsync'],
'R2':['ReadinessDeadlineObservationSharesStoppingBudgetAsync','SelectedSetupExpiryPublishesIssuedStartupTimeoutAsync'],
'R3':['InitializationAfterExactCleanupPreservesIssuedIdentitiesAsync'],
'R4':['InterruptedFirstDiscoveryKeepsKnownContainerAsync','CleanupDeadlinePreservesKnownContainerIdentityAsync'],
'R5':['ListingReconcilesCommittedFailureAfterResultPublicationFailureAsync','ForeignNestedTerminalIdentityPreservesHealthyPeerAsync'],
'R6':['RunningSuitePublishesExecutionActivationForListingAsync'],
'R7':['LaterDiagnosticsPreserveFirstTerminalRulesAsync','ExactDownRetainsCleanupDiagnosticsAfterStoppingExpiryAsync'],
'R8':['NullCommittedResourceEntryReturnsStructuredUncertaintyAsync'],
'R9':['FinalManifestRefusalFinalizesIssuedOwnerAsync','AllocatedMappingFailurePreservesDeclaredOutcomeAndLegacyCleanupAsync'],
'R10':['CompletedPublicationFailureRestoresDiscoverableTrackingAsync','InterruptedCompletionRemainsListedUntilVersionedPublicationAsync','TrackingDeletionFailureDoesNotPublishCompleteAsync','CleanupBeforeStartReturnCannotRecreateOwnerAsync'],
'R11':['SupportedHostProcessObservationControlsCleanupVerificationAsync','NativeEnvironmentTagMatchingPreservesOwnerBoundary'],
'R12':['CliListingShowsIndependentExecutionActivationAsync'],
'R13':['LegacyEvidenceUsesCommittedOutcomeBeforeExportAsync','ConflictingSelectedEvidenceIsRejected']}
rows={}
for finding,methods in findings.items():
 cases=[]
 for method in methods:
  matched=[test.get('name') for test in tests if test.get('method')==method]
  assert matched,(finding,method)
  cases+=matched
 rows[finding]={'implemented':True,'result':'passed','cases':cases}
(evidence/'review-findings-verification.json').write_text(json.dumps(rows,indent=2)+'\n')
matrix_methods={
 'Local success':['NativeOutcomeControlsLocalCleanupAsync'],
 'Test/startup failure or timeout':['ExecutionLifetimeExpiryCommitsFailureBeforeExpiredProcessingAsync','ReadinessDeadlineObservationSharesStoppingBudgetAsync','PackageOutcomesPreserveRetentionAndOwnedCleanupAsync'],
 'Cancellation before/after failure':['CancellationPreservesFirstOutcomeAndFailureRetentionAsync'],
 'Retained or pending cleanup':['CliListingClassifiesPersistedRetentionAsync','CommandRecoversCommittedPassAfterOptionalRecordingFailureAsync','ListingReconcilesCommittedFailureAfterResultPublicationFailureAsync'],
 'Explicit down/retry':['OverlappingDownAndLateCleanupUpdatesPreserveCompletionAsync','DiscoveryFailureReportsClaimsAndRetryPreservesPeerAsync'],
 'Deletion/discovery/recording failure':['UnreadableFirstOutcomeCannotBeOverwrittenAsync','TrackingDeletionFailureDoesNotPublishCompleteAsync','ExactDownRetainsCleanupDiagnosticsAfterStoppingExpiryAsync'],
 'Environment gone':['ImmutableReportsSurviveInterruptedAndRejectedUpdatesAsync','PartialStartupResultsRemainReadableAfterWorkspaceDeletionAsync']}
matrix=[]
for row,methods in matrix_methods.items():
 cases=[test.get('name') for test in tests if test.get('method') in methods]
 if row=='Local success':cases=[name for name in cases if 'fails: False' in name and 'cleanupFails: False' in name]
 assert cases,row
 matrix.append({'matrixRow':row,'passingTestCases':cases})
(evidence/'matrix-audit.json').write_text(json.dumps({'coveredRows':len(matrix),'totalRows':7,'buildsExecutedCases':len(tests),'rows':matrix,'freshLiveQualification':False},indent=2)+'\n')
prior=root/'_bmad-output/implementation-artifacts/evidence/2-5/review-fixes-20261025/final-parent/source-content-identities.json'
paths=set(json.loads(prior.read_text()))
paths.update(['references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionProcessEnvironmentReader.cs','references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/RunEvidence/ModuleRunEvidenceArtifactValidator.cs','references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/LocalRunCommittedReviewTests.cs','references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/TestEnvironmentProvisioningReviewTests.cs'])
fingerprints={}
for name in sorted(paths):
 content=(root/name).read_bytes()
 fingerprints[name]={'sha256':hashlib.sha256(content).hexdigest(),'size':len(content)}
(evidence/'source-content-identities.json').write_text(json.dumps(fingerprints,indent=2)+'\n')
binaries={}
for name in ['references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/bin/Debug/net10.0/Hexalith.Builds.Tooling.dll','references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Tooling.dll','references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll','tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Builds.Tooling.dll','tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll']:
 content=(root/name).read_bytes();binaries[name]={'sha256':hashlib.sha256(content).hexdigest(),'size':len(content)}
assert len({item['sha256'] for name,item in binaries.items() if name.endswith('Hexalith.Builds.Tooling.dll')})==1,binaries
(evidence/'runtime-content-identities.json').write_text(json.dumps(binaries,indent=2)+'\n')
summary={'builds':dict(assembly.attrib),'reviewFindings':len(rows),'sourceFingerprints':len(fingerprints),'sourceMode':'Debug','nativeWindowsAndMacOSRuntimeQualification':'not-run-on-Linux-host','liveServiceQualification':'no-new-credit-in-this-offline-worker','packageRuntimeQualification':'unavailable-until-authoritative-release-declarations-are-published','preExistingDeferral':'D1 cancellation-aware claim transaction contention','platformHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'buildsHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root/'references/Hexalith.Builds',text=True).strip(),'uncommittedSourceIdentities':'source-content-identities.json','supersededObservations':['focused-tests.xml:124/126 before correcting two new fixture inputs','builds-tests-verified.xml:613/616 before completion rollback and shared-fixture-tag corrections']}
(evidence/'verification.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps({key:summary[key] for key in ['reviewFindings','sourceFingerprints','platformHead','buildsHead']},indent=2))
print(json.dumps({key:assembly.get(key) for key in ['total','passed','failed','errors','skipped','not-run','time']},indent=2))
