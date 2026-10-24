#!/usr/bin/env python3
"""Offline matrix/runner, custody, TLS-stream and real process boundary tests."""
import importlib.util
from contextlib import redirect_stdout
from dataclasses import replace
import io
import os
from pathlib import Path
import socket
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

_spec = importlib.util.spec_from_file_location('execution_fixture_support', Path(__file__).with_name('_execution_boundary_fixtures.py'))
f = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = f
_spec.loader.exec_module(f)
b, a = f.b, f.a


class FixtureCase(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)
        self.directory.chmod(0o700)
        self.external = patch.object(socket.socket, 'connect', side_effect=AssertionError('external requests forbidden'))
        self.external.start()
        self.addCleanup(self.external.stop)


class BoundaryTests(FixtureCase):
    def test_runner_composition_orders_authentication_before_dependent_dispatch_and_keeps_null_gates(self):
        boundary, provider, verifier, native = f.compose(self.directory)
        receipts = f.old.MemoryReceipts()
        result = a.run(boundary.binding, boundary, receipts, now=lambda: f.NOW)
        self.assertIsNone(result.failure)
        self.assertIsNone(result.operational_qualification)
        self.assertIsNone(result.recovery_acceptance)
        self.assertEqual(boundary.index, len(boundary.requests))
        self.assertEqual(len(provider.calls), 2)
        self.assertEqual(len(verifier.calls), 1)
        self.assertEqual(verifier.calls[0][2], verifier.policy)
        self.assertTrue(all(h.closed for h in provider.handles))
        self.assertTrue(all(s.closed for s in native.streams))
        self.assertNotEqual(boundary.binding.source_cluster, boundary.binding.target_cluster)
        self.assertEqual({r.endpoint for r in native.calls},
            {boundary.binding.source_cluster.endpoint, boundary.binding.target_cluster.endpoint})
        self.assertEqual(provider.calls[0][1].binding.principal, boundary.binding.validator)
        self.assertTrue(any(k.endswith('-complete') for k in receipts.records))

    def test_real_process_reads_sealed_fd3_fixed_environment_and_exact_stdin_without_preopened_fds(self):
        boundary, _, _, _ = f.compose(self.directory)
        result = a.run(boundary.binding, boundary, f.old.MemoryReceipts(), now=lambda: f.NOW)
        self.assertIsNone(result.failure)

    def test_real_process_keeps_credential_fd3_when_parent_descriptor3_is_occupied(self):
        occupied = os.open('/dev/null', os.O_RDONLY | os.O_CLOEXEC)
        try:
            # If fd 3 was already occupied, preserve that unrelated descriptor too.
            before = os.fstat(3)
            flags = b.fcntl.fcntl(3, b.fcntl.F_GETFD)
            boundary, _, _, _ = f.compose(self.directory)
            result = a.run(boundary.binding, boundary, f.old.MemoryReceipts(), now=lambda: f.NOW)
            self.assertIsNone(result.failure)
            self.assertEqual(os.fstat(3), before)
            self.assertEqual(b.fcntl.fcntl(3, b.fcntl.F_GETFD), flags)
            self.assertEqual(os.read(occupied, 1), b'')
        finally:
            os.close(occupied)

    def test_all_operation_compositions_preserve_existing_runner_semantics_and_distinct_clusters(self):
        for operation in ('capture', 'restore', 'resume', 'cleanup'):
            with self.subTest(operation=operation):
                boundary, provider, verifier, native = f.compose(self.directory, operation)
                boundary.process = f.StageProtocol(boundary.binding, verifier.policy, verifier.signature)
                receipts = f.old.MemoryReceipts()
                result = a.run(boundary.binding, boundary, receipts, now=lambda: f.NOW)
                self.assertIsNone(result.failure)
                self.assertEqual(result.state, operation + '-primitive-complete-unqualified')
                self.assertEqual(boundary.index, len(boundary.requests))
                self.assertEqual(len(verifier.calls), 1)
                self.assertTrue(all(r.principal == boundary.binding.operator for r in native.calls))
                self.assertTrue(all(c.binding.principal == boundary.binding.validator for _, c in provider.calls))
                self.assertIsNone(result.recovery_acceptance)

    def test_uncertain_patch_keeps_intents_and_prior_results_without_retry_or_compensation(self):
        boundary, _, verifier, native = f.compose(self.directory, 'resume')
        boundary.process = f.StageProtocol(boundary.binding, verifier.policy, verifier.signature)
        original = native.exchange
        writes = []
        streams = []
        def exchange(request, deadline, native_binding, credential):
            if request.method != 'PATCH':
                return original(request, deadline, native_binding, credential)
            writes.append(request)
            stream = f.Stream(b'')  # Write sent, connection ended without a response.
            streams.append(stream)
            try:
                return b.NativeTLS()._exchange(request, stream, deadline, credential.secret)
            finally:
                stream.close()
        native.exchange = exchange
        receipts = f.old.MemoryReceipts()
        result = a.run(boundary.binding, boundary, receipts, now=lambda: f.NOW)
        self.assertEqual(result.failure, a.FAILURE)
        self.assertEqual(result.state, 'stopped-pause-reconciliation-required')
        self.assertEqual(len(writes), 1)
        self.assertIn(b'PATCH ', streams[0].sent)
        self.assertTrue(streams[0].closed)
        self.assertTrue(any(k.endswith('-stopped') for k in receipts.records))
        self.assertTrue(any(k.endswith('-result') for k in receipts.records))
        self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_authentication_failure_stops_before_native_requests_and_persists_receipts(self):
        boundary, _, verifier, native = f.compose(self.directory)
        verifier.code = 1
        receipts = f.old.MemoryReceipts()
        result = a.run(boundary.binding, boundary, receipts, now=lambda: f.NOW)
        self.assertEqual(result.failure, a.FAILURE)
        self.assertEqual(native.calls, [])
        self.assertTrue(boundary.stopped)
        self.assertTrue(any(k.endswith('-stopped') for k in receipts.records))
        self.assertTrue(any(k.endswith('000-result') for k in receipts.records))

    def test_semantic_drift_stops_after_auth_without_compensation_or_retry(self):
        boundary, _, verifier, native = f.compose(self.directory)
        native.mutate = lambda r: a.Response(0, 200, b'{"unexpected":"secret diagnostic"}')
        receipts = f.old.MemoryReceipts()
        result = a.run(boundary.binding, boundary, receipts, now=lambda: f.NOW)
        self.assertEqual(result.failure, a.FAILURE)
        self.assertEqual(len(verifier.calls), 1)
        self.assertEqual(len(native.calls), 1)
        self.assertNotIn(b'secret diagnostic', b''.join(receipts.records.values()))
        with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
            boundary.exchange(boundary.requests[3], time.monotonic() + 1)
        self.assertEqual(len(native.calls), 1)

    def test_wrong_request_tool_dependency_cluster_ca_principal_and_custody_refuse_before_dispatch(self):
        mutations = ('request', 'tool', 'dependency', 'principal', 'credential-custody', 'credential-identity', 'cluster', 'ca')
        for mutation in mutations:
            with self.subTest(mutation=mutation):
                boundary, provider, _, native = f.compose(self.directory)
                if mutation in ('cluster', 'ca'):
                    # Advance authentication only; no native request has occurred.
                    for r in boundary.requests[:2]:
                        boundary.exchange(r, time.monotonic() + 2)
                    r = boundary.requests[2]
                    body = a.strict_json(r.stdin)
                    body['exactReadbackBytes'] = {'policy': f.base64.b64encode(boundary.immutable['policy-readback']).decode(),
                        'signature': f.base64.b64encode(boundary.immutable['signature-readback']).decode()}
                    boundary.exchange(replace(r, stdin=a.canonical(body), stdin_materialization=None), time.monotonic() + 2)
                    binding = boundary.native_bindings[0]
                    boundary.native_bindings = (replace(binding, ca=b'wrong') if mutation == 'ca' else
                        replace(binding, cluster=replace(binding.cluster, uid='wrong-cluster')),) + boundary.native_bindings[1:]
                    request = boundary.requests[3]
                else:
                    request = boundary.requests[0]
                    if mutation == 'request':
                        request = replace(request, stdin=b'{}')
                    elif mutation == 'tool':
                        request = replace(request, tool=replace(request.tool, sha256='f' * 64))
                    elif mutation == 'dependency':
                        boundary.dependencies = (replace(boundary.dependencies[0], sha256='f' * 64),) + boundary.dependencies[1:]
                    elif mutation == 'principal':
                        request = replace(request, principal=boundary.binding.operator)
                    else:
                        cred = boundary.credentials[0]
                        boundary.credentials = (replace(cred, **{'custody' if mutation.endswith('custody') else 'identity': b'changed'}),) + boundary.credentials[1:]
                calls = len(provider.calls)
                with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
                    boundary.exchange(request, time.monotonic() + 2)
                self.assertEqual(len(provider.calls), calls)
                self.assertEqual(native.calls, [])

    def test_checked_sibling_loading_ignores_poisoned_bytecode(self):
        import py_compile
        for name in ('recovery_execution_boundary.py', 'recovery_adapters.py',
                     'immutable_readback_driver.py', 'policy_authentication_driver.py'):
            (self.directory / name).write_bytes((f.ROOT / name).read_bytes())
        boundary = self.directory / 'recovery_execution_boundary.py'
        original = boundary.read_bytes()
        marker = self.directory / 'poison-executed'
        poison = ("open(" + repr(str(marker)) + ",'w').write('poison')\n").encode()
        poison += b'#' * (len(original) - len(poison))
        boundary.write_bytes(poison)
        timestamp = boundary.stat().st_mtime_ns
        py_compile.compile(str(boundary), doraise=True)
        boundary.write_bytes(original)
        os.utime(boundary, ns=(timestamp, timestamp))
        for driver in ('immutable_readback_driver.py', 'policy_authentication_driver.py'):
            result = subprocess.run(('/usr/bin/python3.14', '-I', '-S', '-B', str(self.directory / driver)),
                env={'LANG': 'C.UTF-8'}, cwd='/', stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=3)
            self.assertEqual(result.returncode, 0, result.stderr)
        self.assertFalse(marker.exists())

    def test_direct_out_of_order_dependent_request_refuses_without_dispatch(self):
        boundary, provider, verifier, native = f.compose(self.directory)
        with self.assertRaises(b.Refusal):
            boundary.exchange(boundary.requests[3], time.monotonic() + 1)
        self.assertEqual((provider.calls, verifier.calls, native.calls), ([], [], []))

    def test_expiry_and_elapsed_deadline_stop_without_dispatch(self):
        for deadline, now in ((time.monotonic() - 1, f.NOW),
                (time.monotonic() + 1, a.timestamp('2026-10-24T11:00:00Z'))):
            boundary, provider, _, _ = f.compose(self.directory)
            boundary.now = lambda: now
            with self.assertRaises(b.Refusal):
                boundary.exchange(boundary.requests[0], deadline)
            self.assertEqual(provider.calls, [])

    def test_public_live_config_and_activation_selectors_refuse_before_access(self):
        for argv in ([], ['live'], ['--activate'], ['--execute', '--config', '/actual/secret']):
            output = io.StringIO()
            with patch.object(b, 'read_custody', side_effect=AssertionError('configuration access')), patch.object(
                    b.subprocess, 'Popen', side_effect=AssertionError('process access')), redirect_stdout(output):
                self.assertEqual(b.main(argv), 1)
            self.assertEqual(a.strict_json(output.getvalue().encode())['backendRequests'], 0)

    def test_file_custody_rejects_symlinks_hardlinks_mode_and_changed_bytes(self):
        original = self.directory / 'source'
        original.write_bytes(b'original')
        original.chmod(0o600)
        binding = f.measure(original)
        for variant in ('symlink', 'hardlink', 'mode', 'bytes'):
            with self.subTest(variant=variant):
                target = self.directory / 'target'
                if target.exists() or target.is_symlink():
                    target.unlink()
                original.chmod(0o600)
                original.write_bytes(b'original')
                if variant == 'symlink':
                    target.symlink_to(original)
                elif variant == 'hardlink':
                    os.link(original, target)
                else:
                    target.write_bytes(b'original' if variant == 'mode' else b'changed')
                    target.chmod(0o666 if variant == 'mode' else 0o600)
                with self.assertRaises(b.Refusal):
                    b.check_files((replace(binding, path=str(target)),))
                target.unlink()


class ProcessTests(FixtureCase):
    def make_process(self, body):
        path = self.directory / 'bounded-process'
        path.write_text('#!/usr/bin/python3.14 -ISB\n' + body)
        path.chmod(0o700)
        tool = f.measure(path)
        return tool, b.BoundedProcess((tool, f.measure('/usr/bin/python3.14')))

    def test_partial_duplex_io_drains_stderr_while_writing_stdin(self):
        tool, process = self.make_process("import os,sys\nos.write(2,b'x'*32000)\nraw=sys.stdin.buffer.read()\nfor ch in raw: os.write(1,bytes([ch]))\n")
        result = process.run(tool, (), b'partial-data' * 100, b'{}', time.monotonic() + 3, 2000)
        self.assertEqual(result.stdout, b'partial-data' * 100)
        self.assertEqual(result.exit_code, 0)

    def test_output_and_error_overflow_stall_drip_stdin_and_descendant_pipes_are_bounded(self):
        bodies = (
            "import os\nos.write(1,b'x'*10000)\n",
            "import os\nos.write(2,b'private diagnostic'*1000)\n",
            "import time\ntime.sleep(3)\n",
            "import os,time\nwhile True:\n os.write(1,b'x')\n time.sleep(.03)\n",
            "import os,time\nif os.fork()==0: time.sleep(3)\n",
        )
        for body in bodies:
            with self.subTest(body=body):
                tool, process = self.make_process(body)
                started = time.monotonic()
                with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
                    process.run(tool, (), b'x' * 1024 * 1024, b'{}', time.monotonic() + .2, 100, 100)
                self.assertLess(time.monotonic() - started, 1.5)

    def test_stalled_stdin_does_not_extend_absolute_deadline(self):
        tool, process = self.make_process('import time\ntime.sleep(3)\n')
        with self.assertRaises(b.Refusal):
            process.run(tool, (), b'x' * 1024 * 1024, b'{}', time.monotonic() + .15, 10)

    def test_process_failure_remains_a_typed_result_without_raw_stderr(self):
        tool, process = self.make_process("import sys\nsys.stderr.write('PRIVATE')\nsys.exit(7)\n")
        result = process.run(tool, (), b'', b'{}', time.monotonic() + 2, 10)
        self.assertEqual(result, b.ProcessResult(7, b''))


class TLSTests(FixtureCase):
    def request(self):
        boundary, _, _, _ = f.compose(self.directory)
        return boundary.requests[3]

    def test_partial_native_send_and_body_read_share_one_deadline(self):
        request = self.request()
        stream = f.Stream(f.http(b'{}'), send_maximum=3, chunk=1)
        response = b.NativeTLS()._exchange(request, stream, time.monotonic() + 2, b'synthetic')
        self.assertEqual(response, a.Response(0, 200, b'{}'))
        self.assertIn(b'Authorization: Bearer synthetic\r\n', stream.sent)
        self.assertTrue(all(t > 0 for t in stream.timeouts))

    def test_drip_stall_incomplete_overflow_redirect_duplicate_and_encoding_refuse(self):
        request = self.request()
        streams = [f.Stream(f.http(b'{}'), delay=.03), f.Stream(b''),
            f.Stream(f.http(b'x' * (request.maximum_response_bytes + 1))),
            f.Stream(f.http(b'{}', extra=b'Location: https://secret.invalid\r\n')),
            f.Stream(f.http(b'{}', extra=b'Content-Length: 2\r\n')),
            f.Stream(f.http(b'{}', extra=b'Transfer-Encoding: chunked\r\n')),
            f.Stream(b'HTTP/1.1 200 OK\r\n' + b'X: ' + b'x' * b.MAX_HEADERS),
            f.Stream(f.http(b'{}')[:-1])]
        for stream in streams:
            with self.subTest(prefix=stream.raw[:50]):
                with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
                    b.NativeTLS()._exchange(request, stream, time.monotonic() + .08, b'synthetic')

    def test_tls_identity_and_ca_drift_refuse_before_connector_and_close_owned_stream(self):
        boundary, _, _, _ = f.compose(self.directory)
        request, binding = boundary.requests[3], boundary.native_bindings[0]
        credential = boundary._credential(request.principal, binding.cluster.uid)
        calls = []
        stream = f.Stream(f.http(b'{}'))
        def connector(context, cluster, deadline):
            calls.append(cluster)
            self.assertTrue(context.check_hostname)
            self.assertEqual(context.verify_mode, b.ssl.CERT_REQUIRED)
        native = b.NativeTLS(f.owned_connector(stream, connector))
        response = native.exchange(request, time.monotonic() + 2, binding, credential)
        self.assertEqual(response.body, b'{}')
        self.assertTrue(stream.closed)
        for bad in (replace(binding, ca=b'wrong'), replace(binding,
                credential=replace(binding.credential, principal='wrong'))):
            with self.assertRaises(b.Refusal):
                native.exchange(request, time.monotonic() + 1, bad, credential)
        self.assertEqual(len(calls), 1)
        with self.assertRaises(b.Refusal):
            b.NativeTLS().exchange(request, time.monotonic() + 1, binding, credential)



class CorrectedBoundaryTests(FixtureCase):
    def test_certificate_profiles_keep_stale_and_future_drain_and_fence_guards_closed(self):
        from datetime import timedelta
        for operation, target in (('resume', 'resume-replicas'), ('cleanup', 'owned-target-inventory'),
                ('cleanup', 'delete-owned-0'), ('cleanup', 'delete-owned-1'), ('cleanup', 'delete-owned-2')):
            for seconds in (31, -1):
                with self.subTest(operation=operation, target=target, age=seconds):
                    boundary, _, verifier, native = f.compose(self.directory, operation,
                        native_profiles=('client-certificate', 'client-certificate'),
                        native_secrets=(f.certificate_secret(), f.certificate_secret('TARGET')),
                        native_cas=(f.CERT_SOURCE_CA, f.CERT_TARGET_CA))
                    stage = f.StageProtocol(boundary.binding, verifier.policy, verifier.signature, boundary.credentials)
                    boundary.process = stage
                    request = self.advance(boundary, target)
                    before = len(native.calls), len(stage.backend.requests)
                    boundary.now = lambda: f.NOW + timedelta(seconds=seconds)
                    with self.assertRaises(b.Refusal):
                        boundary.exchange(request, time.monotonic() + 3)
                    self.assertEqual((len(native.calls), len(stage.backend.requests)), before)
                    self.assertTrue(boundary.stopped)

    def materialize(self, boundary, request):
        if request.name == 'authenticate-policy':
            value = a.strict_json(request.stdin)
            value['exactReadbackBytes'] = {name: f.base64.b64encode(boundary.immutable[name + '-readback']).decode()
                for name in ('policy', 'signature')}
            request = replace(request, stdin=a.canonical(value), stdin_materialization=None)
        return request

    def advance(self, boundary, target):
        for request in boundary.requests:
            if request.name == target:
                return request
            boundary.exchange(self.materialize(boundary, request), time.monotonic() + 3)
        self.fail('unknown request')

    def test_barrier_duplicate_signature_readback_and_reentrancy_cannot_skip_authentication(self):
        import threading
        for mode in ('concurrent', 'reentrant'):
            with self.subTest(mode=mode):
                boundary, provider, verifier, native = f.compose(self.directory)
                boundary.exchange(boundary.requests[0], time.monotonic() + 3)
                request = boundary.requests[1]
                arrived, proceed = threading.Barrier(2), threading.Barrier(2)
                original = provider.open_exact
                errors = []
                nested = []
                def open_exact(*args):
                    arrived.wait(timeout=3)
                    proceed.wait(timeout=3)
                    if mode == 'reentrant':
                        try:
                            boundary.exchange(request, time.monotonic() + 3)
                        except b.Refusal:
                            nested.append('refused')
                    return original(*args)
                provider.open_exact = open_exact
                def run():
                    try:
                        boundary.exchange(request, time.monotonic() + 3)
                    except BaseException as exc:
                        errors.append(exc)
                owner = threading.Thread(target=run)
                owner.start()
                arrived.wait(timeout=3)
                if mode == 'concurrent':
                    with self.assertRaises(b.Refusal):
                        boundary.exchange(request, time.monotonic() + 3)
                proceed.wait(timeout=3)
                owner.join(timeout=3)
                self.assertFalse(owner.is_alive())
                self.assertEqual(len(errors), 1)
                self.assertIsInstance(errors[0], b.Refusal)
                self.assertEqual(nested, ['refused'] if mode == 'reentrant' else [])
                self.assertEqual(boundary.index, 1)
                self.assertEqual(set(boundary.immutable), {'policy-readback'})
                self.assertFalse(boundary.authenticated)
                self.assertTrue(boundary.stopped)
                self.assertEqual(len(provider.calls), 2)
                with self.assertRaises(b.Refusal):
                    boundary.exchange(boundary.requests[3], time.monotonic() + 3)
                self.assertEqual((verifier.calls, native.calls), ([], []))

    def test_completed_authentication_is_required_independently_of_index_for_native_and_system(self):
        for role in ('kubectl', 'stageRunner'):
            boundary, _, verifier, native = f.compose(self.directory)
            index = next(i for i, r in enumerate(boundary.requests) if r.role == role)
            boundary.index = index  # Reproduce the historical numeric-index bypass.
            with self.assertRaises(b.Refusal):
                boundary.exchange(boundary.requests[index], time.monotonic() + 3)
            self.assertFalse(boundary.authenticated)
            self.assertEqual((verifier.calls, native.calls), ([], []))

    def test_direct_stale_and_future_prerequisites_stop_inventory_patch_and_each_delete(self):
        from datetime import timedelta
        for operation, target in (('resume', 'resume-replicas'), ('cleanup', 'owned-target-inventory'),
                ('cleanup', 'delete-owned-0'), ('cleanup', 'delete-owned-1'), ('cleanup', 'delete-owned-2')):
            for seconds in (31, -1):
                with self.subTest(operation=operation, target=target, age=seconds):
                    boundary, _, verifier, native = f.compose(self.directory, operation)
                    stage = f.StageProtocol(boundary.binding, verifier.policy, verifier.signature)
                    boundary.process = stage
                    request = self.advance(boundary, target)
                    before = len(native.calls), len(stage.backend.requests)
                    boundary.now = lambda: f.NOW + timedelta(seconds=seconds)
                    with self.assertRaises(b.Refusal):
                        boundary.exchange(request, time.monotonic() + 3)
                    self.assertEqual((len(native.calls), len(stage.backend.requests)), before)
                    self.assertTrue(boundary.stopped)

    def test_runner_stops_on_prerequisites_aging_during_boundary_dependency_scan_and_preserves_receipts(self):
        from datetime import timedelta
        for operation, target in (('resume', 'resume-replicas'), ('cleanup', 'owned-target-inventory'),
                ('cleanup', 'delete-owned-0')):
            with self.subTest(target=target):
                boundary, _, verifier, native = f.compose(self.directory, operation)
                stage = f.StageProtocol(boundary.binding, verifier.policy, verifier.signature)
                boundary.process = stage
                current = [f.NOW]
                boundary.now = lambda: current[0]
                original = b.check_files
                def scan(bindings):
                    original(bindings)
                    if boundary.requests[boundary.index].name == target:
                        current[0] = f.NOW + timedelta(seconds=31)
                receipts = f.old.MemoryReceipts()
                with patch.object(b, 'check_files', side_effect=scan):
                    result = a.run(boundary.binding, boundary, receipts, now=lambda: current[0])
                self.assertEqual(result.failure, a.FAILURE)
                self.assertFalse(any(r.name == target for r in native.calls))
                self.assertFalse(any(r.name == target for r in stage.backend.requests))
                self.assertTrue(any(k.endswith('-stopped') for k in receipts.records))
                self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_prerequisites_rechecked_after_credentials_and_delegated_deadline_is_clamped(self):
        from datetime import timedelta
        for operation, target in (('resume', 'resume-replicas'), ('cleanup', 'owned-target-inventory'),
                ('cleanup', 'delete-owned-0')):
            for seconds in (29, 31):
                with self.subTest(target=target, age=seconds):
                    boundary, _, verifier, native = f.compose(self.directory, operation)
                    stage = f.StageProtocol(boundary.binding, verifier.policy, verifier.signature)
                    boundary.process = stage
                    request = self.advance(boundary, target)
                    current, deadlines = [f.NOW], []
                    boundary.now = lambda: current[0]
                    credential = boundary._credential
                    def get_credential(*args):
                        result = credential(*args)
                        current[0] = f.NOW + timedelta(seconds=seconds)
                        return result
                    boundary._credential = get_credential
                    driver = stage if request.role == 'stageRunner' else native
                    method = 'run' if request.role == 'stageRunner' else 'exchange'
                    original = getattr(driver, method)
                    def dispatch(*args, **kwargs):
                        deadline = args[4] if method == 'run' else args[1]
                        deadlines.append(deadline - time.monotonic())
                        return original(*args, **kwargs)
                    with patch.object(driver, method, side_effect=dispatch):
                        if seconds == 31:
                            with self.assertRaises(b.Refusal):
                                boundary.exchange(request, time.monotonic() + 10)
                            self.assertEqual(deadlines, [])
                        else:
                            boundary.exchange(request, time.monotonic() + 10)
                            self.assertEqual(len(deadlines), 1)
                            self.assertTrue(0 < deadlines[0] <= 1)

    def test_keyboard_interrupt_during_uncertain_patch_poisoned_boundary_cannot_retry(self):
        boundary, _, verifier, native = f.compose(self.directory, 'resume')
        boundary.process = f.StageProtocol(boundary.binding, verifier.policy, verifier.signature)
        original = native.exchange
        writes = []
        def dispatch(request, *args):
            if request.method == 'PATCH':
                writes.append(request)
                raise KeyboardInterrupt('cancelled')
            return original(request, *args)
        native.exchange = dispatch
        receipts = f.old.MemoryReceipts()
        with self.assertRaises(KeyboardInterrupt):
            a.run(boundary.binding, boundary, receipts, now=lambda: f.NOW)
        self.assertTrue(boundary.stopped)
        self.assertEqual(len(writes), 1)
        with self.assertRaises(b.Refusal):
            boundary.exchange(writes[0], time.monotonic() + 3)
        self.assertEqual(len(writes), 1)
        key = 'resume-' + boundary.binding.scope_sha256() + '-' + str(boundary.index).zfill(3)
        self.assertIn(key + '-intent', receipts.records)
        self.assertNotIn(key + '-result', receipts.records)
        self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_final_dependency_scan_cannot_outlive_deadline_or_policy_or_observation(self):
        from datetime import timedelta
        for mode in ('deadline', 'policy', 'observation', 'semantic-deadline', 'semantic-policy'):
            boundary, _, verifier, _ = f.compose(self.directory, 'resume')
            boundary.process = f.StageProtocol(boundary.binding, verifier.policy, verifier.signature)
            target = 'fresh-clean-drain' if mode == 'observation' else 'paused-workload'
            request = self.advance(boundary, target)
            current = [f.NOW]
            boundary.now = lambda: current[0]
            original, scans, future = b.check_files, [], time.monotonic() + 100
            semantic, validations = a._semantic, []
            def scan(bindings):
                original(bindings)
                scans.append(None)
                if len(scans) == 3 and not mode.startswith('semantic-'):
                    if mode == 'deadline':
                        monotonic.return_value = future
                    else:
                        current[0] = (a.timestamp(boundary.binding.expires_at) if mode == 'policy'
                            else f.NOW + timedelta(seconds=31))
            def validate(*args):
                semantic(*args)
                validations.append(None)
                if len(validations) == 2:
                    if mode == 'semantic-deadline':
                        monotonic.return_value = future
                    elif mode == 'semantic-policy':
                        current[0] = a.timestamp(boundary.binding.expires_at)
            with patch.object(b.time, 'monotonic', return_value=future - 100) as monotonic, patch.object(
                    b, 'check_files', side_effect=scan), patch.object(a, '_semantic', side_effect=validate):
                with self.assertRaises(b.Refusal):
                    boundary.exchange(request, future - 99)
            self.assertEqual(len(scans), 3)
            if mode.startswith('semantic-'):
                self.assertEqual(len(validations), 2)
            self.assertEqual(boundary.requests[boundary.index].name, target)
            self.assertTrue(boundary.stopped)
            if mode == 'observation':
                self.assertIsNone(boundary.drain_at)

    def test_stage_envelope_empty_or_altered_records_fail_real_and_inert_protocols(self):
        variants = ('empty', 'requestSha256', 'name', 'path', 'method', 'principal', 'audience',
            'identitySha256', 'custodySha256', 'secretBase64')
        for real in (False, True):
            for variant in variants:
                with self.subTest(real=real, variant=variant):
                    boundary, _, verifier, _ = f.compose(self.directory)
                    process = boundary.process if real else f.StageProtocol(boundary.binding, verifier.policy, verifier.signature)
                    class ChangedEnvelope:
                        def run(self, tool, arguments, stdin, credentials, *args, **kwargs):
                            value = a.strict_json(credentials)
                            if variant == 'empty':
                                value['credentials'] = []
                            elif variant in ('requestSha256', 'name', 'path', 'method'):
                                value[variant] = 'wrong'
                            else:
                                value['credentials'][1][variant] = 'd3Jvbmc=' if variant == 'secretBase64' else 'wrong'
                            return process.run(tool, arguments, stdin, a.canonical(value), *args, **kwargs)
                    boundary.process = ChangedEnvelope()
                    result = a.run(boundary.binding, boundary, f.old.MemoryReceipts(), now=lambda: f.NOW)
                    self.assertEqual(result.failure, a.FAILURE)
                    self.assertTrue(boundary.stopped)


    def test_runner_refuses_stale_future_or_unclean_observations_without_retaining_timestamps(self):
        for operation, target in (('resume', 'fresh-clean-drain'), ('cleanup', 'verify-creation-fence-before-inventory')):
            for observed in ('2026-10-23T11:59:29Z', '2026-10-23T12:00:01Z', None):
                with self.subTest(target=target, observed=observed):
                    boundary, _, verifier, native = f.compose(self.directory, operation)
                    stage = f.StageProtocol(boundary.binding, verifier.policy, verifier.signature)
                    boundary.process = stage
                    def mutate(request, response):
                        if request.name == target:
                            value = a.strict_json(response.body)
                            if observed is None:
                                if operation == 'resume':
                                    value['pending'] = 1
                                else:
                                    value['observation'] = 'creation-allowed'
                            else:
                                value['observedAt'] = observed
                            return replace(response, body=a.canonical(value))
                        return response
                    stage.backend.mutation = mutate
                    receipts = f.old.MemoryReceipts()
                    result = a.run(boundary.binding, boundary, receipts, now=lambda: f.NOW)
                    self.assertEqual(result.failure, a.FAILURE)
                    self.assertIsNone(boundary.drain_at)
                    self.assertIsNone(boundary.fence_at)
                    self.assertFalse(any(r.method in ('PATCH', 'DELETE') for r in native.calls))
                    self.assertTrue(any(k.endswith('-stopped') for k in receipts.records))
                    self.assertFalse(any(k.endswith('-complete') for k in receipts.records))


    def test_mutation_during_credential_selection_cannot_change_the_approved_patch(self):
        for mutable in (False, True):
            with self.subTest(mutable=mutable):
                boundary, _, verifier, native = f.compose(self.directory, 'resume')
                boundary.process = f.StageProtocol(boundary.binding, verifier.policy, verifier.signature)
                approved = self.advance(boundary, 'resume-replicas')
                incoming = replace(approved, stdin=bytearray(approved.stdin) if mutable else approved.stdin)
                original, selections = boundary._credential, []
                def credential(*args):
                    result = original(*args)
                    selections.append(None)
                    altered = approved.stdin.replace(b'"value":2', b'"value":9')
                    if mutable:
                        incoming.stdin[:] = altered
                    else:
                        object.__setattr__(incoming, 'stdin', altered)
                    return result
                boundary._credential = credential
                if mutable:
                    with self.assertRaises(b.Refusal):
                        boundary.exchange(incoming, time.monotonic() + 3)
                    self.assertEqual(selections, [])
                    self.assertFalse(any(r.method == 'PATCH' for r in native.calls))
                else:
                    boundary.exchange(incoming, time.monotonic() + 3)
                    self.assertTrue(selections)
                    self.assertIs(native.calls[-1], approved)
                    sent = bytes(native.streams[-1].sent).split(b'\r\n\r\n', 1)[1]
                    self.assertEqual(sent, approved.stdin)
                    self.assertEqual(a.strict_json(sent)[-1]['value'], 2)


class CorrectedProcessTests(FixtureCase):
    make_process = ProcessTests.make_process
    def test_constructor_interruption_after_launch_keeps_ownership_and_reaps_only_that_child(self):
        tool, process = self.make_process('import time\ntime.sleep(10)\n')
        spawn, kill = b.subprocess.Popen, b.os.killpg
        initialize = spawn.__init__
        for when in ('before', 'after'):
            with self.subTest(when=when):
                children, signalled, partial = [], [], []
                interrupt = KeyboardInterrupt('constructor cancellation')
                before = set(os.listdir('/proc/self/fd'))
                def start(child, *args, **kwargs):
                    if when == 'after':
                        initialize(child, *args, **kwargs)
                        children.append(child)
                    else:
                        child.stdin = os.fdopen(os.open('/dev/null', os.O_RDONLY | os.O_CLOEXEC), 'rb')
                        partial.append(child.stdin)
                    raise interrupt
                def signal_group(pid, sig):
                    self.assertEqual(pid, children[0].pid)
                    signalled.append(pid)
                    return kill(pid, sig)
                with patch.object(spawn, '__init__', start), patch.object(b.os, 'killpg', side_effect=signal_group), self.assertRaises(
                        KeyboardInterrupt) as caught:
                    process.run(tool, (), b'', b'{"credentials":["synthetic-only"]}', time.monotonic() + 3, 10)
                self.assertIs(caught.exception, interrupt)
                self.assertTrue(all(stream.closed for stream in partial))
                self.assertEqual(set(os.listdir('/proc/self/fd')), before)
                if when == 'after':
                    self.assertEqual(signalled, [children[0].pid])
                    self.assertIsNotNone(children[0].returncode)
                    self.assertTrue(all(getattr(children[0], name).closed for name in ('stdin', 'stdout', 'stderr')))
                    with self.assertRaises(ChildProcessError):
                        os.waitpid(children[0].pid, os.WNOHANG)
                else:
                    self.assertEqual(signalled, [])

    def test_sealed_construction_close_failure_releases_both_descriptors_and_preserves_interrupt(self):
        create, duplicate, close = b.os.memfd_create, b.fcntl.fcntl, b.os.close
        for error_type in (OSError, KeyboardInterrupt):
            with self.subTest(error_type=error_type):
                low, high, attempts = [], [], []
                error = error_type('PRIVATE construction close diagnostic')
                before = set(os.listdir('/proc/self/fd'))
                def allocate(*args):
                    fd = create(*args)
                    low.append(fd)
                    return fd
                def fcntl(fd, command, *args):
                    result = duplicate(fd, command, *args)
                    if command == b.fcntl.F_DUPFD_CLOEXEC:
                        high.append(result)
                    return result
                def release(fd):
                    attempts.append(fd)
                    if fd == low[0] and attempts.count(fd) == 1:
                        raise error
                    return close(fd)
                expected = b.Refusal if error_type is OSError else KeyboardInterrupt
                with patch.object(b.os, 'memfd_create', side_effect=allocate), patch.object(b.fcntl, 'fcntl', side_effect=fcntl), patch.object(
                        b.os, 'close', side_effect=release), self.assertRaises(expected) as caught:
                    b.sealed(b'synthetic input')
                if expected is b.Refusal:
                    self.assertEqual(str(caught.exception), b.FAILURE)
                else:
                    self.assertIs(caught.exception, error)
                self.assertEqual(len(low), 1)
                self.assertEqual(len(high), 1)
                self.assertEqual(attempts.count(low[0]), 2)
                self.assertIn(high[0], attempts)
                self.assertEqual(set(os.listdir('/proc/self/fd')), before)
                for fd in low + high:
                    with self.assertRaises(OSError):
                        os.fstat(fd)

    def test_sealed_duplication_failure_releases_original_descriptor_and_preserves_interrupt(self):
        create, close = b.os.memfd_create, b.os.close
        for error_type in (OSError, KeyboardInterrupt):
            with self.subTest(error_type=error_type):
                descriptors, attempts = [], []
                error = error_type('PRIVATE duplication diagnostic')
                before = set(os.listdir('/proc/self/fd'))
                def allocate(*args):
                    fd = create(*args)
                    descriptors.append(fd)
                    return fd
                def release(fd):
                    attempts.append(fd)
                    return close(fd)
                expected = b.Refusal if error_type is OSError else KeyboardInterrupt
                with patch.object(b.os, 'memfd_create', side_effect=allocate), patch.object(b.fcntl, 'fcntl', side_effect=error), patch.object(
                        b.os, 'close', side_effect=release), self.assertRaises(expected) as caught:
                    b.sealed(b'synthetic input')
                if expected is b.Refusal:
                    self.assertEqual(str(caught.exception), b.FAILURE)
                else:
                    self.assertIs(caught.exception, error)
                self.assertEqual(len(descriptors), 1)
                self.assertEqual(attempts, descriptors)
                self.assertEqual(set(os.listdir('/proc/self/fd')), before)
                with self.assertRaises(OSError):
                    os.fstat(descriptors[0])

    def test_each_originally_closed_standard_descriptor_stays_closed_in_isolated_host(self):
        script = r'''import importlib.util, json, os, sys, time
from pathlib import Path
root, directory, closed, report = Path(sys.argv[1]), Path(sys.argv[2]), int(sys.argv[3]), Path(sys.argv[4])
spec = importlib.util.spec_from_file_location('isolated_fixture', root / '_execution_boundary_fixtures.py')
f = importlib.util.module_from_spec(spec); sys.modules[spec.name] = f; spec.loader.exec_module(f)
boundary, _, _, _ = f.compose(directory)
os.close(closed)
result = f.a.run(boundary.binding, boundary, f.old.MemoryReceipts(), now=lambda: f.NOW)
try:
    os.fstat(closed)
    still_closed = False
except OSError:
    still_closed = True
report.write_text(json.dumps({'failure': result.failure, 'stillClosed': still_closed}))
'''
        for closed in (0, 1, 2):
            with self.subTest(closed=closed):
                report = self.directory / ('report-' + str(closed))
                result = subprocess.run(('/usr/bin/python3.14', '-I', '-S', '-B', '-c', script,
                    str(f.ROOT), str(self.directory), str(closed), str(report)),
                    stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                    env={'LANG': 'C.UTF-8'}, cwd='/', timeout=5)
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertEqual(a.strict_json(report.read_bytes()), {'failure': None, 'stillClosed': True})

    def test_group_is_signalled_after_observing_exit_and_before_any_reap(self):
        tool, process = self.make_process("import os\nos.write(1,b'ok')\n")
        spawn, kill, wait, scan = b.subprocess.Popen, b.os.killpg, b.subprocess.Popen.wait, b.check_files
        children, events = [], []
        initialize = spawn.__init__
        def start(child, *args, **kwargs):
            initialize(child, *args, **kwargs)
            children.append(child)
        def signal_group(pid, sig):
            self.assertEqual(pid, children[0].pid)
            self.assertIsNone(children[0].returncode)
            observed = os.waitid(os.P_PID, pid, os.WEXITED | os.WNOHANG | os.WNOWAIT)
            self.assertIsNotNone(observed)
            events.append('signal')
            return kill(pid, sig)
        def reap(child, *args, **kwargs):
            self.assertIn('signal', events)
            events.append('reap')
            return wait(child, *args, **kwargs)
        def check(bindings):
            scan(bindings)
            if children:
                self.assertIsNone(children[0].returncode)
                events.append('final-scan')
        with patch.object(spawn, '__init__', start), patch.object(b.os, 'killpg', side_effect=signal_group), patch.object(
                spawn, 'wait', reap), patch.object(b, 'check_files', side_effect=check):
            result = process.run(tool, (), b'', b'{}', time.monotonic() + 3, 10)
        self.assertEqual(result.stdout, b'ok')
        self.assertEqual(events, ['final-scan', 'signal', 'reap'])

    def test_lost_child_ownership_refuses_group_signal_and_sanitizes_the_failure(self):
        tool, process = self.make_process("import os\nos.write(1,b'ok')\n")
        original = os.waitid
        stolen = []
        def observe(kind, pid, flags):
            value = original(kind, pid, flags)
            if value is not None and not stolen:
                os.waitpid(pid, 0)
                stolen.append(pid)
                raise ChildProcessError('PRIVATE independently reaped child')
            return value
        with patch.object(b.os, 'waitid', side_effect=observe), patch.object(b.os, 'killpg') as signal_group:
            with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
                process.run(tool, (), b'', b'{}', time.monotonic() + 3, 10)
        self.assertEqual(len(stolen), 1)
        signal_group.assert_not_called()

    def test_signal_wait_stream_and_descriptor_cleanup_failures_are_sanitized_without_avoidable_leaks(self):
        tool, process = self.make_process("import os\nos.write(1,b'ok')\n")
        spawn, close = b.subprocess.Popen, os.close
        before = set(os.listdir('/proc/self/fd'))
        children, descriptors, streams, errors = [], [], [], []
        class Stream:
            def __init__(self, wrapped):
                self.wrapped = wrapped
                self.failed = False
            def __getattr__(self, name):
                return getattr(self.wrapped, name)
            def close(self):
                if not self.failed:
                    self.failed = True
                    errors.append('stream')
                    raise OSError('PRIVATE stream diagnostic')
                return self.wrapped.close()
        initialize = spawn.__init__
        def start(child, *args, **kwargs):
            initialize(child, *args, **kwargs)
            children.append(child)
            streams.extend((child.stdin, child.stdout, child.stderr))
            child.stdout = Stream(child.stdout)
            original = child.wait
            def wait(*args, **kwargs):
                errors.append('wait')
                raise OSError('PRIVATE wait diagnostic')
            child.wait = wait
        original_sealed = b.sealed
        def seal(*args, **kwargs):
            fd = original_sealed(*args, **kwargs)
            descriptors.append(fd)
            return fd
        def close_fd(fd):
            if fd in descriptors and 'descriptor' not in errors:
                errors.append('descriptor')
                raise OSError('PRIVATE descriptor diagnostic')
            return close(fd)
        def signal_group(pid, sig):
            self.assertEqual(pid, children[0].pid)
            errors.append('signal')
            raise PermissionError('PRIVATE kill diagnostic')
        with patch.object(spawn, '__init__', start), patch.object(b, 'sealed', side_effect=seal), patch.object(
                b.os, 'killpg', side_effect=signal_group), patch.object(b.os, 'close', side_effect=close_fd):
            with self.assertRaisesRegex(b.Refusal, '^' + b.FAILURE + '$'):
                process.run(tool, (), b'', b'{}', time.monotonic() + 3, 10)
        self.assertEqual(set(errors), {'signal', 'wait', 'stream', 'descriptor'})
        self.assertEqual(set(os.listdir('/proc/self/fd')), before)
        self.assertTrue(all(stream.closed for stream in streams))
        self.assertIsNotNone(children[0].returncode)
        for fd in descriptors:
            with self.assertRaises(OSError):
                os.fstat(fd)
        with self.assertRaises(ChildProcessError):
            os.waitpid(children[0].pid, os.WNOHANG)

    def test_interrupt_is_preserved_while_every_owned_resource_is_released(self):
        tool, process = self.make_process('import time\ntime.sleep(3)\n')
        spawn, children, descriptors, sealed = b.subprocess.Popen, [], [], b.sealed
        initialize = spawn.__init__
        def start(child, *args, **kwargs):
            initialize(child, *args, **kwargs)
            children.append(child)
        def seal(*args, **kwargs):
            fd = sealed(*args, **kwargs)
            descriptors.append(fd)
            return fd
        before = set(os.listdir('/proc/self/fd'))
        with patch.object(spawn, '__init__', start), patch.object(b, 'sealed', side_effect=seal), patch.object(
                b.os, 'write', side_effect=KeyboardInterrupt('stop')):
            # The first os.write is sealed input creation, before child launch.
            with self.assertRaises(KeyboardInterrupt):
                process.run(tool, (), b'x', b'{}', time.monotonic() + 3, 10)
        self.assertEqual(children, [])
        self.assertEqual(set(os.listdir('/proc/self/fd')), before)
        for fd in descriptors:
            with self.assertRaises(OSError):
                os.fstat(fd)
        # Interrupt the running child's selector too, and preserve it after kill/reap.
        with patch.object(spawn, '__init__', start), patch.object(b, 'sealed', side_effect=seal), patch.object(
                b.selectors.EpollSelector, 'select', side_effect=KeyboardInterrupt('stop')):
            with self.assertRaises(KeyboardInterrupt):
                process.run(tool, (), b'x', b'{}', time.monotonic() + 3, 10)
        self.assertTrue(all(s.closed for s in (children[0].stdin, children[0].stdout, children[0].stderr)))
        self.assertIsNotNone(children[0].returncode)
        for fd in descriptors:
            with self.assertRaises(OSError):
                os.fstat(fd)

    def test_process_deadline_expiring_only_during_final_scan_or_close_prevents_success(self):
        for mode in ('scan', 'close'):
            tool, process = self.make_process("import os\nos.write(1,b'ok')\n")
            scan, close, future = b.check_files, b.os.close, time.monotonic() + 100
            state = {'scans': 0, 'expired': False}
            def check(bindings):
                scan(bindings)
                state['scans'] += 1
                if state['scans'] == 2 and mode == 'scan':
                    state['expired'] = True
            def close_fd(fd):
                close(fd)
                if state['scans'] == 2 and mode == 'close':
                    state['expired'] = True
            with patch.object(b.time, 'monotonic', side_effect=lambda: future if state['expired'] else future - 100), patch.object(
                    b, 'check_files', side_effect=check), patch.object(b.os, 'close', side_effect=close_fd):
                with self.assertRaises(b.Refusal):
                    process.run(tool, (), b'', b'{}', future - 99, 10)
            self.assertTrue(state['expired'])


class CustodyCleanupTests(FixtureCase):
    def test_final_close_partial_acquisition_and_interrupt_cleanup_are_independent_and_sanitized(self):
        path = self.directory / 'custody-input'
        path.write_bytes(b'synthetic custody bytes')
        readers = (lambda: b.read_custody(path), f.immutable._read_boundary_source, f.policy_driver._read_boundary_source)
        for reader in readers:
            for failure in ('final-close', 'final-directory-close', 'walk-close', 'root-open', 'open', 'interrupt'):
                with self.subTest(reader=reader, failure=failure):
                    opened, read_started, failed = [], [False], []
                    original_open, original_close, original_read = os.open, os.close, os.read
                    primary = KeyboardInterrupt('primary custody cancellation')
                    before = set(os.listdir('/proc/self/fd'))
                    def acquire(*args, **kwargs):
                        if failure == 'root-open' or failure == 'open' and len(opened) == 2:
                            raise OSError('PRIVATE partial acquisition')
                        fd = original_open(*args, **kwargs)
                        opened.append(fd)
                        return fd
                    def read(*args):
                        read_started[0] = True
                        if failure == 'interrupt':
                            raise primary
                        return original_read(*args)
                    def close(fd):
                        final = read_started[0] and (failure in ('final-close', 'interrupt') or
                            failure == 'final-directory-close' and b.stat.S_ISDIR(os.fstat(fd).st_mode))
                        if not failed and (failure == 'walk-close' or final):
                            failed.append(fd)
                            if failure == 'interrupt':
                                raise KeyboardInterrupt('secondary custody cancellation')
                            raise OSError('PRIVATE final close')
                        return original_close(fd)
                    expected = KeyboardInterrupt if failure == 'interrupt' else ValueError
                    with patch.object(b.os, 'open', side_effect=acquire), patch.object(b.os, 'read', side_effect=read), patch.object(
                            b.os, 'close', side_effect=close), self.assertRaises(expected) as caught:
                        reader()
                    if failure == 'interrupt':
                        self.assertIs(caught.exception, primary)
                    else:
                        self.assertEqual(str(caught.exception), b.FAILURE)
                    self.assertEqual(set(os.listdir('/proc/self/fd')), before)
                    for fd in set(opened):
                        with self.assertRaises(OSError):
                            os.fstat(fd)


class CorrectedTLSTests(FixtureCase):
    request = TLSTests.request
    def test_connect_and_handshake_interrupts_close_owned_inert_sockets_and_preserve_primary(self):
        boundary, _, _, _ = f.compose(self.directory)
        binding = boundary.native_bindings[0]
        native = b.NativeTLS()
        context = native._context(binding)
        for where in ('connect', 'handshake'):
            primary, secondary = KeyboardInterrupt('primary socket cancellation'), KeyboardInterrupt('cleanup cancellation')
            class Handle:
                def __init__(self, label):
                    self.label, self.closed = label, False
                def settimeout(self, value):
                    pass
                def connect(self, address):
                    if where == 'connect':
                        raise primary
                def do_handshake(self):
                    raise primary
                def close(self):
                    self.closed = True
                    raise secondary
            raw, stream = Handle('raw'), Handle('tls')
            with self.subTest(where=where), patch.object(b.socket, 'socket', return_value=raw), patch.object(
                    b.ssl.SSLContext, 'wrap_socket', return_value=stream), self.assertRaises(KeyboardInterrupt) as caught:
                native._connect(context, binding.cluster, time.monotonic() + 3)
            self.assertIs(caught.exception, primary)
            self.assertTrue(raw.closed)
            self.assertEqual(stream.closed, where == 'handshake')

    def test_full_source_target_exchanges_use_selected_bearers_and_only_the_pinned_ca(self):
        boundary, _, _, _ = f.compose(self.directory)
        backend = f.old.FakeBackend(boundary.binding, boundary.policy_driver.process.policy, boundary.policy_driver.process.signature)
        seen = []
        test = self
        class Native:
            def exchange(self, request, deadline, binding, credential):
                expected = backend.exchange(request, deadline)
                stream = f.Stream(f.http(expected.body, expected.status))
                def connector(context, cluster, limit):
                    test.assertEqual(cluster, binding.cluster)
                    test.assertTrue(context.check_hostname)
                    test.assertEqual(context.verify_mode, b.ssl.CERT_REQUIRED)
                    der = b.ssl.PEM_cert_to_DER_cert(binding.ca.decode('ascii'))
                    test.assertEqual(context.get_ca_certs(binary_form=True), [der])
                response = b.NativeTLS(f.owned_connector(stream, connector)).exchange(request, deadline, binding, credential)
                headers = bytes(stream.sent).split(b'\r\n\r\n', 1)[0].split(b'\r\n')[1:]
                authorization = dict(line.split(b': ', 1) for line in headers)[b'Authorization']
                test.assertEqual(authorization, b'Bearer ' + credential.secret)
                test.assertEqual(credential.binding.audience, cluster_uid := binding.cluster.uid)
                test.assertEqual(credential.binding.principal, boundary.binding.operator)
                test.assertTrue(stream.closed)
                seen.append((cluster_uid, credential.secret))
                return response
        boundary.native = Native()
        result = a.run(boundary.binding, boundary, f.old.MemoryReceipts(), now=lambda: f.NOW)
        self.assertIsNone(result.failure)
        self.assertEqual({uid for uid, _ in seen}, {boundary.binding.source_cluster.uid, boundary.binding.target_cluster.uid})
        self.assertEqual(len({secret for _, secret in seen}), 2)

    def test_surplus_response_bytes_and_expiry_waiting_for_eof_or_close_refuse(self):
        request = self.request()
        with self.assertRaises(b.Refusal):
            b.NativeTLS()._exchange(request, f.Stream(f.http(b'{}') + b'EXTRA_RESPONSE_BYTES'), time.monotonic() + 2, b'synthetic')
        for mode in ('eof', 'close'):
            boundary, _, _, _ = f.compose(self.directory)
            request, binding = boundary.requests[3], boundary.native_bindings[0]
            credential = boundary._credential(request.principal, binding.cluster.uid)
            stream = f.Stream(f.http(b'{}'))
            future, expired = time.monotonic() + 100, [False]
            if mode == 'eof':
                receive = stream.recv
                def recv(maximum):
                    result = receive(maximum)
                    if not result:
                        expired[0] = True
                    return result
                stream.recv = recv
            else:
                def close():
                    stream.closed = True
                    expired[0] = True
                stream.close = close
            with patch.object(b.time, 'monotonic', side_effect=lambda: future if expired[0] else future - 100):
                with self.assertRaises(b.Refusal):
                    b.NativeTLS(f.owned_connector(stream)).exchange(request, future - 99, binding, credential)
            self.assertTrue(expired[0])
            self.assertTrue(stream.closed)

    def test_exact_native_patch_delete_wire_body_headers_routing_and_preconditions(self):
        for operation in ('resume', 'cleanup'):
            boundary, _, _, _ = f.compose(self.directory, operation)
            for request in boundary.requests:
                if request.method not in ('PATCH', 'DELETE'):
                    continue
                with self.subTest(operation=operation, name=request.name):
                    stream = f.Stream(f.http(b'{}'), send_maximum=2)
                    b.NativeTLS()._exchange(request, stream, time.monotonic() + 3, b'synthetic')
                    header, body = bytes(stream.sent).split(b'\r\n\r\n', 1)
                    lines = header.split(b'\r\n')
                    self.assertEqual(lines[0], (request.method + ' ' + request.path + ' HTTP/1.1').encode())
                    fields = dict(line.split(b': ', 1) for line in lines[1:])
                    self.assertEqual(fields[b'Host'], request.endpoint[8:].encode())
                    self.assertEqual(fields[b'Content-Type'], request.content_type.encode())
                    self.assertEqual(fields[b'Content-Length'], str(len(request.stdin)).encode())
                    self.assertEqual(body, request.stdin)
                    expected = a.Resource.parse(a.strict_json(request.expected)['resource'])
                    value = a.strict_json(body)
                    if request.method == 'DELETE':
                        self.assertEqual(value, {'apiVersion': 'v1', 'kind': 'DeleteOptions', 'propagationPolicy': 'Foreground',
                            'preconditions': {'uid': expected.uid, 'resourceVersion': expected.resource_version}})
                    else:
                        self.assertEqual(value[0], {'op': 'test', 'path': '/metadata/uid', 'value': expected.uid})
                        self.assertEqual(value[1], {'op': 'test', 'path': '/metadata/resourceVersion', 'value': expected.resource_version})
                        if request.name == 'resume-replicas':
                            self.assertEqual(value[2:], [{'op': 'test', 'path': '/spec/replicas', 'value': 0},
                                {'op': 'replace', 'path': '/spec/replicas', 'value': boundary.binding.previous_replicas}])


if __name__ == '__main__':
    unittest.main(verbosity=2)
