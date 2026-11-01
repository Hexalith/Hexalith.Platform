"""Construct/validate the telemetry section of an original signed Memories proof.

Pure construction consumes actual qualified execution receipts and canary
transcripts. No backend, signer, credential read or implicit authority exists.
Policy, manifest, complete Memories proof, gate and final validation keep their
original signatures. The caller must bind this producer/validator in that policy.
"""
import hashlib
import json
import re
import math
from datetime import datetime, timedelta
import telemetry_postgres_adapter as adapter

STAGES = ('capture', 'isolation', 'comparison', 'restore', 'resume', 'cleanup')
ISOLATION_CHECKS = ('noSourceStorageOrCredentialMounts', 'sourceDatabaseAccessDenied',
                    'sourceApplicationAccessDenied', 'anonymousApiAuthorityDenied',
                    'noPublicRoute', 'targetSocketOnly', 'distinctTargetStorage', 'actualControlsPassed')
RESUME_CHECKS = ('workerUidPreserved', 'workerReplicasRestored', 'clockUidPreserved',
                 'clockRemainsZero', 'sourceHealthy', 'cleanDrain')
CLEANUP_CHECKS = ('targetNamespaceAbsent', 'targetWorkloadsAbsent', 'targetSecretsAbsent',
                  'targetPvcAbsent', 'targetPvAbsent', 'ownedVolumePathAbsent')


def require(ok, reason='telemetry-execution-evidence-incomplete'):
    if not ok:
        raise ValueError(reason)


def canonical(value):
    return (json.dumps(value, indent=2, sort_keys=True) + '\n').encode()


def digest(value):
    return hashlib.sha256(value).hexdigest()


def checks(record, names):
    require(isinstance(record.get('checks'), dict) and all(record['checks'].get(k) is True for k in names))


def assemble(proposal, records, manifest, source_raw, target_raw):
    """Require real source/target semantic comparison before producing passing flags.

    records maps each stage to {path, sha256, raw}; raw contains the original
    canonical execution receipt bytes, whose digest must be in the manifest.
    No supplied success boolean substitutes for the tagged transcript comparator.
    """
    adapter.validate(proposal)
    source = proposal['source']
    recovery_id = proposal['transfer']['key'].split('/')[1]
    require(proposal['transfer']['key'] == 'memories/' + recovery_id + '/telemetry-postgresql.dump.age')
    require(set(records) == set(STAGES))
    parsed, evidence = {}, {}
    for stage in STAGES:
        binding = records[stage]
        require(isinstance(binding, dict) and set(binding) == {'path', 'sha256', 'raw'}
                and type(binding['raw']) is bytes and len(binding['raw']) <= 4 * 1024 * 1024
                and binding['path'] == 'memories/telemetry/' + stage + '.json'
                and digest(binding['raw']) == binding['sha256'])
        record = adapter.strict_json(binding['raw'])
        require(record.get('kind') == 'telemetry-' + stage and record.get('recoveryId') == recovery_id
                and record.get('proposalInputSha256') == digest(adapter.canonical(proposal))
                and record.get('sourceClusterUid') == source['cluster']['uid']
                and record.get('sourceUid') == source['statefulSet']['uid']
                and record.get('verificationResult') == 'pass')
        matches = [v for v in manifest.get('localEvidence', [])
                   if v.get('path') == binding['path'] and v.get('sha256') == binding['sha256']]
        require(len(matches) == 1)
        parsed[stage] = record
        evidence[stage] = {k: binding[k] for k in ('path', 'sha256')}
    capture, restore = parsed['capture'], parsed['restore']
    adapter.snapshot_commands(proposal, capture['snapshotBinding'])
    checks(capture, ('snapshotHolderAliveThroughDump', 'sameSnapshotDumpAndCanaries',
                     'remoteSessionsAbsent', 'allSourceWritersQuiesced', 'quiescedSequencesStable',
                     'artifactEncrypted', 'independentValidatorReadback'))
    # This executes the complete strict parser/comparator for coverage, row
    # hashes, metadata, sequences, roles/ACLs and intentional target differences.
    comparison = adapter.compare_canaries(proposal, source_raw, target_raw)
    require(parsed['comparison'].get('comparison') == comparison)
    require(capture.get('sourceCanarySha256') == digest(source_raw)
            and restore.get('targetCanarySha256') == digest(target_raw))
    checks(parsed['isolation'], ISOLATION_CHECKS)
    checks(parsed['resume'], RESUME_CHECKS)
    checks(parsed['cleanup'], CLEANUP_CHECKS)
    checks(restore, ('decryptedStdinMatchesCapture', 'targetToolsAndImageMatch', 'emptyTargetBeforeRestore',
                     'restoreCommandSucceeded', 'completeTargetCanaryCaptured', 'targetRolesRestricted'))
    require(restore.get('plaintextSha256') == capture.get('plaintextSha256')
            and restore.get('plaintextBytes') == capture.get('plaintextBytes')
            and isinstance(capture.get('plaintextSha256'), str) and re.fullmatch('[a-f0-9]{64}', capture['plaintextSha256'])
            and type(capture.get('plaintextBytes')) is int and capture['plaintextBytes'] > 0)
    target_uids = restore.get('isolatedTargetUids', {})
    require(isinstance(target_uids, dict) and set(target_uids) == {'namespace', 'statefulSet', 'pvc', 'pv'}
            and len(set(target_uids.values())) == 4 and all(type(v) is str and v for v in target_uids.values())
            and not set(target_uids.values()) & {source[k]['uid'] for k in ('namespace', 'statefulSet', 'pod', 'pvc', 'pv')}
            and parsed['cleanup'].get('isolatedTargetUids') == target_uids
            and parsed['isolation'].get('isolatedTargetUids') == target_uids)
    artifact = capture.get('artifact', {})
    require(isinstance(artifact, dict) and set(artifact) == {'key', 'versionId', 'bytes', 'sha256'}
            and artifact['key'] == proposal['transfer']['key'] and type(artifact['bytes']) is int
            and artifact['bytes'] > 0 and type(artifact['versionId']) is str and artifact['versionId']
            and isinstance(artifact['sha256'], str) and re.fullmatch('[a-f0-9]{64}', artifact['sha256']))
    matching = [v for v in manifest.get('objects', []) if v.get('key') == artifact['key']
                and v.get('versionId') == artifact['versionId']]
    require(len(matching) == 1)
    obj = matching[0]
    require(obj.get('bytes') == obj.get('contentLength') == artifact['bytes']
            and obj.get('readBackSha256') == artifact['sha256'] and obj.get('readBy') == 'validator'
            and obj.get('serverSideEncryption') == 'AES256' and obj.get('objectLockMode') == 'COMPLIANCE'
            and obj.get('retentionApi') == {'mode': 'COMPLIANCE', 'retainUntil': obj.get('objectLockRetainUntil')})
    captured = datetime.fromisoformat(capture['capturedAt'].replace('Z', '+00:00'))
    expires = datetime.fromisoformat(capture['expiresAt'].replace('Z', '+00:00'))
    retain = datetime.fromisoformat(obj['objectLockRetainUntil'].replace('Z', '+00:00'))
    require(captured.tzinfo is not None and expires == captured + timedelta(seconds=86400)
            and retain >= captured + timedelta(days=30))
    rpo = capture.get('measuredRpo', {})
    require(type(rpo.get('seconds')) is int and 0 <= rpo['seconds'] <= 1800 and rpo.get('withinPolicy') is True)
    observed = datetime.fromisoformat(rpo['sourceObservedAt'].replace('Z', '+00:00'))
    cutoff = datetime.fromisoformat(capture['dataCutoff'].replace('Z', '+00:00'))
    # Preserve actual fractional event/provider precision. RPO rounds upward,
    # so a subsecond interval never understates the elapsed recovery gap.
    require(observed >= captured >= cutoff
            and math.ceil((observed-cutoff).total_seconds()) == rpo['seconds'])
    result = {'sourceClusterUid': source['cluster']['uid'], 'sourceUid': source['statefulSet']['uid'],
            'sourceIncarnation': {k: source[k] for k in ('systemIdentifier', 'timeline', 'serverVersionNum',
                                                       'imageId', 'containerId')},
            'sourcePodUid': source['pod']['uid'], 'sourcePvcUid': source['pvc']['uid'],
            'capturedAt': capture['capturedAt'], 'expiresAt': capture['expiresAt'], 'measuredRpo': rpo,
            'artifact': artifact, 'artifactEncrypted': True, 'isolatedTargetUids': target_uids,
            'evidence': evidence, 'checks': {k: True for k in (
                'snapshotHolderAliveThroughDump', 'sameSnapshotDumpAndCanaries', 'quiescedSequencesStable',
                'completeObjectCoverage', 'sourceAndTargetCanariesEqual', 'targetRolesNoLoginOrSuperuser',
                'sourceFenced', 'isolatedTarget', 'sourceResumedAndHealthy', 'targetCleanupComplete',
                'independentValidatorReadback')}}
    return json.loads(canonical(result))


def validation_check(nested, proposal, records, manifest, source_raw, target_raw):
    """Rebuild from measured evidence after original policy/proof/manifest signatures verify."""
    require(nested == assemble(proposal, records, manifest, source_raw, target_raw))
    return {'id': 'memories-telemetry-postgresql', 'status': 'pass', 'proofSha256': digest(canonical(nested))}
