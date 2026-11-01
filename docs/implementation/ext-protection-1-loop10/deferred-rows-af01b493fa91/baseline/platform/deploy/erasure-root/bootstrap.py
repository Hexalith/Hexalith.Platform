#!/usr/bin/env python3
"""Prepare or explicitly bootstrap an already initialized dedicated ERC; never initializes/unseals a server or exports a key."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import ssl
import urllib.error
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


def policy_name(tenant, scope):
    return 'erc-' + tenant + '-' + hashlib.sha256(scope.encode()).hexdigest()[:32]


def render_policy(enrollment, tenant, scope):
    key = enrollment['nativeKeys'][tenant]
    digest = hashlib.sha256(tenant.encode()).hexdigest()
    head = enrollment['headMount']
    transit = enrollment['transitMount']
    # Native ACL parameter equality requires strings. Keep this exact bounded
    # version inventory paired with TrimAsync's invariant string request values.
    versions = ', '.join(json.dumps(str(version)) for version in range(1, 65))
    grants = (
        '# tenant ' + tenant + ' scope ' + scope + '\n'
        'path "' + head + '/data/tenants/' + digest + '" {\n  capabilities = ["read", "update"]\n}\n'
        'path "' + head + '/data/archives/' + digest + '/*" {\n  capabilities = ["read", "create"]\n}\n'
        'path "' + transit + '/encrypt/' + key + '" {\n  capabilities = ["update"]\n}\n'
        'path "' + transit + '/decrypt/' + key + '" {\n  capabilities = ["update"]\n}\n'
        'path "' + transit + '/rewrap/' + key + '" {\n  capabilities = ["update"]\n}\n'
        'path "' + transit + '/keys/' + key + '" {\n  capabilities = ["read"]\n}\n'
        'path "' + transit + '/keys/' + key + '/rotate" {\n  capabilities = ["update"]\n}\n'
        'path "' + transit + '/keys/' + key + '/config" {\n  capabilities = ["update"]\n'
        '  allowed_parameters = {\n    "min_decryption_version" = [' + versions + ']\n'
        '    "min_encryption_version" = [' + versions + ']\n  }\n}\n'
        'path "' + transit + '/keys/' + key + '/trim" {\n  capabilities = ["update"]\n}\n'
    )
    denies = Path(__file__).with_name('custody-policy.hcl').read_text()
    if 'tenants/*' in denies or 'encrypt/*' in denies or 'tenants/*' in grants or 'encrypt/*' in grants:
        raise ValueError('Shared custody policy must not grant every tenant')
    return grants + denies


def policy_text(response):
    if isinstance(response, dict) and isinstance(response.get('policy'), str):
        return response['policy']
    data = response.get('data') if isinstance(response, dict) else None
    if isinstance(data, dict) and isinstance(data.get('policy'), str):
        return data['policy']
    raise ValueError('Prepared custody policy readback missing')


class BootstrapStop(Exception):
    """Named bootstrap step that stopped before a further native change."""
    def __init__(self, step):
        super().__init__(step)
        self.step = step


def fail(step):
    # No remote body, endpoint exception, credential or private material is released into diagnostics.
    print(json.dumps({'status': 'DependencyNotAvailable', 'code': 'ERC_BOOTSTRAP_INPUT_OR_NATIVE_FAILURE', 'step': step, 'qualificationPassed': False}))
    return 1


def pinned_opener(ca_file):
    """Trust only the dedicated erasure-root CA. The bearer is not attached until this context accepts the certificate."""
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
    context.minimum_version = ssl.TLSVersion.TLSv1_2
    context.check_hostname = True
    context.verify_mode = ssl.CERT_REQUIRED
    context.load_verify_locations(cafile=str(ca_file))
    return urllib.request.build_opener(urllib.request.HTTPSHandler(context=context), NoRedirect())


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--enrollment', type=Path, required=True)
    parser.add_argument('--credential-reference', default='env:ERC_BOOTSTRAP_TOKEN')
    parser.add_argument('--ca-certificate', type=Path)
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args(argv)
    step = 'enrollment'
    try:
        value = enrollment(args.enrollment)
        if not args.apply:
            print(json.dumps({'status': 'PreparedOnly', 'tenants': len(value['nativeKeys']), 'effects': 0}))
            return 0
        step = 'credential'
        if not re.fullmatch(r'env:[A-Za-z_][A-Za-z0-9_]*', args.credential_reference):
            raise BootstrapStop('credential')
        credential = os.environ.get(args.credential_reference[4:])
        if not credential:
            raise BootstrapStop('credential')
        step = 'ca-pin'
        if args.ca_certificate is None or not args.ca_certificate.is_file():
            raise BootstrapStop('ca-pin')
        try:
            opener = pinned_opener(args.ca_certificate)
        except (ssl.SSLError, OSError):
            raise BootstrapStop('ca-pin')
        prepared = [(tenant, scope, policy_name(tenant, scope), render_policy(value, tenant, scope))
                    for tenant, scopes in value['genesisAnchors'].items() for scope in scopes]
        key_body = {'type': 'aes256-gcm96', 'derived': True, 'exportable': False, 'allow_plaintext_backup': False, 'deletion_allowed': False}

        def call(method, path, body=None):
            data = None if body is None else json.dumps(body, separators=(',', ':')).encode()
            request = urllib.request.Request(value['address'].rstrip('/') + '/v1/' + path, data=data, method=method,
                                             headers={'X-Vault-Token': credential, 'Content-Type': 'application/json', 'X-Vault-Request': 'true'})
            with opener.open(request, timeout=30) as response:
                raw = response.read(32 * 1024 * 1024 + 1)
            if len(raw) > 32 * 1024 * 1024:
                raise BootstrapStop(step)
            return json.loads(raw) if raw else None

        def read_optional(path):
            try:
                return call('GET', path)
            except urllib.error.HTTPError as error:
                if error.code == 404:
                    return None
                raise BootstrapStop(step)

        def same_mount(existing, expected_type, options):
            if not isinstance(existing, dict) or existing.get('type') != expected_type:
                return False
            found = existing.get('options') or {}
            return found == (options or {})

        def ensure_mount(name, expected_type, options=None):
            path = 'sys/mounts/' + name
            # OpenBao 2.6.2 returns an error for GET sys/mounts/<absent>, not
            # a missing-object 404. The authoritative table names exact mounts.
            response = call('GET', 'sys/mounts')
            table = response.get('data') if isinstance(response, dict) else None
            if not isinstance(table, dict):
                raise BootstrapStop(step)
            existing = table.get(name + '/')
            if existing is None:
                body = {'type': expected_type}
                if options:
                    body['options'] = options
                call('POST', path, body)
                return True
            if not same_mount(existing, expected_type, options):
                raise BootstrapStop(step)
            return False

        def ensure_config(path, expected, newly_created=False):
            existing = read_optional(path)
            data = existing.get('data') if isinstance(existing, dict) else None
            # Only this invocation's new mount may move from native KV-v2
            # defaults to the enrolled configuration. An existing mismatch is
            # still a conflict and must never be silently overwritten.
            native_defaults = isinstance(data, dict) and type(data.get('max_versions')) is int \
                and data['max_versions'] == 0 and data.get('cas_required') is False \
                and data.get('delete_version_after', '0s') == '0s'
            if data is None or newly_created and native_defaults:
                call('POST', path, expected)
                response = call('GET', path)
                readback = response.get('data') if isinstance(response, dict) else None
                if not isinstance(readback, dict) or any(readback.get(field) != expected[field] for field in expected):
                    raise BootstrapStop(step)
                return
            if not isinstance(data, dict) or any(data.get(field) != expected[field] for field in expected):
                raise BootstrapStop(step)

        step = 'health'
        health = call('GET', 'sys/health')
        if not isinstance(health, dict) or health.get('version') != '2.6.2' or not health.get('initialized') or health.get('sealed'):
            raise BootstrapStop('health')
        # A matching mount, key, policy or CAS-0 head is resumed. A different object is refused before it is changed.
        step = 'mount:' + value['transitMount']
        ensure_mount(value['transitMount'], 'transit')
        step = 'mount:' + value['headMount']
        head_mount_created = ensure_mount(value['headMount'], 'kv', {'version': '2'})
        step = 'head-config'
        ensure_config(value['headMount'] + '/config', {'max_versions': 1, 'cas_required': True}, head_mount_created)
        for _, key in value['nativeKeys'].items():
            step = 'transit-key:' + key
            path = value['transitMount'] + '/keys/' + key
            existing = read_optional(path)
            data = existing.get('data') if isinstance(existing, dict) else None
            if data is None:
                call('POST', path, key_body)
            elif any(data.get(field) != key_body[field] for field in key_body):
                raise BootstrapStop(step)
        for _, _, name, text in prepared:
            step = 'policy:' + name
            path = 'sys/policies/acl/' + name
            existing = read_optional(path)
            if existing is None:
                call('POST', path, {'policy': text})
            elif policy_text(existing) != text:
                raise BootstrapStop(step)
            if policy_text(call('GET', path)) != text:
                raise BootstrapStop(step)
        for tenant, _ in value['nativeKeys'].items():
            step = 'head:' + tenant
            head = {'TenantId': tenant, 'StoreId': value['storeId'], 'EnrollmentId': value['enrollmentId'], 'Epoch': 1,
                    'Targets': {}, 'Erased': {}, 'Manifests': {}, 'LifecycleOutcomes': {},
                    'Anchors': {scope: {'Revision': 0, 'Digest': digest, 'Admissions': {}, 'Completed': {}}
                                for scope, digest in value['genesisAnchors'][tenant].items()}, 'Pending': None}
            path = value['headMount'] + '/data/tenants/' + hashlib.sha256(tenant.encode()).hexdigest()
            existing = read_optional(path)
            data = existing.get('data') if isinstance(existing, dict) else None
            current = data.get('data') if isinstance(data, dict) else None
            if current is None:
                call('POST', path, {'options': {'cas': 0}, 'data': head})
            elif current != head:
                raise BootstrapStop(step)
            read = call('GET', path)
            read_data = read.get('data') if isinstance(read, dict) else None
            if not isinstance(read_data, dict) or read_data.get('data') != head:
                raise BootstrapStop(step)
        print(json.dumps({'status': 'BootstrappedNotQualified', 'tenants': len(value['nativeKeys']), 'copyPolicyQualified': False}))
        return 0
    except BootstrapStop as stopped:
        return fail(stopped.step)
    except Exception:
        return fail(step)


if __name__ == '__main__':
    raise SystemExit(main())
