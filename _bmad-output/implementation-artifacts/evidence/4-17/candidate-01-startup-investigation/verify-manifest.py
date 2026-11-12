"""Verify every size and SHA-256 commitment in an evidence file-manifest.json.

Usage: python3 -I verify-manifest.py PATH/TO/file-manifest.json
Paths in the manifest are relative to the manifest's directory. Exit 0 only when
every listed file exists with the committed byte count and digest.
"""
import hashlib
import json
import sys
from pathlib import Path

manifest = Path(sys.argv[1])
base = manifest.parent
rows = json.loads(manifest.read_text())['files']
verified, total, failures = 0, 0, []
for row in rows:
    path = base / row['path']
    data = path.read_bytes() if path.is_file() else None
    if data is not None and len(data) == row['bytes'] and hashlib.sha256(data).hexdigest() == row['sha256']:
        verified += 1
        total += len(data)
    else:
        failures.append(row['path'])
print(json.dumps({'manifest': str(manifest), 'commitments': len(rows), 'verified': verified,
                  'bytes': total, 'failures': failures}))
raise SystemExit(0 if not failures else 1)
