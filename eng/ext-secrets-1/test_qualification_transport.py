"""Engineering transport checks use local fake credentials only; no qualification authority."""
import http.server
import os
from pathlib import Path
import tempfile
import threading
import time
import unittest

import qualify as q
from test_qualify import Fixture


class QualificationCredentialTransportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.directory = Path(self.temp.name)
        self.channel = self.directory / "channel"
        self.caller = self.directory / "caller"
        self.channel.write_text("offline-channel")
        self.caller.write_text("offline-workload")
        self.config = {"qualification": {"daprApiCredentialReference": "file:" + str(self.channel),
            "enrollmentCredentialReference": "file:" + str(self.caller), "requestTimeoutSeconds": 1,
            "executor": {"appId": "offline-executor"}}}

    def tearDown(self):
        self.temp.cleanup()

    def test_channel_and_independent_caller_credentials_are_distinct_headers(self):
        observed = []
        class Handler(http.server.BaseHTTPRequestHandler):
            def log_message(self, *args): pass
            def do_POST(self):
                self.rfile.read(int(self.headers["Content-Length"]))
                observed.append((self.path, self.headers["dapr-api-token"], self.headers["Authorization"]))
                self.send_response(200)
                self.send_header("Content-Length", "2")
                self.end_headers()
                self.wfile.write(b"{}")
        server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            transport = q.DaprTransport(self.config)
            transport.port = server.server_port
            self.assertEqual({}, transport.call("executor", "execute", {"requestId": "a" * 64}))
            self.assertEqual([("/v1.0/invoke/offline-executor/method/ext-secrets-1/v1/execute",
                               "offline-channel", "Bearer offline-workload")], observed)
        finally:
            server.shutdown()
            server.server_close()
            thread.join()

    def test_absent_caller_credential_blocks_before_connection(self):
        self.caller.unlink()
        with self.assertRaises(q.Unavailable) as caught:
            q.DaprTransport(self.config).call("executor", "execute", {})
        self.assertEqual(["MountedQualificationCallerCredential"], caught.exception.inputs)

    def test_bounded_credential_read_accepts_projected_link_and_rejects_fifo_oversize_and_header_injection(self):
        fifo = self.directory / "fifo"
        os.mkfifo(fifo)
        link = self.directory / "link"
        link.symlink_to(self.caller)
        self.assertTrue(q.mounted_credential_reference("file:" + str(link)))
        self.assertEqual("offline-workload", q.read_mounted_credential("file:" + str(link), "Credential"))
        for path in (fifo,):
            with self.subTest(path=path.name):
                self.assertFalse(q.mounted_credential_reference("file:" + str(path)))
                start = time.monotonic()
                with self.assertRaises(q.Unavailable):
                    q.read_mounted_credential("file:" + str(path), "Credential")
                self.assertLess(time.monotonic() - start, .5)
        for value in ("x" * 8193, "marker\r\nInjected: value", "", "non-ascii-\u00e9"):
            self.caller.write_text(value)
            with self.subTest(length=len(value)), self.assertRaises(q.Unavailable) as caught:
                q.read_mounted_credential("file:" + str(self.caller), "Credential")
            self.assertNotIn("marker", str(caught.exception))
        self.channel.write_text("a" * 8192)
        self.assertEqual("a" * 8192, q.read_mounted_credential("file:" + str(self.channel), "Credential"))


class CohortPrerequisiteTests(unittest.TestCase):
    def test_fully_pinned_standalone_preflight_refuses_before_any_runtime_call(self):
        with tempfile.TemporaryDirectory() as directory:
            fixture = Fixture(directory)
            with fixture.register.open("a") as stream:
                stream.write("\n### OQ-5.4-1 — Bounded Owner-Qualification Cohort\n")
            with unittest.mock.patch.object(q.DaprTransport, "call", side_effect=AssertionError("no runtime call")):
                with self.assertRaises(q.Unavailable) as caught:
                    fixture.preflight()
            self.assertIn("OQ-5.4-1.IndependentCurrentCohortBindingAndOwners", caught.exception.inputs)
            self.assertEqual([], fixture.calls)

    def test_standalone_plan_cannot_run_accepted_joint_cohort(self):
        with tempfile.TemporaryDirectory() as directory:
            register = Path(directory) / "register.md"
            register.write_text("""### EXT-HOST-1 — host
| `TargetVersionOrCommit` | `aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa` |
| `Command` | Command: `host --mode Full` |
### EXT-PROTECTION-1 — protection
| `AcceptedStatus` | `Uncommitted` |
## Owner Qualification Cohorts
### OQ-5.4-1 — bounded
""")
            self.assertEqual(["OQ-5.4-1.CommittedProtectionOwner", "OQ-5.4-1.SharedPlatformTarget",
                "OQ-5.4-1.AcceptedPinnedHostProfile", "OQ-5.4-1.IndependentCurrentCohortBindingAndOwners"],
                q.cohort_prerequisite_inputs(register, "b" * 40))
            text = register.read_text().replace("Uncommitted", "Committed").replace("a" * 40, "b" * 40).replace("host --mode Full", "host --mode Full --profile /accepted/profile.json")
            text = text.replace("### EXT-PROTECTION-1 — protection", "| `AcceptedStatus` | `Committed` |\n| `TargetIntegrationDate` | `2026-10-08` |\n### EXT-PROTECTION-1 — protection")
            text = text.replace("## Owner Qualification Cohorts", "| `TargetVersionOrCommit` | `" + "c" * 40 + "` |\n| `TargetIntegrationDate` | `2026-10-08` |\n| `Command` | Command: `protection --full` |\n## Owner Qualification Cohorts")
            register.write_text(text)
            self.assertEqual(["OQ-5.4-1.IndependentCurrentCohortBindingAndOwners"], q.cohort_prerequisite_inputs(register, "b" * 40))

    def test_missing_cohort_incomplete_owner_and_flag_as_profile_remain_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "register"
            path.write_text("no cohort")
            self.assertEqual(["OQ-5.4-1.AuthoritativeCohortRecord", "OQ-5.4-1.IndependentCurrentCohortBindingAndOwners"],
                             q.cohort_prerequisite_inputs(path, "a" * 40))
            path.write_text("### EXT-HOST-1\n| `TargetVersionOrCommit` | `" + "a" * 40 + "` |\n"
                            "| `AcceptedStatus` | `Uncommitted` |\n| `Command` | Command: `host --profile --garbage` |\n"
                            "### EXT-PROTECTION-1\n| `AcceptedStatus` | `Committed` |\n### OQ-5.4-1\n")
            self.assertEqual(["OQ-5.4-1.CommittedProtectionOwner", "OQ-5.4-1.SharedPlatformTarget",
                              "OQ-5.4-1.AcceptedPinnedHostProfile", "OQ-5.4-1.IndependentCurrentCohortBindingAndOwners"],
                             q.cohort_prerequisite_inputs(path, "a" * 40))

    def test_register_loading_rejects_fifo_and_oversize_before_interpretation(self):
        with tempfile.TemporaryDirectory() as directory:
            fifo, oversized = Path(directory) / "fifo", Path(directory) / "large"
            os.mkfifo(fifo)
            oversized.write_bytes(b"x" * (1024 * 1024 + 1))
            for path in (fifo, oversized):
                with self.subTest(path=path.name), self.assertRaises(ValueError):
                    q.cohort_prerequisite_inputs(path, "a" * 40)

    def test_preflight_interprets_one_register_snapshot(self):
        with tempfile.TemporaryDirectory() as directory:
            fixture = Fixture(directory)
            actual = q.bounded_public_file
            reads = []
            def capture(path, limit):
                if Path(path) == fixture.register:
                    reads.append(path)
                    captured = actual(path, limit)
                    fixture.register.write_text("changed after capture")
                    return captured
                return actual(path, limit)
            with unittest.mock.patch.object(q, "bounded_public_file", side_effect=capture):
                with self.assertRaises(q.Unavailable) as caught:
                    fixture.preflight()
            self.assertNotIn("AuthoritativeAcceptedTargetAndOwnerCommand", caught.exception.inputs)
            self.assertIn("OQ-5.4-1.AuthoritativeCohortRecord", caught.exception.inputs)
            self.assertEqual(1, len(reads))


if __name__ == "__main__":
    unittest.main()
