#!/usr/bin/env python3
"""Persist EXT-HOST-1 custody and guard evidence. A complete profile can record component qualification; live qualification stays gated."""
import argparse
import hashlib
import json
import os
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

import prepare_acceptance as prepare

REFUSAL = ("DependencyNotAvailable: EXT-HOST-1. Complete H1-H4 composition, production providers, "
           "accepted targets/commands and persisted qualification are unavailable.")
BINDING_GAPS = ("partiesBinding", "conversationsBinding", "protectionEngine", "protectionObserver")
REQUIRED_CELLS = (
    "RoutingUsesAuthenticatedOriginAndRetainedReceiptDigest",
    "ExactLookupDoesNotMutateAndRequiresCurrentPrivateAuthority",
    "DurableFairDrainRetainsUnknownAndAdvancesOtherTenantAfterRestart",
    "UnavailableSpoolOrOriginalKeyNeverReportsProcessedDenial",
    "OriginalObservationIsDurableAndStableAcrossRetryAndRestart",
    "SameRevisionDifferentStateCannotPassRestoreProof",
    "LostRecorderAcknowledgementRecoversExactSourceBeforeSpoolAck",
    "LostAcknowledgementAndRetryReuseStoredFirstSeenWithoutRenewal",
    "StartupEveryReadinessAndContentAdmissionRunFreshCanaries",
    "AcceptedAttributionAndInstalledFencesAreJointWithEverySourceWrite",
    "HoldAndSealSerializeAtConditionalEffectWithExactStaleLookup",
    "PostSealAdmissionCannotBecomeContentContainmentOrSuccessfulCompletion",
    "CompromiseAndReplacementRemainExactSameBatchWithoutClearingGlobalBlock",
)
LOST_ACK_CELLS = (
    "LostRecorderAcknowledgementRecoversExactSourceBeforeSpoolAck",
    "LostAcknowledgementAndRetryReuseStoredFirstSeenWithoutRenewal",
)
MISSING_RUNTIME_INPUTS = ("EXT-PROTECTION-1", "trust-anchors", "OQ-5.4-1")
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
ATTEMPT_NAME = "component-attempt.json"


def canonical(value):
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True) + "\n").encode()


def binding_gaps(profile_path):
    if not profile_path:
        return list(BINDING_GAPS)
    try:
        profile = json.loads(Path(profile_path).read_text())
    except (OSError, json.JSONDecodeError):
        return list(BINDING_GAPS)
    if not isinstance(profile, dict):
        return list(BINDING_GAPS)
    gaps = []
    for name in BINDING_GAPS:
        value = profile.get(name)
        if not isinstance(value, str) or not value.strip() or value.strip().casefold() == "pending":
            gaps.append(name)
    return gaps


def suite_mode(configuration):
    if configuration not in ("Debug", "Release"):
        raise ValueError("Unsupported component suite configuration: " + str(configuration))
    return {"configuration": configuration,
            "dependencyMode": "public-packages" if configuration == "Release" else "source-projects"}


def suite_command(project, test_filter, results, configuration="Debug"):
    suite_mode(configuration)
    return ["dotnet", "test", str(project), "--configuration", configuration,
            "-p:UseHexalithProjectReferences=" + ("false" if configuration == "Release" else "true"),
            "-p:UseNuGetDeps=" + ("true" if configuration == "Release" else "false"),
            "--filter", test_filter, "--results-directory", str(results),
            "--report-xunit-trx", "--report-xunit-trx-filename", "results.trx"]


def run_suite(root, project, test_filter, results, log_name, configuration="Debug"):
    results.mkdir(parents=True, exist_ok=True)
    for previous in results.glob("**/results.trx"):
        previous.unlink()
    log = results / log_name
    command = suite_command(project, test_filter, results, configuration)
    (results / "command.json").write_bytes(canonical({"command": command, **suite_mode(configuration)}))
    try:
        completed = subprocess.run(command, cwd=root, text=True, capture_output=True, timeout=3600)
    except subprocess.TimeoutExpired as error:
        log.write_text(str(error.stdout or "") + "\n" + str(error.stderr or ""))
        return 1, []
    log.write_text(completed.stdout + "\n" + completed.stderr)
    names = []
    try:
        for trx in results.glob("**/results.trx"):
            tree = ET.parse(trx)
            for node in tree.iter():
                if node.tag.endswith("UnitTestResult"):
                    names.append({"name": node.attrib.get("testName", ""), "outcome": node.attrib.get("outcome", "")})
            trx.unlink()
    except (ET.ParseError, OSError):
        return 1, []
    code = completed.returncode if completed.returncode != 0 or names else 1
    return code, names


def method_name(test_name):
    leaf = str(test_name).split("(", 1)[0].strip()
    return leaf.rsplit(".", 1)[-1]


def cell_records(names, started):
    if not started:
        return [{"name": name, "outcome": "NotRun"} for name in REQUIRED_CELLS]
    grouped = {name: [] for name in REQUIRED_CELLS}
    allowed = set(REQUIRED_CELLS)
    for item in names:
        method = method_name(item.get("name", ""))
        if method in allowed:
            grouped[method].append(item.get("outcome", ""))
    records = []
    for name in REQUIRED_CELLS:
        outcomes = grouped[name]
        if not outcomes:
            outcome = "Missing"
        elif all(item == "Passed" for item in outcomes):
            outcome = "Passed"
        else:
            outcome = "Failed"
        records.append({"name": name, "outcome": outcome})
    return records


def coverage_gaps(names, custody_code, guard_code, started):
    if not started:
        return []
    gaps = [record["name"] for record in cell_records(names, True) if record["outcome"] != "Passed"]
    if gaps:
        return gaps
    if custody_code != 0:
        gaps.append("custody")
    if guard_code != 0:
        gaps.append("guard")
    return gaps


def result_code(qualified, started, gaps):
    if qualified:
        return "ComponentQualification"
    if not started or gaps:
        return "DependencyNotAvailable"
    return "QualificationNotAccepted"


def result_fields(qualified, gaps, coverage, cells):
    return {
        "blocked": not qualified,
        "componentQualification": qualified,
        "coverageGaps": coverage,
        "dependency": "EXT-HOST-1",
        "dependencyAvailable": False,
        "effects": 0,
        "gap": gaps,
        "liveEffects": 0,
        "missingRuntimeInputs": list(MISSING_RUNTIME_INPUTS),
        "productionEngineRegistered": False,
        "requiredCells": cells,
    }


def write_evidence(evidence, category, gaps, suites, names, qualified, coverage, cells):
    matched = [item for item in names
               if method_name(item.get("name", "")) in REQUIRED_CELLS
               and any(token in method_name(item.get("name", "")) for token in CATEGORIES[category])]
    document = {
        "schema": "EXT-HOST-1.full-evidence.v1",
        "category": category,
        "requirements": ["H1", "H2", "H3", "H4", "v21", "v23"],
        "code": result_code(qualified, suites.get("started", False), gaps),
        "sources": ["Hexalith.Platform.Custody.Tests", "GovernanceScopeGuardTests"],
        "suites": suites,
        "matchedTests": matched,
    }
    document.update(result_fields(qualified, gaps, coverage, cells))
    (evidence / f"{category}.json").write_bytes(canonical(document))


def write_lost_ack(evidence, gaps, suites, names, qualified, coverage, cells):
    matched = [item for item in names
               if method_name(item.get("name", "")) in LOST_ACK_CELLS and item.get("outcome") == "Passed"]
    document = {
        "schema": "EXT-HOST-1.full-evidence.v1",
        "category": "lost-ack",
        "requirements": ["H1", "H2", "H3", "H4", "v21", "v23"],
        "code": result_code(qualified, suites.get("started", False), gaps),
        "sources": ["Hexalith.Platform.Custody.Tests", "GovernanceScopeGuardTests"],
        "suites": suites,
        "matchedTests": matched,
    }
    document.update(result_fields(qualified, gaps, coverage, cells))
    (evidence / "lost-ack.json").write_bytes(canonical(document))


def evidence_files(evidence):
    files = []
    if evidence.exists():
        for path in evidence.rglob("*"):
            if path.is_file() and "prior" not in path.relative_to(evidence).parts:
                files.append((path.relative_to(evidence).as_posix(), path))
    files.sort()
    return files


def hash_files(files):
    digest = hashlib.sha256()
    for name, path in files:
        digest.update(name.encode())
        digest.update(b"\0")
        digest.update(path.read_bytes())
        digest.update(b"\0")
    return digest.hexdigest()


def evidence_digest(evidence):
    return hash_files(evidence_files(evidence))


def required_cells_passed(evidence):
    path = Path(evidence) / "blocked.json"
    try:
        document = json.loads(path.read_text())
    except (OSError, UnicodeError, json.JSONDecodeError):
        return False
    cells = document.get("requiredCells") if isinstance(document, dict) else None
    if not isinstance(cells, list):
        return False
    passed = {item.get("name"): item.get("outcome") for item in cells if isinstance(item, dict)}
    return all(passed.get(name) == "Passed" for name in REQUIRED_CELLS)


def retain_prior_evidence(evidence):
    files = evidence_files(evidence)
    if not files:
        return None
    digest_hex = hash_files(files)
    destination = evidence / "prior" / digest_hex
    if destination.exists():
        return digest_hex
    destination.mkdir(parents=True)
    for name, path in files:
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(path.read_bytes())
    return digest_hex


def publish(evidence, gaps, suites, names, qualified, coverage):
    retain_prior_evidence(evidence)
    cells = cell_records(names, bool(suites.get("started")))
    for category in CATEGORIES:
        write_evidence(evidence, category, gaps, suites, names, qualified, coverage, cells)
    write_lost_ack(evidence, gaps, suites, names, qualified, coverage, cells)
    document = {
        "schema": "EXT-HOST-1.full-evidence.v1",
        "code": result_code(qualified, suites.get("started", False), gaps),
        "suites": suites,
    }
    document.update(result_fields(qualified, gaps, coverage, cells))
    (evidence / "blocked.json").write_bytes(canonical(document))


def suite_projects(root):
    custody_project = root / "tests/Hexalith.Platform.Custody.Tests/Hexalith.Platform.Custody.Tests.csproj"
    guard_project = root / "references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj"
    if not guard_project.exists():
        guard_project = root.parent / "eventstore/tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj"
    return custody_project, guard_project


def run_suites(root, artifacts, configuration="Debug"):
    custody_project, guard_project = suite_projects(root)
    custody_code, custody_names = run_suite(root, custody_project, CUSTODY_FILTER, artifacts / "custody", "custody.log", configuration)
    guard_code, guard_names = run_suite(
        root, guard_project, "FullyQualifiedName~GovernanceScopeGuardTests", artifacts / "guard", "guard.log", configuration)
    suites = {"custodyExitCode": custody_code, "guardExitCode": guard_code,
              "custodyTests": len(custody_names), "guardTests": len(guard_names), "started": True,
              **suite_mode(configuration)}
    return custody_code, guard_code, custody_names + guard_names, suites


def suites_not_started(configuration="Debug"):
    return {"custodyExitCode": None, "guardExitCode": None, "custodyTests": 0, "guardTests": 0, "started": False,
            **suite_mode(configuration)}


def profile_status(path):
    try:
        loaded = prepare.load_profile(path)
        missing = prepare.missing_references(loaded)
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError, TypeError):
        return None, "rejected"
    if missing:
        return loaded, "incomplete"
    return loaded, "complete"


def git_head(root):
    completed = subprocess.run(["git", "-C", str(root), "rev-parse", "HEAD"],
                               check=True, capture_output=True, text=True, timeout=30)
    head = completed.stdout.strip()
    if prepare.COMMIT.fullmatch(head) is None:
        raise ValueError("component target is not a commit")
    return head


def attempt_key(head, profile, configuration="Debug"):
    selected = {"head": head, "plan": prepare.plan(), "profile": profile}
    if configuration != "Debug":
        selected["buildMode"] = suite_mode(configuration)
    return hashlib.sha256(canonical(selected)).hexdigest()


def read_attempt(artifacts):
    path = artifacts / ATTEMPT_NAME
    if not path.exists():
        return None
    try:
        document = json.loads(path.read_text())
    except (OSError, UnicodeError, json.JSONDecodeError):
        return {"attemptKey": "", "exitCode": 1}
    exit_code = document.get("exitCode") if isinstance(document, dict) else None
    if not isinstance(document, dict) or not isinstance(document.get("attemptKey"), str) or isinstance(exit_code, bool) or not isinstance(exit_code, int):
        return {"attemptKey": "", "exitCode": 1}
    return document


def write_attempt(artifacts, key, head, profile, code, configuration="Debug"):
    document = {
        "attemptKey": key,
        "componentQualification": code == 0,
        "dependencyAvailable": False,
        "effects": 0,
        "evidenceDigest": evidence_digest(Path(artifacts) / "evidence"),
        "exitCode": code,
        "head": head,
        "liveEffects": 0,
        "missingRuntimeInputs": list(MISSING_RUNTIME_INPUTS),
        "planSha256": prepare.digest(prepare.plan()),
        "productionEngineRegistered": False,
        "profileSha256": prepare.digest(profile),
        "schema": "EXT-HOST-1.component-attempt.v1",
        **suite_mode(configuration),
    }
    (artifacts / ATTEMPT_NAME).write_bytes(canonical(document))


def refuse_profile(evidence, profile, configuration="Debug"):
    publish(evidence, binding_gaps(profile), suites_not_started(configuration), [], False, [])
    print("ERROR: " + REFUSAL, file=sys.stderr)
    return 1


def verify(root, artifacts, profile, configuration="Debug"):
    suite_mode(configuration)
    cohort_configuration = os.environ.get("PLATFORM_JOINT_COHORT_CONFIGURATION")
    if cohort_configuration:
        sys.path.insert(0, str(Path(root) / "eng/ext-secrets-1"))
        from joint_live_phase_runner import run_installed_cohort, Blocked
        try:
            # The CLI profile is an exact enrolled raw artifact, including formatting.
            from qualify import bounded_public_file, decode_json
            profile_bytes = bounded_public_file(profile, 1024 * 1024) if profile else b""
            supplied = decode_json(profile_bytes)
            joint = decode_json(bounded_public_file(cohort_configuration, 1024 * 1024))
            if not isinstance(supplied, dict) or hashlib.sha256(profile_bytes).hexdigest() != joint["binding"]["profileSha256"]:
                raise Blocked("ExactSuppliedAcceptedHostProfileBytes")
            result = run_installed_cohort(cohort_configuration, Path(root).parent / "agents/_bmad-output/planning-artifacts/external-dependency-register.md")
        except Blocked as error:
            destination = Path(artifacts) / "joint-cohort-failure.json"
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(canonical({"code":"DependencyNotAvailable", "records":["EXT-SECRETS-1","EXT-HOST-1"],
                "missingInputs":error.missing,"attemptState":error.attempt_state,"effectState":error.effect_state,"attemptId":error.attempt_id,"phaseRequestId":error.phase_request_id,
                "effectsStarted":error.effects_started,"qualificationEffects":"Unknown" if error.effects_started else 0,"dependencyAvailable":False}))
            return 1
        except Exception:
            destination = Path(artifacts) / "joint-cohort-failure.json"
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(canonical({"code":"DependencyNotAvailable","records":["EXT-SECRETS-1","EXT-HOST-1"],
                "missingInputs":["ValidCoordinatedInstallation"],"attemptState":"LookupRequired","effectState":"Unknown","effectsStarted":True,
                "qualificationEffects":"Unknown","originalLookupRequired":True,"dependencyAvailable":False}))
            return 1
        destination = Path(artifacts) / "joint-cohort-result.json"
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(canonical(result))
        return 0
    root, artifacts = Path(root).resolve(), Path(artifacts).resolve()
    evidence = artifacts / "evidence"
    evidence.mkdir(parents=True, exist_ok=True)
    if profile:
        loaded, status = profile_status(profile)
        if status != "complete":
            if read_attempt(artifacts) is not None:
                print("ERROR: " + REFUSAL, file=sys.stderr)
                return 1
            return refuse_profile(evidence, profile, configuration)
        try:
            head = git_head(root)
        except (OSError, subprocess.SubprocessError, ValueError):
            print("ERROR: EXT-HOST-1 component target is not a commit.", file=sys.stderr)
            return 1
        key = attempt_key(head, loaded, configuration)
        stored = read_attempt(artifacts)
        if stored is not None:
            if stored.get("attemptKey") == key:
                if stored.get("evidenceDigest") != evidence_digest(evidence):
                    print("ERROR: EXT-HOST-1 evidence digest does not match the persisted attempt.", file=sys.stderr)
                    return 1
                code = int(stored["exitCode"])
                if code == 0 and not required_cells_passed(evidence):
                    print("ERROR: EXT-HOST-1 evidence does not show every required cell Passed.", file=sys.stderr)
                    return 1
                return code
            print("ERROR: EXT-HOST-1 keeps the first complete attempt.", file=sys.stderr)
            return 1
        custody_code, guard_code, names, suites = run_suites(root, artifacts, configuration)
        gaps = binding_gaps(profile)
        coverage = coverage_gaps(names, custody_code, guard_code, True)
        qualified = not gaps and not coverage and custody_code == 0 and guard_code == 0
        publish(evidence, gaps, suites, names, qualified, coverage)
        code = 0 if qualified else 1
        write_attempt(artifacts, key, head, loaded, code, configuration)
        if qualified:
            return 0
        if custody_code != 0 or guard_code != 0:
            print("ERROR: EXT-HOST-1 evidence suites failed.", file=sys.stderr)
        print("ERROR: EXT-HOST-1 component qualification incomplete.", file=sys.stderr)
        return 1
    if read_attempt(artifacts) is not None:
        print("ERROR: " + REFUSAL, file=sys.stderr)
        return 1
    custody_code, guard_code, names, suites = run_suites(root, artifacts, configuration)
    gaps = binding_gaps(profile)
    coverage = coverage_gaps(names, custody_code, guard_code, True)
    publish(evidence, gaps, suites, names, False, coverage)
    if custody_code != 0 or guard_code != 0:
        print("ERROR: EXT-HOST-1 evidence suites failed.", file=sys.stderr)
    print("ERROR: " + REFUSAL, file=sys.stderr)
    return 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--artifacts-path", required=True)
    parser.add_argument("--profile")
    parser.add_argument("--root")
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Debug")
    args = parser.parse_args()
    root = args.root or Path(__file__).resolve().parents[2]
    return verify(root, args.artifacts_path, args.profile, args.configuration)


if __name__ == "__main__":
    raise SystemExit(main())
