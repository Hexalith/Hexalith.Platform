"""Real MSBuild controls for the finite current catalog, independent of source publication."""
import copy
import hashlib
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

from current_catalog_view import (GENERATION6_CATALOG, OPENAPI_PACKAGES, PLATFORM_CONSUMERS, SDK_PACKAGES, UNKNOWN_CONSUMERS,
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


def generation6_binding():
    """Synthetic publication metadata tests catalog admission only, never publication authority."""
    source = GENERATION6_CATALOG['sourceRevision']
    manifest = json.dumps({'sha': source, 'version': '6.0.0', 'packages': [
        {'id': identity, 'path': 'packages/' + identity + '.6.0.0.nupkg'}
        for identity in ('Hexalith.Builds.Evidence.Cli', 'Hexalith.Builds.Module.Cli')], 'containers': []})
    return {'schemaVersion': 1, 'candidateAcceptance': False,
        'producer': {'repository': 'https://github.com/Hexalith/Hexalith.Builds.git', 'sourceRevision': source,
            'catalog': {'path': 'Props/Directory.Packages.props', 'blob': GENERATION6_CATALOG['blob'],
                        'sha256': GENERATION6_CATALOG['sha256']}},
        'publication': {'kind': 'shared-release-handoff/v1', 'version': '6.0.0', 'sourceRevision': source,
            'releaseTag': 'v6.0.0', 'catalogSha256': GENERATION6_CATALOG['sha256'],
            'archiveSha256': 'a' * 64, 'toolingSha256': 'b' * 64, 'releaseId': 11, 'runId': 12, 'artifactId': 13,
            'artifactSha256': 'c' * 64, 'releaseBodySha256': 'd' * 64, 'manifestJson': manifest,
            'manifestSha256': hashlib.sha256(manifest.encode()).hexdigest()}}


def generation6_catalog():
    return builds6_catalog_body()


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
    def verify(self, raw, binding=None):
        return verify_named_platform_catalog(raw, hashlib.sha256(raw).hexdigest(), binding=binding)

    def test_exact_named_views_and_unknown_defaults_are_independently_evaluated(self):
        receipt = self.verify(catalog_body())
        self.assertEqual(list(PLATFORM_CONSUMERS), receipt["consumers"])
        self.assertEqual(len(receipt["evaluations"]), 36)
        self.assertEqual({item["Identity"] for item in receipt["defaultEvaluations"] if item["Sdk"] == "5.0.0"}, set(UNKNOWN_CONSUMERS))
        self.assertEqual({item["Sdk"] for item in receipt["collectingEvaluations"]}, {"4.2.0"})
        self.assertFalse(receipt["runtimeAcceptance"])
        self.assertFalse(receipt["candidateAcceptance"])

    def test_actual_builds6_default_and_explicit_sdk42_views_preserve_exact_negative_consumers(self):
        receipt = self.verify(builds6_catalog_body(), generation6_binding())
        self.assertEqual(len(receipt["defaultEvaluations"]), 36)
        self.assertEqual({item["Sdk"] for item in receipt["defaultEvaluations"]}, {"5.1.0"})
        self.assertEqual({item["CatalogMemoriesDefault"] for item in receipt["defaultEvaluations"]}, {"4.0.0"})
        self.assertEqual(len(receipt["collectingEvaluations"]), 36)
        for item in receipt["collectingEvaluations"]:
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
            from current_catalog_view import evaluate_views, verify_observations
            changed = ET.tostring(document)
            with self.subTest(mutation=mutation), self.assertRaisesRegex(ValueError, "unqualified SDK/OpenAPI consumer view"):
                verify_observations(evaluate_views(changed), "5.1.0", "5.1.0")
                verify_observations(evaluate_views(changed, "4.2.0"), "4.2.0", "4.2.0")

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
            from current_catalog_view import evaluate_views, verify_observations
            changed = ET.tostring(document)
            with self.subTest(mutation=mutation), self.assertRaisesRegex(ValueError, "unqualified SDK/OpenAPI consumer view"):
                verify_observations(evaluate_views(changed), "5.1.0", "5.1.0")
                verify_observations(evaluate_views(changed, "4.2.0"), "4.2.0", "4.2.0")

    def test_current_evaluation_cannot_inherit_a_callers_sdk_or_build_import(self):
        with tempfile.TemporaryDirectory() as directory:
            poison = Path(directory) / "poison.props"
            poison.write_text('<Project><PropertyGroup><HexalithEventStoreVersion>99.0.0</HexalithEventStoreVersion></PropertyGroup></Project>')
            with patch.dict(os.environ, {"HexalithEventStoreVersion": "99.0.0", "UseHexalithProjectReferences": "true",
                                         "CustomAfterMicrosoftCommonProps": str(poison), "DirectoryPackagesPropsPath": str(poison)}):
                receipt = self.verify(catalog_body())
            self.assertEqual(len(receipt["evaluations"]), 36)

    def test_exact_published_generation6_catalog_keeps_current_and_collecting_views_separate(self):
        receipt = verify_named_platform_catalog(generation6_catalog(), GENERATION6_CATALOG['sha256'], binding=generation6_binding())
        self.assertEqual({item['Sdk'] for item in receipt['evaluations']}, {'5.1.0'})
        self.assertEqual({item['Sdk'] for item in receipt['collectingEvaluations']}, {'4.2.0'})
        self.assertEqual(len(receipt['evaluations']), 36)
        self.assertEqual(len(receipt['collectingEvaluations']), 36)
        self.assertFalse(receipt['candidateAcceptance'])
        self.assertFalse(receipt['runtimeAcceptance'])

    def test_generation6_view_cannot_inherit_missing_or_foreign_producer_authority(self):
        raw = generation6_catalog()
        for fault in ('missing', 'source', 'blob', 'hash', 'neighbor', 'future'):
            binding = generation6_binding()
            if fault == 'missing': binding = None
            elif fault == 'source':
                binding['producer']['sourceRevision'] = binding['publication']['sourceRevision'] = 'e' * 40
                manifest = json.loads(binding['publication']['manifestJson'])
                manifest['sha'] = 'e' * 40
                binding['publication']['manifestJson'] = json.dumps(manifest)
                binding['publication']['manifestSha256'] = hashlib.sha256(json.dumps(manifest).encode()).hexdigest()
            elif fault == 'blob': binding['producer']['catalog']['blob'] = 'e' * 40
            elif fault == 'hash': binding['producer']['catalog']['sha256'] = binding['publication']['catalogSha256'] = 'e' * 64
            else:
                version = '6.0.1' if fault == 'neighbor' else '7.0.0'
                binding['publication'].update(version=version, releaseTag='v' + version)
                manifest = json.loads(binding['publication']['manifestJson'])
                manifest['version'] = version
                for item in manifest['packages']:
                    item['path'] = 'packages/' + item['id'] + '.' + version + '.nupkg'
                raw_manifest = json.dumps(manifest)
                binding['publication'].update(manifestJson=raw_manifest, manifestSha256=hashlib.sha256(raw_manifest.encode()).hexdigest())
            if fault == 'missing':
                with self.subTest(fault=fault), self.assertRaisesRegex(ValueError, 'unqualified SDK/OpenAPI'):
                    verify_named_platform_catalog(raw, GENERATION6_CATALOG['sha256'], binding=binding)
            else:
                with self.subTest(fault=fault), patch('current_catalog_view.evaluate_views') as evaluate:
                    with self.assertRaisesRegex(ValueError, 'exact qualified Builds6|exact supported'):
                        verify_named_platform_catalog(raw, GENERATION6_CATALOG['sha256'], binding=binding)
                    evaluate.assert_not_called()

    def test_generation6_default_and_retained_collecting_drift_both_refuse(self):
        raw, binding = generation6_catalog(), generation6_binding()
        accepted = verify_named_platform_catalog(raw, GENERATION6_CATALOG['sha256'], binding=binding)
        for collecting, field in ((False, 'Sdk'), (False, 'Pins'), (True, 'Sdk'), (True, 'Pins'), (True, 'unknown-openapi')):
            defaults = copy.deepcopy(accepted['evaluations'])
            retained = copy.deepcopy(accepted['collectingEvaluations'])
            selected = retained if collecting else defaults
            if field == 'unknown-openapi':
                selected[len(PLATFORM_CONSUMERS)]['Pins'] = selected[len(PLATFORM_CONSUMERS)]['Pins'].replace('Microsoft.OpenApi=3.11.0', 'Microsoft.OpenApi=2.13.0')
            else:
                selected[0][field] = '5.1.1' if field == 'Sdk' else selected[0][field].replace('Microsoft.OpenApi=', 'Foreign.OpenApi=')
            with self.subTest(collecting=collecting, field=field), patch('current_catalog_view.evaluate_views', side_effect=[defaults, retained]), self.assertRaisesRegex(ValueError, 'unqualified SDK/OpenAPI|retained SDK4.2'):
                verify_named_platform_catalog(raw, GENERATION6_CATALOG['sha256'], binding=binding)


if __name__ == "__main__":
    unittest.main()
