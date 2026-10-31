"""Bounded PostgreSQL snapshot/age pipe primitives; no CLI or implicit authority.

The caller must authenticate its exact signed policy and immutable readback,
qualify the native transport, enforce UID/incarnation and writer-quiescence
checks, and separately implement immutable transfer/restore/resume/cleanup.
These primitives alone produce no recovery proof or authorization.
"""
import hashlib
import base64
import json
import os
from pathlib import Path
import re
import selectors
import signal
import stat
import subprocess
import time
from datetime import datetime, timezone

MAX_TRANSCRIPT = 4 * 1024 * 1024
MAX_ARTIFACT = 64 * 1024 * 1024


def require(ok, reason):
    if not ok:
        raise ValueError(reason)


def terminate(child):
    """Reap an owned process, never a target selected from external metadata."""
    if child.poll() is None:
        try:
            os.killpg(child.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    child.wait(timeout=5)
    for stream in (child.stdin, child.stdout, child.stderr):
        if stream is not None:
            stream.close()


class Session:
    """Keep the same psql backend/stdin alive while other commands use its snapshot."""
    def __init__(self, argv, timeout=150):
        require(isinstance(argv, list) and argv and 0 < timeout <= 150, 'session-input-refused')
        self.child = subprocess.Popen(argv, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                      stderr=subprocess.PIPE, start_new_session=True,
                                      env={'LANG': 'C.UTF-8'})
        self.deadline = time.monotonic() + timeout
        self.stdout, self.stderr, self.lines = bytearray(), bytearray(), []
        self.pending = bytearray()
        self.selected = selectors.DefaultSelector()
        for stream, label in ((self.child.stdout, 'stdout'), (self.child.stderr, 'stderr')):
            os.set_blocking(stream.fileno(), False)
            self.selected.register(stream, selectors.EVENT_READ, label)
        os.set_blocking(self.child.stdin.fileno(), False)

    def _step(self, deadline, data=None):
        require(time.monotonic() < min(deadline, self.deadline), 'session-deadline')
        sent = 0
        for key, _ in self.selected.select(min(.1, max(0, min(deadline, self.deadline) - time.monotonic()))):
            if key.data == 'stdin':
                sent = os.write(key.fd, data[:65536])
                continue
            target = self.stdout if key.data == 'stdout' else self.stderr
            limit = MAX_TRANSCRIPT if key.data == 'stdout' else 65536
            chunk = os.read(key.fd, min(65536, limit - len(target) + 1))
            if not chunk:
                self.selected.unregister(key.fileobj)
                continue
            target.extend(chunk)
            require(len(target) <= limit, 'session-output-bound')
            if key.data == 'stdout':
                self.pending.extend(chunk)
                while b'\n' in self.pending:
                    line, _, tail = self.pending.partition(b'\n')
                    self.pending = bytearray(tail)
                    if line.strip():
                        self.lines.append(json.loads(line))
        return sent

    def send(self, sql):
        require(isinstance(sql, bytes) and len(sql) <= 1024 * 1024, 'session-sql-bound')
        self.selected.register(self.child.stdin, selectors.EVENT_WRITE, 'stdin')
        try:
            sent = 0
            while sent < len(sql):
                require(self.child.poll() is None, 'snapshot-session-closed')
                sent += self._step(time.monotonic() + 15, sql[sent:])
        finally:
            self.selected.unregister(self.child.stdin)

    def until(self, category, timeout=20):
        deadline = time.monotonic() + timeout
        while not any(isinstance(v, dict) and v.get('category') == category for v in self.lines):
            require(any(key.data == 'stdout' for key in self.selected.get_map().values()),
                    'snapshot-session-unexpected-eof')
            self._step(deadline)
        return next(v for v in self.lines if isinstance(v, dict) and v.get('category') == category)

    def finish(self):
        self.send(b'ROLLBACK;\n')
        self.child.stdin.close()
        deadline = time.monotonic() + 15
        while self.selected.get_map():
            self._step(deadline)
        self.child.wait(timeout=max(.001, min(deadline, self.deadline) - time.monotonic()))
        require(self.child.returncode == 0 and not self.pending.strip(), 'snapshot-session-final-result-refused')
        return bytes(self.stdout)

    def close(self):
        self.selected.close()
        terminate(self.child)


def encrypted_pipe(source_argv, age, recipients, output, *, timeout=120, record_private=None):
    """Stream binary stdout directly into age; retain only ciphertext on disk."""
    require(0 < timeout <= 120 and isinstance(recipients, list) and len(recipients) == 2
            and recipients[0] != recipients[1], 'capture-pipe-input-refused')
    output = Path(output)
    parent = output.parent.lstat()
    require(stat.S_ISDIR(parent.st_mode) and parent.st_uid == os.getuid()
            and stat.S_IMODE(parent.st_mode) == 0o700 and not output.parent.is_symlink(),
            'capture-output-custody-refused')
    fd = os.open(output, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    source = cipher = None
    selected = selectors.DefaultSelector()
    deadline = time.monotonic() + timeout
    plain_sha, cipher_sha, plain_bytes, cipher_bytes = hashlib.sha256(), hashlib.sha256(), 0, 0
    source_error, cipher_error, queue = bytearray(), bytearray(), bytearray()
    source_eof, input_registered = False, False
    primary_failure = None
    try:
        source = subprocess.Popen(source_argv, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                                  stderr=subprocess.PIPE, start_new_session=True, env={'LANG': 'C.UTF-8'})
        cipher = subprocess.Popen([str(age), '-r', recipients[0], '-r', recipients[1]], stdin=subprocess.PIPE,
                                  stdout=subprocess.PIPE, stderr=subprocess.PIPE, start_new_session=True,
                                  env={'LANG': 'C.UTF-8'})
        for stream, label in ((source.stdout, 'source'), (source.stderr, 'source-error'),
                              (cipher.stdout, 'cipher'), (cipher.stderr, 'cipher-error')):
            os.set_blocking(stream.fileno(), False)
            selected.register(stream, selectors.EVENT_READ, label)
        os.set_blocking(cipher.stdin.fileno(), False)
        while selected.get_map() or queue:
            require(time.monotonic() < deadline, 'capture-pipe-deadline')
            if queue and not input_registered:
                selected.register(cipher.stdin, selectors.EVENT_WRITE, 'cipher-input')
                input_registered = True
            if source_eof and not queue and not cipher.stdin.closed:
                if input_registered:
                    selected.unregister(cipher.stdin)
                    input_registered = False
                cipher.stdin.close()
            for key, _ in selected.select(min(.1, max(0, deadline-time.monotonic()))):
                if key.data == 'cipher-input':
                    count = os.write(key.fd, queue[:65536])
                    del queue[:count]
                    if not queue:
                        selected.unregister(cipher.stdin)
                        input_registered = False
                    continue
                chunk = os.read(key.fd, 65536)
                if not chunk:
                    selected.unregister(key.fileobj)
                    if key.data == 'source':
                        source_eof = True
                    continue
                if key.data == 'source':
                    plain_bytes += len(chunk)
                    require(plain_bytes <= MAX_ARTIFACT, 'capture-plaintext-bound')
                    plain_sha.update(chunk)
                    queue.extend(chunk)
                    if len(queue) >= 1024 * 1024:
                        selected.unregister(source.stdout)
                elif key.data == 'cipher':
                    cipher_bytes += len(chunk)
                    require(cipher_bytes <= MAX_ARTIFACT + 1024 * 1024, 'capture-ciphertext-bound')
                    cipher_sha.update(chunk)
                    view = memoryview(chunk)
                    while view:
                        written = os.write(fd, view)
                        require(written > 0, 'capture-ciphertext-write-refused')
                        view = view[written:]
                else:
                    target = source_error if key.data == 'source-error' else cipher_error
                    target.extend(chunk)
                    require(len(target) <= 65536, 'capture-diagnostic-bound')
            if not source_eof and len(queue) < 1024 * 1024 and source.stdout not in [k.fileobj for k in selected.get_map().values()]:
                selected.register(source.stdout, selectors.EVENT_READ, 'source')
        for child in (source, cipher):
            child.wait(timeout=max(.001, deadline-time.monotonic()))
            require(child.returncode == 0, 'capture-subprocess-refused')
        require(plain_bytes > 0 and cipher_bytes > plain_bytes, 'capture-empty-or-unencrypted-output')
        os.fsync(fd)
        return {'plaintextSha256': plain_sha.hexdigest(), 'plaintextBytes': plain_bytes,
                'ciphertextSha256': cipher_sha.hexdigest(), 'ciphertextBytes': cipher_bytes,
                'sourceStderrSha256': hashlib.sha256(source_error).hexdigest(),
                'cipherStderrSha256': hashlib.sha256(cipher_error).hexdigest(),
                'encryptedBeforeDisk': True, 'plaintextFileWritten': False}
    except BaseException as exc:
        primary_failure=exc
        raise
    finally:
        failures=[]
        if record_private is not None:
            try:
                require(callable(record_private) and record_private('telemetry-dump-pipe-protocol',
                    (json.dumps({'sourceArgv':source_argv,
                     'sourceExitCode':source.poll() if source else None,
                     'cipherExitCode':cipher.poll() if cipher else None,
                     'sourceStderrBase64':base64.b64encode(source_error).decode(),
                     'cipherStderrBase64':base64.b64encode(cipher_error).decode(),
                     'plaintextBytesObserved':plain_bytes,'ciphertextBytesObserved':cipher_bytes,
                     'plaintextSha256Observed':plain_sha.hexdigest(),
                     'ciphertextSha256Observed':cipher_sha.hexdigest(),
                     'primaryError':type(primary_failure).__name__ if primary_failure else None},
                     sort_keys=True)+'\n').encode()) is True,'capture-pipe-protocol-custody-refused')
            except BaseException as exc:failures.append(exc)
        for cleanup in (selected.close,lambda:os.close(fd)):
            try:cleanup()
            except BaseException as exc:failures.append(exc)
        for child in (source, cipher):
            if child is not None:
                try:terminate(child)
                except BaseException as exc:failures.append(exc)
        if failures:raise BaseExceptionGroup('capture-pipe-primary-and-cleanup',
                                             ([primary_failure] if primary_failure else [])+failures)


def capture(value, source_prefix, age, recipients, output, authorize, observe_source, session_guard, record_private):
    """One local capture after caller-owned authentic policy/quiescence gates.

    Callbacks are required integration points, never substitute approval records.
    The exact caller and callback bytes must themselves be qualified and signed.
    No provider upload, target operation, resume or recovery acceptance occurs.
    """
    import telemetry_postgres_adapter as adapter
    adapter.validate(value)
    require(all(callable(v) for v in (authorize, observe_source, session_guard, record_private)), 'capture-integration-gate-missing')
    recovery_id = value['transfer']['key'].split('/')[1]
    require(re.fullmatch(r'[0-9]{8}t[0-9]{6}z', recovery_id), 'capture-attempt-label-invalid')
    names = {role: 's427-tel-' + recovery_id + '-' + role for role in ('holder', 'canary', 'dump')}
    def argv(arguments, role):
        require(arguments.count('PGAPPNAME=telemetry-recovery-proposal') == 1, 'capture-appname-binding-missing')
        result = ['PGAPPNAME=' + names[role] if v == 'PGAPPNAME=telemetry-recovery-proposal'
                  else v for v in arguments]
        binary = value['source']['tools']['pg_dump' if role == 'dump' else 'psql']['path']
        index = result.index(binary)
        options = 'PGOPTIONS=-c statement_timeout=' + ('120000' if role == 'dump' else '15000')
        options += ' -c idle_in_transaction_session_timeout=300000'
        return source_prefix + result[:index] + [options] + result[index:]
    ownership_sql = ("SELECT json_build_object('category','ownedSession','payload',json_build_object("
        "'pid',pid,'backendStart',backend_start::text,'user',usename,'database',datname,"
        "'applicationName',application_name))::text FROM pg_stat_activity WHERE pid=pg_backend_pid();\n")
    def verify_owned(role, observed):
        require(isinstance(observed, dict) and set(observed) == {
            'pid', 'backendStart', 'user', 'database', 'applicationName'}
            and type(observed['pid']) is int and observed['pid'] > 0
            and isinstance(observed['backendStart'], str) and observed['backendStart']
            and observed['user'] == value['source']['user']
            and observed['database'] == value['source']['database']
            and observed['applicationName'] == names[role], 'capture-owned-session-identity-refused')
        return observed
    require(authorize('telemetry-capture') is True, 'capture-policy-or-quiescence-refused')
    before = observe_source()
    require(before == value['source'], 'capture-current-source-binding-drift')
    holder, canary, owned, primary_failure = None, None, {}, None
    require(session_guard('before', names, owned) is True, 'capture-remote-session-precheck-refused')
    try:
        proposal = adapter.render_proposal(value)
        holder = Session(argv(proposal['capture']['snapshotHolder']['argv'], 'holder'))
        initial = proposal['capture']['snapshotHolder']['initialStdin'].replace(
            "SET LOCAL idle_in_transaction_session_timeout = '5min';",
            "SET LOCAL idle_in_transaction_session_timeout = '5min'; SET LOCAL statement_timeout = '15s';")
        holder.send(initial.encode())
        exported = holder.until('snapshotBinding')['payload']
        holder.send(ownership_sql.encode())
        owned['holder'] = verify_owned('holder', holder.until('ownedSession')['payload'])
        require(owned['holder']['pid'] == exported['holderBackendPid'], 'capture-holder-backend-mismatch')
        holder.send((adapter._canary_sections(value)['tableCensus'] + '\n').encode())
        snapshot_tables = holder.until('tableCensus')['payload']
        local_clock_before = datetime.now(timezone.utc)
        holder.send(("SELECT json_build_object('category','snapshotTiming','payload',json_build_object("
                     "'transactionStartedAt',transaction_timestamp()::text,"
                     "'sourceObservedAt',clock_timestamp()::text))::text;\n").encode())
        timing = holder.until('snapshotTiming')['payload']
        local_clock_after = datetime.now(timezone.utc)
        require(isinstance(timing, dict) and set(timing)=={'transactionStartedAt','sourceObservedAt'},
                'capture-source-clock-profile-refused')
        source_started, source_observed = (datetime.fromisoformat(timing[k]) for k in (
            'transactionStartedAt','sourceObservedAt'))
        require(source_started.tzinfo is not None and source_observed.tzinfo is not None
                and source_started <= source_observed, 'capture-source-clock-order-refused')
        timing = dict(timing, localClockBefore=local_clock_before.isoformat(),
                      localClockAfter=local_clock_after.isoformat(),
                      dataCutoffBasis='actual snapshot-holder transaction start; conservative lower bound of exported snapshot horizon')
        binding = {'proposalInputSha256': adapter.digest(adapter.canonical(value)),
                   'sourcePinSha256': value['sourcePinSha256'], **exported,
                   'holderTransactionOpen': True, **{k: value['source'][k] for k in (
                       'systemIdentifier', 'timeline', 'serverVersionNum')},
                   'preSnapshotTableCensus': before['tables'],
                   'snapshotTableCensus': snapshot_tables}
        commands = adapter.snapshot_commands(value, binding)
        sections = adapter._canary_sections(value)
        sql = commands['canaryStdin']
        suffix = sections['sequenceStateAfter'] + '\nROLLBACK;\n'
        require(sql.endswith(suffix), 'capture-canary-protocol-drift')
        canary = Session(argv(commands['canaryArgv'], 'canary'))
        sql = sql.replace("SET LOCAL search_path = pg_catalog;", ownership_sql
                          + "SET LOCAL idle_in_transaction_session_timeout = '5min';\n"
                          + "SET LOCAL search_path = pg_catalog;", 1)
        canary.send(sql[:-len(suffix)].encode())
        owned['canary'] = verify_owned('canary', canary.until('ownedSession')['payload'])
        require(owned['canary']['pid'] != owned['holder']['pid'], 'capture-owned-session-pids-not-distinct')
        canary.until(list(sections)[-2])
        require(session_guard('alive', names, owned) is True, 'capture-remote-session-liveness-refused')
        # Authenticate again immediately before reading the full database.
        require(authorize('telemetry-dump') is True, 'capture-policy-or-quiescence-refused')
        require(holder.child.poll() is None and canary.child.poll() is None,
                'snapshot-holder-or-canary-exited-before-dump')
        artifact = encrypted_pipe(argv(commands['dumpArgv'], 'dump'), age, recipients, output,
                                  record_private=record_private)
        local_dump_completed_at = datetime.now(timezone.utc).isoformat()
        require(holder.child.poll() is None and canary.child.poll() is None,
                'snapshot-holder-or-canary-exited-during-dump')
        require(session_guard('alive', names, owned) is True, 'capture-remote-session-liveness-refused')
        canary.send((sections['sequenceStateAfter'] + '\n').encode())
        transcript = canary.finish()
        records = [json.loads(line) for line in transcript.splitlines() if line.strip()]
        require(sum(v.get('category') == 'ownedSession' for v in records) == 1,
                'capture-owned-session-record-ambiguous')
        # parse_canaries consumes one JSON object per line, including psql's original compact records.
        transcript = b''.join((json.dumps(v, sort_keys=True) + '\n').encode()
                              for v in records if v.get('category') != 'ownedSession')
        parsed = adapter.parse_canaries(value, transcript)
        require(parsed['tableCensus'] == sorted(value['source']['tables'], key=adapter.canonical),
                'capture-snapshot-table-census-drift')
        holder.finish()
        require(observe_source() == before, 'capture-post-source-binding-drift')
        return {'state': 'local-encrypted-capture-only', 'artifact': artifact,
                'snapshotBinding': binding, 'sourceCanaryTranscript': transcript,
                'sourceSnapshotTiming': timing, 'localDumpCompletedAt': local_dump_completed_at,
                'sourceSnapshotCanaryParsed': True, 'snapshotHolderAliveThroughDump': True,
                'sameSnapshotDumpAndCanaries': True, 'providerReadback': False,
                'ownedRemoteSessions': owned, 'ownedRemoteSessionAbsenceVerified': True,
                'restoreExecuted': False, 'recoveryAccepted': False}
    except BaseException as exc:
        primary_failure = exc
        raise
    finally:
        failures = []
        # Retain the original, un-normalized SQL/session transcript on both
        # success and refusal before releasing the owned local sessions.
        protocol = {'sourcePinSha256':value['sourcePinSha256'], 'ownedRemoteSessions':owned,
                    'primaryError':type(primary_failure).__name__ if primary_failure else None}
        for name, session in [('holder',holder),('canary',canary)]:
            if session is not None:
                protocol[name] = {'stdoutBase64':base64.b64encode(session.stdout).decode(),
                                  'stderrBase64':base64.b64encode(session.stderr).decode()}
        try:
            require(record_private('telemetry-snapshot-protocol',
                                   (json.dumps(protocol,sort_keys=True)+'\n').encode()) is True,
                    'capture-protocol-custody-unverified')
        except BaseException as exc:
            failures.append(exc)
        for session in (canary, holder):
            if session is not None:
                try:
                    session.close()
                except BaseException as exc:
                    failures.append(exc)
        try:
            require(session_guard('released', names, owned) is True, 'capture-remote-session-cleanup-unverified')
        except BaseException as exc:
            failures.append(exc)
        if failures:
            raise BaseExceptionGroup('capture-refusal-and-cleanup',
                                     ([primary_failure] if primary_failure is not None else []) + failures)
