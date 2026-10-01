"""Source refusal, exact retirement scope, drift and preservation assertions."""
import copy
import unittest
from types import SimpleNamespace
from rehearse import absence_state, assert_preserved, key, probe_blocked, validate_allowlist, validate_isolation


def resource(kind, name, uid, owner=None):
    return {'apiVersion': 'v1', 'kind': kind, 'namespace': 'fixture' if kind == 'ConfigMap' else None,
            'name': name, 'uid': uid, 'resourceVersion': '1', 'owners': [{'uid': owner}] if owner else [], 'finalizers': []}


def action(obj):
    return {**obj, 'action': 'delete', 'propagation': 'Foreground'}


class RehearsalTests(unittest.TestCase):
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


if __name__ == '__main__':
    unittest.main()
