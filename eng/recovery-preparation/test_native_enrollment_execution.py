"""Inert enrollment/execution fixtures; no real config, keys, output or network."""

from copy import deepcopy
from datetime import datetime, timedelta, timezone, tzinfo
import contextlib
import gc
import io
import json
import os
from pathlib import Path
import socket
import ssl
import stat
import tempfile
import types
import unittest
from unittest.mock import patch


def load_fixed_source():
    """Bootstrap only the one bounded fixed source, without a bytecode importer."""
    path = Path(__file__).absolute().with_name('native_enrollment_execution.py')
    directory = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
    descriptor = None
    try:
        for part in path.parts[1:-1]:
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC,
                            dir_fd=directory)
            os.close(directory)
            directory = child
            info = os.fstat(directory)
            mode = stat.S_IMODE(info.st_mode)
            if info.st_uid not in (0, os.getuid()) or (mode & 0o022 and not
                    (info.st_uid == 0 and mode & stat.S_ISVTX)):
                raise ValueError('source-custody-refused')
        descriptor = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC,
                             dir_fd=directory)
        before = os.fstat(descriptor)
        if (not stat.S_ISREG(before.st_mode) or before.st_nlink != 1
                or before.st_uid not in (0, os.getuid()) or stat.S_IMODE(before.st_mode) & 0o022
                or before.st_size > 1048576):
            raise ValueError('source-custody-refused')
        raw = bytearray()
        while len(raw) <= 1048576:
            chunk = os.read(descriptor, min(65536, 1048577 - len(raw)))
            if not chunk:
                break
            raw.extend(chunk)
        after = os.fstat(descriptor)
        fields = ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns', 'st_ctime_ns',
                  'st_mode', 'st_uid', 'st_gid', 'st_nlink')
        if len(raw) != before.st_size or any(getattr(before, key) != getattr(after, key) for key in fields):
            raise ValueError('source-custody-refused')
        module = types.ModuleType('native_enrollment_under_test')
        module.__file__ = str(path)
        exec(compile(bytes(raw), str(path), 'exec'), module.__dict__)
        return module
    finally:
        if descriptor is not None:
            os.close(descriptor)
        os.close(directory)


guard = load_fixed_source()
adapter = guard._adapter
SECRET = 'synthetic-sensitive-body-never-output'
UID = '01234567-89ab-cdef-0123-456789abcdef'
PAYLOAD = guard.INERT_PREFIX + b'inert-single-credential-never-log'
NOW = datetime(2026, 10, 16, 12, 0, tzinfo=timezone.utc)
RUNTIME_SHA = 'd' * 64
EXPECTED_SELECTORS = [
    'namespace-kube-system', 'namespace-keycloak', 'namespace-openbao', 'namespace-hexalith-memories',
    'version', 'readyz', 'keycloak-postgres', 'keycloak-keycloak', 'openbao-hexalith-keys',
    'hexalith-memories-redis-stack', 'hexalith-memories-falkordb', 'hexalith-memories-memories',
    'hexalith-memories-memories-mcp', 'hexalith-memories-memories-access-telemetry',
    'hexalith-memories-memories-access-telemetry-clock', 'keycloak-service',
    'openebs-hostpath', 'openebs-hostpath-retain', 'native-binding',
    'absent-s40-kcpre-20261015t162756z', 'absent-s40-kc-20261015t162756z',
    'absent-s40-bao-20261015t162756z', 'absent-s40-mem-20261015t162756z',
]


def expected_outputs():
    """Exact independent values; never reconstruct expectations from sanitizer output."""
    def identity(name, namespace='', uid=UID):
        return {'identity': {'name': name, 'namespace': namespace, 'uid': uid}}
    images = {'imageDigests': ['sha256:' + 'a' * 64]}
    replicas = {'replicas': 2, 'readyReplicas': 2}
    storage = {'provisioner': 'openebs.io/local', 'volumeBindingMode': 'WaitForFirstConsumer'}
    return [
        identity('kube-system', uid='b5860674-1d1e-4a3b-b413-e1bb3b9a4557'),
        identity('keycloak', uid='22744997-bdad-4ac5-85bf-631fc700d4ea'),
        identity('openbao'), identity('hexalith-memories'),
        {'version': 'v1.34.12'}, {'ready': True},
        {**identity('keycloak-postgres', 'keycloak', '56d8d39c-d688-48f2-aa27-a549400352ce'),
         'systemId': '7689076343157272595', 'timeline': 2, 'primaryOrdinal': 2, **images},
        {**identity('keycloak', 'keycloak', '98fc84f3-918f-46d9-ae22-7584fb31ff9b'), **replicas, **images},
        {**identity('hexalith-keys', 'openbao', 'df2bbbbe-1fe8-4e0b-a8a6-174bd3287806'), **replicas, **images},
        {**identity('redis-stack', 'hexalith-memories'), **replicas, **images},
        {**identity('falkordb', 'hexalith-memories'), **replicas, **images},
        {**identity('memories', 'hexalith-memories'), **replicas},
        {**identity('memories-mcp', 'hexalith-memories'), **replicas},
        {**identity('memories-access-telemetry', 'hexalith-memories'), **replicas},
        {**identity('memories-access-telemetry-clock', 'hexalith-memories'), **replicas},
        {**identity('keycloak', 'keycloak', '0ac9cd04-9d7e-41ed-8ac7-5a74ca659fd0'),
         'serviceType': 'ClusterIP', 'ports': [{'port': 8080, 'protocol': 'TCP', 'targetPort': 8081}]},
        {**identity('openebs-hostpath'), 'storageClassShape': {**storage, 'reclaimPolicy': 'Delete'}},
        {**identity('openebs-hostpath-retain'), 'storageClassShape': {**storage, 'reclaimPolicy': 'Retain'}},
        {**identity('kubeadm:cluster-admins', uid='8224bcb7-57c6-437d-a27c-1120d03c8b4b'),
         'historicalNativeBindingShape': True, 'currentAuthorityAccepted': False},
        {'targetAbsent': True, 'name': 's40-kcpre-20261015t162756z'},
        {'targetAbsent': True, 'name': 's40-kc-20261015t162756z'},
        {'targetAbsent': True, 'name': 's40-bao-20261015t162756z'},
        {'targetAbsent': True, 'name': 's40-mem-20261015t162756z'},
    ]


def rebind_client_witness_subjects(binding):
    subject = guard.sha(guard._canonical(binding['enrollment']['clientIdentity']))
    for name in guard.WITNESSES:
        binding['enrollment'][name]['subjectSha256'] = subject


def fixture(request):
    """Independent inert Kubernetes-shaped response with deliberately private noise."""
    mode = request['mode']
    if mode == 'readyz':
        return adapter.Response(200, 'text/plain', (b'[+]ping ok\n[+]etcd ok\nreadyz check passed\n',))
    if mode == 'version':
        value = {'major': '1', 'minor': '34', 'gitVersion': 'v1.34.12', 'secret': SECRET}
    elif mode == 'absence':
        value = {'kind': 'Status', 'apiVersion': 'v1', 'status': 'Failure', 'reason': 'NotFound',
                 'code': 404, 'details': {'name': request['name'], 'kind': 'namespaces'}, 'message': SECRET}
    else:
        value = {'kind': request['kind'], 'apiVersion': request['apiVersion'], 'metadata': {
            'name': request['name'], 'namespace': request['namespace'],
            'uid': request['historicalUid'] or UID, 'annotations': {'secret': SECRET},
            'managedFields': [SECRET]}}
        image = 'synthetic.invalid/image@sha256:' + 'a' * 64
        if mode == 'workload':
            value['metadata']['generation'] = 3
            value.update(spec={'replicas': 2, 'template': {'spec': {'containers': [
                {'name': 'main', 'image': image, 'env': [{'value': SECRET}]}]}}},
                status={'readyReplicas': 2, 'observedGeneration': 3, 'message': SECRET})
        elif mode == 'postgres':
            value.update(spec={'imageName': image}, status={'conditions': [
                {'type': 'Ready', 'status': 'True', 'message': SECRET}],
                'systemID': '7689076343157272595', 'timelineID': 2, 'currentPrimary': 'keycloak-postgres-2'})
        elif mode == 'service':
            value['spec'] = {'type': 'ClusterIP', 'ports': [{'port': 8080, 'targetPort': 8081}]}
        elif mode == 'storageclass':
            value.update(provisioner='openebs.io/local',
                         reclaimPolicy='Retain' if request['name'].endswith('-retain') else 'Delete',
                         volumeBindingMode='WaitForFirstConsumer')
        elif mode == 'authority':
            value.update(roleRef={'apiGroup': 'rbac.authorization.k8s.io', 'kind': 'ClusterRole',
                                  'name': 'cluster-admin'},
                         subjects=[{'kind': 'Group', 'apiGroup': 'rbac.authorization.k8s.io',
                                    'name': 'kubeadm:cluster-admins'}])
    return adapter.Response(404 if mode == 'absence' else 200, 'application/json',
                            (json.dumps(value).encode(),))


def complete_binding(custody):
    """Synthetic public metadata/witnesses; no operational fact is inferred."""
    value = guard.binding_template(RUNTIME_SHA)
    enrollment = value['enrollment']
    enrollment['serverTrust'] = {'endpoint': 'https://192.168.1.30:6443', 'ipSan': '192.168.1.30',
        'trustSourceSha256': '1' * 64, 'caSha256': '2' * 64, 'peerSpkiSha256': '3' * 64}
    enrollment['clientIdentity'] = {'kind': guard.INERT_KIND, 'subject': 'synthetic:client',
        'issuer': 'synthetic:issuer', 'groups': ['synthetic:native-read'],
        'publicCertificateSha256': '4' * 64, 'credentialSha256': guard.sha(PAYLOAD),
        'validFrom': adapter.PREPARED, 'validUntil': adapter.CEILING}
    evidence = {
        'nativeGrant': {'currentGrantUid': UID, 'roleSha256': '5' * 64, 'subjectsSha256': '6' * 64,
                        'independent': True},
        'revocation': {'completeLineage': True, 'state': 'not-revoked-in-fixture', 'lineageSha256': '7' * 64},
        'rights': {'allowed': [{'method': item['method'], 'path': item['path']}
                              for item in adapter.request_plan()]},
        'breakGlass': {'custodySha256': '8' * 64, 'independent': True},
        'cnpgController': {'name': 'synthetic:cnpg-controller', 'uid': UID,
                           'watchNamespaces': ['keycloak', 'cnpg-system']},
        'measurementApproval': {'approvalId': 'synthetic:measurement-approval', 'separateFromGrant': True},
    }
    subject = guard.sha(guard._canonical(enrollment['clientIdentity']))
    scope = guard.sha(guard._canonical({'contract': adapter.contract(),
        'serverTrust': enrollment['serverTrust'], 'tlsRequirements': guard.tls_requirements()}))
    for name in guard.WITNESSES:
        enrollment[name] = {'kind': 'synthetic-independent-' + name, 'fixtureOnly': True,
            'provenance': 'inert-independent-witness-fixture', 'artifactSha256': guard.sha(name.encode()),
            'subjectSha256': subject, 'scopeSha256': scope, 'observedAt': adapter.PREPARED,
            'validUntil': adapter.CEILING, 'evidence': evidence[name]}
    enrollment['credentialDelivery'] = {'kind': guard.INERT_KIND, 'sha256': guard.sha(PAYLOAD),
        'bytes': len(PAYLOAD), 'singleUse': True, 'channel': 'protected-in-memory-inert-fixture'}
    enrollment['outputCustody'] = custody.binding()
    return value


class EnrollmentExecutionTests(unittest.TestCase):
    def setUp(self):
        self.custody = guard.SyntheticOutputCustody()
        self.addCleanup(self.custody.close)
        self.binding = complete_binding(self.custody)
        self.runtime = guard.runtime_requirements(RUNTIME_SHA)

    def run_fixture(self, binding=None, responses=None, delivery=None, transport=None,
                    clock=None, custody=None, runtime=None):
        raw = guard._canonical(self.binding if binding is None else binding)
        if transport is None:
            transport = adapter.SyntheticTransport([fixture(request) for request in adapter.request_plan()]
                                                   if responses is None else responses)
        if delivery is None:
            delivery = guard.SyntheticCredentialDelivery([{'kind': guard.INERT_KIND, 'payload': PAYLOAD}])
        result = guard.qualify_synthetic(raw, guard.sha(raw), self.runtime if runtime is None else runtime,
            delivery, transport, self.custody if custody is None else custody,
            guard.SyntheticClock([NOW]) if clock is None else clock)
        return result, transport, delivery

    def assert_closed(self, result):
        self.assertTrue(result['syntheticOnly'])
        self.assertEqual(result['operationalGate'], 'closed')
        self.assertEqual(result['apiCalls'], 0)
        for key in ('authenticationPerformed', 'authorityAccepted', 'executionApprovalReady',
                    'captureOrExecutionAuthorized', 'providerOrIamQualified'):
            self.assertIs(result[key], False)
        self.assertNotIn(SECRET, json.dumps(result))
        self.assertNotIn(PAYLOAD.decode(), json.dumps(result))

    def test_complete_synthetic_fixture_exact_bounded_pass_never_authenticates(self):
        before = datetime.now(timezone.utc)
        result, transport, delivery = self.run_fixture()
        self.assertEqual(result.get('reason'), None)
        self.assertEqual(result['qualification'], 'pass')
        self.assert_closed(result)
        self.assertEqual(transport.requests, adapter.request_plan())
        self.assertEqual(result['syntheticRequests'], 23)
        self.assertEqual(len(result['measurements']), 23)
        expected = [{'selector': selector, 'output': output}
                    for selector, output in zip(EXPECTED_SELECTORS, expected_outputs(), strict=True)]
        self.assertEqual(result['measurements'], expected)
        self.assertEqual(len(result['unsupportedSelectors']), 5)
        self.assertTrue(result['credentialConsumed'])
        self.assertTrue(result['outputRetained'])
        self.assertTrue(delivery._consumed)
        self.assertEqual(set(delivery._payload), {0})
        observed = datetime.fromisoformat(result['observedAt'].replace('Z', '+00:00'))
        self.assertLessEqual(before, observed)
        self.assertLessEqual(observed, datetime.now(timezone.utc))
        self.assertEqual(result['policyPreparedAt'], '2026-10-15T16:27:56Z')
        self.assertEqual(result['policyCeiling'], '2026-10-16T16:27:56Z')
        slot = self.custody.path / 'measurement-output'
        output = slot / 'receipt.json'
        self.assertEqual(set(os.listdir(slot)), {'receipt.json'})
        self.assertEqual(stat.S_IMODE(slot.stat().st_mode), 0o700)
        self.assertEqual(stat.S_IMODE(output.stat().st_mode), 0o600)
        self.assertEqual(output.stat().st_nlink, 1)
        self.assertLessEqual(output.stat().st_size, 65536)
        retained = output.read_bytes()
        self.assertEqual(json.loads(retained)['measurements'], expected)
        self.assertNotIn(SECRET.encode(), retained)
        self.assertNotIn(PAYLOAD, retained)
        self.assertFalse(json.loads(retained)['outputRetained'])
        self.assertEqual(json.loads(retained)['retainedReceiptAcceptance'],
                         'provisional-until-post-write-validation')

    def test_missing_and_partial_independent_fields_stop_before_delivery_or_requests(self):
        template = guard.binding_template(RUNTIME_SHA)
        raw = guard._canonical(template)
        result = guard.inspect_binding(raw, guard.sha(raw), self.runtime)
        self.assertEqual(result['qualification'], 'inspection-only')
        self.assertEqual(result['missingFields'], ['enrollment.' + name for name in guard.ENROLLMENT_FIELDS])
        self.assert_closed(result)
        for name in guard.ENROLLMENT_FIELDS:
            for replacement in (None, {}):
                with self.subTest(field=name, replacement=replacement):
                    changed = deepcopy(self.binding)
                    changed['enrollment'][name] = replacement
                    result, transport, delivery = self.run_fixture(binding=changed)
                    self.assertEqual(result['qualification'], 'refused')
                    self.assertTrue(result['missingFields'])
                    self.assertTrue(all(field.startswith('enrollment.' + name) for field in result['missingFields']))
                    self.assertEqual(transport.requests, [])
                    self.assertFalse(delivery._consumed)
                    self.assertFalse((self.custody.path / 'measurement-output').exists())

    def test_missing_each_nested_witness_fact_remains_explicit(self):
        for name in guard.WITNESSES:
            for field in self.binding['enrollment'][name]['evidence']:
                with self.subTest(witness=name, field=field):
                    changed = deepcopy(self.binding)
                    changed['enrollment'][name]['evidence'][field] = None
                    result, transport, _ = self.run_fixture(binding=changed)
                    self.assertEqual(result['missingFields'], ['enrollment.' + name + '.evidence.' + field])
                    self.assertEqual(transport.requests, [])

    def test_missing_evidence_does_not_mask_malformed_populated_fields(self):
        mutations = [
            lambda e: e['clientIdentity'].__setitem__('kind', 'bearer'),
            lambda e: e['serverTrust'].__setitem__('endpoint', 'https://wrong.invalid'),
            lambda e: e['credentialDelivery'].__setitem__('kind', 'bearer'),
            lambda e: e['revocation']['evidence'].__setitem__('completeLineage', False),
            lambda e: e['rights']['evidence'].__setitem__('allowed', []),
            lambda e: e['outputCustody'].__setitem__('slot', '../elsewhere'),
            lambda e: e['measurementApproval'].__setitem__('evidence', []),
        ]
        for mutate in mutations:
            with self.subTest(mutation=mutations.index(mutate)):
                changed = deepcopy(self.binding)
                changed['enrollment']['nativeGrant'] = None
                mutate(changed['enrollment'])
                raw = guard._canonical(changed)
                inspected = guard.inspect_binding(raw, guard.sha(raw), self.runtime)
                self.assertEqual(inspected['qualification'], 'refused')
                self.assertNotIn('integrityMatched', inspected)
                result, transport, delivery = self.run_fixture(binding=changed)
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(transport.requests, [])
                self.assertFalse(delivery._consumed)
                self.assert_closed(result)

    def test_integrity_matching_public_metadata_and_caller_approval_never_grant_authority(self):
        raw = guard._canonical(self.binding)
        result = guard.inspect_binding(raw, guard.sha(raw), self.runtime)
        self.assertTrue(result['integrityMatched'])
        self.assertFalse(result['witnessesAuthenticated'])
        self.assert_closed(result)
        for extra in ('authenticated', 'approved', 'privateKey', 'bearerToken'):
            changed = deepcopy(self.binding)
            changed['enrollment']['clientIdentity'][extra] = SECRET
            result, transport, _ = self.run_fixture(binding=changed)
            self.assertEqual(result['qualification'], 'refused')
            self.assertEqual(transport.requests, [])
            self.assert_closed(result)

    def test_incomplete_revoked_nonindependent_wrong_scope_and_reused_witnesses_refuse(self):
        changes = [
            ('nativeGrant', 'evidence', 'independent', False),
            ('revocation', 'evidence', 'completeLineage', False),
            ('revocation', 'evidence', 'state', 'revoked'),
            ('breakGlass', 'evidence', 'independent', False),
            ('measurementApproval', 'evidence', 'separateFromGrant', False),
            ('cnpgController', 'evidence', 'watchNamespaces', ['keycloak']),
        ]
        for name, nested, field, value in changes:
            changed = deepcopy(self.binding)
            changed['enrollment'][name][nested][field] = value
            result, transport, _ = self.run_fixture(binding=changed)
            self.assertEqual(result['qualification'], 'refused')
            self.assertEqual(transport.requests, [])
        for name in guard.WITNESSES:
            for field, value in (('subjectSha256', '0' * 64), ('scopeSha256', '0' * 64),
                                 ('fixtureOnly', False), ('provenance', 'caller-assertion')):
                changed = deepcopy(self.binding)
                changed['enrollment'][name][field] = value
                result, transport, _ = self.run_fixture(binding=changed)
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(transport.requests, [])
        changed = deepcopy(self.binding)
        changed['enrollment']['rights']['artifactSha256'] = changed['enrollment']['nativeGrant']['artifactSha256']
        result, transport, _ = self.run_fixture(binding=changed)
        self.assertEqual(transport.requests, [])
        self.assertEqual(result['qualification'], 'refused')

    def test_rights_must_match_all_23_ordered_gets_and_all_unsupported_families_stay_blocked(self):
        rights = self.binding['enrollment']['rights']['evidence']['allowed']
        variants = [rights[:-1], rights[::-1], rights + [rights[0]],
                    [{'method': 'POST', 'path': rights[0]['path']}] + rights[1:]]
        variants.extend(rights + [{'method': 'GET', 'path': path}]
                        for path in adapter.contract()['unsupportedSelectors'])
        for variant in variants:
            changed = deepcopy(self.binding)
            changed['enrollment']['rights']['evidence']['allowed'] = variant
            result, transport, _ = self.run_fixture(binding=changed)
            self.assertEqual(result['qualification'], 'refused')
            self.assertEqual(transport.requests, [])

    def test_server_client_typed_schema_plan_source_and_runtime_drift_stop_before_delivery(self):
        mutations = [
            lambda b: b.__setitem__('schemaVersion', True),
            lambda b: b.__setitem__('sealedSupplementSha256', '0' * 64),
            lambda b: b.__setitem__('policyCeiling', '2099-01-01T00:00:00Z'),
            lambda b: b['sourceBindings']['native_measurement_adapter.py'].__setitem__('sha256', '0' * 64),
            lambda b: b['contract']['requests'][0].__setitem__('path', '/api/v1/secrets'),
            lambda b: b['contract']['limits'].__setitem__('attemptsPerSelector', 2),
            lambda b: b['contract']['transport'].__setitem__('redirects', True),
            lambda b: b['enrollment']['serverTrust'].__setitem__('endpoint', 'https://synthetic.invalid'),
            lambda b: b['enrollment']['serverTrust'].__setitem__('caSha256', SECRET),
            lambda b: b['enrollment']['clientIdentity'].__setitem__('kind', 'bearer'),
            lambda b: b['enrollment']['outputCustody'].__setitem__('rootInode', True),
        ]
        for mutate in mutations:
            changed = deepcopy(self.binding)
            mutate(changed)
            result, transport, delivery = self.run_fixture(binding=changed)
            self.assertEqual(result['qualification'], 'refused')
            self.assertEqual(transport.requests, [])
            self.assertFalse(delivery._consumed)

    def test_wrong_openssl_version_behavior_or_runtime_inventory_refuses_without_tls_loading(self):
        changes = [lambda r: r['tlsRequirements'].__setitem__('opensslVersion', 'OpenSSL wrong'),
                   lambda r: r['tlsRequirements'].__setitem__('opensslVersionInfo', [3, 5, 0, True, 0]),
                   lambda r: r['tlsRequirements'].__setitem__('defaultTrustLoading', True),
                   lambda r: r['tlsRequirements'].__setitem__('opensslDefaultConfigurationLoading', True),
                   lambda r: r['tlsRequirements'].__setitem__('minimumTlsVersion', 'TLSv1.2'),
                   lambda r: r.__setitem__('inventorySha256', '0' * 64)]
        with patch.object(ssl, 'create_default_context', side_effect=AssertionError('default trust')), \
                patch.object(ssl, 'SSLContext', side_effect=AssertionError('TLS context')), \
                patch.object(socket, 'socket', side_effect=AssertionError('network')):
            for mutate in changes:
                runtime = deepcopy(self.runtime)
                mutate(runtime)
                result, transport, _ = self.run_fixture(runtime=runtime)
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(transport.requests, [])

    def test_nested_runtime_objects_are_rejected_before_json_callbacks_or_unbounded_recursion(self):
        calls = []
        class PoisonDict(dict):
            def items(self):
                calls.append('items')
                raise AssertionError('dictionary callback invoked')
        class PoisonList(list):
            def __iter__(self):
                calls.append('iter')
                raise AssertionError('list callback invoked')
        class PoisonKey:
            def __str__(self):
                calls.append('str')
                raise AssertionError('key callback invoked')
        values = [PoisonDict(), PoisonList(), {PoisonKey(): 'inert'}, object()]
        nested = []
        for _ in range(18):
            nested = [nested]
        values.append(nested)
        for value in values:
            runtime = deepcopy(self.runtime)
            runtime['tlsRequirements'] = value
            result, transport, delivery = self.run_fixture(runtime=runtime)
            self.assertEqual(result['qualification'], 'refused')
            self.assertEqual(transport.requests, [])
            self.assertFalse(delivery._consumed)
        self.assertEqual(calls, [])

    def test_custom_timezone_is_refused_without_utcoffset_callback(self):
        calls = []
        class PoisonTimezone(tzinfo):
            def utcoffset(self, _):
                calls.append('utcoffset')
                raise AssertionError('timezone callback invoked')
        sample = datetime(2026, 10, 16, 12, 0, tzinfo=PoisonTimezone())
        result, transport, delivery = self.run_fixture(clock=guard.SyntheticClock([sample]))
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(transport.requests, [])
        self.assertFalse(delivery._consumed)
        self.assertEqual(calls, [])

    def test_credential_kind_extra_credential_noninert_and_oversize_never_get_a_delivery(self):
        for values in ([], [{'kind': guard.INERT_KIND, 'payload': PAYLOAD}] * 2,
                       [{'kind': 'bearer', 'payload': PAYLOAD}],
                       [{'kind': guard.INERT_KIND, 'payload': b'-----BEGIN PRIVATE KEY-----'}],
                       [{'kind': guard.INERT_KIND, 'payload': guard.INERT_PREFIX + b'x' * 4096}]):
            with self.assertRaises(guard.Refusal):
                guard.SyntheticCredentialDelivery(values)

    def test_poison_credential_kind_and_response_field_callbacks_are_never_invoked(self):
        calls = []
        class Poison:
            def __eq__(self, _):
                calls.append('eq')
                raise AssertionError('callback invoked')
            def __len__(self):
                calls.append('len')
                raise AssertionError('callback invoked')
            def __iter__(self):
                calls.append('iter')
                raise AssertionError('callback invoked')
        with self.assertRaises(guard.Refusal):
            guard.SyntheticCredentialDelivery([{'kind': Poison(), 'payload': PAYLOAD}])
        for field, value in (('status', Poison()), ('content_type', Poison()), ('chunks', (Poison(),))):
            with self.subTest(field=field), guard.SyntheticOutputCustody() as custody:
                response = fixture(adapter.request_plan()[0])
                setattr(response, field, value)
                result, transport, _ = self.run_fixture(binding=complete_binding(custody),
                    custody=custody, responses=[response])
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(len(transport.requests), 1)
                self.assertEqual(result['measurements'], [])
        self.assertEqual(calls, [])

    def test_wrong_credential_digest_consumes_once_wipes_and_never_falls_back(self):
        changed = deepcopy(self.binding)
        changed['enrollment']['clientIdentity']['credentialSha256'] = '0' * 64
        changed['enrollment']['credentialDelivery']['sha256'] = '0' * 64
        subject = guard.sha(guard._canonical(changed['enrollment']['clientIdentity']))
        for name in guard.WITNESSES:
            changed['enrollment'][name]['subjectSha256'] = subject
        result, transport, delivery = self.run_fixture(binding=changed)
        self.assertEqual(result['qualification'], 'refused')
        self.assertTrue(result['credentialConsumed'])
        self.assertTrue(delivery._consumed)
        self.assertEqual(set(delivery._payload), {0})
        self.assertEqual(transport.requests, [])
        self.assertNotIn(PAYLOAD.decode(), repr(delivery))
        with self.assertRaises(guard.Refusal):
            delivery.consume(self.binding['enrollment']['credentialDelivery'])

    def test_credential_transport_and_custody_cannot_be_reused_after_failure(self):
        result, transport, delivery = self.run_fixture(responses=[adapter.Response(403, 'application/json', (b'{}',))])
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(len(transport.requests), 1)
        result, fresh, _ = self.run_fixture(delivery=delivery)
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(fresh.requests, [])
        result, _, _ = self.run_fixture(transport=transport)
        self.assertEqual(result['syntheticRequests'], 0)
        with self.assertRaises(guard.Refusal):
            self.custody.reserve(self.binding['enrollment']['outputCustody'])

    def test_postconstruction_delivery_slot_poisoning_never_invokes_callbacks(self):
        calls = []
        class Poison:
            def __bool__(self):
                calls.append('bool')
                raise AssertionError('delivery state consulted')
            def __eq__(self, _):
                calls.append('eq')
                raise AssertionError('delivery kind compared')
            def __len__(self):
                calls.append('len')
                raise AssertionError('delivery payload sized')
            def __getitem__(self, _):
                calls.append('getitem')
                raise AssertionError('delivery payload indexed')
            def __setitem__(self, *_):
                calls.append('setitem')
                raise AssertionError('delivery payload wiped')
        for slot in ('_kind', '_consumed', '_payload'):
            with self.subTest(slot=slot):
                delivery = guard.SyntheticCredentialDelivery([{'kind': guard.INERT_KIND, 'payload': PAYLOAD}])
                setattr(delivery, slot, Poison())
                result, transport, _ = self.run_fixture(delivery=delivery)
                self.assertEqual(result['qualification'], 'refused')
                self.assertIs(type(result['credentialConsumed']), bool)
                self.assertFalse(result['credentialConsumed'])
                self.assertEqual(transport.requests, [])
                self.assertEqual(result['measurements'], [])
                self.assert_closed(result)
        self.assertEqual(calls, [])

    def test_unsafe_slot_alias_modes_and_existing_slot_are_refused_without_overwrite(self):
        for field, value in (('slot', '../escape'), ('successFile', '/tmp/receipt.json'),
                             ('failureFile', 'elsewhere/failure.json'), ('fileMode', 0o644),
                             ('directoryMode', 0o755), ('attempts', True)):
            changed = deepcopy(self.binding)
            changed['enrollment']['outputCustody'][field] = value
            result, transport, _ = self.run_fixture(binding=changed)
            self.assertEqual(result['qualification'], 'refused')
            self.assertEqual(transport.requests, [])
        slot = self.custody.path / 'measurement-output'
        slot.mkdir(mode=0o700)
        original = slot / 'failure.json'
        original.write_bytes(b'inert retained failure')
        result, transport, _ = self.run_fixture()
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(transport.requests, [])
        self.assertEqual(original.read_bytes(), b'inert retained failure')

    def test_symlink_root_or_slot_and_replaced_root_fail_before_dependent_work(self):
        root = self.custody.path
        retained = root.with_name(root.name + '-retained')
        root.rename(retained)
        try:
            root.symlink_to(retained, target_is_directory=True)
            result, transport, _ = self.run_fixture()
            self.assertEqual(result['qualification'], 'refused')
            self.assertEqual(transport.requests, [])
            root.unlink()
            root.mkdir(mode=0o700)
            result, transport, _ = self.run_fixture()
            self.assertEqual(result['qualification'], 'refused')
            self.assertEqual(transport.requests, [])
            root.rmdir()
        finally:
            if root.is_symlink():
                root.unlink()
            retained.rename(root)
        target = root / 'inert-target'
        target.mkdir(mode=0o700)
        (root / 'measurement-output').symlink_to(target, target_is_directory=True)
        result, transport, _ = self.run_fixture()
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(transport.requests, [])
        self.assertEqual(os.listdir(target), [])

    def test_output_file_alias_either_slot_or_unknown_member_prevents_all_writes(self):
        for name in ('receipt.json', 'failure.json', 'unexpected.json'):
            with self.subTest(name=name), guard.SyntheticOutputCustody() as custody:
                binding = complete_binding(custody)
                target = custody.path / 'inert-target'
                target.write_bytes(b'inert original bytes')
                reserve = custody.reserve
                def occupy(expected):
                    reserve(expected)
                    (custody.path / 'measurement-output' / name).symlink_to(target)
                with patch.object(custody, 'reserve', side_effect=occupy):
                    result, transport, _ = self.run_fixture(binding=binding, custody=custody)
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(transport.requests, [])
                self.assertEqual(target.read_bytes(), b'inert original bytes')
                self.assertEqual(os.listdir(custody.path / 'measurement-output'), [name])

    def test_wrong_output_owner_mode_and_root_binding_refuse(self):
        self.custody.path.chmod(0o755)
        try:
            result, transport, _ = self.run_fixture()
            self.assertEqual(result['qualification'], 'refused')
            self.assertEqual(transport.requests, [])
        finally:
            self.custody.path.chmod(0o700)
        changed = deepcopy(self.binding)
        changed['enrollment']['outputCustody']['rootInode'] += 1
        result, transport, _ = self.run_fixture(binding=changed)
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(transport.requests, [])

    def test_close_and_finalization_preserve_foreign_replacement_directory(self):
        custody = guard.SyntheticOutputCustody()
        root = custody.path
        owned = root.with_name(root.name + '-retained-owned')
        root.rename(owned)
        root.mkdir(mode=0o700)
        marker = root / 'foreign-replacement-marker'
        marker.write_bytes(b'inert foreign replacement must survive')
        temporary = custody._temporary
        try:
            with self.assertRaises(guard.Refusal):
                custody.close()
            self.assertEqual(marker.read_bytes(), b'inert foreign replacement must survive')
            self.assertIsNone(custody._root)
            del custody
            del temporary
            gc.collect()
            self.assertEqual(marker.read_bytes(), b'inert foreign replacement must survive')
            self.assertTrue(owned.is_dir())
        finally:
            marker.unlink()
            root.rmdir()
            owned.rmdir()

    def test_short_write_one_attempt_and_no_fallback_or_partial_raw_output(self):
        with patch.object(guard.os, 'write', return_value=1) as write:
            result, transport, _ = self.run_fixture()
        self.assertEqual(result['qualification'], 'refused')
        self.assertFalse(result['outputRetained'])
        self.assertEqual(write.call_count, 1)
        self.assertEqual(len(transport.requests), 23)
        slot = self.custody.path / 'measurement-output'
        self.assertEqual(os.listdir(slot), ['receipt.json'])
        self.assertEqual((slot / 'receipt.json').read_bytes(), b'')

    def test_expiry_during_output_fsync_cannot_return_pass_and_provisional_attempt_is_retained(self):
        clock = guard.SyntheticClock([NOW])
        fsync = guard.os.fsync
        def expire(descriptor):
            fsync(descriptor)
            clock.samples = (guard._ceiling(),)
        with patch.object(guard.os, 'fsync', side_effect=expire):
            result, transport, _ = self.run_fixture(clock=clock)
        self.assertEqual(result['qualification'], 'refused')
        self.assertFalse(result['outputRetained'])
        self.assertEqual(result['measurements'], [])
        self.assertNotIn('bindingSha256', result)
        self.assertNotIn('runtimeInventorySha256', result)
        self.assertEqual(len(transport.requests), 23)
        slot = self.custody.path / 'measurement-output'
        self.assertEqual(os.listdir(slot), ['receipt.json'])
        provisional = json.loads((slot / 'receipt.json').read_bytes())
        self.assertFalse(provisional['outputRetained'])
        self.assertIn('provisional', provisional['retainedReceiptAcceptance'])
        self.assert_closed(provisional)

    def test_expiry_during_last_custody_check_is_enforced_after_slow_checks(self):
        for fault in ('utc', 'monotonic'):
            with self.subTest(fault=fault), guard.SyntheticOutputCustody() as custody:
                binding = complete_binding(custody)
                clock = guard.SyntheticClock([NOW])
                monotonic = [1.0]
                original = custody.binding
                def slow_binding():
                    value = original()
                    if custody._retained_stamp is not None:
                        if fault == 'utc':
                            clock.samples = (guard._ceiling(),)
                        else:
                            monotonic[0] += adapter.LANE_SECONDS
                    return value
                with patch.object(custody, 'binding', side_effect=slow_binding), \
                        patch.object(guard.time, 'monotonic', side_effect=lambda: monotonic[0]):
                    result, transport, _ = self.run_fixture(binding=binding, custody=custody, clock=clock)
                self.assertEqual(result['qualification'], 'refused')
                self.assertFalse(result['outputRetained'])
                self.assertEqual(result['measurements'], [])
                self.assertNotIn('bindingSha256', result)
                self.assertNotIn('runtimeInventorySha256', result)
                self.assertEqual(len(transport.requests), 23)
                self.assertTrue((custody.path / 'measurement-output/receipt.json').is_file())

    def test_runtime_or_source_drift_during_output_fsync_cannot_return_pass(self):
        for drift in ('runtime', 'source'):
            with self.subTest(drift=drift), guard.SyntheticOutputCustody() as custody:
                binding = complete_binding(custody)
                runtime = deepcopy(self.runtime)
                fsync = guard.os.fsync
                source_bindings = guard._source_bindings
                changed = [False]
                def mutate(descriptor):
                    fsync(descriptor)
                    changed[0] = True
                    if drift == 'runtime':
                        runtime['tlsRequirements']['opensslVersion'] = 'wrong'
                def drifted_sources():
                    value = source_bindings()
                    if changed[0] and drift == 'source':
                        value['native_enrollment_execution.py']['sha256'] = '0' * 64
                    return value
                with patch.object(guard.os, 'fsync', side_effect=mutate), \
                        patch.object(guard, '_source_bindings', side_effect=drifted_sources):
                    result, transport, _ = self.run_fixture(binding=binding, custody=custody, runtime=runtime)
                self.assertEqual(result['qualification'], 'refused')
                self.assertFalse(result['outputRetained'])
                self.assertEqual(len(transport.requests), 23)
                self.assertEqual(result['measurements'], [])

    def test_replaced_receipt_inode_after_retain_refuses_and_preserves_attempted_bytes(self):
        retain = self.custody._retain
        kept = self.custody.path / 'retained-original-receipt.json'
        def replace(result):
            retain(result)
            receipt = self.custody.path / 'measurement-output/receipt.json'
            original = receipt.read_bytes()
            receipt.rename(kept)
            receipt.write_bytes(original)
            receipt.chmod(0o600)
        with patch.object(self.custody, '_retain', side_effect=replace):
            result, transport, _ = self.run_fixture()
        self.assertEqual(result['qualification'], 'refused')
        self.assertFalse(result['outputRetained'])
        self.assertEqual(result['measurements'], [])
        self.assertEqual(len(transport.requests), 23)
        receipt = self.custody.path / 'measurement-output/receipt.json'
        self.assertEqual(kept.read_bytes(), receipt.read_bytes())
        self.assertNotEqual(kept.stat().st_ino, receipt.stat().st_ino)
        self.assertEqual(os.listdir(receipt.parent), ['receipt.json'])

    def test_same_length_receipt_mutation_after_retain_refuses_and_keeps_both_versions(self):
        retain = self.custody._retain
        kept = self.custody.path / 'retained-before-mutation.json'
        mutated = []
        def mutate(result):
            retain(result)
            receipt = self.custody.path / 'measurement-output/receipt.json'
            original = receipt.read_bytes()
            kept.write_bytes(original)
            changed = original.replace(b'"qualification":"pass"', b'"qualification":"fail"')
            self.assertNotEqual(original, changed)
            self.assertEqual(len(original), len(changed))
            receipt.write_bytes(changed)
            mutated.append(changed)
        with patch.object(self.custody, '_retain', side_effect=mutate):
            result, transport, _ = self.run_fixture()
        self.assertEqual(result['qualification'], 'refused')
        self.assertFalse(result['outputRetained'])
        self.assertEqual(result['measurements'], [])
        self.assertEqual(len(transport.requests), 23)
        receipt = self.custody.path / 'measurement-output/receipt.json'
        self.assertEqual(receipt.stat().st_size, kept.stat().st_size)
        self.assertEqual(receipt.read_bytes(), mutated[0])
        self.assertNotEqual(guard.sha(receipt.read_bytes()), guard.sha(kept.read_bytes()))

    def test_status_malformed_response_and_response_limit_stop_first_request_without_raw_results(self):
        responses = [adapter.Response(status, 'application/json', (SECRET.encode(),))
                     for status in (301, 403, 404, 429, 500, True)]
        responses += [adapter.Response(200, 'application/json', (raw,)) for raw in
                      (b'', b'{}', b'{"x":1,"x":2}', b'{"x":NaN}', b'\xff',
                       b'[' * 2000 + b']' * 2000, b'x' * (adapter.MAX_BODY + 1))]
        for response in responses:
            with self.subTest(status=response.status, bytes=len(response.chunks[0])), \
                    guard.SyntheticOutputCustody() as custody:
                result, transport, _ = self.run_fixture(binding=complete_binding(custody),
                    custody=custody, responses=[response])
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(len(transport.requests), 1)
                self.assertEqual(result['measurements'], [])
                self.assert_closed(result)
                failure = custody.path / 'measurement-output/failure.json'
                self.assertTrue(failure.is_file())
                self.assertNotIn(SECRET.encode(), failure.read_bytes())

    def test_total_body_receipt_caps_and_first_failure_prevent_dependent_requests(self):
        first = len(fixture(adapter.request_plan()[0]).chunks[0])
        for setting, cap, expected in (('MAX_TOTAL_BODY', first + 1, 2),
                ('MAX_OUTPUT', len(guard._canonical(guard._receipt())) + 1, 1)):
            with self.subTest(setting=setting), patch.object(adapter, setting, cap), \
                    guard.SyntheticOutputCustody() as custody:
                result, transport, _ = self.run_fixture(binding=complete_binding(custody), custody=custody)
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(result['measurements'], [])
                self.assertEqual(len(transport.requests), expected)

    def test_prepreparation_expiry_invalid_and_rollback_clocks_stop_simulation_inspection_remains(self):
        for now in (guard._prepared() - timedelta(seconds=1), guard._ceiling(),
                    guard._ceiling() + timedelta(seconds=1), datetime(2026, 10, 16), SECRET):
            result, transport, delivery = self.run_fixture(clock=guard.SyntheticClock([now]))
            self.assertEqual(result['qualification'], 'refused')
            self.assertEqual(transport.requests, [])
            self.assertFalse(delivery._consumed)
            self.assert_closed(result)
        clock = guard.SyntheticClock([NOW] * 4 + [NOW - timedelta(seconds=1)])
        result, transport, _ = self.run_fixture(clock=clock)
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(len(transport.requests), 1)
        self.assertEqual(result['measurements'], [])
        raw = guard._canonical(self.binding)
        result = guard.inspect_binding(raw, guard.sha(raw), self.runtime)
        self.assertEqual(result['qualification'], 'inspection-only')
        self.assert_closed(result)

    def test_narrow_client_and_witness_validity_and_future_observation_stop_before_requests(self):
        cases = [('clientIdentity', 'validFrom', NOW + timedelta(minutes=1)),
                 ('clientIdentity', 'validUntil', NOW)]
        cases += [(name, field, value) for name in guard.WITNESSES
                  for field, value in (('observedAt', NOW + timedelta(minutes=1)), ('validUntil', NOW))]
        for name, field, value in cases:
            with self.subTest(name=name, field=field), guard.SyntheticOutputCustody() as custody:
                binding = complete_binding(custody)
                binding['enrollment'][name][field] = value.isoformat().replace('+00:00', 'Z')
                if name == 'clientIdentity':
                    rebind_client_witness_subjects(binding)
                # Prove the schema itself is valid: refusal must come from current validity.
                raw = guard._canonical(binding)
                inspected = guard.inspect_binding(raw, guard.sha(raw), self.runtime)
                self.assertEqual(inspected['qualification'], 'inspection-only')
                self.assertEqual(inspected['missingFields'], [])
                result, transport, delivery = self.run_fixture(binding=binding, custody=custody)
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(transport.requests, [])
                self.assertFalse(delivery._consumed)
                self.assertEqual(result['measurements'], [])

    def test_client_and_each_witness_expiry_inside_policy_stop_before_dependent_request(self):
        sanitize = adapter.sanitize
        for name in ('clientIdentity', *guard.WITNESSES):
            with self.subTest(name=name), guard.SyntheticOutputCustody() as custody:
                binding = complete_binding(custody)
                binding['enrollment'][name]['validUntil'] = (NOW + timedelta(seconds=5)).isoformat().replace('+00:00', 'Z')
                if name == 'clientIdentity':
                    rebind_client_witness_subjects(binding)
                clock = guard.SyntheticClock([NOW])
                def expire(*args):
                    output = sanitize(*args)
                    clock.samples = (NOW + timedelta(seconds=6),)
                    return output
                with patch.object(adapter, 'sanitize', side_effect=expire):
                    result, transport, _ = self.run_fixture(binding=binding, custody=custody, clock=clock)
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(len(transport.requests), 1)
                self.assertEqual(result['measurements'], [])
                self.assertNotIn('bindingSha256', result)
                failure = json.loads((custody.path / 'measurement-output/failure.json').read_bytes())
                self.assertEqual(failure['measurements'], [])
                self.assert_closed(failure)

    def test_request_deadline_covers_sanitizer_and_monotonic_clock_regression(self):
        monotonic = [1.0]
        sanitize = adapter.sanitize
        def slow(*args):
            value = sanitize(*args)
            monotonic[0] += 16
            return value
        with patch.object(guard.time, 'monotonic', side_effect=lambda: monotonic[0]), \
                patch.object(adapter, 'sanitize', side_effect=slow):
            result, transport, _ = self.run_fixture()
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(len(transport.requests), 1)
        self.assertEqual(result['measurements'], [])
        with guard.SyntheticOutputCustody() as custody, \
                patch.object(guard.time, 'monotonic', side_effect=[1.0, 0.0]):
            result, transport, _ = self.run_fixture(binding=complete_binding(custody), custody=custody)
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(transport.requests, [])

    def test_expiry_and_binding_drift_between_requests_stop_before_next_request(self):
        sanitize = adapter.sanitize
        for fault in ('expiry', 'runtime'):
            with self.subTest(fault=fault), guard.SyntheticOutputCustody() as custody:
                clock = guard.SyntheticClock([NOW])
                runtime = deepcopy(self.runtime)
                def drift(*args):
                    output = sanitize(*args)
                    if fault == 'expiry':
                        clock.samples = (guard._ceiling(),)
                    else:
                        runtime['tlsRequirements']['checkHostname'] = False
                    return output
                with patch.object(adapter, 'sanitize', side_effect=drift):
                    result, transport, _ = self.run_fixture(binding=complete_binding(custody),
                        custody=custody, clock=clock, runtime=runtime)
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(len(transport.requests), 1)
                self.assertEqual(result['measurements'], [])

    def test_only_exact_inert_transport_and_delivery_types_are_accepted(self):
        class Arbitrary:
            def request(self, *_):
                raise AssertionError('arbitrary transport invoked')
        for transport in (Arbitrary(), object(), lambda *_: None):
            result, _, delivery = self.run_fixture(transport=transport)
            self.assertEqual(result['qualification'], 'refused')
            self.assertFalse(delivery._consumed)
        result, transport, _ = self.run_fixture(delivery=object())
        self.assertEqual(result['qualification'], 'refused')
        self.assertEqual(transport.requests, [])

    def test_instance_transport_callback_is_ignored(self):
        calls = []
        def injected(*_):
            calls.append('request')
            raise AssertionError('caller callback invoked')
        transport = adapter.SyntheticTransport([fixture(request) for request in adapter.request_plan()])
        transport.request = injected
        result, _, _ = self.run_fixture(transport=transport)
        self.assertEqual(result['qualification'], 'pass')
        self.assertEqual(len(transport.requests), 23)
        self.assertEqual(calls, [])

    def test_clock_method_override_is_ignored_and_poison_storage_is_never_consulted(self):
        calls = []
        clock = guard.SyntheticClock([NOW])
        def injected():
            calls.append('sample')
            raise AssertionError('caller sample callback invoked')
        clock.sample = injected
        result, transport, _ = self.run_fixture(clock=clock)
        self.assertEqual(result['qualification'], 'pass')
        self.assertEqual(len(transport.requests), 23)
        class Poison:
            def __len__(self):
                calls.append('len')
                raise AssertionError('clock storage sized')
            def __getitem__(self, _):
                calls.append('getitem')
                raise AssertionError('clock storage indexed')
            def __lt__(self, _):
                calls.append('lt')
                raise AssertionError('clock index compared')
            def __ge__(self, _):
                calls.append('ge')
                raise AssertionError('clock index compared')
        for field, value in (('samples', Poison()), ('index', Poison()), ('index', True),
                             ('samples', ()), ('samples', (NOW,) * 257)):
            with self.subTest(field=field), guard.SyntheticOutputCustody() as custody:
                clock = guard.SyntheticClock([NOW])
                setattr(clock, field, value)
                result, transport, delivery = self.run_fixture(binding=complete_binding(custody),
                    custody=custody, clock=clock)
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(transport.requests, [])
                self.assertFalse(delivery._consumed)
                self.assertEqual(result['measurements'], [])
        self.assertEqual(calls, [])

    def test_poison_transport_storage_is_never_consulted(self):
        calls = []
        class Poison:
            def __bool__(self):
                calls.append('bool')
                raise AssertionError('storage consulted')
            def __len__(self):
                calls.append('len')
                raise AssertionError('storage consulted')
            def __iter__(self):
                calls.append('iter')
                raise AssertionError('storage consulted')
        for field in ('requests', 'responses'):
            with guard.SyntheticOutputCustody() as custody:
                transport = adapter.SyntheticTransport([])
                setattr(transport, field, Poison())
                result, _, delivery = self.run_fixture(binding=complete_binding(custody),
                    custody=custody, transport=transport)
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(result['syntheticRequests'], 0)
                self.assertFalse(delivery._consumed)
        self.assertEqual(calls, [])

    def test_mutated_response_chunks_cannot_introduce_iterators_or_remove_item_bound(self):
        class Poison:
            def __iter__(self):
                raise AssertionError('iterator consulted')
        for chunks in (Poison(), [b'x'], (b'x',) * 65):
            with guard.SyntheticOutputCustody() as custody:
                response = fixture(adapter.request_plan()[0])
                response.chunks = chunks
                result, transport, _ = self.run_fixture(binding=complete_binding(custody),
                    custody=custody, responses=[response])
                self.assertEqual(result['qualification'], 'refused')
                self.assertEqual(len(transport.requests), 1)
                self.assertEqual(result['measurements'], [])

    def test_final_retention_refuses_either_existing_file_and_unexpected_layout(self):
        for name in ('receipt.json', 'failure.json', 'unexpected.json'):
            with self.subTest(name=name), guard.SyntheticOutputCustody() as custody:
                custody.reserve(custody.binding())
                slot = custody.path / 'measurement-output'
                (slot / name).write_bytes(b'inert retained original')
                with self.assertRaises(guard.Refusal):
                    custody._retain(guard._receipt())
                self.assertEqual(os.listdir(slot), [name])
                self.assertEqual((slot / name).read_bytes(), b'inert retained original')
                with self.assertRaises(guard.Refusal):
                    custody._retain(guard._receipt())

    def test_malformed_unbounded_duplicate_or_wrong_digest_bindings_never_echo_inputs(self):
        for raw, expected in ((b'{"x":1,"x":2}', None), (b'{"x":0.1}', None),
                              (SECRET.encode(), None), (b'x' * 65537, None),
                              (b'{}', '0' * 64)):
            result = guard.inspect_binding(raw, guard.sha(raw) if expected is None else expected, self.runtime)
            self.assertEqual(result['qualification'], 'refused')
            self.assert_closed(result)

    def test_live_entrypoints_and_main_refuse_before_arguments_files_credentials_or_sockets(self):
        class Poison:
            def __str__(self):
                raise AssertionError('argument inspected')
            def __iter__(self):
                raise AssertionError('arguments inspected')
        output = io.StringIO()
        with patch.object(guard.os, 'open', side_effect=AssertionError('file access')), \
                patch.object(Path, 'read_bytes', side_effect=AssertionError('credential access')), \
                patch.object(socket, 'socket', side_effect=AssertionError('socket access')), \
                patch('sys.argv', Poison()), contextlib.redirect_stdout(output):
            for activate in (guard.enroll_live, guard.execute_live):
                with self.assertRaises(guard.Refusal):
                    activate(Poison(), config=Poison(), credential=Poison())
            self.assertEqual(guard.main(), 1)
        self.assertEqual(json.loads(output.getvalue())['apiCalls'], 0)
        self.assertEqual(json.loads(output.getvalue())['operationalGate'], 'closed')

    def test_direct_source_compilation_ignores_bytecode_and_refuses_unsafe_inputs(self):
        with tempfile.TemporaryDirectory(prefix='hexalith-source-synthetic-', dir='/tmp') as directory:
            root = Path(directory)
            source = root / 'fixture.py'
            source.write_bytes(b'VALUE = "checked-source"\n')
            (root / '__pycache__').mkdir(mode=0o700)
            (root / '__pycache__/fixture.pyc').write_bytes(b'poisoned-bytecode')
            self.assertEqual(guard.load_source(source, 'inert_source_fixture').VALUE, 'checked-source')
            link = root / 'link.py'
            link.symlink_to(source)
            fifo = root / 'fifo.py'
            os.mkfifo(fifo, 0o600)
            for path in (link, fifo):
                with self.assertRaises((ValueError, OSError)):
                    guard.read_source_bytes(path)
            os.link(source, root / 'hardlink.py')
            with self.assertRaises(ValueError):
                guard.read_source_bytes(source)


if __name__ == '__main__':
    unittest.main(verbosity=2)
