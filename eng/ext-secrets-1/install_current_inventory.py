#!/usr/bin/env python3
"""Install a read-only current-inventory host; absent independent trust/mappings remain unavailable."""
import base64
import json
from pathlib import Path
import subprocess
import sys
import hmac

from install_isolated_registry import Installer, NS, canonical, env_secret, kube, kjson, require_creation_identity, sha
from preload_isolated_image import preload
from provision_registry_identity import ISSUER
from observe_provider_metadata import observe
from native_journal import durable_write


def application_request(token, *args, allowed=(0,)):
    script = ('IFS= read -r BAO_TOKEN || exit 65; export BAO_TOKEN; export BAO_ADDR=https://127.0.0.1:8200 '
              'BAO_CACERT=/openbao/userconfig/openbao-server-tls/ca.crt BAO_TLS_SERVER_NAME=hexalith-keys.openbao.svc.cluster.local BAO_SKIP_VERIFY=false; exec bao "$@"')
    return kube('exec', '-i', '-n', 'openbao', 'hexalith-keys-0', '--', 'sh', '-c', script, 'sh', *args,
                data=bytes(token) + b'\n', allowed=allowed)


def secret_identity(name):
    fields = kube('get', 'secret', name, '-n', NS, '--ignore-not-found', '-o',
        'jsonpath={.metadata.uid}{" "}{.metadata.resourceVersion}').stdout.decode().split()
    if not fields:
        return None
    if len(fields) != 2:
        raise RuntimeError('credential-secret-observation-malformed')
    return {'uid': fields[0], 'resourceVersion': fields[1]}


def read_pinned_credential_secret(name, identity):
    expected = require_creation_identity(identity)
    observed = kube('get', 'secret', name, '-n', NS, '--ignore-not-found', '-o',
                    'jsonpath={.data.token}{" "}{.metadata.uid}{" "}{.metadata.resourceVersion}').stdout.split()
    if len(observed) != 3:
        raise RuntimeError('credential-secret-observation-malformed')
    encoded, uid, version = observed
    if {'uid': uid.decode(), 'resourceVersion': version.decode()} != expected:
        raise RuntimeError('original-credential-secret-identity-changed')
    return bytearray(base64.b64decode(encoded, validate=True))


def delete_matching_credential_secret(name, token=None, *, identity=None):
    expected = require_creation_identity(identity)
    observed = secret_identity(name)
    if observed is None:
        return
    if observed != expected:
        raise RuntimeError('original-credential-secret-identity-changed-no-delete')
    if token is not None:
        decoded = read_pinned_credential_secret(name, expected)
        try:
            if not hmac.compare_digest(decoded, bytes(token)):
                raise RuntimeError('original-credential-secret-value-mismatch-no-delete')
        finally:
            decoded[:] = b'\0' * len(decoded)
    kube('delete', '--raw', '/api/v1/namespaces/' + NS + '/secrets/' + name, '-f', '-',
        data=canonical({'apiVersion': 'v1', 'kind': 'DeleteOptions', 'preconditions': expected}))


def cleanup_journaled_credential(installer, path, name, token=None):
    """Resume acknowledged cleanup only; unknown issuance/revocation/deletion never repeats an effect."""
    journal = json.loads(path.read_text())
    if journal.get('revocationPhase') != 'Acknowledged':
        if journal.get('revocationPhase') == 'Started' or not journal.get('accessor'):
            raise RuntimeError('original-credential-revocation-or-issuance-needs-reconciliation')
        recovered = None
        try:
            if token is None:
                recovered = read_pinned_credential_secret(name, journal.get('secretIdentity'))
                lookup = json.loads(application_request(recovered, 'token', 'lookup', '-format=json').stdout)['data']
                if lookup.get('accessor') != journal['accessor']:
                    raise RuntimeError('original-credential-accessor-changed-no-revocation')
                token = recovered
            journal['revocationPhase'] = 'Started'
            durable_write(path, journal)
            application_request(token, 'token', 'revoke', '-self')
            journal.update({'revocationPhase': 'Acknowledged', 'phase': 'Revoked'})
            durable_write(path, journal)
        finally:
            if recovered is not None:
                recovered[:] = b'\0' * len(recovered)
    if journal.get('secretRemovalPhase') == 'Acknowledged':
        return journal
    identity = journal.get('secretIdentity')
    if identity is None:
        if journal.get('secretCreationPhase') != 'NotStarted':
            raise RuntimeError('original-secret-creation-identity-required-for-cleanup')
    else:
        require_creation_identity(identity)
        if journal.get('secretRemovalPhase') == 'Started':
            if secret_identity(name) is not None:
                raise RuntimeError('original-secret-removal-outcome-needs-reconciliation')
        else:
            journal['secretRemovalPhase'] = 'Started'
            durable_write(path, journal)
            delete_matching_credential_secret(name, identity=identity)
    journal.update({'secretRemovalPhase': 'Acknowledged', 'phase': 'RevokedAndOwnedSecretRemoved'})
    durable_write(path, journal)
    return journal


def create_metadata_credential(installer):
    policy_name = 'ext-secrets-1-inventory-metadata-read'
    names = json.loads(installer.bao('policy', 'list', '-format=json').stdout)
    assert policy_name not in names
    policy = ('path "ext-secrets-1-isolated/metadata" { capabilities = ["list"] }\n'
              'path "ext-secrets-1-isolated/metadata/*" { capabilities = ["read", "list"] }\n'
              'path "auth/token/lookup-self" { capabilities = ["read"] }\n'
              'path "auth/token/renew-self" { capabilities = ["update"] }\n')
    policy += 'path "auth/token/revoke-self" { capabilities = ["update"] }\n'
    policy += 'path "sys/capabilities-self" { capabilities = ["update"] }\n'
    if kube('get', 'secret', 'ext-secrets-1-inventory-metadata-token', '-n', NS, '--ignore-not-found', '-o', 'name').stdout.strip():
        raise RuntimeError('metadata-credential-secret-already-exists')
    installer.bao('policy', 'write', policy_name, '-', data=policy.encode())
    try:
        created = installer.bao('write', '-format=json', 'auth/token/create-orphan', '-', data=canonical({
            'policies': [policy_name], 'no_default_policy': True, 'display_name': 'ext-secrets-1-inventory-metadata',
            'ttl': '24h', 'explicit_max_ttl': '168h', 'renewable': True}))
    except Exception:
        installer.bao('policy', 'delete', policy_name)
        raise
    result = json.loads(created.stdout)
    application_token = bytearray(result['auth']['client_token'].encode())
    accessor = result['auth']['accessor']
    duration = result['auth']['lease_duration']
    del result
    journal = {'phase': 'Issued', 'accessor': accessor, 'policy': policy_name, 'secretReference': NS + '/Secret/ext-secrets-1-inventory-metadata-token#token'}
    try:
        durable_write(installer.evidence / 'metadata-credential-provisioning.json', journal)
        installer.secret('ext-secrets-1-inventory-metadata-token', {'token': bytes(application_token).decode()})
        journal['phase'] = 'Stored'
        durable_write(installer.evidence / 'metadata-credential-provisioning.json', journal)
        return application_token, accessor, duration, policy
    except Exception:
        # Cleanup only this attempt's new token. The accessor survives even if
        # Secret publication and subsequent cleanup both fail.
        journal['phase'] = 'CleanupPending'
        try:
            durable_write(installer.evidence / 'metadata-credential-provisioning.json', journal)
        except OSError:
            pass  # A failed evidence filesystem must not suppress revocation.
        try:
            application_request(application_token, 'token', 'revoke', '-self')
            # Publication may have succeeded before the API response was lost.
            # Delete only a Secret containing this newly issued token and pin
            # both UID/resourceVersion in the native deletion precondition.
            delete_matching_credential_secret('ext-secrets-1-inventory-metadata-token', application_token)
            installer.bao('policy', 'delete', policy_name)
            journal['phase'] = 'RevokedAndPolicyRemoved'
            durable_write(installer.evidence / 'metadata-credential-provisioning.json', journal)
        finally:
            application_token[:] = b'\0' * len(application_token)
        raise


def install(installer, image):
    receipts = json.loads((installer.evidence / 'native-enrollment-receipts.json').read_text())['tenants']
    assert len(receipts) == 2 and all(item['currentEnrollment'] == 'Active' for item in receipts)
    policy_name = 'ext-secrets-1-inventory-metadata-read'
    application_token, accessor, duration, policy = create_metadata_credential(installer)
    try:
        def application(*args, allowed=(0,)):
            return application_request(application_token, *args, allowed=allowed)
        for forbidden in ['ext-secrets-1-isolated/data/*', 'secret/data/*', 'sys/mounts', 'sys/policies/acl/*']:
            assert json.loads(application('token', 'capabilities', '-format=json', forbidden).stdout) == ['deny']
        observation = observe(installer)
        observation.update({'policyReference': 'openbao/policy/' + policy_name, 'policySha256': sha(policy.encode()),
                            'credentialAccessor': accessor, 'credentialInitialTtlSeconds': duration,
                            'credentialMaximumLifetimeSeconds': 604800})
        installer.write('provider-metadata-observation.json', observation)
    finally:
        application_token[:] = b'\0' * len(application_token)
    configuration_path = installer.evidence / 'runtime-reference.json'
    original = Path('/home/administrator/hexalith-secrets-evidence/ext-secrets-1/provider-inventory-installation-20261017T072855253627Z/runtime-reference.json')
    config = json.loads(original.read_bytes())
    config['runtime']['tenantA'] = receipts[0]['tenantId']
    config['runtime']['tenantB'] = receipts[1]['tenantId']
    tenant_host = kjson('get', 'deployment', 'tenants', '-n', NS)
    config['runtime']['tenantInventoryReference'] = NS + '/Deployment/tenants@uid:' + tenant_host['metadata']['uid']
    # The registry IdP is not independent secrets trust; keep all those unknown fields null.
    for field in ['currentInventoryAuthorityReference', 'profileReference', 'currentRevocationReference', 'publicTrustReference',
                  'issuer', 'audience', 'profileVersion', 'profileNotBefore', 'profileValidUntil', 'providerBinding', 'hmacCredentialReference']:
        assert config['runtime'][field] is None
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import qualify
    configuration_path.write_bytes(canonical(config) + b'\n')
    qualify.read_public_configuration(configuration_path)
    for name, field in [('app', 'inventory-app'), ('dapr', 'inventory-dapr')]:
        installer.secret('ext-secrets-1-inventory-' + name + '-token', {'token': installer.secret_field('ext-secrets-1-registry-channels', field)})
    installer.create({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'ext-secrets-1-inventory-runtime'},
                      'data': {'runtime-reference.json': configuration_path.read_text()}})
    installer.create({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'ext-secrets-1-inventory-permitted-references'},
                      'data': {'permitted-references.json': json.dumps({'permittedReferences': []})}})
    installer.create({'apiVersion': 'dapr.io/v1alpha1', 'kind': 'Configuration', 'metadata': {'name': 'ext-secrets-1-inventory'},
                      'spec': {'accessControl': {'defaultAction': 'deny', 'trustDomain': 'cluster.local', 'policies': []}}})
    preload(installer, image)
    env = [env_secret('APP_API_TOKEN', 'ext-secrets-1-inventory-app-token', 'token'), env_secret('DAPR_API_TOKEN', 'ext-secrets-1-inventory-dapr-token', 'token')]
    env += [{'name': name, 'value': value} for name, value in {
        'ASPNETCORE_ENVIRONMENT': 'Production', 'ASPNETCORE_HTTP_PORTS': '8080', 'Logging__LogLevel__Default': 'Warning',
        'Inventory__RuntimeReferenceFile': '/var/run/inventory-runtime/runtime-reference.json',
        'Inventory__PermittedReferencesFile': '/var/run/inventory-allowlist/permitted-references.json',
        'Inventory__ProviderCaFile': '/var/run/openbao-ca/ca.pem', 'Inventory__RegistryCaFile': '/var/run/registry-idp-ca/ca.pem',
        'Inventory__ProviderCredentialFile': '/var/run/inventory-metadata/token', 'Inventory__RegistryCredentialFile': '/var/run/inventory-registry/client-secret',
        'Inventory__RegistryTokenEndpoint': ISSUER + '/protocol/openid-connect/token', 'SSL_CERT_FILE': '/var/run/registry-idp-ca/ca.pem'}.items()]
    volumes = [
        {'name': 'runtime', 'configMap': {'name': 'ext-secrets-1-inventory-runtime'}},
        {'name': 'allowlist', 'configMap': {'name': 'ext-secrets-1-inventory-permitted-references'}},
        {'name': 'provider-ca', 'configMap': {'name': 'ext-secrets-1-openbao-ca'}},
        {'name': 'registry-ca', 'configMap': {'name': 'ext-secrets-1-registry-idp-ca'}},
        {'name': 'metadata', 'secret': {'secretName': 'ext-secrets-1-inventory-metadata-token', 'defaultMode': 288}},
        {'name': 'registry-credential', 'secret': {'secretName': 'ext-secrets-1-registry-oidc-inventory', 'defaultMode': 288}}]
    mounts = [{'name': name, 'mountPath': path, 'readOnly': True} for name, path in [
        ('runtime', '/var/run/inventory-runtime'), ('allowlist', '/var/run/inventory-allowlist'), ('provider-ca', '/var/run/openbao-ca'),
        ('registry-ca', '/var/run/registry-idp-ca'), ('metadata', '/var/run/inventory-metadata'), ('registry-credential', '/var/run/inventory-registry')]]
    installer.deployment('inventory', image, 8080, env, [], 1654, 'ext-secrets-1-inventory', volumes, mounts,
                         {'dapr.io/enabled': 'true', 'dapr.io/app-id': 'ext-secrets-1-inventory', 'dapr.io/app-port': '8080',
                          'dapr.io/app-token-secret': 'ext-secrets-1-inventory-app-token', 'dapr.io/api-token-secret': 'ext-secrets-1-inventory-dapr-token',
                          'dapr.io/config': 'ext-secrets-1-inventory'})
    installer.write('inventory-host-installation.json', {'image': image, 'installedAppId': 'ext-secrets-1-inventory',
        'activeInventoryAuthorityReference': None, 'nativeTenantInventoryReference': config['runtime']['tenantInventoryReference'],
        'providerBinding': None, 'restrictedProviderDataCredentialReference': None, 'metadataCredentialReference': NS + '/Secret/ext-secrets-1-inventory-metadata-token#token',
        'registryReadCredentialReference': NS + '/Secret/ext-secrets-1-registry-oidc-inventory',
        'runtimeReference': str(configuration_path), 'independentSecretsTrustEstablished': False, 'exactPermittedReferenceCount': 0,
        'qualificationPassed': False, 'dependencyAvailable': False, 'contractOperations': 0})


if __name__ == '__main__':
    instance = Installer(sys.argv[1])
    instance.created = json.loads((instance.evidence / 'created-resource-references.json').read_text())
    try:
        install(instance, sys.argv[2])
        print('read-only inventory host installed; absent independent prerequisites remain null and unavailable')
    except Exception as error:
        instance.write('inventory-installation-failure.json', {'exceptionType': type(error).__name__, 'reason': str(error) if isinstance(error, RuntimeError) else 'sanitized-installation-failure'})
        raise SystemExit(1) from None
    finally:
        instance.close()
