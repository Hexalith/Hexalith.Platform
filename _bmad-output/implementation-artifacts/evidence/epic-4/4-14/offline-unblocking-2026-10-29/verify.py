"""Recheck the reviewed current 4.14 cut; never execute its operational argv.

Default: read-only verification with scratch outputs removed at exit.
--receipt-dir records local transcripts and hashes, without operational authority.
Fixed fingerprints retain reviewed wording/provenance, not generic semantic proof.
"""

import argparse
import ast
import contextlib
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import re
import sys
import tempfile
import unittest

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[5]
FOUNDATION = "eng/staging-foundation"
EVIDENCE = "_bmad-output/implementation-artifacts/evidence/epic-4/4-14/offline-unblocking-2026-10-29"
BASELINE = "2a18cdd33fcb609a563011e9bfd4153f56445c63"
RESOLVED = {"network.dns.namespace", "network.dns.podLabels"}
DEPENDENCIES = {"4-1", "4-2", "4-3", "4-27", "4-28"}
PRESERVATION_SHA256 = "8ac774279db064f7aef72f87120b9a78b01d6dddda210092f8044e3dfac30502"
CONTRACT_SHA256 = "2213f81ac8df5c974e036967d7629de14d1371d68c9de82854c29188b8cd19ff"
CONTRACT_KEYS = ("ownerDecisionGroups", "phasePrerequisites", "upstreamOwnerDecisions", "downstreamPreparation")
TRANSCRIPTS = ("foundation-tests.stdout.txt", "foundation-tests.stderr.txt", "handoff-tests.stdout.txt", "handoff-tests.stderr.txt", "secrets-tests.stdout.txt", "secrets-tests.stderr.txt", "render.json", "render.stdout.txt", "render.stderr.txt", "plan.json", "plan.stdout.txt", "plan.stderr.txt")
ARTIFACT_PATHS = tuple(FOUNDATION + "/" + name for name in ("README.md", "UNBLOCKING-REPORT.md", "unblocking.assessed.json")) + tuple(EVIDENCE + "/" + name for name in (*TRANSCRIPTS, "preserved-files.json", "verify.py"))
LOGS = {}
BLOCKED_AUDIT_EVENTS = []


def require(condition, message):
    if not condition:
        raise ValueError(message)


def guard(event, args):
    if event.startswith(("subprocess.", "socket.", "os.exec", "os.spawn", "os.fork", "os.posix_spawn")) or event == "os.system":
        BLOCKED_AUDIT_EVENTS.append(event)
        raise PermissionError("offline verifier refused process/network event: " + event)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_json(path):
    return json.loads(path.read_text())


def local_path(root, path):
    require(isinstance(path, str) and path != "", "missing evidence path")
    p = Path(path)
    require(not p.is_absolute() and ".." not in p.parts, "evidence path must stay within repository")
    result = (root / p).resolve()
    require(result.is_relative_to(root.resolve()), "evidence symlink escapes repository")
    require(result.is_file(), "missing evidence: " + path)
    return result


def pointer(document, reference):
    require(isinstance(reference, str) and (reference == "" or reference.startswith("/")), "bad JSON pointer")
    node = document
    for token in reference.split("/")[1:]:
        require(re.search(r"~(?![01])", token) is None, "bad JSON pointer escape")
        token = token.replace("~1", "/").replace("~0", "~")
        if isinstance(node, list):
            require(re.fullmatch(r"0|[1-9][0-9]*", token) is not None, "bad JSON array index")
            require(int(token) < len(node), "JSON array index outside source")
            node = node[int(token)]
        else:
            require(isinstance(node, dict) and token in node, "JSON pointer absent from source")
            node = node[token]
    return node


def validate_source(source, root):
    path = local_path(root, source["path"])
    require(re.fullmatch(r"[0-9a-f]{64}", source.get("sha256", "")) is not None, "invalid source digest")
    require(sha(path) == source["sha256"], "stale source hash: " + source["path"])
    if source.get("pointer") is not None:
        value = pointer(read_json(path), source["pointer"])
        if "expectedValue" in source:
            require(type(value) is type(source["expectedValue"]) and value == source["expectedValue"], "source value disagrees with claim")
    ranges = []
    if source.get("lineStart") is not None or source.get("lineEnd") is not None:
        ranges.append((source.get("lineStart"), source.get("lineEnd")))
    if "lines" in source:
        legacy = source["lines"]
        if type(legacy) is int:
            ranges.append((legacy, legacy))
        else:
            require(isinstance(legacy, str) and re.fullmatch(r"[1-9][0-9]*-[1-9][0-9]*", legacy) is not None, "invalid legacy source lines")
            ranges.append(tuple(int(value) for value in legacy.split("-")))
    for start, end in ranges:
        require(type(start) is int and type(end) is int and 1 <= start <= end <= len(path.read_text().splitlines()), "invalid source line range")


def citations(node):
    if isinstance(node, dict):
        if "path" in node and "sha256" in node:
            yield node
        for value in node.values():
            yield from citations(value)
    elif isinstance(node, list):
        for value in node:
            yield from citations(value)


def load_module(name, path):
    preflight(ROOT)
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def protected_files(root):
    manifest = root / EVIDENCE / "preserved-files.json"
    require(sha(manifest) == PRESERVATION_SHA256, "reviewed preservation manifest changed")
    receipt = read_json(manifest)
    require(receipt["baselineCommit"] == BASELINE, "protected baseline changed")
    paths = [item["path"] for item in receipt["files"]]
    require(len(paths) == len(set(paths)) == 69, "exactly 69 reviewed protected sources required")
    for item in receipt["files"]:
        validate_source(item, root)
    # Six inherited foundation pins and all other 4.15 citations remain usable.
    pins = read_json(root / "eng/staging-secrets/source-evidence.json")
    for item in pins["sources"]:
        validate_source(item, root)
    require(sum(s["id"].startswith("foundation-") for s in pins["sources"]) == 6, "six foundation pins required")
    return len(paths)


def source_import_boundaries(paths):
    banned = {"subprocess", "socket", "urllib", "http", "requests", "ssl", "asyncio", "multiprocessing", "ctypes"}
    for path in paths:
        for node in ast.walk(ast.parse(path.read_text())):
            modules = [a.name for a in node.names] if isinstance(node, ast.Import) else [node.module or ""] if isinstance(node, ast.ImportFrom) else []
            for module in modules:
                require(module.split(".")[0] not in banned, "forbidden source import: " + str(path))


def preflight(root):
    protected_files(root)
    source_import_boundaries([root / FOUNDATION / "render.py", root / FOUNDATION / "cni_probe.py", root / "eng/staging-secrets/prepare.py", Path(__file__)])


def validate_saved_receipt(saved, root, check_hashes=True):
    require(saved["kind"] == "offline-unblocking-verification" and saved["baselineCommit"] == BASELINE and saved["status"] == "BLOCKED_NOT_FOR_EXECUTION", "saved receipt cut/status changed")
    for key in ("approvalGranted", "liveAcceptance", "newCollectionPerformed", "unexpectedProcessOrNetworkEvents"):
        require(saved[key] is False, "saved receipt " + key + " must remain false")
    require(saved["passed"] is True and saved["protectedFiles"] == 69, "saved offline outcome is not a successful reviewed cut")
    require(set(saved["checks"]) == {"handoff", "foundation", "secrets"}, "saved offline suites changed")
    for name, count in (("handoff", 8), ("foundation", 34), ("secrets", 48)):
        expected = {"testsRun": count, "failures": 0, "errors": 0, "skipped": 0, "passed": True}
        actual = saved["checks"][name]
        require(actual == expected and all(type(actual[key]) is type(value) for key, value in expected.items()), "saved " + name + " outcome failed or changed")
    require(set(saved["artifactSha256"]) == set(ARTIFACT_PATHS) and len(saved["artifactSha256"]) == 17, "saved artifact inventory must contain the exact 17 reviewed paths")
    require(set(saved["moduleAgnosticArtifacts"]) == set(ARTIFACT_PATHS) | {EVIDENCE + "/verification.json"} and len(saved["moduleAgnosticArtifacts"]) == 18, "saved module boundary inventory changed")
    require(sorted(saved["guardSelfTestEvents"]) == sorted(["subprocess.Popen", "socket.connect", "os.system"]), "saved guard outcomes changed")
    if check_hashes:
        for path in ARTIFACT_PATHS:
            require(sha(local_path(root, path)) == saved["artifactSha256"][path], "saved artifact hash changed: " + path)


def receipt_destinations(directory, names, evidence_dir=HERE):
    require(not directory.is_symlink() and directory.is_dir() and directory.resolve() == evidence_dir.resolve(), "receipt directory must be the evidence directory, without a symlink")
    targets = {}
    for name in names:
        require(Path(name).name == name and name not in ("", ".", ".."), "invalid receipt filename")
        target = directory / name
        require(not target.is_symlink() and target.parent.resolve() == evidence_dir.resolve() and target.resolve().parent == evidence_dir.resolve(), "receipt destination symlink or escaping parent: " + name)
        require(not target.exists() or target.is_file(), "receipt destination is not a file: " + name)
        targets[name] = target
    return targets


def write_receipts(directory, contents, evidence_dir=HERE):
    targets = receipt_destinations(directory, contents, evidence_dir)
    # Validate the complete write set before touching even its first file.
    for name, content in contents.items():
        targets[name].write_text(content)


def scan_artifacts(paths):
    forbidden = "".join(("time", "sheets"))
    for path in paths:
        require(forbidden not in path.read_text().lower(), "consumer module named by new artifact: " + str(path))


def validate_assessment(data, root, renderer):
    require(data["baselineCommit"] == BASELINE, "assessment baseline changed")
    require((data["status"], data["story"], data["storyStatus"]) == ("BLOCKED_NOT_FOR_EXECUTION", "4-14", "in-progress"), "assessment status advanced")
    for key in ("approvalGranted", "liveAcceptance", "executable", "newCollectionPerformed", "manifestBytesAvailable", "applyPlanAvailable"):
        require(data[key] is False, key + " must remain false")
    contract = {key: data[key] for key in CONTRACT_KEYS}
    require(hashlib.sha256(json.dumps(contract, sort_keys=True, separators=(",", ":")).encode()).hexdigest() == CONTRACT_SHA256, "reviewed owner/prerequisite/history contract changed")
    for source in citations(data):
        validate_source(source, root)
    inputs = read_json(root / FOUNDATION / "inputs.resolved.json")
    items = data["fieldInventory"]
    require([i["field"] for i in items] == list(renderer.REQUIRED), "field inventory is not exactly the renderer's 37 required fields")
    require((data["requiredFieldCount"], data["resolvedFieldCount"], data["missingFieldCount"]) == (37, 2, 35), "field counts changed")
    missing = {path for path in renderer.REQUIRED if renderer._get(inputs, path) is None}
    require(missing == set(renderer.REQUIRED) - RESOLVED and len(missing) == 35, "actual null set changed")
    question_lines = (root / FOUNDATION / "MISSING-INPUTS.md").read_text().splitlines()
    questions = {}
    for line, text in enumerate(question_lines, 1):
        match = re.fullmatch(r"\| (\d+) \| `([^`]+)` \| (.*?) \|", text)
        if match:
            questions[match[2]] = (int(match[1]), match[3], line)
    require(set(questions) == missing, "individual questions disagree with null set")
    for item in items:
        path = item["field"]
        require(item["kind"] == renderer.REQUIRED[path], "field kind changed")
        require(item["value"] == renderer._get(inputs, path), "inventory adopted an unsupported value")
        if path in missing:
            require(item["state"] == "unresolved" and item["ownerDecision"] is None, "missing choice became an owner decision")
            require((item["questionNumber"], item["question"], item["questionLine"]) == questions[path], "question citation changed")
            require(item["questionRef"] == FOUNDATION + "/MISSING-INPUTS.md", "question reference is not the immutable question table")
        else:
            require(item["state"] == "observed-only" and item["provenance"] == inputs["_provenance"][path], "resolved provenance changed")
            for source in item["provenance"]["sources"]:
                require(pointer(read_json(root / source["path"]), source["pointer"]) == item["value"], "resolved value differs from source")
    groups = data["ownerDecisionGroups"]
    require(len(groups) == 4 and all(g["answer"] is None for g in groups), "owner groups must stay unanswered")
    numbers = [n for g in groups for n in g["questionNumbers"]]
    require(sorted(numbers) == list(range(1, 36)), "groups do not partition the 35 questions")
    for item in items:
        if item["field"] in missing:
            require(any(g["id"] == item["ownerGroup"] and item["questionNumber"] in g["questionNumbers"] for g in groups), "field assigned to wrong owner group")
    old = read_json(root / FOUNDATION / "gates.assessed.json")
    require(data["dependencies"] == old["dependencies"], "dependency assessment changed without authoritative completion")
    require({d["story"] for d in data["dependencies"]} == DEPENDENCIES, "dependency set changed")
    require(all(d["acceptedForLiveApply"] is False for d in data["dependencies"]), "dependency acceptance advanced")
    require(data["acceptedSeparateQualification"] == old["acceptedSeparateQualification"], "4.26 original scope changed")
    require(data["nativeAccess"] == old["nativeAccess"] and data["liveCniEnforcement"] == old["liveCniEnforcement"], "dated native/CNI scope changed")
    sprint = (root / "_bmad-output/implementation-artifacts/sprint-status.yaml").read_text()
    for dependency in data["dependencies"]:
        require(re.search(r"^  " + re.escape(dependency["sprintKey"]) + ": " + re.escape(dependency["sprintStatus"]) + r"$", sprint, re.M) is not None, "dependency sprint status differs")
    require(re.search(r"^  4-14-[^:]+: in-progress$", sprint, re.M) is not None, "Story 4.14 sprint status changed")
    phases = data["phasePrerequisites"]
    require([p["phase"] for p in phases] == ["before-render", "before-live-authorization", "separately-authorized-live-evidence"], "prerequisite phases changed")
    expected = [
        {"resolve-fields", "quantity-validation", "empty-storage-parameters", "storage-enforcement", "priority-enforcement", "rbac-definitions", "dns-datapath", "per-service-tls-flows"},
        {"dependency-acceptance", "exact-target-argv", "review-exact-bytes", "probe-and-recovery-plan", "provider-bootstrap-sequence"},
        {"live-target-and-cni", "ordered-layer-operations", "full-isolation-evidence", "actual-provider-tls-acceptance", "operational-acceptance"},
    ]
    for phase, ids in zip(phases, expected):
        require({r["id"] for r in phase["requirements"]} == ids, "prerequisite missing from " + phase["phase"])
        for requirement in phase["requirements"]:
            require(requirement["state"] in ("not-run",) if phase is phases[-1] else requirement["state"].startswith("pending"), "prerequisite falsely complete")
            require(bool(requirement["requirement"].strip()), "empty prerequisite")
    downstream = data["downstreamPreparation"]
    require(downstream["story"] == "4-15" and downstream["foundationGate"] is False and downstream["suppliesNewFoundationChoices"] is False, "downstream preparation became a gate or owner choice")
    upstream = data["upstreamOwnerDecisions"]
    require(len(upstream) == 1 and upstream[0]["story"] == "4-28" and upstream[0]["state"] == "pending" and len(upstream[0]["decisions"]) == 3, "exact pending upstream decision inventory required")


class HandoffChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.renderer = load_module("render", ROOT / FOUNDATION / "render.py")
        load_module("cni_probe", ROOT / FOUNDATION / "cni_probe.py")
        cls.assessment = read_json(ROOT / FOUNDATION / "unblocking.assessed.json")

    def test_current_cut_exact_fields_questions_sources_and_boundaries(self):
        validate_assessment(self.assessment, ROOT, self.renderer)
        self.assertGreater(protected_files(ROOT), 0)

    def test_changed_source_bytes_and_stale_hash_are_refused(self):
        source = copy.deepcopy(self.assessment["sources"][0])
        source["sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "stale source hash"):
            validate_source(source, ROOT)
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            path = root / "source.json"
            path.write_text('{"value": false}\n')
            source = {"path": "source.json", "sha256": sha(path), "pointer": "/value", "expectedValue": False}
            path.write_text('{"value": true}\n')
            with self.assertRaisesRegex(ValueError, "stale source hash"):
                validate_source(source, root)
            manifest = root / EVIDENCE / "preserved-files.json"
            manifest.parent.mkdir(parents=True)
            manifest.write_bytes((ROOT / EVIDENCE / "preserved-files.json").read_bytes() + b"\n")
            with self.assertRaisesRegex(ValueError, "reviewed preservation manifest"):
                protected_files(root)

    def test_bad_pointer_and_wrong_source_claim_are_refused(self):
        source = copy.deepcopy(next(s for s in self.assessment["sources"] if "pointer" in s))
        for invalid in ("/absent", "/~2invalid", "invalid"):
            source["pointer"] = invalid
            with self.assertRaises(ValueError):
                validate_source(source, ROOT)
        source = copy.deepcopy(next(s for s in self.assessment["sources"] if "pointer" in s))
        source["expectedValue"] = "unsupported"
        with self.assertRaisesRegex(ValueError, "source value"):
            validate_source(source, ROOT)
        source = copy.deepcopy(self.assessment["sources"][0])
        for valid in (1, "1-2"):
            validate_source({**source, "lines": valid}, ROOT)
        for invalid in (0, -1, True, None, 1000000, "1", "bad", "2-1", "0-1", "1-1000000"):
            with self.assertRaises(ValueError):
                validate_source({**source, "lines": invalid}, ROOT)
        for start, end in ((0, 1), (2, 1), (1, 1000000), (True, 2), (None, 2)):
            with self.assertRaises(ValueError):
                validate_source({**source, "lineStart": start, "lineEnd": end}, ROOT)

    def test_approval_dependency_live_and_status_mutations_are_refused(self):
        mutations = [(key,) for key in ("approvalGranted", "liveAcceptance", "executable", "newCollectionPerformed", "manifestBytesAvailable", "applyPlanAvailable")]
        for keys in mutations:
            bad = copy.deepcopy(self.assessment)
            bad[keys[0]] = True
            with self.assertRaises(ValueError):
                validate_assessment(bad, ROOT, self.renderer)
        for index in range(5):
            bad = copy.deepcopy(self.assessment)
            bad["dependencies"][index]["acceptedForLiveApply"] = True
            with self.assertRaises(ValueError):
                validate_assessment(bad, ROOT, self.renderer)
        for key, value in (("storyStatus", "done"), ("status", "APPROVED")):
            bad = copy.deepcopy(self.assessment)
            bad[key] = value
            with self.assertRaises(ValueError):
                validate_assessment(bad, ROOT, self.renderer)
        for key in ("foundationGate", "suppliesNewFoundationChoices"):
            bad = copy.deepcopy(self.assessment)
            bad["downstreamPreparation"][key] = True
            with self.assertRaises(ValueError):
                validate_assessment(bad, ROOT, self.renderer)
        saved = read_json(HERE / "verification.json")
        validate_saved_receipt(saved, ROOT, check_hashes=False)
        for key, value in (("approvalGranted", True), ("liveAcceptance", True), ("newCollectionPerformed", True), ("unexpectedProcessOrNetworkEvents", True), ("passed", False), ("status", "APPROVED"), ("baselineCommit", "0" * 40)):
            bad = copy.deepcopy(saved)
            bad[key] = value
            with self.assertRaises(ValueError):
                validate_saved_receipt(bad, ROOT, check_hashes=False)
        for name in ("handoff", "foundation", "secrets"):
            for key, value in (("testsRun", 0), ("failures", 1), ("errors", 1), ("skipped", 1), ("passed", False)):
                bad = copy.deepcopy(saved)
                bad["checks"][name][key] = value
                with self.assertRaises(ValueError):
                    validate_saved_receipt(bad, ROOT, check_hashes=False)
        for change in ("omit", "extra"):
            bad = copy.deepcopy(saved)
            if change == "omit":
                bad["artifactSha256"].pop(ARTIFACT_PATHS[0])
            else:
                bad["artifactSha256"]["unreviewed.txt"] = "0" * 64
            with self.assertRaises(ValueError):
                validate_saved_receipt(bad, ROOT, check_hashes=False)

    def test_unanswered_choice_question_and_phase_mutations_are_refused(self):
        for change in ("value", "question", "phase", "omit-upstream", "omit-upstream-member", "waive-question", "waive-prerequisite", "fabricated-history"):
            bad = copy.deepcopy(self.assessment)
            if change == "value":
                bad["fieldInventory"][0]["value"] = "invented"
            elif change == "question":
                bad["fieldInventory"][0]["questionLine"] = 1
            elif change == "phase":
                bad["phasePrerequisites"][0]["requirements"].pop()
            elif change == "omit-upstream":
                bad["upstreamOwnerDecisions"] = []
            elif change == "omit-upstream-member":
                bad["upstreamOwnerDecisions"][0]["decisions"].pop()
            elif change == "waive-question":
                bad["ownerDecisionGroups"][0]["question"] = "No owner decision required."
            elif change == "waive-prerequisite":
                bad["phasePrerequisites"][0]["requirements"][0]["requirement"] = "Waived."
            else:
                bad["downstreamPreparation"]["history"]["suppliedCommit"] = "0" * 40
            with self.assertRaises(ValueError):
                validate_assessment(bad, ROOT, self.renderer)

    def test_actual_inputs_render_and_plan_refuse_without_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            output = Path(tmp) / "manifests"
            commands = {
                "render": ["render", "--inputs", str(ROOT / FOUNDATION / "inputs.resolved.json"), "--output", str(output)],
                "plan": ["plan", "--inputs", str(ROOT / FOUNDATION / "inputs.resolved.json"), "--observed-context", self.renderer.EXPECTED_CONTEXT, "--observed-cluster-uid", self.renderer.EXPECTED_UID],
            }
            for name, args in commands.items():
                stdout, stderr = io.StringIO(), io.StringIO()
                with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                    code = self.renderer.main(args)
                LOGS[name + ".stdout.txt"] = stdout.getvalue()
                LOGS[name + ".stderr.txt"] = stderr.getvalue()
                LOGS[name + ".json"] = json.dumps({"execution": "render.main(argv), in process; no child process", "entrypoint": FOUNDATION + "/render.py", "argv": args, "exitCode": code, "stdoutEmpty": stdout.getvalue() == "", "outputDirectoryCreated": output.exists(), "approvalGranted": False, "liveAcceptance": False}, indent=2) + "\n"
                self.assertEqual(code, 2)
                self.assertEqual(stdout.getvalue(), "")
                self.assertFalse(output.exists())
                self.assertEqual(len(stderr.getvalue().splitlines()), 35)

    def test_process_network_guard_and_source_import_boundaries(self):
        for event in ("subprocess.Popen", "socket.connect", "os.system"):
            with self.assertRaises(PermissionError):
                sys.audit(event)
        preflight(ROOT)
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            unsafe = root / "unsafe.py"
            unsafe.write_text("import socket\n")
            with self.assertRaisesRegex(ValueError, "forbidden source import"):
                source_import_boundaries([unsafe])
            evidence = root / "evidence"
            evidence.mkdir()
            outside = root / "outside.txt"
            outside.write_text("untouched outside sentinel")
            first = evidence / "first.stdout.txt"
            first.write_text("untouched first transcript")
            for name in ("handoff-tests.stderr.txt", "verification.json"):
                destination = evidence / name
                destination.symlink_to(outside)
                with self.assertRaisesRegex(ValueError, "symlink or escaping parent"):
                    write_receipts(evidence, {first.name: "overwritten", name: "escaping write"}, evidence)
                self.assertEqual(first.read_text(), "untouched first transcript")
                self.assertEqual(outside.read_text(), "untouched outside sentinel")
                destination.unlink()
            alias = root / "alias"
            alias.symlink_to(evidence, target_is_directory=True)
            with self.assertRaises(ValueError):
                receipt_destinations(alias, [first.name], evidence)
            with self.assertRaises(ValueError):
                receipt_destinations(evidence, ["../outside.txt"], evidence)

    def test_all_new_artifacts_are_module_agnostic_and_direction_is_one_way(self):
        paths = [ROOT / FOUNDATION / name for name in ("README.md", "unblocking.assessed.json", "UNBLOCKING-REPORT.md")]
        paths += sorted(p for p in (ROOT / EVIDENCE).rglob("*") if p.is_file() and "__pycache__" not in p.parts)
        scan_artifacts(paths)
        direction = (ROOT / FOUNDATION / "DEPENDENCY-DIRECTION.md").read_text()
        self.assertIn("consumer module → Platform staging foundation", direction)
        self.assertIn("never", direction)


def run_suite(name, suite, expected):
    stdout, stderr = io.StringIO(), io.StringIO()
    with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
        result = unittest.TextTestRunner(stream=stderr, verbosity=2).run(suite)
    LOGS[name + ".stdout.txt"] = stdout.getvalue()
    LOGS[name + ".stderr.txt"] = stderr.getvalue()
    return {"testsRun": result.testsRun, "failures": len(result.failures), "errors": len(result.errors), "skipped": len(result.skipped), "passed": result.wasSuccessful() and not result.skipped and result.testsRun == expected}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--receipt-dir", type=Path, help="optional local transcript destination")
    args = parser.parse_args(argv)
    sys.addaudithook(guard)
    if args.receipt_dir:
        receipt_destinations(args.receipt_dir, (*TRANSCRIPTS, "verification.json"))
    else:
        validate_saved_receipt(read_json(HERE / "verification.json"), ROOT)
    preflight(ROOT)
    checks = {}
    checks["handoff"] = run_suite("handoff-tests", unittest.defaultTestLoader.loadTestsFromTestCase(HandoffChecks), 8)
    for name, directory, count in (("foundation", FOUNDATION, 34), ("secrets", "eng/staging-secrets", 48)):
        suite = unittest.TestLoader().discover(str(ROOT / directory))
        checks[name] = run_suite(name + "-tests", suite, count)
    success = all(check["passed"] for check in checks.values())
    # Only three deliberately injected audit events are permitted; no real call is attempted.
    guard_ok = sorted(BLOCKED_AUDIT_EVENTS) == sorted(["subprocess.Popen", "socket.connect", "os.system"])
    success = success and guard_ok
    receipt = {"kind": "offline-unblocking-verification", "baselineCommit": BASELINE, "status": "BLOCKED_NOT_FOR_EXECUTION", "approvalGranted": False, "liveAcceptance": False, "newCollectionPerformed": False, "passed": success, "checks": checks, "guardSelfTestEvents": BLOCKED_AUDIT_EVENTS, "unexpectedProcessOrNetworkEvents": not guard_ok, "renderAndPlanExecution": "real Python entrypoints invoked in process with actual inputs; generated argv never executed", "protectedFiles": len(read_json(ROOT / EVIDENCE / "preserved-files.json")["files"]), "syntheticFixtures": "Existing suites only, outputs temporary; never target manifests."}
    if args.receipt_dir:
        require(set(LOGS) == set(TRANSCRIPTS), "complete reviewed transcript set required")
        paths = [ROOT / path for path in ARTIFACT_PATHS]
        scan_artifacts(paths)
        receipt["artifactSha256"] = {path: hashlib.sha256(LOGS[Path(path).name].encode()).hexdigest() if path.startswith(EVIDENCE + "/") and Path(path).name in LOGS else sha(ROOT / path) for path in ARTIFACT_PATHS}
        receipt["artifactHashExclusion"] = "verification.json excludes its own digest to avoid self-reference"
        receipt["moduleAgnosticArtifacts"] = [str(p.relative_to(ROOT)) for p in paths] + [EVIDENCE + "/verification.json"]
        contents = {**LOGS, "verification.json": json.dumps(receipt, indent=2) + "\n"}
        forbidden = "".join(("time", "sheets"))
        require(all(forbidden not in text.lower() for text in contents.values()), "consumer module named by generated receipt")
        write_receipts(args.receipt_dir, contents)
    print(json.dumps(receipt, indent=2))
    if not success:
        for name, content in LOGS.items():
            if name.endswith("stderr.txt"):
                print(name + "\n" + content, file=sys.stderr)
    return 0 if success else 1


if __name__ == "__main__":
    raise SystemExit(main())
