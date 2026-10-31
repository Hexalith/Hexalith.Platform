"""EXT-PROTECTION-1 loop-10 deferred-row verification (run af01b493fa91), one phase per invocation.

Usage: python3 -I phase.py <phase-a|phase-b|final>
  phase-a: regression tests + test-only observation point, before any fix (failures expected and recorded as observed)
  phase-b: the same selection after the fixes
  final:   the full required class sets from a fresh inventoried runtime
Serial Debug project-reference rebuilds with --no-incremental, fresh-DLL check, complete runtime copy and inventory before execution,
direct xUnit DLL runs. No native OpenBao, Live/OQ, Agents, cluster, production or Git mutation.
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

RUN_ID = 'af01b493fa91'
PHASE = sys.argv[1]
assert PHASE in ('phase-a', 'phase-b', 'final'), PHASE
HEX = Path('/home/administrator/projects/hexalith')
PLATFORM = HEX / 'platform'
EVENTSTORE = HEX / 'eventstore'
COMMONS = EVENTSTORE / 'references/Hexalith.Commons'
TENANTS_SRC = EVENTSTORE / 'references/Hexalith.Tenants/src'
SCRATCH = Path('/tmp/claude-1000/-home-administrator-projects-hexalith-platform/b949fb33-be78-47f5-84d2-3974588e5fdf/scratchpad')
ROOT = SCRATCH / ('ext-protection-loop10-deferred-rows-' + RUN_ID) / PHASE
ENVIRONMENT = dict(os.environ, HexalithCommonsRoot=str(COMMONS))
RECORDED_ENVIRONMENT = {'HexalithCommonsRoot': str(COMMONS), 'PATH': ENVIRONMENT.get('PATH'), 'HOME': ENVIRONMENT.get('HOME'), 'DOTNET_ROOT': ENVIRONMENT.get('DOTNET_ROOT')}
BUILD_SETTINGS = ['-c', 'Debug', '-nodeReuse:false', '-m:1', '-p:UseHexalithProjectReferences=true', '-p:GeneratePackageOnBuild=false',
                  '-p:HexalithEventStoreRoot=' + str(EVENTSTORE), '-p:HexalithCommonsRoot=' + str(COMMONS),
                  '-p:HexalithTenantsBasePath=' + str(TENANTS_SRC), '--no-incremental']
# The 6 Platform and 5 EventStore classes from current-graph-314ddb04c56c/commands.json, plus classes covering every file changed here.
REQUIRED_PLATFORM = ['OpenBaoErasureRootStoreTests', 'ErasureRootCustodyTests', 'CustodyOccurrenceProviderTests', 'ErasureRootUnissuedReplacementTests',
                     'ErasureRootLoop8AdmissionTests', 'ErasureRootReceivingTests']
REQUIRED_EVENTSTORE = ['InteractionOccurrenceRegistryActorTests', 'PublishedTrustReplacementAuthorityTests', 'PublishedTrustOwnerStateAuthorityTests',
                       'PublishedTrustDeletionConsumptionAuthorityTests', 'GovernanceScopeGuardTests']
# ErasureRootLoop3ReceivingTests backs R11-BH-6 and exercises the changed authority handler; DeletionConsumption* cover the changed authority's consumers.
EXTRA_PLATFORM = ['ErasureRootLoop3ReceivingTests']
EXTRA_EVENTSTORE = ['DeletionConsumptionActorTests', 'DeletionConsumptionOriginalTests']
if PHASE == 'final':
    PLATFORM_CLASSES, EVENTSTORE_CLASSES = REQUIRED_PLATFORM + EXTRA_PLATFORM, REQUIRED_EVENTSTORE + EXTRA_EVENTSTORE
else:
    # Focused before/after selection: every class holding a deferred-row or R11-BH-9 test.
    PLATFORM_CLASSES = ['OpenBaoErasureRootStoreTests', 'ErasureRootCustodyTests', 'ErasureRootReceivingTests', 'ErasureRootUnissuedReplacementTests', 'ErasureRootLoop3ReceivingTests']
    EVENTSTORE_CLASSES = ['PublishedTrustReplacementAuthorityTests', 'InteractionOccurrenceRegistryActorTests', 'PublishedTrustOwnerStateAuthorityTests']
BUILDS = [('platform', PLATFORM, 'tests/Hexalith.Platform.Protection.Tests/Hexalith.Platform.Protection.Tests.csproj', 'Hexalith.Platform.Protection.Tests'),
          ('eventstore', EVENTSTORE, 'tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj', 'Hexalith.EventStore.Server.Tests')]
TASK_FILES = [PLATFORM / p for p in ['deploy/erasure-root/bootstrap.py', 'eng/ext-protection-1/test_bootstrap.py', 'src/Hexalith.Platform.Protection/OpenBaoErasureRootStore.cs',
                                     'src/Hexalith.Platform.Protection/ErasureRootAuthorityOperationHandler.cs', 'tests/Hexalith.Platform.Protection.Tests/OpenBaoErasureRootStoreTests.cs',
                                     'tests/Hexalith.Platform.Protection.Tests/ErasureRootCustodyTests.cs', 'tests/Hexalith.Platform.Protection.Tests/ErasureRootReceivingTests.cs']] + \
             [EVENTSTORE / p for p in ['src/Hexalith.EventStore.Server/Security/PublishedTrustDeletionConsumptionAuthority.cs',
                                       'tests/Hexalith.EventStore.Server.Tests/Security/PublishedTrustReplacementAuthorityTests.cs',
                                       'tests/Hexalith.EventStore.Server.Tests/Security/InteractionOccurrenceRegistryActorTests.cs',
                                       'tests/Hexalith.EventStore.Server.Tests/Security/PublishedTrustOwnerStateAuthorityTests.cs']]
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
            code = subprocess.run(argv, cwd=cwd, env=ENVIRONMENT, stdout=output, stderr=subprocess.STDOUT, timeout=timeout).returncode
        except subprocess.TimeoutExpired:
            timed_out, code = True, None
    record = {'name': name, 'phase': PHASE, 'argv': argv, 'cwd': str(cwd), 'environment': RECORDED_ENVIRONMENT,
              'environmentNote': 'Inherited process environment otherwise; only these values are recorded so unrelated credentials are never captured.',
              'startedUtc': started, 'finishedUtc': now(), 'elapsedSeconds': round(time.time() - start, 3), 'timeoutSeconds': timeout,
              'timedOut': timed_out, 'exitCode': code, 'log': str(log), 'logSha256': digest(log)}
    records.append(record)
    write_json('commands.json', records)
    print('END ' + name + ' exit=' + str(code) + (' TIMEOUT' if timed_out else ''), flush=True)
    print('\n'.join(log.read_text(errors='replace').splitlines()[-6:]), flush=True)
    if code != 0 and not allow_failure:
        write_json('run-status.json', {'completed': False, 'stoppedAt': name, 'exitCode': code, 'timedOut': timed_out, 'recordedUtc': now()})
        raise SystemExit(code or 124)
    return record


def runtime_inventory(directory):
    return [{'path': str(path.relative_to(directory)), 'bytes': path.stat().st_size, 'sha256': digest(path),
             'mtimeUtc': datetime.fromtimestamp(path.stat().st_mtime, timezone.utc).isoformat()}
            for path in sorted(directory.rglob('*')) if path.is_file()]


def task_sources():
    return [{'path': str(path), 'bytes': path.stat().st_size, 'sha256': digest(path)} for path in TASK_FILES]


def main():
    assert not ROOT.exists(), 'phase directory already exists'
    ROOT.mkdir(parents=True)
    write_json('run-marker.json', {'runId': RUN_ID, 'phase': PHASE, 'root': str(ROOT), 'startedUtc': now()})
    write_json('task-sources-before.json', task_sources())
    run('bootstrap-tests', ['python3', '-m', 'unittest', 'discover', '-s', 'eng/ext-protection-1', '-p', 'test_bootstrap.py', '-v'], PLATFORM, 600, allow_failure=True)
    freshness = []
    for name, repository, project, assembly in BUILDS:
        start_epoch = time.time()
        record = run(name + '-build', ['dotnet', 'build', project, *BUILD_SETTINGS], repository, 2700)
        text = Path(record['log']).read_text(errors='replace')
        warnings = re.search(r'^\s*(\d+) Warning\(s\)', text, re.M)
        errors = re.search(r'^\s*(\d+) Error\(s\)', text, re.M)
        output_dir = repository / 'tests' / assembly / 'bin/Debug/net10.0'
        checked = []
        for output in sorted(set(re.findall(r' -> (/\S+\.dll)$', text, re.M))):
            path = Path(output)
            copy = output_dir / path.name
            present = copy.is_file()
            checked.append({'output': output, 'mtimeUtc': datetime.fromtimestamp(path.stat().st_mtime, timezone.utc).isoformat(),
                            'fresh': path.stat().st_mtime >= start_epoch, 'testOutputCopy': str(copy) if present else None,
                            'testOutputCopyFresh': (copy.stat().st_mtime >= start_epoch) if present else None,
                            'testOutputCopyMatches': (digest(copy) == digest(path)) if present else None})
        freshness.append({'build': name, 'buildStartUtc': datetime.fromtimestamp(start_epoch, timezone.utc).isoformat(),
                          'warnings': int(warnings[1]) if warnings else None, 'errors': int(errors[1]) if errors else None, 'projectOutputs': checked,
                          'allFresh': bool(checked) and all(c['fresh'] and c['testOutputCopyFresh'] is not False and c['testOutputCopyMatches'] is not False for c in checked)})
        write_json('build-freshness.json', freshness)
        if not freshness[-1]['allFresh'] or freshness[-1]['errors'] != 0:
            write_json('run-status.json', {'completed': False, 'stoppedAt': name + '-build-verification', 'recordedUtc': now()})
            raise SystemExit(3)
        destination = ROOT / 'runtime' / name
        source_files = runtime_inventory(output_dir)
        shutil.copytree(output_dir, destination, copy_function=shutil.copy2)
        copied = runtime_inventory(destination)
        if [(f['path'], f['sha256']) for f in copied] != [(f['path'], f['sha256']) for f in source_files]:
            write_json('run-status.json', {'completed': False, 'stoppedAt': name + '-runtime-copy', 'recordedUtc': now()})
            raise SystemExit(4)
        write_json(name + '-runtime.json', {'runtime': str(destination), 'copiedFrom': str(output_dir), 'capturedUtc': now(), 'files': copied})
        print('RUNTIME ' + name + ' files=' + str(len(copied)), flush=True)
    results = {}
    for name, repository, _, assembly in BUILDS:
        namespace = 'Hexalith.Platform.Protection.Tests' if name == 'platform' else 'Hexalith.EventStore.Server.Tests.Security'
        argv = ['dotnet', str(ROOT / 'runtime' / name / (assembly + '.dll')), '-parallel', 'none', '-xml', str(ROOT / (name + '-tests.xml'))]
        for selected in (PLATFORM_CLASSES if name == 'platform' else EVENTSTORE_CLASSES):
            argv += ['-class', namespace + '.' + selected]
        results[name] = run(name + '-tests', argv, repository, 1800, allow_failure=True)['exitCode']
    integrity = {'recordedUtc': now(), 'runtime': [], 'taskSourcesUnchangedDuringPhase': task_sources() == json.loads((ROOT / 'task-sources-before.json').read_text())}
    for name, _, _, _ in BUILDS:
        manifest = json.loads((ROOT / (name + '-runtime.json')).read_text())
        current = runtime_inventory(ROOT / 'runtime' / name)
        integrity['runtime'].append({'repository': name, 'files': len(manifest['files']),
                                     'unchanged': [(f['path'], f['sha256']) for f in current] == [(f['path'], f['sha256']) for f in manifest['files']]})
    write_json('post-run-integrity.json', integrity)
    write_json('run-status.json', {'completed': True, 'testExitCodes': results, 'recordedUtc': now()})
    print('COMPLETE ' + json.dumps(integrity), flush=True)


if __name__ == '__main__':
    main()
