"""Offline failure matrix for the exact one-off transfer; no network or credentials."""
from contextlib import contextmanager
import copy
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

SPEC = importlib.util.spec_from_file_location('transfer_once', Path(__file__).with_name('transfer_once.py'))
t = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(t)


class OfflineMatrix(unittest.TestCase):
    def test_original_sources_full_inventory_and_private_exclusions(self):
        with t.sources() as (files, manifest, rows):
            self.assertEqual(5, len(files))
            self.assertEqual((2215, 2059), (sum(r['entryCount'] for r in rows),
                                           sum(r['packetArtifactCount'] for r in rows)))
            self.assertFalse(manifest['offSiteCustodyAccepted'])

    def test_private_signatures_block_without_exposing_values(self):
        for raw in [b'-----BEGIN PRIVATE KEY-----', b'ghp_' + b'a' * 36,
                    b'AKIA' + b'A' * 16, b'hvs.' + b'a' * 25,
                    b'AWS_SECRET_ACCESS_KEY=offline-canary']:
            with self.subTest(raw_sha256=t.sha(raw)), self.assertRaisesRegex(ValueError, 'private-material-signature'):
                t.private_check('receipt.json', raw)

    def test_private_and_unsafe_paths_block(self):
        for name in ['/absolute', '../escape', 'x/../escape', 'x\\escape', 'private/log',
                     'private-transcripts/x', '.aws/credentials', 'cert.key', '.env', 'x\x00y']:
            with self.subTest(name=name), self.assertRaises(ValueError):
                t.safe_name(name)

    def test_source_byte_drift_blocks(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            os.chmod(path, 0o700)
            (path / next(iter(t.COMMITMENTS))).write_bytes(b'wrong')
            with self.assertRaisesRegex(ValueError, 'source-commitment-mismatch'):
                with t.sources(path):
                    pass

    def test_source_symlink_blocks(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            os.chmod(path, 0o700)
            (path / next(iter(t.COMMITMENTS))).symlink_to('/etc/hosts')
            with self.assertRaisesRegex(ValueError, 'source-file-custody'):
                with t.sources(path):
                    pass

    def test_descriptor_mutation_is_detected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'x'
            path.write_bytes(b'a')
            with path.open('rb') as source:
                before = os.fstat(source.fileno())
                path.write_bytes(b'changed')
                with self.assertRaisesRegex(ValueError, 'source-drift'):
                    t.stable(source.fileno(), before, path)

    def test_prefix_versions_delete_markers_or_truncation_block(self):
        for response in [{'Versions': [{'Key': t.PREFIX + 'x'}]}, {'DeleteMarkers': [{}]}, {'IsTruncated': True}]:
            fake = type('Fake', (), {'list_object_versions': lambda self, **kw: response})()
            with self.assertRaisesRegex(ValueError, 'destination-prefix-collision'):
                t.empty_prefix(fake)

    def test_current_protection_replay_rejects_drift(self):
        observed = json.loads(Path(__file__).with_name('preflight-initial.json').read_bytes())['protection']
        baseline = copy.deepcopy(observed['checks'])
        baseline['get_bucket_policy'] = {'Policy': json.dumps(observed['bucketPolicy'])}

        class Fake:
            def __getattr__(self, name):
                return lambda **kwargs: copy.deepcopy(self.rows[name])

        fake = Fake()
        fake.rows = baseline
        self.assertFalse(t.protections(fake)['evidenceLifecycleExpiry'])
        changes = [
            ('get_bucket_versioning', lambda r: r.update(Status='Suspended')),
            ('get_bucket_location', lambda r: r.update(LocationConstraint='other')),
            ('get_object_lock_configuration', lambda r: r['ObjectLockConfiguration']['Rule']['DefaultRetention'].update(Days=29)),
            ('get_bucket_encryption', lambda r: r['ServerSideEncryptionConfiguration']['Rules'][0]['ApplyServerSideEncryptionByDefault'].update(SSEAlgorithm='aws:kms')),
            ('get_bucket_lifecycle_configuration', lambda r: r['Rules'].append({'ID': 'evidence-expiry'})),
            ('get_bucket_acl', lambda r: r['Grants'].append({'Permission': 'READ'})),
            ('get_bucket_policy', lambda r: r.update(Policy=r['Policy'].replace('s3:PutObject', 's3:*'))),
        ]
        for name, change in changes:
            with self.subTest(control=name):
                fake.rows = copy.deepcopy(baseline)
                change(fake.rows[name])
                with self.assertRaises(ValueError):
                    t.protections(fake)

    def test_zip_schema_hash_and_symlink_failures(self):
        for case in ('unsafe-path', 'symlink', 'entry-hash', 'packet-schema', 'packet-artifact'):
            with self.subTest(case=case):
                packet = t.encode({'schemaVersion': 3, 'artifacts': {}})
                info = zipfile.ZipInfo('../escape' if case == 'unsafe-path' else 'payload.json')
                if case == 'symlink':
                    info.external_attr = 0o120777 << 16
                body = io.BytesIO()
                with zipfile.ZipFile(body, 'w') as archive:
                    archive.writestr(info, b'{}')
                    archive.writestr('packet.json', packet)
                raw = body.getvalue()
                row = {'file': 'offline.zip', 'bytes': len(raw), 'sha256': t.sha(raw), 'entryCount': 2,
                       'entries': [{'path': info.filename, 'bytes': 2, 'sha256': 'wrong' if case == 'entry-hash' else t.sha(b'{}'), 'directory': False},
                                   {'path': 'packet.json', 'bytes': len(packet), 'sha256': t.sha(packet), 'directory': False}],
                       'packetPath': 'packet.json', 'packetSchemaVersion': 2 if case == 'packet-schema' else 3,
                       'packetArtifactCount': 1 if case == 'packet-artifact' else 0, 'authority': 'offline'}
                with patch.object(t, 'COMMITMENTS', {'offline.zip': (len(raw), t.sha(raw))}), self.assertRaises(ValueError):
                    t.inspect_archive(raw, row)

    def test_exception_text_is_never_serialized(self):
        self.assertEqual('operation-failed-details-withheld', t.failure(RuntimeError('secret URL body')))
        self.assertEqual('operation-failed-details-withheld', t.failure(ValueError('secret URL body')))
        self.assertEqual('source-drift', t.failure(ValueError('source-drift')))

    def test_evidence_writes_are_exclusive(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(t, 'HERE', Path(directory)):
            t.save('intent.json', {'x': 1})
            with self.assertRaises(FileExistsError):
                t.save('intent.json', {'x': 2})

    def run_upload(self, behavior='pass', existing=False, inspection=True, collision=False):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        (root / 'plan.md').write_text('offline plan')
        t_review = {'outcome': 'PASS', 'scriptSha256': t.sha(Path(t.__file__).read_bytes()),
                    'planSha256': t.sha((root / 'plan.md').read_bytes()) if inspection else 'wrong'}
        (root / 'inspection.json').write_bytes(t.encode(t_review))
        (root / 'preflight.json').write_bytes(t.encode({'outcome': 'PASS', 'transferId': t.TRANSFER,
                                                       'capturedAtUtc': t.utc()}))
        if existing:
            (root / 'upload-started.json').write_text('existing')
        files = {name: {'fd': 0, 'before': None, 'path': root / name, 'raw': b'fixture'}
                 for name in t.COMMITMENTS}

        @contextmanager
        def fake_sources():
            yield files, {}, []

        class Fake:
            puts = []

            def list_object_versions(self, **kwargs):
                return {'Versions': [{}]} if collision else {}

            def put_object(self, **kwargs):
                self.puts.append(kwargs)
                if behavior == 'timeout':
                    raise TimeoutError('private raw SDK detail')
                return {'VersionId': 'null' if behavior == 'missing-version' else 'exact-version',
                        'ResponseMetadata': {'HTTPStatusCode': 200}}

        fake = Fake()
        # Byte commitments and source guards have separate real-file cases above.
        with patch.object(t, 'HERE', root), patch.object(t, 'sources', fake_sources), \
             patch.object(t, 'credential', lambda role: {'AWS_ACCESS_KEY_ID': role}), \
             patch.object(t, 'client', lambda _: fake), patch.object(t, 'stable'), \
             patch.object(t, 'protections'), patch.object(t, 'COMMITMENTS', {
                 name: (7, t.sha(b'fixture')) for name in files}):
            try:
                t.upload()
                error = None
            except Exception as caught:
                error = caught
        return root, fake, error

    def test_exactly_five_conditional_single_puts(self):
        root, fake, error = self.run_upload()
        self.assertIsNone(error)
        self.assertEqual(5, len(fake.puts))
        for row in fake.puts:
            self.assertEqual('*', row['IfNoneMatch'])
            self.assertEqual('AES256', row['ServerSideEncryption'])
            self.assertTrue(row['Key'].startswith(t.PREFIX))
        self.assertFalse(json.loads((root / 'upload-results.json').read_bytes())['offSiteCustodyAccepted'])

    def test_uncertain_put_never_retries_or_continues(self):
        root, fake, error = self.run_upload('timeout')
        self.assertEqual('put-uncertain-stop-no-retry', str(error))
        self.assertEqual(1, len(fake.puts))
        incident = next(root.glob('uncertain-*.json')).read_text()
        self.assertNotIn('private raw SDK detail', incident)
        self.assertFalse((root / 'upload-results.json').exists())

    def test_missing_returned_version_stops(self):
        root, fake, error = self.run_upload('missing-version')
        self.assertEqual('put-uncertain-stop-no-retry', str(error))
        self.assertEqual(1, len(fake.puts))
        self.assertFalse(list(root.glob('returned-*.json')))

    def test_started_attempt_cannot_restart(self):
        _, fake, error = self.run_upload(existing=True)
        self.assertIsInstance(error, FileExistsError)
        self.assertEqual([], fake.puts)

    def test_uninspected_bytes_block_all_puts(self):
        _, fake, error = self.run_upload(inspection=False)
        self.assertEqual('independent-inspection-required', str(error))
        self.assertEqual([], fake.puts)

    def test_collision_blocks_all_puts(self):
        _, fake, error = self.run_upload(collision=True)
        self.assertEqual('destination-prefix-collision', str(error))
        self.assertEqual([], fake.puts)


if __name__ == '__main__':
    unittest.main()
