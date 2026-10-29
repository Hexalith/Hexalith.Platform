"""Run the affected projects and retained-evidence checks sequentially."""
from pathlib import Path
import hashlib
import json
import subprocess
import shutil
import tempfile
import sys

out = Path(__file__).resolve().parent
root = out.parents[4]
builds = root / 'references/Hexalith.Builds'

def snapshot():
    paths = []
    for directory in ['src/libraries/Hexalith.Builds.Tooling/Runtime', 'src/libraries/Hexalith.Builds.Tooling/TestEnvironment', 'src/libraries/Hexalith.Builds.Tooling/RunEvidence', 'test/Hexalith.Builds.Module.Tests', 'test/Hexalith.Builds.Evidence.Tests']:
        paths.extend((builds / directory).glob('*.cs'))
    paths.append(builds / 'test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj')
    fixture = builds / 'test/fixtures/module/executable/P0Fixture.NativeMetadataProbe'
    paths.extend(path for path in fixture.rglob('*') if path.is_file() and path.suffix in ['.cs', '.c', '.csproj'] and not {'bin', 'obj'}.intersection(path.parts))
    return {str(path.relative_to(root)): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(paths)}

before = snapshot()
(out / 'source-before.json').write_text(json.dumps(before, indent=2) + '\n')

def run(name, argv, cwd=root):
    print('Running ' + name, flush=True)
    subprocess.run([sys.executable, str(out / 'run-check.py'), name, *map(str, argv)], cwd=cwd, check=True)

if '--tests-only' not in sys.argv:
    run('evidence-build', ['dotnet', 'build', 'test/Hexalith.Builds.Evidence.Tests/Hexalith.Builds.Evidence.Tests.csproj', '-c', 'Debug', '-m:1'], builds)
    run('platform-build', ['dotnet', 'build', 'tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj', '-c', 'Debug', '-m:1'])
module = json.loads((out.parent / 'delta-review-fixes-20261025/module-tests.command.json').read_text())['argv']
module[module.index('-result-xml') + 1] = str(out / 'module-tests.xml')
module += ['-class', '*LocalRunSourceReviewTests', '-class', '*NativeTestExecutorTests', '-class', '*NativeMetadataPublicationTests', '-parallelMode', 'none']
run('module-tests', module, builds)
run('evidence-tests', ['dotnet', 'test/Hexalith.Builds.Evidence.Tests/bin/Debug/net10.0/Hexalith.Builds.Evidence.Tests.dll', '-result-xml', out / 'evidence-tests.xml'], builds)
run('platform-tests', ['dotnet', 'tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll', '-method', '*FinalCleanupAttemptsEveryOwnerDespiteFailuresAsync', '-result-xml', out / 'platform-tests.xml'])
native = Path(tempfile.mkdtemp(prefix='story25-native-harness-'))
for path in (out / 'native-harness').iterdir():
    shutil.copy2(path, native / path.name)
native_library = native / 'native-probe.so'
run('native-shim-build', ['cc', '-shared', '-fPIC', '-o', native_library, out / 'native-probe.c', '-ldl'])
run('native-harness-build', ['dotnet', 'build', native / 'NativeProbe.csproj', '-c', 'Debug', '-m:1', '-p:BuildsToolingPath=' + str(builds / 'src/libraries/Hexalith.Builds.Tooling/bin/Debug/net10.0/Hexalith.Builds.Tooling.dll')])
run('native-probe', ['env', 'LD_PRELOAD=' + str(native_library), 'HEXALITH_NATIVE_PROBE_LOG=' + str(out / 'native-observations.log'), 'dotnet', native / 'bin/Debug/net10.0/NativeProbe.dll', native_library, out / 'native-runtime'])
observations = (out / 'native-observations.log').read_text()
assert 'injected-EINTR metadata-read.json' in observations, observations
assert 'shared-lock-ok metadata-read.json' in observations, observations
assert 'fsync-ok-before-rename .metadata-read.json.' in observations, observations
assert 'fsync-ok-before-rename .archive-' in observations, observations
run('retained-evidence', ['python3', '_bmad-output/implementation-artifacts/evidence/2-5/local-lifecycle-20261024/test_audit.py'])
run('schema-parity', ['python3', '_bmad-output/implementation-artifacts/evidence/2-5/committed-review-fixes-20261025/audit-schema-parity.py'])
after = snapshot()
(out / 'source-after.json').write_text(json.dumps(after, indent=2) + '\n')
assert before == after, 'Source changed during verification'
print('All checks passed; verified source unchanged.', flush=True)
