"""Run the Package B local check set end to end; no remote writes.

Starts workspace.py and then checks.py, each under its own run-with-temp job
with held stdin. Sends every required-checks.jsonl job plus the supplementary
negative controls, releases both jobs, waits for them, records
workspace-lifecycle.json and finally runs summarize-checks.py. Exits with the
summary's status. Invoke from any directory: python3 -B run-checks.py
"""
from datetime import datetime, timezone
import json
from pathlib import Path
import subprocess
import sys

out = Path(__file__).resolve().parent


def now():
    return datetime.now(timezone.utc).isoformat()


def start(script):
    return subprocess.Popen(['run-with-temp', sys.executable, '-B', str(out / script)], cwd=out,
                            stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)


def await_line(process, prefix):
    for line in process.stdout:
        print(line, end='', flush=True)
        if line.startswith(prefix):
            return line
        if 'HALT' in line:
            break
    process.stdin.close()
    process.wait()
    raise SystemExit(prefix + ' not reached')


def release(process, released):
    process.stdin.write('cleanup\n')
    process.stdin.flush()
    process.stdin.close()
    await_line(process, released)
    return process.wait()


started = now()
workspace = start('workspace.py')
await_line(workspace, 'WORKSPACE_READY ')
source_job = Path((out / 'workspace-path.txt').read_text().strip()).parent
checks = start('checks.py')
await_line(checks, 'CHECKS_READY ')
tools_job = Path(json.loads((out / 'checks-environment.json').read_text())['scratch'])
jobs = [line for line in (out / 'required-checks.jsonl').read_text().splitlines() if line.strip()]
jobs.append(json.dumps({'log': 'negative-controls', 'argv': ['negative-controls']}))
for job in jobs:
    checks.stdin.write(job + '\n')
    checks.stdin.flush()
    receipt = json.loads(checks.stdout.readline())
    print(json.dumps({'log': receipt['log'], 'exitCode': receipt['exitCode']}), flush=True)
checks_exit = release(checks, 'CHECKS_RELEASED')
workspace_exit = release(workspace, 'WORKSPACE_RELEASED')
lifecycle = {
    'startedUtc': started,
    'completedUtc': now(),
    'jobs': [
        {'role': 'source', 'directory': str(source_job), 'wrapperExitCode': workspace_exit, 'directoryRemoved': not source_job.exists()},
        {'role': 'tools-and-caches', 'directory': str(tools_job), 'wrapperExitCode': checks_exit, 'directoryRemoved': not tools_job.exists()},
    ],
    'remoteWrites': [],
    'remoteReads': ['git ls-remote of refs/pull/4767/head and refs/heads/main', 'git fetch of the official base by SHA',
                    'go.dev and golangci-lint release downloads', 'Go module proxy downloads'],
}
lifecycle['status'] = 'PASS' if all(j['wrapperExitCode'] == 0 and j['directoryRemoved'] for j in lifecycle['jobs']) else 'FAIL'
(out / 'workspace-lifecycle.json').write_text(json.dumps(lifecycle, indent=2) + '\n')
print(json.dumps(lifecycle), flush=True)
summary = subprocess.run([sys.executable, '-B', str(out / 'summarize-checks.py')], cwd=out)
sys.exit(summary.returncode if lifecycle['status'] == 'PASS' else 1)
