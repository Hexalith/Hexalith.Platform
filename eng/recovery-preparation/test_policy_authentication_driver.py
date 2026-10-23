#!/usr/bin/env python3
"""Original-buffer trust/scope/window guards and bounded cryptographic verifier."""
import base64
import importlib.util
from dataclasses import replace
from pathlib import Path
import socket
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

_spec = importlib.util.spec_from_file_location('policy_fixture_support', Path(__file__).with_name('_execution_boundary_fixtures.py'))
f = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = f
_spec.loader.exec_module(f)
b, a, driver = f.b, f.a, f.policy_driver


class PolicyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)
        self.directory.chmod(0o700)
        self.boundary, self.provider, self.verifier, _ = f.compose(self.directory)
        self.driver = self.boundary.policy_driver
        self.network = patch.object(socket.socket, 'connect', side_effect=AssertionError('external requests forbidden'))
        self.network.start()
        self.addCleanup(self.network.stop)

    def request(self, binding=None, policy=None, signature=None):
        binding = self.boundary.binding if binding is None else binding
        request = a.build_requests(binding)[2]
        body = a.strict_json(request.stdin)
        body['exactReadbackBytes'] = {'policy': base64.b64encode(self.verifier.policy if policy is None else policy).decode(),
            'signature': base64.b64encode(self.verifier.signature if signature is None else signature).decode()}
        return replace(request, stdin=a.canonical(body), stdin_materialization=None)

    def exchange(self, request=None):
        return self.driver.exchange(self.request() if request is None else request, time.monotonic() + 3)

    def test_original_buffers_pinned_signer_namespace_and_exact_authentication_response(self):
        response = self.exchange()
        a._semantic(self.boundary.binding, self.request(), response, f.NOW)
        self.assertEqual(self.verifier.calls[0][2], self.verifier.policy)
        self.assertEqual(dict(self.verifier.calls[0][3])['@signature'], self.verifier.signature)
        self.assertEqual(self.verifier.calls[0][1][6:8], ('-n', 'hexalith-recovery'))
        self.assertNotIn('authenticated', a.strict_json(response.body))

    def test_changed_original_buffers_metadata_scope_and_authentication_boolean_cannot_verify(self):
        for variant in ('policy', 'signature', 'scope', 'boolean', 'base64', 'tool'):
            with self.subTest(variant=variant):
                request = self.request()
                body = a.strict_json(request.stdin)
                if variant in ('policy', 'signature'):
                    body['exactReadbackBytes'][variant] = base64.b64encode(b'changed original').decode()
                elif variant == 'scope':
                    body['scopeSha256'] = 'f' * 64
                elif variant == 'boolean':
                    body['authenticated'] = True
                elif variant == 'base64':
                    body['exactReadbackBytes']['signature'] = '!!!'
                else:
                    request = replace(request, tool=replace(request.tool, sha256='f' * 64))
                request = replace(request, stdin=a.canonical(body))
                with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
                    self.exchange(request)
                self.assertEqual(self.verifier.calls, [])

    def test_trust_signer_fingerprint_namespace_and_trust_digest_drift_stop_before_verification(self):
        for trust, digest, signer in (
            (f.TRUST, 'f' * 64, f.SIGNER), (f.TRUST, a.digest(f.TRUST), 'wrong-signer'),
            (f.TRUST.replace(b'hexalith-recovery', b'other-namespace'), None, f.SIGNER),
            (f.TRUST.replace(b'namespaces="hexalith-recovery" ', b''), None, f.SIGNER),
            (f.TRUST + f.TRUST, None, f.SIGNER),
            (f.TRUST.replace(b'ssh-ed25519 ', b'ssh-rsa '), None, f.SIGNER)):
            self.driver.trust, self.driver.trust_sha256, self.driver.signer = trust, a.digest(trust) if digest is None else digest, signer
            with self.assertRaises(b.Refusal):
                self.exchange()
            self.assertEqual(self.verifier.calls, [])

    def test_cryptographic_failure_refuses_even_with_matching_hashes_and_dates(self):
        self.verifier.code = 1
        with self.assertRaises(b.Refusal):
            self.exchange()
        self.assertEqual(len(self.verifier.calls), 1)

    def test_real_pinned_ssh_verifier_rejects_invented_signature_without_signing_or_key_access(self):
        self.driver.process = b.BoundedProcess(self.boundary.dependencies)
        with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
            self.exchange()
        self.assertEqual(self.verifier.calls, [])

    def test_real_verifier_accepts_public_openssh_vector_and_rejects_changed_message_signature_and_namespace(self):
        # OpenSSH public-only regress fixture blobs (no private test key loaded):
        # https://github.com/openssh/openssh-portable/tree/master/regress/unittests/sshsig/testdata
        # ed25519.pub: b078e4516fbe44d2cf03e32a98e624bcb2ea6875
        # ed25519.sig: 8e8ff2a8ac197df506ee00eef1d398a8090609c5
        # signed-data: 7df4bedd135c385ed6e5cf99c901e3ad6e44a043
        public = b'ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIIlizSIN3DQV78VPNjVvvwjgf+P5HqbPYCYu3BOMdjAE'
        trust = b'upstream-fixture namespaces="unittest" ' + public + b'\n'
        signature = b"""-----BEGIN SSH SIGNATURE-----
U1NIU0lHAAAAAQAAADMAAAALc3NoLWVkMjU1MTkAAAAgiWLNIg3cNBXvxU82NW+/COB/4/
keps9gJi7cE4x2MAQAAAAIdW5pdHRlc3QAAAAAAAAABnNoYTUxMgAAAFMAAAALc3NoLWVk
MjU1MTkAAABAihQsbUzuNEFflk5Tw1+H9aLS7tZQk0RG8KW1DtOmDYYnWe3D3UKiG3fcJa
DNg4vBWp1j1gLRiBMOF+gwYNegDg==
-----END SSH SIGNATURE-----
"""
        message = b'This is a test, this is only a test'
        process = b.BoundedProcess(self.boundary.dependencies)
        tool = dict(self.boundary.binding.tools)['signatureVerifier']
        for changed in ('none', 'message', 'signature', 'namespace', 'trust'):
            with self.subTest(changed=changed):
                result = process.run(tool, ('-Y', 'verify', '-f', '@trust', '-I', 'upstream-fixture',
                    '-n', 'different' if changed == 'namespace' else 'unittest', '-s', '@signature'),
                    message + (b'changed' if changed == 'message' else b''), b'{"credentials":[]}',
                    time.monotonic() + 3, 16384, 16384,
                    files=(('@trust', trust.replace(b'upstream-fixture', b'other') if changed == 'trust' else trust),
                           ('@signature', signature.replace(b'DNg4v', b'ENg4v') if changed == 'signature' else signature)))
                self.assertEqual(result.exit_code == 0, changed == 'none')

    def test_scope_dates_decisions_checked_after_verification_and_no_reserialization(self):
        for field in ('scopeSha256', 'validFrom', 'expiresAt', 'retainedDecisionsSha256', 'schemaVersion'):
            policy = a.strict_json(self.verifier.policy)
            policy[field] = 'f' * 64 if field.endswith('Sha256') else ('2026-10-23T13:00:00Z' if field != 'schemaVersion' else 2)
            # Whitespace is retained, proving verifier stdin is the original buffer.
            raw = b'\n ' + a.canonical(policy) + b'\n'
            binding = replace(self.boundary.binding, policy=replace(self.boundary.binding.policy,
                sha256=a.digest(raw), size=len(raw)))
            self.driver.binding = binding
            self.verifier.policy = raw
            with self.assertRaises(b.Refusal):
                self.exchange(self.request(binding, raw))
            self.assertEqual(self.verifier.calls[-1][2], raw)
        self.assertEqual(len(self.verifier.calls), 5)

    def test_expiry_checked_after_verifier_and_expired_deadline_before_verifier(self):
        with self.assertRaises(b.Refusal):
            self.driver.exchange(self.request(), time.monotonic() - 1)
        self.assertEqual(self.verifier.calls, [])
        self.driver.now = lambda: a.timestamp(self.boundary.binding.expires_at)
        with self.assertRaises(b.Refusal):
            self.exchange()
        self.assertEqual(self.verifier.calls, [])
        current = [f.NOW]
        self.driver.now = lambda: current[0]
        run = self.verifier.run
        def verify(*args, **kwargs):
            result = run(*args, **kwargs)
            current[0] = a.timestamp(self.boundary.binding.expires_at)
            return result
        with patch.object(self.verifier, 'run', side_effect=verify), self.assertRaises(b.Refusal):
            self.exchange()
        self.assertEqual(len(self.verifier.calls), 1)

    def test_bool_exit_status_is_not_cryptographic_success(self):
        class BooleanVerifier:
            def run(self, *args, **kwargs):
                return b.ProcessResult(False, b'authenticated=true')
        self.driver.process = BooleanVerifier()
        with self.assertRaises(b.Refusal):
            self.exchange()

    def test_raw_dependency_change_prevents_verifier_dispatch(self):
        self.driver.dependencies = (replace(self.boundary.dependencies[0], sha256='f' * 64),) + self.boundary.dependencies[1:]
        with self.assertRaises(b.Refusal):
            self.exchange()
        self.assertEqual(self.verifier.calls, [])


    def test_verified_coherently_rebound_boolean_schema_version_refuses_after_verification(self):
        value = a.strict_json(self.verifier.policy)
        value['schemaVersion'] = True
        raw = a.canonical(value)
        binding = replace(self.boundary.binding, policy=replace(self.boundary.binding.policy,
            sha256=a.digest(raw), size=len(raw)))
        self.driver.binding = binding
        self.verifier.policy = raw
        with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
            self.exchange(self.request(binding, raw))
        self.assertEqual(self.verifier.calls[0][2], raw)

    def test_final_runtime_scan_expiry_prevents_authentication_success(self):
        for mode in ('policy', 'deadline'):
            with self.subTest(mode=mode):
                current, scans = [f.NOW], []
                future, expired, original = time.monotonic() + 100, [False], b.check_runtime
                self.driver.now = lambda: current[0]
                def scan(*args):
                    original(*args)
                    scans.append(None)
                    if len(scans) == 2:
                        expired[0] = True
                        if mode == 'policy':
                            current[0] = a.timestamp(self.boundary.binding.expires_at)
                with patch.object(b.time, 'monotonic', side_effect=lambda: future if expired[0] and mode == 'deadline' else future - 100), patch.object(
                        b, 'check_runtime', side_effect=scan):
                    with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
                        self.driver.exchange(self.request(), future - 99)
                self.assertEqual(len(scans), 2)
                self.assertTrue(expired[0])


    def test_standalone_verifier_budget_is_capped_to_request_policy_and_rechecked_after_scan(self):
        from datetime import timedelta
        run = self.verifier.run
        request = self.request()
        for seconds in (3600, 2):
            with self.subTest(seconds=seconds):
                self.driver.now = lambda: a.timestamp(self.boundary.binding.expires_at) - timedelta(seconds=seconds)
                deadlines = []
                def verify(tool, arguments, stdin, credentials, deadline, *args, **kwargs):
                    deadlines.append(deadline - time.monotonic())
                    return run(tool, arguments, stdin, credentials, deadline, *args, **kwargs)
                with patch.object(self.verifier, 'run', side_effect=verify):
                    self.driver.exchange(request, time.monotonic() + 120)
                self.assertEqual(len(deadlines), 1)
                self.assertTrue(0 < deadlines[0] <= min(request.timeout_seconds, seconds))
        current = [f.NOW]
        self.driver.now = lambda: current[0]
        scan = b.check_runtime
        def check(*args):
            scan(*args)
            current[0] = a.timestamp(self.boundary.binding.expires_at)
        calls = len(self.verifier.calls)
        with patch.object(b, 'check_runtime', side_effect=check), self.assertRaises(b.Refusal):
            self.driver.exchange(request, time.monotonic() + 120)
        self.assertEqual(len(self.verifier.calls), calls)


if __name__ == '__main__':
    unittest.main(verbosity=2)
