"""Evaluate real historical catalogs with the bounded current Platform security patches."""
from __future__ import annotations

import collections
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
BUILDS = ROOT / "references/Hexalith.Builds"
HISTORICAL = [
    "fc2122240a39f03628c04fa8433e5eece90b8b4c",
    "27145e52ffab5623198ba5efdab5cdb71aa42d28",
    "5ec1779040606be89306129a7d2b245258c9acd2",
]
CURRENT = "163f3d0deac6b0db5ec4bdfcc0934b12e6adb46a"
PATCHES = {"Aspire.Hosting": ("13.6.4", "13.6.5"),
           "Microsoft.AspNetCore.SignalR.StackExchangeRedis": ("10.0.14", "10.0.15"),
           "Microsoft.Extensions.Options": ("10.0.14", "10.0.15"),
           "Microsoft.Extensions.DependencyInjection.Abstractions": ("10.0.14", "10.0.15")}


class HistoricalSecurityPackagesTests(unittest.TestCase):
    def catalog(self, revision):
        return subprocess.check_output(["git", "-c", "core.commitGraph=false", "show",
                                        revision + ":Props/Directory.Packages.props"], cwd=BUILDS)

    def evaluate_pair(self, raw):
        with tempfile.TemporaryDirectory(prefix="platform-historical-security-") as directory:
            root = Path(directory)
            catalog = root / "selected/Props/Directory.Packages.props"
            catalog.parent.mkdir(parents=True)
            catalog.write_bytes(raw)
            outputs = []
            for name, imported in [("baseline", catalog), ("platform", ROOT / "Directory.Packages.props")]:
                project = root / name / "Hexalith.Platform.Composition.Tests.csproj"
                project.parent.mkdir()
                project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                                   '<TargetFramework>net10.0</TargetFramework>'
                                   '<UseHexalithProjectReferences>false</UseHexalithProjectReferences>'
                                   '</PropertyGroup><Import Project="' + escape(str(imported), {'"': '&quot;'}) + '" /></Project>')
                completed = subprocess.run(["dotnet", "msbuild", str(project), "-nologo",
                                            "-p:HexalithBuildsRoot=" + str(catalog.parents[1]),
                                            "-getItem:PackageVersion",
                                            "-getProperty:NuGetAudit,NuGetAuditMode,NuGetAuditLevel"],
                                           capture_output=True, text=True, check=True, timeout=30)
                selected = json.loads(completed.stdout)
                self.assertEqual(selected["Properties"], {"NuGetAudit": "true", "NuGetAuditMode": "all", "NuGetAuditLevel": "low"})
                outputs.append(collections.Counter((item["Identity"], item["Version"])
                                                   for item in selected["Items"]["PackageVersion"]))
            self.assertEqual(catalog.read_bytes(), raw, "Evaluation must never rewrite the selected historical catalog.")
            return outputs

    def test_each_frozen_catalog_changes_only_the_exact_security_dependency_closure(self):
        for revision in HISTORICAL:
            with self.subTest(revision=revision):
                previous, patched = self.evaluate_pair(self.catalog(revision))
                expected = previous.copy()
                for name, (old, new) in PATCHES.items():
                    self.assertEqual(previous[name, old], 1)
                    del expected[name, old]
                    expected[name, new] += 1
                self.assertEqual(patched, expected, "Every owner version and every unrelated package must stay exact.")

    def test_current_catalog_is_unchanged(self):
        previous, selected = self.evaluate_pair(self.catalog(CURRENT))
        self.assertEqual(selected, previous)

    def test_newer_security_packages_are_not_downgraded(self):
        raw = self.catalog(CURRENT).replace(b'Include="Aspire.Hosting" Version="13.6.5"', b'Include="Aspire.Hosting" Version="13.6.6"')
        raw = raw.replace(b'Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" Version="10.0.15"',
                          b'Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" Version="10.0.16"')
        previous, selected = self.evaluate_pair(raw)
        self.assertEqual(previous["Aspire.Hosting", "13.6.6"], 1)
        self.assertEqual(previous["Microsoft.AspNetCore.SignalR.StackExchangeRedis", "10.0.16"], 1)
        self.assertEqual(selected, previous)

    def test_other_packages_with_the_same_old_version_do_not_change(self):
        raw = self.catalog(HISTORICAL[0]).replace(b'</Project>',
            b'<ItemGroup><PackageVersion Include="Historical.Unrelated.Aspire" Version="13.6.4" />'
            b'<PackageVersion Include="Historical.Unrelated.AspNet" Version="10.0.14" /></ItemGroup></Project>')
        previous, selected = self.evaluate_pair(raw)
        for name, version in [("Historical.Unrelated.Aspire", "13.6.4"), ("Historical.Unrelated.AspNet", "10.0.14")]:
            self.assertEqual(selected[name, version], previous[name, version])
            self.assertEqual(selected[name, version], 1)


if __name__ == "__main__":
    unittest.main()
