"""Failure injection for owned lifetime and strict executed-test evidence."""
import contextlib
import io
import json
import os
from pathlib import Path
import stat
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

from component_fixture import ChildFailure, ComponentFixture, sanitize, verify_execution


class ComponentFixtureTests(unittest.TestCase):
    def evidence(self, directory, fault=None):
        root = ET.Element("assemblies")
        assembly = ET.SubElement(root, "assembly")
        collection = ET.SubElement(assembly, "collection")
        faults = ["none", "credential", "caller", "tenant", "audience", "signature", "scope", "expired", "malformed", "missing-proof", "cancelled"]
        for value in faults:
            ET.SubElement(collection, "test", type="Hexalith.Folders.Server.Tests.EnrolledWorkloadSourceBoundaryTests", method="ActualIssuerAndIndependentSdkGuardEverySourceRequest",
                          name=f'Test(fault: "{value}")', result="Skip" if fault == "skipped" else "Pass")
        if fault != "filtered":
            ET.SubElement(collection, "test", name="other", result="Pass")
        path = Path(directory) / "result.xml"
        if fault == "missing":
            path.unlink(missing_ok=True)
            return path
        if fault == "duplicate": collection[0].set("name", collection[1].get("name"))
        ET.ElementTree(root).write(path)
        return path

    def test_strict_executed_inventory(self):
        with tempfile.TemporaryDirectory() as directory:
            path = self.evidence(directory)
            output = "Hexalith.Folders.Server.Tests  Total: 12, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 1.0s\n"
            self.assertEqual(verify_execution(output, path, "Hexalith.Folders.Server.Tests", True)["securityCases"], 11)
            for changed in ("", output + output, output.replace("Total: 12", "Total: 0"), output.replace("Not Run: 0", "Not Run: 1"), output.replace("Skipped: 0", "Skipped: 1")):
                with self.subTest(output=changed), self.assertRaises(RuntimeError):
                    verify_execution(changed, path, "Hexalith.Folders.Server.Tests", True)
            for fault in ("missing", "filtered", "duplicate", "skipped"):
                with self.subTest(fault=fault), self.assertRaises(RuntimeError):
                    verify_execution(output, self.evidence(directory, fault), "Hexalith.Folders.Server.Tests", True)

    def test_cleanup_on_startup_or_preparation_failure(self):
        for failure in ("startup", "fixture preparation", "child"):
            calls = []
            def runner(*args):
                calls.append(args)
                return "owned" if len(calls) == 1 else ""
            output = io.StringIO()
            with contextlib.redirect_stdout(output), self.assertRaisesRegex(RuntimeError, failure):
                with ComponentFixture("owned", runner) as fixture:
                    path = fixture.directory
                    self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o700)
                    secret = path / "secret"
                    secret.write_text("private")
                    self.assertEqual(stat.S_IMODE(secret.stat().st_mode), 0o600)
                    raise RuntimeError(failure)
            self.assertFalse(path.exists())
            self.assertEqual(calls[1], ("docker", "rm", "--force", "--volumes", "owned"))
            self.assertEqual(json.loads(output.getvalue())["observedRemainingContainers"], 0)

    def test_cleanup_failures_are_exposed(self):
        for fault in ("container", "private"):
            output = io.StringIO()
            fixture = ComponentFixture("owned", lambda *args: "still-owned" if fault == "container" else "")
            fixture.__enter__()
            path = fixture.directory
            try:
                with contextlib.redirect_stdout(output), self.assertRaisesRegex(RuntimeError, "cleanup"):
                    if fault == "private":
                        with patch("component_fixture.shutil.rmtree", side_effect=OSError()):
                            fixture.__exit__(None, None, None)
                    else:
                        fixture.__exit__(None, None, None)
                self.assertIn("cleanup-incomplete", output.getvalue())
            finally:
                if path.exists(): __import__('shutil').rmtree(path)

    def test_child_failure_and_timeout_are_awaited_and_sanitized(self):
        for timed_out in (False, True):
            output = io.StringIO()
            with tempfile.TemporaryDirectory() as directory:
                assembly = Path(directory) / "Server.dll"
                assembly.touch()
                with contextlib.redirect_stdout(output):
                    with ComponentFixture("owned", lambda *args: "") as fixture:
                        fixture.secrets.append("private-value")
                        class FakeChild:
                            pid = 99999999
                            returncode = 7
                            waits = 0
                            def wait(self, timeout=None):
                                self.waits += 1
                                if timed_out and timeout is not None: raise subprocess.TimeoutExpired("protected", timeout)
                                return self.returncode
                        child = FakeChild()
                        def launch(*args, **kwargs):
                            kwargs['stdout'].write('client_secret=private-value\n')
                            return child
                        with patch("component_fixture.subprocess.Popen", side_effect=launch), patch("component_fixture.os.killpg") as kill:
                            with self.assertRaises(RuntimeError if timed_out else ChildFailure) as caught:
                                fixture.run_tests(assembly, complete=True, timeout=1)
                            if not timed_out: self.assertEqual(caught.exception.code, 7)
                            self.assertEqual(child.waits, 2)
                            kill.assert_called_once()
            self.assertNotIn("private-value", output.getvalue())

    def test_diagnostic_bound_and_redaction(self):
        value = sanitize('x' * 20000 + ' secret=hidden abcdefghijk.abcdefghijk.abcdefghijk', ['hidden'])
        self.assertLessEqual(len(value), 16000)
        self.assertNotIn('hidden', value)
        self.assertNotIn('abcdefghijk', value)


if __name__ == '__main__':
    unittest.main()
