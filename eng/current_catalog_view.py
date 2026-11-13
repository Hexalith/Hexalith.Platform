"""Evaluate only the finite current Platform SDK4.2 view of an authenticated catalog."""
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
SDK_PACKAGES = tuple("Hexalith.EventStore." + name for name in (
    "Admin.Abstractions", "Admin.Server", "Aspire", "Client", "Contracts", "DomainService", "Gateway",
    "RestApi.Generators", "Server", "ServiceDefaults", "SignalR", "Testing", "Testing.Integration"))
OPENAPI_PACKAGES = ("Microsoft.AspNetCore.OpenApi", "Microsoft.OpenApi")


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


def evaluate_views(raw: bytes) -> list[dict]:
    """Use real project identities and a fresh process without inherited catalog or SDK overrides."""
    with tempfile.TemporaryDirectory(prefix="platform-catalog-view-") as temporary:
        root = Path(temporary)
        (root / "catalog.props").write_bytes(raw)
        paths = []
        for name in (*PLATFORM_CONSUMERS, *UNKNOWN_CONSUMERS):
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


def verify_named_platform_catalog(raw: bytes, expected_sha256: str) -> dict:
    require_catalog_body(raw, expected_sha256)
    require_finite_selector(ET.fromstring(raw))
    observed = evaluate_views(raw)
    names = [item["Identity"] for item in observed]
    if len(names) != len(PLATFORM_CONSUMERS) + len(UNKNOWN_CONSUMERS) \
            or len(set(names)) != len(names) or set(names) != set(PLATFORM_CONSUMERS) | set(UNKNOWN_CONSUMERS):
        raise ValueError("Independent catalog evaluation did not return the exact Platform and negative consumer set.")
    for item in observed:
        current = item["Identity"] in PLATFORM_CONSUMERS
        expected_sdk = "4.2.0" if current else "5.0.0"
        expected = {package: expected_sdk for package in SDK_PACKAGES}
        expected.update(dict(zip(OPENAPI_PACKAGES, ("10.0.15", "2.13.0") if current else ("11.0.0", "3.11.0"))))
        pins = [pin.split("=", 1) for pin in item.get("Pins", "").split("|") if pin]
        if item.get("Sdk") != expected_sdk or item.get("PlatformView") != str(current).lower() \
                or item.get("PartiesView") != "false" or len(pins) != len(expected) or dict(pins) != expected:
            raise ValueError("The authenticated catalog selects an unqualified SDK/OpenAPI consumer view: " + item["Identity"])
    return {"kind": "finite-current-platform-sdk42-catalog-view", "catalogSha256": expected_sha256,
            "consumers": list(PLATFORM_CONSUMERS), "evaluations": observed,
            "scope": "SDK4.2 Platform package view only; default SDK5 observations are negative controls",
            "candidateAcceptance": False, "runtimeAcceptance": False}
