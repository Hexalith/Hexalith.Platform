"""Run the approved individual mapped Debug builds and built xUnit classes, retaining exact commands and output."""
from pathlib import Path
import json
import re
import subprocess
import argparse
import os
import uuid

root = Path(__file__).resolve().parents[4]
evidence = Path(__file__).resolve().parent / "verification"
evidence.mkdir(exist_ok=True)
parser = argparse.ArgumentParser()
parser.add_argument("--mapping-workspace", type=Path, required=True)
parser.add_argument("--start-at")
arguments = parser.parse_args()
mapping = arguments.mapping_workspace / "source-mapping"
environment = dict(os.environ)
environment.update({
    "CustomBeforeDirectoryBuildProps": str(mapping / "SourceMapping.targets"),
    "CustomAfterMicrosoftCommonProps": str(mapping / "SourceMapping.props"),
    "CustomBeforeDirectoryBuildTargets": str(mapping / "SourceMapping.before-targets.props"),
    "CustomAfterDirectoryBuildTargets": str(mapping / "SourceMapping.late.props"),
    "AfterMicrosoftNETSdkTargets": str(mapping / "SourceMapping.final.targets"),
    "HEXALITH_SOURCE_MAPPING_HASH": json.loads((mapping / "mapping.json").read_text())["ContentHash"],
    "Configuration": "Debug",
    "HEXALITH_CATALOG_HTTP_EVIDENCE_PATH": str(evidence / "http-ingress.json"),
})
flags = ["-c", "Debug", "-m:1", "--nologo", "-p:RestoreForceEvaluate=true", "-p:GeneratePackageOnBuild=false",
         "-p:UseHexalithProjectReferences=true", "-p:UseNuGetDeps=false", "-p:HexalithEventStoreFromSource=true",
         "-p:HexalithCommonsFromSource=true", "-p:HexalithMcpCliFromSource=true"]
for owner in ["EventStore", "Builds", "Commons", "McpCli", "Tenants", "Parties", "Memories"]:
    flags.append(f"-p:Hexalith{owner}Root={root / 'references' / ('Hexalith.' + owner)}")
flags.append(f"-p:HexalithTenantsBasePath={root / 'references/Hexalith.Tenants/src'}")
lanes = [
    ("EventStore.Contracts", ["Routing.RouteCatalogV3Tests", "Routing.RouteCatalogCodecTests"]),
    ("Tenants.Contracts", ["TenantCatalogTests"]),
    ("Parties.Contracts", ["PartiesCatalogTests"]),
    ("Memories.EventStore", ["MemoriesCatalogTests"]),
    ("EventStore.Server", ["Routing.RouteCatalogSurfaceTests", "Routing.RouteCatalogV3IngressTests", "Routing.DevelopmentCompositionBootstrapTests"]),
    ("Platform.Composition", ["PlatformRoutingCatalogTests"]),
]
commands = evidence / "source-commands.jsonl"
if not arguments.start_at:
    commands.write_text("")


def run(name, args, expect_tests=False):
    with commands.open("a") as stream:
        stream.write(json.dumps({"name": name, "cwd": str(root), "argv": args, "mappingWorkspace": str(arguments.mapping_workspace)}) + "\n")
    result = subprocess.run(args, cwd=root, env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    (evidence / (name + ".txt")).write_text(result.stdout)
    print(f"{name}: exit {result.returncode}", flush=True)
    if result.returncode or expect_tests and not re.search(r"Total:\s*[1-9]", result.stdout):
        print(result.stdout[-12000:], flush=True)
        raise SystemExit(result.returncode or 1)


verification_id = uuid.uuid4().hex
if arguments.start_at:
    lanes = lanes[next(index for index, lane in enumerate(lanes) if lane[0] == arguments.start_at):]
for lane, classes in lanes:
    assembly = "Hexalith." + lane + ".Tests"
    owner = lane.split(".")[0]
    project = (root if owner == "Platform" else root / "references" / ("Hexalith." + owner)) / "tests" / assembly / (assembly + ".csproj")
    artifacts = (root if owner == "Platform" else root / "references" / ("Hexalith." + owner)) / "artifacts" / "story33-source" / verification_id / lane
    run(lane + "-build", ["dotnet", "build", str(project), "--artifacts-path", str(artifacts), *flags])
    for name in classes:
        run(lane + "-" + name, ["dotnet", str(artifacts / "bin" / assembly / "debug" / (assembly + ".dll")), "-class", assembly + "." + name], True)
