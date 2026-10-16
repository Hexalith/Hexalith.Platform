#!/usr/bin/env python3
"""Bounded native measurement preparation; qualification uses synthetic I/O only.

There is deliberately no live entry point, config loader, credential loader,
command dispatcher or discovery fallback. The sibling native_https_transport.py contains the
reviewable fixed HTTPS mechanics, but its public request method refuses before
socket creation. Its stream parser is qualified using inert byte streams.
All receipts are unsigned synthetic evidence and keep every operational gate
closed. The historical local preflight receipt is never current authority.
"""

from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import time
import stat


RID = '20261015t162756z'
PREPARED = '2026-10-15T16:27:56Z'
CEILING = '2026-10-16T16:27:56Z'
HOST, PORT = '192.168.1.30', 6443
ENDPOINT = 'https://192.168.1.30:6443'
CONTEXT = 'jpiquot@local'
CONFIG_PATH = '/home/administrator/.kube/hexalith-production'
CONFIG_SHA256 = 'f00b691a5886ef76e6be0d6e3efe4030251147b7bba74bb0c58fa1944e0fe86c'
SOURCE = Path(__file__).absolute()
TESTS = SOURCE.with_name('test_native_measurement_adapter.py')
MAX_BODY = 262144
MAX_TOTAL_BODY = 4194304
MAX_HEADERS = 8192
MAX_OUTPUT = 65536
REQUEST_SECONDS = 15
LANE_SECONDS = 300
MAX_ITEMS = 64
UUID = re.compile(r'[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}')
DIGEST = re.compile(r'[0-9a-f]{64}')
IMAGE = re.compile(r'[a-z0-9][a-z0-9._/:@-]{0,254}@sha256:([0-9a-f]{64})')
INPUTS = {
    'packet-v4.json': '1ed5eec5579adeedf7e49519fcba125656caacbaaeaffb6509c49b6575714b14',
    'bindings-v4.json': 'b5fa825a7914a4a71294c5d4c7512c074caf8d144e775a234d7f95adb1e8d346',
    'FUTURE-MEASUREMENTS-v4.md': '81bed336655f99b330c7e5265b174c540b65bd6117fb2ac44a0feb1c33a5f1e9',
    'static-transport-preflight-v4.json': '33e92de1a846f43f7f09f9a7e4a341e039b7e5e40ac1110ab2ef224b4c061c30',
    'policy/recovery-policy.candidate.json': 'a4be017625ba312ab93d76d71bbbf8433f84c08ed43d798c892a39da8939fcdd',
    'user-supplied-receipt.json': 'd089af8354c6f39df2e6c109cf5a309bab021210d75fff5fbdd690b6fe27f603',
    'validation.json': '84f14845bffa7ddd32ce8f1b476942fd855e0628ff3c8c46acd9837fa01744e4',
}
AUTHORITY_FIELDS = ('currentClientIdentity', 'apiCaSha256', 'clientCredentialSha256',
                    'independentNativeBinding', 'completeRevocationLineage',
                    'breakGlassCustody', 'cnpgControllerName', 'cnpgControllerUid',
                    'exactMeasurementApproval')


class Refusal(ValueError):
    """Only fixed application reason codes may cross the receipt boundary."""


def require(condition, code='invalid-input'):
    if not condition:
        raise Refusal(code)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def _request(identifier, path, mode, kind='', api='v1', name='', namespace='', uid=None):
    return {'id': identifier, 'method': 'GET', 'path': path, 'mode': mode,
            'kind': kind, 'apiVersion': api, 'name': name, 'namespace': namespace,
            'historicalUid': uid}


def request_plan():
    """Return only fixed v4 selector paths; callers cannot add a selector."""
    plan = []
    for name, uid in (('kube-system', 'b5860674-1d1e-4a3b-b413-e1bb3b9a4557'),
                      ('keycloak', '22744997-bdad-4ac5-85bf-631fc700d4ea'),
                      ('openbao', None), ('hexalith-memories', None)):
        plan.append(_request('namespace-' + name, '/api/v1/namespaces/' + name,
                             'identity', 'Namespace', name=name, uid=uid))
    plan.extend((_request('version', '/version', 'version'),
                 _request('readyz', '/readyz?verbose', 'readyz')))
    plan.append(_request('keycloak-postgres', '/apis/postgresql.cnpg.io/v1/namespaces/keycloak/clusters/keycloak-postgres',
                         'postgres', 'Cluster', 'postgresql.cnpg.io/v1', 'keycloak-postgres',
                         'keycloak', '56d8d39c-d688-48f2-aa27-a549400352ce'))
    workloads = [('keycloak', 'deployments', 'Deployment', 'keycloak', '98fc84f3-918f-46d9-ae22-7584fb31ff9b'),
                 ('openbao', 'statefulsets', 'StatefulSet', 'hexalith-keys', 'df2bbbbe-1fe8-4e0b-a8a6-174bd3287806'),
                 ('hexalith-memories', 'statefulsets', 'StatefulSet', 'redis-stack', None),
                 ('hexalith-memories', 'statefulsets', 'StatefulSet', 'falkordb', None)]
    workloads.extend(('hexalith-memories', 'deployments', 'Deployment', name, None)
                     for name in ('memories', 'memories-mcp', 'memories-access-telemetry',
                                  'memories-access-telemetry-clock'))
    for namespace, plural, kind, name, uid in workloads:
        plan.append(_request(namespace + '-' + name,
                             f'/apis/apps/v1/namespaces/{namespace}/{plural}/{name}',
                             'workload', kind, 'apps/v1', name, namespace, uid))
    plan.append(_request('keycloak-service', '/api/v1/namespaces/keycloak/services/keycloak',
                         'service', 'Service', name='keycloak', namespace='keycloak',
                         uid='0ac9cd04-9d7e-41ed-8ac7-5a74ca659fd0'))
    for name in ('openebs-hostpath', 'openebs-hostpath-retain'):
        plan.append(_request(name, '/apis/storage.k8s.io/v1/storageclasses/' + name,
                             'storageclass', 'StorageClass', 'storage.k8s.io/v1', name))
    plan.append(_request('native-binding', '/apis/rbac.authorization.k8s.io/v1/clusterrolebindings/kubeadm:cluster-admins',
                         'authority', 'ClusterRoleBinding', 'rbac.authorization.k8s.io/v1',
                         'kubeadm:cluster-admins', uid='8224bcb7-57c6-437d-a27c-1120d03c8b4b'))
    for prefix in ('kcpre', 'kc', 'bao', 'mem'):
        name = 's40-' + prefix + '-' + RID
        plan.append(_request('absent-' + name, '/api/v1/namespaces/' + name,
                             'absence', 'Namespace', name=name))
    return plan


def contract():
    """Exact offline approval scope; unsupported selectors cannot dispatch."""
    return {
        'transport': {'endpoint': ENDPOINT, 'ip': HOST, 'port': PORT, 'method': 'GET',
            'tls': 'strict-pinned-ca-and-static-client-identity-required',
            'dns': False, 'proxies': False, 'redirects': False, 'retries': 0,
            'discovery': False, 'pagination': False, 'compression': False,
            'transferEncoding': False, 'contentLengthRequired': True,
            'credentialFallback': False, 'liveActivation': 'unconditionally-refused'},
        'configuration': {'pathNotRead': CONFIG_PATH, 'sha256': CONFIG_SHA256,
                          'context': CONTEXT, 'endpoint': ENDPOINT},
        'limits': {'requestSeconds': REQUEST_SECONDS, 'laneSeconds': LANE_SECONDS,
                   'maximumHeaderBytes': MAX_HEADERS, 'maximumResponseBytes': MAX_BODY,
                   'maximumLaneResponseBytes': MAX_TOTAL_BODY, 'maximumItems': MAX_ITEMS,
                   'maximumReceiptBytes': MAX_OUTPUT, 'maximumPages': 1,
                   'maximumRequests': len(request_plan()), 'attemptsPerSelector': 1},
        'requests': request_plan(),
        'outputSlots': ['identity', 'version', 'ready', 'replicas', 'readyReplicas',
                        'imageDigests', 'systemId', 'timeline', 'primaryOrdinal',
                        'serviceType', 'ports', 'storageClassShape',
                        'historicalNativeBindingShape', 'targetAbsent'],
        'unsupportedSelectors': {
            'node/node1': 'detailed-node-schema-and-current-node-uid-unqualified',
            'pods/keycloak,openbao,hexalith-memories': 'regular-and-init-image-mapping-unqualified',
            'pvc/keycloak,openbao,hexalith-memories': 'exact-complete-storage-mapping-unqualified',
            'pv/from-measured-pvc-only': 'backing-and-claim-node-drift-checks-unqualified',
            'cnpg-system/deployments?labelSelector=app.kubernetes.io/name=cloudnative-pg':
                'controller-name-uid-and-watch-namespace-sanitizer-unqualified'},
        'approvedCnpgWatchNamespaceReferenceOnly': 'keycloak,cnpg-system',
        'authorityCheckPlan': ['separately-bind-current-client-and-ca',
            'verify-static-client-key-pair-and-custody-without-fallback',
            'verify-current-independent-native-binding-and-role-subjects',
            'verify-complete-grant-and-revocation-lineage-and-break-glass-custody',
            'bind-current-cnpg-controller-and-exact-watch-namespace',
            'obtain-separate-exact-current-measurement-approval'],
        'stopConditions': ['first-failure', 'expired-policy', 'identity-drift',
            'unknown-or-extra-selector', 'insufficient-rights', 'unexpected-status',
            'redirect', 'timeout', 'overflow', 'malformed-response', 'pagination',
            'unsupported-transfer-or-compression', 'target-collision', 'binding-drift'],
    }


def read_source_bytes(path, maximum=1048576):
    """Read bounded regular source through no-follow directory descriptors."""
    path = Path(path)
    if not path.is_absolute() or '..' in path.parts or maximum <= 0:
        raise ValueError('source-custody-refused')
    directory = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
    source = None
    try:
        for part in path.parts[1:-1]:
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC,
                            dir_fd=directory)
            os.close(directory)
            directory = child
            info = os.fstat(directory)
            mode = stat.S_IMODE(info.st_mode)
            sticky_root = info.st_uid == 0 and bool(mode & stat.S_ISVTX)
            if info.st_uid not in (0, os.getuid()) or (mode & 0o022 and not sticky_root):
                raise ValueError('source-custody-refused')
        source = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC,
                         dir_fd=directory)
        before = os.fstat(source)
        if (not stat.S_ISREG(before.st_mode) or before.st_nlink != 1 or
                before.st_uid not in (0, os.getuid()) or stat.S_IMODE(before.st_mode) & 0o022 or
                not 0 <= before.st_size <= maximum):
            raise ValueError('source-custody-refused')
        raw = bytearray()
        while len(raw) <= maximum:
            chunk = os.read(source, min(65536, maximum + 1 - len(raw)))
            if not chunk:
                break
            raw.extend(chunk)
        after = os.fstat(source)
        fields = ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns', 'st_ctime_ns',
                  'st_mode', 'st_uid', 'st_gid', 'st_nlink')
        if (len(raw) != before.st_size or len(raw) > maximum or
                any(getattr(before, key) != getattr(after, key) for key in fields)):
            raise ValueError('source-custody-refused')
        return bytes(raw)
    finally:
        if source is not None:
            os.close(source)
        os.close(directory)


def _file_binding(path):
    raw = read_source_bytes(path)
    return {'path': str(path), 'bytes': len(raw), 'sha256': sha(raw)}


def binding_template(runtime_inventory_sha256):
    """Make nonsecret metadata only; the independent caller inventories runtime."""
    require(type(runtime_inventory_sha256) is str and DIGEST.fullmatch(runtime_inventory_sha256),
            'runtime-binding')
    return {'kind': 'hexalith.synthetic-native-measurement-binding', 'schemaVersion': 1,
            'recoveryId': RID, 'preparedAt': PREPARED, 'policyCeiling': CEILING,
            'immutableInputs': dict(INPUTS), 'source': _file_binding(SOURCE),
            'tests': _file_binding(TESTS),
            'httpsSource': _file_binding(SOURCE.with_name('native_https_transport.py')),
            'httpsTests': _file_binding(SOURCE.with_name('test_native_https_transport.py')),
            'runtimeInventorySha256': runtime_inventory_sha256,
            'contract': contract(), 'authority': dict.fromkeys(AUTHORITY_FIELDS),
            'operationalGate': 'closed', 'syntheticOnly': True}


def _strict_json(raw):
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, 'duplicate-json-field')
            result[key] = value
        return result
    def integer(value):
        require(len(value) <= 20, 'integer-limit')
        result = int(value)
        require(-(2 ** 63) <= result < 2 ** 63, 'integer-limit')
        return result
    def unsupported(_):
        raise Refusal('unsupported-json-number')
    return json.loads(raw, object_pairs_hook=pairs, parse_int=integer,
                      parse_float=unsupported, parse_constant=unsupported)


def _integer(value, maximum=64):
    require(type(value) is int and 0 <= value <= maximum, 'typed-integer')
    return value


def _object(value):
    require(type(value) is dict, 'typed-object')
    return value


def _sequence(value, minimum=0):
    require(type(value) is list and minimum <= len(value) <= MAX_ITEMS, 'item-limit')
    return value


def _identity(request, data):
    require(data.get('kind') == request['kind'] and data.get('apiVersion') == request['apiVersion'],
            'resource-kind')
    metadata = _object(data.get('metadata'))
    require(metadata.get('name') == request['name'] and
            metadata.get('namespace', '') == request['namespace'], 'resource-selector')
    uid = metadata.get('uid')
    require(type(uid) is str and UUID.fullmatch(uid), 'resource-uid')
    require(request['historicalUid'] is None or uid == request['historicalUid'], 'identity-drift')
    return {'name': request['name'], 'namespace': request['namespace'], 'uid': uid}


def _images(containers):
    digests = []
    for container in _sequence(containers, 1):
        image = _object(container).get('image')
        require(type(image) is str and IMAGE.fullmatch(image), 'digest-pinned-image-required')
        digests.append('sha256:' + IMAGE.fullmatch(image).group(1))
    return digests


def sanitize(request, status, content_type, raw):
    """Project only typed fields; never return body, messages, env or diagnostics."""
    mode = request['mode']
    require(type(status) is int and status == (404 if mode == 'absence' else 200),
            'target-collision' if mode == 'absence' and status == 200 else 'unexpected-status')
    if mode == 'readyz':
        require(content_type == 'text/plain' and len(raw) <= 8192, 'readyz-schema')
        lines = raw.decode('ascii').splitlines()
        require(0 < len(lines) <= MAX_ITEMS, 'readyz-schema')
        require(lines == ['ok'] or (lines[-1] == 'readyz check passed' and
                all(re.fullmatch(r'\[\+\][a-z0-9/_-]{1,160} ok', line)
                    for line in lines[:-1]) and len(lines) > 1), 'readyz-unhealthy')
        return {'ready': True}
    require(content_type == 'application/json', 'content-type')
    data = _object(_strict_json(raw))
    if mode == 'absence':
        details = _object(data.get('details'))
        require(data.get('apiVersion') == 'v1' and data.get('kind') == 'Status' and
                data.get('status') == 'Failure' and data.get('reason') == 'NotFound' and
                type(data.get('code')) is int and data['code'] == 404 and
                details.get('name') == request['name'] and details.get('kind') == 'namespaces',
                'explicit-not-found-required')
        return {'targetAbsent': True, 'name': request['name']}
    if mode == 'version':
        value = data.get('gitVersion')
        require(type(value) is str and re.fullmatch(r'v[0-9]{1,2}\.[0-9]{1,2}\.[0-9]{1,3}', value),
                'version-schema')
        require(data.get('major') == value[1:].split('.')[0] and
                data.get('minor') == value.split('.')[1], 'version-schema')
        return {'version': value}
    result = {'identity': _identity(request, data)}
    if mode == 'identity':
        return result
    if mode in ('workload', 'postgres', 'service'):
        spec = _object(data.get('spec'))
    if mode in ('workload', 'postgres'):
        status_data = _object(data.get('status'))
    if mode == 'workload':
        generation = _integer(_object(data.get('metadata')).get('generation'), 2147483647)
        require(generation > 0 and type(status_data.get('observedGeneration')) is int and
                status_data['observedGeneration'] == generation, 'workload-generation-stale')
        replicas = _integer(spec.get('replicas'))
        ready = _integer(status_data.get('readyReplicas', 0))
        require(ready == replicas, 'workload-not-ready')
        template = _object(_object(spec.get('template')).get('spec'))
        result.update(replicas=replicas, readyReplicas=ready)
        # v4 selects images for StatefulSets and Keycloak Deployment only.
        if request['kind'] == 'StatefulSet' or request['namespace'] == 'keycloak':
            result['imageDigests'] = _images(template.get('containers'))
    elif mode == 'postgres':
        conditions = _sequence(status_data.get('conditions'), 1)
        ready_conditions = [item for item in conditions if _object(item).get('type') == 'Ready']
        require(len(ready_conditions) == 1 and ready_conditions[0].get('status') == 'True',
                'postgres-not-ready')
        system_id = status_data.get('systemID')
        require(type(system_id) is str and re.fullmatch(r'[1-9][0-9]{0,19}', system_id), 'postgres-system-id')
        primary = status_data.get('currentPrimary')
        require(type(primary) is str and re.fullmatch(r'keycloak-postgres-[1-9][0-9]?', primary),
                'postgres-primary')
        result.update(systemId=system_id, timeline=_integer(status_data.get('timelineID'), 2147483647),
                      primaryOrdinal=int(primary.rsplit('-', 1)[1]),
                      imageDigests=_images([{'image': spec.get('imageName')}]))
    elif mode == 'service':
        require(spec.get('type') in ('ClusterIP', 'NodePort', 'LoadBalancer'), 'service-type')
        ports = []
        for port in _sequence(spec.get('ports'), 1):
            port = _object(port)
            require(port.get('protocol', 'TCP') in ('TCP', 'UDP', 'SCTP'), 'service-protocol')
            number = _integer(port.get('port'), 65535)
            require(number > 0, 'service-port')
            routing = {'port': number, 'protocol': port.get('protocol', 'TCP')}
            if 'name' in port:
                name = port['name']
                require(type(name) is str and re.fullmatch(r'[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?', name),
                        'service-port-name')
                routing['nameSha256'] = sha(name.encode('ascii'))
            target = port.get('targetPort', number)
            if type(target) is int:
                require(0 < target <= 65535, 'service-target-port')
                routing['targetPort'] = target
            else:
                require(type(target) is str and len(target) <= 15 and
                        re.fullmatch(r'[a-z0-9](?:[a-z0-9-]{0,13}[a-z0-9])?', target) and
                        re.search(r'[a-z]', target), 'service-target-port-name')
                routing['targetPortNameSha256'] = sha(target.encode('ascii'))
            if 'nodePort' in port:
                node_port = _integer(port['nodePort'], 65535)
                require(node_port > 0 and spec['type'] in ('NodePort', 'LoadBalancer'),
                        'service-node-port')
                routing['nodePort'] = node_port
            ports.append(routing)
        result.update(serviceType=spec['type'], ports=ports)
    elif mode == 'storageclass':
        reclaim = 'Retain' if request['name'].endswith('-retain') else 'Delete'
        require(data.get('provisioner') == 'openebs.io/local' and
                data.get('reclaimPolicy') == reclaim and data.get('volumeBindingMode') == 'WaitForFirstConsumer',
                'storageclass-drift')
        result['storageClassShape'] = {'provisioner': 'openebs.io/local', 'reclaimPolicy': reclaim,
                                      'volumeBindingMode': 'WaitForFirstConsumer'}
    elif mode == 'authority':
        require(data['metadata'].get('ownerReferences', []) == [] and data.get('roleRef') ==
                {'apiGroup': 'rbac.authorization.k8s.io', 'kind': 'ClusterRole', 'name': 'cluster-admin'} and
                data.get('subjects') == [{'kind': 'Group', 'apiGroup': 'rbac.authorization.k8s.io',
                                          'name': 'kubeadm:cluster-admins'}], 'native-binding-drift')
        result['historicalNativeBindingShape'] = True
        result['currentAuthorityAccepted'] = False
    else:
        raise Refusal('unsupported-selector')
    return result


class Response:
    """An inert response made from already supplied bounded synthetic chunks."""

    def __init__(self, status, content_type, chunks):
        require(type(chunks) in (tuple, list) and len(chunks) <= MAX_ITEMS, 'response-chunk-limit')
        self.status, self.content_type, self.chunks = status, content_type, tuple(chunks)


class SyntheticTransport:
    """Single-attempt fixture queue; contains no socket, subprocess or callback."""

    def __init__(self, responses):
        require(type(responses) in (tuple, list) and len(responses) <= len(request_plan()),
                'synthetic-response-limit')
        self.responses = tuple(responses)
        self.requests = []

    def request(self, request, deadline, maximum):
        require(time.monotonic() < deadline, 'deadline')
        index = len(self.requests)
        self.requests.append(dict(request))
        require(index < len(self.responses), 'synthetic-response-missing')
        response = self.responses[index]
        require(type(response) is Response, 'synthetic-response-type')
        return response


def _remaining(deadline):
    remaining = deadline - time.monotonic()
    require(0 < remaining <= LANE_SECONDS, 'deadline')
    return remaining



def _utc(now):
    require(type(now) is datetime and now.tzinfo is not None and now.utcoffset().total_seconds() == 0,
            'utc-required')
    return now


def _closed_receipt(now):
    return {'kind': 'hexalith.synthetic-native-measurement-receipt', 'schemaVersion': 1,
        'recoveryId': RID, 'observedAt': now.isoformat().replace('+00:00', 'Z'),
        'policyPreparedAt': PREPARED, 'policyCeiling': CEILING,
        'policyExpired': now >= datetime.fromisoformat(CEILING.replace('Z', '+00:00')),
        'qualification': 'refused', 'syntheticOnly': True, 'operationalGate': 'closed',
        'apiCalls': 0, 'syntheticRequests': 0, 'authenticationPerformed': False,
        'authorityAccepted': False, 'providerOrIamQualified': False,
        'captureOrExecutionAuthorized': False, 'measurements': [],
        'blockers': list(AUTHORITY_FIELDS) + list(contract()['unsupportedSelectors'])}


def qualify_synthetic(binding_raw, expected_binding_sha256, current_runtime_inventory_sha256,
                      transport, now_utc=None):
    """Validate independently anchored in-memory metadata before fixed mock I/O.

    The caller must independently verify its complete runtime inventory and
    supply that digest as current_runtime_inventory_sha256. No caller input
    can select a path, bypass expiry for operations or activate live transport.
    Offline qualification remains possible after expiry and reports it clearly.
    """
    now = datetime.now(timezone.utc) if now_utc is None else now_utc
    # The fallback time is trusted system UTC, never a returned caller string.
    if type(now) is not datetime or now.tzinfo is None or now.utcoffset().total_seconds() != 0:
        now = datetime.now(timezone.utc)
        result = _closed_receipt(now)
        result['reason'] = 'synthetic-qualification-refused'
        return result
    result = _closed_receipt(_utc(now))
    try:
        require(type(binding_raw) is bytes and len(binding_raw) <= MAX_OUTPUT and
                type(expected_binding_sha256) is str and DIGEST.fullmatch(expected_binding_sha256)
                and sha(binding_raw) == expected_binding_sha256, 'binding-digest')
        require(type(current_runtime_inventory_sha256) is str and
                DIGEST.fullmatch(current_runtime_inventory_sha256), 'runtime-binding')
        binding = _strict_json(binding_raw)
        require(json.dumps(binding, sort_keys=True, separators=(',', ':')) ==
                json.dumps(binding_template(current_runtime_inventory_sha256), sort_keys=True,
                           separators=(',', ':')), 'sealed-binding-drift')
        require(type(transport) is SyntheticTransport and not transport.requests, 'synthetic-transport-required')
        lane_deadline = time.monotonic() + LANE_SECONDS
        total_bytes = 0
        for request in request_plan():
            deadline = min(lane_deadline, time.monotonic() + REQUEST_SECONDS)
            _remaining(deadline)
            result['syntheticRequests'] += 1
            response = transport.request(request, deadline, min(MAX_BODY, MAX_TOTAL_BODY - total_bytes))
            body = bytearray()
            for chunk in response.chunks:
                _remaining(deadline)
                require(type(chunk) is bytes and len(chunk) > 0 and
                        len(body) + len(chunk) <= MAX_BODY and
                        total_bytes + len(chunk) <= MAX_TOTAL_BODY, 'response-overflow')
                body.extend(chunk)
                total_bytes += len(chunk)
            _remaining(deadline)
            measurement = sanitize(request, response.status, response.content_type, bytes(body))
            _remaining(deadline)
            _remaining(lane_deadline)
            result['measurements'].append({'selector': request['id'], 'output': measurement})
            require(len(json.dumps(result, sort_keys=True).encode()) <= MAX_OUTPUT, 'receipt-overflow')
        completed = {**result, 'qualification': 'pass', 'bindingSha256': expected_binding_sha256,
                     'runtimeInventorySha256': current_runtime_inventory_sha256}
        require(len(json.dumps(completed, sort_keys=True).encode()) <= MAX_OUTPUT, 'receipt-overflow')
        result = completed
    except Exception:
        # Never retain partial raw data, exception messages or attacker strings.
        result['qualification'] = 'refused'
        result['measurements'] = []
        result['reason'] = 'synthetic-qualification-refused'
    return result


def activate_live(*args, **kwargs):
    """Refuse before consulting arguments, configuration, credentials or sockets."""
    raise Refusal('live-activation-disabled-before-configuration-access')


def main():
    # No argument parser: supplied secrets, paths and unknown flags are not read
    # or echoed. Even --help cannot activate a loader or a transport.
    result = _closed_receipt(datetime.now(timezone.utc))
    result['reason'] = 'live-activation-disabled-before-configuration-access'
    print(json.dumps(result, sort_keys=True))
    return 1


if __name__ == '__main__':
    raise SystemExit(main())
