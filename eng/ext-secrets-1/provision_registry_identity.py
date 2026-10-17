#!/usr/bin/env python3
"""Provision native identities only in the newly isolated registry IdP.

No identity from this IdP establishes independent secrets profile/decision trust.
The bootstrap principal never enters an application process.
"""
import http.client
import json
from pathlib import Path
import secrets
import socket
import ssl
import sys
import urllib.parse

from install_isolated_registry import Installer, IDP_HOST, NS, canonical, kube

REALM = 'ext-secrets-1-registry'
ISSUER = 'https://' + IDP_HOST + ':8443/realms/' + REALM


class Idp:
    def __init__(self, installer, forward_port=38444):
        self.installer = installer
        ca = installer.secret_field('ext-secrets-1-registry-idp-tls', 'ca\\.crt')
        self.context = ssl.create_default_context(cadata=ca)
        self.forward_port = forward_port
        self.token = None

    def call(self, method, path, body=None, form=False, accepted=(200, 201, 204)):
        connection = http.client.HTTPSConnection(IDP_HOST, 8443, timeout=20, context=self.context)
        connection.sock = self.context.wrap_socket(socket.create_connection(('127.0.0.1', self.forward_port), timeout=20), server_hostname=IDP_HOST)
        headers = {}
        if self.token:
            headers['Authorization'] = 'Bearer ' + self.token
        if body is not None:
            body = urllib.parse.urlencode(body).encode() if form else canonical(body)
            headers['Content-Type'] = 'application/x-www-form-urlencoded' if form else 'application/json'
        connection.request(method, path, body, headers)
        response = connection.getresponse()
        raw = response.read(1024 * 1024)
        status = response.status
        location = response.getheader('Location')
        connection.close()
        if status not in accepted:
            raise RuntimeError('idp-request-failed:' + str(status) + ':' + method + ':' + path.split('?')[0])
        return (json.loads(raw) if raw else None), location

    def login(self):
        value, _ = self.call('POST', '/realms/master/protocol/openid-connect/token', {
            'grant_type': 'client_credentials',
            'client_id': self.installer.secret_field('ext-secrets-1-registry-idp-bootstrap', 'client-id'),
            'client_secret': self.installer.secret_field('ext-secrets-1-registry-idp-bootstrap', 'client-secret')}, form=True)
        self.token = value['access_token']


def hardcoded(name, value):
    return {'name': name, 'protocol': 'openid-connect', 'protocolMapper': 'oidc-hardcoded-claim-mapper', 'consentRequired': False,
            'config': {'claim.name': name, 'claim.value': value, 'jsonType.label': 'String', 'access.token.claim': 'true', 'id.token.claim': 'false'}}


def audience(name):
    return {'name': 'audience-' + name, 'protocol': 'openid-connect', 'protocolMapper': 'oidc-audience-mapper', 'consentRequired': False,
            'config': {'included.custom.audience': name, 'access.token.claim': 'true', 'id.token.claim': 'false'}}


def attribute(name, claim, boolean=False):
    return {'name': name, 'protocol': 'openid-connect', 'protocolMapper': 'oidc-usermodel-attribute-mapper', 'consentRequired': False,
            'config': {'user.attribute': name, 'claim.name': claim, 'jsonType.label': 'boolean' if boolean else 'String',
                       'multivalued': 'false' if boolean else 'true', 'access.token.claim': 'true', 'id.token.claim': 'false'}}


def provision(installer):
    idp = Idp(installer)
    idp.login()
    # POST is create-only and returns conflict if another registry realm already owns the name.
    idp.call('POST', '/admin/realms', {'realm': REALM, 'enabled': True, 'sslRequired': 'all', 'registrationAllowed': False,
             'resetPasswordAllowed': False, 'accessTokenLifespan': 120, 'revokeRefreshToken': True,
             'defaultDefaultClientScopes': [], 'defaultOptionalClientScopes': []})
    base = '/admin/realms/' + REALM
    profile, _ = idp.call('GET', base + '/users/profile')
    # This registry provisioning principal has no fabricated person/email identity.
    # Make email optional in this new realm and restrict custom grant attributes to administrators.
    for item in profile['attributes']:
        if item['name'] == 'email':
            item.pop('required', None)
    profile['unmanagedAttributePolicy'] = 'ADMIN_EDIT'
    idp.call('PUT', base + '/users/profile', profile)
    scopes = {}
    for receiver in ['tenants', 'eventstore']:
        name = 'eventstore-audience.' + receiver
        _, location = idp.call('POST', base + '/client-scopes', {'name': name, 'protocol': 'openid-connect',
            'attributes': {'include.in.token.scope': 'true'}, 'protocolMappers': [audience(receiver)]})
        scopes[name] = location.rsplit('/', 1)[1]
    operations = ['domain-service:process', 'domain-service:replay-state', 'domain-service:query', 'domain-service:project', 'domain-service:metadata']
    for operation in operations:
        name = 'eventstore-operation.' + operation.replace(':', '.')
        _, location = idp.call('POST', base + '/client-scopes', {'name': name, 'protocol': 'openid-connect',
            'attributes': {'include.in.token.scope': 'true'}, 'protocolMappers': [hardcoded('eventstore:operation', operation)]})
        scopes[name] = location.rsplit('/', 1)[1]
    references = {'realm': REALM, 'issuer': ISSUER, 'independentSecretsTrustEstablished': False, 'clients': {}}
    native_scopes, _ = idp.call('GET', base + '/client-scopes')
    basic_scope = next(item['id'] for item in native_scopes if item['name'] == 'basic')
    for client in ['eventstore', 'tenants', 'ext-secrets-1-inventory', 'ext-secrets-1-registry-provisioner']:
        client_secret = secrets.token_urlsafe(48)
        is_human = client == 'ext-secrets-1-registry-provisioner'
        mappers = []
        if is_human:
            mappers = [audience('hexalith-eventstore'), attribute('tenants', 'eventstore:tenant'), attribute('domains', 'eventstore:domain'),
                       attribute('permissions', 'eventstore:permission'), attribute('global_admin', 'global_admin', True)]
        elif client == 'ext-secrets-1-inventory':
            mappers = [audience('hexalith-eventstore'), hardcoded('eventstore:tenant', 'system'), hardcoded('eventstore:domain', 'tenants'),
                       hardcoded('eventstore:permission', 'StreamRead')]
        _, location = idp.call('POST', base + '/clients', {'clientId': client, 'enabled': True, 'protocol': 'openid-connect',
            'publicClient': False, 'secret': client_secret, 'serviceAccountsEnabled': not is_human, 'directAccessGrantsEnabled': is_human,
            'standardFlowEnabled': False, 'fullScopeAllowed': False, 'defaultClientScopes': [], 'optionalClientScopes': [], 'protocolMappers': mappers})
        native_id = location.rsplit('/', 1)[1]
        # The built-in basic scope emits the server-managed native subject; never invent a sub claim.
        idp.call('PUT', base + '/clients/' + native_id + '/default-client-scopes/' + basic_scope)
        granted_scopes = []
        if client == 'eventstore':
            granted_scopes = ['eventstore-audience.tenants'] + [name for name in scopes if name.startswith('eventstore-operation.')]
        for scope in granted_scopes:
            idp.call('PUT', base + '/clients/' + native_id + '/optional-client-scopes/' + scopes[scope])
        secret_name = 'ext-secrets-1-registry-oidc-' + client.replace('ext-secrets-1-', '')
        installer.secret(secret_name, {'client-id': client, 'client-secret': client_secret})
        references['clients'][client] = {'nativeClientId': native_id, 'credentialReference': NS + '/Secret/' + secret_name,
                                          'grantedOptionalScopes': granted_scopes, 'serviceAccount': not is_human}
    username = 'registry-provisioner'
    password = secrets.token_urlsafe(48)
    _, location = idp.call('POST', base + '/users', {'username': username, 'enabled': True, 'emailVerified': True,
        'firstName': 'Isolated', 'lastName': 'Registry Provisioner', 'requiredActions': [],
        'attributes': {'tenants': ['system'], 'domains': ['global-administrators', 'tenants'], 'permissions': ['command:submit', 'query:read', 'StreamRead']},
        'credentials': [{'type': 'password', 'value': password, 'temporary': False}]})
    user_id = location.rsplit('/', 1)[1]
    installer.secret('ext-secrets-1-registry-provisioner', {'username': username, 'password': password, 'subject-id': user_id})
    references['provisioner'] = {'nativeUserId': user_id, 'credentialReference': NS + '/Secret/ext-secrets-1-registry-provisioner',
                                  'nativeAdministratorEnrollment': None, 'globalAdministratorClaimGranted': False}
    installer.write('native-idp-references.json', references)
    public_ca = installer.secret_field('ext-secrets-1-registry-idp-tls', 'ca\\.crt')
    installer.create({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'ext-secrets-1-registry-idp-ca'}, 'data': {'ca.pem': public_ca}})
    return references


if __name__ == '__main__':
    instance = Installer(sys.argv[1])
    instance.created = json.loads((instance.evidence / 'created-resource-references.json').read_text())
    try:
        provision(instance)
        print('native registry IdP identities provisioned; credentials retained only in intended cluster Secrets')
    except Exception as error:
        instance.write('identity-provisioning-failure.json', {'exceptionType': type(error).__name__, 'reason': str(error) if isinstance(error, RuntimeError) else 'sanitized-provisioning-failure'})
        raise SystemExit(1) from None
    finally:
        instance.close()
