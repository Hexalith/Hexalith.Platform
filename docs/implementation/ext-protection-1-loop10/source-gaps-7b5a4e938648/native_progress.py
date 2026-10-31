"""Read-only, content-free progress for this exact frozen native runtime."""
from pathlib import Path
import datetime
import json
import re
import subprocess

OUT = Path(__file__).resolve().parent
runtime = '/tmp/ext-protection-loop10-source-gaps-7b5a4e938648/runtime/Hexalith.Platform.Protection.Tests.dll'
observed = {'observedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'testProcesses': []}
for process in Path('/proc').iterdir():
    if not process.name.isdigit():
        continue
    try:
        arguments = (process / 'cmdline').read_bytes().split(b'\0')
    except (FileNotFoundError, PermissionError, ProcessLookupError):
        continue
    if len(arguments) < 2 or arguments[0] != b'dotnet' or arguments[1].decode(errors='replace') != runtime:
        continue
    elapsed = subprocess.run(['ps', '-p', process.name, '-o', 'etime='], capture_output=True, text=True, timeout=5).stdout.strip()
    queue = [process]
    seen = set()
    containers = set()
    while queue:
        child = queue.pop()
        if child.name in seen:
            continue
        seen.add(child.name)
        try:
            for task in (child / 'task').iterdir():
                queue.extend(Path('/proc') / value for value in (task / 'children').read_text().split())
            command = (child / 'cmdline').read_bytes().split(b'\0')
        except (FileNotFoundError, PermissionError, ProcessLookupError):
            continue
        if command[0].endswith(b'docker') and b'exec' in command and b'-i' in command:
            container = command[command.index(b'-i') + 1].decode()
            if re.fullmatch('[0-9a-f]{64}', container):
                containers.add(container)
    states = []
    for container in sorted(containers):
        inspected = subprocess.run(['docker', '--host', 'unix:///var/run/docker.sock', 'inspect', container], capture_output=True, text=True, timeout=10)
        if inspected.returncode:
            continue
        value = json.loads(inspected.stdout)[0]
        nonce = value['Config']['Labels'].get('hexalith.ext-protection-1.raft-probe', '')
        if value['Id'] != container or not re.fullmatch('[0-9a-f]{32}', nonce) \
                or value['Name'] != '/ext-protection-1-raft-probe-' + nonce or value['HostConfig']['NetworkMode'] != 'none':
            raise SystemExit('Owned native progress identity mismatch')
        states.append({'containerId': container, **{key: value['State'][key] for key in ('Running', 'OOMKilled', 'ExitCode')}})
    progress = {'pid': int(process.name), 'elapsed': elapsed, 'ownedContainerStates': states}
    if b'-result-xml' in arguments:
        directory = Path(arguments[arguments.index(b'-result-xml') + 1].decode()).parent
        if directory.is_absolute() and str(directory).startswith('/tmp/erc-native-diagnostic-') and not directory.is_symlink():
            progress.update(runtimePolicyRecords=len(list(directory.glob('policy-*.json'))),
                completedFixtureDisposals=len(list(directory.glob('fixture-*.json'))))
    observed['testProcesses'].append(progress)
name = 'native-progress-' + datetime.datetime.now(datetime.timezone.utc).strftime('%H%M%S') + '.json'
with (OUT / name).open('x') as output:
    json.dump(observed, output, indent=2)
    output.write('\n')
print(json.dumps(observed))
