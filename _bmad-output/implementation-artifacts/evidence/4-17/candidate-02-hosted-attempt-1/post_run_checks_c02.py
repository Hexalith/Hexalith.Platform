"""After the terminal status: GET-only checks that the tag, rulesets and run inventory are unchanged.

Usage (Platform root): python3 -I post_run_checks_c02.py RUN_ID TAG SOURCE_SHA POLICY_JSON OUTPUT_JSON
"""
import datetime
import json
import subprocess
import sys
from pathlib import Path

run_id, tag, source, policy_path, output = sys.argv[1], sys.argv[2], sys.argv[3], Path(sys.argv[4]), Path(sys.argv[5])
REPO = 'Hexalith/Hexalith.Platform'


def gh(path):
    return json.loads(subprocess.run(['gh', 'api', '--method', 'GET', path], capture_output=True, text=True, check=True).stdout)


tag_ref = subprocess.run(['git', 'ls-remote', 'origin', 'refs/tags/' + tag], capture_output=True, text=True, check=True).stdout.split()
policy = {r['id']: r for r in json.loads(policy_path.read_text())['rulesets']}
rulesets = {}
for ruleset_id in (24657570, 24657217):
    live = gh(f'repos/{REPO}/rulesets/{ruleset_id}')
    rulesets[ruleset_id] = (live['enforcement'] == policy[ruleset_id]['enforcement'] and [r['type'] for r in live['rules']] == policy[ruleset_id]['rules']
                            and live['conditions'] == policy[ruleset_id]['conditions'])
runs = gh(f'repos/{REPO}/actions/workflows/415133929/runs?per_page=100')
run = gh(f'repos/{REPO}/actions/runs/{run_id}')
jobs = gh(f'repos/{REPO}/actions/runs/{run_id}/jobs')['jobs']
record = {'observedUtc': datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ'),
          'tagResolvesToSource': tag_ref == [source, 'refs/tags/' + tag], 'rulesetsUnchanged': rulesets,
          'workflowRuns': runs['total_count'], 'runIds': [r['id'] for r in runs['workflow_runs']],
          'run': {k: run.get(k) for k in ('id', 'run_attempt', 'event', 'status', 'conclusion', 'head_branch', 'head_sha', 'created_at', 'run_started_at', 'updated_at')},
          'jobs': [{'id': j['id'], 'name': j['name'], 'status': j['status'], 'conclusion': j['conclusion'], 'startedAt': j['started_at'],
                    'completedAt': j['completed_at'], 'runnerName': j.get('runner_name'), 'runnerGroup': j.get('runner_group_name'), 'labels': j.get('labels'),
                    'steps': [{'number': s['number'], 'name': s['name'], 'conclusion': s['conclusion']} for s in j['steps']]} for j in jobs]}
output.write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps({k: record[k] for k in ('tagResolvesToSource', 'rulesetsUnchanged', 'workflowRuns')}))
