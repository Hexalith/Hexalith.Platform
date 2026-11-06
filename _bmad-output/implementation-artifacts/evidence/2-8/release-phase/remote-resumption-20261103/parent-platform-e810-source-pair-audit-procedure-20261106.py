"""Read-only independent source-pair audit; emits receipts outside the implementation root."""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess

ROOT = Path('/home/administrator/.cache/hexalith/story-2-8/platform-scoped-gateway-4448-source-union-20261106/Hexalith.Platform')
CACHE = Path('/home/administrator/.cache/hexalith/story-2-8/parent-platform-e810-hosted-20261106')
EVIDENCE = Path(__file__).resolve().parent
Q = '8c2d3eea7abc21f041d8b61c9fe16026f25104be'
L = 'b65de93fc57b4e04ac28e02a5df59be388bdac73'
M = 'e810be0356da55b6db1794262c722e358470a093'
INCOMING = '13fc1a8978b4e6dcf8233b443c23c4e4258c49c7'
PREVIOUS = 'ec282f4338c4b6d900e32b827821fc97a272fe76'
META = {'.config/dotnet-tools.json', 'eng/builds-tuple.json', 'eng/legacy-tuple-source.json',
        'eng/tuple-ci-suites.json', 'hexalith-platform.json', 'references/Hexalith.Builds',
        'references/Hexalith.Parties', 'src/Hexalith.Platform.Composition/Declarations/provenance.json'}


def git(*args):
    return subprocess.check_output(['git', '--git-dir', str(CACHE / 'source.git'), *args])


def blob(source, path):
    return git('show', source + ':' + path)


def objects(source):
    return {row.split(b'\t', 1)[1].decode(): row.split(b'\t', 1)[0].decode()
            for row in git('ls-tree', '-rz', source).split(b'\0') if row}


maps = [objects(source) for source in (Q, L, M)]
assert set(maps[0]) == set(maps[1]) == set(maps[2])
for left, right in [(maps[0], maps[1]), (maps[1], maps[2]), (maps[0], maps[2])]:
    assert {path for path in left if left[path] != right[path]} <= META
    assert all(left[path] == right[path] for path in left if path not in META)
assert {path for path in maps[1] if maps[1][path] != maps[2][path]} == META
assert len(maps[2]) - len(META) == 21962
assert git('show', '-s', '--format=%P', Q).decode().strip().split() == [PREVIOUS, INCOMING]
assert git('show', '-s', '--format=%P', L).decode().strip() == Q
assert git('show', '-s', '--format=%P', M).decode().strip() == L
for source in [PREVIOUS, INCOMING, L]:
    subprocess.run(['git', '--git-dir', str(CACHE / 'source.git'), 'merge-base', '--is-ancestor', source, M], check=True)
assert blob(M, 'eng/tuple-ci-suites.json') == blob(PREVIOUS, 'eng/tuple-ci-suites.json')
assert sum(group['requiredCount'] for group in json.loads(blob(M, 'eng/tuple-ci-suites.json'))['groups']) == 271
assert hashlib.sha256(blob(L, 'eng/tuple-ci-suites.json')).hexdigest() == '61689f1598dac43a9fa28dcf5319008059e0fea0f1018209b1a8e79e4f63d7b0'
assert sum(group['requiredCount'] for group in json.loads(blob(L, 'eng/tuple-ci-suites.json'))['groups']) == 349
assert json.loads(blob(M, 'eng/legacy-tuple-source.json'))['sourceRevision'] == L
binding = json.loads((EVIDENCE / 'parent-builds4448-actual-qualified-producer-and-tuple-binding-20261106.json').read_text())
assert json.loads(blob(M, 'eng/builds-tuple.json')) == binding['binding']
assert maps[2]['references/Hexalith.Builds'].endswith(binding['binding']['producer']['sourceRevision'])
incoming_paths = json.loads((ROOT.parent / 'evidence/complete-proposal-and-source-preservation.json').read_text())['incomingGatewayAndTestObjectsByteExact']
incoming_map = objects(INCOMING)
assert len(incoming_paths) == 9
for path in incoming_paths:
    assert maps[2][path] == incoming_map[path], path
retained_paths = json.loads((ROOT.parent / 'evidence/sealed-source-and-exact-pair-static-preservation.json').read_text())['allReviewedSourceProofSealHandoffSeedDeadlineObjectsByteExactToClosedEc282']
previous_map = objects(PREVIOUS)
assert len(retained_paths) == 9
for path in retained_paths:
    assert maps[2][path] == previous_map[path], path
patch = git('diff', '--binary', '--full-index', INCOMING, M, '--')
assert patch == Path('/tmp/story28-platform-e810-complete-ou7frbi5.diff').read_bytes()
assert len(patch) == 82645 and len(patch.splitlines()) == 1161
assert hashlib.sha256(patch).hexdigest() == '1ac987f0381fdd7abd509c8847546fc6a9491fa663b1b6b9c9c2963975d3e059'
assert json.loads((ROOT / 'node_modules/@commitlint/cli/package.json').read_text())['version'] == '21.2.4'
commits = git('rev-list', INCOMING + '..' + M).decode().splitlines()
assert len(commits) == 26
messages = []
for source in commits:
    message = git('show', '-s', '--format=%B', source)
    (CACHE / ('full-message-' + source + '.txt')).write_bytes(message)
    result = subprocess.run(['node', 'node_modules/@commitlint/cli/cli.js'], input=message, cwd=ROOT, capture_output=True)
    (CACHE / ('full-message-' + source + '.lint.txt')).write_bytes(result.stdout + result.stderr)
    assert result.returncode == 0, (source, result.stderr)
    messages.append({'commit': source, 'messageSha256': hashlib.sha256(message).hexdigest(), 'exitCode': 0})
assert subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT) == b''
assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT).decode().strip() == M
heads = []
uninitialized = []
for path, value in maps[2].items():
    if not value.startswith('160000 commit '):
        continue
    if not (ROOT / path / '.git').exists():
        uninitialized.append(path)
        continue
    actual = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT / path).decode().strip()
    assert actual == value.split()[-1], (path, actual, value)
    assert subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT / path) == b'', path
    heads.append({'path': path, 'head': actual})
assert len(heads) == 6 and len(uninitialized) == 11
out = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'source': Q, 'legacy': L, 'current': M,
       'sharedMergeOrderedParents': [PREVIOUS, INCOMING],
       'trees': {source: git('rev-parse', source + '^{tree}').decode().strip() for source in [Q, L, M]},
       'originalEightMetadataDifferences': sorted(META), 'nonmetadataObjectModesIdentical': 21962,
       'incomingGatewayAndTestObjectsByteExact': incoming_paths, 'reviewedSealedSourceObjectsByteExact': retained_paths,
       'current271ProfileByteExact': True, 'original349ProfileByteExact': True,
       'public4448BindingMatchesParentAuthenticatedSignedProducer': True,
       'completeProposal': {'path': '/tmp/story28-platform-e810-complete-ou7frbi5.diff', 'bytes': len(patch),
                            'lines': len(patch.splitlines()), 'sha256': hashlib.sha256(patch).hexdigest(), 'all1161LinesReadByParent': True},
       'allOutgoingFullMessages': messages, 'sourceClean': True, 'sixDirectHeads': heads,
       'otherRootDeclaredDirectsRemainUninitialized': uninitialized, 'candidateAcceptance': False, 'storyAcceptance': False}
(CACHE / 'complete-source-pair-audit.json').write_text(json.dumps(out, indent=2) + '\n')
(EVIDENCE / 'parent-platform-e810-complete-proposal-real-pair-and-full-messages-audit-20261106.json').write_text(json.dumps(out, indent=2) + '\n')
print(json.dumps({'passed': True, 'nonmetadataObjects': 21962, 'messages': len(messages), 'directHeads': len(heads), 'proposalLines': 1161}))
