"""Synthetic offline fixtures shared by the three execution-boundary suites."""
import base64
from dataclasses import replace
from datetime import datetime, timezone
import hashlib
import os
from pathlib import Path
import struct
import sys
import time
import types

ROOT = Path(__file__).absolute().parent

def load(name, filename):
    if name in sys.modules:
        return sys.modules[name]
    raw = (ROOT / filename).read_bytes()
    module = types.ModuleType(name)
    module.__file__ = str(ROOT / filename)
    module.__source_sha256__ = hashlib.sha256(raw).hexdigest()
    sys.modules[name] = module
    exec(compile(raw, module.__file__, 'exec'), module.__dict__)
    return module

b = load('hexalith_recovery_fixed_boundary', 'recovery_execution_boundary.py')
a = b.a
immutable = load('hexalith_recovery_immutable_fixture', 'immutable_readback_driver.py')
policy_driver = load('hexalith_recovery_policy_fixture', 'policy_authentication_driver.py')
old = load('hexalith_recovery_old_fixtures', 'test_recovery_adapters.py')
NOW = datetime(2026, 10, 23, 12, 0, 0, tzinfo=timezone.utc)
KEY = struct.pack('>I', 11) + b'ssh-ed25519' + struct.pack('>I', 32) + bytes.fromhex(
    'd75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a')
CA_SOURCE = b'-----BEGIN CERTIFICATE-----\nMIIFazCCA1OgAwIBAgIRAIIQz7DSQONZRGPgu2OCiwAwDQYJKoZIhvcNAQELBQAw\nTzELMAkGA1UEBhMCVVMxKTAnBgNVBAoTIEludGVybmV0IFNlY3VyaXR5IFJlc2Vh\ncmNoIEdyb3VwMRUwEwYDVQQDEwxJU1JHIFJvb3QgWDEwHhcNMTUwNjA0MTEwNDM4\nWhcNMzUwNjA0MTEwNDM4WjBPMQswCQYDVQQGEwJVUzEpMCcGA1UEChMgSW50ZXJu\nZXQgU2VjdXJpdHkgUmVzZWFyY2ggR3JvdXAxFTATBgNVBAMTDElTUkcgUm9vdCBY\nMTCCAiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAK3oJHP0FDfzm54rVygc\nh77ct984kIxuPOZXoHj3dcKi/vVqbvYATyjb3miGbESTtrFj/RQSa78f0uoxmyF+\n0TM8ukj13Xnfs7j/EvEhmkvBioZxaUpmZmyPfjxwv60pIgbz5MDmgK7iS4+3mX6U\nA5/TR5d8mUgjU+g4rk8Kb4Mu0UlXjIB0ttov0DiNewNwIRt18jA8+o+u3dpjq+sW\nT8KOEUt+zwvo/7V3LvSye0rgTBIlDHCNAymg4VMk7BPZ7hm/ELNKjD+Jo2FR3qyH\nB5T0Y3HsLuJvW5iB4YlcNHlsdu87kGJ55tukmi8mxdAQ4Q7e2RCOFvu396j3x+UC\nB5iPNgiV5+I3lg02dZ77DnKxHZu8A/lJBdiB3QW0KtZB6awBdpUKD9jf1b0SHzUv\nKBds0pjBqAlkd25HN7rOrFleaJ1/ctaJxQZBKT5ZPt0m9STJEadao0xAH0ahmbWn\nOlFuhjuefXKnEgV4We0+UXgVCwOPjdAvBbI+e0ocS3MFEvzG6uBQE3xDk3SzynTn\njh8BCNAw1FtxNrQHusEwMFxIt4I7mKZ9YIqioymCzLq9gwQbooMDQaHWBfEbwrbw\nqHyGO0aoSCqI3Haadr8faqU9GY/rOPNk3sgrDQoo//fb4hVC1CLQJ13hef4Y53CI\nrU7m2Ys6xt0nUW7/vGT1M0NPAgMBAAGjQjBAMA4GA1UdDwEB/wQEAwIBBjAPBgNV\nHRMBAf8EBTADAQH/MB0GA1UdDgQWBBR5tFnme7bl5AFzgAiIyBpY9umbbjANBgkq\nhkiG9w0BAQsFAAOCAgEAVR9YqbyyqFDQDLHYGmkgJykIrGF1XIpu+ILlaS/V9lZL\nubhzEFnTIZd+50xx+7LSYK05qAvqFyFWhfFQDlnrzuBZ6brJFe+GnY+EgPbk6ZGQ\n3BebYhtF8GaV0nxvwuo77x/Py9auJ/GpsMiu/X1+mvoiBOv/2X/qkSsisRcOj/KK\nNFtY2PwByVS5uCbMiogziUwthDyC3+6WVwW6LLv3xLfHTjuCvjHIInNzktHCgKQ5\nORAzI4JMPJ+GslWYHb4phowim57iaztXOoJwTdwJx4nLCgdNbOhdjsnvzqvHu7Ur\nTkXWStAmzOVyyghqpZXjFaH3pO3JLF+l+/+sKAIuvtd7u+Nxe5AW0wdeRlN8NwdC\njNPElpzVmbUq4JUagEiuTDkHzsxHpFKVK7q4+63SM1N95R1NbdWhscdCb+ZAJzVc\noyi3B43njTOQ5yOf+1CceWxG1bQVs5ZufpsMljq4Ui0/1lvh+wjChP4kqKOJ2qxq\n4RgqsahDYVvTH9w7jXbyLeiNdd8XM2w9U/t7y0Ff/9yi0GE44Za4rF2LN9d11TPA\nmRGunUHBcnWEvgJBQl9nJEiU0Zsnvgc/ubhPgXRR4Xq37Z0j4r7g1SgEEzwxA57d\nemyPxgcYxn/eR44/KJ4EBs+lVDR3veyJm+kXQ99b21/+jh5Xos1AnX5iItreGCc=\n-----END CERTIFICATE-----\n'
CA_TARGET = b'-----BEGIN CERTIFICATE-----\nMIICGzCCAaGgAwIBAgIQQdKd0XLq7qeAwSxs6S+HUjAKBggqhkjOPQQDAzBPMQsw\nCQYDVQQGEwJVUzEpMCcGA1UEChMgSW50ZXJuZXQgU2VjdXJpdHkgUmVzZWFyY2gg\nR3JvdXAxFTATBgNVBAMTDElTUkcgUm9vdCBYMjAeFw0yMDA5MDQwMDAwMDBaFw00\nMDA5MTcxNjAwMDBaME8xCzAJBgNVBAYTAlVTMSkwJwYDVQQKEyBJbnRlcm5ldCBT\nZWN1cml0eSBSZXNlYXJjaCBHcm91cDEVMBMGA1UEAxMMSVNSRyBSb290IFgyMHYw\nEAYHKoZIzj0CAQYFK4EEACIDYgAEzZvVn4CDCuwJSvMWSj5cz3es3mcFDR0HttwW\n+1qLFNvicWDEukWVEYmO6gbf9yoWHKS5xcUy4APgHoIYOIvXRdgKam7mAHf7AlF9\nItgKbppbd9/w+kHsOdx1ymgHDB/qo0IwQDAOBgNVHQ8BAf8EBAMCAQYwDwYDVR0T\nAQH/BAUwAwEB/zAdBgNVHQ4EFgQUfEKWrt5LSDv6kviejM9ti6lyN5UwCgYIKoZI\nzj0EAwMDaAAwZQIwe3lORlCEwkSHRhtFcP9Ymd70/aTSVaYgLXTWNLxBo1BfASdW\ntL4ndQavEi51mI38AjEAi/V3bNTIZargCyzuFJ0nN6T5U6VR5CmD1/iQMVtCnwr1\n/q4AaOeMSQ+2b1tbFfLn\n-----END CERTIFICATE-----\n'
SIGNER = 'fixture-signer'
TRUST = SIGNER.encode() + b' namespaces="hexalith-recovery" ssh-ed25519 ' + base64.b64encode(KEY) + b'\n'


def measure(path):
    path = Path(path).absolute()
    raw = b.read_custody(path)
    return a.Tool(str(path), a.digest(raw), len(raw))


def credential(principal, audience, secret=None):
    secret = ('synthetic-only-' + principal + '-' + audience).encode('ascii') if secret is None else secret
    identity = a.canonical({'principal': principal, 'audience': audience})
    custody = a.canonical({'principal': principal, 'audience': audience,
        'identitySha256': a.digest(identity), 'secretSha256': a.digest(secret)})
    binding = b.CredentialBinding(principal, audience, a.digest(identity), a.digest(custody), a.digest(secret))
    return b.Credential(binding, identity, custody, secret)


def certificate_secret(label='SOURCE'):
    return a.canonical({'certificatePem': globals()['CERT_' + label + '_CLIENT'].decode('ascii'),
        'privateKeyPem': globals()['CERT_' + label + '_CLIENT_KEY'].decode('ascii')})


def fixture(directory, operation='capture', distinct=True, native_cas=None):
    value, _, _, _ = old.fixture(operation, distinct_clusters=distinct)
    tool = directory / 'fixture-process'
    expected_credentials = [credential(value['operatorPrincipal'], cluster['uid']).descriptor(
        credential(value['operatorPrincipal'], cluster['uid']).binding) for cluster in
        {v['uid']: v for v in (value['sourceCluster'], value['targetCluster'])}.values()]
    script = '''#!/usr/bin/python3.14 -ISB
import base64, fcntl, hashlib, json, os, sys
fd = 3
flags = fcntl.F_SEAL_WRITE | fcntl.F_SEAL_GROW | fcntl.F_SEAL_SHRINK | fcntl.F_SEAL_SEAL
assert fcntl.fcntl(fd, fcntl.F_GET_SEALS) & flags == flags
assert dict(os.environ) == {'LANG': 'C.UTF-8'}
assert os.getcwd() == '/'
descriptor = json.loads(os.read(fd, 1048576))
raw = sys.stdin.buffer.read()
body = json.loads(raw)
name = descriptor['name']
assert sys.argv[1:] == ['--recovery-protocol-v1', '--request-stdin', '--credential-fd=3']
assert name == 'capture'
assert set(descriptor) == {'requestSha256', 'name', 'path', 'method', 'credentials'}
assert descriptor['path'] == 'capture' and descriptor['method'] == 'PROTOCOL'
assert descriptor['credentials'] == EXPECTED_CREDENTIALS
assert [r['audience'] for r in descriptor['credentials']] == list(dict.fromkeys(
    (body['sourceCluster']['uid'], body['targetCluster']['uid'])))
canonical = lambda value: json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=True).encode('ascii')
sha = lambda raw: hashlib.sha256(raw).hexdigest()
for record in descriptor['credentials']:
    principal, audience = record['principal'], record['audience']
    secret = ('synthetic-only-' + principal + '-' + audience).encode('ascii')
    assert base64.b64decode(record['secretBase64'], validate=True) == secret
    identity = canonical({'principal': principal, 'audience': audience})
    custody = canonical({'principal': principal, 'audience': audience,
        'identitySha256': sha(identity), 'secretSha256': sha(secret)})
    assert record['identitySha256'] == sha(identity) and record['custodySha256'] == sha(custody)
script = open(__file__, 'rb').read()
tool = {'path': TOOL_PATH, 'sha256': sha(script), 'size': len(script)}
request = {'name': 'capture', 'role': 'stageRunner', 'method': 'PROTOCOL', 'endpoint': '', 'path': 'capture',
    'stdin': base64.b64encode(raw).decode('ascii'), 'stdinEncoding': 'base64', 'expected': {'check': 'capture'},
    'effect': 'bounded-source-capture', 'principal': EXPECTED_PRINCIPAL, 'tool': tool,
    'argv': [TOOL_PATH, '--recovery-protocol-v1', '--request-stdin', '--credential-fd=3'],
    'timeout_seconds': 15, 'maximum_response_bytes': 2097152, 'maximum_error_bytes': 65536,
    'content_type': 'application/json', 'environment': [['LANG', 'C.UTF-8']], 'credential_fd': 3,
    'stdin_materialization': None}
assert descriptor['requestSha256'] == sha(canonical(request))
result = {'outcome': 'captured', 'scopeSha256': body['scopeSha256'],
    'sourceUids': [r['uid'] for r in body['protectedSources']],
    'targetUid': body['targetNamespace']['uid'], 'contentSha256': 'a' * 64}
sys.stdout.write(json.dumps(result))
'''
    script = script.replace('EXPECTED_CREDENTIALS', repr(expected_credentials)).replace('TOOL_PATH', repr(str(tool)))
    script = script.replace('EXPECTED_PRINCIPAL', repr(value['operatorPrincipal']))
    tool.write_bytes(script.encode('ascii'))
    tool.chmod(0o700)
    measured = measure(tool)
    verifier = measure('/usr/bin/ssh-keygen')
    value['tools'] = {role: {'path': measured.path, 'sha256': measured.sha256, 'size': measured.size}
        for role in ('kubectl', 'artifactReader', 'stageRunner')}
    value['tools']['signatureVerifier'] = {'path': verifier.path, 'sha256': verifier.sha256, 'size': verifier.size}
    value['signerSha256'] = a.digest(KEY)
    source_ca, target_ca = native_cas or (CA_SOURCE, CA_TARGET if distinct else CA_SOURCE)
    value['sourceCluster']['caSha256'] = a.digest(source_ca)
    value['targetCluster']['caSha256'] = a.digest(target_ca)
    initial = a.parse_binding(value)
    policy = a.canonical({'kind': 'hexalith.recovery-adapter-policy', 'schemaVersion': 1,
        'scopeSha256': initial.scope_sha256(), 'validFrom': initial.valid_from, 'expiresAt': initial.expires_at,
        'retainedDecisionsSha256': initial.decisions_sha256})
    signature = b'inert fixture signature; not cryptographic authentication'
    value['policy'].update(sha256=a.digest(policy), size=len(policy))
    value['signature'].update(sha256=a.digest(signature), size=len(signature))
    binding = a.parse_binding(value)
    dependencies = tuple(measure(ROOT / name) for name in ('recovery_execution_boundary.py',
        'recovery_adapters.py', 'immutable_readback_driver.py', 'policy_authentication_driver.py')) + (
        measured, verifier, measure('/usr/bin/python3.14'))
    return binding, policy, signature, dependencies


class Handle:
    def __init__(self, metadata, raw, chunk=7):
        self.metadata, self.raw, self.chunk = metadata, raw, chunk
        self.closed = False

    def read(self, maximum, deadline):
        b.remaining(deadline)
        result, self.raw = self.raw[:min(maximum, self.chunk)], self.raw[min(maximum, self.chunk):]
        return result

    def close(self):
        self.closed = True


class Provider(immutable.ImmutableProvider):
    def __init__(self, binding, provider_binding, policy, signature):
        self.binding, self.provider_binding = binding, provider_binding
        self.policy, self.signature = policy, signature
        self.calls, self.handles = [], []
        self.mutate = None

    def open_exact(self, artifact, credential, deadline):
        self.calls.append((artifact, credential))
        p = self.provider_binding
        metadata = {'store': artifact.store, 'key': artifact.key, 'version': artifact.version,
            'length': artifact.size, 'sha256': artifact.sha256, 'serverSideEncryption': 'AES256',
            'objectLockMode': 'COMPLIANCE', 'retainUntil': artifact.retain_until,
            'principal': p.reader.principal, 'credentialIdentitySha256': p.reader.identity_sha256,
            'credentialCustodySha256': p.reader.custody_sha256,
            'providerEndpoint': p.endpoint, 'providerCaSha256': p.ca_sha256}
        raw = self.policy if artifact == self.binding.policy else (
            self.signature if artifact == self.binding.signature else b'recovery')
        handle = Handle(a.canonical(metadata), raw)
        if self.mutate:
            self.mutate(handle, metadata)
        self.handles.append(handle)
        return handle


class Verifier:
    """Explicitly inert verifier protocol; it never claims cryptographic coverage."""
    def __init__(self, binding, policy, signature):
        self.binding, self.policy, self.signature = binding, policy, signature
        self.calls = []
        self.code = 0

    def run(self, tool, arguments, stdin, credentials, deadline, maximum, error_maximum, files=()):
        self.calls.append((tool, arguments, stdin, files))
        b.remaining(deadline)
        assert stdin == self.policy
        assert dict(files) == {'@trust': TRUST, '@signature': self.signature}
        assert arguments == ('-Y', 'verify', '-f', '@trust', '-I', SIGNER,
            '-n', 'hexalith-recovery', '-s', '@signature')
        return b.ProcessResult(self.code, b'fixture verifier result')


class StageProtocol:
    """Inert system protocol for matrix composition; no actual store operation."""
    def __init__(self, binding, policy, signature, credentials=None):
        self.binding = binding
        self.backend = old.FakeBackend(binding, policy, signature)
        self.credentials = credentials

    def run(self, tool, arguments, stdin, credentials, deadline, maximum, error_maximum, files=()):
        envelope = a.strict_json(credentials)
        request = next(r for r in a.build_requests(self.binding) if r.name == envelope['name'])
        assert stdin == request.stdin and tool == request.tool and arguments == request.argv[1:]
        expected_credentials = [next(c for c in self.credentials if c.binding.principal == request.principal and
            c.binding.audience == cluster.uid) if self.credentials is not None else credential(request.principal, cluster.uid)
            for cluster in dict.fromkeys((self.binding.source_cluster, self.binding.target_cluster))]
        assert envelope == {'requestSha256': request.sha256(), 'name': request.name, 'path': request.path,
            'method': request.method, 'credentials': [c.descriptor(c.binding) for c in expected_credentials]}
        result = self.backend.exchange(request, deadline)
        return b.ProcessResult(result.exit_code, result.body)


class Stream:
    def __init__(self, raw, send_maximum=13, chunk=5, delay=0):
        self.raw, self.send_maximum, self.chunk, self.delay = raw, send_maximum, chunk, delay
        self.closed, self.sent, self.timeouts = False, bytearray(), []

    def settimeout(self, value):
        self.timeouts.append(value)

    def send(self, raw):
        if self.delay:
            time.sleep(self.delay)
        count = min(len(raw), self.send_maximum)
        self.sent.extend(raw[:count])
        return count

    def recv(self, maximum):
        if self.delay:
            time.sleep(self.delay)
        result, self.raw = self.raw[:min(maximum, self.chunk)], self.raw[min(maximum, self.chunk):]
        return result

    def close(self):
        self.closed = True


def http(body, status=200, extra=b''):
    return ('HTTP/1.1 ' + str(status) + ' Fixture\r\nContent-Type: application/json\r\nContent-Length: ' +
        str(len(body)) + '\r\n').encode() + extra + b'\r\n' + body


class Native:
    def __init__(self, binding, policy, signature):
        self.backend = old.FakeBackend(binding, policy, signature)
        self.calls = []
        self.streams = []
        self.contexts = []
        self.mutate = None

    def exchange(self, request, deadline, native_binding, credential):
        self.calls.append(request)
        credential.descriptor(native_binding.credential)
        response = self.backend.exchange(request, deadline)
        if self.mutate:
            response = self.mutate(response)
        stream = Stream(http(response.body, response.status))
        self.streams.append(stream)
        def connector(context, cluster, limit):
            assert cluster == native_binding.cluster and limit == deadline
            self.contexts.append(context)
            return stream
        return b.NativeTLS(connector).exchange(request, deadline, native_binding, credential)


def compose(directory, operation='capture', process=None, *, native_profiles=None, native_secrets=None, native_cas=None):
    binding, policy, signature, dependencies = fixture(directory, operation, native_cas=native_cas)
    reader = credential(binding.validator, binding.policy.store)
    writer = credential(binding.operator, binding.policy.store)
    provider_binding = immutable.ProviderBinding(binding.policy.store, 'https://provider.invalid', 'c' * 64,
        reader.binding, writer.binding.principal, writer.binding.identity_sha256, writer.binding.custody_sha256, writer.binding.secret_sha256)
    provider = Provider(binding, provider_binding, policy, signature)
    readback = immutable.ImmutableReadbackDriver(binding, provider_binding, provider, now=lambda: NOW)
    verifier = Verifier(binding, policy, signature)
    auth = policy_driver.PolicyAuthenticationDriver(binding, TRUST, a.digest(TRUST), SIGNER, dependencies,
        process=verifier, now=lambda: NOW)
    native = Native(binding, policy, signature)
    clusters = tuple(dict.fromkeys((binding.source_cluster, binding.target_cluster)))
    native_profiles = native_profiles or ('bearer',) * len(clusters)
    native_secrets = native_secrets or (None,) * len(clusters)
    native_cas = native_cas or (CA_SOURCE, CA_TARGET)
    credentials = (reader,) + tuple(credential(binding.operator, c.uid, secret) for c, secret in zip(clusters, native_secrets))
    natives = tuple(b.NativeBinding(c, ca, next(v.binding for v in credentials if v.binding.audience == c.uid), profile)
        for c, ca, profile in zip(clusters, native_cas, native_profiles))
    boundary = b.RecoveryExecutionBoundary(binding, dependencies, credentials,
        tuple(v.binding for v in credentials), immutable_driver=readback, policy_driver=auth,
        native=native, native_bindings=natives, process=process, now=lambda: NOW)
    return boundary, provider, verifier, native


# Static public synthetic TLS material for socket-free fixture handshakes only.
# Created offline; these keys and certificates confer no operational authority.
CERT_SOURCE_CA = b'-----BEGIN CERTIFICATE-----\nMIIB5DCCAYqgAwIBAgIUdl8F4XaPCtDnpFjU9KMJK+efmbkwCgYIKoZIzj0EAwIw\nPTE7MDkGA1UEAwwyUFVCTElDIFNZTlRIRVRJQyBIRVhBTElUSCBTT1VSQ0UgQ0Eg\nLSBOTyBBVVRIT1JJVFkwIBcNMjYxMDIzMTk0MDAxWhgPMjEyNjA5MjkxOTQwMDFa\nMD0xOzA5BgNVBAMMMlBVQkxJQyBTWU5USEVUSUMgSEVYQUxJVEggU09VUkNFIENB\nIC0gTk8gQVVUSE9SSVRZMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEQNxmTBhn\nTrw+KlQp0PMYaa3GlvO7z8f2CcimZp9ySPu9znc+xaZI/8Su2j7l13kmm/trBRTm\nkSdhhMbeCoGl7aNmMGQwHQYDVR0OBBYEFFg5I1M4MFX7tzcKS4zi6fXuvetXMB8G\nA1UdIwQYMBaAFFg5I1M4MFX7tzcKS4zi6fXuvetXMBIGA1UdEwEB/wQIMAYBAf8C\nAQAwDgYDVR0PAQH/BAQDAgEGMAoGCCqGSM49BAMCA0gAMEUCIEeejJNp6kDVElua\nbq1HHttsve1zEOqGRApTSuF3oM/4AiEAk7o08u3xkopdQcdI2zMR69VgSTc943XN\n/lrkiMIvQdQ=\n-----END CERTIFICATE-----\n'
CERT_SOURCE_SERVER = b'-----BEGIN CERTIFICATE-----\nMIIB7jCCAZWgAwIBAgICA+kwCgYIKoZIzj0EAwIwPTE7MDkGA1UEAwwyUFVCTElD\nIFNZTlRIRVRJQyBIRVhBTElUSCBTT1VSQ0UgQ0EgLSBOTyBBVVRIT1JJVFkwIBcN\nMjYxMDIzMTk0MDAxWhgPMjEyNjA5MjkxOTQwMDFaMDgxNjA0BgNVBAMMLVBVQkxJ\nQyBTWU5USEVUSUMgU09VUkNFIFNFUlZFUiAtIE5PIEFVVEhPUklUWTBZMBMGByqG\nSM49AgEGCCqGSM49AwEHA0IABEvj+544D34Lwq7HXZ/jadMzx0BhDuFw+f/WLN38\nNINDHrPHqxw9H9wndP60i5716TCJcnBNSqLmDAgMG35RUAujgYcwgYQwDAYDVR0T\nAQH/BAIwADAOBgNVHQ8BAf8EBAMCB4AwEwYDVR0lBAwwCgYIKwYBBQUHAwEwHQYD\nVR0OBBYEFCGs1VDVxP/mifsM+acRuU5Pz6xYMB8GA1UdIwQYMBaAFFg5I1M4MFX7\ntzcKS4zi6fXuvetXMA8GA1UdEQQIMAaHBMAAAgowCgYIKoZIzj0EAwIDRwAwRAIg\nIcu2rJR9LHeQ+D+OH4poY83ETHz7iW/QyRnSCYmdpIMCIH+Ug5D/bpsiierzfagC\nMKxJcr1WNCy+mv0j14WkCm0x\n-----END CERTIFICATE-----\n'
CERT_SOURCE_SERVER_KEY = b'-----BEGIN PRIVATE KEY-----\nMIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgkiVoz8nW3RibEKpn\npPtXanIRTTOc3eSqRwBUul6UrTihRANCAARL4/ueOA9+C8Kux12f42nTM8dAYQ7h\ncPn/1izd/DSDQx6zx6scPR/cJ3T+tIue9ekwiXJwTUqi5gwIDBt+UVAL\n-----END PRIVATE KEY-----\n'
CERT_SOURCE_CLIENT = b'-----BEGIN CERTIFICATE-----\nMIIB3DCCAYKgAwIBAgICA+swCgYIKoZIzj0EAwIwPTE7MDkGA1UEAwwyUFVCTElD\nIFNZTlRIRVRJQyBIRVhBTElUSCBTT1VSQ0UgQ0EgLSBOTyBBVVRIT1JJVFkwIBcN\nMjYxMDIzMTk0MDAxWhgPMjEyNjA5MjkxOTQwMDFaMDgxNjA0BgNVBAMMLVBVQkxJ\nQyBTWU5USEVUSUMgU09VUkNFIENMSUVOVCAtIE5PIEFVVEhPUklUWTBZMBMGByqG\nSM49AgEGCCqGSM49AwEHA0IABNKsheUrJG3SrhmGAq2EmaNDdXjECkrV9kO8W9xM\nEzoLERejWw/wp7Fb6UaI9tXVxxjvevX4xLWFFTVPPNyDF8WjdTBzMAwGA1UdEwEB\n/wQCMAAwDgYDVR0PAQH/BAQDAgeAMBMGA1UdJQQMMAoGCCsGAQUFBwMCMB0GA1Ud\nDgQWBBQhe1JOcnOQ+JfZdRgrfEap0OZk5TAfBgNVHSMEGDAWgBRYOSNTODBV+7c3\nCkuM4un17r3rVzAKBggqhkjOPQQDAgNIADBFAiAcvZ2ffTGk4lmuq2aAbKJINbOF\np0LbVY1pj8ewOIf4nAIhAMcueEjsVz3IZWppZ0wsL9ghh3Oe96Vq2Y81GGzsfgN7\n-----END CERTIFICATE-----\n'
CERT_SOURCE_CLIENT_KEY = b'-----BEGIN PRIVATE KEY-----\nMIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgDluEd7foBUYgKgeC\ns0YjpG6Do06lfmdmKFnpWVxzKPGhRANCAATSrIXlKyRt0q4ZhgKthJmjQ3V4xApK\n1fZDvFvcTBM6CxEXo1sP8KexW+lGiPbV1ccY73r1+MS1hRU1TzzcgxfF\n-----END PRIVATE KEY-----\n'
CERT_TARGET_CA = b'-----BEGIN CERTIFICATE-----\nMIIB5DCCAYqgAwIBAgIUGfhmfESwW202yn4E4HGINacUudEwCgYIKoZIzj0EAwIw\nPTE7MDkGA1UEAwwyUFVCTElDIFNZTlRIRVRJQyBIRVhBTElUSCBUQVJHRVQgQ0Eg\nLSBOTyBBVVRIT1JJVFkwIBcNMjYxMDIzMTk0MDAxWhgPMjEyNjA5MjkxOTQwMDFa\nMD0xOzA5BgNVBAMMMlBVQkxJQyBTWU5USEVUSUMgSEVYQUxJVEggVEFSR0VUIENB\nIC0gTk8gQVVUSE9SSVRZMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEAfUNIXn5\n0lr/Jh7hgTO40yOG/UBDIAu5WgLRtVUrcH45RBssMmDiIIYNPqowPatVKqkFQsgm\nmUmBBJYk36VcPqNmMGQwHQYDVR0OBBYEFDxhV+CHz4bw6MxnulEtldvYOK3RMB8G\nA1UdIwQYMBaAFDxhV+CHz4bw6MxnulEtldvYOK3RMBIGA1UdEwEB/wQIMAYBAf8C\nAQAwDgYDVR0PAQH/BAQDAgEGMAoGCCqGSM49BAMCA0gAMEUCIBKSCl7WOyD6S88T\nLAlxyhlZf3Ff/n5VpFeyi8xbdMuhAiEAjfF2WDvjwz936tYxp2LoIXDpWgriU8BC\n4eyN3t0hVjk=\n-----END CERTIFICATE-----\n'
CERT_TARGET_SERVER = b'-----BEGIN CERTIFICATE-----\nMIIB8DCCAZWgAwIBAgICA+4wCgYIKoZIzj0EAwIwPTE7MDkGA1UEAwwyUFVCTElD\nIFNZTlRIRVRJQyBIRVhBTElUSCBUQVJHRVQgQ0EgLSBOTyBBVVRIT1JJVFkwIBcN\nMjYxMDIzMTk0MDAxWhgPMjEyNjA5MjkxOTQwMDFaMDgxNjA0BgNVBAMMLVBVQkxJ\nQyBTWU5USEVUSUMgVEFSR0VUIFNFUlZFUiAtIE5PIEFVVEhPUklUWTBZMBMGByqG\nSM49AgEGCCqGSM49AwEHA0IABIhEBP8NJ7X4J2hDp7Pe3NxjEm6rv4CnKlD9ATGV\nY66rZTHTTwcFjRydUgf8vCc9OqQcwc/Yd0bGvBTxInwd7S2jgYcwgYQwDAYDVR0T\nAQH/BAIwADAOBgNVHQ8BAf8EBAMCB4AwEwYDVR0lBAwwCgYIKwYBBQUHAwEwHQYD\nVR0OBBYEFJM1ro4nCXkAGeu+nKzol5OR/G5FMB8GA1UdIwQYMBaAFDxhV+CHz4bw\n6MxnulEtldvYOK3RMA8GA1UdEQQIMAaHBMAAAhQwCgYIKoZIzj0EAwIDSQAwRgIh\nAJOx+Ml/i49WnCKcIKV1v2Uo0Jg4i7ZLkSbIJ1UELTEQAiEA0b8J/LA4WAECFO1n\nR3dzqkOl+Hn107wrCm4SNX7HK+U=\n-----END CERTIFICATE-----\n'
CERT_TARGET_SERVER_KEY = b'-----BEGIN PRIVATE KEY-----\nMIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgmBIB9s7w83SdWw8b\nsjGvQduzIr/doEAn2gdeA+HNQ5ChRANCAASIRAT/DSe1+CdoQ6ez3tzcYxJuq7+A\npypQ/QExlWOuq2Ux008HBY0cnVIH/LwnPTqkHMHP2HdGxrwU8SJ8He0t\n-----END PRIVATE KEY-----\n'
CERT_TARGET_CLIENT = b'-----BEGIN CERTIFICATE-----\nMIIB3TCCAYKgAwIBAgICA/AwCgYIKoZIzj0EAwIwPTE7MDkGA1UEAwwyUFVCTElD\nIFNZTlRIRVRJQyBIRVhBTElUSCBUQVJHRVQgQ0EgLSBOTyBBVVRIT1JJVFkwIBcN\nMjYxMDIzMTk0MDAxWhgPMjEyNjA5MjkxOTQwMDFaMDgxNjA0BgNVBAMMLVBVQkxJ\nQyBTWU5USEVUSUMgVEFSR0VUIENMSUVOVCAtIE5PIEFVVEhPUklUWTBZMBMGByqG\nSM49AgEGCCqGSM49AwEHA0IABE4JnMFh19JDTwNKYmTMMp9F1jcfBa48b2tfDdnE\nR97+NzP1QPRwG6Siqo9gbfYleNTkh5739k0gG/dISbkE9h2jdTBzMAwGA1UdEwEB\n/wQCMAAwDgYDVR0PAQH/BAQDAgeAMBMGA1UdJQQMMAoGCCsGAQUFBwMCMB0GA1Ud\nDgQWBBQWG7xUPM02guZe5XmQ7NepIcxe7TAfBgNVHSMEGDAWgBQ8YVfgh8+G8OjM\nZ7pRLZXb2Dit0TAKBggqhkjOPQQDAgNJADBGAiEA+X1LLa6aj0RKcjrD8iIZtGaQ\n6064M5O0rShLyMik/+8CIQChNJ8vwNAckDqTpsxoqNyNF7rA9vfRFiAOt0PcQIi2\nrQ==\n-----END CERTIFICATE-----\n'
CERT_TARGET_CLIENT_KEY = b'-----BEGIN PRIVATE KEY-----\nMIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQg9ll7+HiW0WJXe2Y9\nPbcz3tb1+gupimEIHinbAsf6VlehRANCAAROCZzBYdfSQ08DSmJkzDKfRdY3HwWu\nPG9rXw3ZxEfe/jcz9UD0cBukoqqPYG32JXjU5Iee9/ZNIBv3SEm5BPYd\n-----END PRIVATE KEY-----\n'
