#!/usr/bin/env python3
"""Read-only verification of this packet's explicitly recorded preservation scope.

Run from repository root with /usr/bin/python3.14 -I -S -B and this file's path.
No recovery modules, credentials, configuration or historical tools are loaded.
"""
import ast
import hashlib
import json
from pathlib import Path
import subprocess


ROOT = Path.cwd()
PACKET = Path(__file__).absolute().parent
INITIAL = PACKET.parent
BASELINE = '693390a790d95a9bad9b1bd9cc0f0f3052f3fbef'


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def git(*arguments):
    return subprocess.check_output(('git', *arguments), cwd=ROOT,
        stderr=subprocess.DEVNULL)


def tests(path):
    tree = ast.parse(path.read_text())
    return {cls.name + '.' + test.name for cls in tree.body
        if isinstance(cls, ast.ClassDef) for test in cls.body
        if isinstance(test, ast.FunctionDef) and test.name.startswith('test_')}


initial = json.loads((PACKET / 'initial-packet-sha256.json').read_text())
assert all(digest((INITIAL / name).read_bytes()) == expected
    for name, expected in initial.items())
prefixes = json.loads((PACKET / 'handoff-prefixes.json').read_text())
for name, prefix in prefixes.items():
    raw = (ROOT / name).read_bytes()
    assert digest(raw[:prefix['size']]) == prefix['sha256']
    assert raw.startswith(git('show', BASELINE + ':' + name))

spec_name = '_bmad-output/implementation-artifacts/spec-4-27-native-client-certificate-delivery.md'
spec = (ROOT / spec_name).read_bytes()


def frozen(raw):
    return raw[raw.index(b'<frozen-after-approval'):raw.index(b'</frozen-after-approval>')]


assert frozen(spec) == frozen((INITIAL / 'spec-baseline.md').read_bytes())
assert frozen(spec) == frozen((PACKET / 'spec-dispatch-baseline.md').read_bytes())
assert spec == (PACKET / 'spec-verification-checkpoint.md').read_bytes()
initial_receipt = json.loads((INITIAL / 'verification.json').read_text())
checkpoint = digest((INITIAL / 'spec-implementation-checkpoint.md').read_bytes())
assert checkpoint == initial_receipt['handoffSha256'][spec_name]
assert digest((INITIAL / 'spec-baseline.md').read_bytes()) == initial_receipt['specBaselineSha256']
assert git('rev-parse', 'HEAD').decode().strip() == BASELINE

changed = ('recovery_execution_boundary.py', '_execution_boundary_fixtures.py',
    'test_native_client_certificate.py', 'test_recovery_execution_boundary.py')
for name in changed:
    assert (PACKET / 'sources' / name).read_bytes() == (
        ROOT / 'eng/recovery-preparation' / name).read_bytes()
unchanged = []
for name in initial_receipt['sourceSha256']:
    if Path(name).name not in changed:
        assert (ROOT / name).read_bytes() == git('show', BASELINE + ':' + name)
        unchanged.append(name)

test_counts = {}
for name in ('test_native_client_certificate.py', 'test_recovery_execution_boundary.py'):
    old = tests(INITIAL / 'sources' / name)
    current = tests(ROOT / 'eng/recovery-preparation' / name)
    assert old <= current
    test_counts[name] = {'initial': len(old), 'current': len(current),
        'allOriginalTestNamesRetained': True}

evidence_path = '_bmad-output/implementation-artifacts/evidence'
assert not git('diff', '--name-only', BASELINE, '--', evidence_path).strip()
assert not git('diff', '--cached', '--name-only', BASELINE).strip()

report = {
    'baselineCommit': BASELINE,
    'initialPacketFilesChecked': len(initial),
    'allInitialPacketBytesUnchanged': True,
    'initialSpecReceiptHashReproduced': checkpoint,
    'frozenIntentUnchanged': True,
    'dispatchHandoffPrefixesUnchanged': prefixes,
    'parentBaselineHandoffPrefixesUnchanged': True,
    'correctedSourceSnapshotsEqualWorkingSources': list(changed),
    'originalTestNames': test_counts,
    'otherInventoriedSourcesEqualParentBaseline': unchanged,
    'trackedHistoricalEvidenceTree': git('rev-parse', BASELINE + ':' + evidence_path).decode().strip(),
    'trackedHistoricalEvidenceUnchanged': True,
    'indexHasNoChangesAgainstParentBaseline': True,
    'unrelatedWorkspaceEdits': 'outside checked scope; no preservation claim inferred',
}
print(json.dumps(report, indent=2, sort_keys=True))
