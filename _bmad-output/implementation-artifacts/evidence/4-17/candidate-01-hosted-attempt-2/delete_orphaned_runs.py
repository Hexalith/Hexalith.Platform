"""Delete the 43 run records orphaned in 'queued' since 2026-11-11 (owner-approved 2026-11-13, "yes").

Usage (from the Platform repository root):
    python3 -I delete_orphaned_runs.py EVIDENCE_DIR

Targets come only from EVIDENCE_DIR/orphaned-run-cancellation.json. Before deleting, each run must still be
queued with zero jobs and the same head SHA and creation time; any change skips it. One DELETE per run, no
retry, then a GET that should return 404. Writes EVIDENCE_DIR/orphaned-run-deletion.json.
"""
import datetime
import json
import subprocess
import sys
from pathlib import Path

evidence = Path(sys.argv[1])
targets = json.loads((evidence / 'orphaned-run-cancellation.json').read_text())['runs']
if len(targets) != 43:
    raise SystemExit(f'expected 43 targets, found {len(targets)}')


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def api(method, path):
    proc = subprocess.run(['gh', 'api', '--include', '--method', method, path], capture_output=True, text=True)
    head, _, body = proc.stdout.partition('\r\n\r\n')
    if not body and '\n\n' in proc.stdout:
        head, _, body = proc.stdout.partition('\n\n')
    line = head.splitlines()[0] if head else ''
    status = int(line.split()[1]) if line.startswith('HTTP/') else None
    request_id = next((l.split(':', 1)[1].strip() for l in head.splitlines()
                       if l.lower().startswith('x-github-request-id:')), None)
    try:
        data = json.loads(body) if body.strip() else None
    except json.JSONDecodeError:
        data = None
    message = data.get('message') if isinstance(data, dict) and status and status >= 400 else None
    return {'observedUtc': now(), 'method': method, 'path': path, 'exitCode': proc.returncode,
            'httpStatus': status, 'githubRequestId': request_id, **({'message': message} if message else {})}, data


rows = []
for target in targets:
    repo, run_id = target['repository'], target['runId']
    row = {'repository': repo, 'runId': run_id, 'name': target['name'], 'headSha': target['headSha'],
           'createdAt': target['createdAt'], 'steps': []}
    receipt, run = api('GET', f'repos/{repo}/actions/runs/{run_id}')
    row['steps'].append(receipt)
    jobs_receipt, jobs = api('GET', f'repos/{repo}/actions/runs/{run_id}/jobs?filter=all')
    row['steps'].append(jobs_receipt)
    unchanged = (receipt['httpStatus'] == 200 and run and run.get('status') == 'queued'
                 and run.get('head_sha') == target['headSha'] and run.get('created_at') == target['createdAt']
                 and (jobs or {}).get('total_count') == 0)
    row['preDeleteState'] = {'status': (run or {}).get('status'), 'jobs': (jobs or {}).get('total_count')}
    if not unchanged:
        row['result'] = 'skipped-target-changed'
        rows.append(row)
        continue
    delete_receipt, _ = api('DELETE', f'repos/{repo}/actions/runs/{run_id}')
    row['steps'].append(delete_receipt)
    after_receipt, _ = api('GET', f'repos/{repo}/actions/runs/{run_id}')
    row['steps'].append(after_receipt)
    row['result'] = ('deleted' if delete_receipt['httpStatus'] == 204 and after_receipt['httpStatus'] == 404
                     else f"not-deleted (DELETE {delete_receipt['httpStatus']}, GET after {after_receipt['httpStatus']})")
    rows.append(row)

summary = {}
for row in rows:
    summary[row['result']] = summary.get(row['result'], 0) + 1
(evidence / 'orphaned-run-deletion.json').write_text(json.dumps({
    'approvedBy': 'repository owner, 2026-11-13 reply "yes" to the offer to delete the 43 stuck run records',
    'targetsFrom': 'orphaned-run-cancellation.json',
    'targeted': len(targets), 'summary': summary, 'retries': 0, 'runs': rows}, indent=2) + '\n')
print(json.dumps(summary))
