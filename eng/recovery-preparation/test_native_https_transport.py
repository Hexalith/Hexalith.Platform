"""Fixed HTTPS mechanics over fake sockets only; zero network/config reads."""

import os
import stat
import types
import tempfile
import marshal
import importlib.util
import json
from pathlib import Path
import socket
import ssl
import unittest
from unittest.mock import patch


def read_source_bytes(path, maximum=1048576):
    """Read bounded regular source through no-follow directory descriptors."""
    path = Path(path)
    if not path.is_absolute() or '..' in path.parts or maximum <= 0:
        raise ValueError('source-custody-refused')
    directory = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
    source = None
    try:
        for part in path.parts[1:-1]:
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC,
                            dir_fd=directory)
            os.close(directory)
            directory = child
            info = os.fstat(directory)
            mode = stat.S_IMODE(info.st_mode)
            sticky_root = info.st_uid == 0 and bool(mode & stat.S_ISVTX)
            if info.st_uid not in (0, os.getuid()) or (mode & 0o022 and not sticky_root):
                raise ValueError('source-custody-refused')
        source = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC,
                         dir_fd=directory)
        before = os.fstat(source)
        if (not stat.S_ISREG(before.st_mode) or before.st_nlink != 1 or
                before.st_uid not in (0, os.getuid()) or stat.S_IMODE(before.st_mode) & 0o022 or
                not 0 <= before.st_size <= maximum):
            raise ValueError('source-custody-refused')
        raw = bytearray()
        while len(raw) <= maximum:
            chunk = os.read(source, min(65536, maximum + 1 - len(raw)))
            if not chunk:
                break
            raw.extend(chunk)
        after = os.fstat(source)
        fields = ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns', 'st_ctime_ns',
                  'st_mode', 'st_uid', 'st_gid', 'st_nlink')
        if (len(raw) != before.st_size or len(raw) > maximum or
                any(getattr(before, key) != getattr(after, key) for key in fields)):
            raise ValueError('source-custody-refused')
        return bytes(raw)
    finally:
        if source is not None:
            os.close(source)
        os.close(directory)


def load_source(path, name):
    """Compile exactly the checked source bytes; never consult a .pyc cache."""
    raw = read_source_bytes(path)
    module = types.ModuleType(name)
    module.__file__ = str(path)
    exec(compile(raw, str(path), 'exec'), module.__dict__)
    return module


https = load_source(Path(__file__).with_name('native_https_transport.py'), 'https_under_test')
SECRET = 'synthetic-sensitive-token-never-output'


class FakeStream:
    """Inert socket shape; no underlying OS file descriptor is ever created."""

    def __init__(self, response=b'', tick=0, fail=None):
        self.response = bytearray(response)
        self.written = bytearray()
        self.timeouts = []
        self.closed = False
        self.clock = 0
        self.tick = tick
        self.fail = fail
        self.connects = []
        self.handshakes = 0
        self.reads = 0

    def settimeout(self, value):
        self.timeouts.append(value)

    def connect(self, endpoint):
        self.connects.append(endpoint)
        self.clock += self.tick
        if self.fail == 'connect': raise OSError(SECRET)

    def do_handshake(self):
        self.handshakes += 1
        self.clock += self.tick
        if self.fail == 'handshake': raise OSError(SECRET)

    def send(self, raw):
        self.clock += self.tick
        if self.fail == 'send': raise OSError(SECRET)
        self.written.extend(raw)
        return len(raw)

    def recv(self, maximum):
        self.reads += 1
        self.clock += self.tick
        if self.fail == 'recv': raise OSError(SECRET)
        raw = bytes(self.response[:maximum])
        del self.response[:maximum]
        return raw

    def close(self):
        self.closed = True


def response(body=b'{}', status=200, extra=b'', length=None):
    return (b'HTTP/1.1 ' + str(status).encode() + b' Fixture\r\nContent-Type: application/json\r\n'
            b'Content-Length: ' + str(len(body) if length is None else length).encode() +
            b'\r\n' + extra + b'\r\n' + body)


class NativeHttpsTests(unittest.TestCase):
    def setUp(self):
        self.request = https.request_plan()[0]
        self.transport = https.NativeHttpsTransport()
        self.socket_patch = patch.object(socket, 'socket', side_effect=AssertionError('network forbidden'))
        self.socket_patch.start()
        self.addCleanup(self.socket_patch.stop)

    def exchange(self, raw, **kwargs):
        stream = FakeStream(raw, **kwargs)
        with patch.object(https.time, 'monotonic', side_effect=lambda: stream.clock):
            try:
                result = self.transport._exchange(self.request, stream, 15)
            except https.Refusal as error:
                self.assertNotIn(SECRET, str(error))
                self.assertTrue(stream.closed)
                raise
        self.assertTrue(stream.closed)
        return result, stream

    def test_fixed_get_host_accept_headers_and_no_auth_proxy_or_command(self):
        result, stream = self.exchange(response())
        self.assertEqual(result.status, 200)
        self.assertEqual(result.chunks, (b'{}',))
        self.assertEqual(bytes(stream.written), (
            'GET /api/v1/namespaces/kube-system HTTP/1.1\r\nHost: 192.168.1.30:6443\r\n'
            'Accept: application/json, text/plain\r\nAccept-Encoding: identity\r\nConnection: close\r\n\r\n').encode())
        self.assertTrue(all(0 < timeout <= 15 for timeout in stream.timeouts))

    def test_public_transport_refuses_before_socket_config_or_credentials(self):
        with patch.object(Path, 'read_bytes', side_effect=AssertionError('file access forbidden')):
            with self.assertRaisesRegex(https.Refusal, 'live-activation-disabled'):
                self.transport.request(self.request, 15, 100)

    def test_fixed_sibling_loader_compiles_source_instead_of_eligible_unbound_cache(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'native_measurement_adapter.py'
            source.write_bytes(read_source_bytes(Path(__file__).with_name(source.name)))
            transport = root / 'native_https_transport.py'
            transport.write_bytes(read_source_bytes(Path(__file__).with_name(transport.name)))
            info = source.stat()
            cache = Path(importlib.util.cache_from_source(str(source)))
            cache.parent.mkdir(mode=0o700)
            header = (importlib.util.MAGIC_NUMBER + (0).to_bytes(4, 'little') +
                      int(info.st_mtime).to_bytes(4, 'little') + info.st_size.to_bytes(4, 'little'))
            malicious = read_source_bytes(source) + b'\nUNBOUND_CACHE_EXECUTED = True\n'
            cache.write_bytes(header + marshal.dumps(compile(malicious, str(source), 'exec')))
            loaded = load_source(transport, 'synthetic_transport_cache_fixture')
            self.assertFalse(hasattr(loaded._adapter, 'UNBOUND_CACHE_EXECUTED'))
            self.assertEqual(loaded._adapter.RID, '20261015t162756z')

    def test_fixed_sibling_loader_refuses_symlink_fifo_oversize_and_symlinked_ancestor(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            actual = root / 'actual'
            actual.mkdir(mode=0o700)
            source = actual / 'native_measurement_adapter.py'
            source.write_bytes(b'raise AssertionError("unsafe source executed")\n')
            transport = actual / 'native_https_transport.py'
            transport.write_bytes(read_source_bytes(Path(__file__).with_name(transport.name)))
            for mode in ('symlink', 'fifo', 'oversize'):
                sibling = root / 'native_measurement_adapter.py'
                candidate = root / 'native_https_transport.py'
                if not candidate.exists(): candidate.write_bytes(read_source_bytes(transport))
                if mode == 'symlink': sibling.symlink_to(source)
                elif mode == 'fifo': os.mkfifo(sibling, 0o600)
                else:
                    with sibling.open('wb') as stream: stream.truncate(1048577)
                with self.subTest(mode=mode), self.assertRaises((OSError, ValueError)):
                    load_source(candidate, 'synthetic_unsafe_sibling')
                sibling.unlink()
            alias = root / 'alias'
            alias.symlink_to(actual, target_is_directory=True)
            with self.assertRaises((OSError, ValueError)):
                load_source(alias / transport.name, 'synthetic_unsafe_ancestor')

    def test_changed_selector_method_identity_or_limit_refuses_before_send(self):
        for key, value in (('path', '/api/v1/secrets'), ('method', 'POST'), ('name', SECRET)):
            request = dict(self.request)
            request[key] = value
            stream = FakeStream(response())
            with self.assertRaises(https.Refusal): self.transport._exchange(request, stream, 15)
            self.assertEqual(stream.written, b'')
            self.assertTrue(stream.closed)

    def test_redirect_status_encoding_pagination_headers_and_duplicate_lengths_refuse(self):
        for raw in (response(status=301), response(status=302), response(status=307),
                    response(extra=b'Location: https://other.invalid/\r\n'),
                    response(extra=b'Transfer-Encoding: chunked\r\n'),
                    response(extra=b'Content-Encoding: gzip\r\n'),
                    response(extra=b'Content-Length: 2\r\n'),
                    response(extra=b' Content-Length: 2\r\n')):
            with self.subTest(raw=raw[:30]):
                with self.assertRaises(https.Refusal): self.exchange(raw)

    def test_vertical_tab_form_feed_and_link_continuation_headers_are_refused(self):
        for extra in (b'X-Diagnostic: \x0b' + SECRET.encode() + b'\r\n',
                      b'X-Diagnostic: ' + SECRET.encode() + b'\x0c\r\n',
                      b'Link: </api/v1/namespaces?continue=opaque>; rel="next"\r\n'):
            with self.subTest(extra=extra[:20]), self.assertRaises(https.Refusal):
                self.exchange(response(extra=extra))
        with self.assertRaises(https.Refusal):
            self.exchange(b'HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n'
                          b'Content-Length: \x0b2\x0c\r\n\r\n{}')
        result, _ = self.exchange(b'HTTP/1.1 200 OK\r\nContent-Type: \tapplication/json \t\r\n'
                                  b'Content-Length: \t2 \t\r\n\r\n{}')
        self.assertEqual(result.chunks, (b'{}',))

    def test_oversized_headers_stop_at_cap_without_body_buffering(self):
        raw = b'HTTP/1.1 200 OK\r\nX-Diagnostic: ' + b'x' * (https.MAX_HEADERS + 50)
        stream = FakeStream(raw)
        with patch.object(https.time, 'monotonic', return_value=0):
            with self.assertRaises(https.Refusal): self.transport._exchange(self.request, stream, 15)
        self.assertEqual(stream.reads, https.MAX_HEADERS)
        self.assertTrue(stream.closed)
        self.assertGreater(len(stream.response), 0)

    def test_content_length_overflow_is_rejected_before_body_reads(self):
        raw = response(body=SECRET.encode(), length=https.MAX_BODY + 1)
        stream = FakeStream(raw)
        with patch.object(https.time, 'monotonic', return_value=0):
            with self.assertRaises(https.Refusal): self.transport._exchange(self.request, stream, 15)
        self.assertEqual(stream.response, SECRET.encode())

    def test_missing_negative_conflicting_and_malformed_content_length_refuse(self):
        for raw in (b'HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n\r\n{}',
                    b'HTTP/1.1 200 OK\r\nContent-Length: -1\r\n\r\n',
                    b'HTTP/1.1 200 OK\r\nContent-Length: 0002\r\n\r\n{}',
                    b'HTTP/1.1 200 OK\r\nContent-Length: 1e5\r\n\r\n{}',
                    response(body=b'{', length=2), b'HTTP/1.1 200 OK\r\n'):
            with self.assertRaises(https.Refusal): self.exchange(raw)

    def test_header_and_body_slow_drip_share_one_absolute_deadline(self):
        # A tiny per-byte advance exhausts the fixed deadline during headers.
        with self.assertRaises(https.Refusal): self.exchange(response(), tick=1)
        stream = FakeStream(response(body=b'x' * 50000))
        real_recv = stream.recv
        def slow_body(maximum):
            if maximum > 1:
                stream.clock += 8
            return real_recv(maximum)
        stream.recv = slow_body
        with patch.object(https.time, 'monotonic', side_effect=lambda: stream.clock):
            with self.assertRaises(https.Refusal): self.transport._exchange(self.request, stream, 15)
        self.assertTrue(stream.closed)
        self.assertLess(len(stream.response), 50000)

    def test_deadline_is_checked_after_last_body_read_before_success(self):
        stream = FakeStream(response())
        real_recv = stream.recv
        def expire_after_body(maximum):
            raw = real_recv(maximum)
            if maximum > 1: stream.clock = 16
            return raw
        stream.recv = expire_after_body
        with patch.object(https.time, 'monotonic', side_effect=lambda: stream.clock):
            with self.assertRaises(https.Refusal): self.transport._exchange(self.request, stream, 15)

    def test_error_diagnostics_do_not_escape_and_stream_closes(self):
        for fail in ('send', 'recv'):
            with self.assertRaisesRegex(https.Refusal, '^bounded-https-response-refused$'):
                self.exchange(response(), fail=fail)

    def test_fixed_numeric_socket_tls_sni_and_one_handshake_with_mocked_io(self):
        raw = FakeStream()
        tls = FakeStream()
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
        with patch.object(https.socket, 'socket', return_value=raw) as create, \
                patch.object(context, 'wrap_socket', return_value=tls) as wrap, \
                patch.object(https.socket, 'getaddrinfo', side_effect=AssertionError('DNS forbidden')), \
                patch.object(https.time, 'monotonic', return_value=0):
            self.assertIs(self.transport._connect(context, 15), tls)
        create.assert_called_once_with(socket.AF_INET, socket.SOCK_STREAM)
        self.assertEqual(raw.connects, [('192.168.1.30', 6443)])
        wrap.assert_called_once_with(raw, server_hostname='192.168.1.30', do_handshake_on_connect=False)
        self.assertEqual(tls.handshakes, 1)

    def test_insecure_tls_refuses_before_any_socket(self):
        for context in (None, ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)):
            if context is not None: context.check_hostname = False
            with self.assertRaises(https.Refusal): self.transport._connect(context, 15)

    def test_tls_connect_handshake_timeout_and_diagnostic_failures_close_once_without_retry(self):
        for fail in ('connect', 'handshake', 'timeout'):
            raw = FakeStream(fail='connect' if fail == 'connect' else None)
            tls = FakeStream(fail='handshake' if fail == 'handshake' else None,
                             tick=16 if fail == 'timeout' else 0)
            context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
            with patch.object(https.socket, 'socket', return_value=raw) as create, \
                    patch.object(context, 'wrap_socket', return_value=tls), \
                    patch.object(https.time, 'monotonic', side_effect=lambda: tls.clock):
                with self.assertRaises(https.Refusal) as error: self.transport._connect(context, 15)
            self.assertNotIn(SECRET, str(error.exception))
            self.assertTrue(raw.closed)
            self.assertEqual(create.call_count, 1)
            self.assertLessEqual(tls.handshakes, 1)

    def test_connect_and_tls_cleanup_exceptions_never_escape_fixed_refusal(self):
        for failure in ('connect', 'handshake'):
            raw = FakeStream(fail='connect' if failure == 'connect' else None)
            tls = FakeStream(fail='handshake')
            raw.close = lambda: (_ for _ in ()).throw(OSError(SECRET))
            tls.close = lambda: (_ for _ in ()).throw(OSError(SECRET))
            context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
            with patch.object(https.socket, 'socket', return_value=raw), \
                    patch.object(context, 'wrap_socket', return_value=tls), \
                    patch.object(https.time, 'monotonic', return_value=0):
                with self.assertRaisesRegex(https.Refusal, '^tls-or-connect-failure$') as error:
                    self.transport._connect(context, 15)
            self.assertNotIn(SECRET, str(error.exception))


if __name__ == '__main__':
    unittest.main(verbosity=2)
