"""Offline audit of retained Story 2.5 observations; grants no qualification credit."""
from pathlib import Path
import hashlib
import json
import xml.etree.ElementTree as ET

here = Path(__file__).resolve().parent
# Audit the retained schema bytes and recorded parity; the original checkout is optional.
recorded_parity = {item["schema"]: item for item in json.loads((here / "schema-parity.json").read_text())}
parity = []
for name in ("hexalith.test-environment.v1.json", "hexalith.test-result.v1.json"):
    retained = (here / "schemas" / name).read_bytes()
    digest = hashlib.sha256(retained).hexdigest()
    recorded = recorded_parity[name]
    assert recorded["identical"] and digest == recorded["platformSha256"] == recorded["buildsSha256"], name
    parity.append({"schema": name, "sha256": digest, "recordedCheckoutParity": True, "retainedBytesVerified": True})
unit_tests = list(ET.parse(here / "builds-tests.xml").getroot().iter("test"))
assert unit_tests and all(test.get("result") == "Pass" for test in unit_tests)
results = []
causes = {"success": ("passed", "tests-completed"), "failure": ("failed", "test-failure"), "timeout": ("failed", "execution-timeout"), "partial-startup": ("failed", "startup-failure")}
for scenario, (outcome, cause) in causes.items():
    path = here / "live/diagnostics" / scenario / "test-result.json"
    assert path.exists(), f"Missing required live scenario: {scenario}"
    document = json.loads(path.read_text())
    assert document["schema"] == "hexalith.test-result.v1"
    assert document["policyId"] == "hexalith.platform.candidate-and-attachment-policy.v1"
    assert document["terminal"]["outcome"] == outcome
    assert document["terminal"]["cause"] == cause
    assert document["cleanup"]["disposition"] == "complete"
    assert document["cleanup"]["leftoverResourceIds"] == []
    references = []
    for reference in document["diagnostics"]:
        retained_reference = Path(reference["reference"])
        assert not retained_reference.is_absolute() and ".." not in retained_reference.parts
        content = (path.parent / retained_reference).read_bytes()
        assert reference["classification"] == "internal" and reference["redacted"] is True
        assert reference["contentIdentity"] == "sha256:" + hashlib.sha256(content).hexdigest()
        references.append(reference["reference"])
    command = json.loads((here / "live" / ("command-" + scenario + ".json")).read_text())
    run_id = document["bindings"]["run"]["runId"]
    assert command["runId"] == run_id
    # This path records where the producer published the result during the run.
    # Relocated evidence authenticates its retained copy; it cannot reobserve that runtime.
    assert Path(command["testResultPath"]).is_absolute()
    repeated = json.loads((here / "live" / ("cleanup-" + scenario + "-idempotent.json")).read_text())
    assert repeated["Result"]["Outcome"]["ExitCode"] == 0 and repeated["Remaining"]["IsEmpty"] is True
    peer = json.loads((here / "live" / ("peer-audit-" + scenario + ".json")).read_text())
    assert peer["observedValue"] == "unchanged"
    results.append({"scenario": scenario, "runId": run_id, "outcome": outcome, "cause": cause, "recordedRuntimeObservations": {"cleanupComplete": True, "leftoversEmpty": True, "idempotentCleanupPassed": True, "originalResultPath": command["testResultPath"]}, "currentFilesystemChecks": {"retainedResultAccessible": True, "originalRuntimePathsChecked": False}, "diagnosticHashesVerified": references, "peerValue": peer["observedValue"]})
summary = {"schemaParity": parity, "unitTestsPassed": len(unit_tests), "matrixRowsCovered": len(json.loads((here / "matrix-audit.json").read_text())), "liveResults": results, "qualificationCredit": False}
if len(results) == len(causes):
    cleanup = json.loads((here / "live/cleanup-audit.json").read_text())
    assert cleanup["completed"] is True and not cleanup["claims"] and not cleanup["runs"] and not cleanup["cleanupFailures"]
    live_tests = list(ET.parse(here / "platform-live-tests.xml").getroot().iter("test"))
    assert len(live_tests) == 1 and live_tests[0].get("result") == "Pass"
    summary["recordedLiveCleanupVerified"] = True
(here / "parsed-audit.json").write_text(json.dumps(summary, indent=2) + "\n")
print(json.dumps({"unitTestsPassed": len(unit_tests), "matrixRowsCovered": summary["matrixRowsCovered"], "liveScenariosAudited": len(results), "schemaParity": True}))
