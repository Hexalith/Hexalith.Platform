"""Render forward-only Dapr Components from the release graph and environment profile."""
import ipaddress
import json
import re
from urllib.parse import urlsplit


DNS_NAME = re.compile(r'[a-z0-9]([-a-z0-9]*[a-z0-9])?([.][a-z0-9]([-a-z0-9]*[a-z0-9])?)*')
PROVIDERS = {'state': 'state.', 'pubsub': 'pubsub.', 'configuration': 'configuration.',
             'secret': 'secretstores.', 'conversation': 'conversation.'}
CREDENTIAL_KEY = re.compile(
    r'(password|pwd|clientsecret|signingkey|authenticationtokens?|accesstokens?|apikeys?|accesskey|'
    r'accountkey|masterkey|sharedaccesskey|passphrase|credentials?|privatekey|appchanneltoken|'
    r'daprapitoken|apidaprtoken|authorization|secrets?|tokens?)(__|$)|connectionstrings?$', re.I)
CREDENTIAL_VALUE = re.compile(
    r'(?:^|[;,\s])(?:password|pwd|user[ _]*id|uid|user[ _]*name|access[ _]*token|'
    r'authentication[ _]*token|token|api[ _-]*key|client[ _]*secret|secret|authorization|'
    r'access[ _]*key|account[ _]*key|master[ _]*key|shared[ _]*access[ _]*key|passphrase|'
    r'credentials?|private[ _]*key)\s*=', re.I)


def _endpoint(address, selection):
    if not isinstance(address, str) or not re.fullmatch(
            r'(https|rediss)://[a-zA-Z0-9.-]+(:[0-9]{1,5})?(/[a-zA-Z0-9._~/-]*)?'
            r'|postgresql://[a-zA-Z0-9.-]+(:[0-9]{1,5})?(/[a-zA-Z0-9._~/-]*)?'
            r'\?sslmode=(verify-full|require|verify-ca)', address):
        raise ValueError('Component endpoint requires a non-secret TLS URL')
    url = urlsplit(address)
    host = url.hostname.rstrip('.')
    if not DNS_NAME.fullmatch(host) or host == 'localhost' or host.endswith('.localhost') or len(host) > 253:
        raise ValueError('invalid Component endpoint hostname')
    if re.fullmatch(r'(0x[0-9a-f]+|[0-9]+)([.](0x[0-9a-f]+|[0-9]+))*', host):
        ip = ipaddress.IPv4Address(host)
        if int(ip) >> 24 in (0, 127):
            raise ValueError('invalid Component endpoint address')
    if url.port == 0 or selection not in ('authority', 'url') or selection == 'authority' and url.port is None:
        raise ValueError('invalid Component endpoint format or port')
    return url.netloc if selection == 'authority' else address


def _binding_spec(capability, values):
    name = capability['name']
    binding = values['environment']['components'][name]
    provider = binding['type']
    if binding['name'] != name or not provider.startswith(PROVIDERS[capability['kind']]) or re.search('local|echo', provider, re.I):
        raise ValueError(f'profile binding differs from Component {name}')
    metadata = binding.get('metadata', {})
    if provider.endswith('.redis') and str(metadata.get('enableTLS')).lower() != 'true':
        raise ValueError(f'Component {name} requires native Redis TLS')
    endpoint_key, credential_key = binding['endpointMetadataKey'], binding['credentialMetadataKey']
    if any(not re.fullmatch('[A-Za-z][A-Za-z0-9]*', key) for key in (endpoint_key, credential_key)) or endpoint_key.lower() == credential_key.lower():
        raise ValueError(f'Component {name} requires distinct endpoint and credential metadata keys')
    credential = binding['credential']
    if (not isinstance(credential, dict) or set(credential) != {'name', 'key'}
            or not isinstance(credential['name'], str) or len(credential['name']) > 253 or not DNS_NAME.fullmatch(credential['name'])
            or not isinstance(credential['key'], str) or not re.fullmatch('[-a-zA-Z0-9._]{1,253}', credential['key'])):
        raise ValueError(f'Component {name} requires an existing credential reference')
    endpoint = _endpoint(values['endpoints'][binding['endpoint']], binding.get('endpointFormat', 'url'))
    entries = [{'name': endpoint_key, 'value': endpoint}, {'name': credential_key, 'secretKeyRef': dict(credential)}]
    seen = {endpoint_key.lower(), credential_key.lower()}
    for key, value in sorted(metadata.items()):
        if (value is None or isinstance(value, (dict, list)) or not key or key.lower() in seen
                or CREDENTIAL_KEY.search(key) or CREDENTIAL_VALUE.search(str(value))
                or re.search('://|localhost|127[.]', str(value), re.I)
                or key.lower() == 'skipverify' and str(value).lower() != 'false'):
            raise ValueError(f'Component {name} has unsafe or ambiguous provider metadata at {key}')
        seen.add(key.lower())
        if isinstance(value, float) and value.is_integer():
            value = int(value)
        entries.append({'name': key, 'value': value if isinstance(value, str) else json.dumps(value, allow_nan=False)})
    return {'type': provider, 'version': 'v1', 'metadata': entries}


def _components(graph, values, namespace):
    """Consume the same checked graph and profile used by the application Helm render."""
    workloads = {workload['appId'] for workload in graph['workloads']}
    components = {}
    for capability in graph['capabilities']:
        name = capability['name']
        scopes = capability['consumers']
        if not isinstance(name, str) or len(name) > 253 or not DNS_NAME.fullmatch(name):
            raise ValueError('invalid Dapr Component name')
        if name in components:
            raise ValueError(f'duplicate Dapr Component in release graph: {name}')
        if not isinstance(scopes, list) or not scopes or len(scopes) != len(set(scopes)) or not set(scopes) <= workloads:
            raise ValueError(f'Component {name} requires unique, declared workload scopes')
        spec = _binding_spec(capability, values)
        document = {'apiVersion': 'dapr.io/v1alpha1', 'kind': 'Component',
                    'metadata': {'name': name, 'namespace': namespace},
                    'spec': spec, 'scopes': sorted(scopes)}
        components[name] = (capability['kind'], capability['recovery'], document)
    return components


def render_components(graph, values, namespace, *, baseline_graph=None, baseline_values=None):
    """Combine separately validated baseline and candidate release inputs for one environment.

    A same-name Component must serve both generations with the same provider contract and
    recovery class. The writer can expand its scopes. A binding replacement requires a
    separately named Component until the previous release stops depending on it.
    """
    if not isinstance(namespace, str) or len(namespace) > 63 or not re.fullmatch(r'[a-z0-9]([-a-z0-9]*[a-z0-9])?', namespace):
        raise ValueError('Component namespace must be a Kubernetes DNS label')
    if (baseline_graph is None) != (baseline_values is None):
        raise ValueError('baseline graph and profile must be supplied together')
    union = _components(graph, values, namespace)
    if baseline_graph is not None:
        for name, baseline in _components(baseline_graph, baseline_values, namespace).items():
            if name not in union:
                union[name] = baseline
                continue
            current = union[name]
            if current[:2] != baseline[:2] or current[2]['spec'] != baseline[2]['spec']:
                raise ValueError(f'incompatible baseline and candidate Dapr Component: {name}')
            current[2]['scopes'] = sorted(set(current[2]['scopes']) | set(baseline[2]['scopes']))
    return [union[name][2] for name in sorted(union)]
