import hashlib
import json
import os
from pathlib import Path
import subprocess
import time

root = Path.cwd()
logs = root / "_bmad-output/implementation-artifacts/evidence/story-1-7/review-patches-2026-10-16/final"
logs.mkdir(exist_ok=True)
environment = os.environ.copy()
for flag in ["HEXALITH_PLATFORM_LIVE_SMOKE", "HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE"]:
    environment.pop(flag, None)
results = []

def run(name, args, env=None, cwd=None):
    print("START " + name, flush=True)
    start = time.monotonic()
    with (logs / (name + ".txt")).open("w") as output:
        result = subprocess.run(args, cwd=cwd or root, env=env or environment, stdout=output, stderr=subprocess.STDOUT)
    record = {"check": name, "exitCode": result.returncode, "seconds": round(time.monotonic() - start, 3), "command": args, "cwd": str(cwd or root)}
    results.append(record)
    (logs / "results.json").write_text(json.dumps(results, indent=2))
    print(json.dumps(record), flush=True)
    if result.returncode:
        print("\n".join((logs / (name + ".txt")).read_text().splitlines()[-70:]), flush=True)
        raise SystemExit(result.returncode)

project = "tests/Hexalith.Platform.Composition.Tests"
assembly = project + "/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll"
run("solution-build", ["dotnet", "build", "Hexalith.Platform.slnx", "-c", "Debug", "-m:1", "--nologo"])
run("platform-build", ["dotnet", "build", project + "/Hexalith.Platform.Composition.Tests.csproj", "-c", "Debug", "-m:1", "--nologo"])
run("platform-suite", ["dotnet", assembly, "-parallelMode", "none"])
run("declarations", ["python3", "scripts/test-reference-module-declarations.py"])
run("helper-gate", ["python3", "eng/test_verify_platform_pins.py"])
run("cleanup-races", ["python3", "eng/test_local_dapr_security_cleanup.py"])
run("timesheets-provenance", ["python3", "eng/timesheets-deployment/test_prepare.py"])
run("architecture", ["python3", ".agents/skills/bmad-architecture/scripts/lint_spine.py", "--workspace", "_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27"])
run("python-compile", ["python3", "-m", "py_compile", "eng/test-local-dapr-security.py", "eng/test_local_dapr_security_cleanup.py", "eng/verify-platform-pins.py"])
run("local-scaffold", ["bash", "eng/verify-agents-host.sh", "--mode", "LocalScaffold", "--artifacts-path", "/tmp/story-1-7-review-final-scaffold"])
run("source-whitespace", ["git", "diff", "--check", "--", "src/Hexalith.Platform.Composition/PlatformDaprBindings.cs", "tests/Hexalith.Platform.Composition.Tests", "eng/test-local-dapr-security.py", "eng/timesheets-deployment/hexalith.module-manifest.v2.json", "_bmad-output/implementation-artifacts/spec-1-7-ratify-local-dapr-conventions-and-bind-component-names.md", "_bmad-output/implementation-artifacts/deferred-work.md"])
run("owner-whitespace", ["git", "diff", "--check"], cwd=root / "references/Hexalith.EventStore")
live = environment.copy()
live.update(HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE="1", HEXALITH_DAPR_HOME="/tmp/story-1-6-builds-toolchain", HEXALITH_ASPIRE_CLI="/tmp/story1-7-aspire-cli/aspire", HEXALITH_PLATFORM_SECURITY_EVIDENCE=str(logs / "security"), HEXALITH_PLATFORM_BUILDS_SMOKE_LOG=str(logs / "builds-tenants-apphost.txt"))
run("security-live", ["dotnet", assembly, "-parallelMode", "none", "-method", "*LiveLocalDaprEnforcesInvocationAndDeclarativeDeadLetterDeliveryAsync"], env=live)
run("builds-tenants-live", ["dotnet", assembly, "-parallelMode", "none", "-method", "*DomainTenantsStartsThroughTheRealBuildsEngineAsync"], env=live)
print("FINAL CHECKS PASSED", flush=True)
