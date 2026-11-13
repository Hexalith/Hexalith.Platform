"""Cancel runs orphaned in 'queued' by the 2026-11-11 Actions incident (owner-approved 2026-11-13).

Usage: python3 -I cancel_orphaned_runs.py OUTPUT.json
Targets only runs created 2026-11-11T20:25:00Z..20:55:00Z that are still queued with zero jobs.
Tries POST .../cancel; if GitHub refuses, POST .../force-cancel; then reads the status back.
"""
import datetime, json, subprocess, sys, time

REPOS = ['Hexalith.Platform', 'Hexalith.EventStore', 'Hexalith.Parties', 'Hexalith.Memories', 'Hexalith.Tenants', 'Hexalith.Builds']


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def api(method, path):
    proc = subprocess.run(['gh', 'api', '--include', '--method', method, path], capture_output=True, text=True)
    head, _, body = proc.stdout.partition('\r\n\r\n')
    if not body and '\n\n' in proc.stdout:
        head, _, body = proc.stdout.partition('\n\n')
    line = head.splitlines()[0] if head else ''
    status = int(line.split()[1]) if line.startswith('HTTP/') else None
    request_id = next((l.split(':', 1)[1].strip() for l in head.splitlines() if l.lower().startswith('x-github-request-id:')), None)
    try:
        data = json.loads(body) if body.strip() else None
    except json.JSONDecodeError:
        data = None
    return {'method': method, 'path': path, 'exitCode': proc.returncode, 'httpStatus': status, 'githubRequestId': request_id}, data


rows = []
for repo in REPOS:
    receipt, listing = api('GET', f'repos/Hexalith/{repo}/actions/runs?status=queued&created=2026-11-11T20:25:00Z..2026-11-11T20:55:00Z&per_page=100')
    for run in (listing or {}).get('workflow_runs', []):
        row = {'repository': f'Hexalith/{repo}', 'runId': run['id'], 'name': run['name'], 'headSha': run['head_sha'],
               'createdAt': run['created_at'], 'statusBefore': run['status'], 'listing': receipt, 'steps': []}
        jobs_receipt, jobs = api('GET', f"repos/Hexalith/{repo}/actions/runs/{run['id']}/jobs?filter=all")
        row['jobsBefore'] = (jobs or {}).get('total_count')
        row['steps'].append({'observedUtc': now(), **jobs_receipt})
        if run['status'] != 'queued' or row['jobsBefore'] != 0:
            row['action'] = 'skipped-not-orphaned'
            rows.append(row)
            continue
        cancel_receipt, _ = api('POST', f"repos/Hexalith/{repo}/actions/runs/{run['id']}/cancel")
        row['steps'].append({'observedUtc': now(), **cancel_receipt})
        row['action'] = 'cancel'
        if cancel_receipt['httpStatus'] != 202:
            force_receipt, _ = api('POST', f"repos/Hexalith/{repo}/actions/runs/{run['id']}/force-cancel")
            row['steps'].append({'observedUtc': now(), **force_receipt})
            row['action'] = 'force-cancel'
        rows.append(row)
if not rows:
    raise SystemExit('no orphaned runs parsed; refusing to record an empty result')
time.sleep(20)
for row in rows:
    receipt, run = api('GET', f"repos/{row['repository']}/actions/runs/{row['runId']}")
    row['after'] = {'observedUtc': now(), 'status': (run or {}).get('status'), 'conclusion': (run or {}).get('conclusion'), **receipt}
result = {'approvedBy': 'repository owner, 2026-11-13 decision dialog', 'window': '2026-11-11T20:25:00Z..20:55:00Z',
          'targeted': len(rows), 'actions': {a: sum(1 for r in rows if r['action'] == a) for a in sorted({r['action'] for r in rows})},
          'afterStatus': {s: sum(1 for r in rows if f"{r['after']['status']}/{r['after']['conclusion']}" == s)
                          for s in sorted({f"{r['after']['status']}/{r['after']['conclusion']}" for r in rows})},
          'runs': rows}
open(sys.argv[1], 'w').write(json.dumps(result, indent=2) + '\n')
print(json.dumps({k: result[k] for k in ['targeted', 'actions', 'afterStatus']}))
