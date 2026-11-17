"""Failure injection for owned lifetime and strict executed-test evidence."""
import contextlib
import io
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import time
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

from component_fixture import ChildFailure, ComponentFixture, command, sanitize, verify_execution
import verify_component


class ComponentFixtureTests(unittest.TestCase):
    def test_missing_provider_refuses_without_allocating_fixture(self):
        with tempfile.TemporaryDirectory() as directory:
            with patch.object(verify_component, "__file__", str(Path(directory) / "verify_component.py")), patch.object(verify_component, "ComponentFixture") as scope:
                with self.assertRaisesRegex(RuntimeError, "Build the provider"):
                    verify_component.run(complete_assembly="unneeded.dll")
                scope.assert_not_called()

    def test_missing_command_is_sanitized(self):
        with patch("component_fixture.subprocess.check_output", side_effect=FileNotFoundError("protected-value")):
            with self.assertRaisesRegex(RuntimeError, "protected arguments and output withheld") as caught:
                command("nonexistent", "protected-value")
            self.assertNotIn("protected-value", str(caught.exception))

    def test_cleanup_command_oserror_still_removes_private_files(self):
        def unavailable(*args):
            raise OSError("protected-value")
        with contextlib.redirect_stdout(io.StringIO()), self.assertRaisesRegex(RuntimeError, "owned-container-cleanup-unverified"):
            with ComponentFixture("owned", unavailable) as fixture:
                path = fixture.directory
                (path / "secret").write_text("private")
        self.assertFalse(path.exists())

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
        assembly.attrib.update(total=str(len(collection)), passed=str(len(collection)), errors="1" if fault == "errors" else "0", failed="0", skipped="0", **{"not-run": "0"})
        ET.ElementTree(root).write(path)
        return path

    def test_strict_executed_inventory(self):
        with tempfile.TemporaryDirectory() as directory:
            path = self.evidence(directory)
            output = "Hexalith.Folders.Server.Tests  Total: 12, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 1.0s\n"
            self.assertEqual(verify_execution(output, path, "Hexalith.Folders.Server.Tests", True)["securityCases"], 11)
            escaped = ET.parse(path)
            for test in escaped.findall(".//test"):
                test.set("name", test.get("name", "").replace('"', chr(92) + '"'))
            escaped.write(path)
            self.assertEqual(verify_execution(output, path, "Hexalith.Folders.Server.Tests", True)["securityCases"], 11)
            for changed in ("", output + output, output.replace("Total: 12", "Total: 0"), output.replace("Not Run: 0", "Not Run: 1"), output.replace("Skipped: 0", "Skipped: 1")):
                with self.subTest(output=changed), self.assertRaises(RuntimeError):
                    verify_execution(changed, path, "Hexalith.Folders.Server.Tests", True)
            for fault in ("missing", "duplicate", "skipped", "errors"):
                with self.subTest(fault=fault), self.assertRaises(RuntimeError):
                    verify_execution(output, self.evidence(directory, fault), "Hexalith.Folders.Server.Tests", True)

    def test_complete_guard_rejects_matching_focused_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            path = self.evidence(directory, "filtered")
            summary = "Hexalith.Folders.Server.Tests Total: 11, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0\n"
            self.assertEqual(verify_execution(summary, path, "Hexalith.Folders.Server.Tests", False)["total"], 11)
            with self.assertRaisesRegex(RuntimeError, "XML does not prove"):
                verify_execution(summary, path, "Hexalith.Folders.Server.Tests", True)

    def test_xml_requires_counters_and_refuses_assembly_errors(self):
        with tempfile.TemporaryDirectory() as directory:
            summary = "Hexalith.Folders.Server.Tests Total: 12, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0\n"
            for fault in ("errors", "failed", "skipped", "not-run", "error-element", "errors-element"):
                path = self.evidence(directory)
                tree = ET.parse(path)
                assembly = tree.getroot().find("assembly")
                if fault.endswith("element"):
                    if fault == "error-element": ET.SubElement(assembly, "error")
                    else: ET.SubElement(ET.SubElement(assembly, "errors"), "message").text = "failure"
                else:
                    del assembly.attrib[fault]
                tree.write(path)
                with self.subTest(fault=fault), self.assertRaises(RuntimeError):
                    verify_execution(summary, path, "Hexalith.Folders.Server.Tests", True)

    def test_real_blocking_child_is_terminated_before_cleanup(self):
        children = []
        original = subprocess.Popen
        def launch(args, **kwargs):
            child = original([sys.executable, "-c", "import time; time.sleep(60)"], **kwargs)
            children.append(child)
            return child
        started = time.monotonic()
        with tempfile.TemporaryDirectory() as directory:
            assembly = Path(directory) / "Server.dll"
            assembly.touch()
            with contextlib.redirect_stdout(io.StringIO()), self.assertRaisesRegex(RuntimeError, "deadline elapsed"):
                with ComponentFixture("owned", lambda *args: "") as fixture:
                    private = fixture.directory
                    with patch("component_fixture.subprocess.Popen", side_effect=launch):
                        fixture.run_tests(assembly, timeout=0.2)
        self.assertLess(time.monotonic() - started, 5)
        self.assertIsNotNone(children[0].returncode)
        self.assertFalse(Path("/proc", str(children[0].pid)).exists())
        self.assertFalse(private.exists())

    def test_structured_diagnostics_and_all_xml_text_are_sanitized(self):
        credential = 'private"value\\credential'
        output = io.StringIO()
        with tempfile.TemporaryDirectory() as directory, contextlib.redirect_stdout(output):
            path = self.evidence(directory)
            tree = ET.parse(path)
            test = tree.getroot().find(".//test")
            test.set("name", credential)
            test.set("result", "Fail")
            ET.SubElement(test, "output").text = credential + "x" * 20000 + credential
            test.tail = credential
            tree.write(path)
            retained = Path(directory) / "retained.xml"
            with ComponentFixture("owned", lambda *args: "") as fixture:
                fixture.secrets.append(credential)
                bindings = "\n".join("COMPONENT_RUNTIME_BINDING " + json.dumps(dict(name=credential, version="x" * 20000)) for _ in range(3))
                self.assertEqual(fixture.record_evidence(bindings, path, retained), [])
            for line in output.getvalue().splitlines():
                self.assertLessEqual(len(line), 16000)
                if line.startswith("{"): json.loads(line)
                self.assertNotIn(credential, line)
                self.assertNotIn(json.dumps(credential)[1:-1], line)
            result = ET.parse(retained)
            self.assertEqual(result.getroot().find(".//test").get("name"), "[redacted]")
            self.assertEqual(len(result.getroot().find(".//output").text), 20000 + 2 * len("[redacted]"))
            self.assertNotIn(credential, retained.read_text())

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

    def test_cleanup_uses_owned_label_and_immutable_id(self):
        calls = []
        def runner(*args):
            calls.append(args)
            return "immutable-owned-id" if len(calls) == 1 else ""
        with contextlib.redirect_stdout(io.StringIO()), ComponentFixture("shared-name", runner) as fixture:
            token = fixture.owner_token
        self.assertIn("label=hexalith.component.fixture=" + token, calls[0])
        self.assertEqual(calls[1], ("docker", "rm", "--force", "--volumes", "immutable-owned-id"))
        self.assertNotIn("shared-name", calls[1])

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
                            kwargs['stdout'].buffer.write(b'\xffclient_secret=private-value\nCOMPONENT_RUNTIME_BINDING {broken}\n')
                            kwargs['stdout'].flush()
                            (fixture.directory / "sdk-results.xml").write_text("<unfinished")
                            return child
                        with patch("component_fixture.subprocess.Popen", side_effect=launch), patch("component_fixture.owned_processes.stop_child") as stop:
                            if timed_out:
                                with self.assertRaisesRegex(RuntimeError, r"^Component test child deadline elapsed\.$") as caught:
                                    fixture.run_tests(assembly, complete=True, timeout=1)
                                self.assertNotIsInstance(caught.exception, ChildFailure)
                            else:
                                with self.assertRaises(ChildFailure) as caught:
                                    fixture.run_tests(assembly, complete=True, timeout=1)
                                self.assertEqual(caught.exception.code, 7)
                            self.assertEqual(child.waits, 2)
                            stop.assert_called_once()
            self.assertNotIn("private-value", output.getvalue())

    def test_diagnostic_bound_and_redaction(self):
        value = sanitize('x' * 20000 + ' secret=hidden abcdefghijk.abcdefghijk.abcdefghijk', ['hidden'])
        self.assertLessEqual(len(value), 16000)
        self.assertNotIn('hidden', value)
        self.assertNotIn('abcdefghijk', value)


if __name__ == '__main__':
    unittest.main()
