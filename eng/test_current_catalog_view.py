"""Real MSBuild controls for the finite current catalog, independent of source publication."""
import copy
import hashlib
import json
import os
import subprocess
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

from current_catalog_view import (GENERATION6_CATALOG, GENERATION601_CATALOG, GENERATION602_CATALOG, GENERATION603_CATALOG, CURRENT_OWNER_CONSUMERS, UNKNOWN_OWNER_CONSUMERS, MEMORIES_PACKAGES, OPENAPI_PACKAGES, PLATFORM_CONSUMERS, SDK_PACKAGES, TENANTS_PACKAGES, UNKNOWN_CONSUMERS,
                                  verify_named_platform_catalog)

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
    """Read the unchanged published6.0.0 catalog control."""
    return catalog_fixture("builds-6.0.0-Directory.Packages.props", BUILDS6_SOURCE,
        BUILDS6_CATALOG_BLOB, BUILDS6_CATALOG_SHA256)


def current_platform_catalog_body() -> bytes:
    """Read committed finite Platform catalog policy; component evidence grants no publication or runtime acceptance."""
    return catalog_fixture("builds-c91beec6-Directory.Packages.props", "c91beec60ce5eaa8cd62bf055d07fa6ca3890e89",
        "e17e81e703da4cd1805644134e62ec1122cafb47", "ef691a7a3caa7a92e524f766a770574cd21ef5e32fccd996d1d2899b9d7bddd7")


def builds602_catalog_body() -> bytes:
    """Read the exact committed producer fix; publication authority remains a separate binding."""
    return catalog_fixture("builds-60ce7967-Directory.Packages.props", GENERATION602_CATALOG["sourceRevision"],
        GENERATION602_CATALOG["blob"], GENERATION602_CATALOG["sha256"])


def builds603_catalog_body() -> bytes:
    """Read the genuine6.0.3 source catalog with its exact committed LF identity."""
    return catalog_fixture("builds-6.0.3-Directory.Packages.props", GENERATION603_CATALOG["sourceRevision"],
        GENERATION603_CATALOG["blob"], GENERATION603_CATALOG["sha256"])


def observe_producer_projects(raw: bytes, names: tuple[str, ...], sdk_version: str | None = None) -> list[dict]:
    """Observe actual producer project identities without claiming mapping or publication authority."""
    with tempfile.TemporaryDirectory(prefix="retained-producer-view-") as temporary:
        root = Path(temporary)
        catalog = root / "catalog.props"
        catalog.write_bytes(raw)
        observations = []
        allowed = {"PATH", "PATHEXT", "HOME", "USERPROFILE", "TMP", "TEMP", "TMPDIR", "SystemRoot",
                   "SystemDrive", "WINDIR", "COMSPEC", "APPDATA", "LOCALAPPDATA", "ProgramFiles",
                   "ProgramFiles(x86)", "ProgramW6432", "DOTNET_ROOT", "DOTNET_ROOT_X64", "DOTNET_ROOT_X86",
                   "LD_LIBRARY_PATH", "DYLD_LIBRARY_PATH", "LANG", "LC_ALL", "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"}
        environment = {key: value for key, value in os.environ.items() if key in allowed}
        for name in names:
            project = ET.Element("Project")
            ET.SubElement(project, "Import", Project=str(catalog))
            path = root / (name + ".proj")
            path.write_bytes(ET.tostring(project))
            command = ["dotnet", "msbuild", str(path), "-nologo", "-nodeReuse:false",
                "-getProperty:HexalithEventStoreVersion,HexalithMemoriesVersion", "-getItem:PackageVersion"]
            if sdk_version is not None:
                command.append("-p:HexalithEventStoreVersion=" + sdk_version)
            result = subprocess.run(command, cwd=root, env=environment, capture_output=True, text=True, timeout=30)
            if result.returncode:
                raise AssertionError("Independent retained producer evaluation failed: " + result.stdout + result.stderr)
            document = json.loads(result.stdout)
            observations.append({"project": name, **document["Properties"],
                "MemoriesPins": {item["Identity"]: item["Version"] for item in document["Items"]["PackageVersion"]
                    if item["Identity"] in MEMORIES_PACKAGES}})
        return observations



def generation6_binding(version="6.0.0"):
    """Recorded official metadata is component input, never an online publication acceptance claim."""
    return json.loads((Path(__file__).with_name("fixtures") / ("builds-" + version + "-binding.json")).read_bytes())


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

    def test_finite_automatic_sdk42_successor_preserves_current_default_and_memories_companion(self):
        document = ET.fromstring(catalog_body())
        properties = document.find("PropertyGroup")
        properties.findall("HexalithEventStoreVersion")[-1].text = "5.1.0"
        ET.SubElement(properties, "HexalithMemoriesVersion").text = "4.0.0"
        companion = ET.SubElement(properties, "HexalithMemoriesVersion",
            Condition="'$(HexalithPlatformSdk42CatalogView)' == 'true' and '$(HexalithEventStoreVersion)' == '4.2.0'")
        companion.text = "3.0.27"
        for name in MEMORIES_PACKAGES:
            ET.SubElement(document.find("ItemGroup"), "PackageVersion", Include=name, Version="$(HexalithMemoriesVersion)")
        from current_catalog_view import evaluate_views, verify_memories_observations, verify_observations
        defaults = evaluate_views(ET.tostring(document))
        collecting = evaluate_views(ET.tostring(document), "4.2.0")
        verify_observations(defaults, "4.2.0", "5.1.0")
        verify_observations(collecting, "4.2.0", "4.2.0")
        verify_memories_observations(defaults)
        verify_memories_observations(collecting)
        receipt = {"defaultEvaluations": defaults}
        for item in receipt["defaultEvaluations"]:
            named = item["Identity"] in PLATFORM_CONSUMERS
            self.assertEqual(item["Sdk"], "4.2.0" if named else "5.1.0")
            self.assertEqual(item["CatalogMemoriesDefault"], "3.0.27" if named else "4.0.0")
        for incompatible in ("4.0.0", "3.0.25", "3.0.28"):
            companion.text = incompatible
            with self.subTest(memories=incompatible), self.assertRaisesRegex(ValueError, "SDK4.2/Memories consumer view"):
                verify_memories_observations(evaluate_views(ET.tostring(document)))
        companion.text = "3.0.27"
        document.find(".//PackageVersion[@Include='Hexalith.Memories.EventStoreExtensions']").set("Version", "4.0.0")
        with self.assertRaisesRegex(ValueError, "SDK4.2/Memories consumer view"):
            verify_memories_observations(evaluate_views(ET.tostring(document)))

    def test_committed_current_catalog_verifies_all_five_memories_pins_and_refuses_split_families(self):
        raw = current_platform_catalog_body()
        receipt = self.verify(raw, generation6_binding("6.0.1"))
        for context in ("defaultEvaluations", "collectingEvaluations"):
            for item in receipt[context]:
                named = item["Identity"] in PLATFORM_CONSUMERS
                pins = dict(pin.split("=", 1) for pin in item["MemoriesPins"].split("|"))
                self.assertEqual(pins, {name: "3.0.27" if named else "4.0.0" for name in MEMORIES_PACKAGES})
                if context == "defaultEvaluations":
                    self.assertEqual(item["Sdk"], "4.2.0" if named else "5.1.0")
        from current_catalog_view import evaluate_views, verify_observations
        sdk51 = evaluate_views(raw, "5.1.0")
        verify_observations(sdk51, "5.1.0", "5.1.0")
        for item in sdk51:
            self.assertEqual(item["CatalogMemoriesDefault"], "4.0.0")
            self.assertEqual(dict(pin.split("=", 1) for pin in item["MemoriesPins"].split("|")),
                {name: "4.0.0" for name in MEMORIES_PACKAGES})
        for name in MEMORIES_PACKAGES:
            document = ET.fromstring(raw)
            document.find(".//PackageVersion[@Include='" + name + "']").set("Version", "4.0.0")
            from current_catalog_view import evaluate_views
            defaults = evaluate_views(ET.tostring(document))
            collecting = evaluate_views(ET.tostring(document), "4.2.0")
            with self.subTest(package=name), patch("current_catalog_view.evaluate_views", side_effect=[defaults, collecting]), \
                    self.assertRaisesRegex(ValueError, "SDK4.2/Memories consumer view"):
                self.verify(raw, generation6_binding("6.0.1"))


    def test_committed_successor_closes_actual_private_producer_memory_mismatch_without_default_leak(self):
        owners = ("Hexalith.Parties", "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions",
            "Hexalith.Parties.Projections", "Hexalith.Parties.Authentication", "Hexalith.Parties.Security",
            "Hexalith.Builds.Module.AppHost", "Hexalith.Builds.Module.EventStoreHost", "Hexalith.Builds.Module.UiHost")
        controls = (*UNKNOWN_CONSUMERS, "Hexalith.Parties.Future", "Hexalith.Builds.Module.FutureHost")
        for raw, expected_memory in ((current_platform_catalog_body(), "4.0.0"), (builds602_catalog_body(), "3.0.27")):
            for item in observe_producer_projects(raw, owners + controls):
                with self.subTest(project=item["project"], memories=expected_memory):
                    named = item["project"] in owners
                    memory = expected_memory if named else "4.0.0"
                    self.assertEqual(item["HexalithEventStoreVersion"], "4.2.0" if named else "5.1.0")
                    self.assertEqual(item["HexalithMemoriesVersion"], memory)
                    self.assertEqual(item["MemoriesPins"], {name: memory for name in MEMORIES_PACKAGES})
        for item in observe_producer_projects(builds602_catalog_body(), owners + controls, "5.1.0"):
            with self.subTest(explicit_sdk51=item["project"]):
                self.assertEqual(item["HexalithEventStoreVersion"], "5.1.0")
                self.assertEqual(item["HexalithMemoriesVersion"], "4.0.0")
                self.assertEqual(item["MemoriesPins"], {name: "4.0.0" for name in MEMORIES_PACKAGES})

    def test_actual_builds602_named_sdk42_and_generic_sdk51_views_are_separate(self):
        receipt = self.verify(builds602_catalog_body(), generation6_binding("6.0.2"))
        self.assertEqual(receipt["kind"], "finite-current-platform-sdk42-catalog-view")
        self.assertEqual(len(receipt["defaultEvaluations"]), 36)
        self.assertEqual(len(receipt["collectingEvaluations"]), 36)
        for item in receipt["defaultEvaluations"]:
            named = item["Identity"] in PLATFORM_CONSUMERS
            self.assertEqual(item["Sdk"], "4.2.0" if named else "5.1.0")
            self.assertEqual(item["CatalogMemoriesDefault"], "3.0.27" if named else "4.0.0")
        self.assertEqual({item["Sdk"] for item in receipt["collectingEvaluations"]}, {"4.2.0"})
        self.assertFalse(receipt["candidateAcceptance"])
        self.assertFalse(receipt["runtimeAcceptance"])
        self.assertEqual(len(receipt["currentOwnerEvaluations"]), 7)
        self.assertEqual({item["Identity"] for item in receipt["currentOwnerEvaluations"]},
            set(CURRENT_OWNER_CONSUMERS) | set(UNKNOWN_OWNER_CONSUMERS))

    def test_builds602_current_owner_views_refuse_changed_family_and_unknown_aliases(self):
        from current_catalog_view import verify_current_owner_observations
        receipt = self.verify(builds602_catalog_body(), generation6_binding("6.0.2"))
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
                verify_current_owner_observations(observed)

    def test_builds602_catalog_cannot_inherit_neighbor_foreign_or_future_authority(self):
        from current_catalog_view import catalog_profile
        for fault in ("source", "newer-checkout", "blob", "hash", "previous", "neighbor", "future"):
            binding = generation6_binding("6.0.2")
            if fault in ("source", "newer-checkout"):
                binding["producer"]["sourceRevision"] = binding["publication"]["sourceRevision"] = (GENERATION6_CATALOG["sourceRevision"] if fault == "source" else "12475add3f14b06709a5ffe74f413d27d4111d75")
            elif fault in ("blob", "hash"):
                binding["producer"]["catalog"]["blob" if fault == "blob" else "sha256"] = "e" * (40 if fault == "blob" else 64)
                if fault == "hash": binding["publication"]["catalogSha256"] = "e" * 64
            else:
                version = {"previous": "6.0.0", "neighbor": "6.0.3", "future": "7.0.0"}[fault]
                binding["publication"].update(version=version, releaseTag="v" + version)
            manifest = json.loads(binding["publication"]["manifestJson"])
            manifest.update(sha=binding["publication"]["sourceRevision"], version=binding["publication"]["version"])
            for item in manifest["packages"]:
                item["path"] = "packages/" + item["id"] + "." + manifest["version"] + ".nupkg"
            raw_manifest = json.dumps(manifest)
            binding["publication"].update(manifestJson=raw_manifest, manifestSha256=hashlib.sha256(raw_manifest.encode()).hexdigest())
            with self.subTest(fault=fault), patch("current_catalog_view.evaluate_views") as evaluate:
                with self.assertRaisesRegex(ValueError, "exact qualified Builds6|exact supported"):
                    catalog_profile(binding)
                evaluate.assert_not_called()

    def test_builds602_named_memories_and_sdk_default_drift_refuse(self):
        binding = generation6_binding("6.0.2")
        receipt = self.verify(builds602_catalog_body(), binding)
        for collecting, index, field, value in ((False, 0, "Sdk", "5.1.0"),
                (False, len(PLATFORM_CONSUMERS), "Sdk", "4.2.0"), (False, 0, "CatalogMemoriesDefault", "4.0.0"),
                (True, len(PLATFORM_CONSUMERS), "CatalogMemoriesDefault", "3.0.27")):
            defaults, retained = copy.deepcopy(receipt["defaultEvaluations"]), copy.deepcopy(receipt["collectingEvaluations"])
            (retained if collecting else defaults)[index][field] = value
            with self.subTest(collecting=collecting, field=field, index=index), patch("current_catalog_view.evaluate_views", side_effect=[defaults, retained]):
                with self.assertRaisesRegex(ValueError, "unqualified"):
                    self.verify(builds602_catalog_body(), binding)

    def _assert_tenants_family_refusals(self, raw, binding, receipt):
        contexts = ("defaultEvaluations", "collectingEvaluations", "currentOwnerEvaluations")
        for context in contexts:
            for index in (0, -1):
                for package in TENANTS_PACKAGES:
                    for fault in ("property", "missing", "duplicate", "pin"):
                        changed = {name: copy.deepcopy(receipt[name]) for name in contexts}
                        item = changed[context][index]
                        pins = item["TenantsPins"].split("|")
                        selected = next(pin for pin in pins if pin.startswith(package + "="))
                        if fault == "property": item["CatalogTenantsDefault"] = "99.0.0"
                        elif fault == "missing": pins.remove(selected)
                        elif fault == "duplicate": pins.append(selected)
                        else: pins[pins.index(selected)] = package + "=99.0.0"
                        item["TenantsPins"] = "|".join(pins)
                        with self.subTest(context=context, index=index, package=package, fault=fault), \
                                patch("current_catalog_view.evaluate_views", side_effect=[changed[name] for name in contexts]), \
                                self.assertRaisesRegex(ValueError, "unqualified Tenants|unqualified current-owner"):
                            self.verify(raw, binding)

    def test_builds602_verifies_the_complete_tenants_family_and_refuses_each_pin_fault(self):
        raw, binding = builds602_catalog_body(), generation6_binding("6.0.2")
        receipt = self.verify(raw, binding)
        for context in ("defaultEvaluations", "collectingEvaluations", "currentOwnerEvaluations"):
            for item in receipt[context]:
                self.assertEqual(item["CatalogTenantsDefault"], "7.0.7")
                self.assertEqual(dict(pin.split("=", 1) for pin in item["TenantsPins"].split("|")),
                    {name: "7.0.7" for name in TENANTS_PACKAGES})
        self._assert_tenants_family_refusals(raw, binding, receipt)

    def test_builds603_retains_named_tenants707_and_observes_generic_tenants8(self):
        from current_catalog_view import evaluate_views, verify_current_owner_observations
        binding = generation6_binding("6.0.3")
        receipt = self.verify(builds603_catalog_body(), binding)
        self.assertEqual(receipt["catalogSha256"], GENERATION603_CATALOG["sha256"])
        self.assertEqual(len(receipt["defaultEvaluations"]), 36)
        self.assertEqual(len(receipt["collectingEvaluations"]), 36)
        self.assertEqual(len(receipt["currentOwnerEvaluations"]), 7)
        for context in ("defaultEvaluations", "collectingEvaluations", "currentOwnerEvaluations"):
            for item in receipt[context]:
                named = item["Identity"] in (*PLATFORM_CONSUMERS, *CURRENT_OWNER_CONSUMERS)
                with self.subTest(context=context, project=item["Identity"]):
                    self.assertEqual(item["Sdk"], "4.2.0" if named or context == "collectingEvaluations" else "5.1.0")
                    self.assertEqual(item["CatalogTenantsDefault"], "7.0.7" if named else "8.0.0")
                    self.assertEqual(dict(pin.split("=", 1) for pin in item["TenantsPins"].split("|")),
                        {name: "7.0.7" if named else "8.0.0" for name in TENANTS_PACKAGES})
                    self.assertEqual(item["CatalogMemoriesDefault"], "3.0.27" if named else "4.0.0")
                    self.assertEqual(item["CatalogPartiesDefault"], "3.1.1")
                    self.assertEqual(item["CatalogMcpDefault"], "2.0.1")
        for index, version in ((0, "8.0.0"), (-1, "7.0.7")):
            observed = copy.deepcopy(receipt["currentOwnerEvaluations"])
            observed[index]["CatalogTenantsDefault"] = version
            with self.subTest(index=index), self.assertRaisesRegex(ValueError, "unqualified current-owner"):
                verify_current_owner_observations(observed, generic_tenants="8.0.0")
        self._assert_tenants_family_refusals(builds603_catalog_body(), binding, receipt)
        sdk51 = evaluate_views(builds603_catalog_body(), "5.1.0",
            consumer_names=(*CURRENT_OWNER_CONSUMERS, *UNKNOWN_OWNER_CONSUMERS))
        for item in sdk51:
            self.assertEqual(item["Sdk"], "5.1.0")
            self.assertEqual(item["CatalogTenantsDefault"], "8.0.0")
            self.assertEqual(dict(pin.split("=", 1) for pin in item["TenantsPins"].split("|")),
                {name: "8.0.0" for name in TENANTS_PACKAGES})
            self.assertEqual(item["CatalogMemoriesDefault"], "4.0.0")
        self.assertFalse(receipt["candidateAcceptance"])
        self.assertFalse(receipt["runtimeAcceptance"])

    def test_builds603_requires_its_own_exact_source_catalog_and_publication(self):
        from current_catalog_view import catalog_profile
        for fault in ("previous", "foreign-source", "foreign-blob", "foreign-hash", "next-patch", "future"):
            binding = generation6_binding("6.0.3")
            if fault in ("previous", "foreign-source"):
                source = GENERATION602_CATALOG["sourceRevision"] if fault == "previous" else "e" * 40
                binding["producer"]["sourceRevision"] = binding["publication"]["sourceRevision"] = source
            elif fault == "foreign-blob": binding["producer"]["catalog"]["blob"] = "e" * 40
            elif fault == "foreign-hash": binding["producer"]["catalog"]["sha256"] = binding["publication"]["catalogSha256"] = "e" * 64
            else:
                version = "6.0.4" if fault == "next-patch" else "7.0.0"
                binding["publication"].update(version=version, releaseTag="v" + version)
            manifest = json.loads(binding["publication"]["manifestJson"])
            manifest.update(sha=binding["publication"]["sourceRevision"], version=binding["publication"]["version"])
            for item in manifest["packages"]:
                item["path"] = "packages/" + item["id"] + "." + manifest["version"] + ".nupkg"
            encoded = json.dumps(manifest)
            binding["publication"].update(manifestJson=encoded, manifestSha256=hashlib.sha256(encoded.encode()).hexdigest())
            with self.subTest(fault=fault), patch("current_catalog_view.evaluate_views") as evaluate:
                with self.assertRaisesRegex(ValueError, "exact qualified Builds6|exact supported"):
                    catalog_profile(binding)
                evaluate.assert_not_called()
        with self.assertRaisesRegex(ValueError, "exact producer binding"):
            self.verify(builds602_catalog_body(), generation6_binding("6.0.3"))
        with self.assertRaisesRegex(ValueError, "exact producer binding"):
            self.verify(builds603_catalog_body(), generation6_binding("6.0.2"))

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

    def test_generation601_requires_its_own_exact_source_catalog_and_binding(self):
        raw = current_platform_catalog_body()
        receipt = self.verify(raw, generation6_binding("6.0.1"))
        self.assertEqual({item["Sdk"] for item in receipt["evaluations"] if item["Identity"] in PLATFORM_CONSUMERS}, {"4.2.0"})
        self.assertEqual({item["Sdk"] for item in receipt["evaluations"] if item["Identity"] in UNKNOWN_CONSUMERS}, {"5.1.0"})
        for fault in ("missing", "previous-source", "foreign-blob", "foreign-hash", "future-patch"):
            binding = generation6_binding("6.0.1")
            if fault == "missing": binding = None
            elif fault == "previous-source": binding = generation6_binding()
            elif fault == "foreign-blob": binding["producer"]["catalog"]["blob"] = "e" * 40
            elif fault == "foreign-hash": binding["producer"]["catalog"]["sha256"] = binding["publication"]["catalogSha256"] = "e" * 64
            else:
                binding["publication"]["version"] = "6.0.2"
                binding["publication"]["releaseTag"] = "v6.0.2"
                manifest = json.loads(binding["publication"]["manifestJson"])
                manifest["version"] = "6.0.2"
                for item in manifest["packages"]: item["path"] = item["path"].replace("6.0.1", "6.0.2")
                encoded = json.dumps(manifest)
                binding["publication"].update(manifestJson=encoded, manifestSha256=hashlib.sha256(encoded.encode()).hexdigest())
            with self.subTest(fault=fault), self.assertRaisesRegex(ValueError, "exact qualified Builds6|exact producer binding|unqualified SDK/OpenAPI"):
                self.verify(raw, binding)

    def test_generation602_requires_its_own_exact_publication_source_and_catalog(self):
        raw = builds602_catalog_body()
        receipt = self.verify(raw, generation6_binding("6.0.2"))
        self.assertEqual(receipt["catalogSha256"], GENERATION602_CATALOG["sha256"])
        for context in ("defaultEvaluations", "collectingEvaluations"):
            for item in receipt[context]:
                named = item["Identity"] in PLATFORM_CONSUMERS
                with self.subTest(context=context, project=item["Identity"]):
                    self.assertEqual(item["Sdk"], "4.2.0" if named or context == "collectingEvaluations" else "5.1.0")
                    self.assertEqual(item["CatalogMemoriesDefault"], "3.0.27" if named else "4.0.0")
                    self.assertEqual(dict(pin.split("=", 1) for pin in item["MemoriesPins"].split("|")),
                        {name: "3.0.27" if named else "4.0.0" for name in MEMORIES_PACKAGES})
        self.assertFalse(receipt["runtimeAcceptance"])
        self.assertFalse(receipt["candidateAcceptance"])
        for fault in ("missing", "previous-source", "foreign-blob", "foreign-hash", "future-patch"):
            binding = generation6_binding("6.0.2")
            if fault == "missing": binding = None
            elif fault == "previous-source": binding = generation6_binding("6.0.1")
            elif fault == "foreign-blob": binding["producer"]["catalog"]["blob"] = "e" * 40
            elif fault == "foreign-hash": binding["producer"]["catalog"]["sha256"] = binding["publication"]["catalogSha256"] = "e" * 64
            else:
                binding["publication"]["version"] = "6.0.3"
                binding["publication"]["releaseTag"] = "v6.0.3"
                manifest = json.loads(binding["publication"]["manifestJson"])
                manifest["version"] = "6.0.3"
                for item in manifest["packages"]: item["path"] = item["path"].replace("6.0.2", "6.0.3")
                encoded = json.dumps(manifest)
                binding["publication"].update(manifestJson=encoded, manifestSha256=hashlib.sha256(encoded.encode()).hexdigest())
            if fault == "missing":
                with self.subTest(fault=fault), self.assertRaisesRegex(ValueError, "unqualified SDK/OpenAPI"):
                    self.verify(raw, binding)
            else:
                with self.subTest(fault=fault), patch("current_catalog_view.evaluate_views") as evaluate:
                    with self.assertRaisesRegex(ValueError, "exact qualified Builds6|exact producer binding"):
                        self.verify(raw, binding)
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
