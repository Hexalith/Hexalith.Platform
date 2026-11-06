"""Independently hash the complete closed checkpoint without changing its root."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path, PurePosixPath
import datetime
import hashlib
import json
import os
import subprocess

ROOT = Path('/home/administrator/.cache/hexalith/story-2-8/platform-scoped-gateway-4448-source-union-20261106')
EV = Path(__file__).resolve().parent
checkpoint_path = ROOT / 'evidence/scoped-gateway-4448-checkpoint.json'
checkpoint_body = checkpoint_path.read_bytes()
assert len(checkpoint_body) == 6971
assert hashlib.sha256(checkpoint_body).hexdigest() == '7a55b85076d26f3fb7bd8475c83a9006761b8d9e7652ae030738b7488574a6f6'
checkpoint = json.loads(checkpoint_body)
assert checkpoint['source'] == 'e810be0356da55b6db1794262c722e358470a093'
assert checkpoint['legacy'] == 'b65de93fc57b4e04ac28e02a5df59be388bdac73'
assert checkpoint['priorIndexedFilesRehashed'] == 601587
assert checkpoint['priorIndexedBytesRehashed'] == 29343454549
assert checkpoint['priorDeclaredSymlinkTargetsVerified'] == 103


def path_for(relative):
    name = PurePosixPath(relative)
    assert not name.is_absolute() and '..' not in name.parts and '\\' not in relative
    return ROOT / relative


def check_anchor(anchor):
    body = path_for(anchor['path']).read_bytes()
    assert len(body) == anchor['bytes'], anchor['path']
    assert hashlib.sha256(body).hexdigest() == anchor['sha256'], anchor['path']


check_anchor(checkpoint['index'])
index = json.loads(path_for(checkpoint['index']['path']).read_bytes())
assert index['root'] == str(ROOT)
assert index['fileCount'] == len(index['files']) == checkpoint['indexedFiles'] == 79324
assert index['totalBytes'] == checkpoint['indexedBytes'] == 3921425603
assert len(index['symlinks']) == checkpoint['declaredSymlinksVerified'] == 6
links = {row['path']: row['target'] for row in index['symlinks']}
assert len(links) == 6
for name, target in links.items():
    path = path_for(name)
    assert path.is_symlink() and os.readlink(path) == target, name
    assert path.exists(), name


def verify(row):
    path = path_for(row['path'])
    if path.is_symlink():
        assert row['path'] in links and os.readlink(path) == links[row['path']], row['path']
    assert path.is_file(), row['path']
    digest = hashlib.sha256()
    size = 0
    with path.open('rb') as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b''):
            digest.update(chunk)
            size += len(chunk)
    assert size == row['bytes'] and digest.hexdigest() == row['sha256'], row['path']
    return size


with ThreadPoolExecutor(max_workers=12) as pool:
    total = sum(pool.map(verify, index['files']))
assert total == index['totalBytes']
names = {row['path'] for row in index['files']}
assert len(names) == len(index['files'])
actual = set()
excluded = {checkpoint_path.relative_to(ROOT).as_posix(), checkpoint['index']['path']}
for parent, directories, files in os.walk(ROOT, followlinks=False):
    directories[:] = [name for name in directories if name != '.git']
    for name in files:
        if name == '.git':
            continue
        relative = (Path(parent) / name).relative_to(ROOT).as_posix()
        if relative not in excluded:
            actual.add(relative)
assert actual == names, {'unindexed': sorted(actual - names), 'missing': sorted(names - actual)}
for anchor in checkpoint['anchors']:
    check_anchor(anchor)
assert checkpoint['clean'] and checkpoint['allIndexedFilesRehashedAfterIndexWrite']
for key in ['candidateAcceptance', 'qualificationCredit', 'nativeAcceptance', 'storyAcceptance']:
    assert checkpoint[key] is False
source_root = ROOT / 'Hexalith.Platform'
assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=source_root).decode().strip() == checkpoint['source']
assert subprocess.check_output(['git', 'status', '--porcelain'], cwd=source_root) == b''
assert checkpoint_path.read_bytes() == checkpoint_body
receipt = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
           'checkpointSha256': hashlib.sha256(checkpoint_body).hexdigest(),
           'source': checkpoint['source'], 'legacy': checkpoint['legacy'],
           'allIndexedFilesIndependentlyRehashed': len(names), 'allIndexedBytes': total,
           'declaredSymlinksVerified': len(links), 'nestedAnchorsVerified': len(checkpoint['anchors']),
           'actualNonGitFileCensusEqualsCompleteIndex': True,
           'allAggregateClosedIndexedFiles': 601587 + len(names),
           'allAggregateClosedIndexedBytes': 29343454549 + total,
           'allAggregateDeclaredLinks': 103 + len(links), 'cleanSource': True,
           'nativeAcceptance': False, 'candidateAcceptance': False, 'storyAcceptance': False}
output = EV / 'parent-platform-scoped-gateway-closed-checkpoint-independent-audit-20261106.json'
assert not output.exists()
output.write_text(json.dumps(receipt, indent=2) + '\n')
print(json.dumps(receipt))
