import json
import os
import stat
import subprocess
import sys
import tempfile
import unittest
from io import StringIO
from pathlib import Path
from unittest.mock import patch

import prepare_acceptance as prepare
import verify_full as verify

TRX = """<?xml version="1.0" encoding="utf-8"?>
<TestRun>
  <Results>
    <UnitTestResult testName="{name}" outcome="Passed" />
  </Results>
</TestRun>
"""


def write_trx(results, name):
    results.mkdir(parents=True, exist_ok=True)
    (results / "results.trx").write_text(TRX.format(name=name))


class VerifyFullTests(unittest.TestCase):
    def test_release_configuration_reaches_both_real_full_probe_compile_commands(self):
        platform = Path(__file__).resolve().parents[2]
        script = platform / "eng/verify-agents-host.sh"
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            commands = root / "bin"
            commands.mkdir()
            captured = root / "dotnet-commands.jsonl"
            # Bypass only prerequisite Python checks. The real shell -> verify_full -> run_suites ->
            # run_suite call chain executes, with only dotnet itself replaced at the process boundary.
            python = commands / "python3"
            python.write_text('#!/bin/sh\ncase "$1" in *verify-platform-pins.py|-m) exit 0 ;; esac\nexec "$REAL_PYTHON" "$@"\n')
            dotnet = commands / "dotnet"
            dotnet.write_text('#!/usr/bin/env python3\nimport json, os, sys\n'
                              'with open(os.environ["CAPTURED_DOTNET"], "a") as output:\n'
                              ' output.write(json.dumps(sys.argv[1:]) + "\\n")\n'
                              'sys.exit(17)\n')
            python.chmod(stat.S_IRWXU)
            dotnet.chmod(stat.S_IRWXU)
            env = os.environ | {"PATH": str(commands) + os.pathsep + os.environ["PATH"],
                                "REAL_PYTHON": sys.executable, "CAPTURED_DOTNET": str(captured)}
            for configuration in ["Debug", "Release"]:
                captured.unlink(missing_ok=True)
                options = [] if configuration == "Debug" else ["--configuration", configuration]
                artifacts = root / configuration
                result = subprocess.run(["bash", script, "--mode", "Full", *options, "--artifacts-path", artifacts],
                                        env=env, capture_output=True, text=True)
                self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
                self.assertIn(verify.REFUSAL, result.stderr)
                executed = [json.loads(line) for line in captured.read_text().splitlines()]
                self.assertEqual(len(executed), 2)
                self.assertEqual({Path(command[1]).stem for command in executed},
                                 {"Hexalith.Platform.Custody.Tests", "Hexalith.EventStore.Server.Tests"})
                for command in executed:
                    self.assertEqual(command[command.index("--configuration") + 1], configuration)
                    self.assertIn("-p:UseHexalithProjectReferences=" + ("false" if configuration == "Release" else "true"), command)
                    self.assertIn("-p:UseNuGetDeps=" + ("true" if configuration == "Release" else "false"), command)
                recorded = json.loads((artifacts / "evidence/blocked.json").read_text())["suites"]
                self.assertEqual(recorded["configuration"], configuration)
                self.assertEqual(recorded["dependencyMode"], "public-packages" if configuration == "Release" else "source-projects")
                for lane in ["custody", "guard"]:
                    evidence = json.loads((artifacts / lane / "command.json").read_text())
                    self.assertEqual(evidence["configuration"], configuration)
                    self.assertIn(evidence["command"], [["dotnet", *command] for command in executed])

    def test_non_object_and_pending_are_binding_gaps(self):
        with tempfile.TemporaryDirectory() as temp:
            array = Path(temp) / "array.json"
            array.write_text("[1]")
            self.assertEqual(list(verify.BINDING_GAPS), verify.binding_gaps(array))
            pending = Path(temp) / "pending.json"
            pending.write_text(json.dumps({
                "partiesBinding": "pending",
                "conversationsBinding": "PENDING",
                "protectionEngine": "ref:engine",
                "protectionObserver": " pending ",
            }))
            gaps = verify.binding_gaps(pending)
            self.assertEqual(["partiesBinding", "conversationsBinding", "protectionObserver"], gaps)

    def test_blocked_evidence_records_zero_effects_and_binding_gaps(self):
        with tempfile.TemporaryDirectory() as temp:
            artifacts = Path(temp) / "artifacts"

            def succeed(command, cwd, text, capture_output, timeout):
                results = Path(command[command.index("--results-directory") + 1])
                write_trx(results, "GovernanceScopeGuardTests.AclFence")
                return subprocess.CompletedProcess(command, 0, stdout="passed", stderr="")

            with patch.object(verify.subprocess, "run", side_effect=succeed):
                stderr = StringIO()
                with patch.object(verify.sys, "stderr", stderr):
                    code = verify.verify(Path(__file__).resolve().parents[2], artifacts, None)
            self.assertEqual(1, code)
            self.assertIn(verify.REFUSAL, stderr.getvalue())
            blocked = json.loads((artifacts / "evidence" / "blocked.json").read_text())
            self.assertTrue(blocked["blocked"])
            self.assertEqual(0, blocked["effects"])
            self.assertEqual(0, blocked["liveEffects"])
            self.assertEqual(list(verify.BINDING_GAPS), blocked["gap"])
            self.assertFalse(blocked["dependencyAvailable"])
            self.assertEqual(list(verify.MISSING_RUNTIME_INPUTS), blocked["missingRuntimeInputs"])
            self.assertFalse(blocked["componentQualification"])
            lost = json.loads((artifacts / "evidence" / "lost-ack.json").read_text())
            self.assertEqual(0, lost["effects"])
            self.assertEqual(list(verify.MISSING_RUNTIME_INPUTS), lost["missingRuntimeInputs"])
            self.assertFalse(lost["componentQualification"])
            for name in ("acl", "isolation", "outage", "restart", "restore"):
                document = json.loads((artifacts / "evidence" / f"{name}.json").read_text())
                self.assertEqual(0, document["effects"])
                self.assertEqual(list(verify.BINDING_GAPS), document["gap"])

    def test_timeout_bad_trx_old_trx_and_empty_suite_still_refuse(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            results = root / "results"
            results.mkdir()
            (results / "results.trx").write_text(TRX.format(name="OldTest"))

            def write_new(command, cwd, text, capture_output, timeout):
                write_trx(results, "NewTest")
                return subprocess.CompletedProcess(command, 0, stdout="", stderr="")

            with patch.object(verify.subprocess, "run", side_effect=write_new):
                code, names = verify.run_suite(root, root / "tests.csproj", "FullyQualifiedName~New", results, "suite.log")
            self.assertEqual(0, code)
            self.assertEqual(["NewTest"], [item["name"] for item in names])

            def invalid(command, cwd, text, capture_output, timeout):
                (results / "results.trx").write_text("<not-xml")
                return subprocess.CompletedProcess(command, 0, stdout="", stderr="")

            with patch.object(verify.subprocess, "run", side_effect=invalid):
                code, names = verify.run_suite(root, root / "tests.csproj", "FullyQualifiedName~New", results, "suite.log")
            self.assertEqual(1, code)
            self.assertEqual([], names)

            def empty(command, cwd, text, capture_output, timeout):
                return subprocess.CompletedProcess(command, 0, stdout="", stderr="")

            with patch.object(verify.subprocess, "run", side_effect=empty):
                code, names = verify.run_suite(root, root / "tests.csproj", "FullyQualifiedName~New", results, "suite.log")
            self.assertEqual(1, code)
            self.assertEqual([], names)

            def expire(command, cwd, text, capture_output, timeout):
                raise subprocess.TimeoutExpired(command, timeout)

            with patch.object(verify.subprocess, "run", side_effect=expire):
                code, names = verify.run_suite(root, root / "tests.csproj", "FullyQualifiedName~New", results, "suite.log")
            self.assertEqual(1, code)
            self.assertEqual([], names)

            artifacts = root / "artifacts"
            with patch.object(verify, "run_suite", return_value=(1, [])):
                stderr = StringIO()
                with patch.object(verify.sys, "stderr", stderr):
                    code = verify.verify(Path(__file__).resolve().parents[2], artifacts, None)
            self.assertEqual(1, code)
            self.assertIn("ERROR: " + verify.REFUSAL, stderr.getvalue())
            blocked = json.loads((artifacts / "evidence" / "blocked.json").read_text())
            self.assertEqual(0, blocked["effects"])
            self.assertEqual(list(verify.BINDING_GAPS), blocked["gap"])
            self.assertEqual(list(verify.MISSING_RUNTIME_INPUTS), blocked["missingRuntimeInputs"])


def complete_profile():
    profile = prepare.default_profile()
    profile["integrationDate"] = "2028-02-29"  # Explicit fixture date; no production default is inferred.
    for key in prepare.REFERENCE_KEYS:
        profile[key] = "ref-" + key
    profile["replicaCount"] = 1
    return profile


def write_profile(directory, profile, name="agents-deployment-profile.json"):
    path = Path(directory) / name
    path.write_bytes(prepare.canonical(profile))
    return path


def passed_names():
    names = []
    for name in verify.REQUIRED_CELLS:
        if name == "RoutingUsesAuthenticatedOriginAndRetainedReceiptDigest":
            shown = ("Hexalith.Platform.Custody.Tests.PlatformSecurityDenialRecorderTests."
                     + name + "(kind: denial, routing: tenant)")
        else:
            shown = "Hexalith.Platform.Custody.Tests.Cell." + name
        names.append({"name": shown, "outcome": "Passed"})
    names.append({"name": "Hexalith.Platform.Custody.Tests.Keyword.AclFence", "outcome": "Passed"})
    names.append({"name": "Hexalith.Platform.Custody.Tests.RecoverSomething", "outcome": "Passed"})
    return names


def passed_suite(root, project, test_filter, results, log_name, configuration="Debug"):
    return 0, passed_names()


class QualificationMatrixTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.platform = Path(temporary.name)
        subprocess.run(["git", "init", "-q", self.platform], check=True)
        subprocess.run(["git", "-C", self.platform, "-c", "user.name=Component fixture",
                        "-c", "user.email=component@example.invalid", "commit", "--allow-empty", "-qm", "test: component fixture"],
                       check=True)

    def test_component_attempt_identity_cannot_reuse_debug_evidence_for_release(self):
        profile = complete_profile()
        head = "a" * 40
        self.assertEqual(verify.attempt_key(head, profile), verify.attempt_key(head, profile, "Debug"))
        self.assertNotEqual(verify.attempt_key(head, profile, "Debug"), verify.attempt_key(head, profile, "Release"))
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            selected = write_profile(root, profile)
            artifacts = root / "attempt"
            with patch.object(verify, "git_head", return_value=head), \
                    patch.object(verify, "run_suite", side_effect=passed_suite) as started:
                self.assertEqual(verify.verify(root, artifacts, selected), 0)
                self.assertEqual(started.call_count, 2)
                recorded = {path.relative_to(artifacts): path.read_bytes() for path in artifacts.rglob("*") if path.is_file()}
                self.assertEqual(verify.verify(root, artifacts, selected, "Release"), 1)
                self.assertEqual(started.call_count, 2)
                self.assertEqual(recorded, {path.relative_to(artifacts): path.read_bytes() for path in artifacts.rglob("*") if path.is_file()})
                release_artifacts = root / "release-attempt"
                self.assertEqual(verify.verify(root, release_artifacts, selected, "Release"), 0)
                self.assertTrue(all(call.args[-1] == "Release" for call in started.call_args_list[-2:]))
            attempt = json.loads((release_artifacts / verify.ATTEMPT_NAME).read_text())
            self.assertEqual(attempt["configuration"], "Release")
            self.assertEqual(attempt["dependencyMode"], "public-packages")
            self.assertEqual(attempt["attemptKey"], verify.attempt_key(head, profile, "Release"))

    def test_profile_absent_refuses_when_every_required_cell_passed(self):
        with tempfile.TemporaryDirectory() as temp:
            artifacts = Path(temp) / "artifacts"

            def every_cell(root, project, test_filter, results, log_name, configuration="Debug"):
                return 0, [{"name": name, "outcome": "Passed"} for name in verify.REQUIRED_CELLS]

            with patch.object(verify, "run_suite", side_effect=every_cell) as started:
                stderr = StringIO()
                with patch.object(verify.sys, "stderr", stderr):
                    code = verify.verify(self.platform, artifacts, None)
            self.assertEqual(1, code)
            self.assertEqual(2, started.call_count)
            self.assertIn(verify.REFUSAL, stderr.getvalue())
            blocked = json.loads((artifacts / "evidence" / "blocked.json").read_text())
            self.assertEqual(0, blocked["effects"])
            self.assertEqual(0, blocked["liveEffects"])
            self.assertFalse(blocked["dependencyAvailable"])
            self.assertFalse(blocked["productionEngineRegistered"])
            self.assertFalse(blocked["componentQualification"])
            self.assertEqual(list(verify.BINDING_GAPS), blocked["gap"])
            self.assertEqual(list(verify.MISSING_RUNTIME_INPUTS), blocked["missingRuntimeInputs"])

    def test_bad_profiles_refuse_before_suites_and_keep_prior_digest(self):
        secret = prepare.FORBIDDEN[2]
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            pending = complete_profile()
            pending["partiesBinding"] = "pending"
            incomplete = prepare.default_profile()
            secret_profile = complete_profile()
            secret_profile["replay"] = secret
            cases = {
                "pending": write_profile(root, pending, "pending.json"),
                "incomplete": write_profile(root, incomplete, "incomplete.json"),
                "non-object": root / "array.json",
                "secret": write_profile(root, secret_profile, "secret.json"),
            }
            cases["non-object"].write_text("[1]")
            for label, path in cases.items():
                with self.subTest(label=label):
                    artifacts = root / label
                    evidence = artifacts / "evidence"
                    evidence.mkdir(parents=True)
                    (evidence / "marker.json").write_text('{"keep":true}\n')
                    with patch.object(verify, "run_suite") as started:
                        stderr = StringIO()
                        with patch.object(verify.sys, "stderr", stderr):
                            code = verify.verify(self.platform, artifacts, path)
                    self.assertEqual(1, code)
                    started.assert_not_called()
                    text = stderr.getvalue()
                    self.assertIn(verify.REFUSAL, text)
                    self.assertNotIn(secret, text)
                    tree = "\n".join(path.read_text(errors="ignore") for path in evidence.rglob("*") if path.is_file())
                    self.assertNotIn(secret, tree)
                    blocked = json.loads((evidence / "blocked.json").read_text())
                    self.assertEqual(0, blocked["effects"])
                    self.assertEqual(0, blocked["liveEffects"])
                    self.assertFalse(blocked["dependencyAvailable"])
                    self.assertEqual(list(verify.MISSING_RUNTIME_INPUTS), blocked["missingRuntimeInputs"])
                    self.assertFalse((artifacts / verify.ATTEMPT_NAME).exists())
            evidence = root / "pending" / "evidence"
            prior = next(path for path in (evidence / "prior").iterdir() if path.is_dir())
            self.assertEqual(64, len(prior.name))
            self.assertEqual('{"keep":true}\n', (prior / "marker.json").read_text())
            (prior / "marker.json").write_text("SENTINEL")
            for child in list(evidence.iterdir()):
                if child.name != "prior" and child.is_file():
                    child.unlink()
            (evidence / "marker.json").write_text('{"keep":true}\n')
            with patch.object(verify, "run_suite") as started:
                stderr = StringIO()
                with patch.object(verify.sys, "stderr", stderr):
                    code = verify.verify(self.platform, root / "pending", cases["pending"])
            self.assertEqual(1, code)
            started.assert_not_called()
            self.assertIn(verify.REFUSAL, stderr.getvalue())
            self.assertEqual("SENTINEL", (prior / "marker.json").read_text())

    def test_complete_profile_records_required_cells_without_live_flags(self):
        with tempfile.TemporaryDirectory() as temp:
            profile = write_profile(temp, complete_profile())
            artifacts = Path(temp) / "artifacts"
            with patch.object(verify, "run_suite", side_effect=passed_suite) as started:
                stderr = StringIO()
                with patch.object(verify.sys, "stderr", stderr):
                    code = verify.verify(self.platform, artifacts, profile)
                self.assertEqual(0, code)
                self.assertNotIn(verify.REFUSAL, stderr.getvalue())
                self.assertEqual(2, started.call_count)
                blocked_bytes = (artifacts / "evidence" / "blocked.json").read_bytes()
                attempt_bytes = (artifacts / verify.ATTEMPT_NAME).read_bytes()
                stderr = StringIO()
                with patch.object(verify.sys, "stderr", stderr):
                    again = verify.verify(self.platform, artifacts, profile)
            self.assertEqual(0, again)
            self.assertEqual("", stderr.getvalue())
            self.assertEqual(2, started.call_count)
            self.assertEqual(blocked_bytes, (artifacts / "evidence" / "blocked.json").read_bytes())
            self.assertEqual(attempt_bytes, (artifacts / verify.ATTEMPT_NAME).read_bytes())
            blocked = json.loads(blocked_bytes)
            self.assertTrue(blocked["componentQualification"])
            self.assertFalse(blocked["dependencyAvailable"])
            self.assertFalse(blocked["productionEngineRegistered"])
            self.assertEqual(0, blocked["effects"])
            self.assertEqual(0, blocked["liveEffects"])
            self.assertEqual([], blocked["coverageGaps"])
            self.assertEqual(list(verify.MISSING_RUNTIME_INPUTS), blocked["missingRuntimeInputs"])
            self.assertEqual(
                [{"name": name, "outcome": "Passed"} for name in verify.REQUIRED_CELLS],
                blocked["requiredCells"])
            acl = json.loads((artifacts / "evidence" / "acl.json").read_text())
            self.assertEqual(list(verify.MISSING_RUNTIME_INPUTS), acl["missingRuntimeInputs"])
            self.assertTrue(acl["componentQualification"])
            self.assertEqual(0, acl["effects"])
            lost = json.loads((artifacts / "evidence" / "lost-ack.json").read_text())
            self.assertEqual("lost-ack", lost["category"])
            self.assertTrue(lost["componentQualification"])
            self.assertEqual(set(verify.LOST_ACK_CELLS), {item["name"].split("(", 1)[0].rsplit(".", 1)[-1] for item in lost["matchedTests"]})
            self.assertFalse(any("RecoverSomething" in item["name"] or "AclFence" in item["name"] for item in lost["matchedTests"]))
            attempt = json.loads(attempt_bytes)
            self.assertEqual(0, attempt["exitCode"])
            self.assertFalse(attempt["dependencyAvailable"])
            self.assertEqual(0, attempt["effects"])
            self.assertEqual(0, attempt["liveEffects"])
            self.assertEqual(list(verify.MISSING_RUNTIME_INPUTS), attempt["missingRuntimeInputs"])
            self.assertNotIn("signature", attempt)
            self.assertEqual(verify.evidence_digest(artifacts / "evidence"), attempt["evidenceDigest"])
            acl_methods = {item["name"].split("(", 1)[0].rsplit(".", 1)[-1] for item in acl["matchedTests"]}
            self.assertTrue(acl_methods <= set(verify.REQUIRED_CELLS))
            self.assertNotIn("AclFence", acl_methods)
            self.assertFalse(any("RecoverSomething" in item["name"] or "AclFence" in item["name"] for item in acl["matchedTests"]))
            blocked_path = artifacts / "evidence" / "blocked.json"
            blocked_path.write_bytes(blocked_path.read_bytes() + b" ")
            stderr = StringIO()
            with patch.object(verify.sys, "stderr", stderr):
                tampered = verify.verify(self.platform, artifacts, profile)
            self.assertEqual(1, tampered)
            self.assertIn("evidence digest", stderr.getvalue())
            self.assertEqual(2, started.call_count)
            self.assertEqual(attempt_bytes, (artifacts / verify.ATTEMPT_NAME).read_bytes())

    def test_missing_cell_or_suite_names_gap_and_keeps_prior_evidence(self):
        platform = self.platform
        with tempfile.TemporaryDirectory() as temp:
            profile = write_profile(temp, complete_profile())
            omitted = "LostRecorderAcknowledgementRecoversExactSourceBeforeSpoolAck"

            def unnamed(root, project, test_filter, results, log_name, configuration="Debug"):
                names = [item for item in passed_names() if omitted not in item["name"]]
                names.append({"name": "Hexalith.Platform.Custody.Tests.Cell." + omitted + "Extra", "outcome": "Passed"})
                return 0, names

            artifacts = Path(temp) / "unnamed"
            evidence = artifacts / "evidence"
            evidence.mkdir(parents=True)
            (evidence / "marker.json").write_text('{"keep":true}\n')
            with patch.object(verify, "run_suite", side_effect=unnamed) as started:
                stderr = StringIO()
                with patch.object(verify.sys, "stderr", stderr):
                    code = verify.verify(platform, artifacts, profile)
                self.assertEqual(1, code)
                self.assertNotIn(verify.REFUSAL, stderr.getvalue())
                self.assertEqual(2, started.call_count)
                blocked = json.loads((evidence / "blocked.json").read_text())
                self.assertEqual([omitted], blocked["coverageGaps"])
                self.assertEqual(0, blocked["effects"])
                self.assertEqual(0, blocked["liveEffects"])
                self.assertFalse(blocked["dependencyAvailable"])
                self.assertFalse(blocked["componentQualification"])
                prior = next(path for path in (evidence / "prior").iterdir() if path.is_dir())
                self.assertEqual('{"keep":true}\n', (prior / "marker.json").read_text())
                (prior / "marker.json").write_text("SENTINEL")
                blocked_bytes = (evidence / "blocked.json").read_bytes()
                attempt_bytes = (artifacts / verify.ATTEMPT_NAME).read_bytes()
                stderr = StringIO()
                with patch.object(verify.sys, "stderr", stderr):
                    again = verify.verify(platform, artifacts, profile)
            self.assertEqual(1, again)
            self.assertEqual("", stderr.getvalue())
            self.assertEqual(2, started.call_count)
            self.assertEqual("SENTINEL", (prior / "marker.json").read_text())
            self.assertEqual(blocked_bytes, (evidence / "blocked.json").read_bytes())
            self.assertEqual(attempt_bytes, (artifacts / verify.ATTEMPT_NAME).read_bytes())

            def failed(root, project, test_filter, results, log_name, configuration="Debug"):
                names = passed_names()
                names[0] = {"name": names[0]["name"], "outcome": "Failed"}
                return 0, names

            failed_artifacts = Path(temp) / "failed"
            with patch.object(verify, "run_suite", side_effect=failed):
                stderr = StringIO()
                with patch.object(verify.sys, "stderr", stderr):
                    code = verify.verify(platform, failed_artifacts, profile)
            self.assertNotIn(verify.REFUSAL, stderr.getvalue())
            failed_blocked = json.loads((failed_artifacts / "evidence" / "blocked.json").read_text())
            self.assertEqual(1, code)
            self.assertEqual(["RoutingUsesAuthenticatedOriginAndRetainedReceiptDigest"], failed_blocked["coverageGaps"])
            self.assertEqual(0, failed_blocked["effects"])
            attempt_path = failed_artifacts / verify.ATTEMPT_NAME
            flipped = json.loads(attempt_path.read_text())
            self.assertEqual(1, flipped["exitCode"])
            flipped["exitCode"] = 0
            attempt_path.write_bytes(verify.canonical(flipped))
            with patch.object(verify, "run_suite", side_effect=failed) as replayed:
                stderr = StringIO()
                with patch.object(verify.sys, "stderr", stderr):
                    code = verify.verify(platform, failed_artifacts, profile)
            self.assertEqual(1, code)
            self.assertIn("required cell", stderr.getvalue())
            self.assertEqual(0, replayed.call_count)

            def mixed(root, project, test_filter, results, log_name, configuration="Debug"):
                method = "ExactLookupDoesNotMutateAndRequiresCurrentPrivateAuthority"
                names = passed_names()
                names.append({"name": "Hexalith.Platform.Custody.Tests.Cell." + method, "outcome": "Failed"})
                return 0, names

            mixed_artifacts = Path(temp) / "mixed"
            with patch.object(verify, "run_suite", side_effect=mixed):
                code = verify.verify(platform, mixed_artifacts, profile)
            mixed_blocked = json.loads((mixed_artifacts / "evidence" / "blocked.json").read_text())
            self.assertEqual(1, code)
            self.assertFalse(mixed_blocked["componentQualification"])
            self.assertIn("ExactLookupDoesNotMutateAndRequiresCurrentPrivateAuthority", mixed_blocked["coverageGaps"])

            def empty(root, project, test_filter, results, log_name, configuration="Debug"):
                return 1, []

            suite_artifacts = Path(temp) / "suite"
            suite_evidence = suite_artifacts / "evidence"
            suite_evidence.mkdir(parents=True)
            (suite_evidence / "marker.json").write_text('{"keep":true}\n')
            with patch.object(verify, "run_suite", side_effect=empty):
                stderr = StringIO()
                with patch.object(verify.sys, "stderr", stderr):
                    code = verify.verify(platform, suite_artifacts, profile)
            self.assertNotIn(verify.REFUSAL, stderr.getvalue())
            self.assertIn("evidence suites failed", stderr.getvalue())
            suite_blocked = json.loads((suite_evidence / "blocked.json").read_text())
            self.assertEqual(1, code)
            self.assertEqual(list(verify.REQUIRED_CELLS), suite_blocked["coverageGaps"])
            self.assertEqual(0, suite_blocked["effects"])
            self.assertEqual(0, suite_blocked["liveEffects"])
            self.assertEqual('{"keep":true}\n', next((suite_evidence / "prior").iterdir()).joinpath("marker.json").read_text())

    def test_other_complete_target_keeps_the_first_attempt(self):
        with tempfile.TemporaryDirectory() as temp:
            first = write_profile(temp, complete_profile(), "first.json")
            other_body = complete_profile()
            other_body["failureModel"] = "ref-failureModel-other"
            other = write_profile(temp, other_body, "other.json")
            artifacts = Path(temp) / "artifacts"
            platform = self.platform
            with patch.object(verify, "run_suite", side_effect=passed_suite) as started:
                self.assertEqual(0, verify.verify(platform, artifacts, first))
                snapshot = {
                    path.relative_to(artifacts).as_posix(): path.read_bytes()
                    for path in artifacts.rglob("*") if path.is_file()
                }
                stderr = StringIO()
                with patch.object(verify.sys, "stderr", stderr):
                    code = verify.verify(platform, artifacts, other)
            self.assertEqual(1, code)
            self.assertIn("keeps the first complete attempt", stderr.getvalue())
            self.assertNotIn(verify.REFUSAL, stderr.getvalue())
            self.assertEqual(2, started.call_count)
            current = {
                path.relative_to(artifacts).as_posix(): path.read_bytes()
                for path in artifacts.rglob("*") if path.is_file()
            }
            self.assertEqual(snapshot, current)
            attempt = json.loads((artifacts / verify.ATTEMPT_NAME).read_text())
            self.assertEqual(0, attempt["exitCode"])
            self.assertFalse(attempt["dependencyAvailable"])
            pending_body = complete_profile()
            pending_body["partiesBinding"] = "pending"
            pending = write_profile(temp, pending_body, "pending.json")
            with patch.object(verify, "run_suite", side_effect=passed_suite) as replay:
                with patch.object(verify, "git_head", return_value="b" * 40):
                    stderr = StringIO()
                    with patch.object(verify.sys, "stderr", stderr):
                        changed_head = verify.verify(platform, artifacts, first)
                self.assertEqual(1, changed_head)
                self.assertIn("keeps the first complete attempt", stderr.getvalue())
                self.assertEqual(0, replay.call_count)
                for label, profile_arg in (("missing", None), ("pending", pending)):
                    with self.subTest(profile=label):
                        code = verify.verify(platform, artifacts, profile_arg)
                        self.assertEqual(1, code)
                        self.assertEqual(0, replay.call_count)
            unchanged = {
                path.relative_to(artifacts).as_posix(): path.read_bytes()
                for path in artifacts.rglob("*") if path.is_file()
            }
            self.assertEqual(snapshot, unchanged)

    def test_parser_rejects_profile_flag_before_pins_and_forwards_a_real_path(self):
        platform = Path(__file__).resolve().parents[2]
        script = platform / "eng" / "verify-agents-host.sh"
        with tempfile.TemporaryDirectory() as temp:
            fake_dir = Path(temp)
            log = fake_dir / "python.log"
            dotnet_log = fake_dir / "dotnet.log"
            (fake_dir / "python3").write_text(
                "#!/bin/sh\n"
                "printf '%s\\n' \"$*\" >> \"$FAKE_PYTHON_LOG\"\n"
                "case \" $* \" in\n"
                "  *verify_full.py*) exit \"${FAKE_VERIFY_CODE:-1}\" ;;\n"
                "esac\n"
                "exit 0\n")
            (fake_dir / "dotnet").write_text("#!/bin/sh\nprintf '%s\\n' \"$*\" >> \"$FAKE_DOTNET_LOG\"\nexit 99\n")
            os.chmod(fake_dir / "python3", stat.S_IRWXU)
            os.chmod(fake_dir / "dotnet", stat.S_IRWXU)
            env = os.environ | {
                "PATH": str(fake_dir) + os.pathsep + os.environ.get("PATH", ""),
                "FAKE_PYTHON_LOG": str(log),
                "FAKE_DOTNET_LOG": str(dotnet_log),
                "FAKE_VERIFY_CODE": "1",
            }
            for args in (["--profile"], ["--profile", ""], ["--not-a-flag"]):
                with self.subTest(args=args):
                    log.unlink(missing_ok=True)
                    dotnet_log.unlink(missing_ok=True)
                    completed = subprocess.run(["bash", str(script), *args], cwd=platform, env=env, capture_output=True, text=True)
                    self.assertEqual(1, completed.returncode)
                    self.assertFalse(log.exists())
                    self.assertFalse(dotnet_log.exists())
                    self.assertNotIn("Building local Platform scaffold", completed.stdout)
            log.write_text("")
            completed = subprocess.run(
                ["bash", str(script), "--mode", "Full", "--artifacts-path", str(fake_dir / "artifacts")],
                cwd=platform, env=env, capture_output=True, text=True)
            self.assertEqual(1, completed.returncode)
            forwarded = log.read_text()
            self.assertIn("verify_full.py", forwarded)
            self.assertNotIn("--profile", forwarded)
            self.assertFalse(dotnet_log.exists())
            log.write_text("")
            profile = fake_dir / "agents-deployment-profile.json"
            profile.write_text("{}\n")
            completed = subprocess.run(
                ["bash", str(script), "--mode", "Full", "--profile", str(profile), "--artifacts-path", str(fake_dir / "out")],
                cwd=platform, env=env, capture_output=True, text=True)
            self.assertEqual(1, completed.returncode)
            forwarded = log.read_text()
            self.assertIn("verify_full.py", forwarded)
            self.assertIn("--profile", forwarded)
            self.assertIn(str(profile), forwarded)
            self.assertFalse(dotnet_log.exists())
            self.assertNotIn("Component qualification passed", completed.stdout)
            env["FAKE_VERIFY_CODE"] = "0"
            log.write_text("")
            completed = subprocess.run(
                ["bash", str(script), "--mode", "Full", "--profile", str(profile), "--artifacts-path", str(fake_dir / "pass")],
                cwd=platform, env=env, capture_output=True, text=True)
            self.assertEqual(0, completed.returncode)
            self.assertIn("Component qualification passed. Live qualification and the OQ-5.4-1 cohort remain gated.", completed.stdout)
            self.assertNotIn("unavailable", completed.stdout)
            self.assertNotIn("Available", completed.stdout)
            self.assertFalse(dotnet_log.exists())
            other = fake_dir / "elsewhere"
            other.mkdir()
            (other / "rel-profile.json").write_text("{}\n")
            log.write_text("")
            env["FAKE_VERIFY_CODE"] = "1"
            completed = subprocess.run(
                ["bash", str(script), "--mode", "Full", "--profile", "rel-profile.json", "--artifacts-path", "rel-artifacts"],
                cwd=other, env=env, capture_output=True, text=True)
            self.assertEqual(1, completed.returncode)
            caller = subprocess.run(["bash", "-c", "pwd"], cwd=other, capture_output=True, text=True, check=True).stdout.strip()
            forwarded = log.read_text()
            self.assertIn(caller + "/rel-profile.json", forwarded)
            self.assertIn(caller + "/rel-artifacts", forwarded)
            self.assertNotIn(str(platform / "rel-profile.json"), forwarded)


if __name__ == "__main__":
    unittest.main()
