"""Real MSBuild controls for the finite current catalog, independent of source publication."""
import hashlib
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

from current_catalog_view import (OPENAPI_PACKAGES, PLATFORM_CONSUMERS, SDK_PACKAGES, UNKNOWN_CONSUMERS,
                                  verify_named_platform_catalog)

BUILDS6_SOURCE = "1ebc5b84ff2c8afd21a87d80ef3d9c9048eaf5e5"
BUILDS6_CATALOG_BLOB = "cbfc6a888f269010600103a33c73d0cfc0af93a2"
BUILDS6_CATALOG_SHA256 = "45b05f6755f337baccfaa805e5e4afb4a57e91b2f70226a357c82d03b4c19bb7"


def builds6_catalog_body() -> bytes:
    """Read the exact published Git body offline, independently of selected/shallow producer checkouts."""
    raw = (Path(__file__).with_name("fixtures") / "builds-6.0.0-Directory.Packages.props").read_bytes()
    blob = hashlib.sha1(b"blob " + str(len(raw)).encode() + b"\0" + raw).hexdigest()
    if blob != BUILDS6_CATALOG_BLOB or hashlib.sha256(raw).hexdigest() != BUILDS6_CATALOG_SHA256:
        raise ValueError("Published Builds6 catalog fixture differs from its exact source " + BUILDS6_SOURCE)
    return raw


def catalog_body() -> bytes:
    project = ET.Element("Project")
    properties = ET.SubElement(project, "PropertyGroup")
    ET.SubElement(properties, "HexalithPlatformSdk42CatalogView").text = "false"
    condition = " or ".join("$(MSBuildProjectName.Equals('" + name + "'))" for name in PLATFORM_CONSUMERS)
    ET.SubElement(properties, "HexalithPlatformSdk42CatalogView", Condition=condition).text = "true"
    ET.SubElement(properties, "HexalithPartiesSdk42CatalogView").text = "false"
    ET.SubElement(properties, "HexalithEventStoreVersion", Condition="'$(HexalithEventStoreVersion)' == '' and '$(HexalithPlatformSdk42CatalogView)' == 'true'").text = "4.2.0"
    ET.SubElement(properties, "HexalithEventStoreVersion", Condition="'$(HexalithEventStoreVersion)' == ''").text = "5.0.0"
    for name, default, compatible in (("AspNetApi", "11.0.0", "10.0.15"), ("OpenApi", "3.11.0", "2.13.0")):
        ET.SubElement(properties, name).text = default
        ET.SubElement(properties, name, Condition="'$(HexalithPlatformSdk42CatalogView)' == 'true' and '$(HexalithEventStoreVersion)' == '4.2.0'").text = compatible
    packages = ET.SubElement(project, "ItemGroup")
    for name in SDK_PACKAGES:
        ET.SubElement(packages, "PackageVersion", Include=name, Version="$(HexalithEventStoreVersion)")
    for name, selector in zip(OPENAPI_PACKAGES, ("AspNetApi", "OpenApi")):
        ET.SubElement(packages, "PackageVersion", Include=name, Version="$(" + selector + ")")
    return ET.tostring(project, encoding="utf-8")


class CurrentCatalogViewTests(unittest.TestCase):
    def verify(self, raw):
        return verify_named_platform_catalog(raw, hashlib.sha256(raw).hexdigest())

    def test_exact_named_views_and_unknown_defaults_are_independently_evaluated(self):
        receipt = self.verify(catalog_body())
        self.assertEqual(list(PLATFORM_CONSUMERS), receipt["consumers"])
        self.assertEqual(len(receipt["evaluations"]), 36)
        self.assertEqual({item["Identity"] for item in receipt["defaultEvaluations"] if item["Sdk"] == "5.0.0"}, set(UNKNOWN_CONSUMERS))
        self.assertEqual({item["Sdk"] for item in receipt["evaluations"]}, {"4.2.0"})
        self.assertFalse(receipt["runtimeAcceptance"])
        self.assertFalse(receipt["candidateAcceptance"])

    def test_actual_builds6_default_and_explicit_sdk42_views_preserve_exact_negative_consumers(self):
        receipt = self.verify(builds6_catalog_body())
        self.assertEqual(len(receipt["defaultEvaluations"]), 36)
        self.assertEqual({item["Sdk"] for item in receipt["defaultEvaluations"]}, {"5.1.0"})
        self.assertEqual(len(receipt["evaluations"]), 36)
        for item in receipt["evaluations"]:
            with self.subTest(consumer=item["Identity"]):
                self.assertEqual(item["Sdk"], "4.2.0")
                pins = dict(pin.split("=", 1) for pin in item["Pins"].split("|"))
                named = item["Identity"] in PLATFORM_CONSUMERS
                self.assertEqual(item["PlatformView"], str(named).lower())
                self.assertEqual([pins[name] for name in OPENAPI_PACKAGES],
                    ["10.0.15", "2.13.0"] if named else ["11.0.0", "3.11.0"])
        self.assertFalse(receipt["runtimeAcceptance"])
        self.assertFalse(receipt["candidateAcceptance"])

    def test_actual_builds6_future_default_and_openapi_leaks_refuse(self):
        for mutation in ("future-default", "missing-named-pair", "unknown-openapi-leak", "wrong-sdk-package"):
            document = ET.fromstring(builds6_catalog_body())
            if mutation == "future-default":
                document.findall(".//HexalithEventStoreVersion")[-1].text = "5.2.0"
            elif mutation == "wrong-sdk-package":
                document.find(".//PackageVersion[@Include='Hexalith.EventStore.Contracts']").set("Version", "4.2.0")
            else:
                for name in ("HexalithAspNetCoreOpenApiVersion", "HexalithOpenApiVersion"):
                    compatible = document.findall(".//" + name)[-1]
                    condition = compatible.get("Condition")
                    if mutation == "missing-named-pair":
                        condition = condition.replace("or '$(HexalithPlatformSdk42CatalogView)' == 'true'", "")
                    else:
                        condition = condition.replace("'$(HexalithPlatformSdk42CatalogView)' == 'true'",
                            "'$(HexalithPlatformSdk42CatalogView)' == 'true' or '$(MSBuildProjectName)' == 'Unqualified.Consumer'")
                    compatible.set("Condition", condition)
            with self.subTest(mutation=mutation), self.assertRaisesRegex(ValueError, "unqualified SDK/OpenAPI consumer view"):
                self.verify(ET.tostring(document))

    def test_exact_consumer_inventory_does_not_implicitly_admit_new_platform_projects(self):
        root = Path(__file__).resolve().parents[1]
        names = {path.stem for directory in ("src", "tests") for path in (root / directory).rglob("*.csproj")}
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
        from run_tuple_ci import verify_extension_catalog_pin
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
            if mutation in ("sdk5", "sdk421"):
                properties.findall("HexalithEventStoreVersion")[0].text = "5.0.0" if mutation == "sdk5" else "4.2.1"
            elif mutation == "aspnet11": properties.findall("AspNetApi")[1].text = "11.0.0"
            elif mutation == "openapi3": properties.findall("OpenApi")[1].text = "3.11.0"
            elif mutation == "default-sdk": properties.findall("HexalithEventStoreVersion")[1].text = "4.2.0"
            elif mutation == "default-api": properties.findall("OpenApi")[0].text = "2.13.0"
            else: ET.SubElement(document.find("ItemGroup"), "PackageVersion", Include="Microsoft.OpenApi", Version="2.13.0")
            with self.subTest(mutation=mutation), self.assertRaisesRegex(ValueError, "unqualified SDK/OpenAPI consumer view"):
                self.verify(ET.tostring(document))

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
