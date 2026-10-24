"""Focused portability regression for the retained lifecycle audit."""
from pathlib import Path
import json
import shutil
import subprocess
import sys
import tempfile
import unittest


class RetainedAuditTests(unittest.TestCase):
    def test_relocated_evidence_does_not_require_original_runtime(self):
        original = Path(__file__).resolve().parent
        with tempfile.TemporaryDirectory(prefix="story25-relocated-") as temporary:
            retained = Path(temporary) / "evidence"
            retained.mkdir()
            for name in ("audit.py", "schema-parity.json", "builds-tests.xml", "platform-live-tests.xml", "matrix-audit.json"):
                shutil.copyfile(original / name, retained / name)
            for name in ("schemas", "live"):
                shutil.copytree(original / name, retained / name)
            for command in (retained / "live").glob("command-*.json"):
                document = json.loads(command.read_text())
                document["testResultPath"] = str(retained / "unavailable-original-runtime" / "test-result.json")
                command.write_text(json.dumps(document))
            completed = subprocess.run([sys.executable, str(retained / "audit.py")], cwd=temporary, capture_output=True, text=True, check=False)
            self.assertEqual(completed.returncode, 0, completed.stdout + completed.stderr)
            summary = json.loads((retained / "parsed-audit.json").read_text())
            self.assertEqual(len(summary["liveResults"]), 4)
            for result in summary["liveResults"]:
                self.assertFalse(result["currentFilesystemChecks"]["originalRuntimePathsChecked"])
                self.assertTrue(result["currentFilesystemChecks"]["retainedResultAccessible"])


if __name__ == "__main__":
    unittest.main()
