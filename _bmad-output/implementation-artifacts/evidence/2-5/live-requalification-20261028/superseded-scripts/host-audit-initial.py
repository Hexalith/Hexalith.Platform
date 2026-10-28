"""Observe only ownership identifiers, never complete process environments."""
from datetime import datetime, timezone
from pathlib import Path
import json
import subprocess
import sys

out = Path(__file__).resolve().parent
phase = sys.argv[1]
assert phase in ['before', 'after']
containers = [json.loads(line) for line in subprocess.check_output([
    'docker', 'ps', '--all', '--no-trunc', '--format',
    '{"id":{{json .ID}},"name":{{json .Names}},"image":{{json .Image}},"state":{{json .State}}}',
], text=True).splitlines()]
owners = {}
unreadable = []
for path in Path('/proc').glob('[0-9]*/environ'):
    try:
        environment = path.read_bytes().split(b'\0')
    except FileNotFoundError:
        continue
    except PermissionError:
        if path.stat().st_uid == __import__('os').getuid():
            unreadable.append(int(path.parent.name))
        continue
    except ProcessLookupError:
        continue
    for token in environment:
        if token.startswith(b'HEXALITH_G4_RUN_ID='):
            owner = token.partition(b'=')[2].decode()
            owners.setdefault(owner, []).append(int(path.parent.name))
observation = {'observedAtUtc': datetime.now(timezone.utc).isoformat(), 'containers': containers, 'ownedProcesses': owners, 'unreadableSameUserProcesses': unreadable}
assert not unreadable, unreadable
if phase == 'after':
    run_ids = set()
    live = out / 'live'
    for path in live.glob('command-*.json'):
        run_ids.add(json.loads(path.read_text())['runId'])
    for path in live.glob('peer-audit-*.json'):
        run_ids.add(json.loads(path.read_text())['RunId'])
    assert len(run_ids) == 5, run_ids
    scoped = {}
    for owner in sorted(run_ids):
        ids = subprocess.check_output(['docker', 'ps', '--all', '--quiet', '--no-trunc', '--filter', 'label=hexalith.g4.run=' + owner], text=True).split()
        scoped[owner] = {'containerIds': ids, 'processIds': owners.get(owner, [])}
    observation['qualificationOwners'] = scoped
    observation['ownedResourcesAbsent'] = all(not item['containerIds'] and not item['processIds'] for item in scoped.values())
    before = json.loads((out / 'host-before.json').read_text())
    after_ids = {item['id'] for item in containers}
    observation['preexistingContainersPreserved'] = all(item['id'] in after_ids for item in before['containers'])
(out / ('host-' + phase + '.json')).write_text(json.dumps(observation, indent=2) + '\n')
if phase == 'after':
    assert observation['ownedResourcesAbsent'], scoped
    assert observation['preexistingContainersPreserved'], 'A preexisting container disappeared'
print(json.dumps({key: value for key, value in observation.items() if key not in ['containers', 'ownedProcesses']}))
