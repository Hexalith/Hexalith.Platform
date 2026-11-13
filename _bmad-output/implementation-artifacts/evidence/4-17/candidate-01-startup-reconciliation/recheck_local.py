"""Read-only GitHub inventory and local checks; never dispatches or changes pins.

Run from the Platform root:
python3 -I PATH/recheck_local.py EVENTSTORE_REPOSITORY NODE_MODULES NEW_OUTPUT_DIRECTORY
Only the fresh output directory and disposable scratch files are written.
"""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
from datetime import datetime, timezone

ROOT = Path.cwd()
OUT = Path(sys.argv[3]).resolve()
OUT.mkdir(exist_ok=False)
EVIDENCE = Path(__file__).resolve().parent
OWNER = Path(sys.argv[1]).resolve()
MODULES = Path(sys.argv[2]).resolve()
PIN = '02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213'
SOURCE = '94001fad925fdf7fa03a90b507b8f8e19074f9e3'
SESSION_START_CHECKOUT = 'f2062f7f7bd8b3dcae946ba209b933ad8e610ff2'
PROFILE = 'deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json'
rows = []


def run(name, argv, cwd=ROOT):
    started = datetime.now(timezone.utc).isoformat()
    result = subprocess.run(argv, cwd=cwd, capture_output=True, check=False)
    finished = datetime.now(timezone.utc).isoformat()
    logs = {}
    for field in ('stdout', 'stderr'):
        data = getattr(result, field)
        path = OUT / (name + '.' + field)
        path.write_bytes(data)
        logs[field] = {'path': path.name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}
    rows.append({'name': name, 'argv': argv, 'cwd': str(cwd), 'startedUtc': started,
                 'finishedUtc': finished, 'exitCode': result.returncode, **logs})
    (OUT / 'local-commands.json').write_text(json.dumps(rows, indent=2) + '\n')
    return result


def git(*args, cwd=ROOT):
    result = run('git-read-' + str(len(rows) + 1), ['git', *args], cwd)
    assert result.returncode == 0
    return result.stdout.decode().strip()


identities = {'observedUtc': datetime.now(timezone.utc).isoformat(),
              'platformBaseline': git('rev-parse', 'HEAD'),
              'eventStoreCheckoutAtAuditStart': git('rev-parse', 'HEAD', cwd=OWNER),
              'sessionStartCheckoutSnapshot': git('rev-parse', SESSION_START_CHECKOUT + '^{commit}', cwd=OWNER),
              'currentPlatformGitlink': git('ls-tree', 'HEAD', 'references/Hexalith.EventStore'),
              'executionPlatformRevision': git('rev-parse', SOURCE + '^{commit}'),
              'executionEventStoreRevision': git('rev-parse', PIN + '^{commit}', cwd=OWNER),
              'executionGitlink': git('ls-tree', SOURCE, 'references/Hexalith.EventStore'),
              'workflowBlob': git('rev-parse', SOURCE + ':.github/workflows/broker-candidate-qualification.yml')}
(OUT / 'source-identities.initial.json').write_text(json.dumps(identities, indent=2) + '\n')
for directory in ('candidate-01-hosted', 'candidate-01-startup-investigation',
                  'candidate-01-hosted-attempt-2', 'candidate-01-packet-diagnosis'):
    verifier = ROOT / '_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/verify-manifest.py'
    manifest = EVIDENCE.parent / directory / 'file-manifest.json'
    assert run('manifest-' + directory, ['python3', '-I', str(verifier), str(manifest)]).returncode == 0

inventory = run('workflow-inventory', ['gh', 'api', '--method', 'GET',
    'repos/Hexalith/Hexalith.Platform/actions/workflows/415133929/runs?per_page=100', '--jq',
    '{total_count,workflow_runs:[.workflow_runs[]|{id,head_sha,head_branch,event,status,conclusion,run_attempt}]}'])
assert inventory.returncode == 0

link = ROOT / 'node_modules'
created_link = not link.exists() and not link.is_symlink()
if created_link:
    link.symlink_to(MODULES, target_is_directory=True)
try:
    assert run('broker-workflow-tests', ['node', '--test', 'tests/source-release/broker-candidate-workflow.test.mjs']).returncode == 0
finally:
    if created_link:
        link.unlink()
assert run('actionlint-version', ['actionlint', '-version']).returncode == 0
assert run('actionlint-current', ['actionlint', '-no-color', '-shellcheck=', '-pyflakes=',
    '.github/workflows/broker-candidate-qualification.yml']).returncode == 0

with tempfile.TemporaryDirectory(prefix='candidate-01-pinned-admission-') as scratch:
    scratch = Path(scratch)
    archive = scratch / 'eventstore.tar'
    assert run('archive-pinned-source', ['git', 'archive', '--format=tar', '--output=' + str(archive), PIN], OWNER).returncode == 0
    isolated = scratch / 'eventstore'
    isolated.mkdir()
    assert run('extract-local-git-archive', ['tar', '-xf', str(archive), '-C', str(isolated)]).returncode == 0
    newer_archive = scratch / 'session-start-checkout.tar'
    assert run('archive-session-start-checkout', ['git', 'archive', '--format=tar', '--output=' + str(newer_archive), SESSION_START_CHECKOUT], OWNER).returncode == 0
    newer = scratch / 'session-start-checkout'
    newer.mkdir()
    assert run('extract-session-start-archive', ['tar', '-xf', str(newer_archive), '-C', str(newer)]).returncode == 0
    pinned_workflow = scratch / 'broker-candidate-qualification.yml'
    workflow_read = run('read-execution-workflow', ['git', 'show', SOURCE + ':.github/workflows/broker-candidate-qualification.yml'])
    assert workflow_read.returncode == 0
    pinned_workflow.write_bytes(workflow_read.stdout)
    assert run('actionlint-execution-source', ['actionlint', '-no-color', '-shellcheck=', '-pyflakes=', str(pinned_workflow)]).returncode == 0
    identities['workflowIdenticalAtBaseline'] = pinned_workflow.read_bytes() == (ROOT / '.github/workflows/broker-candidate-qualification.yml').read_bytes()
    assert identities['workflowIdenticalAtBaseline'], 'baseline controls must target identical execution workflow bytes'
    configuration_read = run('read-execution-configuration', ['git', 'show', SOURCE + ':docs/implementation/story-4-17-broker-candidate-01.json'])
    assert configuration_read.returncode == 0
    config = scratch / 'original-platform-config.json'
    config.write_bytes(configuration_read.stdout)
    identities['configurationSha256'] = hashlib.sha256(config.read_bytes()).hexdigest()
    assert identities['configurationSha256'] == '472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc'
    assert config.read_bytes() == (ROOT / 'docs/implementation/story-4-17-broker-candidate-01.json').read_bytes()
    identities['qualificationProfileSha256'] = hashlib.sha256((isolated / PROFILE).read_bytes()).hexdigest()
    assert identities['qualificationProfileSha256'] == '1c1da59176f78b9b60a87ba85b6076b8d215ff912840ecb52bdb7432497c587f'
    code = '''import hashlib, json, sys
from pathlib import Path
sys.path.insert(0, 'tools')
from dapr_broker_qualification import BROKERS, SCENARIOS, validate_profile
from dapr_platform_broker_configuration import load_configuration
from dapr_runtime_qualification import source_input
profile = json.loads(Path(sys.argv[1]).read_text())
root = Path(sys.argv[2])
drift = []
for name, expected in profile['inputs'].items():
    try:
        path = source_input(profile, root, name, expected)
        actual = hashlib.sha256(path.read_bytes()).hexdigest()
        if actual != expected:
            drift.append({'path': name, 'expected': expected, 'actual': actual})
    except (ValueError, OSError) as error:
        drift.append({'path': name, 'expected': expected, 'error': str(error)})
print(json.dumps({'lockedInputs': len(profile['inputs']), 'brokers': BROKERS, 'scenarios': SCENARIOS, 'vectors': len(BROKERS)*len(SCENARIOS), 'drift': drift}, indent=2))
try:
    validate_profile(profile, root)
    load_configuration(Path(sys.argv[3]))
except ValueError as error:
    print('FAIL: ' + str(error))
    raise SystemExit(1)
print('PASS: owner profile and original configuration admission')
'''
    for name, target, expected in [('admission-pinned', isolated, 0), ('admission-session-start-checkout', newer, 1)]:
        result = run(name, ['python3', '-B', '-c', code, str(isolated / PROFILE), str(target), str(config)], isolated)
        assert result.returncode == expected, (name, result.returncode)
        value, end = json.JSONDecoder().raw_decode(result.stdout.decode())
        assert value['lockedInputs'] == 91 and value['vectors'] == 24
        assert value['brokers'] == ['rabbitmq', 'kafka'] and len(value['scenarios']) == 12
        if name == 'admission-pinned':
            assert value['drift'] == []
            assert result.stdout.decode()[end:].strip() == 'PASS: owner profile and original configuration admission'
        else:
            assert len(value['drift']) == 4
            assert {row['path'] for row in value['drift']} == {
                '.github/workflows/dapr-runtime-qualification.yml',
                'tools/qualify-internal-actor-admission.py', 'tools/run-dapr-runtime-lane.py',
                'tools/tests/test_qualify_internal_actor_admission.py'}
            assert all('actual' in row and row['actual'] != row['expected'] for row in value['drift'])
            assert result.stdout.decode()[end:].strip() == 'FAIL: inputs..github/workflows/dapr-runtime-qualification.yml: source/component bytes drifted'

identities['eventStoreCheckoutAtAuditEnd'] = git('rev-parse', 'HEAD', cwd=OWNER)
identities['sharedCheckoutUsedForAdmission'] = False
assert run('diff-check', ['git', 'diff', '--check']).returncode == 0
(OUT / 'source-identities.json').write_text(json.dumps(identities, indent=2) + '\n')
print(json.dumps({'checks': len(rows), 'identities': identities}, indent=2))
