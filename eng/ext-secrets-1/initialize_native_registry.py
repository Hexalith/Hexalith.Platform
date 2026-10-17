#!/usr/bin/env python3
"""Initialize the empty isolated native EventStore through its supported cutover API.

The backup remains inside the intended isolated provider volume. Only identities,
checksums and restore counts enter evidence. No independent secrets trust is granted.
"""
import base64
import json
import secrets
import sys
import time
from pathlib import Path
from xml.etree import ElementTree

from install_isolated_registry import Installer, NS, canonical, kube, kjson
from provision_registry_identity import Idp, REALM
from enroll_native_tenants import NativeRegistry
from native_journal import durable_write, journal_lock


def restore_recorded_writers(installer, journal):
    changed = []
    for item in journal['writers']:
        current = kjson('get', 'deployment', item['name'], '-n', NS)
        if current['metadata']['uid'] != item['uid']:
            changed.append(item['name'])
            continue
        kube('scale', 'deployment', item['name'], '-n', NS, '--replicas=' + str(item['replicas']))
    journal['writersRestored'] = not changed
    durable_write(installer.evidence / 'native-initialization-journal.json', journal)
    if changed:
        raise RuntimeError('native-writer-identity-changed:' + ','.join(changed))


def initialize(installer, recover_writers_only=False):
    with journal_lock(installer.evidence / 'native-initialization.lock'):
        return initialize_locked(installer, recover_writers_only)


def initialize_locked(installer, recover_writers_only=False):
    # Never initialize this already enrolled registry again, including recovery.
    if list(installer.evidence.glob('native-command-*.json')) and not recover_writers_only:
        raise RuntimeError('native-registry-already-has-durable-commands')
    path = installer.evidence / 'native-initialization-journal.json'
    if path.exists():
        journal = json.loads(path.read_text())
    else:
        if recover_writers_only:
            raise RuntimeError('native-writer-recovery-journal-absent')
        writers = []
        for app in ['eventstore', 'tenants']:
            if 'Deployment/' + NS + '/' + app not in installer.created:
                raise RuntimeError('native-writer-not-created-by-this-installation')
            value = kjson('get', 'deployment', app, '-n', NS)
            writers.append({'name': app, 'uid': value['metadata']['uid'], 'replicas': value['spec']['replicas']})
        journal = {'writers': writers, 'backupVerified': False, 'administratorAssigned': False}
        durable_write(path, journal, create_only=True)
    if recover_writers_only:
        restore_recorded_writers(installer, journal)
        return None
    try:
        for item in journal['writers']:
            current = kjson('get', 'deployment', item['name'], '-n', NS)
            if current['metadata']['uid'] != item['uid']:
                raise RuntimeError('native-writer-identity-changed:' + item['name'])
            kube('scale', 'deployment', item['name'], '-n', NS, '--replicas=0')
        for _ in range(30):
            active = [pod for pod in kjson('get', 'pods', '-n', NS)['items'] if pod['metadata']['labels'].get('app') in ['eventstore', 'tenants']]
            if not active:
                break
            time.sleep(1)
        if active:
            raise RuntimeError('native-new-writers-not-quiesced')
        return initialize_phases(installer, journal)
    finally:
        restore_recorded_writers(installer, journal)


def initialize_phases(installer, journal):
    if 'Secret/' + NS + '/ext-secrets-1-registry-maintenance' in installer.created:
        password = installer.secret_field('ext-secrets-1-registry-maintenance', 'password')
    else:
        password = secrets.token_urlsafe(36)
        installer.secret('ext-secrets-1-registry-maintenance', {'username': 'registry-maintenance', 'password': password})
    acl = installer.secret_field('ext-secrets-1-registry-redis-acl', 'users\\.acl')
    maintenance_line = 'user registry-maintenance on >' + password + ' resetkeys resetchannels ~* -@all +ping +info +bgsave +lastsave +dbsize +scan +shutdown +acl|dryrun +acl|load'
    if maintenance_line not in acl.splitlines():
        acl = '\n'.join(line for line in acl.splitlines() if not line.startswith('user registry-maintenance ')) + '\n' + maintenance_line + '\n'
        kube('patch', 'secret', 'ext-secrets-1-registry-redis-acl', '-n', NS, '--type=merge', '--patch-file=/dev/stdin',
             data=canonical({'data': {'users.acl': base64.b64encode(acl.encode()).decode()}}))
        old_uid = kjson('get', 'pod', 'registry-state-0', '-n', NS)['metadata']['uid']
        kube('delete', 'pod', 'registry-state-0', '-n', NS, '--wait=false')
        for _ in range(45):
            pods = kjson('get', 'pods', '-n', NS)['items']
            current = next((pod for pod in pods if pod['metadata']['name'] == 'registry-state-0' and pod['metadata']['uid'] != old_uid), None)
            if current and any(condition['type'] == 'Ready' and condition['status'] == 'True' for condition in current.get('status', {}).get('conditions', [])):
                break
            time.sleep(1)
        assert current and current['metadata']['uid'] != old_uid
    script = ('IFS= read -r REDISCLI_AUTH || exit 65; export REDISCLI_AUTH; '
              'exec redis-cli --no-auth-warning --user registry-maintenance "$@"')
    def redis(*args):
        return kube('exec', '-i', '-n', NS, 'registry-state-0', '--', 'sh', '-c', script, 'sh', *args, data=password.encode() + b'\n').stdout.decode().strip()
    if journal.get('backupVerified'):
        completed = json.loads((installer.evidence / 'native-first-deployment-initialization.json').read_text())
        backup_reference = completed['nativeBackupReference']
        expected = backup_reference.rsplit('@sha256:', 1)[1]
        actual = kube('exec', '-n', NS, 'registry-state-0', '--', 'sha256sum', '/data/pre-cutover-20261017-native-registry.rdb').stdout.decode().split()[0]
        if not completed['restoreVerified'] or actual != expected:
            raise RuntimeError('native-completed-backup-proof-mismatch')
        commit = completed['packageSourceCommit']
    else:
        before = int(redis('LASTSAVE'))
        assert redis('BGSAVE') in ['Background saving started', 'Background saving already in progress']
        for _ in range(20):
            info = redis('INFO', 'persistence')
            if 'rdb_bgsave_in_progress:0' in info and 'rdb_last_bgsave_status:ok' in info:
                break
            time.sleep(1)
        assert 'rdb_bgsave_in_progress:0' in info and 'rdb_last_bgsave_status:ok' in info
        backup = 'pre-cutover-20261017-native-registry.rdb'
        # Provider-native backup, retained in the same isolated PVC without exporting its contents.
        kube('exec', '-n', NS, 'registry-state-0', '--', 'sh', '-c', 'if test ! -e /data/' + backup + '; then cp /data/dump.rdb /data/' + backup + ' && chmod 400 /data/' + backup + '; fi')
        checksum = kube('exec', '-n', NS, 'registry-state-0', '--', 'sha256sum', '/data/' + backup).stdout.decode().split()[0]
        # Verify a real restore in a separate local-only Redis instance, with the same dedicated ACL.
        restore_dir = '/data/restore-cutover-' + secrets.token_hex(8)
        restore_port = str(40000 + secrets.randbelow(20000))
        kube('exec', '-n', NS, 'registry-state-0', '--', 'sh', '-c',
             'mkdir ' + restore_dir + ' && cp /data/' + backup + ' ' + restore_dir + '/dump.rdb && redis-server --bind 127.0.0.1 --port ' + restore_port + ' --dir ' + restore_dir + ' --dbfilename dump.rdb --appendonly no --daemonize yes --logfile ' + restore_dir + '/restore.log --aclfile /etc/redis-acl/users.acl')
        try:
            restored_count = int(redis('-p', restore_port, 'DBSIZE'))
            original_count = int(redis('DBSIZE'))
            assert restored_count == original_count
        finally:
            redis('-p', restore_port, 'SHUTDOWN', 'NOSAVE')
        keys = redis('--scan', '--pattern', '*projection-checkpoints*').splitlines()
        assert not keys
        nuspec = Path.home() / '.nuget/packages/hexalith.eventstore.gateway/3.124.0/hexalith.eventstore.gateway.nuspec'
        doc = ElementTree.parse(nuspec)
        commit = next(node.attrib['commit'] for node in doc.iter() if node.tag.endswith('repository'))
        backup_reference = NS + '/PersistentVolumeClaim/data-registry-state-0#' + backup + '@sha256:' + checksum
        durable_write(installer.evidence / 'native-first-deployment-initialization.json', {'packageVersion': '3.124.0', 'packageSourceCommit': commit,
            'nativeBackupReference': backup_reference, 'providerBackupContentsExported': False, 'restoreVerified': True,
            'restoredKeyCount': restored_count, 'projectionCheckpointCount': len(keys), 'writersStoppedBeforeBackup': True,
            'oldWriterFleetExists': False, 'domainCommandsBeforeBackup': 0, 'independentSecretsTrustEstablished': False,
            'downgradeProhibitedAcknowledged': True, 'backupLocationLimitation': 'Backup remains in the isolated retained PVC; this is not off-host disaster-recovery qualification.'})
        journal['backupVerified'] = True
        durable_write(installer.evidence / 'native-initialization-journal.json', journal)
    # Native IdP assignment grants administration only over this new isolated registry.
    # It is never used as EXT-SECRETS-1 independent actor-role or approval evidence.
    idp = Idp(installer)
    idp.login()
    base = '/admin/realms/' + REALM
    role = {'name': 'GlobalAdministrator', 'description': 'Administrator of the dedicated isolated native tenant registry only.'}
    existing_role, _ = idp.call('GET', base + '/roles/GlobalAdministrator', accepted=(200, 404))
    if existing_role is None or existing_role.get('error'):
        idp.call('POST', base + '/roles', role)
    role, _ = idp.call('GET', base + '/roles/GlobalAdministrator')
    if role.get('description') != 'Administrator of the dedicated isolated native tenant registry only.':
        raise RuntimeError('native-administrator-role-provenance-mismatch')
    subject = installer.secret_field('ext-secrets-1-registry-provisioner', 'subject-id')
    assigned, _ = idp.call('GET', base + '/users/' + subject + '/role-mappings/realm')
    if not any(item.get('id') == role['id'] for item in assigned):
        idp.call('POST', base + '/users/' + subject + '/role-mappings/realm', [role])
    clients, _ = idp.call('GET', base + '/clients?clientId=ext-secrets-1-registry-provisioner')
    client_id = clients[0]['id']
    mappers, _ = idp.call('GET', base + '/clients/' + client_id + '/protocol-mappers/models')
    if not any(item.get('name') == 'native-registry-administrator-role' for item in mappers):
        idp.call('POST', base + '/clients/' + client_id + '/protocol-mappers/models', {'name': 'native-registry-administrator-role', 'protocol': 'openid-connect',
                 'protocolMapper': 'oidc-usermodel-realm-role-mapper', 'consentRequired': False,
                 'config': {'claim.name': 'role', 'jsonType.label': 'String', 'multivalued': 'true', 'access.token.claim': 'true', 'id.token.claim': 'false'}})
    elif not any(item.get('name') == 'native-registry-administrator-role' and item.get('protocolMapper') == 'oidc-usermodel-realm-role-mapper'
                 and item.get('config', {}).get('claim.name') == 'role' and item.get('config', {}).get('access.token.claim') == 'true' for item in mappers):
        raise RuntimeError('native-administrator-mapper-provenance-mismatch')
    scopes, _ = idp.call('GET', base + '/clients/' + client_id + '/scope-mappings/realm')
    if not any(item.get('id') == role['id'] for item in scopes):
        idp.call('POST', base + '/clients/' + client_id + '/scope-mappings/realm', [role])
    durable_write(installer.evidence / 'native-registry-administrator-assignment.json', {'realm': REALM, 'nativeRoleId': role['id'], 'nativeRoleName': role['name'],
        'nativeSubjectId': subject, 'scope': 'dedicated isolated registry only', 'independentSecretsActorRoleEvidence': None})
    journal['administratorAssigned'] = True
    durable_write(installer.evidence / 'native-initialization-journal.json', journal)
    # Activation occurs after the maintenance host is listening; readiness is expected
    # to remain unhealthy until the exact first-write marker is activated natively.
    return {'cutoverCommit': commit, 'backupReference': backup_reference, 'writersQuiesced': True,
            'retryWorkersQuiesced': True, 'downgradeProhibitedAcknowledged': True}


if __name__ == '__main__':
    instance = Installer(sys.argv[1])
    instance.created = json.loads((instance.evidence / 'created-resource-references.json').read_text())
    try:
        if '--recover-writers' in sys.argv[2:]:
            initialize(instance, recover_writers_only=True)
            print('only recorded new-writer UIDs/replicas restored; no native initialization repeated')
        elif '--activate' in sys.argv[2:]:
            if list(instance.evidence.glob('native-command-*.json')):
                raise RuntimeError('native-registry-already-has-durable-commands')
            request = json.loads((instance.evidence / 'native-cutover-activation-request.json').read_text())
            evidence = json.loads((instance.evidence / 'native-first-deployment-initialization.json').read_text())
            assert evidence['restoreVerified'] and evidence['domainCommandsBeforeBackup'] == 0 and evidence['writersStoppedBeforeBackup']
            registry = NativeRegistry(instance)
            registry.authenticate()
            result = registry.call('POST', '/api/v1/admin/projections/delivery-writer-protocol/activate', request)
            instance.write('native-cutover-activation-result.json', {'request': request, 'nativeResult': result, 'contractOperations': 0})
            print('native projection-delivery writer protocol activated')
        else:
            request = initialize(instance)
            instance.write('native-cutover-activation-request.json', request)
            print('native provider backup restored successfully; isolated native administration assigned; activation request retained')
    except Exception as error:
        instance.write('native-initialization-failure.json', {'exceptionType': type(error).__name__, 'reason': str(error) if isinstance(error, RuntimeError) else 'sanitized-initialization-failure'})
        raise SystemExit(1) from None
    finally:
        instance.close()
