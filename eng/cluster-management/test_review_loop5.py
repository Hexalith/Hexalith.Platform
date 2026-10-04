"""Regression cases for the reviewed driver, finalizer and controller checkpoints."""
import copy
import importlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

from evidence import canonical, digest
from qualify import project_resource
from rehearse import (Fixture, SYSTEM_WORKSPACE_FINALIZER, content_review_digest,
                      lease_renewal_transition)
from rehearse_catalog import RepresentativeFixture, catalog_counts
from rehearse_namespaces import SevenNamespaceFixture
import test_rehearse as existing


class FinalizerCheckpointTests(unittest.TestCase):
    def fixture(self):
        fixture, records = existing.NativeRequestTests().fixture()
        fixture.args.age, fixture.args.recipient = 'age', 'public'
        return fixture, records

    def test_foreground_finalizer_added_by_delete_is_retained_and_other_drift_refused(self):
        fixture, _ = self.fixture()
        raw = existing.NativeRequestTests().obj(finalizers=[SYSTEM_WORKSPACE_FINALIZER, 'other/keep'])
        fixture.reviewed_raw_by_uid['u-1'] = raw
        current = copy.deepcopy(raw)
        current['metadata'].update(resourceVersion='6', deletionTimestamp='2026-10-04T10:00:00Z',
                                   finalizers=[SYSTEM_WORKSPACE_FINALIZER, 'other/keep', 'foregroundDeletion'])
        ok = SimpleNamespace(returncode=0, stdout=b'', stderr=b'')
        with patch.object(fixture, 'native_read', return_value=current), patch.object(fixture, 'kube', return_value=ok) as kube:
            result = fixture.remove_named_finalizer('/apis/x', 'u-1', SYSTEM_WORKSPACE_FINALIZER)
        self.assertTrue(result['privateFullContentVerified'])
        self.assertEqual(kube.call_args.kwargs['obj']['metadata']['finalizers'], ['other/keep', 'foregroundDeletion'])
        for field in ('spec', 'data', 'rules', 'finalizers'):
            drift = copy.deepcopy(current)
            if field == 'finalizers':
                drift['metadata']['finalizers'].append('other/new')
            else:
                drift[field] = {'synthetic': 'changed'}
            with self.subTest(field=field), patch.object(fixture, 'native_read', return_value=drift), patch.object(fixture, 'kube') as kube:
                with self.assertRaisesRegex(ValueError, 'reviewed-full-content-drift'):
                    fixture.remove_named_finalizer('/apis/x', 'u-1', SYSTEM_WORKSPACE_FINALIZER)
            kube.assert_not_called()

    def test_namespace_private_full_content_guard_refuses_unprojected_metadata_drift(self):
        raw = {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': 'default', 'uid': 'ns',
            'resourceVersion': '7', 'finalizers': [SYSTEM_WORKSPACE_FINALIZER, 'other/retain'],
            'labels': {'application': 'keep'}, 'annotations': {'synthetic': 'keep'}},
            'spec': {'finalizers': ['kubernetes']}}
        reviewed = project_resource(raw)
        fixture, _ = self.fixture()
        fixture.reviewed_content_digests['ns'] = content_review_digest(raw)
        current = copy.deepcopy(raw)
        current['metadata'].update(resourceVersion='9', generation=2, managedFields=[])
        current['status'] = {'phase': 'Active'}
        with patch.object(fixture, 'native_read', return_value=current), patch.object(fixture, 'kube') as kube:
            result = fixture.remove_namespace_finalizer('/api/v1/namespaces/default', reviewed)
        self.assertTrue(result['privateFullContentVerified'])
        self.assertEqual(kube.call_args.kwargs['obj']['metadata']['resourceVersion'], '9')
        for field in ('labels', 'annotations', 'spec'):
            drift = copy.deepcopy(current)
            if field == 'spec':
                drift['spec']['unprojected'] = 'changed'
            else:
                drift['metadata'][field]['synthetic'] = 'changed'
            self.assertEqual(project_resource(drift), project_resource(current))
            with self.subTest(field=field), patch.object(fixture, 'native_read', return_value=drift), patch.object(fixture, 'kube') as kube:
                with self.assertRaisesRegex(ValueError, 'reviewed-full-content-drift'):
                    fixture.remove_namespace_finalizer('/api/v1/namespaces/default', reviewed)
            kube.assert_not_called()

    def test_absent_lease_reads_fail_with_identity_drift(self):
        raw = existing.NativeRequestTests().leader_lease()
        for first, second in ((None, raw), (raw, None), (None, None)):
            with self.subTest(first=first is None, second=second is None), self.assertRaisesRegex(ValueError, 'lease-renewal-identity-drift'):
                lease_renewal_transition(raw, first, second, {'lease-1'}, 'console-route', True)

    def test_lease_and_category_reads_are_separated_before_transition_and_delete(self):
        helper = existing.NativeRequestTests()
        cases = [('console-route', helper.leader_lease(), [], 'lease-1'),
                 ('remaining-release-objects', *helper.category_count_inputs()[::2], 'category-1')]
        for name, raw, extensions, uid in cases:
            current = copy.deepcopy(raw)
            if name == 'console-route':
                current['spec']['renewTime'] = '2026-10-04T10:01:00Z'
            else:
                current['metadata']['annotations']['kubesphere.io/count'] = '0'
            fixture, records = self.fixture()
            fixture.reviewed_raw_by_uid = {v['metadata']['uid']: v for v in [raw, *extensions]}
            fixture.retirement_uids = set(fixture.reviewed_raw_by_uid)
            fixture.completed_retirement_phases.update({'catalog-extension', 'controllers-and-services'})
            phase = {'phase': name, 'expected': [uid], 'roots': [uid], 'managerAbsentRequired': True, 'mode': 'native-delete'}
            projected = project_resource(raw)
            resources = {(raw['apiVersion'], raw['kind']): ('objects', name == 'console-route')}
            events = []
            def read(path):
                events.append('read')
                return current
            def sleep(seconds):
                events.append(('sleep', seconds))
            with self.subTest(phase=name), patch.object(fixture, 'settled_inventory', return_value=([projected], True)), \
                 patch.object(fixture, 'native_read', side_effect=read), patch('rehearse.time.sleep', side_effect=sleep), \
                 patch.object(fixture, 'native_retire', side_effect=ValueError('delete-sentinel')):
                with self.assertRaisesRegex(ValueError, 'delete-sentinel'):
                    fixture.retire_phase(phase, fixture.retirement_uids, {uid: projected}, resources)
            self.assertEqual(events, ['read', ('sleep', 2), 'read'])
            self.assertEqual(fixture.reviewed_content_digests[uid], content_review_digest(current))
            self.assertTrue(records)

    def test_global_role_phase_exports_and_refreshes_only_exact_verified_user_transition(self):
        for drift in (False, True):
            fixture, records = self.fixture()
            user = {'apiVersion': 'iam.kubesphere.io/v1beta1', 'kind': 'User',
                'metadata': {'name': 'admin', 'uid': 'user', 'resourceVersion': '1',
                    'annotations': {'iam.kubesphere.io/globalrole': 'platform-admin'}}, 'spec': {'synthetic': 'keep'}}
            binding = {'apiVersion': 'iam.kubesphere.io/v1beta1', 'kind': 'GlobalRoleBinding',
                'metadata': {'name': 'admin', 'uid': 'binding', 'resourceVersion': '1'},
                'roleRef': {'apiGroup': 'iam.kubesphere.io', 'kind': 'GlobalRole', 'name': 'platform-admin'},
                'subjects': [{'apiGroup': 'iam.kubesphere.io', 'kind': 'User', 'name': 'admin'}]}
            raw = {'user': user, 'binding': binding}
            fixture.reviewed_raw_by_uid = copy.deepcopy(raw)
            fixture.reviewed_content_digests = {uid: content_review_digest(v) for uid, v in raw.items()}
            fixture.retirement_uids = set(raw)
            after = copy.deepcopy(user)
            after['metadata']['annotations']['iam.kubesphere.io/globalrole'] = ''
            if drift:
                after['spec']['synthetic'] = 'changed'
            by_uid = {uid: project_resource(v) for uid, v in raw.items()}
            phase = {'phase': 'global-role-bindings', 'order': 1, 'expected': ['binding'], 'roots': ['binding'],
                     'mode': 'native-delete', 'managerAbsentRequired': False, 'controllersRunning': True}
            def settled_after():
                fixture.raw_inventory_by_uid = {'user': after}
                return [project_resource(after)], True
            with self.subTest(drift=drift), patch.object(fixture, 'settled_inventory',
                    side_effect=[(list(by_uid.values()), True), settled_after()]), \
                 patch.object(fixture, 'native_retire', return_value={'outcome': 'native-delete-accepted'}), \
                 patch.object(fixture, 'wait_absent', return_value=True):
                if drift:
                    with self.assertRaisesRegex(ValueError, 'unexpected-controller-role-annotation-transition'):
                        fixture.retire_phase(phase, set(raw), by_uid, {(binding['apiVersion'], binding['kind']): ('globalrolebindings', False)})
                else:
                    fixture.retire_phase(phase, set(raw), by_uid, {(binding['apiVersion'], binding['kind']): ('globalrolebindings', False)})
            if drift:
                fixture.attempt.encrypt.assert_not_called()
                self.assertNotIn('global-role-bindings', fixture.completed_retirement_phases)
                self.assertEqual(fixture.reviewed_content_digests['user'], content_review_digest(user))
            else:
                export = fixture.attempt.encrypt.call_args
                self.assertEqual(export.args[0], 'global-role-annotation-transition')
                self.assertEqual(json.loads(export.args[1])['transitions'][0]['bindingUid'], 'binding')
                self.assertEqual(fixture.reviewed_content_digests['user'], content_review_digest(after))
                self.assertEqual(content_review_digest(fixture.reviewed_raw_by_uid['user']), content_review_digest(after))
                self.assertEqual(records['global-role-annotation-transition.json']['privateReadbackExport'], 'global-role-annotation-transition.age')
                self.assertIn('global-role-bindings', fixture.completed_retirement_phases)

    def test_retirement_seeds_private_baseline_and_scope_before_actual_phase_and_exports_it(self):
        fixture, records = self.fixture()
        fixture.args.ks_chart_sha256, fixture.args.source_digest, fixture.args.script_digest = 'c' * 64, 's' * 64, 'p' * 64
        namespace = {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': 'kept', 'uid': 'ns', 'resourceVersion': '1'}}
        config = {'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'synthetic', 'uid': 'cm', 'resourceVersion': '1',
            'namespace': 'kubesphere-system', 'annotations': {'meta.helm.sh/release-name': 'ks-core'}}, 'data': {'synthetic': 'keep'}}
        raw = {'ns': namespace, 'cm': config}
        def inventory(items):
            fixture.raw_inventory_by_uid = copy.deepcopy(items)
            return [project_resource(v) for v in items.values()], True
        def read(path):
            self.assertEqual(fixture.retirement_uids, {'cm'})
            self.assertEqual(fixture.reviewed_raw_by_uid, raw)
            self.assertEqual(fixture.completed_retirement_phases, set())
            current = copy.deepcopy(config)
            current['metadata']['resourceVersion'] = '2'
            return current
        def health(*args):
            self.assertEqual(fixture.completed_retirement_phases, {'remaining-release-objects'})
            return {}
        with patch.object(fixture, 'probe_workspace_propagation'), patch.object(fixture, 'populate_retirement_decisions'), \
             patch.object(fixture, 'crd_inventory', return_value=[]), \
             patch.object(fixture, 'settled_inventory', side_effect=lambda: inventory(raw if not fixture.completed_retirement_phases and fixture.last_step != 'retire-remaining-release-objects' else {'ns': namespace})), \
             patch.object(fixture, 'discover', return_value={('v1', 'ConfigMap'): ('configmaps', True)}), \
             patch.object(fixture, 'native_read', side_effect=read), patch.object(fixture, 'wait_absent', return_value=True), \
             patch.object(fixture, 'kube', return_value=SimpleNamespace(returncode=0, stdout=b'', stderr=b'')), \
             patch.object(fixture, 'post_retirement_checks', side_effect=health):
            fixture.retire_kubesphere()
        self.assertEqual(fixture.reviewed_raw_by_uid, raw)
        self.assertIsNot(fixture.reviewed_raw_by_uid['cm'], config)
        export = fixture.attempt.encrypt.call_args
        self.assertEqual(export.args[0], 'retirement-reviewed-content-digests')
        self.assertEqual(json.loads(export.args[1])['digests'], {uid: content_review_digest(v) for uid, v in raw.items()})
        self.assertEqual(records['kubesphere-retirement-allowlist.json']['actions'][0]['uid'], 'cm')
        phase_record = next(v for n, v in records.items() if n.startswith('retirement-phase-') and n.endswith('-remaining-release-objects.json'))
        request = phase_record['requests'][0]
        self.assertTrue(request['privateFullContentVerified'])
        self.assertEqual(records['kubesphere-retirement-result.json']['exactBaselineRemovals'], 1)


class DriverSmokeTests(unittest.TestCase):
    def test_shared_driver_lifecycle_binds_and_encrypts_executed_driver_before_start(self):
        from rehearse import rehearse
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / 'source.json'
            source.write_bytes(canonical({'sourceClusterUid': 'synthetic-source', 'nativeEndpoint': 'https://192.0.2.10:6443'}))
            driver = Path(__file__).with_name('rehearse_catalog.py')
            args = SimpleNamespace(source_inventory=source, project_root=Path(temporary), evidence_root=Path(temporary) / 'private',
                attempt_id='driver-smoke', operator='unit', age='age', recipient='public')
            attempt = SimpleNamespace(directory=Path(temporary) / 'attempt', record=Mock(), encrypt=Mock(), finish=Mock())
            fixture = Mock(last_step='not-started')
            fixture.cleanup.return_value = True
            constructor = Mock(return_value=fixture)
            events = []
            attempt.encrypt.side_effect = lambda *a: events.append(('encrypt', a[0]))
            fixture.start.side_effect = lambda: events.append(('start',))
            identities = {'runtimeVersionOutput': {'kind': {'exitCode': 0}, 'docker': {'exitCode': 0}}}
            with patch('rehearse.Attempt', return_value=attempt), patch('rehearse.tool_identities', return_value=identities):
                _, state = rehearse(args, fixture_type=constructor, driver_path=driver)
            records = {c.args[0]: c.args[1] for c in attempt.record.call_args_list}
            self.assertEqual(records['attempt.json']['driverSha256'], digest(driver.read_bytes()))
            self.assertEqual(attempt.encrypt.call_args.args[1], driver.read_bytes())
            self.assertEqual(events, [('encrypt', 'executed-driver'), ('start',)])
            constructor.assert_called_once_with(args, attempt)
            fixture.execute.assert_called_once()
            fixture.cleanup.assert_called_once()
            attempt.finish.assert_called_once()
            self.assertEqual(state, 'passed-limited')

    def test_drivers_import_and_show_explicit_cli_without_allocating_fixtures(self):
        directory = Path(__file__).parent
        for name in ('rehearse_catalog', 'rehearse_namespaces'):
            with self.subTest(driver=name), patch('rehearse.Attempt') as attempt, patch('rehearse.subprocess.run') as run:
                importlib.reload(importlib.import_module(name))
            attempt.assert_not_called()
            run.assert_not_called()
            result = subprocess.run([sys.executable, str(directory / (name + '.py')), '--help'], capture_output=True, timeout=10)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn(b'--readback-identity', result.stdout)
            self.assertIn(b'--source-inventory', result.stdout)

    def test_catalog_driver_creates_source_sized_synthetic_owners_and_real_cleanup_finalizers(self):
        source = {'resources': [{'apiVersion': 'application.kubesphere.io/v2', 'kind': kind}
                  for kind, count in [('Repo', 1), ('Application', 27), ('ApplicationVersion', 90), ('Category', 1)]
                  for _ in range(count)]}
        attempt = SimpleNamespace(directory=Path('/nonexistent'), record=Mock(), encrypt=Mock())
        fixture = RepresentativeFixture(SimpleNamespace(attempt_id='catalog-smoke', source=source, source_digest='s' * 64), attempt)
        objects = []
        def create(raw):
            for value in (raw['items'] if raw['kind'] == 'List' else [raw]):
                value['metadata']['uid'] = 'uid-' + value['metadata']['name']
                objects.append(value)
        def get(resource, name=None):
            kinds = {'repos': 'Repo', 'applications': 'Application', 'categories': 'Category'}
            selected = [v for v in objects if v['kind'] == kinds[resource.split('.')[0]]]
            return next(v for v in selected if v['metadata']['name'] == name) if name else {'items': selected}
        with patch.object(Fixture, 'populate_retirement_decisions'), patch.object(fixture, 'create', side_effect=create), patch.object(fixture, 'get', side_effect=get):
            fixture.populate_retirement_decisions()
        self.assertEqual(catalog_counts({'resources': objects}), fixture.catalog_counts)
        repo = next(v for v in objects if v['kind'] == 'Repo')
        applications = {v['metadata']['uid']: v for v in objects if v['kind'] == 'Application'}
        for app in applications.values():
            self.assertEqual(app['metadata']['ownerReferences'][0]['uid'], repo['metadata']['uid'])
        for version in (v for v in objects if v['kind'] == 'ApplicationVersion'):
            self.assertIn(version['metadata']['ownerReferences'][0]['uid'], applications)
            self.assertEqual(version['metadata']['finalizers'], ['application.kubesphere.io/cleanup'])
            self.assertTrue(version['spec']['pullUrl'].startswith('http://127.0.0.1:9/'))
        source['resources'].pop()
        with patch.object(fixture, 'create') as create, self.assertRaisesRegex(ValueError, 'source-catalog-counts-changed'):
            fixture.populate_retirement_decisions()
        create.assert_not_called()

    def test_application_store_phase_preserves_retained_category_before_passing_result(self):
        counts = {'Repo': 1, 'Application': 27, 'ApplicationVersion': 90, 'Category': 1}
        by_uid = {kind + '-' + str(index): {'kind': kind,
                  'finalizers': ['application.kubesphere.io/cleanup'] if kind == 'ApplicationVersion' else []}
                  for kind in ('Repo', 'Application', 'ApplicationVersion') for index in range(counts[kind])}
        phase = {'phase': 'application-store', 'expected': list(by_uid)}
        category = {'apiVersion': 'application.kubesphere.io/v2', 'kind': 'Category',
            'metadata': {'name': 's426-catalog-category', 'uid': 'category', 'resourceVersion': '1',
                         'finalizers': ['categories.application.kubesphere.io/finalizer']}, 'spec': {}}
        for change in ('unchanged', 'spec', 'finalizers'):
            records = {}
            attempt = SimpleNamespace(directory=Path('/nonexistent'), encrypt=Mock(),
                                      record=lambda n, v: records.update({n: v}))
            fixture = RepresentativeFixture(SimpleNamespace(attempt_id='catalog-phase-smoke'), attempt)
            fixture.catalog_category, fixture.catalog_counts = copy.deepcopy(category), counts
            current = copy.deepcopy(category)
            current['metadata']['resourceVersion'] = '2'
            current['status'] = {'synthetic': 'server-bookkeeping'}
            if change == 'spec':
                current['spec']['description'] = 'changed'
            elif change == 'finalizers':
                current['metadata']['finalizers'] = []
            after = ['native-phase-result']
            with self.subTest(change=change), patch.object(Fixture, 'retire_phase', return_value=after) as native_phase, \
                 patch.object(fixture, 'get', return_value=current) as get:
                if change == 'unchanged':
                    self.assertIs(fixture.retire_phase(phase, set(by_uid), by_uid, {}), after)
                    self.assertEqual(records['catalog-result.json']['state'], 'passed')
                    self.assertTrue(records['catalog-result.json']['categoryIdentityContentAndFinalizerPreserved'])
                    self.assertEqual(records['catalog-result.json']['exactRemovals'], 118)
                else:
                    with self.assertRaisesRegex(ValueError, 'catalog-archive-category-drift'):
                        fixture.retire_phase(phase, set(by_uid), by_uid, {})
                    self.assertNotIn('catalog-result.json', records)
            native_phase.assert_called_once_with(phase, set(by_uid), by_uid, {})
            get.assert_called_once_with('categories.application.kubesphere.io', 's426-catalog-category')

    def test_namespace_driver_executes_seven_native_interventions_and_checks_request_trace(self):
        raw = {}
        records = {}
        attempt = SimpleNamespace(directory=Path('/nonexistent'), encrypt=Mock(), record=lambda n, v: records.update({n: v}))
        fixture = SevenNamespaceFixture(SimpleNamespace(attempt_id='namespace-smoke', age='age', recipient='public'), attempt)
        def create(value):
            value['metadata'].update(uid='uid-' + value['metadata']['name'], resourceVersion='1')
            value['spec'] = {'finalizers': ['kubernetes']}
            raw[value['metadata']['name']] = value
        def get(resource, name):
            return copy.deepcopy(raw[name])
        def settled():
            fixture.raw_inventory_by_uid = {v['metadata']['uid']: copy.deepcopy(v) for v in raw.values()}
            return [project_resource(v) for v in raw.values()], True
        def kube(name, *argv, **kwargs):
            self.assertEqual(argv[:2], ('replace', '--raw'))
            value = kwargs['obj']
            self.assertEqual(value['metadata']['resourceVersion'], '1')
            raw[value['metadata']['name']] = copy.deepcopy(value)
        with patch.object(fixture, 'populate'), patch.object(fixture, 'create', side_effect=create), \
             patch.object(fixture, 'get', side_effect=get), patch.object(fixture, 'settled_inventory', side_effect=settled), \
             patch.object(fixture, 'discover', return_value={('v1', 'Namespace'): ('namespaces', False)}), \
             patch.object(fixture, 'native_read', side_effect=lambda path: copy.deepcopy(raw[path.rsplit('/', 1)[-1]])), \
             patch.object(Fixture, 'kube', side_effect=kube), patch.object(fixture, 'wait_absent', return_value=True), \
             patch.object(fixture, 'run', return_value=SimpleNamespace(stdout=b'synthetic-426-canary')):
            fixture.execute()
        result = records['seven-namespace-result.json']
        self.assertEqual(result['actualNamespaceUidResourceVersionBoundPutRequests'], 7)
        self.assertEqual(result['actualNamespaceDeleteRequests'], 0)
        phase_record = next(v for n, v in records.items() if n.startswith('retirement-phase-') and n.endswith('-namespace-finalizers.json'))
        self.assertTrue(all(v['privateFullContentVerified'] for v in phase_record['requests']))
        self.assertTrue(all(v['onlyKubeSphereMetadataFinalizerRemoved'] for v in result['namespaces']))


class VendorImageStreamTests(unittest.TestCase):
    def fixture(self):
        records = {}
        attempt = SimpleNamespace(directory=Path('/nonexistent'), encrypt=Mock(), finish=Mock(),
                                  record=lambda n, v: records.update({n: v}))
        args = SimpleNamespace(attempt_id='vendor-stream-test', age='age', recipient='public', operator='unit')
        return Fixture(args, attempt), records

    def test_vendor_save_waits_with_timeout_before_reading_stderr(self):
        fixture, _ = self.fixture()
        source, events = Mock(), []
        source.stdout.close.side_effect = lambda: events.append('stdout-closed')
        def wait(timeout=None):
            events.append(('wait', timeout))
            return 0
        source.wait.side_effect = wait
        source.stderr.read.side_effect = lambda: events.append('stderr-read') or b'synthetic-save-diagnostic'
        source.poll.return_value = 0
        imported = SimpleNamespace(returncode=0, stdout=b'synthetic-import-output', stderr=b'')
        with patch.object(fixture, 'run', return_value=SimpleNamespace(stdout=b'[{"Id":"sha256:synthetic"}]')), \
             patch('rehearse.subprocess.Popen', return_value=source), \
             patch('rehearse.subprocess.run', return_value=imported) as importer:
            records = fixture.load_vendor_images(['synthetic:fixture'])
        self.assertEqual(events, ['stdout-closed', ('wait', 30), 'stderr-read', ('wait', None)])
        source.kill.assert_not_called()
        self.assertIs(importer.call_args.kwargs['stdin'], source.stdout)
        self.assertEqual(importer.call_args.kwargs['timeout'], 180)
        self.assertEqual(records[0]['tag'], 'synthetic:fixture')
        self.assertEqual(json.loads(fixture.attempt.encrypt.call_args.args[1])['saveExit'], 0)

    def test_vendor_save_timeout_kills_reaps_and_reaches_fixture_cleanup_without_stderr_read(self):
        from rehearse import rehearse
        fixture, records = self.fixture()
        source, events = Mock(), []
        def wait(timeout=None):
            events.append(('wait', timeout))
            if timeout is not None:
                raise subprocess.TimeoutExpired('docker image save', timeout)
            return 0
        source.wait.side_effect = wait
        source.poll.return_value = None
        source.kill.side_effect = lambda: events.append('kill')
        source.stderr.read.side_effect = AssertionError('stderr read would block')
        fixture.start = Mock()
        fixture.execute = lambda: fixture.load_vendor_images(['synthetic:fixture'])
        fixture.diagnostics = Mock()
        fixture.cleanup = Mock(side_effect=lambda: events.append('fixture-cleanup') or True)
        with tempfile.TemporaryDirectory() as temporary:
            source_inventory = Path(temporary) / 'source.json'
            source_inventory.write_bytes(canonical({'sourceClusterUid': 'synthetic-source', 'nativeEndpoint': 'https://192.0.2.10:6443'}))
            fixture.args.source_inventory = source_inventory
            fixture.args.project_root = Path(temporary)
            fixture.args.evidence_root = Path(temporary) / 'private'
            identities = {'runtimeVersionOutput': {'kind': {'exitCode': 0}, 'docker': {'exitCode': 0}}}
            with patch('rehearse.Attempt', return_value=fixture.attempt), \
                 patch('rehearse.tool_identities', return_value=identities), \
                 patch.object(fixture, 'run', return_value=SimpleNamespace(stdout=b'[{"Id":"sha256:synthetic"}]')), \
                 patch('rehearse.subprocess.Popen', return_value=source), \
                 patch('rehearse.subprocess.run', return_value=SimpleNamespace(returncode=0, stdout=b'', stderr=b'')):
                _, state = rehearse(fixture.args, fixture_type=lambda args, attempt: fixture)
        self.assertEqual(events, [('wait', 30), 'kill', ('wait', None), 'fixture-cleanup'])
        source.stderr.read.assert_not_called()
        source.kill.assert_called_once()
        source.stdout.close.assert_called_once()
        fixture.cleanup.assert_called_once()
        fixture.attempt.finish.assert_called_once()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(records['failure.json']['failureStep'], 'load-vendor-image')
        self.assertEqual(records['summary.json']['state'], 'failed-closed')


if __name__ == '__main__':
    unittest.main()
