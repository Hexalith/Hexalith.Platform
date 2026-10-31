import hashlib
import io
import json
import os
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from datetime import datetime, timezone, timedelta
import publication as p


KEY = 'evidence/4-0/20261031t175250z/native-node/pre/snapshot.db.age'


class Client:
    def __init__(self, raw):
        self.raw = raw
        self.calls = []
        self.meta = SimpleNamespace(endpoint_url='https://s3.fr-par.scw.cloud', region_name='fr-par',
            config=SimpleNamespace(retries={'total_max_attempts': 1}))
        self.created = datetime.now(timezone.utc)
        self.retain = self.created + timedelta(days=30)
        self.version = 'actual-version'
        self.existing = False
        self.fail_put = False
        self.body = None

    def list_object_versions(self, **kwargs):
        return {'Versions': [{'Key': KEY}] if self.existing else []}

    def put_object(self, **kwargs):
        self.calls.append(kwargs)
        if self.fail_put:
            raise TimeoutError('uncertain transport outcome')
        assert kwargs['IfNoneMatch'] == '*'
        assert kwargs['Body'].read() == self.raw
        return {'VersionId': 'actual-version'}

    def head_object(self, **kwargs):
        return {'VersionId': self.version, 'ContentLength': len(self.raw), 'ServerSideEncryption': 'AES256',
            'LastModified': self.created, 'ObjectLockMode': 'COMPLIANCE', 'ObjectLockRetainUntilDate': self.retain}

    def get_object(self, **kwargs):
        self.body = io.BytesIO(self.raw)
        return {**self.head_object(**kwargs), 'Body': self.body}

    def get_object_retention(self, **kwargs):
        return {'Retention': {'Mode': 'COMPLIANCE', 'RetainUntilDate': self.retain}}


class PublicationTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.root.chmod(0o700)
        self.source = self.root/'source.age'
        self.raw = b'age-encryption.org/v1\nsynthetic-ciphertext'
        self.source.write_bytes(self.raw)
        self.source.chmod(0o600)
        self.output = self.root/'download.age'
        self.expected = {'bytes': len(self.raw), 'sha256': hashlib.sha256(self.raw).hexdigest()}
        self.client = Client(self.raw)
        self.records = []

    def tearDown(self):
        self.tmp.cleanup()

    def record(self, label, value):
        self.records.append((label, value))
        return True

    def run_publish(self, auth=lambda _: True, record=None, key=KEY):
        return p.publish(self.client, self.client, self.source, key, self.expected,
            self.output, auth, record or self.record)

    def test_actual_download_is_restore_input_and_closed(self):
        result = self.run_publish()
        self.assertEqual(self.output.read_bytes(), self.raw)
        self.assertEqual(result['versionId'], 'actual-version')
        self.assertEqual(result['readBy'], 'validator')
        self.assertTrue(self.client.body.closed)
        self.assertEqual(len(self.client.calls), 1)
        self.assertEqual([v[0] for v in self.records], ['put-intent', 'put-response', 'exact-version-download'])

    def test_uncertain_put_is_never_retried_and_intent_survives(self):
        self.client.fail_put = True
        with self.assertRaises(TimeoutError):
            self.run_publish()
        self.assertEqual(len(self.client.calls), 1)
        self.assertEqual([v[0] for v in self.records], ['put-intent'])

    def test_authentication_and_evidence_precede_mutation(self):
        with self.assertRaises(ValueError):
            self.run_publish(auth=lambda _: False)
        with self.assertRaises(ValueError):
            self.run_publish(record=lambda *_: False)
        self.assertEqual(self.client.calls, [])

    def test_existing_version_and_retry_profile_refused(self):
        self.client.existing = True
        with self.assertRaises(ValueError):
            self.run_publish()
        self.client.existing = False
        self.client.meta.config.retries['total_max_attempts'] = 2
        with self.assertRaises(ValueError):
            self.run_publish()
        self.assertEqual(self.client.calls, [])

    def test_wrong_version_or_hash_fails_with_closed_body(self):
        self.client.version = 'different-version'
        with self.assertRaises(ValueError):
            self.run_publish()
        self.assertTrue(self.client.body.closed)
        self.assertEqual(len(self.client.calls), 1)

    def test_insufficient_retention_fails_after_actual_readback(self):
        self.client.retain -= timedelta(microseconds=1)
        with self.assertRaises(ValueError):
            self.run_publish()
        self.assertTrue(self.client.body.closed)

    def test_no_secret_or_unapproved_path_and_no_symlink_source(self):
        with self.assertRaises(ValueError):
            self.run_publish(key=KEY.replace('native-node/pre', 'unapproved'))
        link = self.root/'link.age'
        link.symlink_to(self.source)
        self.source = link
        with self.assertRaises(ValueError):
            self.run_publish()
        self.assertEqual(self.client.calls, [])


if __name__ == '__main__':
    unittest.main()
