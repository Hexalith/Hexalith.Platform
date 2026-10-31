import ast
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

SOURCE = Path(__file__).with_name('metadata.py')
spec = importlib.util.spec_from_file_location('native_node_metadata', SOURCE)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
NODE_UID = '11111111-1111-4111-8111-111111111111'
NAMESPACE_UID = '22222222-2222-4222-8222-222222222222'
IMAGE = 'docker.io/library/python@sha256:' + 'a' * 64


class Sink:
    def __init__(self):
        self.exports = []

    def encrypt(self, name, data, *_):
        self.exports.append((name, data))


class FakeClient:
    fault = None
    latest = None

    def __init__(self, args, attempt):
        type(self).latest = self
        self.args, self.objects, self.requests, self.node_uid = args, {}, [], NODE_UID

    def protected_state(self):
        return [{'namespace': 'application', 'name': 'data', 'uid': 'protected', 'volumeName': 'data-pv'}]

    def run(self, verb, path, body=None, absent=False):
        self.requests.append({'verb': verb, 'path': path})
        if path == '/api/v1/nodes/node1':
            return {'metadata': {'uid': self.node_uid}}
        if path == '/api/v1/namespaces/kube-system':
            return {'metadata': {'uid': NAMESPACE_UID}}
        if path.endswith('/log'):
            if self.fault == 'replace-node':
                self.node_uid = '33333333-3333-4333-8333-333333333333'
            if self.fault == 'failed-observation':
                raise ValueError('original-observation-refusal')
            return {'kind': 'node-file-metadata-only', 'entries': {'etc/synthetic': {'sha256': 'f' * 64}},
                    'sourceContentExported': False, 'networkOperations': 0}
        if verb == 'get':
            return copy.deepcopy(self.objects.get(path))
        if verb == 'create':
            value = copy.deepcopy(body)
            value['metadata'].update(uid='owned-' + body['kind'], resourceVersion='7')
            if body['kind'] == 'Pod':
                value['status'] = {'phase': 'Succeeded'}
                value['spec']['containers'][0].update(terminationMessagePath='/dev/termination-log',
                                                      terminationMessagePolicy='File')
            else:
                value['spec'].pop('ingress')
                value['spec'].pop('egress')
            self.objects[m.resource_path(body)] = value
            if self.fault == 'lost-create-response':
                raise TimeoutError('server accepted; response unobserved')
            return copy.deepcopy(value)
        if verb == 'delete':
            if self.fault == 'failed-observation':
                raise ValueError('secondary-cleanup-refusal')
            value = self.objects[path]
            assert body['preconditions'] == {'uid': value['metadata']['uid'],
                                            'resourceVersion': value['metadata']['resourceVersion']}
            assert body['propagationPolicy'] == 'Foreground'
            del self.objects[path]
            return {'kind': 'Status', 'status': 'Success'}
        raise AssertionError('unexpected request')


class MetadataTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.objects = m.manifests('test-attempt', 'node1', NODE_UID, NAMESPACE_UID, IMAGE)
        self.args = SimpleNamespace(node='node1', node_uid=NODE_UID, namespace_uid=NAMESPACE_UID,
                                    age='unused', recipient='unused')
        FakeClient.fault = None

    def tearDown(self):
        self.temp.cleanup()

    def observed(self, kind=1):
        value = copy.deepcopy(self.objects[kind])
        value['metadata'].update(uid='owned', resourceVersion='7')
        return value

    def test_manifests_have_only_fixed_readonly_mounts_and_no_privileged_or_network_process(self):
        policy, pod = self.objects
        self.assertEqual(policy['spec']['egress'], [])
        self.assertEqual(policy['spec']['ingress'], [])
        self.assertFalse(pod['spec']['automountServiceAccountToken'])
        self.assertTrue(all(v['readOnly'] for v in pod['spec']['containers'][0]['volumeMounts']))
        self.assertNotIn('/', {v['hostPath']['path'] for v in pod['spec']['volumes']})
        self.assertFalse(any('sock' in v['hostPath']['path'] or '/var/lib/etcd' in v['hostPath']['path']
                             for v in pod['spec']['volumes']))
        self.assertTrue(all(v['hostPath']['type'] in ('Directory', 'File') for v in pod['spec']['volumes']))
        imports = [node for node in ast.walk(ast.parse(m.program())) if isinstance(node, (ast.Import, ast.ImportFrom))]
        names = {alias.name for node in imports if isinstance(node, ast.Import) for alias in node.names}
        self.assertFalse(names & {'socket', 'ssl', 'urllib', 'http', 'subprocess', 'ctypes'})
        self.assertEqual(pod['spec']['containers'][0]['command'][-1], m.program())

    def test_unpinned_image_or_unsafe_attempt_is_rejected(self):
        for attempt, image in [('test', 'python:latest'), ('../escape', IMAGE)]:
            with self.subTest(attempt=attempt), self.assertRaises(ValueError):
                m.manifests(attempt, 'node1', NODE_UID, NAMESPACE_UID, image)

    def test_safe_api_defaults_and_calico_status_annotations_are_explicitly_normalized(self):
        value = self.observed()
        value['spec']['containers'][0].update(terminationMessagePath='/dev/termination-log', terminationMessagePolicy='File')
        value['spec'].update(serviceAccountName='default', dnsPolicy='ClusterFirst', schedulerName='default-scheduler', priority=0)
        value['metadata']['annotations'].update({'cni.projectcalico.org/containerID': 'a' * 64,
                                                 'cni.projectcalico.org/podIP': '10.0.0.1/32',
                                                 'cni.projectcalico.org/podIPs': '10.0.0.1/32'})
        m.assert_owned(value, self.objects[1], 'owned')
        policy = self.observed(0)
        policy['spec'].pop('ingress')
        policy['spec'].pop('egress')
        m.assert_owned(policy, self.objects[0], 'owned')

    def test_lifecycle_probes_arguments_environment_security_and_volume_injection_are_refused(self):
        attacks = [lambda v: v['spec']['containers'][0].update(lifecycle={'postStart': {'exec': {'command': ['sh', '-c', 'network']}}}),
                   lambda v: v['spec']['containers'][0].update(livenessProbe={'httpGet': {'host': 'source-api', 'port': 443}}),
                   lambda v: v['spec']['containers'][0].update(args=['unreviewed']),
                   lambda v: v['spec']['containers'][0].update(env=[{'name': 'PYTHONPATH', 'value': 'unreviewed'}]),
                   lambda v: v['spec']['containers'][0]['securityContext'].update(privileged=True),
                   lambda v: v['spec']['securityContext'].update(sysctls=[{'name': 'unreviewed', 'value': '1'}]),
                   lambda v: v['spec']['volumes'].append({'name': 'runtime', 'hostPath': {'path': '/var/run/containerd.sock'}}),
                   lambda v: v['spec'].update(hostNetwork=True),
                   lambda v: v['spec'].update(runtimeClassName='unreviewed')]
        for attack in attacks:
            value = self.observed()
            attack(value)
            with self.subTest(value=value), self.assertRaises(ValueError):
                m.assert_owned(value, self.objects[1], 'owned')

    def test_metadata_hashes_contents_without_exporting_them_and_never_follows_links(self):
        tree = self.root / 'source'
        tree.mkdir()
        (tree / 'etc').mkdir()
        secret = b'unique-source-secret-sentinel'
        (tree / 'etc/config').write_bytes(secret)
        outside = self.root / 'outside'
        outside.write_bytes(b'outside-private-material')
        (tree / 'etc/link').symlink_to(outside)
        result = m.collect_metadata(tree, ['etc'], [], require_readonly=False)
        self.assertEqual(result['entries']['etc/config']['sha256'], hashlib.sha256(secret).hexdigest())
        self.assertEqual(result['entries']['etc/link']['kind'], 'symlink')
        self.assertNotIn(secret.decode(), json.dumps(result))
        self.assertEqual(result['hashedBytes'], len(secret))
        self.assertEqual(outside.read_bytes(), b'outside-private-material')

    def test_ancestor_symlinks_special_files_and_writable_source_mounts_are_refused(self):
        outside = self.root / 'outside'
        outside.mkdir()
        (outside / 'config').write_bytes(b'private')
        (self.root / 'escape').symlink_to(outside, target_is_directory=True)
        with self.assertRaises(OSError):
            m.collect_metadata(self.root, ['escape/config'], [], require_readonly=False)
        os.mkfifo(self.root / 'pipe')
        with self.assertRaisesRegex(ValueError, 'special-file'):
            m.collect_metadata(self.root, ['pipe'], [], require_readonly=False)
        with self.assertRaisesRegex(ValueError, 'source-mount-not-readonly'):
            m.collect_metadata(self.root, [], ['/outside'])

    def test_source_replacement_during_hash_is_detected(self):
        path = self.root / 'file'
        path.write_bytes(b'original')
        read = os.read
        changed = []
        def replace(fd, count):
            chunk = read(fd, count)
            if not changed:
                changed.append(True)
                path.unlink()
                path.symlink_to(self.root / 'outside')
            return chunk
        with patch('os.read', replace), self.assertRaises(ValueError):
            m.collect_metadata(self.root, ['file'], [], require_readonly=False)

    def test_success_uses_exact_uid_rv_cleanup_and_preserves_source(self):
        sink = Sink()
        with patch.object(m, 'Client', FakeClient):
            result = m.collect(self.args, self.objects, sink)
        self.assertIsNone(result['error'])
        self.assertTrue(result['metadataObserved'])
        self.assertTrue(result['cleanupPassed'])
        self.assertTrue(result['sourceNodeAndNamespaceUidRevalidated'])
        self.assertTrue(result['namespacePvcPvIdentitiesAndBindingsUnchanged'])
        self.assertEqual(FakeClient.latest.objects, {})
        self.assertEqual(len([x for x in result['requests'] if x['verb'] == 'create']), 2)

    def test_lost_create_response_retains_unverified_object_and_never_invents_delete_authority(self):
        FakeClient.fault = 'lost-create-response'
        with patch.object(m, 'Client', FakeClient):
            result = m.collect(self.args, self.objects, Sink())
        self.assertFalse(result['cleanupPassed'])
        self.assertFalse(result['cleanup'][0]['ownershipVerified'])
        self.assertEqual(result['helperCreationIntents'], 1)
        self.assertEqual(result['helperObjectsCreated'], 0)
        self.assertEqual(result['helperCreateOutcomesUnverified'], 1)
        self.assertEqual(len(FakeClient.latest.objects), 1)
        self.assertFalse(any(x['verb'] == 'delete' for x in result['requests']))
        self.assertEqual(len([x for x in result['requests'] if x['verb'] == 'create']), 1)

    def test_node_replacement_after_observation_prevents_acceptance_and_cleans_only_owned_helpers(self):
        FakeClient.fault = 'replace-node'
        with patch.object(m, 'Client', FakeClient):
            result = m.collect(self.args, self.objects, Sink())
        self.assertEqual(result['error'], 'ValueError:metadata-source-identity-drift')
        self.assertFalse(result['sourceNodeAndNamespaceUidRevalidated'])
        self.assertTrue(result['cleanupPassed'])

    def test_original_observation_error_survives_secondary_cleanup_and_custody_refusal(self):
        FakeClient.fault = 'failed-observation'
        sink = Sink()
        sink.encrypt = lambda *_: (_ for _ in ()).throw(ValueError('secondary-custody-refusal'))
        with patch.object(m, 'Client', FakeClient):
            result = m.collect(self.args, self.objects, sink)
        self.assertEqual(result['error'], 'ValueError:original-observation-refusal')
        self.assertFalse(result['cleanupPassed'])

    def test_nonstatic_config_refuses_before_first_network_call(self):
        kubectl, config = self.root / 'kubectl', self.root / 'native'
        kubectl.write_bytes(b'tool');config.write_bytes(b'static-input')
        args = SimpleNamespace(kubectl=kubectl, kubeconfig=config, context='explicit', age='unused', recipient='unused')
        response = {'users': [{'user': {'exec': {'command': 'unbound'}}}],
                    'clusters': [{'cluster': {'server': 'https://127.0.0.1:6443', 'certificate-authority-data': 'public'}}]}
        with patch.object(m, 'bounded_command', return_value=subprocess.CompletedProcess([], 0, json.dumps(response).encode(), b'')) as call:
            with self.assertRaisesRegex(ValueError, 'static-direct-native-client'):
                m.Client(args, Sink())
        self.assertEqual(call.call_count, 1)
        self.assertIn('config', call.call_args.args[0])
        self.assertNotIn('get', call.call_args.args[0])
        self.assertNotIn('--flatten', call.call_args.args[0])

    def test_process_io_bounds_input_and_output_and_preserves_binary_bytes(self):
        raw = b'private\x00bytes' * 6000
        result = m.bounded_command([sys.executable, '-I', '-S', '-c', 'import sys;sys.stdout.buffer.write(sys.stdin.buffer.read())'], raw)
        self.assertEqual(result.stdout, raw)
        with self.assertRaisesRegex(ValueError, 'output-bound'):
            m.bounded_command([sys.executable, '-I', '-S', '-c', 'import sys;sys.stdout.write("x"*10000)'], maximum=100)
        with self.assertRaisesRegex(ValueError, 'deadline'):
            m.bounded_command([sys.executable, '-I', '-S', '-c', 'import time;time.sleep(5)'], timeout=.05)


if __name__ == '__main__':
    unittest.main()
