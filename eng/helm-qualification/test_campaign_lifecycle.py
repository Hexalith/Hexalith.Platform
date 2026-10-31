"""Offline process/filesystem doubles; no infrastructure or qualification evidence."""
from copy import deepcopy
import hashlib
import hmac
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

import campaign_capacity as capacity
import campaign_environment as environment
import campaign_lifecycle as lifecycle
from test_campaign_capacity import bound_fixture
from qualify import Qualification

BUILDS = next(root for root in (lifecycle.ROOT.parent / 'Hexalith.Builds', lifecycle.ROOT / 'references/Hexalith.Builds')
              if (root / 'Props/Directory.Packages.props').is_file())
CLI = BUILDS / 'src/libraries/Hexalith.Builds.Module.Cli/bin/Debug/net10.0/Hexalith.Builds.Module.Cli.dll'


class CampaignLifecycleTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix='campaign-offline-')
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name); self.root.chmod(0o700)
        self.state = self.root / 'state'; self.state.mkdir(mode=0o700)
        self.now = int(time.time() * 1000)
        self.plan = {'schema': lifecycle.SCHEMA, 'runId': '1' * 32,
            'tools': {name: {'path': '/usr/bin/false', 'sha256': hashlib.sha256(Path('/usr/bin/false').read_bytes()).hexdigest()} for name in ('helm', 'docker', 'kind', 'kubectl', 'dotnet', 'buildsCli')}, 'docker': {'host': 'unix:///never-connect.sock', 'engineId': 'fixture-engine'},
            'paths': {'primary': str(self.root), 'backup': str(self.root / 'backup')},
            'profileSha256': 'a' * 64, 'environment': {'faultWindows': []},
            'window': {'expiresUtcMs': self.now + 100000}}
        self.write_state()

    def write_state(self):
        lifecycle.atomic(self.state / 'campaign-plan.json', self.plan)
        self.record = {'schema': lifecycle.SCHEMA, 'runId': self.plan['runId'], 'attemptId': '2' * 32, 'environmentId': '3' * 32,
            'planSha256': capacity.digest(self.plan), 'target': {'clusterName': 'unit-owned', 'context': 'kind-unit-owned', 'namespace': 'unit-qualification'},
            'status': 'prepared', 'creationAttempted': False, 'started': None, 'stopping': None,
            'executionDeadlineUtcMs': None, 'stopDeadlineUtcMs': None, 'terminal': None, 'inFlight': None,
            'nodes': [], 'resources': [], 'commands': [], 'commandCount': 0,
            'cleanup': {'disposition': 'not-requested', 'unverified': True}, 'ownedData': None}
        lifecycle.atomic(self.state / 'campaign-state.json', self.record)

    def runner(self):
        q = lifecycle.CampaignQualification(self.state)
        self.addCleanup(q.close)
        return q

    def started(self, q):
        q.record.update(started=q.stamp(), status='ready', executionDeadlineUtcMs=self.now + 100000)
        q.save()

    def test_prepare_missing_prerequisites_refuses_before_any_command_or_state(self):
        proposed = self.root / 'proposal.json'; lifecycle.atomic(proposed, self.plan)
        target = self.root / 'fresh'
        with patch.object(Qualification, 'run', side_effect=AssertionError('no command permitted')):
            with self.assertRaises(ValueError):
                lifecycle.prepare(proposed, capacity.digest(self.plan), target)
        self.assertFalse(target.exists())

    def test_real_builds_reader_accepts_generated_pending_and_ready_descriptors(self):
        self.assertTrue(CLI.is_file(), 'Build the source/Debug Builds CLI for this offline contract check')
        q = self.runner()
        for ready in (False, True):
            if ready:
                self.started(q)
            document = q.descriptor('ready' if ready else 'pending', [{'id': 'StatefulSet/unit/postgres', 'owner': 'Hexalith.Platform'}] if ready else [])
            file = self.root / 'descriptor.json'; lifecycle.atomic(file, document)
            result = subprocess.run(['dotnet', str(CLI), 'validate', '--environment', str(file), '--output', 'json'], capture_output=True, text=True, timeout=30)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertEqual('validated', json.loads(result.stdout)['status'])
            self.assertEqual({'state': 'not-yet-populated'}, document['bindings']['candidate'])

    def test_real_reader_refuses_owner_mismatch_and_invalid_deadline_arithmetic(self):
        q = self.runner(); self.started(q)
        for mutate in (lambda d: d['bindings']['run'].update(ownerRunId='foreign'),
                       lambda d: d['bindings']['deadlines']['startup'].update(expiresAt=lifecycle.utc(self.now + 700000))):
            document = q.descriptor(); mutate(document)
            file = self.root / 'descriptor.json'; lifecycle.atomic(file, document)
            result = subprocess.run(['dotnet', str(CLI), 'validate', '--environment', str(file), '--output', 'json'], capture_output=True, text=True, timeout=30)
            self.assertNotEqual(0, result.returncode)
            self.assertIn(json.loads(result.stdout)['outcome']['ruleId'], ('HXT104', 'HXT107'))

    def test_restart_preserves_first_outcome_and_stopping_budget(self):
        q = self.runner(); self.started(q)
        q.terminal('failed', 'environment-failure')
        first = deepcopy(q.record); q.close()
        replacement = self.runner()
        with patch.object(lifecycle.time, 'time', return_value=(first['stopping']['utcMs'] + 300000) / 1000), \
             patch.object(capacity, '_boottime', return_value=first['stopping']['boottimeNs'] + 300_000_000_000):
            replacement.terminal('cancelled', 'run-cancelled')
            self.assertEqual(first['terminal'], replacement.record['terminal'])
            self.assertEqual(first['stopping'], replacement.record['stopping'])
            self.assertAlmostEqual(600, replacement.remaining(True), places=2)

    def test_uncertain_command_cannot_be_replayed_after_restart(self):
        q = self.runner(); q.record['inFlight'] = {'commandSha256': 'c' * 64}; q.save(); q.close()
        q = self.runner()
        with patch.object(Qualification, 'run', side_effect=AssertionError('must not replay')):
            with self.assertRaisesRegex(ValueError, 'uncertain-command'):
                q.run(['forbidden'])
        self.assertIsNotNone(q.record['inFlight'])

    def test_foreign_node_blocks_removal_and_remains_unverified(self):
        q = self.runner(); self.started(q)
        q.record['nodes'] = [{'id': 'original', 'volumes': []}]
        with patch.object(q, 'engine'), patch.object(q, 'nodes', return_value=[{'id': 'foreign'}]), \
             patch.object(q, 'run', side_effect=AssertionError('must not delete')):
            with self.assertRaisesRegex(ValueError, 'foreign-or-unrecorded'):
                q.stop(remove=True)
        self.assertTrue(q.record['cleanup']['unverified'])
        self.assertEqual('incomplete', q.record['cleanup']['disposition'])

    def test_owned_cleanup_uses_original_state_after_configuration_drift(self):
        q = self.runner(); self.started(q)
        q.record['nodes'] = [{'id': 'original', 'volumes': []}]
        calls = []
        with patch.object(lifecycle, 'admit', side_effect=AssertionError('cleanup must not reopen changed profile')), \
             patch.object(q, 'quiesce', return_value={'state': 'verified'}), patch.object(q, 'engine'), patch.object(q, 'owned_nodes', return_value=[]), patch.object(q, 'nodes', side_effect=[[{'id': 'original'}], []]), \
             patch.object(q, 'run', side_effect=lambda args, **kw: calls.append(args)):
            q.stop(remove=True)
        self.assertEqual('removed', q.record['status'])
        self.assertEqual('kind', calls[0][0])
        self.assertIn('unit-owned', calls[0])
        self.assertFalse(q.record['cleanup']['unverified'])

    def test_owned_remnant_never_reports_removed(self):
        q = self.runner(); self.started(q)
        q.record['nodes'] = [{'id': 'original', 'volumes': []}]
        with patch.object(q, 'quiesce', return_value={'state': 'verified'}), patch.object(q, 'engine'), patch.object(q, 'owned_nodes', return_value=[]), patch.object(q, 'nodes', return_value=[{'id': 'original', 'running': True}]), patch.object(q, 'run'):
            with self.assertRaisesRegex(ValueError, 'owned-remnants'):
                q.stop(remove=True)
        self.assertNotEqual('removed', q.record['status'])
        self.assertTrue(q.record['cleanup']['unverified'])

    def test_changed_kind_label_cannot_hide_original_owned_remnant(self):
        q = self.runner(); self.started(q)
        q.record['nodes'] = [{'id': 'original', 'volumes': []}]
        with patch.object(q, 'quiesce', return_value={'state': 'verified'}), patch.object(q, 'engine'), patch.object(q, 'nodes', return_value=[]), \
             patch.object(q, 'owned_nodes', return_value=[{'id': 'original', 'running': True}]):
            with self.assertRaisesRegex(ValueError, 'owned-remnants'):
                q.stop(remove=True)
        self.assertTrue(q.record['cleanup']['unverified'])

    def test_startup_cancellation_prevents_next_command(self):
        q = self.runner(); self.started(q); q.record['status'] = 'starting'
        lifecycle.atomic(self.state / 'stop-request.json', {'action': 'stop', 'requested': q.stamp()})
        with patch.object(Qualification, 'run', side_effect=AssertionError('no startup mutation')):
            with self.assertRaises(InterruptedError):
                q.run(['kind', 'create', 'cluster'])

    def test_expired_stopping_budget_never_resets_for_retry(self):
        q = self.runner(); self.started(q); q.terminal('failed', 'environment-failure')
        origin = deepcopy(q.record['stopping'])
        with patch.object(capacity, '_boottime', return_value=origin['boottimeNs'] + 901_000_000_000), \
             patch.object(q, 'engine', side_effect=AssertionError('no late mutation')):
            with self.assertRaisesRegex(ValueError, 'stopping-budget-exhausted'):
                q.stop(remove=True)
        self.assertEqual(origin, q.record['stopping'])

    def test_cancel_request_and_missing_authority_stop_monitoring(self):
        q = self.runner(); self.started(q)
        lifecycle.atomic(self.state / 'stop-request.json', {'action': 'stop', 'requested': q.stamp()})
        with patch.object(q, 'observe', side_effect=AssertionError('no observation after cancellation')):
            with self.assertRaises(InterruptedError):
                q.monitor(maximum_polls=1)
        (self.state / 'stop-request.json').unlink()
        with patch.object(lifecycle, 'admit', side_effect=ValueError('withdrawn')), patch.object(q, 'observe', side_effect=AssertionError('no observation after withdrawal')):
            with self.assertRaisesRegex(ValueError, 'withdrawn'):
                q.monitor(maximum_polls=1)

    def test_sensitive_command_evidence_excludes_stdout_stderr_stdin(self):
        q = self.runner()
        result = q.run([sys.executable, '-c', 'import sys; print(sys.stdin.read()); print("credential-marker", file=sys.stderr)'], data='secret-marker')
        self.assertIn('secret-marker', result.stdout)
        persisted = b''.join(p.read_bytes() for p in self.state.iterdir() if p.is_file())
        self.assertNotIn(b'secret-marker', persisted)
        self.assertNotIn(b'credential-marker', persisted)
        self.assertEqual(0o600, (self.state / 'campaign-state.json').stat().st_mode & 0o777)

    def test_output_overflow_and_child_timeout_remain_uncertain(self):
        for args, kwargs in (([sys.executable, '-c', 'print("x"*100000)'], {'maximum_output_bytes': 1000}),
                             ([sys.executable, '-c', 'import time; time.sleep(20)'], {'timeout': 0.1})):
            q = self.runner()
            with self.assertRaisesRegex(RuntimeError, 'bounded command'):
                q.run(args, **kwargs)
            self.assertIsNotNone(q.record['inFlight']); q.close()
            self.write_state()

    def test_fault_window_is_exact_resource_and_does_not_extend_on_restart(self):
        q = self.runner(); self.started(q)
        q.record['resources'] = [{'id': 'StatefulSet/dapr-system/dapr-scheduler-server'}]
        self.plan['environment']['faultWindows'] = [{'resourceId': 'StatefulSet/dapr-system/dapr-scheduler-server', 'startSeconds': 0, 'endSeconds': 10}]
        q.plan = self.plan
        self.assertTrue(q.expected_fault('StatefulSet/dapr-system/dapr-scheduler-server'))
        self.assertFalse(q.expected_fault('StatefulSet/dapr-system/dapr-placement-server'))
        with patch.object(capacity, '_boottime', return_value=q.record['started']['boottimeNs'] + 11_000_000_000):
            self.assertFalse(q.expected_fault('StatefulSet/dapr-system/dapr-scheduler-server'))

    def test_supplemental_reporter_and_network_share_finite_control_allocation(self):
        def deployment(cpu, memory):
            return {'kind': 'Deployment', 'spec': {'replicas': 1, 'template': {'spec': {'containers': [
                {'resources': {'limits': {'cpu': cpu, 'memory': memory}}}]}}}}
        environment.supplemental_budget([deployment('200m', '512Mi'), deployment('100m', '512Mi')])
        with self.assertRaisesRegex(ValueError, 'control-envelope'):
            environment.supplemental_budget([deployment('201m', '512Mi'), deployment('100m', '512Mi')])
        with self.assertRaisesRegex(ValueError, 'limit'):
            environment.supplemental_budget([deployment('100m', '')])

    def test_independent_clock_and_authority_signatures_expiry_and_withdrawal(self):
        key = b'unit-test-key-not-an-operational-key'; key_path = self.root / 'key'; lifecycle.atomic(key_path, key)
        key_ref = {'path': str(key_path), 'sha256': hashlib.sha256(key).hexdigest()}
        sample_path = self.root / 'clock.json'; status_path = self.root / 'status.json'
        self.plan['clock'] = {'samplePath': str(sample_path), 'key': key_ref, 'sourceId': 'outside-clock'}
        self.plan['authority'] = {'statusPath': str(status_path), 'key': key_ref, 'issuer': 'outside-owner'}
        def signed(path, value):
            lifecycle.atomic(path, {**value, 'signature': hmac.new(key, capacity.canonical(value), hashlib.sha256).hexdigest()})
        sample = {'utcMs': self.now, 'boottimeNs': capacity._boottime(), 'bootId': capacity._boot(), 'sourceId': 'outside-clock'}
        signed(sample_path, sample)
        self.assertGreaterEqual(lifecycle.clock(self.plan), self.now)
        status = {'issuer': 'outside-owner', 'owner': 'github:user:6775094', 'runId': self.plan['runId'], 'planSha256': 'a' * 64,
                  'observedUtcMs': self.now, 'expiresUtcMs': self.now + 100000, 'disposition': 'active', 'actions': ['launch'], 'capacityReceipt': {}}
        signed(status_path, status)
        self.assertEqual(status, lifecycle.authority(self.plan, 'a' * 64, 'launch', self.now))
        for changes in ({'disposition': 'withdrawn'}, {'runId': 'foreign'}, {'expiresUtcMs': self.now}, {'observedUtcMs': self.now - 300001}):
            signed(status_path, {**status, **changes})
            with self.assertRaises(ValueError): lifecycle.authority(self.plan, 'a' * 64, 'launch', self.now)
        signed(sample_path, {**sample, 'bootId': 'foreign'})
        with self.assertRaises(ValueError): lifecycle.clock(self.plan)


class CampaignReviewRegressionTests(unittest.TestCase):
    setUp = CampaignLifecycleTests.setUp
    write_state = CampaignLifecycleTests.write_state
    runner = CampaignLifecycleTests.runner
    started = CampaignLifecycleTests.started

    def observation_fixture(self):
        q = self.runner(); self.started(q)
        ns = q.record['target']['namespace']; self.ns = ns
        q.plan['environment'].update(daprImages={'daprd': 'ghcr.io/dapr/daprd@sha256:' + '1' * 64},
            leaseHolders=['story-27-4/owner-6775094/' + '4' * 32])
        q.record.update(physicalTarget={'apiServer': 'https://127.0.0.1:1234', 'apiCaSha256': 'b' * 64, 'clusterUid': 'cluster', 'namespaceUid': 'namespace'},
            runtimeBinding={'data': {'capacityProfileSha256': 'c' * 64}}, phases={'c3': [0, 10000]}, controls={'namespaceUid': 'namespace', 'gateUid': 'gate', 'leaseUid': 'lease'})
        def controller(kind, name, replicas, namespace=ns):
            obj = {'apiVersion': 'apps/v1', 'kind': kind, 'metadata': {'name': name, 'namespace': namespace, 'uid': 'uid-' + name, 'generation': 2, 'resourceVersion': '1'},
                'spec': {'replicas': replicas, 'template': {'metadata': {'labels': {'app': name}}, 'spec': {
                    'serviceAccountName': name, 'automountServiceAccountToken': False, 'containers': [{'name': {'memories-access-telemetry': 'lifecycle', 'memories-access-telemetry-clock': 'clock', 'access-telemetry-postgresql': 'postgresql', lifecycle.REPORTER: 'reporter'}.get(name, name), 'image': 'registry.example/' + name + '@sha256:' + '1' * 64,
                    'env': [{'name': 'SETTING', 'value': 'bound'}], 'resources': {'limits': {'cpu': '100m', 'memory': '128Mi'}}}]}}},
                'status': {'observedGeneration': 2, 'replicas': replicas, 'readyReplicas': replicas}}
            return obj
        objects = [controller('Deployment', 'memories', 2), controller('Deployment', 'memories-access-telemetry', 2),
                   controller('Deployment', 'memories-access-telemetry-clock', 1), controller('StatefulSet', 'access-telemetry-postgresql', 1),
                   controller('StatefulSet', 'dapr-scheduler-server', 3, 'dapr-system')]
        job = controller('Job', lifecycle.REPORTER, 1)
        job['spec'].pop('replicas'); job['spec'].update(suspend=True, backoffLimit=0)
        job['status'] = {}; objects.append(job)
        q.record['admittedResources'] = {lifecycle.resource_id(o, ns): deepcopy(o) for o in objects}
        for o in q.record['admittedResources'].values():
            o.pop('status', None)
        for obj in objects:
            if obj['metadata']['name'] in lifecycle.CONTROLLED_WRITERS:
                obj['spec']['replicas'] = 0; obj['status'].update(readyReplicas=0, replicas=0)
        self.objects = objects
        self.gate = {'kind': 'ConfigMap', 'metadata': {'name': lifecycle.GATE, 'namespace': ns, 'uid': 'gate', 'resourceVersion': '1'},
            'data': {'gate.json': json.dumps({'schemaVersion': 1, 'state': 'disabled', 'profileSha256': q.plan['profileSha256'], 'expiresUtcMs': 0})}}
        self.lease = {'kind': 'Lease', 'metadata': {'name': lifecycle.LEASE, 'namespace': ns, 'uid': 'lease', 'resourceVersion': '1'}, 'spec': {'holderIdentity': '', 'leaseDurationSeconds': 0}}
        self.calls = []
        def result(value): return subprocess.CompletedProcess([], 0, json.dumps(value), '')
        def kube(*args, **kwargs):
            self.calls.append((args, kwargs))
            if args[:2] == ('get', 'namespace'):
                return result({'metadata': {'uid': 'namespace'}})
            if args[:3] == ('get', 'configmap', lifecycle.GATE): return result(self.gate)
            if args[:3] == ('get', 'lease', lifecycle.LEASE): return result(self.lease)
            if args[:3] == ('get', 'configmap', 'access-telemetry-campaign-identity'): return result(q.record['runtimeBinding'])
            if args[:2] == ('get', 'deployment'):
                return result(next(o for o in self.objects if o['kind'] == 'Deployment' and o['metadata']['name'] == args[2]))
            if args[0] == 'patch':
                kind, name = args[1:3]
                obj = self.gate if kind == 'configmap' else self.lease if kind == 'lease' else next(o for o in self.objects if o['metadata']['name'] == name)
                operations = json.loads(args[args.index('--patch') + 1])
                for op in operations:
                    pointer = op['path'].strip('/').split('/'); target = obj
                    for field in pointer[:-1]: target = target[field]
                    if op['op'] == 'test': self.assertEqual(op['value'], target[pointer[-1]])
                    else: target[pointer[-1]] = op['value']
                if kind == 'deployment': obj['status'].update(observedGeneration=obj['metadata']['generation'], replicas=0, readyReplicas=0)
                return result(obj)
            if args[0] == 'get' and ',' in args[1]:
                namespace = args[args.index('-n') + 1]
                return result({'items': [deepcopy(o) for o in self.objects if o['metadata']['namespace'] == namespace]})
            raise AssertionError(args)
        for name, value in (('target', lambda: None), ('kube', kube), ('check_storage', lambda **kw: None), ('validate_descriptor', lambda path=None: None)):
            p = patch.object(q, name, value); p.start(); self.addCleanup(p.stop)
        q.save()
        return q

    def pod(self, owner, name, phase='Running'):
        return {'kind': 'Pod', 'metadata': {'name': name, 'namespace': owner['metadata']['namespace'], 'uid': 'uid-' + name,
            'ownerReferences': [{'kind': owner['kind'], 'name': owner['metadata']['name'], 'uid': owner['metadata']['uid'], 'controller': True}]},
            'spec': deepcopy(owner['spec']['template']['spec']), 'status': {'phase': phase, 'conditions': [{'type': 'Ready', 'status': 'True' if phase == 'Running' else 'False'}]}}

    def test_realistic_startup_reporter_completion_fault_and_restoration_sequence(self):
        q = self.observation_fixture()
        writer = self.objects[1]; writer['status'].update(observedGeneration=1, readyReplicas=2, replicas=2)
        old = self.pod(writer, 'terminating-writer'); old['metadata']['deletionTimestamp'] = lifecycle.utc(self.now); self.objects.append(old)
        q._command_bound = time.monotonic() + 3
        def settle(_):
            writer['status'].update(observedGeneration=2, readyReplicas=0, replicas=0)
            self.objects.remove(old)
        with patch.object(lifecycle.time, 'sleep', settle): q.settle_startup()
        self.assertEqual(1, q.record['observationSequence'])
        job = next(o for o in self.objects if o['kind'] == 'Job')
        job['spec']['suspend'] = False; job['status'] = {'active': 1}
        pod = self.pod(job, 'reporter-pod', 'Pending'); self.objects.append(pod)
        self.assertEqual('pending', q.observe())
        job['status'] = {'succeeded': 1}; pod['status']['phase'] = 'Succeeded'
        self.assertEqual('ready', q.observe())
        self.objects.remove(pod)
        self.assertEqual('ready', q.observe())
        scheduler = next(o for o in self.objects if o['metadata']['name'] == 'dapr-scheduler-server')
        rid = lifecycle.resource_id(scheduler, self.ns)
        q.plan['environment']['faultWindows'] = [{'resourceId': rid, 'startSeconds': 0, 'endSeconds': 100, 'replicas': [0], 'allowRolloutRestart': False}]
        scheduler['spec']['replicas'] = 0; scheduler['status']['readyReplicas'] = 0
        self.assertEqual('pending', q.observe())
        pair = q.record['latestObservation']; descriptor = json.loads(Path(pair['descriptor']['path']).read_bytes())
        readiness = descriptor['bindings']['environment']['readiness']
        self.assertEqual('pending', readiness['status'])
        self.assertEqual('pending', next(r for r in readiness['resources'] if r['resourceId'] == rid)['status'])
        scheduler['spec']['replicas'] = 3; scheduler['status']['readyReplicas'] = 3
        self.assertEqual('ready', q.observe())
        foreign = deepcopy(job); foreign['metadata']['name'] = 'undeclared-reporter'; self.objects.append(foreign)
        with self.assertRaisesRegex(ValueError, 'undeclared-controller-or-job'): q.observe()

    def test_reporter_settling_uses_one_original_300_second_budget(self):
        q = self.observation_fixture(); q.observe(startup=True)
        job = next(o for o in self.objects if o['kind'] == 'Job')
        job['spec']['suspend'] = False; job['status'] = {'active': 1}
        self.assertEqual('pending', q.observe())
        anchor = deepcopy(q.record['reporterStarted'])
        with patch.object(capacity, '_boottime', return_value=anchor['boottimeNs'] + 301_000_000_000):
            with self.assertRaisesRegex(ValueError, 'reporter-settling-timeout'): q.observe()
        self.assertEqual(anchor, q.record['reporterStarted'])

    def test_inspect_reads_current_pair_while_mutating_runner_holds_lock(self):
        import contextlib
        import io
        q = self.observation_fixture(); q.observe(startup=True)
        before = {str(p): p.read_bytes() for p in self.state.rglob('*') if p.is_file() and not p.is_symlink()}
        output = io.StringIO()
        with patch.object(lifecycle, 'CampaignQualification', side_effect=AssertionError('inspect must not acquire mutation lock')), contextlib.redirect_stdout(output):
            code = lifecycle.main(['inspect', '--state', str(self.state)])
        self.assertEqual(0, code)
        observed = json.loads(output.getvalue())
        self.assertEqual(q.record['latestPair'], observed['observationPair'])
        self.assertEqual('ready', observed['readiness'])
        after = {str(p): p.read_bytes() for p in self.state.rglob('*') if p.is_file() and not p.is_symlink()}
        self.assertEqual(before, after)

    def test_same_uid_image_environment_resources_and_security_drift_refuse_even_in_fault_window(self):
        q = self.observation_fixture(); q.observe(startup=True)
        obj = self.objects[0]; original = deepcopy(obj)
        q.plan['environment']['faultWindows'] = [{'resourceId': lifecycle.resource_id(obj, self.ns), 'startSeconds': 0, 'endSeconds': 100, 'replicas': [0], 'allowRolloutRestart': False}]
        for change in (lambda c: c.update(image='foreign'), lambda c: c['env'][0].update(value='changed'),
                       lambda c: c['resources']['limits'].update(cpu='1000m'), lambda c: c.update(securityContext={'privileged': True})):
            obj.clear(); obj.update(deepcopy(original)); change(obj['spec']['template']['spec']['containers'][0])
            with self.assertRaisesRegex(ValueError, 'workload-spec-drift'): q.observe()

    def test_prepared_install_input_tamper_refuses_before_first_infrastructure_command(self):
        q = self.runner()
        for name in ('environment.json', 'dapr-values.json', 'application.yaml', 'dapr.yaml'):
            lifecycle.atomic(self.state / name, b'{}')
        q.record['preparedInputs'] = {name: lifecycle.sha(self.state / name) for name in ('environment.json', 'dapr-values.json', 'application.yaml', 'dapr.yaml')}
        lifecycle.atomic(self.state / 'dapr-values.json', b'{"changed":true}')
        with patch.object(lifecycle, 'admit', return_value=({}, [], self.now)), patch.object(q, 'run', side_effect=AssertionError('no command allowed')), patch.object(q, 'engine', side_effect=AssertionError('no infrastructure allowed')):
            with self.assertRaises(ValueError): q.launch()
        self.assertFalse(q.record['creationAttempted'])

    def test_quiescence_disables_gate_clears_owned_lease_and_verifies_all_application_workloads(self):
        q = self.observation_fixture(); q.observe(startup=True)
        self.lease['spec'].update(holderIdentity=q.plan['environment']['leaseHolders'][0], leaseDurationSeconds=900)
        value = json.loads(self.gate['data']['gate.json']); value.update(state='enabled', expiresUtcMs=self.now + 5000); self.gate['data']['gate.json'] = json.dumps(value)
        q.terminal('cancelled', 'run-cancelled'); q._cleanup_mode = True
        with patch.object(lifecycle, 'admit', side_effect=AssertionError('do not reopen drifted inputs')):
            outcome = q.quiesce()
        self.assertEqual('verified', outcome['state']); self.assertTrue(outcome['ownedWriterPodsAbsent'])
        self.assertEqual('', self.lease['spec']['holderIdentity'])
        mutations = [args for args, _ in self.calls if args[0] == 'patch']
        self.assertEqual(('patch', 'configmap', lifecycle.GATE), mutations[0][:3])
        self.assertEqual(('patch', 'lease', lifecycle.LEASE), mutations[-1][:3])

    def test_foreign_lease_or_control_refuses_before_cleanup_mutation(self):
        for field in ('lease', 'gate', 'namespace'):
            q = self.observation_fixture(); q.observe(startup=True); self.calls.clear()
            if field == 'lease': self.lease['spec']['holderIdentity'] = 'foreign-session'
            elif field == 'gate': self.gate['metadata']['uid'] = 'foreign-gate'
            else: q.record['physicalTarget']['namespaceUid'] = 'foreign-namespace'
            with patch.object(q, 'engine'), patch.object(q, 'nodes', return_value=[]), patch.object(q, 'run', side_effect=AssertionError('no Docker mutation')):
                with self.assertRaisesRegex(ValueError, 'foreign-'): q.stop(remove=True)
            self.assertFalse(any(args[0] == 'patch' for args, _ in self.calls)); q.close()
            self.write_state()

    def stopped_sequence_fixture(self):
        q = self.observation_fixture(); q.observe(startup=True)
        node = {'id': 'original', 'name': '/unit-owned-control-plane', 'created': lifecycle.utc(self.now - 2000),
                'image': q.tools['nodeImage'], 'running': True, 'status': 'running', 'startedAt': lifecycle.utc(self.now - 1000),
                'finishedAt': '0001-01-01T00:00:00Z', 'restartCount': 0, 'volumes': []}
        current = [node]; calls = []
        q.record['nodes'] = deepcopy(current)
        lifecycle.atomic(q.kubeconfig, b'original-private-transport')
        q.record['kubeconfigSha256'] = lifecycle.sha(q.kubeconfig); q.save()
        def run(args, **kwargs):
            calls.append(args)
            if args[:2] == ['docker', 'stop']:
                for value in current:
                    if value['running']:
                        value.update(running=False, status='exited', finishedAt=lifecycle.utc(self.now))
            elif args[:3] == ['kind', 'delete', 'cluster']:
                current.clear()
            else:
                raise AssertionError(args)
            return subprocess.CompletedProcess(args, 0, '', '')
        def bind(runner):
            for name, value in (('engine', lambda: None), ('nodes', lambda: deepcopy(current)),
                                ('owned_nodes', lambda: [{'id': n['id'], 'running': n['running']} for n in current]), ('run', run)):
                p = patch.object(runner, name, value); p.start(); self.addCleanup(p.stop)
        bind(q); q.stop()
        self.assertEqual('stopped', q.record['status'])
        self.assertEqual('verified', q.record['stoppedProof']['quiescence']['state'])
        original = deepcopy(q.record); q.close()
        resumed = self.runner(); bind(resumed)
        return resumed, current, calls, original

    def test_stop_reopen_remove_reuses_exact_stopped_quiescence_without_resetting_deadline(self):
        q, current, calls, original = self.stopped_sequence_fixture()
        with patch.object(q, 'quiesce', side_effect=AssertionError('stopped API must not be contacted')), \
             patch.object(lifecycle, 'admit', side_effect=AssertionError('cleanup must not reopen drifted inputs')), \
             patch.object(lifecycle.time, 'time', return_value=(original['stopping']['utcMs'] + 300000) / 1000), \
             patch.object(capacity, '_boottime', return_value=original['stopping']['boottimeNs'] + 300_000_000_000):
            self.assertAlmostEqual(600, q.remaining(True), places=2)
            q.stop(remove=True)
        self.assertEqual('removed', q.record['status'])
        self.assertFalse(q.record['cleanup']['unverified'])
        self.assertFalse(current)
        self.assertEqual(original['stopping'], q.record['stopping'])
        self.assertEqual(original['stopDeadlineUtcMs'], q.record['stopDeadlineUtcMs'])
        self.assertEqual(original['terminal'], q.record['terminal'])
        self.assertEqual(1, sum(args[:3] == ['kind', 'delete', 'cluster'] for args in calls))

    def test_restarted_then_stopped_original_node_cannot_reuse_previous_quiescence(self):
        q, current, calls, original = self.stopped_sequence_fixture()
        current[0].update(startedAt=lifecycle.utc(self.now + 1000), finishedAt=lifecycle.utc(self.now + 2000), restartCount=1)
        with patch.object(q, 'quiesce', side_effect=AssertionError('changed lifecycle must retain resources')):
            q.stop(remove=True)
        self.assertEqual('stopped-unverified', q.record['status'])
        self.assertEqual('retained-unverified', q.record['cleanup']['disposition'])
        self.assertTrue(q.record['quiescence']['priorVerifiedQuiescenceNotReused'])
        self.assertTrue(current)
        self.assertFalse(any(args[:3] == ['kind', 'delete', 'cluster'] for args in calls))
        self.assertEqual(original['stopDeadlineUtcMs'], q.record['stopDeadlineUtcMs'])

    def test_stopped_quiescence_still_refuses_transport_drift(self):
        q, current, calls, _ = self.stopped_sequence_fixture()
        lifecycle.atomic(q.kubeconfig, b'foreign-transport')
        with patch.object(q, 'quiesce', side_effect=AssertionError('do not adopt a changed target')):
            q.stop(remove=True)
        self.assertEqual('stopped-unverified', q.record['status'])
        self.assertEqual('campaign-lifecycle-transport-drift', q.record['quiescence']['causeCode'])
        self.assertTrue(current)
        self.assertFalse(any(args[:3] == ['kind', 'delete', 'cluster'] for args in calls))

    def test_partial_or_unreachable_quiescence_retains_resources_unverified(self):
        for established in (False, True):
            q = self.runner(); self.started(q); q.record['nodes'] = [{'id': 'original', 'volumes': []}]
            if established: q.record['controls'] = {'original': True}
            calls = []
            with patch.object(q, 'control_readback', side_effect=RuntimeError('command failed')), patch.object(q, 'engine'), patch.object(q, 'nodes', return_value=[{'id': 'original'}]), patch.object(q, 'run', side_effect=lambda args, **kw: calls.append(args)):
                q.stop(remove=True)
            self.assertEqual('retained-unverified', q.record['cleanup']['disposition'])
            self.assertTrue(q.record['cleanup']['unverified']); self.assertFalse(any(c[0] == 'kind' for c in calls))
            q.close(); self.write_state()

    def test_foreign_recreated_volume_is_not_deleted(self):
        q = self.runner(); self.started(q)
        original = {'Name': 'original-volume', 'CreatedAt': '2026-10-25T00:00:00Z'}
        q.record['nodes'] = [{'id': 'original', 'volumes': [original]}]
        calls = []
        def run(args, **kwargs):
            calls.append(args)
            return subprocess.CompletedProcess(args, 0, '{"Name":"original-volume"}\n' if args[:3] == ['docker', 'volume', 'ls'] else '', '')
        with patch.object(q, 'quiesce', return_value={'state': 'verified'}), patch.object(q, 'engine'), patch.object(q, 'nodes', return_value=[]), patch.object(q, 'owned_nodes', return_value=[]), patch.object(q, 'run', run), patch.object(q, 'volume_identity', return_value={**original, 'CreatedAt': '2026-10-25T00:01:00Z'}):
            with self.assertRaisesRegex(ValueError, 'foreign-recreated-volume'): q.stop(remove=True)
        self.assertFalse(any(c[:3] == ['docker', 'volume', 'rm'] for c in calls))
        self.assertTrue(q.record['cleanup']['unverified'])

    def test_startup_failure_capture_precedes_stop_and_preserves_first_outcome(self):
        q = self.observation_fixture(); q.record['status'] = 'starting'
        q.record['commands'].append({'outcome': 'exit-code', 'returncode': 1})
        pod = self.pod(self.objects[0], 'failed-pod', 'Failed')
        pod['status']['containerStatuses'] = [{'state': {'terminated': {'exitCode': 137, 'reason': 'OOMKilled', 'message': 'SECRET-PAYLOAD'}}}]
        self.objects.append(pod)
        self.objects.append({'kind': 'Event', 'metadata': {'namespace': self.ns}, 'type': 'Warning', 'reason': 'BackOff', 'message': 'SECRET-TOKEN', 'count': 1})
        order = []
        with patch.object(q, 'launch', side_effect=ValueError('campaign-lifecycle-resource-not-ready')), patch.object(q, 'stop', side_effect=lambda **kw: order.append(q.record['failureCapture']['state'])):
            with self.assertRaises(ValueError): q.supervise('launch')
        self.assertEqual(['captured'], order)
        raw = (self.state / 'failure-status.json').read_bytes()
        self.assertNotIn(b'SECRET', raw); self.assertIn(b'OOMKilled', raw)
        first = deepcopy(q.record['terminal'])
        q.failure(KeyboardInterrupt())
        self.assertEqual(first, q.record['terminal'])
        q.record.pop('failureCapture'); (self.state / 'failure-status.json').unlink()
        with patch.object(q, 'target', side_effect=RuntimeError('lost target')): q.capture_failure()
        self.assertEqual('unavailable', q.record['failureCapture']['state']); self.assertEqual(first, q.record['terminal'])

    def monitoring_fixture(self):
        q = self.runner(); self.started(q)
        key = b'collector-offline-test-key-32-bytes'; path = self.root / 'collector.key'; lifecycle.atomic(path, key)
        limits = {name: 0 for name in lifecycle.MONITOR_LIMITS}
        limits.update(connections=100, connectionUtilizationPermille=800, transactionLatencyP99Ms=1000, checkpointAgeSeconds=600,
            autovacuumStallSeconds=600, lockWaitMilliseconds=1000, oldestDueAgeSeconds=900, physicalEvidenceAgeSeconds=86400)
        allocations = {'postgres': {'poolId': 'primary', 'identity': 'owned-pg'}, 'scheduler-0': {'poolId': 'primary', 'identity': 'owned-scheduler'}}
        q.plan['environment']['monitoring'] = {'collectorId': 'collector', 'source': {'sha256': 'd' * 64}, 'commandSha256': 'e' * 64,
            'key': {'path': str(path), 'sha256': lifecycle.sha(path)}, 'receiptPath': str(self.root / 'metrics.json'), 'allocations': allocations, 'limits': limits}
        q.record['sizing'] = {'allocations': {name: 1000 for name in allocations}, 'relationWithMarginBytes': 500, 'cumulativeRecords': 100}
        q.record['physicalTarget'] = {'clusterUid': 'original'}
        value = {'schema': lifecycle.MONITOR_SCHEMA, 'runId': q.record['runId'], 'planSha256': q.record['planSha256'], 'profileSha256': q.plan['profileSha256'],
            'collectorId': 'collector', 'sourceSha256': 'd' * 64, 'commandSha256': 'e' * 64, 'observedUtcMs': self.now, 'intervalStartUtcMs': self.now, 'expectedFaults': [],
            'boottimeNs': capacity._boottime(), 'bootId': capacity._boot(), 'phase': 'observed', 'target': q.record['physicalTarget'],
            'allocations': {name: {**identity, 'scope': 'owned-allocation', 'usedBytes': 500, 'availableBytes': 500} for name, identity in allocations.items()},
            'series': {name: 0 for name in lifecycle.MONITOR_SERIES}}
        value['series']['postgres.tableCount'] = 1
        def publish(): lifecycle.atomic(Path(q.plan['environment']['monitoring']['receiptPath']), {**value, 'signature': hmac.new(key, capacity.canonical(value), hashlib.sha256).hexdigest()})
        publish(); return q, value, publish

    def test_complete_metrics_overrun_missing_stale_and_pg_fault_scope(self):
        q, value, publish = self.monitoring_fixture()
        with patch.object(lifecycle, 'clock', return_value=self.now):
            q.check_storage()
            value['allocations']['scheduler-0'].update(usedBytes=900, availableBytes=100); publish()
            with patch.object(os, 'statvfs', side_effect=AssertionError('shared filesystem free space cannot authorize component use')):
                with self.assertRaisesRegex(ValueError, 'owned-allocation-bound'): q.check_storage()
            value['allocations']['scheduler-0'].update(usedBytes=500, availableBytes=500)
            value['series'].pop('runtime.etagErrors'); publish()
            with self.assertRaisesRegex(ValueError, 'series-missing'): q.check_storage()
            value['series']['runtime.etagErrors'] = 0; value['observedUtcMs'] -= 61000; publish()
            with self.assertRaisesRegex(ValueError, 'collector-stale'): q.check_storage()
            value['observedUtcMs'] = self.now
            value['series'].update({name: None for name in lifecycle.MONITOR_SERIES if name.startswith('postgres.')}); publish()
            q.check_storage(pg_unavailable=True)
            with self.assertRaisesRegex(ValueError, 'no-data'): q.check_storage()
            value['series']['runtime.etagErrors'] = None; publish()
            with self.assertRaisesRegex(ValueError, 'no-data'): q.check_storage(pg_unavailable=True)

    def c4_rule(self, q, scenario, rid, *, allowances=None):
        return {'commandId': 'C4.other', 'commandSha256': 'f' * 64, 'resourceId': rid, 'startSeconds': 0, 'endSeconds': 100,
            'replicas': [], 'allowRolloutRestart': False, 'scenario': scenario,
            'changes': lifecycle.c4_changes(scenario, rid, q.record['target']['namespace']), 'metricAllowances': allowances or {}}

    def test_source_c4_environment_and_resource_faults_restore_strict_baselines(self):
        q = self.observation_fixture(); q.observe(startup=True)
        for scenario, name in (('bad-key', 'memories-access-telemetry'), ('capacity-pressure', 'access-telemetry-postgresql')):
            obj = next(o for o in self.objects if o['metadata']['name'] == name)
            original = deepcopy(obj); rid = lifecycle.resource_id(obj, self.ns)
            rule = self.c4_rule(q, scenario, rid)
            lifecycle.validate_fault_changes(rule, {'C4.other': {'acceptedRecordUpperBound': 50000}}, self.ns)
            q.plan['environment']['faultWindows'] = [rule]
            container = obj['spec']['template']['spec']['containers'][0]
            for change in rule['changes']:
                if change['field'] == 'env': container['env'].append({'name': change['name'], 'value': change['value']})
                else: container['resources'].setdefault(change['section'], {})[change['name']] = change['value']
            self.assertEqual('pending', q.observe())
            mutated = deepcopy(obj)
            container['resources']['limits']['cpu'] = '9000m'
            with self.assertRaisesRegex(ValueError, 'workload-spec-drift'): q.observe()
            obj.clear(); obj.update(mutated)
            with patch.object(capacity, '_boottime', return_value=q.record['started']['boottimeNs'] + 101_000_000_000):
                with self.assertRaisesRegex(ValueError, 'workload-spec-drift'): q.observe()
            obj.clear(); obj.update(original)
            self.assertEqual('ready', q.observe())
        # The closed registry rejects an invented environment/security exception.
        rule['changes'] = [{'container': 'postgresql', 'field': 'resources', 'section': 'limits', 'name': 'cpu', 'value': '9000m'}]
        with self.assertRaisesRegex(ValueError, 'not-in-source-registry'):
            lifecycle.validate_fault_changes(rule, {'C4.other': {'acceptedRecordUpperBound': 50000}}, self.ns)

    def test_expected_error_counts_require_exact_attribution_and_one_window_budget(self):
        q, value, publish = self.monitoring_fixture()
        rid = 'Deployment/' + q.record['target']['namespace'] + '/memories-access-telemetry'
        q.record['resources'] = [{'id': rid, 'uid': 'original-lifecycle'}]
        rule = self.c4_rule(q, 'etag-failure', rid, allowances={'runtime.etagErrors': 2})
        q.plan['environment']['faultWindows'] = [rule]
        value['intervalStartUtcMs'] = q.record['started']['utcMs']; value['observedUtcMs'] = value['intervalStartUtcMs'] + 10
        value['expectedFaults'] = [{'faultSha256': capacity.digest(rule), 'resourceUid': 'original-lifecycle', 'series': {'runtime.etagErrors': 1}}]
        value['series']['runtime.etagErrors'] = 1; publish()
        with patch.object(lifecycle, 'clock', side_effect=lambda plan: value['observedUtcMs']):
            q.check_storage(); q.check_storage()
            self.assertEqual(1, q.record['faultMetricTotals'][capacity.digest(rule)]['totals']['runtime.etagErrors'])
            value['series']['runtime.daprTransactionErrors'] = 1; publish()
            with self.assertRaisesRegex(ValueError, 'operational-monitor-limit'): q.check_storage()
            value['series']['runtime.daprTransactionErrors'] = 0
            value['expectedFaults'][0]['resourceUid'] = 'foreign'; publish()
            with self.assertRaisesRegex(ValueError, 'attribution-binding'): q.check_storage()
            value['expectedFaults'][0]['resourceUid'] = 'original-lifecycle'
            value['intervalStartUtcMs'] = value['observedUtcMs']; value['observedUtcMs'] += 10
            value['expectedFaults'][0]['series']['runtime.etagErrors'] = 2; value['series']['runtime.etagErrors'] = 2; publish()
            with self.assertRaisesRegex(ValueError, 'budget-exhausted'): q.check_storage()
            value['expectedFaults'][0]['series']['runtime.etagErrors'] = 1; value['series']['runtime.etagErrors'] = 1
            tick = q.record['started']['boottimeNs'] + 101_000_000_000
            value.update(observedUtcMs=q.record['started']['utcMs'] + 101000, intervalStartUtcMs=q.record['started']['utcMs'] + 101000, boottimeNs=tick)
            publish()
            with patch.object(capacity, '_boottime', return_value=tick):
                with self.assertRaisesRegex(ValueError, 'attribution-binding'): q.check_storage()
                value['expectedFaults'] = []; value['series']['runtime.etagErrors'] = 0; publish()
                q.check_storage()
                value['series']['runtime.etagErrors'] = 1; publish()
                with self.assertRaisesRegex(ValueError, 'operational-monitor-limit'): q.check_storage()

    def provider_fixture(self):
        labels = {'pod-security.kubernetes.io/' + mode: 'restricted' for mode in ('enforce', 'audit', 'warn')}
        namespace = {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': 'monitoring', 'labels': labels}}
        def deployment(name, selector):
            return {'kind': 'Deployment', 'metadata': {'name': name, 'namespace': 'monitoring'}, 'spec': {'replicas': 1,
                'template': {'metadata': {'labels': selector}, 'spec': {'containers': [{'name': name, 'resources': {'limits': {'cpu': '100m', 'memory': '128Mi'}}}]}}}}
        prometheus = deployment('prometheus', {'app': 'prometheus'})
        collector = deployment('collector', {'app.kubernetes.io/name': 'opentelemetry-collector'})
        service = {'kind': 'Service', 'metadata': {'name': 'prometheus-operated', 'namespace': 'monitoring'}, 'spec': {'selector': {'app': 'prometheus'}, 'ports': [{'port': 9090}]}}
        return [namespace, prometheus, collector, service]

    def test_declared_provider_namespaces_enforce_security_source_shape_and_shared_budget(self):
        objects = self.provider_fixture()
        names = environment.provider_namespaces({'providerNamespaces': ['monitoring']}, 'application')
        environment.validate_provider_scope(objects, 'application', names)
        environment.supplemental_budget(objects)
        objects[1]['spec']['template']['spec']['containers'][0]['resources']['limits']['cpu'] = '250m'
        with self.assertRaisesRegex(ValueError, 'control-envelope'): environment.supplemental_budget(objects)
        objects[1]['metadata']['namespace'] = 'foreign'
        with self.assertRaisesRegex(ValueError, 'foreign-namespace'): environment.validate_provider_scope(objects, 'application', names)
        objects[1]['metadata']['namespace'] = 'monitoring'; objects[0]['metadata']['labels']['pod-security.kubernetes.io/enforce'] = 'privileged'
        with self.assertRaisesRegex(ValueError, 'namespace-security'): environment.validate_provider_scope(objects, 'application', names)
        with self.assertRaisesRegex(ValueError, 'namespace-declaration'):
            environment.provider_namespaces({'providerNamespaces': ['monitoring', 'kube-system']}, 'application')

    def test_provider_namespace_creation_binding_and_foreign_inventory_refusal(self):
        q = self.runner(); q.plan['environment']['providerNamespaces'] = ['monitoring']
        network = self.root / 'network.yaml'; lifecycle.atomic(network, b'[]')
        q.plan['assets'] = {'network': {'path': str(network), 'sha256': lifecycle.sha(network)}}
        namespace = self.provider_fixture()[0]
        app = {'kind': 'Namespace', 'metadata': {'name': q.record['target']['namespace']}}
        proposed = [environment.label(o, q.record['runId']) for o in (app, namespace)]
        inventory = [{'kind': 'Namespace', 'metadata': {'name': 'kube-system', 'uid': 'system', 'labels': {}}}]
        calls = []
        def kube(*args, **kwargs):
            calls.append(args)
            if args[0] == 'create':
                for obj in json.loads(kwargs['data'])['items']:
                    obj['metadata']['uid'] = 'uid-' + obj['metadata']['name']; inventory.append(obj)
            return subprocess.CompletedProcess(args, 0, json.dumps({'items': inventory}), '')
        with patch.object(q, 'kube', kube):
            q.create_owned_namespaces(proposed); q.namespace_readback()
            self.assertIn('monitoring', q.record['namespaceUids'])
            self.assertIn('dapr-system', q.record['namespaceUids'])
            before = len(calls)
            with self.assertRaisesRegex(ValueError, 'preexisting-or-foreign'): q.create_owned_namespaces(proposed)
            self.assertFalse(any(args[0] == 'create' for args in calls[before:]))
            inventory.append({'kind': 'Namespace', 'metadata': {'name': 'foreign', 'uid': 'foreign', 'labels': {}}})
            with self.assertRaisesRegex(ValueError, 'foreign-namespace-inventory'): q.namespace_readback()
            inventory.pop(); inventory[-1]['metadata']['uid'] = 'replaced'
            with self.assertRaisesRegex(ValueError, 'foreign-namespace-inventory'): q.namespace_readback()

    def test_monitoring_provider_workload_readiness_and_uid_drift_are_observed(self):
        q = self.observation_fixture(); q.plan['environment']['providerNamespaces'] = ['monitoring']
        provider = deepcopy(self.objects[0]); provider['metadata'].update(name='collector', namespace='monitoring', uid='collector-uid')
        provider['spec']['template']['metadata']['labels'] = {'app.kubernetes.io/name': 'opentelemetry-collector'}
        rid = lifecycle.resource_id(provider, self.ns)
        q.record['admittedResources'][rid] = deepcopy(provider); self.objects.append(provider)
        with patch.object(q, 'namespace_readback'):
            self.assertEqual('ready', q.observe(startup=True))
            self.assertIn(rid, {r['id'] for r in q.record['resources']})
            provider['status']['readyReplicas'] = 0
            with self.assertRaisesRegex(ValueError, 'resource-not-ready'): q.observe()
            provider['status']['readyReplicas'] = provider['spec']['replicas']; provider['metadata']['uid'] = 'foreign-collector'
            with self.assertRaisesRegex(ValueError, 'owned-resource-drift'): q.observe()

    def test_monitor_limit_triggers_supervised_stop(self):
        q, value, publish = self.monitoring_fixture()
        value['series']['runtime.tlsAuthErrors'] = 1; publish()
        with patch.object(lifecycle, 'clock', return_value=self.now), patch.object(lifecycle, 'admit'), patch.object(q, 'observe', side_effect=q.check_storage), patch.object(q, 'capture_failure'), patch.object(q, 'stop') as stop:
            with self.assertRaisesRegex(ValueError, 'operational-monitor-limit'): q.supervise('monitor')
        stop.assert_called_once_with(remove=False)
        self.assertEqual('campaign-lifecycle-operational-monitor-limit', q.record['terminal']['cause'])

if __name__ == '__main__':
    unittest.main()
