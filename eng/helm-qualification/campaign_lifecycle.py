"""Durable full-campaign mode of Qualification, using Builds' environment contract.

This module extends the existing Platform kind runner. It never runs Memories
assertions, appoints C1 authority, or grants qualification from environment state.
"""
from __future__ import annotations

import argparse
import base64
import copy
from datetime import datetime, timezone
import fcntl
import hashlib
import hmac
import json
import os
from pathlib import Path
import re
import signal
import shutil
import stat
import sys
import time
import uuid

import campaign_capacity as capacity
import campaign_environment as environment
from qualify import Qualification, HERE, ROOT, CHART, documents, sha

SCHEMA = 'hexalith.platform.campaign-lifecycle/v1'
POLICY = 'hexalith.platform.candidate-and-attachment-policy.v1'
MAXIMUM_BYTES = 16 * 1024 * 1024
STARTUP_SECONDS = 600
STOPPING_SECONDS = 900
POLL_SECONDS = 5


def require(condition, code):
    if not condition:
        raise ValueError('campaign-lifecycle-' + code)


def strict(raw):
    def pairs(items):
        result = {}
        for k, v in items:
            require(k not in result, 'duplicate-key')
            result[k] = v
        return result
    return json.loads(raw, object_pairs_hook=pairs, parse_constant=lambda _: require(False, 'nonfinite'))


def private(path, directory=False):
    path = Path(path)
    require(path.is_absolute() and path == path.resolve() and not path.is_symlink(), 'custody-path')
    info = path.stat()
    require(info.st_uid == os.geteuid() and stat.S_IMODE(info.st_mode) == (0o700 if directory else 0o600), 'private-custody-required')
    require(stat.S_ISDIR(info.st_mode) if directory else stat.S_ISREG(info.st_mode), 'custody-kind')
    return path


def read_private(path, expected=None):
    private(path); private(Path(path).parent, directory=True)
    return capacity.snapshot(path, expected)


def atomic(path, value, *, exclusive=False):
    path = Path(path)
    raw = value if isinstance(value, bytes) else capacity.canonical(value)
    require(len(raw) <= MAXIMUM_BYTES, 'state-too-large')
    temp = path.with_name('.' + path.name + '-' + uuid.uuid4().hex)
    fd = os.open(temp, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    try:
        with os.fdopen(fd, 'wb') as stream:
            stream.write(raw); stream.flush(); os.fsync(stream.fileno())
        if exclusive:
            os.link(temp, path)
            temp.unlink()
        else:
            os.replace(temp, path)
        descriptor = os.open(path.parent, os.O_DIRECTORY)
        try:
            os.fsync(descriptor)
        finally:
            os.close(descriptor)
    finally:
        if temp.exists():
            temp.unlink()


def utc(milliseconds):
    return datetime.fromtimestamp(milliseconds / 1000, timezone.utc).isoformat(timespec='milliseconds').replace('+00:00', 'Z')


def binary(ref):
    require(set(ref) == {'path', 'sha256'}, 'tool-ref')
    path = Path(ref['path'])
    require(path.is_absolute() and path.resolve() == path and path.is_file() and not path.is_symlink(), 'tool-path')
    require(path.stat().st_size <= 512 * 1024 * 1024, 'tool-too-large')
    with path.open('rb') as stream:
        require(hashlib.file_digest(stream, 'sha256').hexdigest() == ref['sha256'], 'tool-drift')
    return str(path)


def signed(path, key_ref, raw=None):
    key = read_private(key_ref['path'], key_ref['sha256'])
    require(len(key) >= 32, 'authority-key')
    value = strict(read_private(path) if raw is None else raw)
    require(isinstance(value, dict) and 'signature' in value, 'signed-input-required')
    body = {k: v for k, v in value.items() if k != 'signature'}
    require(hmac.compare_digest(str(value['signature']), hmac.new(key, capacity.canonical(body), hashlib.sha256).hexdigest()), 'signature-invalid')
    return body


def clock(plan):
    current = signed(plan['clock']['samplePath'], plan['clock']['key'])
    require(set(current) == {'utcMs', 'boottimeNs', 'bootId', 'sourceId'} and current['sourceId'] == plan['clock']['sourceId'], 'clock-binding')
    elapsed = capacity._boottime() - capacity.integer(current['boottimeNs'])
    require(current['bootId'] == capacity._boot() and 0 <= elapsed <= 60_000_000_000, 'independent-clock-stale')
    return capacity.integer(current['utcMs'], 1) + elapsed // 1_000_000


def authority(plan, plan_sha, action, now):
    value = signed(plan['authority']['statusPath'], plan['authority']['key'])
    require(set(value) == {'issuer', 'owner', 'runId', 'planSha256', 'observedUtcMs', 'expiresUtcMs', 'disposition', 'actions', 'capacityReceipt'}, 'authority-shape')
    require(value['issuer'] == plan['authority']['issuer'] and value['owner'] == 'github:user:6775094'
            and value['runId'] == plan['runId'] and value['planSha256'] == plan_sha, 'authority-binding')
    require(value['disposition'] == 'active' and action in value['actions'] and set(value['actions']) <= {'prepare', 'launch', 'monitor'}, 'authority-withdrawn-or-action')
    observed = capacity.integer(value['observedUtcMs'], 1)
    require(0 <= now - observed <= 300000 and now < value['expiresUtcMs'] <= observed + 900000, 'authority-stale')
    return value


def load_plan(path, expected):
    require(re.fullmatch('[0-9a-f]{64}', expected or '') is not None, 'outside-plan-approval-required')
    plan = strict(read_private(path, expected))
    require(set(plan) == {'schema', 'runId', 'profile', 'profileSha256', 'initialAdmission', 'clock', 'authority', 'window',
                          'docker', 'paths', 'tools', 'toolFiles', 'assets', 'environment', 'source'}, 'plan-incomplete')
    require(plan['schema'] == SCHEMA and re.fullmatch('[0-9a-f]{32}', plan['runId']) is not None, 'plan-identity')
    require(re.fullmatch('[0-9a-f]{64}', plan['profileSha256']) is not None, 'canonical-profile-required')
    require(set(plan['window']) == {'earliestLaunchUtcMs', 'latestLaunchUtcMs', 'expiresUtcMs'} and
            plan['window']['earliestLaunchUtcMs'] <= plan['window']['latestLaunchUtcMs'] < plan['window']['expiresUtcMs'] and
            plan['window']['expiresUtcMs'] - plan['window']['earliestLaunchUtcMs'] <= capacity.CAMPAIGN_SECONDS * 1000, 'window-invalid')
    require(set(plan['tools']) == {'docker', 'kind', 'kubectl', 'helm', 'dotnet', 'buildsCli'}, 'tools-incomplete')
    require(plan['docker']['host'].startswith('unix:///') and bool(plan['docker']['engineId']), 'physical-engine-required')
    require(set(plan['paths']) == {'primary', 'backup'}, 'storage-paths')
    for value in plan['paths'].values():
        private(value, directory=True)
    require(Path(plan['paths']['primary']).resolve() != Path(plan['paths']['backup']).resolve(), 'backup-alias')
    return plan


MONITOR_SCHEMA = 'hexalith.platform.campaign-monitor/v1'
PG_SERIES = {'tableCount', 'relationBytes', 'liveRecords', 'deadRecords', 'supportBytes', 'walBytes',
    'connections', 'connectionUtilizationPermille', 'transactionLatencyP99Ms', 'transactionErrors',
    'checkpointsTimed', 'checkpointsRequested', 'checkpointAgeSeconds', 'vacuumProgress',
    'autovacuumStallSeconds', 'lockWaitMilliseconds', 'oldestDueAgeSeconds', 'physicalEvidenceAgeSeconds', 'recoveryUnhealthy'}
RUNTIME_SERIES = {'tlsAuthErrors', 'daprTransactionErrors', 'etagErrors', 'restartUnhealthy'}
MONITOR_SERIES = {'postgres.' + name for name in PG_SERIES} | {'runtime.' + name for name in RUNTIME_SERIES}
MONITOR_LIMITS = {'connections', 'connectionUtilizationPermille', 'transactionLatencyP99Ms', 'transactionErrors',
    'checkpointAgeSeconds', 'autovacuumStallSeconds', 'lockWaitMilliseconds', 'oldestDueAgeSeconds',
    'physicalEvidenceAgeSeconds', 'recoveryUnhealthy', *RUNTIME_SERIES}
GATE = 'access-telemetry-qualification-gate'
LEASE = 'access-telemetry-qualification'
REPORTER = 'access-telemetry-physical-evidence-reporter'
CONTROLLED_WRITERS = {'memories-access-telemetry': 2, 'memories-access-telemetry-clock': 1}


# Closed literals from the unchanged Memories C4 source registry. A rule still
# binds that source's command hash, resource identity and finite schedule window.
C4_ENVIRONMENT = {
    'bad-configuration': ('memories-access-telemetry', 'lifecycle', 'Retention', '00:00:01'),
    'bad-key': ('memories-access-telemetry', 'lifecycle', 'AttestationVerificationKey', 'invalid'),
    'degraded-rollback': ('memories-access-telemetry', 'lifecycle', 'Enabled', 'false'),
    'etag-failure': ('memories-access-telemetry', 'lifecycle', 'StateStoreName', 'qualification-etag-failure'),
    'profile-drift': ('memories-access-telemetry', 'lifecycle', 'ComponentProfileHash', '0' * 64),
    'queue-byte-exhaustion': ('memories', 'memories', 'QueueByteLimit', '1024'),
    'queue-record-exhaustion': ('memories', 'memories', 'QueueRecordLimit', '1'),
    'retry-exhaustion': ('memories', 'memories', 'RetryMaximumDelay', '00:00:00.100'),
    'ttl-failure': ('memories-access-telemetry', 'lifecycle', 'SchemaVersion', '2'),
    'transaction-failure': ('memories-access-telemetry', 'lifecycle', 'ConfigurationStoreName', 'qualification-transaction-failure'),
}
C4_EXPECTED_ERRORS = {
    'etag-failure': {'runtime.etagErrors'},
    'transaction-failure': {'runtime.daprTransactionErrors', 'postgres.transactionErrors'},
    'capacity-pressure': {'runtime.daprTransactionErrors', 'postgres.transactionErrors', 'postgres.recoveryUnhealthy', 'runtime.restartUnhealthy'},
    'state-outage': {'runtime.daprTransactionErrors', 'postgres.transactionErrors'},
    'approved-adapter-fault': {'runtime.daprTransactionErrors', 'postgres.transactionErrors'},
}


def c4_changes(scenario, rid, namespace):
    if scenario in C4_ENVIRONMENT:
        resource, container, setting, value = C4_ENVIRONMENT[scenario]
        if rid == 'Deployment/' + namespace + '/' + resource:
            return [{'container': container, 'field': 'env', 'name': 'AccessTelemetryLifecycle__' + setting, 'value': value}]
    if scenario == 'capacity-pressure' and rid == 'StatefulSet/' + namespace + '/access-telemetry-postgresql':
        return [{'container': 'postgresql', 'field': 'resources', 'section': section, 'name': 'memory', 'value': '1Mi'} for section in ('requests', 'limits')]
    return []


def validate_fault_changes(fault, commands, namespace):
    changes, allowances = fault['changes'], fault['metricAllowances']
    require(isinstance(changes, list) and isinstance(allowances, dict), 'fault-delta-shape')
    require(changes == c4_changes(fault['scenario'], fault['resourceId'], namespace), 'fault-delta-not-in-source-registry')
    if changes or allowances:
        require(fault['commandId'] == 'C4.other', 'configuration-fault-outside-c4-source')
    allowed = C4_EXPECTED_ERRORS.get(fault['scenario'], set())
    if allowances:
        expected_resource = ('Deployment/' + namespace + '/memories-access-telemetry' if fault['scenario'] in {'etag-failure', 'transaction-failure'}
                             else 'StatefulSet/' + namespace + '/access-telemetry-postgresql')
        require(fault['resourceId'] == expected_resource, 'fault-metric-resource-differs')
    require(set(allowances) <= allowed and all(type(v) is int and 0 < v <= 2 * commands[fault['commandId']]['acceptedRecordUpperBound'] for v in allowances.values()), 'fault-metric-allowance-invalid')


def change_matches(spec, change):
    container = next((c for c in spec['template']['spec']['containers'] if c['name'] == change['container']), None)
    if container is None:
        return False
    if change['field'] == 'env':
        return next((v for v in container.get('env', []) if v['name'] == change['name']), None) == {'name': change['name'], 'value': change['value']}
    return container.get('resources', {}).get(change['section'], {}).get(change['name']) == change['value']


def normalize_changes(spec, approved, changes):
    for change in changes:
        if not change_matches(spec, change):
            continue
        container = next(c for c in spec['template']['spec']['containers'] if c['name'] == change['container'])
        original = next(c for c in approved['template']['spec']['containers'] if c['name'] == change['container'])
        if change['field'] == 'env':
            before = next((v for v in original.get('env', []) if v['name'] == change['name']), None)
            container['env'] = [copy.deepcopy(before) if v['name'] == change['name'] else v for v in container.get('env', []) if v['name'] != change['name'] or before is not None]
            if not container['env'] and 'env' not in original:
                container.pop('env')
        else:
            section = container['resources'][change['section']]
            before = original.get('resources', {}).get(change['section'], {})
            if change['name'] in before:
                section[change['name']] = before[change['name']]
            else:
                section.pop(change['name'], None)
                if not section and change['section'] not in original.get('resources', {}):
                    container['resources'].pop(change['section'])


def cause_code(error):
    if isinstance(error, (KeyboardInterrupt, InterruptedError)):
        return 'run-cancelled'
    value = str(error)
    if re.fullmatch(r'(?:campaign-lifecycle-|environment-|campaign-capacity-)[a-z0-9-]{1,96}', value):
        return value
    if value == 'bounded command timed out':
        return 'command-timeout'
    if value == 'bounded command refused: output-limit':
        return 'command-output-limit'
    return 'command-failed' if isinstance(error, RuntimeError) else 'invalid-operational-input'


def resource_id(obj, namespace):
    return obj['kind'] + '/' + obj['metadata'].get('namespace', namespace) + '/' + obj['metadata']['name']


def contains(actual, expected):
    """Admitted fields must survive API defaulting; lists cannot gain entries."""
    if isinstance(expected, dict):
        return isinstance(actual, dict) and all(k in actual and contains(actual[k], v) for k, v in expected.items())
    if isinstance(expected, list):
        return isinstance(actual, list) and len(actual) == len(expected) and all(contains(a, b) for a, b in zip(actual, expected))
    return actual == expected


def monitoring_config(plan, profile):
    config = plan['environment']['monitoring']
    require(set(config) == {'collectorId', 'source', 'commandSha256', 'key', 'receiptPath', 'allocations', 'limits'}, 'collector-configuration')
    capacity.reference(config['source'])
    require(config['source'] in plan['source'] and re.fullmatch('[0-9a-f]{64}', config['commandSha256']), 'collector-source-binding')
    require(set(config['allocations']) == set(profile['sizing']['allocations']), 'collector-allocation-series')
    for name, identity in config['allocations'].items():
        require(set(identity) == {'poolId', 'identity'} and bool(identity['identity']) and
                any(pool['poolId'] == identity['poolId'] and name in pool['allocations'] for pool in profile['storage']), 'collector-backing-binding')
    require(len({v['identity'] for v in config['allocations'].values()}) == len(config['allocations']), 'collector-allocation-alias')
    limits = config['limits']
    require(set(limits) == MONITOR_LIMITS and all(type(v) is int and v >= 0 for v in limits.values()), 'collector-limits-incomplete')
    require(0 < limits['connections'] <= 100 and 0 < limits['connectionUtilizationPermille'] <= 900
            and 0 < limits['transactionLatencyP99Ms'] and 0 < limits['checkpointAgeSeconds']
            and 0 < limits['autovacuumStallSeconds'] and 0 < limits['lockWaitMilliseconds']
            and 0 < limits['oldestDueAgeSeconds'] <= 900 and 0 < limits['physicalEvidenceAgeSeconds'] <= 86400
            and all(limits[k] == 0 for k in ('transactionErrors', 'recoveryUnhealthy', *RUNTIME_SERIES)), 'collector-limits-unsafe')
    return config


def monitor_receipt(plan, plan_sha, now):
    config = plan['environment']['monitoring']
    raw = read_private(config['receiptPath'])
    value = signed(config['receiptPath'], config['key'], raw=raw)
    require(set(value) == {'schema', 'runId', 'planSha256', 'profileSha256', 'collectorId', 'sourceSha256', 'commandSha256',
        'observedUtcMs', 'intervalStartUtcMs', 'boottimeNs', 'bootId', 'phase', 'target', 'allocations', 'series', 'expectedFaults'}, 'collector-series-shape')
    require(value['schema'] == MONITOR_SCHEMA and value['runId'] == plan['runId'] and value['planSha256'] == plan_sha
            and value['profileSha256'] == plan['profileSha256'] and value['collectorId'] == config['collectorId']
            and value['sourceSha256'] == config['source']['sha256'] and value['commandSha256'] == config['commandSha256'], 'collector-binding')
    require(value['bootId'] == capacity._boot() and 0 <= capacity._boottime() - value['boottimeNs'] <= 60_000_000_000
            and 0 <= now - value['observedUtcMs'] <= 60000, 'collector-stale')
    require(type(value['intervalStartUtcMs']) is int and 0 <= value['observedUtcMs'] - value['intervalStartUtcMs'] <= 60000
            and isinstance(value['expectedFaults'], list), 'collector-interval-invalid')
    require(set(value['series']) == MONITOR_SERIES and set(value['allocations']) == set(config['allocations']), 'collector-series-missing')
    for name, item in value['allocations'].items():
        require(set(item) == {'poolId', 'identity', 'scope', 'usedBytes', 'availableBytes'}
                and all(item[k] == config['allocations'][name][k] for k in ('poolId', 'identity'))
                and item['scope'] == 'owned-allocation', 'collector-allocation-identity')
    value['_receiptSha256'] = hashlib.sha256(raw).hexdigest()
    return value


def original_admission(plan):
    item = plan['initialAdmission']
    require(set(item) == {'receipt', 'issuer', 'key'}, 'initial-admission-required')
    return dict(admission=read_private(item['receipt']['path'], item['receipt']['sha256']), admission_sha256=item['receipt']['sha256'],
                issuer=item['issuer'], key=read_private(item['key']['path'], item['key']['sha256']), key_sha256=item['key']['sha256'])


def admit(plan, plan_sha, action):
    now = clock(plan)
    require(now < plan['window']['expiresUtcMs'], 'reservation-expired')
    status = authority(plan, plan_sha, action, now)
    profile = capacity.read_bound(plan['profile']['path'], plan['profile']['sha256'], now_ms=now, check_freshness=False)
    capacity.admit_launch(profile, status['capacityReceipt']['path'], status['capacityReceipt']['sha256'], now_ms=now,
                          original_admission=original_admission(plan), require_original=True)
    for ref in [*plan['tools'].values(), *plan['toolFiles']]:
        binary(ref)
    expected_sources = {str((HERE / name).resolve()) for name in ('qualify.py', 'campaign_capacity.py', 'campaign_environment.py', 'campaign_lifecycle.py')}
    require(expected_sources <= {r['path'] for r in plan['source']}, 'lifecycle-source-incomplete')
    profile_sources = {r['path']: r['sha256'] for r in profile['inputs']['source']}
    for ref in plan['source']:
        capacity.reference(ref)
        require(profile_sources.get(ref['path']) == ref['sha256'], 'profile-source-binding')
    tool_manifest = capacity.retained_json(profile['inputs']['tool'][0])
    require(tool_manifest == {'tools': plan['tools'], 'toolFiles': plan['toolFiles'], 'assets': plan['assets']}, 'profile-tool-binding')
    require(len(profile['inputs']['applications']) == 1 and capacity.retained_json(profile['inputs']['applications'][0]) == plan['environment'], 'profile-environment-binding')
    pins = json.loads((HERE / 'toolchain.json').read_bytes())
    require(set(plan['assets']) == {'dapr', 'gateway', 'network'}, 'assets-incomplete')
    for name, ref in plan['assets'].items():
        capacity.reference(ref)
        if name in pins:
            require(ref['sha256'] == pins[name]['sha256'], 'asset-pin-drift')
    commands = {v['commandId']: v for v in capacity.retained_json(profile['inputs']['workloadPlan'][0])['commandInventory']}
    for fault in plan['environment']['faultWindows']:
        require(set(fault) == {'commandId', 'commandSha256', 'resourceId', 'startSeconds', 'endSeconds', 'replicas', 'allowRolloutRestart', 'scenario', 'changes', 'metricAllowances'}, 'fault-window-shape')
        validate_fault_changes(fault, commands, profile['target']['namespace'])
        require(fault['commandId'] in commands and capacity.digest(commands[fault['commandId']]) == fault['commandSha256'], 'fault-command-binding')
        require(type(fault['allowRolloutRestart']) is bool and type(fault['replicas']) is list and all(type(v) is int and 0 <= v <= 3 for v in fault['replicas']), 'fault-delta-invalid')
        require(0 <= fault['startSeconds'] < fault['endSeconds'] <= capacity.CAMPAIGN_SECONDS and fault['endSeconds'] - fault['startSeconds'] <= 900, 'fault-window-bound')
        command = fault['commandId']
        if '/' in command:
            round_id, gate = command.split('/', 1)
            slot = next(r['captureBoundsSeconds'][gate] for r in profile['renewalSchedule']['rounds'] if r['id'] == round_id)
        else:
            require(command.startswith(('C2.', 'C4.')), 'fault-phase-refused')
            slot = profile['renewalSchedule']['phases'][command[:2].lower()]
        require(slot[0] <= fault['startSeconds'] < fault['endSeconds'] <= slot[1], 'fault-outside-source-window')
    monitoring_config(plan, profile)
    if action in {'prepare', 'launch'}:
        receipt = monitor_receipt(plan, plan_sha, now)
        require(receipt['phase'] == 'preflight' and receipt['target'] is None and not receipt['expectedFaults'] and all(v is None for v in receipt['series'].values()), 'collector-preflight-required')
    holders = plan['environment']['leaseHolders']
    require(isinstance(holders, list) and holders and len(holders) == len(set(holders)) and all(re.fullmatch(r'story-27-4/owner-6775094/[0-9a-f]{32}', v) for v in holders), 'exact-qualification-holders-required')
    topology = environment.topology(profile, plan['environment'], plan['runId'])
    supplemental = [*capacity.retained_json(plan['environment']['providerObjects']), *capacity.retained_json(plan['environment']['reporterObjects'])]
    network = documents(capacity.snapshot(plan['assets']['network']['path'], plan['assets']['network']['sha256']).decode())
    environment.supplemental_budget([*supplemental, *network])
    for ref in plan['environment']['secrets'] + [plan['environment']['openbaoSeed']]:
        read_private(ref['path'], ref['sha256'])
    return profile, topology, now


class CampaignQualification(Qualification):
    """Persistent mode of the existing kind qualifier, not a parallel runner."""

    schema = SCHEMA
    state_name = 'campaign-state.json'
    plan_name = 'campaign-plan.json'
    composition_id = capacity.PROFILE_ID
    execution_seconds = capacity.CAMPAIGN_SECONDS

    def admission(self, action):
        return admit(self.plan, self.record['planSha256'], action)

    @staticmethod
    def binding(profile, profile_sha256):
        return capacity.runtime_binding(profile, profile_sha256)

    def phases(self, profile):
        return profile['renewalSchedule']['phases']

    def __init__(self, evidence):
        # Qualification takes the same exclusive directory flock and closes on
        # initialization failure. No transient qualifier/fixture is constructed.
        private(evidence, directory=True)
        super().__init__(Path(evidence))

    def _initialize(self):
        self.state_path = self.evidence / self.state_name
        self.record = strict(read_private(self.state_path))
        self.plan_raw = read_private(self.evidence / self.plan_name, self.record['planSha256'])
        self.plan = strict(self.plan_raw)
        require(self.record['schema'] == self.schema and self.plan['runId'] == self.record['runId'], 'state-binding')
        self.temp = self.evidence
        self.cluster = self.record['target']['clusterName']
        self.context = self.record['target']['context']
        self.kubeconfig = self.evidence / 'kubeconfig'
        self.created = self.record['creationAttempted']
        self.tools = json.loads((HERE / 'toolchain.json').read_bytes())
        self.helm = self.plan['tools']['helm']['path']
        self.env = {k: v for k, v in os.environ.items() if k in {'PATH', 'HOME', 'TMPDIR', 'DOTNET_ROOT', 'NUGET_PACKAGES'}}
        # kind invokes Docker through PATH. Bind that transitive executable too.
        tool_bin = self.evidence / 'bin'
        tool_bin.mkdir(mode=0o700, exist_ok=True)
        docker_link = tool_bin / 'docker'
        if not docker_link.exists():
            docker_link.symlink_to(self.plan['tools']['docker']['path'])
        require(docker_link.is_symlink() and os.readlink(docker_link) == self.plan['tools']['docker']['path'], 'docker-tool-binding')
        self.env['PATH'] = str(tool_bin) + os.pathsep + self.env.get('PATH', '')
        self.env.update(KUBECONFIG=str(self.kubeconfig), DOCKER_HOST=self.plan['docker']['host'],
                        MSBUILDDISABLENODEREUSE='1', KIND_EXPERIMENTAL_PROVIDER='docker')
        self._cleanup_mode = False
        self._command_bound = None

    def save(self):
        atomic(self.state_path, self.record)

    def stamp(self):
        return {'utcMs': int(time.time() * 1000), 'boottimeNs': capacity._boottime(), 'bootId': capacity._boot()}

    def remaining(self, stopping=False):
        anchor = self.record['stopping'] if stopping else self.record['started']
        require(anchor is not None, 'original-budget-missing')
        require(anchor['bootId'] == capacity._boot(), 'boot-changed')
        elapsed = (capacity._boottime() - anchor['boottimeNs']) / 1e9
        require(elapsed >= 0, 'elapsed-regressed')
        end = self.record['stopDeadlineUtcMs'] if stopping else self.record['executionDeadlineUtcMs']
        return max(0, min((STOPPING_SECONDS if stopping else self.execution_seconds) - elapsed,
                          (end - int(time.time() * 1000)) / 1000))

    def run(self, args, **kwargs):
        if self.record.get('inFlight'):
            require(self._cleanup_mode, 'uncertain-command-no-replay')
        if not self._cleanup_mode and (self.evidence / 'stop-request.json').exists():
            raise InterruptedError('campaign-lifecycle-stop-requested')
        args = [str(v) for v in args]
        if args[0] in self.plan['tools']:
            args[0] = self.plan['tools'][args[0]]['path']
        # Cleanup uses original tool/target custody and survives configuration,
        # measurements or authority drift. Changing a tool binary still refuses.
        for name in self.plan['tools']:
            if args[0] == self.plan['tools'][name]['path']:
                binary(self.plan['tools'][name])
        limit = kwargs.get('timeout', 120)
        if self.record['started'] is not None:
            limit = min(limit, self.remaining(self._cleanup_mode))
        if self._command_bound is not None:
            limit = min(limit, max(0, self._command_bound - time.monotonic()))
        require(limit > 0, 'original-budget-exhausted')
        self.record['inFlight'] = {'commandSha256': capacity.digest(args), 'started': self.stamp()}
        self.save()
        previous_commands = len(self.record['commands'])
        try:
            result = super().run(args, **{**kwargs, 'timeout': limit, 'sensitive': True})
        except BaseException:
            # A failed mutating command may already have created resources. Do
            # not replay it. Stop/removal can still use the original ownership.
            self.record['commandCount'] += len(self.record['commands']) - previous_commands
            self.save()
            raise
        self.record['inFlight'] = None
        self.record['commandCount'] += len(self.record['commands']) - previous_commands
        # Finite command metadata/log ring; command stdout is never retained.
        while len(self.record['commands']) > 64:
            old = self.record['commands'].pop(0)
            (self.evidence / old['log']).unlink(missing_ok=True)
        self.save()
        return result

    def validate_descriptor(self, path=None):
        result = self.run(['dotnet', self.plan['tools']['buildsCli']['path'], 'validate', '--environment',
                           str(path or self.evidence / 'test-environment.json'), '--output', 'json'], timeout=30)
        require(strict(result.stdout).get('status') == 'validated', 'builds-descriptor-refused')

    def descriptor(self, readiness='pending', resources=()):
        unknown = {'state': 'not-yet-populated'}
        started = self.record['started']
        now = int(time.time() * 1000)
        resource_values = [{'resourceId': item['id'], 'moduleId': item['owner'], 'endpoints': []} for item in resources]
        deadlines = {'startup': unknown, 'execution': unknown}
        if started:
            deadlines['startup'] = {'state': 'populated', 'origin': 'environment-requested', 'originAt': utc(started['utcMs']),
                'effectiveSeconds': STARTUP_SECONDS, 'expiresAt': utc(started['utcMs'] + STARTUP_SECONDS * 1000), 'override': {'state': 'not-overridden'}}
        return {'schema': 'hexalith.test-environment.v1', 'policyId': POLICY, 'diagnostics': [], 'bindings': {
            'run': {'runId': self.record['runId'], 'attemptId': self.record['attemptId'], 'attemptSequence': 1,
                    'ownerRunId': self.record['runId'], 'environmentId': self.record['environmentId'], 'role': 'owner'},
            'tool': unknown, 'candidate': unknown, 'decision': unknown, 'reuseLineage': unknown,
            'environment': {'composition': {'id': self.composition_id, 'contentIdentity': 'sha256:' + self.record['planSha256']},
                'mode': 'source', 'executionContext': {'kind': 'local'}, 'catalog': unknown, 'isolation': unknown,
                'resources': resource_values, 'readiness': {'state': 'populated', 'status': readiness, 'observedAt': utc(now),
                    'resources': [{'resourceId': r['resourceId'], 'status': resources[i].get('readiness', readiness), 'observedAt': utc(now), 'diagnosticIds': []} for i, r in enumerate(resource_values)]},
                'startupTasks': unknown}, 'artifacts': {'expected': unknown, 'loaded': unknown}, 'suite': {'declared': unknown, 'completion': unknown}, 'attachments': unknown, 'deadlines': deadlines}}

    def engine(self):
        observed = self.run(['docker', 'info', '--format', '{{.ID}}'], timeout=10).stdout.strip()
        require(observed == self.plan['docker']['engineId'], 'foreign-engine')

    def nodes(self):
        result = self.run(['docker', 'ps', '-aq', '--filter', 'label=io.x-k8s.kind.cluster=' + self.cluster], timeout=10)
        ids = result.stdout.split()
        require(all(re.fullmatch('[0-9a-f]{12,64}', v) for v in ids), 'node-inventory')
        if not ids:
            return []
        objects = strict(self.run(['docker', 'inspect', *ids], timeout=10).stdout)
        return [{'id': o['Id'], 'name': o['Name'], 'created': o['Created'], 'running': o['State']['Running'],
                 'image': o['Config']['Image'], 'status': o['State']['Status'],
                 'startedAt': o['State']['StartedAt'], 'finishedAt': o['State']['FinishedAt'], 'restartCount': o['RestartCount'], 'volumes': [m['Name'] for m in o.get('Mounts', []) if m['Type'] == 'volume']} for o in objects]

    def owned_nodes(self):
        # Inspect all container IDs as well as the kind label: a changed label
        # must not hide a previously recorded owned remnant.
        known = {v['id'] for v in self.record['nodes']}
        if not known:
            return []
        output = self.run(['docker', 'ps', '-a', '--no-trunc', '--format', '{{json .}}'], timeout=10).stdout
        objects = [strict(line) for line in output.splitlines() if line.strip()]
        require(all('ID' in value and 'State' in value for value in objects), 'container-inventory-missing')
        return [{'id': value['ID'], 'running': value['State'] != 'exited'} for value in objects if value['ID'] in known]

    def capture_nodes(self):
        nodes = self.nodes()
        for item in nodes:
            require(item['name'] == '/' + self.cluster + '-control-plane' and item['image'] == self.tools['nodeImage'], 'foreign-node')
        if self.record['nodes']:
            require({i['id'] for i in nodes} <= {i['id'] for i in self.record['nodes']}, 'node-replaced')
        else:
            require(self.record['creationAttempted'], 'node-not-owned')
            require(all(datetime.fromisoformat(v['created'].replace('Z', '+00:00')).timestamp() * 1000 >= self.record['started']['utcMs'] - 1000 for v in nodes), 'node-predates-attempt')
            for node in nodes:
                node['volumes'] = [self.volume_identity(name) for name in node['volumes']]
            self.record['nodes'] = nodes
            self.save()
        return nodes

    def target(self):
        # kubectl config view is local. Exact private transport bytes are frozen
        # after creation and checked before every subsequent cluster operation.
        require(sha(self.kubeconfig) == self.record['kubeconfigSha256'], 'transport-drift')
        value = strict(self.kube('get', 'namespace', 'kube-system', '-o', 'json', timeout=10).stdout)
        require(value['metadata']['uid'] == self.record['physicalTarget']['clusterUid'], 'foreign-cluster')

    def bind_target(self):
        config = strict(self.run(['kubectl', '--kubeconfig', self.kubeconfig, 'config', 'view', '--raw', '-o', 'json'], timeout=10).stdout)
        require(len(config['clusters']) == len(config['contexts']) == 1 and config['contexts'][0]['name'] == self.context, 'transport-scope')
        endpoint = config['clusters'][0]['cluster']['server']
        require(re.fullmatch(r'https://127\.0\.0\.1:\d+', endpoint), 'transport-not-private')
        ca = base64.b64decode(config['clusters'][0]['cluster']['certificate-authority-data'], validate=True)
        uid = strict(self.kube('get', 'namespace', 'kube-system', '-o', 'json', timeout=10).stdout)['metadata']['uid']
        self.record['kubeconfigSha256'] = sha(self.kubeconfig)
        self.record['physicalTarget'] = {'apiServer': endpoint, 'apiCaSha256': hashlib.sha256(ca).hexdigest(), 'clusterUid': uid}
        self.save()

    def observation_namespaces(self):
        return [self.record['target']['namespace'], 'dapr-system', *self.plan['environment'].get('providerNamespaces', [])]

    def create_owned_namespaces(self, objects):
        owned = set(self.observation_namespaces())
        supplied = [o for o in objects if o['kind'] == 'Namespace']
        supplied.append(environment.label({'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': 'dapr-system'}}, self.record['runId']))
        require({o['metadata']['name'] for o in supplied} == owned and len(supplied) == len(owned), 'namespace-install-scope')
        before = strict(self.kube('get', 'namespaces', '-o', 'json', timeout=10).stdout)['items']
        before_ids = {o['metadata']['name']: o['metadata']['uid'] for o in before}
        infrastructure = {'default', 'kube-system', 'kube-public', 'kube-node-lease', 'local-path-storage'}
        infrastructure |= {o['metadata']['name'] for o in documents(capacity.snapshot(self.plan['assets']['network']['path'], self.plan['assets']['network']['sha256']).decode()) if o.get('kind') == 'Namespace'}
        require(not set(before_ids) & owned and set(before_ids) <= infrastructure, 'preexisting-or-foreign-namespace')
        # create, rather than apply, also refuses a namespace created between the
        # read and this command. Partial creation remains retained/unverified.
        self.kube('create', '-f', '-', data=json.dumps({'apiVersion': 'v1', 'kind': 'List', 'items': supplied}), timeout=30)
        after = strict(self.kube('get', 'namespaces', '-o', 'json', timeout=10).stdout)['items']
        identities = {o['metadata']['name']: o['metadata']['uid'] for o in after}
        require(set(identities) == set(before_ids) | owned and all(identities[k] == v for k, v in before_ids.items()), 'namespace-creation-drift')
        labels = {o['metadata']['name']: o['metadata'].get('labels', {}) for o in after}
        require(all(labels[name].get(environment.RUN_LABEL) == self.record['runId'] for name in owned), 'namespace-ownership-label')
        self.record['namespaceUids'] = identities
        self.record['namespaceLabels'] = labels
        self.save()

    def namespace_readback(self):
        expected = self.record.get('namespaceUids')
        require(expected or not self.plan['environment'].get('providerNamespaces'), 'provider-namespace-ownership-missing')
        if not expected:
            return
        observed = strict(self.kube('get', 'namespaces', '-o', 'json', timeout=10).stdout)['items']
        require({o['metadata']['name']: o['metadata']['uid'] for o in observed} == expected, 'foreign-namespace-inventory')
        require({o['metadata']['name']: o['metadata'].get('labels', {}) for o in observed} == self.record['namespaceLabels'], 'namespace-policy-drift')

    def apply_objects(self, objects):
        self.kube('apply', '--server-side', '--field-manager=hexalith-campaign', '-f', '-',
                  data=json.dumps({'apiVersion': 'v1', 'kind': 'List', 'items': objects}), timeout=60)

    def bootstrap_openbao(self):
        ns = self.record['target']['namespace']
        prefix = ['-n', ns, 'exec', '-i', 'campaign-openbao-0', '-c', 'openbao', '--']
        # Initialization is intentionally once. Its private keys are outside
        # descriptor/command evidence; a lost response refuses replay.
        initialized = strict(self.kube(*prefix, 'bao', 'operator', 'init', '-key-shares=3', '-key-threshold=2', '-format=json', timeout=30).stdout)
        require(len(initialized.get('unseal_keys_b64', [])) == 3 and initialized.get('root_token'), 'openbao-init-incomplete')
        atomic(self.evidence / 'openbao-custody.json', initialized, exclusive=True)
        for key in initialized['unseal_keys_b64'][:2]:
            self.kube(*prefix, '/bin/sh', '-ec', 'IFS= read -r key; exec bao operator unseal "$key"', data=key + '\n', timeout=30)
        token = initialized['root_token']
        seed = capacity.retained_json(self.plan['environment']['openbaoSeed'])
        script = 'IFS= read -r BAO_TOKEN; export BAO_TOKEN; exec bao write -format=json "$1" -'
        def write(path, value):
            self.kube(*prefix, '/bin/sh', '-ec', script, 'bootstrap', path, data=token + '\n' + json.dumps(value), timeout=30)
        write('sys/mounts/secret', {'type': 'kv', 'options': {'version': '2'}})
        for name, value in seed['entries'].items():
            require(re.fullmatch('[a-z0-9/-]+', name), 'openbao-path')
            write('secret/data/' + name, {'data': value})
        for name, value in seed['policies'].items():
            require(re.fullmatch('[a-z0-9-]+', name), 'openbao-policy')
            write('sys/policies/acl/' + name, {'policy': value})
        for value in seed['tokens']:
            require(isinstance(value, dict) and value.get('policies') and 'root' not in value['policies'], 'openbao-token-policy')
            write('auth/token/create', value)
        self.kube(*prefix, '/bin/sh', '-ec', 'IFS= read -r BAO_TOKEN; export BAO_TOKEN; exec bao token revoke -self', data=token + '\n', timeout=30)

    def launch(self):
        require(self.record['status'] == 'prepared' and not self.record['creationAttempted'] and self.record['inFlight'] is None, 'launch-already-consumed')
        profile, objects, now = self.admission('launch')
        prepared = self.verify_prepared_inputs(profile, objects)
        application_values = capacity.snapshot(profile['inputs']['values'][0]['path'], profile['inputs']['values'][0]['sha256'])
        self.record['admittedResources'] = self.admitted_resources(objects, prepared['application.yaml'], prepared['dapr.yaml'])
        self.record['phases'] = self.phases(profile); self.save()
        require(self.plan['window']['earliestLaunchUtcMs'] <= now <= self.plan['window']['latestLaunchUtcMs'], 'outside-launch-window')
        self.record['started'] = {'utcMs': now, 'boottimeNs': capacity._boottime(), 'bootId': capacity._boot()}
        self.record['executionDeadlineUtcMs'] = min(now + self.execution_seconds * 1000, self.plan['window']['expiresUtcMs'])
        self.record['status'] = 'starting'; self.save()
        self._command_bound = time.monotonic() + STARTUP_SECONDS
        self.engine()
        require(not self.nodes(), 'preexisting-cluster')
        # Record intent BEFORE kind can partially create a node. This exact
        # immutable attempt is never retried by launch after restart.
        self.record['creationAttempted'] = True; self.save()
        data_path = Path(self.plan['paths']['primary']) / self.record['runId']
        data_path.mkdir(mode=0o700)
        self.record['ownedData'] = {'path': str(data_path), 'device': data_path.stat().st_dev, 'inode': data_path.stat().st_ino}
        self.save()
        kind_config = {'networking': {'disableDefaultCNI': True}, 'kind': 'Cluster', 'apiVersion': 'kind.x-k8s.io/v1alpha4', 'nodes': [{'role': 'control-plane',
            'extraMounts': [{'hostPath': str(data_path), 'containerPath': '/var/local-path-provisioner'}]}]}
        atomic(self.evidence / 'kind.json', kind_config)
        try:
            self.run(['kind', 'create', 'cluster', '--name', self.cluster, '--image', self.tools['nodeImage'],
                      '--config', self.evidence / 'kind.json', '--kubeconfig', self.kubeconfig, '--wait', '0s'], timeout=240)
        finally:
            # Preserve returned partial-create ownership when possible; an
            # abruptly killed runner never guesses unknown IDs on restart.
            uncertain = self.record['inFlight']; self.record['inFlight'] = None
            try:
                self.capture_nodes()
            finally:
                self.record['inFlight'] = uncertain; self.save()
        self.bind_target()
        self.kube('apply', '--server-side', '--field-manager=hexalith-campaign', '-f', self.plan['assets']['network']['path'], timeout=30)
        self.kube('wait', '--for=condition=Ready', 'node', '--all', '--timeout=90s', timeout=95)
        self.create_owned_namespaces(objects)
        self.apply_objects([environment.label(capacity.retained_json(r), self.record['runId']) for r in self.plan['environment']['secrets']])
        self.kube('apply', '--server-side', '--field-manager=hexalith-campaign', '-f', self.plan['assets']['gateway']['path'], timeout=30)
        self.apply_objects([o for o in objects if o['kind'] not in {'Namespace', 'Job'}])
        self.kube('wait', '-n', profile['target']['namespace'], '--for=jsonpath={.status.phase}=Running', 'pod/campaign-openbao-0', '--timeout=90s', timeout=95)
        self.bootstrap_openbao()
        self.helm_run('install', 'dapr', self.plan['assets']['dapr']['path'], '--namespace', 'dapr-system', '--create-namespace',
                      '-f', '-', '--wait', '--timeout=180s', data=prepared['dapr-values.json'], timeout=185)
        self.apply_objects([self.binding(profile, self.plan['profileSha256']),
            {'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'access-telemetry-qualification-gate', 'namespace': self.record['target']['namespace']},
             'data': {'gate.json': json.dumps({'schemaVersion': 1, 'state': 'disabled', 'profileSha256': self.plan['profileSha256'], 'expiresUtcMs': 0})}},
            {'apiVersion': 'coordination.k8s.io/v1', 'kind': 'Lease', 'metadata': {'name': 'access-telemetry-qualification', 'namespace': self.record['target']['namespace']},
             'spec': {'holderIdentity': '', 'leaseDurationSeconds': 0}}])
        self.control_readback(capture=True)
        self.apply_objects([o for o in objects if o['kind'] == 'Job'])
        self.helm_run('install', 'reference', CHART, '-n', self.record['target']['namespace'], '-f', '-',
                      '--server-side=true', '--wait', '--timeout=180s', data=application_values, timeout=185)
        # Existing qualification consumers require a disabled initial gate and
        # no lifecycle/clock writers until their separately authorized operation.
        self.kube('scale', '-n', self.record['target']['namespace'], 'deployment/memories-access-telemetry',
                  'deployment/memories-access-telemetry-clock', '--replicas=0', timeout=15)
        self.settle_startup()
        self.record['status'] = 'ready'; self.save()
        self._command_bound = None
        # Launch remains in bounded supervision; no detached unmonitored ready
        # environment is returned. Monitor reconnects after an actual interruption.
        self.monitor()

    def expected_fault(self, resource_id):
        if not self.record['started'] or not self.record['resources']:
            return False
        elapsed = (capacity._boottime() - self.record['started']['boottimeNs']) / 1e9
        approved = {r['id'] for r in self.record['resources']}
        return resource_id in approved and any(f['resourceId'] == resource_id and f['startSeconds'] <= elapsed < f['endSeconds']
            for f in self.plan['environment']['faultWindows'])

    def verify_prepared_inputs(self, profile, objects):
        expected_names = {'environment.json', 'dapr-values.json', 'application.yaml', 'dapr.yaml'}
        require(set(self.record.get('preparedInputs', {})) == expected_names, 'prepared-inputs-missing')
        held = {name: read_private(self.evidence / name, digest) for name, digest in self.record['preparedInputs'].items()}
        require(strict(held['environment.json']) == {'apiVersion': 'v1', 'kind': 'List', 'items': objects}, 'prepared-environment-drift')
        require(strict(held['dapr-values.json']) == environment.dapr_values(self.plan['environment']['daprImages']), 'prepared-dapr-values-drift')
        require(any(hashlib.sha256(held['application.yaml']).hexdigest() == r['sha256'] for r in profile['inputs']['render']), 'prepared-application-drift')
        environment.validate_dapr_render(documents(held['dapr.yaml'].decode()))
        # Re-render Dapr locally with held values before the first infrastructure
        # command. An edited render cannot define the runtime comparison baseline.
        rendered = self.run([self.helm, 'template', 'dapr', self.plan['assets']['dapr']['path'], '--namespace', 'dapr-system', '-f', '-'],
                            data=held['dapr-values.json'], timeout=30).stdout.encode()
        require(rendered == held['dapr.yaml'], 'prepared-dapr-render-drift')
        return held

    def admitted_resources(self, objects, application, dapr):
        result = {}
        for namespace, values in ((self.record['target']['namespace'], [*objects, *documents(application.decode())]),
                                  ('dapr-system', documents(dapr.decode()))):
            for obj in values:
                if obj['kind'] in {'Deployment', 'StatefulSet', 'Job', 'ConfigMap', 'ServiceAccount', 'Role', 'RoleBinding', 'Service'}:
                    result[resource_id(obj, namespace)] = copy.deepcopy(obj)
        return result

    def control_readback(self, *, capture=False):
        self.target()
        ns = self.record['target']['namespace']
        namespace = strict(self.kube('get', 'namespace', ns, '-o', 'json', timeout=10).stdout)
        prior_uid = self.record['physicalTarget'].get('namespaceUid')
        require(prior_uid in (None, namespace['metadata']['uid']) and (prior_uid is not None or capture), 'foreign-namespace')
        self.record['physicalTarget']['namespaceUid'] = namespace['metadata']['uid']
        gate = strict(self.kube('get', 'configmap', GATE, '-n', ns, '-o', 'json', timeout=10).stdout)
        lease = strict(self.kube('get', 'lease', LEASE, '-n', ns, '-o', 'json', timeout=10).stdout)
        identities = {'namespaceUid': namespace['metadata']['uid'], 'gateUid': gate['metadata']['uid'], 'leaseUid': lease['metadata']['uid']}
        if capture:
            require(not self.record.get('controls'), 'controls-already-captured')
            self.record['controls'] = identities; self.save()
        require(self.record.get('controls') == identities, 'foreign-control-identity')
        value = strict(gate['data']['gate.json'])
        require(set(value) == {'schemaVersion', 'state', 'profileSha256', 'expiresUtcMs'} and value['schemaVersion'] == 1
                and value['profileSha256'] == self.plan['profileSha256'] and value['state'] in {'enabled', 'disabled'}
                and type(value['expiresUtcMs']) is int, 'foreign-control-profile')
        holder = lease.get('spec', {}).get('holderIdentity')
        require(holder == '' or holder in self.plan['environment']['leaseHolders'], 'foreign-qualification-session')
        if capture:
            require(value['state'] == 'disabled' and value['expiresUtcMs'] == 0 and holder == '', 'initial-controls-not-disabled')
        return gate, lease, value

    def fault_rules(self, rid):
        if not self.record['started']:
            return []
        elapsed = (capacity._boottime() - self.record['started']['boottimeNs']) / 1e9
        return [f for f in self.plan['environment']['faultWindows'] if f['resourceId'] == rid and f['startSeconds'] <= elapsed < f['endSeconds']]

    def normalized_live(self, obj, gate):
        """Only existing source-defined replica/restart/report transitions vary."""
        rid = resource_id(obj, self.record['target']['namespace'])
        fields = ('spec',) if obj['kind'] in {'Deployment', 'StatefulSet', 'Job', 'Service'} else ('data',) if obj['kind'] == 'ConfigMap' else ('automountServiceAccountToken',) if obj['kind'] == 'ServiceAccount' else ('rules',) if obj['kind'] == 'Role' else ('subjects', 'roleRef')
        result = {k: copy.deepcopy(obj[k]) for k in fields if k in obj}
        if 'spec' not in result:
            # The evidence payload is governed by Memories' exact C3 assertions.
            # Retain its resource identity but do not inspect/report payload data.
            if obj['metadata']['name'] == 'access-telemetry-physical-evidence-report':
                result = {}
            return result
        spec = result['spec']; name = obj['metadata']['name']
        approved = self.record['admittedResources'][rid]['spec']
        if obj['kind'] in {'Deployment', 'StatefulSet'}:
            normalize_changes(spec, approved, [change for rule in self.fault_rules(rid) for change in rule.get('changes', [])])
            expected = approved.get('replicas', 1)
            desired = 0 if name in CONTROLLED_WRITERS and gate['state'] == 'disabled' else expected
            require(spec.get('replicas', 1) == desired or any(spec.get('replicas', 1) in r['replicas'] for r in self.fault_rules(rid)), 'workload-replica-drift')
            spec['replicas'] = expected
            annotations = spec.get('template', {}).get('metadata', {}).get('annotations', {})
            restarted = annotations.get('kubectl.kubernetes.io/restartedAt')
            if restarted is not None:
                try:
                    when = int(datetime.fromisoformat(restarted.replace('Z', '+00:00')).timestamp() * 1000)
                except (ValueError, TypeError):
                    raise ValueError('campaign-lifecycle-rollout-marker-invalid') from None
                require(any(r.get('allowRolloutRestart') and r['resourceId'] == rid
                    and self.record['started']['utcMs'] + r['startSeconds'] * 1000 <= when <= self.record['started']['utcMs'] + r['endSeconds'] * 1000
                    for r in self.plan['environment']['faultWindows']), 'rollout-outside-source-window')
                del annotations['kubectl.kubernetes.io/restartedAt']
                if not annotations:
                    spec['template']['metadata'].pop('annotations', None)
        if obj['kind'] == 'Job':
            require(name == REPORTER, 'undeclared-job')
            elapsed = (capacity._boottime() - self.record['started']['boottimeNs']) / 1e9
            phase = self.record['phases']['c3']
            require(spec.get('suspend') is True or (spec.get('suspend') is False and
                (phase[0] <= elapsed < phase[1] or obj.get('status', {}).get('succeeded') == 1)), 'reporter-outside-source-window')
            spec['suspend'] = True
        return result

    def observe(self, startup=False):
        gate, lease, gate_value = self.control_readback()
        self.namespace_readback()
        ns = self.record['target']['namespace']
        require(self.record.get('admittedResources'), 'admitted-workloads-missing')
        values = []
        for namespace in self.observation_namespaces():
            listed = strict(self.kube('get', 'deployments,statefulsets,replicasets,jobs,pods,persistentvolumeclaims,configmaps,serviceaccounts,roles,rolebindings,services', '-n', namespace, '-o', 'json', timeout=15).stdout)
            for obj in listed['items']:
                obj['metadata'].setdefault('namespace', namespace)
                values.append(obj)
        inventory = {resource_id(o, ns): o for o in values}
        admitted = self.record['admittedResources']
        prior = {v['id']: v['uid'] for v in self.record['resources'] if not v['id'].startswith('Pod/')}
        observed = []; baselines = self.record.setdefault('liveBaselines', {})
        # All fixed resources retain their original UID. New Jobs/controllers are
        # refused; the existing fixed reporter changes state, never identity.
        require(set(admitted) <= set(inventory), 'readiness-incomplete')
        for rid, obj in inventory.items():
            if obj['kind'] in {'Deployment', 'StatefulSet', 'Job'}:
                require(rid in admitted, 'undeclared-controller-or-job')
            if rid not in admitted:
                continue
            meta, spec, status = obj['metadata'], obj.get('spec', {}), obj.get('status', {})
            require(rid not in prior or prior[rid] == meta['uid'], 'owned-resource-drift')
            normalized = self.normalized_live(obj, gate_value)
            expected = self.normalized_live(admitted[rid], {'state': 'enabled'})
            # The admitted Job has no status and remains suspended; template and
            # security/configuration fields must match before recording defaults.
            require(contains(normalized, expected), 'admitted-workload-spec-drift')
            if rid in baselines:
                require(normalized == baselines[rid], 'running-workload-spec-drift')
            else:
                baselines[rid] = normalized
            ready = True
            if obj['kind'] in {'Deployment', 'StatefulSet'}:
                ready = status.get('observedGeneration', 0) >= meta.get('generation', 1) and status.get('readyReplicas', 0) == spec.get('replicas', 1)
                expected_replicas = 0 if obj['metadata']['name'] in CONTROLLED_WRITERS and gate_value['state'] == 'disabled' else admitted[rid]['spec'].get('replicas', 1)
                if self.fault_rules(rid) and (spec.get('replicas', 1) != expected_replicas
                        or any(change_matches(spec, change) for rule in self.fault_rules(rid) for change in rule.get('changes', []))):
                    ready = False
            elif obj['kind'] == 'Job':
                require(status.get('failed', 0) == 0, 'reporter-job-failed')
                ready = spec.get('suspend') is True or status.get('succeeded', 0) == 1
                if not ready:
                    anchor = self.record.setdefault('reporterStarted', self.stamp())
                    require(anchor['bootId'] == capacity._boot() and 0 <= capacity._boottime() - anchor['boottimeNs'] <= 300_000_000_000, 'reporter-settling-timeout')
                    require(self.record['phases']['c3'][0] <= (capacity._boottime() - self.record['started']['boottimeNs']) / 1e9 < self.record['phases']['c3'][1], 'reporter-settling-outside-window')
            require(ready or startup or self.expected_fault(rid) or obj['kind'] == 'Job', 'resource-not-ready')
            observed.append({'id': rid, 'uid': meta['uid'], 'owner': 'Hexalith.Platform', 'readiness': 'ready' if ready else 'pending'})
        # Resolve exact controller UID lineage, including Deployment ReplicaSets.
        def owner_of(obj):
            refs = obj['metadata'].get('ownerReferences', [])
            owners = [inventory.get(r['kind'] + '/' + obj['metadata']['namespace'] + '/' + r['name']) for r in refs if r.get('controller') is True]
            require(len(owners) == 1 and owners[0] is not None and owners[0]['metadata']['uid'] == next(r['uid'] for r in refs if r.get('controller') is True), 'foreign-pod-lineage')
            owner = owners[0]
            if owner['kind'] == 'ReplicaSet':
                owner = owner_of(owner)
            require(resource_id(owner, ns) in admitted, 'foreign-pod-controller')
            return owner
        for obj in values:
            rid = resource_id(obj, ns)
            if obj['kind'] == 'PersistentVolumeClaim':
                require(not prior or prior.get(rid) == obj['metadata']['uid'], 'foreign-storage-claim')
                ready = obj.get('status', {}).get('phase') == 'Bound'
                require(ready or startup, 'storage-not-bound')
            elif obj['kind'] == 'Pod':
                owner = owner_of(obj); owner_id = resource_id(owner, ns)
                status = obj.get('status', {})
                completed = owner['kind'] == 'Job' and owner.get('status', {}).get('succeeded') == 1 and status.get('phase') == 'Succeeded'
                ready = completed or (not obj['metadata'].get('deletionTimestamp') and status.get('phase') == 'Running' and
                    any(c.get('type') == 'Ready' and c.get('status') == 'True' for c in status.get('conditions', [])))
                expected_containers = {c['name']: c for c in owner['spec']['template']['spec']['containers'] + owner['spec']['template']['spec'].get('initContainers', [])}
                expected_images = {name: c['image'] for name, c in expected_containers.items()}
                live_containers = obj['spec'].get('containers', []) + obj['spec'].get('initContainers', [])
                for container in live_containers:
                    expected_image = expected_images.get(container['name'], self.plan['environment']['daprImages']['daprd'] if container['name'] == 'daprd' else None)
                    require(expected_image == container.get('image'), 'pod-image-drift')
                    if container['name'] in expected_containers:
                        original_container = next((c for c in admitted[owner_id]['spec']['template']['spec']['containers'] if c['name'] == container['name']), None)
                        allowed_resources = [expected_containers[container['name']].get('resources', {})]
                        if self.fault_rules(owner_id) and original_container is not None:
                            allowed_resources.append(original_container.get('resources', {}))
                        require(container.get('resources', {}) in allowed_resources, 'pod-resource-drift')
                allowed_pending = startup or self.expected_fault(owner_id) or owner['kind'] == 'Job'
                if owner['metadata']['name'] in CONTROLLED_WRITERS and gate_value['state'] == 'disabled' and obj['metadata'].get('deletionTimestamp'):
                    allowed_pending = True
                require(ready or allowed_pending, 'pod-not-ready')
            else:
                continue
            observed.append({'id': rid, 'uid': obj['metadata']['uid'], 'owner': 'Hexalith.Platform', 'readiness': 'ready' if ready else 'pending'})
        require(not prior or set(prior) <= {v['id'] for v in observed}, 'owned-resource-missing')
        identity = strict(self.kube('get', 'configmap', 'access-telemetry-campaign-identity', '-n', ns, '-o', 'json', timeout=10).stdout)
        require(identity['data'] == self.record['runtimeBinding']['data'], 'profile-readback-drift')
        # Capture stable UIDs on first observation, even while startup settles.
        if not self.record['resources']:
            self.record['resources'] = observed
        self.save()
        if not startup:
            self.check_storage(pg_unavailable=self.expected_fault('StatefulSet/' + ns + '/access-telemetry-postgresql'))
        readiness = 'ready' if all(v['readiness'] == 'ready' for v in observed) else 'pending'
        if not startup or readiness == 'ready':
            self.publish_observation(readiness, observed)
        return readiness

    def settle_startup(self):
        while self.observe(startup=True) != 'ready':
            require(self._command_bound is not None and time.monotonic() < self._command_bound, 'startup-settling-timeout')
            time.sleep(min(0.2, max(0, self._command_bound - time.monotonic())))

    def publish_observation(self, readiness, observed):
        root = self.evidence / 'observations'; root.mkdir(mode=0o700, exist_ok=True)
        now = int(time.time() * 1000)
        # A held snapshot remains usable throughout its complete 60s freshness
        # interval. Excess rapid publication refuses instead of removing it.
        retained = []
        latest = self.evidence / 'latest-observation.json'
        current = Path(strict(read_private(latest))['path']).parent if latest.exists() else None
        for path in root.iterdir():
            if path.is_dir() and re.fullmatch('[0-9]{12}', path.name):
                if path != current and now - strict(read_private(path / 'pair.json'))['observedUtcMs'] > 120000:
                    shutil.rmtree(path)
                else:
                    retained.append(path)
        require(len(retained) < 64, 'observation-retention-bound')
        sequence = max([self.record.get('observationSequence', 0), *[int(path.name) for path in retained]]) + 1
        final = root / f'{sequence:012d}'; staging = root / ('.pending-' + uuid.uuid4().hex)
        staging.mkdir(mode=0o700)
        try:
            atomic(staging / 'test-environment.json', self.descriptor(readiness, observed), exclusive=True)
            self.validate_descriptor(staging / 'test-environment.json')
            readback = {'schema': 'hexalith.platform.campaign-readback/v1', 'runId': self.record['runId'],
                'planSha256': self.record['planSha256'], 'profileSha256': self.plan['profileSha256'],
                'capacityProfileSha256': self.record['runtimeBinding']['data']['capacityProfileSha256'],
                'descriptorSha256': sha(staging / 'test-environment.json'), 'target': self.record['physicalTarget'],
                'observedUtcMs': now, 'qualified': False}
            atomic(staging / 'campaign-readback.json', readback, exclusive=True)
            pair = {'schema': 'hexalith.platform.campaign-observation/v1', 'runId': self.record['runId'], 'observedUtcMs': now,
                'descriptor': {'path': str(final / 'test-environment.json'), 'sha256': sha(staging / 'test-environment.json')},
                'readback': {'path': str(final / 'campaign-readback.json'), 'sha256': sha(staging / 'campaign-readback.json')}}
            atomic(staging / 'pair.json', pair, exclusive=True)
            os.rename(staging, final)
            fd = os.open(root, os.O_DIRECTORY)
            try:
                os.fsync(fd)
            finally:
                os.close(fd)
            atomic(self.evidence / 'latest-observation.json', {'path': str(final / 'pair.json'), 'sha256': sha(final / 'pair.json')})
            self.record.update(observationSequence=sequence, latestObservation=pair, latestPair={'path': str(final / 'pair.json'), 'sha256': sha(final / 'pair.json')}, lastObservation=self.stamp())
            self.save()
            return pair
        finally:
            if staging.exists():
                shutil.rmtree(staging)

    def expected_metric_counts(self, value):
        counts = {}; history = copy.deepcopy(self.record.get('faultMetricTotals', {})); seen = set()
        owned = {v['id']: v['uid'] for v in self.record['resources']}
        for attribution in value['expectedFaults']:
            require(set(attribution) == {'faultSha256', 'resourceUid', 'series'}, 'expected-fault-attribution-shape')
            digest = attribution['faultSha256']
            require(digest not in seen, 'duplicate-fault-attribution'); seen.add(digest)
            rule = next((f for f in self.plan['environment']['faultWindows'] if capacity.digest(f) == digest), None)
            require(rule is not None and rule in self.fault_rules(rule['resourceId'])
                    and owned.get(rule['resourceId']) == attribution['resourceUid'], 'expected-fault-attribution-binding')
            require(self.record['started']['utcMs'] + rule['startSeconds'] * 1000 <= value['intervalStartUtcMs']
                    <= value['observedUtcMs'] < self.record['started']['utcMs'] + rule['endSeconds'] * 1000, 'expected-metrics-outside-window')
            allowances = rule['metricAllowances']; measured = attribution['series']
            require(set(measured) == set(allowances) and all(type(v) is int and 0 <= v <= allowances[k] for k, v in measured.items()), 'expected-metric-count-invalid')
            previous = history.get(digest)
            if previous and previous['observedUtcMs'] == value['observedUtcMs']:
                require(previous['lastSeries'] == measured, 'expected-metric-observation-changed')
            else:
                require(previous is None or value['intervalStartUtcMs'] >= previous['observedUtcMs'], 'expected-metric-interval-overlap')
                totals = {k: v + (previous['totals'].get(k, 0) if previous else 0) for k, v in measured.items()}
                require(all(totals[k] <= allowances[k] for k in totals), 'expected-metric-budget-exhausted')
                history[digest] = {'observedUtcMs': value['observedUtcMs'], 'lastSeries': measured, 'totals': totals}
            for name, number in measured.items():
                counts[name] = counts.get(name, 0) + number
        return counts, history

    def check_storage(self, *, pg_unavailable=False):
        value = monitor_receipt(self.plan, self.record['planSha256'], clock(self.plan))
        require(value['phase'] == 'observed' and value['target'] == self.record['physicalTarget'], 'collector-target-binding')
        for name, amount in self.record['sizing']['allocations'].items():
            measured = value['allocations'][name]
            used, free = measured['usedBytes'], measured['availableBytes']
            require(type(used) is int and type(free) is int and used >= 0 and free >= 0, 'collector-allocation-no-data')
            require(used + free <= amount and used <= amount * 4 // 5 and free >= amount // 5, 'owned-allocation-bound')
        expected_counts, fault_history = self.expected_metric_counts(value)
        series = value['series']; limits = self.plan['environment']['monitoring']['limits']; bounds = self.record['sizing']
        for key, number in series.items():
            if pg_unavailable and key.startswith('postgres.') and number is None:
                continue
            require(type(number) is int and number >= 0, 'collector-series-no-data')
            metric = key.split('.', 1)[1]
            if metric in limits:
                require(expected_counts.get(key, 0) <= number <= limits[metric] + expected_counts.get(key, 0), 'operational-monitor-limit')
        require(series['postgres.tableCount'] is None or series['postgres.tableCount'] == 1, 'state-table-missing')
        physical_limits = {'relationBytes': bounds['relationWithMarginBytes'], 'walBytes': capacity.BOUNDS['walPeakBytes'],
            'liveRecords': bounds['cumulativeRecords'], 'deadRecords': bounds['cumulativeRecords'],
            'supportBytes': bounds['cumulativeRecords'] * capacity.BOUNDS['indexAndStateBytesPerRecord']}
        for metric, limit in physical_limits.items():
            require(series['postgres.' + metric] is None or series['postgres.' + metric] <= limit, 'physical-storage-bound')
        self.record['faultMetricTotals'] = fault_history
        self.record['storageObservation'] = {'receiptSha256': value['_receiptSha256'],
            'observedUtcMs': value['observedUtcMs'], 'unavailableSeries': [k for k, v in series.items() if v is None],
            'qualified': False, 'acceptedCountsAndReclamation': 'Memories-assertions-not-evaluated'}

    def monitor(self, *, maximum_polls=None):
        require(self.record['status'] == 'ready' and self.record['terminal'] is None and self.record['inFlight'] is None, 'monitor-state')
        polls = 0
        while maximum_polls is None or polls < maximum_polls:
            if (self.evidence / 'stop-request.json').exists():
                raise InterruptedError('campaign-lifecycle-stop-requested')
            self.admission('monitor')
            require(self.remaining() > 0, 'original-budget-exhausted')
            self.observe()
            polls += 1
            time.sleep(min(POLL_SECONDS, self.remaining()))

    def failure(self, error):
        first = self.record['terminal'] is None
        stage = self.record['status']
        self.terminal('cancelled' if isinstance(error, (KeyboardInterrupt, InterruptedError)) else 'failed', cause_code(error))
        if first:
            command = self.record['commands'][-1] if self.record['commands'] else None
            self.record['failure'] = {'stage': stage, 'causeCode': cause_code(error),
                'command': None if command is None else {k: command.get(k) for k in ('outcome', 'returncode', 'timedOut', 'outputLimitExceeded', 'timeoutSeconds', 'capturedBytes', 'processGroupTerminated')},
                'uncertainCommandSha256': (self.record.get('inFlight') or {}).get('commandSha256')}
            self.save()
            self.capture_failure()

    def capture_failure(self):
        if self.record.get('failureCapture'):
            return
        previous_cleanup, previous_bound = self._cleanup_mode, self._command_bound
        self._cleanup_mode = True; self._command_bound = time.monotonic() + 30
        self.record['failureCapture'] = {'state': 'unavailable', 'causeCode': 'target-not-established'}
        self.save()
        try:
            require(self.record.get('physicalTarget') and self.record['physicalTarget'].get('namespaceUid'), 'diagnostic-target-unavailable')
            self.target()
            ns = self.record['target']['namespace']
            current = strict(self.kube('get', 'namespace', ns, '-o', 'json', timeout=5).stdout)
            require(current['metadata']['uid'] == self.record['physicalTarget']['namespaceUid'], 'foreign-namespace')
            facts = []; events = []
            reasons = {'BackOff', 'Failed', 'Unhealthy', 'FailedScheduling', 'FailedMount', 'FailedAttachVolume', 'Evicted',
                       'Killing', 'SuccessfulCreate', 'SuccessfulDelete', 'ScalingReplicaSet', 'Pulled', 'Created', 'Started',
                       'OOMKilled', 'Error', 'Completed', 'ContainerCannotRun', 'CrashLoopBackOff', 'ImagePullBackOff', 'ErrImagePull'}
            for namespace in self.observation_namespaces():
                value = strict(self.kube('get', 'deployments,statefulsets,jobs,pods,events', '-n', namespace, '-o', 'json',
                                         timeout=5, maximum_output_bytes=512 * 1024).stdout)
                require(len(value['items']) <= 256, 'diagnostic-inventory-bound')
                for obj in value['items']:
                    status = obj.get('status', {})
                    if obj['kind'] == 'Event':
                        events.append({'type': obj.get('type') if obj.get('type') in {'Normal', 'Warning'} else 'unknown',
                            'reason': obj.get('reason') if obj.get('reason') in reasons else 'other',
                            'count': obj.get('count') if type(obj.get('count')) is int else None})
                        continue
                    item = {'kind': obj['kind'], 'uidSha256': hashlib.sha256(obj['metadata']['uid'].encode()).hexdigest(),
                        'phase': status.get('phase') if status.get('phase') in {'Pending', 'Running', 'Succeeded', 'Failed', 'Unknown'} else None,
                        'readyReplicas': status.get('readyReplicas'), 'succeeded': status.get('succeeded'), 'failed': status.get('failed'),
                        'terminations': []}
                    for c in status.get('containerStatuses', []) + status.get('initContainerStatuses', []):
                        term = c.get('state', {}).get('terminated') or c.get('lastState', {}).get('terminated')
                        if term:
                            item['terminations'].append({'exitCode': term.get('exitCode') if type(term.get('exitCode')) is int else None,
                                'signal': term.get('signal') if type(term.get('signal')) is int else None,
                                'reason': term.get('reason') if term.get('reason') in reasons else 'other'})
                    facts.append(item)
            snapshot = {'schema': 'hexalith.platform.campaign-failure/v1', 'runId': self.record['runId'],
                        'observedUtcMs': int(time.time() * 1000), 'resources': facts, 'events': events}
            atomic(self.evidence / 'failure-status.json', snapshot, exclusive=True)
            self.record['failureCapture'] = {'state': 'captured', 'path': str(self.evidence / 'failure-status.json'), 'sha256': sha(self.evidence / 'failure-status.json')}
        except BaseException as error:
            self.record['failureCapture'] = {'state': 'unavailable', 'causeCode': cause_code(error)}
        finally:
            self._cleanup_mode, self._command_bound = previous_cleanup, previous_bound
            self.save()

    def quiesce(self):
        # No source/capacity/authority input is reopened. Exact original private
        # target/control identities and holder allowlist authorize only shutdown.
        if not self.record.get('controls'):
            return {'state': 'unavailable', 'causeCode': 'controls-not-established'}
        try:
            gate, lease, value = self.control_readback()
            self.namespace_readback()
        except (OSError, RuntimeError) as error:
            return {'state': 'unavailable', 'causeCode': cause_code(error)}
        ns = self.record['target']['namespace']
        def patch(kind, obj, changes):
            metadata = obj['metadata']
            operations = [{'op': 'test', 'path': '/metadata/uid', 'value': metadata['uid']},
                          {'op': 'test', 'path': '/metadata/resourceVersion', 'value': metadata['resourceVersion']}, *changes]
            try:
                self.kube('patch', kind, metadata['name'], '-n', ns, '--type=json', '--patch', json.dumps(operations), timeout=10)
            except (OSError, RuntimeError):
                raise ValueError('campaign-lifecycle-quiescence-mutation-not-verified') from None
        disabled = {'schemaVersion': 1, 'state': 'disabled', 'profileSha256': self.plan['profileSha256'], 'expiresUtcMs': 0}
        patch('configmap', gate, [{'op': 'replace', 'path': '/data/gate.json', 'value': json.dumps(disabled)}])
        # The gate is disabled before any workload is scaled. Every scale tests
        # the recorded controller UID and current resourceVersion atomically.
        owned = {v['id']: v['uid'] for v in self.record['resources']}
        workloads = [rid for rid in self.record.get('admittedResources', {}) if rid.startswith('Deployment/' + ns + '/')]
        require({f'Deployment/{ns}/{name}' for name in CONTROLLED_WRITERS} <= set(workloads), 'quiescence-workload-identities-missing')
        for rid in workloads:
            obj = strict(self.kube('get', 'deployment', rid.split('/')[-1], '-n', ns, '-o', 'json', timeout=10).stdout)
            require(owned.get(rid) == obj['metadata']['uid'], 'foreign-workload-control')
            patch('deployment', obj, [{'op': 'replace', 'path': '/spec/replicas', 'value': 0}])
        patch('lease', lease, [{'op': 'test', 'path': '/spec/holderIdentity', 'value': lease['spec']['holderIdentity']},
              {'op': 'replace', 'path': '/spec/holderIdentity', 'value': ''}, {'op': 'replace', 'path': '/spec/leaseDurationSeconds', 'value': 0}])
        while True:
            gate, lease, value = self.control_readback()
            require(value == disabled and lease['spec']['holderIdentity'] == '' and lease['spec']['leaseDurationSeconds'] == 0, 'quiescence-controls-not-disabled')
            listing = strict(self.kube('get', 'deployments,replicasets,pods', '-n', ns, '-o', 'json', timeout=10).stdout)['items']
            current = {resource_id(o, ns): o for o in listing}
            settled = True
            for rid in workloads:
                obj = current.get(rid)
                require(obj and obj['metadata']['uid'] == owned[rid] and obj['spec']['replicas'] == 0, 'foreign-workload-control')
                status = obj.get('status', {})
                settled &= status.get('observedGeneration', 0) >= obj['metadata'].get('generation', 1) and status.get('replicas', 0) == 0 and status.get('readyReplicas', 0) == 0
            replica_uids = {o['metadata']['uid'] for o in listing if o['kind'] == 'ReplicaSet' and any(r.get('uid') in {owned[rid] for rid in workloads} for r in o['metadata'].get('ownerReferences', []))}
            settled &= not any(o['kind'] == 'Pod' and any(r.get('uid') in replica_uids for r in o['metadata'].get('ownerReferences', [])) for o in listing)
            if settled:
                return {'state': 'verified', 'namespaceUid': self.record['controls']['namespaceUid'], 'gateDisabled': True,
                        'leaseCleared': True, 'workloadsScaledToZero': sorted(workloads), 'ownedWriterPodsAbsent': True}
            require(self.remaining(True) > 0, 'quiescence-deadline-exhausted')
            time.sleep(min(0.2, self.remaining(True)))

    def volume_identity(self, name):
        values = strict(self.run(['docker', 'volume', 'inspect', name], timeout=10).stdout)
        require(len(values) == 1 and values[0].get('Name') == name, 'volume-identity-unavailable')
        value = values[0]
        require(value.get('CreatedAt') and value.get('Mountpoint') and value.get('Driver'), 'volume-creation-identity-unavailable')
        return {k: value.get(k) for k in ('Name', 'CreatedAt', 'Driver', 'Mountpoint', 'Scope', 'Options', 'Labels')}

    def supervise(self, action):
        try:
            getattr(self, action)()
        except BaseException as error:
            self.failure(error)
            request = self.evidence / 'stop-request.json'
            self.stop(remove=request.exists() and strict(read_private(request))['action'] == 'remove')
            raise

    def terminal(self, outcome, cause):
        if self.record['terminal'] is None:
            self.record['terminal'] = {'outcome': outcome, 'cause': cause, 'recordedAt': utc(int(time.time() * 1000))}
        if self.record['stopping'] is None:
            request = self.evidence / 'stop-request.json'
            self.record['stopping'] = strict(read_private(request))['requested'] if request.exists() else self.stamp()
            self.record['stopDeadlineUtcMs'] = min(self.record['stopping']['utcMs'] + STOPPING_SECONDS * 1000,
                self.plan['window']['expiresUtcMs'] + STOPPING_SECONDS * 1000)
        self.record['status'] = 'stopping'; self.save()

    def stopped_node_identity(self, nodes):
        originals = {v['id']: v for v in self.record['nodes']}
        require(originals and {v['id'] for v in nodes} == set(originals), 'stopped-node-set-differs')
        fields = ('id', 'name', 'created', 'image', 'running', 'status', 'startedAt', 'finishedAt', 'restartCount')
        result = []
        for node in nodes:
            require(all(k in node for k in fields) and node['running'] is False and node['status'] == 'exited'
                    and type(node['restartCount']) is int and node['restartCount'] >= 0, 'stopped-node-lifecycle-unavailable')
            require(all(node[k] == originals[node['id']][k] for k in ('name', 'created', 'image')), 'stopped-node-identity-differs')
            try:
                started = datetime.fromisoformat(node['startedAt'].replace('Z', '+00:00'))
                finished = datetime.fromisoformat(node['finishedAt'].replace('Z', '+00:00'))
                require(started.year > 1 and started <= finished, 'stopped-node-times-invalid')
            except (ValueError, TypeError, AttributeError):
                raise ValueError('campaign-lifecycle-stopped-node-times-invalid') from None
            result.append({k: node[k] for k in fields})
        return sorted(result, key=lambda value: value['id'])

    def stopped_quiescence_binding(self):
        return copy.deepcopy({k: self.record.get(k) for k in ('runId', 'attemptId', 'planSha256', 'physicalTarget', 'controls',
                                               'kubeconfigSha256', 'namespaceUids', 'namespaceLabels', 'stopping', 'stopDeadlineUtcMs')})

    def reuse_stopped_quiescence(self, nodes):
        proof = self.record['stoppedProof']
        try:
            require(proof.get('schema') == 'hexalith.platform.campaign-stopped/v1'
                    and proof.get('binding') == self.stopped_quiescence_binding()
                    and proof.get('engineId') == self.plan['docker']['engineId']
                    and proof.get('quiescence', {}).get('state') == 'verified', 'stopped-quiescence-binding-differs')
            # The API is intentionally unavailable after stop. Its original
            # transport/target/control bindings remain exact, and Docker must
            # prove that no node has run again since verified quiescence.
            require(self.record.get('kubeconfigSha256') and sha(self.kubeconfig) == self.record['kubeconfigSha256'], 'transport-drift')
            require(self.stopped_node_identity(nodes) == proof['nodes'], 'stopped-node-lifecycle-changed')
            return copy.deepcopy(proof['quiescence'])
        except (ValueError, OSError, KeyError, TypeError) as error:
            return {'state': 'unavailable', 'causeCode': cause_code(error), 'priorVerifiedQuiescenceNotReused': True}

    def stop(self, *, remove=False):
        self.terminal('cancelled', 'run-cancelled')
        self._cleanup_mode = True; self._command_bound = None
        try:
            require(self.remaining(True) > 0, 'original-stopping-budget-exhausted')
            self.engine()
            nodes = self.nodes()
            known = {v['id'] for v in self.record['nodes']}
            require({v['id'] for v in nodes} <= known, 'foreign-or-unrecorded-node-retained')
            quiescence = self.reuse_stopped_quiescence(nodes) if self.record.get('stoppedProof') else self.quiesce()
            self.record['quiescence'] = quiescence; self.save()
            if quiescence['state'] != 'verified':
                # Retain node/volume custody after abrupt or partial loss; only
                # bounded physical containment is allowed without quiescence.
                if nodes:
                    self.run(['docker', 'stop', '--time', '30', *sorted(known)], timeout=40)
                self.record['status'] = 'stopped-unverified'
                self.record['cleanup'] = {'disposition': 'retained-unverified', 'unverified': True, 'remainingNodeIds': sorted(known), 'quiescence': quiescence}
                self.save()
                return
            if nodes and remove:
                # kind deletion is allowed only after exact original Docker IDs
                # match. Unknown/replaced nodes are retained and never deleted.
                self.run(['kind', 'delete', 'cluster', '--name', self.cluster, '--kubeconfig', self.kubeconfig], timeout=120)
            elif nodes:
                self.run(['docker', 'stop', '--time', '30', *sorted(known)], timeout=40)
            stopped_nodes = self.nodes()
            remaining = list({v['id']: v for v in [*stopped_nodes, *self.owned_nodes()]}.values())
            success = not remaining if remove else all(not node['running'] for node in remaining)
            self.record['cleanup'] = {'disposition': 'complete' if success and remove else 'stopped' if success else 'incomplete',
                                       'remainingNodeIds': [v['id'] for v in remaining], 'unverified': not success,
                                       'persistentData': 'retained-in-original-primary-and-backup-custody'}
            require(success, 'owned-remnants')
            if remove:
                volumes = {v['Name']: v for node in self.record['nodes'] for v in node.get('volumes', [])}
                for volume, original in sorted(volumes.items()):
                    require(not self.run(['docker', 'ps', '-aq', '--filter', 'volume=' + volume], timeout=10).stdout.strip(), 'foreign-volume-attachment')
                    listed = [strict(line) for line in self.run(['docker', 'volume', 'ls', '--format', '{{json .}}'], timeout=10).stdout.splitlines() if line.strip()]
                    if any(v['Name'] == volume for v in listed):
                        require(self.volume_identity(volume) == original, 'foreign-recreated-volume-retained')
                        self.run(['docker', 'volume', 'rm', volume], timeout=15)
                        verified = [strict(line) for line in self.run(['docker', 'volume', 'ls', '--format', '{{json .}}'], timeout=10).stdout.splitlines() if line.strip()]
                        require(all(v['Name'] != volume for v in verified), 'owned-volume-remains')
                if self.record.get('ownedData'):
                    data = self.record['ownedData']; path = Path(data['path'])
                    if path.exists():
                        require(path.resolve() == path and path.stat().st_dev == data['device'] and path.stat().st_ino == data['inode'], 'owned-storage-replaced')
                        shutil.rmtree(path)
                    require(not path.exists(), 'owned-storage-remains')
                self.record['cleanup']['persistentData'] = 'owned-primary-removed; independent-backup-retained'
                self.kubeconfig.unlink(missing_ok=True)
                self.record['status'] = 'removed'
            else:
                stopped_identity = self.stopped_node_identity(stopped_nodes)
                before_stop = {node['id']: node for node in nodes}
                require(all(node['startedAt'] == before_stop[node['id']].get('startedAt')
                            and node['restartCount'] == before_stop[node['id']].get('restartCount') for node in stopped_identity),
                        'node-restarted-during-quiescence')
                self.record['stoppedProof'] = {'schema': 'hexalith.platform.campaign-stopped/v1',
                    'binding': self.stopped_quiescence_binding(), 'engineId': self.plan['docker']['engineId'],
                    'quiescence': copy.deepcopy(quiescence), 'nodes': stopped_identity}
                self.record['status'] = 'stopped'
            self.record['inFlight'] = None; self.save()
        except BaseException:
            self.record['cleanup']['disposition'] = 'incomplete'; self.record['cleanup']['unverified'] = True
            self.save()
            raise


def prepare(path, expected, evidence):
    plan = load_plan(path, expected)
    profile, objects, now = admit(plan, expected, 'prepare')
    return prepare_admitted(plan, profile, objects, now, expected, evidence, raw=capacity.snapshot(path, expected))


def prepare_admitted(plan, profile, objects, now, expected, evidence, runner=CampaignQualification, raw=None):
    """Persist and render an admitted plan without infrastructure mutation."""
    evidence = Path(evidence)
    require(evidence.is_absolute() and evidence.resolve() == evidence and not evidence.exists(), 'fresh-state-required')
    private(evidence.parent, directory=True)
    evidence.mkdir(mode=0o700)
    atomic(evidence / runner.plan_name, raw if raw is not None else capacity.canonical(plan), exclusive=True)
    record = {'schema': runner.schema, 'runId': plan['runId'], 'attemptId': uuid.uuid4().hex, 'environmentId': uuid.uuid4().hex,
        'planSha256': expected, 'target': profile['target'], 'status': 'prepared', 'preparedUtcMs': now,
        'started': None, 'stopping': None, 'executionDeadlineUtcMs': None, 'stopDeadlineUtcMs': None,
        'creationAttempted': False, 'nodes': [], 'resources': [], 'physicalTarget': None, 'kubeconfigSha256': None,
        'terminal': None, 'inFlight': None, 'commandCount': 0, 'commands': [], 'lastObservation': None,
        'runtimeBinding': runner.binding(profile, plan['profileSha256']), 'sizing': profile['sizing'], 'ownedData': None,
        'cleanup': {'disposition': 'not-requested', 'remainingNodeIds': [], 'unverified': True}}
    atomic(evidence / runner.state_name, record, exclusive=True)
    atomic(evidence / 'environment.json', {'apiVersion': 'v1', 'kind': 'List', 'items': objects})
    atomic(evidence / 'dapr-values.json', environment.dapr_values(plan['environment']['daprImages']))
    q = runner(evidence)
    try:
        atomic(evidence / 'test-environment.json', q.descriptor())
        q.validate_descriptor()
        # Render the actual chart with the verified Helm binary, offline only.
        rendered = q.run([q.helm, 'template', 'reference', CHART, '--namespace', q.record['target']['namespace'], '-f', profile['inputs']['values'][0]['path']], timeout=30).stdout.encode()
        require(any(hashlib.sha256(rendered).hexdigest() == r['sha256'] for r in profile['inputs']['render']), 'render-drift')
        atomic(evidence / 'application.yaml', rendered)
        dapr = q.run([q.helm, 'template', 'dapr', plan['assets']['dapr']['path'], '--namespace', 'dapr-system', '-f', evidence / 'dapr-values.json'], timeout=30).stdout
        environment.validate_dapr_render(documents(dapr))
        atomic(evidence / 'dapr.yaml', dapr.encode())
        q.record['preparedInputs'] = {name: sha(evidence / name) for name in ('environment.json', 'dapr-values.json', 'application.yaml', 'dapr.yaml')}
        q.save()
        return q.record
    except BaseException:
        q.record['status'] = 'preparation-failed'; q.save()
        raise
    finally:
        q.close()


def inspect_state(evidence, expected=None):
    """Read atomic snapshots without acquiring the mutator lock or writing files."""
    evidence = private(evidence, directory=True)
    record = strict(read_private(evidence / 'campaign-state.json'))
    require(expected is None or expected == record['planSha256'], 'outside-plan-approval-differs')
    plan = strict(read_private(evidence / 'campaign-plan.json', record['planSha256']))
    require(record['schema'] == SCHEMA and record['runId'] == plan['runId'], 'state-binding')
    latest = evidence / 'latest-observation.json'
    pair_ref = strict(read_private(latest)) if latest.exists() else None
    pair = strict(read_private(pair_ref['path'], pair_ref['sha256'])) if pair_ref else None
    readiness = 'pending'
    if pair:
        require(pair['schema'] == 'hexalith.platform.campaign-observation/v1' and pair['runId'] == record['runId'], 'observation-owner-drift')
        descriptor = strict(read_private(pair['descriptor']['path'], pair['descriptor']['sha256']))
        readback = strict(read_private(pair['readback']['path'], pair['readback']['sha256']))
        require(readback['runId'] == record['runId'] and readback['planSha256'] == record['planSha256']
                and readback['descriptorSha256'] == pair['descriptor']['sha256'], 'observation-pair-drift')
        readiness = descriptor['bindings']['environment']['readiness']['status']
    return {**{k: record[k] for k in ('runId', 'status', 'terminal', 'cleanup')}, 'qualified': False,
        'planSha256': record['planSha256'], 'target': record['target'], 'window': plan['window'], 'sizing': record.get('sizing'),
        'resources': record['resources'], 'readiness': readiness, 'observation': pair, 'observationPair': pair_ref,
        'latestObservationReference': str(latest), 'ownedNodeIds': [v['id'] for v in record['nodes']],
        'failure': record.get('failure'), 'failureCapture': record.get('failureCapture'), 'quiescence': record.get('quiescence')}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('prepare', 'inspect', 'launch', 'monitor', 'stop', 'remove'))
    parser.add_argument('--state', type=Path, required=True)
    parser.add_argument('--plan', type=Path)
    parser.add_argument('--approved-plan-sha256')
    args = parser.parse_args(argv)
    q = None
    def interrupted(signum, frame):
        raise KeyboardInterrupt
    original = {s: signal.signal(s, interrupted) for s in (signal.SIGINT, signal.SIGTERM)}
    try:
        if args.action == 'inspect':
            print(json.dumps(inspect_state(args.state, args.approved_plan_sha256), sort_keys=True))
            return 0
        if args.action == 'prepare':
            require(args.plan is not None, 'plan-required')
            record = prepare(args.plan, args.approved_plan_sha256, args.state)
        else:
            if args.action in {'stop', 'remove'}:
                private(args.state, directory=True)
                request = {'action': args.action, 'requested': {'utcMs': int(time.time() * 1000), 'boottimeNs': capacity._boottime(), 'bootId': capacity._boot()}}
                try:
                    atomic(args.state / 'stop-request.json', request, exclusive=True)
                except FileExistsError:
                    pass  # First cancellation consumes the original stopping budget.
                end = time.monotonic() + STOPPING_SECONDS
                while q is None:
                    try:
                        q = CampaignQualification(args.state)
                    except RuntimeError as error:
                        require('already in use' in str(error) and time.monotonic() < end, 'active-owner-not-stopped')
                        time.sleep(0.2)
            else:
                q = CampaignQualification(args.state)
            if args.action in {'launch', 'monitor'}:
                require(args.approved_plan_sha256 == q.record['planSha256'], 'outside-plan-approval-required')
                q.supervise(args.action)
            elif args.action in {'stop', 'remove'}:
                q.stop(remove=args.action == 'remove')
            record = q.record
        output = {k: record[k] for k in ('runId', 'status', 'terminal', 'cleanup')}
        output['qualified'] = False
        print(json.dumps(output, sort_keys=True))
        return 1 if args.action in {'stop', 'remove'} and record['cleanup']['unverified'] else 0
    except (ValueError, OSError, RuntimeError, KeyError, TypeError, KeyboardInterrupt) as error:
        print(json.dumps({'status': 'refused', 'qualified': False, 'causeCode': cause_code(error)}))
        return 1
    finally:
        if q is not None:
            q.close()
        for signum, handler in original.items():
            signal.signal(signum, handler)


if __name__ == '__main__':
    raise SystemExit(main())
