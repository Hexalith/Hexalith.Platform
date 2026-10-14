"""Offline packets retain sources and integrity, while all live gates remain closed."""
import json
import os
from pathlib import Path
import shutil
import socket
import stat
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import zipfile

import prepare_retirement as preparation
import retire


class PreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.project = self.base / 'project'
        real_project = Path(__file__).resolve().parents[2]
        for name in preparation.SOURCE_FILES:
            output = self.project / name
            output.parent.mkdir(parents=True, exist_ok=True)
            original = real_project / name
            output.write_bytes(original.read_bytes() if original.exists() else b'local preparation source\n')
        self.binding = retire.code_binding(self.project)
        (self.project / preparation.SOURCE_FILES[-1]).write_bytes(preparation.canonical({'currentBytes': self.binding}))
        self.inputs = self.base / 'inputs'
        self.inputs.mkdir(mode=0o700)
        self.handoff = self.inputs / 'handoff.md'
        self.handoff.write_text('# Timesheets staging dependency handoff\nDated input; no current approval.\n')
        self.inventory = self.inputs / 'inventory.json'
        self.resources = [{'apiVersion': 'v1', 'kind': 'Namespace', 'namespace': None, 'name': 'kube-system',
                           'uid': 'source-kube-system', 'resourceVersion': '1'}]
        self.inventory.write_bytes(preparation.canonical({'context': 'jpiquot@local',
            'sourceClusterUid': 'source-kube-system', 'capturedAt': '2026-10-01T00:00:00Z',
            'resources': self.resources}))
        self.catalog = self.base / 'catalog'
        self.namespaces = self.base / 'namespaces'
        self.receipt(self.catalog, 'catalog')
        self.receipt(self.namespaces, 'namespaces')
        self.roots = [self.base / name for name in ('management', 'recovery', 'upgrade')]
        for root in self.roots:
            root.mkdir(mode=0o700)
        self.output = self.base / 'packets'

    def receipt(self, directory, label):
        directory.mkdir(mode=0o700)
        cipher = b'age-encryption.org/v1\n-> ssh-ed25519 AbCdEf synthetic\n-> ssh-ed25519 GhIjKl synthetic\n--- synthetic\nretained ciphertext'
        export = {'file': 'retained.age', 'ciphertextSha256': preparation.sha(cipher),
                  'ciphertextBytes': len(cipher), 'readbackVerified': True,
                  'recipientStanzas': [{'type': 'ssh-ed25519', 'tag': 'AbCdEf'},
                                       {'type': 'ssh-ed25519', 'tag': 'GhIjKl'}]}
        code = self.binding['codeSha256']
        records = {'attempt.json': {'attemptId': 'historical-' + label,
              'scriptSha256': code['rehearse.py'], 'productionExecutorSha256': code['retire.py'],
              'productionProcedureSha256': self.binding['procedureSha256'],
              'sourceInventorySha256': preparation.sha(self.inventory.read_bytes()),
              'driverSha256': code['rehearse_' + label + '.py'],
              'moduleSha256': {name: code[name] for name in ('evidence.py', 'qualify.py')}},
            'summary.json': {'state': 'passed-limited'}, 'cleanup.json': {'passed': True},
            'encrypted-exports.json': {'readbackVerified': True, 'exports': [export]},
            'private-export-digests.json': {'exports': [{**export, 'plaintextSha256': 'a' * 64}]}}
        if label == 'catalog':
            records.update({'catalog-result.json': {'state': 'passed',
                'sourceCounts': dict.fromkeys(('Repo', 'Application', 'ApplicationVersion', 'Category'), 0)},
                'kubesphere-retirement-result.json': {'state': 'passed-dependency-first-native-retirement',
                                                     'phases': list(range(19))},
                'rollback-restore-result.json': {'state': 'passed', 'freshDockerNodeVerified': True,
                    'freshEtcdClusterAndMemberIdentities': True, 'sourceNodeDestroyedBeforeRestore': True,
                    'syntheticCanaryRestoredAndReadbackEqual': True, 'markCompacted': True,
                    'skipHashCheck': False, 'baselineUidMismatches': [], 'crdUidMismatches': []}})
        else:
            records['seven-namespace-result.json'] = {'state': 'passed', 'namespaceCount': 7,
              'actualNamespaceDeleteRequests': 0, 'actualNamespaceUidResourceVersionBoundPutRequests': 7}
        for name, record in records.items():
            preparation.write_new(directory / name, preparation.canonical(record))
        preparation.write_new(directory / 'retained.age', cipher)
        self.reseal(directory)

    def reseal(self, directory):
        sums = ''.join(f'{preparation.sha(path.read_bytes())}  {path.name}\n'
                       for path in sorted(directory.iterdir()) if path.name != 'SHA256SUMS')
        path = directory / 'SHA256SUMS'
        if path.exists():
            path.write_text(sums)
        else:
            preparation.write_new(path, sums.encode())

    def run_prepare(self, **changes):
        arguments = dict(project_root=self.project, evidence_root=self.output, attempt_id='preparation-1',
            operator='local preparer', handoff=self.handoff, inventory=self.inventory,
            catalog=self.catalog, namespaces=self.namespaces, recovery_roots=self.roots)
        arguments.update(changes)
        with patch.object(subprocess, 'run', side_effect=AssertionError('command dispatch forbidden')), \
             patch.object(subprocess, 'Popen', side_effect=AssertionError('command dispatch forbidden')), \
             patch.object(os, 'system', side_effect=AssertionError('command dispatch forbidden')), \
             patch.object(socket, 'create_connection', side_effect=AssertionError('network forbidden')), \
             patch.object(retire, 'run', side_effect=AssertionError('retirement dispatch forbidden')):
            return preparation.prepare(**arguments)

    def test_no_command_network_signing_or_retirement_dispatch(self):
        directory = self.run_prepare()
        self.assertFalse((directory / 'plan.json').exists())
        for path in directory.glob('*.json'):
            record = json.loads(path.read_bytes())
            self.assertIs(record['signed'], False)
            self.assertIs(record['mutationAuthorized'], False)
            self.assertIs(record['liveGateOpen'], False)
            self.assertIs(record['liveEvidenceCollected'], False)
            self.assertEqual(record['operationalState'], 'not-run')
            self.assertEqual(record['mutationRequests'], 0)

    def test_sources_remain_complete_after_originals_are_removed(self):
        handoff_bytes, inventory_bytes = self.handoff.read_bytes(), self.inventory.read_bytes()
        directory = self.run_prepare()
        lineage = json.loads((directory / 'input-lineage.json').read_bytes())['sources']
        shutil.rmtree(self.project)
        shutil.rmtree(self.inputs)
        shutil.rmtree(self.catalog)
        shutil.rmtree(self.namespaces)
        with zipfile.ZipFile(directory / 'preparation-sources.zip') as archive:
            self.assertEqual(archive.read('inputs/external-staging-dependency-handoff.md'), handoff_bytes)
            self.assertEqual(archive.read('inputs/historical-inventory.json'), inventory_bytes)
            for source in lineage:
                self.assertEqual(preparation.sha(archive.read(source['archiveMember'])), source['sha256'])
                self.assertEqual(len(archive.read(source['archiveMember'])), source['bytes'])
                self.assertEqual((archive.getinfo(source['archiveMember']).external_attr >> 16) & 0o777, 0o600)

    def test_owner_only_packet_and_checked_manifest(self):
        directory = self.run_prepare()
        self.assertEqual(stat.S_IMODE(self.output.stat().st_mode), 0o700)
        self.assertEqual(stat.S_IMODE(directory.stat().st_mode), 0o700)
        for path in directory.iterdir():
            self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o600)
        for line in (directory / 'SHA256SUMS').read_text().splitlines():
            digest, name = line.split('  ', 1)
            self.assertEqual(preparation.sha((directory / name).read_bytes()), digest)

    def test_signed_passing_future_metadata_cannot_open_gates(self):
        for root in self.roots:
            for name in ('backup-gate.json', 'console-closure.json', 'retirement-approval.json'):
                preparation.write_new(root / name, preparation.canonical({'signed': True, 'gateOpen': True,
                    'mutationAuthorized': True, 'verificationResult': 'pass', 'accepted': True,
                    'expiresAt': '2099-10-14T00:00:00Z', 'planSha256': 'a' * 64}))
                preparation.write_new(root / (name + '.sig'), b'not a validated signature')
        directory = self.run_prepare()
        gates = json.loads((directory / 'closed-gates.json').read_bytes())
        self.assertFalse(gates['story41GateOpen'])
        self.assertEqual(gates['timesheetsAcceptance'], 'FAIL')
        self.assertIsNone(gates['sourceClusterUid'])
        self.assertIsNone(gates['freshPlanSha256'])
        audit = json.loads((directory / 'metadata-audit.json').read_bytes())
        self.assertEqual(audit['inspectedRoots'], list(map(str, self.roots)))
        self.assertEqual(len(audit['records']), 9)
        self.assertFalse(audit['claimsTrusted'])
        self.assertTrue(all(v['signaturePresent'] and not v['signatureValidated'] for v in audit['records']))
        self.assertTrue(all(v['untrustedRecordClaims']['gateOpen'] for v in audit['records']))
        self.assertTrue(all(not v['archived'] and v['retainedAtExistingPrivateSource'] for v in audit['records']))
        with zipfile.ZipFile(directory / 'preparation-sources.zip') as archive:
            self.assertFalse(any(name.startswith('metadata/') for name in archive.namelist()))
            self.assertFalse(any(name.endswith('.sig') for name in archive.namelist()))

    def test_existing_id_preserved(self):
        directory = self.run_prepare()
        before = {path.name: path.read_bytes() for path in directory.iterdir()}
        with self.assertRaisesRegex(ValueError, 'existing-preparation-id'):
            self.run_prepare()
        self.assertEqual(before, {path.name: path.read_bytes() for path in directory.iterdir()})

    def test_repository_and_recovery_and_input_destinations_refused(self):
        for output in (self.project / 'packet', self.roots[0] / 'packet', self.inputs / 'packet',
                       self.catalog / 'packet', self.base):
            with self.subTest(output=output), self.assertRaisesRegex(ValueError, 'output-overlap'):
                self.run_prepare(evidence_root=output)
            self.assertFalse((output / 'preparation-1').exists())

    def test_other_repository_custody_refused(self):
        repository = self.base / 'another-repo'
        (repository / '.git').mkdir(parents=True)
        with self.assertRaisesRegex(ValueError, 'repository-output-refused'):
            self.run_prepare(evidence_root=repository / 'packets')

    def test_symlink_custody_and_source_refused_without_overwriting(self):
        destination = self.base / 'destination'
        destination.mkdir(mode=0o700)
        self.output.symlink_to(destination, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, 'symlink'):
            self.run_prepare()
        self.assertEqual(list(destination.iterdir()), [])
        self.output.unlink()
        original = self.handoff.read_bytes()
        actual = self.inputs / 'actual.md'
        self.handoff.rename(actual)
        self.handoff.symlink_to(actual)
        with self.assertRaisesRegex(ValueError, 'symlink'):
            self.run_prepare()
        self.assertEqual(actual.read_bytes(), original)
        self.assertFalse(self.output.exists())

    def test_unsafe_existing_custody_permissions_preserved(self):
        self.output.mkdir(mode=0o755)
        before = stat.S_IMODE(self.output.stat().st_mode)
        with self.assertRaisesRegex(ValueError, 'owner-only-directory'):
            self.run_prepare()
        self.assertEqual(stat.S_IMODE(self.output.stat().st_mode), before)
        self.assertEqual(list(self.output.iterdir()), [])

    def test_tampered_manifest_member_refuses_before_publication(self):
        (self.catalog / 'retained.age').write_bytes(b'changed evidence')
        with self.assertRaisesRegex(ValueError, 'checksum-mismatch'):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_two_recipient_receipt_drift_refuses_even_after_rechecksum(self):
        path = self.catalog / 'encrypted-exports.json'
        record = json.loads(path.read_bytes())
        record['exports'][0]['recipientStanzas'][1]['tag'] = 'AbCdEf'
        path.write_bytes(preparation.canonical(record))
        self.reseal(self.catalog)
        with self.assertRaisesRegex(ValueError, 'two-recipient'):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_source_bound_rehearsal_mismatch_refuses(self):
        record = json.loads(self.inventory.read_bytes())
        record['resources'][0]['resourceVersion'] = '2'
        self.inventory.write_bytes(preparation.canonical(record))
        with self.assertRaisesRegex(ValueError, 'rehearsal-execution-bytes-mismatch'):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_malformed_required_input_refuses_before_publication(self):
        for data in (b'{}', b'{"context":"jpiquot@local","context":"other"}',
                     b'{"resources":NaN}', b'{"resources":[]}'):
            with self.subTest(data=data):
                self.inventory.write_bytes(data)
                with self.assertRaises((ValueError, KeyError)):
                    self.run_prepare()
                self.assertFalse(self.output.exists())

    def test_missing_required_source_refuses_without_publication(self):
        (self.project / 'eng/cluster-management/STAGING-PREREQUISITES.md').unlink()
        with self.assertRaises(OSError):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_changed_source_between_capture_and_publication_refused(self):
        actual = preparation.read_source
        calls = 0
        def replaced(path, private=False):
            nonlocal calls
            data = actual(path, private)
            if Path(path) == self.handoff:
                calls += 1
                if calls > 1:
                    return data + b'changed between capture and publication'
            return data
        with patch.object(preparation, 'read_source', side_effect=replaced), \
             self.assertRaisesRegex(ValueError, 'input-changed-before-publication'):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_historical_binding_and_retained_limits_are_truthful(self):
        directory = self.run_prepare()
        measured = json.loads((directory / 'historical-integrity.json').read_bytes())
        self.assertTrue(measured['actualPureRehearsalValidatorAccepted'])
        self.assertFalse(measured['fixturesRun'])
        for receipt in measured['privateManifests'].values():
            self.assertFalse(receipt['freshDecryptionPerformed'])
            self.assertFalse(receipt['independentOffNodeAcceptance'])
            self.assertEqual(receipt['encryptedExportCount'], 1)

    def test_manifest_ciphertext_omitted_from_both_export_lists_refused(self):
        preparation.write_new(self.catalog / 'omitted.age', (self.catalog / 'retained.age').read_bytes())
        self.reseal(self.catalog)
        with self.assertRaisesRegex(ValueError, 'retained-export-set-mismatch'):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_nonsticky_writable_ancestor_refused_for_new_and_existing_roots(self):
        ancestor = self.base / 'writable-parent'
        ancestor.mkdir()
        ancestor.chmod(0o777)
        output = ancestor / 'packets'
        for existing in (False, True):
            if existing:
                output.mkdir(mode=0o700)
            with self.subTest(existing=existing), \
                 self.assertRaisesRegex(ValueError, 'custody-ancestor'):
                self.run_prepare(evidence_root=output)
            self.assertFalse((output / 'preparation-1').exists())
            if existing:
                self.assertEqual(list(output.iterdir()), [])
        self.assertEqual(stat.S_IMODE(ancestor.stat().st_mode), 0o777)

    def test_safe_sticky_shared_ancestor_permits_custody(self):
        ancestor = self.base / 'sticky-parent'
        ancestor.mkdir()
        ancestor.chmod(0o1777)
        directory = self.run_prepare(evidence_root=ancestor / 'packets')
        self.assertTrue(directory.is_dir())
        self.assertEqual(stat.S_IMODE(ancestor.stat().st_mode), 0o1777)

    def test_sticky_ancestor_with_untrusted_owner_refused(self):
        ancestor = self.base / 'sticky-parent'
        ancestor.mkdir()
        ancestor.chmod(0o1777)
        original = Path.stat
        def untrusted(path, *args, **kwargs):
            observed = original(path, *args, **kwargs)
            if path == ancestor:
                fields = list(observed)
                fields[4] = max(os.getuid(), 1) + 10000
                return os.stat_result(fields)
            return observed
        with patch.object(Path, 'stat', new=untrusted), \
             self.assertRaisesRegex(ValueError, 'custody-ancestor'):
            self.run_prepare(evidence_root=ancestor / 'packets')
        self.assertFalse((ancestor / 'packets').exists())

    def test_rehearsal_file_mode_drift_refused_at_final_source_check(self):
        actual = retire.validate_rehearsals
        def changed(*args, **kwargs):
            receipt = actual(*args, **kwargs)
            (self.catalog / 'retained.age').chmod(0o644)
            return receipt
        with patch.object(retire, 'validate_rehearsals', side_effect=changed), \
             self.assertRaisesRegex(ValueError, 'owner-only-input'):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_rehearsal_directory_mode_drift_refused_before_publication(self):
        actual = preparation.metadata_audit
        def changed(*args, **kwargs):
            audit = actual(*args, **kwargs)
            self.catalog.chmod(0o755)
            return audit
        with patch.object(preparation, 'metadata_audit', side_effect=changed), \
             self.assertRaisesRegex(ValueError, 'owner-only-directory'):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_selected_preparer_must_match_executing_source(self):
        path = self.project / 'eng/cluster-management/prepare_retirement.py'
        path.write_bytes(path.read_bytes() + b'\n# different selected preparer\n')
        with self.assertRaisesRegex(ValueError, 'executing-preparer-source-mismatch'):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_metadata_traversal_error_refuses_without_publication(self):
        actual = os.walk
        def refused(root, **kwargs):
            if root == self.roots[1]:
                kwargs['onerror'](PermissionError('metadata traversal denied'))
            return actual(root, **kwargs)
        with patch.object(os, 'walk', side_effect=refused), \
             self.assertRaisesRegex(PermissionError, 'traversal denied'):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_metadata_and_signature_additions_refuse_final_scope_check(self):
        existing = self.roots[1] / 'backup-gate.json'
        preparation.write_new(existing, b'{"signed": true}')
        actual = preparation.metadata_audit
        for addition in ('metadata', 'signature'):
            added = self.roots[1] / ('console-closure.json' if addition == 'metadata' else 'backup-gate.json.sig')
            calls = 0
            def changed(*args, **kwargs):
                nonlocal calls
                audit = actual(*args, **kwargs)
                calls += 1
                if calls == 1:
                    preparation.write_new(added, b'{}')
                return audit
            with self.subTest(addition=addition), \
                 patch.object(preparation, 'metadata_audit', side_effect=changed), \
                 self.assertRaisesRegex(ValueError, 'metadata-scope-or-identity-changed'):
                self.run_prepare()
            self.assertFalse(self.output.exists())
            added.unlink()

    def test_metadata_same_bytes_replacement_changes_identity_and_refuses(self):
        existing = self.roots[1] / 'backup-gate.json'
        preparation.write_new(existing, b'{"signed": true}')
        actual = preparation.metadata_audit
        calls = 0
        def changed(*args, **kwargs):
            nonlocal calls
            audit = actual(*args, **kwargs)
            calls += 1
            if calls == 1:
                existing.rename(existing.with_name('retained-old-original'))
                preparation.write_new(existing, b'{"signed": true}')
            return audit
        with patch.object(preparation, 'metadata_audit', side_effect=changed), \
             self.assertRaisesRegex(ValueError, 'metadata-scope-or-identity-changed'):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_validator_manifest_and_record_digests_must_match_archived_bytes(self):
        actual = retire.validate_rehearsals
        for changed_field in ('manifest', 'record'):
            def changed(*args, **kwargs):
                receipts = actual(*args, **kwargs)
                if changed_field == 'manifest':
                    receipts['catalog']['checksumsSha256'] = '0' * 64
                else:
                    receipts['catalog']['records']['attempt.json'] = '0' * 64
                return receipts
            with self.subTest(changed_field=changed_field), \
                 patch.object(retire, 'validate_rehearsals', side_effect=changed), \
                 self.assertRaisesRegex(ValueError, 'validator-receipt-archive-mismatch'):
                self.run_prepare()
            self.assertFalse(self.output.exists())

    def test_overflowing_metadata_serialization_refuses_before_allocating_custody(self):
        preparation.write_new(self.roots[1] / 'backup-gate.json', b'{"kind": 1e999, "signed": true}')
        with self.assertRaisesRegex(ValueError, 'Out of range float'):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_partial_staging_failure_is_preserved_and_burns_attempt_id(self):
        actual = preparation.write_new
        def failed(path, data):
            if Path(path).name == 'historical-integrity.json':
                raise OSError('staging write refused')
            return actual(path, data)
        with patch.object(preparation, 'write_new', side_effect=failed), \
             self.assertRaisesRegex(OSError, 'staging write refused'):
            self.run_prepare()
        self.assertFalse((self.output / 'preparation-1').exists())
        stages = list(self.output.glob('.preparation-1.incomplete-*'))
        self.assertEqual(len(stages), 1)
        staging = stages[0]
        self.assertEqual(stat.S_IMODE(staging.stat().st_mode), 0o700)
        before = {path.name: path.read_bytes() for path in staging.iterdir()}
        self.assertIn('preparation-sources.zip', before)
        self.assertIs(json.loads(before['attempt.json'])['preparationComplete'], False)
        with self.assertRaisesRegex(ValueError, 'existing-preparation-id'):
            self.run_prepare()
        self.assertEqual(before, {path.name: path.read_bytes() for path in staging.iterdir()})
        self.assertTrue(all(stat.S_IMODE(path.stat().st_mode) == 0o600 for path in staging.iterdir()))

    def test_nested_metadata_and_signature_lineage_is_measured_without_copying_originals(self):
        expected = {}
        for root in reversed(self.roots):
            for branch in ('z-history', 'a-history'):
                directory = root / branch / 'gate'
                directory.parent.mkdir(mode=0o700)
                directory.mkdir(mode=0o700)
                path = directory / 'backup-gate.json'
                data = preparation.canonical({'signed': True, 'expiresAt': '2026-10-02T00:00:00Z'})
                preparation.write_new(path, data)
                signature = path.with_name(path.name + '.sig')
                preparation.write_new(signature, b'historical detached signature')
                expected[str(path)] = (preparation.sha(data), str(signature), preparation.sha(signature.read_bytes()))
        directory = self.run_prepare()
        audit = json.loads((directory / 'metadata-audit.json').read_bytes())
        self.assertEqual([v['path'] for v in audit['records']],
                         [str(root / branch / 'gate/backup-gate.json')
                          for root in self.roots for branch in ('a-history', 'z-history')])
        for record in audit['records']:
            digest, signature_path, signature_digest = expected[record['path']]
            self.assertEqual(record['sha256'], digest)
            self.assertEqual(record['signaturePath'], signature_path)
            self.assertEqual(record['signatureSha256'], signature_digest)
            self.assertFalse(record['signatureValidated'])
            self.assertFalse(record['archived'])
            self.assertEqual(record['sourceIdentity']['inode'], Path(record['path']).stat().st_ino)
        retained = json.loads((directory / 'input-lineage.json').read_bytes())['retainedMetadata']
        self.assertEqual({v['path'] for v in retained}, set(expected))
        self.assertTrue(all('untrustedRecordClaims' not in v for v in retained))
        with zipfile.ZipFile(directory / 'preparation-sources.zip') as archive:
            self.assertFalse(any(name.startswith('metadata/') or name.endswith('.sig') for name in archive.namelist()))

    def test_published_packet_replaces_only_its_empty_reservation(self):
        actual = os.rename
        observed = []
        def checked(source, destination):
            self.assertTrue(Path(source).name.startswith('.preparation-1.incomplete-'))
            self.assertEqual(list(Path(destination).iterdir()), [])
            self.assertEqual(stat.S_IMODE(Path(destination).stat().st_mode), 0o700)
            attempt = json.loads((Path(source) / 'attempt.json').read_bytes())
            self.assertTrue(attempt['preparationComplete'])
            observed.append((source, destination))
            return actual(source, destination)
        with patch.object(os, 'rename', side_effect=checked):
            directory = self.run_prepare()
        self.assertEqual(len(observed), 1)
        self.assertTrue(directory.is_dir())
        self.assertEqual(list(self.output.glob('.preparation-1.incomplete-*')), [])


if __name__ == '__main__':
    unittest.main()
