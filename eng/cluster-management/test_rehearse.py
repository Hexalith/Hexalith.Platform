"""Source refusal, exact retirement scope, drift and preservation assertions."""
import copy
import io
import itertools
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import Mock, patch
from evidence import canonical, digest, file_digest
from qualify import project_resource
import rehearse as rehearse_module
from rehearse import (Fixture, SYNTHETIC_HOLD, SYSTEM_WORKSPACE_FINALIZER, absence_state, api_path, assert_phase, assert_preserved,
                      content_review_digest, dependency_plan, extension_phase_actions, key, native_conflict, native_delete_options, native_not_found,
                      category_count_transition, cluster_grant_annotation_transition, lease_renewal_transition, phase_delta, probe_blocked, rehearse, retirement_scope, role_annotation_transitions, synthetic_hold_blocked, tool_identities,
                      validate_allowlist, validate_isolation)


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

    def test_surviving_workload_desired_images_replicas_and_references_cannot_drift(self):
        pod = {'serviceAccountName': 'application-reader',
               'containers': [{'name': 'app', 'image': 'registry.example/app:v1',
                   'envFrom': [{'secretRef': {'name': 'app-secret'}}, {'configMapRef': {'name': 'app-config'}}]}],
               'initContainers': [{'name': 'init', 'image': 'registry.example/init:v1'}],
               'volumes': [{'name': 'data', 'persistentVolumeClaim': {'claimName': 'app-data'}}]}
        for kind in ('Deployment', 'StatefulSet', 'DaemonSet', 'Job', 'CronJob', 'Pod'):
            spec = copy.deepcopy(pod) if kind == 'Pod' else {'template': {'spec': copy.deepcopy(pod)}}
            if kind == 'CronJob':spec = {'jobTemplate': {'spec': spec}}
            if kind in ('Deployment', 'StatefulSet'):spec['replicas'] = 2
            raw = {'apiVersion': 'v1' if kind == 'Pod' else 'batch/v1' if kind in ('Job', 'CronJob') else 'apps/v1',
                   'kind': kind, 'metadata': {'name': 'application', 'namespace': 'workload', 'uid': 'workload-1',
                                            'resourceVersion': '1'}, 'spec': spec}
            changes = ('image', 'init-image', 'service-account', 'claim', 'secret', 'configmap')
            if 'replicas' in spec:changes += ('replicas',)
            for change in changes:
                current = copy.deepcopy(raw)
                current['metadata']['resourceVersion'] = '2'
                current_pod = (current['spec'] if kind == 'Pod' else
                    current['spec']['jobTemplate']['spec']['template']['spec'] if kind == 'CronJob' else
                    current['spec']['template']['spec'])
                if change == 'image':current_pod['containers'][0]['image'] = 'registry.example/app:v2'
                elif change == 'init-image':current_pod['initContainers'][0]['image'] = 'registry.example/init:v2'
                elif change == 'service-account':current_pod['serviceAccountName'] = 'privileged-writer'
                elif change == 'claim':current_pod['volumes'][0]['persistentVolumeClaim']['claimName'] = 'other-data'
                elif change == 'secret':current_pod['containers'][0]['envFrom'][0]['secretRef']['name'] = 'other-secret'
                elif change == 'configmap':current_pod['containers'][0]['envFrom'][1]['configMapRef']['name'] = 'other-config'
                else:current['spec']['replicas'] = 0
                with self.subTest(kind=kind, change=change), self.assertRaisesRegex(ValueError, 'identity-or-binding'):
                    assert_preserved([project_resource(raw)], [project_resource(current)], set())
            # Status image IDs and server versions are observations, rather than projected desired content.
            status_changed = copy.deepcopy(raw)
            status_changed['metadata']['resourceVersion'] = '3'
            status_changed['status'] = {'containerStatuses': [{'imageID': 'registry.example/app@sha256:' + 'a' * 64}]}
            assert_preserved([project_resource(raw)], [project_resource(status_changed)], set())
            assert_preserved([project_resource(raw)], [], {key(project_resource(raw))})

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
        # A repeated action and an inventory name containing '*' each reach the duplicate/wildcard guard, not the missing-key one.
        starred = resource('ConfigMap', 'manager-*', 'cm-2')
        for inventory, actions in (([cm], [action(cm), action(cm)]), ([starred], [action(starred)])):
            with self.subTest(actions=[a['name'] for a in actions]), \
                 self.assertRaisesRegex(ValueError, 'allowlist-missing-duplicate-or-wildcard'):
                validate_allowlist(inventory, actions)

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
        # The catalog Extension 'catalog' has no InstallPlan, so its executor identity is not an installed extension's.
        ks('v1', 'ServiceAccount', 'helm-executor.catalog', 'catalog-executor', 'kubesphere-system',
           labels={'kubesphere.io/extension-ref': 'catalog'}),
        ks('v1', 'Secret', 'kubeconfig-admin', 'user-kubeconfig', 'kubesphere-system', labels={'kubesphere.io/username': 'admin'}),
        ks('v1', 'Secret', 'kubeconfig-operator', 'operator-kubeconfig', 'kubesphere-system', labels={'kubesphere.io/username': 'operator'}),
        ks('kubesphere.io/v1alpha1', 'ServiceAccount', 'ks-console', 'ks-sa', 'kubesphere-system', 'ks-core',
           finalizers=['finalizers.kubesphere.io/serviceaccount']),
        {**ks('v1', 'Secret', 'ks-console-x1', 'ks-sa-token', 'kubesphere-system'), 'serviceAccountReference': 'ks-console'},
        {**ks('v1', 'Secret', 'other-token', 'other-token', 'other-namespace'), 'serviceAccountReference': 'ks-console'},
        {**ks('admissionregistration.k8s.io/v1', 'ValidatingWebhookConfiguration', 'validator.license.kubesphere.io', 'license-hook'),
         'webhooks': [{'failurePolicy': 'Ignore', 'service': {'namespace': 'kubesphere-system', 'name': 'ks-controller-manager'}}]},
        {**ks('admissionregistration.k8s.io/v1', 'MutatingWebhookConfiguration', 'unrelated-hook', 'other-hook'),
         'webhooks': [{'service': {'namespace': 'other', 'name': 'ks-controller-manager'}}]},
        {**ks('apiregistration.k8s.io/v1', 'APIService', 'v1.apps', 'local-api'), 'service': {'namespace': None, 'name': None}},
        # A native webhook backed by a Service that stays is never pulled into the retirement scope.
        ks('v1', 'Namespace', 'cert-manager', 'cert-manager-ns'),
        ks('v1', 'Service', 'cert-manager-webhook', 'cert-manager-svc', 'cert-manager'),
        {**ks('admissionregistration.k8s.io/v1', 'ValidatingWebhookConfiguration', 'cert-manager-webhook', 'cert-manager-hook'),
         'webhooks': [{'failurePolicy': 'Fail', 'service': {'namespace': 'cert-manager', 'name': 'cert-manager-webhook'}}]},
    ]


class DependencyRetirementTests(unittest.TestCase):
    def test_dependency_plan_is_child_first_exact_and_partitions_scope(self):
        baseline = kubesphere_baseline()
        scope = retirement_scope(baseline)
        # Unrelated users' grants, other extensions' identities and the host Cluster stay outside the exact scope.
        self.assertEqual(scope, {v['uid'] for v in baseline} - {'ns', 'other', 'cluster', 'other-user', 'operator-native-grant',
                                                                'other-executor', 'catalog-executor', 'operator-kubeconfig', 'other-token',
                                                                'other-hook', 'local-api', 'cert-manager-ns', 'cert-manager-svc',
                                                                'cert-manager-hook'})
        self.assertNotIn('catalog-executor', scope);self.assertNotIn('cert-manager-hook', scope)
        plan = dependency_plan(baseline, scope)
        phases = {p['phase']: p for p in plan}
        self.assertEqual([p['phase'] for p in plan], ['installed-extension', 'global-role-bindings', 'workspace-role-bindings',
                         'workspace-roles', 'kubesphere-cluster-role-bindings', 'users', 'kubesphere-service-accounts',
                         'extension-repository', 'admission', 'controllers-and-services', 'reconciled-admission',
                         'remaining-release-objects', 'release-records', 'system-workspace-finalizers', 'namespace-finalizers'])
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
        self.assertEqual(phases['admission']['roots'], ['hook'])
        self.assertEqual((phases['reconciled-admission']['roots'], phases['reconciled-admission']['managerAbsentRequired']),
                         (['license-hook'], True))
        named = phases['system-workspace-finalizers']
        self.assertEqual((named['mode'], set(named['roots'])), ('named-finalizer', {'wst', 'ws'}))
        self.assertEqual(sorted(u for p in plan for u in p['expected']), sorted(scope))

    def test_dependency_plan_refuses_unreviewed_finalizers_protected_cascade_and_unknown_owners(self):
        for mutate, reason in [
                (lambda b: b[[v['uid'] for v in b].index('cm')].update(finalizers=['kubesphere.io/cleanup']), 'finalizer-after-controller'),
                (lambda b: b[[v['uid'] for v in b].index('ws')].update(finalizers=[SYSTEM_WORKSPACE_FINALIZER, 'other/hold']), 'system-workspace'),
                (lambda b: b.append(ks('v1', 'PersistentVolumeClaim', 'data', 'claim', 'kubesphere-system', owner='cm')), 'protected'),
                (lambda b: b[[v['uid'] for v in b].index('cm')].update(owners=[{'uid': 'unseen'}]), 'owner-outside'),
                (lambda b: b[[v['uid'] for v in b].index('license-hook')]['webhooks'][0].update(failurePolicy='Fail'), 'blocking-registration'),
                (lambda b: b.append({**ks('apiregistration.k8s.io/v1', 'APIService', 'v1.iam.kubesphere.io', 'reconciled-api'),
                                     'service': {'namespace': 'kubesphere-system', 'name': 'ks-controller-manager'}}), 'blocking-registration'),
                # An operator-created console Ingress or HTTPRoute outside the release still routes to a retired Service.
                (lambda b: b.append({**ks('networking.k8s.io/v1', 'Ingress', 'another-console-route', 'console-route', 'kubesphere-system'),
                                     'backends': [{'kind': 'Service', 'name': 'ks-controller-manager', 'namespace': 'kubesphere-system'}]}),
                 'route-consumer-of-retired-service'),
                (lambda b: b.append({**ks('gateway.networking.k8s.io/v1', 'HTTPRoute', 'console', 'console-http', 'kubesphere-system'),
                                     'backends': [{'kind': None, 'name': 'ks-console-embed', 'namespace': None}]}),
                 'route-consumer-of-retired-service')]:
            with self.subTest(reason=reason):
                baseline = kubesphere_baseline();mutate(baseline)
                with self.assertRaisesRegex(ValueError, reason):dependency_plan(baseline, retirement_scope(baseline))
        # Routes to retained Services, non-Service backends and routes inside the retirement scope do not block planning.
        baseline = kubesphere_baseline()
        baseline += [{**ks('networking.k8s.io/v1', 'Ingress', 'web', 'web-route', 'app'),
                      'backends': [{'kind': 'Service', 'name': 'web', 'namespace': 'app'},
                                   {'apiGroup': 'x', 'kind': 'Bucket', 'name': 'ks-controller-manager'}]},
                     {**ks('networking.k8s.io/v1', 'Ingress', 'ks-console', 'chart-route', 'kubesphere-system', 'ks-core'),
                      'backends': [{'kind': 'Service', 'name': 'ks-controller-manager', 'namespace': 'kubesphere-system'}]}]
        scope = retirement_scope(baseline)
        self.assertIn('chart-route', scope);self.assertNotIn('web-route', scope)
        self.assertEqual(sorted(u for p in dependency_plan(baseline, scope) for u in p['expected']), sorted(scope))

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

    def test_native_read_maps_success_not_found_and_other_errors(self):
        fixture, _ = self.fixture()
        for result, expected in [(SimpleNamespace(returncode=0, stdout=b'{"metadata": {"uid": "u-1"}}', stderr=b''),
                                  {'metadata': {'uid': 'u-1'}}),
                                 (SimpleNamespace(returncode=1, stdout=b'', stderr=b'Error from server (NotFound): x not found\n'), None)]:
            with patch.object(fixture, 'kube', return_value=result) as kube:
                self.assertEqual(fixture.native_read('/api/v1/namespaces/x'), expected)
            self.assertEqual(kube.call_args.args, ('native-read', 'get', '--raw', '/api/v1/namespaces/x'))
        for stderr in (b'Error from server (Forbidden): denied', b'Cannot connect to the Docker daemon'):
            with self.subTest(stderr=stderr), patch.object(fixture, 'kube',
                    return_value=SimpleNamespace(returncode=1, stdout=b'', stderr=stderr)):
                with self.assertRaisesRegex(ValueError, 'native-read-failed'):fixture.native_read('/api/v1/namespaces/x')

    def test_settle_requires_two_equal_polls_and_wait_absent_counts_terminating_objects(self):
        fixture, _ = self.fixture()
        for states, settled, sleeps in [([{'a'}, {'a'}], True, 1), ([{'a'}, {'b'}, {'b'}], True, 2),
                                        ([{str(i)} for i in range(10)], False, 9)]:
            with self.subTest(states=states), patch.object(fixture, 'uid_states', side_effect=map(frozenset, states)), \
                 patch.object(fixture, 'kubesphere_inventory', return_value=['inventory']), patch('rehearse.time.sleep') as sleep:
                self.assertEqual(fixture.settled_inventory(), (['inventory'], settled))
            self.assertEqual(sleep.call_count, sleeps)
        terminating = frozenset({'u-1 2026-10-03T09:00:00Z', 'u-2'})
        with patch.object(fixture, 'uid_states', return_value=terminating), patch('rehearse.time.sleep'), \
             patch('rehearse.time.monotonic', side_effect=itertools.count(0, 100)):
            self.assertFalse(fixture.wait_absent({'u-1'}, 300))
        with patch.object(fixture, 'uid_states', side_effect=[terminating, frozenset({'u-2'})]), patch('rehearse.time.sleep'), \
             patch('rehearse.time.monotonic', side_effect=itertools.count(0, 100)):
            self.assertTrue(fixture.wait_absent({'u-1'}, 300))

    def test_phase_that_does_not_settle_stops_before_requests_or_after_its_record(self):
        baseline = kubesphere_baseline()
        by_uid = {v['uid']: v for v in baseline}
        phase = {p['phase']: p for p in dependency_plan(baseline, retirement_scope(baseline))}['global-role-bindings']
        resources = {(v['apiVersion'], v['kind']): ('things', bool(v['namespace'])) for v in baseline}
        after = [v for v in baseline if v['uid'] not in phase['expected']]
        fixture, records = self.fixture()
        with patch.object(fixture, 'settled_inventory', return_value=(baseline, False)), patch.object(fixture, 'native_retire') as retire:
            with self.assertRaisesRegex(ValueError, 'did-not-settle-before-global-role-bindings'):
                fixture.retire_phase(phase, set(by_uid), by_uid, resources)
        retire.assert_not_called();self.assertEqual(records, {})
        fixture, records = self.fixture()
        with patch.object(fixture, 'settled_inventory', side_effect=[(baseline, True), (after, False)]), \
             patch.object(fixture, 'native_retire', return_value={'outcome': 'native-delete-accepted'}), \
             patch.object(fixture, 'wait_absent', return_value=True):
            with self.assertRaisesRegex(ValueError, 'did-not-settle-after-global-role-bindings'):
                fixture.retire_phase(phase, set(by_uid), by_uid, resources)
        self.assertFalse(records['retirement-phase-02-global-role-bindings.json']['settled'])

    def test_native_retire_revalidates_uid_retries_conflict_and_refuses_other_errors(self):
        conflict = SimpleNamespace(returncode=1, stdout=b'', stderr=b'Error from server (Conflict): precondition failed')
        fixture, _ = self.fixture()
        reviewed = project_resource(self.obj(version='5'))
        fixture.reviewed_content_digests[reviewed['uid']] = content_review_digest(self.obj(version='5'))
        with patch.object(fixture, 'native_read', side_effect=[self.obj(version='5'), self.obj(version='6')]), \
             patch.object(fixture, 'kube', side_effect=[conflict, SimpleNamespace(returncode=0, stdout=b'', stderr=b'')]) as kube:
            outcome = fixture.native_retire('users', '/apis/x', reviewed)
        self.assertEqual((outcome['outcome'], outcome['resourceVersion'], outcome['conflictRetries']), ('native-delete-accepted', '6', 1))
        self.assertEqual((outcome['reviewedResourceVersion'], outcome['resourceVersionChangedSinceReview']), ('5', True))
        self.assertEqual([c.kwargs['obj']['preconditions'] for c in kube.call_args_list],
                         [{'uid': 'u-1', 'resourceVersion': '5'}, {'uid': 'u-1', 'resourceVersion': '6'}])
        self.assertTrue(all(c.args[1:3] == ('delete', '--raw') for c in kube.call_args_list))
        with patch.object(fixture, 'native_read', return_value=self.obj(uid='replacement')), patch.object(fixture, 'kube') as kube:
            with self.assertRaisesRegex(ValueError, 'uid-drift'):fixture.native_retire('users', '/apis/x', reviewed)
        kube.assert_not_called()
        with patch.object(fixture, 'native_read', return_value=self.obj()), patch.object(fixture, 'kube',
                return_value=SimpleNamespace(returncode=1, stdout=b'', stderr=b'Error from server (Forbidden): denied')):
            with self.assertRaisesRegex(ValueError, 'rejected'):fixture.native_retire('users', '/apis/x', reviewed)
        with patch.object(fixture, 'native_read', return_value=self.obj()), patch.object(fixture, 'kube', return_value=conflict):
            with self.assertRaisesRegex(ValueError, 'retries-exhausted'):fixture.native_retire('users', '/apis/x', reviewed)
        with patch.object(fixture, 'native_read', return_value=None), patch.object(fixture, 'kube') as kube:
            self.assertEqual(fixture.native_retire('users', '/apis/x', reviewed)['outcome'], 'absent-before-request')
        kube.assert_not_called()

    def test_native_retire_stops_when_reviewed_fields_drift_after_review(self):
        fixture, _ = self.fixture()
        reviewed = project_resource(self.obj(version='5'))
        fixture.reviewed_content_digests[reviewed['uid']] = content_review_digest(self.obj(version='5'))
        ok = SimpleNamespace(returncode=0, stdout=b'', stderr=b'')
        with patch.object(fixture, 'native_read', return_value=self.obj(version='5')), patch.object(fixture, 'kube', return_value=ok):
            outcome = fixture.native_retire('users', '/apis/x', reviewed)
        self.assertEqual((outcome['resourceVersion'], outcome['resourceVersionChangedSinceReview']), ('5', False))
        labelled = self.obj(version='7');labelled['metadata']['labels'] = {'kubesphere.io/workspace': 'other'}
        for drifted in (self.obj(version='7', finalizers=['kubesphere.io/new-hold']), self.obj(version='7', deleting=True),
                        self.obj(version='7', owners=[{'apiVersion': 'v1', 'kind': 'X', 'name': 'x', 'uid': 'new-owner'}]), labelled):
            # A later reviewed-field change stops before any DELETE, even though the UID is unchanged.
            with self.subTest(meta=drifted['metadata']), patch.object(fixture, 'native_read', return_value=drifted), \
                 patch.object(fixture, 'kube') as kube:
                with self.assertRaisesRegex(ValueError, 'reviewed-entry-drift'):fixture.native_retire('users', '/apis/x', reviewed)
            kube.assert_not_called()
        # A conflict retry re-reads and compares with the reviewed entry again, not with the previous read.
        conflict = SimpleNamespace(returncode=1, stdout=b'', stderr=b'Error from server (Conflict): precondition failed')
        with patch.object(fixture, 'native_read', side_effect=[self.obj(version='6'), self.obj(version='7', finalizers=['x/hold'])]), \
             patch.object(fixture, 'kube', return_value=conflict) as kube:
            with self.assertRaisesRegex(ValueError, 'reviewed-entry-drift'):fixture.native_retire('users', '/apis/x', reviewed)
        kube.assert_called_once()

    def test_private_full_content_stops_spec_secret_data_and_rbac_rules_drift_before_delete(self):
        examples = [
            {'apiVersion': 'apps/v1', 'kind': 'Deployment', 'spec': {'replicas': 1}},
            {'apiVersion': 'v1', 'kind': 'Secret', 'data': {'synthetic': 'c3ludGhldGlj'}},
            {'apiVersion': 'rbac.authorization.k8s.io/v1', 'kind': 'ClusterRole',
             'rules': [{'apiGroups': [''], 'resources': ['pods'], 'verbs': ['get']}]},
        ]
        for example in examples:
            with self.subTest(kind=example['kind']):
                raw = {**example, 'metadata': {'name': 'synthetic-review', 'uid': 'u-1', 'resourceVersion': '1'}}
                reviewed = project_resource(raw)
                fixture, _ = self.fixture()
                fixture.reviewed_content_digests['u-1'] = content_review_digest(raw)
                changed = copy.deepcopy(raw);changed['metadata'].update(resourceVersion='2', generation=2)
                if 'spec' in changed:changed['spec']['strategy'] = {'type': 'Recreate'}
                elif 'data' in changed:changed['data']['synthetic'] = 'ZHJpZnQ='
                else:changed['rules'][0]['verbs'] = ['*']
                # These fields are absent from the public projection; the private baseline must still refuse them.
                self.assertEqual(rehearse_module.review_digest(project_resource(changed)), rehearse_module.review_digest(reviewed))
                with patch.object(fixture, 'native_read', return_value=changed), patch.object(fixture, 'kube') as kube:
                    with self.assertRaisesRegex(ValueError, 'reviewed-full-content-drift'):
                        fixture.native_retire('synthetic', '/apis/synthetic', reviewed)
                kube.assert_not_called()

    def test_private_content_allows_only_status_bookkeeping_churn_and_rechecks_conflict(self):
        fixture, _ = self.fixture();raw = self.obj(version='5')
        fixture.reviewed_content_digests['u-1'] = content_review_digest(raw)
        updated = copy.deepcopy(raw);updated['metadata'].update(resourceVersion='6', managedFields=[{'manager': 'status'}])
        updated['status'] = {'ready': True}
        with patch.object(fixture, 'native_read', return_value=updated), patch.object(fixture, 'kube',
                return_value=SimpleNamespace(returncode=0, stdout=b'', stderr=b'')):
            result = fixture.native_retire('synthetic', '/apis/synthetic', project_resource(raw))
        self.assertTrue(result['privateFullContentVerified']);self.assertFalse(result['strictReviewedResourceVersion'])
        drifted = copy.deepcopy(updated);drifted['spec'] = {'synthetic': 'changed'}
        conflict = SimpleNamespace(returncode=1, stdout=b'', stderr=b'Error from server (Conflict): precondition failed')
        with patch.object(fixture, 'native_read', side_effect=[updated, drifted]), patch.object(fixture, 'kube', return_value=conflict) as kube:
            with self.assertRaisesRegex(ValueError, 'reviewed-full-content-drift'):
                fixture.native_retire('synthetic', '/apis/synthetic', project_resource(raw))
        kube.assert_called_once()

    def test_server_generation_churn_keeps_full_desired_content_enforcement(self):
        raw = self.obj(version='5')
        raw.update(apiVersion='kubesphere.io/v1alpha1', kind='Extension', spec={'synthetic': 'keep'}, status={'state': 'installed'})
        raw['metadata'].update(generation=11, annotations={'synthetic': 'keep'})
        current = copy.deepcopy(raw)
        current['metadata'].update(resourceVersion='6', generation=13)
        current['status'] = {'state': 'uninstalled'}
        fixture, _ = self.fixture()
        fixture.reviewed_content_digests['u-1'] = content_review_digest(raw)
        with patch.object(fixture, 'native_read', return_value=current), patch.object(fixture, 'kube',
                return_value=SimpleNamespace(returncode=0, stdout=b'', stderr=b'')):
            result = fixture.native_retire('synthetic', '/apis/synthetic', project_resource(raw))
        self.assertTrue(result['privateFullContentVerified'])
        for change in ('spec', 'annotations'):
            changed = copy.deepcopy(current)
            if change == 'spec':
                changed['spec']['synthetic'] = 'drift'
            else:
                changed['metadata']['annotations']['synthetic'] = 'drift'
            with self.subTest(change=change), patch.object(fixture, 'native_read', return_value=changed), patch.object(fixture, 'kube') as kube:
                with self.assertRaisesRegex(ValueError, 'reviewed-full-content-drift'):
                    fixture.native_retire('synthetic', '/apis/synthetic', project_resource(raw))
            kube.assert_not_called()

    def test_changed_resource_version_without_private_content_baseline_stops(self):
        fixture, _ = self.fixture()
        with patch.object(fixture, 'native_read', return_value=self.obj(version='6')), patch.object(fixture, 'kube') as kube:
            with self.assertRaisesRegex(ValueError, 'reviewed-resource-version-drift-without-content-baseline'):
                fixture.native_retire('synthetic', '/apis/synthetic', project_resource(self.obj(version='5')))
        kube.assert_not_called()

    def leader_lease(self):
        return {'apiVersion': 'coordination.k8s.io/v1', 'kind': 'Lease',
            'metadata': {'namespace': 'kubesphere-system', 'name': 'ks-controller-manager-leader-election',
                         'uid': 'lease-1', 'resourceVersion': '1', 'annotations': {'synthetic': 'keep'}},
            'spec': {'renewTime': '2026-10-04T10:00:00Z', 'acquireTime': '2026-10-04T09:59:00Z',
                     'holderIdentity': 'synthetic-manager', 'leaseDurationSeconds': 15, 'leaseTransitions': 0}}

    def test_final_lease_renewal_requires_exact_scope_absence_monotonic_stable_full_content(self):
        before = self.leader_lease(); current = copy.deepcopy(before)
        current['metadata']['resourceVersion'] = '2'; current['spec']['renewTime'] = '2026-10-04T10:01:00Z'
        expected = lease_renewal_transition(before, current, current, {'lease-1'}, 'console-route', True)
        self.assertEqual(content_review_digest(expected), content_review_digest(current))
        self.assertIsNone(lease_renewal_transition(before, before, before, {'lease-1'}, 'console-route', True))
        for scope, phase, absent, reason in [(set(), 'console-route', True, 'not-exact-retirement-identity'),
                ({'lease-1'}, 'admission', True, 'wrong-phase'), ({'lease-1'}, 'console-route', False, 'manager-still-present')]:
            with self.subTest(reason=reason), self.assertRaisesRegex(ValueError, reason):
                lease_renewal_transition(before, current, current, scope, phase, absent)
        for field, value in [('uid', 'other'), ('name', 'other'), ('namespace', 'other')]:
            changed = copy.deepcopy(current); changed['metadata'][field] = value
            with self.subTest(identity=field), self.assertRaisesRegex(ValueError, 'identity-drift'):
                lease_renewal_transition(before, changed, changed, {'lease-1'}, 'console-route', True)
        for field in ('holderIdentity', 'acquireTime', 'leaseDurationSeconds', 'leaseTransitions', 'other'):
            changed = copy.deepcopy(current); changed['spec'][field] = 'drift'
            with self.subTest(spec=field), self.assertRaisesRegex(ValueError, 'other-desired-content-drift'):
                lease_renewal_transition(before, changed, changed, {'lease-1'}, 'console-route', True)
        changed = copy.deepcopy(current); changed['metadata']['annotations']['synthetic'] = 'drift'
        with self.assertRaisesRegex(ValueError, 'other-desired-content-drift'):
            lease_renewal_transition(before, changed, changed, {'lease-1'}, 'console-route', True)
        for value, reason in [('2026-10-04T09:58:00Z', 'backwards-time'), ('2026-10-04', 'invalid-time'),
                              ('2026-99-04T10:00:00Z', 'invalid-time'), (None, 'invalid-time')]:
            changed = copy.deepcopy(current); changed['spec']['renewTime'] = value
            with self.subTest(time=value), self.assertRaisesRegex(ValueError, reason):
                lease_renewal_transition(before, changed, changed, {'lease-1'}, 'console-route', True)
        changed = copy.deepcopy(current); changed['spec']['renewTime'] = '2026-10-04T10:02:00Z'
        with self.assertRaisesRegex(ValueError, 'not-stable'):
            lease_renewal_transition(before, current, changed, {'lease-1'}, 'console-route', True)

    def test_console_lease_checkpoint_requires_completed_controller_phase_and_absent_workloads(self):
        before = self.leader_lease(); current = copy.deepcopy(before)
        current['spec']['renewTime'] = '2026-10-04T10:01:00Z'
        projected = project_resource(before)
        phase = {'phase': 'console-route', 'expected': ['lease-1'], 'roots': ['lease-1'],
                 'managerAbsentRequired': True, 'mode': 'native-delete'}
        resources = {('coordination.k8s.io/v1', 'Lease'): ('leases', True)}
        fixture, records = self.fixture(); fixture.args.age = 'age'; fixture.args.recipient = 'public'
        fixture.reviewed_raw_by_uid = {'lease-1': before}; fixture.retirement_uids = {'lease-1'}
        with patch.object(fixture, 'settled_inventory', return_value=([projected], True)), patch.object(fixture, 'native_read') as read:
            with self.assertRaisesRegex(ValueError, 'before-controller-retirement'):
                fixture.retire_phase(phase, {'lease-1'}, {'lease-1': projected}, resources)
        read.assert_not_called()
        fixture.completed_retirement_phases.add('controllers-and-services')
        manager = project_resource({'apiVersion': 'apps/v1', 'kind': 'Deployment', 'metadata': {
            'namespace': 'kubesphere-system', 'name': 'ks-controller-manager', 'uid': 'manager-1', 'resourceVersion': '1'}})
        with patch.object(fixture, 'settled_inventory', return_value=([projected, manager], True)), \
             patch.object(fixture, 'native_read', return_value=current), patch.object(fixture, 'native_retire') as retire, \
             patch('rehearse.time.sleep'):
            with self.assertRaisesRegex(ValueError, 'manager-still-present'):
                fixture.retire_phase(phase, {'lease-1'}, {'lease-1': projected}, resources)
        retire.assert_not_called()
        with patch.object(fixture, 'settled_inventory', return_value=([projected], True)), \
             patch.object(fixture, 'native_read', return_value=current) as read, \
             patch.object(fixture, 'native_retire', side_effect=ValueError('delete-sentinel')), patch('rehearse.time.sleep'):
            with self.assertRaisesRegex(ValueError, 'delete-sentinel'):
                fixture.retire_phase(phase, {'lease-1'}, {'lease-1': projected}, resources)
        self.assertEqual(read.call_count, 2)
        self.assertEqual(fixture.reviewed_content_digests['lease-1'], content_review_digest(current))
        self.assertTrue(records['leader-lease-renewal-transition.json']['twoNativeReadsStable'])

    def category_count_inputs(self):
        category = {'apiVersion': 'kubesphere.io/v1alpha1', 'kind': 'Category',
            'metadata': {'name': 'synthetic', 'uid': 'category-1', 'resourceVersion': '1',
                         'annotations': {'kubesphere.io/count': '2', 'synthetic': 'keep'}}, 'spec': {'synthetic': 'keep'}}
        extensions = [{'apiVersion': 'kubesphere.io/v1alpha1', 'kind': 'Extension',
            'metadata': {'name': 'extension-' + str(i), 'uid': 'extension-' + str(i),
                         'labels': {'kubesphere.io/category': 'synthetic'}}} for i in range(2)]
        current = copy.deepcopy(category); current['metadata']['annotations']['kubesphere.io/count'] = '0'
        current['metadata']['resourceVersion'] = '2'
        return category, current, extensions

    def test_category_count_requires_exact_retired_membership_absence_stability_and_full_content(self):
        before, current, extensions = self.category_count_inputs()
        scope = {'category-1', 'extension-0', 'extension-1'}
        phases = {'catalog-extension', 'controllers-and-services'}
        def verify(a=before, b=current, c=current, members=extensions, uids=scope,
                   phase='remaining-release-objects', completed=phases, manager_absent=True, extensions_absent=True):
            return category_count_transition(a, b, c, members, uids, phase, completed, manager_absent, extensions_absent)
        self.assertEqual(content_review_digest(verify()), content_review_digest(current))
        empty = copy.deepcopy(before); empty['metadata']['annotations']['kubesphere.io/count'] = '0'
        self.assertIsNone(verify(a=empty, b=empty, c=empty, members=[]))
        for arguments, reason in [({'phase': 'console-route'}, 'wrong-phase'),
                ({'completed': {'catalog-extension'}}, 'before-catalog-controller-retirement'),
                ({'manager_absent': False}, 'manager-or-extensions-present'),
                ({'extensions_absent': False}, 'manager-or-extensions-present'),
                ({'uids': {'extension-0', 'extension-1'}}, 'not-exact-retirement-identity'),
                ({'uids': {'category-1', 'extension-0'}}, 'membership-not-retired'),
                ({'members': [extensions[0], extensions[0]]}, 'membership-not-retired'),
                ({'members': extensions[:1]}, 'reviewed-membership-mismatch')]:
            with self.subTest(arguments=arguments), self.assertRaisesRegex(ValueError, reason): verify(**arguments)
        for field, value in [('uid', 'other'), ('name', 'other'), ('namespace', 'other')]:
            changed = copy.deepcopy(current); changed['metadata'][field] = value
            with self.subTest(identity=field), self.assertRaisesRegex(ValueError, 'identity-drift'): verify(b=changed, c=changed)
        changed = copy.deepcopy(before); changed['apiVersion'] = 'application.kubesphere.io/v2'
        with self.assertRaisesRegex(ValueError, 'not-exact-retirement-identity'): verify(a=changed)
        for value in ('01', '-1', 2, None):
            changed = copy.deepcopy(before); changed['metadata']['annotations']['kubesphere.io/count'] = value
            with self.subTest(reviewed=value), self.assertRaisesRegex(ValueError, 'reviewed-membership-mismatch'): verify(a=changed)
        changed = copy.deepcopy(current); changed['metadata']['annotations']['kubesphere.io/count'] = '1'
        with self.assertRaisesRegex(ValueError, 'current-not-zero'): verify(b=changed, c=changed)
        with self.assertRaisesRegex(ValueError, 'not-stable'): verify(c=changed)
        for field in ('spec', 'data', 'rules', 'annotation'):
            changed = copy.deepcopy(current)
            if field == 'annotation': changed['metadata']['annotations']['synthetic'] = 'drift'
            else: changed[field] = {'synthetic': 'drift'}
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, 'other-desired-content-drift'): verify(b=changed, c=changed)

    def test_remaining_release_category_checkpoint_updates_only_verified_private_baseline(self):
        before, current, extensions = self.category_count_inputs(); projected = project_resource(before)
        fixture, records = self.fixture(); fixture.args.age = 'age'; fixture.args.recipient = 'public'
        fixture.reviewed_raw_by_uid = {v['metadata']['uid']: v for v in [before, *extensions]}
        fixture.retirement_uids = set(fixture.reviewed_raw_by_uid)
        fixture.completed_retirement_phases.update({'catalog-extension', 'controllers-and-services'})
        phase = {'phase': 'remaining-release-objects', 'expected': ['category-1'], 'roots': ['category-1'],
                 'managerAbsentRequired': True, 'mode': 'native-delete'}
        resources = {('kubesphere.io/v1alpha1', 'Category'): ('categories', False)}
        with patch.object(fixture, 'settled_inventory', return_value=([projected], True)), \
             patch.object(fixture, 'native_read', return_value=current) as read, \
             patch.object(fixture, 'native_retire', side_effect=ValueError('delete-sentinel')), patch('rehearse.time.sleep'):
            with self.assertRaisesRegex(ValueError, 'delete-sentinel'):
                fixture.retire_phase(phase, fixture.retirement_uids, {'category-1': projected}, resources)
        self.assertEqual(read.call_count, 2)
        self.assertEqual(fixture.reviewed_content_digests['category-1'], content_review_digest(current))
        self.assertTrue(records['category-count-transitions.json']['extensionsAbsent'])
        fixture.completed_retirement_phases.clear()
        with patch.object(fixture, 'settled_inventory', return_value=([projected], True)), \
             patch.object(fixture, 'native_read', return_value=current), patch.object(fixture, 'native_retire') as retire, \
             patch('rehearse.time.sleep'):
            with self.assertRaisesRegex(ValueError, 'before-catalog-controller-retirement'):
                fixture.retire_phase(phase, fixture.retirement_uids, {'category-1': projected}, resources)
        retire.assert_not_called()

    def test_expected_role_annotation_transition_is_bound_to_deleted_binding_and_exact_content(self):
        user = self.obj();user.update(apiVersion='iam.kubesphere.io/v1beta1', kind='User')
        user['metadata'].update(name='admin', annotations={'iam.kubesphere.io/globalrole': 'platform-admin', 'other': 'keep'})
        binding = {'apiVersion': 'iam.kubesphere.io/v1beta1', 'kind': 'GlobalRoleBinding',
                   'metadata': {'uid': 'binding-1'}, 'roleRef': {'apiGroup': 'iam.kubesphere.io', 'kind': 'GlobalRole', 'name': 'platform-admin'},
                   'subjects': [{'apiGroup': 'iam.kubesphere.io', 'kind': 'User', 'name': 'admin'}]}
        changed = copy.deepcopy(user);changed['metadata']['resourceVersion'] = '6'
        changed['metadata']['annotations']['iam.kubesphere.io/globalrole'] = ''
        reviewed = {'u-1': user, 'binding-1': binding}
        transitions = role_annotation_transitions(reviewed, {'u-1': changed}, {'binding-1'}, {'u-1', 'binding-1'})
        self.assertEqual([(t['uid'], t['bindingUid']) for t in transitions], [('u-1', 'binding-1')])
        self.assertEqual(content_review_digest(transitions[0]['expected']), content_review_digest(changed))
        self.assertEqual(role_annotation_transitions(reviewed, {'u-1': changed}, set(), {'u-1'}), [])
        self.assertEqual(role_annotation_transitions(reviewed, {'u-1': changed}, {'binding-1'}, set()), [])
        wrong_binding = copy.deepcopy(binding);wrong_binding['roleRef']['name'] = 'other-role'
        self.assertEqual(role_annotation_transitions({'u-1': user, 'binding-1': wrong_binding}, {'u-1': changed}, {'binding-1'}, {'u-1'}), [])
        for field, value in [('apiGroup', 'unrelated.example'), ('kind', 'Role')]:
            wrong_binding = copy.deepcopy(binding);wrong_binding['roleRef'][field] = value
            self.assertEqual(role_annotation_transitions({'u-1': user, 'binding-1': wrong_binding}, {'u-1': changed}, {'binding-1'}, {'u-1'}), [])
        missing = copy.deepcopy(changed);missing['metadata']['annotations'].pop('iam.kubesphere.io/globalrole')
        with self.assertRaisesRegex(ValueError, 'unexpected-controller-role-annotation-transition'):
            role_annotation_transitions(reviewed, {'u-1': missing}, {'binding-1'}, {'u-1'})
        for field, value in [('spec', {'changed': True}), ('data', {'synthetic': 'changed'}), ('rules', [{'verbs': ['*']}])]:
            drifted = copy.deepcopy(changed);drifted[field] = value
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, 'unexpected-controller-role-annotation-transition'):
                role_annotation_transitions(reviewed, {'u-1': drifted}, {'binding-1'}, {'u-1'})
        unrelated = copy.deepcopy(changed);unrelated['metadata']['annotations']['other'] = 'changed'
        with self.assertRaisesRegex(ValueError, 'unexpected-controller-role-annotation-transition'):
            role_annotation_transitions(reviewed, {'u-1': unrelated}, {'binding-1'}, {'u-1'})

    def cluster_grant_inputs(self):
        user = self.obj();user.update(apiVersion='iam.kubesphere.io/v1beta1', kind='User', spec={'synthetic': 'keep'})
        user['metadata'].update(name='admin', annotations={'iam.kubesphere.io/globalrole': '',
            'iam.kubesphere.io/granted-clusters': 'host', 'synthetic': 'keep'})
        binding = {'apiVersion': 'iam.kubesphere.io/v1beta1', 'kind': 'ClusterRoleBinding',
            'metadata': {'name': 'admin-cluster-admin', 'uid': 'binding-1', 'resourceVersion': '1',
                'labels': {'iam.kubesphere.io/role-ref': 'cluster-admin', 'iam.kubesphere.io/user-ref': 'admin'}},
            'roleRef': {'apiGroup': 'iam.kubesphere.io', 'kind': 'ClusterRole', 'name': 'cluster-admin'},
            'subjects': [{'apiGroup': 'iam.kubesphere.io', 'kind': 'User', 'name': 'admin'}]}
        current = copy.deepcopy(user);current['metadata']['resourceVersion'] = '6'
        current['metadata']['annotations']['iam.kubesphere.io/granted-clusters'] = ''
        return user, current, {'binding-1': binding}

    def test_cluster_grant_clear_requires_exact_reviewed_scope_role_subject_labels_and_stable_content(self):
        before, current, bindings = self.cluster_grant_inputs()
        def verify(a=before, b=current, c=current, refs=bindings, uids={'u-1', 'binding-1'},
                   removed={'binding-1'}, phase='users', completed={'kubesphere-cluster-role-bindings'}):
            return cluster_grant_annotation_transition(a, b, c, refs, uids, removed, phase, completed)
        expected = verify()
        self.assertEqual(content_review_digest(expected), content_review_digest(current))
        self.assertEqual(expected['metadata']['annotations']['iam.kubesphere.io/granted-clusters'], '')
        self.assertEqual(expected['metadata']['annotations']['iam.kubesphere.io/globalrole'], '')
        self.assertIsNone(verify(b=before, c=before))
        second_binding = copy.deepcopy(bindings['binding-1']);second_binding['metadata'].update(uid='binding-2', name='admin-additional')
        refs = {**bindings, 'binding-2': second_binding}
        self.assertIsNotNone(verify(refs=refs, uids={'u-1', 'binding-1', 'binding-2'}, removed={'binding-1', 'binding-2'}))
        for arguments, reason in [({'phase': 'global-role-bindings'}, 'before-binding-retirement'),
                ({'completed': set()}, 'before-binding-retirement'), ({'uids': {'binding-1'}}, 'exact-reviewed-user'),
                ({'uids': {'u-1'}}, 'binding-not-retired'), ({'removed': set()}, 'binding-not-retired'),
                ({'refs': {}}, 'binding-not-retired'),
                ({'refs': refs, 'uids': {'u-1', 'binding-1', 'binding-2'}}, 'binding-not-retired')]:
            with self.subTest(arguments=arguments), self.assertRaisesRegex(ValueError, reason):verify(**arguments)
        mutations = [('apiVersion', 'rbac.authorization.k8s.io/v1'), ('kind', 'RoleBinding')]
        for field, value in mutations:
            wrong = copy.deepcopy(bindings);wrong['binding-1'][field] = value
            with self.subTest(binding=field), self.assertRaisesRegex(ValueError, 'binding-mismatch'):verify(refs=wrong)
        for field, value in [('uid', 'wrong-binding'), ('namespace', 'other'), ('name', '')]:
            wrong = copy.deepcopy(bindings);wrong['binding-1']['metadata'][field] = value
            with self.subTest(bindingIdentity=field), self.assertRaisesRegex(ValueError, 'binding-mismatch'):verify(refs=wrong)
        for field, value in [('apiGroup', 'rbac.authorization.k8s.io'), ('kind', 'Role'), ('name', 'other-role')]:
            wrong = copy.deepcopy(bindings);wrong['binding-1']['roleRef'][field] = value
            with self.subTest(role=field), self.assertRaisesRegex(ValueError, 'binding-mismatch'):verify(refs=wrong)
        for field, value in [('apiGroup', 'rbac.authorization.k8s.io'), ('kind', 'ServiceAccount'), ('name', 'other-user')]:
            wrong = copy.deepcopy(bindings);wrong['binding-1']['subjects'][0][field] = value
            with self.subTest(subject=field), self.assertRaisesRegex(ValueError, 'binding-mismatch'):verify(refs=wrong)
        for label in ('iam.kubesphere.io/role-ref', 'iam.kubesphere.io/user-ref'):
            wrong = copy.deepcopy(bindings);wrong['binding-1']['metadata']['labels'][label] = 'other'
            with self.subTest(label=label), self.assertRaisesRegex(ValueError, 'binding-mismatch'):verify(refs=wrong)
        for value in ('', 'other', 'host,other', None):
            wrong = copy.deepcopy(before);wrong['metadata']['annotations']['iam.kubesphere.io/granted-clusters'] = value
            with self.subTest(prior=value), self.assertRaisesRegex(ValueError, 'reviewed-value-not-exact-host'):verify(a=wrong)
        missing_prior = copy.deepcopy(before);missing_prior['metadata']['annotations'].pop('iam.kubesphere.io/granted-clusters')
        with self.assertRaisesRegex(ValueError, 'reviewed-value-not-exact-host'):verify(a=missing_prior)
        wrong_user = copy.deepcopy(before);wrong_user['metadata']['uid'] = 'binding-1'
        with self.assertRaisesRegex(ValueError, 'exact-reviewed-user'):verify(a=wrong_user)
        for field, value in [('uid', 'other-user'), ('name', 'other'), ('namespace', 'other')]:
            wrong = copy.deepcopy(current);wrong['metadata'][field] = value
            with self.subTest(user=field), self.assertRaisesRegex(ValueError, 'identity-drift'):verify(b=wrong, c=wrong)
        for value in ('other', 'host,other', None):
            wrong = copy.deepcopy(current);wrong['metadata']['annotations']['iam.kubesphere.io/granted-clusters'] = value
            with self.subTest(grants=value), self.assertRaisesRegex(ValueError, 'missing-key-or-unexpected-grants'):verify(b=wrong, c=wrong)
        missing = copy.deepcopy(current);missing['metadata']['annotations'].pop('iam.kubesphere.io/granted-clusters')
        with self.assertRaisesRegex(ValueError, 'missing-key-or-unexpected-grants'):verify(b=missing, c=missing)
        with self.assertRaisesRegex(ValueError, 'not-stable'):verify(c=before)
        for field in ('spec', 'data', 'rules', 'annotation'):
            wrong = copy.deepcopy(current)
            if field == 'annotation':wrong['metadata']['annotations']['synthetic'] = 'changed'
            else:wrong[field] = {'synthetic': 'changed'}
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, 'other-desired-content-drift'):verify(b=wrong, c=wrong)

    def cluster_grant_fixture(self):
        before, current, bindings = self.cluster_grant_inputs()
        fixture, records = self.fixture();fixture.args.age = 'age';fixture.args.recipient = 'public'
        fixture.reviewed_raw_by_uid = {'u-1': before, **bindings}
        fixture.reviewed_content_digests['u-1'] = content_review_digest(before)
        fixture.retirement_uids = {'u-1', 'binding-1'}
        fixture.completed_retirement_phases.add('kubesphere-cluster-role-bindings')
        phase = {'phase': 'users', 'expected': ['u-1'], 'roots': ['u-1'], 'managerAbsentRequired': False, 'mode': 'native-delete'}
        by_uid = {uid: project_resource(raw) for uid, raw in fixture.reviewed_raw_by_uid.items()}
        resources = {('iam.kubesphere.io/v1beta1', 'User'): ('users', False),
                     ('iam.kubesphere.io/v1beta1', 'ClusterRoleBinding'): ('clusterrolebindings', False)}
        return fixture, records, before, current, phase, by_uid, resources

    def test_users_checkpoint_waits_for_exact_async_clear_and_two_fresh_stable_reads(self):
        fixture, records, before, current, phase, by_uid, resources = self.cluster_grant_fixture()
        stable = copy.deepcopy(current);stable['metadata'].update(resourceVersion='7', generation=4, managedFields=[])
        stable['status'] = {'state': 'ready'}
        with patch.object(fixture, 'settled_inventory', return_value=([project_resource(current)], True)), \
             patch.object(fixture, 'native_read', side_effect=[None, before, current, stable, None]) as read, \
             patch.object(fixture, 'native_retire', side_effect=ValueError('delete-sentinel')) as retire, \
             patch('rehearse.time.sleep') as sleep:
            with self.assertRaisesRegex(ValueError, 'delete-sentinel'):
                fixture.retire_phase(phase, fixture.retirement_uids, by_uid, resources)
        self.assertEqual(read.call_count, 5);self.assertEqual(sleep.call_count, 2);retire.assert_called_once()
        self.assertEqual(read.call_args_list[0].args[0], '/apis/iam.kubesphere.io/v1beta1/clusterrolebindings/admin-cluster-admin')
        self.assertEqual(read.call_args_list[-1].args, read.call_args_list[0].args)
        self.assertTrue(all(c.args[0].endswith('/users/admin') for c in read.call_args_list[1:-1]))
        self.assertEqual(fixture.reviewed_content_digests['u-1'], content_review_digest(current))
        self.assertEqual(fixture.reviewed_raw_by_uid['binding-1'], self.cluster_grant_inputs()[2]['binding-1'])
        record = records['cluster-grant-annotation-transition.json']
        self.assertTrue(record['allMatchingReviewedBindingsRetired']);self.assertTrue(record['twoNativeReadsStable'])
        self.assertEqual(record['transitions'][0]['nativeUserReadCount'], 3)
        self.assertEqual(fixture.attempt.encrypt.call_args.args[0], 'cluster-grant-annotation-transition')

    def test_users_checkpoint_stops_on_bound_unstable_content_live_binding_and_incomplete_phase(self):
        for case in ('timeout', 'deadline', 'unstable', 'other-content', 'live-binding', 'recreated-binding',
                     'retained-binding', 'retained-extra-binding', 'wrong-phase'):
            fixture, records, before, current, phase, by_uid, resources = self.cluster_grant_fixture()
            initial_digest = fixture.reviewed_content_digests['u-1']
            inventory = [project_resource(current)]
            reads = [None, *([before] * 10)]
            reason = 'within-bound'
            if case == 'unstable':reads = [None, current, before];reason = 'not-stable'
            elif case == 'other-content':
                wrong = copy.deepcopy(current);wrong['spec']['synthetic'] = 'changed'
                reads = [None, wrong];reason = 'other-desired-content-drift'
            elif case == 'live-binding':reads = [fixture.reviewed_raw_by_uid['binding-1']];reason = 'still-present'
            elif case == 'recreated-binding':reads = [None, current, current, fixture.reviewed_raw_by_uid['binding-1']];reason = 'still-present'
            elif case == 'retained-binding':inventory.append(by_uid['binding-1']);reads = [];reason = 'binding-not-retired'
            elif case == 'retained-extra-binding':
                extra = copy.deepcopy(fixture.reviewed_raw_by_uid['binding-1']);extra['metadata'].update(uid='binding-2', name='admin-additional')
                fixture.reviewed_raw_by_uid['binding-2'] = extra;fixture.retirement_uids.add('binding-2')
                inventory.append(project_resource(extra));reads = [];reason = 'binding-not-retired'
            elif case == 'wrong-phase':fixture.completed_retirement_phases.clear();reads = [];reason = 'before-binding-retirement'
            with self.subTest(case=case), patch.object(fixture, 'settled_inventory', return_value=(inventory, True)), \
                 patch.object(fixture, 'native_read', side_effect=reads) as read, patch.object(fixture, 'native_retire') as retire, \
                 patch('rehearse.time.sleep'), patch('rehearse.time.monotonic', side_effect=itertools.count(0, 31) if case == 'deadline' else itertools.repeat(0)):
                with self.assertRaisesRegex(ValueError, reason):fixture.retire_phase(phase, fixture.retirement_uids, by_uid, resources)
            retire.assert_not_called();self.assertNotIn('cluster-grant-annotation-transition.json', records)
            self.assertEqual(fixture.reviewed_content_digests['u-1'], initial_digest)
            if case == 'timeout':self.assertEqual(read.call_count, 11)
            if case == 'deadline':self.assertEqual(read.call_count, 2)

    def test_named_finalizer_removes_only_one_named_finalizer_after_deletion_request(self):
        fixture, _ = self.fixture()
        fixture.reviewed_raw_by_uid['u-1'] = self.obj(finalizers=[SYSTEM_WORKSPACE_FINALIZER, 'foregroundDeletion'])
        current = self.obj(finalizers=[SYSTEM_WORKSPACE_FINALIZER, 'foregroundDeletion'], deleting=True)
        current['metadata']['deletionGracePeriodSeconds'] = 0
        with patch.object(fixture, 'native_read', return_value=current), \
             patch.object(fixture, 'kube', return_value=SimpleNamespace(returncode=0, stdout=b'', stderr=b'')) as kube:
            outcome = fixture.remove_named_finalizer('/apis/x', 'u-1', SYSTEM_WORKSPACE_FINALIZER)
        body = kube.call_args.kwargs['obj']
        self.assertEqual(kube.call_args.args[1:3], ('replace', '--raw'))
        self.assertEqual(body['metadata']['finalizers'], ['foregroundDeletion'])
        self.assertEqual((body['metadata']['uid'], body['metadata']['resourceVersion']), ('u-1', '5'))
        self.assertEqual(outcome['otherFinalizersRetained'], ['foregroundDeletion'])
        self.assertTrue(outcome['privateFullContentVerified'])
        for candidate, reason in [(self.obj(finalizers=[SYSTEM_WORKSPACE_FINALIZER]), 'precondition'),
                                  (self.obj(finalizers=['other/hold'], deleting=True), 'precondition'),
                                  (self.obj(uid='replacement', finalizers=[SYSTEM_WORKSPACE_FINALIZER], deleting=True), 'precondition'),
                                  (None, 'precondition'),
                                  (self.obj(finalizers=[SYSTEM_WORKSPACE_FINALIZER], deleting=True,
                                            owners=[{'apiVersion': 'v1', 'kind': 'X', 'name': 'x', 'uid': 'unseen'}]), 'full-content-drift')]:
            with self.subTest(reason=reason), patch.object(fixture, 'native_read', return_value=candidate), \
                 patch.object(fixture, 'kube') as kube:
                with self.assertRaisesRegex(ValueError, reason):fixture.remove_named_finalizer('/apis/x', 'u-1', SYSTEM_WORKSPACE_FINALIZER)
            kube.assert_not_called()
        with patch.object(fixture, 'native_read', return_value=current), patch.object(fixture, 'kube',
                return_value=SimpleNamespace(returncode=1, stdout=b'', stderr=b'Error from server (Forbidden): denied')):
            with self.assertRaisesRegex(ValueError, 'rejected'):fixture.remove_named_finalizer('/apis/x', 'u-1', SYSTEM_WORKSPACE_FINALIZER)

    def test_named_finalizer_requires_existing_raw_review_and_exact_native_preconditions(self):
        fixture, _ = self.fixture()
        current = self.obj(finalizers=[SYSTEM_WORKSPACE_FINALIZER], deleting=True)
        with patch.object(fixture, 'native_read') as read, patch.object(fixture, 'kube') as kube:
            with self.assertRaisesRegex(ValueError, 'baseline-missing'):
                fixture.remove_named_finalizer('/apis/x', 'u-1', SYSTEM_WORKSPACE_FINALIZER)
        read.assert_not_called();kube.assert_not_called()
        fixture.reviewed_raw_by_uid['u-1'] = self.obj(finalizers=[SYSTEM_WORKSPACE_FINALIZER])
        for version in ('', None, 7):
            changed = copy.deepcopy(current);changed['metadata']['resourceVersion'] = version
            with self.subTest(version=version), patch.object(fixture, 'native_read', return_value=changed), patch.object(fixture, 'kube') as kube:
                with self.assertRaisesRegex(ValueError, 'precondition'):
                    fixture.remove_named_finalizer('/apis/x', 'u-1', SYSTEM_WORKSPACE_FINALIZER)
            kube.assert_not_called()
        for update in ({'deletionGracePeriodSeconds': 1}, {'deletionGracePeriodSeconds': False},
                       {'annotations': {'unreviewed': 'change'}}, {'finalizers': [SYSTEM_WORKSPACE_FINALIZER, 'other/hold']}):
            changed = copy.deepcopy(current);changed['metadata'].update(update)
            with self.subTest(update=update), patch.object(fixture, 'native_read', return_value=changed), patch.object(fixture, 'kube') as kube:
                with self.assertRaisesRegex(ValueError, 'full-content-drift'):
                    fixture.remove_named_finalizer('/apis/x', 'u-1', SYSTEM_WORKSPACE_FINALIZER)
            kube.assert_not_called()

    def test_named_finalizer_refuses_spec_data_rules_drift_after_delete_and_on_conflict_retry(self):
        examples = [('apps/v1', 'Deployment', 'spec', {'strategy': {'type': 'RollingUpdate'}}, {'strategy': {'type': 'Recreate'}}),
                    ('v1', 'Secret', 'data', {'synthetic': 'c3ludGhldGlj'}, {'synthetic': 'ZHJpZnQ='}),
                    ('rbac.authorization.k8s.io/v1', 'ClusterRole', 'rules', [{'verbs': ['get']}], [{'verbs': ['*']}])]
        conflict = SimpleNamespace(returncode=1, stdout=b'', stderr=b'Error from server (Conflict): stale version')
        for api, kind, field, original, changed_value in examples:
            reviewed = self.obj(finalizers=[SYSTEM_WORKSPACE_FINALIZER]);reviewed.update(apiVersion=api, kind=kind)
            reviewed[field] = original
            current = copy.deepcopy(reviewed);current['metadata'].update(resourceVersion='6',
                deletionTimestamp='2026-10-02T09:00:00Z', deletionGracePeriodSeconds=0)
            changed = copy.deepcopy(current);changed['metadata']['resourceVersion'] = '7';changed[field] = changed_value
            fixture, _ = self.fixture();fixture.reviewed_raw_by_uid['u-1'] = reviewed
            for retry in (False, True):
                reads = [current, changed] if retry else [changed]
                with self.subTest(kind=kind, retry=retry), patch.object(fixture, 'native_read', side_effect=reads), \
                     patch.object(fixture, 'kube', return_value=conflict) as kube:
                    with self.assertRaisesRegex(ValueError, 'full-content-drift'):
                        fixture.remove_named_finalizer('/apis/x', 'u-1', SYSTEM_WORKSPACE_FINALIZER)
                self.assertEqual(kube.call_count, int(retry))
                if retry:self.assertEqual(kube.call_args.kwargs['obj']['metadata']['resourceVersion'], '6')
            self.assertEqual(fixture.reviewed_raw_by_uid['u-1'], reviewed)

    def test_named_finalizer_conflict_rechecks_full_content_and_binds_fresh_version(self):
        fixture, _ = self.fixture();reviewed = self.obj(finalizers=[SYSTEM_WORKSPACE_FINALIZER, 'other/keep'])
        reviewed['spec'] = {'synthetic': 'keep'};fixture.reviewed_raw_by_uid['u-1'] = reviewed
        current = copy.deepcopy(reviewed);current['metadata'].update(resourceVersion='6',
            deletionTimestamp='2026-10-02T09:00:00Z', deletionGracePeriodSeconds=0)
        retried = copy.deepcopy(current);retried['metadata'].update(resourceVersion='7', generation=2, managedFields=[])
        retried['status'] = {'ready': False}
        conflict = SimpleNamespace(returncode=1, stdout=b'', stderr=b'Error from server (Conflict): stale version')
        ok = SimpleNamespace(returncode=0, stdout=b'', stderr=b'')
        with patch.object(fixture, 'native_read', side_effect=[current, retried]), patch.object(fixture, 'kube', side_effect=[conflict, ok]) as kube:
            result = fixture.remove_named_finalizer('/apis/x', 'u-1', SYSTEM_WORKSPACE_FINALIZER)
        self.assertEqual(result['conflictRetries'], 1);self.assertTrue(result['privateFullContentVerified'])
        self.assertEqual([c.kwargs['obj']['metadata']['resourceVersion'] for c in kube.call_args_list], ['6', '7'])
        self.assertTrue(all(c.kwargs['obj']['metadata']['uid'] == 'u-1' and
            c.kwargs['obj']['metadata']['finalizers'] == ['other/keep'] for c in kube.call_args_list))
        changed_time = copy.deepcopy(retried);changed_time['metadata']['deletionTimestamp'] = '2026-10-02T10:00:00Z'
        with patch.object(fixture, 'native_read', side_effect=[current, changed_time]), patch.object(fixture, 'kube', return_value=conflict) as kube:
            with self.assertRaisesRegex(ValueError, 'deletion-state-drift'):
                fixture.remove_named_finalizer('/apis/x', 'u-1', SYSTEM_WORKSPACE_FINALIZER)
        kube.assert_called_once()

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
                         ['reconciled-admission', 'remaining-release-objects', 'release-records', 'system-workspace-finalizers', 'namespace-finalizers'])
        removal = plan['controllers-and-services']
        after = [v for v in baseline if v['uid'] not in removal['expected']]
        observed = [{**v, 'resourceVersion': '2'} for v in baseline]  # status churn seen at phase start
        fixture, records = self.fixture()
        with patch.object(fixture, 'settled_inventory', side_effect=[(observed, True), (after, True)]), \
             patch.object(fixture, 'native_retire', return_value={'outcome': 'native-delete-accepted'}) as retire, \
             patch.object(fixture, 'wait_absent', return_value=True):
            fixture.retire_phase(removal, uids, by_uid, resources)
        self.assertEqual(retire.call_count, len(removal['roots']))
        # Each request is bound to the reviewed baseline entry, not to the phase's fresh observation.
        self.assertEqual([c.args[2] for c in retire.call_args_list], [by_uid[uid] for uid in removal['roots']])
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

    def test_tenant_sync_gate_reports_elapsed_or_none(self):
        fixture = Fixture(SimpleNamespace(attempt_id='probe-test'), SimpleNamespace(directory=Path('/nonexistent')))
        with patch.object(fixture, 'native_read', side_effect=[None, None, {'metadata': {}}]), patch('rehearse.time.sleep'), \
             patch('rehearse.time.monotonic', side_effect=itertools.count(0, 10)):
            self.assertEqual(fixture.wait_tenant_sync(), 30)
        with patch.object(fixture, 'native_read', return_value=None), patch('rehearse.time.sleep'), \
             patch('rehearse.time.monotonic', side_effect=itertools.count(0, 10)):
            self.assertIsNone(fixture.wait_tenant_sync())

    def test_probe_records_unbinding_without_treating_it_as_a_cascade(self):
        attempt = SimpleNamespace(directory=Path('/nonexistent'), encrypt=Mock(), record=Mock())
        fixture = Fixture(SimpleNamespace(attempt_id='probe-test'), attempt)
        meta = lambda name, uid, **extra: {'name': name, 'uid': uid, 'resourceVersion': '1', **extra}
        template = {'apiVersion': 'tenant.kubesphere.io/v1beta1', 'kind': 'WorkspaceTemplate', 'metadata': meta('s426-probe', 't')}
        workspace = {'apiVersion': 'tenant.kubesphere.io/v1beta1', 'kind': 'Workspace', 'metadata': meta('s426-probe', 'w')}
        bound = {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': meta('s426-probe-member', 'n', finalizers=[SYSTEM_WORKSPACE_FINALIZER],
                 labels={'kubesphere.io/workspace': 's426-probe'})}
        unbound = {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': meta('s426-probe-member', 'n')}
        reads = iter([workspace, bound, template, None, None, unbound] + [None] * 9)
        with patch.object(fixture, 'create'), patch.object(fixture, 'native_read', side_effect=lambda path: next(reads)), \
             patch.object(fixture, 'native_retire', return_value={'outcome': 'native-delete-accepted'}), patch('rehearse.time.sleep'):
            record = fixture.probe_workspace_propagation()
        self.assertEqual(record['memberNamespaceOutcome'], 'retained-label-or-finalizer-changed')
        self.assertFalse(record['cascadeHazard']);self.assertFalse(record['procedurePart'])
        self.assertEqual(record['memberAfter']['finalizers'], [])

    def quota_fixture(self):
        records = {}
        attempt = SimpleNamespace(directory=Path('/nonexistent'), encrypt=Mock(),
                                  record=lambda name, value: records.update({name: copy.deepcopy(value)}))
        fixture = Fixture(SimpleNamespace(attempt_id='synthetic-quota-test', age='age', recipient='synthetic-recipient'), attempt)
        template = {'apiVersion': 'tenant.kubesphere.io/v1beta1', 'kind': 'WorkspaceTemplate',
                    'metadata': {'name': 's426-probe', 'uid': 'synthetic-template-uid', 'resourceVersion': '1'}}
        secret = {'apiVersion': 'v1', 'kind': 'Secret', 'type': 'Opaque',
                  'metadata': {'name': 'io.kubesphere.license.quota.v3.workspace.synthetic-template-uid',
                               'namespace': 'kubesphere-system', 'uid': 'synthetic-quota-uid', 'resourceVersion': '1',
                               'labels': {'kubesphere.io/workspace': 's426-probe'}}, 'data': {'synthetic': 'cHJvYmU='}}
        return fixture, records, template, secret

    def test_quota_cleanup_derives_exact_name_and_binds_full_content_uid_and_fresh_rv(self):
        fixture, records, template, secret = self.quota_fixture()
        fresh = copy.deepcopy(secret);fresh['metadata'].update({'resourceVersion': '2', 'managedFields': [{'manager': 'synthetic'}]})
        reads = [None, None, secret, None, None, fresh] + [None] * 6
        with patch.object(fixture, 'native_read', side_effect=reads) as read, \
             patch.object(fixture, 'kube', return_value=SimpleNamespace(returncode=0)) as kube, patch('rehearse.time.sleep') as sleep:
            result = fixture.cleanup_workspace_probe_quota(template)
        path = '/api/v1/namespaces/kubesphere-system/secrets/' + secret['metadata']['name']
        self.assertEqual(kube.call_args.args, ('native-retire-workspace-probe-quota', 'delete', '--raw', path, '-f', '-'))
        self.assertEqual(kube.call_args.kwargs['obj']['preconditions'], {'uid': 'synthetic-quota-uid', 'resourceVersion': '2'})
        self.assertEqual(kube.call_args.kwargs['obj']['propagationPolicy'], 'Foreground')
        self.assertTrue(result['request']['privateFullContentVerified'])
        self.assertEqual(result['state'], 'passed-synthetic-only-cleanup')
        self.assertEqual(result['parentsAbsentBeforeCapture'], [True, True])
        self.assertEqual(result['parentsAbsentBeforeRequest'], [True, True])
        self.assertEqual(result['absenceSamples'], [{'templateAbsent': True, 'workspaceAbsent': True, 'quotaAbsent': True}] * 2)
        self.assertEqual(sleep.call_args_list, [unittest.mock.call(2)] * 2)
        self.assertEqual(read.call_count, 12)
        self.assertTrue(all('*' not in call.args[0] for call in read.call_args_list))
        self.assertEqual(json.loads(fixture.attempt.encrypt.call_args.args[1]), secret)
        self.assertEqual(fixture.reviewed_content_digests, {})
        self.assertEqual(records['workspace-probe-quota-cleanup.json'], result)

    def test_quota_cleanup_initial_absence_requires_two_separated_parent_and_residue_samples(self):
        fixture, records, template, _ = self.quota_fixture()
        with patch.object(fixture, 'native_read', side_effect=[None] * 9) as read, \
             patch.object(fixture, 'kube') as kube, patch('rehearse.time.sleep') as sleep:
            result = fixture.cleanup_workspace_probe_quota(template)
        kube.assert_not_called();fixture.attempt.encrypt.assert_not_called()
        self.assertIsNone(result['request']);self.assertEqual(len(result['absenceSamples']), 2)
        self.assertEqual(read.call_count, 9);self.assertEqual(sleep.call_count, 2)
        self.assertEqual(result['state'], 'passed-synthetic-only-cleanup')

    def test_quota_cleanup_refuses_wrong_template_or_secret_identity_and_owned_or_deleting_content(self):
        for field, value in (('apiVersion', 'other/v1'), ('kind', 'Workspace'), ('name', 'source'),
                             ('namespace', 'source'), ('uid', '../source'), ('uid', '')):
            with self.subTest(template_field=field, value=value):
                fixture, _, template, _ = self.quota_fixture()
                (template if field in ('apiVersion', 'kind') else template['metadata'])[field] = value
                with patch.object(fixture, 'native_read') as read, patch.object(fixture, 'kube') as kube:
                    with self.assertRaisesRegex(ValueError, 'template-identity-invalid'):fixture.cleanup_workspace_probe_quota(template)
                read.assert_not_called();kube.assert_not_called()
        changes = ({'apiVersion': 'other/v1'}, {'kind': 'ConfigMap'}, {'metadata': {'namespace': 'source'}},
                   {'metadata': {'name': 'io.kubesphere.license.quota.v3.workspace.source-template-uid'}},
                   {'metadata': {'labels': {'kubesphere.io/workspace': 'source'}}}, {'metadata': {'labels': None}},
                   {'metadata': {'uid': ''}}, {'metadata': {'resourceVersion': ''}},
                   {'metadata': {'ownerReferences': [{'uid': 'source'}]}}, {'metadata': {'finalizers': ['source/retain']}},
                   {'metadata': {'deletionTimestamp': '2026-10-04T00:00:00Z'}}, {'metadata': {'deletionGracePeriodSeconds': 0}})
        for change in changes:
            with self.subTest(secret_change=change):
                fixture, records, template, secret = self.quota_fixture()
                secret['metadata'].update(change.get('metadata', {}));secret.update({k: v for k, v in change.items() if k != 'metadata'})
                with patch.object(fixture, 'native_read', side_effect=[None, None, secret]), patch.object(fixture, 'kube') as kube:
                    with self.assertRaisesRegex(ValueError, 'quota-identity-invalid'):fixture.cleanup_workspace_probe_quota(template)
                kube.assert_not_called();self.assertEqual(records['workspace-probe-quota-cleanup.json']['state'], 'failed-closed')

    def test_quota_cleanup_refuses_raw_drift_before_delete_and_on_conflict_retry(self):
        changes = ({'data': {'synthetic': 'YWx0ZXJlZA=='}}, {'spec': {'unexpected': True}},
                   {'rules': [{'verbs': ['*']}]}, {'metadata': {'annotations': {'unexpected': 'grant'}}})
        for retry in (False, True):
            for change in changes:
                with self.subTest(retry=retry, change=change):
                    fixture, records, template, secret = self.quota_fixture()
                    changed = copy.deepcopy(secret)
                    changed['metadata'].update(change.get('metadata', {}))
                    changed.update({k: v for k, v in change.items() if k != 'metadata'})
                    reads = [None, None, secret, None, None] + ([secret] if retry else []) + [changed]
                    conflict = SimpleNamespace(returncode=1, stdout=b'', stderr=b'Error from server (Conflict): synthetic conflict')
                    with patch.object(fixture, 'native_read', side_effect=reads), patch.object(fixture, 'kube', return_value=conflict) as kube:
                        with self.assertRaisesRegex(ValueError, 'reviewed-full-content-drift'):fixture.cleanup_workspace_probe_quota(template)
                    self.assertEqual(kube.call_count, int(retry));self.assertEqual(fixture.reviewed_content_digests, {})
                    self.assertEqual(records['workspace-probe-quota-cleanup.json']['state'], 'failed-closed')

    def test_quota_cleanup_refuses_replacement_uid_and_native_delete_rejection(self):
        for refusal in ('uid', 'delete'):
            with self.subTest(refusal=refusal):
                fixture, records, template, secret = self.quota_fixture()
                observed = copy.deepcopy(secret)
                if refusal == 'uid':observed['metadata']['uid'] = 'replacement'
                rejected = SimpleNamespace(returncode=1, stdout=b'', stderr=b'Error from server (Forbidden): synthetic refusal')
                with patch.object(fixture, 'native_read', side_effect=[None, None, secret, None, None, observed]), \
                     patch.object(fixture, 'kube', return_value=rejected) as kube:
                    with self.assertRaisesRegex(ValueError, 'uid-drift' if refusal == 'uid' else 'native-delete-rejected'):
                        fixture.cleanup_workspace_probe_quota(template)
                self.assertEqual(kube.call_count, int(refusal == 'delete'))
                self.assertEqual(records['workspace-probe-quota-cleanup.json']['state'], 'failed-closed')

    def test_quota_cleanup_refuses_lingering_or_recreated_parents_and_residue(self):
        for stage in ('capture', 'request', 'first-sample', 'second-sample'):
            for member in ('template', 'workspace', 'quota'):
                if stage in ('capture', 'request') and member == 'quota':continue
                with self.subTest(stage=stage, member=member):
                    fixture, records, template, secret = self.quota_fixture()
                    reads = [None, None, secret, None, None, secret] + [None] * 6
                    index = {'capture': 0, 'request': 3, 'first-sample': 6, 'second-sample': 9}[stage]
                    index += ('template', 'workspace', 'quota').index(member)
                    reads[index] = secret if member == 'quota' else template
                    with patch.object(fixture, 'native_read', side_effect=reads), \
                         patch.object(fixture, 'kube', return_value=SimpleNamespace(returncode=0)) as kube, patch('rehearse.time.sleep'):
                        with self.assertRaisesRegex(ValueError, 'parent-still-present|parent-recreated|not-stably-absent'):
                            fixture.cleanup_workspace_probe_quota(template)
                    self.assertEqual(kube.call_count, int(stage in ('first-sample', 'second-sample')))
                    self.assertEqual(records['workspace-probe-quota-cleanup.json']['state'], 'failed-closed')

    def test_quota_cleanup_refuses_residue_appearing_after_initial_absence(self):
        fixture, records, template, secret = self.quota_fixture()
        with patch.object(fixture, 'native_read', side_effect=[None] * 8 + [secret]), \
             patch.object(fixture, 'kube') as kube, patch('rehearse.time.sleep'):
            with self.assertRaisesRegex(ValueError, 'not-stably-absent'):fixture.cleanup_workspace_probe_quota(template)
        kube.assert_not_called();self.assertEqual(len(records['workspace-probe-quota-cleanup.json']['absenceSamples']), 2)

    def test_probe_refuses_lingering_parent_before_quota_cleanup_and_baseline(self):
        for lingering in ('template', 'workspace'):
            with self.subTest(parent=lingering):
                fixture, records, template, _ = self.quota_fixture()
                workspace = {**template, 'kind': 'Workspace'}
                member = {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': 's426-probe-member', 'uid': 'member',
                          'resourceVersion': '1', 'finalizers': [SYSTEM_WORKSPACE_FINALIZER]}}
                counts = {}
                def read(path):
                    counts[path] = counts.get(path, 0) + 1
                    if path.endswith('/workspacetemplates/s426-probe'):return template if lingering == 'template' or counts[path] == 1 else None
                    if path.endswith('/workspaces/s426-probe'):return workspace if lingering == 'workspace' or counts[path] == 1 else None
                    return member
                with patch.object(fixture, 'create'), patch.object(fixture, 'native_read', side_effect=read), \
                     patch.object(fixture, 'native_retire', return_value={}), patch.object(fixture, 'cleanup_workspace_probe_quota') as cleanup, \
                     patch('rehearse.time.sleep'), patch('rehearse.time.monotonic', side_effect=itertools.count(0, 20)):
                    with self.assertRaisesRegex(ValueError, 'parent-still-present'):fixture.probe_workspace_propagation()
                cleanup.assert_not_called();self.assertIn('workspace-propagation-probe.json', records)


class PostRetirementTests(unittest.TestCase):
    def run_checks(self, final, crds=(), pods=(), namespace_labels=None, namespace_gone=True, namespace_finalizers=(),
                   config_gone=True, job_status=None):
        records = {}
        attempt = SimpleNamespace(directory=Path('/nonexistent'), encrypt=Mock(), record=lambda n, v: records.update({n: v}))
        fixture = Fixture(SimpleNamespace(attempt_id='post-retirement-test'), attempt)
        created = {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': 's426-post-retirement', 'uid': 'n-1',
                   'resourceVersion': '1', 'labels': namespace_labels or {}, 'finalizers': list(namespace_finalizers)}}
        written = {'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 's426-post-retirement', 'namespace': 's426-workload',
                   'uid': 'c-1', 'resourceVersion': '1'}}
        reads = {'/api/v1/namespaces/s426-workload/configmaps/s426-post-retirement':
                 [written] + [None if config_gone else written] * 30,
                 '/api/v1/namespaces/s426-post-retirement': [created] + [None if namespace_gone else created] * 30}
        def get(resource, name=None, namespace=None):
            if resource == 'pods':
                return {'items': list(pods)}
            self.assertEqual(resource, 'jobs.batch')
            job = next(v for v in final if v['kind'] == 'Job' and v['name'] == name and v['namespace'] == namespace)
            return {'metadata': {'uid': job['uid']}, 'status': job_status or {}}
        with patch.object(fixture, 'create'), patch.object(fixture, 'get', side_effect=get), \
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
        # A live native webhook whose Service stays (kube-system/kube-dns) is not a stale reference.
        dns_hook = {**ks('admissionregistration.k8s.io/v1', 'ValidatingWebhookConfiguration', 'dns-guard', 'dns-hook'),
                    'webhooks': [{'service': {'namespace': 'kube-system', 'name': 'kube-dns'}}]}
        error, record, retire = self.run_checks([ks('v1', 'Service', 'kube-dns', 's', 'kube-system'), dns_hook])
        self.assertIsNone(error)
        self.assertEqual(record['staleAdmissionOrApiServiceReferences'], [])
        self.assertEqual(retire.call_args_list[0].args[1], '/api/v1/namespaces/s426-workload/configmaps/s426-post-retirement')
        self.assertEqual([c.args[2]['uid'] for c in retire.call_args_list], ['c-1', 'n-1'])
        self.assertTrue(record['nativeConfigMapDeletionCompleted'])
        self.assertTrue(record['newNamespaceDeletionCompleted'])
        clean = {'staleAdmissionOrApiServiceReferences': [], 'staleRouteBackends': [], 'managerRuntimeRemaining': [],
                 'newNamespaceManagementLabels': {}, 'newNamespaceFinalizers': [], 'newNamespaceDeletionCompleted': True,
                 'nativeConfigMapDeletionCompleted': True}
        hook = {**ks('admissionregistration.k8s.io/v1', 'ValidatingWebhookConfiguration', 'users', 'h'),
                'webhooks': [{'service': {'namespace': 'kubesphere-system', 'name': 'ks-controller-manager'}}]}
        crd = {**ks('apiextensions.k8s.io/v1', 'CustomResourceDefinition', 'x.kubesphere.io', 'crd'),
               'customResource': {'conversionService': {'namespace': 'kubesphere-system', 'name': 'ks-controller-manager'}}}
        pod = {'apiVersion': 'v1', 'kind': 'Pod', 'metadata': {'name': 'ks-apiserver-x', 'namespace': 'kubesphere-system', 'uid': 'p'},
               'status': {'phase': 'Running'}}
        done = {**pod, 'metadata': {**pod['metadata'], 'name': 'installer-x'}, 'status': {'phase': 'Succeeded'}}
        api = {**ks('apiregistration.k8s.io/v1', 'APIService', 'v1alpha1.iam.kubesphere.io', 'api'),
               'service': {'namespace': 'kubesphere-system', 'name': 'ks-apiserver'}}
        for kwargs, field, value in [
                ({'final': [hook]}, 'staleAdmissionOrApiServiceReferences',
                 ['admissionregistration.k8s.io/v1/ValidatingWebhookConfiguration/-/users']),
                ({'final': [api]}, 'staleAdmissionOrApiServiceReferences', ['apiregistration.k8s.io/v1/APIService/-/v1alpha1.iam.kubesphere.io']),
                ({'final': [], 'namespace_finalizers': [SYSTEM_WORKSPACE_FINALIZER]}, 'newNamespaceFinalizers', [SYSTEM_WORKSPACE_FINALIZER]),
                ({'final': [], 'crds': [crd]}, 'staleAdmissionOrApiServiceReferences',
                 ['apiextensions.k8s.io/v1/CustomResourceDefinition/-/x.kubesphere.io']),
                ({'final': [ks('apps/v1', 'Deployment', 'ks-apiserver', 'd', 'kubesphere-system')]}, 'managerRuntimeRemaining',
                 ['apps/v1/Deployment/kubesphere-system/ks-apiserver']),
                ({'final': [], 'pods': [pod]}, 'managerRuntimeRemaining', ['v1/Pod/kubesphere-system/ks-apiserver-x']),
                ({'final': [], 'namespace_labels': {'kubesphere.io/workspace': 'system-workspace'}}, 'newNamespaceManagementLabels',
                 {'kubesphere.io/workspace': 'system-workspace'}),
                ({'final': [], 'namespace_gone': False}, 'newNamespaceDeletionCompleted', False),
                ({'final': [{**ks('networking.k8s.io/v1', 'Ingress', 'kubesphere-console', 'ing', 'kubesphere-system'),
                             'backends': [{'kind': 'Service', 'name': 'ks-console', 'namespace': 'kubesphere-system'}]}]},
                 'staleRouteBackends', ['networking.k8s.io/v1/Ingress/kubesphere-system/kubesphere-console -> kubesphere-system/ks-console']),
                ({'final': [{**ks('gateway.networking.k8s.io/v1', 'HTTPRoute', 'console', 'route', 'kubesphere-system'),
                             'backends': [{'kind': None, 'name': 'ks-console', 'namespace': None}]}]},
                 'staleRouteBackends', ['gateway.networking.k8s.io/v1/HTTPRoute/kubesphere-system/console -> kubesphere-system/ks-console'])]:
            with self.subTest(field=field, kwargs=list(kwargs)):
                error, record, _ = self.run_checks(**kwargs)
                self.assertEqual(error, 'post-retirement-native-health-failed')
                # Exactly the failing field carries its value; every other health field stays clean.
                self.assertEqual({f: record[f] for f in clean}, {**clean, field: value})
                self.assertIs(record['newNamespaceDeletionCompleted'], field != 'newNamespaceDeletionCompleted')
        live = [ks('v1', 'Service', 'web', 'svc', 'app'),
                {**ks('networking.k8s.io/v1', 'Ingress', 'web', 'ing', 'app'),
                 'backends': [{'kind': 'Service', 'name': 'web', 'namespace': 'app'}, {'apiGroup': 'x', 'kind': 'Bucket', 'name': 'b'}]}]
        error, record, _ = self.run_checks(live)
        self.assertIsNone(error);self.assertEqual(record['staleRouteBackends'], [])
        error, record, _ = self.run_checks([], pods=[done])
        self.assertIsNone(error)
        self.assertEqual(record['completedManagerPodResidue'], ['v1/Pod/kubesphere-system/installer-x'])

    def test_native_health_refuses_configmap_left_after_accepted_delete(self):
        error, record, retire = self.run_checks([], config_gone=False)
        self.assertEqual(error, 'post-retirement-native-health-failed')
        self.assertFalse(record['nativeConfigMapDeletionCompleted'])
        self.assertEqual(record['nativeConfigMapWriteDelete']['outcome'], 'native-delete-accepted')
        self.assertEqual(retire.call_count, 2)

    def test_native_health_refuses_manager_cronjob_and_active_podless_job(self):
        for kind, status in (('CronJob', None), ('Job', {'active': 1})):
            with self.subTest(kind=kind):
                manager = ks('batch/v1', kind, 'manager-task', 'task-1', 'kubesphere-system')
                error, record, _ = self.run_checks([manager], job_status=status)
                self.assertEqual(error, 'post-retirement-native-health-failed')
                self.assertEqual(record['managerRuntimeRemaining'],
                                 [f'batch/v1/{kind}/kubesphere-system/manager-task'])
        finished = ks('batch/v1', 'Job', 'finished-task', 'task-2', 'kubesphere-system')
        error, record, _ = self.run_checks([finished], job_status={
            'conditions': [{'type': 'Complete', 'status': 'True'}]})
        self.assertIsNone(error)
        self.assertEqual(record['completedManagerJobResidue'],
                         ['batch/v1/Job/kubesphere-system/finished-task'])
        error, record, _ = self.run_checks([finished], job_status={
            'active': 1, 'conditions': [{'type': 'Complete', 'status': 'True'}]})
        self.assertEqual(error, 'post-retirement-native-health-failed')
        self.assertEqual(record['managerRuntimeRemaining'],
                         ['batch/v1/Job/kubesphere-system/finished-task'])


class ReviewFollowUpTests(unittest.TestCase):
    def fixture(self, **args):
        records = {}
        attempt = SimpleNamespace(directory=Path('/nonexistent'), encrypt=Mock(), record=lambda n, v: records.update({n: v}))
        return Fixture(SimpleNamespace(attempt_id='review-test', **args), attempt), records

    def test_inventory_covers_additional_native_kinds_and_their_removal_stops(self):
        for resource in ('leases.coordination.k8s.io', 'ingresses.networking.k8s.io', 'networkpolicies.networking.k8s.io',
                         'cronjobs.batch', 'controllerrevisions.apps', 'poddisruptionbudgets.policy',
                         'horizontalpodautoscalers.autoscaling', 'resourcequotas', 'limitranges'):
            self.assertIn(resource, Fixture.NATIVE_RESOURCES.split(','))
        lease = ks('coordination.k8s.io/v1', 'Lease', 'ks-controller-manager-leader-election', 'lease-1', 'kubesphere-system')
        with self.assertRaisesRegex(ValueError, 'unexpected'):
            assert_phase({'lease-1'}, [lease], [], set())

    def test_synthetic_hold_tolerates_transient_foreground_finalizer_only(self):
        held = lambda *finalizers, deleting=True: {'metadata': {'finalizers': list(finalizers),
                                                               **({'deletionTimestamp': 't'} if deleting else {})}}
        self.assertTrue(synthetic_hold_blocked(held(SYNTHETIC_HOLD)))
        self.assertTrue(synthetic_hold_blocked(held('foregroundDeletion', SYNTHETIC_HOLD)))
        for obj in (held(), held('foregroundDeletion'), held(SYNTHETIC_HOLD, deleting=False), held(SYNTHETIC_HOLD, 'other/hold')):
            self.assertFalse(synthetic_hold_blocked(obj))

    def test_tool_identities_digest_files_resolved_runtime_binaries_and_versions_without_running_kubectl(self):
        with tempfile.TemporaryDirectory() as temp:
            tools = {}
            for name in ('kubectl', 'helm', 'age'):
                tools[name] = Path(temp) / name;tools[name].write_bytes(name.encode())
            # docker on PATH is a symlink; its target's bytes are the identity.
            (Path(temp) / 'kind').write_bytes(b'kind binary');(Path(temp) / 'docker-real').write_bytes(b'docker binary')
            (Path(temp) / 'docker').symlink_to(Path(temp) / 'docker-real')
            calls = []
            def run(argv, **kwargs):
                calls.append(argv)
                return SimpleNamespace(returncode=0, stdout=(Path(argv[0]).name + ' version output').encode(), stderr=b'')
            with patch('rehearse.subprocess.run', side_effect=run), \
                 patch('rehearse.shutil.which', side_effect=lambda name: str(Path(temp) / name)):
                result = tool_identities(SimpleNamespace(**tools))
            self.assertEqual(calls, [[str(Path(temp) / 'kind'), 'version'], [str(Path(temp) / 'docker'), 'version']])
            with patch('rehearse.shutil.which', return_value=None), patch('rehearse.subprocess.run') as run:
                missing = tool_identities(SimpleNamespace(**tools))
            run.assert_not_called()
        self.assertEqual(result['toolSha256'], {n: digest(n.encode()) for n in tools})
        runtime = result['runtimeVersionOutput']
        self.assertEqual((runtime['kind']['binarySha256'], runtime['docker']['binarySha256']),
                         (digest(b'kind binary'), digest(b'docker binary')))
        self.assertEqual(runtime['kind']['outputSha256'], digest(b'kind version output'))
        self.assertEqual(runtime['docker']['outputSha256'], digest(b'docker version output'))
        self.assertFalse(result['kubectlExecuted']);self.assertFalse(result['releaseAuthenticityVerified'])
        self.assertEqual(missing['runtimeVersionOutput']['kind'], {'exitCode': None, 'outputSha256': None, 'binarySha256': None})

    def run_preflight(self, identities, operator='operator'):
        path = Mock();path.read_bytes.return_value = canonical({'sourceClusterUid': 'source-1', 'nativeEndpoint': 'https://192.168.1.30:6443'})
        args = SimpleNamespace(source_inventory=path, project_root='repo', evidence_root='private', attempt_id='unit', operator=operator)
        attempt = Mock(directory=Path('/private/unit'))
        fixture = Mock(last_step='not-started');fixture.cleanup.return_value = True
        with patch('rehearse.Attempt', return_value=attempt), patch('rehearse.Fixture', return_value=fixture), \
             patch('rehearse.tool_identities', return_value=identities):
            _, state = rehearse(args)
        return state, {c.args[0]: c.args[1] for c in attempt.record.call_args_list}, fixture

    def test_missing_runtime_tool_fails_closed_before_fixture_start(self):
        missing = {'exitCode': None, 'outputSha256': None, 'binarySha256': None}
        state, records, fixture = self.run_preflight({'runtimeVersionOutput': {'kind': missing, 'docker': {'exitCode': 0}}})
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(records['failure.json']['reasonCode'], 'runtime-version-preflight-failed')
        fixture.start.assert_not_called()

    def test_attempt_binds_imported_module_digests_and_sanitized_operator(self):
        state, records, _ = self.run_preflight({'runtimeVersionOutput': {'kind': {'exitCode': 1}}}, operator='Claude')
        here = Path(rehearse_module.__file__).resolve().parent
        self.assertEqual(records['attempt.json']['moduleSha256'],
                         {name: file_digest(here / name) for name in ('evidence.py', 'qualify.py')})
        self.assertEqual(records['attempt.json']['scriptSha256'], file_digest(here / 'rehearse.py'))
        self.assertEqual(records['attempt.json']['operator'], 'Claude')
        for unsafe in ('operator\nINJECTED', 'name with spaces', 'x' * 301, ''):
            with self.subTest(operator=unsafe):
                _, records, _ = self.run_preflight({'runtimeVersionOutput': {'kind': {'exitCode': 1}}}, operator=unsafe)
                self.assertIsNone(records['attempt.json']['operator'])

    def test_failed_runtime_version_preflight_is_recorded_and_fails_closed_before_fixture_start(self):
        path = Mock();path.read_bytes.return_value = canonical({'sourceClusterUid': 'source-1', 'nativeEndpoint': 'https://192.168.1.30:6443'})
        args = SimpleNamespace(source_inventory=path, project_root='repo', evidence_root='private', attempt_id='unit', operator='operator')
        attempt = Mock(directory=Path('/private/unit'))
        fixture = Mock(last_step='not-started');fixture.cleanup.return_value = True
        identities = {'runtimeVersionOutput': {'kind': {'exitCode': 0}, 'docker': {'exitCode': 1}}}
        with patch('rehearse.Attempt', return_value=attempt), patch('rehearse.Fixture', return_value=fixture), \
             patch('rehearse.tool_identities', return_value=identities):
            _, state = rehearse(args)
        records = {c.args[0]: c.args[1] for c in attempt.record.call_args_list}
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(records['tools.json'], identities)
        self.assertEqual(records['failure.json']['reasonCode'], 'runtime-version-preflight-failed')
        self.assertIn('unmeasured', records['failure.json']['sourceUnchanged'])
        fixture.start.assert_not_called();fixture.cleanup.assert_called_once()

    def test_main_refusal_covers_unexpected_errors(self):
        argv = ['rehearse.py', '--operator', 'unit', '--source-inventory', 'source.json', '--node-image', 'sha256:' + 'a' * 64,
                '--kubectl', 'kubectl', '--age', 'age', '--recipient', 'public-only', '--readback-recipient', 'second-public', '--readback-identity', 'readback-key']
        for error in (StopIteration(), ImportError('yaml'), RuntimeError('unexpected')):
            with self.subTest(error=type(error).__name__), patch('sys.argv', argv), \
                 patch('rehearse.rehearse', side_effect=error), patch('sys.stderr'):
                with self.assertRaises(SystemExit) as stopped:
                    rehearse_module.main()
                self.assertEqual(stopped.exception.code, 2)

    def test_tenant_sync_gate_records_false_before_raising(self):
        fixture, records = self.fixture()
        with patch.object(fixture, 'wait_tenant_sync', return_value=None):
            with self.assertRaisesRegex(ValueError, 'kubesphere-tenant-sync-not-ready'):
                fixture.record_runtime(['ks-apiserver'], [], True)
        self.assertFalse(records['kubesphere-runtime.json']['systemWorkspaceSynchronized'])
        self.assertIsNone(records['kubesphere-runtime.json']['tenantSyncSeconds'])
        fixture, records = self.fixture()
        with patch.object(fixture, 'wait_tenant_sync', return_value=3):
            fixture.record_runtime(['ks-apiserver'], [], True)
        self.assertTrue(records['kubesphere-runtime.json']['systemWorkspaceSynchronized'])

    def retire(self, settles, final):
        baseline = kubesphere_baseline()
        crd = {**ks('apiextensions.k8s.io/v1', 'CustomResourceDefinition', 'workspaces.tenant.kubesphere.io', 'crd-1'),
               'customResource': {'group': 'tenant.kubesphere.io', 'plural': 'workspaces', 'scope': 'Cluster',
                                  'versions': [{'name': 'v1beta1', 'served': True, 'storage': True}]}}
        fixture, records = self.fixture(ks_chart_sha256='c' * 64, source_digest='s' * 64, script_digest='p' * 64)
        with patch.object(fixture, 'probe_workspace_propagation'), patch.object(fixture, 'populate_retirement_decisions'), patch.object(fixture, 'crd_inventory', return_value=[crd]), \
             patch.object(fixture, 'settled_inventory', side_effect=[(baseline, settles[0]), (final(baseline), settles[1])]), \
             patch.object(fixture, 'discover', return_value={}), patch.object(fixture, 'retire_phase') as phases, \
             patch.object(fixture, 'post_retirement_checks', return_value={}) as health:
            try:
                fixture.retire_kubesphere()
                error = None
            except ValueError as raised:
                error = str(raised)
        return error, records, phases, health

    def test_retirement_settle_gates_and_final_exact_comparison_stop_before_result(self):
        retired = lambda b: [{**v, 'finalizers': [f for f in v['finalizers'] if f != SYSTEM_WORKSPACE_FINALIZER]} if v['kind'] == 'Namespace' else v for v in b if v['uid'] not in retirement_scope(b)]
        error, records, phases, health = self.retire((True, True), retired)
        self.assertIsNone(error)
        self.assertEqual(records['kubesphere-retirement-result.json']['state'], 'passed-dependency-first-native-retirement')
        error, records, phases, _ = self.retire((False, True), retired)
        self.assertEqual(error, 'fixture-state-did-not-settle-before-retirement')
        phases.assert_not_called();self.assertNotIn('kubesphere-retirement-allowlist.json', records)
        error, records, _, health = self.retire((True, False), retired)
        self.assertEqual(error, 'fixture-state-did-not-settle-after-retirement')
        self.assertNotIn('kubesphere-after-state.json', records);health.assert_not_called()
        # A baseline object outside the scope disappearing, or an expected removal remaining, fails the final comparison.
        for name, final in (('unexpected', lambda b: [v for v in retired(b) if v['uid'] != 'other']),
                            ('incomplete', lambda b: retired(b) + [v for v in b if v['uid'] == 'cm'])):
            with self.subTest(case=name):
                error, records, _, health = self.retire((True, True), final)
                self.assertEqual(error, 'unexpected-or-incomplete-deletion')
                self.assertIn('kubesphere-after-state.json', records)
                self.assertNotIn('kubesphere-retirement-result.json', records);health.assert_not_called()

    def test_changed_retained_crd_stops_before_any_result_record(self):
        baseline = kubesphere_baseline()
        scope = retirement_scope(baseline)
        final = [{**v, 'finalizers': [f for f in v['finalizers'] if f != SYSTEM_WORKSPACE_FINALIZER]} if v['kind'] == 'Namespace' else v for v in baseline if v['uid'] not in scope]
        crd = {**ks('apiextensions.k8s.io/v1', 'CustomResourceDefinition', 'workspaces.tenant.kubesphere.io', 'crd-1'),
               'customResource': {'group': 'tenant.kubesphere.io', 'plural': 'workspaces', 'scope': 'Cluster',
                                  'versions': [{'name': 'v1beta1', 'served': True, 'storage': True}]}}
        changed = {**crd, 'customResource': {**crd['customResource'], 'versions': [{'name': 'v1beta1', 'served': False, 'storage': True}]}}
        fixture, records = self.fixture(ks_chart_sha256='c' * 64, source_digest='s' * 64, script_digest='p' * 64)
        fixture.custom_resources = ['stale-cache']
        with patch.object(fixture, 'probe_workspace_propagation'), patch.object(fixture, 'populate_retirement_decisions'), \
             patch.object(fixture, 'crd_inventory', side_effect=[[crd], [changed]]), \
             patch.object(fixture, 'settled_inventory', side_effect=[(baseline, True), (final, True)]), \
             patch.object(fixture, 'discover', return_value={}), patch.object(fixture, 'retire_phase') as phases, \
             patch.object(fixture, 'post_retirement_checks') as health:
            with self.assertRaisesRegex(ValueError, 'retained-crd-changed'):
                fixture.retire_kubesphere()
        self.assertIsNone(fixture.custom_resources)
        self.assertEqual(phases.call_count, len(dependency_plan(baseline, scope)))
        health.assert_not_called()
        self.assertIn('kubesphere-after-state.json', records)
        self.assertNotIn('kubesphere-retirement-result.json', records)


class SyntheticExecuteTests(unittest.TestCase):
    """The standalone synthetic rehearsal: per-action revalidation, the named hold, final preservation and the canary."""
    def raw(self, state, mutate):
        meta = lambda name, uid, rv='1', **extra: {'name': name, 'uid': uid, 'resourceVersion': rv, **extra}
        owner = {'apiVersion': 'qualification.hexalith.io/v1', 'kind': 'Workspace',
                 'metadata': meta('s426-owner', 'owner', '3' if state['owner-deleting'] else '2', finalizers=[SYNTHETIC_HOLD])}
        if state['owner-deleting'] and mutate != 'hold':
            owner['metadata']['deletionTimestamp'] = '2026-10-03T09:00:00Z'
            # Foreground deletion adds GC's own finalizer; only the named hold may be removed by the replace.
            owner['metadata']['finalizers'] = ['foregroundDeletion', SYNTHETIC_HOLD]
        child = {'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': meta('s426-managed', 'child',
                 '9' if mutate == 'revalidation' and state['inventories'] == 2 else '1', namespace='s426-management',
                 ownerReferences=[{'apiVersion': 'qualification.hexalith.io/v1', 'kind': 'Workspace', 'name': 's426-owner', 'uid': 'owner'}])}
        claim = {'apiVersion': 'v1', 'kind': 'PersistentVolumeClaim', 'metadata': meta('s426-data', 'claim', namespace='s426-workload'),
                 'spec': {'volumeName': 'other-volume' if mutate == 'preservation' and state['owner-removed'] else 's426-data',
                          'storageClassName': ''}, 'status': {'phase': 'Bound'}}
        objects = [{'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': meta(name, name)} for name in ('s426-management', 's426-workload')]
        objects += [{'apiVersion': 'apps/v1', 'kind': 'Deployment', 'metadata': meta('s426-preserve', 'deploy', namespace='s426-workload'),
                     'spec': {'replicas': 0}},
                    {'apiVersion': 'v1', 'kind': 'PersistentVolume', 'metadata': meta('s426-data', 'volume'),
                     'spec': {'claimRef': {'name': 's426-data', 'namespace': 's426-workload', 'uid': 'claim'}}}, claim]
        objects += [] if state['child-deleted'] else [child]
        objects += [] if state['owner-removed'] else [owner]
        return objects, owner

    def execute(self, mutate=None):
        state = {'inventories': 0, 'child-deleted': False, 'owner-deleting': False, 'owner-removed': False}
        records, requests = {}, []
        attempt = SimpleNamespace(directory=Path('/nonexistent'), encrypt=Mock(), record=lambda n, v: records.update({n: v}))
        fixture = Fixture(SimpleNamespace(attempt_id='execute-test', ks_chart=None, source_digest='s' * 64,
                                          script_digest='p' * 64), attempt)
        def inventory():
            state['inventories'] += 1
            return [project_resource(v) for v in self.raw(state, mutate)[0]]
        def kube(name, *argv, obj=None, allowed=(0,)):
            requests.append(name)
            if name == 'uid-bound-native-delete':
                state['child-deleted' if obj['preconditions']['uid'] == 'child' else 'owner-deleting'] = True
            if name == 'remove-synthetic-finalizer':
                requests.append(obj['metadata']['finalizers'])
                state['owner-removed'] = True
            return SimpleNamespace(returncode=0, stdout=b'', stderr=b'')
        canary = b'changed' if mutate == 'canary' else b'synthetic-426-canary'
        with patch.object(fixture, 'populate'), patch.object(fixture, 'check_native_delete_preconditions'), \
             patch.object(fixture, 'inventory', side_effect=inventory), patch.object(fixture, 'kube', side_effect=kube), \
             patch.object(fixture, 'get', side_effect=lambda *a, **k: copy.deepcopy(self.raw(state, mutate)[1])), \
             patch.object(fixture, 'run', return_value=SimpleNamespace(returncode=0, stdout=canary, stderr=b'')):
            try:
                fixture.execute()
                error = None
            except ValueError as raised:
                error = str(raised)
        return error, records, requests

    def test_synthetic_rehearsal_passes_child_first_with_named_hold_only(self):
        error, records, requests = self.execute()
        self.assertIsNone(error)
        self.assertEqual(records['native-result.json']['state'], 'passed-limited-synthetic-native-rehearsal')
        self.assertEqual(requests.count('uid-bound-native-delete'), 2)
        self.assertEqual(requests[requests.index('remove-synthetic-finalizer') + 1], ['foregroundDeletion'])

    def test_revalidation_hold_preservation_and_canary_failures_stop_before_result(self):
        for mutate, reason, forbidden in [('revalidation', 'uid-or-resource-version-drift', 'uid-bound-native-delete'),
                                          ('hold', 'finalizer-behavior-unexpected', 'remove-synthetic-finalizer'),
                                          ('preservation', 'preserved-identity-or-binding-changed', None),
                                          ('canary', 'synthetic-data-changed', None)]:
            with self.subTest(mutate=mutate):
                error, records, requests = self.execute(mutate)
                self.assertEqual(error, reason)
                self.assertNotIn('native-result.json', records)
                if forbidden:
                    self.assertNotIn(forbidden, requests)


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

    def full_start(self, fixture, reachable=None, networks=None, internal=True):
        """Simulate every Docker/kind/native command of a complete start(); fence probes report blocked unless named."""
        counts, calls = {}, []
        networks = networks or [fixture.network]
        def command(argv, **kwargs):
            calls.append(argv)
            tag = argv[-1];counts[(tuple(argv[:3]), tag)] = counts.get((tuple(argv[:3]), tag), 0) + 1
            first = counts[(tuple(argv[:3]), tag)] == 1
            def ok(data=b''):
                return SimpleNamespace(returncode=0, stdout=data if isinstance(data, bytes) else json.dumps(data).encode(), stderr=b'')
            if argv[:3] == ['docker', 'container', 'inspect']:
                return SimpleNamespace(returncode=1, stdout=b'[]', stderr=f'Error: No such object: {tag}'.encode())
            if argv[:3] == ['docker', 'network', 'inspect']:
                if first:
                    return SimpleNamespace(returncode=1, stdout=b'[]',
                                           stderr=f'Error response from daemon: network {tag} not found'.encode())
                return ok([{'Internal': internal, 'IPAM': {'Config': [{'Subnet': '172.30.0.0/16', 'Gateway': '172.30.0.1'}]}}])
            if argv[:3] == ['docker', 'image', 'inspect']:
                if tag in (fixture.base_tag, fixture.derived_tag) and first:
                    return SimpleNamespace(returncode=1, stdout=b'[]', stderr=f'Error response from daemon: No such image: {tag}'.encode())
                return ok([{'Id': fixture.args.node_image if tag == fixture.args.node_image else 'sha256:' + 'b' * 64}])
            if argv[:2] == ['docker', 'run']:
                return ok(b"docker_host_ip=$(ip -4 route show default | cut -d' ' -f3)")
            if argv[:2] == ['docker', 'inspect']:
                return ok([{'Mounts': [{'Type': 'volume', 'Name': 'owned-kind-volume'}],
                            'Id': 'fresh-docker-id', 'NetworkSettings': {'Networks': {name: {'IPAddress': '172.30.0.2'} for name in networks}}}])
            if argv[:3] == ['kind', 'create', 'cluster']:
                return ok(b'created')
            if argv[:2] == ['docker', 'exec'] and 'bash' in argv:
                return ok(b'reachable\n' if reachable and f'/dev/tcp/{reachable}' in argv[-1] else b'blocked\n')
            if argv[:2] == ['docker', 'exec'] and 'view' in argv:
                return ok({'current-context': 'kubernetes-admin@kubernetes'})
            if argv[:2] == ['docker', 'exec'] and 'kube-system' in argv:
                return ok({'metadata': {'uid': 'fixture-cluster-uid'}})
            if argv[:2] == ['docker', 'exec'] and 'version' in argv:
                return ok({'serverVersion': {'gitVersion': 'v1.34.9'}})
            return ok()
        return command, calls

    def started(self, temp, endpoint='https://192.168.1.30:6443', **simulation):
        fixture, records = self.fixture(temp)
        fixture.args.source = {'nativeEndpoint': endpoint, 'sourceClusterUid': 'source-cluster-uid'}
        command, calls = self.full_start(fixture, **simulation)
        with patch('rehearse.subprocess.run', side_effect=command):
            try:
                fixture.start()
                error = None
            except ValueError as raised:
                error = str(raised)
        return error, records, [c for c in calls if 'bash' in c]

    def test_complete_start_records_isolation_only_after_every_fence_gate(self):
        with tempfile.TemporaryDirectory() as temp:
            error, records, probes = self.started(temp)
        self.assertIsNone(error)
        isolation = records['isolation.json']
        self.assertTrue(isolation['passed']);self.assertTrue(isolation['internalNetwork'])
        self.assertEqual(len(probes), 6)
        self.assertTrue(all(v['blocked'] for v in isolation['probes']))
        self.assertEqual(isolation['fixture']['clusterUid'], 'fixture-cluster-uid')
        self.assertEqual(isolation['nativeVersion'], 'v1.34.9')

    def test_reachable_source_or_unfenced_network_stops_before_isolation_record(self):
        for name, simulation, reason, probe_count in [
                ('reachable-source-api', {'reachable': '192.168.1.30/6443'}, 'source-isolation-unverified', 6),
                ('reachable-egress', {'reachable': '1.1.1.1/443'}, 'source-isolation-unverified', 6),
                ('not-internal', {'internal': False}, 'source-isolation-unverified', 6),
                ('second-network', {'networks': ['NETWORK', 'bridge']}, 'fixture-has-unfenced-network', 0)]:
            with self.subTest(case=name), tempfile.TemporaryDirectory() as temp:
                if 'networks' in simulation:
                    fixture, _ = self.fixture(temp)
                    simulation = {'networks': [fixture.network, 'bridge']}
                error, records, probes = self.started(temp, **simulation)
                self.assertEqual(error, reason)
                self.assertNotIn('isolation.json', records)
                self.assertEqual(len(probes), probe_count)

    def test_non_literal_source_endpoint_is_refused_before_any_fence_probe(self):
        for endpoint, host in (('https://api.example:6443', 'api.example'), ('https://192.168.1.30$(id):6443', '192.168.1.30$(id)')):
            with self.subTest(endpoint=endpoint), tempfile.TemporaryDirectory() as temp:
                error, records, probes = self.started(temp, endpoint=endpoint)
                # The literal-IP check refuses the host itself; an earlier startup failure cannot satisfy this.
                self.assertEqual(error, f'{host!r} does not appear to be an IPv4 or IPv6 address')
                self.assertEqual(probes, [])
                self.assertNotIn('isolation.json', records)

    def chart(self, directory, version='1.2.4'):
        import tarfile
        path = Path(directory) / 'ks-core-1.2.4.tgz'
        with tarfile.open(path, 'w:gz') as archive:
            for name, data in (('ks-core/Chart.yaml', f'version: {version}\nappVersion: v4.2.1\n'.encode()),
                               ('ks-core/scripts/post-delete.sh', b'#!/bin/sh\n'),
                               ('ks-core/charts/ks-crds/scripts/post-delete.sh', b'#!/bin/sh\n')):
                info = tarfile.TarInfo(name);info.size = len(data);archive.addfile(info, io.BytesIO(data))
        return path

    def install(self, temp, chart_sha256=None, version='1.2.4', unready=()):
        fixture, records = self.fixture(temp)
        chart = self.chart(temp, version)
        fixture.args.ks_chart, fixture.args.helm = chart, Path(temp) / 'helm'
        fixture.args.ks_chart_sha256 = chart_sha256 or file_digest(chart)
        def run(name, argv, input=None, timeout=120, allowed=(0,)):
            if name == 'local-vendor-image':
                return SimpleNamespace(returncode=0, stdout=json.dumps([{'Id': 'sha256:' + 'e' * 64}]).encode(), stderr=b'')
            return SimpleNamespace(returncode=0, stdout=b'', stderr=b'')
        def kube(name, *argv, obj=None, allowed=(0,)):
            failed = name == 'kubesphere-runtime-ready' and argv[2].split('/')[-1] in unready
            return SimpleNamespace(returncode=1 if failed else 0, stdout=b'{"gitVersion":"v4.2.1"}' if name == 'kubesphere-native-version-ready' else b'{}', stderr=b'')
        source = Mock();source.stderr.read.return_value = b'';source.wait.return_value = 0;source.poll.return_value = 0
        with patch.object(fixture, 'run', side_effect=run) as runs, patch.object(fixture, 'kube', side_effect=kube), \
             patch.object(fixture, 'diagnostics') as diagnostics, patch.object(fixture, 'record_runtime') as runtime, \
             patch('rehearse.subprocess.Popen', return_value=source), \
             patch('rehearse.subprocess.run', return_value=SimpleNamespace(returncode=0, stdout=b'', stderr=b'')):
            try:
                fixture.install_kubesphere()
                error = None
            except ValueError as raised:
                error = str(raised)
        return error, records, [c.args[0] for c in runs.call_args_list], diagnostics, runtime

    def test_chart_digest_and_version_are_refused_before_any_fixture_command(self):
        with tempfile.TemporaryDirectory() as temp:
            error, records, runs, _, runtime = self.install(temp, chart_sha256='0' * 64)
        self.assertEqual((error, runs, records), ('kubesphere-chart-digest-mismatch', [], {}))
        runtime.assert_not_called()
        with tempfile.TemporaryDirectory() as temp:
            error, records, runs, _, _ = self.install(temp, version='1.2.5')
        self.assertEqual((error, runs), ('unqualified-kubesphere-chart-version', []))

    def test_unready_core_controller_or_api_stops_before_runtime_record(self):
        for name in ('ks-apiserver', 'ks-controller-manager'):
            with self.subTest(deployment=name), tempfile.TemporaryDirectory() as temp:
                error, _, runs, _, runtime = self.install(temp, unready=(name,))
                self.assertEqual(error, 'core-controller-or-api-not-ready')
                self.assertIn('install-kubesphere', runs)
                runtime.assert_not_called()
        # An unready optional console only triggers diagnostics and is recorded as limited.
        with tempfile.TemporaryDirectory() as temp:
            error, _, _, diagnostics, runtime = self.install(temp, unready=('ks-console',))
        self.assertIsNone(error)
        diagnostics.assert_called_once()
        self.assertEqual(runtime.call_args.args[1], ['ks-console'])

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

    def test_inspected_node_image_id_must_equal_requested_id_before_docker_run(self):
        with tempfile.TemporaryDirectory() as temp:
            fixture, _ = self.fixture(temp)
            original = self.startup_commands(fixture)
            def command(argv, **kwargs):
                if argv == ['docker', 'image', 'inspect', fixture.args.node_image]:
                    # A different local image answers to the requested reference.
                    return SimpleNamespace(returncode=0, stdout=json.dumps([{'Id': 'sha256:' + 'c' * 64}]).encode(), stderr=b'')
                return original(argv, **kwargs)
            with patch('rehearse.subprocess.run', side_effect=command) as commands:
                with self.assertRaisesRegex(ValueError, 'node-image-must-be-local-sha256-identity'):fixture.start()
        self.assertFalse(any(call.args[0][:2] == ['docker', 'run'] for call in commands.call_args_list))
        self.assertFalse(fixture.entrypoint_reader_started);self.assertEqual(fixture.image_tags, [])

    def test_parse_args_refuses_a_node_image_that_is_not_a_sha256_id(self):
        argv = ['--operator', 'unit', '--source-inventory', 'source.json', '--kubectl', 'kubectl', '--age', 'age', '--recipient',
                'public-only', '--readback-recipient', 'second-public', '--readback-identity', 'readback-key']
        self.assertEqual(rehearse_module.parse_args(argv + ['--node-image', 'sha256:' + 'a' * 64]).node_image, 'sha256:' + 'a' * 64)
        for image in ('kindest/node:v1.34.0', 'kindest/node@sha256:' + 'a' * 64):
            with self.subTest(image=image), patch('sys.stderr', new_callable=io.StringIO) as stderr:
                with self.assertRaises(SystemExit) as stopped:
                    rehearse_module.parse_args(argv + ['--node-image', image])
                self.assertEqual(stopped.exception.code, 2)
                self.assertIn('exact local Docker image ID required', stderr.getvalue())

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
        with patch('rehearse.Attempt', return_value=attempt), patch('rehearse.Fixture', return_value=fixture), \
             patch('rehearse.tool_identities', return_value={'runtimeVersionOutput': {}}):
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
                     patch('rehearse.tool_identities', return_value={'runtimeVersionOutput': {}}), \
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
        import tarfile
        import yaml
        for error in (KeyError('private-native-value'), yaml.YAMLError('private-native-value'),
                      tarfile.TarError('private-native-value'), ImportError('private-native-value'), StopIteration()):
            with self.subTest(error=type(error).__name__):
                self.assert_failure_receipt_and_cleanup(error)

    def assert_failure_receipt_and_cleanup(self, error):
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
                 patch('rehearse.tool_identities', return_value={'runtimeVersionOutput': {}}), \
                 patch.object(fixture, 'execute', side_effect=error), \
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
