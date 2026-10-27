"""Run only the lifecycle/recovery and evidence diagnostics cases changed by review."""
from pathlib import Path
import subprocess
import sys

out = Path(__file__).resolve().parent
builds = out.parents[5] / 'references/Hexalith.Builds'


def run(name, argv):
    subprocess.run([sys.executable, str(out / 'run-check.py'), name, *map(str, argv)], cwd=builds, check=True)


argv = ['dotnet', 'test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll']
for method in ['SessionlessStartupPreservesCommittedOutcomeAsync', 'SessionlessStartupRejectsMalformedCommittedOutcomeAsync', 'NativeOutcomeControlsLocalCleanupAsync', 'SupportedTerminalMetadataRestoresAndListsAsync', 'MalformedTerminalMetadataRefusesRestorationAsync', 'RetainedSurvivorsAreRecordedAfterStoppingBudgetExpiresAsync']:
    argv.extend(['-method', '*' + method])
run('module-focused', [*argv, '-result-xml', out / 'module-focused.xml'])
run('evidence-build', ['dotnet', 'build', 'test/Hexalith.Builds.Evidence.Tests/Hexalith.Builds.Evidence.Tests.csproj', '-c', 'Debug', '-m:1'])
run('evidence-focused', ['dotnet', 'test/Hexalith.Builds.Evidence.Tests/bin/Debug/net10.0/Hexalith.Builds.Evidence.Tests.dll', '-method', '*CompletedSelectedEvidenceRequiresVerifiedCleanupAsync', '-result-xml', out / 'evidence-focused.xml'])
