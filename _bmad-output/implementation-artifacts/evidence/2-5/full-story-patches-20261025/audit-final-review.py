"""Audit final, post-review executions against every accepted fix and matrix row."""
from pathlib import Path
import hashlib
import json
import re
import xml.etree.ElementTree as ET

out = Path(__file__).resolve().parent
root = out.parents[4]
tests = []
summaries = {}
for kind in ("module", "evidence", "platform"):
    assembly = ET.parse(out / f"{kind}-tests.xml").getroot().find("assembly")
    assert assembly is not None
    assert all(assembly.get(key) == "0" for key in ("failed", "errors", "skipped", "not-run")), assembly.attrib
    cases = assembly.findall(".//test")
    assert cases and len(cases) == int(assembly.get("total"))
    assert all(case.get("result") == "Pass" for case in cases)
    summaries[kind] = dict(assembly.attrib)
    tests.extend(cases)
passed = {test.get("name") for test in tests}
prior = out / "pre-workflow-review"
matrix = json.loads((prior / "matrix-audit.json").read_text())
assert matrix["coveredRows"] == 7
for names in matrix["rows"].values():
    assert names and set(names) <= passed
original = json.loads((prior / "review-findings-verification.json").read_text())
assert len(original) == 16
for item in original.values():
    if isinstance(item, list):
        assert item and set(item) <= passed
methods = {
    "C1": "FinalComponentSymlinksRefuseAtomically",
    "C2": "NullTrackedReadinessReturnsStructuredUncertaintyAsync",
    "C3": "DiscoveryFailuresPreventCompleteCleanupAsync",
    "C4": "MalformedTerminalMetadataRefusesRestorationAsync",
    "C5": "StateRootAbsenceAndNonDirectoryAreDistinguishedAsync",
    "C6": "ExclusiveLocksRefuseUntilReleased",
    "C7": "UnavailableResourcesRequireUnavailableReadiness",
}
corrections = {}
for finding, method in methods.items():
    cases = [test.get("name") for test in tests if test.get("method") == method]
    assert cases, (finding, method)
    corrections[finding] = {"cases": cases, "result": "passed"}
corrections["C1"]["windowsRuntimeQualification"] = False
corrections["C1"]["note"] = "Portable reader cases execute on Linux; Windows held-handle branch compiles and was checked against retained Microsoft API documentation."
sources = json.loads((out / "source-snapshot.json").read_text())
for name, expected in sources.items():
    data = (root / name).read_bytes()
    assert hashlib.sha256(data).hexdigest() == expected["sha256"] and len(data) == expected["size"], name
schemas = {}
for name in ("hexalith.test-environment.v1.json", "hexalith.test-result.v1.json"):
    data = (root / "docs/contracts/schemas" / name).read_bytes()
    assert data == (root / "references/Hexalith.Builds/schemas" / name).read_bytes()
    schemas[name] = hashlib.sha256(data).hexdigest()
for name in ("final-module-build", "final-evidence-build", "final-platform-build", "final-module-tests", "final-evidence-tests", "final-platform-tests", "final-retained-evidence", "final-schema-parity"):
    command = json.loads((out / f"{name}.command.json").read_text())
    assert command["exitCode"] == 0, name
for name in ("final-module-build", "final-evidence-build", "final-platform-build"):
    assert re.search(r"0 Warning\(s\).*0 Error\(s\)", (out / f"{name}.log").read_text(), re.S), name
summary = {**summaries, "executedPassingCases": len(tests), "originalAcceptedFixes": 16, "workflowReviewPatchGroups": 7, "matrixRowsCovered": 7, "sourceFilesUnchangedDuringFinalChecks": len(sources), "individualDebugBuilds": "zero warnings/errors", "schemaParity": schemas, "finalSourceBroadVerification": True, "freshLiveQualification": False, "nativeWindowsMacOSQualification": False, "preExistingReviewDeferrals": ["D2 process creation identity before PID deletion", "D3 exact captured-secret filtering for optional metadata archives"], "carriedDeferrals": ["D1 claim transaction cancellation", "CB3 cancellable workspace traversal", "CE1 inaccessible Linux process environments", "Q3 same-user Windows process inspection"], "qualificationLimits": json.loads((out / "resume.json").read_text()), "historicalVerification": "pre-workflow-review/ preserves the earlier 668-case broad lane and separate 11-case final archival correction; neither is substituted for this final full-source lane."}
(out / "verification.json").write_text(json.dumps(summary, indent=2) + "\n")
(out / "matrix-audit.json").write_text(json.dumps(matrix, indent=2) + "\n")
(out / "review-findings-verification.json").write_text(json.dumps(original, indent=2) + "\n")
(out / "workflow-review-findings-verification.json").write_text(json.dumps(corrections, indent=2) + "\n")
print(json.dumps({key: summary[key] for key in ("executedPassingCases", "originalAcceptedFixes", "workflowReviewPatchGroups", "matrixRowsCovered", "sourceFilesUnchangedDuringFinalChecks")}, indent=2))
