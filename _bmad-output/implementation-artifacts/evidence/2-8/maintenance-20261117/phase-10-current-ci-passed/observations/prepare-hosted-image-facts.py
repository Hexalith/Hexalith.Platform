"""Prepare a separate metadata-only diagnostic ref; preserve product branches and pins."""
import hashlib
import json
import os
from pathlib import Path
import subprocess

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-current-20261117'
OUT = AREA / 'current-image-facts-source-01'
OUT.mkdir(exist_ok=False)
REPO = Path(os.environ['TMPDIR']) / 'Hexalith.Parties'
BUILDS = REPO.parent / 'Hexalith.Builds'
SOURCE = '7afc4981e21bc2fcce687199a3fd2c61fe87336e'
BASE = 'c9c21fc3e48a999db575a361562707a79f1663cf'
BRANCH = 'fix/story-2-8-current-image-facts'
NODE = Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV = dict(os.environ, PATH=str(NODE.parent) + ':' + os.environ['PATH'], PYTHONDONTWRITEBYTECODE='1')
RECORD = {'base': BASE, 'purpose': 'hosted read-only metadata only; no native or publication authority', 'remoteMutations': [], 'commands': []}


def run(label, argv, cwd=REPO, payload=None, timeout=300):
    result = subprocess.run(argv, cwd=cwd, env=ENV, input=payload, capture_output=True, text=True, timeout=timeout)
    (OUT / (label + '.log')).write_text(result.stdout + result.stderr)
    RECORD['commands'].append({'label': label, 'exitCode': result.returncode})
    print(label, result.returncode, flush=True)
    if result.returncode: raise RuntimeError(label)
    return result


def git(label, *args):
    return run(label, ['git', '-c', 'submodule.recurse=false', *args]).stdout.strip()


try:
    run('clone', ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references/Hexalith.Parties'), str(REPO)], ROOT)
    git('origin', 'remote', 'set-url', 'origin', 'https://github.com/Hexalith/Hexalith.Parties.git')
    git('fetch-current-main', 'fetch', '--no-recurse-submodules', 'origin', 'main')
    assert git('current-main', 'rev-parse', 'FETCH_HEAD') == BASE
    git('checkout', 'checkout', '-b', BRANCH, BASE)
    if (REPO / '.github/hexalith-ci.json').exists():
        raise RuntimeError('Adopted CiCd ownership/checker must be resolved before this diagnostic ref can be pushed.')
    paths = ['.github/workflows/ci-lifecycle-qualification.yml', 'scripts/collect_current_image_facts.py', 'scripts/test_collect_current_image_facts.py']
    sources = ['current-image-facts-workflow.yml', 'collect_current_image_facts.py', 'test_collect_current_image_facts.py']
    for name, source in zip(paths, sources):
        target = REPO / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes((AREA / source).read_bytes())
    run('collector-controls', ['python3', '-B', '-m', 'unittest', 'discover', '-s', 'scripts', '-p', 'test_collect_current_image_facts.py', '-v'])
    run('actionlint', ['actionlint', paths[0]])
    # Use the released6.3 shared validator, with the owning module's exact effective policy.
    # This diagnostic ref never enters product release CI; no missing module npm files are synthesized.
    run('clone-validator', ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references/Hexalith.Builds'), str(BUILDS)])
    run('fetch-validator', ['git', 'fetch', '--no-recurse-submodules', 'https://github.com/Hexalith/Hexalith.Builds.git', SOURCE], BUILDS)
    run('checkout-validator', ['git', 'checkout', '--detach', SOURCE], BUILDS)
    guard = BUILDS / 'Github/guard'
    run('validator-npm-ci', ['npm', 'ci', '--ignore-scripts', '--no-fund'], guard)
    run('validator-npm-signatures', ['npm', 'audit', 'signatures'], guard)
    message = 'ci: observe current lifecycle image prerequisites\n'
    (OUT / 'commit-message.txt').write_text(message)
    run('commitlint', [str(NODE), str(guard / 'node_modules/@commitlint/cli/cli.js'), '--cwd', str(guard),
        '--config', str(REPO / 'commitlint.config.mjs'), '--color=false'], payload=message)
    git('stage', 'add', '--', *paths)
    git('diff-check', 'diff', '--cached', '--check')
    git('commit', 'commit', '-F', str(OUT / 'commit-message.txt'))
    RECORD.update(source=git('source', 'rev-parse', 'HEAD'), branch=BRANCH)
    git('source-diff', 'diff', '--binary', BASE, 'HEAD')
    git('bundle', 'bundle', 'create', str(OUT / 'Parties.bundle'), 'refs/heads/' + BRANCH, '^' + BASE)
    old_pins = git('original-pins', 'ls-tree', '-r', BASE)
    new_pins = git('final-pins', 'ls-tree', '-r', 'HEAD')
    assert [x for x in old_pins.splitlines() if x.startswith('160000 ')] == [x for x in new_pins.splitlines() if x.startswith('160000 ')]
    assert git('remote-before-push', 'ls-remote', '--heads', 'origin', 'main', BRANCH).splitlines() == [BASE + '\trefs/heads/main']
    git('push-diagnostic-ref', 'push', '--no-recurse-submodules', 'origin', 'HEAD:refs/heads/' + BRANCH)
    RECORD['remoteMutations'].append({'action': 'push-separate-read-only-diagnostic-ref', 'sha': RECORD['source']})
    RECORD.update(status='diagnostic-ref-ready; not dispatched', imagesExpected=6, nativeExecution=False,
                  nativeQualificationCredit=False, productPrOpened=False)
except BaseException as error:
    RECORD.update(status='incomplete', reason=type(error).__name__ + ': ' + str(error))
    raise
finally:
    if REPO.exists():
        subprocess.run(['git', 'bundle', 'create', str(OUT / 'recoverable.bundle'), 'refs/heads/' + BRANCH, '^' + BASE], cwd=REPO, capture_output=True)
    (OUT / 'result.json').write_text(json.dumps(RECORD, indent=2) + '\n')
