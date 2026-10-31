"""Compare exact numeric/string request encodings through CLI and direct HTTP.

Only a fresh verified owned network-disabled tmpfs OpenBao 2.6.2 is used.
Native credentials and response bodies stay in process memory and stdin.
"""
from pathlib import Path
import datetime
import hashlib
import importlib.util
import json
import os
import re

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


harness = module('erc_source_gap_harness', ROOT / 'eng/ext-protection-1/probe_erc_native.py')
base = harness.base
shipped = module('erc_source_gap_bootstrap', ROOT / 'deploy/erasure-root/bootstrap.py')


class Diagnosis(base.Probe):
    def command(self, *args, **kwargs):
        started = datetime.datetime.now(datetime.timezone.utc).isoformat()
        try:
            return super().command(*args, **kwargs)
        finally:
            if self.result['commands']:
                self.result['commands'][-1].update(startedUtc=started,
                    finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())

    def execute(self):
        inspected = base.document(self.command('image-inspect', ['image', 'inspect', self.image]).stdout)
        if not isinstance(inspected, list) or len(inspected) != 1:
            raise base.Blocked('ImageIdentityUnverified')
        image = inspected[0]
        revision = image.get('Config', {}).get('Labels', {}).get('org.opencontainers.image.revision')
        if revision != 'dd9c19c37a878cf4a81b18efb8d6f0599c7da923' \
                or self.image.split('@')[0].split(':')[0] + '@' + self.image.split('@')[1] not in image.get('RepoDigests', []) \
                or set(image.get('Config', {}).get('Volumes', {})) != {'/openbao/file', '/openbao/logs'}:
            raise base.Blocked('ImageIdentityUnverified')
        self.mark('image-identity', sourceRevision=revision, localImageId=image.get('Id'))
        self.creation_attempted = True
        arguments = [value.replace(base.CONFIG, harness.ERC_CONFIG) for value in base.create_args(self.image, self.run_id, self.name)]
        self.container_id = self.command('container-create', arguments).stdout.decode('ascii').strip()
        if not re.fullmatch('[0-9a-f]{64}', self.container_id):
            raise base.Blocked('ContainerIdentityUnverified')
        inspected = base.document(self.command('container-inspect', ['inspect', self.container_id]).stdout)[0]
        base.require_isolation(inspected, self.run_id, self.container_id)
        self.mark('container-isolation', network='none', storage='tmpfs', publishedPorts=False)
        self.command('container-start', ['start', self.container_id])
        version = self.bao('native-version', 'version').stdout.decode('ascii').strip()
        if not version.startswith('OpenBao v2.6.2 (' + revision + ')'):
            raise base.Blocked('NativeVersionMismatch')
        self.mark('native-version', version='2.6.2', sourceRevision=revision)
        self.await_seal('startup-raft', False)
        initialized = base.document(self.bao('initialize', 'operator', 'init', '-format=json', '-key-shares=1', '-key-threshold=1').stdout)
        self.token, self.unseal_key = initialized['root_token'], initialized['unseal_keys_b64'][0]
        self.await_seal('unsealed', True)
        self.leader('raft-leader')
        self.write('transit-mount', 'sys/mounts/erc-transit', {'type': 'transit'})
        self.write('tenant-key', 'erc-transit/keys/epoch-a', {'type': 'aes256-gcm96', 'derived': True,
            'exportable': False, 'allow_plaintext_backup': False, 'deletion_allowed': False})
        self.write('successor-epoch', 'erc-transit/keys/epoch-a/rotate', {})
        scope = 'tenant-a:custody:key-lifecycle-v1|custody-key-lifecycle-candidate-v1'
        enrollment = {'nativeKeys': {'tenant-a': 'epoch-a'}, 'headMount': 'erc-heads', 'transitMount': 'erc-transit'}
        policy = shipped.render_policy(enrollment, 'tenant-a', scope)
        self.result['renderedPolicySha256'] = hashlib.sha256(policy.encode()).hexdigest()
        self.write('exact-shipped-policy', 'sys/policies/acl/source-gap-diagnosis', {'policy': policy})
        runtime = base.document(self.write('tenant-token', 'auth/token/create', {'policies': ['source-gap-diagnosis'],
            'no_default_policy': True, 'ttl': '10m', 'renewable': False}).stdout)
        if runtime['auth']['policies'] != ['source-gap-diagnosis']:
            raise base.Blocked('ExclusiveRuntimePolicyRequired')
        self.token = runtime['auth']['client_token']
        observations = []
        for transport in ('bao-json-stdin', 'direct-http'):
            for representation in ('number', 'string'):
                epoch = 2 if representation == 'number' else '2'
                body = {'min_decryption_version': epoch, 'min_encryption_version': epoch}
                label = transport + '-' + representation
                if transport == 'bao-json-stdin':
                    response = self.bao(label, 'write', '-format=json', 'erc-transit/keys/epoch-a/config', '-', payload=body, allow_failure=True)
                    found = re.search(rb'Code:\s*([1-5][0-9]{2})', response.stderr)
                    status = 204 if response.returncode == 0 else int(found[1]) if found else None
                else:
                    payload = json.dumps(body, separators=(',', ':')).encode()
                    wire = (b'POST /v1/erc-transit/keys/epoch-a/config HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n'
                        b'Content-Type: application/json\r\nX-Vault-Token: ' + self.token.encode() + b'\r\nContent-Length: '
                        + str(len(payload)).encode() + b'\r\n\r\n' + payload)
                    response = self.command(label, ['exec', '-i', self.container_id, 'nc', '-w', '5', '127.0.0.1', '8200'], wire, allow_failure=True)
                    found = re.match(rb'HTTP/1\.[01] ([1-5][0-9]{2})\b', response.stdout)
                    status = int(found[1]) if found else None
                observations.append({'transport': transport, 'requestVersionType': representation, 'httpStatus': status})
        self.result['comparisons'] = observations
        self.mark('request-comparison', count=len(observations))


probe = Diagnosis(base.IMAGE)
probe.result.update(schema='EXT-PROTECTION-1.source-gap-policy-diagnosis.v1',
    scope='Owned disposable request/ACL comparison only',
    startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    diagnosticSourceSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
    bootstrapSourceSha256=hashlib.sha256((ROOT / 'deploy/erasure-root/bootstrap.py').read_bytes()).hexdigest())
with (OUT / 'policy-diagnosis-prepared.json').open('x') as output:
    json.dump(probe.result, output, indent=2)
    output.flush()
    os.fsync(output.fileno())
try:
    probe.execute()
except base.Blocked as error:
    probe.result['failure'] = str(error)
except Exception as error:
    probe.result.update(failure='UnexpectedDiagnosticFailure', failureType=type(error).__name__)
finally:
    probe.token = probe.unseal_key = ''
    try:
        probe.cleanup()
    except Exception:
        probe.result['cleanup'] = {'complete': False}
probe.result.update(finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    outcome='Compared' if probe.result['failure'] is None and probe.result['cleanup']['complete'] else 'BlockedUnknown')
with (OUT / 'policy-diagnosis.result.json').open('x') as output:
    json.dump(probe.result, output, indent=2)
    output.write('\n')
print(json.dumps({key: probe.result.get(key) for key in ('outcome', 'failure', 'comparisons', 'cleanup')}))
raise SystemExit(0 if probe.result['outcome'] == 'Compared' else 1)
