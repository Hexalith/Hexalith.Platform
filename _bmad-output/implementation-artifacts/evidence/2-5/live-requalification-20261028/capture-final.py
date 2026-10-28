"""Retain exact final runtime snapshots and durable empty ownership proofs."""
from pathlib import Path
import hashlib
import json
import shutil

out = Path(__file__).resolve().parent
root = out.parents[4]
live = out / 'live-final'
manifest = {}
owners = {json.loads((live / 'peer-before.json').read_text())['RunId']}
states = set()
for scenario in ['success', 'failure', 'timeout', 'partial-startup']:
    command = json.loads((live / ('command-' + scenario + '.json')).read_text())
    owners.add(command['runId'])
    original = Path(command['testResultPath']).parent
    states.add(original.parents[1])
    target = live / 'final-diagnostics' / scenario
    target.mkdir(parents=True)
    hashes = {}
    for path in original.iterdir():
        if path.is_file() and path.suffix in ['.json', '.trx']:
            content = path.read_bytes()
            (target / path.name).write_bytes(content)
            hashes[path.name] = hashlib.sha256(content).hexdigest()
    manifest[scenario] = {'originalDirectory': str(original), 'filesSha256': hashes}
assert len(states) == 1 and len(owners) == 5
state = next(iter(states))
receipts = {}
for owner in sorted(owners):
    path = state / 'claims' / 'owners' / (owner + '.json')
    content = path.read_bytes()
    assert json.loads(content) == [], owner
    target = out / 'claim-receipts' / path.name
    target.parent.mkdir(exist_ok=True)
    target.write_bytes(content)
    receipts[owner] = {'identities': [], 'sha256': hashlib.sha256(content).hexdigest(), 'snapshot': str(target.relative_to(out))}
(out / 'claim-receipts.json').write_text(json.dumps(receipts, indent=2) + '\n')
(out / 'final-runtime-archive.json').write_text(json.dumps(manifest, indent=2) + '\n')
before = json.loads((out / 'source-before-final.json').read_text())
after = {path: hashlib.sha256((root / path).read_bytes()).hexdigest() for path in before}
assert before == after
(out / 'source-after-final.json').write_text(json.dumps(after, indent=2) + '\n')
assemblies = json.loads((out / 'executed-assemblies.json').read_text())['sha256']
actual = {path: hashlib.sha256((root / path).read_bytes()).hexdigest() for path in assemblies}
assert actual == assemblies
(out / 'executed-assemblies-after.json').write_text(json.dumps(actual, indent=2) + '\n')
print(json.dumps({'finalSnapshotsRetained': len(manifest), 'durableEmptyOwnerReceipts': len(receipts), 'sourceFilesUnchanged': len(after), 'executedAssembliesUnchanged': True}))
