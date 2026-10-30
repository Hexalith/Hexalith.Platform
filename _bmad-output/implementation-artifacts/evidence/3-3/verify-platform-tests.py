"""Run final mapped Platform checks, including both actual Aspire readiness lanes."""
from pathlib import Path
import json
import os
import re
import subprocess

root = Path(__file__).resolve().parents[4]
evidence = Path(__file__).resolve().parent / "verification"
records = [json.loads(line) for line in (evidence / "source-commands.jsonl").read_text().splitlines()]
build = next(record for record in reversed(records) if record["name"] == "Platform.Composition-final-build")
mapping = Path(build["mappingWorkspace"]) / "source-mapping"
environment = dict(os.environ)
for key, file in {"CustomBeforeDirectoryBuildProps": "SourceMapping.targets", "CustomAfterMicrosoftCommonProps": "SourceMapping.props",
                  "CustomBeforeDirectoryBuildTargets": "SourceMapping.before-targets.props", "CustomAfterDirectoryBuildTargets": "SourceMapping.late.props",
                  "AfterMicrosoftNETSdkTargets": "SourceMapping.final.targets"}.items():
    environment[key] = str(mapping / file)
environment.update({"HEXALITH_PLATFORM_LIVE_SMOKE": "1", "HEXALITH_PLATFORM_PREBUILD_SOURCE": "1",
                    "HEXALITH_PLATFORM_PACKAGE_REFUSAL_PATH": str(evidence / "package-composition-refusal.txt"),
                    "HEXALITH_PLATFORM_SOURCE_PREBUILD_LOG": str(evidence / "reference-readiness-prebuild.txt")})


def run(name, argv):
    with (evidence / "source-commands.jsonl").open("a") as output:
        output.write(json.dumps({"name": name, "cwd": str(root), "argv": argv, "mappingWorkspace": str(mapping.parent), "liveSmoke": True}) + "\n")
    result = subprocess.run(argv, cwd=root, env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    safe = re.sub(r'([?&]t=)[^\s"\\&]+', r'\1[redacted]', result.stdout)
    (evidence / (name + ".txt")).write_text(safe)
    print(name, result.returncode, safe[-1800:], flush=True)
    if result.returncode:
        raise SystemExit(result.returncode)


run("Platform.Composition-final-build", build["argv"])
artifacts = Path(build["argv"][build["argv"].index("--artifacts-path") + 1])
assembly = artifacts / "bin/Hexalith.Platform.Composition.Tests/debug/Hexalith.Platform.Composition.Tests.dll"
for name in (["PlatformRoutingCatalogTests", "WriterProtocolStartupTests"] if os.environ.get("STORY33_UNIT_ONLY") == "1" else ["PlatformRoutingCatalogIntegrationTests"] if os.environ.get("STORY33_LIVE_ONLY") == "1" else ["PlatformRoutingCatalogTests", "WriterProtocolStartupTests", "PlatformRoutingCatalogIntegrationTests"]):
    run("Platform-final-" + name, ["dotnet", str(assembly), "-class", "Hexalith.Platform.Composition.Tests." + name])
run("Platform-final-PackageAdmission", ["dotnet", str(assembly), "-method", "*PackageModeRefusesUnreleasedOwnerSnapshotBeforeAddingResources"])
if os.environ.get("STORY33_UNIT_ONLY") != "1":
    run("Platform-final-ReferenceReadiness", ["dotnet", str(assembly), "-method", "*ReferenceServersReachDeclaredReadinessInTheFileEntryAsync"])
