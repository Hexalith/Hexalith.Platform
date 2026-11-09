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

ROOT = Path(__file__).resolve().parents[1]
OPTIONS = None


class SourceSdkPropagationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory(prefix="platform-source-sdk-metadata-")
        cls.addClassCleanup(cls.temporary.cleanup)
        cls.fixture = Path(cls.temporary.name) / "Selection.csproj"
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
        if cls.default_sdk not in ("4.1.0", "4.2.0"):
            raise AssertionError("The focused controls require one of the recorded current/historical catalog SDKs.")

    @classmethod
    def invoke(cls, project, properties, target=None):
        args = ["dotnet", "msbuild", str(project), "-nologo", "-p:Configuration=Release",
                "-p:DirectoryBuildPropsPath=" + str(ROOT / "Directory.Build.props"),
                "-p:DirectoryBuildTargetsPath=" + str(ROOT / "Directory.Build.targets"),
                "-p:DirectoryPackagesPropsPath=" + str(ROOT / "Directory.Packages.props"),
                *properties, "-getProperty:HexalithEventStoreVersion", "-getItem:ProjectReference"]
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
        self.assertEqual(self.explicit, entries["Hexalith.EventStore.Contracts"].get("AdditionalProperties"))
        return document, entries["Hexalith.EventStore.DomainService"].get("AdditionalProperties", "")

    def test_default_source_uses_the_recorded_catalog_selection(self):
        document, metadata = self.selection(["-p:UseHexalithProjectReferences=true"])
        self.assertEqual(self.default_sdk, document["Properties"]["HexalithEventStoreVersion"])
        self.assertEqual(";HexalithEventStoreVersion=" + self.default_sdk, metadata)

    def test_explicit_historical_global_selection_is_preserved(self):
        document, metadata = self.selection(["-p:UseHexalithProjectReferences=true", "-p:HexalithEventStoreVersion=4.0.4"])
        self.assertEqual("4.0.4", document["Properties"]["HexalithEventStoreVersion"])
        self.assertEqual(";HexalithEventStoreVersion=4.0.4", metadata)

    def test_explicit_case_insensitive_edge_and_unrelated_metadata_are_preserved(self):
        self.selection(["-p:UseHexalithProjectReferences=true"])

    def test_package_mode_does_not_add_source_edge_metadata(self):
        _, metadata = self.selection(["-p:UseHexalithProjectReferences=false"])
        self.assertEqual("", metadata)

    def excluded(self, signal):
        _, metadata = self.selection(["-p:UseHexalithProjectReferences=true", signal])
        self.assertEqual("", metadata)

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
    if suite.countTestCases() != 9:
        raise ValueError("The source SDK metadata contract requires exactly nine controls.")
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    passed = result.testsRun == 9 and not result.skipped and result.wasSuccessful()
    (OPTIONS.evidence / "result.json").write_text(json.dumps({"expected": 9, "executed": result.testsRun,
        "failures": len(result.failures), "errors": len(result.errors), "skipped": len(result.skipped),
        "passed": passed, "qualificationCredit": False,
        "scope": "Real metadata evaluation only; controlled mapping signals grant no authentication or runtime authority."}, indent=2) + "\n")
    return 0 if passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
