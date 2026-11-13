"""Read-only post-terminal checks for attempt 2: tag, rulesets, run inventory, final job record.

Usage (from the Platform repository root):
    python3 -I post_run_checks.py EVIDENCE_DIR

Every call is a GET. Account logins and numeric user ids are replaced with role labels before writing.
"""
import datetime
import json
import subprocess
import sys
from pathlib import Path

evidence = Path(sys.argv[1])
REPO = 'Hexalith/Hexalith.Platform'
TAG = 'qualification/broker-candidate-01/attempt-2-94001fad'
SOURCE = '94001fad925fdf7fa03a90b507b8f8e19074f9e3'
RUN = 43333995506
WORKFLOW = 415133929
receipts = []


def gh(path, paginate=False):
    argv = ['gh', 'api', '--method', 'GET', path] + (['--paginate', '--jq', '.[] | @json'] if paginate else [])
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    proc = subprocess.run(argv, capture_output=True, text=True)
    receipts.append({'argv': argv, 'startedUtc': started, 'exitCode': proc.returncode})
    if proc.returncode != 0:
        raise SystemExit(f'GET failed: {path}: {proc.stderr.strip()}')
    if paginate:
        return [json.loads(line) for line in proc.stdout.splitlines() if line.strip()]
    return json.loads(proc.stdout)


me = gh('user')['id']
ref = gh(f'repos/{REPO}/git/ref/tags/{TAG}')
lsremote = subprocess.run(['git', 'ls-remote', 'origin', f'refs/tags/{TAG}'], capture_output=True, text=True)
receipts.append({'argv': ['git', 'ls-remote', 'origin', f'refs/tags/{TAG}'], 'exitCode': lsremote.returncode})
tag_activity = [e for e in gh(f'repos/{REPO}/activity?ref=refs/tags/{TAG}&per_page=100', paginate=True)]

previous = {r['id']: r for r in json.loads(
    Path('_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-startup-investigation/repository-policy.json')
    .read_text())['rulesets']}
rulesets = []
for ruleset_id in (24657570, 24657217):
    body = gh(f'repos/{REPO}/rulesets/{ruleset_id}')
    normalized = {
        'id': body['id'], 'name': body['name'], 'target': body['target'], 'enforcement': body['enforcement'],
        'conditions': body.get('conditions'), 'rules': [r['type'] for r in body.get('rules', [])],
        'bypassActors': [{'actorType': a.get('actor_type'), 'bypassMode': a.get('bypass_mode'),
                          'actor': 'repository-owner-user' if a.get('actor_type') == 'User' and a.get('actor_id') == me
                          else 'other-actor'} for a in body.get('bypass_actors', [])],
        'currentUserCanBypass': body.get('current_user_can_bypass'),
        'updatedAt': body.get('updated_at')}
    comparable = {k: v for k, v in normalized.items() if k != 'updatedAt'}
    rulesets.append({**normalized, 'unchangedFromRepositoryPolicyJson': comparable == previous[ruleset_id]})

runs = gh(f'repos/{REPO}/actions/workflows/{WORKFLOW}/runs?per_page=100')
run = gh(f'repos/{REPO}/actions/runs/{RUN}')
jobs = gh(f'repos/{REPO}/actions/runs/{RUN}/attempts/1/jobs?per_page=100')
keep = ['id', 'run_number', 'run_attempt', 'event', 'status', 'conclusion', 'head_branch', 'head_sha', 'created_at',
        'run_started_at', 'updated_at', 'path']
job_keep = ['id', 'name', 'status', 'conclusion', 'created_at', 'started_at', 'completed_at', 'runner_name',
            'runner_group_name', 'labels', 'head_sha', 'run_attempt']
result = {
    'observedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'tag': {'ref': ref['ref'], 'objectType': ref['object']['type'], 'objectSha': ref['object']['sha'],
            'resolvesToSource': ref['object']['type'] == 'commit' and ref['object']['sha'] == SOURCE,
            'lsRemote': lsremote.stdout.strip(),
            'activity': [{k: e.get(k) for k in ['ref', 'activity_type', 'before', 'after', 'timestamp']} for e in tag_activity]},
    'rulesets': rulesets,
    'workflowRuns': {'totalCount': runs['total_count'], 'runs': [{k: r.get(k) for k in keep} for r in runs['workflow_runs']]},
    'run': {k: run.get(k) for k in keep},
    'jobsAttempt1': {'totalCount': jobs['total_count'],
                     'jobs': [{**{k: j.get(k) for k in job_keep},
                               'steps': [{k: s.get(k) for k in ['number', 'name', 'status', 'conclusion', 'started_at', 'completed_at']}
                                         for s in j.get('steps', [])]} for j in jobs['jobs']]},
    'receipts': receipts,
}
(evidence / 'post-run-checks.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({'tagResolvesToSource': result['tag']['resolvesToSource'], 'tagEvents': len(tag_activity),
                  'rulesetsUnchanged': [r['unchangedFromRepositoryPolicyJson'] for r in rulesets],
                  'workflowRuns': runs['total_count'], 'runAttempt': run['run_attempt'], 'jobs': jobs['total_count']}))
