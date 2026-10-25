"""Render review templates with explicitly synthetic references and inspect wiring; never deploy."""
from pathlib import Path
from string import Template
import unittest
import subprocess
import yaml

ROOT = Path(__file__).parent


class WiringTests(unittest.TestCase):
    def render(self, name):
        template = Template((ROOT / 'templates' / name).read_text())
        keys = {match.group('named') or match.group('braced') for match in template.pattern.finditer(template.template)} - {None}
        values = {key: 'synthetic-' + key.lower().replace('_', '-') for key in keys}
        values.update(CUSTODY_APP_PORT='8080', CUSTODY_APP_PROTOCOL='http', DAPR_INTERNAL_GRPC_PORT='50002',
                      DAPR_SENTRY_TOKEN_EXPIRATION_SECONDS='600', WORKER_GROUP_ID='2001')
        return list(yaml.safe_load_all(template.substitute(values))), values

    def test_sidecar_identity_host_channel_and_private_service_wiring(self):
        docs, values = self.render('workload.yaml.in'); deployment, service = docs
        pod = deployment['spec']['template']['spec']
        self.assertFalse(pod['automountServiceAccountToken'])
        app, daprd = pod['containers']; component = pod['initContainers'][0]
        args = daprd['args']
        def value(flag): return args[args.index(flag) + 1]
        self.assertIn('--enable-mtls', args)
        self.assertEqual('8080', value('--app-port')); self.assertEqual('http', value('--app-protocol'))
        self.assertEqual('127.0.0.1', value('--dapr-listen-addresses'))
        self.assertEqual(values['DAPR_CONTROL_PLANE_ADDRESS'], value('--control-plane-address'))
        self.assertEqual(values['DAPR_SENTRY_ADDRESS'], value('--sentry-address'))
        env = {entry['name']: entry for entry in daprd['env']}
        self.assertEqual('/var/run/dapr-identity/token', env['DAPR_SENTRY_TOKEN_FILE']['value'])
        self.assertIn('secretKeyRef', env['DAPR_TRUST_ANCHORS']['valueFrom'])
        appenv = {entry['name']: entry for entry in app['env']}
        for key in ['DAPR_API_TOKEN', 'APP_API_TOKEN']: self.assertEqual(env[key]['valueFrom'], appenv[key]['valueFrom'])
        volumes = {volume['name']: volume for volume in pod['volumes']}
        token = volumes['dapr-identity']['projected']['sources'][0]['serviceAccountToken']
        self.assertEqual(values['DAPR_SENTRY_TOKEN_AUDIENCE'], token['audience'])
        self.assertEqual(600, token['expirationSeconds'])
        self.assertEqual(values['CUSTODY_APP_ID'] + '-dapr', service['metadata']['name'])
        self.assertEqual(deployment['spec']['template']['metadata']['labels'], service['spec']['selector'])
        self.assertEqual([50002], [port['port'] for port in service['spec']['ports']])
        self.assertEqual('dapr-internal', service['spec']['ports'][0]['targetPort'])
        self.assertEqual(50002, daprd['ports'][0]['containerPort'])
        self.assertEqual('50002', value('--dapr-internal-grpc-port'))
        self.assertEqual('true', next(e['value'] for e in component['env'] if e['name'] == 'AWS_EC2_METADATA_DISABLED'))
        self.assertEqual('/credentials/credentials', next(e['value'] for e in component['env'] if e['name'] == 'CUSTODY_REFERENCE_CREDENTIAL_FILE'))
        self.assertEqual('credentials', volumes['component-credentials']['secret']['items'][0]['path'])

    def test_worker_group_output_and_consumed_credential_mount(self):
        (worker,), values = self.render('worker.yaml.in'); pod = worker['spec']['template']['spec']
        self.assertEqual(pod['securityContext']['runAsGroup'], pod['securityContext']['fsGroup'])
        self.assertEqual(2001, pod['securityContext']['fsGroup'])
        self.assertEqual('OnRootMismatch', pod['securityContext']['fsGroupChangePolicy'])
        env = {entry['name']: entry['value'] for entry in pod['containers'][0]['env']}
        self.assertEqual(values['WORKER_CREDENTIAL_PROFILE'], env['CUSTODY_REFERENCE_CREDENTIAL_PROFILE'])
        self.assertEqual('/credentials/credentials', env['CUSTODY_REFERENCE_CREDENTIAL_FILE'])
        self.assertEqual('true', env['AWS_EC2_METADATA_DISABLED'])
        secret = next(v for v in pod['volumes'] if v['name'] == 'worker-credentials')['secret']
        self.assertEqual(values['WORKER_CREDENTIAL_KEY'], secret['items'][0]['key'])
        self.assertEqual('credentials', secret['items'][0]['path'])

    def test_only_custody_evidence_logs_have_an_ignore_exception(self):
        workspace = ROOT.parents[2]
        for path in ['docs/implementation/custody-lifecycle-foundation-2026-10-25/verification/final-image-context.log',
                     'docs/implementation/custody-lifecycle-foundation-2026-10-25/verification/review-repairs/new.log']:
            result = subprocess.run(['git', 'check-ignore', '--no-index', '--quiet', path], cwd=workspace)
            self.assertEqual(1, result.returncode)
        self.assertEqual(0, subprocess.run(['git', 'check-ignore', '--no-index', '--quiet', 'docs/implementation/unrelated.log'], cwd=workspace).returncode)
