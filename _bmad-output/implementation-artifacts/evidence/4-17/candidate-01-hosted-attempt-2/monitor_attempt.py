"""Discover and monitor the single attempt-2 run; apply the owner-approved 30-minute no-job timeout.

Usage: python3 -I monitor_attempt.py EVIDENCE_DIR
Writes run-discovery.json and appends to monitoring-observations.json until the run completes.
"""
import datetime, json, subprocess, sys, time
from pathlib import Path

out = Path(sys.argv[1])
TAG = 'qualification/broker-candidate-01/attempt-2-94001fad'
SHA = '94001fad925fdf7fa03a90b507b8f8e19074f9e3'
R = 'repos/Hexalith/Hexalith.Platform'
started = (out / 'single-dispatch.started').read_text().strip()


def now():
    return datetime.datetime.now(datetime.timezone.utc)


def get(path):
    proc = subprocess.run(['gh', 'api', '--method', 'GET', path], capture_output=True, text=True)
    return json.loads(proc.stdout) if proc.returncode == 0 and proc.stdout.strip() else None


run = None
for _ in range(40):
    listing = get(f'{R}/actions/workflows/415133929/runs?per_page=20') or {}
    candidates = [r for r in listing.get('workflow_runs', []) if r['id'] != 43097161643]
    if candidates:
        run = candidates
        break
    time.sleep(15)
(out / 'run-discovery.json').write_text(json.dumps({
    'observedUtc': now().isoformat(), 'dispatchStarted': started,
    'newRuns': [{k: r.get(k) for k in ['id', 'run_number', 'run_attempt', 'event', 'head_branch', 'head_sha', 'status',
                                       'conclusion', 'created_at', 'html_url']} for r in (run or [])],
    'workflowRunsTotalCount': listing.get('total_count')}, indent=2) + '\n')
if not run or len(run) != 1:
    raise SystemExit(f'expected exactly one new run, found {len(run or [])}')
run_id = run[0]['id']
created = datetime.datetime.fromisoformat(run[0]['created_at'].replace('Z', '+00:00'))
observations, cancelled = [], None
while True:
    r = get(f'{R}/actions/runs/{run_id}') or {}
    jobs = (get(f'{R}/actions/runs/{run_id}/jobs?filter=all') or {}).get('jobs', [])
    observations.append({'observedUtc': now().isoformat(), 'status': r.get('status'), 'conclusion': r.get('conclusion'),
                         'runAttempt': r.get('run_attempt'),
                         'jobs': [{k: j.get(k) for k in ['id', 'name', 'status', 'conclusion', 'started_at', 'completed_at',
                                                         'runner_name', 'labels']} |
                                  {'steps': [{k: s.get(k) for k in ['number', 'name', 'status', 'conclusion']} for s in j.get('steps') or []]}
                                  for j in jobs]})
    (out / 'monitoring-observations.json').write_text(json.dumps({'runId': run_id, 'cancelledForQueueTimeout': cancelled,
                                                                  'observations': observations}, indent=2) + '\n')
    if r.get('status') == 'completed':
        break
    if not jobs and cancelled is None and now() - created > datetime.timedelta(minutes=30):
        proc = subprocess.run(['gh', 'run', 'cancel', str(run_id), '--repo', 'Hexalith/Hexalith.Platform'], capture_output=True, text=True)
        cancelled = {'observedUtc': now().isoformat(), 'exitCode': proc.returncode, 'reason': 'no job allocated within 30 minutes'}
    if now() - created > datetime.timedelta(minutes=95):
        raise SystemExit('monitor bound exceeded without completion')
    time.sleep(30)
print(json.dumps({'runId': run_id, 'status': observations[-1]['status'], 'conclusion': observations[-1]['conclusion'],
                  'jobs': [(j['name'], j['status'], j['conclusion']) for j in observations[-1]['jobs']],
                  'cancelledForQueueTimeout': cancelled, 'observations': len(observations)}))
