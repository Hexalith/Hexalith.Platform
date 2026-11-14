"""Restore/build the finite SDK4.2 collection graph; never qualify a production or runtime tuple."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET

from builds_tuple import load_binding
from run_tuple_ci import sdk_suite, sdk42_collecting_cohort, verify_collecting_cohort
from current_catalog_view import catalog_profile, require_catalog_body
from test_current_catalog_view import builds6_catalog_body, generation6_binding

OPTIONS = None
PACKAGES = (
    "Hexalith.EventStore.Contracts", "Hexalith.EventStore.Client", "Hexalith.EventStore.Server",
    "Hexalith.EventStore.ServiceDefaults", "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server",
    "Hexalith.Memories.Contracts", "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions",
    "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions", "Hexalith.McpCli.Abstractions",
)


class CollectionGraphTests(unittest.TestCase):
    def test_real_catalog_unscoped_refusal_and_exact_scoped_locked_graph_build(self):
        with tempfile.TemporaryDirectory(prefix="platform-sdk42-collection-") as temporary:
            root = Path(temporary)
            project = root / "Hexalith.Platform.Composition.Tests.csproj"
            document = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
            group = ET.SubElement(document, "PropertyGroup")
            for key, value in {"TargetFramework": "net11.0", "ManagePackageVersionsCentrally": "true",
                    "CentralPackageTransitivePinningEnabled": "true", "RestoreEnablePackagePruning": "false",
                    "RestorePackagesWithLockFile": "true", "GenerateDependencyFile": "true",
                    "TreatWarningsAsErrors": "true"}.items():
                ET.SubElement(group, key).text = value
            group = ET.SubElement(document, "ItemGroup")
            for name in PACKAGES:
                ET.SubElement(group, "PackageReference", Include=name)
            project.write_bytes(ET.tostring(document))
            (root / "CohortProbe.cs").write_text("/// <summary>Compiles the selected package collection graph.</summary>\npublic sealed class CohortProbe {}\n")
            (root / "NuGet.Config").write_text('<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>')
            environment = {key: value for key, value in os.environ.items() if not key.lower().startswith(("hexalith", "custom", "aftermicrosoft"))
                and key.lower() not in {"configuration", "gh_token", "directorybuildpropspath", "directorybuildtargetspath", "directorypackagespropspath"}}
            environment.update(NUGET_PACKAGES=str(root / "packages"), NUGET_HTTP_CACHE_PATH=str(root / "http"))
            configuration = "Release" if os.environ.get("CI", "").lower() == "true" else "Debug"
            base = ["-p:Configuration=" + configuration, "-p:UseHexalithProjectReferences=false"]

            def run(name, arguments):
                command = ["dotnet", *arguments]
                result = subprocess.run(command, cwd=root, env=environment, capture_output=True, text=True, timeout=180)
                (OPTIONS.evidence / (name + ".stdout")).write_text(result.stdout)
                (OPTIONS.evidence / (name + ".stderr")).write_text(result.stderr)
                (OPTIONS.evidence / (name + ".command.json")).write_text(json.dumps(command, indent=2) + "\n")
                return result

            catalog = root / "Directory.Packages.props"
            catalog.write_bytes(builds6_catalog_body())
            result = run("unscoped-builds6-restore", ["restore", str(project), "--force-evaluate", *base,
                "-p:HexalithEventStoreVersion=4.2.0"])
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("NU1109", result.stdout + result.stderr)
            self.assertIn("Hexalith.Memories.EventStoreExtensions 4.0.0", result.stdout + result.stderr)

            raw = OPTIONS.catalog.read_bytes() if OPTIONS.catalog else builds6_catalog_body()
            binding = load_binding(OPTIONS.producer_binding) if OPTIONS.producer_binding else generation6_binding()
            profile = catalog_profile(binding)
            require_catalog_body(raw, binding["producer"]["catalog"]["sha256"])
            if OPTIONS.catalog:
                require_catalog_body(raw, OPTIONS.catalog_sha256)
            cohort = sdk42_collecting_cohort(binding, "current42")
            self.assertIsNotNone(cohort, "The collection regression requires an exact authenticated current producer profile.")
            catalog.write_bytes(raw)
            properties = [*sdk_suite("current42", "4.2.0")[1],
                *("-p:" + name + "=" + value for name, value in cohort.items())]
            result = run("scoped-restore", ["restore", str(project), "--force-evaluate", *base, *properties])
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            lock = (root / "packages.lock.json").read_bytes()
            result = run("scoped-locked-restore", ["restore", str(project), "--locked-mode", *base, *properties])
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertEqual((root / "packages.lock.json").read_bytes(), lock)
            result = run("scoped-build", ["build", str(project), "--no-restore", "--configuration", configuration, "-m:1", *base, *properties])
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            assets = json.loads((root / "obj/project.assets.json").read_bytes())
            self.assertEqual(set(assets["project"]["frameworks"]), {"net11.0"})
            self.assertEqual(set(assets["project"]["restore"]["sources"]), {"https://api.nuget.org/v3/index.json"})
            self.assertEqual(assets["project"]["restore"]["restoreAuditProperties"]["enableAudit"], "true")
            self.assertEqual(assets["project"]["restore"]["restoreAuditProperties"]["auditMode"], "all")
            dependencies = json.loads((root / ("bin/" + configuration + "/net11.0/Hexalith.Platform.Composition.Tests.deps.json")).read_bytes())["libraries"]
            expected = {name: "4.2.0" if name.startswith("Hexalith.EventStore.") else
                "3.0.27" if name.startswith("Hexalith.Memories.") else "7.0.7" if name.startswith("Hexalith.Tenants.") else
                "3.1.1" if name.startswith("Hexalith.Parties.") else "2.0.1" for name in (*PACKAGES, "Hexalith.EventStore.DomainService")}
            for name, version in expected.items():
                self.assertEqual(assets["libraries"][name + "/" + version]["type"], "package")
                self.assertEqual(dependencies[name + "/" + version]["type"], "package")
            verify_collecting_cohort(assets, cohort)
            self.assertEqual(catalog.read_bytes(), raw)
            (OPTIONS.evidence / "collector.packages.lock.json").write_bytes(lock)
            (OPTIONS.evidence / "selected-graph.json").write_text(json.dumps({"catalogSha256": hashlib.sha256(raw).hexdigest(),
                "producerSource": binding["producer"]["sourceRevision"], "producerVersion": binding["publication"]["version"],
                "catalogProfile": profile, "collectingProperties": cohort, "framework": "net11.0", "configuration": configuration, "packages": expected, "scope": "Package collection regression only",
                "runtimeAcceptance": False, "candidateAcceptance": False}, indent=2) + "\n")


def main():
    global OPTIONS
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path)
    parser.add_argument("--catalog-sha256")
    parser.add_argument("--producer-binding", type=Path)
    parser.add_argument("--evidence", type=Path, required=True)
    OPTIONS = parser.parse_args()
    if bool(OPTIONS.catalog) != bool(OPTIONS.producer_binding) or bool(OPTIONS.catalog) != bool(OPTIONS.catalog_sha256):
        parser.error("Supply the catalog, exact SHA256 and producer binding together.")
    OPTIONS.evidence.mkdir(parents=True, exist_ok=False)
    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(CollectionGraphTests))
    return 0 if result.wasSuccessful() and result.testsRun == 1 and not result.skipped else 1


if __name__ == "__main__":
    raise SystemExit(main())
