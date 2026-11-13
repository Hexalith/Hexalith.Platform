"""Candidate-02 go/no-go checks before tag creation (GET/read-only only).

Usage (Platform repository root): python3 -I go_no_go.py SOURCE_SHA TAG POLICY_JSON OUTPUT_JSON
Exit 0 only when every check passes. Nothing is created, dispatched or changed.
"""
import datetime
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

source, tag, policy_path, output = sys.argv[1], sys.argv[2], Path(sys.argv[3]), Path(sys.argv[4])
REPO = 'Hexalith/Hexalith.Platform'
WORKFLOW_ID = 415133929
EVENTSTORE = '495dd24943031ec75e2822c6168ada3c8b9fdb82'
CONFIG = 'docs/implementation/story-4-17-broker-candidate-02.json'
CONFIG_SHA = 'b8601e3a6d4e8460d039573ca2f7e1728e52aa67e61fc0710d74cab5d072580b'
checks = []


def now():
    return datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')


def run(argv, **kwargs):
    return subprocess.run(argv, capture_output=True, text=True, **kwargs)


def gh(path):
    proc = run(['gh', 'api', '--method', 'GET', path])
    if proc.returncode:
        raise SystemExit(f'gh api {path} failed: {proc.stderr.strip()}')
    return json.loads(proc.stdout)


def record(name, argv, result, passed):
    checks.append({'check': name, 'observedUtc': now(), 'argv': argv, 'result': result, 'pass': bool(passed)})


# 1. Provider status.
argv = ['curl', '-sS', '-A', 'status-check', 'https://www.githubstatus.com/api/v2/summary.json']
status = json.loads(run(argv, check=True).stdout)
components = {c['name']: c['status'] for c in status['components'] if c['name'] in ('Actions', 'API Requests', 'Git Operations', 'Webhooks')}
open_incidents = [i['name'] for i in status.get('incidents', []) if i.get('status') != 'resolved']
record('githubstatus: indicator none, core components operational, no unresolved incident', argv,
       {'indicator': status['status']['indicator'], 'description': status['status']['description'], 'pageUpdatedAt': status['page']['updated_at'],
        'components': components, 'unresolvedIncidents': open_incidents, 'scheduledMaintenances': [m['name'] for m in status.get('scheduled_maintenances', [])]},
       status['status']['indicator'] == 'none' and set(components.values()) == {'operational'} and not open_incidents)

# 2. Rulesets equal to the recorded policy.
policy = {r['id']: r for r in json.loads(policy_path.read_text())['rulesets'] if r['id'] in (24657570, 24657217)}
for ruleset_id in (24657570, 24657217):
    live = gh(f'repos/{REPO}/rulesets/{ruleset_id}')
    observed = {'id': live['id'], 'name': live['name'], 'target': live['target'], 'enforcement': live['enforcement'],
                'conditions': live['conditions'], 'rules': [r['type'] for r in live['rules']],
                'bypassActors': [{'actorType': a['actor_type'], 'bypassMode': a['bypass_mode']} for a in live.get('bypass_actors', [])],
                'currentUserCanBypass': live.get('current_user_can_bypass')}
    expected = dict(policy[ruleset_id]); expected['bypassActors'] = [{'actorType': a['actorType'], 'bypassMode': a['bypassMode']} for a in expected['bypassActors']]
    record(f'ruleset {ruleset_id} equals the recorded policy', ['gh', 'api', '--method', 'GET', f'repos/{REPO}/rulesets/{ruleset_id}'],
           observed, observed == expected)

# 3. Tag absent.
argv = ['git', 'ls-remote', 'origin', 'refs/tags/' + tag, 'refs/tags/qualification/broker-candidate-02/*']
listing = run(argv, check=True).stdout.strip()
record('the candidate-02 tag and namespace do not exist on origin', argv, {'refs': listing.splitlines()}, listing == '')

# 4. Workflow active and idle.
workflow = gh(f'repos/{REPO}/actions/workflows/{WORKFLOW_ID}')
runs = gh(f'repos/{REPO}/actions/workflows/{WORKFLOW_ID}/runs?per_page=100')
busy = {state: gh(f'repos/{REPO}/actions/workflows/{WORKFLOW_ID}/runs?status={state}')['total_count']
        for state in ('queued', 'in_progress', 'waiting', 'pending', 'requested')}
record('workflow 415133929 active with exactly the two earlier runs and none busy',
       ['gh', 'api', '--method', 'GET', f'repos/{REPO}/actions/workflows/{WORKFLOW_ID} (+ /runs, /runs?status=...)'],
       {'state': workflow['state'], 'path': workflow['path'], 'totalRuns': runs['total_count'],
        'runs': [{'id': r['id'], 'status': r['status'], 'conclusion': r['conclusion'], 'headSha': r['head_sha']} for r in runs['workflow_runs']],
        'busy': busy},
       workflow['state'] == 'active' and runs['total_count'] == 2 and {r['id'] for r in runs['workflow_runs']} == {43097161643, 43333995506} and not any(busy.values()))

# 5. Identities at the published source.
remote_branch = run(['git', 'ls-remote', 'origin', 'refs/heads/chore/broker-candidate-02-source'], check=True).stdout.split()
workflow_text = run(['git', 'show', source + ':.github/workflows/broker-candidate-qualification.yml'], check=True).stdout
config_bytes = subprocess.run(['git', 'show', source + ':' + CONFIG], capture_output=True, check=True).stdout
gitlink = run(['git', 'ls-tree', source, 'references/Hexalith.EventStore'], check=True).stdout.strip()
env = dict(re.findall(r'^      ([A-Z0-9_]+): (\S+)$', workflow_text, re.M))
jobs = re.findall(r'^  ([a-z0-9-]+):$', workflow_text.split('\njobs:\n', 1)[1], re.M)
contained = run(['git', '-C', 'references/Hexalith.EventStore', 'merge-base', '--is-ancestor', EVENTSTORE, 'origin/main']).returncode == 0
identity = {'remoteBranch': remote_branch, 'workflowBlob': run(['git', 'rev-parse', source + ':.github/workflows/broker-candidate-qualification.yml'], check=True).stdout.strip(),
            'workflowSha256': hashlib.sha256(workflow_text.encode()).hexdigest(), 'env': env, 'jobs': jobs, 'gitlink': gitlink,
            'configurationSha256': hashlib.sha256(config_bytes).hexdigest(), 'eventStoreRevisionContainedInRemoteMain': contained}
record('source, gitlink, workflow pins, job and configuration identities agree', ['git', 'show/ls-tree/ls-remote', source], identity,
       remote_branch == [source, 'refs/heads/chore/broker-candidate-02-source'] and env.get('EVENTSTORE_REVISION') == EVENTSTORE
       and env.get('CONFIGURATION_SHA256') == CONFIG_SHA and jobs == ['candidate-02']
       and gitlink == f'160000 commit {EVENTSTORE}\treferences/Hexalith.EventStore' and identity['configurationSha256'] == CONFIG_SHA and contained)

# 6. No workflow at the source triggers on tag creation.
names = run(['git', 'ls-tree', '--name-only', source, '.github/workflows/'], check=True).stdout.split()
script = ("const yaml=require('js-yaml');let s='';process.stdin.on('data',d=>s+=d).on('end',()=>{const o=JSON.parse(s);"
          "const r={};for(const [n,t] of Object.entries(o)){const d=yaml.load(t);const on=d.on??d[true];r[n]=on;}console.log(JSON.stringify(r));});")
texts = {name: run(['git', 'show', f'{source}:{name}'], check=True).stdout for name in names}
parsed = json.loads(run(['node', '-e', script], input=json.dumps(texts), check=True, cwd='.').stdout)
survey, tag_triggers = {}, []
for name, on in parsed.items():
    keys = list(on) if isinstance(on, dict) else ([on] if isinstance(on, str) else on)
    survey[name] = keys
    for event in ('push', 'create'):
        if event in keys:
            filters = (on.get(event) or {}) if isinstance(on, dict) else {}
            if event == 'create' or 'tags' in filters or 'tags-ignore' in filters or not ({'branches', 'branches-ignore'} & set(filters)):
                tag_triggers.append(f'{name}:{event}')
record('no workflow at the source runs on tag creation', ['git show', source, '.github/workflows/*', 'parsed with js-yaml'],
       {'triggers': survey, 'tagTriggers': tag_triggers}, not tag_triggers)

result = {'sourceRevision': source, 'tag': tag, 'checks': checks, 'allPassed': all(c['pass'] for c in checks)}
output.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({c['check']: c['pass'] for c in checks}, indent=1))
raise SystemExit(0 if result['allPassed'] else 1)
