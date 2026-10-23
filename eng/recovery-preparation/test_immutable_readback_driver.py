#!/usr/bin/env python3
"""Raw immutable metadata, byte, identity and provider-failure cases; no network."""
import importlib.util
from dataclasses import replace
from pathlib import Path
import socket
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

_spec = importlib.util.spec_from_file_location('immutable_fixture_support', Path(__file__).with_name('_execution_boundary_fixtures.py'))
f = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = f
_spec.loader.exec_module(f)
b, a, driver = f.b, f.a, f.immutable


class ReadbackTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        directory = Path(self.temp.name)
        directory.chmod(0o700)
        self.boundary, self.provider, _, _ = f.compose(directory)
        self.driver = self.boundary.immutable_driver
        self.request = self.boundary.requests[0]
        self.credential = self.boundary.credentials[0]
        self.network = patch.object(socket.socket, 'connect', side_effect=AssertionError('external requests forbidden'))
        self.network.start()
        self.addCleanup(self.network.stop)

    def exchange(self):
        return self.driver.exchange(self.request, time.monotonic() + 2, self.credential)

    def test_exact_version_independent_raw_metadata_and_bytes_return_existing_typed_response(self):
        response = self.exchange()
        artifact = self.boundary.binding.policy
        self.assertEqual(response, a.Response(0, 200, self.provider.policy, artifact.version, artifact.retain_until))
        self.assertEqual(self.provider.calls[0][0], artifact)
        self.assertEqual(self.provider.calls[0][1].binding.principal, self.boundary.binding.validator)
        self.assertTrue(self.provider.handles[0].closed)

    def test_each_unknown_or_changed_metadata_field_stops_and_closes_handle(self):
        fields = {'store': 'wrong', 'key': 'wrong', 'version': 'latest', 'length': 1,
            'sha256': 'f' * 64, 'serverSideEncryption': 'aws:kms', 'objectLockMode': 'GOVERNANCE',
            'retainUntil': '2026-10-24T12:00:00Z', 'principal': self.boundary.binding.operator,
            'credentialIdentitySha256': 'f' * 64, 'credentialCustodySha256': 'f' * 64,
            'providerEndpoint': 'https://wrong.invalid', 'providerCaSha256': 'f' * 64}
        for name, value in fields.items():
            with self.subTest(field=name):
                def mutate(handle, metadata):
                    metadata[name] = value
                    handle.metadata = a.canonical(metadata)
                self.provider.mutate = mutate
                with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
                    self.exchange()
                self.assertTrue(self.provider.handles[-1].closed)
        for mode in ('missing', 'unknown', 'duplicate', 'boolean-length'):
            with self.subTest(mode=mode):
                def mutate(handle, metadata):
                    if mode == 'missing':
                        metadata.pop('serverSideEncryption')
                    elif mode == 'unknown':
                        metadata['authenticated'] = True
                    elif mode == 'boolean-length':
                        metadata['length'] = True
                    raw = a.canonical(metadata)
                    handle.metadata = raw[:-1] + b',"version":"duplicate"}' if mode == 'duplicate' else raw
                self.provider.mutate = mutate
                with self.assertRaises(b.Refusal):
                    self.exchange()
                self.assertTrue(self.provider.handles[-1].closed)

    def test_short_changed_overflow_and_nonbytes_content_stop_independent_of_metadata_claims(self):
        for mode in ('short', 'changed', 'overflow', 'nonbytes', 'oversized-chunk'):
            with self.subTest(mode=mode):
                def mutate(handle, metadata):
                    if mode == 'short':
                        handle.raw = handle.raw[:-1]
                    elif mode == 'changed':
                        handle.raw = b'x' * len(handle.raw)
                    elif mode == 'overflow':
                        handle.raw += b'x'
                    elif mode == 'nonbytes':
                        handle.read = lambda maximum, deadline: 'private diagnostic'
                    else:
                        handle.read = lambda maximum, deadline: b'x' * (maximum + 1)
                self.provider.mutate = mutate
                with self.assertRaises(b.Refusal):
                    self.exchange()
                self.assertTrue(self.provider.handles[-1].closed)

    def test_writer_principal_identity_custody_or_credential_substitution_refuses_before_provider(self):
        original = self.driver.provider_binding
        variants = (replace(original, writer_principal=original.reader.principal),
            replace(original, writer_identity_sha256=original.reader.identity_sha256),
            replace(original, writer_custody_sha256=original.reader.custody_sha256),
            replace(original, writer_secret_sha256=original.reader.secret_sha256),
            replace(original, reader=replace(original.reader, principal=original.writer_principal)))
        for value in variants:
            self.driver.provider_binding = value
            with self.assertRaises(b.Refusal):
                self.exchange()
            self.assertEqual(self.provider.calls, [])
        self.driver.provider_binding = original
        for cred in (f.credential(original.writer_principal, original.store), replace(self.credential, secret=b'changed')):
            with self.assertRaises(b.Refusal):
                self.driver.exchange(self.request, time.monotonic() + 1, cred)
            self.assertEqual(self.provider.calls, [])

    def test_unavailable_provider_and_provider_diagnostics_are_sanitized_without_writer_fallback(self):
        for provider in (None, driver.ImmutableProvider()):
            self.driver.provider = provider
            with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
                self.exchange()
        self.assertEqual(self.provider.calls, [])

    def test_expired_deadline_and_slow_drip_stop_and_release_owned_handle(self):
        with self.assertRaises(b.Refusal):
            self.driver.exchange(self.request, time.monotonic() - 1, self.credential)
        self.assertEqual(self.provider.calls, [])
        def mutate(handle, metadata):
            original = handle.read
            def read(maximum, deadline):
                time.sleep(.03)
                return original(maximum, deadline)
            handle.read = read
        self.provider.mutate = mutate
        started = time.monotonic()
        with self.assertRaises(b.Refusal):
            self.driver.exchange(self.request, time.monotonic() + .05, self.credential)
        self.assertLess(time.monotonic() - started, .3)
        self.assertTrue(self.provider.handles[-1].closed)

    def test_close_failure_prevents_returning_independently_checked_bytes(self):
        def mutate(handle, metadata):
            def close():
                raise OSError('private diagnostic')
            handle.close = close
        self.provider.mutate = mutate
        with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
            self.exchange()

    def test_restore_input_is_read_by_distinct_validator_after_authentication(self):
        directory = Path(self.temp.name)
        boundary, provider, _, _ = f.compose(directory, operation='restore')
        request = next(r for r in boundary.requests if r.name == 'recovery-input-readback')
        response = boundary.immutable_driver.exchange(request, time.monotonic() + 2, boundary.credentials[0])
        self.assertEqual(response.body, b'recovery')
        self.assertEqual(provider.calls[0][1].binding.principal, boundary.binding.validator)


    def test_policy_or_deadline_expiry_only_during_owned_close_refuses_checked_bytes(self):
        for mode in ('policy', 'deadline'):
            with self.subTest(mode=mode):
                current, expired = [f.NOW], [False]
                self.driver.now = lambda: current[0]
                future = time.monotonic() + 100
                def mutate(handle, metadata):
                    def close():
                        handle.closed = True
                        expired[0] = True
                        current[0] = a.timestamp(self.boundary.binding.expires_at)
                    handle.close = close
                self.provider.mutate = mutate
                with patch.object(b.time, 'monotonic', side_effect=lambda: future if expired[0] and mode == 'deadline' else future - 100):
                    with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
                        self.driver.exchange(self.request, future - 99, self.credential)
                self.assertTrue(expired[0])
                self.assertTrue(self.provider.handles[-1].closed)

    def test_policy_expired_before_open_refuses_without_provider_request(self):
        self.driver.now = lambda: a.timestamp(self.boundary.binding.expires_at)
        with self.assertRaises(b.Refusal):
            self.exchange()
        self.assertEqual(self.provider.calls, [])


    def test_read_interrupt_keeps_primary_cancellation_when_owned_close_also_interrupts(self):
        primary = KeyboardInterrupt('primary read cancellation')
        closes = []
        def mutate(handle, metadata):
            def read(maximum, deadline):
                raise primary
            def close():
                handle.closed = True
                closes.append(None)
                raise KeyboardInterrupt('secondary close cancellation')
            handle.read, handle.close = read, close
        self.provider.mutate = mutate
        with self.assertRaises(KeyboardInterrupt) as caught:
            self.exchange()
        self.assertIs(caught.exception, primary)
        self.assertEqual(len(closes), 1)
        self.assertTrue(self.provider.handles[-1].closed)

    def test_standalone_provider_budget_is_capped_to_request_and_policy_after_credential_work(self):
        from datetime import timedelta
        original = self.provider.open_exact
        for seconds in (3600, 2):
            with self.subTest(seconds=seconds):
                deadlines = []
                self.driver.now = lambda: a.timestamp(self.boundary.binding.expires_at) - timedelta(seconds=seconds)
                def open_exact(artifact, credential, deadline):
                    deadlines.append(deadline - time.monotonic())
                    return original(artifact, credential, deadline)
                with patch.object(self.provider, 'open_exact', side_effect=open_exact):
                    response = self.driver.exchange(self.request, time.monotonic() + 120, self.credential)
                self.assertEqual(response.body, self.provider.policy)
                self.assertEqual(len(deadlines), 1)
                self.assertTrue(0 < deadlines[0] <= min(self.request.timeout_seconds, seconds))
        current = [f.NOW]
        self.driver.now = lambda: current[0]
        descriptor = b.Credential.descriptor
        def custody(credential, expected):
            result = descriptor(credential, expected)
            current[0] = a.timestamp(self.boundary.binding.expires_at)
            return result
        calls = len(self.provider.calls)
        with patch.object(b.Credential, 'descriptor', custody), self.assertRaises(b.Refusal):
            self.driver.exchange(self.request, time.monotonic() + 120, self.credential)
        self.assertEqual(len(self.provider.calls), calls)


if __name__ == '__main__':
    unittest.main(verbosity=2)
