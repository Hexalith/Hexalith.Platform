"""Real MSBuild and fault controls for the unified Builds catalog read from the recorded gitlink."""
import copy
import functools
import hashlib
import os
import subprocess
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

from builds_tuple import ROOT, recorded_gitlink, resolve_builds_root
from current_catalog_view import (CURRENT_OWNER_CONSUMERS, MCP_PACKAGES, MEMORIES_PACKAGES,
                                  PLATFORM_CONSUMERS, SDK_PACKAGES, TENANTS_PACKAGES, UNKNOWN_CONSUMERS,
                                  UNKNOWN_OWNER_CONSUMERS, catalog_cohort, evaluate_views, verify_unified_catalog)

# Historical published 6.0.0 catalog: a collection-graph negative control with its original subject.
BUILDS6_SOURCE = "1ebc5b84ff2c8afd21a87d80ef3d9c9048eaf5e5"
BUILDS6_CATALOG_BLOB = "cbfc6a888f269010600103a33c73d0cfc0af93a2"
BUILDS6_CATALOG_SHA256 = "45b05f6755f337baccfaa805e5e4afb4a57e91b2f70226a357c82d03b4c19bb7"


def catalog_fixture(name: str, source: str, expected_blob: str, expected_sha256: str) -> bytes:
    """Read exact committed catalog bytes offline, independently of producer checkout depth."""
    raw = (Path(__file__).with_name("fixtures") / name).read_bytes()
    blob = hashlib.sha1(b"blob " + str(len(raw)).encode() + b"\0" + raw).hexdigest()
    if blob != expected_blob or hashlib.sha256(raw).hexdigest() != expected_sha256:
        raise ValueError("Catalog regression fixture differs from its exact source " + source)
    return raw


def builds6_catalog_body() -> bytes:
    """Read the unchanged published 6.0.0 catalog control."""
    return catalog_fixture("builds-6.0.0-Directory.Packages.props", BUILDS6_SOURCE, BUILDS6_CATALOG_BLOB, BUILDS6_CATALOG_SHA256)


@functools.cache
def gitlink_catalog() -> bytes:
    """Read the current catalog from the recorded Builds gitlink blob; the pin files name no catalog digest."""
    gitlink = recorded_gitlink(ROOT)
    return subprocess.check_output(["git", "cat-file", "blob", gitlink + ":Props/Directory.Packages.props"],
                                   cwd=resolve_builds_root(ROOT), timeout=30)


def unconditioned(raw: bytes, name: str) -> str:
    """Read a catalog property's unconditioned default independently of MSBuild."""
    values = [item.text for item in ET.fromstring(raw).iter()
              if item.tag.rsplit("}", 1)[-1] == name and item.get("Condition", "").replace(" ", "") in ("", "'$(" + name + ")'==''")]
    return values[-1]


def unified_catalog_body(*, sdk="5.6.0", memories="4.1.4", tenants="8.1.1", parties="4.0.1", mcp="3.0.0") -> bytes:
    """Synthesize a minimal unified Builds catalog for offline fault controls."""
    project = ET.Element("Project")
    properties = ET.SubElement(project, "PropertyGroup")
    for name, value in (("HexalithEventStoreVersion", sdk), ("HexalithMemoriesVersion", memories),
                        ("HexalithTenantsVersion", tenants), ("HexalithPartiesVersion", parties),
                        ("HexalithMcpCliVersion", mcp)):
        ET.SubElement(properties, name).text = value
    packages = ET.SubElement(project, "ItemGroup")
    for names, version in ((SDK_PACKAGES, "HexalithEventStoreVersion"), (MEMORIES_PACKAGES, "HexalithMemoriesVersion"),
                           (TENANTS_PACKAGES, "HexalithTenantsVersion"), (MCP_PACKAGES, "HexalithMcpCliVersion")):
        for name in names:
            ET.SubElement(packages, "PackageVersion", Include=name, Version="$(" + version + ")")
    return ET.tostring(project, encoding="utf-8")


def synthetic_observations(cohort: dict[str, str]) -> list[dict]:
    names = (*PLATFORM_CONSUMERS, *UNKNOWN_CONSUMERS, *CURRENT_OWNER_CONSUMERS, *UNKNOWN_OWNER_CONSUMERS)
    sdk = cohort["HexalithEventStoreVersion"]
    memories = cohort["HexalithMemoriesVersion"]
    tenants = cohort["HexalithTenantsVersion"]
    parties = cohort["HexalithPartiesVersion"]
    mcp = cohort["HexalithMcpCliVersion"]
    return [{
        "Identity": name,
        "Sdk": sdk,
        "CatalogMemoriesDefault": memories,
        "CatalogTenantsDefault": tenants,
        "CatalogPartiesDefault": parties,
        "CatalogMcpDefault": mcp,
        "Pins": "|".join(f"{pkg}={sdk}" for pkg in SDK_PACKAGES),
        "MemoriesPins": "|".join(f"{pkg}={memories}" for pkg in MEMORIES_PACKAGES),
        "TenantsPins": "|".join(f"{pkg}={tenants}" for pkg in TENANTS_PACKAGES),
        "McpPins": "|".join(f"{pkg}={mcp}" for pkg in MCP_PACKAGES),
    } for name in names]


class UnifiedGitlinkCatalogTests(unittest.TestCase):
    def test_all_consumers_use_the_current_release(self):
        raw = gitlink_catalog()
        receipt = verify_unified_catalog(raw, hashlib.sha256(raw).hexdigest())
        self.assertEqual(receipt["kind"], "unified-package-catalog")
        self.assertEqual(receipt["catalogSha256"], hashlib.sha256(raw).hexdigest())
        self.assertFalse(receipt["runtimeAcceptance"])
        for owner in ("EventStore", "Memories", "Tenants", "Parties", "McpCli"):
            key = "Hexalith" + owner + "Version"
            self.assertEqual(receipt["properties"][key], unconditioned(raw, key))

    def test_divergent_package_is_rejected_even_with_a_valid_catalog_digest(self):
        catalog = ET.fromstring(gitlink_catalog())
        catalog.find(".//PackageVersion[@Include='Hexalith.EventStore.Contracts']").set("Version", "0.0.1")
        altered = ET.tostring(catalog)
        with self.assertRaisesRegex(ValueError, "EventStore package family"):
            verify_unified_catalog(altered, hashlib.sha256(altered).hexdigest())


class UnifiedCatalogFaultTests(unittest.TestCase):
    def test_catalog_cohort_reads_exact_stable_owner_versions_and_refuses_faults(self):
        raw = unified_catalog_body()
        self.assertEqual(catalog_cohort(raw), {
            "HexalithEventStoreVersion": "5.6.0",
            "HexalithMemoriesVersion": "4.1.4",
            "HexalithTenantsVersion": "8.1.1",
            "HexalithPartiesVersion": "4.0.1",
            "HexalithMcpCliVersion": "3.0.0",
        })
        for owner in ("EventStore", "Memories", "Tenants", "Parties", "McpCli"):
            key = "Hexalith" + owner + "Version"
            for fault in ("missing", "duplicate", "prerelease", "empty", "two-part"):
                document = ET.fromstring(raw)
                group = document.find("PropertyGroup")
                node = group.find(key)
                if fault == "missing":
                    group.remove(node)
                elif fault == "duplicate":
                    ET.SubElement(group, key).text = node.text
                elif fault == "prerelease":
                    node.text = "5.6.0-preview.1"
                elif fault == "empty":
                    node.text = ""
                else:
                    node.text = "5.6"
                with self.subTest(key=key, fault=fault), self.assertRaisesRegex(ValueError, key):
                    catalog_cohort(ET.tostring(document, encoding="utf-8"))

    def test_changed_or_unbound_catalog_body_refuses_before_msbuild(self):
        raw = unified_catalog_body()
        for digest, body in (("0" * 64, raw), (hashlib.sha256(raw).hexdigest(), raw + b"\n"), (None, raw), ("not-hex", raw)):
            with self.subTest(digest=digest), patch("current_catalog_view.evaluate_views") as evaluate:
                with self.assertRaisesRegex(ValueError, "authenticated catalog Git body"):
                    verify_unified_catalog(body, digest)
                evaluate.assert_not_called()

    def test_verify_unified_catalog_rejects_consumer_set_or_family_drift(self):
        raw = unified_catalog_body()
        digest = hashlib.sha256(raw).hexdigest()
        cohort = catalog_cohort(raw)
        baseline = synthetic_observations(cohort)
        with patch("current_catalog_view.evaluate_views", return_value=copy.deepcopy(baseline)):
            receipt = verify_unified_catalog(raw, digest)
            self.assertEqual(receipt["properties"], cohort)

        for fault, pattern in (
            ("missing-consumer", "Missing or duplicate consumer"),
            ("duplicate-consumer", "Missing or duplicate consumer"),
            ("eventstore-pin", "EventStore package family"),
            ("memories-prop", "Memories package family"),
            ("tenants-missing-pin", "Tenants package family"),
            ("mcp-pin", "McpCli package family"),
            ("parties-prop", "Parties release"),
        ):
            mutated = copy.deepcopy(baseline)
            if fault == "missing-consumer":
                mutated.pop()
            elif fault == "duplicate-consumer":
                mutated.append(copy.deepcopy(mutated[0]))
            elif fault == "eventstore-pin":
                mutated[0]["Pins"] = mutated[0]["Pins"].replace("Hexalith.EventStore.Contracts=5.6.0", "Hexalith.EventStore.Contracts=5.5.0")
            elif fault == "memories-prop":
                mutated[-1]["CatalogMemoriesDefault"] = "4.0.0"
            elif fault == "tenants-missing-pin":
                mutated[0]["TenantsPins"] = "|".join(mutated[0]["TenantsPins"].split("|")[:-1])
            elif fault == "mcp-pin":
                mutated[0]["McpPins"] = "Hexalith.McpCli.Abstractions=2.0.1"
            else:
                mutated[0]["CatalogPartiesDefault"] = "3.1.1"
            with self.subTest(fault=fault), patch("current_catalog_view.evaluate_views", return_value=mutated), \
                    self.assertRaisesRegex(ValueError, pattern):
                verify_unified_catalog(raw, digest)

    def test_verify_unified_catalog_evaluates_real_msbuild_and_rejects_per_consumer_override(self):
        raw = unified_catalog_body()
        receipt = verify_unified_catalog(raw, hashlib.sha256(raw).hexdigest())
        self.assertEqual(receipt["kind"], "unified-package-catalog")
        self.assertEqual(receipt["properties"]["HexalithEventStoreVersion"], "5.6.0")
        self.assertFalse(receipt["runtimeAcceptance"])

        document = ET.fromstring(raw)
        override = ET.SubElement(document, "ItemGroup", Condition="$(MSBuildProjectName.Equals('Hexalith.Platform.Composition'))")
        ET.SubElement(override, "PackageVersion", Update="Hexalith.EventStore.Contracts", Version="4.2.0")
        mutated = ET.tostring(document, encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Hexalith.Platform.Composition selects a different EventStore package family"):
            verify_unified_catalog(mutated, hashlib.sha256(mutated).hexdigest())

    def test_evaluate_views_isolates_ambient_overrides_and_supports_single_consumer_probe(self):
        raw = unified_catalog_body(sdk="5.6.0")
        with tempfile.TemporaryDirectory() as directory:
            poison = Path(directory) / "poison.props"
            poison.write_text('<Project><PropertyGroup><HexalithEventStoreVersion>99.0.0</HexalithEventStoreVersion></PropertyGroup></Project>')
            with patch.dict(os.environ, {"HexalithEventStoreVersion": "99.0.0", "UseHexalithProjectReferences": "true",
                                         "CustomAfterMicrosoftCommonProps": str(poison), "DirectoryPackagesPropsPath": str(poison)}):
                observed = evaluate_views(raw, consumer_names=("Unqualified.Consumer",))
        self.assertEqual(len(observed), 1)
        self.assertEqual(observed[0]["Identity"], "Unqualified.Consumer")
        self.assertEqual(observed[0]["Sdk"], "5.6.0")

    def test_exact_consumer_inventory_does_not_implicitly_admit_new_platform_projects(self):
        names = {path.stem for directory in ("src", "tests") for path in (ROOT / directory).rglob("*.csproj")}
        # The attempt store uses the generic catalog and has no EventStore SDK dependency.
        generic_projects = {"Hexalith.Platform.AttemptStore", "Hexalith.Platform.AttemptStore.Tests"}
        self.assertEqual(len(PLATFORM_CONSUMERS), 33)
        self.assertTrue(generic_projects.isdisjoint(PLATFORM_CONSUMERS))
        self.assertEqual(names, set(PLATFORM_CONSUMERS) | generic_projects)


if __name__ == "__main__":
    unittest.main()
