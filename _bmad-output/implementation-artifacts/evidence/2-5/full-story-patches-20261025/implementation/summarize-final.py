import hashlib, json, pathlib, xml.etree.ElementTree as ET
root=pathlib.Path(__file__).parent
builds=pathlib.Path('/home/administrator/projects/hexalith/platform/references/Hexalith.Builds')
module=ET.parse(root/'module-receipt-final.xml')
evidence=ET.parse(root/'evidence-metadata-final.xml')
focused=ET.parse(root/'survivor-metadata-final.xml')
tests=[{'name':t.attrib['name'],'result':t.attrib['result'],'lane':lane} for lane,tree in [('broad-before-final-metadata-flag',module),('final-focused',focused),('final-evidence',evidence)] for t in tree.iter('test')]
patterns={
 'P1':['RetainedSurvivorsAreRecordedAfterStoppingBudgetExpiresAsync'],
 'P2':['ConcurrentDownCancelsStartingOwnerAsync(declared: False'],
 'P3':['ReadinessFailuresTearDownAndRetainFailedStateAsync','UncleanTeardownAfterReadinessFailureReportsAndPersistsHxr023Async'],
 'P4':['NativeOutcomeControlsLocalCleanupAsync'],
 'P5':['UnreadableStateNeverClaimsEmptySuccessAsync','UnverifiableObservationNamesOwnerAsync','InvalidEntryDoesNotHideVerifiedPeerAsync'],
 'P6':['ExactDownEvidenceWithoutManifestRefusesAsync'],
 'P7':['ConcurrentDownCancelsStartingOwnerAsync(declared: True'],
 'P8':['NativeOutcomeControlsLocalCleanupAsync','PackageCommandFailureEstablishesLocalRetentionAsync'],
 'P9':['DiscoveryFailureReportsClaimsAndRetryPreservesPeerAsync'],
 'P10':['ConflictNamesOwnerAndRollsBackOnlyTheRejectedAcquisition','OversizedAcquisitionPreservesReadableReceipt','CorruptClaimDiscoveryStaysIncompleteUntilOwnershipIsRestoredAsync'],
 'P11':['ExhaustedExecutionLifetimeBeforeNativeReturnRecordsTimeoutCauseAsync'],
 'P14':['MissingRequiredOptionWritesStableJsonUsageDiagnosticAsync(command: "down")'],
 'P15':['CompletedSelectedEvidenceRequiresVerifiedCleanupAsync'],
}
findings={}
for key,parts in patterns.items():
 matches=[t for t in tests if any(p in t['name'] for p in parts)]
 if key in ['P1','P4','P8']: matches=[t for t in matches if t['lane']=='final-focused']
 if key=='P4': matches=[t for t in matches if 'evidenceFails: True' in t['name']]
 if key=='P8': matches=[t for t in matches if 'fails: True' in t['name'] or 'PackageCommandFailure' in t['name']]
 findings[key]={'passed':bool(matches) and all(t['result']=='Pass' for t in matches),'tests':matches}
doc=(builds.parent.parent/'docs/contracts/test-environment-v1.md').read_text()
findings['P12']={'passed':all(s in doc for s in ['non-blocking and bounded','symlink in the final path component','exclusive lock']),'verification':'Contract documents bounded input and final-link/exclusive-lock refusals.'}
state=(builds/'src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionRunState.cs').read_text()
findings['P13']={'passed':'CompositionContentIdentity' not in state,'verification':'Unused state property removed; plan property remains authoritative.'}
findings['P16']={'passed':all(s in doc for s in ['Composition, catalog and `environment.resources`','On 2026-10-25, the contract owner approved an in-place v1 amendment','`remainingResources` includes workspace IDs, claim IDs and discovery-failure']),'verification':'Owner-approved amendment and resources population rule are explicit.'}
summary={'moduleBroadBeforeFinalFlag':dict(module.find('assembly').attrib),'finalFocused':dict(focused.find('assembly').attrib),'finalEvidence':dict(evidence.find('assembly').attrib),'finalSourceAdjustment':json.loads((root/'survivor-metadata-final-change.json').read_text()),'findings':dict(sorted(findings.items(),key=lambda x:int(x[0][1:])))}
(root/'patch-verification.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps({'moduleBroadBeforeFinalFlag':summary['moduleBroadBeforeFinalFlag'],'finalFocused':summary['finalFocused'],'finalEvidence':summary['finalEvidence'],'unverifiedFindings':[k for k,v in findings.items() if not v['passed']]},indent=2))
assert all(v['passed'] for v in findings.values())
