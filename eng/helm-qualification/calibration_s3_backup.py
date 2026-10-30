"""One-shot independent calibration custody through the existing bounded runner.

Admission inspects credential custody only. Values are parsed as data exclusively
by the future authorized execution, never sourced, logged or placed in argv.
"""
import base64
from datetime import datetime
import hashlib
import os
from pathlib import Path
import re
import shutil
import stat
import xml.etree.ElementTree as ET

import campaign_capacity as capacity
import campaign_lifecycle as lifecycle

SCHEMA = 'hexalith.platform.calibration-s3-backup/v1'
DESTINATION = {'provider': 'scaleway', 'projectId': 'bed84924-0e48-4275-8b7c-5d9a1f57a8dd',
    'bucket': 'hexalith-recovery-points', 'endpoint': 'https://s3.fr-par.scw.cloud', 'region': 'fr-par', 'keyPrefix': 'memories/'}
IDENTITIES = {'writer': '9a7cff8d-cffc-4554-93fe-01ed8e0d0d04', 'validator': '406da2fa-5d02-4a49-bcbc-de0218c6460d'}
CONTROL_BYTES = 64 * 1024
OVERHEAD_BYTES = 2 * 1024 * 1024
MAX_ARCHIVE_BYTES = 4 * capacity.GIB


def require(value, code):
    lifecycle.require(value, 'calibration-s3-' + code)


def private_ref(ref):
    require(isinstance(ref, dict) and set(ref) == {'path', 'sha256'}
        and re.fullmatch('[0-9a-f]{64}', ref['sha256'] or ''), 'private-reference')
    path = lifecycle.private(ref['path'])
    lifecycle.private(path.parent, directory=True)
    info = path.stat()
    require(info.st_nlink == 1 and info.st_size <= CONTROL_BYTES, 'credential-custody')
    return path


def configuration(plan):
    """Closed opt-in shape; does not open a credential value."""
    config = plan['s3Backup']
    require(isinstance(config, dict) and set(config) == {'schema', *DESTINATION, 'key', 'failureDomain', 'poolId',
        'curl', 'curlVersion', 'ca', 'writer', 'validator', 'maximumArchiveBytes', 'requestTimeoutSeconds'}, 'configuration')
    require(config['schema'] == SCHEMA and all(config[k] == v for k, v in DESTINATION.items()), 'destination')
    require(config['key'] == 'memories/calibration/' + plan['runId'] + '.dump', 'exact-run-key')
    require(isinstance(config['failureDomain'], str) and bool(config['failureDomain'])
        and isinstance(config['poolId'], str) and bool(config['poolId']), 'provider-backing')
    require(config['curlVersion'] == '8.18.0', 'curl-version')
    lifecycle.binary(config['curl']); capacity.reference(config['ca'])
    require(Path(config['ca']['path']).stat().st_size <= CONTROL_BYTES * 16, 'ca-bound')
    credentials = []
    for role in IDENTITIES:
        value = config[role]
        require(isinstance(value, dict) and set(value) == {'identity', 'credentials'}
            and value['identity'] == IDENTITIES[role], 'credential-identity')
        path = private_ref(value['credentials'])
        credentials.append((path, path.stat().st_dev, path.stat().st_ino))
    require(credentials[0][0] != credentials[1][0] and credentials[0][1:] != credentials[1][1:], 'credential-alias')
    maximum = config['maximumArchiveBytes']
    require(type(maximum) is int and 0 < maximum <= MAX_ARCHIVE_BYTES
        and type(config['requestTimeoutSeconds']) is int and 1 <= config['requestTimeoutSeconds'] <= 300, 'transfer-bounds')
    require(5 * maximum <= 4 * plan['allocations']['backup']
        and 5 * (2 * maximum + OVERHEAD_BYTES) <= 4 * plan['allocations']['host-workspace'], 'staging-or-backup-capacity')
    primary, staging = (lifecycle.private(plan['paths'][k], directory=True) for k in ('primary', 'backup'))
    require(primary.stat().st_dev == staging.stat().st_dev
        and staging != primary / plan['runId'] and primary / plan['runId'] not in staging.parents, 'staging-custody')
    return config


def reservation(plan, now, primary):
    config = configuration(plan)
    custody = plan['reservations']['backup']
    require(set(custody) == {'issuer', 'path', 'key', 'binding'}, 'reservation-custody')
    value = lifecycle.signed(custody['path'], custody['key'])
    binding = {'schema': SCHEMA, **DESTINATION, 'key': config['key'], 'poolId': config['poolId'],
        'failureDomain': config['failureDomain'], 'runId': plan['runId'], 'profileSha256': plan['profileSha256'],
        'issuer': custody['issuer'], 'owner': 'github:user:6775094'}
    required = {*binding, 'reservationId', 'observedUtcMs', 'validFromUtcMs', 'validUntilUtcMs', 'disposition',
        'revokedUtcMs', 'availableBytes', 'competingBytes', 'reservedBytes', 'allocations'}
    require(set(value) == required and all(value[k] == v for k, v in binding.items()), 'reservation-binding')
    require(isinstance(value['reservationId'], str) and re.fullmatch(r'[a-zA-Z0-9][a-zA-Z0-9:/._-]{0,127}', value['reservationId']), 'reservation-identity')
    backing = {*DESTINATION, 'key', 'poolId', 'failureDomain', 'reservationId', 'owner', 'validFromUtcMs', 'validUntilUtcMs'}
    require(isinstance(custody['binding'], dict) and set(custody['binding']) == backing
        and all(value[k] == v for k, v in custody['binding'].items()), 'reservation-backing-drift')
    require(value['disposition'] == 'active' and value['revokedUtcMs'] is None, 'reservation-withdrawn')
    require(all(type(value[k]) is int for k in ('observedUtcMs', 'validFromUtcMs', 'validUntilUtcMs'))
        and 0 <= now - value['observedUtcMs'] <= 300000 and value['validFromUtcMs'] <= now < value['validUntilUtcMs']
        and value['validUntilUtcMs'] >= plan['window']['expiresUtcMs'], 'reservation-stale-or-short')
    require(config['failureDomain'] != primary['failureDomain'] and config['poolId'] != primary['poolId'], 'independent-provider-required')
    require(value['allocations'] == {'backup': plan['allocations']['backup']}
        and all(type(value[k]) is int and value[k] >= 0 for k in ('availableBytes', 'competingBytes', 'reservedBytes'))
        and value['reservedBytes'] >= plan['allocations']['backup']
        and value['availableBytes'] - value['competingBytes'] >= plan['allocations']['backup'], 'net-provider-capacity')
    return value


def credentials(ref):
    """Parse existing private .env as a closed data format, without evaluation."""
    private_ref(ref)
    raw = lifecycle.read_private(ref['path'], ref['sha256'])
    try:
        lines = raw.decode('ascii').splitlines()
    except UnicodeError:
        require(False, 'credential-format')
    fields = {}
    allowed = {'AWS_ACCESS_KEY_ID', 'AWS_SECRET_ACCESS_KEY', 'AWS_DEFAULT_REGION', 'AWS_REGION',
        'SCW_ACCESS_KEY', 'SCW_SECRET_KEY', 'SCW_DEFAULT_PROJECT_ID', 'SCW_DEFAULT_REGION'}
    for line in lines:
        line = line.strip()
        if not line or line.startswith('#'):
            continue
        match = re.fullmatch(r'(?:export )?([A-Z_]+)=([A-Za-z0-9/+_=.:\-]+|"[A-Za-z0-9/+_=.:\-]+"|\'[A-Za-z0-9/+_=.:\-]+\')', line)
        require(match is not None and match[1] in allowed and match[1] not in fields, 'credential-format')
        fields[match[1]] = match[2].strip('"\'')
    aws = {'AWS_ACCESS_KEY_ID', 'AWS_SECRET_ACCESS_KEY'}
    scw = {'SCW_ACCESS_KEY', 'SCW_SECRET_KEY'}
    require((aws <= fields.keys()) != (scw <= fields.keys()), 'credential-format')
    names = ('AWS_ACCESS_KEY_ID', 'AWS_SECRET_ACCESS_KEY') if aws <= fields.keys() else ('SCW_ACCESS_KEY', 'SCW_SECRET_KEY')
    require(not ((scw if names[0].startswith('AWS') else aws) & fields.keys()), 'credential-format')
    for name in ('AWS_DEFAULT_REGION', 'AWS_REGION', 'SCW_DEFAULT_REGION'):
        require(name not in fields or fields[name] == DESTINATION['region'], 'credential-region')
    require('SCW_DEFAULT_PROJECT_ID' not in fields or fields['SCW_DEFAULT_PROJECT_ID'] == DESTINATION['projectId'], 'credential-project')
    access, secret = (fields[n] for n in names)
    require(re.fullmatch('[A-Za-z0-9-]{8,128}', access) and re.fullmatch('[A-Za-z0-9/+_=.-]{16,256}', secret), 'credential-format')
    return access, secret


def file_hashes(path, maximum, guard=None):
    if guard is not None:
        guard()
    lifecycle.private(path); lifecycle.private(Path(path).parent, directory=True)
    sha, md5 = hashlib.sha256(), hashlib.md5(usedforsecurity=False)
    with os.fdopen(os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK), 'rb') as stream:
        before = os.fstat(stream.fileno())
        require(stat.S_ISREG(before.st_mode) and before.st_nlink == 1 and 0 < before.st_size <= maximum, 'archive-size')
        total = 0
        while True:
            if guard is not None:
                guard()
            chunk = stream.read(1024 * 1024)
            if guard is not None:
                guard()
            if not chunk:
                break
            total += len(chunk); require(total <= maximum, 'archive-size')
            sha.update(chunk); md5.update(chunk)
        after = os.fstat(stream.fileno())
    current = Path(path).stat()
    require(total == before.st_size and before.st_size == after.st_size and before.st_mtime_ns == after.st_mtime_ns
        and (current.st_dev, current.st_ino) == (before.st_dev, before.st_ino), 'archive-changed')
    return {'size': total, 'sha256': sha.hexdigest(), 'contentMd5': base64.b64encode(md5.digest()).decode()}


def xml(raw):
    require(len(raw) <= CONTROL_BYTES and b'<!' not in raw, 'response-xml')
    try:
        root = ET.fromstring(raw)
    except ET.ParseError:
        require(False, 'response-xml')
    for node in root.iter():
        node.tag = node.tag.rsplit('}', 1)[-1]
    return root


def one(root, name):
    nodes = root.findall('.//' + name)
    require(len(nodes) == 1 and nodes[0].text is not None, 'response-' + name.lower())
    return nodes[0].text


def headers(path, expected):
    raw = lifecycle.read_private(path)
    require(len(raw) <= CONTROL_BYTES, 'response-headers')
    try:
        lines = raw.decode('ascii').strip().splitlines()
    except UnicodeError:
        require(False, 'response-headers')
    require(lines and re.fullmatch(r'HTTP/(?:1\.[01]|2|3) ' + str(expected) + r'(?: [^\r\n]*)?', lines[0]), 'response-status')
    result = {}
    for line in lines[1:]:
        require(':' in line, 'response-headers')
        key, value = line.split(':', 1); key = key.lower()
        require(key not in result and re.fullmatch('[a-z0-9-]+', key), 'response-headers')
        result[key] = value.strip()
    require('location' not in result, 'redirect-refused')
    return result


class Backup:
    def __init__(self, runner):
        self.q = runner
        self.config = configuration(runner.plan)
        self.directory = Path(runner.plan['paths']['backup']) / ('calibration-' + runner.plan['runId'])
        self.sequence = 0

    def authority(self):
        if (self.q.evidence / 'stop-request.json').exists():
            raise InterruptedError('campaign-lifecycle-stop-requested')
        self.q.admission('monitor')
        require(self.q.remaining() > 0, 'original-budget-exhausted')

    def heartbeat(self):
        self.authority()
        # A native command owns inFlight; reuse the original observation guard.
        self.q.guard()

    def request(self, role, method, query='', *, object_key=False, payload=None, extra=None, maximum=CONTROL_BYTES):
        self.authority()
        require(all(credentials(self.config[r]['credentials']) == self.keys[r] for r in IDENTITIES), 'credentials-changed')
        self.sequence += 1
        body = self.directory / ('response-' + str(self.sequence) + '.bin')
        header = self.directory / ('headers-' + str(self.sequence) + '.txt')
        fd = os.open(body, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600); os.close(fd)
        access, secret = self.keys[role]
        digest = self.archive['sha256'] if payload is not None else hashlib.sha256(b'').hexdigest()
        fields = {'x-amz-content-sha256': digest, 'Expect': '', **(extra or {})}
        # Restricted credential alphabet makes curl config quoting unambiguous.
        data = 'user = "' + access + ':' + secret + '"\n' + ''.join('header = "' + k + ': ' + v + '"\n' for k, v in fields.items())
        url = self.config['endpoint'] + '/' + self.config['bucket'] + ('/' + self.config['key'] if object_key else '') + query
        args = [self.config['curl']['path'], '-q', '--config', '-', '--silent', '--show-error', '--fail',
            '--proxy', '', '--noproxy', '*', '--proto', '=https', '--proto-redir', '=https', '--max-redirs', '0',
            '--retry', '0', '--connect-timeout', '10', '--max-time', str(self.config['requestTimeoutSeconds']),
            '--max-filesize', str(maximum), '--cacert', self.config['ca']['path'], '--aws-sigv4', 'aws:amz:fr-par:s3',
            '--request', method, '--dump-header', '-', '--output', str(body)]
        if payload is not None:
            args += ['--upload-file', str(payload)]
        args.append(url)
        self.q.run(args, data=data, stdout_file=header, maximum_output_bytes=CONTROL_BYTES,
            timeout=self.config['requestTimeoutSeconds'], heartbeat=self.heartbeat)
        require(body.stat().st_size <= maximum, 'response-size')
        return body, headers(header, 200)

    def bucket_checks(self):
        body, _ = self.request('validator', 'GET', '?versioning')
        require(one(xml(body.read_bytes()), 'Status') == 'Enabled', 'versioning-required')
        body, _ = self.request('validator', 'GET', '?object-lock')
        root = xml(body.read_bytes())
        require(one(root, 'ObjectLockEnabled') == 'Enabled' and one(root, 'Mode') == 'COMPLIANCE'
            and not root.findall('.//Years') and one(root, 'Days').isdigit()
            and int(one(root, 'Days')) >= 30, 'object-lock-required')
        body, _ = self.request('validator', 'GET', '?encryption')
        require(one(xml(body.read_bytes()), 'SSEAlgorithm') == 'AES256', 'aes256-required')
        body, _ = self.request('validator', 'GET', '?versions&prefix=' + self.config['key'] + '&max-keys=1')
        root = xml(body.read_bytes())
        require(one(root, 'IsTruncated') == 'false' and not root.findall('.//Version')
            and not root.findall('.//DeleteMarker'), 'key-already-used')

    def pod(self):
        self.q.observe()
        ns = self.q.record['target']['namespace']
        name = 'access-telemetry-postgresql-0'
        pod = lifecycle.strict(self.q.kube('get', 'pod', name, '-n', ns, '-o', 'json', timeout=10).stdout)
        rid = 'Pod/' + ns + '/' + name
        owned = [r for r in self.q.record['resources'] if r['id'] == rid]
        require(len(owned) == 1 and pod['metadata']['uid'] == owned[0]['uid']
            and pod['metadata']['namespace'] == ns and pod['metadata']['name'] == name, 'owned-postgres-pod')
        containers = [c for c in pod['spec']['containers'] if c['name'] == 'postgresql']
        require(len(containers) == 1 and containers[0]['image'] ==
            'docker.io/library/postgres:18.6-trixie@sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722', 'postgres-image')
        return ns, name, pod['metadata']['uid']

    def remote_headroom(self, pending_bytes, *, refresh=True):
        if refresh:
            self.q.check_storage()
        observed = self.q.record['storageObservation']['allocations']['backup']
        require(type(observed['usedBytes']) is int and observed['usedBytes'] >= 0
            and observed['usedBytes'] + pending_bytes <= self.q.plan['allocations']['backup'] * 4 // 5,
            'remote-backup-headroom')

    def capture(self):
        require(self.q.record.get('measurementQuiesced') is True
            and self.q.record['measurementQuiescence']['proof']['state'] == 'verified', 'verified-quiescence-required')
        self.q.check_storage()
        self.remote_headroom(self.config['maximumArchiveBytes'], refresh=False)
        workspace = self.q.record['storageObservation']['allocations']['host-workspace']
        footprint = 2 * self.config['maximumArchiveBytes'] + OVERHEAD_BYTES
        allocation = self.q.plan['allocations']['host-workspace']
        require(workspace['usedBytes'] + footprint <= allocation * 4 // 5
            and shutil.disk_usage(self.directory).free >= footprint, 'staging-headroom')
        ns, name, uid = self.pod()
        path = self.directory / 'postgresql.dump'
        self.q.kube('exec', '-n', ns, 'pod/' + name, '-c', 'postgresql', '--', 'pg_dump', '--no-password',
            '--format=custom', '--dbname=memories_access_telemetry', stdout_file=path,
            maximum_output_bytes=self.config['maximumArchiveBytes'], timeout=min(600, self.q.remaining()), heartbeat=self.heartbeat)
        self.archive = file_hashes(path, self.config['maximumArchiveBytes'], self.authority)
        with path.open('rb') as stream:
            require(stream.read(5) == b'PGDMP', 'custom-archive-required')
        require(self.pod() == (ns, name, uid), 'postgres-pod-replaced')
        validity = self.q.kube('exec', '--stdin', '-n', ns, 'pod/' + name, '-c', 'postgresql', '--',
            'pg_restore', '--list', data_file=path, maximum_output_bytes=CONTROL_BYTES * 16,
            timeout=min(300, self.q.remaining()), heartbeat=self.heartbeat)
        require(self.pod() == (ns, name, uid) and bool(validity.stdout.strip()), 'archive-validation')
        # Decode every archive data block without connecting to a database.
        self.q.kube('exec', '--stdin', '-n', ns, 'pod/' + name, '-c', 'postgresql', '--',
            'pg_restore', '--exit-on-error', '--file=/dev/null', data_file=path,
            maximum_output_bytes=CONTROL_BYTES, timeout=min(300, self.q.remaining()), heartbeat=self.heartbeat)
        require(self.pod() == (ns, name, uid), 'postgres-pod-replaced')
        require(file_hashes(path, self.config['maximumArchiveBytes'], self.authority) == self.archive, 'archive-changed')
        return path, {'namespace': ns, 'podName': name, 'podUid': uid, 'container': 'postgresql',
            'nativeArchiveListSha256': hashlib.sha256(validity.stdout.encode()).hexdigest(),
            'nativeArchiveDecodeVerified': True, 'actualRestorePerformed': False}

    def execute(self):
        self.authority()
        require(self.q.record.get('measurementQuiesced') is True
            and self.q.record['measurementQuiescence']['proof']['state'] == 'verified', 'verified-quiescence-required')
        require('s3Backup' not in self.q.record and not self.directory.exists(), 'attempt-consumed-or-uncertain')
        self.directory.mkdir(mode=0o700)
        self.q.record['s3Backup'] = {'schema': SCHEMA, 'state': 'started', 'verified': False,
            'directory': str(self.directory), 'runId': self.q.plan['runId'], 'profileSha256': self.q.plan['profileSha256']}
        self.q.save()
        try:
            self.keys = {r: credentials(self.config[r]['credentials']) for r in IDENTITIES}
            require(self.keys['writer'][0] != self.keys['validator'][0]
                and self.keys['writer'][1] != self.keys['validator'][1], 'actual-credential-alias')
            version = self.q.run([self.config['curl']['path'], '-q', '--version'], timeout=10, maximum_output_bytes=CONTROL_BYTES).stdout
            require(version.startswith('curl 8.18.0 ') and 'https' in version.lower(), 'curl-version')
            self.bucket_checks()
            archive, native = self.capture()
            self.authority()
            self.remote_headroom(self.archive['size'])
            now = lifecycle.clock(self.q.plan)
            until = lifecycle.utc(now + 31 * 86400000)
            binding = {**DESTINATION, 'key': self.config['key'], 'runId': self.q.plan['runId'],
                'planSha256': self.q.record['planSha256'], 'profileSha256': self.q.plan['profileSha256'], **self.archive}
            # fsync durable intent BEFORE the only mutating remote command.
            self.q.record['s3Backup'].update(state='upload-intent', uploadIntent={**binding, 'retainUntilUtc': until}, nativeArchive=native)
            self.q.save()
            _, uploaded = self.request('writer', 'PUT', object_key=True, payload=archive, extra={
                'If-None-Match': '*', 'Content-MD5': self.archive['contentMd5'], 'x-amz-server-side-encryption': 'AES256',
                'x-amz-object-lock-mode': 'COMPLIANCE', 'x-amz-object-lock-retain-until-date': until,
                'x-amz-meta-run-id': binding['runId'], 'x-amz-meta-profile-sha256': binding['profileSha256'],
                'x-amz-meta-plan-sha256': binding['planSha256'],
                'x-amz-meta-content-sha256': binding['sha256'], 'x-amz-meta-project-id': binding['projectId']})
            version_id = uploaded.get('x-amz-version-id', '')
            require(re.fullmatch('[A-Za-z0-9._~+-]{1,256}', version_id) and version_id != 'null', 'immutable-version-required')
            self.q.record['s3Backup'].update(state='uploaded-unverified', versionId=version_id)
            self.q.save()
            from urllib.parse import quote
            query = '?versionId=' + quote(version_id, safe='')
            downloaded, actual = self.request('validator', 'GET', query, object_key=True, maximum=self.config['maximumArchiveBytes'])
            require(actual.get('x-amz-version-id') == version_id and actual.get('x-amz-server-side-encryption') == 'AES256'
                and actual.get('content-length') == str(binding['size'])
                and actual.get('x-amz-meta-run-id') == binding['runId']
                and actual.get('x-amz-meta-profile-sha256') == binding['profileSha256']
                and actual.get('x-amz-meta-plan-sha256') == binding['planSha256']
                and actual.get('x-amz-meta-content-sha256') == binding['sha256']
                and actual.get('x-amz-meta-project-id') == binding['projectId'], 'object-properties')
            require(file_hashes(downloaded, self.config['maximumArchiveBytes'], self.authority) == self.archive
                and file_hashes(archive, self.config['maximumArchiveBytes'], self.authority) == self.archive, 'readback-content')
            body, _ = self.request('validator', 'GET', '?retention&versionId=' + quote(version_id, safe=''), object_key=True)
            root = xml(body.read_bytes())
            retain = one(root, 'RetainUntilDate')
            try:
                retained_until = datetime.fromisoformat(retain.replace('Z', '+00:00'))
                require(retained_until.tzinfo is not None, 'retention-date')
                retain_ms = int(retained_until.timestamp() * 1000)
            except (ValueError, OverflowError):
                require(False, 'retention-date')
            self.authority()
            require(one(root, 'Mode') == 'COMPLIANCE' and retain_ms >= lifecycle.clock(self.q.plan) + 30 * 86400000
                and retained_until >= datetime.fromisoformat(until.replace('Z', '+00:00')), 'retention-too-short')
            receipt = {'schema': SCHEMA, **binding, 'versionId': version_id, 'encryption': 'AES256',
                'retentionMode': 'COMPLIANCE', 'retainUntilUtc': retain, 'verified': True, 'nativeArchive': native,
                'qualified': False, 'restoreCredit': False, 'C1Credit': False, 'C2Credit': False, 'ProductionCredit': False, 'A41Credit': False}
            path = self.q.evidence / 's3-backup-verification.json'
            lifecycle.atomic(path, receipt, exclusive=True)
            self.q.record['s3Backup'].update(state='verified', verified=True, receipt={'path': str(path), 'sha256': lifecycle.sha(path)})
            self.q.save()
            return receipt
        except BaseException:
            self.q.record['s3Backup'].update(state='unverified', verified=False,
                uncertainUpload='uploadIntent' in self.q.record['s3Backup'], disposition='retain-primary-and-staging')
            self.q.save()
            raise
        finally:
            self.keys = {}


def verified(runner):
    """A saved status label alone cannot authorize removing the primary copy."""
    saved = runner.record.get('s3Backup', {})
    if saved.get('verified') is not True or saved.get('state') != 'verified':
        return False
    ref = saved.get('receipt', {})
    try:
        require(ref['path'] == str(runner.evidence / 's3-backup-verification.json'), 'receipt-path')
        value = lifecycle.strict(lifecycle.read_private(ref['path'], ref['sha256']))
        config = configuration(runner.plan)
        archive_fields = {'size', 'sha256', 'contentMd5'}
        binding_fields = {*DESTINATION, 'key', 'runId', 'planSha256', 'profileSha256', *archive_fields}
        credits = {'qualified', 'restoreCredit', 'C1Credit', 'C2Credit', 'ProductionCredit', 'A41Credit'}
        require(set(value) == {'schema', *binding_fields, 'versionId', 'encryption', 'retentionMode',
            'retainUntilUtc', 'verified', 'nativeArchive', *credits}, 'receipt-shape')
        intent = saved['uploadIntent']
        require(set(intent) == {*binding_fields, 'retainUntilUtc'}
            and all(value[k] == intent[k] for k in binding_fields), 'receipt-upload-intent')
        require(type(value['size']) is int and 0 < value['size'] <= config['maximumArchiveBytes']
            and re.fullmatch('[0-9a-f]{64}', value['sha256'])
            and len(base64.b64decode(value['contentMd5'], validate=True)) == 16, 'receipt-archive')
        require(value['encryption'] == 'AES256' and value['retentionMode'] == 'COMPLIANCE'
            and all(value[k] is False for k in credits), 'receipt-security')
        actual_until = datetime.fromisoformat(value['retainUntilUtc'].replace('Z', '+00:00'))
        requested_until = datetime.fromisoformat(intent['retainUntilUtc'].replace('Z', '+00:00'))
        require(actual_until.tzinfo is not None and requested_until.tzinfo is not None
            and actual_until >= requested_until, 'receipt-retention')
        native = value['nativeArchive']
        require(isinstance(native, dict) and set(native) == {'namespace', 'podName', 'podUid', 'container',
            'nativeArchiveListSha256', 'nativeArchiveDecodeVerified', 'actualRestorePerformed'}
            and native == saved['nativeArchive'] and native['namespace'] == runner.record['target']['namespace']
            and native['podName'] == 'access-telemetry-postgresql-0' and native['container'] == 'postgresql'
            and native['nativeArchiveDecodeVerified'] is True and native['actualRestorePerformed'] is False
            and re.fullmatch('[0-9a-f]{64}', native['nativeArchiveListSha256'])
            and any(r['id'] == 'Pod/' + native['namespace'] + '/' + native['podName']
                and r['uid'] == native['podUid'] for r in runner.record['resources']), 'receipt-native-archive')
        require(value['schema'] == SCHEMA and value['verified'] is True and value['runId'] == runner.plan['runId']
            and value['planSha256'] == runner.record['planSha256']
            and value['profileSha256'] == runner.plan['profileSha256'] and value['versionId'] == saved['versionId']
            and all(value[k] == v for k, v in DESTINATION.items()) and value['key'] == config['key'], 'receipt-binding')
        return True
    except (ValueError, OSError, KeyError, TypeError, AttributeError, OverflowError):
        return False
