"""Independent prerequisite calibration mode of the existing Qualification runner.

It does not call finite campaign admission or a Memories C2 producer. Actual
outside-approved storage/host reservations replace measurement-dependent campaign
admission for this one attempt only. Its output never grants campaign gate credit.
"""
import argparse
import copy
import json
from pathlib import Path
import re
import signal
import time

import campaign_capacity as capacity
import campaign_environment as environment
import campaign_lifecycle as lifecycle
from qualify import HERE, CHART

SCHEMA = 'hexalith.platform.independent-calibration/v1'
PROFILE_ID = 'PG-INDEPENDENT-CALIBRATION-1'
EXECUTION_SECONDS = 50400
ALLOCATION_NAMES = {'postgres', 'control', 'scheduler-0', 'scheduler-1', 'scheduler-2', 'application-spill', 'host-workspace', 'backup'}
INPUT_GROUPS = capacity.INPUT_GROUPS - {'workloadPlan', 'renewalEvidence', 'storageConfiguration'}
LIMITS = {'attempts': 1, 'retries': 0, 'restarts': 0, 'startupSeconds': 600, 'executionSeconds': EXECUTION_SECONDS,
    'stoppingSeconds': 900, 'totalSeconds': 51900, 'postgresCpuMilli': 8000, 'postgresMemoryBytes': 16 * capacity.GIB,
    'cpuMilli': 16250, 'memoryBytes': 31 * capacity.GIB, 'writers': 2, 'recordsPerWriterSecond': 125,
    'overlapMilliseconds': 1800000, 'acceptedRecords': 450000, 'componentOperations': 900000, 'backlogRecords': 150000}
SMALL_LIMITS = {**LIMITS, 'postgresCpuMilli': 2000, 'postgresMemoryBytes': 4 * capacity.GIB,
    'cpuMilli': 10250, 'memoryBytes': 19 * capacity.GIB}


def require(value, code):
    lifecycle.require(value, 'independent-calibration-' + code)


def s3_adapter():
    # Legacy admission/execution does not load an unselected optional closure.
    import calibration_s3_backup
    return calibration_s3_backup


def identity(plan):
    """Exact independent identity; it is deliberately not a finite profile."""
    fields = {k: plan[k] for k in ('schema', 'runId', 'target', 'docker', 'paths', 'tools', 'toolFiles',
        'assets', 'environment', 'source', 'inputs', 'allocations', 'allocationBasis', 'limits', 'reservations')}
    if 's3Backup' in plan:
        fields['s3Backup'] = plan['s3Backup']
    return capacity.digest(fields)


def selected_limits(plan):
    """Select one exact signed candidate; configured ceilings are not measurements."""
    limits = plan['limits']
    require(isinstance(limits, dict) and set(limits) == set(LIMITS)
        and all(type(value) is int for value in limits.values())
        and limits in (LIMITS, SMALL_LIMITS), 'identity-or-budget-drift')
    return dict(limits)


def topology(plan, profile):
    """Specialize only independent PostgreSQL compute and its two memory settings."""
    limits = selected_limits(plan)
    objects = environment.topology(profile, plan['environment'], plan['runId'], profile_id=PROFILE_ID)
    postgres = [o for o in objects if o['kind'] == 'StatefulSet' and o['metadata']['name'] == 'access-telemetry-postgresql']
    require(len(postgres) == 1, 'postgres-required')
    container = postgres[0]['spec']['template']['spec']['containers'][0]
    require(container['resources']['limits'] == {'cpu': '8', 'memory': '16Gi'}, 'postgres-caps')
    if limits == SMALL_LIMITS:
        args = container['args']
        require(isinstance(args, list) and all(isinstance(value, str) for value in args), 'postgres-memory-tuning')
        for name, original, selected in (('shared_buffers', '4GB', '1GB'), ('effective_cache_size', '12GB', '3GB')):
            positions = [i for i, value in enumerate(args) if value.split('=', 1)[0] == name]
            require(len(positions) == 1 and positions[0] > 0 and args[positions[0] - 1] == '-c'
                and args[positions[0]] == name + '=' + original, 'postgres-memory-tuning')
            args[positions[0]] = name + '=' + selected
        resources = {'cpu': str(limits['postgresCpuMilli'] // 1000), 'memory': str(limits['postgresMemoryBytes'] // capacity.GIB) + 'Gi'}
        for section in ('requests', 'limits'):
            container['resources'].setdefault(section, {}).update(resources)
    return objects


def load_plan(path, expected):
    require(re.fullmatch('[0-9a-f]{64}', expected or ''), 'outside-plan-approval-required')
    value = lifecycle.strict(lifecycle.read_private(path, expected))
    require(isinstance(value, dict) and set(value) == {'schema', 'runId', 'target', 'docker', 'paths', 'tools', 'toolFiles',
        'assets', 'environment', 'source', 'inputs', 'allocations', 'allocationBasis', 'limits', 'window', 'clock',
        'authority', 'reservations', 'profileSha256'} | ({'s3Backup'} if 's3Backup' in value else set()), 'plan-incomplete')
    require(value['schema'] == SCHEMA and re.fullmatch('[0-9a-f]{32}', value['runId']), 'identity')
    selected_limits(value)
    require(value['profileSha256'] == identity(value), 'identity-or-budget-drift')
    target = value['target']
    require(set(target) == {'hostId', 'clusterName', 'context', 'namespace', 'nodeHa'} and bool(target['hostId'])
        and target['nodeHa'] is False and target['clusterName'] == 'hxcal-' + value['runId']
        and target['context'] == 'kind-' + target['clusterName']
        and target['namespace'] == 'hxcal-' + value['runId'] + '-qualification', 'isolated-target-required')
    require(set(value['docker']) == {'host', 'engineId'} and value['docker']['host'].startswith('unix:///')
        and bool(value['docker']['engineId']), 'physical-engine-required')
    window = value['window']
    require(set(window) == {'earliestLaunchUtcMs', 'latestLaunchUtcMs', 'expiresUtcMs'}
        and all(type(v) is int for v in window.values())
        and window['earliestLaunchUtcMs'] <= window['latestLaunchUtcMs']
        and window['latestLaunchUtcMs'] + (EXECUTION_SECONDS + 900) * 1000 <= window['expiresUtcMs']
        and window['expiresUtcMs'] - window['earliestLaunchUtcMs'] <= 51900000, 'original-window-required')
    require(set(value['paths']) == {'primary', 'backup'}, 'storage-paths')
    for path in value['paths'].values():
        lifecycle.private(path, directory=True)
    require(value['paths']['primary'] != value['paths']['backup'], 'backup-alias')
    require(set(value['tools']) == {'docker', 'kind', 'kubectl', 'helm', 'dotnet', 'buildsCli', 'python'}, 'tools-incomplete')
    require(set(value['allocations']) == ALLOCATION_NAMES and all(type(n) is int and n > 0 for n in value['allocations'].values()), 'separate-storage-allocation-required')
    # The basis is an actual independently approved calibration allocation, not
    # campaign single-collection sizing or a guessed renewal inventory.
    basis = capacity.retained_json(value['allocationBasis'])
    require(basis == {'schema': SCHEMA, 'purpose': 'independent-calibration-allocation', 'allocations': value['allocations']}, 'allocation-basis-differs')
    if 's3Backup' in value:
        s3_adapter().configuration(value)
    return value


def current_authority(plan, plan_sha, action, now):
    config = plan['authority']
    status = lifecycle.signed(config['statusPath'], config['key'])
    require(set(status) == {'schema', 'issuer', 'owner', 'runId', 'planSha256', 'profileSha256', 'observedUtcMs',
        'expiresUtcMs', 'disposition', 'actions'}, 'authority-shape')
    require(status['schema'] == SCHEMA and status['issuer'] == config['issuer'] and status['owner'] == 'github:user:6775094'
        and status['runId'] == plan['runId'] and status['planSha256'] == plan_sha
        and status['profileSha256'] == plan['profileSha256'], 'authority-binding')
    require(status['disposition'] == 'active' and isinstance(status['actions'], list) and action in status['actions']
        and set(status['actions']) <= {'prepare', 'launch', 'monitor'}, 'authority-withdrawn-or-action')
    require(type(status['observedUtcMs']) is int and type(status['expiresUtcMs']) is int
        and 0 <= now - status['observedUtcMs'] <= 300000
        and now < status['expiresUtcMs'] <= status['observedUtcMs'] + 900000, 'authority-stale')
    return status


def reservations(plan, now):
    limits = selected_limits(plan)
    require(set(plan['reservations']) == {'compute', 'primary', 'backup'}, 'reservation-missing')
    records = {}
    for name, config in plan['reservations'].items():
        if name == 'backup' and 's3Backup' in plan:
            continue
        require(set(config) == {'issuer', 'path', 'key', 'binding'}, 'reservation-custody')
        record = lifecycle.signed(config['path'], config['key'])
        common = {'schema', 'issuer', 'runId', 'profileSha256', 'observedUtcMs', 'validUntilUtcMs', 'hostId', 'engineId',
            'reservationId', 'owner', 'validFromUtcMs', 'disposition', 'revokedUtcMs'}
        fields = {'cpuMilliAvailable', 'cpuMilliCompeting', 'cpuMilliReserved', 'memoryBytesAvailable', 'memoryBytesCompeting', 'memoryBytesReserved'} if name == 'compute' else {'path', 'poolId', 'failureDomain', 'filesystemId', 'linux', 'availableBytes', 'competingBytes', 'reservedBytes', 'allocations'}
        require(set(record) == common | fields and record['schema'] == SCHEMA and record['issuer'] == config['issuer']
            and record['runId'] == plan['runId'] and record['profileSha256'] == plan['profileSha256'], 'reservation-binding')
        require(isinstance(record['reservationId'], str) and re.fullmatch(r'[a-zA-Z0-9][a-zA-Z0-9:/._-]{0,127}', record['reservationId'])
            and record['owner'] == 'github:user:6775094', 'reservation-identity')
        require(record['disposition'] == 'active' and record['revokedUtcMs'] is None, 'reservation-withdrawn')
        require(type(record['validFromUtcMs']) is int and type(record['validUntilUtcMs']) is int
            and record['validFromUtcMs'] <= now < record['validUntilUtcMs'], 'reservation-not-current')
        require(type(record['observedUtcMs']) is int and 0 <= now - record['observedUtcMs'] <= 300000
            and record['validUntilUtcMs'] >= plan['window']['expiresUtcMs'], 'reservation-stale-or-short')
        require(isinstance(config['binding'], dict) and set(config['binding']) == ({'hostId', 'engineId'} if name == 'compute' else {'hostId', 'engineId', 'path', 'poolId', 'failureDomain', 'filesystemId'}) | {'reservationId', 'owner', 'validFromUtcMs', 'validUntilUtcMs'}
            and all(record[k] == v for k, v in config['binding'].items()), 'reservation-backing-drift')
        if name == 'compute':
            require(record['hostId'] == plan['target']['hostId'] and record['engineId'] == plan['docker']['engineId'], 'compute-target')
            for kind, demand in (('cpuMilli', limits['cpuMilli']), ('memoryBytes', limits['memoryBytes'])):
                require(all(type(record[kind + suffix]) is int and record[kind + suffix] >= 0 for suffix in ('Available', 'Competing', 'Reserved'))
                    and record[kind + 'Available'] - record[kind + 'Competing'] >= demand
                    and record[kind + 'Reserved'] >= demand, 'net-compute-insufficient')
        else:
            expected = {k: n for k, n in plan['allocations'].items() if (k == 'backup') == (name == 'backup')}
            require(record['linux'] is True and record['path'] == plan['paths'][name] and all(record[k] for k in ('hostId', 'poolId', 'failureDomain', 'filesystemId'))
                and record['allocations'] == expected, 'storage-scope')
            if name == 'primary':
                require(record['hostId'] == plan['target']['hostId'] and record['engineId'] == plan['docker']['engineId'], 'primary-target')
            require(all(type(record[k]) is int and record[k] >= 0 for k in ('availableBytes', 'competingBytes', 'reservedBytes'))
                and record['reservedBytes'] >= sum(expected.values())
                and record['availableBytes'] - record['competingBytes'] >= sum(expected.values()), 'net-storage-insufficient')
        records[name] = record
    if 's3Backup' in plan:
        records['backup'] = s3_adapter().reservation(plan, now, records['primary'])
        return records
    primary, backup = records['primary'], records['backup']
    require(all(primary[k] != backup[k] for k in ('poolId', 'failureDomain', 'filesystemId'))
        and primary['hostId'] != backup['hostId'], 'independent-linux-backup-required')
    return records


def admit(plan, plan_sha, action):
    limits = selected_limits(plan)
    require(plan['profileSha256'] == identity(plan), 'identity-or-budget-drift')
    now = lifecycle.clock(plan)
    require(now < plan['window']['expiresUtcMs'], 'reservation-expired')
    current_authority(plan, plan_sha, action, now)
    reservations(plan, now)
    for ref in [*plan['tools'].values(), *plan['toolFiles']]:
        lifecycle.binary(ref)
    inputs = plan['inputs']
    require(set(inputs) == INPUT_GROUPS and all(isinstance(group, list) and 1 <= len(group) <= 128 for group in inputs.values()), 'inputs-incomplete')
    for ref in [*plan['source'], *[r for group in inputs.values() for r in group]]:
        capacity.reference(ref)
    sources = {r['path']: r['sha256'] for r in plan['source']}
    required = {'qualify.py', 'campaign_lifecycle.py', 'campaign_capacity.py', 'campaign_environment.py', 'independent_calibration.py', 'calibration_workload.py'}
    if 's3Backup' in plan:
        required.add('calibration_s3_backup.py')
    require({str((HERE / n).resolve()) for n in required} <= set(sources)
        and inputs['source'] == plan['source'], 'source-closure')
    pins = json.loads((HERE / 'toolchain.json').read_bytes())
    require(len(inputs['toolchain']) == 1 and capacity.retained_json(inputs['toolchain'][0]) == pins, 'toolchain-drift')
    require(set(plan['assets']) == {'dapr', 'gateway', 'network'}, 'assets-incomplete')
    for name, ref in plan['assets'].items():
        capacity.reference(ref)
        require(name not in pins or ref['sha256'] == pins[name]['sha256'], 'asset-pin-drift')
    require({r['path'] for r in inputs['chart']} == {str(p.resolve()) for p in CHART.rglob('*') if p.is_file()}, 'chart-closure')
    require(len(inputs['values']) == len(inputs['render']) == len(inputs['backendImages']) == len(inputs['applications']) == 1, 'input-ambiguity')
    require(capacity.retained_json(inputs['applications'][0]) == plan['environment'], 'environment-binding')
    require(capacity.retained_json(inputs['tool'][0]) == {'tools': plan['tools'], 'toolFiles': plan['toolFiles'], 'assets': plan['assets']}, 'tool-binding')
    values = capacity.retained_json(inputs['values'][0])
    configured = capacity.configure_application(values, json.loads((CHART / 'reference-graph.json').read_bytes()))
    configured['capacityProfile'] = PROFILE_ID
    require(values == configured and plan['environment']['faultWindows'] == [], 'application-or-fault-drift')
    require(plan['environment']['leaseHolders'] == ['calibration/owner-6775094/' + plan['runId']], 'independent-holder-required')
    backend = capacity.retained_json(inputs['backendImages'][0])
    require(backend['postgresql'] == 'docker.io/library/postgres:18.6-trixie@sha256:5a5a84b19854a9ffaa54082c166ff4ec27473a361e496e5ea167f298f2da9722'
        and backend['openbao'] == 'quay.io/openbao/openbao:2.6.4@sha256:cf2340fc9a22cb9358ca0defd1f39b65673836bd23fe2bb8984a07e11fe13ef4', 'backend-pin-drift')
    profile = {'schema': SCHEMA, 'profileId': PROFILE_ID, 'runId': plan['runId'], 'approvedPlanSha256': plan_sha, 'target': plan['target'], 'inputs': inputs,
        'sizing': {'allocations': plan['allocations'], 'cpuMilli': limits['cpuMilli'], 'memoryBytes': limits['memoryBytes']}}
    objects = topology(plan, profile)
    supplemental = [*capacity.retained_json(plan['environment']['providerObjects']), *capacity.retained_json(plan['environment']['reporterObjects'])]
    environment.supplemental_budget([*supplemental, *lifecycle.documents(capacity.snapshot(plan['assets']['network']['path'], plan['assets']['network']['sha256']).decode())])
    for ref in plan['environment']['secrets'] + [plan['environment']['openbaoSeed']]:
        lifecycle.read_private(ref['path'], ref['sha256'])
    # Use the existing authenticated, complete monitor contract. Calibration has
    # no fault exemptions and its observed values cannot grant campaign credit.
    lifecycle.monitoring_config(plan, {'sizing': profile['sizing'], 'storage': [
        {'poolId': c['poolId'], 'allocations': list(c['allocations'])} for c in reservations(plan, now).values() if 'poolId' in c]})
    if action in {'prepare', 'launch'}:
        receipt = lifecycle.monitor_receipt(plan, plan_sha, now)
        require(receipt['phase'] == 'preflight' and receipt['target'] is None and not receipt['expectedFaults']
            and all(v is None for v in receipt['series'].values()), 'monitor-preflight-required')
    return profile, objects, now


PHYSICAL_SQL = """SELECT json_build_object(
 'databaseBytes',pg_database_size(current_database()),
 'heapToastBytes',pg_table_size('access_telemetry.lifecycle_state'),
 'indexBytes',pg_indexes_size('access_telemetry.lifecycle_state'),
 'walBytes',(SELECT coalesce(sum(size),0) FROM pg_ls_waldir()),
 'recordCount',(SELECT count(*) FROM access_telemetry.lifecycle_state WHERE key LIKE 'memories-access-telemetry||records/%'),
 'relations',(SELECT coalesce(json_agg(json_build_object('schema',n.nspname,'name',c.relname,'kind',c.relkind,
 'bytes',pg_relation_size(c.oid),'totalBytes',pg_total_relation_size(c.oid)) ORDER BY n.nspname,c.relname),'[]'::json)
 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind IN ('r','i','t','m')
 AND (n.nspname='access_telemetry' OR n.nspname='pg_toast')));"""


class CalibrationQualification(lifecycle.CampaignQualification):
    schema = SCHEMA
    state_name = 'calibration-state.json'
    plan_name = 'calibration-plan.json'
    composition_id = PROFILE_ID
    execution_seconds = EXECUTION_SECONDS

    def admission(self, action):
        return admit(self.plan, self.record['planSha256'], action)

    def supervise(self, action):
        try:
            getattr(self, action)()
        except BaseException as error:
            self.failure(error)
            request = self.evidence / 'stop-request.json'
            self.stop(remove=not request.exists() or lifecycle.strict(lifecycle.read_private(request))['action'] == 'remove')
            raise

    def stop(self, *, remove=False):
        if 's3Backup' not in self.plan:
            return super().stop(remove=remove)
        unverified = not s3_adapter().verified(self)
        def persist_custody():
            self.record.setdefault('cleanup', {})
            self.record['cleanup']['backupVerified'] = not unverified
            self.record['cleanup']['backupDisposition'] = 'immutable-version-retained' if not unverified else 'unverified-primary-and-staging-retained'
            if unverified:
                self.record['cleanup']['persistentData'] = 'retained-in-original-primary-and-private-staging-custody'
                self.record['cleanup']['unverified'] = True
            self.save()
        persist_custody()
        try:
            super().stop(remove=remove and not unverified)
        finally:
            persist_custody()

    def phases(self, profile):
        # This independent action never opens the campaign reporter Job.
        return {'c3': [0, 0]}

    @staticmethod
    def binding(profile, profile_sha256):
        return {'apiVersion': 'v1', 'kind': 'ConfigMap',
            'metadata': {'name': 'access-telemetry-campaign-identity', 'namespace': profile['target']['namespace']},
            'data': {'profileId': PROFILE_ID, 'profileSha256': profile_sha256, 'capacityProfileSha256': capacity.digest(profile),
                'calibrationRunId': profile['runId'], 'calibrationPlanSha256': profile['approvedPlanSha256']}}

    def check_storage(self, **_):
        now = lifecycle.clock(self.plan)
        value = lifecycle.monitor_receipt(self.plan, self.record['planSha256'], now)
        require(value['phase'] == 'observed' and value['target'] == self.record['physicalTarget'] and not value['expectedFaults'], 'monitor-target-or-fault')
        limits = self.plan['environment']['monitoring']['limits']
        observation = {'receiptSha256': value['_receiptSha256'], 'observedUtcMs': value['observedUtcMs'], 'qualified': False,
            'allocations': {name: {k: item[k] if type(item[k]) is int else None for k in ('usedBytes', 'availableBytes')}
                for name, item in value['allocations'].items()}}
        self.record['storageObservation'] = observation
        self.save()  # Preserve observed operands before any failure/disposal.
        for name, item in value['allocations'].items():
            require(type(item['usedBytes']) is int and type(item['availableBytes']) is int and item['usedBytes'] >= 0 and item['availableBytes'] >= 0
                and item['usedBytes'] + item['availableBytes'] == self.plan['allocations'][name]
                and item['availableBytes'] * 5 >= self.plan['allocations'][name], 'allocation-readback-or-headroom')
        for key, number in value['series'].items():
            require(type(number) is int and number >= 0, 'monitor-data-missing')
            metric = key.split('.')[1]
            require(metric not in limits or number <= limits[metric], 'monitor-limit')
        require(value['series']['postgres.walBytes'] <= capacity.BOUNDS['walPeakBytes'], 'wal-bound')

    def remaining(self, stopping=False):
        if stopping and getattr(self, '_measurement_quiescing', False):
            self.admission('monitor')
            anchor = self.record['measurementQuiescence']['started']
            require(anchor['bootId'] == capacity._boot(), 'quiescence-boot-changed')
            elapsed = (capacity._boottime() - anchor['boottimeNs']) / 1e9
            require(elapsed >= 0, 'quiescence-elapsed-regressed')
            return max(0, min(super().remaining(False), 900 - elapsed,
                (self.record['measurementQuiescence']['deadlineUtcMs'] - int(time.time() * 1000)) / 1000))
        return super().remaining(stopping)

    def measurement_quiesce(self):
        # Quiescence belongs to execution, and does not start/reset cleanup or
        # terminal state. Reuse the exact inherited control/UID/lineage checks.
        self.admission('monitor')
        self.observe()
        ns = self.record['target']['namespace']
        listing = lifecycle.strict(self.kube('get', 'deployments,replicasets,pods', '-n', ns, '-o', 'json', timeout=10).stdout)['items']
        deployments = {o['metadata']['uid'] for o in listing if o['kind'] == 'Deployment'}
        replicas = {o['metadata']['uid'] for o in listing if o['kind'] == 'ReplicaSet'
            and any(r.get('controller') is True and r.get('uid') in deployments for r in o['metadata'].get('ownerReferences', []))}
        retired = [lifecycle.resource_id(o, ns) for o in listing if o['kind'] == 'Pod'
            and any(r.get('controller') is True and r.get('uid') in deployments | replicas for r in o['metadata'].get('ownerReferences', []))]
        started = self.stamp()
        self.record['measurementQuiescence'] = {'started': started,
            'deadlineUtcMs': min(started['utcMs'] + 900000, self.record['executionDeadlineUtcMs']), 'retiredPodIds': retired}
        self.save()
        previous = self._command_bound
        self._command_bound = min(previous if previous is not None else float('inf'), time.monotonic() + min(900, self.remaining()))
        self._measurement_quiescing = True
        try:
            proof = super().quiesce()
            require(proof['state'] == 'verified', 'measurement-quiescence-unverified')
            self.record['measurementQuiescence']['proof'] = proof
            self.record['measurementQuiesced'] = True
            self.save()
            return proof
        finally:
            self._measurement_quiescing = False
            self._command_bound = previous

    def control_readback(self, *, capture=False):
        gate, lease, value = super().control_readback(capture=capture)
        if self.record.get('measurementQuiesced'):
            require(value['state'] == 'disabled' and value['expiresUtcMs'] == 0
                and lease['spec']['holderIdentity'] == '' and lease['spec']['leaseDurationSeconds'] == 0, 'quiesced-controls-reopened')
        return gate, lease, value

    def normalized_live(self, obj, gate):
        if self.record.get('measurementQuiesced') and obj['kind'] == 'Deployment' and 'uid' in obj['metadata']:
            rid = lifecycle.resource_id(obj, self.record['target']['namespace'])
            proof = self.record['measurementQuiescence']['proof']
            if rid in proof['workloadsScaledToZero']:
                owned = {r['id']: r['uid'] for r in self.record['resources']}
                require(owned.get(rid) == obj['metadata']['uid'] and obj['spec']['replicas'] == 0
                    and gate['state'] == 'disabled', 'quiesced-workload-reopened-or-replaced')
                obj = copy.deepcopy(obj)
                obj['spec']['replicas'] = self.record['admittedResources'][rid]['spec'].get('replicas', 1)
                gate = {'state': 'enabled'}
        return super().normalized_live(obj, gate)

    def guard(self):
        if (self.evidence / 'stop-request.json').exists():
            raise InterruptedError('campaign-lifecycle-stop-requested')
        _, _, now = self.admission('monitor')
        require(self.remaining() > 0, 'original-budget-exhausted')
        # Parent process execution is already recorded inFlight; nested native
        # observations use the same process owner while preserving that intent.
        inflight = self.record['inFlight']; self.record['inFlight'] = None
        try:
            self.observe()
            if not self.record.get('measurementQuiesced'):
                self.enable(now)
            self.sample()
        finally:
            self.record['inFlight'] = inflight; self.save()

    def enable(self, now):
        gate, lease, _ = self.control_readback()
        ns = self.record['target']['namespace']
        for kind, obj, changes in (
            ('configmap', gate, [{'op': 'replace', 'path': '/data/gate.json', 'value': json.dumps({'schemaVersion': 1, 'state': 'enabled', 'profileSha256': self.plan['profileSha256'], 'expiresUtcMs': now + 60000})}]),
            ('lease', lease, [{'op': 'replace', 'path': '/spec/holderIdentity', 'value': self.plan['environment']['leaseHolders'][0]},
                {'op': 'add', 'path': '/spec/renewTime', 'value': lifecycle.utc(now)}, {'op': 'replace', 'path': '/spec/leaseDurationSeconds', 'value': 60}])):
            patch = [{'op': 'test', 'path': '/metadata/uid', 'value': obj['metadata']['uid']},
                {'op': 'test', 'path': '/metadata/resourceVersion', 'value': obj['metadata']['resourceVersion']}, *changes]
            self.kube('patch', kind, obj['metadata']['name'], '-n', ns, '--type=json', '--patch', json.dumps(patch), timeout=10)

    def physical(self):
        value = lifecycle.strict(self.kube('-n', self.record['target']['namespace'], 'exec', 'statefulset/access-telemetry-postgresql',
            '-c', 'postgresql', '--', 'psql', '--no-psqlrc', '--tuples-only', '--no-align', '--dbname=memories_access_telemetry', '--command', PHYSICAL_SQL,
            timeout=30, maximum_output_bytes=512 * 1024).stdout)
        require(set(value) == {'databaseBytes', 'heapToastBytes', 'indexBytes', 'walBytes', 'recordCount', 'relations'}
            and all(type(value[k]) is int and value[k] >= 0 for k in value if k != 'relations')
            and isinstance(value['relations'], list) and 1 <= len(value['relations']) <= 2048, 'physical-readback-missing')
        return value

    def sample(self):
        # The metrics API supplies actual per-container CPU/memory, including
        # sidecars. A missing metrics provider refuses instead of zero filling.
        from decimal import Decimal
        limits = selected_limits(self.plan)
        def quantity(raw, cpu):
            require(isinstance(raw, str), 'compute-quantity')
            match = re.fullmatch(r'([0-9]+(?:\.[0-9]+)?)(n|u|m|Ki|Mi|Gi|Ti)?', raw)
            require(match is not None, 'compute-quantity')
            number, unit = match.groups()
            units = {'n': Decimal('.000001'), 'u': Decimal('.001'), 'm': Decimal(1), None: Decimal(1000)} if cpu else {None: 1, 'Ki': 1024, 'Mi': 1024**2, 'Gi': 1024**3, 'Ti': 1024**4}
            require(unit in units, 'compute-quantity-unit')
            return int((Decimal(number) * units[unit]).to_integral_value(rounding='ROUND_CEILING'))
        observed = lifecycle.strict(self.kube('get', '--raw', '/apis/metrics.k8s.io/v1beta1/pods', timeout=15, maximum_output_bytes=512 * 1024).stdout)
        require(isinstance(observed.get('items'), list) and 1 <= len(observed['items']) <= 256, 'compute-readback-missing')
        cpu = memory = 0
        pg_found = False
        now = lifecycle.clock(self.plan)
        from datetime import datetime
        ready_pods = {r['id'] for r in self.record['resources'] if r['id'].startswith('Pod/') and r['readiness'] == 'ready'}
        if self.record.get('measurementQuiesced'):
            ready_pods -= set(self.record['measurementQuiescence']['retiredPodIds'])
        measured_pods = set()
        for pod in observed['items']:
            measured_pods.add('Pod/' + pod['metadata']['namespace'] + '/' + pod['metadata']['name'])
            timestamp = int(datetime.fromisoformat(pod['timestamp'].replace('Z', '+00:00')).timestamp() * 1000)
            require(0 <= now - timestamp <= 60000, 'compute-readback-stale')
            require(pod['metadata']['namespace'] in self.observation_namespaces() + ['kube-system', 'local-path-storage'], 'foreign-compute-readback')
            for c in pod['containers']:
                c_cpu, c_memory = quantity(c['usage']['cpu'], True), quantity(c['usage']['memory'], False)
                cpu += c_cpu; memory += c_memory
                if pod['metadata']['name'] == 'access-telemetry-postgresql-0' and c['name'] == 'postgresql':
                    pg_found = True
                    require(c_cpu <= limits['postgresCpuMilli'] and c_memory <= limits['postgresMemoryBytes'], 'postgres-compute-bound')
        require(ready_pods <= measured_pods, 'compute-readback-incomplete')
        require(pg_found and cpu <= limits['cpuMilli'] and memory <= limits['memoryBytes'], 'aggregate-compute-bound')
        physical = self.physical()
        current = self.record.get('nativeReadbacks', {'sampleCount': 0, 'peaks': {}, 'limits': limits})
        # Historical readbacks predate selection and necessarily used LIMITS.
        # They cannot be relabelled as measurements of the new small candidate.
        require(current.get('limits', LIMITS) == limits, 'compute-limits-drift')
        current['limits'] = limits
        current['sampleCount'] += 1
        for k, v in {**{k: v for k, v in physical.items() if k != 'relations'}, 'cpuMilli': cpu, 'memoryBytes': memory}.items():
            current['peaks'][k] = max(current['peaks'].get(k, 0), v)
        current['last'] = {'observed': self.stamp(), 'physical': physical, 'cpuMilli': cpu, 'memoryBytes': memory}
        self.record['nativeReadbacks'] = current
        self.save()

    def operation_count(self):
        ns = self.record['target']['namespace']
        pods = lifecycle.strict(self.kube('get', 'pods', '-n', ns, '-l', 'app.kubernetes.io/name=memories-access-telemetry', '-o', 'json', timeout=10).stdout)['items']
        require(len(pods) == 2, 'lifecycle-writers-missing')
        counts = {}
        for pod in pods:
            text = self.kube('exec', '-n', ns, 'pod/' + pod['metadata']['name'], '-c', 'lifecycle', '--',
                '/bin/sh', '-ec', 'wget -qO- http://127.0.0.1:8080/metrics', timeout=15, maximum_output_bytes=512 * 1024).stdout
            samples = re.findall(r'^memories_access_telemetry_lifecycle_state_operations_total(?:\{[^\n]*\})? ([0-9]+(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)$', text, re.M)
            require(samples, 'component-counter-missing')
            counts[pod['metadata']['uid']] = sum(int(float(v)) for v in samples)
        return counts

    def lane(self, name, pods):
        config = {'kubectl': self.plan['tools']['kubectl'], 'kubeconfig': str(self.kubeconfig), 'kubeconfigSha256': self.record['kubeconfigSha256'],
            'context': self.context, 'namespace': self.record['target']['namespace'], 'runId': 'run-' + self.record['runId'][:24],
            'lane': name, 'pods': pods, 'segments': 1200 if name == 'backlog' else 1801,
            'plan': {'path': str(self.evidence / self.plan_name), 'sha256': self.record['planSha256']}}
        path = self.evidence / (name + '-configuration.json'); lifecycle.atomic(path, config, exclusive=True)
        directory = self.evidence / name; directory.mkdir(mode=0o700)
        self.env['HEXALITH_CALIBRATION_CHILD'] = capacity.digest(config)
        try:
            result = self.run(['python', '-B', str(HERE / 'calibration_workload.py'), '--configuration', path, '--evidence', directory],
                timeout=self.remaining(), heartbeat=self.guard, maximum_output_bytes=1024 * 1024)
        finally:
            self.env.pop('HEXALITH_CALIBRATION_CHILD', None)
        value = lifecycle.strict(result.stdout)
        require(value['lane'] == name and value['retries'] == 0 and value['acceptedRecords'] >= (150000 if name == 'backlog' else 450000), 'workload-result')
        lifecycle.atomic(self.evidence / (name + '-result.json'), value, exclusive=True)
        return value

    def monitor(self, **_):
        require(self.record['status'] == 'ready' and self.record['terminal'] is None and self.record['inFlight'] is None
            and not self.record.get('measurementStarted'), 'measurement-already-consumed-or-uncertain')
        self.admission('monitor')
        self.record['measurementStarted'] = self.stamp(); self.save()
        ns = self.record['target']['namespace']
        self._command_bound = time.monotonic() + max(0, 600 - (capacity._boottime() - self.record['started']['boottimeNs']) / 1e9)
        self.enable(lifecycle.clock(self.plan))
        self.kube('scale', '-n', ns, 'deployment/memories-access-telemetry', '--replicas=2', timeout=15)
        self.kube('scale', '-n', ns, 'deployment/memories-access-telemetry-clock', '--replicas=1', timeout=15)
        self.kube('rollout', 'status', '-n', ns, 'deployment/memories-access-telemetry', '--timeout=120s', timeout=125)
        self.kube('rollout', 'status', '-n', ns, 'deployment/memories-access-telemetry-clock', '--timeout=120s', timeout=125)
        self._command_bound = None
        listing = lifecycle.strict(self.kube('get', 'pods', '-n', ns, '-l', 'app.kubernetes.io/name=memories', '-o', 'json', timeout=10).stdout)['items']
        require(len(listing) == 2, 'two-server-writers-required')
        pods = sorted(p['metadata']['name'] for p in listing)
        before = self.physical()
        require(before['recordCount'] == 0, 'empty-independent-target-required')
        backlog = self.lane('backlog', pods[:1])
        backlog_physical = self.physical()
        require(backlog_physical['recordCount'] == 150000, 'separate-backlog-not-observed')
        counters_before = self.operation_count()
        writers = self.lane('writers', pods)
        counters_after = self.operation_count()
        require(set(counters_before) == set(counters_after) and all(counters_after[k] >= v for k, v in counters_before.items()), 'component-counter-reset')
        operations = sum(counters_after.values()) - sum(counters_before.values())
        require(operations >= 900000 and writers['commonOverlapMilliseconds'] >= 1800000, 'component-workload-below-contract')
        pre_rewrite = self.physical()
        # The exact owned target is quiesced before its separately authorized
        # maintenance operation. This is a measurement, never a C3 reclaim proof.
        quiescence = self.measurement_quiesce()
        require(quiescence['state'] == 'verified', 'measurement-quiescence-unverified')
        self.kube('exec', '-n', ns, 'statefulset/access-telemetry-postgresql', '-c', 'postgresql', '--', 'psql', '--no-psqlrc',
            '--dbname=memories_access_telemetry', '--command', 'VACUUM (FULL, ANALYZE) access_telemetry.lifecycle_state;', timeout=min(86400, self.remaining()), heartbeat=self.guard)
        after_rewrite = self.physical()
        backup = s3_adapter().Backup(self).execute() if 's3Backup' in self.plan else None
        result = {'schema': SCHEMA, 'runId': self.record['runId'], 'profileSha256': self.plan['profileSha256'],
            'inputIdentity': identity(self.plan), 'limits': selected_limits(self.plan), 'target': self.record['physicalTarget'], 'backlog': backlog, 'writers': writers,
            'componentOperations': operations, 'componentCounters': {'before': counters_before, 'after': counters_after},
            'physical': {'before': before, 'backlog': backlog_physical, 'beforeRewrite': pre_rewrite, 'afterRewrite': after_rewrite},
            'nativeReadbacks': self.record.get('nativeReadbacks'), 'operationalMonitor': self.record.get('storageObservation'), 'qualified': False, 'C1Credit': False, 'C2Credit': False, 'A41Credit': False}
        if backup is not None:
            require(s3_adapter().verified(self), 's3-backup-unverified')
            result['s3Backup'] = backup
        lifecycle.atomic(self.evidence / 'calibration-result.json', result, exclusive=True)
        self.terminal('completed', 'calibration-measurements-recorded')
        self.stop(remove=True)


def prepare(path, expected, evidence):
    plan = load_plan(path, expected)
    profile, objects, now = admit(plan, expected, 'prepare')
    return lifecycle.prepare_admitted(plan, profile, objects, now, expected, evidence, runner=CalibrationQualification,
        raw=capacity.snapshot(path, expected))


def inspect_state(evidence):
    evidence = lifecycle.private(evidence, directory=True)
    record = lifecycle.strict(lifecycle.read_private(evidence / CalibrationQualification.state_name))
    plan = lifecycle.strict(lifecycle.read_private(evidence / CalibrationQualification.plan_name, record['planSha256']))
    selected_limits(plan)
    require(plan['profileSha256'] == identity(plan), 'identity-or-budget-drift')
    require(record['schema'] == SCHEMA and plan['schema'] == SCHEMA and record['runId'] == plan['runId'], 'state-binding')
    return {**{k: record[k] for k in ('runId', 'status', 'terminal', 'cleanup', 'target', 'planSha256')},
        'qualified': False, 'limits': plan['limits'], 'measurementStarted': record.get('measurementStarted'),
        **({'s3Backup': record.get('s3Backup'), 'backupMode': 's3', 'restoreCredit': False} if 's3Backup' in plan else {}),
        'failure': record.get('failure'), 'failureCapture': record.get('failureCapture'), 'resultPath':
        str(evidence / 'calibration-result.json') if (evidence / 'calibration-result.json').exists() else None}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('prepare', 'inspect', 'launch', 'monitor', 'stop', 'remove'))
    parser.add_argument('--state', type=Path, required=True)
    parser.add_argument('--plan', type=Path)
    parser.add_argument('--approved-plan-sha256')
    args = parser.parse_args(argv)
    q = None
    original = {s: signal.signal(s, lambda *_: (_ for _ in ()).throw(KeyboardInterrupt())) for s in (signal.SIGTERM, signal.SIGINT)}
    try:
        if args.action == 'inspect':
            print(json.dumps(inspect_state(args.state), sort_keys=True)); return 0
        if args.action == 'prepare':
            require(args.plan is not None, 'plan-required')
            record = prepare(args.plan, args.approved_plan_sha256, args.state)
        else:
            if args.action in {'stop', 'remove'}:
                lifecycle.private(args.state, directory=True)
                try:
                    lifecycle.atomic(args.state / 'stop-request.json', {'action': args.action, 'requested':
                        {'utcMs': int(time.time() * 1000), 'boottimeNs': capacity._boottime(), 'bootId': capacity._boot()}}, exclusive=True)
                except FileExistsError:
                    pass
            end = time.monotonic() + 900
            while q is None:
                try:
                    q = CalibrationQualification(args.state)
                except RuntimeError as error:
                    require(args.action in {'stop', 'remove'} and 'already in use' in str(error) and time.monotonic() < end, 'active-owner-not-stopped')
                    time.sleep(.2)
            if args.action in {'launch', 'monitor'}:
                require(args.approved_plan_sha256 == q.record['planSha256'], 'outside-plan-approval-required')
                q.supervise(args.action)
            else:
                q.stop(remove=args.action == 'remove')
            record = q.record
        print(json.dumps({**{k: record[k] for k in ('runId', 'status', 'terminal', 'cleanup')}, 'qualified': False}, sort_keys=True))
        return 1 if record.get('cleanup', {}).get('unverified') and (args.action in {'stop', 'remove', 'monitor'} or record.get('terminal') is not None) else 0
    except (ValueError, OSError, RuntimeError, TypeError, KeyError, KeyboardInterrupt) as error:
        print(json.dumps({'status': 'refused', 'qualified': False, 'causeCode': lifecycle.cause_code(error)})); return 1
    finally:
        if q is not None:
            q.close()
        for number, handler in original.items():
            signal.signal(number, handler)


if __name__ == '__main__':
    raise SystemExit(main())
