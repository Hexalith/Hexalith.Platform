"""Independent calibration regressions. All authority/capacity data is fictional.

These local filesystem/process doubles never connect to a daemon or cluster and
must not be imported as operational evidence or used to populate a live plan.
"""
from copy import deepcopy
import hashlib
import hmac
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

import campaign_capacity as capacity
import campaign_lifecycle as lifecycle
import independent_calibration as calibration
import calibration_workload as workload
from qualify import Qualification, HERE


class IndependentCalibrationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='independent-calibration-unit-')
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name); self.root.chmod(0o700)
        self.now = int(time.time() * 1000)
        self.key = b'unit-test-only-not-an-operational-key'
        self.key_ref = self.write('key', self.key)
        self.tools = {k: {'path': '/usr/bin/false', 'sha256': lifecycle.sha('/usr/bin/false')}
            for k in ('docker', 'kind', 'kubectl', 'helm', 'dotnet', 'buildsCli', 'python')}
        self.primary = self.root / 'primary'; self.primary.mkdir(mode=0o700)
        self.backup = self.root / 'backup'; self.backup.mkdir(mode=0o700)
        self.allocations = {k: capacity.GIB for k in calibration.ALLOCATION_NAMES}
        self.plan = {'schema': calibration.SCHEMA, 'runId': 'a' * 32, 'profileSha256': None,
            'target': {'hostId': 'test-primary-host', 'clusterName': 'hxcal-' + 'a' * 32,
                'context': 'kind-hxcal-' + 'a' * 32, 'namespace': 'hxcal-' + 'a' * 32 + '-qualification', 'nodeHa': False},
            'docker': {'host': 'unix:///must-not-connect.sock', 'engineId': 'test-engine'},
            'tools': self.tools, 'toolFiles': [], 'paths': {'primary': str(self.primary), 'backup': str(self.backup)},
            'allocations': self.allocations, 'allocationBasis': self.write('allocation.json', {'schema': calibration.SCHEMA,
                'purpose': 'independent-calibration-allocation', 'allocations': self.allocations}),
            'limits': deepcopy(calibration.LIMITS), 'window': {'earliestLaunchUtcMs': self.now,
                'latestLaunchUtcMs': self.now + 600000, 'expiresUtcMs': self.now + 51900000},
            'clock': {'samplePath': str(self.root / 'clock.json'), 'sourceId': 'unit-independent-utc', 'key': self.key_ref},
            'authority': {'statusPath': str(self.root / 'status.json'), 'issuer': 'unit-owner', 'key': self.key_ref},
            'reservations': {}, 'source': [], 'inputs': {}, 'assets': {}, 'environment': {'faultWindows': []}}
        self.receipts = {}
        for name in ('compute', 'primary', 'backup'):
            binding = {'hostId': 'test-backup-host' if name == 'backup' else 'test-primary-host', 'engineId': 'test-engine'}
            binding.update(reservationId='test-reservation-' + name, owner='github:user:6775094',
                validFromUtcMs=self.now - 1000, validUntilUtcMs=self.plan['window']['expiresUtcMs'])
            record = dict(binding)
            if name == 'compute':
                record.update(cpuMilliAvailable=18000, cpuMilliCompeting=1000, cpuMilliReserved=16250,
                    memoryBytesAvailable=40 * capacity.GIB, memoryBytesCompeting=capacity.GIB, memoryBytesReserved=31 * capacity.GIB)
            else:
                binding.update(path=self.plan['paths'][name], poolId='pool-' + name, failureDomain='domain-' + name,
                    filesystemId='filesystem-' + name)
                record.update(binding)
                record.update(linux=True, availableBytes=100 * capacity.GIB, competingBytes=0, reservedBytes=100 * capacity.GIB,
                    allocations={k: v for k, v in self.allocations.items() if (k == 'backup') == (name == 'backup')})
            self.plan['reservations'][name] = {'issuer': 'unit-' + name, 'path': str(self.root / (name + '-receipt.json')),
                'key': self.key_ref, 'binding': binding}
            self.receipts[name] = record
        self.seal()

    def write(self, name, value):
        path = self.root / name
        lifecycle.atomic(path, value)
        return {'path': str(path), 'sha256': lifecycle.sha(path)}

    def sign(self, path, body):
        lifecycle.atomic(path, {**body, 'signature': hmac.new(self.key, capacity.canonical(body), hashlib.sha256).hexdigest()})

    def seal(self):
        self.plan['profileSha256'] = calibration.identity(self.plan)
        self.plan_ref = self.write('plan.json', self.plan)
        for name, body in self.receipts.items():
            body.update(schema=calibration.SCHEMA, issuer='unit-' + name, runId=self.plan['runId'],
                profileSha256=self.plan['profileSha256'], observedUtcMs=self.now, validUntilUtcMs=self.plan['window']['expiresUtcMs'],
                disposition='active', revokedUtcMs=None)
            self.sign(self.plan['reservations'][name]['path'], body)
        self.status = {'schema': calibration.SCHEMA, 'issuer': 'unit-owner', 'owner': 'github:user:6775094',
            'runId': self.plan['runId'], 'planSha256': self.plan_ref['sha256'], 'profileSha256': self.plan['profileSha256'],
            'observedUtcMs': self.now, 'expiresUtcMs': self.now + 900000, 'disposition': 'active', 'actions': ['prepare', 'launch', 'monitor']}
        self.sign(self.plan['authority']['statusPath'], self.status)
        self.sign(self.plan['clock']['samplePath'], {'utcMs': self.now, 'boottimeNs': capacity._boottime(),
            'bootId': capacity._boot(), 'sourceId': self.plan['clock']['sourceId']})

    def runner(self):
        self.state = self.root / 'state'; self.state.mkdir(mode=0o700)
        lifecycle.atomic(self.state / calibration.CalibrationQualification.plan_name, self.plan)
        record = {'schema': calibration.SCHEMA, 'runId': self.plan['runId'], 'planSha256': self.plan_ref['sha256'],
            'target': self.plan['target'], 'attemptId': 'b' * 32, 'environmentId': 'c' * 32, 'status': 'prepared',
            'creationAttempted': False, 'started': None, 'stopping': None, 'executionDeadlineUtcMs': None,
            'stopDeadlineUtcMs': None, 'terminal': None, 'inFlight': None, 'commands': [], 'commandCount': 0,
            'resources': [], 'nodes': [], 'ownedData': None, 'cleanup': {'unverified': True, 'disposition': 'not-requested'}}
        lifecycle.atomic(self.state / calibration.CalibrationQualification.state_name, record)
        q = calibration.CalibrationQualification(self.state); self.addCleanup(q.close)
        return q

    def start(self, q):
        q.record.update(started=q.stamp(), status='ready', executionDeadlineUtcMs=self.now + 50400000)
        q.save()

    def test_complete_independent_plan_and_signed_reservations_do_not_need_finite_profile(self):
        loaded = calibration.load_plan(self.plan_ref['path'], self.plan_ref['sha256'])
        self.assertEqual(self.plan, loaded)
        self.assertEqual(self.receipts, calibration.reservations(loaded, self.now))
        calibration.current_authority(loaded, self.plan_ref['sha256'], 'launch', self.now)
        with self.assertRaises(ValueError):
            capacity.validate_bound(loaded, now_ms=self.now)

    def test_missing_inputs_refuse_prepare_before_process_or_state(self):
        target = self.root / 'uncreated'
        with patch.object(Qualification, 'run', side_effect=AssertionError('no process allowed')):
            with self.assertRaises(ValueError):
                calibration.prepare(self.plan_ref['path'], self.plan_ref['sha256'], target)
        self.assertFalse(target.exists())

    def test_missing_reservation_is_not_replaced_by_gross_capacity(self):
        self.plan['reservations'].pop('backup')
        with self.assertRaisesRegex(ValueError, 'reservation-missing'):
            calibration.reservations(self.plan, self.now)

    def test_net_cpu_memory_and_competing_commitments_are_enforced(self):
        for key, value in (('cpuMilliAvailable', 16250), ('memoryBytesAvailable', 31 * capacity.GIB), ('cpuMilliReserved', 16249)):
            with self.subTest(key=key):
                body = deepcopy(self.receipts['compute']); body[key] = value
                self.sign(self.plan['reservations']['compute']['path'], body)
                with self.assertRaisesRegex(ValueError, 'net-compute-insufficient'):
                    calibration.reservations(self.plan, self.now)

    def test_unsigned_observation_and_stale_reservation_refuse(self):
        path = self.plan['reservations']['compute']['path']
        lifecycle.atomic(path, self.receipts['compute'])
        with self.assertRaisesRegex(ValueError, 'signed-input'):
            calibration.reservations(self.plan, self.now)
        self.sign(path, {**self.receipts['compute'], 'observedUtcMs': self.now - 300001})
        with self.assertRaisesRegex(ValueError, 'reservation-stale'):
            calibration.reservations(self.plan, self.now)

    def test_backup_linux_failure_domain_and_original_backing_cannot_be_substituted(self):
        for key, value in (('linux', False), ('failureDomain', 'domain-primary'), ('poolId', 'pool-replacement'),
            ('filesystemId', 'filesystem-primary'), ('hostId', 'test-primary-host')):
            body = {**self.receipts['backup'], key: value}
            self.sign(self.plan['reservations']['backup']['path'], body)
            with self.subTest(key=key), self.assertRaises(ValueError):
                calibration.reservations(self.plan, self.now)

    def test_authority_wrong_plan_target_expiry_withdrawal_and_action_refuse(self):
        for key, value in (('schema', capacity.SCHEMA), ('planSha256', 'f' * 64), ('profileSha256', 'f' * 64),
            ('disposition', 'withdrawn'), ('expiresUtcMs', self.now), ('actions', ['prepare'])):
            self.sign(self.plan['authority']['statusPath'], {**self.status, key: value})
            with self.subTest(key=key), self.assertRaises(ValueError):
                calibration.current_authority(self.plan, self.plan_ref['sha256'], 'launch', self.now)

    def test_independent_clock_wrong_boot_and_stale_sample_refuse(self):
        for boot, tick in (('foreign', capacity._boottime()), (capacity._boot(), capacity._boottime() - 61000000000)):
            self.sign(self.plan['clock']['samplePath'], {'utcMs': self.now, 'boottimeNs': tick,
                'bootId': boot, 'sourceId': self.plan['clock']['sourceId']})
            with self.assertRaisesRegex(ValueError, 'independent-clock-stale'):
                lifecycle.clock(self.plan)

    def test_restart_original_execution_and_stop_budgets_never_reset(self):
        q = self.runner(); self.start(q)
        q.terminal('failed', 'unit-failure')
        original = deepcopy(q.record)
        q.close()
        q = calibration.CalibrationQualification(self.state); self.addCleanup(q.close)
        with patch.object(capacity, '_boottime', return_value=original['stopping']['boottimeNs'] + 300000000000), \
             patch.object(lifecycle.time, 'time', return_value=(original['stopping']['utcMs'] + 300000) / 1000):
            q.terminal('cancelled', 'different')
            self.assertEqual(original['stopping'], q.record['stopping'])
            self.assertAlmostEqual(600, q.remaining(True), places=2)
            self.assertLess(q.remaining(), 50400)

    def test_uncertain_workload_is_never_replayed(self):
        q = self.runner(); self.start(q)
        q.record['measurementStarted'] = q.stamp()
        with patch.object(q, 'admission', side_effect=AssertionError('no replay')):
            with self.assertRaisesRegex(ValueError, 'already-consumed'):
                q.monitor()

    def test_foreign_cleanup_and_owned_remnants_remain_unverified(self):
        q = self.runner(); self.start(q)
        q.record['nodes'] = [{'id': 'original', 'volumes': []}]
        with patch.object(q, 'engine'), patch.object(q, 'nodes', return_value=[{'id': 'foreign'}]), \
             patch.object(q, 'run', side_effect=AssertionError('no delete')):
            with self.assertRaisesRegex(ValueError, 'foreign-or-unrecorded'):
                q.stop(remove=True)
        self.assertTrue(q.record['cleanup']['unverified'])

    def test_failure_capture_precedes_owned_remove_and_keeps_first_failure(self):
        q = self.runner(); self.start(q)
        order = []
        with patch.object(q, 'monitor', side_effect=ValueError('unit-failure')), \
             patch.object(q, 'capture_failure', side_effect=lambda: order.append('capture')), \
             patch.object(q, 'stop', side_effect=lambda **kw: order.append(('remove', kw['remove']))):
            with self.assertRaises(ValueError):
                q.supervise('monitor')
        self.assertEqual(['capture', ('remove', True)], order)
        self.assertEqual('failed', q.record['terminal']['outcome'])

    def test_native_segment_validates_real_accounting_and_rejects_foreign_or_late_data(self):
        value = dict(runId='run-' + 'a' * 24, segmentId='writers-w1-s1', writer='pod-1',
            attempted=125, enqueued=125, acknowledged=125, persisted=125, transactionAcknowledgements=125,
            conflicted=0, dropped=0, rejected=0, startedUtcMs=1000, emitFinishedUtcMs=2000, acknowledgedUtcMs=2400,
            recordIds=[str(i).zfill(26) for i in range(125)])
        self.assertEqual((1000, 2000), workload.validate_segment(value, value['runId'], value['segmentId'], value['writer'])[:2])
        for key, replacement in (('persisted', 124), ('conflicted', 1), ('recordIds', ['x'] * 125),
            ('runId', 'foreign'), ('emitFinishedUtcMs', 2300), ('acknowledgedUtcMs', 1999)):
            with self.subTest(key=key), self.assertRaises(ValueError):
                workload.validate_segment({**value, key: replacement}, value['runId'], value['segmentId'], value['writer'])

    def test_native_writer_uses_emit_then_ack_and_retains_private_segment_proof(self):
        directory = self.root / 'writer'; directory.mkdir(mode=0o700)
        writer = workload.Writer(directory); self.addCleanup(writer.close)
        config = {'pods': ['pod-1'], 'segments': 1, 'lane': 'backlog', 'runId': 'run-' + 'a' * 24}
        phases = []
        def invoke(config, pod, segment, emitted, phase):
            phases.append(phase)
            return dict(runId=config['runId'], segmentId=segment, writer=pod, attempted=125, enqueued=125,
                acknowledged=125, persisted=125, transactionAcknowledgements=125, conflicted=0, dropped=0,
                rejected=0, startedUtcMs=1000, emitFinishedUtcMs=2000, acknowledgedUtcMs=2400, finishedUtcMs=2010,
                recordIds=[str(i).zfill(26) for i in range(125)])
        import threading
        with patch.object(writer, 'invoke', side_effect=invoke), patch.object(workload.time, 'monotonic', return_value=1):
            result = writer.execute(config, 0, threading.Barrier(1))
        self.assertEqual(['emit', 'ack'], phases)
        self.assertEqual(125, result['acceptedRecords'])
        proof = directory / 'segments.jsonl'
        self.assertEqual(0o600, proof.stat().st_mode & 0o777)
        self.assertNotIn('recordIds', proof.read_text())

    def test_full_measurement_sequence_preserves_separate_lane_and_native_rewrite_before_remove(self):
        q = self.runner(); self.start(q)
        q.record['kubeconfigSha256'] = 'a' * 64
        q.record['physicalTarget'] = {'clusterUid': 'unit-cluster'}
        q.plan['environment']['leaseHolders'] = ['calibration/owner-6775094/' + q.record['runId']]
        calls = []
        def kube(*args, **kwargs):
            calls.append(args)
            return subprocess.CompletedProcess([], 0, json.dumps({'items': [{'metadata': {'name': p}} for p in ['pod-1', 'pod-2']]}), '')
        def lane(name, pods):
            calls.append((name, tuple(pods)))
            return {'lane': name, 'acceptedRecords': 150000 if name == 'backlog' else 450250,
                'commonOverlapMilliseconds': 1800000, 'retries': 0}
        with patch.object(q, 'admission'), patch.object(q, 'enable'), patch.object(q, 'kube', side_effect=kube), \
             patch.object(q, 'physical', side_effect=[{'recordCount': n} for n in (0, 150000, 600250, 600250)]), \
             patch.object(q, 'operation_count', side_effect=[{'one': 0, 'two': 0}, {'one': 450125, 'two': 450125}]), \
             patch.object(q, 'lane', side_effect=lane), patch.object(q, 'measurement_quiesce', return_value={'state': 'verified'}), \
             patch.object(q, 'stop', side_effect=lambda **kw: calls.append(('remove', kw['remove']))):
            q.monitor()
        result = json.loads((self.state / 'calibration-result.json').read_bytes())
        self.assertEqual(900250, result['componentOperations'])
        self.assertFalse(result['qualified']); self.assertFalse(result['C1Credit']); self.assertFalse(result['C2Credit'])
        self.assertIn(('backlog', ('pod-1',)), calls)
        self.assertIn(('writers', ('pod-1', 'pod-2')), calls)
        self.assertTrue(any('VACUUM (FULL, ANALYZE) access_telemetry.lifecycle_state;' in args for args in calls))
        self.assertEqual(('remove', True), calls[-1])
        self.assertEqual('completed', q.record['terminal']['outcome'])

    def test_multisegment_transport_overhead_does_not_accumulate_dispatch_drift(self):
        import threading
        directory = self.root / 'writer'; directory.mkdir(mode=0o700)
        writer = workload.Writer(directory); self.addCleanup(writer.close)
        config = {'pods': ['pod-1'], 'segments': 4, 'lane': 'writers', 'runId': 'run-' + 'a' * 24}
        dispatched = []; lock = threading.Lock()
        def invoke(config, pod, segment, emitted, phase):
            if phase == 'emit':
                with lock:
                    dispatched.append(time.monotonic())
                time.sleep(1.12)  # One-second emission plus real native overhead.
            else:
                time.sleep(.025)
            start = emitted
            return dict(runId=config['runId'], segmentId=segment, writer=pod, attempted=125, enqueued=125,
                acknowledged=125, persisted=125, transactionAcknowledgements=125, conflicted=0, dropped=0,
                rejected=0, startedUtcMs=start, emitFinishedUtcMs=start + 1000, finishedUtcMs=start + 1000,
                acknowledgedUtcMs=start + 1750, recordIds=[str(i).zfill(26) for i in range(125)])
        with patch.object(writer, 'invoke', side_effect=invoke):
            result = writer.execute(config, 0, threading.Barrier(1))
        self.assertEqual(4, len(dispatched))
        self.assertLess(max(abs((tick - dispatched[0]) - i) for i, tick in enumerate(dispatched)), .250)
        self.assertEqual(1750, result['latencyP99Ms'])
        self.assertEqual(500, result['acceptedRecords'])
        proofs = [json.loads(line) for line in (directory / 'segments.jsonl').read_text().splitlines()]
        self.assertTrue(all(p['acknowledgedUtcMs'] - p['startedUtcMs'] == 1750 for p in proofs))

    def test_first_transport_error_cancels_work_before_next_dispatch(self):
        import threading
        directory = self.root / 'writer'; directory.mkdir(mode=0o700)
        writer = workload.Writer(directory); self.addCleanup(writer.close)
        config = {'pods': ['pod-1'], 'segments': 4, 'lane': 'writers', 'runId': 'run-' + 'a' * 24}
        calls = []
        original = RuntimeError('first-native-error')
        def invoke(*args):
            calls.append(args[-1]); time.sleep(.05); raise original
        with patch.object(writer, 'invoke', side_effect=invoke), self.assertRaises(RuntimeError) as caught:
            writer.execute(config, 0, threading.Barrier(1))
        self.assertIs(original, caught.exception)
        self.assertEqual(['emit'], calls)
        self.assertTrue(writer._cancel.is_set())

    def test_reservation_identity_time_and_withdrawal_fields_are_required(self):
        original = deepcopy(self.receipts['compute'])
        for changes in ({'reservationId': ''}, {'owner': 'foreign'}, {'validFromUtcMs': self.now + 1},
            {'validUntilUtcMs': self.now}, {'disposition': 'withdrawn'}, {'revokedUtcMs': self.now}):
            with self.subTest(changes=changes):
                self.sign(self.plan['reservations']['compute']['path'], {**original, **changes})
                with self.assertRaises(ValueError):
                    calibration.reservations(self.plan, self.now)
        for key in ('reservationId', 'owner', 'validFromUtcMs', 'disposition', 'revokedUtcMs'):
            body = deepcopy(original); body.pop(key)
            self.sign(self.plan['reservations']['compute']['path'], body)
            with self.subTest(missing=key), self.assertRaises(ValueError):
                calibration.reservations(self.plan, self.now)

    def test_allocation_headroom_violation_is_retained_before_refusal(self):
        q = self.runner(); self.start(q)
        q.record['physicalTarget'] = {'clusterUid': 'unit'}
        q.plan['environment']['monitoring'] = {'limits': {}}
        allocation = q.plan['allocations']['postgres']
        for free in (-1, 0, (allocation - 1) // 5):
            value = {'phase': 'observed', 'target': q.record['physicalTarget'], 'expectedFaults': [],
                'allocations': {'postgres': {'usedBytes': allocation - free, 'availableBytes': free}}, 'series': {'postgres.walBytes': 0},
                '_receiptSha256': 'c' * 64, 'observedUtcMs': self.now}
            with patch.object(lifecycle, 'clock', return_value=self.now), patch.object(lifecycle, 'monitor_receipt', return_value=value):
                with self.assertRaisesRegex(ValueError, 'headroom'):
                    q.check_storage()
            retained = json.loads(q.state_path.read_bytes())['storageObservation']
            self.assertEqual(free, retained['allocations']['postgres']['availableBytes'])
            self.assertEqual('c' * 64, retained['receiptSha256'])
        free = (allocation + 4) // 5
        value['allocations']['postgres'] = {'usedBytes': allocation - free, 'availableBytes': free}
        with patch.object(lifecycle, 'clock', return_value=self.now), patch.object(lifecycle, 'monitor_receipt', return_value=value):
            q.check_storage()

    def inherited_observation_fixture(self):
        from test_campaign_lifecycle import CampaignReviewRegressionTests
        fixture = CampaignReviewRegressionTests()
        fixture.setUp(); self.addCleanup(fixture.doCleanups)
        q = fixture.observation_fixture()
        q.__class__ = calibration.CalibrationQualification
        q.record['phases'] = q.phases(None)
        # Prepared Kubernetes input has no server-assigned UID; live readbacks do.
        for obj in q.record['admittedResources'].values():
            obj['metadata'].pop('uid', None)
        return fixture, q

    def test_inherited_observation_accepts_only_the_suspended_independent_reporter(self):
        fixture, q = self.inherited_observation_fixture()
        self.assertEqual({'c3': [0, 0]}, q.record['phases'])
        self.assertEqual('ready', q.observe(startup=True))
        reporter = next(o for o in fixture.objects if o['kind'] == 'Job')
        reporter['spec']['suspend'] = False
        with self.assertRaisesRegex(ValueError, 'reporter-outside-source-window'):
            q.observe(startup=True)

    def test_inherited_quiescence_settles_without_starting_cleanup_and_rewrite_guard_keeps_zero_replicas(self):
        fixture, q = self.inherited_observation_fixture()
        server = next(o for o in fixture.objects if o['kind'] == 'Deployment' and o['metadata']['name'] == 'memories')
        replica = deepcopy(server); replica['kind'] = 'ReplicaSet'
        replica['metadata'].update(name='server-replicas', uid='server-replicas', ownerReferences=[{
            'kind': 'Deployment', 'name': server['metadata']['name'], 'uid': server['metadata']['uid'], 'controller': True}])
        pod = fixture.pod(replica, 'old-server-pod')
        fixture.objects.extend([replica, pod])
        q.observe(startup=True)
        started, execution_end = deepcopy(q.record['started']), q.record['executionDeadlineUtcMs']
        native = q.kube; polls = []
        def kube(*args, **kwargs):
            if args[:2] == ('get', '--raw'):
                return subprocess.CompletedProcess([], 0, json.dumps({'items': [{'metadata': {
                    'name': 'access-telemetry-postgresql-0', 'namespace': fixture.ns}, 'timestamp': lifecycle.utc(fixture.now),
                    'containers': [{'name': 'postgresql', 'usage': {'cpu': '100m', 'memory': '1Gi'}}]}]}), '')
            if 'psql' in args:
                return subprocess.CompletedProcess([], 0, json.dumps(dict(databaseBytes=100, heapToastBytes=50, indexBytes=20,
                    walBytes=20, recordCount=10, relations=[{'name': 'lifecycle_state'}])), '')
            if args[:2] == ('get', 'deployments,replicasets,pods') and getattr(q, '_measurement_quiescing', False):
                polls.append(True)
                if len(polls) > 1 and pod in fixture.objects:
                    fixture.objects.remove(pod)
            return native(*args, **kwargs)
        with patch.object(q, 'admission', return_value=(None, None, fixture.now)), patch.object(q, 'kube', side_effect=kube), \
             patch.object(lifecycle, 'clock', return_value=fixture.now):
            proof = q.measurement_quiesce()
            self.assertGreaterEqual(len(polls), 2)
            self.assertEqual('verified', proof['state'])
            self.assertIsNone(q.record['stopping']); self.assertIsNone(q.record['stopDeadlineUtcMs'])
            self.assertEqual(started, q.record['started']); self.assertEqual(execution_end, q.record['executionDeadlineUtcMs'])
            self.assertIn(lifecycle.resource_id(pod, fixture.ns), q.record['measurementQuiescence']['retiredPodIds'])
            q.record['inFlight'] = {'commandSha256': 'd' * 64}
            with patch.object(q, 'enable', side_effect=AssertionError('must remain closed')):
                q.guard()  # Real inherited observe, normalization and native sample.
            self.assertEqual({'commandSha256': 'd' * 64}, q.record['inFlight'])
            self.assertEqual(1, q.record['nativeReadbacks']['sampleCount'])
            self.assertTrue(all(o['spec']['replicas'] == 0 for o in fixture.objects if o['kind'] == 'Deployment'))
            server['spec']['replicas'] = 1
            with self.assertRaisesRegex(ValueError, 'quiesced-workload'):
                q.guard()

    def test_shortened_or_same_pod_writers_are_refused_before_command(self):
        config = dict(kubectl=self.tools['kubectl'], kubeconfig='/never', kubeconfigSha256='a' * 64,
            context='never', namespace='never', plan={'path': '/never', 'sha256': 'a' * 64}, runId='run-' + 'a' * 24, lane='writers', pods=['same', 'same'], segments=1801)
        with patch.object(Qualification, 'run', side_effect=AssertionError('no execution')):
            with self.assertRaises(ValueError):
                workload.execute(config, self.root)
            config.update(pods=['one', 'two'], segments=1800)
            with self.assertRaisesRegex(ValueError, 'workload-shrunk'):
                workload.execute(config, self.root)

    def test_real_chart_has_separate_calibration_identity_and_preserves_security_and_limits(self):
        from test_chart import fixture_values, graph, render, documents
        values = capacity.configure_application(fixture_values(), graph())
        values['capacityProfile'] = calibration.PROFILE_ID
        result = render(values)
        self.assertEqual(0, result.returncode, result.stderr)
        deployments = [d for d in documents(result.stdout) if d['kind'] == 'Deployment']
        self.assertEqual(9, sum(d['spec']['replicas'] for d in deployments))
        for obj in deployments:
            container = obj['spec']['template']['spec']['containers'][0]
            self.assertEqual('400m', container['resources']['limits']['cpu'])
            self.assertFalse(container['securityContext']['allowPrivilegeEscalation'])
            if obj['metadata']['name'] in {'memories', 'memories-access-telemetry', 'memories-access-telemetry-clock'}:
                env = {v['name']: v for v in container['env']}
                self.assertEqual(calibration.PROFILE_ID, env['AccessTelemetryQualification__ProfileId']['value'])
                self.assertIn('valueFrom', env['AccessTelemetryQualification__IndependentCalibrationRunId'])
                self.assertIn('valueFrom', env['AccessTelemetryQualification__IndependentCalibrationPlanHash'])
        values['bindings']['memories/server']['replicas'] = 1
        self.assertNotEqual(0, render(values).returncode)

    def test_launch_supervises_measurements_without_returning_ready_to_caller(self):
        q = self.runner()
        order = []
        with patch.object(lifecycle.CampaignQualification, 'launch', side_effect=lambda: order.append('startup')), \
             patch.object(q, 'monitor', side_effect=lambda: order.append('measurements')):
            q.launch()
        self.assertEqual(['startup', 'measurements'], order)

    def test_wal_bound_violation_preserves_receipt_before_refusal(self):
        q = self.runner(); self.start(q)
        q.record['physicalTarget'] = {'clusterUid': 'unit'}
        q.plan['environment']['monitoring'] = {'limits': {}}
        value = {'phase': 'observed', 'target': q.record['physicalTarget'], 'expectedFaults': [],
            'allocations': {}, 'series': {'postgres.walBytes': capacity.BOUNDS['walPeakBytes'] + 1},
            '_receiptSha256': 'c' * 64, 'observedUtcMs': self.now}
        with patch.object(lifecycle, 'clock', return_value=self.now), patch.object(lifecycle, 'monitor_receipt', return_value=value):
            with self.assertRaisesRegex(ValueError, 'wal-bound'):
                q.check_storage()
        self.assertEqual('c' * 64, json.loads(q.state_path.read_bytes())['storageObservation']['receiptSha256'])

    def test_successful_launch_reports_ready_without_claiming_cleanup(self):
        from contextlib import redirect_stdout
        from io import StringIO
        from unittest.mock import Mock
        q = Mock()
        q.record = {'runId': 'a' * 32, 'status': 'ready', 'terminal': None,
            'planSha256': 'b' * 64, 'cleanup': {'unverified': True, 'disposition': 'not-requested'}}
        with patch.object(calibration, 'CalibrationQualification', return_value=q), redirect_stdout(StringIO()) as output:
            result = calibration.main(['launch', '--state', str(self.root / 'unit-only-state'),
                '--approved-plan-sha256', 'b' * 64])
        self.assertEqual(0, result)
        q.supervise.assert_called_once_with('launch')
        self.assertEqual('ready', json.loads(output.getvalue())['status'])
        self.assertTrue(json.loads(output.getvalue())['cleanup']['unverified'])
        q.close.assert_called_once()

    def test_cli_missing_plan_and_mixed_campaign_inputs_never_fall_into_fixture(self):
        for arguments in (['--calibration', 'prepare', '--calibration-state', str(self.root / 'uncreated')],
            ['--calibration', 'launch', '--campaign', 'launch', '--calibration-state', str(self.root / 'uncreated')]):
            result = subprocess.run([sys.executable, '-B', str(HERE / 'qualify.py'), *arguments], capture_output=True, text=True, timeout=5)
            self.assertNotEqual(0, result.returncode)
            self.assertFalse((self.root / 'uncreated').exists())

    def test_real_bounded_child_heartbeat_failure_terminates_descendants_inherited_group(self):
        q = self.runner()
        marker = self.root / 'descendant-pid'
        script = self.root / 'child.py'
        script.write_text("import subprocess,sys,time\np=subprocess.Popen([sys.executable,'-c','import time; time.sleep(120)'])\nopen(sys.argv[1],'w').write(str(p.pid))\ntime.sleep(120)\n")
        def heartbeat():
            if marker.exists():
                raise InterruptedError('unit-withdrawal')
        # The real process tree, pipe draining and reaping are exercised locally.
        with self.assertRaises(InterruptedError):
            Qualification.run(q, [sys.executable, str(script), str(marker)], timeout=10, heartbeat=heartbeat, sensitive=True)
        pid = int(marker.read_text())
        with self.assertRaises(ProcessLookupError):
            os.kill(pid, 0)


if __name__ == '__main__':
    unittest.main()
