"""Read-only monitor of the two distinct exact-source feature dispatches."""
import datetime
import json
import subprocess
import time
from pathlib import Path

CACHE = Path('/home/administrator/.cache/hexalith/story-2-8/parent-platform-e810-hosted-20261106')
SOURCE = 'e810be0356da55b6db1794262c722e358470a093'
BRANCH = 'story-2-8/scoped-gateway-4448-union-e810be03'
RUNS = {'tuple': (42178949195, 'tuple-ci.yml'), 'catalog': (42178951812, 'catalog-builder.yml')}
previous = {}
completed = {}


def get(endpoint):
    result = subprocess.run(['gh', 'api', endpoint], capture_output=True)
    assert result.returncode == 0, result.stderr.decode()
    return json.loads(result.stdout)


while len(completed) != len(RUNS):
    for label, (run_id, workflow) in RUNS.items():
        if label in completed:
            continue
        run = get(f'repos/Hexalith/Hexalith.Platform/actions/runs/{run_id}')
        jobs = get(f'repos/Hexalith/Hexalith.Platform/actions/runs/{run_id}/jobs?per_page=100')
        assert run['head_sha'] == SOURCE and run['head_branch'] == BRANCH
        assert run['event'] == 'workflow_dispatch' and run['run_attempt'] == 1
        assert run['path'] == '.github/workflows/' + workflow
        assert run['repository']['full_name'] == 'Hexalith/Hexalith.Platform'
        assert jobs['total_count'] == len(jobs['jobs'])
        summary = [(job['id'], job['name'], job['status'], job['conclusion']) for job in jobs['jobs']]
        state = (run['status'], run['conclusion'], summary)
        if state != previous.get(label):
            at = datetime.datetime.now(datetime.timezone.utc)
            path = CACHE / (label + '-phase-' + at.strftime('%Y%m%dT%H%M%S%fZ') + '.json')
            assert not path.exists()
            path.write_text(json.dumps({'utc': at.isoformat(), 'run': run, 'jobs': jobs}, indent=2) + '\n')
            print(json.dumps({'utc': at.isoformat(), 'label': label, 'run': run_id, 'status': run['status'], 'conclusion': run['conclusion'], 'jobs': summary}), flush=True)
            previous[label] = state
        if run['status'] == 'completed':
            artifacts = get(f'repos/Hexalith/Hexalith.Platform/actions/runs/{run_id}/artifacts?per_page=100')
            assert artifacts['total_count'] == len(artifacts['artifacts'])
            for suffix, data in [('run', run), ('jobs', jobs), ('artifacts', artifacts)]:
                (CACHE / (label + '-final-' + suffix + '.json')).write_text(json.dumps(data, indent=2) + '\n')
            completed[label] = run['conclusion']
    if len(completed) != len(RUNS):
        time.sleep(30)
raise SystemExit(0 if all(value == 'success' for value in completed.values()) else 1)
