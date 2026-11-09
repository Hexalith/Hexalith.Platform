"""Prove exact SDK suite selection and package dependency refusals without running services."""
import json
from pathlib import Path
import tempfile
import unittest

from run_tuple_ci import COMPOSITION_TEST_DEPENDENCIES, sdk_suite, validate_profile, verify_extension_catalog_pin, verify_historical_catalog_pin, verify_test_sdk


class SdkSuiteTests(unittest.TestCase):
    def test_legacy_profile_retains_its_recorded_default(self):
        self.assertEqual(sdk_suite("current", None), ("eng/tuple-ci-suites.json", []))
        self.assertEqual(sdk_suite("current", "4.1.0")[1], ["-p:HexalithEventStoreVersion=4.1.0"])

    def test_extension_suite_has_one_exact_sdk_and_nine_mandatory_cases(self):
        self.assertEqual(sdk_suite("extensions", "4.2.0"), ("eng/tuple-ci-extension-suites.json", ["-p:HexalithEventStoreVersion=4.2.0"]))
        profile = json.loads((Path(__file__).parent / "tuple-ci-extension-suites.json").read_text())
        self.assertEqual(validate_profile(profile), 9)
        retained = json.loads((Path(__file__).parent / "tuple-ci-suites.json").read_text())
        self.assertEqual(validate_profile(retained), 412)

    def test_default_sdk42_base_requires_all_controls_and_actual_entry_cases(self):
        self.assertEqual(sdk_suite("base42", "4.2.0")[0], "eng/tuple-ci-sdk42-base-suites.json")
        profile = json.loads((Path(__file__).parent / "tuple-ci-sdk42-base-suites.json").read_text())
        self.assertEqual(validate_profile(profile), 97)
        self.assertEqual([group["requiredCount"] for group in profile["groups"]], [41, 51, 3, 2])
        for version in [None, "4.1.0", "4.2.1"]:
            with self.subTest(version=version), self.assertRaises(ValueError):
                sdk_suite("base42", version)

    def test_upstream_current_sdk42_inventory_remains_separately_mandatory(self):
        self.assertEqual(sdk_suite("current42", "4.2.0")[0], "eng/tuple-ci-sdk42-current-suites.json")
        profile = json.loads((Path(__file__).parent / "tuple-ci-sdk42-current-suites.json").read_text())
        self.assertEqual(validate_profile(profile), 456)
        for version in [None, "4.1.0", "4.2.1"]:
            with self.subTest(version=version), self.assertRaises(ValueError):
                sdk_suite("current42", version)

    def test_unknown_or_mixed_suite_selection_never_substitutes(self):
        for suite, version in [("extensions", None), ("extensions", "4.1.0"), ("extensions", "4.2.1"),
                               ("current", "4.2.0"), ("arbitrary", "4.2.0")]:
            with self.subTest(suite=suite, version=version), self.assertRaises(ValueError):
                sdk_suite(suite, version)

    def test_extension_suite_requires_the_single_real_catalog_pin(self):
        with tempfile.TemporaryDirectory() as root:
            path = Path(root) / "catalog.props"
            for pins, valid in [("<HexalithEventStoreVersion>4.2.0</HexalithEventStoreVersion>", True),
                                ("<HexalithEventStoreVersion>4.1.0</HexalithEventStoreVersion>", False),
                                ("<HexalithEventStoreVersion>4.2.0</HexalithEventStoreVersion>" * 2, False), ("", False)]:
                path.write_text("<Project><PropertyGroup>" + pins + "</PropertyGroup></Project>")
                if valid:
                    verify_extension_catalog_pin(path)
                else:
                    with self.assertRaises(ValueError):
                        verify_extension_catalog_pin(path)

    def test_historical_controls_require_the_actual_old_catalog(self):
        with tempfile.TemporaryDirectory() as root:
            path = Path(root) / "catalog.props"
            for version, valid in [("4.1.0", True), ("4.2.0", False), ("4.1.1", False)]:
                path.write_text("<Project><PropertyGroup><HexalithEventStoreVersion>" + version + "</HexalithEventStoreVersion></PropertyGroup></Project>")
                if valid:
                    verify_historical_catalog_pin(path)
                else:
                    with self.assertRaises(ValueError):
                        verify_historical_catalog_pin(path)

    def test_actual_test_dependency_identity_rejects_missing_source_or_other_sdk(self):
        with tempfile.TemporaryDirectory() as root:
            assembly = Path(root) / "Tests.dll"
            path = assembly.with_suffix(".deps.json")
            dependencies = {name.removesuffix(".dll") + "/4.2.0": {"type": "package"}
                            for name in COMPOSITION_TEST_DEPENDENCIES if name.startswith("Hexalith.EventStore.")}
            path.write_text(json.dumps({"libraries": dependencies}))
            verify_test_sdk(assembly, "4.2.0")
            for fault in ["missing", "project", "other-sdk"]:
                changed = json.loads(json.dumps(dependencies))
                first = next(iter(changed))
                if fault == "missing":
                    del changed[first]
                elif fault == "project":
                    changed[first]["type"] = "project"
                else:
                    changed[first.replace("4.2.0", "4.1.0")] = changed.pop(first)
                path.write_text(json.dumps({"libraries": changed}))
                with self.subTest(fault=fault), self.assertRaises(ValueError):
                    verify_test_sdk(assembly, "4.2.0")


if __name__ == "__main__":
    unittest.main()
