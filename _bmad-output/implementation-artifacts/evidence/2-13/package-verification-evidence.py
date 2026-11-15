"""Preserve bulk verification evidence in an indexed, verified archive."""
from pathlib import Path
import csv, hashlib, json, tarfile

root = Path(__file__).resolve().parent
keep = {
    'README.md', 'coverage-map.md', 'before-inventory.json', 'after-inventory.json',
    'selector-count-mapping.tsv', 'continuing-group-observations.tsv', 'canonical-revisions.json', 'verification-summary.json',
    'published-artifact-summary.json', 'non-xunit-execution-counts.json', 'python-verified-summary.json',
    'continuing-inventories-summary.json', 'transport-correction-summary.json', 'matrix-correction-summary.json',
    'isolated-merge-summary.json', 'delivery-source-summary.json',
    'verify-transport-correction.py', 'verify-matrix-correction.py', 'assembly-method-fingerprint.cs', 'prepare-candidate.py',
    'sync-candidate.py', 'execute-final-inventories.py', 'run-python-controls.py',
    'run-delivery-controls.py', 'verify-isolated-merge.py', 'package-verification-evidence.py',
}
assert all((root / p).is_file() for p in keep), sorted(p for p in keep if not (root / p).is_file())
archive = root / 'verification-artifacts.tar.gz'
assert not archive.exists()
paths = sorted(p for p in root.rglob('*') if p.is_file() and p.relative_to(root).as_posix() not in keep)
records = [{'path': p.relative_to(root).as_posix(), 'bytes': p.stat().st_size,
            'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in paths]
with tarfile.open(archive, 'w:gz', compresslevel=9, dereference=True) as bundle:
    for path in paths:
        bundle.add(path, arcname=path.relative_to(root).as_posix(), recursive=False)
with tarfile.open(archive, 'r:gz') as bundle:
    for record in records:
        body = bundle.extractfile(record['path']).read()
        assert len(body) == record['bytes'] and hashlib.sha256(body).hexdigest() == record['sha256']
index = root / 'archive-index.tsv'
with index.open('w') as stream:
    writer = csv.DictWriter(stream, ['path', 'bytes', 'sha256'], delimiter='\t', lineterminator='\n')
    writer.writeheader()
    writer.writerows(records)
receipt = {'archive': archive.name, 'archiveSha256': hashlib.sha256(archive.read_bytes()).hexdigest(),
           'archiveBytes': archive.stat().st_size, 'files': len(records), 'uncompressedBytes': sum(r['bytes'] for r in records),
           'index': index.name, 'indexSha256': hashlib.sha256(index.read_bytes()).hexdigest(),
           'allArchivedFileHashesVerified': True, 'diagnosticFailuresPreserved': True}
(root / 'archive-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
for path in paths:
    path.unlink()
for path in sorted((p for p in root.rglob('*') if p.is_dir()), key=lambda p: len(p.parts), reverse=True):
    if not any(path.iterdir()):
        path.rmdir()
print(json.dumps(receipt, indent=2))
