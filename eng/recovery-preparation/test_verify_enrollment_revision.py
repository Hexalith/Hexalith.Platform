#!/usr/bin/env python3
"""Synthetic-only regression checks for the fixed-path revision verifier."""

import contextlib
import io
import json
import os
from pathlib import Path
import stat
import tempfile
import types
import unittest
from unittest.mock import patch


def load(path, name):
    directory = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        for part in path.parts[1:-1]:
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=directory)
            os.close(directory)
            directory = child
            info = os.fstat(directory)
            if info.st_uid not in (0, os.getuid()) or stat.S_IMODE(info.st_mode) & 0o022:
                raise ValueError('source-custody-refused')
        fd = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK, dir_fd=directory)
        with os.fdopen(fd, 'rb') as stream:
            before = os.fstat(stream.fileno())
            if (not stat.S_ISREG(before.st_mode) or before.st_nlink != 1 or before.st_size > 1048576
                    or before.st_uid not in (0, os.getuid()) or stat.S_IMODE(before.st_mode) & 0o022):
                raise ValueError('source-custody-refused')
            raw = stream.read(1048577)
            after = os.fstat(stream.fileno())
            if len(raw) > 1048576 or (before.st_size, before.st_mtime_ns, before.st_ctime_ns) != (after.st_size, after.st_mtime_ns, after.st_ctime_ns):
                raise ValueError('source-changed')
    finally:
        os.close(directory)
    module = types.ModuleType(name)
    module.__file__ = str(path)
    exec(compile(raw, str(path), 'exec'), module.__dict__)
    return module


r = load(Path(__file__).with_name('verify_enrollment_revision.py'), 'revision')
v = r.load_prior_verifier()


class RevisionVerifierTests(unittest.TestCase):
    def test_invalid_anchor_does_not_load_dependency_or_read_packet(self):
        for anchor in (None, '', 'SECRET', 'A' * 64, '0' * 63, '0' * 65, 1):
            with self.subTest(anchor=anchor), patch.object(r, 'load_prior_verifier') as dependency:
                with self.assertRaises(r.Refusal):
                    r.verify(anchor)
                dependency.assert_not_called()

    def test_wrong_anchor_stops_before_payloads(self):
        with patch.object(r, 'load_prior_verifier', return_value=v), patch.object(v, 'read_file', return_value=b'{}'), patch.object(v, 'check_record') as read:
            with self.assertRaises(r.Refusal):
                r.verify('0' * 64)
            read.assert_not_called()

    def test_cli_redacts_unknown_argument_and_anchor(self):
        for args in (['verify', '--sha256', 'SECRET'], ['verify', '--command', 'SECRET'], ['verify', '--sha', 'SECRET']):
            output = io.StringIO()
            with patch('sys.argv', args), contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
                self.assertEqual(r.main(), 1)
            self.assertNotIn('SECRET', output.getvalue())
            self.assertEqual(json.loads(output.getvalue())['operationalGate'], 'closed')

    def test_missing_source_rejected_before_read(self):
        for sources in ({}, {str(r.SOURCE.with_name('native_enrollment_execution.py')): {}}):
            with patch.object(v, 'check_record') as read, self.assertRaises(r.Refusal):
                r.validate_sources(v, sources)
            read.assert_not_called()

    def test_extra_source_cannot_select_credential_path(self):
        sources = {str(r.SOURCE.with_name(name)): {} for name in r.NEW_SOURCES}
        sources['/home/administrator/.kube/hexalith-production'] = {}
        with patch.object(v, 'check_record') as read, self.assertRaises(r.Refusal):
            r.validate_sources(v, sources)
        read.assert_not_called()

    def test_layout_mode_symlink_hardlink_and_extra_slot_refused(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            packet = root / 'packet-v1.json'
            payload = root / 'payload.json'
            for path in (packet, payload):
                path.write_bytes(b'{}')
                path.chmod(0o600)
            with patch.object(r, 'ROOT', root):
                r.check_private_layout(v, {'payload.json': {}})
                root.chmod(0o755)
                with self.assertRaises(r.Refusal):
                    r.check_private_layout(v, {'payload.json': {}})
                root.chmod(0o700)
                payload.chmod(0o644)
                with self.assertRaises(r.Refusal):
                    r.check_private_layout(v, {'payload.json': {}})
                payload.chmod(0o600)
                os.link(payload, root / 'alias')
                with self.assertRaises((r.Refusal, v.Refusal)):
                    r.check_private_layout(v, {'payload.json': {}})
                (root / 'alias').unlink()
                payload.unlink()
                payload.symlink_to(packet)
                with self.assertRaises((r.Refusal, v.Refusal)):
                    r.check_private_layout(v, {'payload.json': {}})
                payload.unlink()
                payload.write_bytes(b'{}')
                payload.chmod(0o600)
                (root / 'measurement-output').mkdir(mode=0o700)
                with self.assertRaises(v.Refusal):
                    r.check_private_layout(v, {'payload.json': {}})

    def synthetic_baseline(self):
        before = {f'/synthetic/history/{i}': {'sha256': str(i).zfill(64), 'bytes': 1} for i in range(550)}
        prior = dict(before)
        prior[r.STATUS] = {'sha256': r.OLD_STATUS_SHA, 'bytes': 1}
        before[r.STATUS] = {'sha256': r.CURRENT_STATUS_SHA, 'bytes': 2}
        before[str(r.PRIOR / 'packet-v1.json')] = {'sha256': r.PRIOR_SHA, 'bytes': 1}
        sources = {f'/synthetic/source/{i}': {} for i in range(5)}
        before.update({name: {'sha256': '0' * 64, 'bytes': 1} for name in sources})
        self.assertEqual(len(before), 557)
        return before, prior, {}, sources

    def test_only_pinned_historical_status_drift_is_accepted(self):
        before, prior, files, sources = self.synthetic_baseline()
        with patch.object(v, 'check_record') as check:
            r.validate_baseline(v, before, prior, files, sources)
        self.assertEqual(check.call_count, 557)
        before[r.STATUS]['sha256'] = '0' * 64
        with patch.object(v, 'check_record'), self.assertRaises(r.Refusal):
            r.validate_baseline(v, before, prior, files, sources)

    def test_baseline_cannot_omit_replace_or_add_any_member(self):
        before, prior, files, sources = self.synthetic_baseline()
        for mutation in ('omit', 'replace', 'credential'):
            changed = dict(before)
            if mutation != 'credential':
                changed.pop('/synthetic/history/0')
            if mutation == 'replace':
                changed['/synthetic/unbound'] = {}
            if mutation == 'credential':
                changed['/home/administrator/.kube/hexalith-production'] = {}
            with self.subTest(mutation=mutation), patch.object(v, 'check_record') as read:
                with self.assertRaises(r.Refusal):
                    r.validate_baseline(v, changed, prior, files, sources)
                read.assert_not_called()

    def test_original_historical_digest_cannot_be_rebased(self):
        before, prior, files, sources = self.synthetic_baseline()
        before['/synthetic/history/0'] = {'sha256': 'f' * 64, 'bytes': 1}
        with patch.object(v, 'check_record'), self.assertRaises(r.Refusal):
            r.validate_baseline(v, before, prior, files, sources)

    def test_final_baseline_catches_external_drift(self):
        before, prior, files, sources = self.synthetic_baseline()
        with patch.object(v, 'check_record', side_effect=v.Refusal('byte-binding-mismatch')), self.assertRaises(v.Refusal):
            r.validate_baseline(v, before, prior, files, sources)

    def test_prior_verifier_hash_is_checked_before_compilation(self):
        with patch.object(r, 'VERIFIER_SHA', '0' * 64), self.assertRaises(r.Refusal):
            r.load_prior_verifier()


class CompleteSyntheticRevision(unittest.TestCase):
    """Exercise the complete verifier using an isolated 557-file fake history."""

    def setUp(self):
        old_tests = load(Path(__file__).with_name('test_verify_native_supplement.py'), 'old_tests')
        self.fixture = old_tests.CompleteSyntheticVerification('test_complete_synthetic_packet_passes')
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.v = old_tests.v
        f = self.fixture
        # Expand its private supplement to exactly the original member count.
        for index in range(51):
            f.write(f.root / f'supplement-payload-{index}', b'synthetic-supplement')
        f.write_json(f.root / 'adapter-binding-v2.json', {'contract': {'synthetic': True}})
        prior_files = {p.name: f.record(p) for p in f.root.iterdir() if p.name != 'packet-v1.json'}
        f.write_json(f.root / 'packet-v1.json', {'files': prior_files})
        prior_sha = r.sha((f.root / 'packet-v1.json').read_bytes())
        self.root = f.base / 'revision'
        self.root.mkdir(mode=0o700)
        self.source_root = f.base / 'new-sources'
        self.source_root.mkdir(mode=0o700)
        for name in r.NEW_SOURCES:
            f.write(self.source_root / name, b'# synthetic-new-source\n')
        before = json.loads((f.root / 'preservation-before.json').read_bytes())['files']
        status = next(iter(f.statuses))
        old_status_sha = before[str(status)]['sha256']
        f.write(status, b'synthetic-known-prior-status-drift')
        before[str(status)] = f.record(status)
        before.update({str(p): f.record(p) for p in f.sources.iterdir()})
        before.update({str(p): f.record(p) for p in f.root.iterdir()})
        self.assertEqual(len(before), 557)
        for name in r.MANDATORY:
            f.write_json(self.root / name, {'synthetic': True})
        f.write_json(self.root / 'preservation-before.json', {'files': before})
        f.write_json(self.root / 'source-inventory.json', {'files': {str(p): f.record(p) for p in self.source_root.iterdir()}})
        source_bindings = {}
        for name in ('native_measurement_adapter.py', 'test_native_measurement_adapter.py',
                     'native_https_transport.py', 'test_native_https_transport.py',
                     'native_enrollment_execution.py', 'test_native_enrollment_execution.py'):
            directory = self.source_root if name in r.NEW_SOURCES else f.sources
            source_bindings[name] = f.record(directory / name)
        f.write_json(self.root / 'enrollment-execution-binding-v1.json', {
            'liveActivationAllowed': False, 'authorityAccepted': False,
            'operationalGate': 'closed', 'priorSupplementSha256': prior_sha,
            'configuration': {'context': 'jpiquot@local', 'endpoint': 'https://192.168.1.30:6443',
                'configSha256': 'f00b691a5886ef76e6be0d6e3efe4030251147b7bba74bb0c58fa1944e0fe86c',
                'actualConfigRead': False},
            'proposal': {'contract': {'synthetic': True}, 'sealedSupplementSha256': prior_sha,
                'preparedAt': r.PREPARED, 'policyCeiling': r.CEILING,
                'syntheticOnly': True, 'operationalGate': 'closed', 'sourceBindings': source_bindings,
                'runtime': {'inventorySha256': r.sha((f.root / 'runtime-inventory.json').read_bytes())},
                'enrollment': dict.fromkeys(('serverTrust', 'clientIdentity', 'nativeGrant', 'revocation',
                    'rights', 'breakGlass', 'cnpgController', 'measurementApproval', 'credentialDelivery', 'outputCustody'))},
            'outputCustody': {'root': str(self.root), 'child': 'measurement-output',
                'childMustRemainAbsent': True, 'directoryMode': 0o700, 'fileMode': 0o600,
                'slots': ['receipt.json', 'failure.json'], 'exclusiveSingleResult': True,
                'maximumBytes': 65536, 'attempts': 1, 'realSlotCreated': False}})
        f.write_json(self.root / 'evidence-gaps-v1.json', {'executionApprovalReady': False,
            'authorityAccepted': False, 'missingEvidence': list(range(12))})
        manifest = {'kind': 'hexalith.native-enrollment-execution-revision',
            'activeBinding': 'enrollment-execution-binding-v1.json',
            'priorSupplementSha256': prior_sha, 'policyPreparedAt': r.PREPARED,
            'policyCeilingEnds': r.CEILING, 'liveActivationAllowed': False,
            'authorityAccepted': False, 'operationalGate': 'closed',
            'files': {p.name: f.record(p) for p in self.root.iterdir()}}
        f.write_json(self.root / 'packet-v1.json', manifest)
        self.anchor = r.sha((self.root / 'packet-v1.json').read_bytes())
        self.stack = contextlib.ExitStack()
        self.addCleanup(self.stack.close)
        self.stack.enter_context(patch.multiple(r, ROOT=self.root, PRIOR=f.root,
            PRIOR_SHA=prior_sha, SOURCE=self.source_root / 'verify_enrollment_revision.py',
            STATUS=str(status), OLD_STATUS_SHA=old_status_sha,
            CURRENT_STATUS_SHA=before[str(status)]['sha256'], PUBLIC_EVIDENCE={}))
        self.stack.enter_context(patch.object(r, 'load_prior_verifier', return_value=self.v))

    def test_complete_synthetic_revision_passes_with_closed_gate(self):
        result = r.verify(self.anchor)
        self.assertEqual(result['preservationFilesUnchanged'], 557)
        self.assertEqual(result['priorSupplementFilesUnchanged'], 56)
        self.assertEqual(result['newSources'], 4)
        self.assertFalse(result['originalFullVerifierPass'])
        self.assertFalse(result['executionApprovalReady'])

    def reseal(self):
        f = self.fixture
        path = self.root / 'packet-v1.json'
        manifest = json.loads(path.read_bytes())
        manifest['files'] = {str(p.relative_to(self.root)): f.record(p)
                             for p in self.root.rglob('*') if p.is_file() and p != path}
        f.write_json(path, manifest)
        self.anchor = r.sha(path.read_bytes())

    def test_resealed_scope_source_runtime_and_false_authority_are_refused(self):
        f = self.fixture
        path = self.root / 'enrollment-execution-binding-v1.json'
        original = path.read_bytes()
        for change in ('scope', 'source', 'runtime', 'authority', 'output'):
            value = json.loads(original)
            if change == 'scope':
                value['proposal']['contract'] = {'requests': []}
            elif change == 'source':
                value['proposal']['sourceBindings']['native_measurement_adapter.py']['sha256'] = '0' * 64
            elif change == 'runtime':
                value['proposal']['runtime']['inventorySha256'] = '0' * 64
            elif change == 'authority':
                value['proposal']['enrollment']['serverTrust'] = {'callerSaysVerified': True}
            else:
                value['outputCustody']['attempts'] = 2
            f.write_json(path, value)
            self.reseal()
            with self.subTest(change=change), self.assertRaises(r.Refusal):
                r.verify(self.anchor)
        f.write(path, original)
        self.reseal()

    def test_public_evidence_source_is_rechecked_after_initial_validation(self):
        f = self.fixture
        evidence = f.base / 'public-evidence-source'
        evidence.mkdir(mode=0o700)
        f.write(evidence / 'public.json', b'synthetic-historical-public-evidence')
        (self.root / 'public-evidence').mkdir(mode=0o700)
        f.write(self.root / 'public-evidence/public.json', (evidence / 'public.json').read_bytes())
        self.reseal()
        original = self.v.read_file
        count = 0
        def mutate_after_initial_reads(candidate, private=False):
            nonlocal count
            value = original(candidate, private)
            if candidate == self.root / 'packet-v1.json':
                count += 1
                if count == 2:
                    (evidence / 'public.json').write_bytes(b'synthetic-evidence-drift')
            return value
        with patch.multiple(r, PUBLIC_EVIDENCE={'public.json': r.sha((evidence / 'public.json').read_bytes())}, EVIDENCE_ROOT=evidence):
            self.assertEqual(r.verify(self.anchor)['operationalGate'], 'closed')
            with patch.object(self.v, 'read_file', side_effect=mutate_after_initial_reads), self.assertRaises(r.Refusal):
                r.verify(self.anchor)

    def test_mutated_source_and_history_are_caught_on_final_recheck(self):
        for path in (self.source_root / 'native_enrollment_execution.py', self.fixture.changed_history):
            with self.subTest(path=path.name):
                raw = path.read_bytes()
                original = self.v.read_file
                count = 0
                def mutate_after_initial_reads(candidate, private=False):
                    nonlocal count
                    value = original(candidate, private)
                    if candidate == self.root / 'packet-v1.json':
                        count += 1
                        if count == 2:
                            path.write_bytes(b'synthetic-late-mutation')
                    return value
                try:
                    with patch.object(self.v, 'read_file', side_effect=mutate_after_initial_reads):
                        with self.assertRaises(self.v.Refusal):
                            r.verify(self.anchor)
                finally:
                    path.write_bytes(raw)


if __name__ == '__main__':
    unittest.main(verbosity=2)
