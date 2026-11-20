"""Real MSBuild controls for the finite named catalog view, read from the recorded Builds gitlink."""
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
from current_catalog_view import (CURRENT_OWNER_CONSUMERS, UNKNOWN_OWNER_CONSUMERS, MCP_PACKAGES, MEMORIES_PACKAGES, OPENAPI_PACKAGES,
                                  PLATFORM_CONSUMERS, SDK_PACKAGES, TENANTS_PACKAGES, UNKNOWN_CONSUMERS, evaluate_views,
                                  generic_defaults, verify_current_owner_observations, verify_memories_observations,
                                  verify_named_platform_catalog, verify_observations)

# Historical published 6.0.0 catalog: a collection-graph negative control with its original subject.
BUILDS6_SOURCE = "1ebc5b84ff2c8afd21a87d80ef3d9c9048eaf5e5"
BUILDS6_CATALOG_BLOB = "cbfc6a888f269010600103a33c73d0cfc0af93a2"
BUILDS6_CATALOG_SHA256 = "45b05f6755f337baccfaa805e5e4afb4a57e91b2f70226a357c82d03b4c19bb7"
CONTEXTS = ("defaultEvaluations", "collectingEvaluations", "currentOwnerEvaluations")


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


@functools.cache
def historical_catalog():
    return catalog_fixture("builds-before-unified-catalog.props", "dc225a6969473c21c3dad6bda8f1a18ad0aaae0d", "eca862048fa5a5348f3bc2c23dc66df9cec5c1a9", "bd616e0d9e88168104d8a6b847612bff7ff7ec595b8e014d9886122be0bfbb25")

@functools.cache
def gitlink_receipt() -> dict:
    raw = historical_catalog()
    return verify_named_platform_catalog(raw, hashlib.sha256(raw).hexdigest())


def unconditioned(raw: bytes, name: str) -> str:
    """Read a catalog property's unconditioned default independently of MSBuild."""
    values = [item.text for item in ET.fromstring(raw).iter()
              if item.tag.rsplit("}", 1)[-1] == name and item.get("Condition", "").replace(" ", "") in ("", "'$(" + name + ")'==''")]
    return values[-1]


def pins(item: dict, key: str) -> dict:
    return dict(pin.split("=", 1) for pin in item[key].split("|") if pin)


def catalog_body(*, sdk="5.0.0", aspnet="11.0.0", openapi="3.11.0", memories="4.0.0", tenants="8.0.0",
                 parties="3.1.1", mcp="3.0.0") -> bytes:
    """Synthesize the finite named Platform and owner views with explicit generic defaults."""
    project = ET.Element("Project")
    properties = ET.SubElement(project, "PropertyGroup")
    for selector, names in (("HexalithPlatformSdk42CatalogView", PLATFORM_CONSUMERS), ("HexalithPartiesSdk42CatalogView", CURRENT_OWNER_CONSUMERS)):
        ET.SubElement(properties, selector).text = "false"
        ET.SubElement(properties, selector, Condition=" or ".join("$(MSBuildProjectName.Equals('" + name + "'))" for name in names)).text = "true"
    named = "('$(HexalithPlatformSdk42CatalogView)' == 'true' or '$(HexalithPartiesSdk42CatalogView)' == 'true')"
    ET.SubElement(properties, "HexalithEventStoreVersion", Condition="'$(HexalithEventStoreVersion)' == '' and " + named).text = "4.2.0"
    ET.SubElement(properties, "HexalithEventStoreVersion", Condition="'$(HexalithEventStoreVersion)' == ''").text = sdk
    selected = named + " and '$(HexalithEventStoreVersion)' == '4.2.0'"
    for name, default, compatible in (("AspNetApi", aspnet, "10.0.15"), ("OpenApi", openapi, "2.13.0"),
                                      ("HexalithMemoriesVersion", memories, "3.0.27"), ("HexalithTenantsVersion", tenants, "7.0.7"),
                                      ("HexalithMcpCliVersion", mcp, "2.0.1"), ("HexalithPartiesVersion", parties, "3.1.1")):
        ET.SubElement(properties, name).text = default
        ET.SubElement(properties, name, Condition=selected).text = compatible
    packages = ET.SubElement(project, "ItemGroup")
    for names, version in ((SDK_PACKAGES, "HexalithEventStoreVersion"), (MEMORIES_PACKAGES, "HexalithMemoriesVersion"),
                           (TENANTS_PACKAGES, "HexalithTenantsVersion"), (MCP_PACKAGES, "HexalithMcpCliVersion")):
        for name in names:
            ET.SubElement(packages, "PackageVersion", Include=name, Version="$(" + version + ")")
    for name, selector in zip(OPENAPI_PACKAGES, ("AspNetApi", "OpenApi")):
        ET.SubElement(packages, "PackageVersion", Include=name, Version="$(" + selector + ")")
    return ET.tostring(project, encoding="utf-8")


class UnifiedGitlinkCatalogTests(unittest.TestCase):
    def test_all_consumers_use_the_current_release(self):
        from current_catalog_view import verify_unified_catalog
        raw = gitlink_catalog()
        receipt = verify_unified_catalog(raw, hashlib.sha256(raw).hexdigest())
        self.assertEqual(receipt["properties"]["HexalithEventStoreVersion"], unconditioned(raw, "HexalithEventStoreVersion"))

    def test_divergent_package_is_rejected_even_with_a_valid_catalog_digest(self):
        from current_catalog_view import verify_unified_catalog
        catalog = ET.fromstring(gitlink_catalog())
        catalog.find(".//PackageVersion[@Include='Hexalith.EventStore.Contracts']").set("Version", "0.0.1")
        altered = ET.tostring(catalog)
        with self.assertRaisesRegex(ValueError, "EventStore package family"):
            verify_unified_catalog(altered, hashlib.sha256(altered).hexdigest())


class HistoricalCatalogViewTests(unittest.TestCase):
    """Release archive for the named catalog views that preceded the unified catalog."""

    def verify(self, raw):
        return verify_named_platform_catalog(raw, hashlib.sha256(raw).hexdigest())

    def test_named_views_keep_owner_versions_and_generic_defaults_come_from_the_catalog(self):
        raw, receipt = historical_catalog(), gitlink_receipt()
        generic = receipt["genericDefaults"]
        self.assertEqual(generic["sdk"], unconditioned(raw, "HexalithEventStoreVersion"))
        self.assertNotEqual(generic["sdk"], "4.2.0")
        for context, count in zip(CONTEXTS, (36, 36, 7)):
            self.assertEqual(len(receipt[context]), count)
            for item in receipt[context]:
                named = item["Identity"] in (*PLATFORM_CONSUMERS, *CURRENT_OWNER_CONSUMERS)
                with self.subTest(context=context, project=item["Identity"]):
                    self.assertEqual(item["Sdk"], "4.2.0" if named or context == "collectingEvaluations" else generic["sdk"])
                    self.assertEqual([pins(item, "Pins")[name] for name in OPENAPI_PACKAGES],
                                     ["10.0.15", "2.13.0"] if named else generic["openapi"])
                    self.assertEqual(item["CatalogTenantsDefault"], "7.0.7" if named else generic["tenants"])
                    self.assertEqual(item["CatalogMemoriesDefault"], "3.0.27" if named else generic["memories"])
                    self.assertEqual(item["CatalogMcpDefault"], "2.0.1" if named else generic["mcp"])
                    self.assertEqual(pins(item, "TenantsPins"), {name: item["CatalogTenantsDefault"] for name in TENANTS_PACKAGES})
                    self.assertEqual(pins(item, "MemoriesPins"), {name: item["CatalogMemoriesDefault"] for name in MEMORIES_PACKAGES})
        self.assertEqual(receipt["catalogSha256"], hashlib.sha256(raw).hexdigest())
        self.assertFalse(receipt["candidateAcceptance"])
        self.assertFalse(receipt["runtimeAcceptance"])

    def test_named_and_generic_package_family_drift_refuses_in_every_context(self):
        receipt = gitlink_receipt()
        for context in CONTEXTS:
            for index in (0, -1):
                for field in ("Sdk", "Pins", "CatalogTenantsDefault", "TenantsPins", "CatalogMemoriesDefault", "MemoriesPins",
                              "CatalogMcpDefault", "McpPins", "PlatformView", "PartiesView"):
                    observations = [copy.deepcopy(receipt[name]) for name in CONTEXTS]
                    observations[CONTEXTS.index(context)][index][field] = "unqualified"
                    with self.subTest(context=context, index=index, field=field), \
                            patch("current_catalog_view.evaluate_views", side_effect=observations), self.assertRaises(ValueError):
                        self.verify(historical_catalog())

    def test_each_tenants_family_fault_refuses(self):
        receipt = gitlink_receipt()
        for context in CONTEXTS:
            for index in (0, -1):
                for package in TENANTS_PACKAGES:
                    for fault in ("property", "missing", "duplicate", "pin"):
                        changed = {name: copy.deepcopy(receipt[name]) for name in CONTEXTS}
                        item = changed[context][index]
                        values = item["TenantsPins"].split("|")
                        selected = next(pin for pin in values if pin.startswith(package + "="))
                        if fault == "property": item["CatalogTenantsDefault"] = "99.0.0"
                        elif fault == "missing": values.remove(selected)
                        elif fault == "duplicate": values.append(selected)
                        else: values[values.index(selected)] = package + "=99.0.0"
                        item["TenantsPins"] = "|".join(values)
                        with self.subTest(context=context, index=index, package=package, fault=fault), \
                                patch("current_catalog_view.evaluate_views", side_effect=[changed[name] for name in CONTEXTS]), \
                                self.assertRaisesRegex(ValueError, "unqualified Tenants|unqualified current-owner|disagree"):
                            self.verify(historical_catalog())

    def test_named_and_unknown_mcp_property_or_package_drift_refuses(self):
        receipt = gitlink_receipt()
        generic = receipt["genericDefaults"]["mcp"]
        for context in CONTEXTS:
            for index in (0, -1):
                for fault in ("property", "pin", "missing", "duplicate", "foreign"):
                    observations = [copy.deepcopy(receipt[name]) for name in CONTEXTS]
                    item = observations[CONTEXTS.index(context)][index]
                    wrong = generic if item["CatalogMcpDefault"] == "2.0.1" else "2.0.1"
                    if fault == "property": item["CatalogMcpDefault"] = wrong
                    elif fault == "pin": item["McpPins"] = "Hexalith.McpCli.Abstractions=" + wrong
                    elif fault == "missing": item["McpPins"] = ""
                    elif fault == "duplicate": item["McpPins"] += "|" + item["McpPins"]
                    else: item["McpPins"] = "Foreign.McpCli.Abstractions=" + item["CatalogMcpDefault"]
                    with self.subTest(context=context, index=index, fault=fault), \
                            patch("current_catalog_view.evaluate_views", side_effect=observations), \
                            self.assertRaisesRegex(ValueError, "unqualified MCP|unqualified current-owner|disagree"):
                        self.verify(historical_catalog())

    def test_current_owner_views_refuse_changed_family_and_unknown_aliases(self):
        receipt = gitlink_receipt()
        generic = receipt["genericDefaults"]
        options = dict(generic_tenants=generic["tenants"], generic_mcp=generic["mcp"], generic_sdk=generic["sdk"],
                       generic_openapi=tuple(generic["openapi"]), generic_memories=generic["memories"], generic_parties=generic["parties"])
        verify_current_owner_observations(receipt["currentOwnerEvaluations"], **options)
        for fault in ("sdk", "memories", "tenants", "parties", "mcp", "pins", "unknown", "missing", "duplicate", "future"):
            observed = copy.deepcopy(receipt["currentOwnerEvaluations"])
            field = {"sdk": "Sdk", "memories": "CatalogMemoriesDefault", "tenants": "CatalogTenantsDefault",
                     "parties": "CatalogPartiesDefault", "mcp": "CatalogMcpDefault"}.get(fault)
            if field: observed[0][field] = "99.0.0"
            elif fault == "pins": observed[0]["Pins"] = observed[0]["Pins"].replace("Hexalith.EventStore.Contracts=4.2.0", "Hexalith.EventStore.Contracts=4.2.1")
            elif fault == "unknown": observed[-1]["PartiesView"] = "true"
            elif fault == "missing": observed.pop()
            elif fault == "duplicate": observed.append(observed[0])
            else: observed[-1]["Identity"] = "Hexalith.Parties.Future"
            with self.subTest(fault=fault), self.assertRaisesRegex(ValueError, "exact source project|unqualified current-owner"):
                verify_current_owner_observations(observed, **options)

    def test_split_memories_family_or_pinned_sdk_member_in_the_real_catalog_refuses(self):
        raw = historical_catalog()
        for name in (MEMORIES_PACKAGES[0], MEMORIES_PACKAGES[-1]):
            document = ET.fromstring(raw)
            next(item for item in document.iter() if item.tag.rsplit("}", 1)[-1] == "PackageVersion"
                 and item.get("Include") == name).set("Version", "99.0.0")
            changed = ET.tostring(document)
            with self.subTest(package=name), self.assertRaisesRegex(ValueError, "Memories consumer view"):
                verify_memories_observations(evaluate_views(changed), generic_memories=gitlink_receipt()["genericDefaults"]["memories"])
        document = ET.fromstring(raw)
        next(item for item in document.iter() if item.tag.rsplit("}", 1)[-1] == "PackageVersion"
             and item.get("Include") == "Hexalith.EventStore.Contracts").set("Version", "4.2.0")
        with self.assertRaisesRegex(ValueError, "unqualified SDK/OpenAPI consumer view"):
            verify_observations(evaluate_views(ET.tostring(document)), "4.2.0", gitlink_receipt()["genericDefaults"]["sdk"],
                                generic_openapi=tuple(gitlink_receipt()["genericDefaults"]["openapi"]))


class CurrentCatalogViewTests(unittest.TestCase):
    def verify(self, raw):
        return verify_named_platform_catalog(raw, hashlib.sha256(raw).hexdigest())

    def test_exact_named_views_and_unknown_defaults_are_independently_evaluated(self):
        receipt = self.verify(catalog_body())
        self.assertEqual(list(PLATFORM_CONSUMERS), receipt["consumers"])
        self.assertEqual(len(receipt["evaluations"]), 36)
        self.assertEqual({item["Identity"] for item in receipt["defaultEvaluations"] if item["Sdk"] == "5.0.0"}, set(UNKNOWN_CONSUMERS))
        self.assertEqual({item["Sdk"] for item in receipt["collectingEvaluations"]}, {"4.2.0"})
        self.assertEqual(len(receipt["currentOwnerEvaluations"]), 7)
        self.assertFalse(receipt["runtimeAcceptance"])
        self.assertFalse(receipt["candidateAcceptance"])

    def test_moved_generic_defaults_need_no_code_change(self):
        """An adoption that moves every generic default still verifies; the named SDK4.2 family is unchanged."""
        receipt = self.verify(catalog_body(sdk="5.3.0", aspnet="11.0.1", openapi="3.12.0", memories="4.1.0",
                                           tenants="8.0.2", parties="3.2.0", mcp="3.1.0"))
        self.assertEqual(receipt["genericDefaults"], {"sdk": "5.3.0", "openapi": ["11.0.1", "3.12.0"], "memories": "4.1.0",
                                                      "tenants": "8.0.2", "parties": "3.2.0", "mcp": "3.1.0"})
        for item in receipt["defaultEvaluations"]:
            named = item["Identity"] in PLATFORM_CONSUMERS
            self.assertEqual((item["Sdk"], item["CatalogTenantsDefault"]), ("4.2.0", "7.0.7") if named else ("5.3.0", "8.0.2"))

    def test_generic_defaults_must_agree_and_stay_off_the_named_view(self):
        observed = evaluate_views(catalog_body())
        self.assertEqual(generic_defaults(observed)["sdk"], "5.0.0")
        for fault in ("disagree", "missing", "named", "undeclared"):
            changed = copy.deepcopy(observed)
            unknown = [item for item in changed if item["Identity"] in UNKNOWN_CONSUMERS]
            if fault == "disagree": unknown[-1]["CatalogTenantsDefault"] = "8.0.9"
            elif fault == "missing": changed.remove(unknown[-1])
            elif fault == "named":
                for item in unknown: item["Sdk"] = "4.2.0"
            else:
                for item in unknown: item["CatalogMcpDefault"] = ""
            with self.subTest(fault=fault), self.assertRaisesRegex(ValueError, "disagree|named SDK4.2 view|every generic default"):
                generic_defaults(changed)
        with self.assertRaisesRegex(ValueError, "named SDK4.2 view"):
            self.verify(catalog_body(sdk="4.2.0"))

    def test_finite_automatic_sdk42_successor_preserves_current_default_and_memories_companion(self):
        document = ET.fromstring(catalog_body(sdk="5.1.0"))
        defaults = evaluate_views(ET.tostring(document))
        collecting = evaluate_views(ET.tostring(document), "4.2.0")
        verify_observations(defaults, "4.2.0", "5.1.0")
        verify_observations(collecting, "4.2.0", "4.2.0")
        verify_memories_observations(defaults)
        verify_memories_observations(collecting)
        for item in defaults:
            named = item["Identity"] in PLATFORM_CONSUMERS
            self.assertEqual(item["Sdk"], "4.2.0" if named else "5.1.0")
            self.assertEqual(item["CatalogMemoriesDefault"], "3.0.27" if named else "4.0.0")
        companion = [item for item in document.find("PropertyGroup") if item.tag == "HexalithMemoriesVersion"][1]
        for incompatible in ("4.0.0", "3.0.25", "3.0.28"):
            companion.text = incompatible
            with self.subTest(memories=incompatible), self.assertRaisesRegex(ValueError, "SDK4.2/Memories consumer view"):
                verify_memories_observations(evaluate_views(ET.tostring(document)))
        companion.text = "3.0.27"
        document.find(".//PackageVersion[@Include='Hexalith.Memories.EventStoreExtensions']").set("Version", "4.0.0")
        with self.assertRaisesRegex(ValueError, "SDK4.2/Memories consumer view"):
            verify_memories_observations(evaluate_views(ET.tostring(document)))

    def test_exact_consumer_inventory_does_not_implicitly_admit_new_platform_projects(self):
        names = {path.stem for directory in ("src", "tests") for path in (ROOT / directory).rglob("*.csproj")}
        self.assertEqual(len(PLATFORM_CONSUMERS), 33)
        self.assertEqual(names, set(PLATFORM_CONSUMERS))

    def test_changed_or_unbound_catalog_body_refuses_before_msbuild(self):
        raw = catalog_body()
        for digest, body in (("0" * 64, raw), (hashlib.sha256(raw).hexdigest(), raw + b"\n"), (None, raw)):
            with self.subTest(digest=digest), patch("current_catalog_view.evaluate_views") as evaluate:
                with self.assertRaisesRegex(ValueError, "authenticated catalog Git body"):
                    verify_named_platform_catalog(body, digest)
                evaluate.assert_not_called()

    def test_named_view_cannot_be_read_from_an_unverified_worktree_path(self):
        from current_catalog_view import verify_extension_catalog_pin
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "catalog.props"
            raw = catalog_body()
            path.write_bytes(raw)
            with self.assertRaisesRegex(ValueError, "authenticated catalog Git body"):
                verify_extension_catalog_pin(path)
            receipt = verify_extension_catalog_pin(path, authenticated_body=raw, expected_sha256=hashlib.sha256(raw).hexdigest())
            self.assertEqual(receipt["kind"], "finite-current-platform-sdk42-catalog-view")

    def test_unknown_broad_duplicate_or_missing_consumer_selector_refuses_before_execution(self):
        raw = catalog_body()
        for mutation in ("unknown", "prefix", "duplicate", "missing", "global"):
            document = ET.fromstring(raw)
            selectors = document.findall(".//HexalithPlatformSdk42CatalogView")
            condition = selectors[1].attrib["Condition"]
            if mutation == "unknown": condition = condition.replace(PLATFORM_CONSUMERS[0], "Hexalith.Platform.Unqualified")
            elif mutation == "prefix": condition = "$(MSBuildProjectName.StartsWith('Hexalith.Platform.'))"
            elif mutation == "duplicate": condition += " or $(MSBuildProjectName.Equals('" + PLATFORM_CONSUMERS[0] + "'))"
            elif mutation == "missing": condition = condition.split(" or ", 1)[1]
            else: selectors[0].text = "true"
            selectors[1].set("Condition", condition)
            with self.subTest(mutation=mutation), patch("current_catalog_view.evaluate_views") as evaluate:
                with self.assertRaisesRegex(ValueError, "finite|33 approved"):
                    self.verify(ET.tostring(document))
                evaluate.assert_not_called()

    def test_real_evaluation_rejects_future_sdk_wrong_openapi_and_changed_default(self):
        for mutation in ("sdk5", "sdk421", "aspnet11", "openapi3", "default-sdk", "default-api", "duplicate-api"):
            document = ET.fromstring(catalog_body())
            properties = document.find("PropertyGroup")
            def selected(name, index):
                return [item for item in properties if item.tag == name][index]
            if mutation in ("sdk5", "sdk421"):
                selected("HexalithEventStoreVersion", 0).text = "5.0.0" if mutation == "sdk5" else "4.2.1"
            elif mutation == "aspnet11": selected("AspNetApi", 1).text = "11.0.0"
            elif mutation == "openapi3": selected("OpenApi", 1).text = "3.11.0"
            elif mutation == "default-sdk": selected("HexalithEventStoreVersion", 1).text = "4.2.0"
            elif mutation == "default-api": selected("OpenApi", 0).text = "2.13.0"
            else: ET.SubElement(document.find("ItemGroup"), "PackageVersion", Include="Microsoft.OpenApi", Version="2.13.0")
            changed = ET.tostring(document)
            with self.subTest(mutation=mutation), self.assertRaisesRegex(ValueError, "unqualified SDK/OpenAPI consumer view"):
                verify_observations(evaluate_views(changed), "4.2.0", "5.0.0")
                verify_observations(evaluate_views(changed, "4.2.0"), "4.2.0", "4.2.0")

    def test_current_evaluation_cannot_inherit_a_callers_sdk_or_build_import(self):
        with tempfile.TemporaryDirectory() as directory:
            poison = Path(directory) / "poison.props"
            poison.write_text('<Project><PropertyGroup><HexalithEventStoreVersion>99.0.0</HexalithEventStoreVersion></PropertyGroup></Project>')
            with patch.dict(os.environ, {"HexalithEventStoreVersion": "99.0.0", "UseHexalithProjectReferences": "true",
                                         "CustomAfterMicrosoftCommonProps": str(poison), "DirectoryPackagesPropsPath": str(poison)}):
                receipt = self.verify(catalog_body())
            self.assertEqual(len(receipt["evaluations"]), 36)


if __name__ == "__main__":
    unittest.main()
