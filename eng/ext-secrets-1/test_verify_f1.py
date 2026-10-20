"""Regressions for the synthetic F1 evidence audit, with no runtime or native effects."""
import json
from pathlib import Path
import tempfile
import unittest

from verify_f1 import audit, BEHAVIORAL_COVERAGE, verify_owner_capture_fixture, OWNER_CAPTURE_FIXTURE, OWNER_CAPTURE_PATH, OWNER_CAPTURE_SHA256, CANDIDATE_CAPTURES


class F1EvidenceAuditTests(unittest.TestCase):
    def test_local_owner_fixture_requires_exact_external_source_and_approved_pin(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            external = root / OWNER_CAPTURE_PATH
            external.parent.mkdir(parents=True)
            original = OWNER_CAPTURE_FIXTURE.read_bytes()
            external.write_bytes(original)
            self.assertEqual(OWNER_CAPTURE_SHA256, verify_owner_capture_fixture(root)["sha256"])
            # Even a newline-only external change must fail the byte-exact comparison.
            external.write_bytes(original + b"\n")
            with self.assertRaisesRegex(ValueError, "fixture and external source SHA"):
                verify_owner_capture_fixture(root)
            # Updating both copies together cannot silently replace the approved pin.
            changed_fixture = root / "changed-fixture.cs"
            changed_fixture.write_bytes(original + b"\n")
            with self.assertRaisesRegex(ValueError, "approved pin"):
                verify_owner_capture_fixture(root, changed_fixture)

    def evidence(self, available=False, live=False):
        captures = {name: {"DataBytes": values[0], "DataSha256": values[1], "ApiProtobufBytes": values[2],
                           "ApiProtobufSha256": values[3], "GrpcFrameBytes": values[2] + 5, "SdkCalls": 1,
                           "MaxSendMessageBytes": 134217728, "MaxReceiveMessageBytes": 1048575, "RealSidecarPathExecuted": False}
                    for name, values in CANDIDATE_CAPTURES.items()}
        captures.update(SidecarOrComponentExecuted=False, ProductionInvokerProfileChanged=False, DecodeCapBytes=67108864,
                        FullManifestRequestBoundBytes=45508608, FullTransitionRequestBoundBytes=48933548,
                        ComponentForwardedMetadataWireBudgetBytes=66560)
        return {"DependencyAvailable": available, "LiveReady": live, "CandidateSdkCaptures": captures}

    def check(self, *, result="Pass", method="EmitDeterministicF1Evidence", total=None,
              available=False, live=False, evidence=True, behavioral=True, omit=None, mutate=None):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            xml, report = root / "tests.xml", root / "report.json"
            entries = [("Hexalith.Platform.Secrets.Runtime.Tests.CustodyBindingFeasibilityTests", method)]
            if behavioral:
                entries.extend((cls, name) for cls, methods in BEHAVIORAL_COVERAGE.items() for name in methods if (cls, name) != omit)
            count = len(entries) if total is None else total
            tests = "".join(f'<test type="{cls}" method="{name}" result="{result}"/>' for cls, name in entries)
            xml.write_text(f'<assemblies><assembly total="{count}" passed="{count}" failed="0" errors="0" skipped="0" not-run="0">'
                           f'<collection>{tests}</collection></assembly></assemblies>')
            if evidence:
                data = self.evidence(available, live)
                if mutate:
                    mutate(data)
                report.write_text(json.dumps(data))
            return audit(xml, report)

    def test_passing_synthetic_report_stays_unavailable(self):
        counts, report = self.check()
        self.assertEqual(1 + sum(map(len, BEHAVIORAL_COVERAGE.values())), counts["f1Passed"])
        self.assertFalse(report["DependencyAvailable"])

    def test_candidate_capture_requires_exact_bytes_hashes_one_call_and_explicit_limits(self):
        for key, value in (("DataBytes", 1), ("ApiProtobufSha256", "0" * 64), ("SdkCalls", 2),
                           ("MaxSendMessageBytes", 4194304), ("RealSidecarPathExecuted", True)):
            with self.subTest(key=key), self.assertRaises(ValueError):
                self.check(mutate=lambda e: e["CandidateSdkCaptures"]["RetainedMaximumManifest"].update({key: value}))
        with self.assertRaises(ValueError):
            self.check(mutate=lambda e: e.pop("CandidateSdkCaptures"))

    def test_missing_report_cannot_use_earlier_evidence(self):
        with self.assertRaises(FileNotFoundError):
            self.check(evidence=False)

    def test_missing_or_unrun_tests_reject_report(self):
        for arguments in ({"total": 2}, {"result": "Skip"}, {"result": "Fail"}):
            with self.subTest(arguments=arguments), self.assertRaises(ValueError):
                self.check(**arguments)

    def test_synthetic_report_cannot_authorize_live_or_availability(self):
        for arguments in ({"available": True}, {"live": True}, {"available": None}):
            with self.subTest(arguments=arguments), self.assertRaises(ValueError):
                self.check(**arguments)

    def test_arbitrary_passing_test_cannot_replace_evidence_producer(self):
        with self.assertRaisesRegex(ValueError, "evidence producer"):
            self.check(method="SomeOtherPassingTest")

    def test_producer_only_report_fails_behavioral_coverage(self):
        with self.assertRaisesRegex(ValueError, "behavioral coverage"):
            self.check(behavioral=False)

    def test_missing_behavioral_method_fails_even_with_producer(self):
        for cls, methods in BEHAVIORAL_COVERAGE.items():
            with self.subTest(class_name=cls), self.assertRaisesRegex(ValueError, "behavioral coverage"):
                self.check(omit=(cls, methods[0]))


if __name__ == "__main__":
    unittest.main()
