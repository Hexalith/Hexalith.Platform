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
import shutil
import subprocess
import sys
import time
import urllib.parse

from evidence import Attempt, canonical, digest, file_digest, now
from qualify import PROTECTED_KINDS, management_resource, project_resource, safe


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
        namespace_intervention = (current['kind'] == 'Namespace' and current['apiVersion'] == 'v1'
                                  and current['name'] in NAMESPACE_FINALIZER_NAMES
                                  and action.get('action') == 'remove-named-finalizer'
                                  and action.get('finalizer') == SYSTEM_WORKSPACE_FINALIZER
                                  and not current.get('deletionTimestamp') and not current['owners'])
        if current['kind'] in PROTECTED_KINDS and not namespace_intervention:
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


def assert_preserved(before, after, expected_deleted, named_finalizers=None):
    initial = {key(v): v for v in before}
    current = {key(v): v for v in after}
    removed = set(initial) - set(current)
    if removed != set(expected_deleted):
        raise ValueError('unexpected-or-incomplete-deletion')
    for k in set(initial) & set(current):
        protected = ('owners', 'finalizers', 'namespaceFinalizers', 'managementLabels', 'storageClass', 'reclaimPolicy',
                     'storagePropertiesSha256', 'volumeMode', 'accessModes', 'provisioner', 'volumeBindingMode')
        fields = ('uid', 'binding', 'deletionTimestamp') + (protected if initial[k]['kind'] in PROTECTED_KINDS else ())
        if initial[k]['kind'] in ('Deployment', 'StatefulSet', 'DaemonSet', 'Job', 'CronJob', 'Pod'):
            fields += ('replicas', 'images', 'serviceAccount', 'claims', 'secretReferences',
                       'configMapReferences', 'configReferences')
        expected = dict(initial[k])
        if k in (named_finalizers or {}):
            finalizer = named_finalizers[k]
            if expected['kind'] != 'Namespace' or finalizer != SYSTEM_WORKSPACE_FINALIZER or finalizer not in expected['finalizers']:
                raise ValueError('unreviewed-namespace-finalizer-preservation')
            expected['finalizers'] = [v for v in expected['finalizers'] if v != finalizer]
        if any(expected.get(field) != current[k].get(field) for field in fields):
            raise ValueError('preserved-identity-or-binding-changed')


RELEASES = ('ks-core', 'ks-console-embed')
SYSTEM_WORKSPACE_FINALIZER = 'kubesphere.io/cascading-deletion'
PRODUCTION_FINALIZER_NAMES = ('default', 'kube-node-lease', 'kube-public', 'kube-system', 'kubekey-system',
                              'kubesphere-controls-system', 'kubesphere-system')
FIXTURE_FINALIZER_NAMES = tuple('s426-residue-' + str(n) for n in range(1, 8))
NAMESPACE_FINALIZER_NAMES = PRODUCTION_FINALIZER_NAMES + FIXTURE_FINALIZER_NAMES
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


# KubeSphere IAM controllers project some grants without ownerReferences; the label names the source object.
LABEL_LINKS = (('iam.kubesphere.io/workspacerolebinding-ref', ('iam.kubesphere.io/v1beta1', 'WorkspaceRoleBinding')),
               ('iam.kubesphere.io/user-ref', ('iam.kubesphere.io/v1beta1', 'User')),
               ('kubesphere.io/username', ('iam.kubesphere.io/v1beta1', 'User')))


def with_counterparts(inventory, uids):
    """Add exact native Endpoints, service-account token Secrets and label-linked IAM projections
    that their controllers remove with the source object."""
    expected = set(uids)
    while True:
        members = [v for v in inventory if v['uid'] in expected]
        services = {(v['namespace'], v['name']) for v in members if (v['apiVersion'], v['kind']) == ('v1', 'Service')}
        accounts = {(v['namespace'], v['name']) for v in members if v['kind'] == 'ServiceAccount'
                    and v['apiVersion'] in ('v1', 'kubesphere.io/v1alpha1')}
        sources = {(label, v['name']) for label, identity in LABEL_LINKS for v in members if (v['apiVersion'], v['kind']) == identity}
        linked = {v['uid'] for v in inventory if ((v['apiVersion'], v['kind']) == ('v1', 'Endpoints')
                  and (v['namespace'], v['name']) in services)
                  or (v['kind'] == 'Secret' and (v['namespace'], v.get('serviceAccountReference')) in accounts)
                  or any(v['managementLabels'].get(label) == name for label, name in sources)}
        expanded = descendants(inventory, expected | linked)
        if expanded == expected:
            return expected
        expected = expanded


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
    return with_counterparts(inventory, descendants(inventory, members))


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
    """Exact release objects/records, the controller-created system Workspace, the installed extension's
    leftover executor identities, admission/API registrations backed by retired Services and native/IAM counterparts."""
    installed = {v['name'] for v in inventory if (v['apiVersion'], v['kind']) == ('kubesphere.io/v1alpha1', 'InstallPlan')}
    scope = {v['uid'] for v in inventory if v['kind'] not in PROTECTED_KINDS
             and (v.get('helmRelease', {}).get('release-name') in RELEASES or release_record(v) or system_workspace(v)
                  or v['managementLabels'].get('kubesphere.io/extension-ref') in installed or console_residue(v))}
    scope = with_counterparts(inventory, descendants(inventory, scope))
    # Nothing deletes a dynamically registered webhook/APIService with its backend; it must be an explicit root.
    retired = {(v['namespace'], v['name']) for v in inventory if v['uid'] in scope and (v['apiVersion'], v['kind']) == ('v1', 'Service')}
    backends = lambda v: ({(w['service']['namespace'], w['service']['name']) for w in v.get('webhooks', [])}
                          | ({(v['service']['namespace'], v['service']['name'])} if v['kind'] == 'APIService' else set()))
    scope |= {v['uid'] for v in inventory if v['kind'] in ('ValidatingWebhookConfiguration', 'MutatingWebhookConfiguration', 'APIService')
              and backends(v) & retired}
    return with_counterparts(inventory, descendants(inventory, scope))


def _api(api, *kinds):
    return lambda v: v['apiVersion'] == api and v['kind'] in kinds


def console_residue(v):
    """Administrator-decided route/certificate/secret/lease identities; never a kind/namespace wildcard."""
    return v['namespace'] == 'kubesphere-system' and (v['apiVersion'], v['kind'], v['name']) in (
        ('networking.k8s.io/v1', 'Ingress', 'kubesphere-console'),
        ('cert-manager.io/v1', 'Certificate', 'kubesphere-console-letsencrypt'),
        ('v1', 'Secret', 'kubesphere-console-letsencrypt-tls'),
        ('coordination.k8s.io/v1', 'Lease', 'ks-controller-manager-leader-election'))


# Controllers still run through 'admission'; later phases cannot depend on a KubeSphere reconciler.
DEPENDENCY_PHASES = (
    ('installed-extension', _api('kubesphere.io/v1alpha1', 'InstallPlan')),
    ('global-role-bindings', _api('iam.kubesphere.io/v1beta1', 'GlobalRoleBinding')),
    ('workspace-role-bindings', _api('iam.kubesphere.io/v1beta1', 'WorkspaceRoleBinding')),
    ('workspace-roles', _api('iam.kubesphere.io/v1beta1', 'WorkspaceRole')),
    ('kubesphere-cluster-role-bindings', _api('iam.kubesphere.io/v1beta1', 'ClusterRoleBinding')),
    ('users', _api('iam.kubesphere.io/v1beta1', 'User')),
    ('global-roles', _api('iam.kubesphere.io/v1beta1', 'GlobalRole')),
    ('kubesphere-service-accounts', _api('kubesphere.io/v1alpha1', 'ServiceAccount')),
    ('catalog-extension', lambda v: _api('kubesphere.io/v1alpha1', 'Extension', 'ExtensionVersion')(v) and not v['owners']),
    ('extension-repository', _api('kubesphere.io/v1alpha1', 'Repository')),
    # Production-only app store; the egress-fenced fixture cannot sync its applications, so this phase is unrehearsed.
    ('application-store', _api('application.kubesphere.io/v2', 'Repo')),
    ('admission', lambda v: admission_registration(v) and bool(v.get('helmRelease'))),
    ('controllers-and-services', lambda v: (v['apiVersion'], v['kind']) in (('apps/v1', 'Deployment'), ('v1', 'Service'))),
    # A running manager recreates its dynamic registrations; retire them only after it is gone.
    ('reconciled-admission', lambda v: admission_registration(v) and not v.get('helmRelease')),
    ('console-route', console_residue),
    ('remaining-release-objects', lambda v: not release_record(v) and not system_workspace(v)),
    ('release-records', release_record),
    ('system-workspace-finalizers', system_workspace),
)
POST_CONTROLLER_PHASES = ('controllers-and-services', 'reconciled-admission', 'console-route', 'remaining-release-objects', 'release-records')


def admission_registration(v):
    return v['kind'] in ('ValidatingWebhookConfiguration', 'MutatingWebhookConfiguration') or (
        (v['apiVersion'], v['kind']) == ('apiregistration.k8s.io/v1', 'APIService'))


def route_service_backends(v):
    """(namespace, name) of every Service an Ingress or HTTPRoute sends traffic to; other backend kinds are not Services."""
    if v['kind'] not in ('Ingress', 'HTTPRoute'):
        return set()
    return {(b.get('namespace') or v['namespace'], b['name']) for b in v.get('backends') or []
            if (b.get('kind') or 'Service') == 'Service' and b.get('name')}


def dependency_plan(inventory, scope):
    """Partition the exact scope into ordered child-first phases before any request is sent."""
    by_uid = {v['uid']: v for v in inventory}
    if not set(scope) <= set(by_uid):
        raise ValueError('scope-outside-censused-inventory')
    # A retained route to a retired Service is an undecided consumer: close or repoint it before planning, never after deletion.
    retired = {(v['namespace'], v['name']) for v in inventory if v['uid'] in scope and (v['apiVersion'], v['kind']) == ('v1', 'Service')}
    if any(v['uid'] not in scope and route_service_backends(v) & retired for v in inventory):
        raise ValueError('route-consumer-of-retired-service-outside-scope')
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
            expected = with_counterparts(remaining, descendants(remaining, roots))
        if not expected:
            continue
        if expected - scope:
            raise ValueError('phase-outside-retirement-allowlist')
        if expected & assigned:
            raise ValueError('retirement-member-in-multiple-phases')
        members = [by_uid[uid] for uid in expected]
        if name in POST_CONTROLLER_PHASES and any(v['finalizers'] for v in members):
            raise ValueError('finalizer-after-controller-removal')
        # Between controller removal and this phase a stale registration must not block requests or discovery.
        if name == 'reconciled-admission' and any(v['kind'] == 'APIService' or any(
                hook.get('failurePolicy') != 'Ignore' for hook in v.get('webhooks', [])) for v in members):
            raise ValueError('blocking-registration-reconciled-by-controller')
        named = name == 'system-workspace-finalizers'
        if named and any(v['finalizers'] != [SYSTEM_WORKSPACE_FINALIZER] or v['owners'] or v['uid'] not in roots for v in members):
            raise ValueError('unreviewed-system-workspace-finalizer-scope')
        actions = [{**{k: v[k] for k in ('apiVersion', 'kind', 'namespace', 'name', 'uid', 'resourceVersion')},
                    'action': 'delete', 'propagation': 'Foreground'} for v in members]
        validate_allowlist(remaining, actions)
        assigned |= expected
        phases.append({'phase': name, 'order': order, 'controllersRunning': name not in POST_CONTROLLER_PHASES and not named,
                       # The controller-removal phase itself starts with the manager present; later phases require it gone.
                       'managerAbsentRequired': named or name in POST_CONTROLLER_PHASES[1:],
                       'mode': 'named-finalizer' if named else ('controller-lifecycle' if name == 'installed-extension' else 'delete'),
                       'namedFinalizer': SYSTEM_WORKSPACE_FINALIZER if named else None,
                       'roots': sorted(roots, key=lambda uid: (by_uid[uid]['kind'], by_uid[uid]['namespace'] or '', by_uid[uid]['name'])),
                       'expected': sorted(expected)})
    if scope - assigned:
        raise ValueError('unphased-retirement-member')
    namespaces = [v for v in inventory if v['kind'] == 'Namespace' and v['name'] in NAMESPACE_FINALIZER_NAMES
                  and SYSTEM_WORKSPACE_FINALIZER in v['finalizers']]
    if namespaces:
        actions = [{**v, 'action': 'remove-named-finalizer', 'finalizer': SYSTEM_WORKSPACE_FINALIZER,
                    'propagation': 'Foreground'} for v in namespaces]
        validate_allowlist(inventory, actions)
        phases.append({'phase': 'namespace-finalizers', 'order': len(DEPENDENCY_PHASES) + 1,
                       'controllersRunning': False, 'managerAbsentRequired': True,
                       'mode': 'namespace-finalizer', 'namedFinalizer': SYSTEM_WORKSPACE_FINALIZER,
                       'roots': sorted(v['uid'] for v in namespaces), 'expected': [],
                       'expectedModified': sorted(v['uid'] for v in namespaces)})
    return phases


def label(k):
    return '/'.join(v or '-' for v in k)


def review_digest(projection):
    """Digest of the sanitized reviewed content: every projected field except the server's resourceVersion."""
    return digest(canonical({k: v for k, v in projection.items() if k != 'resourceVersion'}))


def content_review_digest(raw):
    """Private digest of complete desired content; only status and server bookkeeping may churn."""
    content = copy.deepcopy(raw)
    content.pop('status', None)
    # CRDs without a status subresource also increment generation for status-only updates.
    # Full desired fields remain in the digest, including spec/data/rules and other metadata.
    for field in ('resourceVersion', 'managedFields', 'generation'):
        content.get('metadata', {}).pop(field, None)
    return digest(canonical(content))


def role_annotation_transitions(reviewed, current, removed, retirement_uids):
    """Only the exact User role annotation cleared by an already retired, matching GlobalRoleBinding."""
    annotation = 'iam.kubesphere.io/globalrole'
    transitions = []
    for binding_uid in sorted(removed):
        binding = reviewed.get(binding_uid, {})
        if (binding.get('apiVersion'), binding.get('kind')) != ('iam.kubesphere.io/v1beta1', 'GlobalRoleBinding'):
            continue
        role_ref = binding.get('roleRef', {})
        if (role_ref.get('apiGroup'), role_ref.get('kind')) != ('iam.kubesphere.io', 'GlobalRole'):
            continue
        role = role_ref.get('name')
        users = {s.get('name') for s in binding.get('subjects', []) if s.get('kind') == 'User'
                 and s.get('apiGroup') == 'iam.kubesphere.io'}
        for uid, before in reviewed.items():
            if (uid not in retirement_uids or (before.get('apiVersion'), before.get('kind')) != ('iam.kubesphere.io/v1beta1', 'User')
                    or before.get('metadata', {}).get('name') not in users
                    or before.get('metadata', {}).get('annotations', {}).get(annotation) != role):
                continue
            after = current.get(uid)
            if after is None:
                raise ValueError('expected-role-annotation-user-missing')
            if content_review_digest(before) == content_review_digest(after):
                continue
            expected = copy.deepcopy(before)
            annotations = expected['metadata']['annotations']
            annotations[annotation] = ''
            if content_review_digest(expected) != content_review_digest(after):
                raise ValueError('unexpected-controller-role-annotation-transition')
            transitions.append({'uid': uid, 'bindingUid': binding_uid, 'field': 'metadata.annotations.' + annotation,
                                'expected': expected})
    return transitions


def cluster_grant_annotation_transition(reviewed, first, second, bindings, retirement_uids,
                                        retired_binding_uids, phase, completed_phases):
    """Only the reviewed host grant cleared after every matching IAM cluster binding retires."""
    if phase != 'users' or 'kubesphere-cluster-role-bindings' not in completed_phases:
        raise ValueError('cluster-grant-checkpoint-before-binding-retirement')
    meta = reviewed.get('metadata', {})
    identity = (reviewed.get('apiVersion'), reviewed.get('kind'), meta.get('namespace'), meta.get('name'), meta.get('uid'))
    if (identity[:3] != ('iam.kubesphere.io/v1beta1', 'User', None) or not identity[3]
            or identity[4] not in retirement_uids or identity[4] in bindings):
        raise ValueError('cluster-grant-not-exact-reviewed-user')
    if not bindings or not set(bindings) <= set(retirement_uids) & set(retired_binding_uids):
        raise ValueError('cluster-grant-matching-binding-not-retired')
    for uid, binding in bindings.items():
        metadata = binding.get('metadata', {})
        if ((binding.get('apiVersion'), binding.get('kind'), metadata.get('namespace'), metadata.get('uid')) !=
                ('iam.kubesphere.io/v1beta1', 'ClusterRoleBinding', None, uid) or not metadata.get('name')
                or metadata.get('labels', {}).get('iam.kubesphere.io/role-ref') != 'cluster-admin'
                or metadata.get('labels', {}).get('iam.kubesphere.io/user-ref') != identity[3]
                or binding.get('roleRef') != {'apiGroup': 'iam.kubesphere.io', 'kind': 'ClusterRole', 'name': 'cluster-admin'}
                or binding.get('subjects') != [{'apiGroup': 'iam.kubesphere.io', 'kind': 'User', 'name': identity[3]}]):
            raise ValueError('cluster-grant-reviewed-binding-mismatch')
    annotation = 'iam.kubesphere.io/granted-clusters'
    if meta.get('annotations', {}).get(annotation) != 'host':
        raise ValueError('cluster-grant-reviewed-value-not-exact-host')
    expected = copy.deepcopy(reviewed)
    expected['metadata']['annotations'][annotation] = ''
    for current in (first, second):
        fields = (current or {}).get('metadata', {})
        if ((current or {}).get('apiVersion'), (current or {}).get('kind'), fields.get('namespace'), fields.get('name'), fields.get('uid')) != identity:
            raise ValueError('cluster-grant-user-identity-drift')
        value = fields.get('annotations', {}).get(annotation)
        if value not in ('host', ''):
            raise ValueError('cluster-grant-missing-key-or-unexpected-grants')
        if content_review_digest(current) != content_review_digest(reviewed if value == 'host' else expected):
            raise ValueError('cluster-grant-other-desired-content-drift')
    if content_review_digest(first) != content_review_digest(second):
        raise ValueError('cluster-grant-not-stable')
    return expected if second['metadata']['annotations'][annotation] == '' else None


def lease_renewal_transition(reviewed, first, second, retirement_uids, phase, manager_absent):
    """One exact final leader renewal, only after removal and two stable native reads."""
    if phase != 'console-route':
        raise ValueError('lease-renewal-checkpoint-wrong-phase')
    if not manager_absent:
        raise ValueError('lease-renewal-manager-still-present')
    metadata = reviewed.get('metadata', {})
    identity = (reviewed.get('apiVersion'), reviewed.get('kind'), metadata.get('namespace'), metadata.get('name'))
    if identity != ('coordination.k8s.io/v1', 'Lease', 'kubesphere-system', 'ks-controller-manager-leader-election') or metadata.get('uid') not in retirement_uids:
        raise ValueError('lease-renewal-not-exact-retirement-identity')
    for current in (first, second):
        fields = (current or {}).get('metadata', {})
        if ((current or {}).get('apiVersion'), (current or {}).get('kind'), fields.get('namespace'), fields.get('name'), fields.get('uid')) != (*identity, metadata['uid']):
            raise ValueError('lease-renewal-identity-drift')
    if content_review_digest(first) != content_review_digest(second):
        raise ValueError('lease-renewal-not-stable')
    expected = copy.deepcopy(reviewed)
    renewal = second.get('spec', {}).get('renewTime')
    def timestamp(value):
        if not isinstance(value, str) or not re.fullmatch(r'\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,9})?(?:Z|[+-]\d{2}:\d{2})', value):
            raise ValueError('lease-renewal-invalid-time')
        try:
            return datetime.fromisoformat(value.replace('Z', '+00:00'))
        except ValueError:
            raise ValueError('lease-renewal-invalid-time') from None
    if timestamp(renewal) < timestamp(reviewed.get('spec', {}).get('renewTime')):
        raise ValueError('lease-renewal-backwards-time')
    expected['spec']['renewTime'] = renewal
    if content_review_digest(expected) != content_review_digest(second):
        raise ValueError('lease-renewal-other-desired-content-drift')
    return None if content_review_digest(reviewed) == content_review_digest(second) else expected


def category_count_transition(reviewed, first, second, reviewed_extensions, retirement_uids,
                              phase, completed_phases, manager_absent, extensions_absent):
    """The exact retired extension membership count, after its controllers and catalog are absent."""
    if phase != 'remaining-release-objects':
        raise ValueError('category-count-checkpoint-wrong-phase')
    if not {'catalog-extension', 'controllers-and-services'} <= set(completed_phases):
        raise ValueError('category-count-before-catalog-controller-retirement')
    if not manager_absent or not extensions_absent:
        raise ValueError('category-count-manager-or-extensions-present')
    metadata = reviewed.get('metadata', {})
    identity = (reviewed.get('apiVersion'), reviewed.get('kind'), metadata.get('namespace'), metadata.get('name'))
    if identity[:3] != ('kubesphere.io/v1alpha1', 'Category', None) or not identity[3] or metadata.get('uid') not in retirement_uids:
        raise ValueError('category-count-not-exact-retirement-identity')
    for current in (first, second):
        fields = (current or {}).get('metadata', {})
        if ((current or {}).get('apiVersion'), (current or {}).get('kind'), fields.get('namespace'), fields.get('name'), fields.get('uid')) != (*identity, metadata['uid']):
            raise ValueError('category-count-identity-drift')
    if content_review_digest(first) != content_review_digest(second):
        raise ValueError('category-count-not-stable')
    members = [v for v in reviewed_extensions if (v.get('apiVersion'), v.get('kind')) ==
               ('kubesphere.io/v1alpha1', 'Extension') and v.get('metadata', {}).get('labels', {}).get('kubesphere.io/category') == identity[3]]
    member_uids = [v.get('metadata', {}).get('uid') for v in members]
    if any(not uid or uid not in retirement_uids for uid in member_uids) or len(set(member_uids)) != len(member_uids):
        raise ValueError('category-count-membership-not-retired')
    annotation = 'kubesphere.io/count'
    if metadata.get('annotations', {}).get(annotation) != str(len(members)):
        raise ValueError('category-count-reviewed-membership-mismatch')
    if second['metadata'].get('annotations', {}).get(annotation) != '0':
        raise ValueError('category-count-current-not-zero')
    expected = copy.deepcopy(reviewed)
    expected['metadata']['annotations'][annotation] = '0'
    if content_review_digest(expected) != content_review_digest(second):
        raise ValueError('category-count-other-desired-content-drift')
    return None if content_review_digest(reviewed) == content_review_digest(second) else expected


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


def assert_phase(baseline_uids, before, after, expected_uids, named_finalizers=None):
    """Exact baseline removals; attempt-created transients may disappear, retired keys may not reappear."""
    tracked = lambda items: [v for v in items if v['uid'] in baseline_uids or v['kind'] in PROTECTED_KINDS]
    initial = tracked(before)
    expected = {key(v) for v in initial if v['uid'] in expected_uids}
    if expected & {key(v) for v in after if v['uid'] not in expected_uids}:
        raise ValueError('controller-recreated-retired-object')
    assert_preserved(initial, tracked(after), expected, named_finalizers)


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


SYNTHETIC_HOLD = 'qualification.hexalith.io/hold'


def synthetic_hold_blocked(obj):
    """Deletion is held only by the named synthetic finalizer; GC's transient foregroundDeletion is tolerated."""
    meta = obj['metadata']
    return bool(meta.get('deletionTimestamp')) and set(meta.get('finalizers') or []) - {'foregroundDeletion'} == {SYNTHETIC_HOLD}


def tool_identities(args):
    """Digests of the supplied files, the PATH-resolved runtime binaries and their version output:
    identities, not release authenticity."""
    files = {name: (file_digest(path) if path else None)
             for name, path in (('kubectl', args.kubectl), ('helm', getattr(args, 'helm', None)), ('age', args.age))}
    versions = {}
    for name in ('kind', 'docker'):
        resolved = shutil.which(name)
        if resolved is None:
            versions[name] = {'exitCode': None, 'outputSha256': None, 'binarySha256': None}
            continue
        # Hash the bytes the PATH lookup reaches (through any symlink) and run that same lookup result.
        result = subprocess.run([resolved, 'version'], capture_output=True, timeout=30)
        versions[name] = {'exitCode': result.returncode, 'outputSha256': digest(result.stdout),
                          'binarySha256': file_digest(os.path.realpath(resolved))}
    return {'toolSha256': files, 'kubectlExecuted': False, 'runtimeVersionOutput': versions, 'releaseAuthenticityVerified': False}


class ScopedAttempt:
    """One immutable attempt, distinct rollback record/export names; no additional evidence root."""
    def __init__(self, attempt, prefix):
        self.attempt, self.prefix = attempt, prefix
        self.directory = attempt.directory / (prefix + '-work')
        self.directory.mkdir(mode=0o700)

    def record(self, name, value):
        return self.attempt.record(self.prefix + '-' + name, value)

    def encrypt(self, name, *args):
        return self.attempt.encrypt(self.prefix + '-' + name, *args)


class PhaseExecutor:
    """Reviewed phase guards with an injected native transport and inventory methods.

    Implement kube, settled_inventory and wait_absent without importing source
    credentials into a fixture. Fixture and the explicit production client share
    these exact deletion, finalizer and controller-transition guards.
    """
    def native_read(self, path):
        result = self.kube('native-read', 'get', '--raw', path, allowed=(0, 1))
        if result.returncode == 0:
            return json.loads(result.stdout)
        if native_not_found(result):
            return None
        raise ValueError('native-read-failed')

    def native_retire(self, phase, path, reviewed):
        """Fresh read compared with the reviewed allowlist entry, then a DELETE carrying its UID and the read resourceVersion.
        Changed resourceVersions require identical private full-content and projected digests.
        Without a private content baseline, the reviewed resourceVersion must remain unchanged."""
        uid = reviewed['uid']
        for retry in range(3):
            current = self.native_read(path)
            if current is None:
                return {'outcome': 'absent-before-request', 'conflictRetries': retry}
            if current['metadata']['uid'] != uid:
                raise ValueError('uid-drift-before-native-delete')
            version = current['metadata']['resourceVersion']
            changed = version != reviewed['resourceVersion']
            if changed and review_digest(project_resource(current)) != review_digest(reviewed):
                raise ValueError('reviewed-entry-drift-before-native-delete')
            content_verified = self.verify_reviewed_content(current, reviewed)
            binding = {'reviewedResourceVersion': reviewed['resourceVersion'], 'resourceVersion': version,
                       'resourceVersionChangedSinceReview': changed, 'reviewedProjectionSha256': review_digest(reviewed),
                       'privateFullContentVerified': content_verified,
                       'strictReviewedResourceVersion': not content_verified}
            result = self.kube('native-retire-' + phase, 'delete', '--raw', path, '-f', '-', allowed=(0, 1),
                               obj=native_delete_options({'uid': uid, 'resourceVersion': version, 'propagation': 'Foreground'}))
            if result.returncode == 0:
                return {'outcome': 'native-delete-accepted', **binding, 'conflictRetries': retry}
            if native_not_found(result):
                return {'outcome': 'absent-at-request', **binding, 'conflictRetries': retry}
            if not native_conflict(result):
                raise ValueError('native-delete-rejected')
        raise ValueError('native-delete-conflict-retries-exhausted')

    def verify_reviewed_content(self, current, reviewed):
        expected = self.reviewed_content_digests.get(reviewed['uid'])
        if expected is not None:
            if content_review_digest(current) != expected:
                raise ValueError('reviewed-full-content-drift')
            return True
        if current['metadata']['resourceVersion'] != reviewed['resourceVersion']:
            raise ValueError('reviewed-resource-version-drift-without-content-baseline')
        return False

    def remove_named_finalizer(self, path, uid, finalizer):
        """One object, one named finalizer, only after deletion was requested and its reconciler is gone."""
        reviewed = self.reviewed_raw_by_uid.get(uid)
        if reviewed is None or reviewed.get('metadata', {}).get('uid') != uid:
            raise ValueError('named-finalizer-reviewed-content-baseline-missing')
        deleted_at = None
        for retry in range(3):
            current = self.native_read(path)
            meta = (current or {}).get('metadata', {})
            if (current is None or meta.get('uid') != uid or not meta.get('deletionTimestamp')
                    or not isinstance(meta.get('resourceVersion'), str) or not meta['resourceVersion']
                    or finalizer not in meta.get('finalizers', [])):
                raise ValueError('named-finalizer-precondition-failed')
            if deleted_at is not None and meta['deletionTimestamp'] != deleted_at:
                raise ValueError('named-finalizer-deletion-state-drift')
            deleted_at = meta['deletionTimestamp']
            compared = copy.deepcopy(current)
            reviewed_meta = reviewed['metadata']
            if 'deletionTimestamp' not in reviewed_meta:
                compared['metadata'].pop('deletionTimestamp', None)
            if ('deletionGracePeriodSeconds' not in reviewed_meta
                    and type(meta.get('deletionGracePeriodSeconds')) is int and meta['deletionGracePeriodSeconds'] == 0):
                compared['metadata'].pop('deletionGracePeriodSeconds')
            # Foreground DELETE adds this server finalizer. Ignore only its new presence
            # for comparison; the PUT below retains it and every other finalizer.
            if 'foregroundDeletion' not in reviewed_meta.get('finalizers', []):
                compared['metadata']['finalizers'] = [v for v in meta['finalizers'] if v != 'foregroundDeletion']
            if content_review_digest(compared) != content_review_digest(reviewed):
                raise ValueError('reviewed-full-content-drift')
            projected = project_resource(reviewed)
            validate_allowlist([projected], [{**projected, 'action': 'remove-named-finalizer',
                                              'propagation': 'Foreground', 'finalizer': finalizer}])
            updated = copy.deepcopy(current)
            updated['metadata']['finalizers'] = [v for v in meta['finalizers'] if v != finalizer]
            # The PUT carries the observed UID and resourceVersion; the server rejects any intervening change.
            result = self.kube('named-finalizer-removal', 'replace', '--raw', path, '-f', '-', obj=updated, allowed=(0, 1))
            if result.returncode == 0:
                return {'outcome': 'named-finalizer-removed', 'finalizer': finalizer, 'resourceVersion': meta['resourceVersion'],
                        'otherFinalizersRetained': updated['metadata']['finalizers'], 'conflictRetries': retry,
                        'privateFullContentVerified': True}
            if not native_conflict(result):
                raise ValueError('named-finalizer-request-rejected')
        raise ValueError('named-finalizer-conflict-retries-exhausted')

    def remove_namespace_finalizer(self, path, reviewed):
        """Preserve the named namespace and remove just its decided metadata finalizer, with native UID/RV binding."""
        current = self.native_read(path)
        if current is None or current['metadata']['uid'] != reviewed['uid']:
            raise ValueError('namespace-finalizer-identity-drift')
        projected = project_resource(current)
        if review_digest(projected) != review_digest(reviewed):
            raise ValueError('namespace-finalizer-reviewed-entry-drift')
        content_verified = self.verify_reviewed_content(current, reviewed)
        validate_allowlist([projected], [{**projected, 'action': 'remove-named-finalizer',
                           'finalizer': SYSTEM_WORKSPACE_FINALIZER, 'propagation': 'Foreground'}])
        updated = copy.deepcopy(current)
        updated['metadata']['finalizers'] = [v for v in current['metadata']['finalizers'] if v != SYSTEM_WORKSPACE_FINALIZER]
        self.kube('namespace-named-finalizer-removal', 'replace', '--raw', path, '-f', '-', obj=updated)
        return {'outcome': 'namespace-retained-named-finalizer-removed', 'finalizer': SYSTEM_WORKSPACE_FINALIZER,
                'uid': reviewed['uid'], 'resourceVersion': current['metadata']['resourceVersion'],
                'otherFinalizersRetained': updated['metadata']['finalizers'], 'deleteRequested': False,
                'privateFullContentVerified': content_verified}

    def retire_phase(self, phase, baseline, by_uid, resources):
        name, expected = phase['phase'], set(phase['expected'])
        before, settled = self.settled_inventory()
        current = {v['uid']: v for v in before}
        if not settled:
            raise ValueError('fixture-state-did-not-settle-before-' + name)
        if phase['managerAbsentRequired'] and any(v['kind'] == 'Pod' and v['namespace'] in MANAGER_NAMESPACES for v in before):
            raise ValueError('manager-runtime-present-before-' + name)
        if phase['mode'] == 'named-finalizer' and any(set(current.get(uid, {}).get('finalizers', [])) - {SYSTEM_WORKSPACE_FINALIZER}
                                                     for uid in phase['roots']):
            raise ValueError('unreviewed-finalizer-before-named-intervention')
        if name == 'users':
            transitions = []
            for uid in phase['roots']:
                reviewed = self.reviewed_raw_by_uid.get(uid, {})
                if (reviewed.get('apiVersion'), reviewed.get('kind')) != ('iam.kubesphere.io/v1beta1', 'User'):
                    continue
                username = reviewed.get('metadata', {}).get('name')
                if not isinstance(username, str) or not username or reviewed['metadata'].get('uid') != uid:
                    raise ValueError('cluster-grant-not-exact-reviewed-user')
                bindings = {binding_uid: binding for binding_uid, binding in self.reviewed_raw_by_uid.items()
                    if (binding.get('apiVersion'), binding.get('kind')) == ('iam.kubesphere.io/v1beta1', 'ClusterRoleBinding')
                    and (binding.get('metadata', {}).get('labels', {}).get('iam.kubesphere.io/user-ref') == username
                         or binding.get('metadata', {}).get('name') == username + '-cluster-admin'
                         or any(v.get('name') == username for v in binding.get('subjects', [])))}
                if not bindings:
                    continue
                retired = set(bindings) - set(current)
                cluster_grant_annotation_transition(reviewed, reviewed, reviewed, bindings, self.retirement_uids,
                    retired, name, self.completed_retirement_phases)
                self.last_step = 'cluster-grant-annotation-checkpoint'
                for binding_uid in bindings:
                    if self.native_read(api_path(resources, by_uid[binding_uid])) is not None:
                        raise ValueError('cluster-grant-matching-binding-still-present')
                path = api_path(resources, by_uid[uid])
                deadline, previous = time.monotonic() + 30, None
                for read_count in range(1, 11):
                    observed = self.native_read(path)
                    refreshed = cluster_grant_annotation_transition(reviewed, observed, observed, bindings,
                        self.retirement_uids, retired, name, self.completed_retirement_phases)
                    if previous is not None and previous['metadata']['annotations']['iam.kubesphere.io/granted-clusters'] == '':
                        refreshed = cluster_grant_annotation_transition(reviewed, previous, observed, bindings,
                            self.retirement_uids, retired, name, self.completed_retirement_phases)
                        if refreshed is not None:
                            break
                    if time.monotonic() >= deadline or read_count == 10:
                        raise ValueError('cluster-grant-transition-not-stable-within-bound')
                    previous = observed
                    time.sleep(2)
                for binding_uid in bindings:
                    if self.native_read(api_path(resources, by_uid[binding_uid])) is not None:
                        raise ValueError('cluster-grant-matching-binding-still-present')
                transitions.append({'uid': uid, 'bindingUids': sorted(bindings), 'reviewed': reviewed, 'bindings': bindings,
                    'first': previous, 'second': observed, 'expected': refreshed, 'nativeUserReadCount': read_count,
                    'field': 'metadata.annotations.iam.kubesphere.io/granted-clusters'})
            if transitions:
                self.attempt.encrypt('cluster-grant-annotation-transition', canonical({'transitions': transitions,
                    'basis': 'exact host grant clear-to-empty-string after every matching reviewed IAM cluster binding retires; two stable native reads'}),
                    self.args.age, self.args.recipient)
                for transition in transitions:
                    uid = transition['uid']
                    self.reviewed_raw_by_uid[uid] = transition['expected']
                    self.reviewed_content_digests[uid] = content_review_digest(transition['expected'])
                self.attempt.record('cluster-grant-annotation-transition.json', {'phase': name,
                    'clusterBindingRetirementPhaseCompleted': True, 'allMatchingReviewedBindingsRetired': True,
                    'twoNativeReadsStable': True, 'clearToEmptyStringWithKeyRetained': True,
                    'otherDesiredContentChangesAccepted': False,
                    'transitions': [{k: t[k] for k in ('uid', 'bindingUids', 'field', 'nativeUserReadCount')} for t in transitions],
                    'privateReadbackExport': 'cluster-grant-annotation-transition.age', 'productionAccepted': False})
        if name == 'console-route':
            for uid in phase['roots']:
                reviewed = self.reviewed_raw_by_uid.get(uid, {})
                if reviewed.get('kind') != 'Lease':
                    continue
                if 'controllers-and-services' not in self.completed_retirement_phases:
                    raise ValueError('lease-renewal-before-controller-retirement')
                manager_absent = not any(v['namespace'] in MANAGER_NAMESPACES and v['kind'] in
                                        ('Pod', 'Deployment', 'ReplicaSet', 'StatefulSet', 'DaemonSet') for v in before)
                path = api_path(resources, by_uid[uid])
                first = self.native_read(path)
                time.sleep(2)
                second = self.native_read(path)
                renewed = lease_renewal_transition(reviewed, first, second, self.retirement_uids, name, manager_absent)
                if renewed is not None:
                    self.attempt.encrypt('leader-lease-renewal-transition', canonical({
                        'reviewed': reviewed, 'first': first, 'second': second, 'expected': renewed,
                        'basis': 'exact allowlisted leader Lease final renewal after confirmed controller removal; two stable native reads'}),
                        self.args.age, self.args.recipient)
                    self.reviewed_raw_by_uid[uid] = renewed
                    self.reviewed_content_digests[uid] = content_review_digest(renewed)
                    self.attempt.record('leader-lease-renewal-transition.json', {
                        'phase': name, 'uid': uid, 'name': reviewed['metadata']['name'], 'namespace': reviewed['metadata']['namespace'],
                        'managerWorkloadsAndPodsAbsent': manager_absent, 'controllerRetirementPhaseCompleted': True,
                        'twoNativeReadsStable': True, 'renewTimeMonotonic': True,
                        'otherDesiredContentChangesAccepted': False, 'privateReadbackExport': 'leader-lease-renewal-transition.age',
                        'productionAccepted': False})
        if name == 'remaining-release-objects':
            transitions = []
            manager_absent = not any(v['namespace'] in MANAGER_NAMESPACES and v['kind'] in
                                    ('Pod', 'Deployment', 'ReplicaSet', 'StatefulSet', 'DaemonSet') for v in before)
            extensions_absent = not any(v['kind'] == 'Extension' for v in before)
            extensions = list(self.reviewed_raw_by_uid.values())
            for uid in phase['roots']:
                reviewed = self.reviewed_raw_by_uid.get(uid, {})
                if (reviewed.get('apiVersion'), reviewed.get('kind')) != ('kubesphere.io/v1alpha1', 'Category'):
                    continue
                path = api_path(resources, by_uid[uid])
                first = self.native_read(path)
                time.sleep(2)
                second = self.native_read(path)
                refreshed = category_count_transition(reviewed, first, second, extensions, self.retirement_uids,
                    name, self.completed_retirement_phases, manager_absent, extensions_absent)
                if refreshed is not None:
                    export = 'category-count-transition-' + uid
                    self.attempt.encrypt(export, canonical({'reviewed': reviewed, 'first': first, 'second': second,
                        'expected': refreshed, 'reviewedExtensions': extensions,
                        'basis': 'exact category count after reviewed extension membership and manager retirement'}),
                        self.args.age, self.args.recipient)
                    self.reviewed_raw_by_uid[uid] = refreshed
                    self.reviewed_content_digests[uid] = content_review_digest(refreshed)
                    transitions.append({'uid': uid, 'name': reviewed['metadata']['name'],
                        'field': 'metadata.annotations.kubesphere.io/count', 'currentCountZero': True,
                        'reviewedExtensionMembershipMatchesCount': True, 'reviewedExtensionMembersRetired': True,
                        'twoNativeReadsStable': True, 'otherDesiredContentChangesAccepted': False,
                        'privateReadbackExport': export + '.age'})
            if transitions:
                self.attempt.record('category-count-transitions.json', {'phase': name,
                    'catalogAndControllerRetirementPhasesCompleted': True, 'managerWorkloadsAndPodsAbsent': manager_absent,
                    'extensionsAbsent': extensions_absent, 'transitions': transitions, 'productionAccepted': False})
        requests = []
        self.last_step = 'retire-' + name
        for uid in phase['roots']:
            path = api_path(resources, by_uid[uid])
            # by_uid holds the reviewed allowlist entry (baseline projection), not this phase's fresh read.
            outcome = (self.remove_namespace_finalizer(path, by_uid[uid]) if phase['mode'] == 'namespace-finalizer'
                       else self.native_retire(name, path, by_uid[uid]))
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
        modifications = {key(by_uid[uid]): phase['namedFinalizer'] for uid in phase.get('expectedModified', [])}
        assert_phase(baseline, before, after, expected, modifications)
        if name == 'global-role-bindings':
            transitions = role_annotation_transitions(self.reviewed_raw_by_uid, self.raw_inventory_by_uid,
                                                      expected, self.retirement_uids)
            if transitions:
                self.attempt.encrypt('global-role-annotation-transition', canonical({'transitions': transitions,
                    'basis': 'exact annotation clear-to-empty-string caused by matching allowlisted GlobalRoleBinding retirement'}),
                    self.args.age, self.args.recipient)
                for transition in transitions:
                    uid = transition['uid']
                    self.reviewed_raw_by_uid[uid] = transition['expected']
                    self.reviewed_content_digests[uid] = content_review_digest(transition['expected'])
                self.attempt.record('global-role-annotation-transition.json', {
                    'phase': name, 'transitions': [{k: v for k, v in t.items() if k != 'expected'} for t in transitions],
                    'privateReadbackExport': 'global-role-annotation-transition.age',
                    'otherDesiredContentChangesAccepted': False, 'productionAccepted': False})
        self.completed_retirement_phases.add(name)
        return after



class Fixture(PhaseExecutor):
    """All native mutations go through docker exec into the newly created node."""
    def __init__(self, args, attempt):
        self.args, self.attempt = args, attempt
        self.cluster = 's426-' + digest(args.attempt_id.encode())[:12]
        self.network = self.cluster + '-internal'
        self.node = self.cluster + '-control-plane'
        self.entrypoint_reader = self.cluster + '-entrypoint-reader'
        self.entrypoint_reader_started = False
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
        self.rollback_fixture = None
        self.raw_inventory_by_uid = {}
        self.reviewed_content_digests = {}
        self.reviewed_raw_by_uid = {}
        self.retirement_uids = set()
        self.completed_retirement_phases = set()

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
                ('container', self.entrypoint_reader, ['docker', 'container', 'inspect', self.entrypoint_reader]),
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
        self.entrypoint_reader_started = True  # clean the exact helper even when launch, encryption or waiting fails
        original = self.run('read-public-entrypoint', ['docker', 'run', '--rm', '--name', self.entrypoint_reader,
                             '--network', 'none', '--entrypoint',
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
        network_options = []
        for field, option in (('Subnet', '--subnet'), ('Gateway', '--gateway')):
            if getattr(self, 'network_config', {}).get(field):
                network_options += [option, self.network_config[field]]
        self.run('create-internal-network', ['docker', 'network', 'create', '--internal', *network_options, self.network])
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
        self.node_ip = container['NetworkSettings']['Networks'][self.network]['IPAddress']
        self.network_config = network['IPAM']['Config'][0]
        self.container_id = container['Id']
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
        declared = ('sourceCredentialsImported', 'sourceDataImported')
        self.attempt.record('isolation.json', {'fixture': {k: v for k, v in identity.items() if k not in declared},
                    'unmeasuredDeclarations': {**{k: identity[k] for k in declared},
                        'basis': 'procedure design accepts no source kubeconfig or data; not measured'},
                    'internalNetwork': network['Internal'],
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
        self.vendor_images = sorted(images)
        image_records = self.load_vendor_images(self.vendor_images)
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
        # The initial host-Cluster reconcile can race the apiserver Service's readiness.
        # Requeue only this fixture's host object after its native Service proxy actually answers.
        self.refresh_fixture_host_readiness()
        self.record_runtime(ready, limited, installed_plan.get('status', {}).get('state') == 'Installed')

    def refresh_fixture_host_readiness(self):
        deadline = time.monotonic() + 120
        while True:
            result = self.kube('kubesphere-native-version-ready', 'get', '--raw',
                '/api/v1/namespaces/kubesphere-system/services/http:ks-apiserver:80/proxy/version', allowed=(0, 1))
            if result.returncode == 0:
                version = json.loads(result.stdout)
                if not isinstance(version, dict) or not version.get('gitVersion'):
                    raise ValueError('fixture-kubesphere-version-response-invalid')
                break
            if time.monotonic() > deadline:
                raise ValueError('fixture-kubesphere-service-not-ready')
            time.sleep(5)
        self.kube('refresh-fixture-host-readiness', 'annotate', 'clusters.cluster.kubesphere.io', 'host',
                  'qualification.hexalith.io/runtime-ready-check=' + now(), '--overwrite')

    def load_vendor_images(self, images):
        image_records = []
        for image in images:
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
                source_code = source.wait(timeout=30)
                source_error = source.stderr.read()
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
        return image_records

    def record_runtime(self, ready, limited, extension_installed):
        tenant_sync = self.wait_tenant_sync()
        self.attempt.record('kubesphere-runtime.json', {'deployedCore': 'ks-core1.2.4/app4.2.1', 'readyDeployments': ready,
                    'unreadyDeployments': limited, 'fullRuntimeAccepted': False,
                    'nativeCredentialsOnly': True, 'licensedApplicationWritesUsed': False,
                    'sourceConsoleEmbedInstallPlanReproduced': extension_installed,
                    'systemWorkspaceSynchronized': tenant_sync is not None, 'tenantSyncSeconds': tenant_sync})
        if tenant_sync is None:
            # Production has Workspace/system-workspace; without it the fixture baseline is not representative.
            raise ValueError('kubesphere-tenant-sync-not-ready')

    def wait_tenant_sync(self, timeout=300):
        """Seconds until the controller materializes the system Workspace, or None."""
        started = time.monotonic()
        while True:
            if self.native_read('/apis/tenant.kubesphere.io/v1beta1/workspaces/system-workspace') is not None:
                return round(time.monotonic() - started)
            if time.monotonic() - started > timeout:
                return None
            time.sleep(5)

    def resource_list(self):
        # Native names alone omit chart-owned KubeSphere ServiceAccounts/IAM/tenant
        # instances. Include all served KubeSphere CRD resources before and after;
        # CRDs are retained and compared separately, so their large schemas are read once per side.
        if self.custom_resources is None:
            self.custom_resources = sorted({v['name'] for v in self.crd_inventory()
                                            if ((v['customResource']['group'] or '').endswith('kubesphere.io')
                                                or v['customResource']['group'] in ('cert-manager.io', 'acme.cert-manager.io'))
                                            and any(version['served'] for version in v['customResource']['versions'])})
        return ','.join([self.NATIVE_RESOURCES, *self.custom_resources])

    NATIVE_RESOURCES = ('namespaces,persistentvolumes,persistentvolumeclaims,storageclasses.storage.k8s.io,'
                        'deployments.apps,replicasets.apps,statefulsets.apps,daemonsets.apps,jobs.batch,pods,services,endpoints,'
                        'endpointslices.discovery.k8s.io,configmaps,secrets,serviceaccounts,roles.rbac.authorization.k8s.io,'
                        'rolebindings.rbac.authorization.k8s.io,clusterroles.rbac.authorization.k8s.io,'
                        'clusterrolebindings.rbac.authorization.k8s.io,apiservices.apiregistration.k8s.io,'
                        'validatingwebhookconfigurations.admissionregistration.k8s.io,mutatingwebhookconfigurations.admissionregistration.k8s.io,'
                        'leases.coordination.k8s.io,ingresses.networking.k8s.io,networkpolicies.networking.k8s.io,cronjobs.batch,'
                        'controllerrevisions.apps,poddisruptionbudgets.policy,horizontalpodautoscalers.autoscaling,resourcequotas,limitranges')

    def crd_inventory(self):
        return [project_resource(v) for v in self.get('customresourcedefinitions.apiextensions.k8s.io')['items']]

    def kubesphere_inventory(self):
        raw = self.get(self.resource_list())['items']
        self.raw_inventory_by_uid = {v['metadata']['uid']: v for v in raw}
        return [project_resource(v) for v in raw]

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

        workspace = observe(workspace_path, lambda v: v is not None, 180)
        if workspace is None:
            raise ValueError('workspace-probe-setup-incomplete')
        self.create({'apiVersion': 'v1', 'kind': 'Namespace',
                     'metadata': {'name': namespace, 'labels': {'kubesphere.io/workspace': template}}})
        member = observe(namespace_path, lambda v: SYSTEM_WORKSPACE_FINALIZER in v['metadata'].get('finalizers', []), 45)
        before = self.native_read(template_path)
        if before is None or workspace is None or member is None:
            raise ValueError('workspace-probe-setup-incomplete')
        projections = {'template': project_resource(before), 'workspace': project_resource(workspace), 'member': project_resource(member)}
        request = self.native_retire('workspace-probe', template_path, projections['template'])
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
        if template_after is not None or workspace_after is not None:
            raise ValueError('workspace-probe-parent-still-present')
        self.cleanup_workspace_probe_quota(before)
        return record

    def cleanup_workspace_probe_quota(self, template):
        """Remove only the synthetic probe's exact UID-derived quota Secret before baseline/snapshot."""
        meta = template.get('metadata', {})
        template_uid = meta.get('uid')
        if ((template.get('apiVersion'), template.get('kind'), meta.get('name'), meta.get('namespace'))
                != ('tenant.kubesphere.io/v1beta1', 'WorkspaceTemplate', 's426-probe', None)
                or not isinstance(template_uid, str) or re.fullmatch(r'[a-z0-9-]+', template_uid) is None):
            raise ValueError('workspace-probe-template-identity-invalid')
        parent_paths = ['/apis/tenant.kubesphere.io/v1beta1/workspacetemplates/s426-probe',
                        '/apis/tenant.kubesphere.io/v1beta1/workspaces/s426-probe']
        name = 'io.kubesphere.license.quota.v3.workspace.' + template_uid
        path = '/api/v1/namespaces/kubesphere-system/secrets/' + name
        record = {'scope': 'exact synthetic workspace-probe quota Secret only, before fixture baseline/snapshot',
                  'templateUid': template_uid, 'namespace': 'kubesphere-system', 'name': name,
                  'request': None, 'absenceSamples': [], 'state': 'failed-closed', 'productionAccepted': False}
        self.last_step = 'workspace-probe-quota-cleanup'
        try:
            record['parentsAbsentBeforeCapture'] = [self.native_read(parent) is None for parent in parent_paths]
            if not all(record['parentsAbsentBeforeCapture']):
                raise ValueError('workspace-probe-parent-still-present')
            current = self.native_read(path)
            if current is not None:
                secret_meta = current.get('metadata', {})
                if ((current.get('apiVersion'), current.get('kind'), secret_meta.get('namespace'), secret_meta.get('name'))
                        != ('v1', 'Secret', 'kubesphere-system', name)
                        or not isinstance(secret_meta.get('labels'), dict)
                        or secret_meta['labels'].get('kubesphere.io/workspace') != 's426-probe'
                        or any(not isinstance(secret_meta.get(k), str) or not secret_meta[k] for k in ('uid', 'resourceVersion'))
                        or secret_meta.get('ownerReferences') or secret_meta.get('finalizers')
                        or secret_meta.get('deletionTimestamp') or 'deletionGracePeriodSeconds' in secret_meta):
                    raise ValueError('workspace-probe-quota-identity-invalid')
                self.attempt.encrypt('workspace-probe-quota-reviewed-content', canonical(current), self.args.age, self.args.recipient)
                uid = secret_meta['uid']
                record.update({'uid': uid, 'reviewedResourceVersion': secret_meta['resourceVersion']})
                record['parentsAbsentBeforeRequest'] = [self.native_read(parent) is None for parent in parent_paths]
                if not all(record['parentsAbsentBeforeRequest']):
                    raise ValueError('workspace-probe-parent-recreated-before-quota-delete')
                previous_digest = self.reviewed_content_digests.get(uid)
                self.reviewed_content_digests[uid] = content_review_digest(current)
                try:
                    record['request'] = self.native_retire('workspace-probe-quota', path, project_resource(current))
                finally:
                    if previous_digest is None:
                        self.reviewed_content_digests.pop(uid, None)
                    else:
                        self.reviewed_content_digests[uid] = previous_digest
            # Two separated native samples must show both parents and the exact residue absent.
            # Any lingering or recreated object stops; no wildcard cleanup or baseline exemption.
            for _ in range(2):
                time.sleep(2)
                absent = [self.native_read(candidate) is None for candidate in [*parent_paths, path]]
                record['absenceSamples'].append(dict(zip(('templateAbsent', 'workspaceAbsent', 'quotaAbsent'), absent)))
                if not all(absent):
                    raise ValueError('workspace-probe-quota-cleanup-not-stably-absent')
            record['state'] = 'passed-synthetic-only-cleanup'
            return record
        finally:
            self.attempt.record('workspace-probe-quota-cleanup.json', record)

    def post_retirement_checks(self, final, crds):
        """Native API/admission/namespace lifecycle must work without any KubeSphere component."""
        services = {(v['namespace'], v['name']) for v in final if (v['apiVersion'], v['kind']) == ('v1', 'Service')}
        stale = [label(key(v)) for v in final if v['kind'] in ('ValidatingWebhookConfiguration', 'MutatingWebhookConfiguration')
                 for hook in v['webhooks'] if hook['service']['name']
                 and (hook['service']['namespace'], hook['service']['name']) not in services]
        stale += [label(key(v)) for v in final if v['kind'] == 'APIService' and v['service']['name']
                  and (v['service']['namespace'], v['service']['name']) not in services]
        stale += [label(key(v)) for v in crds if v['customResource'].get('conversionService', {}).get('name')
                  and (v['customResource']['conversionService']['namespace'],
                       v['customResource']['conversionService']['name']) not in services]
        # A route still pointing at a retired Service is a stale entry point, public or private.
        routes = sorted(f'{label(key(v))} -> {namespace or "-"}/{name}' for v in final
                        for namespace, name in route_service_backends(v) if (namespace, name) not in services)
        runtime = [label(key(v)) for v in final if v['namespace'] in MANAGER_NAMESPACES
                   and v['kind'] in ('Deployment', 'ReplicaSet', 'StatefulSet', 'DaemonSet', 'CronJob')]
        completed_jobs = []
        for job in (v for v in final if v['namespace'] in MANAGER_NAMESPACES and v['kind'] == 'Job'):
            current = self.get('jobs.batch', job['name'], job['namespace'])
            conditions = current.get('status', {}).get('conditions', [])
            terminal = (current.get('metadata', {}).get('uid') == job['uid']
                        and current.get('status', {}).get('active', 0) == 0
                        and any(c.get('type') in ('Complete', 'Failed') and c.get('status') == 'True'
                                for c in conditions))
            (completed_jobs if terminal else runtime).append(label(key(job)))
        completed = []
        for pod in self.get('pods')['items']:
            if pod['metadata'].get('namespace') in MANAGER_NAMESPACES:
                entry = label(key(project_resource(pod)))
                # A finished controller Job pod is inert residue for a named decision; anything else is live runtime.
                (completed if pod.get('status', {}).get('phase') in ('Succeeded', 'Failed') else runtime).append(entry)
        workload = 's426-workload'
        self.create({'apiVersion': 'v1', 'kind': 'ConfigMap', 'metadata': {'name': 's426-post-retirement', 'namespace': workload},
                     'data': {'synthetic': 'post-retirement-native-write'}})
        config_path = f'/api/v1/namespaces/{workload}/configmaps/s426-post-retirement'
        written = self.native_read(config_path)
        write = self.native_retire('post-retirement-health', config_path, project_resource(written))
        config_deadline, config_gone = time.monotonic() + 30, False
        while not config_gone and time.monotonic() < config_deadline:
            config_gone = self.native_read(config_path) is None
            if not config_gone:
                time.sleep(2)
        self.create({'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {'name': 's426-post-retirement'}})
        time.sleep(15)
        created = self.native_read('/api/v1/namespaces/s426-post-retirement')
        managed = project_resource(created)
        lifecycle = self.native_retire('post-retirement-namespace', '/api/v1/namespaces/s426-post-retirement', managed)
        deadline, gone = time.monotonic() + 120, False
        while not gone and time.monotonic() < deadline:
            gone = self.native_read('/api/v1/namespaces/s426-post-retirement') is None
            if not gone:
                time.sleep(5)
        record = {'staleAdmissionOrApiServiceReferences': stale, 'staleRouteBackends': routes, 'managerRuntimeRemaining': runtime,
                  'completedManagerPodResidue': completed, 'completedManagerJobResidue': completed_jobs,
                  'nativeConfigMapWriteDelete': write, 'nativeConfigMapDeletionCompleted': config_gone,
                  'newNamespaceManagementLabels': managed['managementLabels'],
                  'newNamespaceFinalizers': managed['finalizers'], 'namespaceLifecycle': lifecycle,
                  'newNamespaceDeletionCompleted': gone, 'scope': 'fresh fixture only', 'productionAccepted': False}
        self.attempt.record('post-retirement-native-health.json', record)
        if stale or routes or runtime or not config_gone or managed['managementLabels'] or managed['finalizers'] or not gone:
            raise ValueError('post-retirement-native-health-failed')
        return record

    def retire_kubesphere(self):
        """Dependency-first native retirement: controller lifecycles first, exact native removal, named finalizers last."""
        self.probe_workspace_propagation()
        self.populate_retirement_decisions()
        # Earlier diagnostics may have cached the served KubeSphere resources before later CRDs existed.
        self.custom_resources = None
        baseline_crds = self.crd_inventory()
        before, settled = self.settled_inventory()
        if not settled:
            raise ValueError('fixture-state-did-not-settle-before-retirement')
        self.reviewed_content_digests = {uid: content_review_digest(raw)
                                        for uid, raw in self.raw_inventory_by_uid.items()}
        self.reviewed_raw_by_uid = copy.deepcopy(self.raw_inventory_by_uid)
        if self.reviewed_content_digests:
            self.attempt.encrypt('retirement-reviewed-content-digests', canonical({
                'scope': 'fixture-only', 'digests': self.reviewed_content_digests,
                'excludedFields': ['status', 'metadata.resourceVersion', 'metadata.managedFields', 'metadata.generation']}),
                self.args.age, self.args.recipient)
        self.attempt.record('kubesphere-before-state.json', {'resources': before, 'crds': baseline_crds, 'scope': 'fixture-only'})
        if getattr(self.args, 'rollback', False):
            self.capture_rollback(before, baseline_crds)
        by_uid = {v['uid']: v for v in before}
        scope = retirement_scope(before)
        self.retirement_uids = set(scope)
        plan = dependency_plan(before, scope)
        actions = [{**{k: by_uid[uid][k] for k in ('apiVersion', 'kind', 'namespace', 'name', 'uid', 'resourceVersion')},
                    'action': 'delete', 'propagation': 'Foreground', 'phase': phase['phase'], 'order': phase['order'],
                    'request': ('native-named-finalizer' if phase['mode'] == 'named-finalizer' else 'native-uid-resource-version-delete')
                    if uid in phase['roots'] else ('controller-lifecycle' if phase['mode'] == 'controller-lifecycle' else 'owner-or-endpoints-propagation')}
                   for phase in plan for uid in phase['expected']]
        actions += [{**{k: by_uid[uid][k] for k in ('apiVersion', 'kind', 'namespace', 'name', 'uid', 'resourceVersion')},
                     'action': 'remove-named-finalizer', 'finalizer': phase['namedFinalizer'], 'propagation': 'Foreground',
                     'phase': phase['phase'], 'order': phase['order'], 'request': 'native-namespace-named-finalizer'}
                    for phase in plan for uid in phase.get('expectedModified', [])]
        validate_allowlist(before, [{k: v for k, v in a.items() if k not in ('phase', 'order', 'request')} for a in actions])
        allowlist = {'actions': actions, 'phases': plan, 'chartSha256': self.args.ks_chart_sha256,
                     'sourceInventorySha256': self.args.source_digest, 'procedureSha256': self.args.script_digest,
                     'expectedControllerContentTransitions': [{'phase': 'global-role-bindings',
                         'field': 'metadata.annotations.iam.kubesphere.io/globalrole', 'change': 'clear-to-empty-string',
                         'condition': 'exact removed GlobalRoleBinding subject and role match an allowlisted User; all other raw desired fields unchanged'},
                        {'phase': 'users', 'field': 'metadata.annotations.iam.kubesphere.io/granted-clusters',
                         'change': 'clear-exact-host-grant-to-empty-string',
                         'condition': 'matching reviewed User and IAM ClusterRoleBinding UIDs in retirement scope; exact subject, cluster-admin role and labels; cluster binding phase complete and all matching bindings absent; bounded native reads establish stable empty value with key retained; every other raw desired field unchanged'},
                        {'phase': 'console-route', 'field': 'spec.renewTime',
                         'change': 'capture-stable-final-renewal-after-confirmed-controller-removal',
                         'condition': 'exact reviewed leader Lease UID/name/namespace in retirement scope; controllers-and-services phase complete, all manager workloads/Pods absent, two native reads stable and time monotonic; every other raw desired field unchanged'},
                        {'phase': 'remaining-release-objects', 'field': 'metadata.annotations.kubesphere.io/count',
                         'change': 'capture-zero-after-reviewed-extension-membership-retirement',
                         'condition': 'exact reviewed extension Category UID/name in retirement scope; reviewed count equals exact retired Extension membership; catalog and controller phases complete, no manager workloads/Pods or Extensions remain, two native reads stable at zero; every other raw desired field unchanged'}],
                     'procedure': 'dependency-first native retirement; exact console residue and named namespace metadata finalizers; no Helm uninstall/hooks, wildcard, blanket finalizer or namespace/PVC deletion',
                     'namedFinalizerInterventions': [label(key(by_uid[uid])) + ':' + SYSTEM_WORKSPACE_FINALIZER
                                                     for phase in plan if phase['mode'] == 'named-finalizer' for uid in phase['roots']],
                     'scope': 'fixture-only', 'productionAccepted': False}
        self.attempt.record('kubesphere-retirement-allowlist.json', allowlist)
        resources = self.discover({v['apiVersion'] for v in actions})
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
        namespace_modifications = {key(by_uid[uid]): phase['namedFinalizer'] for phase in plan
                                   for uid in phase.get('expectedModified', [])}
        assert_phase(baseline, before, final, scope, namespace_modifications)
        # Retained CRDs keep identity, served/storage versions and conversion; status-driven resourceVersion churn is recorded.
        crd_identity = lambda items: sorted(canonical({k: v[k] for k in ('uid', 'name', 'customResource')}) for v in items)
        if crd_identity(baseline_crds) != crd_identity(final_crds):
            raise ValueError('retained-crd-changed')
        crd_versions_changed = sorted(v['name'] for v in final_crds
                                      if (v['uid'], v['resourceVersion']) not in {(x['uid'], x['resourceVersion']) for x in baseline_crds})
        health = self.post_retirement_checks(final, final_crds)
        retained = [{'object': label(key(v)), 'finalizers': v['finalizers'], 'managementLabels': v['managementLabels'],
                     'disposition': ('protected; KubeSphere label/finalizer residual retained pending a named decision'
                                     if v['kind'] in PROTECTED_KINDS else 'retained inert instance pending owner decision')}
                    for v in final if management_resource(v) and v['kind'] != 'CustomResourceDefinition']
        self.attempt.record('kubesphere-retirement-result.json', {'state': 'passed-dependency-first-native-retirement',
                    'allowlistSha256': digest(canonical(allowlist)), 'procedureSha256': self.args.script_digest,
                    'phases': [p['phase'] for p in plan], 'exactBaselineRemovals': len(delta['expectedRemovals']),
                    'licensedApplicationWritesUsed': False, 'helmUninstallOrVendorHooksUsed': False,
                    'blanketFinalizerRemovalUsed': False, 'namespacePvcPvIdentitiesAndBindingsPreserved': True,
                    'retainedCrds': len(final_crds), 'retainedCrdResourceVersionChanges': crd_versions_changed,
                    'retainedKubeSphereMarkedObjects': retained,
                    'postRetirementHealth': health, 'productionAccepted': False})
        if getattr(self.args, 'rollback', False):
            self.restore_rollback()

    def copy_etcd_tools(self):
        for name in ('etcdctl', 'etcdutl'):
            self.run('copy-' + name, ['docker', 'cp', str(getattr(self.args, name)), self.node + ':/usr/local/bin/' + name + '426'])

    def etcd_command(self, name, *argv):
        return self.run(name, ['docker', 'exec', self.node, '/usr/local/bin/etcdctl426',
                 '--endpoints=https://127.0.0.1:2379', '--cacert=/etc/kubernetes/pki/etcd/ca.crt',
                 '--cert=/etc/kubernetes/pki/etcd/healthcheck-client.crt', '--key=/etc/kubernetes/pki/etcd/healthcheck-client.key',
                 '--dial-timeout=3s', '--command-timeout=30s', *argv], timeout=60)

    def capture_rollback(self, before, crds):
        """Snapshot and fresh node inputs belong solely to this synthetic fixture; plaintext stays in memory or its owned node."""
        self.last_step = 'rollback-snapshot'
        self.copy_etcd_tools()
        self.etcd_command('rollback-snapshot-save', 'snapshot', 'save', '/var/lib/s426-rollback.db')
        status = json.loads(self.run('rollback-snapshot-status', ['docker', 'exec', self.node,
            '/usr/local/bin/etcdutl426', 'snapshot', 'status', '/var/lib/s426-rollback.db', '-w', 'json']).stdout)
        self.rollback_snapshot = self.run('rollback-snapshot-export', ['docker', 'exec', self.node, 'cat', '/var/lib/s426-rollback.db']).stdout
        self.attempt.encrypt('rollback-snapshot', self.rollback_snapshot, self.args.age, self.args.recipient)
        self.rollback_archive = self.run('rollback-node-archive', ['docker', 'exec', self.node, 'tar', '-C', '/', '-czf', '-',
            'etc/kubernetes', 'var/lib/kubelet/pki', 'var/lib/kubelet/config.yaml', 'var/lib/kubelet/kubeadm-flags.env',
            'var/lib/kubelet/instance-config.yaml']).stdout
        self.attempt.encrypt('rollback-node-inputs', self.rollback_archive, self.args.age, self.args.recipient)
        self.rollback_baseline, self.rollback_crds = before, crds
        self.rollback_status = status
        self.rollback_datastore = json.loads(self.etcd_command('rollback-source-etcd-status', 'endpoint', 'status', '-w', 'json').stdout)[0]['Status']
        self.rollback_canary = self.run('rollback-data-input', ['docker', 'exec', self.node, 'cat', '/var/local/s426-synthetic/canary']).stdout
        self.attempt.record('rollback-snapshot.json', {'capturedAt': now(), 'scope': 'fresh synthetic fixture only',
            'snapshotStatus': status, 'exports': ['rollback-snapshot.age', 'rollback-node-inputs.age'],
            'baselineIdentities': len(before), 'baselineCrds': len(crds), 'freshFixtureCredentialsOnly': True,
            'productionDataOrCredentialsImported': False, 'productionRecoveryAccepted': False,
            'toolSha256': {name: file_digest(getattr(self.args, name)) for name in ('etcdctl', 'etcdutl')},
            'source': 'https://etcd.io/docs/v3.6/op-guide/recovery/'})

    def restore_rollback(self):
        """Replace the retired owned node with a fresh node, restore its own snapshot, and prove baseline identities/runtime."""
        self.last_step = 'rollback-retired-node-cleanup'
        if not self.cleanup(record_name='retired-node-cleanup.json'):
            raise ValueError('rollback-retired-node-cleanup-failed')
        old_container = self.container_id
        # Same synthetic node/network names and subnet preserve node and serving identities; Docker's new ID proves a fresh node.
        target = Fixture(self.args, ScopedAttempt(self.attempt, 'rollback'))
        target.network_config = self.network_config
        self.rollback_fixture = target
        target.start()
        if target.container_id == old_container or target.node_ip != self.node_ip:
            raise ValueError('rollback-fresh-node-identity-unverified')
        target.load_vendor_images(self.vendor_images)
        target.copy_etcd_tools()
        target.run('stop-fresh-kubelet', ['docker', 'exec', target.node, 'systemctl', 'stop', 'kubelet'])
        pods = target.run('fresh-pod-sandboxes', ['docker', 'exec', target.node, 'crictl', 'pods', '-q']).stdout.decode().splitlines()
        if not pods or any(not re.fullmatch(r'[0-9a-f]{12,64}', pod) for pod in pods):
            raise ValueError('rollback-fresh-pod-identities-invalid')
        for pod in pods:
            target.run('stop-fresh-pod', ['docker', 'exec', target.node, 'crictl', '--timeout', '30s', 'stopp', pod])
        target.run('remove-fresh-node-inputs', ['docker', 'exec', target.node, 'rm', '-rf',
            '/etc/kubernetes', '/var/lib/kubelet/pki', '/var/lib/etcd'])
        target.run('restore-fixture-node-inputs', ['docker', 'exec', '-i', target.node, 'tar', '-C', '/', '-xzf', '-'], input=self.rollback_archive)
        target.run('restore-snapshot-input', ['docker', 'exec', '-i', target.node, 'tee', '/var/lib/s426-rollback.db'], input=self.rollback_snapshot)
        peer = 'https://' + target.node_ip + ':2380'
        target.run('etcd-snapshot-restore', ['docker', 'exec', target.node, '/usr/local/bin/etcdutl426', 'snapshot', 'restore',
            '/var/lib/s426-rollback.db', '--data-dir=/var/lib/etcd', '--name=' + target.node,
            '--initial-cluster=' + target.node + '=' + peer, '--initial-advertise-peer-urls=' + peer,
            '--initial-cluster-token=' + target.cluster + '-rollback', '--bump-revision=1000000000', '--mark-compacted'])
        target.run('restore-synthetic-hostpath', ['docker', 'exec', target.node, 'mkdir', '-p', '/var/local/s426-synthetic'])
        target.run('restore-synthetic-canary', ['docker', 'exec', '-i', target.node, 'tee', '/var/local/s426-synthetic/canary'], input=self.rollback_canary)
        canary = target.run('restored-synthetic-canary-readback', ['docker', 'exec', target.node, 'cat',
                           '/var/local/s426-synthetic/canary'], allowed=(0, 1))
        canary_matches = canary.returncode == 0 and canary.stdout == self.rollback_canary
        self.attempt.record('rollback-canary-readback.json', {'scope': 'fresh synthetic fixture only',
            'passed': canary_matches, 'readbackExitCode': canary.returncode,
            'expectedSha256': digest(self.rollback_canary), 'readbackSha256': digest(canary.stdout),
            'exactCapturedVolumeBytesRestored': canary_matches, 'productionRecoveryAccepted': False})
        if not canary_matches:
            raise ValueError('rollback-synthetic-canary-readback-mismatch')
        target.run('start-restored-kubelet', ['docker', 'exec', target.node, 'systemctl', 'start', 'kubelet'])
        deadline = time.monotonic() + 180
        while True:
            ready = target.kube('restored-api-ready', 'get', '--raw', '/readyz', allowed=(0, 1))
            if ready.returncode == 0 and ready.stdout.strip() == b'ok':
                break
            if time.monotonic() > deadline:
                raise ValueError('rollback-api-not-ready')
            time.sleep(5)
        # A snapshot's stale Ready status alone cannot prove runtime: also require running containers on the fresh node.
        expected = {'ks-apiserver', 'ks-controller-manager', 'ks-console', 'extensions-museum', 'ks-console-embed'}
        deadline = time.monotonic() + 240
        while True:
            containers = json.loads(target.run('restored-live-containers', ['docker', 'exec', target.node, 'crictl', 'ps', '-o', 'json']).stdout)
            running = {v['metadata']['name'] for v in containers['containers'] if v['state'] == 'CONTAINER_RUNNING'}
            if expected <= running:
                break
            if time.monotonic() > deadline:
                raise ValueError('rollback-kubesphere-runtime-not-running')
            time.sleep(5)
        for name in sorted(expected):
            target.kube('restored-runtime-ready', 'rollout', 'status', 'deployment/' + name, '-n', 'kubesphere-system', '--timeout=90s')
        current = target.kubesphere_inventory()
        restored = {key(v): v for v in current}
        mismatched = [label(key(v)) for v in self.rollback_baseline if key(v) not in restored or restored[key(v)]['uid'] != v['uid']]
        target_crds = target.crd_inventory()
        crd_mismatched = [v['name'] for v in self.rollback_crds
                         if (v['name'], v['uid']) not in {(o['name'], o['uid']) for o in target_crds}]
        datastore = json.loads(target.etcd_command('restored-etcd-status', 'endpoint', 'status', '-w', 'json').stdout)[0]['Status']
        fresh_member = (datastore['header']['cluster_id'] != self.rollback_datastore['header']['cluster_id']
                        and datastore['header']['member_id'] != self.rollback_datastore['header']['member_id'])
        self.attempt.record('rollback-restore-result.json', {'state': 'passed' if not mismatched and not crd_mismatched and fresh_member else 'failed',
            'scope': 'fresh synthetic fixture only', 'sourceNodeDestroyedBeforeRestore': True, 'freshDockerNodeVerified': True,
            'baselineIdentityCount': len(self.rollback_baseline), 'baselineUidMismatches': mismatched, 'crdUidMismatches': crd_mismatched,
            'runningKubeSphereContainers': sorted(expected & running), 'readyKubeSphereDeployments': sorted(expected),
            'freshEtcdClusterAndMemberIdentities': fresh_member, 'snapshotRevision': self.rollback_status['revision'],
            'syntheticCanaryRestoredAndReadbackEqual': canary_matches,
            'syntheticCanaryReadbackRecord': 'rollback-canary-readback.json',
            'revisionBump': 1000000000, 'markCompacted': True, 'skipHashCheck': False,
            'productionCredentialsOrDataImported': False, 'productionRecoveryAccepted': False})
        if mismatched or crd_mismatched or not fresh_member:
            raise ValueError('rollback-baseline-or-datastore-identity-mismatch')

    def populate_retirement_decisions(self):
        """Synthetic console certificate chain; seven namespace equivalents use the manager-free driver."""
        for group, plural, kind in (('cert-manager.io', 'certificates', 'Certificate'),
                                     ('cert-manager.io', 'certificaterequests', 'CertificateRequest'),
                                     ('acme.cert-manager.io', 'orders', 'Order')):
            self.create({'apiVersion': 'apiextensions.k8s.io/v1', 'kind': 'CustomResourceDefinition',
                         'metadata': {'name': plural + '.' + group}, 'spec': {'group': group,
                         'names': {'plural': plural, 'singular': plural.lower(), 'kind': kind}, 'scope': 'Namespaced',
                         'versions': [{'name': 'v1', 'served': True, 'storage': True,
                                       'schema': {'openAPIV3Schema': {'type': 'object', 'x-kubernetes-preserve-unknown-fields': True}}}]}})
            self.kube('synthetic-certificate-api-ready', 'wait', 'crd/' + plural + '.' + group,
                      '--for=condition=Established', '--timeout=30s')
        namespace = 'kubesphere-system'
        self.create({'apiVersion': 'cert-manager.io/v1', 'kind': 'Certificate', 'metadata': {
            'name': 'kubesphere-console-letsencrypt', 'namespace': namespace},
            'spec': {'secretName': 'kubesphere-console-letsencrypt-tls', 'dnsNames': ['fixture.invalid']}})
        certificate = self.get('certificates.cert-manager.io', 'kubesphere-console-letsencrypt', namespace)
        owner = lambda obj: {'apiVersion': obj['apiVersion'], 'kind': obj['kind'], 'name': obj['metadata']['name'],
                             'uid': obj['metadata']['uid'], 'controller': True, 'blockOwnerDeletion': True}
        self.create({'apiVersion': 'cert-manager.io/v1', 'kind': 'CertificateRequest', 'metadata': {
            'name': 'kubesphere-console-letsencrypt-1', 'namespace': namespace, 'ownerReferences': [owner(certificate)]}})
        request = self.get('certificaterequests.cert-manager.io', 'kubesphere-console-letsencrypt-1', namespace)
        self.create({'apiVersion': 'acme.cert-manager.io/v1', 'kind': 'Order', 'metadata': {
            'name': 'kubesphere-console-letsencrypt-1-synthetic', 'namespace': namespace, 'ownerReferences': [owner(request)]}})
        self.create({'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {
            'name': 'kubesphere-console-letsencrypt-tls', 'namespace': namespace},
            'type': 'Opaque', 'stringData': {'synthetic': 'fixture-only-certificate-placeholder'}})
        self.create({'apiVersion': 'networking.k8s.io/v1', 'kind': 'Ingress', 'metadata': {
            'name': 'kubesphere-console', 'namespace': namespace}, 'spec': {
            'rules': [{'host': 'fixture.invalid', 'http': {'paths': [{'path': '/', 'pathType': 'Prefix',
                       'backend': {'service': {'name': 'ks-console', 'port': {'number': 80}}}}]}}],
            'tls': [{'hosts': ['fixture.invalid'], 'secretName': 'kubesphere-console-letsencrypt-tls'}]}})
        self.attempt.record('retirement-decision-fixtures.json', {
            'consoleRoute': 'fixture.invalid, no ingress controller/public route',
            'certificateController': 'synthetic owner-reference chain; real cert-manager reconciliation untested',
            'namespaceFinalizerCoverage': 'separate manager-free rehearse_namespaces.py driver', 'sourceDataImported': False,
            'productionPrerequisite': 'Story 4.2 closes kube.hexalith.com before retirement', 'productionAccepted': False})

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
        if not synthetic_hold_blocked(blocked):
            raise ValueError('finalizer-behavior-unexpected')
        named = {**project_resource(blocked), 'action': 'remove-named-finalizer', 'propagation': 'Foreground',
                 'finalizer': 'qualification.hexalith.io/hold'}
        validate_allowlist(self.inventory(), [named])
        self.attempt.record('named-finalizer-phase.json', {'action': named, 'scope': 'fixture-only synthetic finalizer'})
        # A specific UID/resourceVersion-bound replace; never blanket stripping of a production finalizer.
        blocked['metadata']['finalizers'] = [v for v in blocked['metadata']['finalizers'] if v != SYNTHETIC_HOLD]
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

    def cleanup(self, record_name='cleanup.json'):
        if self.rollback_fixture is not None:
            passed = self.rollback_fixture.cleanup()
            self.attempt.record(record_name, {'passed': passed, 'scope': 'fresh rollback node',
                'retiredNodeCleanup': 'retired-node-cleanup.json', 'details': 'rollback-cleanup.json'})
            return passed
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

        outcomes['entrypointReaderName'] = self.entrypoint_reader
        outcomes['entrypointReaderStarted'] = self.entrypoint_reader_started
        if self.entrypoint_reader_started:
            _, result = command(['docker', 'container', 'rm', '--force', '--volumes', self.entrypoint_reader], 30)
            outcomes['entrypointReaderDeleteExit'] = result['exitCode']
            outcomes['entrypointReaderDeleteState'] = result['state']
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
                       'entrypointReader': inspect(['docker', 'inspect', self.entrypoint_reader], 'container', self.entrypoint_reader),
                       'network': inspect(['docker', 'network', 'inspect', self.network], 'network', self.network)}
        outcomes['inspectionStates'] = inspections
        outcomes['nodeAbsent'] = inspections['node'] == 'absent'
        outcomes['entrypointReaderAbsent'] = inspections['entrypointReader'] == 'absent'
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
        outcomes['unmeasuredDeclarations'] = {'globalPruneUsed': False, 'unrelatedDockerObjectsChanged': False,
                                              'basis': 'procedure issues only exact named removals; not measured'}
        outcomes['passed'] = (outcomes['nodeAbsent'] and outcomes['entrypointReaderAbsent']
                              and outcomes['networkAbsent'] and outcomes['freshCredentialFileAbsent']
                              and outcomes['fixtureVolumesAbsent'] and outcomes['fixtureImageTagsAbsent'])
        self.attempt.record(record_name, outcomes)
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
        except Exception:  # diagnostics never replace the original failure
            pass


def rehearse(args, fixture_type=None, driver_path=None):
    args.script_digest = file_digest(Path(__file__))
    # Imported helpers (including project_resource) are part of the executed procedure.
    args.module_digests = {name: file_digest(Path(__file__).with_name(name)) for name in ('evidence.py', 'qualify.py')}
    executor = getattr(args, 'production_executor', None)
    executor_binding = {'productionExecutorSha256': file_digest(executor),
                        'productionProcedureSha256': file_digest(Path(executor).with_name('RETIRE-KUBESPHERE.md'))} if executor else {}
    source_bytes = args.source_inventory.read_bytes()
    args.source_digest = digest(source_bytes)
    args.source = json.loads(source_bytes)
    if (not isinstance(args.source, dict)
            or any(not isinstance(args.source.get(field), str) or not args.source[field]
                   for field in ('sourceClusterUid', 'nativeEndpoint'))):
        raise ValueError('source-identity-missing')
    attempt = Attempt(args.project_root, args.evidence_root, args.attempt_id, 'rehearsal',
                      readback_recipient=getattr(args, 'readback_recipient', None),
                      readback_identity=getattr(args, 'readback_identity', None))
    driver_binding = {'driverSha256': file_digest(driver_path)} if driver_path else {}
    attempt.record('attempt.json', {'attemptId': args.attempt_id, 'recordedAt': now(), 'operator': safe(args.operator),
                    'scriptSha256': args.script_digest, 'moduleSha256': args.module_digests,
                    **executor_binding,
                    **driver_binding,
                    'sourceInventorySha256': args.source_digest, 'scope': 'isolated synthetic native operations only',
                    'mutationAuthorized': False, 'signed': False, 'productionAccepted': False})
    fixture, state = (fixture_type or Fixture)(args, attempt), 'failed-closed'
    try:
        if driver_path:
            attempt.encrypt('executed-driver', Path(driver_path).read_bytes(), args.age, args.recipient)
        identities = tool_identities(args)
        attempt.record('tools.json', identities)
        if any(v['exitCode'] != 0 for v in identities['runtimeVersionOutput'].values()):
            raise ValueError('runtime-version-preflight-failed')
        fixture.start()
        fixture.execute()
        state = 'passed-limited'
    except Exception as error:  # any parser/import/tooling error must still leave a receipt and cleanup
        failure_step = fixture.last_step
        fixture.diagnostics()
        attempt.record('failure.json', {'state': 'failed-closed', 'failureStep': failure_step,
                                     'reasonCode': (str(error) if isinstance(error, ValueError)
                                                    and re.fullmatch(r'[a-z0-9-]+', str(error)) else 'command-or-custody-error'),
                                     'reason': 'isolation, fixture command, drift or preservation failed',
                                     'diagnostics': 'encrypted exports',
                                     'sourceUnchanged': 'unmeasured declaration; the fixture holds no source credentials',
                                     'productionAccepted': False})
    finally:
        try:
            if not fixture.cleanup():
                state = 'failed-cleanup'
        except Exception:
            state = 'failed-cleanup'
        attempt.record('summary.json', {'state': state, 'productionRetirementAccepted': False,
                       'qualificationAccepted': False, 'licensedKubeSphereWritesUsed': False})
        attempt.finish()
    return attempt.directory, state


def parse_args(argv=None):
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--project-root', type=Path, default=Path(__file__).resolve().parents[2])
    p.add_argument('--evidence-root', type=Path, default=Path.home() / 'hexalith-management-evidence')
    p.add_argument('--attempt-id', default=datetime.now(timezone.utc).strftime('%Y%m%dt%H%M%Sz-rehearsal'))
    p.add_argument('--operator', required=True)
    p.add_argument('--source-inventory', type=Path, required=True, help='Sanitized census only; no source kubeconfig is accepted')
    p.add_argument('--production-executor', type=Path,
                   help='Bind the exact separate production executor bytes in renewed 4.27 fixture receipts')
    p.add_argument('--node-image', required=True, help='Existing local Docker sha256:<ID>; no unqualified pulls')
    p.add_argument('--kubectl', type=Path, required=True,
                   help='Not executed: digest-recorded only; fixture commands use the new node\'s own kubectl')
    p.add_argument('--age', type=Path, required=True)
    p.add_argument('--recipient', required=True, help='Administrator-owned ssh-ed25519 public recipient; no private key')
    p.add_argument('--readback-recipient', required=True, help='Distinct agent-held ssh-ed25519 public recipient')
    p.add_argument('--readback-identity', type=Path, required=True, help='Owner-only readback key outside Git and evidence custody')
    p.add_argument('--ks-chart', type=Path, help='Public retained ks-core 1.2.4 archive; enables the actual-chart retirement fixture')
    p.add_argument('--ks-chart-sha256', help='Independently checked exact public chart SHA-256')
    p.add_argument('--helm', type=Path, help='Retained fixture Helm 3 binary, required with --ks-chart')
    p.add_argument('--rollback', action='store_true', help='Snapshot before retirement and restore onto a fresh isolated fixture node')
    p.add_argument('--etcdctl', type=Path, help='Retained matching etcdctl for the fixture rollback')
    p.add_argument('--etcdutl', type=Path, help='Retained matching etcdutl for the fixture rollback')
    args = p.parse_args(argv)
    if not re.fullmatch(r'sha256:[0-9a-f]{64}', args.node_image):
        p.exit(2, 'Rehearsal refused: exact local Docker image ID required.\n')
    if args.ks_chart and (not args.helm or not args.ks_chart_sha256
            or not re.fullmatch(r'[0-9a-f]{64}', args.ks_chart_sha256)):
        p.exit(2, 'Rehearsal refused: actual core fixture requires chart SHA-256 and retained Helm binary.\n')
    if args.rollback and (not args.ks_chart or not args.etcdctl or not args.etcdutl
                          or not args.etcdctl.is_file() or not args.etcdutl.is_file()):
        p.exit(2, 'Rehearsal refused: rollback requires the actual core fixture and retained etcd tools.\n')
    return args


def main():
    args = parse_args()
    try:
        directory, state = rehearse(args)
    except Exception:
        sys.stderr.write('Rehearsal refused: invalid source identity, tools or private custody.\n')
        raise SystemExit(2) from None
    print(f'Private fixture evidence: {directory}\nRehearsal: {state}; production retirement remains unaccepted')
    if state.startswith('failed'):
        raise SystemExit(2)


if __name__ == '__main__':
    main()
