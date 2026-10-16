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
ROOT = Path('/home/administrator/hexalith-recovery-evidence/4-0/20261016t103632z-native-enrollment-execution-revision')
PRIOR = ROOT.parent / '20261016t063900z-native-measurement-supplement'
PRIOR_SHA = 'a7b9bf06139cec3022fbced5fe182b42c773b9a7099c40c84162893a766e2631'
VERIFIER_SHA = 'd7e88423178d5b1f54dc03c5d939029a8176cfe467c95d15c1b0996279c975eb'
PREPARED = '2026-10-15T16:27:56Z'
CEILING = '2026-10-16T16:27:56Z'
STATUS = '/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/sprint-status.yaml'
OLD_STATUS_SHA = '1d66b846fb554deefa67acb44711671376d5d354f74558ffb9b34a87a638283d'
CURRENT_STATUS_SHA = '2d0c7c85dc259481180cd9c65fb327c32706e9ddbfa0c1a01fcd848da2d2d41e'
NEW_SOURCES = {'native_enrollment_execution.py', 'test_native_enrollment_execution.py',
               'verify_enrollment_revision.py', 'test_verify_enrollment_revision.py'}
MANDATORY = {'preservation-before.json', 'prior-verifier-refusal.json',
             'enrollment-execution-binding-v1.json', 'evidence-gaps-v1.json',
             'source-inventory.json', 'NEXT-PREREQUISITE.md', 'README.md',
             'runtime-requirements-v1.json', 'review-summary.json',
             'preservation-after.json'}
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


class Refusal(ValueError):
    """Only fixed refusal codes leave the verifier."""


def require(ok, code):
    if not ok:
        raise Refusal(code)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


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


def validate_baseline(v, before, prior_before, prior_files, prior_sources):
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
        v.check_record(Path(name), record)


def validate_sources(v, sources):
    expected = {str(SOURCE.with_name(name)) for name in NEW_SOURCES}
    require(type(sources) is dict and set(sources) == expected, 'revision-source-members-drift')
    for name, record in sources.items():
        v.check_record(Path(name), record)


def validate_binding(v, binding, sources, prior_sources, prior_payloads):
    require(binding['configuration'] == {'context': 'jpiquot@local',
            'endpoint': 'https://192.168.1.30:6443',
            'configSha256': 'f00b691a5886ef76e6be0d6e3efe4030251147b7bba74bb0c58fa1944e0fe86c',
            'actualConfigRead': False}, 'configuration-scope-drift')
    proposal = binding['proposal']
    original = v.strict_json(prior_payloads['adapter-binding-v2.json'])
    require(proposal['contract'] == original['contract'] and
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
    require(proposal['sourceBindings'] == expected, 'proposal-source-drift')
    require(proposal['runtime']['inventorySha256'] == sha(prior_payloads['runtime-inventory.json']),
            'proposal-runtime-drift')
    require(type(proposal['enrollment']) is dict and
            set(proposal['enrollment']) == {'serverTrust', 'clientIdentity', 'nativeGrant', 'revocation',
                'rights', 'breakGlass', 'cnpgController', 'measurementApproval',
                'credentialDelivery', 'outputCustody'} and
            all(value is None for value in proposal['enrollment'].values()),
            'unresolved-witnesses-must-remain-null')
    require(binding['outputCustody'] == {'root': str(ROOT), 'child': 'measurement-output',
            'childMustRemainAbsent': True, 'directoryMode': 0o700, 'fileMode': 0o600,
            'slots': ['receipt.json', 'failure.json'], 'exclusiveSingleResult': True,
            'maximumBytes': 65536, 'attempts': 1, 'realSlotCreated': False}, 'output-plan-drift')
    require(not os.path.lexists(ROOT / 'measurement-output'), 'real-output-slot-present')


def verify(expected):
    require(type(expected) is str and len(expected) == 64 and all(c in '0123456789abcdef' for c in expected),
            'invalid-anchor')
    v = load_prior_verifier()
    manifest_raw = v.read_file(ROOT / 'packet-v1.json')
    require(sha(manifest_raw) == expected, 'revision-anchor-drift')
    manifest = v.strict_json(manifest_raw)
    require(manifest['kind'] == 'hexalith.native-enrollment-execution-revision' and
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
    validate_baseline(v, before, prior_before, prior_files, prior_sources)
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
    require(gaps['executionApprovalReady'] is False and gaps['authorityAccepted'] is False and
            type(gaps['missingEvidence']) is list and len(gaps['missingEvidence']) >= 8,
            'evidence-gap-drift')
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
    v.recheck_external(before={name: before[name] for name in prior_before}, runtime=runtime, sources=prior_sources)
    validate_baseline(v, before, prior_before, prior_files, prior_sources)
    validate_sources(v, sources)
    for name, digest in PUBLIC_EVIDENCE.items():
        require(sha(v.read_file(EVIDENCE_ROOT / name)) == digest, 'late-public-evidence-drift')
    now = datetime.now(timezone.utc)
    return {'kind': 'hexalith.offline-enrollment-revision-verification', 'observedAt': now.isoformat(),
            'revisionSha256': expected, 'priorSupplementSha256': PRIOR_SHA,
            'payloadFiles': len(files), 'preservationFilesUnchanged': len(before),
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
