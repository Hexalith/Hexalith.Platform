"""One conditional encrypted object PUT, exact validator download, no credentials.

The caller authenticates current signed policy before each ordinary operation.
An uncertain write is retained as an incident; this module never retries it.
Only ciphertext reaches disk. The independently downloaded bytes are the restore
input, rather than a declaration about an earlier local copy.
"""
import base64
from datetime import datetime, timezone, timedelta
import hashlib
import os
from pathlib import Path
import re
import stat

MAXIMUM = 600 * 1024 * 1024
BUCKET = 'hexalith-recovery-points'


def require(value, reason):
    if not value:
        raise ValueError(reason)


def custody(path, maximum=MAXIMUM):
    path = Path(path)
    before = path.lstat()
    parent = path.parent.lstat()
    require(stat.S_ISREG(before.st_mode) and before.st_uid == os.getuid()
            and before.st_nlink == 1 and stat.S_IMODE(before.st_mode) == 0o600
            and 0 < before.st_size <= maximum and stat.S_ISDIR(parent.st_mode)
            and parent.st_uid == os.getuid() and stat.S_IMODE(parent.st_mode) == 0o700
            and not any(p.is_symlink() for p in [path, *path.parents]),
            'node-object-private-custody-refused')
    return before


def profile(client):
    require(client.meta.endpoint_url == 'https://s3.fr-par.scw.cloud'
            and client.meta.region_name == 'fr-par'
            and client.meta.config.retries.get('total_max_attempts') == 1,
            'node-object-sdk-profile-refused')


def read_exact(validator, key, version, expected, output, record):
    profile(validator)
    require(re.fullmatch(r'evidence/4-0/[0-9]{8}t[0-9]{6}z/native-node/(?:pre|post)/[a-z0-9.-]+', key)
            and type(version) is str and 0 < len(version) <= 1024
            and type(expected) is dict and set(expected) == {'bytes', 'sha256'}
            and type(expected['bytes']) is int and 0 < expected['bytes'] <= MAXIMUM
            and re.fullmatch('[a-f0-9]{64}', expected['sha256']),
            'node-object-version-binding-refused')
    output = Path(output)
    directory = output.parent.lstat()
    require(stat.S_ISDIR(directory.st_mode) and directory.st_uid == os.getuid()
            and stat.S_IMODE(directory.st_mode) == 0o700
            and not any(p.is_symlink() for p in [output.parent, *output.parent.parents]),
            'node-object-download-directory-refused')
    head = validator.head_object(Bucket=BUCKET, Key=key, VersionId=version)
    lock = validator.get_object_retention(Bucket=BUCKET, Key=key, VersionId=version)['Retention']
    response = validator.get_object(Bucket=BUCKET, Key=key, VersionId=version)
    count = 0
    digest = hashlib.sha256()
    try:
        fd = os.open(output, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, 'wb') as target:
            while True:
                raw = response['Body'].read(1048576)
                if not raw:
                    break
                count += len(raw)
                require(count <= expected['bytes'], 'node-object-download-size-bound')
                target.write(raw)
                digest.update(raw)
            target.flush()
            os.fsync(target.fileno())
    finally:
        response['Body'].close()
    created, retain = head.get('LastModified'), head.get('ObjectLockRetainUntilDate')
    require(head.get('VersionId') == response.get('VersionId') == version
            and count == head.get('ContentLength') == response.get('ContentLength') == expected['bytes']
            and digest.hexdigest() == expected['sha256']
            and head.get('ServerSideEncryption') == response.get('ServerSideEncryption') == 'AES256'
            and head.get('LastModified') == response.get('LastModified')
            and head.get('ObjectLockMode') == lock.get('Mode') == 'COMPLIANCE'
            and isinstance(created, datetime) and created.tzinfo is not None
            and isinstance(retain, datetime) and retain.tzinfo is not None
            and retain == lock.get('RetainUntilDate') and retain >= created + timedelta(days=30)
            and retain > datetime.now(timezone.utc), 'node-object-immutable-readback-refused')
    result = {'key': key, 'versionId': version, **expected,
              'readBackSha256': digest.hexdigest(), 'readBy': 'validator',
              'createdAt': created.isoformat(), 'readAt': datetime.now(timezone.utc).isoformat(),
              'serverSideEncryption': 'AES256', 'objectLockMode': 'COMPLIANCE',
              'retainUntil': retain.isoformat(), 'download': str(output)}
    require(record('exact-version-download', {'result': result, 'head': head, 'retention': lock}) is True,
            'node-object-readback-custody-refused')
    return result


def publish(writer, validator, source, key, expected, output, authorize, record):
    require(callable(authorize) and callable(record) and authorize('node-object-before-put') is True,
            'node-object-authentication-refused')
    for client in (writer, validator):
        profile(client)
    require(re.fullmatch(r'evidence/4-0/[0-9]{8}t[0-9]{6}z/native-node/(?:pre|post)/(?:snapshot.db.age|node-recovery.tar.gz.age|independent-native.age)', key)
            and type(expected) is dict and set(expected) == {'bytes', 'sha256'}
            and type(expected['bytes']) is int and 0 < expected['bytes'] <= MAXIMUM
            and re.fullmatch('[a-f0-9]{64}', expected['sha256']), 'node-object-publication-scope-refused')
    source = Path(source)
    before = custody(source)
    fd = os.open(source, os.O_RDONLY | os.O_NOFOLLOW)
    try:
        opened = os.fstat(fd)
        require((opened.st_dev, opened.st_ino) == (before.st_dev, before.st_ino), 'node-object-replaced')
        digest, md5, count = hashlib.sha256(), hashlib.md5(), 0
        while True:
            raw = os.read(fd, 1048576)
            if not raw:
                break
            count += len(raw)
            require(count <= expected['bytes'], 'node-object-size-bound')
            digest.update(raw)
            md5.update(raw)
        require(count == expected['bytes'] and digest.hexdigest() == expected['sha256'],
                'node-object-source-binding-refused')
        prior = validator.list_object_versions(Bucket=BUCKET, Prefix=key, MaxKeys=1000)
        require(not prior.get('IsTruncated') and not any(v['Key'] == key for name in ('Versions', 'DeleteMarkers') for v in prior.get(name, [])),
                'node-object-key-preexists')
        require(authorize('node-object-put-once') is True, 'node-object-current-policy-refused')
        require(record('put-intent', {'key': key, 'expected': expected, 'conditional': 'If-None-Match:*', 'maximumAttempts': 1}) is True,
                'node-object-put-intent-custody-refused')
        os.lseek(fd, 0, os.SEEK_SET)
        with os.fdopen(os.dup(fd), 'rb') as body:
            response = writer.put_object(Bucket=BUCKET, Key=key, Body=body, ContentLength=count,
                ContentMD5=base64.b64encode(md5.digest()).decode(), IfNoneMatch='*', ServerSideEncryption='AES256',
                ContentType='application/octet-stream', Metadata={'sha256': expected['sha256'], 'client-encryption': 'age-two-ssh-recipients'})
        version = response.get('VersionId')
        require(type(version) is str and 0 < len(version) <= 1024, 'node-object-put-outcome-unverified')
        require(record('put-response', {'key': key, 'versionId': version, 'response': response}) is True,
                'node-object-put-response-custody-refused')
        after = os.fstat(fd)
        require(all(getattr(before, k) == getattr(after, k) for k in ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns', 'st_ctime_ns')),
                'node-object-source-drift')
        require(authorize('node-object-validator-download') is True, 'node-object-current-policy-refused')
        return read_exact(validator, key, version, expected, output, record)
    finally:
        os.close(fd)
