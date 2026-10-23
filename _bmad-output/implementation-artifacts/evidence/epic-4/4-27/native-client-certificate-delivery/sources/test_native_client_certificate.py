#!/usr/bin/env python3
"""Static synthetic client identities, real TLS/MemoryBIO, and zero sockets.

The embedded public fixture keys confer no authority. Nothing reads actual
credentials/configuration, signs recovery documents, or selects a live connector.
"""
from contextlib import redirect_stdout
from dataclasses import replace
import importlib.util
import inspect
import io
import json
import os
from pathlib import Path
import socket
import ssl
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

_spec = importlib.util.spec_from_file_location('native_certificate_fixture_support',
    Path(__file__).with_name('_execution_boundary_fixtures.py'))
f = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = f
_spec.loader.exec_module(f)
b, a = f.b, f.a


def envelope(certificate=f.CERT_SOURCE_CLIENT, key=f.CERT_SOURCE_CLIENT_KEY, **extra):
    return a.canonical(dict(certificatePem=certificate.decode('ascii'), privateKeyPem=key.decode('ascii'), **extra))


def descriptors():
    result = {}
    for name in os.listdir('/proc/self/fd'):
        fd = int(name)
        try:
            result[fd] = (os.fstat(fd), b.fcntl.fcntl(fd, b.fcntl.F_GETFD))
        except OSError:
            pass  # The directory enumeration's already closed descriptor.
    return result


def server_context(label='SOURCE'):
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    context.minimum_version = ssl.TLSVersion.TLSv1_2
    context.verify_mode = ssl.CERT_REQUIRED
    context.verify_flags |= ssl.VERIFY_X509_STRICT
    context.load_verify_locations(cadata=getattr(f, 'CERT_' + label + '_CA').decode('ascii'))
    owned = []
    try:
        for suffix in ('_SERVER', '_SERVER_KEY'):
            owned.append(b.sealed(getattr(f, 'CERT_' + label + suffix)))
        context.load_cert_chain(*(('/proc/self/fd/' + str(fd)) for fd in owned))
    finally:
        error = b._close_owned(owned)
        if error is not None:
            raise error
    return context


class MemoryStream:
    """Real mutual TLS peer and HTTP bytes, entirely through four memory BIOs."""
    def __init__(self, client_context, server, hostname, expected_client, response, deadline):
        self.cin, self.cout, self.sin, self.sout = (ssl.MemoryBIO() for _ in range(4))
        self.client = client_context.wrap_bio(self.cin, self.cout, server_hostname=hostname)
        self.server = server.wrap_bio(self.sin, self.sout, server_side=True)
        self.sent, self.timeouts, self.closed = bytearray(), [], False
        self.response, self.responded = response, False
        self.deadline = deadline
        completed = set()
        for _ in range(100):
            b.remaining(deadline)
            for label, peer in (('client', self.client), ('server', self.server)):
                if label not in completed:
                    try:
                        peer.do_handshake()
                        completed.add(label)
                    except ssl.SSLWantReadError:
                        pass
                self.pump()
            if len(completed) == 2:
                break
        assert len(completed) == 2, 'synthetic handshake did not complete'
        self.peer = self.server.getpeercert(binary_form=True)
        assert self.peer == ssl.PEM_cert_to_DER_cert(expected_client.decode('ascii')), 'wrong synthetic client identity'

    def pump(self):
        for outgoing, incoming in ((self.cout, self.sin), (self.sout, self.cin)):
            raw = outgoing.read()
            if raw:
                incoming.write(raw)

    def settimeout(self, value):
        self.timeouts.append(value)

    def send(self, raw):
        count = self.client.write(raw[:13])
        self.pump()
        self.sent.extend(self.server.read(count))
        self.pump()
        return count

    def recv(self, maximum):
        if not self.responded:
            self.server.write(self.response)
            try:
                self.server.unwrap()  # Sends close_notify; no socket or blocking wait.
            except ssl.SSLWantReadError:
                pass
            self.pump()
            self.responded = True
        return self.client.read(maximum)

    def close(self):
        self.closed = True


class CertificateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)
        self.directory.chmod(0o700)
        self.network = patch.object(socket, 'socket', side_effect=AssertionError('all sockets forbidden'))
        self.network.start()
        self.addCleanup(self.network.stop)
        self.boundary, _, _, _ = f.compose(self.directory)
        self.request = self.boundary.requests[3]
        self.credential = f.credential(self.request.principal, self.boundary.binding.source_cluster.uid, envelope())
        cluster = replace(self.boundary.binding.source_cluster, ca_sha256=a.digest(f.CERT_SOURCE_CA))
        self.binding = b.NativeBinding(cluster, f.CERT_SOURCE_CA, self.credential.binding, 'client-certificate')
        self.streams, self.contexts = [], []
        self.server = server_context()

    def connector(self, context, cluster, deadline):
        self.contexts.append(context)
        self.assertEqual(cluster, self.binding.cluster)
        self.assertEqual(context.get_ca_certs(binary_form=True), [ssl.PEM_cert_to_DER_cert(self.binding.ca.decode('ascii'))])
        stream = MemoryStream(context, self.server, cluster.endpoint[8:].split(':')[0],
            f.CERT_SOURCE_CLIENT, f.http(b'{}'), deadline)
        self.streams.append(stream)
        return stream

    def exchange(self, *, binding=None, credential=None, connector=None, deadline=None):
        return b.NativeTLS(self.connector if connector is None else connector).exchange(self.request,
            time.monotonic() + 5 if deadline is None else deadline, binding or self.binding, credential or self.credential)

    def assert_refused(self, **kwargs):
        with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
            self.exchange(**kwargs)

    def test_exact_certificate_and_key_authenticate_real_peer_without_authorization(self):
        before = descriptors()
        response = self.exchange()
        self.assertEqual(response, a.Response(0, 200, b'{}'))
        self.assertEqual(descriptors(), before)
        self.assertTrue(self.streams[0].closed)
        self.assertNotIn(b'Authorization:', self.streams[0].sent)
        self.assertNotIn(self.credential.secret, self.streams[0].sent)
        self.assertEqual(bytes(self.streams[0].sent).split(b'\r\n')[0],
            (self.request.method + ' ' + self.request.path + ' HTTP/1.1').encode())

    def test_sealed_descriptor_paths_exact_bytes_permissions_and_noninteractive_loader(self):
        original = ssl.SSLContext.load_cert_chain
        seen = []
        def load(context, certificate, key, password=None):
            self.assertTrue(callable(password))
            with self.assertRaises(b.Refusal):
                password()
            for path, expected in ((certificate, f.CERT_SOURCE_CLIENT), (key, f.CERT_SOURCE_CLIENT_KEY)):
                self.assertRegex(path, r'^/proc/self/fd/[0-9]+$')
                fd = int(path.rsplit('/', 1)[1])
                self.assertGreaterEqual(fd, 4)
                flags = b.fcntl.F_SEAL_WRITE | b.fcntl.F_SEAL_GROW | b.fcntl.F_SEAL_SHRINK | b.fcntl.F_SEAL_SEAL
                self.assertEqual(b.fcntl.fcntl(fd, b.fcntl.F_GET_SEALS) & flags, flags)
                self.assertEqual(os.pread(fd, len(expected) + 1, 0), expected)
                self.assertEqual(os.fstat(fd).st_mode & 0o777, 0o600)
                self.assertTrue(b.fcntl.fcntl(fd, b.fcntl.F_GETFD) & b.fcntl.FD_CLOEXEC)
                with self.assertRaises(OSError):
                    os.write(fd, b'alteration')
                seen.append(fd)
            return original(context, certificate, key, password=password)
        with patch.object(ssl.SSLContext, 'load_cert_chain', load):
            self.exchange()
        self.assertEqual(len(seen), 2)
        for fd in seen:
            with self.assertRaises(OSError):
                os.fstat(fd)

    def test_bearer_default_and_explicit_profile_preserve_wire_and_audience(self):
        credential = f.credential(self.request.principal, self.binding.cluster.uid)
        for profile in ('default', 'bearer'):
            binding = b.NativeBinding(self.binding.cluster, self.binding.ca, credential.binding)
            if profile != 'default':
                binding = replace(binding, authentication_profile=profile)
            stream = f.Stream(f.http(b'{}'), send_maximum=1)
            self.exchange(binding=binding, credential=credential, connector=lambda *args: stream)
            expected = (self.request.method + ' ' + self.request.path + ' HTTP/1.1\r\nHost: ' + self.request.endpoint[8:] +
                '\r\nAccept: application/json\r\nAccept-Encoding: identity\r\nConnection: close\r\nContent-Type: ' +
                self.request.content_type + '\r\nContent-Length: 0\r\nAuthorization: Bearer ').encode() + credential.secret + b'\r\n\r\n'
            self.assertEqual(bytes(stream.sent), expected)
            self.assertTrue(stream.closed)

    def test_unknown_mixed_and_unselected_profiles_refuse_without_connector(self):
        for profile, raw in (('unknown', envelope()), ('bearer+client-certificate', envelope()),
                ('bearer', envelope()), ('client-certificate', b'synthetic-bearer'),
                ('client-certificate', envelope(token='synthetic-bearer')), (None, envelope())):
            with self.subTest(profile=profile):
                credential = f.credential(self.request.principal, self.binding.cluster.uid, raw)
                binding = replace(self.binding, authentication_profile=profile, credential=credential.binding)
                self.assert_refused(binding=binding, credential=credential)
        self.assertEqual(self.contexts, [])

    def test_principal_audience_identity_custody_secret_and_ca_drift_refuse_before_dispatch(self):
        altered = [replace(self.credential, identity=self.credential.identity + b' '),
            replace(self.credential, custody=self.credential.custody + b' '),
            replace(self.credential, secret=self.credential.secret + b' '),
            replace(self.credential, binding=replace(self.credential.binding, principal='other')),
            replace(self.credential, binding=replace(self.credential.binding, audience='target-cluster-uid'))]
        for index, credential in enumerate(altered):
            with self.subTest(field=index):
                self.assert_refused(credential=credential)
        for binding in (replace(self.binding, ca=f.CERT_TARGET_CA),
                replace(self.binding, credential=replace(self.binding.credential, principal='other')),
                replace(self.binding, credential=replace(self.binding.credential, audience='target-cluster-uid'))):
            self.assert_refused(binding=binding)
        self.assertEqual(self.contexts, [])

    def test_malformed_missing_mismatched_encrypted_duplicate_and_oversized_material_refuse(self):
        cert, key = f.CERT_SOURCE_CLIENT.decode(), f.CERT_SOURCE_CLIENT_KEY.decode()
        raws = [b'{malformed', a.canonical({'certificatePem': cert}), a.canonical({'privateKeyPem': key}),
            envelope(certificate=b'not PEM'), envelope(key=b'not PEM'), envelope(key=f.CERT_TARGET_CLIENT_KEY),
            envelope(key=key.replace('PRIVATE KEY', 'ENCRYPTED PRIVATE KEY').encode()),
            envelope(key=b'-----BEGIN RSA PRIVATE KEY-----\nProc-Type: 4,ENCRYPTED\nAAAA\n-----END RSA PRIVATE KEY-----\n'),
            a.canonical({'certificatePem': cert, 'privateKeyPem': 1}),
            ('{"certificatePem":' + json.dumps(cert) + ',"privateKeyPem":' + json.dumps(key) + ',"privateKeyPem":' + json.dumps(key) + '}').encode(),
            envelope(certificate=f.CERT_SOURCE_CLIENT + b'garbage'), envelope(key=f.CERT_SOURCE_CLIENT_KEY + b'\x00'),
            envelope(certificate=f.CERT_SOURCE_CLIENT * 70), b'x' * 65537]
        for index, raw in enumerate(raws):
            with self.subTest(index=index):
                before = descriptors()
                credential = f.credential(self.request.principal, self.binding.cluster.uid, raw)
                self.assert_refused(binding=replace(self.binding, credential=credential.binding), credential=credential)
                self.assertEqual(descriptors(), before)
        self.assertEqual(self.contexts, [])

    def test_noop_loader_cannot_pass_peer_authentication(self):
        with patch.object(ssl.SSLContext, 'load_cert_chain', return_value=None):
            self.assert_refused()
        self.assertEqual(self.streams, [])
        self.assertEqual(len(self.contexts), 1)

    def test_wrong_certificate_loader_cannot_pass_peer_authentication(self):
        original = ssl.SSLContext.load_cert_chain
        def wrong(context, *args, **kwargs):
            owned = [b.sealed(f.CERT_TARGET_CLIENT), b.sealed(f.CERT_TARGET_CLIENT_KEY)]
            try:
                return original(context, *(('/proc/self/fd/' + str(fd)) for fd in owned))
            finally:
                b._close_owned(owned)
        before = descriptors()
        with patch.object(ssl.SSLContext, 'load_cert_chain', wrong):
            self.assert_refused()
        self.assertEqual(descriptors(), before)
        self.assertEqual(self.streams, [])

    def test_wrong_ca_and_wrong_hostname_are_detected_by_real_tls(self):
        for cluster in (replace(self.binding.cluster, ca_sha256=a.digest(f.CERT_TARGET_CA)),
                replace(self.binding.cluster, endpoint='https://192.0.2.99:6443')):
            ca = f.CERT_TARGET_CA if cluster.ca_sha256 == a.digest(f.CERT_TARGET_CA) else f.CERT_SOURCE_CA
            binding = replace(self.binding, cluster=cluster, ca=ca)
            request = replace(self.request, endpoint=cluster.endpoint)
            def connector(context, cluster, deadline):
                return MemoryStream(context, self.server, cluster.endpoint[8:].split(':')[0],
                    f.CERT_SOURCE_CLIENT, f.http(b'{}'), deadline)
            with self.subTest(endpoint=cluster.endpoint), self.assertRaises(b.Refusal):
                b.NativeTLS(connector).exchange(request, time.monotonic() + 5, binding, self.credential)

    def test_loading_expiry_and_descriptor_cleanup_expiry_prevent_connector(self):
        original_load, original_seal, original_close = ssl.SSLContext.load_cert_chain, b.sealed, b._close_owned
        for stage in ('seal', 'load', 'cleanup'):
            clock, count = [100.0], [0]
            def seal(raw):
                fd = original_seal(raw)
                count[0] += 1
                if stage == 'seal' and count[0] == 2:
                    clock[0] = 102.0
                return fd
            def load(*args, **kwargs):
                result = original_load(*args, **kwargs)
                if stage == 'load':
                    clock[0] = 102.0
                return result
            def close(*args, **kwargs):
                result = original_close(*args, **kwargs)
                if stage == 'cleanup':
                    clock[0] = 102.0
                return result
            before = descriptors()
            with self.subTest(stage=stage), patch.object(b, 'sealed', seal), patch.object(
                    ssl.SSLContext, 'load_cert_chain', load), patch.object(b, '_close_owned', close), patch.object(
                    b.time, 'monotonic', side_effect=lambda: clock[0]):
                self.assert_refused(deadline=101.0)
            self.assertEqual(descriptors(), before)
        self.assertEqual(self.contexts, [])

    def test_loading_interrupt_preserved_while_every_owned_descriptor_closes_independently(self):
        owned, closes = [], []
        primary = KeyboardInterrupt('primary synthetic loading cancellation')
        secondary = KeyboardInterrupt('secondary synthetic cleanup cancellation')
        original_seal, original_close = b.sealed, os.close
        def seal(raw):
            fd = original_seal(raw)
            owned.append(fd)
            return fd
        def close(fd):
            if fd in owned:
                closes.append(fd)
                if closes.count(fd) == 1:
                    raise secondary
            return original_close(fd)
        before = descriptors()
        with patch.object(b, 'sealed', seal), patch.object(ssl.SSLContext, 'load_cert_chain', side_effect=primary), patch.object(
                b.os, 'close', close), self.assertRaises(KeyboardInterrupt) as caught:
            self.exchange()
        self.assertIs(caught.exception, primary)
        self.assertEqual(sorted(closes), sorted(owned * 2))
        self.assertEqual(descriptors(), before)
        self.assertEqual(self.contexts, [])

    def test_interrupt_on_first_installation_cleanup_line_retains_outer_descriptor_ownership(self):
        source, start = inspect.getsourcelines(b.NativeTLS._install_certificate)
        line = start + next(i for i, text in enumerate(source) if text.strip() == 'error = _close_owned(owned, error)')
        primary = KeyboardInterrupt('synthetic cleanup transition interruption')
        fired, owned = [], []
        original = b.sealed
        def seal(raw):
            fd = original(raw)
            owned.append(fd)
            return fd
        def trace(frame, event, argument):
            if event == 'line' and frame.f_code is b.NativeTLS._install_certificate.__code__ and frame.f_lineno == line and not fired:
                fired.append(True)
                raise primary
            return trace
        before = descriptors()
        previous = sys.gettrace()
        with patch.object(b, 'sealed', seal), self.assertRaises(KeyboardInterrupt) as caught:
            try:
                sys.settrace(trace)
                self.exchange()
            finally:
                sys.settrace(previous)
        self.assertIs(caught.exception, primary)
        self.assertEqual(fired, [True])
        self.assertEqual(len(owned), 2)
        self.assertEqual(descriptors(), before)
        self.assertEqual(self.contexts, [])

    def test_sealing_failure_closes_first_descriptor_and_cancellation_never_dispatches(self):
        original = b.sealed
        for problem in (OSError('synthetic sealing failure'), KeyboardInterrupt('synthetic sealing interrupt')):
            calls = []
            def seal(raw):
                if calls:
                    raise problem
                fd = original(raw)
                calls.append(fd)
                return fd
            before = descriptors()
            with self.subTest(kind=type(problem).__name__), patch.object(b, 'sealed', seal):
                if isinstance(problem, Exception):
                    self.assert_refused()
                else:
                    with self.assertRaises(KeyboardInterrupt) as caught:
                        self.exchange()
                    self.assertIs(caught.exception, problem)
            self.assertEqual(descriptors(), before)
        self.assertEqual(self.contexts, [])

    def test_cleanup_failure_refuses_and_closes_both_descriptors(self):
        owned, failed = [], set()
        original_seal, original_close = b.sealed, os.close
        def seal(raw):
            fd = original_seal(raw)
            owned.append(fd)
            return fd
        def close(fd):
            if fd in owned and fd not in failed:
                failed.add(fd)
                raise OSError('synthetic close failure')
            return original_close(fd)
        before = descriptors()
        with patch.object(b, 'sealed', seal), patch.object(b.os, 'close', close):
            self.assert_refused()
        self.assertEqual(failed, set(owned))
        self.assertEqual(descriptors(), before)
        self.assertEqual(self.contexts, [])

    def test_exchange_interrupt_preserves_primary_and_closes_stream_without_retry(self):
        stream = f.Stream(f.http(b'{}'))
        primary, secondary = KeyboardInterrupt('primary exchange interrupt'), KeyboardInterrupt('cleanup interrupt')
        stream.send = lambda raw: (_ for _ in ()).throw(primary)
        def close():
            stream.closed = True
            raise secondary
        stream.close = close
        calls = []
        def connector(*args):
            calls.append(args)
            return stream
        before = descriptors()
        with self.assertRaises(KeyboardInterrupt) as caught:
            self.exchange(connector=connector)
        self.assertIs(caught.exception, primary)
        self.assertTrue(stream.closed)
        self.assertEqual(len(calls), 1)
        self.assertEqual(descriptors(), before)

    def test_exchange_and_stream_cleanup_expiry_or_interrupt_never_return_late_success(self):
        for stage in ('send', 'recv', 'close'):
            for interrupt in (False, True):
                clock = [100.0]
                stream = f.Stream(f.http(b'{}'))
                original = getattr(stream, stage)
                primary = KeyboardInterrupt('synthetic ' + stage + ' cancellation')
                def action(*args):
                    result = original(*args)
                    if interrupt:
                        raise primary
                    clock[0] = 102.0
                    return result
                setattr(stream, stage, action)
                before = descriptors()
                with self.subTest(stage=stage, interrupt=interrupt), patch.object(b.time, 'monotonic', side_effect=lambda: clock[0]):
                    if interrupt:
                        with self.assertRaises(KeyboardInterrupt) as caught:
                            self.exchange(connector=lambda *args: stream, deadline=101.0)
                        self.assertIs(caught.exception, primary)
                    else:
                        self.assert_refused(connector=lambda *args: stream, deadline=101.0)
                self.assertTrue(stream.closed)
                self.assertEqual(descriptors(), before)

    def test_default_connector_and_public_activation_refuse_before_credential_context_or_network(self):
        for arguments in ([], ['--activate'], ['--profile', 'client-certificate'], ['capture', '--live']):
            output = io.StringIO()
            with patch.object(b.Credential, 'descriptor', side_effect=AssertionError('credential access forbidden')), patch.object(
                    b.NativeTLS, '_context', side_effect=AssertionError('TLS work forbidden')), patch.object(
                    b.os, 'open', side_effect=AssertionError('configuration access forbidden')):
                with self.assertRaises(b.Refusal):
                    b.NativeTLS().exchange(self.request, time.monotonic() + 1, self.binding, self.credential)
                with redirect_stdout(output):
                    self.assertEqual(b.main(arguments), 1)
            self.assertEqual(json.loads(output.getvalue()), {'backendRequests': 0, 'operationalGate': 'closed', 'result': b.FAILURE})

    def test_occupied_parent_fd3_and_its_flags_remain_unchanged(self):
        fd = os.open('/dev/null', os.O_RDONLY | os.O_CLOEXEC)
        try:
            before = descriptors()
            self.exchange()
            self.assertEqual(descriptors(), before)
            self.assertEqual(os.read(fd, 1), b'')
        finally:
            os.close(fd)

    def test_loading_preserves_originally_closed_standard_descriptors(self):
        source = '''import importlib.util, os, pathlib, socket, sys, time
path = pathlib.Path(sys.argv[1])
spec = importlib.util.spec_from_file_location('isolated_native_certificate', path)
t = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = t
spec.loader.exec_module(t)
case = t.CertificateTests('test_exact_certificate_and_key_authenticate_real_peer_without_authorization')
case.setUp()
try:
    os.close(int(sys.argv[2]))
    before = t.descriptors()
    case.exchange()
    assert t.descriptors() == before
finally:
    case.doCleanups()
'''
        for fd in (0, 1, 2):
            with self.subTest(fd=fd):
                result = subprocess.run(('/usr/bin/python3.14', '-I', '-S', '-B', '-c', source, __file__, str(fd)),
                    stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=10)
                self.assertEqual(result.returncode, 0, result.stderr.decode())


class CompositionTests(unittest.TestCase):
    def test_all_four_plans_preserve_distinct_certificate_identities_audiences_native_requests_and_guards(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(socket, 'socket', side_effect=AssertionError('all sockets forbidden')):
            for operation in ('capture', 'restore', 'resume', 'cleanup'):
                with self.subTest(operation=operation):
                    boundary, provider, verifier, native = f.compose(Path(directory), operation,
                        native_profiles=('client-certificate', 'client-certificate'),
                        native_secrets=(envelope(), envelope(f.CERT_TARGET_CLIENT, f.CERT_TARGET_CLIENT_KEY)),
                        native_cas=(f.CERT_SOURCE_CA, f.CERT_TARGET_CA))
                    boundary.process = f.StageProtocol(boundary.binding, verifier.policy, verifier.signature, boundary.credentials)
                    peers, streams = [], []
                    servers = {boundary.binding.source_cluster.uid: server_context('SOURCE'),
                        boundary.binding.target_cluster.uid: server_context('TARGET')}
                    def exchange(request, deadline, binding, credential):
                        expected = native.backend.exchange(request, deadline)
                        label = 'SOURCE' if binding.cluster == boundary.binding.source_cluster else 'TARGET'
                        def connector(context, cluster, limit):
                            self.assertEqual(context.get_ca_certs(binary_form=True), [ssl.PEM_cert_to_DER_cert(binding.ca.decode())])
                            stream = MemoryStream(context, servers[cluster.uid], cluster.endpoint[8:].split(':')[0],
                                getattr(f, 'CERT_' + label + '_CLIENT'), f.http(expected.body, expected.status), limit)
                            streams.append(stream)
                            peers.append((cluster.uid, credential.binding.principal, stream.peer))
                            return stream
                        result = b.NativeTLS(connector).exchange(request, deadline, binding, credential)
                        header, body = bytes(streams[-1].sent).split(b'\r\n\r\n', 1)
                        self.assertNotIn(b'Authorization:', header)
                        self.assertEqual(header.split(b'\r\n')[0], (request.method + ' ' + request.path + ' HTTP/1.1').encode())
                        self.assertEqual(body, b'' if request.method == 'GET' else request.stdin)
                        self.assertEqual(credential.binding.audience, binding.cluster.uid)
                        return result
                    native.exchange = exchange
                    result = a.run(boundary.binding, boundary, f.old.MemoryReceipts(), now=lambda: f.NOW)
                    self.assertIsNone(result.failure)
                    self.assertEqual(boundary.index, len(boundary.requests))
                    self.assertIsNone(result.operational_qualification)
                    self.assertIsNone(result.recovery_acceptance)
                    self.assertTrue(all(stream.closed for stream in streams))
                    self.assertEqual({principal for _, principal, _ in peers}, {boundary.binding.operator})
                    self.assertTrue(all(c.binding.principal == boundary.binding.validator for _, c in provider.calls))
                    for uid, _, peer in peers:
                        label = 'SOURCE' if uid == boundary.binding.source_cluster.uid else 'TARGET'
                        self.assertEqual(peer, ssl.PEM_cert_to_DER_cert(getattr(f, 'CERT_' + label + '_CLIENT').decode()))
                    if operation in ('capture', 'restore'):
                        self.assertEqual(len({uid for uid, _, _ in peers}), 2)
                        self.assertEqual(len({peer for _, _, peer in peers}), 2)

    def test_source_certificate_target_bearer_are_explicit_independent_selections(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(socket, 'socket', side_effect=AssertionError('all sockets forbidden')):
            boundary, _, verifier, native = f.compose(Path(directory), native_profiles=('client-certificate', 'bearer'),
                native_secrets=(envelope(), None), native_cas=(f.CERT_SOURCE_CA, f.CERT_TARGET_CA))
            boundary.process = f.StageProtocol(boundary.binding, verifier.policy, verifier.signature, boundary.credentials)
            result = a.run(boundary.binding, boundary, f.old.MemoryReceipts(), now=lambda: f.NOW)
            self.assertIsNone(result.failure)
            for request, stream in zip(native.calls, native.streams):
                authorization = b'Authorization: Bearer ' in stream.sent
                self.assertEqual(authorization, request.endpoint == boundary.binding.target_cluster.endpoint)


if __name__ == '__main__':
    unittest.main(verbosity=2)
