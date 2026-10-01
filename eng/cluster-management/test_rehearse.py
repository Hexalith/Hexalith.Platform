"""Source refusal, exact retirement scope, drift and preservation assertions."""
import copy
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import Mock, patch
from evidence import canonical, digest
from qualify import project_resource
from rehearse import Fixture, absence_state, assert_preserved, extension_phase_actions, key, native_conflict, native_delete_options, probe_blocked, rehearse, validate_allowlist, validate_isolation


def resource(kind, name, uid, owner=None):
    return {'apiVersion': 'v1', 'kind': kind, 'namespace': 'fixture' if kind == 'ConfigMap' else None,
            'name': name, 'uid': uid, 'resourceVersion': '1', 'owners': [{'uid': owner}] if owner else [], 'finalizers': []}


def action(obj):
    return {**obj, 'action': 'delete', 'propagation': 'Foreground'}


class RehearsalTests(unittest.TestCase):
    def test_orphan_propagation_retains_children_and_stops_nested_cascade(self):
        owner, child = resource('ConfigMap', 'owner', 'owner-1'), resource('ConfigMap', 'child', 'child-1', 'owner-1')
        grandchild = resource('ConfigMap', 'grandchild', 'grandchild-1', 'child-1')
        orphan = {**action(owner), 'propagation': 'Orphan'}
        validate_allowlist([owner, child, grandchild], [orphan])
        with self.assertRaisesRegex(ValueError, 'unallowlisted'):
            validate_allowlist([owner, child, grandchild], [action(owner)])
        validate_allowlist([owner, child, grandchild], [action(owner), {**action(child), 'propagation': 'Orphan'}])
        namespace = resource('Namespace', 'application', 'ns-1', 'owner-1')
        validate_allowlist([owner, namespace], [orphan])
        with self.assertRaisesRegex(ValueError, 'protected'):
            validate_allowlist([owner, namespace], [{**action(namespace), 'propagation': 'Orphan'}])

    def test_protected_storage_properties_finalizers_and_deletion_timestamps_cannot_drift(self):
        pv = {'apiVersion': 'v1', 'kind': 'PersistentVolume', 'metadata': {'name': 'data', 'uid': 'pv-1', 'resourceVersion': '1'},
              'spec': {'storageClassName': 'local', 'persistentVolumeReclaimPolicy': 'Retain',
                       'accessModes': ['ReadWriteOnce'], 'volumeMode': 'Filesystem', 'hostPath': {'path': '/synthetic'}}}
        for field, value in [('persistentVolumeReclaimPolicy', 'Delete'), ('storageClassName', 'other'),
                             ('accessModes', ['ReadWriteMany']), ('volumeMode', 'Block'), ('hostPath', {'path': '/other'})]:
            changed = copy.deepcopy(pv);changed['spec'][field] = value
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, 'identity-or-binding'):
                assert_preserved([project_resource(pv)], [project_resource(changed)], set())
        storageclass = {'apiVersion': 'storage.k8s.io/v1', 'kind': 'StorageClass',
                        'metadata': {'name': 'local', 'uid': 'sc-1'}, 'provisioner': 'local.example', 'parameters': {'private': 'one'}}
        changed = copy.deepcopy(storageclass);changed['parameters']['private'] = 'two'
        with self.assertRaises(ValueError):assert_preserved([project_resource(storageclass)], [project_resource(changed)], set())
        namespace = resource('Namespace', 'app', 'ns-1');namespace['namespaceFinalizers'] = ['kubernetes']
        for updates in ({'deletionTimestamp': '2026-10-01T20:00:00Z'}, {'finalizers': ['new/finalizer']},
                        {'namespaceFinalizers': []}, {'owners': [{'uid': 'new-owner'}]}):
            with self.subTest(updates=updates), self.assertRaises(ValueError):
                assert_preserved([namespace], [{**namespace, **updates}], set())

    def test_extension_phase_cannot_use_later_core_scope_to_allow_unexpected_deletion(self):
        plan = resource('InstallPlan', 'ks-console-embed', 'plan-1')
        console = resource('Deployment', 'ks-console-embed', 'console-1')
        console['helmRelease'] = {'release-name': 'ks-console-embed'}
        core = resource('Deployment', 'ks-controller-manager', 'core-1')
        core['helmRelease'] = {'release-name': 'ks-core'}
        protected = resource('PersistentVolumeClaim', 'data', 'claim-1')
        before = [plan, console, core, protected]
        phase = extension_phase_actions(before, plan, [action(v) for v in (plan, console, core)])
        self.assertEqual({v['uid'] for v in phase}, {'plan-1', 'console-1'})
        assert_preserved(before, [core, protected], {key(v) for v in phase})
        with self.assertRaisesRegex(ValueError, 'unexpected'):
            assert_preserved(before, [protected], {key(v) for v in phase})
        with self.assertRaisesRegex(ValueError, 'outside-retirement'):
            extension_phase_actions(before, plan, [action(plan)])

    def test_daemon_failure_does_not_prove_cleanup_or_source_refusal(self):
        missing = SimpleNamespace(returncode=1, stderr=b'Error: No such object: fixture\n')
        self.assertEqual(absence_state(missing, 'container', 'fixture'), 'absent')
        missing.stderr = b'error: no such object: fixture\n'
        self.assertEqual(absence_state(missing, 'container', 'fixture'), 'absent')
        for error in (b'Cannot connect to the Docker daemon', b'permission denied', b'context deadline exceeded',
                      b'Error: No such object: fixture\nCannot connect to daemon'):
            result = SimpleNamespace(returncode=1, stderr=error, stdout=b'')
            self.assertEqual(absence_state(result, 'container', 'fixture'), 'unverified')
            with self.assertRaises(ValueError):probe_blocked(result)
        self.assertEqual(absence_state(SimpleNamespace(returncode=0, stderr=b''), 'container', 'fixture'), 'present')
        self.assertEqual(absence_state(SimpleNamespace(returncode=1, stdout=b'[]',
                         stderr=b'Error response from daemon: No such image: local:fixture\n'), 'image', 'local:fixture'), 'absent')
        self.assertTrue(probe_blocked(SimpleNamespace(returncode=0, stdout=b'blocked\n')))
        self.assertFalse(probe_blocked(SimpleNamespace(returncode=0, stdout=b'reachable\n')))

    def test_source_endpoint_uid_reachability_and_credentials_refused(self):
        source = {'sourceClusterUid': 'source-1'}
        fixture = {'endpoint': 'https://127.0.0.1:44443', 'clusterUid': 'fixture-1',
                   'sourceCredentialsImported': False, 'sourceDataImported': False}
        validate_isolation(source, fixture, True, [{'blocked': True}])
        for updates, internal, probes in [({'endpoint': 'https://192.168.1.30:6443'}, True, [{'blocked': True}]),
            ({'clusterUid': 'source-1'}, True, [{'blocked': True}]), ({'sourceCredentialsImported': True}, True, [{'blocked': True}]),
            ({'sourceDataImported': True}, True, [{'blocked': True}]), ({}, False, [{'blocked': True}]),
            ({}, True, [{'blocked': False}]), ({}, True, [])]:
            with self.assertRaises(ValueError):validate_isolation(source, {**fixture, **updates}, internal, probes)

    def test_wildcards_missing_resources_protected_namespaces_and_claims_refused(self):
        cm = resource('ConfigMap', 'manager', 'cm-1')
        validate_allowlist([cm], [action(cm)])
        for kind in ('Namespace', 'PersistentVolumeClaim', 'PersistentVolume', 'StorageClass'):
            obj = resource(kind, 'protected', 'protected-1')
            with self.assertRaisesRegex(ValueError, 'protected'):validate_allowlist([obj], [action(obj)])
        for updates in ({'name': '*'}, {'name': 'other'}, {'uid': 'different'}, {'resourceVersion': '2'}):
            with self.assertRaises(ValueError):validate_allowlist([cm], [{**action(cm), **updates}])

    def test_unknown_cascade_blocks_and_child_first_allows_explicit_scope(self):
        owner, child = resource('ConfigMap', 'owner', 'owner-1'), resource('ConfigMap', 'child', 'child-1', 'owner-1')
        with self.assertRaisesRegex(ValueError, 'owner-outside'):validate_allowlist([child], [action(child)])
        with self.assertRaisesRegex(ValueError, 'unallowlisted'):validate_allowlist([owner, child], [action(owner)])
        validate_allowlist([owner, child], [action(child), action(owner)])
        namespace = resource('Namespace', 'application', 'ns-1', 'owner-1')
        with self.assertRaises(ValueError):validate_allowlist([owner, namespace], [action(owner)])

    def test_named_finalizer_only_and_drift_stop(self):
        obj = resource('ConfigMap', 'held', 'held-1');obj['finalizers'] = ['qualification.hexalith.io/hold']
        intervention = {**action(obj), 'action': 'remove-named-finalizer', 'finalizer': 'qualification.hexalith.io/hold'}
        validate_allowlist([obj], [intervention])
        with self.assertRaises(ValueError):validate_allowlist([obj], [{**intervention, 'finalizer': '*'}])
        with self.assertRaises(ValueError):validate_allowlist([obj], [{**intervention, 'resourceVersion': '0'}])

    def test_unexpected_deletion_incomplete_deletion_or_uid_binding_change_stops(self):
        cm = resource('ConfigMap', 'manager', 'cm-1')
        pvc = resource('PersistentVolumeClaim', 'data', 'claim-1');pvc['binding'] = {'volumeName': 'pv-1'}
        before = [cm, pvc]
        assert_preserved(before, [pvc], {key(cm)})
        with self.assertRaisesRegex(ValueError, 'unexpected'):assert_preserved(before, [], {key(cm)})
        with self.assertRaisesRegex(ValueError, 'incomplete'):assert_preserved(before, before, {key(cm)})
        for updates in ({'uid': 'replacement-claim'}, {'binding': {'volumeName': 'other-pv'}}):
            with self.assertRaisesRegex(ValueError, 'identity-or-binding'):assert_preserved(before, [{**pvc, **updates}], {key(cm)})


class FixtureBoundaryTests(unittest.TestCase):
    def fixture(self, directory):
        args = SimpleNamespace(attempt_id='test-fixture', age='age', recipient='public-only',
                               node_image='sha256:' + 'a' * 64, source={'nativeEndpoint': 'https://192.168.1.30:6443'})
        records = {}
        attempt = SimpleNamespace(directory=Path(directory), encrypt=Mock(),
                                  record=lambda name, value: records.update({name: value}))
        return Fixture(args, attempt), records

    def startup_commands(self, fixture, timeout_kind=False, existing=None):
        counts = {}
        def command(argv, **kwargs):
            tag = argv[-1];counts[tag] = counts.get(tag, 0) + 1
            if argv[:3] == ['docker', 'container', 'inspect']:
                error = 'Error: No such object: ' + tag
            elif argv[:3] == ['docker', 'network', 'inspect']:
                error = 'Error response from daemon: network ' + tag + ' not found'
            elif argv[:3] == ['docker', 'image', 'inspect'] and tag in (fixture.base_tag, fixture.derived_tag) and counts[tag] == 1:
                error = 'Error response from daemon: No such image: ' + tag
            else:
                error = None
            if error:
                if tag == existing:return SimpleNamespace(returncode=0, stdout=b'[{}]', stderr=b'')
                return SimpleNamespace(returncode=1, stdout=b'[]', stderr=error.encode())
            if argv[:3] == ['docker', 'image', 'inspect']:
                output = json.dumps([{'Id': fixture.args.node_image if tag == fixture.args.node_image else 'sha256:' + 'b' * 64}]).encode()
            elif argv[:2] == ['docker', 'run']:
                output = b"docker_host_ip=$(ip -4 route show default | cut -d' ' -f3)"
            elif argv[:2] == ['docker', 'inspect']:
                output = json.dumps([{'Mounts': [{'Type': 'volume', 'Name': 'owned-kind-volume'}]}]).encode()
            elif argv[:3] == ['kind', 'create', 'cluster']:
                if timeout_kind:raise subprocess.TimeoutExpired(argv, 180)
                output = b'created'
            else:
                output = b''
            return SimpleNamespace(returncode=0, stdout=output, stderr=b'')
        return command

    def test_side_effects_owned_before_encryption_failure(self):
        for failed in ('tag-local-base', 'build-fenced-image', 'create-internal-network', 'kind-create'):
            with self.subTest(step=failed), tempfile.TemporaryDirectory() as temp:
                fixture, _ = self.fixture(temp)
                def encryption(name, *args):
                    if name.endswith(failed):raise ValueError('export-encryption-failed')
                fixture.attempt.encrypt.side_effect = encryption
                with patch('rehearse.subprocess.run', side_effect=self.startup_commands(fixture)) as command:
                    with self.assertRaisesRegex(ValueError, 'encryption'):fixture.start()
                self.assertIn(fixture.base_tag, fixture.image_tags)
                if failed != 'tag-local-base':self.assertIn(fixture.derived_tag, fixture.image_tags)
                self.assertEqual(fixture.network_created, failed in ('create-internal-network', 'kind-create'))
                self.assertEqual(fixture.created, failed == 'kind-create')
                if fixture.created:
                    self.assertEqual(fixture.fixture_volumes, ['owned-kind-volume'])
                    self.assertTrue(fixture.volume_capture_verified)
                    volume_probe = next(call for call in command.call_args_list if call.args[0][:2] == ['docker', 'inspect'])
                    self.assertEqual(volume_probe.kwargs['timeout'], 20)

    def test_exact_name_conflicts_stop_before_mutation(self):
        with tempfile.TemporaryDirectory() as temp:
            fixture, _ = self.fixture(temp)
            for existing in (fixture.node, fixture.network, fixture.base_tag, fixture.derived_tag):
                with self.subTest(name=existing), patch('rehearse.subprocess.run', side_effect=self.startup_commands(fixture, existing=existing)) as command:
                    with self.assertRaisesRegex(ValueError, 'name-exists'):fixture.start()
                self.assertFalse(fixture.created);self.assertFalse(fixture.network_created);self.assertEqual(fixture.image_tags, [])
                self.assertTrue(all('inspect' in call.args[0] for call in command.call_args_list))
                self.assertTrue(all(call.kwargs['timeout'] == 20 for call in command.call_args_list))

    def test_kind_timeout_captures_owned_volumes_before_failure_returns(self):
        with tempfile.TemporaryDirectory() as temp:
            fixture, _ = self.fixture(temp)
            with patch('rehearse.subprocess.run', side_effect=self.startup_commands(fixture, timeout_kind=True)):
                with self.assertRaises(subprocess.TimeoutExpired):fixture.start()
            self.assertTrue(fixture.created);self.assertTrue(fixture.network_created)
            self.assertEqual(fixture.fixture_volumes, ['owned-kind-volume'])
            self.assertTrue(fixture.volume_capture_verified)
            with patch('rehearse.subprocess.run', side_effect=subprocess.TimeoutExpired('inspect', 20)):
                self.assertFalse(fixture.capture_volumes())
            self.assertFalse(fixture.volume_capture_verified)
            self.assertEqual(fixture.fixture_volumes, ['owned-kind-volume'])
            for mounts in ([{}], [{'Type': 'volume'}], [None]):
                result = SimpleNamespace(returncode=0, stdout=json.dumps([{'Mounts': mounts}]).encode(), stderr=b'')
                with self.subTest(mounts=mounts), patch('rehearse.subprocess.run', return_value=result):
                    self.assertFalse(fixture.capture_volumes())
                self.assertFalse(fixture.volume_capture_verified)

    def test_source_inventory_hash_and_parse_share_one_capture(self):
        source = canonical({'sourceClusterUid': 'source-1', 'nativeEndpoint': 'https://192.168.1.30:6443'})
        path = Mock();path.read_bytes.side_effect = [source, b'changed contents']
        args = SimpleNamespace(source_inventory=path, project_root='repo', evidence_root='private', attempt_id='unit', operator='operator')
        attempt = Mock(directory=Path('/private/unit'))
        fixture = Mock(last_step='unit-start');fixture.start.side_effect = ValueError('fixture-refused');fixture.cleanup.return_value = True
        with patch('rehearse.Attempt', return_value=attempt), patch('rehearse.Fixture', return_value=fixture):
            _, state = rehearse(args)
        self.assertEqual(state, 'failed-closed')
        path.read_bytes.assert_called_once();path.read_text.assert_not_called()
        self.assertEqual(args.source_digest, digest(source))
        self.assertEqual(args.source['sourceClusterUid'], 'source-1')
        self.assertEqual(attempt.record.call_args_list[0].args[1]['sourceInventorySha256'], digest(source))
        attempt.finish.assert_called_once()

    def test_native_delete_request_contains_both_preconditions_on_fixture_only(self):
        action = {'uid': 'observed-uid', 'resourceVersion': '7', 'propagation': 'Foreground'}
        options = native_delete_options(action)
        self.assertEqual(options, {'apiVersion': 'v1', 'kind': 'DeleteOptions', 'propagationPolicy': 'Foreground',
                                  'preconditions': {'uid': 'observed-uid', 'resourceVersion': '7'}})
        for updates in ({'uid': None}, {'uid': ''}, {'resourceVersion': None}, {'resourceVersion': 7}, {'propagation': 'Background'}):
            with self.subTest(updates=updates), self.assertRaises(ValueError):native_delete_options({**action, **updates})
        with tempfile.TemporaryDirectory() as temp:
            fixture, _ = self.fixture(temp)
            with patch.object(fixture, 'run') as run:
                fixture.kube('native-delete', 'delete', '--raw', '/api/v1/namespaces/s426-management/configmaps/race', '-f', '-', obj=options)
            argv = run.call_args.args[1]
            self.assertEqual(argv[:4], ['docker', 'exec', '-i', fixture.node])
            self.assertEqual(argv[argv.index('--kubeconfig') + 1], '/etc/kubernetes/admin.conf')
            self.assertEqual(argv[argv.index('--context') + 1], fixture.cluster)
            self.assertEqual(json.loads(run.call_args.args[2]), options)

    def test_native_conflict_requires_specific_server_rejection(self):
        for stdout, stderr in [(b'', b'Error from server (Conflict): precondition UID does not match'),
                               (canonical({'kind': 'Status', 'reason': 'Conflict', 'code': 409}), b'')]:
            self.assertTrue(native_conflict(SimpleNamespace(returncode=1, stdout=stdout, stderr=stderr)))
        for code, stdout, stderr in [(0, b'', b'Error from server (Conflict): misleading'),
            (1, b'', b'Cannot connect to the Docker daemon'), (1, b'', b'Error from server (Forbidden): denied'),
            (1, b'', b'Error from server (NotFound): absent'), (1, canonical({'kind': 'Status', 'reason': 'Conflict', 'code': 500}), b''),
            (1, b'', b'Error from server (Conflict): mismatch\ntransport failed')]:
            with self.subTest(stderr=stderr):self.assertFalse(native_conflict(SimpleNamespace(returncode=code, stdout=stdout, stderr=stderr)))

    def race_objects(self):
        def cm(uid, rv, data):
            return {'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 's426-precondition-race',
                    'namespace': 's426-management', 'uid': uid, 'resourceVersion': rv}, 'data': {'synthetic': data}}
        return cm('old-uid', '1', 'first-incarnation'), cm('current-uid', '2', 'second-incarnation'), cm('current-uid', '3', 'mutated-after-snapshot')

    def test_real_stale_uid_and_version_requests_require_conflict_and_unchanged_content(self):
        with tempfile.TemporaryDirectory() as temp:
            fixture, records = self.fixture(temp)
            old, current, mutated = self.race_objects()
            def kube(name, *args, **kwargs):
                return SimpleNamespace(returncode=1 if name.startswith('native-negative-') else 0, stdout=b'',
                    stderr=b'Error from server (Conflict): precondition does not match' if name.startswith('native-negative-') else b'')
            with patch.object(fixture, 'get', side_effect=[old, current, copy.deepcopy(current), mutated, copy.deepcopy(mutated)]), \
                 patch.object(fixture, 'create') as create, patch.object(fixture, 'kube', side_effect=kube) as calls:
                fixture.check_native_delete_preconditions()
            self.assertEqual(create.call_count, 2)
            deletes = {call.args[0]: call for call in calls.call_args_list if call.args[0].startswith('native-negative-')}
            self.assertEqual(deletes['native-negative-stale-uid'].kwargs['obj']['preconditions'], {'uid': 'old-uid', 'resourceVersion': '2'})
            self.assertEqual(deletes['native-negative-stale-resource-version'].kwargs['obj']['preconditions'], {'uid': 'current-uid', 'resourceVersion': '2'})
            mutation = next(call for call in calls.call_args_list if call.args[0] == 'native-race-mutate')
            self.assertEqual(mutation.kwargs['obj']['metadata']['resourceVersion'], '2')
            self.assertEqual(mutation.kwargs['obj']['data'], {'synthetic': 'mutated-after-snapshot'})
            for receipt in records.values():
                self.assertTrue(receipt['nativeConflictConfirmed']);self.assertTrue(receipt['uidAndContentUnchanged'])
                self.assertEqual(receipt['nativeStatusCode'], 409);self.assertFalse(receipt['productionAccepted'])

    def test_native_negative_transport_failure_or_content_drift_stops(self):
        for error, drift in [(b'Cannot connect to the Docker daemon', False),
                             (b'Error from server (Conflict): precondition does not match', True)]:
            with self.subTest(error=error, drift=drift), tempfile.TemporaryDirectory() as temp:
                fixture, records = self.fixture(temp)
                old, current, _ = self.race_objects();after = copy.deepcopy(current)
                if drift:after['data']['synthetic'] = 'unexpected mutation'
                def kube(name, *args, **kwargs):
                    return SimpleNamespace(returncode=1 if name.startswith('native-negative-') else 0, stdout=b'', stderr=error)
                with patch.object(fixture, 'get', side_effect=[old, current, after]), patch.object(fixture, 'create'), \
                     patch.object(fixture, 'kube', side_effect=kube) as calls:
                    with self.assertRaisesRegex(ValueError, 'not-enforced'):fixture.check_native_delete_preconditions()
                receipt = records['native-precondition-stale-uid.json']
                self.assertEqual(receipt['nativeConflictConfirmed'], drift)
                self.assertEqual(receipt['uidAndContentUnchanged'], not drift)
                self.assertFalse(any(call.args[0] == 'native-race-cleanup' for call in calls.call_args_list))


if __name__ == '__main__':
    unittest.main()
