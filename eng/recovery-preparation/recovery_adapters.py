#!/usr/bin/env python3
"""Offline recovery request primitives; no credential loader or live dispatcher.

The injected backend is a protocol seam, not an operational qualification.
Even a successful synthetic run leaves every real-system acceptance unknown.
"""

import argparse
import base64
from dataclasses import asdict, dataclass, replace
from datetime import datetime, timezone
import hashlib
import ipaddress
import json
import os
from pathlib import Path
import re
import stat
import sys
import time

MAX_JSON = 2 * 1024 * 1024
MAX_ARTIFACT = 64 * 1024 * 1024
MAX_STDIN = 2 * 1024 * 1024
TIMEOUT = 15
ALIASES = {'latest', 'current', 'default', 'head', 'null', 'none'}
FAILURE = 'recovery-adapter-refused'


class Refusal(ValueError):
    """Fixed sanitized failure; raw input and backend diagnostics never escape."""


def require(value):
    if not value:
        raise Refusal(FAILURE)


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=True).encode('ascii')


def strict_json(raw):
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result)
            result[key] = value
        return result
    try:
        require(type(raw) is bytes and len(raw) <= MAX_JSON)
        return json.loads(raw, object_pairs_hook=pairs,
                          parse_float=lambda _: require(False),
                          parse_constant=lambda _: require(False))
    except Exception:
        raise Refusal(FAILURE) from None


def text(value, maximum=256, pattern=r'[A-Za-z0-9][A-Za-z0-9_.:@/+\-]*'):
    require(type(value) is str and 0 < len(value) <= maximum and
            re.fullmatch(pattern, value) is not None and value.lower() not in ALIASES)
    return value


def sha(value):
    require(type(value) is str and re.fullmatch(r'[0-9a-f]{64}', value) is not None)
    return value


def integer(value, lower=0, upper=1000000000):
    require(type(value) is int and lower <= value <= upper)
    return value


def timestamp(value):
    require(type(value) is str and re.fullmatch(r'\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ', value))
    try:
        return datetime.strptime(value, '%Y-%m-%dT%H:%M:%SZ').replace(tzinfo=timezone.utc)
    except Exception:
        raise Refusal(FAILURE) from None


def absolute_path(value, volume=False):
    require(type(value) is str and value.startswith('/') and '\x00' not in value and
            len(value) <= 1024 and str(Path(value)) == value and
            '..' not in Path(value).parts and value != '/')
    if volume:
        require(value.startswith('/var/openebs/local/') and len(Path(value).parts) >= 5)
    return value


def fields(value, expected):
    require(type(value) is dict and set(value) == set(expected))


@dataclass(frozen=True)
class Tool:
    path: str
    sha256: str
    size: int

    @classmethod
    def parse(cls, value):
        if value is None:
            return None
        fields(value, ('path', 'sha256', 'size'))
        return cls(absolute_path(value['path']), sha(value['sha256']), integer(value['size'], 1, 1024**3))


@dataclass(frozen=True)
class Cluster:
    uid: str
    endpoint: str
    ca_sha256: str

    @classmethod
    def parse(cls, value):
        fields(value, ('uid', 'endpoint', 'caSha256'))
        endpoint = value['endpoint']
        require(type(endpoint) is str)
        match = re.fullmatch(r'https://([0-9.]+):([0-9]{1,5})', endpoint)
        require(match is not None)
        try:
            address = ipaddress.IPv4Address(match[1])
            require(str(address) == match[1] and not address.is_unspecified and not address.is_multicast)
        except Exception:
            raise Refusal(FAILURE) from None
        integer(int(match[2]), 1, 65535)
        require(str(int(match[2])) == match[2])
        return cls(text(value['uid']), endpoint, sha(value['caSha256']))


KINDS = {
    ('v1', 'Namespace'): ('/api/v1', 'namespaces', False),
    ('v1', 'PersistentVolume'): ('/api/v1', 'persistentvolumes', False),
    ('v1', 'PersistentVolumeClaim'): ('/api/v1', 'persistentvolumeclaims', True),
    ('v1', 'Secret'): ('/api/v1', 'secrets', True),
    ('v1', 'Service'): ('/api/v1', 'services', True),
    ('apps/v1', 'Deployment'): ('/apis/apps/v1', 'deployments', True),
    ('apps/v1', 'StatefulSet'): ('/apis/apps/v1', 'statefulsets', True),
    ('networking.k8s.io/v1', 'NetworkPolicy'): ('/apis/networking.k8s.io/v1', 'networkpolicies', True),
    ('postgresql.cnpg.io/v1', 'Cluster'): ('/apis/postgresql.cnpg.io/v1', 'clusters', True),
    ('admissionregistration.k8s.io/v1', 'ValidatingAdmissionPolicyBinding'):
        ('/apis/admissionregistration.k8s.io/v1', 'validatingadmissionpolicybindings', False),
}

STORE_KINDS = {('apps/v1', 'StatefulSet'), ('apps/v1', 'Deployment'), ('postgresql.cnpg.io/v1', 'Cluster')}
FENCE_KIND = ('admissionregistration.k8s.io/v1', 'ValidatingAdmissionPolicyBinding')


@dataclass(frozen=True)
class Resource:
    api_version: str
    kind: str
    namespace: str
    name: str
    uid: str | None
    resource_version: str | None

    @classmethod
    def parse(cls, value, proposed=False):
        fields(value, ('apiVersion', 'kind', 'namespace', 'name', 'uid', 'resourceVersion'))
        key = (value['apiVersion'], value['kind'])
        require(key in KINDS)
        namespace = value['namespace']
        require(type(namespace) is str)
        if KINDS[key][2]:
            text(namespace, 63, r'[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?')
        else:
            require(namespace == '')
        name = text(value['name'], 253, r'[a-z0-9](?:[a-z0-9.\-]*[a-z0-9])?')
        uid, rv = value['uid'], value['resourceVersion']
        if proposed and uid is None and rv is None:
            require(key == ('v1', 'Namespace'))
        else:
            text(uid)
            text(rv, 64, r'[0-9]+')
        return cls(*key, namespace, name, uid, rv)

    def wire(self):
        return {'apiVersion': self.api_version, 'kind': self.kind, 'namespace': self.namespace,
                'name': self.name, 'uid': self.uid, 'resourceVersion': self.resource_version}

    def path(self):
        prefix, plural, namespaced = KINDS[(self.api_version, self.kind)]
        return prefix + ('/namespaces/' + self.namespace if namespaced else '') + '/' + plural + '/' + self.name


@dataclass(frozen=True)
class Artifact:
    store: str
    key: str
    version: str
    sha256: str
    size: int
    retain_until: str

    @classmethod
    def parse(cls, value):
        fields(value, ('store', 'key', 'version', 'sha256', 'size', 'retainUntil'))
        key = text(value['key'], 1024)
        require(not key.startswith('/') and all(p and p not in ('.', '..') and
                p.lower() not in ALIASES for p in key.split('/')))
        timestamp(value['retainUntil'])
        return cls(text(value['store']), key, text(value['version']), sha(value['sha256']),
                   integer(value['size'], 1, MAX_ARTIFACT), value['retainUntil'])

    def wire(self):
        result = asdict(self)
        result['retainUntil'] = result.pop('retain_until')
        return result


@dataclass(frozen=True)
class VolumePath:
    pv_uid: str
    node_uid: str
    path: str

    @classmethod
    def parse(cls, value):
        fields(value, ('pvUid', 'nodeUid', 'path'))
        return cls(text(value['pvUid']), text(value['nodeUid']), absolute_path(value['path'], True))


@dataclass(frozen=True)
class CreationFence:
    """Reference to externally qualified enforcement, never its implementation.

    The qualification must cover the binding, its admission policy/dependencies,
    fail-closed enforcement and exact target namespace UID. Identity alone does
    not establish a fence, nor does a NetworkPolicy restrict object creation.
    """
    resource: Resource
    namespace_uid: str
    qualification_sha256: str

    @classmethod
    def parse(cls, value):
        fields(value, ('resource', 'namespaceUid', 'qualificationSha256'))
        resource = Resource.parse(value['resource'])
        require((resource.api_version, resource.kind) == FENCE_KIND)
        return cls(resource, text(value['namespaceUid']), sha(value['qualificationSha256']))

    def wire(self):
        return {'resource': self.resource.wire(), 'namespaceUid': self.namespace_uid,
                'qualificationSha256': self.qualification_sha256}


@dataclass(frozen=True)
class Binding:
    operation: str
    policy_id: str
    valid_from: str
    expires_at: str
    retain_until: str
    operator: str
    validator: str
    signer_sha256: str
    decisions_sha256: str
    source_cluster: Cluster
    target_cluster: Cluster
    source_namespace: Resource
    target_namespace: Resource
    protected_sources: tuple
    owned_targets: tuple
    paused_workload: Resource
    source_stores: tuple
    previous_replicas: int
    watcher: Resource
    watcher_before: tuple
    watcher_during: tuple
    protected_paths: tuple
    target_paths: tuple
    creation_fence: CreationFence | None
    policy: Artifact
    signature: Artifact
    restore_input: Artifact | None
    tools: tuple
    watcher_container_index: int = 0
    watcher_environment_index: int = 0

    def scope(self):
        value = asdict(self)
        del value['policy']
        del value['signature']
        return value

    def scope_sha256(self):
        return digest(canonical(self.scope()))


def _parse_binding(value):
    required = ('schemaVersion operation policyId validFrom expiresAt retainUntil operatorPrincipal '
                'validatorPrincipal signerSha256 retainedDecisionsSha256 sourceCluster targetCluster '
                'sourceNamespace targetNamespace protectedSources ownedTargets pausedWorkload sourceStores '
                'previousReplicas watcher watcherBefore watcherDuring protectedPaths targetPaths policy '
                'signature restoreInput tools creationFence').split()
    require(type(value) is dict and set(required) <= set(value) <=
            set(required + ['watcherContainerIndex', 'watcherEnvironmentIndex']))
    require(type(value['schemaVersion']) is int and value['schemaVersion'] == 1)
    operation = value['operation']
    require(operation in ('capture', 'restore', 'resume', 'cleanup'))
    for field in ('protectedSources', 'ownedTargets', 'sourceStores', 'watcherBefore',
                  'watcherDuring', 'protectedPaths', 'targetPaths'):
        require(type(value[field]) is list and len(value[field]) <= 64)
    fields(value['tools'], ('kubectl', 'artifactReader', 'signatureVerifier', 'stageRunner'))
    result = Binding(operation, text(value['policyId']), value['validFrom'], value['expiresAt'],
        value['retainUntil'], text(value['operatorPrincipal']), text(value['validatorPrincipal']),
        sha(value['signerSha256']), sha(value['retainedDecisionsSha256']),
        Cluster.parse(value['sourceCluster']), Cluster.parse(value['targetCluster']),
        Resource.parse(value['sourceNamespace']), Resource.parse(value['targetNamespace'], operation == 'capture'),
        tuple(Resource.parse(v) for v in value['protectedSources']),
        tuple(Resource.parse(v) for v in value['ownedTargets']), Resource.parse(value['pausedWorkload']),
        tuple(Resource.parse(v) for v in value['sourceStores']), integer(value['previousReplicas'], 1, 64),
        Resource.parse(value['watcher']), tuple(value['watcherBefore']), tuple(value['watcherDuring']),
        tuple(VolumePath.parse(v) for v in value['protectedPaths']),
        tuple(VolumePath.parse(v) for v in value['targetPaths']),
        None if value['creationFence'] is None else CreationFence.parse(value['creationFence']),
        Artifact.parse(value['policy']),
        Artifact.parse(value['signature']), None if value['restoreInput'] is None else Artifact.parse(value['restoreInput']),
        tuple((k, Tool.parse(v)) for k, v in sorted(value['tools'].items())),
        integer(value.get('watcherContainerIndex', 0), 0, 31), integer(value.get('watcherEnvironmentIndex', 0), 0, 127))
    validate_binding(result)
    return result


def parse_binding(value):
    try:
        return _parse_binding(value)
    except Exception:
        raise Refusal(FAILURE) from None


def _validate_binding(b):
    # Reparse typed fields too: direct dataclass construction cannot bypass validation.
    require(type(b) is Binding and b.operation in ('capture', 'restore', 'resume', 'cleanup'))
    for cluster in (b.source_cluster, b.target_cluster):
        require(type(cluster) is Cluster and Cluster.parse({'uid': cluster.uid, 'endpoint': cluster.endpoint,
                'caSha256': cluster.ca_sha256}) == cluster)
    require(b.operator != b.validator and text(b.operator) and text(b.validator) and
            text(b.policy_id) and sha(b.signer_sha256) and sha(b.decisions_sha256))
    start, end, retain = timestamp(b.valid_from), timestamp(b.expires_at), timestamp(b.retain_until)
    require(0 < (end - start).total_seconds() <= 86400 and retain > end)
    require(b.source_namespace.kind == b.target_namespace.kind == 'Namespace')
    require(b.source_namespace.uid is not None and b.source_namespace.name != b.target_namespace.name)
    same_cluster = b.source_cluster.uid == b.target_cluster.uid
    require((b.source_cluster == b.target_cluster) if same_cluster else
            b.source_cluster.endpoint != b.target_cluster.endpoint)
    require(0 < len(b.protected_sources) <= 64 and 0 < len(b.source_stores) <= 16 and
            len(b.owned_targets) <= 64 and len(b.target_paths) <= 32)
    for resource in (*b.protected_sources, *b.owned_targets, b.source_namespace, b.target_namespace,
                     b.paused_workload, b.watcher, *b.source_stores):
        require(type(resource) is Resource and Resource.parse(resource.wire(), resource == b.target_namespace
                and b.operation == 'capture') == resource)
    require(b.source_namespace in b.protected_sources and b.paused_workload in b.protected_sources and
            all(r in b.protected_sources for r in b.source_stores))
    require(b.paused_workload.kind == 'Deployment' and b.paused_workload.namespace == b.source_namespace.name)
    source_namespaces = {r.name for r in b.protected_sources if r.kind == 'Namespace'}
    require(b.target_namespace.name not in source_namespaces and
            all(not r.namespace or r.namespace in source_namespaces for r in b.protected_sources))
    require(b.watcher.kind == 'Deployment' and all(r.uid != b.watcher.uid and
            r.path() != b.watcher.path() for r in b.owned_targets))
    require(all((r.api_version, r.kind) in STORE_KINDS and
                r.namespace == b.source_namespace.name for r in b.source_stores) and
            len({r.uid for r in b.source_stores}) == len(b.source_stores) and
            len({r.path() for r in b.source_stores}) == len(b.source_stores))
    require(len({r.uid for r in b.protected_sources}) == len(b.protected_sources))
    require(len({r.path() for r in b.protected_sources}) == len(b.protected_sources))
    require(len({r.uid for r in b.owned_targets}) == len(b.owned_targets))
    require(len({r.path() for r in b.owned_targets}) == len(b.owned_targets))
    require(not ({r.uid for r in b.protected_sources} & {r.uid for r in b.owned_targets}))
    require(all(r.namespace in ('', b.target_namespace.name) and
                (r.kind != 'Namespace' or r == b.target_namespace) and
                (r.namespace != '' or r.kind in ('Namespace', 'PersistentVolume')) for r in b.owned_targets))
    require(not same_cluster or not ({r.path() for r in b.protected_sources} & {r.path() for r in b.owned_targets}))
    if b.operation == 'capture':
        require(b.target_namespace.uid is None and b.target_namespace.resource_version is None and
                not b.owned_targets and not b.target_paths and b.restore_input is None)
    else:
        require(b.target_namespace.uid is not None and b.target_namespace in b.owned_targets)
    if b.operation in ('restore', 'resume'):
        require(b.restore_input is not None)
    if b.operation == 'cleanup':
        require(type(b.creation_fence) is CreationFence and
                CreationFence.parse(b.creation_fence.wire()) == b.creation_fence and
                b.creation_fence.namespace_uid == b.target_namespace.uid and
                all(r.uid != b.creation_fence.resource.uid and
                    r.path() != b.creation_fence.resource.path() for r in b.owned_targets))
    else:
        require(b.creation_fence is None)
    integer(b.previous_replicas, 1, 64)
    integer(b.watcher_container_index, 0, 31)
    integer(b.watcher_environment_index, 0, 127)
    require(0 < len(b.watcher_before) <= 32 and len(set(b.watcher_before)) == len(b.watcher_before))
    for name in (*b.watcher_before, *b.watcher_during):
        text(name, 63, r'[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?')
    require(tuple(b.watcher_before) + (b.target_namespace.name,) == tuple(b.watcher_during))
    for path in (*b.protected_paths, *b.target_paths):
        require(type(path) is VolumePath and VolumePath.parse({'pvUid': path.pv_uid,
                'nodeUid': path.node_uid, 'path': path.path}) == path)
    require(len({p.pv_uid for p in b.protected_paths}) == len(b.protected_paths) and
            len({p.path for p in b.protected_paths}) == len(b.protected_paths) and
            {p.pv_uid for p in b.protected_paths} ==
            {r.uid for r in b.protected_sources if r.kind == 'PersistentVolume'})
    for target in b.target_paths:
        require(all(not (target.path == source.path or
                    Path(target.path) in Path(source.path).parents or
                    Path(source.path) in Path(target.path).parents) for source in b.protected_paths))
    require(len({p.path for p in b.target_paths}) == len(b.target_paths) and
            len({p.pv_uid for p in b.target_paths}) == len(b.target_paths))
    require({p.pv_uid for p in b.target_paths} == {r.uid for r in b.owned_targets if r.kind == 'PersistentVolume'})
    for artifact in (b.policy, b.signature, b.restore_input):
        if artifact is not None:
            require(type(artifact) is Artifact and Artifact.parse(artifact.wire()) == artifact and
                    timestamp(artifact.retain_until) >= retain)
    require(b.policy.size + b.signature.size <= MAX_STDIN // 2 and
            (b.policy.store, b.policy.key, b.policy.version) != (b.signature.store, b.signature.key, b.signature.version))
    require(tuple(k for k, _ in b.tools) == tuple(sorted(('kubectl', 'artifactReader', 'signatureVerifier', 'stageRunner'))))
    for _, tool in b.tools:
        require(tool is None or type(tool) is Tool and Tool.parse(asdict(tool)) == tool)


def validate_binding(b):
    try:
        _validate_binding(b)
    except Exception:
        raise Refusal(FAILURE) from None


@dataclass(frozen=True)
class Request:
    name: str
    role: str
    method: str
    endpoint: str
    path: str
    stdin: bytes
    expected: bytes
    effect: str
    principal: str
    tool: Tool | None
    argv: tuple | None
    timeout_seconds: int = TIMEOUT
    maximum_response_bytes: int = MAX_JSON
    maximum_error_bytes: int = 65536
    content_type: str = 'application/json'
    environment: tuple = (('LANG', 'C.UTF-8'),)
    credential_fd: int = 3
    stdin_materialization: str | None = None

    def private(self):
        value = asdict(self)
        value['stdin'] = base64.b64encode(value['stdin']).decode('ascii')
        value['stdinEncoding'] = 'base64'
        value['expected'] = strict_json(self.expected)
        return value

    def sha256(self):
        return digest(canonical(self.private()))


@dataclass(frozen=True)
class Response:
    exit_code: int
    status: int
    body: bytes
    version: str = ''
    retain_until: str = ''


@dataclass(frozen=True)
class RunResult:
    state: str
    requests: int
    failure: str | None
    operational_qualification: None = None
    recovery_acceptance: None = None
    source_resumed: None = None
    target_cleanup_complete: None = None


def _request(b, name, role, method, path, body, expected, effect, cluster=None, maximum=MAX_JSON):
    tool = dict(b.tools)[role]
    stdin = canonical(body)
    require(len(stdin) <= MAX_STDIN)
    # Native requests are API contracts, not invented kubectl patch/delete argv.
    # Protocol argv exists only when its exact future tool is explicitly bound.
    argv = None if tool is None or role == 'kubectl' else (
        tool.path, '--recovery-protocol-v1', '--request-stdin', '--credential-fd=3')
    principal = b.validator if role == 'artifactReader' else b.operator
    return Request(name, role, method, '' if cluster is None else cluster.endpoint, path,
                   stdin, canonical(expected), effect, principal, tool, argv,
                   maximum_response_bytes=maximum,
                   content_type='application/json-patch+json' if method == 'PATCH' else 'application/json')


def _get(b, name, resource, target=False, expected='identity'):
    return _request(b, name, 'kubectl', 'GET', resource.path(), {},
                    {'check': expected, 'resource': resource.wire()}, 'read-only',
                    b.target_cluster if target else b.source_cluster)


def _stage(b, name, expected, effect='read-only'):
    return _request(b, name, 'stageRunner', 'PROTOCOL', name,
        {'scopeSha256': b.scope_sha256(), 'sourceCluster': asdict(b.source_cluster),
         'targetCluster': asdict(b.target_cluster), 'sourceNamespace': b.source_namespace.wire(),
         'targetNamespace': b.target_namespace.wire(), 'protectedSources': [r.wire() for r in b.protected_sources],
         'protectedPaths': [asdict(p) for p in b.protected_paths],
         'ownedTargets': [r.wire() for r in b.owned_targets],
         'creationFence': None if b.creation_fence is None else b.creation_fence.wire(),
         'restoreInput': None if b.restore_input is None else b.restore_input.wire(),
         'targetPaths': [asdict(p) for p in b.target_paths]}, expected, effect)


def build_requests(b):
    """Deterministic complete protocol, including dependent semantic checks.

    Null tools and proposed target UIDs can be rendered, never executed.
    Target creation, quiesce, multipart transfer and full store orchestration
    are intentionally outside these primitives.
    """
    validate_binding(b)
    result = []
    for name, artifact in (('policy-readback', b.policy), ('signature-readback', b.signature)):
        result.append(_request(b, name, 'artifactReader', 'READ_EXACT_VERSION', artifact.key,
            artifact.wire(), {'check': 'immutable', 'artifact': artifact.wire()}, 'read-only', maximum=artifact.size))
    result.append(_request(b, 'authenticate-policy', 'signatureVerifier', 'AUTHENTICATE', 'policy-signature',
        {'policy': b.policy.wire(), 'signature': b.signature.wire(), 'scopeSha256': b.scope_sha256(),
         'signerSha256': b.signer_sha256, 'exactReadbackBytes': None}, {'check': 'authentication'}, 'read-only'))
    result[-1] = replace(result[-1], stdin_materialization='exact-policy-and-signature-readback-bytes')
    result.extend(_get(b, 'protect-source-' + str(i), r) for i, r in enumerate(b.protected_sources))
    if b.operation == 'capture':
        result.append(_get(b, 'proposed-target-absent', b.target_namespace, True, 'absence'))
        result.append(_stage(b, 'capture', {'check': 'capture'}, 'bounded-source-capture'))
    else:
        result.extend(_get(b, 'bind-target-' + str(i), r, True) for i, r in enumerate(b.owned_targets))
        if b.operation in ('restore', 'resume'):
            a = b.restore_input
            result.append(_request(b, 'recovery-input-readback', 'artifactReader', 'READ_EXACT_VERSION', a.key,
                a.wire(), {'check': 'immutable', 'artifact': a.wire()}, 'read-only', maximum=a.size))
            result.append(_stage(b, 'verify-isolation', {'check': 'isolation'}))
        if b.operation == 'restore':
            result.append(_stage(b, 'restore', {'check': 'restore'}, 'bounded-isolated-restore'))
            result.append(_stage(b, 'verify-restore', {'check': 'restore-verification'}))
            result.extend(_get(b, 'preserve-source-' + str(i), r) for i, r in enumerate(b.protected_sources))
        elif b.operation == 'resume':
            result.append(_stage(b, 'verify-restore', {'check': 'restore-verification'}))
            result.append(_stage(b, 'verify-restarted-stores', {'check': 'store-health'}))
            result.append(_stage(b, 'fresh-clean-drain', {'check': 'drain'}))
            result.append(_get(b, 'paused-workload', b.paused_workload, expected='paused'))
            patch = [{'op': 'test', 'path': '/metadata/uid', 'value': b.paused_workload.uid},
                     {'op': 'test', 'path': '/metadata/resourceVersion', 'value': b.paused_workload.resource_version},
                     {'op': 'test', 'path': '/spec/replicas', 'value': 0},
                     {'op': 'replace', 'path': '/spec/replicas', 'value': b.previous_replicas}]
            result.append(_request(b, 'resume-replicas', 'kubectl', 'PATCH', b.paused_workload.path(), patch,
                {'check': 'replica-patch', 'resource': b.paused_workload.wire()}, 'restore-recorded-replicas', b.source_cluster))
            result.append(_get(b, 'reconcile-resume', b.paused_workload, expected='reconciled'))
        elif b.operation == 'cleanup':
            result.append(_stage(b, 'verify-creation-fence-before-inventory', {'check': 'creation-fence'}))
            result.append(_stage(b, 'owned-target-inventory', {'check': 'inventory'}))
            # All identities are read before the first delete. Namespace last.
            rank = {'PersistentVolumeClaim': 2, 'PersistentVolume': 3, 'Namespace': 4}
            ordered = sorted(b.owned_targets, key=lambda r: (rank.get(r.kind, 1), r.path()))
            for i, r in enumerate(ordered):
                if r.kind == 'Namespace':
                    result.append(_stage(b, 'verify-creation-fence-before-namespace-delete', {'check': 'creation-fence'}))
                body = {'apiVersion': 'v1', 'kind': 'DeleteOptions', 'propagationPolicy': 'Foreground',
                        'preconditions': {'uid': r.uid, 'resourceVersion': r.resource_version}}
                result.append(_request(b, 'delete-owned-' + str(i), 'kubectl', 'DELETE', r.path(), body,
                    {'check': 'deleted', 'resource': r.wire()}, 'delete-recorded-owned-target', b.target_cluster))
            result.extend(_get(b, 'independent-absence-' + str(i), r, True, 'absence') for i, r in enumerate(ordered))
            for i, p in enumerate(b.target_paths):
                result.append(_request(b, 'independent-path-absence-' + str(i), 'stageRunner', 'PROTOCOL',
                    'verify-path-absence', asdict(p), {'check': 'path-absence', 'volume': asdict(p)}, 'read-only'))
            result.append(_get(b, 'watcher-before-restoration', b.watcher, expected='watcher-current'))
            base = '/spec/template/spec/containers/' + str(b.watcher_container_index)
            env = base + '/env/' + str(b.watcher_environment_index)
            patch = [{'op': 'test', 'path': '/metadata/uid', 'value': b.watcher.uid},
                     {'op': 'test', 'path': '/metadata/resourceVersion', 'value': b.watcher.resource_version},
                     {'op': 'test', 'path': env + '/name', 'value': 'WATCH_NAMESPACE'},
                     {'op': 'test', 'path': env + '/value', 'value': ','.join(b.watcher_during)},
                     {'op': 'replace', 'path': env + '/value', 'value': ','.join(b.watcher_before)}]
            result.append(_request(b, 'restore-watcher', 'kubectl', 'PATCH', b.watcher.path(), patch,
                {'check': 'watcher-restored', 'resource': b.watcher.wire()}, 'restore-exact-watcher-scope', b.source_cluster))
            result.append(_get(b, 'independent-watcher-readback', b.watcher, expected='watcher-restored'))
            result.extend(_get(b, 'preserve-source-' + str(i), r,
                expected='watcher-restored' if r.uid == b.watcher.uid and r.path() == b.watcher.path()
                else 'identity') for i, r in enumerate(b.protected_sources))
    require(len(result) <= 256)
    return tuple(result)


def _identity(value, r, exact_version=True):
    require(type(value) is dict and value.get('apiVersion') == r.api_version and value.get('kind') == r.kind)
    metadata = value.get('metadata')
    require(type(metadata) is dict and metadata.get('name') == r.name and
            metadata.get('namespace', '') == r.namespace and metadata.get('uid') == r.uid)
    text(metadata.get('resourceVersion'), 64, r'[0-9]+')
    if exact_version:
        require(metadata['resourceVersion'] == r.resource_version)


def _semantic(b, request, response, now):
    require(type(response) is Response and type(response.exit_code) is int and response.exit_code == 0 and
            type(response.status) is int and type(response.body) is bytes and
            len(response.body) <= request.maximum_response_bytes)
    expected = strict_json(request.expected)
    check = expected['check']
    if check == 'immutable':
        a = Artifact.parse(expected['artifact'])
        require(response.status == 200 and response.version == a.version and len(response.body) == a.size and
                digest(response.body) == a.sha256 and timestamp(response.retain_until) >= timestamp(b.retain_until) and
                response.retain_until == a.retain_until and request.principal == b.validator)
        if request.name == 'policy-readback':
            policy = strict_json(response.body)
            require(policy == {'kind': 'hexalith.recovery-adapter-policy', 'schemaVersion': 1,
                'scopeSha256': b.scope_sha256(), 'validFrom': b.valid_from, 'expiresAt': b.expires_at,
                'retainedDecisionsSha256': b.decisions_sha256})
        return
    value = strict_json(response.body)
    if check == 'absence':
        require(response.status == 404 and type(value) is dict and value.get('kind') == 'Status' and
                value.get('reason') == 'NotFound' and value.get('code') == 404 and
                value.get('details') == {'name': expected['resource']['name'],
                                         'kind': KINDS[(expected['resource']['apiVersion'], expected['resource']['kind'])][1]})
        return
    require(response.status == 200)
    if check == 'authentication':
        require(value == {'authenticatedPolicySha256': b.policy.sha256,
            'authenticatedSignatureSha256': b.signature.sha256, 'signerSha256': b.signer_sha256,
            'scopeSha256': b.scope_sha256(), 'algorithm': 'approved-detached-signature'})
    elif check in ('identity', 'paused', 'replica-patch', 'reconciled', 'watcher-current', 'watcher-restored'):
        r = Resource.parse(expected['resource'])
        _identity(value, r, check in ('identity', 'paused', 'watcher-current'))
        if check in ('paused', 'replica-patch', 'reconciled'):
            require(type(value.get('spec')) is dict and type(value['spec'].get('replicas')) is int and
                    value['spec']['replicas'] == (0 if check == 'paused' else b.previous_replicas))
        if check == 'reconciled':
            status = value.get('status', {})
            generation = integer(value['metadata'].get('generation'), 1)
            require(type(status) is dict and type(status.get('observedGeneration')) is int and
                    status['observedGeneration'] >= generation and
                    all(type(status.get(k)) is int and status[k] == b.previous_replicas for k in
                        ('replicas', 'updatedReplicas', 'readyReplicas', 'availableReplicas')))
        if check.startswith('watcher-'):
            try:
                env = value['spec']['template']['spec']['containers'][b.watcher_container_index]['env'][b.watcher_environment_index]
                require(env == {'name': 'WATCH_NAMESPACE', 'value': ','.join(
                    b.watcher_during if check == 'watcher-current' else b.watcher_before)})
            except Exception:
                raise Refusal(FAILURE) from None
    elif check == 'deleted':
        r = Resource.parse(expected['resource'])
        require(value == {'kind': 'Status', 'status': 'Success', 'details': {'uid': r.uid, 'name': r.name}})
    elif check == 'inventory':
        require(value == {'namespaceUid': b.target_namespace.uid,
                          'resources': [r.wire() for r in b.owned_targets]})
    elif check == 'path-absence':
        require(value == {'volume': expected['volume'], 'observation': 'path-not-found',
                          'nodeUid': expected['volume']['node_uid']})
    elif check == 'isolation':
        require(value == {'targetUid': b.target_namespace.uid, 'sourceNamespaceUid': b.source_namespace.uid,
            'networkMode': 'default-deny', 'hostMounts': [], 'sourceMounts': [], 'identity': 'isolated'})
    elif check in ('capture', 'restore', 'restore-verification'):
        expected_value = {'outcome': {'capture': 'captured', 'restore': 'restored',
            'restore-verification': 'verified'}[check], 'scopeSha256': b.scope_sha256(),
            'sourceUids': [r.uid for r in b.protected_sources], 'targetUid': b.target_namespace.uid}
        if check == 'capture':
            require(type(value) is dict and set(value) == set(expected_value) | {'contentSha256'})
            sha(value['contentSha256'])
            require({k: v for k, v in value.items() if k != 'contentSha256'} == expected_value)
        else:
            expected_value['inputSha256'] = b.restore_input.sha256
            require(value == expected_value)
    elif check == 'store-health':
        require(type(value) is dict and set(value) == {'stores'} and type(value['stores']) is list and
                len(value['stores']) == len(b.source_stores))
        for observed, store in zip(value['stores'], b.source_stores):
            fields(observed, ('uid', 'health', 'priorRuntimeId', 'runtimeId', 'restartObservation'))
            require(observed['uid'] == store.uid and observed['health'] == 'healthy' and
                    observed['restartObservation'] == 'restart-observed' and
                    text(observed['runtimeId']) != text(observed['priorRuntimeId']))
    elif check == 'drain':
        fields(value, ('observedAt', 'sourceNamespaceUid', 'pending', 'inFlight', 'missing', 'duplicates',
                       'streamBeforeSha256', 'streamAfterSha256'))
        age = (now - timestamp(value['observedAt'])).total_seconds()
        require(0 <= age <= 30 and value['sourceNamespaceUid'] == b.source_namespace.uid and
                all(type(value[k]) is int and value[k] == 0 for k in ('pending', 'inFlight', 'missing', 'duplicates')) and
                sha(value['streamBeforeSha256']) == sha(value['streamAfterSha256']))
    else:
        require(False)


def _window(b, now):
    require(type(now) is datetime and now.tzinfo is not None and timestamp(b.valid_from) <= now < timestamp(b.expires_at))


def run(b, backend, receipts, now=lambda: datetime.now(timezone.utc), monotonic=time.monotonic):
    """Execute the injected protocol once. This is never an operational proof.

    The approved backend must enforce the absolute deadline, exact executable
    custody, sealed fd 3 credential delivery and no ambient config/environment.
    This module has no backend implementation and never retries or compensates.
    """
    requests = build_requests(b)
    _window(b, now())
    require(all(r.tool is not None for r in requests))
    count = 0
    run_id = b.operation + '-' + b.scope_sha256()
    state = {'resume': 'stopped-pause-reconciliation-required',
             'cleanup': 'stopped-cleanup-incomplete'}.get(b.operation, 'stopped-incomplete')
    try:
        receipts.create(run_id + '-intent', canonical({'scopeSha256': b.scope_sha256(),
            'operation': b.operation, 'state': 'prepared', 'operationalQualification': None}))
        drain_at = None
        immutable = {}
        for index, request in enumerate(requests):
            _window(b, now())
            if request.name == 'authenticate-policy':
                require(set(immutable) == {'policy-readback', 'signature-readback'})
                body = strict_json(request.stdin)
                body['exactReadbackBytes'] = {name: base64.b64encode(immutable[name + '-readback']).decode('ascii')
                                            for name in ('policy', 'signature')}
                raw = canonical(body)
                require(len(raw) <= MAX_STDIN)
                request = replace(request, stdin=raw, stdin_materialization=None)
            if request.name == 'resume-replicas':
                require(drain_at is not None and 0 <= (now() - drain_at).total_seconds() <= 30)
            key = run_id + '-' + str(index).zfill(3)
            receipts.create(key + '-intent', canonical({'requestSha256': request.sha256(), 'state': 'pending'}))
            deadline = monotonic() + request.timeout_seconds
            count += 1
            response = backend.exchange(request, deadline)
            require(monotonic() < deadline)
            _window(b, now())
            _semantic(b, request, response, now())
            if request.name in ('policy-readback', 'signature-readback'):
                immutable[request.name] = response.body
            if request.name == 'fresh-clean-drain':
                drain_at = timestamp(strict_json(response.body)['observedAt'])
            receipts.create(key + '-result', canonical({'requestSha256': request.sha256(),
                'responseSha256': digest(response.body), 'state': 'semantic-check-passed',
                'operationalQualification': None}))
        state = b.operation + '-primitive-complete-unqualified'
        receipts.create(run_id + '-complete', canonical({'state': state, 'requests': count,
            'operationalQualification': None, 'recoveryAcceptance': None}))
        return RunResult(state, count, None)
    except Exception:
        # Retain prior intents/results and partial side effects. No retry, cleanup,
        # rollback, resume, multipart abort or guessed absence follows a failure.
        try:
            receipts.create(run_id + '-stopped', canonical({'state': state, 'requests': count,
                'failure': FAILURE, 'operationalQualification': None}))
        except Exception:
            pass
        return RunResult(state, count, FAILURE)


def _directory(path):
    path = Path(absolute_path(str(path)))
    directory = os.open('/', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        for part in path.parts[1:]:
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC, dir_fd=directory)
            os.close(directory)
            directory = child
            info = os.fstat(directory)
            mode = stat.S_IMODE(info.st_mode)
            sticky_root = info.st_uid == 0 and bool(mode & stat.S_ISVTX)
            require(info.st_uid in (0, os.getuid()) and (not mode & 0o022 or sticky_root))
        return directory
    except Exception:
        os.close(directory)
        raise Refusal(FAILURE) from None


def read_exact_local(path, expected):
    """Bounded no-follow local input read. Never used to load credentials."""
    sha(expected)
    path = Path(absolute_path(str(path)))
    directory = _directory(path.parent)
    fd = None
    try:
        fd = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC, dir_fd=directory)
        before = os.fstat(fd)
        require(stat.S_ISREG(before.st_mode) and before.st_nlink == 1 and before.st_uid == os.getuid() and
                stat.S_IMODE(before.st_mode) == 0o600 and 0 < before.st_size <= MAX_JSON)
        raw = bytearray()
        while len(raw) <= MAX_JSON:
            part = os.read(fd, min(65536, MAX_JSON + 1 - len(raw)))
            if not part:
                break
            raw.extend(part)
        after = os.fstat(fd)
        require(all(getattr(before, k) == getattr(after, k) for k in
                    ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns', 'st_ctime_ns', 'st_mode', 'st_nlink', 'st_uid')) and
                len(raw) == before.st_size and digest(raw) == expected)
        return bytes(raw)
    except Exception:
        raise Refusal(FAILURE) from None
    finally:
        if fd is not None:
            os.close(fd)
        os.close(directory)


class LocalReceipts:
    """Create-only 0600 records in an existing owner-only 0700 directory."""
    def __init__(self, path):
        self.path = Path(absolute_path(str(path)))

    def create(self, key, raw):
        text(key, 256, r'[A-Za-z0-9][A-Za-z0-9_.\-]*')
        require(type(raw) is bytes and len(raw) <= MAX_JSON)
        directory = _directory(self.path)
        fd = None
        try:
            info = os.fstat(directory)
            require(info.st_uid == os.getuid() and stat.S_IMODE(info.st_mode) == 0o700)
            fd = os.open(key, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_CLOEXEC,
                         0o600, dir_fd=directory)
            os.fchmod(fd, 0o600)
            offset = 0
            while offset < len(raw):
                written = os.write(fd, raw[offset:])
                require(written > 0)
                offset += written
            os.fsync(fd)
            os.fsync(directory)
        except Exception:
            raise Refusal(FAILURE) from None
        finally:
            if fd is not None:
                os.close(fd)
            os.close(directory)


def private_plan(b):
    requests = build_requests(b)
    return {'kind': 'hexalith.offline-recovery-adapter-plan', 'schemaVersion': 1,
            'scopeSha256': b.scope_sha256(), 'operation': b.operation,
            'requests': [r.private() for r in requests],
            'missingToolBindings': sorted({r.role for r in requests if r.tool is None}),
            'operationalQualification': None, 'operationalGate': 'closed',
            'policyAuthentication': None, 'recoveryAcceptance': None}


def render_proposal(value):
    """Render current facts with explicit null future dependencies. Never runnable.

    This separate schema deliberately cannot be passed to parse_binding/run.
    It supplies no date refresh, recovery RID, signature or invented target UID.
    """
    try:
        return _render_proposal(value)
    except Exception:
        raise Refusal(FAILURE) from None


def _render_proposal(value):
    fields(value, ('schemaVersion policyId observedAt sourceCluster targetCluster sourceNamespace '
        'targetNamespace protectedSources pausedWorkload sourceStores previousReplicas watcher '
        'watcherBefore watcherDuring operatorPrincipal validatorPrincipal signerSha256 '
        'retainedDecisionsSha256 tools preparationTools').split())
    require(type(value['schemaVersion']) is int and value['schemaVersion'] == 1)
    text(value['policyId'])
    timestamp(value['observedAt'])
    sha(value['retainedDecisionsSha256'])
    text(value['operatorPrincipal'])
    if value['validatorPrincipal'] is not None:
        text(value['validatorPrincipal'])
        require(value['validatorPrincipal'] != value['operatorPrincipal'])
    if value['signerSha256'] is not None:
        sha(value['signerSha256'])
    source, target = Cluster.parse(value['sourceCluster']), Cluster.parse(value['targetCluster'])
    require(source == target if source.uid == target.uid else source.endpoint != target.endpoint)
    source_ns = Resource.parse(value['sourceNamespace'])
    target_ns = Resource.parse(value['targetNamespace'], True)
    require(source_ns.kind == target_ns.kind == 'Namespace' and source_ns.name != target_ns.name and
            target_ns.uid is None and target_ns.resource_version is None)
    for key in ('protectedSources', 'sourceStores', 'watcherBefore', 'watcherDuring', 'preparationTools'):
        require(type(value[key]) is list and 0 < len(value[key]) <= 64)
    protected = tuple(Resource.parse(v) for v in value['protectedSources'])
    stores = tuple(Resource.parse(v) for v in value['sourceStores'])
    workload, watcher = Resource.parse(value['pausedWorkload']), Resource.parse(value['watcher'])
    require(source_ns in protected and workload in protected and all(r in protected for r in stores) and
            workload.kind == watcher.kind == 'Deployment' and workload.namespace == source_ns.name and
            len({r.uid for r in protected}) == len(protected) and
            all(not r.namespace or r.namespace in {n.name for n in protected if n.kind == 'Namespace'} for r in protected) and
            target_ns.name not in {n.name for n in protected if n.kind == 'Namespace'})
    integer(value['previousReplicas'], 1, 64)
    before, during = value['watcherBefore'], value['watcherDuring']
    for namespace in (*before, *during):
        text(namespace, 63, r'[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?')
    require(len(set(before)) == len(before) and before + [target_ns.name] == during)
    fields(value['tools'], ('kubectl', 'artifactReader', 'signatureVerifier', 'stageRunner'))
    tools = {k: Tool.parse(v) for k, v in value['tools'].items()}
    preparation = [Tool.parse(t) for t in value['preparationTools']]
    require(all(t is not None for t in preparation))
    known = [{'method': 'GET', 'endpoint': source.endpoint, 'path': r.path(), 'body': {},
              'expectedIdentity': r.wire(), 'effect': 'read-only'} for r in protected]
    known.append({'method': 'GET', 'endpoint': target.endpoint, 'path': target_ns.path(),
        'body': {}, 'expected': 'exact-NotFound-required', 'effect': 'read-only'})
    resume_patch = [{'op': 'test', 'path': '/metadata/uid', 'value': workload.uid},
        {'op': 'test', 'path': '/metadata/resourceVersion', 'value': None},
        {'op': 'test', 'path': '/spec/replicas', 'value': 0},
        {'op': 'replace', 'path': '/spec/replicas', 'value': value['previousReplicas']}]
    gates = {key: None for key in ('freshAuthenticatedPolicy', 'independentPolicySignatureReadback',
        'distinctValidatorCredentialQualification', 'currentAuthorityAndGrantLineage',
        'providerProtectionQualification', 'exactExecutableAndCredentialDeliveryQualification',
        'fullStoreOrchestrationQualification', 'captureImmutableVersionAndReadback',
        'createdIsolatedTargetUidAndResourceVersions', 'sourcePauseAndRestartObservations',
        'freshCleanDrainAndReconciliation', 'ownedStorageAndNodePathAbsence')}
    shapes = [
        {'operation': 'capture', 'method': 'PROTOCOL', 'path': 'capture', 'argv': None,
         'body': {'sourceCluster': asdict(source), 'protectedSources': [r.wire() for r in protected],
                  'proposedTargetNamespace': target_ns.wire()},
         'effect': 'bounded-source-capture-after-authenticated-policy', 'state': 'blocked'},
        {'operation': 'restore', 'method': 'PROTOCOL', 'path': 'restore', 'argv': None,
         'body': {'inputArtifact': None, 'targetNamespace': target_ns.wire()},
         'effect': 'restore-into-independently-verified-isolated-owned-target', 'state': 'blocked'},
        {'operation': 'resume', 'method': 'PATCH', 'endpoint': source.endpoint,
         'path': workload.path(), 'body': resume_patch, 'argv': None,
         'effect': 'restore-exact-recorded-replicas-after-restore-health-and-fresh-drain', 'state': 'blocked'},
        {'operation': 'cleanup', 'method': 'DELETE', 'endpoint': target.endpoint,
         'path': target_ns.path(), 'body': {'apiVersion': 'v1', 'kind': 'DeleteOptions',
             'propagationPolicy': 'Foreground', 'preconditions': {'uid': None, 'resourceVersion': None}},
         'ownedStorageIdentities': None, 'watcherIdentity': watcher.wire(),
         'watcherRestoreValue': ','.join(before), 'argv': None,
         'effect': 'delete-only-recorded-owned-targets-then-independent-absence-and-exact-watcher-restoration',
         'state': 'blocked'}]
    return {'kind': 'hexalith.offline-recovery-adapter-proposal', 'schemaVersion': 1,
        'proposalInputSha256': digest(canonical(value)), 'knownReadOnlyRequests': known,
        'dependentRequestShapes': shapes, 'unresolvedGates': gates,
        'missingToolBindings': sorted(k for k, t in tools.items() if t is None),
        'operationalGate': 'closed', 'operationalQualification': None, 'recoveryAcceptance': None,
        'backendRequests': 0, 'runnable': False}


class Parser(argparse.ArgumentParser):
    def error(self, message):
        raise Refusal(FAILURE)


def main(argv=None):
    try:
        argv = list(sys.argv[1:] if argv is None else argv)
        # Refuse before any local input/configuration/credential access.
        require(not any(a in ('live', 'activate', 'execute', '--live', '--activate', '--execute') for a in argv))
        parser = Parser(description='Local validation/rendering only; operational gate closed.')
        parser.add_argument('mode', choices=('validate', 'render', 'proposal'))
        parser.add_argument('--input', required=True)
        parser.add_argument('--sha256', required=True)
        parser.add_argument('--private-output')
        args = parser.parse_args(argv)
        value = strict_json(read_exact_local(args.input, args.sha256))
        if args.mode == 'proposal':
            plan = render_proposal(value)
            operation, scope, count = 'preparation', plan['proposalInputSha256'], len(plan['knownReadOnlyRequests'])
        else:
            b = parse_binding(value)
            _window(b, datetime.now(timezone.utc))
            plan = private_plan(b)
            operation, scope, count = b.operation, b.scope_sha256(), len(plan['requests'])
        raw = canonical(plan)
        if args.private_output is not None:
            require(args.mode in ('render', 'proposal'))
            target = Path(absolute_path(args.private_output))
            LocalReceipts(target.parent).create(target.name, raw)
        print(canonical({'result': 'consistent-offline-proposal' if args.mode == 'proposal' else 'consistent-offline-plan',
            'operation': operation, 'scopeSha256': scope, 'privatePlanSha256': digest(raw),
            'requestCount': count, 'missingToolBindings': plan['missingToolBindings'],
            'operationalGate': 'closed', 'operationalQualification': None,
            'authenticationPerformed': False, 'backendRequests': 0, 'recoveryAcceptance': None}).decode('ascii'))
        return 0
    except Exception:
        print(canonical({'result': FAILURE, 'operationalGate': 'closed',
                         'operationalQualification': None, 'backendRequests': 0}).decode('ascii'))
        return 1


if __name__ == '__main__':
    sys.exit(main())
