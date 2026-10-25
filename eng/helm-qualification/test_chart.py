"""Real Helm qualification. Missing tools are failures, never skipped acceptance checks."""
import copy
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from urllib.parse import urlsplit

import yaml

HERE = Path(__file__).resolve().parent
CHART = HERE / 'chart'
HELM = os.environ.get('HELM_BIN') or shutil.which('helm')


def graph():
    return json.loads((CHART / 'reference-graph.json').read_text())


def fixture_values():
    """Synthetic qualification bindings; these grant no environment authority."""
    g = graph()
    ref = {'name': 'fixture-credential', 'key': 'token'}
    values = {
        'mode': 'normal', 'imagePullSecret': 'fixture-registry',
        'endpoints': {'data': 'rediss://fixture-data.environment.svc.cluster.local:6380',
                      'identity': 'https://identity.fixture.invalid/realm',
                      'sentry': 'https://dapr-sentry.dapr-system.svc.cluster.local:443'},
        'environment': {'trustDomain': 'fixture.invalid', 'controlPlaneTrustDomain': 'cluster.local', 'sentryEndpoint': 'sentry', 'componentConfigMap': 'fixture-components',
                        'components': {c['name']: {'name': c['name'], 'endpoint': 'data', 'credential': ref}
                                       for c in g['capabilities']}},
        'gateway': {'name': 'fixture-gateway', 'namespace': 'environment', 'sectionName': 'https'},
        'hostnames': {w['identity']: w['resourceId'] + '.fixture.invalid' for w in g['workloads'] if w['publicRoutes']}, 'images': {}, 'bindings': {},
    }
    providers = {'state': ('state.redis', 'redisHost', 'redisPassword'),
                 'pubsub': ('pubsub.redis', 'redisHost', 'redisPassword'),
                 'configuration': ('configuration.redis', 'redisHost', 'redisPassword'),
                 'secret': ('secretstores.hashicorp.vault', 'vaultAddr', 'vaultToken'),
                 'conversation': ('conversation.openai', 'endpoint', 'key')}
    for c in g['capabilities']:
        provider, endpoint_key, credential_key = providers[c['kind']]
        values['environment']['components'][c['name']]['endpoint'] = 'data' if c['kind'] in ('state', 'pubsub', 'configuration') else 'identity'
        values['environment']['components'][c['name']].update(type=provider, endpointMetadataKey=endpoint_key,
            credentialMetadataKey=credential_key, endpointFormat='authority' if c['kind'] in ('state', 'pubsub', 'configuration') else 'url', metadata={'enableTLS': 'true'} if c['kind'] in ('state', 'pubsub', 'configuration') else {})
    for w in g['workloads']:
        values['images'][w['identity']] = 'qualification.invalid/' + w['resourceId'] + '@sha256:' + 'a' * 64
        config = {}
        for i in w['inputs']:
            if i['source'] in ('secret', 'dynamic-secret'):
                value = copy.deepcopy(ref)
            elif i['key'].startswith('ConnectionStrings:'):
                value = {'endpoint': 'data'}
            elif any(s in i['key'].lower() for s in ('authority', 'issuer', 'endpoint')) and 'workloadissuer:' not in i['key'].lower():
                value = {'endpoint': 'identity'}
            elif i['key'].endswith('Path'):
                value = '/etc/hexalith/environment/' + i['reference'] + '.yaml'
            else:
                value = 'fixture'
            if i['key'].endswith(':AppChannelToken'):
                value = {'name': 'fixture-' + w['resourceId'] + '-app', 'key': 'token'}
            elif i['key'].endswith((':DaprApiToken', ':ApiDaprToken')):
                value = {'name': 'fixture-' + w['resourceId'] + '-api', 'key': 'token'}
            config[i['key']] = value
        values['bindings'][w['identity']] = {'configuration': config,
            'appToken': {'name': 'fixture-' + w['resourceId'] + '-app', 'key': 'token'},
            'apiToken': {'name': 'fixture-' + w['resourceId'] + '-api', 'key': 'token'}}
    return values


def render(values, chart=CHART, namespace='reference'):
    if not HELM:
        raise RuntimeError('Helm 4.2.0+ is required; set HELM_BIN')
    with tempfile.TemporaryDirectory(prefix='h45-render-') as tmp:
        path = Path(tmp) / 'values.json'
        path.write_text(json.dumps(values))
        return subprocess.run([HELM, 'template', 'reference', str(chart), '--namespace', namespace, '-f', str(path)],
                              capture_output=True, text=True, timeout=30)


def documents(output):
    return [d for d in yaml.safe_load_all(output) if d]


def expected_environment(workload, values):
    binding = values['bindings'][workload['identity']]
    environment = {}
    def flatten(key, value):
        key = key.replace(':', '__')
        if isinstance(value, str) and value.strip().startswith(('[', '{')):
            value = json.loads(value)
        if isinstance(value, dict):
            if 'endpoint' in value:
                environment[key] = {'name': key, 'value': values['endpoints'][value['endpoint']]}
            elif 'configMapKeyRef' in value:
                environment[key] = {'name': key, 'valueFrom': value}
            else:
                for child, leaf in value.items(): flatten(key + '__' + child, leaf)
        elif isinstance(value, list):
            for index, leaf in enumerate(value): flatten(key + '__' + str(index), leaf)
        else:
            environment[key] = {'name': key, 'value': str(value).lower() if isinstance(value, bool) else str(value)}
    for item in workload['inputs']:
        key = item['key'].replace(':', '__'); value = binding['configuration'][item['key']]
        if item['source'] in ('secret', 'dynamic-secret'):
            environment[key] = {'name': key, 'valueFrom': {'secretKeyRef': value}}
        else: flatten(key, value)
    for key, value in binding.get('credentials', {}).items():
        key = key.replace(':', '__')
        environment[key] = {'name': key, 'valueFrom': {'secretKeyRef': value}}
    for key, value in {**workload['environment'], 'ASPNETCORE_URLS': 'http://0.0.0.0:8080', 'Dapr__DeclarativeSubscriptions': 'true'}.items():
        environment[key] = {'name': key, 'value': value}
    for key, value in [('APP_API_TOKEN', binding['appToken']), ('DAPR_API_TOKEN', binding['apiToken'])]:
        environment[key] = {'name': key, 'valueFrom': {'secretKeyRef': value}}
    return [environment[key] for key in sorted(environment)]


class ChartTests(unittest.TestCase):
    def test_finite_campaign_resources_replicas_and_sidecars_are_explicit(self):
        import campaign_capacity
        values = campaign_capacity.configure_application(fixture_values(), graph())
        _, docs = self.good(values)
        deployments = [d for d in docs if d['kind'] == 'Deployment']
        self.assertEqual(9, sum(d['spec']['replicas'] for d in deployments))
        for d in deployments:
            pod = d['spec']['template']
            app = pod['spec']['containers'][0]
            self.assertEqual({'cpu': '400m', 'memory': '640Mi', 'ephemeral-storage': '384Mi'}, app['resources']['limits'])
            self.assertEqual('100m', pod['metadata']['annotations']['dapr.io/sidecar-cpu-limit'])
            self.assertEqual('128Mi', pod['metadata']['annotations']['dapr.io/sidecar-memory-limit'])
            self.assertEqual('128Mi', pod['spec']['volumes'][0]['emptyDir']['sizeLimit'])
        values['bindings']['memories/server']['replicas'] = 1
        self.bad(values, 'finite campaign replica allocation differs')
        values = campaign_capacity.configure_application(fixture_values(), graph())
        values['bindings']['memories/server']['resources']['limits']['cpu'] = '8'
        self.bad(values, 'finite campaign resources differ')

    @classmethod
    def setUpClass(cls):
        if not HELM:
            raise RuntimeError('Helm 4.2.0+ required; missing tools fail qualification')
        version = subprocess.run([HELM, 'version', '--template', '{{.Version}}'], capture_output=True, text=True, check=True).stdout
        if tuple(int(v) for v in version.lstrip('v').split('+')[0].split('.')[:3]) < (4, 2, 0):
            raise RuntimeError('Helm 4.2.0+ required')

    def good(self, values, chart=CHART):
        result = render(values, chart)
        self.assertEqual(result.returncode, 0, result.stderr)
        return result.stdout, documents(result.stdout)

    def bad(self, values, expected_message=None):
        result = render(values)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stdout, '')
        if expected_message is not None:
            self.assertIn(expected_message, result.stderr)

    def assert_subscriptions(self, docs):
        expected = []
        for subscription in graph()['subscriptions']:
            spec = {'pubsubname': subscription['component'], 'topic': subscription['topic'],
                    'routes': {'default': subscription['route']}}
            if subscription['deadLetter']:
                spec['deadLetterTopic'] = subscription['deadLetter']
            expected.append({'apiVersion': 'dapr.io/v2alpha1', 'kind': 'Subscription',
                             'metadata': {'name': subscription['name']}, 'spec': spec,
                             'scopes': [subscription['appId']]})
        self.assertEqual(sorted([d for d in docs if d['kind'] == 'Subscription'], key=lambda d: d['metadata']['name']),
                         sorted(expected, key=lambda d: d['metadata']['name']))

    def assert_complete_routes(self, docs, values):
        routes = [d for d in docs if d['kind'] == 'HTTPRoute']
        expected = {}
        if values['mode'] == 'normal':
            for workload in graph()['workloads']:
                if workload['publicRoutes']:
                    expected[workload['resourceId']] = workload
        self.assertEqual({d['metadata']['name'] for d in routes}, set(expected))
        self.assertEqual(len(routes), len(expected))
        for route in routes:
            workload = expected[route['metadata']['name']]
            self.assertEqual(route['spec']['parentRefs'], [values['gateway']])
            self.assertEqual(route['spec']['hostnames'], [values['hostnames'][workload['identity']].lower().rstrip('.')])
            prefixes = {p.split('{')[0].rstrip('/') or '/' for p in workload['publicRoutes']}
            self.assertEqual(route['spec']['rules'], [
                {'matches': [{'path': {'type': 'PathPrefix', 'value': prefix}}],
                 'backendRefs': [{'name': workload['resourceId'], 'port': 80}]} for prefix in sorted(prefixes)])

    def assert_complete_services(self, docs, values):
        workloads = graph()['workloads'] if values['mode'] != 'remove-workloads' else []
        expected = [{'apiVersion': 'v1', 'kind': 'Service', 'metadata': {'name': w['resourceId']},
                     'spec': {'selector': {'app.kubernetes.io/name': w['resourceId']},
                              'ports': [{'name': 'http', 'port': 80, 'targetPort': 'http'}]}} for w in workloads]
        services = [d for d in docs if d['kind'] == 'Service']
        self.assertEqual(sorted(services, key=lambda d: d['metadata']['name']),
                         sorted(expected, key=lambda d: d['metadata']['name']))
        deployments = {d['metadata']['name']: d for d in docs if d['kind'] == 'Deployment'}
        self.assertEqual(set(deployments), {w['resourceId'] for w in workloads})
        for service in services:
            deployment = deployments[service['metadata']['name']]
            selector = service['spec']['selector']
            self.assertEqual(deployment['spec']['selector']['matchLabels'], selector)
            self.assertEqual(deployment['spec']['template']['metadata']['labels'], selector)
            app = deployment['spec']['template']['spec']['containers'][0]
            self.assertEqual(app['ports'], [{'name': 'http', 'containerPort': 8080}])
            workload = next(w for w in workloads if w['resourceId'] == service['metadata']['name'])
            self.assertEqual(app['readinessProbe']['httpGet'], {'path': workload['readiness'], 'port': 'http'})
            binding = values['bindings'][workload['identity']]
            security = {'runAsNonRoot': True, 'runAsUser': 1654, 'seccompProfile': {'type': 'RuntimeDefault'}}
            self.assertEqual(deployment['spec']['template'], {
                'metadata': {'labels': selector, 'annotations': {
                    'dapr.io/enabled': 'true', 'dapr.io/app-id': workload['appId'], 'dapr.io/app-port': '8080',
                    'dapr.io/app-protocol': 'http', 'dapr.io/config': 'platform-' + workload['appId'],
                    'dapr.io/app-token-secret': binding['appToken']['name'], 'dapr.io/api-token-secret': binding['apiToken']['name'],
                    'dapr.io/sidecar-listen-addresses': '127.0.0.1', 'dapr.io/enable-api-logging': 'false'}},
                'spec': {'automountServiceAccountToken': True, 'imagePullSecrets': [{'name': values['imagePullSecret']}],
                    'securityContext': security,
                    'containers': [{'name': 'app', 'image': values['images'][workload['identity']],
                        'ports': [{'name': 'http', 'containerPort': 8080}],
                        'readinessProbe': {'httpGet': {'path': workload['readiness'], 'port': 'http'}},
                        'securityContext': {**security, 'allowPrivilegeEscalation': False, 'readOnlyRootFilesystem': True,
                                            'capabilities': {'drop': ['ALL']}},
                        'env': expected_environment(workload, values),
                        'volumeMounts': [{'name': 'tmp', 'mountPath': '/tmp'},
                                         {'name': 'environment-components', 'mountPath': '/etc/hexalith/environment', 'readOnly': True}],
                        'resources': {'requests': {'cpu': '10m', 'memory': '32Mi'}, 'limits': {'cpu': '1', 'memory': '512Mi'}}}],
                    'volumes': [{'name': 'tmp', 'emptyDir': {}},
                                {'name': 'environment-components', 'configMap': {'name': values['environment']['componentConfigMap']}}]}})

    def assert_complete_dapr(self, docs, values):
        expected = []
        for capability in graph()['capabilities']:
            binding = values['environment']['components'][capability['name']]
            endpoint = values['endpoints'][binding['endpoint']]
            if binding.get('endpointFormat', 'url') == 'authority':
                endpoint = urlsplit(endpoint).netloc
            metadata = [{'name': binding['endpointMetadataKey'], 'value': endpoint},
                        {'name': binding['credentialMetadataKey'], 'secretKeyRef': binding['credential']}]
            metadata += [{'name': key, 'value': str(value).lower() if isinstance(value, bool) else str(value)}
                         for key, value in sorted(binding.get('metadata', {}).items())]
            expected.append({'apiVersion': 'dapr.io/v1alpha1', 'kind': 'Component',
                             'metadata': {'name': capability['name']},
                             'spec': {'type': binding['type'], 'version': 'v1', 'metadata': metadata},
                             'scopes': capability['consumers']})
        for workload in graph()['workloads']:
            acl = json.loads(json.dumps(workload['accessControl']).replace('__APPLICATION_NAMESPACE__', 'reference')
                             .replace('__TRUST_DOMAIN__', values['environment']['trustDomain']))
            expected.append({'apiVersion': 'dapr.io/v1alpha1', 'kind': 'Configuration',
                             'metadata': {'name': 'platform-' + workload['appId']},
                             'spec': {'mtls': {'enabled': True,
                                               'controlPlaneTrustDomain': values['environment']['controlPlaneTrustDomain'],
                                               'sentryAddress': urlsplit(values['endpoints'][values['environment']['sentryEndpoint']]).netloc},
                                      'features': [{'name': 'HotReload', 'enabled': False}], 'accessControl': acl}})
        actual = [d for d in docs if d['kind'] in ('Component', 'Configuration')]
        self.assertEqual(sorted(actual, key=lambda d: (d['kind'], d['metadata']['name'])),
                         sorted(expected, key=lambda d: (d['kind'], d['metadata']['name'])))
        self.assert_subscriptions(docs)

    def test_modes_determinism_and_archive_parity(self):
        with tempfile.TemporaryDirectory(prefix='h45-package-') as tmp:
            subprocess.run([HELM, 'package', str(CHART), '-d', tmp], check=True, capture_output=True)
            archive = next(Path(tmp).glob('*.tgz'))
            for mode in ('normal', 'recovery', 'remove-workloads'):
                with self.subTest(mode=mode):
                    values = fixture_values(); values['mode'] = mode
                    text, docs = self.good(values)
                    self.assertEqual(text, self.good(values)[0])
                    archive_docs = self.good(values, archive)[1]
                    self.assertEqual(docs, archive_docs)
                    self.assert_complete_dapr(archive_docs, values)
                    self.assert_complete_services(archive_docs, values)
                    self.assert_complete_routes(archive_docs, values)
                    self.assertEqual(self.good(values, archive)[0], self.good(values, archive)[0])
                    workloads = [d for d in docs if d['kind'] == 'Deployment']
                    routes = [d for d in docs if d['kind'] == 'HTTPRoute']
                    self.assertEqual(len(workloads), 0 if mode == 'remove-workloads' else len(graph()['workloads']))
                    self.assert_complete_routes(docs, values)
                    self.assert_complete_dapr(docs, values)
                    self.assert_complete_services(docs, values)
                    for d in workloads:
                        self.assertEqual(d['spec']['replicas'], 0 if mode == 'recovery' else 1)
                    # Application Dapr objects are retained through workload removal; infrastructure is external.
                    self.assertEqual(len([d for d in docs if d['kind'] == 'Configuration']), len(graph()['workloads']))

    def test_identity_readiness_and_shared_dapr_security(self):
        _, docs = self.good(fixture_values())
        for w in graph()['workloads']:
            d = next(d for d in docs if d['kind'] == 'Deployment' and d['metadata']['name'] == w['resourceId'])
            pod = d['spec']['template']; annotations = pod['metadata']['annotations']
            self.assertEqual(annotations['dapr.io/app-id'], w['appId'])
            self.assertEqual(annotations['dapr.io/config'], 'platform-' + w['appId'])
            binding = fixture_values()['bindings'][w['identity']]
            self.assertNotEqual(binding['appToken'], binding['apiToken'])
            self.assertEqual(annotations['dapr.io/app-token-secret'], binding['appToken']['name'])
            self.assertEqual(annotations['dapr.io/api-token-secret'], binding['apiToken']['name'])
            app = pod['spec']['containers'][0]
            self.assertEqual(len(app['env']), len({e['name'].lower() for e in app['env']}))
            environment = {e['name']: e for e in app['env']}
            for name, reference in [('APP_API_TOKEN', binding['appToken']), ('DAPR_API_TOKEN', binding['apiToken'])]:
                self.assertEqual(environment[name]['valueFrom'], {'secretKeyRef': reference})
            for item in w['inputs']:
                if item['source'] in ('secret', 'dynamic-secret'):
                    name = item['key'].replace(':', '__')
                    self.assertEqual(environment[name]['valueFrom'], {'secretKeyRef': binding['configuration'][item['key']]})
            self.assertEqual(app['readinessProbe']['httpGet']['path'], w['readiness'])
            self.assertEqual(app['securityContext'], {'allowPrivilegeEscalation': False, 'readOnlyRootFilesystem': True,
                'runAsNonRoot': True, 'runAsUser': 1654, 'capabilities': {'drop': ['ALL']}, 'seccompProfile': {'type': 'RuntimeDefault'}})
            config = next(d for d in docs if d['kind'] == 'Configuration' and d['metadata']['name'] == 'platform-' + w['appId'])
            expected = json.loads(json.dumps(w['accessControl']).replace('__APPLICATION_NAMESPACE__', 'reference').replace('__TRUST_DOMAIN__', 'fixture.invalid'))
            self.assertEqual(config['spec']['accessControl'], expected)
            self.assertEqual(config['spec']['mtls'], {'enabled': True, 'controlPlaneTrustDomain': 'cluster.local', 'sentryAddress': 'dapr-sentry.dapr-system.svc.cluster.local:443'})
            self.assertEqual(config['spec']['features'], [{'name': 'HotReload', 'enabled': False}])
            for key, value in w['environment'].items():
                self.assertIn({'name': key, 'value': value}, app['env'])
        for capability in graph()['capabilities']:
            component = next(d for d in docs if d['kind'] == 'Component' and d['metadata']['name'] == capability['name'])
            self.assertEqual(component['scopes'], capability['consumers'])
            self.assertTrue(any('secretKeyRef' in m for m in component['spec']['metadata']))
            expected_endpoint = 'fixture-data.environment.svc.cluster.local:6380' if capability['kind'] in ('state', 'pubsub', 'configuration') else fixture_values()['endpoints']['identity']
            self.assertTrue(any(m.get('value') == expected_endpoint for m in component['spec']['metadata']))
        for d in docs:
            if d['kind'] == 'HTTPRoute':
                self.assertEqual(d['spec']['parentRefs'], [{'name': 'fixture-gateway', 'namespace': 'environment', 'sectionName': 'https'}])
                workload = next(w for w in graph()['workloads'] if w['resourceId'] == d['metadata']['name'])
                expected = {p.split('{')[0].rstrip('/') or '/' for p in workload['publicRoutes']}
                self.assertEqual({r['matches'][0]['path']['value'] for r in d['spec']['rules']}, expected)

    def test_structured_configuration_flattens_and_canonical_dapr_bindings_win(self):
        values = fixture_values()
        values['bindings']['eventstore/gateway']['configuration']['Authentication:JwtBearer:AllowedAlgorithms'] = '["RS256", "ES256"]'
        values['bindings']['eventstore/gateway']['configuration']['Authentication:DaprInternal'] = '{"Extra":"parent","OtherLifetimeSeconds":42}'
        values['bindings']['eventstore/gateway']['configuration']['Authentication:DaprInternal:Audience'] = 'explicit-child'
        _, docs = self.good(values)
        app = next(d for d in docs if d['kind'] == 'Deployment' and d['metadata']['name'] == 'eventstore')['spec']['template']['spec']['containers'][0]
        env = {e['name']: e for e in app['env']}
        self.assertEqual(env['Authentication__JwtBearer__AllowedAlgorithms__0']['value'], 'RS256')
        self.assertEqual(env['Authentication__DaprInternal__Audience']['value'], 'explicit-child')
        self.assertEqual(env['EventStore__Projections__CheckpointStateStoreName']['value'], 'statestore')
        values['bindings']['memories/clock']['credentials'] = {'Clock__Sources__0__AuthenticationToken': {'name': 'source-token', 'key': 'token'}}
        _, docs = self.good(values)
        clock = next(d for d in docs if d['kind'] == 'Deployment' and d['metadata']['name'] == 'memories-access-telemetry-clock')
        self.assertIn({'name': 'Clock__Sources__0__AuthenticationToken', 'valueFrom': {'secretKeyRef': {'name': 'source-token', 'key': 'token'}}}, clock['spec']['template']['spec']['containers'][0]['env'])
        values['bindings']['eventstore/gateway']['configuration']['Authentication:JwtBearer:AllowedAlgorithms'] = '[malformed]'
        self.bad(values)

    def test_decoded_credentials_aliases_and_token_overrides_fail(self):
        for structured in [r'[{"Authentication\u0054oken":"review-literal"}]',
                           '[{"authenticationtoken":"literal"}]',
                           '[{"Nested":{"ClientSecret":"literal"}}]',
                           '[{"AuthenticationToken":{"Value":"literal"}}]',
                           '[{"AuthenticationTokens":["literal"]}]',
                           '[{"Secret":"literal"}]',
                           '[{"Endpoint":{"endpoint":"identity"},"AuthenticationToken":{"configMapKeyRef":{"name":"x","key":"token"}}}]']:
            values = fixture_values()
            values['bindings']['memories/clock']['configuration']['Clock:Sources'] = structured
            with self.subTest(structured=structured):
                self.bad(values)
        for key in ['eventstore__projections__checkpointstatestorename', 'dapr__declarativesubscriptions',
                    'app_api_token', 'EventStore__AppChannelToken', 'eventstore__appchanneltoken']:
            values = fixture_values()
            values['bindings']['eventstore/gateway']['credentials'] = {key: {'name': 'override', 'key': 'token'}}
            with self.subTest(key=key): self.bad(values)
        for identity, key in [('eventstore/gateway', 'EventStore:AppChannelToken'), ('memories/server', 'Memories:DaprApiToken')]:
            values = fixture_values()
            values['bindings'][identity]['configuration'][key] = {'name': 'mismatch', 'key': 'token'}
            self.bad(values)
        values = fixture_values()
        values['bindings']['eventstore/gateway']['configuration']['authentication:jwtbearer:audience'] = 'alias'
        self.bad(values)
        values = fixture_values()
        values['bindings']['eventstore/gateway']['configuration']['Authentication:DaprInternal'] = '{"audience":"alias"}'
        self.bad(values)

    def test_nested_references_and_literal_https_leaves(self):
        for encode in (lambda x: x, json.dumps):
            values = fixture_values()
            values['bindings']['memories/clock']['configuration']['Clock:Sources'] = encode([
                {'Endpoint': {'endpoint': 'identity'}, 'Name': {'configMapKeyRef': {'name': 'source-config', 'key': 'name'}}},
                {'Endpoint': 'https://source.fixture.invalid/api'}])
            values['bindings']['memories/clock']['credentials'] = {
                'Clock__Sources__0__AuthenticationToken': {'name': 'source-token', 'key': 'token'}}
            _, docs = self.good(values)
            app = next(d for d in docs if d['kind'] == 'Deployment' and d['metadata']['name'] == 'memories-access-telemetry-clock')
            env = {e['name']: e for e in app['spec']['template']['spec']['containers'][0]['env']}
            self.assertEqual(env['Clock__Sources__0__Endpoint']['value'], values['endpoints']['identity'])
            self.assertEqual(env['Clock__Sources__1__Endpoint']['value'], 'https://source.fixture.invalid/api')
            self.assertEqual(env['Clock__Sources__0__Name']['valueFrom'], {'configMapKeyRef': {'name': 'source-config', 'key': 'name'}})
            self.assertNotIn('Clock__Sources__0__Endpoint__endpoint', env)
        for endpoint in ('https://user:password@source.invalid', 'http://source.invalid', 'https://source.invalid?token=secret'):
            values = fixture_values()
            values['bindings']['memories/clock']['configuration']['Clock:Sources'] = json.dumps([{'Endpoint': endpoint}])
            self.bad(values)

    def test_host_collisions_image_registry_ports_and_integer_replicas(self):
        values = fixture_values()
        values['hostnames']['eventstore/gateway'] = 'SHARED.fixture.invalid.'
        values['hostnames']['memories/server'] = 'shared.fixture.invalid'
        self.bad(values)
        values = fixture_values(); del values['hostnames']['tenants/api']; self.bad(values)
        values = fixture_values()
        values['images'] = {k: 'registry.example:5000/app@sha256:' + 'a' * 64 for k in values['images']}
        self.good(values)
        values['images']['eventstore/gateway'] = 'registry.example:5000/app:tag@sha256:' + 'a' * 64
        self.bad(values)
        for replicas in (1.5, 0, 4, True, '1.5'):
            values = fixture_values(); values['replicas'] = replicas; self.bad(values)

    def test_no_secrets_providers_or_persistent_environment_resources(self):
        output, docs = self.good(fixture_values())
        self.assertLessEqual({d['kind'] for d in docs}, {'Deployment', 'Service', 'HTTPRoute', 'Configuration', 'Subscription', 'Component'})
        for forbidden in ('secretstores.local.file', 'conversation.echo', 'redis:7', 'kind: Gateway\n', 'kind: Secret\n', 'secretsFile', 'HotReload: true'):
            self.assertNotIn(forbidden, output)
        for d in docs:
            if d['kind'] == 'Deployment':
                for e in d['spec']['template']['spec']['containers'][0]['env']:
                    if e['name'] in ('APP_API_TOKEN', 'DAPR_API_TOKEN'):
                        self.assertIn('secretKeyRef', e['valueFrom'])

    def test_values_only_digest_change(self):
        values = fixture_values(); _, before = self.good(values)
        chart_hash = hashlib.sha256(b''.join(p.read_bytes() for p in sorted(CHART.rglob('*')) if p.is_file())).hexdigest()
        values['images'] = {k: v[:-64] + 'b' * 64 for k, v in values['images'].items()}
        _, after = self.good(values)
        self.assertEqual(len(before), len(after))
        for b, a in zip(before, after):
            if b['kind'] == 'Deployment':
                workload = next(w for w in graph()['workloads'] if w['resourceId'] == a['metadata']['name'])
                image = a['spec']['template']['spec']['containers'][0]['image']
                self.assertEqual(image, values['images'][workload['identity']])
                self.assertTrue(image.endswith('@sha256:' + 'b' * 64))
                b['spec']['template']['spec']['containers'][0]['image'] = image
            self.assertEqual(b, a)
        self.assertEqual(chart_hash, hashlib.sha256(b''.join(p.read_bytes() for p in sorted(CHART.rglob('*')) if p.is_file())).hexdigest())

    def test_connection_string_credentials_and_ambiguous_numeric_endpoints_fail(self):
        for literal in ('Host=data;Password = literal', 'Password\t=literal', 'Host=data; Pwd = literal',
                        'Server=data; User Id = owner; Password = literal', 'Host=data;Token\u00a0=literal'):
            values = fixture_values()
            values['bindings']['memories/server']['configuration']['ConnectionStrings:redis'] = literal
            with self.subTest(literal=literal): self.bad(values)
        for endpoint in ('https://2130706433', 'https://0177.0.0.1', 'https://0x7f000001',
                         'https://127.1', 'https://0x7f.0.0.1', 'https://127.0.0.1.', 'https://999.1.2.3'):
            values = fixture_values(); values['endpoints']['identity'] = endpoint
            with self.subTest(endpoint=endpoint): self.bad(values)
        for endpoint in ('https://10.20.30.40:443/path', 'https://identity.fixture.invalid/path'):
            values = fixture_values(); values['endpoints']['identity'] = endpoint
            self.good(values)

    def test_scheme_less_authorities_require_native_tls_and_postgresql_states_tls(self):
        cases = [('redis.data.svc:6379,ssl=false', 'require ssl=true'),
                 ('redis.data.svc,ssl=false', 'requires an explicit port'),
                 ('127.1', 'requires an explicit port'),
                 ('redis.data.svc:6379,ssl=true,127.0.0.2:6379', 'loopback or unspecified'),
                 ('redis.data.svc:6379,ssl=true,secondary.data.svc', 'requires an explicit port'),
                 ('redis.data.svc:6379,ssl=true,secondary.data.svc:65536', 'invalid endpoint port'),
                 ('  redis.data.svc:6379,ssl=false  ', 'require ssl=true'),
                 ('redis.data.svc:6379 ,ssl=false', 'require ssl=true'),
                 ('redis.data.svc: 6379,ssl=true', 'invalid endpoint authority'),
                 ('redis.data.svc:6379', 'require ssl=true'),
                 ('127.1:6379,ssl=true', 'canonical non-loopback IPv4'),
                 ('127.0.0.2:6379,ssl=true', 'loopback or unspecified'),
                 ('2130706433:6379,ssl=true', 'canonical non-loopback IPv4'),
                 ('0177.0.0.1:6379,ssl=true', 'ambiguous numeric'),
                 ('redis.data.svc:0,ssl=true', 'invalid endpoint port'),
                 ('redis.data.svc:65536,ssl=true', 'invalid endpoint port'),
                 ('redis.data.svc:6379,ssl=true,ssl=false', 'forbid ssl=false')]
        for literal, message in cases:
            values = fixture_values()
            values['bindings']['memories/server']['configuration']['ConnectionStrings:redis'] = literal
            with self.subTest(literal=literal): self.bad(values, message)
        for literal in ('redis.data.svc:6379,ssl=true', '10.20.30.40:6379,ssl=true',
                        'redis.data.svc:6379,ssl=true,secondary.data.svc:6380',
                        'ssl=true,redis.data.svc:6379,10.20.30.40:6380,connectRetry=3'):
            values = fixture_values()
            values['bindings']['memories/server']['configuration']['ConnectionStrings:redis'] = literal
            self.good(values)
        for literal, message in [('127.0.0.2:6379,ssl=false', 'loopback or unspecified'),
                                 ('redis.data.svc:6379,ssl=false', 'require ssl=true'),
                                 ('00:10', 'canonical non-loopback IPv4')]:
            values = fixture_values()
            values['bindings']['memories/clock']['configuration']['Clock:Sources'] = [{'Endpoint': literal}]
            with self.subTest(clock_endpoint=literal): self.bad(values, message)
        for duration in ('00:00:10', '00:10', '1.00:00:10'):
            values = fixture_values()
            values['bindings']['eventstore/gateway']['configuration']['EventStore:ProjectionDispatch:RetryBaseDelay'] = duration
            _, docs = self.good(values)
            app = next(d for d in docs if d['kind'] == 'Deployment' and d['metadata']['name'] == 'eventstore')['spec']['template']['spec']['containers'][0]
            self.assertIn({'name': 'EventStore__ProjectionDispatch__RetryBaseDelay', 'value': duration}, app['env'])
        for suffix in ('', '?sslmode=disable', '?sslmode=require&other=true', '?sslmode=require#fragment'):
            values = fixture_values()
            values['environment']['components']['statestore'].update(type='state.postgresql', endpointFormat='url', endpointMetadataKey='connectionEndpoint')
            values['endpoints']['data'] = 'postgresql://pg.data.svc:5432/db' + suffix
            with self.subTest(suffix=suffix): self.bad(values, 'PostgreSQL requires a single sslmode=')
        for mode in ('verify-full', 'require', 'verify-ca'):
            values['endpoints']['data'] = 'postgresql://pg.data.svc:5432/db?sslmode=' + mode
            self.good(values)

    def test_component_metadata_rejects_objects_and_lists_before_rendering(self):
        for structured in ({'password': 'synthetic-review-marker'}, ['synthetic-review-marker'], {}, []):
            values = fixture_values()
            values['environment']['components']['statestore']['metadata']['options'] = structured
            with self.subTest(structured=structured):
                self.bad(values, 'provider metadata values must be scalar')

    def test_large_numeric_leaves_and_component_metadata_keep_integer_notation(self):
        values = fixture_values()
        values['bindings']['memories/server']['configuration']['retentionSeconds'] = 2592000
        values['bindings']['memories/clock']['configuration']['Clock:Sources'] = '[{"RetentionSeconds":2000000}]'
        values['environment']['components']['statestore']['metadata']['retentionSeconds'] = 2592000
        _, docs = self.good(values)
        for resource, key, expected in [('memories', 'retentionSeconds', '2592000'),
                                         ('memories-access-telemetry-clock', 'Clock__Sources__0__RetentionSeconds', '2000000')]:
            app = next(d for d in docs if d['kind'] == 'Deployment' and d['metadata']['name'] == resource)['spec']['template']['spec']['containers'][0]
            self.assertIn({'name': key, 'value': expected}, app['env'])
        component = next(d for d in docs if d['kind'] == 'Component' and d['metadata']['name'] == 'statestore')
        self.assertIn({'name': 'retentionSeconds', 'value': '2592000'}, component['spec']['metadata'])

    def test_credential_keys_share_one_denylist_for_configuration_and_metadata(self):
        for key in ('Pwd', 'AccessKey', 'AccountKey', 'MasterKey', 'SharedAccessKey', 'Passphrase', 'Credential', 'Credentials'):
            for encode in (lambda x: x, json.dumps):
                values = fixture_values()
                values['bindings']['memories/clock']['configuration']['Clock:Sources'] = encode([{key: 'hunter2'}])
                with self.subTest(key=key, site='configuration'):
                    self.bad(values, 'credential leaves require existing Secret references')
            values = fixture_values()
            values['environment']['components']['statestore']['metadata'][key.lower()] = 'hunter2'
            with self.subTest(key=key, site='component'):
                self.bad(values, 'provider credentials and endpoints require references')
            values = fixture_values()
            values['bindings']['memories/clock']['configuration']['Clock:Sources'] = [{'Name': key + ' = hunter2'}]
            with self.subTest(key=key, site='literal'):
                self.bad(values, 'credential-bearing connection-string literals require existing references')
        for key in ('RedisHost', 'REDISPASSWORD'):
            values = fixture_values()
            values['environment']['components']['statestore']['metadata'][key] = 'literal'
            self.bad(values, 'provider credentials and endpoints require references')

    def test_authority_format_and_sentry_require_explicit_ports(self):
        for parameter, endpoint in [('sentry', 'https://dapr-sentry.dapr-system.svc.cluster.local'),
                                    ('data', 'rediss://fixture-data.environment.svc.cluster.local')]:
            values = fixture_values(); values['endpoints'][parameter] = endpoint
            with self.subTest(parameter=parameter): self.bad(values, 'requires an explicit port')

    def test_each_validation_rule_fails_with_its_own_message(self):
        component = lambda v: v['environment']['components']['statestore']
        cases = [
            (lambda v: component(v).update(endpointMetadataKey='invalid-key'), 'provider metadata keys are required'),
            (lambda v: component(v).update(endpointFormat='invalid'), 'unknown endpoint metadata format'),
            (lambda v: component(v)['metadata'].update(enableTLS='false'), 'hosted Redis bindings require native TLS'),
            (lambda v: component(v)['metadata'].update(redisPassword='literal'), 'provider credentials and endpoints require references'),
            (lambda v: component(v)['metadata'].update(example='http://unsafe.invalid'), 'unsafe provider metadata literal'),
            (lambda v: v['endpoints'].update(identity='https://host.invalid:65535'), None),
            (lambda v: v['endpoints'].update(identity='https://host.invalid:65536'), 'invalid endpoint port'),
            (lambda v: v['endpoints'].update(identity='https://127.1'), 'canonical non-loopback IPv4'),
            (lambda v: v['endpoints'].update(identity='https://localhost'), 'localhost endpoint authority'),
            (lambda v: v['bindings']['memories/clock']['configuration'].update({'Clock:Sources': [{'Path': '/tmp/literal'}]}), 'unsafe or credential-bearing configuration literal'),
            (lambda v: v['bindings']['memories/clock'].update(credentials={'ASPNETCORE_URLS': {'name': 'override', 'key': 'value'}}), 'credentials cannot override canonical Dapr/security values'),
            (lambda v: v['bindings']['eventstore/gateway']['apiToken'].update(key='wrong'), 'Dapr token references must use the token key'),
            (lambda v: v['bindings']['eventstore/gateway']['apiToken'].update(extra='wrong'), 'existing reference requires only name and key'),
            (lambda v: v['endpoints'].clear(), 'non-secret endpoint parameters are required')]
        for mutate, message in cases:
            values = fixture_values(); mutate(values)
            with self.subTest(message=message):
                if message: self.bad(values, message)
                else: self.good(values)

    def test_empty_bindings_and_recursive_nulls_fail(self):
        for value in ({}, [], '{}', '[]', {'Nested': None}, '[null]', '[{"Endpoint":null}]', '{"Nested":{}}'):
            values = fixture_values()
            values['bindings']['memories/clock']['configuration']['Clock:Sources'] = value
            with self.subTest(value=value): self.bad(values)

    def test_existing_reference_names_follow_their_kubernetes_fields(self):
        variants = [lambda v: v['gateway'].update(namespace='environment.invalid'),
                    lambda v: v['gateway'].update(namespace='a' * 64),
                    lambda v: v['gateway'].update(name='gateway..invalid'),
                    lambda v: v['gateway'].update(name='a' * 254),
                    lambda v: v['gateway'].update(sectionName='https..invalid'),
                    lambda v: v['gateway'].update(sectionName='a' * 254),
                    lambda v: v.update(imagePullSecret='registry..invalid'),
                    lambda v: v['environment'].update(componentConfigMap='a' * 254),
                    lambda v: v['hostnames'].update({'tenants/api': 'tenant..invalid'}),
                    lambda v: v['hostnames'].update({'tenants/api': 'a' * 254}),
                    lambda v: v['bindings']['eventstore/gateway']['appToken'].update(name='credential..invalid')]
        for index, mutate in enumerate(variants):
            values = fixture_values(); mutate(values)
            with self.subTest(case=index): self.bad(values)
        values = fixture_values(); values['gateway']['sectionName'] = 'https.public'
        self.assert_complete_routes(self.good(values)[1], values)

    def test_missing_bindings_tags_literals_unknown_modes_and_unsafe_endpoints_fail(self):
        self.bad({})
        variants = []
        for empty in (None, '', '   '):
            values = fixture_values()
            values['bindings']['eventstore/gateway']['configuration']['Authentication:JwtBearer:Audience'] = empty
            self.bad(values)
        for mutate in [lambda v: v.update(mode='unknown'), lambda v: v['images'].clear(),
                       lambda v: v['bindings'].clear(), lambda v: v.update(imagePullSecret=''),
                       lambda v: v['endpoints'].update(data='redis://user:password@host:6379'),
                       lambda v: v['endpoints'].update(identity='http://identity.fixture.invalid'),
                       lambda v: v['endpoints'].update(data='rediss://127.0.0.1:6380'),
                       lambda v: v['gateway'].update(namespace=''),
                       lambda v: v['environment']['components']['statestore'].update(type='state.local'),
                       lambda v: v['environment']['components']['statestore'].update(metadata={'redisPassword': 'literal'}),
                       lambda v: v['bindings']['memories/clock']['configuration'].update({'Clock:Sources': '[{"AuthenticationToken":"literal"}]'})]:
            v = fixture_values(); mutate(v); variants.append(v)
        identity = graph()['workloads'][0]['identity']
        v = fixture_values(); v['images'][identity] = 'registry.example/app:latest'; variants.append(v)
        v = fixture_values(); v['bindings'][identity]['appToken'] = 'literal-token'; variants.append(v)
        v = fixture_values(); next_input = next(i for i in graph()['workloads'][0]['inputs'] if i['source'] == 'secret')
        v['bindings'][identity]['configuration'][next_input['key']] = 'literal-secret'; variants.append(v)
        for index, v in enumerate(variants):
            with self.subTest(case=index): self.bad(v)


if __name__ == '__main__':
    unittest.main()
