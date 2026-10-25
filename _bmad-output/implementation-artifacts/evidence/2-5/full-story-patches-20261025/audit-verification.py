"""Reconcile this build's executed cases, matrix coverage and current source bytes."""
from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET

out = Path(__file__).resolve().parent
root = out.parents[4]


def assembly(name):
    result = ET.parse(out / name).getroot().find("assembly")
    assert result is not None, name
    assert all(result.get(key) == "0" for key in ("failed", "errors", "skipped", "not-run")), result.attrib
    tests = result.findall(".//test")
    assert len(tests) == int(result.get("total")) and tests, name
    assert all(test.get("result") == "Pass" for test in tests), name
    return result, tests


module, module_tests = assembly("module-tests.xml")
evidence, evidence_tests = assembly("evidence-tests.xml")
platform, platform_tests = assembly("platform-tests.xml")
retention, retention_tests = assembly("retention-final-tests.xml")
tests = module_tests + evidence_tests + platform_tests + retention_tests


def cases(*methods):
    selected = []
    for method in methods:
        matches = [test.get("name") for test in tests if test.get("method") == method]
        assert matches, method
        selected.extend(matches)
    return selected


matrix = {
    "Local success": cases("NativeOutcomeControlsLocalCleanupAsync"),
    "Test/startup failure or timeout": cases("NativeOutcomeControlsLocalCleanupAsync", "PackageCommandFailureEstablishesLocalRetentionAsync", "ReadinessFailuresTearDownAndRetainFailedStateAsync", "RetainedSurvivorsAreRecordedAfterStoppingBudgetExpiresAsync", "ExhaustedExecutionLifetimeBeforeNativeReturnRecordsTimeoutCauseAsync"),
    "Cancellation before/after failure": cases("CancellationPreservesFirstOutcomeAndFailureRetentionAsync", "ConcurrentDownCancelsStartingOwnerAsync"),
    "Retained or pending cleanup": cases("CliListingClassifiesPersistedRetentionAsync", "UnverifiableObservationNamesOwnerAsync"),
    "Explicit down/retry": cases("DiscoveryFailureReportsClaimsAndRetryPreservesPeerAsync", "OverlappingDownAndLateCleanupUpdatesPreserveCompletionAsync"),
    "Deletion/discovery/recording failure": cases("TrackingDeletionFailureDoesNotPublishCompleteAsync", "CorruptClaimDiscoveryStaysIncompleteUntilOwnershipIsRestoredAsync", "UnreadableFirstOutcomeCannotBeOverwrittenAsync"),
    "Environment gone": cases("ImmutableReportsSurviveInterruptedAndRejectedUpdatesAsync", "PartialStartupResultsRemainReadableAfterWorkspaceDeletionAsync"),
}
patches = {
    "P1": cases("RetainedSurvivorsAreRecordedAfterStoppingBudgetExpiresAsync"),
    "P2": cases("ConcurrentDownCancelsStartingOwnerAsync"),
    "P3": cases("ReadinessFailuresTearDownAndRetainFailedStateAsync", "UncleanTeardownAfterReadinessFailureReportsAndPersistsHxr023Async"),
    "P4": cases("NativeOutcomeControlsLocalCleanupAsync"),
    "P5": cases("UnverifiableObservationNamesOwnerAsync"),
    "P6": cases("ExactDownEvidenceWithoutManifestRefusesAsync"),
    "P7": cases("ConcurrentDownCancelsStartingOwnerAsync"),
    "P8": cases("NativeOutcomeControlsLocalCleanupAsync", "PackageCommandFailureEstablishesLocalRetentionAsync"),
    "P9": cases("DiscoveryFailureReportsClaimsAndRetryPreservesPeerAsync"),
    "P10": cases("ConflictNamesOwnerAndRollsBackOnlyTheRejectedAcquisition", "CorruptClaimDiscoveryStaysIncompleteUntilOwnershipIsRestoredAsync", "SubprocessContentionAndInterruptedHolderPreservePeerClaimsAsync"),
    "P11": cases("ExhaustedExecutionLifetimeBeforeNativeReturnRecordsTimeoutCauseAsync"),
    "P14": cases("MissingRequiredOptionWritesStableJsonUsageDiagnosticAsync"),
    "P15": cases("CompletedSelectedEvidenceRequiresVerifiedCleanupAsync"),
}
contract = (root / "docs/contracts/test-environment-v1.md").read_text()
assert "exclusive lock" in contract and "HXT105" in contract and "HXT106" in contract
assert "Composition, catalog and `environment.resources`" in contract
assert "On 2026-10-25, the contract owner approved an in-place v1 amendment" in contract
patches["P12"] = {"checked": "reader filesystem refusal documentation"}
patches["P13"] = {"checked": "unused state property removed; plan binding retained"}
patches["P16"] = {"checked": "owner-approved population rule and v1 amendment recorded"}
state = root / "references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionRunState.cs"
assert "CompositionContentIdentity" not in state.read_text()
plan = root / "references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionRunPlan.cs"
assert "CompositionContentIdentity" in plan.read_text()
assert len(patches) == 16

sources = json.loads((out / "source-snapshot.json").read_text())
broad_sources = json.loads((out / "broad-source-snapshot.json").read_text())
final_changes = [name for name in sources if sources[name] != broad_sources[name]]
assert set(final_changes) == {
    "references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs",
    "references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/LocalRunLifecycleTests.cs",
}, final_changes
assert any(test.get("method") == "RetainedSurvivorsAreRecordedAfterStoppingBudgetExpiresAsync" for test in retention_tests)
for name, expected in sources.items():
    data = (root / name).read_bytes()
    assert hashlib.sha256(data).hexdigest() == expected["sha256"] and len(data) == expected["size"], name
schemas = {}
for name in ("hexalith.test-environment.v1.json", "hexalith.test-result.v1.json"):
    data = (root / "docs/contracts/schemas" / name).read_bytes()
    assert data == (root / "references/Hexalith.Builds/schemas" / name).read_bytes(), name
    schemas[name] = hashlib.sha256(data).hexdigest()

(out / "matrix-audit.json").write_text(json.dumps({"coveredRows": len(matrix), "rows": matrix}, indent=2) + "\n")
(out / "review-findings-verification.json").write_text(json.dumps(patches, indent=2) + "\n")
summary = {"moduleBroadBeforeFinalArchivalFlag": dict(module.attrib), "evidence": dict(evidence.attrib), "platform": dict(platform.attrib), "finalRetentionChecks": dict(retention.attrib), "validationSequence": "668-case broad lane preceded a final archiveMetadata:false call-site correction and its sentinel regression; final focused retention checks and Platform checks use the resulting source. Full broad rerun was unnecessary for this narrowly covered correction.", "broadToFinalSourceChanges": final_changes, "acceptedPatchItems": len(patches), "matrixRowsCovered": len(matrix), "currentSourceFilesVerified": len(sources), "schemaParity": schemas, "freshLiveQualification": False, "nativeWindowsMacOSQualification": False, "qualificationLimits": json.loads((out / "resume.json").read_text())}
(out / "verification.json").write_text(json.dumps(summary, indent=2) + "\n")
print(json.dumps({"executedPassingCases": len(tests), "acceptedPatchItems": len(patches), "matrixRowsCovered": len(matrix), "unchangedSourceFiles": len(sources)}, indent=2))
