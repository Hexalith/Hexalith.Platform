"""Extend the measured source-SDK metadata controls to the exact new catalog's SDK5.2."""
import json
import os
from pathlib import Path
import subprocess

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-current-20261117'
OUT = AREA / 'platform630-source-07'
OUT.mkdir(exist_ok=False)
REPO = Path(os.environ['TMPDIR']) / 'Hexalith.Platform'
BASE = '11719c5cf51c2b2b080b89e0e301c2ef8dbacc46'
BRANCH = 'fix/story-2-8-builds-6-3'
PREVIOUS = json.loads((AREA / 'platform630-source-06/result-reconciled.json').read_text())
assert PREVIOUS['status'] == 'fully-prepared-pr-awaiting-hosted-ci'
NODE = Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV = dict(os.environ, PATH=str(NODE.parent) + ':' + os.environ['PATH'], PYTHONDONTWRITEBYTECODE='1')
RECORD = {'base': BASE, 'buildsVersion': '6.3.0', 'remoteMutations': [], 'commands': []}


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
    run('clone', ['git', 'clone', '--shared', '--no-checkout', str(ROOT), str(REPO)], ROOT)
    git('origin', 'remote', 'set-url', 'origin', 'https://github.com/Hexalith/Hexalith.Platform.git')
    git('recover', 'fetch', str(AREA / 'platform630-source-06/Platform.bundle'), 'refs/heads/' + BRANCH)
    git('checkout', 'checkout', '-b', BRANCH, PREVIOUS['source'])
    path = REPO / 'eng/test_source_sdk_propagation.py'
    text = path.read_text()
    old = 'cls.default_sdk not in ("4.1.0", "4.2.0", "5.1.0")'
    assert text.count(old) == 1
    path.write_text(text.replace(old, 'cls.default_sdk not in ("4.1.0", "4.2.0", "5.1.0", "5.2.0")'))
    # These metadata controls inspect SDK-promoted source edges, so prepare every declared direct dependency.
    pins = {}
    for row in git('owner-pins', 'ls-tree', '-r', 'HEAD').splitlines():
        if row.startswith('160000 '):
            name = row.split('\t', 1)[1]
            pins[name] = row.split()[2]
    assert len(pins) == 18
    for name, revision in pins.items():
        target = REPO / name
        label = Path(name).name
        run('clone-' + label, ['git', 'clone', '--shared', '--no-checkout', str(ROOT / name), str(target)])
        present = subprocess.run(['git', 'cat-file', '-e', revision + '^{commit}'], cwd=target, env=ENV, capture_output=True)
        if present.returncode:
            run('fetch-' + label, ['git', 'fetch', '--no-recurse-submodules', 'https://github.com/Hexalith/' + label + '.git', revision], target)
        run('checkout-' + label, ['git', 'checkout', '--detach', revision], target)
    run('npm-ci', ['npm', 'ci', '--ignore-scripts', '--no-fund'])
    run('signatures', ['npm', 'audit', 'signatures'])
    message = 'fix(ci): retain source SDK propagation controls for SDK 5.2\n'
    (OUT / 'commit-message.txt').write_text(message)
    run('commitlint', [str(NODE), str(REPO / 'node_modules/@commitlint/cli/cli.js'), '--config', str(REPO / 'commitlint.config.mjs'), '--color=false'], payload=message)
    git('stage', 'add', '--', 'eng/test_source_sdk_propagation.py')
    git('diff-check', 'diff', '--cached', '--check')
    git('commit', 'commit', '-F', str(OUT / 'commit-message.txt'))
    RECORD.update(source=git('source', 'rev-parse', 'HEAD'), tree=git('tree', 'rev-parse', 'HEAD^{tree}'), branch=BRANCH)
    git('bundle', 'bundle', 'create', str(OUT / 'Platform.bundle'), 'refs/heads/' + BRANCH, '^' + BASE)
    git('source-diff', 'diff', '--binary', BASE, 'HEAD')
    run('normal-published-artifact-preparation', ['python3', '-B', 'eng/prepare_builds_artifacts.py', '--output', str(REPO.parent / 'verified-publication'), '--require-recorded'])
    identity = json.loads((REPO.parent / 'verified-publication/artifact-identity.json').read_bytes())
    (OUT / 'artifact-identity.json').write_text(json.dumps(identity, indent=2) + '\n')
    tool = Path(identity['toolRoot'])
    properties = ['-p:Configuration=Release', '-p:UseHexalithProjectReferences=false', '-p:HexalithEventStoreVersion=4.2.0',
        '-p:HexalithTenantsVersion=7.0.7', '-p:HexalithMemoriesVersion=3.0.27', '-p:HexalithPartiesVersion=3.1.1',
        '-p:HexalithMcpCliVersion=2.0.1', '-p:HexalithBuildsRoot=' + str(REPO / 'references/Hexalith.Builds'),
        '-p:HexalithBuildsPublishedToolRoot=' + str(tool)]
    project = 'tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj'
    run('restore-metadata-assets', ['dotnet', 'restore', project, *properties], timeout=600)
    run('source-sdk-propagation-controls', ['python3', '-B', 'eng/test_source_sdk_propagation.py', '--evidence',
        str(OUT / 'source-sdk-metadata'), '--published-tool-root', str(tool)], timeout=900)
    result = json.loads((OUT / 'source-sdk-metadata/result.json').read_bytes())
    assert result['expected'] == result['executed'] == 29 and result['passed'] and not result['skipped']
    current = git('remote-before-push', 'ls-remote', '--heads', 'origin', BRANCH).split()[0]
    assert current == PREVIOUS['source']
    git('push', 'push', '--no-recurse-submodules', 'origin', 'HEAD:refs/heads/' + BRANCH)
    RECORD['remoteMutations'].append({'action': 'normal-fast-forward-update-pr46', 'sha': RECORD['source']})
    RECORD.update(status='final-source-awaiting-hosted-ci', prUrl=PREVIOUS['prUrl'])
except BaseException as error:
    RECORD.update(status='incomplete', reason=type(error).__name__ + ': ' + str(error))
    raise
finally:
    if REPO.exists():
        subprocess.run(['git', 'bundle', 'create', str(OUT / 'recoverable.bundle'), 'refs/heads/' + BRANCH, '^' + BASE], cwd=REPO, capture_output=True)
    (OUT / 'result.json').write_text(json.dumps(RECORD, indent=2) + '\n')
