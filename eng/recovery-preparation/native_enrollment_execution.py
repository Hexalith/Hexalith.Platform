#!/usr/bin/env python3
"""Dormant enrollment/execution safeguards; inert synthetic fixtures only.

Public live entry points refuse before inspecting inputs. Public metadata and
matching hashes establish integrity only. No certificate, CA, key, kubeconfig,
default trust store, OpenSSL configuration, socket or executable is loaded.
TLS requirements below are requirements for a future separately reviewed
transport; synthetic qualification neither implements nor authenticates TLS.
"""

from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import tempfile
import time
import types


def read_source_bytes(path, maximum=1048576):
    """Compile-source input only: bounded, regular, single-link, no-follow."""
    path = Path(path)
    if not path.is_absolute() or '..' in path.parts or maximum <= 0:
        raise ValueError('source-custody-refused')
    directory = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
    source = None
    try:
        for part in path.parts[1:-1]:
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC,
                            dir_fd=directory)
            os.close(directory)
            directory = child
            info = os.fstat(directory)
            mode = stat.S_IMODE(info.st_mode)
            sticky_root = info.st_uid == 0 and bool(mode & stat.S_ISVTX)
            if info.st_uid not in (0, os.getuid()) or (mode & 0o022 and not sticky_root):
                raise ValueError('source-custody-refused')
        source = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC,
                         dir_fd=directory)
        before = os.fstat(source)
        if (not stat.S_ISREG(before.st_mode) or before.st_nlink != 1 or
                before.st_uid not in (0, os.getuid()) or stat.S_IMODE(before.st_mode) & 0o022 or
                not 0 <= before.st_size <= maximum):
            raise ValueError('source-custody-refused')
        raw = bytearray()
        while len(raw) <= maximum:
            chunk = os.read(source, min(65536, maximum + 1 - len(raw)))
            if not chunk:
                break
            raw.extend(chunk)
        after = os.fstat(source)
        fields = ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns', 'st_ctime_ns',
                  'st_mode', 'st_uid', 'st_gid', 'st_nlink')
        if (len(raw) != before.st_size or len(raw) > maximum or
                any(getattr(before, key) != getattr(after, key) for key in fields)):
            raise ValueError('source-custody-refused')
        return bytes(raw)
    finally:
        if source is not None:
            os.close(source)
        os.close(directory)


def load_source(path, name):
    """Load exactly one fixed sibling from checked source, never bytecode."""
    raw = read_source_bytes(path)
    module = types.ModuleType(name)
    module.__file__ = str(path)
    exec(compile(raw, str(path), 'exec'), module.__dict__)
    module.__checked_source_sha256__ = hashlib.sha256(raw).hexdigest()
    return module


SOURCE = Path(__file__).absolute()
_adapter = load_source(SOURCE.with_name('native_measurement_adapter.py'),
                       'hexalith_native_enrollment_fixed_adapter')
_SYNTHETIC_REQUEST = _adapter.SyntheticTransport.request
_LOADED_SELF_SHA256 = hashlib.sha256(read_source_bytes(SOURCE)).hexdigest()
Refusal, require = _adapter.Refusal, _adapter.require
SUPPLEMENT_SHA256 = 'a7b9bf06139cec3022fbced5fe182b42c773b9a7099c40c84162893a766e2631'
INERT_KIND = 'inert-static-client-pair-v1'
INERT_PREFIX = b'HEXALITH-INERT-SYNTHETIC-CREDENTIAL-v1\n'
MAX_CREDENTIAL = 4096
MAX_BINDING = 65536
WITNESSES = ('nativeGrant', 'revocation', 'rights', 'breakGlass', 'cnpgController',
             'measurementApproval')
ENROLLMENT_FIELDS = ('serverTrust', 'clientIdentity', *WITNESSES,
                     'credentialDelivery', 'outputCustody')
SOURCE_NAMES = ('native_measurement_adapter.py', 'test_native_measurement_adapter.py',
                'native_https_transport.py', 'test_native_https_transport.py',
                'native_enrollment_execution.py', 'test_native_enrollment_execution.py')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def _canonical(value):
    remaining = [8192]
    def check(item, depth=0):
        remaining[0] -= 1
        require(depth <= 16 and remaining[0] >= 0, 'json-shape-limit')
        kind = type(item)
        if kind is dict:
            require(len(item) <= 512, 'json-shape-limit')
            for key, child in item.items():
                require(type(key) is str and len(key) <= 128, 'inert-json-key')
                check(child, depth + 1)
        elif kind is list:
            require(len(item) <= 512, 'json-shape-limit')
            for child in item:
                check(child, depth + 1)
        elif kind is str:
            require(len(item) <= MAX_BINDING, 'json-string-limit')
        elif kind is int:
            require(-(2 ** 63) <= item < 2 ** 63, 'json-integer-limit')
        else:
            require(kind in (bool, type(None)), 'inert-json-value')
    check(value)
    return json.dumps(value, sort_keys=True, separators=(',', ':'), allow_nan=False).encode('ascii')


def tls_requirements():
    """Explicit compatibility requirements; never construct a TLS context."""
    return {
        'opensslVersion': 'OpenSSL 3.5.5 27 Jan 2026',
        'opensslVersionInfo': [3, 5, 0, 5, 0],
        'minimumTlsVersion': 'TLSv1.3', 'maximumTlsVersion': 'TLSv1.3',
        'verifyMode': 'CERT_REQUIRED', 'verifyFlags': ['VERIFY_X509_STRICT'],
        'checkHostname': True, 'peerIpSan': _adapter.HOST,
        'caSource': 'explicit-bound-ca-only', 'peerPin': 'sha256-spki-required',
        'clientIdentity': 'one-explicit-static-certificate-key-pair',
        'defaultTrustLoading': False, 'opensslDefaultConfigurationLoading': False,
        'environmentConfiguration': False, 'automaticProviderLoading': False,
        'providerAndConfigurationBytes': 'independently-bound-before-future-activation',
        'compression': False, 'renegotiation': False, 'sessionResumption': False,
        'alpn': ['http/1.1'], 'handshakeSeconds': _adapter.REQUEST_SECONDS,
        'revocation': 'complete-independent-current-witness-required',
        'liveTlsImplemented': False, 'liveHandshakeQualified': False,
    }


def runtime_requirements(inventory_sha256):
    """Nonsecret fixture compatibility; the digest never attests a runtime."""
    require(type(inventory_sha256) is str and _adapter.DIGEST.fullmatch(inventory_sha256),
            'runtime-binding')
    return {'inventorySha256': inventory_sha256, 'interpreter': '/usr/bin/python3.14',
            'flags': ['-I', '-S', '-B'], 'tlsRequirements': tls_requirements(),
            'qualification': 'inert-profile-comparison-only'}


def _source_bindings():
    value = {name: {'bytes': len(raw), 'sha256': sha(raw)} for name in SOURCE_NAMES
             for raw in (read_source_bytes(SOURCE.with_name(name)),)}
    require(value['native_measurement_adapter.py']['sha256'] == _adapter.__checked_source_sha256__
            and value['native_enrollment_execution.py']['sha256'] == _LOADED_SELF_SHA256,
            'loaded-source-drift')
    return value


def binding_template(runtime_inventory_sha256):
    """Return an incomplete exact schema. Null evidence remains a blocker."""
    return {'kind': 'hexalith.synthetic-native-enrollment-execution-binding',
            'schemaVersion': 1, 'recoveryId': _adapter.RID,
            'preparedAt': _adapter.PREPARED, 'policyCeiling': _adapter.CEILING,
            'sealedSupplementSha256': SUPPLEMENT_SHA256,
            'sourceBindings': _source_bindings(),
            'runtime': runtime_requirements(runtime_inventory_sha256),
            'contract': _adapter.contract(),
            'enrollment': dict.fromkeys(ENROLLMENT_FIELDS),
            'syntheticOnly': True, 'operationalGate': 'closed'}


def _keys(value, names):
    require(type(value) is dict and all(type(key) is str for key in value)
            and set(value) == set(names), 'typed-schema')


def _digest(value):
    require(type(value) is str and _adapter.DIGEST.fullmatch(value), 'typed-digest')


def _label(value):
    require(type(value) is str and re.fullmatch(r'synthetic:[a-z0-9-]{1,64}', value),
            'inert-identity-required')


def _timestamp(value):
    require(type(value) is str and re.fullmatch(r'2026-10-(?:15|16)T\d\d:\d\d:\d\dZ', value),
            'typed-utc')
    result = datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(_prepared() <= result <= _ceiling(), 'witness-time-scope')
    return result


def _prepared():
    return datetime.fromisoformat(_adapter.PREPARED.replace('Z', '+00:00'))


def _ceiling():
    return datetime.fromisoformat(_adapter.CEILING.replace('Z', '+00:00'))


def _rights():
    return [{'method': item['method'], 'path': item['path']} for item in _adapter.request_plan()]


def _validate_populated(enrollment, missing, evidence_keys):
    """Missing evidence never excuses malformed facts that were supplied."""
    fixed = {
        'serverTrust': {'endpoint': _adapter.ENDPOINT, 'ipSan': _adapter.HOST},
        'clientIdentity': {'kind': INERT_KIND},
        'credentialDelivery': {'kind': INERT_KIND, 'singleUse': True,
                               'channel': 'protected-in-memory-inert-fixture'},
        'outputCustody': {'rootMode': 0o700, 'slot': 'measurement-output', 'directoryMode': 0o700,
                          'fileMode': 0o600, 'successFile': 'receipt.json', 'failureFile': 'failure.json',
                          'maximumBytes': _adapter.MAX_OUTPUT, 'attempts': 1, 'syntheticScratchOnly': True},
    }
    evidence_fixed = {
        'nativeGrant': {'independent': True},
        'revocation': {'completeLineage': True, 'state': 'not-revoked-in-fixture'},
        'rights': {'allowed': _rights()},
        'breakGlass': {'independent': True},
        'cnpgController': {'watchNamespaces': ['keycloak', 'cnpg-system']},
        'measurementApproval': {'separateFromGrant': True},
    }
    for name in WITNESSES:
        fixed[name] = {'kind': 'synthetic-independent-' + name, 'fixtureOnly': True,
                       'provenance': 'inert-independent-witness-fixture'}

    def fields(name, values, constants):
        for key, value in values.items():
            if value is None:
                continue
            if key in constants:
                require(_canonical(value) == _canonical(constants[key]), 'supplied-field-semantics')
            elif key.endswith('Sha256') or key == 'sha256':
                _digest(value)
            elif key in ('validFrom', 'validUntil', 'observedAt'):
                _timestamp(value)
            elif key in ('subject', 'issuer', 'approvalId', 'name'):
                _label(value)
            elif key in ('currentGrantUid', 'uid'):
                require(type(value) is str and _adapter.UUID.fullmatch(value), 'typed-uid')
            elif key == 'groups':
                require(type(value) is list and 1 <= len(value) <= 8, 'client-groups')
                for group in value:
                    _label(group)
                require(len(value) == len(set(value)), 'client-groups')
            elif key in ('rootDevice', 'rootInode', 'rootUid'):
                require(type(value) is int and value >= 0, 'typed-output-custody')
            elif key == 'bytes':
                require(type(value) is int and len(INERT_PREFIX) < value <= MAX_CREDENTIAL,
                        'typed-credential-size')
            elif key == 'evidence':
                require(type(value) is dict and set(value) <= set(evidence_keys[name]), 'typed-schema')
                fields(name, value, evidence_fixed[name])
            else:
                raise Refusal('supplied-field-semantics')

    for name, item in enrollment.items():
        if item is None:
            continue
        fields(name, item, fixed[name])
        start = 'validFrom' if name == 'clientIdentity' else 'observedAt'
        if item.get(start) is not None and item.get('validUntil') is not None:
            require(_timestamp(item[start]) < _timestamp(item['validUntil']), 'supplied-validity')
    client, server = enrollment['clientIdentity'], enrollment['serverTrust']
    subject = None if any(path.startswith('enrollment.clientIdentity') for path in missing) else sha(_canonical(client))
    scope = None if any(path.startswith('enrollment.serverTrust') for path in missing) else sha(_canonical({
        'contract': _adapter.contract(), 'serverTrust': server, 'tlsRequirements': tls_requirements()}))
    artifacts = []
    for name in WITNESSES:
        item = enrollment[name]
        if item is None:
            continue
        for key, expected in (('subjectSha256', subject), ('scopeSha256', scope)):
            if expected is not None and item.get(key) is not None:
                require(item[key] == expected, 'witness-identity-or-scope')
        if item.get('artifactSha256') is not None:
            artifacts.append(item['artifactSha256'])
    require(len(artifacts) == len(set(artifacts)), 'separate-witnesses-required')
    delivery = enrollment['credentialDelivery']
    if client is not None and delivery is not None and client.get('credentialSha256') is not None and delivery.get('sha256') is not None:
        require(client['credentialSha256'] == delivery['sha256'], 'credential-delivery-binding')


def _validate_enrollment(enrollment):
    """Validate fixture semantics separately from its byte-integrity anchor."""
    missing = []
    _keys(enrollment, ENROLLMENT_FIELDS)
    expected_keys = {
        'serverTrust': ('endpoint', 'ipSan', 'trustSourceSha256', 'caSha256', 'peerSpkiSha256'),
        'clientIdentity': ('kind', 'subject', 'issuer', 'groups', 'publicCertificateSha256',
                           'credentialSha256', 'validFrom', 'validUntil'),
        'credentialDelivery': ('kind', 'sha256', 'bytes', 'singleUse', 'channel'),
        'outputCustody': ('rootDevice', 'rootInode', 'rootUid', 'rootMode', 'slot',
                          'directoryMode', 'fileMode', 'successFile', 'failureFile',
                          'maximumBytes', 'attempts', 'syntheticScratchOnly'),
    }
    witness_keys = ('kind', 'fixtureOnly', 'provenance', 'artifactSha256', 'subjectSha256',
                    'scopeSha256', 'observedAt', 'validUntil', 'evidence')
    evidence_keys = {
        'nativeGrant': ('currentGrantUid', 'roleSha256', 'subjectsSha256', 'independent'),
        'revocation': ('completeLineage', 'state', 'lineageSha256'),
        'rights': ('allowed',),
        'breakGlass': ('custodySha256', 'independent'),
        'cnpgController': ('name', 'uid', 'watchNamespaces'),
        'measurementApproval': ('approvalId', 'separateFromGrant'),
    }
    for name in ENROLLMENT_FIELDS:
        item = enrollment[name]
        if item is None:
            missing.append('enrollment.' + name)
            continue
        fields = witness_keys if name in WITNESSES else expected_keys[name]
        require(type(item) is dict and set(item) <= set(fields), 'typed-schema')
        missing.extend('enrollment.' + name + '.' + key for key in fields
                       if item.get(key) is None)
        if name in WITNESSES and type(item.get('evidence')) is dict:
            require(set(item['evidence']) <= set(evidence_keys[name]), 'typed-schema')
            missing.extend('enrollment.' + name + '.evidence.' + key
                           for key in evidence_keys[name] if item['evidence'].get(key) is None)
    _validate_populated(enrollment, missing, evidence_keys)
    if missing:
        return missing
    server, client = enrollment['serverTrust'], enrollment['clientIdentity']
    _keys(server, expected_keys['serverTrust'])
    require(server['endpoint'] == _adapter.ENDPOINT and server['ipSan'] == _adapter.HOST,
            'server-scope')
    for field in ('trustSourceSha256', 'caSha256', 'peerSpkiSha256'):
        _digest(server[field])
    _keys(client, expected_keys['clientIdentity'])
    require(client['kind'] == INERT_KIND, 'credential-kind')
    _label(client['subject']); _label(client['issuer'])
    require(type(client['groups']) is list and 1 <= len(client['groups']) <= 8
            and len(client['groups']) == len(set(client['groups'])), 'client-groups')
    for group in client['groups']:
        _label(group)
    for field in ('publicCertificateSha256', 'credentialSha256'):
        _digest(client[field])
    require(_timestamp(client['validFrom']) < _timestamp(client['validUntil']), 'client-validity')
    subject = sha(_canonical(client))
    scope = sha(_canonical({'contract': _adapter.contract(), 'serverTrust': server,
                            'tlsRequirements': tls_requirements()}))
    artifacts = []
    for name in WITNESSES:
        item = enrollment[name]
        _keys(item, witness_keys)
        require(item['kind'] == 'synthetic-independent-' + name and item['fixtureOnly'] is True
                and item['provenance'] == 'inert-independent-witness-fixture', 'independent-witness')
        _digest(item['artifactSha256'])
        require(item['subjectSha256'] == subject and item['scopeSha256'] == scope,
                'witness-identity-or-scope')
        require(_timestamp(item['observedAt']) < _timestamp(item['validUntil']), 'witness-validity')
        artifacts.append(item['artifactSha256'])
        _keys(item['evidence'], evidence_keys[name])
    require(len(set(artifacts)) == len(WITNESSES), 'separate-witnesses-required')
    grant = enrollment['nativeGrant']['evidence']
    require(type(grant['currentGrantUid']) is str and _adapter.UUID.fullmatch(grant['currentGrantUid'])
            and grant['independent'] is True, 'grant-witness')
    _digest(grant['roleSha256']); _digest(grant['subjectsSha256'])
    revocation = enrollment['revocation']['evidence']
    require(revocation['completeLineage'] is True
            and revocation['state'] == 'not-revoked-in-fixture', 'revocation-witness')
    _digest(revocation['lineageSha256'])
    require(_canonical(enrollment['rights']['evidence']['allowed']) == _canonical(_rights()),
            'exact-read-rights')
    custody = enrollment['breakGlass']['evidence']
    _digest(custody['custodySha256'])
    require(custody['independent'] is True, 'break-glass-witness')
    cnpg = enrollment['cnpgController']['evidence']
    _label(cnpg['name'])
    require(type(cnpg['uid']) is str and _adapter.UUID.fullmatch(cnpg['uid'])
            and cnpg['watchNamespaces'] == ['keycloak', 'cnpg-system'], 'cnpg-scope')
    approval = enrollment['measurementApproval']['evidence']
    _label(approval['approvalId'])
    require(approval['separateFromGrant'] is True, 'separate-approval-witness')
    delivery = enrollment['credentialDelivery']
    _keys(delivery, expected_keys['credentialDelivery'])
    require(delivery['kind'] == INERT_KIND and delivery['sha256'] == client['credentialSha256']
            and type(delivery['bytes']) is int and len(INERT_PREFIX) < delivery['bytes'] <= MAX_CREDENTIAL
            and delivery['singleUse'] is True
            and delivery['channel'] == 'protected-in-memory-inert-fixture', 'credential-delivery-binding')
    output = enrollment['outputCustody']
    _keys(output, expected_keys['outputCustody'])
    for field in ('rootDevice', 'rootInode', 'rootUid'):
        require(type(output[field]) is int and output[field] >= 0, 'typed-output-custody')
    fixed = {'rootMode': 0o700, 'slot': 'measurement-output', 'directoryMode': 0o700,
             'fileMode': 0o600, 'successFile': 'receipt.json', 'failureFile': 'failure.json',
             'maximumBytes': _adapter.MAX_OUTPUT, 'attempts': 1, 'syntheticScratchOnly': True}
    require(_canonical({key: output[key] for key in fixed}) == _canonical(fixed), 'output-custody-scope')
    return []


def _validate_binding(raw, expected_sha256, runtime):
    require(type(raw) is bytes and len(raw) <= MAX_BINDING, 'bounded-binding')
    _digest(expected_sha256)
    require(sha(raw) == expected_sha256, 'binding-integrity')
    require(type(runtime) is dict, 'runtime-profile')
    runtime_raw = _canonical(runtime)
    require(type(runtime.get('inventorySha256')) is str, 'runtime-profile')
    require(runtime_raw == _canonical(runtime_requirements(runtime['inventorySha256'])),
            'openssl-or-runtime-profile')
    value = _adapter._strict_json(raw)
    expected = binding_template(runtime['inventorySha256'])
    _keys(value, expected)
    require(_canonical({key: item for key, item in value.items() if key != 'enrollment'}) ==
            _canonical({key: item for key, item in expected.items() if key != 'enrollment'}),
            'immutable-binding-drift')
    return value, _validate_enrollment(value['enrollment'])


def _receipt():
    now = datetime.now(timezone.utc)
    return {'kind': 'hexalith.synthetic-native-enrollment-execution-receipt',
            'schemaVersion': 1, 'observedAt': now.isoformat().replace('+00:00', 'Z'),
            'recoveryId': _adapter.RID, 'policyPreparedAt': _adapter.PREPARED,
            'policyCeiling': _adapter.CEILING, 'policyExpired': now >= _ceiling(),
            'qualification': 'refused', 'syntheticOnly': True, 'operationalGate': 'closed',
            'apiCalls': 0, 'syntheticRequests': 0, 'authenticationPerformed': False,
            'authorityAccepted': False, 'executionApprovalReady': False,
            'captureOrExecutionAuthorized': False, 'providerOrIamQualified': False,
            'credentialConsumed': False, 'outputRetained': False,
            'retainedReceiptAcceptance': 'provisional-until-post-write-validation',
            'missingFields': [], 'measurements': [],
            'unsupportedSelectors': list(_adapter.contract()['unsupportedSelectors'])}


def inspect_binding(raw, expected_sha256, runtime):
    """Offline inspection remains possible after expiry; never yields approval."""
    result = _receipt()
    try:
        _, missing = _validate_binding(raw, expected_sha256, runtime)
        result.update(qualification='inspection-only', missingFields=missing,
                      integrityMatched=True, witnessesAuthenticated=False)
    except Exception:
        result['reason'] = 'offline-binding-refused'
    return result


class SyntheticCredentialDelivery:
    """One protected inert credential fixture, wiped on its sole consumption."""

    __slots__ = ('_kind', '_payload', '_consumed')

    def __init__(self, credentials):
        require(type(credentials) in (list, tuple) and len(credentials) == 1, 'one-credential-required')
        item = credentials[0]
        _keys(item, ('kind', 'payload'))
        require(type(item['kind']) is str and item['kind'] == INERT_KIND and type(item['payload']) is bytes
                and len(INERT_PREFIX) < len(item['payload']) <= MAX_CREDENTIAL
                and item['payload'].startswith(INERT_PREFIX)
                and re.fullmatch(rb'[a-z0-9-]+', item['payload'][len(INERT_PREFIX):]),
                'inert-credential-required')
        self._kind, self._payload, self._consumed = item['kind'], bytearray(item['payload']), False

    def __repr__(self):
        return '<SyntheticCredentialDelivery redacted>'

    def consume(self, expected):
        kind, payload, consumed = self._kind, self._payload, self._consumed
        require(type(kind) is str and type(payload) is bytearray and type(consumed) is bool,
                'inert-delivery-storage')
        require(not consumed, 'credential-already-consumed')
        self._consumed = True
        try:
            require(kind == INERT_KIND and len(INERT_PREFIX) < len(payload) <= MAX_CREDENTIAL
                    and payload.startswith(INERT_PREFIX)
                    and re.fullmatch(rb'[a-z0-9-]+', payload[len(INERT_PREFIX):])
                    and kind == expected['kind'] and sha(payload) == expected['sha256']
                    and len(payload) == expected['bytes'], 'credential-delivery-refused')
        finally:
            payload[:] = b'\x00' * len(payload)


class SyntheticClock:
    """Bounded inert UTC samples; fixture time never replaces observed system UTC."""

    def __init__(self, samples):
        require(type(samples) in (list, tuple) and 1 <= len(samples) <= 256, 'clock-fixture')
        self.samples, self.index = tuple(samples), 0

    def sample(self):
        samples, index = self.samples, self.index
        require(type(samples) is tuple and type(index) is int, 'inert-clock-storage')
        require(1 <= len(samples) <= 256 and index >= 0, 'inert-clock-storage')
        value = samples[min(index, len(samples) - 1)]
        self.index = index + 1
        return value


_SYNTHETIC_CLOCK_SAMPLE = SyntheticClock.sample


def _private_directory(info):
    require(stat.S_ISDIR(info.st_mode) and info.st_uid == os.getuid()
            and stat.S_IMODE(info.st_mode) == 0o700, 'output-directory-custody')


def _identity(info):
    return (info.st_dev, info.st_ino, info.st_uid, info.st_gid, info.st_mode)


def _file_stamp(info):
    return (*_identity(info), info.st_nlink, info.st_size, info.st_mtime_ns, info.st_ctime_ns)


class SyntheticOutputCustody:
    """Own scratch only, never a caller-selected or real operational output root.

    Retained descriptors bind /tmp and the exclusively allocated private scratch
    directory. Every child/file operation is relative, exclusive and no-follow.
    A failed attempt is never retried, replaced, truncated or removed by this
    class. Context cleanup removes this synthetic scratch directory only.
    """

    def __init__(self):
        self._temporary = tempfile.TemporaryDirectory(prefix='hexalith-native-synthetic-', dir='/tmp')
        # Never let a pathname-based finalizer remove a later replacement.
        self._temporary._finalizer.detach()
        self.path = Path(self._temporary.name)
        self._root_name = self.path.name
        self._root = self._parent = self._slot = None
        self._retained_name = None
        self._retained_stamp = self._retained_sha256 = None
        self._attempted = self._write_attempted = False
        try:
            top = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
            try:
                self._parent = os.open('tmp', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC,
                                       dir_fd=top)
            finally:
                os.close(top)
            parent = os.fstat(self._parent)
            require(parent.st_uid == 0 and bool(parent.st_mode & stat.S_ISVTX), 'scratch-parent-custody')
            self._parent_identity = _identity(parent)
            self._root = os.open(self._root_name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC,
                                 dir_fd=self._parent)
            info = os.fstat(self._root)
            _private_directory(info)
            self._root_identity = _identity(info)
        except Exception:
            self.close()
            raise Refusal('synthetic-output-custody-refused') from None

    def _check_root(self):
        require(_identity(os.stat('/tmp', follow_symlinks=False)) == self._parent_identity
                and _identity(os.fstat(self._parent)) == self._parent_identity, 'scratch-parent-drift')
        actual = os.stat(self._root_name, dir_fd=self._parent, follow_symlinks=False)
        _private_directory(actual)
        require(_identity(actual) == self._root_identity
                and _identity(os.fstat(self._root)) == self._root_identity, 'scratch-root-drift')

    def _check(self):
        self._check_root()
        if self._slot is not None:
            actual = os.stat('measurement-output', dir_fd=self._root, follow_symlinks=False)
            _private_directory(actual)
            require(_identity(actual) == _identity(os.fstat(self._slot)) == self._slot_identity,
                    'output-slot-drift')
            require(set(os.listdir(self._slot)) == ({self._retained_name} if self._retained_name else set()),
                    'output-layout-drift')
            if self._retained_name is not None:
                info = os.stat(self._retained_name, dir_fd=self._slot, follow_symlinks=False)
                require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1
                        and info.st_uid == os.getuid() and stat.S_IMODE(info.st_mode) == 0o600
                        and info.st_size <= _adapter.MAX_OUTPUT
                        and _file_stamp(info) == self._retained_stamp, 'receipt-file-custody')
                descriptor = os.open(self._retained_name,
                    os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC, dir_fd=self._slot)
                try:
                    require(_file_stamp(os.fstat(descriptor)) == self._retained_stamp,
                            'receipt-file-replaced')
                    raw = bytearray()
                    while len(raw) <= _adapter.MAX_OUTPUT:
                        chunk = os.read(descriptor, min(65536, _adapter.MAX_OUTPUT + 1 - len(raw)))
                        if not chunk:
                            break
                        raw.extend(chunk)
                    require(len(raw) == info.st_size and sha(raw) == self._retained_sha256
                            and _file_stamp(os.fstat(descriptor)) == self._retained_stamp
                            and _file_stamp(os.stat(self._retained_name, dir_fd=self._slot,
                                                   follow_symlinks=False)) == self._retained_stamp,
                            'receipt-file-changed')
                finally:
                    os.close(descriptor)

    def binding(self):
        self._check()
        info = os.fstat(self._root)
        return {'rootDevice': info.st_dev, 'rootInode': info.st_ino, 'rootUid': info.st_uid,
                'rootMode': 0o700, 'slot': 'measurement-output', 'directoryMode': 0o700,
                'fileMode': 0o600, 'successFile': 'receipt.json', 'failureFile': 'failure.json',
                'maximumBytes': _adapter.MAX_OUTPUT, 'attempts': 1, 'syntheticScratchOnly': True}

    def reserve(self, expected):
        require(not self._attempted, 'output-attempt-already-used')
        self._attempted = True
        require(_canonical(expected) == _canonical(self.binding()), 'output-custody-binding')
        os.mkdir('measurement-output', mode=0o700, dir_fd=self._root)
        self._slot = os.open('measurement-output', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC,
                             dir_fd=self._root)
        self._slot_identity = _identity(os.fstat(self._slot))
        self._check()

    def _retain(self, result):
        require(self._slot is not None and not self._write_attempted, 'output-write-already-used')
        self._write_attempted = True
        self._check()
        raw = _canonical(result) + b'\n'
        require(len(raw) <= _adapter.MAX_OUTPUT, 'receipt-overflow')
        name = 'receipt.json' if result['qualification'] == 'pass' else 'failure.json'
        descriptor = os.open(name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC,
                             0o600, dir_fd=self._slot)
        self._retained_name = name
        try:
            before = os.fstat(descriptor)
            require(stat.S_ISREG(before.st_mode) and before.st_nlink == 1
                    and before.st_uid == os.getuid() and stat.S_IMODE(before.st_mode) == 0o600
                    and before.st_size == 0, 'receipt-file-custody')
            require(os.write(descriptor, raw) == len(raw), 'receipt-write-incomplete')
            os.fsync(descriptor)
            after = os.fstat(descriptor)
            actual = os.stat(name, dir_fd=self._slot, follow_symlinks=False)
            require(_identity(before) == _identity(after) == _identity(actual)
                    and after.st_nlink == actual.st_nlink == 1
                    and after.st_size == actual.st_size == len(raw), 'receipt-file-drift')
            self._retained_stamp, self._retained_sha256 = _file_stamp(after), sha(raw)
            self._check()
        finally:
            os.close(descriptor)

    def close(self):
        try:
            if self._root is not None:
                self._check_root()
                self._remove_owned_contents(self._root)
                self._check_root()
                os.rmdir(self._root_name, dir_fd=self._parent)
        except Exception:
            raise Refusal('synthetic-cleanup-custody-refused') from None
        finally:
            for name in ('_slot', '_root', '_parent'):
                descriptor = getattr(self, name, None)
                if descriptor is not None:
                    os.close(descriptor)
                    setattr(self, name, None)

    def _remove_owned_contents(self, directory):
        """Remove scratch children through retained descriptors, never aliases."""
        for name in os.listdir(directory):
            self._check_root()
            info = os.stat(name, dir_fd=directory, follow_symlinks=False)
            if stat.S_ISDIR(info.st_mode):
                child = os.open(name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC,
                                dir_fd=directory)
                try:
                    require(_identity(os.fstat(child)) == _identity(info), 'cleanup-child-replaced')
                    self._remove_owned_contents(child)
                    self._check_root()
                    require(_identity(os.stat(name, dir_fd=directory, follow_symlinks=False)) == _identity(info),
                            'cleanup-child-replaced')
                    os.rmdir(name, dir_fd=directory)
                finally:
                    os.close(child)
            else:
                os.unlink(name, dir_fd=directory)

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()


class _Boundaries:
    """Recheck sealed bindings, current/fixture UTC and monotonic clocks."""

    def __init__(self, raw, expected, runtime, clock, custody):
        require(clock is None or type(clock) is SyntheticClock, 'inert-clock-required')
        self.raw, self.expected, self.runtime, self.clock = raw, expected, runtime, clock
        self.custody = custody
        self.last_utc = self.last_monotonic = None
        self.start = time.monotonic()

    def check(self, deadline=None):
        value, missing = _validate_binding(self.raw, self.expected, self.runtime)
        require(not missing, 'missing-independent-witness-fixtures')
        self.custody._check()
        require(_canonical(value['enrollment']['outputCustody']) == _canonical(self.custody.binding()),
                'output-custody-binding-drift')
        # Clock enforcement follows every potentially slow source/custody read.
        now = datetime.now(timezone.utc) if self.clock is None else _SYNTHETIC_CLOCK_SAMPLE(self.clock)
        require(type(now) is datetime and now.tzinfo is timezone.utc, 'utc-clock-required')
        monotonic = time.monotonic()
        require(_prepared() <= now < _ceiling() and
                (self.last_utc is None or now >= self.last_utc), 'expired-or-regressed-utc')
        require(monotonic >= self.start and
                (self.last_monotonic is None or monotonic >= self.last_monotonic)
                and monotonic - self.start < _adapter.LANE_SECONDS
                and (deadline is None or monotonic < deadline), 'deadline-or-regressed-clock')
        client = value['enrollment']['clientIdentity']
        require(_timestamp(client['validFrom']) <= now < _timestamp(client['validUntil']), 'client-validity')
        for key in WITNESSES:
            witness = value['enrollment'][key]
            require(_timestamp(witness['observedAt']) <= now < _timestamp(witness['validUntil']), 'witness-validity')
        self.last_utc, self.last_monotonic = now, monotonic
        return value


def qualify_synthetic(raw, expected_sha256, runtime, delivery, transport, custody, clock=None):
    """Run one exact fixture lane. Authority and every operational gate stay closed."""
    result = _receipt()
    reserved = False
    try:
        value, missing = _validate_binding(raw, expected_sha256, runtime)
        result['missingFields'] = missing
        require(not missing, 'missing-independent-witness-fixtures')
        require(type(delivery) is SyntheticCredentialDelivery
                and type(transport) is _adapter.SyntheticTransport
                and type(transport.requests) is list and not transport.requests
                and type(transport.responses) is tuple
                and len(transport.responses) <= len(_adapter.request_plan())
                and type(custody) is SyntheticOutputCustody, 'inert-fixtures-required')
        boundaries = _Boundaries(raw, expected_sha256, runtime, clock, custody)
        boundaries.check()
        try:
            delivery.consume(value['enrollment']['credentialDelivery'])
        finally:
            result['credentialConsumed'] = delivery._consumed is True
        boundaries.check()
        custody.reserve(value['enrollment']['outputCustody'])
        reserved = True
        total = 0
        for request in _adapter.request_plan():
            boundaries.check()
            deadline = min(boundaries.start + _adapter.LANE_SECONDS,
                           time.monotonic() + _adapter.REQUEST_SECONDS)
            boundaries.check(deadline)
            require(type(transport.requests) is list and type(transport.responses) is tuple
                    and len(transport.responses) <= len(_adapter.request_plan()), 'inert-transport-storage')
            result['syntheticRequests'] += 1
            response = _SYNTHETIC_REQUEST(transport, request, deadline,
                                        min(_adapter.MAX_BODY, _adapter.MAX_TOTAL_BODY - total))
            boundaries.check(deadline)
            require(type(response) is _adapter.Response and type(response.status) is int
                    and type(response.content_type) is str and type(response.chunks) is tuple
                    and len(response.chunks) <= _adapter.MAX_ITEMS, 'inert-response-required')
            body = bytearray()
            for chunk in response.chunks:
                boundaries.check(deadline)
                require(type(chunk) is bytes and len(chunk) > 0
                        and len(body) + len(chunk) <= _adapter.MAX_BODY
                        and total + len(chunk) <= _adapter.MAX_TOTAL_BODY, 'response-overflow')
                body.extend(chunk)
                total += len(chunk)
            measurement = _adapter.sanitize(request, response.status, response.content_type, bytes(body))
            boundaries.check(deadline)
            result['measurements'].append({'selector': request['id'], 'output': measurement})
            require(len(_canonical(result)) + 1 <= _adapter.MAX_OUTPUT, 'receipt-overflow')
        boundaries.check()
        result.update(qualification='pass', bindingSha256=expected_sha256,
                      runtimeInventorySha256=runtime['inventorySha256'])
        require(len(_canonical(result)) + 1 <= _adapter.MAX_OUTPUT, 'receipt-overflow')
        boundaries.check()
    except Exception:
        result.update(qualification='refused', measurements=[], reason='synthetic-enrollment-execution-refused')
        result.pop('bindingSha256', None)
        result.pop('runtimeInventorySha256', None)
    if reserved:
        try:
            custody._retain(result)
            if result['qualification'] == 'pass':
                boundaries.check()
            result['outputRetained'] = True
        except Exception:
            result.update(qualification='refused', measurements=[], outputRetained=False,
                          reason='synthetic-output-custody-refused')
            result.pop('bindingSha256', None)
            result.pop('runtimeInventorySha256', None)
    return result


def enroll_live(*args, **kwargs):
    """Unconditional refusal before arguments, files, credentials or sockets."""
    raise Refusal('live-enrollment-execution-disabled-before-input-access')


def execute_live(*args, **kwargs):
    """Unconditional refusal before arguments, files, credentials or sockets."""
    raise Refusal('live-enrollment-execution-disabled-before-input-access')


def main():
    # Do not read argv, environment, configuration or attacker-supplied objects.
    print('{"operationalGate":"closed","authorityAccepted":false,"apiCalls":0,'
          '"reason":"live-enrollment-execution-disabled-before-input-access"}')
    return 1


if __name__ == '__main__':
    raise SystemExit(main())
