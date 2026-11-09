#!/usr/bin/env python3
"""Inspect public archives without changing repository dependency selections."""
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

HERE = Path(__file__).resolve().parent
work = Path(tempfile.mkdtemp(prefix="story32-published-inspection-"))
receipt = {"observedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
           "work": str(work), "packages": [], "releases": {}, "qualificationCredit": False}


def fetch(url):
    return urllib.request.urlopen(url, timeout=60).read()


def inspect(path, typename):
    command = ["dnx", "dotnet-inspect", "-y", "--", "type", typename,
               "--library", str(path), "--json"]
    result = subprocess.run(command, capture_output=True, text=True, timeout=120)
    raw = path.parent / (path.stem + ".api.json")
    raw.write_text(result.stdout)
    methods = []
    if result.returncode == 0:
        data = json.loads(result.stdout)
        methods = [member["signature"] for member in data.get("members", [])
                   if member.get("name") == "CatalogPackageVersion"]
    return {"command": command, "exitCode": result.returncode,
            "typeFound": result.returncode == 0, "catalogPackageVersionSignatures": methods,
            "rawOutputPath": str(raw), "rawOutputSha256": hashlib.sha256(result.stdout.encode()).hexdigest(),
            "stderr": result.stderr}


for repo in ["Hexalith.EventStore", "Hexalith.Builds"]:
    url = f"https://api.github.com/repos/Hexalith/{repo}/releases?per_page=10"
    releases = json.loads(fetch(url))
    receipt["releases"][repo] = {"url": url, "items": [
        {key: release[key] for key in ["tag_name", "published_at", "html_url", "target_commitish"]}
        for release in releases]}

for package, versions in [("Hexalith.EventStore.DomainService", ["4.1.0"]),
                          ("Hexalith.EventStore.Server", ["4.1.0"]),
                          ("Hexalith.Builds.Module.Cli", ["4.44.13", "4.45.0", "4.45.1"])]:
    index_url = f"https://api.nuget.org/v3-flatcontainer/{package.lower()}/index.json"
    index = json.loads(fetch(index_url))
    if package.startswith("Hexalith.EventStore") and index["versions"][-1] != "4.1.0":
        raise RuntimeError("Published EventStore inventory changed; inspect the newer archives before drawing an availability conclusion")
    for version in versions:
        url = f"https://api.nuget.org/v3-flatcontainer/{package.lower()}/{version}/{package.lower()}.{version}.nupkg"
        archive = work / f"{package}.{version}.nupkg"
        archive.write_bytes(fetch(url))
        item = {"id": package, "version": version, "url": url, "indexUrl": index_url,
                "latestListed": index["versions"][-1], "archiveSha256": hashlib.sha256(archive.read_bytes()).hexdigest()}
        with zipfile.ZipFile(archive) as z:
            destination = work / f"{package}.{version}"
            z.extractall(destination)
            ns = ET.fromstring(z.read(next(n for n in z.namelist() if n.endswith(".nuspec"))))
            item["repository"] = next((e.attrib for e in ns.iter() if e.tag.endswith("repository")), None)
            item["repositorySignaturePresent"] = ".signature.p7s" in z.namelist()
            if package.startswith("Hexalith.Builds"):
                dll = destination / "tools/net10.0/any/Hexalith.Builds.Tooling.dll"
                data = dll.read_bytes()
                item["toolingSha256"] = hashlib.sha256(data).hexdigest()
                item["eventStoreExtensionsSchemaLiteral"] = b'"eventStoreExtensions"' in data
                item["api"] = inspect(dll, "Hexalith.Builds.Tooling.Runtime.CompositionDeclarationOptions")
            else:
                dll = destination / f"lib/net10.0/{package}.dll"
                names = ["EventStoreExtensionApi", "EventStoreExtensionAttribute"] if package.endswith("DomainService") else ["EventStoreExtensionServiceCollectionExtensions", "EventStoreExtensionDeclaration"]
                item["binaryMetadataNames"] = {name: name.encode() in dll.read_bytes() for name in names}
                item["api"] = inspect(dll, ("Hexalith.EventStore.DomainService.EventStoreExtensionApi" if package.endswith("DomainService")
                                            else "Hexalith.EventStore.Server.Extensions.EventStoreExtensionServiceCollectionExtensions"))
        if package.startswith("Hexalith.Builds"):
            expected = version != "4.44.13"
            assert bool(item["api"]["catalogPackageVersionSignatures"]) == expected
            assert item["eventStoreExtensionsSchemaLiteral"] == expected
        else:
            assert "not found" in item["api"]["stderr"]
            assert not any(item["binaryMetadataNames"].values())
        receipt["packages"].append(item)
        print(package, version, item["archiveSha256"], flush=True)

receipt["conclusion"] = {
    "EventStore": "No published release carries the required extension API. Latest 4.1.0 lacks the types in actual DomainService and Server archives; its source predates the accepted extension work.",
    "Builds": "4.45.0 and 4.45.1 carry the original extension manifest/catalog lookup API; 4.44.13 does not. None carries the proposed consumer-view overload. Selected 4.45.0 need not change for the original API, but the owner correction needs release/adoption.",
    "candidateScope": "Local candidate archives demonstrate packaging mechanics only. Published package startup and automatic CI qualification remain unavailable pending approved owner release/adoption."}
(HERE / "published-inspection.json").write_text(json.dumps(receipt, indent=2) + "\n")
print("Receipt:", HERE / "published-inspection.json")
