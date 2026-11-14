"""One-off, validator-only Story 4.17 custody verification; no remote mutations.

uv run --quiet --no-project --with boto3==1.44.3 --with botocore==1.44.3 python -B ...
Independent implementation: never imports or invokes the transfer helper.
"""
from datetime import datetime, timedelta, timezone
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import subprocess
import sys
import tarfile
import tempfile
import zipfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
ENDPOINT = 'https://s3.fr-par.scw.cloud'
REGION = 'fr-par'
BUCKET = 'hexalith-recovery-points'
PREFIX = 'evidence/4-17/20261114t010116z-5574db84/'
PREPARATION = ROOT / 'docs/implementation/story-4-17-packet-custody-preparation-2026-11-14.md'
MANIFEST_SHA = 'ea4b7666ae5b77b3f00b24b6ee32c35c98da6bbe5004ebc24fb5f9c1bfdd183f'
EXPECTED = {
    '13638347236.zip': (1515070, '20a1719b753c7379207bbeb92cc73f70af99b7fa7e71734fd4a58ec7427cf223'),
    '13638293057.zip': (1946018, '20643dbd7d3702f3f504b0a4ce5fd3fe627ff32493d00624d37dca40e81b8a27'),
    '13995674927.zip': (2666335, '906c0d943e7a0ee8393bb83b96c4c917477fe18820afe37fe6ba7274afe7aeb7'),
    '14018214306.zip': (1650434, 'a26b848840ca8061a00d8ea7598b52b6a91b5331822645c23bc527278e40949a'),
    'manifest.json': (574728, MANIFEST_SHA),
}
# Identity and authority commitments copied from the approved, unchanged manifest.
IDENTITY = {
    '13638347236.zip': ('nov9-standalone', 'Hexalith/Hexalith.EventStore', 42532773702,
                       'historical failure; no new qualification verification',
                       '2026-12-08T23:05:01Z', 495, 2, 494, 'packet.json'),
    '13638293057.zip': ('nov9-fixture', 'Hexalith/Hexalith.EventStore', 42532776864,
                       'historical failure; no new qualification verification',
                       '2026-12-08T23:08:16Z', 601, 2, 494, 'packet/packet.json'),
    '13995674927.zip': ('candidate-01', 'Hexalith/Hexalith.Platform', 43333995506,
                       'diagnostic-only; original qualification integrity rejected',
                       '2026-12-13T00:11:32Z', 575, 3, 551, 'packet/packet.json'),
    '14018214306.zip': ('candidate-02', 'Hexalith/Hexalith.Platform', 43386723091,
                       'original independently verified packet; technical FAIL 20/24',
                       '2026-12-13T07:40:43Z', 544, 3, 520, 'packet/packet.json'),
}
SIGNATURES = (
    rb'-----BEGIN (?:RSA |EC |OPENSSH |DSA |ENCRYPTED )?PRIVATE KEY-----',
    rb'\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})\b',
    rb'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b',
    rb'\b(?:hvs|s)\.[A-Za-z0-9]{20,}\b',
    rb'(?m)^(?:AWS_SECRET_ACCESS_KEY|SCW_SECRET_KEY|GITHUB_TOKEN|GH_TOKEN)=[^\r\n]+',
)


class Refused(Exception):
    """Only fixed local diagnostic codes are safe to serialize."""


def require(ok, code):
    if not ok:
        raise Refused(code)


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def now():
    return datetime.now(timezone.utc)


def date(value):
    result = value if isinstance(value, datetime) else datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(result.tzinfo is not None, 'timezone-missing')
    return result.astimezone(timezone.utc)


def error_code(error):
    return str(error) if isinstance(error, Refused) else 'operation-failed-details-withheld'


def save(path, value):
    with path.open('x', encoding='utf-8') as out:
        json.dump(value, out, indent=2, sort_keys=True)
        out.write('\n')
        out.flush()
        os.fsync(out.fileno())


def content(raw, name):
    require((len(raw), digest(raw)) == EXPECTED[name], 'downloaded-commitment-mismatch')


def read_download(directory, name):
    path = directory / name
    require(path.is_file() and not path.is_symlink(), 'download-missing-or-unsafe')
    info = path.stat()
    require(info.st_uid == os.getuid() and stat.S_IMODE(info.st_mode) == 0o600,
            'download-file-custody')
    raw = path.read_bytes()
    content(raw, name)
    return raw


def version_protection(head, get, retention, requested, size, at):
    for response in (head, get):
        require(response.get('VersionId') == requested, 'response-version-mismatch')
        require(response.get('ContentLength') == size, 'response-length-mismatch')
        require(response.get('ServerSideEncryption') == 'AES256', 'object-encryption-inadequate')
        require(response.get('ObjectLockMode') == 'COMPLIANCE', 'object-retention-mode-inadequate')
    created = date(head['LastModified'])
    until = date(head['ObjectLockRetainUntilDate'])
    require(date(get['LastModified']) == created and date(get['ObjectLockRetainUntilDate']) == until,
            'head-get-protection-mismatch')
    require(created <= at and until >= created + timedelta(days=30) and until > at,
            'object-retention-duration-inadequate')
    require(retention.get('Retention', {}).get('Mode') == 'COMPLIANCE'
            and date(retention['Retention']['RetainUntilDate']) == until, 'retention-api-mismatch')
    returned_version = retention.get('ResponseMetadata', {}).get('HTTPHeaders', {}).get('x-amz-version-id')
    require(returned_version is None or returned_version == requested, 'retention-response-version-mismatch')
    return {'outcome': 'PASS', 'createdAtUtc': created.isoformat(), 'retainUntilUtc': until.isoformat(),
            'retentionApiUntilUtc': date(retention['Retention']['RetainUntilDate']).isoformat(),
            'mode': 'COMPLIANCE', 'minimumDaysFromCreation': 30, 'validAtVerification': True,
            'encryption': 'AES256', 'headVersionId': head['VersionId'], 'getVersionId': get['VersionId'],
            'headContentLength': head['ContentLength'], 'getContentLength': get['ContentLength'],
            'retentionRequestedVersionId': requested, 'retentionReturnedVersionId': returned_version}


def safe_path(name):
    p = PurePosixPath(name)
    require(bool(name) and not p.is_absolute() and '..' not in p.parts and '\\' not in name
            and all(ord(c) >= 32 for c in name), 'unsafe-entry-path')
    require(not ({v.lower() for v in p.parts} &
                 {'private', 'private-transcripts', '.ssh', '.aws', '.kube', 'node_modules',
                  'downloads', 'environment-dump'})
            and p.suffix.lower() not in {'.key', '.pem', '.p12', '.pfx', '.env'}
            and not p.name.lower().startswith('.env'), 'private-material-path')


def scan(name, body):
    safe_path(name)
    require(not any(re.search(pattern, body) for pattern in SIGNATURES), 'private-material-signature')


def inspect_archive(raw, row):
    content(raw, row['file'])
    nested_count = nested_members = 0
    with zipfile.ZipFile(io.BytesIO(raw)) as z:
        names = z.namelist()
        require(len(names) == len(set(names)) == row['entryCount']
                and len(row['entries']) == len(names)
                and set(names) == {e['path'] for e in row['entries']}, 'entry-inventory-mismatch')
        for entry in row['entries']:
            info = z.getinfo(entry['path'])
            require(not info.flag_bits & 1 and stat.S_IFMT(info.external_attr >> 16)
                    in {0, stat.S_IFREG, stat.S_IFDIR}, 'unsafe-or-encrypted-zip-entry')
            body = z.read(info)
            require(len(body) == entry['bytes'] and digest(body) == entry['sha256']
                    and info.is_dir() == entry['directory'], 'entry-bytes-mismatch')
            scan(info.filename, body)
            if info.filename.endswith(('.tgz', '.tar.gz')):
                nested_count += 1
                with tarfile.open(fileobj=io.BytesIO(body), mode='r:gz') as t:
                    members = t.getmembers()
                    require(len(members) < 10000 and sum(x.size for x in members) < 20000000,
                            'nested-public-chart-bound-exceeded')
                    seen = set()
                    for member in members:
                        safe_path(member.name)
                        require(member.name not in seen and (member.isfile() or member.isdir()),
                                'nested-public-chart-unsafe-entry')
                        seen.add(member.name)
                        if member.isfile():
                            scan(member.name, t.extractfile(member).read())
                    nested_members += len(members)
        packet = json.loads(z.read(row['packetPath']))
        require(packet['schemaVersion'] == row['packetSchemaVersion']
                and len(packet['artifacts']) == row['packetArtifactCount'], 'original-packet-schema-mismatch')
        root = row['packetPath'][:-len('packet.json')]
        for name, wanted in packet['artifacts'].items():
            safe_path(name)
            require(digest(z.read(root + name)) == wanted, 'packet-artifact-digest-mismatch')
    return {'entryCount': len(names), 'packetArtifactCount': len(packet['artifacts']),
            'packetSchemaVersion': packet['schemaVersion'], 'inventory': 'PASS',
            'packetArtifactHashes': 'PASS', 'archiveSafety': 'PASS', 'privateMaterialExclusions': 'PASS',
            'nestedPublicChartArchives': nested_count, 'nestedPublicChartMembers': nested_members}


def manifest_rows(raw):
    content(raw, 'manifest.json')
    m = json.loads(raw)
    require(m['schemaVersion'] == 1 and m['status'] == 'LOCAL_STAGING_ONLY'
            and m['offSiteCustodyAccepted'] is False
            and all(m[k] is None for k in ('destination', 'transferOperator', 'offSiteVerifier'))
            and len(m['archives']) == 4 and {r['file'] for r in m['archives']} == set(IDENTITY),
            'original-manifest-scope-mismatch')
    for row in m['archives']:
        fixed = IDENTITY[row['file']]
        require((row['label'], row['repository'], row['runId'], row['authority'], row['expiresAtUtc'],
                 row['entryCount'], row['packetSchemaVersion'], row['packetArtifactCount'], row['packetPath'])
                == fixed and row['artifactId'] == int(row['file'][:-4])
                and (row['bytes'], row['sha256']) == EXPECTED[row['file']], 'original-identity-authority-mismatch')
    return {row['file']: row for row in m['archives']}


def credentials():
    # This verifier has no writer-file path or credential-role selector.
    path = Path.home() / '.config/hexalith-recovery/validator.env'
    require(not any(p.is_symlink() for p in (path, *path.parents)), 'credential-symlink')
    info = path.stat()
    parent = path.parent.stat()
    require(stat.S_ISREG(info.st_mode) and info.st_uid == os.getuid() and info.st_nlink == 1
            and stat.S_IMODE(info.st_mode) == 0o600 and parent.st_uid == os.getuid()
            and stat.S_IMODE(parent.st_mode) == 0o700, 'validator-credential-custody')
    values = {}
    for line in path.read_text().splitlines():
        if not line.strip() or line.lstrip().startswith('#'):
            continue
        key, value = line.strip().split('=', 1)
        require(key in {'AWS_ACCESS_KEY_ID', 'AWS_SECRET_ACCESS_KEY'} and key not in values
                and bool(value) and not any(c in value for c in '\r\n\x00'), 'validator-credential-schema')
        values[key] = value
    require(set(values) == {'AWS_ACCESS_KEY_ID', 'AWS_SECRET_ACCESS_KEY'}, 'validator-credential-schema')
    return values


def make_client():
    import boto3
    import botocore
    from botocore.config import Config
    require(boto3.__version__ == botocore.__version__ == '1.44.3', 'sdk-version-drift')
    values = credentials()
    return boto3.client('s3', endpoint_url=ENDPOINT, region_name=REGION, verify=True,
                        aws_access_key_id=values['AWS_ACCESS_KEY_ID'],
                        aws_secret_access_key=values['AWS_SECRET_ACCESS_KEY'],
                        config=Config(signature_version='s3v4', retries={'total_max_attempts': 1},
                                      connect_timeout=10, read_timeout=30, s3={'addressing_style': 'path'}))


def current_protection(client):
    observed = {}
    for method in ('get_bucket_location', 'get_bucket_versioning', 'get_object_lock_configuration',
                   'get_bucket_encryption', 'get_bucket_lifecycle_configuration', 'get_bucket_acl'):
        response = getattr(client, method)(Bucket=BUCKET)
        observed[method] = {k: v for k, v in response.items() if k != 'ResponseMetadata'}
    require(observed['get_bucket_location']['LocationConstraint'] == REGION
            and observed['get_bucket_versioning']['Status'] == 'Enabled', 'current-versioning-location-inadequate')
    require(observed['get_object_lock_configuration']['ObjectLockConfiguration'] ==
            {'ObjectLockEnabled': 'Enabled', 'Rule': {'DefaultRetention': {'Mode': 'COMPLIANCE', 'Days': 30}}},
            'current-default-retention-inadequate')
    rules = observed['get_bucket_encryption']['ServerSideEncryptionConfiguration']['Rules']
    require(len(rules) == 1 and rules[0]['ApplyServerSideEncryptionByDefault'] == {'SSEAlgorithm': 'AES256'},
            'current-default-encryption-inadequate')
    expected_lifecycle = [{'ID': 'expire-' + n, 'Status': 'Enabled', 'Filter': {'Prefix': n + '/'},
                           'Expiration': {'Days': 31}, 'NoncurrentVersionExpiration': {'NoncurrentDays': 1},
                           'AbortIncompleteMultipartUpload': {'DaysAfterInitiation': 1}}
                          for n in ('keycloak', 'openbao', 'memories')]
    lifecycle = observed['get_bucket_lifecycle_configuration']['Rules']
    require(sorted(lifecycle, key=lambda r: r['ID']) == sorted(expected_lifecycle, key=lambda r: r['ID']),
            'current-lifecycle-drift-or-evidence-expiry')
    acl = observed['get_bucket_acl']
    owner = 'bed84924-0e48-4275-8b7c-5d9a1f57a8dd:bed84924-0e48-4275-8b7c-5d9a1f57a8dd'
    require(acl['Owner']['ID'] == owner and len(acl['Grants']) == 1
            and acl['Grants'][0]['Grantee']['ID'] == owner
            and acl['Grants'][0]['Permission'] == 'FULL_CONTROL', 'current-private-acl-drift')
    policy = client.get_bucket_policy(Bucket=BUCKET)['Policy']
    parsed = json.loads(policy)
    expected_roles = {
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
    require(parsed.get('Id') == 'hexalith-recovery-points-story-4-0'
            and parsed.get('Version') == '2023-04-17' and len(parsed.get('Statement', [])) == 3,
            'current-approved-bucket-policy-drift')
    statements = {r['Sid']: r for r in parsed['Statement']}
    require(set(statements) == set(expected_roles), 'current-approved-bucket-policy-drift')
    for sid, (principal, actions) in expected_roles.items():
        row = statements[sid]
        actual = row['Action']
        require(set(row) == {'Sid', 'Effect', 'Principal', 'Action', 'Resource'}
                and row['Effect'] == 'Allow' and row['Principal'] == {'SCW': principal}
                and set([actual] if isinstance(actual, str) else actual) == actions
                and set(row['Resource']) == {BUCKET, BUCKET + '/*'}, 'current-approved-bucket-policy-drift')
    return {'outcome': 'PASS', 'capturedAtUtc': now().isoformat(), 'checks': observed,
            'bucketPolicyRawSha256': digest(policy.encode()), 'evidenceLifecycleExpiry': False,
            'bucketPolicyMatchesApprovedRoles': True, 'bucketPolicy': parsed,
            'effectiveIamGrants': 'Not re-enumerated; Administrator-selected reference and observed bucket policy',
            'apiKeyToApplicationBinding': 'Historical/reference binding; not independently enumerated'}


def retrieve(client, row, directory, result):
    args = {'Bucket': BUCKET, 'Key': row['key'], 'VersionId': row['versionId']}
    head = client.head_object(**args)
    response = client.get_object(**args)
    body = response['Body']
    path = directory / row['file']
    count = 0
    sha = hashlib.sha256()
    try:
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, 'wb') as out:
            while chunk := body.read(1048576):
                count += len(chunk)
                sha.update(chunk)
                out.write(chunk)
                require(count <= EXPECTED[row['file']][0], 'download-size-bound-exceeded')
            out.flush()
            os.fsync(out.fileno())
    finally:
        body.close()
        result.update({'downloadedBytes': count, 'downloadedSha256': sha.hexdigest(),
                       'downloadPath': str(path.relative_to(ROOT))})
    retention = client.get_object_retention(**args)
    # Read the retained download again; metadata and the streaming hash alone cannot accept it.
    read_download(directory, row['file'])
    result['protection'] = version_protection(head, response, retention, row['versionId'], count, now())
    result['wholeFileCommitment'] = 'PASS'


def inventory_procedure(directory):
    text = PREPARATION.read_text()
    start = "python3 -B - <<'PY'\n"
    require(text.count(start) == 1, 'literal-inventory-procedure-not-unique')
    code = text.split(start, 1)[1].split('\nPY\n', 1)[0] + '\n'
    run = subprocess.run([sys.executable, '-B', '-c', code], cwd=directory, capture_output=True, text=True)
    require(run.returncode == 0 and run.stdout.strip() ==
            'PASS: four ZIP commitments; 2215 entries; 2059 packet artifacts; local staging only',
            'literal-inventory-procedure-failed')
    return {'outcome': 'PASS', 'procedureSha256': digest(code.encode()), 'exitCode': run.returncode,
            'entries': 2215, 'packetArtifactHashes': 2059, 'zipBytes': 7777857,
            'scope': 'Literal historical stdlib procedure executed on independently downloaded directory; its local-staging wording describes unchanged manifest fields'}


def accepted(files, protection, procedure, negative):
    return (len(files) == 5 and {r['file'] for r in files} == set(EXPECTED)
            and all(r.get('accepted') is True and r.get('outcome') == 'PASS' for r in files)
            and all(v.get('outcome') == 'PASS' for v in (protection, procedure, negative)))


def main():
    require(not (HERE / 'receipt.json').exists(), 'existing-receipt-refused')
    negative = json.loads((HERE / 'verifier-negative-checks.json').read_bytes())
    require(negative['outcome'] == 'PASS' and negative['scriptSha256'] == digest(Path(__file__).read_bytes())
            and negative['testScriptSha256'] == digest((HERE / 'test_verify_readback.py').read_bytes()),
            'focused-negative-checks-missing-or-stale')
    uploaded = json.loads((HERE / 'upload-results.json').read_bytes())
    files = uploaded['files']
    require(len(files) == 5 and {r['file'] for r in files} == set(EXPECTED), 'returned-file-scope-mismatch')
    for row in files:
        require(row['key'] == PREFIX + row['file'] and (row['bytes'], row['sha256']) == EXPECTED[row['file']]
                and isinstance(row['versionId'], str) and bool(row['versionId']), 'returned-object-identity-mismatch')
    directory = Path(tempfile.mkdtemp(prefix='story-4-17-independent-readback-', dir=ROOT / 'artifacts'))
    os.chmod(directory, 0o700)
    client = make_client()
    try:
        protection = current_protection(client)
    except Exception as error:
        protection = {'outcome': 'FAIL', 'failureCode': error_code(error)}
    results = []
    for row in files:
        result = {'file': row['file'], 'key': row['key'], 'versionId': row['versionId'],
                  'expectedBytes': EXPECTED[row['file']][0], 'expectedSha256': EXPECTED[row['file']][1],
                  'accepted': False, 'outcome': 'FAIL'}
        if row['file'] in IDENTITY:
            label, repo, run, authority, expiry, *_ = IDENTITY[row['file']]
            result.update({'label': label, 'repository': repo, 'runId': run,
                           'artifactId': int(row['file'][:-4]), 'originalAuthority': authority,
                           'originalExpiryUtc': expiry})
        try:
            retrieve(client, row, directory, result)
            result.update({'outcome': 'READBACK_PASS_PENDING_CONTENT_CHECKS'})
        except Exception as error:
            result['failureCode'] = error_code(error)
        results.append(result)
    rows = None
    manifest = next(r for r in results if r['file'] == 'manifest.json')
    try:
        rows = manifest_rows(read_download(directory, 'manifest.json'))
        require(manifest['outcome'] == 'READBACK_PASS_PENDING_CONTENT_CHECKS', 'manifest-readback-failed')
        manifest.update({'originalLocalOnlyFieldsUnchanged': True, 'originalAuthorityLabelsUnchanged': True,
                         'accepted': protection['outcome'] == 'PASS', 'outcome': 'PASS'})
    except Exception as error:
        manifest.update({'outcome': 'FAIL', 'accepted': False, 'failureCode': error_code(error)})
    manifest['completedAtUtc'] = now().isoformat()
    for result in results:
        if result['file'] == 'manifest.json':
            continue
        try:
            require(result['outcome'] == 'READBACK_PASS_PENDING_CONTENT_CHECKS',
                    result.get('failureCode', 'archive-readback-failed'))
            require(rows is not None, 'verified-manifest-unavailable')
            result.update(inspect_archive(read_download(directory, result['file']), rows[result['file']]))
            completed = now()
            require(completed < date(result['originalExpiryUtc']), 'original-expiry-missed')
            result.update({'outcome': 'PASS', 'accepted': protection['outcome'] == 'PASS',
                           'completedBeforeOriginalExpiry': True})
        except Exception as error:
            result.update({'outcome': 'FAIL', 'accepted': False, 'failureCode': error_code(error)})
        result['completedAtUtc'] = now().isoformat()
    try:
        procedure = inventory_procedure(directory)
    except Exception as error:
        procedure = {'outcome': 'FAIL', 'failureCode': error_code(error)}
    overall = accepted(results, protection, procedure, negative)
    receipt = {'schemaVersion': 1, 'completedAtUtc': now().isoformat(), 'offSiteCustodyAccepted': overall,
               'outcome': 'PASS' if overall else 'FAIL_CUSTODY_INCOMPLETE', 'files': results,
               'destination': {'provider': 'Scaleway', 'bucket': BUCKET, 'region': REGION,
                               'endpoint': ENDPOINT, 'prefix': PREFIX,
                               'failureDomain': 'off-site object storage, separate from local host and GitHub artifacts'},
               'operator': {'accountableOwner': 'Administrator', 'executor': '/root/preserve_packets_impl'},
               'verifier': {'executor': '/root/custody_verifier', 'credentialReference': '~/.config/hexalith-recovery/validator.env',
                            'expectedApplication': '406da2fa-5d02-4a49-bcbc-de0218c6460d',
                            'separatelyExecutedAutomatedVerification': True,
                            'limitation': 'Shared host; not security isolation or independent human accountability'},
               'sdk': {'boto3': '1.44.3', 'botocore': '1.44.3', 'tlsVerification': True, 'totalMaximumAttempts': 1},
               'currentProtection': protection, 'literalStdlibInventoryProcedure': procedure,
               'focusedNegativeChecks': negative,
               'downloadDirectory': str(directory.relative_to(ROOT)), 'downloadDirectoryMode': '0700', 'downloadFileModes': '0600',
               'administrativeHold': {'owner': 'Administrator', 'until': 'guarantees close, then deletion review against policy',
                                      'automaticDeletionDate': None, 'objectLegalHoldOrControlChange': False,
                                      'enforcedInitialMinimum': '30 days COMPLIANCE; not permission to delete afterward'},
               'privateMaterialVerification': {'scope': 'Approved bounded path and key/token/environment signatures across all ZIP entries and nested public chart members',
                                               'limitation': 'Not a general proof of detecting every conceivable secret; admission remains bound to five exact owner-approved commitments'},
               'originalManifestSha256': MANIFEST_SHA, 'originalManifestChanged': False,
               'qualification': {'parentStatuses': 'in-progress', 'guarantees': 'five REJECTED/open',
                                 'technical': 'FAIL (20/24)', 'selection': None, 'production': 'FAIL'},
               'verificationScriptSha256': digest(Path(__file__).read_bytes())}
    save(HERE / 'receipt.json', receipt)
    print(json.dumps({'outcome': receipt['outcome'], 'offSiteCustodyAccepted': overall,
                      'filesPassed': sum(r['accepted'] for r in results), 'receipt': str((HERE / 'receipt.json').relative_to(ROOT)),
                      'downloadDirectory': receipt['downloadDirectory']}))
    return 0 if overall else 1


if __name__ == '__main__':
    try:
        sys.exit(main())
    except Exception as error:
        print(json.dumps({'outcome': 'FAIL_CUSTODY_INCOMPLETE', 'offSiteCustodyAccepted': False,
                          'failureCode': error_code(error)}))
        sys.exit(1)
