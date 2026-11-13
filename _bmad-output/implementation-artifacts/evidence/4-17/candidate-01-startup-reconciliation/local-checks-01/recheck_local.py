"""Read-only GitHub inventory and local checks; never dispatches or changes pins.

Run from the Platform root:
python3 -I PATH/recheck_local.py /absolute/path/to/EventStore /absolute/path/to/node_modules
Only this script's directory and disposable scratch files are written.
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
OUT = Path(__file__).resolve().parent
OWNER = Path(sys.argv[1]).resolve()
MODULES = Path(sys.argv[2]).resolve()
PIN = '02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213'
SOURCE = '94001fad925fdf7fa03a90b507b8f8e19074f9e3'
PROFILE = 'deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json'
rows = []


def run(name, argv, cwd=ROOT):
    result = subprocess.run(argv, cwd=cwd, capture_output=True, check=False)
    logs = {}
    for field in ('stdout', 'stderr'):
        data = getattr(result, field)
        path = OUT / (name + '.' + field)
        path.write_bytes(data)
        logs[field] = {'path': path.name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}
    rows.append({'name': name, 'argv': argv, 'cwd': str(cwd), 'exitCode': result.returncode, **logs})
    (OUT / 'local-commands.json').write_text(json.dumps(rows, indent=2) + '\n')
    return result


def git(*args, cwd=ROOT):
    return subprocess.check_output(['git', *args], cwd=cwd).decode().strip()


identities = {'observedUtc': datetime.now(timezone.utc).isoformat(),
              'platformBaseline': git('rev-parse', 'HEAD'),
              'currentEventStoreCheckout': git('rev-parse', 'HEAD', cwd=OWNER),
              'currentPlatformGitlink': git('ls-tree', 'HEAD', 'references/Hexalith.EventStore'),
              'executionPlatformRevision': git('rev-parse', SOURCE + '^{commit}'),
              'executionEventStoreRevision': git('rev-parse', PIN + '^{commit}', cwd=OWNER),
              'executionGitlink': git('ls-tree', SOURCE, 'references/Hexalith.EventStore'),
              'workflowBlob': git('rev-parse', SOURCE + ':.github/workflows/broker-candidate-qualification.yml')}
for directory in ('candidate-01-hosted', 'candidate-01-startup-investigation',
                  'candidate-01-hosted-attempt-2', 'candidate-01-packet-diagnosis'):
    verifier = ROOT / '_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/verify-manifest.py'
    manifest = OUT.parent / directory / 'file-manifest.json'
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
    pinned_workflow = scratch / 'broker-candidate-qualification.yml'
    pinned_workflow.write_bytes(subprocess.check_output(['git', 'show', SOURCE + ':.github/workflows/broker-candidate-qualification.yml'], cwd=ROOT))
    assert run('actionlint-execution-source', ['actionlint', '-no-color', '-shellcheck=', '-pyflakes=', str(pinned_workflow)]).returncode == 0
    identities['workflowIdenticalAtBaseline'] = pinned_workflow.read_bytes() == (ROOT / '.github/workflows/broker-candidate-qualification.yml').read_bytes()
    identities['configurationSha256'] = hashlib.sha256((ROOT / 'docs/implementation/story-4-17-broker-candidate-01.json').read_bytes()).hexdigest()
    identities['qualificationProfileSha256'] = hashlib.sha256((isolated / PROFILE).read_bytes()).hexdigest()
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
    config = ROOT / 'docs/implementation/story-4-17-broker-candidate-01.json'
    for name, target, expected in [('admission-pinned', isolated, 0), ('admission-current-checkout', OWNER, 1)]:
        result = run(name, ['python3', '-B', '-c', code, str(isolated / PROFILE), str(target), str(config)], isolated)
        assert result.returncode == expected, (name, result.returncode)

assert git('rev-parse', 'HEAD', cwd=OWNER) == identities['currentEventStoreCheckout']
assert run('diff-check', ['git', 'diff', '--check']).returncode == 0
(OUT / 'source-identities.json').write_text(json.dumps(identities, indent=2) + '\n')
print(json.dumps({'checks': len(rows), 'identities': identities}, indent=2))
