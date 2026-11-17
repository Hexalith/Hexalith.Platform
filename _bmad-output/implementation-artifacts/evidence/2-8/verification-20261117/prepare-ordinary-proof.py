"""Prepare one old, genuine holds-only proof subject; no dispatch, publication or pin overlay."""
from pathlib import Path
helper = Path(__file__).with_name('check-coherent-caller.py').read_text()
prefix = helper.split('paths = [')[0].replace("coherent-caller-2'", "ordinary-proof-2'")
prefix = prefix.replace("BASE = '4eca9e8fbad451a602a9ec7d2d6384345b6b0e4e'", "BASE = '11053e562a2cba0196354cec655c01677e2afd46'")
exec(compile(prefix, str(__file__), 'exec'))
BUILD = '6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b'
executor = WORK / '.hexalith/builds-execution'
executor.parent.mkdir(exist_ok=True)
run('clone-executor', ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references/Hexalith.Builds'), str(executor)])
run('checkout-executor', ['git', '-C', str(executor), '-c', 'submodule.recurse=false', 'checkout', '--detach', BUILD])
paths = []
def write(path, value):
    destination = WORK / path
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(value if isinstance(value, bytes) else value.encode())
    paths.append(path)
def replace(path, old, new, count=1):
    data = (WORK / path).read_bytes()
    assert data.count(old.encode()) == count, (path, old, data.count(old.encode()))
    write(path, data.replace(old.encode(), new.encode()))

manifest = json.loads((WORK / '.config/dotnet-tools.json').read_text())
assert manifest['tools']['hexalith.builds.module.cli']['version'] == '4.44.13'
manifest['tools']['hexalith.builds.module.cli']['version'] = '4.48.2'
write('.config/dotnet-tools.json', json.dumps(manifest, indent=2) + '\n')
workflow = (WORK / '.github/workflows/ci-lifecycle-qualification.yml').read_text()
assert workflow.count('@3c8096180ecf2aa539ac454815efbfceb8a92822') == 1
workflow = workflow.replace('@3c8096180ecf2aa539ac454815efbfceb8a92822', '@' + BUILD)
anchor = '      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0\n'
checkout = '''      # The released executor remains separate from the caller's immutable catalog pin.
      - name: Check out the exact released proof executor
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          repository: Hexalith/Hexalith.Builds
          ref: 6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b
          path: .hexalith/builds-execution
          persist-credentials: false
          submodules: false
      - name: Verify the exact clean proof executor
        run: |
          test "$(git -C .hexalith/builds-execution rev-parse HEAD)" = 6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b
          test -z "$(git -C .hexalith/builds-execution status --porcelain --untracked-files=normal)"
'''
assert workflow.count(anchor) == 1
workflow = workflow.replace(anchor, checkout + anchor)
workflow = workflow.replace('run: python3 -B references/Hexalith.Builds/Github/prepare-module-build/prepare_module_build.py', 'run: python3 -B .hexalith/builds-execution/Github/prepare-module-build/prepare_module_build.py')
assert workflow.count("builds = Path('references/Hexalith.Builds')") == 1
workflow = workflow.replace("builds = Path('references/Hexalith.Builds')", "builds = Path('.hexalith/builds-execution')")
observer_anchor = '      - name: Observe actual execution cancellation readiness and active hold\n'
workflow = workflow.replace(observer_anchor, '''      - name: Start the Story 2.8 proof event observer
        if: ${{ inputs.scenario == 'cancel-execution' }}
        run: setsid nohup python3 -B scripts/story-2-8-proof-observer.py lifecycle >/dev/null 2>&1 </dev/null &
''' + observer_anchor)
assert workflow.count('          path: TestResults/ci-lifecycle\n') == 2
workflow = workflow.replace('          path: TestResults/ci-lifecycle\n', '          path: |\n            TestResults/ci-lifecycle\n            TestResults/story-2-8-proof-observer\n', 1)
workflow += '''      - name: Wait for the Story 2.8 proof observer flush
        if: ${{ always() && inputs.scenario == 'cancel-execution' }}
        run: |
          for _ in $(seq 1 40); do
            if ! pgrep -f 'story-2-8-proof-observer.py lifecycle' > /dev/null; then break; fi
            sleep 0.5
          done
      - name: Retain the Story 2.8 proof event observations
        if: ${{ always() && inputs.scenario == 'cancel-execution' }}
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9
        with:
          name: story-2-8-proof-observer
          path: TestResults/story-2-8-proof-observer
          if-no-files-found: error
          retention-days: 14
'''
write('.github/workflows/ci-lifecycle-qualification.yml', workflow)
replace('scripts/qualify-disposable-ci.py', "ROOT / 'references/Hexalith.Builds/Github/run-module-integration/run_module_integration.py'", "ROOT / '.hexalith/builds-execution/Github/run-module-integration/run_module_integration.py'")
write('scripts/story-2-8-proof-observer.py', subprocess.check_output(['git', '-C', str(SOURCE), 'show', 'b00e60a69ace273150c133665dd42488841eb7d5:scripts/story-2-8-proof-observer.py']))
replace('tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs', 'manual.ShouldContain($"Hexalith/Hexalith.Builds/.github/workflows/domain-ci-v2.yml@{BuildsCiSha}");', 'manual.ShouldContain("Hexalith/Hexalith.Builds/.github/workflows/domain-ci-v2.yml@6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b");')
replace('tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs', 'tool.GetProperty("version").GetString().ShouldBe("4.44.13");', 'tool.GetProperty("version").GetString().ShouldBe("4.48.2");')
replace('tests/ci-lifecycle/test_workflow_boundaries.py', "self.assertEqual(re.search(reference, baseline).group(), re.search(reference, ordinary).group())", "self.assertEqual(re.search(reference, baseline).group(),\n                         'Hexalith/Hexalith.Builds/.github/workflows/domain-ci-v2.yml@6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b')\n        self.assertEqual(re.search(reference, ordinary).group(),\n                         'Hexalith/Hexalith.Builds/.github/workflows/domain-ci-v2.yml@3c8096180ecf2aa539ac454815efbfceb8a92822')")
replace('tests/ci-lifecycle/test_review_fixes.py', 'references/Hexalith.Builds/Props', '.hexalith/builds-execution/Props', count=3)
for path in ['README.md', 'docs/getting-started.md']:
    original = (WORK / path).read_text()
    first, rest = original.split('\n', 1)
    note = '''
This Story 2.8 proof ref selects the already released module CLI **4.48.2**
with `rollForward=false`. Its manual lifecycle workflow uses the separate
immutable Builds executor `6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b` and
that tool's Aspire 13.6.5 / Dapr CLI and runtime 1.19.1 pins. The genuine
caller catalog `3c8096180ecf2aa539ac454815efbfceb8a92822`, Platform
`0f4e27f1f78310bd731c10c19dc51bb1e0345d44`, declaration, native suites,
and all runtime budgets remain unchanged. Only `cancel-execution` is an
authorized proof scenario here. This holds-only subject supplies no attached
native-suite or Story 2.10 qualification credit. The general instructions
below retain their original 4.44.13 subject.

'''
    write(path, first + '\n' + note + rest)

paths = list(dict.fromkeys(paths))
files = {}
for path in paths:
    data = (WORK / path).read_bytes()
    target = OUT / 'changed-files' / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)
    files[path] = hashlib.sha256(data).hexdigest()
(OUT / 'projected-files.json').write_text(json.dumps(files, indent=2) + '\n')
run('stage-projection', ['git', 'add', '--', *paths])
(OUT / 'projection.diff').write_bytes(subprocess.check_output(['git', 'diff', '--cached', '--binary'], cwd=WORK))
source_files = subprocess.check_output(['git', 'ls-tree', '-r', '--name-only', BASE, 'tests/Hexalith.Parties.Platform.IntegrationTests', 'tests/Hexalith.Parties.Platform.LifecycleTests', 'module/hexalith.module-manifest.v2.json'], cwd=WORK, text=True).splitlines()
assert all((WORK / path).read_bytes() == subprocess.check_output(['git', '-C', str(SOURCE), 'show', BASE + ':' + path]) for path in source_files)
(OUT / 'native-inputs-unchanged.json').write_text(json.dumps({'base': BASE, 'paths': {path: hashlib.sha256((WORK / path).read_bytes()).hexdigest() for path in source_files}, 'filesChecked': len(source_files), 'allByteIdentical': True}, indent=2) + '\n')
run('diff-check', ['git', '-c', 'core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol', 'diff', '--check'])
run('actionlint', ['actionlint', '.github/workflows/ci-lifecycle-qualification.yml'])
run('lifecycle-controller-controls', ['python3', '-B', '-m', 'unittest', 'discover', '-s', 'tests/ci-lifecycle', '-v'], required=False)
(OUT / 'prior-unchanged-controls.json').write_text(json.dumps({
    'helperSource': BUILD, 'helperPassedCases': 75,
    'primaryHelperLog': '../ordinary-proof/released-helper-controls.log',
    'observerSha256': files['scripts/story-2-8-proof-observer.py'], 'observerPassedCases': 3,
    'primaryObserverLog': '../ordinary-proof/observer-controls.log',
    'bothImplementationsUnchanged': True,
}, indent=2) + '\n')
ENV['GITHUB_ENV'] = str(Path(os.environ['TMPDIR']) / 'github-env.txt')
run('prepare-real-4482', ['python3', '-B', str(executor / 'Github/prepare-module-build/prepare_module_build.py'), '--manifest', 'module/hexalith.module-manifest.v2.json'])
prepared = dict(line.split('=', 1) for line in Path(ENV['GITHUB_ENV']).read_text().splitlines())
ENV.update(prepared)
ci = 'tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj'
project = 'tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj'
props = ['-p:UseHexalithProjectReferences=false', '-p:UseNuGetDeps=true']
for label, path in [('ci', ci)]:
    run('build-' + label, ['dotnet', 'build', path, '--configuration', 'Release', '--disable-build-servers', '-m:1', '-warnaserror', *props], timeout=1200)
run('owning-workflow-class', ['dotnet', 'test', '--project', ci, '--configuration', 'Release', '--no-build', '--no-restore', *props, '--filter-class', 'Hexalith.Parties.Ci.Tests.PartiesContainerPublishWorkflowTests', '--minimum-expected-tests', '1', '--fail-skips', 'on', '--report-xunit-trx', '--results-directory', str(OUT / 'owning-workflow-class')], required=False)
(OUT / 'blocked-documentation-case.json').write_text(json.dumps({
    'method': 'Hexalith.Parties.Tests.FitnessTests.AppHostTenantsTopologyTests.LocalRunDocumentationUsesPinnedPlatformToolAndDirectReferencesOnly',
    'executed': False, 'passed': False,
    'blocker': 'actual owning project build NU1903-as-error: Microsoft.AspNetCore.SignalR.StackExchangeRedis/10.0.14',
    'primaryBuildLog': '../ordinary-proof/build-parties.log',
    'auditSuppressed': False, 'packagePinsChanged': False,
}, indent=2) + '\n')
print(json.dumps({'base': BASE, 'executor': BUILD, 'files': files, 'results': RESULTS}), flush=True)
raise SystemExit(int(any(value['exitCode'] for value in RESULTS.values())))
