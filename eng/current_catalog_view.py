"""Evaluate the finite current and retained Platform views of an authenticated catalog."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET


PLATFORM_CONSUMERS = (
    "Hexalith.Platform.CatalogBuilder", "Hexalith.Platform.Composition", "Hexalith.Platform.Composition.Tests",
    "Hexalith.Platform.Custody", "Hexalith.Platform.Custody.Component", "Hexalith.Platform.Custody.Component.Tests",
    "Hexalith.Platform.Custody.Tests", "Hexalith.Platform.EventStoreHost", "Hexalith.Platform.Identity",
    "Hexalith.Platform.Identity.Tests", "Hexalith.Platform.OwnerServices.Tests", "Hexalith.Platform.Protection",
    "Hexalith.Platform.Protection.Qualification.Enrollment", "Hexalith.Platform.Protection.Qualification.Executor",
    "Hexalith.Platform.Protection.Qualification.Observer", "Hexalith.Platform.Protection.Tests",
    "Hexalith.Platform.Secrets.ArchiveProcessFixture", "Hexalith.Platform.Secrets.Authority",
    "Hexalith.Platform.Secrets.Authority.Tests", "Hexalith.Platform.Secrets.Custody",
    "Hexalith.Platform.Secrets.Custody.Tests", "Hexalith.Platform.Secrets.Inventory",
    "Hexalith.Platform.Secrets.Inventory.Tests", "Hexalith.Platform.Secrets.Qualification.Enrollment",
    "Hexalith.Platform.Secrets.Qualification.Executor", "Hexalith.Platform.Secrets.Qualification.Observer",
    "Hexalith.Platform.Secrets.Runtime", "Hexalith.Platform.Secrets.Runtime.Tests",
    "Hexalith.Platform.TenantRegistry.Host", "Hexalith.Platform.TestExtension",
    "Hexalith.Platform.TestExtension.Qualification.Tests", "Hexalith.Platform.Works.Runtime",
    "Hexalith.Platform.Works.Tests",
)
UNKNOWN_CONSUMERS = ("Unqualified.Consumer", "Hexalith.Platform.Unqualified", "Hexalith.Platform.Composition.Future")
CURRENT_OWNER_CONSUMERS = (
    "Hexalith.Parties", "Hexalith.Parties.Authentication", "Hexalith.Parties.Contracts",
    "Hexalith.Parties.EventStoreExtensions", "Hexalith.Parties.Projections", "Hexalith.Parties.Security",
)
UNKNOWN_OWNER_CONSUMERS = ("Hexalith.Parties.Unqualified",)
SDK_PACKAGES = tuple("Hexalith.EventStore." + name for name in (
    "Admin.Abstractions", "Admin.Server", "Aspire", "Client", "Contracts", "DomainService", "Gateway",
    "RestApi.Generators", "Server", "ServiceDefaults", "SignalR", "Testing", "Testing.Integration"))
OPENAPI_PACKAGES = ("Microsoft.AspNetCore.OpenApi", "Microsoft.OpenApi")
GENERATION6_CATALOG = {
    "version": "6.0.0",
    "sourceRevision": "1ebc5b84ff2c8afd21a87d80ef3d9c9048eaf5e5",
    "blob": "cbfc6a888f269010600103a33c73d0cfc0af93a2",
    "sha256": "45b05f6755f337baccfaa805e5e4afb4a57e91b2f70226a357c82d03b4c19bb7",
}

GENERATION6_SDK42_CATALOG = {
    "version": "6.0.2",
    "sourceRevision": "60ce7967607dc8b703fff11f4fc13cabb09c090a",
    "blob": "86e9b0d6fd49551d80047d92f3aaed93b91ca5a3",
    "sha256": "1291e32272127e2409e337fb6a974bc4c5f76a2907da0ba0d5528cc5016fe44e",
}


def catalog_profile(binding=None) -> str:
    """A new producer generation cannot inherit an earlier consumer qualification."""
    if binding is None:
        return "retained-sdk42"
    from builds_tuple import validate_binding
    validate_binding(binding)
    if binding["publication"]["version"].startswith("6."):
        producer = binding["producer"]
        for catalog, profile in ((GENERATION6_CATALOG, "current-sdk51"),
                                 (GENERATION6_SDK42_CATALOG, "current-sdk42")):
            if binding["publication"]["version"] == catalog["version"] \
                    and producer["sourceRevision"] == catalog["sourceRevision"] \
                    and all(producer["catalog"][key] == catalog[key] for key in ("blob", "sha256")):
                return profile
        raise ValueError("The current catalog view requires an exact qualified Builds6 producer and catalog.")
    return "retained-sdk42"


def require_catalog_body(raw: bytes, expected_sha256: str) -> None:
    if not isinstance(expected_sha256, str) or re.fullmatch(r"[0-9a-f]{64}", expected_sha256) is None \
            or hashlib.sha256(raw).hexdigest() != expected_sha256:
        raise ValueError("The current catalog view requires the exact authenticated catalog Git body and SHA256.")


def require_finite_selector(document: ET.Element) -> None:
    selectors = document.findall(".//HexalithPlatformSdk42CatalogView")
    if len(selectors) != 2 or selectors[0].attrib or selectors[0].text != "false" \
            or set(selectors[1].attrib) != {"Condition"} or selectors[1].text != "true":
        raise ValueError("The current Platform catalog view requires its finite false/default and exact named selector.")
    condition = selectors[1].attrib["Condition"]
    pattern = r"\$\(MSBuildProjectName\.Equals\('([^']+)'\)\)"
    names = re.findall(pattern, condition)
    expression = re.sub(pattern, "NAME", condition).strip()
    if len(names) != len(PLATFORM_CONSUMERS) or set(names) != set(PLATFORM_CONSUMERS) \
            or re.fullmatch(r"NAME(?:\s+or\s+NAME)*", expression) is None:
        raise ValueError("The current Platform catalog view must contain exactly the 33 approved consumer names.")


def evaluate_views(raw: bytes, sdk_version: str | None = None, *, consumer_names=None) -> list[dict]:
    """Use real project identities and a fresh process without inherited catalog or SDK overrides."""
    with tempfile.TemporaryDirectory(prefix="platform-catalog-view-") as temporary:
        root = Path(temporary)
        (root / "catalog.props").write_bytes(raw)
        paths = []
        names = (*PLATFORM_CONSUMERS, *UNKNOWN_CONSUMERS) if consumer_names is None else tuple(consumer_names)
        for name in names:
            project = ET.Element("Project")
            properties = ET.SubElement(project, "PropertyGroup")
            for key, value in (("TargetFramework", "net11.0"), ("Configuration", "Release"),
                               ("UseHexalithProjectReferences", "false")):
                ET.SubElement(properties, key).text = value
            ET.SubElement(project, "Import", Project=str(root / "catalog.props"))
            target = ET.SubElement(project, "Target", Name="Observe", Returns="@(_Observed)")
            items = ET.SubElement(target, "ItemGroup")
            condition = " or ".join("'%(PackageVersion.Identity)' == '" + package + "'" for package in OPENAPI_PACKAGES)
            ET.SubElement(items, "_Pins", Include="@(PackageVersion)", Condition=condition
                          + " or $([System.String]::Copy('%(PackageVersion.Identity)').StartsWith('Hexalith.EventStore.'))")
            observation = ET.SubElement(items, "_Observed", Include="$(MSBuildProjectName)")
            for key, value in (("Sdk", "$(HexalithEventStoreVersion)"),
                               ("CatalogMemoriesDefault", "$(HexalithMemoriesVersion)"),
                               ("CatalogTenantsDefault", "$(HexalithTenantsVersion)"),
                               ("CatalogPartiesDefault", "$(HexalithPartiesVersion)"),
                               ("CatalogMcpDefault", "$(HexalithMcpCliVersion)"),
                               ("PlatformView", "$(HexalithPlatformSdk42CatalogView)"),
                               ("PartiesView", "$(HexalithPartiesSdk42CatalogView)"),
                               ("Pins", "@(_Pins->'%(Identity)=%(Version)', '|')")):
                ET.SubElement(observation, key).text = value
            path = root / (name + ".proj")
            ET.ElementTree(project).write(path, encoding="utf-8", xml_declaration=True)
            paths.append(str(path))
        driver = ET.Element("Project")
        target = ET.SubElement(driver, "Target", Name="ObserveAll", Returns="@(_Collected)")
        task = ET.SubElement(target, "MSBuild", Projects=";".join(paths), Targets="Observe", BuildInParallel="false")
        if sdk_version is not None:
            task.set("Properties", "HexalithEventStoreVersion=" + sdk_version)
        ET.SubElement(task, "Output", TaskParameter="TargetOutputs", ItemName="_Collected")
        driver_path = root / "Observe.proj"
        ET.ElementTree(driver).write(driver_path, encoding="utf-8", xml_declaration=True)
        allowed = {"PATH", "PATHEXT", "HOME", "USERPROFILE", "TMP", "TEMP", "TMPDIR", "SystemRoot",
                   "SystemDrive", "WINDIR", "COMSPEC", "APPDATA", "LOCALAPPDATA", "ProgramFiles",
                   "ProgramFiles(x86)", "ProgramW6432", "DOTNET_ROOT", "DOTNET_ROOT_X64", "DOTNET_ROOT_X86",
                   "LD_LIBRARY_PATH", "DYLD_LIBRARY_PATH", "LANG", "LC_ALL", "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"}
        environment = {key: value for key, value in os.environ.items() if key in allowed}
        result = subprocess.run(["dotnet", "msbuild", str(driver_path), "-nologo", "-nodeReuse:false",
                                 "-target:ObserveAll", "-getTargetResult:ObserveAll"], cwd=root,
                                env=environment, capture_output=True, text=True, timeout=90)
        if result.returncode:
            raise ValueError("Independent current catalog evaluation failed: " + result.stdout + result.stderr)
        return json.loads(result.stdout)["TargetResults"]["ObserveAll"]["Items"]


def verify_observations(observed: list[dict], named_sdk: str, other_sdk: str) -> None:
    """Check every package and both OpenAPI members in one exact independent evaluation context."""
    names = [item["Identity"] for item in observed]
    if len(names) != len(PLATFORM_CONSUMERS) + len(UNKNOWN_CONSUMERS) \
            or len(set(names)) != len(names) or set(names) != set(PLATFORM_CONSUMERS) | set(UNKNOWN_CONSUMERS):
        raise ValueError("Independent catalog evaluation did not return the exact Platform and negative consumer set.")
    for item in observed:
        current = item["Identity"] in PLATFORM_CONSUMERS
        expected_sdk = named_sdk if current else other_sdk
        expected = {package: expected_sdk for package in SDK_PACKAGES}
        expected.update(dict(zip(OPENAPI_PACKAGES, ("10.0.15", "2.13.0")
            if current and expected_sdk == "4.2.0" else ("11.0.0", "3.11.0"))))
        pins = [pin.split("=", 1) for pin in item.get("Pins", "").split("|") if pin]
        if item.get("Sdk") != expected_sdk or item.get("PlatformView") != str(current).lower() \
                or item.get("PartiesView") != "false" or len(pins) != len(expected) or dict(pins) != expected:
            raise ValueError("The authenticated catalog selects an unqualified SDK/OpenAPI consumer view: " + item["Identity"])


def verify_current_owner_observations(observed: list[dict]) -> None:
    """Require each actual source-owner context's complete family and an unknown sibling refusal."""
    names = [item["Identity"] for item in observed]
    if len(names) != len(CURRENT_OWNER_CONSUMERS) + len(UNKNOWN_OWNER_CONSUMERS) \
            or len(set(names)) != len(names) or set(names) != set(CURRENT_OWNER_CONSUMERS) | set(UNKNOWN_OWNER_CONSUMERS):
        raise ValueError("Current-owner catalog evaluation differs from its exact source project contexts.")
    for item in observed:
        named = item["Identity"] in CURRENT_OWNER_CONSUMERS
        sdk = "4.2.0" if named else "5.1.0"
        expected = {package: sdk for package in SDK_PACKAGES}
        expected.update(dict(zip(OPENAPI_PACKAGES, ("10.0.15", "2.13.0") if named else ("11.0.0", "3.11.0"))))
        pins = [pin.split("=", 1) for pin in item.get("Pins", "").split("|") if pin]
        if item.get("Sdk") != sdk or item.get("PlatformView") != "false" \
                or item.get("PartiesView") != str(named).lower() \
                or item.get("CatalogMemoriesDefault") != ("3.0.27" if named else "4.0.0") \
                or any(item.get(key) != value for key, value in (("CatalogTenantsDefault", "7.0.7"),
                    ("CatalogPartiesDefault", "3.1.1"), ("CatalogMcpDefault", "2.0.1"))) \
                or len(pins) != len(expected) or dict(pins) != expected:
            raise ValueError("The authenticated catalog selects an unqualified current-owner package view: " + item["Identity"])


def verify_named_platform_catalog(raw: bytes, expected_sha256: str, *, binding=None) -> dict:
    require_catalog_body(raw, expected_sha256)
    profile = catalog_profile(binding)
    if binding is not None and binding["producer"]["catalog"]["sha256"] != expected_sha256:
        raise ValueError("The current catalog view differs from its exact producer binding.")
    require_finite_selector(ET.fromstring(raw))
    observed = evaluate_views(raw)
    names = [item["Identity"] for item in observed]
    verify_observations(observed, "5.1.0" if profile == "current-sdk51" else "4.2.0",
                        "5.1.0" if profile in ("current-sdk51", "current-sdk42") else "5.0.0")
    collecting = evaluate_views(raw, "4.2.0")
    if [item["Identity"] for item in collecting] != names:
        raise ValueError("The retained collecting view changed its exact consumer set.")
    verify_observations(collecting, "4.2.0", "4.2.0")
    if profile == "current-sdk42":
        for item in (*observed, *collecting):
            expected = "3.0.27" if item["Identity"] in PLATFORM_CONSUMERS else "4.0.0"
            if item.get("CatalogMemoriesDefault") != expected:
                raise ValueError("The authenticated catalog selects an unqualified named/default Memories view.")
    owner_observed = []
    if profile == "current-sdk42":
        owner_observed = evaluate_views(raw, consumer_names=(*CURRENT_OWNER_CONSUMERS, *UNKNOWN_OWNER_CONSUMERS))
        verify_current_owner_observations(owner_observed)
    kind = {"current-sdk51": "finite-current-platform-sdk51-with-retained-sdk42-catalog-view",
            "current-sdk42": "finite-current-platform-sdk42-with-sdk51-default-catalog-view"}.get(
                profile, "finite-current-platform-sdk42-catalog-view")
    scope = {"current-sdk51": "Exact Builds6 current SDK5.1 view and explicit retained SDK4.2 collecting view",
             "current-sdk42": "Exact Builds6.0.2 named Platform and current-owner SDK4.2/Memories3.0.27 views; generic SDK5.1/Memories4 defaults"}.get(
                 profile, "SDK4.2 Platform package view only; default SDK5 observations are negative controls")
    return {"kind": kind, "catalogSha256": expected_sha256,
            "consumers": list(PLATFORM_CONSUMERS), "evaluations": observed, "defaultEvaluations": observed,
            "collectingEvaluations": collecting, "currentOwnerEvaluations": owner_observed,
            "scope": scope,
            "candidateAcceptance": False, "runtimeAcceptance": False}
