"""Offline exact-scope maintenance and interrupted-effect recovery fixtures."""
from copy import deepcopy
from datetime import datetime, timedelta, timezone
from pathlib import Path
import base64
import hashlib
import json
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

import maintain_isolated_custody as subject
from native_journal import durable_write

NOW = datetime(2030, 1, 2, 1, 2, 3, 456780, timezone.utc)


class Clock(datetime):
    current = NOW

    @classmethod
    def now(cls, tz=None):
        return cls.current


class MaintenanceTests(unittest.TestCase):
    def setUp(self):
        Clock.current = NOW
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
                                        'opaqueVersion': opaque, 'mapping': mapping, 'phase': 'MetadataVerified', 'secretVersion': '2'})
                self.metadata[relative] = {'current_version': 2, 'versions': {'1': {'destroyed': False}, '2': {'destroyed': False}},
                                           'custom_metadata': {'ext-secrets-1.versionMappings': json.dumps({opaque: mapping})}}
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
        durable_write(self.binding / 'owner-administered-state.json', self.state)
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
        durable_write(self.binding / 'runtime-reference.json', self.public)
        self.objects = {}
        custody_runtime = subject.APP + '-runtime-' + 'a' * 12
        for kind, name in [('ConfigMap', 'ext-secrets-1-inventory-runtime'), ('ConfigMap', custody_runtime), ('Deployment', 'inventory'), ('Deployment', subject.APP)]:
            obj = {'kind': kind, 'apiVersion': 'v1', 'metadata': {'uid': 'uid-' + name, 'resourceVersion': '30', 'name': name, 'namespace': subject.NS}}
            if kind == 'ConfigMap':
                obj['data'] = {'runtime-reference.json': json.dumps(self.public)}
            else:
                obj['spec'] = {'template': {'metadata': {'annotations': {}}, 'spec': {'volumes': [
                    {'name': 'runtime', 'configMap': {'name': 'ext-secrets-1-inventory-runtime' if name == 'inventory' else custody_runtime}}]}}}
            self.objects[kind + '/' + subject.NS + '/' + name] = obj
        durable_write(self.baseline / 'preserved-kubernetes-before.json', self.snapshot())
        durable_write(self.baseline / 'preserved-openbao-before.json', {'existing-policy-hashes': 'preserved'})
        self.instance = subject.Maintenance.__new__(subject.Maintenance)
        self.instance.evidence, self.instance.baseline = self.evidence, self.baseline
        self.instance.tokens, self.instance.private_values = {}, []
        self.instance.openbao_snapshot = Mock(return_value={'existing-policy-hashes': 'preserved'})
        self.renewals, self.posts, self.replaces, self.lose_renewal, self.lose_post = [], [], [], None, False
        self.capability_overrides, self.post_renewal_changes = {}, {}
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
        result = {}
        for key, obj in self.objects.items():
            material = {name: value for name, value in obj.items() if name not in ('metadata', 'status', 'kind', 'apiVersion')}
            material['metadata'] = {name: obj['metadata'][name] for name in ('labels', 'annotations') if name in obj['metadata']}
            result[key] = {'uid': obj['metadata']['uid'], 'specSha256': subject.sha(subject.canonical(material))}
        return result

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
            value = self.capability_overrides.get((role, args[-1]), ['deny'] if '*' in args[-1] else ['read'])
        elif args[:2] == ('token', 'renew'):
            journal = subject.read(self.evidence / ('renewal-' + role + '.json'))
            self.assertEqual(journal['phase'], 'Started')
            increment = int(args[-1].split('=')[1][:-1])
            self.assertLessEqual(increment, 86400)
            self.renewals.append(role)
            cap = subject.instant(self.leases[role]['issue_time']) + timedelta(seconds=self.leases[role]['explicit_max_ttl'])
            self.leases[role]['expire_time'] = min(Clock.current + timedelta(seconds=increment), cap).isoformat()
            self.leases[role]['ttl'] = increment
            self.leases[role].update(self.post_renewal_changes.get(role, {}))
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
        self.posts.append(deepcopy(operation))
        existing = next((item for item in self.state['audit'] if item.get('operationId') == operation['operationId']), None)
        if existing:
            return 200, existing
        self.assertEqual(operation['expectedRevision'], self.state['revision'])
        plan = subject.read(self.evidence / 'maintenance-plan.json')
        index = len(self.state['audit']) - len(plan['beforeState']['audit'])
        self.assertEqual(subject.read(self.evidence / ('owner-operation-' + str(index) + '.json'))['operation'], operation)
        self.assertEqual(operation, plan['operations'][index])
        # Model AuthorityRepository.Apply independently of the maintenance helper.
        audit = deepcopy(self.state['audit'])
        if operation['kind'] == 'PublishProfile':
            self.state['profile'] = deepcopy(operation['profile'])
        elif operation['kind'] == 'SetCaller':
            caller = operation['caller']
            self.state['callers'] = [item for item in self.state['callers'] if item['callerId'] != caller['callerId']] + [deepcopy(caller)]
        elif operation['kind'] == 'SetInventoryHost':
            host = operation['host']
            self.state['hosts'] = [item for item in self.state['hosts'] if item['inventoryAuthorityReference'] != host['inventoryAuthorityReference']] + [deepcopy(host)]
        elif operation['kind'] == 'SetKeyStatus':
            status = operation['keyStatus']
            self.state['keyStatuses'] = [item for item in self.state['keyStatuses']
                if item['tenantId'] != status['tenantId'] or item['purpose'] != status['purpose'] or item['version'] != status['version']] + [deepcopy(status)]
        else:
            raise AssertionError('Unexpected owner operation: ' + operation['kind'])
        generation = self.state['generation'] + 1
        receipt = {'operationId': operation['operationId'], 'administratorId': subject.ADMIN_CLIENT, 'kind': operation['kind'],
                   'payloadSha256': 'A' * 64, 'outcome': 'Applied', 'previousRevision': operation['expectedRevision'],
                   'revision': 'original-instance:' + str(generation), 'recordedAt': Clock.current.isoformat()}
        self.state.update(audit=audit + [receipt], generation=generation, revision=receipt['revision'])
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
        for path in self.evidence.rglob('*'):
            if path.is_file():
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

    def test_prefix_recovery_matches_repository_reordering_and_exact_committed_receipts(self):
        plan = subject.inspect(self.instance)
        plan_bytes = (self.evidence / 'maintenance-plan.json').read_bytes()
        # The service has committed profile + first host; the second response was lost.
        for index, operation in enumerate(plan['operations'][:2]):
            path = self.evidence / ('owner-operation-' + str(index) + '.json')
            journal = {'phase': 'Started', 'operation': operation}
            durable_write(path, journal)
            _, receipt = self.administration_request(self.instance, 'POST', '/admin/operations', operation)
            if index == 0:
                journal.update(phase='Committed', receipt=receipt)
            else:
                journal['phase'] = 'OutcomeUnknown'
            durable_write(path, journal)
        committed_prefix = deepcopy(self.state['audit'])
        first_journal_bytes = (self.evidence / 'owner-operation-0.json').read_bytes()
        self.assertEqual(self.state['revision'], 'original-instance:12')
        self.assertEqual([host['inventoryAuthorityReference'] for host in self.state['hosts']], [subject.APP, subject.INVENTORY])
        self.assertEqual(subject.validate_progress(self.instance, plan, self.state), 2)
        subject.maintain_owner_records(self.instance, plan)
        self.assertEqual(self.posts[2], plan['operations'][1])
        self.assertEqual(self.state['audit'][:12], committed_prefix)
        self.assertEqual(subject.read(self.evidence / 'owner-operation-1.json')['receipt'], committed_prefix[-1])
        self.assertEqual((self.evidence / 'owner-operation-0.json').read_bytes(), first_journal_bytes)
        self.assertEqual((self.evidence / 'maintenance-plan.json').read_bytes(), plan_bytes)
        post_count = len(self.posts)
        subject.maintain_owner_records(self.instance, plan)
        self.assertEqual(len(self.posts), post_count)

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
        item = self.successors[0]
        self.metadata[item['original']['secretReference']]['custom_metadata']['ext-secrets-1.versionMappings'] = json.dumps({item['opaqueVersion']: item['mapping']})
        durable_write(self.baseline / 'key-metadata.before.json', self.metadata)
        plan = subject.inspect(self.instance)
        self.assertEqual(subject.instant(plan['deadline']), NOW + timedelta(hours=4))
        for operation in plan['operations']:
            record = next(value for key, value in operation.items() if key in ('profile', 'caller', 'host', 'keyStatus') and value is not None)
            self.assertEqual(record['validUntil'], plan['deadline'])
        self.assertEqual(plan['keyMetadata'], self.metadata)

    def test_dependent_runtime_copy_drift_denies_before_any_mutation(self):
        self.objects['ConfigMap/' + subject.NS + '/ext-secrets-1-inventory-runtime']['data']['runtime-reference.json'] = '{}'
        with self.assertRaisesRegex(RuntimeError, 'kubernetes-intent-drift|runtime-copy-not-coherent'):
            subject.inspect(self.instance)
        self.assertEqual(self.replaces, [])
        self.assertEqual(self.posts, [])

    def test_runtime_drift_after_inspect_blocks_before_renewal_or_owner_effects(self):
        subject.inspect(self.instance)
        self.objects['ConfigMap/' + subject.NS + '/ext-secrets-1-inventory-runtime']['data']['runtime-reference.json'] = '{}'
        with self.assertRaisesRegex(RuntimeError, 'resource-version-or-public-intent-changed'):
            subject.maintain(self.instance)
        self.assertEqual(self.renewals, [])
        self.assertEqual(self.posts, [])

    def test_foreign_native_runtime_target_is_never_read_or_updated(self):
        with patch.object(subject, 'kjson') as native:
            with self.assertRaisesRegex(RuntimeError, 'outside-exact-existing-isolated-scope'):
                subject.update_public_object(self.instance, {'reference': 'Deployment/production/preserved'})
            native.assert_not_called()

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

    def test_mutated_cached_plan_cannot_expand_deadline_only_authority(self):
        subject.inspect(self.instance)
        path = self.evidence / 'maintenance-plan.json'
        original = subject.read(path)
        changes = [lambda plan: plan['operations'][0]['profile'].update(issuer='https://foreign'),
            lambda plan: plan['operations'][0].update(operationId='foreign-id'),
            lambda plan: plan['operations'][1].update(expectedRevision='original-instance:999'),
            lambda plan: plan['operations'][0]['profile'].update(version='foreign-version'),
            lambda plan: plan['publicRuntime']['runtime']['timing'].update(L=999),
            lambda plan: plan['objects'][0].update(reference='ConfigMap/production/foreign'),
            lambda plan: plan['objects'][2]['intended']['spec']['template']['spec'].update(hostNetwork=True),
            lambda plan: plan.update(deadline=(NOW + timedelta(hours=25)).isoformat()),
            lambda plan: plan.update(preparedAt=(NOW + timedelta(seconds=1)).isoformat()),
            lambda plan: plan.update(runId='b' * 32),
            lambda plan: plan.update(dependencyAvailable=True),
            lambda plan: plan['credentials']['data']['identity'].update(accessor='foreign')]
        for change in changes:
            altered = deepcopy(original)
            change(altered)
            durable_write(path, altered)
            retained = path.read_bytes()
            with self.subTest(change=change), self.assertRaises((RuntimeError, ValueError)):
                subject.maintain(self.instance)
            self.assertEqual(path.read_bytes(), retained)
            self.assertEqual((self.renewals, self.posts, self.replaces), ([], [], []))
        durable_write(path, original)

    def test_changed_successor_window_invalidates_cached_plan_before_effects(self):
        subject.inspect(self.instance)
        self.successors[0]['mapping']['verifyUntil'] = (NOW + timedelta(hours=4)).isoformat()
        durable_write(self.binding / 'successor-key-plan.json', self.successors)
        with self.assertRaisesRegex(RuntimeError, 'technical-window-or-mapping-changed|plan-intent-mismatch'):
            subject.maintain(self.instance)
        self.assertEqual((self.renewals, self.posts, self.replaces), ([], [], []))

    def test_fresh_inspect_rejects_runtime_constant_policy_and_controller_drift(self):
        original_objects = deepcopy(self.objects)
        public = subject.read(self.runtime)
        altered = deepcopy(public)
        altered['runtime']['timing']['L'] = 301
        durable_write(self.runtime, altered)
        with self.assertRaisesRegex(RuntimeError, 'runtime-drift'):
            subject.inspect(self.instance)
        durable_write(self.runtime, public)
        self.instance.openbao_snapshot.return_value = {'existing-policy-hashes': 'foreign-policy'}
        with self.assertRaisesRegex(RuntimeError, 'openbao-policy-mount-or-auth-drift'):
            subject.inspect(self.instance)
        self.instance.openbao_snapshot.return_value = {'existing-policy-hashes': 'preserved'}
        self.objects['Deployment/' + subject.NS + '/inventory']['spec']['template']['spec']['hostNetwork'] = True
        with self.assertRaisesRegex(RuntimeError, 'kubernetes-intent-drift'):
            subject.inspect(self.instance)
        self.objects = original_objects
        self.assertFalse((self.evidence / 'maintenance-plan.json').exists())
        self.assertEqual((self.renewals, self.posts, self.replaces), ([], [], []))

    def test_cached_inspect_retains_fresh_observations_and_denies_expired_or_revoked_records(self):
        subject.inspect(self.instance)
        path = self.evidence / 'maintenance-plan.json'
        original = path.read_bytes()
        subject.inspect(self.instance)
        self.assertTrue(list(self.evidence.glob('read-only-observation-*.json')))
        self.assertEqual(path.read_bytes(), original)
        self.state['profile']['revoked'] = True
        with self.assertRaisesRegex(RuntimeError, 'revocation-or-revision-drift'):
            subject.inspect(self.instance)
        self.state = deepcopy(self.original_state)
        Clock.current = NOW + timedelta(hours=3)
        with self.assertRaisesRegex(RuntimeError, 'expired-or-not-current'):
            subject.inspect(self.instance)
        self.assertEqual((self.renewals, self.posts, self.replaces), ([], [], []))
        self.assertEqual(path.read_bytes(), original)

    def test_rollout_expiry_stops_before_next_effect_and_cannot_complete(self):
        subject.inspect(self.instance)
        def kube(*args, **kwargs):
            result = self.kube(*args, **kwargs)
            if args[0] == 'rollout':
                Clock.current = NOW + timedelta(hours=25)
            return result
        with patch.object(subject, 'kube', kube), self.assertRaisesRegex(RuntimeError, 'window-expired-before-next-effect'):
            subject.maintain(self.instance)
        self.assertEqual(len(self.replaces), 3)
        self.assertFalse((self.evidence / 'completion.json').exists())

    def test_health_work_cannot_finish_after_owner_deadline(self):
        def health(*args):
            Clock.current = NOW + timedelta(hours=25)
            return {path: {'status': 200} for path in ('/alive', '/health', '/ready')}
        with patch.object(subject, 'http_health', health), self.assertRaisesRegex(RuntimeError, 'deadline-changed-before-completion'):
            subject.maintain(self.instance)
        self.assertFalse((self.evidence / 'completion.json').exists())
        self.assertFalse((self.evidence / 'verified-next-baseline').exists())

    def test_two_cycles_reuse_verified_baseline_and_retain_all_twenty_eight_receipts(self):
        subject.maintain(self.instance)
        first_evidence = self.evidence
        first_baseline = first_evidence / 'verified-next-baseline'
        first_receipts = deepcopy(self.state['audit'])
        first_files = {path: path.read_bytes() for path in first_evidence.rglob('*') if path.is_file()}
        Clock.current = NOW + timedelta(hours=6)
        self.evidence = first_evidence.parent / 'second-maintenance'
        self.evidence.mkdir()
        self.instance.evidence, self.instance.baseline = self.evidence, first_baseline
        subject.maintain(self.instance)
        self.assertEqual(len(self.state['audit']), 28)
        self.assertEqual(self.state['audit'][:19], first_receipts)
        self.assertEqual(self.state['audit'][:10], self.original_state['audit'])
        provenance = subject.read(self.evidence / 'verified-next-baseline/original-owner-receipts.json')
        self.assertEqual(len(provenance), 28)
        self.assertEqual([item['receipt'] for item in provenance], self.state['audit'])
        self.assertTrue(all(item['sourceSha256'] == subject.sha(Path(item['source']).read_bytes()) for item in provenance))
        self.assertTrue(all(path.read_bytes() == content for path, content in first_files.items()))

    def test_coordinator_can_emit_next_baseline_from_verified_public_facts_without_installed_calls(self):
        subject.maintain(self.instance)
        plan = subject.read(self.evidence / 'maintenance-plan.json')
        output = self.evidence.parent / 'coordinator-verified-baseline'
        facts = {'state': deepcopy(self.state), 'credentials': {role: subject.read(self.evidence / ('credential-' + role + '.after.json')) for role in ('data', 'metadata')},
            'key_metadata': deepcopy(self.metadata), 'public_runtime': subject.read(self.runtime), 'kubernetes': self.snapshot(),
            'openbao': {'existing-policy-hashes': 'preserved'}, 'objects': [deepcopy(self.objects[item['reference']]) for item in plan['objects']],
            'health': subject.read(self.evidence / 'health.after.json'), 'output': output}
        originals = {path: path.read_bytes() for path in self.evidence.rglob('*') if path.is_file()}
        with patch.object(subject, 'administration_request') as admin, patch.object(subject, 'application_request') as provider, patch.object(subject, 'kube') as kube, patch.object(subject, 'kjson') as kjson:
            subject.emit_verified_baseline(self.instance, plan, **facts)
            for native in (admin, provider, kube, kjson):
                native.assert_not_called()
        self.assertEqual(subject.validate_baseline(output), self.state)
        self.assertTrue(all(path.read_bytes() == content for path, content in originals.items()))

    def fractional_cap(self, sufficient):
        cap = NOW + timedelta(hours=4, microseconds=250000)
        self.successors[0]['mapping']['verifyUntil'] = cap.isoformat()
        durable_write(self.binding / 'successor-key-plan.json', self.successors)
        item = self.successors[0]
        self.metadata[item['original']['secretReference']]['custom_metadata']['ext-secrets-1.versionMappings'] = json.dumps({item['opaqueVersion']: item['mapping']})
        durable_write(self.baseline / 'key-metadata.before.json', self.metadata)
        lease = self.leases['data']
        issued = cap - timedelta(seconds=604800)
        lease.update(creation_time=int(issued.timestamp()), issue_time=issued.isoformat())
        if sufficient:
            lease['expire_time'] = cap.isoformat()
        originals = subject.read(self.baseline / 'credentials.before.json')
        originals['data']['lease'] = deepcopy(lease)
        durable_write(self.baseline / 'credentials.before.json', originals)
        return cap

    def test_sufficient_lease_at_fractional_cap_needs_no_renewal_arithmetic(self):
        cap = self.fractional_cap(sufficient=True)
        plan = subject.inspect(self.instance)
        subject.maintain_lease(self.instance, plan, 'data')
        journal = subject.read(self.evidence / 'renewal-data.json')
        self.assertEqual(journal['phase'], 'AlreadySufficient')
        self.assertEqual(journal['incrementSeconds'], 0)
        self.assertEqual(subject.instant(plan['deadline']), cap)
        self.assertEqual(self.renewals, [])

    def test_native_renewal_can_clamp_bounded_increment_to_fractional_original_cap(self):
        cap = self.fractional_cap(sufficient=False)
        plan = subject.inspect(self.instance)
        subject.maintain_lease(self.instance, plan, 'data')
        journal = subject.read(self.evidence / 'renewal-data.json')
        self.assertEqual(journal['phase'], 'Renewed')
        self.assertLessEqual(journal['incrementSeconds'], 86400)
        self.assertEqual(subject.instant(journal['after']['expire_time']), cap)
        self.assertEqual(self.renewals, ['data'])

    def test_hash_only_credential_exposure_is_rejected_without_exposing_hash_in_report(self):
        value = b'synthetic-private-token-data'
        self.instance.private_values = [value]
        digest = hashlib.sha256(value).digest()
        for encoded in (digest, digest.hex().encode(), digest.hex().upper().encode(), base64.b64encode(digest), base64.urlsafe_b64encode(digest).rstrip(b'=')):
            leak = self.evidence / 'synthetic-hash-only.bin'
            leak.write_bytes(encoded)
            with self.subTest(encoded_format=len(encoded)), self.assertRaisesRegex(RuntimeError, '^credential-exposure-check-failed$'):
                subject.exposure_check(self.instance)
            report_path = self.evidence / 'credential-exposure-check.json'
            report = subject.read(report_path)
            self.assertFalse(report['passed'])
            self.assertEqual(report['literalAndBase64Matches'], 0)
            self.assertEqual(report['prohibitedCredentialHashMatches'], 1)
            self.assertTrue(report['credentialHashesRecorded'])
            self.assertNotIn(encoded, report_path.read_bytes())
            self.assertNotIn(value, report_path.read_bytes())
            leak.unlink()

    def test_effective_forbidden_missing_read_or_excess_write_under_original_policy_denies(self):
        relative = self.successors[0]['original']['secretReference']
        cases = [('data', 'secret/data/*', ['read']), ('data', subject.MOUNT + '/data/' + relative, ['list']),
                 ('metadata', subject.MOUNT + '/metadata/' + relative, ['read', 'update'])]
        for role, path, capabilities in cases:
            self.capability_overrides = {(role, path): capabilities}
            with self.subTest(role=role, capabilities=capabilities), self.assertRaisesRegex(RuntimeError, 'capability'):
                subject.maintain(self.instance)
            self.assertEqual((self.renewals, self.posts, self.replaces), ([], [], []))
            self.assertEqual(self.leases[role]['policies'], [subject.DATA_POLICY if role == 'data' else subject.METADATA_POLICY])

    def test_acknowledged_renewal_with_unverified_post_lookup_stays_unknown_and_never_repeats(self):
        plan = subject.inspect(self.instance)
        before = deepcopy(self.leases['data'])
        cap = subject.instant(before['issue_time']) + timedelta(seconds=604800)
        cases = [{'expire_time': before['expire_time']}, {'expire_time': (NOW + timedelta(hours=23)).isoformat()},
                 {'accessor': 'foreign-accessor'}, {'policies': ['foreign-policy']}, {'expire_time': (cap + timedelta(seconds=1)).isoformat()}]
        for changes in cases:
            self.leases['data'] = deepcopy(before)
            self.post_renewal_changes = {'data': changes}
            renewal = self.evidence / 'renewal-data.json'
            if renewal.exists():
                renewal.unlink()
            self.renewals.clear()
            with self.subTest(changes=changes), self.assertRaises(RuntimeError):
                subject.maintain(self.instance)
            self.assertEqual(subject.read(renewal)['phase'], 'OutcomeUnknown')
            with self.assertRaisesRegex(RuntimeError, 'observation-only-owner-reconciliation-required'):
                subject.maintain_lease(self.instance, plan, 'data')
            self.assertEqual(self.renewals, ['data'])
            self.assertEqual((self.posts, self.replaces), ([], []))


if __name__ == '__main__':
    unittest.main()
