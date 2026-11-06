"""Read-only monitor of the actual incoming main CI; no dispatch or cancellation."""
import datetime
import json
import subprocess
import time
from pathlib import Path

EV = Path(__file__).resolve().parent
SOURCE = 'ad50acf920d78ee16033bdc1cf26c34e8275cdcb'
RUN = 42142102328
last = None
while True:
    def get(path):
        result = subprocess.run(['gh', 'api', path], capture_output=True)
        assert result.returncode == 0, result.stderr.decode()
        return json.loads(result.stdout)
    run = get(f'repos/Hexalith/Hexalith.Builds/actions/runs/{RUN}')
    jobs = get(f'repos/Hexalith/Hexalith.Builds/actions/runs/{RUN}/jobs?per_page=100')
    assert run['head_sha'] == SOURCE and run['path'] == '.github/workflows/ci.yml'
    assert run['event'] == 'push' and run['run_attempt'] == 1
    summary = [(job['id'], job['name'], job['status'], job['conclusion']) for job in jobs['jobs']]
    state = (run['status'], run['conclusion'], summary)
    if state != last:
        at = datetime.datetime.now(datetime.timezone.utc)
        output = EV / ('parent-builds-ad50-main-phase-' + at.strftime('%Y%m%dT%H%M%SZ') + '.json')
        output.write_text(json.dumps({'utc': at.isoformat(), 'run': run, 'jobs': jobs}, indent=2) + '\n')
        print(json.dumps({'utc': at.isoformat(), 'run': RUN, 'status': run['status'], 'conclusion': run['conclusion'], 'jobs': summary}), flush=True)
        last = state
    if run['status'] == 'completed':
        raise SystemExit(0 if run['conclusion'] == 'success' else 1)
    time.sleep(30)
