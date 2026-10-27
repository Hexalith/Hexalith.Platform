#!/usr/bin/env python3
"""Bounded owner-authorized successor and custody continuation; never runs Live or contract cases.

Only four retained isolated originals can receive one CAS successor. Unknown native
writes retain their original journal and stop; a new invocation never repeats them.
Existing policies and original journals remain untouched. Credentials are transient
stdin/HTTPS values and intended new Kubernetes Secrets, never evidence or argv.
"""
import argparse
import base64
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import http.client
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import sys
import uuid

from install_isolated_registry import Installer, NS, ROOT, REGISTER, BAO_CA, BAO_NAME, canonical, env_secret, kube, kjson, sha, snapshot
from install_current_inventory import application_request
from install_current_inventory import cleanup_journaled_credential, read_pinned_credential_secret
from install_hmac_authority import AuthorityInstaller, APP as AUTHORITY, REALM, ISSUER, QUERY_CLIENT, ADMIN_CLIENT, RUNTIME, forward, administration_request, owner_operation, timestamp
from native_journal import durable_write, journal_lock
from preload_isolated_image import preload
from provision_registry_identity import Idp, audience, hardcoded

EVIDENCE_ROOT = Path('/home/administrator/hexalith-secrets-evidence/ext-secrets-1')
ORIGINAL_AUTHORITY = EVIDENCE_ROOT / 'authority-installation-20261027T173655237972Z'
ORIGINAL_KEYS = EVIDENCE_ROOT / 'provider-key-installation-20261017T100804405564Z'
APP = 'ext-secrets-1-custody'
CUSTODY_CLIENT = 'ext-secrets-1-hmac-custody'
INVENTORY = 'ext-secrets-1-inventory'
DATA_POLICY = 'ext-secrets-1-hmac-data-read'
METADATA_POLICY = 'ext-secrets-1-inventory-metadata-read'
MOUNT = 'ext-secrets-1-isolated'
TENANTS = ('01M54GTX47NH1PRB00T2YS5048', '01M54GTX479F4Y9A3MRW578C4R')
PURPOSES = ('TrustedEnvelope', 'ContentDigest')
MUTABLE = {'Deployment/' + NS + '/inventory', 'ConfigMap/' + NS + '/ext-secrets-1-inventory-runtime',
           'Configuration/' + NS + '/ext-secrets-1-inventory', 'Configuration/' + NS + '/' + AUTHORITY}


def exact_image(value, name):
    if not re.fullmatch(r'registry\.hexalith\.com/' + re.escape(name) + r'@sha256:[0-9a-f]{64}', value):
        raise ValueError('An exact isolated image digest is required.')
    return value


def instant(value):
    return datetime.fromisoformat(value.replace('Z', '+00:00'))


def original_plan():
    plan = json.loads((ORIGINAL_KEYS / 'operational-key-plan.json').read_text())
    expected = {(tenant, purpose) for tenant in TENANTS for purpose in PURPOSES}
    if len(plan) != 4 or {(item['tenantId'], item['purpose']) for item in plan} != expected:
        raise RuntimeError('original-four-key-plan-mismatch')
    for item in plan:
        if (item['phase'] != 'MetadataVerified' or item['secretVersion'] != '1'
            or item['secretReference'] != 'tenants/' + item['tenantId'] + '/' + item['purpose'] + '/operational'
            or item['secretField'] != 'hmacKey' or set(item['mapping']) != {item['opaqueVersion']}
            or instant(item['verifyUntil']) > datetime.now(timezone.utc)):
            raise RuntimeError('original-expired-key-prerequisite-mismatch')
    return plan


def validate_original(item, metadata, successor=False):
    old = item['original'] if 'original' in item else item
    custom = metadata.get('custom_metadata') or {}
    mappings = json.loads(custom.get('ext-secrets-1.versionMappings', '{}'))
    native = metadata.get('versions', {}).get('1')
    expected_native = {key: old['writeReceipt'][key] for key in ('created_time', 'deletion_time', 'destroyed')}
    if (native != expected_native or mappings.get(old['opaqueVersion']) != old['mapping'][old['opaqueVersion']]
        or custom.get('ext-secrets-1.tenantId') != old['tenantId'] or custom.get('ext-secrets-1.purpose') != old['purpose']
        or custom.get('ext-secrets-1.secretField') != old['secretField']):
        raise RuntimeError('original-key-material-or-validity-metadata-changed')
    permitted = {old['opaqueVersion']}
    if successor:
        permitted.add(item['opaqueVersion'])
    if set(mappings) - permitted or set(metadata.get('versions', {})) - ({'1', '2'} if successor else {'1'}):
        raise RuntimeError('unexpected-extra-key-version-or-mapping')
    if not successor and (metadata.get('current_version') != 1 or set(mappings) != permitted):
        raise RuntimeError('original-current-version-not-exactly-one')
    return mappings


def retention_preserves_original(config, metadata):
    """Zero means the KV2 default (ten versions); no automatic deletion is permitted."""
    mount_maximum = config.get('max_versions', 0)
    key_maximum = metadata.get('max_versions', 0)
    if type(mount_maximum) is not int or type(key_maximum) is not int or min(mount_maximum, key_maximum) < 0:
        raise RuntimeError('provider-retention-prerequisite-invalid')
    effective = key_maximum or mount_maximum or 10
    if effective < 2 or config.get('delete_version_after', '0s') not in ('0s', 0) or metadata.get('delete_version_after', '0s') not in ('0s', 0):
        raise RuntimeError('provider-retention-would-not-preserve-original-version')
    return {'effectiveMaximumVersions': effective, 'automaticDeletionDisabled': True}


def generate_successor_inside_provider(token, relative, expected_version):
    if expected_version != 1 or relative not in {item['secretReference'] for item in original_plan()}:
        raise RuntimeError('successor-target-outside-four-authorized-originals')
    script = ('set -eu; IFS= read -r BAO_TOKEN; export BAO_TOKEN; '
        'export BAO_ADDR=https://127.0.0.1:8200 BAO_CACERT=' + BAO_CA + ' BAO_TLS_SERVER_NAME=' + BAO_NAME + ' BAO_SKIP_VERIFY=false; '
        'material=$(bao write -field=random_bytes sys/tools/random/32 format=base64); test "${#material}" -eq 44; '
        'printf \'{"options":{"cas":1},"data":{"hmacKey":"%s"}}\' "$material" | bao write -format=json "$1" -; unset material')
    result = kube('exec', '-i', '-n', 'openbao', 'hexalith-keys-0', '--', 'sh', '-c', script, 'sh', MOUNT + '/data/' + relative,
                  data=bytes(token) + b'\n')
    document = json.loads(result.stdout)
    receipt = {key: document['data'].get(key) for key in ('version', 'created_time', 'deletion_time', 'destroyed')}
    return {'requestId': document['request_id'], 'data': receipt}


def provider_request(token, *args, data=b''):
    script = ('IFS= read -r BAO_TOKEN || exit 65; export BAO_TOKEN; '
        'export BAO_ADDR=https://127.0.0.1:8200 BAO_CACERT=' + BAO_CA + ' BAO_TLS_SERVER_NAME=' + BAO_NAME + ' BAO_SKIP_VERIFY=false; exec bao "$@"')
    return kube('exec', '-i', '-n', 'openbao', 'hexalith-keys-0', '--', 'sh', '-c', script, 'sh', *args,
        data=bytes(token) + b'\n' + data)


def provisioning_policy(plan):
    statements = ['path "' + MOUNT + '/config" { capabilities = ["read"] }',
        'path "sys/tools/random/32" { capabilities = ["update"] }']
    for item in plan:
        statements += ['path "' + MOUNT + '/data/' + item['secretReference'] + '" { capabilities = ["create", "update"] }',
            'path "' + MOUNT + '/metadata/' + item['secretReference'] + '" { capabilities = ["read", "update"] }']
    statements += ['path "auth/token/lookup-self" { capabilities = ["read"] }',
        'path "auth/token/revoke-self" { capabilities = ["update"] }', 'path "sys/capabilities-self" { capabilities = ["update"] }']
    return '\n'.join(statements) + '\n'


class CustodyInstaller(AuthorityInstaller):
    def __init__(self, evidence, custody_image, inventory_image):
        Installer.__init__(self, evidence)
        self.image = exact_image(custody_image, APP)
        self.inventory_image = exact_image(inventory_image, INVENTORY)
        self.run_path = self.evidence / 'run.json'
        if self.run_path.exists():
            self.run = json.loads(self.run_path.read_text())
            if self.run['custodyImage'] != custody_image or self.run['inventoryImage'] != inventory_image:
                raise RuntimeError('original-continuation-image-mismatch')
        else:
            self.run = {'schema': 'EXT-SECRETS-1.custody-binding.v1', 'runId': uuid.uuid4().hex,
                'startedAt': timestamp(), 'phase': 'Prepared', 'custodyImage': custody_image, 'inventoryImage': inventory_image,
                'contractOperations': 0, 'liveRun': False, 'dependencyAvailable': False, 'providerDataReads': 0}
            durable_write(self.run_path, self.run, create_only=True)
        self.journal_path = self.evidence / 'resource-journal.json'
        self.resources = json.loads(self.journal_path.read_text()) if self.journal_path.exists() else []
        self.created = [item['reference'] for item in self.resources if item['phase'] == 'Created']
        self.tokens = {}
        self.private_values = [bytes(self.token)]

    def close(self):
        try:
            issuance = self.evidence / 'provisioner-credential-issuance.json'
            policy = self.evidence / 'provisioner-policy-journal.json'
            if issuance.exists() and policy.exists() and json.loads(policy.read_text())['phase'] != 'Removed' and json.loads(issuance.read_text()).get('accessor'):
                self.remove_provisioner()
        finally:
            for token in self.tokens.values():
                token[:] = b'\0' * len(token)
            self.tokens.clear()
            self.private_values.clear()
            Installer.close(self)

    def secret_field(self, name, field):
        value = Installer.secret_field(self, name, field)
        if field not in ('ca\\.crt', 'client-id') and value:
            self.private_values.append(value.encode())
        return value

    def metadata(self, relative):
        value = application_request(self.tokens['metadata'], 'read', '-format=json', MOUNT + '/metadata/' + relative)
        return json.loads(value.stdout)['data']

    def credential(self, role, policy):
        path = self.evidence / (role + '-credential-issuance.json')
        name = APP + '-' + role + '-' + self.run['runId'][:12]
        if path.exists():
            journal = json.loads(path.read_text())
            if journal['phase'] != 'Stored' or journal.get('policy') != policy or journal.get('secretName') != name:
                raise RuntimeError('original-' + role + '-credential-issuance-needs-reconciliation')
            token = read_pinned_credential_secret(name, journal.get('secretIdentity'))
            try:
                lookup = json.loads(application_request(token, 'token', 'lookup', '-format=json').stdout)['data']
                if not journal.get('accessor') or lookup.get('accessor') != journal['accessor']:
                    raise RuntimeError('original-' + role + '-credential-accessor-changed')
            except Exception:
                token[:] = b'\0' * len(token)
                raise
        else:
            if kube('get', 'secret', name, '-n', NS, '--ignore-not-found', '-o', 'name').stdout.strip():
                raise RuntimeError('new-credential-name-overlap')
            journal = {'phase': 'IssuanceStarted', 'issuanceId': uuid.uuid4().hex, 'policy': policy, 'startedAt': timestamp(),
                'secretName': name, 'credentialReference': NS + '/Secret/' + name + '#token', 'existingPolicyChanged': False,
                'secretCreationPhase': 'NotStarted'}
            durable_write(path, journal, create_only=True)
            token = None
            try:
                auth = json.loads(self.bao('write', '-format=json', 'auth/token/create-orphan', '-', data=canonical({
                    'policies': [policy], 'no_default_policy': True, 'display_name': APP + '-' + journal['issuanceId'],
                    'ttl': '1200s' if role == 'provisioner' else '24h', 'explicit_max_ttl': '1200s' if role == 'provisioner' else '168h',
                    'renewable': role != 'provisioner'})).stdout)['auth']
                token = bytearray(auth.pop('client_token').encode())
                self.tokens[role] = token
                self.private_values.append(bytes(token))
                journal.update({'phase': 'Issued', 'accessor': auth['accessor'], 'leaseDuration': auth['lease_duration'], 'issuedAt': timestamp()})
                durable_write(path, journal)
                journal['secretCreationPhase'] = 'Started'
                durable_write(path, journal)
                journal['secretIdentity'] = self.secret(name, {'token': bytes(token).decode()})
                journal.update({'phase': 'Stored', 'secretCreationPhase': 'Created'})
                durable_write(path, journal)
            except Exception:
                journal['phase'] = 'IssuanceUnknown' if token is None else 'CredentialStorageNeedsReconciliation'
                durable_write(path, journal)
                raise
        self.tokens[role] = token
        lookup = json.loads(application_request(token, 'token', 'lookup', '-format=json').stdout)['data']
        if lookup.get('accessor') != journal['accessor'] or lookup['policies'] != [policy] or lookup['ttl'] <= 0:
            raise RuntimeError('current-' + role + '-credential-policy-or-lease-mismatch')
        for forbidden in ('secret/data/*', 'sys/policies/acl/*', MOUNT + '/data/*' if role in ('metadata', 'provisioner') else MOUNT + '/metadata/*'):
            if json.loads(application_request(token, 'token', 'capabilities', '-format=json', forbidden).stdout) != ['deny']:
                raise RuntimeError('current-' + role + '-credential-excess-permission')
        for item in original_plan():
            path_name = MOUNT + ('/data/' if role == 'data' else '/metadata/') + item['secretReference']
            capabilities = json.loads(application_request(token, 'token', 'capabilities', '-format=json', path_name).stdout)
            required = {'read', 'update'} if role == 'provisioner' else {'read'}
            if not required <= set(capabilities) or any(value not in (('read', 'update') if role == 'provisioner' else ('read', 'list')) for value in capabilities):
                raise RuntimeError('current-' + role + '-credential-required-capability-missing')
            if role == 'provisioner':
                data_capabilities = json.loads(application_request(token, 'token', 'capabilities', '-format=json', MOUNT + '/data/' + item['secretReference']).stdout)
                if set(data_capabilities) != {'create', 'update'}:
                    raise RuntimeError('provisioner-exact-write-capabilities-mismatch')
        journal['verification'] = {'observedAt': timestamp(), 'ttlSeconds': lookup['ttl'], 'expireTime': lookup['expire_time'],
            'explicitMaximumTtlSeconds': lookup['explicit_max_ttl'], 'policies': lookup['policies'], 'providerDataReads': 0}
        durable_write(path, journal)
        return journal

    def provisioner(self):
        path = self.evidence / 'provisioner-policy-journal.json'
        policy = provisioning_policy(original_plan())
        name = APP + '-successor-' + self.run['runId'][:12]
        names = json.loads(self.bao('policy', 'list', '-format=json').stdout)
        if path.exists():
            journal = json.loads(path.read_text())
            if journal['policy'] != policy or journal['name'] != name or journal['phase'] == 'Removed':
                raise RuntimeError('original-provisioning-policy-reconciliation-required')
            if name not in names or self.bao('policy', 'read', name).stdout.strip() != policy.encode().strip():
                raise RuntimeError('original-provisioning-policy-outcome-unknown')
            journal['phase'] = 'CurrentExactPolicyVerified'
            self.write(path.name, journal)
        else:
            if name in names:
                raise RuntimeError('new-provisioning-policy-name-overlap')
            journal = {'phase': 'CreationStarted', 'name': name, 'policy': policy, 'runId': self.run['runId'], 'startedAt': timestamp(), 'existingPolicyChanged': False}
            durable_write(path, journal, create_only=True)
            self.bao('policy', 'write', name, '-', data=policy.encode())
            if self.bao('policy', 'read', name).stdout.strip() != policy.encode().strip():
                raise RuntimeError('new-provisioning-policy-readback-mismatch')
            journal['phase'] = 'Created'
            self.write(path.name, journal)
        self.credential('provisioner', name)

    def remove_provisioner(self):
        policy_path = self.evidence / 'provisioner-policy-journal.json'
        journal = json.loads(policy_path.read_text())
        issuance_path = self.evidence / 'provisioner-credential-issuance.json'
        issuance = json.loads(issuance_path.read_text())
        if journal.get('runId') != self.run['runId'] or journal.get('name') != APP + '-successor-' + self.run['runId'][:12]:
            raise RuntimeError('original-run-owned-provisioner-policy-required-no-delete')
        try:
            cleanup_journaled_credential(self, issuance_path, issuance['secretName'], self.tokens.get('provisioner'))
        finally:
            if json.loads(issuance_path.read_text()).get('revocationPhase') == 'Acknowledged':
                token = self.tokens.pop('provisioner', None)
                if token is not None:
                    token[:] = b'\0' * len(token)
        names = json.loads(self.bao('policy', 'list', '-format=json').stdout)
        if journal.get('removalPhase') == 'Started':
            if journal['name'] in names:
                raise RuntimeError('original-provisioner-policy-removal-outcome-needs-reconciliation')
        elif journal['name'] not in names:
            if journal['phase'] != 'Removed':
                raise RuntimeError('original-provisioner-policy-unexpectedly-absent')
            return
        else:
            if self.bao('policy', 'read', journal['name']).stdout.strip() != journal['policy'].encode().strip():
                raise RuntimeError('run-owned-provisioner-policy-changed-no-delete')
            journal['removalPhase'] = 'Started'
            self.write(policy_path.name, journal)
            self.bao('policy', 'delete', journal['name'])
        journal.update({'phase': 'Removed', 'removalPhase': 'Acknowledged', 'removedAt': timestamp()})
        self.write(policy_path.name, journal)


def preserve_original_journals(installer):
    public = {}
    for directory in (ORIGINAL_KEYS, ORIGINAL_AUTHORITY):
        for path in directory.glob('*.json'):
            public[str(path)] = sha(path.read_bytes())
    for directory in EVIDENCE_ROOT.glob('provider-key-installation-*'):
        for path in directory.glob('*.json'):
            public[str(path)] = sha(path.read_bytes())
    original = installer.evidence / 'original-journal-fingerprints.json'
    if original.exists():
        if json.loads(original.read_text()) != public:
            raise RuntimeError('preserved-original-journal-changed')
    else:
        durable_write(original, public, create_only=True)


def inspect(installer):
    preserve_original_journals(installer)
    original_plan()
    controller = kjson('get', 'statefulset', AUTHORITY, '-n', NS)
    original_resource = next(item for item in json.loads((ORIGINAL_AUTHORITY / 'resource-journal.json').read_text())
        if item['kind'] == 'StatefulSet' and item['name'] == AUTHORITY)
    original_run = json.loads((ORIGINAL_AUTHORITY / 'run.json').read_text())
    if (controller['metadata']['uid'] != original_resource['uid']
        or next(item['image'] for item in controller['spec']['template']['spec']['containers'] if item['name'] == 'authority') != original_run['installedImage']
        or controller.get('status', {}).get('readyReplicas') != 1):
        raise RuntimeError('installed-original-authority-not-current-ready')
    component = kjson('get', 'component', 'ext-secrets-1-hmac', '-n', NS)
    values = {item['name']: item.get('value') for item in component['spec']['metadata']}
    if (component['spec']['type'] != 'secretstores.hashicorp.vault' or component['scopes'] != [APP]
        or values.get('enginePath') != MOUNT or values.get('tlsServerName') != BAO_NAME or values.get('skipVerify') != 'false'
        or values.get('vaultTokenMountPath') != '/var/run/ext-secrets-1/provider/token'
        or values.get('caCert') != '/var/run/ext-secrets-1/ca/ca.pem'):
        raise RuntimeError('existing-provider-component-prerequisite-mismatch')
    mounts = json.loads(installer.bao('secrets', 'list', '-detailed', '-format=json').stdout)
    if mounts[MOUNT + '/']['type'] != 'kv' or mounts[MOUNT + '/']['options'].get('version') != '2':
        raise RuntimeError('existing-isolated-kv2-mount-mismatch')
    foundations = json.loads((EVIDENCE_ROOT / 'provider-inventory-installation-20261017T072855253627Z/installation-references.json').read_text())
    service_account = kjson('get', 'serviceaccount', APP, '-n', NS)
    if not any(item.get('uid') == service_account['metadata']['uid'] for item in foundations['installedResources']):
        raise RuntimeError('preserved-custody-service-account-identity-mismatch')
    if not (installer.evidence / 'preserved-kubernetes-before.json').exists():
        installer.run['agentsRegisterSha256'] = sha(REGISTER.read_bytes())
        installer.write('run.json', installer.run)
        installer.write('preserved-kubernetes-before.json', snapshot())
        installer.write('preserved-openbao-before.json', installer.openbao_snapshot())
        installer.write('runtime-reference.before.json', json.loads(RUNTIME.read_text()))
    installer.provisioner()
    installer.write('provider-retention-config.json', json.loads(provider_request(installer.tokens['provisioner'], 'read', '-format=json', MOUNT + '/config').stdout)['data'])
    _, state = administration_request(installer, 'GET', '/admin/state')
    for name in ('PublishProfile', 'SetCaller'):
        old = json.loads((ORIGINAL_AUTHORITY / ('owner-operation-' + name + '.json')).read_text())
        if old['phase'] != 'Committed' or old['receipt'] not in state['audit']:
            raise RuntimeError('original-authority-administration-receipt-not-current')
    if not (installer.evidence / 'authority-state.before.json').exists():
        durable_write(installer.evidence / 'authority-state.before.json', state, create_only=True)
    if state['profile'] is None or state['profile']['revoked'] or state['profile']['profileReference'] != AUTHORITY:
        raise RuntimeError('current-authority-profile-missing-or-revoked')
    if instant(state['profile']['validUntil']) <= datetime.now(timezone.utc) + timedelta(hours=1):
        start = datetime.now(timezone.utc)
        renewed = dict(state['profile'], version='owner-' + installer.run['runId'], notBefore=start.isoformat(), validUntil=(start + timedelta(days=1)).isoformat())
        owner_operation(installer, 'PublishProfile', profile=renewed)
        _, state = administration_request(installer, 'GET', '/admin/state')
    installer.write('authority-profile-verification.json', {'observedAt': timestamp(), 'revision': state['revision'],
        'profile': state['profile'], 'originalReceiptsVerified': True, 'profileRenewed': (installer.evidence / 'owner-operation-PublishProfile.json').exists()})
    return state['profile']


def successors(installer):
    path = installer.evidence / 'successor-key-plan.json'
    if path.exists():
        plan = json.loads(path.read_text())
    else:
        plan = []
        for original in original_plan():
            before = installer.metadata(original['secretReference'])
            validate_original(original, before)
            retention_preserves_original(json.loads((installer.evidence / 'provider-retention-config.json').read_text()), before)
            plan.append({'original': original, 'beforeMetadata': before, 'phase': 'Prepared',
                'opaqueVersion': 'hmac-' + str(uuid.uuid4()), 'expectedCurrentVersion': 1})
        durable_write(path, plan, create_only=True)
    if len(plan) != 4 or [item['original'] for item in plan] != original_plan():
        raise RuntimeError('original-successor-journal-target-mismatch')
    for item in plan:
        relative = item['original']['secretReference']
        before = installer.metadata(relative)
        if item['phase'] in ('CreationStarted', 'CreationUnknown'):
            raise RuntimeError('unknown-successor-native-write-needs-original-request-reconciliation-no-repeat')
        if item['phase'] == 'Prepared':
            validate_original(item, before)
            current_retention = json.loads(provider_request(installer.tokens['provisioner'], 'read', '-format=json', MOUNT + '/config').stdout)['data']
            item['retentionVerification'] = retention_preserves_original(current_retention, before)
            if before != item['beforeMetadata']:
                raise RuntimeError('original-current-metadata-changed-before-successor-cas')
            item.update({'phase': 'CreationStarted', 'startedAt': timestamp()})
            durable_write(path, plan)
            try:
                item['nativeWriteReceipt'] = generate_successor_inside_provider(installer.tokens['provisioner'], relative, 1)
                item['phase'] = 'NativeCreated'
                durable_write(path, plan)
            except Exception:
                item['phase'] = 'CreationUnknown'
                durable_write(path, plan)
                raise
        metadata = installer.metadata(relative)
        validate_original(item, metadata, successor=True)
        receipt = item['nativeWriteReceipt']
        native = metadata.get('versions', {}).get('2')
        if (not receipt.get('requestId') or receipt['data']['version'] != 2 or metadata.get('current_version') != 2
            or native != {key: receipt['data'][key] for key in ('created_time', 'deletion_time', 'destroyed')}
            or native.get('destroyed') is not False or native.get('deletion_time') != ''):
            raise RuntimeError('successor-does-not-match-original-native-write-receipt')
        created = receipt['data']['created_time']
        mapping = {'secretVersion': '2', 'state': 'Active', 'notBefore': created,
            'verifyUntil': (instant(created) + timedelta(days=7)).isoformat(), 'retiredAt': None}
        mappings = deepcopy(item['original']['mapping'])
        mappings[item['opaqueVersion']] = mapping
        custom = deepcopy(item['beforeMetadata']['custom_metadata'])
        custom['ext-secrets-1.versionMappings'] = canonical(mappings).decode()
        if metadata['custom_metadata'] != custom:
            if metadata['custom_metadata'] != item['beforeMetadata']['custom_metadata']:
                raise RuntimeError('successor-custom-metadata-conflict')
            # A local journal/process lock cannot fence another provider writer.
            # Completed publication still reconciles below; future writes require
            # an independently verified provider-native conditional-write fence.
            raise RuntimeError('provider-native-metadata-fence-required-no-publication')
        after = installer.metadata(relative)
        validate_original(item, after, successor=True)
        if after['custom_metadata'] != custom or after['versions']['2'] != native or after['current_version'] != 2:
            raise RuntimeError('successor-metadata-readback-mismatch')
        item.update({'phase': 'MetadataVerified', 'mapping': mapping, 'secretVersion': '2', 'afterMetadata': after,
                     'originalMaterialAndValidityPreserved': True, 'verifiedAt': timestamp()})
        durable_write(path, plan)
    return plan


def provision_custody_identity(installer):
    path = installer.evidence / 'custody-identity-journal.json'
    if path.exists():
        journal = json.loads(path.read_text())
        if journal['phase'] != 'Provisioned':
            raise RuntimeError('original-custody-identity-needs-reconciliation')
        return journal
    idp = Idp(installer)
    idp.login()
    base = '/admin/realms/' + REALM
    clients, _ = idp.call('GET', base + '/clients?clientId=' + CUSTODY_CLIENT)
    if clients:
        raise RuntimeError('custody-identity-name-overlap')
    name = APP + '-authority-identity-' + installer.run['runId'][:12]
    journal = {'phase': 'CreationStarted', 'clientId': CUSTODY_CLIENT, 'credentialReference': NS + '/Secret/' + name,
        'secretName': name, 'startedAt': timestamp(), 'realm': REALM, 'audiences': [AUTHORITY, INVENTORY],
        'operations': ['hmac:query', 'inventory:query']}
    durable_write(path, journal, create_only=True)
    secret = secrets.token_urlsafe(48)
    installer.private_values.append(secret.encode())
    operation_mapper = hardcoded('eventstore:operation', json.dumps(journal['operations']))
    operation_mapper['config']['jsonType.label'] = 'JSON'
    _, location = idp.call('POST', base + '/clients', {'clientId': CUSTODY_CLIENT, 'enabled': True, 'protocol': 'openid-connect',
        'publicClient': False, 'secret': secret, 'serviceAccountsEnabled': True, 'directAccessGrantsEnabled': False,
        'standardFlowEnabled': False, 'fullScopeAllowed': False, 'defaultClientScopes': [], 'optionalClientScopes': [],
        'protocolMappers': [audience(AUTHORITY), audience(INVENTORY), operation_mapper]})
    native = location.rsplit('/', 1)[1]
    # Keycloak's hardcoded mapper emits an array when JSON type is JSON.
    client, _ = idp.call('GET', base + '/clients/' + native)
    mapper = next(item for item in client['protocolMappers'] if item['name'] == 'eventstore:operation')
    mapper['config']['claim.value'] = json.dumps(journal['operations'])
    mapper['config']['jsonType.label'] = 'JSON'
    idp.call('PUT', base + '/clients/' + native, client)
    journal.update({'phase': 'IdentityCreated', 'nativeClientId': native})
    durable_write(path, journal)
    scopes, _ = idp.call('GET', base + '/client-scopes')
    basic = next(item['id'] for item in scopes if item['name'] == 'basic')
    idp.call('PUT', base + '/clients/' + native + '/default-client-scopes/' + basic)
    installer.secret(name, {'client-id': CUSTODY_CLIENT, 'client-secret': secret})
    del secret
    journal['phase'] = 'Provisioned'
    durable_write(path, journal)
    return journal


def replace_public(installer, kind, name, value, journal_suffix=''):
    key = kind + '/' + NS + '/' + name
    owned = next((item for item in getattr(installer, 'resources', []) if item['reference'] == key and item['phase'] == 'Created'), None)
    if key not in MUTABLE and not (kind == 'Deployment' and name == APP and owned):
        raise RuntimeError('isolated-resource-update-outside-authorized-set')
    before = kjson('get', kind, name, '-n', NS)
    baseline = json.loads((installer.evidence / 'preserved-kubernetes-before.json').read_text()).get(key) or owned
    if before['metadata']['uid'] != baseline['uid']:
        raise RuntimeError('isolated-resource-uid-changed')
    if journal_suffix and not re.fullmatch(r'-[a-z-]+', journal_suffix):
        raise RuntimeError('invalid-public-update-journal-suffix')
    path = installer.evidence / ('update-' + kind.lower() + '-' + name + journal_suffix + '.json')
    material = deepcopy(value)
    material.pop('status', None)
    supplied = value.get('metadata', {})
    if supplied.get('uid') != before['metadata']['uid'] or not supplied.get('resourceVersion'):
        raise RuntimeError('original-supplied-update-precondition-required')
    material['metadata'] = deepcopy(supplied)
    material['metadata'].pop('managedFields', None)
    # Exact native resourceVersion is the update precondition.
    intended = public_object(material)
    if path.exists():
        journal = json.loads(path.read_text())
        if journal['uid'] != before['metadata']['uid'] or public_object(journal['intendedPublicObject']) != intended:
            raise RuntimeError('original-isolated-update-intent-changed')
        if public_object(before) != intended:
            raise RuntimeError('original-isolated-update-outcome-unknown-reconciliation-required')
        journal.update({'phase': 'ReconciledCurrent', 'observedResourceVersion': before['metadata']['resourceVersion']})
        installer.write(path.name, journal)
        return
    if supplied['resourceVersion'] != before['metadata']['resourceVersion']:
        raise RuntimeError('stale-supplied-update-resource-version-no-replace')
    journal = {'phase': 'ReplacementStarted', 'reference': key, 'uid': before['metadata']['uid'],
        'expectedResourceVersion': before['metadata']['resourceVersion'], 'intendedPublicObject': intended,
        'originalPublicObject': public_object(before), 'intentSha256': sha(canonical(intended))}
    durable_write(path, journal, create_only=True)
    kube('replace', '-f', '-', '-o', 'name', data=canonical(material))
    after = kjson('get', kind, name, '-n', NS)
    if public_object(after) != intended:
        raise RuntimeError('isolated-update-readback-mismatch')
    journal.update({'phase': 'Replaced', 'resourceVersion': after['metadata']['resourceVersion']})
    installer.write(path.name, journal)


def public_object(value):
    public = {key: deepcopy(item) for key, item in value.items() if key not in ('metadata', 'status')}
    public['metadata'] = {key: deepcopy(value['metadata'][key]) for key in ('name', 'namespace', 'labels', 'annotations') if key in value['metadata']}
    if value.get('kind') == 'Deployment' and 'annotations' in public['metadata']:
        public['metadata']['annotations'].pop('deployment.kubernetes.io/revision', None)
        if not public['metadata']['annotations']:
            public['metadata'].pop('annotations')
    return public


def restart_authority(installer):
    path = installer.evidence / 'authority-pod-restart.json'
    pod = kjson('get', 'pod', AUTHORITY + '-0', '-n', NS)
    if path.exists():
        journal = json.loads(path.read_text())
        if pod['metadata']['uid'] == journal['originalPodUid']:
            raise RuntimeError('original-authority-restart-outcome-unknown-no-delete-repeat')
        journal.update({'phase': 'ReplacementObserved', 'replacementPodUid': pod['metadata']['uid']})
        installer.write(path.name, journal)
        return
    journal = {'originalPodUid': pod['metadata']['uid'], 'phase': 'DeletionStarted', 'startedAt': timestamp()}
    durable_write(path, journal, create_only=True)
    kube('delete', '--raw', '/api/v1/namespaces/' + NS + '/pods/' + AUTHORITY + '-0', '-f', '-',
        data=canonical({'apiVersion': 'v1', 'kind': 'DeleteOptions', 'preconditions': {'uid': pod['metadata']['uid']}}))
    journal['phase'] = 'OriginalDeleteAcknowledged'
    installer.write(path.name, journal)


def mount_regular_runtime_files(installer):
    observations = []
    for name, container_name, mount_directory in [('inventory', 'inventory', '/run/inventory-runtime'), (APP, 'custody', '/run/custody-runtime')]:
        value = kjson('get', 'deployment', name, '-n', NS)
        app = next(item for item in value['spec']['template']['spec']['containers'] if item['name'] == container_name)
        mount = next(item for item in app['volumeMounts'] if item['name'] == 'runtime')
        mount.update({'mountPath': mount_directory + '/runtime-reference.json', 'subPath': 'runtime-reference.json', 'readOnly': True})
        setting = 'Inventory__RuntimeReferenceFile' if name == 'inventory' else 'Custody__RuntimeReferenceFile'
        next(item for item in app['env'] if item['name'] == setting)['value'] = mount_directory + '/runtime-reference.json'
        replace_public(installer, 'Deployment', name, value, '-canonical-runtime-file')
        observed = kjson('get', 'deployment', name, '-n', NS)
        actual = next(item for item in observed['spec']['template']['spec']['containers'] if item['name'] == container_name)
        if next(item for item in actual['volumeMounts'] if item['name'] == 'runtime') != mount:
            raise RuntimeError('regular-runtime-file-mount-metadata-mismatch')
        observations.append({'workloadReference': NS + '/Deployment/' + name + '@uid:' + observed['metadata']['uid'], 'mount': mount})
    installer.write('canonical-runtime-file-mounts.json', observations)


def configure_workloads(installer, profile, plan, data, metadata, identity):
    public = json.loads(RUNTIME.read_text())
    public['runtime'].update({'hmacCredentialReference': data['credentialReference'], 'currentInventoryAuthorityReference': INVENTORY,
        'profileVersion': profile['version'], 'profileNotBefore': profile['notBefore'], 'profileValidUntil': profile['validUntil']})
    # No new runtime fields, qualification identities, decision keys or availability status.
    installer.write('runtime-reference.pending.json', public)
    import qualify
    qualify.read_public_configuration(installer.evidence / 'runtime-reference.pending.json')
    allowlist = kjson('get', 'configmap', 'ext-secrets-1-inventory-permitted-references', '-n', NS)
    allowed = json.loads(allowlist['data']['permitted-references.json'])['permittedReferences']
    expected = [{name: item['original'][name] for name in ('tenantId', 'purpose', 'secretReference', 'secretField')} for item in plan]
    if allowed != expected:
        raise RuntimeError('existing-inventory-allowlist-not-exact-four-authorized-mappings')
    runtime_name = APP + '-runtime-' + installer.run['runId'][:12]
    installer.create({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': runtime_name}, 'data': {'runtime-reference.json': json.dumps(public, sort_keys=True)}})
    for channel in ('app', 'dapr'):
        installer.secret(APP + '-' + channel + '-' + installer.run['runId'][:12], {'token': secrets.token_urlsafe(48)})
    installer.create({'apiVersion': 'dapr.io/v1alpha1', 'kind': 'Configuration', 'metadata': {'name': APP},
        'spec': {'accessControl': {'defaultAction': 'deny', 'trustDomain': 'cluster.local', 'policies': []}}})
    installer.create({'apiVersion': 'v1', 'kind': 'Service', 'metadata': {'name': APP},
        'spec': {'selector': {'app': APP}, 'ports': [{'port': 8080, 'targetPort': 8080}]}})
    inventory = kjson('get', 'deployment', 'inventory', '-n', NS)
    sidecar = deepcopy(next(item for item in inventory['spec']['template']['spec']['containers'] if item['name'] == 'daprd'))
    if sidecar['image'] != 'ghcr.io/dapr/daprd:1.18.1':
        raise RuntimeError('installed-dapr-version-mismatch')
    for flag in ('--app-id', '--config'):
        sidecar['args'][sidecar['args'].index(flag) + 1] = APP
    for env in sidecar['env']:
        if env['name'] in ('APP_API_TOKEN', 'DAPR_API_TOKEN'):
            env['valueFrom']['secretKeyRef']['name'] = APP + ('-app-' if env['name'] == 'APP_API_TOKEN' else '-dapr-') + installer.run['runId'][:12]
    required = {item['name'] for item in sidecar['volumeMounts']}
    original_volumes = {item['name']: item for item in inventory['spec']['template']['spec']['volumes']}
    # Sidecar gets the new provider credential and public CA, plus fresh projected Kubernetes/Dapr identity.
    volumes = []
    for name in required:
        volume = deepcopy(original_volumes[name])
        if 'secret' in volume:
            if volume['secret']['secretName'] != 'ext-secrets-1-hmac-data-token':
                raise RuntimeError('unexpected-existing-sidecar-secret-mount')
            volume['secret']['secretName'] = data['secretName']
        volumes.append(volume)
    provider_name = next((v['name'] for v in volumes if v.get('secret', {}).get('secretName') == data['secretName']), 'custody-provider')
    if not any(v['name'] == provider_name for v in volumes):
        volumes.append({'name': provider_name, 'secret': {'secretName': data['secretName'], 'defaultMode': 288}})
        sidecar['volumeMounts'].append({'name': provider_name, 'mountPath': '/var/run/ext-secrets-1/provider', 'readOnly': True})
    if not any(item['mountPath'] == '/var/run/ext-secrets-1/ca' for item in sidecar['volumeMounts']):
        volumes.append({'name': 'custody-provider-ca', 'configMap': {'name': 'ext-secrets-1-openbao-ca'}})
        sidecar['volumeMounts'].append({'name': 'custody-provider-ca', 'mountPath': '/var/run/ext-secrets-1/ca', 'readOnly': True})
    volumes += [{'name': 'runtime', 'configMap': {'name': runtime_name}}, {'name': 'allowlist', 'configMap': {'name': 'ext-secrets-1-inventory-permitted-references'}},
        {'name': 'idp-ca', 'configMap': {'name': 'ext-secrets-1-registry-idp-ca'}}, {'name': 'authority-identity', 'secret': {'secretName': identity['secretName'], 'defaultMode': 288}},
        {'name': 'tmp', 'emptyDir': {}}]
    env = [{'name': name, 'value': value} for name, value in {
        'ASPNETCORE_ENVIRONMENT': 'Production', 'ASPNETCORE_HTTP_PORTS': '8080', 'Logging__LogLevel__Default': 'Warning',
        'Custody__RuntimeReferenceFile': '/var/run/custody-runtime/runtime-reference.json', 'Custody__PermittedReferencesFile': '/var/run/custody-allowlist/permitted-references.json',
        'Custody__CallerIssuer': ISSUER, 'Custody__IssuerCaFile': '/var/run/custody-idp-ca/ca.pem',
        'Inventory__WorkloadTokenEndpoint': ISSUER + '/protocol/openid-connect/token', 'Inventory__WorkloadClientId': CUSTODY_CLIENT,
        'Inventory__WorkloadCredentialFile': '/var/run/custody-authority/client-secret', 'Inventory__WorkloadCaFile': '/var/run/custody-idp-ca/ca.pem'}.items()]
    env += [env_secret('APP_API_TOKEN', APP + '-app-' + installer.run['runId'][:12], 'token'), env_secret('DAPR_API_TOKEN', APP + '-dapr-' + installer.run['runId'][:12], 'token')]
    app = {'name': 'custody', 'image': installer.image, 'imagePullPolicy': 'IfNotPresent', 'env': env,
        'ports': [{'containerPort': 8080}], 'securityContext': {'allowPrivilegeEscalation': False, 'readOnlyRootFilesystem': True, 'capabilities': {'drop': ['ALL']}},
        'resources': {'requests': {'cpu': '100m', 'memory': '96Mi'}, 'limits': {'cpu': '1', 'memory': '256Mi'}},
        'volumeMounts': [{'name': name, 'mountPath': target, 'readOnly': True} for name, target in [
            ('runtime', '/var/run/custody-runtime'), ('allowlist', '/var/run/custody-allowlist'), ('idp-ca', '/var/run/custody-idp-ca'), ('authority-identity', '/var/run/custody-authority')]]
            + [{'name': 'tmp', 'mountPath': '/tmp'}],
        'readinessProbe': {'httpGet': {'path': '/alive', 'port': 8080}, 'periodSeconds': 5, 'timeoutSeconds': 3},
        'livenessProbe': {'httpGet': {'path': '/alive', 'port': 8080}, 'periodSeconds': 10, 'timeoutSeconds': 3}}
    deployment = {'apiVersion': 'apps/v1', 'kind': 'Deployment', 'metadata': {'name': APP}, 'spec': {'replicas': 1,
        'selector': {'matchLabels': {'app': APP}}, 'template': {'metadata': {'labels': {'app': APP}, 'annotations': {
            'dapr.io/enabled': 'true', 'dapr.io/app-id': APP, 'dapr.io/app-port': '8080', 'dapr.io/config': APP,
            'dapr.io/app-token-secret': APP + '-app-' + installer.run['runId'][:12], 'dapr.io/api-token-secret': APP + '-dapr-' + installer.run['runId'][:12]}},
        'spec': {'serviceAccountName': APP, 'automountServiceAccountToken': False,
            'securityContext': {'runAsNonRoot': True, 'runAsUser': 1654, 'runAsGroup': 1654, 'fsGroup': 1654, 'seccompProfile': {'type': 'RuntimeDefault'}},
            'containers': [app, sidecar], 'volumes': volumes}}}}
    installer.create(deployment)
    # No authority app credential belongs in inventory: its outbound identity is the separate query client.
    inventory_runtime = kjson('get', 'configmap', 'ext-secrets-1-inventory-runtime', '-n', NS)
    inventory_runtime['data']['runtime-reference.json'] = json.dumps(public, sort_keys=True)
    replace_public(installer, 'ConfigMap', 'ext-secrets-1-inventory-runtime', inventory_runtime)
    invapp = next(item for item in inventory['spec']['template']['spec']['containers'] if item['name'] == 'inventory')
    invapp['image'] = installer.inventory_image
    additions = {'Inventory__CallerAuthority': ISSUER, 'Inventory__EnrolledCallerIds__0': CUSTODY_CLIENT,
        'Inventory__WorkloadTokenEndpoint': ISSUER + '/protocol/openid-connect/token', 'Inventory__WorkloadClientId': QUERY_CLIENT,
        'Inventory__WorkloadCredentialFile': '/var/run/hmac-authority-query/client-secret', 'Inventory__WorkloadCaFile': '/var/run/registry-idp-ca/ca.pem'}
    invapp['env'] = [item for item in invapp['env'] if item['name'] not in additions] + [{'name': key, 'value': value} for key, value in additions.items()]
    invspec = inventory['spec']['template']['spec']
    for volume in invspec['volumes']:
        if volume.get('secret', {}).get('secretName') == 'ext-secrets-1-inventory-metadata-token':
            volume['secret']['secretName'] = metadata['secretName']
    invspec['volumes'] = [item for item in invspec['volumes'] if item['name'] != 'hmac-authority-query'] + [{'name': 'hmac-authority-query', 'secret': {'secretName': AUTHORITY + '-query-identity', 'defaultMode': 288}}]
    invapp['volumeMounts'] = [item for item in invapp['volumeMounts'] if item['name'] != 'hmac-authority-query'] + [{'name': 'hmac-authority-query', 'mountPath': '/var/run/hmac-authority-query', 'readOnly': True}]
    # Inventory has no need for a data credential; its Dapr component remains scoped only to custody.
    for volume in invspec['volumes']:
        if volume.get('secret', {}).get('secretName') == 'ext-secrets-1-hmac-data-token':
            raise RuntimeError('inventory-unexpected-data-credential-mount')
    for name, callers, methods in [(INVENTORY, [APP], ['query-inventory']), (AUTHORITY, [INVENTORY, APP], ['query-profile', 'query-hmac-revocation'])]:
        config = kjson('get', 'configuration', name, '-n', NS)
        config['spec']['accessControl']['policies'] = [{'appId': caller, 'namespace': NS, 'trustDomain': 'cluster.local', 'defaultAction': 'deny',
            'operations': [{'name': '/' + method, 'httpVerb': ['POST'], 'action': 'allow'} for method in (methods if caller == INVENTORY else [m for m in methods if m != 'query-hmac-revocation'])]} for caller in callers]
        replace_public(installer, 'Configuration', name, config)
    # Load the updated ACL in the replacement sidecar.
    replace_public(installer, 'Deployment', 'inventory', inventory)
    return public


def enroll(installer, profile, plan):
    # Host identities are actual UID-qualified controllers, never proposed fixtures.
    for app, name, client in [(INVENTORY, 'inventory', QUERY_CLIENT), (APP, APP, CUSTODY_CLIENT)]:
        obj = kjson('get', 'deployment', name, '-n', NS)
        workload = NS + '/Deployment/' + name + '@uid:' + obj['metadata']['uid']
        # owner_operation's filename is operation-specific; retain separate originals for repeated kinds.
        administer(installer, 'host-' + app, 'SetInventoryHost', host={'inventoryAuthorityReference': app, 'callerId': client,
            'workloadReference': workload, 'notBefore': profile['notBefore'], 'validUntil': profile['validUntil'], 'enrolled': True})
        administer(installer, 'caller-' + client, 'SetCaller', caller={'callerId': client, 'notBefore': profile['notBefore'],
            'validUntil': profile['validUntil'], 'enabled': True})
    for item in plan:
        old = item['original']
        administer(installer, 'key-' + old['tenantId'] + '-' + old['purpose'], 'SetKeyStatus', keyStatus={
            'tenantId': old['tenantId'], 'purpose': old['purpose'], 'version': item['opaqueVersion'], 'profileVersion': profile['version'],
            'notBefore': item['mapping']['notBefore'], 'validUntil': min(instant(item['mapping']['verifyUntil']), instant(profile['validUntil'])).isoformat(), 'revoked': False})
    _, state = administration_request(installer, 'GET', '/admin/state')
    installer.write('owner-administered-state.json', state)


def administer(installer, suffix, kind, **record):
    path = installer.evidence / ('owner-operation-' + suffix + '.json')
    if path.exists():
        journal = json.loads(path.read_text())
        if journal['operation']['kind'] != kind or any(journal['operation'][key] != value for key, value in record.items()):
            raise RuntimeError('original-owner-operation-payload-changed')
    else:
        _, state = administration_request(installer, 'GET', '/admin/state')
        journal = {'phase': 'Prepared', 'operation': {'operationId': uuid.uuid4().hex, 'expectedRevision': state['revision'],
            'kind': kind, 'profile': None, 'caller': None, 'host': None, 'keyStatus': None}, 'preparedAt': timestamp()}
        journal['operation'].update(record)
        durable_write(path, journal, create_only=True)
    status, receipt = administration_request(installer, 'POST', '/admin/operations', journal['operation'])
    if status != 200 or receipt['outcome'] != 'Applied' or receipt['operationId'] != journal['operation']['operationId']:
        raise RuntimeError('original-owner-administration-not-applied')
    journal.update({'phase': 'Committed', 'receipt': receipt})
    durable_write(path, journal)


def http_health(service, port):
    result = {}
    with forward(service, port, 8080):
        for path in ('/alive', '/health', '/ready'):
            connection = http.client.HTTPConnection('127.0.0.1', port, timeout=40)
            try:
                connection.request('GET', path)
                response = connection.getresponse()
                raw = response.read(65537)
                if len(raw) > 65536:
                    raise RuntimeError('health-response-too-large')
                result[path] = {'status': response.status, 'body': json.loads(raw) if raw else None}
            finally:
                connection.close()
    return result


def verify(installer, plan):
    before = json.loads((installer.evidence / 'preserved-kubernetes-before.json').read_text())
    after = snapshot()
    verify_final_public_intents(installer, before, after)
    changed = [key for key, value in before.items() if after.get(key) != value]
    unexpected = sorted(set(changed) - MUTABLE)
    openbao_same = json.loads((installer.evidence / 'preserved-openbao-before.json').read_text()) == installer.openbao_snapshot()
    preserve_original_journals(installer)
    for item in plan:
        observed = installer.metadata(item['original']['secretReference'])
        if observed != item['afterMetadata']:
            raise RuntimeError('current-successor-metadata-changed-before-binding-completion')
    health = {'observedAt': timestamp(), 'custody': http_health(APP, 38453), 'inventory': http_health('inventory', 38454),
        'authority': http_health(AUTHORITY, 38455), 'dependencyAvailable': False, 'qualificationPassed': False, 'liveRun': False, 'contractOperations': 0}
    record_observation(installer, 'health.json', health)
    register_same = sha(REGISTER.read_bytes()) == installer.run['agentsRegisterSha256']
    result = {'observedAt': timestamp(), 'preexistingKubernetesObjectCount': len(before), 'authorizedUpdatedReferences': sorted(set(changed) & MUTABLE),
        'unexpectedChangedPreexistingReferences': unexpected, 'openbaoMountAuthAndPoliciesUnchanged': openbao_same,
        'originalJournalsUnchanged': True, 'originalKeyMaterialAndValidityMetadataPreserved': True, 'successorCount': len(plan),
        'nativeSuccessorWriteCount': len([item for item in plan if item.get('nativeWriteReceipt')]), 'agentsRegisterUnchanged': register_same,
        'providerDataReads': 0, 'contractOperations': 0, 'liveRun': False, 'dependencyAvailable': False}
    record_observation(installer, 'preservation.json', result)
    if unexpected or not openbao_same or not register_same:
        raise RuntimeError('preservation-verification-failed')
    if any(health[name]['/ready']['status'] != 200 for name in ('custody', 'inventory', 'authority')):
        raise RuntimeError('current-metadata-readiness-not-established')
    return health


def verify_final_public_intents(installer, before, after):
    final_journals = {
        'Deployment/' + NS + '/inventory': 'update-deployment-inventory-canonical-runtime-file.json',
        'ConfigMap/' + NS + '/ext-secrets-1-inventory-runtime': 'update-configmap-ext-secrets-1-inventory-runtime.json',
        'Configuration/' + NS + '/ext-secrets-1-inventory': 'update-configuration-ext-secrets-1-inventory.json',
        'Configuration/' + NS + '/' + AUTHORITY: 'update-configuration-' + AUTHORITY + '.json',
    }
    owned_key = 'Deployment/' + NS + '/' + APP
    owned = next((item for item in getattr(installer, 'resources', []) if item['reference'] == owned_key and item['phase'] == 'Created'), None)
    if owned:
        final_journals[owned_key] = 'update-deployment-' + APP + '-canonical-runtime-file.json'
    for key, name in final_journals.items():
        original = before.get(key) or (owned if key == owned_key else None)
        if not original or after.get(key, {}).get('uid') != original['uid']:
            raise RuntimeError('final-isolated-resource-original-uid-mismatch:' + key)
        path = installer.evidence / name
        if not path.exists():
            raise RuntimeError('final-isolated-public-intent-journal-required:' + key)
        journal = json.loads(path.read_text())
        if journal.get('uid') != original['uid'] or journal.get('reference') != key or journal.get('phase') not in ('Replaced', 'ReconciledCurrent') or 'intendedPublicObject' not in journal:
            raise RuntimeError('final-isolated-public-intent-unverified:' + key)
        kind, namespace, resource_name = key.split('/')
        observed = kjson('get', kind, resource_name, '-n', namespace)
        if observed['metadata']['uid'] != original['uid'] or public_object(observed) != public_object(journal['intendedPublicObject']):
            raise RuntimeError('unjournaled-final-isolated-resource-drift:' + key)


def record_observation(installer, name, value):
    current = installer.evidence / name
    if current.exists():
        durable_write(installer.evidence / (current.stem + '-prior-' + uuid.uuid4().hex + '.json'), json.loads(current.read_text()), create_only=True)
    installer.write(name, value)


def blockers(installer, public):
    prior = json.loads((ORIGINAL_AUTHORITY / 'blockers.json').read_text())
    known = {item['input']: item for item in prior['blockers']}
    missing = []
    def walk(value, prefix=''):
        if isinstance(value, dict):
            for key, item in value.items():
                walk(item, prefix + '.' + key if prefix else key)
        elif value is None:
            missing.append(prefix)
    walk(public)
    values = [known.get(name, {'input': name, 'owner': 'EXT-SECRETS-1 owning service administrator',
        'action': 'Supply the actual independently enrolled current reference; preserve null until the owner verifies it.'}) for name in sorted(missing)]
    installer.write('blockers.json', {'nullReferenceCount': len(missing), 'blockers': values, 'dependencyAvailable': False, 'liveRun': False, 'contractOperations': 0})


def exposure_check(installer):
    # Compare values only in memory; credential hashes and values are never retained.
    secret_names = kube('get', 'secrets', '-n', NS, '-o', 'jsonpath={range .items[*]}{.metadata.name}{"\\n"}{end}').stdout.decode().splitlines()
    for name in secret_names:
        raw = kube('get', 'secret', name, '-n', NS, '-o', 'jsonpath={.data}').stdout
        for field, value in json.loads(raw or b'{}').items():
            if field in ('ca.crt', 'tls.crt', 'client-id', 'username', 'subject-id'):
                continue
            decoded = base64.b64decode(value, validate=True)
            if len(decoded) >= 16:
                installer.private_values.append(decoded)
    candidates = [path for path in installer.evidence.rglob('*') if path.is_file()]
    candidates += [ROOT / 'eng/ext-secrets-1/bind_isolated_custody.py', RUNTIME]
    matches = []
    for path in candidates:
        content = path.read_bytes()
        if any(value in content or base64.b64encode(value) in content for value in installer.private_values if len(value) >= 16):
            matches.append(str(path))
    installer.write('credential-exposure-check.json', {'literalAndBase64Matches': len(matches), 'scannedFileCount': len(candidates),
        'credentialValuesRecorded': False, 'credentialHashesRecorded': False, 'passed': not matches})
    if matches:
        raise RuntimeError('credential-exposure-check-failed')


def provider_ingress_manifest(run_id):
    return {'apiVersion': 'networking.k8s.io/v1', 'kind': 'NetworkPolicy',
        'metadata': {'name': APP + '-provider-' + run_id[:12], 'namespace': 'openbao',
            'labels': {'hexalith.io/custody-binding': run_id}},
        'spec': {'podSelector': {'matchLabels': {'app.kubernetes.io/instance': 'hexalith-keys',
            'app.kubernetes.io/name': 'openbao', 'component': 'server'}}, 'policyTypes': ['Ingress'],
            'ingress': [{'from': [{'namespaceSelector': {'matchLabels': {'kubernetes.io/metadata.name': NS}},
                'podSelector': {'matchExpressions': [{'key': 'app', 'operator': 'In', 'values': ['inventory', APP]}]}}],
                'ports': [{'protocol': 'TCP', 'port': 8200}]}]}}


def ensure_provider_ingress(installer):
    """A new exact-scope ingress resource; every existing provider policy/spec is preserved."""
    manifest = provider_ingress_manifest(installer.run['runId'])
    selector = manifest['spec']['podSelector']['matchLabels']
    server = kjson('get', 'statefulset', 'hexalith-keys', '-n', 'openbao')
    pods = kjson('get', 'pods', '-n', 'openbao', '-l', ','.join(key + '=' + value for key, value in sorted(selector.items())))['items']
    if len(pods) != 3 or server.get('status', {}).get('readyReplicas') != 3 or any(
        not any(owner.get('kind') == 'StatefulSet' and owner.get('uid') == server['metadata']['uid'] for owner in pod['metadata'].get('ownerReferences', []))
        or any(pod['metadata'].get('labels', {}).get(key) != value for key, value in selector.items()) for pod in pods):
        raise RuntimeError('actual-openbao-server-selector-prerequisite-mismatch')
    path = installer.evidence / 'provider-ingress-resource-journal.json'
    name = manifest['metadata']['name']
    if path.exists():
        journal = json.loads(path.read_text())
        if journal['intent'] != manifest:
            raise RuntimeError('original-provider-ingress-intent-changed')
        observed = kjson('get', 'networkpolicy', name, '-n', 'openbao')
        if public_object(observed) != public_object(manifest) or journal.get('uid', observed['metadata']['uid']) != observed['metadata']['uid']:
            raise RuntimeError('original-provider-ingress-outcome-needs-reconciliation')
    else:
        if kube('get', 'networkpolicy', name, '-n', 'openbao', '--ignore-not-found', '-o', 'name').stdout.strip():
            raise RuntimeError('provider-ingress-name-overlap')
        journal = {'phase': 'CreationStarted', 'intent': manifest, 'startedAt': timestamp(),
            'serverStatefulSetUid': server['metadata']['uid'], 'verifiedServerPodUids': [pod['metadata']['uid'] for pod in pods],
            'existingProviderPoliciesAndWorkloadsChanged': False}
        durable_write(path, journal, create_only=True)
        kube('create', '-f', '-', '-o', 'name', data=canonical(manifest))
        observed = kjson('get', 'networkpolicy', name, '-n', 'openbao')
        if public_object(observed) != public_object(manifest):
            raise RuntimeError('created-provider-ingress-readback-mismatch')
    journal.update({'phase': 'Created', 'uid': observed['metadata']['uid'], 'resourceVersion': observed['metadata']['resourceVersion'],
        'reference': 'openbao/NetworkPolicy/' + name + '@uid:' + observed['metadata']['uid']})
    installer.write(path.name, journal)


def install(installer):
    cleanup = installer.evidence / 'provisioner-policy-journal.json'
    if cleanup.exists() and json.loads(cleanup.read_text())['phase'] == 'Removed':
        # Final verification can resume after completed provisioning cleanup;
        # it never reopens issuance, native writes, enrollment or workload updates.
        preserve_original_journals(installer)
        if not (installer.evidence / 'preserved-kubernetes-before.json').exists() or not (installer.evidence / 'preserved-openbao-before.json').exists():
            raise RuntimeError('original-preservation-baseline-required-before-ingress')
        for role in ('data', 'metadata'):
            issuance = installer.evidence / (role + '-credential-issuance.json')
            if not issuance.exists() or json.loads(issuance.read_text())['phase'] != 'Stored':
                raise RuntimeError('original-current-credential-journal-required-no-reissuance')
        data = installer.credential('data', DATA_POLICY)
        metadata = installer.credential('metadata', METADATA_POLICY)
        plan = json.loads((installer.evidence / 'successor-key-plan.json').read_text())
        if len(plan) != 4 or any(item['phase'] != 'MetadataVerified' for item in plan):
            raise RuntimeError('completed-provisioning-plan-not-exactly-four-verified-successors')
        if [item['original'] for item in plan] != original_plan():
            raise RuntimeError('original-four-successor-targets-required-before-ingress')
        for item in plan:
            observed = installer.metadata(item['original']['secretReference'])
            validate_original(item, observed, successor=True)
            if observed != item['afterMetadata']:
                raise RuntimeError('current-successor-metadata-required-before-ingress')
        identity = json.loads((installer.evidence / 'custody-identity-journal.json').read_text())
        public = json.loads((installer.evidence / 'runtime-reference.pending.json').read_text())
        ensure_provider_ingress(installer)
        finalize(installer, plan, public, data, metadata, identity)
        return
    with forward('registry-idp', 38444, 8443), forward(AUTHORITY, 38452, 8080):
        profile = inspect(installer)
        installer.phase('ProvisioningCurrentRestrictedCredentials')
        data = installer.credential('data', DATA_POLICY)
        metadata = installer.credential('metadata', METADATA_POLICY)
        ensure_provider_ingress(installer)
        installer.phase('SuccessorProvisioning')
        plan = successors(installer)
        installer.phase('ProvisioningCustodyIdentity')
        identity = provision_custody_identity(installer)
        for image in (installer.image, installer.inventory_image):
            done = installer.evidence / ('preload-complete-' + sha(image.encode())[:12] + '.json')
            if not done.exists():
                preload(installer, image)
                durable_write(done, {'image': image, 'temporaryProvisioningCredentialRemoved': True}, create_only=True)
        installer.phase('InstallingAndEnrollingCustody')
        configured = installer.evidence / 'workload-configuration-complete.json'
        if configured.exists():
            public = json.loads((installer.evidence / 'runtime-reference.pending.json').read_text())
        else:
            public = configure_workloads(installer, profile, plan, data, metadata, identity)
            durable_write(configured, {'completedAt': timestamp(), 'initialUpdateJournalsRetained': True}, create_only=True)
        enroll(installer, profile, plan)
        mount_regular_runtime_files(installer)
        # Dapr loads access control at startup; restart only the already authorized isolated authority pod.
        # StatefulSet controller/spec and all persisted state remain unchanged.
        restart_authority(installer)
    for kind, name in [('statefulset', AUTHORITY), ('deployment', 'inventory'), ('deployment', APP)]:
        kube('rollout', 'status', kind + '/' + name, '-n', NS, '--timeout=50s', timeout=55)
    installer.phase('VerifyingCurrentMetadataAndHealth')
    installer.remove_provisioner()
    finalize(installer, plan, public, data, metadata, identity)


def finalize(installer, plan, public, data, metadata, identity):
    installer.phase('VerifyingCurrentMetadataAndHealth')
    verify(installer, plan)
    durable_write(RUNTIME, public)
    installer.write('runtime-reference.json', public)
    blockers(installer, public)
    installer.write('credential-storage-boundaries.json', {'dataCredentialReference': data['credentialReference'],
        'metadataCredentialReference': metadata['credentialReference'], 'custodyQueryCredentialReference': identity['credentialReference'],
        'inventoryQueryCredentialReference': NS + '/Secret/' + AUTHORITY + '-query-identity',
        'administratorCredentialReference': NS + '/Secret/' + AUTHORITY + '-owner-identity', 'operatorCredentialsMountedInApplications': False,
        'authorityPersistentStorageSeparate': True, 'registryCredentialsGrantSecretsAuthority': False, 'decisionSignerInstalled': False})
    exposure_check(installer)
    installer.phase('InstalledMetadataBoundDependencyUnavailable')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--custody-image', required=True)
    parser.add_argument('--inventory-image', required=True)
    parser.add_argument('--evidence', type=Path)
    args = parser.parse_args()
    exact_image(args.custody_image, APP)
    exact_image(args.inventory_image, INVENTORY)
    os.umask(0o077)
    evidence = args.evidence or EVIDENCE_ROOT / ('custody-binding-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'))
    evidence.mkdir(parents=False, exist_ok=args.evidence is not None)
    print('evidence=' + str(evidence), flush=True)
    installer = None
    try:
        with journal_lock(Path('/tmp/ext-secrets-1-custody-binding.lock')), journal_lock(evidence / 'installation.lock'):
            run_path = evidence / 'run.json'
            if run_path.exists() and json.loads(run_path.read_text())['phase'] == 'InstalledMetadataBoundDependencyUnavailable':
                print('original-custody-binding-complete; no effects repeated; use read-only verification for current health', flush=True)
                return 0
            installer = CustodyInstaller(evidence, args.custody_image, args.inventory_image)
            install(installer)
            installer.close()
            installer = None
        print('isolated-custody-metadata-bound; dependency remains unavailable; Live and contract cases unrun', flush=True)
        return 0
    except Exception as error:
        failure = {'observedAt': timestamp(), 'exceptionType': type(error).__name__,
            'reason': str(error) if isinstance(error, RuntimeError) else 'sanitized-custody-binding-failure',
            'owner': 'Isolated provider, custody and authority administrators',
            'action': 'Reconcile the exact retained original journal before continuing; never repeat an unknown native key write.',
            'originalJournalsRetained': True, 'dependencyAvailable': False, 'liveRun': False, 'contractOperations': 0}
        failure_path = evidence / 'installation-failure.json'
        if failure_path.exists():
            durable_write(evidence / ('prior-installation-failure-' + uuid.uuid4().hex + '.json'), json.loads(failure_path.read_text()), create_only=True)
        durable_write(failure_path, failure)
        print('custody-binding-incomplete; preserve original journals and reconcile named effect', flush=True)
        return 1
    finally:
        if installer is not None:
            try:
                installer.close()
            except Exception:
                durable_write(evidence / ('credential-cleanup-failure-' + uuid.uuid4().hex + '.json'), {
                    'observedAt': timestamp(), 'owner': 'Provider credential administrator',
                    'action': 'Reconcile the original revocation/Secret/policy cleanup journals; do not repeat an uncertain operation.',
                    'originalJournalsRetained': True, 'dependencyAvailable': False})


if __name__ == '__main__':
    sys.exit(main())
