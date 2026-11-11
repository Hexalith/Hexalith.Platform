# Historical pre-hosted evidence

`file-manifest.json` is preserved byte-for-byte from the pre-hosted snapshot.
Its paths retain their original evidence-root meaning. Resolve each entry against
this archive directory first; only when the entry is absent here, resolve it against
the parent `candidate-01-hosted/` directory. An archived entry with invalid bytes
must fail verification rather than fall back to a newer parent observation.

The five archived observations override their later parent versions. The remaining
47 entries are unchanged shared publication receipts in the parent directory.
The original manifest does not cover the historical report, whose relative links
were repaired after relocation, or this explanatory file. The current outer manifest
commits those files and the unchanged original manifest.

Repeat from the repository root; this uses only retained local bytes:

```bash
PYTHONOPTIMIZE=0 python3 -B - <<'VERIFY'
import hashlib
import json
from pathlib import Path

archive = Path("_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted/pre-hosted-status")
manifest = archive / "file-manifest.json"
assert hashlib.sha256(manifest.read_bytes()).hexdigest() == "51c9659fd6d682e9cfacdd2546f4de3e9b7d72ca8ec2c2b5cc1991bb927eaed8"
rows = json.loads(manifest.read_bytes())["files"]
counts = {"archive": 0, "parent": 0}
for row in rows:
    relative = Path(row["path"])
    assert not relative.is_absolute() and ".." not in relative.parts
    local = archive / relative
    path = local if local.exists() or local.is_symlink() else archive.parent / relative
    assert path.is_file() and not path.is_symlink(), row["path"]
    data = path.read_bytes()
    assert len(data) == row["bytes"], row["path"]
    assert hashlib.sha256(data).hexdigest() == row["sha256"], row["path"]
    counts["archive" if path == local else "parent"] += 1
assert len(rows) == 52 and counts == {"archive": 5, "parent": 47}
print("PASS: 52 original size/hash commitments; 5 archive entries, 47 parent receipts")
VERIFY
```
