import json
import base64
import os
import subprocess
import sys
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
    def test_actual_provider_shell_transports_only_synthetic_material_to_exact_cas_target(self):
        with TemporaryDirectory() as directory:
            root = Path(directory)
            old = original()
            encoded = base64.b64encode(bytes(range(32))).decode()
            receipt = {'request_id': 'synthetic-native-request', 'data': {'version': 2, 'created_time': '2026-01-01T00:00:00Z', 'destroyed': False, 'deletion_time': ''}}
            shim = root / 'bao'
            shim.write_text('#!' + sys.executable + '\n' + '''import base64,json,os,sys
from pathlib import Path
args=sys.argv[1:]
assert os.environ['BAO_TOKEN']=='synthetic-provisioner'
assert os.environ['BAO_SKIP_VERIFY']=='false'
if args==['write','-field=random_bytes','sys/tools/random/32','format=base64']:
 print(base64.b64encode(bytes(range(32))).decode())
elif len(args)==4 and args[:2]==['write','-format=json'] and args[3]=='-':
 value=json.load(sys.stdin)
 Path(os.environ['SHIM_RECORD']).write_text(json.dumps({'target':args[2],'body':value}))
 print(os.environ['SHIM_RECEIPT'])
else: raise SystemExit(73)
''')
            shim.chmod(0o700)
            def native(*args, **kwargs):
                index = args.index('-c')
                env = {'PATH': directory, 'SHIM_RECORD': str(root / 'write.json'), 'SHIM_RECEIPT': json.dumps(receipt)}
                result = subprocess.run(['/bin/sh', '-c', args[index + 1], *args[index + 2:]], input=kwargs['data'], capture_output=True, env=env, timeout=5)
                self.assertEqual(result.returncode, 0, result.stderr.decode())
                self.assertEqual(json.loads(result.stdout), receipt)
                self.assertNotIn(encoded.encode(), result.stdout + result.stderr)
                self.assertNotIn(b'synthetic-provisioner', result.stdout + result.stderr)
                return result
            with patch.object(subject, 'original_plan', return_value=[old]), patch.object(subject, 'kube', side_effect=native):
                result = subject.generate_successor_inside_provider(bytearray(b'synthetic-provisioner'), old['secretReference'], 1)
            self.assertEqual(json.loads((root / 'write.json').read_text()), {'target': subject.MOUNT + '/data/' + old['secretReference'],
                'body': {'options': {'cas': 1}, 'data': {'hmacKey': encoded}}})
            self.assertEqual(result, {'requestId': receipt['request_id'], 'data': receipt['data']})

    def test_stale_supplied_spec_is_not_restamped_with_fresh_resource_version(self):
        with TemporaryDirectory() as directory:
            installer = SimpleNamespace(evidence=Path(directory))
            current = {'kind': 'ConfigMap', 'metadata': {'name': 'ext-secrets-1-inventory-runtime', 'namespace': subject.NS, 'uid': 'original', 'resourceVersion': '2'}, 'data': {'public': 'concurrent-owner-edit'}}
            supplied = deepcopy(current)
            supplied['metadata']['resourceVersion'] = '1'
            supplied['data']['public'] = 'stale-edit'
            durable_write(Path(directory) / 'preserved-kubernetes-before.json', {'ConfigMap/' + subject.NS + '/ext-secrets-1-inventory-runtime': {'uid': 'original'}})
            with patch.object(subject, 'kjson', return_value=current), patch.object(subject, 'kube') as native:
                with self.assertRaisesRegex(RuntimeError, 'stale-supplied'):
                    subject.replace_public(installer, 'ConfigMap', 'ext-secrets-1-inventory-runtime', supplied)
            native.assert_not_called()

    def final_intent_fixture(self, directory):
        objects, before, after = {}, {}, {}
        for key in subject.MUTABLE:
            kind, namespace, name = key.split('/')
            obj = {'kind': kind, 'metadata': {'name': name, 'namespace': namespace, 'uid': 'uid-' + name, 'resourceVersion': '9'}, 'spec': {'public': 'canonical-final-intent'}}
            objects[key] = obj
            before[key] = after[key] = {'uid': obj['metadata']['uid']}
            suffix = '-canonical-runtime-file' if kind == 'Deployment' else ''
            journal = {'phase': 'Replaced', 'uid': obj['metadata']['uid'], 'reference': key, 'intendedPublicObject': subject.public_object(obj)}
            durable_write(Path(directory) / ('update-' + kind.lower() + '-' + name + suffix + '.json'), journal)
        return SimpleNamespace(evidence=Path(directory)), objects, before, after

    def test_final_verification_requires_original_uid_and_exact_intent_for_every_mutable(self):
        with TemporaryDirectory() as directory:
            installer, objects, before, after = self.final_intent_fixture(directory)
            def read(*args): return objects[args[1] + '/' + args[4] + '/' + args[2]]
            with patch.object(subject, 'kjson', side_effect=read):
                subject.verify_final_public_intents(installer, before, after)
                for key in subject.MUTABLE:
                    original = objects[key]['spec']['public']
                    objects[key]['spec']['public'] = 'unjournaled-owner-drift'
                    with self.assertRaisesRegex(RuntimeError, 'unjournaled'):
                        subject.verify_final_public_intents(installer, before, after)
                    objects[key]['spec']['public'] = original
                    changed = deepcopy(after)
                    changed[key]['uid'] = 'replacement'
                    with self.assertRaisesRegex(RuntimeError, 'original-uid'):
                        subject.verify_final_public_intents(installer, before, changed)

    def test_initial_deployment_intent_cannot_replace_final_canonical_mount_intent(self):
        with TemporaryDirectory() as directory:
            installer, objects, before, after = self.final_intent_fixture(directory)
            key = 'Deployment/' + subject.NS + '/inventory'
            objects[key]['spec']['public'] = 'obsolete-initial-mount'
            durable_write(Path(directory) / 'update-deployment-inventory.json', {'phase': 'Replaced', 'uid': before[key]['uid'], 'reference': key,
                'intendedPublicObject': subject.public_object(objects[key])})
            with patch.object(subject, 'kjson', side_effect=lambda *args: objects[args[1] + '/' + args[4] + '/' + args[2]]):
                with self.assertRaisesRegex(RuntimeError, 'unjournaled'):
                    subject.verify_final_public_intents(installer, before, after)

    def test_same_policy_token_with_different_native_accessor_is_not_adopted(self):
        with TemporaryDirectory() as directory:
            installer = subject.CustodyInstaller.__new__(subject.CustodyInstaller)
            installer.evidence, installer.run, installer.tokens, installer.private_values = Path(directory), {'runId': 'original-run'}, {}, []
            name = subject.APP + '-data-original-run'
            durable_write(Path(directory) / 'data-credential-issuance.json', {'phase': 'Stored', 'policy': subject.DATA_POLICY, 'secretName': name,
                'accessor': 'original-accessor', 'secretIdentity': {'uid': 'original', 'resourceVersion': '7'}})
            candidate = bytearray(b'synthetic-substitute')
            with patch.object(subject, 'read_pinned_credential_secret', return_value=candidate), patch.object(subject, 'application_request',
                return_value=SimpleNamespace(stdout=json.dumps({'data': {'accessor': 'foreign-accessor', 'policies': [subject.DATA_POLICY], 'ttl': 100}}).encode())):
                with self.assertRaisesRegex(RuntimeError, 'accessor-changed'):
                    installer.credential('data', subject.DATA_POLICY)
            self.assertEqual(installer.tokens, {})
            self.assertTrue(all(value == 0 for value in candidate))

    def test_known_provisioner_failure_invokes_run_owned_cleanup_and_wipes_transport(self):
        with TemporaryDirectory() as directory:
            installer = subject.CustodyInstaller.__new__(subject.CustodyInstaller)
            installer.evidence, installer.tokens, installer.private_values, installer.token = Path(directory), {'provisioner': bytearray(b'synthetic-token')}, [], bytearray(b'synthetic-operator')
            durable_write(Path(directory) / 'provisioner-credential-issuance.json', {'phase': 'Issued', 'accessor': 'original-accessor'})
            durable_write(Path(directory) / 'provisioner-policy-journal.json', {'phase': 'Created'})
            with patch.object(installer, 'remove_provisioner') as cleanup:
                installer.close()
            cleanup.assert_called_once()
            self.assertEqual(installer.tokens, {})
            self.assertTrue(all(value == 0 for value in installer.token))

    def test_acknowledged_revocation_can_finish_partial_policy_cleanup_without_token_lookup(self):
        with TemporaryDirectory() as directory:
            installer = subject.CustodyInstaller.__new__(subject.CustodyInstaller)
            installer.evidence, installer.run, installer.tokens = Path(directory), {'runId': 'original-run'}, {}
            policy_name = subject.APP + '-successor-original-run'
            policy_path = Path(directory) / 'provisioner-policy-journal.json'
            durable_write(policy_path, {'phase': 'Created', 'runId': 'original-run', 'name': policy_name, 'policy': 'exact-public-scope'})
            durable_write(Path(directory) / 'provisioner-credential-issuance.json', {'phase': 'RevokedAndOwnedSecretRemoved', 'secretName': 'original-secret',
                'accessor': 'original-accessor', 'revocationPhase': 'Acknowledged', 'secretRemovalPhase': 'Acknowledged'})
            def native(*args):
                if args[:2] == ('policy', 'list'): return SimpleNamespace(stdout=json.dumps([policy_name]).encode())
                if args[:2] == ('policy', 'read'): return SimpleNamespace(stdout=b'exact-public-scope')
                raise RuntimeError('lost-policy-delete-response')
            with patch.object(installer, 'bao', side_effect=native), patch('install_current_inventory.application_request') as credential:
                with self.assertRaisesRegex(RuntimeError, 'lost-policy-delete'):
                    installer.remove_provisioner()
            credential.assert_not_called()
            self.assertEqual(json.loads(policy_path.read_text())['removalPhase'], 'Started')
            with patch.object(installer, 'bao', return_value=SimpleNamespace(stdout=b'[]')) as native, patch('install_current_inventory.application_request') as credential:
                installer.remove_provisioner()
            credential.assert_not_called()
            native.assert_called_once_with('policy', 'list', '-format=json')
            self.assertEqual(json.loads(policy_path.read_text())['phase'], 'Removed')

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
            retained = []
            for old in plan:
                relative = old['secretReference']
                before = deepcopy(state[relative])
                native = {'created_time': datetime.now(timezone.utc).isoformat(), 'destroyed': False, 'deletion_time': ''}
                state[relative]['versions']['2'] = native
                state[relative]['current_version'] = 2
                mapping = {'secretVersion': '2', 'state': 'Active', 'notBefore': native['created_time'],
                    'verifyUntil': (subject.instant(native['created_time']) + subject.timedelta(days=7)).isoformat(), 'retiredAt': None}
                opaque = 'retained-' + old['purpose'] + '-' + old['tenantId']
                mappings = dict(old['mapping'], **{opaque: mapping})
                state[relative]['custom_metadata']['ext-secrets-1.versionMappings'] = subject.canonical(mappings).decode()
                retained.append({'original': old, 'opaqueVersion': opaque, 'phase': 'MetadataVerified', 'beforeMetadata': before,
                    'nativeWriteReceipt': {'requestId': 'native-' + relative, 'data': dict(native, version=2)}})
            durable_write(Path(directory) / 'successor-key-plan.json', retained)
            with patch.object(subject, 'original_plan', return_value=plan), patch.object(subject, 'generate_successor_inside_provider') as native_write, patch.object(subject, 'provider_request') as provider:
                first = subject.successors(installer)
                second = subject.successors(installer)
            native_write.assert_not_called()
            provider.assert_not_called()
            self.assertEqual([item['nativeWriteReceipt'] for item in first], [item['nativeWriteReceipt'] for item in second])
            self.assertEqual([item['afterMetadata'] for item in first], [item['afterMetadata'] for item in second])
            for item in second:
                self.assertEqual(item['phase'], 'MetadataVerified')
                self.assertEqual(item['afterMetadata']['versions']['1'], item['beforeMetadata']['versions']['1'])
                self.assertEqual(subject.validate_original(item, item['afterMetadata'], successor=True)[item['original']['opaqueVersion']], item['original']['mapping'][item['original']['opaqueVersion']])

    def test_unfenced_future_metadata_publication_denied_before_provider_metadata_write(self):
        with TemporaryDirectory() as directory:
            old = original()
            before = metadata(old)
            current = deepcopy(before)
            native = {'created_time': datetime.now(timezone.utc).isoformat(), 'destroyed': False, 'deletion_time': ''}
            current['versions']['2'] = native
            current['current_version'] = 2
            retained = {'original': old, 'opaqueVersion': 'unpublished-successor', 'phase': 'NativeCreated',
                        'beforeMetadata': before, 'nativeWriteReceipt': {'requestId': 'original-request', 'data': dict(native, version=2)}}
            durable_write(Path(directory) / 'successor-key-plan.json', [retained] * 4)
            installer = SimpleNamespace(evidence=Path(directory), metadata=lambda relative: current)
            with patch.object(subject, 'original_plan', return_value=[old] * 4), patch.object(subject, 'provider_request') as provider:
                with self.assertRaisesRegex(RuntimeError, 'provider-native-metadata-fence-required-no-publication'):
                    subject.successors(installer)
            provider.assert_not_called()
            self.assertEqual(json.loads((Path(directory) / 'successor-key-plan.json').read_text())[0]['phase'], 'NativeCreated')

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
