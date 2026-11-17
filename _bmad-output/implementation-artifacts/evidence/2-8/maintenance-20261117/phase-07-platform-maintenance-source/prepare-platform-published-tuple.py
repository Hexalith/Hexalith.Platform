"""Prepare genuine current/legacy source commits from an observed public release."""
import copy
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-maintenance-20261117'
OBSERVATION = json.loads((AREA / 'builds-published-observation.json').read_text())
assert OBSERVATION['status'] == 'verified-publication-and-public-signed-artifact'
assert OBSERVATION['nugetSignatureVerified'] and OBSERVATION['officialArtifactDigestAndCrcVerified']
BINDING = OBSERVATION['binding']
SOURCE = '979cfd3e2c4145be516fbd833dacfc6b8e8fdce5'
assert BINDING['producer']['sourceRevision'] == SOURCE
BASE = '0f4e27f1f78310bd731c10c19dc51bb1e0345d44'
OLD_LEGACY = '3cd036f78ccdfeb72521122a65f1a3aef0bff9da'
SUPPORT = '33d28ad2f3cac02de45652743707f73a8fd3d9f7'
CURRENT_BRANCH = 'fix/story-2-8-1.7-maintenance'
LEGACY_BRANCH = 'fix/story-2-8-1.7-legacy-source'
OUT = AREA / 'platform-tuple-source-01'
OUT.mkdir(exist_ok=False)
SCRATCH = Path(os.environ['TMPDIR'])
REPO = SCRATCH / 'Hexalith.Platform'
BUILDS = SCRATCH / 'Hexalith.Builds'
NODE = Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV = dict(os.environ, PATH=str(NODE.parent) + ':' + os.environ['PATH'], PYTHONDONTWRITEBYTECODE='1')
RECORD = {'base': BASE, 'supportSource': SUPPORT, 'originalLegacy': OLD_LEGACY,
          'remoteMutations': False, 'candidateAcceptance': False, 'commands': []}


def run(label, args, cwd=REPO, payload=None, timeout=300):
    result = subprocess.run(args, cwd=cwd, env=ENV, input=payload, capture_output=True, text=True, timeout=timeout)
    (OUT / (label + '.log')).write_text(result.stdout + result.stderr)
    RECORD['commands'].append({'label': label, 'exitCode': result.returncode})
    print(label, result.returncode, flush=True)
    if result.returncode:
        raise RuntimeError(label + ' failed; inspect retained output')
    return result.stdout.strip()


def git(label, *args):
    return run(label, ['git', '-c', 'submodule.recurse=false', *args])


def commit(label, message):
    (OUT / (label + '-message.txt')).write_text(message)
    run(label + '-commitlint', [str(NODE), str(REPO / 'node_modules/@commitlint/cli/cli.js'),
                               '--config', str(REPO / 'commitlint.config.mjs'), '--color=false'], payload=message)
    git(label + '-diff-check', 'diff', '--cached', '--check')
    git(label + '-commit', 'commit', '-F', str(OUT / (label + '-message.txt')))
    return git(label + '-source', 'rev-parse', 'HEAD')


try:
    run('clone', ['git', 'clone', '--shared', '--no-checkout', str(ROOT), str(REPO)], ROOT)
    git('origin', 'remote', 'set-url', 'origin', 'https://github.com/Hexalith/Hexalith.Platform.git')
    git('import-support', 'fetch', str(AREA / 'platform-support-03/Platform.bundle'), 'refs/heads/1.7.x:refs/heads/1.7.x')
    git('checkout', 'checkout', '-b', CURRENT_BRANCH, SUPPORT)
    assert git('support-source', 'rev-parse', 'HEAD') == SUPPORT
    original_profile = (REPO / 'eng/tuple-ci-suites.json').read_bytes()
    assert sum(g['requiredCount'] for g in json.loads(original_profile)['groups']) == 412
    original_descriptor = json.loads((REPO / 'eng/legacy-tuple-source.json').read_bytes())
    assert original_descriptor['sourceRevision'] == OLD_LEGACY and original_descriptor['requiredCount'] == 349
    version = BINDING['publication']['version']
    (REPO / 'eng/builds-tuple.json').write_text(json.dumps(BINDING, indent=2) + '\n')
    for name, old, new in [('.config/dotnet-tools.json', b'"version": "4.44.13"', ('"version": "' + version + '"').encode()),
                           ('hexalith-platform.json', b'[4.44.13, 5.0.0-0)', ('[' + version + ', 5.0.0-0)').encode())]:
        path = REPO / name
        raw = path.read_bytes()
        assert raw.count(old) == 1
        path.write_bytes(raw.replace(old, new, 1))
    git('record-builds-gitlink', 'update-index', '--cacheinfo', '160000,' + SOURCE + ',references/Hexalith.Builds')
    publication = BINDING['publication']
    catalog = BINDING['producer']['catalog']
    section = f'''## Current signed Builds {version} maintenance tuple

This source prepares [Builds v{version}](https://github.com/Hexalith/Hexalith.Builds/releases/tag/v{version})
at `{SOURCE}`, published by normal
[release 44133307523, attempt 2](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44133307523)
after exact-source [push CI 44129245136](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44129245136).
The exact module-tool pin and supported range floor are `{version}`;
the upper bound remains `5.0.0-0` and roll-forward is disabled.
The verified public NuGet repository-signed archive SHA-256 is
`{publication['archiveSha256']}`. Its observed AppHost assembly is
`{OBSERVATION['apphostSha256']}` and Tooling assembly is
`{publication['toolingSha256']}`.
Catalog blob `{catalog['blob']}` has raw SHA-256
`{catalog['sha256']}`. The [official release inventory]({publication['inventoryUrl']})
has SHA-256 `{publication['inventorySha256']}`;
catalog-origin revision is `{OBSERVATION['catalogOriginRevision']}`.

The bounded catalog patch updates three .NET 10 package pins to 10.0.15.
SDK 4.1.0, public Parties 3.1.0 and the other public package, image and
declaration identities retain their recorded roles. The current profile still
requires 412 cases and the genuine same-code legacy profile all 349 original
controls. Both source qualifications and all five SDK gates must pass before
normal Platform source publication. Published artifact identity grants no
candidate acceptance, caller/native qualification, current-cohort registration
or Story 2.10 qualification. The previous producer facts below remain historical.

'''
    for name in ['README.md', 'docs/current-tuple-preparation.md', 'docs/domain-module-enrollment.md']:
        path = REPO / name
        raw = path.read_bytes()
        marker = b'## Current signed Builds4.44.13 and public Parties3.1.0 tuple'
        assert raw.count(marker) == 1
        newline = '\r\n' if b'\r\n' in raw else '\n'
        replacement = section.replace('\n', newline).encode() + marker.replace(b'## Current', b'## Previous')
        path.write_bytes(raw.replace(marker, replacement, 1))
    run('npm-ci', ['npm', 'ci', '--ignore-scripts', '--no-fund'])
    run('npm-signatures', ['npm', 'audit', 'signatures'])
    changed = ['eng/builds-tuple.json', '.config/dotnet-tools.json', 'hexalith-platform.json',
               'README.md', 'docs/current-tuple-preparation.md', 'docs/domain-module-enrollment.md']
    git('stage-published-tuple', 'add', '--', *changed)
    c1 = commit('published-tuple', 'fix(deps): bind the published 4.44 maintenance tuple\n')
    git('legacy-branch', 'checkout', '-b', LEGACY_BRANCH)
    allowed = ['src/Hexalith.Platform.Composition/Declarations/provenance.json', 'eng/legacy-tuple-source.json',
               'eng/builds-tuple.json', 'eng/tuple-ci-suites.json', '.config/dotnet-tools.json',
               'hexalith-platform.json', 'references/Hexalith.Builds', 'references/Hexalith.Parties']
    git('restore-authentic-legacy-metadata', 'restore', '--source=' + OLD_LEGACY, '--staged', '--worktree', '--', *allowed)
    legacy = commit('legacy-projection', 'test(ci): record the same-code legacy maintenance source\n')
    git('return-current', 'checkout', CURRENT_BRANCH)
    descriptor = copy.deepcopy(original_descriptor)
    descriptor['sourceRevision'] = legacy
    (REPO / 'eng/legacy-tuple-source.json').write_text(json.dumps(descriptor, indent=2) + '\n')
    git('stage-legacy-descriptor', 'add', '--', 'eng/legacy-tuple-source.json')
    current = commit('current-source', 'fix(ci): bind the committed legacy regression source\n')
    assert (REPO / 'eng/tuple-ci-suites.json').read_bytes() == original_profile
    git('execute-legacy-head', 'checkout', LEGACY_BRANCH)
    comparison = json.loads(run('genuine-same-code-legacy-proof', ['python3', '-B', 'eng/legacy_tuple_source.py',
                                  '--current', current, '--requested', legacy, '--verify']))
    assert comparison['sourceEquivalence'] == 'verified' and comparison['requiredCount'] == 349
    assert comparison['qualificationCredit'] is False
    git('execute-current-head', 'checkout', CURRENT_BRANCH)
    run('clone-published-producer', ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references/Hexalith.Builds'), str(BUILDS)])
    run('fetch-published-producer', ['git', 'fetch', '--no-recurse-submodules', 'https://github.com/Hexalith/Hexalith.Builds.git', SOURCE], BUILDS)
    run('checkout-published-producer', ['git', 'checkout', '--detach', SOURCE], BUILDS)
    run('retained-tuple-controls', ['python3', '-B', 'eng/test_builds_tuple.py'])
    run('retained-legacy-controls', ['python3', '-B', 'eng/test_legacy_tuple_source.py'])
    run('release-controls', ['npm', 'run', 'test:source-release'])
    artifacts = SCRATCH / 'verified-platform-published-artifacts'
    run('actual-published-artifact-verifier', ['python3', '-B', 'eng/prepare_builds_artifacts.py',
        '--output', str(artifacts), '--require-recorded'], timeout=300)
    for name in ['artifact-identity.json', 'signature.stdout', 'signature.stderr']:
        shutil.copyfile(artifacts / name, OUT / name)
    assert git('final-source-cleanliness', 'status', '--porcelain', '--untracked-files=normal') == ''
    run('version-analysis', [str(NODE), str(AREA / 'analyze-maintenance-version.mjs'), str(REPO), BASE,
        '1.7.x', str(OUT / 'version.json'), 'conventionalcommits'])
    git('source-diff', 'diff', '--binary', BASE, current)
    git('bundle', 'bundle', 'create', str(OUT / 'Platform.bundle'),
        'refs/heads/' + CURRENT_BRANCH, 'refs/heads/' + LEGACY_BRANCH, '^' + BASE)
    RECORD.update(status='verified-local-current-and-genuine-legacy-source', currentSource=current,
                  legacySource=legacy, tupleCommit=c1, currentBranch=CURRENT_BRANCH, legacyBranch=LEGACY_BRANCH,
                  currentProfileSha256=hashlib.sha256(original_profile).hexdigest(), currentCount=412,
                  legacyCount=349, sameCodeProof=comparison, binding=BINDING)
except BaseException as error:
    RECORD.update(status='incomplete', reason=type(error).__name__ + ': ' + str(error))
    raise
finally:
    (OUT / 'result.json').write_text(json.dumps(RECORD, indent=2) + '\n')
