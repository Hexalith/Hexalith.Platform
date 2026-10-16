#!/usr/bin/env python3
"""Refuse file-AppHost pin drift from the Builds version authority."""
import argparse
import pathlib
import re
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument("--root", type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[1])
parser.add_argument("--catalog", type=pathlib.Path)
parser.add_argument("--hosting-helper")
parser.add_argument("--spine", type=pathlib.Path)
arguments = parser.parse_args()
catalog = arguments.catalog or arguments.root / "references/Hexalith.Builds/Props/Directory.Packages.props"
properties = ET.parse(catalog).getroot()
values = {item.tag: item.text for group in properties.findall("PropertyGroup") for item in group if item.text}
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
for package, expected in selected.items():
    match = re.search(r"^#:(?:sdk|package) " + re.escape(package) + r"@([^\s]+)$", text, re.MULTILINE)
    observed = match.group(1) if match else "<missing>"
    if observed != expected:
        raise SystemExit(f"{package}: apphost.cs selects {observed}; Builds catalog selects {expected}.")
print("Platform file AppHost SDK, EventStore.Aspire and Dapr pins match Builds.")
