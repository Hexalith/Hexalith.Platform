"""Local-only supplemental controls; each invocation retains fresh report paths.

Default: only the supplemental cases. --original-verifier-controls-only reruns
the unchanged historical verifier matrix with its output redirected to a fresh file.
No credentials, remote requests or historical evidence replacements are permitted.
"""
import argparse
from contextlib import contextmanager, redirect_stdout
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import importlib.util
import io
import json
import os
from pathlib import Path
import socket
import stat
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import transfer_once as t
import verify_readback as v


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


executed = load('transfer_executed', 'transfer_once.executed.py')
REPORT = None
HISTORICAL = ['transfer_once.executed.py', 'plan.md', 'inspection.json', 'preflight.json',
              'preflight-initial.json', 'offline-checks.json', 'verifier-negative-checks.json',
              'receipt.json', 'upload-results.json', 'upload-started.json',
              'verify_readback.py', 'test_transfer_once.py', 'test_verify_readback.py']


@contextmanager
def upload_fixture():
    with tempfile.TemporaryDirectory(prefix='story-4-17-upload-fixture-') as tmp:
        directory = Path(tmp)
        (directory / 'plan.md').write_text('LOCAL TEST FIXTURE; NO EXECUTION APPROVAL')
        (directory / 'inspection.json').write_bytes(t.encode({
            'outcome': 'PASS', 'scriptSha256': t.sha(Path(t.__file__).read_bytes()),
            'planSha256': t.sha((directory / 'plan.md').read_bytes())}))
        (directory / 'preflight.json').write_bytes(t.encode({
            'outcome': 'PASS', 'transferId': t.TRANSFER, 'capturedAtUtc': t.utc()}))
        with patch.object(t, 'HERE', directory):
            yield directory


class Supplemental(unittest.TestCase):
    def test_marker_fsyncs_file_then_parent_directory(self):
        calls = []
        actual = os.fsync

        def observe(fd):
            calls.append('directory' if stat.S_ISDIR(os.fstat(fd).st_mode) else 'file')
            actual(fd)

        with tempfile.TemporaryDirectory() as tmp, patch.object(t, 'HERE', Path(tmp)), \
                patch.object(t.os, 'fsync', observe):
            t.save('marker.json', {'fixture': True})
        self.assertEqual(['file', 'directory'], calls)

    def test_pinned_sdk_redirect_resend_is_suppressed_per_api_invocation(self):
        from botocore.awsrequest import AWSResponse

        class Raw(io.BytesIO):
            def stream(self, **ignored):
                yield self.read()

        def transport(sends):
            def send(request):
                sends.append(request.method)
                if len(sends) == 1:
                    return AWSResponse(request.url, 301, {'x-amz-bucket-region': 'fr-par'},
                                       Raw(b'<Error><Code>PermanentRedirect</Code></Error>'))
                return AWSResponse(request.url, 200, {'x-amz-version-id': 'offline-version'}, Raw(b''))
            return send

        values = {'AWS_ACCESS_KEY_ID': 'offline-fixture', 'AWS_SECRET_ACCESS_KEY': 'offline-fixture'}
        args = {'Bucket': t.BUCKET, 'Key': t.PREFIX + 'offline-fixture', 'Body': b'fixture',
                'IfNoneMatch': '*', 'ServerSideEncryption': 'AES256'}
        old = executed.client(values)
        old_sends = []
        with patch.object(old._endpoint, '_send', transport(old_sends)):
            self.assertEqual('offline-version', old.put_object(**args)['VersionId'])
        self.assertEqual(['PUT', 'PUT'], old_sends)
        hardened = t.client(values)
        sends = []
        with patch.object(hardened._endpoint, '_send', transport(sends)):
            with self.assertRaisesRegex(ValueError, '^put-http-resend-refused$'):
                hardened.put_object(**args)
            self.assertEqual(['PUT'], sends)
            # A separate API invocation resets the guard; the redirected send did not.
            self.assertEqual('offline-version', hardened.put_object(**args)['VersionId'])
        self.assertEqual(['PUT', 'PUT'], sends)

    def test_pre_put_credential_failure_retains_sanitized_terminal_incident(self):
        with upload_fixture() as directory, \
                patch.object(t, 'credential', side_effect=RuntimeError('private fake diagnostic')), \
                patch.object(t, 'client') as make_client:
            with self.assertRaises(RuntimeError):
                t.upload()
            incident = json.loads((directory / 'upload-incident.json').read_bytes())
            self.assertTrue((directory / 'upload-started.json').exists())
            self.assertEqual('UNACCEPTED_STOP_NO_RETRY', incident['outcome'])
            self.assertEqual('before-first-put', incident['phase'])
            self.assertEqual(0, incident['putInvocations'])
            self.assertEqual((0, 0), (incident['intentCount'], incident['returnedVersionCount']))
            self.assertEqual('operation-failed-details-withheld', incident['reason'])
            self.assertFalse(incident['offSiteCustodyAccepted'])
            self.assertNotIn('private fake diagnostic', json.dumps(incident))
            make_client.assert_not_called()

    def test_pre_put_source_failure_retains_terminal_incident_without_put(self):
        @contextmanager
        def refused_sources():
            raise ValueError('source-commitment-mismatch')
            yield

        fake = unittest.mock.Mock()
        with upload_fixture() as directory, \
                patch.object(t, 'credential', lambda role: {'AWS_ACCESS_KEY_ID': role}), \
                patch.object(t, 'client', lambda ignored: fake), patch.object(t, 'sources', refused_sources):
            with self.assertRaisesRegex(ValueError, '^source-commitment-mismatch$'):
                t.upload()
            incident = json.loads((directory / 'upload-incident.json').read_bytes())
            self.assertEqual('source-commitment-mismatch', incident['reason'])
            self.assertFalse(incident['offSiteCustodyAccepted'])
            fake.put_object.assert_not_called()

    def test_after_intent_incident_preserves_counts_without_inventing_sends(self):
        def partial_journal():
            first, second, *_ = t.COMMITMENTS
            for name in (first, second):
                t.save('intent-' + name + '.json', {'fixture': True})
            t.save('returned-' + first + '.json', {'fixture': True})
            raise RuntimeError('local test failure')

        with upload_fixture() as directory, patch.object(t, '_upload_after_start', partial_journal):
            with self.assertRaises(RuntimeError):
                t.upload()
            incident = json.loads((directory / 'upload-incident.json').read_bytes())
            self.assertEqual('after-put-intent', incident['phase'])
            self.assertEqual((2, 1), (incident['intentCount'], incident['returnedVersionCount']))
            self.assertIsNone(incident['putInvocations'])
            self.assertIsNone(incident['httpTransportSends'])

    def test_correct_count_packet_with_wrong_artifact_digest_is_refused(self):
        payload = b'{}'
        packet = t.encode({'schemaVersion': 3, 'artifacts': {'payload.json': '0' * 64}})
        output = io.BytesIO()
        with zipfile.ZipFile(output, 'w') as archive:
            archive.writestr('payload.json', payload)
            archive.writestr('packet.json', packet)
        raw = output.getvalue()
        row = {'file': 'offline.zip', 'bytes': len(raw), 'sha256': t.sha(raw), 'entryCount': 2,
               'entries': [{'path': name, 'bytes': len(body), 'sha256': t.sha(body), 'directory': False}
                           for name, body in [('payload.json', payload), ('packet.json', packet)]],
               'packetPath': 'packet.json', 'packetSchemaVersion': 3, 'packetArtifactCount': 1,
               'authority': 'local test fixture'}
        self.assertEqual(row['packetArtifactCount'], len(json.loads(packet)['artifacts']))
        with patch.object(t, 'COMMITMENTS', {'offline.zip': (len(raw), t.sha(raw))}):
            with self.assertRaisesRegex(ValueError, '^packet-artifact-mismatch$'):
                t.inspect_archive(raw, row)
        with patch.object(v, 'EXPECTED', {'offline.zip': (len(raw), t.sha(raw))}):
            with self.assertRaisesRegex(v.Refused, '^packet-artifact-digest-mismatch$'):
                v.inspect_archive(raw, row)

    def test_governance_readback_generates_unaccepted_main_receipt_and_exit_one(self):
        sandbox = REPORT / 'governance-main-fixture'
        evidence = sandbox / 'evidence'
        evidence.mkdir(parents=True)
        (sandbox / 'artifacts').mkdir()
        for name in ('verifier-negative-checks.json', 'test_verify_readback.py', 'upload-results.json'):
            (evidence / name).write_bytes((HERE / name).read_bytes())
        preflight = json.loads((HERE / 'preflight.json').read_bytes())['protection']
        uploaded = json.loads((HERE / 'upload-results.json').read_bytes())['files']
        by_key = {row['key']: row for row in uploaded}
        staging = v.ROOT / 'artifacts/story-4-17-custody-staging-2026-11-14'
        bodies = {row['file']: (staging / row['file']).read_bytes() for row in uploaded}
        target = '13638347236.zip'
        at = datetime(2026, 11, 14, 2, tzinfo=timezone.utc)

        class FakeReadOnly:
            def __getattr__(self, name):
                if name == 'get_bucket_policy':
                    return lambda **ignored: {'Policy': json.dumps(preflight['bucketPolicy'])}
                return lambda **ignored: deepcopy(preflight['checks'][name])

            def head_object(self, **args):
                row = by_key[args['Key']]
                assert args['VersionId'] == row['versionId']
                return {'VersionId': row['versionId'], 'ContentLength': len(bodies[row['file']]),
                        'ServerSideEncryption': 'AES256', 'LastModified': at - timedelta(hours=1),
                        'ObjectLockMode': 'GOVERNANCE' if row['file'] == target else 'COMPLIANCE',
                        'ObjectLockRetainUntilDate': at - timedelta(hours=1) + timedelta(days=30)}

            def get_object(self, **args):
                row = by_key[args['Key']]
                return {**self.head_object(**args), 'Body': io.BytesIO(bodies[row['file']])}

            def get_object_retention(self, **args):
                head = self.head_object(**args)
                return {'Retention': {'Mode': head['ObjectLockMode'],
                                      'RetainUntilDate': head['ObjectLockRetainUntilDate']}}

        with patch.object(v, 'HERE', evidence), patch.object(v, 'ROOT', sandbox), \
                patch.object(v, 'make_client', lambda: FakeReadOnly()), patch.object(v, 'now', lambda: at), \
                redirect_stdout(io.StringIO()):
            code = v.main()
        receipt = json.loads((evidence / 'receipt.json').read_bytes())
        self.assertEqual(1, code)
        self.assertEqual('FAIL_CUSTODY_INCOMPLETE', receipt['outcome'])
        self.assertFalse(receipt['offSiteCustodyAccepted'])
        bad = next(row for row in receipt['files'] if row['file'] == target)
        self.assertFalse(bad['accepted'])
        self.assertEqual('object-retention-mode-inadequate', bad['failureCode'])
        self.assertEqual(4, sum(row['accepted'] for row in receipt['files']))


def original_verifier_controls(report_path):
    original = load('original_verifier_controls', 'test_verify_readback.py')
    save = v.save

    def fresh_save(path, value):
        if path != HERE / 'verifier-negative-checks.json':
            raise AssertionError('unexpected historical output path')
        save(report_path, value)

    with patch.object(v, 'save', fresh_save):
        return original.run()


def main():
    global REPORT
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-verifier-controls-only', action='store_true')
    args = parser.parse_args()
    REPORT = Path(tempfile.mkdtemp(prefix='story-4-17-post-review-'))
    before = {name: t.sha((HERE / name).read_bytes()) for name in HISTORICAL}
    # Explicit fake SDK credentials and fake transports above; accidental network
    # access fails before a socket can connect. Real credential readers are unused.
    with patch.object(socket.socket, 'connect', side_effect=AssertionError('network-forbidden')):
        if args.original_verifier_controls_only:
            code = original_verifier_controls(REPORT / 'original-verifier-controls.json')
            count = 25
        else:
            output = io.StringIO()
            suite = unittest.defaultTestLoader.loadTestsFromTestCase(Supplemental)
            result = unittest.TextTestRunner(stream=output, verbosity=2).run(suite)
            code, count = (0 if result.wasSuccessful() else 1), result.testsRun
            print(output.getvalue(), end='')
            (REPORT / 'test-output.txt').write_text(output.getvalue())
    after = {name: t.sha((HERE / name).read_bytes()) for name in HISTORICAL}
    if before != after:
        raise AssertionError('historical-evidence-changed')
    report = {'outcome': 'PASS' if code == 0 else 'FAIL', 'tests': count,
              'scope': 'LOCAL ONLY; fake transport/receipts are not live custody evidence',
              'historicalFilesUnchanged': before, 'realNetworkRequests': 0,
              'transferCodeSha256': t.sha(Path(t.__file__).read_bytes()),
              'supplementalRunnerSha256': t.sha(Path(__file__).read_bytes()),
              'reportDirectory': str(REPORT)}
    v.save(REPORT / 'supplemental-report.json', report)
    print(json.dumps(report, indent=2))
    return code


if __name__ == '__main__':
    raise SystemExit(main())
