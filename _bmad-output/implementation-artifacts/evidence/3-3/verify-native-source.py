"""Build immutable source-tool artifacts and attempt the real owner-declared native suite."""
from pathlib import Path
import hashlib
import json
import os
import re
import shutil
import subprocess
import time

root = Path(__file__).resolve().parents[4]
evidence = Path(__file__).resolve().parent / "verification"
capture = json.loads((evidence / "builds-capture-receipt.json").read_text())
snapshot = Path(capture["snapshot"])
workspace = snapshot.parent
environment = {key: value for key, value in os.environ.items() if not key.startswith(("CustomBefore", "CustomAfter", "AfterMicrosoft", "HEXALITH_SOURCE_MAPPING", "NUGET_PACKAGES"))}
environment["CustomAfterMicrosoftCommonProps"] = capture["versionProps"]
flags = ["-c", "Debug", "-m:1", "--artifacts-path", str(workspace / "captured-build"), "-p:GeneratePackageOnBuild=false", "-p:UseSharedCompilation=false",
         "-p:UseHexalithProjectReferences=true", "-p:UseNuGetDeps=false", "-p:HexalithEventStoreFromSource=true", "-p:HexalithCommonsFromSource=true", "-p:HexalithMcpCliFromSource=true", "--nologo"]
for owner in ["EventStore", "Commons", "McpCli"]:
    flags.append("-p:Hexalith" + owner + "Root=" + str(root / "references" / ("Hexalith." + owner)))
flags.append("-p:HexalithBuildsRoot=" + str(snapshot))


def safe(text):
    return re.sub(r'([?&]t=)[^\s"\\&]+', r'\1[redacted]', text)


for name, project, output in [
    ("captured-source-host", snapshot / "src/hosts/Hexalith.Builds.Module.AppHost/Hexalith.Builds.Module.AppHost.csproj", workspace / "captured-host-output"),
    ("captured-source-cli", snapshot / "src/libraries/Hexalith.Builds.Module.Cli/Hexalith.Builds.Module.Cli.csproj", workspace / "captured-cli-output"),
]:
    argv = ["dotnet", "build", str(project), "--output", str(output), *flags]
    (evidence / (name + "-command.json")).write_text(json.dumps({"argv": argv, "sourceCapture": str(snapshot), "sourceVersion": capture["sourceToolVersion"]}, indent=2) + "\n")
    result = subprocess.run(argv, cwd=root, env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    (evidence / (name + "-build.txt")).write_text(safe(result.stdout))
    print(name, result.returncode, result.stdout[-1200:], flush=True)
    if result.returncode:
        raise SystemExit(result.returncode)

cli = workspace / "captured-cli-output/Hexalith.Builds.Module.Cli.dll"
host = workspace / "captured-host-output/Hexalith.Builds.Module.AppHost.dll"
environment["HEXALITH_G4_APPHOST"] = str(host)
environment.pop("CustomAfterMicrosoftCommonProps", None)
diagnostics = Path.home() / ".local/share/hexalith-builds/g4-runs/diagnostics"
known = {path.name for path in diagnostics.iterdir()}
relative_evidence = "artifacts/story33-native-module-evidence.json"
argv = ["dotnet", str(cli), "test", "--manifest", str(root / "references/Hexalith.Parties/module/hexalith.module-manifest.v2.json"),
        "--profile", "platform-source", "--mode", "source", "--output", "json", "--evidence", relative_evidence]
(evidence / "run-plan-native-captured-command.json").write_text(json.dumps({"argv": argv, "appHost": str(host), "cliSha256": hashlib.sha256(cli.read_bytes()).hexdigest(),
    "hostSha256": hashlib.sha256(host.read_bytes()).hexdigest(), "sourceVersion": capture["sourceToolVersion"], "sourceCapture": str(snapshot)}, indent=2) + "\n")
log = workspace / "native-output.txt"
run = None
with log.open("w") as output:
    process = subprocess.Popen(argv, cwd=root, env=environment, stdout=output, stderr=subprocess.STDOUT)
    while process.poll() is None:
        if run is None:
            for path in diagnostics.iterdir():
                if path.name in known or not (path / "selected-plan.json").exists():
                    continue
                plan = json.loads((path / "selected-plan.json").read_text())
                if str(plan.get("selectedTest", {}).get("moduleId", "")) == "parties":
                    run = path
                    (evidence / "run-plan-native-run.json").write_text(json.dumps({"runId": path.name, "diagnostics": str(path), "sourceCapture": str(snapshot)}, indent=2) + "\n")
                    print("native-run", path.name, str(path), flush=True)
                    break
        if run is not None:
            plan_path = run / "selected-plan.json"
            if plan_path.exists():
                plan = json.loads(plan_path.read_text())
                path = Path(plan.get("workspace", "")) / "test-environment.json"
                if path.is_file():
                    shutil.copy(path, evidence / "native-test-environment.json")
                    print("native-descriptor", str(path), flush=True)
                    # Copy once while the real declared suite executes; cleanup may remove private workspace state.
                    run = None
                    known.add(plan.get("runId", ""))
        time.sleep(1)
    result = process.returncode
(evidence / "run-plan-native-captured-output.txt").write_text(safe(log.read_text()))
print("native-command", result, safe(log.read_text())[-3500:], flush=True)
raise SystemExit(result)
