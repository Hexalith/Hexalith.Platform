"""Exact historical build contexts and genuine copied-reader MSBuild propagation; no services."""
from copy import deepcopy
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

from prepare_tuple_sources import FROZEN
from run_tuple_ci import tuple_runtime_context

ROOT = Path(__file__).resolve().parents[1]


class TupleRuntimeContextTests(unittest.TestCase):
    def test_only_the_complete_original_regression_tuple_selects_net10(self):
        for name, version in (("legacy", "4.44.3"), ("pre-extension", "4.45.0")):
            frozen = FROZEN[name]
            source = frozen["gitlinks"]["references/Hexalith.Builds"]
            profile = frozen["hashes"]["eng/tuple-ci-suites.json"]
            binding = {"producer": {"sourceRevision": source}, "publication": {"version": version}}
            result = tuple_runtime_context(binding, "current", profile)
            self.assertEqual(result["targetFramework"], "net10.0")
            self.assertIs(result["currentRuntimeQualification"], False)
            for field in ("source", "version", "profile", "suite"):
                changed = deepcopy(binding)
                if field == "source": changed["producer"]["sourceRevision"] = "0" * 40
                if field == "version": changed["publication"]["version"] = "4.48.3"
                with self.subTest(name=name, fault=field), self.assertRaises(ValueError):
                    tuple_runtime_context(changed, "current42" if field == "suite" else "current", "0" * 64 if field == "profile" else profile)

    def test_current_context_cannot_substitute_a_legacy_or_future_generation_or_suite(self):
        binding = {"producer": {"sourceRevision": "1" * 40}, "publication": {"version": "5.0.0"}}
        for version in ("5.0.0", "6.0.0"):
            binding["publication"]["version"] = version
            for suite in ("current42", "base42", "extensions"):
                with self.subTest(version=version, suite=suite):
                    self.assertEqual(tuple_runtime_context(binding, suite, "2" * 64)["targetFramework"], "net11.0")
            for suite in ("current", "unknown"):
                with self.subTest(version=version, suite=suite), self.assertRaises(ValueError):
                    tuple_runtime_context(binding, suite, "2" * 64)
        for version in ("4.48.3", "7.0.0", "11.0.0"):
            binding["publication"]["version"] = version
            with self.subTest(version=version), self.assertRaises(ValueError):
                tuple_runtime_context(binding, "current42", "2" * 64)

    def test_actual_msbuild_and_copied_reader_follow_only_the_owning_tool_manifest(self):
        with tempfile.TemporaryDirectory(prefix="platform-runtime-context-") as temporary:
            root = Path(temporary)
            (root / "eng").mkdir()
            (root / ".config").mkdir()
            shutil.copyfile(ROOT / "Directory.Build.props", root / "Directory.Build.props")
            shutil.copyfile(ROOT / "eng/SourceRestore.props", root / "eng/SourceRestore.props")
            outside = root / "copied-reader"; outside.mkdir()
            project = outside / "Reader.proj"
            project.write_text('<Project><Import Project="' + str(root / "Directory.Build.props") + '"/></Project>')
            for version, expected in (("4.44.3", "net10.0"), ("4.45.0", "net10.0"), ("4.47.0", "net10.0"),
                                      ("4.48.3", "net11.0"), ("5.0.0", "net11.0"), ("5.1.0", "net11.0"), ("6.0.0", "net11.0")):
                manifest = {"tools": {"unrelated": {"version": "4.44.3"}, "hexalithXbuildsYmoduleZcli": {"version": "4.44.3"}, "hexalith.builds.module.cli": {"version": version}}}
                (root / ".config/dotnet-tools.json").write_text(json.dumps(manifest, indent=2))
                with self.subTest(version=version):
                    result = subprocess.run(["dotnet", "msbuild", str(project), "-nologo", "-getProperty:TargetFramework"],
                                            cwd=outside, capture_output=True, text=True, timeout=30, check=True)
                    self.assertEqual(result.stdout.strip(), expected)


if __name__ == "__main__": unittest.main()
