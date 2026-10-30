"""Offline binary/private-custody tests. All credentials and receipts are fictional."""
from contextlib import ExitStack
from copy import deepcopy
import hashlib
from http.server import BaseHTTPRequestHandler, HTTPServer
import json
import os
from pathlib import Path
import ssl
import subprocess
import sys
import threading
import unittest
from unittest.mock import patch

import campaign_capacity as capacity
import campaign_lifecycle as lifecycle
import calibration_s3_backup as s3
import independent_calibration as calibration
from qualify import Qualification
import test_independent_calibration as fixtures


class S3BackupTests(unittest.TestCase):
    setUp = fixtures.IndependentCalibrationTests.setUp
    write = fixtures.IndependentCalibrationTests.write
    sign = fixtures.IndependentCalibrationTests.sign
    seal = fixtures.IndependentCalibrationTests.seal
    runner = fixtures.IndependentCalibrationTests.runner
    start = fixtures.IndependentCalibrationTests.start

    def s3_plan(self):
        self.legacy_identity = calibration.identity(self.plan)
        writer = self.write('writer.env', b'AWS_ACCESS_KEY_ID=FICTIONALWRITER01\nAWS_SECRET_ACCESS_KEY=fictional-writer-secret-0001\n')
        validator = self.write('validator.env', b'SCW_ACCESS_KEY=FICTIONALVALIDATOR02\nSCW_SECRET_KEY=fictional-validator-secret-0002\n')
        self.plan['s3Backup'] = {'schema': s3.SCHEMA, **s3.DESTINATION,
            'key': 'memories/calibration/' + self.plan['runId'] + '.dump', 'poolId': 'unit-provider-pool',
            'failureDomain': 'unit-independent-provider', 'curl': {'path': '/usr/bin/curl', 'sha256': lifecycle.sha('/usr/bin/curl')},
            'curlVersion': '8.18.0', 'ca': self.write('ca.pem', b'fictional-ca-not-for-network'),
            'writer': {'identity': s3.IDENTITIES['writer'], 'credentials': writer},
            'validator': {'identity': s3.IDENTITIES['validator'], 'credentials': validator},
            'maximumArchiveBytes': 1024 * 1024, 'requestTimeoutSeconds': 10}
        config = self.plan['s3Backup']
        binding = {**s3.DESTINATION, 'key': config['key'], 'poolId': config['poolId'], 'failureDomain': config['failureDomain'],
            'reservationId': 'fictional-provider-reservation', 'owner': 'github:user:6775094',
            'validFromUtcMs': self.now - 1000, 'validUntilUtcMs': self.plan['window']['expiresUtcMs']}
        self.plan['reservations']['backup']['binding'] = binding
        # Compute/primary stay under their exact existing signed schema.
        self.receipts.pop('backup')
        self.seal()
        self.provider = {'schema': s3.SCHEMA, **binding, 'issuer': 'unit-backup', 'runId': self.plan['runId'],
            'profileSha256': self.plan['profileSha256'], 'observedUtcMs': self.now, 'disposition': 'active', 'revokedUtcMs': None,
            'availableBytes': 10 * capacity.GIB, 'competingBytes': 0, 'reservedBytes': capacity.GIB,
            'allocations': {'backup': capacity.GIB}}
        self.sign(self.plan['reservations']['backup']['path'], self.provider)
        return config

    def test_explicit_s3_identity_and_admission_preserve_legacy_identity_without_reading_secrets(self):
        legacy = deepcopy(self.plan)
        config = self.s3_plan()
        self.assertNotEqual(self.legacy_identity, calibration.identity(self.plan))
        self.assertEqual(self.legacy_identity, calibration.identity(legacy))
        actual = lifecycle.read_private
        def no_values(path, *args, **kwargs):
            self.assertNotIn(str(path), [config[r]['credentials']['path'] for r in s3.IDENTITIES])
            return actual(path, *args, **kwargs)
        with patch.object(lifecycle, 'read_private', side_effect=no_values):
            self.assertEqual(self.plan, calibration.load_plan(self.plan_ref['path'], self.plan_ref['sha256']))
            records = calibration.reservations(self.plan, self.now)
        self.assertEqual(self.provider, records['backup'])
        with self.assertRaises(ValueError):
            capacity.validate_bound(self.plan, now_ms=self.now)

    def test_configuration_wrong_destination_key_identity_bounds_and_extra_fields_refuse_before_command(self):
        self.s3_plan()
        original = deepcopy(self.plan['s3Backup'])
        for changes in ({'endpoint': 'http://fixture.invalid'}, {'bucket': 'hexalith-velero-backups'}, {'region': 'foreign'},
            {'projectId': 'foreign'}, {'key': 'memories/calibration/foreign.dump'}, {'extra': True}, {'curlVersion': '8.17.0'},
            {'maximumArchiveBytes': capacity.GIB}, {'requestTimeoutSeconds': 301}, {'maximumArchiveBytes': True},
            {'writer': original['validator']}):
            self.plan['s3Backup'] = {**deepcopy(original), **changes}
            with self.subTest(changes=changes), patch.object(Qualification, 'run', side_effect=AssertionError('no command')):
                with self.assertRaises(ValueError):
                    s3.configuration(self.plan)

    def test_missing_unsigned_reused_stale_revoked_mismatched_provider_reservation_refuses(self):
        self.s3_plan()
        path = self.plan['reservations']['backup']['path']
        for changes in ({'runId': 'b' * 32}, {'profileSha256': '0' * 64}, {'key': 'memories/reused.dump'},
            {'observedUtcMs': self.now - 300001}, {'disposition': 'withdrawn'}, {'revokedUtcMs': self.now},
            {'reservedBytes': capacity.GIB - 1}, {'availableBytes': 0}, {'failureDomain': 'domain-primary'}, {'owner': 'foreign'}):
            self.sign(path, {**self.provider, **changes})
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                calibration.reservations(self.plan, self.now)
        lifecycle.atomic(path, self.provider)
        with self.assertRaisesRegex(ValueError, 'signed-input'):
            calibration.reservations(self.plan, self.now)
        Path(path).unlink()
        with self.assertRaises(OSError):
            calibration.reservations(self.plan, self.now)

    def test_credentials_are_data_only_and_reject_duplicate_unknown_shell_and_wrong_project_or_region(self):
        self.s3_plan()
        for raw in (b'AWS_ACCESS_KEY_ID=$(touch /tmp/never)\n', b'UNKNOWN=secret\n',
            b'AWS_ACCESS_KEY_ID=FICTIONALWRITER01\nAWS_ACCESS_KEY_ID=duplicate\nAWS_SECRET_ACCESS_KEY=fictional-secret-11111\n',
            b'SCW_ACCESS_KEY=FICTIONALWRITER01\nSCW_SECRET_KEY=fictional-secret-11111\nSCW_DEFAULT_PROJECT_ID=foreign\n',
            b'SCW_ACCESS_KEY=FICTIONALWRITER01\nSCW_SECRET_KEY=fictional-secret-11111\nSCW_DEFAULT_REGION=foreign\n',
            b'AWS_ACCESS_KEY_ID=FICTIONALWRITER01\nAWS_SECRET_ACCESS_KEY=fictional-secret-11111\nSCW_ACCESS_KEY=ambiguous\n'):
            ref = self.write('parser.env', raw)
            with self.subTest(raw=raw), self.assertRaises(ValueError):
                s3.credentials(ref)
        ref = self.write('parser.env', b'# comment\nexport AWS_ACCESS_KEY_ID="FICTIONALWRITER01"\nAWS_SECRET_ACCESS_KEY=\'fictional-secret-11111\'\nAWS_REGION=fr-par\n')
        self.assertEqual(('FICTIONALWRITER01', 'fictional-secret-11111'), s3.credentials(ref))
        self.write('parser.env', b'changed')
        with self.assertRaises(ValueError):
            s3.credentials(ref)

    def test_aliased_unsafe_and_stale_credential_references_refuse(self):
        config = self.s3_plan()
        original = deepcopy(config)
        config['validator']['credentials'] = config['writer']['credentials']
        with self.assertRaisesRegex(ValueError, 'credential-alias'):
            s3.configuration(self.plan)
        config.update(deepcopy(original))
        alias = self.root / 'hardlink.env'
        os.link(config['writer']['credentials']['path'], alias)
        with self.assertRaisesRegex(ValueError, 'credential-custody'):
            s3.configuration(self.plan)
        alias.unlink()
        Path(config['writer']['credentials']['path']).chmod(0o644)
        with self.assertRaises(ValueError):
            s3.configuration(self.plan)

    def transaction(self, fault=None):
        self.s3_plan()
        q = self.runner(); self.start(q)
        q.record.update(measurementQuiesced=True, measurementQuiescence={'proof': {'state': 'verified'}},
            resources=[{'id': 'Pod/' + q.plan['target']['namespace'] + '/access-telemetry-postgresql-0', 'uid': 'fictional-pod-uid'}])
        q.record['storageObservation'] = {'allocations': {'host-workspace': {'usedBytes': 0, 'availableBytes': capacity.GIB}}}
        q.record['storageObservation']['allocations']['backup'] = {'usedBytes': 0, 'availableBytes': capacity.GIB}
        q.save()
        self.payload = b'PGDMP\x00\xff\xfe\x80\r\n\x00' + bytes(range(256)) * 32
        self.calls = []; self.remote = self.root / 'fictional-remote'; self.remote.mkdir(mode=0o700)
        self.puts = 0
        def kube(*args, **kwargs):
            self.calls.append(('kube', args))
            if args[0] == 'get':
                return subprocess.CompletedProcess([], 0, json.dumps({'metadata': {'namespace': q.plan['target']['namespace'],
                    'name': 'access-telemetry-postgresql-0', 'uid': 'foreign' if fault == 'pod' else 'fictional-pod-uid'},
                    'spec': {'containers': [{'name': 'postgresql', 'image': 'docker.io/library/postgres:18.6-trixie@sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722'}]}}), '')
            if 'pg_dump' in args:
                lifecycle.atomic(kwargs['stdout_file'], self.payload, exclusive=True)
                return subprocess.CompletedProcess([], 0, '', '')
            self.assertIn('pg_restore', args)
            self.assertEqual(self.payload, Path(kwargs['data_file']).read_bytes())
            if fault == 'archive':
                raise RuntimeError('fictional-native-invalid-archive')
            if '--file=/dev/null' in args:
                if fault == 'archive-data':
                    raise RuntimeError('fictional-native-invalid-data-with-intact-toc')
                return subprocess.CompletedProcess([], 0, '', '')
            return subprocess.CompletedProcess([], 0, 'fictional native archive TOC', '')
        def run(args, **kwargs):
            self.calls.append(('curl', args, kwargs.get('data')))
            if '--version' in args:
                return subprocess.CompletedProcess([], 0, 'curl 8.18.0 fixture\nProtocols: https', '')
            data = kwargs['data']; url = args[-1]
            self.assertNotIn('FICTIONAL', ' '.join(args))
            body = b''; extra = {}
            if '?versioning' in url:
                body = ('<VersioningConfiguration><Status>' + ('Suspended' if fault == 'bucket-versioning' else 'Enabled') + '</Status></VersioningConfiguration>').encode()
            elif '?object-lock' in url:
                body = ('<ObjectLockConfiguration><ObjectLockEnabled>' + ('Disabled' if fault == 'bucket-lock' else 'Enabled')
                    + '</ObjectLockEnabled><Rule><DefaultRetention><Mode>' + ('GOVERNANCE' if fault == 'bucket-governance' else 'COMPLIANCE')
                    + '</Mode>' + ('<Years>1</Years>' if fault == 'bucket-years' else '<Days>'
                    + ('29' if fault == 'bucket-short-retention' else '30') + '</Days>')
                    + '</DefaultRetention></Rule></ObjectLockConfiguration>').encode()
            elif '?encryption' in url:
                body = ('<ServerSideEncryptionConfiguration><SSEAlgorithm>' + ('aws:kms' if fault == 'bucket-encryption' else 'AES256') + '</SSEAlgorithm></ServerSideEncryptionConfiguration>').encode()
            elif '?versions' in url:
                body = b'<ListVersionsResult><IsTruncated>false</IsTruncated>' + (b'<Version/>' if fault == 'reused' else b'') + b'</ListVersionsResult>'
            elif '?retention' in url:
                body = ('<Retention><Mode>' + ('GOVERNANCE' if fault == 'retention' else 'COMPLIANCE') +
                    '</Mode><RetainUntilDate>' + (lifecycle.utc(self.now + 29 * 86400000) if fault == 'short-retention'
                    else q.record['s3Backup']['uploadIntent']['retainUntilUtc']) + '</RetainUntilDate></Retention>').encode()
            elif '--upload-file' in args:
                self.puts += 1
                self.assertIn('uploadIntent', lifecycle.strict(q.state_path.read_bytes())['s3Backup'])
                self.assertIn('If-None-Match: *', data)
                self.assertIn('Content-MD5:', data)
                self.assertIn('x-amz-content-sha256: ' + hashlib.sha256(self.payload).hexdigest(), data)
                self.assertIn('x-amz-server-side-encryption: AES256', data)
                self.assertIn('x-amz-meta-plan-sha256: ' + q.record['planSha256'], data)
                payload = Path(args[args.index('--upload-file') + 1]).read_bytes()
                lifecycle.atomic(self.remote / 'immutable-version-1', payload, exclusive=True)
                if fault == 'uncertain':
                    raise RuntimeError('fictional-timeout-after-remote-write')
                extra = {'x-amz-version-id': '' if fault == 'empty-version' else 'immutable-version-1'}
            else:
                self.assertIn('?versionId=immutable-version-1', url)
                self.assertIn('FICTIONALVALIDATOR02:', data)
                body = (self.remote / 'immutable-version-1').read_bytes()
                if fault == 'content':
                    body = body[:-1] + b'x'
                extra = {'x-amz-version-id': 'other' if fault == 'version' else 'immutable-version-1',
                    'x-amz-server-side-encryption': 'foreign' if fault == 'encryption' else 'AES256',
                    'content-length': str(len(self.payload) + (1 if fault == 'size' else 0)),
                    'x-amz-meta-run-id': 'foreign' if fault == 'run' else q.plan['runId'],
                    'x-amz-meta-profile-sha256': '0' * 64 if fault == 'profile' else q.plan['profileSha256'],
                    'x-amz-meta-plan-sha256': '0' * 64 if fault == 'plan' else q.record['planSha256'],
                    'x-amz-meta-content-sha256': '0' * 64 if fault == 'checksum' else hashlib.sha256(self.payload).hexdigest(),
                    'x-amz-meta-project-id': 'foreign' if fault == 'project' else s3.DESTINATION['projectId']}
            output = Path(args[args.index('--output') + 1])
            output.write_bytes(body)
            lifecycle.atomic(kwargs['stdout_file'], ('HTTP/1.1 200 OK\r\n' + ''.join(k + ': ' + v + '\r\n' for k, v in extra.items()) + '\r\n').encode(), exclusive=True)
            return subprocess.CompletedProcess([], 0, '', '')
        stack = ExitStack(); self.addCleanup(stack.close)
        for name in ('admission', 'observe', 'check_storage', 'guard'):
            stack.enter_context(patch.object(q, name))
        stack.enter_context(patch.object(q, 'kube', side_effect=kube))
        stack.enter_context(patch.object(q, 'run', side_effect=run))
        return q, s3.Backup(q)

    def test_real_private_file_end_state_binary_fidelity_immutable_identity_and_no_restore_credit(self):
        q, backup = self.transaction()
        receipt = backup.execute()
        self.assertEqual(1, self.puts)
        self.assertTrue(s3.verified(q))
        self.assertEqual(self.payload, (self.remote / 'immutable-version-1').read_bytes())
        self.assertEqual(self.payload, (backup.directory / 'postgresql.dump').read_bytes())
        self.assertEqual(hashlib.sha256(self.payload).hexdigest(), receipt['sha256'])
        self.assertFalse(any(receipt[k] for k in ('qualified', 'restoreCredit', 'C1Credit', 'C2Credit', 'ProductionCredit', 'A41Credit')))
        for path in backup.directory.iterdir():
            self.assertEqual(0o600, path.stat().st_mode & 0o777)
        self.assertNotIn('fictional-writer-secret', q.state_path.read_text())
        self.assertNotIn('fictional-validator-secret', (q.evidence / 's3-backup-verification.json').read_text())
        with self.assertRaisesRegex(ValueError, 'consumed-or-uncertain'):
            s3.Backup(q).execute()
        self.assertEqual(1, self.puts)

    def test_failed_or_uncertain_upload_readback_properties_retention_and_native_archive_never_pass(self):
        # Fresh private fixture per failure; a failed attempt is never reset.
        for fault in ('pod', 'archive', 'archive-data', 'reused', 'uncertain', 'empty-version', 'version', 'content', 'encryption',
            'retention', 'short-retention', 'run', 'profile', 'plan', 'size', 'checksum', 'project'):
            with self.subTest(fault=fault):
                case = S3BackupTests(); case.setUp()
                try:
                    q, backup = case.transaction(fault)
                    with self.assertRaises((ValueError, RuntimeError)):
                        backup.execute()
                    self.assertFalse(s3.verified(q))
                    self.assertFalse((q.evidence / 'calibration-result.json').exists())
                    self.assertFalse((q.evidence / 's3-backup-verification.json').exists())
                    self.assertEqual('retain-primary-and-staging', q.record['s3Backup']['disposition'])
                    with self.assertRaisesRegex(ValueError, 'consumed-or-uncertain'):
                        s3.Backup(q).execute()
                    self.assertLessEqual(case.puts, 1)
                finally:
                    case.doCleanups()

    def test_bucket_security_refusals_retain_unverified_custody_without_upload(self):
        for fault in ('bucket-versioning', 'bucket-lock', 'bucket-governance', 'bucket-short-retention',
            'bucket-years', 'bucket-encryption'):
            case = S3BackupTests(); case.setUp()
            try:
                q, backup = case.transaction(fault)
                with self.subTest(fault=fault), self.assertRaises(ValueError):
                    backup.execute()
                self.assertEqual(0, case.puts)
                self.assertEqual([], list(case.remote.iterdir()))
                self.assertFalse((q.evidence / 's3-backup-verification.json').exists())
                self.assertFalse(s3.verified(q))
                self.assertEqual('retain-primary-and-staging', q.record['s3Backup']['disposition'])
            finally:
                case.doCleanups()

    def test_additive_remote_headroom_refuses_before_capture_and_before_put(self):
        for exhausted_at in ('capture', 'put'):
            case = S3BackupTests(); case.setUp()
            try:
                q, backup = case.transaction()
                allocation = q.plan['allocations']['backup']
                checks = []
                def observe_storage():
                    checks.append(True)
                    pending = backup.config['maximumArchiveBytes'] if exhausted_at == 'capture' else len(case.payload)
                    used = allocation * 4 // 5 - pending + 1 if exhausted_at == 'capture' or len(checks) == 2 else 0
                    q.record['storageObservation']['allocations']['backup'] = {'usedBytes': used, 'availableBytes': allocation - used}
                with patch.object(q, 'check_storage', side_effect=observe_storage), self.assertRaisesRegex(ValueError, 'remote-backup-headroom'):
                    backup.execute()
                self.assertEqual(0, case.puts)
                self.assertEqual([], list(case.remote.iterdir()))
                self.assertEqual(exhausted_at == 'put', (backup.directory / 'postgresql.dump').exists())
                self.assertFalse((q.evidence / 's3-backup-verification.json').exists())
                self.assertEqual('retain-primary-and-staging', q.record['s3Backup']['disposition'])
            finally:
                case.doCleanups()

    def test_rehashed_incomplete_or_mismatched_receipts_never_authorize_primary_removal(self):
        q, backup = self.transaction(); backup.execute()
        ref = q.record['s3Backup']['receipt']
        original = lifecycle.strict(lifecycle.read_private(ref['path'], ref['sha256']))
        changes = [{field: None} for field in original]
        changes += [{'size': original['size'] + 1}, {'size': True}, {'sha256': '0' * 64}, {'contentMd5': 'invalid'},
            {'encryption': 'aws:kms'}, {'retentionMode': 'GOVERNANCE'}, {'retainUntilUtc': lifecycle.utc(self.now)},
            {'qualified': True}, {'restoreCredit': True}, {'nativeArchive': {**original['nativeArchive'], 'nativeArchiveDecodeVerified': False}},
            {'nativeArchive': {**original['nativeArchive'], 'podUid': 'foreign'}}, {'extra': 'unbound'}]
        for patch_value in changes:
            value = {**deepcopy(original), **patch_value}
            # None variants remove required fields rather than changing their type.
            value = {k: v for k, v in value.items() if v is not None}
            lifecycle.atomic(ref['path'], value); ref['sha256'] = lifecycle.sha(ref['path'])
            with self.subTest(changes=patch_value):
                self.assertFalse(s3.verified(q))
        lifecycle.atomic(ref['path'], original); ref['sha256'] = lifecycle.sha(ref['path'])
        self.assertTrue(s3.verified(q))

    def test_foreign_plan_verification_receipt_cannot_authorize_primary_removal(self):
        q, backup = self.transaction()
        backup.execute()
        ref = q.record['s3Backup']['receipt']
        value = lifecycle.strict(lifecycle.read_private(ref['path'], ref['sha256']))
        value['planSha256'] = '0' * 64
        lifecycle.atomic(ref['path'], value)
        ref['sha256'] = lifecycle.sha(ref['path'])
        self.assertFalse(s3.verified(q))

    def test_lost_authority_exhausted_budget_and_aliased_actual_credentials_deny_before_remote(self):
        for fault in ('authority', 'budget', 'credentials'):
            case = S3BackupTests(); case.setUp()
            try:
                q, backup = case.transaction()
                if fault == 'credentials':
                    ref = case.write('validator.env', Path(q.plan['s3Backup']['writer']['credentials']['path']).read_bytes())
                    q.plan['s3Backup']['validator']['credentials'] = ref
                changed = patch.object(q, 'admission', side_effect=ValueError('fictional-withdrawn')) if fault == 'authority' else patch.object(q, 'remaining', return_value=0) if fault == 'budget' else ExitStack()
                with changed, self.assertRaises(ValueError):
                    backup.execute()
                self.assertEqual([], case.calls)
            finally:
                case.doCleanups()

    def test_failed_backup_primary_retention_uses_original_stop_budget_and_never_requests_remove(self):
        self.s3_plan(); q = self.runner(); self.start(q)
        q.record['s3Backup'] = {'state': 'unverified', 'verified': False}
        def stopped(*, remove):
            self.assertFalse(remove)
            q.terminal('failed', 'fictional-backup-failure')
            q.record['cleanup'] = {'unverified': False, 'disposition': 'stopped'}
        with patch.object(lifecycle.CampaignQualification, 'stop', side_effect=stopped):
            q.stop(remove=True)
            original = deepcopy(q.record['stopping'])
            q.stop(remove=True)
        self.assertEqual(original, q.record['stopping'])
        self.assertTrue(q.record['cleanup']['unverified'])
        self.assertIn('primary-and-private-staging', q.record['cleanup']['persistentData'])

    def test_inherited_cleanup_failure_preserves_saved_backup_disposition(self):
        self.s3_plan(); q = self.runner(); self.start(q)
        failure = RuntimeError('fictional-inherited-stop-failure')
        def stop(*, remove):
            on_disk = lifecycle.strict(q.state_path.read_bytes())['cleanup']
            self.assertEqual(verification, on_disk['backupVerified'])
            self.assertEqual(verification, remove)
            q.record['cleanup'] = {'unverified': True, 'disposition': 'incomplete'}
            raise failure
        for verification in (False, True):
            with patch.object(s3, 'verified', return_value=verification), \
                 patch.object(lifecycle.CampaignQualification, 'stop', side_effect=stop), \
                 self.assertRaises(RuntimeError) as caught:
                q.stop(remove=True)
            self.assertIs(failure, caught.exception)
            retained = lifecycle.strict(q.state_path.read_bytes())['cleanup']
            self.assertEqual(verification, retained['backupVerified'])
            self.assertEqual('immutable-version-retained' if verification else 'unverified-primary-and-staging-retained', retained['backupDisposition'])
            if not verification:
                self.assertIn('primary-and-private-staging', retained['persistentData'])

    def test_binary_process_sink_and_input_preserve_bytes_bound_size_and_never_log_archive(self):
        q = self.runner(); self.start(q)
        payload = bytes(range(256)) * 8
        source = self.root / 'source.bin'; lifecycle.atomic(source, payload)
        target = self.root / 'output.bin'
        result = q.run([sys.executable, '-c', 'import sys; sys.stdout.buffer.write(sys.stdin.buffer.read())'],
            data_file=source, stdout_file=target, maximum_output_bytes=len(payload), timeout=5)
        self.assertEqual('', result.stdout)
        self.assertEqual(payload, target.read_bytes())
        self.assertEqual(0o600, target.stat().st_mode & 0o777)
        self.assertNotIn('source.bin', (q.evidence / q.record['commands'][-1]['log']).read_text())
        with self.assertRaises(FileExistsError):
            q.run([sys.executable, '-c', 'raise AssertionError()'], stdout_file=target, timeout=5)
        q.record['inFlight'] = None
        bounded = self.root / 'bounded.bin'
        with self.assertRaisesRegex(RuntimeError, 'output-limit'):
            q.run([sys.executable, '-c', 'import sys; sys.stdout.buffer.write(b"x"*1025)'], stdout_file=bounded,
                maximum_output_bytes=1024, timeout=5)
        self.assertLessEqual(bounded.stat().st_size, 1024)

    def test_binary_input_alone_redacts_echoed_content_and_setup_failures_close_files(self):
        q = self.runner(); self.start(q)
        source = self.root / 'source.bin'; lifecycle.atomic(source, b'fictional-sensitive-archive')
        result = q.run([sys.executable, '-c', 'import sys; print(sys.stdin.read())'], data_file=source, timeout=5)
        self.assertIn('fictional-sensitive-archive', result.stdout)
        self.assertNotIn('fictional-sensitive-archive', (q.evidence / q.record['commands'][-1]['log']).read_text())
        target = self.root / 'setup-output.bin'
        import ctypes
        from unittest.mock import Mock
        libc = Mock(); libc.prctl.return_value = 1
        with patch.object(ctypes, 'CDLL', return_value=libc), self.assertRaisesRegex(RuntimeError, 'supervision unavailable'):
            q.run([sys.executable, '-c', 'pass'], stdout_file=target, timeout=5)
        self.assertFalse(target.exists())
        q.record['inFlight'] = None
        failure = RuntimeError('fictional-spawn-error')
        with patch.object(subprocess, 'Popen', side_effect=failure), self.assertRaises(RuntimeError) as caught:
            q.run([sys.executable, '-c', 'pass'], data_file=source, stdout_file=target, timeout=5)
        self.assertIs(failure, caught.exception)
        for entry in Path('/proc/self/fd').iterdir():
            try:
                linked = os.readlink(entry)
            except FileNotFoundError:
                continue
            self.assertNotIn(linked, (str(source), str(target)))

    def test_replaced_credentials_deny_before_a_subsequent_remote_request(self):
        q, backup = self.transaction()
        backup.directory.mkdir(mode=0o700)
        backup.keys = {role: s3.credentials(backup.config[role]['credentials']) for role in s3.IDENTITIES}
        self.write('validator.env', b'SCW_ACCESS_KEY=FICTIONALCHANGED02\nSCW_SECRET_KEY=fictional-changed-secret-0002\n')
        with self.assertRaises(ValueError):
            backup.request('validator', 'GET', '?versioning')
        self.assertEqual([], self.calls)
        self.assertEqual([], list(backup.directory.iterdir()))

    def test_archive_hashing_rechecks_original_deadline_withdrawal_and_stop_between_chunks(self):
        import time
        q, backup = self.transaction()
        path = self.root / 'slow-archive.dump'; lifecycle.atomic(path, b'PGDMP' + b'x' * (3 * 1024 * 1024))
        opened = os.fdopen
        reads = []
        class SlowReader:
            def __init__(self, stream):
                self.stream = stream
            def __enter__(self):
                return self
            def __exit__(self, *args):
                return self.stream.__exit__(*args)
            def fileno(self):
                return self.stream.fileno()
            def read(self, length):
                raw = self.stream.read(length); reads.append(len(raw)); time.sleep(.035)
                return raw
        started = time.monotonic()
        with patch.object(q, 'remaining', side_effect=lambda: max(0, .05 - (time.monotonic() - started))), \
             patch.object(s3.os, 'fdopen', side_effect=lambda *a, **k: SlowReader(opened(*a, **k))), \
             self.assertRaisesRegex(ValueError, 'original-budget-exhausted'):
            s3.file_hashes(path, 4 * capacity.GIB, backup.authority)
        self.assertLess(time.monotonic() - started, .5)
        self.assertLessEqual(len(reads), 2)
        reads.clear()
        with patch.object(q, 'admission', side_effect=[None, None, ValueError('fictional-withdrawal')]), \
             patch.object(s3.os, 'fdopen', side_effect=lambda *a, **k: SlowReader(opened(*a, **k))), \
             self.assertRaisesRegex(ValueError, 'withdrawal'):
            s3.file_hashes(path, 4 * capacity.GIB, backup.authority)
        self.assertEqual(1, len(reads))
        lifecycle.atomic(q.evidence / 'stop-request.json', {'action': 'stop'})
        with self.assertRaises(InterruptedError):
            s3.file_hashes(path, 4 * capacity.GIB, backup.authority)
        self.assertEqual(0, self.puts)

    def test_executable_kubectl_fixture_forwards_binary_stdin_and_decodes_all_archive_data(self):
        for damaged in (False, True):
            case = S3BackupTests(); case.setUp()
            try:
                q, backup = case.transaction()
                # Remove only the transaction's kube/run mocks. Keep observation
                # and authority doubles; the actual existing process path runs.
                q.kube = calibration.CalibrationQualification.kube.__get__(q)
                q.run = calibration.CalibrationQualification.run.__get__(q)
                received = case.root / 'native-received'; received.mkdir(mode=0o700)
                original = case.payload
                archive = original[:-1] + b'!' if damaged else original
                pod = {'metadata': {'namespace': q.plan['target']['namespace'], 'name': 'access-telemetry-postgresql-0',
                    'uid': 'fictional-pod-uid'}, 'spec': {'containers': [{'name': 'postgresql',
                    'image': 'docker.io/library/postgres:18.6-trixie@sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722'}]}}
                script = '#!/usr/bin/python3\nimport json,os,pathlib,sys\n'
                script += 'args=sys.argv[1:]\noriginal=bytes.fromhex(' + repr(original.hex()) + ')\narchive=bytes.fromhex(' + repr(archive.hex()) + ')\n'
                script += 'received=pathlib.Path(' + repr(str(received)) + ')\n'
                script += 'if "get" in args:\n print(' + repr(json.dumps(pod)) + ')\n'
                script += 'elif "pg_dump" in args:\n sys.stdout.buffer.write(archive)\n'
                script += '''elif "pg_restore" in args:
 raw=sys.stdin.buffer.read() if "--stdin" in args else b""
 name="list" if "--list" in args else "decode"
 with os.fdopen(os.open(received/(name+".bin"),os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600),"wb") as stream: stream.write(raw)
 if not raw.startswith(b"PGDMP"): raise SystemExit(17)
 if name=="list": print("fictional intact archive TOC")
 elif raw!=original: raise SystemExit(18)
else: raise SystemExit(19)
'''
                executable = case.root / 'kubectl'; executable.write_text(script); executable.chmod(0o700)
                q.plan['tools']['kubectl'] = {'path': str(executable), 'sha256': lifecycle.sha(executable)}
                backup.directory.mkdir(mode=0o700)
                if damaged:
                    with self.assertRaises(RuntimeError):
                        backup.capture()
                else:
                    path, native = backup.capture()
                    self.assertEqual(original, path.read_bytes())
                    self.assertTrue(native['nativeArchiveDecodeVerified'])
                    self.assertEqual(hashlib.sha256(b'fictional intact archive TOC\n').hexdigest(), native['nativeArchiveListSha256'])
                    self.assertFalse(native['actualRestorePerformed'])
                self.assertEqual(archive, (received / 'list.bin').read_bytes())
                self.assertEqual(archive, (received / 'decode.bin').read_bytes())
                # Both commands passed through real kube -> run; list succeeds
                # even for damaged data, while full decode fails independently.
                returns = [entry['returncode'] for entry in q.record['commands']]
                self.assertEqual(18 if damaged else 0, returns[-1] if damaged else returns[-2])
                self.assertEqual(0, case.puts)
            finally:
                case.doCleanups()

    def test_binary_input_fifo_refuses_without_waiting_for_a_writer(self):
        q = self.runner(); self.start(q)
        fifo = self.root / 'input.fifo'; os.mkfifo(fifo, 0o600)
        import time
        started = time.monotonic()
        with self.assertRaisesRegex(ValueError, 'private binary command file'):
            q.run([sys.executable, '-c', 'raise AssertionError()'], data_file=fifo, timeout=2)
        self.assertLess(time.monotonic() - started, 1)

    def test_staging_headroom_quiescence_and_native_size_refuse_before_dump(self):
        q, backup = self.transaction()
        backup.directory.mkdir(mode=0o700)
        q.record['measurementQuiesced'] = False
        with self.assertRaisesRegex(ValueError, 'quiescence'):
            backup.capture()
        q.record['measurementQuiesced'] = True
        q.record['storageObservation']['allocations']['host-workspace']['usedBytes'] = capacity.GIB * 4 // 5
        with self.assertRaisesRegex(ValueError, 'staging-headroom'):
            backup.capture()
        self.assertEqual([], self.calls)
        path = backup.directory / 'excess.dump'; lifecycle.atomic(path, self.payload)
        with self.assertRaisesRegex(ValueError, 'archive-size'):
            s3.file_hashes(path, len(self.payload) - 1)

    def test_failed_backup_cannot_publish_measurement_completion_or_remove_primary(self):
        self.s3_plan(); q = self.runner(); self.start(q)
        q.record['physicalTarget'] = {'clusterUid': 'fictional-cluster'}
        q.plan['environment']['leaseHolders'] = ['calibration/owner-6775094/' + q.record['runId']]
        def kube(*args, **_):
            return subprocess.CompletedProcess([], 0, json.dumps({'items': [{'metadata': {'name': p}} for p in ('one', 'two')]}), '')
        def lane(name, pods):
            return {'lane': name, 'acceptedRecords': 150000 if name == 'backlog' else 450250,
                'commonOverlapMilliseconds': 1800000, 'retries': 0}
        stopped = []
        def stop(*, remove):
            stopped.append(remove)
            q.record['cleanup'] = {'unverified': False, 'disposition': 'stopped'}
        with patch.object(q, 'admission'), patch.object(q, 'enable'), patch.object(q, 'kube', side_effect=kube), \
             patch.object(q, 'physical', side_effect=[{'recordCount': n} for n in (0, 150000, 600250, 600250)]), \
             patch.object(q, 'operation_count', side_effect=[{'one': 0, 'two': 0}, {'one': 450125, 'two': 450125}]), \
             patch.object(q, 'lane', side_effect=lane), patch.object(q, 'measurement_quiesce', return_value={'state': 'verified'}), \
             patch.object(s3.Backup, 'execute', side_effect=RuntimeError('fictional-failed-backup')), \
             patch.object(q, 'capture_failure'), patch.object(lifecycle.CampaignQualification, 'stop', side_effect=stop):
            with self.assertRaisesRegex(RuntimeError, 'failed-backup'):
                q.supervise('monitor')
        self.assertEqual([False], stopped)
        self.assertFalse((q.evidence / 'calibration-result.json').exists())
        self.assertEqual('failed', q.record['terminal']['outcome'])
        self.assertTrue(q.record['cleanup']['unverified'])

    def test_redirect_duplicate_version_and_unsafe_xml_are_refused(self):
        for raw in (b'HTTP/1.1 302 Found\r\nLocation: https://foreign.invalid\r\n\r\n',
            b'HTTP/1.1 200 OK\r\nx-amz-version-id: one\r\nx-amz-version-id: two\r\n\r\n'):
            path = Path(self.write('headers.txt', raw)['path'])
            with self.assertRaises(ValueError):
                s3.headers(path, 200)
        for raw in (b'<!DOCTYPE x><x/>', b'<broken', b'x' * (s3.CONTROL_BYTES + 1)):
            with self.assertRaises(ValueError):
                s3.xml(raw)

    def test_native_curl_loopback_tls_uses_sigv4_sha256_ca_and_no_credential_argv(self):
        config = self.s3_plan(); q = self.runner(); self.start(q)
        certificate, key = self.root / 'loopback.pem', self.root / 'loopback.key'
        subprocess.run(['openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-keyout', str(key), '-out', str(certificate),
            '-days', '1', '-subj', '/CN=localhost', '-addext', 'subjectAltName=DNS:localhost'], check=True,
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=10)
        certificate.chmod(0o600); key.chmod(0o600)
        seen = []
        class Handler(BaseHTTPRequestHandler):
            def handle(self):
                try:
                    super().handle()
                except (ConnectionResetError, BrokenPipeError):
                    pass  # Expected client close after the TLS-name refusal.
            def do_GET(self):
                seen.append(dict(self.headers))
                self.send_response(200); self.send_header('Content-Length', '5'); self.end_headers(); self.wfile.write(b'proof')
            def do_PUT(self):
                seen.append(dict(self.headers))
                raw = self.rfile.read(int(self.headers['Content-Length']))
                lifecycle.atomic(self.server.archive_path, raw, exclusive=True)
                self.send_response(200); self.send_header('Content-Length', '0')
                self.send_header('x-amz-version-id', 'native-fixture-version'); self.end_headers()
            def log_message(self, *_):
                pass
        server = HTTPServer(('127.0.0.1', 0), Handler)
        server.archive_path = self.root / 'native-remote.dump'
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER); context.load_cert_chain(certificate, key)
        server.socket = context.wrap_socket(server.socket, server_side=True)
        thread = threading.Thread(target=server.serve_forever, daemon=True); thread.start()
        self.addCleanup(lambda: (server.shutdown(), server.server_close(), thread.join(2)))
        backup = s3.Backup(q); backup.directory.mkdir(mode=0o700)
        # Only the local fixture overrides the closed production destination.
        backup.config = {**config, 'endpoint': 'https://localhost:' + str(server.server_port),
            'ca': {'path': str(certificate), 'sha256': lifecycle.sha(certificate)}}
        backup.keys = {role: s3.credentials(config[role]['credentials']) for role in s3.IDENTITIES}
        with patch.object(q, 'admission'), patch.object(q, 'guard'):
            body, _ = backup.request('validator', 'GET', '?versioning')
            self.assertEqual(b'proof', body.read_bytes())
            self.assertIn('AWS4-HMAC-SHA256 Credential=FICTIONALVALIDATOR02/', seen[0]['Authorization'])
            self.assertEqual(hashlib.sha256(b'').hexdigest(), seen[0]['x-amz-content-sha256'])
            self.assertNotIn('FICTIONAL', json.dumps(q.record['commands']))
            archive = self.root / 'native-source.dump'; lifecycle.atomic(archive, b'PGDMP\x00\xff\x80\r\n')
            backup.archive = s3.file_hashes(archive, 1024)
            _, returned = backup.request('writer', 'PUT', object_key=True, payload=archive,
                extra={'If-None-Match': '*', 'Content-MD5': backup.archive['contentMd5'], 'x-amz-server-side-encryption': 'AES256'})
            self.assertEqual('native-fixture-version', returned['x-amz-version-id'])
            self.assertEqual(archive.read_bytes(), server.archive_path.read_bytes())
            self.assertEqual(backup.archive['sha256'], seen[1]['x-amz-content-sha256'])
            self.assertEqual('*', seen[1]['If-None-Match'])
            backup.config['endpoint'] = 'https://127.0.0.1:' + str(server.server_port)
            with self.assertRaises(RuntimeError):
                backup.request('validator', 'GET', '?versioning')
        self.assertEqual(2, len(seen))  # Hostname mismatch failed before HTTP.


if __name__ == '__main__':
    unittest.main()
