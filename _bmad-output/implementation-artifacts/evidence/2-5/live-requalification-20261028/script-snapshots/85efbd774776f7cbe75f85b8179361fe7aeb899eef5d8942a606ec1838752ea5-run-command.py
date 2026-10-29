"""Capture exact commands and retained log bytes for this qualification."""
from datetime import datetime, timezone
from pathlib import Path
import gzip
import hashlib
import json
import subprocess
import sys
import time

out = Path(__file__).resolve().parent
name, *argv = sys.argv[1:]
log = out / (name + '.log')
assert not log.exists(), f'Refusing to replace evidence: {log}'
started = datetime.now(timezone.utc).isoformat()
clock = time.monotonic()
scripts = []
for path in dict.fromkeys([Path(__file__).resolve(), *(Path(value).resolve() for value in argv if value.endswith('.py') and Path(value).is_file())]):
    data = path.read_bytes()
    identity = hashlib.sha256(data).hexdigest()
    snapshot = out / 'script-snapshots' / (identity + '-' + path.name)
    snapshot.parent.mkdir(exist_ok=True)
    snapshot.write_bytes(data)
    scripts.append({'executedPath': str(path), 'sha256': identity, 'snapshot': str(snapshot.relative_to(out))})
with log.open('wb') as stream:
    result = subprocess.run(argv, stdout=stream, stderr=subprocess.STDOUT)
data = log.read_bytes()
archive = out / (log.name + '.gz')
archive.write_bytes(gzip.compress(data, mtime=0))
record = {
    'cwd': str(Path.cwd()), 'argv': argv,
    'startedAtUtc': started,
    'finishedAtUtc': datetime.now(timezone.utc).isoformat(),
    'durationSeconds': round(time.monotonic() - clock, 3),
    'exitCode': result.returncode, 'log': log.name,
    'archivedLog': archive.name,
    'logSha256': hashlib.sha256(data).hexdigest(),
    'archiveSha256': hashlib.sha256(archive.read_bytes()).hexdigest(),
    'scriptSnapshots': scripts,
}
(out / (name + '.command.json')).write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps(record), flush=True)
sys.exit(result.returncode)
