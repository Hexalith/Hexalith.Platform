import copy
from datetime import datetime, timezone, timedelta
import hashlib
import json
import unittest
import records as r


def inputs():
    clock = datetime.now(timezone.utc)
    at = (clock-timedelta(seconds=5)).isoformat()
    files = {'entries': {'synthetic/file': {'kind': 'file'}}, 'hashedBytes': 7}
    config_sha = r.sha(r.canonical(files))
    key_hash = 'a'*64
    canaries = {'/synthetic': 'b'*64}
    canary_sha = r.sha(json.dumps(canaries, sort_keys=True, separators=(',', ':')).encode())
    artifacts = {name: {'ciphertextBytes': 123, 'ciphertextSha256': 'c'*64,
        'plaintextSha256': 'd'*64} for name in ['snapshot.db.age', 'node-recovery.tar.gz.age']}
    summary = {'sourceMountsReadonly': True, 'snapshotPlaintextOnlyInMemoryBackedHelper': True,
        'plaintextSnapshotRemoved': True, 'sourceNodeMetadataUnchanged': True, 'plaintextArchiveFileWritten': False,
        'snapshotStatus': {'revision': 8, 'totalKey': 3},
        'sourceFullVisibleKeyspaceSha256': {'revision': 8, 'keyCount': 3, 'sha256': key_hash},
        'sourceEtcdIdentity': {'clusterId': '1', 'memberId': '2'}, 'archiveManifest': {'entries': files['entries']},
        'sourceCanaryDigests': canaries, 'sourceNativeHashkv': [{'HashKV': {'hash': 123, 'hash_revision': 8}}],
        'artifacts': artifacts}
    profile = {'capabilityMask': 0, 'noNewPrivileges': True, 'rootReadonly': True, 'workTmpfsNoexec': True,
        'sourceRouteDenials': [{'errno': 101}, {'errno': 101}]}
    etcd = {'healthy': True, 'sourceAndTargetVisibleStateEqual': True, 'snapshotIntegrityVerified': True,
        'nativeHashkvEqual': True, 'revisionBump': False, 'skipHashCheck': False, 'revision': 8,
        'keyCount': 3, 'keyHash': key_hash, 'targetClusterId': '3', 'targetMemberId': '4', 'canarySha256': canary_sha}
    node = {'state': 'verified-node-file-restoration-only', 'numericOwnersAndModesApplied': True,
        'allIncludedDiscoveredCertificateKeyPairsMatch': True, 'members': 1, 'regularFileBytes': 7}
    restored = {'state': 'passed', 'cleanupPassed': True, 'confirmedTargetCreates': 2,
        'cleanup': [{'absenceVerified': True}, {'absenceVerified': True}], 'configurationSha256': config_sha,
        'results': {'file': {'profile': {**profile, 'capabilityMask': 13}, 'restoration': node},
                    'etcd': {'profile': profile, 'restoration': etcd}}}
    protected = {name: {'bytes': 123, 'sha256': 'c'*64, 'readBackSha256': 'c'*64, 'readBy': 'validator',
        'versionId': 'provider-version', 'serverSideEncryption': 'AES256', 'objectLockMode': 'COMPLIANCE',
        'retainUntil': (clock+timedelta(days=30)).isoformat(), 'createdAt': at, 'readAt': at} for name in artifacts}
    native = {k: True for k in ['directPrivateAccess', 'administratorAccessVerified', 'independentCustodyReadback',
        'breakGlassIndependent', 'noCredentialOrGrantIssued', 'noPlaintextConfigurationFileWritten']}
    native['observedAt'] = at
    actual = {'kind': 'actual-node-etcd-recovery-evidence', 'sourceClusterUid': 'source', 'phase': 'pre',
        'startedAt': at, 'completedAt': at, 'restoreStartedAt': at, 'restoreFinishedAt': at,
        'scopeSha256': 'e'*64, 'configuration': files, 'restored': restored,
        'published': protected, 'native': native, 'captured': {'error': None, 'localCaptureCompleted': True,
            'cleanupPassed': True, 'sourceNodeAndNamespaceUidRevalidated': True,
            'protectedNamespacePvcPvBindingsUnchanged': True, 'summary': summary}}
    plan = {'kind': 'kubesphere-retirement-plan', 'attemptId': 'future-execution',
        'productionApproved': False, 'sourceClusterUid': 'source'}
    return actual, plan


class RecordTests(unittest.TestCase):
    def build(self, actual=None, plan=None):
        a, p = inputs()
        a, p = actual or a, plan or p
        return r.build(a, p, r.sha(r.canonical(p)))

    def test_exact_point_hash_binds_restore_and_node_without_legacy_green(self):
        out = self.build()
        point_sha = r.sha(r.canonical(out['external-etcd-recovery-point.json']))
        for name in ['external-etcd-isolated-restore.json', 'node-recovery-bundle.json']:
            self.assertEqual(out[name]['recoveryPointSha256'], point_sha)
        self.assertTrue(all(v['legacy41ReadinessPassed'] is False for v in out.values()))
        self.assertTrue(all(v['sshAuthenticationProven'] is False for v in out.values()))

    def test_substituted_source_or_keyspace_cannot_be_projected_to_pass(self):
        actual, plan = inputs()
        actual['restored']['results']['etcd']['restoration']['keyHash'] = 'f'*64
        with self.assertRaises(ValueError):
            self.build(actual, plan)
        actual, plan = inputs()
        plan['sourceClusterUid'] = 'different-source'
        with self.assertRaises(ValueError):
            self.build(actual, plan)

    def test_unclean_target_or_expanded_capability_refused(self):
        for change in ['cleanup', 'capability']:
            actual, plan = inputs()
            if change == 'cleanup':
                actual['restored']['cleanup'][0]['absenceVerified'] = False
            else:
                actual['restored']['results']['file']['profile']['capabilityMask'] = 0x3fff
            with self.assertRaises(ValueError):
                self.build(actual, plan)

    def test_local_only_or_changed_validator_bytes_refused(self):
        actual, plan = inputs()
        actual['published']['snapshot.db.age']['readBackSha256'] = 'f'*64
        with self.assertRaises(ValueError):
            self.build(actual, plan)

    def test_future_or_expired_evidence_is_not_refreshed(self):
        for days in [-2, 2]:
            actual, plan = inputs()
            actual['startedAt'] = actual['completedAt'] = (datetime.now(timezone.utc)+timedelta(days=days)).isoformat()
            with self.assertRaises(ValueError):
                self.build(actual, plan)

    def test_source_target_identity_reuse_or_missing_native_readback_refused(self):
        actual, plan = inputs()
        actual['restored']['results']['etcd']['restoration']['targetMemberId'] = '2'
        with self.assertRaises(ValueError):
            self.build(actual, plan)
        actual, plan = inputs()
        actual['native']['independentCustodyReadback'] = False
        with self.assertRaises(ValueError):
            self.build(actual, plan)


if __name__ == '__main__':
    unittest.main()
