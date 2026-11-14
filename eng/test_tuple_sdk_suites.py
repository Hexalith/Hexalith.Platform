"""Prove exact SDK suite selection and package dependency refusals without running services."""
import json
from pathlib import Path
import tempfile
import unittest

from test_current_catalog_view import CurrentCatalogViewTests
from run_tuple_ci import COMPOSITION_TEST_DEPENDENCIES, sdk_suite, sdk42_collecting_cohort, validate_profile, verify_collecting_cohort, verify_extension_catalog_pin, verify_historical_catalog_pin, verify_test_sdk


class SdkSuiteTests(unittest.TestCase):
    def test_exact_generation6_current42_collection_keeps_its_measured_public_family(self):
        from test_current_catalog_view import generation6_binding
        binding = generation6_binding()
        cohort = sdk42_collecting_cohort(binding, 'current42')
        self.assertEqual(cohort, {'HexalithEventStoreVersion': '4.2.0', 'HexalithTenantsVersion': '7.0.7',
            'HexalithMemoriesVersion': '3.0.27', 'HexalithPartiesVersion': '3.1.1', 'HexalithMcpCliVersion': '2.0.1'})
        for suite in ('base42', 'extensions'):
            with self.subTest(suite=suite):
                self.assertEqual(sdk42_collecting_cohort(binding, suite), cohort)
        for suite in ('current', 'unknown'):
            with self.subTest(suite=suite):
                self.assertIsNone(sdk42_collecting_cohort(binding, suite))
        for field, value in (('version', '6.0.1'), ('sourceRevision', 'e' * 40)):
            changed = generation6_binding()
            changed['publication'][field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                sdk42_collecting_cohort(changed, 'current42')

    def test_exact_generation601_uses_the_same_measured_cohort_without_qualifying_other_generations(self):
        from test_current_catalog_view import generation6_binding
        for suite in ("current42", "base42", "extensions"):
            with self.subTest(suite=suite):
                self.assertEqual(sdk42_collecting_cohort(generation6_binding("6.0.1"), suite),
                    sdk42_collecting_cohort(generation6_binding(), suite))
        self.assertIsNone(sdk42_collecting_cohort(None, "current42"))
        changed = generation6_binding("6.0.1")
        changed["producer"]["catalog"]["blob"] = "e" * 40
        with self.assertRaisesRegex(ValueError, "exact qualified Builds6"):
            sdk42_collecting_cohort(changed, "current42")

    def test_current42_restored_graph_refuses_missing_project_or_neighbor_family_packages(self):
        from test_current_catalog_view import generation6_binding
        cohort = sdk42_collecting_cohort(generation6_binding(), 'current42')
        packages = {'Hexalith.EventStore.DomainService': '4.2.0', 'Hexalith.Tenants.Contracts': '7.0.7',
            'Hexalith.Tenants.Server': '7.0.7', 'Hexalith.Memories.Contracts': '3.0.27',
            'Hexalith.Memories.EventStore': '3.0.27', 'Hexalith.Memories.EventStoreExtensions': '3.0.27',
            'Hexalith.Parties.Contracts': '3.1.1', 'Hexalith.Parties.EventStoreExtensions': '3.1.1',
            'Hexalith.McpCli.Abstractions': '2.0.1'}
        assets = {'libraries': {name + '/' + version: {'type': 'package'} for name, version in packages.items()}}
        receipt = verify_collecting_cohort(assets, cohort)
        self.assertFalse(receipt['runtimeAcceptance'])
        self.assertFalse(receipt['candidateAcceptance'])
        for fault in ('missing', 'project', 'memories4', 'neighbor', 'duplicate', 'extra-sdk'):
            changed = json.loads(json.dumps(assets))
            name = 'Hexalith.Memories.EventStoreExtensions/3.0.27'
            if fault == 'missing': del changed['libraries'][name]
            elif fault == 'project': changed['libraries'][name]['type'] = 'project'
            elif fault == 'extra-sdk': changed['libraries']['Hexalith.EventStore.Contracts/5.1.0'] = {'type': 'package'}
            else:
                changed['libraries']['Hexalith.Memories.EventStoreExtensions/' + ('4.0.0' if fault == 'memories4' else '3.0.28')] = {'type': 'package'}
                if fault != 'duplicate': del changed['libraries'][name]
            with self.subTest(fault=fault), self.assertRaisesRegex(ValueError, 'exact public cohort|Every restored EventStore'):
                verify_collecting_cohort(changed, cohort)

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
        self.assertEqual(validate_profile(profile), 478)
        binding_test = "Hexalith.Platform.Composition.Tests.PlatformAppModelTests.PackageCatalogParticipantsBindBeforeContainersOrSidecarsAreAdded"
        binding_groups = [group for group in profile["groups"] if binding_test in group["selectors"]]
        self.assertEqual(len(binding_groups), 1)
        self.assertEqual(binding_groups[0]["filter"], "-method")
        self.assertEqual(binding_groups[0]["requiredCount"], 8)
        for version in [None, "4.1.0", "4.2.1"]:
            with self.subTest(version=version), self.assertRaises(ValueError):
                sdk_suite("current42", version)

    def test_unknown_or_mixed_suite_selection_never_substitutes(self):
        for suite, version in [("extensions", None), ("extensions", "4.1.0"), ("extensions", "4.2.1"),
                               ("current", "4.2.0"), ("arbitrary", "4.2.0")]:
            with self.subTest(suite=suite, version=version), self.assertRaises(ValueError):
                sdk_suite(suite, version)

    def test_sdk42_suite_selection_does_not_override_historical_companions(self):
        for suite in ("current42", "base42", "extensions"):
            with self.subTest(suite=suite):
                self.assertEqual(sdk_suite(suite, "4.2.0")[1], ["-p:HexalithEventStoreVersion=4.2.0"])
        self.assertEqual(sdk_suite("current", None)[1], [])
        self.assertEqual(sdk_suite("current", "4.1.0")[1], ["-p:HexalithEventStoreVersion=4.1.0"])

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

    def test_current_collector_rejects_missing_source_or_crossed_memories_packages(self):
        with tempfile.TemporaryDirectory() as root:
            assembly = Path(root) / "Tests.dll"
            dependencies = {name.removesuffix(".dll") + "/4.2.0": {"type": "package"}
                for name in COMPOSITION_TEST_DEPENDENCIES if name.startswith("Hexalith.EventStore.")}
            memories = {"Hexalith.Memories." + name + "/3.0.27": {"type": "package"}
                for name in ("Contracts", "EventStore", "EventStoreExtensions")}
            dependencies.update(memories)
            assembly.with_suffix(".deps.json").write_text(json.dumps({"libraries": dependencies}))
            verify_test_sdk(assembly, "4.2.0", "3.0.27")
            for name in memories:
                for fault in ("missing", "project", "4.0.0", "3.0.25"):
                    changed = json.loads(json.dumps(dependencies))
                    if fault == "missing": del changed[name]
                    elif fault == "project": changed[name]["type"] = "project"
                    else: changed[name.replace("3.0.27", fault)] = changed.pop(name)
                    assembly.with_suffix(".deps.json").write_text(json.dumps({"libraries": changed}))
                    with self.subTest(package=name, fault=fault), self.assertRaisesRegex(ValueError, "collection companion"):
                        verify_test_sdk(assembly, "4.2.0", "3.0.27")


if __name__ == "__main__":
    unittest.main()
