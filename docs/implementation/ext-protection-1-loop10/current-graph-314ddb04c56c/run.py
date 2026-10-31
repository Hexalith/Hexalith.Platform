"""EXT-PROTECTION-1 loop-10 current-graph regression resume (run 314ddb04c56c).

Serial: audit -> bootstrap/prepared-shell and harness unit tests -> two serialized Debug project-reference rebuilds ->
fresh-DLL check -> complete runtime copy + inventory -> direct xUnit DLL class runs -> post-run integrity.
No native OpenBao, Live/OQ, Agents, cluster, production or Git mutation. Usage: python3 -I run.py
"""
import hashlib
import json
import os
from datetime import datetime, timezone
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time

RUN_ID = '314ddb04c56c'
HEX = Path('/home/administrator/projects/hexalith')
PLATFORM = HEX / 'platform'
EVENTSTORE = HEX / 'eventstore'
COMMONS = EVENTSTORE / 'references/Hexalith.Commons'
TENANTS_SRC = EVENTSTORE / 'references/Hexalith.Tenants/src'
SCRATCH = Path('/tmp/claude-1000/-home-administrator-projects-hexalith-platform/b949fb33-be78-47f5-84d2-3974588e5fdf/scratchpad')
ROOT = SCRATCH / ('ext-protection-loop10-current-graph-' + RUN_ID)
SCRIPTS = SCRATCH / ('scripts-' + RUN_ID)
LOOP10 = PLATFORM / 'docs/implementation/ext-protection-1-loop10'
ENVIRONMENT = dict(os.environ, HexalithCommonsRoot=str(COMMONS))
RECORDED_ENVIRONMENT = {'HexalithCommonsRoot': str(COMMONS), 'PATH': ENVIRONMENT.get('PATH'), 'HOME': ENVIRONMENT.get('HOME'),
                        'DOTNET_ROOT': ENVIRONMENT.get('DOTNET_ROOT')}
BUILD_SETTINGS = ['-c', 'Debug', '-nodeReuse:false', '-m:1', '-p:UseHexalithProjectReferences=true', '-p:GeneratePackageOnBuild=false',
                  '-p:HexalithEventStoreRoot=' + str(EVENTSTORE), '-p:HexalithCommonsRoot=' + str(COMMONS),
                  '-p:HexalithTenantsBasePath=' + str(TENANTS_SRC), '--no-incremental']
PLATFORM_CLASSES = ['OpenBaoErasureRootStoreTests', 'ErasureRootCustodyTests', 'CustodyOccurrenceProviderTests', 'ErasureRootUnissuedReplacementTests',
                    'ErasureRootLoop8AdmissionTests', 'ErasureRootReceivingTests']
EVENTSTORE_CLASSES = ['InteractionOccurrenceRegistryActorTests', 'PublishedTrustReplacementAuthorityTests', 'PublishedTrustOwnerStateAuthorityTests',
                      'PublishedTrustDeletionConsumptionAuthorityTests', 'GovernanceScopeGuardTests']
BUILDS = [('platform', PLATFORM, 'tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj', 'Hexalith.Platform.Protection.Tests'),
          ('eventstore', EVENTSTORE, 'tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj', 'Hexalith.EventStore.Server.Tests')]
records = []


def now():
    return datetime.now(timezone.utc).isoformat()


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(name, value):
    (ROOT / name).write_text(json.dumps(value, indent=2) + '\n')


def run(name, argv, cwd, timeout, allow_failure=False):
    log = ROOT / (name + '.log')
    started, start = now(), time.time()
    print('START ' + name + ' ' + started, flush=True)
    timed_out = False
    with log.open('wb') as output:
        try:
            result = subprocess.run(argv, cwd=cwd, env=ENVIRONMENT, stdout=output, stderr=subprocess.STDOUT, timeout=timeout)
            code = result.returncode
        except subprocess.TimeoutExpired:
            timed_out, code = True, None
    record = {'name': name, 'argv': argv, 'cwd': str(cwd), 'environment': RECORDED_ENVIRONMENT,
              'environmentNote': 'Inherited process environment otherwise; only these values are recorded so unrelated credentials are never captured.',
              'startedUtc': started, 'finishedUtc': now(), 'elapsedSeconds': round(time.time() - start, 3), 'timeoutSeconds': timeout,
              'timedOut': timed_out, 'exitCode': code, 'log': str(log), 'logSha256': digest(log)}
    records.append(record)
    write_json('commands.json', records)
    print('END ' + name + ' exit=' + str(code) + (' TIMEOUT' if timed_out else ''), flush=True)
    print('\n'.join(log.read_text(errors='replace').splitlines()[-8:]), flush=True)
    if (code != 0) and not allow_failure:
        write_json('run-status.json', {'completed': False, 'stoppedAt': name, 'exitCode': code, 'timedOut': timed_out, 'recordedUtc': now()})
        raise SystemExit(code or 124)
    return record


def git_state(repository):
    def git(*arguments):
        return subprocess.run(['git', '--no-optional-locks', '-C', str(repository), *arguments], capture_output=True, text=True, env=ENVIRONMENT).stdout
    return {'repository': str(repository), 'head': git('rev-parse', 'HEAD').strip(),
            'statusPorcelain': [line for line in git('status', '--porcelain=v1', '--untracked-files=all').splitlines() if line]}


ROOT_FILES = re.compile(r'^(Directory\..*\.(props|targets)|.*\.props|.*\.targets|global\.json|nuget\.config|NuGet\.config|stylecop\.json|\.editorconfig)$')


def project_dirs_from(log_text):
    directories = set()
    for line in log_text.splitlines():
        match = re.match(r'^\s+\S+ -> (/\S+?)/bin/Debug/[^/]+/[^/]+\.dll$', line)
        if match:
            directories.add(Path(match[1]))
    return directories


def source_inventory(project_directories):
    roots = [PLATFORM, EVENTSTORE, COMMONS, EVENTSTORE / 'references/Hexalith.Tenants', PLATFORM / 'references/Hexalith.Builds',
             EVENTSTORE / 'references/Hexalith.McpCli']
    files = set()
    for root in roots:
        if root.is_dir():
            files.update(path for path in root.iterdir() if path.is_file() and ROOT_FILES.match(path.name))
    builds_src = PLATFORM / 'references/Hexalith.Builds/src'
    directories = set(project_directories) | ({builds_src} if builds_src.is_dir() else set())
    for directory in directories:
        for path in directory.rglob('*'):
            relative = path.relative_to(directory).parts
            if path.is_file() and not ({'bin', 'obj', 'node_modules', '__pycache__'} & set(relative)):
                files.add(path)
    task = [PLATFORM / p for p in ['deploy/erasure-root/bootstrap.py', 'deploy/erasure-root/custody-policy.hcl', 'deploy/erasure-root/statefulset.yaml',
                                   'deploy/erasure-root/kubelet-noswap.fragment.yaml', 'deploy/erasure-root/namespace.yaml', 'deploy/erasure-root/network-policy.yaml',
                                   'deploy/erasure-root/README.md', 'eng/ext-protection-1/test_bootstrap.py', 'eng/ext-protection-1/test_probe_erc_native.py',
                                   'eng/ext-protection-1/probe_erc_native.py']]
    files.update(path for path in task if path.is_file())
    return [{'path': str(path), 'bytes': path.stat().st_size, 'sha256': digest(path)} for path in sorted(files)]


def runtime_inventory(directory):
    return [{'path': str(path.relative_to(directory)), 'bytes': path.stat().st_size, 'sha256': digest(path),
             'mtimeUtc': datetime.fromtimestamp(path.stat().st_mtime, timezone.utc).isoformat()}
            for path in sorted(directory.rglob('*')) if path.is_file()]


def main():
    ROOT.mkdir(parents=True, exist_ok=True)
    marker = {'runId': RUN_ID, 'root': str(ROOT), 'startedUtc': now(), 'pid': os.getpid()}
    write_json('run-marker.json', marker)
    write_json('git-before.json', [git_state(PLATFORM), git_state(EVENTSTORE)])
    concurrent = subprocess.run(['ps', '-eo', 'pid,etime,args'], capture_output=True, text=True).stdout.splitlines()
    write_json('concurrent-processes-before.json', [line for line in concurrent if re.search(r'dotnet (build|test|msbuild)|MSBuild\.dll', line)])
    run('tool-versions', ['bash', '-c', 'dotnet --version; python3 --version; git --version; openssl version'], PLATFORM, 120)

    run('audit', ['python3', '-I', str(SCRIPTS / 'audit.py'), str(ROOT / 'audit.json')], PLATFORM, 300)

    # Predict the build closure from the last recorded loop-10 resume graph so the before-inventory precedes every build.
    prior = LOOP10 / 'ext-protection-loop10-resume-27fe4f343c1a'
    predicted = project_dirs_from((prior / 'platform-build.log.txt').read_text()) | project_dirs_from((prior / 'eventstore-build.log.txt').read_text())
    before = source_inventory(predicted)
    write_json('source-before.json', {'capturedUtc': now(), 'projectDirectories': sorted(map(str, predicted)), 'files': before})
    print('SOURCE-BEFORE ' + str(len(before)), flush=True)

    run('bootstrap-tests', ['python3', '-m', 'unittest', 'discover', '-s', 'eng/ext-protection-1', '-p', 'test_bootstrap.py', '-v'], PLATFORM, 600)
    run('harness-unit-tests', ['python3', '-m', 'unittest', 'discover', '-s', 'eng/ext-protection-1', '-p', 'test_probe_erc_native.py', '-v'], PLATFORM, 300)

    freshness = []
    for name, repository, project, assembly in BUILDS:
        start_epoch = time.time()
        record = run(name + '-build', ['dotnet', 'build', project, *BUILD_SETTINGS], repository, 2700)
        text = Path(record['log']).read_text(errors='replace')
        warnings = re.search(r'^\s*(\d+) Warning\(s\)', text, re.M)
        errors = re.search(r'^\s*(\d+) Error\(s\)', text, re.M)
        outputs = sorted({match for match in re.findall(r' -> (/\S+\.dll)$', text, re.M)})
        output_dir = repository / 'tests' / assembly / 'bin/Debug/net10.0'
        checked = []
        for output in outputs:
            path = Path(output)
            mtime = path.stat().st_mtime
            copy = output_dir / path.name
            present = copy.is_file()
            # Analyzer/tool outputs that are not copy-local are recorded as absent from the test output, not as failures.
            checked.append({'output': output, 'mtimeUtc': datetime.fromtimestamp(mtime, timezone.utc).isoformat(), 'fresh': mtime >= start_epoch,
                            'testOutputCopy': str(copy) if present else None,
                            'testOutputCopyFresh': (copy.stat().st_mtime >= start_epoch) if present else None,
                            'testOutputCopyMatches': (digest(copy) == digest(path)) if present else None})
        freshness.append({'build': name, 'buildStartUtc': datetime.fromtimestamp(start_epoch, timezone.utc).isoformat(),
                          'warnings': int(warnings[1]) if warnings else None, 'errors': int(errors[1]) if errors else None,
                          'projectOutputs': checked,
                          'allFresh': bool(checked) and all(c['fresh'] and c['testOutputCopyFresh'] is not False and c['testOutputCopyMatches'] is not False for c in checked)})
        write_json('build-freshness.json', freshness)
        # Warnings are recorded and reported; only errors, a nonzero exit or a stale/mismatched output stop execution.
        if not freshness[-1]['allFresh'] or freshness[-1]['errors'] != 0:
            write_json('run-status.json', {'completed': False, 'stoppedAt': name + '-build-verification', 'recordedUtc': now()})
            raise SystemExit(3)
        # Copy the complete output immediately and inventory both sides before any execution.
        destination = ROOT / 'runtime' / name
        source_files = runtime_inventory(output_dir)
        shutil.copytree(output_dir, destination, copy_function=shutil.copy2)
        copied = runtime_inventory(destination)
        if [(f['path'], f['sha256']) for f in copied] != [(f['path'], f['sha256']) for f in source_files]:
            write_json('run-status.json', {'completed': False, 'stoppedAt': name + '-runtime-copy', 'recordedUtc': now()})
            raise SystemExit(4)
        write_json(name + '-runtime.json', {'runtime': str(destination), 'copiedFrom': str(output_dir), 'capturedUtc': now(), 'files': copied})
        print('RUNTIME ' + name + ' files=' + str(len(copied)) + ' manifest=' + digest(ROOT / (name + '-runtime.json')), flush=True)

    actual = set()
    for name, _, _, _ in BUILDS:
        actual |= project_dirs_from((ROOT / (name + '-build.log')).read_text(errors='replace'))
    write_json('build-closure.json', {'predicted': sorted(map(str, predicted)), 'actual': sorted(map(str, actual)),
                                      'unpredicted': sorted(map(str, actual - predicted)), 'notBuilt': sorted(map(str, predicted - actual))})

    results = []
    for name, repository, _, assembly in BUILDS:
        namespace = 'Hexalith.Platform.Protection.Tests' if name == 'platform' else 'Hexalith.EventStore.Server.Tests.Security'
        classes = PLATFORM_CLASSES if name == 'platform' else EVENTSTORE_CLASSES
        argv = ['dotnet', str(ROOT / 'runtime' / name / (assembly + '.dll')), '-parallel', 'none', '-xml', str(ROOT / (name + '-tests.xml'))]
        for selected in classes:
            argv += ['-class', namespace + '.' + selected]
        results.append(run(name + '-tests', argv, repository, 1800, allow_failure=True))

    # Post-run integrity: runtime unchanged, source changes during the run, closure additions, and Git observations.
    integrity = {'recordedUtc': now(), 'runtime': [], 'sourceChangedDuringRun': [], 'sourceAddedDuringRunInPredictedScope': []}
    for name, _, _, _ in BUILDS:
        manifest = json.loads((ROOT / (name + '-runtime.json')).read_text())
        current = runtime_inventory(ROOT / 'runtime' / name)
        integrity['runtime'].append({'repository': name, 'files': len(manifest['files']),
                                     'unchanged': [(f['path'], f['sha256']) for f in current] == [(f['path'], f['sha256']) for f in manifest['files']]})
    after = {entry['path']: entry['sha256'] for entry in source_inventory(predicted | actual)}
    previous = {entry['path']: entry['sha256'] for entry in before}
    integrity['sourceChangedDuringRun'] = sorted(path for path, value in previous.items() if after.get(path) != value)
    integrity['sourceAddedDuringRunInPredictedScope'] = sorted(path for path in after if path not in previous and any(Path(path).is_relative_to(d) for d in predicted))
    write_json('source-after.json', {'capturedUtc': now(), 'files': [{'path': p, 'sha256': h} for p, h in sorted(after.items())]})
    write_json('git-after.json', [git_state(PLATFORM), git_state(EVENTSTORE)])
    write_json('post-run-integrity.json', integrity)
    write_json('run-status.json', {'completed': True, 'testExitCodes': {r['name']: r['exitCode'] for r in results}, 'recordedUtc': now()})
    print('COMPLETE ' + json.dumps(integrity['runtime']) + ' changed=' + str(len(integrity['sourceChangedDuringRun'])), flush=True)


if __name__ == '__main__':
    main()
