"""Read-only exact-source normal Actions monitor; never dispatches or cancels."""
import argparse
import datetime
import json
import re
import subprocess
import time
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--run', type=int, required=True)
parser.add_argument('--source', required=True)
parser.add_argument('--event', choices=['pull_request', 'push', 'workflow_dispatch'], required=True)
parser.add_argument('--workflow', choices=['.github/workflows/ci.yml', '.github/workflows/build-release.yml'], required=True)
parser.add_argument('--stem', required=True)
args = parser.parse_args()
assert re.fullmatch('[0-9a-f]{40}', args.source)
assert re.fullmatch('[a-z0-9-]+', args.stem)
assert args.run > 0
root = Path(__file__).resolve().parent
last = None
while True:
    def get(endpoint):
        result = subprocess.run(['gh', 'api', endpoint], capture_output=True)
        assert result.returncode == 0, result.stderr.decode()
        return json.loads(result.stdout)
    run = get(f'repos/Hexalith/Hexalith.Builds/actions/runs/{args.run}')
    jobs = get(f'repos/Hexalith/Hexalith.Builds/actions/runs/{args.run}/jobs?per_page=100')
    assert run['head_sha'] == args.source and run['event'] == args.event
    assert run['path'] == args.workflow and run['run_attempt'] == 1
    assert run['repository']['full_name'] == 'Hexalith/Hexalith.Builds'
    assert jobs['total_count'] == len(jobs['jobs'])
    summary = [(job['id'], job['name'], job['status'], job['conclusion']) for job in jobs['jobs']]
    state = (run['status'], run['conclusion'], summary)
    if state != last:
        at = datetime.datetime.now(datetime.timezone.utc)
        output = root / (args.stem + '-phase-' + at.strftime('%Y%m%dT%H%M%SZ') + '.json')
        assert not output.exists()
        output.write_text(json.dumps({'utc': at.isoformat(), 'run': run, 'jobs': jobs}, indent=2) + '\n')
        print(json.dumps({'utc': at.isoformat(), 'run': args.run, 'status': run['status'], 'conclusion': run['conclusion'], 'jobs': summary}), flush=True)
        last = state
    if run['status'] == 'completed':
        raise SystemExit(0 if run['conclusion'] == 'success' else 1)
    time.sleep(30)
