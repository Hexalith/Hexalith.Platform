import hashlib
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import telemetry_capture as c
from test_telemetry_postgres_adapter import fixture


class CapturePrimitivesTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.root.chmod(0o700)
        self.age = self.root / 'synthetic-age'
        self.age.write_text('#!' + sys.executable + '\nimport os,sys\nos.write(1,b"SYNTHETIC-ENCRYPTOR-PREFIX")\nwhile True:\n b=os.read(0,65536)\n if not b:break\n os.write(1,b)\n')
        self.age.chmod(0o700)

    def tearDown(self):
        self.temp.cleanup()

    def source(self, code):
        return [sys.executable, '-I', '-S', '-B', '-c', code]

    def test_snapshot_session_keeps_same_stdin_backend_open_until_explicit_rollback(self):
        code = "import sys,json,os\nfor line in sys.stdin:\n if line.startswith('ROLLBACK'):break\n print(json.dumps({'category':'snapshotBinding','payload':{'pid':os.getpid()}}),flush=True)\n"
        session = c.Session(self.source(code), timeout=5)
        try:
            session.send(b'SELECT snapshot;\n')
            value = session.until('snapshotBinding')
            self.assertEqual(value['payload']['pid'], session.child.pid)
            self.assertIsNone(session.child.poll())
            transcript = session.finish()
            self.assertIn(b'snapshotBinding', transcript)
            self.assertEqual(session.child.returncode, 0)
        finally:
            session.close()
        self.assertIsNotNone(session.child.poll())

    def test_premature_eof_cannot_claim_snapshot_holder_alive(self):
        session = c.Session(self.source('pass'), timeout=5)
        try:
            with self.assertRaisesRegex(ValueError, 'unexpected-eof'):
                session.until('snapshotBinding')
        finally:
            session.close()

    def test_stalled_or_oversized_session_stops_and_reaps(self):
        for code, limit, reason in [('import time;time.sleep(30)', 1024, 'deadline'),
                                     ('import os;os.write(1,b"x"*4096)', 128, 'output-bound')]:
            session = c.Session(self.source(code), timeout=.3)
            try:
                with patch.object(c, 'MAX_TRANSCRIPT', limit), self.assertRaisesRegex(ValueError, reason):
                    session.until('snapshotBinding', timeout=.1)
            finally:
                session.close()
            self.assertIsNotNone(session.child.poll())

    def test_binary_capture_stream_handles_backpressure_and_digest_without_plaintext_file(self):
        payload = bytes(range(256)) * 32768
        argv = self.source('import os\nb=bytes(range(256))*256\nfor _ in range(128):\n v=memoryview(b)\n while v:\n  n=os.write(1,v);v=v[n:]\n')
        path = self.root / 'artifact.age'
        record = c.encrypted_pipe(argv, self.age, ['synthetic-a', 'synthetic-b'], path, timeout=10)
        self.assertEqual(record['plaintextSha256'], hashlib.sha256(payload).hexdigest())
        self.assertEqual(record['plaintextBytes'], len(payload))
        self.assertEqual(record['ciphertextSha256'], hashlib.sha256(path.read_bytes()).hexdigest())
        self.assertEqual(path.read_bytes(), b'SYNTHETIC-ENCRYPTOR-PREFIX' + payload)
        self.assertEqual(path.stat().st_mode & 0o777, 0o600)
        self.assertEqual(set(p.name for p in self.root.iterdir()), {'artifact.age', 'synthetic-age'})

    def test_nonzero_source_is_refused_even_when_encryptor_exits_successfully(self):
        with self.assertRaisesRegex(ValueError, 'capture-subprocess-refused'):
            c.encrypted_pipe(self.source('import os;os.write(1,b"private-sentinel");raise SystemExit(9)'),
                             self.age, ['a', 'b'], self.root / 'failed.age', timeout=5)
        self.assertTrue((self.root / 'failed.age').is_file())

    def test_failed_dump_diagnostics_are_retained_without_plaintext_dump_and_primary_survives_cleanup_error(self):
        records=[]
        argv=self.source('import os;os.write(2,b"synthetic-dump-error");raise SystemExit(9)')
        with self.assertRaisesRegex(ValueError,'capture-subprocess-refused'):
            c.encrypted_pipe(argv,self.age,['a','b'],self.root/'diagnostic.age',timeout=5,
                record_private=lambda label,raw:(records.append((label,__import__('json').loads(raw))) or True))
        self.assertEqual(records[0][1]['sourceExitCode'],9)
        self.assertEqual(__import__('base64').b64decode(records[0][1]['sourceStderrBase64']),b'synthetic-dump-error')
        self.assertEqual(records[0][1]['plaintextBytesObserved'],0)
        original=c.terminate
        def broken_cleanup(child):original(child);raise RuntimeError('cleanup-after-reap')
        with patch.object(c,'terminate',broken_cleanup),self.assertRaises(BaseExceptionGroup) as caught:
            c.encrypted_pipe(argv,self.age,['a','b'],self.root/'cleanup.age',timeout=5)
        self.assertTrue(any(isinstance(v,ValueError) and 'capture-subprocess-refused' in str(v) for v in caught.exception.exceptions))
        self.assertTrue(any(isinstance(v,RuntimeError) for v in caught.exception.exceptions))

    def test_artifact_bound_stops_source_and_preserves_only_partial_ciphertext(self):
        with patch.object(c, 'MAX_ARTIFACT', 1024), self.assertRaisesRegex(ValueError, 'plaintext-bound'):
            c.encrypted_pipe(self.source('import os;os.write(1,b"x"*65536)'), self.age, ['a', 'b'],
                             self.root / 'oversized.age', timeout=5)
        self.assertFalse((self.root / 'plaintext').exists())

    def test_output_custody_and_create_only_checks_precede_process_dispatch(self):
        path = self.root / 'existing.age'
        path.write_bytes(b'preserved')
        with patch.object(c.subprocess, 'Popen') as popen, self.assertRaises(FileExistsError):
            c.encrypted_pipe(['unreviewed'], self.age, ['a', 'b'], path)
        popen.assert_not_called()
        self.assertEqual(path.read_bytes(), b'preserved')
        self.root.chmod(0o755)
        with patch.object(c.subprocess, 'Popen') as popen, self.assertRaisesRegex(ValueError, 'custody'):
            c.encrypted_pipe(['unreviewed'], self.age, ['a', 'b'], self.root / 'new.age')
        popen.assert_not_called()
        self.root.chmod(0o700)

    def input(self):
        value = fixture()
        value['transfer']['key'] = 'memories/20261031t175250z/telemetry-postgresql.dump.age'
        return value

    def test_absent_or_false_policy_quiescence_gate_cannot_start_a_snapshot(self):
        value = self.input()
        for outcome in (False, None):
            with patch.object(c, 'Session') as session, patch.object(c, 'encrypted_pipe') as pipe:
                with self.assertRaisesRegex(ValueError, 'policy-or-quiescence-refused'):
                    c.capture(value, [], self.age, ['a', 'b'], self.root / 'never.age',
                              lambda _: outcome, lambda: value['source'], lambda *args: True, lambda *args: True)
                session.assert_not_called()
                pipe.assert_not_called()

    def test_unverified_remote_precheck_cannot_start_a_snapshot(self):
        value = self.input()
        with patch.object(c, 'Session') as session:
            with self.assertRaisesRegex(ValueError, 'remote-session-precheck-refused'):
                c.capture(value, [], self.age, ['a', 'b'], self.root / 'never.age',
                          lambda _: True, lambda: value['source'], lambda *args: False, lambda *args: True)
            session.assert_not_called()

    def test_remote_holder_refusal_prevents_dump_and_requires_remote_release_after_local_close(self):
        value = self.input()
        sessions, phases = [], []
        class FakeSession:
            def __init__(self, argv):
                self.argv, self.closed = argv, False
                self.stdout,self.stderr=bytearray(),bytearray()
                self.child = type('Child', (), {'poll': lambda _: None})()
                sessions.append(self)
            def send(self, data):
                pass
            def until(self, category):
                payload = {'snapshotId': '00000003-0000001B-1', 'holderBackendPid': 1234}
                if category == 'tableCensus': payload = value['source']['tables']
                if category == 'snapshotTiming':
                    payload = {'transactionStartedAt':'2026-10-31T12:00:00+00:00',
                               'sourceObservedAt':'2026-10-31T12:00:01.123456+00:00'}
                if category == 'ownedSession':
                    role = 'holder' if self is sessions[0] else 'canary'
                    payload = {'pid': 1234 if role == 'holder' else 1235, 'backendStart': '2026-10-31 12:00:00+00',
                               'user': value['source']['user'], 'database': value['source']['database'],
                               'applicationName': 's427-tel-20261031t175250z-' + role}
                return {'category': category, 'payload': payload}
            def close(self):
                self.closed = True
        def guard(phase, names, owned):
            phases.append(phase)
            if phase == 'released':
                self.assertTrue(sessions and all(v.closed for v in sessions))
            return phase != 'alive'
        with patch.object(c, 'Session', FakeSession), patch.object(c, 'encrypted_pipe') as pipe:
            with self.assertRaisesRegex(ValueError, 'remote-session-liveness-refused'):
                c.capture(value, [], self.age, ['a', 'b'], self.root / 'never.age',
                          lambda _: True, lambda: value['source'], guard, lambda *args: True)
            pipe.assert_not_called()
        self.assertEqual(phases, ['before', 'alive', 'released'])
        self.assertEqual(len(sessions), 2)
        for session, role in zip(sessions, ('holder', 'canary')):
            self.assertIn('PGAPPNAME=s427-tel-20261031t175250z-' + role, session.argv)


if __name__ == '__main__':
    unittest.main()
