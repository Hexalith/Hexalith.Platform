"""Offline exact-scope maintenance and interrupted-effect recovery fixtures."""
from copy import deepcopy
from datetime import datetime, timedelta, timezone
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

import maintain_isolated_custody as subject
from native_journal import durable_write

NOW = datetime(2030, 1, 2, 1, 2, 3, 456780, timezone.utc)


class Clock(datetime):
    @classmethod
    def now(cls, tz=None):
        return NOW


class MaintenanceTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        self.baseline, self.binding, self.evidence = root / 'baseline', root / 'binding', root / 'maintenance'
        for directory in (self.baseline, self.binding, self.evidence):
            directory.mkdir()
        self.runtime = root / 'runtime.json'
        register = root / 'register.md'
        register.write_text('untouched-register')
        self.source = root / 'original.json'
        durable_write(self.source, {'original': 'retained'})
        fingerprints = {str(self.source): subject.sha(self.source.read_bytes())}
        durable_write(self.baseline / 'original-public-evidence-fingerprints.json', fingerprints)
        durable_write(self.baseline / 'agents-register-before.json', {'sha256': subject.sha(register.read_bytes())})
        self.before = (NOW - timedelta(hours=2)).isoformat()
        self.old_deadline = (NOW + timedelta(hours=2)).isoformat()
        self.profile = {'profileReference': subject.AUTHORITY, 'publicTrustReference': 'original-trust@uid:trust-uid',
                        'version': 'original-profile-version', 'issuer': 'https://original-issuer', 'audience': subject.APP,
                        'notBefore': self.before, 'validUntil': self.old_deadline, 'revoked': False}
        self.successors, self.metadata = [], {}
        key_statuses = []
        for tenant in ('tenant-a', 'tenant-b'):
            for purpose in ('TrustedEnvelope', 'ContentDigest'):
                relative = 'tenants/' + tenant + '/' + purpose + '/operational'
                opaque = tenant + '-' + purpose
                mapping = {'notBefore': self.before, 'verifyUntil': (NOW + timedelta(days=5)).isoformat(), 'secretVersion': '2'}
                self.successors.append({'original': {'tenantId': tenant, 'purpose': purpose, 'secretReference': relative},
                                        'opaqueVersion': opaque, 'mapping': mapping})
                self.metadata[relative] = {'current_version': 2, 'versions': {'1': {'destroyed': False}, '2': {'destroyed': False}},
                                           'custom_metadata': {'original-and-successor': opaque}}
                key_statuses.append({'tenantId': tenant, 'purpose': purpose, 'version': opaque, 'profileVersion': self.profile['version'],
                                     'notBefore': self.before, 'validUntil': self.old_deadline, 'revoked': False})
        self.state = {'instanceId': 'original-instance', 'generation': 10, 'revision': 'original-instance:10',
                      'profile': deepcopy(self.profile), 'callers': [], 'hosts': [], 'keyStatuses': key_statuses, 'audit': []}
        for name, app, caller in [('inventory', subject.INVENTORY, subject.QUERY_CLIENT), (subject.APP, subject.APP, subject.CUSTODY_CLIENT)]:
            self.state['callers'].append({'callerId': caller, 'notBefore': self.before, 'validUntil': self.old_deadline, 'enabled': True})
            self.state['hosts'].append({'inventoryAuthorityReference': app, 'callerId': caller,
                'workloadReference': subject.NS + '/Deployment/' + name + '@uid:uid-' + name,
                'notBefore': self.before, 'validUntil': self.old_deadline, 'enrolled': True})
        originals = []
        for index in range(10):
            receipt = {'operationId': 'original-' + str(index), 'original': True}
            self.state['audit'].append(receipt)
            source = self.binding / ('original-owner-' + str(index) + '.json')
            durable_write(source, {'phase': 'Committed', 'operation': {'operationId': receipt['operationId']}, 'receipt': receipt})
            originals.append({'operationId': receipt['operationId'], 'source': str(source), 'receiptRetained': True})
        self.original_state = deepcopy(self.state)
        durable_write(self.baseline / 'authority-state.before.json', self.state)
        durable_write(self.baseline / 'original-owner-receipts.json', originals)
        durable_write(self.baseline / 'key-metadata.before.json', self.metadata)
        durable_write(self.binding / 'successor-key-plan.json', self.successors)
        durable_write(self.binding / 'run.json', {'runId': 'a' * 32})
        self.identities, self.leases, credential_originals, resources = {}, {}, {}, []
        for index, role in enumerate(('data', 'metadata')):
            name = 'original-' + role + '-secret'
            policy = subject.DATA_POLICY if role == 'data' else subject.METADATA_POLICY
            reference = subject.NS + '/Secret/' + name + '#token'
            identity = {'uid': 'uid-' + role, 'resourceVersion': str(20 + index)}
            lease = {'accessor': 'original-' + role + '-accessor', 'creation_time': int((NOW - timedelta(days=1)).timestamp()),
                     'issue_time': (NOW - timedelta(days=1)).isoformat(), 'expire_time': (NOW + timedelta(hours=1)).isoformat(),
                     'explicit_max_ttl': 604800, 'policies': [policy], 'renewable': True, 'ttl': 3600}
            self.leases[role] = lease
            self.identities[role] = identity
            issuance = {'phase': 'Stored', 'policy': policy, 'secretName': name, 'credentialReference': reference,
                        'accessor': lease['accessor']}
            durable_write(self.binding / (role + '-credential-issuance.json'), issuance)
            resources.append({'reference': 'Secret/' + subject.NS + '/' + name, 'phase': 'Created', **identity})
            credential_originals[role] = {'credentialReference': reference, 'lease': deepcopy(lease), 'originalRecordedSecretIdentity': identity}
        durable_write(self.binding / 'resource-journal.json', resources)
        durable_write(self.baseline / 'credentials.before.json', credential_originals)
        self.public = {'schema': 'EXT-SECRETS-1.qualification.v1', 'qualification': {'null' + str(index): None for index in range(42)},
            'runtime': {'profileReference': subject.AUTHORITY, 'publicTrustReference': self.profile['publicTrustReference'],
                        'profileVersion': self.profile['version'], 'profileNotBefore': self.before, 'profileValidUntil': self.old_deadline,
                        'issuer': self.profile['issuer'], 'audience': self.profile['audience'],
                        'currentRevocationReference': subject.AUTHORITY, 'currentInventoryAuthorityReference': subject.INVENTORY,
                        'hmacCredentialReference': credential_originals['data']['credentialReference'],
                        'timing': {'L': 300, 'S': 30, 'O': 600, 'H': 86400, 'R': 604800}}}
        durable_write(self.runtime, self.public)
        self.objects = {}
        custody_runtime = subject.APP + '-runtime-' + 'a' * 12
        for kind, name in [('ConfigMap', 'ext-secrets-1-inventory-runtime'), ('ConfigMap', custody_runtime), ('Deployment', 'inventory'), ('Deployment', subject.APP)]:
            obj = {'kind': kind, 'apiVersion': 'v1', 'metadata': {'uid': 'uid-' + name, 'resourceVersion': '30', 'name': name, 'namespace': subject.NS}}
            if kind == 'ConfigMap':
                import json
                obj['data'] = {'runtime-reference.json': json.dumps(self.public)}
            else:
                obj['spec'] = {'template': {'metadata': {'annotations': {}}, 'spec': {'volumes': [
                    {'name': 'runtime', 'configMap': {'name': 'ext-secrets-1-inventory-runtime' if name == 'inventory' else custody_runtime}}]}}}
            self.objects[kind + '/' + subject.NS + '/' + name] = obj
        durable_write(self.baseline / 'preserved-kubernetes-before.json', self.snapshot())
        self.instance = subject.Maintenance.__new__(subject.Maintenance)
        self.instance.evidence, self.instance.baseline = self.evidence, self.baseline
        self.instance.tokens, self.instance.private_values = {}, []
        self.instance.openbao_snapshot = Mock(return_value={'existing-policy-hashes': 'preserved'})
        self.renewals, self.posts, self.replaces, self.lose_renewal, self.lose_post = [], [], [], None, False
        for target, value in [('BINDING', self.binding), ('RUNTIME', self.runtime), ('REGISTER', register), ('datetime', Clock),
                              ('application_request', self.application_request), ('administration_request', self.administration_request),
                              ('kjson', self.kjson), ('kube', self.kube), ('snapshot', self.snapshot),
                              ('read_pinned_credential_secret', self.pinned), ('http_health', lambda *args: {p: {'status': 200} for p in ('/alive', '/health', '/ready')})]:
            replacement = patch.object(subject, target, value)
            replacement.start()
            self.addCleanup(replacement.stop)

    def pinned(self, name, identity):
        role = 'metadata' if 'metadata' in name else 'data'
        self.assertEqual(identity, self.identities[role])
        return bytearray(('synthetic-private-token-' + role).encode())

    def snapshot(self):
        return {key: {'uid': value['metadata']['uid'], 'specSha256': subject.sha(subject.canonical(subject.public_object(value)))}
                for key, value in self.objects.items()}

    def kjson(self, verb, kind, name, *args):
        self.assertEqual(verb, 'get')
        return deepcopy(self.objects[kind + '/' + subject.NS + '/' + name])

    def kube(self, *args, **kwargs):
        if args[0] == 'rollout':
            return SimpleNamespace(stdout=b'')
        self.assertEqual(args[0], 'replace')
        import json
        obj = json.loads(kwargs['data'])
        key = obj['kind'] + '/' + subject.NS + '/' + obj['metadata']['name']
        old = self.objects[key]
        self.assertEqual(obj['metadata']['uid'], old['metadata']['uid'])
        self.assertEqual(obj['metadata']['resourceVersion'], old['metadata']['resourceVersion'])
        journal = subject.read(self.evidence / ('runtime-update-' + obj['kind'].lower() + '-' + obj['metadata']['name'] + '.json'))
        self.assertEqual(journal['phase'], 'Started')
        obj['metadata']['resourceVersion'] = str(int(old['metadata']['resourceVersion']) + 1)
        self.objects[key] = obj
        self.replaces.append(key)
        return SimpleNamespace(stdout=b'applied')

    def application_request(self, token, *args):
        role = 'metadata' if token.endswith(b'metadata') else 'data'
        import json
        if args[:2] == ('token', 'lookup'):
            value = {'data': deepcopy(self.leases[role])}
        elif args[:2] == ('token', 'capabilities'):
            value = ['deny'] if '*' in args[-1] else ['read']
        elif args[:2] == ('token', 'renew'):
            journal = subject.read(self.evidence / ('renewal-' + role + '.json'))
            self.assertEqual(journal['phase'], 'Started')
            increment = int(args[-1].split('=')[1][:-1])
            self.assertLessEqual(increment, 86400)
            self.renewals.append(role)
            self.leases[role]['expire_time'] = (NOW + timedelta(seconds=increment)).isoformat()
            self.leases[role]['ttl'] = increment
            if self.lose_renewal == role:
                raise RuntimeError('lost-renewal-response')
            value = {'auth': {'client_token': bytes(token).decode()}}
        elif args[0] == 'read' and '/metadata/' in args[-1]:
            value = {'data': self.metadata[args[-1].split('/metadata/')[1]]}
        else:
            raise AssertionError('Unexpected provider operation: ' + str(args))
        return SimpleNamespace(stdout=json.dumps(value).encode())

    def administration_request(self, instance, method, path, operation=None):
        if method == 'GET':
            return 200, deepcopy(self.state)
        journal = subject.read(self.evidence / ('owner-operation-' + str(len(self.posts)) + '.json')) if not self.posts else None
        self.posts.append(deepcopy(operation))
        existing = next((item for item in self.state['audit'] if item.get('operationId') == operation['operationId']), None)
        if existing:
            return 200, existing
        self.assertEqual(operation['expectedRevision'], self.state['revision'])
        plan = subject.read(self.evidence / 'maintenance-plan.json')
        index = len(self.state['audit']) - 10
        self.assertEqual(subject.read(self.evidence / ('owner-operation-' + str(index) + '.json'))['operation'], operation)
        self.assertEqual(operation, plan['operations'][index])
        self.state = subject.expected_records(plan, index + 1)
        audit = deepcopy(self.original_state['audit'])
        # Reconstruct prior receipts from committed journals.
        for previous in range(index):
            audit.append(subject.read(self.evidence / ('owner-operation-' + str(previous) + '.json'))['receipt'])
        receipt = {'operationId': operation['operationId'], 'administratorId': subject.ADMIN_CLIENT, 'kind': operation['kind'],
                   'payloadSha256': 'A' * 64, 'outcome': 'Applied', 'previousRevision': operation['expectedRevision'],
                   'revision': 'original-instance:' + str(11 + index), 'recordedAt': NOW.isoformat()}
        self.state.update(audit=audit + [receipt], generation=11 + index, revision=receipt['revision'])
        if self.lose_post:
            self.lose_post = False
            raise RuntimeError('lost-owner-response')
        return 200, receipt

    def test_complete_maintenance_retains_receipts_versions_keys_nulls_and_exact_credentials(self):
        plan = subject.inspect(self.instance)
        original_plan = (self.evidence / 'maintenance-plan.json').read_bytes()
        subject.maintain(self.instance)
        subject.maintain(self.instance)
        self.assertEqual(self.renewals, ['data', 'metadata'])
        self.assertEqual(len(self.posts), 9)
        self.assertEqual(len(self.replaces), 4)
        self.assertEqual(self.state['audit'][:10], self.original_state['audit'])
        self.assertEqual(self.state['profile']['version'], self.profile['version'])
        self.assertEqual(self.state['profile']['notBefore'], self.profile['notBefore'])
        self.assertLessEqual(subject.instant(plan['deadline']), NOW + timedelta(days=1))
        self.assertEqual((self.evidence / 'maintenance-plan.json').read_bytes(), original_plan)
        self.assertEqual(subject.read(self.runtime), plan['publicRuntime'])
        self.assertEqual(subject.null_paths(subject.read(self.runtime)), subject.null_paths(self.public))
        self.assertFalse(subject.read(self.evidence / 'completion.json')['dependencyAvailable'])
        self.assertTrue(subject.read(self.evidence / 'credential-exposure-check.json')['passed'])
        for path in self.evidence.iterdir():
            self.assertNotIn(b'synthetic-private-token', path.read_bytes())

    def test_owner_lost_response_reconciles_exact_operation_and_retains_all_original_outcomes(self):
        plan = subject.inspect(self.instance)
        self.lose_post = True
        with self.assertRaisesRegex(RuntimeError, 'lost-owner-response'):
            subject.maintain_owner_records(self.instance, plan)
        original = subject.read(self.evidence / 'owner-operation-0.json')['operation']
        subject.maintain_owner_records(self.instance, plan)
        self.assertEqual(self.posts[0], original)
        self.assertEqual(self.posts[1], original)
        self.assertEqual(len(self.state['audit']), 19)
        self.assertEqual(self.state['audit'][:10], self.original_state['audit'])

    def test_unknown_renewal_is_observation_only_even_when_current_expiry_covers_plan(self):
        plan = subject.inspect(self.instance)
        self.lose_renewal = 'data'
        with self.assertRaisesRegex(RuntimeError, 'lost-renewal-response'):
            subject.maintain_lease(self.instance, plan, 'data')
        with self.assertRaisesRegex(RuntimeError, 'observation-only-owner-reconciliation-required'):
            subject.maintain_lease(self.instance, plan, 'data')
        self.assertEqual(self.renewals, ['data'])
        self.assertIn('currentObservation', subject.read(self.evidence / 'renewal-data.json'))

    def test_expired_nonrenewable_and_mismatched_credential_denied_without_renewal(self):
        for changes in ({'ttl': 0}, {'renewable': False}, {'accessor': 'foreign'}, {'policies': ['root']}, {'creation_time': 0}, {'explicit_max_ttl': 604801}):
            original = deepcopy(self.leases['data'])
            self.leases['data'].update(changes)
            with self.subTest(changes=changes), self.assertRaises(RuntimeError):
                subject.inspect(self.instance)
            self.leases['data'] = original
        self.assertEqual(self.renewals, [])

    def test_missing_original_secret_uid_or_resource_version_is_never_synthesized(self):
        resources = subject.read(self.binding / 'resource-journal.json')
        del resources[0]['resourceVersion']
        durable_write(self.binding / 'resource-journal.json', resources)
        with patch.object(subject, 'read_pinned_credential_secret') as native:
            with self.assertRaisesRegex(RuntimeError, 'creation-identity-required'):
                subject.inspect(self.instance)
            native.assert_not_called()

    def test_seven_day_absolute_cap_requires_separate_successor_secret_path(self):
        originals = subject.read(self.baseline / 'credentials.before.json')
        created = NOW - timedelta(days=6, hours=12)
        for role in ('data', 'metadata'):
            self.leases[role].update(creation_time=int(created.timestamp()), issue_time=created.isoformat())
            originals[role]['lease'] = deepcopy(self.leases[role])
        durable_write(self.baseline / 'credentials.before.json', originals)
        with self.assertRaisesRegex(RuntimeError, 'seven-day-cap-requires-separate-successor-secret-cutover-and-revocation'):
            subject.inspect(self.instance)
        self.assertFalse((self.evidence / 'maintenance-plan.json').exists())
        self.assertEqual(self.renewals, [])

    def test_revoked_foreign_or_extra_records_and_changed_receipts_deny(self):
        alterations = [lambda state: state['profile'].update(revoked=True),
            lambda state: state['callers'].append(dict(state['callers'][0], callerId='foreign')),
            lambda state: state['hosts'][0].update(workloadReference='foreign'),
            lambda state: state['keyStatuses'][0].update(version='foreign'),
            lambda state: state['audit'][0].update(original=False)]
        for alter in alterations:
            self.state = deepcopy(self.original_state)
            alter(self.state)
            with self.subTest(alter=alter), self.assertRaisesRegex(RuntimeError, 'original-reconciliation'):
                subject.inspect(self.instance)
        self.assertEqual(self.posts, [])
        self.assertEqual(self.renewals, [])

    def test_technical_deadline_caps_all_owner_records_without_extending_key_metadata(self):
        self.successors[0]['mapping']['verifyUntil'] = (NOW + timedelta(hours=4)).isoformat()
        durable_write(self.binding / 'successor-key-plan.json', self.successors)
        plan = subject.inspect(self.instance)
        self.assertEqual(subject.instant(plan['deadline']), NOW + timedelta(hours=4))
        for operation in plan['operations']:
            record = next(value for key, value in operation.items() if key in ('profile', 'caller', 'host', 'keyStatus') and value is not None)
            self.assertEqual(record['validUntil'], plan['deadline'])
        self.assertEqual(plan['keyMetadata'], self.metadata)

    def test_dependent_runtime_copy_drift_denies_before_any_mutation(self):
        self.objects['ConfigMap/' + subject.NS + '/ext-secrets-1-inventory-runtime']['data']['runtime-reference.json'] = '{}'
        with self.assertRaisesRegex(RuntimeError, 'runtime-copy-not-coherent'):
            subject.inspect(self.instance)
        self.assertEqual(self.replaces, [])
        self.assertEqual(self.posts, [])

    def test_lost_public_update_response_reconciles_exact_object_without_second_replace(self):
        plan = subject.inspect(self.instance)
        item = plan['objects'][0]
        def lost(*args, **kwargs):
            self.kube(*args, **kwargs)
            raise RuntimeError('lost-native-response')
        with patch.object(subject, 'kube', lost), self.assertRaisesRegex(RuntimeError, 'lost-native-response'):
            subject.update_public_object(self.instance, item)
        subject.update_public_object(self.instance, item)
        self.assertEqual(len(self.replaces), 1)
        self.assertEqual(subject.read(self.evidence / 'runtime-update-configmap-ext-secrets-1-inventory-runtime.json')['phase'], 'ReconciledCurrent')

    def test_unknown_public_update_with_no_effect_is_never_repeated(self):
        plan = subject.inspect(self.instance)
        with patch.object(subject, 'kube', side_effect=RuntimeError('lost-before-effect')):
            with self.assertRaisesRegex(RuntimeError, 'lost-before-effect'):
                subject.update_public_object(self.instance, plan['objects'][0])
        with self.assertRaisesRegex(RuntimeError, 'outcome-unknown-no-repeat'):
            subject.update_public_object(self.instance, plan['objects'][0])
        self.assertEqual(self.replaces, [])

    def test_replacement_runtime_uid_and_stale_resource_version_deny(self):
        plan = subject.inspect(self.instance)
        item = plan['objects'][0]
        current = self.objects[item['reference']]
        current['metadata']['resourceVersion'] = '999'
        with self.assertRaisesRegex(RuntimeError, 'resource-version-or-public-intent-changed'):
            subject.update_public_object(self.instance, item)
        current['metadata']['uid'] = 'foreign'
        with self.assertRaisesRegex(RuntimeError, 'uid-changed-no-update'):
            subject.update_public_object(self.instance, item)
        self.assertEqual(self.replaces, [])

    def test_recursive_original_journal_fingerprints_are_checked_when_present(self):
        nested = self.binding / 'nested-original.json'
        durable_write(nested, {'original': True})
        durable_write(self.baseline / 'original-public-evidence-recursive-fingerprints.json', {
            str(self.source): subject.sha(self.source.read_bytes()), str(nested): subject.sha(nested.read_bytes())})
        durable_write(nested, {'original': False})
        with self.assertRaisesRegex(RuntimeError, 'original-public-evidence-changed'):
            subject.inspect(self.instance)

    def test_one_failed_health_check_prevents_completion(self):
        subject.inspect(self.instance)
        with patch.object(subject, 'http_health', return_value={p: {'status': 503 if p == '/alive' else 200} for p in ('/alive', '/health', '/ready')}):
            with self.assertRaisesRegex(RuntimeError, 'all-nine-isolated-health-checks-required'):
                subject.maintain(self.instance)
        self.assertFalse((self.evidence / 'completion.json').exists())


if __name__ == '__main__':
    unittest.main()
