import json
from copy import deepcopy
from contextlib import nullcontext
from datetime import datetime, timezone
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace
import unittest
from unittest.mock import patch, Mock

import bind_isolated_custody as subject
from native_journal import durable_write


def original():
    created = '2020-01-01T00:00:00Z'
    mapping = {'secretVersion': '1', 'state': 'Active', 'notBefore': created, 'verifyUntil': '2020-01-08T00:00:00Z', 'retiredAt': None}
    return {'tenantId': subject.TENANTS[0], 'purpose': 'TrustedEnvelope', 'secretReference': 'tenants/' + subject.TENANTS[0] + '/TrustedEnvelope/operational',
        'opaqueVersion': 'hmac-original', 'mapping': {'hmac-original': mapping}, 'secretField': 'hmacKey', 'secretVersion': '1',
        'writeReceipt': {'created_time': created, 'destroyed': False, 'deletion_time': '', 'version': 1}, 'phase': 'MetadataVerified', 'verifyUntil': mapping['verifyUntil']}


def metadata(old):
    return {'current_version': 1, 'max_versions': 0, 'delete_version_after': '0s',
        'custom_metadata': {'ext-secrets-1.tenantId': old['tenantId'], 'ext-secrets-1.purpose': old['purpose'],
            'ext-secrets-1.secretField': old['secretField'], 'ext-secrets-1.versionMappings': json.dumps(old['mapping'])},
        'versions': {'1': {key: old['writeReceipt'][key] for key in ('created_time', 'deletion_time', 'destroyed')}}}


class BindingTests(unittest.TestCase):
    def test_fresh_ingress_is_withheld_when_original_prerequisites_fail(self):
        with TemporaryDirectory() as directory:
            installer = SimpleNamespace(evidence=Path(directory))
            with patch.object(subject, 'forward', return_value=nullcontext()), patch.object(subject, 'inspect', side_effect=RuntimeError('original-prerequisites-unverified')), patch.object(subject, 'ensure_provider_ingress') as ingress:
                with self.assertRaisesRegex(RuntimeError, 'prerequisites'):
                    subject.install(installer)
            ingress.assert_not_called()

    def test_resume_ingress_is_withheld_when_original_journals_fail(self):
        with TemporaryDirectory() as directory:
            installer = SimpleNamespace(evidence=Path(directory))
            durable_write(Path(directory) / 'provisioner-policy-journal.json', {'phase': 'Removed'})
            with patch.object(subject, 'preserve_original_journals', side_effect=RuntimeError('original-journals-unverified')), patch.object(subject, 'ensure_provider_ingress') as ingress:
                with self.assertRaisesRegex(RuntimeError, 'journals'):
                    subject.install(installer)
            ingress.assert_not_called()

    def test_provider_ingress_is_exact_namespace_and_apps_and_port(self):
        manifest = subject.provider_ingress_manifest('offline-run')
        self.assertEqual(manifest['metadata']['namespace'], 'openbao')
        self.assertEqual(manifest['spec']['policyTypes'], ['Ingress'])
        self.assertEqual(manifest['spec']['podSelector']['matchLabels'], {
            'app.kubernetes.io/instance': 'hexalith-keys', 'app.kubernetes.io/name': 'openbao', 'component': 'server'})
        ingress = manifest['spec']['ingress']
        self.assertEqual(len(ingress), 1)
        self.assertEqual(ingress[0]['ports'], [{'protocol': 'TCP', 'port': 8200}])
        self.assertEqual(ingress[0]['from'], [{'namespaceSelector': {'matchLabels': {'kubernetes.io/metadata.name': subject.NS}},
            'podSelector': {'matchExpressions': [{'key': 'app', 'operator': 'In', 'values': ['inventory', subject.APP]}]}}])

    def test_exact_four_successors_preserve_originals_and_restarts_do_not_rewrite(self):
        with TemporaryDirectory() as directory:
            plan, state = [], {}
            for tenant in subject.TENANTS:
                for purpose in subject.PURPOSES:
                    old = original()
                    old.update(tenantId=tenant, purpose=purpose, secretReference='tenants/' + tenant + '/' + purpose + '/operational')
                    plan.append(old)
                    state[old['secretReference']] = metadata(old)
            installer = SimpleNamespace(evidence=Path(directory), tokens={'provisioner': bytearray(b'offline')},
                metadata=lambda relative: deepcopy(state[relative]))
            durable_write(Path(directory) / 'provider-retention-config.json', {'max_versions': 0, 'delete_version_after': '0s'})
            def generate(token, relative, cas):
                self.assertEqual(cas, 1)
                native = {'created_time': datetime.now(timezone.utc).isoformat(), 'destroyed': False, 'deletion_time': ''}
                state[relative]['versions']['2'] = native
                state[relative]['current_version'] = 2
                return {'requestId': 'native-' + relative, 'data': dict(native, version=2)}
            def request(token, *args, data=b''):
                if args[0] == 'read':
                    return SimpleNamespace(stdout=b'{"data":{"max_versions":0,"delete_version_after":"0s"}}')
                relative = args[1].split('/metadata/', 1)[1]
                state[relative]['custom_metadata'] = json.loads(data)['custom_metadata']
                return SimpleNamespace(stdout=b'{}')
            with patch.object(subject, 'original_plan', return_value=plan), patch.object(subject, 'generate_successor_inside_provider', side_effect=generate) as native_write, patch.object(subject, 'provider_request', side_effect=request):
                first = subject.successors(installer)
                second = subject.successors(installer)
            self.assertEqual(native_write.call_count, 4)
            self.assertEqual([item['nativeWriteReceipt'] for item in first], [item['nativeWriteReceipt'] for item in second])
            self.assertEqual([item['afterMetadata'] for item in first], [item['afterMetadata'] for item in second])
            for item in second:
                self.assertEqual(item['phase'], 'MetadataVerified')
                self.assertEqual(item['afterMetadata']['versions']['1'], item['beforeMetadata']['versions']['1'])
                self.assertEqual(subject.validate_original(item, item['afterMetadata'], successor=True)[item['original']['opaqueVersion']], item['original']['mapping'][item['original']['opaqueVersion']])

    def test_new_provisioner_policy_has_no_data_reads_or_foreign_paths(self):
        policy = subject.provisioning_policy([original()])
        self.assertIn('capabilities = ["create", "update"]', policy)
        self.assertNotIn('capabilities = ["read", "create"', policy)
        self.assertNotIn('secret/data', policy)
        self.assertNotIn('sys/policies', policy)
        self.assertNotIn('*', policy)

    def test_mount_correction_uses_canonical_regular_paths_and_new_journal(self):
        with TemporaryDirectory() as directory:
            state = {}
            for name, container, setting in [('inventory', 'inventory', 'Inventory__RuntimeReferenceFile'), (subject.APP, 'custody', 'Custody__RuntimeReferenceFile')]:
                state[name] = {'metadata': {'name': name, 'uid': name + '-uid'}, 'spec': {'template': {'spec': {'containers': [
                    {'name': container, 'env': [{'name': setting, 'value': '/var/run/original'}], 'volumeMounts': [{'name': 'runtime', 'mountPath': '/var/run/original'}]}]}}}}
            def replace(installer, kind, name, value, suffix):
                self.assertEqual(suffix, '-canonical-runtime-file')
                state[name] = deepcopy(value)
            installer = SimpleNamespace(evidence=Path(directory), write=lambda name, value: durable_write(Path(directory) / name, value))
            with patch.object(subject, 'kjson', side_effect=lambda *args: deepcopy(state[args[2]])), patch.object(subject, 'replace_public', side_effect=replace):
                subject.mount_regular_runtime_files(installer)
            for item in state.values():
                app = item['spec']['template']['spec']['containers'][0]
                mount = app['volumeMounts'][0]
                self.assertTrue(mount['mountPath'].startswith('/run/'))
                self.assertEqual(mount['subPath'], 'runtime-reference.json')
                self.assertEqual(app['env'][0]['value'], mount['mountPath'])

    def test_semantic_comparison_ignores_only_managed_deployment_revision(self):
        value = {'kind': 'Deployment', 'metadata': {'name': 'inventory', 'uid': 'ignored', 'annotations': {
            'deployment.kubernetes.io/revision': '2', 'preserved': 'owner-intent'}}, 'spec': {'replicas': 1}}
        public = subject.public_object(value)
        self.assertEqual(public['metadata']['annotations'], {'preserved': 'owner-intent'})
        value['metadata']['annotations']['deployment.kubernetes.io/revision'] = '3'
        self.assertEqual(subject.public_object(value), public)
        value['metadata']['annotations']['preserved'] = 'changed'
        self.assertNotEqual(subject.public_object(value), public)

    def test_exact_original_validity_is_required(self):
        old = original()
        current = metadata(old)
        self.assertEqual(subject.validate_original(old, current), old['mapping'])
        mapping = dict(old['mapping']['hmac-original'], verifyUntil='2030-01-01T00:00:00Z')
        current['custom_metadata']['ext-secrets-1.versionMappings'] = json.dumps({'hmac-original': mapping})
        with self.assertRaisesRegex(RuntimeError, 'validity'):
            subject.validate_original(old, current)

    def test_original_native_material_metadata_must_be_preserved(self):
        old = original()
        current = metadata(old)
        current['versions']['1']['destroyed'] = True
        with self.assertRaisesRegex(RuntimeError, 'material'):
            subject.validate_original(old, current)

    def test_extra_mappings_are_never_authorized(self):
        old = original()
        current = metadata(old)
        current['custom_metadata']['ext-secrets-1.versionMappings'] = json.dumps(dict(old['mapping'], foreign={}))
        with self.assertRaisesRegex(RuntimeError, 'extra'):
            subject.validate_original(old, current)

    def test_retention_must_keep_both_native_versions_without_expiry(self):
        for mount, per_key in [(0, 0), (10, 0), (1, 2)]:
            self.assertGreaterEqual(subject.retention_preserves_original({'max_versions': mount}, {'max_versions': per_key})['effectiveMaximumVersions'], 2)
        for mount, per_key in [(1, 0), (10, 1)]:
            with self.assertRaisesRegex(RuntimeError, 'retention'):
                subject.retention_preserves_original({'max_versions': mount}, {'max_versions': per_key})
        for config, value in [({'delete_version_after': '1h'}, {}), ({}, {'delete_version_after': '1h'})]:
            with self.assertRaisesRegex(RuntimeError, 'retention'):
                subject.retention_preserves_original(config, value)

    def test_generation_is_cas_one_and_keeps_material_inside_provider(self):
        old = original()
        output = {'request_id': 'native-request', 'data': dict(old['writeReceipt'], version=2)}
        with patch.object(subject, 'original_plan', return_value=[old]), patch.object(subject, 'kube', return_value=SimpleNamespace(stdout=json.dumps(output).encode())) as call:
            result = subject.generate_successor_inside_provider(bytearray(b'offline-operator'), old['secretReference'], 1)
        self.assertEqual(result['requestId'], 'native-request')
        args, kwargs = call.call_args
        script = args[args.index('-c') + 1]
        self.assertIn('"cas":1', script)
        self.assertIn('sys/tools/random/32', script)
        self.assertNotIn('offline-operator', str(args))
        self.assertEqual(kwargs['data'], b'offline-operator\n')

    def test_generation_rejects_foreign_scope_or_cas(self):
        with patch.object(subject, 'original_plan', return_value=[original()]), patch.object(subject, 'kube') as call:
            for relative, cas in [('foreign', 1), (original()['secretReference'], 0)]:
                with self.assertRaises(RuntimeError):
                    subject.generate_successor_inside_provider(bytearray(b'offline'), relative, cas)
        call.assert_not_called()

    def test_uncertain_native_write_is_not_replayed(self):
        for phase in ('CreationStarted', 'CreationUnknown'):
            with TemporaryDirectory() as directory:
                old = original()
                plan = [{'original': old, 'opaqueVersion': 'hmac-successor', 'phase': phase} for _ in range(4)]
                durable_write(Path(directory) / 'successor-key-plan.json', plan)
                installer = SimpleNamespace(evidence=Path(directory), metadata=lambda name: metadata(old))
                with patch.object(subject, 'original_plan', return_value=[old] * 4), patch.object(subject, 'generate_successor_inside_provider') as generate:
                    with self.assertRaisesRegex(RuntimeError, 'no-repeat'):
                        subject.successors(installer)
                generate.assert_not_called()

    def test_unknown_credential_issuance_is_never_repeated_or_policy_deleted(self):
        with TemporaryDirectory() as directory:
            installer = subject.CustodyInstaller.__new__(subject.CustodyInstaller)
            installer.evidence = Path(directory)
            installer.run = {'runId': 'offline-run'}
            installer.bao = Mock()
            durable_write(installer.evidence / 'data-credential-issuance.json', {'phase': 'IssuanceUnknown'})
            with self.assertRaisesRegex(RuntimeError, 'reconciliation'):
                installer.credential('data', subject.DATA_POLICY)
            installer.bao.assert_not_called()

    def test_lost_replace_response_is_reconciled_without_repeat(self):
        with TemporaryDirectory() as directory:
            installer = SimpleNamespace(evidence=Path(directory), write=lambda name, value: durable_write(Path(directory) / name, value))
            obj = {'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'ext-secrets-1-inventory-runtime', 'namespace': subject.NS,
                'uid': 'original-uid', 'resourceVersion': '2'}, 'data': {'public': 'intended'}}
            key = 'ConfigMap/' + subject.NS + '/ext-secrets-1-inventory-runtime'
            durable_write(Path(directory) / 'preserved-kubernetes-before.json', {key: {'uid': 'original-uid'}})
            path = Path(directory) / 'update-configmap-ext-secrets-1-inventory-runtime.json'
            journal = {'phase': 'ReplacementStarted', 'uid': 'original-uid', 'intendedPublicObject': subject.public_object(obj)}
            durable_write(path, journal)
            with patch.object(subject, 'kjson', return_value=obj), patch.object(subject, 'kube') as call:
                subject.replace_public(installer, 'ConfigMap', 'ext-secrets-1-inventory-runtime', obj)
            call.assert_not_called()
            self.assertEqual(json.loads(path.read_text())['phase'], 'ReconciledCurrent')

    def test_unresolved_replace_outcome_stops_without_overwriting_original(self):
        with TemporaryDirectory() as directory:
            installer = SimpleNamespace(evidence=Path(directory))
            obj = {'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'ext-secrets-1-inventory-runtime', 'namespace': subject.NS,
                'uid': 'original-uid', 'resourceVersion': '2'}, 'data': {'public': 'current'}}
            intended = dict(obj, data={'public': 'intended'})
            key = 'ConfigMap/' + subject.NS + '/ext-secrets-1-inventory-runtime'
            durable_write(Path(directory) / 'preserved-kubernetes-before.json', {key: {'uid': 'original-uid'}})
            path = Path(directory) / 'update-configmap-ext-secrets-1-inventory-runtime.json'
            journal = {'phase': 'ReplacementStarted', 'uid': 'original-uid', 'intendedPublicObject': subject.public_object(intended)}
            durable_write(path, journal)
            original_bytes = path.read_bytes()
            with patch.object(subject, 'kjson', return_value=obj), patch.object(subject, 'kube') as call:
                with self.assertRaisesRegex(RuntimeError, 'outcome-unknown'):
                    subject.replace_public(installer, 'ConfigMap', 'ext-secrets-1-inventory-runtime', intended)
            call.assert_not_called()
            self.assertEqual(path.read_bytes(), original_bytes)

    def test_pod_delete_never_repeats_an_unknown_original(self):
        with TemporaryDirectory() as directory:
            installer = SimpleNamespace(evidence=Path(directory))
            durable_write(Path(directory) / 'authority-pod-restart.json', {'phase': 'DeletionStarted', 'originalPodUid': 'same-uid'})
            with patch.object(subject, 'kjson', return_value={'metadata': {'uid': 'same-uid'}}), patch.object(subject, 'kube') as call:
                with self.assertRaisesRegex(RuntimeError, 'no-delete-repeat'):
                    subject.restart_authority(installer)
            call.assert_not_called()

    def test_owner_retry_reuses_original_operation(self):
        with TemporaryDirectory() as directory:
            path = Path(directory) / 'owner-operation-offline.json'
            operation = {'operationId': 'original-op', 'expectedRevision': 'original:1', 'kind': 'SetCaller', 'caller': {'callerId': 'offline'}}
            durable_write(path, {'phase': 'Prepared', 'operation': operation})
            installer = SimpleNamespace(evidence=Path(directory))
            with patch.object(subject, 'administration_request', return_value=(200, {'operationId': 'original-op', 'outcome': 'Applied'})) as call:
                subject.administer(installer, 'offline', 'SetCaller', caller={'callerId': 'offline'})
            self.assertEqual(call.call_args.args[3], operation)


if __name__ == '__main__':
    unittest.main()
