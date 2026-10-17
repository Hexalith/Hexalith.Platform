import base64
from copy import deepcopy
import json
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

import capture_native_installation as capture
import enroll_native_tenants as enroll
import initialize_native_registry as initialize
import install_current_inventory as inventory
import native_journal
import registry_redis
import renew_inventory_metadata as renewal


class ReviewProvisioningTests(unittest.TestCase):
    def setUp(self):
        self.temporary = TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)

    def registry(self, phase=None):
        registry = enroll.NativeRegistry.__new__(enroll.NativeRegistry)
        registry.installer = SimpleNamespace(evidence=self.directory)
        request = {'messageId': 'original-message', 'tenant': 'system', 'domain': 'tenants', 'aggregateId': 'actual-planned-tenant'}
        if phase:
            native_journal.durable_write(self.directory / 'native-command-original-message.json', {'request': request, 'phase': phase, 'originalStatus': None})
        registry.call = Mock(side_effect=lambda method, path, *args: {'messageId': 'original-message', 'status': 'Completed'} if method == 'GET' else {})
        return registry, request

    def test_interrupted_prepared_original_can_be_submitted_once(self):
        registry, request = self.registry('Prepared')
        registry.process(request)
        self.assertEqual(1, sum(call.args[0] == 'POST' for call in registry.call.call_args_list))
        registry.process(request)
        self.assertEqual(1, sum(call.args[0] == 'POST' for call in registry.call.call_args_list))

    def test_authoritatively_denied_original_reuses_exact_identity(self):
        registry, request = self.registry('AdmissionDenied')
        registry.process(request)
        self.assertEqual(request, registry.call.call_args_list[0].args[2])

    def test_uncertain_original_never_resubmits_on_missing_lookup(self):
        registry, request = self.registry('AdmissionStarted')
        registry.call.side_effect = RuntimeError('native-registry-request-failed:404:unavailable')
        with self.assertRaises(RuntimeError): registry.process(request)
        self.assertTrue(all(call.args[0] == 'GET' for call in registry.call.call_args_list))
        self.assertEqual(request, json.loads((self.directory / 'native-command-original-message.json').read_text())['request'])

    def test_atomic_journal_interruption_preserves_previous_original(self):
        path = self.directory / 'original.json'
        native_journal.durable_write(path, {'phase': 'Prepared'})
        with patch.object(native_journal.os, 'replace', side_effect=OSError('interrupted')):
            with self.assertRaises(OSError): native_journal.durable_write(path, {'phase': 'AdmissionStarted'})
        self.assertEqual({'phase': 'Prepared'}, json.loads(path.read_text()))
        self.assertEqual([path], list(self.directory.iterdir()))

    def test_failed_secret_creation_revokes_only_new_token_and_policy(self):
        calls = []
        def bao(*args, **kwargs):
            calls.append(args)
            return SimpleNamespace(stdout=json.dumps({'auth': {'client_token': 'offline-new-token', 'accessor': 'public-accessor', 'lease_duration': 86400}}).encode() if args[0] == 'write' else b'[]')
        installer = SimpleNamespace(evidence=self.directory, bao=bao, secret=Mock(side_effect=RuntimeError('secret-create-failed')))
        with patch.object(inventory, 'kube', return_value=SimpleNamespace(stdout=b'')), patch.object(inventory, 'application_request') as revoke:
            with self.assertRaises(RuntimeError): inventory.create_metadata_credential(installer)
        revoke.assert_called_once()
        self.assertEqual(('policy', 'delete', 'ext-secrets-1-inventory-metadata-read'), calls[-1])
        state = (self.directory / 'metadata-credential-provisioning.json').read_text()
        self.assertNotIn('offline-new-token', state)
        self.assertEqual('RevokedAndPolicyRemoved', json.loads(state)['phase'])

    def test_existing_secret_preflight_never_mints_token_or_policy(self):
        installer = SimpleNamespace(evidence=self.directory, bao=Mock(return_value=SimpleNamespace(stdout=b'[]')))
        with patch.object(inventory, 'kube', return_value=SimpleNamespace(stdout=b'secret/existing')):
            with self.assertRaises(RuntimeError): inventory.create_metadata_credential(installer)
        self.assertEqual([('policy', 'list', '-format=json')], [call.args for call in installer.bao.call_args_list])

    def test_complete_configuration_preserves_deleted_and_nonruntime_fields(self):
        before = {'schema': 'strict-v1', 'runtime': {'tenantA': None, 'tenantB': None, 'tenantInventoryReference': None, 'callTimeout': None, 'timing': 300}, 'qualification': {'acceptedTarget': 'settled'}}
        after = deepcopy(before); after['runtime'].update(tenantA='A', tenantB='B', tenantInventoryReference='native')
        self.assertTrue(capture.same_settled_configuration(before, after))
        for mutate in [lambda value: value['runtime'].pop('callTimeout'), lambda value: value.update(schema='foreign'), lambda value: value['qualification'].update(acceptedTarget='changed'), lambda value: value['runtime'].update(timing=301)]:
            changed = deepcopy(after); mutate(changed)
            self.assertFalse(capture.same_settled_configuration(before, changed))

    def test_initialization_failure_restores_only_recorded_new_writer_uids(self):
        installer = SimpleNamespace(evidence=self.directory, created=['Deployment/ext-secrets-1-isolated/eventstore', 'Deployment/ext-secrets-1-isolated/tenants'])
        def get(*args):
            if args[1] == 'pods': return {'items': []}
            return {'metadata': {'uid': 'new-' + args[2]}, 'spec': {'replicas': 2 if args[2] == 'tenants' else 1}}
        with patch.object(initialize, 'kjson', side_effect=get), patch.object(initialize, 'kube') as kube, patch.object(initialize, 'initialize_phases', side_effect=RuntimeError('backup-failed')):
            with self.assertRaises(RuntimeError): initialize.initialize(installer)
        self.assertEqual(['--replicas=0', '--replicas=0', '--replicas=1', '--replicas=2'], [call.args[-1] for call in kube.call_args_list])
        self.assertTrue(json.loads((self.directory / 'native-initialization-journal.json').read_text())['writersRestored'])

    def test_enrolled_registry_refuses_initialization_without_any_mutation(self):
        (self.directory / 'native-command-original.json').write_text('{}')
        with patch.object(initialize, 'kube') as kube:
            with self.assertRaises(RuntimeError): initialize.initialize(SimpleNamespace(evidence=self.directory))
        kube.assert_not_called()

    def test_retry_validates_completed_backup_and_roles_without_repeating_them(self):
        proof = {'nativeBackupReference': 'pvc#backup@sha256:original-checksum', 'restoreVerified': True, 'packageSourceCommit': 'original-package-commit'}
        native_journal.durable_write(self.directory / 'native-first-deployment-initialization.json', proof)
        maintenance = 'user registry-maintenance on >offline-password resetkeys resetchannels ~* -@all +ping +info +bgsave +lastsave +dbsize +scan +shutdown +acl|dryrun +acl|load'
        def field(name, key):
            return maintenance if key == 'users\\.acl' else 'offline-subject' if key == 'subject-id' else 'offline-password'
        installer = SimpleNamespace(evidence=self.directory, created=['Secret/ext-secrets-1-isolated/ext-secrets-1-registry-maintenance'], secret_field=field)
        role = {'id': 'native-role-id', 'name': 'GlobalAdministrator', 'description': 'Administrator of the dedicated isolated native tenant registry only.'}
        identity = Mock()
        def call(method, path, *args, **kwargs):
            self.assertEqual('GET', method)
            if '/roles/GlobalAdministrator' in path: return role, None
            if '/clients?' in path: return [{'id': 'native-client'}], None
            if '/protocol-mappers/models' in path: return [{'name': 'native-registry-administrator-role', 'protocolMapper': 'oidc-usermodel-realm-role-mapper', 'config': {'claim.name': 'role', 'access.token.claim': 'true'}}], None
            return [role], None
        identity.call.side_effect = call
        with patch.object(initialize, 'Idp', return_value=identity), patch.object(initialize, 'kube', return_value=SimpleNamespace(stdout=b'original-checksum  /data/backup')) as kube:
            result = initialize.initialize_phases(installer, {'backupVerified': True, 'administratorAssigned': True})
        self.assertEqual('original-package-commit', result['cutoverCommit'])
        self.assertEqual(1, kube.call_count)
        self.assertIn('sha256sum', kube.call_args.args)
        self.assertEqual(proof, json.loads((self.directory / 'native-first-deployment-initialization.json').read_text()))

    def test_recovery_skips_changed_uid_and_restores_other_recorded_writer(self):
        journal = {'writers': [{'name': 'eventstore', 'uid': 'original', 'replicas': 1}, {'name': 'tenants', 'uid': 'tenants-original', 'replicas': 2}]}
        with patch.object(initialize, 'kjson', side_effect=lambda *args: {'metadata': {'uid': 'changed' if args[2] == 'eventstore' else 'tenants-original'}}), patch.object(initialize, 'kube') as kube:
            with self.assertRaises(RuntimeError): initialize.restore_recorded_writers(SimpleNamespace(evidence=self.directory), journal)
        self.assertEqual(1, kube.call_count)
        self.assertEqual('tenants', kube.call_args.args[2])

    def test_native_redis_components_and_acls_are_workload_scoped(self):
        for app in ['eventstore', 'tenants']:
            for component in registry_redis.components(app):
                self.assertEqual([app], component['scopes'])
                metadata = {entry['name']: entry for entry in component['spec']['metadata']}
                self.assertEqual(app, metadata['redisPassword']['secretKeyRef']['key'])
            acl = registry_redis.native_acl(app, 'offline-password')
            self.assertNotIn(' ~* ', acl); self.assertNotIn('&*', acl)
            self.assertIn('~' + app + '||*', acl)
            self.assertNotIn('~' + ('tenants' if app == 'eventstore' else 'eventstore') + '||*', acl)

    def test_publish_failed_and_timed_out_stop_the_status_poll(self):
        for terminal in ('PublishFailed', 'TimedOut'):
            registry, request = self.registry('Admitted')
            registry.call.side_effect = lambda method, path, *args, status=terminal: {'messageId': 'original-message', 'status': status} if method == 'GET' else {}
            with self.assertRaisesRegex(RuntimeError, 'native-command-terminal:' + terminal):
                registry.process(request)
            self.assertEqual(1, sum(call.args[0] == 'GET' for call in registry.call.call_args_list))

    def test_hmac_data_renewal_refuses_other_policies_and_omits_the_token(self):
        import renew_hmac_data as hmac_renewal
        secret = b'offline-hmac-token'
        encoded = base64.b64encode(secret)
        with patch.object(hmac_renewal, 'kube', return_value=SimpleNamespace(stdout=encoded)), \
                patch.object(hmac_renewal, 'application_request', return_value=SimpleNamespace(stdout=json.dumps({'data': {'policies': ['other-policy']}}).encode())) as called:
            with self.assertRaisesRegex(RuntimeError, 'unexpected-isolated-data-credential-policy'):
                hmac_renewal.renew(self.directory)
        self.assertEqual(called.call_count, 1)
        lease = {'accessor': 'public-accessor', 'policies': ['ext-secrets-1-hmac-data-read'], 'ttl': 3600, 'explicit_max_ttl': 86400, 'renewable': True}
        responses = [{'data': lease}, {'auth': {'lease_duration': 3600, 'client_token': secret.decode()}}, {'data': lease}]
        with patch.object(hmac_renewal, 'kube', return_value=SimpleNamespace(stdout=encoded)), \
                patch.object(hmac_renewal, 'application_request', side_effect=lambda *_args, **_kwargs: SimpleNamespace(stdout=json.dumps(responses.pop(0)).encode())):
            hmac_renewal.renew(self.directory)
        journal = (self.directory / 'hmac-data-credential-renewal.json').read_bytes()
        self.assertNotIn(secret, journal)
        self.assertNotIn(encoded, journal)

    def test_credential_renewal_never_records_returned_token(self):
        response = {'auth': {'client_token': 'offline-private-token', 'accessor': 'public-accessor', 'lease_duration': 86400, 'renewable': True}}
        with patch.object(renewal, 'kube', return_value=SimpleNamespace(stdout=base64.b64encode(b'offline-private-token'))), patch.object(renewal, 'application_request', return_value=SimpleNamespace(stdout=json.dumps(response).encode())):
            renewal.renew(self.directory)
        self.assertNotIn('offline-private-token', (self.directory / 'metadata-credential-renewal.json').read_text())


if __name__ == '__main__': unittest.main()
