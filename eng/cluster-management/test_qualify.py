"""Evidence disclosure, coverage and fail-closed qualification boundaries."""
import copy
import base64
import io
import json
import os
from pathlib import Path
import stat
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

from evidence import Attempt, canonical, digest, now
from qualify import (Capture, classify, collect, compatible, management_resource, project_resource, validate_authority,
                     validate_native_endpoint, validate_ownership, validate_pins)
import urllib.error


def sealed(event):
    return {**event, 'sha256': digest(canonical(event))}


class ProjectionTests(unittest.TestCase):
    def test_explicit_management_keys_and_finalizers_classify_native_objects(self):
        objects = [{'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {
            'name': 'native', 'uid': 'label-1', 'labels': {'iam.kubesphere.io/role': 'viewer',
                                                        'private': 'DO NOT PUBLISH'}}},
            {'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'name': 'native-secret', 'uid': 'finalizer-1',
             'finalizers': ['kubesphere.io/cleanup']}}]
        projected = [project_resource(v) for v in objects]
        self.assertEqual(projected[0]['managementLabels'], {'iam.kubesphere.io/role': 'viewer'})
        self.assertEqual({v['resource']['uid'] for v in classify(projected)}, {'label-1', 'finalizer-1'})
        self.assertNotIn('DO NOT PUBLISH', json.dumps(projected))

    def test_secret_and_config_consumers_project_only_reference_identities(self):
        raw = {'apiVersion': 'v1', 'kind': 'Pod', 'metadata': {'name': 'consumer', 'uid': 'pod-1'}, 'spec': {
            'volumes': [{'secret': {'secretName': 'volume-secret', 'items': [{'key': 'PRIVATE-KEY'}]}},
                        {'configMap': {'name': 'volume-config'}}, {'projected': {'sources': [
                            {'secret': {'name': 'projected-secret'}}, {'configMap': {'name': 'projected-config'}}]}}],
            'containers': [{'name': 'main', 'env': [{'name': 'PRIVATE-ENV', 'value': 'DO NOT PUBLISH'},
                {'valueFrom': {'secretKeyRef': {'name': 'env-secret', 'key': 'PRIVATE-KEY'}}},
                {'valueFrom': {'configMapKeyRef': {'name': 'env-config', 'key': 'PRIVATE-KEY'}}}],
                'envFrom': [{'secretRef': {'name': 'envfrom-secret'}}, {'configMapRef': {'name': 'envfrom-config'}}]}],
            'initContainers': [{'envFrom': [{'secretRef': {'name': 'init-secret'}}]}],
            'ephemeralContainers': [{'envFrom': [{'secretRef': {'name': 'debug-secret'}}]}],
            'imagePullSecrets': [{'name': 'pull-secret'}]}}
        projected = project_resource(raw)
        self.assertEqual(set(projected['secretReferences']), {'volume-secret', 'projected-secret', 'env-secret',
                         'envfrom-secret', 'init-secret', 'debug-secret', 'pull-secret'})
        self.assertEqual(set(projected['configMapReferences']), {'volume-config', 'projected-config', 'env-config', 'envfrom-config'})
        for value in ('PRIVATE-KEY', 'PRIVATE-ENV', 'DO NOT PUBLISH'):
            self.assertNotIn(value, json.dumps(projected))

    def test_ingress_backends_and_gateway_listeners_preserve_sanitized_consumers(self):
        ingress = project_resource({'apiVersion': 'networking.k8s.io/v1', 'kind': 'Ingress',
            'metadata': {'name': 'web', 'namespace': 'app', 'uid': 'ingress-1'}, 'spec': {
            'defaultBackend': {'service': {'name': 'default-web', 'port': {'number': 80}}},
            'rules': [{'host': 'web.example', 'http': {'paths': [{'path': '/private/path',
                'backend': {'service': {'name': 'web', 'port': {'name': 'http'}}}}]}}],
            'tls': [{'secretName': 'web-cert'}]}})
        self.assertEqual([v['name'] for v in ingress['backends']], ['default-web', 'web'])
        self.assertEqual(ingress['backends'][0]['port'], 80)
        self.assertEqual(ingress['backends'][1]['portName'], 'http')
        self.assertEqual(ingress['secretReferences'], ['web-cert'])
        gateway = project_resource({'apiVersion': 'gateway.networking.k8s.io/v1', 'kind': 'Gateway',
            'metadata': {'name': 'web', 'uid': 'gateway-1'}, 'spec': {'listeners': [{'name': 'https',
            'hostname': 'web.example', 'protocol': 'HTTPS', 'port': 443,
            'tls': {'certificateRefs': [{'kind': 'Secret', 'name': 'gateway-cert', 'namespace': 'app',
                                         'private': 'DO NOT PUBLISH'}]}}]}})
        self.assertEqual(gateway['listeners'][0]['port'], 443)
        self.assertEqual(gateway['listeners'][0]['certificateReferences'][0]['name'], 'gateway-cert')
        self.assertEqual(gateway['routeHosts'], ['web.example'])
        self.assertNotIn('DO NOT PUBLISH', json.dumps(gateway))
        self.assertNotIn('/private/path', json.dumps(ingress))

    def test_terminating_custom_resource_projects_timestamp_and_named_finalizer_only(self):
        raw = {'apiVersion': 'tenant.kubesphere.io/v1beta1', 'kind': 'WorkspaceTemplate',
               'metadata': {'name': 'system-workspace', 'uid': 'held-1', 'resourceVersion': '7',
                            'deletionTimestamp': '2026-10-01T18:40:00Z',
                            'finalizers': ['kubesphere.io/workspacetemplate-protection']},
               'spec': {'privateConfiguration': 'DO-NOT-PUBLISH-SECRET'}}
        projected = project_resource(raw)
        self.assertEqual(projected['deletionTimestamp'], '2026-10-01T18:40:00Z')
        self.assertEqual(projected['finalizers'], ['kubesphere.io/workspacetemplate-protection'])
        self.assertNotIn('DO-NOT-PUBLISH-SECRET', json.dumps(projected))
        raw['metadata'].pop('deletionTimestamp')
        self.assertIsNone(project_resource(raw)['deletionTimestamp'])

    def test_secret_and_configuration_values_never_enter_projection(self):
        for kind in ('Secret', 'ConfigMap', 'ClusterConfiguration', 'ExtensionVersion', 'User'):
            raw = {'apiVersion': 'v1', 'kind': kind, 'metadata': {'name': 'example', 'uid': 'uid-1', 'resourceVersion': '12',
                    'annotations': {'private': 'DO-NOT-PUBLISH-SECRET'}, 'labels': {'private': 'DO-NOT-PUBLISH-SECRET'}},
                   'data': {'password': 'DO-NOT-PUBLISH-SECRET'}, 'stringData': {'key': 'DO-NOT-PUBLISH-SECRET'},
                   'spec': {'config': 'DO-NOT-PUBLISH-SECRET'}, 'status': {'token': 'DO-NOT-PUBLISH-SECRET'}}
            projection = json.dumps(project_resource(raw))
            self.assertNotIn('DO-NOT-PUBLISH-SECRET', projection)
            self.assertNotIn('password', projection)

    def test_token_secret_projects_only_its_service_account_identity(self):
        for annotation in ('kubesphere.io/service-account.name', 'kubernetes.io/service-account.name'):
            raw = {'apiVersion': 'v1', 'kind': 'Secret', 'type': 'kubesphere.io/service-account-token',
                   'metadata': {'name': 'ks-console-x1', 'namespace': 'kubesphere-system', 'uid': 'uid-1', 'resourceVersion': '3',
                                'annotations': {annotation: 'ks-console', 'private': 'DO-NOT-PUBLISH-SECRET'}},
                   'data': {'token': 'DO-NOT-PUBLISH-SECRET'}}
            projection = project_resource(raw)
            self.assertEqual(projection['serviceAccountReference'], 'ks-console')
            self.assertNotIn('DO-NOT-PUBLISH-SECRET', json.dumps(projection))
        unsafe = {'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'name': 'x', 'uid': 'u', 'annotations': {
                  'kubesphere.io/service-account.name': 'not a safe identity; DO-NOT-PUBLISH'}}}
        self.assertIsNone(project_resource(unsafe)['serviceAccountReference'])

    def test_ownership_storage_and_installed_catalog_distinction(self):
        base = {'apiVersion': 'extensions.kubesphere.io/v1alpha1', 'metadata': {'name': 'sample', 'uid': 'uid-1', 'resourceVersion': '2'}}
        self.assertEqual(project_resource({**base, 'kind': 'Extension'})['extensionEvidenceClass'], 'catalog-or-configuration')
        self.assertEqual(project_resource({**base, 'kind': 'InstallPlan'})['extensionEvidenceClass'], 'installation-plan')
        claim = project_resource({'apiVersion': 'v1', 'kind': 'PersistentVolumeClaim', 'metadata': {'name': 'data', 'uid': 'claim-1'},
                                  'spec': {'volumeName': 'pv-1', 'storageClassName': 'local'}, 'status': {'phase': 'Bound'}})
        self.assertEqual(claim['binding']['volumeName'], 'pv-1')
        self.assertEqual(claim['uid'], 'claim-1')

    def test_unlabeled_registrations_backed_by_manager_services_are_management_state(self):
        hook = project_resource({'apiVersion': 'admissionregistration.k8s.io/v1', 'kind': 'ValidatingWebhookConfiguration',
            'metadata': {'name': 'validator.license.example', 'uid': 'hook-1'},
            'webhooks': [{'name': 'quota', 'clientConfig': {'service': {'namespace': 'kubesphere-system', 'name': 'manager'}}}]})
        api = project_resource({'apiVersion': 'apiregistration.k8s.io/v1', 'kind': 'APIService', 'metadata': {'name': 'v1.example', 'uid': 'api-1'},
                                'spec': {'service': {'namespace': 'kubesphere-system', 'name': 'manager'}}})
        crd = project_resource({'apiVersion': 'apiextensions.k8s.io/v1', 'kind': 'CustomResourceDefinition',
            'metadata': {'name': 'widgets.example.io', 'uid': 'crd-1'}, 'spec': {'group': 'example.io', 'names': {'plural': 'widgets'},
            'conversion': {'strategy': 'Webhook', 'webhook': {'clientConfig': {'service': {'namespace': 'kubesphere-system', 'name': 'manager'}}}}}})
        self.assertTrue(all(management_resource(v) for v in (hook, api, crd)))
        native = project_resource({'apiVersion': 'admissionregistration.k8s.io/v1', 'kind': 'ValidatingWebhookConfiguration',
            'metadata': {'name': 'cert-manager', 'uid': 'hook-2'},
            'webhooks': [{'clientConfig': {'service': {'namespace': 'cert-manager', 'name': 'webhook'}}}]})
        local = project_resource({'apiVersion': 'apiregistration.k8s.io/v1', 'kind': 'APIService', 'metadata': {'name': 'v1.apps', 'uid': 'api-2'}, 'spec': {}})
        self.assertFalse(management_resource(native) or management_resource(local))

    def test_unknown_owner_consumer_and_deletion_effects_block_retirement(self):
        item = project_resource({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'manager', 'namespace': 'kubesphere-system',
                                 'uid': 'child-1', 'ownerReferences': [{'uid': 'unknown-1', 'kind': 'Workspace', 'name': 'unknown'}]}})
        errors = validate_ownership(classify([item]))
        self.assertIn('unresolved-owner-or-consumer', errors)
        self.assertIn('unverified-propagation', errors)
        self.assertIn('unresolved-disposition', errors)


class CensusTests(unittest.TestCase):
    def test_malformed_successful_lists_are_coverage_failures(self):
        resource = {'name': 'configmaps', 'kind': 'ConfigMap', 'verbs': ['list']}
        for malformed in ({}, [], {'items': [] , 'metadata': []}, {'items': [], 'metadata': {'continue': 1}},
                          {'items': [{'metadata': {'name': 'omitted', 'resourceVersion': '1'}}]}):
            with self.subTest(payload=malformed):
                capture = Capture(SimpleNamespace(), None)
                with patch.object(capture, 'raw', return_value=malformed):
                    capture.list_resource('v1', '/api/v1', resource, 'list-configmaps')
                self.assertTrue(any(v['state'] == 'invalid-schema' for v in capture.coverage))
                self.assertEqual(capture.inventory, [])
        capture = Capture(SimpleNamespace(), None)
        valid = {'metadata': {'name': 'kept', 'uid': 'kept-1', 'resourceVersion': '2'}}
        with patch.object(capture, 'raw', return_value={'items': [valid, None]}):
            capture.list_resource('v1', '/api/v1', resource, 'list-configmaps')
        self.assertEqual(capture.inventory[0]['uid'], 'kept-1')
        self.assertEqual(capture.inventory[0]['kind'], 'ConfigMap')
        self.assertTrue(any(v['state'] == 'invalid-schema' for v in capture.coverage))

    def test_all_helm_states_paginate_beyond_default_limit(self):
        capture = Capture(SimpleNamespace(helm='helm', kubeconfig='native', context='local'), None)
        first = [{'name': 'release-' + str(v), 'namespace': 'app', 'chart': 'sample-1.0',
                  'status': 'pending-install'} for v in range(256)]
        late = {'name': 'late-manager', 'namespace': 'kubesphere-system', 'chart': 'ks-core-1.2.4', 'status': 'pending-upgrade'}
        with patch.object(capture, 'command', side_effect=[first, [late]]) as command:
            releases = capture.helm_releases()
        self.assertEqual(len(releases), 257)
        self.assertEqual(releases[-1], late)
        for offset, call in zip(('0', '256'), command.call_args_list):
            argv = call.args[1]
            self.assertIn('--all', argv)
            self.assertEqual(argv[argv.index('--max') + 1], '256')
            self.assertEqual(argv[argv.index('--offset') + 1], offset)

    def test_collect_pagination_nonpreferred_crd_and_empty_discovery_coverage(self):
        def native(name, uid, **extra):
            return {'metadata': {'name': name, 'uid': uid, 'resourceVersion': '7', **extra}}
        namespace = native('kube-system', 'ns-1')
        crd = {**native('widgets.sample.kubesphere.io', 'crd-1'), 'spec': {'group': 'sample.kubesphere.io',
               'names': {'plural': 'widgets'}, 'scope': 'Cluster', 'versions': [
                   {'name': 'v1alpha1', 'served': True, 'storage': True}]}}
        widget = native('installed', 'widget-1', ownerReferences=[{'uid': 'ns-1', 'kind': 'Namespace', 'name': 'kube-system'}],
                        finalizers=['sample.kubesphere.io/protect'])
        raw = {'/api/v1': {'resources': [{'name': 'namespaces', 'kind': 'Namespace', 'verbs': ['list']}]},
               '/apis': {'groups': [{'preferredVersion': {'groupVersion': 'apiextensions.k8s.io/v1'}},
                                   {'preferredVersion': {'groupVersion': 'sample.kubesphere.io/v1beta1'}}]},
               '/api/v1/namespaces?limit=500': {'items': [namespace], 'metadata': {'continue': 'a+/='}},
               '/api/v1/namespaces?limit=500&continue=a%2B%2F%3D': {'items': [native('app', 'ns-2')]},
               '/apis/apiextensions.k8s.io/v1': {'resources': [
                   {'name': 'customresourcedefinitions', 'kind': 'CustomResourceDefinition', 'verbs': ['list']}]},
               '/apis/apiextensions.k8s.io/v1/customresourcedefinitions?limit=500': {'items': [crd]},
               '/apis/sample.kubesphere.io/v1beta1': {'resources': []},
               '/apis/sample.kubesphere.io/v1alpha1': {'resources': [{'name': 'widgets', 'kind': 'Widget', 'verbs': ['list']}]},
               '/apis/sample.kubesphere.io/v1alpha1/widgets?limit=500': {'items': [widget]}}
        config = {'clusters': [{'cluster': {'server': 'https://127.0.0.1:6443',
                  'certificate-authority-data': base64.b64encode(b'fixture-cert').decode()}}]}
        for empty in (None, {}, []):
            with self.subTest(preferred=empty):
                responses = copy.deepcopy(raw)
                if empty is not None:
                    responses['/apis/sample.kubesphere.io/v1beta1'] = empty
                args = SimpleNamespace(kubectl='kubectl', helm='helm', context='local', kubeconfig=Path(__file__),
                                       age='age', recipient='public-only')
                records, requests = {}, []
                attempt = SimpleNamespace(encrypt=Mock(), record=lambda name, value: records.update({name: value}))
                capture = Capture(args, attempt)
                def command(argv, **kwargs):
                    if argv[0] == 'helm':
                        data = []
                    elif 'config' in argv:
                        data = config
                    elif '--raw' in argv:
                        path = argv[argv.index('--raw') + 1];requests.append(path);data = responses[path]
                    elif 'version' in argv:
                        data = {'clientVersion': {'gitVersion': 'v1.35.9'}, 'serverVersion': {'gitVersion': 'v1.34.9'}}
                    else:
                        data = config
                    return SimpleNamespace(returncode=0, stdout=json.dumps(data).encode(), stderr=b'')
                with patch('qualify.subprocess.run', side_effect=command), patch('qualify.ssl.create_default_context'), \
                     patch('qualify.urllib.request.urlopen', side_effect=OSError):
                    capture.collect()
                observed = {v['uid']: v for v in capture.inventory}
                self.assertEqual(set(observed), {'ns-1', 'ns-2', 'crd-1', 'widget-1'})
                self.assertEqual(observed['widget-1']['apiVersion'], 'sample.kubesphere.io/v1alpha1')
                self.assertEqual(observed['widget-1']['kind'], 'Widget')
                self.assertEqual(observed['widget-1']['owners'][0]['uid'], 'ns-1')
                self.assertEqual(observed['widget-1']['finalizers'], ['sample.kubesphere.io/protect'])
                self.assertIn('/api/v1/namespaces?limit=500&continue=a%2B%2F%3D', requests)
                self.assertIn('/apis/sample.kubesphere.io/v1alpha1/widgets?limit=500', requests)
                failures = [v for v in capture.coverage if v['state'] != 'observed']
                self.assertEqual(len(failures), 0 if empty is None else 1)
                if failures:self.assertEqual(failures[0]['state'], 'invalid-schema')
                self.assertFalse(records['capabilities.json']['accepted'])

    def test_allocated_tool_and_shape_failures_finalize_closed_criteria(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp);project = base / 'repo';maintenance = project / 'eng/kubernetes-upgrade/MAINTENANCE.md'
            maintenance.parent.mkdir(parents=True);maintenance.write_text('existing proposal')
            tool = base / 'tool';tool.write_text('retained tool')
            errors = [PermissionError('private detail'), subprocess.TimeoutExpired('tool', 30),
                      SimpleNamespace(returncode=1, stdout=b'', stderr=b'private detail'), None]
            for index, result in enumerate(errors):
                args = SimpleNamespace(project_root=project, evidence_root=base / 'private', attempt_id='failure-' + str(index),
                    operator='operator', context='local', kubectl=tool, helm=tool, age=tool, kubeconfig=tool)
                with patch('qualify.subprocess.run', side_effect=result if isinstance(result, Exception) else None,
                           return_value=result or SimpleNamespace(returncode=0, stdout=b'{}', stderr=b'')), \
                     patch.object(Capture, 'collect', side_effect=AttributeError('malformed decoded version')) as census:
                    directory, state = collect(args)
                self.assertEqual(state, 'failed-closed')
                if index < 3:census.assert_not_called()
                criteria = json.loads((directory / 'criteria.json').read_text())
                self.assertFalse(criteria['qualificationAccepted'])
                self.assertEqual(criteria['upgradeGate'], 'closed')
                self.assertTrue(all(v['state'] == 'failed-closed' for v in criteria['criteria']))
                self.assertTrue((directory / 'SHA256SUMS').is_file())
                self.assertTrue((directory / 'capture-failure.json').is_file())
                self.assertNotIn('private detail', (directory / 'capture-failure.json').read_text())

    def run_access(self, outcome, mode):
        with tempfile.TemporaryDirectory() as temp:
            kubeconfig = Path(temp) / 'config';kubeconfig.write_text('native');kubeconfig.chmod(mode)
            records = {}
            attempt = SimpleNamespace(encrypt=Mock(), record=lambda name, value: records.update({name: value}))
            capture = Capture(SimpleNamespace(kubectl='kubectl', helm='helm', context='local', kubeconfig=kubeconfig,
                                              age='age', recipient='public-only'), attempt)
            config = {'clusters': [{'cluster': {'server': 'https://127.0.0.1:6443',
                      'certificate-authority-data': base64.b64encode(b'fixture-cert').decode()}}]}
            raw = {'/api/v1': {'resources': [{'name': 'namespaces', 'kind': 'Namespace', 'verbs': ['list']}]}, '/apis': {'groups': []},
                   '/api/v1/namespaces?limit=500': {'items': [{'metadata': {'name': 'kube-system', 'uid': 'ns-1', 'resourceVersion': '1'}}]}}
            def command(argv, **kwargs):
                if argv[0] == 'helm':
                    data = []
                elif 'config' in argv:
                    data = config
                elif '--raw' in argv:
                    data = raw[argv[argv.index('--raw') + 1]]
                else:
                    data = {'clientVersion': {'gitVersion': 'v1.34.12'}, 'serverVersion': {'gitVersion': 'v1.34.9'}}
                return SimpleNamespace(returncode=0, stdout=json.dumps(data).encode(), stderr=b'')
            if isinstance(outcome, int):
                side = urllib.error.HTTPError('https://127.0.0.1:6443/api/v1/namespaces', outcome, 'status', {}, io.BytesIO(b''))
            elif outcome == 'success':
                side = None
            else:
                side = outcome
            response = Mock(status=200)
            with patch('qualify.subprocess.run', side_effect=command), patch('qualify.ssl.create_default_context'), \
                 patch('qualify.urllib.request.urlopen', side_effect=side, return_value=response):
                capture.collect()
            return records['access.json']

    def test_credential_free_probe_and_kubeconfig_custody_fields_are_recorded(self):
        expected = {401: (True, 401, False), 403: (True, 403, False), 404: (False, 404, None), 500: (False, 500, None),
                    'success': (False, 200, True)}
        for outcome, (denied, status, anonymous) in expected.items():
            with self.subTest(outcome=outcome):
                access = self.run_access(outcome, 0o600)
                self.assertEqual((access['unauthorizedDenied'], access['unauthorizedHttpStatus'], access['anonymousReadAllowed']),
                                 (denied, status, anonymous))
                self.assertNotIn('unauthorizedProbe', access)
                self.assertTrue(access['authorizedRead'])
                self.assertFalse(access['publicDenied']);self.assertEqual(access['acceptance'], 'incomplete')
        access = self.run_access(OSError('unreachable'), 0o600)
        self.assertFalse(access['unauthorizedDenied']);self.assertIsNone(access['anonymousReadAllowed'])
        self.assertNotIn('unauthorizedHttpStatus', access)
        self.assertIn('denial-not-proven', access['unauthorizedProbe'])
        for mode, restricted in ((0o600, True), (0o644, False)):
            with self.subTest(mode=oct(mode)):
                access = self.run_access(403, mode)
                self.assertEqual(access['custodyPermissionsRestricted'], restricted)
                self.assertFalse(access['effectiveCustodyAccepted'])
                self.assertFalse(access['independentCustodyReadback']);self.assertFalse(access['mfaVerified'])

    def test_completed_census_with_any_gap_finalizes_failed_closed_and_complete_one_stays_incomplete(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp);project = base / 'repo';maintenance = project / 'eng/kubernetes-upgrade/MAINTENANCE.md'
            maintenance.parent.mkdir(parents=True);maintenance.write_text('existing proposal')
            tool = base / 'tool';tool.write_text('retained tool')
            for name, coverage, expected in (('gap', [{'step': 'list', 'state': 'observed', 'exitCode': 0},
                                                       {'step': 'list-object', 'state': 'invalid-schema', 'exitCode': 0}], 'failed-closed'),
                                              ('observed', [{'step': 'list', 'state': 'observed', 'exitCode': 0}], 'incomplete')):
                with self.subTest(case=name):
                    args = SimpleNamespace(project_root=project, evidence_root=base / 'private', attempt_id='census-' + name,
                                           operator='operator', context='local', kubectl=tool, helm=tool, age=tool, kubeconfig=tool)
                    def census(self_capture, coverage=coverage):
                        self_capture.coverage.extend(coverage)
                    with patch('qualify.subprocess.run', return_value=SimpleNamespace(returncode=0, stdout=b'{}', stderr=b'')), \
                         patch.object(Capture, 'collect', autospec=True, side_effect=census):
                        directory, state = collect(args)
                    self.assertEqual(state, expected)
                    self.assertEqual((directory / 'capture-failure.json').is_file(), expected == 'failed-closed')
                    criteria = json.loads((directory / 'criteria.json').read_text())
                    self.assertTrue(all(v['state'] == expected for v in criteria['criteria']))
                    self.assertFalse(criteria['qualificationAccepted'])
                    if expected == 'failed-closed':
                        failure = json.loads((directory / 'capture-failure.json').read_text())
                        self.assertEqual(failure['coverage'][-1]['state'], 'invalid-schema')

    def test_endpoint_userinfo_query_and_fragment_cannot_disclose_credentials(self):
        validate_native_endpoint('https://192.168.1.30:6443')
        for endpoint in ('https://user:secret@api.example:6443', 'https://api.example:6443?token=secret',
                         'https://api.example:6443#secret', 'https://api.example/k8s/clusters/source'):
            with self.assertRaises(ValueError):validate_native_endpoint(endpoint)

    def test_supported_skew_and_prerelease_fail(self):
        self.assertTrue(compatible('v1.34.12', 'v1.34.9'))
        self.assertTrue(compatible('v1.35.9', 'v1.34.9'))
        self.assertFalse(compatible('v1.36.1', 'v1.34.9'))
        self.assertFalse(compatible('v1.35.9-rc.1', 'v1.34.9'))

    def test_failed_discovery_is_not_an_empty_accepted_census(self):
        capture = Capture(SimpleNamespace(kubeconfig=Path(__file__)), None)
        config = {'clusters': [{'cluster': {'server': 'https://127.0.0.1:6443',
                  'certificate-authority-data': __import__('base64').b64encode(b'invalid-cert').decode()}}]}
        with patch.object(capture, 'kube', side_effect=[{'clientVersion': {'gitVersion': 'v1.34.12'},
                         'serverVersion': {'gitVersion': 'v1.34.9'}}, config]), patch('qualify.ssl.create_default_context'), \
                patch('qualify.urllib.request.urlopen', side_effect=OSError), patch.object(capture, 'raw', return_value=None):
            with self.assertRaisesRegex(ValueError, 'api-discovery-failed'):
                capture.collect()
        self.assertEqual(capture.inventory, [])

    def test_proxy_credentials_are_refused_before_discovery(self):
        capture = Capture(SimpleNamespace(), None)
        proxy = {'clusters': [{'cluster': {'server': 'https://rancher.example/k8s/clusters/cluster-id',
                                          'certificate-authority-data': 'irrelevant'}}]}
        with patch.object(capture, 'kube', side_effect=[{'clientVersion': {'gitVersion': 'v1.34.12'},
                         'serverVersion': {'gitVersion': 'v1.34.9'}}, proxy]):
            with self.assertRaisesRegex(ValueError, 'direct-verified-tls'):
                capture.collect()


class PinAuthorityTests(unittest.TestCase):
    def pins(self):
        return {'checkedAt': now(), **{k: {'version': v, 'artifactSha256': 'a' * 64, 'prerelease': False} for k, v in
                [('rancher', 'v2.14.6'), ('managementK3s', 'v1.35.8+k3s1'), ('workloadKubernetes', 'v1.35.9')]},
                'chart': {'sha256': 'b' * 64}, 'images': [{'identity': 'rancher/rancher@sha256:' + 'c' * 64}],
                **{k: {'verified': True, 'evidenceSha256': 'd' * 64} for k in
                   ('hostingMatrix', 'importMatrix', 'securityReview', 'licenses', 'managementTools')}}

    def test_unqualified_tag_hosting_security_and_tools_fail_closed(self):
        pins = self.pins()
        pins['hostingMatrix']['distro'] = 'k3s'
        self.assertEqual(validate_pins(pins), [])
        for field in ('securityReview', 'licenses', 'managementTools', 'importMatrix'):
            failed = copy.deepcopy(pins);failed[field]['verified'] = False
            self.assertIn('unqualified-' + field, validate_pins(failed))
        pins['hostingMatrix']['distro'] = 'generic-import'
        pins['images'][0]['identity'] = 'rancher/rancher:latest'
        self.assertIn('generic-import-does-not-qualify-hosting', validate_pins(pins))
        self.assertIn('unqualified-image-identity', validate_pins(pins))

    def test_github_prerelease_and_stale_review_rejected(self):
        pins = self.pins();pins['hostingMatrix']['distro'] = 'k3s'
        pins['managementK3s']['prerelease'] = True
        self.assertIn('prerelease-managementK3s', validate_pins(pins))
        pins['checkedAt'] = '2025-01-01T00:00:00Z'
        self.assertIn('stale-or-future-pin-review', validate_pins(pins))
        self.assertTrue(validate_pins({}))

    def authority(self):
        event = sealed({'sequence': 1, 'previousSha256': None, 'signatureVerified': True,
                        'approvedBy': 'Administrator', 'action': 'grant', 'principal': 'deputy', 'scope': 'scoped-native-recovery'})
        return {'nativeAccess': 'verified', 'nativePath': 'direct',
                **{k: True for k in ('mfaVerified', 'unauthorizedDenied', 'publicDenied', 'independentCustodyReadback',
                   'administratorSignatureVerified', 'independentOffsiteLineageReadback', 'restoredRevocationDenied')},
                'events': [event], 'currentHeadSha256': event['sha256'], 'restoredScopes': {'deputy': 'scoped-native-recovery'}}

    def test_missing_gapped_conflicting_or_expanded_authority_fails_closed(self):
        self.assertTrue(validate_authority({}))
        a = self.authority();self.assertEqual(validate_authority(a), [])
        a['events'][0]['sequence'] = 2
        self.assertIn('gapped-or-unverified-authority-lineage', validate_authority(a))
        a = self.authority();a['events'][0]['scope'] = 'global-admin'
        self.assertIn('deputy-authority-expanded', validate_authority(a))
        # Content changed after sealing, or a well-formed but arbitrary declared digest, fails closed.
        self.assertIn('authority-event-digest-mismatch', validate_authority(a))
        a = self.authority();a['events'][0]['sha256'] = a['currentHeadSha256'] = 'a' * 64
        self.assertEqual(validate_authority(a), ['authority-event-digest-mismatch'])
        a = self.authority();a['nativePath'] = 'rancher-proxy'
        self.assertIn('native-access-unverified-or-proxy-only', validate_authority(a))

    def test_post_cut_revocation_survives_source_loss(self):
        a = self.authority()
        a['events'].append(sealed({'sequence': 2, 'previousSha256': a['events'][0]['sha256'], 'signatureVerified': True,
                                   'approvedBy': 'Administrator', 'action': 'revoke', 'principal': 'deputy'}))
        a['currentHeadSha256'] = a['events'][1]['sha256']
        self.assertIn('missing-or-conflicting-current-authority', validate_authority(a))
        a['restoredScopes'] = {}
        self.assertEqual(validate_authority(a), [])
        a['independentOffsiteLineageReadback'] = False
        self.assertIn('missing-independentOffsiteLineageReadback', validate_authority(a))


class CustodyTests(unittest.TestCase):
    def test_private_attempts_immutable_and_no_recovery_overwrite(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp);project = base / 'repo';project.mkdir()
            root = base / 'private'
            attempt = Attempt(project, root, 'first')
            self.assertEqual(stat.S_IMODE(attempt.directory.stat().st_mode), 0o700)
            attempt.record('safe.json', {'accepted': False})
            self.assertEqual(stat.S_IMODE((attempt.directory/'safe.json').stat().st_mode), 0o600)
            with self.assertRaises(FileExistsError):attempt.record('safe.json', {})
            with self.assertRaises(FileExistsError):Attempt(project, root, 'first')
            with self.assertRaises(ValueError):Attempt(project, project / 'private', 'second')
            home = base / 'home';(home / 'hexalith-recovery-evidence').mkdir(parents=True, mode=0o700)
            with patch('evidence.Path.home', return_value=home), self.assertRaisesRegex(ValueError, 'recovery-evidence'):
                Attempt(project, home / 'hexalith-recovery-evidence' / 'new', 'second')
            self.assertFalse((home / 'hexalith-recovery-evidence' / 'new').exists())

    def test_symlink_evidence_custody_refused(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp);project = base / 'repo';project.mkdir();private = base / 'private';private.mkdir(mode=0o700)
            link = base / 'link';link.symlink_to(private)
            with self.assertRaisesRegex(ValueError, 'symlink'):Attempt(project, link, 'first')


if __name__ == '__main__':
    unittest.main()
