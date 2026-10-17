#!/usr/bin/env python3
"""Repair only this installation's scoped components/ACLs/images; never initialize its enrolled registry."""
import base64
import json
import sys
import time
from datetime import datetime, timezone

from deploy_native_registry import eventstore_component_environment
from install_isolated_registry import Installer, NS, canonical, kube, kjson, sha
from native_journal import durable_write
from preload_isolated_image import preload
from registry_redis import components, native_acl, INTERNAL_REPLAY_ACL


def repair(installer, eventstore_image, inventory_image):
    before = json.loads((installer.evidence / 'installation-references.json').read_text())['installedResources']
    for name in ['eventstore', 'tenants', 'inventory']:
        value = kjson('get', 'deployment', name, '-n', NS)
        if value['metadata']['uid'] != before['Deployment/' + NS + '/' + name]['uid']:
            raise RuntimeError('new-workload-uid-changed:' + name)
    preload(installer, eventstore_image)
    preload(installer, inventory_image)
    writers = [{'name': name, 'uid': kjson('get', 'deployment', name, '-n', NS)['metadata']['uid'],
                'replicas': kjson('get', 'deployment', name, '-n', NS)['spec']['replicas']} for name in ['eventstore', 'tenants']]
    journal = {'writers': writers, 'phase': 'Prepared', 'nativeInitializationRepeated': False, 'contractOperations': 0}
    path = installer.evidence / 'native-runtime-repair.json'
    durable_write(path, journal)
    try:
        for item in writers:
            kube('scale', 'deployment', item['name'], '-n', NS, '--replicas=0')
        for _ in range(45):
            if not any(pod['metadata']['labels'].get('app') in ['eventstore', 'tenants'] for pod in kjson('get', 'pods', '-n', NS)['items']):
                break
            time.sleep(1)
        else:
            raise RuntimeError('new-native-writers-not-stopped')
        for app in ['eventstore', 'tenants']:
            for component in components(app):
                name = component['metadata']['name']
                if name.startswith('eventstore-'):
                    installer.create(component)
                else:
                    existing = kjson('get', 'component', name, '-n', NS)
                    if existing['metadata']['uid'] != before['Component/' + NS + '/' + name]['uid']:
                        raise RuntimeError('new-component-uid-changed:' + name)
                    kube('patch', 'component', name, '-n', NS, '--type=merge', '--patch-file=/dev/stdin',
                         data=canonical({'spec': component['spec'], 'scopes': component['scopes']}))
        passwords = {name: installer.secret_field('ext-secrets-1-registry-state-credentials', name) for name in ['eventstore', 'tenants', 'inventory']}
        maintenance = installer.secret_field('ext-secrets-1-registry-maintenance', 'password')
        acl = INTERNAL_REPLAY_ACL + '\n' + '\n'.join(native_acl(name, password) for name, password in passwords.items()) + '\n'
        acl += 'user registry-maintenance on >' + maintenance + ' resetkeys resetchannels ~* -@all +ping +info +bgsave +lastsave +dbsize +scan +shutdown +acl|dryrun +acl|load\n'
        kube('patch', 'secret', 'ext-secrets-1-registry-redis-acl', '-n', NS, '--type=merge', '--patch-file=/dev/stdin',
             data=canonical({'data': {'users.acl': base64.b64encode(acl.encode()).decode()}}))
        journal.update(phase='ScopedComponentsAndAclWritten', aclSha256=sha(acl.encode()),
                       componentReferences=[NS + '/Component/' + item['metadata']['name'] for app in ['eventstore', 'tenants'] for item in components(app)])
        durable_write(path, journal)
        old_pod_uid = kjson('get', 'pod', 'registry-state-0', '-n', NS)['metadata']['uid']
        kube('delete', 'pod', 'registry-state-0', '-n', NS, '--wait=false')
        for _ in range(60):
            pods = kjson('get', 'pods', '-n', NS)['items']
            current = next((pod for pod in pods if pod['metadata']['name'] == 'registry-state-0' and pod['metadata']['uid'] != old_pod_uid), None)
            if current and any(condition['type'] == 'Ready' and condition['status'] == 'True' for condition in current.get('status', {}).get('conditions', [])):
                break
            time.sleep(1)
        else:
            raise RuntimeError('isolated-redis-acl-startup-not-ready')
        script = 'IFS= read -r REDISCLI_AUTH || exit 65; export REDISCLI_AUTH; exec redis-cli --no-auth-warning --user registry-maintenance "$@"'
        observations = []
        for app in ['eventstore', 'tenants']:
            for key, should_allow in [(app + '||metadata-only-permission-probe', True),
                                     (('tenants' if app == 'eventstore' else 'eventstore') + '||metadata-only-permission-probe', False)]:
                result = kube('exec', '-i', '-n', NS, 'registry-state-0', '--', 'sh', '-c', script, 'sh', 'ACL', 'DRYRUN', app, 'HGET', key, 'value', data=maintenance.encode() + b'\n').stdout.decode().strip()
                allowed = result == 'OK'
                if allowed != should_allow:
                    raise RuntimeError('native-redis-workload-scope-check-failed')
                observations.append({'workload': app, 'keyNamespace': key.split('||')[0], 'allowed': allowed, 'commandExecuted': False})
        journal['nativeAclDryRun'] = observations
        for name, image in [('eventstore', eventstore_image), ('inventory', inventory_image)]:
            deployment = kjson('get', 'deployment', name, '-n', NS)
            containers = deployment['spec']['template']['spec']['containers']
            application = next(container for container in containers if container['name'] == name)
            application['image'] = image
            application['env'] = [item for item in application['env'] if item['name'] != 'Inventory__CallerAuthority']
            if name == 'eventstore':
                changes = eventstore_component_environment()
                application['env'] = [item for item in application['env'] if item['name'] not in changes] + [{'name': key, 'value': value} for key, value in changes.items()]
            kube('patch', 'deployment', name, '-n', NS, '--type=merge', '--patch-file=/dev/stdin', data=canonical({'spec': {'template': {'spec': {'containers': containers}}}}))
        journal['phase'] = 'ImagesAndAuthenticationUpdated'
        durable_write(path, journal)
    finally:
        for item in writers:
            current = kjson('get', 'deployment', item['name'], '-n', NS)
            if current['metadata']['uid'] == item['uid']:
                kube('scale', 'deployment', item['name'], '-n', NS, '--replicas=' + str(item['replicas']))
    for app in ['eventstore', 'tenants', 'inventory']:
        kube('rollout', 'status', 'deployment/' + app, '-n', NS, '--timeout=120s', timeout=125)
    journal.update(phase='ReadyForReadOnlyVerification', observedAt=datetime.now(timezone.utc).isoformat(),
                   eventstoreImage=eventstore_image, inventoryImage=inventory_image, independentWorkloadCredentialReference=None)
    durable_write(path, journal)


if __name__ == '__main__':
    instance = Installer(sys.argv[1])
    instance.created = json.loads((instance.evidence / 'created-resource-references.json').read_text())
    try:
        repair(instance, sys.argv[2], sys.argv[3])
        print('new native runtime scoped and restarted; independent workload trust inputs remain absent')
    finally:
        instance.close()
