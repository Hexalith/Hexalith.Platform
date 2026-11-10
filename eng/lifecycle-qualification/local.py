#!/usr/bin/env python3
"""Local Parties lifecycle observations. Resource-free controls confer no live credit."""
from __future__ import annotations

import argparse
from contextlib import contextmanager
from datetime import datetime, timedelta, timezone
import fcntl
import hashlib
import json
import math
import os
from pathlib import Path
import re
import select
import signal
import stat
import subprocess
import sys
import tempfile
import time
import zipfile

POLICY = 'hexalith.platform.candidate-and-attachment-policy.v1'
CALLER = 'c9eb1fe37373b2b25d3759a4ac8f33ac46754e17'
BUILDS = '5ec1779040606be89306129a7d2b245258c9acd2'
VERSION = '4.47.0'
PACKAGE_SHA256 = '1eea9b282d1d247e1f6e9e2213c5969f7895df44105e6900c873b586c5c4f905'
MANIFEST = 'module/hexalith.module-manifest.v2.json'
PROFILE = 'lifecycle-package'
RUN = re.compile(r'[0-9a-f]{32}')
HASH = re.compile(r'[0-9a-f]{64}')
MAX_JSON = 8_388_608
TOOL_STOP_SECONDS = 95
SCENARIOS = ('success-failure', 'startup-timeout-partial', 'cancellation-collision',
             'owner-finish-hold', 'owner-cancel-hold', 'attached-failure-expiry',
             'failure-then-cancellation', 'locks-retry-peer')
MISSING = ('attached-suite-execution-unavailable', 'attached-work-access-producer-unavailable',
           'exclusive-resource-selection-unavailable', 'owner-cancellation-window-missed',
           'startup-duration-producer-unavailable', 'termination-producer-unavailable',
           'original-source-mapping-proof-unavailable')
MAPPING_IMPORTS = {
    'CustomBeforeDirectoryBuildProps': 'SourceMapping.targets',
    'CustomAfterMicrosoftCommonProps': 'SourceMapping.props',
    'CustomBeforeDirectoryBuildTargets': 'SourceMapping.before-targets.props',
    'CustomAfterDirectoryBuildTargets': 'SourceMapping.late.props',
    'AfterMicrosoftNETSdkTargets': 'SourceMapping.final.targets',
}
CANCELLED = []


class Refusal(ValueError):
    def __init__(self, code='HXLQ202'):
        assert code in {f'HXLQ{value}' for value in range(201, 210)}
        self.code = code
        super().__init__(code)


def require(condition, code='HXLQ202'):
    if not condition:
        raise Refusal(code)


def failure(error):
    kinds = ('Refusal', 'OSError', 'FileNotFoundError', 'PermissionError', 'TimeoutError',
             'TimeoutExpired', 'ValueError', 'KeyError', 'TypeError', 'JSONDecodeError')
    return {'failureCode': error.code if isinstance(error, Refusal) else 'HXLQ205',
            'failureType': type(error).__name__ if type(error).__name__ in kinds else 'Exception',
            'redacted': True}


def now():
    return datetime.now(timezone.utc).isoformat()


def utc(value):
    require(isinstance(value, str) and len(value) <= 40 and re.fullmatch(
        r'[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:[.][0-9]{1,7})?(?:Z|[+]00:00)', value))
    parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(parsed.utcoffset() == timedelta(0))
    return parsed


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(131072), b''):
            digest.update(block)
    return digest.hexdigest()


def unlinked(path):
    path = Path(path)
    require(path.is_absolute() and not any(item.is_symlink() for item in (path, *path.parents)))
    return path


def private(path):
    path = unlinked(path)
    path.mkdir(parents=True, exist_ok=True, mode=0o700)
    info = path.stat()
    require(stat.S_ISDIR(info.st_mode) and info.st_uid == os.geteuid() and info.st_mode & 0o077 == 0)
    return path


def read(path, *, with_hash=False):
    path = unlinked(path)
    descriptor = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    with os.fdopen(descriptor, 'rb') as stream:
        info = os.fstat(stream.fileno())
        require(stat.S_ISREG(info.st_mode) and info.st_nlink == 1 and info.st_size <= MAX_JSON)
        body = stream.read(MAX_JSON + 1)
    require(len(body) <= MAX_JSON)

    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result)
            result[key] = value
        return result

    def constant(_):
        raise Refusal()

    value = json.loads(body.decode('utf-8'), object_pairs_hook=pairs, parse_constant=constant)
    return (value, hashlib.sha256(body).hexdigest()) if with_hash else value


def prepared_mapping(environment, checkout):
    """Bind the actual producer's original mapping, never only its declared hash."""
    require(isinstance(environment, dict), 'HXLQ209')
    expected = environment.get('HEXALITH_SOURCE_MAPPING_HASH')
    require(isinstance(expected, str) and HASH.fullmatch(expected)
            and environment.get('Configuration') == 'Release', 'HXLQ209')
    require(isinstance(environment.get('CustomAfterMicrosoftCommonProps'), str), 'HXLQ209')
    directory = unlinked(Path(environment['CustomAfterMicrosoftCommonProps']).parent)
    import_hashes = {}
    for key, filename in MAPPING_IMPORTS.items():
        require(isinstance(environment.get(key), str), 'HXLQ209')
        path = unlinked(Path(environment[key]))
        require(path == directory / filename and path.is_file() and path.stat().st_nlink == 1
                and path.stat().st_size <= MAX_JSON, 'HXLQ209')
        import_hashes[filename] = sha(path)
    mapping, content = read(directory / 'mapping.json', with_hash=True)
    require(isinstance(mapping, dict) and mapping.get('Root') == str(checkout)
            and mapping.get('Mode') == 'package' and mapping.get('ContentHash') == expected
            and mapping.get('HasGit') is True and isinstance(mapping.get('Entries'), list)
            and 0 < len(mapping['Entries']) <= 256, 'HXLQ209')
    require(len(str(checkout)) <= 2048 and not any(ord(character) < 32 or 127 <= ord(character) <= 159
                                               for character in str(checkout)), 'HXLQ209')
    return mapping, {'Root': str(checkout), 'Mode': 'package', 'ContentHash': expected,
                     'documentSha256': content, 'importsSha256': import_hashes}


def execution_checkpoint(value, owner):
    """Project only bounded fixture fields after binding them to the issued owner."""
    require(isinstance(value, dict) and value.get('runId') == owner['runId']
            and value.get('realPersistenceAssertionsPassed') is True, 'HXLQ203')
    observed = value.get('observedAtUtc')
    require(utc(owner['createdAt']) <= utc(observed) <= datetime.now(timezone.utc), 'HXLQ204')
    digest = value.get('descriptorSha256')
    require(isinstance(digest, str) and HASH.fullmatch(digest))
    require(type(value.get('processId')) is int and value['processId'] > 1, 'HXLQ203')
    return {'runId': owner['runId'], 'observedAtUtc': observed,
            'descriptorSha256': digest, 'processId': value['processId']}


def write(path, value):
    path = Path(path)
    private(path.parent)
    require(not path.is_symlink() and (not path.exists() or path.stat().st_nlink == 1))
    descriptor, temporary = tempfile.mkstemp(dir=path.parent, prefix=path.name + '.')
    try:
        with os.fdopen(descriptor, 'w') as stream:
            json.dump(value, stream, indent=2, sort_keys=True, allow_nan=False)
            stream.write('\n')
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
        descriptor = os.open(path.parent, os.O_RDONLY | os.O_DIRECTORY)
        try:
            os.fsync(descriptor)
        finally:
            os.close(descriptor)
    finally:
        Path(temporary).unlink(missing_ok=True)


def process(pid, run_id=None):
    require(type(pid) is int and pid > 1, 'HXLQ203')
    root = Path('/proc') / str(pid)
    fields = (root / 'stat').read_text().rpartition(')')[2].split()
    require(fields[0] not in ('Z', 'X') and root.stat().st_uid == os.geteuid(), 'HXLQ203')
    if run_id is not None:
        require(RUN.fullmatch(run_id) and (b'HEXALITH_G4_RUN_ID=' + run_id.encode())
                in (root / 'environ').read_bytes().split(b'\0'), 'HXLQ203')
    return {'pid': pid, 'birth': fields[19], 'session': int(fields[3])}


def signal_exact(identity, number, run_id=None):
    """Bind the kernel handle before the final birth/session/tag check; never signal a process group."""
    require(number in (signal.SIGINT, signal.SIGKILL, signal.SIGSTOP, signal.SIGCONT), 'HXLQ203')
    descriptor = os.pidfd_open(identity['pid'])
    try:
        require(process(identity['pid'], run_id) == identity, 'HXLQ203')
        signal.pidfd_send_signal(descriptor, number)
    finally:
        os.close(descriptor)


def deadline(state, previous=None):
    fields = ('createdAt', 'startupDeadlineUtc', 'effectiveDeadlineSeconds', 'effectiveDeadlineIsOverride')
    result = {key: state.get(key) for key in fields}
    require(type(result['effectiveDeadlineSeconds']) in (int, float)
            and math.isfinite(result['effectiveDeadlineSeconds']) and result['effectiveDeadlineSeconds'] > 0, 'HXLQ204')
    require(utc(result['startupDeadlineUtc']) - utc(result['createdAt'])
            == timedelta(seconds=result['effectiveDeadlineSeconds']), 'HXLQ204')
    require(result['effectiveDeadlineIsOverride'] is False and result['effectiveDeadlineSeconds'] == 600,
            'HXLQ204')  # This fixed fixture declares no startup override.
    require(previous is None or result == previous, 'HXLQ204')
    return result


def first_outcome(value, owner):
    require(value.get('runId') == value.get('ownerId') == owner['runId'], 'HXLQ203')
    status = value.get('terminalStatus')
    require(status in ('completed', 'failed', 'cancelled'), 'HXLQ202')
    utc(value.get('terminalAtUtc'))
    result = {key: value.get(key) for key in ('runId', 'ownerId', 'terminalStatus', 'terminalAtUtc', 'sourceMappingHash')}
    raw_outcome = value.get('terminalOutcome')
    require(isinstance(raw_outcome, dict))
    outcome = {key: raw_outcome.get(key) for key in ('exitCode', 'phase', 'category', 'ruleId')}
    require(outcome['exitCode'] in ('Success', 'UsageOrManifest', 'PrerequisiteUnavailable', 'TopologyOrLifecycle',
                                   'ProductOrTest', 'PersistedState', 'EvidenceSchemaOrPolicy', 'Cancelled'))
    require(outcome['phase'] in ('None', 'Usage', 'Manifest', 'Prerequisite', 'Topology', 'Test', 'PersistedState', 'Evidence', 'Cleanup'))
    require(outcome['category'] in ('None', 'Usage', 'Manifest', 'PrerequisiteUnavailable', 'TopologyOrLifecycle',
                                   'ProductOrTest', 'PersistedState', 'EvidenceSchema', 'EvidencePolicy', 'Cancelled'))
    require(outcome['ruleId'] is None or isinstance(outcome['ruleId'], str)
            and re.fullmatch(r'H[A-Z]{1,4}[0-9]{3}', outcome['ruleId']))
    if status == 'completed':
        require(outcome == {'exitCode': 'Success', 'phase': 'None', 'category': 'None', 'ruleId': None})
    else:
        require(outcome['phase'] != 'None' and outcome['category'] != 'None' and outcome['ruleId'] is not None)
        require(outcome['exitCode'] == 'Cancelled' and outcome['category'] == 'Cancelled' if status == 'cancelled'
                else outcome['exitCode'] not in ('Success', 'Cancelled'))
    result['terminalOutcome'] = outcome
    cause = value.get('terminalCause')
    allowed = {'completed': ('tests-completed',), 'cancelled': ('run-cancelled',),
               'failed': ('test-failure', 'startup-failure', 'startup-timeout', 'execution-timeout')}
    require(cause is None or cause in allowed[status])
    result['terminalCause'] = cause
    # The published TestResultWriter.Cause fallback, retained separately from the actual internal field.
    result['expectedContractCause'] = cause or ('tests-completed' if status == 'completed' else
        'run-cancelled' if status == 'cancelled' else
        'startup-failure' if value.get('executionTiming') is None else 'test-failure')
    require(result['sourceMappingHash'] is None or HASH.fullmatch(result['sourceMappingHash'].lower()))
    return result


def public_terminal(document, first):
    terminal = document.get('terminal')
    require(isinstance(terminal, dict))
    expected = {'completed': 'passed', 'failed': 'failed', 'cancelled': 'cancelled'}[first['terminalStatus']]
    require(terminal.get('outcome') == expected and terminal.get('cause') == first['expectedContractCause']
            and utc(terminal.get('recordedAt')) == utc(first['terminalAtUtc']), 'HXLQ205')


def hold(value, owner, original=None):
    keys = ('originalAttachmentRequestId', 'environmentId', 'ownerRunId', 'attachedRunId',
            'requestedHoldSeconds', 'acceptedAtUtc', 'effectiveHoldSeconds', 'expiresAtUtc')
    result = {key: value.get(key) for key in keys}
    require(all(isinstance(result[key], str) and RUN.fullmatch(result[key])
                for key in keys[:4]) and result['ownerRunId'] == owner['runId']
            and result['environmentId'] == owner['environmentId'], 'HXLQ203')
    require(result['attachedRunId'] != result['ownerRunId']
            and result['originalAttachmentRequestId'] != result['attachedRunId'], 'HXLQ203')
    seconds = result['effectiveHoldSeconds']
    require(type(seconds) is int and 1 <= seconds <= 14400 and
            (type(result['requestedHoldSeconds']) is int and result['requestedHoldSeconds'] == seconds
             if result['requestedHoldSeconds'] is not None else seconds == 1800), 'HXLQ204')
    require(utc(result['expiresAtUtc']) == utc(result['acceptedAtUtc']) + timedelta(seconds=seconds), 'HXLQ204')
    require(original is None or all(result[key] == original[key] for key in keys), 'HXLQ204')
    require(value.get('outcome') in (None, 'passed', 'failed', 'cancelled', 'withdrawn'))
    reason = value.get('endReason')
    require(reason in (None, 'terminal-outcome', 'expired', 'owner-cancelled', 'developer-stop', 'cleanup'))
    if value.get('endedAtUtc') is not None:
        ended = utc(value['endedAtUtc'])
        require(ended >= utc(result['acceptedAtUtc']) and
                (ended >= utc(result['expiresAtUtc']) if reason == 'expired' else ended < utc(result['expiresAtUtc'])), 'HXLQ204')
    result.update({key: value.get(key) for key in ('outcome', 'endReason', 'endedAtUtc')})
    return result


def verify_package(package, assembly, expected):
    require(HASH.fullmatch(expected) and sha(package) == expected, 'HXLQ201')
    assembly = unlinked(assembly)
    require(assembly.name == 'Hexalith.Builds.Module.Cli.dll', 'HXLQ201')
    checked = {}
    with zipfile.ZipFile(package) as archive:
        prefix = 'tools/net10.0/any/'
        for info in archive.infolist():
            require(not info.filename.startswith('/') and '..' not in Path(info.filename).parts, 'HXLQ201')
            if info.is_dir() or not info.filename.startswith(prefix):
                continue
            require(info.file_size <= 536_870_912 and stat.S_IFMT(info.external_attr >> 16) != stat.S_IFLNK, 'HXLQ201')
            relative = info.filename[len(prefix):]
            installed = unlinked(assembly.parent / relative)
            digest = hashlib.sha256()
            with archive.open(info) as stream:
                for block in iter(lambda: stream.read(131072), b''):
                    digest.update(block)
            require(installed.is_file() and installed.stat().st_size == info.file_size
                    and sha(installed) == digest.hexdigest(), 'HXLQ201')
            checked[relative] = digest.hexdigest()
    require(assembly.name in checked, 'HXLQ201')
    return {'version': VERSION, 'packageSha256': expected, 'files': checked}


def tool_arguments(assembly):
    return ['dotnet', str(assembly), 'test', '--manifest=' + MANIFEST,
            '--mode=package', '--profile=' + PROFILE, '--output=json']


def gated_launch(intent_path):
    intent = read(intent_path)
    require(intent['command'] == tool_arguments(Path(intent['assembly'])) and Path(intent['checkout']).is_absolute())
    identity = process(os.getpid())
    require(identity['session'] == identity['pid'], 'HXLQ203')
    record = {'identity': identity, 'commandSha256': hashlib.sha256(json.dumps(intent['command']).encode()).hexdigest(),
              'released': False}
    write(Path(intent_path).parent / 'launch.json', record)
    readable, _, _ = select.select([sys.stdin.buffer], [], [], 30)
    if not readable or sys.stdin.buffer.read(1) != b'1':
        write(Path(intent_path).parent / 'launch.json', {**record, 'closed': True})
        return 1
    write(Path(intent_path).parent / 'launch.json', {**record, 'released': True})
    os.chdir(intent['checkout'])
    with open(os.devnull, 'rb') as empty:
        os.dup2(empty.fileno(), 0)
    os.execvpe(intent['command'][0], intent['command'], os.environ)


class Campaign:
    def __init__(self, checkout, work, evidence, assembly, package, package_hash, selected, environment=None):
        self.checkout, self.work, self.evidence = map(lambda p: unlinked(Path(p).absolute()), (checkout, work, evidence))
        require(self.work != self.evidence and self.work not in self.evidence.parents and self.evidence not in self.work.parents)
        require(not self.work.exists() or not any(self.work.iterdir()), 'HXLQ201')
        private(self.work)
        private(self.evidence)
        self.assembly = Path(assembly).absolute()
        self.children, self.owners = {}, []
        self.data = private(self.work / 'data')
        self.states = self.data / 'hexalith-builds/g4-runs'
        self.controls = Path.home() / '.local/share/hexalith-builds/ci-qualification'
        require(sys.platform == 'linux' and hasattr(os, 'pidfd_open') and hasattr(signal, 'pidfd_send_signal'), 'HXLQ201')
        require(not any(os.environ.get(key, '').lower() == 'true' for key in ('CI', 'GITHUB_ACTIONS')), 'HXLQ201')
        names = ('PATH', 'HOME', 'USER', 'LOGNAME', 'LANG', 'TMPDIR', 'DOTNET_ROOT', 'DOTNET_CLI_HOME',
                 'NUGET_PACKAGES', 'DOCKER_HOST', 'DOCKER_CONFIG', 'DOCKER_CONTEXT', 'HEXALITH_DAPR_HOME',
                 'DOTNET_NOLOGO', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_GENERATE_ASPNET_CERTIFICATE',
                 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE', 'MSBUILDDISABLENODEREUSE')
        self.env = {key: os.environ[key] for key in names if key in os.environ}
        self.prepared_environment = None
        if environment is not None:
            prepared = read(Path(environment).absolute())
            require(isinstance(prepared, dict) and all(isinstance(key, str) and isinstance(value, str)
                    for key, value in prepared.items()), 'HXLQ201')
            forbidden = ('CI', 'GITHUB_ACTIONS', 'HEXALITH_G4_APPHOST', 'DOTNET_STARTUP_HOOKS',
                         'DOTNET_ADDITIONAL_DEPS', 'DOTNET_SHARED_STORE')
            imports = ('CustomBeforeDirectoryBuildProps', 'CustomBeforeDirectoryBuildTargets',
                       'CustomAfterMicrosoftCommonProps', 'CustomAfterDirectoryBuildTargets',
                       'AfterMicrosoftNETSdkTargets', 'HEXALITH_SOURCE_MAPPING_HASH',
                       'Configuration', 'XDG_DATA_HOME', 'LC_ALL')
            require(set(prepared) <= set(names) | set(imports), 'HXLQ201')
            require(not any(key in prepared for key in forbidden) and not any(key.startswith('GITHUB_') for key in prepared), 'HXLQ201')
            require(prepared.get('HOME', os.environ.get('HOME')) == os.environ.get('HOME'), 'HXLQ201')
            self.env.update(prepared)  # Private producer-issued package imports; never copied into public records.
            self.prepared_environment = prepared
        self.env['XDG_DATA_HOME'] = str(self.data)
        self.journal = {'schema': 'hexalith.local-lifecycle-qualification.v1', 'policyId': POLICY,
                        'executionContext': {'kind': 'local'}, 'candidateAcceptance': False, 'toolAccepted': False,
                        'requiredCases': list(SCENARIOS), 'selectedCases': list(selected), 'completedCases': [],
                        'scenarios': {name: {'status': 'unexecuted'} for name in SCENARIOS}, 'owners': [], 'observations': []}
        self.save()
        self.preflight(package, package_hash)

    def save(self):
        write(self.work / 'journal.json', self.journal)
        write(self.evidence / 'observations.json', self.journal)

    def observe(self, kind, **fields):
        require(isinstance(kind, str) and re.fullmatch(r'[a-z][a-z0-9-]{0,63}', kind))
        observed = now()
        self.journal['observations'].append({'kind': kind, 'observedAtUtc': observed, **fields})
        require(len(self.journal['observations']) <= 2048)
        self.save()
        # Only controller event names, clock and ordinal reach the monitoring pipe.
        print(json.dumps({'event': kind, 'observation': len(self.journal['observations']),
                          'observedAtUtc': observed}), flush=True)

    def preflight(self, package, package_hash):
        require(package_hash == PACKAGE_SHA256, 'HXLQ201')
        self.prepared_mapping, mapping_binding = prepared_mapping(self.prepared_environment, self.checkout)
        def git(*args):
            return subprocess.check_output(['git', '-c', 'core.commitGraph=false', *args], cwd=self.checkout, timeout=20, env=self.env).decode().strip()
        require(git('rev-parse', 'HEAD') == CALLER and not git('status', '--porcelain', '--untracked-files=no'), 'HXLQ201')
        require(git('rev-parse', 'HEAD:references/Hexalith.Builds') == BUILDS, 'HXLQ201')
        manifest = read(self.checkout / '.config/dotnet-tools.json')['tools']['hexalith.builds.module.cli']
        require(manifest['version'] == VERSION and manifest['rollForward'] is False, 'HXLQ201')
        proof = verify_package(package, self.assembly, package_hash)
        result = subprocess.run(['dotnet', str(self.assembly), '--version'], cwd=self.checkout, env=self.env,
                                capture_output=True, text=True, timeout=30)
        require(result.returncode == 0 and re.fullmatch(r'4[.]47[.]0(?:\+[0-9a-z.-]+)?', result.stdout.strip(), re.I), 'HXLQ201')
        inputs = {name: sha(self.checkout / name) for name in (MANIFEST, '.config/dotnet-tools.json',
                  'tests/Hexalith.Parties.Platform.LifecycleTests/LifecycleQualificationTests.cs',
                  'tests/Hexalith.Parties.Platform.LifecycleTests/Hexalith.Parties.Platform.LifecycleTests.csproj')}
        self.manifest_hash = inputs[MANIFEST]
        self.journal['inputs'] = {'caller': CALLER, 'builds': BUILDS, 'files': inputs, 'tool': proof,
                                  'sourceMapping': mapping_binding}
        self.save()

    def poll(self, predicate, seconds, allow_exit=False, item=None):
        end = time.monotonic() + max(0, seconds)
        while time.monotonic() < end:
            require(not CANCELLED, 'HXLQ208')
            try:
                value = predicate()
            except (FileNotFoundError, json.JSONDecodeError):
                value = None  # A producer's partial publication never restarts this deadline.
            if value:
                return value
            if item and not allow_exit:
                require(self.children[item['label']].poll() is None, 'HXLQ207')
            time.sleep(0.1)
        raise Refusal('HXLQ207')

    def begin(self, label):
        require(label not in self.children)
        directory = private(self.work / label)
        item = {'label': label, 'intent': str(directory / 'intent.json')}
        self.owners.append(item)
        self.journal['owners'].append({'label': label, 'issued': False, 'cleanupVerified': False})
        self.save()  # Durable intent precedes process creation and any allocation.
        write(directory / 'intent.json', {'command': tool_arguments(self.assembly), 'assembly': str(self.assembly), 'checkout': str(self.checkout)})
        stream = (directory / 'stdout.json').open('x')
        os.chmod(directory / 'stdout.json', 0o600)
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), '--_launch', item['intent']],
                                 cwd=self.checkout, env=self.env, stdin=subprocess.PIPE, stdout=stream,
                                 stderr=subprocess.DEVNULL, start_new_session=True, bufsize=0)
        stream.close()
        self.children[label] = child
        try:
            record = self.poll(lambda: read(directory / 'launch.json') if (directory / 'launch.json').exists() else None, 5, item=item)
            require(record['identity'] == process(child.pid) and record['identity']['session'] == child.pid
                    and record['released'] is False, 'HXLQ203')
            item['identity'] = record['identity']
            self.public(item)['launchIdentity'] = item['identity']
            self.save()
            require(not CANCELLED, 'HXLQ208')
            require(child.stdin.write(b'1') == 1, 'HXLQ208')
        finally:
            child.stdin.close()
        self.poll(lambda: self.discover(item), 600, item=item)
        self.control(item, 'wait')
        return item

    def discover(self, item):
        if item.get('owner'):
            return True
        known = {entry['owner']['runId'] for entry in self.owners if entry.get('owner')}
        candidates = [path for path in self.states.glob('*.json') if RUN.fullmatch(path.stem) and path.stem not in known]
        require(len(candidates) <= 1, 'HXLQ203')
        if not candidates:
            return False
        state = read(candidates[0])
        require(state['executionContext'] == {'kind': 'local'} and state['manifestHash'].lower() == self.manifest_hash, 'HXLQ203')
        owner = {key: state[key] for key in ('runId', 'attemptId', 'environmentId', 'createdAt', 'startupDeadlineUtc')}
        require(all(isinstance(owner[key], str) and RUN.fullmatch(owner[key]) for key in ('runId', 'attemptId', 'environmentId')), 'HXLQ203')
        require(state['workspace'] == str(self.data / 'hexalith-builds/g4-workspaces' / owner['runId']), 'HXLQ203')
        item.update(owner=owner, deadline=deadline(state), workspace=state['workspace'])
        self.public(item).update(issued=True, **owner, deadline=item['deadline'])
        self.observe('actual-issued-owner', label=item['label'], **owner)
        return True

    def public(self, item):
        return next(value for value in self.journal['owners'] if value['label'] == item['label'])

    def state(self, item):
        owner = item['owner']
        active = self.states / (owner['runId'] + '.json')
        path = active if active.exists() else self.diagnostics(item) / 'owner.json'
        value = read(path)
        require(all(value.get(key) == owner[key] for key in ('runId', 'attemptId', 'environmentId', 'createdAt', 'startupDeadlineUtc'))
                and value.get('executionContext') == {'kind': 'local'}, 'HXLQ203')
        deadline(value, item['deadline'])
        duration = value.get('startupDurationSeconds')
        require(duration is None or type(duration) in (int, float) and math.isfinite(duration) and duration >= 0)
        self.public(item)['startupDurationSeconds'] = duration
        return value

    def diagnostics(self, item):
        return self.states / 'diagnostics' / item['owner']['runId']

    def control(self, item, action):
        require(action in ('wait', 'pass', 'fail'))
        directory = private(self.controls / item['owner']['runId'])
        write(directory / 'control.json', {'runId': item['owner']['runId'], 'action': action})

    def ready(self, item, startup=False):
        remaining = (utc(item['owner']['startupDeadlineUtc']) - datetime.now(timezone.utc)).total_seconds()
        def check():
            state = self.state(item)
            if startup:
                if state['status'] != 'Starting' or state.get('appHostProcessId') is None or not self.containers(item):
                    return False
                item['paused'] = process(state['appHostProcessId'], item['owner']['runId'])
                signal_exact(item['paused'], signal.SIGSTOP, item['owner']['runId'])
                self.observe('owned-startup-paused', ownerRunId=item['owner']['runId'], identity=item['paused'])
                return True
            marker = self.controls / item['owner']['runId'] / 'execution-ready.json'
            if not marker.exists():
                return False
            value = execution_checkpoint(read(marker), item['owner'])
            require(state['status'] == 'Ready')
            item['native'] = process(value['processId'], item['owner']['runId'])
            require(HASH.fullmatch(value['descriptorSha256']))
            self.observe('real-persistence-checkpoint', ownerRunId=item['owner']['runId'], identity=item['native'],
                         observedAtUtc=value['observedAtUtc'], descriptorSha256=value['descriptorSha256'])
            return True
        self.poll(check, remaining + (30 if not startup else 0), item=item)
        self.observe('actual-images', ownerRunId=item['owner']['runId'], images=self.images(item))
        return item

    def start(self, label, startup=False):
        return self.ready(self.begin(label), startup)

    def containers(self, item):
        result = subprocess.run(['docker', 'ps', '-aq', '--no-trunc', '--filter', 'label=hexalith.g4.run=' + item['owner']['runId']],
                                capture_output=True, text=True, timeout=20, env=self.env)
        require(result.returncode == 0, 'HXLQ206')
        identities = result.stdout.split()
        require(all(re.fullmatch(r'[0-9a-f]{12,64}', value) for value in identities), 'HXLQ206')
        return sorted(identities)

    def images(self, item):
        images = []
        for identity in self.containers(item):
            result = subprocess.run(['docker', 'inspect', '--format', '{{json .Id}}\t{{json .Config.Image}}\t{{json .Image}}\t{{json (index .Config.Labels "hexalith.g4.run")}}', identity],
                                    capture_output=True, text=True, timeout=20, env=self.env)
            require(result.returncode == 0 and len(result.stdout) <= 4096)
            values = [json.loads(value) for value in result.stdout.strip().split('\t')]
            require(len(values) == 4 and values[3] == item['owner']['runId'], 'HXLQ203')
            require(all(isinstance(value, str) and len(value) < 512 and re.fullmatch(r'[a-zA-Z0-9./:_@+-]+', value) for value in values))
            images.append(dict(zip(('containerId', 'imageReference', 'imageContentId'), values[:3])))
        require(images, 'HXLQ206')
        return images

    def invoke(self, arguments):
        result = subprocess.run(['dotnet', str(self.assembly), *arguments, '--output=json'], cwd=self.checkout,
                                env=self.env, capture_output=True, text=True, timeout=150)
        require(len(result.stdout.encode()) <= MAX_JSON)
        return result.returncode, json.loads(result.stdout)

    def outcome(self, item, required=True):
        path = self.diagnostics(item) / 'selected-test.json'
        if not path.exists():
            require(not required, 'HXLQ207')
            return None
        raw = read(path)
        first = first_outcome(raw, item['owner'])
        require(item.get('first') in (None, first), 'HXLQ205')
        mapping_proven = self.terminal_mapping(item, first, required)
        item['first'] = first
        self.public(item).update(firstOutcome=first, failureRetained=raw.get('failureRetained') is True,
                                 originalMappingProof=mapping_proven)
        contract = self.diagnostics(item) / 'test-result.json'
        if contract.exists():
            document = read(contract)
            bindings = document.get('bindings', {})
            run = bindings.get('run', {})
            require(document.get('policyId') == POLICY and
                    all(run.get(key) == item['owner'][key] for key in ('runId', 'attemptId', 'environmentId'))
                    and run.get('ownerRunId') == item['owner']['runId'] and run.get('role') == 'owner'
                    and bindings.get('environment', {}).get('executionContext') == {'kind': 'local'}, 'HXLQ203')
            public_terminal(document, first)
            self.public(item)['publicTerminalProof'] = True
            startup = bindings.get('deadlines', {}).get('startup', {})
            duration = startup.get('actualDurationSeconds')
            if startup.get('state') == 'populated' and duration is not None:
                require(type(duration) in (int, float) and math.isfinite(duration) and duration >= 0, 'HXLQ204')
                require(utc(startup['originAt']) == utc(item['owner']['createdAt'])
                        and utc(startup['expiresAt']) == utc(item['owner']['startupDeadlineUtc'])
                        and startup['effectiveSeconds'] == item['deadline']['effectiveDeadlineSeconds'], 'HXLQ204')
                self.public(item).update(startupDurationSeconds=duration, startupDurationProof=True)
        self.save()
        return first

    def terminal_mapping(self, item, first, required):
        original = getattr(self, 'prepared_mapping', None)
        require(isinstance(original, dict), 'HXLQ209')
        observed = first['sourceMappingHash']
        require(observed is None or observed.lower() == original['ContentHash'], 'HXLQ209')
        path = self.diagnostics(item) / 'selected-source-mapping.json'
        if not path.exists():
            require(not required, 'HXLQ209')
            return False
        archived, digest = read(path, with_hash=True)
        require(archived == original and archived.get('Root') == str(self.checkout)
                and archived.get('Mode') == 'package' and archived.get('ContentHash') == original['ContentHash'], 'HXLQ209')
        self.public(item)['sourceMappingDocumentSha256'] = digest
        return True

    def finish(self, item, action):
        self.control(item, action)
        first = self.poll(lambda: self.outcome(item, False), 120, allow_exit=True)
        require(first['terminalStatus'] == ('completed' if action == 'pass' else 'failed'))
        require(first['terminalOutcome']['exitCode'] == ('Success' if action == 'pass' else 'ProductOrTest'))
        return first

    def signal_tool(self, item, number):
        identity = item.get('identity')
        require(identity is not None, 'HXLQ203')
        prior = self.outcome(item, False) if item.get('owner') else None
        requested = now()
        signal_exact(identity, number)
        self.observe('actual-tool-signal', label=item['label'], identity=identity, signal=int(number),
                     requestedAtUtc=requested, firstOutcomeBeforeSignal=prior)

    def resume(self, item):
        if item.get('paused'):
            try:
                signal_exact(item['paused'], signal.SIGCONT, item['owner']['runId'])
            except (ProcessLookupError, FileNotFoundError):
                pass
            item.pop('paused')

    def attachment(self, item, seconds=30, previous=None, expected_code=None):
        command = ['attach', '--owner-run-id=' + item['owner']['runId'], '--mode=package',
                   '--composition-identity=' + self.manifest_hash, '--hold-seconds=' + str(seconds)]
        if previous:
            command.append('--attachment-request-id=' + previous['originalAttachmentRequestId'])
        code, result = self.invoke(command)
        if expected_code:
            require(expected_code in ('HXR035', 'HXR036') and type(code) is int and -128 <= code <= 255)
            diagnostics = result.get('diagnostics', [])
            require(isinstance(diagnostics, list) and len(diagnostics) <= 256)
            rules = []
            reasons = []
            for diagnostic in diagnostics:
                require(isinstance(diagnostic, dict) and isinstance(diagnostic.get('ruleId'), str)
                        and re.fullmatch(r'H[A-Z]{1,4}[0-9]{3}', diagnostic['ruleId']))
                rules.append(diagnostic['ruleId'])
                if diagnostic.get('field') in ('unknown-attachment-request', 'attachment-ended', 'attachment-expired',
                                              'owner-cleaned', 'owner-cleaning', 'owner-passed', 'owner-cancelled',
                                              'owner-failed', 'owner-finished', 'owner-withdrawn', 'owner-starting'):
                    reasons.append(diagnostic['field'])
            receipt = {'ownerRunId': item['owner']['runId'], 'exitCode': code,
                       'attachmentPresent': result.get('attachment') is not None,
                       'expectedRuleId': expected_code, 'ruleIds': rules, 'knownReasons': reasons}
            if previous:
                receipt['originalAttachmentRequestId'] = hold(previous, item['owner'])['originalAttachmentRequestId']
            self.observe('negative-attachment-receipt', **receipt)
            require(code != 0 and not receipt['attachmentPresent'] and expected_code in rules)
            return None
        require(code == 0 and result.get('attachment'))
        admitted = hold(result['attachment'], item['owner'], previous)
        self.observe('actual-admission', ownerRunId=item['owner']['runId'], attachment=admitted)
        return admitted

    def observed_hold(self, item, original):
        document = read(self.diagnostics(item) / 'attachments.json')
        require(document.get('ownerRunId') == item['owner']['runId'], 'HXLQ203')
        value = next(entry for entry in document['attachments'] if entry['attachedRunId'] == original['attachedRunId'])
        return hold(value, item['owner'], original)

    def raw_hold_snapshot(self, item, original, stage):
        require(stage in ('before-expiry', 'before-public-list', 'after-public-list', 'after-cleanup'))
        document, digest = read(self.diagnostics(item) / 'attachments.json', with_hash=True)
        require(document.get('schema') == 'hexalith.g4-run-attachments.v1'
                and document.get('ownerRunId') == item['owner']['runId'], 'HXLQ203')
        entries = document.get('attachments')
        require(isinstance(entries, list) and len(entries) <= 256)
        matches = [entry for entry in entries if isinstance(entry, dict)
                   and entry.get('attachedRunId') == original['attachedRunId']]
        require(len(matches) == 1, 'HXLQ203')
        snapshot = {'documentSha256': digest, 'attachment': hold(matches[0], item['owner'], original)}
        self.observe('raw-hold-snapshot', ownerRunId=item['owner']['runId'], stage=stage, **snapshot)
        return snapshot

    def public_expiry(self, item, original, failed):
        before = self.raw_hold_snapshot(item, original, 'before-public-list')
        code, result = self.invoke(['list'])
        after = self.raw_hold_snapshot(item, original, 'after-public-list')
        require(before == after, 'HXLQ205')
        entries = result.get('environments')
        require(code == 0 and isinstance(entries, list) and len(entries) <= 2048)
        matches = [entry for entry in entries if isinstance(entry, dict)
                   and entry.get('ownerRunId') == item['owner']['runId']]
        require(len(matches) == 1, 'HXLQ203')
        entry = matches[0]
        require(all(entry.get(key) == item['owner'][key] for key in ('environmentId', 'attemptId'))
                and utc(entry.get('createdAt')) == utc(item['owner']['createdAt']), 'HXLQ203')
        attachments = entry.get('attachments')
        require(isinstance(attachments, list) and len(attachments) <= 256)
        matches = [value for value in attachments if isinstance(value, dict)
                   and value.get('attachedRunId') == original['attachedRunId']]
        require(len(matches) == 1, 'HXLQ203')
        expired = hold(matches[0], item['owner'], original)
        require(expired['outcome'] == 'withdrawn' and expired['endReason'] == 'expired'
                and utc(expired['endedAtUtc']) == utc(original['expiresAtUtc']), 'HXLQ204')
        failures, live = entry.get('failureRunIds'), entry.get('attachedRunIds')
        require(all(isinstance(values, list) and len(values) <= 256
                    and all(isinstance(value, str) and RUN.fullmatch(value) for value in values)
                    for values in (failures, live)), 'HXLQ203')
        require(entry.get('retention') == 'failure-retained' and failed['attachedRunId'] in failures
                and expired['attachedRunId'] not in live)
        self.observe('public-hold-expiry', ownerRunId=item['owner']['runId'], attachment=expired,
                     rawStoreUnchanged=True, retention='failure-retained', failedAttachedRunId=failed['attachedRunId'])
        return expired

    def end_hold(self, item, original, outcome):
        require(outcome in ('passed', 'failed', 'cancelled'))
        code, _ = self.invoke(['attach', '--end', '--owner-run-id=' + item['owner']['runId'],
                               '--attached-run-id=' + original['attachedRunId'], '--outcome=' + outcome])
        require(code == 0)
        ended = self.observed_hold(item, original)
        require(ended['outcome'] == outcome)
        self.observe('reported-attachment-terminal', attachment=ended, actualAttachedSuiteExecuted=False)

    @contextmanager
    def locks(self, item, names):
        files = []
        try:
            for name in names:
                require(name in ('attachments.lock', 'provisioning.lock'))
                path = unlinked(self.diagnostics(item) / name)
                descriptor = os.open(path, os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
                stream = os.fdopen(descriptor, 'a+')
                fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
                files.append(stream)
            yield files
        finally:
            for stream in reversed(files):
                stream.close()

    def down(self, item, refusal=None):
        require(item.get('owner') and RUN.fullmatch(item['owner']['runId']), 'HXLQ203')
        if not refusal:
            record = self.public(item)
            record['cleanupVerified'] = False
            record.pop('ownedLeftovers', None)
            record.pop('cleanupFailure', None)
            self.save()
        code, result = self.invoke(['down', '--run-id=' + item['owner']['runId']])
        if refusal:
            require(refusal in ('HXR037', 'HXR023') and code != 0 and result.get('outcome', {}).get('ruleId') == refusal)
            self.observe('real-lock-refusal', ownerRunId=item['owner']['runId'], ruleId=refusal)
            return
        remaining = result.get('remainingResources')
        require(code == 0 and isinstance(remaining, dict), 'HXLQ206')
        keys = ('containerIds', 'processIds', 'workspaceIds', 'claimIds', 'discoveryFailures')
        require(remaining.get('unverified') is False and all(remaining.get(key) == [] for key in keys)
                and not self.containers(item) and not Path(item['workspace']).exists(), 'HXLQ206')
        self.settle_tool(item)
        require(not self.owned_processes(item), 'HXLQ206')
        receipt = self.states / 'claims/owners' / (item['owner']['runId'] + '.json')
        require(not receipt.exists() or read(receipt) == [], 'HXLQ206')
        for claim in (self.states / 'claims').glob('*.json'):
            require(read(claim).get('OwnerRunId') != item['owner']['runId'], 'HXLQ206')
        self.outcome(item)
        self.public(item).update(cleanupVerified=True, ownedLeftovers={key: [] for key in keys})
        self.observe('exact-cleanup-verified', ownerRunId=item['owner']['runId'])

    def settle_tool(self, item):
        child = self.children.get(item['label'])
        identity = item.get('identity', {})
        require(child is not None and type(identity.get('pid')) is int and identity['pid'] > 1
                and child.pid == identity['pid'] and isinstance(identity.get('birth'), str)
                and identity['birth'].isdigit() and len(identity['birth']) <= 32
                and type(identity.get('session')) is int and identity['session'] > 1, 'HXLQ203')
        # Popen.wait reaps this exact issued child, including its non-readable exit transition.
        exit_code = child.wait(timeout=TOOL_STOP_SECONDS)
        require(type(exit_code) is int and -128 <= exit_code <= 255, 'HXLQ206')
        self.observe('owner-tool-reaped', ownerRunId=item['owner']['runId'], exitCode=exit_code,
                     identity={key: identity[key] for key in ('pid', 'birth', 'session')})

    def owned_processes(self, item):
        found = []
        issued_birth = item.get('identity', {}).get('birth')
        require(isinstance(issued_birth, str) and issued_birth.isdigit() and len(issued_birth) <= 32, 'HXLQ203')
        boundary = {'ownerToolBirthTicks': issued_birth, 'excludedPreexistingProcesses': 0, 'excludedExitedProcesses': 0,
                    'checkedSinceOwnerStart': 0, 'completeWithinBoundary': False}
        self.public(item)['processDiscovery'] = boundary
        for entry in Path('/proc').iterdir():
            if not entry.name.isdigit():
                continue
            identity = {'pid': int(entry.name), 'birth': None, 'parentPid': None, 'session': None, 'state': None}
            stage = 'uid'
            try:
                if entry.stat().st_uid != os.geteuid():
                    continue
                stage = 'stat'
                status = (entry / 'stat').read_text()
                require(len(status) <= 8192, 'HXLQ206')
                fields = status.rpartition(')')[2].split()
                require(len(fields) >= 20 and re.fullmatch(r'[A-Za-z]', fields[0])
                        and all(value.isdigit() and len(value) <= 32
                                                for value in (fields[1], fields[3], fields[19])), 'HXLQ206')
                born = fields[19]
                require(born.isdigit() and len(born) <= 32, 'HXLQ206')
                identity.update(birth=born, parentPid=int(fields[1]), session=int(fields[3]), state=fields[0])
                if int(born) < int(issued_birth):
                    # Such a process cannot descend from this freshly issued owner.
                    boundary['excludedPreexistingProcesses'] += 1
                    continue
                if fields[0] in ('Z', 'X'):
                    boundary['excludedExitedProcesses'] += 1
                    continue
                boundary['checkedSinceOwnerStart'] += 1
                stage = 'environ'
                environment = (entry / 'environ').read_bytes()
                require(len(environment) <= 1_048_576, 'HXLQ206')
                if ('HEXALITH_G4_RUN_ID=' + item['owner']['runId']).encode() in environment.split(b'\0'):
                    stage = 'owned-identity'
                    found.append(process(int(entry.name), item['owner']['runId']))
            except PermissionError:
                boundary['refusal'] = {**identity, 'readStage': stage}
                self.observe('process-read-refused', ownerRunId=item['owner']['runId'],
                             ownerToolBirthTicks=issued_birth, **boundary['refusal'])
                raise
            except (FileNotFoundError, ProcessLookupError):
                continue
        boundary['completeWithinBoundary'] = True
        return found

    def peer_scope(self, item):
        state = self.state(item)
        require(state['status'] == 'Ready' and state.get('testRun') is None)
        identity = process(state['appHostProcessId'], item['owner']['runId'])
        claims = self.states / 'claims'
        paths = read(claims / 'owners' / (item['owner']['runId'] + '.json'))
        require(isinstance(paths, list) and all(HASH.fullmatch(value) for value in paths))
        hashes = {}
        for key in paths:
            path = claims / (key + '.json')
            require(read(path).get('OwnerRunId') == item['owner']['runId'], 'HXLQ203')
            hashes[key] = sha(path)
        return {'containers': self.containers(item), 'appHost': identity, 'claimHashes': hashes,
                'workspacePresent': Path(item['workspace']).is_dir(), 'native': process(item['native']['pid'], item['owner']['runId'])}

    def cleanup_all(self):
        success = True
        for item in self.owners:
            try:
                self.resume(item)
                child = self.children.get(item['label'])
                if child is not None and child.poll() is None:
                    self.signal_tool(item, signal.SIGINT)
                    try:
                        child.wait(timeout=TOOL_STOP_SECONDS)
                    except subprocess.TimeoutExpired:
                        self.signal_tool(item, signal.SIGKILL)
                        child.wait(timeout=10)
                        self.public(item)['forcedToolStop'] = True
                self.discover(item)
                if item.get('owner') and not self.public(item)['cleanupVerified']:
                    self.down(item)
                elif not item.get('owner'):
                    require(child is not None and child.poll() is not None, 'HXLQ206')
                    self.public(item)['notAllocatedVerified'] = True
            except Exception as error:
                success = False
                self.public(item)['cleanupFailure'] = failure(error)
                self.save()
        return success

    def run(self, selected):
        clean = True
        try:
            for name in selected:
                if CANCELLED or not clean:
                    break
                row = self.journal['scenarios'][name]
                first_owner = len(self.owners)
                row.update(status='in-progress', startedAtUtc=now())
                self.save()
                try:
                    reasons = getattr(self, 'case_' + name.replace('-', '_'))()
                    if any(not self.public(item).get('startupDurationProof') for item in self.owners[first_owner:]):
                        reasons = [*reasons, 'startup-duration-producer-unavailable']
                    if any(not self.public(item).get('originalMappingProof') for item in self.owners[first_owner:]):
                        reasons = [*reasons, 'original-source-mapping-proof-unavailable']
                    require(all(reason in MISSING for reason in reasons))
                    row.update(status='incomplete' if reasons else 'passed', missingProof=reasons)
                    self.journal['completedCases'].append(name)
                except Exception as error:
                    row.update(status='failed', failure=failure(error))
                finally:
                    row['finishedAtUtc'] = now()
                    clean = self.cleanup_all()
                    self.save()
        finally:
            clean = self.cleanup_all() and clean
            self.journal['finalizationVerified'] = clean
            self.journal['fullCampaignPassed'] = clean and all(self.journal['scenarios'][name]['status'] == 'passed' for name in SCENARIOS)
            self.journal['selectedPassed'] = clean and all(self.journal['scenarios'][name]['status'] == 'passed' for name in selected)
            self.save()
        return 0 if self.journal['selectedPassed'] else 1

    def case_success_failure(self):
        for label, action in (('success', 'pass'), ('failure', 'fail')):
            item = self.start(label)
            self.finish(item, action)
            self.children[label].wait(timeout=150)
            if action == 'pass':
                require(not self.containers(item) and not Path(item['workspace']).exists())
            else:
                require(self.public(item)['failureRetained'] and self.containers(item) and Path(item['workspace']).is_dir())
            self.down(item)
            self.down(item)
        return []

    def case_startup_timeout_partial(self):
        item = self.start('timeout', startup=True)
        remaining = (utc(item['owner']['startupDeadlineUtc']) - datetime.now(timezone.utc)).total_seconds()
        self.children['timeout'].wait(timeout=max(0, remaining) + 150)
        require(self.outcome(item)['terminalCause'] == 'startup-timeout' and self.public(item)['failureRetained'])
        require(self.containers(item) and Path(item['workspace']).is_dir())
        self.resume(item)
        self.down(item)
        item = self.start('partial', startup=True)
        self.signal_tool(item, signal.SIGKILL)
        self.children['partial'].wait(timeout=10)
        self.resume(item)
        self.down(item)
        self.down(item)
        return []

    def case_cancellation_collision(self):
        item = self.start('cancel')
        self.signal_tool(item, signal.SIGINT)
        self.children['cancel'].wait(timeout=150)
        require(self.outcome(item)['terminalStatus'] == 'cancelled')
        self.down(item)
        return ['exclusive-resource-selection-unavailable']

    def case_owner_finish_hold(self):
        item = self.start('finish-hold')
        admitted = self.attachment(item, 120)
        self.attachment(item, 120, previous=admitted)
        self.finish(item, 'pass')
        require(self.children[item['label']].poll() is None and self.containers(item))
        self.attachment(item, expected_code='HXR035')
        self.end_hold(item, admitted, 'passed')
        self.children[item['label']].wait(timeout=150)
        require(not self.containers(item))
        self.down(item)
        return []

    def case_owner_cancel_hold(self):
        item = self.start('cancel-hold')
        admitted = self.attachment(item, 120)
        with self.locks(item, ('provisioning.lock',)):
            self.signal_tool(item, signal.SIGINT)
            ended = self.poll(lambda: value if (value := self.observed_hold(item, admitted))['endedAtUtc'] else None, 30, allow_exit=True)
            require(ended['outcome'] == 'withdrawn' and ended['endReason'] == 'owner-cancelled' and self.containers(item))
            self.observe('withdrawn-before-teardown', attachment=ended, ownedResourcesStillLive=True)
        self.children[item['label']].wait(timeout=150)
        require(self.outcome(item)['terminalStatus'] == 'cancelled')
        self.down(item)
        return ['attached-work-access-producer-unavailable']

    def case_attached_failure_expiry(self):
        item = self.start('attached-failure')
        admitted = self.attachment(item, 120)
        self.end_hold(item, admitted, 'failed')
        failed = self.observed_hold(item, admitted)
        self.finish(item, 'pass')
        self.children[item['label']].wait(timeout=150)
        require(self.containers(item) and Path(item['workspace']).is_dir())
        expiring = self.attachment(item, 2)
        self.raw_hold_snapshot(item, expiring, 'before-expiry')
        while datetime.now(timezone.utc) < utc(expiring['expiresAtUtc']):
            require(not CANCELLED, 'HXLQ208')
            time.sleep(0.1)
        self.attachment(item, 2, previous=expiring, expected_code='HXR036')
        expired = self.public_expiry(item, expiring, failed)
        require(self.containers(item))
        require(self.observed_hold(item, admitted) == failed, 'HXLQ205')
        self.observe('actual-hold-expiry', attachment=expired, retainedResourcesStillLive=True)
        self.down(item)
        durable = self.raw_hold_snapshot(item, expiring, 'after-cleanup')['attachment']
        require(durable == expired and self.observed_hold(item, admitted) == failed, 'HXLQ205')
        return ['attached-suite-execution-unavailable', 'attached-work-access-producer-unavailable']

    def case_failure_then_cancellation(self):
        item = self.start('failure-cancel')
        self.finish(item, 'fail')
        reasons = []
        if self.children[item['label']].poll() is None:
            try:
                self.signal_tool(item, signal.SIGINT)
            except (FileNotFoundError, ProcessLookupError):
                reasons.append('owner-cancellation-window-missed')
        else:
            reasons.append('owner-cancellation-window-missed')
        self.children[item['label']].wait(timeout=150)
        require(self.outcome(item)['terminalStatus'] == 'failed' and self.public(item)['failureRetained'] and self.containers(item))
        self.down(item)
        return reasons

    def case_locks_retry_peer(self):
        item, peer = self.begin('retry'), self.begin('peer')
        self.ready(item)
        self.ready(peer)
        before = self.peer_scope(peer)
        admitted = self.attachment(item, 240)
        with self.locks(item, ('attachments.lock', 'provisioning.lock')) as held:
            self.down(item, 'HXR037')
            first = self.outcome(item, False)
            require(self.containers(item) and self.peer_scope(peer) == before)
            fcntl.flock(held[0], fcntl.LOCK_UN)
            self.down(item, 'HXR023')
            self.outcome(item)
            require(first is None or first == item['first'], 'HXLQ205')
            ended = self.observed_hold(item, admitted)
            require(ended['outcome'] == 'withdrawn' and ended['endReason'] == 'developer-stop' and self.containers(item))
            require(self.peer_scope(peer) == before)
        self.down(item)
        self.down(item)
        require(self.peer_scope(peer) == before)
        self.finish(peer, 'pass')  # The genuine suite rechecks real persisted peer data before returning.
        self.children[peer['label']].wait(timeout=150)
        self.down(peer)
        self.observe('real-peer-functional-recheck-passed', ownerRunId=peer['owner']['runId'])
        return []


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--checkout', type=Path)
    parser.add_argument('--work', type=Path)
    parser.add_argument('--evidence', type=Path)
    parser.add_argument('--tool-assembly', type=Path)
    parser.add_argument('--tool-package', type=Path)
    parser.add_argument('--tool-sha256', default=PACKAGE_SHA256)
    parser.add_argument('--environment', type=Path, help='Private producer-validated package build environment JSON; values are never retained.')
    parser.add_argument('--scenario', action='append', choices=SCENARIOS)
    parser.add_argument('--_launch', type=Path, help=argparse.SUPPRESS)
    args = parser.parse_args(argv)
    if args._launch:
        return gated_launch(args._launch)
    require(all(getattr(args, key) is not None for key in ('checkout', 'work', 'evidence', 'tool_assembly', 'tool_package')), 'HXLQ201')
    selected = args.scenario or list(SCENARIOS)
    require(len(set(selected)) == len(selected))
    for number in (signal.SIGINT, signal.SIGTERM):
        signal.signal(number, lambda received, frame: CANCELLED.append(received))
    campaign = None
    try:
        campaign = Campaign(args.checkout, args.work, args.evidence, args.tool_assembly,
                            args.tool_package, args.tool_sha256, selected, args.environment)
        return campaign.run(selected)
    except Exception as error:
        print(json.dumps(failure(error)), file=sys.stderr)
        if campaign is not None:
            campaign.cleanup_all()
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
