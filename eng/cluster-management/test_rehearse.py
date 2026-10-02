"""Source refusal, exact retirement scope, drift and preservation assertions."""
import copy
import itertools
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import Mock, patch
from evidence import canonical, digest
from qualify import project_resource
from rehearse import (Fixture, SYSTEM_WORKSPACE_FINALIZER, absence_state, api_path, assert_phase, assert_preserved,
                      dependency_plan, extension_phase_actions, key, native_conflict, native_delete_options, native_not_found,
                      phase_delta, probe_blocked, rehearse, retirement_scope, validate_allowlist, validate_isolation)


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


def ks(api, kind, name, uid, namespace=None, release=None, owner=None, finalizers=(), labels=None):
    return {'apiVersion': api, 'kind': kind, 'namespace': namespace, 'name': name, 'uid': uid, 'resourceVersion': '1',
            'owners': [{'uid': owner}] if owner else [], 'finalizers': list(finalizers), 'deletionTimestamp': None,
            'managementLabels': labels or {}, 'helmRelease': {'release-name': release} if release else {}}


def kubesphere_baseline():
    sys = 'kubesphere-system'
    return [
        ks('v1', 'Namespace', sys, 'ns', finalizers=[SYSTEM_WORKSPACE_FINALIZER], labels={'kubesphere.io/workspace': 'system-workspace'}),
        ks('kubesphere.io/v1alpha1', 'InstallPlan', 'ks-console-embed', 'plan', release='ks-core', finalizers=['kubesphere.io/installplan-protection']),
        ks('apps/v1', 'Deployment', 'ks-console-embed', 'embed-deploy', sys, 'ks-console-embed'),
        ks('v1', 'Service', 'ks-console-embed', 'embed-svc', sys, 'ks-console-embed'),
        ks('v1', 'Endpoints', 'ks-console-embed', 'embed-ep', sys),
        ks('v1', 'Secret', 'sh.helm.release.v1.ks-console-embed.v1', 'embed-record', sys),
        ks('iam.kubesphere.io/v1beta1', 'GlobalRoleBinding', 'admin', 'grb', release='ks-core', finalizers=['finalizers.kubesphere.io/globalrolebindings']),
        ks('rbac.authorization.k8s.io/v1', 'ClusterRoleBinding', 'admin-cluster-admin', 'crb', owner='grb'),
        ks('tenant.kubesphere.io/v1beta1', 'WorkspaceTemplate', 'system-workspace', 'wst', release='ks-core', finalizers=[SYSTEM_WORKSPACE_FINALIZER]),
        ks('iam.kubesphere.io/v1beta1', 'WorkspaceRole', 'system-workspace-admin', 'wsr', owner='wst', finalizers=['finalizers.kubesphere.io/workspaceroles']),
        ks('tenant.kubesphere.io/v1beta1', 'Workspace', 'system-workspace', 'ws', finalizers=[SYSTEM_WORKSPACE_FINALIZER]),
        ks('kubesphere.io/v1alpha1', 'Repository', 'extensions-museum', 'repo', release='ks-core', finalizers=['kubesphere.io/repository-protection']),
        ks('kubesphere.io/v1alpha1', 'Extension', 'catalog', 'ext', owner='repo', finalizers=['kubesphere.io/extension-protection']),
        ks('kubesphere.io/v1alpha1', 'ExtensionVersion', 'catalog-1', 'extv', owner='ext'),
        ks('admissionregistration.k8s.io/v1', 'ValidatingWebhookConfiguration', 'users.iam.kubesphere.io', 'hook', release='ks-core'),
        ks('apps/v1', 'Deployment', 'ks-controller-manager', 'deploy', sys, 'ks-core'),
        ks('apps/v1', 'ReplicaSet', 'ks-controller-manager-1', 'rs', sys, owner='deploy'),
        ks('v1', 'Pod', 'ks-controller-manager-1-a', 'pod', sys, owner='rs'),
        ks('v1', 'Service', 'ks-controller-manager', 'svc', sys, 'ks-core'),
        ks('v1', 'Endpoints', 'ks-controller-manager', 'ep', sys),
        ks('discovery.k8s.io/v1', 'EndpointSlice', 'ks-controller-manager-x', 'slice', sys, owner='svc'),
        ks('v1', 'ConfigMap', 'kubesphere-config', 'cm', sys, 'ks-core'),
        ks('v1', 'Secret', 'sh.helm.release.v1.ks-core.v1', 'record', sys),
        ks('v1', 'ConfigMap', 'unrelated', 'other', sys),
        ks('cluster.kubesphere.io/v1alpha1', 'Cluster', 'host', 'cluster', finalizers=['finalizer.cluster.kubesphere.io']),
        ks('iam.kubesphere.io/v1beta1', 'WorkspaceRoleBinding', 'system-workspace-admin', 'wrb', owner='wst',
           finalizers=['finalizers.kubesphere.io/workspacerolebindings'], labels={'iam.kubesphere.io/user-ref': 'admin'}),
        ks('rbac.authorization.k8s.io/v1', 'RoleBinding', 'kubesphere:iam:system-workspace:system-workspace-admin', 'wrb-projection',
           'kube-system', labels={'iam.kubesphere.io/workspacerolebinding-ref': 'system-workspace-admin'}),
        ks('iam.kubesphere.io/v1beta1', 'User', 'admin', 'user', release='ks-core', finalizers=['finalizers.kubesphere.io/users']),
        ks('iam.kubesphere.io/v1beta1', 'ClusterRoleBinding', 'admin-cluster-admin', 'iam-crb', labels={'iam.kubesphere.io/user-ref': 'admin'}),
        ks('rbac.authorization.k8s.io/v1', 'ClusterRoleBinding', 'kubesphere:iam:admin-cluster-admin', 'iam-crb-projection', owner='iam-crb'),
        ks('iam.kubesphere.io/v1beta1', 'User', 'operator', 'other-user', finalizers=['finalizers.kubesphere.io/users']),
        ks('rbac.authorization.k8s.io/v1', 'ClusterRoleBinding', 'operator-cluster-admin', 'operator-native-grant',
           labels={'iam.kubesphere.io/user-ref': 'operator'}),
        ks('v1', 'ServiceAccount', 'helm-executor.ks-console-embed', 'executor', 'kubesphere-system',
           labels={'kubesphere.io/extension-ref': 'ks-console-embed'}),
        ks('v1', 'ServiceAccount', 'helm-executor.other', 'other-executor', 'kubesphere-system',
           labels={'kubesphere.io/extension-ref': 'other'}),
        ks('v1', 'Secret', 'kubeconfig-admin', 'user-kubeconfig', 'kubesphere-system', labels={'kubesphere.io/username': 'admin'}),
        ks('v1', 'Secret', 'kubeconfig-operator', 'operator-kubeconfig', 'kubesphere-system', labels={'kubesphere.io/username': 'operator'}),
        ks('kubesphere.io/v1alpha1', 'ServiceAccount', 'ks-console', 'ks-sa', 'kubesphere-system', 'ks-core',
           finalizers=['finalizers.kubesphere.io/serviceaccount']),
        {**ks('v1', 'Secret', 'ks-console-x1', 'ks-sa-token', 'kubesphere-system'), 'serviceAccountReference': 'ks-console'},
        {**ks('v1', 'Secret', 'other-token', 'other-token', 'other-namespace'), 'serviceAccountReference': 'ks-console'},
        {**ks('admissionregistration.k8s.io/v1', 'ValidatingWebhookConfiguration', 'validator.license.kubesphere.io', 'license-hook'),
         'webhooks': [{'service': {'namespace': 'kubesphere-system', 'name': 'ks-controller-manager'}}]},
        {**ks('admissionregistration.k8s.io/v1', 'MutatingWebhookConfiguration', 'unrelated-hook', 'other-hook'),
         'webhooks': [{'service': {'namespace': 'other', 'name': 'ks-controller-manager'}}]},
        {**ks('apiregistration.k8s.io/v1', 'APIService', 'v1.apps', 'local-api'), 'service': {'namespace': None, 'name': None}},
    ]


class DependencyRetirementTests(unittest.TestCase):
    def test_dependency_plan_is_child_first_exact_and_partitions_scope(self):
        baseline = kubesphere_baseline()
        scope = retirement_scope(baseline)
        # Unrelated users' grants, other extensions' identities and the host Cluster stay outside the exact scope.
        self.assertEqual(scope, {v['uid'] for v in baseline} - {'ns', 'other', 'cluster', 'other-user', 'operator-native-grant',
                                                                'other-executor', 'operator-kubeconfig', 'other-token',
                                                                'other-hook', 'local-api'})
        plan = dependency_plan(baseline, scope)
        phases = {p['phase']: p for p in plan}
        self.assertEqual([p['phase'] for p in plan], ['installed-extension', 'global-role-bindings', 'workspace-role-bindings',
                         'workspace-roles', 'kubesphere-cluster-role-bindings', 'users', 'kubesphere-service-accounts',
                         'extension-repository', 'admission',
                         'controllers-and-services', 'remaining-release-objects', 'release-records', 'system-workspace-finalizers'])
        self.assertEqual(set(phases['workspace-role-bindings']['expected']), {'wrb', 'wrb-projection'})
        self.assertEqual(phases['workspace-role-bindings']['roots'], ['wrb'])
        self.assertEqual(set(phases['kubesphere-cluster-role-bindings']['expected']), {'iam-crb', 'iam-crb-projection'})
        self.assertEqual(set(phases['users']['expected']), {'user', 'user-kubeconfig'})
        self.assertEqual(set(phases['kubesphere-service-accounts']['expected']), {'ks-sa', 'ks-sa-token'})
        self.assertIn('executor', phases['remaining-release-objects']['expected'])
        self.assertEqual(set(phases['installed-extension']['expected']), {'plan', 'embed-deploy', 'embed-svc', 'embed-ep', 'embed-record'})
        self.assertEqual(phases['installed-extension']['roots'], ['plan'])
        self.assertEqual(set(phases['global-role-bindings']['expected']), {'grb', 'crb'})
        self.assertEqual(phases['workspace-roles']['roots'], ['wsr'])
        self.assertEqual(set(phases['extension-repository']['expected']), {'repo', 'ext', 'extv'})
        self.assertEqual(phases['extension-repository']['roots'], ['repo'])
        self.assertEqual(set(phases['controllers-and-services']['expected']), {'deploy', 'rs', 'pod', 'svc', 'ep', 'slice'})
        self.assertEqual(set(phases['controllers-and-services']['roots']), {'deploy', 'svc'})
        self.assertFalse(phases['controllers-and-services']['controllersRunning'])
        self.assertTrue(phases['admission']['controllersRunning'])
        self.assertEqual(set(phases['admission']['roots']), {'hook', 'license-hook'})
        named = phases['system-workspace-finalizers']
        self.assertEqual((named['mode'], set(named['roots'])), ('named-finalizer', {'wst', 'ws'}))
        self.assertEqual(sorted(u for p in plan for u in p['expected']), sorted(scope))

    def test_dependency_plan_refuses_unreviewed_finalizers_protected_cascade_and_unknown_owners(self):
        for mutate, reason in [
                (lambda b: b[[v['uid'] for v in b].index('cm')].update(finalizers=['kubesphere.io/cleanup']), 'finalizer-after-controller'),
                (lambda b: b[[v['uid'] for v in b].index('ws')].update(finalizers=[SYSTEM_WORKSPACE_FINALIZER, 'other/hold']), 'system-workspace'),
                (lambda b: b.append(ks('v1', 'PersistentVolumeClaim', 'data', 'claim', 'kubesphere-system', owner='cm')), 'protected'),
                (lambda b: b[[v['uid'] for v in b].index('cm')].update(owners=[{'uid': 'unseen'}]), 'owner-outside')]:
            with self.subTest(reason=reason):
                baseline = kubesphere_baseline();mutate(baseline)
                with self.assertRaisesRegex(ValueError, reason):dependency_plan(baseline, retirement_scope(baseline))

    def test_phase_assertion_detects_recreation_and_permits_only_attempt_transients(self):
        cm = ks('v1', 'ConfigMap', 'retired', 'cm-1', 'kubesphere-system')
        job = ks('batch/v1', 'Job', 'installer', 'job-1', 'kubesphere-system')
        namespace = ks('v1', 'Namespace', 'kubesphere-system', 'ns-1', labels={'kubesphere.io/workspace': 'system-workspace'})
        transient = ks('batch/v1', 'Job', 'uninstaller', 'job-2', 'kubesphere-system')
        baseline, before = {'cm-1', 'job-1', 'ns-1'}, [cm, job, namespace, transient]
        assert_phase(baseline, before, [job, namespace], {'cm-1'})
        delta = phase_delta(baseline, before, [job, namespace], {'cm-1'})
        self.assertEqual(delta['transientRemovals'], ['batch/v1/Job/kubesphere-system/uninstaller'])
        self.assertEqual((delta['unexpectedRemovals'], delta['retainedExpected'], delta['recreated']), ([], [], []))
        recreated = {**cm, 'uid': 'cm-2'}
        self.assertEqual(phase_delta(baseline, before, [job, namespace, recreated], {'cm-1'})['recreated'],
                         ['v1/ConfigMap/kubesphere-system/retired'])
        for after, reason in [([job, namespace, recreated], 'recreated'), ([namespace], 'unexpected'),
                              ([job, {**namespace, 'managementLabels': {}}], 'identity-or-binding'),
                              ([job, {**namespace, 'uid': 'ns-2'}], 'identity-or-binding')]:
            with self.subTest(reason=reason), self.assertRaisesRegex(ValueError, reason):
                assert_phase(baseline, before, after, {'cm-1'})

    def test_native_not_found_and_discovered_paths_are_exact(self):
        self.assertTrue(native_not_found(SimpleNamespace(returncode=1, stderr=b'Error from server (NotFound): users "x" not found\n')))
        for code, stderr in [(0, b''), (1, b'Error from server (Forbidden): denied'), (1, b'Cannot connect to the Docker daemon'),
                             (1, b'Error from server (NotFound): x\ntransport failed')]:
            self.assertFalse(native_not_found(SimpleNamespace(returncode=code, stderr=stderr)))
        resources = {('iam.kubesphere.io/v1beta1', 'ClusterRole'): ('clusterroles', False), ('v1', 'Secret'): ('secrets', True)}
        role = ks('iam.kubesphere.io/v1beta1', 'ClusterRole', 'kubesphere:iam:admin', 'r')
        self.assertEqual(api_path(resources, role), '/apis/iam.kubesphere.io/v1beta1/clusterroles/kubesphere:iam:admin')
        secret = ks('v1', 'Secret', 'tls', 's', 'kubesphere-system')
        self.assertEqual(api_path(resources, secret), '/api/v1/namespaces/kubesphere-system/secrets/tls')
        for obj in ({**secret, 'namespace': None}, {**role, 'namespace': 'other'}, {**secret, 'name': '../../namespaces/x'},
                    {**secret, 'name': 'a/b'}, {**secret, 'namespace': 'a?b'}):
            with self.subTest(obj=obj), self.assertRaises(ValueError):api_path(resources, obj)
        with self.assertRaises(KeyError):api_path(resources, ks('v1', 'Namespace', 'x', 'n'))


class NativeRequestTests(unittest.TestCase):
    def fixture(self):
        records = {}
        attempt = SimpleNamespace(directory=Path('/nonexistent'), encrypt=Mock(), record=lambda n, v: records.update({n: v}))
        return Fixture(SimpleNamespace(attempt_id='native-request-test'), attempt), records

    def obj(self, uid='u-1', version='5', finalizers=(), deleting=False, owners=()):
        meta = {'name': 'system-workspace', 'uid': uid, 'resourceVersion': version, 'finalizers': list(finalizers),
                'ownerReferences': list(owners)}
        if deleting:
            meta['deletionTimestamp'] = '2026-10-02T09:00:00Z'
        return {'apiVersion': 'tenant.kubesphere.io/v1beta1', 'kind': 'WorkspaceTemplate', 'metadata': meta, 'spec': {}}

    def test_native_retire_revalidates_uid_retries_conflict_and_refuses_other_errors(self):
        conflict = SimpleNamespace(returncode=1, stdout=b'', stderr=b'Error from server (Conflict): precondition failed')
        fixture, _ = self.fixture()
        with patch.object(fixture, 'native_read', side_effect=[self.obj(version='5'), self.obj(version='6')]), \
             patch.object(fixture, 'kube', side_effect=[conflict, SimpleNamespace(returncode=0, stdout=b'', stderr=b'')]) as kube:
            outcome = fixture.native_retire('users', '/apis/x', 'u-1')
        self.assertEqual((outcome['outcome'], outcome['resourceVersion'], outcome['conflictRetries']), ('native-delete-accepted', '6', 1))
        self.assertEqual([c.kwargs['obj']['preconditions'] for c in kube.call_args_list],
                         [{'uid': 'u-1', 'resourceVersion': '5'}, {'uid': 'u-1', 'resourceVersion': '6'}])
        self.assertTrue(all(c.args[1:3] == ('delete', '--raw') for c in kube.call_args_list))
        with patch.object(fixture, 'native_read', return_value=self.obj(uid='replacement')), patch.object(fixture, 'kube') as kube:
            with self.assertRaisesRegex(ValueError, 'uid-drift'):fixture.native_retire('users', '/apis/x', 'u-1')
        kube.assert_not_called()
        with patch.object(fixture, 'native_read', return_value=self.obj()), patch.object(fixture, 'kube',
                return_value=SimpleNamespace(returncode=1, stdout=b'', stderr=b'Error from server (Forbidden): denied')):
            with self.assertRaisesRegex(ValueError, 'rejected'):fixture.native_retire('users', '/apis/x', 'u-1')
        with patch.object(fixture, 'native_read', return_value=self.obj()), patch.object(fixture, 'kube', return_value=conflict):
            with self.assertRaisesRegex(ValueError, 'retries-exhausted'):fixture.native_retire('users', '/apis/x', 'u-1')
        with patch.object(fixture, 'native_read', return_value=None), patch.object(fixture, 'kube') as kube:
            self.assertEqual(fixture.native_retire('users', '/apis/x', 'u-1')['outcome'], 'absent-before-request')
        kube.assert_not_called()

    def test_named_finalizer_removes_only_one_named_finalizer_after_deletion_request(self):
        fixture, _ = self.fixture()
        current = self.obj(finalizers=[SYSTEM_WORKSPACE_FINALIZER, 'foregroundDeletion'], deleting=True)
        with patch.object(fixture, 'native_read', return_value=current), \
             patch.object(fixture, 'kube', return_value=SimpleNamespace(returncode=0, stdout=b'', stderr=b'')) as kube:
            outcome = fixture.remove_named_finalizer('/apis/x', 'u-1', SYSTEM_WORKSPACE_FINALIZER)
        body = kube.call_args.kwargs['obj']
        self.assertEqual(kube.call_args.args[1:3], ('replace', '--raw'))
        self.assertEqual(body['metadata']['finalizers'], ['foregroundDeletion'])
        self.assertEqual((body['metadata']['uid'], body['metadata']['resourceVersion']), ('u-1', '5'))
        self.assertEqual(outcome['otherFinalizersRetained'], ['foregroundDeletion'])
        for candidate, reason in [(self.obj(finalizers=[SYSTEM_WORKSPACE_FINALIZER]), 'precondition'),
                                  (self.obj(finalizers=['other/hold'], deleting=True), 'precondition'),
                                  (self.obj(uid='replacement', finalizers=[SYSTEM_WORKSPACE_FINALIZER], deleting=True), 'precondition'),
                                  (None, 'precondition'),
                                  (self.obj(finalizers=[SYSTEM_WORKSPACE_FINALIZER], deleting=True,
                                            owners=[{'apiVersion': 'v1', 'kind': 'X', 'name': 'x', 'uid': 'unseen'}]), 'owner-outside')]:
            with self.subTest(reason=reason), patch.object(fixture, 'native_read', return_value=candidate), \
                 patch.object(fixture, 'kube') as kube:
                with self.assertRaisesRegex(ValueError, reason):fixture.remove_named_finalizer('/apis/x', 'u-1', SYSTEM_WORKSPACE_FINALIZER)
            kube.assert_not_called()
        with patch.object(fixture, 'native_read', return_value=current), patch.object(fixture, 'kube',
                return_value=SimpleNamespace(returncode=1, stdout=b'', stderr=b'Error from server (Forbidden): denied')):
            with self.assertRaisesRegex(ValueError, 'rejected'):fixture.remove_named_finalizer('/apis/x', 'u-1', SYSTEM_WORKSPACE_FINALIZER)

    def test_phase_records_delta_before_stopping_and_refuses_post_controller_runtime(self):
        baseline = kubesphere_baseline()
        by_uid = {v['uid']: v for v in baseline}
        plan = {p['phase']: p for p in dependency_plan(baseline, retirement_scope(baseline))}
        resources = {(v['apiVersion'], v['kind']): ('things', bool(v['namespace'])) for v in baseline}
        uids = {v['uid'] for v in baseline}
        phase = plan['global-role-bindings']
        unexpected = [v for v in baseline if v['uid'] not in ('grb', 'crb', 'other')]
        fixture, records = self.fixture()
        with patch.object(fixture, 'settled_inventory', side_effect=[(baseline, True), (unexpected, True)]), \
             patch.object(fixture, 'native_retire', return_value={'outcome': 'native-delete-accepted'}) as retire, \
             patch.object(fixture, 'wait_absent', return_value=True):
            with self.assertRaisesRegex(ValueError, 'unexpected'):fixture.retire_phase(phase, uids, by_uid, resources)
        retire.assert_called_once()
        record = records['retirement-phase-02-global-role-bindings.json']
        self.assertEqual(record['delta']['unexpectedRemovals'], ['v1/ConfigMap/kubesphere-system/unrelated'])
        fixture, records = self.fixture()
        with patch.object(fixture, 'settled_inventory', return_value=(baseline, True)), patch.object(fixture, 'native_retire') as retire:
            with self.assertRaisesRegex(ValueError, 'manager-runtime-present'):
                fixture.retire_phase(plan['remaining-release-objects'], uids, by_uid, resources)
        retire.assert_not_called()
        # The controller-removal phase starts while the manager still runs; it must not be refused for that.
        self.assertEqual([p['phase'] for p in plan.values() if p['managerAbsentRequired']],
                         ['remaining-release-objects', 'release-records', 'system-workspace-finalizers'])
        removal = plan['controllers-and-services']
        after = [v for v in baseline if v['uid'] not in removal['expected']]
        fixture, records = self.fixture()
        with patch.object(fixture, 'settled_inventory', side_effect=[(baseline, True), (after, True)]), \
             patch.object(fixture, 'native_retire', return_value={'outcome': 'native-delete-accepted'}) as retire, \
             patch.object(fixture, 'wait_absent', return_value=True):
            fixture.retire_phase(removal, uids, by_uid, resources)
        self.assertEqual(retire.call_count, len(removal['roots']))
        fixture, records = self.fixture()
        incomplete = [v for v in baseline if v['uid'] != 'grb']
        with patch.object(fixture, 'settled_inventory', side_effect=[(baseline, True), (incomplete, True)]), \
             patch.object(fixture, 'native_retire', return_value={'outcome': 'native-delete-accepted'}), \
             patch.object(fixture, 'wait_absent', return_value=False):
            with self.assertRaisesRegex(ValueError, 'incomplete'):fixture.retire_phase(phase, uids, by_uid, resources)
        self.assertEqual(records['retirement-phase-02-global-role-bindings.json']['delta']['retainedExpected'],
                         ['rbac.authorization.k8s.io/v1/ClusterRoleBinding/-/admin-cluster-admin'])


class WorkspaceProbeTests(unittest.TestCase):
    def test_probe_refuses_before_member_namespace_when_controller_never_materializes_workspace(self):
        attempt = SimpleNamespace(directory=Path('/nonexistent'), encrypt=Mock(), record=Mock())
        fixture = Fixture(SimpleNamespace(attempt_id='probe-test'), attempt)
        with patch.object(fixture, 'create') as create, patch.object(fixture, 'native_read', return_value=None), \
             patch.object(fixture, 'native_retire') as retire, patch('rehearse.time.sleep'), \
             patch('rehearse.time.monotonic', side_effect=itertools.count(0, 10)):
            with self.assertRaisesRegex(ValueError, 'workspace-probe-setup-incomplete'):fixture.probe_workspace_propagation()
        self.assertEqual([c.args[0]['kind'] for c in create.call_args_list], ['WorkspaceTemplate'])
        retire.assert_not_called();attempt.record.assert_not_called()

    def test_probe_records_unbinding_without_treating_it_as_a_cascade(self):
        attempt = SimpleNamespace(directory=Path('/nonexistent'), encrypt=Mock(), record=Mock())
        fixture = Fixture(SimpleNamespace(attempt_id='probe-test'), attempt)
        meta = lambda name, uid, **extra: {'name': name, 'uid': uid, 'resourceVersion': '1', **extra}
        template = {'apiVersion': 'tenant.kubesphere.io/v1beta1', 'kind': 'WorkspaceTemplate', 'metadata': meta('s426-probe', 't')}
        workspace = {'apiVersion': 'tenant.kubesphere.io/v1beta1', 'kind': 'Workspace', 'metadata': meta('s426-probe', 'w')}
        bound = {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': meta('s426-probe-member', 'n', finalizers=[SYSTEM_WORKSPACE_FINALIZER],
                 labels={'kubesphere.io/workspace': 's426-probe'})}
        unbound = {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': meta('s426-probe-member', 'n')}
        reads = iter([workspace, bound, template, None, None, unbound])
        with patch.object(fixture, 'create'), patch.object(fixture, 'native_read', side_effect=lambda path: next(reads)), \
             patch.object(fixture, 'native_retire', return_value={'outcome': 'native-delete-accepted'}), patch('rehearse.time.sleep'):
            record = fixture.probe_workspace_propagation()
        self.assertEqual(record['memberNamespaceOutcome'], 'retained-label-or-finalizer-changed')
        self.assertFalse(record['cascadeHazard']);self.assertFalse(record['procedurePart'])
        self.assertEqual(record['memberAfter']['finalizers'], [])


class PostRetirementTests(unittest.TestCase):
    def run_checks(self, final, crds=(), pods=(), namespace_labels=None, namespace_gone=True):
        records = {}
        attempt = SimpleNamespace(directory=Path('/nonexistent'), encrypt=Mock(), record=lambda n, v: records.update({n: v}))
        fixture = Fixture(SimpleNamespace(attempt_id='post-retirement-test'), attempt)
        created = {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': 's426-post-retirement', 'uid': 'n-1',
                   'resourceVersion': '1', 'labels': namespace_labels or {}}}
        written = {'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 's426-post-retirement', 'namespace': 's426-workload',
                   'uid': 'c-1', 'resourceVersion': '1'}}
        reads = {'/api/v1/namespaces/s426-workload/configmaps/s426-post-retirement': [written],
                 '/api/v1/namespaces/s426-post-retirement': [created] + [None if namespace_gone else created] * 30}
        with patch.object(fixture, 'create'), patch.object(fixture, 'get', return_value={'items': list(pods)}), \
             patch.object(fixture, 'native_read', side_effect=lambda path: reads[path].pop(0)), \
             patch.object(fixture, 'native_retire', return_value={'outcome': 'native-delete-accepted'}) as retire, \
             patch('rehearse.time.sleep'), patch('rehearse.time.monotonic', side_effect=itertools.count(0, 10)):
            try:
                fixture.post_retirement_checks(final, list(crds))
                error = None
            except ValueError as raised:
                error = str(raised)
        return error, records['post-retirement-native-health.json'], retire

    def test_native_health_requires_no_stale_backends_runtime_or_namespace_mutation(self):
        error, record, retire = self.run_checks([ks('v1', 'Service', 'kube-dns', 's', 'kube-system')])
        self.assertIsNone(error)
        self.assertEqual(retire.call_args_list[0].args[1:], ('/api/v1/namespaces/s426-workload/configmaps/s426-post-retirement', 'c-1'))
        self.assertTrue(record['newNamespaceDeletionCompleted'])
        hook = {**ks('admissionregistration.k8s.io/v1', 'ValidatingWebhookConfiguration', 'users', 'h'),
                'webhooks': [{'service': {'namespace': 'kubesphere-system', 'name': 'ks-controller-manager'}}]}
        crd = {**ks('apiextensions.k8s.io/v1', 'CustomResourceDefinition', 'x.kubesphere.io', 'crd'),
               'customResource': {'conversionService': {'namespace': 'kubesphere-system', 'name': 'ks-controller-manager'}}}
        pod = {'apiVersion': 'v1', 'kind': 'Pod', 'metadata': {'name': 'ks-apiserver-x', 'namespace': 'kubesphere-system', 'uid': 'p'},
               'status': {'phase': 'Running'}}
        done = {**pod, 'metadata': {**pod['metadata'], 'name': 'installer-x'}, 'status': {'phase': 'Succeeded'}}
        for kwargs, field in [({'final': [hook]}, 'staleAdmissionOrApiServiceReferences'),
                              ({'final': [], 'crds': [crd]}, 'staleAdmissionOrApiServiceReferences'),
                              ({'final': [ks('apps/v1', 'Deployment', 'ks-apiserver', 'd', 'kubesphere-system')]}, 'managerRuntimeRemaining'),
                              ({'final': [], 'pods': [pod]}, 'managerRuntimeRemaining'),
                              ({'final': [], 'namespace_labels': {'kubesphere.io/workspace': 'system-workspace'}}, 'newNamespaceManagementLabels'),
                              ({'final': [], 'namespace_gone': False}, 'newNamespaceDeletionCompleted')]:
            with self.subTest(field=field, kwargs=list(kwargs)):
                error, record, _ = self.run_checks(**kwargs)
                self.assertEqual(error, 'post-retirement-native-health-failed')
                self.assertTrue(record[field] in (False,) or record[field])
        error, record, _ = self.run_checks([], pods=[done])
        self.assertIsNone(error)
        self.assertEqual(record['completedManagerPodResidue'], ['v1/Pod/kubesphere-system/installer-x'])


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

    def test_malformed_source_identity_refused_before_fixture_allocation(self):
        for source in (None, [], 'private-source-value', {},
                       {'sourceClusterUid': True, 'nativeEndpoint': 'https://192.168.1.30:6443'},
                       {'sourceClusterUid': 'source-1', 'nativeEndpoint': {'private': 'source-value'}}):
            with self.subTest(source=source):
                path = Mock();path.read_bytes.return_value = canonical(source)
                args = SimpleNamespace(source_inventory=path)
                with patch('rehearse.Attempt') as attempt, patch('rehearse.Fixture') as fixture:
                    with self.assertRaisesRegex(ValueError, 'source-identity-missing'):
                        rehearse(args)
                attempt.assert_not_called();fixture.assert_not_called()

    def test_malformed_successful_docker_image_schema_finalizes_closed(self):
        for output in ([], [None], [{}]):
            with self.subTest(output=output), tempfile.TemporaryDirectory() as temp:
                fixture, _ = self.fixture(temp)
                path = Mock();path.read_bytes.return_value = canonical({
                    'sourceClusterUid': 'source-1', 'nativeEndpoint': 'https://192.168.1.30:6443'})
                fixture.args.source_inventory = path
                fixture.args.project_root = Path(temp) / 'repo'
                fixture.args.evidence_root = Path(temp) / 'private'
                fixture.args.operator = 'operator'
                fixture.attempt.record = Mock()
                fixture.attempt.finish = Mock()
                original = self.startup_commands(fixture)
                def command(argv, **kwargs):
                    if argv == ['docker', 'image', 'inspect', fixture.args.node_image]:
                        return SimpleNamespace(returncode=0, stdout=canonical(output), stderr=b'')
                    return original(argv, **kwargs)
                with patch('rehearse.Attempt', return_value=fixture.attempt), \
                     patch('rehearse.Fixture', return_value=fixture), \
                     patch('rehearse.subprocess.run', side_effect=command) as commands, \
                     patch.object(fixture, 'cleanup', return_value=True) as cleanup:
                    _, state = rehearse(fixture.args)
                records = {call.args[0]: call.args[1] for call in fixture.attempt.record.call_args_list}
                self.assertEqual(state, 'failed-closed')
                self.assertEqual(records['failure.json']['failureStep'], 'inspect-image')
                self.assertEqual(records['failure.json']['reasonCode'], 'command-or-custody-error')
                self.assertFalse(records['summary.json']['qualificationAccepted'])
                self.assertTrue(all('inspect' in call.args[0] for call in commands.call_args_list))
                cleanup.assert_called_once();fixture.attempt.finish.assert_called_once()

    def test_malformed_native_diagnostics_cannot_interrupt_failure_receipt_or_cleanup(self):
        with tempfile.TemporaryDirectory() as temp:
            fixture, _ = self.fixture(temp)
            fixture.created = True
            fixture.args.ks_chart = Path(temp) / 'public-chart.tgz'
            fixture.args.source_inventory = Mock()
            fixture.args.source_inventory.read_bytes.return_value = canonical({
                'sourceClusterUid': 'source-1', 'nativeEndpoint': 'https://192.168.1.30:6443'})
            fixture.args.project_root = Path(temp) / 'repo'
            fixture.args.evidence_root = Path(temp) / 'private'
            fixture.args.operator = 'operator'
            fixture.attempt.record = Mock();fixture.attempt.finish = Mock()
            fixture.last_step = 'native-fixture-schema'
            with patch('rehearse.Attempt', return_value=fixture.attempt), \
                 patch('rehearse.Fixture', return_value=fixture), patch.object(fixture, 'start'), \
                 patch.object(fixture, 'execute', side_effect=KeyError('private-native-value')), \
                 patch.object(fixture, 'get', return_value={'items': [None]}), \
                 patch.object(fixture, 'cleanup', return_value=True) as cleanup:
                _, state = rehearse(fixture.args)
            records = {call.args[0]: call.args[1] for call in fixture.attempt.record.call_args_list}
            self.assertEqual(state, 'failed-closed')
            self.assertEqual(records['failure.json']['failureStep'], 'native-fixture-schema')
            self.assertNotIn('private-native-value', json.dumps(records))
            self.assertEqual(records['summary.json']['state'], 'failed-closed')
            cleanup.assert_called_once();fixture.attempt.finish.assert_called_once()

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
