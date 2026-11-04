"""Synthetic file/context controls only; never start CI, restore packages or claim runtime qualification."""
import copy
import hashlib
from pathlib import Path
import tempfile
import unittest
from unittest import mock

from run_tuple_ci import snapshot_composition_test_outputs, verify_composition_test_outputs_unchanged


class CompositionTestOutputIdentityTests(unittest.TestCase):
    DEPENDENCIES = (
        "Hexalith.EventStore.Contracts.dll",
        "Hexalith.EventStore.ServiceDefaults.dll",
        "Hexalith.EventStore.Client.dll",
        "Hexalith.EventStore.DomainService.dll",
        "Hexalith.Tenants.Contracts.dll",
    )

    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="tuple-output-identity-synthetic-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.assembly = self.root / "Synthetic.Composition.Tests.dll"
        for name in (self.assembly.name, *self.DEPENDENCIES):
            (self.root / name).write_bytes(("synthetic-file:" + name).encode())

    def test_snapshot_binds_all_five_dependencies_and_the_actual_test_file(self):
        with mock.patch("run_tuple_ci.platform.system", return_value="SyntheticOS"), mock.patch("run_tuple_ci.platform.machine", return_value="SyntheticArchitecture"):
            snapshot = snapshot_composition_test_outputs(self.assembly)
        self.assertEqual(snapshot["os"], "SyntheticOS")
        self.assertEqual(snapshot["architecture"], "SyntheticArchitecture")
        self.assertEqual(set(snapshot["dependencies"]), set(self.DEPENDENCIES))
        for path, identity in [(self.assembly, snapshot["testAssembly"]), *((self.root / name, snapshot["dependencies"][name]) for name in self.DEPENDENCIES)]:
            self.assertEqual(identity["path"], str(path.resolve()))
            self.assertEqual(identity["sha256"], hashlib.sha256(path.read_bytes()).hexdigest())

    def test_stable_outputs_retain_equal_before_and_after_observations(self):
        record = {"before": snapshot_composition_test_outputs(self.assembly)}
        before = copy.deepcopy(record["before"])
        verify_composition_test_outputs_unchanged(record)
        self.assertTrue(record["unchanged"])
        self.assertEqual(record["before"], before)
        self.assertEqual(record["after"], before)
        self.assertIn("afterCapturedUtc", record)

    def test_each_dependency_and_test_assembly_mutation_refuses(self):
        for name in (self.assembly.name, *self.DEPENDENCIES):
            with self.subTest(file=name):
                path = self.root / name
                original = path.read_bytes()
                record = {"before": snapshot_composition_test_outputs(self.assembly)}
                before = copy.deepcopy(record["before"])
                try:
                    path.write_bytes(original + b":changed")
                    with self.assertRaisesRegex(ValueError, "changed during required tests"):
                        verify_composition_test_outputs_unchanged(record)
                    self.assertFalse(record["unchanged"])
                    self.assertEqual(record["before"], before)
                    self.assertNotEqual(record["after"], before)
                finally:
                    path.write_bytes(original)

    def test_missing_required_file_refuses_the_initial_snapshot(self):
        for name in (self.assembly.name, *self.DEPENDENCIES):
            with self.subTest(file=name):
                path = self.root / name
                original = path.read_bytes()
                try:
                    path.unlink()
                    with self.assertRaises(FileNotFoundError):
                        snapshot_composition_test_outputs(self.assembly)
                finally:
                    path.write_bytes(original)

    def test_missing_final_file_retains_failure_instead_of_a_stale_snapshot(self):
        record = {"before": snapshot_composition_test_outputs(self.assembly)}
        verify_composition_test_outputs_unchanged(record)
        (self.root / self.DEPENDENCIES[0]).unlink()
        with self.assertRaises(FileNotFoundError):
            verify_composition_test_outputs_unchanged(record)
        self.assertFalse(record["unchanged"])
        self.assertIsNone(record["after"])
        self.assertIn(self.DEPENDENCIES[0], record["afterError"])

    def test_changed_os_or_architecture_refuses_without_changing_files(self):
        for function, field in (("system", "os"), ("machine", "architecture")):
            with self.subTest(field=field):
                record = {"before": snapshot_composition_test_outputs(self.assembly)}
                with mock.patch("run_tuple_ci.platform." + function, return_value=record["before"][field] + "-changed"):
                    with self.assertRaisesRegex(ValueError, "changed during required tests"):
                        verify_composition_test_outputs_unchanged(record)
                self.assertFalse(record["unchanged"])
                self.assertEqual(record["before"]["dependencies"], record["after"]["dependencies"])
                self.assertEqual(record["before"]["testAssembly"], record["after"]["testAssembly"])


if __name__ == "__main__":
    unittest.main()
