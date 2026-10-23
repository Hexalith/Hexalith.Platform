#!/usr/bin/env python3
"""Verify original SSHSIG buffers with pinned trust, then exact policy semantics."""
import base64
import struct
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

NAMESPACE = 'hexalith-recovery'


def trust_key(trust, expected_sha256, signer):
    """One exact ed25519 signer, one namespace, no patterns or delegated trust."""
    b.require(type(trust) is bytes and len(trust) <= 16384 and a.digest(trust) == a.sha(expected_sha256))
    a.text(signer)
    prefix = (signer + ' namespaces="' + NAMESPACE + '" ssh-ed25519 ').encode('ascii')
    b.require(trust.startswith(prefix) and trust.endswith(b'\n'))
    key = base64.b64decode(trust[len(prefix):-1], validate=True)
    b.require(len(key) == 51 and key[:19] == struct.pack('>I', 11) + b'ssh-ed25519' + struct.pack('>I', 32))
    b.require(trust == prefix + base64.b64encode(key) + b'\n')
    return a.digest(key)


class PolicyAuthenticationDriver:
    def __init__(self, binding, trust, trust_sha256, signer, dependencies, *, process=None,
                 now=lambda: datetime.now(timezone.utc)):
        self.binding, self.trust, self.trust_sha256, self.signer = binding, trust, trust_sha256, signer
        self.dependencies = dependencies
        self.process = b.BoundedProcess(dependencies) if process is None else process
        self.now = now

    def exchange(self, request, absolute_deadline):
        entered_at = b.time.monotonic()
        try:
            b.remaining(absolute_deadline)
            a.validate_binding(self.binding)
            a._window(self.binding, self.now())
            b.check_runtime(self.dependencies, sys.modules[__name__], b, a)
            expected = a.build_requests(self.binding)[2]
            b.require(type(request) is a.Request and request.name == 'authenticate-policy' and
                request == b.replace(expected, stdin=request.stdin, stdin_materialization=None) and
                request.tool in self.dependencies)
            request = b.replace(expected, stdin=request.stdin, stdin_materialization=None)
            value = a.strict_json(request.stdin)
            a.fields(value, ('policy', 'signature', 'scopeSha256', 'signerSha256', 'exactReadbackBytes'))
            b.require({k: value[k] for k in value if k != 'exactReadbackBytes'} ==
                {k: a.strict_json(expected.stdin)[k] for k in value if k != 'exactReadbackBytes'})
            a.fields(value['exactReadbackBytes'], ('policy', 'signature'))
            policy, signature = (base64.b64decode(value['exactReadbackBytes'][key], validate=True)
                for key in ('policy', 'signature'))
            for raw, artifact in ((policy, self.binding.policy), (signature, self.binding.signature)):
                b.require(type(raw) is bytes and len(raw) == artifact.size and a.digest(raw) == artifact.sha256)
            b.require(trust_key(self.trust, self.trust_sha256, self.signer) == self.binding.signer_sha256)
            dispatch_at = self.now()
            a._window(self.binding, dispatch_at)
            absolute_deadline = min(absolute_deadline, entered_at + request.timeout_seconds,
                b.time.monotonic() + (a.timestamp(self.binding.expires_at) - dispatch_at).total_seconds())
            b.remaining(absolute_deadline)
            # Only this exact cryptographic command may produce the existing response.
            # Original signed bytes are stdin, never a JSON reserialization.
            result = self.process.run(request.tool, ('-Y', 'verify', '-f', '@trust', '-I', self.signer,
                '-n', NAMESPACE, '-s', '@signature'), policy, b'{"credentials":[]}', absolute_deadline,
                16384, 16384, files=(('@trust', self.trust), ('@signature', signature)))
            b.require(type(result) is b.ProcessResult and type(result.exit_code) is int and result.exit_code == 0)
            b.remaining(absolute_deadline)
            a._window(self.binding, self.now())
            verified = a.strict_json(policy)
            b.require(type(verified.get('schemaVersion')) is int and verified == {'kind': 'hexalith.recovery-adapter-policy', 'schemaVersion': 1,
                'scopeSha256': self.binding.scope_sha256(), 'validFrom': self.binding.valid_from,
                'expiresAt': self.binding.expires_at, 'retainedDecisionsSha256': self.binding.decisions_sha256})
            b.check_runtime(self.dependencies, sys.modules[__name__], b, a)
            b.remaining(absolute_deadline)
            a._window(self.binding, self.now())
            return a.Response(0, 200, a.canonical({'authenticatedPolicySha256': self.binding.policy.sha256,
                'authenticatedSignatureSha256': self.binding.signature.sha256,
                'signerSha256': self.binding.signer_sha256, 'scopeSha256': self.binding.scope_sha256(),
                'algorithm': 'approved-detached-signature'}))
        except Exception:
            raise b.Refusal(b.FAILURE) from None
