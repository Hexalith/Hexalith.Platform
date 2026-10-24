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


# These complete inventories are defined independently of receipt metadata.
DEPENDENCY_SOURCES = ('eng/recovery-preparation/_execution_boundary_fixtures.py',
 'eng/recovery-preparation/fresh_prerequisite.py',
 'eng/recovery-preparation/immutable_readback_driver.py',
 'eng/recovery-preparation/native_enrollment_execution.py',
 'eng/recovery-preparation/native_https_transport.py',
 'eng/recovery-preparation/native_measurement_adapter.py',
 'eng/recovery-preparation/policy_authentication_driver.py',
 'eng/recovery-preparation/recovery_adapters.py',
 'eng/recovery-preparation/recovery_execution_boundary.py',
 'eng/recovery-preparation/static_transport_preflight.py',
 'eng/recovery-preparation/telemetry_postgres_adapter.py',
 'eng/recovery-preparation/test_fresh_prerequisite.py',
 'eng/recovery-preparation/test_immutable_readback_driver.py',
 'eng/recovery-preparation/test_immutable_s3_provider.py',
 'eng/recovery-preparation/test_native_client_certificate.py',
 'eng/recovery-preparation/test_native_enrollment_execution.py',
 'eng/recovery-preparation/test_native_https_transport.py',
 'eng/recovery-preparation/test_native_measurement_adapter.py',
 'eng/recovery-preparation/test_policy_authentication_driver.py',
 'eng/recovery-preparation/test_recovery_adapters.py',
 'eng/recovery-preparation/test_recovery_execution_boundary.py',
 'eng/recovery-preparation/test_static_transport_preflight.py',
 'eng/recovery-preparation/test_telemetry_postgres_adapter.py',
 'eng/recovery-preparation/test_verify_enrollment_revision.py',
 'eng/recovery-preparation/test_verify_native_supplement.py',
 'eng/recovery-preparation/test_verify_packet.py',
 'eng/recovery-preparation/verify_enrollment_revision.py',
 'eng/recovery-preparation/verify_native_supplement.py',
 'eng/recovery-preparation/verify_packet.py')
SUPPORTING_PACKET_FILES = ('README.md',
 'cancellation-sweeps.json',
 'check-parser-mutant.py',
 'command-results.json',
 'common-adapter.log',
 'connector-audit.log',
 'current-head.txt',
 'diff-check.log',
 'execution-boundary.log',
 'handoff-prefixes.json',
 'historical-parent-blind-probes.py',
 'historical-parent-parent-guard-entry.py',
 'immutable-readback.log',
 'immutable-s3-provider.log',
 'matrix-coverage.md',
 'native-client-certificate.log',
 'parser-mutant-control.json',
 'policy-authentication.log',
 'pre-correction/blind-probes.log',
 'pre-correction/cancellation-sweeps.json',
 'pre-correction/common-adapter.log',
 'pre-correction/diff-check.log',
 'pre-correction/execution-boundary.log',
 'pre-correction/immutable-readback.log',
 'pre-correction/immutable-s3-provider.log',
 'pre-correction/native-client-certificate.log',
 'pre-correction/parent-guard-entry.json',
 'pre-correction/parser-mutant.log',
 'pre-correction/policy-authentication.log',
 'pre-correction/pre-review-content.json',
 'pre-correction/preparation-head.txt',
 'pre-correction/prior-test-names.json',
 'pre-correction/receipt-self-test.log',
 'pre-correction/results.json',
 'pre-correction/telemetry-postgres.log',
 'preparation-head.txt',
 'prior-packet-sha256.json',
 'receipt-check.log',
 'run-cancellation-sweeps.py',
 'run-seven-suites.py',
 'runtime-environment.json',
 'sources/_execution_boundary_fixtures.py',
 'sources/recovery_execution_boundary.py',
 'sources/test_native_client_certificate.py',
 'sources/test_recovery_execution_boundary.py',
 'spec-dispatch-baseline.md',
 'spec-verification-checkpoint.md',
 'telemetry-postgres.log',
 'verify-receipt.py')
SWEEP_MODES = ('line', 'opcode', 'secondary', 'preparedEntry')
SWEEP_FLAGS = ('allTargetsInjected', 'descriptorTablesPreserved', 'allPeersClosed', 'primaryPropagationVerified')



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
    statuses = re.findall(r'^\w+ \([^\n]*\) \.\.\. ([^\n]+)$', text, re.MULTILINE)
    check(outcome == 'OK' and not detail and len(statuses) == int(counts[0]) and
        all(status == 'ok' for status in statuses), 'passing unittest summary')
    return {'tests': int(counts[0]), 'skips': 0, 'outcome': outcome}


def executed_test_names(raw, expected, total):
    rows = re.findall(r'^(test_\w+) \(__main__\.([A-Za-z_]\w*)\.(test_\w+)\) \.\.\. [^\n]+$', raw.decode('utf-8'), re.MULTILINE)
    names = [cls + '.' + method for label, cls, method in rows]
    return (len(rows) == len(set(names)) == total and
        all(label == method for label, cls, method in rows) and set(names) == expected)


def adverse_transcript(total, summary, identities=None):
    """Real same-count unittest runs; named controls keep the source inventory."""
    identities = sorted(identities) if identities is not None else ['Unrelated.test_other_' + str(i) for i in range(total)]
    check(len(identities) == total, 'actual transcript identity setup')
    def passing(self):
        pass
    def control(self):
        if summary in ('FAILED (failures=1)', 'OK (expected failures=1)'):
            self.fail('synthetic receipt-checker negative control')
    if summary == 'OK (skipped=1)':
        control = unittest.skip('synthetic receipt-checker negative control')(control)
    elif summary in ('OK (expected failures=1)', 'FAILED (unexpected successes=1)'):
        control = unittest.expectedFailure(control)
    cases = []
    for index, identity in enumerate(identities):
        cls, method = identity.split('.')
        kind = type(cls, (unittest.TestCase,), {'__module__': '__main__', method: control if index == 0 else passing})
        cases.append(kind(method))
    output = io.StringIO()
    result = unittest.TextTestRunner(stream=output, verbosity=2).run(unittest.TestSuite(cases))
    check(result.testsRun == total and output.getvalue().rstrip().endswith(summary), 'actual adverse transcript setup')
    return output.getvalue().encode('utf-8')


def verify_sweeps(packet, receipt):
    results = json.loads((packet / 'cancellation-sweeps.json').read_text())
    metadata = receipt['currentCancellationSweeps']
    check(set(results) == set(SWEEP_MODES) and set(metadata['modes']) == set(SWEEP_MODES), 'complete cancellation sweep inventory')
    for mode in SWEEP_MODES:
        result = results[mode]
        check(type(result['targets']) is int and result['targets'] > 0, 'positive sweep targets:' + mode)
        check(result['findings'] == [] and result['unraisableExceptions'] == [], 'clean sweep results:' + mode)
        check(all(result[flag] is True for flag in SWEEP_FLAGS), 'verified sweep flags:' + mode)
        terminal = result['terminalExternalCancellations']
        check(type(terminal) is list and (mode == 'secondary' or not terminal), 'terminal sweep classification:' + mode)
        check(all(item['resourcesReleasedBeforeInjection'] is True and item['externalCancellationPropagated'] is True
            for item in terminal), 'terminal release and propagation:' + mode)
        check(len({tuple(item['target']) for item in terminal}) == len(terminal), 'unique terminal targets:' + mode)
        actual = {'targets': result['targets'], 'terminalExternalCancellations': len(terminal),
            **{flag: result[flag] for flag in SWEEP_FLAGS}}
        check(metadata['modes'][mode] == actual, 'sweep metadata:' + mode)
    check(metadata['findings'] == metadata['unraisableExceptions'] == 0 and
        metadata['lineTargets'] == results['line']['targets'] and metadata['opcodeTargets'] == results['opcode']['targets'] and
        metadata['secondaryTargets'] == results['secondary']['targets'] and metadata['preparedEntryTargets'] == results['preparedEntry']['targets'] and
        metadata['terminalExternalCancellationsAfterResourceRelease'] == len(results['secondary']['terminalExternalCancellations']), 'sweep aggregate metadata')
    check(metadata['source'] == 'run-cancellation-sweeps.py' and metadata['result'] == 'cancellation-sweeps.json', 'sweep source/result mapping')
    entry = results['preparedEntry']['entryTransitions']
    check(len(entry) == len({tuple(item) for item in entry}) == results['preparedEntry']['targets'] and
        any(item[0] == 'call' and item[1] == 'NativeTLS._guard_native' for item in entry) and
        any(item[0] == 'line' and item[1] == 'NativeTLS.exchange' for item in entry) and
        any(item[0] == 'opcode' and item[1] == 'NativeTLS.exchange' for item in entry), 'prepared entry transition inventory')


def verify(root, packet):
    receipt = json.loads((packet / 'verification.json').read_text())
    check(receipt['baselineCommit'] == '693390a790d95a9bad9b1bd9cc0f0f3052f3fbef', 'parent baseline')
    check(receipt['storyStatus'] == 'in-progress' and receipt['publicActivation'] == 'disabled' and
        receipt['operationalGate'] == 'closed' and receipt['operationalQualification'] is None and
        receipt['recoveryAcceptance'] is None, 'closed operational scope')
    check(set(receipt['sourceSha256']) == set(DEPENDENCY_SOURCES), 'complete dependency source inventory')
    check(set(receipt['packetFileSha256']) == set(SUPPORTING_PACKET_FILES), 'complete supporting packet inventory')
    check({str(path.relative_to(packet)) for path in packet.rglob('*') if path.is_file()} ==
        set(SUPPORTING_PACKET_FILES) | {'verification.json'}, 'all packet files bound')
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
        check(executed_test_names((packet / item['log']).read_bytes(),
            test_names(root / 'eng/recovery-preparation' / REQUIRED_SUITES[item['name']]), actual['tests']), 'unittest identities:' + item['log'])
    check(json.loads((packet / 'command-results.json').read_text()) == receipt['tests'], 'command results match receipt')
    verify_sweeps(packet, receipt)
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
        check(test_names(packet.parent / 'review-4/sources' / name) <= test_names(root / 'eng/recovery-preparation' / name),
            'all review-4 test names retained:' + name)
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
            transcript = adverse_transcript(item['tests'], summary, test_names(root / 'eng/recovery-preparation' / REQUIRED_SUITES[item['name']]))
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

        # Every control starts from original bytes and rebinds only the changed
        # content hashes. All original passing claims stay unchanged unless the
        # particular control is explicitly a mismatched metadata check.
        def restore():
            shutil.rmtree(packet)
            shutil.copytree(PACKET, packet)
            for name in DEPENDENCY_SOURCES:
                shutil.copyfile(ROOT / name, root / name)

        def reject(changed, reason):
            (packet / 'verification.json').write_text(json.dumps(changed, indent=2, sort_keys=True) + '\n')
            try:
                verify(root, packet)
            except AssertionError as exc:
                check(str(exc) == reason, 'isolated control rejection reason:' + reason + ':' + str(exc))
            else:
                raise AssertionError('isolated coherent control escaped:' + reason)

        for kind in ('empty-sources', 'omitted-dependency', 'empty-packet', 'omitted-packet', 'unlisted-dependency-drift'):
            restore()
            changed = json.loads(json.dumps(receipt))
            dependency = 'eng/recovery-preparation/policy_authentication_driver.py'
            if kind == 'empty-sources':
                changed['sourceSha256'] = {}
            elif kind in ('omitted-dependency', 'unlisted-dependency-drift'):
                del changed['sourceSha256'][dependency]
                if kind == 'unlisted-dependency-drift':
                    target = root / dependency
                    target.write_bytes(target.read_bytes() + b'\n# synthetic omitted dependency drift\n')
            elif kind == 'empty-packet':
                changed['packetFileSha256'] = {}
            else:
                del changed['packetFileSha256']['runtime-environment.json']
            reject(changed, 'complete dependency source inventory' if kind in ('empty-sources', 'omitted-dependency', 'unlisted-dependency-drift') else 'complete supporting packet inventory')

        for kind in ('finding', 'unraisable', 'count', 'terminal-count', 'terminal-flag', 'release-flag'):
            restore()
            changed = json.loads(json.dumps(receipt))
            sweep = json.loads((packet / 'cancellation-sweeps.json').read_text())
            if kind == 'finding':
                sweep['line']['findings'] = [{'descriptorTablePreserved': False, 'injected': True}]
                reason = 'clean sweep results:line'
            elif kind == 'unraisable':
                sweep['opcode']['unraisableExceptions'] = ['KeyboardInterrupt']
                reason = 'clean sweep results:opcode'
            elif kind == 'count':
                sweep['line']['targets'] += 1
                reason = 'sweep metadata:line'
            elif kind == 'terminal-count':
                sweep['secondary']['terminalExternalCancellations'].pop()
                reason = 'sweep metadata:secondary'
            elif kind == 'terminal-flag':
                sweep['secondary']['terminalExternalCancellations'][0]['resourcesReleasedBeforeInjection'] = False
                reason = 'terminal release and propagation:secondary'
            else:
                sweep['preparedEntry']['descriptorTablesPreserved'] = False
                reason = 'verified sweep flags:preparedEntry'
            raw = (json.dumps(sweep, indent=2, sort_keys=True) + '\n').encode()
            (packet / 'cancellation-sweeps.json').write_bytes(raw)
            changed['packetFileSha256']['cancellation-sweeps.json'] = digest(raw)
            for item in changed['verificationCommands']:
                if item['log'] == 'cancellation-sweeps.json':
                    item['logSha256'] = digest(raw)
            reject(changed, reason)

        for kind in ('unrelated', 'repeated'):
            restore()
            changed = json.loads(json.dumps(receipt))
            item = changed['tests'][0]
            if kind == 'unrelated':
                raw = adverse_transcript(item['tests'], 'OK')
            else:
                identity = next(iter(test_names(root / 'eng/recovery-preparation' / REQUIRED_SUITES[item['name']])))
                raw = adverse_transcript(item['tests'], 'OK', [identity] * item['tests'])
            check(unittest_summary(raw) == {'tests': item['tests'], 'skips': 0, 'outcome': 'OK'}, 'same-count passing transcript control')
            (packet / item['log']).write_bytes(raw)
            item['logSha256'] = changed['packetFileSha256'][item['log']] = digest(raw)
            commands = (json.dumps(changed['tests'], indent=2, sort_keys=True) + '\n').encode()
            (packet / 'command-results.json').write_bytes(commands)
            changed['packetFileSha256']['command-results.json'] = digest(commands)
            reject(changed, 'unittest identities:' + item['log'])


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
        'completeDependencyAndSupportingInventoriesRequired': True if options.self_test else None,
        'hashConsistentAdverseSweepAndMetadataControlsRejected': True if options.self_test else None,
        'sameCountUnrelatedAndRepeatedTestsRejected': True if options.self_test else None,
        'historicalSpecCheckpointsSeparateFromWorkflowEdits': True}, sort_keys=True))
