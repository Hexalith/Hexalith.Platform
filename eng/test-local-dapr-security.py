#!/usr/bin/env python3
"""Live fixture qualification of Platform-rendered Dapr policies/subscriptions; no owner release claims."""
import http.server
import json
import os
from pathlib import Path
import signal
import shutil
import hashlib
import socket
import subprocess
import sys
import tempfile
import threading
import time
import urllib.error
import urllib.request
import uuid

fixtures, dapr_home, runtime_version, cli_version, redis_image = sys.argv[1:]
scope = os.environ.get('HEXALITH_PLATFORM_SECURITY_SCOPE', 'full')
assert scope in ['full', 'timesheets-invocation'], scope
fixtures = Path(fixtures)
dapr_home = Path(dapr_home)
assert subprocess.check_output([str(dapr_home / '.dapr/bin/daprd'), '--version'], text=True).strip() == runtime_version
assert f'CLI version: {cli_version}' in subprocess.check_output([str(dapr_home / 'tools/dapr'), '--version'], text=True)

def port():
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        return sock.getsockname()[1]

def request(url, data=None, method=None):
    try:
        with urllib.request.urlopen(urllib.request.Request(url, data=data, method=method, headers={'Content-Type': 'application/json'}), timeout=3) as response:
            return response.status, response.read().decode()
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode()

def until(check, label, timeout=40):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            if check():
                return
        except (OSError, urllib.error.URLError):
            pass
        time.sleep(.2)
    raise AssertionError(f'{label} did not complete in {timeout}s')

received = []
class App(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path.startswith('/api/v1/commands/status/'):
            self.send_response(200)
            self.end_headers()
            self.wfile.write(b'{}')
            return
        # Retain discovery probes; the 404 supplies no programmatic registrations.
        if self.path == '/dapr/subscribe':
            received.append((self.server.app_id, 'discovery', None))
        self.send_response(404)
        self.end_headers()
    def do_POST(self):
        body = self.rfile.read(int(self.headers.get('Content-Length', 0)))
        payload = json.loads(body or '{}')
        if self.path in ['/events/ingest', '/dead-letter', '/projections/changed']:
            received.append((self.server.app_id, self.path, payload))
        failed = self.path == '/events/ingest' and payload.get('data', {}).get('poison') is True
        self.send_response(500 if failed else 200)
        self.end_headers()
        self.wfile.write(b'{}')
    def log_message(self, *_):
        pass

run = 'platform-security-' + uuid.uuid4().hex[:12]
containers, processes, logs, servers = [], [], [], []
sentry_port = port()
sentry_address = f'localhost:{sentry_port}'
transport = fixtures / 'transport'
transport.mkdir(exist_ok=True)
adaptations = []
for original in fixtures.glob('*.yaml'):
    target = transport / original.name
    if original.name.startswith('accesscontrol.'):
        configuration = json.loads(original.read_text())
        before = configuration['spec']['mtls']['sentryAddress']
        configuration['spec']['mtls']['sentryAddress'] = sentry_address
        target.write_text(json.dumps(configuration))
        adaptations.append({'file': original.name, 'field': 'spec.mtls.sentryAddress', 'before': before, 'after': sentry_address, 'originalSha256': hashlib.sha256(original.read_bytes()).hexdigest(), 'adaptedSha256': hashlib.sha256(target.read_bytes()).hexdigest()})
    else:
        shutil.copyfile(original, target)
(fixtures / 'transport-adaptation.json').write_text(json.dumps({'purpose': 'Fixture-only Sentry transport port; production fixed-port behavior and ACL/subscription content are unchanged.', 'scope': scope, 'launcher': 'direct pinned daprd; Dapr CLI launch is not exercised by this adapted fixture', 'runtimePath': str(dapr_home / '.dapr/bin/daprd'), 'hostPort': sentry_port, 'containerPort': 50001, 'configurations': adaptations}, indent=2))

def start_container(name, args):
    # docker run can create a container before port binding fails; track ownership before launch.
    containers.append(name)
    result = subprocess.run(args, capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError(f'Owned container {name} failed to start: {result.stderr.strip()}')

def retain_runtime_commands():
    commands = []
    for process in processes:
        command = Path(f'/proc/{process.pid}/cmdline').read_bytes().decode().split('\0')
        assert Path(command[0]).name == 'daprd', command
        assert '--enable-mtls' in command, command
        assert command[command.index('--sentry-address') + 1] == sentry_address, command
        commands.append(command)
    (fixtures / 'runtime-commands.json').write_text(json.dumps(commands, indent=2))

def timesheets_invocations(endpoints):
    allowed = {path: request(endpoints['eventstore'] + '/v1.0/invoke/timesheets/method' + path, b'{}')[0] for path in ['/process', '/query', '/project', '/replay-state', '/admin/operational-index-metadata']}
    denied = request(endpoints['memories'] + '/v1.0/invoke/timesheets/method/process', b'{}')[0]
    assert all(status == 200 for status in allowed.values()), allowed
    assert denied == 403, denied
    return allowed, denied

def timesheets_gateway_invocations(endpoints):
    calls = [
        ('POST', '/api/v1/commands'),
        ('POST', '/api/v1/commands/workload'),
        ('POST', '/api/v1/queries'),
        ('GET', '/api/v1/commands/status/fixture-message'),
        ('GET', '/api/v1/commands/status/workload/fixture-tenant/fixture-message'),
    ]
    gateway = endpoints['timesheets'] + '/v1.0/invoke/eventstore/method'
    allowed = {f'{method} {path}': request(gateway + path, b'{}' if method == 'POST' else None, method=method)[0] for method, path in calls}
    denied_get = request(gateway + '/api/v1/commands/unlisted', method='GET')[0]
    assert all(status == 200 for status in allowed.values()), allowed
    assert denied_get == 403, denied_get
    return allowed, denied_get

with tempfile.TemporaryDirectory(prefix=run) as directory:
    credentials = Path(directory) / 'credentials'
    credentials.mkdir()
    try:
        sentry_name = run + '-sentry'
        start_container(sentry_name, ['docker', 'run', '-d', '--name', sentry_name, '-p', f'127.0.0.1:{sentry_port}:50001', '--user', f'{os.getuid()}:{os.getgid()}', '-v', f'{credentials}:/credentials', '-v', f'{transport / "accesscontrol.eventstore.yaml"}:/config.yaml:ro', '--entrypoint', '/sentry', f'daprio/sentry:{runtime_version}', '--issuer-credentials=/credentials', '--config=/config.yaml', '--trust-domain=localhost', '--listen-address=0.0.0.0'])
        until(lambda: all((credentials / file).exists() and (credentials / file).stat().st_size for file in ['ca.crt', 'issuer.crt', 'issuer.key']), 'Sentry credentials')
        redis_name = run + '-redis'
        redis_port = int((fixtures / 'redis-port').read_text())
        start_container(redis_name, ['docker', 'run', '-d', '--name', redis_name, '-p', f'127.0.0.1:{redis_port}:6379', redis_image])
        until(lambda: subprocess.run(['docker', 'exec', redis_name, 'redis-cli', 'ping'], capture_output=True).stdout.strip() == b'PONG', 'Redis readiness')
        env = os.environ.copy()
        env.update(DAPR_TRUST_ANCHORS=(credentials / 'ca.crt').read_text(), DAPR_CERT_CHAIN=(credentials / 'issuer.crt').read_text(), DAPR_CERT_KEY=(credentials / 'issuer.key').read_text(), DAPR_CONTROLPLANE_TRUST_DOMAIN='localhost', DAPR_CONTROLPLANE_NAMESPACE='default', NAMESPACE='default')
        endpoints = {}
        apps = ['eventstore', 'memories', 'timesheets'] if scope == 'timesheets-invocation' else ['eventstore', 'tenants', 'memories', 'tenants-api', 'timesheets']
        for app_id in apps:
            app_port, http_port, grpc_port, internal_port, metrics_port = [port() for _ in range(5)]
            app = http.server.ThreadingHTTPServer(('127.0.0.1', app_port), App)
            app.app_id = app_id
            threading.Thread(target=app.serve_forever, daemon=True).start()
            servers.append(app)
            endpoint = f'http://127.0.0.1:{http_port}'
            endpoints[app_id] = endpoint
            log = open(fixtures / f'live-{app_id}.log', 'w')
            logs.append(log)
            args = [str(dapr_home / '.dapr/bin/daprd'), '--mode', 'standalone', '--enable-mtls', '--sentry-address', sentry_address, '--app-id', app_id, '--app-port', str(app_port), '--app-channel-address', '127.0.0.1', '--dapr-http-port', str(http_port), '--dapr-grpc-port', str(grpc_port), '--dapr-internal-grpc-port', str(internal_port), '--metrics-port', str(metrics_port), '--placement-host-address', '', '--scheduler-host-address', '', '--config', str(transport / f'accesscontrol.{app_id}.yaml'), '--resources-path', str(transport)]
            processes.append(subprocess.Popen(args, env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True))
            until(lambda: request(endpoint + '/v1.0/healthz/outbound')[0] == 204, app_id + ' outbound readiness')
        # The pinned runtime may probe /dapr/subscribe during initialization.
        # A 404 provides no programmatic registrations. All deliveries below use rendered Subscription objects.
        if scope == 'timesheets-invocation':
            allowed, denied = timesheets_invocations(endpoints)
            gateway_allowed, gateway_denied_get = timesheets_gateway_invocations(endpoints)
            retain_runtime_commands()
            print(json.dumps({'scope': scope, 'evidence': 'live rendered Timesheets receiver and gateway invocations; delivery checks are not exercised', 'launcher': 'direct pinned daprd with fixture-only Sentry transport adaptation', 'runtime': runtime_version, 'timesheetsAllowed': allowed, 'timesheetsDeniedCaller': denied, 'timesheetsGatewayAllowed': gateway_allowed, 'timesheetsGatewayDeniedGet': gateway_denied_get}))
            raise SystemExit(0)
        permitted = request(endpoints['eventstore'] + '/v1.0/invoke/tenants/method/process', b'{}')
        assert permitted[0] == 200, permitted
        denied = request(endpoints['memories'] + '/v1.0/invoke/tenants/method/process', b'{}')
        assert denied[0] == 403, denied
        wrong_verb = request(endpoints['eventstore'] + '/v1.0/invoke/tenants/method/process', method='GET')
        assert wrong_verb[0] == 403, wrong_verb
        wrong_path = request(endpoints['eventstore'] + '/v1.0/invoke/tenants/method/unlisted', b'{}')
        assert wrong_path[0] == 403, wrong_path
        gateway_commands = request(endpoints['tenants-api'] + '/v1.0/invoke/eventstore/method/api/v1/commands', b'{}')
        gateway_queries = request(endpoints['tenants-api'] + '/v1.0/invoke/eventstore/method/api/v1/queries', b'{}')
        gateway_denied_caller = request(endpoints['memories'] + '/v1.0/invoke/eventstore/method/api/v1/commands', b'{}')
        gateway_denied_verb = request(endpoints['tenants-api'] + '/v1.0/invoke/eventstore/method/api/v1/commands', method='GET')
        gateway_denied_path = request(endpoints['tenants-api'] + '/v1.0/invoke/eventstore/method/admin', b'{}')
        assert gateway_commands[0] == gateway_queries[0] == 200, (gateway_commands, gateway_queries)
        assert gateway_denied_caller[0] == gateway_denied_verb[0] == gateway_denied_path[0] == 403, (gateway_denied_caller, gateway_denied_verb, gateway_denied_path)
        timesheets_allowed, timesheets_denied = timesheets_invocations(endpoints)
        timesheets_gateway_allowed, timesheets_gateway_denied_get = timesheets_gateway_invocations(endpoints)
        for poison in [False, True]:
            published = request(endpoints['eventstore'] + '/v1.0/publish/pubsub/memories-events', json.dumps({'poison': poison, 'id': run}).encode())
            assert published[0] in [200, 204], published
        until(lambda: any(route == '/events/ingest' and value.get('data', {}).get('poison') is False for app_id, route, value in received), 'declarative delivery')
        until(lambda: any(route == '/dead-letter' and value.get('data', {}).get('poison') is True for app_id, route, value in received), 'poison dead-letter delivery')
        assert all(app_id == 'memories' for app_id, route, _ in received if route in ['/events/ingest', '/dead-letter']), received
        projection_topic = 'fixture-tenant.fixture-view.projection-changed'
        projection_published = request(endpoints['eventstore'] + '/v1.0/publish/pubsub/' + projection_topic, json.dumps({'id': run}).encode())
        assert projection_published[0] in [200, 204], projection_published
        time.sleep(2)
        projection_observed = any(route == '/projections/changed' for app_id, route, _ in received)
        (fixtures / 'projection-observation.json').write_text(json.dumps({'concreteTopic': projection_topic, 'publishedStatus': projection_published[0], 'callbackObservedWithinSeconds': 2, 'callbackObserved': projection_observed, 'qualification': 'Existing Redis wildcard fan-out is not qualified; the source-established pattern is preserved.'}, indent=2))
        for log in logs:
            log.flush()
        retain_runtime_commands()
        print(json.dumps({'evidence': 'live rendered fixtures', 'launcher': 'direct pinned daprd with fixture-only Sentry transport adaptation', 'runtime': runtime_version, 'cliPrerequisiteVersion': cli_version, 'permittedInvocation': permitted[0], 'deniedCaller': denied[0], 'deniedVerb': wrong_verb[0], 'deniedPath': wrong_path[0], 'gatewayCommands': gateway_commands[0], 'gatewayQueries': gateway_queries[0], 'gatewayDeniedCaller': gateway_denied_caller[0], 'gatewayDeniedVerb': gateway_denied_verb[0], 'gatewayDeniedPath': gateway_denied_path[0], 'timesheetsAllowed': timesheets_allowed, 'timesheetsDeniedCaller': timesheets_denied, 'timesheetsGatewayAllowed': timesheets_gateway_allowed, 'timesheetsGatewayDeniedGet': timesheets_gateway_denied_get, 'declarativeDelivery': True, 'deadLetterDelivery': True, 'deadLetterPhysicalTopic': 'failed.physical', 'concreteProjectionCallbackObserved': projection_observed, 'programmaticSubscriptions': 0}))
    finally:
        for process in processes:
            if process.poll() is None:
                try:
                    os.killpg(process.pid, signal.SIGTERM)
                except ProcessLookupError:
                    pass
        for process in processes:
            try:
                process.wait(timeout=8)
            except subprocess.TimeoutExpired:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                process.wait(timeout=5)
        for app in servers:
            app.shutdown()
            app.server_close()
        for log in logs:
            log.close()
        for container in reversed(containers):
            if subprocess.run(['docker', 'container', 'inspect', container], capture_output=True).returncode:
                continue
            subprocess.run(['docker', 'logs', container], stdout=open(fixtures / f'live-{container.rsplit("-", 1)[-1]}.log', 'w'), stderr=subprocess.STDOUT, check=False)
            subprocess.run(['docker', 'rm', '-f', container], check=True, capture_output=True)
        leftovers = subprocess.check_output(['docker', 'ps', '-aq', '--filter', f'name={run}'], text=True).strip()
        assert not leftovers, leftovers
        assert all(process.poll() is not None for process in processes)
        print('Owned sidecars and containers removed; no leftovers.')
