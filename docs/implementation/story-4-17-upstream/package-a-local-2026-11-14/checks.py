"""Run serialized component checks with a verified disposable Go toolchain.

Invoke with run-with-temp; accepts JSON {argv: [...], log: 'name'} on stdin.
An argv[0] of 'go' or 'gofmt' resolves to the verified toolchain. Send 'cleanup'
to exit. All test logs/receipts survive outside TMPDIR.
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import urllib.request
from datetime import datetime, timezone

out = Path(__file__).resolve().parent
scratch = Path(os.environ['TMPDIR'])
source = Path((out / 'workspace-path.txt').read_text().strip())
index_url = 'https://go.dev/dl/?mode=json&include=all'
index = json.load(urllib.request.urlopen(index_url))
release = next(r for r in index if r['version'] == 'go1.26.10')
archive = next(f for f in release['files'] if f['os'] == 'linux' and f['arch'] == 'amd64' and f['kind'] == 'archive')
url = 'https://go.dev/dl/' + archive['filename']
path = scratch / archive['filename']
with urllib.request.urlopen(url) as response, path.open('wb') as stream:
    shutil.copyfileobj(response, stream)
digest = hashlib.sha256(path.read_bytes()).hexdigest()
if digest != archive['sha256']:
    raise RuntimeError('Go toolchain checksum mismatch')
with tarfile.open(path) as package:
    package.extractall(scratch, filter='data')
env = dict(os.environ, GOCACHE=str(scratch / 'go-cache'), GOMODCACHE=str(scratch / 'go-mod'), GOPATH=str(scratch / 'gopath'), GOTOOLCHAIN='local', GOMAXPROCS='4')
env['PATH'] = str(scratch / 'go' / 'bin') + os.pathsep + env['PATH']
record = {'indexUrl': index_url, 'archiveUrl': url, 'archive': archive, 'verifiedSha256': digest, 'version': subprocess.check_output(['go', 'version'], env=env, text=True).strip()}
(out / 'toolchain-identity.json').write_text(json.dumps(record, indent=2) + '\n')
(out / 'checks-environment.json').write_text(json.dumps({'source': str(source), 'scratch': str(scratch), 'go': str(scratch / 'go' / 'bin' / 'go')}, indent=2)+'\n')
print('CHECKS_READY ' + str(scratch), flush=True)
for line in sys.stdin:
    if line.strip() == 'cleanup':
        break
    job = json.loads(line)
    argv = job['argv']
    name = job['log']
    if Path(name).name != name:
        raise ValueError('log must be a basename')
    started = datetime.now(timezone.utc).isoformat()
    with (out / (name + '.log')).open('w') as log:
        result = subprocess.run(argv, cwd=source, env=env, stdout=log, stderr=subprocess.STDOUT)
    receipt = {'argv': argv, 'cwdSubject': 'official checkout plus local patch', 'startedUtc': started, 'endedUtc': datetime.now(timezone.utc).isoformat(), 'exitCode': result.returncode, 'log': name + '.log'}
    (out / (name + '.receipt.json')).write_text(json.dumps(receipt, indent=2)+'\n')
    print(json.dumps(receipt), flush=True)
print('CHECKS_RELEASED', flush=True)
