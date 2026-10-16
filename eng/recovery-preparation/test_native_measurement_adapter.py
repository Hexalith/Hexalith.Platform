"""Synthetic-only adapter qualification; never read a config or credential."""

from copy import deepcopy
from datetime import datetime, timezone
import os
import stat
import types
import tempfile
import marshal
import importlib.util
import io
import json
import hashlib
from pathlib import Path
import socket
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


guard = load_source(Path(__file__).with_name('native_measurement_adapter.py'), 'guard_under_test')
SECRET = 'synthetic-sensitive-token-never-output'
UID = '01234567-89ab-cdef-0123-456789abcdef'
IMAGE = 'synthetic.invalid/repository@sha256:' + 'a' * 64
EXPECTED_PLAN = [
    ('namespace-kube-system', 'GET', '/api/v1/namespaces/kube-system', 'b5860674-1d1e-4a3b-b413-e1bb3b9a4557'),
    ('namespace-keycloak', 'GET', '/api/v1/namespaces/keycloak', '22744997-bdad-4ac5-85bf-631fc700d4ea'),
    ('namespace-openbao', 'GET', '/api/v1/namespaces/openbao', None),
    ('namespace-hexalith-memories', 'GET', '/api/v1/namespaces/hexalith-memories', None),
    ('version', 'GET', '/version', None),
    ('readyz', 'GET', '/readyz?verbose', None),
    ('keycloak-postgres', 'GET', '/apis/postgresql.cnpg.io/v1/namespaces/keycloak/clusters/keycloak-postgres', '56d8d39c-d688-48f2-aa27-a549400352ce'),
    ('keycloak-keycloak', 'GET', '/apis/apps/v1/namespaces/keycloak/deployments/keycloak', '98fc84f3-918f-46d9-ae22-7584fb31ff9b'),
    ('openbao-hexalith-keys', 'GET', '/apis/apps/v1/namespaces/openbao/statefulsets/hexalith-keys', 'df2bbbbe-1fe8-4e0b-a8a6-174bd3287806'),
    ('hexalith-memories-redis-stack', 'GET', '/apis/apps/v1/namespaces/hexalith-memories/statefulsets/redis-stack', None),
    ('hexalith-memories-falkordb', 'GET', '/apis/apps/v1/namespaces/hexalith-memories/statefulsets/falkordb', None),
    ('hexalith-memories-memories', 'GET', '/apis/apps/v1/namespaces/hexalith-memories/deployments/memories', None),
    ('hexalith-memories-memories-mcp', 'GET', '/apis/apps/v1/namespaces/hexalith-memories/deployments/memories-mcp', None),
    ('hexalith-memories-memories-access-telemetry', 'GET', '/apis/apps/v1/namespaces/hexalith-memories/deployments/memories-access-telemetry', None),
    ('hexalith-memories-memories-access-telemetry-clock', 'GET', '/apis/apps/v1/namespaces/hexalith-memories/deployments/memories-access-telemetry-clock', None),
    ('keycloak-service', 'GET', '/api/v1/namespaces/keycloak/services/keycloak', '0ac9cd04-9d7e-41ed-8ac7-5a74ca659fd0'),
    ('openebs-hostpath', 'GET', '/apis/storage.k8s.io/v1/storageclasses/openebs-hostpath', None),
    ('openebs-hostpath-retain', 'GET', '/apis/storage.k8s.io/v1/storageclasses/openebs-hostpath-retain', None),
    ('native-binding', 'GET', '/apis/rbac.authorization.k8s.io/v1/clusterrolebindings/kubeadm:cluster-admins', '8224bcb7-57c6-437d-a27c-1120d03c8b4b'),
    ('absent-s40-kcpre-20261015t162756z', 'GET', '/api/v1/namespaces/s40-kcpre-20261015t162756z', None),
    ('absent-s40-kc-20261015t162756z', 'GET', '/api/v1/namespaces/s40-kc-20261015t162756z', None),
    ('absent-s40-bao-20261015t162756z', 'GET', '/api/v1/namespaces/s40-bao-20261015t162756z', None),
    ('absent-s40-mem-20261015t162756z', 'GET', '/api/v1/namespaces/s40-mem-20261015t162756z', None),
]


def expected_identity(name, namespace='', uid=UID):
    return {'identity': {'name': name, 'namespace': namespace, 'uid': uid}}


def expected_outputs():
    """Independent exact receipt expectations, not reconstructed from the plan."""
    images = {'imageDigests': ['sha256:' + 'a' * 64]}
    replicas = {'replicas': 2, 'readyReplicas': 2}
    storage = {'provisioner': 'openebs.io/local', 'volumeBindingMode': 'WaitForFirstConsumer'}
    return [
        expected_identity('kube-system', uid='b5860674-1d1e-4a3b-b413-e1bb3b9a4557'),
        expected_identity('keycloak', uid='22744997-bdad-4ac5-85bf-631fc700d4ea'),
        expected_identity('openbao'), expected_identity('hexalith-memories'),
        {'version': 'v1.34.12'}, {'ready': True},
        {**expected_identity('keycloak-postgres', 'keycloak', '56d8d39c-d688-48f2-aa27-a549400352ce'),
         'systemId': '7689076343157272595', 'timeline': 2, 'primaryOrdinal': 2, **images},
        {**expected_identity('keycloak', 'keycloak', '98fc84f3-918f-46d9-ae22-7584fb31ff9b'), **replicas, **images},
        {**expected_identity('hexalith-keys', 'openbao', 'df2bbbbe-1fe8-4e0b-a8a6-174bd3287806'), **replicas, **images},
        {**expected_identity('redis-stack', 'hexalith-memories'), **replicas, **images},
        {**expected_identity('falkordb', 'hexalith-memories'), **replicas, **images},
        {**expected_identity('memories', 'hexalith-memories'), **replicas},
        {**expected_identity('memories-mcp', 'hexalith-memories'), **replicas},
        {**expected_identity('memories-access-telemetry', 'hexalith-memories'), **replicas},
        {**expected_identity('memories-access-telemetry-clock', 'hexalith-memories'), **replicas},
        {**expected_identity('keycloak', 'keycloak', '0ac9cd04-9d7e-41ed-8ac7-5a74ca659fd0'),
         'serviceType': 'ClusterIP', 'ports': [{'port': 8080, 'protocol': 'TCP', 'targetPort': 8081,
             'nameSha256': hashlib.sha256(SECRET.encode()).hexdigest()}]},
        {**expected_identity('openebs-hostpath'), 'storageClassShape': {**storage, 'reclaimPolicy': 'Delete'}},
        {**expected_identity('openebs-hostpath-retain'), 'storageClassShape': {**storage, 'reclaimPolicy': 'Retain'}},
        {**expected_identity('kubeadm:cluster-admins', uid='8224bcb7-57c6-437d-a27c-1120d03c8b4b'),
         'historicalNativeBindingShape': True, 'currentAuthorityAccepted': False},
        {'targetAbsent': True, 'name': 's40-kcpre-20261015t162756z'},
        {'targetAbsent': True, 'name': 's40-kc-20261015t162756z'},
        {'targetAbsent': True, 'name': 's40-bao-20261015t162756z'},
        {'targetAbsent': True, 'name': 's40-mem-20261015t162756z'},
    ]


def fixture(request):
    """Each supported selector has a typed fixture independent of transport."""
    mode = request['mode']
    if mode == 'readyz':
        return guard.Response(200, 'text/plain', (b'[+]ping ok\n[+]etcd ok\nreadyz check passed\n',))
    if mode == 'version':
        data = {'major': '1', 'minor': '34', 'gitVersion': 'v1.34.12', 'diagnostic': SECRET}
    elif mode == 'absence':
        data = {'kind': 'Status', 'apiVersion': 'v1', 'status': 'Failure', 'reason': 'NotFound',
                'code': 404, 'details': {'name': request['name'], 'kind': 'namespaces'},
                'message': SECRET}
    else:
        data = {'kind': request['kind'], 'apiVersion': request['apiVersion'],
                'metadata': {'name': request['name'], 'namespace': request['namespace'],
                             'uid': request['historicalUid'] or UID,
                             'annotations': {'secret': SECRET}, 'managedFields': [SECRET]}}
        if mode == 'workload':
            data['metadata']['generation'] = 3
            data.update(spec={'replicas': 2, 'template': {'spec': {
                'containers': [{'name': 'main', 'image': IMAGE, 'env': [{'value': SECRET}]}]}}},
                status={'readyReplicas': 2, 'observedGeneration': 3, 'message': SECRET})
        elif mode == 'postgres':
            data.update(spec={'imageName': IMAGE}, status={'conditions': [
                {'type': 'Ready', 'status': 'True', 'message': SECRET}],
                'systemID': '7689076343157272595', 'timelineID': 2,
                'currentPrimary': 'keycloak-postgres-2'})
        elif mode == 'service':
            data['spec'] = {'type': 'ClusterIP', 'ports': [
                {'port': 8080, 'targetPort': 8081, 'protocol': 'TCP', 'name': SECRET}]}
        elif mode == 'storageclass':
            data.update(provisioner='openebs.io/local', volumeBindingMode='WaitForFirstConsumer',
                        reclaimPolicy='Retain' if request['name'].endswith('-retain') else 'Delete',
                        parameters={'secret': SECRET})
        elif mode == 'authority':
            data.update(roleRef={'apiGroup': 'rbac.authorization.k8s.io', 'kind': 'ClusterRole',
                                 'name': 'cluster-admin'},
                        subjects=[{'kind': 'Group', 'apiGroup': 'rbac.authorization.k8s.io',
                                   'name': 'kubeadm:cluster-admins'}])
    return guard.Response(404 if mode == 'absence' else 200, 'application/json',
                          (json.dumps(data).encode(),))


class NativeMeasurementTests(unittest.TestCase):
    def setUp(self):
        self.runtime = 'b' * 64
        self.binding = guard.binding_template(self.runtime)
        self.now = datetime(2026, 10, 16, 16, 0, tzinfo=timezone.utc)
        self.socket_patch = patch.object(socket, 'socket', side_effect=AssertionError('network forbidden'))
        self.socket_patch.start()
        self.addCleanup(self.socket_patch.stop)

    def run_fixture(self, binding=None, responses=None, runtime=None, now=None):
        raw = json.dumps(self.binding if binding is None else binding, sort_keys=True).encode()
        transport = guard.SyntheticTransport([fixture(request) for request in guard.request_plan()]
                                             if responses is None else responses)
        result = guard.qualify_synthetic(raw, guard.sha(raw), self.runtime if runtime is None else runtime,
                                        transport, self.now if now is None else now)
        self.assertNotIn(SECRET, json.dumps(result))
        self.assertEqual(result['operationalGate'], 'closed')
        self.assertEqual(result['apiCalls'], 0)
        self.assertFalse(result['authorityAccepted'])
        return result, transport

    def test_every_supported_selector_passes_with_only_typed_nonsecret_output(self):
        result, transport = self.run_fixture()
        self.assertEqual(result['qualification'], 'pass')
        self.assertEqual([(r['id'], r['method'], r['path'], r['historicalUid'])
                          for r in transport.requests], EXPECTED_PLAN)
        self.assertEqual(len(result['measurements']), 23)
        self.assertEqual(result['measurements'], [
            {'selector': row[0], 'output': output}
            for row, output in zip(EXPECTED_PLAN, expected_outputs(), strict=True)])
        self.assertLess(len(json.dumps(result)), guard.MAX_OUTPUT)
        self.assertEqual(transport.requests, guard.request_plan())
        self.assertFalse(result['authenticationPerformed'])
        self.assertFalse(result['captureOrExecutionAuthorized'])
        self.assertFalse(result['providerOrIamQualified'])

    def test_source_loader_ignores_eligible_unbound_bytecode(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / 'fixture.py'
            source.write_bytes(b'VALUE = "reviewed-source"\n')
            info = source.stat()
            cache = Path(importlib.util.cache_from_source(str(source)))
            cache.parent.mkdir(mode=0o700)
            header = (importlib.util.MAGIC_NUMBER + (0).to_bytes(4, 'little') +
                      int(info.st_mtime).to_bytes(4, 'little') + info.st_size.to_bytes(4, 'little'))
            cache.write_bytes(header + marshal.dumps(compile('VALUE = "unbound-cache"', str(source), 'exec')))
            self.assertEqual(load_source(source, 'reviewed_fixture').VALUE, 'reviewed-source')

    def test_source_binding_refuses_symlink_ancestor_fifo_hardlink_and_oversize_before_read(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'source.py'
            source.write_bytes(b'inert source bytes')
            link = root / 'link.py'
            link.symlink_to(source)
            fifo = root / 'fifo.py'
            os.mkfifo(fifo, 0o600)
            actual = root / 'actual'
            actual.mkdir(mode=0o700)
            (actual / 'child.py').write_bytes(b'inert')
            alias = root / 'alias'
            alias.symlink_to(actual, target_is_directory=True)
            large = root / 'large.py'
            with large.open('wb') as stream:
                stream.truncate(1048577)
            with patch.object(guard.os, 'read', side_effect=AssertionError('unsafe input read')):
                for path in (link, fifo, alias / 'child.py', large):
                    with self.subTest(path=path.name), self.assertRaises((OSError, ValueError)):
                        guard._file_binding(path)
            hardlink = root / 'hardlink.py'
            os.link(source, hardlink)
            with patch.object(guard.os, 'read', side_effect=AssertionError('hardlink read')):
                with self.assertRaises(ValueError): guard._file_binding(hardlink)
            hardlink.unlink()
            with patch.object(Path, 'read_bytes', side_effect=AssertionError('unbounded path read')):
                self.assertEqual(guard._file_binding(source), {
                    'path': str(source), 'bytes': 18,
                    'sha256': hashlib.sha256(b'inert source bytes').hexdigest()})

    def test_source_binding_bounds_each_descriptor_read_and_detects_mutation(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / 'source.py'
            source.write_bytes(b'x' * 100000)
            real_read = os.read
            with patch.object(guard.os, 'read', wraps=real_read) as read:
                self.assertEqual(len(guard.read_source_bytes(source)), 100000)
            self.assertTrue(all(call.args[1] <= 65536 for call in read.call_args_list))
            changed = False
            def mutate(descriptor, maximum):
                nonlocal changed
                chunk = real_read(descriptor, maximum)
                if not changed:
                    changed = True
                    source.write_bytes(b'y' * 100001)
                return chunk
            with patch.object(guard.os, 'read', side_effect=mutate):
                with self.assertRaises(ValueError): guard.read_source_bytes(source)

    def test_request_deadline_covers_sanitizer_before_any_dependent_request(self):
        clock = [0]
        sanitize = guard.sanitize
        def slow_sanitize(*args):
            result = sanitize(*args)
            clock[0] += 16
            return result
        with patch.object(guard.time, 'monotonic', side_effect=lambda: clock[0]), \
                patch.object(guard, 'sanitize', side_effect=slow_sanitize):
            result, transport = self.run_fixture()
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(len(transport.requests), 1)
        self.assertEqual(result['measurements'], [])

    def test_final_receipt_cap_includes_binding_and_runtime_digest_fields(self):
        responses = [fixture(request) for request in guard.request_plan()]
        for index, request in enumerate(guard.request_plan()):
            if request['mode'] == 'workload':
                data = json.loads(responses[index].chunks[0])
                data['spec']['template']['spec']['containers'] *= 64
                responses[index] = guard.Response(200, 'application/json', (json.dumps(data).encode(),))
        successful, _ = self.run_fixture(responses=responses)
        self.assertEqual(successful['qualification'], 'pass')
        cap = len(json.dumps(successful, sort_keys=True).encode()) - 1
        with patch.object(guard, 'MAX_OUTPUT', cap):
            self.binding = guard.binding_template(self.runtime)
            self.assertLess(len(json.dumps(self.binding, sort_keys=True).encode()), cap)
            result, transport = self.run_fixture(responses=responses)
        self.assertEqual(len(transport.requests), 23)
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(result['measurements'], [])

    def test_workload_requires_positive_generation_and_current_observed_generation(self):
        request = next(r for r in guard.request_plan() if r['mode'] == 'workload')
        data = json.loads(fixture(request).chunks[0])
        for generation, observed in ((None, 3), (0, 0), (-1, -1), (True, 1),
                                     (3, None), (3, 2), (3, 4), (1, True)):
            changed = deepcopy(data)
            changed['metadata']['generation'] = generation
            changed['status']['observedGeneration'] = observed
            with self.subTest(generation=generation, observed=observed), self.assertRaises(guard.Refusal):
                guard.sanitize(request, 200, 'application/json', json.dumps(changed).encode())

    def test_service_preserves_numeric_and_named_target_node_and_port_name_bindings(self):
        request = next(r for r in guard.request_plan() if r['mode'] == 'service')
        data = json.loads(fixture(request).chunks[0])
        data['spec'] = {'type': 'NodePort', 'ports': [
            {'port': 443, 'targetPort': 8443, 'nodePort': 30443, 'protocol': 'TCP', 'name': 'https'},
            {'port': 80, 'targetPort': 'web-target', 'nodePort': 30080, 'name': 'http'},
            {'port': 9090, 'nodePort': 30090}]}
        output = guard.sanitize(request, 200, 'application/json', json.dumps(data).encode())
        self.assertEqual(output['ports'], [
            {'port': 443, 'targetPort': 8443, 'nodePort': 30443, 'protocol': 'TCP',
             'nameSha256': hashlib.sha256(b'https').hexdigest()},
            {'port': 80, 'targetPortNameSha256': hashlib.sha256(b'web-target').hexdigest(),
             'nodePort': 30080, 'protocol': 'TCP', 'nameSha256': hashlib.sha256(b'http').hexdigest()},
            {'port': 9090, 'targetPort': 9090, 'nodePort': 30090, 'protocol': 'TCP'}])
        for target in (False, 0, 65536, '123', 'contains spaces', 'name-too-long-1234'):
            changed = deepcopy(data)
            changed['spec']['ports'][0]['targetPort'] = target
            with self.subTest(target=target), self.assertRaises(guard.Refusal):
                guard.sanitize(request, 200, 'application/json', json.dumps(changed).encode())

    def test_wrong_packet_source_tests_transport_runtime_authority_and_scope_refuse_before_transport(self):
        mutations = [lambda b: b['immutableInputs'].__setitem__('packet-v4.json', 'c' * 64),
                     lambda b: b['source'].__setitem__('sha256', 'c' * 64),
                     lambda b: b['tests'].__setitem__('sha256', 'c' * 64),
                     lambda b: b['httpsSource'].__setitem__('sha256', 'c' * 64),
                     lambda b: b['httpsTests'].__setitem__('sha256', 'c' * 64),
                     lambda b: b.__setitem__('runtimeInventorySha256', 'c' * 64),
                     lambda b: b['contract']['requests'][0].__setitem__('path', '/api/v1/secrets'),
                     lambda b: b['contract']['transport'].__setitem__('ip', '127.0.0.1'),
                     lambda b: b['contract']['transport'].__setitem__('retries', 1),
                     lambda b: b['authority'].__setitem__('apiCaSha256', 'c' * 64),
                     lambda b: b.__setitem__('schemaVersion', True),
                     lambda b: b.__setitem__('policyCeiling', '2099-01-01T00:00:00Z')]
        for mutate in mutations:
            with self.subTest(mutation=mutations.index(mutate)):
                changed = deepcopy(self.binding)
                mutate(changed)
                result, transport = self.run_fixture(binding=changed)
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(transport.requests, [])

    def test_wrong_independent_binding_digest_or_runtime_digest_refuses_before_transport(self):
        raw = json.dumps(self.binding).encode()
        for digest, runtime in [('c' * 64, self.runtime), (guard.sha(raw), 'c' * 64)]:
            transport = guard.SyntheticTransport([])
            result = guard.qualify_synthetic(raw, digest, runtime, transport, self.now)
            self.assertEqual(result['qualification'], 'refused')
            self.assertEqual(transport.requests, [])

    def test_no_live_or_arbitrary_callback_transport_can_be_supplied(self):
        raw = json.dumps(self.binding).encode()
        for transport in (lambda *a: None, object()):
            result = guard.qualify_synthetic(raw, guard.sha(raw), self.runtime, transport, self.now)
            self.assertEqual(result['syntheticRequests'], 0)

    def test_all_live_entry_points_refuse_before_file_socket_or_argument_access(self):
        class Poison:
            def __str__(self):
                raise AssertionError('argument consulted')
        with patch.object(Path, 'read_bytes', side_effect=AssertionError('file access forbidden')):
            with self.assertRaises(guard.Refusal):
                guard.activate_live(Poison(), config=Poison())
            output = io.StringIO()
            with patch('sys.stdout', output), patch('sys.argv', [SECRET, '--live', SECRET]):
                self.assertEqual(guard.main(), 1)
        self.assertNotIn(SECRET, output.getvalue())
        self.assertEqual(json.loads(output.getvalue())['operationalGate'], 'closed')

    def test_expiry_is_reported_without_refreshing_times_or_enabling_live(self):
        for now, expired in ((self.now, False),
                (datetime(2026, 10, 16, 16, 27, 56, tzinfo=timezone.utc), True),
                (datetime(2026, 10, 17, tzinfo=timezone.utc), True)):
            result, _ = self.run_fixture(now=now)
            self.assertEqual(result['qualification'], 'pass')
            self.assertEqual(result['policyExpired'], expired)
            self.assertEqual(result['policyPreparedAt'], guard.PREPARED)
            self.assertEqual(result['policyCeiling'], guard.CEILING)
            with self.assertRaises(guard.Refusal): guard.activate_live(now_utc=now)

    def test_untrusted_or_naive_time_refuses_without_echo(self):
        raw = json.dumps(self.binding).encode()
        for now in (SECRET, datetime(2026, 10, 16)):
            result = guard.qualify_synthetic(raw, guard.sha(raw), self.runtime,
                                            guard.SyntheticTransport([]), now)
            self.assertEqual(result['qualification'], 'refused')
            self.assertNotIn(SECRET, json.dumps(result))

    def test_error_redirect_rights_failure_and_unknown_status_stop_without_dependent_requests(self):
        for status in (0, 301, 302, 307, 401, 403, 404, 429, 500, True):
            responses = [guard.Response(status, 'application/json', (SECRET.encode(),))]
            responses.extend(fixture(request) for request in guard.request_plan()[1:])
            result, transport = self.run_fixture(responses=responses)
            self.assertEqual(result['qualification'], 'refused')
            self.assertEqual(len(transport.requests), 1)
            self.assertEqual(result['measurements'], [])

    def test_missing_malformed_duplicate_recursive_and_sensitive_body_are_sanitized(self):
        for raw in (b'', SECRET.encode(), b'{"token":', b'{"a":1,"a":2}',
                    b'{"n":NaN}', b'{"n":0.1}', b'{"n":' + b'9' * 10000 + b'}',
                    b'[' * 2000 + b']' * 2000, b'\xff'):
            with self.subTest(size=len(raw)):
                result, transport = self.run_fixture(responses=[guard.Response(200, 'application/json', (raw,))])
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(len(transport.requests), 1)

    def test_oversized_response_and_excess_chunks_are_stopped_before_buffering(self):
        response = guard.Response(200, 'application/json', (b'x' * guard.MAX_BODY, b'x'))
        result, transport = self.run_fixture(responses=[response])
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(len(transport.requests), 1)
        with self.assertRaises(guard.Refusal): guard.Response(200, 'application/json', [b'x'] * 65)
        with self.assertRaises(guard.Refusal): guard.Response(200, 'application/json', iter([b'x']))

    def test_total_lane_body_and_output_bounds_stop_subsequent_requests(self):
        responses = [fixture(request) for request in guard.request_plan()]
        with patch.object(guard, 'MAX_TOTAL_BODY', len(responses[0].chunks[0]) + 1):
            self.binding = guard.binding_template(self.runtime)
            result, transport = self.run_fixture(responses=responses)
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(len(transport.requests), 2)
        responses = [fixture(request) for request in guard.request_plan()]
        for index, request in enumerate(guard.request_plan()):
            if request['mode'] == 'workload':
                data = json.loads(responses[index].chunks[0])
                data['spec']['template']['spec']['containers'] *= 64
                responses[index] = guard.Response(200, 'application/json', (json.dumps(data).encode(),))
        with patch.object(guard, 'MAX_OUTPUT', 17000):
            self.binding = guard.binding_template(self.runtime)
            result, transport = self.run_fixture(responses=responses)
        self.assertEqual(result['qualification'], 'refused')
        self.assertGreater(len(transport.requests), 1)
        self.assertLess(len(transport.requests), len(guard.request_plan()))
        self.assertEqual(result['measurements'], [])

    def test_absolute_deadline_timeout_stops_first_request_and_removes_partial_output(self):
        raw = json.dumps(self.binding).encode()
        transport = guard.SyntheticTransport([fixture(request) for request in guard.request_plan()])
        with patch.object(guard.time, 'monotonic', side_effect=[0, 0, 0, 0, 16]):
            result = guard.qualify_synthetic(raw, guard.sha(raw), self.runtime, transport, self.now)
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(len(transport.requests), 1)
        self.assertEqual(result['measurements'], [])

    def test_prior_failure_does_not_allow_reusing_transport_or_retry(self):
        result, transport = self.run_fixture(responses=[guard.Response(403, 'application/json', (b'{}',))])
        raw = json.dumps(self.binding).encode()
        result = guard.qualify_synthetic(raw, guard.sha(raw), self.runtime, transport, self.now)
        self.assertEqual(result['syntheticRequests'], 0)
        self.assertEqual(len(transport.requests), 1)

    def test_wrong_resource_uid_name_namespace_kind_and_type_refuse(self):
        request = guard.request_plan()[0]
        response = fixture(request)
        data = json.loads(response.chunks[0])
        for field, value in (('uid', UID), ('uid', SECRET), ('name', SECRET), ('namespace', SECRET)):
            changed = deepcopy(data)
            changed['metadata'][field] = value
            with self.assertRaises(guard.Refusal): guard.sanitize(request, 200, 'application/json', json.dumps(changed).encode())
        for field, value in (('kind', SECRET), ('apiVersion', SECRET), ('metadata', [])):
            changed = deepcopy(data)
            changed[field] = value
            with self.assertRaises(guard.Refusal): guard.sanitize(request, 200, 'application/json', json.dumps(changed).encode())

    def test_absence_requires_explicit_404_typed_not_found_for_exact_target(self):
        request = next(r for r in guard.request_plan() if r['mode'] == 'absence')
        response = fixture(request)
        data = json.loads(response.chunks[0])
        for field, value in (('reason', 'Forbidden'), ('code', 403), ('kind', 'Namespace'), ('details', {})):
            changed = deepcopy(data)
            changed[field] = value
            with self.assertRaises(guard.Refusal): guard.sanitize(request, 404, 'application/json', json.dumps(changed).encode())
        with self.assertRaises(guard.Refusal): guard.sanitize(request, 200, 'application/json', response.chunks[0])

    def test_native_binding_is_only_historical_shape_and_never_independent_authority(self):
        request = next(r for r in guard.request_plan() if r['mode'] == 'authority')
        response = fixture(request)
        result = guard.sanitize(request, response.status, response.content_type, response.chunks[0])
        self.assertTrue(result['historicalNativeBindingShape'])
        self.assertFalse(result['currentAuthorityAccepted'])
        data = json.loads(response.chunks[0])
        for field, value in (('subjects', []), ('roleRef', {})):
            changed = deepcopy(data)
            changed[field] = value
            with self.assertRaises(guard.Refusal): guard.sanitize(request, 200, 'application/json', json.dumps(changed).encode())

    def test_unhealthy_workload_boolean_count_unpinned_image_and_item_overflow_refuse(self):
        request = next(r for r in guard.request_plan() if r['mode'] == 'workload')
        data = json.loads(fixture(request).chunks[0])
        variants = []
        changed = deepcopy(data); changed['status']['readyReplicas'] = 1; variants.append(changed)
        changed = deepcopy(data); changed['spec']['replicas'] = True; variants.append(changed)
        changed = deepcopy(data); changed['spec']['template']['spec']['containers'][0]['image'] = SECRET; variants.append(changed)
        changed = deepcopy(data); changed['spec']['template']['spec']['containers'] *= 65; variants.append(changed)
        for changed in variants:
            with self.assertRaises(guard.Refusal): guard.sanitize(request, 200, 'application/json', json.dumps(changed).encode())

    def test_memories_deployment_does_not_emit_unselected_image_field(self):
        request = next(r for r in guard.request_plan() if r['name'] == 'memories')
        response = fixture(request)
        result = guard.sanitize(request, 200, response.content_type, response.chunks[0])
        self.assertNotIn('imageDigests', result)

    def test_readyz_failure_raw_diagnostics_and_large_verbose_output_are_not_accepted(self):
        request = next(r for r in guard.request_plan() if r['mode'] == 'readyz')
        for raw in (b'[-]etcd failed\nreadyz check failed\n', SECRET.encode(), b'x' * 8193,
                    b'readyz check passed\n', b'[+]ping ok\n' * 65 + b'readyz check passed\n'):
            with self.assertRaises(guard.Refusal): guard.sanitize(request, 200, 'text/plain', raw)

    def test_unsupported_selectors_are_explicitly_blocked_and_not_in_request_plan(self):
        contract = guard.contract()
        self.assertEqual(len(contract['unsupportedSelectors']), 5)
        for request in guard.request_plan():
            self.assertEqual(request['method'], 'GET')
            self.assertNotIn('/secrets', request['path'])
            self.assertNotIn('/pods', request['path'])
            self.assertNotIn('/persistentvolumes', request['path'])
            self.assertNotIn('labelSelector', request['path'])
        self.assertEqual(contract['approvedCnpgWatchNamespaceReferenceOnly'], 'keycloak,cnpg-system')


if __name__ == '__main__':
    unittest.main(verbosity=2)
