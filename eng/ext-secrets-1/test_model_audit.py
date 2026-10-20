"""Executable regressions for the Local model evidence freshness/producer guards."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from xml.sax.saxutils import quoteattr

ROOT = Path(__file__).resolve().parents[2]
PRODUCER = "BoundedExplorationAndUnsafeCounterexamplesProduceValueFreeEvidence"
CLASS = "Hexalith.Platform.Custody.Tests.CustodySharedCutModelTests"


class ModelAuditTests(unittest.TestCase):
    def invoke(self, *, method=PRODUCER, result="Pass", fresh=True, fresh_xml=True, fail=False):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            report, xml = root / "report.json", root / "tests.xml"
            report.write_text("STALE REPORT MUST BE REMOVED")
            xml_payload = ("<assemblies><assembly><collection><test type=" + quoteattr(CLASS)
                           + " method=" + quoteattr(method) + " result=" + quoteattr(result)
                           + "/></collection></assembly></assemblies>")
            xml.write_text("STALE XML MUST BE REMOVED")
            payload = json.dumps(dict(Kind="OfflineModelOnly", EvidenceProducer=PRODUCER,
                                      LiveReady=False, DependencyAvailable=False))
            # Paths/data travel as environment variables; no shell interpolation or credentials.
            runner = root / "run.ps1"
            runner.write_text("""
$ErrorActionPreference = 'Stop'
. $env:MODEL_AUDIT_HELPER
try {
    $hash = Invoke-CustodySharedCutModelAudit -ReportPath $env:MODEL_AUDIT_REPORT -XmlPath $env:MODEL_AUDIT_XML -RunTests {
        if (Test-Path -LiteralPath $env:MODEL_AUDIT_REPORT) { throw 'stale-report-survived' }
        if (Test-Path -LiteralPath $env:MODEL_AUDIT_XML) { throw 'stale-xml-survived' }
        if ($env:MODEL_AUDIT_FAIL -eq '1') { throw 'runner-failed' }
        if ($env:MODEL_AUDIT_FRESH -eq '1') { [IO.File]::WriteAllText($env:MODEL_AUDIT_REPORT, $env:MODEL_AUDIT_PAYLOAD) }
        if ($env:MODEL_AUDIT_FRESH_XML -eq '1') { [IO.File]::WriteAllText($env:MODEL_AUDIT_XML, $env:MODEL_AUDIT_XML_PAYLOAD) }
    }
    Write-Output $hash
} catch { Write-Output $_.Exception.Message; exit 3 }
""")
            environment = dict(os.environ, MODEL_AUDIT_HELPER=str(ROOT / "eng/ext-secrets-1/model_audit.ps1"),
                               MODEL_AUDIT_REPORT=str(report), MODEL_AUDIT_XML=str(xml),
                               MODEL_AUDIT_PAYLOAD=payload, MODEL_AUDIT_FRESH="1" if fresh else "0",
                               MODEL_AUDIT_XML_PAYLOAD=xml_payload, MODEL_AUDIT_FRESH_XML="1" if fresh_xml else "0",
                               MODEL_AUDIT_FAIL="1" if fail else "0")
            pwsh = shutil.which("pwsh")
            self.assertIsNotNone(pwsh, "Local gate requires pwsh to exercise its evidence guards")
            execution = subprocess.run([pwsh, "-NoProfile", "-File", str(runner)], env=environment,
                                       text=True, capture_output=True, check=False)
            return execution, report.exists(), xml.exists(), hashlib.sha256(payload.encode()).hexdigest()

    def test_fresh_report_hashed_only_after_specific_producer_passes(self):
        result, exists, xml_exists, expected = self.invoke()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertTrue(exists)
        self.assertTrue(xml_exists)
        self.assertEqual(expected, result.stdout.strip())

    def test_stale_report_removed_even_when_runner_fails(self):
        result, exists, xml_exists, _ = self.invoke(fail=True)
        self.assertEqual(3, result.returncode)
        self.assertIn("runner-failed", result.stdout)
        self.assertFalse(exists)
        self.assertFalse(xml_exists)

    def test_prior_report_cannot_satisfy_missing_fresh_evidence(self):
        result, exists, _, _ = self.invoke(fresh=False)
        self.assertEqual(3, result.returncode)
        self.assertIn("Fresh shared-cut model report was not produced", result.stdout)
        self.assertFalse(exists)

    def test_prior_xml_cannot_satisfy_missing_fresh_test_execution(self):
        result, report_exists, xml_exists, _ = self.invoke(fresh_xml=False)
        self.assertEqual(3, result.returncode)
        self.assertIn("Fresh shared-cut model test XML was not produced", result.stdout)
        self.assertTrue(report_exists)
        self.assertFalse(xml_exists)

    def test_arbitrary_passing_class_method_cannot_replace_producer(self):
        result, _, _, _ = self.invoke(method="AnotherModelTest")
        self.assertEqual(3, result.returncode)
        self.assertIn("specific shared-cut model evidence-producing test", result.stdout)

    def test_producer_must_pass(self):
        for outcome in ("Fail", "Skip", "NotRun"):
            with self.subTest(outcome=outcome):
                result, _, _, _ = self.invoke(result=outcome)
                self.assertEqual(3, result.returncode)
                self.assertIn("specific shared-cut model evidence-producing test", result.stdout)

    def test_local_gate_registers_class_and_uses_guarded_execution(self):
        script = (ROOT / "eng/verify-ext-secrets-1.ps1").read_text()
        self.assertIn("'" + CLASS + "'", script)
        self.assertIn("Invoke-CustodySharedCutModelAudit -ReportPath $modelReportPath -XmlPath $xmlPath -RunTests", script)
        self.assertIn("ModelReportSha256 = $modelReportSha256", script)


if __name__ == "__main__":
    unittest.main()
