#!/usr/bin/env python3
"""Prepare or inspect a fresh, unsigned prerequisite; never execute recovery.

The expired candidate and its verifier are immutable historical inputs. This
separate tool creates a successor with new target names and an actual clock
observation. A structurally consistent packet never supplies authority, current
recovery, deployment permission or Timesheets acceptance. No subprocess,
credential loader, provider SDK or network transport is used.
"""

import argparse
from datetime import datetime, timedelta, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import stat


MAX_BYTES = 1024 * 1024
HISTORICAL_RID = '20261015t162756z'
HISTORICAL_PREPARED = '2026-10-15T16:27:56Z'
HISTORICAL_CEILING = '2026-10-16T16:27:56Z'
HISTORICAL_SEALED_SHA256 = 'c71f38517e02ea5bc7fa176cfb1abe8f12b43b499fbe6f74fbdc339d91f0b23d'
# Existing adapter source pins the old policy. The refusal anchor was measured
# directly from the retained Timesheets receipt; neither is candidate supplied.
ORIGINAL_POLICY_SHA256 = 'a4be017625ba312ab93d76d71bbbf8433f84c08ed43d798c892a39da8939fcdd'
ORIGINAL_REFUSAL_SHA256 = '38610efadcc9e0358a806e9178c9de75cb737079e864e5186cee21c744d6f810'
HEX = re.compile(r'[0-9a-f]{64}')
LABEL = re.compile(r'[a-z0-9][a-z0-9-]{0,79}')
GAPS = (
    'current-server-and-client-identity', 'current-native-authority-and-revocations',
    'current-selector-rights', 'protected-credential-custody',
    'provider-retention-iam-and-immutable-readback', 'current-source-continuity',
    'legacy-inventory-and-writer-delivery-quiescence', 'maintenance-window-and-resume',
    'qualified-capture-restore-and-cleanup-adapters', 'fresh-capture-and-isolated-restores',
    'exact-operational-approval', 'deployed-timesheets-runtime-acceptance',
)
OUTPUTS = ('capture', 'restore', 'cleanup', 'signatures', 'publication', 'acceptance')


class Refusal(ValueError):
    """A bounded diagnostic containing no supplied record or credential bytes."""


def require(condition, reason):
    if not condition:
        raise Refusal(reason)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def canonical(value):
    return (json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + '\n').encode()


def timestamp(value):
    require(isinstance(value, str) and re.fullmatch(r'20[0-9]{2}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z', value),
            'invalid-utc-time')
    try:
        return datetime.fromisoformat(value.replace('Z', '+00:00'))
    except ValueError:
        raise Refusal('invalid-utc-time') from None


def utc(value):
    return value.astimezone(timezone.utc).replace(microsecond=0).isoformat().replace('+00:00', 'Z')


def custody(info):
    require(info.st_uid in (0, os.getuid()), 'untrusted-owner')
    sticky = stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and bool(info.st_mode & stat.S_ISVTX)
    require(not stat.S_IMODE(info.st_mode) & 0o022 or sticky, 'unsafe-writable-custody')


def open_directory(path):
    """Keep the final directory descriptor; never traverse a symbolic link."""
    path = Path(path)
    require(path.is_absolute() and '..' not in path.parts, 'absolute-nonescaping-path-required')
    descriptor = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try:
        custody(os.fstat(descriptor))
        for component in path.parts[1:]:
            next_descriptor = os.open(component, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
                                      dir_fd=descriptor)
            os.close(descriptor)
            descriptor = next_descriptor
            custody(os.fstat(descriptor))
        return descriptor
    except BaseException:
        os.close(descriptor)
        raise


def safe_path(path):
    path = Path(path)
    parent = open_directory(path.parent)
    try:
        info = os.stat(path.name, dir_fd=parent, follow_symlinks=False)
        require(not stat.S_ISLNK(info.st_mode), 'symlink-refused')
        custody(info)
    finally:
        os.close(parent)
    return info


def read(path):
    path = Path(path)
    parent = open_directory(path.parent)
    try:
        info = os.stat(path.name, dir_fd=parent, follow_symlinks=False)
        require(not stat.S_ISLNK(info.st_mode), 'symlink-refused')
        custody(info)
        require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1 and info.st_size <= MAX_BYTES,
                'bounded-single-link-file-required')
        descriptor = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW, dir_fd=parent)
    finally:
        os.close(parent)
    with os.fdopen(descriptor, 'rb') as stream:
        opened = os.fstat(stream.fileno())
        require((info.st_dev, info.st_ino, info.st_size, info.st_mtime_ns, info.st_ctime_ns) ==
                (opened.st_dev, opened.st_ino, opened.st_size, opened.st_mtime_ns, opened.st_ctime_ns),
                'input-replaced')
        raw = stream.read(MAX_BYTES + 1)
        end = os.fstat(stream.fileno())
    require(len(raw) <= MAX_BYTES and (opened.st_size, opened.st_mtime_ns, opened.st_ctime_ns) ==
            (end.st_size, end.st_mtime_ns, end.st_ctime_ns), 'input-changed')
    return raw


def parse(raw):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, 'duplicate-json-key')
            result[key] = value
        return result
    try:
        return json.loads(raw, object_pairs_hook=unique,
                          parse_constant=lambda _: (_ for _ in ()).throw(Refusal('nonfinite-json')))
    except (UnicodeError, json.JSONDecodeError, RecursionError):
        raise Refusal('invalid-json') from None


def object_value(value):
    require(isinstance(value, dict), 'json-object-required')
    return value


def owner_document(value):
    owner = object_value(value)
    object_value(owner.get('declaredOwner'))
    object_value(owner.get('trustModel'))
    return owner


def binding(path, role):
    require(isinstance(role, str) and LABEL.fullmatch(role), 'invalid-evidence-role')
    raw = read(path)
    return {'role': role, 'path': str(path), 'bytes': len(raw), 'sha256': sha(raw),
            'classification': 'bound-local-record-not-authenticated-by-this-tool'}


def targets(rid):
    return {
        'keycloakPretestNamespace': 's40-kcpre-' + rid,
        'keycloakNamespace': 's40-kc-' + rid,
        'keycloakCluster': 's40-postgres-' + rid,
        'keycloakApplication': 's40-oidc-' + rid,
        'openbaoNamespace': 's40-bao-' + rid,
        'memoriesNamespace': 's40-mem-' + rid,
        'evidencePrefix': 'evidence/4-0/' + rid + '/',
        'payloadPrefixes': {name: name + '/' + rid + '/' for name in ('keycloak', 'openbao', 'memories')},
        'created': False, 'uids': None,
    }


def historical(policy, refusal):
    object_value(policy)
    object_value(refusal)
    require(policy.get('kind') == 'hexalith.unsigned-recovery-policy-candidate' and
            policy.get('recoveryId') == HISTORICAL_RID and
            policy.get('preparedAt') == HISTORICAL_PREPARED and
            policy.get('policyExpiresAt') == HISTORICAL_CEILING and
            policy.get('signed') is False and policy.get('operationalGate') == 'closed',
            'historical-policy-mismatch')
    require(refusal.get('kind') == 'offline-intake-exact-verifier-result' and
            refusal.get('exitCode') == 1 and refusal.get('operationalGate') == 'closed' and
            refusal.get('command', [])[-2:] == ['--sha256', HISTORICAL_SEALED_SHA256] and
            parse(refusal.get('stdout', '')) == {
                'status': 'refused', 'operationalGate': 'closed',
                'reason': 'offline-revision-integrity-or-custody-refusal'},
            'original-verifier-refusal-mismatch')


def prepare(output_parent, old_policy, original_refusal, owner_policy, records=(), *, now=None):
    """Create a distinct local prerequisite directory; never overwrite a file."""
    now = now or datetime.now(timezone.utc)
    prepared = utc(now)
    now = timestamp(prepared)
    require(now >= timestamp(HISTORICAL_CEILING), 'historical-candidate-not-expired')
    policy_raw, refusal_raw, owner_raw = read(old_policy), read(original_refusal), read(owner_policy)
    require(sha(policy_raw) == ORIGINAL_POLICY_SHA256 and sha(refusal_raw) == ORIGINAL_REFUSAL_SHA256,
            'historical-original-digest-mismatch')
    prior, refused, owner = parse(policy_raw), parse(refusal_raw), owner_document(parse(owner_raw))
    historical(prior, refused)
    require(owner.get('kind') == 'sole-owner-documentary-policy-direction' and
            owner.get('trustModel', {}).get('independentHumanReviewerRequired') is False and
            owner.get('trustModel', {}).get('scope') and
            owner.get('declaredOwner', {}).get('name'), 'owner-policy-scope-missing')
    source_bindings = [binding(Path(path), role) for role, path in records]
    require(len(source_bindings) <= 32 and len({item['role'] for item in source_bindings}) == len(source_bindings),
            'duplicate-or-excess-evidence-role')
    rid = now.strftime('%Y%m%dt%H%M%Sz')
    policy = {**prior, 'recoveryId': rid, 'preparedAt': prepared,
              'policyExpiresAt': utc(now + timedelta(days=1)),
              'targetProposals': targets(rid), 'signed': False, 'operationalGate': 'closed',
              'capturedAt': None, 'acceptedAt': None}
    prior_ref = {'rid': HISTORICAL_RID, 'preparedAt': HISTORICAL_PREPARED,
                 'ceilingExclusive': HISTORICAL_CEILING, 'status': 'expired-permanently',
                 'sealedManifestSha256': HISTORICAL_SEALED_SHA256,
                 'originalVerifierExitCode': 1, 'originalVerifierRerun': False}
    prerequisite = {
        'kind': 'hexalith.fresh-offline-recovery-prerequisite', 'schemaVersion': 1,
        'rid': rid, 'preparedAt': prepared, 'expiresAtExclusive': policy['policyExpiresAt'],
        'operationalGate': 'closed', 'authorityAccepted': False, 'approvalGranted': False,
        'recoveryCurrent': False, 'timesheetsAcceptance': False,
        'preparationOnly': True, 'historicalCandidate': prior_ref,
        'documentaryPolicy': {'declaredOwner': owner['declaredOwner']['name'],
                              'review': 'owner-self-review',
                              'independentIdentityClaimed': False,
                              'scope': owner['trustModel']['scope']},
        'localRecords': source_bindings,
        'requiredEvidence': {key: {'value': None, 'status': 'not-established',
                                  'blocker': key} for key in GAPS},
        'operationalResults': dict.fromkeys(OUTPUTS),
        'protectedStatus': {'story36': 'in-progress', 'spec23': 'draft',
                            'story427': 'in-progress', 'story41OperationalGate': 'closed',
                            'release': 'FAIL'},
    }
    payloads = {
        'prior-policy.original.json': policy_raw, 'original-verifier-refusal.json': refusal_raw,
        'owner-documentary-policy.original.json': owner_raw,
        'policy.candidate.json': canonical(policy), 'prerequisite.json': canonical(prerequisite),
    }
    manifest = {'kind': 'hexalith.offline-prerequisite-manifest', 'schemaVersion': 1,
                'rid': rid, 'operationalGate': 'closed',
                'files': {name: {'bytes': len(raw), 'sha256': sha(raw)} for name, raw in payloads.items()}}
    manifest_raw = canonical(manifest)
    parent = Path(output_parent)
    destination = parent / rid
    parent_descriptor = open_directory(parent)
    try:
        os.mkdir(rid, mode=0o700, dir_fd=parent_descriptor)
        destination_descriptor = os.open(rid, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
                                         dir_fd=parent_descriptor)
        try:
            # A failed write leaves a retained partial attempt. There is no cleanup/retry.
            for name, raw in {**payloads, 'packet.json': manifest_raw}.items():
                with os.fdopen(os.open(name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW,
                                       0o600, dir_fd=destination_descriptor), 'wb') as stream:
                    stream.write(raw)
                    stream.flush()
                    os.fsync(stream.fileno())
            os.fsync(destination_descriptor)
        finally:
            os.close(destination_descriptor)
        os.fsync(parent_descriptor)
    finally:
        os.close(parent_descriptor)
    require(read(destination / 'packet.json') == manifest_raw, 'output-parent-changed')
    return {'status': 'prepared-unsigned-incomplete', 'path': str(destination),
            'packetSha256': sha(manifest_raw), 'rid': rid, 'preparedAt': prepared,
            'expiresAtExclusive': policy['policyExpiresAt'], 'operationalGate': 'closed'}


def verify(root, expected_sha256, *, now=None):
    """Check local consistency and freshness separately; never approve execution."""
    root = Path(root)
    require(isinstance(expected_sha256, str) and HEX.fullmatch(expected_sha256), 'invalid-manifest-digest')
    require(stat.S_IMODE(safe_path(root).st_mode) == 0o700, 'private-directory-required')
    manifest_raw = read(root / 'packet.json')
    require(sha(manifest_raw) == expected_sha256, 'manifest-digest-mismatch')
    manifest = object_value(parse(manifest_raw))
    require(manifest.get('kind') == 'hexalith.offline-prerequisite-manifest' and
            manifest.get('schemaVersion') == 1 and manifest.get('operationalGate') == 'closed', 'manifest-schema-mismatch')
    names = {'prior-policy.original.json', 'original-verifier-refusal.json',
             'owner-documentary-policy.original.json', 'policy.candidate.json', 'prerequisite.json'}
    require(set(object_value(manifest.get('files'))) == names and
            {path.name for path in root.iterdir()} == names | {'packet.json'}, 'packet-layout-mismatch')
    docs = {}
    for name in names:
        path = root / name
        require(stat.S_IMODE(safe_path(path).st_mode) == 0o600, 'private-file-required')
        raw = read(path)
        require(manifest['files'][name] == {'sha256': sha(raw), 'bytes': len(raw)}, 'payload-digest-mismatch')
        docs[name] = object_value(parse(raw))
    require(stat.S_IMODE(safe_path(root / 'packet.json').st_mode) == 0o600, 'private-file-required')
    prior, policy, packet = (docs[name] for name in ('prior-policy.original.json', 'policy.candidate.json', 'prerequisite.json'))
    require(manifest['files']['prior-policy.original.json']['sha256'] == ORIGINAL_POLICY_SHA256 and
            manifest['files']['original-verifier-refusal.json']['sha256'] == ORIGINAL_REFUSAL_SHA256,
            'historical-original-digest-mismatch')
    historical(prior, docs['original-verifier-refusal.json'])
    prepared = timestamp(packet['preparedAt'])
    ceiling = timestamp(packet['expiresAtExclusive'])
    rid = prepared.strftime('%Y%m%dt%H%M%Sz')
    require(root.name == manifest['rid'] == packet['rid'] == rid and rid != HISTORICAL_RID and
            prepared >= timestamp(HISTORICAL_CEILING) and ceiling == prepared + timedelta(days=1),
            'fresh-prerequisite-identity-mismatch')
    expected_policy = {**prior, 'recoveryId': rid, 'preparedAt': packet['preparedAt'],
                       'policyExpiresAt': packet['expiresAtExclusive'], 'targetProposals': targets(rid),
                       'signed': False, 'operationalGate': 'closed', 'capturedAt': None, 'acceptedAt': None}
    require(policy == expected_policy, 'retained-policy-or-fresh-targets-mismatch')
    require(packet.get('kind') == 'hexalith.fresh-offline-recovery-prerequisite' and
            packet.get('schemaVersion') == 1 and packet.get('preparationOnly') is True and
            packet.get('operationalGate') == 'closed' and
            all(packet.get(key) is False for key in ('authorityAccepted', 'approvalGranted', 'recoveryCurrent', 'timesheetsAcceptance')) and
            packet.get('operationalResults') == dict.fromkeys(OUTPUTS) and
            packet.get('requiredEvidence') == {key: {'value': None, 'status': 'not-established', 'blocker': key} for key in GAPS},
            'closed-prerequisite-required')
    owner = owner_document(docs['owner-documentary-policy.original.json'])
    require(packet.get('historicalCandidate') == {
                'rid': HISTORICAL_RID, 'preparedAt': HISTORICAL_PREPARED,
                'ceilingExclusive': HISTORICAL_CEILING, 'status': 'expired-permanently',
                'sealedManifestSha256': HISTORICAL_SEALED_SHA256,
                'originalVerifierExitCode': 1, 'originalVerifierRerun': False} and
            packet.get('documentaryPolicy') == {
                'declaredOwner': owner['declaredOwner']['name'], 'review': 'owner-self-review',
                'independentIdentityClaimed': False, 'scope': owner['trustModel']['scope']} and
            owner['trustModel']['independentHumanReviewerRequired'] is False and
            packet.get('protectedStatus') == {'story36': 'in-progress', 'spec23': 'draft',
                'story427': 'in-progress', 'story41OperationalGate': 'closed', 'release': 'FAIL'},
            'historical-or-documentary-claims-mismatch')
    require(isinstance(packet['localRecords'], list) and len(packet['localRecords']) <= 32 and
            all(isinstance(record, dict) and isinstance(record.get('role'), str) and
                isinstance(record.get('path'), str) for record in packet['localRecords']) and
            len({record['role'] for record in packet['localRecords']}) == len(packet['localRecords']),
            'duplicate-or-excess-evidence-role')
    for record in packet['localRecords']:
        require(record == binding(Path(record['path']), record['role']), 'bound-local-record-changed')
    observed = (now or datetime.now(timezone.utc)).astimezone(timezone.utc)
    require(observed >= prepared, 'future-preparation')
    # Payloads are rechecked after all external reads to detect concurrent drift.
    require(read(root / 'packet.json') == manifest_raw and
            all(sha(read(root / name)) == manifest['files'][name]['sha256'] for name in names),
            'packet-changed-during-verification')
    require(all(record == binding(Path(record['path']), record['role']) for record in packet['localRecords']),
            'bound-local-record-changed')
    return {'status': 'consistent-unsigned-incomplete', 'rid': rid,
            'candidateExpired': observed >= ceiling, 'operationalGate': 'closed',
            'missingEvidenceCount': len(GAPS), 'authorityAccepted': False,
            'approvalGranted': False, 'recoveryCurrent': False, 'timesheetsAcceptance': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='mode', required=True)
    create = sub.add_parser('prepare')
    create.add_argument('--output-parent', type=Path, required=True)
    create.add_argument('--prior-policy', type=Path, required=True)
    create.add_argument('--original-refusal', type=Path, required=True)
    create.add_argument('--owner-policy', type=Path, required=True)
    create.add_argument('--record', action='append', nargs=2, default=[], metavar=('ROLE', 'PATH'))
    inspect = sub.add_parser('verify')
    inspect.add_argument('--packet', type=Path, required=True)
    inspect.add_argument('--sha256', required=True)
    args = parser.parse_args()
    try:
        result = (prepare(args.output_parent, args.prior_policy, args.original_refusal,
                          args.owner_policy, args.record) if args.mode == 'prepare'
                  else verify(args.packet, args.sha256))
        print(json.dumps(result, sort_keys=True))
        return 0
    except (Refusal, OSError, KeyError, TypeError, ValueError):
        print(json.dumps({'status': 'refused', 'operationalGate': 'closed',
                          'reason': 'offline-prerequisite-input-or-custody-refusal'}))
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
