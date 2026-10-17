#!/usr/bin/env python3
"""Read native tenant stream metadata with the inventory's own OAuth and Dapr credentials."""
import http.client
import json
import sys
from datetime import datetime, timezone

from install_isolated_registry import Installer, NS, canonical, sha
from provision_registry_identity import Idp, REALM


def verify(installer, forward_port=38451, idp_port=38444):
    identity = Idp(installer, idp_port)
    token, _ = identity.call('POST', '/realms/' + REALM + '/protocol/openid-connect/token', {
        'grant_type': 'client_credentials', 'client_id': installer.secret_field('ext-secrets-1-registry-oidc-inventory', 'client-id'),
        'client_secret': installer.secret_field('ext-secrets-1-registry-oidc-inventory', 'client-secret')}, form=True)
    bearer = token['access_token']
    dapr = installer.secret_field('ext-secrets-1-inventory-dapr-token', 'token')

    def read(domain, aggregate):
        connection = http.client.HTTPConnection('127.0.0.1', forward_port, timeout=30)
        body = {'tenant': 'system', 'domain': domain, 'aggregateId': aggregate, 'fromSequence': 0, 'pageSize': 100}
        connection.request('POST', '/v1.0/invoke/eventstore/method/api/v1/streams/read', canonical(body),
                           {'Authorization': 'Bearer ' + bearer, 'dapr-api-token': dapr, 'Content-Type': 'application/json'})
        response = connection.getresponse()
        status, raw = response.status, response.read(1048577)
        connection.close()
        return status, json.loads(raw) if status == 200 and len(raw) <= 1048576 else None

    tenants = json.loads((installer.evidence / 'native-enrollment-receipts.json').read_text())['tenants']
    results = []
    for receipt in tenants:
        status, page = read('tenants', receipt['tenantId'])
        valid = status == 200 and page['tenant'] == 'system' and page['domain'] == 'tenants' and page['aggregateId'] == receipt['tenantId']
        valid = valid and page['metadata']['isTruncated'] is False and page['metadata']['latestSequence'] == 1 and len(page['events']) == 1
        valid = valid and 'TenantCreated' in page['events'][0]['eventTypeName'] and page['events'][0]['sequenceNumber'] == 1
        results.append({'tenantId': receipt['tenantId'], 'nativeHttpStatus': status, 'completeNativeEnrollmentRead': valid,
                        'nativeStreamMetadata': page['metadata'] if valid else None, 'sourceStreamSha256': sha(canonical(page)) if valid else None})
    denied, _ = read('global-administrators', 'global-administrators')
    result = {'observedAt': datetime.now(timezone.utc).isoformat(), 'credentialReference': NS + '/Secret/ext-secrets-1-registry-oidc-inventory',
              'daprCredentialReference': NS + '/Secret/ext-secrets-1-inventory-dapr-token#token', 'daprAppId': 'ext-secrets-1-inventory',
              'tenantStreams': results, 'outsideTenantDomainHttpStatus': denied,
              'restrictedReadVerified': all(item['completeNativeEnrollmentRead'] for item in results) and denied == 403,
              'credentialValuesRecorded': False, 'contractOperations': 0}
    installer.write('inventory-restricted-registry-observation.json', result)
    if not result['restrictedReadVerified']:
        raise RuntimeError('restricted-inventory-registry-read-verification-failed')
    return result


if __name__ == '__main__':
    instance = Installer(sys.argv[1])
    try:
        verify(instance)
        print('inventory restricted identity read both complete native tenant streams; unrelated administrator domain denied 403')
    finally:
        instance.close()
