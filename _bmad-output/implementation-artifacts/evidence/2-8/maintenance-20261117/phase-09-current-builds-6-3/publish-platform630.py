"""Register the exact-producer regression and open the supported current upgrade for normal CI."""
import hashlib
import json
import os
from pathlib import Path
import subprocess

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-current-20261117'
OUT = AREA / 'platform630-source-03'
OUT.mkdir(exist_ok=False)
REPO = Path(os.environ['TMPDIR']) / 'Hexalith.Platform'
BUILDS = REPO.parent / 'Hexalith.Builds'
BASE = '11719c5cf51c2b2b080b89e0e301c2ef8dbacc46'
SOURCE = '7afc4981e21bc2fcce687199a3fd2c61fe87336e'
BRANCH = 'fix/story-2-8-builds-6-3'
PREVIOUS = json.loads((AREA / 'platform630-source-02/result.json').read_text())
assert PREVIOUS['status'] == 'prepared-and-normal-artifact-path-passed'
NODE = Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV = dict(os.environ, PATH=str(NODE.parent) + ':' + os.environ['PATH'], PYTHONDONTWRITEBYTECODE='1')
RECORD = {'base': BASE, 'buildsSource': SOURCE, 'buildsVersion': '6.3.0', 'remoteMutations': [], 'commands': []}


def run(label, argv, cwd=REPO, payload=None, timeout=300, required=True):
    result = subprocess.run(argv, cwd=cwd, env=ENV, input=payload, capture_output=True, text=True, timeout=timeout)
    (OUT / (label + '.log')).write_text(result.stdout + result.stderr)
    RECORD['commands'].append({'label': label, 'exitCode': result.returncode})
    print(label, result.returncode, flush=True)
    if required and result.returncode:
        raise RuntimeError(label)
    return result


def git(label, *args):
    return run(label, ['git', '-c', 'submodule.recurse=false', *args]).stdout.strip()


try:
    run('clone', ['git', 'clone', '--shared', '--no-checkout', str(ROOT), str(REPO)], ROOT)
    git('origin', 'remote', 'set-url', 'origin', 'https://github.com/Hexalith/Hexalith.Platform.git')
    git('recover', 'fetch', str(AREA / 'platform630-source-02/Platform.bundle'), 'refs/heads/' + BRANCH)
    git('checkout', 'checkout', '-b', BRANCH, PREVIOUS['source'])
    workflow = REPO / '.github/workflows/tuple-ci.yml'
    text = workflow.read_text()
    anchor = '          python3 -B eng/test_builds_publication.py\n'
    assert text.count(anchor) == 1
    workflow.write_text(text.replace(anchor, anchor + '          python3 -B eng/test_bound_builds.py\n'))
    run('clone-builds', ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references/Hexalith.Builds'), str(BUILDS)])
    run('fetch-builds', ['git', 'fetch', '--no-recurse-submodules', 'https://github.com/Hexalith/Hexalith.Builds.git', SOURCE], BUILDS)
    run('checkout-builds', ['git', 'checkout', '--detach', SOURCE], BUILDS)
    for label, script in [('bound-producer-controls', 'test_bound_builds.py'), ('tuple-controls', 'test_builds_tuple.py'),
                          ('publication-controls', 'test_builds_publication.py')]:
        run(label, ['python3', '-B', 'eng/' + script])
    run('npm-ci', ['npm', 'ci', '--ignore-scripts', '--no-fund'])
    run('signatures', ['npm', 'audit', 'signatures'])
    message = 'fix(ci): run exact producer regression in the tuple gate\n'
    (OUT / 'commit-message.txt').write_text(message)
    run('commitlint', [str(NODE), str(REPO / 'node_modules/@commitlint/cli/cli.js'), '--config', str(REPO / 'commitlint.config.mjs'), '--color=false'], payload=message)
    git('stage', 'add', '--', '.github/workflows/tuple-ci.yml')
    git('diff-check', 'diff', '--cached', '--check')
    git('commit', 'commit', '-F', str(OUT / 'commit-message.txt'))
    RECORD.update(source=git('source', 'rev-parse', 'HEAD'), tree=git('tree', 'rev-parse', 'HEAD^{tree}'), branch=BRANCH)
    git('source-diff', 'diff', '--binary', BASE, 'HEAD')
    git('bundle', 'bundle', 'create', str(OUT / 'Platform.bundle'), 'refs/heads/' + BRANCH, '^' + BASE)
    catalog = BUILDS / 'Props/Directory.Packages.props'
    before = catalog.read_bytes()
    assert b'\r\n' in before
    run('normal-published-artifact-preparation', ['python3', '-B', 'eng/prepare_builds_artifacts.py', '--output',
        str(REPO.parent / 'verified-publication'), '--require-recorded'], timeout=300)
    identity_path = REPO.parent / 'verified-publication/artifact-identity.json'
    identity = json.loads(identity_path.read_bytes())
    (OUT / 'artifact-identity.json').write_bytes(identity_path.read_bytes())
    assert identity['version'] == '6.3.0' and identity['platformRevision'] == RECORD['source']
    assert identity['buildsRevision'] == SOURCE and not identity['platformSourceState']['dirty']
    exact = Path(identity['boundProducerContext']['buildsRoot'])
    assert not exact.exists(), 'The private exact producer must be cleaned after verification.'
    assert before == catalog.read_bytes()
    assert run('builds-clean-after', ['git', 'status', '--porcelain'], BUILDS).stdout == ''
    assert not (BUILDS / '.git/info/attributes').exists()
    (OUT / 'scratch-cleanup.json').write_text(json.dumps({'privateProducerRemoved': True,
        'normalCrlfCheckoutUnchanged': True, 'normalCheckoutAttributesUnchanged': True,
        'nativeServicesStarted': False}, indent=2) + '\n')
    # Only the intended Builds gitlink may change among the owner's eighteen direct pins.
    def pins(revision):
        rows = git('pins-' + revision[:8], 'ls-tree', '-r', revision).splitlines()
        return {row.split('\t', 1)[1]: row.split()[2] for row in rows if row.startswith('160000 ')}
    old_pins, new_pins = pins(BASE), pins(RECORD['source'])
    assert len(old_pins) == len(new_pins) == 18
    assert {key for key in old_pins if old_pins[key] != new_pins[key]} == {'references/Hexalith.Builds'}
    assert new_pins['references/Hexalith.Builds'] == SOURCE
    (OUT / 'direct-pin-audit.json').write_text(json.dumps({'before': old_pins, 'after': new_pins,
        'unchangedOtherPins': 17, 'changedOnly': ['references/Hexalith.Builds']}, indent=2) + '\n')
    remote = git('remote-before-push', 'ls-remote', '--heads', 'origin', 'main', BRANCH).splitlines()
    assert remote == [BASE + '\trefs/heads/main'], remote
    RECORD['status'] = 'validated-source-ready-for-normal-pr'
    (OUT / 'result.json').write_text(json.dumps(RECORD, indent=2) + '\n')
    git('push', 'push', '--no-recurse-submodules', 'origin', 'HEAD:refs/heads/' + BRANCH)
    RECORD['remoteMutations'].append({'action': 'push-new-source-branch', 'sha': RECORD['source']})
    body = ('Bind the supported Platform 2.x line to the signed Builds 6.3.0 release: exact tool pin, '
        'range floor, publication handoff, catalog and Builds gitlink. Update the current preparation documentation.\n\n'
        'Normal recorded artifact preparation now acquires a private checkout of the bound producer, '
        'preserving exact committed catalog bytes despite the producer’s normal CRLF checkout. '
        'The final guard rechecks that same producer, and the temporary checkout is removed on exit. '
        'Register the real Git regression in the tuple gate.\n\n'
        'Validation: tuple/publication/bound-producer, collector/runtime/source controls and source-release tests passed; '
        'npm signature audit and commitlint passed. Real public release metadata, NuGet signature, package/executable identity, '
        'unchanged owner/source and scratch cleanup passed through the normal artifact-preparation command. '
        'The other 17 direct dependency pins are unchanged. Hosted tuple and six SDK gates must pass normally.\n\n'
        'Story 2.8 cancellation-order and cleanup qualification remains open; these artifact and source checks grant no native acceptance.\n')
    body_path = OUT / 'pr-body.md'
    body_path.write_text(body)
    pr = run('create-pr', ['gh', 'pr', 'create', '--repo', 'Hexalith/Hexalith.Platform', '--base', 'main', '--head', BRANCH,
        '--title', 'fix(deps): adopt the signed Builds 6.3.0 tuple', '--body-file', str(body_path)])
    RECORD.update(status='normal-pr-open', prUrl=pr.stdout.strip())
    RECORD['remoteMutations'].append({'action': 'create-normal-pr', 'url': RECORD['prUrl']})
except BaseException as error:
    RECORD.update(status='incomplete', reason=type(error).__name__ + ': ' + str(error))
    raise
finally:
    if REPO.exists():
        subprocess.run(['git', 'bundle', 'create', str(OUT / 'recoverable.bundle'), 'refs/heads/' + BRANCH, '^' + BASE], cwd=REPO, capture_output=True)
    (OUT / 'result.json').write_text(json.dumps(RECORD, indent=2) + '\n')
