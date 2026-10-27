"""Offline safety/recovery checks. No cluster or EXT-SECRETS-1 contract operation is invoked."""
import json
import io
from copy import deepcopy
from pathlib import Path
import tempfile
import types
import unittest
from unittest.mock import Mock, patch

import install_hmac_authority as authority

IMAGE = 'registry.hexalith.com/ext-secrets-1-hmac-authority@sha256:' + 'a' * 64


class AuthorityInstallationTests(unittest.TestCase):
    def test_administration_response_accepts_full_repository_capacity_and_rejects_overflow_or_error(self):
        installer = Mock()
        installer.secret_field.return_value = 'synthetic-private-channel'
        prefix, suffix = b'{"retainedAudit":"', b'"}'
        limit = authority.ADMINISTRATION_RESPONSE_LIMIT
        self.assertEqual(limit, 16 * 1024 * 1024)
        for size, status, accepted in [(1024 * 1024 + 100, 200, True), (limit, 200, True), (limit + 1, 200, False), (20, 500, False)]:
            raw = prefix + b'x' * (size - len(prefix) - len(suffix)) + suffix
            response = Mock(status=status)
            response.read.side_effect = io.BytesIO(raw).read
            connection = Mock()
            connection.getresponse.return_value = response
            with self.subTest(size=size, status=status), patch.object(authority, 'Idp') as idp, patch.object(authority.http.client, 'HTTPConnection', return_value=connection):
                idp.return_value.call.return_value = ({'access_token': 'synthetic-private-access'}, {})
                if accepted:
                    returned_status, document = authority.administration_request(installer, 'GET', '/admin/state')
                    self.assertEqual(returned_status, 200)
                    self.assertEqual(len(document['retainedAudit']), size - len(prefix) - len(suffix))
                else:
                    with self.assertRaisesRegex(RuntimeError, 'authority-administration-failed'):
                        authority.administration_request(installer, 'GET', '/admin/state')
            response.read.assert_called_once_with(limit + 1)
            connection.close.assert_called_once()

    def test_creation_returns_native_response_identity_without_later_metadata_get(self):
        with tempfile.TemporaryDirectory() as directory:
            installer = self.instance(directory)
            with patch.object(authority, 'kube', side_effect=[types.SimpleNamespace(stdout=b''), types.SimpleNamespace(stdout=b'created-uid 7')]) as native:
                identity = installer.secret('original', {'token': 'synthetic-token'})
            self.assertEqual(identity, {'uid': 'created-uid', 'resourceVersion': '7'})
            self.assertEqual(native.call_count, 2)
            self.assertEqual(native.call_args.args[0], 'create')
            self.assertIn('jsonpath={.metadata.uid}{" "}{.metadata.resourceVersion}', native.call_args.args)

    def test_recovery_checks_declared_public_intent_but_allows_server_defaults(self):
        with tempfile.TemporaryDirectory() as directory:
            installer = self.instance(directory)
            obj = {'apiVersion': 'v1', 'kind': 'Service', 'metadata': {'name': 'original'},
                'spec': {'selector': {'app': 'original'}, 'ports': [{'port': 8080}]}}
            def create(*args, **kwargs):
                if args[0] != 'create': return types.SimpleNamespace(stdout=b'')
                created = json.loads(kwargs['data'])
                created['metadata'].update(uid='created-uid', resourceVersion='7')
                created['spec'].update(clusterIP='10.0.0.1')
                created['spec']['ports'][0]['protocol'] = 'TCP'
                return types.SimpleNamespace(stdout=json.dumps(created).encode())
            with patch.object(authority, 'kube', side_effect=create):
                installer.create(obj)
            observed = deepcopy(installer.resources[0]['createdPublicIntent'])
            observed['metadata'].update(uid='created-uid', resourceVersion='9')
            observed['spec'].update(clusterIP='10.0.0.1')
            observed['spec']['ports'][0]['protocol'] = 'TCP'
            with patch.object(authority, 'kjson', return_value=observed), patch.object(authority, 'kube') as native:
                self.assertEqual(installer.create(obj)['uid'], 'created-uid')
            native.assert_not_called()
            observed['spec']['ports'][0]['port'] = 8081
            with patch.object(authority, 'kjson', return_value=observed), patch.object(authority, 'kube') as native:
                with self.assertRaisesRegex(RuntimeError, 'intent-changed'):
                    installer.create(obj)
            native.assert_not_called()

    def test_same_uid_new_behavior_field_cannot_be_adopted_as_server_default(self):
        with tempfile.TemporaryDirectory() as directory:
            installer = self.instance(directory)
            obj = {'apiVersion': 'apps/v1', 'kind': 'Deployment', 'metadata': {'name': 'original'}, 'spec': {'template': {'spec': {'containers': [{'name': 'app', 'image': 'exact-digest'}]}}}}
            def native(*args, **kwargs):
                if args[0] != 'create': return types.SimpleNamespace(stdout=b'')
                created = json.loads(kwargs['data'])
                created['metadata'].update(uid='created-uid', resourceVersion='7')
                return types.SimpleNamespace(stdout=json.dumps(created).encode())
            with patch.object(authority, 'kube', side_effect=native):
                installer.create(obj)
            observed = deepcopy(installer.resources[0]['createdPublicIntent'])
            observed['metadata'].update(uid='created-uid', resourceVersion='9')
            observed['spec']['template']['spec']['hostNetwork'] = True
            with patch.object(authority, 'kjson', return_value=observed):
                with self.assertRaisesRegex(RuntimeError, 'intent-changed'):
                    installer.create(obj)

    def test_legacy_public_intent_hash_cannot_be_fabricated_into_recovery_intent(self):
        with tempfile.TemporaryDirectory() as directory:
            installer = self.instance(directory)
            installer.resources = [{'reference': 'ConfigMap/' + authority.NS + '/original', 'phase': 'Created',
                'uid': 'created-uid', 'resourceVersion': '7', 'intentSha256': 'historical-hash-only'}]
            with patch.object(authority, 'kube') as native, patch.object(authority, 'kjson') as read:
                with self.assertRaisesRegex(RuntimeError, 'declared-intent-unavailable'):
                    installer.create({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'original'}, 'data': {'public': 'reference'}})
            native.assert_not_called()
            read.assert_not_called()

    def test_secret_recovery_rejects_new_version_even_with_original_uid(self):
        with tempfile.TemporaryDirectory() as directory:
            installer = self.instance(directory)
            with patch.object(authority, 'kube', side_effect=[types.SimpleNamespace(stdout=b''), types.SimpleNamespace(stdout=b'created-uid 7')]):
                installer.secret('original', {'token': 'synthetic-token'})
            with patch.object(authority, 'secret_identity', return_value={'uid': 'created-uid', 'resourceVersion': '8'}):
                with self.assertRaisesRegex(RuntimeError, 'identity-changed'):
                    installer.secret('original', {'token': 'synthetic-token'})

    def instance(self, directory):
        value = authority.AuthorityInstaller.__new__(authority.AuthorityInstaller)
        value.evidence = Path(directory)
        value.run = {'runId': 'original-run', 'image': IMAGE}
        value.resources = []
        value.created = []
        value.metadata_token = None
        value.metadata_credential_verified = False
        value.token = bytearray(b'offline-provisioner')
        value.journal_path = value.evidence / 'resource-journal.json'
        return value

    def test_image_requires_exact_own_repository_digest(self):
        self.assertEqual(authority.exact_image(IMAGE), IMAGE)
        for value in ['registry.hexalith.com/ext-secrets-1-hmac-authority:latest', IMAGE.replace('hmac-authority', 'inventory'), IMAGE[:-1], IMAGE.replace('registry.hexalith.com', 'foreign.example')]:
            with self.assertRaises(ValueError):
                authority.exact_image(value)

    def test_secret_creation_journals_original_intent_before_mutation_and_retains_only_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            installer = self.instance(directory)
            def native(*args, **kwargs):
                if args[0] == 'create':
                    journal = json.loads(installer.journal_path.read_text())
                    self.assertEqual(journal[0]['phase'], 'CreationStarted')
                    self.assertNotIn('credential-value', installer.journal_path.read_text())
                    return types.SimpleNamespace(stdout=b'uid-original 7')
                return types.SimpleNamespace(stdout=b'uid-original 7' if any('jsonpath' in item for item in args) else b'')
            with patch.object(authority, 'kube', side_effect=native):
                installer.secret('new-credential', {'token': 'credential-value'})
            journal = json.loads(installer.journal_path.read_text())
            self.assertEqual(journal[0]['phase'], 'Created')
            self.assertEqual(journal[0]['uid'], 'uid-original')
            for path in Path(directory).iterdir():
                self.assertNotIn('credential-value', path.read_text())
                self.assertNotIn('Y3JlZGVudGlhbC12YWx1ZQ==', path.read_text())

    def test_uncertain_creation_preserves_original_journal_and_cannot_be_repeated(self):
        with tempfile.TemporaryDirectory() as directory:
            installer = self.instance(directory)
            with patch.object(authority, 'kube', side_effect=[types.SimpleNamespace(stdout=b''), RuntimeError('lost-response')]):
                with self.assertRaises(RuntimeError):
                    installer.secret('new-credential', {'token': 'not-recorded'})
            self.assertEqual(json.loads(installer.journal_path.read_text())[0]['phase'], 'CreationUnknown')
            with patch.object(authority, 'kube') as native:
                with self.assertRaisesRegex(RuntimeError, 'original-resource-effect-unknown'):
                    installer.secret('new-credential', {'token': 'replacement-refused'})
                native.assert_not_called()

    def test_preserved_name_is_never_adopted_or_changed(self):
        with tempfile.TemporaryDirectory() as directory:
            installer = self.instance(directory)
            with patch.object(authority, 'kube', return_value=types.SimpleNamespace(stdout=b'secret/preserved')) as native:
                with self.assertRaisesRegex(RuntimeError, 'preserved-resource-name-overlap'):
                    installer.secret('preserved', {'token': 'unused'})
                self.assertEqual(native.call_count, 1)
            self.assertEqual(installer.resources, [])

    def test_repeated_created_resource_requires_original_uid_and_ownership(self):
        with tempfile.TemporaryDirectory() as directory:
            installer = self.instance(directory)
            installer.resources = [{'reference': 'Secret/' + authority.NS + '/original', 'phase': 'Created', 'uid': 'original-uid'}]
            with patch.object(authority, 'kjson', return_value={'metadata': {'uid': 'replacement-uid', 'labels': {'hexalith.io/authority-installation': 'original-run'}}}), patch.object(authority, 'kube') as native:
                with self.assertRaisesRegex(RuntimeError, 'original-resource-identity-changed'):
                    installer.secret('original', {'token': 'unused'})
                native.assert_not_called()

    def test_owner_lost_response_retry_keeps_original_id_expected_revision_and_payload(self):
        with tempfile.TemporaryDirectory() as directory:
            installer = self.instance(directory)
            calls = []
            def request(instance, method, path, body=None):
                if method == 'GET':
                    return 200, {'revision': 'original-revision'}
                original = json.loads((Path(directory) / 'owner-operation-PublishProfile.json').read_text())
                self.assertEqual(original['operation'], body)
                calls.append(body.copy())
                if len(calls) == 1:
                    raise RuntimeError('lost-response')
                return 200, {'operationId': body['operationId'], 'outcome': 'Applied', 'revision': 'committed-revision'}
            with patch.object(authority, 'administration_request', side_effect=request):
                with self.assertRaisesRegex(RuntimeError, 'lost-response'):
                    authority.owner_operation(installer, 'PublishProfile', profile={'version': 'original-profile'})
                result = authority.owner_operation(installer, 'PublishProfile', profile={'version': 'must-not-replace-original'})
            self.assertEqual(calls[0], calls[1])
            self.assertEqual(result['phase'], 'Committed')
            self.assertEqual(result['operation']['profile']['version'], 'original-profile')

    def test_blocker_ledger_covers_every_remaining_null_with_owner_and_action(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            historical = root / 'custody-inventory-20261016/refresh-20261027T170637456955Z'
            historical.mkdir(parents=True)
            (historical / 'blockers.json').write_text(json.dumps({'blockers': [{'input': 'runtime.hmacCredentialReference', 'owner': 'Provider owner', 'action': 'Supply a current restricted credential.'}]}))
            runtime = root / 'isolated-runtime-20261016/runtime-reference.json'
            runtime.parent.mkdir()
            installer = self.instance(directory)
            with patch.object(authority, 'RUNTIME', runtime):
                authority.write_blockers(installer, {'runtime': {'hmacCredentialReference': None, 'currentInventoryAuthorityReference': None}, 'qualification': {'app': None}}, [])
            result = json.loads((root / 'blockers.json').read_text())
            self.assertEqual(result['nullReferenceCount'], 3)
            null_inputs = {item['input'] for item in result['blockers']}
            self.assertTrue({'runtime.hmacCredentialReference', 'runtime.currentInventoryAuthorityReference', 'qualification.app'} <= null_inputs)
            self.assertTrue(all(item['owner'] and item['action'] for item in result['blockers']))

    def test_metadata_observer_issuance_is_journaled_before_effect_and_restricted_then_revoked(self):
        with tempfile.TemporaryDirectory() as directory:
            installer = self.instance(directory)
            def provision(*args, **kwargs):
                journal = json.loads((Path(directory) / 'metadata-observer-issuance.json').read_text())
                self.assertEqual(journal['phase'], 'IssuanceStarted')
                self.assertEqual(args[:3], ('write', '-format=json', 'auth/token/create-orphan'))
                request = json.loads(kwargs['data'])
                self.assertEqual(request['policies'], ['ext-secrets-1-inventory-metadata-read'])
                self.assertFalse(request['renewable'])
                return types.SimpleNamespace(stdout=json.dumps({'auth': {'client_token': 'offline-observer-credential', 'accessor': 'original-public-accessor', 'lease_duration': 1200}}).encode())
            def application(token, *args, **kwargs):
                if args[:2] == ('token', 'lookup'):
                    return types.SimpleNamespace(stdout=b'{"data":{"accessor":"original-public-accessor","policies":["ext-secrets-1-inventory-metadata-read"]}}')
                return types.SimpleNamespace(stdout=b'["deny"]')
            with patch.object(installer, 'bao', side_effect=provision) as provider, patch.object(installer, 'secret', return_value={'uid': 'original-secret', 'resourceVersion': '7'}), patch.object(authority, 'application_request', side_effect=application), patch('install_current_inventory.application_request', side_effect=application), patch('install_current_inventory.delete_matching_credential_secret') as remove:
                installer.metadata_credential()
                installer.close()
                self.assertEqual(provider.call_count, 1)
                remove.assert_called_once()
            journal = json.loads((Path(directory) / 'metadata-observer-issuance.json').read_text())
            self.assertEqual(journal['phase'], 'RevokedAndOwnedSecretRemoved')
            self.assertFalse(journal['existingPolicyChanged'])
            self.assertIsNone(installer.metadata_token)
            self.assertNotIn('offline-observer-credential', json.dumps(journal))

    def test_unknown_observer_issuance_never_retries_or_changes_existing_policy(self):
        with tempfile.TemporaryDirectory() as directory:
            installer = self.instance(directory)
            with patch.object(installer, 'bao', side_effect=RuntimeError('lost-original-issuance-response')) as provider:
                with self.assertRaisesRegex(RuntimeError, 'lost-original-issuance-response'):
                    installer.metadata_credential()
                with self.assertRaisesRegex(RuntimeError, 'original-metadata-observer-issuance-needs-reconciliation'):
                    installer.metadata_credential()
                self.assertEqual(provider.call_count, 1)
            journal = json.loads((Path(directory) / 'metadata-observer-issuance.json').read_text())
            self.assertEqual(journal['phase'], 'IssuanceUnknown')
            self.assertFalse(journal['existingPolicyChanged'])

    def test_observer_cleanup_failure_retains_original_accessor_and_wipes_local_transport(self):
        with tempfile.TemporaryDirectory() as directory:
            installer = self.instance(directory)
            installer.metadata_token = bytearray(b'offline-observer-credential')
            installer.write('metadata-observer-issuance.json', {'phase': 'Stored', 'accessor': 'original-public-accessor', 'existingPolicyChanged': False})
            with patch('install_current_inventory.application_request', side_effect=RuntimeError('native-revoke-unavailable')), patch.object(installer, 'bao') as provider:
                with self.assertRaisesRegex(RuntimeError, 'native-revoke-unavailable'):
                    installer.close()
                provider.assert_not_called()
            journal = json.loads((Path(directory) / 'metadata-observer-issuance.json').read_text())
            self.assertEqual(journal['revocationPhase'], 'Started')
            self.assertEqual(journal['accessor'], 'original-public-accessor')
            self.assertIsNone(installer.metadata_token)

    def test_missing_metadata_identity_returns_explicit_unknown_mappings_without_preventing_independent_authority(self):
        with tempfile.TemporaryDirectory() as directory:
            installer = self.instance(directory)
            allowlist = {'data': {'permitted-references.json': json.dumps({'permittedReferences': [
                {'secretReference': 'tenants/tenant-a/TrustedEnvelope/operational', 'tenantId': 'tenant-a', 'purpose': 'TrustedEnvelope'}]})}}
            with patch.object(authority, 'kjson', return_value=allowlist), patch.object(authority, 'application_request') as native:
                result = authority.native_metadata(installer)
                native.assert_not_called()
            self.assertEqual(result[0]['observation'], 'Unavailable')
            self.assertIsNone(result[0]['versionMappings'])
            self.assertIsNone(result[0]['metadataSha256'])


if __name__ == '__main__':
    unittest.main()
