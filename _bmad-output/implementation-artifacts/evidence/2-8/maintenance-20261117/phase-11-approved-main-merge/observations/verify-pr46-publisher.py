"""Authenticate a normal source-release check or publication receipt from GitHub."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess
import zipfile

AREA = Path(__file__).resolve().parent
MERGE = AREA / 'pr46-approved-merge-01'
REPO = 'repos/Hexalith/Hexalith.Platform/'
SOURCE = 'a18e5f0e65b9cf733795f7fa134c5bdb2fd0858b'
RUNS = {'.github/workflows/tuple-ci.yml': 44189252348,
        '.github/workflows/catalog-builder.yml': 44189252435}


def api(endpoint):
    return subprocess.check_output(['gh', 'api', REPO + endpoint], timeout=30)


def write(path, value):
    path.write_text(json.dumps(value, indent=2) + '\n')


def verify(run_id, published):
    run = json.loads(api(f'actions/runs/{run_id}'))
    assert run['head_sha'] == SOURCE and run['head_branch'] == 'main'
    assert run['event'] == 'workflow_run' and run['path'] == '.github/workflows/source-release.yml'
    assert run['status'] == 'completed' and run['conclusion'] == 'success'
    assert run['run_attempt'] == 1
    jobs = json.loads(api(f'actions/runs/{run_id}/attempts/1/jobs?per_page=20'))
    assert jobs['total_count'] == len(jobs['jobs']) == 2
    assert {j['name']: j['conclusion'] for j in jobs['jobs']} == {
        'verify': 'success', 'publish': 'success' if published else 'skipped'}
    artifact_name = f'source-release-{run_id}-1' if published else f'source-release-check-{run_id}-1'
    artifacts = json.loads(api(f'actions/runs/{run_id}/artifacts?per_page=20'))
    matching = [a for a in artifacts['artifacts'] if a['name'] == artifact_name]
    assert len(matching) == 1 and not matching[0]['expired']
    artifact = matching[0]
    if artifact.get('workflow_run') is not None:
        assert artifact['workflow_run']['id'] == run_id and artifact['workflow_run']['head_sha'] == SOURCE
    assert 0 < artifact['size_in_bytes'] < 65536
    raw = api(f'actions/artifacts/{artifact["id"]}/zip')
    digest = hashlib.sha256(raw).hexdigest()
    assert artifact['digest'] == 'sha256:' + digest and len(raw) == artifact['size_in_bytes']
    filename = 'source-release.json' if published else 'source-release-check.json'
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        assert archive.namelist() == [filename] and archive.testzip() is None
        assert 0 < archive.getinfo(filename).file_size <= 16384
        receipt_bytes = archive.read(filename)
    receipt = json.loads(receipt_bytes)
    assert receipt['schema'] == 'hexalith.platform.source-release.v1' and receipt['source'] == SOURCE
    assert receipt['executingWorkflow'] == {
        'ref': 'Hexalith/Hexalith.Platform/.github/workflows/source-release.yml@refs/heads/main',
        'sha': SOURCE, 'checkout': SOURCE, 'job': 'publish' if published else 'verify',
    }
    for flag in ('nativeAcceptance', 'candidateAcceptance', 'storyAcceptance'):
        assert receipt[flag] is False
    callback = receipt['callback']
    assert callback['attempt'] == 1 and RUNS[callback['workflow']] == callback['run']
    result = {'source': SOURCE, 'publisherRun': run_id, 'artifactId': artifact['id'],
              'archiveSha256': digest, 'receiptSha256': hashlib.sha256(receipt_bytes).hexdigest(),
              'status': receipt['status'], 'nativeAcceptance': False}
    if published:
        assert receipt['eligible'] is True and receipt['status'] == 'published'
        assert {q['workflow']: q['run'] for q in receipt['qualifyingRuns']} == RUNS
        for qualification in receipt['qualifyingRuns']:
            qualified = json.loads(api(f'actions/runs/{qualification["run"]}/attempts/1/jobs?per_page=20'))
            assert qualification['attempt'] == 1
            assert all(j['head_sha'] == SOURCE and j['conclusion'] == 'success' for j in qualified['jobs'])
            assert {j['name']: j['id'] for j in qualified['jobs']} == {j['name']: j['id'] for j in qualification['jobs']}
        tag = receipt['tag']
        assert re.fullmatch(r'v2\.[0-9]+\.[0-9]+', tag) and tag == 'v' + receipt['computedVersion']
        ref = json.loads(api('git/ref/tags/' + tag))
        release = json.loads(api('releases/tags/' + tag))
        assert ref['object']['type'] == 'commit' and ref['object']['sha'] == SOURCE
        assert release['tag_name'] == tag and not release['draft'] and not release['prerelease']
        assert release['html_url'] in receipt['releaseUrls'] and SOURCE in release['body']
        result.update(tag=tag, url=release['html_url'])
    else:
        assert receipt['eligible'] is False and receipt['status'] == 'deferred'
        assert receipt['reason'] == 'counterpart-pending'
        result['reason'] = receipt['reason']
    out = MERGE / f'publisher-{run_id}-authenticated'
    out.mkdir(exist_ok=False)
    for name, data in [('run', run), ('jobs', jobs), ('artifact', artifact), ('result', result)]:
        write(out / f'{name}.json', data)
    (out / filename).write_bytes(receipt_bytes)
    if published:
        write(out / 'release.json', release)
        write(out / 'tag.json', ref)
    print(json.dumps(result))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('run_id', type=int)
    parser.add_argument('--published', action='store_true')
    args = parser.parse_args()
    verify(args.run_id, args.published)
