#!/usr/bin/env python3
"""Apply the proposed Builds patch only to a disposable copy; run its focused controls."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import signal
import os
import subprocess
import tempfile
import xml.etree.ElementTree as ET

here = Path(__file__).resolve().parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--root", type=Path, default=here.parents[4])
parser.add_argument("--work", type=Path)
parser.add_argument("--reuse-work", action="store_true", help="Verify the exact patch in an existing disposable copy before repeating controls")
parser.add_argument("--candidate-receipt", type=Path, required=True)
args = parser.parse_args()
root = args.root.resolve()
work = args.work or Path(tempfile.mkdtemp(prefix="story32-owner-snapshot-"))
owner = root / "references/Hexalith.Builds"
copy = work / "builds"
commands = []
work.mkdir(parents=True, exist_ok=True)


def run(command, cwd, name, timeout=600):
    log = work / name
    commands.append({"command": command, "cwd": str(cwd), "log": str(log), "timeoutSeconds": timeout})
    with log.open("wb") as output:
        process = subprocess.Popen(command, cwd=cwd, stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
        try:
            code = process.wait(timeout=timeout)
        except BaseException:
            os.killpg(process.pid, signal.SIGKILL)
            process.wait()
            raise
    commands[-1]["exitCode"] = code
    if code:
        raise RuntimeError(f"Exit {code}; see {log}")


receipt = {"candidateOnly": True, "qualificationCredit": False, "ownerPatchAppliedToSelectedDependency": False,
           "ownerRevision": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=owner, text=True).strip(),
           "commands": commands, "work": str(work)}
try:
    if args.reuse_work:
        run(["git", "apply", "--reverse", "--check", str(here / "builds-consumer-snapshot.patch")], copy, "check-applied.log", 30)
    else:
        for relative in subprocess.check_output(["git", "ls-files"], cwd=owner, text=True).splitlines():
            source = owner / relative
            if source.is_file() and not relative.startswith(("references/", "_bmad", ".agents/", ".claude/", ".github/")):
                target = copy / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source, target)
        run(["git", "apply", str(here / "builds-consumer-snapshot.patch")], copy, "apply.log", 30)
    run(["dotnet", "build", str(copy / "src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj"), "-c", "Debug", "-nodeReuse:false", "--disable-build-servers", "--nologo"], copy, "build-owner.log")
    tests = work / "tests"
    tests.mkdir(exist_ok=True)
    shutil.copyfile(copy / "test/Hexalith.Builds.Module.Tests/Workspace/CompositionCatalogPackageViewTests.cs", tests / "CompositionCatalogPackageViewTests.cs")
    project = tests / "OwnerSnapshot.Tests.csproj"
    tooling = copy / "src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj"
    model = root / "src/Hexalith.Platform.Composition/bin/Debug/net10.0/Hexalith.Platform.Composition.dll"
    project.write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><Using Include="Xunit"/><PackageReference Include="xunit.v3" Version="4.0.2"/><PackageReference Include="Shouldly" Version="4.3.0"/><ProjectReference Include="{tooling}"/><Reference Include="Hexalith.Platform.Composition" HintPath="{model}"/></ItemGroup></Project>')
    shutil.copyfile(here / "HostSnapshotRestoreTests.cs.txt", tests / "HostSnapshotRestoreTests.cs")
    run(["dotnet", "build", str(project), "-c", "Debug", "-nodeReuse:false", "--disable-build-servers", "--nologo"], tests, "build-tests.log")
    os.environ["STORY32_CANDIDATE_RECEIPT"] = str(args.candidate_receipt.resolve())
    result = work / "tests.xml"
    run(["dotnet", str(tests / "bin/Debug/net10.0/OwnerSnapshot.Tests.dll"), "-xml", str(result)], tests, "tests.log", 180)
    assembly = ET.parse(result).getroot().find("assembly")
    receipt["tests"] = {name: int(assembly.get(name, "0")) for name in ["total", "passed", "failed", "skipped", "not-run"]}
    assert receipt["tests"] == {"total": 6, "passed": 6, "failed": 0, "skipped": 0, "not-run": 0}
    receipt["hostViewRestore"] = json.loads((tests / "bin/Debug/net10.0/host-view-result.json").read_text())
    receipt["passed"] = True
except Exception as error:
    receipt.update(passed=False, failure=str(error))
    raise
finally:
    (work / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")
    print(work / "receipt.json", flush=True)
