#!/usr/bin/env python3
"""Verify operational-key installation using public metadata and native health."""
import base64
from contextlib import ExitStack
from datetime import datetime, timezone
import http.client
import json
from pathlib import Path
import sys

from capture_native_installation import scan_credentials
from deploy_native_registry import record_provenance
from install_isolated_registry import Installer, NS, REGISTER, ROOT, canonical, kjson, kube, sha, snapshot
from provision_operational_hmac import COMPONENT, DATA_SECRET, ProviderMetadata, component_manifest, forward, validate_plan, validate_version, verify_permissions
from verify_inventory_registry_reads import verify

ORIGINAL = Path('/home/administrator/hexalith-secrets-evidence/ext-secrets-1/native-registry-inventory-20261017T082852516535Z')


def health(port, path):
    connection = http.client.HTTPConnection('127.0.0.1', port, timeout=20)
    try:
        connection.request('GET', path)
        response = connection.getresponse()
        raw = response.read(1048577)
        return {'httpStatus': response.status, 'publicResponse': json.loads(raw) if raw[:1] == b'{' else None}
    finally:
        connection.close()


def verify_public_binding(plan, allowed, component, expected_uid):
    validate_plan(plan)
    expected = {'permittedReferences': [{key: item[key] for key in ['tenantId', 'purpose', 'secretReference', 'secretField']} for item in plan]}
    if any(item['phase'] != 'MetadataVerified' for item in plan) or allowed != expected:
        raise RuntimeError('installed-allowlist-does-not-match-verified-keys')
    definition = component_manifest()
    if (not expected_uid or component.get('kind') != 'Component' or component.get('apiVersion') != definition['apiVersion']
            or component.get('metadata', {}).get('name') != COMPONENT or component['metadata'].get('namespace') != NS
            or component['metadata'].get('uid') != expected_uid or component.get('spec') != definition['spec']
            or component.get('scopes') != definition['scopes']):
        raise RuntimeError('installed-provider-component-changed')


def verify_health(state):
    expected = {'nativeEventStoreReadiness': 200, 'inventoryLiveness': 200, 'inventoryHealth': 200, 'inventoryReadiness': 503}
    if any(state.get(key, {}).get('httpStatus') != status for key, status in expected.items()):
        raise RuntimeError('installed-native-or-inventory-health-unexpected')
    body = state['inventoryHealth'].get('publicResponse')
    if (not isinstance(body, dict) or body.get('status') != 'InstalledUnbound' or body.get('qualificationPassed') is not False
            or body.get('dependencyAvailable') is not False or body.get('independentProfilePresent') is not False
            or body.get('exactPermittedReferenceCount') != 4 or body.get('missingHmacInput') != 'runtime.currentInventoryAuthorityReference'):
        raise RuntimeError('inventory-unbound-health-unexpected')
    provider = state.get('openbao', [])
    if len(provider) != 3 or any(item.get('sealed') is not False or item.get('initialized') is not True or item.get('version') != '2.6.2' for item in provider):
        raise RuntimeError('provider-health-unexpected')
    controllers = state.get('controllers', [])
    expected_controllers = state.get('expectedControllers')
    actual_controllers = sorted('/'.join((item.get('kind', ''), item.get('namespace', ''), item.get('name', ''))) for item in controllers)
    if (not isinstance(expected_controllers, list) or actual_controllers != expected_controllers
            or {item.get('namespace') for item in controllers} != {'dapr-system', 'hexalith-memories', NS}
            or any(type(item.get('desired')) is not int or type(item.get('ready')) is not int
                   or item['desired'] < 0 or item['ready'] != item['desired'] for item in controllers)):
        raise RuntimeError('controller-health-unexpected')


def capture(installer):
    before = json.loads((installer.evidence / 'preservation-before.json').read_text())
    after = snapshot()
    installer.write('preservation-after.json', after)
    intended = {'ConfigMap/' + NS + '/ext-secrets-1-inventory-runtime', 'ConfigMap/' + NS + '/ext-secrets-1-inventory-permitted-references',
                'Deployment/' + NS + '/inventory'}
    changed = [key for key, value in before.items() if after.get(key) != value and key not in intended]
    preserved_ids = all(after.get(key, {}).get('uid') == value['uid'] for key, value in before.items())
    old_bao = json.loads((installer.evidence / 'openbao-preservation-before.json').read_text())
    current_bao = installer.openbao_snapshot()
    installer.write('openbao-preservation-after.json', current_bao)
    changed_bao = [group + '/' + key for group, items in old_bao.items() for key, value in items.items() if current_bao[group].get(key) != value]
    settled = json.loads((installer.evidence / 'settled-before.json').read_text())
    register_same = sha(REGISTER.read_bytes()) == settled['agentsRegisterSha256']
    original_config = json.loads((ORIGINAL / 'runtime-reference.json').read_text())
    runtime = json.loads((installer.evidence / 'runtime-reference.json').read_text())
    normalized = json.loads(json.dumps(runtime))
    runtime_changes = ['providerBinding', 'hmacCredentialReference', 'callTimeout']
    for key in runtime_changes:
        normalized['runtime'][key] = original_config['runtime'][key]
    complete_document_preserved = normalized == original_config
    from qualify import read_public_configuration
    read_public_configuration(installer.evidence / 'runtime-reference.json')
    installed = kjson('get', 'deployment', 'inventory', '-n', NS)
    previous = next(item for item in json.loads((ORIGINAL / 'installed-public-manifests.json').read_text())['items']
                    if item['kind'] == 'Deployment' and item['metadata']['name'] == 'inventory')
    expected_spec = previous['spec']
    expected_spec['template']['spec']['containers'][0]['image'] = installed['spec']['template']['spec']['containers'][0]['image']
    permitted = json.loads((installer.evidence / 'permitted-references.json').read_text())
    expected_spec['template'].setdefault('metadata', {}).setdefault('annotations', {})['hexalith.io/ext-secrets-1-binding-sha256'] = sha(canonical({'runtime': runtime, 'allowlist': permitted}))
    only_image_changed = expected_spec == installed['spec']
    keys = json.loads((installer.evidence / 'operational-key-plan.json').read_text())
    references = json.loads((installer.evidence / 'installation-references.json').read_text())
    component_identity = references.get('newResources', {}).get('Component/' + NS + '/' + COMPONENT, {}).get('uid')
    verify_public_binding(keys, permitted, kjson('get', 'component', COMPONENT, '-n', NS), component_identity)
    maps_match = all(kjson('get', 'configmap', name, '-n', NS)['data'] == data for name, data in [
        ('ext-secrets-1-inventory-runtime', {'runtime-reference.json': canonical(runtime).decode()}),
        ('ext-secrets-1-inventory-permitted-references', {'permitted-references.json': canonical(permitted).decode()})])
    spec = installed['spec']['template']['spec']
    data_not_mounted = all(item.get('secret', {}).get('secretName') != DATA_SECRET for item in spec['volumes'])
    passed = not changed and not changed_bao and preserved_ids and register_same and complete_document_preserved and only_image_changed and maps_match and data_not_mounted
    installer.write('preservation-result.json', {'existingKubernetesObjectCount': len(before), 'changedProtectedKubernetesReferences': changed,
        'allOriginalKubernetesUidsPreserved': preserved_ids, 'allowedIsolatedBindingChanges': sorted(intended),
        'changedExistingOpenBaoReferences': changed_bao, 'agentsRegisterUnchanged': register_same,
        'completeRuntimePreservedOutsideInstalledProviderFields': complete_document_preserved, 'runtimeChangedFields': runtime_changes,
        'onlyInventoryImageChanged': only_image_changed, 'installedPublicConfigurationMatches': maps_match,
        'inventoryHasNoProviderDataCredentialMount': data_not_mounted, 'redisAofSafeguardsAndScopedAclResourcesUnchanged':
            all(after[key] == value for key, value in before.items() if NS in key and ('registry-state' in key or 'redis' in key)),
        'allProtectedObjectsPreserved': passed, 'foundationReferencesReused': settled['foundationReferences']})
    if not passed:
        raise RuntimeError('operational-installation-preservation-failed')
    with ExitStack() as stack:
        idp = stack.enter_context(forward('service/registry-idp', NS, 8443))
        native = stack.enter_context(forward('deployment/inventory', NS, 3500))
        inventory = stack.enter_context(forward('service/inventory', NS, 8080))
        eventstore = stack.enter_context(forward('service/eventstore', NS, 8080))
        provider = stack.enter_context(forward('service/hexalith-keys-active', 'openbao', 8200))
        tenants = verify(installer, native, idp)
        originals = json.loads((ORIGINAL / 'native-enrollment-receipts.json').read_text())['tenants']
        if any(item['sourceStreamSha256'] != original['sourceStreamSha256'] for item, original in zip(tenants['tenantStreams'], originals)):
            raise RuntimeError('original-native-enrollment-changed')
        reader = ProviderMetadata(installer, provider)
        keys = json.loads((installer.evidence / 'operational-key-plan.json').read_text())
        observations = []
        for item in keys:
            metadata = reader.read(item['secretReference'])
            if metadata is None or validate_version(item, metadata)[0] != item['secretVersion'] or sha(canonical(metadata)) != item['providerMetadataSha256']:
                raise RuntimeError('operational-key-metadata-changed')
            observations.append({'providerReference': item['providerReference'], 'secretVersion': item['secretVersion'], 'metadata': metadata})
        installer.write('provider-native-metadata-readback.json', {'keys': observations, 'tlsVerified': True, 'readTarget': 'hexalith-keys-active.openbao.svc', 'keyDataReads': 0})
        state = {'observedAt': datetime.now(timezone.utc).isoformat(), 'nativeEventStoreReadiness': health(eventstore, '/ready'),
                 'inventoryLiveness': health(inventory, '/alive'), 'inventoryReadiness': health(inventory, '/ready'),
                 'inventoryHealth': health(inventory, '/health'), 'openbao': [], 'controllers': []}
    for index in range(3):
        script = 'export BAO_ADDR=https://127.0.0.1:8200 BAO_CACERT=/openbao/userconfig/openbao-server-tls/ca.crt BAO_TLS_SERVER_NAME=hexalith-keys.openbao.svc.cluster.local BAO_SKIP_VERIFY=false; exec bao status -format=json'
        value = json.loads(kube('exec', '-n', 'openbao', 'hexalith-keys-' + str(index), '--', 'sh', '-c', script).stdout)
        state['openbao'].append({key: value[key] for key in ['sealed', 'initialized', 'version']})
    for namespace in ['dapr-system', 'hexalith-memories', NS]:
        for item in kjson('get', 'deployment,statefulset', '-n', namespace)['items']:
            state['controllers'].append({'namespace': namespace, 'name': item['metadata']['name'], 'kind': item['kind'],
                                         'desired': item['spec']['replicas'], 'ready': item.get('status', {}).get('readyReplicas', 0)})
    state['expectedControllers'] = sorted(key for key in after if key.split('/')[0] in ('Deployment', 'StatefulSet') and key.split('/')[1] in ('dapr-system', 'hexalith-memories', NS))
    state.update({'qualificationPassed': False, 'dependencyAvailable': False, 'contractOperations': 0, 'providerDataReads': 0})
    verify_health(state)
    installer.write('health.json', state)
    manifests = kjson('get', 'deployment,statefulset,configmap,component,configuration,serviceaccount,service,networkpolicy', '-n', NS)['items']
    for value in manifests:
        value.pop('status', None)
        value['metadata'] = {key: entry for key, entry in value['metadata'].items() if key in ['name', 'namespace', 'uid', 'resourceVersion', 'labels', 'annotations', 'creationTimestamp']}
    installer.write('installed-public-manifests.json', {'items': manifests, 'secretObjectsIncluded': False})
    installer.write('installation-references.json', {'newResources': {key: value for key, value in after.items() if key not in before},
        'updatedIsolatedBindingResources': {key: after[key] for key in sorted(intended)}, 'reusedFoundationResources': settled['foundationReferences']})
    verify_permissions(installer, keys)
    scan_credentials(installer)
    token = installer.secret_field(DATA_SECRET, 'token').encode()
    needles = (token, base64.b64encode(token))
    files = [path for path in installer.evidence.rglob('*') if path.is_file()]
    files += [path for path in (ROOT / 'eng/ext-secrets-1').rglob('*') if path.is_file() and path.suffix in ('.py', '.json', '.md')]
    if any(any(needle in path.read_bytes() for needle in needles) for path in files):
        raise RuntimeError('provider-data-credential-exposure-check-failed')
    installer.write('data-credential-exposure-check.json', {'passed': True, 'filesChecked': len(files), 'literalAndBase64Checked': True})
    record_provenance(installer)
    return state


if __name__ == '__main__':
    instance = Installer(sys.argv[1])
    try:
        capture(instance)
        print('operational provider metadata, native health and preservation verified')
    except Exception as error:
        instance.write('capture-failure.json', {'exceptionType': type(error).__name__, 'reason': str(error) if type(error) is RuntimeError else 'sanitized-capture-failure'})
        raise SystemExit(1) from None
    finally:
        instance.close()
