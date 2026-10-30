#!/usr/bin/env python3
"""Render a review packet only; no cluster, registry or domain mutations."""
from __future__ import annotations
import argparse
import copy
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import yaml

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
IDENTITIES = {'eventstore/gateway', 'tenants/domain', 'tenants/api', 'parties/domain',
              'memories/server', 'memories/domain', 'memories/clock', 'memories/telemetry'}
IMAGE = re.compile(r'[a-z0-9][a-z0-9.:-]*/[a-z0-9][a-z0-9/_.-]*@sha256:[0-9a-f]{64}')
OWNED_KINDS = {'ConfigMap', 'Service', 'StatefulSet', 'Deployment', 'PersistentVolumeClaim',
               'NetworkPolicy', 'ServiceAccount', 'Role', 'RoleBinding', 'ResourceQuota',
               'LimitRange', 'ExternalSecret'}


def require(condition, code):
    if not condition:
        raise ValueError(code)


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(path):
    return json.loads(Path(path).read_text())


def run(argv):
    # The complete subprocess allowlist is read-only. Never shell out to a profile command.
    allowed = (argv[:3] == ['git', 'rev-parse', 'HEAD'] or argv[1:3] == ['template', 'mcpcli-417']
               or argv[1:3] == ['version', '--short'] or argv[1:3] == ['inspect', '--raw'])
    require(allowed, 'external-command-not-read-only')
    result = subprocess.run([str(a) for a in argv], capture_output=True, timeout=90)
    require(result.returncode == 0, 'read-only-tool-failed:' + Path(str(argv[0])).name)
    return result.stdout


def source_inputs(root):
    paths = [root / 'apphost.cs', *sorted((root / 'src/Hexalith.Platform.Composition').glob('*.cs')),
             *sorted((root / 'eng/helm-qualification/chart').rglob('*')),
             *sorted((root / 'eng/mcpcli-operator-setup/chart').rglob('*')),
             *sorted((root / 'eng/mcpcli-operator-setup').glob('*.py')),
             *sorted((root / 'eng/mcpcli-operator-setup/graph-export').glob('*.cs*')),
             root / 'Directory.Build.props', root / 'Directory.Packages.props', root / 'global.json']
    for module in ('EventStore', 'Tenants', 'Parties', 'Memories'):
        paths.append(root / 'references' / ('Hexalith.' + module) / 'module/hexalith.module-manifest.v2.json')
    return {str(p.relative_to(root)): sha(p) for p in paths if p.is_file()}


def validate_profile(profile, root, canonical_graph):
    require(set(profile) == {'schema', 'sourceRevision', 'sourceHashes', 'canonicalGraphSha256', 'target',
            'expiresAt', 'tenant', 'operatorSubject', 'operatorRole', 'operatorPermissions', 'operatorDomains',
            'values', 'environmentManifests', 'privateSecretFiles', 'registryAuthFile', 'privateSeedFile', 'prerequisites'}, 'profile-fields-invalid')
    require(profile['schema'] == 'hexalith.mcpcli.operator-preparation/v1', 'profile-schema-invalid')
    require(re.fullmatch(r'[0-9a-f]{40}', profile['sourceRevision'] or '') is not None, 'source-revision-required')
    require(profile['sourceRevision'] == subprocess_git_revision(root), 'source-revision-drift')
    require(profile['sourceHashes'] == source_inputs(root), 'source-inputs-drift-or-unbound')
    require(sha(canonical_graph) == profile['canonicalGraphSha256'], 'canonical-graph-hash-mismatch')
    graph = read(canonical_graph)
    for original in (root / 'eng/helm-qualification/chart').rglob('*'):
        if original.is_file() and original.name != 'reference-graph.json':
            require(sha(original) == sha(root / 'eng/mcpcli-operator-setup/chart' / original.relative_to(root / 'eng/helm-qualification/chart')),
                    'task-chart-template-drift')
    require(graph == read(root / 'eng/mcpcli-operator-setup/chart/reference-graph.json'), 'canonical-chart-graph-mismatch')
    require({w['identity'] for w in graph['workloads']} == IDENTITIES and len(graph['workloads']) == 8,
            'complete-eight-workload-closure-required')
    target = profile['target']
    require(set(target) == {'context', 'api', 'namespace', 'release', 'localPort'}, 'target-fields-invalid')
    require(target['context'] == 'jpiquot@local' and target['api'] == 'https://192.168.1.30:6443', 'unapproved-target')
    for key in ('namespace', 'release'):
        require(re.fullmatch(r'mcpcli-417-[a-z0-9][a-z0-9-]{0,35}', target[key] or '') is not None, 'task-identity-required')
    require(type(target['localPort']) is int and 1024 <= target['localPort'] <= 65535, 'private-port-invalid')
    expiry = datetime.fromisoformat(profile['expiresAt'].replace('Z', '+00:00'))
    require(expiry.tzinfo is not None and 0 < (expiry - datetime.now(timezone.utc)).total_seconds() <= 86400,
            'expiry-must-be-within-24-hours')
    require(re.fullmatch(r'mcpcli-417-[a-zA-Z0-9-]+', profile['tenant'] or '') is not None, 'task-tenant-required')
    require(profile['operatorSubject'] and profile['operatorRole'] == 'TenantContributor', 'scoped-writer-role-required')
    require(profile['operatorPermissions'] == ['command:submit', 'query:read']
            and set(profile['operatorDomains']) == {'tenants', 'party'}, 'operator-scope-invalid')
    require(set(profile['prerequisites']) == {'daprMtls', 'restrictedInjector', 'privateGateway', 'privateSecretCreation',
            'taskDataServices', 'routingCatalog', 'writerActivation'}, 'prerequisites-required')
    require(all(isinstance(v, str) and v.strip() and 'TODO' not in v for v in profile['prerequisites'].values()),
            'prerequisite-bindings-required')
    values = profile['values']
    require(set(values['images']) == IDENTITIES and set(values['bindings']) == IDENTITIES, 'complete-image-and-input-bindings-required')
    for image in values['images'].values():
        require(IMAGE.fullmatch(image or '') is not None and not any(s in image for s in ('.invalid/', 'fixture', 'qualification')),
                'real-immutable-image-required')
    require(values['mode'] == 'normal' and values.get('replicas', 1) == 1 and not values.get('capacityProfile'), 'single-replica-normal-mode-required')
    for key in ('registryAuthFile', 'privateSeedFile'):
        if key == 'registryAuthFile' and not profile[key]:
            continue
        path = Path(profile[key] or '')
        require(path.is_absolute() and path.is_file() and not path.is_symlink(), 'private-file-unavailable:' + key)
        require(not path.is_relative_to(root) and (path.stat().st_mode & 0o077) == 0, 'private-file-location-or-mode-invalid:' + key)
    require(profile['registryAuthFile'] != profile['privateSeedFile'], 'private-authorities-must-be-separated')
    from seed import private_json, validate
    validate(private_json(profile['privateSeedFile']), target['namespace'], profile['tenant'])
    return graph


def subprocess_git_revision(root):
    # No Git mutations; the working tree content hashes are bound independently of HEAD.
    result = subprocess.run(['git', '-C', str(root), 'rev-parse', 'HEAD'], capture_output=True, timeout=15)
    require(result.returncode == 0, 'source-revision-unavailable')
    return result.stdout.decode().strip()


def environment_documents(profile):
    documents = profile['environmentManifests']
    require(isinstance(documents, list) and documents, 'task-environment-manifests-required')
    namespace = profile['target']['namespace']
    seen = set()
    for doc in documents:
        kind = doc.get('kind'); metadata = doc.get('metadata', {})
        require(kind in OWNED_KINDS and metadata.get('namespace') == namespace, 'external-or-cluster-resource-refused')
        identity = (kind, metadata.get('name'))
        require(identity not in seen and metadata.get('labels', {}).get('hexalith.com/operator-task') == namespace,
                'duplicate-or-unowned-environment-resource')
        seen.add(identity)
        require('ownerReferences' not in metadata and not metadata.get('finalizers'), 'external-resource-owner-refused')
        require(kind != 'Service' or doc.get('spec', {}).get('type', 'ClusterIP') == 'ClusterIP', 'public-service-refused')
        require(kind != 'PersistentVolumeClaim' or doc.get('spec', {}).get('resources', {}).get('requests', {}).get('storage'),
                'bounded-storage-required')
        if kind == 'ConfigMap':
            require(not any(re.search(r'(?i)(password|signingkey|privatekey|token|credential)', key) for key in doc.get('data', {})),
                    'configmap-credential-refused')
        if kind in ('Deployment', 'StatefulSet'):
            spec = doc['spec']; pod = spec['template']['spec']
            require(spec.get('replicas') == 1 and not pod.get('hostNetwork') and not pod.get('hostPID'), 'unsafe-data-workload')
            require(not any('hostPath' in v for v in pod.get('volumes', [])), 'host-mount-refused')
            for container in pod.get('containers', []) + pod.get('initContainers', []):
                require(IMAGE.fullmatch(container.get('image', '')) is not None, 'data-image-digest-required')
                require(all(container.get('resources', {}).get(k, {}).get(r) for k in ('requests', 'limits') for r in ('cpu', 'memory')),
                        'bounded-data-resources-required')
                require(not container.get('securityContext', {}).get('privileged'), 'privileged-workload-refused')
                require(not any('value' in e and any(k in e['name'].lower() for k in ('password', 'token', 'secret', 'signingkey'))
                                for e in container.get('env', [])), 'literal-environment-credential-refused')
    require(any(k == 'PersistentVolumeClaim' for k, _ in seen), 'task-owned-persistent-storage-required')
    require(('ConfigMap', profile['values']['environment']['componentConfigMap']) in seen, 'component-file-configmap-required')
    # Existing Secret references can be satisfied later from task-owned private files or an existing provider.
    return documents


def prepare(profile_path, canonical_graph, output, helm, skopeo, root=ROOT):
    output = Path(output).absolute()
    require(not output.exists() and not output.is_symlink(), 'output-already-exists')
    profile = read(profile_path)
    graph = validate_profile(profile, root, canonical_graph)
    environment = environment_documents(profile)
    for name, keys in profile['privateSecretFiles'].items():
        require(re.fullmatch(r'[a-z0-9][a-z0-9-]{0,62}', name) is not None and isinstance(keys, dict) and keys, 'private-secret-reference-invalid')
        for key, location in keys.items():
            path = Path(location)
            require(re.fullmatch(r'[-a-zA-Z0-9._]+', key) is not None and path.is_absolute() and path.is_file() and not path.is_symlink()
                    and not path.is_relative_to(root) and (path.stat().st_mode & 0o077) == 0, 'private-secret-file-unavailable-or-public')
    source_before = source_inputs(root)
    images = sorted(set(profile['values']['images'].values()) | {c['image'] for d in environment if d['kind'] in ('Deployment', 'StatefulSet')
                     for c in d['spec']['template']['spec'].get('containers', []) + d['spec']['template']['spec'].get('initContainers', [])})
    version = run([helm, 'version', '--short']).decode().strip()
    require(version.startswith('v4.2.0'), 'pinned-helm-4.2.0-required')
    with tempfile.TemporaryDirectory(prefix='mcpcli-417-values-') as temporary:
        private_values = Path(temporary) / 'values.json'
        private_values.write_text(json.dumps(profile['values']))
        private_values.chmod(0o600)
        raw = run([helm, 'template', 'mcpcli-417', root / 'eng/mcpcli-operator-setup/chart',
                   '--namespace', profile['target']['namespace'], '-f', private_values])
    pulled = {}
    for image in images:
        raw_manifest = run([skopeo, 'inspect', '--raw', *(['--authfile', profile['registryAuthFile']] if profile['registryAuthFile'] else []), 'docker://' + image])
        require(hashlib.sha256(raw_manifest).hexdigest() == image.rsplit(':', 1)[1], 'registry-manifest-digest-mismatch')
        pulled[image] = hashlib.sha256(raw_manifest).hexdigest()
    # Only validated public metadata reaches the fresh review directory.
    docs = [d for d in yaml.safe_load_all(raw) if d]
    # The declared public surfaces remain graph-bound; this task publishes none of their routes.
    secret_refs = {}
    def references(node):
        if isinstance(node, dict):
            if isinstance(node.get('secretKeyRef'), dict):
                ref = node['secretKeyRef']; secret_refs.setdefault(ref['name'], set()).add(ref['key'])
            for child in node.values(): references(child)
        elif isinstance(node, list):
            for child in node: references(child)
    references(docs)
    secret_refs.setdefault(profile['values']['imagePullSecret'], set()).add('.dockerconfigjson')
    external_secrets = {d['spec'].get('target', {}).get('name', d['metadata']['name'])
                        for d in environment if d['kind'] == 'ExternalSecret'}
    for name, keys in secret_refs.items():
        require(name in external_secrets or keys <= set(profile['privateSecretFiles'].get(name, {})),
                'unresolved-private-secret-reference:' + name)
    routes = [d for d in docs if d['kind'] == 'HTTPRoute']
    docs = [d for d in docs if d['kind'] != 'HTTPRoute']
    for doc in docs:
        doc.setdefault('metadata', {})['namespace'] = profile['target']['namespace']
        doc['metadata'].setdefault('labels', {})['hexalith.com/operator-task'] = profile['target']['namespace']
        if doc['kind'] == 'Deployment':
            doc['spec']['template']['spec']['volumes'][0]['emptyDir']['sizeLimit'] = '128Mi'
            doc['spec']['template']['metadata']['annotations'].update({
                'dapr.io/sidecar-cpu-request': '50m', 'dapr.io/sidecar-cpu-limit': '500m',
                'dapr.io/sidecar-memory-request': '64Mi', 'dapr.io/sidecar-memory-limit': '256Mi'})
    output.mkdir(parents=True)
    chart = output / 'chart'; shutil.copytree(root / 'eng/mcpcli-operator-setup/chart', chart)
    values = output / 'values.json'; values.write_text(json.dumps(profile['values'], indent=2) + '\n')
    namespace = {'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': profile['target']['namespace'],
        'labels': {'hexalith.com/operator-task': profile['target']['namespace'], 'pod-security.kubernetes.io/enforce': 'restricted'}}}
    (output / 'application.yaml').write_text(yaml.safe_dump_all(docs, sort_keys=False))
    (output / 'environment.yaml').write_text(yaml.safe_dump_all([namespace, *environment], sort_keys=False))
    require(source_before == source_inputs(root), 'source-changed-during-preparation')
    inventory = [{'kind': d['kind'], 'name': d['metadata']['name'], 'namespace': d['metadata'].get('namespace')}
                 for d in [namespace, *environment, *docs]]
    require(len({(i['kind'], i['namespace'], i['name']) for i in inventory}) == len(inventory), 'resource-identity-conflict')
    packet = {'schema': 'hexalith.mcpcli.operator-review/v1', 'status': 'review-required', 'applyAuthorized': False,
              'sourceRevision': profile['sourceRevision'], 'sourceHashes': source_before,
              'graphSha256': sha(canonical_graph), 'imagesPulled': pulled, 'helm': version, 'target': profile['target'],
              'expiresAt': profile['expiresAt'], 'tenant': profile['tenant'], 'operatorSubject': profile['operatorSubject'],
              'operatorRole': profile['operatorRole'], 'operatorPermissions': profile['operatorPermissions'],
              'operatorDomains': profile['operatorDomains'], 'prerequisites': profile['prerequisites'],
              'inventory': inventory, 'omittedPublicRoutes': [r['metadata']['name'] for r in routes],
              'privateSecretCreations': {name: sorted(keys) for name, keys in profile['privateSecretFiles'].items()},
              'seed': {'helper': 'seed.py', 'privateInputCopied': False, 'scope': ['system', profile['tenant']],
                       'privatePlanSha256': sha(profile['privateSeedFile']),
                       'requests': [{k: step[k] for k in ('name', 'method', 'url', 'credential', 'expectedStatus', 'assertions', 'body') if k in step}
                                    for step in read(profile['privateSeedFile'])['steps']],
                       'steps': ['BootstrapGlobalAdmin (bound workload)', 'CreateTenant', 'AddUserToTenant (TenantContributor)',
                                 'GetTenant persisted confirmation', 'Memories provisioning and Active confirmation']},
              'access': ['kubectl', '--context', profile['target']['context'], '-n', profile['target']['namespace'], 'port-forward',
                         'service/eventstore', str(profile['target']['localPort']) + ':80', '--address=127.0.0.1'],
              'cleanup': 'After sessions, compare the live namespace UID and task label with the install receipt; delete only this namespace. Retain cleanup outcome.',
              'files': {p.name: sha(p) for p in output.iterdir() if p.is_file()}}
    (output / 'review.json').write_text(json.dumps(packet, indent=2) + '\n')
    return packet


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile', type=Path, required=True); parser.add_argument('--canonical-graph', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True); parser.add_argument('--helm', required=True); parser.add_argument('--skopeo', default='skopeo')
    args = parser.parse_args()
    try:
        prepare(args.profile, args.canonical_graph, args.output, args.helm, args.skopeo)
    except (ValueError, KeyError, OSError, subprocess.SubprocessError) as error:
        # Error values never include arbitrary profile/credential contents or child stderr.
        print('Preparation refused: ' + (str(error) if isinstance(error, ValueError) else type(error).__name__), file=sys.stderr)
        return 1
    print('Review packet rendered. Deployment is not authorized.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
