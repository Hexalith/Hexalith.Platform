"""Observe the existing normal publisher; never dispatch, approve or mutate it."""
import datetime
import json
import subprocess
import time
from pathlib import Path

EV = Path(__file__).resolve().parent
RUN = 42144370420
SOURCE = 'ad50acf920d78ee16033bdc1cf26c34e8275cdcb'
previous = None
while True:
    def get(endpoint):
        result = subprocess.run(['gh', 'api', endpoint], capture_output=True)
        assert result.returncode == 0, result.stderr.decode()
        return json.loads(result.stdout)
    run = get(f'repos/Hexalith/Hexalith.Builds/actions/runs/{RUN}')
    jobs = get(f'repos/Hexalith/Hexalith.Builds/actions/runs/{RUN}/jobs?per_page=100')
    assert run['head_sha'] == SOURCE and run['event'] == 'workflow_dispatch'
    assert run['path'] == '.github/workflows/build-release.yml' and run['run_attempt'] == 1
    summary = [{'id': job['id'], 'name': job['name'], 'status': job['status'],
                'conclusion': job['conclusion'],
                'active': [step['name'] for step in job.get('steps', []) if step['status'] == 'in_progress']}
               for job in jobs['jobs']]
    state = (run['status'], run['conclusion'], summary)
    if state != previous:
        now = datetime.datetime.now(datetime.timezone.utc)
        out = EV / ('parent-builds-ad50-normal-release-phase-' + now.strftime('%Y%m%dT%H%M%SZ') + '.json')
        out.write_text(json.dumps({'utc': now.isoformat(), 'run': run, 'jobs': jobs}, indent=2) + '\n')
        print(json.dumps({'utc': now.isoformat(), 'run': RUN, 'status': run['status'],
                          'conclusion': run['conclusion'], 'jobs': summary}), flush=True)
        previous = state
    if run['status'] == 'completed':
        raise SystemExit(0 if run['conclusion'] == 'success' else 1)
    time.sleep(30)
