"""Prepare the supported current Platform source against the verified Builds6 release."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-current-20261117'
OUT = AREA / 'platform630-source-01'
OUT.mkdir(exist_ok=False)
REPO = Path(os.environ['TMPDIR']) / 'Hexalith.Platform'
BUILDS = REPO.parent / 'Hexalith.Builds'
BASE = '11719c5cf51c2b2b080b89e0e301c2ef8dbacc46'
SOURCE = '7afc4981e21bc2fcce687199a3fd2c61fe87336e'
BRANCH = 'fix/story-2-8-builds-6-3'
OBSERVED = json.loads((AREA / 'builds630-observation.json').read_text())
assert OBSERVED['status'] == 'verified-official-shared-release-and-signed-package'
BINDING = OBSERVED['binding']
NODE = Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV = dict(os.environ, PATH=str(NODE.parent) + ':' + os.environ['PATH'], PYTHONDONTWRITEBYTECODE='1')
RECORD = {'base': BASE, 'buildsSource': SOURCE, 'buildsVersion': '6.3.0', 'remoteMutations': False, 'commands': []}


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
    git('checkout', 'checkout', '-b', BRANCH, BASE)
    assert git('base', 'rev-parse', 'HEAD') == BASE
    original_binding = json.loads((REPO / 'eng/builds-tuple.json').read_text())
    assert original_binding['publication']['version'] == '6.0.5'
    (REPO / 'eng/builds-tuple.json').write_text(json.dumps(BINDING, indent=2) + '\n')
    for name, old, new in [('.config/dotnet-tools.json', b'"version": "6.0.5"', b'"version": "6.3.0"'),
                           ('hexalith-platform.json', b'[6.0.5, 7.0.0-0)', b'[6.3.0, 7.0.0-0)')]:
        path = REPO / name
        raw = path.read_bytes()
        assert raw.count(old) == 1
        path.write_bytes(raw.replace(old, new))
    git('record-builds-source', 'update-index', '--cacheinfo', '160000,' + SOURCE + ',references/Hexalith.Builds')
    old_publication = original_binding['publication']
    replacements = {'6.0.5': '6.3.0', original_binding['producer']['sourceRevision']: SOURCE,
        original_binding['producer']['catalog']['blob']: BINDING['producer']['catalog']['blob'],
        original_binding['producer']['catalog']['sha256']: BINDING['producer']['catalog']['sha256'],
        old_publication['toolingSha256']: BINDING['publication']['toolingSha256'],
        'b9c80faba94356860ac73b9e91f1c3721ccc90371726c8b8e33e8774ddbd61b1': OBSERVED['apphostSha256']}
    for name in ['README.md', 'docs/current-tuple-preparation.md', 'docs/domain-module-enrollment.md']:
        path = REPO / name
        text = path.read_text()
        start = text.index('## Candidate-only tuple preparation')
        end = text.index('\n## ', start + 1)
        section = text[start:end]
        for old, new in replacements.items():
            assert old in section, (name, old)
            section = section.replace(old, new)
        paragraph = ('The signed Module archive SHA-256 is `' + BINDING['publication']['archiveSha256'] + '`.\n'
            'Its [normal release](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v6.3.0) and\n'
            '[publisher run](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44147684659)\n'
            'bind the original artifact and the [public package inventory](https://github.com/Hexalith/Hexalith.Builds/releases/download/v6.3.0/hexalith-package-publication.v1.json),\n'
            'SHA-256 `25078b6710fe5d86298d7d1574a69a102c0bdd450ff82b5ba9482a536f64b459`.\n'
            'Every public package payload matches the authenticated release artifact; the NuGet\n'
            'repository signature is verified separately. These are artifact facts only.\n\n')
        anchor = 'This signed-host pairing grants no ordinary image or runtime admission.\n\n'
        assert section.count(anchor) == 1
        section = section.replace(anchor, anchor + paragraph)
        text = text[:start] + section + text[end:]
        if name == 'README.md':
            text = text.replace('range permits later 6.x versions from `6.0.5`', 'range permits later 6.x versions from `6.3.0`')
        path.write_text(text)
    run('npm-ci', ['npm', 'ci', '--ignore-scripts', '--no-fund'])
    run('signatures', ['npm', 'audit', 'signatures'])
    message = 'fix(deps): bind the released Builds 6.3.0 tuple\n'
    (OUT / 'commit-message.txt').write_text(message)
    run('commitlint', [str(NODE), str(REPO / 'node_modules/@commitlint/cli/cli.js'),
        '--config', str(REPO / 'commitlint.config.mjs'), '--color=false'], payload=message)
    paths = ['eng/builds-tuple.json', '.config/dotnet-tools.json', 'hexalith-platform.json',
             'README.md', 'docs/current-tuple-preparation.md', 'docs/domain-module-enrollment.md']
    git('stage', 'add', '--', *paths)
    git('diff-check', 'diff', '--cached', '--check')
    git('source-diff', 'diff', '--cached', '--binary')
    git('commit', 'commit', '-F', str(OUT / 'commit-message.txt'))
    current = git('source', 'rev-parse', 'HEAD')
    RECORD.update(source=current, tree=git('tree', 'rev-parse', 'HEAD^{tree}'), branch=BRANCH)
    git('bundle', 'bundle', 'create', str(OUT / 'Platform.bundle'), 'refs/heads/' + BRANCH, '^' + BASE)
    run('clone-builds', ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references/Hexalith.Builds'), str(BUILDS)])
    run('fetch-builds', ['git', 'fetch', '--no-recurse-submodules', 'https://github.com/Hexalith/Hexalith.Builds.git', SOURCE], BUILDS)
    run('checkout-builds', ['git', 'checkout', '--detach', SOURCE], BUILDS)
    run('tuple-controls', ['python3', '-B', 'eng/test_builds_tuple.py'])
    run('publication-controls', ['python3', '-B', 'eng/test_builds_publication.py'])
    run('bound-producer-controls', ['python3', '-B', 'eng/test_bound_builds.py'])
    # Exercise the existing normal path before adapting any implementation.
    actual = run('normal-published-artifact-preparation', ['python3', '-B', 'eng/prepare_builds_artifacts.py',
        '--output', str(REPO.parent / 'verified-publication'), '--require-recorded'], timeout=300, required=False)
    RECORD['normalArtifactPreparationExitCode'] = actual.returncode
    RECORD['status'] = 'prepared-and-local-controls-passed' if actual.returncode == 0 else 'prepared; normal-artifact-path-needs-correction'
except BaseException as error:
    RECORD.update(status='incomplete', reason=type(error).__name__ + ': ' + str(error))
    raise
finally:
    if REPO.exists():
        subprocess.run(['git', 'bundle', 'create', str(OUT / 'recoverable.bundle'), 'refs/heads/' + BRANCH, '^' + BASE],
                       cwd=REPO, capture_output=True)
    (OUT / 'result.json').write_text(json.dumps(RECORD, indent=2) + '\n')
