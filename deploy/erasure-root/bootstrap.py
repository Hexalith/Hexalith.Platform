#!/usr/bin/env python3
"""Prepare or explicitly bootstrap an already initialized dedicated ERC; never initializes/unseals a server or exports a key."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import urllib.request
import urllib.parse


class NoRedirect(urllib.request.HTTPRedirectHandler):
    """A credential-bearing request must never follow any redirect."""
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def enrollment(path):
    def unique(pairs):
        result = {}
        for key, item in pairs:
            if key in result:
                raise ValueError('Duplicate enrollment identity')
            result[key] = item
        return result
    value = json.loads(Path(path).read_bytes(), object_pairs_hook=unique)
    if set(value) != {'address', 'storeId', 'enrollmentId', 'transitMount', 'headMount', 'nativeKeys', 'genesisAnchors'}:
        raise ValueError('Exact public enrollment schema required')
    address = urllib.parse.urlsplit(value['address'])
    if address.scheme != 'https' or not address.hostname or address.username or address.password or address.path not in ('', '/') or address.query or address.fragment:
        raise ValueError('Dedicated HTTPS ERC address required')
    if any(not isinstance(value[field], str) or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]{0,255}', value[field])
           for field in ('storeId', 'enrollmentId')):
        raise ValueError('Canonical bounded store and enrollment identities required')
    if not isinstance(value['nativeKeys'], dict) or not isinstance(value['genesisAnchors'], dict) or not value['nativeKeys'] or set(value['nativeKeys']) != set(value['genesisAnchors']):
        raise ValueError('Complete independently selected tenant enrollment required')
    if value['transitMount'] != 'erc-transit' or value['headMount'] != 'erc-heads':
        raise ValueError('Shipped custody policy requires erc-transit and erc-heads')
    segments = [value['transitMount'], value['headMount'], *value['nativeKeys'].values()]
    if any(not isinstance(segment, str) or not re.fullmatch(r'[A-Za-z0-9_-]{1,128}', segment) for segment in segments):
        raise ValueError('Exact native mount/key segments required')
    if len(set(value['nativeKeys'].values())) != len(value['nativeKeys']):
        raise ValueError('Distinct native tenant keys required')
    for tenant, scopes in value['genesisAnchors'].items():
        if not isinstance(tenant, str) or len(tenant) > 64 or not re.fullmatch(r'[a-z0-9](?:[a-z0-9-]*[a-z0-9])?', tenant) or not isinstance(scopes, dict) or not scopes:
            raise ValueError('Independent genesis scope/digest enrollment required')
        required = {tenant + ':protection:candidate-interaction-occurrences-v1|candidate-interaction-occurrences-v1',
                    tenant + ':protection:deletion-consumption-v23|deletion-consumption-v23',
                    tenant + ':custody:key-lifecycle-v1|custody-key-lifecycle-candidate-v1'}
        if not required.issubset(scopes):
            raise ValueError('Complete occurrence, deletion and lifecycle genesis required')
        reserved = {scope.split('|')[0]: scope for scope in required}
        for scope, digest in scopes.items():
            if isinstance(scope, str) and scope.split('|')[0] in reserved and reserved[scope.split('|')[0]] != scope:
                raise ValueError('Exact runtime owner genesis scope required')
            parts = scope.split('|') if isinstance(scope, str) else []
            actor = parts[0].split(':') if parts else []
            if len(parts) != 2 or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]{0,255}', parts[1]) or len(actor) != 3 or actor[0] != tenant \
                    or len(actor[1]) > 64 or not re.fullmatch(r'[a-z0-9](?:[a-z0-9-]*[a-z0-9])?', actor[1]) \
                    or len(actor[2]) > 256 or not re.fullmatch(r'[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?', actor[2]) \
                    or not isinstance(digest, str) or not re.fullmatch(r'[0-9A-F]{64}', digest):
                raise ValueError('Exact independent scope and genesis digest required')
    return value


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--enrollment', type=Path, required=True)
    parser.add_argument('--credential-reference', default='env:ERC_BOOTSTRAP_TOKEN')
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args(argv)
    try:
        value = enrollment(args.enrollment)
        if not args.apply:
            print(json.dumps({'status': 'PreparedOnly', 'tenants': len(value['nativeKeys']), 'effects': 0}))
            return 0
        if not re.fullmatch(r'env:[A-Za-z_][A-Za-z0-9_]*', args.credential_reference):
            raise ValueError('Environment credential reference required')
        credential = os.environ.get(args.credential_reference[4:])
        if not credential:
            raise ValueError('DependencyNotAvailable: bootstrap credential reference')

        def call(method, path, body=None):
            data = None if body is None else json.dumps(body, separators=(',', ':')).encode()
            request = urllib.request.Request(value['address'].rstrip('/') + '/v1/' + path, data=data, method=method,
                                             headers={'X-Vault-Token': credential, 'Content-Type': 'application/json', 'X-Vault-Request': 'true'})
            with urllib.request.build_opener(NoRedirect()).open(request, timeout=30) as response:
                raw = response.read(32 * 1024 * 1024 + 1)
            if len(raw) > 32 * 1024 * 1024:
                raise ValueError('Oversized native response')
            return json.loads(raw) if raw else None

        health = call('GET', 'sys/health')
        if health['version'] != '2.6.2' or not health['initialized'] or health['sealed']:
            raise ValueError('Exact initialized unsealed OpenBao 2.6.2 required')
        # Never automatically retry bootstrap mutations; preserve the public enrollment and reconcile native state after loss.
        call('POST', 'sys/mounts/' + value['transitMount'], {'type': 'transit'})
        call('POST', 'sys/mounts/' + value['headMount'], {'type': 'kv', 'options': {'version': '2'}})
        call('POST', value['headMount'] + '/config', {'max_versions': 1, 'cas_required': True})
        for tenant, key in value['nativeKeys'].items():
            call('POST', value['transitMount'] + '/keys/' + key,
                 {'type': 'aes256-gcm96', 'derived': True, 'exportable': False, 'allow_plaintext_backup': False})
            head = {'TenantId': tenant, 'StoreId': value['storeId'], 'EnrollmentId': value['enrollmentId'], 'Epoch': 1,
                    'Targets': {}, 'Erased': {}, 'Manifests': {}, 'LifecycleOutcomes': {},
                    'Anchors': {scope: {'Revision': 0, 'Digest': digest, 'Admissions': {}, 'Completed': {}}
                                for scope, digest in value['genesisAnchors'][tenant].items()}, 'Pending': None}
            call('POST', value['headMount'] + '/data/tenants/' + hashlib.sha256(tenant.encode()).hexdigest(),
                 {'options': {'cas': 0}, 'data': head})
        print(json.dumps({'status': 'BootstrappedNotQualified', 'tenants': len(value['nativeKeys']), 'copyPolicyQualified': False}))
        return 0
    except Exception:
        # No remote body, endpoint exception, credential or private material is released into diagnostics.
        print(json.dumps({'status': 'DependencyNotAvailable', 'code': 'ERC_BOOTSTRAP_INPUT_OR_NATIVE_FAILURE', 'qualificationPassed': False}))
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
