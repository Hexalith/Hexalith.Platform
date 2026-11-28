"""Evaluate the unified Platform catalog read from the Builds gitlink."""
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
MEMORIES_PACKAGES = tuple("Hexalith.Memories." + name for name in (
    "Aspire", "Client.Rest", "Contracts", "EventStore", "EventStoreExtensions"))
TENANTS_PACKAGES = tuple("Hexalith.Tenants." + name for name in (
    "Aspire", "Client", "Contracts", "Server", "Testing"))
MCP_PACKAGES = ("Hexalith.McpCli.Abstractions",)


def require_catalog_body(raw: bytes, expected_sha256: str) -> None:
    if not isinstance(expected_sha256, str) or re.fullmatch(r"[0-9a-f]{64}", expected_sha256) is None \
            or hashlib.sha256(raw).hexdigest() != expected_sha256:
        raise ValueError("The current catalog view requires the exact authenticated catalog Git body and SHA256.")


def evaluate_views(raw: bytes, *, consumer_names=None) -> list[dict]:
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
            ET.SubElement(items, "_Pins", Include="@(PackageVersion)",
                Condition="$([System.String]::Copy('%(PackageVersion.Identity)').StartsWith('Hexalith.EventStore.'))")
            ET.SubElement(items, "_MemoriesPins", Include="@(PackageVersion)", Condition=" or ".join(
                "'%(PackageVersion.Identity)' == '" + package + "'" for package in MEMORIES_PACKAGES))
            ET.SubElement(items, "_TenantsPins", Include="@(PackageVersion)",
                Condition="$([System.String]::Copy('%(PackageVersion.Identity)').StartsWith('Hexalith.Tenants.'))")
            ET.SubElement(items, "_McpPins", Include="@(PackageVersion)", Condition=" or ".join(
                "'%(PackageVersion.Identity)' == '" + package + "'" for package in MCP_PACKAGES))
            observation = ET.SubElement(items, "_Observed", Include="$(MSBuildProjectName)")
            for key, value in (("Sdk", "$(HexalithEventStoreVersion)"),
                               ("CatalogMemoriesDefault", "$(HexalithMemoriesVersion)"),
                               ("CatalogTenantsDefault", "$(HexalithTenantsVersion)"),
                               ("CatalogPartiesDefault", "$(HexalithPartiesVersion)"),
                               ("CatalogMcpDefault", "$(HexalithMcpCliVersion)"),
                               ("MemoriesPins", "@(_MemoriesPins->'%(Identity)=%(Version)', '|')"),
                               ("TenantsPins", "@(_TenantsPins->'%(Identity)=%(Version)', '|')"),
                               ("McpPins", "@(_McpPins->'%(Identity)=%(Version)', '|')"),
                               ("Pins", "@(_Pins->'%(Identity)=%(Version)', '|')")):
                ET.SubElement(observation, key).text = value
            path = root / (name + ".proj")
            ET.ElementTree(project).write(path, encoding="utf-8", xml_declaration=True)
            paths.append(str(path))
        driver = ET.Element("Project")
        target = ET.SubElement(driver, "Target", Name="ObserveAll", Returns="@(_Collected)")
        task = ET.SubElement(target, "MSBuild", Projects=";".join(paths), Targets="Observe", BuildInParallel="false")
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


def catalog_cohort(raw):
    """Read each owner's single release selection independently from the Builds catalog."""
    root = ET.fromstring(raw)
    result = {}
    for owner in ("EventStore", "Memories", "Tenants", "Parties", "McpCli"):
        key = "Hexalith" + owner + "Version"
        entries = root.findall("PropertyGroup/" + key)
        if len(entries) != 1 or not re.fullmatch(r"[0-9]+(?:\.[0-9]+){2,3}", entries[0].text or ""):
            raise ValueError(f"Expected one stable Builds catalog selection for {key}")
        result[key] = entries[0].text
    return result


def verify_unified_catalog(raw, expected_sha256):
    require_catalog_body(raw, expected_sha256)
    cohort = catalog_cohort(raw)
    names = (*PLATFORM_CONSUMERS, *UNKNOWN_CONSUMERS, *CURRENT_OWNER_CONSUMERS, *UNKNOWN_OWNER_CONSUMERS)
    observed = evaluate_views(raw, consumer_names=names)
    if sorted(item["Identity"] for item in observed) != sorted(names):
        raise ValueError("Missing or duplicate consumer views in the Builds catalog")
    for item in observed:
        for prop, field, pin_field, packages in (
                ("EventStore", "Sdk", "Pins", SDK_PACKAGES), ("Memories", "CatalogMemoriesDefault", "MemoriesPins", MEMORIES_PACKAGES),
                ("Tenants", "CatalogTenantsDefault", "TenantsPins", TENANTS_PACKAGES), ("McpCli", "CatalogMcpDefault", "McpPins", MCP_PACKAGES)):
            expected = cohort["Hexalith" + prop + "Version"]
            pins = [pin.split("=", 1) for pin in item[pin_field].split("|") if pin]
            actual = sorted((name, version) for name, version in pins if name.startswith("Hexalith."))
            if actual != sorted((name, expected) for name in packages) or item[field] != expected:
                raise ValueError(f"{item['Identity']} selects a different {prop} package family from its catalog")
        if item["CatalogPartiesDefault"] != cohort["HexalithPartiesVersion"]:
            raise ValueError(f"{item['Identity']} selects a different Parties release from its catalog")
    return {"kind": "unified-package-catalog", "catalogSha256": expected_sha256, "properties": cohort,
            "consumers": list(names), "runtimeAcceptance": False}
