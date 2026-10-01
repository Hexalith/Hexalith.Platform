#!/usr/bin/env python3
"""Exercise bounded native deletion on a disposable source-fenced synthetic cluster."""
import argparse
import copy
from datetime import datetime, timezone
import ipaddress
import json
import os
from pathlib import Path
import re
import subprocess
import urllib.parse

from evidence import Attempt, canonical, digest, file_digest, now
from qualify import PROTECTED_KINDS, project_resource


def key(obj):
    return (obj['apiVersion'], obj['kind'], obj.get('namespace'), obj['name'])


def absence_state(result, kind, name):
    """An unavailable Docker daemon is not evidence of object absence."""
    if result.returncode == 0:
        return 'present'
    messages = result.stderr.decode(errors='replace').strip().lower().splitlines()
    expected = {
        'container': {f'Error: No such object: {name}', f'Error: No such container: {name}',
                      f'Error response from daemon: No such container: {name}'},
        'network': {f'Error response from daemon: network {name} not found', f'Error: No such network: {name}'},
        'volume': {f'Error response from daemon: get {name}: no such volume', f'Error: No such volume: {name}'},
    }
    expected_messages = {message.lower() for message in expected[kind]}
    return 'absent' if result.returncode == 1 and messages and all(m in expected_messages for m in messages) else 'unverified'


def probe_blocked(result):
    if result.returncode != 0 or result.stdout not in (b'blocked\n', b'reachable\n'):
        raise ValueError('fence-probe-did-not-execute')
    return result.stdout == b'blocked\n'


def validate_isolation(source, fixture, network_internal, probes):
    endpoint = urllib.parse.urlparse(fixture.get('endpoint', ''))
    if (not network_internal or endpoint.hostname not in ('127.0.0.1', 'localhost', '::1')
            or fixture.get('clusterUid') == source.get('sourceClusterUid')
            or not fixture.get('clusterUid') or not source.get('sourceClusterUid')
            or fixture.get('sourceCredentialsImported') is not False
            or fixture.get('sourceDataImported') is not False
            or not probes or not all(v.get('blocked') is True for v in probes)):
        raise ValueError('source-isolation-unverified')


def validate_allowlist(inventory, actions):
    indexed = {key(o): o for o in inventory}
    by_uid = {o['uid']: o for o in inventory}
    seen = set()
    for action in actions:
        k = key(action)
        if k in seen or k not in indexed or any('*' in str(v) for v in k):
            raise ValueError('allowlist-missing-duplicate-or-wildcard')
        seen.add(k)
        current = indexed[k]
        if action.get('uid') != current['uid'] or action.get('resourceVersion') != current['resourceVersion']:
            raise ValueError('uid-or-resource-version-drift')
        if current['kind'] in PROTECTED_KINDS:
            raise ValueError('protected-resource-in-removal-scope')
        if action.get('action') not in ('delete', 'remove-named-finalizer'):
            raise ValueError('unsupported-retirement-action')
        if action.get('propagation') not in ('Foreground', 'Orphan'):
            raise ValueError('unreviewed-propagation')
        if action['action'] == 'remove-named-finalizer' and action.get('finalizer') not in current['finalizers']:
            raise ValueError('unnamed-or-drifted-finalizer-intervention')
    deleted = {a['uid'] for a in actions if a['action'] == 'delete'}
    closure = set(deleted)
    while True:
        descendants = {o['uid'] for o in inventory if any(v.get('uid') in closure for v in o['owners'])}
        expanded = closure | descendants
        if expanded == closure:
            break
        closure = expanded
    if closure - deleted:
        # Every cascaded deletion must be an explicit reviewed member too.
        raise ValueError('unallowlisted-deletion-propagation')
    if any(by_uid[uid]['kind'] in PROTECTED_KINDS for uid in closure):
        raise ValueError('propagation-threatens-protected-resource')


def assert_preserved(before, after, expected_deleted):
    initial = {key(v): v for v in before}
    current = {key(v): v for v in after}
    removed = set(initial) - set(current)
    if removed != set(expected_deleted):
        raise ValueError('unexpected-or-incomplete-deletion')
    for k in set(initial) & set(current):
        if initial[k]['uid'] != current[k]['uid'] or initial[k].get('binding') != current[k].get('binding'):
            raise ValueError('preserved-identity-or-binding-changed')


class Fixture:
    """All native mutations go through docker exec into the newly created node."""
    def __init__(self, args, attempt):
        self.args, self.attempt = args, attempt
        self.cluster = 's426-' + digest(args.attempt_id.encode())[:12]
        self.network = self.cluster + '-internal'
        self.node = self.cluster + '-control-plane'
        self.kubeconfig = attempt.directory / 'fixture-kubeconfig'
        self.sequence = 0
        self.created = False
        self.network_created = False
        self.image_tags = []
        self.last_step = 'not-started'
        self.fixture_volumes = []

    def run(self, name, argv, input=None, timeout=120, allowed=(0,)):
        self.last_step = name
        self.sequence += 1
        r = subprocess.run(argv, input=input, capture_output=True, timeout=timeout)
        self.attempt.encrypt(f'fixture-{self.sequence:03d}-{name}', canonical({
            'exitCode': r.returncode, 'stdoutBase64': __import__('base64').b64encode(r.stdout).decode(),
            'stderrBase64': __import__('base64').b64encode(r.stderr).decode()}), self.args.age, self.args.recipient)
        if r.returncode not in allowed:
            raise ValueError('fixture-command-failed-' + name)
        return r

    def kube(self, name, *argv, obj=None, allowed=(0,)):
        return self.run(name, ['docker', 'exec', '-i', self.node, 'kubectl', '--kubeconfig', '/etc/kubernetes/admin.conf',
                               '--context', self.cluster, '--request-timeout=20s', *argv],
                        canonical(obj) if obj is not None else None, allowed=allowed)

    def get(self, resource, name=None, namespace=None):
        argv = ['get', resource]
        if name:
            argv.append(name)
        else:
            argv.append('--all-namespaces')
        if namespace:
            argv += ['-n', namespace]
        argv += ['-o', 'json']
        return json.loads(self.kube('get', *argv).stdout)

    def create(self, obj):
        self.kube('create-synthetic', 'create', '--validate=strict', '-f', '-', obj=obj)

    def start(self):
        # Refuse existing names, and require an already retained local image by immutable Docker ID.
        image = json.loads(self.run('inspect-image', ['docker', 'image', 'inspect', self.args.node_image]).stdout)[0]
        if image['Id'] != self.args.node_image:
            raise ValueError('node-image-must-be-local-sha256-identity')
        inspect = subprocess.run(['docker', 'container', 'inspect', self.node], capture_output=True)
        if absence_state(inspect, 'container', self.node) != 'absent':
            raise ValueError('fixture-node-exists-or-absence-unverified')
        # Stock kind assumes a default gateway. Internal Docker networks deliberately
        # have none; use the container's own IP for its DNS rewrite without adding a route.
        original = self.run('read-public-entrypoint', ['docker', 'run', '--rm', '--network', 'none', '--entrypoint',
                             'cat', self.args.node_image, '/usr/local/bin/entrypoint']).stdout
        needle = b"docker_host_ip=$(ip -4 route show default | cut -d' ' -f3)"
        if original.count(needle) != 1:
            raise ValueError('unrecognized-kind-entrypoint')
        updated = original.replace(needle, needle + b'''\n    if [[ -z "${docker_host_ip}" ]]; then
      docker_host_ip=$(ip -4 -o addr show dev eth0 | awk '{print $4}' | cut -d/ -f1)
    fi''')
        from evidence import write_new
        write_new(self.attempt.directory / 'entrypoint-fenced', updated)
        base_tag, derived_tag = 'hexalith-s426-base:' + self.cluster, 'hexalith-s426-fenced:' + self.cluster
        self.run('tag-local-base', ['docker', 'tag', self.args.node_image, base_tag])
        self.image_tags.append(base_tag)
        write_new(self.attempt.directory / 'Dockerfile', (f'FROM {base_tag}\nCOPY entrypoint-fenced /usr/local/bin/entrypoint\n'
                  'RUN chmod 755 /usr/local/bin/entrypoint\n').encode())
        self.image_tags.append(derived_tag)
        self.run('build-fenced-image', ['docker', 'build', '--network', 'none', '--pull=false', '-t', derived_tag,
                 str(self.attempt.directory)], timeout=180)
        derived = json.loads(self.run('inspect-fenced-image', ['docker', 'image', 'inspect', derived_tag]).stdout)[0]['Id']
        self.attempt.record('fixture-image.json', {'baseImage': self.args.node_image, 'derivedImage': derived,
                   'originalEntrypointSha256': digest(original), 'fencedEntrypointSha256': digest(updated),
                   'change': 'internal-network DNS rewrite uses node IP; no default route or external network added'})
        self.run('create-internal-network', ['docker', 'network', 'create', '--internal', self.network])
        self.network_created = True
        env = dict(os.environ, KIND_EXPERIMENTAL_DOCKER_NETWORK=self.network)
        # KIND never uses or modifies the source kubeconfig; all credentials here are fresh.
        config = {'kind': 'Cluster', 'apiVersion': 'kind.x-k8s.io/v1alpha4',
                  'networking': {'apiServerAddress': '127.0.0.1', 'serviceSubnet': '10.96.0.0/16'},
                  'nodes': [{'role': 'control-plane'}]}
        self.created = True  # permit exact cleanup even when create returns partially failed
        self.last_step = 'kind-create'
        r = subprocess.run(['kind', 'create', 'cluster', '--name', self.cluster, '--image', derived_tag,
                            '--kubeconfig', str(self.kubeconfig), '--config', '-', '--wait', '90s', '--retain'],
                           input=canonical(config), capture_output=True, timeout=180, env=env)
        self.attempt.encrypt('kind-create', canonical({'exitCode': r.returncode,
                             'stdoutBase64': __import__('base64').b64encode(r.stdout).decode(),
                             'stderrBase64': __import__('base64').b64encode(r.stderr).decode()}), self.args.age, self.args.recipient)
        internal_port_failure = (b'Ready after' in r.stderr and b'failed to get api server port' in r.stderr)
        if r.returncode and not internal_port_failure:
            raise ValueError('fenced-kind-create-failed')
        # kubeadm admin.conf inside kind uses kubernetes-admin@kubernetes, not the host kind context.
        internal = json.loads(self.run('fixture-context', ['docker', 'exec', self.node, 'kubectl', '--kubeconfig',
                             '/etc/kubernetes/admin.conf', 'config', 'view', '-o', 'json']).stdout)
        self.internal_context = internal['current-context']
        # Replace only the newly created fixture's context name to make every native command explicit.
        self.run('fixture-context-alias', ['docker', 'exec', self.node, 'kubectl', '--kubeconfig',
                 '/etc/kubernetes/admin.conf', 'config', 'rename-context', self.internal_context, self.cluster])
        native = self.get('namespace', 'kube-system')
        network = json.loads(self.run('inspect-network', ['docker', 'network', 'inspect', self.network]).stdout)[0]
        # Ensure no second externally reachable Docker network was attached.
        container = json.loads(self.run('inspect-fixture-node', ['docker', 'inspect', self.node]).stdout)[0]
        self.fixture_volumes = [v['Name'] for v in container.get('Mounts', []) if v.get('Type') == 'volume']
        attached = set(container['NetworkSettings']['Networks'])
        if attached != {self.network}:
            raise ValueError('fixture-has-unfenced-network')
        # API-server webhook calls need an initial route before kube-proxy can
        # translate Service IPs. This route is only for the new fixture's explicit
        # service CIDR; it adds neither a default route nor any source route.
        self.run('fixture-service-cidr-route', ['docker', 'exec', self.node, 'ip', 'route', 'add',
                 '10.96.0.0/16', 'dev', 'eth0'])
        probes = []
        source_host = urllib.parse.urlparse(self.args.source['nativeEndpoint']).hostname
        ipaddress.ip_address(source_host)  # no interpolated hostnames or shell metacharacters
        for address, port in [(source_host, 22), (source_host, 2379), (source_host, 2380), (source_host, 6443),
                              (source_host, 443), ('1.1.1.1', 443)]:
            r = self.run('fence-probe', ['docker', 'exec', self.node, 'bash', '-c',
                f'if timeout 3 bash -c "exec 3<>/dev/tcp/{address}/{port}"; then printf "reachable\\n"; '
                'else code=$?; if [ "$code" = 1 ] || [ "$code" = 124 ]; then printf "blocked\\n"; else exit 2; fi; fi'])
            probes.append({'port': port, 'source': address == source_host, 'blocked': probe_blocked(r)})
        identity = {'endpoint': 'https://127.0.0.1:6443', 'transport': 'docker-exec into new fixture only; no host API published',
                    'clusterUid': native['metadata']['uid'],
                    'sourceCredentialsImported': False, 'sourceDataImported': False}
        validate_isolation(self.args.source, identity, network['Internal'], probes)
        self.attempt.record('isolation.json', {'fixture': identity, 'internalNetwork': network['Internal'],
                    'serviceCidrRoute': '10.96.0.0/16 dev eth0, fixture only; no default/source route',
                    'probes': probes, 'sourceUidDifferent': True, 'passed': True,
                    'nodeImage': self.args.node_image, 'nativeVersion': json.loads(self.kube('fixture-version', 'version', '-o', 'json').stdout)['serverVersion']['gitVersion']})

    def install_kubesphere(self):
        import secrets
        import tarfile
        import yaml
        if file_digest(self.args.ks_chart) != self.args.ks_chart_sha256:
            raise ValueError('kubesphere-chart-digest-mismatch')
        with tarfile.open(self.args.ks_chart) as chart:
            meta = yaml.safe_load(chart.extractfile('ks-core/Chart.yaml').read())
            if meta['version'] != '1.2.4' or meta['appVersion'].lstrip('v') != '4.2.1':
                raise ValueError('unqualified-kubesphere-chart-version')
            scripts = {name: chart.extractfile(name).read() for name in (
                'ks-core/scripts/post-delete.sh', 'ks-core/charts/ks-crds/scripts/post-delete.sh')}
        self.attempt.record('kubesphere-uninstall-hooks.json', {'chartVersion': '1.2.4', 'appVersion': '4.2.1',
                   'chartSha256': self.args.ks_chart_sha256,
                   'hookScriptSha256': {k: digest(v) for k, v in scripts.items()},
                   'vendorHooksAccepted': False, 'qualifiedProcedure': 'Helm uninstall --no-hooks; retained CRDs require separate exact decisions',
                   'reason': 'Vendor cleanup includes broad namespace/CRD/finalizer effects; no wildcard retirement scope accepted'})
        self.run('copy-fixture-helm', ['docker', 'cp', str(self.args.helm), self.node + ':/usr/local/bin/helm426'])
        self.run('copy-public-chart', ['docker', 'cp', str(self.args.ks_chart), self.node + ':/ks-core-1.2.4.tgz'])
        values = {'authentication': {'adminPassword': secrets.token_urlsafe(32)}, 'telemetry': {'enabled': False},
                  'application': {'repository': {'builtinRepo': {'enabled': False}}},
                  'global': {'imageRegistry': 'registry.cn-beijing.aliyuncs.com'}}
        rendered = self.run('render-fixture-chart', ['docker', 'exec', '-i', self.node, '/usr/local/bin/helm426',
                           'template', 'ks-core', '/ks-core-1.2.4.tgz', '-n', 'kubesphere-system', '-f', '-'], input=canonical(values)).stdout
        objects = [v for v in yaml.safe_load_all(rendered) if v]
        images = set()
        def walk(value):
            if isinstance(value, dict):
                for k, v in value.items():
                    if k == 'image' and isinstance(v, str):
                        images.add(v)
                    walk(v)
            elif isinstance(value, list):
                for v in value:
                    walk(v)
        walk(objects)
        # The actual installed console extension is created dynamically by the
        # core controller's InstallPlan and is absent from helm template's Pods.
        images.add('registry.cn-beijing.aliyuncs.com/kse/ks-console-embed:v4.2.1')
        image_records = []
        for image in sorted(images):
            details = json.loads(self.run('local-vendor-image', ['docker', 'image', 'inspect', image]).stdout)[0]
            image_records.append({'tag': image, 'dockerId': details['Id'], 'repoDigests': details.get('RepoDigests', [])})
            # Docker's content store may retain a multi-platform index but only
            # amd64 layers. Kind's --all-platforms importer then requests absent
            # arm64 content; select amd64 explicitly on both sides of this stream.
            self.last_step = 'load-vendor-image'
            source = subprocess.Popen(['docker', 'image', 'save', '--platform=linux/amd64', image],
                                      stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            try:
                imported = subprocess.run(['docker', 'exec', '-i', self.node, 'ctr', '--namespace=k8s.io',
                    'images', 'import', '--platform=linux/amd64', '--digests', '-'], stdin=source.stdout,
                    capture_output=True, timeout=180)
                source.stdout.close()
                source_error = source.stderr.read()
                source_code = source.wait(timeout=30)
                self.attempt.encrypt('vendor-import-' + digest(image.encode())[:12], canonical({
                    'saveExit': source_code, 'importExit': imported.returncode,
                    'diagnosticBase64': __import__('base64').b64encode(source_error + imported.stdout + imported.stderr).decode()}),
                    self.args.age, self.args.recipient)
                if source_code or imported.returncode:
                    raise ValueError('vendor-amd64-import-failed')
            finally:
                if source.poll() is None:
                    source.kill()
                source.wait()
        self.attempt.record('kubesphere-fixture-inputs.json', {'images': image_records, 'chartSha256': self.args.ks_chart_sha256,
                'sourceValuesImported': False, 'freshCredentialGenerated': True, 'telemetryDisabled': True,
                'externalApplicationRepositoryDisabled': True, 'configurationDifferencesRequireReview': True})
        self.run('install-kubesphere', ['docker', 'exec', '-i', self.node, '/usr/local/bin/helm426', '--kubeconfig',
                    '/etc/kubernetes/admin.conf', '--kube-context', self.cluster, 'install', 'ks-core', '/ks-core-1.2.4.tgz',
                    '--namespace', 'kubesphere-system', '--create-namespace', '--timeout', '120s', '-f', '-'],
                    input=canonical(values), timeout=180)
        ready, limited = [], []
        for name in ('ks-apiserver', 'ks-controller-manager', 'ks-console', 'extensions-museum', 'ks-console-embed'):
            result = self.kube('kubesphere-runtime-ready', 'rollout', 'status', 'deployment/' + name, '-n', 'kubesphere-system',
                              '--timeout=30s', allowed=(0, 1))
            (ready if result.returncode == 0 else limited).append(name)
            if result.returncode and name in ('ks-apiserver', 'ks-controller-manager'):
                raise ValueError('core-controller-or-api-not-ready')
        if limited:
            self.diagnostics()
        installed_plan = self.get('installplans.kubesphere.io', 'ks-console-embed')
        extension_installed = installed_plan.get('status', {}).get('state') == 'Installed'
        self.attempt.record('kubesphere-runtime.json', {'deployedCore': 'ks-core1.2.4/app4.2.1', 'readyDeployments': ready,
                    'unreadyDeployments': limited, 'fullRuntimeAccepted': False,
                    'nativeCredentialsOnly': True, 'licensedApplicationWritesUsed': False,
                    'sourceConsoleEmbedInstallPlanReproduced': extension_installed})

    def kubesphere_inventory(self):
        # Native names alone omit chart-owned KubeSphere ServiceAccounts/IAM/tenant
        # instances. Include all served KubeSphere CRD resources before and after.
        resources = ('namespaces,persistentvolumes,persistentvolumeclaims,deployments.apps,replicasets.apps,pods,services,'
                     'configmaps,secrets,serviceaccounts,roles.rbac.authorization.k8s.io,rolebindings.rbac.authorization.k8s.io,'
                     'clusterroles.rbac.authorization.k8s.io,clusterrolebindings.rbac.authorization.k8s.io,'
                     'validatingwebhookconfigurations.admissionregistration.k8s.io,mutatingwebhookconfigurations.admissionregistration.k8s.io')
        crds = self.get('customresourcedefinitions.apiextensions.k8s.io')['items']
        custom = sorted({v['metadata']['name'] for v in crds
                         if v.get('spec', {}).get('group', '').endswith('kubesphere.io')
                         and any(version.get('served') for version in v['spec'].get('versions', []))})
        return [project_resource(v) for v in self.get(','.join([resources, *custom]))['items']]

    def uninstall_kubesphere(self):
        # Snapshot exact chart identities/descendants before a native Helm uninstall.
        # Hooks are disabled because their broad cleanup is outside an exact reviewed scope.
        before = self.kubesphere_inventory()
        self.attempt.record('kubesphere-before-state.json', {'resources': before, 'scope': 'fixture-only'})
        retired = {v['uid'] for v in before if v.get('helmRelease', {}).get('release-name') in ('ks-core', 'ks-console-embed')
                   and v['kind'] not in PROTECTED_KINDS}
        # Helm release history is also a named record in its exact retirement scope.
        retired |= {v['uid'] for v in before if v['kind'] == 'Secret' and v['namespace'] == 'kubesphere-system'
                    and (v['name'].startswith('sh.helm.release.v1.ks-core.') or v['name'].startswith('sh.helm.release.v1.ks-console-embed.'))}
        while True:
            expanded = retired | {v['uid'] for v in before if any(o.get('uid') in retired for o in v['owners'])}
            if expanded == retired:
                break
            retired = expanded
        actions = [{**{k: v[k] for k in ('apiVersion', 'kind', 'namespace', 'name', 'uid', 'resourceVersion')},
                    'action': 'delete', 'propagation': 'Foreground'} for v in before if v['uid'] in retired]
        validate_allowlist(before, actions)
        allowlist = {'actions': actions, 'chartSha256': self.args.ks_chart_sha256, 'sourceInventorySha256': self.args.source_digest,
                    'procedure': 'native Helm3 uninstall ks-core --no-hooks; no namespace/CRD wildcard/finalizer edits',
                    'scope': 'fixture-only; Helm observes UIDs immediately before command, no atomic multiobject preconditions',
                    'productionAccepted': False}
        self.attempt.record('kubesphere-retirement-allowlist.json', allowlist)
        # Complete the installed extension's native controller/finalizer lifecycle
        # while its controller still exists, before removing core reconcilers.
        plan = self.get('installplans.kubesphere.io', 'ks-console-embed')
        original = next(v for v in before if v['kind'] == 'InstallPlan' and v['name'] == 'ks-console-embed')
        if plan['metadata']['uid'] != original['uid']:
            raise ValueError('installed-plan-uid-drift')
        phase = {'apiVersion': plan['apiVersion'], 'kind': plan['kind'], 'name': plan['metadata']['name'],
                 'uid': plan['metadata']['uid'], 'resourceVersion': plan['metadata']['resourceVersion'],
                 'namespace': None, 'action': 'delete', 'propagation': 'Foreground'}
        self.attempt.record('installed-extension-delete-phase.json', {'action': phase,
                    'allowlistSha256': digest(canonical(allowlist)), 'controllerStillRunning': True})
        self.kube('native-extension-retirement', 'delete', '--raw', '/apis/kubesphere.io/v1alpha1/installplans/ks-console-embed',
                  '-f', '-', obj={'apiVersion': 'v1', 'kind': 'DeleteOptions', 'propagationPolicy': 'Foreground',
                  'preconditions': {'uid': phase['uid'], 'resourceVersion': phase['resourceVersion']}})
        self.kube('installed-extension-absent', 'wait', 'installplans.kubesphere.io/ks-console-embed', '--for=delete', '--timeout=90s')
        result = self.run('native-kubesphere-uninstall', ['docker', 'exec', self.node, '/usr/local/bin/helm426', '--kubeconfig',
                 '/etc/kubernetes/admin.conf', '--kube-context', self.cluster, 'uninstall', 'ks-core', '-n', 'kubesphere-system',
                 '--no-hooks', '--wait', '--timeout', '90s'], timeout=120, allowed=(0, 1))
        after = self.kubesphere_inventory()
        self.attempt.record('kubesphere-uninstall-observation.json', {'helmExitCode': result.returncode,
                    'resources': after, 'remainingAllowlisted': [v for v in after if v['uid'] in retired],
                    'productionAccepted': False, 'noFinalizersStripped': True})
        if result.returncode:
            self.last_step = 'native-kubesphere-uninstall'
            raise ValueError('native-kubesphere-uninstall-incomplete')
        # Assert exact expected set only in the manager namespace and protected synthetic state.
        relevant = lambda v: (v['namespace'] in ('kubesphere-system', 's426-workload') or v['uid'] in retired
                               or v['kind'] in PROTECTED_KINDS)
        assert_preserved([v for v in before if relevant(v)], [v for v in after if relevant(v)],
                         {key(v) for v in before if v['uid'] in retired})
        self.attempt.record('kubesphere-uninstall-result.json', {'state': 'passed-core-native-uninstall',
                    'allowlistSha256': digest(canonical(allowlist)), 'licensedApplicationWritesUsed': False,
                    'namespacePvcPvIdentitiesAndBindingsPreserved': True, 'vendorPostDeleteHooksExecuted': False,
                    'remainingCrdsAndInstances': 'retained; separate owner/consumer/propagation decisions required',
                    'sourceInstalledConsoleEmbedExtensionRehearsed': True, 'productionAccepted': False})

    def inventory(self):
        items = []
        for resource in ('namespaces', 'persistentvolumes', 'persistentvolumeclaims', 'configmaps', 'deployments.apps',
                         'customresourcedefinitions.apiextensions.k8s.io', 'workspaces.qualification.hexalith.io'):
            result = self.get(resource)
            for obj in result['items']:
                if ((obj['metadata'].get('namespace') or '').startswith('s426-')
                        or obj['metadata']['name'].startswith('s426-')
                        or obj['metadata']['name'] == 'workspaces.qualification.hexalith.io'):
                    items.append(project_resource(obj))
        return items

    def populate(self):
        for namespace in ('s426-management', 's426-workload'):
            self.create({'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': namespace}})
        self.create({'apiVersion': 'apiextensions.k8s.io/v1', 'kind': 'CustomResourceDefinition',
                     'metadata': {'name': 'workspaces.qualification.hexalith.io'},
                     'spec': {'group': 'qualification.hexalith.io', 'names': {'plural': 'workspaces', 'singular': 'workspace', 'kind': 'Workspace'},
                              'scope': 'Cluster', 'versions': [{'name': 'v1', 'served': True, 'storage': True,
                               'schema': {'openAPIV3Schema': {'type': 'object', 'properties': {'spec': {'type': 'object'}}}}}]}})
        self.kube('crd-established', 'wait', 'crd/workspaces.qualification.hexalith.io', '--for=condition=Established', '--timeout=20s')
        self.create({'apiVersion': 'qualification.hexalith.io/v1', 'kind': 'Workspace',
                     'metadata': {'name': 's426-owner', 'finalizers': ['qualification.hexalith.io/hold']}, 'spec': {}})
        owner = self.get('workspaces.qualification.hexalith.io', 's426-owner')
        self.create({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 's426-managed', 'namespace': 's426-management',
            'ownerReferences': [{'apiVersion': owner['apiVersion'], 'kind': owner['kind'], 'name': owner['metadata']['name'],
                                 'uid': owner['metadata']['uid'], 'controller': True, 'blockOwnerDeletion': False}]},
            'data': {'synthetic': 'no-production-configuration'}})
        self.create({'apiVersion': 'apps/v1', 'kind': 'Deployment', 'metadata': {'name': 's426-preserve', 'namespace': 's426-workload'},
                     'spec': {'replicas': 0, 'selector': {'matchLabels': {'app': 's426-preserve'}},
                              'template': {'metadata': {'labels': {'app': 's426-preserve'}}, 'spec': {'containers': [
                              {'name': 'synthetic', 'image': 'registry.k8s.io/pause:3.10', 'imagePullPolicy': 'Never'}]}}}})
        self.create({'apiVersion': 'v1', 'kind': 'PersistentVolume', 'metadata': {'name': 's426-data'},
                     'spec': {'capacity': {'storage': '1Mi'}, 'accessModes': ['ReadWriteOnce'], 'persistentVolumeReclaimPolicy': 'Retain',
                              'storageClassName': '', 'hostPath': {'path': '/var/local/s426-synthetic'}}})
        self.create({'apiVersion': 'v1', 'kind': 'PersistentVolumeClaim', 'metadata': {'name': 's426-data', 'namespace': 's426-workload'},
                     'spec': {'accessModes': ['ReadWriteOnce'], 'storageClassName': '', 'volumeName': 's426-data',
                              'resources': {'requests': {'storage': '1Mi'}}}})
        self.kube('claim-bound', 'wait', 'pvc/s426-data', '-n', 's426-workload', '--for=jsonpath={.status.phase}=Bound', '--timeout=30s')
        self.run('synthetic-data', ['docker', 'exec', self.node, 'bash', '-c',
                 'mkdir -p /var/local/s426-synthetic && printf synthetic-426-canary > /var/local/s426-synthetic/canary'])

    def delete(self, action, path):
        current = self.inventory()
        # Revalidate this individual action immediately before the native delete; previously deleted dependents no longer participate.
        validate_allowlist(current, [action])
        self.kube('uid-bound-native-delete', 'delete', '--raw', path, '-f', '-', obj={'apiVersion': 'v1', 'kind': 'DeleteOptions',
            'propagationPolicy': action['propagation'], 'preconditions': {'uid': action['uid'], 'resourceVersion': action['resourceVersion']}})

    def execute(self):
        if self.args.ks_chart:
            self.install_kubesphere()
        self.populate()
        before = self.inventory()
        owner = next(v for v in before if v['kind'] == 'Workspace')
        child = next(v for v in before if v['name'] == 's426-managed')
        actions = [{**{k: v[k] for k in ('apiVersion', 'kind', 'namespace', 'name', 'uid', 'resourceVersion')},
                     'action': 'delete', 'propagation': 'Foreground', 'order': order}
                    for order, v in enumerate((child, owner), 1)]
        validate_allowlist(before, actions)
        plan = {'sourceInventorySha256': self.args.source_digest, 'scope': 'synthetic fixture only',
                'actions': actions, 'scriptSha256': self.args.script_digest, 'productionActions': []}
        self.attempt.record('fixture-allowlist.json', plan)
        self.delete(actions[0], '/api/v1/namespaces/s426-management/configmaps/s426-managed')
        self.kube('child-absent', 'wait', 'configmap/s426-managed', '-n', 's426-management', '--for=delete', '--timeout=20s')
        assert_preserved(before, self.inventory(), {key(child)})
        # Resource versions may change; capture an append-only phase bound to the original UID before each action.
        current_owner = next(v for v in self.inventory() if v['kind'] == 'Workspace')
        phase = {**actions[1], 'resourceVersion': current_owner['resourceVersion']}
        self.attempt.record('owner-delete-phase.json', {'action': phase, 'originalUidUnchanged': phase['uid'] == owner['uid']})
        self.delete(phase, '/apis/qualification.hexalith.io/v1/workspaces/s426-owner')
        blocked = self.get('workspaces.qualification.hexalith.io', 's426-owner')
        if not blocked['metadata'].get('deletionTimestamp') or blocked['metadata'].get('finalizers') != ['qualification.hexalith.io/hold']:
            raise ValueError('finalizer-behavior-unexpected')
        named = {**project_resource(blocked), 'action': 'remove-named-finalizer', 'propagation': 'Foreground',
                 'finalizer': 'qualification.hexalith.io/hold'}
        validate_allowlist(self.inventory(), [named])
        self.attempt.record('named-finalizer-phase.json', {'action': named, 'scope': 'fixture-only synthetic finalizer'})
        # A specific UID/resourceVersion-bound replace; never blanket stripping of a production finalizer.
        blocked['metadata']['finalizers'] = []
        self.kube('remove-synthetic-finalizer', 'replace', '-f', '-', obj=blocked)
        self.kube('owner-absent', 'wait', 'workspaces.qualification.hexalith.io/s426-owner', '--for=delete', '--timeout=20s')
        after = self.inventory()
        assert_preserved(before, after, {key(child), key(owner)})
        canary = self.run('canary-readback', ['docker', 'exec', self.node, 'cat', '/var/local/s426-synthetic/canary']).stdout
        if canary != b'synthetic-426-canary':
            raise ValueError('synthetic-data-changed')
        self.attempt.record('native-result.json', {'state': 'passed-limited-synthetic-native-rehearsal',
            'before': before, 'after': after, 'allowlistSha256': digest(canonical(plan)),
            'observedNamedFinalizerBlock': True, 'protectedIdentitiesAndBindingsPreserved': True,
            'syntheticCanarySha256': digest(canary), 'licensedKubeSphereWritesUsed': False,
            'stopRecovery': 'stop deletions; retain failed fixture encrypted diagnostics; rebuild only a fresh synthetic target',
            'limitations': [('Actual core runtime/uninstall outcome is recorded separately; broad vendor cleanup hooks remain disabled'
                            if self.args.ks_chart else 'Does not install ks-core or run its real controllers/webhooks/finalizers/uninstall hooks'),
                           'Fixture Kubernetes patch/CNI/runtime differ from source; no source workload/data restore',
                           'Explicit child-first native deletion exercises scope and finalizer preconditions; controller cascade still unaccepted',
                           'Synthetic deployment stays at zero; no production application health claim'],
            'productionRetirementAccepted': False, 'mutationAuthorized': False})
        if self.args.ks_chart:
            self.uninstall_kubesphere()

    def cleanup(self):
        outcomes = {}
        if self.created:
            r = subprocess.run(['kind', 'delete', 'cluster', '--name', self.cluster, '--kubeconfig', str(self.kubeconfig)],
                               capture_output=True, timeout=120)
            outcomes['kindDeleteExit'] = r.returncode
        if self.kubeconfig.exists():
            self.kubeconfig.unlink()
        if self.network_created:
            r = subprocess.run(['docker', 'network', 'rm', self.network], capture_output=True, timeout=30)
            outcomes['networkDeleteExit'] = r.returncode
        inspections = {'node': absence_state(subprocess.run(['docker', 'inspect', self.node], capture_output=True), 'container', self.node),
                       'network': absence_state(subprocess.run(['docker', 'network', 'inspect', self.network], capture_output=True), 'network', self.network)}
        outcomes['inspectionStates'] = inspections
        outcomes['nodeAbsent'] = inspections['node'] == 'absent'
        outcomes['networkAbsent'] = inspections['network'] == 'absent'
        outcomes['freshCredentialFileAbsent'] = not self.kubeconfig.exists()
        outcomes['volumeInspectionStates'] = {volume: absence_state(subprocess.run(['docker', 'volume', 'inspect', volume],
                         capture_output=True), 'volume', volume) for volume in self.fixture_volumes}
        outcomes['fixtureVolumesAbsent'] = all(state == 'absent' for state in outcomes['volumeInspectionStates'].values())
        image_cleanup = []
        for tag in reversed(self.image_tags):
            r = subprocess.run(['docker', 'image', 'rm', tag], capture_output=True, timeout=60)
            image_cleanup.append({'tag': tag, 'exitCode': r.returncode})
        outcomes['fixtureImageTagsRemoved'] = image_cleanup
        outcomes['globalPruneUsed'] = False
        outcomes['unrelatedDockerObjectsChanged'] = False
        outcomes['passed'] = (outcomes['nodeAbsent'] and outcomes['networkAbsent'] and outcomes['freshCredentialFileAbsent']
                              and outcomes['fixtureVolumesAbsent']
                              and outcomes.get('kindDeleteExit', 0) == 0 and outcomes.get('networkDeleteExit', 0) == 0)
        self.attempt.record('cleanup.json', outcomes)
        return outcomes['passed']

    def diagnostics(self):
        if not self.created:
            return
        # Failure logs stay encrypted, including fresh fixture configuration.
        try:
            if self.args.ks_chart:
                failure_step = self.last_step
                self.attempt.record('kubesphere-diagnostic-state-' + str(self.sequence) + '.json', {'failedStep': failure_step,
                            'resources': self.kubesphere_inventory(), 'productionAccepted': False,
                            'noFinalizersStripped': True})
            self.kube('failed-fixture-pods', 'get', 'pods,replicasets.apps,events', '-n', 'kubesphere-system', '-o', 'json', allowed=(0, 1))
            self.kube('failed-console-log', 'logs', 'deployment/ks-console', '-n', 'kubesphere-system', '--all-containers', '--tail=80', allowed=(0, 1))
            self.kube('failed-controller-log', 'logs', 'deployment/ks-controller-manager', '-n', 'kubesphere-system', '--all-containers', '--tail=80', allowed=(0, 1))
        except (ValueError, OSError, subprocess.TimeoutExpired):
            pass


def rehearse(args):
    args.script_digest = file_digest(Path(__file__))
    args.source_digest = file_digest(args.source_inventory)
    args.source = json.loads(args.source_inventory.read_text())
    if not args.source.get('sourceClusterUid') or not args.source.get('nativeEndpoint'):
        raise ValueError('source-identity-missing')
    attempt = Attempt(args.project_root, args.evidence_root, args.attempt_id, 'rehearsal')
    attempt.record('attempt.json', {'attemptId': args.attempt_id, 'recordedAt': now(), 'operator': args.operator,
                    'scriptSha256': args.script_digest,
                    'sourceInventorySha256': args.source_digest, 'scope': 'isolated synthetic native operations only',
                    'mutationAuthorized': False, 'signed': False, 'productionAccepted': False})
    fixture, state = Fixture(args, attempt), 'failed-closed'
    try:
        fixture.start()
        fixture.execute()
        state = 'passed-limited'
    except (ValueError, OSError, subprocess.TimeoutExpired) as error:
        failure_step = fixture.last_step
        fixture.diagnostics()
        attempt.record('failure.json', {'state': 'failed-closed', 'failureStep': failure_step,
                                     'reasonCode': (str(error) if isinstance(error, ValueError)
                                                    and re.fullmatch(r'[a-z0-9-]+', str(error)) else 'command-or-custody-error'),
                                     'reason': 'isolation, fixture command, drift or preservation failed',
                                     'diagnostics': 'encrypted exports; source unchanged', 'productionAccepted': False})
    finally:
        try:
            if not fixture.cleanup():
                state = 'failed-cleanup'
        except (ValueError, OSError, subprocess.TimeoutExpired):
            state = 'failed-cleanup'
        attempt.record('summary.json', {'state': state, 'productionRetirementAccepted': False,
                       'qualificationAccepted': False, 'licensedKubeSphereWritesUsed': False})
        attempt.finish()
    return attempt.directory, state


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--project-root', type=Path, default=Path(__file__).resolve().parents[2])
    p.add_argument('--evidence-root', type=Path, default=Path.home() / 'hexalith-management-evidence')
    p.add_argument('--attempt-id', default=datetime.now(timezone.utc).strftime('%Y%m%dt%H%M%Sz-rehearsal'))
    p.add_argument('--operator', required=True)
    p.add_argument('--source-inventory', type=Path, required=True, help='Sanitized census only; no source kubeconfig is accepted')
    p.add_argument('--node-image', required=True, help='Existing local Docker sha256:<ID>; no unqualified pulls')
    p.add_argument('--kubectl', type=Path, required=True, help='Local tool used only with fresh fixture kubeconfig')
    p.add_argument('--age', type=Path, required=True)
    p.add_argument('--recipient', required=True)
    p.add_argument('--ks-chart', type=Path, help='Public retained ks-core1.2.4 archive; enables real core uninstall fixture')
    p.add_argument('--ks-chart-sha256', help='Independently checked exact public chart SHA-256')
    p.add_argument('--helm', type=Path, help='Retained fixture Helm3 binary, required with --ks-chart')
    args = p.parse_args()
    if not re.fullmatch(r'sha256:[0-9a-f]{64}', args.node_image):
        p.exit(2, 'Rehearsal refused: exact local Docker image ID required.\n')
    if args.ks_chart and (not args.helm or not args.ks_chart_sha256
            or not re.fullmatch(r'[0-9a-f]{64}', args.ks_chart_sha256)):
        p.exit(2, 'Rehearsal refused: actual core fixture requires chart SHA-256 and retained Helm binary.\n')
    try:
        directory, state = rehearse(args)
    except (ValueError, OSError):
        p.exit(2, 'Rehearsal refused: invalid source identity, tools or private custody.\n')
    print(f'Private fixture evidence: {directory}\nRehearsal: {state}; production retirement remains unaccepted')
    if state.startswith('failed'):
        raise SystemExit(2)


if __name__ == '__main__':
    main()
