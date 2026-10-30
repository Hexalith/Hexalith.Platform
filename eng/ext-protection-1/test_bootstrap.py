"""Prepared bootstrap credential redirect regression; only synthetic loopback HTTP is used."""
import importlib.util
import ipaddress
import json
import tempfile
from unittest.mock import patch
from pathlib import Path
import threading
import unittest
import urllib.error
import urllib.request
from urllib.parse import urlsplit
from http.server import BaseHTTPRequestHandler, HTTPServer

spec = importlib.util.spec_from_file_location('erc_bootstrap', Path(__file__).resolve().parents[2] / 'deploy/erasure-root/bootstrap.py')
bootstrap = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bootstrap)


def required_scopes(tenant):
    return [tenant + ':protection:candidate-interaction-occurrences-v1|candidate-interaction-occurrences-v1',
            tenant + ':protection:deletion-consumption-v23|deletion-consumption-v23',
            tenant + ':custody:key-lifecycle-v1|custody-key-lifecycle-candidate-v1']


class BootstrapRedirectTests(unittest.TestCase):
    def test_credential_never_follows_redirect_to_another_origin(self):
        seen = []
        class Receiver(BaseHTTPRequestHandler):
            def do_GET(self):
                seen.append(self.headers.get('X-Vault-Token'))
                self.send_response(200)
                self.end_headers()
            def log_message(self, *args):
                pass
        target = HTTPServer(('127.0.0.1', 0), Receiver)
        class Redirect(BaseHTTPRequestHandler):
            def do_GET(self):
                self.send_response(302)
                self.send_header('Location', 'http://127.0.0.1:' + str(target.server_port) + '/stolen')
                self.end_headers()
            def log_message(self, *args):
                pass
        origin = HTTPServer(('127.0.0.1', 0), Redirect)
        threads = [threading.Thread(target=server.serve_forever, daemon=True) for server in (origin, target)]
        for thread in threads:
            thread.start()
        try:
            request = urllib.request.Request('http://127.0.0.1:' + str(origin.server_port) + '/', headers={'X-Vault-Token': 'synthetic-component-credential'})
            with self.assertRaises(urllib.error.HTTPError) as rejected:
                urllib.request.build_opener(bootstrap.NoRedirect()).open(request, timeout=2)
            self.assertEqual(rejected.exception.code, 302)
            self.assertEqual(seen, [])
        finally:
            for server in (origin, target):
                server.shutdown()
                server.server_close()
            for thread in threads:
                thread.join(timeout=2)

    def test_exact_public_mounts_agree_with_shipped_policy_before_any_native_effect(self):
        value = {'address': 'https://erc.invalid', 'storeId': 'erc-test', 'enrollmentId': 'enrollment', 'transitMount': 'erc-transit', 'headMount': 'erc-heads',
                 'nativeKeys': {'tenant-a': 'epoch-a'}, 'genesisAnchors': {'tenant-a': {scope: 'A' * 64 for scope in required_scopes('tenant-a')}}}
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'enrollment.json'
            path.write_text(json.dumps(value))
            self.assertEqual(bootstrap.enrollment(path), value)
            for field in ('transitMount', 'headMount'):
                changed = dict(value, **{field: 'valid-but-unusable-alternate'})
                path.write_text(json.dumps(changed))
                with patch.object(bootstrap.urllib.request, 'build_opener') as transport:
                    with self.assertRaisesRegex(ValueError, 'erc-transit and erc-heads'):
                        bootstrap.enrollment(path)
                    transport.assert_not_called()


if __name__ == '__main__':
    unittest.main()

class PreparedReadinessTests(unittest.TestCase):
    def test_probe_uses_verified_https_native_health_and_never_listening_tcp(self):
        source = (Path(__file__).resolve().parents[2] / 'deploy/erasure-root/statefulset.yaml').read_text()
        probe = source.split('readinessProbe:', 1)[1].split('      volumes:', 1)[0]
        self.assertNotIn('tcpSocket:', probe)
        self.assertIn('exec:', probe)
        for required in ('- bao', '- read', '- -address=https://127.0.0.1:8200', '- -ca-cert=/bao/tls/ca.crt', '- -tls-server-name=erasure-root.erasure-root.svc', '- sys/health'):
            self.assertIn(required, probe)
        for unsafe in ('tls-skip-verify', 'standbyok=true', 'sealedcode=200', 'uninitcode=200'):
            self.assertNotIn(unsafe, probe)

    def test_unready_headless_service_dns_is_not_needed_to_become_ready(self):
        source = (Path(__file__).resolve().parents[2] / 'deploy/erasure-root/statefulset.yaml').read_text()
        service = source.split('kind: Service\n', 1)[1].split('---', 1)[0]
        self.assertIn('clusterIP: None', service)
        self.assertNotIn('publishNotReadyAddresses: true', service)
        probe = source.split('readinessProbe:', 1)[1].split('      volumes:', 1)[0]
        command = [line.strip()[2:] for line in probe.splitlines() if line.strip().startswith('- ')]
        destination = urlsplit(next(value.removeprefix('-address=') for value in command if value.startswith('-address=')))
        # An IP literal never needs the absent ready-Service DNS endpoint. Certificate
        # identity remains the installed Service name through the native TLS override.
        self.assertEqual(destination.scheme, 'https')
        self.assertTrue(ipaddress.ip_address(destination.hostname).is_loopback)
        self.assertEqual(destination.port, 8200)
        self.assertIn('-tls-server-name=erasure-root.erasure-root.svc', command)

class CanonicalEnrollmentAndDefaultCliTests(unittest.TestCase):
    @staticmethod
    def valid():
        return {'address': 'https://127.0.0.1:8200', 'storeId': 'erc-test', 'enrollmentId': 'accepted-enrollment',
                'transitMount': 'erc-transit', 'headMount': 'erc-heads', 'nativeKeys': {'tenant-a': 'epoch-a', 'tenant-b': 'epoch-b'},
                'genesisAnchors': {tenant: {scope: 'A' * 64 for scope in required_scopes(tenant)} for tenant in ('tenant-a', 'tenant-b')}}

    def test_store_and_enrollment_identities_are_canonical_bounded_strings_before_any_request(self):
        for field in ('storeId', 'enrollmentId'):
            for invalid in (1, [], {}, True, '', ' ', 'a b', 'a\n', 'x' * 257):
                value = self.valid(); value[field] = invalid
                with tempfile.TemporaryDirectory() as directory:
                    path = Path(directory) / 'enrollment.json'; path.write_text(json.dumps(value))
                    with patch.object(bootstrap.urllib.request, 'build_opener') as native:
                        self.assertEqual(bootstrap.main(['--enrollment', str(path), '--apply']), 1)
                        native.assert_not_called()

    def test_full_tenant_and_scope_grammar_and_duplicate_json_refuse_before_transport(self):
        import copy
        mutations = [lambda v: v['nativeKeys'].update({'Tenant-A': v['nativeKeys'].pop('tenant-a')}),
                     lambda v: v['genesisAnchors'].update({'Tenant-A': v['genesisAnchors'].pop('tenant-a')}),
                     lambda v: v['genesisAnchors']['tenant-a'].update({'tenant-b:custody:key-lifecycle-v1|state': 'A' * 64}),
                     lambda v: v['genesisAnchors']['tenant-a'].update({'tenant-a:custody:bad/actor|state': 'A' * 64}),
                     lambda v: v['genesisAnchors']['tenant-a'].update({'unrelated:scope': 'A' * 64}),
                     lambda v: v['nativeKeys'].update({'tenant-b': 'epoch-a'})]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'enrollment.json'
            path.write_text(json.dumps(self.valid()))
            self.assertEqual(bootstrap.enrollment(path), self.valid())
            for mutate in mutations:
                value = copy.deepcopy(self.valid()); mutate(value); path.write_text(json.dumps(value))
                with patch.object(bootstrap.urllib.request, 'build_opener') as native:
                    with self.assertRaises(ValueError): bootstrap.enrollment(path)
                    native.assert_not_called()
            path.write_text(json.dumps(self.valid()).replace('"epoch-a"', '"epoch-a", "tenant-a":"epoch-other"'))
            with self.assertRaisesRegex(ValueError, 'Duplicate'): bootstrap.enrollment(path)

    def test_every_runtime_genesis_is_required_before_any_transport_and_names_match_shipped_owners(self):
        for tenant in ('tenant-a', 'tenant-b'):
            for scope in required_scopes(tenant):
                value = self.valid(); del value['genesisAnchors'][tenant][scope]
                with tempfile.TemporaryDirectory() as directory:
                    path = Path(directory) / 'enrollment.json'; path.write_text(json.dumps(value))
                    with patch.dict('os.environ', {'ERC_BOOTSTRAP_TOKEN': 'synthetic-scope-regression-credential'}), patch.object(bootstrap.urllib.request, 'build_opener') as native:
                        with self.assertRaises(ValueError): bootstrap.enrollment(path)
                        self.assertEqual(bootstrap.main(['--enrollment', str(path), '--apply']), 1); native.assert_not_called()
        platform = Path(__file__).resolve().parents[2]
        actor_sources = [platform / '../eventstore/src/Hexalith.EventStore.Server/Security/InteractionOccurrenceRegistryActor.cs',
                         platform / '../eventstore/src/Hexalith.EventStore.Server/Security/DeletionConsumptionActor.cs',
                         platform / 'src/Hexalith.Platform.Custody/CustodyKeyLifecycleActor.cs']
        for scope, path in zip(required_scopes('tenant-a'), actor_sources):
            domain, actor = scope.split('|')[0].split(':')[1:]
            text = path.read_text(); self.assertIn('"' + domain + '", "' + actor + '"', text)
            self.assertIn('"' + scope.split('|')[1] + '"', text)

    def test_actual_cli_defaults_to_prepared_only_with_available_credential_and_zero_native_requests(self):
        import os
        import ssl
        import subprocess
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); cert = root / 'cert.pem'; key = root / 'key.pem'
            subprocess.run(['openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-keyout', str(key), '-out', str(cert), '-days', '1',
                            '-subj', '/CN=127.0.0.1', '-addext', 'subjectAltName=IP:127.0.0.1'], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            requests = []
            class Native(BaseHTTPRequestHandler):
                def do_GET(self):
                    requests.append('GET'); data = json.dumps({'version': '2.6.2', 'initialized': True, 'sealed': False}).encode()
                    self.send_response(200); self.end_headers(); self.wfile.write(data)
                def do_POST(self):
                    requests.append('POST'); self.rfile.read(int(self.headers.get('Content-Length', 0))); self.send_response(204); self.end_headers()
                def log_message(self, *args): pass
            server = HTTPServer(('127.0.0.1', 0), Native)
            context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER); context.load_cert_chain(cert, key); server.socket = context.wrap_socket(server.socket, server_side=True)
            thread = threading.Thread(target=server.serve_forever, daemon=True); thread.start()
            try:
                value = self.valid(); value['address'] = 'https://127.0.0.1:' + str(server.server_port)
                path = root / 'enrollment.json'; path.write_text(json.dumps(value))
                environment = dict(os.environ, ERC_BOOTSTRAP_TOKEN='synthetic-local-test-credential', SSL_CERT_FILE=str(cert))
                result = subprocess.run(['python3', str(Path(bootstrap.__file__)), '--enrollment', str(path)], env=environment, capture_output=True, text=True, timeout=10)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertEqual(json.loads(result.stdout), {'status': 'PreparedOnly', 'tenants': 2, 'effects': 0})
                self.assertEqual(requests, [])
            finally:
                server.shutdown(); server.server_close(); thread.join(timeout=2)
