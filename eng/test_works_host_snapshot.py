"""Exports bind full origin revisions, overlays, ordinary inputs and dependency targets."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import works_host_snapshot as snapshot


EMBEDDED_COMPOSITION_INPUTS = (
    "eng/ext-secrets-1/public-joint-plan.json",
    "docs/contracts/schemas/hexalith.realm-contract.v1.json",
    "infrastructure/keycloak-local-realm/platform-requester-audience.jar",
    "infrastructure/keycloak-local-realm/platform-workload-binding.jar",
)


class WorksHostSnapshotTests(unittest.TestCase):
    def setUp(self):
        roots = patch.dict(os.environ, {module.replace(".", "") + "Root": "" for module in snapshot.SOURCE_DEPENDENCIES})
        roots.start()
        self.addCleanup(roots.stop)
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
        self.builds = catalog.parents[1]
        self.tool = self.builds / "src/libraries/Hexalith.Builds.Tooling/Tool.cs"
        self.tool.parent.mkdir(parents=True)
        self.tool.write_text("tracked tooling")
        for args in (("init", "-q"), ("config", "user.email", "fixture@example.test"),
                     ("config", "user.name", "Fixture"), ("add", "."), ("commit", "-qm", "initial tooling")):
            snapshot.git(self.builds, *args)
        self.tool.write_text("compiled working overlay")
        self.commit = snapshot.revision(self.origin)
        (self.origin / "src/runtime.cs").write_text("working overlay")
        (self.origin / "src/new.cs").write_text("new input")
        for name in [*snapshot.UNRELATED_CANONICAL_INPUTS, "config/works-production-profile.schema.json", "eng/used-fixture.json",
                     "eng/executed.py", "src/Used.csproj", *EMBEDDED_COMPOSITION_INPUTS]:
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
            "config/works-production-profile.schema.json", "eng/used-fixture.json", "eng/executed.py", "src/Used.csproj",
            *EMBEDDED_COMPOSITION_INPUTS})
        self.assertTrue((self.export / "references/Dependency").is_symlink())
        (self.export / "src/obj").mkdir()
        (self.export / "src/obj/generated.cs").write_text("generated")
        self.verify()

    def test_metadata_only_commit_is_recorded_without_changing_execution_inputs(self):
        (self.origin / "docs/note.md").write_text("unrelated documentation")
        self.call("add", "docs/note.md")
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

    def test_embedded_composition_inputs_cannot_drift(self):
        for root in (self.origin, self.export):
            for name in EMBEDDED_COMPOSITION_INPUTS:
                path = root / name
                original = path.read_bytes()
                for change in ("changed", "removed"):
                    with self.subTest(root=root.name, name=name, change=change):
                        if change == "changed": path.write_bytes(b"different embedded input")
                        else: path.unlink()
                        with self.assertRaisesRegex(ValueError, "canonical ordinary"): self.verify()
                        path.write_bytes(original)
        self.verify()

    def test_isolated_origin_also_binds_the_original_owner_working_overlay(self):
        upstream = self.root / 'upstream'
        subprocess.run(['git', 'clone', '--shared', str(self.origin), str(upstream)], check=True,
                       stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        for name in snapshot.origin_files(upstream).keys() - snapshot.origin_files(self.origin).keys():
            (upstream / name).unlink()
        for name in snapshot.origin_files(self.origin):
            target = upstream / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes((self.origin / name).read_bytes())
        export, manifest = self.root / 'upstream-export', self.root / 'upstream-binding.json'
        snapshot.create(self.origin, export, manifest, upstream_origin=upstream)
        observed = snapshot.verify(manifest, export)
        self.assertEqual(observed['upstreamOrigin']['root'], str(upstream))
        (upstream / 'src/runtime.cs').write_text('foreign owner overlay')
        with self.assertRaisesRegex(ValueError, 'upstream owner ordinary'):
            snapshot.verify(manifest, export)

    def test_a_different_upstream_overlay_cannot_be_selected(self):
        upstream = self.root / 'different-upstream'
        subprocess.run(['git', 'clone', '--shared', str(self.origin), str(upstream)], check=True,
                       stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        with self.assertRaisesRegex(ValueError, 'Upstream owner revision or ordinary overlay differs'):
            snapshot.create(self.origin, self.root / 'rejected-export', self.root / 'rejected-binding.json', upstream_origin=upstream)

    def test_generated_debugger_history_is_never_read_or_exported_as_source(self):
        (self.origin / ".netcoredbg_hist").write_text("private debugger history")
        original = Path.read_bytes
        def read(path):
            if path.name == ".netcoredbg_hist": raise AssertionError("Debugger contents must not enter source inventory")
            return original(path)
        with patch.object(Path, "read_bytes", read):
            self.assertNotIn(".netcoredbg_hist", snapshot.origin_files(self.origin))
            self.verify()

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

    def test_direct_and_exported_snapshots_bind_dependency_revision_and_working_source(self):
        from verify_works_host import canonical_source_snapshot

        direct = canonical_source_snapshot(self.origin)
        selected = direct["dependencyInputs"]["references/Hexalith.Builds"]
        self.assertEqual(selected["commit"], snapshot.revision(self.builds))
        self.assertEqual(selected["sourceFiles"][self.tool.relative_to(self.builds).as_posix()],
                         snapshot.digest(b"compiled working overlay"))
        self.assertEqual(direct["dependencyInputs"], self.verify()["dependencyInputs"])
        changes = ((self.tool, "changed"), (self.tool, None),
                   (self.builds / "Props/Hexalith.Build.props", "new import"),
                   (self.builds / "schemas/hexalith.ci.v1.json", "new embedded resource"))
        for path, value in changes:
            with self.subTest(path=path, value=value):
                previous = path.read_bytes() if path.exists() else None
                if value is None:
                    path.unlink()
                else:
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_text(value)
                with self.assertRaisesRegex(ValueError, "dependency catalog, source inputs or revision"):
                    self.verify()
                self.assertNotEqual(canonical_source_snapshot(self.origin), direct)
                if previous is None:
                    path.unlink()
                else:
                    path.write_bytes(previous)
        generated = self.tool.parent / "obj/Generated.cs"
        generated.parent.mkdir()
        generated.write_text("post-compile output")
        self.assertEqual(canonical_source_snapshot(self.origin), direct)
        self.verify()
        snapshot.git(self.builds, "commit", "--allow-empty", "-qm", "new revision with identical inputs")
        self.assertNotEqual(canonical_source_snapshot(self.origin), direct)
        with self.assertRaisesRegex(ValueError, "source inputs or revision"):
            self.verify()

    def test_dependency_build_input_link_is_refused(self):
        link = self.tool.parent / "Linked.cs"
        link.symlink_to(self.tool)
        with self.assertRaisesRegex(ValueError, "Dependency build input cannot be a symbolic link"):
            self.verify()

    def test_direct_platform_selects_the_sibling_catalog_and_works_prefers_its_populated_reference(self):
        from works_toolchain import builds_catalog

        sibling = self.root / "Hexalith.Builds"
        catalog = sibling / "Props/Directory.Packages.props"
        catalog.parent.mkdir(parents=True)
        catalog.write_text("sibling catalog")
        self.assertEqual(builds_catalog(self.origin), catalog)
        selected = snapshot.dependency_inputs(self.origin)
        self.assertEqual(selected["build/Hexalith.Builds"]["root"], str(sibling))
        self.assertEqual(selected["references/Hexalith.Builds"]["commit"], snapshot.revision(self.builds))
        self.verify()  # The export still authenticates its original dependency link.

        (self.origin / "Hexalith.Works.slnx").touch()
        self.assertNotIn("build/Hexalith.Builds", snapshot.dependency_inputs(self.origin))
        self.builds.rename(self.root / "saved-builds")
        self.builds.mkdir()
        (self.origin / "Hexalith.Works.slnx").unlink()
        self.assertEqual(set(snapshot.dependency_inputs(self.origin)), {"build/Hexalith.Builds"})
        with self.assertRaisesRegex(ValueError, "Missing selected dependency catalog"):
            self.verify()

    def test_configured_relative_source_is_bound_from_execution_root_including_overlay_drift(self):
        from verify_works_host import canonical_source_snapshot
        from works_toolchain import builds_catalog

        # Keep the override outside the snapshot's copied source and separate from its Git origin.
        execution = self.root / "isolated/platform"
        execution.parent.mkdir()
        manifest = self.root / "isolated.json"
        snapshot.create(self.origin, execution, manifest)
        alternate = execution.parent / "alternate-builds"
        catalog = alternate / "Props/Directory.Packages.props"
        catalog.parent.mkdir(parents=True)
        catalog.write_text("configured catalog")
        with patch.dict(os.environ, {"HexalithBuildsRoot": "../alternate-builds"}):
            self.assertEqual(builds_catalog(execution).resolve(), catalog)
            before = canonical_source_snapshot(self.origin, execution_root=execution)
            selection = before["dependencyInputs"]["build/Hexalith.Builds"]
            self.assertEqual(selection["root"], str(alternate))
            self.assertEqual(set(selection["sourceFiles"]), {"Props/Directory.Packages.props"})
            catalog.write_text("configured overlay changed")
            self.assertNotEqual(canonical_source_snapshot(self.origin, execution_root=execution), before)
            snapshot.verify(manifest, execution)
        self.assertNotIn("build/Hexalith.Builds", canonical_source_snapshot(
            self.origin, execution_root=execution)["dependencyInputs"])

    def test_declared_exported_dependency_tracks_source_without_attributing_an_ancestor_commit(self):
        declaration = "references/Hexalith.Tenants"
        with (self.origin / ".gitmodules").open("a") as file:
            file.write(f'[submodule "{declaration}"]\n path = {declaration}\n')
        tenant = self.origin / declaration
        tenant.mkdir()
        self.assertNotIn(declaration, snapshot.dependency_inputs(self.origin))
        source = tenant / "src/Hexalith.Tenants.Contracts/Query.cs"
        source.parent.mkdir(parents=True)
        source.write_text("exported contract")
        inputs = snapshot.dependency_inputs(self.origin)[declaration]
        self.assertIsNone(inputs["commit"])
        self.assertEqual(inputs["sourceFiles"], {"src/Hexalith.Tenants.Contracts/Query.cs":
                         snapshot.digest(b"exported contract")})
        source.unlink()
        self.assertNotIn(declaration, snapshot.dependency_inputs(self.origin))

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
