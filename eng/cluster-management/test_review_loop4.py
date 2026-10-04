"""Regression evidence for loop 4's independently identified gaps and decided retirement edits."""
import base64
import copy
import json
from pathlib import Path
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

from evidence import Attempt, canonical, digest, file_digest, ssh_recipient_tag
from qualify import Capture, classify, collect, management_resource, project_resource, validate_ownership
from rehearse import (Fixture, FIXTURE_FINALIZER_NAMES, PRODUCTION_FINALIZER_NAMES, SYSTEM_WORKSPACE_FINALIZER,
                      assert_phase, content_review_digest, dependency_plan, key, retirement_scope, validate_allowlist)
import test_rehearse as existing


class CensusRegressionTests(unittest.TestCase):
    def test_helm_annotations_bind_native_objects_to_release_and_release_namespace(self):
        raw = {'apiVersion': 'rbac.authorization.k8s.io/v1', 'kind': 'ClusterRoleBinding', 'metadata': {
            'name': 'manager', 'uid': 'grant-1', 'resourceVersion': '7', 'annotations': {
                'meta.helm.sh/release-name': 'ks-core', 'meta.helm.sh/release-namespace': 'kubesphere-system',
                'private': 'DO-NOT-PUBLISH'}}}
        projected = project_resource(raw)
        self.assertEqual(projected['helmRelease'], {'release-name': 'ks-core', 'release-namespace': 'kubesphere-system'})
        self.assertTrue(management_resource(projected))
        self.assertEqual(retirement_scope([projected]), {'grant-1'})
        self.assertNotIn('DO-NOT-PUBLISH', json.dumps(projected))

    def test_classification_resolves_actual_owners_and_records_actual_dependents(self):
        parent = project_resource({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {
            'name': 'manager', 'namespace': 'kubesphere-system', 'uid': 'parent'}})
        child = project_resource({'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {
            'name': 'child', 'namespace': 'kubesphere-system', 'uid': 'child',
            'ownerReferences': [{'uid': 'parent', 'kind': 'ConfigMap', 'name': 'manager'}]}})
        entries = {e['resource']['uid']: e for e in classify([parent, child])}
        self.assertTrue(entries['child']['ownersResolved'])
        self.assertEqual(entries['parent']['observedOwnedDependents'], [
            {'uid': 'child', 'kind': 'Secret', 'namespace': 'kubesphere-system', 'name': 'child'}])
        self.assertFalse(classify([child])[0]['ownersResolved'])
        self.assertIn('management-inventory-empty', validate_ownership([]))

    def test_crd_group_and_helm_namespace_each_classify_an_otherwise_unmarked_resource(self):
        crd = project_resource({'apiVersion': 'apiextensions.k8s.io/v1', 'kind': 'CustomResourceDefinition',
            'metadata': {'name': 'widgets.sample.kubesphere.io', 'uid': 'crd'},
            'spec': {'group': 'sample.kubesphere.io', 'names': {'plural': 'widgets'}, 'versions': []}})
        binding = project_resource({'apiVersion': 'rbac.authorization.k8s.io/v1', 'kind': 'ClusterRoleBinding',
            'metadata': {'name': 'isolated', 'uid': 'binding', 'annotations': {
                'meta.helm.sh/release-namespace': 'kubesphere-controls-system'}}})
        self.assertEqual({e['resource']['uid'] for e in classify([crd, binding])}, {'crd', 'binding'})
        crd['customResource']['group'] = 'sample.example.io';binding['helmRelease'] = {}
        self.assertFalse(management_resource(crd) or management_resource(binding))

    def test_malformed_owner_finalizer_and_namespace_metadata_each_leave_coverage_gap(self):
        valid = {'metadata': {'name': 'native', 'uid': 'native-1', 'resourceVersion': '7'}}
        cases = ({'ownerReferences': {}}, {'ownerReferences': [{'uid': None}]}, {'ownerReferences': [None]},
                 {'finalizers': {}}, {'finalizers': ['not a safe finalizer']}, {'namespace': 'bad namespace'})
        for malformed in cases:
            with self.subTest(metadata=malformed):
                capture = Capture(SimpleNamespace(), None)
                raw = copy.deepcopy(valid);raw['metadata'].update(malformed)
                with patch.object(capture, 'raw', return_value={'items': [raw]}):
                    capture.list_resource('v1', '/api/v1', {'name': 'configmaps', 'kind': 'ConfigMap'}, 'objects')
                self.assertEqual(capture.inventory, [])
                self.assertEqual(capture.coverage, [{'step': 'objects-object', 'state': 'invalid-schema', 'exitCode': 0}])

    def test_malformed_helm_entry_leaves_gap_instead_of_empty_accepted_list(self):
        capture = Capture(SimpleNamespace(helm='helm', kubeconfig='native', context='context'), None)
        for entry in (None, {}, {'name': 'manager', 'namespace': 'kubesphere-system', 'chart': 'ks-core', 'status': None}):
            with self.subTest(entry=entry), patch.object(capture, 'command', return_value=[entry]):
                capture.coverage = []
                self.assertEqual(capture.helm_releases(), [])
                self.assertEqual(capture.coverage, [{'step': 'helm-releases', 'state': 'invalid-schema', 'exitCode': 0}])

    def capture(self, groups, releases):
        records, commands = {}, []
        config = {'clusters': [{'cluster': {'server': 'https://127.0.0.1:6443',
                  'certificate-authority-data': base64.b64encode(b'synthetic-ca').decode()}}]}
        args = SimpleNamespace(kubeconfig=Path(__file__), age='age', recipient='public', context='context', helm='helm')
        capture = Capture(args, SimpleNamespace(encrypt=Mock(), record=lambda n, v: records.update({n: v})))
        def command(name, argv, parse=True):
            commands.append((name, argv, parse))
            return []
        with patch.object(capture, 'kube', side_effect=[
                {'clientVersion': {'gitVersion': 'v1.34.12'}, 'serverVersion': {'gitVersion': 'v1.34.9'}}, config]), \
             patch.object(capture, 'raw', side_effect=[{'resources': []}, {'groups': groups}]), \
             patch.object(capture, 'helm_releases', return_value=releases), patch.object(capture, 'command', side_effect=command), \
             patch('qualify.ssl.create_default_context'), patch('qualify.urllib.request.urlopen', side_effect=OSError):
            capture.collect()
        return capture, records, commands

    def test_missing_group_preferred_version_is_coverage_gap_and_helm_exports_are_executed(self):
        release = {'name': 'ks-core', 'namespace': 'kubesphere-system', 'chart': 'ks-core-1.2.4', 'status': 'deployed'}
        for group in ({}, {'preferredVersion': None}, {'preferredVersion': {'groupVersion': 'unsafe group'}}):
            with self.subTest(group=group):
                capture, _, commands = self.capture([group], [release])
                self.assertIn({'step': 'group-preferred-version', 'state': 'invalid-schema', 'exitCode': 0}, capture.coverage)
                self.assertEqual([v[0] for v in commands], ['helm-values-ks-core', 'helm-manifest-ks-core', 'helm-hooks-ks-core'])
                for verb, (_, argv, parse) in zip(('values', 'manifest', 'hooks'), commands):
                    self.assertEqual(argv[argv.index('get') + 1], verb)
                    self.assertEqual(parse, verb == 'values')
                    self.assertEqual(argv[argv.index('-n') + 1], 'kubesphere-system')

    def test_census_attempt_binds_its_script_and_imported_module(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp);project = base / 'repo';maintenance = project / 'eng/kubernetes-upgrade/MAINTENANCE.md'
            maintenance.parent.mkdir(parents=True);maintenance.write_text('retained proposal')
            tool = base / 'tool';tool.write_bytes(b'tool')
            args = SimpleNamespace(project_root=project, evidence_root=base / 'private', attempt_id='procedure-test',
                operator='operator', context='context', kubectl=tool, helm=tool, age=tool, kubeconfig=tool)
            with patch('qualify.subprocess.run', return_value=SimpleNamespace(returncode=1, stdout=b'', stderr=b'')):
                directory, state = collect(args)
            record = json.loads((directory / 'attempt.json').read_text())
            self.assertEqual(state, 'failed-closed')
            self.assertEqual(record['scriptSha256'], file_digest(Path(__file__).with_name('qualify.py')))
            self.assertEqual(record['moduleSha256'], {'evidence.py': file_digest(Path(__file__).with_name('evidence.py'))})


class FixtureRegressionTests(unittest.TestCase):
    def fixture(self):
        records = {}
        attempt = SimpleNamespace(directory=Path('/nonexistent'), encrypt=Mock(), record=lambda n, v: records.update({n: v}))
        return Fixture(SimpleNamespace(attempt_id='loop4', age='age', recipient='public'), attempt), records

    def test_fixture_listing_includes_served_custom_resources_and_excludes_unserved_or_other_groups(self):
        fixture, _ = self.fixture()
        crd = lambda name, group, served: {'name': name, 'customResource': {'group': group, 'versions': [{'served': served}]}}
        with patch.object(fixture, 'crd_inventory', return_value=[crd('users.iam.kubesphere.io', 'iam.kubesphere.io', True),
                crd('hidden.iam.kubesphere.io', 'iam.kubesphere.io', False), crd('other.example.io', 'example.io', True)]), \
             patch.object(fixture, 'get', return_value={'items': [{'apiVersion': 'iam.kubesphere.io/v1beta1', 'kind': 'User',
                'metadata': {'name': 'admin', 'uid': 'user', 'resourceVersion': '1'}}]}) as get:
            inventory = fixture.kubesphere_inventory()
        self.assertEqual(inventory[0]['kind'], 'User')
        self.assertIn('users.iam.kubesphere.io', get.call_args.args[0].split(','))
        self.assertNotIn('hidden.iam.kubesphere.io', get.call_args.args[0].split(','))
        self.assertNotIn('other.example.io', get.call_args.args[0].split(','))

    def test_disallowed_fixture_exit_is_encrypted_then_refused_and_allowed_exit_is_returned(self):
        fixture, _ = self.fixture()
        for code in (1, 2, 137):
            result = SimpleNamespace(returncode=code, stdout=b'PRIVATE', stderr=b'PRIVATE-ERROR')
            with self.subTest(exit=code), patch('rehearse.subprocess.run', return_value=result):
                with self.assertRaisesRegex(ValueError, 'fixture-command-failed-probe'):
                    fixture.run('probe', ['command'])
                self.assertEqual(json.loads(fixture.attempt.encrypt.call_args.args[1])['exitCode'], code)
                self.assertIs(fixture.run('accepted-probe', ['command'], allowed=(code,)), result)

    def test_health_gate_and_propagation_probe_run_before_a_passing_retirement_result(self):
        fixture, records = self.fixture()
        fixture.args.ks_chart_sha256 = 'c' * 64;fixture.args.source_digest = 's' * 64;fixture.args.script_digest = 'p' * 64
        namespace = existing.ks('v1', 'Namespace', 'kept', 'ns')
        for health_error in (None, ValueError('health-rejected')):
            records.clear()
            with patch.object(fixture, 'populate_retirement_decisions'), \
                 patch.object(fixture, 'probe_workspace_propagation') as probe, \
                 patch.object(fixture, 'crd_inventory', return_value=[]), \
                 patch.object(fixture, 'settled_inventory', return_value=([namespace], True)), \
                 patch.object(fixture, 'discover', return_value={}), \
                 patch.object(fixture, 'post_retirement_checks', side_effect=health_error, return_value={}) as health:
                if health_error:
                    with self.assertRaisesRegex(ValueError, 'health-rejected'):fixture.retire_kubesphere()
                    self.assertNotIn('kubesphere-retirement-result.json', records)
                else:
                    fixture.retire_kubesphere()
                    self.assertEqual(records['kubesphere-retirement-result.json']['state'], 'passed-dependency-first-native-retirement')
                probe.assert_called_once();health.assert_called_once()

    def test_deleted_or_terminating_probe_members_are_recorded_as_cascade_hazards(self):
        for final in ('deleted', 'terminating', 'stuck'):
            with self.subTest(final=final):
                fixture, records = self.fixture()
                def obj(kind, name, uid, **meta):
                    return {'apiVersion': 'v1' if kind == 'Namespace' else 'tenant.kubesphere.io/v1beta1',
                            'kind': kind, 'metadata': {'name': name, 'uid': uid, 'resourceVersion': '1', **meta}}
                template = obj('WorkspaceTemplate', 's426-probe', 't')
                workspace = obj('Workspace', 's426-probe', 'w')
                member = obj('Namespace', 's426-probe-member', 'n', finalizers=[SYSTEM_WORKSPACE_FINALIZER])
                deleting = copy.deepcopy(member);deleting['metadata']['deletionTimestamp'] = '2026-10-03T00:00:00Z'
                reads = iter([workspace, member, template, None, None, None if final == 'deleted' else deleting])
                def read(path):
                    return next(reads, deleting if final == 'stuck' else None)
                with patch.object(fixture, 'create'), patch.object(fixture, 'native_read', side_effect=read), \
                     patch.object(fixture, 'native_retire', return_value={'outcome': 'native-delete-accepted'}), \
                     patch('rehearse.time.sleep'), patch('rehearse.time.monotonic', side_effect=__import__('itertools').count(0, 20)):
                    if final == 'stuck':
                        with self.assertRaisesRegex(ValueError, 'workspace-probe-namespace-stuck'):fixture.probe_workspace_propagation()
                    else:fixture.probe_workspace_propagation()
                record = records['workspace-propagation-probe.json']
                self.assertEqual(record['memberNamespaceOutcome'], 'deleted' if final == 'deleted' else 'terminating')
                self.assertTrue(record['cascadeHazard'])
                self.assertEqual(record['memberNamespaceFinallyAbsent'], final != 'stuck')
                if final == 'stuck':
                    self.assertNotIn('workspace-probe-quota-cleanup.json', records)
                else:
                    cleanup = records['workspace-probe-quota-cleanup.json']
                    self.assertEqual(cleanup['state'], 'passed-synthetic-only-cleanup')
                    self.assertEqual(cleanup['name'], 'io.kubesphere.license.quota.v3.workspace.t')
                    self.assertEqual(cleanup['templateUid'], template['metadata']['uid'])
                    self.assertEqual(cleanup['absenceSamples'], [
                        {'templateAbsent': True, 'workspaceAbsent': True, 'quotaAbsent': True}] * 2)

    def test_console_phase_contains_exact_roots_and_certificate_cascade_then_namespaces_are_kept(self):
        baseline = existing.kubesphere_baseline()
        baseline += [existing.ks(api, kind, name, uid, 'kubesphere-system', owner=owner) for api, kind, name, uid, owner in (
            ('networking.k8s.io/v1', 'Ingress', 'kubesphere-console', 'route', None),
            ('cert-manager.io/v1', 'Certificate', 'kubesphere-console-letsencrypt', 'cert', None),
            ('cert-manager.io/v1', 'CertificateRequest', 'request', 'request', 'cert'),
            ('acme.cert-manager.io/v1', 'Order', 'order', 'order', 'request'),
            ('v1', 'Secret', 'kubesphere-console-letsencrypt-tls', 'tls', None),
            ('coordination.k8s.io/v1', 'Lease', 'ks-controller-manager-leader-election', 'lease', None))]
        baseline += [existing.ks('v1', 'Namespace', n, n, finalizers=[SYSTEM_WORKSPACE_FINALIZER, 'other/retain'])
                     for n in FIXTURE_FINALIZER_NAMES]
        scope = retirement_scope(baseline);plan = dependency_plan(baseline, scope)
        console = next(p for p in plan if p['phase'] == 'console-route')
        self.assertEqual(set(console['expected']), {'route', 'cert', 'request', 'order', 'tls', 'lease'})
        self.assertEqual(set(console['roots']), {'route', 'cert', 'tls', 'lease'})
        self.assertTrue(console['managerAbsentRequired'])
        phase = plan[-1];self.assertEqual(phase['phase'], 'namespace-finalizers')
        self.assertEqual(phase['expected'], []);self.assertEqual(set(phase['expectedModified']), {'ns', *FIXTURE_FINALIZER_NAMES})
        self.assertTrue(set(phase['expectedModified']).isdisjoint(scope))

    def test_named_namespace_intervention_preserves_identity_and_other_metadata_and_never_deletes(self):
        fixture, _ = self.fixture()
        current = {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': 'default', 'uid': 'ns', 'resourceVersion': '9',
                   'finalizers': [SYSTEM_WORKSPACE_FINALIZER, 'other/retain'], 'labels': {'application': 'keep'}},
                   'spec': {'finalizers': ['kubernetes']}}
        reviewed = project_resource(current);reviewed['resourceVersion'] = '7'
        fixture.reviewed_content_digests['ns'] = content_review_digest(current)
        with patch.object(fixture, 'native_read', return_value=current), patch.object(fixture, 'kube') as kube:
            result = fixture.remove_namespace_finalizer('/api/v1/namespaces/default', reviewed)
        self.assertFalse(result['deleteRequested']);self.assertEqual(kube.call_args.args[1], 'replace')
        updated = kube.call_args.kwargs['obj'];self.assertEqual(updated['metadata']['finalizers'], ['other/retain'])
        self.assertEqual(updated['spec'], current['spec']);self.assertEqual(updated['metadata']['labels'], current['metadata']['labels'])
        modifications = {key(reviewed): SYSTEM_WORKSPACE_FINALIZER}
        assert_phase({'ns'}, [reviewed], [project_resource(updated)], set(), modifications)
        for changed in ({'uid': 'other'}, {'deletionTimestamp': '2026-10-03T00:00:00Z'}, {'namespaceFinalizers': []}, {'finalizers': []}):
            with self.subTest(change=changed), self.assertRaises(ValueError):
                assert_phase({'ns'}, [reviewed], [{**project_resource(updated), **changed}], set(), modifications)
        for name in ('unreviewed-namespace', 's426-workload'):
            bad = copy.deepcopy(reviewed);bad['name'] = name
            with self.assertRaisesRegex(ValueError, 'protected-resource'):
                validate_allowlist([bad], [{**bad, 'action': 'remove-named-finalizer', 'finalizer': SYSTEM_WORKSPACE_FINALIZER,
                                         'propagation': 'Foreground'}])


class ReadbackTests(unittest.TestCase):
    def test_two_recipients_and_readback_digest_are_verified_before_writing_and_published(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp);project = base / 'repo';project.mkdir(mode=0o700)
            identity = base / 'readback';identity.write_bytes(b'private test identity');identity.chmod(0o600)
            administrator = 'ssh-ed25519 ' + base64.b64encode(b'administrator-public-blob').decode()
            second = 'ssh-ed25519 ' + base64.b64encode(b'readback-public-blob').decode()
            tags = [ssh_recipient_tag(p) for p in (administrator, second)]
            ciphertext = ('age-encryption.org/v1\n' + ''.join('-> ssh-ed25519 ' + t + ' share\nwrapped\n' for t in tags)
                          + '--- mac\nciphertext').encode()
            attempt = Attempt(project, base / 'private', 'readback-test', readback_recipient=second, readback_identity=identity)
            encrypted = SimpleNamespace(returncode=0, stdout=ciphertext, stderr=b'')
            good = SimpleNamespace(returncode=0, stdout=b'PRIVATE', stderr=b'')
            with patch('evidence.subprocess.run', side_effect=[encrypted, good]) as run:
                entry = attempt.encrypt('raw', b'PRIVATE', 'age', administrator)
            self.assertTrue(entry['readbackVerified'])
            self.assertEqual(run.call_args_list[0].args[0], ['age', '--encrypt', '--recipient', administrator, '--recipient', second])
            self.assertEqual(run.call_args_list[1].args[0], ['age', '--decrypt', '--identity', str(identity)])
            self.assertEqual(run.call_args_list[1].kwargs['input'], ciphertext)
            for name, bad in (('mismatch', SimpleNamespace(returncode=0, stdout=b'WRONG', stderr=b'')),
                              ('failed', SimpleNamespace(returncode=1, stdout=b'PRIVATE', stderr=b''))):
                with self.subTest(case=name), patch('evidence.subprocess.run', side_effect=[encrypted, bad]):
                    with self.assertRaisesRegex(ValueError, 'export-readback-failed'):attempt.encrypt(name, b'PRIVATE', 'age', administrator)
                    self.assertFalse((attempt.directory / (name + '.age')).exists())
            missing = SimpleNamespace(returncode=0, stdout=ciphertext.replace(('-> ssh-ed25519 ' + tags[1]).encode(), b'-> X25519'), stderr=b'')
            with patch('evidence.subprocess.run', return_value=missing):
                with self.assertRaisesRegex(ValueError, 'export-second-recipient-missing'):attempt.encrypt('missing', b'PRIVATE', 'age', administrator)
            attempt.finish()
            export = json.loads((project / '_bmad-output/implementation-artifacts/evidence/epic-4/4-26/readback-test/encrypted-exports.json').read_text())
            self.assertTrue(export['readbackVerified']);self.assertTrue(export['exports'][0]['readbackVerified'])
            self.assertEqual({s['tag'] for s in export['exports'][0]['recipientStanzas']}, set(tags))
            self.assertNotIn('plaintextSha256', json.dumps(export));self.assertFalse(export['offNodeCustodyAccepted'])


class RollbackTests(unittest.TestCase):
    def test_snapshot_retains_only_fixture_inputs_and_records_matching_tools(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp);tools = {}
            for name in ('etcdctl', 'etcdutl'):
                tools[name] = base / name;tools[name].write_bytes(name.encode())
            records = {};attempt = SimpleNamespace(directory=base, encrypt=Mock(), record=lambda n, v: records.update({n: v}))
            fixture = Fixture(SimpleNamespace(attempt_id='snapshot', age='age', recipient='public', **tools), attempt)
            status = {'revision': 42, 'hash': 123, 'totalKey': 10}
            def run(name, argv, **kwargs):
                value = canonical(status) if name == 'rollback-snapshot-status' else b'synthetic-snapshot' if name == 'rollback-snapshot-export' else b'synthetic-archive' if name == 'rollback-node-archive' else b'synthetic-canary'
                return SimpleNamespace(returncode=0, stdout=value, stderr=b'')
            with patch.object(fixture, 'copy_etcd_tools'), patch.object(fixture, 'run', side_effect=run), \
                 patch.object(fixture, 'etcd_command', return_value=SimpleNamespace(returncode=0,
                    stdout=canonical([{'Status': {'header': {'cluster_id': 1, 'member_id': 2}}}]), stderr=b'')) as ctl:
                fixture.capture_rollback([], [])
            self.assertEqual(ctl.call_args_list[0].args[1:], ('snapshot', 'save', '/var/lib/s426-rollback.db'))
            self.assertEqual([c.args[0] for c in attempt.encrypt.call_args_list], ['rollback-snapshot', 'rollback-node-inputs'])
            self.assertEqual(fixture.rollback_snapshot, b'synthetic-snapshot')
            self.assertEqual(fixture.rollback_archive, b'synthetic-archive')
            self.assertEqual(records['rollback-snapshot.json']['snapshotStatus'], status)
            self.assertEqual(records['rollback-snapshot.json']['toolSha256'], {n: digest(n.encode()) for n in tools})
            self.assertFalse(records['rollback-snapshot.json']['productionRecoveryAccepted'])

    def restore(self, temp, mismatch=None):
        base = Path(temp);records = {};attempt = SimpleNamespace(directory=base, encrypt=Mock(), record=lambda n, v: records.update({n: v}))
        fixture = Fixture(SimpleNamespace(attempt_id='rollback'), attempt)
        fixture.container_id = 'original-container';fixture.node_ip = '172.30.0.2';fixture.network_config = {'Subnet': '172.30.0.0/16'}
        fixture.vendor_images = ['retained-manager-image'];fixture.rollback_archive = b'fresh-fixture-inputs'
        fixture.rollback_snapshot = b'synthetic-etcd-snapshot';fixture.rollback_canary = b'synthetic-canary'
        namespace = existing.ks('v1', 'Namespace', 'kube-system', 'fixture-source-uid')
        fixture.rollback_baseline = [namespace];fixture.rollback_crds = [];fixture.rollback_status = {'revision': 42}
        fixture.rollback_datastore = {'header': {'cluster_id': 1, 'member_id': 2}}
        target = Mock(container_id='fresh-container' if mismatch != 'container' else fixture.container_id,
                      node_ip=fixture.node_ip, node=fixture.node, cluster=fixture.cluster)
        target.kube.return_value = SimpleNamespace(returncode=0, stdout=b'ok', stderr=b'')
        target.kubesphere_inventory.return_value = [{**namespace, 'uid': 'wrong'}] if mismatch == 'uid' else [namespace]
        target.crd_inventory.return_value = []
        target.etcd_command.return_value = SimpleNamespace(returncode=0, stderr=b'',
            stdout=canonical([{'Status': {'header': {'cluster_id': 3 if mismatch != 'datastore' else 1, 'member_id': 4}}}]))
        live = {'containers': [{'metadata': {'name': n}, 'state': 'CONTAINER_RUNNING'} for n in (
            'ks-apiserver', 'ks-controller-manager', 'ks-console', 'extensions-museum', 'ks-console-embed')]}
        canary_state = {}
        def run(name, *argv, **kwargs):
            if name == 'restore-synthetic-canary':
                canary_state['bytes'] = kwargs.get('input')
            if name == 'restored-synthetic-canary-readback':
                stored = canary_state.get('bytes')
                if mismatch == 'missing-canary':stored = None
                elif mismatch == 'changed-canary' and isinstance(stored, bytes):stored += b'\n'
                exists = isinstance(stored, bytes)
                return SimpleNamespace(returncode=0 if exists else 1, stdout=stored if exists else b'',
                    stderr=b'' if exists else b'missing synthetic file')
            return SimpleNamespace(returncode=0, stdout=b'a' * 64 + b'\n' if name == 'fresh-pod-sandboxes' else
                                   canonical(live) if name == 'restored-live-containers' else b'', stderr=b'')
        def receive_run(name, *argv, **kwargs):
            # Inject mutations before the fake write receives its input; readback still observes only stored bytes.
            if name == 'restore-synthetic-canary':
                if mismatch == 'missing-write-input':kwargs.pop('input', None)
                elif mismatch == 'changed-write-input':kwargs['input'] = b'altered-write-input'
            return run(name, *argv, **kwargs)
        target.run.side_effect = receive_run
        with patch.object(fixture, 'cleanup', return_value=mismatch != 'cleanup') as cleanup, \
             patch('rehearse.Fixture', return_value=target) as allocation:
            try:fixture.restore_rollback();error = None
            except ValueError as e:error = str(e)
        return error, records, cleanup, allocation, target

    def test_rollback_requires_retired_node_cleanup_and_a_fresh_node(self):
        for mismatch, reason in (('cleanup', 'rollback-retired-node-cleanup-failed'), ('container', 'rollback-fresh-node-identity-unverified')):
            with tempfile.TemporaryDirectory() as temp:
                error, records, cleanup, allocation, target = self.restore(temp, mismatch)
                self.assertEqual(error, reason);self.assertNotIn('rollback-restore-result.json', records)
                if mismatch == 'cleanup':allocation.assert_not_called()
                target.load_vendor_images.assert_not_called()

    def test_restore_proves_baseline_uids_running_containers_and_new_etcd_member_and_refuses_mismatches(self):
        for mismatch in (None, 'uid', 'datastore'):
            with tempfile.TemporaryDirectory() as temp:
                error, records, _, _, target = self.restore(temp, mismatch)
                record = records['rollback-restore-result.json']
                self.assertEqual(record['state'], 'passed' if mismatch is None else 'failed')
                self.assertEqual(error, None if mismatch is None else 'rollback-baseline-or-datastore-identity-mismatch')
                self.assertEqual(len(record['runningKubeSphereContainers']), 5)
                self.assertEqual(len(record['readyKubeSphereDeployments']), 5)
                self.assertTrue(record['syntheticCanaryRestoredAndReadbackEqual'])
                self.assertEqual(record['syntheticCanaryReadbackRecord'], 'rollback-canary-readback.json')
                self.assertTrue(records['rollback-canary-readback.json']['exactCapturedVolumeBytesRestored'])
                restore_call = next(c for c in target.run.call_args_list if c.args[0] == 'etcd-snapshot-restore')
                self.assertIn('--bump-revision=1000000000', restore_call.args[1]);self.assertIn('--mark-compacted', restore_call.args[1])
                self.assertNotIn('--skip-hash-check', restore_call.args[1])
                self.assertFalse(record['productionRecoveryAccepted'])

    def test_restore_refuses_missing_or_changed_fresh_target_canary_and_retains_readback(self):
        for mismatch, code in (('missing-canary', 1), ('changed-canary', 0),
                               ('missing-write-input', 1), ('changed-write-input', 0)):
            with self.subTest(mismatch=mismatch), tempfile.TemporaryDirectory() as temp:
                error, records, _, _, target = self.restore(temp, mismatch)
                self.assertEqual(error, 'rollback-synthetic-canary-readback-mismatch')
                self.assertNotIn('rollback-restore-result.json', records)
                readback = records['rollback-canary-readback.json']
                self.assertFalse(readback['passed']);self.assertFalse(readback['exactCapturedVolumeBytesRestored'])
                self.assertEqual(readback['readbackExitCode'], code)
                self.assertNotEqual(readback['expectedSha256'], readback['readbackSha256'])
                read_call = next(c for c in target.run.call_args_list if c.args[0] == 'restored-synthetic-canary-readback')
                self.assertEqual(read_call.args[1], ['docker', 'exec', target.node, 'cat', '/var/local/s426-synthetic/canary'])
                self.assertEqual(read_call.kwargs['allowed'], (0, 1))
                target.kube.assert_not_called()
                self.assertFalse(any(c.args[0] == 'start-restored-kubelet' for c in target.run.call_args_list))

    def test_final_cleanup_reaches_owned_rollback_node_after_restore_failure_and_keeps_failed_cleanup_closed(self):
        records = {};attempt = SimpleNamespace(directory=Path('/nonexistent'), encrypt=Mock(), record=lambda n, v: records.update({n: v}))
        fixture = Fixture(SimpleNamespace(attempt_id='restore-failure'), attempt)
        fixture.rollback_fixture = Mock()
        for passed in (True, False):
            fixture.rollback_fixture.cleanup.return_value = passed
            self.assertEqual(fixture.cleanup(), passed)
            self.assertEqual(records['cleanup.json'], {'passed': passed, 'scope': 'fresh rollback node',
                'retiredNodeCleanup': 'retired-node-cleanup.json', 'details': 'rollback-cleanup.json'})
        self.assertEqual(fixture.rollback_fixture.cleanup.call_count, 2)


class FixtureBootstrapTests(unittest.TestCase):
    def test_host_readiness_reconcile_waits_for_native_service_and_invalid_or_failed_service_closes_gate(self):
        fixture = Fixture(SimpleNamespace(attempt_id='host-ready'), SimpleNamespace(directory=Path('/nonexistent')))
        calls = []
        responses = iter([SimpleNamespace(returncode=1, stdout=b'', stderr=b''),
                          SimpleNamespace(returncode=0, stdout=b'{"gitVersion":"v4.2.1"}', stderr=b'')])
        def kube(name, *args, **kwargs):
            calls.append((name, args));return next(responses) if name == 'kubesphere-native-version-ready' else None
        with patch.object(fixture, 'kube', side_effect=kube), patch('rehearse.time.sleep'):
            fixture.refresh_fixture_host_readiness()
        self.assertEqual([v[0] for v in calls], ['kubesphere-native-version-ready', 'kubesphere-native-version-ready', 'refresh-fixture-host-readiness'])
        self.assertIn('clusters.cluster.kubesphere.io', calls[-1][1]);self.assertIn('host', calls[-1][1])
        for result, reason in ((SimpleNamespace(returncode=0, stdout=b'{}', stderr=b''), 'fixture-kubesphere-version-response-invalid'),
                               (SimpleNamespace(returncode=1, stdout=b'', stderr=b''), 'fixture-kubesphere-service-not-ready')):
            calls.clear()
            with patch.object(fixture, 'kube', side_effect=lambda name, *a, **k: calls.append(name) or result), \
                 patch('rehearse.time.sleep'), patch('rehearse.time.monotonic', side_effect=__import__('itertools').count(0, 70)):
                with self.assertRaisesRegex(ValueError, reason):fixture.refresh_fixture_host_readiness()
            self.assertNotIn('refresh-fixture-host-readiness', calls)
