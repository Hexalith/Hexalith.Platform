"""Monitor candidate-02 attempt 1 with GET polling until the run completes.

Usage: python3 -I monitor_attempt.py RUN_ID DISPATCHED_UTC OUTPUT_JSON
The only mutation it may make is the approved 30-minute no-job rule: if no job has started 30
minutes after dispatch, it sends one cancel for this run, records not-allocated, and stops.
"""
import datetime
import json
import subprocess
import sys
import time
from pathlib import Path

run_id, dispatched, output = sys.argv[1], sys.argv[2], Path(sys.argv[3])
REPO = 'Hexalith/Hexalith.Platform'
start = datetime.datetime.fromisoformat(dispatched.replace('Z', '+00:00'))
observations = []


def now():
    return datetime.datetime.now(datetime.timezone.utc)


def gh(path, method='GET'):
    proc = subprocess.run(['gh', 'api', '--method', method, path], capture_output=True, text=True)
    return proc.returncode, (json.loads(proc.stdout) if proc.stdout.strip().startswith(('{', '[')) else proc.stdout), proc.stderr.strip()


def save(extra=None):
    output.write_text(json.dumps({'runId': int(run_id), 'dispatchedUtc': dispatched, 'observations': observations, **(extra or {})}, indent=2) + '\n')


job_started = False
while True:
    code, run, _ = gh(f'repos/{REPO}/actions/runs/{run_id}')
    jcode, jobs, _ = gh(f'repos/{REPO}/actions/runs/{run_id}/jobs')
    row = {'observedUtc': now().strftime('%Y-%m-%dT%H:%M:%SZ'), 'status': run.get('status') if code == 0 else None,
           'conclusion': run.get('conclusion') if code == 0 else None,
           'jobs': [{'id': j['id'], 'status': j['status'], 'conclusion': j['conclusion'], 'startedAt': j['started_at'],
                     'runner': j.get('runner_name'), 'labels': j.get('labels'),
                     'steps': [[s['number'], s['name'], s['status'], s['conclusion']] for s in j.get('steps', [])]}
                    for j in (jobs.get('jobs', []) if jcode == 0 else [])]}
    if not observations or {k: v for k, v in row.items() if k != 'observedUtc'} != {k: v for k, v in observations[-1].items() if k != 'observedUtc'}:
        observations.append(row); save()
    job_started = job_started or any(j['startedAt'] for j in row['jobs'])
    if row['status'] == 'completed':
        save({'result': 'completed', 'finalRun': {k: run.get(k) for k in ('id', 'run_attempt', 'status', 'conclusion', 'head_sha', 'head_branch', 'event', 'created_at', 'run_started_at', 'updated_at')}})
        print(json.dumps({'status': 'completed', 'conclusion': row['conclusion']}))
        break
    if not job_started and now() - start > datetime.timedelta(minutes=30):
        ccode, body, err = gh(f'repos/{REPO}/actions/runs/{run_id}/cancel', 'POST')
        save({'result': 'not-allocated', 'cancel': {'exitCode': ccode, 'stderr': err}})
        print(json.dumps({'status': 'not-allocated', 'cancelExit': ccode}))
        break
    if now() - start > datetime.timedelta(minutes=75):
        save({'result': 'monitor-timeout-without-mutation'})
        print(json.dumps({'status': 'monitor-timeout'}))
        break
    time.sleep(30)
