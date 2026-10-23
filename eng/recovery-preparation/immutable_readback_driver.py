#!/usr/bin/env python3
"""Independent immutable reads through explicitly bound, dormant transports."""
from dataclasses import dataclass
from datetime import datetime, timezone
import os
import stat
import types
from pathlib import Path
import sys
import hashlib
import hmac
import re
import ssl
import xml.etree.ElementTree as ET

# Bootstrap the fixed sibling from stable no-follow bytes, never a pyc cache.
_path = Path(__file__).absolute().with_name('recovery_execution_boundary.py')
_name = 'hexalith_recovery_fixed_boundary'
def _read_boundary_source():
    owned, error = [], None
    try:
        directory = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
        owned.append(directory)
        for part in _path.parts[1:-1]:
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC, dir_fd=directory)
            owned.append(child)
            os.close(directory)
            owned.remove(directory)
            directory = child
            info = os.fstat(directory)
            mode = stat.S_IMODE(info.st_mode)
            if info.st_uid not in (0, os.getuid()) or (mode & 0o022 and not (info.st_uid == 0 and mode & stat.S_ISVTX)):
                raise ValueError('recovery-execution-boundary-refused')
        fd = os.open(_path.name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC, dir_fd=directory)
        owned.append(fd)
        before = os.fstat(fd)
        if (not stat.S_ISREG(before.st_mode) or before.st_uid not in (0, os.getuid()) or
                before.st_nlink != 1 or stat.S_IMODE(before.st_mode) & 0o022 or not 0 < before.st_size <= 2 * 1024 * 1024):
            raise ValueError('recovery-execution-boundary-refused')
        raw = bytearray()
        while len(raw) <= 2 * 1024 * 1024:
            chunk = os.read(fd, min(65536, 2 * 1024 * 1024 + 1 - len(raw)))
            if not chunk:
                break
            raw.extend(chunk)
        after = os.fstat(fd)
        if len(raw) != before.st_size or any(getattr(before, k) != getattr(after, k) for k in
                ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns', 'st_ctime_ns', 'st_mode', 'st_uid', 'st_nlink')):
            raise ValueError('recovery-execution-boundary-refused')
    except BaseException as exc:
        error = exc
    for fd in reversed(owned):
        for _ in range(2):
            try:
                os.close(fd)
                break
            except BaseException as cleanup:
                if error is None or isinstance(error, Exception) and not isinstance(cleanup, Exception):
                    error = cleanup
    if error is not None:
        if not isinstance(error, Exception):
            raise error.with_traceback(error.__traceback__)
        raise ValueError('recovery-execution-boundary-refused') from None
    return bytes(raw)


_raw = _read_boundary_source()
_identity = hashlib.sha256(_raw).hexdigest()
if _name in sys.modules:
    b = sys.modules[_name]
    if getattr(b, '__file__', None) != str(_path) or getattr(b, '__source_sha256__', None) != _identity:
        raise ValueError('recovery-execution-boundary-refused')
else:
    b = types.ModuleType(_name)
    b.__file__, b.__source_sha256__ = str(_path), _identity
    sys.modules[_name] = b
    try:
        exec(compile(_raw, str(_path), 'exec'), b.__dict__)
    except BaseException as exc:
        sys.modules.pop(_name, None)
        if not isinstance(exc, Exception):
            raise
        raise ValueError('recovery-execution-boundary-refused') from None

a = b.a



@dataclass(frozen=True)
class ProviderBinding:
    store: str
    endpoint: str
    ca_sha256: str
    reader: b.CredentialBinding
    writer_principal: str
    writer_identity_sha256: str
    writer_custody_sha256: str
    writer_secret_sha256: str


class ImmutableProvider:
    """Trusted injected interface, never a successful placeholder implementation.

    open_exact(artifact, credential, absolute_deadline) returns an owned handle
    with raw `metadata: bytes`, read(maximum, deadline) and close(). The provider
    must enforce the same deadline within every blocking call. Reads return no
    more than maximum bytes; EOF is b''. No writer credential is available.
    """
    def open_exact(self, artifact, credential, absolute_deadline):
        raise b.Refusal(b.FAILURE)


def _s3_error(error):
    if not isinstance(error, Exception):
        raise error.with_traceback(error.__traceback__)
    raise b.Refusal(b.FAILURE) from None


def _s3_release(streams, deadline, error=None, *, only=None):
    """Nonblocking connector-owned closes, independently attempted at most twice.

    Even a close that succeeds on the second attempt refuses the operation.
    The original cancellation takes precedence over every cleanup failure.
    """
    for stream in reversed(tuple(streams)):
        if only is not None and stream is not only:
            continue
        for _ in range(2):
            try:
                stream.close()
                break
            except BaseException as cleanup:
                if error is None or isinstance(error, Exception) and not isinstance(cleanup, Exception):
                    error = cleanup
        streams.remove(stream)  # Retain ownership until all close attempts finish.
    try:
        b.remaining(deadline)
    except BaseException as cleanup:
        if error is None or isinstance(error, Exception) and not isinstance(cleanup, Exception):
            error = cleanup
    if error is not None:
        _s3_error(error)


def _s3_call(stream, deadline, method, argument):
    stream.settimeout(b.remaining(deadline))
    b.remaining(deadline)
    result = getattr(stream, method)(argument)
    b.remaining(deadline)
    return result


def _s3_headers(stream, deadline):
    # Read no body bytes while validating metadata. Storage is bounded even for
    # slow drip headers, duplicate fields, folded lines and non-ASCII input.
    raw = bytearray()
    while not raw.endswith(b'\r\n\r\n'):
        b.require(len(raw) < b.MAX_HEADERS)
        chunk = _s3_call(stream, deadline, 'recv', 1)
        b.require(type(chunk) is bytes and len(chunk) == 1)
        raw.extend(chunk)
    lines = raw[:-4].decode('ascii').split('\r\n')
    b.require(len(lines) <= 128 and re.fullmatch(r'HTTP/1\.[01] 200(?: [\x20-\x7e]{0,80})?', lines[0]))
    headers = {}
    for line in lines[1:]:
        b.require(':' in line and not line.startswith((' ', '\t')))
        name, value = line.split(':', 1)
        name = name.lower()
        b.require(re.fullmatch(r'[a-z0-9-]+', name) and name not in headers and
                  all(ch == '\t' or 32 <= ord(ch) < 127 for ch in value))
        headers[name] = value.strip(' \t')
    b.require(not {'location', 'transfer-encoding', 'content-encoding', 'content-range',
                   'trailer', 'upgrade', 'link'} & headers.keys() and
              headers.get('connection', 'close').lower() == 'close' and
              re.fullmatch(r'0|[1-9][0-9]{0,8}', headers.get('content-length', '')))
    return headers


def _s3_instant(value):
    # S3 emits zero fractional seconds in some retention representations.
    b.require(type(value) is str and len(value) <= 40 and re.fullmatch(
        r'\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.0{1,9})?Z', value))
    normalized = value.split('.', 1)[0] + 'Z' if '.' in value else value
    a.timestamp(normalized)
    return normalized


def _s3_retention(raw, expected):
    b.require(type(raw) is bytes and 0 < len(raw) <= b.MAX_HEADERS and
              b'<!' not in raw and b'&' not in raw)
    # ElementTree also auto-detects UTF-16. Keep declaration/DTD/PI checks on
    # exactly the UTF-8 wire profile they examine, rejecting NUL/control bytes.
    document_text = raw.decode('utf-8')
    b.require(all(ch in '\t\r\n' or ord(ch) >= 32 for ch in document_text) and '\x00' not in document_text)
    # Permit only an optional XML declaration; no DTD, entities, comments, PIs
    # or mixed/nested content. ElementTree never receives an unbounded document.
    document = raw
    if document.startswith(b'<?xml '):
        declaration = re.match(br'<\?xml version=[\'"]1\.0[\'"](?: encoding=[\'"]UTF-8[\'"])?\?>', document)
        b.require(declaration is not None)
        document = document[declaration.end():]
    b.require(b'<?' not in document)
    # Parse the original declaration too: stripping it before parsing could
    # turn malformed declaration quotes/encoding into an accepted document.
    root = ET.fromstring(raw)
    namespace = '{http://s3.amazonaws.com/doc/2006-03-01/}' if root.tag.startswith('{') else ''
    b.require(root.tag == namespace + 'Retention' and not root.attrib and
              not (root.text or '').strip() and not (root.tail or '').strip() and len(root) == 2)
    values = {}
    for child in root:
        b.require(child.tag in (namespace + 'Mode', namespace + 'RetainUntilDate') and
                  child.tag not in values and not child.attrib and not len(child) and
                  not (child.tail or '').strip())
        values[child.tag] = child.text
    b.require(values.get(namespace + 'Mode') == 'COMPLIANCE' and
              _s3_instant(values.get(namespace + 'RetainUntilDate')) == expected)


class _S3ReadHandle:
    """Exclusive sequential ownership of an exact body; no spool/object buffer.

    One caller owns the handle and calls read/close sequentially. The persistent
    owned-stream list stays releasable through cleanup and provider handoff.
    """
    def __init__(self, owned, artifact, metadata, deadline):
        self._owned, self._artifact, self._deadline = owned, artifact, deadline
        self.metadata = a.canonical(metadata)
        self._count, self._hash, self._failed = 0, hashlib.sha256(), False

    def read(self, maximum, deadline):
        try:
            b.require(bool(self._owned) and not self._failed and type(maximum) is int and
                      1 <= maximum <= 65536)
            b.remaining(deadline)
            deadline = min(deadline, self._deadline)
            b.remaining(deadline)
            left = self._artifact.size - self._count
            if left:
                chunk = _s3_call(self._owned[0], deadline, 'recv', min(maximum, left))
                b.require(type(chunk) is bytes and 0 < len(chunk) <= min(maximum, left))
                self._count += len(chunk)
                self._hash.update(chunk)
                b.remaining(deadline)
                return chunk
            tail = _s3_call(self._owned[0], deadline, 'recv', 1)
            b.require(type(tail) is bytes and tail == b'' and
                      self._count == self._artifact.size and self._hash.hexdigest() == self._artifact.sha256)
            _s3_release(self._owned, deadline)
            return b''
        except BaseException as error:
            self._failed = True
            _s3_release(self._owned, self._deadline, error)

    def close(self):
        try:
            _s3_release(self._owned, self._deadline)
            b.require(not self._failed)
        except BaseException as error:
            self._failed = True
            _s3_release(self._owned, self._deadline, error)


class S3ImmutableProvider(ImmutableProvider):
    """Read-only SigV4 HTTPS; never selects a connector or ambient credentials.

    connector(context, exact_endpoint, absolute_deadline) returns an owned stream
    implementing bounded settimeout/send/recv and nonblocking close. It must
    verify the exact endpoint hostname with this context, enforce the deadline
    inside connection/TLS work, and release resources it cannot return. There
    is deliberately no native/default connector, discovery, proxy or retry.
    """
    def __init__(self, binding, ca, region, *, connector=None, now=lambda: datetime.now(timezone.utc)):
        self.binding, self.ca, self.region = binding, ca, region
        self.connector, self.now = connector, now

    @staticmethod
    def _validate(artifact, credential, p, ca, region):
        b.require(type(p) is ProviderBinding and type(ca) is bytes and 0 < len(ca) <= 65536 and
                  type(p.reader) is b.CredentialBinding and type(credential) is b.Credential and
                  type(artifact) is a.Artifact and a.Artifact.parse(artifact.wire()) == artifact)
        a.text(p.store, 63, r'[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]')
        b.require('..' not in p.store and p.reader.audience == p.store == artifact.store and
                  p.reader.principal != p.writer_principal and
                  p.reader.identity_sha256 != p.writer_identity_sha256 and
                  p.reader.custody_sha256 != p.writer_custody_sha256 and
                  p.reader.secret_sha256 != p.writer_secret_sha256)
        a.text(p.writer_principal)
        for value in (p.ca_sha256, p.writer_identity_sha256, p.writer_custody_sha256, p.writer_secret_sha256):
            a.sha(value)
        b.require(a.digest(ca) == p.ca_sha256 and type(p.endpoint) is str and len(p.endpoint) <= 256 and
                  re.fullmatch(r'https://[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?(?::[1-9][0-9]{0,4})?', p.endpoint) and
                  '..' not in p.endpoint and type(region) is str and
                  re.fullmatch(r'[a-z0-9]+(?:-[a-z0-9]+){1,5}', region) and len(region) <= 63)
        authority = p.endpoint[8:]
        host, _, port = authority.partition(':')
        b.require(all(0 < len(label) <= 63 and not label.startswith('-') and not label.endswith('-')
                      for label in host.split('.')) and (not port or int(port) <= 65535))
        credential.descriptor(p.reader)
        b.require(len(credential.secret) <= 4096)
        secret = a.strict_json(credential.secret)
        a.fields(secret, ('accessKeyId', 'secretAccessKey'))
        b.require(type(secret['accessKeyId']) is str and re.fullmatch(r'[A-Za-z0-9_-]{1,128}', secret['accessKeyId']) and
                  type(secret['secretAccessKey']) is str and
                  re.fullmatch(r'[\x21-\x7e]{1,256}', secret['secretAccessKey']))
        return authority, secret

    @staticmethod
    def _encode(value, slash=False):
        safe = b'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.~' + (b'/' if slash else b'')
        return ''.join(chr(v) if v in safe else '%%%02X' % v for v in value.encode('utf-8'))

    @staticmethod
    def _wire(artifact, authority, secret, instant, retention, region):
        path = '/' + S3ImmutableProvider._encode(artifact.store) + '/' + S3ImmutableProvider._encode(artifact.key, True)
        query = ('retention=&' if retention else '') + 'versionId=' + S3ImmutableProvider._encode(artifact.version)
        stamp, day, empty = instant.strftime('%Y%m%dT%H%M%SZ'), instant.strftime('%Y%m%d'), a.digest(b'')
        signed = 'host;x-amz-content-sha256;x-amz-date'
        headers = 'host:' + authority + '\nx-amz-content-sha256:' + empty + '\nx-amz-date:' + stamp + '\n'
        canonical = 'GET\n' + path + '\n' + query + '\n' + headers + '\n' + signed + '\n' + empty
        scope = day + '/' + region + '/s3/aws4_request'
        message = 'AWS4-HMAC-SHA256\n' + stamp + '\n' + scope + '\n' + a.digest(canonical.encode('ascii'))
        key = ('AWS4' + secret['secretAccessKey']).encode('ascii')
        for part in (day, region, 's3', 'aws4_request'):
            key = hmac.digest(key, part.encode('ascii'), 'sha256')
        signature = hmac.new(key, message.encode('ascii'), hashlib.sha256).hexdigest()
        authorization = 'AWS4-HMAC-SHA256 Credential=' + secret['accessKeyId'] + '/' + scope + \
            ', SignedHeaders=' + signed + ', Signature=' + signature
        return ('GET ' + path + '?' + query + ' HTTP/1.1\r\nHost: ' + authority +
            '\r\nx-amz-content-sha256: ' + empty + '\r\nx-amz-date: ' + stamp +
            '\r\nAuthorization: ' + authorization + '\r\nAccept-Encoding: identity\r\nConnection: close\r\n' +
            'Content-Length: 0\r\n\r\n').encode('ascii')

    @staticmethod
    def _request(context, wire, deadline, owned, connector, endpoint):
        b.remaining(deadline)
        stream = connector(context, endpoint, deadline)
        owned.append(stream)  # Own a late-returned stream before checking time.
        b.remaining(deadline)
        offset = 0
        while offset < len(wire):
            count = _s3_call(stream, deadline, 'send', wire[offset:])
            b.require(type(count) is int and 0 < count <= len(wire) - offset)
            offset += count
        return stream, _s3_headers(stream, deadline)

    def open_exact(self, artifact, credential, absolute_deadline):
        owned = []
        try:
            entered_at = b.time.monotonic()
            b.remaining(absolute_deadline)
            p, ca, region, connector, now = self.binding, self.ca, self.region, self.connector, self.now
            # No credential parsing or TLS work at all without explicit injection.
            b.require(connector is not None)
            authority, secret = self._validate(artifact, credential, p, ca, region)
            instant = now()
            b.require(type(instant) is datetime and instant.tzinfo is not None and instant.utcoffset() is not None)
            instant = instant.astimezone(timezone.utc)
            absolute_deadline = min(absolute_deadline,
                entered_at + (a.timestamp(artifact.retain_until) - instant).total_seconds())
            b.remaining(absolute_deadline)
            context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
            context.minimum_version = ssl.TLSVersion.TLSv1_2
            context.verify_mode, context.check_hostname = ssl.CERT_REQUIRED, True
            context.verify_flags |= ssl.VERIFY_X509_STRICT
            context.load_verify_locations(cadata=ca.decode('ascii'))
            body, headers = self._request(context,
                self._wire(artifact, authority, secret, instant, False, region), absolute_deadline, owned, connector, p.endpoint)
            b.require(headers.get('x-amz-version-id') == artifact.version and
                      int(headers['content-length']) == artifact.size and
                      headers.get('x-amz-server-side-encryption') == 'AES256' and
                      headers.get('x-amz-object-lock-mode') == 'COMPLIANCE' and
                      _s3_instant(headers.get('x-amz-object-lock-retain-until-date')) == artifact.retain_until and
                      headers.get('x-amz-delete-marker', 'false') == 'false')
            retention, headers = self._request(context,
                self._wire(artifact, authority, secret, instant, True, region), absolute_deadline, owned, connector, p.endpoint)
            b.require(int(headers['content-length']) <= b.MAX_HEADERS and
                      headers.get('content-type', '').split(';', 1)[0].strip() in ('application/xml', 'text/xml') and
                      headers.get('x-amz-version-id', artifact.version) == artifact.version)
            raw, length = bytearray(), int(headers['content-length'])
            while len(raw) < length:
                chunk = _s3_call(retention, absolute_deadline, 'recv', min(65536, length - len(raw)))
                b.require(type(chunk) is bytes and 0 < len(chunk) <= length - len(raw))
                raw.extend(chunk)
            tail = _s3_call(retention, absolute_deadline, 'recv', 1)
            b.require(type(tail) is bytes and tail == b'')
            _s3_retention(bytes(raw), artifact.retain_until)
            _s3_release(owned, absolute_deadline, only=retention)
            metadata = {'store': artifact.store, 'key': artifact.key, 'version': artifact.version,
                'length': artifact.size, 'sha256': artifact.sha256, 'serverSideEncryption': 'AES256',
                'objectLockMode': 'COMPLIANCE', 'retainUntil': artifact.retain_until,
                'principal': p.reader.principal, 'credentialIdentitySha256': p.reader.identity_sha256,
                'credentialCustodySha256': p.reader.custody_sha256,
                'providerEndpoint': p.endpoint, 'providerCaSha256': p.ca_sha256}
            # sha256 is the pinned expectation, independently checked from the
            # streamed body at terminal EOF; it is never a provider checksum.
            handle = _S3ReadHandle(owned, artifact, metadata, absolute_deadline)
            b.remaining(absolute_deadline)
            # Keep the same ownership list attached to the handle through the
            # return: an interrupt before handoff is still cleaned up below.
            return handle
        except BaseException as error:
            _s3_release(owned, absolute_deadline, error)


class ImmutableReadbackDriver:
    def __init__(self, binding, provider_binding, provider=None, *, now=lambda: datetime.now(timezone.utc)):
        self.binding, self.provider_binding, self.provider = binding, provider_binding, provider
        self.now = now

    def exchange(self, request, absolute_deadline, credential):
        handle = None
        error = response = None
        entered_at = b.time.monotonic()
        try:
            provider, p = self.provider, self.provider_binding
            b.remaining(absolute_deadline)
            a.validate_binding(self.binding)
            a._window(self.binding, self.now())
            b.require(type(p) is ProviderBinding and provider is not None and
                type(request) is a.Request and type(request.stdin) is bytes and type(request.expected) is bytes and
                request in a.build_requests(self.binding) and
                request.role == 'artifactReader' and request.method == 'READ_EXACT_VERSION' and
                request.principal == self.binding.validator and request.principal == p.reader.principal and
                p.reader.audience == p.store and p.writer_principal != p.reader.principal and
                p.writer_identity_sha256 != p.reader.identity_sha256 and
                p.writer_custody_sha256 != p.reader.custody_sha256 and
                p.writer_secret_sha256 != p.reader.secret_sha256)
            request = next(v for v in a.build_requests(self.binding) if v == request)
            a.text(p.store)
            a.text(p.writer_principal)
            for value in (p.ca_sha256, p.writer_identity_sha256, p.writer_custody_sha256, p.writer_secret_sha256):
                a.sha(value)
            b.require(type(p.endpoint) is str and p.endpoint.startswith('https://') and
                len(p.endpoint) <= 256 and all(33 <= ord(ch) < 127 for ch in p.endpoint) and
                not any(ch in p.endpoint for ch in ('@', '#', '?')))
            credential.descriptor(p.reader)
            artifact = a.Artifact.parse(a.strict_json(request.stdin))
            b.require(artifact.store == p.store and request.path == artifact.key and
                a.strict_json(request.expected) == {'check': 'immutable', 'artifact': artifact.wire()})
            dispatch_at = self.now()
            a._window(self.binding, dispatch_at)
            absolute_deadline = min(absolute_deadline, entered_at + request.timeout_seconds,
                b.time.monotonic() + (a.timestamp(self.binding.expires_at) - dispatch_at).total_seconds())
            b.remaining(absolute_deadline)
            b.require(not isinstance(provider, S3ImmutableProvider) or provider.binding == p)
            handle = provider.open_exact(artifact, credential, absolute_deadline)
            b.remaining(absolute_deadline)
            metadata = a.strict_json(handle.metadata)
            b.require(metadata == {'store': artifact.store, 'key': artifact.key, 'version': artifact.version,
                'length': artifact.size, 'sha256': artifact.sha256, 'serverSideEncryption': 'AES256',
                'objectLockMode': 'COMPLIANCE', 'retainUntil': artifact.retain_until,
                'principal': p.reader.principal, 'credentialIdentitySha256': p.reader.identity_sha256,
                'credentialCustodySha256': p.reader.custody_sha256,
                'providerEndpoint': p.endpoint, 'providerCaSha256': p.ca_sha256})
            b.require(type(metadata['length']) is int and
                a.timestamp(metadata['retainUntil']) >= a.timestamp(self.binding.retain_until))
            raw = bytearray()
            while True:
                b.remaining(absolute_deadline)
                maximum = min(65536, artifact.size - len(raw) + 1)
                chunk = handle.read(maximum, absolute_deadline)
                b.remaining(absolute_deadline)
                b.require(type(chunk) is bytes and len(chunk) <= maximum and len(raw) + len(chunk) <= artifact.size)
                if not chunk:
                    break
                raw.extend(chunk)
            b.require(len(raw) == artifact.size and a.digest(raw) == artifact.sha256)
            response = a.Response(0, 200, bytes(raw), artifact.version, artifact.retain_until)
        except BaseException as exc:
            error = exc
        if handle is not None:
            try:
                handle.close()
            except BaseException as cleanup:
                if error is None or isinstance(error, Exception) and not isinstance(cleanup, Exception):
                    error = cleanup
        if error is not None:
            if not isinstance(error, Exception):
                raise error.with_traceback(error.__traceback__)
            raise b.Refusal(b.FAILURE) from None
        try:
            b.remaining(absolute_deadline)
            a._window(self.binding, self.now())
        except Exception:
            raise b.Refusal(b.FAILURE) from None
        return response
