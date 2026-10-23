#!/usr/bin/env python3
"""Adversarial offline tests. All backend/provider/source data is synthetic."""

import base64
from contextlib import redirect_stdout
from copy import deepcopy
from dataclasses import replace
from datetime import datetime, timedelta, timezone
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


SOURCE = Path(__file__).with_name('recovery_adapters.py').absolute()
spec = importlib.util.spec_from_file_location('recovery_adapters_under_test', SOURCE)
adapter = importlib.util.module_from_spec(spec)
# dataclasses resolves annotations through this exact source module only.
import sys
sys.modules[spec.name] = adapter
spec.loader.exec_module(adapter)
a = adapter
NOW = datetime(2026, 10, 23, 12, 0, 0, tzinfo=timezone.utc)
STAMP = '2026-10-23T12:00:00Z'
H = 'a' * 64
J = 'b' * 64


def resource(kind, name, uid, rv='11', namespace=''):
    versions = {'Deployment': 'apps/v1', 'StatefulSet': 'apps/v1', 'ReplicaSet': 'apps/v1',
        'ControllerRevision': 'apps/v1', 'Job': 'batch/v1', 'CronJob': 'batch/v1',
        'Role': 'rbac.authorization.k8s.io/v1', 'RoleBinding': 'rbac.authorization.k8s.io/v1',
        'EndpointSlice': 'discovery.k8s.io/v1', 'VolumeSnapshot': 'snapshot.storage.k8s.io/v1',
        'VolumeSnapshotContent': 'snapshot.storage.k8s.io/v1', 'Lease': 'coordination.k8s.io/v1',
        'PodDisruptionBudget': 'policy/v1'}
    return {'apiVersion': versions.get(kind, 'v1'),
            'kind': kind, 'namespace': namespace, 'name': name, 'uid': uid, 'resourceVersion': rv}


def fixture(operation='capture', missing_tools=False, protected_watcher=False, unchanged_watcher=False,
            generated_census=False, distinct_clusters=False):
    source = resource('Namespace', 'source', 'source-namespace-uid')
    target = resource('Namespace', 'isolated-target', None if operation == 'capture' else 'target-namespace-uid',
                      None if operation == 'capture' else '21')
    workload = resource('Deployment', 'mcp', 'source-mcp-uid', namespace='source')
    store = resource('StatefulSet', 'store', 'source-store-uid', namespace='source')
    source_pvc = resource('PersistentVolumeClaim', 'source-data', 'source-pvc-uid', namespace='source')
    source_pv = resource('PersistentVolume', 'source-data', 'source-pv-uid')
    target_pvc = resource('PersistentVolumeClaim', 'target-data', 'target-pvc-uid', '22', 'isolated-target')
    target_pv = resource('PersistentVolume', 'target-data', 'target-pv-uid', '23')
    artifact = {'store': 'synthetic-provider', 'key': 'synthetic/policy.json', 'version': 'immutable-version-1',
                'sha256': H, 'size': 1, 'retainUntil': '2026-11-23T12:00:00Z'}
    tool = {'path': '/synthetic/exact/tool', 'sha256': H, 'size': 17}
    value = {'schemaVersion': 1, 'operation': operation, 'policyId': 'retained-policy-identity',
        'validFrom': '2026-10-23T11:00:00Z', 'expiresAt': '2026-10-24T11:00:00Z',
        'retainUntil': '2026-11-23T12:00:00Z', 'operatorPrincipal': 'synthetic-operator',
        'validatorPrincipal': 'synthetic-independent-validator', 'signerSha256': J,
        'retainedDecisionsSha256': H,
        'sourceCluster': {'uid': 'source-cluster-uid', 'endpoint': 'https://192.0.2.10:6443', 'caSha256': H},
        'targetCluster': {'uid': 'source-cluster-uid', 'endpoint': 'https://192.0.2.10:6443', 'caSha256': H},
        'sourceNamespace': source, 'targetNamespace': target,
        'protectedSources': [source, workload, store, source_pvc, source_pv],
        'ownedTargets': [] if operation == 'capture' else [target, target_pvc, target_pv],
        'pausedWorkload': workload, 'sourceStores': [store], 'previousReplicas': 2,
        'watcher': resource('Deployment', 'cnpg', 'source-watcher-uid', namespace='cnpg-system'),
        'watcherBefore': ['source', 'cnpg-system'], 'watcherDuring': ['source', 'cnpg-system', 'isolated-target'],
        'protectedPaths': [{'pvUid': 'source-pv-uid', 'nodeUid': 'source-node-uid',
                            'path': '/var/openebs/local/source-data'}],
        'targetPaths': [] if operation == 'capture' else [
            {'pvUid': 'target-pv-uid', 'nodeUid': 'node-uid', 'path': '/var/openebs/local/target-data'}],
        'creationFence': None if operation != 'cleanup' else {
            'resource': {'apiVersion': 'admissionregistration.k8s.io/v1',
                'kind': 'ValidatingAdmissionPolicyBinding', 'namespace': '', 'name': 'synthetic-creation-fence',
                'uid': 'external-fence-uid', 'resourceVersion': '31'},
            'namespaceUid': 'target-namespace-uid', 'qualificationSha256': J},
        'cleanupCensus': None if operation != 'cleanup' else [target, target_pvc, target_pv],
        'policy': dict(artifact), 'signature': dict(artifact, key='synthetic/policy.sig', version='immutable-signature-1'),
        'restoreInput': None if operation not in ('restore', 'resume') else
            dict(artifact, key='synthetic/restore.bin', version='immutable-recovery-1', sha256=a.digest(b'recovery'), size=8),
        'tools': {k: None if missing_tools else dict(tool) for k in
                  ('kubectl', 'artifactReader', 'signatureVerifier', 'stageRunner')}}
    if protected_watcher:
        value['protectedSources'].extend([
            resource('Namespace', 'cnpg-system', 'source-controller-namespace-uid'), value['watcher']])
    if unchanged_watcher:
        value['watcherDuring'] = list(value['watcherBefore'])
    if distinct_clusters:
        value['targetCluster'] = {'uid': 'target-cluster-uid', 'endpoint': 'https://192.0.2.20:7443',
                                  'caSha256': J}
    if generated_census:
        assert operation == 'cleanup'
        for kind in ('Pod', 'ConfigMap', 'ServiceAccount', 'ReplicaSet', 'ControllerRevision',
                     'Job', 'CronJob', 'Role', 'RoleBinding', 'Endpoints', 'EndpointSlice',
                     'VolumeSnapshot', 'VolumeSnapshotContent', 'Lease', 'PodDisruptionBudget'):
            name = 'generated-' + kind.lower()
            value['cleanupCensus'].append(resource(kind, name, name + '-uid',
                namespace='' if kind == 'VolumeSnapshotContent' else 'isolated-target'))
    initial = a.parse_binding(value)
    policy = a.canonical({'kind': 'hexalith.recovery-adapter-policy', 'schemaVersion': 1,
        'scopeSha256': initial.scope_sha256(), 'validFrom': value['validFrom'], 'expiresAt': value['expiresAt'],
        'retainedDecisionsSha256': H})
    signature = b'synthetic detached signature bytes'
    value['policy'].update(sha256=a.digest(policy), size=len(policy))
    value['signature'].update(sha256=a.digest(signature), size=len(signature))
    return value, a.parse_binding(value), policy, signature


def object_value(r, rv=None):
    value = {'apiVersion': r.api_version, 'kind': r.kind,
             'metadata': {'name': r.name, 'uid': r.uid, 'resourceVersion': r.resource_version if rv is None else rv}}
    if r.namespace:
        value['metadata']['namespace'] = r.namespace
    return value


def kubernetes_status(r, status=200, handler=False):
    """Synthetic identities in the native Kubernetes v0.34 response shapes.

    Upstream sources (not exact-dict schemas invented for this adapter):
    https://github.com/kubernetes/apimachinery/blob/v0.34.0/pkg/api/errors/errors.go#L133
    https://github.com/kubernetes/apiserver/blob/v0.34.0/pkg/endpoints/handlers/delete.go#L163
    https://github.com/kubernetes/apiserver/blob/v0.34.0/pkg/registry/generic/registry/store.go#L1287
    https://github.com/kubernetes/apimachinery/blob/v0.34.0/pkg/apis/meta/v1/types.go#L692
    """
    group = r.api_version.split('/')[0] if '/' in r.api_version else ''
    plural = a.KINDS[(r.api_version, r.kind)][1]
    value = {'apiVersion': 'v1', 'kind': 'Status', 'metadata': {},
             'status': 'Failure' if status == 404 else 'Success', 'details': {'name': r.name}}
    if status == 404:
        value.update(code=404, reason='NotFound', message=plural + ' "' + r.name + '" not found')
        value['details'].update(kind=plural)
    elif handler:
        value.update(code=status)
        value['details'].update(kind=r.kind)
    else:
        value['details'].update(kind=plural, uid=r.uid)
    if group:
        value['details']['group'] = group
    return value


class MemoryReceipts:
    def __init__(self):
        self.records = {}

    def create(self, key, raw):
        if key in self.records:
            raise FileExistsError('synthetic create-only refusal')
        self.records[key] = raw


class FakeBackend:
    def __init__(self, binding, policy, signature, mutation=None):
        self.binding = binding
        self.policy = policy
        self.signature = signature
        self.mutation = mutation
        self.requests = []

    def exchange(self, request, deadline):
        self.requests.append(request)
        expected = a.strict_json(request.expected)
        b, check = self.binding, expected['check']
        if check == 'immutable':
            raw = self.policy if request.name == 'policy-readback' else (
                self.signature if request.name == 'signature-readback' else b'recovery')
            ref = a.Artifact.parse(expected['artifact'])
            response = a.Response(0, 200, raw, ref.version, ref.retain_until)
        else:
            status = 200
            if check == 'authentication':
                value = {'authenticatedPolicySha256': b.policy.sha256,
                    'authenticatedSignatureSha256': b.signature.sha256, 'signerSha256': b.signer_sha256,
                    'scopeSha256': b.scope_sha256(), 'algorithm': 'approved-detached-signature'}
            elif check == 'absence':
                r = a.Resource.parse(expected['resource'], True)
                status = 404
                value = kubernetes_status(r, 404)
            elif check in ('identity', 'paused', 'replica-patch', 'reconciled', 'watcher-current', 'watcher-restored'):
                r = a.Resource.parse(expected['resource'])
                value = object_value(r, '99' if check in ('replica-patch', 'reconciled', 'watcher-restored') else None)
                if check in ('paused', 'replica-patch', 'reconciled'):
                    value['spec'] = {'replicas': 0 if check == 'paused' else b.previous_replicas}
                if check == 'reconciled':
                    value['metadata']['generation'] = 7
                    value['status'] = {'observedGeneration': 7, 'replicas': b.previous_replicas,
                        'updatedReplicas': b.previous_replicas, 'readyReplicas': b.previous_replicas,
                        'availableReplicas': b.previous_replicas}
                if check.startswith('watcher-'):
                    value['spec'] = {'template': {'spec': {'containers': [{'env': [
                        {'name': 'WATCH_NAMESPACE', 'value': ','.join(
                            b.watcher_during if check == 'watcher-current' else b.watcher_before)}]}]}}}
            elif check == 'deleted':
                r = a.Resource.parse(expected['resource'])
                value = kubernetes_status(r)
            elif check == 'creation-fence':
                value = {'creationFence': b.creation_fence.wire(), 'namespaceUid': b.target_namespace.uid,
                         'observation': 'creation-blocked', 'observedAt': STAMP}
            elif check == 'inventory':
                value = {'namespaceUid': b.target_namespace.uid, 'resources': [r.wire() for r in b.cleanup_census]}
            elif check == 'path-absence':
                value = {'volume': expected['volume'], 'observation': 'path-not-found',
                         'nodeUid': expected['volume']['node_uid']}
            elif check == 'isolation':
                value = {'targetUid': b.target_namespace.uid, 'sourceNamespaceUid': b.source_namespace.uid,
                    'networkMode': 'default-deny', 'hostMounts': [], 'sourceMounts': [], 'identity': 'isolated'}
            elif check in ('capture', 'restore', 'restore-verification'):
                value = {'outcome': {'capture': 'captured', 'restore': 'restored', 'restore-verification': 'verified'}[check],
                    'scopeSha256': b.scope_sha256(), 'sourceUids': [r.uid for r in b.protected_sources],
                    'targetUid': b.target_namespace.uid}
                if check == 'capture':
                    value['contentSha256'] = H
                else:
                    value['inputSha256'] = b.restore_input.sha256
            elif check == 'store-health':
                value = {'stores': [{'uid': s.uid, 'health': 'healthy', 'priorRuntimeId': 'old-store-process',
                    'runtimeId': 'new-store-process', 'restartObservation': 'restart-observed'} for s in b.source_stores]}
            elif check == 'drain':
                value = {'observedAt': STAMP, 'sourceNamespaceUid': b.source_namespace.uid,
                    'pending': 0, 'inFlight': 0, 'missing': 0, 'duplicates': 0,
                    'streamBeforeSha256': H, 'streamAfterSha256': H}
            else:
                raise AssertionError('unhandled synthetic check')
            response = a.Response(0, status, a.canonical(value))
        if self.mutation:
            response = self.mutation(request, response)
        return response


def mutate_json(response, mutate):
    value = a.strict_json(response.body)
    mutate(value)
    return replace(response, body=a.canonical(value))


class RecoveryTests(unittest.TestCase):
    def perform(self, operation='capture', mutation=None, receipts=None, now=lambda: NOW, monotonic=lambda: 0):
        value, b, policy, signature = fixture(operation)
        backend = FakeBackend(b, policy, signature, mutation)
        receipts = MemoryReceipts() if receipts is None else receipts
        result = a.run(b, backend, receipts, now=now, monotonic=monotonic)
        return result, backend, receipts

    def refused_input(self, mutate, operation='capture'):
        value, _, _, _ = fixture(operation)
        mutate(value)
        with self.assertRaisesRegex(a.Refusal, '^' + a.FAILURE + '$'):
            a.parse_binding(value)

    def assert_stop(self, operation, name, mutation):
        def alter(request, response):
            return mutation(response) if request.name == name else response
        result, backend, receipts = self.perform(operation, alter)
        self.assertEqual(result.failure, a.FAILURE)
        self.assertEqual(backend.requests[-1].name, name)
        self.assertIsNone(result.operational_qualification)
        self.assertTrue(any(k.endswith('-stopped') for k in receipts.records))
        return result, backend, receipts

    def test_all_operations_complete_synthetic_protocol_only(self):
        for operation in ('capture', 'restore', 'resume', 'cleanup'):
            with self.subTest(operation=operation):
                result, backend, receipts = self.perform(operation)
                self.assertIsNone(result.failure)
                self.assertEqual(result.state, operation + '-primitive-complete-unqualified')
                self.assertEqual(result.requests, len(backend.requests))
                self.assertTrue(any(k.endswith('-complete') for k in receipts.records))
                for field in ('operational_qualification', 'recovery_acceptance', 'source_resumed', 'target_cleanup_complete'):
                    self.assertIsNone(getattr(result, field))

    def test_offline_plan_is_deterministic_and_never_calls_backend(self):
        _, b, _, _ = fixture('restore', missing_tools=True)
        with patch('socket.socket', side_effect=AssertionError('socket forbidden')), \
             patch('subprocess.Popen', side_effect=AssertionError('subprocess forbidden')):
            first = a.private_plan(b)
            self.assertEqual(a.canonical(first), a.canonical(a.private_plan(b)))
            self.assertEqual(first['operationalGate'], 'closed')
            self.assertEqual(first['missingToolBindings'], sorted(k for k, _ in b.tools))
            self.assertTrue(all(r['argv'] is None for r in first['requests']))

    def test_null_driver_cannot_reach_injected_backend(self):
        _, b, policy, signature = fixture(missing_tools=True)
        backend = FakeBackend(b, policy, signature)
        with self.assertRaises(a.Refusal):
            a.run(b, backend, MemoryReceipts(), now=lambda: NOW)
        self.assertEqual(backend.requests, [])

    def test_capture_requires_proposed_target_absence_and_not_fabricated_uid(self):
        self.refused_input(lambda v: v['targetNamespace'].update(uid='invented-uid', resourceVersion='1'))
        _, b, _, _ = fixture()
        request = next(r for r in a.build_requests(b) if r.name == 'proposed-target-absent')
        self.assertEqual(request.method, 'GET')
        self.assertEqual(request.path, '/api/v1/namespaces/isolated-target')
        self.assert_stop('capture', 'proposed-target-absent', lambda r: replace(r, status=200))

    def test_restore_requires_recorded_created_target_uid(self):
        self.refused_input(lambda v: v['targetNamespace'].update(uid=None, resourceVersion=None), 'restore')

    def test_expired_future_and_24_hour_overrun_refuse_before_backend(self):
        _, b, policy, signature = fixture()
        for now in (NOW - timedelta(days=1), NOW + timedelta(days=1)):
            backend = FakeBackend(b, policy, signature)
            with self.assertRaises(a.Refusal):
                a.run(b, backend, MemoryReceipts(), now=lambda: now)
            self.assertEqual(backend.requests, [])
        self.refused_input(lambda v: v.update(expiresAt='2026-10-25T11:00:00Z'))

    def test_duplicate_json_float_nan_and_invalid_encoding_refuse(self):
        for raw in (b'{"x":1,"x":2}', b'{"x":1.1}', b'{"x":NaN}', b'\xff', b'{'):
            with self.subTest(raw=raw), self.assertRaises(a.Refusal):
                a.strict_json(raw)

    def test_bad_schema_type_bool_integer_unhashable_and_unknown_field_refuse(self):
        for mutate in (lambda v: v.update(extra='unexpected'), lambda v: v.update(previousReplicas=True),
                       lambda v: v['sourceNamespace'].update(kind=[]), lambda v: v.update(schemaVersion=True),
                       lambda v: v['policy'].update(size=False)):
            self.refused_input(mutate)

    def test_alias_version_key_and_resource_refuse(self):
        for alias in ('latest', 'CURRENT', 'default', 'head'):
            self.refused_input(lambda v: v['policy'].update(version=alias))
        self.refused_input(lambda v: v['policy'].update(key='prefix/latest/policy'))
        self.refused_input(lambda v: v['sourceNamespace'].update(uid='latest'))

    def test_path_traversal_dns_plain_http_and_endpoint_alias_refuse(self):
        for mutate in (lambda v: v['policy'].update(key='../escape'),
                       lambda v: v['tools']['stageRunner'].update(path='/tool/../historical'),
                       lambda v: v['sourceCluster'].update(endpoint='https://alias.example:6443'),
                       lambda v: v['sourceCluster'].update(endpoint='http://192.0.2.10:6443'),
                       lambda v: v['targetCluster'].update(uid='different-cluster-same-endpoint')):
            self.refused_input(mutate)

    def test_principals_must_be_distinct(self):
        self.refused_input(lambda v: v.update(validatorPrincipal=v['operatorPrincipal']))

    def test_source_target_name_uid_and_path_collision_refuse(self):
        self.refused_input(lambda v: v['targetNamespace'].update(name='source'))
        self.refused_input(lambda v: v['ownedTargets'][1].update(uid='source-pvc-uid'), 'cleanup')
        self.refused_input(lambda v: v['targetPaths'][0].update(path='/var/openebs/local/source-data'), 'cleanup')

    def test_duplicate_owned_and_unbound_storage_refuse(self):
        self.refused_input(lambda v: v['ownedTargets'].append(deepcopy(v['ownedTargets'][1])), 'cleanup')
        self.refused_input(lambda v: v.update(targetPaths=[]), 'cleanup')
        self.refused_input(lambda v: v['targetPaths'][0].update(nodeUid=''), 'cleanup')

    def test_source_protection_inventory_must_include_namespace_workload_and_stores(self):
        for index in (0, 1, 2):
            self.refused_input(lambda v: v['protectedSources'].pop(index))

    def test_policy_and_signature_exact_readback_precede_any_stage(self):
        result, backend, _ = self.perform('restore')
        self.assertIsNone(result.failure)
        self.assertEqual([r.name for r in backend.requests[:3]],
                         ['policy-readback', 'signature-readback', 'authenticate-policy'])
        for r in backend.requests:
            if r.role == 'artifactReader':
                self.assertEqual(r.principal, backend.binding.validator)
                self.assertNotEqual(r.principal, backend.binding.operator)
        self.assertLess([r.name for r in backend.requests].index('recovery-input-readback'),
                        [r.name for r in backend.requests].index('restore'))

    def test_authentication_consumes_the_exact_independently_verified_readback_bytes(self):
        _, b, policy, signature = fixture()
        planned = next(r for r in a.build_requests(b) if r.name == 'authenticate-policy')
        self.assertEqual(planned.stdin_materialization, 'exact-policy-and-signature-readback-bytes')
        self.assertIsNone(a.strict_json(planned.stdin)['exactReadbackBytes'])
        _, backend, _ = self.perform()
        actual = next(r for r in backend.requests if r.name == 'authenticate-policy')
        materialized = a.strict_json(actual.stdin)['exactReadbackBytes']
        self.assertEqual(base64.b64decode(materialized['policy'], validate=True), policy)
        self.assertEqual(base64.b64decode(materialized['signature'], validate=True), signature)
        self.assertIsNone(actual.stdin_materialization)
        self.assertNotEqual(actual.sha256(), planned.sha256())

    def test_policy_byte_hash_version_retention_and_scope_drift_stop(self):
        for mutation in (lambda r: replace(r, body=r.body + b' '), lambda r: replace(r, version='different-version'),
                         lambda r: replace(r, retain_until='2026-10-24T12:00:00Z')):
            self.assert_stop('capture', 'policy-readback', mutation)
        _, b, policy, signature = fixture()
        changed = replace(b, previous_replicas=3)
        backend = FakeBackend(changed, policy, signature)
        result = a.run(changed, backend, MemoryReceipts(), now=lambda: NOW)
        self.assertEqual(result.failure, a.FAILURE)
        self.assertEqual(len(backend.requests), 1)

    def test_signature_hash_and_authenticated_signer_drift_stop(self):
        self.assert_stop('capture', 'signature-readback', lambda r: replace(r, body=b'wrong'))
        self.assert_stop('capture', 'authenticate-policy', lambda r:
            mutate_json(r, lambda v: v.update(signerSha256=H)))

    def test_self_reported_authentication_boolean_is_not_accepted(self):
        self.assert_stop('capture', 'authenticate-policy', lambda r:
            replace(r, body=a.canonical({'authenticated': True, 'approved': True})))

    def test_immutable_restore_input_corruption_stops_before_restore(self):
        self.assert_stop('restore', 'recovery-input-readback', lambda r: replace(r, body=b'corrupt!'))

    def test_identity_uid_and_resource_version_drift_stop(self):
        for field, value in (('uid', 'wrong-source-uid'), ('resourceVersion', '99')):
            self.assert_stop('capture', 'protect-source-0', lambda r:
                mutate_json(r, lambda v: v['metadata'].update({field: value})))

    def test_process_success_does_not_override_semantic_failure(self):
        self.assert_stop('capture', 'capture', lambda r: mutate_json(r, lambda v: v.update(outcome='unknown')))
        self.assert_stop('restore', 'restore', lambda r: mutate_json(r, lambda v: v.update(outcome='failed')))
        self.assert_stop('restore', 'verify-restore', lambda r: mutate_json(r, lambda v: v.update(outcome='unknown')))

    def test_nonzero_process_status_and_provider_error_stop(self):
        self.assert_stop('capture', 'capture', lambda r: replace(r, exit_code=1))
        self.assert_stop('restore', 'recovery-input-readback', lambda r: replace(r, status=403))

    def test_restore_isolation_source_mount_host_mount_and_network_failures_stop(self):
        for field, value in (('hostMounts', ['/host']), ('sourceMounts', ['source-pvc-uid']),
                             ('networkMode', 'allow-all'), ('targetUid', 'unknown-target')):
            self.assert_stop('restore', 'verify-isolation', lambda r:
                mutate_json(r, lambda v: v.update({field: value})))

    def test_capture_source_uid_drift_in_outcome_stops(self):
        self.assert_stop('capture', 'capture', lambda r: mutate_json(r, lambda v: v.update(sourceUids=[])))

    def test_resume_restore_failure_leaves_pause_without_patch(self):
        result, backend, _ = self.assert_stop('resume', 'verify-restore', lambda r:
            mutate_json(r, lambda v: v.update(outcome='failed')))
        self.assertEqual(result.state, 'stopped-pause-reconciliation-required')
        self.assertFalse(any(r.method == 'PATCH' for r in backend.requests))

    def test_resume_unhealthy_or_unrestarted_stores_leave_pause(self):
        for change in ({'health': 'unhealthy'}, {'runtimeId': 'old-store-process'},
                       {'restartObservation': 'unknown'}):
            self.assert_stop('resume', 'verify-restarted-stores', lambda r:
                mutate_json(r, lambda v: v['stores'][0].update(change)))

    def test_resume_dirty_stale_future_or_changed_stream_drain_leaves_pause(self):
        for field, value in (('pending', 1), ('inFlight', 1), ('missing', 1), ('duplicates', 1),
                             ('pending', False), ('observedAt', '2026-10-23T11:59:29Z'),
                             ('observedAt', '2026-10-23T12:00:01Z'), ('streamAfterSha256', J)):
            self.assert_stop('resume', 'fresh-clean-drain', lambda r:
                mutate_json(r, lambda v: v.update({field: value})))

    def test_drain_that_ages_before_patch_is_rechecked(self):
        current = [NOW]
        def mutation(request, response):
            if request.name == 'paused-workload':
                current[0] += timedelta(seconds=31)
            return response
        result, backend, _ = self.perform('resume', mutation, now=lambda: current[0])
        self.assertEqual(result.failure, a.FAILURE)
        self.assertEqual(backend.requests[-1].name, 'paused-workload')
        self.assertFalse(any(r.name == 'resume-replicas' for r in backend.requests))

    def test_resume_only_patches_recorded_identity_zero_and_prior_replicas(self):
        _, backend, _ = self.perform('resume')
        request = next(r for r in backend.requests if r.name == 'resume-replicas')
        body = a.strict_json(request.stdin)
        self.assertEqual(request.method, 'PATCH')
        self.assertEqual(request.content_type, 'application/json-patch+json')
        self.assertEqual(body[0], {'op': 'test', 'path': '/metadata/uid', 'value': 'source-mcp-uid'})
        self.assertEqual(body[1], {'op': 'test', 'path': '/metadata/resourceVersion', 'value': '11'})
        self.assertEqual(body[2]['value'], 0)
        self.assertEqual(body[3]['value'], 2)
        self.assertIsNone(request.argv)
        self.assertEqual(request.environment, (('LANG', 'C.UTF-8'),))
        self.assertEqual(request.credential_fd, 3)

    def test_resume_already_running_workload_refuses_patch(self):
        self.assert_stop('resume', 'paused-workload', lambda r:
            mutate_json(r, lambda v: v['spec'].update(replicas=1)))

    def test_resume_requires_actual_reconciliation_after_patch(self):
        result, backend, _ = self.assert_stop('resume', 'reconcile-resume', lambda r:
            mutate_json(r, lambda v: v['status'].update(readyReplicas=0)))
        self.assertTrue(any(r.name == 'resume-replicas' for r in backend.requests))
        self.assertEqual(result.state, 'stopped-pause-reconciliation-required')
        self.assertIsNone(result.source_resumed)

    def test_cleanup_inventory_unknown_object_prevents_all_deletes(self):
        result, backend, _ = self.assert_stop('cleanup', 'owned-target-inventory', lambda r:
            mutate_json(r, lambda v: v['resources'].append(resource('Secret', 'unknown', 'unknown-uid', namespace='isolated-target'))))
        self.assertEqual(result.state, 'stopped-cleanup-incomplete')
        self.assertFalse(any(r.method == 'DELETE' for r in backend.requests))

    def test_cleanup_deletes_only_owned_uids_rv_and_namespace_last(self):
        result, backend, _ = self.perform('cleanup')
        self.assertIsNone(result.failure)
        deletes = [r for r in backend.requests if r.method == 'DELETE']
        self.assertEqual(len(deletes), len(backend.binding.owned_targets))
        self.assertTrue(deletes[-1].path.endswith('/namespaces/isolated-target'))
        for request in deletes:
            body = a.strict_json(request.stdin)
            target = next(r for r in backend.binding.owned_targets if r.path() == request.path)
            self.assertEqual(body['preconditions'], {'uid': target.uid, 'resourceVersion': target.resource_version})
            self.assertNotIn(target.uid, {r.uid for r in backend.binding.protected_sources})

    def test_cleanup_403_timeout_and_remaining_resource_are_not_absence(self):
        for mutation in (lambda r: replace(r, status=403), lambda r: replace(r, status=504),
                         lambda r: replace(r, status=200), lambda r: mutate_json(r, lambda v: v.update(reason='Forbidden'))):
            self.assert_stop('cleanup', 'independent-absence-0', mutation)

    def test_cleanup_remaining_path_and_wrong_node_are_not_absence(self):
        for change in ({'observation': 'path-present'}, {'nodeUid': 'wrong-node'}):
            self.assert_stop('cleanup', 'independent-path-absence-0', lambda r:
                mutate_json(r, lambda v: v.update(change)))

    def test_cleanup_watcher_restoration_tests_exact_uid_rv_and_original_scope(self):
        _, backend, _ = self.perform('cleanup')
        request = next(r for r in backend.requests if r.name == 'restore-watcher')
        body = a.strict_json(request.stdin)
        self.assertEqual(body[0]['value'], backend.binding.watcher.uid)
        self.assertEqual(body[1]['value'], backend.binding.watcher.resource_version)
        self.assertEqual(body[-2]['value'], ','.join(backend.binding.watcher_during))
        self.assertEqual(body[-1]['value'], ','.join(backend.binding.watcher_before))
        self.assertTrue(any(r.name == 'independent-watcher-readback' for r in backend.requests))

    def test_cleanup_watcher_wrong_scope_or_unconfirmed_restoration_stops(self):
        for name in ('watcher-before-restoration', 'independent-watcher-readback'):
            self.assert_stop('cleanup', name, lambda r: mutate_json(r,
                lambda v: v['spec']['template']['spec']['containers'][0]['env'][0].update(value='wrong')))

    def test_cleanup_delete_success_without_matching_uid_stops(self):
        self.assert_stop('cleanup', 'delete-owned-0', lambda r:
            mutate_json(r, lambda v: v['details'].update(uid='source-pvc-uid')))

    def test_partial_failure_retains_receipts_without_automatic_cleanup_resume_or_retry(self):
        result, backend, receipts = self.assert_stop('cleanup', 'delete-owned-1', lambda r: replace(r, status=403))
        self.assertTrue(any(r.name == 'delete-owned-0' for r in backend.requests))
        self.assertFalse(any(r.name == 'restore-watcher' for r in backend.requests))
        before = dict(receipts.records)
        # Reusing identical input refuses the create-only run intent before I/O.
        repeated = a.run(backend.binding, backend, receipts, now=lambda: NOW)
        self.assertEqual(repeated.requests, 0)
        self.assertEqual(receipts.records, before)
        self.assertIsNone(result.target_cleanup_complete)

    def test_multipart_failure_does_not_issue_abort_or_delete_or_claim_cleanup(self):
        def mutation(request, response):
            if request.name == 'capture':
                raise PermissionError('synthetic provider multipart details SECRET')
            return response
        result, backend, receipts = self.perform('capture', mutation)
        self.assertEqual(result.failure, a.FAILURE)
        self.assertEqual(backend.requests[-1].name, 'capture')
        self.assertFalse(any('abort' in r.name or r.method == 'DELETE' for r in backend.requests))
        self.assertNotIn(b'SECRET', b''.join(receipts.records.values()))
        self.assertIsNone(result.target_cleanup_complete)

    def test_deadline_failure_stops_without_retry(self):
        ticks = iter([0, 16])
        result, backend, _ = self.perform(monotonic=lambda: next(ticks))
        self.assertEqual(result.failure, a.FAILURE)
        self.assertEqual(len(backend.requests), 1)

    def test_response_overflow_malformed_json_and_raw_error_are_sanitized(self):
        for mutation in (lambda r: replace(r, body=b'x' * (a.MAX_JSON + 1)),
                         lambda r: replace(r, body=b'{malformed SECRET}'),
                         lambda r: replace(r, body=b'{"authenticated":true,"authenticated":true}')):
            _, _, receipts = self.assert_stop('capture', 'authenticate-policy', mutation)
            self.assertNotIn(b'SECRET', b''.join(receipts.records.values()))

    def test_receipt_failure_prevents_operation(self):
        class BrokenReceipts:
            def create(self, key, raw):
                raise OSError('SECRET disk diagnostics')
        result, backend, _ = self.perform(receipts=BrokenReceipts())
        self.assertEqual(result.failure, a.FAILURE)
        self.assertEqual(backend.requests, [])

    def test_direct_dataclass_mutation_is_revalidated(self):
        _, b, policy, signature = fixture()
        for changed in (replace(b, previous_replicas=True), replace(b, validator=b.operator),
                        replace(b, protected_sources=()), replace(b, tools=())):
            with self.assertRaises(a.Refusal):
                a.run(changed, FakeBackend(changed, policy, signature), MemoryReceipts(), now=lambda: NOW)

    def test_source_volume_mapping_is_typed_complete_and_one_to_one(self):
        for mutate in (lambda v: v.update(protectedPaths=[]),
                       lambda v: v.update(protectedPaths=['/var/openebs/local/source-data']),
                       lambda v: v['protectedPaths'].append(deepcopy(v['protectedPaths'][0])),
                       lambda v: v['protectedPaths'][0].update(pvUid='unprotected-pv-uid'),
                       lambda v: v['protectedPaths'][0].update(nodeUid='')):
            with self.subTest(mutate=mutate):
                self.refused_input(mutate, 'cleanup')
        self.refused_input(lambda v: v['protectedPaths'].append(dict(v['protectedPaths'][0],
            path='/var/openebs/local/second-path')), 'cleanup')

    def test_target_volume_equal_ancestor_and_descendant_source_paths_refuse(self):
        for source_path, target_path in (
                ('/var/openebs/local/source-data', '/var/openebs/local/source-data'),
                ('/var/openebs/local/source-data/child', '/var/openebs/local/source-data'),
                ('/var/openebs/local/source-data', '/var/openebs/local/source-data/child')):
            def mutate(v):
                v['protectedPaths'][0]['path'] = source_path
                v['targetPaths'][0]['path'] = target_path
            self.refused_input(mutate, 'cleanup')
        value, _, _, _ = fixture('cleanup')
        value['targetPaths'][0]['path'] = '/var/openebs/local/source-data-sibling'
        a.parse_binding(value)

    def test_dependent_stages_carry_exact_bound_source_volume_records(self):
        for operation in ('capture', 'restore', 'resume', 'cleanup'):
            _, b, _, _ = fixture(operation)
            expected = [{'pv_uid': 'source-pv-uid', 'node_uid': 'source-node-uid',
                         'path': '/var/openebs/local/source-data'}]
            stages = [r for r in a.build_requests(b) if r.role == 'stageRunner']
            self.assertTrue(stages)
            self.assertTrue(all(a.strict_json(r.stdin)['protectedPaths'] == expected for r in stages))

    def test_source_stores_reject_namespace_duplicate_and_unrelated_resources(self):
        self.refused_input(lambda v: v.update(sourceStores=[v['sourceNamespace']]))
        self.refused_input(lambda v: v['sourceStores'].append(deepcopy(v['sourceStores'][0])))
        def unrelated(v):
            other = resource('Secret', 'credentials', 'source-secret-uid', namespace='source')
            v['protectedSources'].append(other)
            v['sourceStores'] = [other]
        self.refused_input(unrelated)
        def wrong_namespace(v):
            other_ns = resource('Namespace', 'other', 'other-namespace-uid')
            other = resource('StatefulSet', 'store', 'other-store-uid', namespace='other')
            v['protectedSources'].extend([other_ns, other])
            v['sourceStores'] = [other]
        self.refused_input(wrong_namespace)
        for kind, version in (('Deployment', 'apps/v1'), ('StatefulSet', 'apps/v1'),
                              ('Cluster', 'postgresql.cnpg.io/v1')):
            value, _, _, _ = fixture()
            value['sourceStores'][0].update(kind=kind, apiVersion=version)
            a.parse_binding(value)

    def test_owned_target_cannot_alias_watcher_uid_or_path_at_different_version(self):
        for alias in ('uid-and-path', 'uid', 'path'):
            def mutate(v):
                v['watcher']['namespace'] = 'isolated-target'
                owned = dict(v['watcher'], resourceVersion='999')
                if alias == 'uid':
                    owned['name'] = 'different-name'
                elif alias == 'path':
                    owned['uid'] = 'different-uid'
                v['ownedTargets'].append(owned)
            self.refused_input(mutate, 'cleanup')

    def test_noncanonical_numeric_ports_refuse_before_endpoint_identity_comparison(self):
        for port in ('06443', '00080', '00001'):
            self.refused_input(lambda v: v['targetCluster'].update(
                uid='different-cluster', endpoint='https://192.0.2.10:' + port))

    def test_cleanup_requires_external_fence_binding_and_noncleanup_requires_null(self):
        for mutate in (lambda v: v.update(creationFence=None),
                       lambda v: v.pop('creationFence'),
                       lambda v: v['creationFence'].update(namespaceUid='wrong-namespace-uid'),
                       lambda v: v['creationFence'].update(qualificationSha256=''),
                       lambda v: v['creationFence']['resource'].update(uid=None, resourceVersion=None),
                       lambda v: v['creationFence']['resource'].update(apiVersion='networking.k8s.io/v1',
                           kind='NetworkPolicy', namespace='isolated-target')):
            self.refused_input(mutate, 'cleanup')
        cleanup, _, _, _ = fixture('cleanup')
        for operation in ('capture', 'restore', 'resume'):
            self.refused_input(lambda v: v.update(creationFence=cleanup['creationFence']), operation)

    def test_creation_fence_and_other_cluster_controls_cannot_be_owned_targets(self):
        for name, uid in (('synthetic-creation-fence', 'external-fence-uid'),
                          ('other-admission-binding', 'other-admission-binding-uid')):
            self.refused_input(lambda v: v['ownedTargets'].append(dict(
                v['creationFence']['resource'], name=name, uid=uid)), 'cleanup')
        def alias_uid(v):
            v['ownedTargets'][2]['uid'] = v['creationFence']['resource']['uid']
            v['targetPaths'][0]['pvUid'] = v['creationFence']['resource']['uid']
        self.refused_input(alias_uid, 'cleanup')

    def test_cleanup_fence_observations_immediately_precede_inventory_and_namespace_delete(self):
        result, backend, _ = self.perform('cleanup')
        self.assertIsNone(result.failure)
        requests = backend.requests
        inventory = next(i for i, r in enumerate(requests) if r.name == 'owned-target-inventory')
        namespace = next(i for i, r in enumerate(requests) if r.method == 'DELETE' and
                         r.path == backend.binding.target_namespace.path())
        self.assertEqual(requests[inventory - 1].name, 'verify-creation-fence-before-inventory')
        self.assertEqual(requests[namespace - 1].name, 'verify-creation-fence-before-namespace-delete')
        for index in (inventory - 1, namespace - 1):
            self.assertEqual(a.strict_json(requests[index].stdin)['creationFence'],
                             backend.binding.creation_fence.wire())

    def test_failed_unknown_stale_or_changed_fence_stops_before_destructive_request(self):
        for name in ('verify-creation-fence-before-inventory', 'verify-creation-fence-before-namespace-delete'):
            for mutation in (lambda r: replace(r, status=403), lambda r: replace(r, status=504),
                    lambda r: mutate_json(r, lambda v: v.update(observation='unknown')),
                    lambda r: mutate_json(r, lambda v: v.update(observedAt='2026-10-23T11:59:29Z')),
                    lambda r: mutate_json(r, lambda v: v.update(namespaceUid='wrong-namespace-uid')),
                    lambda r: mutate_json(r, lambda v: v['creationFence']['resource'].update(uid='changed-uid')),
                    lambda r: mutate_json(r, lambda v: v['creationFence']['resource'].update(resourceVersion='32')),
                    lambda r: mutate_json(r, lambda v: v['creationFence'].update(qualificationSha256=H))):
                _, backend, _ = self.assert_stop('cleanup', name, mutation)
                if name.endswith('inventory'):
                    self.assertFalse(any(r.method == 'DELETE' for r in backend.requests))
                self.assertFalse(any(r.method == 'DELETE' and r.path == backend.binding.target_namespace.path()
                                     for r in backend.requests))

    def test_slow_request_intent_cannot_dispatch_after_policy_expiry(self):
        for operation, request_name in (('capture', 'capture'), ('restore', 'restore'),
                                         ('cleanup', 'delete-owned-0')):
            value, b, policy, signature = fixture(operation)
            current = [NOW]
            index = next(i for i, r in enumerate(a.build_requests(b)) if r.name == request_name)
            class SlowReceipts(MemoryReceipts):
                def create(self, key, raw):
                    super().create(key, raw)
                    if key.endswith('-' + str(index).zfill(3) + '-intent'):
                        current[0] = a.timestamp(b.expires_at)
            receipts = SlowReceipts()
            backend = FakeBackend(b, policy, signature)
            result = a.run(b, backend, receipts, now=lambda: current[0])
            self.assertEqual(result.failure, a.FAILURE)
            self.assertNotIn(request_name, [r.name for r in backend.requests])
            self.assertTrue(any(k.endswith('-' + str(index).zfill(3) + '-intent') for k in receipts.records))
            self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_slow_resume_intent_cannot_dispatch_after_clean_drain_ages(self):
        _, b, policy, signature = fixture('resume')
        current = [NOW]
        index = next(i for i, r in enumerate(a.build_requests(b)) if r.name == 'resume-replicas')
        class SlowReceipts(MemoryReceipts):
            def create(self, key, raw):
                super().create(key, raw)
                if key.endswith('-' + str(index).zfill(3) + '-intent'):
                    current[0] += timedelta(seconds=31)
        backend = FakeBackend(b, policy, signature)
        receipts = SlowReceipts()
        result = a.run(b, backend, receipts, now=lambda: current[0])
        self.assertEqual(result.failure, a.FAILURE)
        self.assertFalse(any(r.method == 'PATCH' for r in backend.requests))
        self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_slow_cleanup_intent_cannot_use_stale_fence_for_inventory_or_delete(self):
        for request_name in ('owned-target-inventory', 'delete-owned-0', 'delete-owned-2'):
            _, b, policy, signature = fixture('cleanup')
            current = [NOW]
            index = next(i for i, r in enumerate(a.build_requests(b)) if r.name == request_name)
            class SlowReceipts(MemoryReceipts):
                def create(self, key, raw):
                    super().create(key, raw)
                    if key.endswith('-' + str(index).zfill(3) + '-intent'):
                        current[0] += timedelta(seconds=31)
            backend = FakeBackend(b, policy, signature)
            result = a.run(b, backend, SlowReceipts(), now=lambda: current[0])
            self.assertEqual(result.failure, a.FAILURE)
            self.assertNotIn(request_name, [r.name for r in backend.requests])

    def test_native_delete_accepts_v034_status_200_202_and_resource_shapes(self):
        for status, shape in ((200, 'registry'), (202, 'handler'), (200, 'resource'), (202, 'resource')):
            def mutation(request, response):
                if request.method == 'DELETE':
                    r = a.Resource.parse(a.strict_json(request.expected)['resource'])
                    value = object_value(r, '100') if shape == 'resource' else kubernetes_status(r, status,
                                                                                              shape == 'handler')
                    return replace(response, status=status, body=a.canonical(value))
                return response
            result, backend, _ = self.perform('cleanup', mutation)
            self.assertIsNone(result.failure)
            self.assertEqual(len([r for r in backend.requests if r.name.startswith('independent-absence-')]),
                             len(backend.binding.owned_targets))

    def test_native_delete_malformed_wrong_identity_group_and_failure_refuse(self):
        for change in (lambda v: v['details'].update(name='wrong-name'),
                       lambda v: v['details'].update(group='wrong.example'),
                       lambda v: v['details'].update(kind='wrong-plural'),
                       lambda v: v.update(status='Failure'), lambda v: v.update(code=True),
                       lambda v: v.update(code=403), lambda v: v.update(metadata=[]),
                       lambda v: v.update(reason='Forbidden'), lambda v: v.update(details='malformed'),
                       lambda v: v.update(message=False)):
            self.assert_stop('cleanup', 'delete-owned-0', lambda r: mutate_json(r, change))
        for status in (201, 204, 403, 504):
            self.assert_stop('cleanup', 'delete-owned-0', lambda r: replace(r, status=status))
        for field, value in (('uid', 'wrong-uid'), ('name', 'wrong-name')):
            def wrong_resource(response):
                _, b, _, _ = fixture('cleanup')
                target = next(r for r in b.owned_targets if r.kind == 'PersistentVolumeClaim')
                native = object_value(target, '100')
                native['metadata'][field] = value
                return replace(response, body=a.canonical(native))
            self.assert_stop('cleanup', 'delete-owned-0', wrong_resource)

    def test_grouped_native_notfound_accepts_standard_optional_fields(self):
        _, b, _, _ = fixture('cleanup')
        r = a.Resource.parse(resource('Deployment', 'target-worker', 'target-worker-uid', namespace='isolated-target'))
        request = a._get(b, 'grouped-native-notfound', r, True, 'absence')
        value = kubernetes_status(r, 404)
        value['metadata'] = {'resourceVersion': '', 'continue': '', 'remainingItemCount': 0}
        value['details'].update(causes=[{'reason': 'UnexpectedServerResponse', 'message': '', 'field': ''}],
                                retryAfterSeconds=0)
        response = a.Response(0, 404, a.canonical(value))
        a._semantic(b, request, response, NOW)
        for mutate in (lambda v: v['details'].update(group='wrong.example'),
                       lambda v: v['details'].pop('group'),
                       lambda v: v['details'].update(kind='Deployment'),
                       lambda v: v['details'].update(name='wrong-name'),
                       lambda v: v.update(code=True), lambda v: v.update(metadata=[]),
                       lambda v: v.update(status='Success'), lambda v: v['details'].update(causes=[False])):
            with self.assertRaises(a.Refusal):
                a._semantic(b, request, mutate_json(response, mutate), NOW)

    def test_native_delete_acceptance_never_replaces_independent_absence(self):
        def mutation(request, response):
            if request.method == 'DELETE':
                r = a.Resource.parse(a.strict_json(request.expected)['resource'])
                return replace(response, status=202, body=a.canonical(kubernetes_status(r, 202, handler=True)))
            if request.name == 'independent-absence-0':
                return replace(response, status=403)
            return response
        result, backend, receipts = self.perform('cleanup', mutation)
        self.assertEqual(result.failure, a.FAILURE)
        self.assertEqual(backend.requests[-1].name, 'independent-absence-0')
        self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_final_source_drift_after_restore_and_cleanup_prevents_completion(self):
        for operation in ('restore', 'cleanup'):
            for field, value in (('uid', 'changed-source-uid'), ('resourceVersion', '100')):
                for index in (0, 1, 2, 3, 4):
                    _, backend, receipts = self.assert_stop(operation, 'preserve-source-' + str(index), lambda r:
                        mutate_json(r, lambda v: v['metadata'].update({field: value})))
                    if operation == 'restore':
                        self.assertTrue(any(r.name == 'restore' for r in backend.requests))
                    else:
                        self.assertTrue(any(r.method == 'DELETE' for r in backend.requests))
                    self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_cleanup_protected_watcher_allows_restored_rv_and_requires_exact_original_scope(self):
        _, b, policy, signature = fixture('cleanup', protected_watcher=True)
        index = next(i for i, r in enumerate(b.protected_sources) if r == b.watcher)
        name = 'preserve-source-' + str(index)
        backend = FakeBackend(b, policy, signature)
        result = a.run(b, backend, MemoryReceipts(), now=lambda: NOW)
        self.assertIsNone(result.failure)
        request = next(r for r in backend.requests if r.name == name)
        self.assertEqual(a.strict_json(request.expected)['check'], 'watcher-restored')
        for change in ('scope', 'uid'):
            def mutation(request, response):
                if request.name != name:
                    return response
                def alter(value):
                    if change == 'scope':
                        value['spec']['template']['spec']['containers'][0]['env'][0]['value'] = ','.join(b.watcher_during)
                    else:
                        value['metadata']['uid'] = 'changed-watcher-uid'
                return mutate_json(response, alter)
            backend = FakeBackend(b, policy, signature, mutation)
            receipts = MemoryReceipts()
            result = a.run(b, backend, receipts, now=lambda: NOW)
            self.assertEqual(result.failure, a.FAILURE)
            self.assertEqual(backend.requests[-1].name, name)
            self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_cleanup_unchanged_watcher_uses_only_exact_scope_readbacks_and_no_patch(self):
        _, b, policy, signature = fixture('cleanup', protected_watcher=True, unchanged_watcher=True)
        index = next(i for i, r in enumerate(b.protected_sources) if r == b.watcher)
        final_name = 'preserve-source-' + str(index)
        backend = FakeBackend(b, policy, signature)
        result = a.run(b, backend, MemoryReceipts(), now=lambda: NOW)
        self.assertIsNone(result.failure)
        self.assertEqual(b.watcher_before, b.watcher_during)
        self.assertFalse(any(r.method == 'PATCH' for r in backend.requests))
        for name in ('watcher-before-restoration', 'independent-watcher-readback', final_name):
            request = next(r for r in backend.requests if r.name == name)
            self.assertEqual(request.method, 'GET')
            self.assertEqual(a.strict_json(request.expected)['check'], 'watcher-current')
        for name, change in (('watcher-before-restoration', 'scope'),
                             ('independent-watcher-readback', 'scope'), (final_name, 'scope'),
                             ('independent-watcher-readback', 'resourceVersion')):
            with self.subTest(name=name, change=change):
                def mutation(request, response):
                    if request.name != name:
                        return response
                    def alter(value):
                        if change == 'scope':
                            value['spec']['template']['spec']['containers'][0]['env'][0]['value'] = \
                                ','.join(b.watcher_before + (b.target_namespace.name,))
                        else:
                            value['metadata']['resourceVersion'] = '99'
                    return mutate_json(response, alter)
                backend = FakeBackend(b, policy, signature, mutation)
                receipts = MemoryReceipts()
                result = a.run(b, backend, receipts, now=lambda: NOW)
                self.assertEqual(result.failure, a.FAILURE)
                self.assertEqual(backend.requests[-1].name, name)
                self.assertFalse(any(r.method == 'PATCH' for r in backend.requests))
                self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_cleanup_census_is_required_complete_unique_and_cleanup_only(self):
        for mutate in (lambda v: v.pop('cleanupCensus'), lambda v: v.update(cleanupCensus=None),
                       lambda v: v.update(cleanupCensus=[]),
                       lambda v: v['cleanupCensus'].pop(),
                       lambda v: v['cleanupCensus'].append(deepcopy(v['cleanupCensus'][0])),
                       lambda v: v['cleanupCensus'].append(dict(v['cleanupCensus'][1], uid='alias-uid')),
                       lambda v: v['cleanupCensus'].append(dict(v['cleanupCensus'][1], name='alias-name')),
                       lambda v: v['cleanupCensus'].append(resource('UnknownKind', 'unknown', 'unknown-uid',
                                                                  namespace='isolated-target'))):
            with self.subTest(mutate=mutate):
                self.refused_input(mutate, 'cleanup')
        cleanup, _, _, _ = fixture('cleanup')
        for operation in ('capture', 'restore', 'resume'):
            self.refused_input(lambda v: v.update(cleanupCensus=cleanup['cleanupCensus']), operation)
        _, b, _, _ = fixture('cleanup')
        with self.assertRaises(a.Refusal):
            a.build_requests(replace(b, cleanup_census=()))

    def test_full_cleanup_census_covers_generated_children_without_expanding_deletion_roots(self):
        _, b, policy, signature = fixture('cleanup', generated_census=True)
        backend = FakeBackend(b, policy, signature)
        receipts = MemoryReceipts()
        result = a.run(b, backend, receipts, now=lambda: NOW)
        self.assertIsNone(result.failure)
        self.assertGreater(len(b.cleanup_census), len(b.owned_targets))
        names = [r.name for r in backend.requests]
        fence = names.index('verify-creation-fence-before-inventory')
        inventory = names.index('owned-target-inventory')
        target_reads = [r for r in backend.requests if r.name.startswith('bind-target-')]
        self.assertEqual(inventory, fence + 1)
        self.assertEqual({r.path for r in target_reads}, {r.path() for r in b.cleanup_census})
        first_delete = next(i for i, r in enumerate(backend.requests) if r.method == 'DELETE')
        self.assertTrue(all(inventory < names.index(r.name) < first_delete for r in target_reads))
        deletes = [r for r in backend.requests if r.method == 'DELETE']
        self.assertEqual({r.path for r in deletes}, {r.path() for r in b.owned_targets})
        children = set(b.cleanup_census) - set(b.owned_targets)
        self.assertFalse({r.path() for r in children} & {r.path for r in deletes})
        absence = [r for r in backend.requests if r.name.startswith('independent-absence-')]
        self.assertEqual({r.path for r in absence}, {r.path() for r in b.cleanup_census})
        for request in backend.requests:
            if request.path == 'owned-target-inventory':
                self.assertEqual(a.strict_json(request.stdin)['cleanupCensus'],
                                 [r.wire() for r in b.cleanup_census])

    def test_cleanup_census_only_kinds_cannot_become_direct_deletion_roots(self):
        value, _, _, _ = fixture('cleanup', generated_census=True)
        for child in value['cleanupCensus'][3:]:
            with self.subTest(kind=child['kind']):
                bad = deepcopy(value)
                bad['ownedTargets'].append(deepcopy(child))
                with self.assertRaises(a.Refusal):
                    a.parse_binding(bad)

    def test_cleanup_census_pv_requires_owned_root_before_request_construction(self):
        value, b, policy, signature = fixture('cleanup')
        extra = resource('PersistentVolume', 'extra-target-data', 'extra-target-pv-uid')
        value['cleanupCensus'].append(extra)
        with self.assertRaises(a.Refusal):
            a.parse_binding(value)
        changed = replace(b, cleanup_census=b.cleanup_census + (a.Resource.parse(extra),))
        backend = FakeBackend(changed, policy, signature)
        with self.assertRaises(a.Refusal):
            a.run(changed, backend, MemoryReceipts(), now=lambda: NOW)
        self.assertEqual(backend.requests, [])

    def test_cleanup_slow_census_refreshes_qualified_fence_before_deletes(self):
        _, b, policy, signature = fixture('cleanup', generated_census=True)
        current = [NOW]
        def mutation(request, response):
            if request.name.startswith('bind-target-'):
                current[0] += timedelta(seconds=2)
            if a.strict_json(request.expected)['check'] == 'creation-fence':
                return mutate_json(response, lambda v: v.update(
                    observedAt=current[0].strftime('%Y-%m-%dT%H:%M:%SZ')))
            return response
        backend = FakeBackend(b, policy, signature, mutation)
        receipts = MemoryReceipts()
        result = a.run(b, backend, receipts, now=lambda: current[0], monotonic=lambda: 0)
        self.assertGreater((current[0] - NOW).total_seconds(), 30)
        self.assertIsNone(result.failure)
        self.assertTrue(any(k.endswith('-complete') for k in receipts.records))
        names = [r.name for r in backend.requests]
        refresh = names.index('verify-creation-fence-before-deletes')
        self.assertTrue(all(i < refresh for i, r in enumerate(backend.requests)
                            if r.name.startswith('bind-target-')))
        first_delete = next(i for i, r in enumerate(backend.requests) if r.method == 'DELETE')
        self.assertEqual(first_delete, refresh + 1)
        self.assertEqual(a.strict_json(backend.requests[refresh].stdin)['creationFence'], b.creation_fence.wire())

    def test_cleanup_fence_refresh_failure_prevents_every_delete(self):
        for mutation in (lambda r: replace(r, status=403), lambda r: replace(r, status=504),
                         lambda r: mutate_json(r, lambda v: v.update(observation='unknown')),
                         lambda r: mutate_json(r, lambda v: v.update(observedAt='2026-10-23T11:59:29Z')),
                         lambda r: mutate_json(r, lambda v: v['creationFence']['resource'].update(uid='changed-uid'))):
            _, backend, receipts = self.assert_stop('cleanup', 'verify-creation-fence-before-deletes', mutation)
            self.assertTrue(any(r.name.startswith('bind-target-') for r in backend.requests))
            self.assertFalse(any(r.method == 'DELETE' for r in backend.requests))
            self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_cleanup_inventory_accepts_reordered_complete_census(self):
        _, b, policy, signature = fixture('cleanup', generated_census=True)
        def mutation(request, response):
            if request.name == 'owned-target-inventory':
                return mutate_json(response, lambda v: v['resources'].reverse())
            return response
        backend = FakeBackend(b, policy, signature, mutation)
        receipts = MemoryReceipts()
        result = a.run(b, backend, receipts, now=lambda: NOW)
        self.assertIsNone(result.failure)
        self.assertTrue(any(k.endswith('-complete') for k in receipts.records))
        self.assertEqual(len([r for r in backend.requests if r.name.startswith('independent-absence-')]),
                         len(b.cleanup_census))

    def test_cleanup_census_excludes_sources_watcher_and_cluster_control_identities(self):
        for alias in ('source-uid', 'source-path', 'watcher-uid', 'watcher-path', 'fence', 'other-fence'):
            def mutate(v):
                if alias == 'fence' or alias == 'other-fence':
                    item = dict(v['creationFence']['resource'])
                    if alias == 'other-fence':
                        item.update(name='another-fence', uid='another-fence-uid')
                elif alias.startswith('source'):
                    item = resource('Pod', 'extra', 'extra-uid', namespace='isolated-target')
                    if alias == 'source-uid':
                        item['uid'] = v['protectedSources'][1]['uid']
                    else:
                        item = dict(v['protectedSources'][-1], uid='aliased-source-path-uid')
                else:
                    v['watcher']['namespace'] = 'isolated-target'
                    item = dict(v['watcher'], resourceVersion='999')
                    item.update(name='different' if alias == 'watcher-uid' else item['name'],
                                uid='different-uid' if alias == 'watcher-path' else item['uid'])
                v['cleanupCensus'].append(item)
            self.refused_input(mutate, 'cleanup')

    def test_cleanup_inventory_unknown_extra_missing_or_changed_children_prevent_deletion(self):
        for change in ('unknown', 'extra', 'missing', 'uid', 'resourceVersion'):
            _, b, policy, signature = fixture('cleanup', generated_census=True)
            def mutation(request, response):
                if request.name != 'owned-target-inventory':
                    return response
                def alter(value):
                    if change == 'unknown':
                        value['resources'][-1]['kind'] = 'UnsupportedChild'
                    elif change == 'extra':
                        value['resources'].append(resource('Pod', 'extra', 'extra-uid', namespace='isolated-target'))
                    elif change == 'missing':
                        value['resources'].pop()
                    else:
                        value['resources'][-1][change] = 'changed-uid' if change == 'uid' else '999'
                return mutate_json(response, alter)
            backend = FakeBackend(b, policy, signature, mutation)
            receipts = MemoryReceipts()
            result = a.run(b, backend, receipts, now=lambda: NOW)
            self.assertEqual(result.failure, a.FAILURE)
            self.assertEqual(backend.requests[-1].name, 'owned-target-inventory')
            self.assertFalse(any(r.method == 'DELETE' for r in backend.requests))
            self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_cleanup_generated_child_identity_and_independent_absence_are_required(self):
        for check in ('identity', 'absence'):
            _, b, policy, signature = fixture('cleanup', generated_census=True)
            child = next(r for r in b.cleanup_census if r.kind == 'Pod')
            def mutation(request, response):
                if request.path == child.path() and a.strict_json(request.expected)['check'] == check:
                    return replace(response, status=403)
                return response
            backend = FakeBackend(b, policy, signature, mutation)
            receipts = MemoryReceipts()
            result = a.run(b, backend, receipts, now=lambda: NOW)
            self.assertEqual(result.failure, a.FAILURE)
            self.assertEqual(backend.requests[-1].path, child.path())
            if check == 'identity':
                self.assertFalse(any(r.method == 'DELETE' for r in backend.requests))
            self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_terminating_source_target_paused_resume_and_watcher_identities_refuse(self):
        checks = [('capture', 'protect-source-0'), ('capture', 'preserve-source-0'),
            ('restore', 'bind-target-0'), ('restore', 'preserve-source-0'),
            ('cleanup', 'bind-target-0'), ('cleanup', 'watcher-before-restoration'),
            ('cleanup', 'restore-watcher'), ('cleanup', 'independent-watcher-readback'),
            ('cleanup', 'preserve-source-0'), ('resume', 'paused-workload'),
            ('resume', 'resume-replicas'), ('resume', 'reconcile-resume')]
        for operation, name in checks:
            with self.subTest(operation=operation, name=name):
                _, _, receipts = self.assert_stop(operation, name, lambda r:
                    mutate_json(r, lambda v: v['metadata'].update(deletionTimestamp=STAMP)))
                self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_native_delete_accepts_valid_terminating_resource_only_as_acceptance(self):
        for deletion_time in (STAMP, '2026-10-23T12:00:00.123456789Z', '2026-10-23T14:00:00+02:00'):
            def mutation(request, response):
                if request.method == 'DELETE':
                    r = a.Resource.parse(a.strict_json(request.expected)['resource'])
                    body = object_value(r, '100')
                    body['metadata']['deletionTimestamp'] = deletion_time
                    return replace(response, status=202, body=a.canonical(body))
                return response
            result, backend, _ = self.perform('cleanup', mutation)
            self.assertIsNone(result.failure)
            self.assertEqual(len([r for r in backend.requests if r.name.startswith('independent-absence-')]),
                             len(backend.binding.cleanup_census))
        for bad_time in ('invalid', False, '2026-99-23T12:00:00Z', '2026-10-23T12:00:00+02:99'):
            def malformed(response):
                _, b, _, _ = fixture('cleanup')
                target = next(r for r in b.owned_targets if r.kind == 'PersistentVolumeClaim')
                body = object_value(target)
                body['metadata']['deletionTimestamp'] = bad_time
                return replace(response, body=a.canonical(body))
            self.assert_stop('cleanup', 'delete-owned-0', malformed)

    def test_capture_rechecks_every_source_uid_and_version_after_stage_without_completion_on_drift(self):
        for index in range(5):
            for field, value in (('uid', 'changed-source-uid'), ('resourceVersion', '999')):
                with self.subTest(index=index, field=field):
                    _, backend, receipts = self.assert_stop('capture', 'preserve-source-' + str(index), lambda r:
                        mutate_json(r, lambda v: v['metadata'].update({field: value})))
                    self.assertTrue(any(r.name == 'capture' for r in backend.requests))
                    self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_namespace_names_use_dns_labels_while_other_resources_keep_dns_subdomains(self):
        for key in ('sourceNamespace', 'targetNamespace'):
            for name in ('has.dot', 'n' * 64):
                self.refused_input(lambda v: v[key].update(name=name))
                self.refused_input(lambda v: v[key].update(name=name), 'cleanup')
        for name in ('n' * 63, 'valid-label'):
            a.Resource.parse(resource('Namespace', name, 'namespace-uid'))
            a.Resource.parse(resource('Namespace', name, None, None), proposed=True)
        a.Resource.parse(resource('ConfigMap', 'valid.dotted.name', 'config-uid', namespace='source'))

    def test_backend_deadlines_are_clamped_to_remaining_policy_validity_at_dispatch(self):
        for operation in ('capture', 'restore', 'resume', 'cleanup'):
            for request_name in ('policy-readback', {'capture': 'capture', 'restore': 'restore',
                                                     'resume': 'resume-replicas', 'cleanup': 'delete-owned-0'}[operation]):
                with self.subTest(operation=operation, request=request_name):
                    _, b, policy, signature = fixture(operation)
                    current = [NOW]
                    index = next(i for i, r in enumerate(a.build_requests(b)) if r.name == request_name)
                    class NearExpiryReceipts(MemoryReceipts):
                        def create(self, key, raw):
                            super().create(key, raw)
                            if key.endswith('-' + str(index).zfill(3) + '-intent'):
                                current[0] = a.timestamp(b.expires_at) - timedelta(milliseconds=250)
                    class DeadlineBackend(FakeBackend):
                        def exchange(self, request, deadline):
                            if request.name == request_name:
                                self.deadline = deadline
                                raise TimeoutError('synthetic uncertain request effect')
                            return super().exchange(request, deadline)
                    # Keep drain/fence observations fresh while advancing the
                    # synthetic wall clock only immediately before dispatch.
                    if operation in ('resume', 'cleanup') and request_name != 'policy-readback':
                        current[0] = a.timestamp(b.expires_at) - timedelta(seconds=1)
                        def fresh_observations(request, response):
                            if a.strict_json(request.expected)['check'] in ('drain', 'creation-fence'):
                                return mutate_json(response, lambda v: v.update(
                                    observedAt=current[0].strftime('%Y-%m-%dT%H:%M:%SZ')))
                            return response
                    else:
                        fresh_observations = None
                    backend = DeadlineBackend(b, policy, signature, fresh_observations)
                    receipts = NearExpiryReceipts()
                    result = a.run(b, backend, receipts, now=lambda: current[0], monotonic=lambda: 123)
                    self.assertEqual(backend.deadline, 123.25)
                    self.assertEqual(result.failure, a.FAILURE)
                    self.assertFalse(any(k.endswith('-complete') for k in receipts.records))

    def test_distinct_clusters_route_target_reads_deletes_and_source_preservation_watcher_correctly(self):
        for operation in ('capture', 'restore', 'resume', 'cleanup'):
            with self.subTest(operation=operation):
                _, b, policy, signature = fixture(operation, distinct_clusters=True, protected_watcher=True,
                                                  generated_census=operation == 'cleanup')
                backend = FakeBackend(b, policy, signature)
                result = a.run(b, backend, MemoryReceipts(), now=lambda: NOW)
                self.assertIsNone(result.failure)
                target_reads = []
                source_reads = []
                for request in backend.requests:
                    if request.method == 'GET':
                        target = (request.name == 'proposed-target-absent' or
                                  request.name.startswith(('bind-target-', 'independent-absence-')))
                        self.assertEqual(request.endpoint, b.target_cluster.endpoint if target else b.source_cluster.endpoint,
                                         request.name)
                        (target_reads if target else source_reads).append(request)
                    elif request.method == 'DELETE':
                        self.assertEqual(request.endpoint, b.target_cluster.endpoint)
                    elif request.method == 'PATCH':
                        self.assertEqual(request.endpoint, b.source_cluster.endpoint)
                self.assertTrue(target_reads)
                self.assertTrue(source_reads)
                if operation == 'cleanup':
                    for name in ('watcher-before-restoration', 'restore-watcher', 'independent-watcher-readback'):
                        request = next(r for r in backend.requests if r.name == name)
                        self.assertEqual(request.endpoint, b.source_cluster.endpoint)


class LocalTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.root.chmod(0o700)

    def tearDown(self):
        self.temporary.cleanup()

    def write(self, name, raw):
        path = self.root / name
        path.write_bytes(raw)
        path.chmod(0o600)
        return path

    def cli(self, argv):
        output = io.StringIO()
        with redirect_stdout(output):
            code = a.main(argv)
        return code, output.getvalue()

    def proposal(self):
        value, _, _, _ = fixture(missing_tools=True)
        wanted = ('policyId sourceCluster targetCluster sourceNamespace targetNamespace protectedSources '
            'pausedWorkload sourceStores previousReplicas watcher watcherBefore watcherDuring operatorPrincipal '
            'validatorPrincipal signerSha256 retainedDecisionsSha256 tools').split()
        result = {key: value[key] for key in wanted}
        result.update(schemaVersion=1, observedAt=STAMP, preparationTools=[
            {'path': str(SOURCE), 'sha256': a.digest(SOURCE.read_bytes()), 'size': SOURCE.stat().st_size}])
        result['validatorPrincipal'] = None
        result['signerSha256'] = None
        return result

    def test_private_input_hash_permissions_symlink_hardlink_and_ancestor_refuse(self):
        path = self.write('input.json', b'{}')
        self.assertEqual(a.read_exact_local(path, a.digest(b'{}')), b'{}')
        with self.assertRaises(a.Refusal):
            a.read_exact_local(path, H)
        path.chmod(0o644)
        with self.assertRaises(a.Refusal):
            a.read_exact_local(path, a.digest(b'{}'))
        path.chmod(0o600)
        alias = self.root / 'alias'
        alias.symlink_to(path)
        with self.assertRaises(a.Refusal):
            a.read_exact_local(alias, a.digest(b'{}'))
        alias.unlink()
        os.link(path, alias)
        with self.assertRaises(a.Refusal):
            a.read_exact_local(path, a.digest(b'{}'))
        alias.unlink()
        self.root.chmod(0o777)
        with self.assertRaises(a.Refusal):
            a.read_exact_local(path, a.digest(b'{}'))
        self.root.chmod(0o700)

    def test_create_only_receipts_preserve_existing_bytes(self):
        receipts = a.LocalReceipts(self.root)
        receipts.create('same-key', b'original')
        with self.assertRaises(a.Refusal):
            receipts.create('same-key', b'changed')
        self.assertEqual((self.root / 'same-key').read_bytes(), b'original')
        self.assertEqual((self.root / 'same-key').stat().st_mode & 0o777, 0o600)
        with self.assertRaises(a.Refusal):
            receipts.create('../escape', b'changed')

    def test_live_activation_refused_before_any_local_read(self):
        for flag in ('live', '--live', 'activate', '--execute'):
            with patch.object(a, 'read_exact_local', side_effect=AssertionError('must not touch input')):
                code, output = self.cli([flag, '--input', 'SECRET', '--sha256', 'SECRET'])
            self.assertEqual(code, 1)
            self.assertNotIn('SECRET', output)
            self.assertEqual(json.loads(output)['backendRequests'], 0)

    def test_cli_invalid_arguments_do_not_echo_values(self):
        code, output = self.cli(['render', '--unknown=SECRET'])
        self.assertEqual(code, 1)
        self.assertNotIn('SECRET', output)

    def test_bound_cli_private_render_is_exact_create_only_and_publicly_sanitized(self):
        value, b, _, _ = fixture('cleanup')
        path = self.write('bound.json', a.canonical(value))
        output_path = self.root / 'rendered.json'
        with patch.object(a, '_window'), patch('socket.socket', side_effect=AssertionError('forbidden')), \
             patch('subprocess.Popen', side_effect=AssertionError('forbidden')):
            code, output = self.cli(['render', '--input', str(path), '--sha256', a.digest(path.read_bytes()),
                                     '--private-output', str(output_path)])
        self.assertEqual(code, 0)
        self.assertEqual(output_path.read_bytes(), a.canonical(a.private_plan(b)))
        for sensitive in ('source-namespace-uid', '192.0.2.10', 'synthetic-provider', 'retained-policy-identity'):
            self.assertNotIn(sensitive, output)
        before = output_path.read_bytes()
        with patch.object(a, '_window'):
            code, _ = self.cli(['render', '--input', str(path), '--sha256', a.digest(path.read_bytes()),
                                '--private-output', str(output_path)])
        self.assertEqual(code, 1)
        self.assertEqual(output_path.read_bytes(), before)

    def test_current_proposal_null_gates_render_without_fabricated_policy_or_target_uid(self):
        value = self.proposal()
        plan = a.render_proposal(value)
        self.assertEqual(plan['operationalGate'], 'closed')
        self.assertFalse(plan['runnable'])
        self.assertTrue(all(v is None for v in plan['unresolvedGates'].values()))
        self.assertEqual(plan['knownReadOnlyRequests'][-1]['path'], '/api/v1/namespaces/isolated-target')
        resume = next(r for r in plan['dependentRequestShapes'] if r['operation'] == 'resume')
        self.assertEqual(resume['body'][0]['value'], 'source-mcp-uid')
        self.assertIsNone(resume['body'][1]['value'])
        self.assertTrue(all(r['state'] == 'blocked' for r in plan['dependentRequestShapes']))
        with self.assertRaises(a.Refusal):
            a.parse_binding(value)

    def test_proposal_cli_only_local_and_public_projection_is_neutral(self):
        path = self.write('proposal.json', a.canonical(self.proposal()))
        target = self.root / 'proposal-render.json'
        with patch('socket.socket', side_effect=AssertionError('forbidden')), \
             patch('subprocess.Popen', side_effect=AssertionError('forbidden')):
            code, output = self.cli(['proposal', '--input', str(path), '--sha256', a.digest(path.read_bytes()),
                                     '--private-output', str(target)])
        self.assertEqual(code, 0)
        public = json.loads(output)
        self.assertEqual(public['result'], 'consistent-offline-proposal')
        self.assertEqual(public['backendRequests'], 0)
        self.assertNotIn('source-namespace-uid', output)
        self.assertEqual(public['privatePlanSha256'], a.digest(target.read_bytes()))

    def test_proposal_rejects_invented_target_identity_and_same_validator(self):
        for mutate in (lambda v: v['targetNamespace'].update(uid='invented', resourceVersion='1'),
                       lambda v: v.update(validatorPrincipal=v['operatorPrincipal'])):
            value = self.proposal()
            mutate(value)
            with self.assertRaises(a.Refusal):
                a.render_proposal(value)

    def test_proposal_keeps_missing_volume_and_creation_fence_bindings_unestablished(self):
        value = self.proposal()
        self.assertNotIn('protectedPaths', value)
        self.assertNotIn('creationFence', value)
        self.assertNotIn('cleanupCensus', value)
        plan = a.render_proposal(value)
        cleanup = next(r for r in plan['dependentRequestShapes'] if r['operation'] == 'cleanup')
        self.assertIsNone(cleanup['creationFence'])
        self.assertIsNone(cleanup['cleanupCensus'])
        self.assertIsNone(plan['unresolvedGates']['qualifiedNamespaceCreationFence'])
        for key, bad_value in (('protectedPaths', []), ('creationFence', None), ('cleanupCensus', None)):
            with self.assertRaises(a.Refusal):
                a.render_proposal(dict(value, **{key: bad_value}))

    def test_proposal_rejects_namespace_and_duplicate_source_store_identities(self):
        for mutate in (lambda v: v.update(sourceStores=[v['sourceNamespace']]),
                       lambda v: v['sourceStores'].append(deepcopy(v['sourceStores'][0]))):
            value = self.proposal()
            mutate(value)
            with self.assertRaises(a.Refusal):
                a.render_proposal(value)

    def test_proposal_discloses_unchanged_or_extended_watcher_restoration_requirement(self):
        for unchanged in (False, True):
            with self.subTest(unchanged=unchanged):
                value = self.proposal()
                if unchanged:
                    value['watcherDuring'] = list(value['watcherBefore'])
                plan = a.render_proposal(value)
                cleanup = next(r for r in plan['dependentRequestShapes'] if r['operation'] == 'cleanup')
                self.assertIs(cleanup['restorationRequired'], not unchanged)
                self.assertEqual(cleanup['watcherRestoreValue'], ','.join(value['watcherBefore']))

    def test_proposal_rejects_duplicate_resource_paths_with_distinct_uids(self):
        value = self.proposal()
        value['protectedSources'].append(dict(value['protectedSources'][1], uid='different-uid'))
        with self.assertRaises(a.Refusal):
            a.render_proposal(value)

    def test_proposal_rejects_dotted_or_overlength_namespace_names(self):
        for key in ('sourceNamespace', 'targetNamespace'):
            for name in ('dotted.namespace', 'n' * 64):
                value = self.proposal()
                value[key]['name'] = name
                with self.assertRaises(a.Refusal):
                    a.render_proposal(value)


if __name__ == '__main__':
    unittest.main(verbosity=2)
