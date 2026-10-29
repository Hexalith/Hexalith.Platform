"""Offline recovery-scope regression tests; no live state or credentials."""
from copy import deepcopy
import json
from pathlib import Path
import tempfile
import unittest
import uuid

import prepare_expired_hmac_recovery as recovery


class RecoveryScopeTests(unittest.TestCase):
    def setUp(self):
        self.run = '22222222222222222222222222222222'
        self.prepared = '2026-10-29T17:00:00+00:00'
        identity = '11111111111111111111111111111111'
        self.state = {'instanceId': identity, 'generation': 19, 'revision': identity + ':19',
            'audit': [{'operationId': uuid.uuid5(uuid.UUID(identity), str(n)).hex,
                'previousRevision': identity + ':' + str(n), 'revision': identity + ':' + str(n + 1),
                'administratorId': 'ext-secrets-1-hmac-owner', 'outcome': 'Applied'} for n in range(19)],
            'profile': {'version': 'original-profile', 'notBefore': '2026-10-27T17:00:00Z',
                        'validUntil': '2026-10-28T17:00:00Z', 'revoked': False},
            'callers': [{'callerId': caller, 'enabled': True, 'notBefore': '2026-10-27T17:00:00Z',
                         'validUntil': '2026-10-28T17:00:00Z'} for caller in ('ext-secrets-1-hmac-inventory', 'ext-secrets-1-hmac-custody')],
            'hosts': [{'enrolled': True, 'notBefore': '2026-10-27T17:00:00Z', 'validUntil': '2026-10-28T17:00:00Z'} for _ in range(2)],
            'keyStatuses': []}
        self.runtime = {'runtime': {'profileVersion': 'original-profile', 'profileValidUntil': '2026-10-28T17:00:00Z',
            'timing': {'L': 300, 'S': 30, 'O': 600, 'H': 86400, 'R': 604800},
            'overlappingExportDisposition': 'Open', 'decisionKeysOutsideCustody': True,
            'tenantA': 'tenant-a', 'tenantB': 'tenant-b', 'actorRoleAuthorityReference': None}}
        self.keys = []
        for tenant in ('tenant-a', 'tenant-b'):
            for purpose in ('TrustedEnvelope', 'ContentDigest'):
                version = tenant + purpose
                self.keys.append({'original': {'tenantId': tenant, 'purpose': purpose}, 'phase': 'MetadataVerified',
                    'opaqueVersion': version, 'secretVersion': '2', 'mapping': {'secretVersion': '2', 'state': 'Active',
                    'retiredAt': None, 'notBefore': '2026-10-27T18:00:00Z', 'verifyUntil': '2026-11-03T18:00:00Z'}})
                self.state['keyStatuses'].append({'tenantId': tenant, 'purpose': purpose, 'version': version,
                    'profileVersion': 'original-profile', 'revoked': False,
                    'notBefore': '2026-10-27T18:00:00Z', 'validUntil': '2026-10-28T17:00:00Z'})
        self.identities = {role: {'accessor': role + '-public-accessor', 'policy': policy}
            for role, policy in [('data', 'ext-secrets-1-hmac-data-read'), ('metadata', 'ext-secrets-1-inventory-metadata-read')]}

    def build(self):
        return recovery.build_scope(self.state, self.runtime, self.keys, self.identities, self.prepared, self.run)

    def test_activation_keeps_expired_profile_until_last_and_changes_only_deadlines(self):
        before = deepcopy(self.state)
        plan = self.build()
        operations = plan['nativeOwnerOperations']
        self.assertEqual(len(operations), 9)
        self.assertEqual(operations[-1]['kind'], 'PublishProfile')
        self.assertFalse(any(operation['kind'] == 'PublishProfile' for operation in operations[:-1]))
        for index, (operation, (_, original)) in enumerate(zip(operations, recovery.records(before))):
            field = recovery.FIELDS[operation['kind']]
            changed = deepcopy(operation[field]); changed['validUntil'] = original['validUntil']
            self.assertEqual(changed, original)
            self.assertEqual(operation['expectedRevision'], before['instanceId'] + ':' + str(19 + index))
        self.assertEqual(self.state, before)
        self.assertEqual(plan['originalReceipts'], before['audit'])
        self.assertEqual(plan['proposedWindow']['exclusiveDeadline'], '2026-10-30T17:00:00+00:00')
        self.assertIsNone(plan['proposedRuntimeForReuseOrRenew']['runtime']['actorRoleAuthorityReference'])

    def test_confirmed_ownership_never_grants_operation_approval_or_executor(self):
        plan = self.build()
        self.assertTrue(plan['confirmedOwnership']['confirmedByUser'])
        self.assertFalse(plan['confirmedOwnership']['operationApproval'])
        self.assertFalse(plan['implementationStillRequired']['liveRecoveryExecutorAvailable'])
        self.assertIsNone(plan['liveMutationCommand'])
        self.assertFalse(plan['dependencyAvailable'])

    def test_credential_branches_remain_unselected_and_exactly_scoped(self):
        plan = self.build()
        for role, branch in plan['credentialBranches'].items():
            self.assertIsNone(branch['freshLeaseState']); self.assertIsNone(branch['selectedBranch'])
            self.assertEqual(branch['reuse']['mutations'], [])
            body = branch['replace']['nativeRequest']['body']
            self.assertEqual(body['policies'], [self.identities[role]['policy']])
            self.assertTrue(body['no_default_policy'])
            self.assertEqual(body['ttl'], '24h'); self.assertEqual(body['explicit_max_ttl'], '168h')
            self.assertEqual(branch['replace']['revokeOriginal']['body']['accessor'], self.identities[role]['accessor'])

    def test_short_technical_window_requires_new_bounded_proposal(self):
        self.keys[0]['mapping']['verifyUntil'] = '2026-10-29T18:00:00Z'
        with self.assertRaises(ValueError): self.build()

    def test_changed_receipt_or_revoked_profile_denies_preparation(self):
        self.state['audit'][4]['previousRevision'] = 'foreign:0'
        with self.assertRaises(ValueError): self.build()
        self.setUp(); self.state['profile']['revoked'] = True
        with self.assertRaises(ValueError): self.build()

    def test_wrong_version_mapping_or_cross_profile_key_denies(self):
        self.keys[1]['mapping']['secretVersion'] = '3'
        with self.assertRaises(ValueError): self.build()
        self.setUp(); self.state['keyStatuses'][0]['profileVersion'] = 'other'
        with self.assertRaises(ValueError): self.build()

    def test_failure_containment_preserves_originals_and_conditional_revisions(self):
        plan = self.build()
        deny = plan['failureContainment']['proposedDenyOperations']
        self.assertEqual([op['expectedRevision'] for op in deny], [self.state['instanceId'] + ':28', self.state['instanceId'] + ':29'])
        self.assertTrue(all(op['caller']['enabled'] is False for op in deny))
        self.assertFalse(plan['failureContainment']['automaticRollbackToOldProfileOrCredential'])
        self.assertEqual(len({op['operationId'] for op in deny + plan['nativeOwnerOperations']}), 11)

    def test_duplicate_or_symlink_public_input_is_rejected_without_echo(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'public.json'
            path.write_text('{"field":1,"field":2}')
            with self.assertRaises(ValueError): recovery.read_public(path, {})
            path.write_text(json.dumps({'field': None}))
            alias = Path(directory) / 'alias.json'; alias.symlink_to(path)
            with self.assertRaises(ValueError): recovery.read_public(alias, {})


if __name__ == '__main__':
    unittest.main()
