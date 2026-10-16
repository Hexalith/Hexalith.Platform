#!/usr/bin/env python3
"""Dormant bounded native HTTPS; synthetic stream qualification only.

No public live activation, credential/configuration loader, proxy, DNS,
redirect, retry or external executable exists. Fixed sibling source is the
only source dependency; the supplemental runtime inventory binds both files.
"""

import os
import stat
import types
from pathlib import Path
import re
import socket
import ssl
import time

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


_adapter = load_source(Path(__file__).with_name('native_measurement_adapter.py'),
                       'hexalith_native_measurement_fixed_dependency')
Refusal = _adapter.Refusal
require = _adapter.require
request_plan = _adapter.request_plan
Response = _adapter.Response
_remaining = _adapter._remaining
HOST, PORT = _adapter.HOST, _adapter.PORT
MAX_BODY, MAX_HEADERS = _adapter.MAX_BODY, _adapter.MAX_HEADERS


class NativeHttpsTransport:
    """Dormant fixed-IP HTTPS implementation. Public activation is closed.

    _connect and _exchange are reviewable mechanics for a future separately
    bound activation. Qualification calls _exchange with fake streams only.
    No CA store, client key, config, bearer token, proxy or environment is read.
    The unresolved credentials and authority cannot be filled by this class.
    """

    def request(self, request, deadline, maximum):
        raise Refusal('live-activation-disabled-before-configuration-access')

    def _connect(self, context, deadline):
        require(type(context) is ssl.SSLContext and context.check_hostname and
                context.verify_mode == ssl.CERT_REQUIRED, 'strict-tls-required')
        raw = None
        try:
            raw = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            raw.settimeout(_remaining(deadline))
            raw.connect((HOST, PORT))
            raw.settimeout(_remaining(deadline))
            stream = context.wrap_socket(raw, server_hostname=HOST, do_handshake_on_connect=False)
            try:
                stream.settimeout(_remaining(deadline))
                stream.do_handshake()
                _remaining(deadline)
                return stream
            except Exception:
                stream.close()
                raise
        except Exception:
            if raw is not None:
                try:
                    raw.close()
                except Exception:
                    pass
            raise Refusal('tls-or-connect-failure') from None

    def _exchange(self, request, stream, deadline, maximum=MAX_BODY):
        """Absolute deadline covers send, header and body reads, including drip."""
        try:
            require(request in request_plan() and type(maximum) is int and 0 <= maximum <= MAX_BODY,
                    'fixed-request-required')
            wire = ('GET ' + request['path'] + ' HTTP/1.1\r\nHost: ' + HOST + ':' + str(PORT) +
                    '\r\nAccept: application/json, text/plain\r\nAccept-Encoding: identity\r\nConnection: close\r\n\r\n').encode('ascii')
            offset = 0
            while offset < len(wire):
                stream.settimeout(_remaining(deadline))
                sent = stream.send(wire[offset:])
                require(type(sent) is int and 0 < sent <= len(wire) - offset, 'request-write')
                offset += sent
            headers = bytearray()
            while not headers.endswith(b'\r\n\r\n'):
                require(len(headers) < MAX_HEADERS, 'header-overflow')
                stream.settimeout(_remaining(deadline))
                chunk = stream.recv(1)
                require(type(chunk) is bytes and len(chunk) == 1, 'incomplete-headers')
                headers.extend(chunk)
            _remaining(deadline)
            lines = bytes(headers[:-4]).decode('ascii').split('\r\n')
            match = re.fullmatch(r'HTTP/1\.[01] (200|404)(?: [\x20-\x7e]{0,80})?', lines[0])
            require(match is not None, 'unexpected-http-status')
            status = int(match.group(1))
            fields = {}
            for line in lines[1:]:
                require(':' in line and not line.startswith((' ', '\t')), 'http-header-schema')
                name, value = line.split(':', 1)
                require(all(ch == '\t' or 32 <= ord(ch) < 127 for ch in value), 'http-header-schema')
                name, value = name.lower(), value.strip(' \t')
                require(re.fullmatch(r'[a-z0-9-]+', name) and name not in fields and
                        all(ch == '\t' or 32 <= ord(ch) < 127 for ch in value), 'http-header-schema')
                fields[name] = value
            require(not ({'location', 'transfer-encoding', 'content-encoding', 'link'} & fields.keys()),
                    'unsupported-http-response')
            length = fields.get('content-length', '')
            require(re.fullmatch(r'0|[1-9][0-9]{0,6}', length) is not None, 'content-length-required')
            length = int(length)
            require(length <= maximum, 'body-overflow')
            content_type = fields.get('content-type', '').split(';', 1)[0].strip(' \t')
            require(content_type in ('application/json', 'text/plain'), 'content-type')
            body = bytearray()
            while len(body) < length:
                stream.settimeout(_remaining(deadline))
                chunk = stream.recv(min(16384, length - len(body)))
                require(type(chunk) is bytes and 0 < len(chunk) <= length - len(body), 'incomplete-body')
                body.extend(chunk)
            _remaining(deadline)
            return Response(status, content_type, (bytes(body),))
        except Exception:
            raise Refusal('bounded-https-response-refused') from None
        finally:
            try:
                stream.close()
            except Exception:
                raise Refusal('bounded-https-response-refused') from None

