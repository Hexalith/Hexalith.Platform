"""Deliver only the reviewed supported Platform lane through a normal PR."""
import hashlib
import json
import os
from pathlib import Path
import subprocess

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-maintenance-20261117'
PREPARED = json.loads((AREA / 'platform-tuple-source-01/result.json').read_text())
OBSERVED = json.loads((AREA / 'builds-published-observation.json').read_text())
assert PREPARED['status'] == 'verified-local-current-and-genuine-legacy-source'
assert OBSERVED['status'] == 'verified-publication-and-public-signed-artifact'
assert PREPARED['binding'] == OBSERVED['binding']
OUT = AREA / 'platform-maintenance-pr-01'
OUT.mkdir(exist_ok=False)
REPO = Path(os.environ['TMPDIR']) / 'Hexalith.Platform'
BASE = PREPARED['base']
CURRENT = PREPARED['currentSource']
LEGACY = PREPARED['legacySource']
BRANCH = PREPARED['currentBranch']
LEGACY_BRANCH = PREPARED['legacyBranch']
RECORD = {'source': CURRENT, 'legacySource': LEGACY, 'base': BASE,
          'remoteOperations': [], 'status': 'preflight', 'commands': []}


def run(label, args, cwd=REPO):
    result = subprocess.run(args, cwd=cwd, capture_output=True, text=True, timeout=90)
    (OUT / (label + '.log')).write_text(result.stdout + result.stderr)
    RECORD['commands'].append({'label': label, 'exitCode': result.returncode})
    print(label, result.returncode, flush=True)
    if result.returncode:
        raise RuntimeError(label + ' failed; inspect without automatically repeating mutations')
    return result.stdout.strip()


def git(label, *args):
    return run(label, ['git', '-c', 'submodule.recurse=false', *args])


def api_read(label, path):
    value = json.loads(run(label, ['gh', 'api', path]))
    (OUT / (label + '.json')).write_text(json.dumps(value, indent=2) + '\n')
    return value


def mutation(label, args):
    RECORD['remoteOperations'].append(label)
    (OUT / 'result.json').write_text(json.dumps(RECORD, indent=2) + '\n')
    return run(label, args)


try:
    run('clone', ['git', 'clone', '--shared', '--no-checkout', str(ROOT), str(REPO)], ROOT)
    git('origin', 'remote', 'set-url', 'origin', 'https://github.com/Hexalith/Hexalith.Platform.git')
    git('import', 'fetch', str(AREA / 'platform-tuple-source-01/Platform.bundle'),
        'refs/heads/' + BRANCH + ':refs/heads/' + BRANCH,
        'refs/heads/' + LEGACY_BRANCH + ':refs/heads/' + LEGACY_BRANCH)
    git('checkout', 'checkout', BRANCH)
    assert git('current-source', 'rev-parse', 'HEAD') == CURRENT
    assert git('legacy-source', 'rev-parse', LEGACY_BRANCH) == LEGACY
    assert git('base-ancestry', 'merge-base', BASE, CURRENT) == BASE
    assert git('source-cleanliness', 'status', '--porcelain', '--untracked-files=all') == ''
    assert json.loads((REPO / 'eng/builds-tuple.json').read_bytes()) == OBSERVED['binding']
    assert json.loads((REPO / 'eng/legacy-tuple-source.json').read_bytes())['sourceRevision'] == LEGACY
    assert git('recorded-producer', 'rev-parse', 'HEAD:references/Hexalith.Builds') == OBSERVED['source']
    assert hashlib.sha256((REPO / 'eng/tuple-ci-suites.json').read_bytes()).hexdigest() == PREPARED['currentProfileSha256']
    expected_paths = {'.config/dotnet-tools.json', '.github/workflows/catalog-builder.yml',
        '.github/workflows/source-release.yml', '.github/workflows/tuple-ci.yml', 'README.md',
        'docs/current-tuple-preparation.md', 'docs/domain-module-enrollment.md', 'eng/builds-tuple.json',
        'eng/legacy-tuple-source.json', 'eng/source-release/gate.mjs', 'eng/source-release/maintenance-gate.mjs',
        'eng/test_legacy_tuple_source.py', 'hexalith-platform.json', 'references/Hexalith.Builds',
        'release.config.mjs', 'tests/source-release/maintenance-source-release.test.mjs',
        'tests/source-release/source-release.test.mjs'}
    assert set(git('reviewed-paths', 'diff', '--name-only', BASE, CURRENT).splitlines()) == expected_paths
    remote = git('remote-ref-preflight', 'ls-remote', '--heads', 'origin',
        'refs/heads/1.7.x', 'refs/heads/' + BRANCH, 'refs/heads/' + LEGACY_BRANCH)
    assert remote == '', 'An existing remote branch requires reconciliation before any write'
    release = api_read('published-builds', 'repos/Hexalith/Hexalith.Builds/releases/tags/v' + OBSERVED['version'])
    assert release['id'] == json.loads((AREA / 'builds-published-observation-01/release.json').read_text())['id']
    assert not release['draft'] and not release['prerelease']
    for file, expected_id, expected_name in [('tuple-ci.yml', 401427721, 'Platform tuple CI preflight'),
                                           ('catalog-builder.yml', 403192424, 'Catalog builder consumer binding'),
                                           ('source-release.yml', 409894074, 'Qualified Platform source release')]:
        workflow = api_read('registered-' + file, 'repos/Hexalith/Hexalith.Platform/actions/workflows/' + file)
        assert workflow['id'] == expected_id and workflow['state'] == 'active'
        assert workflow['path'] == '.github/workflows/' + file and workflow['name'] == expected_name
    base_request = {'ref': 'refs/heads/1.7.x', 'sha': BASE}
    (OUT / 'base-request.json').write_text(json.dumps(base_request, indent=2) + '\n')
    mutation('create-supported-base', ['gh', 'api', '--method', 'POST', 'repos/Hexalith/Hexalith.Platform/git/refs',
        '--input', str(OUT / 'base-request.json')])
    mutation('push-genuine-legacy', ['git', 'push', 'origin', 'refs/heads/' + LEGACY_BRANCH + ':refs/heads/' + LEGACY_BRANCH])
    mutation('push-reviewed-current', ['git', 'push', 'origin', 'refs/heads/' + BRANCH + ':refs/heads/' + BRANCH])
    body = f'''Supported Platform 1.7 consumer builds are blocked by the old .NET 10 catalog pins. Bind the normally published and repository-signed Builds {OBSERVED['version']} at `{OBSERVED['source']}` and enable the named 1.7.x qualification/publication lane.

The exact tool pin, supported range floor, catalog gitlink/blob/hash and public archive/inventory identities now agree. Source publication requires both latest exact-source current/legacy qualification jobs and all five SDK jobs, with the actual executing workflow/source/run bound and rechecked before publication.

The genuine legacy source `{LEGACY}` carries identical code, tests, helpers and documentation; only the original eight metadata paths differ. Its original 349 controls, tool/catalog/provenance and no-acceptance classification remain intact. The current 412-case profile is byte-identical to the supported base.

Validation: all 19 tuple controls, 10 legacy controls and 42 source-release controls passed. The actual committed same-code comparison and normal published-artifact verifier (`--require-recorded`) passed, including the NuGet repository signature and exact executable version/source. Locked dependency signatures, complete commit messages and actual maintenance version analysis passed.

Hosted PR and merged-source current/legacy qualification and all five SDK gates are still required. This maintenance change grants no caller/native acceptance, current-cohort registration or Story 2.10 qualification.
'''
    request = {'base': '1.7.x', 'head': BRANCH, 'title': 'fix(ci): qualify the supported 1.7 maintenance tuple', 'body': body, 'draft': False}
    (OUT / 'pr-request.json').write_text(json.dumps(request, indent=2) + '\n')
    response = json.loads(mutation('create-normal-pr', ['gh', 'api', '--method', 'POST',
        'repos/Hexalith/Hexalith.Platform/pulls', '--input', str(OUT / 'pr-request.json')]))
    assert response['head']['sha'] == CURRENT and response['base']['ref'] == '1.7.x'
    (OUT / 'pr.json').write_text(json.dumps(response, indent=2) + '\n')
    RECORD.update(status='normal-maintenance-pr-opened', pr=response['number'], url=response['html_url'])
    print(RECORD['url'], flush=True)
except BaseException as error:
    RECORD.update(status='incomplete', reason=type(error).__name__ + ': ' + str(error))
    raise
finally:
    (OUT / 'result.json').write_text(json.dumps(RECORD, indent=2) + '\n')
