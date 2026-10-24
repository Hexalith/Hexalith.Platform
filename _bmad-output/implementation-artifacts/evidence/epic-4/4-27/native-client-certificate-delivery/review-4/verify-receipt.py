#!/usr/bin/env python3
"""Check unsigned offline receipt content, independent of Git HEAD or index.

Run from the repository root with Python -I -S -B. --self-test first verifies
an isolated copy, then changes both a working source and its snapshot and proves
that the receipt's recorded content hash rejects their coherent drift. No
recovery module, actual credential/configuration, historical tool or socket is
loaded. This is reproducibility evidence, not a signature or operational grant.
"""
import argparse
import ast
import hashlib
import io
import json
import re
from pathlib import Path
import shutil
import tempfile
import unittest


ROOT = Path.cwd()
PACKET = Path(__file__).absolute().parent
SPEC = '_bmad-output/implementation-artifacts/spec-4-27-native-client-certificate-delivery.md'
SOURCES = ('recovery_execution_boundary.py', '_execution_boundary_fixtures.py',
    'test_native_client_certificate.py', 'test_recovery_execution_boundary.py')
REQUIRED_SUITES = {
    'native-client-certificate': 'test_native_client_certificate.py',
    'execution-boundary': 'test_recovery_execution_boundary.py',
    'immutable-readback': 'test_immutable_readback_driver.py',
    'policy-authentication': 'test_policy_authentication_driver.py',
    'immutable-s3-provider': 'test_immutable_s3_provider.py',
    'common-adapter': 'test_recovery_adapters.py',
    'telemetry-postgres': 'test_telemetry_postgres_adapter.py',
}



def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def check(value, label):
    if not value:
        raise AssertionError(label)


def test_names(path):
    tree = ast.parse(path.read_text())
    return {cls.name + '.' + test.name for cls in tree.body
        if isinstance(cls, ast.ClassDef) for test in cls.body
        if isinstance(test, ast.FunctionDef) and test.name.startswith('test_')}


def frozen(raw):
    end = b'</frozen-after-approval>'
    return raw[raw.index(b'<frozen-after-approval'):raw.index(end) + len(end)]


def unittest_summary(raw):
    text = raw.decode('utf-8')
    counts = re.findall(r'^Ran ([0-9]+) tests? in [0-9.]+s$', text, re.MULTILINE)
    outcomes = re.findall(r'^(OK|FAILED)(?: \(([^\n]*)\))?$', text, re.MULTILINE)
    check(len(counts) == len(outcomes) == 1, 'unique unittest summary')
    outcome, detail = outcomes[0]
    # Expected failures, unexpected successes, skips and failures are not a
    # zero-skip passing qualification. Accept only the plain OK summary.
    check(outcome == 'OK' and not detail, 'passing unittest summary')
    return {'tests': int(counts[0]), 'skips': 0, 'outcome': outcome}


def adverse_transcript(total, summary):
    """Actual unittest transcripts with the bound run count, isolated from it."""
    class Passing(unittest.TestCase):
        def runTest(self):
            pass
    def control(self):
        if summary in ('FAILED (failures=1)', 'OK (expected failures=1)'):
            self.fail('synthetic receipt-checker negative control')
    if summary == 'OK (skipped=1)':
        control = unittest.skip('synthetic receipt-checker negative control')(control)
    elif summary in ('OK (expected failures=1)', 'FAILED (unexpected successes=1)'):
        control = unittest.expectedFailure(control)
    failing = type('TranscriptControl', (unittest.TestCase,), {'runTest': control})
    output = io.StringIO()
    suite = unittest.TestSuite([failing()] + [Passing() for _ in range(total - 1)])
    result = unittest.TextTestRunner(stream=output, verbosity=2).run(suite)
    check(result.testsRun == total and output.getvalue().rstrip().endswith(summary), 'actual adverse transcript setup')
    return output.getvalue().encode('utf-8')


def verify(root, packet):
    receipt = json.loads((packet / 'verification.json').read_text())
    check(receipt['baselineCommit'] == '693390a790d95a9bad9b1bd9cc0f0f3052f3fbef', 'parent baseline')
    check(receipt['storyStatus'] == 'in-progress' and receipt['publicActivation'] == 'disabled' and
        receipt['operationalGate'] == 'closed' and receipt['operationalQualification'] is None and
        receipt['recoveryAcceptance'] is None, 'closed operational scope')
    for name, expected in receipt['sourceSha256'].items():
        check(digest((root / name).read_bytes()) == expected, 'sourceSha256:' + name)
    for name, expected in receipt['snapshotSha256'].items():
        raw = (packet / 'sources' / name).read_bytes()
        check(digest(raw) == expected, 'snapshotSha256:' + name)
        check(raw == (root / 'eng/recovery-preparation' / name).read_bytes(), 'source/snapshot equality:' + name)
    check(set(receipt['snapshotSha256']) == set(SOURCES), 'complete source snapshot inventory')
    for name, expected in receipt['packetFileSha256'].items():
        check(digest((packet / name).read_bytes()) == expected, 'packetFileSha256:' + name)
    for item in receipt['tests'] + receipt['verificationCommands']:
        check(digest((packet / item['log']).read_bytes()) == item['logSha256'], 'logSha256:' + item['log'])
        check(item['exitCode'] == 0, 'command exit:' + item['log'])
    check(len(receipt['tests']) == len(REQUIRED_SUITES) and
        {item['name'] for item in receipt['tests']} == set(REQUIRED_SUITES), 'required seven-suite inventory')
    for item in receipt['tests']:
        check(item['argv'] == ['/usr/bin/python3.14', '-I', '-S', '-B',
            'eng/recovery-preparation/' + REQUIRED_SUITES[item['name']]] and
            item['log'] == item['name'] + '.log', 'suite command:' + item['name'])
        try:
            actual = unittest_summary((packet / item['log']).read_bytes())
        except AssertionError:
            raise AssertionError('unittest summary:' + item['log']) from None
        check(actual == {key: item[key] for key in ('tests', 'skips', 'outcome')} and
            actual['outcome'] == 'OK', 'unittest summary:' + item['log'])
    check(json.loads((packet / 'command-results.json').read_text()) == receipt['tests'], 'command results match receipt')
    check(sum(item['tests'] for item in receipt['tests']) == receipt['testsPassed'] and
        sum(item['skips'] for item in receipt['tests']) == receipt['skips'] == 0, 'test totals')
    prior = json.loads((packet / 'prior-packet-sha256.json').read_text())
    for name, expected in prior.items():
        check(digest((packet.parent / name).read_bytes()) == expected, 'prior packet preservation:' + name)
    for name, prefix in json.loads((packet / 'handoff-prefixes.json').read_text()).items():
        check(digest((root / name).read_bytes()[:prefix['size']]) == prefix['sha256'], 'handoff prefix:' + name)
    for name, expected in receipt['handoffSha256'].items():
        raw = (packet / 'spec-verification-checkpoint.md').read_bytes() if name == SPEC else (root / name).read_bytes()
        check(digest(raw) == expected, 'handoffSha256:' + name)
    dispatch = (packet / 'spec-dispatch-baseline.md').read_bytes()
    checkpoint = (packet / 'spec-verification-checkpoint.md').read_bytes()
    spec = (root / SPEC).read_bytes()
    check(digest(dispatch) == receipt['specDispatchSha256'], 'spec dispatch content')
    check(digest(checkpoint) == receipt['specVerificationCheckpointSha256'], 'spec verification content')
    # Full checkpoints bind historical dispatch/verification bytes. Later
    # workflow status/triage edits need not equal that historical checkpoint;
    # the human-owned frozen intent must still match it exactly.
    check(frozen(spec) == frozen(checkpoint), 'working/checkpoint frozen equality')
    check(digest(frozen(spec)) == receipt['frozenSpecSha256'], 'frozen spec content')
    for candidate in (dispatch, (packet.parent / 'spec-baseline.md').read_bytes(),
            (packet.parent / 'review-1/spec-dispatch-baseline.md').read_bytes(),
            (packet.parent / 'review-2/spec-verification-checkpoint.md').read_bytes()):
        check(frozen(spec) == frozen(candidate), 'frozen intent checkpoint equality')
    original = json.loads((packet.parent / 'verification.json').read_text())
    check(digest((packet.parent / 'spec-implementation-checkpoint.md').read_bytes()) ==
        original['handoffSha256'][SPEC], 'historical spec receipt reproducibility')
    for name in ('test_native_client_certificate.py', 'test_recovery_execution_boundary.py'):
        check(test_names(packet.parent / 'review-3/sources' / name) <= test_names(root / 'eng/recovery-preparation' / name),
            'all review-3 test names retained:' + name)
    return receipt


def self_test(receipt):
    # Copy only repository-owned fixture/evidence content required by verify().
    # The mutation never touches working sources or historical packets.
    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        packet = root / PACKET.relative_to(ROOT)
        shutil.copytree(PACKET.parent, packet.parent)
        for name in set(receipt['sourceSha256']) | set(receipt['handoffSha256']):
            target = root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / name, target)
        verify(root, packet)
        name = 'recovery_execution_boundary.py'
        working = root / 'eng/recovery-preparation' / name
        snapshot = packet / 'sources' / name
        drift = working.read_bytes() + b'\n# synthetic coherent receipt-binding drift\n'
        working.write_bytes(drift)
        snapshot.write_bytes(drift)
        check(working.read_bytes() == snapshot.read_bytes(), 'self-test coherent drift setup')
        try:
            verify(root, packet)
        except AssertionError as exc:
            check(str(exc) == 'sourceSha256:eng/recovery-preparation/' + name, 'self-test rejection reason')
        else:
            raise AssertionError('coherent source/snapshot drift escaped receipt binding')

        # Restore the source, then alter duplicated metadata coherently. Log
        # bytes and their bound hashes remain exact; transcript parsing must
        # independently reject each otherwise internally consistent variant.
        working.write_bytes(drift[:drift.index(b'\n# synthetic coherent receipt-binding drift')])
        snapshot.write_bytes(working.read_bytes())
        for field in ('tests', 'skips', 'outcome'):
            changed = json.loads(json.dumps(receipt))
            item = changed['tests'][0]
            if field == 'tests':
                item['tests'] += 1
                changed['testsPassed'] += 1
            elif field == 'skips':
                item['skips'] += 1
                changed['skips'] += 1
            else:
                item['outcome'] = 'FAILED'
            commands = (json.dumps(changed['tests'], indent=2, sort_keys=True) + '\n').encode()
            (packet / 'command-results.json').write_bytes(commands)
            changed['packetFileSha256']['command-results.json'] = digest(commands)
            (packet / 'verification.json').write_text(json.dumps(changed, indent=2, sort_keys=True) + '\n')
            check(json.loads((packet / 'command-results.json').read_text()) == changed['tests'], 'coherent summary metadata setup')
            try:
                verify(root, packet)
            except AssertionError as exc:
                check(str(exc) == 'unittest summary:' + item['log'], 'summary self-test rejection reason:' + field)
            else:
                raise AssertionError('coherent unittest metadata drift escaped:' + field)

        # Restore the bound success metadata before each isolated variant.
        # Actual transcript bytes and every hash referencing them change, while
        # all success claims remain the original passing receipt values. This
        # catches parsers hardcoded to OK/zero skips as well as metadata drift.
        original_log = (PACKET / receipt['tests'][0]['log']).read_bytes()
        for summary in ('FAILED (failures=1)', 'OK (skipped=1)', 'OK (expected failures=1)', 'FAILED (unexpected successes=1)'):
            changed = json.loads(json.dumps(receipt))
            item = changed['tests'][0]
            transcript = adverse_transcript(item['tests'], summary)
            check(transcript != original_log, 'adverse transcript setup')
            (packet / item['log']).write_bytes(transcript)
            item['logSha256'] = digest(transcript)
            changed['packetFileSha256'][item['log']] = digest(transcript)
            commands = (json.dumps(changed['tests'], indent=2, sort_keys=True) + '\n').encode()
            (packet / 'command-results.json').write_bytes(commands)
            changed['packetFileSha256']['command-results.json'] = digest(commands)
            (packet / 'verification.json').write_text(json.dumps(changed, indent=2, sort_keys=True) + '\n')
            check(item['outcome'] == 'OK' and item['skips'] == 0 and changed['testsPassed'] == receipt['testsPassed'], 'unchanged transcript success claims')
            check(digest((packet / item['log']).read_bytes()) == item['logSha256'] == changed['packetFileSha256'][item['log']], 'coherent adverse log binding')
            try:
                verify(root, packet)
            except AssertionError as exc:
                check(str(exc) == 'unittest summary:' + item['log'], 'transcript rejection reason:' + summary)
            else:
                raise AssertionError('hash-consistent non-passing transcript escaped:' + summary)
        (packet / receipt['tests'][0]['log']).write_bytes(original_log)

        for variant in ('omission', 'command'):
            changed = json.loads(json.dumps(receipt))
            if variant == 'omission':
                changed['tests'] = [item for item in changed['tests'] if item['name'] != 'telemetry-postgres']
                changed['testsPassed'] = sum(item['tests'] for item in changed['tests'])
                changed['skips'] = sum(item['skips'] for item in changed['tests'])
                reason = 'required seven-suite inventory'
            else:
                changed['tests'][0]['argv'][-1] = 'eng/recovery-preparation/test_recovery_execution_boundary.py'
                reason = 'suite command:native-client-certificate'
            commands = (json.dumps(changed['tests'], indent=2, sort_keys=True) + '\n').encode()
            (packet / 'command-results.json').write_bytes(commands)
            changed['packetFileSha256']['command-results.json'] = digest(commands)
            (packet / 'verification.json').write_text(json.dumps(changed, indent=2, sort_keys=True) + '\n')
            check(json.loads((packet / 'command-results.json').read_text()) == changed['tests'], 'coherent suite metadata setup')
            try:
                verify(root, packet)
            except AssertionError as exc:
                check(str(exc) == reason, 'suite rejection reason:' + variant)
            else:
                raise AssertionError('coherent required suite control escaped:' + variant)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--self-test', action='store_true')
    options = parser.parse_args()
    receipt = verify(ROOT, PACKET)
    if options.self_test:
        self_test(receipt)
    print(json.dumps({'receiptContentVerified': True, 'gitHeadOrIndexRequired': False,
        'coherentWorkingSnapshotDriftRejected': True if options.self_test else None,
        'coherentCountSkipOutcomeDriftRejected': True if options.self_test else None,
        'hashConsistentNonPassingTranscriptsRejected': True if options.self_test else None,
        'coherentSuiteOmissionAndCommandDriftRejected': True if options.self_test else None,
        'historicalSpecCheckpointsSeparateFromWorkflowEdits': True}, sort_keys=True))
