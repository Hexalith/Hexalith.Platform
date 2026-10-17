"""Focused recovery, binding and capture regressions; no cluster connections."""
import base64
from contextlib import contextmanager
from copy import deepcopy
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

import capture_operational_hmac as capture
import install_current_inventory as inventory
import provision_operational_hmac as provisioning


class OperationalReviewTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.directory = Path(temporary.name)

    def test_data_publication_failure_revokes_wipes_and_preserves_unrelated_replacement(self):
        for observed_token, race in [(b'new-token', False), (b'unrelated-token', False), (b'new-token', True)]:
            with self.subTest(observed_token=observed_token, race=race), tempfile.TemporaryDirectory() as directory:
                token = bytearray(b'new-token')
                journal = Path(directory) / 'data-credential-provisioning.json'
                calls = []
                def issue(*args, **kwargs):
                    journal.write_text(json.dumps({'phase': 'Issued'}))
                    return token, {'accessor': 'new-accessor'}
                def kube(*args, **kwargs):
                    calls.append((args, kwargs))
                    if args[0] == 'get':
                        return SimpleNamespace(stdout=base64.b64encode(observed_token) + b' original-uid original-rv')
                    self.assertEqual(json.loads(kwargs['data'])['preconditions'], {'uid': 'original-uid', 'resourceVersion': 'original-rv'})
                    if race:
                        raise RuntimeError('native-delete-precondition-conflict')
                    return SimpleNamespace(stdout=b'')
                installer = SimpleNamespace(evidence=Path(directory), bao=Mock(return_value=SimpleNamespace(stdout=b'[]')),
                                            secret=Mock(side_effect=RuntimeError('lost-publication-response')))
                with patch.object(provisioning, 'kube', return_value=SimpleNamespace(stdout=b'')), \
                        patch.object(inventory, 'kube', side_effect=kube), patch.object(provisioning, 'issue', side_effect=issue), \
                        patch.object(provisioning, 'application_request') as revoke:
                    with self.assertRaises(RuntimeError):
                        provisioning.install_data_credential(installer, provisioning.plan_keys())
                    revoke.assert_called_once_with(token, 'token', 'revoke', '-self')
                self.assertTrue(all(value == 0 for value in token))
                self.assertEqual(sum(args[0] == 'get' for args, _ in calls), 1)
                self.assertEqual(sum(args[0] == 'delete' for args, _ in calls), int(observed_token == b'new-token'))
                self.assertNotIn('new-token', journal.read_text())

    def test_metadata_cleanup_uses_the_single_selected_secret_observation(self):
        encoded = base64.b64encode(b'new-token')
        with patch.object(inventory, 'kube', side_effect=[SimpleNamespace(stdout=encoded + b' first-uid first-rv'), SimpleNamespace(stdout=b'')]) as native:
            inventory.delete_matching_credential_secret('new-secret', b'new-token')
        self.assertEqual(native.call_count, 2)
        self.assertIn('{.data.token}', native.call_args_list[0].args[-1])
        self.assertIn('{.metadata.uid}', native.call_args_list[0].args[-1])
        self.assertEqual(json.loads(native.call_args_list[1].kwargs['data'])['preconditions'], {'uid': 'first-uid', 'resourceVersion': 'first-rv'})

    def test_issuance_is_durable_before_request_and_unknown_never_repeats(self):
        journal = self.directory / 'issuance.json'
        def native(*args, **kwargs):
            if args[0] == 'write':
                before = json.loads(journal.read_text())
                self.assertEqual(before['phase'], 'IssuanceStarted')
                self.assertEqual(json.loads(kwargs['data'])['display_name'], before['displayName'])
                raise RuntimeError('lost-issuance-response')
            return SimpleNamespace(stdout=b'')
        installer = SimpleNamespace(bao=Mock(side_effect=native))
        with self.assertRaisesRegex(RuntimeError, 'lost-issuance-response'):
            provisioning.issue(installer, 'new-exact-policy', 'new-purpose', '15m', journal_path=journal)
        state = json.loads(journal.read_text())
        self.assertEqual(state['phase'], 'IssuanceUnknown')
        self.assertIsNone(state['accessor'])
        self.assertTrue(state['scopePolicyRemoved'])
        self.assertEqual(installer.bao.call_args.args, ('policy', 'delete', 'new-exact-policy'))
        count = installer.bao.call_count
        with self.assertRaisesRegex(RuntimeError, 'no-repeat'):
            provisioning.issue(installer, 'new-exact-policy', 'new-purpose', '15m', journal_path=journal)
        self.assertEqual(installer.bao.call_count, count)

    def native_created_plan(self):
        plan = provisioning.plan_keys()
        for item in plan:
            item.update({'phase': 'NativeCreated', 'writeReceipt': {'version': 1, 'created_time': '2026-10-17T10:00:00.123456789Z'}})
        return plan

    def test_native_created_recovers_only_matching_original_version_and_time(self):
        for mismatch in [None, 'version', 'created_time']:
            with self.subTest(mismatch=mismatch), tempfile.TemporaryDirectory() as directory:
                plan = self.native_created_plan()
                records = {item['secretReference']: {'current_version': 1, 'versions': {'1': {
                    'created_time': item['writeReceipt']['created_time'], 'destroyed': False, 'deletion_time': ''}}} for item in plan}
                first = records[plan[0]['secretReference']]
                if mismatch == 'version':
                    first['current_version'] = 2
                    first['versions']['2'] = first['versions'].pop('1')
                elif mismatch == 'created_time':
                    first['versions']['1']['created_time'] = '2026-10-17T10:00:01.123456789Z'
                def publish(token, *args, data):
                    relative = args[1].split('/metadata/', 1)[1]
                    records[relative]['custom_metadata'] = json.loads(data)['custom_metadata']
                reader = SimpleNamespace(read=lambda name: deepcopy(records[name]))
                installer = SimpleNamespace(evidence=Path(directory), bao=Mock(return_value=SimpleNamespace(stdout=b'[]')))
                token = bytearray(b'offline-provisioner')
                with patch.object(provisioning, 'issue', return_value=(token, {'accessor': 'offline-accessor'})), \
                        patch.object(provisioning, 'credential_request', side_effect=publish), \
                        patch.object(provisioning, 'application_request') as revoke, \
                        patch.object(provisioning, 'generate_inside_provider') as generate:
                    if mismatch:
                        with self.assertRaisesRegex(RuntimeError, 'original-write-receipt'):
                            provisioning.provision_keys(installer, reader, plan)
                    else:
                        provisioning.provision_keys(installer, reader, plan)
                        self.assertTrue(all(item['phase'] == 'MetadataVerified' for item in plan))
                    generate.assert_not_called()
                    revoke.assert_called_once()
                self.assertTrue(all(value == 0 for value in token))

    def test_replace_uses_resource_version_of_observed_spec_without_second_get(self):
        (self.directory / 'preservation-before.json').write_text(json.dumps({'Deployment/' + provisioning.NS + '/inventory': {'uid': 'original'}}))
        observed = {'metadata': {'uid': 'original', 'resourceVersion': 'observed-rv'}}
        installer = SimpleNamespace(evidence=self.directory)
        with patch.object(provisioning, 'kjson', side_effect=AssertionError('second GET must not occur')), \
                patch.object(provisioning, 'kube', side_effect=RuntimeError('native-version-conflict')) as native:
            with self.assertRaisesRegex(RuntimeError, 'version-conflict'):
                provisioning.replace_public(installer, 'Deployment', 'inventory', {'spec': {'original': True}}, observed=observed)
        self.assertEqual(json.loads(native.call_args.kwargs['data'])['metadata']['resourceVersion'], 'observed-rv')

    def test_unknown_provisioner_resume_never_recreates_scope_or_issues(self):
        journal = self.directory / 'key-provisioning-credential.json'
        journal.write_text(json.dumps({'phase': 'IssuanceUnknown', 'scopePolicyRemoved': True}))
        installer = SimpleNamespace(evidence=self.directory, bao=Mock())
        with patch.object(provisioning, 'issue') as issue:
            with self.assertRaisesRegex(RuntimeError, 'no-repeat'):
                provisioning.provision_keys(installer, SimpleNamespace(), provisioning.plan_keys())
        installer.bao.assert_not_called()
        issue.assert_not_called()

    def test_binding_preloads_before_replace_and_changes_checksum_for_same_image(self):
        config = json.loads((Path(provisioning.__file__).parent / 'runtime-reference.example.json').read_text())
        (self.directory / 'runtime-reference.json').write_text(json.dumps(config))
        plan = provisioning.plan_keys()
        image = 'registry.hexalith.com/ext-secrets-1-inventory@sha256:' + 'a' * 64
        deployment = {'apiVersion': 'apps/v1', 'kind': 'Deployment', 'metadata': {'uid': 'original', 'resourceVersion': 'observed', 'namespace': provisioning.NS},
            'spec': {'template': {'spec': {'serviceAccountName': 'ext-secrets-1-inventory', 'automountServiceAccountToken': False,
                'containers': [{'name': 'inventory', 'image': image}, {'name': 'daprd', 'image': 'existing-sidecar'}]}}}}
        installer = SimpleNamespace(evidence=self.directory, write=lambda name, value: (self.directory / name).write_text(json.dumps(value)))
        events, replaced = [], []
        def replacement(instance, kind, name, value, observed=None):
            if kind == 'Deployment':
                self.assertEqual(events[-1], 'preloaded')
                self.assertEqual(observed['metadata']['resourceVersion'], 'observed')
                replaced.append(deepcopy(value))
        def read(*args):
            return deepcopy(deployment) if args[1] == 'deployment' else provisioning.component_manifest()
        with patch.object(provisioning, 'kjson', side_effect=read), patch.object(provisioning, 'kube', return_value=SimpleNamespace(stdout=b'present')), \
                patch.object(provisioning, 'replace_public', side_effect=replacement), patch.object(provisioning, 'preload', side_effect=lambda *args: events.append('preloaded')):
            provisioning.bind(installer, plan, image)
            changed = json.loads((self.directory / 'runtime-reference.json').read_text())
            changed['qualification']['requestTimeoutSeconds'] = 29
            (self.directory / 'runtime-reference.json').write_text(json.dumps(changed))
            provisioning.bind(installer, plan, image)
        checksums = [item['spec']['template']['metadata']['annotations']['hexalith.io/ext-secrets-1-binding-sha256'] for item in replaced]
        self.assertNotEqual(checksums[0], checksums[1])
        self.assertTrue(all(item['spec']['template']['spec']['containers'][0]['image'] == image for item in replaced))

    def test_install_lock_is_held_through_binding(self):
        held = False
        @contextmanager
        def lock(path):
            nonlocal held
            held = True
            try: yield
            finally: held = False
        (self.directory / 'created-resource-references.json').write_text('[]')
        tenants = [{'tenantId': tenant, 'sourceStreamSha256': 'original'} for tenant in provisioning.TENANTS]
        (self.directory / 'native-enrollment-receipts.json').write_text(json.dumps({'tenants': tenants}))
        plan = provisioning.plan_keys()
        for item in plan: item['phase'] = 'MetadataVerified'
        (self.directory / 'operational-key-plan.json').write_text(json.dumps(plan))
        installer = SimpleNamespace(evidence=self.directory, write=Mock())
        @contextmanager
        def forwarded(*args): yield 12345
        with patch.object(provisioning, 'journal_lock', side_effect=lock), patch.object(provisioning, 'forward', side_effect=forwarded), \
                patch.object(provisioning, 'verify', return_value={'tenantStreams': tenants}), patch.object(provisioning, 'ProviderMetadata'), \
                patch.object(provisioning, 'provision_keys'), patch.object(provisioning, 'install_data_credential', return_value={}), \
                patch.object(provisioning, 'verify_permissions'), patch.object(provisioning, 'bind', side_effect=lambda *args: self.assertTrue(held)) as bind:
            provisioning.install(installer, 'registry.hexalith.com/ext-secrets-1-inventory@sha256:' + 'a' * 64)
        bind.assert_called_once()
        self.assertFalse(held)

    def test_capture_rejects_altered_allowlist_component_and_uid(self):
        plan = self.native_created_plan()
        for item in plan:
            item['phase'] = 'MetadataVerified'
        allowed = {'permittedReferences': [{key: item[key] for key in ['tenantId', 'purpose', 'secretReference', 'secretField']} for item in plan]}
        component = provisioning.component_manifest()
        component['metadata']['uid'] = 'original-component'
        capture.verify_public_binding(plan, allowed, component, 'original-component')
        mutations = [lambda a, c: a['permittedReferences'].pop(), lambda a, c: a['permittedReferences'][0].update(secretReference='foreign'),
                     lambda a, c: c.update(scopes=['ext-secrets-1-inventory']), lambda a, c: c['spec']['metadata'].append({'name': 'vaultToken', 'value': 'offline'}),
                     lambda a, c: c['metadata'].update(uid='replacement')]
        for mutation in mutations:
            a, c = deepcopy(allowed), deepcopy(component)
            mutation(a, c)
            with self.assertRaises(RuntimeError):
                capture.verify_public_binding(plan, a, c, 'original-component')

    def test_capture_rejects_native_provider_controller_and_inventory_health_failures(self):
        state = {key: {'httpStatus': status} for key, status in {'nativeEventStoreReadiness': 200, 'inventoryLiveness': 200,
            'inventoryHealth': 200, 'inventoryReadiness': 503}.items()}
        state['inventoryHealth']['publicResponse'] = {'status': 'InstalledUnbound', 'qualificationPassed': False, 'dependencyAvailable': False,
            'independentProfilePresent': False, 'exactPermittedReferenceCount': 4, 'missingHmacInput': 'runtime.currentInventoryAuthorityReference'}
        state['openbao'] = [{'sealed': False, 'initialized': True, 'version': '2.6.2'} for _ in range(3)]
        state['controllers'] = [{'namespace': namespace, 'kind': 'Deployment', 'name': 'expected-controller', 'desired': 1, 'ready': 1} for namespace in [provisioning.NS, 'hexalith-memories', 'dapr-system']]
        state['expectedControllers'] = sorted('/'.join((item['kind'], item['namespace'], item['name'])) for item in state['controllers'])
        capture.verify_health(state)
        mutations = [lambda s: s['nativeEventStoreReadiness'].update(httpStatus=503), lambda s: s['inventoryLiveness'].update(httpStatus=500),
                     lambda s: s['inventoryReadiness'].update(httpStatus=200), lambda s: s['openbao'][0].update(sealed=True),
                     lambda s: s['controllers'][0].update(ready=0), lambda s: s['controllers'].pop(), lambda s: s['inventoryHealth']['publicResponse'].update(qualificationPassed=True)]
        for mutation in mutations:
            invalid = deepcopy(state)
            mutation(invalid)
            with self.assertRaises(RuntimeError):
                capture.verify_health(invalid)


if __name__ == '__main__':
    unittest.main()
