"""Run focused review regressions and the prescribed affected Builds lanes."""
from pathlib import Path
import json
import subprocess
import sys

out = Path(__file__).resolve().parent
root = out.parents[4]
builds = root / "references/Hexalith.Builds"


def run(name, argv):
    subprocess.run([sys.executable, str(out / "run-check.py"), name, *map(str, argv)], cwd=builds, check=True)


module_assembly = "test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll"
focused = ["dotnet", module_assembly]
for method in ["SupportedTerminalMetadataRestoresAndListsAsync", "UnverifiableObservationNamesOwnerAsync", "SessionlessStartupRejectsMalformedCommittedOutcomeAsync", "NullCommittedResourceEntryReturnsStructuredUncertaintyAsync", "NullTrackedReadinessReturnsStructuredUncertaintyAsync", "DiscoveryFailuresPreventCompleteCleanupAsync", "MalformedTerminalMetadataRefusesRestorationAsync", "NativeOutcomeControlsLocalCleanupAsync", "ExhaustedExecutionLifetimeBeforeNativeReturnRecordsTimeoutCauseAsync"]:
    focused.extend(["-method", "*" + method])
run("module-focused", [*focused, "-result-xml", out / "module-focused.xml"])
run("evidence-build", ["dotnet", "build", "test/Hexalith.Builds.Evidence.Tests/Hexalith.Builds.Evidence.Tests.csproj", "-c", "Debug", "-m:1"])
module = json.loads((out.parent / "full-story-patches-20261025/implementation/module-receipt-final.command.json").read_text())["argv"]
module[module.index("-result-xml") + 1] = str(out / "module-tests.xml")
run("module-tests", module)
run("evidence-tests", ["dotnet", "test/Hexalith.Builds.Evidence.Tests/bin/Debug/net10.0/Hexalith.Builds.Evidence.Tests.dll", "-result-xml", out / "evidence-tests.xml"])
