"""Evaluate the real MSBuild source SDK contract without compiling or granting runtime authority."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET

from current_catalog_view import PLATFORM_CONSUMERS

ROOT = Path(__file__).resolve().parents[1]
OPTIONS = None


class SourceSdkPropagationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory(prefix="platform-source-sdk-metadata-")
        cls.addClassCleanup(cls.temporary.cleanup)
        cls.fixture = Path(cls.temporary.name) / "Hexalith.Platform.Composition.csproj"
        if cls.fixture.stem not in PLATFORM_CONSUMERS or not (ROOT / "src" / cls.fixture.stem / cls.fixture.name).is_file():
            raise AssertionError("Metadata parents must use the exact existing Composition consumer context.")
        cls.explicit = "ControlContext=keep;hexalithEventStoreVersion=4.1.0"
        project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
        group = ET.SubElement(project, "ItemGroup")
        for name in ["DomainService", "Contracts"]:
            item = ET.SubElement(group, "ProjectReference", Include=str(
                ROOT / "references/Hexalith.EventStore/src" / ("Hexalith.EventStore." + name)
                / ("Hexalith.EventStore." + name + ".csproj")))
            if name == "Contracts":
                ET.SubElement(item, "AdditionalProperties").text = cls.explicit
        cls.fixture.write_bytes(ET.tostring(project, encoding="utf-8"))
        cls.evaluations = []
        cls.default_sdk = cls.invoke(cls.fixture, ["-p:UseHexalithProjectReferences=true"])["Properties"]["HexalithEventStoreVersion"]
        from builds_tuple import load_binding
        from current_catalog_view import catalog_profile
        if cls.default_sdk not in ("4.1.0", "4.2.0") and not (
                cls.default_sdk == "5.1.0" and catalog_profile(load_binding()) == "current-sdk51"):
            raise AssertionError("The focused controls require one of the recorded current/historical catalog SDKs.")

    @classmethod
    def invoke(cls, project, properties, target=None, configuration="Release"):
        args = ["dotnet", "msbuild", str(project), "-nologo", "-p:Configuration=" + configuration,
                "-p:DirectoryBuildPropsPath=" + str(ROOT / "Directory.Build.props"),
                "-p:DirectoryBuildTargetsPath=" + str(ROOT / "Directory.Build.targets"),
                "-p:DirectoryPackagesPropsPath=" + str(ROOT / "Directory.Packages.props"),
                *properties, "-getProperty:HexalithEventStoreVersion,RestoreUseStaticGraphEvaluation", "-getItem:ProjectReference"]
        if OPTIONS.artifacts:
            args.append("-p:ArtifactsPath=" + str(OPTIONS.artifacts.resolve()))
        if OPTIONS.published_tool_root:
            args.append("-p:HexalithBuildsPublishedToolRoot=" + str(OPTIONS.published_tool_root.resolve()))
        if target:
            args.append("-target:" + target)
        result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, timeout=120)
        index = len(cls.evaluations)
        prefix = OPTIONS.evidence / ("evaluation-" + str(index))
        prefix.with_suffix(".stdout").write_text(result.stdout)
        prefix.with_suffix(".stderr").write_text(result.stderr)
        cls.evaluations.append({"command": args, "exitCode": result.returncode})
        (OPTIONS.evidence / "commands.json").write_text(json.dumps(cls.evaluations, indent=2) + "\n")
        if result.returncode:
            raise AssertionError("Actual MSBuild metadata evaluation failed: " + result.stdout + result.stderr)
        return json.loads(result.stdout)

    def selection(self, properties):
        document = self.invoke(self.fixture, properties)
        entries = {item["Filename"]: item for item in document["Items"]["ProjectReference"]}
        return document, entries["Hexalith.EventStore.DomainService"].get("AdditionalProperties", "")

    def test_default_source_uses_the_recorded_catalog_selection(self):
        document, metadata = self.selection(["-p:UseHexalithProjectReferences=true"])
        self.assertEqual(self.default_sdk, document["Properties"]["HexalithEventStoreVersion"])
        self.assertEqual(";HexalithEventStoreVersion=" + self.default_sdk + ";UseHexalithProjectReferences=true", metadata)

    def test_explicit_historical_global_selection_is_preserved(self):
        document, metadata = self.selection(["-p:UseHexalithProjectReferences=true", "-p:HexalithEventStoreVersion=4.0.4"])
        self.assertEqual("4.0.4", document["Properties"]["HexalithEventStoreVersion"])
        self.assertEqual(";HexalithEventStoreVersion=4.0.4;UseHexalithProjectReferences=true", metadata)

    def test_explicit_case_insensitive_edge_and_unrelated_metadata_are_preserved(self):
        document, _ = self.selection(["-p:UseHexalithProjectReferences=true"])
        contracts = next(item for item in document["Items"]["ProjectReference"] if item["Filename"] == "Hexalith.EventStore.Contracts")
        self.assertEqual(self.explicit + ";UseHexalithProjectReferences=true", contracts["AdditionalProperties"])

    def test_package_mode_does_not_add_source_edge_metadata(self):
        document, metadata = self.selection(["-p:UseHexalithProjectReferences=false"])
        self.assertEqual("", metadata)
        self.assertNotEqual("true", document["Properties"]["RestoreUseStaticGraphEvaluation"])

    def excluded(self, signal):
        document, metadata = self.selection(["-p:UseHexalithProjectReferences=true", signal])
        self.assertEqual("", metadata)
        self.assertNotEqual("true", document["Properties"]["RestoreUseStaticGraphEvaluation"])

    def test_source_mapping_mode_keeps_its_version_authority(self):
        self.excluded("-p:HexalithSourceMappingMode=source")

    def test_source_mapping_hash_keeps_its_version_authority(self):
        self.excluded("-p:HexalithSourceMappingHash=" + "a" * 64)

    def test_parent_mapping_hash_keeps_its_version_authority(self):
        self.excluded("-p:HEXALITH_SOURCE_MAPPING_HASH=" + "b" * 64)

    def test_parent_mapping_path_keeps_its_version_authority(self):
        # A metadata-only exclusion signal; this is never accepted as an authenticated mapping.
        self.excluded("-p:HEXALITH_SOURCE_MAPPING_PATH=" + str(Path(self.temporary.name) / "not-an-authenticated-mapping.json"))

    def test_actual_sdk_promoted_references_receive_the_same_selected_sdk(self):
        project = ROOT / "tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj"
        properties = ["-p:UseHexalithProjectReferences=true"]
        before = self.invoke(project, properties)["Items"]["ProjectReference"]
        after = self.invoke(project, properties, "IncludeTransitiveProjectReferences")["Items"]["ProjectReference"]
        previous = {item["FullPath"] for item in before}
        promoted = [item for item in after if item["FullPath"] not in previous]
        self.assertTrue(promoted, "The actual restored SDK graph must promote at least one reference.")
        for item in promoted:
            values = re.findall(r"(?:^|;)\s*HexalithEventStoreVersion\s*=([^;]+)", item.get("AdditionalProperties", ""), re.IGNORECASE)
            self.assertEqual([self.default_sdk], values)
            modes = re.findall(r"(?:^|;)\s*UseHexalithProjectReferences\s*=([^;]+)", item.get("AdditionalProperties", ""), re.IGNORECASE)
            self.assertEqual(["true"], modes)


    def child_selection(self, assignments="", global_properties=None):
        directory = Path(self.temporary.name) / ("child-selection-" + str(len(self.evaluations)))
        directory.mkdir()
        leaf = directory / "observe-child.targets"
        leaf.write_text("<Project><Target Name='ObserveSourceModeLeaf' Returns='@(_ObservedSourceMode)'><ItemGroup><_ObservedSourceMode Include='$(MSBuildProjectName)'><Mode>$(UseHexalithProjectReferences)</Mode><Sdk>$(HexalithEventStoreVersion)</Sdk><EventStoreFromSource>$(HexalithEventStoreFromSource)</EventStoreFromSource></_ObservedSourceMode></ItemGroup></Target></Project>")
        project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
        group = ET.SubElement(project, "ItemGroup")
        reference = ET.SubElement(group, "ProjectReference", Include=str(ROOT / "references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj"))
        if assignments:
            ET.SubElement(reference, "AdditionalProperties").text = assignments
        target = ET.SubElement(project, "Target", Name="ObserveChildSourceMode", Returns="@(_ObservedChildSourceMode)")
        task = ET.SubElement(target, "MSBuild", Projects="@(ProjectReference)", Targets="ObserveSourceModeLeaf", BuildInParallel="false",
                             Properties="%(ProjectReference.AdditionalProperties)",
                             RemoveProperties="DirectoryBuildPropsPath;DirectoryBuildTargetsPath;DirectoryPackagesPropsPath")
        ET.SubElement(task, "Output", TaskParameter="TargetOutputs", ItemName="_ObservedChildSourceMode")
        fixture = directory / "Hexalith.Platform.Composition.csproj"
        fixture.write_bytes(ET.tostring(project, encoding="utf-8"))
        document = self.invoke(fixture, ["-p:CustomAfterMicrosoftCommonTargets=" + str(leaf),
                              "-getTargetResult:ObserveChildSourceMode", *(global_properties or [])],
                              "ObserveChildSourceMode", configuration="Debug")
        items = document["TargetResults"]["ObserveChildSourceMode"]["Items"]
        self.assertEqual(1, len(items))
        self.assertEqual("Hexalith.Tenants.Contracts", items[0]["Identity"])
        return items[0]

    def test_default_debug_source_mode_reaches_actual_owner_without_global_override(self):
        child = self.child_selection()
        self.assertEqual("true", child["Mode"])
        self.assertEqual(self.default_sdk, child["Sdk"])
        self.assertEqual("true", child["EventStoreFromSource"])

    def test_explicit_global_source_mode_reaches_actual_owner(self):
        child = self.child_selection(global_properties=["-p:UseHexalithProjectReferences=true"])
        self.assertEqual("true", child["Mode"])
        self.assertEqual(self.default_sdk, child["Sdk"])
        self.assertEqual("true", child["EventStoreFromSource"])

    def test_explicit_global_package_mode_keeps_actual_owner_in_package_mode(self):
        child = self.child_selection(global_properties=["-p:UseHexalithProjectReferences=false"])
        self.assertEqual("false", child["Mode"])
        self.assertNotEqual("true", child.get("EventStoreFromSource", ""))

    def test_case_insensitive_explicit_package_edge_keeps_its_own_sdk(self):
        child = self.child_selection("ControlContext=keep;useHEXALITHProjectReferences=false;hexalithEventStoreVersion=4.1.0")
        self.assertEqual("false", child["Mode"])
        self.assertEqual("4.1.0", child["Sdk"])
        self.assertNotEqual("true", child.get("EventStoreFromSource", ""))

    def test_explicit_sdk_only_edge_still_inherits_default_debug_source_mode(self):
        child = self.child_selection("ControlContext=keep;hexalithEventStoreVersion=4.1.0")
        self.assertEqual("true", child["Mode"])
        self.assertEqual("4.1.0", child["Sdk"])
        self.assertEqual("true", child["EventStoreFromSource"])

    def test_source_mode_propagation_is_independent_of_an_empty_sdk_selection(self):
        child = self.child_selection(global_properties=["-p:HexalithEventStoreVersion="])
        self.assertEqual("true", child["Mode"])
        self.assertEqual("true", child["EventStoreFromSource"])

    def test_default_debug_sdk_promoted_edges_keep_source_mode_without_global_override(self):
        project = ROOT / "tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj"
        before = self.invoke(project, [], configuration="Debug")["Items"]["ProjectReference"]
        after = self.invoke(project, [], "IncludeTransitiveProjectReferences", configuration="Debug")["Items"]["ProjectReference"]
        previous = {item["FullPath"] for item in before}
        promoted = [item for item in after if item["FullPath"] not in previous]
        self.assertTrue(promoted)
        for item in promoted:
            values = re.findall(r"(?:^|;)\s*HexalithEventStoreVersion\s*=([^;]+)", item.get("AdditionalProperties", ""), re.IGNORECASE)
            self.assertEqual([self.default_sdk], values)
            modes = re.findall(r"(?:^|;)\s*UseHexalithProjectReferences\s*=([^;]+)", item.get("AdditionalProperties", ""), re.IGNORECASE)
            self.assertEqual(["true"], modes)


    def test_explicit_package_edge_overrides_inherited_global_source_mode(self):
        child = self.child_selection("ControlContext=keep;useHEXALITHProjectReferences=false;hexalithEventStoreVersion=4.1.0",
                                     ["-p:UseHexalithProjectReferences=true"])
        self.assertEqual("false", child["Mode"])
        self.assertEqual("4.1.0", child["Sdk"])
        self.assertNotEqual("true", child.get("EventStoreFromSource", ""))

    def restore_edge(self, assignments="", properties=None, solution=False):
        directory = Path(self.temporary.name) / ("restore-control-" + str(len(self.evaluations)))
        owner = directory / "owner"
        owner.mkdir(parents=True)
        (owner / "Directory.Build.props").write_text("<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework><UseHexalithProjectReferences Condition=\"'$(UseHexalithProjectReferences)' == ''\">false</UseHexalithProjectReferences><HexalithEventStoreVersion Condition=\"'$(HexalithEventStoreVersion)' == ''\">4.0.4</HexalithEventStoreVersion><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>")
        child = owner / "RestoreOwner.csproj"
        child.write_text("<Project Sdk='Microsoft.NET.Sdk'><ItemGroup><PackageReference Include='Hexalith.EventStore.DomainService' Version='$(HexalithEventStoreVersion)' /></ItemGroup><ItemGroup Condition=\"'$(UseHexalithProjectReferences)' == 'true'\"><ProjectReference Include='RestoreLeaf.csproj' /></ItemGroup></Project>")
        (owner / "RestoreLeaf.csproj").write_text("<Project Sdk='Microsoft.NET.Sdk' />")
        project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
        ET.SubElement(project, "Import", Project=str(ROOT / "Directory.Build.props"))
        ET.SubElement(project, "Import", Project=str(ROOT / "Directory.Packages.props"))
        group = ET.SubElement(project, "ItemGroup")
        reference = ET.SubElement(group, "ProjectReference", Include=str(child))
        if assignments:
            ET.SubElement(reference, "AdditionalProperties").text = assignments
        ET.SubElement(project, "Import", Project=str(ROOT / "Directory.Build.targets"))
        parent = directory / "Hexalith.Platform.Composition.csproj"
        parent.write_bytes(ET.tostring(project, encoding="utf-8"))
        entry = parent
        if solution:
            entry = directory / "RestoreControl.slnx"
            entry.write_text('<Solution><Project Path="' + parent.name + '" /></Solution>')
            solution_props = ET.Element("Project")
            ET.SubElement(solution_props, "Import", Project=str(ROOT / "Directory.Solution.props"))
            (directory / "Directory.Solution.props").write_bytes(ET.tostring(solution_props, encoding="utf-8"))
        artifacts = directory / "artifacts"
        args = ["dotnet", "restore", str(entry), "--artifacts-path", str(artifacts),
                "-p:Configuration=Debug", "-nodeReuse:false", *(properties or [])]
        completed = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, timeout=180)
        index = len(self.evaluations)
        prefix = OPTIONS.evidence / ("restore-" + str(index))
        prefix.with_suffix(".stdout").write_text(completed.stdout)
        prefix.with_suffix(".stderr").write_text(completed.stderr)
        self.evaluations.append({"command": args, "exitCode": completed.returncode})
        (OPTIONS.evidence / "commands.json").write_text(json.dumps(self.evaluations, indent=2) + "\n")
        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        assets = artifacts / "obj/RestoreOwner/project.assets.json"
        document = json.loads(assets.read_bytes())
        (OPTIONS.evidence / ("restore-" + str(index) + "-owner-assets.json")).write_text(json.dumps(document, indent=2) + "\n")
        leaf = artifacts / "obj/RestoreLeaf/project.assets.json"
        return document["libraries"], leaf.exists()

    def test_default_debug_restore_keeps_the_actual_source_and_sdk_graph(self):
        libraries, leaf = self.restore_edge()
        self.assertTrue(leaf)
        self.assertIn("Hexalith.EventStore.DomainService/" + self.default_sdk, libraries)

    def test_restore_preserves_explicit_package_edge_and_sdk_against_global_source(self):
        libraries, leaf = self.restore_edge("useHEXALITHProjectReferences=false;hexalithEventStoreVersion=4.1.0",
                                            ["-p:UseHexalithProjectReferences=true"])
        self.assertFalse(leaf)
        self.assertIn("Hexalith.EventStore.DomainService/4.1.0", libraries)

    def test_restore_preserves_explicit_sdk_only_and_inherits_default_source_mode(self):
        libraries, leaf = self.restore_edge("hexalithEventStoreVersion=4.1.0")
        self.assertTrue(leaf)
        self.assertIn("Hexalith.EventStore.DomainService/4.1.0", libraries)

    def test_explicit_legacy_restore_choice_is_retained_and_exposes_the_context_loss(self):
        libraries, leaf = self.restore_edge(properties=["-p:RestoreUseStaticGraphEvaluation=false"])
        self.assertFalse(leaf)
        self.assertIn("Hexalith.EventStore.DomainService/4.0.4", libraries)

    def test_package_restore_keeps_its_original_owner_defaults(self):
        libraries, leaf = self.restore_edge(properties=["-p:UseHexalithProjectReferences=false"])
        self.assertFalse(leaf)
        self.assertIn("Hexalith.EventStore.DomainService/4.0.4", libraries)


    def test_solution_restore_keeps_default_debug_edge_source_and_sdk_context(self):
        libraries, leaf = self.restore_edge(solution=True)
        self.assertTrue(leaf)
        self.assertIn("Hexalith.EventStore.DomainService/" + self.default_sdk, libraries)

    def test_solution_package_restore_keeps_its_original_owner_defaults(self):
        libraries, leaf = self.restore_edge(properties=["-p:UseHexalithProjectReferences=false"], solution=True)
        self.assertFalse(leaf)
        self.assertIn("Hexalith.EventStore.DomainService/4.0.4", libraries)

    def test_explicit_restore_engine_choice_is_preserved_for_source_and_package_modes(self):
        for mode in ["true", "false"]:
            for engine in ["true", "false"]:
                with self.subTest(mode=mode, engine=engine):
                    document = self.invoke(self.fixture, ["-p:UseHexalithProjectReferences=" + mode,
                                           "-p:RestoreUseStaticGraphEvaluation=" + engine])
                    self.assertEqual(engine, document["Properties"]["RestoreUseStaticGraphEvaluation"])


def main():
    global OPTIONS
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--artifacts", type=Path)
    parser.add_argument("--published-tool-root", type=Path)
    OPTIONS = parser.parse_args()
    if OPTIONS.evidence.exists():
        raise ValueError("Choose a fresh metadata-control evidence directory.")
    OPTIONS.evidence.mkdir(parents=True)
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(SourceSdkPropagationTests)
    if suite.countTestCases() != 25:
        raise ValueError("The source SDK metadata contract requires exactly twenty-five controls.")
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    passed = result.testsRun == 25 and not result.skipped and result.wasSuccessful()
    (OPTIONS.evidence / "result.json").write_text(json.dumps({"expected": 25, "executed": result.testsRun,
        "failures": len(result.failures), "errors": len(result.errors), "skipped": len(result.skipped),
        "passed": passed, "qualificationCredit": False,
        "scope": "Real metadata evaluation only; controlled mapping signals grant no authentication or runtime authority."}, indent=2) + "\n")
    return 0 if passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
