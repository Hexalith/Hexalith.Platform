"""Local HTTP/control fixtures; these confer no published host or native acceptance."""
import datetime
import hashlib
import http.server
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import threading
import time
import unittest
import xml.etree.ElementTree as ET

SOURCE = Path(__file__).with_name("qualify-host.py")
ORIGINAL = Path(__file__).resolve().parents[2] / "tests/fixtures/Hexalith.Platform.TestExtension"
PROFILE = "eventstore-extension-package"
ENTRY = "tests/Hexalith.Platform.TestExtension.Qualification.Tests/Hexalith.Platform.TestExtension.Qualification.Tests.csproj"
OBSERVER = "eng/test-extension-release/qualify-host.py"


class HostEntryTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.script = self.root / OBSERVER
        self.script.parent.mkdir(parents=True)
        shutil.copyfile(SOURCE, self.script)
        self.fixture = self.root / "tests/fixtures/Hexalith.Platform.TestExtension"
        self.fixture.mkdir(parents=True)
        declaration = json.loads((ORIGINAL / "hexalith.module-manifest.v2.json").read_bytes())
        declaration["modules"][0]["identity"]["dependencies"] = ["eventstore", "tenants", "memories"]
        declaration["modules"][0]["tests"] = [{"name": PROFILE, "executor": "mtp", "entry": ENTRY,
            "reportFormat": "trx", "executionTimeoutSeconds": 120, "stopTimeoutSeconds": 90, "mode": "package"}]
        self.manifest = self.fixture / "hexalith.module-manifest.v2.json"
        self.manifest.write_text(json.dumps(declaration, indent=2) + "\n")
        producer = json.loads((ORIGINAL / "fixture-producer.json").read_bytes())
        producer["ownerRepositoryIdentity"] = "Hexalith.Platform"
        for relative in producer["test"]["files"]:
            source = ORIGINAL.parents[2] / relative
            target = self.root / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            if relative != OBSERVER:
                shutil.copyfile(source, target)
        producer["test"] = {"name": PROFILE, "sourceEntry": ENTRY,
            "files": {relative: {"path": metadata["path"], "sha256": hashlib.sha256((self.root / relative).read_bytes()).hexdigest()}
                      for relative, metadata in producer["test"]["files"].items()}}
        self.producer = self.fixture / "fixture-producer.json"
        self.producer.write_text(json.dumps(producer, indent=2) + "\n")
        self.report = self.root / "native-tests/result.trx"
        self.report.parent.mkdir()
        self.body = {"status": "Unhealthy", "results": {"eventstore-extensions": {"status": "Healthy",
            "data": {"platform-test": 1}, "description": "platform-test@1 (Hexalith.Platform.TestExtension)"}}}
        self.status = 503
        self.redirect = False
        self.chunk_delays = False
        self.requests = []
        fixture = self

        class Handler(http.server.BaseHTTPRequestHandler):
            def do_GET(self):
                fixture.requests.append((self.path, dict(self.headers)))
                if fixture.redirect and self.path == "/ready":
                    self.send_response(302)
                    self.send_header("Location", f"http://127.0.0.1:{fixture.server.server_port}/healthy-target")
                    self.send_header("Content-Length", "0")
                    self.end_headers()
                    return
                payload = fixture.body if isinstance(fixture.body, bytes) else json.dumps(fixture.body).encode()
                self.send_response(fixture.status)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(payload)))
                self.end_headers()
                if fixture.chunk_delays:
                    time.sleep(0.15)
                    self.wfile.write(payload[:len(payload) // 2])
                    self.wfile.flush()
                    time.sleep(0.15)
                    self.wfile.write(payload[len(payload) // 2:])
                else:
                    self.wfile.write(payload)

            def log_message(self, *args):
                pass

        self.server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.addCleanup(self.close_server)
        self.environment = os.environ.copy()
        self.environment.update({"HEXALITH_G4_TEST_OWNER": "platformtestextension",
            "HEXALITH_G4_TEST_NAME": PROFILE, "HEXALITH_G4_TEST_MODE": "package",
            "HEXALITH_G4_TEST_MANIFEST_HASH": hashlib.sha256(self.manifest.read_bytes()).hexdigest(),
            "HEXALITH_G4_TEST_ENTRY_HASH": hashlib.sha256((self.root / ENTRY).read_bytes()).hexdigest(),
            "HEXALITH_G4_RUN_ID": "a" * 32,
            "HEXALITH_G4_EXECUTION_DEADLINE_UTC": (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(seconds=120)).isoformat(),
            "HEXALITH_G4_EVENTSTORE_URL": f"http://127.0.0.1:{self.server.server_port}/",
            "HEXALITH_G4_TEST_REPORT_PATH": str(self.report),
            "HEXALITH_G4_ACCESS_TOKEN": "not-a-real-token-never-send-or-retain"})

    def close_server(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=5)

    def run_entry(self):
        return subprocess.run(["python3", "-E", "-s", "-B", str(self.script)], env=self.environment,
                              capture_output=True, text=True, timeout=10)

    def result(self):
        return ET.parse(self.report).getroot().find(".//{*}UnitTestResult").attrib["outcome"]

    def test_healthy_extension_is_independent_of_overall_distributed_readiness(self):
        result = self.run_entry()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("Passed", self.result())
        self.assertEqual("/ready", self.requests[0][0])
        self.assertNotIn("Authorization", self.requests[0][1])
        self.assertNotIn(self.environment["HEXALITH_G4_ACCESS_TOKEN"], result.stdout + result.stderr + self.report.read_text())

    def test_actual_sdk_uppercase_hash_handoff_passes(self):
        for name in ["HEXALITH_G4_TEST_MANIFEST_HASH", "HEXALITH_G4_TEST_ENTRY_HASH"]:
            self.environment[name] = self.environment[name].upper()
        self.assertEqual(0, self.run_entry().returncode)
        self.assertEqual("Passed", self.result())

    def test_healthy_extension_with_http200_also_passes(self):
        self.status = 200
        self.assertEqual(0, self.run_entry().returncode)
        self.assertEqual("Passed", self.result())

    def test_unhealthy_extension_fails_and_retains_a_failed_report(self):
        self.body["results"]["eventstore-extensions"]["status"] = "Unhealthy"
        self.assertNotEqual(0, self.run_entry().returncode)
        self.assertEqual("Failed", self.result())

    def test_declared_major_mismatch_is_refused(self):
        self.body["results"]["eventstore-extensions"]["data"]["platform-test"] = 2
        self.assertNotEqual(0, self.run_entry().returncode)
        self.assertEqual("Failed", self.result())

    def test_boolean_is_not_an_api_major(self):
        self.body["results"]["eventstore-extensions"]["data"]["platform-test"] = True
        self.assertNotEqual(0, self.run_entry().returncode)

    def test_wrong_extension_or_package_is_refused(self):
        self.body["results"]["eventstore-extensions"]["description"] = "platform-test@1 (Foreign.Package)"
        self.assertNotEqual(0, self.run_entry().returncode)

    def test_wrong_native_owner_is_refused_before_http(self):
        self.environment["HEXALITH_G4_TEST_OWNER"] = "platform"
        self.assertNotEqual(0, self.run_entry().returncode)
        self.assertEqual([], self.requests)
        self.assertFalse(self.report.exists())

    def test_wrong_profile_or_mode_is_refused_before_http(self):
        for name, value in [("HEXALITH_G4_TEST_NAME", "other"), ("HEXALITH_G4_TEST_MODE", "source")]:
            with self.subTest(name=name):
                original = self.environment[name]
                self.environment[name] = value
                self.assertNotEqual(0, self.run_entry().returncode)
                self.environment[name] = original
        self.assertEqual([], self.requests)

    def test_changed_source_hashes_are_refused_before_http(self):
        for name in ["HEXALITH_G4_TEST_MANIFEST_HASH", "HEXALITH_G4_TEST_ENTRY_HASH"]:
            with self.subTest(name=name):
                original = self.environment[name]
                self.environment[name] = "f" * 64
                self.assertNotEqual(0, self.run_entry().returncode)
                self.environment[name] = original
        self.assertEqual([], self.requests)

    def test_foreign_producer_is_refused_before_http(self):
        document = json.loads(self.producer.read_bytes())
        document["ownerRepositoryIdentity"] = "Hexalith.Platformtestextension"
        self.producer.write_text(json.dumps(document))
        self.assertNotEqual(0, self.run_entry().returncode)
        self.assertEqual([], self.requests)

    def test_invalid_or_expired_run_is_refused_before_http(self):
        self.environment["HEXALITH_G4_RUN_ID"] = "invalid"
        self.assertNotEqual(0, self.run_entry().returncode)
        self.environment["HEXALITH_G4_RUN_ID"] = "a" * 32
        self.environment["HEXALITH_G4_EXECUTION_DEADLINE_UTC"] = "2000-01-01T00:00:00+00:00"
        self.assertNotEqual(0, self.run_entry().returncode)
        self.assertEqual([], self.requests)

    def test_malformed_or_oversized_health_body_fails(self):
        for body in [b"not-json", b"x" * 262145]:
            with self.subTest(size=len(body)):
                self.body = body
                self.assertNotEqual(0, self.run_entry().returncode)
                self.assertEqual("Failed", self.result())
                self.report.unlink()

    def test_authentication_failure_cannot_become_a_healthy_extension(self):
        self.status = 401
        self.assertNotEqual(0, self.run_entry().returncode)
        self.assertEqual("Failed", self.result())

    def test_redirect_to_healthy_target_is_refused_without_second_request(self):
        self.redirect = True
        self.assertNotEqual(0, self.run_entry().returncode)
        self.assertEqual("Failed", self.result())
        self.assertEqual(["/ready"], [path for path, _ in self.requests])

    def test_expiry_during_http_cannot_emit_a_passing_report(self):
        self.chunk_delays = True
        self.environment["HEXALITH_G4_EXECUTION_DEADLINE_UTC"] = (
            datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(seconds=0.25)).isoformat()
        self.assertNotEqual(0, self.run_entry().returncode)
        self.assertEqual("Failed", self.result())

    def test_existing_report_is_never_overwritten(self):
        self.report.write_bytes(b"existing evidence")
        self.assertNotEqual(0, self.run_entry().returncode)
        self.assertEqual(b"existing evidence", self.report.read_bytes())


if __name__ == "__main__":
    unittest.main()
