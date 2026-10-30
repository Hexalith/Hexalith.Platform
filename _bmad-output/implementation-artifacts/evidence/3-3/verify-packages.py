"""Build matching owner Release packages into a private feed/cache, then consume only those packages in the catalog reader."""
from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess
import uuid
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[4]
evidence = Path(__file__).resolve().parent / "verification"
evidence.mkdir(exist_ok=True)
workspace = root / "artifacts" / "story33-packages" / uuid.uuid4().hex
feed, cache = workspace / "feed", workspace / "cache"
feed.mkdir(parents=True)
cache.mkdir()
versions = {"EventStore": "3.128.5-story33.0", "Tenants": "5.11.2-story33.0", "Parties": "2.0.1-story33.0", "Memories": "3.0.11-story33.0"}
environment = {key: value for key, value in os.environ.items() if not key.startswith(("CustomBefore", "CustomAfter", "AfterMicrosoft", "HEXALITH_SOURCE_MAPPING"))}
environment.update({"NUGET_PACKAGES": str(cache), "DOTNET_ENVIRONMENT": "Development", "ASPNETCORE_ENVIRONMENT": "Development"})
# Seed immutable published nupkgs, never extracted assemblies or source projects.
for package in (Path.home() / ".nuget/packages").glob("*/*/*.nupkg"):
    target = feed / package.name
    if not target.exists():
        os.link(package, target)
config = workspace / "nuget.config"
config.write_text(f'<configuration><packageSources><clear/><add key="private" value="{feed}"/><add key="nuget" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>')
commands = evidence / "package-commands.jsonl"
commands.write_text("")


def run(name, args):
    with commands.open("a") as stream:
        stream.write(json.dumps({"name": name, "cwd": str(root), "argv": args, "packageCache": str(cache), "feed": str(feed)}) + "\n")
    result = subprocess.run(args, cwd=root, env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    (evidence / ("package-" + name + ".txt")).write_text(result.stdout)
    print(f"{name}: exit {result.returncode}", flush=True)
    if result.returncode:
        print(result.stdout[-16000:], flush=True)
        raise SystemExit(result.returncode)


flags = ["-c", "Release", "-m:1", "--nologo", "-p:GeneratePackageOnBuild=false", "-p:UseHexalithProjectReferences=false", "-p:UseNuGetDeps=true",
         "-p:HexalithEventStoreFromSource=false", "-p:HexalithCommonsFromSource=false", "-p:HexalithMcpCliFromSource=false",
         "-p:RestoreConfigFile=" + str(config)]
for owner in ["EventStore", "Builds", "Commons", "McpCli", "Tenants", "Parties", "Memories"]:
    flags.append(f"-p:Hexalith{owner}Root={root / 'references' / ('Hexalith.' + owner)}")
for owner, version in versions.items():
    flags.append(f"-p:Hexalith{owner}Version={version}")
projects = {
    "EventStore": ["Contracts", "ServiceDefaults", "Client", "DomainService", "Admin.Abstractions", "Server", "Gateway", "Aspire"],
    "Tenants": ["Contracts", "Server"],
    "Parties": ["Contracts", "EventStoreExtensions"],
    "Memories": ["Contracts", "EventStore", "EventStoreExtensions"],
}
produced = []
for owner, names in projects.items():
    for name in names:
        assembly = "Hexalith." + owner + "." + name
        project = root / "references" / ("Hexalith." + owner) / "src" / assembly / (assembly + ".csproj")
        run(assembly, ["dotnet", "pack", str(project), "--artifacts-path", str(workspace / "builds" / owner), "--output", str(feed), *flags,
                       "-p:Version=" + versions[owner], "-p:PackageVersion=" + versions[owner]])
        package = feed / (assembly + "." + versions[owner] + ".nupkg")
        if not package.exists():
            raise RuntimeError("The matching producer package is absent: " + str(package))
        produced.append({"id": assembly, "version": versions[owner], "path": str(package), "sha256": hashlib.sha256(package.read_bytes()).hexdigest()})
reader = workspace / "reader"
reader.mkdir()
template = root / "src/Hexalith.Platform.CatalogBuilder"
shutil.copy(template / "Program.cs", reader / "Program.cs")
shutil.copy(template / "Hexalith.Platform.CatalogBuilder.csproj", reader / "Hexalith.Platform.CatalogBuilder.csproj")
bindings = ET.Element("Project")
ET.SubElement(bindings, "Import", Project=str(root / "references/Hexalith.Builds/Props/Directory.Packages.props"))
items = ET.SubElement(bindings, "ItemGroup")
owners = []
for owner in ["Tenants", "Parties", "Memories"]:
    contract = "Hexalith." + owner + (".EventStore" if owner == "Memories" else ".Contracts")
    adapter = "Hexalith." + owner + (".Server" if owner == "Tenants" else ".EventStoreExtensions")
    for package in [contract, adapter]:
        ET.SubElement(items, "PackageReference", Include=package)
    manifest = json.loads((root / "references" / ("Hexalith." + owner) / "module/hexalith.module-manifest.v2.json").read_text())["modules"][0]
    app = next(server["appId"] for server in manifest["identity"]["servers"] if server["id"] == "domain")
    owners.append({"assembly": contract, "adapterAssembly": adapter, "appId": app})
ET.ElementTree(bindings).write(reader / "bindings.props")
run("reader-build", ["dotnet", "build", str(reader / "Hexalith.Platform.CatalogBuilder.csproj"), "--output", str(reader / "output"),
                     "-p:PlatformCatalogBindings=" + str(reader / "bindings.props"), *flags])
assets = json.loads((reader / "obj/project.assets.json").read_text())
assert all(library["type"] == "package" for library in assets["libraries"].values()), "A source substitute entered the package reader."
output = workspace / "catalog"
output.mkdir()
request = {"generation": 1, "instanceId": workspace.name + "-catalog", "outputDirectory": str(output), "owners": owners}
(reader / "request.json").write_text(json.dumps(request))
run("reader-generate", ["dotnet", str(reader / "output/Hexalith.Platform.CatalogBuilder.dll"), str(reader / "request.json")])
catalog = json.loads((output / "catalog.json").read_text())
assert catalog["schemaVersion"] == 3 and any(declaration["domain"] == "party" for declaration in catalog["declarations"])
shutil.copy(output / "catalog.json", evidence / "package-catalog.json")
receipt = {"workspace": str(workspace), "feed": str(feed), "packageCache": str(cache), "runtimeEnvironment": "Development", "buildConfiguration": "Release",
           "qualification": "local matching package consumption; this does not establish candidate or hosted acceptance",
           "packages": produced, "rootDigest": hashlib.sha256((output / "catalog.json").read_bytes()).hexdigest(), "operations": len(catalog["operations"]),
           "sourceRevisions": {owner: subprocess.check_output(["git", "-C", str(root / "references" / ("Hexalith." + owner)), "rev-parse", "HEAD"], text=True).strip() for owner in versions},
           "sourceStatus": {owner: subprocess.check_output(["git", "-C", str(root / "references" / ("Hexalith." + owner)), "status", "--porcelain"], text=True).splitlines() for owner in versions}}
(evidence / "package-receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")
print("Matching package reader consumed " + str(len(catalog["operations"])) + " operations.", flush=True)
