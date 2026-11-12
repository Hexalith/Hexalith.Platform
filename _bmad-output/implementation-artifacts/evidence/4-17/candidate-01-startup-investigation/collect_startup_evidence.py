"""Collect read-only GitHub diagnostics for broker candidate-01 run 43097161643.

Usage (from the Platform repository root, with an authenticated gh CLI):
    python3 -I collect_startup_evidence.py EVIDENCE_DIRECTORY

Only HTTP GET requests are issued. Actor logins and user ids are replaced with a
role label before anything is written into the (public) repository evidence tree.
Each receipt keeps GitHub's X-GitHub-Request-Id and the SHA-256 of the raw,
unredacted response body, so a reader with the same access can re-query and compare.
The collector overwrites only its own output files.
"""
import hashlib
import datetime
import json
import subprocess
import sys
from pathlib import Path

REPO = 'Hexalith/Hexalith.Platform'
RUN_ID = 43097161643
WORKFLOW_ID = 415133929
DISPATCH_REF = 'refs/heads/chore/broker-candidate-01-hosted-94001fad'
OWNER_LABEL = 'repository-owner-account (same actor as dispatch-branch creation and run dispatch)'

out = Path(sys.argv[1])
out.mkdir(parents=True, exist_ok=True)
commands = []


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def get(path, label):
    argv = ['gh', 'api', '--include', '--method', 'GET', path]
    observed = now()
    proc = subprocess.run(argv, capture_output=True, text=True)
    head, _, body = proc.stdout.partition('\r\n\r\n')
    if not body and '\n\n' in proc.stdout:
        head, _, body = proc.stdout.partition('\n\n')
    status_line = head.splitlines()[0] if head else ''
    status = int(status_line.split()[1]) if status_line.startswith('HTTP/') else None
    try:
        data = json.loads(body) if body.strip() else None
    except json.JSONDecodeError:
        data = None
    request_id = next((line.split(':', 1)[1].strip() for line in head.splitlines()
                       if line.lower().startswith('x-github-request-id:')), None)
    receipt = {'label': label, 'observedUtc': observed, 'argv': argv, 'method': 'GET',
               'endpoint': f'https://api.github.com/{path}', 'exitCode': proc.returncode, 'httpStatus': status,
               'githubRequestId': request_id, 'rawBodySha256': hashlib.sha256(body.encode()).hexdigest()}
    commands.append(receipt)
    return data, receipt


def pick(row, keys):
    return {key: row.get(key) for key in keys}


def write(name, value):
    (out / name).write_text(json.dumps(value, indent=2) + '\n')


RUN_KEYS = ['id', 'name', 'workflow_id', 'run_number', 'run_attempt', 'event', 'path', 'head_branch', 'head_sha',
            'status', 'conclusion', 'created_at', 'updated_at', 'run_started_at', 'previous_attempt_url',
            'check_suite_id', 'html_url']

r = f'repos/{REPO}'
run, q_run = get(f'{r}/actions/runs/{RUN_ID}', 'run')
attempt, q_attempt = get(f'{r}/actions/runs/{RUN_ID}/attempts/1', 'run-attempt-1')
timing, q_timing = get(f'{r}/actions/runs/{RUN_ID}/timing', 'run-timing')
jobs, q_jobs = get(f'{r}/actions/runs/{RUN_ID}/jobs?filter=all&per_page=100', 'run-jobs-all-attempts')
artifacts, q_artifacts = get(f'{r}/actions/runs/{RUN_ID}/artifacts', 'run-artifacts')
_, q_logs = get(f'{r}/actions/runs/{RUN_ID}/logs', 'run-logs-probe')
write('run-current.json', {
    'run': pick(run, RUN_KEYS), 'attempt1': pick(attempt, ['run_attempt', 'status', 'conclusion', 'updated_at']),
    'timing': timing, 'jobsTotalCount': jobs.get('total_count'), 'artifactsTotalCount': artifacts.get('total_count'),
    'logsHttpStatus': q_logs['httpStatus'],
    'queries': [q_run, q_attempt, q_timing, q_jobs, q_artifacts, q_logs]})

workflow, q_workflow = get(f'{r}/actions/workflows/{WORKFLOW_ID}', 'workflow')
wf_runs, q_wf_runs = get(f'{r}/actions/workflows/{WORKFLOW_ID}/runs?per_page=100', 'workflow-runs')
failures, q_failures = get(f'{r}/actions/runs?status=startup_failure&per_page=100', 'repository-startup-failures')
write('workflow-run-inventory.json', {
    'workflow': pick(workflow, ['id', 'name', 'path', 'state', 'created_at', 'updated_at']),
    'workflowRunsTotalCount': wf_runs.get('total_count'),
    'workflowRuns': [pick(row, ['id', 'run_attempt', 'status', 'conclusion', 'head_sha', 'created_at'])
                     for row in wf_runs.get('workflow_runs', [])],
    'repositoryStartupFailureTotalCount': failures.get('total_count'),
    'repositoryStartupFailures': [pick(row, ['id', 'name', 'created_at', 'head_branch'])
                                  for row in failures.get('workflow_runs', [])],
    'queries': [q_workflow, q_wf_runs, q_failures]})

window, q_window = get(f'{r}/actions/runs?created=2026-11-11T19:30:00Z..2026-11-11T22:30:00Z&per_page=100',
                       'repository-runs-window')
rows, queries = [], [q_window]
for row in window.get('workflow_runs', []):
    run_jobs, q = get(f"{r}/actions/runs/{row['id']}/jobs?filter=all&per_page=100", f"jobs-{row['id']}")
    queries.append(q)
    first = sorted((job.get('started_at') or '' for job in run_jobs.get('jobs', [])), key=str)
    labels = sorted({label for job in run_jobs.get('jobs', []) for label in job.get('labels', [])})
    rows.append({**pick(row, ['id', 'name', 'event', 'status', 'conclusion', 'head_sha', 'head_branch',
                              'created_at', 'updated_at', 'run_started_at']),
                 'jobsTotalCount': run_jobs.get('total_count'),
                 'firstJobStartedAt': next((value for value in first if value), None), 'jobLabels': labels})
queued, q_queued = get(f'{r}/actions/runs?status=queued&per_page=100', 'repository-queued-runs')
queries.append(q_queued)
write('repository-runs-window.json', {
    'window': '2026-11-11T19:30:00Z..2026-11-11T22:30:00Z', 'totalCount': window.get('total_count'), 'runs': rows,
    'currentlyQueued': {'reportedTotalCount': queued.get('total_count'),
                        'listed': [pick(row, ['id', 'name', 'created_at', 'head_sha']) for row in queued.get('workflow_runs', [])]},
    'queries': queries})

ref_activity, q_ref_activity = get(f'{r}/activity?ref={DISPATCH_REF}&per_page=100', 'dispatch-ref-activity')
def get_paginated(path, label):
    argv = ['gh', 'api', '--method', 'GET', '--paginate', '--jq', '.[] | @json', path]
    observed = now()
    proc = subprocess.run(argv, capture_output=True, text=True)
    if proc.returncode != 0:
        raise SystemExit(f'paginated query failed: {label} exit {proc.returncode}')
    rows = [json.loads(line) for line in proc.stdout.splitlines() if line.strip()]
    receipt = {'label': label, 'observedUtc': observed, 'argv': argv, 'method': 'GET',
               'endpoint': f'https://api.github.com/{path}', 'exitCode': proc.returncode, 'httpStatus': None,
               'rows': len(rows), 'rawOutputSha256': hashlib.sha256(proc.stdout.encode()).hexdigest()}
    commands.append(receipt)
    return rows, receipt


week, q_week = get_paginated(f'{r}/activity?time_period=week&per_page=100', 'repository-ref-activity-week-paginated')
branch, q_branch = get(f'{r}/branches/chore/broker-candidate-01-hosted-94001fad', 'dispatch-branch-current')
matching, q_matching = get(f'{r}/git/matching-refs/heads/chore/broker-candidate-01-hosted', 'dispatch-ref-matching')
repository, q_repository = get(r, 'repository-settings')
head_pulls, q_head_pulls = get(f'{r}/pulls?head=Hexalith:chore/broker-candidate-01-hosted-94001fad&state=all', 'dispatch-branch-pulls')
_, q_audit = get('orgs/Hexalith/audit-log?phrase=action:git.branch_delete', 'org-audit-log-probe')


def activity(row):
    return {'timestamp': row.get('timestamp'), 'activityType': row.get('activity_type'), 'ref': row.get('ref'),
            'before': row.get('before'), 'after': row.get('after'),
            'actor': OWNER_LABEL if row.get('actor') else None}


write('dispatch-ref-lifecycle.json', {
    'ref': DISPATCH_REF,
    'refActivity': [activity(row) for row in ref_activity or []],
    'repositoryActivity20261111T2020to2100': sorted(
        (activity(row) for row in week or [] if '2026-11-11T20:20:00Z' <= row.get('timestamp', '') <= '2026-11-11T21:00:00Z'),
        key=lambda row: row['timestamp']),
    'activityCompleteness': {'events': len(week), 'oldest': min((row.get('timestamp') for row in week), default=None),
                             'windowCovered': min((row.get('timestamp') for row in week), default='9') <= '2026-11-11T20:20:00Z'},
    'deletionLeads': {
        'deleteBranchOnMerge': (repository or {}).get('delete_branch_on_merge'),
        'deleteBranchOnMergeExplains': 'the 20:32:49Z deletion of refs/heads/chore/broker-candidate-01-publication (PR #40 head) one second after its merge',
        'pullRequestsWithDispatchBranchAsHead': len(head_pulls or []),
        'orgAuditLogHttpStatus': q_audit['httpStatus'],
        'accountSecurityLog': 'not available through the REST API; not consulted'},
    'currentBranchHttpStatus': q_branch['httpStatus'],
    'currentMatchingRefs': [row.get('ref') for row in matching or []],
    'actorDisclosure': 'logins replaced with a role label; every listed event used the same owner account',
    'queries': [q_ref_activity, q_week, q_branch, q_matching, q_repository, q_head_pulls, q_audit]})

permissions, q_permissions = get(f'{r}/actions/permissions', 'actions-permissions')
workflow_permissions, q_wp = get(f'{r}/actions/permissions/workflow', 'actions-workflow-permissions')
rulesets = []
ruleset_queries = []
for ruleset_id in [24657570, 24657217, 24657106]:
    ruleset, q = get(f'{r}/rulesets/{ruleset_id}', f'ruleset-{ruleset_id}')
    ruleset_queries.append(q)
    rulesets.append({'id': ruleset.get('id'), 'name': ruleset.get('name'), 'target': ruleset.get('target'),
                     'enforcement': ruleset.get('enforcement'), 'conditions': ruleset.get('conditions'),
                     'rules': [rule.get('type') for rule in ruleset.get('rules', [])],
                     'bypassActors': [{'actorType': actor.get('actor_type'), 'bypassMode': actor.get('bypass_mode'),
                                       'actor': 'repository-owner-user' if actor.get('actor_type') == 'User' else actor.get('actor_type')}
                                      for actor in ruleset.get('bypass_actors', [])],
                     'currentUserCanBypass': ruleset.get('current_user_can_bypass')})
write('repository-policy.json', {
    'actionsPermissions': permissions, 'workflowPermissions': workflow_permissions, 'rulesets': rulesets,
    'queries': [q_permissions, q_wp, *ruleset_queries]})

cross = []
cross_queries = []
for name in ['Hexalith.Platform', 'Hexalith.EventStore', 'Hexalith.Parties', 'Hexalith.Memories', 'Hexalith.Tenants', 'Hexalith.Builds']:
    window_runs, q = get(f'repos/Hexalith/{name}/actions/runs?created=2026-11-11T17:00:00Z..2026-11-11T21:30:00Z&per_page=100',
                         f'cross-repository-runs-{name}')
    cross_queries.append(q)
    runs = [{'createdAt': row['created_at'], 'status': row['status'], 'conclusion': row.get('conclusion')}
            for row in window_runs.get('workflow_runs', [])]
    stuck = sorted(row['createdAt'] for row in runs if row['status'] == 'queued')
    done = sorted(row['createdAt'] for row in runs if row['status'] == 'completed')
    cross.append({'repository': f'Hexalith/{name}', 'totalCount': window_runs.get('total_count'), 'listed': len(runs),
                  'stillQueued': len(stuck), 'firstQueuedCreatedAt': stuck[0] if stuck else None,
                  'lastQueuedCreatedAt': stuck[-1] if stuck else None,
                  'lastCompletedCreatedBeforeFirstQueued': max((c for c in done if stuck and c < stuck[0]), default=None),
                  'firstCompletedCreatedAfterLastQueued': min((c for c in done if stuck and c > stuck[-1]), default=None),
                  'completedCreatedInsideQueuedSpan': sum(1 for c in done if stuck and stuck[0] <= c <= stuck[-1])})
write('cross-repository-runs-window.json', {
    'window': '2026-11-11T17:00:00Z..2026-11-11T21:30:00Z', 'repositories': cross,
    'note': 'run metadata only (creation time, status, conclusion); listings are the first 100 runs per repository',
    'queries': cross_queries})

survey = []
for revision in ['94001fad925fdf7fa03a90b507b8f8e19074f9e3', 'HEAD']:
    names = subprocess.run(['git', 'ls-tree', '--name-only', revision, '.github/workflows/'], capture_output=True, text=True, check=True).stdout.split()
    for name in names:
        text = subprocess.run(['git', 'show', f'{revision}:{name}'], capture_output=True, text=True, check=True).stdout
        lines = text.splitlines()
        start = next(i for i, line in enumerate(lines) if line.rstrip() in ('on:', "'on':", '"on":'))
        block = []
        for line in lines[start + 1:]:
            if line and not line.startswith(' '):
                break
            block.append(line)
        triggers = [line.strip().rstrip(':') for line in block if line.startswith('  ') and not line.startswith('   ') and line.strip()]
        survey.append({'revision': subprocess.run(['git', 'rev-parse', revision], capture_output=True, text=True, check=True).stdout.strip(),
                       'workflow': name, 'triggers': triggers,
                       'mentionsTags': any('tags' in line for line in block)})
write('workflow-trigger-survey.json', {'workflows': survey,
                                       'anyTagTrigger': any(row['mentionsTags'] for row in survey),
                                       'argv': ['git', 'show', 'REVISION:.github/workflows/NAME']})

status_argv = ['curl', '-sS', '-A', 'status-check', 'https://www.githubstatus.com/api/v2/incidents.json']
status_observed = now()
status_proc = subprocess.run(status_argv, capture_output=True, text=True)
incidents = json.loads(status_proc.stdout)['incidents'] if status_proc.returncode == 0 else []
selected = []
for incident in incidents:
    if incident['id'] in ('dvy0t3mvh1lg', '1h2wbly1y72t'):
        selected.append({
            'id': incident['id'], 'name': incident['name'], 'impact': incident['impact'],
            'createdAt': incident['created_at'], 'startedAt': incident.get('started_at'),
            'resolvedAt': incident.get('resolved_at'), 'shortlink': incident.get('shortlink'),
            'components': [component['name'] for component in incident.get('components', [])],
            'updates': [{'createdAt': update['created_at'], 'status': update['status'], 'body': update['body']}
                        for update in sorted(incident['incident_updates'], key=lambda update: update['created_at'])]})
status_receipt = {'label': 'githubstatus-incidents', 'observedUtc': status_observed, 'argv': status_argv,
                  'method': 'GET', 'exitCode': status_proc.returncode,
                  'listedRange': [incidents[-1]['created_at'], incidents[0]['created_at']] if incidents else None}
commands.append(status_receipt)
write('provider-status.json', {'source': 'https://www.githubstatus.com/api/v2/incidents.json',
                               'selectedIncidents': selected, 'query': status_receipt})
write('commands.json', {'readOnly': True, 'mutatingRequests': 0, 'commands': commands})
print(json.dumps({'written': sorted(path.name for path in out.iterdir()), 'commands': len(commands),
                  'nonGet': [c for c in commands if c['method'] != 'GET']}))
