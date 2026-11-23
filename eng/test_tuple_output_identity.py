"""Synthetic file/context controls only; never start CI, restore packages or claim runtime qualification."""
import copy
import hashlib
from pathlib import Path
import tempfile
import unittest
from unittest import mock

from run_tuple_ci import (
    ATTEMPT_STORE_ARTIFACT_MEMBERS,
    builds_artifact_identities,
    snapshot_attempt_store_test_outputs,
    snapshot_composition_test_outputs,
    verify_attempt_store_test_outputs_unchanged,
    verify_composition_test_outputs_unchanged,
)


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


class AttemptStoreTestOutputIdentityTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="tuple-attempt-store-output-")
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        self.assembly = root / "Hexalith.Platform.AttemptStore.Tests.dll"
        self.library = root / "Hexalith.Platform.AttemptStore.dll"
        self.tooling_copy = root / "Hexalith.Builds.Tooling.dll"
        self.published = root / "published"
        self.published.mkdir()
        for output in (self.assembly, self.library, self.tooling_copy, self.published / self.tooling_copy.name):
            output.write_bytes(("synthetic-file:" + output.name).encode())

    def test_only_tooling_is_required_from_the_archive_and_a_mismatched_copy_refuses(self):
        lock = {"testLock": "synthetic archive"}
        source = "b" * 40
        prefix = "tools/net11.0/any/"
        member = prefix + self.tooling_copy.name
        expected = hashlib.sha256((self.published / self.tooling_copy.name).read_bytes()).hexdigest()
        receipt = {"toolLock": lock, "buildsRevision": source, "toolPrefix": prefix, "files": {member: expected}}
        collectors = {"attemptStore": {"toolRoot": str(self.published), "assembly": str(self.assembly)}}

        identities = builds_artifact_identities(receipt, lock, source, collectors, members=ATTEMPT_STORE_ARTIFACT_MEMBERS)
        self.assertEqual(set(identities["attemptStore"]), {self.tooling_copy.name})
        self.assertEqual(identities["attemptStore"][self.tooling_copy.name]["expectedSha256"], expected)

        self.tooling_copy.write_bytes(b"different Tooling copy")
        with self.assertRaisesRegex(ValueError, "authenticated published artifact: Hexalith.Builds.Tooling.dll"):
            builds_artifact_identities(receipt, lock, source, collectors, members=ATTEMPT_STORE_ARTIFACT_MEMBERS)

    def test_changes_to_attempt_store_outputs_refuse(self):
        for path in (self.assembly, self.library, self.tooling_copy):
            with self.subTest(file=path.name):
                record = {"before": snapshot_attempt_store_test_outputs(self.assembly)}
                original = path.read_bytes()
                try:
                    path.write_bytes(original + b":changed")
                    with self.assertRaisesRegex(ValueError, "AttemptStore test outputs.*changed during required tests"):
                        verify_attempt_store_test_outputs_unchanged(record)
                    self.assertFalse(record["unchanged"])
                finally:
                    path.write_bytes(original)


if __name__ == "__main__":
    unittest.main()
