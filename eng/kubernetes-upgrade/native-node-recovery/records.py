"""Measured node/etcd evidence to unsigned exact-plan retirement prerequisites.

This is the narrower 4.27 contract. It never labels the separate legacy 4.1
readiness consumer passed, and cannot sign, publish or approve any record.
"""
from datetime import datetime, timezone, timedelta
import hashlib
import json
import re


def require(ok, why):
    if not ok:
        raise ValueError(why)


def canonical(value):
    return (json.dumps(value, sort_keys=True, indent=2)+'\n').encode()


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def timestamp(value):
    require(type(value) is str and re.fullmatch(r'\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,6})?(?:Z|\+00:00)', value),
            'node-record-timestamp-refused')
    return datetime.fromisoformat(value.replace('Z', '+00:00'))


def build(actual, plan, plan_sha, *, now=None):
    now = now or datetime.now(timezone.utc)
    require(type(plan_sha) is str and re.fullmatch('[a-f0-9]{64}', plan_sha)
            and sha(canonical(plan)) == plan_sha and plan.get('kind') == 'kubesphere-retirement-plan'
            and plan.get('productionApproved') is False and actual.get('kind') == 'actual-node-etcd-recovery-evidence'
            and plan['sourceClusterUid'] == actual['sourceClusterUid'] and actual['phase'] in ('pre', 'post'),
            'node-record-source-or-plan-binding-refused')
    observed = timestamp(actual['completedAt'])
    require(timestamp(actual['startedAt']) <= observed <= now < observed+timedelta(days=1),
            'node-record-evidence-future-or-expired')
    if actual['phase'] == 'post':
        execution = actual.get('completedRetirementBinding', {}) or {}
        require(execution.get('planSha256') == plan_sha
                and timestamp(execution['finishedAt']) <= timestamp(actual['startedAt']),
                'node-record-post-retirement-execution-binding-refused')
    captured, restored, files = actual['captured'], actual['restored'], actual['configuration']
    require(captured.get('error') is None and captured.get('localCaptureCompleted') is True
            and captured.get('cleanupPassed') is True
            and captured.get('sourceNodeAndNamespaceUidRevalidated') is True
            and captured.get('protectedNamespacePvcPvBindingsUnchanged') is True
            and restored.get('state') == 'passed' and restored.get('cleanupPassed') is True
            and restored.get('confirmedTargetCreates') == 2
            and len(restored.get('cleanup', [])) == 2
            and all(v.get('absenceVerified') is True for v in restored['cleanup']),
            'node-record-capture-restore-or-cleanup-refused')
    summary = captured['summary']
    etcd = restored['results']['etcd']['restoration']
    node = restored['results']['file']['restoration']
    require(all(summary.get(k) is True for k in ('sourceMountsReadonly', 'snapshotPlaintextOnlyInMemoryBackedHelper',
            'plaintextSnapshotRemoved', 'sourceNodeMetadataUnchanged'))
            and summary.get('plaintextArchiveFileWritten') is False
            and all(etcd.get(k) is True for k in ('healthy', 'sourceAndTargetVisibleStateEqual',
                'snapshotIntegrityVerified', 'nativeHashkvEqual'))
            and etcd.get('revisionBump') is False and etcd.get('skipHashCheck') is False
            and etcd['revision'] == summary['snapshotStatus']['revision'] == summary['sourceFullVisibleKeyspaceSha256']['revision']
            and etcd['keyCount'] == summary['snapshotStatus']['totalKey'] == summary['sourceFullVisibleKeyspaceSha256']['keyCount']
            and etcd['keyHash'] == summary['sourceFullVisibleKeyspaceSha256']['sha256']
            and etcd['targetClusterId'] != summary['sourceEtcdIdentity']['clusterId']
            and etcd['targetMemberId'] != summary['sourceEtcdIdentity']['memberId']
            and node.get('state') == 'verified-node-file-restoration-only'
            and node.get('numericOwnersAndModesApplied') is True
            and node.get('allIncludedDiscoveredCertificateKeyPairsMatch') is True
            and node['members'] == len(files['entries']) == len(summary['archiveManifest']['entries'])
            and node['regularFileBytes'] == files['hashedBytes'], 'node-record-measured-comparison-refused')
    for role, mask in [('file', 13), ('etcd', 0)]:
        profile = restored['results'][role]['profile']
        require(profile.get('capabilityMask') == mask and profile.get('noNewPrivileges') is True
                and profile.get('rootReadonly') is True and profile.get('workTmpfsNoexec') is True
                and len(profile.get('sourceRouteDenials', [])) >= 2
                and all(v.get('errno') in (101, 113) for v in profile['sourceRouteDenials']),
                'node-record-actual-isolation-profile-refused')
    configuration = sha(canonical(files))
    require(configuration == restored['configurationSha256'], 'node-record-configuration-hash-refused')
    protected = actual['published']
    for name in ('snapshot.db.age', 'node-recovery.tar.gz.age'):
        item, source = protected[name], summary['artifacts'][name]
        require(item['bytes'] == source['ciphertextBytes']
                and item['sha256'] == item['readBackSha256'] == source['ciphertextSha256']
                and item['readBy'] == 'validator' and item['versionId']
                and item['serverSideEncryption'] == 'AES256' and item['objectLockMode'] == 'COMPLIANCE'
                and timestamp(item['retainUntil']) >= timestamp(item['createdAt'])+timedelta(days=30)
                and timestamp(item['createdAt']) <= timestamp(item['readAt']) <= observed,
                'node-record-actual-immutable-readback-refused')
    common = {'attemptId': plan['attemptId'], 'planSha256': plan_sha,
        'sourceClusterUid': actual['sourceClusterUid'], 'capturedAt': actual['completedAt'],
        'expiresAt': (observed+timedelta(days=1)).isoformat(), 'verificationResult': 'pass',
        'sourceEvidencePhase': actual['phase'], 'scopeSha256': actual['scopeSha256'],
        'legacy41ReadinessPassed': False, 'operatingSystemBootProven': False, 'sshAuthenticationProven': False}
    canary = sha(json.dumps(summary['sourceCanaryDigests'], sort_keys=True, separators=(',', ':')).encode())
    require(etcd['canarySha256'] == canary, 'node-record-canary-digest-refused')
    point = {**common, 'kind': 'external-etcd-recovery-point', 'snapshotIntegrityVerified': True,
        'capturedAt': protected['snapshot.db.age']['createdAt'],
        'capturedAtBasis': 'actual exact-version provider LastModified; original precision preserved',
        'expiresAt': (timestamp(protected['snapshot.db.age']['createdAt'])+timedelta(days=1)).isoformat(),
        'encrypted': True, 'immutableOffNodeVersion': protected['snapshot.db.age']['versionId'],
        'independentReadbackVerified': True, 'revision': etcd['revision'], 'keyCount': etcd['keyCount'],
        'keyHash': etcd['keyHash'], 'canarySha256': canary, 'configurationSha256': configuration,
        'sourceMemberId': summary['sourceEtcdIdentity']['memberId'],
        'sourceEtcdClusterId': summary['sourceEtcdIdentity']['clusterId'],
        'snapshotSha256': summary['artifacts']['snapshot.db.age']['plaintextSha256'],
        'payloadReadback': protected['snapshot.db.age'], 'fullVisibleKeyspace': summary['sourceFullVisibleKeyspaceSha256'],
        'nativeHashkv': summary['sourceNativeHashkv']}
    point_sha = sha(canonical(point))
    restore = {**common, 'kind': 'external-etcd-isolated-restore', 'recoveryPointSha256': point_sha,
        'startedAt': actual['restoreStartedAt'], 'finishedAt': actual['restoreFinishedAt'],
        'targetClusterUid': etcd['targetClusterId'], 'targetIdentityKind': 'external-etcd-cluster-id',
        'targetMemberId': etcd['targetMemberId'], 'sourceFenced': True,
        'fenceDirection': 'isolated target has no network route to source',
        'isolated': True, 'memberHealthy': True, 'cleanupVerified': True,
        **{k: point[k] for k in ('revision', 'keyCount', 'keyHash', 'canarySha256')},
        'actualEtcdRestoration': restored['results']['etcd'], 'cleanup': restored['cleanup']}
    node_record = {**common, 'kind': 'node-recovery-bundle', 'recoveryPointSha256': point_sha,
        'capturedAt': protected['node-recovery.tar.gz.age']['createdAt'],
        'capturedAtBasis': 'actual exact-version provider LastModified; original precision preserved',
        'expiresAt': (timestamp(protected['node-recovery.tar.gz.age']['createdAt'])+timedelta(days=1)).isoformat(),
        'configurationSha256': configuration, 'encrypted': True, 'independentReadbackVerified': True,
        'configurationMatchesSource': True, 'ownerModesPreserved': True, 'testedRecoveryProcedure': True,
        'testedProcedureScope': 'descriptor-confined numeric-owner/mode/file/key restoration; separate native client access',
        'immutableOffNodeVersion': protected['node-recovery.tar.gz.age']['versionId'],
        'payloadReadback': protected['node-recovery.tar.gz.age'], 'actualFileRestoration': restored['results']['file']}
    native = actual['native']
    require(all(native.get(k) is True for k in ('directPrivateAccess', 'administratorAccessVerified',
            'independentCustodyReadback', 'breakGlassIndependent', 'noCredentialOrGrantIssued',
            'noPlaintextConfigurationFileWritten')), 'node-record-independent-native-access-refused')
    require(timestamp(actual['startedAt']) <= timestamp(actual['restoreStartedAt'])
            <= timestamp(actual['restoreFinishedAt']) <= timestamp(native['observedAt']) <= observed
            and all(now < timestamp(protected[name]['createdAt'])+timedelta(days=1)
                    for name in ('snapshot.db.age', 'node-recovery.tar.gz.age')),
            'node-record-original-event-order-or-age-refused')
    return {'external-etcd-recovery-point.json': point,
        'external-etcd-isolated-restore.json': restore, 'node-recovery-bundle.json': node_record,
        'native-access.json': {**native, **common, 'kind': 'independent-native-access',
            'capturedAt': native['observedAt'],
            'expiresAt': (timestamp(native['observedAt'])+timedelta(days=1)).isoformat()}}
