#!/usr/bin/env python3
"""Verify an unsigned, local Story 4.0 preparation packet. Never authorize work.

Only standard-library file reads and hashes are used. The caller must supply the
packet.json digest from a separately reviewed receipt. No contained source is
imported, evaluated, extracted or executed; no approval or signature is accepted.
"""

import argparse
import base64
from datetime import datetime, timedelta, timezone
import hashlib
import json
import math
import os
from pathlib import Path, PurePosixPath
import re
import stat


RECOVERY_ROOT = Path('/home/administrator/hexalith-recovery-evidence/4-0')
INVESTIGATION_ROOT = Path('/home/administrator/hexalith-retirement-investigation-evidence')
HISTORY_ROOTS = (
    RECOVERY_ROOT / '20261014t112353z',
    RECOVERY_ROOT / 'final-run-tools',
    INVESTIGATION_ROOT / '20261014t131927z-s427-private-oidc-correction',
    INVESTIGATION_ROOT / '20261014t144239z-s427-accepted-oidc-continuation',
    INVESTIGATION_ROOT / '20261015t140709z-s427-expired-recovery-preparation',
)
RID = re.compile(r'20[0-9]{6}t[0-9]{6}z')
HEX = re.compile(r'[0-9a-f]{64}')
MAX_JSON_BYTES = 8 * 1024 * 1024
MAX_DEPTH = 48
PUBLIC_RECIPIENT_SOURCE = Path('/home/administrator/.ssh/id_ed25519_git_signing.pub')
SIGNER_FINGERPRINT = 'SHA256:8XlNQvE3ucPf/e509wU4qtNgiyWA+TKmLei7F7+TCvk'
BASE_FILES = {
    'README.md', 'NEXT-OPERATIONAL-APPROVAL.md', 'policy/recovery-policy.candidate.json',
    'bindings.json', 'measurements.json', 'operation-plan.json',
    'preservation-inventory.json', 'source-freeze.json', 'SHA256SUMS',
    'trust/allowed_signers.txt', 'trust/recipient.pub',
}
REVISION_FILES = {'bindings-v2.json', 'SHA256SUMS-v2', 'README-v2.md', 'NEXT-OPERATIONAL-APPROVAL-v2.md'}
V3_FILES = {'bindings-v3.json', 'SHA256SUMS-v3', 'README-v3.md',
            'NEXT-OPERATIONAL-APPROVAL-v3.md', 'operation-plan-v3.json', 'static-transport-preflight.json',
            'FUTURE-MEASUREMENTS-v3.md'}
V4_FILES = {'bindings-v4.json', 'SHA256SUMS-v4', 'README-v4.md',
            'NEXT-OPERATIONAL-APPROVAL-v4.md', 'operation-plan-v4.json',
            'static-transport-preflight-v4.json', 'FUTURE-MEASUREMENTS-v4.md'}
# Independent reviewed receipts, never values supplied by a candidate packet.
REVIEWED_MANIFEST_SHA256 = {
    'packet.json': '3f4dc1e480c03413d44f3a8343e2730913aea220456c2dc4736a8df3eeebed2d',
    'packet-v2.json': '70a721ca7ced74281029c480419d08889658dc07f83844cdcefd10c7f8f99d59',
    'packet-v3.json': '5582ea912dcca2b2839f08236405747d6a54232d096d5ce25286c831dfed042a',
}
MEASUREMENTS = {
    'source-resource-uids', 'source-pvc-pv-bindings', 'source-incarnations-and-indices',
    'source-health-and-canaries', 'source-tool-and-ca-fingerprints',
    'provider-bucket-policy-and-retention', 'provider-writer-iam', 'provider-validator-iam',
    'provider-current-object-versions', 'native-authority-and-revocation-lineage',
    'ssh-host-trust-and-current-client', 'break-glass-custody-and-client-fingerprint',
    'target-image-digests-and-runtime-tools', 'target-capacity-and-storage-class',
    'target-runtime-and-isolation', 'target-uids-and-volume-identities',
    'current-memories-tenant-census', 'current-c3-and-drain',
    'current-oidc-live-reference', 'capture-and-restore-results',
    'immutable-validator-readbacks', 'cleanup-and-absence', 'signatures-and-acceptance',
}
ADAPTER_BLOCKERS = {
    'legacy-preparer-side-effects', 'fresh-create-only-adapters',
    'private-oidc-attempt-guards', 'generic-dispatch-not-an-allowlist',
    'memories-split-semantic-adapters', 'exact-reviewed-execution-and-signing-approval',
    'current-measurement-approval', 'tool-docker-custody',
}
LOCAL_ROLES = {'kubectl', 'age', 'etcd', 'etcdctl', 'etcdutl', 'ssh',
               'ssh-keygen', 'docker', 'tar', 'timeout', 'python'}
OIDC_CHECKS = {'clientCredentialsLoginSucceeded', 'tokenRoleClaimsEqualLive',
               'oidcDiscoveryAndJwksAllRealms', 'realmCountEqualsDb',
               'clientCountEqualsDb', 'roleCountEqualsDb', 'userCountEqualsDb',
               'realmNamesDigestEqualsDb'}
STAGES = ('measurement-approval', 'bounded-read-only-measurements',
          'revise-and-review-exact-packet', 'execution-and-signing-approval',
          'signed-policy-and-validator-readback', 'fresh-source-and-destination-preflight',
          'keycloak-capture-restore-private-oidc', 'openbao-capture-restore',
          'memories-final-pre-census-c3-drain', 'memories-paired-copy-restore-resume',
          'target-only-cleanup-and-absence', 'cleanup-signature-readback',
          'manifest-and-three-proof-signatures-readbacks', 'backup-gate-signature-readback',
          'final-non-dry-validation-m1-m2-signature-readback', 'independent-acceptance')


class Refusal(ValueError):
    """A sanitized offline refusal; never include input file contents."""


def require(condition, reason):
    if not condition:
        raise Refusal(reason)


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def relative(value):
    require(isinstance(value, str) and value and '\\' not in value,
            'invalid relative packet path')
    path = PurePosixPath(value)
    require(not path.is_absolute() and value == str(path)
            and all(p not in ('.', '..') for p in path.parts), 'packet path escape')
    return Path(value)


def path_info(path, private=False, allow_group_write=False):
    """Check every component before reading. Never resolve away a symlink."""
    path = Path(path)
    require(path.is_absolute() and '..' not in path.parts, 'absolute nonescaping path required')
    for item in (*reversed(path.parents), path):
        info = item.lstat()
        require(not stat.S_ISLNK(info.st_mode), 'symlink refused')
        require(info.st_uid in (0, os.getuid()), 'untrusted owner')
        mode = stat.S_IMODE(info.st_mode)
        sticky = stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and mode & stat.S_ISVTX
        require(not mode & 0o022 or sticky or (allow_group_write and not mode & 0o002
                and info.st_uid == 0), 'unsafe writable custody')
    if private:
        expected = 0o700 if stat.S_ISDIR(info.st_mode) else 0o600
        require(info.st_uid == os.getuid() and stat.S_IMODE(info.st_mode) == expected,
                'private custody must be directories0700/files0600')
    return info


def file_hash(path, allow_group_write=False, allow_hardlinks=False):
    info = path_info(path, allow_group_write=allow_group_write)
    require(stat.S_ISREG(info.st_mode) and (info.st_nlink == 1 or allow_hardlinks),
            'single-link regular file required')
    value = hashlib.sha256()
    # O_NOFOLLOW also protects the final component against replacement after lstat.
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, 'rb') as stream:
        opened = os.fstat(stream.fileno())
        require((opened.st_dev, opened.st_ino, opened.st_size, opened.st_mtime_ns)
                == (info.st_dev, info.st_ino, info.st_size, info.st_mtime_ns), 'input changed during read')
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
        end = os.fstat(stream.fileno())
    require((end.st_size, end.st_mtime_ns, end.st_ctime_ns)
            == (info.st_size, info.st_mtime_ns, info.st_ctime_ns), 'input changed during read')
    return value.hexdigest(), info.st_size


def read_bytes(path):
    info = path_info(path)
    require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1
            and info.st_size <= MAX_JSON_BYTES, 'bounded regular metadata required')
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, 'rb') as stream:
        opened = os.fstat(stream.fileno())
        require((opened.st_dev, opened.st_ino) == (info.st_dev, info.st_ino), 'metadata replaced')
        raw = stream.read(MAX_JSON_BYTES + 1)
        end = os.fstat(stream.fileno())
    require((end.st_size, end.st_mtime_ns, end.st_ctime_ns)
            == (info.st_size, info.st_mtime_ns, info.st_ctime_ns), 'metadata changed during read')
    require(len(raw) <= MAX_JSON_BYTES, 'metadata size limit exceeded')
    return raw


def parse_json(raw):
    depth, quoted, escaped = 0, False, False
    for c in raw:
        if quoted:
            if escaped:
                escaped = False
            elif c == 92:
                escaped = True
            elif c == 34:
                quoted = False
        elif c == 34:
            quoted = True
        elif c in (91, 123):
            depth += 1
            require(depth <= MAX_DEPTH, 'metadata nesting limit exceeded')
        elif c in (93, 125):
            depth -= 1

    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, 'duplicate JSON field')
            result[key] = value
        return result

    def finite(value):
        result = float(value)
        require(math.isfinite(result), 'nonfinite JSON refused')
        return result

    value = json.loads(raw, object_pairs_hook=pairs, parse_float=finite,
                       parse_constant=lambda _: require(False, 'nonfinite JSON refused'))
    require(isinstance(value, dict), 'JSON object required')
    return value


def timestamp(value):
    require(isinstance(value, str) and value.endswith('Z'), 'UTC timestamp required')
    result = datetime.fromisoformat(value[:-1] + '+00:00')
    require(result.utcoffset() == timedelta(0), 'UTC timestamp required')
    return result


def inventory(root):
    """Hash historical bytes without parsing or executing them."""
    path_info(root, private=True)
    result = {}
    for folder, directories, files in os.walk(root, followlinks=False):
        for name in sorted(directories + files):
            path = Path(folder) / name
            info = path_info(path)
            item = {'mode': stat.S_IMODE(info.st_mode), 'uid': info.st_uid,
                    'kind': 'directory' if stat.S_ISDIR(info.st_mode) else 'file'}
            if item['kind'] == 'file':
                require(not secret_path(path), 'secret material cannot be inventoried')
                item['sha256'], item['bytes'] = file_hash(path)
            result[str(path.relative_to(root))] = item
    return result


def secret_path(path):
    name = Path(path).name.lower()
    return name.endswith(('.env', '.key', '.pem')) or (name.startswith(('id_rsa', 'id_ed25519'))
            and not name.endswith('.pub')) or name in ('admin.conf', 'hexalith-production')


def bound_bytes(path, binding):
    raw = read_bytes(path)
    require(isinstance(binding, dict) and digest(raw) == binding['sha256']
            and len(raw) == binding['bytes'], 'semantic input checksum mismatch')
    return raw


def expected_historical_pins(load):
    """Reconstruct concrete historical expectations; never promote current claims."""
    prior = load(4, 'attempt-bindings-timesheets-reviewed.json')
    correction = load(2, 'bindings.json')
    return {
        'classification': 'historical-expectations-require-fresh-measurement',
        'currentIdentityAccepted': False,
        'apiEndpoint': prior['source']['apiEndpoint'], 'clusterUid': prior['source']['clusterUid'],
        'nodeName': prior['source']['nodeName'],
        'sourceTransportPathOnlyNotRead': prior['privatePaths']['retainedKubeSphereTransport'],
        'context': correction['context'],
        'transportAuthority': 'retained-KubeSphere-not-independent-native-proof',
        'correctionSourceIdentities': {k: v for k, v in correction['identities'].items() if k.startswith('source')},
        'sourcePreRecordsHistoricalOnly': {name: load(0, f'{name}/source-pre.json')
                                         for name in ('keycloak', 'openbao', 'memories')},
        'independentAuthorityHistoricalOnly': prior['authority'],
        'allNestedPriorAuthorityFieldsAreHistoricalEvenIfOldNamesLackPrefix': True,
    }


def check_inventory(root, files, manifest, expected_sha256, permitted):
    """Check the complete active layout, custody and bytes, including the anchor."""
    path_info(root, private=True)
    require(digest(read_bytes(root / manifest)) == expected_sha256, 'packet SHA-256 mismatch')
    actual, directories = set(), set()
    for folder, dirs, names in os.walk(root, followlinks=False):
        for name in dirs + names:
            path = Path(folder) / name
            info = path_info(path, private=True)
            if stat.S_ISDIR(info.st_mode):
                directories.add(str(path.relative_to(root)))
            else:
                require(stat.S_ISREG(info.st_mode), 'nonregular packet artifact')
                actual.add(str(path.relative_to(root)))
    require(actual == set(files) | {manifest}, 'missing or unexpected packet artifact')
    expected_dirs = {str(parent) for f in files for parent in relative(f).parents if str(parent) != '.'}
    require(directories == expected_dirs, 'unexpected packet directory or operational stage')
    for name, info in files.items():
        relative(name)
        require(name in permitted or (name.startswith('frozen/') and name.endswith('.txt')),
                'operational artifact or runnable source refused')
        require(isinstance(info, dict) and set(info) == {'sha256', 'bytes'}
                and isinstance(info['sha256'], str) and HEX.fullmatch(info['sha256'])
                and type(info['bytes']) is int, 'invalid file binding')
        require(file_hash(root / name) == (info['sha256'], info['bytes']), 'packet input checksum mismatch')
    return actual


def verify_packet(root, expected_sha256, manifest='packet.json'):
    require(isinstance(expected_sha256, str) and HEX.fullmatch(expected_sha256),
            'independent packet SHA-256 required')
    root = Path(root)
    require(manifest in ('packet.json', 'packet-v2.json', 'packet-v3.json', 'packet-v4.json'), 'fixed explicit manifest revision required')
    revision = manifest != 'packet.json'
    v4 = manifest == 'packet-v4.json'
    v3 = manifest in ('packet-v3.json', 'packet-v4.json')
    suffix = '-v4' if v4 else '-v3' if v3 else '-v2' if revision else ''
    binding_name = f'bindings{suffix}.json'
    checksum_name = f'SHA256SUMS{suffix}'
    require(root.parent == RECOVERY_ROOT and RID.fullmatch(root.name), 'fresh custody root or RID mismatch')
    require(root not in HISTORY_ROOTS, 'historical RID cannot be a fresh packet')
    path_info(root, private=True)
    raw = read_bytes(root / manifest)
    require(digest(raw) == expected_sha256, 'packet SHA-256 mismatch')
    packet = parse_json(raw)
    require(set(packet) == {'schemaVersion', 'kind', 'recoveryId', 'custodyDirectory',
                           'preparationOnly', 'operationalGate', 'files'}, 'packet fields mismatch')
    require(packet['schemaVersion'] == 1 and packet['kind'] == 'hexalith.offline-s40-packet'
            and packet['recoveryId'] == root.name and packet['custodyDirectory'] == str(root)
            and packet['preparationOnly'] is True and packet['operationalGate'] == 'closed',
            'preparation-only identity or gate mismatch')
    files = packet['files']
    required_files = BASE_FILES | REVISION_FILES | {'packet.json'} if revision else BASE_FILES
    if v3:
        required_files |= V3_FILES | {'packet-v2.json'}
    if v4:
        required_files |= V4_FILES | {'packet-v3.json'}
    require(isinstance(files, dict) and required_files <= set(files), 'required packet input missing')
    actual = check_inventory(root, files, manifest, expected_sha256, required_files)
    sums = ''.join(f"{info['sha256']}  {name}\n" for name, info in sorted(files.items()) if name != checksum_name)
    require(bound_bytes(root / checksum_name, files[checksum_name]).decode('utf-8') == sums, 'checksum inventory mismatch')

    load = lambda name: parse_json(bound_bytes(root / name, files[name]))
    retained_manifests = (['packet.json', 'packet-v2.json', 'packet-v3.json'] if v4
                          else ['packet.json', 'packet-v2.json'] if v3 else ['packet.json'] if revision else [])
    for name in retained_manifests:
        require(files[name]['sha256'] == REVIEWED_MANIFEST_SHA256[name], 'independent retained manifest anchor mismatch')
        retained_manifest = load(name)
        original_map = retained_manifest['files']
        require(isinstance(original_map, dict) and BASE_FILES <= set(original_map), 'retained manifest file map invalid')
        for original_name, original_binding in original_map.items():
            relative(original_name)
            require(original_name in files and files[original_name] == original_binding,
                    'retained original binding changed')
            require(file_hash(root / original_name) == (original_binding['sha256'], original_binding['bytes']),
                    'retained original bytes changed')
    preservation = load('preservation-inventory.json')
    require(set(preservation) == {'kind', 'recordedAt', 'roots', 'priorInventoryException'}
            and preservation['kind'] == 'historical-preservation-baseline', 'preservation metadata mismatch')
    require(isinstance(preservation['roots'], dict)
            and set(preservation['roots']) == {str(p) for p in HISTORY_ROOTS}, 'historical roots object invalid')
    count = 0
    for original, expected in preservation['roots'].items():
        require(isinstance(expected, dict), 'historical root entries invalid')
        require(inventory(Path(original)) == expected, 'historical inventory changed')
        count += sum(v['kind'] == 'file' for v in expected.values())

    def historical_load(index, name):
        root_path = HISTORY_ROOTS[index]
        return parse_json(bound_bytes(root_path / relative(name), preservation['roots'][str(root_path)][name]))
    freezes = load('source-freeze.json')
    require(set(freezes) == {'kind', 'sources'} and freezes['kind'] == 'inert-historical-source-freeze',
            'source freeze metadata mismatch')
    require(isinstance(freezes['sources'], list) and freezes['sources'], 'historical source freeze missing')
    frozen_paths = set()
    for item in freezes['sources']:
        require(set(item) == {'original', 'frozen', 'sha256', 'classification'}
                and item['classification'] == 'historical-reference-only-never-execute', 'source classification mismatch')
        origin = Path(item['original'])
        require(any(origin.is_relative_to(p) for p in HISTORY_ROOTS) and not secret_path(origin),
                'historical source escaped permitted inventory')
        target = str(relative(item['frozen']))
        require(target.startswith('frozen/') and target.endswith('.txt') and target not in frozen_paths,
                'invalid or duplicate inert source')
        frozen_paths.add(target)
        require(file_hash(origin)[0] == file_hash(root / target)[0] == item['sha256'], 'frozen source changed')
    require(frozen_paths == {f for f in files if f.startswith('frozen/')}, 'unbound frozen source')

    policy = load('policy/recovery-policy.candidate.json')
    require(set(policy) == {'kind', 'schemaVersion', 'recoveryId', 'preparedAt', 'policyExpiresAt',
                           'maximumPolicyAgeSeconds', 'capturedAt', 'acceptedAt', 'signed',
                           'operationalGate', 'retainedHistoricalPolicy', 'retainedDecisions',
                           'targetProposals', 'freshMeasurementsRequired'}, 'candidate policy fields mismatch')
    old_policy = historical_load(0, 'policy/recovery-policy.json')
    require(policy['kind'] == 'hexalith.unsigned-recovery-policy-candidate' and policy['schemaVersion'] == 1
            and policy['recoveryId'] == root.name and policy['signed'] is False
            and policy['operationalGate'] == 'closed' and policy['freshMeasurementsRequired'] is True
            and policy['capturedAt'] is None and policy['acceptedAt'] is None, 'fabricated policy completion')
    require(policy['maximumPolicyAgeSeconds'] == 86400
            and timestamp(policy['policyExpiresAt']) == timestamp(policy['preparedAt']) + timedelta(seconds=86400),
            'policy-age ceiling or preparation chronology changed')
    require(timestamp(policy['preparedAt']) <= datetime.now(timezone.utc), 'future preparation time')
    require(timestamp(policy['preparedAt']).strftime('%Y%m%dt%H%M%Sz') == root.name,
            'RID and policy preparation time mismatch')
    require(policy['retainedHistoricalPolicy'] == {
        'recoveryId': '20261014t112353z', 'classification': 'expired-historical-decisions-only',
        'sha256': file_hash(HISTORY_ROOTS[0] / 'policy/recovery-policy.json')[0],
        'preparedAt': old_policy['preparedAt'], 'expiresAt': '2026-10-15T11:40:42Z'}, 'historical policy relabeled')
    retained = {k: old_policy[k] for k in ('approvalSources', 'cleanupProcedure', 'definitions',
                'destination', 'signing', 'systems', 'validation')}
    require(policy['retainedDecisions'] == retained, 'retained policy decision changed')
    targets = policy['targetProposals']
    require(set(targets) == {'keycloakPretestNamespace', 'keycloakNamespace', 'keycloakCluster',
                            'keycloakApplication', 'openbaoNamespace', 'memoriesNamespace', 'evidencePrefix',
                            'payloadPrefixes', 'created', 'uids'}, 'target proposal fields mismatch')
    expected_names = {'keycloakPretestNamespace': 's40-kcpre-', 'keycloakNamespace': 's40-kc-',
                      'keycloakCluster': 's40-postgres-', 'keycloakApplication': 's40-oidc-',
                      'openbaoNamespace': 's40-bao-', 'memoriesNamespace': 's40-mem-'}
    require(all(targets[k] == prefix + root.name for k, prefix in expected_names.items())
            and targets['evidencePrefix'] == f'evidence/4-0/{root.name}/'
            and targets['payloadPrefixes'] == {s: f'{s}/{root.name}/' for s in ('keycloak', 'openbao', 'memories')}
            and targets['created'] is False and targets['uids'] is None, 'target identity collision or fabricated target')

    measurements = load('measurements.json')
    require(set(measurements) == MEASUREMENTS, 'required current measurement omitted')
    for name, value in measurements.items():
        require(value == {'value': None, 'blocker': name, 'status': 'not-measured-closed'},
                'current measurement fabricated or historical observation promoted')
    bindings = load(binding_name)
    binding_fields = {'kind', 'recoveryId', 'operationalGate', 'blockers', 'historicalSourcePins',
            'localTools', 'publicTrust', 'safeguards', 'operationalResults', 'protectedStates',
            'offlineVerifierSources'}
    if v3:
        binding_fields |= {'retainedManifests', 'activeApproval', 'staticTransportPreflight', 'measurementLanes'}
    require(set(bindings) == binding_fields, 'binding fields mismatch')
    require(bindings['kind'] == 'hexalith.offline-s40-bindings' and bindings['recoveryId'] == root.name
            and bindings['operationalGate'] == 'closed', 'binding gate or identity mismatch')
    blockers = bindings['blockers']
    require(isinstance(blockers, dict) and set(blockers) == MEASUREMENTS | ADAPTER_BLOCKERS
            and all(isinstance(v, str) and v.strip() for v in blockers.values()), 'named closed blockers missing')
    require(bindings['operationalResults'] == {'capture': None, 'restore': None, 'publication': None,
            'cleanup': None, 'signatures': None, 'acceptance': None}, 'fabricated completed operational stage')
    require(bindings['protectedStates'] == {'story3_6': 'in-progress', 'epic3': 'in-progress',
            'story4_27': 'in-progress', 'story4_1Gate': 'closed', 'spec23': 'draft', 'release': 'FAIL'},
            'downstream state incorrectly opened')
    pins = bindings['historicalSourcePins']
    require(pins == expected_historical_pins(historical_load), 'historical pins incomplete or promoted')
    tools = bindings['localTools']
    require(isinstance(tools, dict) and set(tools) == LOCAL_ROLES, 'local tool role omitted')
    for role, item in tools.items():
        require(set(item) == {'path', 'sha256', 'bytes', 'mode', 'links', 'executionQualified', 'custodyAccepted', 'blocker'}
                and item['executionQualified'] is False, 'tool inventory or qualification mismatch')
        path = Path(item['path'])
        require(not secret_path(path), 'credential cannot be a local tool')
        info = path_info(path, allow_group_write=role == 'docker')
        require(file_hash(path, allow_group_write=role == 'docker', allow_hardlinks=True)
                == (item['sha256'], item['bytes']) and stat.S_IMODE(info.st_mode) == item['mode']
                and info.st_nlink == item['links'], 'local tool drift')
        require(item['custodyAccepted'] is (role != 'docker')
                and item['blocker'] == ('tool-docker-custody' if role == 'docker' else None),
                'unsafe Docker custody silently accepted')
    verifier_sources = bindings['offlineVerifierSources']
    source_roles = [('verifier', 'verify_packet.py'), ('tests', 'test_verify_packet.py'), ('readme', 'README.md')]
    if v3:
        source_roles += [('staticPreflight', 'static_transport_preflight.py'),
                         ('staticPreflightTests', 'test_static_transport_preflight.py')]
    require(set(verifier_sources) == {r for r, _ in source_roles}, 'offline source role omitted')
    for role, name in source_roles:
        item = verifier_sources[role]
        expected_path = Path(__file__).absolute().with_name(name)
        require(set(item) == {'path', 'sha256', 'bytes'} and item['path'] == str(expected_path)
                and file_hash(expected_path) == (item['sha256'], item['bytes']), 'offline verifier source drift')
    trust = bindings['publicTrust']
    require(set(trust) == {'allowedSignersSha256', 'recipientSha256', 'recipientFingerprint',
                           'signingPrincipal', 'namespace', 'authenticationPerformed', 'decryptionPerformed'},
            'trust binding fields mismatch')
    require(trust['allowedSignersSha256'] == file_hash(root / 'trust/allowed_signers.txt')[0]
            == file_hash(HISTORY_ROOTS[0] / 'signatures/allowed_signers')[0]
            and trust['recipientSha256'] == file_hash(root / 'trust/recipient.pub')[0]
            == file_hash(PUBLIC_RECIPIENT_SOURCE)[0]
            and trust['recipientFingerprint'] == SIGNER_FINGERPRINT
            and trust['signingPrincipal'] == 'jpiquot@itaneo.com' and trust['namespace'] == 'hexalith-recovery'
            and trust['authenticationPerformed'] is False and trust['decryptionPerformed'] is False,
            'public trust or no-authentication boundary mismatch')
    public_parts = bound_bytes(root / 'trust/recipient.pub', files['trust/recipient.pub']).decode('ascii').split()
    require(len(public_parts) >= 2 and public_parts[0] == 'ssh-ed25519', 'public recipient type mismatch')
    fingerprint = 'SHA256:' + base64.b64encode(hashlib.sha256(
        base64.b64decode(public_parts[1], validate=True)).digest()).decode('ascii').rstrip('=')
    require(fingerprint == SIGNER_FINGERPRINT, 'public recipient fingerprint mismatch')
    safeguards = bindings['safeguards']
    require(safeguards['oidc'] == {'sourceEndpoint': 'http://127.0.0.1:38080',
            'targetEndpoint': 'http://127.0.0.1:28080', 'proxiesEnabled': False, 'redirectsEnabled': False,
            'requiredChecks': sorted(OIDC_CHECKS), 'failedOriginalSha256':
            '014463ef76fd99431f6c268edbc41274b5031157f683a1630011956cb5738578',
            'oldGuardScope': 'old-attempt-only-requires-new-reviewed-adapter'}, 'OIDC safeguards weakened')
    require(safeguards['memories'] == {'actualC3Required': True, 'oneFinalPreAndCensus': True,
            'maximumDrainSeconds': 900, 'pairedStores': ['redis-stack', 'falkordb'],
            'splitSemanticWrapperRequired': True, 'immediateConditionalResumeRequired': True,
            'resumeRequiresApprovedScopeRestoreProofAndCleanDrain': True,
            'historicalZeroTenantCountIsNotCurrentEvidence': True}, 'Memories safeguards weakened')
    plan = load(f'operation-plan{suffix}.json' if v3 else 'operation-plan.json')
    plan_fields = {'kind', 'inert', 'operationalGate', 'stages'} | ({'activeApproval'} if v3 else set())
    require(set(plan) == plan_fields
            and plan['kind'] == 'hexalith.inert-s40-operation-plan' and plan['inert'] is True
            and plan['operationalGate'] == 'closed' and len(plan['stages']) == len(STAGES), 'inert operation plan mismatch')
    for index, (name, stage) in enumerate(zip(STAGES, plan['stages'])):
        require(set(stage) == {'name', 'status', 'dispatchAllowed', 'requires', 'description'}
                and stage['name'] == name and stage['status'] == 'not-run'
                and stage['dispatchAllowed'] is False and isinstance(stage['description'], str)
                and stage['description'] and stage['requires'] == (list(STAGES[index-1:index]) if index else []),
                'completed or dispatchable stage refused')
    if v3:
        approval_name = f'NEXT-OPERATIONAL-APPROVAL{suffix}.md'
        approval = {'filename': approval_name, **files[approval_name]}
        require(bindings['activeApproval'] == plan['activeApproval'] == approval, 'active approval binding mismatch')
        require(bindings['retainedManifests'] == {n: files[n] for n in retained_manifests},
                'retained manifest chain mismatch')
        preflight_name = 'static-transport-preflight-v4.json' if v4 else 'static-transport-preflight.json'
        require(bindings['staticTransportPreflight'] == {'filename': preflight_name,
                **files[preflight_name]}, 'static preflight binding mismatch')
        def tool_ref(role):
            return {k: tools[role][k] for k in ('path', 'sha256', 'bytes')}
        config_path = pins['sourceTransportPathOnlyNotRead']
        require(load(preflight_name) == {
            'kind': 'hexalith.local-static-transport-preflight', 'schemaVersion': 1,
            'kubeconfig': {'path': config_path,
                'sha256': historical_load(2, 'bindings.json')['files'][config_path],
                'context': pins['context'], 'endpoint': pins['apiEndpoint']},
            'kubectl': tool_ref('kubectl'), 'interpreter': tool_ref('python'),
            'source': verifier_sources['staticPreflight'],
            'limits': {'maximumConfigBytes': 1048576, 'maximumOutputBytes': 2097152,
                'timeoutSeconds': 10, 'networkCallsAllowed': False, 'authenticationAllowed': False}},
            'static transport exact scope mismatch')
        require(bindings['measurementLanes'] == {'localStaticTransport': 'requires-Administrator-protected-local-approval',
                'nativeApi': 'BLOCKED-static-transport-and-exact-adapter-unqualified',
                'provider': 'BLOCKED-exact-adapter-runtime-and-authority-unqualified',
                'iam': 'BLOCKED-exact-adapter-runtime-and-authority-unqualified'}, 'measurement lanes opened')
    for original, expected in preservation['roots'].items():
        require(inventory(Path(original)) == expected, 'historical inventory changed')
    check_inventory(root, files, manifest, expected_sha256, required_files)
    return {'result': 'offline-preparation-pass', 'operationalGate': 'closed',
            'approvalAuthenticated': False, 'currentRecoveryClaimed': False,
            'recoveryId': root.name, 'manifest': manifest, 'packetSha256': expected_sha256,
            'historicalFilesUnchanged': count, 'frozenSourcesVerified': len(frozen_paths),
            'packetFilesVerified': len(actual), 'closedBlockers': sorted(blockers)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packet', required=True, help='fresh private RID directory')
    parser.add_argument('--sha256', required=True, help='independently reviewed packet.json digest')
    parser.add_argument('--manifest', choices=('packet.json', 'packet-v2.json', 'packet-v3.json', 'packet-v4.json'), default='packet.json',
                        help='explicit manifest revision; no latest or automatic fallback')
    args = parser.parse_args()
    try:
        result = verify_packet(Path(args.packet), args.sha256, args.manifest)
        code = 0
    except (Refusal, OSError, ValueError, TypeError, KeyError, RecursionError, AttributeError, IndexError, OverflowError) as error:
        result = {'result': 'offline-preparation-refused', 'operationalGate': 'closed',
                  'approvalAuthenticated': False, 'currentRecoveryClaimed': False,
                  'reason': str(error) if isinstance(error, Refusal) else 'invalid or unavailable local input'}
        code = 1
    print(json.dumps(result, indent=2, sort_keys=True))
    return code


if __name__ == '__main__':
    raise SystemExit(main())
