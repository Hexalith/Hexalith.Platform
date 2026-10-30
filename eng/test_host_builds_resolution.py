"""Exercise the real MSBuild dependency selection without restoring or starting services."""

import json
import pathlib
import shutil
import subprocess
import tempfile
import unittest
from xml.sax.saxutils import escape


ROOT = pathlib.Path(__file__).resolve().parents[1]
TOOLING = "src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj"


class HostBuildsResolutionTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.workspace = pathlib.Path(self.temporary.name)
        self.platform = self.workspace / "Hexalith.Platform"
        self.project = self.platform / "src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj"
        self.project.parent.mkdir(parents=True)
        for name in ("Directory.Build.props", "Directory.Packages.props", "Directory.Build.targets"):
            shutil.copyfile(ROOT / name, self.platform / name)
        shutil.copyfile(ROOT / self.project.relative_to(self.platform), self.project)
        self.test_project = self.platform / "tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj"
        self.test_project.parent.mkdir(parents=True)
        shutil.copyfile(ROOT / self.test_project.relative_to(self.platform), self.test_project)

    def builds(self, root, identity):
        (root / "Props").mkdir(parents=True)
        (root / "Props/Directory.Packages.props").write_text(
            f"<Project><PropertyGroup><ProbeCatalog>{identity}</ProbeCatalog>"
            "<HexalithVersionsLoaded>true</HexalithVersionsLoaded>"
            "</PropertyGroup></Project>")
        project = root / TOOLING
        project.parent.mkdir(parents=True)
        project.write_text('<Project Sdk="Microsoft.NET.Sdk" />')
        return root

    def evaluate(self, *properties, project=None):
        return subprocess.run(
            ["dotnet", "msbuild", str(project or self.project), "-nologo", "-p:Configuration=Release",
             "-getProperty:HexalithBuildsResolvedRoot,ProbeCatalog,CompatibilityMarker", "-getItem:ProjectReference,Reference,AssemblyAttribute,None", *properties],
            text=True, capture_output=True, timeout=30)

    def mapped_root_import(self, selected):
        path = self.workspace / "command-mapping.props"
        path.write_text(
            "<Project><PropertyGroup><HexalithBuildsRoot>"
            f"{escape(str(selected))}</HexalithBuildsRoot></PropertyGroup></Project>")
        return f"-p:CustomAfterMicrosoftCommonProps={path}"

    def assert_selection(self, selected, identity, *properties):
        result = self.evaluate(*properties)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        evaluated = json.loads(result.stdout)
        self.assertEqual(pathlib.Path(evaluated["Properties"]["HexalithBuildsResolvedRoot"]).resolve(), selected.resolve())
        self.assertEqual(evaluated["Properties"]["ProbeCatalog"], identity)
        self.assertEqual(
            [pathlib.Path(item["FullPath"]).resolve() for item in evaluated["Items"]["ProjectReference"] if item["Identity"].endswith("Hexalith.Builds.Tooling.csproj")],
            [(selected / TOOLING).resolve()])

    def test_workspace_sibling_is_used_without_nested_initialization(self):
        selected = self.builds(self.workspace / "Hexalith.Builds", "workspace")
        self.assert_selection(selected, "workspace")
        self.assertFalse((self.platform / "references/Hexalith.Builds").exists())

    def test_sibling_keeps_existing_catalog_precedence(self):
        selected = self.builds(self.workspace / "Hexalith.Builds", "workspace")
        self.builds(self.platform / "references/Hexalith.Builds", "nested")
        self.assert_selection(selected, "workspace")

    def test_standalone_initialized_dependency_is_supported(self):
        selected = self.builds(self.platform / "references/Hexalith.Builds", "standalone")
        self.assert_selection(selected, "standalone")

    def test_explicit_root_binds_both_catalog_and_tooling(self):
        self.builds(self.workspace / "Hexalith.Builds", "workspace")
        selected = self.builds(self.workspace / "selected-builds", "explicit")
        self.assert_selection(selected, "explicit", f"-p:HexalithBuildsRoot={selected}")

    def test_missing_dependency_refuses_instead_of_initializing(self):
        result = self.evaluate()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("MSB4019", result.stdout + result.stderr)
        self.assertFalse((self.platform / "references").exists())

    def test_relative_explicit_root_is_relative_to_platform_for_every_consumer(self):
        selected = self.builds(self.workspace / "Hexalith.Builds", "relative")
        self.assert_selection(selected, "relative", "-p:HexalithBuildsRoot=../Hexalith.Builds")

    def test_debug_keeps_eventstore_compatibility_import_after_selected_catalog(self):
        selected = self.builds(self.workspace / "Hexalith.Builds", "debug")
        eventstore = self.platform / "references/Hexalith.EventStore"
        eventstore.mkdir(parents=True)
        (eventstore / "Directory.Packages.props").write_text(
            '<Project><PropertyGroup><CompatibilityMarker>retained</CompatibilityMarker>'
            '<ProbeCatalog Condition="\'$(HexalithVersionsLoaded)\' != \'true\'">wrong</ProbeCatalog></PropertyGroup></Project>')
        self.assert_selection(selected, "debug", "-p:Configuration=Debug")
        result = self.evaluate("-p:Configuration=Debug")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(json.loads(result.stdout)["Properties"]["CompatibilityMarker"], "retained")

    def test_test_project_seed_reference_uses_sibling_and_relative_override(self):
        selected = self.builds(self.workspace / "Hexalith.Builds", "seed")
        for properties in ([], ["-p:HexalithBuildsRoot=../Hexalith.Builds"]):
            with self.subTest(properties=properties):
                result = self.evaluate(*properties, project=self.test_project)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                evaluated = json.loads(result.stdout)
                self.assertEqual(evaluated["Properties"]["ProbeCatalog"], "seed")
                references = [item for item in evaluated["Items"]["ProjectReference"] if item.get("Aliases") == "buildsseed"]
                self.assertEqual(len(references), 1)
                seed = selected / "src/hosts/Hexalith.Builds.Module.EventStoreHost/Hexalith.Builds.Module.EventStoreHost.csproj"
                self.assertEqual(pathlib.Path(references[0]["FullPath"]).resolve(), seed.resolve())

    def test_missing_explicit_root_does_not_fall_back(self):
        self.builds(self.workspace / "Hexalith.Builds", "workspace")
        result = self.evaluate(f"-p:HexalithBuildsRoot={self.workspace / 'missing'}")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("MSB4019", result.stdout + result.stderr)

    def test_late_mapping_binds_catalog_tooling_and_seed_to_selected_root(self):
        self.builds(self.workspace / "Hexalith.Builds", "workspace")
        selected = self.builds(self.workspace / "mapped-builds", "mapped")
        for root in (selected, "../mapped-builds"):
            with self.subTest(root=root):
                mapped = self.mapped_root_import(root)
                self.assert_selection(selected, "mapped", mapped)
                result = self.evaluate(mapped, project=self.test_project)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                evaluated = json.loads(result.stdout)
                self.assertEqual(evaluated["Properties"]["ProbeCatalog"], "mapped")
                references = [item for item in evaluated["Items"]["ProjectReference"] if item.get("Aliases") == "buildsseed"]
                self.assertEqual(len(references), 1)
                seed = selected / "src/hosts/Hexalith.Builds.Module.EventStoreHost/Hexalith.Builds.Module.EventStoreHost.csproj"
                self.assertEqual(pathlib.Path(references[0]["FullPath"]).resolve(), seed.resolve())

    def test_missing_late_mapped_root_does_not_fall_back(self):
        self.builds(self.workspace / "Hexalith.Builds", "workspace")
        self.builds(self.platform / "references/Hexalith.Builds", "nested")
        missing = self.workspace / "missing-mapped-builds"
        for root in (missing, "../missing-mapped-builds"):
            mapped = self.mapped_root_import(root)
            for project in (self.project, self.test_project):
                with self.subTest(root=root, project=project):
                    result = self.evaluate(mapped, project=project)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertIn("MSB4019", result.stdout + result.stderr)
                    self.assertIn(str(missing), result.stdout + result.stderr)

    def test_published_tool_root_removes_foreign_builds_projects_and_keeps_host_metadata(self):
        self.builds(self.workspace / "Hexalith.Builds", "published")
        published = self.workspace / "published tool"
        result = self.evaluate(f"-p:HexalithBuildsPublishedToolRoot={published}", project=self.test_project)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        items = json.loads(result.stdout)["Items"]
        self.assertFalse(any("Hexalith.Builds.Module.EventStoreHost.csproj" in x["Identity"] for x in items["ProjectReference"]))
        host = next(x for x in items["Reference"] if x.get("Aliases") == "buildsseed")
        self.assertEqual(pathlib.Path(host["HintPath"]), published / "g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll")
        self.assertTrue(any(x.get("CopyToOutputDirectory") == "PreserveNewest" and x.get("Link", "").endswith(".deps.json") for x in items["None"]))
        result = self.evaluate(f"-p:HexalithBuildsPublishedToolRoot={published}")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertFalse(any("Hexalith.Builds.Tooling.csproj" in x["Identity"] for x in json.loads(result.stdout)["Items"]["ProjectReference"]))

    def test_ci_rejects_foreign_source_fallback_before_build(self):
        self.builds(self.workspace / "Hexalith.Builds", "ci")
        result = self.evaluate("-p:CI=true", "-target:ValidatePlatformCiReferences")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Platform CI refuses foreign project references", result.stdout + result.stderr)

    def test_runner_assembly_override_does_not_add_a_source_reference(self):
        self.builds(self.workspace / "Hexalith.Builds", "workspace")
        result = self.evaluate(f"-p:BuildsToolingAssemblyPath={self.workspace / 'runner/Hexalith.Builds.Tooling.dll'}")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertFalse(any(item["Identity"].endswith("Hexalith.Builds.Tooling.csproj") for item in json.loads(result.stdout)["Items"]["ProjectReference"]))


if __name__ == "__main__":
    unittest.main()
