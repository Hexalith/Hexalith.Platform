#!/usr/bin/env python3
"""Offline local retirement preparation. Never dispatch, sign, or authorize operations."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import tempfile
import zipfile

import retire
from evidence import recipient_stanzas

ARTIFACTS = Path('_bmad-output/implementation-artifacts')
SOURCE_FILES = (
    *(Path('eng/cluster-management') / name for name in (*retire.CODE_FILES,
      'RETIRE-KUBESPHERE.md', 'prepare_retirement.py', 'test_prepare_retirement.py',
      'STAGING-PREREQUISITES.md', 'README.md')),
    Path('eng/kubernetes-upgrade/MAINTENANCE.md'),
    Path('eng/kubernetes-upgrade/prepare.py'), Path('eng/kubernetes-upgrade/README.md'),
    Path('eng/timesheets-deployment/staging-dependency-handoff.md'),
    ARTIFACTS / 'epic-4-context.md',
    ARTIFACTS / '4-27-retire-kubesphere-without-changing-workload-data.md',
    ARTIFACTS / 'spec-4-27-retire-kubesphere-without-changing-workload-data.md',
    ARTIFACTS / 'evidence/epic-4/4-27/20261006t091552z-s427-resume-verification/verification.json',
)
METADATA_NAMES = set(Path(name).name for name in (*retire.BACKUP_FILES, *retire.GATE_FILES)) | {
    'retirement-policy.json', 'retirement-approval.json', 'exact-execution-approval.json'}
CLAIM_FIELDS = ('kind', 'attemptId', 'state', 'status', 'capturedAt', 'validatedAt', 'expiresAt',
                'signed', 'verificationResult', 'mutationAuthorized', 'gateOpen', 'accepted',
                'independentOffNodeReadback', 'independentReadbackVerified', 'planSha256')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return (json.dumps(value, sort_keys=True, indent=2, allow_nan=False) + '\n').encode()


def json_object(data):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, 'duplicate-json-key')
            result[key] = value
        return result
    value = json.loads(data, object_pairs_hook=unique,
                       parse_constant=lambda value: (_ for _ in ()).throw(ValueError('nonfinite-json')))
    require(isinstance(value, dict), 'json-object-required')
    return value


def safe_path(path):
    path = Path(path)
    require(path.is_absolute() and '..' not in path.parts, 'explicit-absolute-local-path-required')
    require(not any(p.is_symlink() for p in (path, *path.parents)), 'symlink-input-or-custody')
    return path


def private_directory(path):
    path = safe_path(path)
    require(path.is_dir(), 'missing-private-directory')
    mode = path.stat()
    require(mode.st_uid == os.getuid() and stat.S_IMODE(mode.st_mode) == 0o700,
            'owner-only-directory-required')
    return path


def read_source(path, private=False):
    path = safe_path(path)
    with os.fdopen(os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK), 'rb') as source:
        before = os.fstat(source.fileno())
        require(stat.S_ISREG(before.st_mode), 'regular-local-source-required')
        if private:
            require(before.st_uid == os.getuid() and stat.S_IMODE(before.st_mode) == 0o600,
                    'owner-only-input-required')
        data = source.read()
        after = os.fstat(source.fileno())
    require((before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns, before.st_ctime_ns) ==
            (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns, after.st_ctime_ns),
            'source-changed-during-read')
    require(path.stat().st_ino == after.st_ino, 'source-replaced-during-read')
    return data


def add_source(sources, name, path, private=False):
    require(name not in sources, 'duplicate-source-archive-member')
    data = read_source(path, private)
    sources[name] = (Path(path), data)
    return data


def vcs_provenance(project, sources):
    """Read local refs as provenance only, without running Git or changing its index."""
    git = project / '.git'
    if not git.exists():
        return {'state': 'unavailable', 'revision': None}
    require(git.is_dir() and not git.is_symlink(), 'explicit-local-git-directory-required')
    head = add_source(sources, 'vcs/HEAD', git / 'HEAD').decode('ascii').strip()
    if head.startswith('ref: '):
        reference = head[5:]
        require(re.fullmatch(r'refs/heads/[A-Za-z0-9._/-]+', reference) is not None
                and '..' not in reference.split('/'), 'invalid-local-git-ref')
        path = git / reference
        if path.exists():
            revision = add_source(sources, 'vcs/current-ref', path).decode('ascii').strip()
        else:
            packed = add_source(sources, 'vcs/packed-refs', git / 'packed-refs').decode('ascii')
            matches = [line.split(' ', 1)[0] for line in packed.splitlines() if line.endswith(' ' + reference)]
            require(len(matches) == 1, 'ambiguous-local-git-ref')
            revision = matches[0]
    else:
        revision = head
    require(re.fullmatch(r'[0-9a-f]{40}|[0-9a-f]{64}', revision) is not None, 'invalid-local-vcs-revision')
    return {'state': 'measured-local-ref', 'revision': revision,
            'scope': 'local reference bytes only; working sources archived separately; no commit/staging performed'}


def verify_manifest(root, sources, label):
    root = private_directory(root)
    data = add_source(sources, f'rehearsals/{label}/SHA256SUMS', root / 'SHA256SUMS', True)
    sums = {}
    for line in data.decode('utf-8').splitlines():
        require(re.fullmatch(r'[0-9a-f]{64}  [A-Za-z0-9][A-Za-z0-9._-]*', line) is not None,
                'invalid-private-manifest')
        digest, name = line.split('  ', 1)
        require(name not in sums and name != 'SHA256SUMS', 'duplicate-private-manifest-member')
        content = add_source(sources, f'rehearsals/{label}/{name}', root / name, True)
        require(sha(content) == digest, 'private-manifest-checksum-mismatch')
        if name.endswith('.json'):
            json_object(content)
        sums[name] = digest
    require(bool(sums), 'empty-private-manifest')
    exports = json_object(sources[f'rehearsals/{label}/encrypted-exports.json'][1])
    private = json_object(sources[f'rehearsals/{label}/private-export-digests.json'][1])
    require(exports.get('readbackVerified') is True and isinstance(exports.get('exports'), list)
            and exports['exports'], 'missing-retained-readback-records')
    require(isinstance(private.get('exports'), list), 'missing-private-export-lineage')
    private_by_name = {v['file']: v for v in private['exports']}
    require(len(private_by_name) == len(private['exports']), 'duplicate-private-export')
    seen = set()
    for export in exports['exports']:
        name = export['file']
        require(name not in seen and name in sums and name.endswith('.age'), 'invalid-retained-export')
        seen.add(name)
        cipher = sources[f'rehearsals/{label}/{name}'][1]
        stanzas = recipient_stanzas(cipher)
        retained = private_by_name.get(name, {})
        require(export.get('readbackVerified') is True and retained.get('readbackVerified') is True
                and export.get('ciphertextSha256') == retained.get('ciphertextSha256') == sums[name]
                and export.get('ciphertextBytes') == retained.get('ciphertextBytes') == len(cipher)
                and stanzas == export.get('recipientStanzas') == retained.get('recipientStanzas')
                and len(stanzas) == 2 and all(v.get('type') == 'ssh-ed25519' for v in stanzas)
                and len({v.get('tag') for v in stanzas}) == 2
                and re.fullmatch(r'[0-9a-f]{64}', retained.get('plaintextSha256', '')) is not None,
                'retained-two-recipient-readback-lineage-invalid')
    require(seen == set(private_by_name) == {name for name in sums if name.endswith('.age')},
            'retained-export-set-mismatch')
    return {'checksummedFileCount': len(sums), 'encryptedExportCount': len(seen),
            'manifestSha256': sha(data), 'allManifestChecksumsMatched': True,
            'retainedTwoRecipientReceiptIntegrityMeasured': True,
            'freshDecryptionPerformed': False, 'independentOffNodeAcceptance': False}


def metadata_audit(roots, clock):
    def traversal_error(error):
        raise error

    def identity(path):
        observed = safe_path(path).stat()
        return {'device': observed.st_dev, 'inode': observed.st_ino,
                'uid': observed.st_uid, 'mode': stat.S_IMODE(observed.st_mode),
                'size': observed.st_size, 'mtimeNs': observed.st_mtime_ns, 'ctimeNs': observed.st_ctime_ns}

    audit = {'inspectedRoots': [], 'exactRecordNames': sorted(METADATA_NAMES),
             'scope': 'recursive exact-name local metadata and adjacent .sig bytes only; no raw recovery payloads',
             'cryptographicSignatureValidation': 'not-performed', 'records': [], 'skippedSymlinks': [],
             'claimsTrusted': False, 'liveGateOpen': False,
             'originalRetention': 'existing private source custody; metadata originals and signatures are not copied into the preparation archive'}
    for root in roots:
        root = private_directory(root)
        audit['inspectedRoots'].append(str(root))
        for directory, children, files in os.walk(root, followlinks=False, onerror=traversal_error):
            children.sort()
            for name in list(children):
                path = Path(directory) / name
                if path.is_symlink():
                    children.remove(name)
                    audit['skippedSymlinks'].append(str(path))
            for name in sorted(set(files) & METADATA_NAMES):
                path = Path(directory) / name
                if path.is_symlink():
                    audit['skippedSymlinks'].append(str(path))
                    continue
                private_directory(Path(directory))
                data = read_source(path, True)
                measured = {'path': str(path), 'sha256': sha(data), 'bytes': len(data),
                            'sourceIdentity': identity(path),
                            'signaturePresent': False, 'signatureValidated': False,
                            'archived': False, 'retainedAtExistingPrivateSource': True}
                signature = path.with_name(name + '.sig')
                if signature.exists() or signature.is_symlink():
                    signature_data = read_source(signature, True)
                    measured.update(signaturePresent=True, signaturePath=str(signature),
                                    signatureSha256=sha(signature_data), signatureIdentity=identity(signature))
                try:
                    record = json_object(data)
                    claims = {key: record[key] for key in CLAIM_FIELDS if key in record}
                    expiry = retire.timestamp(record['expiresAt']) if 'expiresAt' in record else None
                    measured['expiredAccordingToUntrustedTimestamp'] = clock >= expiry if expiry else None
                    measured['untrustedRecordClaims'] = claims
                except (ValueError, TypeError, KeyError, OverflowError):
                    measured['metadataParseState'] = 'malformed-untrusted-record'
                audit['records'].append(measured)
    audit['exactNamedRecordCounts'] = {name: sum(Path(v['path']).name == name for v in audit['records'])
                                     for name in sorted(METADATA_NAMES)}
    return audit


def custody_ancestry(root):
    """Sticky shared parents are safe; other writable or untrusted ancestors are refused."""
    for parent in (root, *root.parents):
        if parent.exists():
            observed = parent.stat()
            require(parent.is_dir(), 'custody-parent-not-directory')
            require(observed.st_uid in (0, os.getuid())
                    and (not stat.S_IMODE(observed.st_mode) & 0o022 or observed.st_mode & stat.S_ISVTX),
                    'writable-or-unowned-custody-ancestor')


def validate_destination(root, project, protected, attempt_id):
    root = safe_path(root)
    require(re.fullmatch(r'[a-z0-9][a-z0-9-]{0,119}', attempt_id) is not None, 'invalid-preparation-id')
    for blocked in (project, *protected):
        blocked = safe_path(blocked)
        require(root != blocked and blocked not in root.parents and root not in blocked.parents,
                'repository-recovery-or-input-output-overlap')
    require(not any((parent / '.git').exists() for parent in (root, *root.parents)),
            'repository-output-refused')
    # Existing custody directories must already be safe; never chmod another owner's evidence.
    for path in (root, root / attempt_id):
        safe_path(path)
    require(not (root / attempt_id).exists()
            and not any(root.glob(f'.{attempt_id}.incomplete-*')), 'existing-preparation-id-refused')
    custody_ancestry(root)
    if root.exists():
        private_directory(root)
    return root / attempt_id


def write_new(path, data):
    with os.fdopen(os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600), 'wb') as out:
        out.write(data)


def verify_packet(directory, serialized):
    private_directory(directory)
    expected_names = {*serialized, 'preparation-sources.zip', 'SHA256SUMS'}
    require({path.name for path in directory.iterdir()} == expected_names, 'packet-member-set-mismatch')
    for name, data in serialized.items():
        require(read_source(directory / name, True) == data, 'packet-record-write-mismatch')
    expected_sums = ''.join(f'{sha(read_source(directory / name, True))}  {name}\n'
                            for name in sorted(expected_names - {'SHA256SUMS'})).encode()
    require(read_source(directory / 'SHA256SUMS', True) == expected_sums, 'packet-checksum-mismatch')


def prepare(*, project_root, evidence_root, attempt_id, operator, handoff, inventory,
            catalog, namespaces, recovery_roots):
    project = safe_path(project_root)
    roots = [safe_path(v) for v in recovery_roots]
    require(len(roots) == 3 and len(set(roots)) == 3, 'three-distinct-explicit-metadata-roots-required')
    require(isinstance(operator, str) and operator.strip(), 'preparation-operator-required')
    destination = validate_destination(evidence_root, project, [*roots, Path(handoff).parent,
                                      Path(inventory).parent, catalog, namespaces], attempt_id)
    sources = {}
    for relative in SOURCE_FILES:
        add_source(sources, 'project/' + relative.as_posix(), project / relative)
    require(sources['project/eng/cluster-management/prepare_retirement.py'][1]
            == read_source(Path(__file__).absolute()), 'executing-preparer-source-mismatch')
    vcs = vcs_provenance(project, sources)
    handoff_bytes = add_source(sources, 'inputs/external-staging-dependency-handoff.md', handoff)
    require(handoff_bytes.decode('utf-8').strip().startswith('# Timesheets staging dependency handoff'),
            'malformed-required-handoff')
    inventory_bytes = add_source(sources, 'inputs/historical-inventory.json', inventory)
    source = json_object(inventory_bytes)
    resources = source.get('resources')
    require(source.get('context') == 'jpiquot@local' and source.get('sourceClusterUid')
            and isinstance(resources, list) and resources, 'malformed-required-historical-inventory')
    require(all(isinstance(v, dict) and all(v.get(k) for k in ('uid', 'resourceVersion', 'kind', 'apiVersion', 'name'))
                for v in resources), 'invalid-historical-resource-identity')
    require(len({v['uid'] for v in resources}) == len(resources), 'duplicate-historical-uid')
    clock = datetime.now(timezone.utc)
    retire.timestamp(source.get('capturedAt'))
    binding = retire.code_binding(project)
    for name, digest in binding['codeSha256'].items():
        require(sha(sources[f'project/eng/cluster-management/{name}'][1]) == digest, 'code-changed-during-preparation')
    require(sha(sources['project/eng/cluster-management/RETIRE-KUBESPHERE.md'][1]) == binding['procedureSha256']
            and sha(sources['project/eng/kubernetes-upgrade/MAINTENANCE.md'][1]) == binding['maintenanceSha256'],
            'bound-procedure-changed-during-preparation')
    audit_source = json_object(sources['project/' + str(SOURCE_FILES[-1])][1])
    require(audit_source.get('currentBytes') == binding, 'historical-execution-binding-changed')
    manifests = {name: verify_manifest(root, sources, name)
                 for name, root in (('catalog', catalog), ('namespaces', namespaces))}
    receipts = retire.validate_rehearsals({**binding, 'resources': resources,
                      'rehearsalSourceInventorySha256': sha(inventory_bytes)},
                      {'catalog': catalog, 'namespaces': namespaces})
    for label, receipt in receipts.items():
        require(receipt['checksumsSha256'] == sha(sources[f'rehearsals/{label}/SHA256SUMS'][1])
                and receipt['attemptId'] == json_object(sources[f'rehearsals/{label}/attempt.json'][1])['attemptId'],
                'validator-receipt-archive-mismatch')
        for name, digest in receipt['records'].items():
            require(f'rehearsals/{label}/{name}' in sources
                    and digest == sha(sources[f'rehearsals/{label}/{name}'][1]),
                    'validator-receipt-archive-mismatch')
    audit = metadata_audit(roots, clock)
    lineage = [{'archiveMember': name, 'originalPath': str(path), 'sha256': sha(data), 'bytes': len(data)}
               for name, (path, data) in sorted(sources.items())]
    # Recheck all measured inputs before allocating any output; never mix replaced source bytes.
    for directory in (catalog, namespaces, *roots):
        private_directory(directory)
    for name, (path, data) in sources.items():
        require(sha(read_source(path, private=name.startswith('rehearsals/'))) == sha(data),
                'input-changed-before-publication')
    require(metadata_audit(roots, clock) == audit, 'metadata-scope-or-identity-changed-before-publication')
    common = {'schemaVersion': 1, 'attemptId': attempt_id, 'signed': False,
              'mutationAuthorized': False, 'liveGateOpen': False, 'liveEvidenceCollected': False,
              'operationalState': 'not-run', 'remoteCalls': 0, 'signingCalls': 0, 'mutationRequests': 0}
    records = {
        'attempt.json': {**common, 'kind': 'offline-retirement-preparation', 'operator': operator,
            'recordedAt': clock.strftime('%Y-%m-%dT%H:%M:%SZ'), 'context': 'jpiquot@local',
            'externalHandoffSha256': sha(handoff_bytes), 'historicalInventorySha256': sha(inventory_bytes),
            'executionBinding': binding, 'sourceArchive': 'preparation-sources.zip',
            'vcsProvenance': vcs,
            'sourceArchiveSha256': '0' * 64, 'preparationComplete': True},
        'input-lineage.json': {**common, 'sources': lineage,
            'retainedMetadata': [{key: value for key, value in record.items() if key != 'untrustedRecordClaims'}
                                 for record in audit['records']]},
        'historical-integrity.json': {**common, 'measuredExecutionBindingMatchesHistoricalAudit': True,
            'actualPureRehearsalValidatorAccepted': True, 'privateManifests': manifests,
            'historicalReceipts': receipts, 'fixturesRun': False,
            'scope': 'local checksums and retained receipt structure; no decryption, live observation, source qualification or off-node acceptance'},
        'metadata-audit.json': {**common, **audit},
        'closed-gates.json': {**common, 'story427Status': 'in-progress', 'story41GateOpen': False,
            'timesheetsStory36Status': 'in-progress', 'timesheetsAcceptance': 'FAIL',
            'sourceClusterUid': None, 'currentServerVersion': None, 'freshPlanSha256': None,
            'approvalSha256': None, 'recoveryAccepted': None, 'consoleClosureAccepted': None,
            'nativeAccessAccepted': None, 'authenticatedHealthAccepted': None,
            'operationalCommands': [], 'retirementAccepted': None,
            'unselectedOperatorInputs': ['approved trust root and Administrator principal',
                'private static native configuration', 'Administrator and distinct readback recipients and identity',
                'approved receipt signing key and signing scope', 'current approved freshness policy',
                'fresh signed 4.0/4.1 proofs and independent off-node readback',
                'accepted 4.2 closure and private native access/authenticated health',
                'fresh census, renewed source-bound fixtures, second fresh plan',
                'exact future execution/outage/owner/window/stop/recovery approval']},
    }
    # Validate every record before allocating custody, including untrusted metadata claims.
    serialized = {name: canonical(record) for name, record in records.items()}
    validate_destination(evidence_root, project, [*roots, Path(handoff).parent,
                         Path(inventory).parent, catalog, namespaces], attempt_id)
    missing = []
    cursor = Path(evidence_root)
    while not cursor.exists():
        missing.append(cursor)
        cursor = cursor.parent
    for path in reversed(missing):
        path.mkdir(mode=0o700)
    private_directory(evidence_root)
    # Failed construction remains in distinctly incomplete custody and burns its ID.
    staging = Path(tempfile.mkdtemp(prefix=f'.{attempt_id}.incomplete-', dir=evidence_root))
    archive = staging / 'preparation-sources.zip'
    with os.fdopen(os.open(archive, os.O_RDWR | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600), 'w+b') as stream:
        with zipfile.ZipFile(stream, 'w', compression=zipfile.ZIP_STORED) as output:
            for name, (_, data) in sorted(sources.items()):
                member = zipfile.ZipInfo(name)
                member.external_attr = (stat.S_IFREG | 0o600) << 16
                output.writestr(member, data)
    with zipfile.ZipFile(archive) as retained:
        require(retained.namelist() == [v['archiveMember'] for v in lineage], 'archive-member-set-mismatch')
        require(all(sha(retained.read(v['archiveMember'])) == v['sha256'] for v in lineage),
                'retained-source-archive-mismatch')
    records['attempt.json']['sourceArchiveSha256'] = sha(read_source(archive, True))
    records['attempt.json']['preparationComplete'] = False
    serialized['attempt.json'] = canonical(records['attempt.json'])
    for name, data in serialized.items():
        write_new(staging / name, data)
    sums = ''.join(f'{sha(read_source(path, True))}  {path.name}\n' for path in sorted(staging.iterdir()))
    write_new(staging / 'SHA256SUMS', sums.encode())
    verify_packet(staging, serialized)
    records['attempt.json']['preparationComplete'] = True
    serialized['attempt.json'] = canonical(records['attempt.json'])
    write_new(staging / '.complete-attempt', serialized['attempt.json'])
    os.replace(staging / '.complete-attempt', staging / 'attempt.json')
    sums = ''.join(f'{sha(read_source(path, True))}  {path.name}\n'
                   for path in sorted(staging.iterdir()) if path.name != 'SHA256SUMS')
    write_new(staging / '.complete-checksums', sums.encode())
    os.replace(staging / '.complete-checksums', staging / 'SHA256SUMS')
    verify_packet(staging, serialized)
    private_directory(evidence_root)
    custody_ancestry(Path(evidence_root))
    safe_path(destination)
    destination.mkdir(mode=0o700)
    reservation = destination.stat()
    observed = private_directory(destination).stat()
    require((observed.st_dev, observed.st_ino) == (reservation.st_dev, reservation.st_ino)
            and not any(destination.iterdir()), 'final-reservation-changed')
    os.rename(staging, destination)
    return destination


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--evidence-root', type=Path, required=True)
    parser.add_argument('--attempt-id', required=True)
    parser.add_argument('--operator', required=True)
    for name in ('handoff', 'inventory', 'catalog', 'namespaces'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--recovery-root', dest='recovery_roots', type=Path, action='append', required=True)
    try:
        packet = prepare(**vars(parser.parse_args(argv)))
    except (ValueError, OSError, KeyError, TypeError, UnicodeError, zipfile.BadZipFile) as error:
        parser.exit(2, f'Offline preparation refused: {error}\n')
    print(f'Unsigned local packet: {packet}; live gates closed; operational commands not run.')


if __name__ == '__main__':
    main()
