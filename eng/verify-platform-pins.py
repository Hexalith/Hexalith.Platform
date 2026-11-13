#!/usr/bin/env python3
"""Refuse file-AppHost pin drift from the Builds version authority."""
import argparse
import pathlib
import re
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument("--root", type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[1])
parser.add_argument("--catalog", type=pathlib.Path)
parser.add_argument("--catalog-sha256")
parser.add_argument("--hosting-helper")
parser.add_argument("--spine", type=pathlib.Path)
arguments = parser.parse_args()
arguments.root = arguments.root.resolve()
catalog = arguments.catalog
if catalog is None:
    sibling = arguments.root.parent / "Hexalith.Builds/Props/Directory.Packages.props"
    catalog = sibling if sibling.is_file() else arguments.root / "references/Hexalith.Builds/Props/Directory.Packages.props"
catalog_body = catalog.read_bytes()
properties = ET.fromstring(catalog_body)
values = {item.tag: item.text for group in properties.findall("PropertyGroup") for item in group if item.text}
if properties.findall(".//HexalithPlatformSdk42CatalogView"):
    from current_catalog_view import verify_named_platform_catalog
    try:
        view = verify_named_platform_catalog(catalog_body, arguments.catalog_sha256)
    except ValueError as failure:
        raise SystemExit(str(failure)) from failure
    composition_view = next(item for item in view["evaluations"] if item["Identity"] == "Hexalith.Platform.Composition")
    values["HexalithEventStoreVersion"] = composition_view["Sdk"]
selected = {
    "Aspire.AppHost.Sdk": values["HexalithAspireAppHostSdkVersion"],
    "Hexalith.EventStore.Aspire": values["HexalithEventStoreVersion"],
    "Hexalith.EventStore.ServiceDefaults": values["HexalithEventStoreVersion"],
    "CommunityToolkit.Aspire.Hosting.Dapr": values["HexalithAspireHostingDaprVersion"],
}
# The Dapr catalog has a consumer-specific override; the file AppHost uses the default selection.
for group in properties.findall("PropertyGroup"):
    for item in group:
        if item.tag == "HexalithAspireHostingDaprVersion" and not item.get("Condition"):
            selected["CommunityToolkit.Aspire.Hosting.Dapr"] = item.text
canonical = ("Hexalith.EventStore.Aspire", "Hexalith.EventStore.ServiceDefaults", "CommunityToolkit.Aspire.Hosting.Dapr")
spine = arguments.spine or arguments.root / "_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md"
record = spine.read_text() if spine.is_file() else ""
decision = re.search(r"(?ms)^### AD-9\b[^\n]*\[(ADOPTED(?:,[^\]]*)?)\][^\n]*\n(.*?)(?=^### |\Z)", record)
hosting = re.search(r"(?m)^\s*- \*Canonical hosting:\* ([^\n]+)", decision.group(2)) if decision else None
if not hosting or any(f"`{package}`" not in hosting.group(1) for package in canonical):
    raise SystemExit(f"{spine}: AD-9: canonical hosting package set is not recorded; new hosting helpers are refused.")
if arguments.hosting_helper and arguments.hosting_helper not in canonical:
    raise SystemExit(f"{arguments.hosting_helper}: helper is outside the recorded canonical hosting package set; record its ownership in AD-9 before admission.")
text = (arguments.root / "apphost.cs").read_text()
# File-host compilation must retain the catalog floor exposed by the mapped model.
reference_closure = ("Microsoft.Extensions.Hosting", "Microsoft.Extensions.Http", "System.Text.Json")
# Explicit vendor integration directives share the same exact catalog authority.
for package in reference_closure + ("Aspire.Hosting.Docker", "Aspire.Hosting.Kubernetes", "Aspire.Hosting.Redis", "Aspire.Hosting.Keycloak"):
    if package in reference_closure or re.search(r"^#:package " + re.escape(package) + r"@", text, re.MULTILINE):
        entries = properties.findall(".//PackageVersion[@Include='" + package + "']")
        if len(entries) != 1 or not entries[0].get("Version"):
            raise SystemExit(f"{package}: the file AppHost requires one exact catalog entry.")
        selected[package] = entries[0].get("Version")
composition = arguments.root / "src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj"
mapped = None
if re.search(r"^#:project src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj$", text, re.MULTILINE) and composition.is_file():
    mapped = ET.parse(composition).getroot()
for package, expected in selected.items():
    match = re.search(r"^#:(?:sdk|package) " + re.escape(package) + r"@([^\s]+)$", text, re.MULTILINE)
    observed = match.group(1) if match else "<missing>"
    if observed == "<missing>" and package in canonical[:2] and mapped is not None:
        packages = mapped.findall(".//PackageReference[@Include='" + package + "']")
        projects = mapped.findall(".//ProjectReference[@Include='$(HexalithEventStoreRoot)/src/" + package + "/" + package + ".csproj']")
        if len(packages) == 1 and len(projects) == 1 and not packages[0].get("Version") and not packages[0].get("VersionOverride"):
            observed = expected
    if observed != expected:
        raise SystemExit(f"{package}: apphost.cs selects {observed}; Builds catalog selects {expected}.")
print("Platform file AppHost SDK, mapped EventStore hosting references and Dapr pins match Builds.")
