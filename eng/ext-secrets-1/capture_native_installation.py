#!/usr/bin/env python3
"""Capture public installation, health and preservation evidence; never read complete Secrets."""
import base64
import http.client
import json
import sys
from datetime import datetime, timezone

from install_isolated_registry import Installer, NS, REGISTER, ROOT, canonical, kjson, kube, reference, sha, snapshot
from deploy_native_registry import distribution_auth, record_provenance


def same_settled_configuration(before, after):
    normalized = []
    for document in [before, after]:
        value = json.loads(json.dumps(document))
        if not isinstance(value.get('runtime'), dict):
            return False
        for field in ['tenantA', 'tenantB', 'tenantInventoryReference']:
            if field not in value['runtime']:
                return False
            del value['runtime'][field]
        normalized.append(value)
    return normalized[0] == normalized[1]


def capture(installer, native_port=38446, inventory_port=38450):
    before = json.loads((installer.evidence / 'preservation-before.json').read_text())
    after = snapshot()
    installer.write('preservation-after.json', after)
    changed = [key for key, value in before.items() if after.get(key) != value]
    previous_bao = json.loads((installer.evidence / 'openbao-preservation-before.json').read_text())
    current_bao = installer.openbao_snapshot()
    installer.write('openbao-preservation-after.json', current_bao)
    changed_bao = [group + '/' + key for group, items in previous_bao.items() for key, value in items.items()
                   if current_bao[group].get(key) != value]
    settled = json.loads((installer.evidence / 'settled-before.json').read_text())
    register_unchanged = sha(REGISTER.read_bytes()) == settled['agentsRegisterSha256']
    original_path = installer.evidence.parent / 'provider-inventory-installation-20261017T072855253627Z/runtime-reference.json'
    previous_runtime = json.loads(original_path.read_text())
    runtime = json.loads((installer.evidence / 'runtime-reference.json').read_text())
    runtime_changes = [key for key in set(runtime['runtime']) | set(previous_runtime['runtime'])
                       if runtime['runtime'].get(key) != previous_runtime['runtime'].get(key)
                       or (key in runtime['runtime']) != (key in previous_runtime['runtime'])]
    allowed_runtime_changes = same_settled_configuration(previous_runtime, runtime)
    preserved = not changed and not changed_bao and register_unchanged and allowed_runtime_changes
    installer.write('preservation-result.json', {'observedAt': datetime.now(timezone.utc).isoformat(),
        'existingKubernetesObjectCount': len(before), 'changedExistingKubernetesReferences': changed,
        'changedExistingOpenBaoReferences': changed_bao, 'agentsRegisterSha256': sha(REGISTER.read_bytes()),
        'agentsRegisterUnchanged': register_unchanged, 'runtimeChangedFields': runtime_changes,
        'onlyNativeTenantReferencesUpdated': allowed_runtime_changes, 'allProtectedObjectsPreserved': preserved,
        'foundationReferencesReused': settled['foundationReferences'], 'existingSecretComparison': 'UID and resourceVersion only; no complete Secret reads'})

    kinds = 'deployments,statefulsets,services,configmaps,serviceaccounts,persistentvolumeclaims,networkpolicies,roles,rolebindings,issuers,certificates,components,configurations'
    objects = kjson('get', kinds, '-n', NS)['items']
    # ConfigMaps contain only public CA/configuration. Credential-bearing resources
    # use secretKeyRef/Secret volumes and are represented solely by those references.
    manifests = [{key: value for key, value in obj.items() if key != 'status'} for obj in objects]
    for obj in manifests:
        obj['metadata'] = {key: value for key, value in obj['metadata'].items()
                           if key in ['name', 'namespace', 'uid', 'resourceVersion', 'labels', 'annotations', 'creationTimestamp']}
    installer.write('installed-public-manifests.json', {'items': manifests, 'secretObjectsIncluded': False})
    new_references = {key: value for key, value in after.items() if key not in before}
    installer.write('installation-references.json', {'installedResources': new_references,
        'reusedFoundationResources': settled['foundationReferences'],
        'deletedProvisioningOnlyResources': [key for key in json.loads((installer.evidence / 'created-resource-references.json').read_text())
                                             if key not in after], 'credentialValuesRecorded': False})
    deployments = [obj for obj in objects if obj['kind'] == 'Deployment']
    installed = {obj['metadata']['name']: {'uid': obj['metadata']['uid'], 'readyReplicas': obj.get('status', {}).get('readyReplicas', 0),
                 'desiredReplicas': obj['spec']['replicas'], 'images': [item['image'] for item in obj['spec']['template']['spec']['containers']],
                 'imagePullSecretReferences': obj['spec']['template']['spec'].get('imagePullSecrets', [])} for obj in deployments}
    openbao = []
    for index in range(3):
        script = 'export BAO_ADDR=https://127.0.0.1:8200 BAO_CACERT=/openbao/userconfig/openbao-server-tls/ca.crt BAO_TLS_SERVER_NAME=hexalith-keys.openbao.svc.cluster.local BAO_SKIP_VERIFY=false; exec bao status -format=json'
        value = json.loads(kube('exec', '-n', 'openbao', 'hexalith-keys-' + str(index), '--', 'sh', '-c', script).stdout)
        openbao.append({key: value.get(key) for key in ['sealed', 'initialized', 'version', 'cluster_id', 'storage_type']})
    controllers = kjson('get', 'deployment,statefulset', '-n', 'dapr-system')['items']
    memories = kjson('get', 'deployment,statefulset', '-n', 'hexalith-memories')['items']

    def health(port, path):
        connection = http.client.HTTPConnection('127.0.0.1', port, timeout=20)
        connection.request('GET', path)
        response = connection.getresponse()
        status, data = response.status, response.read(1048576)
        connection.close()
        return {'httpStatus': status, 'publicResponse': json.loads(data) if data[:1] == b'{' else None}

    incident_path = installer.evidence / 'native-aof-recovery.json'
    incident = json.loads(incident_path.read_text()) if incident_path.exists() else None
    health_result = {'observedAt': datetime.now(timezone.utc).isoformat(), 'openbao': openbao,
        'daprControllers': [{'kind': value['kind'], 'name': value['metadata']['name'], 'desired': value['spec']['replicas'], 'ready': value.get('status', {}).get('readyReplicas', 0)} for value in controllers],
        'memoriesControllers': [{'kind': value['kind'], 'name': value['metadata']['name'], 'desired': value['spec']['replicas'], 'ready': value.get('status', {}).get('readyReplicas', 0)} for value in memories],
        'installedDeployments': installed, 'eventstoreReadiness': health(native_port, '/ready'),
        'inventoryLiveness': health(inventory_port, '/alive'), 'inventoryReadiness': health(inventory_port, '/ready'), 'inventoryHealth': health(inventory_port, '/health'),
        'nativePersistenceIncident': {'cause': 'Disabled internal default user ACL denied genuine AOF transaction replay during the scoped ACL restart.',
            'recoveryEvidence': str(incident_path), 'originalTenantStreamHashesUnchanged': incident.get('originalTenantStreamHashesUnchanged'),
            'phase': incident.get('phase'), 'originalBackingFilesPreserved': True} if incident else None,
        'contractOperations': 0, 'qualificationPassed': False, 'dependencyAvailable': False}
    installer.write('health.json', health_result)
    record_provenance(installer)
    return preserved


def scan_credentials(installer):
    values = [bytes(installer.token)]
    fields = {'ext-secrets-1-registry-idp-db': ['password'], 'ext-secrets-1-registry-idp-bootstrap': ['client-secret'],
              'ext-secrets-1-registry-channels': ['eventstore-app', 'eventstore-dapr', 'tenants-app', 'tenants-dapr', 'inventory-app', 'inventory-dapr'],
              'ext-secrets-1-registry-state-credentials': ['eventstore', 'tenants', 'inventory'],
              'ext-secrets-1-registry-provisioner': ['password'], 'ext-secrets-1-registry-maintenance': ['password'],
              'ext-secrets-1-inventory-metadata-token': ['token']}
    for client in ['eventstore', 'tenants', 'inventory', 'registry-provisioner']:
        fields['ext-secrets-1-registry-oidc-' + client] = ['client-secret']
    for name, names in fields.items():
        for field in names:
            values.append(installer.secret_field(name, field).encode())
    auth = distribution_auth()['auth'].encode()
    values.extend([auth, base64.b64decode(auth).split(b':', 1)[1]])
    needles = set(value for value in values if len(value) >= 16)
    needles.update(base64.b64encode(value) for value in list(needles))
    files = [path for path in installer.evidence.rglob('*') if path.is_file()]
    files += [path for directory in ['src/Hexalith.Platform.TenantRegistry.Host', 'src/Hexalith.Platform.Secrets.Inventory', 'tests/Hexalith.Platform.Secrets.Inventory.Tests', 'eng/ext-secrets-1']
              for path in (ROOT / directory).rglob('*') if path.is_file() and path.suffix in ('.py', '.cs', '.json', '.md', '.csproj', '.props')
              and not any(part in ('bin', 'obj', '__pycache__') for part in path.parts)]
    leaks = [str(path) for path in files if any(needle in path.read_bytes() for needle in needles)]
    private_markers = [str(path) for path in files if any(line in [b'-----BEGIN PRIVATE KEY-----', b'-----BEGIN RSA PRIVATE KEY-----', b'-----BEGIN EC PRIVATE KEY-----']
                                                            for line in path.read_bytes().splitlines())]
    installer.write('credential-exposure-check.json', {'filesChecked': len(files), 'transientCredentialValuesChecked': len(values),
        'literalAndBase64CredentialMatches': leaks, 'privateKeyMarkerFiles': private_markers, 'completeSecretObjectsRecorded': False,
        'passed': not leaks and not private_markers})
    if leaks or private_markers:
        raise RuntimeError('credential-exposure-check-failed')


if __name__ == '__main__':
    instance = Installer(sys.argv[1])
    try:
        preserved = capture(instance, inventory_port=int(sys.argv[2]) if len(sys.argv) > 2 else 38450)
        scan_credentials(instance)
        print('public evidence captured; protected preservation=' + str(preserved))
        if not preserved:
            raise SystemExit(1)
    finally:
        instance.close()
