#!/usr/bin/env python3
"""Static synthetic client identities, real TLS/MemoryBIO, and zero sockets.

The embedded public fixture keys confer no authority. Nothing reads actual
credentials/configuration, signs recovery documents, or selects a live connector.
"""
from contextlib import redirect_stdout
from dataclasses import replace
import importlib.util
import inspect
import dis
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
        error = b._close_raw(owned)
        if error is not None:
            raise error
    return context


class MemoryStream:
    _native_owned_stream = True
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


class PreparedDescriptorStream(f.Stream):
    """A synthetic prepared peer whose release state is C-owned."""
    def __init__(self):
        super().__init__(f.http(b'{}'))
        self.file = io.FileIO(b.sealed(b'public synthetic prepared peer'), 'rb', closefd=True)
        self.close_calls = 0

    @property
    def closed(self):
        return hasattr(self, 'file') and self.file.closed

    @closed.setter
    def closed(self, value):
        pass

    def close(self):
        self.close_calls += 1
        self.file.close()


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

    def connector(self, custody, context, cluster, deadline):
        self.contexts.append(context)
        self.assertEqual(cluster, self.binding.cluster)
        self.assertEqual(context.get_ca_certs(binary_form=True), [ssl.PEM_cert_to_DER_cert(self.binding.ca.decode('ascii'))])
        stream = custody.construct(MemoryStream, context, self.server, cluster.endpoint[8:].split(':')[0],
            f.CERT_SOURCE_CLIENT, f.http(b'{}'), deadline)
        self.streams.append(stream)

    def exchange(self, *, binding=None, credential=None, connector=None, deadline=None):
        return b.NativeTLS(b._OwnedConnector(self.connector) if connector is None else connector).exchange(self.request,
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

    def test_tls12_capped_real_peer_preserves_original_identity_and_native_wire(self):
        self.server.maximum_version = ssl.TLSVersion.TLSv1_2
        def connector(custody, context, cluster, deadline):
            context.maximum_version = ssl.TLSVersion.TLSv1_2
            self.connector(custody, context, cluster, deadline)
        request = replace(self.request, method='PATCH', path='/api/v1/namespaces',
            content_type='application/json-patch+json', stdin=b'[{"op":"test","path":"/metadata/uid","value":"fixture"}]')
        before = descriptors()
        response = b.NativeTLS(b._OwnedConnector(connector)).exchange(request,
            time.monotonic() + 5, self.binding, self.credential)
        self.assertEqual(response, a.Response(0, 200, b'{}'))
        stream = self.streams[-1]
        self.assertEqual(stream.client.version(), 'TLSv1.2')
        self.assertEqual(stream.server.version(), 'TLSv1.2')
        self.assertEqual(stream.peer, ssl.PEM_cert_to_DER_cert(f.CERT_SOURCE_CLIENT.decode('ascii')))
        headers, body = bytes(stream.sent).split(b'\r\n\r\n', 1)
        self.assertTrue(headers.startswith(b'PATCH /api/v1/namespaces HTTP/1.1\r\n'))
        self.assertIn(b'Content-Type: application/json-patch+json', headers)
        self.assertIn(b'Content-Length: ' + str(len(request.stdin)).encode(), headers)
        self.assertNotIn(b'Authorization:', headers)
        self.assertEqual(body, request.stdin)
        self.assertTrue(stream.closed)
        self.assertEqual(descriptors(), before)

    def test_saved_loading_interrupt_context_cannot_replace_current_exchange_primary(self):
        for history in ('saved-before-exchange', 'handled-inside-loader'):
            with self.subTest(history=history):
                older = KeyboardInterrupt('synthetic previously handled interruption')
                current = KeyboardInterrupt('synthetic reused loading interruption')
                def save_history():
                    try:
                        raise older
                    except KeyboardInterrupt:
                        try:
                            raise current
                        except KeyboardInterrupt:
                            pass
                if history == 'saved-before-exchange':
                    save_history()
                    self.assertIs(current.__context__, older)
                self.assertIsNone(sys.exception())
                def load(*arguments, **kwargs):
                    if history == 'handled-inside-loader':
                        save_history()
                    raise current
                before = descriptors()
                with patch.object(ssl.SSLContext, 'load_cert_chain', side_effect=load), patch.object(
                        b.NativeTLS, '_exchange', side_effect=AssertionError('no native dispatch')) as dispatch, self.assertRaises(KeyboardInterrupt) as caught:
                    self.exchange()
                self.assertIs(caught.exception, current)
                self.assertIsNot(caught.exception, older)
                self.assertIs(current.__context__, older)
                self.assertEqual(self.streams, [])
                self.assertEqual(descriptors(), before)
                dispatch.assert_not_called()

    def test_reraised_active_caller_interrupt_is_current_failure_and_never_success(self):
        primary = KeyboardInterrupt('synthetic reused active caller interruption')
        before = descriptors()
        try:
            raise primary
        except KeyboardInterrupt:
            with patch.object(ssl.SSLContext, 'load_cert_chain', side_effect=primary), patch.object(
                    b.NativeTLS, '_exchange', side_effect=AssertionError('no native dispatch')) as dispatch, self.assertRaises(KeyboardInterrupt) as caught:
                self.exchange()
        self.assertIs(caught.exception, primary)
        self.assertEqual(self.streams, [])
        self.assertEqual(descriptors(), before)
        dispatch.assert_not_called()

    def test_exchange_placeholder_guard_invocation_and_guard_entry_release_prepared_c_owned_stream(self):
        source, start = inspect.getsourcelines(b.NativeTLS.exchange)
        entry = start + next(i for i, text in enumerate(source) if 'owned = object.__new__' in text)
        instructions = list(dis.get_instructions(b.NativeTLS.exchange))
        guard_load = next(i for i, item in enumerate(instructions) if item.argval == '_guard_native')
        invocation = next(item.offset for item in instructions[guard_load + 1:]
            if item.opname == 'CALL' and item.argval == 6)
        for transition in ('placeholder', 'guard-invocation', 'guard-entry'):
            with self.subTest(transition=transition):
                before = descriptors()
                sentinel = io.FileIO(b.sealed(b'public caller descriptor'), 'rb', closefd=True)
                stream = PreparedDescriptorStream()
                stream_fd = stream.file.fileno()
                held = descriptors()
                primary, fired = KeyboardInterrupt('synthetic exchange entry ' + transition), []
                def trace(frame, event, argument):
                    if frame.f_code is b.NativeTLS.exchange.__code__:
                        frame.f_trace_opcodes = True
                    hit = (transition == 'placeholder' and event == 'line' and frame.f_code is b.NativeTLS.exchange.__code__ and frame.f_lineno == entry or
                        transition == 'guard-invocation' and event == 'opcode' and frame.f_code is b.NativeTLS.exchange.__code__ and frame.f_lasti == invocation or
                        transition == 'guard-entry' and event == 'call' and frame.f_code is b.NativeTLS._guard_native.__code__)
                    if hit and not fired:
                        self.assertFalse(stream.closed)
                        self.assertEqual(descriptors(), held)
                        fired.append(transition)
                        raise primary
                    return trace
                previous = sys.gettrace()
                try:
                    with patch.object(b.Credential, 'descriptor', side_effect=AssertionError('no credential work')) as descriptor, patch.object(
                            b.NativeTLS, '_context', side_effect=AssertionError('no context work')) as context, patch.object(
                            b.NativeTLS, '_exchange', side_effect=AssertionError('no native dispatch')) as dispatch, self.assertRaises(KeyboardInterrupt) as caught:
                        try:
                            sys.settrace(trace)
                            self.exchange(connector=f.owned_connector(stream))
                        finally:
                            sys.settrace(previous)
                    self.assertIs(caught.exception, primary)
                    self.assertEqual(fired, [transition])
                    self.assertTrue(stream.file.closed)
                    self.assertTrue(stream.closed)
                    self.assertEqual(stream.close_calls, 1)
                    self.assertEqual(stream.sent, b'')
                    self.assertEqual(os.pread(sentinel.fileno(), 100, 0), b'public caller descriptor')
                    remaining = dict(held)
                    remaining.pop(stream_fd)
                    self.assertEqual(descriptors(), remaining)
                    descriptor.assert_not_called()
                    context.assert_not_called()
                    dispatch.assert_not_called()
                finally:
                    stream.close()
                    sentinel.close()
                self.assertEqual(descriptors(), before)

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

    def test_valid_crlf_and_missing_terminal_newlines_install_original_bytes(self):
        original = ssl.SSLContext.load_cert_chain
        for crlf in (False, True):
            for terminal in (False, True):
                with self.subTest(crlf=crlf, terminal=terminal):
                    pair = [f.CERT_SOURCE_CLIENT, f.CERT_SOURCE_CLIENT_KEY]
                    pair = [raw if terminal else raw.rstrip(b'\n') for raw in pair]
                    if crlf:
                        pair = [raw.replace(b'\n', b'\r\n') for raw in pair]
                    raw = envelope(*pair)
                    credential = f.credential(self.request.principal, self.binding.cluster.uid, raw)
                    seen = []
                    def load(context, cert_path, key_path, **kwargs):
                        for path, expected in zip((cert_path, key_path), pair):
                            fd = int(path.rsplit('/', 1)[1])
                            self.assertEqual(os.pread(fd, len(expected) + 1, 0), expected)
                        seen.append(True)
                        return original(context, cert_path, key_path, **kwargs)
                    with patch.object(ssl.SSLContext, 'load_cert_chain', load):
                        self.exchange(binding=replace(self.binding, credential=credential.binding), credential=credential)
                    self.assertEqual(seen, [True])

    def test_traditional_ec_key_authenticates_real_peer(self):
        self.assertTrue(f.CERT_SOURCE_CLIENT_EC_KEY.startswith(b'-----BEGIN EC PRIVATE KEY-----'))
        credential = f.credential(self.request.principal, self.binding.cluster.uid,
            envelope(key=f.CERT_SOURCE_CLIENT_EC_KEY))
        self.exchange(binding=replace(self.binding, credential=credential.binding), credential=credential)
        self.assertEqual(self.streams[-1].peer, ssl.PEM_cert_to_DER_cert(f.CERT_SOURCE_CLIENT.decode()))

    def test_traditional_rsa_key_and_valid_intermediate_chain_authenticate_real_peer(self):
        self.assertTrue(f.CERT_RSA_CLIENT_KEY.startswith(b'-----BEGIN RSA PRIVATE KEY-----'))
        chain = f.CERT_RSA_CLIENT + f.CERT_CHAIN_INTERMEDIATE
        credential = f.credential(self.request.principal, self.binding.cluster.uid,
            envelope(chain, f.CERT_RSA_CLIENT_KEY))
        server = server_context()
        server.load_verify_locations(cadata=f.CERT_CHAIN_ROOT.decode())
        def connector(custody, context, cluster, deadline):
            self.assertEqual(context.get_ca_certs(binary_form=True),
                [ssl.PEM_cert_to_DER_cert(self.binding.ca.decode())])
            stream = custody.construct(MemoryStream, context, server, cluster.endpoint[8:].split(':')[0],
                f.CERT_RSA_CLIENT, f.http(b'{}'), deadline)
            self.streams.append(stream)
        self.exchange(binding=replace(self.binding, credential=credential.binding), credential=credential, connector=b._OwnedConnector(connector))
        self.assertEqual(self.streams[-1].peer, ssl.PEM_cert_to_DER_cert(f.CERT_RSA_CLIENT.decode()))
        self.assertNotIn(b'Authorization:', self.streams[-1].sent)
        # Removing the intermediate must fail peer chain validation.
        missing = f.credential(self.request.principal, self.binding.cluster.uid,
            envelope(f.CERT_RSA_CLIENT, f.CERT_RSA_CLIENT_KEY))
        self.assert_refused(binding=replace(self.binding, credential=missing.binding), credential=missing, connector=b._OwnedConnector(connector))

    def test_certificate_count_limit_isolated_below_all_byte_limits(self):
        for count in (16, 17):
            certificate = f.CERT_SOURCE_CLIENT * count
            raw = envelope(certificate)
            self.assertLess(len(certificate), 49152)
            self.assertLess(len(raw), 65536)
            credential = f.credential(self.request.principal, self.binding.cluster.uid, raw)
            with self.subTest(count=count):
                if count == 16:
                    self.exchange(binding=replace(self.binding, credential=credential.binding), credential=credential)
                else:
                    with patch.object(ssl.SSLContext, 'load_cert_chain', side_effect=AssertionError('count must refuse before loading')) as load:
                        self.assert_refused(binding=replace(self.binding, credential=credential.binding), credential=credential)
                        load.assert_not_called()

    def test_bearer_default_and_explicit_profile_preserve_wire_and_audience(self):
        credential = f.credential(self.request.principal, self.binding.cluster.uid)
        for profile in ('default', 'bearer'):
            binding = b.NativeBinding(self.binding.cluster, self.binding.ca, credential.binding)
            if profile != 'default':
                binding = replace(binding, authentication_profile=profile)
            stream = f.Stream(f.http(b'{}'), send_maximum=1)
            self.exchange(binding=binding, credential=credential, connector=f.owned_connector(stream))
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
                b._close_raw(owned)
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
            def connector(custody, context, cluster, deadline):
                custody.construct(MemoryStream, context, self.server, cluster.endpoint[8:].split(':')[0],
                    f.CERT_SOURCE_CLIENT, f.http(b'{}'), deadline)
            with self.subTest(endpoint=cluster.endpoint), self.assertRaises(b.Refusal):
                b.NativeTLS(b._OwnedConnector(connector)).exchange(request, time.monotonic() + 5, binding, self.credential)

    def test_loading_expiry_and_descriptor_cleanup_expiry_prevent_connector(self):
        original_load, original_seal, original_close = ssl.SSLContext.load_cert_chain, b.sealed, b._close_owned
        for stage in ('seal', 'load', 'cleanup'):
            clock, count = [100.0], [0]
            def seal(raw, **kwargs):
                fd = original_seal(raw, **kwargs)
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
        original_seal, original_close = b.sealed, b._OwnedDescriptor.close
        def seal(raw, **kwargs):
            fd = original_seal(raw, **kwargs)
            owned.append(fd)
            return fd
        def close(handle):
            fd = handle.fileno()
            if fd in owned:
                closes.append(fd)
                if closes.count(fd) == 1:
                    raise secondary
            return original_close(handle)
        before = descriptors()
        with patch.object(b, 'sealed', seal), patch.object(ssl.SSLContext, 'load_cert_chain', side_effect=primary), patch.object(
                b._OwnedDescriptor, 'close', close), self.assertRaises(KeyboardInterrupt) as caught:
            self.exchange()
        self.assertIs(caught.exception, primary)
        self.assertEqual(sorted(closes), sorted(owned * 2))
        self.assertEqual(descriptors(), before)
        self.assertEqual(self.contexts, [])

    def test_interrupt_on_first_installation_cleanup_line_retains_outer_descriptor_ownership(self):
        source, start = inspect.getsourcelines(b.NativeTLS._install_certificate)
        line = start + next(i for i, text in enumerate(source) if text.strip() == 'error = _close_owned(owned)')
        primary = KeyboardInterrupt('synthetic cleanup transition interruption')
        fired, owned = [], []
        original = b.sealed
        def seal(raw, **kwargs):
            fd = original(raw, **kwargs)
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

    def test_allocator_return_and_return_opcode_interrupts_retain_registered_ownership(self):
        returns = {instruction.offset for instruction in dis.get_instructions(b.sealed)
            if instruction.opname in ('RETURN_VALUE', 'RETURN_CONST')}
        for event in ('return', 'opcode'):
            for allocation in (1, 2):
                with self.subTest(event=event, allocation=allocation):
                    primary = KeyboardInterrupt('synthetic allocator transfer cancellation')
                    fired, visited = [], []
                    def trace(frame, kind, argument):
                        if frame.f_code is b.sealed.__code__ and frame.f_locals.get('custody') is not None:
                            frame.f_trace_opcodes = True
                            if (kind == 'return' if event == 'return' else kind == 'opcode' and frame.f_lasti in returns):
                                visited.append(True)
                                if len(visited) == allocation and not fired:
                                    custody = frame.f_locals['custody']
                                    self.assertTrue(any(not handle.closed for handle in custody.handles))
                                    fired.append(True)
                                    raise primary
                        return trace
                    before, previous = descriptors(), sys.gettrace()
                    with self.assertRaises(KeyboardInterrupt) as caught:
                        try:
                            sys.settrace(trace)
                            self.exchange()
                        finally:
                            sys.settrace(previous)
                    self.assertIs(caught.exception, primary)
                    self.assertEqual(fired, [True])
                    self.assertEqual(descriptors(), before)
                    self.assertEqual(self.contexts, [])

    def test_registration_entry_append_and_constructor_returns_retain_every_input(self):
        source, start = inspect.getsourcelines(b._DescriptorCustody.register)
        append = start + next(i for i, text in enumerate(source) if text.strip() == 'self.handles.append(handle)')
        entry = start + next(i for i, text in enumerate(source) if text.strip().startswith('handle = _OwnedDescriptor.__new__'))
        source, start = inspect.getsourcelines(b._OwnedDescriptor.__init__)
        initialize = start + next(i for i, text in enumerate(source) if text.strip().startswith('io.FileIO.__init__'))
        stages = ((b._DescriptorCustody.register.__code__, 'line', entry),
            (b._DescriptorCustody.register.__code__, 'line', append),
            (b._OwnedDescriptor.__init__.__code__, 'line', initialize),
            (b._OwnedDescriptor.__new__.__code__, 'return', None),
            (b._OwnedDescriptor.__init__.__code__, 'return', None),
            (b._DescriptorCustody.register.__code__, 'return', None))
        for code, event, line in stages:
            # Each certificate installation allocates two low and two high fds.
            for occurrence in (1, 2, 3, 4):
                with self.subTest(function=code.co_name, event=event, line=line, occurrence=occurrence):
                    primary = KeyboardInterrupt('synthetic registration transition interruption')
                    count, fired = [], []
                    def trace(frame, kind, argument):
                        if frame.f_code is code and kind == event and (line is None or frame.f_lineno == line):
                            count.append(True)
                            if len(count) == occurrence and not fired:
                                fired.append(True)
                                raise primary
                        return trace
                    before, previous = descriptors(), sys.gettrace()
                    with self.assertRaises(KeyboardInterrupt) as caught:
                        try:
                            sys.settrace(trace)
                            self.exchange()
                        finally:
                            sys.settrace(previous)
                    self.assertIs(caught.exception, primary)
                    self.assertEqual(fired, [True])
                    self.assertEqual(descriptors(), before)
                    self.assertEqual(self.contexts, [])

    def test_successful_owned_close_never_retries_a_reused_descriptor_number(self):
        custody = b._DescriptorCustody()
        first = b.sealed(b'public synthetic owned bytes', custody=custody)
        handle = next(handle for handle in custody.handles if not handle.closed)
        original, replacement, fired = b._OwnedDescriptor.close, [], []
        primary = KeyboardInterrupt('synthetic cancellation after completed close')
        def close(owner):
            original(owner)
            if owner is handle and not fired:
                fired.append(True)
                allocated = os.open('/dev/null', os.O_RDONLY | os.O_CLOEXEC)
                if allocated != first:
                    os.dup2(allocated, first, inheritable=False)
                    os.close(allocated)
                replacement.append(first)
                raise primary
        try:
            with patch.object(b._OwnedDescriptor, 'close', close):
                self.assertIs(b._close_owned(custody), primary)
                self.assertIs(b._close_owned(custody), primary)
            self.assertTrue(handle.closed)
            self.assertEqual(os.read(first, 1), b'')
            self.assertTrue(b.fcntl.fcntl(first, b.fcntl.F_GETFD) & b.fcntl.FD_CLOEXEC)
        finally:
            for fd in replacement:
                os.close(fd)
            b._close_owned(custody)

    def test_loading_interrupt_survives_second_cleanup_transition_interrupt(self):
        source, start = inspect.getsourcelines(b.NativeTLS._install_certificate)
        line = start + next(i for i, text in enumerate(source) if text.strip() == 'error = _close_owned(owned)')
        primary = KeyboardInterrupt('primary synthetic loader interruption')
        secondary = SystemExit('secondary synthetic cleanup transition interruption')
        fired = []
        def trace(frame, event, argument):
            if frame.f_code is b.NativeTLS._install_certificate.__code__ and event == 'line' and frame.f_lineno == line and not fired:
                self.assertIs(frame.f_locals['owned'].error, primary)
                fired.append(True)
                raise secondary
            return trace
        before, previous = descriptors(), sys.gettrace()
        with patch.object(ssl.SSLContext, 'load_cert_chain', side_effect=primary), self.assertRaises(KeyboardInterrupt) as caught:
            try:
                sys.settrace(trace)
                self.exchange()
            finally:
                sys.settrace(previous)
        self.assertIs(caught.exception, primary)
        self.assertEqual(fired, [True])
        self.assertEqual(descriptors(), before)
        self.assertEqual(self.contexts, [])

    def test_raw_memfd_and_duplicate_store_interrupts_recover_both_input_allocations(self):
        instructions = list(dis.get_instructions(b.sealed))
        stores = {instruction.argval: instruction.offset for previous, instruction in
            zip(instructions, instructions[1:]) if previous.opname == 'CALL' and
            instruction.opname == 'STORE_FAST' and instruction.argval in ('low', 'fd')}
        self.assertEqual(set(stores), {'low', 'fd'})
        for result in ('low', 'fd'):
            for allocation in (1, 2):
                with self.subTest(result=result, allocation=allocation):
                    primary = KeyboardInterrupt('synthetic raw syscall transfer cancellation')
                    fired, visited = [], []
                    def trace(frame, event, argument):
                        if frame.f_code is b.sealed.__code__ and frame.f_locals.get('custody') is not None:
                            frame.f_trace_opcodes = True
                            if event == 'opcode' and frame.f_lasti == stores[result]:
                                visited.append(True)
                                if len(visited) == allocation and not fired:
                                    custody = frame.f_locals['custody']
                                    identity = custody.allocations[-1]
                                    pending = [fd for fd in descriptors() if
                                        os.readlink('/proc/self/fd/' + str(fd)) == identity.link and
                                        not any(handle.original_fd == fd and not handle.closed for handle in custody.handles)]
                                    self.assertEqual(len(pending), 1, 'must reach the raw unregistered result')
                                    self.assertIsNone(frame.f_locals[result])
                                    fired.append(True)
                                    raise primary
                        return trace
                    before, previous = descriptors(), sys.gettrace()
                    with self.assertRaises(KeyboardInterrupt) as caught:
                        try:
                            sys.settrace(trace)
                            self.exchange()
                        finally:
                            sys.settrace(previous)
                    self.assertIs(caught.exception, primary)
                    self.assertEqual(fired, [True])
                    self.assertEqual(descriptors(), before)
                    self.assertEqual(self.contexts, [])

    def test_raw_result_recovery_preserves_unrelated_similar_and_reused_descriptors(self):
        instructions = list(dis.get_instructions(b.sealed))
        stores = {instruction.argval: instruction.offset for previous, instruction in
            zip(instructions, instructions[1:]) if previous.opname == 'CALL' and
            instruction.opname == 'STORE_FAST' and instruction.argval in ('low', 'fd')}
        for result in ('low', 'fd'):
            for allocation in (1, 2):
                with self.subTest(result=result, allocation=allocation):
                    primary = KeyboardInterrupt('synthetic raw result reclaimed before cancellation')
                    sentinel = b.sealed(b'public unrelated memory input')
                    replacements, visited, fired = [], [], []
                    def trace(frame, event, argument):
                        if frame.f_code is b.sealed.__code__ and frame.f_locals.get('custody') is not None:
                            frame.f_trace_opcodes = True
                            if event == 'opcode' and frame.f_lasti == stores[result]:
                                visited.append(True)
                                if len(visited) == allocation and not fired:
                                    custody = frame.f_locals['custody']
                                    identity = custody.allocations[-1]
                                    pending = [fd for fd in descriptors() if
                                        os.readlink('/proc/self/fd/' + str(fd)) == identity.link and
                                        not any(handle.original_fd == fd and not handle.closed for handle in custody.handles)]
                                    self.assertEqual(len(pending), 1)
                                    fd = pending[0]
                                    os.close(fd)
                                    replacement = os.open('/dev/null', os.O_RDONLY | os.O_CLOEXEC)
                                    if replacement != fd:
                                        os.dup2(replacement, fd, inheritable=False)
                                        os.close(replacement)
                                    replacements.append(fd)
                                    # An exact same name with a different inode must not
                                    # displace the identity already pinned before duplication.
                                    if result == 'fd':
                                        replacements.append(os.memfd_create(identity.name, os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING))
                                    else:
                                        replacements.append(os.memfd_create(identity.name + '-unrelated', os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING))
                                    fired.append(True)
                                    raise primary
                        return trace
                    before, previous = descriptors(), sys.gettrace()
                    try:
                        with self.assertRaises(KeyboardInterrupt) as caught:
                            try:
                                sys.settrace(trace)
                                self.exchange()
                            finally:
                                sys.settrace(previous)
                        self.assertIs(caught.exception, primary)
                        self.assertEqual(fired, [True])
                        self.assertEqual(os.read(replacements[0], 1), b'')
                        self.assertEqual(os.pread(sentinel, 100, 0), b'public unrelated memory input')
                        for fd in replacements:
                            self.assertTrue(b.fcntl.fcntl(fd, b.fcntl.F_GETFD) & b.fcntl.FD_CLOEXEC)
                        self.assertEqual({fd: value for fd, value in descriptors().items() if fd not in replacements}, before)
                    finally:
                        for fd in replacements:
                            os.close(fd)
                        os.close(sentinel)
                    self.assertEqual(self.contexts, [])

    def test_primary_loading_interrupt_survives_interruption_at_first_error_assignment(self):
        source, start = inspect.getsourcelines(b._DescriptorCustody.remember)
        line = start + next(i for i, text in enumerate(source) if text.strip() == 'self.error = error')
        primary = KeyboardInterrupt('synthetic original loader interrupt')
        secondary = SystemExit('synthetic interruption before first recording')
        fired = []
        def trace(frame, event, argument):
            if frame.f_code is b._DescriptorCustody.remember.__code__ and event == 'line' and frame.f_lineno == line and not fired:
                self.assertIs(frame.f_locals['error'], primary)
                self.assertIsNone(frame.f_locals['self'].error)
                fired.append(True)
                raise secondary
            return trace
        before, previous = descriptors(), sys.gettrace()
        with patch.object(ssl.SSLContext, 'load_cert_chain', side_effect=primary), self.assertRaises(KeyboardInterrupt) as caught:
            try:
                sys.settrace(trace)
                self.exchange()
            finally:
                sys.settrace(previous)
        self.assertIs(caught.exception, primary)
        self.assertIs(secondary.__context__, primary)
        self.assertEqual(fired, [True])
        self.assertEqual(descriptors(), before)
        self.assertEqual(self.contexts, [])

    def test_final_descriptor_cleanup_entry_preserves_primary_loading_interrupt(self):
        source, start = inspect.getsourcelines(b._cleanup_native)
        line = start + next(i for i, text in enumerate(source) if text.strip() == 'finally: _close_owned(owned)')
        primary = KeyboardInterrupt('synthetic loading interrupt at final cleanup')
        secondary = SystemExit('synthetic interruption at outer descriptor cleanup')
        fired = []
        def trace(frame, event, argument):
            if frame.f_code is b._cleanup_native.__code__ and event == 'line' and frame.f_lineno == line and not fired:
                self.assertIs(frame.f_locals['owned'].error, primary)
                fired.append(True)
                raise secondary
            return trace
        before, previous = descriptors(), sys.gettrace()
        with patch.object(ssl.SSLContext, 'load_cert_chain', side_effect=primary), self.assertRaises(KeyboardInterrupt) as caught:
            try:
                sys.settrace(trace)
                self.exchange()
            finally:
                sys.settrace(previous)
        self.assertIs(caught.exception, primary)
        self.assertEqual(fired, [True])
        self.assertEqual(descriptors(), before)
        self.assertEqual(self.contexts, [])

    def test_real_peer_success_interrupted_at_stream_cleanup_entry_still_closes_stream(self):
        source, start = inspect.getsourcelines(b._cleanup_native)
        line = start + next(i for i, text in enumerate(source) if text.strip() == 'try: _close_native(owned, connector)')
        primary = KeyboardInterrupt('synthetic cancellation at stream cleanup entry')
        fired = []
        def trace(frame, event, argument):
            if frame.f_code is b._cleanup_native.__code__ and event == 'line' and frame.f_lineno == line and not fired:
                self.assertTrue(self.streams[-1].sent)
                self.assertIs(frame.f_locals['owned'].stream, self.streams[-1])
                self.assertFalse(self.streams[-1].closed)
                fired.append(True)
                raise primary
            return trace
        before, previous = descriptors(), sys.gettrace()
        with self.assertRaises(KeyboardInterrupt) as caught:
            try:
                sys.settrace(trace)
                self.exchange()
            finally:
                sys.settrace(previous)
        self.assertIs(caught.exception, primary)
        self.assertEqual(fired, [True])
        self.assertTrue(self.streams[-1].closed)
        self.assertEqual(descriptors(), before)

    def test_loading_interrupt_survives_second_interrupt_at_fallback_descriptor_cleanup_entry(self):
        source, start = inspect.getsourcelines(b.NativeTLS.exchange)
        lines = [start + i for i, text in enumerate(source) if "_cleanup_native(locals().get('owned'), self.connector, sys._getframe())" in text]
        self.assertEqual(len(lines), 3, 'inner, guarded fallback and last cleanup pass')
        primary = KeyboardInterrupt('synthetic loading interrupt reaches fallback')
        secondary = SystemExit('synthetic second interruption at fallback descriptor cleanup')
        fired = []
        def trace(frame, event, argument):
            if frame.f_code is b.NativeTLS.exchange.__code__ and event == 'line' and frame.f_lineno == lines[1] and not fired:
                self.assertIs(frame.f_locals['owned'].error, primary)
                fired.append(True)
                raise secondary
            return trace
        before, previous = descriptors(), sys.gettrace()
        with patch.object(ssl.SSLContext, 'load_cert_chain', side_effect=primary), self.assertRaises(KeyboardInterrupt) as caught:
            try:
                sys.settrace(trace)
                self.exchange()
            finally:
                sys.settrace(previous)
        self.assertIs(caught.exception, primary)
        self.assertEqual(fired, [True])
        self.assertEqual(descriptors(), before)
        self.assertEqual(self.contexts, [])

    def test_second_interrupt_at_fallback_stream_cleanup_entry_releases_open_peer_and_inputs(self):
        source, start = inspect.getsourcelines(b.NativeTLS.exchange)
        lines = [start + i for i, text in enumerate(source) if "_cleanup_native(locals().get('owned'), self.connector, sys._getframe())" in text]
        self.assertEqual(len(lines), 3, 'inner, guarded fallback and last cleanup pass')
        primary = KeyboardInterrupt('synthetic cancellation before inner stream cleanup')
        secondary = SystemExit('synthetic second cancellation before fallback stream cleanup')
        fired, extra = [], []
        def trace(frame, event, argument):
            if frame.f_code is b.NativeTLS.exchange.__code__ and event == 'line':
                if frame.f_lineno == lines[0] and not fired:
                    self.assertTrue(self.streams[-1].sent)
                    self.assertFalse(self.streams[-1].closed)
                    extra.append(b.sealed(b'public synthetic still-owned cleanup input', custody=frame.f_locals['owned']))
                    fired.append('primary')
                    raise primary
            return trace
        # Raising from a trace hook disables that trace. Re-arm it when the
        # outer guard records the first cancellation, before its own close line.
        close_source, close_start = inspect.getsourcelines(b._cleanup_native)
        close_line = close_start + next(i for i, text in enumerate(close_source) if text.strip() == 'try: _close_native(owned, connector)')
        original_remember = b._DescriptorCustody.remember
        def second_trace(frame, event, argument):
            if frame.f_code is b._cleanup_native.__code__ and event == 'line' and frame.f_lineno == close_line and len(fired) == 1:
                self.assertFalse(self.streams[-1].closed)
                self.assertEqual(os.pread(extra[0], 100, 0), b'public synthetic still-owned cleanup input')
                fired.append('secondary')
                raise secondary
            return second_trace
        def remember(custody, error):
            result = original_remember(custody, error)
            if error is primary and fired == ['primary']:
                sys.settrace(second_trace)
                caller = sys._getframe(1)
                while caller is not None:
                    caller.f_trace = second_trace
                    caller = caller.f_back
            return result
        before, previous = descriptors(), sys.gettrace()
        with patch.object(b._DescriptorCustody, 'remember', remember), self.assertRaises(KeyboardInterrupt) as caught:
            try:
                sys.settrace(trace)
                self.exchange()
            finally:
                sys.settrace(previous)
        self.assertIs(caught.exception, primary)
        self.assertEqual(fired, ['primary', 'secondary'])
        self.assertTrue(self.streams[-1].closed)
        self.assertEqual(descriptors(), before)

    def test_custody_backed_duplicate_write_chmod_and_sealing_failures_reach_owned_inputs(self):
        for stage in ('duplicate', 'write', 'chmod', 'add-seals', 'get-seals'):
            for occurrence in (1, 2):
                for interrupt in (False, True):
                    with self.subTest(stage=stage, occurrence=occurrence, interrupt=interrupt):
                        problem = KeyboardInterrupt('synthetic owned allocator cancellation') if interrupt else OSError('synthetic owned allocator failure')
                        original = b.fcntl.fcntl if stage in ('duplicate', 'add-seals', 'get-seals') else os.write if stage == 'write' else os.fchmod
                        target, name = (b.fcntl, 'fcntl') if stage in ('duplicate', 'add-seals', 'get-seals') else (os, 'write' if stage == 'write' else 'fchmod')
                        command = {'duplicate': b.fcntl.F_DUPFD_CLOEXEC, 'add-seals': b.fcntl.F_ADD_SEALS,
                            'get-seals': b.fcntl.F_GET_SEALS}.get(stage)
                        visited, fired = [], []
                        def action(fd, *args):
                            if command is None or args[0] == command:
                                visited.append(True)
                                if len(visited) == occurrence:
                                    self.assertTrue(os.readlink('/proc/self/fd/' + str(fd)).startswith('/memfd:recovery-fixture-input-'))
                                    fired.append(True)
                                    raise problem
                            return original(fd, *args)
                        before = descriptors()
                        with patch.object(target, name, action):
                            if interrupt:
                                with self.assertRaises(KeyboardInterrupt) as caught:
                                    self.exchange()
                                self.assertIs(caught.exception, problem)
                            else:
                                self.assert_refused()
                        self.assertEqual(fired, [True], 'failure injection must reach the custody-backed allocator')
                        self.assertEqual(descriptors(), before)
                        self.assertEqual(self.contexts, [])

    def test_independent_certificate_key_and_envelope_size_limits_refuse_before_loader(self):
        # Valid PEM grammar and <=16 certificates isolate byte bounds from format/count.
        certificate = b'-----BEGIN CERTIFICATE-----\n' + (b'A' * 64 + b'\n') * 760 + b'-----END CERTIFICATE-----\n'
        key = b'-----BEGIN PRIVATE KEY-----\n' + (b'A' * 64 + b'\n') * 253 + b'-----END PRIVATE KEY-----\n'
        self.assertGreater(len(certificate), 49152)
        self.assertLess(len(certificate), 65536 - len(f.CERT_SOURCE_CLIENT_KEY) - 2000)
        self.assertGreater(len(key), 16384)
        self.assertLess(len(key), 65536 - len(f.CERT_SOURCE_CLIENT) - 2000)
        cases = [('certificate', envelope(certificate=certificate)), ('key', envelope(key=key))]
        # Within material/count bounds; JSON whitespace alone crosses envelope bound.
        cases.append(('envelope', envelope() + b' ' * 65536))
        for limit, raw in cases:
            if limit != 'envelope':
                self.assertLess(len(raw), 65536)
            with self.subTest(limit=limit):
                credential = f.credential(self.request.principal, self.binding.cluster.uid, raw)
                before = descriptors()
                with patch.object(ssl.SSLContext, 'load_cert_chain') as load:
                    self.assert_refused(binding=replace(self.binding, credential=credential.binding), credential=credential)
                    load.assert_not_called()
                self.assertEqual(descriptors(), before)
        self.assertEqual(self.contexts, [])

    def test_sealing_failure_closes_first_descriptor_and_cancellation_never_dispatches(self):
        original = b.sealed
        for problem in (OSError('synthetic sealing failure'), KeyboardInterrupt('synthetic sealing interrupt')):
            calls = []
            def seal(raw, **kwargs):
                if calls:
                    raise problem
                fd = original(raw, **kwargs)
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
        original_seal, original_close = b.sealed, b._OwnedDescriptor.close
        def seal(raw, **kwargs):
            fd = original_seal(raw, **kwargs)
            owned.append(fd)
            return fd
        def close(handle):
            fd = handle.fileno()
            if fd in owned and fd not in failed:
                failed.add(fd)
                raise OSError('synthetic close failure')
            return original_close(handle)
        before = descriptors()
        with patch.object(b, 'sealed', seal), patch.object(b._OwnedDescriptor, 'close', close):
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
        before = descriptors()
        with self.assertRaises(KeyboardInterrupt) as caught:
            self.exchange(connector=f.owned_connector(stream, connector))
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
                            self.exchange(connector=f.owned_connector(stream), deadline=101.0)
                        self.assertIs(caught.exception, primary)
                    else:
                        self.assert_refused(connector=f.owned_connector(stream), deadline=101.0)
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
        source = '''import importlib.util, os, pathlib, sys
path = pathlib.Path(sys.argv[1])
spec = importlib.util.spec_from_file_location('isolated_fd3_certificate', path)
t = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = t
spec.loader.exec_module(t)
case = t.CertificateTests('test_exact_certificate_and_key_authenticate_real_peer_without_authorization')
case.setUp()
try:
    try:
        t.b.fcntl.fcntl(3, t.b.fcntl.F_GETFD)
    except OSError as exc:
        assert exc.errno == t.b.errno.EBADF
    else:
        raise AssertionError('isolated fixture requires initially closed fd 3')
    data = b'public synthetic parent fd 3 sentinel bytes'
    sentinel = case.directory / 'parent-fd-three'
    sentinel.write_bytes(data)
    fd = os.open(sentinel, os.O_RDONLY | os.O_CLOEXEC | os.O_NONBLOCK)
    assert fd == 3, 'fixture must explicitly occupy fd 3'
    flags = (t.b.fcntl.fcntl(3, t.b.fcntl.F_GETFD), t.b.fcntl.fcntl(3, t.b.fcntl.F_GETFL))
    before = t.descriptors()
    case.exchange()
    assert t.descriptors() == before
    assert os.pread(3, len(data) + 1, 0) == data
    assert (t.b.fcntl.fcntl(3, t.b.fcntl.F_GETFD), t.b.fcntl.fcntl(3, t.b.fcntl.F_GETFL)) == flags
    os.close(3)
finally:
    case.doCleanups()
'''
        result = subprocess.run(('/usr/bin/python3.14', '-I', '-S', '-B', '-c', source, __file__),
            stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=10)
        self.assertEqual(result.returncode, 0, result.stderr.decode())

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


    def test_private_connector_refuses_raw_factories_before_credential_or_context_access(self):
        raw = lambda *args: self.fail('raw factory must never run')
        with patch.object(b.Credential, 'descriptor', side_effect=AssertionError('credential access')), patch.object(
                b.NativeTLS, '_context', side_effect=AssertionError('context access')):
            self.assert_refused(connector=raw)

    def test_owned_connector_constructor_and_return_interruptions_close_registered_real_peer(self):
        source, start = inspect.getsourcelines(b._DescriptorCustody.construct)
        entry = start + next(i for i, text in enumerate(source) if text.strip().startswith('stream_type.__init__'))
        stages = ((b._DescriptorCustody.construct.__code__, 'line', entry),
            (MemoryStream.__init__.__code__, 'return', None),
            (b._DescriptorCustody.construct.__code__, 'return', None),
            (self.connector.__func__.__code__, 'return', None),
            (b._OwnedConnector.__call__.__code__, 'return', None))
        for code, event, line in stages:
            with self.subTest(function=code.co_name, event=event):
                primary = KeyboardInterrupt('synthetic owned acquisition transition')
                fired, owned_streams = [], []
                original_close = MemoryStream.close
                def close(stream):
                    owned_streams.append(stream)
                    original_close(stream)
                def trace(frame, kind, argument):
                    if frame.f_code is code and kind == event and (line is None or frame.f_lineno == line) and not fired:
                        custody = frame.f_locals.get('custody', frame.f_locals.get('self'))
                        if type(custody) is b._DescriptorCustody:
                            self.assertIsNotNone(custody.stream)
                        fired.append(True)
                        raise primary
                    return trace
                before, previous = descriptors(), sys.gettrace()
                with patch.object(MemoryStream, 'close', close), self.assertRaises(KeyboardInterrupt) as caught:
                    try:
                        sys.settrace(trace)
                        self.exchange()
                    finally:
                        sys.settrace(previous)
                self.assertIs(caught.exception, primary)
                self.assertEqual(fired, [True])
                self.assertEqual(len(owned_streams), 1)
                self.assertTrue(owned_streams[0].closed)
                self.assertEqual(descriptors(), before)

    def test_prepared_stream_handoff_store_interrupt_preserves_shared_owner(self):
        stream = f.Stream(f.http(b'{}'))
        primary = KeyboardInterrupt('synthetic prepared handoff opcode')
        target = next(i.offset for i in dis.get_instructions(b.NativeTLS._acquire_exchange)
            if i.opname == 'STORE_ATTR' and i.argval == 'stream')
        fired = []
        def trace(frame, event, argument):
            if frame.f_code is b.NativeTLS._acquire_exchange.__code__:
                frame.f_trace_opcodes = True
                if event == 'opcode' and frame.f_lasti == target and not fired:
                    self.assertIsNone(frame.f_locals['owned'].stream)
                    self.assertIs(frame.f_locals['owned'].connector.prepared, stream)
                    fired.append(True)
                    raise primary
            return trace
        before, previous = descriptors(), sys.gettrace()
        with self.assertRaises(KeyboardInterrupt) as caught:
            try:
                sys.settrace(trace)
                self.exchange(connector=f.owned_connector(stream))
            finally:
                sys.settrace(previous)
        self.assertIs(caught.exception, primary)
        self.assertEqual(fired, [True])
        self.assertTrue(stream.closed)
        self.assertEqual(descriptors(), before)

    def test_recovery_registration_rechecks_expected_identity_before_adopting_replacement(self):
        before = descriptors()
        custody = b._DescriptorCustody()
        allocation = b._MemoryAllocation()
        custody.allocations.append(allocation)
        fd = os.memfd_create(allocation.name, os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING)
        allocation.observe(fd)
        original, replacements, expected_identities = custody.register, [], []
        def register(raw, expected=None):
            expected_identities.append(expected)
            os.close(raw)
            replacement = os.open('/dev/null', os.O_RDONLY | os.O_CLOEXEC)
            if replacement != raw:
                os.dup2(replacement, raw, inheritable=False)
                os.close(replacement)
            replacements.append(raw)
            return original(raw, expected)
        custody.register = register
        try:
            self.assertIsInstance(b._close_owned(custody), b.Refusal)
            self.assertEqual(expected_identities, [allocation.identity])
            self.assertEqual(custody.handles, [])
            self.assertEqual(os.read(fd, 1), b'')
            self.assertTrue(b.fcntl.fcntl(fd, b.fcntl.F_GETFD) & b.fcntl.FD_CLOEXEC)
        finally:
            for raw in replacements:
                os.close(raw)
            b._close_owned(custody)
        self.assertEqual(descriptors(), before)

    def test_preacquired_recovery_scans_cover_isolated_descriptor_exhaustion_and_raw_results(self):
        source = '''import dis, importlib.util, json, os, pathlib, resource, sys
from unittest.mock import patch
path = pathlib.Path(sys.argv[1])
spec = importlib.util.spec_from_file_location('isolated_exhausted_certificate', path)
t = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = t
spec.loader.exec_module(t)
b = t.b
limit = resource.getrlimit(resource.RLIMIT_NOFILE)
results = []
sentinel = b.sealed(b'public unrelated descriptor-limit sentinel')
try:
    for mode in ('initial-refusal', 'raw-low', 'duplicate-refusal', 'raw-high'):
        before = t.descriptors()
        custody = b._DescriptorCustody()
        primary = KeyboardInterrupt('isolated exhausted raw-result transition')
        instructions = list(dis.get_instructions(b.sealed))
        stores = {i.argval: i.offset for previous, i in zip(instructions, instructions[1:])
            if previous.opname == 'CALL' and i.opname == 'STORE_FAST' and i.argval in ('low', 'fd')}
        original_memory, original_fcntl = os.memfd_create, b.fcntl.fcntl
        fired, reached = [], []
        def memory(name, flags):
            if mode in ('raw-low', 'duplicate-refusal'):
                high = max(t.descriptors())
                resource.setrlimit(resource.RLIMIT_NOFILE, (high + 2, limit[1]))
            value = original_memory(name, flags)
            reached.append('memory')
            return value
        def control(fd, command, *arguments):
            if mode == 'raw-high' and command == b.fcntl.F_DUPFD_CLOEXEC:
                high = max(t.descriptors())
                resource.setrlimit(resource.RLIMIT_NOFILE, (high + 2, limit[1]))
            if mode == 'duplicate-refusal' and command == b.fcntl.F_DUPFD_CLOEXEC:
                try:
                    return original_fcntl(fd, command, *arguments)
                except OSError as error:
                    assert error.errno == b.errno.EMFILE, 'must exhaust the actual final descriptor slot'
                    raise
            return original_fcntl(fd, command, *arguments)
        def trace(frame, event, argument):
            if frame.f_code is b.sealed.__code__:
                frame.f_trace_opcodes = True
                target = stores['fd' if mode == 'raw-high' else 'low']
                if event == 'opcode' and frame.f_lasti == target and not fired:
                    fired.append(True)
                    assert frame.f_locals['custody'].allocations[-1].scanners
                    raise primary
            return trace
        try:
            if mode == 'initial-refusal':
                resource.setrlimit(resource.RLIMIT_NOFILE, (max(before) + 1, limit[1]))
            with patch.object(os, 'memfd_create', memory), patch.object(b.fcntl, 'fcntl', control):
                if mode in ('raw-low', 'raw-high'):
                    sys.settrace(trace)
                try:
                    b.sealed(b'public bounded memory input', custody=custody)
                except BaseException as error:
                    if mode in ('raw-low', 'raw-high'):
                        assert error is primary
                    else:
                        assert isinstance(error, b.Refusal)
                else:
                    raise AssertionError('exhaustion/interrupt must refuse')
                finally:
                    sys.settrace(None)
        finally:
            resource.setrlimit(resource.RLIMIT_NOFILE, limit)
        assert resource.getrlimit(resource.RLIMIT_NOFILE) == limit
        assert t.descriptors() == before, (mode, t.descriptors(), before)
        assert os.pread(sentinel, 100, 0) == b'public unrelated descriptor-limit sentinel'
        if mode == 'initial-refusal':
            assert reached == []
            assert len(custody.allocations[0].scanners) == 1, 'must fail after reserving one scanner'
        if mode != 'initial-refusal':
            assert reached == ['memory']
            assert custody.handles, 'must recover/adopt actual allocated memory'
        if mode in ('raw-low', 'raw-high'):
            assert fired == [True]
        results.append({'mode': mode, 'noLeak': True, 'rawTransitionInjected': bool(fired)})
finally:
    resource.setrlimit(resource.RLIMIT_NOFILE, limit)
    os.close(sentinel)
print(json.dumps(results))
'''
        result = subprocess.run(('/usr/bin/python3.14', '-I', '-S', '-B', '-c', source, __file__),
            stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=10)
        self.assertEqual(result.returncode, 0, result.stderr.decode())
        report = json.loads(result.stdout)
        self.assertEqual(len(report), 4)
        self.assertTrue(all(item['noLeak'] for item in report))
        self.assertEqual(sum(item['rawTransitionInjected'] for item in report), 2)

    def test_recovery_reservation_constructor_and_return_interruptions_close_registered_scans(self):
        source, start = inspect.getsourcelines(b._MemoryAllocation.__init__)
        reserve = start + next(i for i, text in enumerate(source) if text.strip().startswith('self.scanners.append'))
        for event, occurrence in (('line', 2), ('line', 3), ('return', 1)):
            with self.subTest(event=event, occurrence=occurrence):
                custody = b._DescriptorCustody()
                primary = KeyboardInterrupt('synthetic recovery reservation cancellation')
                visits, fired = [], []
                def trace(frame, kind, argument):
                    if frame.f_code is b._MemoryAllocation.__init__.__code__ and kind == event and (event == 'return' or frame.f_lineno == reserve):
                        visits.append(True)
                        if len(visits) == occurrence and not fired:
                            self.assertIs(custody.allocations[-1], frame.f_locals['self'])
                            self.assertGreater(len(custody.allocations[-1].scanners), 0)
                            fired.append(True)
                            raise primary
                    return trace
                before, previous = descriptors(), sys.gettrace()
                with self.assertRaises(KeyboardInterrupt) as caught:
                    try:
                        sys.settrace(trace)
                        b.sealed(b'public unacquired input', custody=custody)
                    finally:
                        sys.settrace(previous)
                self.assertIs(caught.exception, primary)
                self.assertEqual(fired, [True])
                self.assertEqual(descriptors(), before)
                self.assertEqual(custody.handles, [])

    def test_caller_handled_interrupt_does_not_replace_current_loading_interrupt(self):
        source, start = inspect.getsourcelines(b.NativeTLS._acquire_exchange)
        line = start + next(i for i, text in enumerate(source) if text.strip() == 'owned.connector = self.connector')
        boundary_line = start + next(i for i, text in enumerate(source) if text.strip() == 'owned.caller_context = caller_context')
        for transition in ('loading', 'acquisition-boundary', 'acquisition-entry'):
            with self.subTest(transition=transition):
                old = KeyboardInterrupt('caller handled interruption')
                primary = KeyboardInterrupt('current exchange ' + transition + ' interruption')
                before, previous, fired = descriptors(), sys.gettrace(), []
                stream = f.Stream(f.http(b'{}'))
                def trace(frame, event, argument):
                    target = boundary_line if transition == 'acquisition-boundary' else line
                    if frame.f_code is b.NativeTLS._acquire_exchange.__code__ and event == 'line' and frame.f_lineno == target and not fired:
                        if transition == 'acquisition-boundary':
                            self.assertFalse(hasattr(frame.f_locals['owned'], 'caller_context'))
                        else:
                            self.assertIs(frame.f_locals['owned'].caller_context, old)
                        fired.append(True)
                        raise primary
                    return trace
                try:
                    raise old
                except KeyboardInterrupt:
                    with patch.object(ssl.SSLContext, 'load_cert_chain', side_effect=primary), self.assertRaises(KeyboardInterrupt) as caught:
                        try:
                            if transition != 'loading':
                                sys.settrace(trace)
                            self.exchange(connector=f.owned_connector(stream))
                        finally:
                            sys.settrace(previous)
                self.assertIs(caught.exception, primary)
                self.assertIs(primary.__context__, old)
                self.assertEqual(fired, [True] if transition != 'loading' else [])
                self.assertTrue(stream.closed)
                self.assertEqual(stream.sent, b'')
                self.assertEqual(descriptors(), before)

        for old in (KeyboardInterrupt('handled caller cancellation before success'), ValueError('handled caller ordinary exception')):
            with self.subTest(success_inside=type(old).__name__):
                before = descriptors()
                try:
                    raise old
                except BaseException:
                    response = self.exchange()
                self.assertEqual(response, a.Response(0, 200, b'{}'))
                self.assertTrue(self.streams[-1].closed)
                self.assertEqual(descriptors(), before)

    def test_two_interruptions_inside_stream_close_preserve_first_and_release_all_inputs(self):
        stream = f.Stream(f.http(b'{}'))
        primary, secondary = KeyboardInterrupt('first close interruption'), SystemExit('second close interruption')
        calls, extra = [], []
        def close():
            calls.append(True)
            if len(calls) < 3:
                raise primary if len(calls) == 1 else secondary
            stream.closed = True
        stream.close = close
        def acquire(custody, context, cluster, deadline):
            extra.append(b.sealed(b'public independent cleanup input', custody=custody))
        before = descriptors()
        with self.assertRaises(KeyboardInterrupt) as caught:
            self.exchange(connector=b._OwnedConnector(acquire, prepared=stream))
        self.assertIs(caught.exception, primary)
        self.assertEqual(len(calls), 3)
        self.assertTrue(stream.closed)
        self.assertEqual(len(extra), 1)
        self.assertEqual(descriptors(), before)

    def test_idempotent_c_owned_stream_close_preserves_reused_unrelated_descriptor(self):
        # The C FileIO state is authoritative even if Python bookkeeping has
        # not run when cancellation follows the actual descriptor release.
        class DescriptorStream(f.Stream):
            def __init__(self):
                super().__init__(f.http(b'{}'))
                self.file = io.FileIO('/dev/null', 'rb')
                self.original_fd = self.file.fileno()
                self.calls = 0
                self.bookkeeping = False
            @property
            def closed(self):
                return hasattr(self, 'file') and self.file.closed
            @closed.setter
            def closed(self, value):
                pass
            def close(self):
                self.calls += 1
                self.file.close()
                self.bookkeeping = True
        before = descriptors()
        stream = DescriptorStream()
        fd, replacements, fired = stream.original_fd, [], []
        primary = KeyboardInterrupt('after C-level stream release')
        source, start = inspect.getsourcelines(DescriptorStream.close)
        line = start + next(i for i, text in enumerate(source) if text.strip() == 'self.bookkeeping = True')
        def trace(frame, event, argument):
            if frame.f_code is DescriptorStream.close.__code__ and event == 'line' and frame.f_lineno == line and not fired:
                self.assertTrue(stream.closed)
                replacement = os.open('/dev/null', os.O_RDONLY | os.O_CLOEXEC)
                if replacement != fd:
                    os.dup2(replacement, fd, inheritable=False)
                    os.close(replacement)
                replacements.append(fd)
                fired.append(True)
                raise primary
            return trace
        previous = sys.gettrace()
        try:
            with self.assertRaises(KeyboardInterrupt) as caught:
                try:
                    sys.settrace(trace)
                    self.exchange(connector=f.owned_connector(stream))
                finally:
                    sys.settrace(previous)
            self.assertIs(caught.exception, primary)
            self.assertEqual(fired, [True])
            self.assertEqual(stream.calls, 1)
            self.assertFalse(stream.bookkeeping)
            self.assertEqual(os.read(fd, 1), b'')
            self.assertTrue(b.fcntl.fcntl(fd, b.fcntl.F_GETFD) & b.fcntl.FD_CLOEXEC)
        finally:
            for raw in replacements:
                os.close(raw)
            if not stream.closed:
                stream.file.close()
        self.assertEqual(descriptors(), before)

    def test_compact_base64_and_leading_blank_lines_authenticate_and_install_original_bytes(self):
        def compact(raw):
            lines = raw.splitlines()
            return lines[0] + b'\n' + b''.join(lines[1:-1]) + b'\n' + lines[-1] + b'\n'
        for form in ('compact', 'leading-blank', 'both'):
            with self.subTest(form=form):
                certificate, key = f.CERT_SOURCE_CLIENT, f.CERT_SOURCE_CLIENT_KEY
                if form in ('compact', 'both'):
                    certificate, key = compact(certificate), compact(key)
                if form in ('leading-blank', 'both'):
                    certificate, key = b'\n\r\n' + certificate, b'\r\n\n' + key
                credential = f.credential(self.request.principal, self.binding.cluster.uid, envelope(certificate, key))
                actual, original = [], ssl.SSLContext.load_cert_chain
                def load(context, certfile, keyfile, password=None):
                    actual.extend(Path(path).read_bytes() for path in (certfile, keyfile))
                    return original(context, certfile, keyfile, password=password)
                before = descriptors()
                with patch.object(ssl.SSLContext, 'load_cert_chain', load):
                    response = self.exchange(binding=replace(self.binding, credential=credential.binding), credential=credential)
                self.assertEqual(response, a.Response(0, 200, b'{}'))
                self.assertEqual(actual, [certificate, key])
                self.assertEqual(self.streams[-1].peer, ssl.PEM_cert_to_DER_cert(f.CERT_SOURCE_CLIENT.decode('ascii')))
                self.assertNotIn(b'Authorization:', self.streams[-1].sent)
                self.assertTrue(self.streams[-1].closed)
                self.assertEqual(descriptors(), before)

    def test_custody_constructor_entry_partial_and_return_interruptions_close_prepared_c_owned_stream(self):
        class DescriptorStream(f.Stream):
            def __init__(self):
                super().__init__(f.http(b'{}'))
                self.file = io.FileIO(b.sealed(b'public synthetic prepared stream'), 'rb', closefd=True)
                self.close_calls = 0
            @property
            def closed(self):
                return hasattr(self, 'file') and self.file.closed
            @closed.setter
            def closed(self, value):
                pass
            def close(self):
                self.close_calls += 1
                self.file.close()
        source, start = inspect.getsourcelines(b._DescriptorCustody.__init__)
        partial = start + next(i for i, text in enumerate(source) if text.strip() == 'self.allocations = []')
        for transition in ('entry', 'partial', 'return'):
            with self.subTest(transition=transition):
                before = descriptors()
                stream = DescriptorStream()
                primary, fired = KeyboardInterrupt('synthetic custody initialization ' + transition), []
                def trace(frame, event, argument):
                    if frame.f_code is b._DescriptorCustody.__init__.__code__ and not fired:
                        hit = event == 'call' if transition == 'entry' else event == 'return' if transition == 'return' else event == 'line' and frame.f_lineno == partial
                        if hit:
                            self.assertFalse(stream.closed)
                            if transition == 'partial':
                                self.assertEqual(frame.f_locals['self'].handles, [])
                                self.assertFalse(hasattr(frame.f_locals['self'], 'allocations'))
                            fired.append(transition)
                            raise primary
                    return trace
                previous = sys.gettrace()
                with patch.object(b.Credential, 'descriptor', side_effect=AssertionError('no credential work')) as descriptor, patch.object(
                        b.NativeTLS, '_exchange', side_effect=AssertionError('no native dispatch')) as dispatch, self.assertRaises(KeyboardInterrupt) as caught:
                    try:
                        sys.settrace(trace)
                        self.exchange(connector=f.owned_connector(stream))
                    finally:
                        sys.settrace(previous)
                self.assertIs(caught.exception, primary)
                self.assertEqual(fired, [transition])
                self.assertTrue(stream.closed)
                self.assertEqual(stream.close_calls, 1)
                descriptor.assert_not_called()
                dispatch.assert_not_called()
                self.assertEqual(descriptors(), before)

    def test_trusted_stream_constructor_failure_after_c_owned_acquisition_releases_partial_stream(self):
        for primary in (KeyboardInterrupt('synthetic partial constructor interruption'), ValueError('synthetic partial constructor failure')):
            with self.subTest(error=type(primary).__name__):
                partials = []
                class PartialStream(f.Stream):
                    def __init__(self):
                        super().__init__(f.http(b'{}'))
                        self.file = io.FileIO(b.sealed(b'public synthetic partial constructor'), 'rb', closefd=True)
                        self.close_calls = 0
                        partials.append(self)
                        raise primary
                    @property
                    def closed(self):
                        return hasattr(self, 'file') and self.file.closed
                    @closed.setter
                    def closed(self, value):
                        pass
                    def close(self):
                        self.close_calls += 1
                        file = getattr(self, 'file', None)
                        if file is not None:
                            file.close()
                def acquire(custody, context, cluster, deadline):
                    custody.construct(PartialStream)
                before = descriptors()
                expected = KeyboardInterrupt if isinstance(primary, KeyboardInterrupt) else b.Refusal
                with patch.object(b.NativeTLS, '_exchange', side_effect=AssertionError('no native dispatch')) as dispatch, self.assertRaises(expected) as caught:
                    self.exchange(connector=b._OwnedConnector(acquire))
                if expected is KeyboardInterrupt:
                    self.assertIs(caught.exception, primary)
                self.assertEqual(len(partials), 1)
                self.assertTrue(partials[0].closed)
                self.assertEqual(partials[0].close_calls, 1)
                self.assertEqual(partials[0].sent, b'')
                dispatch.assert_not_called()
                self.assertEqual(descriptors(), before)

    def test_lost_raw_result_and_second_scan_or_registration_interrupt_preserve_exact_descriptors(self):
        instructions = list(dis.get_instructions(b.sealed))
        stores = {instruction.argval: instruction.offset for previous, instruction in zip(instructions, instructions[1:])
            if previous.opname == 'CALL' and instruction.opname == 'STORE_FAST' and instruction.argval in ('low', 'fd')}
        source, start = inspect.getsourcelines(b._MemoryAllocation.recover)
        sites = {name: start + next(i for i, text in enumerate(source) if text.strip() == statement) for name, statement in
            (('scan', 'fd = int(entry.name)'), ('registration', 'custody.register(fd, identity)'))}
        for result in ('low', 'fd'):
            for allocation in (1, 2):
                for transition, line in sites.items():
                    with self.subTest(result=result, allocation=allocation, transition=transition):
                        sentinel = io.FileIO(b.sealed(b'public unrelated caller descriptor'), 'rb', closefd=True)
                        before, previous = descriptors(), sys.gettrace()
                        primary, secondary = KeyboardInterrupt('synthetic lost raw syscall result'), SystemExit('synthetic interrupted recovery ' + transition)
                        fired, visited = [], []
                        original = b._DescriptorCustody.remember
                        def second_trace(frame, event, argument):
                            if frame.f_code is b._MemoryAllocation.recover.__code__ and event == 'line' and frame.f_lineno == line and fired == ['primary']:
                                if transition == 'registration':
                                    fd = frame.f_locals['fd']
                                    self.assertEqual(os.readlink('/proc/self/fd/' + str(fd)), frame.f_locals['self'].link)
                                fired.append('secondary')
                                raise secondary
                            return second_trace
                        def remember(custody, error):
                            value = original(custody, error)
                            if error is primary and fired == ['primary']:
                                sys.settrace(second_trace)
                                caller = sys._getframe(1)
                                while caller is not None:
                                    caller.f_trace = second_trace
                                    caller = caller.f_back
                            return value
                        def trace(frame, event, argument):
                            if frame.f_code is b.sealed.__code__ and frame.f_locals.get('custody') is not None:
                                frame.f_trace_opcodes = True
                                if event == 'opcode' and frame.f_lasti == stores[result]:
                                    visited.append(True)
                                    if len(visited) == allocation and not fired:
                                        self.assertIsNone(frame.f_locals[result])
                                        identity = frame.f_locals['custody'].allocations[-1]
                                        self.assertTrue([fd for fd in descriptors() if os.readlink('/proc/self/fd/' + str(fd)) == identity.link])
                                        fired.append('primary')
                                        raise primary
                            return trace
                        try:
                            with patch.object(b._DescriptorCustody, 'remember', remember), self.assertRaises(KeyboardInterrupt) as caught:
                                try:
                                    sys.settrace(trace)
                                    self.exchange()
                                finally:
                                    sys.settrace(previous)
                            self.assertIs(caught.exception, primary)
                            self.assertEqual(fired, ['primary', 'secondary'])
                            self.assertEqual(descriptors(), before)
                            self.assertEqual(os.pread(sentinel.fileno(), 100, 0), b'public unrelated caller descriptor')
                            self.assertEqual(self.contexts, [])
                        finally:
                            sentinel.close()

    def test_private_acquire_postconditions_refuse_before_dispatch_and_close_all_known_streams(self):
        class UnsupportedStream(f.Stream):
            _native_owned_stream = False
        for violation in ('return-object', 'unsupported-stream', 'missing-stream', 'missing-method'):
            with self.subTest(violation=violation):
                prepared = f.Stream(f.http(b'{}'))
                unsupported = UnsupportedStream(f.http(b'{}'))
                def acquire(custody, context, cluster, deadline):
                    if violation == 'return-object':
                        return object()
                    if violation == 'unsupported-stream':
                        custody.stream = unsupported
                    elif violation == 'missing-stream':
                        custody.stream = None
                    else:
                        prepared.send = None
                before = descriptors()
                with patch.object(b.NativeTLS, '_exchange', side_effect=AssertionError('no native dispatch')) as dispatch:
                    self.assert_refused(connector=b._OwnedConnector(acquire, prepared=prepared))
                dispatch.assert_not_called()
                self.assertTrue(prepared.closed)
                self.assertEqual(prepared.sent, b'')
                if violation == 'unsupported-stream':
                    self.assertTrue(unsupported.closed)
                    self.assertEqual(unsupported.sent, b'')
                self.assertEqual(descriptors(), before)

    def test_two_cleanup_invocation_interruptions_preserve_peer_and_input_ownership(self):
        source, start = inspect.getsourcelines(b.NativeTLS.exchange)
        lines = [start + i for i, text in enumerate(source) if "_cleanup_native(locals().get('owned'), self.connector, sys._getframe())" in text]
        self.assertEqual(len(lines), 3)
        primary, secondary = KeyboardInterrupt('synthetic first cleanup invocation'), SystemExit('synthetic second cleanup invocation')
        original, fired, extra = b._cleanup_native, [], []
        def trace(frame, event, argument):
            if frame.f_code is b.NativeTLS.exchange.__code__ and event == 'line' and frame.f_lineno == lines[0] and not fired:
                self.assertFalse(self.streams[-1].closed)
                self.assertTrue(self.streams[-1].sent)
                extra.append(b.sealed(b'public synthetic cleanup handoff input', custody=frame.f_locals['owned']))
                fired.append('primary')
                raise primary
            return trace
        def cleanup(owned, connector, caller_context):
            if fired == ['primary']:
                self.assertFalse(self.streams[-1].closed)
                self.assertEqual(os.pread(extra[0], 100, 0), b'public synthetic cleanup handoff input')
                fired.append('secondary')
                raise secondary
            return original(owned, connector, caller_context)
        before, previous = descriptors(), sys.gettrace()
        with patch.object(b, '_cleanup_native', cleanup), self.assertRaises(KeyboardInterrupt) as caught:
            try:
                sys.settrace(trace)
                self.exchange()
            finally:
                sys.settrace(previous)
        self.assertIs(caught.exception, primary)
        self.assertIs(secondary.__context__, primary)
        self.assertEqual(fired, ['primary', 'secondary'])
        self.assertTrue(self.streams[-1].closed)
        self.assertEqual(descriptors(), before)

    def test_fixture_native_profile_and_ca_refusals_close_preallocated_stream(self):
        for change in ({'authentication_profile': 'unknown'}, {'ca': b'invalid CA'}):
            boundary, _, _, native = f.compose(self.directory)
            binding = replace(boundary.native_bindings[0], **change)
            request = boundary.requests[3]
            credential = boundary._credential(request.principal, binding.cluster.uid)
            before = descriptors()
            with self.assertRaises(b.Refusal):
                native.exchange(request, time.monotonic() + 5, binding, credential)
            self.assertEqual(len(native.streams), 1)
            self.assertTrue(native.streams[0].closed)
            self.assertEqual(native.contexts, [])
            self.assertEqual(descriptors(), before)


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
                        def connector(custody, context, cluster, limit):
                            self.assertEqual(context.get_ca_certs(binary_form=True), [ssl.PEM_cert_to_DER_cert(binding.ca.decode())])
                            stream = custody.construct(MemoryStream, context, servers[cluster.uid], cluster.endpoint[8:].split(':')[0],
                                getattr(f, 'CERT_' + label + '_CLIENT'), f.http(expected.body, expected.status), limit)
                            streams.append(stream)
                            peers.append((cluster.uid, credential.binding.principal, stream.peer))
                        result = b.NativeTLS(b._OwnedConnector(connector)).exchange(request, deadline, binding, credential)
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

    def test_fixture_collections_distinguish_none_empty_and_mismatched_lengths(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            boundary, _, _, _ = f.compose(root)
            self.assertEqual(tuple(v.authentication_profile for v in boundary.native_bindings), ('bearer', 'bearer'))
            for name in ('native_profiles', 'native_secrets', 'native_cas'):
                for values in ((), ('bearer',), ('bearer',) * 3):
                    with self.subTest(name=name, length=len(values)), self.assertRaises(b.Refusal):
                        f.compose(root, **{name: values})
            for values in ((), (f.CA_SOURCE,), (f.CA_SOURCE,) * 3):
                with self.subTest(fixture_cas=len(values)), self.assertRaises(b.Refusal):
                    f.fixture(root, native_cas=values)

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
