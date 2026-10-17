#!/usr/bin/env python3
"""Provision only new operational HMAC keys and an unbound restricted Dapr store.

No HMAC operation, provider data read, enrollment mutation or qualification runs.
Key generation and CAS=0 writes stay inside the provider process/pod. Public
journals recover uncertain creation by exact metadata, never by replacing a key.
"""
from contextlib import ExitStack, contextmanager
from datetime import datetime, timedelta, timezone
import http.client
import json
from pathlib import Path
import re
import selectors
import socket
import ssl
import subprocess
import sys
import uuid

from install_isolated_registry import BAO_CA, BAO_NAME, Installer, KUBE, NS, canonical, kjson, kube, sha
from install_current_inventory import application_request, delete_matching_credential_secret
from native_journal import durable_write, journal_lock
from preload_isolated_image import preload
from verify_inventory_registry_reads import verify

TENANTS = ('01M54GTX47NH1PRB00T2YS5048', '01M54GTX479F4Y9A3MRW578C4R')
PURPOSES = ('TrustedEnvelope', 'ContentDigest')
MOUNT = 'ext-secrets-1-isolated'
COMPONENT = 'ext-secrets-1-hmac'
DATA_POLICY = 'ext-secrets-1-hmac-data-read'
DATA_SECRET = 'ext-secrets-1-hmac-data-token'


@contextmanager
def forward(resource, namespace, remote):
    process = subprocess.Popen(KUBE + ['port-forward', '-n', namespace, resource, ':' + str(remote), '--address=127.0.0.1'],
                               stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    selector = selectors.DefaultSelector()
    try:
        selector.register(process.stdout, selectors.EVENT_READ)
        for _ in range(4):
            if not selector.select(5):
                break
            match = re.search(rb'Forwarding from 127\.0\.0\.1:(\d+) ->', process.stdout.readline())
            if match:
                yield int(match[1])
                return
        raise RuntimeError('isolated-forward-unavailable')
    finally:
        selector.close()
        process.terminate()
        process.communicate(timeout=10)


class ProviderMetadata:
    def __init__(self, installer, port):
        self.token = installer.secret_field('ext-secrets-1-inventory-metadata-token', 'token')
        self.port = port
        self.context = ssl.create_default_context(cadata=kjson('get', 'configmap', 'ext-secrets-1-openbao-ca', '-n', NS)['data']['ca.pem'])

    def read(self, relative):
        connection = http.client.HTTPSConnection(BAO_NAME, 8200, timeout=20, context=self.context)
        try:
            connection.sock = self.context.wrap_socket(socket.create_connection(('127.0.0.1', self.port), timeout=20), server_hostname=BAO_NAME)
            connection.request('GET', '/v1/' + MOUNT + '/metadata/' + relative, headers={'X-Vault-Token': self.token})
            response = connection.getresponse()
            raw = response.read(1048577)
            if len(raw) > 1048576:
                raise RuntimeError('provider-metadata-response-too-large')
            value = json.loads(raw)
            if response.status == 404 and value == {'errors': []}:
                return None
            if response.status != 200 or not isinstance(value.get('data'), dict):
                raise RuntimeError('provider-metadata-read-denied-or-failed')
            return value['data']
        finally:
            connection.close()


def credential_request(token, *args, data=b''):
    script = ('IFS= read -r BAO_TOKEN || exit 65; export BAO_TOKEN; export BAO_ADDR=https://127.0.0.1:8200 '
              'BAO_CACERT=' + BAO_CA + ' BAO_TLS_SERVER_NAME=' + BAO_NAME + ' BAO_SKIP_VERIFY=false; exec bao "$@"')
    return kube('exec', '-i', '-n', 'openbao', 'hexalith-keys-0', '--', 'sh', '-c', script, 'sh', *args,
                data=bytes(token) + b'\n' + data)


def issue(installer, policy, display, lifetime, maximum=None, *, journal_path):
    if journal_path.exists():
        prior = json.loads(journal_path.read_text())
        if prior.get('phase') not in ('Revoked', 'RevokedAfterPublicationFailure'):
            raise RuntimeError('credential-issuance-recovery-required-no-repeat')
        durable_write(journal_path.with_name(journal_path.stem + '-prior-' + str(uuid.uuid4()) + '.json'), prior, create_only=True)
    attempt = str(uuid.uuid4())
    journal = {'phase': 'IssuanceStarted', 'policy': policy, 'issuanceId': attempt,
               'displayName': display + '-' + attempt, 'lifetime': lifetime, 'maximumLifetime': maximum or lifetime,
               'accessor': None}
    durable_write(journal_path, journal)
    token = None
    try:
        value = json.loads(installer.bao('write', '-format=json', 'auth/token/create-orphan', '-', data=canonical({
            'policies': [policy], 'no_default_policy': True, 'display_name': journal['displayName'],
            'ttl': lifetime, 'explicit_max_ttl': maximum or lifetime, 'renewable': True})).stdout)['auth']
        token = bytearray(value.pop('client_token').encode())
        lease = {key: value[key] for key in ['accessor', 'lease_duration', 'renewable']}
        journal.update({'phase': 'Issued', **lease})
        durable_write(journal_path, journal)
        return token, lease
    except Exception:
        journal['phase'] = 'IssuanceUnknown' if token is None else 'CleanupPending'
        if token is None:
            try:
                installer.bao('policy', 'delete', policy)
                journal['scopePolicyRemoved'] = True
            except Exception:
                journal['scopePolicyRemoved'] = False
        try:
            durable_write(journal_path, journal)
        except OSError:
            pass  # The durable IssuanceStarted record still forbids a blind repeat.
        if token is not None:
            try:
                application_request(token, 'token', 'revoke', '-self')
                journal['phase'] = 'Revoked'
                durable_write(journal_path, journal)
            finally:
                token[:] = b'\0' * len(token)
        raise


def policy_for(plan, provisioning=False):
    paths = []
    for item in plan:
        relative = item['secretReference']
        paths.append('path "' + MOUNT + '/data/' + relative + '" { capabilities = [' + ('"create"' if provisioning else '"read"') + '] }')
        if provisioning:
            paths.append('path "' + MOUNT + '/metadata/' + relative + '" { capabilities = ["update"] }')
    if provisioning:
        paths.append('path "sys/tools/random/32" { capabilities = ["update"] }')
    paths += ['path "auth/token/lookup-self" { capabilities = ["read"] }',
              'path "auth/token/renew-self" { capabilities = ["update"] }',
              'path "auth/token/revoke-self" { capabilities = ["update"] }',
              'path "sys/capabilities-self" { capabilities = ["update"] }']
    return '\n'.join(paths) + '\n'


def plan_keys():
    # PlatformHmacPurpose and PlatformHmacScope permit these tenant scopes.
    # SecurityObservation requires reserved-system enrollment, absent here.
    return [{'tenantId': tenant, 'purpose': purpose, 'secretReference': 'tenants/' + tenant + '/' + purpose + '/operational',
             'secretField': 'hmacKey', 'opaqueVersion': 'hmac-' + str(uuid.uuid4()), 'phase': 'Prepared'}
            for tenant in TENANTS for purpose in PURPOSES]


def validate_plan(plan):
    expected = {(tenant, purpose) for tenant in TENANTS for purpose in PURPOSES}
    if not isinstance(plan, list) or len(plan) != len(expected):
        raise RuntimeError('invalid-operational-key-plan')
    seen, versions = set(), set()
    for item in plan:
        if not isinstance(item, dict):
            raise RuntimeError('invalid-operational-key-plan')
        pair = (item.get('tenantId'), item.get('purpose'))
        version = item.get('opaqueVersion')
        if pair not in expected or pair in seen or not isinstance(version, str) or not re.fullmatch(r'hmac-[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}', version) or version in versions:
            raise RuntimeError('invalid-operational-key-plan')
        if item.get('secretReference') != 'tenants/' + pair[0] + '/' + pair[1] + '/operational' or item.get('secretField') != 'hmacKey' or item.get('phase') not in ('Prepared', 'CreationStarted', 'NativeCreated', 'MetadataVerified'):
            raise RuntimeError('invalid-operational-key-plan')
        if item['phase'] in ('NativeCreated', 'MetadataVerified') and (not isinstance(item.get('writeReceipt'), dict) or type(item['writeReceipt'].get('version')) is not int or item['writeReceipt']['version'] < 1 or not isinstance(item['writeReceipt'].get('created_time'), str)):
            raise RuntimeError('invalid-operational-key-plan')
        seen.add(pair)
        versions.add(version)


def validate_version(item, metadata):
    version = metadata.get('current_version')
    if type(version) is not int or version <= 0 or set(metadata.get('versions', {})) != {str(version)}:
        raise RuntimeError('unexpected-provider-native-version')
    native = metadata['versions'][str(version)]
    if native.get('destroyed') is not False or native.get('deletion_time') != '' or not isinstance(native.get('created_time'), str):
        raise RuntimeError('provider-native-version-not-current')
    if item.get('secretVersion') is not None and item['secretVersion'] != str(version):
        raise RuntimeError('provider-native-version-changed')
    return str(version), native['created_time']


def generate_inside_provider(token, relative):
    # The only output is the native KV2 write metadata. Random bytes never reach
    # this Python process, argv, a file, terminal or evidence.
    script = ('set -eu; IFS= read -r BAO_TOKEN; export BAO_TOKEN; '
              'export BAO_ADDR=https://127.0.0.1:8200 BAO_CACERT=' + BAO_CA + ' BAO_TLS_SERVER_NAME=' + BAO_NAME + ' BAO_SKIP_VERIFY=false; '
              'material=$(bao write -field=random_bytes sys/tools/random/32 format=base64); '
              'test "${#material}" -eq 44; '
              'printf \'{"options":{"cas":0},"data":{"hmacKey":"%s"}}\' "$material" | bao write -format=json "$1" -; unset material')
    result = kube('exec', '-i', '-n', 'openbao', 'hexalith-keys-0', '--', 'sh', '-c', script, 'sh', MOUNT + '/data/' + relative,
                  data=bytes(token) + b'\n')
    value = json.loads(result.stdout)['data']
    return {key: value.get(key) for key in ['version', 'created_time', 'destroyed', 'deletion_time']}


def provision_keys(installer, reader, plan):
    journal = installer.evidence / 'operational-key-plan.json'
    validate_plan(plan)
    credential_journal = 'key-provisioning-credential.json'
    policy_name = 'ext-secrets-1-hmac-provision-' + sha(str(installer.evidence).encode())[:12]
    prior_credential = installer.evidence / credential_journal
    if prior_credential.exists() and json.loads(prior_credential.read_text()).get('phase') != 'Revoked':
        raise RuntimeError('credential-issuance-recovery-required-no-repeat')
    if policy_name in json.loads(installer.bao('policy', 'list', '-format=json').stdout):
        raise RuntimeError('provisioning-policy-already-exists-requires-accessor-recovery')
    policy = policy_for(plan, True)
    installer.bao('policy', 'write', policy_name, '-', data=policy.encode())
    token = None
    try:
        token, lease = issue(installer, policy_name, 'ext-secrets-1-operational-key-provisioner', '15m', journal_path=installer.evidence / credential_journal)
        issued_record = json.loads((installer.evidence / credential_journal).read_text()) if (installer.evidence / credential_journal).exists() else {}
        durable_write(installer.evidence / credential_journal, {**issued_record, 'phase': 'Issued', 'policy': policy_name, **lease})
        for item in plan:
            metadata = reader.read(item['secretReference'])
            if item['phase'] == 'CreationStarted':
                raise RuntimeError('uncertain-key-creation-requires-exact-metadata-recovery')
            if item['phase'] == 'Prepared':
                if metadata is not None:
                    raise RuntimeError('new-key-reference-already-exists')
                item['phase'] = 'CreationStarted'
                durable_write(journal, plan)
                item['writeReceipt'] = generate_inside_provider(token, item['secretReference'])
                item['phase'] = 'NativeCreated'
                durable_write(journal, plan)
                metadata = reader.read(item['secretReference'])
                if metadata is None:
                    raise RuntimeError('created-key-metadata-not-readable-original-receipt-retained')
                item['phase'] = 'NativeCreated'
                durable_write(journal, plan)
            elif metadata is None:
                # An uncertain creation never generates replacement key bytes.
                raise RuntimeError('uncertain-key-creation-requires-exact-metadata-recovery')
            numeric, created = validate_version(item, metadata)
            receipt = item.get('writeReceipt')
            if receipt is None or receipt.get('version') != int(numeric) or receipt.get('created_time') != created:
                raise RuntimeError('native-key-does-not-match-original-write-receipt')
            item['secretVersion'] = numeric
            # Technical key validity is bounded to seven days from native creation;
            # it does not establish independent profile/actor/revocation validity.
            start = datetime.fromisoformat(created.replace('Z', '+00:00'))
            mapping = {item['opaqueVersion']: {'secretVersion': numeric, 'state': 'Active', 'notBefore': created,
                       'verifyUntil': (start + timedelta(days=7)).isoformat(), 'retiredAt': None}}
            custom = {'ext-secrets-1.tenantId': item['tenantId'], 'ext-secrets-1.purpose': item['purpose'],
                      'ext-secrets-1.secretField': item['secretField'], 'ext-secrets-1.versionMappings': canonical(mapping).decode()}
            # Interrupted metadata publication can only reuse exact original values.
            if metadata.get('custom_metadata') not in (None, {}, custom):
                raise RuntimeError('key-custom-metadata-conflicts-with-original')
            credential_request(token, 'write', MOUNT + '/metadata/' + item['secretReference'], '-', data=canonical({'custom_metadata': custom}))
            after = reader.read(item['secretReference'])
            if after is None or after.get('custom_metadata') != custom or validate_version(item, after)[0] != numeric:
                raise RuntimeError('key-metadata-readback-mismatch')
            item.update({'phase': 'MetadataVerified', 'providerReference': MOUNT + '/' + item['secretReference'],
                         'notBefore': created, 'verifyUntil': mapping[item['opaqueVersion']]['verifyUntil'],
                         'providerMetadataSha256': sha(canonical(after)), 'mapping': mapping})
            durable_write(journal, plan)
    finally:
        if token is not None:
            try:
                application_request(token, 'token', 'revoke', '-self')
                durable_write(installer.evidence / credential_journal, {**issued_record, 'phase': 'Revoked', 'policy': policy_name, **lease})
                installer.bao('policy', 'delete', policy_name)
            finally:
                token[:] = b'\0' * len(token)


def install_data_credential(installer, plan):
    journal_path = installer.evidence / 'data-credential-provisioning.json'
    policy = policy_for(plan)
    if journal_path.exists():
        journal = json.loads(journal_path.read_text())
        if journal['phase'] != 'Stored':
            raise RuntimeError('data-credential-recovery-required-use-recorded-accessor')
        return journal
    if DATA_POLICY in json.loads(installer.bao('policy', 'list', '-format=json').stdout) or kube('get', 'secret', DATA_SECRET, '-n', NS, '--ignore-not-found', '-o', 'name').stdout.strip():
        raise RuntimeError('provider-data-credential-name-already-exists')
    installer.bao('policy', 'write', DATA_POLICY, '-', data=policy.encode())
    token, lease = issue(installer, DATA_POLICY, 'ext-secrets-1-hmac-data-read', '24h', '168h', journal_path=journal_path)
    journal = {**json.loads(journal_path.read_text()), 'phase': 'Issued', 'policy': DATA_POLICY, 'credentialReference': NS + '/Secret/' + DATA_SECRET + '#token',
               'policySha256': sha(policy.encode()), 'explicitMaximumLifetimeSeconds': 604800, **lease}
    try:
        durable_write(journal_path, journal)
        installer.secret(DATA_SECRET, {'token': bytes(token).decode()})
        journal['phase'] = 'Stored'
        durable_write(journal_path, journal)
    except Exception:
        journal['phase'] = 'CleanupPending'
        try:
            durable_write(journal_path, journal)
        except OSError:
            pass
        application_request(token, 'token', 'revoke', '-self')
        # A lost create response can leave only this attempt's newly published
        # revoked credential. Remove it only after constant-time value match and
        # native UID/resourceVersion deletion preconditions.
        delete_matching_credential_secret(DATA_SECRET, token)
        installer.bao('policy', 'delete', DATA_POLICY)
        journal['phase'] = 'RevokedAfterPublicationFailure'
        durable_write(journal_path, journal)
        raise
    finally:
        token[:] = b'\0' * len(token)
    return journal


def component_manifest():
    return {'apiVersion': 'dapr.io/v1alpha1', 'kind': 'Component', 'metadata': {'name': COMPONENT, 'namespace': NS},
            'spec': {'type': 'secretstores.hashicorp.vault', 'version': 'v1', 'metadata': [
                {'name': name, 'value': value} for name, value in {
                    'vaultAddr': 'https://hexalith-keys-active.openbao.svc:8200', 'enginePath': MOUNT,
                    'vaultKVUsePrefix': 'false', 'vaultValueType': 'map', 'vaultTokenMountPath': '/var/run/ext-secrets-1/provider/token',
                    'caCert': '/var/run/ext-secrets-1/ca/ca.pem', 'tlsServerName': BAO_NAME, 'skipVerify': 'false'}.items()]},
            'scopes': ['ext-secrets-1-custody']}


def replace_public(installer, kind, name, value, observed=None):
    before = observed if observed is not None else kjson('get', kind, name, '-n', NS)
    baseline = json.loads((installer.evidence / 'preservation-before.json').read_text())[kind + '/' + NS + '/' + name]
    if before['metadata']['uid'] != baseline['uid']:
        raise RuntimeError('isolated-binding-resource-uid-changed')
    value['metadata'] = {'name': name, 'namespace': NS, 'uid': before['metadata']['uid'], 'resourceVersion': before['metadata']['resourceVersion']}
    kube('replace', '-f', '-', '-o', 'name', data=canonical(value))


def bind(installer, plan, image):
    deployment = kjson('get', 'deployment', 'inventory', '-n', NS)
    if (not re.fullmatch(r'registry\.hexalith\.com/ext-secrets-1-inventory@sha256:[0-9a-f]{64}', image)
            or deployment['metadata'].get('namespace') != NS
            or deployment['spec']['template']['spec'].get('serviceAccountName') != 'ext-secrets-1-inventory'
            or deployment['spec']['template']['spec'].get('automountServiceAccountToken') is not False):
        raise RuntimeError('isolated-inventory-target-rejected')
    preload(installer, image)
    component = component_manifest()
    present = kube('get', 'component', COMPONENT, '-n', NS, '--ignore-not-found', '-o', 'name').stdout.strip()
    if present:
        value = kjson('get', 'component', COMPONENT, '-n', NS)
        if value['spec'] != component['spec'] or value['scopes'] != component['scopes']:
            raise RuntimeError('existing-dapr-component-conflicts')
    else:
        installer.create(component)
    installer.write('installed-provider-component.json', component)
    allowed = {'permittedReferences': [{key: item[key] for key in ['tenantId', 'purpose', 'secretReference', 'secretField']} for item in plan]}
    installer.write('permitted-references.json', allowed)
    config = json.loads((installer.evidence / 'runtime-reference.json').read_text())
    config['runtime'].update({'providerBinding': COMPONENT, 'hmacCredentialReference': NS + '/Secret/' + DATA_SECRET + '#token', 'callTimeout': 30})
    # Physical provider installation never fills independently enrolled authority.
    assert all(config['runtime'][field] is None for field in ['currentInventoryAuthorityReference', 'profileReference', 'issuer', 'audience',
        'profileVersion', 'profileNotBefore', 'profileValidUntil', 'currentRevocationReference', 'publicTrustReference', 'actorRoleAuthorityReference'])
    durable_write(installer.evidence / 'runtime-reference.json', config)
    import qualify
    qualify.read_public_configuration(installer.evidence / 'runtime-reference.json')
    for name, data in [('ext-secrets-1-inventory-runtime', {'runtime-reference.json': canonical(config).decode()}),
                       ('ext-secrets-1-inventory-permitted-references', {'permitted-references.json': canonical(allowed).decode()})]:
        replace_public(installer, 'ConfigMap', name, {'apiVersion': 'v1', 'kind': 'ConfigMap', 'data': data})
    spec = deployment['spec']['template']['spec']
    app = next(item for item in spec['containers'] if item['name'] == 'inventory')
    app['image'] = image
    deployment['spec']['template'].setdefault('metadata', {}).setdefault('annotations', {})['hexalith.io/ext-secrets-1-binding-sha256'] = sha(canonical({'runtime': config, 'allowlist': allowed}))
    # The read-only inventory receives no data token/volume. The component is
    # scoped solely to the separately enrolled future custody app.
    installer.write('custody-provider-pod-fragment.json', {
        'applied': False, 'activeRuntimeEnrolled': False, 'independentInputsStillRequired': True,
        'serviceAccountName': 'ext-secrets-1-custody', 'automountServiceAccountToken': False,
        'daprAppId': 'ext-secrets-1-custody', 'component': COMPONENT,
        'sidecarVolumeMounts': [
            {'name': 'hmac-data', 'mountPath': '/var/run/ext-secrets-1/provider', 'readOnly': True},
            {'name': 'hmac-ca', 'mountPath': '/var/run/ext-secrets-1/ca', 'readOnly': True}],
        'volumes': [
            {'name': 'hmac-data', 'secret': {'secretName': DATA_SECRET, 'defaultMode': 288}},
            {'name': 'hmac-ca', 'configMap': {'name': 'ext-secrets-1-openbao-ca'}}],
        'requiredBeforeDeployment': ['independently enrolled custody image/app/service account and workload identity',
            'separate app and Dapr channel Secrets', 'independent current profile/revocation/public trust',
            'exact enrolled Dapr custody/inventory/profile/revocation ACLs']})
    replace_public(installer, 'Deployment', 'inventory', {key: deployment[key] for key in ['apiVersion', 'kind', 'spec']}, observed=deployment)
    kube('rollout', 'status', 'deployment/inventory', '-n', NS, '--timeout=50s', timeout=55)


def verify_permissions(installer, plan):
    token = bytearray(installer.secret_field(DATA_SECRET, 'token').encode())
    try:
        permitted = {MOUNT + '/data/' + item['secretReference']: ['read'] for item in plan}
        denied = [MOUNT + '/data/unlisted', MOUNT + '/data/tenants/*', MOUNT + '/metadata', MOUNT + '/metadata/*',
                  MOUNT + '/delete/*', MOUNT + '/destroy/*', 'secret/data/*', 'sys/mounts', 'sys/tools/random/32', 'auth/token/create-orphan']
        effective = {path: json.loads(application_request(token, 'token', 'capabilities', '-format=json', path).stdout) for path in [*permitted, *denied]}
        if any(effective[path] != expected for path, expected in permitted.items()) or any(effective[path] != ['deny'] for path in denied):
            raise RuntimeError('provider-data-credential-scope-mismatch')
        lease = json.loads(application_request(token, 'token', 'lookup', '-format=json').stdout)['data']
        public_lease = {key: lease.get(key) for key in ['accessor', 'creation_time', 'expire_time', 'ttl', 'explicit_max_ttl', 'renewable', 'policies']}
        installer.write('provider-data-permissions.json', {'effectiveCapabilities': effective, 'passed': True, 'providerDataReads': 0,
            'credentialLeaseObservation': public_lease, 'providerKeyValidationWrites': 0, 'credentialValuesRecorded': False, 'separateMetadataCredentialRetained': True})
    finally:
        token[:] = b'\0' * len(token)


def install(installer, image):
    if not re.fullmatch(r'registry\.hexalith\.com/ext-secrets-1-inventory@sha256:[0-9a-f]{64}', image):
        raise RuntimeError('isolated-inventory-image-reference-rejected')
    installer.created = json.loads((installer.evidence / 'created-resource-references.json').read_text())
    with journal_lock(installer.evidence / 'provider-key-installation.lock'), ExitStack() as stack:
        idp_port = stack.enter_context(forward('service/registry-idp', NS, 8443))
        native_port = stack.enter_context(forward('deployment/inventory', NS, 3500))
        provider_port = stack.enter_context(forward('service/hexalith-keys-active', 'openbao', 8200))
        observed = verify(installer, native_port, idp_port)
        originals = json.loads((installer.evidence / 'native-enrollment-receipts.json').read_text())['tenants']
        if tuple(item['tenantId'] for item in observed['tenantStreams']) != TENANTS or any(
                item['sourceStreamSha256'] != original['sourceStreamSha256'] for item, original in zip(observed['tenantStreams'], originals)):
            raise RuntimeError('original-native-enrollment-changed')
        installer.write('native-enrollment-before-keys.json', observed)
        path = installer.evidence / 'operational-key-plan.json'
        if not path.exists():
            durable_write(path, plan_keys(), create_only=True)
        plan = json.loads(path.read_text())
        reader = ProviderMetadata(installer, provider_port)
        provision_keys(installer, reader, plan)
        verified = [item for item in plan if item['phase'] == 'MetadataVerified']
        if len(verified) != 4:
            raise RuntimeError('not-all-operational-key-mappings-verified')
        lease = install_data_credential(installer, verified)
        verify_permissions(installer, verified)
        installer.write('provider-key-mappings.json', {'keys': verified, 'dataCredential': lease,
            'ordinarySnapshotSemantics': True, 'lifecycleOrAllCopyQualified': False, 'independentTrustEstablished': False,
            'reservedSystemSecurityObservationKey': None, 'contractOperations': 0, 'providerDataReads': 0,
            'newOperationalKeyWritesWithDurableOriginalReceipt': len(verified), 'uncertainKeyPaths': [], 'validationKeyWrites': 0, 'providerKeyRemovals': 0})
        # Keep the original installation lock through config binding and rollout.
        bind(installer, verified, image)


if __name__ == '__main__':
    instance = Installer(sys.argv[1])
    try:
        install(instance, sys.argv[2])
        print('operational HMAC metadata and restricted provider binding installed; independent authority remains unbound')
    except Exception as error:
        instance.write('provider-key-installation-failure.json', {'exceptionType': type(error).__name__,
                       'reason': str(error) if type(error) is RuntimeError else 'sanitized-installation-failure'})
        raise SystemExit(1) from None
    finally:
        instance.close()
