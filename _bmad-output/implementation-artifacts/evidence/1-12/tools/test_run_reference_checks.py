"""Exercise portable pinned-validator discovery without running the ACL matrix."""
import importlib.util
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

SCRIPT = Path(__file__).with_name("run_reference_checks.py")
SPEC = importlib.util.spec_from_file_location("run_reference_checks", SCRIPT)
checks = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(checks)
RELATIVE = "hexalith.builds.module.cli/4.34.0/tools/net10.0/any/Hexalith.Builds.Module.Cli.dll"


class RestoredValidatorTests(unittest.TestCase):
    def test_configured_nuget_cache_is_authoritative(self):
        with tempfile.TemporaryDirectory() as temporary:
            validator = Path(temporary) / RELATIVE
            validator.parent.mkdir(parents=True)
            validator.touch()
            with patch.dict(os.environ, {"NUGET_PACKAGES": temporary}):
                self.assertEqual(checks.restored_validator(), validator)

    def test_normal_user_cache_is_used_without_an_override(self):
        with tempfile.TemporaryDirectory() as temporary:
            home = Path(temporary)
            validator = home / ".nuget/packages" / RELATIVE
            validator.parent.mkdir(parents=True)
            validator.touch()
            with patch.dict(os.environ), patch.object(checks.Path, "home", return_value=home):
                os.environ.pop("NUGET_PACKAGES", None)
                self.assertEqual(checks.restored_validator(), validator)

    def test_missing_pinned_tool_reports_the_restore_prerequisite(self):
        with tempfile.TemporaryDirectory() as temporary:
            result = subprocess.run([sys.executable, str(SCRIPT)], env={**os.environ, "NUGET_PACKAGES": temporary}, capture_output=True, text=True, check=False)
            self.assertEqual(result.returncode, 2)
            self.assertIn("Prerequisite unavailable", result.stderr)
            self.assertIn("4.34.0", result.stderr)
            self.assertIn("dotnet tool restore", result.stderr)
            self.assertIn(temporary, result.stderr)
            self.assertNotIn("Traceback", result.stderr)


if __name__ == "__main__":
    unittest.main()
