"""Observe normal Platform main checks and source publication for PR 46."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import json
from pathlib import Path
import re
import subprocess
import time

AREA = Path(__file__).resolve().parent
OUT = AREA / 'pr46-approved-merge-01' / 'main-observations'
REPO = 'repos/Hexalith/Hexalith.Platform/'
SOURCE = 'a18e5f0e65b9cf733795f7fa134c5bdb2fd0858b'
WORKFLOWS = {
    'tuple': (44189252348, 'tuple-ci.yml', {'resolve-sources', 'tuple', 'tuple-verdict'}),
    'sdk': (44189252435, 'catalog-builder.yml', {
        f'consumer-binding ({version})'
        for version in ('3.130.0', '3.130.1', '3.130.2', '3.132.1', '4.1.0', '4.2.0')
    }),
}


def api(endpoint):
    return json.loads(subprocess.check_output(['gh', 'api', REPO + endpoint], timeout=25))


def save(name, record):
    (OUT / f'{name}.json').write_text(json.dumps(record, indent=2) + '\n')


def main_run(kind):
    run_id, file, required = WORKFLOWS[kind]
    run = api(f'actions/runs/{run_id}')
    assert run['id'] == run_id and run['path'] == f'.github/workflows/{file}'
    assert run['head_sha'] == SOURCE and run['head_branch'] == 'main' and run['event'] == 'push'
    assert run['head_repository']['full_name'] == 'Hexalith/Hexalith.Platform'
    assert run['run_attempt'] == 1 and not run['pull_requests']
    jobs = api(f'actions/runs/{run_id}/attempts/1/jobs?per_page=100')
    assert jobs['total_count'] == len(jobs['jobs'])
    if run['status'] == 'completed':
        save(f'{kind}-run-final', run)
        save(f'{kind}-jobs-final', jobs)
        if run['conclusion'] != 'success':
            raise RuntimeError(f'Main {kind} run {run_id} completed with {run["conclusion"]}')
        assert {j['name'] for j in jobs['jobs']} == required and len(jobs['jobs']) == len(required)
        assert all(j['conclusion'] == 'success' and j['head_sha'] == SOURCE for j in jobs['jobs'])
    return {
        'id': run_id, 'status': run['status'], 'conclusion': run['conclusion'],
        'jobs': [
            {'name': j['name'], 'status': j['status'], 'conclusion': j['conclusion'],
             'step': next((s['name'] for s in j['steps'] if s['status'] == 'in_progress'), None)}
            for j in jobs['jobs']
        ],
    }


def publication():
    runs = api(f'actions/workflows/source-release.yml/runs?head_sha={SOURCE}&event=workflow_run&per_page=30')
    assert runs['total_count'] == len(runs['workflow_runs'])
    status = []
    for run in runs['workflow_runs']:
        assert run['head_sha'] == SOURCE and run['head_branch'] == 'main' and run['event'] == 'workflow_run'
        assert run['path'] == '.github/workflows/source-release.yml' and run['run_attempt'] == 1
        jobs = api(f'actions/runs/{run["id"]}/attempts/1/jobs?per_page=30')
        detail = {'id': run['id'], 'status': run['status'], 'conclusion': run['conclusion'],
                  'jobs': {j['name']: j['conclusion'] or j['status'] for j in jobs['jobs']}}
        status.append(detail)
        if run['status'] != 'completed':
            continue
        save(f'publisher-{run["id"]}-run', run)
        save(f'publisher-{run["id"]}-jobs', jobs)
        if run['conclusion'] == 'success' and detail['jobs'].get('publish') == 'success':
            return status, run['id']
    return status, None


def observe():
    OUT.mkdir(exist_ok=False)
    latest = None
    deadline = time.monotonic() + 130 * 60
    with ThreadPoolExecutor(max_workers=3) as pool:
        while time.monotonic() < deadline:
            try:
                checks = {key: pool.submit(main_run, key) for key in WORKFLOWS}
                publisher = pool.submit(publication)
                state = {key: result.result() for key, result in checks.items()}
                state['publishers'], publishing_run = publisher.result()
            except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
                print(f'GitHub observation unavailable: {error}', flush=True)
                time.sleep(20)
                continue
            if state != latest:
                with (OUT / 'timeline.jsonl').open('a') as stream:
                    stream.write(json.dumps({'at': datetime.now(timezone.utc).isoformat(), **state}) + '\n')
                save('latest', state)
                print(json.dumps(state), flush=True)
                latest = state
            if publishing_run is not None and all(state[key]['conclusion'] == 'success' for key in WORKFLOWS):
                release = api('releases/latest')
                tag = release['tag_name']
                assert re.fullmatch(r'v2\.[0-9]+\.[0-9]+', tag)
                assert not release['draft'] and not release['prerelease']
                ref = api('git/ref/tags/' + tag)
                assert ref['object']['type'] == 'commit' and ref['object']['sha'] == SOURCE
                save('published-release', release)
                save('published-tag', ref)
                save('result', {'source': SOURCE, 'tag': tag, 'releaseUrl': release['html_url'],
                                'publisherRun': publishing_run, 'mainChecks': state})
                return
            time.sleep(35)
    raise TimeoutError('Normal GitHub publication was not observed within the bounded monitor')


if __name__ == '__main__':
    observe()
