#!/usr/bin/env python3
"""Persist EXT-HOST-1 custody and guard evidence. Missing Parties, Conversations, or protection bindings stay blocked."""
import argparse
import json
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REFUSAL = ("DependencyNotAvailable: EXT-HOST-1. Complete H1-H4 composition, production providers, "
           "accepted targets/commands and persisted qualification are unavailable.")
BINDING_GAPS = ("partiesBinding", "conversationsBinding", "protectionEngine", "protectionObserver")
CATEGORIES = {
    "acl": ("Acl", "Authoriz", "Guard", "Fence", "Hold", "Scope"),
    "isolation": ("Cross", "Tenant", "Isolation", "Scope"),
    "outage": ("Unavailable", "Outage", "Fault", "Unknown"),
    "restart": ("Restart", "Worker"),
    "restore": ("Restore", "Recover", "Replay"),
}
CUSTODY_FILTER = ("FullyQualifiedName~ReplicatedSecurityObservationSpoolTests|"
                  "FullyQualifiedName~ReplicatedSecurityObservationSpoolWorkerTests|"
                  "FullyQualifiedName~TrustedEnvelopeReplayVerifierTests|"
                  "FullyQualifiedName~PlatformSecurityDenialRecorderTests|"
                  "FullyQualifiedName~Fr34ProtectionGateTests")


def canonical(value):
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True) + "\n").encode()


def binding_gaps(profile_path):
    if not profile_path:
        return list(BINDING_GAPS)
    try:
        profile = json.loads(Path(profile_path).read_text())
    except (OSError, json.JSONDecodeError):
        return list(BINDING_GAPS)
    gaps = []
    for name in BINDING_GAPS:
        value = profile.get(name)
        if not isinstance(value, str) or not value.strip() or value.strip() == "Pending":
            gaps.append(name)
    return gaps


def run_suite(root, project, test_filter, results, log_name):
    results.mkdir(parents=True, exist_ok=True)
    log = results / log_name
    command = ["dotnet", "test", str(project), "--filter", test_filter,
               "--results-directory", str(results),
               "--report-xunit-trx", "--report-xunit-trx-filename", "results.trx"]
    completed = subprocess.run(command, cwd=root, text=True, capture_output=True, timeout=3600)
    log.write_text(completed.stdout + "\n" + completed.stderr)
    names = []
    for trx in results.glob("**/results.trx"):
        tree = ET.parse(trx)
        for node in tree.iter():
            if node.tag.endswith("UnitTestResult"):
                names.append({"name": node.attrib.get("testName", ""), "outcome": node.attrib.get("outcome", "")})
        trx.unlink()
    return completed.returncode, names


def write_evidence(evidence, category, gaps, suites, names):
    matched = [item for item in names if any(token in item["name"] for token in CATEGORIES[category])]
    document = {
        "schema": "EXT-HOST-1.full-evidence.v1",
        "category": category,
        "requirements": ["H1", "H2", "H3", "H4", "v21", "v23"],
        "blocked": bool(gaps),
        "dependency": "EXT-HOST-1",
        "code": "DependencyNotAvailable" if gaps else "QualificationNotAccepted",
        "gap": gaps,
        "effects": 0,
        "liveEffects": 0,
        "dependencyAvailable": False,
        "productionEngineRegistered": False,
        "sources": ["Hexalith.Platform.Custody.Tests", "GovernanceScopeGuardTests"],
        "suites": suites,
        "matchedTests": matched,
    }
    (evidence / f"{category}.json").write_bytes(canonical(document))


def verify(root, artifacts, profile):
    root, artifacts = Path(root).resolve(), Path(artifacts).resolve()
    evidence = artifacts / "evidence"
    evidence.mkdir(parents=True, exist_ok=True)
    gaps = binding_gaps(profile)
    custody_project = root / "tests/Hexalith.Platform.Custody.Tests/Hexalith.Platform.Custody.Tests.csproj"
    guard_project = root / "references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj"
    if not guard_project.exists():
        guard_project = root.parent / "eventstore/tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj"
    custody_code, custody_names = run_suite(root, custody_project, CUSTODY_FILTER, artifacts / "custody", "custody.log")
    guard_code, guard_names = run_suite(root, guard_project, "FullyQualifiedName~GovernanceScopeGuardTests", artifacts / "guard", "guard.log")
    suites = {"custodyExitCode": custody_code, "guardExitCode": guard_code,
              "custodyTests": len(custody_names), "guardTests": len(guard_names)}
    names = custody_names + guard_names
    for category in CATEGORIES:
        write_evidence(evidence, category, gaps, suites, names)
    (evidence / "blocked.json").write_bytes(canonical({
        "schema": "EXT-HOST-1.full-evidence.v1",
        "blocked": True,
        "dependency": "EXT-HOST-1",
        "gap": gaps,
        "effects": 0,
        "liveEffects": 0,
        "dependencyAvailable": False,
        "productionEngineRegistered": False,
        "suites": suites,
    }))
    if custody_code != 0 or guard_code != 0:
        print("ERROR: EXT-HOST-1 evidence suites failed.", file=sys.stderr)
        return 1
    print("ERROR: " + REFUSAL, file=sys.stderr)
    return 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--artifacts-path", required=True)
    parser.add_argument("--profile")
    parser.add_argument("--root")
    args = parser.parse_args()
    root = args.root or Path(__file__).resolve().parents[2]
    return verify(root, args.artifacts_path, args.profile)


if __name__ == "__main__":
    raise SystemExit(main())
