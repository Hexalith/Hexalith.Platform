"""Maintain EventStore's 1.19.1 trusted-input archive and the explicit Platform lock inventory.

Usage (EVENTSTORE_ROOT is a checkout; nothing is executed from it):
    python3 -I refresh_qualification_inventory.py EVENTSTORE_ROOT archive PATH=REVISION [PATH=REVISION ...]
    python3 -I refresh_qualification_inventory.py EVENTSTORE_ROOT refresh PATH [PATH ...]

archive: copy the exact historical bytes of PATH at REVISION (git show) into
         deploy/dapr/qualification/public-identities/1.19.1-original-inputs/PATH, record them in its
         archive.json, and require they equal the historical runtime-lock-1.19.1.json commitment.
refresh: set the Platform lock's commitment for each PATH (and every archive file, including
         archive.json) to the current bytes. Both JSON files keep their sorted indent-2 format.
"""
import hashlib
import json
import subprocess
import sys
from pathlib import Path

root, mode, items = Path(sys.argv[1]).resolve(), sys.argv[2], sys.argv[3:]
ARCHIVE = 'deploy/dapr/qualification/public-identities/1.19.1-original-inputs'
HISTORICAL = 'deploy/dapr/qualification/runtime-lock-1.19.1.json'
PLATFORM = 'deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def dump(path, value):
    (root / path).write_text(json.dumps(value, indent=2) + '\n')


if mode == 'archive':
    historical = json.loads((root / HISTORICAL).read_text())['inputs']
    archive = json.loads((root / ARCHIVE / 'archive.json').read_text())
    for item in items:
        name, revision = item.split('=', 1)
        revision = subprocess.run(['git', '-C', str(root), 'rev-parse', '--verify', revision + '^{commit}'],
                                  capture_output=True, text=True, check=True).stdout.strip()
        data = subprocess.run(['git', '-C', str(root), 'show', revision + ':' + name], capture_output=True, check=True).stdout
        if sha(data) != historical[name]:
            raise SystemExit(f'{name}@{revision} does not match the historical lock commitment')
        target = root / ARCHIVE / name
        if target.exists():
            raise SystemExit(f'refusing to overwrite archived {name}')
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        archive['files'][name] = {'sha256': sha(data), 'sourceRevision': revision}
        print(f'archived {name} {sha(data)} from {revision}')
    archive['files'] = dict(sorted(archive['files'].items()))
    dump(ARCHIVE + '/archive.json', archive)
elif mode == 'refresh':
    lock = json.loads((root / PLATFORM).read_text())
    names = set(items) | {str(p.relative_to(root)) for p in (root / ARCHIVE).rglob('*') if p.is_file()}
    for name in sorted(names):
        old = lock['inputs'].get(name)
        new = sha((root / name).read_bytes())
        lock['inputs'][name] = new
        if old != new:
            print(f'{name}: {old} -> {new}')
    lock['inputs'] = dict(sorted(lock['inputs'].items()))
    dump(PLATFORM, lock)
    print(f'{len(lock["inputs"])} inputs')
else:
    raise SystemExit('mode must be archive or refresh')
