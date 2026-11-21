"""Run a bounded disposable TLS/OpenBao installation for the signed Platform fixture."""
import base64
from contextlib import contextmanager
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import signal
import socketserver
import ssl
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request

IMAGE = 'quay.io/openbao/openbao@sha256:e29524ba7c3f20d01f562c481e3eccbad6c91df45a2f2531433da4951e408cff'
IMAGE_CONFIG = 'sha256:9f75be0ab4714bfb80349632dbeea5f9eb4e941bcd131d0c9abb8f5ba228a3ee'
VERSION = '2.6.2'
REVISION = 'dd9c19c37a878cf4a81b18efb8d6f0599c7da923'
OWNER = 'hexalith.g4.run'
BEARER = 'HEXALITH_STORY32_PROTECTION_BEARER'
TENANT = 'story32-fixture'
DOCKER = ['docker', '--host', 'unix:///var/run/docker.sock']


class Stop(Exception):
    """A public stopping stage without remote bodies or credential text."""


def require(condition, stage):
    if not condition:
        raise Stop(stage)


def command(stage, argv, *, timeout=20, env=None):
    outcome = subprocess.run(argv, capture_output=True, timeout=timeout, env=env)
    require(outcome.returncode == 0, stage)
    return outcome.stdout


def write_json(path, value):
    path.write_text(json.dumps(value, separators=(',', ':')) + '\n', encoding='utf-8')
    return hashlib.sha256(path.read_bytes()).hexdigest()


def tls(directory):
    certs = directory / 'tls'
    certs.mkdir(mode=0o700)
    ca, key = directory / 'ca.pem', directory / 'ca-key.pem'
    command('ca', ['openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-days', '1', '-subj', '/CN=Disposable Story32 CA',
                   '-keyout', str(key), '-out', str(ca), '-addext', 'basicConstraints=critical,CA:TRUE',
                   '-addext', 'keyUsage=critical,keyCertSign,cRLSign'])
    server_key, request = certs / 'server.key', directory / 'server.csr'
    command('tls-request', ['openssl', 'req', '-new', '-newkey', 'rsa:2048', '-nodes', '-subj', '/CN=127.0.0.1',
                            '-keyout', str(server_key), '-out', str(request)])
    extensions = directory / 'server.ext'
    extensions.write_text('basicConstraints=critical,CA:FALSE\nkeyUsage=critical,digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\nsubjectAltName=IP:127.0.0.1\n')
    command('tls-certificate', ['openssl', 'x509', '-req', '-in', str(request), '-CA', str(ca), '-CAkey', str(key),
                                '-CAcreateserial', '-out', str(certs / 'server.pem'), '-days', '1', '-extfile', str(extensions)])
    key.unlink()
    return certs, ca


def create_container(directory, run_id, name):
    certs, ca = tls(directory)
    (certs / 'native.hcl').write_text('''ui = false
disable_mlock = true
storage "inmem" {}
listener "tcp" {
  address = "127.0.0.1:8200"
  cluster_address = "127.0.0.1:8201"
  tls_cert_file = "/fixture/server.pem"
  tls_key_file = "/fixture/server.key"
}
''')
    uid, gid = os.getuid(), os.getgid()
    require(uid > 0 and gid > 0, 'nonroot-owner')
    arguments = [*DOCKER, 'create', '--pull=missing', '--platform=linux/amd64', '--name', name, '--label', f'{OWNER}={run_id}',
                 '--network=none', '--read-only', '--log-driver=none', '--cap-drop=ALL',
                 '--security-opt=no-new-privileges', '--user', f'{uid}:{gid}', '--memory=512m', '--memory-swap=512m', '--pids-limit=128',
                 '--env', 'GOMAXPROCS=2', '--mount', f'type=bind,source={certs},target=/fixture,readonly']
    for mount in ('/tmp', '/openbao/file', '/openbao/logs'):
        arguments += ['--tmpfs', f'{mount}:rw,noexec,nosuid,nodev,size=32m,mode=0700,uid={uid},gid={gid}']
    arguments += ['--entrypoint=/bin/sh', IMAGE, '-ec', 'exec bao server -config=/fixture/native.hcl']
    container = command('container-create', arguments, timeout=160).decode('ascii').strip()
    require(re.fullmatch('[0-9a-f]{64}', container), 'container-id')
    inspected = inspect_container(container)
    options = inspected['HostConfig']
    mounts = inspected['Mounts']
    binds = [mount for mount in mounts if mount['Type'] == 'bind']
    require(inspected['Config']['Labels'].get(OWNER) == run_id and inspected['Image'] in (IMAGE_CONFIG, IMAGE.split('@')[1])
            and not options['PortBindings'] and not options['PublishAllPorts']
            and options['NetworkMode'] == 'none' and not options['Privileged']
            and options['ReadonlyRootfs'] and options['LogConfig']['Type'] == 'none' and options['CapDrop'] == ['ALL']
            and 'no-new-privileges' in options['SecurityOpt'] and set(options['Tmpfs']) == {'/tmp', '/openbao/file', '/openbao/logs'}
            and len(binds) == 1 and binds[0]['Destination'] == '/fixture' and binds[0]['Source'] == str(certs) and not binds[0]['RW']
            and all(mount['Type'] in ('bind', 'tmpfs') for mount in mounts), 'container-isolation')
    command('container-start', [*DOCKER, 'start', container])
    network = inspect_container(container)['NetworkSettings']
    require(set(network['Networks']) == {'none'} and not network['Ports']
            and not network['Networks']['none']['IPAddress'], 'container-network-readback')
    wait_for_loopback_listener(container)
    actual = command('native-version', [*DOCKER, 'exec', container, 'bao', 'version']).decode('ascii').strip()
    require(actual.startswith(f'OpenBao v{VERSION} ({REVISION})'), 'native-version')
    return container, ca


def inspect_container(container):
    return json.loads(command('container-inspect', [*DOCKER, 'inspect', '--type', 'container', container]))[0]


def wait_for_loopback_listener(container):
    deadline = time.monotonic() + 30
    while True:
        sockets = command('container-sockets', [*DOCKER, 'exec', container, '/bin/sh', '-ec',
                           'cat /proc/net/tcp; if [ -r /proc/net/tcp6 ]; then cat /proc/net/tcp6; fi']).decode('ascii')
        listeners = [fields[1] for row in sockets.splitlines() if len(fields := row.split()) > 3 and fields[3] == '0A']
        require(set(listeners) <= {'0100007F:2008', '0100007F:2009'}, 'loopback-listener')
        if '0100007F:2008' in listeners:
            return
        require(time.monotonic() < deadline, 'loopback-listener')
        time.sleep(0.1)


@contextmanager
def loopback_tunnel(container):
    # Pass raw TLS bytes through Docker's local Unix socket to the container's isolated loopback.
    # Neither the host proxy nor Docker CLI needs the key, token or HTTP body in its arguments.
    slots = threading.BoundedSemaphore(8)

    class Forward(socketserver.BaseRequestHandler):
        def handle(self):
            if not slots.acquire(blocking=False):
                return
            try:
                subprocess.run([*DOCKER, 'exec', '-i', container, '/bin/busybox', 'nc', '127.0.0.1', '8200'],
                               stdin=self.request, stdout=self.request, stderr=subprocess.DEVNULL, timeout=35)
            except (OSError, subprocess.TimeoutExpired):
                pass
            finally:
                slots.release()

    with socketserver.ThreadingTCPServer(('127.0.0.1', 0), Forward) as server:
        address, port = server.server_address
        require(address == '127.0.0.1' and port > 0, 'host-loopback-tunnel')
        thread = threading.Thread(target=server.serve_forever)
        thread.start()
        try:
            yield f'https://{address}:{port}/'
        finally:
            server.shutdown()
            thread.join()


def cleanup(name, run_id):
    result = subprocess.run([*DOCKER, 'inspect', '--type', 'container', '--format', f'{{{{index .Config.Labels "{OWNER}"}}}}', name],
                            capture_output=True, timeout=10)
    if result.returncode == 0:
        require(result.stdout.decode('ascii').strip() == run_id, 'container-cleanup-owner')
        command('container-cleanup', [*DOCKER, 'rm', '--force', '--volumes', name], timeout=15)


def install(directory, run_id, bootstrap_path, address, ca, credential):
    spec = importlib.util.spec_from_file_location('shipped_bootstrap', bootstrap_path)
    bootstrap = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(bootstrap)
    context = ssl.create_default_context(cafile=str(ca))
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), urllib.request.HTTPSHandler(context=context), bootstrap.NoRedirect())

    def call(method, path, body=None, token=None):
        headers = {'Content-Type': 'application/json', 'X-Vault-Request': 'true'}
        if token is not None:
            headers['X-Vault-Token'] = token
        request = urllib.request.Request(address + 'v1/' + path, method=method, headers=headers,
                                         data=json.dumps(body).encode() if body is not None else None)
        with opener.open(request, timeout=5) as response:
            value = response.read(1_000_001)
        require(len(value) <= 1_000_000, 'native-response-size')
        return json.loads(value) if value else None

    deadline = time.monotonic() + 30
    while True:
        try:
            initial = call('GET', 'sys/init')
            break
        except OSError:
            require(time.monotonic() < deadline, 'native-tls-listener')
            time.sleep(0.2)
    require(initial == {'initialized': False}, 'fresh-native-installation')
    initialized = call('PUT', 'sys/init', {'secret_shares': 1, 'secret_threshold': 1})
    root = initialized['root_token']
    require(len(initialized['keys_base64']) == 1 and bool(root), 'native-init')
    opened = call('PUT', 'sys/unseal', {'key': initialized['keys_base64'][0]})
    require(opened['sealed'] is False, 'native-unseal')
    scope_names = ('protection:candidate-interaction-occurrences-v1|candidate-interaction-occurrences-v1',
                   'protection:deletion-consumption-v23|deletion-consumption-v23',
                   'custody:key-lifecycle-v1|custody-key-lifecycle-candidate-v1')
    scopes = {f'{TENANT}:{scope}': hashlib.sha256(f'fixture:{run_id}:{scope}:empty'.encode()).hexdigest().upper() for scope in scope_names}
    enrollment = {'address': address, 'storeId': 'fixture-' + run_id, 'enrollmentId': 'fixture-enrollment-' + run_id,
                  'transitMount': 'erc-transit', 'headMount': 'erc-heads', 'nativeKeys': {TENANT: 'fixture-root'}, 'genesisAnchors': {TENANT: scopes}}
    path = directory / 'enrollment.json'
    write_json(path, enrollment)
    environment = {'PATH': os.environ['PATH'], 'NO_PROXY': '127.0.0.1', 'ERC_BOOTSTRAP_TOKEN': root}
    sender = command('shipped-bootstrap', [sys.executable, '-I', '-B', str(bootstrap_path), '--apply', '--enrollment', str(path),
                                            '--ca-certificate', str(ca)], timeout=75, env=environment)
    require(json.loads(sender) == {'status': 'BootstrappedNotQualified', 'tenants': 1, 'copyPolicyQualified': False}, 'shipped-bootstrap-receipt')
    tenant_key = hashlib.sha256(TENANT.encode()).hexdigest()
    head_path = 'erc-heads/data/tenants/' + tenant_key
    key_path = 'erc-transit/keys/fixture-root'
    policy_path = 'sys/policies/acl/fixture-readiness-observer'
    observer_policy = ''.join(f'path "{source}" {{\n  capabilities = ["read"]\n}}\n' for source in (head_path, key_path))
    call('PUT', policy_path, {'policy': observer_policy}, root)
    require(bootstrap.policy_text(call('GET', policy_path, token=root)) == observer_policy, 'observer-policy-readback')
    policies = ['fixture-readiness-observer']
    token = call('POST', 'auth/token/create', {'id': credential, 'policies': policies, 'no_default_policy': True,
                                              'no_parent': True, 'renewable': False, 'ttl': '15m'}, root)['auth']
    require(secrets.compare_digest(token['client_token'], credential) and sorted(token['policies']) == policies, 'exact-runtime-policy')
    call('POST', 'auth/token/revoke-self', {}, root)
    stored = call('GET', head_path, token=credential)['data']['data']
    require(stored['StoreId'] == enrollment['storeId'] and stored['EnrollmentId'] == enrollment['enrollmentId']
            and set(stored['Anchors']) == set(scopes), 'runtime-owner-head')
    require(call('GET', key_path, token=credential)['data']['name'] == 'fixture-root', 'runtime-key-read')
    for method, path, token in [('GET', policy_path, root), ('POST', head_path, credential), ('POST', key_path + '/rotate', credential),
                                ('POST', 'erc-transit/decrypt/fixture-root', credential),
                                ('GET', 'erc-transit/export/encryption-key/fixture-root', credential)]:
        try:
            call(method, path, {} if method == 'POST' else None, token)
        except urllib.error.HTTPError as refusal:
            require(refusal.code == 403, 'observer-mutation-or-root-reuse-refusal')
        else:
            raise Stop('observer-mutation-or-root-reuse-refusal')
    return enrollment, policies


def write_installation(directory, run_id, enrollment, policies, ca):
    authorities = {}
    for role in ('Private', 'Source', 'Guard', 'Registrar', 'Evidence'):
        key = directory / (role.lower() + '.pem')
        command('authority-anchor', ['openssl', 'genpkey', '-algorithm', 'EC', '-pkeyopt', 'ec_paramgen_curve:prime256v1', '-out', str(key)])
        public = command('authority-public-anchor', ['openssl', 'pkey', '-in', str(key), '-pubout']).decode('ascii')
        key.unlink()
        bearer = directory / (role.lower() + '.bearer')
        bearer.write_text(secrets.token_hex(32), encoding='ascii')
        authorities[role + 'Authority'] = {'AppId': f'fixture-{run_id[:16]}-{role.lower()}', 'CredentialReference': 'file:' + str(bearer),
                                          'PublicKeyPem': public, 'EnrollmentId': enrollment['enrollmentId'], 'Scope': TENANT,
                                          'CurrentTrustRevision': '1'}
    # This fixture exercises ordinary startup and readiness. Private role operations have no authorized receiver or signing key.
    anchor = authorities['PrivateAuthority']['PublicKeyPem'].splitlines()[1:-1]
    base64.b64decode(''.join(anchor), validate=True)
    trust = {'TenantId': TENANT, 'KeyFamily': 'DeletionBatchCapabilitySigningKey', 'CapabilityKeyVersion': 'fixture-1',
             'Issuer': 'fixture-' + run_id, 'Audience': 'fixture-readiness', 'PublicAnchorId': 'fixture-anchor',
             'PublicAnchorVersion': '1', 'SubjectPublicKeyInfo': ''.join(anchor), 'TrustProfileRevision': 1, 'IsRevoked': True}
    policy = directory / 'copy-policy.json'
    policy_digest = write_json(policy, {'purpose': 'disposable-fixture-readiness', 'runId': run_id, 'nativeStorage': 'inmem',
                                       'persistentVolume': False, 'fullCopyQualified': False, 'nativeRuntimePolicies': policies})
    inventory = directory / 'inventory.json'
    inventory_digest = write_json(inventory, {'runtime': {'currentInventoryAuthorityReference': 'fixture-' + run_id + '-inventory'}})
    native = {name[0].upper() + name[1:]: enrollment[name] for name in ('storeId', 'enrollmentId', 'transitMount', 'headMount', 'nativeKeys')}
    native['TrustedCaCertificatePem'] = ca.read_text(encoding='ascii')
    return write_json(directory / 'installation.json', {'Native': native, 'Endpoint': enrollment['address'], 'CredentialReference': 'env:' + BEARER,
                     'RegistryEpochs': {TENANT: 'fixture-1'}, **authorities, 'CapabilityTrust': [trust],
                     'InventoryConfigurationFile': str(inventory), 'InventoryConfigurationSha256': inventory_digest,
                     'CopyPolicyFile': str(policy), 'CopyPolicySha256': policy_digest})


def interrupted(_signal, _frame):
    raise KeyboardInterrupt


def main():
    bootstrap, run_id = Path(sys.argv[1]), sys.argv[2]
    require(re.fullmatch('[0-9a-f]{32}', run_id) and os.environ.get('HEXALITH_G4_RUN_ID') == run_id, 'run-owner')
    workspace = Path.cwd()
    require(workspace.name == run_id and not workspace.is_symlink(), 'run-workspace')
    credential = os.environ.pop(BEARER, '')
    require(re.fullmatch('[0-9a-f]{64}', credential), 'credential-input')
    for name in ('DOCKER_HOST', 'DOCKER_CONTEXT', 'DOCKER_TLS_VERIFY', 'DOCKER_CERT_PATH'):
        os.environ.pop(name, None)
    os.umask(0o077)
    directory = workspace / 'fixture-protection'
    directory.mkdir(mode=0o700)
    name = f'hexalith-g4-{run_id}-fixture-erasure-root'
    signal.signal(signal.SIGTERM, interrupted)
    signal.signal(signal.SIGINT, interrupted)
    try:
        container, ca = create_container(directory, run_id, name)
        with loopback_tunnel(container) as address:
            enrollment, policies = install(directory, run_id, bootstrap, address, ca, credential)
            digest = write_installation(directory, run_id, enrollment, policies, ca)
            pending = directory / 'installed.pending'
            write_json(pending, {'runId': run_id, 'installationSha256': digest, 'nativeVersion': VERSION, 'nativeSource': REVISION,
                                 'image': IMAGE, 'containerId': container, 'runtimePolicies': policies,
                                 'runtimeAccess': 'read-only', 'rootRevoked': True, 'healthy': True,
                                 'networkMode': 'none', 'listenerAddress': '127.0.0.1', 'publishedPorts': 0})
            pending.replace(directory / 'installed.json')
            print('fixture-erasure-root: run-owned TLS installation ready', flush=True)
            while True:
                time.sleep(2)
                running = command('native-runtime-status', [*DOCKER, 'inspect', '--format', '{{.State.Running}}', container])
                require(running.strip() == b'true', 'native-runtime-exited')
    except KeyboardInterrupt:
        return
    finally:
        (directory / 'installed.json').unlink(missing_ok=True)
        cleanup(name, run_id)


if __name__ == '__main__':
    try:
        main()
    except (Stop, OSError, ValueError, KeyError, subprocess.SubprocessError) as failure:
        print('fixture-erasure-root: ' + (str(failure) if isinstance(failure, Stop) else type(failure).__name__), file=sys.stderr)
        raise SystemExit(1) from None
