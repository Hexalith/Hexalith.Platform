#!/usr/bin/env python3
"""Offline, fixed-path byte verification. Never enrolls or executes a lane."""

from datetime import datetime, timezone
import argparse
import hashlib
import json
import os
from pathlib import Path
import stat
import types


SOURCE = Path(__file__).absolute()
ROOT = Path('/home/administrator/hexalith-recovery-evidence/4-0/20261016t103632z-native-enrollment-execution-revision-sealed')
PRIOR = ROOT.parent / '20261016t063900z-native-measurement-supplement'
PRIOR_SHA = 'a7b9bf06139cec3022fbced5fe182b42c773b9a7099c40c84162893a766e2631'
VERIFIER_SHA = 'd7e88423178d5b1f54dc03c5d939029a8176cfe467c95d15c1b0996279c975eb'
PREPARED = '2026-10-15T16:27:56Z'
CEILING = '2026-10-16T16:27:56Z'
STATUS = '/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/sprint-status.yaml'
OLD_STATUS_SHA = '1d66b846fb554deefa67acb44711671376d5d354f74558ffb9b34a87a638283d'
CURRENT_STATUS_SHA = '2d0c7c85dc259481180cd9c65fb327c32706e9ddbfa0c1a01fcd848da2d2d41e'
OBSERVED_STATUS_SHA = 'f6c25046d9db40c069cb183cf87f8fef19a0a952b601c87dbe690d0b0aff7e43'
NEW_SOURCES = {'native_enrollment_execution.py', 'test_native_enrollment_execution.py',
               'verify_enrollment_revision.py', 'test_verify_enrollment_revision.py'}
MANDATORY = {'preservation-before.json', 'prior-verifier-refusal.json',
             'enrollment-execution-binding-v1.json', 'evidence-gaps-v1.json',
             'source-inventory.json', 'NEXT-PREREQUISITE.md', 'README.md',
             'runtime-requirements-v1.json', 'review-summary.json',
             'preservation-after.json', 'concurrent-preservation-drift-v1.json'}
GAP_IDS = {'serverTrustSource', 'apiCaPublicIdentity', 'tlsPeerIdentity',
           'singlePublicClientIdentity', 'independentNativeGrant', 'completeRevocationLineage',
           'currentReadRights', 'protectedDeliveryCustody', 'runtimeOpenSslBehavior',
           'actualResponseCompatibility', 'priorPreservationDrift', 'exactOperationalApproval'}
EVIDENCE_ROOT = Path('/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-26')
PUBLIC_EVIDENCE = {
    '20261004t112212z-independent-native-access/independent-native-access.json':
        'baf8fa62efc17835e00c2a546f8ba8dd98a8457ad85720ed21cfe097c606dec6',
    '20261003t062816z-kubeadm-admin-credential/admin-credential-observation.json':
        'a611dc5da71a5d426fd9ee732870214a60729b8c3c94a040c235a04aa3598250',
    '20261001t210315z-native-authority/native-access.json':
        '65f3e446dadd34335f18a7842ffbf097adc2831a4975a9640cc6f48a37447197',
    '20261004t154618z-capability-assessment/authority-design.json':
        'fa6545e4fa267aeae760196d66596fb5a122ba774ba7dbd8c8303776748b03cd',
}
HISTORICAL_CLASSIFICATIONS = {
    '20261004t112212z-independent-native-access/independent-native-access.json':
        'historical-recovered-use-no-current-public-identity-or-rights',
    '20261003t062816z-kubeadm-admin-credential/admin-credential-observation.json':
        'historical-subject-issuer-validity-no-certificate-fingerprint',
    '20261001t210315z-native-authority/native-access.json':
        'historical-managementGrantLineageAuthenticated-false',
    '20261004t154618z-capability-assessment/authority-design.json':
        'design-actualSignedRepositoryOrOffsiteMirrorVerified-false',
}


class Refusal(ValueError):
    """Only fixed refusal codes leave the verifier."""


def require(ok, code):
    if not ok:
        raise Refusal(code)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), allow_nan=False)


def runtime_profile(inventory_sha):
    """Exact inert requirements; parity with the guard is regression tested."""
    return {'inventorySha256': inventory_sha, 'interpreter': '/usr/bin/python3.14',
        'flags': ['-I', '-S', '-B'], 'qualification': 'inert-profile-comparison-only',
        'tlsRequirements': {
            'opensslVersion': 'OpenSSL 3.5.5 27 Jan 2026', 'opensslVersionInfo': [3, 5, 0, 5, 0],
            'minimumTlsVersion': 'TLSv1.3', 'maximumTlsVersion': 'TLSv1.3',
            'verifyMode': 'CERT_REQUIRED', 'verifyFlags': ['VERIFY_X509_STRICT'],
            'checkHostname': True, 'peerIpSan': '192.168.1.30',
            'caSource': 'explicit-bound-ca-only', 'peerPin': 'sha256-spki-required',
            'clientIdentity': 'one-explicit-static-certificate-key-pair',
            'defaultTrustLoading': False, 'opensslDefaultConfigurationLoading': False,
            'environmentConfiguration': False, 'automaticProviderLoading': False,
            'providerAndConfigurationBytes': 'independently-bound-before-future-activation',
            'compression': False, 'renegotiation': False, 'sessionResumption': False,
            'alpn': ['http/1.1'], 'handshakeSeconds': 15,
            'revocation': 'complete-independent-current-witness-required',
            'liveTlsImplemented': False, 'liveHandshakeQualified': False}}


def load_prior_verifier():
    """Walk no-follow descriptors before compiling the fixed sealed verifier."""
    path = SOURCE.with_name('verify_native_supplement.py')
    fd = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        for part in path.parts[1:-1]:
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC, dir_fd=fd)
            os.close(fd)
            fd = child
            info = os.fstat(fd)
            require(info.st_uid in (0, os.getuid()) and not stat.S_IMODE(info.st_mode) & 0o022,
                    'unsafe-verifier-ancestry')
        source = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC, dir_fd=fd)
        with os.fdopen(source, 'rb') as stream:
            info = os.fstat(stream.fileno())
            require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1 and
                    info.st_uid in (0, os.getuid()) and not stat.S_IMODE(info.st_mode) & 0o022 and
                    0 < info.st_size <= 1048576, 'unsafe-verifier-source')
            raw = stream.read(1048577)
            after = os.fstat(stream.fileno())
            require((info.st_size, info.st_mtime_ns, info.st_ctime_ns) ==
                    (after.st_size, after.st_mtime_ns, after.st_ctime_ns) and sha(raw) == VERIFIER_SHA,
                    'prior-verifier-drift')
    finally:
        os.close(fd)
    module = types.ModuleType('fixed_sealed_offline_verifier')
    module.__file__ = str(path)
    exec(compile(raw, str(path), 'exec'), module.__dict__)
    return module


def check_private_layout(v, files):
    require(v.inventory_paths(ROOT) == {*files, 'packet-v1.json'}, 'revision-layout-drift')
    v.check_directories(ROOT, files)
    for path in (ROOT, *ROOT.rglob('*')):
        info = path.lstat()
        mode = 0o700 if stat.S_ISDIR(info.st_mode) else 0o600
        require(info.st_uid == os.getuid() and stat.S_IMODE(info.st_mode) == mode and
                not stat.S_ISLNK(info.st_mode) and
                (stat.S_ISDIR(info.st_mode) or stat.S_ISREG(info.st_mode) and info.st_nlink == 1),
                'revision-custody-drift')


def validate_baseline(v, before, prior_before, prior_files, prior_sources, current_status=None):
    """One explicit historical status drift is recorded, never silently accepted."""
    expected = set(prior_before) | set(prior_sources) | {str(PRIOR / name) for name in prior_files} | {str(PRIOR / 'packet-v1.json')}
    require(type(before) is dict and set(before) == expected and len(before) == 557,
            'preservation-members-drift')
    for name, record in before.items():
        if name in prior_before:
            original = prior_before[name]
            if name == STATUS:
                require(original['sha256'] == OLD_STATUS_SHA and record['sha256'] == CURRENT_STATUS_SHA,
                        'unrecognized-prior-status-drift')
            else:
                require(record['sha256'] == original['sha256'] and record['bytes'] == original['bytes'],
                        'historical-baseline-rewrite')
        v.check_record(Path(name), current_status if name == STATUS and current_status is not None else record)


def validate_sources(v, sources):
    expected = {str(SOURCE.with_name(name)) for name in NEW_SOURCES}
    require(type(sources) is dict and set(sources) == expected, 'revision-source-members-drift')
    for name, record in sources.items():
        v.check_record(Path(name), record)


def validate_binding(v, binding, sources, prior_sources, prior_payloads):
    require(type(binding) is dict and set(binding) == {'kind', 'schemaVersion', 'recoveryId',
            'priorSupplementSha256', 'liveActivationAllowed', 'authorityAccepted',
            'operationalGate', 'configuration', 'proposal', 'outputCustody'} and
            binding['kind'] == 'hexalith.offline-native-enrollment-execution-revision-binding' and
            type(binding['schemaVersion']) is int and binding['schemaVersion'] == 1 and
            binding['recoveryId'] == '20261015t162756z', 'binding-schema-drift')
    require(canonical(binding['configuration']) == canonical({'context': 'jpiquot@local',
            'endpoint': 'https://192.168.1.30:6443',
            'configSha256': 'f00b691a5886ef76e6be0d6e3efe4030251147b7bba74bb0c58fa1944e0fe86c',
            'actualConfigRead': False}), 'configuration-scope-drift')
    proposal = binding['proposal']
    require(type(proposal) is dict and set(proposal) == {'kind', 'schemaVersion', 'recoveryId',
            'preparedAt', 'policyCeiling', 'sealedSupplementSha256', 'sourceBindings',
            'runtime', 'contract', 'enrollment', 'syntheticOnly', 'operationalGate'} and
            proposal['kind'] == 'hexalith.synthetic-native-enrollment-execution-binding' and
            type(proposal['schemaVersion']) is int and proposal['schemaVersion'] == 1 and
            proposal['recoveryId'] == '20261015t162756z', 'proposal-schema-drift')
    original = v.strict_json(prior_payloads['adapter-binding-v2.json'])
    require(canonical(proposal['contract']) == canonical(original['contract']) and
            proposal['sealedSupplementSha256'] == PRIOR_SHA and
            proposal['preparedAt'] == PREPARED and proposal['policyCeiling'] == CEILING and
            proposal['syntheticOnly'] is True and proposal['operationalGate'] == 'closed',
            'proposal-scope-drift')
    expected = {}
    for name in ('native_measurement_adapter.py', 'test_native_measurement_adapter.py',
                 'native_https_transport.py', 'test_native_https_transport.py',
                 'native_enrollment_execution.py', 'test_native_enrollment_execution.py'):
        path = str(SOURCE.with_name(name) if name in NEW_SOURCES else v.SOURCE_ROOT / name)
        record = sources[path] if path in sources else prior_sources[path]
        expected[name] = {key: record[key] for key in ('bytes', 'sha256')}
    require(canonical(proposal['sourceBindings']) == canonical(expected), 'proposal-source-drift')
    require(canonical(proposal['runtime']) == canonical(runtime_profile(sha(prior_payloads['runtime-inventory.json']))),
            'proposal-runtime-drift')
    require(type(proposal['enrollment']) is dict and
            set(proposal['enrollment']) == {'serverTrust', 'clientIdentity', 'nativeGrant', 'revocation',
                'rights', 'breakGlass', 'cnpgController', 'measurementApproval',
                'credentialDelivery', 'outputCustody'} and
            all(value is None for value in proposal['enrollment'].values()),
            'unresolved-witnesses-must-remain-null')
    require(canonical(binding['outputCustody']) == canonical({'root': str(ROOT), 'child': 'measurement-output',
            'childMustRemainAbsent': True, 'directoryMode': 0o700, 'fileMode': 0o600,
            'slots': ['receipt.json', 'failure.json'], 'exclusiveSingleResult': True,
            'maximumBytes': 65536, 'attempts': 1, 'realSlotCreated': False}), 'output-plan-drift')
    require(not os.path.lexists(ROOT / 'measurement-output'), 'real-output-slot-present')


def validate_gaps(gaps):
    require(type(gaps) is dict and set(gaps) == {'kind', 'executionApprovalReady', 'authorityAccepted',
            'operationalGate', 'missingEvidence', 'historicalEvidenceOnly'} and
            gaps['kind'] == 'hexalith.offline-native-evidence-gap-ledger' and
            gaps['executionApprovalReady'] is False and gaps['authorityAccepted'] is False and
            gaps['operationalGate'] == 'closed', 'evidence-gap-schema')
    entries = gaps['missingEvidence']
    require(type(entries) is list and len(entries) == len(GAP_IDS), 'evidence-gap-members')
    names = []
    for entry in entries:
        require(type(entry) is dict and set(entry) == {'id', 'required'} and
                type(entry['id']) is str and type(entry['required']) is str and
                0 < len(entry['required']) <= 2048, 'evidence-gap-entry')
        names.append(entry['id'])
    require(set(names) == GAP_IDS and len(names) == len(set(names)), 'evidence-gap-members')
    entries = gaps['historicalEvidenceOnly']
    require(type(entries) is list and len(entries) == len(PUBLIC_EVIDENCE), 'historical-evidence-members')
    seen = set()
    for entry in entries:
        require(type(entry) is dict and set(entry) == {'path', 'sha256', 'classification'}, 'historical-evidence-schema')
        require(entry['path'] in {str(EVIDENCE_ROOT / name) for name in PUBLIC_EVIDENCE} and
                entry['path'] not in seen and
                entry['classification'] == HISTORICAL_CLASSIFICATIONS.get(str(Path(entry['path']).relative_to(EVIDENCE_ROOT))) and
                entry['sha256'] == PUBLIC_EVIDENCE[str(Path(entry['path']).relative_to(EVIDENCE_ROOT))],
                'historical-evidence-classification')
        seen.add(entry['path'])


def concurrent_status(v, record, before):
    require(type(record) is dict and set(record) == {'kind', 'path', 'baselineSha256',
            'currentRecord', 'initiatedByThisTask', 'commit'} and
            record['kind'] == 'hexalith.recorded-concurrent-preservation-drift' and
            record['path'] == STATUS and record['baselineSha256'] == CURRENT_STATUS_SHA and
            record['baselineSha256'] == before[STATUS]['sha256'] and
            record['currentRecord']['sha256'] == OBSERVED_STATUS_SHA and
            record['initiatedByThisTask'] is False and type(record['commit']) is str and
            len(record['commit']) == 40 and all(c in '0123456789abcdef' for c in record['commit']),
            'concurrent-status-binding')
    v.check_record(Path(STATUS), record['currentRecord'])
    return record['currentRecord']


def validate_after(after, before, status):
    require(type(after) is dict and set(after) == {'kind', 'baselineFiles', 'files',
            'unchangedFiles', 'knownConcurrentChanges', 'unexpectedChanges', 'passed', 'operationalGate'} and
            after['kind'] == 'hexalith.offline-preservation-after' and after['passed'] is True and
            after['operationalGate'] == 'closed' and after['unexpectedChanges'] == [] and
            type(after['baselineFiles']) is int and after['baselineFiles'] == len(before) and
            type(after['unchangedFiles']) is int and after['unchangedFiles'] == len(before) - 1 and
            after['knownConcurrentChanges'] == [STATUS] and
            canonical(after['files']) == canonical({**before, STATUS: status}), 'preservation-after-drift')


def verify(expected):
    require(type(expected) is str and len(expected) == 64 and all(c in '0123456789abcdef' for c in expected),
            'invalid-anchor')
    v = load_prior_verifier()
    manifest_raw = v.read_file(ROOT / 'packet-v1.json')
    require(sha(manifest_raw) == expected, 'revision-anchor-drift')
    manifest = v.strict_json(manifest_raw)
    require(set(manifest) == {'kind', 'schemaVersion', 'recoveryId', 'priorSupplementSha256',
            'policyPreparedAt', 'policyCeilingEnds', 'liveActivationAllowed', 'authorityAccepted',
            'operationalGate', 'activeBinding', 'files'} and
            type(manifest['schemaVersion']) is int and manifest['schemaVersion'] == 1 and
            manifest['recoveryId'] == '20261015t162756z' and
            manifest['kind'] == 'hexalith.native-enrollment-execution-revision' and
            manifest['priorSupplementSha256'] == PRIOR_SHA and manifest['policyPreparedAt'] == PREPARED and
            manifest['policyCeilingEnds'] == CEILING and manifest['liveActivationAllowed'] is False and
            manifest['authorityAccepted'] is False and manifest['operationalGate'] == 'closed',
            'revision-contract-drift')
    files = manifest['files']
    require(type(files) is dict and MANDATORY <= files.keys() and 1 <= len(files) <= 256, 'revision-payload-members')
    check_private_layout(v, files)
    payloads = {name: v.check_record(ROOT / v.safe_relative(name), record) for name, record in files.items()}
    prior_raw = v.read_file(PRIOR / 'packet-v1.json', True)
    require(sha(prior_raw) == PRIOR_SHA, 'prior-anchor-drift')
    prior_manifest = v.strict_json(prior_raw)
    prior_files = prior_manifest['files']
    require(v.inventory_paths(PRIOR) == {*prior_files, 'packet-v1.json'}, 'prior-layout-drift')
    v.check_directories(PRIOR, prior_files)
    prior_payloads = {name: v.check_record(PRIOR / v.safe_relative(name), record, True) for name, record in prior_files.items()}
    prior_before_raw = prior_payloads['preservation-before.json']
    require(sha(prior_before_raw) == v.PRESERVATION_SHA, 'prior-preservation-anchor')
    prior_before = v.strict_json(prior_before_raw)['files']
    v4_raw = v.read_file(v.V4 / 'packet-v4.json', True)
    require(sha(v4_raw) == v.V4_SHA and set(prior_before) == v.expected_preservation(v.strict_json(v4_raw)),
            'prior-preservation-members')
    prior_sources = v.strict_json(prior_payloads['source-inventory.json'])['files']
    before = v.strict_json(payloads['preservation-before.json'])['files']
    status = concurrent_status(v, v.strict_json(payloads['concurrent-preservation-drift-v1.json']), before)
    validate_baseline(v, before, prior_before, prior_files, prior_sources, status)
    validate_after(v.strict_json(payloads['preservation-after.json']), before, status)
    runtime = v.strict_json(prior_payloads['runtime-inventory.json'])
    v.validate_runtime(runtime)
    v.validate_sources(prior_sources)
    sources = v.strict_json(payloads['source-inventory.json'])['files']
    validate_sources(v, sources)
    binding_name = manifest['activeBinding']
    require(binding_name in {'enrollment-execution-binding-v' + str(i) + '.json' for i in range(1, 6)},
            'binding-revision-name')
    binding = v.strict_json(payloads[binding_name])
    require(binding['liveActivationAllowed'] is False and binding['authorityAccepted'] is False and
            binding['operationalGate'] == 'closed' and binding['priorSupplementSha256'] == PRIOR_SHA,
            'binding-gate-drift')
    validate_binding(v, binding, sources, prior_sources, prior_payloads)
    gaps = v.strict_json(payloads['evidence-gaps-v1.json'])
    validate_gaps(gaps)
    for name, digest in PUBLIC_EVIDENCE.items():
        raw = v.read_file(EVIDENCE_ROOT / name)
        require(sha(raw) == digest and payloads['public-evidence/' + name] == raw,
                'public-evidence-drift')
    # Re-read all inputs and layouts after expensive runtime checks. No before-only success.
    for name, record in files.items():
        v.check_record(ROOT / v.safe_relative(name), record)
    check_private_layout(v, files)
    require(sha(v.read_file(ROOT / 'packet-v1.json')) == expected and
            sha(v.read_file(PRIOR / 'packet-v1.json', True)) == PRIOR_SHA, 'late-manifest-drift')
    for name, record in prior_files.items():
        v.check_record(PRIOR / v.safe_relative(name), record, True)
    require(v.inventory_paths(PRIOR) == {*prior_files, 'packet-v1.json'}, 'late-prior-layout-drift')
    v.check_directories(PRIOR, prior_files)
    # Preserve the original verifier's whole-history layout checks without rewriting
    # its sealed before-map. The current status digest is separately pinned above.
    v.recheck_external(before={name: status if name == STATUS else before[name] for name in prior_before}, runtime=runtime, sources=prior_sources)
    validate_baseline(v, before, prior_before, prior_files, prior_sources, status)
    validate_sources(v, sources)
    for name, digest in PUBLIC_EVIDENCE.items():
        require(sha(v.read_file(EVIDENCE_ROOT / name)) == digest, 'late-public-evidence-drift')
    now = datetime.now(timezone.utc)
    return {'kind': 'hexalith.offline-enrollment-revision-verification', 'observedAt': now.isoformat(),
            'revisionSha256': expected, 'priorSupplementSha256': PRIOR_SHA,
            'payloadFiles': len(files), 'preservationFilesUnchanged': len(before) - 1,
            'preservationFilesCompared': len(before), 'knownConcurrentPreservationChanges': [STATUS],
            'priorSupplementFilesUnchanged': len(prior_files) + 1,
            'v4FilesUnchanged': 140, 'historicalFilesUnchanged': 348,
            'runtimeFiles': len(runtime['files']), 'priorSourcesUnchanged': len(prior_sources),
            'newSources': len(sources), 'recordedPriorDrift': [STATUS],
            'originalFullVerifierPass': False, 'authorityAccepted': False,
            'executionApprovalReady': False, 'liveActivationAllowed': False,
            'policyPreparedAt': PREPARED, 'policyCeilingEnds': CEILING,
            'candidateExpired': now >= datetime.fromisoformat(CEILING.replace('Z', '+00:00')),
            'policyTimesRefreshed': False, 'operationalGate': 'closed'}


class Parser(argparse.ArgumentParser):
    def error(self, message):
        raise Refusal('invalid-invocation')


def main():
    try:
        parser = Parser(description=__doc__, allow_abbrev=False)
        parser.add_argument('--sha256', required=True)
        args = parser.parse_args()
        print(json.dumps(verify(args.sha256), sort_keys=True))
        return 0
    except (Refusal, OSError, ValueError, KeyError, TypeError, RecursionError):
        print(json.dumps({'status': 'refused', 'operationalGate': 'closed',
                          'reason': 'offline-revision-integrity-or-custody-refusal'}))
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
