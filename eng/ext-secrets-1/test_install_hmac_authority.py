"""Offline safety/recovery checks. No cluster or EXT-SECRETS-1 contract operation is invoked."""
import json
from pathlib import Path
import tempfile
import types
import unittest
from unittest.mock import patch

import install_hmac_authority as authority

IMAGE = 'registry.hexalith.com/ext-secrets-1-hmac-authority@sha256:' + 'a' * 64


class AuthorityInstallationTests(unittest.TestCase):
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
                    return types.SimpleNamespace(stdout=b'secret/new-credential')
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
                    return types.SimpleNamespace(stdout=b'{"data":{"policies":["ext-secrets-1-inventory-metadata-read"]}}')
                return types.SimpleNamespace(stdout=b'["deny"]')
            with patch.object(installer, 'bao', side_effect=provision) as provider, patch.object(installer, 'secret'), patch.object(authority, 'application_request', side_effect=application), patch.object(authority, 'delete_matching_credential_secret') as remove:
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
            with patch.object(authority, 'application_request', side_effect=RuntimeError('native-revoke-unavailable')), patch.object(installer, 'bao') as provider:
                with self.assertRaisesRegex(RuntimeError, 'native-revoke-unavailable'):
                    installer.close()
                provider.assert_not_called()
            journal = json.loads((Path(directory) / 'metadata-observer-issuance.json').read_text())
            self.assertEqual(journal['phase'], 'CredentialCleanupPending')
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
