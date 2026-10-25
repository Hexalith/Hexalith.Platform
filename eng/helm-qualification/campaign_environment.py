"""Environment layer for the maintained chart; no lifecycle or domain assertions."""
from __future__ import annotations

import copy
import json
import re
from pathlib import Path

import yaml

import campaign_capacity as capacity

RUN_LABEL = 'hexalith.com/campaign-run'
SECURITY = {'allowPrivilegeEscalation': False, 'capabilities': {'drop': ['ALL']},
            'readOnlyRootFilesystem': True, 'runAsNonRoot': True,
            'seccompProfile': {'type': 'RuntimeDefault'}}


def require(value, code):
    capacity.require(value, 'environment-' + code)


def image(value):
    require(isinstance(value, str) and re.fullmatch(r'[^\s]+@sha256:[0-9a-f]{64}', value), 'immutable-image-required')
    require('.invalid/' not in value and len(set(value.rsplit(':', 1)[-1])) > 2, 'fixture-image-refused')
    return value


def label(obj, run_id, namespace=None):
    obj = copy.deepcopy(obj)
    obj.setdefault('metadata', {}).setdefault('labels', {})[RUN_LABEL] = run_id
    if namespace is not None:
        obj['metadata']['namespace'] = namespace
    if obj.get('kind') in ('Deployment', 'StatefulSet', 'Job'):
        obj['spec']['template'].setdefault('metadata', {}).setdefault('labels', {})[RUN_LABEL] = run_id
    return obj


def dapr_values(images):
    """Keys checked against the pinned 1.18.7 chart, including its digest branch.

    Replication exercises member faults on one kind node; it is not node HA.
    Placement's three 256Mi claims fit inside the existing control allowance.
    Scheduler's quota is below the measured-required 1Gi per-member filesystem.
    """
    require(set(images) == {'operator', 'placement', 'scheduler', 'sentry', 'injector', 'daprd'}, 'dapr-images-incomplete')
    for name, value in images.items():
        image(value)
        require(value.startswith('ghcr.io/dapr/' + name + ':1.18.7@sha256:'), 'dapr-version-drift')
    values = {'global': {'ha': {'enabled': True, 'replicaCount': 3}, 'scheduler': {'enabled': True},
                        'mtls': {'enabled': True}, 'tag': '1.18.7'},
              'dapr_placement': {'volumeclaims': {'storageSize': '256Mi', 'storageClassName': 'standard'}},
              'dapr_scheduler': {'cluster': {'inMemoryStorage': False, 'storageSize': '1Gi', 'storageClassName': 'standard'},
                                 'etcdSpaceQuota': '536870912',
                                 'resources': {'requests': {'cpu': '250m', 'memory': '341Mi'}, 'limits': {'cpu': '250m', 'memory': '341Mi'}}},
              'dapr_sidecar_injector': {'sidecarRunAsNonRoot': True, 'sidecarRunAsUser': 1654,
                                       'sidecarReadOnlyRootFilesystem': True, 'sidecarDropALLCapabilities': True,
                                       'webhookFailurePolicy': 'Fail', 'image': {'name': images['daprd']},
                                       'injectorImage': {'name': images['injector']}}}
    for name in ('operator', 'placement', 'scheduler', 'sentry'):
        values.setdefault('dapr_' + name, {})['image'] = {'name': images[name]}
    # Twelve control pods at 100m/128Mi + OpenBao 500m/512Mi stay below
    # the configured 2CPU/3GiB control envelope; measurements still gate use.
    for name in ('operator', 'placement', 'sentry', 'sidecar_injector'):
        values['dapr_' + name]['resources'] = {'requests': {'cpu': '100m', 'memory': '128Mi'},
                                              'limits': {'cpu': '100m', 'memory': '128Mi'}}
    return values


def validate_dapr_render(objects):
    states = {o['metadata']['name']: o for o in objects if o.get('kind') == 'StatefulSet'}
    require({'dapr-placement-server', 'dapr-scheduler-server'} <= set(states), 'fault-topology-missing')
    for name, size in (('dapr-placement-server', '256Mi'), ('dapr-scheduler-server', '1Gi')):
        require(states[name]['spec']['replicas'] == 3, 'fault-members-missing')
        claims = states[name]['spec']['volumeClaimTemplates']
        require(len(claims) == 1 and claims[0]['spec']['resources']['requests']['storage'] == size
                and claims[0]['spec']['storageClassName'] == 'standard', 'persistent-member-allocation')
    for obj in objects:
        if obj.get('kind') in {'Deployment', 'StatefulSet'}:
            for container in obj['spec']['template']['spec']['containers']:
                image(container['image'])


def supplemental_budget(objects):
    """Reserve provider/reporter/CNI limits even when a Job is suspended."""
    cpu = memory = 0
    for obj in objects:
        if obj.get('kind') not in {'Deployment', 'StatefulSet', 'DaemonSet', 'Job'}:
            continue
        spec = obj['spec']; pod = spec['template']['spec']
        count = spec.get('replicas', 1) if obj['kind'] != 'Job' else spec.get('parallelism', 1)
        require(type(count) is int and count > 0, 'provider-replicas')
        for c in pod.get('containers', []) + pod.get('initContainers', []):
            limits = c.get('resources', {}).get('limits', {})
            raw_cpu, raw_memory = str(limits.get('cpu', '')), str(limits.get('memory', ''))
            require(re.fullmatch(r'[0-9]+m', raw_cpu) is not None, 'provider-cpu-limit-millicores-required')
            require(re.fullmatch(r'[0-9]+(?:Mi|Gi)', raw_memory) is not None, 'provider-memory-limit-required')
            cpu += count * int(raw_cpu[:-1])
            memory += count * int(raw_memory[:-2]) * (1024 ** (2 if raw_memory.endswith('Mi') else 3))
    # Twelve Dapr control members plus OpenBao consume 1700m/2048Mi;
    # provider services, CNI and reporter share the remaining control allocation.
    require(cpu <= capacity.BOUNDS['controlCpuMilli'] - 1700 and memory <= capacity.BOUNDS['controlMemoryBytes'] - 2 * capacity.GIB,
            'supplemental-allocation-exceeds-control-envelope')


def provider_namespaces(config, application_namespace):
    names = config['providerNamespaces']
    forbidden = {'default', 'kube-system', 'kube-public', 'kube-node-lease', 'local-path-storage', 'dapr-system', application_namespace}
    require(isinstance(names, list) and 1 <= len(names) <= 4 and len(names) == len(set(names)) and 'monitoring' in names
            and all(isinstance(name, str) and re.fullmatch('[a-z][a-z0-9-]{0,61}[a-z0-9]', name) and name not in forbidden for name in names), 'provider-namespace-declaration')
    return set(names)


def validate_provider_scope(objects, application_namespace, declared):
    namespaces = [o for o in objects if o.get('kind') == 'Namespace']
    require({o.get('metadata', {}).get('name') for o in namespaces} == set(declared) and len(namespaces) == len(declared), 'provider-namespaces-missing')
    for obj in objects:
        metadata = obj.get('metadata', {})
        if obj.get('kind') == 'Namespace':
            require('namespace' not in metadata and all(metadata.get('labels', {}).get('pod-security.kubernetes.io/' + mode) == 'restricted' for mode in ('enforce', 'audit', 'warn')), 'provider-namespace-security')
        else:
            require(metadata.get('namespace', application_namespace) in {application_namespace, *declared}, 'foreign-namespace')
    prometheus = [o for o in objects if o.get('kind') == 'Service' and o.get('metadata', {}).get('namespace') == 'monitoring' and o['metadata']['name'] == 'prometheus-operated']
    require(len(prometheus) == 1 and any(p.get('port') == 9090 for p in prometheus[0].get('spec', {}).get('ports', [])), 'prometheus-federation-service-required')
    workloads = [o for o in objects if o.get('kind') in {'Deployment', 'StatefulSet'} and o.get('metadata', {}).get('namespace') == 'monitoring']
    require(any(o['spec']['template']['metadata'].get('labels', {}).get('app.kubernetes.io/name') == 'opentelemetry-collector' for o in workloads), 'opentelemetry-collector-required')
    selector = prometheus[0]['spec'].get('selector', {})
    require(selector and any(all(o['spec']['template']['metadata'].get('labels', {}).get(k) == v for k, v in selector.items()) for o in workloads), 'prometheus-backend-required')


def topology(profile, config, run_id):
    """Render real TLS services using separately supplied private bootstrap inputs.

    PostgreSQL bootstrap SQL, TLS certificates, scoped provider tokens, and the
    OpenBao KV seed are required inputs, never generated fixture credentials.
    The existing application chart remains the sole workload definition.
    """
    require(set(config) == {'daprImages', 'componentFiles', 'providerObjects', 'reporterObjects', 'secrets', 'openbaoSeed', 'postgresqlTemplate', 'faultWindows', 'monitoring', 'leaseHolders', 'providerNamespaces'}, 'configuration-incomplete')
    ns = profile['target']['namespace']
    declared_namespaces = provider_namespaces(config, ns)
    backend = capacity.retained_json(profile['inputs']['backendImages'][0])
    for name in ('postgresql', 'openbao'):
        image(backend[name])
    components = capacity.retained_json(config['componentFiles'])
    require(isinstance(components, dict) and bool(components) and all(isinstance(v, str) and v for v in components.values()), 'components-missing')
    values = capacity.retained_json(profile['inputs']['values'][0])
    require('.invalid' not in json.dumps(values) and 'fixture' not in json.dumps(values).lower(), 'fixture-values-refused')
    for value in values['images'].values():
        image(value)
    for value in config['secrets']:
        capacity.reference(value)
    secret_objects = [capacity.retained_json(ref) for ref in config['secrets']]
    required = {'access-telemetry-postgresql-tls', 'access-telemetry-postgresql-bootstrap', 'campaign-openbao-tls', values['imagePullSecret']}
    for binding in values['bindings'].values():
        required.update(binding[key]['name'] for key in ('appToken', 'apiToken'))
    actual = set()
    for secret in secret_objects:
        require(secret.get('kind') == 'Secret' and secret.get('apiVersion') == 'v1'
                and secret.get('metadata', {}).get('namespace') in {ns, *declared_namespaces} and bool(secret.get('data')), 'credential-invalid')
        if secret['metadata']['namespace'] == ns:
            actual.add(secret['metadata']['name'])
    require(required <= actual, 'credentials-missing')
    by_name = {s['metadata']['name']: s for s in secret_objects if s['metadata']['namespace'] == ns}
    require({'tls.crt', 'tls.key', 'ca.crt'} <= set(by_name['access-telemetry-postgresql-tls']['data']), 'postgres-tls-missing')
    require({'tls.crt', 'tls.key', 'ca.crt'} <= set(by_name['campaign-openbao-tls']['data']), 'openbao-tls-missing')
    require({'admin-password', 'runtime-password'} <= set(by_name['access-telemetry-postgresql-bootstrap']['data']), 'postgres-bootstrap-missing')
    seed = capacity.retained_json(config['openbaoSeed'])
    require(isinstance(seed, dict) and set(seed) == {'entries', 'policies', 'tokens'} and all(seed.values()), 'openbao-seed-missing')
    objects = [{'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': ns, 'labels': {
        'pod-security.kubernetes.io/enforce': 'restricted', 'pod-security.kubernetes.io/audit': 'restricted',
        'pod-security.kubernetes.io/warn': 'restricted'}}},
        {'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': values['environment']['componentConfigMap']}, 'data': components}]
    # Reuse the existing owner conformance template's bootstrap, SCRAM/TLS,
    # peer mapping, role separation and physical inspection extension. Only the
    # explicitly new finite allocation/storage/profile fields are specialized.
    capacity.reference(config['postgresqlTemplate'])
    postgres = [o for o in yaml.safe_load_all(capacity.snapshot(config['postgresqlTemplate']['path'], config['postgresqlTemplate']['sha256'])) if o]
    pg = next((o for o in postgres if o.get('kind') == 'StatefulSet' and o['metadata']['name'] == 'access-telemetry-postgresql'), None)
    require(pg is not None, 'postgres-template-required')
    container = pg['spec']['template']['spec']['containers'][0]
    require(container['name'] == 'postgresql' and container['image'] == backend['postgresql']
            and pg['spec']['template']['spec']['serviceAccountName'] == 'access-telemetry-postgresql', 'postgres-consumer-binding')
    pg['metadata']['labels']['hexalith.io/profile'] = capacity.PROFILE_ID
    pg['spec']['template']['metadata']['labels']['hexalith.io/profile'] = capacity.PROFILE_ID
    pg['spec']['template']['metadata']['annotations']['hexalith.io/profile-id'] = capacity.PROFILE_ID
    for claim in pg['spec']['volumeClaimTemplates']:
        claim['metadata']['labels']['hexalith.io/profile'] = capacity.PROFILE_ID
        claim['spec']['storageClassName'] = 'standard'
        claim['spec']['resources']['requests']['storage'] = str(profile['sizing']['allocations']['postgres'])
    objects.extend(postgres)
    objects.extend([
        {'apiVersion': 'v1', 'kind': 'PersistentVolumeClaim', 'metadata': {'name': 'campaign-openbao'},
         'spec': {'accessModes': ['ReadWriteOnce'], 'storageClassName': 'standard', 'resources': {'requests': {'storage': str(capacity.GIB)}}}},
        {'apiVersion': 'v1', 'kind': 'Service', 'metadata': {'name': 'campaign-openbao'},
         'spec': {'selector': {'app': 'campaign-openbao'}, 'ports': [{'port': 8200, 'targetPort': 8200}]}},
        {'apiVersion': 'v1', 'kind': 'ServiceAccount', 'metadata': {'name': 'campaign-openbao'}, 'automountServiceAccountToken': False}])
    bao = {'name': 'openbao', 'image': backend['openbao'], 'securityContext': {**SECURITY, 'runAsUser': 100, 'runAsGroup': 1000},
           'args': ['server', '-config=/config/openbao.hcl'],
           'env': [{'name': 'BAO_ADDR', 'value': 'https://127.0.0.1:8200'}, {'name': 'BAO_CACERT', 'value': '/tls/ca.crt'}],
           'volumeMounts': [{'name': 'data', 'mountPath': '/data'}, {'name': 'tls', 'mountPath': '/tls', 'readOnly': True},
                           {'name': 'config', 'mountPath': '/config', 'readOnly': True}],
           'resources': {'requests': {'cpu': '500m', 'memory': '512Mi'}, 'limits': {'cpu': '500m', 'memory': '512Mi'}},
           'readinessProbe': {'exec': {'command': ['bao', 'status']}, 'periodSeconds': 5}}
    objects.append({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 'campaign-openbao'}, 'data': {'openbao.hcl':
        'disable_mlock = true\nui = false\napi_addr = "https://campaign-openbao.' + ns + '.svc.cluster.local:8200"\n'
        'cluster_addr = "https://127.0.0.1:8201"\n'
        'storage "raft" { path = "/data" node_id = "campaign-openbao-0" }\n'
        'listener "tcp" { address = "0.0.0.0:8200" tls_cert_file = "/tls/tls.crt" tls_key_file = "/tls/tls.key" }\n'}})
    for name, container, uid, gid in (('openbao', bao, 100, 1000),):
        volumes = [{'name': 'data', 'persistentVolumeClaim': {'claimName': 'campaign-' + name}},
                   {'name': 'tls', 'secret': {'secretName': 'campaign-' + name + '-tls', 'defaultMode': 0o440}}]
        volumes.append({'name': 'config', 'configMap': {'name': 'campaign-openbao'}})
        objects.append({'apiVersion': 'apps/v1', 'kind': 'StatefulSet', 'metadata': {'name': 'campaign-' + name},
            'spec': {'replicas': 1, 'serviceName': 'campaign-' + name, 'selector': {'matchLabels': {'app': 'campaign-' + name}},
                     'template': {'metadata': {'labels': {'app': 'campaign-' + name}}, 'spec': {
                         'automountServiceAccountToken': False, 'serviceAccountName': 'campaign-' + name, 'securityContext': {'runAsNonRoot': True, 'runAsUser': uid, 'runAsGroup': gid,
                             'fsGroup': gid, 'seccompProfile': {'type': 'RuntimeDefault'}}, 'containers': [container], 'volumes': volumes}}}})
    for group in ('providerObjects', 'reporterObjects'):
        supplied = capacity.retained_json(config[group])
        require(isinstance(supplied, list) and bool(supplied), 'provider-or-reporter-missing')
        if group == 'providerObjects':
            validate_provider_scope(supplied, ns, declared_namespaces)
        for obj in supplied:
            require(obj.get('kind') in {'Namespace', 'ConfigMap', 'Service', 'ServiceAccount', 'NetworkPolicy', 'Role', 'RoleBinding', 'Deployment', 'StatefulSet', 'Job'}, 'provider-kind-refused')
            if obj['kind'] == 'Namespace':
                require(group == 'providerObjects' and obj['metadata']['name'] in declared_namespaces, 'foreign-namespace')
            else:
                require(obj.get('metadata', {}).get('namespace', ns) in ({ns, *declared_namespaces} if group == 'providerObjects' else {ns}), 'foreign-namespace')
            if obj['kind'] in {'Deployment', 'StatefulSet', 'Job'}:
                for container in obj['spec']['template']['spec']['containers']:
                    image(container['image'])
                    require(all(container.get('securityContext', {}).get(k) == v for k, v in SECURITY.items()), 'provider-security')
                    require(bool(container.get('resources', {}).get('limits')), 'provider-limits')
            objects.append(obj)
    reporter = [o for o in objects if o.get('kind') == 'Job' and o['metadata']['name'] == 'access-telemetry-physical-evidence-reporter']
    require(len(reporter) == 1 and reporter[0]['spec']['template']['spec'].get('automountServiceAccountToken') is False
            and reporter[0]['spec']['template']['spec'].get('serviceAccountName') == 'access-telemetry-adapter'
            and not any('serviceAccountToken' in source for volume in reporter[0]['spec']['template']['spec'].get('volumes', []) for source in volume.get('projected', {}).get('sources', []))
            and not any(o.get('kind') == 'RoleBinding' and any(subject.get('kind') == 'ServiceAccount' and subject.get('name') == 'access-telemetry-adapter' for subject in o.get('subjects', [])) for o in objects)
            and reporter[0]['spec']['suspend'] is True and reporter[0]['spec']['backoffLimit'] == 0, 'reporter-invalid')
    for namespace in {ns, *declared_namespaces}:
        require(any(o.get('kind') == 'NetworkPolicy' and o['metadata'].get('namespace', ns) == namespace and o['spec'].get('podSelector') == {} and set(o['spec'].get('policyTypes', [])) == {'Ingress', 'Egress'} for o in objects), 'network-policy-required')
    require(any(o.get('kind') == 'StatefulSet' and o['metadata']['name'] == 'redis-stack'
                and any(c['name'] == 'redis' for c in o['spec']['template']['spec']['containers']) for o in objects), 'redis-fault-target-required')
    graph = json.loads((Path(__file__).parent / 'chart/reference-graph.json').read_bytes())
    required_accounts = {w['resourceId'] for w in graph['workloads']} | {'memories', 'memories-access-telemetry', 'memories-access-telemetry-clock', 'access-telemetry-postgresql', 'access-telemetry-adapter'}
    existing_accounts = {o['metadata']['name'] for o in objects if o['kind'] == 'ServiceAccount' and o['metadata'].get('namespace', ns) == ns}
    for name in sorted(required_accounts - existing_accounts):
        objects.append({'apiVersion': 'v1', 'kind': 'ServiceAccount', 'metadata': {'name': name}, 'automountServiceAccountToken': False})
    identities = [(o['kind'], o['metadata'].get('namespace', ns) if o['kind'] != 'Namespace' else '', o['metadata']['name']) for o in objects]
    require(len(identities) == len(set(identities)), 'duplicate-resource')
    return [label(o, run_id, None if o['kind'] == 'Namespace' else o['metadata'].get('namespace', ns)) for o in objects]
