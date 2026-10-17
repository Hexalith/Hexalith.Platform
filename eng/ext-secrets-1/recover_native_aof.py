#!/usr/bin/env python3
"""Reload genuine retained native AOF after fixing disabled-user replay permissions.

All backing files are preserved inside the intended isolated PVC. This never
initializes EventStore, submits commands, changes tenant IDs or exports data.
"""
import base64
import json
import sys
import time
from datetime import datetime, timezone
from install_isolated_registry import Installer, NS, canonical, kjson, kube
from native_journal import durable_write
from registry_redis import INTERNAL_REPLAY_ACL


def recover(installer):
    repair = json.loads((installer.evidence / 'native-runtime-repair.json').read_text())
    for writer in repair['writers']:
        current = kjson('get', 'deployment', writer['name'], '-n', NS)
        if current['metadata']['uid'] != writer['uid'] or current['spec']['replicas'] != 0:
            raise RuntimeError('native-aof-recovery-requires-recorded-writers-stopped')
    directory = '/data/pre-acl-recovery-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    kube('exec', '-n', NS, 'registry-state-0', '--', 'sh', '-c',
         'mkdir ' + directory + ' && cp -a /data/appendonlydir ' + directory + '/ && cp -a /data/dump.rdb ' + directory + '/ && chmod 700 ' + directory)
    checksums = kube('exec', '-n', NS, 'registry-state-0', '--', 'sh', '-c',
                     'sha256sum ' + directory + '/appendonlydir/* ' + directory + '/dump.rdb').stdout.decode().splitlines()
    evidence = {'phase': 'OriginalFilesPreserved', 'preservedDirectory': NS + '/PersistentVolumeClaim/data-registry-state-0#' + directory,
                'originalBackingFileChecksums': checksums, 'nativeInitializationRepeated': False, 'providerContentsExported': False, 'contractOperations': 0}
    durable_write(installer.evidence / 'native-aof-recovery.json', evidence)
    acl = installer.secret_field('ext-secrets-1-registry-redis-acl', 'users\\.acl')
    acl = INTERNAL_REPLAY_ACL + '\n' + '\n'.join(line for line in acl.splitlines() if not line.startswith('user default ')) + '\n'
    kube('patch', 'secret', 'ext-secrets-1-registry-redis-acl', '-n', NS, '--type=merge', '--patch-file=/dev/stdin',
         data=canonical({'data': {'users.acl': base64.b64encode(acl.encode()).decode()}}))
    old = kjson('get', 'pod', 'registry-state-0', '-n', NS)['metadata']['uid']
    kube('delete', 'pod', 'registry-state-0', '-n', NS, '--wait=false')
    for _ in range(60):
        current = next((pod for pod in kjson('get', 'pods', '-n', NS)['items'] if pod['metadata']['name'] == 'registry-state-0' and pod['metadata']['uid'] != old), None)
        if current and any(item['type'] == 'Ready' and item['status'] == 'True' for item in current.get('status', {}).get('conditions', [])): break
        time.sleep(1)
    else:
        raise RuntimeError('native-aof-recovery-redis-not-ready')
    log = kube('logs', 'registry-state-0', '-n', NS).stdout.decode()
    if 'AOF-loading-client' in log or 'NOPERM' in log:
        raise RuntimeError('native-aof-replay-still-denied')
    password = installer.secret_field('ext-secrets-1-registry-maintenance', 'password')
    script = 'IFS= read -r REDISCLI_AUTH || exit 65; export REDISCLI_AUTH; exec redis-cli --no-auth-warning --user registry-maintenance --scan'
    names = kube('exec', '-i', '-n', NS, 'registry-state-0', '--', 'sh', '-c', script, data=password.encode() + b'\n').stdout.decode().splitlines()
    receipts = json.loads((installer.evidence / 'native-enrollment-receipts.json').read_text())['tenants']
    required = ['eventstore||projection-delivery-writer-protocol'] + [
        'eventstore||AggregateActor||system:tenants:' + item['tenantId'] + '||system:tenants:' + item['tenantId'] + ':events:1' for item in receipts]
    if not all(name in names for name in required):
        raise RuntimeError('original-native-history-not-recovered-keep-writers-stopped')
    evidence.update(phase='OriginalAofLoaded', nativeKeyCount=len(names), originalNativeKeyNamesVerified=required,
                    aofLoadingAclErrors=0, tenantStreamContentVerificationPending=True)
    durable_write(installer.evidence / 'native-aof-recovery.json', evidence)
    for writer in repair['writers']:
        current = kjson('get', 'deployment', writer['name'], '-n', NS)
        if current['metadata']['uid'] != writer['uid']:
            raise RuntimeError('native-writer-uid-changed-before-restart')
        kube('scale', 'deployment', writer['name'], '-n', NS, '--replicas=' + str(writer['replicas']))
    print('genuine retained AOF loaded without ACL errors; original native stream keys and writer marker present; content verification pending')


if __name__ == '__main__':
    instance = Installer(sys.argv[1])
    try:
        recover(instance)
    finally:
        instance.close()
