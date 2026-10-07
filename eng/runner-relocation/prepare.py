#!/usr/bin/env python3
"""Story 4.3 private preparation and signed source-drift checks; no remote operations."""
import argparse
from datetime import datetime, timedelta, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess


PROJECT = Path(__file__).resolve().parents[2]
ARTIFACTS = Path('_bmad-output/implementation-artifacts')
STORY = ARTIFACTS / '4-3-relocate-the-privileged-forgejo-runner.md'
SOURCES = (STORY, ARTIFACTS / 'epic-4-context.md',
           ARTIFACTS / 'evidence/epic-4/initial-cluster-inventory.md',
           Path('eng/runner-relocation/prepare.py'), Path('eng/runner-relocation/test_prepare.py'),
           Path('eng/runner-relocation/README.md'))
PHASES = ('disable-scheduling', 'revoke-credentials', 'delete-resources', 'delete-namespace')
NAMESPACE = 'hexalith-runner-relocation'
ID = re.compile(r'[A-Za-z0-9][A-Za-z0-9_.@:-]{0,127}')
SHA = re.compile(r'[0-9a-f]{64}')
FORBIDDEN = {'token', 'registrationtoken', 'jobsecret', 'password', 'credentials',
             'registrycredentials', 'kubeconfig', 'kubeconfigcontent', 'secretdata',
             'authorization', 'cookie', 'headers', 'responsebody', 'data', 'stringdata',
             'clientsecret', 'privatekey', 'serviceaccounttoken', 'certificate'}


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(record):
    return (json.dumps(record, indent=2, sort_keys=True, allow_nan=False) + '\n').encode()


def identifier(value):
    return isinstance(value, str) and ID.fullmatch(value) is not None


def sha(value):
    return isinstance(value, str) and SHA.fullmatch(value) is not None


def timestamp(value):
    require(isinstance(value, str), 'missing-evidence-time')
    try:
        parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    except ValueError:
        raise ValueError('invalid-evidence-time') from None
    require(parsed.tzinfo is not None, 'invalid-evidence-time')
    return parsed


def private_path(path, project, *, file=False):
    path = Path(path).absolute()
    require(not any(p.is_symlink() for p in (path, *path.parents)), 'symlink-in-private-path')
    require(project.resolve() not in (path.resolve(), *path.resolve().parents),
            'evidence-must-be-outside-repository')
    require(not any((p / '.git').exists() or (p / '.git').is_symlink() for p in (path, *path.parents)),
            'evidence-must-be-outside-git-worktree')
    if path.exists():
        require(path.stat().st_uid == os.getuid() and not stat.S_IMODE(path.stat().st_mode) & 0o077,
                'custody-must-be-owner-only')
    if file:
        require(path.is_file(), 'missing-private-input')
        private_path(path.parent, project)
    return path


def write_new(path, data):
    fd = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as stream:
        stream.write(data)


def templates(attempt, operator, hashes):
    common = {'story': '4.3', 'attemptId': attempt, 'operator': operator,
              'capturedAt': None, 'expiresAt': None, 'accepted': False}
    source = {'clusterUid': None, 'namespace': {'name': 'forgejo-runner', 'uid': None,
              'resourceVersion': None, 'configSha256': None},
              'deployment': {'name': 'forgejo-runner', 'uid': None, 'resourceVersion': None,
              'podTemplateSha256': None, 'configSha256': None},
              'registeredRunnerId': None, 'runnerConfigSha256': None}
    records = {
        'attempt.json': {**common, 'classification': 'local preparation only',
            'sourceHashes': hashes, 'source': source, 'targetHostId': None,
            'cutoverWindow': {'startsAt': None, 'endsAt': None},
            'mutationAuthorized': False, 'operationalAcceptance': False, 'complete': False},
        'administrator-decisions.json': {**common, 'sourceBaselineSha256': None,
            'operatorIds': [], 'checkpointMaxAgeSeconds': 60, 'approvedPhases': [],
            'cutoverWindow': {'startsAt': None, 'endsAt': None},
            'hostOwner': None, 'targetHostId': None, 'hardeningPolicyReference': None,
            'patchingEncryptionBackupPolicyReference': None, 'networkZone': None,
            'egressPolicyReference': None, 'clusterRouteInventorySha256': None,
            'runnerVersion': None, 'runnerArtifactSha256': None,
            'runtimeVersion': None, 'runtimeArtifactSha256': None,
            'validationLabel': None, 'productionLabels': [], 'approvedJobMatrix': [],
            'secretStoreEnrollmentRotationReference': None,
            'cachePolicy': 'disposable', 'storageEraseAndResidualCheckReference': None},
        'source-baseline.json': {**common, 'source': source},
        'target-host-attestation.json': {**common, 'hostIdentity': None, 'owner': None,
            'nonCoResidencyChecks': [], 'hardeningChecks': [],
            'networkPolicyFirewallConfigSha256': None, 'approvedInstallationPins': None,
            'credentialEnrollmentReference': None},
        'external-runner-job-matrix.json': {**common, 'hostId': None, 'runnerInstanceId': None,
            'validationLabelRuns': [], 'normalTriggerRuns': [], 'postRemovalRuns': []},
        'cluster-isolation-proof.json': {**common, 'hostId': None, 'runnerInstanceId': None,
            'routeInventory': [], 'jobContainerProbes': [], 'absentCredentialChecks': [],
            'networkProxyAuditEvents': [], 'postRemovalProbes': []},
        'source-removal.json': {**common, 'sourceRevalidations': [], 'quiescence': None,
            'credentialRevocationChecks': [], 'configurationPreservation': None,
            'deletedResourceIdentities': [], 'namespaceAbsence': None, 'oldImageAbsence': None,
            'storageInventory': [], 'secureErasureReceipts': [],
            'providerResidualChecks': [], 'postRemovalJobResults': []},
        'runner-relocation-result.json': {**common, 'evidenceSha256': {},
            'mutationAuthorized': False, 'operationalAcceptance': False, 'complete': False},
    }
    for phase in PHASES:
        records[f'{phase}-checkpoint.json'] = {**common, 'phase': phase, 'source': source,
            'collectionStartedAt': None, 'activeJobsObservedAt': None,
            'activeJobsReadable': False, 'activeJobIds': [], 'deploymentPresent': None,
            'sourceDeletionReceipt': None}
    return records


def prepare(project, root, attempt, operator):
    require(isinstance(attempt, str) and re.fullmatch(r'[a-z0-9][a-z0-9-]{0,79}', attempt),
            'invalid-attempt-id')
    require(identifier(operator), 'invalid-operator')
    project = Path(project).resolve()
    root = private_path(root, project)
    recovery = (Path.home() / 'hexalith-upgrade-evidence', Path.home() / 'hexalith-recovery-evidence')
    require(not any(p.resolve() in (root.resolve(), *root.resolve().parents) for p in recovery),
            'must-not-write-into-recovery-custody')
    directory = root / 'evidence/epic-4/4-3' / attempt
    for path in (directory, *directory.parents):
        if path == root.parent:
            break
        private_path(path, project)
    require(not directory.exists(), 'attempt-already-exists')
    hashes = {str(p): digest((project / p).read_bytes()) for p in SOURCES}
    old_umask = os.umask(0o077)
    try:
        directory.mkdir(mode=0o700, parents=True, exist_ok=False)
        for name, record in templates(attempt, operator, hashes).items():
            write_new(directory / name, canonical(record))
        write_new(directory / 'SHA256SUMS', ''.join(
            f'{digest(p.read_bytes())}  {p.name}\n' for p in sorted(directory.iterdir())).encode())
    finally:
        os.umask(old_umask)
    return directory


def no_sensitive_fields(value):
    if isinstance(value, dict):
        for name, child in value.items():
            require(name.lower().replace('_', '').replace('-', '') not in FORBIDDEN,
                    'prohibited-evidence-field')
            no_sensitive_fields(child)
    elif isinstance(value, list):
        for child in value:
            no_sensitive_fields(child)
    elif isinstance(value, str):
        require('-----BEGIN ' not in value and 'k8s-aws-v1.' not in value,
                'prohibited-evidence-value')


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'duplicate-json-key')
        result[key] = value
    return result


def reject_constant(value):
    raise ValueError('nonfinite-json')


def signed_input(path, project, trust, principal, tool):
    path = private_path(path, project, file=True)
    signature = private_path(str(path) + '.sig', project, file=True)
    trust = private_path(trust, project, file=True)
    tool = Path(tool)
    require(tool.is_absolute() and tool.is_file() and os.access(tool, os.X_OK),
            'absolute-local-ssh-keygen-required')
    data = path.read_bytes()
    verified = subprocess.run([str(tool), '-Y', 'verify', '-f', str(trust), '-I', principal,
        '-n', NAMESPACE, '-s', str(signature)], input=data, capture_output=True, timeout=30)
    require(verified.returncode == 0, 'unverified-evidence-signature')
    record = json.loads(data, object_pairs_hook=unique_object, parse_constant=reject_constant)
    require(isinstance(record, dict), 'invalid-evidence-record')
    no_sensitive_fields(record)
    return record, digest(data)


def source_identity(source):
    require(isinstance(source, dict) and identifier(source.get('clusterUid')),
            'missing-cluster-identity')
    for kind in ('namespace', 'deployment'):
        obj = source.get(kind)
        require(isinstance(obj, dict) and obj.get('name') == 'forgejo-runner', 'wrong-source-name')
        require(identifier(obj.get('uid')) and identifier(obj.get('resourceVersion')),
                'missing-source-identity')
        require(sha(obj.get('configSha256')), 'missing-source-config-digest')
    require(sha(source['deployment'].get('podTemplateSha256')), 'missing-template-digest')
    require(identifier(source.get('registeredRunnerId')) and sha(source.get('runnerConfigSha256')),
            'ambiguous-registered-runner')


def check_source(baseline, decisions, current, baseline_hash, phase, clock):
    """Check signed statements only; callers must reread at actual request dispatch."""
    require(phase in PHASES, 'unknown-phase')
    for record in (baseline, decisions, current):
        require(record.get('story') == '4.3' and identifier(record.get('attemptId')),
                'wrong-evidence-scope')
        require(timestamp(record.get('capturedAt')) <= clock < timestamp(record.get('expiresAt')),
                'stale-or-future-evidence')
        no_sensitive_fields(record)
    require(baseline['attemptId'] == decisions['attemptId'] == current['attemptId'], 'attempt-drift')
    require(sha(baseline_hash) and decisions.get('sourceBaselineSha256') == baseline_hash,
            'unapproved-baseline-bytes')
    require(decisions.get('accepted') is True and isinstance(decisions.get('approvedPhases'), list)
            and phase in decisions['approvedPhases'], 'phase-not-approved')
    operators = decisions.get('operatorIds')
    require(isinstance(operators, list) and operators and all(identifier(v) for v in operators)
            and len(set(operators)) == len(operators), 'missing-approved-operators')
    require(current.get('operator') in operators and current.get('phase') == phase,
            'wrong-checkpoint-operator-or-phase')
    window = decisions.get('cutoverWindow')
    require(isinstance(window, dict) and timestamp(window.get('startsAt')) <= clock
            < timestamp(window.get('endsAt')), 'outside-approved-cutover-window')
    limit = decisions.get('checkpointMaxAgeSeconds')
    require(type(limit) is int and 0 < limit <= 60, 'invalid-checkpoint-age-limit')
    started = timestamp(current.get('collectionStartedAt'))
    captured = timestamp(current['capturedAt'])
    require(timestamp(baseline['capturedAt']) <= timestamp(decisions['capturedAt']) <= started
            <= captured <= clock and clock - started <= timedelta(seconds=limit),
            'checkpoint-not-immediately-after-approval')
    source_identity(baseline.get('source'))
    source_identity(current.get('source'))
    require(baseline['source'] == current['source'], 'source-identity-or-config-drift')
    require(current.get('activeJobsReadable') is True, 'unreadable-active-job-set')
    jobs = current.get('activeJobIds')
    require(isinstance(jobs, list) and all(identifier(v) for v in jobs) and len(set(jobs)) == len(jobs),
            'invalid-active-job-set')
    require(started <= timestamp(current.get('activeJobsObservedAt')) <= captured,
            'stale-active-job-set')
    require(phase == 'disable-scheduling' or not jobs, 'active-jobs-block-revocation-or-deletion')
    if phase == 'delete-namespace' and current.get('deploymentPresent') is False:
        receipt = current.get('sourceDeletionReceipt')
        require(isinstance(receipt, dict) and receipt.get('source') == baseline['source']
                and sha(receipt.get('evidenceSha256')) and receipt.get('absenceVerified') is True
                and timestamp(decisions['capturedAt']) <= timestamp(receipt.get('deletedAt')) <= started,
                'missing-exact-source-deletion-lineage')
    else:
        require(current.get('deploymentPresent') is True, 'source-deployment-not-present')
    return {'story': '4.3', 'attemptId': current['attemptId'], 'phase': phase,
            'sourceCheckpointConsistent': True,
            'mutationAuthorized': False, 'operationalAcceptance': False, 'complete': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    create = commands.add_parser('prepare')
    create.add_argument('--evidence-root', required=True)
    create.add_argument('--attempt-id', required=True)
    create.add_argument('--operator', required=True)
    check = commands.add_parser('check-source')
    check.add_argument('--baseline', required=True)
    check.add_argument('--decisions', required=True)
    check.add_argument('--checkpoint', required=True)
    check.add_argument('--allowed-signers', required=True)
    check.add_argument('--principal', required=True)
    check.add_argument('--ssh-keygen', default='/usr/bin/ssh-keygen')
    check.add_argument('--phase', required=True, choices=PHASES)
    args = parser.parse_args()
    try:
        if args.command == 'prepare':
            directory = prepare(PROJECT, args.evidence_root, args.attempt_id, args.operator)
            print(json.dumps({'directory': str(directory), 'mutationAuthorized': False,
                              'operationalAcceptance': False, 'complete': False}))
        else:
            records = [signed_input(p, PROJECT, args.allowed_signers, args.principal, args.ssh_keygen)
                       for p in (args.baseline, args.decisions, args.checkpoint)]
            result = check_source(records[0][0], records[1][0], records[2][0],
                                  records[0][1], args.phase, datetime.now(timezone.utc))
            result['signatureVerification'] = 'passed'
            result['verifiedRecordSha256'] = dict(zip(('baseline', 'decisions', 'checkpoint'),
                                                      (record[1] for record in records)))
            print(json.dumps(result))
    except (ValueError, OSError, UnicodeError, RecursionError, subprocess.SubprocessError):
        parser.exit(2, 'Runner relocation preparation/check failed; no mutation is authorized.\n')


if __name__ == '__main__':
    main()
