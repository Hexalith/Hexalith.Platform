"""Offline faults for the approved recovery executor; fixtures never contact a cluster."""
from contextlib import contextmanager
from copy import deepcopy
from datetime import datetime, timezone, timedelta
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import recover_expired_hmac as r
import test_prepare_expired_hmac_recovery as scope_fixture


class Backend:
    def __init__(self, state, snapshot, provider):
        self.state, self.snapshot, self.provider = deepcopy(state), deepcopy(snapshot), deepcopy(provider)
        self.effects, self.groups = [], []
        self.fail_provider = False; self.wrong_echo = False; self.change_before_activation = False
        self.renewal_success = False; self.replacement_success = False; self.fail_data_issuance = False
        self.selected = {}; self.cleaned = []; self.revoked = []
        self.health_override = None
    def close(self): pass
    def observe_kubernetes(self): self.groups.append('kubernetes'); return {'snapshot': deepcopy(self.snapshot), 'registerSha256': 'register'}
    def observe_authority(self): self.groups.append('authority'); return deepcopy(self.state)
    def observe_administration(self): self.groups.append('administration'); return deepcopy(self.state)
    def observe_provider(self):
        self.groups.append('provider')
        if self.fail_provider: raise RuntimeError('private failure text must not escape')
        value = deepcopy(self.provider)
        if self.change_before_activation and len(self.effects) == 8: value['metadataSha256'] = 'changed'
        return value
    def observe_enrollment(self):
        self.groups.append('enrollment')
        return {'restrictedReadVerified': True, 'originalHashesMatch': True,
            'tenantStreams': [{'tenantId': tenant, 'sourceStreamSha256': sha, 'completeNativeEnrollmentRead': True}
                              for tenant, sha in r.ORIGINAL_TENANT_HASHES.items()]}
    def observe_health(self):
        self.groups.append('health')
        if self.health_override is not None: return deepcopy(self.health_override)
        return {name: {'/alive': {'status': 200}, '/health': {'status': 200,
            'body': {'qualificationPassed': False, 'dependencyAvailable': False}},
            '/ready': {'status': 200 if name == 'authority' or self.state['generation'] >= 28 else 503}}
            for name in ('authority', 'inventory', 'custody')}
    def verify_capabilities(self, role, accessor): pass
    def verify_selected_secret(self, journal, accessor): pass
    def lease(self, accessor):
        if accessor in self.selected: return deepcopy(self.selected[accessor])
        return deepcopy(next(value for value in self.provider['leases'].values() if value['lease'] and value['lease']['accessor'] == accessor))
    def administer(self, operation):
        for receipt in self.state['audit']:
            if receipt['operationId'] == operation['operationId']:
                result = deepcopy(receipt)
                if self.wrong_echo: result['payloadSha256'] = 'different'
                return result
        self.effects.append(deepcopy(operation))
        receipt = {'operationId': operation['operationId'], 'kind': operation['kind'], 'administratorId': 'ext-secrets-1-hmac-owner',
            'previousRevision': operation['expectedRevision'], 'revision': self.state['instanceId'] + ':' + str(self.state['generation'] + 1),
            'outcome': 'Applied', 'payloadSha256': r.digest(r.canonical(operation))}
        field = {'SetInventoryHost': ('hosts', 'host'), 'SetCaller': ('callers', 'caller'), 'SetKeyStatus': ('keyStatuses', 'keyStatus')}.get(operation['kind'])
        if field:
            collection, item = field; changed = deepcopy(operation[item])
            identity = {'hosts': ('inventoryAuthorityReference',), 'callers': ('callerId',), 'keyStatuses': ('tenantId', 'purpose', 'version')}[collection]
            self.state[collection] = [old for old in self.state[collection] if any(old[k] != changed[k] for k in identity)] + [changed]
        else: self.state['profile'] = deepcopy(operation['profile'])
        self.state['generation'] += 1; self.state['revision'] = receipt['revision']; self.state['audit'].append(receipt)
        return receipt
    def stage(self, *args): pass
    def rollout(self): pass
    def verify_bound(self, public): pass
    def renew(self, role, original):
        self.effects.append('renew')
        if not self.renewal_success: raise OSError('lost response')
        self.provider['leases'][role]['lease']['expire_time'] = '2026-10-31T00:00:00Z'
    def issue_successor(self, role, branch, path, before_effect):
        self.effects.append('issue-' + role)
        if not self.replacement_success or role == 'data' and self.fail_data_issuance: raise OSError('lost response')
        result = {'phase': 'Stored', 'original': branch['original'], 'accessor': 'new-' + role,
            'selectedSecret': branch['replace']['newSecretName'], 'credentialReference': branch['replace']['newCredentialReference'],
            'secretIdentity': {'uid': 'new-' + role, 'resourceVersion': '2'}}
        self.selected[result['accessor']] = {'status': 200, 'classification': 'Present', 'lease': {'accessor': result['accessor'],
            'policies': [branch['original']['policy']], 'ttl': 86400, 'explicit_max_ttl': 604800,
            'issue_time': '2026-10-29T17:00:00Z', 'expire_time': '2026-10-31T00:00:00Z'}}
        r.durable(path, result); return result
    def cleanup_unused_successor(self, role, credential, directory): self.cleaned.append(role)
    def revoke_original(self, role, credential, directory): self.revoked.append(role)


class RecoveryTests(unittest.TestCase):
    def setUp(self):
        fixture = scope_fixture.RecoveryScopeTests(); fixture.setUp()
        for key in fixture.keys:
            key['original']['secretReference'] = 'tenants/' + key['original']['tenantId'] + '/' + key['original']['purpose'] + '/operational'
        fixture.state['hosts'] = [{'inventoryAuthorityReference': name, 'callerId': caller, 'workloadReference': name + '@uid:fixture',
            'notBefore': '2026-10-27T17:00:00Z', 'validUntil': '2026-10-28T17:00:00Z', 'enrolled': True}
            for name, caller in [('inventory', 'ext-secrets-1-hmac-inventory'), ('custody', 'ext-secrets-1-hmac-custody')]]
        for role, identity in fixture.identities.items():
            identity.update(secretName='fixture-' + role, credentialReference='fixture/' + role,
                creationTime=1, issueTime='2026-10-29T00:00:00Z', explicitMaximumTtlSeconds=604800,
                secretIdentity={'uid': role, 'resourceVersion': '1'})
        self.scope = fixture.build(); self.base = deepcopy(fixture.state)
        self.clock = lambda: datetime(2026, 10, 29, 17, 0, 1, tzinfo=timezone.utc)
        self.temporary = tempfile.TemporaryDirectory(); self.addCleanup(self.temporary.cleanup)
        root = Path(self.temporary.name); inputs = root / 'provider-inventory-reconciliation-20261029TJ6KL9LyH'; inputs.mkdir()
        self.snapshot = {}
        self.scope['inputSha256'] = {}
        for name, value in [('authority-state.current.json', self.base), ('preservation-after.json', self.snapshot)]:
            path = inputs / name; path.write_bytes(r.canonical(value)); self.scope['inputSha256'][str(path)] = r.digest(path.read_bytes())
        self.scope['installedResourcePreconditions'] = {}; self.scope['unresolvedPreservationDifferences'] = {}
        self.scope['retainedProviderConfigurationSha256'] = 'config'; self.scope['retainedNativeMetadataSha256'] = 'metadata'
        self.scope['scopeSha256'] = r.digest(r.canonical(self.scope))
        # Explicitly inject a test-only approved digest; the production CLI keeps its fixed chat-approved digest.
        self.approval = patch.object(r, 'APPROVED', self.scope['scopeSha256']); self.approval.start(); self.addCleanup(self.approval.stop)
        self.provider = {'configurationSha256': 'config', 'metadataSha256': 'metadata', 'leases': {}}
        for role, branch in self.scope['credentialBranches'].items():
            original = branch['original']
            self.provider['leases'][role] = {'status': 200, 'classification': 'Present', 'lease': {'accessor': original['accessor'],
                'policies': [original['policy']], 'creation_time': 1, 'issue_time': original['issueTime'], 'explicit_max_ttl': 604800,
                'expire_time': '2026-10-31T00:00:00Z', 'ttl': 86400, 'renewable': True}}
        self.backend = Backend(self.base, self.snapshot, self.provider)
        self.registry = root / 'application-originals'
        self.run = r.Recovery(self.scope, root / 'run', self.backend, self.clock, registry_root=self.registry); self.addCleanup(self.run.close)

    def test_observation_collects_other_groups_after_provider_failure(self):
        self.backend.fail_provider = True
        result = self.run.observe()
        self.assertEqual(len(set(self.backend.groups)), 6)
        self.assertIsNotNone(result['facts']['enrollment']); self.assertIsNone(result['facts']['provider'])
        self.assertNotIn('private failure', json.dumps(result)); self.assertEqual(self.backend.effects, [])

    def test_missing_historical_evidence_stops_before_any_effect(self):
        with self.assertRaisesRegex(r.Blocked, 'historical-preservation'): self.run.apply(None)
        self.assertEqual(self.backend.effects, [])

    def test_unhealthy_or_incomplete_admission_stops_before_any_effect(self):
        original = self.backend.observe_health()
        faults = [('inventory', '/alive', 'status', 500), ('custody', '/health', 'status', 503),
                  ('authority', '/ready', 'status', 503), ('inventory', '/ready', 'status', 200),
                  ('custody', '/health', 'body', {'qualificationPassed': True, 'dependencyAvailable': False}),
                  ('inventory', '/health', 'body', {'qualificationPassed': False, 'dependencyAvailable': True})]
        for name, endpoint, field, value in faults:
            with self.subTest(name=name, endpoint=endpoint, field=field):
                self.backend.health_override = deepcopy(original)
                self.backend.health_override[name][endpoint][field] = value
                with self.assertRaises(r.Blocked): self.run.apply(None)
                self.assertEqual(self.backend.effects, [])
        for incomplete in ({}, {'authority': original['authority']}, {**original, 'custody': {}}):
            self.backend.health_override = incomplete
            with self.assertRaisesRegex(r.Blocked, 'complete-recovery-health'): self.run.apply(None)
            self.assertEqual(self.backend.effects, [])

    def test_rollout_liveness_failure_stops_before_owner_activation(self):
        unhealthy = self.backend.observe_health(); unhealthy['custody']['/alive']['status'] = 503
        with patch.object(self.run, 'preservation', return_value='fixture-accepted'), \
             patch.object(self.backend, 'rollout', side_effect=lambda: setattr(self.backend, 'health_override', unhealthy)):
            with self.assertRaisesRegex(r.Blocked, 'liveness-or-health'): self.run.apply('fixture')
        self.assertEqual(self.backend.effects, [])
        self.assertEqual(self.backend.state, self.base)

    def test_current_profile_requires_ready_consumers(self):
        for operation in self.scope['nativeOwnerOperations']: self.backend.administer(operation)
        self.run.require_health(self.backend.observe_health(), self.backend.state['profile'])
        self.backend.health_override = self.backend.observe_health()
        self.backend.health_override['inventory']['/ready']['status'] = 503
        with self.assertRaisesRegex(r.Blocked, 'readiness-mismatch'):
            self.run.require_health(self.backend.observe_health(), self.backend.state['profile'])

    def test_changed_original_source_journal_stops_even_with_unchanged_scope(self):
        path = Path(next(iter(self.scope['inputSha256']))); path.write_text('{}')
        with self.assertRaisesRegex(r.Blocked, 'input-journal-changed'): self.run.admit(self.run.observe(), None)

    def test_incoherent_progressed_host_denies_before_resumed_effect(self):
        self.backend.administer(self.scope['nativeOwnerOperations'][0]); self.backend.effects.clear()
        self.backend.state['hosts'][0]['workloadReference'] = 'foreign'
        with self.assertRaisesRegex(r.Blocked, 'coherent-progressed'): self.run.admit(self.run.observe(), None)
        self.assertEqual(self.backend.effects, [])

    def test_resume_requires_full_native_original_payload_receipt(self):
        self.backend.administer(self.scope['nativeOwnerOperations'][0]); self.backend.effects.clear(); self.backend.wrong_echo = True
        with self.assertRaisesRegex(r.Blocked, 'exact-original-payload'): self.run.admit(self.run.observe(), None)
        self.assertEqual(self.backend.effects, [])

    def test_new_drift_cannot_be_accepted_by_rehashing_current_snapshot(self):
        facts = self.run.observe()['facts']; facts['kubernetes']['snapshot']['Foreign/fixture/new'] = {'uid': 'new'}
        path = self.run.directory / 'decision.json'; path.write_text('{}')
        with self.assertRaisesRegex(r.Blocked, 'new-unaccepted-preservation-drift'): self.run.preservation(facts, path)

    def test_old_group_time_not_refreshed_by_later_observation_finish(self):
        observed = self.run.observe(); observed['groupObservedAt']['provider'] = (self.clock() - timedelta(seconds=31)).isoformat()
        with self.assertRaisesRegex(r.Blocked, 'observations-expired'): self.run.admit(observed, None)

    def test_delayed_receipt_replay_expires_admission_before_any_new_effect(self):
        self.backend.administer(self.scope['nativeOwnerOperations'][0]); self.backend.effects.clear()
        instant = self.clock(); self.run.clock = lambda: instant
        original = self.backend.administer
        def delayed(operation):
            nonlocal instant
            result = original(operation); instant += timedelta(seconds=31); return result
        with patch.object(self.backend, 'administer', side_effect=delayed), \
             patch.object(self.run, 'preservation', return_value='fixture'):
            with self.assertRaisesRegex(r.Blocked, 'observations-expired'):
                self.run.admit(self.run.observe(), 'fixture')
        self.assertEqual(self.backend.effects, [])

    def test_selected_credential_delay_denies_profile_publication(self):
        instant = self.clock(); self.run.clock = lambda: instant
        original = self.run.validate_selected
        def delayed(role, journal):
            nonlocal instant
            original(role, journal)
            if self.backend.state['generation'] == 27: instant += timedelta(seconds=31)
        with patch.object(self.run, 'preservation', return_value='fixture'), \
             patch.object(self.run, 'validate_selected', side_effect=delayed):
            with self.assertRaisesRegex(r.Blocked, 'observations-expired'): self.run.apply('fixture')
        self.assertEqual(self.backend.state['generation'], 27)
        self.assertFalse(any(item['kind'] == 'PublishProfile' for item in self.backend.effects))
        self.assertFalse((self.run.directory / 'completion.json').exists())

    def test_profile_journal_delay_is_rechecked_at_native_publication(self):
        instant = self.clock(); self.run.clock = lambda: instant
        original = r.durable
        def delayed(path, value):
            nonlocal instant
            original(path, value)
            if path.name == 'authority-8.json': instant += timedelta(seconds=31)
        with patch.object(self.run, 'preservation', return_value='fixture'), patch.object(r, 'durable', side_effect=delayed):
            with self.assertRaisesRegex(r.Blocked, 'observations-expired'): self.run.apply('fixture')
        self.assertEqual(self.backend.state['generation'], 27)

    def activate(self):
        for operation in self.scope['nativeOwnerOperations']: self.backend.administer(operation)
        self.backend.effects.clear()

    def test_lost_first_deny_response_and_journal_resume_only_remaining_denials(self):
        self.activate()
        unhealthy = self.backend.observe_health(); unhealthy['custody']['/alive']['status'] = 503
        self.backend.health_override = unhealthy
        original = self.backend.administer
        first = self.scope['failureContainment']['proposedDenyOperations'][0]
        lost = True
        def lost_response(operation):
            nonlocal lost
            result = original(operation)
            if operation == first and lost:
                lost = False; raise OSError('private lost response')
            return result
        with patch.object(self.backend, 'administer', side_effect=lost_response):
            with self.assertRaisesRegex(r.Blocked, 'liveness-or-health'): self.run.apply(None)
        self.assertEqual(self.backend.state['generation'], 29)
        (self.run.directory / 'authority-deny-0.json').unlink()
        (self.run.directory / 'containment.json').unlink()
        with self.assertRaises(r.Blocked): self.run.apply(None)
        self.assertEqual(self.backend.state['generation'], 30)
        self.assertTrue(all(item['enabled'] is False for item in self.backend.state['callers']))
        self.assertEqual([item['operationId'] for item in self.backend.effects],
                         [item['operationId'] for item in self.scope['failureContainment']['proposedDenyOperations']])
        self.assertEqual(self.backend.state['audit'][:19], self.base['audit'])
        self.assertEqual(r.load(self.run.directory / 'authority-deny-0.json')['receipt'], self.backend.state['audit'][28])
        self.assertFalse((self.run.directory / 'completion.json').exists())
        before = deepcopy(self.backend.state)
        with self.assertRaises(r.Blocked): self.run.apply(None)
        self.assertEqual(self.backend.state, before)
        self.assertFalse((self.run.directory / 'completion.json').exists())

    def test_unhealthy_activated_restart_disables_both_callers_without_completion(self):
        self.activate()
        self.backend.health_override = self.backend.observe_health()
        self.backend.health_override['inventory']['/health']['status'] = 503
        with self.assertRaisesRegex(r.Blocked, 'liveness-or-health'): self.run.apply(None)
        self.assertEqual(self.backend.state['generation'], 30)
        self.assertTrue(all(item['enabled'] is False for item in self.backend.state['callers']))
        self.assertEqual(self.backend.state['audit'][:19], self.base['audit'])
        self.assertFalse((self.run.directory / 'completion.json').exists())

    def test_persisted_containment_intent_cannot_reenter_normal_activation(self):
        self.activate(); r.durable(self.run.directory / 'containment.json', {'phase': 'Started'})
        with self.assertRaisesRegex(r.Blocked, 'containment-must-remain-denied'): self.run.apply(None)
        self.assertEqual(self.backend.state['generation'], 30)
        self.assertEqual([item['kind'] for item in self.backend.effects], ['SetCaller', 'SetCaller'])
        self.assertTrue(all(item['caller']['enabled'] is False for item in self.backend.effects))

    def test_authority_failure_never_suppresses_known_successor_cleanup_or_original_error(self):
        self.backend.replacement_success = True; self.backend.fail_data_issuance = True
        for role in ('data', 'metadata'): self.backend.provider['leases'][role] = {'status': 400, 'classification': 'Absent', 'lease': None}
        original = self.backend.observe_administration
        def unavailable_after_issue():
            if 'issue-data' in self.backend.effects: raise OSError('private authority error')
            return original()
        with patch.object(self.backend, 'observe_administration', side_effect=unavailable_after_issue), \
             patch.object(self.run, 'preservation', return_value='fixture'):
            with self.assertRaisesRegex(OSError, 'lost response'): self.run.apply('fixture')
        self.assertEqual(self.backend.cleaned, ['metadata'])
        evidence = r.load(self.run.directory / 'failure-reconciliation.json')
        self.assertEqual(evidence['authorityContainment'], 'AuthenticatedExactOriginalReconciliationRequired')
        self.assertNotIn('private', json.dumps(evidence))
        self.assertFalse((self.run.directory / 'completion.json').exists())

    def test_final_provider_delay_cannot_complete_after_deadline(self):
        instant = self.clock(); self.run.clock = lambda: instant
        original = self.backend.observe_provider
        def delayed():
            nonlocal instant
            result = original()
            if self.backend.state['generation'] == 28:
                instant = r.utc(self.scope['proposedWindow']['exclusiveDeadline'])
            return result
        with patch.object(self.run, 'preservation', return_value='fixture'), \
             patch.object(self.backend, 'observe_provider', side_effect=delayed):
            with self.assertRaises(r.Blocked): self.run.apply('fixture')
        self.assertFalse((self.run.directory / 'completion.json').exists())

    def test_final_health_delay_denies_stale_completion(self):
        instant = self.clock(); self.run.clock = lambda: instant
        original = self.backend.observe_health
        def delayed():
            nonlocal instant
            result = original()
            if self.backend.state['generation'] == 28: instant += timedelta(seconds=31)
            return result
        with patch.object(self.run, 'preservation', return_value='fixture'), \
             patch.object(self.backend, 'observe_health', side_effect=delayed):
            with self.assertRaisesRegex(r.Blocked, 'final-provider-or-health-observation-expired'): self.run.apply('fixture')
        self.assertFalse((self.run.directory / 'completion.json').exists())

    def test_unknown_renewal_is_not_repeated(self):
        role = 'data'; original = self.scope['credentialBranches'][role]['original']
        r.durable(self.run.directory / 'credential-data.json', {'phase': 'RenewalUnknown', 'original': original})
        with self.assertRaisesRegex(r.Blocked, 'never-repeated'): self.run.credential(role, {'provider': self.provider})
        self.assertEqual(self.backend.effects, [])

    def test_provider_change_before_final_publication_keeps_profile_expired(self):
        self.backend.change_before_activation = True
        with patch.object(self.run, 'preservation', return_value='fixture-accepted'):
            with self.assertRaisesRegex(r.Blocked, 'provider-metadata'): self.run.apply('fixture')
        self.assertEqual(len(self.backend.effects), 8)
        self.assertFalse(any(op['kind'] == 'PublishProfile' for op in self.backend.effects))
        self.assertEqual(self.backend.state['profile'], self.base['profile'])

    def test_known_provider_denial_preserves_other_public_facts(self):
        live = object.__new__(r.LiveBackend); live.scope = self.scope
        @contextmanager
        def provider():
            def request(method, path, body=None):
                if path.endswith('lookup-accessor'):
                    return 200, {'data': {'accessor': body['accessor'], 'ttl': 2}}
                return 403, {'errors': ['permission denied']}
            yield request
        live.provider = provider
        result = live.observe_provider()
        self.assertEqual(result['leases']['data']['status'], 200)
        self.assertEqual(result['capabilities']['metadata']['status'], 403)
        self.assertTrue(all(status == 403 for status in result['metadataHttpStatuses'].values()))
        self.assertIsNone(result['configurationSha256'])

    def test_unknown_issuance_is_not_repeated(self):
        role = 'metadata'; original = self.scope['credentialBranches'][role]['original']
        r.durable(self.run.directory / 'credential-metadata.json', {'phase': 'IssuanceUnknown', 'original': original})
        with self.assertRaisesRegex(r.Blocked, 'never-repeated'): self.run.credential(role, {'provider': self.provider})
        self.assertEqual(self.backend.effects, [])

    def test_full_apply_reuses_credentials_and_activates_profile_last(self):
        with patch.object(self.run, 'preservation', return_value='fixture-accepted'):
            self.run.apply('fixture')
        self.assertEqual(len(self.backend.effects), 9)
        self.assertEqual(self.backend.effects[-1]['kind'], 'PublishProfile')
        self.assertEqual(self.backend.state['audit'][:19], self.base['audit'])
        self.assertEqual(self.backend.state['generation'], 28)
        self.assertTrue((self.run.directory / 'completion.json').exists())

    def test_current_short_lease_renews_once_and_resume_verifies_selected(self):
        self.backend.renewal_success = True
        self.backend.provider['leases']['data']['lease']['expire_time'] = '2026-10-29T18:00:00Z'
        result = self.run.credential('data', {'provider': deepcopy(self.backend.provider)})
        self.assertEqual(result['phase'], 'Renewed'); self.assertEqual(self.backend.effects, ['renew'])
        self.run.credential('data', {'provider': deepcopy(self.backend.provider)})
        self.assertEqual(self.backend.effects, ['renew'])
        self.backend.provider['leases']['data']['classification'] = 'Absent'
        with self.assertRaisesRegex(r.Blocked, 'selected-credential-not-current'):
            self.run.credential('data', {'provider': deepcopy(self.backend.provider)})

    def test_replacement_validates_selected_successor_and_rejects_revoked_resume(self):
        self.backend.replacement_success = True
        self.backend.provider['leases']['metadata'] = {'status': 400, 'classification': 'Absent', 'lease': None}
        result = self.run.credential('metadata', {'provider': deepcopy(self.backend.provider)})
        self.assertEqual(result['phase'], 'Stored')
        self.assertEqual(self.backend.effects, ['issue-metadata'])
        self.backend.selected[result['accessor']]['classification'] = 'Absent'
        with self.assertRaisesRegex(r.Blocked, 'selected-credential-not-current'):
            self.run.credential('metadata', {'provider': deepcopy(self.backend.provider)})
        self.assertEqual(self.backend.effects, ['issue-metadata'])

    def test_partial_role_failure_retains_metadata_for_cleanup(self):
        self.backend.replacement_success = True; self.backend.fail_data_issuance = True
        for role in ('data', 'metadata'): self.backend.provider['leases'][role] = {'status': 400, 'classification': 'Absent', 'lease': None}
        with patch.object(self.run, 'preservation', return_value='fixture-accepted'):
            with self.assertRaises(OSError): self.run.apply('fixture')
        self.assertEqual(self.backend.cleaned, ['metadata'])
        self.assertEqual(self.backend.state['profile'], self.base['profile'])

    def test_native_publication_failure_revokes_only_known_new_accessor(self):
        live = object.__new__(r.LiveBackend); live.scope = self.scope; live.private = []
        requests = []
        @contextmanager
        def provider():
            def request(method, path, body):
                requests.append(path)
                if path.endswith('create-orphan'): return 200, {'auth': {'accessor': 'new-public-accessor', 'client_token': 'offline-fixture-token'}}
                return 204, {}
            yield request
        live.provider = provider
        live.i = SimpleNamespace(secret_identity=lambda _: None)
        def fail_publication(*args, **kwargs): raise OSError('lost create response')
        live.k = SimpleNamespace(kube=fail_publication)
        live.lease = lambda _: {'status': 200, 'lease': {'policies': ['ext-secrets-1-inventory-metadata-read'],
            'explicit_max_ttl': 604800, 'renewable': True, 'expire_time': '2026-10-31T00:00:00Z'}}
        live.verify_capabilities = lambda *args, **kwargs: None
        path = self.run.directory / 'publication.json'
        with self.assertRaises(r.Blocked): live.issue_successor('metadata', self.scope['credentialBranches']['metadata'], path, lambda: None)
        journal = r.load(path)
        self.assertEqual(journal['phase'], 'PublicationUnknown')
        self.assertEqual(journal['cleanupRevocation'], 'Acknowledged')
        self.assertEqual(requests, ['/v1/auth/token/create-orphan', '/v1/auth/token/revoke-accessor'])
        self.assertNotIn(b'offline-fixture-token', path.read_bytes())
        self.assertTrue(all(not any(value) for value in live.private))

    def test_cutover_journal_forbids_successor_cleanup_even_without_current_reference(self):
        live = object.__new__(r.LiveBackend)
        r.durable(self.run.directory / 'object-Deployment_fixture.json', {'phase': 'Started'})
        live.cleanup_unused_successor('data', {'selectedSecret': 'new-data', 'accessor': 'new-accessor'}, self.run.directory)
        self.assertEqual(r.load(self.run.directory / 'cleanup-data.json')['phase'], 'ConsumerCutoverStartedPreserved')

    def test_generated_deployment_revision_is_only_normalized_difference(self):
        live = object.__new__(r.LiveBackend)
        actual = {'apiVersion': 'apps/v1', 'kind': 'Deployment', 'metadata': {'name': 'fixture', 'namespace': 'fixture',
            'uid': 'same', 'annotations': {'deployment.kubernetes.io/revision': '12'}}, 'spec': {'replicas': 1}}
        def public(value):
            result = deepcopy(value); result['metadata'].pop('uid'); result['metadata'].pop('annotations'); return result
        live.k = SimpleNamespace(kjson=lambda *args: actual); live.b = SimpleNamespace(public_object=public)
        journal = {'uid': 'same', 'intended': public(actual)}
        self.assertTrue(live.verify_planned_object('Deployment/fixture/fixture', journal, r.public_identity(actual, 'same')))
        actual['spec']['replicas'] = 2
        self.assertFalse(live.verify_planned_object('Deployment/fixture/fixture', journal, r.public_identity(actual, 'same')))

    def test_only_canonical_native_absence_is_classified_absent(self):
        self.assertEqual(r.LiveBackend.public_lease(400, {'errors': ['invalid accessor']})['classification'], 'Absent')
        self.assertEqual(r.LiveBackend.public_lease(400, {'errors': ['1 error occurred:\n\t* invalid accessor\n\n']})['classification'], 'Absent')
        for status, message in [(400, 'permission denied'), (403, 'invalid accessor'), (400, 'failed to find accessor')]:
            self.assertEqual(r.LiveBackend.public_lease(status, {'errors': [message]})['classification'], 'Unknown')

    def test_full_apply_replacement_keeps_keys_and_revokes_originals_after_activation(self):
        self.backend.replacement_success = True
        for role in ('data', 'metadata'): self.backend.provider['leases'][role] = {'status': 400, 'classification': 'Absent', 'lease': None}
        before_keys = deepcopy(self.scope['keyPlan'])
        with patch.object(self.run, 'preservation', return_value='fixture-accepted'): self.run.apply('fixture')
        self.assertEqual(self.backend.revoked, ['metadata', 'data'])
        self.assertEqual(self.backend.state['generation'], 28)
        self.assertEqual(self.scope['keyPlan'], before_keys)
        self.assertEqual(self.backend.effects[-1]['kind'], 'PublishProfile')

    def test_native_successor_publication_records_only_assigned_identity(self):
        live = object.__new__(r.LiveBackend); live.scope = self.scope; live.private = []
        @contextmanager
        def provider():
            yield lambda *args: (200, {'auth': {'accessor': 'new-accessor', 'client_token': 'offline-fixture-token'}})
        live.provider = provider; live.i = SimpleNamespace(secret_identity=lambda _: None)
        submitted = []
        def create(*args, **kwargs):
            submitted.append(json.loads(kwargs['data'])); return SimpleNamespace(stdout=b'assigned-uid assigned-rv')
        live.k = SimpleNamespace(kube=create, creation_identity=lambda raw: dict(zip(('uid', 'resourceVersion'), raw.decode().split())))
        live.lease = lambda _: {'status': 200, 'lease': {'policies': ['ext-secrets-1-hmac-data-read'], 'explicit_max_ttl': 604800,
            'renewable': True, 'expire_time': '2026-10-31T00:00:00Z'}}
        live.verify_capabilities = lambda *args, **kwargs: None
        path = self.run.directory / 'successful-publication.json'
        result = live.issue_successor('data', self.scope['credentialBranches']['data'], path, lambda: None)
        self.assertEqual(result['phase'], 'Stored')
        self.assertEqual(result['secretIdentity'], {'uid': 'assigned-uid', 'resourceVersion': 'assigned-rv'})
        self.assertEqual(submitted[0]['metadata']['name'], self.scope['credentialBranches']['data']['replace']['newSecretName'])
        self.assertNotIn(b'offline-fixture-token', path.read_bytes())
        self.assertTrue(all(not any(value) for value in live.private))

    def test_persistent_evidence_failure_after_issuance_still_revokes_known_accessor(self):
        live = object.__new__(r.LiveBackend); live.scope = self.scope; live.private = []
        requests = []
        @contextmanager
        def provider():
            def request(method, path, body):
                requests.append((path, body))
                if path.endswith('create-orphan'):
                    return 200, {'auth': {'accessor': 'known-issued-accessor', 'client_token': 'offline-issued-token'}}
                return 204, {}
            yield request
        live.provider = provider; live.i = SimpleNamespace(secret_identity=lambda _: None)
        path = self.run.directory / 'persistent-evidence-failure.json'
        original = r.durable
        def failed_storage(destination, value):
            if destination == path and value['phase'] != 'IssuanceStarted': raise OSError('private storage failure')
            return original(destination, value)
        with patch.object(r, 'durable', side_effect=failed_storage):
            with self.assertRaisesRegex(r.Blocked, 'original-needs-reconciliation'):
                live.issue_successor('metadata', self.scope['credentialBranches']['metadata'], path, lambda: None)
        self.assertEqual([item[0] for item in requests], ['/v1/auth/token/create-orphan', '/v1/auth/token/revoke-accessor'])
        self.assertEqual(requests[-1][1], {'accessor': 'known-issued-accessor'})
        self.assertEqual(r.load(path)['phase'], 'IssuanceStarted')
        self.assertNotIn(b'offline-issued-token', path.read_bytes())
        self.assertTrue(all(not any(value) for value in live.private))

    def staging_backend(self):
        live = object.__new__(r.LiveBackend); live.scope = self.scope
        objects, submitted = {}, []
        def public(value):
            result = deepcopy(value); result.pop('status', None)
            result['metadata'] = {key: item for key, item in result['metadata'].items()
                                  if key in ('name', 'namespace', 'labels', 'annotations')}
            return result
        for index, name in enumerate(('inventory', 'ext-secrets-1-custody')):
            reference = 'Deployment/' + r.NS + '/' + name
            volumes = [{'name': 'metadata', 'secret': {'secretName': self.scope['credentialBranches']['metadata']['original']['secretName']}},
                       {'name': 'unrelated', 'secret': {'secretName': 'unrelated-secret'}}]
            if name != 'inventory':
                volumes.append({'name': 'data', 'secret': {'secretName': self.scope['credentialBranches']['data']['original']['secretName']}})
            objects[reference] = {'apiVersion': 'apps/v1', 'kind': 'Deployment', 'metadata': {'name': name,
                'namespace': r.NS, 'uid': 'original-' + name, 'resourceVersion': str(100 + index)},
                'spec': {'replicas': 1, 'template': {'metadata': {'annotations': {'preserved': 'annotation'}},
                    'spec': {'volumes': volumes, 'containers': [{'name': name, 'image': 'fixture@sha256:unchanged',
                        'volumeMounts': [{'name': volume['name'], 'mountPath': '/mounted/' + volume['name']} for volume in volumes]}]}}},
                'status': {'readyReplicas': 1}}
            self.scope['installedResourcePreconditions'][reference] = r.public_identity(objects[reference], objects[reference]['metadata']['uid'])
        def get(verb, kind, name, _, namespace):
            self.assertEqual(verb, 'get'); return deepcopy(objects[kind + '/' + namespace + '/' + name])
        def replace(*args, data):
            self.assertEqual(args, ('replace', '-f', '-'))
            intended = json.loads(data); reference = intended['kind'] + '/' + intended['metadata']['namespace'] + '/' + intended['metadata']['name']
            current = objects[reference]
            self.assertEqual(intended['metadata']['uid'], current['metadata']['uid'])
            self.assertEqual(intended['metadata']['resourceVersion'], current['metadata']['resourceVersion'])
            submitted.append(deepcopy(intended)); intended['metadata']['resourceVersion'] = str(int(current['metadata']['resourceVersion']) + 1)
            objects[reference] = intended
        live.k = SimpleNamespace(kjson=get, kube=replace); live.b = SimpleNamespace(public_object=public)
        return live, objects, submitted

    def test_real_staging_cuts_over_both_selected_mounts_and_preserves_everything_else(self):
        live, objects, submitted = self.staging_backend(); original = deepcopy(objects)
        credentials = {role: {'selectedSecret': 'selected-' + role} for role in ('metadata', 'data')}
        with patch.object(r, 'window', return_value=None):
            for reference in objects: live.stage(reference, {}, credentials, self.run.directory)
        self.assertEqual(len(submitted), 2)
        for reference, value in objects.items():
            old = original[reference]
            self.assertEqual(value['metadata']['uid'], old['metadata']['uid'])
            self.assertEqual(value['spec']['template']['spec']['containers'], old['spec']['template']['spec']['containers'])
            mounts = {volume['name']: volume['secret']['secretName'] for volume in value['spec']['template']['spec']['volumes']}
            self.assertEqual(mounts['metadata'], 'selected-metadata')
            self.assertEqual(mounts['unrelated'], 'unrelated-secret')
            if 'data' in mounts: self.assertEqual(mounts['data'], 'selected-data')
            else: self.assertNotIn('selected-data', mounts.values())
            self.assertEqual(value['spec']['template']['metadata']['annotations']['preserved'], 'annotation')
            self.assertEqual(value['spec']['template']['metadata']['annotations']['hexalith.io/expired-hmac-recovery'], self.scope['runId'])
            journal = r.load(self.run.directory / ('object-' + reference.replace('/', '_') + '.json'))
            self.assertEqual(journal['resourceVersion'], old['metadata']['resourceVersion'])
            self.assertEqual(journal['phase'], 'Committed')

    def test_interrupted_before_replace_admits_only_exact_original_then_resumes_staging(self):
        live, objects, submitted = self.staging_backend()
        reference = next(iter(objects)); original = deepcopy(objects[reference]); replace = live.k.kube
        credentials = {role: {'selectedSecret': 'selected-' + role} for role in ('metadata', 'data')}
        with patch.object(r, 'window', return_value=None), \
             patch.object(live.k, 'kube', side_effect=OSError('interrupted before submission')):
            with self.assertRaises(OSError): live.stage(reference, {}, credentials, self.run.directory)
        path = self.run.directory / ('object-' + reference.replace('/', '_') + '.json')
        journal = r.load(path)
        self.assertEqual(journal['phase'], 'Started'); self.assertEqual(submitted, [])
        self.backend.snapshot.update({key: r.public_identity(value, value['metadata']['uid']) for key, value in objects.items()})
        self.backend.verify_planned_object = live.verify_planned_object
        approved = deepcopy(self.scope); approved.pop('scopeSha256'); self.scope['scopeSha256'] = r.digest(r.canonical(approved))
        with patch.object(r, 'APPROVED', self.scope['scopeSha256']), patch.object(self.run, 'preservation', return_value='fixture'):
            self.run.admit(self.run.observe(), 'fixture')
        for mutation in ('uid', 'resourceVersion', 'spec'):
            objects[reference] = deepcopy(original)
            if mutation == 'spec': objects[reference]['spec']['replicas'] = 2
            else: objects[reference]['metadata'][mutation] = 'replacement'
            observed = r.public_identity(objects[reference], objects[reference]['metadata']['uid'])
            self.assertFalse(live.verify_planned_object(reference, journal, observed))
        objects[reference] = deepcopy(original)
        with patch.object(r, 'window', return_value=None): live.stage(reference, {}, credentials, self.run.directory)
        self.assertEqual(len(submitted), 1)
        self.assertEqual(r.load(path)['phase'], 'Committed')
        self.assertEqual(objects[reference]['spec']['template']['spec']['volumes'][0]['secret']['secretName'], 'selected-metadata')

    def native_enrollment_backend(self):
        live = object.__new__(r.LiveBackend); live.scope = deepcopy(self.scope); live.evidence = self.run.directory
        owner = self.run.directory.parent / 'maintenance/original-owner-receipts.json'
        owner.parent.mkdir(exist_ok=True); owner.write_text('{}')
        live.scope['inputSha256'][str(owner)] = r.digest(owner.read_bytes())
        native = owner.parent.parent / 'native-registry-inventory-20261017T082852516535Z/native-enrollment-receipts.json'
        native.parent.mkdir(exist_ok=True)
        tenants = [{'tenantId': tenant, 'sourceStreamSha256': sha} for tenant, sha in r.ORIGINAL_TENANT_HASHES.items()]
        native.write_bytes(r.canonical({'tenants': tenants}))
        result = {'restrictedReadVerified': True, 'tenantStreams': [{**item, 'completeNativeEnrollmentRead': True} for item in tenants]}
        @contextmanager
        def forward(*args): yield 1234
        live.p = SimpleNamespace(forward=forward)
        live.v = SimpleNamespace(verify=lambda *args, **kwargs: deepcopy(result))
        return live, native, tenants, result

    def test_native_enrollment_requires_pinned_bytes_before_reading_current_streams(self):
        live, native, _, _ = self.native_enrollment_backend()
        pin = r.digest(native.read_bytes())
        with patch.object(r, 'NATIVE_ENROLLMENT_SHA256', pin):
            self.assertTrue(live.observe_enrollment()['originalHashesMatch'])
            native.write_bytes(native.read_bytes() + b'\n')
            with self.assertRaisesRegex(r.Blocked, 'receipt-pin-mismatch'): live.observe_enrollment()

    def test_native_enrollment_rejects_empty_missing_duplicate_foreign_and_changed_hashes(self):
        live, native, tenants, result = self.native_enrollment_backend()
        foreign = deepcopy(tenants); foreign[0]['tenantId'] = 'foreign-tenant'
        changed = deepcopy(tenants); changed[0]['sourceStreamSha256'] = 'changed'
        for field in ('receipts', 'results'):
            for fault in ([], tenants[:1], [tenants[0], tenants[0]], foreign, changed, None):
                with self.subTest(field=field, fault=fault):
                    receipt_items = fault if field == 'receipts' else tenants
                    native.write_bytes(r.canonical({'tenants': receipt_items}))
                    result['tenantStreams'] = ([{**item, 'completeNativeEnrollmentRead': True} for item in fault]
                                               if field == 'results' and fault is not None else None if field == 'results'
                                               else [{**item, 'completeNativeEnrollmentRead': True} for item in tenants])
                    with patch.object(r, 'NATIVE_ENROLLMENT_SHA256', r.digest(native.read_bytes())):
                        with self.assertRaisesRegex(r.Blocked, 'exact-original-native-tenant-hashes'): live.observe_enrollment()

    def test_admission_does_not_accept_vacuous_native_enrollment_success_flags(self):
        observation = self.run.observe(); observation['facts']['enrollment']['tenantStreams'] = []
        with self.assertRaisesRegex(r.Blocked, 'exact-original-native-tenant-hashes'):
            self.run.admit(observation, None)

    def test_identical_public_spec_on_replacement_uid_never_commits_original(self):
        live = object.__new__(r.LiveBackend); live.scope = self.scope
        current = {'kind': 'ConfigMap', 'metadata': {'name': 'fixture', 'namespace': 'fixture', 'uid': 'replacement', 'resourceVersion': '2'}, 'data': {'runtime-reference.json': '{}'}}
        public = {'kind': 'ConfigMap', 'metadata': {'name': 'fixture', 'namespace': 'fixture'}, 'data': {'runtime-reference.json': '{}'}}
        live.k = SimpleNamespace(kjson=lambda *args: current)
        live.b = SimpleNamespace(public_object=lambda _: public)
        path = self.run.directory / 'object-ConfigMap_fixture_fixture.json'
        r.durable(path, {'phase': 'Started', 'uid': 'original', 'resourceVersion': '1', 'intended': public})
        with self.assertRaisesRegex(r.Blocked, 'object-uid-changed'):
            live.stage('ConfigMap/fixture/fixture', {}, {}, self.run.directory)
        self.assertEqual(r.load(path)['phase'], 'Started')

    def test_targeted_observation_cannot_admit_live_application(self):
        value = self.run.observe(['enrollment'])
        with self.assertRaisesRegex(r.Blocked, 'complete-current-observation-required'): self.run.admit(value, None)

    def owner_backend(self):
        live = object.__new__(r.LiveBackend); live.scope = self.scope; live.private = []; live.evidence = self.run.directory
        record = {'schema': 'EXT-SECRETS-1.owner-metadata-credential.v1', 'scopeSha256': r.APPROVED,
            'secretName': 'owner-metadata-read', 'credentialReference': r.NS + '/Secret/owner-metadata-read#token',
            'secretIdentity': {'uid': '11111111-1111-4111-8111-111111111111', 'resourceVersion': '123'},
            'accessor': 'OwnerPublicAccessor', 'policy': 'ext-secrets-1-inventory-metadata-read', 'ownerIdentity': 'fixture-owner',
            'authenticatedOwnerChannelReference': 'fixture-authenticated-channel', 'provenance': {'sourceSystem': 'fixture-provider',
                'observationReference': 'fixture-public-proof', 'observedAt': '2026-01-01T00:00:00Z'}}
        path = self.run.directory / 'owner-record.json'; r.durable(path, record); live.metadata_credential_record = path
        live.i = SimpleNamespace(read_pinned_credential_secret=lambda name, identity: bytearray(b'offline-owner-token'))
        calls = []
        @contextmanager
        def provider(default_credential=None):
            self.assertIsNotNone(default_credential)
            def request(method, path, body=None):
                calls.append((method, path, body))
                if path.endswith('/lookup-self'):
                    return 200, {'data': {'accessor': 'OwnerPublicAccessor', 'policies': ['ext-secrets-1-inventory-metadata-read'],
                        'ttl': 3600, 'explicit_max_ttl': 604800, 'issue_time': r.now().isoformat(),
                        'expire_time': (r.now() + timedelta(hours=1)).isoformat()}}
                if path.endswith('/capabilities-self'):
                    return 200, {'data': {key: ['read'] if '/metadata/tenants/' in key else ['deny'] for key in body['paths']}}
                return 200, {'data': {'public-native-version': 2}}
            yield request
        live.provider = provider
        return live, record, calls

    def test_owner_metadata_reference_uses_self_proof_and_only_four_metadata_reads(self):
        live, record, calls = self.owner_backend()
        result = live.owner_metadata()
        self.assertTrue(result['ownerMetadataCredentialVerified'])
        paths = [path for method, path, body in calls if '/metadata/' in path]
        self.assertEqual(set(paths), {'/v1/ext-secrets-1-isolated/metadata/' + item['original']['secretReference'] for item in self.scope['keyPlan']})
        self.assertEqual(len(paths), 4)
        self.assertTrue(any(path.endswith('/capabilities-self') for _, path, _ in calls))
        self.assertFalse(any('capabilities-accessor' in path or '/data/' in path for _, path, _ in calls))
        self.assertTrue(all(not any(value) for value in live.private))
        self.assertNotIn(b'offline-owner-token', (self.run.directory / 'owner-metadata-record-pin.json').read_bytes())

    def test_owner_metadata_record_rejects_private_fields_or_other_secret_namespace(self):
        live, record, _ = self.owner_backend()
        record['token'] = 'must-not-be-a-record-field'; r.durable(live.metadata_credential_record, record)
        with self.assertRaises(r.Blocked): r.metadata_record(live.metadata_credential_record, self.scope)
        record.pop('token'); record['credentialReference'] = 'openbao/Secret/operator#token'; r.durable(live.metadata_credential_record, record)
        with self.assertRaises(r.Blocked): r.metadata_record(live.metadata_credential_record, self.scope)

    def test_owner_metadata_secret_identity_failure_precedes_provider_reads(self):
        live, _, calls = self.owner_backend()
        def replaced(*args): raise r.Blocked('fixture-secret-identity-changed')
        live.i.read_pinned_credential_secret = replaced
        with self.assertRaisesRegex(r.Blocked, 'identity-changed'): live.owner_metadata()
        self.assertEqual(calls, [])

    def test_successor_capabilities_self_succeeds_without_operator_accessor_permission(self):
        live, _, calls = self.owner_backend()
        token = bytearray(b'offline-owner-token')
        live.verify_capabilities('metadata', 'OwnerPublicAccessor', credential=token)
        self.assertEqual([path for _, path, _ in calls], ['/v1/auth/token/lookup-self', '/v1/sys/capabilities-self'])

    def test_owner_metadata_success_does_not_hide_missing_operator_configuration(self):
        live, _, _ = self.owner_backend()
        live._observe_operator_provider = lambda: (_ for _ in ()).throw(OSError('operator-unavailable'))
        result = live.observe_provider()
        self.assertTrue(result['ownerMetadataCredentialVerified']); self.assertIsNotNone(result['metadataSha256'])
        self.assertIsNone(result['configurationSha256']); self.assertTrue(result['operatorObservationUnavailable'])

    def test_scope_original_binding_blocks_second_application_directory_after_uncertainty(self):
        r.durable(self.run.directory / 'credential-data.json', {'phase': 'IssuanceUnknown'})
        with self.run.application_original(): pass
        other = r.Recovery(self.scope, self.run.directory.parent / 'other-run', self.backend, self.clock, registry_root=self.registry)
        self.addCleanup(other.close)
        self.assertIsNotNone(other.observe(['authority'])['facts']['authority'])
        with self.assertRaisesRegex(r.Blocked, 'different-application-directory'):
            other.apply(None)
        self.assertEqual(self.backend.effects, [])

    def test_scope_application_lock_serializes_different_directories(self):
        other = r.Recovery(self.scope, self.run.directory.parent / 'parallel-run', self.backend, self.clock, registry_root=self.registry)
        self.addCleanup(other.close)
        with self.run.application_original():
            with self.assertRaisesRegex(r.Blocked, 'already-running'):
                with other.application_original(): self.fail('second lock entered')

    def test_owner_metadata_wrong_native_accessor_or_broad_capability_denies_reads(self):
        for fault in ('accessor', 'capabilities'):
            live, _, calls = self.owner_backend(); original = live.provider
            @contextmanager
            def provider(default_credential=None):
                with original(default_credential) as native:
                    def request(method, path, body=None):
                        status, value = native(method, path, body)
                        if fault == 'accessor' and path.endswith('/lookup-self'): value['data']['accessor'] = 'different-accessor'
                        if fault == 'capabilities' and path.endswith('/capabilities-self'):
                            value['data']['secret/data/*'] = ['read']
                        return status, value
                    yield request
            live.provider = provider
            with self.assertRaises(r.Blocked): live.owner_metadata()
            self.assertFalse(any('/metadata/' in path for _, path, _ in calls))
            self.assertTrue(all(not any(value) for value in live.private))

    def test_original_capabilities_accessor_denial_falls_back_to_selected_self_proof(self):
        live, _, calls = self.owner_backend(); original_accessor = self.scope['credentialBranches']['metadata']['original']['accessor']
        @contextmanager
        def provider(default_credential=None):
            def request(method, path, body=None):
                calls.append((method, path, body))
                if default_credential is None: return 403, {'errors': ['permission denied']}
                if path.endswith('/lookup-self'): return 200, {'data': {'accessor': original_accessor}}
                return 200, {'data': {key: ['read'] if '/metadata/tenants/' in key else ['deny'] for key in body['paths']}}
            yield request
        live.provider = provider
        live.verify_capabilities('metadata', original_accessor)
        self.assertEqual([path for _, path, _ in calls], ['/v1/sys/capabilities-accessor', '/v1/auth/token/lookup-self', '/v1/sys/capabilities-self'])

    def test_owner_metadata_partial_denial_retains_other_successful_paths(self):
        live, _, _ = self.owner_backend(); original = live.provider
        first = self.scope['keyPlan'][0]['original']['secretReference']
        @contextmanager
        def provider(default_credential=None):
            with original(default_credential) as native:
                def request(method, path, body=None):
                    if path.endswith('/metadata/' + first): return 403, {'errors': ['permission denied']}
                    return native(method, path, body)
                yield request
        live.provider = provider
        result = live.owner_metadata()
        self.assertEqual(result['metadataHttpStatuses'][first], 403)
        self.assertEqual(sum(status == 200 for status in result['metadataHttpStatuses'].values()), 3)
        self.assertIsNone(result['metadataSha256'])

    def test_full_selected_successor_validation_uses_its_token_when_operator_capabilities_deny(self):
        live = object.__new__(r.LiveBackend); live.scope = self.scope; live.private = []
        journal = {'phase': 'Stored', 'accessor': 'new-selected-accessor',
            'original': self.scope['credentialBranches']['metadata']['original'], 'selectedSecret': 'new-selected-secret',
            'secretIdentity': {'uid': 'assigned', 'resourceVersion': '5'}}
        reads, requests = [], []
        def read(name, identity):
            reads.append((name, identity)); return bytearray(b'offline-selected-token')
        live.i = SimpleNamespace(read_pinned_credential_secret=read)
        lease = {'accessor': journal['accessor'], 'policies': [journal['original']['policy']], 'ttl': 86400,
                 'explicit_max_ttl': 604800, 'issue_time': '2026-10-29T17:00:00Z', 'expire_time': '2026-10-31T00:00:00Z'}
        @contextmanager
        def provider(default_credential=None):
            def request(method, path, body=None, credential=None):
                requests.append(path)
                if path.endswith('/capabilities-accessor'): return 403, {'errors': ['permission denied']}
                if path.endswith('/lookup-accessor'): return 200, {'data': lease}
                self.assertEqual(bytes(credential or default_credential), b'offline-selected-token')
                if path.endswith('/lookup-self'): return 200, {'data': lease}
                return 200, {'data': {key: ['read'] if '/metadata/tenants/' in key else ['deny'] for key in body['paths']}}
            yield request
        live.provider = provider
        with patch.object(self.run, 'backend', live): self.run.validate_selected('metadata', journal)
        self.assertEqual(reads, [('new-selected-secret', journal['secretIdentity'])])
        self.assertNotIn('/v1/sys/capabilities-accessor', requests)
        self.assertIn('/v1/sys/capabilities-self', requests)
        live.close()


if __name__ == '__main__': unittest.main()
