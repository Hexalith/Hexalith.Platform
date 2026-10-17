#!/usr/bin/env python3
"""Observe only public KV2 metadata with the restricted application credential.

The HTTP status is authoritative: a CLI exit code cannot establish an empty mount.
Credentials remain in memory and HTTPS headers; no provider data route is used.
"""
import http.client
import json
import re
import selectors
import socket
import ssl
import subprocess
from datetime import datetime, timezone

from install_isolated_registry import BAO_NAME, KUBE, NS, canonical, kjson


def classify(status, document, capabilities):
    """Keep failed/denied/ambiguous observations unknown, including unexpected 404s."""
    permitted = capabilities.get('ext-secrets-1-isolated/metadata') == ['list']
    if status == 404 and document == {'errors': []} and permitted:
        return {'status': 'Empty', 'metadataRootNames': [], 'opaqueNumericMappings': []}
    keys = document.get('data', {}).get('keys') if isinstance(document, dict) else None
    if status == 200 and permitted and isinstance(keys, list) and all(isinstance(key, str) for key in keys):
        # A root census alone never proves the exact per-key version mappings.
        return {'status': 'Observed', 'metadataRootNames': keys, 'opaqueNumericMappings': None}
    return {'status': 'Denied' if status == 403 else 'Failed', 'metadataRootNames': None, 'opaqueNumericMappings': None}


def observe(installer):
    token = installer.secret_field('ext-secrets-1-inventory-metadata-token', 'token')
    ca = kjson('get', 'configmap', 'ext-secrets-1-openbao-ca', '-n', NS)['data']['ca.pem']
    context = ssl.create_default_context(cadata=ca)
    process = subprocess.Popen(KUBE + ['port-forward', '-n', 'openbao', 'service/hexalith-keys-active', ':8200', '--address=127.0.0.1'],
                               stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    try:
        selector = selectors.DefaultSelector()
        selector.register(process.stdout, selectors.EVENT_READ)
        port = None
        for _ in range(4):
            if not selector.select(5):
                break
            line = process.stdout.readline().decode()
            matched = re.search(r'Forwarding from 127\.0\.0\.1:(\d+) -> 8200', line)
            if matched:
                port = int(matched.group(1))
                break
        selector.close()
        if port is None:
            raise RuntimeError('metadata-observation-forward-unavailable')

        def request(method, path, body=None):
            connection = http.client.HTTPSConnection(BAO_NAME, 8200, timeout=20, context=context)
            connection.sock = context.wrap_socket(socket.create_connection(('127.0.0.1', port), timeout=20), server_hostname=BAO_NAME)
            headers = {'X-Vault-Token': token}
            if body is not None:
                headers['Content-Type'] = 'application/json'
            connection.request(method, '/v1/' + path, canonical(body) if body is not None else None, headers)
            response = connection.getresponse()
            raw, status = response.read(1048577), response.status
            connection.close()
            if len(raw) > 1048576:
                raise RuntimeError('metadata-observation-response-too-large')
            return status, json.loads(raw)

        paths = ['ext-secrets-1-isolated/metadata', 'ext-secrets-1-isolated/metadata/*',
                 'ext-secrets-1-isolated/data/*', 'secret/data/*', 'sys/mounts', 'sys/policies/acl/*']
        capability_status, response = request('POST', 'sys/capabilities-self', {'paths': paths})
        capabilities = {path: sorted(response.get(path, [])) for path in paths} if capability_status == 200 else {}
        status, document = request('LIST', 'ext-secrets-1-isolated/metadata')
        result = classify(status, document, capabilities)
        lookup_status, lookup = request('GET', 'auth/token/lookup-self')
        lease = lookup.get('data', {}) if lookup_status == 200 else {}
        result.update({'observedAt': datetime.now(timezone.utc).isoformat(), 'providerNativeHttpStatus': status,
                       'capabilitiesNativeHttpStatus': capability_status, 'effectiveCapabilities': capabilities,
                       'empty404ResponseErrors': [] if status == 404 and document == {'errors': []} else None,
                       'mountReference': 'openbao/mount/ext-secrets-1-isolated/', 'exactPermittedReferences': [],
                       'credentialReference': NS + '/Secret/ext-secrets-1-inventory-metadata-token#token',
                       'credentialValueRecorded': False, 'keyValueReads': 0, 'keyWrites': 0,
                       'credentialLeaseObservation': {key: lease.get(key) for key in ['accessor', 'creation_time', 'expire_time', 'ttl', 'explicit_max_ttl', 'renewable', 'policies']},
                       'dataAndOperatorCapabilitiesDenied': all(capabilities.get(path) == ['deny'] for path in paths[2:]),
                       'metadataReadListCapabilitiesVerified': capabilities.get(paths[0]) == ['list'] and capabilities.get(paths[1]) == ['list', 'read']})
        installer.write('provider-metadata-observation.json', result)
        return result
    finally:
        token = None
        process.terminate()
        process.communicate(timeout=10)


if __name__ == '__main__':
    import sys
    from install_isolated_registry import Installer
    instance = Installer(sys.argv[1])
    try:
        result = observe(instance)
        print('provider metadata observation: ' + result['status'] + ' HTTP ' + str(result['providerNativeHttpStatus']))
    finally:
        instance.close()
