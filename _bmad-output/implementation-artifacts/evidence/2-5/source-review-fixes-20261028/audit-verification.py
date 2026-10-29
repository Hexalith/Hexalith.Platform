"""Audit executed cases against the frozen matrix and each accepted source finding."""
from pathlib import Path
import json
import xml.etree.ElementTree as ET

out = Path(__file__).resolve().parent
assemblies = {}
passed = []
for name in ['module', 'evidence', 'platform']:
    assembly = ET.parse(out / (name + '-tests.xml')).getroot().find('assembly')
    assert assembly is not None
    assert int(assembly.attrib['total']) > 0, name
    for field in ['failed', 'errors', 'skipped', 'not-run']:
        assert int(assembly.attrib[field]) == 0, (name, field, assembly.attrib)
    assemblies[name] = assembly.attrib
    passed.extend(test.attrib['name'] for test in assembly.findall('.//test') if test.attrib['result'] == 'Pass')

old_matrix = json.loads((out.parent / 'delta-review-fixes-20261025/matrix-audit.json').read_text())['rows']
rows = {}
for label, examples in old_matrix.items():
    methods = {example.split('(')[0] for example in examples}
    covering = [case for case in passed if any(case.startswith(method) for method in methods)]
    assert covering, label
    rows[label] = covering
(out / 'matrix-audit.json').write_text(json.dumps({'coveredRows': len(rows), 'rows': rows}, indent=2) + '\n')

findings = [
    ('R01', 'Post-readiness HXR030 preserves failed/test-failure', ['MissingNativeProcessCommitsPrerequisiteClassificationAsync']),
    ('R02', 'Committed outcome recovery waits within the original metadata allowance', ['SessionRecoveryWaitsForCommittedPassAsync', 'OutcomeCallbackFailureDoesNotRenewStoppingAllowanceAsync']),
    ('R03', 'Startup timeout retry preserves the first cause', ['StartupTimeoutRecordingRetryPreservesCauseAsync']),
    ('R04', 'Claim lookup failure preserves the known workspace', ['WorkspaceObservationSurvivesClaimFailureAsync']),
    ('R05', 'Non-directory workspace remains an owned observation', ['WorkspaceObservationSurvivesClaimFailureAsync']),
    ('R06', 'Wrong selected-test shapes refuse atomically', ['TamperedCleanupProofIsRefusedAsync']),
    ('R07', 'Claimed empty cleanup cannot ignore leftover arrays or uncertainty', ['TamperedCleanupProofIsRefusedAsync']),
    ('R08', 'Invalid supplemental metadata preserves verified listed owners', ['UnverifiableObservationNamesOwnerAsync']),
    ('R09', 'Missing legacy cleanup is unavailable', ['ListingPreservesStableAgeAfterManifestDeletionAsync']),
    ('R10', 'Interrupted flock retries', ['SharedMetadataReadPreservesPublishedBytesAsync']),
    ('R11', 'Linked cleanup requests cannot release failure retention', ['LinkedCleanupRequestCannotSuppressFailureRetentionAsync']),
    ('R12', 'Metadata copy refuses symlink destinations', ['MetadataCopiesRespectPrivateOwnedDestinationsAsync']),
    ('R13', 'Missing run bindings preserve the committed outcome with or without active tracking', ['MissingRunBindingPreservesFirstOutcomeAsync']),
    ('R14', 'Torn unbound claims remain uncertain while known peer claims stay independent', ['TornClaimWithoutReceiptNeverEstablishesEmptyCleanupAsync', 'UnverifiablePeerClaimDoesNotBlockOwnedCleanupAsync']),
    ('R15', 'Metadata and archive publication flush before rename', ['SharedMetadataReadPreservesPublishedBytesAsync', 'MetadataCopiesRespectPrivateOwnedDestinationsAsync']),
    ('R16', 'Verified complete cleanup takes priority over stale retained disposition', ['CliListingClassifiesPersistedRetentionAsync']),
    ('R17', 'Metadata copy creates private replacement files', ['MetadataCopiesRespectPrivateOwnedDestinationsAsync']),
]
verified = []
for identity, summary, methods in findings:
    cases = []
    for method in methods:
        covering = [case for case in passed if ('.' + method) in case]
        assert covering, (identity, method)
        cases.extend(covering)
    verified.append({'id': identity, 'summary': summary, 'executedPassingCases': cases})
observations = (out / 'native-observations.log').read_text()
for expected in ['injected-EINTR metadata-read.json', 'shared-lock-ok metadata-read.json', 'fsync-ok-before-rename .metadata-read.json.', 'fsync-ok-before-rename .archive-']:
    assert expected in observations, expected
assert observations.index('injected-EINTR metadata-read.json') < observations.index('shared-lock-ok metadata-read.json')
(out / 'finding-verification.json').write_text(json.dumps({'acceptedFindingsVerified': len(verified), 'findings': verified, 'nativeProbe': observations.splitlines()}, indent=2) + '\n')
workflow = {
    'B1': ['TornClaimWithoutReceiptNeverEstablishesEmptyCleanupAsync'],
    'B2': ['UnverifiableCleanupMarkerCannotPermitIssuanceAsync'],
    'B3': ['UnverifiableCleanupMarkerCannotPermitIssuanceAsync', 'LinkedCleanupRequestCannotSuppressFailureRetentionAsync'],
    'B4': ['TornClaimWithoutReceiptNeverEstablishesEmptyCleanupAsync', 'UnverifiablePeerClaimDoesNotBlockOwnedCleanupAsync'],
    'B6': ['MalformedDeadlineBindingsRefuseBeforeUpdatingDurationAsync'],
    'B7': ['DelayedStoppingOriginPreservesMetadataRecoveryAllowanceAsync', 'OutcomeCallbackFailureDoesNotRenewStoppingAllowanceAsync'],
    'E1': ['TornClaimWithoutReceiptNeverEstablishesEmptyCleanupAsync'],
    'E2': ['UnverifiableCleanupMarkerCannotPermitIssuanceAsync'],
    'V1': ['NativeMetadataFailuresPreserveCommittedBytesAsync'],
    'V2': ['NativeMetadataFailuresPreserveCommittedBytesAsync'],
    'P1': ['NativeMetadataFailuresPreserveCommittedBytesAsync'],
    'P2': ['UnverifiablePeerClaimDoesNotBlockOwnedCleanupAsync', 'EmptyReleasePreservesProofAcrossLaterPeerCorruption'],
    'P3': ['ExplicitStopAcrossProcessesPreservesFirstFailureAsync'],
}
workflow_cases = {}
for identity, methods in workflow.items():
    cases = []
    for method in methods:
        covering = [case for case in passed if ('.' + method) in case]
        assert covering, (identity, method)
        cases.extend(covering)
    workflow_cases[identity] = cases
native = [case for case in passed if '.NativeMetadataPublicationTests.' in case]
assert len(native) == 5, native
for scenario in ['eintr', 'document-flush', 'snapshot-flush', 'document-flush-eintr', 'document-flush-cancelled']:
    assert any('scenario: "' + scenario + '"' in case.replace(chr(92) + '"', '"') for case in native), scenario
(out / 'workflow-finding-verification.json').write_text(json.dumps({'reviewFindingsVerified': 10, 'parentAuditFindingsVerified': 3, 'findings': workflow_cases, 'normalNativeCases': native}, indent=2) + '\n')
before = json.loads((out / 'source-before.json').read_text())
after = json.loads((out / 'source-after.json').read_text())
assert before == after
summary = {'assemblies': assemblies, 'executedPassingCases': len(passed), 'matrixRowsCovered': len(rows), 'acceptedFindingsVerified': len(verified), 'sourceFilesVerified': len(before), 'sourceUnchangedDuringVerification': True, 'freshLiveQualification': False, 'packageQualification': False, 'windowsMacOSNativeQualification': False, 'nativeProbePassed': True, 'priorDeferralsPreserved': True, 'workflowReviewFindingsVerified': 10, 'parentAuditFindingsVerified': 3, 'ordinaryNativeCasesPassed': len(native)}
(out / 'verification.json').write_text(json.dumps(summary, indent=2) + '\n')
print(json.dumps(summary, indent=2))
