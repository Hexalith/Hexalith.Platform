"""Offline boundary tests using invented records, signatures and provider versions."""

from copy import deepcopy
from contextlib import redirect_stdout
from datetime import datetime, timedelta, timezone
import hashlib
import io
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import recovery_readiness as guard


NOW = datetime(2030, 1, 2, 12, tzinfo=timezone.utc)


class ReadinessTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.root.chmod(0o700)
        self.bindings_path = self.root / 'bindings.json'
        self.evidence_path = self.root / 'evidence.json'
        self.trust = {'principal': 'synthetic@example.invalid', 'namespace': 'hexalith-recovery',
                      'trustRootSha256': 'a' * 64, 'validatorIdentity': 'synthetic-reader',
                      'writerIdentity': 'synthetic-writer'}
        source = {'clusterUid': 'synthetic-source', 'etcdClusterId': 'source-cluster', 'memberId': 'source-member',
                  'nodeName': 'synthetic-node', 'nodeUid': 'synthetic-node-uid',
                  'endpoint': 'https://192.0.2.10:2379', 'dataDirectory': '/var/lib/etcd'}
        target = {'clusterId': 'isolated-cluster', 'memberId': 'isolated-member', 'etcdContainerId': 'container-etcd',
                  'nodeContainerId': 'container-node', 'network': 'none', 'readOnlyRoot': True, 'tmpfs': True,
                  'privileged': False, 'writableHostMounts': [], 'publishedPorts': [], 'sourceNetworkProbeDenied': True,
                  'clientEndpoint': 'http://127.0.0.1:2379', 'peerEndpoint': 'http://127.0.0.1:2380',
                  'dataDirectory': '/restore/etcd', 'readOnlyMounts': [], 'hostMounts': [],
                  'hostPid': False, 'hostIpc': False, 'devices': [], 'capabilities': [], 'noNewPrivileges': True,
                  'tmpfsFlags': ['nosuid', 'nodev', 'noexec', 'mode=0700'], 'inputDelivery': 'stdin', 'toolsDelivery': 'image-baked',
                  'tmpfsMiB': {'etcdRestore': 384, 'nodeRestore': 768, 'input': 512},
                  'extractionCapabilityQualified': True, 'userNamespaceMappingQualified': True}
        authority = {'bindingUid': 'unowned-native-binding', 'bindingName': 'synthetic-native', 'subject': 'synthetic-subject',
                     'ownerReferences': [], 'dependency': 'native-kubeadm',
                     **dict.fromkeys(('clientFingerprint', 'sshFingerprint', 'hostKeyFingerprint', 'breakGlassFingerprint'), 'SHA256:' + 'A' * 43),
                     'lineage': {'dependency': 'independent-native', 'currentHeadSha256': '1'*64, 'completeChainSha256': '2'*64,
                                 'postCutRevocationsSha256': '3'*64, 'sequenceComplete': True, 'postCutRevocationsApplied': True,
                                 'cutAt': '2030-01-02T10:00:00Z', 'observedAt': '2030-01-02T11:09:00Z'},
                     'roleRef': {'apiGroup': 'rbac.authorization.k8s.io', 'kind': 'ClusterRole', 'name': 'cluster-admin'}}
        tool = self.root / 'synthetic-tool'
        tool.write_bytes(b'This is never executed.\n')
        tool.chmod(0o600)
        reference = {'path': str(tool), 'sha256': guard.digest(tool.read_bytes())}
        self.b = {'schemaVersion': 1, 'kind': 'hexalith.offline-recovery-bindings', 'attemptId': 'synthetic-attempt',
                  'custodyDirectory': str(self.root), 'forbiddenRoots': ['/synthetic-forbidden'],
                  'lineage': {**reference, 'attemptId': '20261015t112841z-post-40-recovery-prerequisite'},
                  'procedure': reference, 'tools': [{**reference, 'role': role} for role in sorted(guard.LOCAL_TOOL_ROLES)],
                  'runtimeTools': {role: {'path': '/synthetic/' + role, 'sha256': 'a'*64, 'measurement': 'declared-observation'}
                                   for role in guard.RUNTIME_TOOL_ROLES}, 'maximumAgeSeconds': 86400,
                  'guard': {'path': str(Path(guard.__file__).resolve()),
                            'sha256': guard.digest(Path(guard.__file__).read_bytes())},
                  'priorRecovery': {'recoveryId': 'synthetic-workload', 'preparedAt': '2030-01-02T11:00:00Z',
                                    'expiresAt': '2030-01-03T11:00:00Z', 'maximumAgeSeconds': 86400},
                  'unmeasured': [], 'uncoveredActions': [], 'source': source, 'target': target, 'authority': authority,
                  'retainedKubeSphereAuthorityUids': ['ks-owned-binding'], 'trust': self.trust}
        comparison = {'revision': 123, 'keyCount': 42, 'keyHash': 'b' * 64,
                      'canarySha256': 'c' * 64, 'configurationSha256': 'd' * 64}
        payload = {**self.object_metadata('2030-01-02T11:01:00Z', '2030-01-02T11:02:00Z', '2030-01-02T11:03:00Z'),
                   'sha256': 'e' * 64, 'readBackSha256': 'e' * 64, 'bytes': 100, 'readBackBytes': 100,
                   'versionId': 'synthetic-version', 'readBy': self.trust['validatorIdentity'],
                   'decryptedPlaintextSha256': 'f' * 64}
        self.records = {
            'workload': {'recoveryId': 'synthetic-workload'},
            'source': {**comparison, 'identity': source, 'healthy': True, 'alarms': [], 'versionsMatch': True},
            'point': {**comparison, 'snapshotSha256': 'f' * 64, 'integrityVerified': True,
                      'encrypted': True, 'payloadReadback': deepcopy(payload)},
            'restore': {**comparison, 'snapshotSha256': 'f' * 64, 'target': target,
                        'startedAt': '2030-01-02T11:04:00Z', 'finishedAt': '2030-01-02T11:06:00Z',
                        'memberHealthy': True, 'revisionBump': False},
            'node': {'configurationSha256': 'd' * 64, 'archiveSha256': 'f' * 64, 'payloadReadback': deepcopy(payload),
                     'archiveCapturedAt': '2030-01-02T11:01:00Z', 'startedAt': '2030-01-02T11:04:00Z', 'finishedAt': '2030-01-02T11:08:00Z',
                     **dict.fromkeys(('encrypted', 'ownerModesPreserved', 'fileInventoryMatches',
                                      'certificateKeyPairsMatch', 'recoveredFromValidatorCopy'), True)},
            'native': {'authority': authority, 'sourceClusterUid': source['clusterUid'], 'recoveredSubject': authority['subject'],
                       'nativeBinding': {k: authority[k] for k in ('bindingUid', 'bindingName', 'subject', 'roleRef', 'ownerReferences')},
                       **dict.fromkeys(('recoveredPrivateApi', 'recoveredSsh', 'breakGlass',
                                                               'independentCustodyReadback', 'noNewGrant'), True)},
            'cleanup': {'target': target, **dict.fromkeys(('exactTargetsAbsent', 'tmpfsAbsent', 'plaintextAbsent',
                                                          'temporaryCredentialsAbsent', 'sourceUnchanged'), True)}}
        for name, minute in [('workload', '00'), ('source', '00'), ('point', '01'), ('restore', '06'),
                             ('node', '08'), ('native', '10'), ('cleanup', '12')]:
            self.records[name]['capturedAt'] = '2030-01-02T11:' + minute + ':00Z'
        self.approval_changes = {}
        self.scope_changes = {}
        self.b['storage'] = {'endpoint': 'https://storage.example.invalid', 'bucket': 'synthetic-bucket',
                             'region': 'synthetic-region', 'attemptPrefix': 'evidence/4-1/synthetic-attempt/'}
        self.b['window'] = {'start': '2030-01-02T10:00:00Z', 'exclusiveEnd': '2030-01-03T12:00:00Z'}
        self.b['privatePaths'] = {'sourceTemporary': '/run/synthetic-attempt-source',
                                 'sourceSnapshot': '/run/synthetic-attempt-source/snapshot.db',
                                 'localPlaintextTmpfs': '/dev/shm/synthetic-attempt'}
        self.b['ownedChildProcessIds'] = [1234, 1235]
        source.update(currentObservationAccepted=True, apiCaFingerprint='SHA256:' + 'B' * 43,
                      capturedAt='2030-01-02T10:59:00Z', expiresAt='2030-01-03T10:59:00Z',
                      apiEndpoint='https://192.0.2.10:6443', peerEndpoint='https://192.0.2.10:2380',
                      serverVersion='v1.34.9', historicalServerVersion='v1.34.9',
                      etcdVersion='3.6.5', historicalEtcdVersion='3.6.5')
        target.update(addedCapabilities=[], droppedCapabilities=['ALL'], imageQualified=True,
                      engineQualified=True, capacityQualified=True, imageDigest='sha256:' + '1' * 64,
                      currentImageId='sha256:' + '2' * 64, engineFingerprint='SHA256:' + 'C' * 43,
                      engineContext='synthetic-context', daemonId='synthetic-daemon',
                      availableMemoryBytes=4 * 1024 * 1024 * 1024,
                      capturedAt='2030-01-02T10:58:00Z', expiresAt='2030-01-03T10:58:00Z',
                      etcdContainerName='synthetic-etcd', nodeContainerName='synthetic-node')
        target['nodeIsolation'] = {k: deepcopy(target[k]) for k in guard.ISOLATION_FIELDS}
        for name, key in (('point', 'external-etcd-snapshot.db.age'), ('node', 'node-recovery.tar.gz.age')):
            item = self.records[name]['payloadReadback']
            self.records[name]['payloadReadback'] = self.storage_object(key, item['sha256'], item['bytes'], item)
            self.records[name]['immutableOffNodeVersion'] = item['versionId']
        authority.update(currentLineageAccepted=True, encryptedNativeSha256='4' * 64, encryptedNativeBytes=99)
        authority['encryptedNativeCustody'] = {**self.storage_object('independent-native-credential.age', '4' * 64, 99,
            self.object_metadata('2030-01-02T11:01:00Z', '2030-01-02T11:02:00Z', '2030-01-02T11:03:00Z')),
            'decryptedCredentialFingerprint': authority['clientFingerprint'], 'independentCustodyReadback': True}
        self.records['cleanup'].update(
            ownedResources={**self.b['privatePaths'], 'etcdContainerId': target['etcdContainerId'],
                            'nodeContainerId': target['nodeContainerId'], 'childProcessIds': self.b['ownedChildProcessIds']},
            absentChildProcessIds=self.b['ownedChildProcessIds'],
            absentMounts={target[k]: ['/input', '/restore'] for k in ('etcdContainerId', 'nodeContainerId')},
            **dict.fromkeys(('sourceTemporaryAbsent', 'sourceSnapshotAbsent', 'childrenAbsent',
                             'mountsAbsent', 'associatedVolumesAbsent', 'listenersAbsent'), True))

    def object_metadata(self, created='2030-01-02T11:30:00Z', published='2030-01-02T11:30:00Z', read='2030-01-02T11:31:00Z'):
        until = (datetime.fromisoformat(created.replace('Z', '+00:00')) + timedelta(days=30)).isoformat()
        return {'createdAt': created, 'publishedAt': published, 'readAt': read, 'retainUntil': until,
                'encryption': 'AES256', 'lockMode': 'COMPLIANCE', 'versionId': 'synthetic-version',
                'objectLockRetention': {'mode': 'COMPLIANCE', 'retainUntil': until},
                'retentionApi': {'mode': 'COMPLIANCE', 'retainUntil': until}}

    def write(self, name, value):
        data = value if isinstance(value, bytes) else (json.dumps(value, sort_keys=True) + '\n').encode()
        path = self.root / name
        path.write_bytes(data)
        path.chmod(0o600)
        return {'file': name, 'sha256': guard.digest(data)}

    def storage_object(self, key, sha256, size, metadata=None):
        item = {**(metadata or self.object_metadata()), **self.b['storage']}
        return {**item, 'objectKey': self.b['storage']['attemptPrefix'] + key,
                'readBackVersionId': item['versionId'], 'sha256': sha256, 'readBackSha256': sha256,
                'bytes': size, 'readBackBytes': size, 'uploadedBy': self.trust['writerIdentity'],
                'readBy': self.trust['validatorIdentity'], 'readBackCreatedAt': item['createdAt'],
                'readBackEncryption': item['encryption'], 'readBackLockMode': item['lockMode'],
                'readBackRetention': deepcopy(item['retentionApi'])}

    def seal_record(self, name, fields):
        common = {'attemptId': self.b['attemptId'], 'capturedAt': '2030-01-02T11:00:00Z',
                  'expiresAt': '2030-01-03T11:00:00Z', 'verificationResult': 'pass'}
        record = self.write(name + '.json', {**common, **fields})
        signature = self.write(name + '.json.sig', b'-----BEGIN SSH SIGNATURE-----\nSYNTHETIC-NOT-A-SIGNATURE\n')
        verification = self.write(name + '-verification.json', {'method': 'ssh-keygen-Y-verify', 'exitCode': 0,
                                  'recordSha256': record['sha256'], 'signatureSha256': signature['sha256'], **self.trust})
        metadata = self.object_metadata() if name != 'approval' else self.object_metadata('2030-01-02T11:41:00Z', '2030-01-02T11:41:00Z', '2030-01-02T11:42:00Z')
        if name == 'scope-approval':
            metadata = self.object_metadata('2030-01-02T10:31:00Z', '2030-01-02T10:31:00Z', '2030-01-02T10:32:00Z')
        record_object = self.storage_object(name + '.json', record['sha256'], (self.root / (name + '.json')).stat().st_size, metadata)
        signature_object = self.storage_object(name + '.json.sig', signature['sha256'], (self.root / (name + '.json.sig')).stat().st_size,
                                              {**metadata, 'versionId': 'synthetic-signature'})
        readback = self.write(name + '-readback.json', {**common, **record_object, 'recordSha256': record['sha256'],
                              'capturedAt': metadata['readAt'],
                              'expiresAt': (datetime.fromisoformat(metadata['readAt'].replace('Z', '+00:00')) + timedelta(days=1)).isoformat(),
                              'signatureSha256': signature['sha256'], 'credentialIdentity': self.trust['validatorIdentity'],
                              'encryption': 'AES256', 'lockMode': 'COMPLIANCE',
                              'signatureVersionId': 'synthetic-signature', 'bytesMatch': True,
                              'retentionApiMatches': True,
                              'signatureObject': signature_object})
        return dict(record=record, signature=signature, verification=verification, readback=readback)

    def packet(self):
        plan = self.write('execution-plan.json', guard.execution_plan(self.b))
        self.b['executionPlan'] = {'path': str(self.root / 'execution-plan.json'), 'sha256': plan['sha256']}
        self.write('bindings.json', self.b)
        self.binding_hash = guard.digest(self.bindings_path.read_bytes())
        refs = {}
        hashes = {}
        for name in guard.RECORDS:
            record = deepcopy(self.records[name])
            if name in ('restore', 'node'):
                record['recoveryPointSha256'] = hashes['point']
            if name in ('native', 'cleanup'):
                record['nodeBundleSha256'] = hashes['node']
            if name == 'cleanup':
                record['restoreSha256'] = hashes['restore']
            refs[name] = self.seal_record(name, record)
            hashes[name] = refs[name]['record']['sha256']
        scope = self.seal_record('scope-approval', {
            'kind': 'hexalith.recovery-execution-scope-approval', 'approvedBy': self.trust['principal'],
            'capturedAt': '2030-01-02T10:30:00Z', 'expiresAt': '2030-01-03T10:30:00Z',
            'executionPlanSha256': plan['sha256'], 'procedureSha256': self.b['procedure']['sha256'],
            'toolInputs': self.b['tools'], 'scopes': list(guard.SCOPES),
            'windowStart': self.b['window']['start'], 'windowEnd': self.b['window']['exclusiveEnd'], **self.scope_changes})
        approval = {'approvedBy': self.trust['principal'], 'bindingsSha256': self.binding_hash,
                    'kind': 'hexalith.recovery-final-acceptance', 'scopeApprovalSha256': scope['record']['sha256'],
                    'capturedAt': '2030-01-02T11:40:00Z',
                    'procedureSha256': self.b['procedure']['sha256'], 'toolInputs': self.b['tools'],
                    'recordInputs': hashes, 'scopes': list(guard.SCOPES),
                    'windowStart': '2030-01-02T10:00:00Z', 'windowEnd': '2030-01-03T12:00:00Z', **self.approval_changes}
        self.e = {'attemptId': self.b['attemptId'], 'records': refs, 'scopeApproval': scope,
                  'approval': self.seal_record('approval', approval)}
        self.write('evidence.json', self.e)

    def check(self):
        return guard.evaluate(self.bindings_path, self.binding_hash, self.evidence_path, now=NOW)

    def test_synthetic_consistent_baseline_never_authenticates_or_opens_a_gate(self):
        self.packet()
        result = self.check()
        self.assertTrue(result['metadataConsistent'], result)
        self.assertFalse(result['cryptographicVerificationPerformed'])
        self.assertTrue(result['claimsAreUntrustedMetadata'])
        self.assertFalse(result['mutationAuthorized'])
        self.assertEqual(result['operationalGate'], 'closed')

    def test_expired_future_unknown_and_unchanged_earliest_policy_deadline(self):
        for change in ({'preparedAt': '2030-01-01T11:00:00Z', 'expiresAt': '2030-02-01T11:00:00Z'},
                       {'preparedAt': '2030-01-03T11:00:00Z'}, {'preparedAt': None}, {'maximumAgeSeconds': True}):
            with self.subTest(change=change):
                original = deepcopy(self.b['priorRecovery'])
                self.b['priorRecovery'].update(change)
                self.packet()
                self.assertFalse(self.check()['metadataConsistent'])
                self.b['priorRecovery'] = original

    def test_invented_passing_flags_cannot_replace_signature_verification_or_readback(self):
        for field in ('signature', 'verification', 'readback'):
            with self.subTest(field=field):
                self.packet()
                self.e['records']['point'].pop(field)
                self.e['records']['point'].update(signed=True, verified=True)
                self.write('evidence.json', self.e)
                self.assertFalse(self.check()['metadataConsistent'])

    def test_missing_approval_exact_bindings_and_claimed_verification_fail(self):
        for key in ('approval', 'records'):
            self.packet()
            self.e.pop(key)
            self.write('evidence.json', self.e)
            self.assertFalse(self.check()['metadataConsistent'])
        self.packet()
        data = json.loads((self.root / 'approval.json').read_text())
        data['bindingsSha256'] = '0' * 64
        self.e['approval'] = self.seal_record('approval', data)
        self.write('evidence.json', self.e)
        self.assertFalse(self.check()['metadataConsistent'])

    def test_individual_record_and_approval_expiry_future_and_unknown_refuse(self):
        for name in ('point', 'approval'):
            for field, value in (('expiresAt', '2030-01-02T11:59:59Z'),
                                 ('capturedAt', '2030-01-02T12:00:01Z'), ('expiresAt', None)):
                with self.subTest(name=name, field=field):
                    if name == 'approval':
                        self.approval_changes = {field: value}
                    else:
                        original = deepcopy(self.records[name])
                        self.records[name][field] = value
                    self.packet()  # Rebuild every downstream digest and acceptance binding.
                    result = self.check()
                    self.assertFalse(result['metadataConsistent'])
                    self.assertIn(result['blockers'][0], ('expired, future or excessive prerequisite age', 'unknown timestamp'))
                    self.approval_changes = {}
                    if name != 'approval':
                        self.records[name] = original

    def test_nonapproval_freshness_mutation_is_detected(self):
        original = guard.fresh
        def skip_nonapproval(record, now, maximum):
            if 'credentialIdentity' in record or 'approvedBy' in record:
                original(record, now, maximum)
        result = unittest.TestResult()
        with patch.object(guard, 'fresh', side_effect=skip_nonapproval):
            ReadinessTests('test_individual_record_and_approval_expiry_future_and_unknown_refuse').run(result)
        self.assertGreater(len(result.failures), 0, 'nonapproval freshness mutation survived')

    def test_custody_symlink_escape_writable_input_and_tool_hash_drift(self):
        for failure in ('symlink', 'mode', 'tool', 'reference-escape', 'bindings'):
            with self.subTest(failure=failure):
                self.packet()
                path = self.root / 'point.json'
                if failure == 'symlink':
                    old = path.read_bytes()
                    path.unlink()
                    (self.root / 'elsewhere').write_bytes(old)
                    path.symlink_to(self.root / 'elsewhere')
                elif failure == 'mode':
                    path.chmod(0o644)
                elif failure == 'tool':
                    (self.root / 'synthetic-tool').write_bytes(b'changed tool')
                elif failure == 'reference-escape':
                    self.e['records']['point']['record']['file'] = '../point.json'
                    self.write('evidence.json', self.e)
                else:
                    self.binding_hash = '0' * 64
                self.assertFalse(self.check()['metadataConsistent'])
                if path.is_symlink():
                    path.unlink()
                (self.root / 'synthetic-tool').write_bytes(b'This is never executed.\n')

    def test_collision_fencing_authority_and_cleanup_safety_boundaries(self):
        changes = [('target', 'clusterId', 'source-cluster'), ('target', 'memberId', 'source-member'),
                   ('target', 'network', 'host'), ('target', 'publishedPorts', ['2379']),
                   ('target', 'sourceNetworkProbeDenied', False), ('target', 'writableHostMounts', ['/var/lib/etcd']),
                   ('target', 'readOnlyMounts', ['/host/input']), ('target', 'hostMounts', ['/host/data']),
                   ('target', 'hostPid', True), ('target', 'hostIpc', True), ('target', 'devices', ['/dev/sda']),
                   ('target', 'capabilities', ['SYS_ADMIN']), ('target', 'noNewPrivileges', False),
                   ('target', 'tmpfsFlags', ['exec']), ('target', 'tmpfsMiB', {}),
                   ('target', 'extractionCapabilityQualified', False), ('target', 'userNamespaceMappingQualified', None),
                   ('target', 'etcdContainerId', None), ('target', 'nodeContainerId', 'container-etcd'),
                   ('authority', 'dependency', 'KubeSphere'),
                   ('authority', 'bindingUid', 'ks-owned-binding'), ('authority', 'ownerReferences', ['manager'])]
        for section, key, value in changes:
            with self.subTest(section=section, key=key):
                original = deepcopy(self.b[section][key])
                self.b[section][key] = value
                self.packet()
                self.assertFalse(self.check()['metadataConsistent'])
                self.b[section][key] = original
        for name, key, value in [('cleanup', 'exactTargetsAbsent', False), ('native', 'recoveredSsh', False),
                                 ('restore', 'revision', 124), ('point', 'integrityVerified', False),
                                 ('node', 'ownerModesPreserved', False), ('source', 'healthy', False)]:
            with self.subTest(record=name, key=key):
                original = deepcopy(self.records[name][key])
                self.records[name][key] = value
                self.packet()
                self.assertFalse(self.check()['metadataConsistent'])
                self.records[name][key] = original

    def test_payload_readback_mismatch_and_uncovered_actions_refuse(self):
        for key, value in [('readBackSha256', '0' * 64), ('readBy', 'synthetic-writer'),
                           ('readBackBytes', 99), ('decryptedPlaintextSha256', '0' * 64)]:
            with self.subTest(key=key):
                original = deepcopy(self.records['point']['payloadReadback'])
                self.records['point']['payloadReadback'][key] = value
                self.packet()
                self.assertFalse(self.check()['metadataConsistent'])
                self.records['point']['payloadReadback'] = original
        self.b['uncoveredActions'] = ['new IAM grant']
        self.packet()
        self.assertFalse(self.check()['metadataConsistent'])

    def test_unknown_authority_identity_and_missing_exact_tool_inventory_refuse(self):
        for identity in (None, '', '   ', 17, False, []):
            with self.subTest(bindingUid=identity):
                self.b['authority']['bindingUid'] = identity
                self.packet()
                result = self.check()
                self.assertFalse(result['metadataConsistent'], result)
                self.assertEqual(result['operationalGate'], 'closed')
        self.b['authority']['bindingUid'] = 'unowned-native-binding'
        for tools in ([], None, {}, [None], [{'path': '/unknown-tool'}],
                      [{'path': self.b['procedure']['path'], 'sha256': 'not-a-digest'}]):
            with self.subTest(tools=tools):
                self.b['tools'] = tools
                self.packet()
                result = self.check()
                self.assertFalse(result['metadataConsistent'], result)
                self.assertFalse(result['mutationAuthorized'])

    def test_zero_mount_baseline_rejects_every_declared_host_privilege(self):
        self.packet()
        self.assertEqual(self.b['target']['hostMounts'], [])
        self.assertEqual(self.b['target']['readOnlyMounts'], [])
        self.assertTrue(self.check()['metadataConsistent'])
        for key, value in [('inputDelivery', 'bind-mount'), ('toolsDelivery', 'tmpfs-copy'), ('readOnlyRoot', False)]:
            original = self.b['target'][key]
            self.b['target'][key] = value
            self.packet()
            self.assertEqual(self.check()['blockers'], ['insufficient isolated fencing'])
            self.b['target'][key] = original

    def test_archive_plaintext_digest_and_thirty_day_retention_are_substantive(self):
        original = deepcopy(self.records['node'])
        self.records['node']['archiveSha256'] = None
        self.records['node']['payloadReadback']['decryptedPlaintextSha256'] = None
        self.packet()
        self.assertEqual(self.check()['blockers'], ['snapshot or archive readback mismatch'])
        self.records['node'] = original
        for name in ('point', 'node'):
            original = deepcopy(self.records[name]['payloadReadback'])
            payload = self.records[name]['payloadReadback']
            until = (NOW + timedelta(seconds=1)).isoformat()
            payload['retainUntil'] = until
            payload['objectLockRetention']['retainUntil'] = until
            payload['retentionApi']['retainUntil'] = until
            self.packet()
            self.assertEqual(self.check()['blockers'], ['immutable publication chronology or retention interval invalid'])
            self.records[name]['payloadReadback'] = original
        self.packet()
        self.assertLess(guard.timestamp(original['retainUntil']) - NOW, timedelta(days=30))
        self.assertTrue(self.check()['metadataConsistent'])  # Duration from creation, not thirty days remaining.

    def test_payload_and_record_retention_apis_must_agree_with_complete_times(self):
        for key, value in [('retentionApi', {'mode': 'GOVERNANCE', 'retainUntil': '2030-02-01T11:01:00Z'}),
                           ('objectLockRetention', {}), ('createdAt', None), ('publishedAt', None), ('readAt', None)]:
            original = deepcopy(self.records['point']['payloadReadback'])
            self.records['point']['payloadReadback'][key] = value
            self.packet()
            self.assertFalse(self.check()['metadataConsistent'])
            self.records['point']['payloadReadback'] = original
        for name in ('point', 'approval'):
            self.packet()
            path = self.root / (name + '-readback.json')
            rb = json.loads(path.read_text())
            rb['retentionApi'] = {}
            ref = self.write(path.name, rb)
            (self.e['records'][name] if name != 'approval' else self.e['approval'])['readback'] = ref
            self.write('evidence.json', self.e)
            self.assertEqual(self.check()['blockers'], ['immutable retention API mismatch'])

    def test_nonstring_identities_provider_names_fingerprints_and_lineage_refuse(self):
        for section, fields in [('source', ('clusterUid', 'etcdClusterId', 'memberId', 'nodeName', 'nodeUid')),
                                ('target', ('clusterId', 'memberId', 'etcdContainerId', 'nodeContainerId'))]:
            for key in fields:
                for bad in (17, True, ['truthy'], {}, ' ', None):
                    original = self.b[section][key]
                    self.b[section][key] = bad
                    self.packet()
                    self.assertFalse(self.check()['metadataConsistent'], (section, key, bad))
                    self.b[section][key] = original
        for key in ('validatorIdentity', 'writerIdentity'):
            original = self.trust[key]
            for bad in ('', ' ', True):
                self.trust[key] = bad
                self.packet()
                self.assertEqual(self.check()['blockers'], ['unknown or dependent trust'])
            self.trust[key] = original
        for key in ('clientFingerprint', 'sshFingerprint', 'hostKeyFingerprint', 'breakGlassFingerprint'):
            original = self.b['authority'][key]
            for bad in (17, 'unknown', 'SHA256:short', None):
                self.b['authority'][key] = bad
                self.packet()
                self.assertEqual(self.check()['blockers'], ['wrong recovered authority or dependency'])
            self.b['authority'][key] = original
        for key, bad in [('recoveredSubject', 'other-subject'), ('nativeBinding', {}), ('sourceClusterUid', 'other-cluster')]:
            original = self.records['native'][key]
            self.records['native'][key] = bad
            self.packet()
            self.assertEqual(self.check()['blockers'], ['wrong recovered authority or dependency'])
            self.records['native'][key] = original
        for key, bad in [('dependency', 'KubeSphere'), ('sequenceComplete', False), ('postCutRevocationsApplied', False),
                         ('completeChainSha256', None)]:
            original = self.b['authority']['lineage'][key]
            self.b['authority']['lineage'][key] = bad
            self.packet()
            self.assertEqual(self.check()['blockers'], ['wrong recovered authority or dependency'])
            self.b['authority']['lineage'][key] = original

    def test_event_dependencies_and_complete_tool_roles_refuse_coherent_wrong_inputs(self):
        for name, key, bad in [('cleanup', 'capturedAt', '2030-01-02T11:09:00Z'),
                               ('source', 'capturedAt', '2030-01-02T11:02:00Z'),
                               ('restore', 'startedAt', '2030-01-02T11:02:00Z'),
                               ('node', 'finishedAt', '2030-01-02T11:11:00Z')]:
            original = self.records[name][key]
            self.records[name][key] = bad
            self.packet()
            self.assertEqual(self.check()['blockers'], ['recovery dependency chronology invalid'])
            self.records[name][key] = original
        self.approval_changes = {'capturedAt': '2030-01-02T11:30:00Z'}
        self.packet()
        self.assertEqual(self.check()['blockers'], ['recovery dependency chronology invalid'])
        self.approval_changes = {}
        for role in sorted(guard.LOCAL_TOOL_ROLES):
            original = self.b['tools']
            self.b['tools'] = [t for t in original if t['role'] != role]
            self.packet()
            self.assertEqual(self.check()['blockers'], ['missing exact tool bindings'])
            self.b['tools'] = original
        for role in sorted(guard.RUNTIME_TOOL_ROLES):
            tool = self.b['runtimeTools'].pop(role)
            self.packet()
            self.assertEqual(self.check()['blockers'], ['runtime tool role inventory incomplete or unmeasured'])
            self.b['runtimeTools'][role] = tool
        self.packet()
        rb = json.loads((self.root / 'approval-readback.json').read_text())
        rb['readAt'] = '2030-01-02T11:40:00Z'  # Before its own publication, with all references rebound.
        self.e['approval']['readback'] = self.write('approval-readback.json', rb)
        self.write('evidence.json', self.e)
        self.assertEqual(self.check()['blockers'], ['immutable publication chronology or retention interval invalid'])

    def test_private_parse_values_and_invalid_attempt_ids_never_reach_output(self):
        marker = 'SYNTHETIC-PRIVATE-MARKER'
        for field, value in (('preparedAt', marker), ('expiresAt', marker),
                             ('preparedAt', '9999-12-31T23:59:59-01:00')):
            with self.subTest(field=field, value=value):
                original = deepcopy(self.b['priorRecovery'])
                self.b['priorRecovery'][field] = value
                self.packet()
                result = self.check()
                self.assertFalse(result['metadataConsistent'], result)
                self.assertNotIn(value, json.dumps(result))
                self.assertNotIn(str(self.root), json.dumps(result))
                self.b['priorRecovery'] = original
        for attempt in (marker, '../private-marker', '', None, ['private-marker'], 'a' * 81):
            with self.subTest(attempt=attempt):
                self.b['attemptId'] = attempt
                self.packet()
                result = self.check()
                self.assertFalse(result['metadataConsistent'], result)
                self.assertNotIn('attemptId', result)
                self.assertNotIn(marker, json.dumps(result))

    def test_malformed_reference_and_record_objects_refuse_through_public_cli(self):
        for field in ('record', 'signature', 'verification', 'readback'):
            for malformed in (None, [], 'SYNTHETIC-PRIVATE-MARKER'):
                with self.subTest(field=field, malformed=malformed):
                    self.packet()
                    self.e['records']['point'][field] = malformed
                    self.write('evidence.json', self.e)
                    output = io.StringIO()
                    with patch.object(guard, 'datetime') as clock, \
                         patch.object(sys, 'argv', ['recovery_readiness.py', '--bindings', str(self.bindings_path),
                                      '--bindings-sha256', self.binding_hash, '--evidence', str(self.evidence_path)]), \
                         redirect_stdout(output):
                        clock.now.return_value = NOW
                        clock.fromisoformat.side_effect = datetime.fromisoformat
                        self.assertEqual(guard.main(), 1)
                    result = json.loads(output.getvalue())
                    self.assertFalse(result['metadataConsistent'])
                    self.assertEqual(result['operationalGate'], 'closed')
                    self.assertNotIn('SYNTHETIC-PRIVATE-MARKER', output.getvalue())
                    self.assertNotIn(str(self.root), output.getvalue())
        for field in ('record', 'verification', 'readback'):
            self.packet()
            self.e['records']['point'][field] = self.write('malformed.json', [])
            self.write('evidence.json', self.e)
            result = self.check()
            self.assertFalse(result['metadataConsistent'], result)
            self.assertTrue(result['blockers'])
            self.assertNotIn(str(self.root), json.dumps(result))

    def test_all_paths_have_zero_command_network_credential_and_write_side_effects(self):
        self.packet()
        def inventory():
            return {p.name: (guard.digest(p.read_bytes()), p.stat().st_mode) for p in self.root.iterdir() if p.is_file()}
        before = inventory()
        with patch.object(subprocess, 'run', side_effect=AssertionError('command dispatch')), \
             patch.object(subprocess, 'Popen', side_effect=AssertionError('process dispatch')), \
             patch.object(socket, 'socket', side_effect=AssertionError('network dispatch')), \
             patch.object(os, 'system', side_effect=AssertionError('shell dispatch')), \
             patch.object(Path, 'write_bytes', side_effect=AssertionError('file write')), \
             patch.dict(os.environ, {'KUBECONFIG': '/must-not-read', 'SSH_AUTH_SOCK': '/must-not-use'}):
            self.assertTrue(self.check()['metadataConsistent'])
            self.assertFalse(guard.evaluate(self.bindings_path, '0' * 64, self.evidence_path, now=NOW)['metadataConsistent'])
        self.assertEqual(before, inventory())

    def test_both_targets_require_zero_mounts_and_complete_confinement(self):
        unsafe = [('hostMounts', ['/var/lib/etcd']), ('readOnlyMounts', [{'source': '/input'}]),
                  ('writableHostMounts', ['/tmp']), ('hostPid', True), ('hostIpc', True),
                  ('devices', ['/dev/mem']), ('capabilities', ['SYS_ADMIN']), ('addedCapabilities', ['CHOWN']),
                  ('droppedCapabilities', []), ('noNewPrivileges', False), ('network', 'host')]
        for section in ('target', 'nodeIsolation'):
            for field, value in unsafe:
                with self.subTest(section=section, field=field):
                    target = self.b['target'] if section == 'target' else self.b['target']['nodeIsolation']
                    original = deepcopy(target[field])
                    target[field] = value
                    self.packet()
                    self.assertFalse(self.check()['metadataConsistent'])
                    target[field] = original

    def test_historical_unknown_authority_and_target_qualification_cannot_pass(self):
        failures = [('source', 'currentObservationAccepted', False), ('source', 'currentObservationAccepted', None),
                    ('source', 'nodeUid', None), ('source', 'apiCaFingerprint', 3),
                    ('source', 'apiEndpoint', None), ('source', 'serverVersion', None), ('source', 'etcdVersion', 'drift'),
                    ('source', 'capturedAt', '2029-01-02T10:00:00Z'),
                    ('authority', 'currentLineageAccepted', False), ('authority', 'clientFingerprint', 7),
                    ('authority', 'encryptedNativeCustody', None), ('authority', 'encryptedNativeSha256', None),
                    ('target', 'imageQualified', False), ('target', 'engineQualified', None),
                    ('target', 'capacityQualified', False), ('target', 'imageDigest', None),
                    ('target', 'currentImageId', 'unknown'), ('target', 'engineFingerprint', False),
                    ('target', 'daemonId', ''), ('target', 'availableMemoryBytes', 0)]
        for section, field, value in failures:
            with self.subTest(section=section, field=field, value=value):
                original = deepcopy(self.b[section][field])
                self.b[section][field] = value
                self.packet()
                self.assertFalse(self.check()['metadataConsistent'])
                self.b[section][field] = original

    def test_separate_scope_plan_and_retained_window_precede_capture(self):
        for change in ({'executionPlanSha256': '0' * 64}, {'approvedBy': 'someone-else'},
                       {'kind': 'hexalith.recovery-final-acceptance'}, {'windowStart': '2030-01-02T09:00:00Z'}):
            with self.subTest(change=change):
                self.scope_changes = change
                self.packet()
                self.assertFalse(self.check()['metadataConsistent'])
        self.scope_changes = {}
        self.packet()
        self.e.pop('scopeApproval')
        self.write('evidence.json', self.e)
        self.assertFalse(self.check()['metadataConsistent'])
        self.packet()
        readback = json.loads((self.root / 'scope-approval-readback.json').read_text())
        late = self.object_metadata('2030-01-02T11:01:00Z', '2030-01-02T11:01:00Z', '2030-01-02T11:02:00Z')
        for item in (readback, readback['signatureObject']):
            item.update(late)
            item['readBackCreatedAt'] = item['createdAt']
            item['readBackRetention'] = deepcopy(item['retentionApi'])
        readback['signatureObject']['versionId'] = readback['signatureVersionId']
        readback['signatureObject']['readBackVersionId'] = readback['signatureVersionId']
        self.e['scopeApproval']['readback'] = self.write('scope-approval-readback.json', readback)
        self.write('evidence.json', self.e)
        result = self.check()
        self.assertFalse(result['metadataConsistent'])
        self.assertEqual(result['blockers'], ['scope approval was not published and read before capture'])
        for change in ({'start': '2030-01-02T12:01:00Z'}, {'exclusiveEnd': '2030-01-02T11:59:59Z'},
                       {'start': '2030-01-02T11:01:00Z'}, {'exclusiveEnd': '2030-01-02T12:30:00Z'}):
            with self.subTest(window=change):
                original = deepcopy(self.b['window'])
                self.b['window'].update(change)
                self.packet()
                self.assertFalse(self.check()['metadataConsistent'])
                self.b['window'] = original

    def test_full_envelope_failures_reach_verification_and_independent_readback_checks(self):
        self.packet()
        verification = json.loads((self.root / 'point-verification.json').read_text())
        verification['exitCode'] = 1
        self.e['records']['point']['verification'] = self.write('point-verification.json', verification)
        self.write('evidence.json', self.e)
        self.assertEqual(self.check()['blockers'], ['missing or mismatched verification receipt'])
        for field in ('credentialIdentity', 'readBy', 'signature-reader'):
            with self.subTest(field=field):
                self.packet()
                readback = json.loads((self.root / 'point-readback.json').read_text())
                if field == 'signature-reader':
                    readback['signatureObject']['readBy'] = self.trust['writerIdentity']
                else:
                    readback[field] = self.trust['writerIdentity']
                self.e['records']['point']['readback'] = self.write('point-readback.json', readback)
                self.write('evidence.json', self.e)
                result = self.check()
                self.assertFalse(result['metadataConsistent'])
                self.assertIn('readback mismatch', result['blockers'][0])

    def test_exact_destination_versions_bytes_creation_and_retention_are_required(self):
        cases = [('endpoint', 'https://other.invalid'), ('bucket', 'other-bucket'), ('region', 'other-region'),
                 ('attemptPrefix', 'evidence/other/'), ('objectKey', 'wrong-key'),
                 ('readBackVersionId', 'wrong-version'), ('readBackBytes', 0), ('readBackSha256', '0' * 64),
                 ('uploadedBy', 'someone-else'), ('readBackCreatedAt', '2030-01-01T10:00:00Z'),
                 ('createdAt', '2030-01-02T11:04:00Z'), ('retainUntil', '2030-01-03T12:00:00Z'),
                 ('readBackRetention', {'mode': 'GOVERNANCE'}), ('readBackLockMode', 'GOVERNANCE')]
        for field, value in cases:
            with self.subTest(field=field):
                original = deepcopy(self.records['point']['payloadReadback'])
                self.records['point']['payloadReadback'][field] = value
                self.packet()
                self.assertFalse(self.check()['metadataConsistent'])
                self.records['point']['payloadReadback'] = original
        self.records['node']['archiveSha256'] = None
        self.records['node']['payloadReadback']['decryptedPlaintextSha256'] = None
        self.packet()
        self.assertFalse(self.check()['metadataConsistent'])

    def test_named_source_children_and_mount_cleanup_is_complete(self):
        for field, value in [('sourceTemporaryAbsent', False), ('sourceSnapshotAbsent', None),
                             ('childrenAbsent', False), ('mountsAbsent', False), ('associatedVolumesAbsent', False),
                             ('listenersAbsent', False), ('ownedResources', {}), ('absentChildProcessIds', [1234]),
                             ('absentMounts', {'container-etcd': ['/restore']})]:
            with self.subTest(field=field):
                original = deepcopy(self.records['cleanup'][field])
                self.records['cleanup'][field] = value
                self.packet()
                result = self.check()
                self.assertFalse(result['metadataConsistent'])
                self.assertEqual(result['operationalGate'], 'closed')
                self.records['cleanup'][field] = original

    def test_metadata_resource_limits_are_sanitized_and_tool_hashing_is_uncapped(self):
        for raw in (b'{"private":' + b'[' * 1000 + b'0' + b']' * 1000 + b'}',
                    b'{"private":"' + b'x' * guard.MAX_METADATA_BYTES + b'"}'):
            with self.subTest(bytes=len(raw)):
                self.packet()
                self.write('bindings.json', raw)
                self.binding_hash = guard.digest(raw)
                result = self.check()
                self.assertFalse(result['metadataConsistent'])
                self.assertNotIn(str(self.root), json.dumps(result))
                self.assertEqual(result['operationalGate'], 'closed')
        self.packet()
        for error in (RecursionError, MemoryError):
            with self.subTest(error=error.__name__), patch.object(guard.json, 'loads', side_effect=error):
                result = self.check()
                self.assertEqual(result['blockers'], ['metadata parser resource limit exceeded'])
        tool = self.root / 'synthetic-tool'
        tool.write_bytes(b'x' * (guard.MAX_METADATA_BYTES + 1))
        for reference in [self.b['lineage'], self.b['procedure'], *self.b['tools']]:
            reference['sha256'] = guard.digest(tool.read_bytes())
        self.packet()
        self.assertTrue(self.check()['metadataConsistent'])

    def test_exact_hashed_bindings_are_parsed_once(self):
        self.packet()
        original = guard.read_file
        reads = []
        def read(path, private=False):
            if Path(path) == self.bindings_path:
                reads.append(path)
                if len(reads) > 1:
                    return b'{"changed":true}'
            return original(path, private)
        with patch.object(guard, 'read_file', side_effect=read):
            self.assertTrue(self.check()['metadataConsistent'])
        self.assertEqual(len(reads), 1)


if __name__ == '__main__':
    unittest.main()
