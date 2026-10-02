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
import time
import urllib.parse

from evidence import Attempt, canonical, digest, file_digest, now
from qualify import PROTECTED_KINDS, management_resource, project_resource


def key(obj):
    return (obj['apiVersion'], obj['kind'], obj.get('namespace'), obj['name'])


def native_delete_options(action):
    """Every native deletion carries both observed object preconditions."""
    if (action.get('propagation') not in ('Foreground', 'Orphan')
            or any(not isinstance(action.get(k), str) or not action[k] for k in ('uid', 'resourceVersion'))):
        raise ValueError('missing-native-delete-preconditions')
    return {'apiVersion': 'v1', 'kind': 'DeleteOptions', 'propagationPolicy': action['propagation'],
            'preconditions': {'uid': action['uid'], 'resourceVersion': action['resourceVersion']}}


def native_conflict(result):
    """Require Kubernetes Conflict evidence, not a transport or command failure."""
    if result.returncode != 1:
        return False
    for payload in (result.stdout, result.stderr):
        try:
            status = json.loads(payload)
            if (isinstance(status, dict) and status.get('kind') == 'Status'
                    and status.get('reason') == 'Conflict' and status.get('code') == 409):
                return True
        except (ValueError, TypeError):
            pass
    lines = result.stderr.decode(errors='replace').strip().splitlines()
    return len(lines) == 1 and lines[0].startswith('Error from server (Conflict): ')


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
        'image': {f'Error response from daemon: No such image: {name}', f'Error: No such image: {name}',
                  f'Error: No such object: {name}'},
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
        if any(owner.get('uid') not in by_uid for owner in current['owners']):
            raise ValueError('owner-outside-censused-inventory')
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
    orphaned = {a['uid'] for a in actions if a['action'] == 'delete' and a['propagation'] == 'Orphan'}
    closure, cascading = set(deleted), deleted - orphaned
    while True:
        descendants = {o['uid'] for o in inventory if any(v.get('uid') in cascading for v in o['owners'])}
        expanded = closure | descendants
        if expanded == closure:
            break
        closure = expanded
        cascading |= descendants - orphaned
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
        protected = ('owners', 'finalizers', 'namespaceFinalizers', 'managementLabels', 'storageClass', 'reclaimPolicy',
                     'storagePropertiesSha256', 'volumeMode', 'accessModes', 'provisioner', 'volumeBindingMode')
        fields = ('uid', 'binding', 'deletionTimestamp') + (protected if initial[k]['kind'] in PROTECTED_KINDS else ())
        if any(initial[k].get(field) != current[k].get(field) for field in fields):
            raise ValueError('preserved-identity-or-binding-changed')


RELEASES = ('ks-core', 'ks-console-embed')
SYSTEM_WORKSPACE_FINALIZER = 'kubesphere.io/cascading-deletion'
MANAGER_NAMESPACES = ('kubesphere-system', 'kubesphere-controls-system')
NAME = re.compile(r'[A-Za-z0-9][A-Za-z0-9.:@_-]{0,252}')


def descendants(inventory, roots):
    """Owner-reference closure, including the roots themselves."""
    expected = set(roots)
    while True:
        expanded = expected | {v['uid'] for v in inventory if any(owner.get('uid') in expected for owner in v['owners'])}
        if expanded == expected:
            return expected
        expected = expanded


def with_endpoint_counterparts(inventory, uids):
    """The native endpoints controller removes a deleted Service's same-named Endpoints."""
    services = {(v['namespace'], v['name']) for v in inventory
                if v['uid'] in uids and (v['apiVersion'], v['kind']) == ('v1', 'Service')}
    return descendants(inventory, set(uids) | {v['uid'] for v in inventory if (v['apiVersion'], v['kind']) == ('v1', 'Endpoints')
                                               and (v['namespace'], v['name']) in services})


def release_record(v, releases=RELEASES):
    return (v['kind'] == 'Secret' and v['namespace'] == 'kubesphere-system'
            and any(v['name'].startswith(f'sh.helm.release.v1.{release}.') for release in releases))


def system_workspace(v):
    return (v['apiVersion'] == 'tenant.kubesphere.io/v1beta1' and v['kind'] in ('WorkspaceTemplate', 'Workspace')
            and v['name'] == 'system-workspace')


def extension_members(inventory, plan):
    """The installed extension controller retires its own Helm release objects and record."""
    members = {plan['uid']} | {v['uid'] for v in inventory if v.get('helmRelease', {}).get('release-name') == 'ks-console-embed'}
    members |= {v['uid'] for v in inventory if release_record(v, ('ks-console-embed',))}
    return with_endpoint_counterparts(inventory, descendants(inventory, members))


def extension_phase_actions(inventory, plan, retirement_actions):
    """Bound the installed extension's phase separately from later core removal."""
    expected = extension_members(inventory, plan)
    if expected - {v['uid'] for v in retirement_actions}:
        raise ValueError('extension-phase-outside-retirement-allowlist')
    actions = [{**{k: v[k] for k in ('apiVersion', 'kind', 'namespace', 'name', 'uid', 'resourceVersion')},
                'action': 'delete', 'propagation': 'Foreground'} for v in inventory if v['uid'] in expected]
    validate_allowlist(inventory, actions)
    return actions


def retirement_scope(inventory):
    """Exact release objects/records, the controller-created system Workspace and native counterparts."""
    scope = {v['uid'] for v in inventory if v['kind'] not in PROTECTED_KINDS
             and (v.get('helmRelease', {}).get('release-name') in RELEASES or release_record(v) or system_workspace(v))}
    return with_endpoint_counterparts(inventory, descendants(inventory, scope))


def _api(api, *kinds):
    return lambda v: v['apiVersion'] == api and v['kind'] in kinds


# Controllers still run through 'admission'; later phases cannot depend on a KubeSphere reconciler.
DEPENDENCY_PHASES = (
    ('installed-extension', _api('kubesphere.io/v1alpha1', 'InstallPlan')),
    ('global-role-bindings', _api('iam.kubesphere.io/v1beta1', 'GlobalRoleBinding')),
    ('workspace-role-bindings', _api('iam.kubesphere.io/v1beta1', 'WorkspaceRoleBinding')),
    ('workspace-roles', _api('iam.kubesphere.io/v1beta1', 'WorkspaceRole')),
    ('users', _api('iam.kubesphere.io/v1beta1', 'User')),
    ('global-roles', _api('iam.kubesphere.io/v1beta1', 'GlobalRole')),
    ('kubesphere-service-accounts', _api('kubesphere.io/v1alpha1', 'ServiceAccount')),
    ('catalog-extension', lambda v: _api('kubesphere.io/v1alpha1', 'Extension', 'ExtensionVersion')(v) and not v['owners']),
    ('extension-repository', _api('kubesphere.io/v1alpha1', 'Repository')),
    ('admission', lambda v: v['kind'] in ('ValidatingWebhookConfiguration', 'MutatingWebhookConfiguration')
                            or _api('apiregistration.k8s.io/v1', 'APIService')(v)),
    ('controllers-and-services', lambda v: (v['apiVersion'], v['kind']) in (('apps/v1', 'Deployment'), ('v1', 'Service'))),
    ('remaining-release-objects', lambda v: not release_record(v) and not system_workspace(v)),
    ('release-records', release_record),
    ('system-workspace-finalizers', system_workspace),
)
POST_CONTROLLER_PHASES = ('controllers-and-services', 'remaining-release-objects', 'release-records')


def dependency_plan(inventory, scope):
    """Partition the exact scope into ordered child-first phases before any request is sent."""
    by_uid = {v['uid']: v for v in inventory}
    if not set(scope) <= set(by_uid):
        raise ValueError('scope-outside-censused-inventory')
    assigned, phases = set(), []
    for order, (name, selector) in enumerate(DEPENDENCY_PHASES, 1):
        # Earlier child-first phases have already removed their members from the simulated state.
        remaining = [v for v in inventory if v['uid'] not in assigned]
        selected = {uid for uid in scope - assigned if selector(by_uid[uid])}
        if name == 'installed-extension':
            if len(selected) > 1:
                raise ValueError('ambiguous-installed-extension-plan')
            roots = selected
            expected = set().union(*(extension_members(remaining, by_uid[uid]) for uid in selected))
        else:
            roots = {uid for uid in selected if not any(o.get('uid') in selected for o in by_uid[uid]['owners'])}
            expected = with_endpoint_counterparts(remaining, descendants(remaining, roots))
        if not expected:
            continue
        if expected - scope:
            raise ValueError('phase-outside-retirement-allowlist')
        if expected & assigned:
            raise ValueError('retirement-member-in-multiple-phases')
        members = [by_uid[uid] for uid in expected]
        if name in POST_CONTROLLER_PHASES and any(v['finalizers'] for v in members):
            raise ValueError('finalizer-after-controller-removal')
        named = name == 'system-workspace-finalizers'
        if named and any(v['finalizers'] != [SYSTEM_WORKSPACE_FINALIZER] or v['owners'] or v['uid'] not in roots for v in members):
            raise ValueError('unreviewed-system-workspace-finalizer-scope')
        actions = [{**{k: v[k] for k in ('apiVersion', 'kind', 'namespace', 'name', 'uid', 'resourceVersion')},
                    'action': 'delete', 'propagation': 'Foreground'} for v in members]
        validate_allowlist(remaining, actions)
        assigned |= expected
        phases.append({'phase': name, 'order': order, 'controllersRunning': name not in POST_CONTROLLER_PHASES and not named,
                       'mode': 'named-finalizer' if named else ('controller-lifecycle' if name == 'installed-extension' else 'delete'),
                       'namedFinalizer': SYSTEM_WORKSPACE_FINALIZER if named else None,
                       'roots': sorted(roots, key=lambda uid: (by_uid[uid]['kind'], by_uid[uid]['namespace'] or '', by_uid[uid]['name'])),
                       'expected': sorted(expected)})
    if scope - assigned:
        raise ValueError('unphased-retirement-member')
    return phases


def label(k):
    return '/'.join(v or '-' for v in k)


def phase_delta(baseline_uids, before, after, expected_uids):
    """Explain a phase without exposing values; transients created during the attempt are reported separately."""
    tracked = lambda items: {key(v): v for v in items if v['uid'] in baseline_uids or v['kind'] in PROTECTED_KINDS}
    initial, current = tracked(before), tracked(after)
    expected = {k for k, v in initial.items() if v['uid'] in expected_uids}
    removed, prior = set(initial) - set(current), {v['uid'] for v in before}
    return {'expectedRemovals': sorted(map(label, expected)),
            'unexpectedRemovals': sorted(map(label, removed - expected)),
            'retainedExpected': sorted(map(label, expected - removed)),
            'recreated': sorted(label(key(v)) for v in after if key(v) in expected and v['uid'] not in expected_uids),
            'additions': sorted(label(key(v)) for v in after if v['uid'] not in prior),
            'transientRemovals': sorted(label(key(v)) for v in before if v['uid'] not in baseline_uids
                                        and v['kind'] not in PROTECTED_KINDS and v['uid'] not in {x['uid'] for x in after})}


def assert_phase(baseline_uids, before, after, expected_uids):
    """Exact baseline removals; attempt-created transients may disappear, retired keys may not reappear."""
    tracked = lambda items: [v for v in items if v['uid'] in baseline_uids or v['kind'] in PROTECTED_KINDS]
    initial = tracked(before)
    expected = {key(v) for v in initial if v['uid'] in expected_uids}
    if expected & {key(v) for v in after if v['uid'] not in expected_uids}:
        raise ValueError('controller-recreated-retired-object')
    assert_preserved(initial, tracked(after), expected)


def native_not_found(result):
    """Only a specific server NotFound response proves a native object is absent."""
    if result.returncode != 1:
        return False
    lines = result.stderr.decode(errors='replace').strip().splitlines()
    return len(lines) == 1 and lines[0].startswith('Error from server (NotFound): ')


def api_path(resources, obj):
    """Derive the exact native object path from current discovery, never from a guessed plural."""
    plural, namespaced = resources[(obj['apiVersion'], obj['kind'])]
    names = [obj['name']] + ([obj['namespace']] if namespaced else [])
    if any(not isinstance(v, str) or not NAME.fullmatch(v) for v in names) or bool(obj['namespace']) != namespaced:
        raise ValueError('unsafe-or-mismatched-native-path')
    prefix = '/api/v1' if obj['apiVersion'] == 'v1' else '/apis/' + obj['apiVersion']
    scope = f'/namespaces/{obj["namespace"]}' if namespaced else ''
    return f'{prefix}{scope}/{plural}/{urllib.parse.quote(obj["name"], safe=":@")}'


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
        self.volume_capture_verified = True  # no kind invocation and no owned volumes yet
        self.base_tag = 'hexalith-s426-base:' + self.cluster
        self.custom_resources = None
        self.derived_tag = 'hexalith-s426-fenced:' + self.cluster

    def capture_volumes(self):
        """Keep volume identities before encryption or teardown can fail."""
        self.volume_capture_verified = False
        try:
            result = subprocess.run(['docker', 'inspect', self.node], capture_output=True, timeout=20)
            if result.returncode:
                return False
            containers = json.loads(result.stdout)
            if (not isinstance(containers, list) or len(containers) != 1
                    or not isinstance(containers[0].get('Mounts'), list)):
                return False
            if any(not isinstance(v, dict) or not isinstance(v.get('Type'), str) or not v['Type']
                   for v in containers[0]['Mounts']):
                return False
            volumes = [v['Name'] for v in containers[0]['Mounts'] if v.get('Type') == 'volume']
            if any(not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]{0,255}', name) for name in volumes):
                return False
            self.fixture_volumes = sorted(set(self.fixture_volumes) | set(volumes))
            self.volume_capture_verified = True
            return True
        except (ValueError, TypeError, KeyError, AttributeError, OSError, subprocess.TimeoutExpired):
            return False

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
        for kind, name, command in [('container', self.node, ['docker', 'container', 'inspect', self.node]),
                ('network', self.network, ['docker', 'network', 'inspect', self.network]),
                ('image', self.base_tag, ['docker', 'image', 'inspect', self.base_tag]),
                ('image', self.derived_tag, ['docker', 'image', 'inspect', self.derived_tag])]:
            if absence_state(subprocess.run(command, capture_output=True, timeout=20), kind, name) != 'absent':
                raise ValueError('fixture-name-exists-or-absence-unverified')
        image = json.loads(self.run('inspect-image', ['docker', 'image', 'inspect', self.args.node_image]).stdout)[0]
        if image['Id'] != self.args.node_image:
            raise ValueError('node-image-must-be-local-sha256-identity')
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
        base_tag, derived_tag = self.base_tag, self.derived_tag
        self.image_tags.append(base_tag)
        self.run('tag-local-base', ['docker', 'tag', self.args.node_image, base_tag])
        write_new(self.attempt.directory / 'Dockerfile', (f'FROM {base_tag}\nCOPY entrypoint-fenced /usr/local/bin/entrypoint\n'
                  'RUN chmod 755 /usr/local/bin/entrypoint\n').encode())
        self.image_tags.append(derived_tag)
        self.run('build-fenced-image', ['docker', 'build', '--network', 'none', '--pull=false', '-t', derived_tag,
                 str(self.attempt.directory)], timeout=180)
        derived = json.loads(self.run('inspect-fenced-image', ['docker', 'image', 'inspect', derived_tag]).stdout)[0]['Id']
        self.attempt.record('fixture-image.json', {'baseImage': self.args.node_image, 'derivedImage': derived,
                   'originalEntrypointSha256': digest(original), 'fencedEntrypointSha256': digest(updated),
                   'change': 'internal-network DNS rewrite uses node IP; no default route or external network added'})
        self.network_created = True
        self.run('create-internal-network', ['docker', 'network', 'create', '--internal', self.network])
        env = dict(os.environ, KIND_EXPERIMENTAL_DOCKER_NETWORK=self.network)
        # KIND never uses or modifies the source kubeconfig; all credentials here are fresh.
        config = {'kind': 'Cluster', 'apiVersion': 'kind.x-k8s.io/v1alpha4',
                  'networking': {'apiServerAddress': '127.0.0.1', 'serviceSubnet': '10.96.0.0/16'},
                  'nodes': [{'role': 'control-plane'}]}
        self.created = True  # permit exact cleanup even when create returns partially failed
        self.volume_capture_verified = False
        self.last_step = 'kind-create'
        try:
            r = subprocess.run(['kind', 'create', 'cluster', '--name', self.cluster, '--image', derived_tag,
                            '--kubeconfig', str(self.kubeconfig), '--config', '-', '--wait', '90s', '--retain'],
                           input=canonical(config), capture_output=True, timeout=300, env=env)
        finally:
            self.capture_volumes()
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
        self.fixture_volumes = sorted(set(self.fixture_volumes) | {v['Name'] for v in container.get('Mounts', []) if v.get('Type') == 'volume'})
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
                   'vendorHooksAccepted': False, 'proposedProcedure': 'dependency-first native UID/resourceVersion-bound retirement; no Helm uninstall or hooks; CRDs retained for separate decisions',
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

    def resource_list(self):
        # Native names alone omit chart-owned KubeSphere ServiceAccounts/IAM/tenant
        # instances. Include all served KubeSphere CRD resources before and after;
        # CRDs are retained and compared separately, so their large schemas are read once per side.
        if self.custom_resources is None:
            self.custom_resources = sorted({v['name'] for v in self.crd_inventory()
                                            if (v['customResource']['group'] or '').endswith('kubesphere.io')
                                            and any(version['served'] for version in v['customResource']['versions'])})
        return ','.join([self.NATIVE_RESOURCES, *self.custom_resources])

    NATIVE_RESOURCES = ('namespaces,persistentvolumes,persistentvolumeclaims,storageclasses.storage.k8s.io,'
                        'deployments.apps,replicasets.apps,statefulsets.apps,daemonsets.apps,jobs.batch,pods,services,endpoints,'
                        'endpointslices.discovery.k8s.io,configmaps,secrets,serviceaccounts,roles.rbac.authorization.k8s.io,'
                        'rolebindings.rbac.authorization.k8s.io,clusterroles.rbac.authorization.k8s.io,'
                        'clusterrolebindings.rbac.authorization.k8s.io,apiservices.apiregistration.k8s.io,'
                        'validatingwebhookconfigurations.admissionregistration.k8s.io,mutatingwebhookconfigurations.admissionregistration.k8s.io')

    def crd_inventory(self):
        return [project_resource(v) for v in self.get('customresourcedefinitions.apiextensions.k8s.io')['items']]

    def kubesphere_inventory(self):
        return [project_resource(v) for v in self.get(self.resource_list())['items']]

    def uid_states(self):
        """Small UID/deletion-state polls; full projections are taken only once a phase settles."""
        out = self.kube('fixture-state-poll', 'get', self.resource_list(), '--all-namespaces', '-o',
                        'jsonpath={range .items[*]}{.metadata.uid}{" "}{.metadata.deletionTimestamp}{"\\n"}{end}').stdout
        return frozenset(line for line in out.decode().splitlines() if line.strip())

    def wait_absent(self, uids, timeout):
        deadline = time.monotonic() + timeout
        while True:
            present = {line.split()[0] for line in self.uid_states()}
            if not set(uids) & present:
                return True
            if time.monotonic() > deadline:
                return False
            time.sleep(5)

    def settled_inventory(self):
        """Delayed controller cascades must be visible before a phase is compared."""
        previous = self.uid_states()
        for _ in range(9):
            time.sleep(10)
            current = self.uid_states()
            if current == previous:
                return self.kubesphere_inventory(), True
            previous = current
        return self.kubesphere_inventory(), False

    def discover(self, api_versions):
        resources = {}
        for api_version in sorted(api_versions):
            path = '/api/v1' if api_version == 'v1' else '/apis/' + api_version
            listing = json.loads(self.kube('native-discovery', 'get', '--raw', path).stdout)
            for item in listing['resources']:
                if '/' not in item['name']:
                    resources[(api_version, item['kind'])] = (item['name'], item['namespaced'] is True)
        return resources

    def native_read(self, path):
        result = self.kube('native-read', 'get', '--raw', path, allowed=(0, 1))
        if result.returncode == 0:
            return json.loads(result.stdout)
        if native_not_found(result):
            return None
        raise ValueError('native-read-failed')

    def native_retire(self, phase, path, uid):
        """Fresh UID-bound read, then a DELETE carrying the original UID and current resourceVersion."""
        for retry in range(3):
            current = self.native_read(path)
            if current is None:
                return {'outcome': 'absent-before-request', 'conflictRetries': retry}
            if current['metadata']['uid'] != uid:
                raise ValueError('uid-drift-before-native-delete')
            version = current['metadata']['resourceVersion']
            result = self.kube('native-retire-' + phase, 'delete', '--raw', path, '-f', '-', allowed=(0, 1),
                               obj=native_delete_options({'uid': uid, 'resourceVersion': version, 'propagation': 'Foreground'}))
            if result.returncode == 0:
                return {'outcome': 'native-delete-accepted', 'resourceVersion': version, 'conflictRetries': retry}
            if native_not_found(result):
                return {'outcome': 'absent-at-request', 'conflictRetries': retry}
            if not native_conflict(result):
                raise ValueError('native-delete-rejected')
        raise ValueError('native-delete-conflict-retries-exhausted')

    def remove_named_finalizer(self, path, uid, finalizer):
        """One object, one named finalizer, only after deletion was requested and its reconciler is gone."""
        for retry in range(3):
            current = self.native_read(path)
            meta = (current or {}).get('metadata', {})
            if (current is None or meta.get('uid') != uid or not meta.get('deletionTimestamp')
                    or finalizer not in meta.get('finalizers', [])):
                raise ValueError('named-finalizer-precondition-failed')
            projected = project_resource(current)
            validate_allowlist([projected], [{**projected, 'action': 'remove-named-finalizer',
                                              'propagation': 'Foreground', 'finalizer': finalizer}])
            updated = copy.deepcopy(current)
            updated['metadata']['finalizers'] = [v for v in meta['finalizers'] if v != finalizer]
            # The PUT carries the observed UID and resourceVersion; the server rejects any intervening change.
            result = self.kube('named-finalizer-removal', 'replace', '--raw', path, '-f', '-', obj=updated, allowed=(0, 1))
            if result.returncode == 0:
                return {'outcome': 'named-finalizer-removed', 'finalizer': finalizer, 'resourceVersion': meta['resourceVersion'],
                        'otherFinalizersRetained': updated['metadata']['finalizers'], 'conflictRetries': retry}
            if not native_conflict(result):
                raise ValueError('named-finalizer-request-rejected')
        raise ValueError('named-finalizer-conflict-retries-exhausted')

    def probe_workspace_propagation(self):
        """Observe controller-processed workspace deletion on a synthetic workspace only."""
        template, namespace = 's426-probe', 's426-probe-member'
        template_path = '/apis/tenant.kubesphere.io/v1beta1/workspacetemplates/' + template
        workspace_path = '/apis/tenant.kubesphere.io/v1beta1/workspaces/' + template
        namespace_path = '/api/v1/namespaces/' + namespace
        self.last_step = 'workspace-propagation-probe'
        self.create({'apiVersion': 'tenant.kubesphere.io/v1beta1', 'kind': 'WorkspaceTemplate', 'metadata': {'name': template},
                     'spec': {'placement': {'clusterSelector': {}}, 'template': {'spec': {}}}})

        def observe(path, until, timeout):
            deadline = time.monotonic() + timeout
            while True:
                current = self.native_read(path)
                if until(current) or time.monotonic() > deadline:
                    return current
                time.sleep(3)

        workspace = observe(workspace_path, lambda v: v is not None, 60)
        self.create({'apiVersion': 'v1', 'kind': 'Namespace',
                     'metadata': {'name': namespace, 'labels': {'kubesphere.io/workspace': template}}})
        member = observe(namespace_path, lambda v: SYSTEM_WORKSPACE_FINALIZER in v['metadata'].get('finalizers', []), 45)
        before = self.native_read(template_path)
        if before is None or workspace is None or member is None:
            raise ValueError('workspace-probe-setup-incomplete')
        projections = {'template': project_resource(before), 'workspace': project_resource(workspace), 'member': project_resource(member)}
        request = self.native_retire('workspace-probe', template_path, before['metadata']['uid'])
        template_after = observe(template_path, lambda v: v is None, 90)
        workspace_after = observe(workspace_path, lambda v: v is None, 60)
        time.sleep(30)  # allow a delayed namespace cascade to become visible
        member_after = self.native_read(namespace_path)
        outcome = ('deleted' if member_after is None else 'terminating' if member_after['metadata'].get('deletionTimestamp')
                   else 'retained-label-or-finalizer-changed'
                   if project_resource(member_after)['managementLabels'] != projections['member']['managementLabels']
                   or member_after['metadata'].get('finalizers') != member['metadata'].get('finalizers') else 'retained-unchanged')
        if outcome == 'terminating':
            member_after = observe(namespace_path, lambda v: v is None, 120)
        record = {'scope': 'synthetic workspace and member namespace in the fresh fixture only; not a retirement action',
                  'controllersRunning': True, 'request': request, 'before': projections,
                  'templateRemoved': template_after is None, 'workspaceRemoved': workspace_after is None,
                  'memberNamespaceOutcome': outcome, 'memberNamespaceFinallyAbsent': member_after is None,
                  'memberAfter': project_resource(member_after) if member_after else None,
                  'cascadeHazard': outcome in ('deleted', 'terminating'), 'procedurePart': False, 'productionAccepted': False}
        self.attempt.record('workspace-propagation-probe.json', record)
        if outcome == 'terminating' and member_after is not None:
            raise ValueError('workspace-probe-namespace-stuck')
        return record

    def retire_phase(self, phase, baseline, by_uid, resources):
        name, expected = phase['phase'], set(phase['expected'])
        before, settled = self.settled_inventory()
        current = {v['uid']: v for v in before}
        if not settled:
            raise ValueError('fixture-state-did-not-settle-before-' + name)
        if not phase['controllersRunning'] and any(v['kind'] == 'Pod' and v['namespace'] in MANAGER_NAMESPACES for v in before):
            raise ValueError('manager-runtime-present-before-' + name)
        if phase['mode'] == 'named-finalizer' and any(set(current.get(uid, {}).get('finalizers', [])) - {SYSTEM_WORKSPACE_FINALIZER}
                                                     for uid in phase['roots']):
            raise ValueError('unreviewed-finalizer-before-named-intervention')
        requests = []
        self.last_step = 'retire-' + name
        for uid in phase['roots']:
            path = api_path(resources, by_uid[uid])
            outcome = self.native_retire(name, path, uid)
            if phase['mode'] == 'named-finalizer' and outcome['outcome'] == 'native-delete-accepted':
                outcome['intervention'] = self.remove_named_finalizer(path, uid, phase['namedFinalizer'])
            requests.append({'object': label(key(by_uid[uid])), 'uid': uid, **outcome})
        complete = self.wait_absent(expected, 300 if phase['controllersRunning'] else 180)
        after, settled = self.settled_inventory()
        delta = phase_delta(baseline, before, after, expected)
        record = {'phase': name, 'order': phase['order'], 'mode': phase['mode'], 'controllersRunning': phase['controllersRunning'],
                  'requests': requests, 'expectedAbsentWithinTimeout': complete, 'settled': settled, 'delta': delta,
                  'phaseAllowlistSha256': digest(canonical(phase)), 'licensedApplicationWritesUsed': False, 'productionAccepted': False}
        self.attempt.record(f'retirement-phase-{phase["order"]:02d}-{name}.json', record)
        if not complete:
            raise ValueError('phase-expected-removals-incomplete-' + name)
        if not settled:
            raise ValueError('fixture-state-did-not-settle-after-' + name)
        assert_phase(baseline, before, after, expected)
        return after

    def post_retirement_checks(self, final):
        """Native API/admission/namespace lifecycle must work without any KubeSphere component."""
        services = {(v['namespace'], v['name']) for v in final if (v['apiVersion'], v['kind']) == ('v1', 'Service')}
        stale = [label(key(v)) for v in final if v['kind'] in ('ValidatingWebhookConfiguration', 'MutatingWebhookConfiguration')
                 for hook in v['webhooks'] if hook['service']['name']
                 and (hook['service']['namespace'], hook['service']['name']) not in services]
        stale += [label(key(v)) for v in final if v['kind'] == 'APIService' and v['service']['name']
                  and (v['service']['namespace'], v['service']['name']) not in services]
        runtime = [label(key(v)) for v in final if v['namespace'] in MANAGER_NAMESPACES
                   and v['kind'] in ('Deployment', 'ReplicaSet', 'StatefulSet', 'DaemonSet')]
        completed = []
        for pod in self.get('pods')['items']:
            if pod['metadata'].get('namespace') in MANAGER_NAMESPACES:
                entry = label(key(project_resource(pod)))
                # A finished controller Job pod is inert residue for a named decision; anything else is live runtime.
                (completed if pod.get('status', {}).get('phase') in ('Succeeded', 'Failed') else runtime).append(entry)
        workload = 's426-workload'
        self.create({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 's426-post-retirement', 'namespace': workload},
                     'data': {'synthetic': 'post-retirement-native-write'}})
        written = self.native_read(f'/api/v1/namespaces/{workload}/configmaps/s426-post-retirement')
        write = self.native_retire('post-retirement-health', f'/api/v1/namespaces/{workload}/configmaps/s426-post-retirement',
                                   written['metadata']['uid'])
        self.create({'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': 's426-post-retirement'}})
        time.sleep(15)
        created = self.native_read('/api/v1/namespaces/s426-post-retirement')
        managed = project_resource(created)
        lifecycle = self.native_retire('post-retirement-namespace', '/api/v1/namespaces/s426-post-retirement',
                                       created['metadata']['uid'])
        deadline, gone = time.monotonic() + 120, False
        while not gone and time.monotonic() < deadline:
            gone = self.native_read('/api/v1/namespaces/s426-post-retirement') is None
            if not gone:
                time.sleep(5)
        record = {'staleAdmissionOrApiServiceReferences': stale, 'managerRuntimeRemaining': runtime,
                  'completedManagerPodResidue': completed,
                  'nativeConfigMapWriteDelete': write, 'newNamespaceManagementLabels': managed['managementLabels'],
                  'newNamespaceFinalizers': managed['finalizers'], 'namespaceLifecycle': lifecycle,
                  'newNamespaceDeletionCompleted': gone, 'scope': 'fresh fixture only', 'productionAccepted': False}
        self.attempt.record('post-retirement-native-health.json', record)
        if stale or runtime or managed['managementLabels'] or managed['finalizers'] or not gone:
            raise ValueError('post-retirement-native-health-failed')
        return record

    def retire_kubesphere(self):
        """Dependency-first native retirement: controller lifecycles first, exact native removal, named finalizers last."""
        self.probe_workspace_propagation()
        baseline_crds = self.crd_inventory()
        before, settled = self.settled_inventory()
        if not settled:
            raise ValueError('fixture-state-did-not-settle-before-retirement')
        self.attempt.record('kubesphere-before-state.json', {'resources': before, 'crds': baseline_crds, 'scope': 'fixture-only'})
        by_uid = {v['uid']: v for v in before}
        scope = retirement_scope(before)
        plan = dependency_plan(before, scope)
        actions = [{**{k: by_uid[uid][k] for k in ('apiVersion', 'kind', 'namespace', 'name', 'uid', 'resourceVersion')},
                    'action': 'delete', 'propagation': 'Foreground', 'phase': phase['phase'], 'order': phase['order'],
                    'request': ('native-named-finalizer' if phase['mode'] == 'named-finalizer' else 'native-uid-resource-version-delete')
                    if uid in phase['roots'] else ('controller-lifecycle' if phase['mode'] == 'controller-lifecycle' else 'owner-or-endpoints-propagation')}
                   for phase in plan for uid in phase['expected']]
        validate_allowlist(before, [{k: v for k, v in a.items() if k not in ('phase', 'order', 'request')} for a in actions])
        allowlist = {'actions': actions, 'phases': plan, 'chartSha256': self.args.ks_chart_sha256,
                     'sourceInventorySha256': self.args.source_digest, 'procedureSha256': self.args.script_digest,
                     'procedure': 'dependency-first native retirement; no Helm uninstall/hooks, wildcard, blanket finalizer or namespace/PVC edits',
                     'namedFinalizerInterventions': [label(key(by_uid[uid])) + ':' + SYSTEM_WORKSPACE_FINALIZER
                                                     for phase in plan if phase['mode'] == 'named-finalizer' for uid in phase['roots']],
                     'scope': 'fixture-only', 'productionAccepted': False}
        self.attempt.record('kubesphere-retirement-allowlist.json', allowlist)
        resources = self.discover({by_uid[uid]['apiVersion'] for uid in scope})
        baseline = set(by_uid)
        for phase in plan:
            self.retire_phase(phase, baseline, by_uid, resources)
        self.last_step = 'retirement-final-state'
        final, settled = self.settled_inventory()
        final_crds = self.crd_inventory()
        if not settled:
            raise ValueError('fixture-state-did-not-settle-after-retirement')
        delta = phase_delta(baseline, before, final, scope)
        self.attempt.record('kubesphere-after-state.json', {'resources': final, 'crds': final_crds, 'delta': delta,
                            'allowlistSha256': digest(canonical(allowlist)), 'productionAccepted': False})
        assert_phase(baseline, before, final, scope)
        if {(v['uid'], v['resourceVersion']) for v in baseline_crds} != {(v['uid'], v['resourceVersion']) for v in final_crds}:
            raise ValueError('retained-crd-changed')
        health = self.post_retirement_checks(final)
        retained = [{'object': label(key(v)), 'finalizers': v['finalizers'], 'managementLabels': v['managementLabels'],
                     'disposition': ('protected; KubeSphere label/finalizer residual retained pending a named decision'
                                     if v['kind'] in PROTECTED_KINDS else 'retained inert instance pending owner decision')}
                    for v in final if management_resource(v) and v['kind'] != 'CustomResourceDefinition']
        self.attempt.record('kubesphere-retirement-result.json', {'state': 'passed-dependency-first-native-retirement',
                    'allowlistSha256': digest(canonical(allowlist)), 'procedureSha256': self.args.script_digest,
                    'phases': [p['phase'] for p in plan], 'exactBaselineRemovals': len(delta['expectedRemovals']),
                    'licensedApplicationWritesUsed': False, 'helmUninstallOrVendorHooksUsed': False,
                    'blanketFinalizerRemovalUsed': False, 'namespacePvcPvIdentitiesAndBindingsPreserved': True,
                    'retainedCrds': len(final_crds), 'retainedKubeSphereMarkedObjects': retained,
                    'postRetirementHealth': health, 'productionAccepted': False})

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
        self.kube('uid-bound-native-delete', 'delete', '--raw', path, '-f', '-', obj=native_delete_options(action))

    def check_native_delete_preconditions(self):
        # Deliberately bypass local drift rejection only in this newly created
        # fixture, so native apiserver precondition enforcement is observed.
        name, namespace = 's426-precondition-race', 's426-management'
        path = '/api/v1/namespaces/' + namespace + '/configmaps/' + name
        obj = {'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': name, 'namespace': namespace},
               'data': {'synthetic': 'first-incarnation'}}
        self.create(obj)
        snapshot = self.get('configmap', name, namespace)
        self.kube('native-race-recreate-delete', 'delete', '--raw', path, '-f', '-',
                  obj=native_delete_options({**project_resource(snapshot), 'propagation': 'Foreground'}))
        self.kube('native-race-old-absent', 'wait', 'configmap/' + name, '-n', namespace, '--for=delete', '--timeout=20s')
        self.create({**obj, 'data': {'synthetic': 'second-incarnation'}})
        before = self.get('configmap', name, namespace)
        for label, field in [('stale-uid', 'uid'), ('stale-resource-version', 'resourceVersion')]:
            if label == 'stale-resource-version':
                snapshot = before
                replacement = copy.deepcopy(before)
                replacement['data']['synthetic'] = 'mutated-after-snapshot'
                self.kube('native-race-mutate', 'replace', '-f', '-', obj=replacement)
                before = self.get('configmap', name, namespace)
            action = {**project_resource(before), 'propagation': 'Foreground'}
            stale = snapshot['metadata'][field]
            if stale == action[field]:
                raise ValueError('native-negative-precondition-not-stale')
            result = self.kube('native-negative-' + label, 'delete', '--raw', path, '-f', '-',
                              obj=native_delete_options({**action, field: stale}), allowed=(0, 1))
            after = self.get('configmap', name, namespace)
            conflict = native_conflict(result)
            unchanged = (after['metadata']['uid'] == before['metadata']['uid']
                         and canonical(after) == canonical(before))
            self.attempt.record('native-precondition-' + label + '.json', {
                'precondition': field, 'nativeConflictConfirmed': conflict,
                'nativeStatusCode': 409 if conflict else None, 'uidAndContentUnchanged': unchanged,
                'snapshotIdentity': {k: snapshot['metadata'][k] for k in ('uid', 'resourceVersion')},
                'currentIdentity': {k: before['metadata'][k] for k in ('uid', 'resourceVersion')},
                'beforeObjectSha256': digest(canonical(before)), 'afterObjectSha256': digest(canonical(after)),
                'scope': 'fresh synthetic fixture only', 'productionAccepted': False})
            if not conflict or not unchanged:
                raise ValueError('native-delete-precondition-not-enforced')
        self.kube('native-race-cleanup', 'delete', '--raw', path, '-f', '-', obj=native_delete_options(action))
        self.kube('native-race-absent', 'wait', 'configmap/' + name, '-n', namespace, '--for=delete', '--timeout=20s')

    def execute(self):
        if self.args.ks_chart:
            self.install_kubesphere()
        self.populate()
        self.check_native_delete_preconditions()
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
            'nativeStaleUidAndResourceVersionConflictConfirmed': True,
            'syntheticCanarySha256': digest(canary), 'licensedKubeSphereWritesUsed': False,
            'stopRecovery': 'stop deletions; retain failed fixture encrypted diagnostics; rebuild only a fresh synthetic target',
            'limitations': [('Actual core runtime/uninstall outcome is recorded separately; broad vendor cleanup hooks remain disabled'
                            if self.args.ks_chart else 'Does not install ks-core or run its real controllers/webhooks/finalizers/uninstall hooks'),
                           'Fixture Kubernetes patch/CNI/runtime differ from source; no source workload/data restore',
                           'Explicit child-first native deletion exercises scope and finalizer preconditions; controller cascade still unaccepted',
                           'Synthetic deployment stays at zero; no production application health claim'],
            'productionRetirementAccepted': False, 'mutationAuthorized': False})
        if self.args.ks_chart:
            self.retire_kubesphere()

    def cleanup(self):
        outcomes = {}

        def command(argv, timeout):
            try:
                result = subprocess.run(argv, capture_output=True, timeout=timeout)
                return result, {'state': 'completed', 'exitCode': result.returncode}
            except subprocess.TimeoutExpired:
                return None, {'state': 'timed-out', 'exitCode': None}
            except OSError:
                return None, {'state': 'inaccessible', 'exitCode': None}

        def inspect(argv, kind, name):
            result, _ = command(argv, 30)
            return absence_state(result, kind, name) if result is not None else 'unverified'

        # Capture mounts before deleting an owned node, even when startup failed
        # before its normal inspection. Unknown coverage never proves absence.
        if self.created:
            try:
                self.capture_volumes()
            except (ValueError, OSError, subprocess.TimeoutExpired):
                self.volume_capture_verified = False
            _, result = command(['kind', 'delete', 'cluster', '--name', self.cluster,
                                 '--kubeconfig', str(self.kubeconfig)], 120)
            outcomes['kindDeleteExit'] = result['exitCode']
            outcomes['kindDeleteState'] = result['state']
        try:
            if self.kubeconfig.exists() or self.kubeconfig.is_symlink():
                self.kubeconfig.unlink()
            outcomes['freshCredentialFileAbsent'] = not (self.kubeconfig.exists() or self.kubeconfig.is_symlink())
        except OSError:
            outcomes['freshCredentialFileAbsent'] = False
        if self.network_created:
            _, result = command(['docker', 'network', 'rm', self.network], 30)
            outcomes['networkDeleteExit'] = result['exitCode']
            outcomes['networkDeleteState'] = result['state']
        inspections = {'node': inspect(['docker', 'inspect', self.node], 'container', self.node),
                       'network': inspect(['docker', 'network', 'inspect', self.network], 'network', self.network)}
        outcomes['inspectionStates'] = inspections
        outcomes['nodeAbsent'] = inspections['node'] == 'absent'
        outcomes['networkAbsent'] = inspections['network'] == 'absent'
        outcomes['volumeInventoryVerified'] = self.volume_capture_verified
        outcomes['volumeInspectionStates'] = {volume: inspect(['docker', 'volume', 'inspect', volume], 'volume', volume)
                                              for volume in self.fixture_volumes}
        outcomes['fixtureVolumesAbsent'] = (outcomes['volumeInventoryVerified']
                    and all(state == 'absent' for state in outcomes['volumeInspectionStates'].values()))
        image_cleanup = []
        for tag in reversed(self.image_tags):
            _, result = command(['docker', 'image', 'rm', tag], 60)
            image_cleanup.append({'tag': tag, **result})
        outcomes['fixtureImageTagsRemoved'] = image_cleanup
        outcomes['imageInspectionStates'] = {tag: inspect(['docker', 'image', 'inspect', tag], 'image', tag)
                                             for tag in self.image_tags}
        outcomes['fixtureImageTagsAbsent'] = all(state == 'absent' for state in outcomes['imageInspectionStates'].values())
        outcomes['globalPruneUsed'] = False
        outcomes['unrelatedDockerObjectsChanged'] = False
        outcomes['passed'] = (outcomes['nodeAbsent'] and outcomes['networkAbsent'] and outcomes['freshCredentialFileAbsent']
                              and outcomes['fixtureVolumesAbsent'] and outcomes['fixtureImageTagsAbsent'])
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
        except (ValueError, OSError, subprocess.TimeoutExpired, TypeError, KeyError, AttributeError, IndexError):
            pass


def rehearse(args):
    args.script_digest = file_digest(Path(__file__))
    source_bytes = args.source_inventory.read_bytes()
    args.source_digest = digest(source_bytes)
    args.source = json.loads(source_bytes)
    if (not isinstance(args.source, dict)
            or any(not isinstance(args.source.get(field), str) or not args.source[field]
                   for field in ('sourceClusterUid', 'nativeEndpoint'))):
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
    except (ValueError, OSError, subprocess.TimeoutExpired, TypeError, KeyError, AttributeError, IndexError) as error:
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
        except (ValueError, OSError, subprocess.TimeoutExpired, TypeError, KeyError, AttributeError, IndexError):
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
