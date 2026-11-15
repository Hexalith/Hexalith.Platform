"""Current runtime selection and copied-reader MSBuild propagation; no services."""
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

from run_tuple_ci import tuple_runtime_context

ROOT = Path(__file__).resolve().parents[1]


class TupleRuntimeContextTests(unittest.TestCase):
    def test_current_context_cannot_substitute_a_legacy_or_future_generation_or_suite(self):
        binding = {"producer": {"sourceRevision": "1" * 40}, "publication": {"version": "5.0.0"}}
        for version in ("5.0.0", "6.0.0"):
            binding["publication"]["version"] = version
            for suite in ("current42", "base42", "extensions"):
                with self.subTest(version=version, suite=suite):
                    result = tuple_runtime_context(binding, suite, "2" * 64)
                    self.assertEqual(result["targetFramework"], "net11.0")
                    self.assertEqual(result["toolVersion"], version)
                    self.assertEqual(result["purpose"], "current-net11-with-explicit-public-sdk42")
                    self.assertIs(result["nativeAcceptance"], False)
            for suite in ("current", "unknown"):
                with self.subTest(version=version, suite=suite), self.assertRaises(ValueError):
                    tuple_runtime_context(binding, suite, "2" * 64)
        for version in ("4.44.3", "4.45.0", "4.47.0", "4.48.3", "7.0.0", "11.0.0"):
            binding["publication"]["version"] = version
            with self.subTest(version=version), self.assertRaises(ValueError):
                tuple_runtime_context(binding, "current42", "2" * 64)

    def test_actual_msbuild_and_copied_reader_keep_current_net11_without_a_manifest_switch(self):
        with tempfile.TemporaryDirectory(prefix="platform-runtime-context-") as temporary:
            root = Path(temporary)
            (root / "eng").mkdir()
            (root / ".config").mkdir()
            shutil.copyfile(ROOT / "Directory.Build.props", root / "Directory.Build.props")
            shutil.copyfile(ROOT / "eng/SourceRestore.props", root / "eng/SourceRestore.props")
            outside = root / "copied-reader"; outside.mkdir()
            project = outside / "Reader.proj"
            project.write_text('<Project><Import Project="' + str(root / "Directory.Build.props") + '"/></Project>')
            for version in (None, "6.0.5", "4.47.0", "99.0.0"):
                manifest_path = root / ".config/dotnet-tools.json"
                if version is None:
                    manifest_path.unlink(missing_ok=True)
                else:
                    manifest = {"tools": {"hexalith.builds.module.cli": {"version": version}}}
                    manifest_path.write_text(json.dumps(manifest, indent=2))
                with self.subTest(version=version):
                    result = subprocess.run(["dotnet", "msbuild", str(project), "-nologo", "-getProperty:TargetFramework"],
                                            cwd=outside, capture_output=True, text=True, timeout=30, check=True)
                    self.assertEqual(result.stdout.strip(), "net11.0")



if __name__ == "__main__": unittest.main()
