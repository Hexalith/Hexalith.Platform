"""Retained, bounded Story 4.17 transfer. No qualification or control mutations.

Run with the S3 SDK: uv run --no-project --with boto3==1.44.3 --with botocore==1.44.3 python -B ...
Only upload() mutates remotely: at most five If-None-Match:* PutObject calls.
The validator-only independent agent performs acceptance separately.
"""
import base64
from contextlib import ExitStack, contextmanager
from datetime import datetime, timezone
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import sys
import tarfile
import zipfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
SOURCE = ROOT / 'artifacts/story-4-17-custody-staging-2026-11-14'
ENDPOINT = 'https://s3.fr-par.scw.cloud'
REGION = 'fr-par'
BUCKET = 'hexalith-recovery-points'
TRANSFER = '20261114t010116z-5574db84'
PREFIX = 'evidence/4-17/' + TRANSFER + '/'
MANIFEST_SHA = 'ea4b7666ae5b77b3f00b24b6ee32c35c98da6bbe5004ebc24fb5f9c1bfdd183f'
COMMITMENTS = {
    '13638347236.zip': (1515070, '20a1719b753c7379207bbeb92cc73f70af99b7fa7e71734fd4a58ec7427cf223'),
    '13638293057.zip': (1946018, '20643dbd7d3702f3f504b0a4ce5fd3fe627ff32493d00624d37dca40e81b8a27'),
    '13995674927.zip': (2666335, '906c0d943e7a0ee8393bb83b96c4c917477fe18820afe37fe6ba7274afe7aeb7'),
    '14018214306.zip': (1650434, 'a26b848840ca8061a00d8ea7598b52b6a91b5331822645c23bc527278e40949a'),
    'manifest.json': (574728, MANIFEST_SHA),
}
OPERATOR = {'accountableOwner': 'Administrator', 'executor': '/root/preserve_packets_impl',
            'credentialReference': '~/.config/hexalith-recovery/writer.env',
            'expectedApplication': '9a7cff8d-cffc-4554-93fe-01ed8e0d0d04'}
HOLD = 'Administrator-owned no-deletion hold while guarantees remain open; then deletion review against applicable policy'
FIELDS = ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns', 'st_ctime_ns', 'st_nlink')
PRIVATE = {
    'private-key': rb'-----BEGIN (?:RSA |EC |OPENSSH |DSA |ENCRYPTED )?PRIVATE KEY-----',
    'github-token': rb'\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})\b',
    'aws-access-key': rb'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b',
    'vault-token': rb'\b(?:hvs|s)\.[A-Za-z0-9]{20,}\b',
    'secret-environment': rb'(?m)^(?:AWS_SECRET_ACCESS_KEY|SCW_SECRET_KEY|GITHUB_TOKEN|GH_TOKEN)=[^\r\n]+',
}


def require(ok, code):
    if not ok:
        raise ValueError(code)


def utc():
    return datetime.now(timezone.utc).isoformat()


def sha(data):
    return hashlib.sha256(data).hexdigest()


def encode(value):
    return (json.dumps(value, indent=2, sort_keys=True) + '\n').encode()


def save(name, value):
    """Immutable local evidence; never serialize an SDK response or exception."""
    data = encode(value)
    with open(HERE / name, 'xb') as out:
        out.write(data)
        out.flush()
        os.fsync(out.fileno())


def failure(error):
    # SDK errors can contain credentials, request URLs or arbitrary server text.
    if isinstance(error, ValueError) and re.fullmatch('[a-z0-9-]+', str(error)):
        return str(error)
    return 'operation-failed-details-withheld'


def same(a, b):
    return all(getattr(a, key) == getattr(b, key) for key in FIELDS)


def stable(fd, before, path):
    require(same(before, os.fstat(fd)) and same(before, path.lstat()), 'source-drift')


def safe_name(name):
    p = PurePosixPath(name)
    require(bool(name) and not p.is_absolute() and '..' not in p.parts and '\\' not in name
            and '\x00' not in name and all(ord(c) >= 32 for c in name), 'unsafe-entry-path')
    require(not any(part.lower() in {'private', 'private-transcripts', '.ssh', '.aws', '.kube',
                                    'node_modules', 'downloads', 'environment-dump'} for part in p.parts)
            and p.suffix.lower() not in {'.key', '.pem', '.p12', '.pfx', '.env'}
            and not p.name.lower().startswith('.env'), 'private-material-path')


def private_check(name, data):
    safe_name(name)
    require(not any(re.search(pattern, data) for pattern in PRIVATE.values()), 'private-material-signature')
    if name.endswith(('.tgz', '.tar.gz')):
        # The admitted ZIPs include pinned public Helm-chart inputs. Inspect those
        # containers too, without extracting or accepting links/special files.
        with tarfile.open(fileobj=io.BytesIO(data), mode='r:gz') as archive:
            members = archive.getmembers()
            require(len(members) < 10000 and sum(x.size for x in members) < 20000000,
                    'nested-archive-bound')
            seen = set()
            for item in members:
                safe_name(item.name)
                require(item.name not in seen and (item.isdir() or item.isfile()), 'unsafe-nested-entry')
                seen.add(item.name)
                if item.isfile():
                    body = archive.extractfile(item).read()
                    require(not any(re.search(p, body) for p in PRIVATE.values()), 'private-material-signature')


def inspect_archive(raw, row):
    require((len(raw), sha(raw)) == COMMITMENTS[row['file']] and
            (row['bytes'], row['sha256']) == COMMITMENTS[row['file']], 'zip-commitment-mismatch')
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)) == row['entryCount'] and
                set(names) == {x['path'] for x in row['entries']}, 'zip-inventory-mismatch')
        for item in row['entries']:
            info = archive.getinfo(item['path'])
            mode = info.external_attr >> 16
            require(not info.flag_bits & 1 and not stat.S_ISLNK(mode)
                    and (stat.S_IFMT(mode) in (0, stat.S_IFREG, stat.S_IFDIR)), 'unsafe-zip-entry')
            data = archive.read(info)
            require(len(data) == item['bytes'] and sha(data) == item['sha256']
                    and info.is_dir() == item['directory'], 'zip-entry-mismatch')
            private_check(item['path'], data)
        packet = json.loads(archive.read(row['packetPath']))
        require(packet['schemaVersion'] == row['packetSchemaVersion']
                and len(packet['artifacts']) == row['packetArtifactCount'], 'packet-schema-mismatch')
        base = row['packetPath'][:-len('packet.json')]
        for name, digest in packet['artifacts'].items():
            safe_name(name)
            require(sha(archive.read(base + name)) == digest, 'packet-artifact-mismatch')
    return {'file': row['file'], 'entryCount': len(names), 'packetArtifactCount': len(packet['artifacts']),
            'packetSchemaVersion': row['packetSchemaVersion'], 'authority': row['authority'],
            'inventory': 'PASS', 'packetArtifactHashes': 'PASS', 'archiveSafety': 'PASS',
            'privateMaterialExclusions': 'PASS'}


@contextmanager
def sources(directory=SOURCE):
    """Open all five originals once; immutable in-memory bodies come from these FDs."""
    require(not any(p.is_symlink() for p in [directory, *directory.parents]), 'source-symlink-parent')
    require(directory.stat().st_uid == os.getuid() and stat.S_IMODE(directory.stat().st_mode) == 0o700,
            'source-directory-custody')
    with ExitStack() as stack:
        files = {}
        for name, (size, digest) in COMMITMENTS.items():
            path = directory / name
            before = path.lstat()
            require(stat.S_ISREG(before.st_mode) and before.st_uid == os.getuid(), 'source-file-custody')
            fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
            stack.callback(os.close, fd)
            stable(fd, before, path)
            body = bytearray()
            while chunk := os.read(fd, 1048576):
                body.extend(chunk)
                require(len(body) <= size, 'source-size-bound')
            raw = bytes(body)
            require((len(raw), sha(raw)) == (size, digest), 'source-commitment-mismatch')
            stable(fd, before, path)
            files[name] = {'fd': fd, 'before': before, 'path': path, 'raw': raw}
        manifest = json.loads(files['manifest.json']['raw'])
        require(manifest['schemaVersion'] == 1 and manifest['status'] == 'LOCAL_STAGING_ONLY'
                and manifest['offSiteCustodyAccepted'] is False
                and all(manifest[k] is None for k in ('destination', 'transferOperator', 'offSiteVerifier'))
                and {r['file'] for r in manifest['archives']} == set(COMMITMENTS) - {'manifest.json'},
                'manifest-scope-mismatch')
        results = [inspect_archive(files[row['file']]['raw'], row) for row in manifest['archives']]
        require(sum(r['entryCount'] for r in results) == 2215
                and sum(r['packetArtifactCount'] for r in results) == 2059, 'inventory-denominator-mismatch')
        try:
            yield files, manifest, results
        finally:
            for item in files.values():
                stable(item['fd'], item['before'], item['path'])


def credential(role):
    require(role in {'writer', 'validator'}, 'credential-role-refused')
    path = Path.home() / '.config/hexalith-recovery' / (role + '.env')
    require(not any(p.is_symlink() for p in [path, *path.parents]), 'credential-symlink')
    st = path.lstat()
    parent = path.parent.stat()
    require(stat.S_ISREG(st.st_mode) and st.st_uid == os.getuid() and st.st_nlink == 1
            and stat.S_IMODE(st.st_mode) == 0o600 and parent.st_uid == os.getuid()
            and stat.S_IMODE(parent.st_mode) == 0o700, 'credential-custody-refused')
    result = {}
    with open(path, 'r') as src:
        for line in src:
            line = line.strip()
            if not line or line.startswith('#'):
                continue
            key, value = line.split('=', 1)
            require(key in {'AWS_ACCESS_KEY_ID', 'AWS_SECRET_ACCESS_KEY'} and key not in result,
                    'credential-schema-refused')
            require(bool(value) and not any(c in value for c in '\r\n\x00'), 'credential-value-refused')
            result[key] = value
    require(set(result) == {'AWS_ACCESS_KEY_ID', 'AWS_SECRET_ACCESS_KEY'}, 'credential-schema-refused')
    return result


def client(values):
    import boto3
    import botocore
    from botocore.config import Config
    require(boto3.__version__ == botocore.__version__ == '1.44.3', 'sdk-version-drift')
    return boto3.client('s3', endpoint_url=ENDPOINT, region_name=REGION, verify=True,
                        aws_access_key_id=values['AWS_ACCESS_KEY_ID'],
                        aws_secret_access_key=values['AWS_SECRET_ACCESS_KEY'],
                        config=Config(signature_version='s3v4', connect_timeout=10, read_timeout=30,
                                      retries={'total_max_attempts': 1},
                                      s3={'addressing_style': 'path'}))


def protections(validator):
    """Read-only checks; no IAM API, mutation probes or control changes."""
    def get(method):
        response = getattr(validator, method)(Bucket=BUCKET)
        return {k: v for k, v in response.items() if k != 'ResponseMetadata'}
    observed = {name: get(name) for name in (
        'get_bucket_location', 'get_bucket_versioning', 'get_object_lock_configuration',
        'get_bucket_encryption', 'get_bucket_lifecycle_configuration', 'get_bucket_acl')}
    policy_raw = get('get_bucket_policy')['Policy']
    policy = json.loads(policy_raw)
    require(observed['get_bucket_location']['LocationConstraint'] == REGION
            and observed['get_bucket_versioning']['Status'] == 'Enabled', 'bucket-location-versioning-drift')
    require(observed['get_object_lock_configuration']['ObjectLockConfiguration'] == {
        'ObjectLockEnabled': 'Enabled', 'Rule': {'DefaultRetention': {'Mode': 'COMPLIANCE', 'Days': 30}}},
        'bucket-retention-drift')
    rules = observed['get_bucket_encryption']['ServerSideEncryptionConfiguration']['Rules']
    require(len(rules) == 1 and rules[0]['ApplyServerSideEncryptionByDefault'] == {'SSEAlgorithm': 'AES256'},
            'bucket-encryption-drift')
    lifecycle = observed['get_bucket_lifecycle_configuration']['Rules']
    expected = [{'Expiration': {'Days': 31}, 'ID': 'expire-' + n, 'Filter': {'Prefix': n + '/'},
                 'Status': 'Enabled', 'NoncurrentVersionExpiration': {'NoncurrentDays': 1},
                 'AbortIncompleteMultipartUpload': {'DaysAfterInitiation': 1}}
                for n in ('keycloak', 'openbao', 'memories')]
    require(sorted(lifecycle, key=lambda r: r['ID']) == sorted(expected, key=lambda r: r['ID']),
            'bucket-lifecycle-drift')
    require(policy.get('Id') == 'hexalith-recovery-points-story-4-0'
            and len(policy.get('Statement', [])) == 3, 'bucket-policy-scope-drift')
    statements = {r['Sid']: r for r in policy['Statement']}
    role_actions = {
        'OwnerAdministration': ('user_id:31877e57-5937-4688-b018-a741852bf03f', {'s3:*'}),
        'BackupWriterNoDelete': ('application_id:9a7cff8d-cffc-4554-93fe-01ed8e0d0d04', {
            's3:AbortMultipartUpload', 's3:GetBucketLocation', 's3:GetObject', 's3:ListBucket',
            's3:ListBucketMultipartUploads', 's3:ListMultipartUploadParts', 's3:PutObject'}),
        'IndependentValidatorReadOnly': ([
            'application_id:406da2fa-5d02-4a49-bcbc-de0218c6460d',
            'user_id:97a06b64-82d5-47bc-8642-06f84b86db99'], {
                's3:GetBucketAcl', 's3:GetBucketLocation', 's3:GetBucketObjectLockConfiguration',
                's3:GetBucketTagging', 's3:GetBucketVersioning', 's3:GetEncryptionConfiguration',
                's3:GetLifecycleConfiguration', 's3:GetObject', 's3:GetObjectAcl', 's3:GetObjectLegalHold',
                's3:GetObjectRetention', 's3:GetObjectTagging', 's3:GetObjectVersion',
                's3:GetObjectVersionTagging', 's3:ListBucket', 's3:ListBucketVersions'})}
    for sid, (principal, actions) in role_actions.items():
        row = statements.get(sid, {})
        actual = row.get('Action', [])
        require(set(row) == {'Sid', 'Effect', 'Principal', 'Action', 'Resource'}
                and row['Effect'] == 'Allow' and row['Principal'] == {'SCW': principal}
                and set([actual] if isinstance(actual, str) else actual) == actions
                and set(row['Resource']) == {BUCKET, BUCKET + '/*'}, 'bucket-policy-role-drift')
    acl = observed['get_bucket_acl']
    owner = 'bed84924-0e48-4275-8b7c-5d9a1f57a8dd:bed84924-0e48-4275-8b7c-5d9a1f57a8dd'
    require(acl['Owner']['ID'] == owner and len(acl['Grants']) == 1
            and acl['Grants'][0]['Grantee']['ID'] == owner
            and acl['Grants'][0]['Permission'] == 'FULL_CONTROL', 'bucket-acl-drift')
    return {'checks': observed, 'bucketPolicy': policy, 'bucketPolicyRawSha256': sha(policy_raw.encode()),
            'evidenceLifecycleExpiry': False, 'administrativeHold': HOLD,
            'effectiveIamGrants': 'NOT_REQUERIED; historical identity references and observed bucket policy only',
            'scope': 'Owner admitted these exact sanitized bytes under evidence/4-17/; no object legal hold or control change'}


def empty_prefix(validator):
    result = validator.list_object_versions(Bucket=BUCKET, Prefix=PREFIX, MaxKeys=1000)
    require(not result.get('IsTruncated') and not result.get('Versions')
            and not result.get('DeleteMarkers'), 'destination-prefix-collision')


def preflight():
    writer_values, validator_values = credential('writer'), credential('validator')
    require(writer_values['AWS_ACCESS_KEY_ID'] != validator_values['AWS_ACCESS_KEY_ID'], 'credential-roles-not-distinct')
    validator = client(validator_values)
    with sources() as (_, manifest, checks):
        protection = protections(validator)
        empty_prefix(validator)
        import boto3
        import botocore
        result = {'schemaVersion': 1, 'outcome': 'PASS', 'capturedAtUtc': utc(), 'transferId': TRANSFER,
                  'destination': {'provider': 'Scaleway', 'bucket': BUCKET, 'endpoint': ENDPOINT,
                                  'region': REGION, 'prefix': PREFIX, 'failureDomain': 'off-site Scaleway object storage'},
                  'operator': OPERATOR, 'protection': protection, 'archives': checks,
                  'sourceCommitments': {n: {'bytes': s, 'sha256': h} for n, (s, h) in COMMITMENTS.items()},
                  'manifestSha256': MANIFEST_SHA, 'originalManifestUnchanged': True,
                  'originalAuthorityLabelsUnchanged': True, 'originalLocalOnlyFieldsUnchanged': True,
                  'privateMaterialCheck': {'outcome': 'PASS', 'scope': 'All ZIP entries and nested public Helm chart members; forbidden private paths, key/token/environment-secret signatures; original sanitized packet admission remains byte-bound',
                                           'limitations': 'Bounded exclusion checks are not a general secret-detection proof'},
                  'sdk': {'boto3Version': boto3.__version__, 'botocoreVersion': botocore.__version__,
                          'tlsVerification': True, 'totalMaximumAttempts': 1},
                  'credentialFilesOwnerOnly': True, 'credentialRolesDistinct': True,
                  'actualIamIdentityBinding': 'Existing Administrator-selected credential references; bucket policy observed; no IAM enumeration or denial mutation probes',
                  'qualification': {'parentStatuses': 'in-progress', 'guarantees': 'five REJECTED/open',
                                    'technical': 'FAIL (20/24)', 'selection': None, 'production': 'FAIL'}}
        save('preflight.json', result)
    return result


def upload():
    # One durable start marker and one durable intent per file make restart/retry
    # impossible through this operation, even after a missing PUT response.
    review = json.loads((HERE / 'inspection.json').read_bytes())
    require(review['outcome'] == 'PASS' and review['scriptSha256'] == sha(Path(__file__).read_bytes())
            and review['planSha256'] == sha((HERE / 'plan.md').read_bytes()), 'independent-inspection-required')
    prior = json.loads((HERE / 'preflight.json').read_bytes())
    require(prior['outcome'] == 'PASS' and prior['transferId'] == TRANSFER
            and (datetime.now(timezone.utc) - datetime.fromisoformat(prior['capturedAtUtc'])).total_seconds() < 3600,
            'preflight-missing-or-stale')
    save('upload-started.json', {'startedAtUtc': utc(), 'transferId': TRANSFER, 'operator': OPERATOR,
                                  'scriptSha256': review['scriptSha256'], 'inspectionSha256': sha(encode(review))})
    writer_values, validator_values = credential('writer'), credential('validator')
    require(writer_values['AWS_ACCESS_KEY_ID'] != validator_values['AWS_ACCESS_KEY_ID'], 'credential-roles-not-distinct')
    writer, validator = client(writer_values), client(validator_values)
    outcomes = []
    with sources() as (files, manifest, _):
        protections(validator)
        empty_prefix(validator)
        for name, (size, digest) in COMMITMENTS.items():
            item = files[name]
            stable(item['fd'], item['before'], item['path'])
            require(sha(item['raw']) == digest, 'source-snapshot-drift')
            key = PREFIX + name
            save('intent-' + name + '.json', {'createdAtUtc': utc(), 'key': key, 'bytes': size,
                'sha256': digest, 'conditional': 'If-None-Match:*', 'maximumAttempts': 1,
                'endpoint': ENDPOINT, 'region': REGION, 'tlsVerification': True, 'operator': OPERATOR})
            try:
                response = writer.put_object(Bucket=BUCKET, Key=key, Body=item['raw'], ContentLength=size,
                    ContentMD5=base64.b64encode(hashlib.md5(item['raw']).digest()).decode(),
                    IfNoneMatch='*', ServerSideEncryption='AES256',
                    ContentType='application/json' if name == 'manifest.json' else 'application/zip',
                    Metadata={'sha256': digest, 'story': '4-17', 'transfer-id': TRANSFER})
                version = response.get('VersionId')
                require(isinstance(version, str) and 0 < len(version) <= 1024 and version != 'null'
                        and response.get('ResponseMetadata', {}).get('HTTPStatusCode') == 200,
                        'put-response-version-unverified')
                outcome = {'file': name, 'key': key, 'versionId': version, 'bytes': size, 'sha256': digest,
                           'outcome': 'PUT_RETURNED_VERSION_AWAITING_INDEPENDENT_READBACK', 'completedAtUtc': utc()}
                # Persist returned version before any following check can fail.
                save('returned-' + name + '.json', outcome)
                outcomes.append(outcome)
                stable(item['fd'], item['before'], item['path'])
            except Exception as error:
                save('uncertain-' + name + '.json', {'file': name, 'key': key,
                    'outcome': 'UNACCEPTED_STOP_READ_ONLY_RECONCILIATION_REQUIRED',
                    'reason': failure(error), 'capturedAtUtc': utc()})
                raise ValueError('put-uncertain-stop-no-retry') from None
        save('upload-results.json', {'transferId': TRANSFER, 'operator': OPERATOR,
             'completedAtUtc': utc(), 'offSiteCustodyAccepted': False,
             'outcome': 'ALL_FIVE_RETURNED_VERSIONS_AWAITING_INDEPENDENT_VERIFIER', 'files': outcomes})


def reconcile():
    """Read-only listing after uncertainty; it never resumes or accepts uploads."""
    validator = client(credential('validator'))
    result = validator.list_object_versions(Bucket=BUCKET, Prefix=PREFIX, MaxKeys=1000)
    rows = [{k: r[k] for k in ('Key', 'VersionId', 'IsLatest', 'Size') if k in r}
            for r in result.get('Versions', [])]
    save('reconciliation-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S') + '.json', {
        'capturedAtUtc': utc(), 'transferId': TRANSFER, 'versions': rows,
        'isTruncated': bool(result.get('IsTruncated')), 'deleteMarkerCount': len(result.get('DeleteMarkers', [])),
        'outcome': 'READ_ONLY_OBSERVATION_NO_RETRY_OR_CUSTODY_CREDIT'})


if __name__ == '__main__':
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in {'preflight', 'upload', 'reconcile'}, 'command-refused')
        {'preflight': preflight, 'upload': upload, 'reconcile': reconcile}[sys.argv[1]]()
        print('PASS: ' + sys.argv[1] + '; sanitized evidence retained')
    except Exception as error:
        print('STOP: ' + failure(error), file=sys.stderr)
        sys.exit(1)
