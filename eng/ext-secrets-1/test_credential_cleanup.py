"""Offline original-identity and acknowledged-cleanup regressions; no real credential or cluster."""
import base64
import json
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

import install_current_inventory as subject
from native_journal import durable_write

IDENTITY = {'uid': 'original-creation', 'resourceVersion': '7'}


class CredentialCleanupTests(unittest.TestCase):
    def test_same_token_in_recreated_or_modified_secret_is_never_deleted(self):
        for observed in (b'recreated-uid 7', b'original-creation 8'):
            with patch.object(subject, 'kube', return_value=SimpleNamespace(stdout=observed)) as native:
                with self.assertRaisesRegex(RuntimeError, 'identity-changed'):
                    subject.delete_matching_credential_secret('original', bytearray(b'synthetic-token'), identity=IDENTITY)
            self.assertEqual(native.call_count, 1)
            self.assertEqual(native.call_args.args[0], 'get')

    def test_missing_original_identity_does_not_observe_or_delete_any_secret(self):
        with patch.object(subject, 'kube') as native:
            with self.assertRaisesRegex(RuntimeError, 'creation-identity-required'):
                subject.delete_matching_credential_secret('original', bytearray(b'synthetic-token'))
        native.assert_not_called()

    def test_cleanup_uses_original_delete_preconditions(self):
        with patch.object(subject, 'kube', side_effect=[SimpleNamespace(stdout=b'original-creation 7'), SimpleNamespace(stdout=b'')]) as native:
            subject.delete_matching_credential_secret('original', identity=IDENTITY)
        self.assertEqual(json.loads(native.call_args.kwargs['data'])['preconditions'], IDENTITY)

    def test_recovery_checks_creation_identity_before_decoding_token(self):
        raw = base64.b64encode(b'synthetic-token') + b' recreated-uid 7'
        with patch.object(subject, 'kube', return_value=SimpleNamespace(stdout=raw)):
            with self.assertRaisesRegex(RuntimeError, 'identity-changed'):
                subject.read_pinned_credential_secret('original', IDENTITY)

    def test_acknowledged_revocation_resumes_secret_cleanup_without_lookup_or_revoke(self):
        with TemporaryDirectory() as directory:
            path = Path(directory) / 'credential.json'
            durable_write(path, {'phase': 'Revoked', 'accessor': 'original-accessor', 'revocationPhase': 'Acknowledged',
                'secretIdentity': IDENTITY, 'secretCreationPhase': 'Created'})
            with patch.object(subject, 'application_request') as provider, patch.object(subject, 'delete_matching_credential_secret') as delete:
                result = subject.cleanup_journaled_credential(SimpleNamespace(), path, 'original')
            provider.assert_not_called()
            delete.assert_called_once_with('original', identity=IDENTITY)
            self.assertEqual(result['phase'], 'RevokedAndOwnedSecretRemoved')

    def test_lost_secret_delete_response_reconciles_absence_without_repeating_effect(self):
        with TemporaryDirectory() as directory:
            path = Path(directory) / 'credential.json'
            durable_write(path, {'phase': 'Stored', 'accessor': 'original-accessor', 'secretIdentity': IDENTITY, 'secretCreationPhase': 'Created'})
            def revoke(token, *args):
                self.assertEqual(json.loads(path.read_text())['revocationPhase'], 'Started')
                return SimpleNamespace(stdout=b'')
            def delete(*args, **kwargs):
                self.assertEqual(json.loads(path.read_text())['revocationPhase'], 'Acknowledged')
                raise RuntimeError('lost-delete-response')
            with patch.object(subject, 'application_request', side_effect=revoke) as provider, patch.object(subject, 'delete_matching_credential_secret', side_effect=delete):
                with self.assertRaisesRegex(RuntimeError, 'lost-delete'):
                    subject.cleanup_journaled_credential(SimpleNamespace(), path, 'original', bytearray(b'synthetic-token'))
            self.assertEqual(provider.call_count, 1)
            with patch.object(subject, 'application_request') as provider, patch.object(subject, 'secret_identity', return_value=None), patch.object(subject, 'delete_matching_credential_secret') as delete:
                result = subject.cleanup_journaled_credential(SimpleNamespace(), path, 'original')
            provider.assert_not_called()
            delete.assert_not_called()
            self.assertEqual(result['secretRemovalPhase'], 'Acknowledged')

    def test_uncertain_revocation_never_retries_or_reads_secret(self):
        with TemporaryDirectory() as directory:
            path = Path(directory) / 'credential.json'
            durable_write(path, {'phase': 'Stored', 'accessor': 'original-accessor', 'secretIdentity': IDENTITY})
            with patch.object(subject, 'application_request', side_effect=RuntimeError('lost-revocation')) as provider:
                with self.assertRaisesRegex(RuntimeError, 'lost-revocation'):
                    subject.cleanup_journaled_credential(SimpleNamespace(), path, 'original', bytearray(b'synthetic-token'))
            self.assertEqual(provider.call_count, 1)
            with patch.object(subject, 'application_request') as provider, patch.object(subject, 'read_pinned_credential_secret') as read:
                with self.assertRaisesRegex(RuntimeError, 'needs-reconciliation'):
                    subject.cleanup_journaled_credential(SimpleNamespace(), path, 'original')
            provider.assert_not_called()
            read.assert_not_called()


if __name__ == '__main__':
    unittest.main()
