#!/usr/bin/env python3
"""Independent immutable reads through an explicit, unresolved provider seam."""
from dataclasses import dataclass
from datetime import datetime, timezone
import os
import stat
import types
from pathlib import Path
import sys
import hashlib

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


class ImmutableReadbackDriver:
    def __init__(self, binding, provider_binding, provider=None, *, now=lambda: datetime.now(timezone.utc)):
        self.binding, self.provider_binding, self.provider = binding, provider_binding, provider
        self.now = now

    def exchange(self, request, absolute_deadline, credential):
        handle = None
        error = response = None
        entered_at = b.time.monotonic()
        try:
            b.remaining(absolute_deadline)
            a.validate_binding(self.binding)
            a._window(self.binding, self.now())
            p = self.provider_binding
            b.require(type(p) is ProviderBinding and self.provider is not None and
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
            handle = self.provider.open_exact(artifact, credential, absolute_deadline)
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
