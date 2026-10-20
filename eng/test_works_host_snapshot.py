"""Exports bind full origin revisions, overlays, ordinary inputs and dependency targets."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

import works_host_snapshot as snapshot


class WorksHostSnapshotTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.origin = self.root / "origin"
        self.origin.mkdir()
        self.call("init", "-q")
        self.call("config", "user.email", "fixture@example.test")
        self.call("config", "user.name", "Fixture")
        (self.origin / "src").mkdir()
        (self.origin / "src/runtime.cs").write_text("baseline")
        (self.origin / ".gitmodules").write_text('[submodule "references/Dependency"]\n path = references/Dependency\n'
            '[submodule "references/Hexalith.Builds"]\n path = references/Hexalith.Builds\n')
        (self.origin / "references/Dependency").mkdir(parents=True)
        catalog = self.origin / "references/Hexalith.Builds/Props/Directory.Packages.props"
        catalog.parent.mkdir(parents=True)
        catalog.write_text("selected producer catalog")
        self.call("add", "src", ".gitmodules")
        self.call("commit", "-qm", "baseline")
        self.commit = snapshot.revision(self.origin)
        (self.origin / "src/runtime.cs").write_text("working overlay")
        (self.origin / "src/new.cs").write_text("new input")
        for name in [*snapshot.UNRELATED_CANONICAL_INPUTS, "config/works-production-profile.schema.json", "eng/used-fixture.json",
                     "eng/executed.py", "src/Used.csproj"]:
            path = self.origin / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("bound input")
        self.export = self.root / "export"
        self.manifest = self.root / "binding.json"
        self.record = snapshot.create(self.origin, self.export, self.manifest)

    def call(self, *args):
        return subprocess.check_output(["git", "-C", str(self.origin), *args], stderr=subprocess.STDOUT)

    def verify(self):
        return snapshot.verify(self.manifest, self.export)

    def test_exact_revision_overlays_links_and_build_output_exclusion(self):
        observed = self.verify()
        self.assertEqual(observed["originCommit"], self.commit)
        self.assertEqual(set(self.record["workingOverlays"]), {"src/runtime.cs", "src/new.cs", *snapshot.UNRELATED_CANONICAL_INPUTS,
            "config/works-production-profile.schema.json", "eng/used-fixture.json", "eng/executed.py", "src/Used.csproj"})
        self.assertTrue((self.export / "references/Dependency").is_symlink())
        (self.export / "src/obj").mkdir()
        (self.export / "src/obj/generated.cs").write_text("generated")
        self.verify()

    def test_metadata_only_commit_is_recorded_without_changing_execution_inputs(self):
        (self.origin / "docs").mkdir()
        (self.origin / "docs/note.md").write_text("unrelated documentation")
        self.call("add", "docs")
        self.call("commit", "-qm", "documentation")
        observed = self.verify()
        self.assertNotEqual(observed["observedOriginCommit"], self.commit)
        self.assertEqual(observed["originCommit"], self.commit)

    def test_changed_missing_added_and_linked_export_inputs_refuse(self):
        for change in ("change", "missing", "added", "link"):
            with self.subTest(change=change):
                path = self.export / "src/runtime.cs"
                if change == "change": path.write_text("changed")
                elif change == "missing": path.unlink()
                elif change == "added": (self.export / "src/added.cs").write_text("extra")
                else:
                    path.unlink()
                    path.symlink_to(self.origin / "src/runtime.cs")
                with self.assertRaises(ValueError): self.verify()
                if path.exists() or path.is_symlink(): path.unlink()
                path.write_text("working overlay")
                (self.export / "src/added.cs").unlink(missing_ok=True)

    def test_current_origin_drift_refuses(self):
        (self.origin / "src/new.cs").write_text("changed ordinary fixture")
        with self.assertRaisesRegex(ValueError, "canonical ordinary"): self.verify()

    def test_only_demonstrated_unrelated_canonical_inputs_may_drift_and_are_recorded(self):
        for name in snapshot.UNRELATED_CANONICAL_INPUTS:
            (self.origin / name).write_text("unrelated worker change")
        for prefix in snapshot.UNRELATED_CANONICAL_PREFIXES:
            path = self.origin / prefix / "new-unexecuted-input"
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("unrelated worker addition")
        observed = self.verify()
        self.assertEqual(set(observed["unrelatedOriginInputDrift"]), snapshot.UNRELATED_CANONICAL_INPUTS |
            {prefix + "new-unexecuted-input" for prefix in snapshot.UNRELATED_CANONICAL_PREFIXES})
        # The exported copies remain immutable even for unrelated inputs.
        (self.export / next(iter(snapshot.UNRELATED_CANONICAL_INPUTS))).write_text("export mutation")
        with self.assertRaises(ValueError): self.verify()

    def test_executed_helper_used_schema_and_new_unlisted_inputs_still_refuse(self):
        for name in ["eng/used-fixture.json", "config/works-production-profile.schema.json", "eng/new-unlisted.py"]:
            with self.subTest(name=name):
                path = self.origin / name
                previous = path.read_bytes() if path.exists() else None
                path.write_text("relevant or unaudited change")
                with self.assertRaisesRegex(ValueError, "canonical ordinary"): self.verify()
                if previous is None: path.unlink()
                else: path.write_bytes(previous)

    def test_new_import_or_compile_reference_into_an_excluded_boundary_refuses(self):
        references = {"eng/executed.py": 'import runpy; runpy.run_path("eng/ext-secrets-1/local-transport/run.py")',
            "src/Used.csproj": '<Project><ItemGroup><Compile Include="../tests/Hexalith.Platform.Secrets.Runtime.Tests/*.cs" /></ItemGroup></Project>'}
        for name, text in references.items():
            with self.subTest(name=name):
                path = self.origin / name
                previous = path.read_bytes()
                path.write_text(text)
                with self.assertRaisesRegex(ValueError, "canonical ordinary"): self.verify()
                path.write_bytes(previous)

    def test_selected_root_dependency_catalog_bytes_remain_bound(self):
        (self.origin / "references/Hexalith.Builds/Props/Directory.Packages.props").write_text("different producer")
        with self.assertRaisesRegex(ValueError, "dependency catalog"): self.verify()

    def test_dependency_substitution_or_extra_links_refuse(self):
        dependency = self.export / "references/Dependency"
        dependency.unlink()
        dependency.symlink_to(self.root)
        with self.assertRaisesRegex(ValueError, "dependency links"): self.verify()

    def test_revision_inventory_or_overlay_manifest_tampering_refuses(self):
        for key, value in (("originCommit", self.commit[:7]), ("inventorySha256", "0" * 64), ("workingOverlays", {})):
            with self.subTest(key=key):
                changed = dict(self.record)
                changed[key] = value
                self.manifest.write_text(json.dumps(changed))
                with self.assertRaises(ValueError): self.verify()
        self.manifest.write_text(json.dumps(self.record))
        with self.assertRaisesRegex(ValueError, "different execution root"):
            snapshot.verify(self.manifest, self.origin)


if __name__ == "__main__":
    unittest.main()
