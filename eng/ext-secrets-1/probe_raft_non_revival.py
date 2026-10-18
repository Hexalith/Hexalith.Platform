#!/usr/bin/env python3
"""Disposable ordinary-Raft recovery negative control; never qualifies custody.

Only the discovered, already local OpenBao image is accepted. The daemon is a
fixed local Unix socket, networking is disabled, and all mutable container paths
are tmpfs. Credentials and payloads travel over stdin, never argv or evidence.
"""

import argparse
import base64
import hashlib
import json
import os
import re
import secrets
import shlex
import signal
import subprocess
import time
from pathlib import Path


IMAGE = ('quay.io/openbao/openbao:2.6.2@sha256:'
         '11fd73a2102cda9c55d5d881a8c3210303146a7ec1e8ac76f526e175c6d24641')
VERSION = '2.6.2'
DOCKER = ['docker', '--host', 'unix:///var/run/docker.sock']
LABEL = 'hexalith.ext-protection-1.raft-probe'
MOUNTS = {'/tmp', '/openbao/file', '/openbao/logs'}
CONFIG = '''ui = false
api_addr = "http://127.0.0.1:8200"
cluster_addr = "https://127.0.0.1:8201"
listener "tcp" {
  address = "127.0.0.1:8200"
  cluster_address = "127.0.0.1:8201"
  tls_disable = true
}
storage "raft" {
  path = "/openbao/file/raft"
  node_id = "disposable-negative-control"
}
'''
WRAPPER = ('IFS= read -r BAO_TOKEN || exit 65; export BAO_TOKEN; '
           'export HOME=/tmp; exec bao "$@"')
REQUIRED = (
    'image-identity', 'container-isolation', 'native-version', 'initialized',
    'unsealed', 'raft-leader', 'transit-enabled', 'target-created',
    'control-created', 'target-encrypted', 'control-encrypted',
    'target-before-delete', 'control-before-snapshot', 'snapshot-saved',
    'snapshot-present', 'control-after-snapshot', 'target-deletion-enabled',
    'target-deleted', 'control-after-delete', 'target-absent',
    'target-decrypt-refused', 'control-before-restore', 'snapshot-restored',
    'restore-unsealed', 'restore-raft-leader', 'control-after-restore',
    'target-present-after-restore', 'target-decrypt-after-restore',
)


class Blocked(Exception):
    """A public fixed reason; never carries backend stdout/stderr or secrets."""


class PublicParser(argparse.ArgumentParser):
    """Never echo an unsupported argument that might contain a credential."""

    def error(self, message):
        self.exit(1, 'BlockedUnknown: InvalidArguments\n')


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def document(data):
    """Parse private native JSON without reflecting malformed content."""
    try:
        return json.loads(data)
    except (ValueError, UnicodeError, TypeError):
        raise Blocked('MalformedNativeResponse') from None


def run_command(args, data=None, timeout=20):
    """Capture private output; every subprocess has a finite deadline."""
    try:
        return subprocess.run(DOCKER + args, input=data, capture_output=True,
                              timeout=timeout, check=False)
    except subprocess.TimeoutExpired:
        raise Blocked('CommandTimeout') from None
    except OSError:
        raise Blocked('LocalDockerUnavailable') from None


def create_args(image, run_id, name):
    if image != IMAGE:
        raise Blocked('DiscoveryImagePinMismatch')
    startup = ('umask 077; mkdir -p /openbao/file/raft; printf %s ' + shlex.quote(CONFIG)
               + ' > /tmp/probe.hcl; exec bao server -config=/tmp/probe.hcl')
    args = ['create', '--pull=never', '--name', name, '--label', LABEL + '=' + run_id,
            '--network=none', '--read-only', '--log-driver=none',
            '--cap-drop=ALL', '--security-opt=no-new-privileges',
            '--user=65532:65532', '--memory=2g', '--memory-swap=2g',
            '--pids-limit=128', '--env', 'BAO_ADDR=http://127.0.0.1:8200']
    args += ['--env', 'GOMAXPROCS=2', '--env', 'GOMEMLIMIT=128MiB']
    for path in sorted(MOUNTS):
        args += ['--tmpfs', path + ':rw,noexec,nosuid,nodev,size=128m,'
                 'mode=0700,uid=65532,gid=65532']
    return args + ['--entrypoint=/bin/sh', image, '-ec', startup]


def require_isolation(value, run_id, container_id):
    """Reject binds, persistent volumes, published ports and foreign ownership."""
    host = value.get('HostConfig', {})
    config = value.get('Config', {})
    mounts = value.get('Mounts', [])
    valid = (value.get('Id') == container_id
             and config.get('Labels', {}).get(LABEL) == run_id
             and host.get('NetworkMode') == 'none'
             and host.get('ReadonlyRootfs') is True
             and host.get('LogConfig', {}).get('Type') == 'none'
             and not host.get('Privileged')
             and not host.get('Binds') and not host.get('PortBindings')
             and config.get('User') == '65532:65532'
             and host.get('CapDrop') == ['ALL']
             and 'no-new-privileges' in host.get('SecurityOpt', [])
             and set(host.get('Tmpfs', {})) == MOUNTS
             and all(m.get('Type') == 'tmpfs' and m.get('Destination') in MOUNTS
                     for m in mounts))
    if not valid:
        raise Blocked('ContainerIsolationMismatch')


class Probe:
    """Owns one new container and an allowlisted public observation ledger."""

    def __init__(self, image, runner=run_command, sleep=time.sleep):
        self.image = image
        self.runner = runner
        self.sleep = sleep
        self.run_id = secrets.token_hex(16)
        self.name = 'ext-protection-1-raft-probe-' + self.run_id
        self.container_id = None
        self.creation_attempted = False
        self.token = ''
        self.unseal_key = ''
        self.ciphertexts = {}
        self.plaintexts = {}
        self.result = {
            'schema': 'EXT-PROTECTION-1.ordinary-raft-negative-control.v1',
            'runId': self.run_id, 'containerName': self.name,
            'ownershipLabel': {LABEL: self.run_id}, 'phase': 'Prepared',
            'image': image if image == IMAGE else None,
            'expectedNativeVersion': VERSION,
            'probeSourceSha256': sha256(Path(__file__).read_bytes()),
            'serverConfigurationSha256': sha256(CONFIG.encode()),
            'scope': 'Disposable single-node ordinary Raft; native transit only',
            'productionQualified': False, 'g1Qualified': False,
            'transactionProofExecuted': False, 'productionEngineTested': False,
            'independentErasureAuthorityTested': False,
            'secretMaterialIncludedInEvidence': False, 'hostMemoryErasureProven': False,
            'observations': {}, 'commands': [], 'cleanup': {'complete': False},
            'outcome': 'BlockedUnknown', 'failure': None,
        }

    def command(self, stage, args, data=None, allow_failure=False):
        record = {'stage': stage, 'argv': DOCKER + args, 'stdin':
                  'private-omitted' if data is not None else 'none'}
        self.result['commands'].append(record)
        try:
            response = self.runner(args, data=data)
        except Blocked:
            record['status'] = 'Unavailable'
            raise
        record['exitCode'] = response.returncode
        # Do not retain raw stdout, stderr, bodies, tokens, keys or snapshots.
        match = re.search(rb'\bCode:\s*([1-5][0-9]{2})\b', response.stderr)
        if match:
            record['nativeHttpStatus'] = int(match[1])
        if response.returncode != 0 and not allow_failure:
            raise Blocked('NativeCommandFailed')
        return response

    def bao(self, stage, *args, payload=None, allow_failure=False):
        if not self.container_id:
            raise Blocked('ContainerNotOwned')
        data = self.token.encode() + b'\n'
        if payload is not None:
            data += json.dumps(payload, separators=(',', ':')).encode()
        return self.command(stage, ['exec', '-i', self.container_id, 'sh', '-ec',
                                   WRAPPER, 'sh', *args], data, allow_failure)

    def mark(self, stage, **values):
        self.result['observations'][stage] = {'verified': True, **values}

    def write(self, stage, path, payload):
        response = self.bao(stage, 'write', '-format=json', path, '-', payload=payload)
        if response.stdout.strip():
            value = document(response.stdout)
            if not isinstance(value, dict) or value.get('errors'):
                raise Blocked('UnexpectedNativeResponse')
        self.mark(stage)
        return response

    def await_seal(self, stage, initialized):
        # Readiness retries are bounded and never retry destructive operations.
        for _ in range(40):
            response = self.bao(stage, 'read', '-format=json', 'sys/seal-status',
                                allow_failure=True)
            if response.returncode == 0:
                value = document(response.stdout)
                data = value.get('data', {})
                if (data.get('initialized') is initialized
                        and data.get('storage_type') == 'raft'):
                    if initialized and data.get('sealed') is True:
                        self.bao(stage + '-unseal', 'write', '-format=json', 'sys/unseal',
                                 '-', payload={'key': self.unseal_key})
                    elif data.get('sealed') is (not initialized):
                        self.mark(stage, storageType='raft', initialized=initialized,
                                  sealed=not initialized)
                        return
            self.sleep(0.25)
        raise Blocked('ReadinessUnverified')

    def leader(self, stage):
        for _ in range(40):
            response = self.bao(stage, 'read', '-format=json', 'sys/leader',
                                allow_failure=True)
            if response.returncode == 0:
                data = document(response.stdout).get('data', {})
                if data.get('ha_enabled') is True and data.get('is_self') is True:
                    self.mark(stage, leader=True)
                    return
            self.sleep(0.25)
        raise Blocked('RaftLeaderUnverified')

    def decrypt(self, stage, key):
        response = self.bao(stage, 'write', '-format=json', 'probe-transit/decrypt/' + key,
                            '-', payload={'ciphertext': self.ciphertexts[key]})
        actual = document(response.stdout).get('data', {}).get('plaintext')
        if not isinstance(actual, str) or not secrets.compare_digest(actual, self.plaintexts[key]):
            raise Blocked('PlaintextMatchUnverified')
        self.mark(stage, plaintextMatches=True)

    def key_list(self, stage, expected):
        response = self.bao(stage, 'list', '-format=json', 'probe-transit/keys')
        keys = document(response.stdout)
        if not isinstance(keys, list) or sorted(keys) != sorted(expected):
            raise Blocked('KeyInventoryUnverified')
        self.mark(stage, targetPresent='target' in keys, controlPresent='control' in keys)

    def await_restored_keys(self):
        """Poll only native reads/decryptions while asynchronous restore applies."""
        stages = ('target-present-after-restore', 'target-decrypt-after-restore',
                  'control-after-restore')
        for _ in range(40):
            try:
                self.key_list(stages[0], ['control', 'target'])
                self.decrypt(stages[1], 'target')
                self.decrypt(stages[2], 'control')
                return
            except Blocked as error:
                for stage in stages:
                    self.result['observations'].pop(stage, None)
                reason = str(error)
                status = self.result['commands'][-1].get('nativeHttpStatus')
                retryable = (reason in ('KeyInventoryUnverified', 'CommandTimeout')
                             or (reason == 'NativeCommandFailed'
                                 and status in (404, 429, 500, 502, 503, 504)))
                if not retryable:
                    raise
            self.sleep(0.25)
        raise Blocked('RestoredKeysUnverified')

    def execute(self):
        if self.image != IMAGE:
            raise Blocked('DiscoveryImagePinMismatch')
        inspected = document(self.command('image-inspect', ['image', 'inspect', self.image]).stdout)
        if not isinstance(inspected, list) or len(inspected) != 1:
            raise Blocked('ImageIdentityUnverified')
        image = inspected[0]
        revision = image.get('Config', {}).get('Labels', {}).get('org.opencontainers.image.revision')
        if (not isinstance(revision, str) or not re.fullmatch('[0-9a-f]{40}', revision)
                or self.image.split('@')[0].split(':')[0] + '@' + self.image.split('@')[1]
                not in image.get('RepoDigests', [])
                or set(image.get('Config', {}).get('Volumes', {}))
                != {'/openbao/file', '/openbao/logs'}):
            raise Blocked('ImageIdentityUnverified')
        self.mark('image-identity', localImageId=image.get('Id'), sourceRevision=revision,
                  architecture=image.get('Architecture'), operatingSystem=image.get('Os'))
        self.creation_attempted = True
        created = self.command('container-create', create_args(self.image, self.run_id, self.name))
        self.container_id = created.stdout.decode('ascii').strip()
        if not re.fullmatch('[0-9a-f]{64}', self.container_id):
            self.container_id = None
            raise Blocked('ContainerIdentityUnverified')
        value = document(self.command('container-inspect', ['inspect', self.container_id]).stdout)[0]
        require_isolation(value, self.run_id, self.container_id)
        self.mark('container-isolation', network='none', listener='127.0.0.1',
                  publishedPorts=False, persistentVolumes=False, storage='tmpfs', logging='none')
        self.command('container-start', ['start', self.container_id])
        version = self.bao('native-version', 'version').stdout.decode('ascii').strip()
        match = re.match(r'^OpenBao v([^ ]+) \(([0-9a-f]{40})\)(?:,|$)', version)
        if not match or match.group(1) != VERSION or match.group(2) != revision:
            raise Blocked('NativeVersionMismatch')
        self.mark('native-version', version=VERSION, sourceRevision=revision)
        self.await_seal('startup-raft', False)
        response = self.bao('initialize', 'operator', 'init', '-format=json',
                            '-key-shares=1', '-key-threshold=1')
        initialized = document(response.stdout)
        keys = initialized.get('unseal_keys_b64')
        token = initialized.get('root_token')
        if not isinstance(keys, list) or len(keys) != 1 or not keys[0] or not isinstance(token, str) or not token:
            raise Blocked('InitializationUnverified')
        self.token, self.unseal_key = token, keys[0]
        self.mark('initialized', fresh=True, keyShares=1, keyThreshold=1)
        self.await_seal('unsealed', True)
        self.leader('raft-leader')
        self.bao('transit-enabled', 'secrets', 'enable', '-path=probe-transit', 'transit')
        self.mark('transit-enabled')
        for key in ('target', 'control'):
            self.write(key + '-created', 'probe-transit/keys/' + key,
                       {'type': 'aes256-gcm96', 'exportable': False, 'allow_plaintext_backup': False})
            self.plaintexts[key] = base64.b64encode(secrets.token_bytes(32)).decode('ascii')
            response = self.bao(key + '-encrypted', 'write', '-format=json',
                                'probe-transit/encrypt/' + key, '-',
                                payload={'plaintext': self.plaintexts[key]})
            ciphertext = document(response.stdout).get('data', {}).get('ciphertext')
            if not isinstance(ciphertext, str) or not ciphertext.startswith('vault:v1:'):
                raise Blocked('EncryptionUnverified')
            self.ciphertexts[key] = ciphertext
            self.mark(key + '-encrypted')
        self.decrypt('target-before-delete', 'target')
        self.decrypt('control-before-snapshot', 'control')
        self.bao('snapshot-saved', 'operator', 'raft', 'snapshot', 'save', '/tmp/pre-delete.snap')
        self.mark('snapshot-saved', ordinarySnapshot=True)
        size = self.command('snapshot-present', ['exec', self.container_id, 'stat', '-c', '%s',
                                                '/tmp/pre-delete.snap']).stdout.strip()
        if not size.isdigit() or not 0 < int(size) <= 128 * 1024 * 1024:
            raise Blocked('SnapshotUnverified')
        self.mark('snapshot-present', nonempty=True)
        self.decrypt('control-after-snapshot', 'control')
        self.write('target-deletion-enabled', 'probe-transit/keys/target/config', {'deletion_allowed': True})
        self.bao('target-deleted', 'delete', 'probe-transit/keys/target')
        self.mark('target-deleted')
        self.decrypt('control-after-delete', 'control')
        self.key_list('target-absent', ['control'])
        refused = self.bao('target-decrypt-refused', 'write', '-format=json',
                           'probe-transit/decrypt/target', '-',
                           payload={'ciphertext': self.ciphertexts['target']}, allow_failure=True)
        if (refused.returncode == 0
                or not re.search(rb'\bCode:\s*400\b', refused.stderr)
                or b'encryption key not found' not in refused.stderr.lower()):
            raise Blocked('DeletionRefusalUnverified')
        self.mark('target-decrypt-refused', nativeHttpStatus=400, missingKey=True)
        self.decrypt('control-before-restore', 'control')
        self.bao('snapshot-restored', 'operator', 'raft', 'snapshot', 'restore', '/tmp/pre-delete.snap')
        self.mark('snapshot-restored', force=False, originalSeal=True)
        self.await_seal('restore-unsealed', True)
        self.leader('restore-raft-leader')
        self.await_restored_keys()

    def cleanup(self):
        """Inspect exact ownership before removal, including ambiguous create."""
        if not self.creation_attempted:
            self.result['cleanup'] = {'complete': True, 'containerCreated': False}
            return
        target = self.container_id or self.name
        response = self.command('cleanup-inspect', ['inspect', target], allow_failure=True)
        if response.returncode != 0:
            if b'no such object:' in response.stderr.lower() or b'no such container:' in response.stderr.lower():
                if self.container_id is None:
                    raise Blocked('CreateCompletionUnknown')
                self.result['cleanup'] = {'complete': True, 'containerAbsent': True}
                return
            raise Blocked('CleanupInspectionFailed')
        values = document(response.stdout)
        if not isinstance(values, list) or len(values) != 1:
            raise Blocked('CleanupOwnershipUnverified')
        value = values[0]
        state = value.get('State', {})
        self.result['ownedContainerStateBeforeCleanup'] = {
            'running': state.get('Running'), 'exitCode': state.get('ExitCode'),
            'oomKilled': state.get('OOMKilled')}
        owned_id = value.get('Id', '')
        if (not re.fullmatch('[0-9a-f]{64}', owned_id)
                or value.get('Config', {}).get('Labels', {}).get(LABEL) != self.run_id
                or (self.container_id and owned_id != self.container_id)):
            raise Blocked('CleanupOwnershipUnverified')
        self.command('cleanup-remove', ['rm', '--force', '--volumes', owned_id])
        absent = self.command('cleanup-verify', ['inspect', owned_id], allow_failure=True)
        if absent.returncode == 0 or not (b'no such object:' in absent.stderr.lower() or b'no such container:' in absent.stderr.lower()):
            raise Blocked('CleanupAbsenceUnverified')
        self.result['cleanup'] = {'complete': True, 'containerAbsent': True,
                                  'containerTmpfsUnmounted': True}

    def run(self):
        try:
            self.execute()
        except Blocked as error:
            self.result['failure'] = str(error)
        except (KeyboardInterrupt, Exception):
            # Never serialize arbitrary exception text, which may contain input.
            self.result['failure'] = 'InterruptedOrUnexpectedFailure'
        finally:
            self.token = self.unseal_key = ''
            self.ciphertexts.clear()
            self.plaintexts.clear()
            try:
                self.cleanup()
            except Blocked as error:
                self.result['cleanup'] = {'complete': False, 'failure': str(error)}
            except (KeyboardInterrupt, Exception):
                self.result['cleanup'] = {'complete': False, 'failure': 'CleanupUnverified'}
        self.result['phase'] = 'Completed'
        complete = all(self.result['observations'].get(stage, {}).get('verified') is True
                       for stage in REQUIRED)
        if complete and self.result['cleanup']['complete'] and self.result['failure'] is None:
            self.result['outcome'] = 'ObservedRevival'
            self.result['nonRevivalRequirement'] = 'FailedForOrdinaryRaftSnapshot'
            self.result['productionConstruction'] = 'StoppedBeforeTransactionProof'
            return self.result, 2
        return self.result, 1


def main(argv=None):
    parser = PublicParser(description=__doc__)
    parser.add_argument('--image', required=True, help='Exact discovered digest; must already exist locally')
    parser.add_argument('--output', required=True, type=Path, help='New public JSON result file (never overwritten)')
    args = parser.parse_args(argv)
    # Reserve the public output before effects; an existing/symlink path refuses.
    try:
        output = args.output.open('x', encoding='utf-8')
    except OSError:
        print('BlockedUnknown: PublicOutputUnavailable')
        return 1
    previous = signal.signal(signal.SIGTERM, lambda *_: (_ for _ in ()).throw(KeyboardInterrupt()))
    try:
        with output:
            instance = Probe(args.image)
            # Keep recovery identity at the beginning in both prepared/final JSON.
            # A killed interpreter must not erase the only owned-container locator.
            json.dump(instance.result, output, indent=2)
            output.write('\n')
            output.flush()
            os.fsync(output.fileno())
            result, code = instance.run()
            output.seek(0)
            json.dump(result, output, indent=2)
            output.write('\n')
            output.truncate()
            output.flush()
            os.fsync(output.fileno())
        print(result['outcome'] + ': G1 remains open; production construction stopped')
        return code
    finally:
        signal.signal(signal.SIGTERM, previous)


if __name__ == '__main__':
    raise SystemExit(main())
