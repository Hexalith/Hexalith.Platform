#!/usr/bin/env python3
"""Journaled, create-only installation of the owner-approved isolated HMAC authority.

No provider key or existing policy is written. The new realm establishes only this
authority's query/administration identities. Expired keys withhold consumer enrollment.
Credentials travel through bounded stdin/HTTPS and intended Kubernetes Secrets only.
"""
import argparse
import base64
from contextlib import contextmanager
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import http.client
import json
import os
from pathlib import Path
import re
import secrets
import socket
import subprocess
import sys
import time
import uuid

from install_isolated_registry import Installer, KUBE, NS, ROOT, REGISTER, canonical, creation_identity, env_secret, kube, kjson, reference, sha, snapshot
from native_journal import durable_write, journal_lock
from preload_isolated_image import preload
from provision_registry_identity import Idp, IDP_HOST, audience, hardcoded
from install_current_inventory import application_request, cleanup_journaled_credential, read_pinned_credential_secret, secret_identity

APP = 'ext-secrets-1-hmac-authority'
REALM = APP
ISSUER = 'https://' + IDP_HOST + ':8443/realms/' + REALM
QUERY_CLIENT = 'ext-secrets-1-hmac-inventory'
ADMIN_CLIENT = 'ext-secrets-1-hmac-owner'
RUNTIME = Path('/home/administrator/hexalith-secrets-evidence/ext-secrets-1/isolated-runtime-20261016/runtime-reference.json')
APPROVAL = Path('/home/administrator/hexalith-secrets-evidence/ext-secrets-1/custody-inventory-20261016/authority-discovery-20261027T171614Z/approval.json')
OWNER_MAP = ROOT / 'eng/ext-secrets-1/provisioning/owner-input-map.json'
# Match AuthorityRepository.MaximumStateBytes, including the complete retained audit.
ADMINISTRATION_RESPONSE_LIMIT = 16 * 1024 * 1024


def timestamp():
    return datetime.now(timezone.utc).isoformat()


def exact_image(value):
    if not re.fullmatch(r'registry\.hexalith\.com/ext-secrets-1-hmac-authority@sha256:[0-9a-f]{64}', value):
        raise ValueError('An exact isolated authority image digest is required.')
    return value


def declared_intent_matches(declared, observed):
    if isinstance(declared, dict):
        return isinstance(observed, dict) and all(key in observed and declared_intent_matches(value, observed[key]) for key, value in declared.items())
    if isinstance(declared, list):
        return isinstance(observed, list) and len(declared) == len(observed) and all(declared_intent_matches(a, b) for a, b in zip(declared, observed))
    return type(declared) is type(observed) and declared == observed


def secret_public_intent(obj):
    return {key: deepcopy(value) for key, value in obj.items() if key not in ('data', 'stringData')} | {
        'dataKeys': sorted(set(obj.get('data', {})) | set(obj.get('stringData', {})))}


def normalized_created_public_object(obj):
    public = {key: deepcopy(value) for key, value in obj.items() if key not in ('metadata', 'status')}
    public['metadata'] = {key: deepcopy(obj['metadata'][key]) for key in ('name', 'namespace', 'labels', 'annotations') if key in obj['metadata']}
    if obj.get('kind') == 'Deployment' and 'annotations' in public['metadata']:
        public['metadata']['annotations'].pop('deployment.kubernetes.io/revision', None)
        if not public['metadata']['annotations']:
            del public['metadata']['annotations']
    return public


@contextmanager
def forward(service, local, remote):
    process = subprocess.Popen(KUBE + ['port-forward', '-n', NS, 'service/' + service, str(local) + ':' + str(remote), '--address=127.0.0.1'],
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        for _ in range(100):
            if process.poll() is not None:
                raise RuntimeError('isolated-forward-unavailable:' + service)
            try:
                with socket.create_connection(('127.0.0.1', local), timeout=0.1):
                    break
            except OSError:
                time.sleep(0.1)
        else:
            raise RuntimeError('isolated-forward-timeout:' + service)
        yield
    finally:
        if process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=3)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=3)


class AuthorityInstaller(Installer):
    def __init__(self, evidence, image):
        super().__init__(evidence)
        self.image = exact_image(image)
        self.metadata_token = None
        self.metadata_credential_verified = False
        self.run_path = self.evidence / 'run.json'
        if self.run_path.exists():
            self.run = json.loads(self.run_path.read_text())
            if self.run['image'] != image:
                raise RuntimeError('original-installation-image-mismatch')
        else:
            self.run = {'schema': 'EXT-SECRETS-1.authority-installation.v1', 'runId': uuid.uuid4().hex, 'startedAt': timestamp(),
                        'image': image, 'phase': 'Prepared', 'approvalSha256': sha(APPROVAL.read_bytes()),
                        'contractOperations': 0, 'liveRun': False, 'providerKeyWrites': 0, 'dependencyAvailable': False}
            durable_write(self.run_path, self.run, create_only=True)
        self.journal_path = self.evidence / 'resource-journal.json'
        self.resources = json.loads(self.journal_path.read_text()) if self.journal_path.exists() else []
        self.created = [item['reference'] for item in self.resources if item['phase'] == 'Created']

    def write(self, name, value):
        durable_write(self.evidence / name, value)

    def phase(self, phase):
        self.run['phase'] = phase
        self.write('run.json', self.run)

    def metadata_credential(self):
        path = self.evidence / 'metadata-observer-issuance.json'
        name = APP + '-metadata-observer-token'
        if path.exists():
            journal = json.loads(path.read_text())
            if journal['phase'] != 'Stored':
                raise RuntimeError('original-metadata-observer-issuance-needs-reconciliation')
            candidate = read_pinned_credential_secret(name, journal.get('secretIdentity'))
            try:
                lookup = json.loads(application_request(candidate, 'token', 'lookup', '-format=json').stdout)['data']
                if lookup.get('accessor') != journal.get('accessor') or not journal.get('accessor'):
                    raise RuntimeError('original-metadata-observer-accessor-changed')
                self.metadata_token = candidate
            finally:
                if self.metadata_token is not candidate:
                    candidate[:] = b'\0' * len(candidate)
        else:
            journal = {'phase': 'IssuanceStarted', 'issuanceId': uuid.uuid4().hex, 'startedAt': timestamp(),
                'policy': 'ext-secrets-1-inventory-metadata-read', 'ttlSeconds': 1200, 'maximumTtlSeconds': 1200,
                'renewable': False, 'secretReference': NS + '/Secret/' + name + '#token', 'existingPolicyChanged': False,
                'secretCreationPhase': 'NotStarted'}
            self.write('metadata-observer-issuance.json', journal)
            try:
                result = json.loads(self.bao('write', '-format=json', 'auth/token/create-orphan', '-', data=canonical({
                    'policies': [journal['policy']], 'no_default_policy': True, 'display_name': APP + '-' + journal['issuanceId'],
                    'ttl': '1200s', 'explicit_max_ttl': '1200s', 'renewable': False})).stdout)['auth']
                self.metadata_token = bytearray(result['client_token'].encode())
                journal.update({'phase': 'Issued', 'accessor': result['accessor'], 'leaseDuration': result['lease_duration'], 'issuedAt': timestamp()})
                self.write('metadata-observer-issuance.json', journal)
                journal['secretCreationPhase'] = 'Started'
                self.write('metadata-observer-issuance.json', journal)
                journal['secretIdentity'] = self.secret(name, {'token': bytes(self.metadata_token).decode()})
                journal.update({'phase': 'Stored', 'secretCreationPhase': 'Created'})
                self.write('metadata-observer-issuance.json', journal)
            except Exception:
                journal['phase'] = 'IssuanceUnknown' if self.metadata_token is None else 'CredentialCleanupPending'
                self.write('metadata-observer-issuance.json', journal)
                raise
        lookup = json.loads(application_request(self.metadata_token, 'token', 'lookup', '-format=json').stdout)['data']
        if lookup.get('accessor') != journal['accessor'] or lookup['policies'] != ['ext-secrets-1-inventory-metadata-read']:
            raise RuntimeError('metadata-observer-policy-mismatch')
        for forbidden in ['ext-secrets-1-isolated/data/*', 'secret/data/*', 'sys/policies/acl/*']:
            if json.loads(application_request(self.metadata_token, 'token', 'capabilities', '-format=json', forbidden).stdout) != ['deny']:
                raise RuntimeError('metadata-observer-excess-permission')
        self.metadata_credential_verified = True

    def close(self):
        try:
            path = self.evidence / 'metadata-observer-issuance.json'
            if path.exists() and json.loads(path.read_text()).get('accessor'):
                cleanup_journaled_credential(self, path, APP + '-metadata-observer-token', self.metadata_token)
        finally:
            if self.metadata_token is not None:
                self.metadata_token[:] = b'\0' * len(self.metadata_token)
                self.metadata_token = None
            super().close()

    def create(self, obj):
        obj = deepcopy(obj)
        metadata = obj.setdefault('metadata', {})
        metadata.setdefault('namespace', NS)
        metadata.setdefault('labels', {})['hexalith.io/authority-installation'] = self.run['runId']
        ref = reference(obj)
        original = next((item for item in self.resources if item['reference'] == ref), None)
        if original:
            if original['phase'] != 'Created':
                raise RuntimeError('original-resource-effect-unknown:' + ref)
            identity = original.get('creationIdentity')
            if obj['kind'] == 'Secret':
                if not identity or secret_identity(metadata['name']) != identity:
                    raise RuntimeError('original-resource-identity-changed:' + ref)
                if original.get('secretPublicIntent') != secret_public_intent(obj):
                    raise RuntimeError('original-secret-declared-intent-unavailable-or-changed:' + ref)
            else:
                if 'declaredPublicIntent' not in original or 'createdPublicIntent' not in original or original['declaredPublicIntent'] != obj:
                    raise RuntimeError('original-resource-declared-intent-unavailable-or-changed:' + ref)
                observed = kjson('get', obj['kind'], metadata['name'], '-n', NS)
                if not identity or observed['metadata']['uid'] != identity['uid'] or normalized_created_public_object(observed) != original['createdPublicIntent']:
                    raise RuntimeError('original-resource-identity-or-intent-changed:' + ref)
            return deepcopy(identity)
        if kube('get', obj['kind'], metadata['name'], '-n', NS, '--ignore-not-found', '-o', 'name').stdout.strip():
            raise RuntimeError('preserved-resource-name-overlap:' + ref)
        original = {'reference': ref, 'kind': obj['kind'], 'name': metadata['name'], 'phase': 'CreationStarted', 'startedAt': timestamp(),
                    'runId': self.run['runId']}
        # No Secret value, hash of credential material or Secret object is journaled.
        if obj['kind'] != 'Secret':
            original['intentSha256'] = sha(canonical(obj))
            original['declaredPublicIntent'] = deepcopy(obj)
        else:
            original['secretPublicIntent'] = secret_public_intent(obj)
        self.resources.append(original)
        self.write('resource-journal.json', self.resources)
        try:
            output = 'jsonpath={.metadata.uid}{" "}{.metadata.resourceVersion}' if obj['kind'] == 'Secret' else 'json'
            result = kube('create', '-f', '-', '-o', output, data=canonical(obj))
            if obj['kind'] == 'Secret':
                identity = creation_identity(result.stdout)
            else:
                created = json.loads(result.stdout)
                identity = {key: created['metadata'][key] for key in ('uid', 'resourceVersion')}
                if not declared_intent_matches(obj, created):
                    raise RuntimeError('native-created-resource-differs-from-declared-intent:' + ref)
                original['createdPublicIntent'] = normalized_created_public_object(created)
            original.update({'phase': 'Created', **identity, 'creationIdentity': identity, 'recordedAt': timestamp()})
            self.created.append(ref)
            self.write('resource-journal.json', self.resources)
            self.write('created-resource-references.json', self.created)
            return deepcopy(identity)
        except Exception:
            original['phase'] = 'CreationUnknown'
            self.write('resource-journal.json', self.resources)
            raise


def preserved(installer):
    before = json.loads((installer.evidence / 'preserved-kubernetes-before.json').read_text())
    after = snapshot()
    changed = [ref for ref, original in before.items() if after.get(ref) != original]
    provider_before = json.loads((installer.evidence / 'preserved-openbao-before.json').read_text())
    provider_after = installer.openbao_snapshot()
    keys_before = json.loads((installer.evidence / 'native-key-metadata-before.json').read_text())
    keys_after = native_metadata(installer)
    metadata_verified = bool(keys_before) and all(item.get('metadataSha256') is not None for item in keys_before + keys_after)
    result = {'observedAt': timestamp(), 'preexistingKubernetesObjectCount': len(before), 'changedPreexistingReferences': changed,
              'openbaoMountAuthAndPoliciesUnchanged': provider_before == provider_after,
              'nativeKeyMetadataUnchanged': keys_before == keys_after if metadata_verified else None,
              'nativeMetadataVerificationComplete': metadata_verified,
              'agentsRegisterUnchanged': sha(REGISTER.read_bytes()) == installer.run['agentsRegisterSha256'],
              'ownerInputMapUnchanged': sha(OWNER_MAP.read_bytes()) == installer.run['ownerInputMapSha256'],
              'providerKeyWrites': 0, 'contractOperations': 0, 'liveRun': False}
    installer.write('preservation.json', result)
    if changed or result['nativeKeyMetadataUnchanged'] is False or not all(result[name] for name in ['openbaoMountAuthAndPoliciesUnchanged', 'agentsRegisterUnchanged', 'ownerInputMapUnchanged']):
        raise RuntimeError('preservation-verification-failed')
    return result


def native_metadata(installer):
    allowed = kjson('get', 'configmap', 'ext-secrets-1-inventory-permitted-references', '-n', NS)
    entries = json.loads(allowed['data']['permitted-references.json'])['permittedReferences']
    result = []
    for entry in entries:
        name = entry['secretReference']
        if not re.fullmatch(r'[A-Za-z0-9_-]+(?:/[A-Za-z0-9_-]+)*', name):
            raise RuntimeError('invalid-existing-metadata-reference')
        observation = None if not installer.metadata_credential_verified else application_request(installer.metadata_token, 'read', '-format=json',
            'ext-secrets-1-isolated/metadata/' + name, allowed=(0, 1, 2))
        if observation is None or observation.returncode != 0:
            result.append({'secretReference': name, 'tenantId': entry['tenantId'], 'purpose': entry['purpose'],
                'metadataSha256': None, 'nativeCurrentVersion': None, 'versionMappings': None, 'observation': 'Unavailable',
                'nativeExitCode': observation.returncode if observation is not None else None})
            continue
        document = json.loads(observation.stdout)['data']
        custom = document.get('custom_metadata') or {}
        result.append({'secretReference': name, 'tenantId': entry['tenantId'], 'purpose': entry['purpose'],
                       'metadataSha256': sha(canonical(document)), 'nativeCurrentVersion': document['current_version'],
                       'versionMappings': json.loads(custom['ext-secrets-1.versionMappings']) if custom.get('ext-secrets-1.versionMappings') else None})
    return result


def provision_identity(installer):
    journal = installer.evidence / 'identity-journal.json'
    if journal.exists():
        value = json.loads(journal.read_text())
        if value['phase'] != 'Provisioned':
            raise RuntimeError('original-identity-provisioning-unknown')
        return value
    idp = Idp(installer)
    idp.login()
    original_registry, _ = idp.call('GET', '/admin/realms/ext-secrets-1-registry')
    value = {'phase': 'CreationStarted', 'realm': REALM, 'issuer': ISSUER, 'runId': installer.run['runId'],
             'preservedRegistryRealmSha256': sha(canonical(original_registry)), 'startedAt': timestamp(), 'clients': {}}
    installer.write('identity-journal.json', value)
    try:
        idp.call('POST', '/admin/realms', {'realm': REALM, 'enabled': True, 'sslRequired': 'all', 'registrationAllowed': False,
            'resetPasswordAllowed': False, 'accessTokenLifespan': 120, 'revokeRefreshToken': True,
            'defaultDefaultClientScopes': [], 'defaultOptionalClientScopes': []})
        base = '/admin/realms/' + REALM
        scopes, _ = idp.call('GET', base + '/client-scopes')
        basic = next(item['id'] for item in scopes if item['name'] == 'basic')
        for client, receiver, operation, credential in [
            (QUERY_CLIENT, APP, 'hmac:query', APP + '-query-identity'),
            (ADMIN_CLIENT, APP + '-admin', 'hmac:administer', APP + '-owner-identity')]:
            secret = secrets.token_urlsafe(48)
            _, location = idp.call('POST', base + '/clients', {'clientId': client, 'enabled': True, 'protocol': 'openid-connect',
                'publicClient': False, 'secret': secret, 'serviceAccountsEnabled': True, 'directAccessGrantsEnabled': False,
                'standardFlowEnabled': False, 'fullScopeAllowed': False, 'defaultClientScopes': [], 'optionalClientScopes': [],
                'protocolMappers': [audience(receiver), hardcoded('eventstore:operation', operation)]})
            native = location.rsplit('/', 1)[1]
            value['clients'][client] = {'nativeClientId': native, 'audience': receiver, 'operation': operation,
                'credentialReference': NS + '/Secret/' + credential, 'phase': 'IdentityCreated'}
            installer.write('identity-journal.json', value)
            idp.call('PUT', base + '/clients/' + native + '/default-client-scopes/' + basic)
            installer.secret(credential, {'client-id': client, 'client-secret': secret})
            del secret
            value['clients'][client]['phase'] = 'CredentialStored'
            installer.write('identity-journal.json', value)
        after_registry, _ = idp.call('GET', '/admin/realms/ext-secrets-1-registry')
        if sha(canonical(after_registry)) != value['preservedRegistryRealmSha256']:
            raise RuntimeError('preserved-registry-realm-changed')
        idp.token = None
        discovery, _ = idp.call('GET', '/realms/' + REALM + '/.well-known/openid-configuration')
        if discovery['issuer'] != ISSUER or discovery['jwks_uri'] != ISSUER + '/protocol/openid-connect/certs':
            raise RuntimeError('oidc-discovery-binding-mismatch')
        jwks, _ = idp.call('GET', '/realms/' + REALM + '/protocol/openid-connect/certs')
        if not jwks.get('keys') or any(any(field in key for field in ('d', 'p', 'q', 'dp', 'dq', 'qi', 'k')) for key in jwks['keys']):
            raise RuntimeError('oidc-public-key-unavailable')
        installer.create({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': APP + '-trust'},
                          'data': {'issuer': ISSUER, 'jwks.json': json.dumps(jwks, sort_keys=True)}})
        trust = kjson('get', 'configmap', APP + '-trust', '-n', NS)
        value.update({'phase': 'Provisioned', 'verifiedHttpsDiscovery': True, 'jwksSha256': sha(canonical(jwks)),
                      'publicTrustReference': NS + '/ConfigMap/' + APP + '-trust@uid:' + trust['metadata']['uid'], 'registryRealmUnchanged': True})
        installer.write('identity-journal.json', value)
        return value
    except Exception:
        value['phase'] = 'ProvisioningUnknown'
        installer.write('identity-journal.json', value)
        raise


def authority_manifests(installer):
    installer.create({'apiVersion': 'v1', 'kind': 'ServiceAccount', 'metadata': {'name': APP}, 'automountServiceAccountToken': False})
    for name in ['app', 'dapr', 'administrator-channel']:
        installer.secret(APP + '-' + name + '-token', {'token': secrets.token_urlsafe(48)})
    installer.create({'apiVersion': 'v1', 'kind': 'Service', 'metadata': {'name': APP},
                      'spec': {'selector': {'app': APP}, 'ports': [{'port': 8080, 'targetPort': 8080}]}})
    installer.create({'apiVersion': 'dapr.io/v1alpha1', 'kind': 'Configuration', 'metadata': {'name': APP},
        'spec': {'accessControl': {'defaultAction': 'deny', 'trustDomain': 'cluster.local', 'policies': [
            {'appId': 'ext-secrets-1-inventory', 'namespace': NS, 'trustDomain': 'cluster.local', 'defaultAction': 'deny',
             'operations': [{'name': '/' + method, 'httpVerb': ['POST'], 'action': 'allow'} for method in ['query-profile', 'query-hmac-revocation']]}]}}})
    original = kjson('get', 'deployment', 'inventory', '-n', NS)['spec']['template']['spec']
    sidecar = deepcopy(next(item for item in original['containers'] if item['name'] == 'daprd'))
    if sidecar['image'] != 'ghcr.io/dapr/daprd:1.18.1':
        raise RuntimeError('dapr-version-changed')
    arguments = sidecar['args']
    for flag in ['--app-id', '--config']:
        arguments[arguments.index(flag) + 1] = APP
    for item in sidecar['env']:
        if item['name'] in ('APP_API_TOKEN', 'DAPR_API_TOKEN'):
            item['valueFrom']['secretKeyRef']['name'] = APP + ('-app-token' if item['name'] == 'APP_API_TOKEN' else '-dapr-token')
    for item in sidecar['volumeMounts']:
        if item['mountPath'] == '/var/run/secrets/kubernetes.io/serviceaccount':
            item['name'] = 'authority-kube-api'
    sidecar['securityContext'] = {'allowPrivilegeEscalation': False, 'capabilities': {'drop': ['ALL']}, 'readOnlyRootFilesystem': True, 'runAsNonRoot': True}
    env = [{'name': name, 'value': value} for name, value in {
        'ASPNETCORE_ENVIRONMENT': 'Production', 'ASPNETCORE_HTTP_PORTS': '8080', 'Logging__LogLevel__Default': 'Warning',
        'Authority__StateDirectory': '/var/lib/hmac-authority', 'Authority__AppId': APP, 'Authority__CallerIssuer': ISSUER,
        'Authority__AdministratorClientId': ADMIN_CLIENT, 'Authority__IssuerCaFile': '/var/run/authority-idp-ca/ca.pem',
        'Authority__AdministratorChannelTokenFile': '/var/run/authority-administrator-channel/token'}.items()]
    env.append(env_secret('APP_API_TOKEN', APP + '-app-token', 'token'))
    app = {'name': 'authority', 'image': installer.image, 'imagePullPolicy': 'IfNotPresent', 'env': env,
           'ports': [{'containerPort': 8080}],
           'securityContext': {'allowPrivilegeEscalation': False, 'readOnlyRootFilesystem': True, 'capabilities': {'drop': ['ALL']}},
           'resources': {'requests': {'cpu': '100m', 'memory': '96Mi'}, 'limits': {'cpu': '1', 'memory': '256Mi'}},
           'volumeMounts': [{'name': 'data', 'mountPath': '/var/lib/hmac-authority'}, {'name': 'tmp', 'mountPath': '/tmp'},
               {'name': 'idp-ca', 'mountPath': '/var/run/authority-idp-ca', 'readOnly': True},
               {'name': 'administrator-channel', 'mountPath': '/var/run/authority-administrator-channel', 'readOnly': True}],
           'readinessProbe': {'httpGet': {'path': '/ready', 'port': 8080}, 'periodSeconds': 5, 'timeoutSeconds': 3},
           'livenessProbe': {'httpGet': {'path': '/alive', 'port': 8080}, 'periodSeconds': 10, 'timeoutSeconds': 3}}
    projections = deepcopy(next(item for item in original['volumes'] if item['name'] == 'dapr-identity-token'))
    installer.create({'apiVersion': 'apps/v1', 'kind': 'StatefulSet', 'metadata': {'name': APP},
        'spec': {'serviceName': APP, 'replicas': 1, 'selector': {'matchLabels': {'app': APP}},
            'template': {'metadata': {'labels': {'app': APP}, 'annotations': {'dapr.io/enabled': 'true', 'dapr.io/app-id': APP,
                'dapr.io/app-port': '8080', 'dapr.io/config': APP, 'dapr.io/app-token-secret': APP + '-app-token', 'dapr.io/api-token-secret': APP + '-dapr-token'}},
                'spec': {'serviceAccountName': APP, 'automountServiceAccountToken': False,
                    'securityContext': {'runAsNonRoot': True, 'runAsUser': 1654, 'runAsGroup': 1654, 'fsGroup': 1654, 'seccompProfile': {'type': 'RuntimeDefault'}},
                    'containers': [app, sidecar], 'volumes': [{'name': 'tmp', 'emptyDir': {}}, projections,
                        {'name': 'idp-ca', 'configMap': {'name': 'ext-secrets-1-registry-idp-ca'}},
                        {'name': 'administrator-channel', 'secret': {'secretName': APP + '-administrator-channel-token', 'defaultMode': 288}},
                        {'name': 'authority-kube-api', 'projected': {'sources': [
                            {'serviceAccountToken': {'path': 'token', 'expirationSeconds': 3600}},
                            {'configMap': {'name': 'kube-root-ca.crt', 'items': [{'key': 'ca.crt', 'path': 'ca.crt'}]}},
                            {'downwardAPI': {'items': [{'path': 'namespace', 'fieldRef': {'fieldPath': 'metadata.namespace'}}]}}]}}]}},
            'volumeClaimTemplates': [{'metadata': {'name': 'data', 'labels': {'hexalith.io/authority-installation': installer.run['runId']}},
                'spec': {'accessModes': ['ReadWriteOnce'], 'storageClassName': 'openebs-hostpath-retain', 'resources': {'requests': {'storage': '1Gi'}}}}]}})


def administration_request(installer, method, path, body=None):
    idp = Idp(installer)
    token, _ = idp.call('POST', '/realms/' + REALM + '/protocol/openid-connect/token', {
        'grant_type': 'client_credentials', 'client_id': ADMIN_CLIENT,
        'client_secret': installer.secret_field(APP + '-owner-identity', 'client-secret')}, form=True)
    channel = installer.secret_field(APP + '-administrator-channel-token', 'token')
    connection = http.client.HTTPConnection('127.0.0.1', 38452, timeout=20)
    try:
        connection.request(method, path, canonical(body) if body is not None else None,
            {'Authorization': 'Bearer ' + token['access_token'], 'authority-admin-token': channel, 'Content-Type': 'application/json'})
        response = connection.getresponse()
        data = response.read(ADMINISTRATION_RESPONSE_LIMIT + 1)
        if len(data) > ADMINISTRATION_RESPONSE_LIMIT or response.status not in (200, 409):
            raise RuntimeError('authority-administration-failed:' + str(response.status))
        return response.status, json.loads(data)
    finally:
        connection.close()


def owner_operation(installer, kind, **record):
    path = installer.evidence / ('owner-operation-' + kind + '.json')
    if path.exists():
        journal = json.loads(path.read_text())
    else:
        _, state = administration_request(installer, 'GET', '/admin/state')
        journal = {'phase': 'Prepared', 'operation': {'operationId': uuid.uuid4().hex, 'expectedRevision': state['revision'], 'kind': kind,
            'profile': None, 'caller': None, 'host': None, 'keyStatus': None}, 'preparedAt': timestamp()}
        journal['operation'].update(record)
        durable_write(path, journal, create_only=True)
    # A lost HTTP response is retried only with the exact original operation and expected revision.
    status, receipt = administration_request(installer, 'POST', '/admin/operations', journal['operation'])
    if receipt['operationId'] != journal['operation']['operationId'] or receipt['outcome'] != 'Applied' or status != 200:
        raise RuntimeError('original-owner-operation-not-applied')
    journal.update({'phase': 'Committed', 'receipt': receipt})
    durable_write(path, journal)
    return journal


def bind_public_references(installer, identity, profile):
    before_path = installer.evidence / 'runtime-reference.before.json'
    current = json.loads(RUNTIME.read_text())
    if not before_path.exists():
        durable_write(before_path, current, create_only=True)
    before = json.loads(before_path.read_text())
    changes = {'profileReference': APP, 'publicTrustReference': identity['publicTrustReference'], 'currentRevocationReference': APP,
               'issuer': profile['issuer'], 'audience': profile['audience'], 'profileVersion': profile['version'],
               'profileNotBefore': profile['notBefore'], 'profileValidUntil': profile['validUntil']}
    for name, value in changes.items():
        if current['runtime'].get(name) not in (None, value):
            raise RuntimeError('runtime-current-reference-conflict:' + name)
    current['runtime'].update(changes)
    # No key-dependent host or provider credential is adopted from expired historical installation.
    if current['runtime'].get('currentInventoryAuthorityReference') is not None or current['runtime'].get('hmacCredentialReference') is not None:
        raise RuntimeError('unexpected-current-consumer-binding')
    import qualify
    pending = installer.evidence / 'runtime-reference.json'
    durable_write(pending, current)
    qualify.read_public_configuration(pending)
    for section in before:
        if section != 'runtime' and before[section] != current[section]:
            raise RuntimeError('settled-runtime-section-changed')
    if {name for name in before['runtime'] if before['runtime'][name] != current['runtime'][name]} - set(changes):
        raise RuntimeError('unexpected-runtime-field-change')
    durable_write(RUNTIME, current)
    return current


def write_blockers(installer, config, metadata):
    prior = json.loads((RUNTIME.parent.parent / 'custody-inventory-20261016/refresh-20261027T170637456955Z/blockers.json').read_text())
    owners = {item['input']: item for item in prior['blockers']}
    missing = []
    def walk(value, prefix=''):
        if isinstance(value, dict):
            for name, item in value.items():
                walk(item, prefix + '.' + name if prefix else name)
        elif value is None:
            missing.append(prefix)
    walk(config)
    blockers = [owners.get(name, {'input': name, 'owner': 'EXT-SECRETS-1 owning service administrator',
        'action': 'Provision the actual current isolated reference and return authenticated owner evidence; keep this reference null until verified.'}) for name in sorted(missing)]
    mappings = [mapping for item in metadata for mapping in (item.get('versionMappings') or {}).values()]
    expired = bool(metadata) and all(item.get('versionMappings') for item in metadata) and all(
        datetime.fromisoformat(mapping['verifyUntil'].replace('Z', '+00:00')) <= datetime.now(timezone.utc) for mapping in mappings)
    blockers += [
        {'input': 'currentUsableHmacKeyMappings', 'owner': 'Provider/HMAC key owner',
         'action': ('All observed original mappings are expired. ' if expired else 'Current mappings and exact key-dependent host enrollment are not fully verified. ')
            + 'Preserve existing keys and metadata; supply an independently authorized current usable-key plan. This run cannot rotate, create or extend them.'},
        {'input': 'currentInventoryHostEnrollment', 'owner': 'Inventory workload owner and independent authority administrator',
         'action': 'After current provider credentials and usable mappings exist, enroll the UID-qualified actual inventory host and enable its separate query identity through revision-checked owner operations; then mount its distinct authority credential.'},
        {'input': 'actualCustodyComposition', 'owner': 'Custody workload owner',
         'action': 'Supply the actual isolated custody workload and mount references, then install the existing IDaprPlatformHmacAuthority composition after all current prerequisites are verified.'}]
    installer.write('blockers.json', {'nullReferenceCount': len(missing), 'blockers': blockers, 'nativeMetadataReferences': [item['secretReference'] for item in metadata],
        'dependencyAvailable': False, 'liveRun': False, 'contractOperations': 0})


def health(installer):
    connection = http.client.HTTPConnection('127.0.0.1', 38452, timeout=20)
    try:
        connection.request('GET', '/health')
        response = connection.getresponse()
        status, document = response.status, json.loads(response.read(65536))
    finally:
        connection.close()
    controller = kjson('get', 'statefulset', APP, '-n', NS)
    pod = kjson('get', 'pod', APP + '-0', '-n', NS)
    claim = kjson('get', 'pvc', 'data-' + APP + '-0', '-n', NS)
    bao = kjson('get', 'statefulset', 'hexalith-keys', '-n', 'openbao')
    provider = json.loads(installer.bao('status', '-format=json').stdout)
    result = {'observedAt': timestamp(), 'authorityHttpStatus': status, 'authority': document,
        'statefulSetReference': NS + '/StatefulSet/' + APP + '@uid:' + controller['metadata']['uid'],
        'persistentStorageReference': NS + '/PersistentVolumeClaim/' + claim['metadata']['name'] + '@uid:' + claim['metadata']['uid'],
        'storagePhase': claim['status']['phase'], 'readyReplicas': controller.get('status', {}).get('readyReplicas', 0),
        'nativeImageIds': {item['name']: item['imageID'] for item in pod.get('status', {}).get('containerStatuses', [])},
        'openbaoReadyReplicas': bao.get('status', {}).get('readyReplicas', 0), 'openbaoSealed': provider['sealed'],
        'independentOfRegistryProviderCredentialAndState': True, 'physicalAdministrativeIndependenceClaimed': False,
        'queryCredentialMountedIntoConsumer': False, 'inventoryHostEnrolled': False, 'exactKeyStatusesEnrolled': False,
        'custodyCompositionInstalled': False, 'dependencyAvailable': False, 'qualificationPassed': False,
        'liveRun': False, 'contractOperations': 0}
    installer.write('health.json', result)
    if status != 200 or result['readyReplicas'] != 1 or result['storagePhase'] != 'Bound' or result['openbaoReadyReplicas'] != 3 or result['openbaoSealed']:
        raise RuntimeError('authority-health-not-ready')
    return result


def install(installer):
    if json.loads(APPROVAL.read_text())['exactUserInstruction'] != 'apply recommended':
        raise RuntimeError('retained-owner-approval-missing')
    if not (installer.evidence / 'preserved-kubernetes-before.json').exists():
        installer.run.update({'agentsRegisterSha256': sha(REGISTER.read_bytes()), 'ownerInputMapSha256': sha(OWNER_MAP.read_bytes())})
        installer.write('run.json', installer.run)
        installer.write('preserved-kubernetes-before.json', snapshot())
        installer.write('preserved-openbao-before.json', installer.openbao_snapshot())
    try:
        installer.metadata_credential()
    except RuntimeError:
        # A missing observer cannot authorize key-dependent enrollment, but does not prevent separate authority provisioning.
        installer.write('metadata-observation-blocker.json', {'input': 'currentNativeHmacMetadata', 'owner': 'Provider credential administrator',
            'action': 'Reconcile the original observer issuance journal and supply a current restricted metadata credential; do not reissue an uncertain operation or change its preserved policy.',
            'consumerEnrollmentWithheld': True})
    if not (installer.evidence / 'native-key-metadata-before.json').exists():
        installer.write('native-key-metadata-before.json', native_metadata(installer))
    metadata = json.loads((installer.evidence / 'native-key-metadata-before.json').read_text())
    installer.phase('ProvisioningIdentity')
    with forward('registry-idp', 38444, 8443):
        identity = provision_identity(installer)
        if not (installer.evidence / 'preload-complete.json').exists():
            preload(installer, installer.image)
            installer.write('preload-complete.json', {'image': installer.image, 'temporaryProvisioningCredentialRemoved': True})
        installer.phase('InstallingAuthority')
        authority_manifests(installer)
        kube('rollout', 'status', 'statefulset/' + APP, '-n', NS, '--timeout=50s', timeout=55)
        with forward(APP, 38452, 8080):
            original_profile = installer.evidence / 'owner-operation-PublishProfile.json'
            if original_profile.exists():
                profile = json.loads(original_profile.read_text())['operation']['profile']
            else:
                start = datetime.now(timezone.utc)
                profile = {'profileReference': APP, 'publicTrustReference': identity['publicTrustReference'],
                    'version': 'owner-' + installer.run['runId'], 'issuer': ISSUER, 'audience': 'ext-secrets-1-custody',
                    'notBefore': start.isoformat(), 'validUntil': (start + timedelta(days=1)).isoformat(), 'revoked': False}
            installer.phase('OwnerAdministration')
            owner_operation(installer, 'PublishProfile', profile=profile)
            owner_operation(installer, 'SetCaller', caller={'callerId': QUERY_CLIENT, 'notBefore': profile['notBefore'],
                'validUntil': profile['validUntil'], 'enabled': False})
            _, state = administration_request(installer, 'GET', '/admin/state')
            installer.write('owner-administered-state.json', state)
            if state['profile'] != profile or any(item['enabled'] for item in state['callers']) or state['hosts'] or state['keyStatuses']:
                raise RuntimeError('key-dependent-consumer-enrollment-not-withheld')
            config = bind_public_references(installer, identity, profile)
            write_blockers(installer, config, metadata)
            health(installer)
    installer.write('credential-storage-boundaries.json', {'authorityAppId': APP, 'queryClientId': QUERY_CLIENT, 'administratorClientId': ADMIN_CLIENT,
        'queryCredentialReference': identity['clients'][QUERY_CLIENT]['credentialReference'],
        'administratorCredentialReference': identity['clients'][ADMIN_CLIENT]['credentialReference'],
        'administratorChannelCredentialReference': NS + '/Secret/' + APP + '-administrator-channel-token#token',
        'queryAppChannelCredentialReference': NS + '/Secret/' + APP + '-app-token#token',
        'separateAuthorityState': NS + '/PersistentVolumeClaim/data-' + APP + '-0',
        'operatorCredentialsMountedInApplications': False, 'registryCredentialGrantsAuthority': False,
        'queryCredentialMountedIntoConsumer': False, 'existingKeyValidityChanged': False, 'decisionSignerInstalled': False})
    preserved(installer)
    installer.phase('InstalledConsumerEnrollmentBlocked')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--image', required=True, type=exact_image)
    parser.add_argument('--evidence', type=Path)
    args = parser.parse_args()
    os.umask(0o077)
    evidence = args.evidence or Path('/home/administrator/hexalith-secrets-evidence/ext-secrets-1') / ('authority-installation-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'))
    evidence.mkdir(parents=False, exist_ok=args.evidence is not None)
    print('evidence=' + str(evidence), flush=True)
    instance = None
    exit_code = 1
    try:
        with journal_lock(Path('/tmp/ext-secrets-1-hmac-authority-installation.lock')):
            with journal_lock(evidence / 'installation.lock'):
                instance = AuthorityInstaller(evidence, args.image)
                install(instance)
                instance.close()
                instance = None
        print('authority-installed; expired-key and actual-custody-composition gates remain blocked', flush=True)
        exit_code = 0
    except Exception as error:
        # External error messages can include credentials. Only controlled RuntimeError reason codes are retained.
        reason = str(error) if isinstance(error, RuntimeError) else 'sanitized-authority-installation-failure'
        durable_write(evidence / 'installation-failure.json', {'observedAt': timestamp(), 'exceptionType': type(error).__name__, 'reason': reason,
            'originalJournalsRetained': True, 'automaticRollbackPerformed': False, 'dependencyAvailable': False, 'liveRun': False})
        print('installation-incomplete; preserve original journals and reconcile the named effect', flush=True)
    finally:
        if instance is not None:
            try:
                instance.close()
            except Exception:
                durable_write(evidence / 'credential-cleanup-failure.json', {'observedAt': timestamp(), 'phase': 'CredentialCleanupPending',
                    'owner': 'Provider credential administrator', 'action': 'Reconcile the original journaled accessor and revoke only that observer credential; preserve its existing policy.',
                    'originalJournalsRetained': True, 'dependencyAvailable': False})
                exit_code = 1
    return exit_code


if __name__ == '__main__':
    sys.exit(main())
