#!/usr/bin/env python3
"""Dormant, explicitly injected recovery execution mechanics. No live selector.

Nothing here loads configuration or credentials. The caller supplies measured
public code, pinned in-memory credential records, and inert transports. The
native connector is review material only and is never selected by exchange().
"""
import base64
from dataclasses import dataclass, replace
from datetime import datetime, timezone
import fcntl
import errno
import hashlib
import io
import math
import os
from pathlib import Path
import re
import selectors
import signal
import socket
import ssl
import stat
import subprocess
import sys
import time
import threading
import types
import uuid

FAILURE = 'recovery-execution-boundary-refused'
MAX_FILE = 128 * 1024 * 1024
MAX_HEADERS = 16384


class Refusal(ValueError):
    pass


def require(value):
    if not value:
        raise Refusal(FAILURE)


def remaining(deadline):
    require(type(deadline) in (int, float) and math.isfinite(deadline))
    value = deadline - time.monotonic()
    require(value > 0)
    return value


class _OwnedDescriptor:
    """A close invalidates the C-level owner before returning to Python.

    FileIO.close owns the release state even if a signal or a tracing hook runs
    after the underlying close. Retrying this object can never close a later
    allocation that happens to reuse its old descriptor number.
    """
    def __new__(cls, fd, expected=None):
        handle = super().__new__(cls)
        # The C owner exists before initialization can return or be interrupted.
        handle.file = io.FileIO.__new__(io.FileIO)
        handle.original_fd = fd
        handle.pending_fd = None
        info = os.fstat(fd)
        handle.identity = (info.st_dev, info.st_ino)
        require(expected is None or handle.identity == expected)
        handle.pending_fd = fd
        return handle

    def __init__(self, fd):
        info = os.fstat(fd)
        require((info.st_dev, info.st_ino) == self.identity)
        io.FileIO.__init__(self.file, fd, 'rb', closefd=True)
        self.pending_fd = None

    def fileno(self):
        return self.file.fileno()

    @property
    def closed(self):
        return self.file.closed and self.pending_fd is None

    def close(self):
        # A constructor interrupted before initialization leaves an owned raw
        # input in this already-registered placeholder. Adopt it before release.
        if self.pending_fd is not None and self.file.closed:
            info = os.fstat(self.pending_fd)
            if (info.st_dev, info.st_ino) != self.identity:
                self.pending_fd = None
                raise Refusal(FAILURE)
            io.FileIO.__init__(self.file, self.pending_fd, 'rb', closefd=True)
        self.pending_fd = None
        self.file.close()


class _DescriptorCustody:
    """Shared ownership and primary failure across allocation and cleanup."""
    def __init__(self, connector=None):
        self.handles = []
        self.allocations = []
        self.stream = None
        self.connector = connector
        self.error = None
        # Exception contexts older than this exchange are caller-owned history.
        self.caller_context = sys.exception()

    def construct(self, stream_type, *arguments):
        """Register a resource-free placeholder before its constructor acquires.

        Trusted private connectors use object.__new__; acquisition belongs in
        __init__, with idempotent close safe on a partially initialized object.
        No resource-returning factory handoff is accepted by this protocol.
        """
        require(self.stream is None and getattr(stream_type, '_native_owned_stream', False) is True)
        self.stream = object.__new__(stream_type)
        stream_type.__init__(self.stream, *arguments)
        return self.stream

    def remember(self, error):
        # A second interrupt during the first exception handler retains the
        # active first interrupt as context, even before its assignment below.
        # Select the oldest interruption, preserving ordinary-error sanitizing.
        if error is getattr(self, 'caller_context', None):
            return
        candidate, seen = error, set()
        while candidate is not None and candidate is not getattr(self, 'caller_context', None) and id(candidate) not in seen:
            seen.add(id(candidate))
            if not isinstance(candidate, Exception):
                error = candidate
            candidate = candidate.__context__
        previous = getattr(self, 'error', None)
        if error is not None and (previous is None or
                isinstance(previous, Exception) and not isinstance(error, Exception)):
            self.error = error

    def register(self, fd, expected=None):
        handle = _OwnedDescriptor.__new__(_OwnedDescriptor, fd, expected)
        try:
            # Register the uninitialized object before the constructor runs.
            self.handles.append(handle)
            _OwnedDescriptor.__init__(handle, fd)
            return handle
        except BaseException as exc:
            self.remember(exc)
            # The append itself may have been interrupted. There is no live
            # FileIO resource before that append; retain its pending raw input.
            if handle not in self.handles:
                self.handles.append(handle)
            raise


class _MemoryAllocation:
    """Unique local allocation identity and pre-acquisition recovery resources.

    Scandir owns its directory stream in C. Reserve three independent scans
    before creating any memory file, so raw-result recovery needs no new fd,
    even at RLIMIT_NOFILE. Spare scans cover interrupted discovery/registration.
    This is a bounded process-local protocol, not protection against arbitrary
    hostile descriptor-table mutation between identity checks and adoption.
    """
    def __init__(self):
        self.name = 'recovery-fixture-input-' + uuid.uuid4().hex
        self.link = '/memfd:' + self.name + ' (deleted)'
        self.identity = None
        self.scanners = []
        for _ in range(3):
            self.scanners.append(os.scandir('/proc/self/fd'))

    def observe(self, fd):
        require(os.readlink('/proc/self/fd/' + str(fd)) == self.link)
        info = os.fstat(fd)
        self.identity = (info.st_dev, info.st_ino)

    def recover(self, custody):
        for scanner in getattr(self, 'scanners', ()):
            for entry in scanner:
                fd = int(entry.name)
                try:
                    if os.readlink('/proc/self/fd/' + entry.name) != self.link:
                        continue
                    info = os.fstat(fd)
                except OSError as exc:
                    if exc.errno in (errno.ENOENT, errno.EBADF):
                        continue
                    raise
                require(stat.S_ISREG(info.st_mode) and info.st_nlink == 0)
                identity = (info.st_dev, info.st_ino)
                if self.identity is None:
                    self.identity = identity
                if identity != self.identity:
                    continue
                registered = False
                for handle in custody.handles:
                    if handle.original_fd == fd and handle.identity == identity:
                        registered = True
                        break
                if registered:
                    continue
                # Discovery identity is checked again at the adoption boundary.
                custody.register(fd, identity)

    def close(self):
        for scanner in getattr(self, 'scanners', ()):
            scanner.close()  # C-owned, idempotent even after interrupted return.


class _OwnedConnector:
    """Explicit private native acquisition contract; never selected implicitly.

    acquire(custody, context, cluster, deadline) constructs via custody.construct
    and returns no resource. A prepared fixture stream is already held here and
    shared with custody before acquire is invoked. Trusted stream close must be
    idempotent: real descriptors require C-level release state, and partial
    construction must be safely closeable. Arbitrary third-party closers and
    resource-returning callables are outside this qualification.
    """
    def __init__(self, acquire, *, prepared=None):
        require(callable(acquire) and (prepared is None or
                getattr(type(prepared), '_native_owned_stream', False) is True))
        self.acquire = acquire
        self.prepared = prepared

    def __call__(self, custody, context, cluster, deadline):
        result = self.acquire(custody, context, cluster, deadline)
        require(result is None and custody.stream is not None and
                getattr(type(custody.stream), '_native_owned_stream', False) is True and
                all(callable(getattr(custody.stream, name, None)) for name in ('send', 'recv', 'settimeout', 'close')))


def _close_native(owned, connector=None):
    stream = getattr(owned, 'stream', None)
    connector = getattr(owned, 'connector', connector)
    prepared = connector.prepared if type(connector) is _OwnedConnector else None
    streams = (stream,) if prepared is stream else (stream, prepared)
    for stream in streams:
        if stream is None:
            continue
        # Two interruptions before release still leave a third independent try.
        for _ in range(3):
            if getattr(stream, 'closed', False):
                break
            try:
                stream.close()
            except BaseException as cleanup:
                owned.remember(cleanup)


def _close_owned(owned, error=None):
    """Close stateful owners independently, retaining their shared first error."""
    require(type(owned) is _DescriptorCustody)
    owned.remember(error)
    for allocation in getattr(owned, 'allocations', ()):
        for _ in range(3):
            try:
                allocation.recover(owned)
                break
            except BaseException as cleanup:
                owned.remember(cleanup)
        for _ in range(3):
            try:
                allocation.close()
                break
            except BaseException as cleanup:
                owned.remember(cleanup)
    for handle in reversed(getattr(owned, 'handles', ())):
        for _ in range(2):
            if handle.closed:
                break
            try:
                handle.close()
            except BaseException as cleanup:
                owned.remember(cleanup)
    return getattr(owned, 'error', None)


def _cleanup_native(owned, connector):
    # Inline try statements avoid unprotected NOPs at cleanup entry in 3.14.
    # Every pass records the current lifetime exception before independent
    # stream and input cleanup. Partial custody construction is safe here.
    try: owned.remember(sys.exception())
    finally:
        try: _close_native(owned, connector)
        finally: _close_owned(owned)


def _close_raw(owned, error=None):
    """Compatibility cleanup for the unchanged source-custody reader."""
    for fd in reversed(owned):
        for _ in range(2):
            try:
                os.close(fd)
                break
            except BaseException as cleanup:
                if error is None or isinstance(error, Exception) and not isinstance(cleanup, Exception):
                    error = cleanup
    return error


def read_custody(path, maximum=MAX_FILE):
    """No-follow regular single-link bytes, stable before/after the bounded read."""
    owned, error = [], None
    try:
        path = Path(path)
        require(path.is_absolute() and '..' not in path.parts and str(path) != '/')
        directory = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
        owned.append(directory)
        for part in path.parts[1:-1]:
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC, dir_fd=directory)
            owned.append(child)
            os.close(directory)
            owned.remove(directory)
            directory = child
            info = os.fstat(directory)
            mode = stat.S_IMODE(info.st_mode)
            require(info.st_uid in (0, os.getuid()) and
                    (not mode & 0o022 or info.st_uid == 0 and mode & stat.S_ISVTX))
        fd = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC, dir_fd=directory)
        owned.append(fd)
        before = os.fstat(fd)
        require(stat.S_ISREG(before.st_mode) and before.st_uid in (0, os.getuid()) and
                before.st_nlink == 1 and not stat.S_IMODE(before.st_mode) & 0o022 and
                0 < before.st_size <= maximum)
        raw = bytearray()
        while len(raw) <= maximum:
            chunk = os.read(fd, min(65536, maximum + 1 - len(raw)))
            if not chunk:
                break
            raw.extend(chunk)
        after = os.fstat(fd)
        require(len(raw) == before.st_size and all(getattr(before, name) == getattr(after, name) for name in
                ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns', 'st_ctime_ns', 'st_mode', 'st_uid', 'st_nlink')))
    except BaseException as exc:
        error = exc
    error = _close_raw(owned, error)
    if error is not None:
        if not isinstance(error, Exception):
            raise error.with_traceback(error.__traceback__)
        raise Refusal(FAILURE) from None
    return bytes(raw)


def load_source(path, name):
    """Use checked sibling source bytes, never ambient import paths or pyc files."""
    raw = read_custody(path, 2 * 1024 * 1024)
    identity = hashlib.sha256(raw).hexdigest()
    if name in sys.modules:
        module = sys.modules[name]
        require(getattr(module, '__file__', None) == str(path) and
                getattr(module, '__source_sha256__', None) == identity)
        return module
    module = types.ModuleType(name)
    module.__file__ = str(path)
    module.__source_sha256__ = identity
    sys.modules[name] = module
    try:
        exec(compile(raw, str(path), 'exec'), module.__dict__)
    except Exception:
        sys.modules.pop(name, None)
        raise Refusal(FAILURE) from None
    return module


a = load_source(Path(__file__).absolute().with_name('recovery_adapters.py'), 'hexalith_recovery_fixed_adapter')


def check_files(bindings):
    require(type(bindings) is tuple and len(bindings) > 0 and
            len({t.path for t in bindings}) == len(bindings))
    for tool in bindings:
        require(type(tool) is a.Tool and a.Tool.parse({'path': tool.path, 'sha256': tool.sha256, 'size': tool.size}) == tool)
        raw = read_custody(tool.path)
        require(len(raw) == tool.size and a.digest(raw) == tool.sha256)


def check_runtime(dependencies, *modules):
    """Manifest bytes must be the source snapshots actually compiled in memory."""
    check_files(dependencies)
    inventory = {item.path: item.sha256 for item in dependencies}
    for module in modules:
        require(inventory.get(getattr(module, '__file__', None)) ==
                getattr(module, '__source_sha256__', None) and
                getattr(module, '__source_sha256__', None) is not None)


@dataclass(frozen=True)
class CredentialBinding:
    principal: str
    audience: str
    identity_sha256: str
    custody_sha256: str
    secret_sha256: str


@dataclass(frozen=True)
class Credential:
    """Already acquired bytes; pinned records identify custody, never a boolean."""
    binding: CredentialBinding
    identity: bytes
    custody: bytes
    secret: bytes

    def descriptor(self, expected):
        require(type(expected) is CredentialBinding and self.binding == expected)
        a.text(expected.principal)
        a.text(expected.audience)
        require(all(type(v) is bytes and 0 < len(v) <= 65536 for v in (self.identity, self.custody, self.secret)))
        require(a.digest(self.identity) == a.sha(expected.identity_sha256) and
                a.digest(self.custody) == a.sha(expected.custody_sha256) and
                a.digest(self.secret) == a.sha(expected.secret_sha256))
        identity = {'principal': expected.principal, 'audience': expected.audience}
        require(a.strict_json(self.identity) == identity and a.strict_json(self.custody) == dict(identity,
                identitySha256=expected.identity_sha256, secretSha256=expected.secret_sha256))
        return dict(identity, identitySha256=expected.identity_sha256, custodySha256=expected.custody_sha256,
                    secretBase64=base64.b64encode(self.secret).decode('ascii'))


def sealed(raw, executable=False, *, custody=None):
    require(type(raw) is bytes and 0 < len(raw) <= MAX_FILE)
    # Never allocate an executable/input at stdio or the credential destination.
    low = fd = None
    low_owner = high_owner = None
    try:
        require(custody is None or type(custody) is _DescriptorCustody)
        allocation = None
        if custody is not None:
            allocation = object.__new__(_MemoryAllocation)
            custody.allocations.append(allocation)
            _MemoryAllocation.__init__(allocation)
        # The shared identity precedes the syscall, including an interruption
        # before STORE_FAST transfers its raw return value to low or fd.
        low = os.memfd_create('recovery-fixture-input' if allocation is None else allocation.name,
            os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING)
        if custody is not None:
            allocation.observe(low)
            low_owner = custody.register(low, allocation.identity)
        fd = fcntl.fcntl(low, fcntl.F_DUPFD_CLOEXEC, 4)
        if custody is not None:
            high_owner = custody.register(fd, allocation.identity)
            low_owner.close()
        else:
            os.close(low)
        low = None
        os.fchmod(fd, 0o700 if executable else 0o600)
        offset = 0
        while offset < len(raw):
            count = os.write(fd, raw[offset:])
            require(count > 0)
            offset += count
        os.lseek(fd, 0, os.SEEK_SET)
        flags = fcntl.F_SEAL_WRITE | fcntl.F_SEAL_GROW | fcntl.F_SEAL_SHRINK | fcntl.F_SEAL_SEAL
        fcntl.fcntl(fd, fcntl.F_ADD_SEALS, flags)
        require(fcntl.fcntl(fd, fcntl.F_GET_SEALS) & flags == flags)
        return fd
    except BaseException as exc:
        if custody is not None:
            # Recover even a raw syscall result that never reached a local.
            # Shared identities and owners also survive interrupted registration.
            custody.remember(exc)
            error = _close_owned(custody)
            if not isinstance(error, Exception):
                raise error.with_traceback(error.__traceback__)
            raise Refusal(FAILURE) from None
        interrupt = exc if not isinstance(exc, Exception) else None
        for owned in (fd, low):
            if owned is not None:
                try:
                    os.close(owned)
                except BaseException as cleanup:
                    if interrupt is None and not isinstance(cleanup, Exception):
                        interrupt = cleanup
        if interrupt is not None:
            raise interrupt.with_traceback(interrupt.__traceback__)
        raise Refusal(FAILURE) from None


@dataclass(frozen=True)
class ProcessResult:
    exit_code: int
    stdout: bytes


class BoundedProcess:
    """One owned, unreaped process group; fixed environment and sealed fd 3.

    The leader is observed with WNOWAIT, then the group is signalled before
    reaping. A retained zombie protects its identifier during custody scans.
    """
    def __init__(self, dependencies):
        self.dependencies = dependencies

    def run(self, tool, arguments, stdin, credentials, deadline, maximum, error_maximum=65536, files=()):
        child = None
        owned = []
        result = error = None
        try:
            remaining(deadline)
            check_files(self.dependencies)
            require(tool in self.dependencies and type(arguments) is tuple and
                    all(type(v) is str and '\x00' not in v for v in arguments) and
                    type(stdin) is bytes and len(stdin) <= a.MAX_STDIN and
                    type(maximum) is int and 0 <= maximum <= a.MAX_ARTIFACT and
                    type(error_maximum) is int and 0 <= error_maximum <= 65536)
            raw = read_custody(tool.path)
            require(len(raw) == tool.size and a.digest(raw) == tool.sha256)
            if raw.startswith(b'#!'):
                require(raw.split(b'\n', 1)[0] == b'#!/usr/bin/python3.14 -ISB' and
                        any(t.path == '/usr/bin/python3.14' for t in self.dependencies))
            else:
                require(raw.startswith(b'\x7fELF'))
            executable = sealed(raw, True)
            owned.append(executable)
            credential_fd = sealed(credentials)
            owned.append(credential_fd)
            # close_fds follows preexec: keep fd 3 in pass_fds, reserving it only
            # if originally closed. Never replace an unrelated parent descriptor.
            try:
                fcntl.fcntl(3, fcntl.F_GETFD)
            except OSError as exc:
                require(exc.errno == errno.EBADF)
                os.dup2(credential_fd, 3, inheritable=False)
                owned.append(3)
            require(type(files) is tuple and len(files) <= 4 and len({k for k, _ in files}) == len(files))
            substitutions = {}
            for name, content in files:
                require(type(name) is str and re.fullmatch(r'@[a-z]+', name))
                fd = sealed(content)
                owned.append(fd)
                substitutions[name] = '/proc/self/fd/' + str(fd)
            argv = (tool.path,) + tuple(substitutions.get(v, v) for v in arguments)
            remaining(deadline)
            child = subprocess.Popen.__new__(subprocess.Popen)
            subprocess.Popen.__init__(child, argv, executable='/proc/self/fd/' + str(executable),
                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                cwd='/', env={'LANG': 'C.UTF-8'}, pass_fds=tuple(sorted({3, *owned})), close_fds=True,
                start_new_session=True, shell=False, preexec_fn=lambda: os.dup2(credential_fd, 3))
            output = bytearray()
            error_count = offset = 0
            with selectors.DefaultSelector() as selector:
                for stream, events, label in ((child.stdout, selectors.EVENT_READ, 'out'),
                        (child.stderr, selectors.EVENT_READ, 'err'), (child.stdin, selectors.EVENT_WRITE, 'in')):
                    os.set_blocking(stream.fileno(), False)
                    if label == 'in' and not stdin:
                        stream.close()
                    else:
                        selector.register(stream, events, label)
                while selector.get_map():
                    for key, _ in selector.select(min(remaining(deadline), 0.05)):
                        remaining(deadline)
                        if key.data == 'in':
                            count = os.write(key.fd, stdin[offset:offset + 65536])
                            require(count > 0)
                            offset += count
                            if offset == len(stdin):
                                selector.unregister(key.fileobj)
                                key.fileobj.close()
                        else:
                            used, limit = (len(output), maximum) if key.data == 'out' else (error_count, error_maximum)
                            chunk = os.read(key.fd, min(65536, limit - used + 1))
                            if not chunk:
                                selector.unregister(key.fileobj)
                                continue
                            require(used + len(chunk) <= limit)
                            if key.data == 'out':
                                output.extend(chunk)
                            else:
                                error_count += len(chunk)
                # Do not poll()/wait() here: reaping before killpg permits PID reuse.
                while True:
                    remaining(deadline)
                    observed = os.waitid(os.P_PID, child.pid, os.WEXITED | os.WNOHANG | os.WNOWAIT)
                    if observed is not None:
                        break
                    selector.select(min(remaining(deadline), 0.01))
            remaining(deadline)
            check_files(self.dependencies)
            remaining(deadline)
            code = observed.si_status if observed.si_code == os.CLD_EXITED else -observed.si_status
            result = ProcessResult(code, bytes(output))
        except BaseException as exc:
            error = exc
        # Every cleanup is attempted independently, even after cancellation or a
        # failing signal/wait/close. Only fixed errors escape ordinary failures.
        cleanup_errors = []
        def attempt(action):
            try:
                action()
                return True
            except BaseException as exc:
                cleanup_errors.append(exc)
                return False
        if child is not None and type(getattr(child, 'pid', None)) is int and child.pid > 0:
            def kill_group():
                # Refuse signalling if another owner has already reaped it.
                # WNOWAIT proves this PID still belongs to our child; None is
                # an owned live leader, a result is an owned unreaped zombie.
                require(getattr(child, 'returncode', None) is None)
                os.waitid(os.P_PID, child.pid, os.WEXITED | os.WNOHANG | os.WNOWAIT)
                try:
                    os.killpg(child.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
            attempt(kill_group)
            reap_deadline = time.monotonic() + 0.5
            if not attempt(lambda: child.wait(timeout=max(0, reap_deadline - time.monotonic()))):
                # A failing wrapper must not prevent the bounded reap attempt.
                def reap():
                    for _ in range(50):
                        try:
                            pid, status = os.waitpid(child.pid, os.WNOHANG)
                        except ChildProcessError:
                            return
                        if pid:
                            child.returncode = os.waitstatus_to_exitcode(status)
                            return
                        left = reap_deadline - time.monotonic()
                        if left <= 0:
                            return
                        time.sleep(min(left, 0.01))
                attempt(reap)
        if child is not None:
            for stream in (getattr(child, 'stdin', None), getattr(child, 'stdout', None), getattr(child, 'stderr', None)):
                if stream is not None and not attempt(stream.close):
                    # Standard stream close can fail before releasing its fd.
                    # Retrying only owned cleanup does not retry any operation.
                    if not attempt(stream.close):
                        def close_stream_fd(stream=stream):
                            try:
                                os.close(stream.fileno())
                            except OSError as exc:
                                if exc.errno != errno.EBADF:
                                    raise
                        attempt(close_stream_fd)
        for fd in reversed(owned):
            if not attempt(lambda fd=fd: os.close(fd)):
                attempt(lambda fd=fd: os.close(fd))
        # Preserve interrupt propagation; cleanup exceptions cannot replace it.
        for exc in (error, *cleanup_errors):
            if exc is not None and not isinstance(exc, Exception):
                raise exc.with_traceback(exc.__traceback__)
        if error is not None or cleanup_errors:
            raise Refusal(FAILURE) from None
        remaining(deadline)  # Includes time spent in all owned cleanup.
        return result


@dataclass(frozen=True)
class NativeBinding:
    cluster: a.Cluster
    ca: bytes
    credential: CredentialBinding
    authentication_profile: str = 'bearer'


def _pem_blocks(raw, labels, maximum):
    """Bounded linear framing check; SSLContext validates actual ASN.1/key data.

    Accept LF/CRLF, compact base64 and blank separator lines without rewriting
    the original material installed through sealed descriptors.
    """
    lines = raw.split(b'\n')
    index = count = 0
    while index < len(lines):
        line = lines[index].removesuffix(b'\r')
        index += 1
        if not line:
            continue
        label = None
        for supported in labels:
            if line == b'-----BEGIN ' + supported + b'-----':
                label = supported
                break
        require(label is not None and count < maximum)
        body = 0
        while index < len(lines):
            line = lines[index].removesuffix(b'\r')
            index += 1
            if line == b'-----END ' + label + b'-----':
                break
            require(re.fullmatch(rb'[A-Za-z0-9+/=]+', line) is not None)
            body += len(line)
        else:
            raise Refusal(FAILURE)
        require(body > 0)
        count += 1
    require(count > 0)


class NativeTLS:
    """Strict fixed-IP TLS. Only an explicitly injected inert connector is callable."""
    def __init__(self, connector=None):
        self.connector = connector

    def _context(self, binding):
        require(type(binding) is NativeBinding and type(binding.ca) is bytes and
                type(binding.authentication_profile) is str and
                binding.authentication_profile in ('bearer', 'client-certificate') and
                a.digest(binding.ca) == binding.cluster.ca_sha256)
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
        context.minimum_version = ssl.TLSVersion.TLSv1_2
        context.verify_mode = ssl.CERT_REQUIRED
        context.check_hostname = True
        context.verify_flags |= ssl.VERIFY_X509_STRICT
        context.load_verify_locations(cadata=binding.ca.decode('ascii'))
        return context

    def _install_certificate(self, context, secret, deadline, owned):
        """Install original bounded PEM bytes from registered sealed owners."""
        try:
            remaining(deadline)
            require(type(secret) is bytes and 0 < len(secret) <= 65536)
            envelope = a.strict_json(secret)
            require(type(envelope) is dict and set(envelope) == {'certificatePem', 'privateKeyPem'} and
                    all(type(value) is str for value in envelope.values()))
            certificate = envelope['certificatePem'].encode('ascii')
            key = envelope['privateKeyPem'].encode('ascii')
            require(0 < len(certificate) <= 49152 and 0 < len(key) <= 16384)
            _pem_blocks(certificate, (b'CERTIFICATE',), 16)
            _pem_blocks(key, (b'PRIVATE KEY', b'RSA PRIVATE KEY', b'EC PRIVATE KEY'), 1)
            paths = []
            for raw in (certificate, key):
                remaining(deadline)
                fd = sealed(raw, custody=owned)
                paths.append('/proc/self/fd/' + str(fd))
                remaining(deadline)
            def refuse_password():
                raise Refusal(FAILURE)
            context.load_cert_chain(*paths, password=refuse_password)
            remaining(deadline)
        except BaseException as exc:
            owned.remember(exc)
        error = _close_owned(owned)
        if error is not None:
            if not isinstance(error, Exception):
                raise error.with_traceback(error.__traceback__)
            raise Refusal(FAILURE) from None
        remaining(deadline)

    def _connect(self, context, cluster, deadline):
        """Dormant native mechanics; exchange never selects this connector."""
        require(type(context) is ssl.SSLContext and context.check_hostname and context.verify_mode == ssl.CERT_REQUIRED)
        cluster = a.Cluster.parse({'uid': cluster.uid, 'endpoint': cluster.endpoint, 'caSha256': cluster.ca_sha256})
        host, port = cluster.endpoint[8:].split(':')
        raw = stream = None
        try:
            raw = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            raw.settimeout(remaining(deadline))
            raw.connect((host, int(port)))
            raw.settimeout(remaining(deadline))
            stream = context.wrap_socket(raw, server_hostname=host, do_handshake_on_connect=False)
            stream.settimeout(remaining(deadline))
            stream.do_handshake()
            remaining(deadline)
            return stream
        except BaseException as exc:
            error = exc
            for handle in (stream, raw):
                if handle is not None:
                    try:
                        handle.close()
                    except BaseException as cleanup:
                        if isinstance(error, Exception) and not isinstance(cleanup, Exception):
                            error = cleanup
            if not isinstance(error, Exception):
                raise error.with_traceback(error.__traceback__)
            raise Refusal(FAILURE) from None

    def exchange(self, request, deadline, binding, credential):
        # Capture caller exception history before creating lifetime metadata.
        # An interruption at acquisition's first line must not select that old
        # caller exception merely because initialization has not stored it yet.
        caller_context = sys.exception()
        # The placeholder acquires no resource. The lifetime starts before any
        # custody initialization; cleanup can recover a prepared connector even
        # if constructor entry, partial initialization or return is interrupted.
        owned = object.__new__(_DescriptorCustody)
        owned.caller_context = caller_context
        try: response = self._guard_native(owned, request, deadline, binding, credential)
        except BaseException as exc: owned.remember(exc)
        # All known inputs and streams have had independent release attempts.
        # Raise outside an active handler so secondary.__context__ still names
        # the primary, rather than creating and breaking an exception cycle.
        error = getattr(owned, 'error', None)
        if error is not None:
            if not isinstance(error, Exception):
                raise error.with_traceback(error.__traceback__)
            raise Refusal(FAILURE) from None
        remaining(deadline)
        return response

    def _guard_native(self, owned, request, deadline, binding, credential):
        """Three independent cleanup passes, including their invocation handoff.

        Inline try statements avoid unprotected cleanup-entry NOPs. Each pass
        protects the next through two active interruptions, without retrying
        acquisition or exchange. Terminal propagation follows resource release;
        a new cancellation there is a separate external interruption.
        """
        try: return self._acquire_exchange(owned, request, deadline, binding, credential)
        finally:
            try: _cleanup_native(owned, self.connector)
            finally:
                try: _cleanup_native(owned, self.connector)
                finally: _cleanup_native(owned, self.connector)

    def _acquire_exchange(self, owned, request, deadline, binding, credential):
        owned.connector = self.connector
        _DescriptorCustody.__init__(owned, self.connector)
        remaining(deadline)
        require(type(self.connector) is _OwnedConnector)
        owned.stream = self.connector.prepared
        require(type(binding) is NativeBinding and type(credential) is Credential and
                type(request) is a.Request and
                type(binding.authentication_profile) is str and
                binding.authentication_profile in ('bearer', 'client-certificate') and
                request.endpoint == binding.cluster.endpoint and
                binding.credential.principal == request.principal and binding.credential.audience == binding.cluster.uid)
        credential.descriptor(binding.credential)
        context = self._context(binding)
        bearer = None
        if binding.authentication_profile == 'bearer':
            require(re.fullmatch(rb'[A-Za-z0-9._~+/=-]{1,65536}', credential.secret))
            bearer = credential.secret
        else:
            self._install_certificate(context, credential.secret, deadline, owned)
        remaining(deadline)
        self.connector(owned, context, binding.cluster, deadline)
        response = self._exchange(request, owned.stream, deadline, bearer)
        return response

    def _exchange(self, request, stream, deadline, bearer):
        """One absolute deadline covers every partial write and header/body byte."""
        try:
            require(type(request) is a.Request and request.method in ('GET', 'PATCH', 'DELETE') and
                    re.fullmatch(r'/[a-zA-Z0-9/._-]+', request.path) and
                    request.content_type in ('application/json', 'application/json-patch+json') and
                    (bearer is None or type(bearer) is bytes and
                     re.fullmatch(rb'[A-Za-z0-9._~+/=-]{1,65536}', bearer)))
            body = b'' if request.method == 'GET' else request.stdin
            wire = (request.method + ' ' + request.path + ' HTTP/1.1\r\nHost: ' + request.endpoint[8:] +
                '\r\nAccept: application/json\r\nAccept-Encoding: identity\r\nConnection: close\r\nContent-Type: ' +
                request.content_type + '\r\nContent-Length: ' + str(len(body)) + '\r\n').encode('ascii')
            if bearer is not None:
                wire += b'Authorization: Bearer ' + bearer + b'\r\n'
            wire += b'\r\n' + body
            offset = 0
            while offset < len(wire):
                stream.settimeout(remaining(deadline))
                count = stream.send(wire[offset:])
                require(type(count) is int and 0 < count <= len(wire) - offset)
                offset += count
            header = bytearray()
            while not header.endswith(b'\r\n\r\n'):
                require(len(header) < MAX_HEADERS)
                stream.settimeout(remaining(deadline))
                chunk = stream.recv(1)
                require(type(chunk) is bytes and len(chunk) == 1)
                header.extend(chunk)
            lines = bytes(header[:-4]).decode('ascii').split('\r\n')
            match = re.fullmatch(r'HTTP/1\.[01] (200|201|202|404|409|422)(?: [\x20-\x7e]{0,80})?', lines[0])
            require(match is not None)
            headers = {}
            for line in lines[1:]:
                require(':' in line and not line.startswith((' ', '\t')))
                name, value = line.split(':', 1)
                name = name.lower()
                require(re.fullmatch(r'[a-z0-9-]+', name) and name not in headers and
                        all(ch == '\t' or 32 <= ord(ch) < 127 for ch in value))
                headers[name] = value.strip(' \t')
            require(not {'location', 'transfer-encoding', 'content-encoding', 'link'} & headers.keys() and
                    headers.get('content-type', '').split(';', 1)[0].strip() == 'application/json' and
                    re.fullmatch(r'0|[1-9][0-9]{0,8}', headers.get('content-length', '')))
            length = int(headers['content-length'])
            require(length <= request.maximum_response_bytes)
            output = bytearray()
            while len(output) < length:
                stream.settimeout(remaining(deadline))
                chunk = stream.recv(min(65536, length - len(output)))
                require(type(chunk) is bytes and 0 < len(chunk) <= length - len(output))
                output.extend(chunk)
            # Single-request Connection: close profile: declared length is not
            # enough. Require bounded EOF and reject every surplus byte.
            stream.settimeout(remaining(deadline))
            tail = stream.recv(1)
            require(type(tail) is bytes and tail == b'')
            remaining(deadline)
            return a.Response(0, int(match[1]), bytes(output))
        except Exception:
            raise Refusal(FAILURE) from None


class RecoveryExecutionBoundary:
    """Single-use bound plan with authentication before all dependent dispatches.

    Providers/connectors are explicitly supplied trusted adapters. No default
    driver, discovery, credential loader, fallback, retry, or compensation exists.
    """
    def __init__(self, binding, dependencies, credentials, credential_bindings, *,
                 immutable_driver, policy_driver, native, native_bindings, process=None,
                 now=lambda: datetime.now(timezone.utc)):
        self.binding, self.dependencies = binding, dependencies
        self.credentials, self.credential_bindings = credentials, credential_bindings
        self.immutable_driver, self.policy_driver = immutable_driver, policy_driver
        self.native, self.native_bindings = native, native_bindings
        self.process = BoundedProcess(dependencies) if process is None else process
        self.now = now
        self.requests = a.build_requests(binding)
        self.index = 0
        self.stopped = False
        self.immutable = {}
        self.authenticated = False
        self.drain_at = self.fence_at = self.previous_request = None
        self._claim = threading.Lock()

    def _credential(self, principal, audience):
        matches = [v for v in self.credential_bindings if v.principal == principal and v.audience == audience]
        require(len(matches) == 1)
        creds = [v for v in self.credentials if v.binding == matches[0]]
        require(len(creds) == 1)
        creds[0].descriptor(matches[0])
        return creds[0]

    def _deadline(self, request, limit):
        """Recheck the exact prerequisite after all custody work, at dispatch."""
        now = self.now()
        a._window(self.binding, now)
        seconds = (a.timestamp(self.binding.expires_at) - now).total_seconds()
        observation = None
        if request.name == 'resume-replicas':
            observation = self.drain_at
        if self.binding.operation == 'cleanup' and (request.method == 'DELETE' or
                request.name == 'owned-target-inventory'):
            observation = self.fence_at
            if request.name == 'owned-target-inventory':
                require(self.previous_request == 'verify-creation-fence-before-inventory')
            elif request.path == self.binding.target_namespace.path():
                require(self.previous_request == 'verify-creation-fence-before-namespace-delete')
            elif request.name == 'delete-owned-0':
                require(self.previous_request == 'verify-creation-fence-before-deletes')
        if request.name == 'resume-replicas' or self.binding.operation == 'cleanup' and (
                request.method == 'DELETE' or request.name == 'owned-target-inventory'):
            require(observation is not None)
            age = (now - observation).total_seconds()
            require(0 <= age <= 30)
            seconds = min(seconds, 30 - age)
        require(not self.stopped)
        deadline = min(limit, time.monotonic() + seconds)
        remaining(deadline)
        return deadline

    def exchange(self, request, absolute_deadline):
        # A failed concurrent/reentrant claim poisons the same instance. The
        # active owner also checks stopped before dispatch and before committing.
        if not self._claim.acquire(blocking=False):
            self.stopped = True
            raise Refusal(FAILURE)
        try:
            return self._exchange_claimed(request, absolute_deadline)
        except BaseException as exc:
            self.stopped = True
            if not isinstance(exc, Exception):
                raise
            raise Refusal(FAILURE) from None
        finally:
            self._claim.release()

    def _exchange_claimed(self, request, absolute_deadline):
        entered_at = time.monotonic()
        remaining(absolute_deadline)
        require(not self.stopped and self.index < len(self.requests) and type(request) is a.Request)
        a.validate_binding(self.binding)
        a._window(self.binding, self.now())
        check_files(self.dependencies)
        # All three drivers and the unchanged common adapter must be pinned.
        required = {str(Path(__file__).absolute().with_name(name)) for name in
            ('recovery_execution_boundary.py', 'recovery_adapters.py', 'immutable_readback_driver.py',
             'policy_authentication_driver.py')}
        require(required <= {t.path for t in self.dependencies})
        check_runtime(self.dependencies, sys.modules[__name__], a,
            sys.modules[type(self.immutable_driver).__module__], sys.modules[type(self.policy_driver).__module__])
        expected = self.requests[self.index]
        if expected.name == 'authenticate-policy':
            body = a.strict_json(expected.stdin)
            require(set(self.immutable) == {'policy-readback', 'signature-readback'})
            body['exactReadbackBytes'] = {name: base64.b64encode(self.immutable[name + '-readback']).decode('ascii')
                for name in ('policy', 'signature')}
            expected = replace(expected, stdin=a.canonical(body), stdin_materialization=None)
        require(type(request.stdin) is bytes and type(request.expected) is bytes and
                request == expected and request.tool in self.dependencies and request.environment == (('LANG', 'C.UTF-8'),) and
                request.credential_fd == 3 and request.stdin_materialization is None)
        request = expected  # Dispatch only the boundary-owned immutable plan.
        # Cap even direct callers to the request and policy's single window.
        deadline = min(absolute_deadline, entered_at + request.timeout_seconds,
            time.monotonic() + (a.timestamp(self.binding.expires_at) - self.now()).total_seconds())
        require(request.name in ('policy-readback', 'signature-readback', 'authenticate-policy') or self.authenticated)
        if request.role == 'artifactReader':
            artifact = a.Artifact.parse(a.strict_json(request.stdin))
            credential = self._credential(request.principal, artifact.store)
            deadline = self._deadline(request, deadline)
            response = self.immutable_driver.exchange(request, deadline, credential)
        elif request.role == 'signatureVerifier':
            deadline = self._deadline(request, deadline)
            response = self.policy_driver.exchange(request, deadline)
        elif request.role == 'kubectl':
            bindings = [v for v in self.native_bindings if v.cluster.endpoint == request.endpoint]
            require(len(bindings) == 1 and bindings[0].cluster in
                    (self.binding.source_cluster, self.binding.target_cluster))
            require(a.digest(bindings[0].ca) == bindings[0].cluster.ca_sha256 and
                    bindings[0].credential.principal == request.principal and
                    bindings[0].credential.audience == bindings[0].cluster.uid)
            credential = self._credential(request.principal, bindings[0].cluster.uid)
            credential.descriptor(bindings[0].credential)
            deadline = self._deadline(request, deadline)
            response = self.native.exchange(request, deadline, bindings[0], credential)
        else:
            require(request.role == 'stageRunner' and request.argv ==
                    (request.tool.path, '--recovery-protocol-v1', '--request-stdin', '--credential-fd=3'))
            credentials = [self._credential(request.principal, cluster.uid) for cluster in
                dict.fromkeys((self.binding.source_cluster, self.binding.target_cluster))]
            descriptor = a.canonical({'requestSha256': request.sha256(), 'name': request.name,
                'path': request.path, 'method': request.method,
                'credentials': [c.descriptor(c.binding) for c in credentials]})
            deadline = self._deadline(request, deadline)
            result = self.process.run(request.tool, request.argv[1:], request.stdin, descriptor, deadline,
                request.maximum_response_bytes, request.maximum_error_bytes)
            require(type(result) is ProcessResult and type(result.exit_code) is int and result.exit_code == 0)
            response = a.Response(0, 200, result.stdout)
        remaining(deadline)
        a._window(self.binding, self.now())
        a._semantic(self.binding, request, response, self.now())
        check_files(self.dependencies)
        # Final scans/owned driver cleanup can consume the remaining time/window.
        remaining(deadline)
        checked_at = self.now()
        a._window(self.binding, checked_at)
        a._semantic(self.binding, request, response, checked_at)
        remaining(deadline)
        a._window(self.binding, self.now())
        require(not self.stopped)
        if request.name == 'authenticate-policy':
            self.authenticated = True
        if request.name == 'fresh-clean-drain':
            self.drain_at = a.timestamp(a.strict_json(response.body)['observedAt'])
        if a.strict_json(request.expected)['check'] == 'creation-fence':
            self.fence_at = a.timestamp(a.strict_json(response.body)['observedAt'])
        if request.name in ('policy-readback', 'signature-readback'):
            self.immutable[request.name] = response.body
        self.previous_request = request.name
        self.index += 1
        return response


def main(argv=None):
    # Every public invocation refuses before any configuration/credential read.
    print('{"backendRequests":0,"operationalGate":"closed","result":"' + FAILURE + '"}')
    return 1


if __name__ == '__main__':
    raise SystemExit(main())
