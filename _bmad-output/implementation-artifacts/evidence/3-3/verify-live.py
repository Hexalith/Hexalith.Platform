"""Run the actual mapped source AppHost and preserve sanitized resource diagnostics."""
from pathlib import Path
import json
import os
import re
import subprocess
import time
import uuid

root = Path(__file__).resolve().parents[4]
evidence = Path(__file__).resolve().parent / "verification" / ("live-" + uuid.uuid4().hex)
evidence.mkdir()
mapping = Path("/tmp/platform-routing-generation-958b40aa96df4aaea3b83212a4ac0953/catalog-run-one/source-mapping")
environment = dict(os.environ)
for key, file in {"CustomBeforeDirectoryBuildProps": "SourceMapping.targets", "CustomAfterMicrosoftCommonProps": "SourceMapping.props",
                  "CustomBeforeDirectoryBuildTargets": "SourceMapping.before-targets.props", "CustomAfterDirectoryBuildTargets": "SourceMapping.late.props",
                  "AfterMicrosoftNETSdkTargets": "SourceMapping.final.targets"}.items():
    environment[key] = str(mapping / file)
environment.update({"HEXALITH_SOURCE_MAPPING_PATH": str(mapping / "mapping.json"), "HEXALITH_SOURCE_MAPPING_HASH": json.loads((mapping / "mapping.json").read_text())["ContentHash"],
                    "Platform__EnvironmentId": evidence.name, "Platform__ActiveModule": "parties",
                    "Platform__UsePrebuiltProjects": "true",
                    "Platform__DeclarationPath": str(root / "references/Hexalith.Parties/module/hexalith.module-manifest.v2.json")})
secrets = set()


def run(name, args, timeout=300):
    with (evidence / "commands.jsonl").open("a") as output:
        output.write(json.dumps({"name": name, "cwd": str(root), "argv": args, "mapping": str(mapping), "environmentId": evidence.name}) + "\n")
    result = subprocess.run(args, cwd=root, env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=timeout)
    safe = re.sub(r'([?&]t=)[^\s"\\&]+', r'\1[redacted]', result.stdout)
    if name.startswith("describe-") and result.returncode == 0 and safe.lstrip().startswith("{"):
        model = json.loads(safe)
        for resource in model["resources"]:
            for key, value in resource.get("environment", {}).items():
                if isinstance(value, str) and any(word in key.lower() for word in ["key", "token", "secret", "password"]):
                    secrets.add(value)
            resource["environment"] = {key: "[redacted]" for key in resource.get("environment", {})}
        safe = json.dumps(model, indent=2)
    for value in sorted(secrets, key=len, reverse=True):
        if len(value) >= 8:
            safe = safe.replace(value, "[redacted]")
    (evidence / (name + ".txt")).write_text(safe)
    print(name, result.returncode, flush=True)
    return result


for owner, name in ([] if os.environ.get("STORY33_SKIP_PREBUILD") == "1" else [("EventStore", "EventStore"), ("Tenants", "Tenants"), ("Tenants", "Tenants.Api"), ("Memories", "Memories.DomainService"),
                    ("Memories", "Memories.Server"), ("Memories", "Memories.AccessTelemetry.Clock"), ("Memories", "Memories.AccessTelemetry"), ("Parties", "Parties")]):
    project = root / "references" / ("Hexalith." + owner) / "src" / ("Hexalith." + name) / ("Hexalith." + name + ".csproj")
    result = run(name + "-build", ["dotnet", "build", str(project), "-c", "Debug", "-m:1", "-p:GeneratePackageOnBuild=false", "--nologo"])
    if result.returncode:
        print(result.stdout[-5000:])
        raise SystemExit(result.returncode)
apphost = ["--apphost", str(root / "apphost.cs"), "--non-interactive"]
result = run("start", ["aspire", "start", "--isolated", "--format", "Json", *apphost])
if result.returncode:
    print(result.stdout[-5000:])
    raise SystemExit(result.returncode)
print("evidence", evidence, flush=True)
for attempt in range(12):
    description = run("describe-" + str(attempt), ["aspire", "describe", "--include-hidden", "--format", "Json", *apphost], 30)
    run("logs-" + str(attempt), ["aspire", "logs", "--include-hidden", "--tail", "1000", "--format", "Json", *apphost], 30)
    if description.returncode or not description.stdout.lstrip().startswith("{"):
        break
    time.sleep(10)
