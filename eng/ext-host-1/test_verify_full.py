import json
import subprocess
import tempfile
import unittest
from io import StringIO
from pathlib import Path
from unittest.mock import patch

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


if __name__ == "__main__":
    unittest.main()
