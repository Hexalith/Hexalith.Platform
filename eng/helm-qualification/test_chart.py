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


class ChartTests(unittest.TestCase):
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

    def bad(self, values):
        result = render(values)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stdout, '')

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
                    self.assert_subscriptions(archive_docs)
                    self.assert_complete_routes(archive_docs, values)
                    self.assertEqual(self.good(values, archive)[0], self.good(values, archive)[0])
                    workloads = [d for d in docs if d['kind'] == 'Deployment']
                    routes = [d for d in docs if d['kind'] == 'HTTPRoute']
                    self.assertEqual(len(workloads), 0 if mode == 'remove-workloads' else len(graph()['workloads']))
                    self.assert_complete_routes(docs, values)
                    self.assert_subscriptions(docs)
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
        for b, a in zip(before, after):
            if b['kind'] == 'Deployment':
                b['spec']['template']['spec']['containers'][0]['image'] = a['spec']['template']['spec']['containers'][0]['image']
            self.assertEqual(b, a)
        self.assertEqual(chart_hash, hashlib.sha256(b''.join(p.read_bytes() for p in sorted(CHART.rglob('*')) if p.is_file())).hexdigest())

    def test_missing_bindings_tags_literals_unknown_modes_and_unsafe_endpoints_fail(self):
        self.bad({})
        variants = []
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
