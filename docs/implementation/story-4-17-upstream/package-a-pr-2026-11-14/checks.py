"""Run serialized checks on the held PR checkout with verified disposable tools.

Invoke with run-with-temp after workspace.py prints WORKSPACE_READY. Downloads
Go 1.26.10 (checked against the go.dev index SHA-256) and golangci-lint v2.10.1
(checked against its release checksums file) into TMPDIR, with all caches
there. Accepts JSON {"argv": [...], "log": "name"} lines on stdin; argv[0] of
go, gofmt or golangci-lint resolves to the verified tool. Send 'cleanup' to exit.
Logs and receipts are written next to this script; logs over 1 MiB are stored
as deterministic gzip with both hashes in the receipt.
"""
import gzip
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

GO_VERSION = 'go1.26.10'
LINT_VERSION = '2.10.1'
LINT_RELEASE = 'https://github.com/golangci/golangci-lint/releases/download/v' + LINT_VERSION + '/'
LINT_ARCHIVE = 'golangci-lint-' + LINT_VERSION + '-linux-amd64.tar.gz'
LINT_CHECKSUMS = 'golangci-lint-' + LINT_VERSION + '-checksums.txt'
COMPRESS_OVER = 1024 * 1024

out = Path(__file__).resolve().parent
scratch = Path(os.environ['TMPDIR'])
source = Path((out / 'workspace-path.txt').read_text().strip())


def sha(data):
    return hashlib.sha256(data).hexdigest()


def download(url, path):
    with urllib.request.urlopen(url) as response, path.open('wb') as stream:
        shutil.copyfileobj(response, stream)
    return path.read_bytes()


def now():
    return datetime.now(timezone.utc).isoformat()


index_url = 'https://go.dev/dl/?mode=json&include=all'
index = json.load(urllib.request.urlopen(index_url))
release = next(r for r in index if r['version'] == GO_VERSION)
archive = next(f for f in release['files'] if f['os'] == 'linux' and f['arch'] == 'amd64' and f['kind'] == 'archive')
go_url = 'https://go.dev/dl/' + archive['filename']
go_digest = sha(download(go_url, scratch / archive['filename']))
if go_digest != archive['sha256']:
    raise RuntimeError('Go toolchain checksum mismatch')
with tarfile.open(scratch / archive['filename']) as package:
    package.extractall(scratch, filter='data')

checksums = download(LINT_RELEASE + LINT_CHECKSUMS, scratch / LINT_CHECKSUMS).decode()
expected = {name: digest for digest, name in (line.split() for line in checksums.splitlines() if line.strip())}
lint_digest = sha(download(LINT_RELEASE + LINT_ARCHIVE, scratch / LINT_ARCHIVE))
if lint_digest != expected[LINT_ARCHIVE]:
    raise RuntimeError('golangci-lint checksum mismatch')
with tarfile.open(scratch / LINT_ARCHIVE) as package:
    package.extractall(scratch, filter='data')

tools = {
    'go': str(scratch / 'go' / 'bin' / 'go'),
    'gofmt': str(scratch / 'go' / 'bin' / 'gofmt'),
    'golangci-lint': str(scratch / LINT_ARCHIVE.removesuffix('.tar.gz') / 'golangci-lint'),
}
env = dict(os.environ, GOCACHE=str(scratch / 'go-cache'), GOMODCACHE=str(scratch / 'go-mod'), GOPATH=str(scratch / 'gopath'),
           GOLANGCI_LINT_CACHE=str(scratch / 'golangci-cache'), GOTOOLCHAIN='local', GOMAXPROCS='4', NO_COLOR='1', TERM='dumb')
env['PATH'] = str(scratch / 'go' / 'bin') + os.pathsep + env['PATH']
record = {
    'go': {'indexUrl': index_url, 'archiveUrl': go_url, 'archive': archive, 'verifiedSha256': go_digest,
           'version': subprocess.check_output([tools['go'], 'version'], env=env, text=True).strip()},
    'golangciLint': {'archiveUrl': LINT_RELEASE + LINT_ARCHIVE, 'checksumsUrl': LINT_RELEASE + LINT_CHECKSUMS,
                     'checksumsSha256': sha(checksums.encode()), 'expectedSha256': expected[LINT_ARCHIVE],
                     'verifiedSha256': lint_digest,
                     'version': subprocess.check_output([tools['golangci-lint'], 'version'], env=env, text=True).strip()},
    'environment': {key: env[key] for key in ('GOTOOLCHAIN', 'GOMAXPROCS')},
}
(out / 'toolchain-identity.json').write_text(json.dumps(record, indent=2) + '\n')
(out / 'checks-environment.json').write_text(json.dumps({'source': str(source), 'scratch': str(scratch), **tools}, indent=2) + '\n')
print('CHECKS_READY ' + str(scratch), flush=True)


def subject():
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=source, text=True).strip()
    status = subprocess.check_output(['git', 'status', '--porcelain', '--untracked-files=all'], cwd=source, text=True)
    return head, status


for line in sys.stdin:
    if line.strip() == 'cleanup':
        break
    job = json.loads(line)
    source = Path((out / 'workspace-path.txt').read_text().strip())
    argv = list(job['argv'])
    name = job['log']
    if Path(name).name != name:
        raise ValueError('log must be a basename')
    resolved = [tools.get(argv[0], argv[0]), *argv[1:]]
    head_before, status_before = subject()
    log_path = out / (name + '.log')
    started = now()
    with log_path.open('wb') as log:
        result = subprocess.run(resolved, cwd=source, env=env, stdout=log, stderr=subprocess.STDOUT)
    ended = now()
    head_after, status_after = subject()
    data = log_path.read_bytes()
    receipt = {'argv': argv, 'resolvedArgv0': resolved[0], 'cwdSubject': 'official main plus the Package A commit',
               'subjectHead': head_before, 'subjectHeadAfter': head_after, 'cleanTreeBefore': status_before == '',
               'cleanTreeAfter': status_after == '', 'startedUtc': started, 'endedUtc': ended,
               'exitCode': result.returncode, 'log': log_path.name, 'logBytes': len(data), 'logSha256': sha(data)}
    if len(data) > COMPRESS_OVER:
        stored = out / (log_path.name + '.gz')
        with stored.open('wb') as raw, gzip.GzipFile(filename='', mode='wb', fileobj=raw, mtime=0) as packed:
            packed.write(data)
        log_path.unlink()
        receipt.update({'log': stored.name, 'originalLog': log_path.name, 'logEncoding': 'gzip',
                        'uncompressedLogSha256': receipt.pop('logSha256'), 'storedLogSha256': sha(stored.read_bytes())})
    (out / (name + '.receipt.json')).write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps(receipt), flush=True)
print('CHECKS_RELEASED', flush=True)
