#!/usr/bin/env python3
"""Measure official signed SDK archives after the normal exact-source owner release."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--owner", choices=["EventStore", "Builds"], required=True)
parser.add_argument("--version", required=True)
parser.add_argument("--source", required=True)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
if args.output.exists():
    raise ValueError("Retain prior measurements; choose a fresh receipt path")
repository = "Hexalith/Hexalith." + args.owner
release = json.loads(subprocess.check_output(["gh", "api", f"repos/{repository}/releases/tags/v{args.version}"], text=True))
tag = json.loads(subprocess.check_output(["gh", "api", f"repos/{repository}/commits/v{args.version}"], text=True))
if tag["sha"] != args.source or release["draft"] or release["prerelease"]:
    raise ValueError("The actual stable release tag differs from the approved exact source")
work = Path(tempfile.mkdtemp(prefix="story32-released-sdk-"))
result = {"schema": "story32-released-sdk-measurement.v1", "recordedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
          "repository": repository, "source": args.source, "version": args.version, "releaseUrl": release["html_url"],
          "releasePublishedUtc": release["published_at"], "work": str(work), "packages": [], "qualificationCredit": False}
ids = ["Hexalith.EventStore." + name for name in ["Admin.Abstractions", "Contracts", "ServiceDefaults", "Client", "DomainService", "Server", "Gateway", "Aspire"]] \
    if args.owner == "EventStore" else ["Hexalith.Builds.Module.Cli"]
for name in ids:
    lower = name.lower()
    url = f"https://api.nuget.org/v3-flatcontainer/{lower}/{args.version}/{lower}.{args.version}.nupkg"
    archive = work / (name + ".nupkg")
    with urllib.request.urlopen(url, timeout=60) as response:
        archive.write_bytes(response.read(100_000_001))
    if archive.stat().st_size > 100_000_000:
        raise ValueError("Archive exceeds its finite inspection bound")
    verification = subprocess.run(["dotnet", "nuget", "verify", str(archive), "--all"], capture_output=True, text=True, timeout=120, check=True)
    entry = {"id": name, "version": args.version, "url": url, "archiveSha256": hashlib.sha256(archive.read_bytes()).hexdigest(),
             "signatureVerification": verification.stdout, "bodies": {}, "api": []}
    with zipfile.ZipFile(archive) as package:
        specification = next(path for path in package.namelist() if path.endswith(".nuspec"))
        metadata = ET.fromstring(package.read(specification))
        identity = next((node.attrib for node in metadata.iter() if node.tag.endswith("repository")), {})
        if identity.get("commit") != args.source:
            raise ValueError(name + " package repository source differs from its approved release")
        entry["repository"] = identity
        if ".signature.p7s" not in package.namelist():
            raise ValueError(name + " official archive is unsigned")
        prefix = "tools/net10.0/any/" if args.owner == "Builds" else "lib/net10.0/"
        for path in package.namelist():
            if path.startswith(prefix) and path.endswith(".dll"):
                data = package.read(path)
                entry["bodies"][path] = hashlib.sha256(data).hexdigest()
        library = "tools/net10.0/any/Hexalith.Builds.Tooling.dll" if args.owner == "Builds" else "lib/net10.0/" + name + ".dll"
        selected = work / name / Path(library).name
        selected.parent.mkdir()
        selected.write_bytes(package.read(library))
    types = {"Hexalith.EventStore.DomainService": ["Hexalith.EventStore.DomainService.EventStoreExtensionApi", "Hexalith.EventStore.DomainService.EventStoreExtensionAttribute"],
             "Hexalith.EventStore.Server": ["Hexalith.EventStore.Server.Extensions.EventStoreExtensionDeclaration", "Hexalith.EventStore.Server.Extensions.EventStoreExtensionServiceCollectionExtensions"],
             "Hexalith.Builds.Module.Cli": ["Hexalith.Builds.Tooling.Runtime.CompositionDeclarationOptions"]}.get(name, [])
    for typename in types:
        command = ["dnx", "dotnet-inspect", "-y", "--", "member", typename, "--library", str(selected), "--json"]
        probe = subprocess.run(command, capture_output=True, text=True, timeout=120, check=True)
        data = json.loads(probe.stdout)
        entry["api"].append({"type": typename, "command": command, "exitCode": probe.returncode, "result": data})
    result["packages"].append(entry)
    print(name, entry["archiveSha256"], flush=True)
args.output.write_text(json.dumps(result, indent=2) + "\n")
print(args.output)
