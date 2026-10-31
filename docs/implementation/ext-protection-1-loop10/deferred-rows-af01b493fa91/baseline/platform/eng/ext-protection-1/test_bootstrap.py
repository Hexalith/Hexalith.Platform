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


class PreparedReadinessTests(unittest.TestCase):
    @staticmethod
    def script(section, ending):
        import textwrap
        source = (Path(__file__).resolve().parents[2] / 'deploy/erasure-root/statefulset.yaml').read_text()
        return textwrap.dedent(source.split(section, 1)[1].split(ending, 1)[0])

    def test_prepared_startup_requires_exact_memory_and_zero_swap_before_openbao(self):
        import os
        import subprocess
        script = self.script('  start-erasure-root.sh: |\n', '  erasure-root.hcl: |\n')
        source = (Path(__file__).resolve().parents[2] / 'deploy/erasure-root/statefulset.yaml').read_text()
        self.assertIn('command: [/bin/sh, /bao/config/start-erasure-root.sh]', source)
        self.assertNotIn('${', script)
        fragment = (Path(__file__).resolve().parents[2] / 'deploy/erasure-root/kubelet-noswap.fragment.yaml').read_text()
        self.assertIn('swapBehavior: NoSwap', fragment)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); cells = root / 'cgroup'; cells.mkdir(); started = root / 'started'
            bao = root / 'bao'; bao.write_text('#!/bin/sh\nprintf "%s\\n" "$*" > "$ERC_TEST_STARTED"\n'); bao.chmod(0o700)
            # Only this test copy replaces fixed production kernel paths. There is
            # no environment variable or writable-path override in the shipped script.
            fixture = script.replace('/sys/fs/cgroup/', str(cells) + '/')
            self.assertEqual(script.count('/sys/fs/cgroup/'), 2)
            for memory, swap, succeeds in [('1073741824', '0', True), ('1073741824', '1', False),
                                           ('1073741824', '1073741824', False), ('1073741824', 'max', False),
                                           ('max', '0', False), ('2147483648', '0', False),
                                           ('268435456', '0', False), (None, '0', False), ('1073741824', None, False)]:
                for filename, value in [('memory.max', memory), ('memory.swap.max', swap)]:
                    path = cells / filename
                    if value is None:
                        path.unlink(missing_ok=True)
                    else:
                        path.write_text(value + '\n')
                started.unlink(missing_ok=True)
                result = subprocess.run(['/bin/sh', '-ec', fixture], env=dict(os.environ, PATH=str(root) + os.pathsep + os.defpath,
                                        ERC_TEST_STARTED=str(started)), capture_output=True, text=True, timeout=5)
                self.assertEqual(result.returncode == 0, succeeds, (memory, swap, result.stderr))
                self.assertEqual(started.exists(), succeeds)
                if succeeds:
                    self.assertEqual(started.read_text().strip(), 'server -config=/bao/config/erasure-root.hcl')
                else:
                    self.assertIn('ERC_MEMORY_AND_SWAP_LIMIT_UNVERIFIED', result.stderr)

    def test_actual_readiness_shell_refuses_wrong_native_health(self):
        import os
        import subprocess
        script = self.script('                - |\n', '            timeoutSeconds:')
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            bao = root / 'bao'; bao.write_text('#!/bin/sh\nprintf "%s\\n" "$ERC_TEST_HEALTH"\nexit "$ERC_TEST_HEALTH_STATUS"\n'); bao.chmod(0o700)
            good = {'version': '2.6.2', 'initialized': True, 'sealed': False}
            vectors = [(good, '0', True), ({**good, 'version': '2.6.1'}, '0', False),
                       ({**good, 'initialized': False}, '0', False), ({**good, 'sealed': True}, '0', False),
                       ({'version': '2.6.2', 'initialized': True}, '0', False), (good, '1', False)]
            for health, status, succeeds in vectors:
                result = subprocess.run(['/bin/sh', '-ec', script], env=dict(os.environ, PATH=str(root) + os.pathsep + os.defpath,
                                        ERC_TEST_HEALTH=json.dumps(health), ERC_TEST_HEALTH_STATUS=status), capture_output=True, text=True, timeout=5)
                self.assertEqual(result.returncode == 0, succeeds, (health, status, result.stderr))

    def test_probe_uses_verified_https_native_health_and_never_listening_tcp(self):
        source = (Path(__file__).resolve().parents[2] / 'deploy/erasure-root/statefulset.yaml').read_text()
        probe = source.split('readinessProbe:', 1)[1].split('      volumes:', 1)[0]
        self.assertNotIn('tcpSocket:', probe)
        self.assertIn('exec:', probe)
        for required in ('bao read', '-address=https://127.0.0.1:8200', '-ca-cert=/bao/tls/ca.crt', '-tls-server-name=erasure-root.erasure-root.svc', 'sys/health',
                         '"version"[[:space:]]*:[[:space:]]*"2\\.6\\.2"', '"initialized"[[:space:]]*:[[:space:]]*true', '"sealed"[[:space:]]*:[[:space:]]*false'):
            self.assertIn(required, probe)
        for unsafe in ('tls-skip-verify', 'standbyok=true', 'sealedcode=200', 'uninitcode=200'):
            self.assertNotIn(unsafe, probe)

    def test_unready_headless_service_dns_is_not_needed_to_become_ready(self):
        source = (Path(__file__).resolve().parents[2] / 'deploy/erasure-root/statefulset.yaml').read_text()
        service = source.split('kind: Service\n', 1)[1].split('---', 1)[0]
        self.assertIn('clusterIP: None', service)
        self.assertNotIn('publishNotReadyAddresses: true', service)
        probe = source.split('readinessProbe:', 1)[1].split('      volumes:', 1)[0]
        command = probe
        destination = urlsplit(command.split('-address=', 1)[1].split()[0])
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

    def test_rendered_policy_is_one_tenant_and_manifests_pin_the_client_boundary(self):
        import hashlib
        value = self.valid()
        scope = required_scopes('tenant-a')[0]
        text = bootstrap.render_policy(value, 'tenant-a', scope)
        digest = hashlib.sha256(b'tenant-a').hexdigest()
        self.assertNotIn('tenants/*', text)
        self.assertNotIn('encrypt/*', text)
        self.assertIn('erc-heads/data/tenants/' + digest, text)
        self.assertIn('erc-transit/encrypt/epoch-a', text)
        self.assertNotIn('epoch-b', text)
        self.assertIn('path "erc-transit/export/*"', text)
        self.assertIn('path "erc-transit/backup/*"', text)
        self.assertIn('path "erc-transit/restore/*"', text)
        self.assertIn('"min_decryption_version" = ["1", "2",', text)
        self.assertIn(', "64"]', text)
        self.assertIn('"min_encryption_version" = ["1", "2",', text)
        self.assertNotIn('= []', text)
        self.assertNotIn(', "65"]', text)
        import re
        for field in ('min_decryption_version', 'min_encryption_version'):
            versions = re.search('"' + field + r'" = (\[[^\]]*\])', text)
            self.assertIsNotNone(versions)
            self.assertEqual(json.loads(versions[1]), [str(version) for version in range(1, 65)])
        root = Path(__file__).resolve().parents[2] / 'deploy/erasure-root'
        shared = (root / 'custody-policy.hcl').read_text()
        self.assertNotIn('tenants/*', shared)
        self.assertNotIn('encrypt/*', shared)
        stateful = (root / 'statefulset.yaml').read_text()
        self.assertIn('disable_mlock = true', stateful)
        self.assertIn('emptyDir: {}', stateful)
        self.assertIn('-ca-cert=/bao/tls/ca.crt', stateful)
        self.assertIn('defaultMode: 0400', stateful)
        self.assertIn('max_request_size = 67108864', stateful)
        self.assertIn('max_request_json_memory = 134217728', stateful)
        self.assertLess(67108864 + 134217728, 1024 * 1024 * 1024)
        requests = stateful.split('resources:', 1)[1].split('volumeMounts:', 1)[0]
        self.assertIn('memory: 256Mi', requests)
        self.assertIn('memory: 1Gi', requests)
        policy = (root / 'network-policy.yaml').read_text()
        self.assertNotIn('hexalith.com/erc-client', policy)
        client = policy.split('kubernetes.io/metadata.name: hexalith', 1)[1].split('podSelector:', 1)[0]
        self.assertIn('8200', client)
        self.assertNotIn('8201', client)
        namespace = (root / 'namespace.yaml').read_text()
        self.assertIn('pod-security.kubernetes.io/enforce: restricted', namespace)

    def test_apply_installs_non_deletable_keys_policies_and_reads_heads_back(self):
        import os
        import ssl
        import subprocess
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); cert = root / 'cert.pem'; key = root / 'key.pem'
            subprocess.run(['openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-keyout', str(key), '-out', str(cert), '-days', '1',
                            '-subj', '/CN=127.0.0.1', '-addext', 'subjectAltName=IP:127.0.0.1'], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            other = root / 'other.pem'; other_key = root / 'other.key'
            subprocess.run(['openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-keyout', str(other_key), '-out', str(other), '-days', '1',
                            '-subj', '/CN=127.0.0.1', '-addext', 'subjectAltName=IP:127.0.0.1'], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            class Native(BaseHTTPRequestHandler):
                records = []
                tokens = []
                policies = {}
                heads = {}
                mounts = {}
                keys = {}
                configs = {}
                mode = 'ok'
                def _body(self):
                    length = int(self.headers.get('Content-Length', 0) or 0)
                    return self.rfile.read(length) if length else b''
                def _json(self, payload, status=200):
                    raw = json.dumps(payload).encode(); self.send_response(status)
                    self.send_header('Content-Type', 'application/json'); self.send_header('Content-Length', str(len(raw))); self.end_headers(); self.wfile.write(raw)
                def _missing(self):
                    self.send_response(404); self.end_headers()
                def _committed(self, stage):
                    if Native.mode == 'stop-after-' + stage:
                        self._json({'errors': ['synthetic acknowledgement loss']}, 503)
                    else:
                        self.send_response(204); self.end_headers()
                def do_GET(self): self._dispatch('GET', self._body())
                def do_POST(self): self._dispatch('POST', self._body())
                def _dispatch(self, method, body):
                    Native.tokens.append(self.headers.get('X-Vault-Token'))
                    Native.records.append((method, self.path, body))
                    if self.path.endswith('/sys/health'):
                        self._json({'version': '2.6.2', 'initialized': True, 'sealed': False}); return
                    if self.path.endswith('/sys/mounts'):
                        mounts = {name + '/': value for name, value in Native.mounts.items()}
                        if Native.mode == 'mount-mismatch':
                            mounts['erc-transit/'] = {'type': 'kv', 'options': {}}
                        self._json({'data': mounts}); return
                    if '/sys/mounts/' in self.path:
                        name = self.path.split('/sys/mounts/', 1)[1].strip('/')
                        if method == 'GET':
                            if Native.mode == 'mount-mismatch' and name == 'erc-transit':
                                self._json({'type': 'kv', 'options': {}}); return
                            if name not in Native.mounts:
                                self._json({'errors': ['No secret engine mount at ' + name + '/']}, 400); return
                            self._json({'data': Native.mounts[name]}); return
                        payload = json.loads(body)
                        Native.mounts[name] = {'type': payload['type'], 'options': payload.get('options') or {}}
                        if name == 'erc-heads':
                            # Real KV-v2 returns these defaults immediately after
                            # mount creation, rather than a missing config path.
                            Native.configs['config'] = {'max_versions': 0, 'cas_required': False, 'delete_version_after': '0s'}
                            if Native.mode == 'fresh-config-conflict':
                                Native.configs['config']['max_versions'] = 2
                        self._committed('mount'); return
                    if self.path.endswith('/config') and '/sys/' not in self.path:
                        if method == 'GET':
                            if 'config' not in Native.configs:
                                self._missing(); return
                            if Native.mode == 'config-readback-mismatch' and Native.configs['config']['max_versions'] == 1:
                                self._json({'data': {'max_versions': 0, 'cas_required': False}}); return
                            self._json({'data': Native.configs['config']}); return
                        Native.configs['config'] = json.loads(body); self.send_response(204); self.end_headers(); return
                    if '/keys/' in self.path:
                        if method == 'GET':
                            if Native.mode == 'key-mismatch':
                                self._json({'data': {'type': 'aes256-gcm96', 'derived': True, 'exportable': False, 'allow_plaintext_backup': False, 'deletion_allowed': True}}); return
                            if self.path not in Native.keys:
                                self._missing(); return
                            self._json({'data': Native.keys[self.path]}); return
                        Native.keys[self.path] = json.loads(body); self._committed('key'); return
                    if '/sys/policies/acl/' in self.path:
                        if method == 'POST':
                            Native.policies[self.path] = json.loads(body)['policy']; self._committed('policy'); return
                        if Native.mode == 'policy-missing':
                            self._missing(); return
                        if Native.mode == 'policy-mismatch':
                            self._json({'policy': 'different'}); return
                        if self.path not in Native.policies:
                            self._missing(); return
                        self._json({'policy': Native.policies[self.path]}); return
                    if '/data/tenants/' in self.path:
                        if method == 'POST':
                            Native.heads[self.path] = json.loads(body)['data']; self._committed('head'); return
                        if Native.mode == 'head-mismatch':
                            self._json({'data': {'data': {'Epoch': 2}, 'metadata': {'version': 1}}}); return
                        if self.path not in Native.heads:
                            self._missing(); return
                        self._json({'data': {'data': Native.heads[self.path], 'metadata': {'version': 1}}}); return
                    self._missing()
                def log_message(self, *args): pass
            server = HTTPServer(('127.0.0.1', 0), Native)
            context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER); context.load_cert_chain(cert, key); server.socket = context.wrap_socket(server.socket, server_side=True)
            thread = threading.Thread(target=server.serve_forever, daemon=True); thread.start()
            def run(apply, mode, ca=cert, keep=False, pin=True):
                Native.mode = mode; Native.records.clear(); Native.tokens.clear()
                if not keep:
                    Native.policies.clear(); Native.heads.clear(); Native.mounts.clear(); Native.keys.clear(); Native.configs.clear()
                value = self.valid(); value['address'] = 'https://127.0.0.1:' + str(server.server_port)
                path = root / 'enrollment.json'; path.write_text(json.dumps(value))
                command = ['python3', str(Path(bootstrap.__file__)), '--enrollment', str(path)]
                if apply:
                    command.append('--apply')
                    if pin: command.extend(['--ca-certificate', str(ca)])
                environment = dict(os.environ, ERC_BOOTSTRAP_TOKEN='synthetic-local-test-credential', SSL_CERT_FILE=str(other))
                return subprocess.run(command, env=environment, capture_output=True, text=True, timeout=20)
            try:
                prepared = run(False, 'ok')
                self.assertEqual(prepared.returncode, 0, prepared.stdout + prepared.stderr)
                self.assertEqual(Native.records, [])
                unpinned = run(True, 'ok', pin=False)
                self.assertEqual(unpinned.returncode, 1, unpinned.stdout + unpinned.stderr)
                self.assertEqual(json.loads(unpinned.stdout)['step'], 'ca-pin')
                self.assertEqual(Native.records, [])
                rejected = run(True, 'ok', ca=other)
                self.assertEqual(rejected.returncode, 1, rejected.stdout + rejected.stderr)
                self.assertEqual(json.loads(rejected.stdout)['step'], 'health')
                self.assertEqual(Native.tokens, [])
                applied = run(True, 'ok')
                self.assertEqual(applied.returncode, 0, applied.stdout + applied.stderr)
                self.assertEqual(json.loads(applied.stdout)['status'], 'BootstrappedNotQualified')
                self.assertEqual(Native.configs['config'], {'max_versions': 1, 'cas_required': True})
                self.assertEqual([method for method, path, _ in Native.records if path.endswith('/erc-heads/config')], ['GET', 'POST', 'GET'])
                keys = [json.loads(body) for method, path, body in Native.records if method == 'POST' and '/keys/epoch-' in path]
                self.assertEqual(len(keys), 2)
                self.assertTrue(all(item['deletion_allowed'] is False for item in keys))
                self.assertTrue(Native.policies)
                self.assertTrue(all('tenants/*' not in text and 'encrypt/*' not in text and '"min_encryption_version" = ["1", "2",' in text for text in Native.policies.values()))
                resumed = run(True, 'ok', keep=True)
                self.assertEqual(resumed.returncode, 0, resumed.stdout + resumed.stderr)
                self.assertEqual([method for method, _, _ in Native.records if method == 'POST'], [])
                # Defaults are only initialized on a mount created by this run.
                # An existing mismatching mount remains an enrollment conflict.
                for configuration in ({'max_versions': 0, 'cas_required': False}, {'max_versions': 2, 'cas_required': True}):
                    Native.configs['config'] = configuration
                    refused = run(True, 'ok', keep=True)
                    self.assertEqual(refused.returncode, 1, refused.stdout + refused.stderr)
                    self.assertEqual(json.loads(refused.stdout)['step'], 'head-config')
                    self.assertEqual([method for method, _, _ in Native.records if method == 'POST'], [])
                fresh_conflict = run(True, 'fresh-config-conflict')
                self.assertEqual(fresh_conflict.returncode, 1, fresh_conflict.stdout + fresh_conflict.stderr)
                self.assertEqual(json.loads(fresh_conflict.stdout)['step'], 'head-config')
                self.assertEqual([path for method, path, _ in Native.records if method == 'POST' and path.endswith('/erc-heads/config')], [])
                unreadback = run(True, 'config-readback-mismatch')
                self.assertEqual(unreadback.returncode, 1, unreadback.stdout + unreadback.stderr)
                self.assertEqual(json.loads(unreadback.stdout)['step'], 'head-config')
                self.assertEqual(Native.keys, {})
                # Each mutation may commit while its acknowledgement is lost. The
                # next invocation resumes the exact enrollment without rewriting it.
                for stage in ('mount', 'key', 'policy', 'head'):
                    interrupted = run(True, 'stop-after-' + stage)
                    self.assertEqual(interrupted.returncode, 1, interrupted.stdout + interrupted.stderr)
                    self.assertTrue(json.loads(interrupted.stdout)['step'].startswith({'key': 'transit-key'}.get(stage, stage) + ':'))
                    committed_paths = {path for method, path, _ in Native.records if method == 'POST'}
                    continued = run(True, 'ok', keep=True)
                    self.assertEqual(continued.returncode, 0, continued.stdout + continued.stderr)
                    rewritten = {path for method, path, _ in Native.records if method == 'POST'}
                    self.assertTrue(committed_paths.isdisjoint(rewritten), (stage, committed_paths & rewritten))
                missing = run(True, 'policy-missing')
                self.assertEqual(missing.returncode, 1, missing.stdout + missing.stderr)
                self.assertTrue(json.loads(missing.stdout)['step'].startswith('policy:'))
                mismatch = run(True, 'policy-mismatch')
                self.assertEqual(mismatch.returncode, 1, mismatch.stdout + mismatch.stderr)
                self.assertTrue(json.loads(mismatch.stdout)['step'].startswith('policy:'))
                self.assertEqual([method for method, path, _ in Native.records if method == 'POST' and '/sys/policies/acl/' in path], [])
                head = run(True, 'head-mismatch')
                self.assertEqual(head.returncode, 1, head.stdout + head.stderr)
                self.assertTrue(json.loads(head.stdout)['step'].startswith('head:'))
                self.assertEqual([method for method, path, _ in Native.records if method == 'POST' and '/data/tenants/' in path], [])
                mount = run(True, 'mount-mismatch')
                self.assertEqual(mount.returncode, 1, mount.stdout + mount.stderr)
                self.assertEqual(json.loads(mount.stdout)['step'], 'mount:erc-transit')
                self.assertEqual([path for method, path, _ in Native.records if method == 'POST' and path.endswith('/sys/mounts/erc-transit')], [])
                key_mismatch = run(True, 'key-mismatch')
                self.assertEqual(key_mismatch.returncode, 1, key_mismatch.stdout + key_mismatch.stderr)
                self.assertTrue(json.loads(key_mismatch.stdout)['step'].startswith('transit-key:'))
                self.assertEqual([path for method, path, _ in Native.records if method == 'POST' and '/keys/' in path], [])
            finally:
                server.shutdown(); server.server_close(); thread.join(timeout=2)

if __name__ == '__main__':
    unittest.main()
