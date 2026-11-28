"""Prove exact unified suite inventories and package dependency refusals without running services."""
import json
from pathlib import Path
import tempfile
import unittest

from run_tuple_ci import (COMPOSITION_TEST_DEPENDENCIES, REQUIRED_TOTALS, validate_profile, verify_collecting_cohort,
                          verify_test_sdk)


class SdkSuiteTests(unittest.TestCase):
    def test_restored_graph_refuses_missing_project_or_neighbor_family_packages(self):
        cohort = {
            "HexalithEventStoreVersion": "5.6.0",
            "HexalithTenantsVersion": "8.1.1",
            "HexalithMemoriesVersion": "4.1.4",
            "HexalithPartiesVersion": "4.0.1",
            "HexalithMcpCliVersion": "3.0.0",
        }
        packages = {
            "Hexalith.EventStore.DomainService": "5.6.0",
            "Hexalith.Tenants.Contracts": "8.1.1",
            "Hexalith.Tenants.Server": "8.1.1",
            "Hexalith.Memories.Contracts": "4.1.4",
            "Hexalith.Memories.EventStore": "4.1.4",
            "Hexalith.Memories.EventStoreExtensions": "4.1.4",
            "Hexalith.Parties.Contracts": "4.0.1",
            "Hexalith.Parties.EventStoreExtensions": "4.0.1",
            "Hexalith.McpCli.Abstractions": "3.0.0",
        }
        assets = {"libraries": {name + "/" + version: {"type": "package"} for name, version in packages.items()}}
        receipt = verify_collecting_cohort(assets, cohort)
        self.assertFalse(receipt["runtimeAcceptance"])
        self.assertFalse(receipt["candidateAcceptance"])
        for fault in ("missing", "project", "memories-old", "neighbor", "duplicate", "extra-sdk"):
            changed = json.loads(json.dumps(assets))
            name = "Hexalith.Memories.EventStoreExtensions/4.1.4"
            if fault == "missing":
                del changed["libraries"][name]
            elif fault == "project":
                changed["libraries"][name]["type"] = "project"
            elif fault == "extra-sdk":
                changed["libraries"]["Hexalith.EventStore.Contracts/5.1.0"] = {"type": "package"}
            else:
                changed["libraries"]["Hexalith.Memories.EventStoreExtensions/" + ("3.0.27" if fault == "memories-old" else "4.1.5")] = {"type": "package"}
                if fault != "duplicate":
                    del changed["libraries"][name]
            with self.subTest(fault=fault), self.assertRaisesRegex(ValueError, "exact public cohort|Every restored EventStore"):
                verify_collecting_cohort(changed, cohort)

    def test_required_totals_match_unified_suite_names(self):
        self.assertEqual(REQUIRED_TOTALS, {"current": 430, "base": 28, "extensions": 27})
        for retired in ("tuple-ci-sdk42-current-suites.json", "tuple-ci-sdk42-base-suites.json"):
            self.assertFalse((Path(__file__).parent / retired).exists(), retired)

    def test_extension_suite_preserves_sdk_controls_and_adds_finite_fixture_controls(self):
        profile = json.loads((Path(__file__).parent / "tuple-ci-extension-suites.json").read_text())
        self.assertEqual(validate_profile(profile), 27)
        self.assertEqual([group["requiredCount"] for group in profile["groups"]], [15, 12])

    def test_base_suite_requires_lock_controls_and_current_entry_cases_without_duplicates(self):
        base_profile = json.loads((Path(__file__).parent / "tuple-ci-base-suites.json").read_text())
        current_profile = json.loads((Path(__file__).parent / "tuple-ci-current-suites.json").read_text())
        self.assertEqual(validate_profile(base_profile), 28)
        self.assertEqual([group["requiredCount"] for group in base_profile["groups"]], [26, 2])
        base_selectors = [selector for group in base_profile["groups"] for selector in group["selectors"]]
        current_selectors = [selector for group in current_profile["groups"] for selector in group["selectors"]]
        self.assertEqual(base_selectors, [
            "Hexalith.Platform.Composition.Tests.PlatformOwnerPackageLockTests",
            "Hexalith.Platform.Composition.Tests.PlatformCurrentPackageEntryTests",
        ])
        self.assertTrue(set(current_selectors).isdisjoint(base_selectors))

    def test_current_inventory_remains_separately_mandatory(self):
        profile = json.loads((Path(__file__).parent / "tuple-ci-current-suites.json").read_text())
        self.assertEqual(validate_profile(profile), 430)
        store_groups = [group for group in profile["groups"] if "Hexalith.Platform.AttemptStore.Tests" in group["project"]]
        self.assertEqual(len(store_groups), 1)
        self.assertEqual(store_groups[0]["requiredCount"], 41)
        self.assertEqual(store_groups[0]["selectors"], [
            "Hexalith.Platform.AttemptStore.Tests.AttemptStoreTests",
            "Hexalith.Platform.AttemptStore.Tests.PromotionDecisionTests",
            "Hexalith.Platform.AttemptStore.Tests.S3AttemptObjectStoreTests",
        ])
        binding_test = "Hexalith.Platform.Composition.Tests.PlatformAppModelTests.PackageModeRefusesChangedOwnerProvenanceBeforeAddingResources"
        binding_groups = [group for group in profile["groups"] if binding_test in group["selectors"]]
        self.assertEqual(len(binding_groups), 1)
        self.assertEqual(binding_groups[0]["filter"], "-method")
        self.assertEqual(binding_groups[0]["requiredCount"], 7)

    def test_actual_test_dependency_identity_rejects_missing_source_or_other_sdk(self):
        with tempfile.TemporaryDirectory() as root:
            assembly = Path(root) / "Tests.dll"
            path = assembly.with_suffix(".deps.json")
            dependencies = {name.removesuffix(".dll") + "/5.6.0": {"type": "package"}
                            for name in COMPOSITION_TEST_DEPENDENCIES if name.startswith("Hexalith.EventStore.")}
            path.write_text(json.dumps({"libraries": dependencies}))
            verify_test_sdk(assembly, "5.6.0")
            for fault in ["missing", "project", "other-sdk"]:
                changed = json.loads(json.dumps(dependencies))
                first = next(iter(changed))
                if fault == "missing":
                    del changed[first]
                elif fault == "project":
                    changed[first]["type"] = "project"
                else:
                    changed[first.replace("5.6.0", "5.5.0")] = changed.pop(first)
                path.write_text(json.dumps({"libraries": changed}))
                with self.subTest(fault=fault), self.assertRaises(ValueError):
                    verify_test_sdk(assembly, "5.6.0")

    def test_current_collector_rejects_missing_source_or_crossed_memories_packages(self):
        with tempfile.TemporaryDirectory() as root:
            assembly = Path(root) / "Tests.dll"
            dependencies = {name.removesuffix(".dll") + "/5.6.0": {"type": "package"}
                for name in COMPOSITION_TEST_DEPENDENCIES if name.startswith("Hexalith.EventStore.")}
            memories = {"Hexalith.Memories." + name + "/4.1.4": {"type": "package"}
                for name in ("Contracts", "EventStore", "EventStoreExtensions")}
            dependencies.update(memories)
            assembly.with_suffix(".deps.json").write_text(json.dumps({"libraries": dependencies}))
            verify_test_sdk(assembly, "5.6.0", "4.1.4")
            for name in memories:
                for fault in ("missing", "project", "4.0.0", "3.0.27"):
                    changed = json.loads(json.dumps(dependencies))
                    if fault == "missing":
                        del changed[name]
                    elif fault == "project":
                        changed[name]["type"] = "project"
                    else:
                        changed[name.replace("4.1.4", fault)] = changed.pop(name)
                    assembly.with_suffix(".deps.json").write_text(json.dumps({"libraries": changed}))
                    with self.subTest(package=name, fault=fault), self.assertRaisesRegex(ValueError, "collection companion"):
                        verify_test_sdk(assembly, "5.6.0", "4.1.4")


if __name__ == "__main__":
    unittest.main()
