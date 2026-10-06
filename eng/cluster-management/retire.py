#!/usr/bin/env python3
"""Plan or explicitly arm native KubeSphere retirement; every incomplete outcome closes 4.1."""
import argparse
import base64
import copy
from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import time

from evidence import Attempt, canonical, digest, file_digest, now, write_new
from qualify import Capture, PROTECTED_KINDS, management_resource, project_resource, safe
from rehearse import (PhaseExecutor, MANAGER_NAMESPACES, PRODUCTION_FINALIZER_NAMES,
                      SYSTEM_WORKSPACE_FINALIZER, admission_registration, api_path,
                      assert_phase, content_review_digest, dependency_plan, key, label,
                      retirement_scope, route_service_backends, validate_allowlist)

CODE_FILES = ('retire.py', 'rehearse.py', 'qualify.py', 'evidence.py',
              'rehearse_catalog.py', 'rehearse_namespaces.py')
WORKLOADS = ('keycloak', 'openbao', 'memories', 'forgejo')
MANAGER_RUNTIME_KINDS = {'Pod', 'Deployment', 'ReplicaSet', 'StatefulSet', 'DaemonSet', 'Job', 'CronJob'}
REVIEWED_CLUSTER_KINDS = {
    ('application.kubesphere.io/v2', kind) for kind in ('Repo', 'Application', 'ApplicationVersion')
} | {('kubesphere.io/v1alpha1', kind) for kind in ('InstallPlan', 'Extension', 'ExtensionVersion', 'Category', 'Repository')} \
  | {('iam.kubesphere.io/v1beta1', kind) for kind in ('RoleTemplate', 'Category', 'BuiltinRole', 'GlobalRole',
       'GlobalRoleBinding', 'WorkspaceRole', 'WorkspaceRoleBinding', 'ClusterRole', 'ClusterRoleBinding', 'User', 'LoginRecord')} \
  | {('tenant.kubesphere.io/v1beta1', kind) for kind in ('WorkspaceTemplate', 'Workspace')} \
  | {('extensions.kubesphere.io/v1alpha1', 'ReverseProxy'),
       ('rbac.authorization.k8s.io/v1', 'ClusterRole'), ('rbac.authorization.k8s.io/v1', 'ClusterRoleBinding'),
       ('admissionregistration.k8s.io/v1', 'MutatingWebhookConfiguration'),
       ('admissionregistration.k8s.io/v1', 'ValidatingWebhookConfiguration'), ('apiregistration.k8s.io/v1', 'APIService')}
AUTHORITY = {('iam.kubesphere.io/v1beta1', 'User', None, 'jpiquot'),
             ('iam.kubesphere.io/v1beta1', 'GlobalRoleBinding', None, 'jpiquot-platform-admin'),
             ('rbac.authorization.k8s.io/v1', 'ClusterRoleBinding', None, 'jpiquot-cluster-admin'),
             ('v1', 'Secret', 'kubesphere-system', 'kubeconfig-jpiquot'),
             ('iam.kubesphere.io/v1beta1', 'LoginRecord', None, 'jpiquot-dgjhn')}
BACKUP_FILES = ('policy/recovery-policy.json', 'manifest/evidence-manifest.json',
                'cleanup/restore-target-cleanup.json', 'gate/backup-gate.json',
                'keycloak/keycloak-restore-proof.json', 'openbao/openbao-restore-proof.json',
                'memories/memories-restore-proof.json', 'validation/validation.json')
GATE_FILES = ('recovery-validation.json', 'console-closure.json',
              'external-etcd-recovery-point.json', 'external-etcd-isolated-restore.json',
              'node-recovery-bundle.json', 'native-access.json', 'pre-retirement-health.json')
HEX = re.compile(r'[0-9a-f]{64}')
TOOL_NAMES = ('kubectl', 'helm', 'age', 'ssh_keygen')
LEASE_IDENTITY_FIELDS = ('apiVersion', 'kind', 'namespace', 'name', 'uid')


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def timestamp(value):
    require(isinstance(value, str), 'invalid-evidence-time')
    try:
        parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    except ValueError:
        raise ValueError('invalid-evidence-time') from None
    require(parsed.tzinfo is not None, 'invalid-evidence-time')
    return parsed


def fresh(record, maximum_age, *, captured='capturedAt', after=None):
    clock = datetime.now(timezone.utc)
    observed = timestamp(record.get(captured))
    require(type(maximum_age) is int and 0 < maximum_age <= 86400,
            'invalid-approved-freshness-policy')
    require(0 <= (clock - observed).total_seconds() <= maximum_age
            and clock < timestamp(record.get('expiresAt')), 'stale-or-future-evidence')
    if after:
        require(observed >= timestamp(after), 'evidence-predates-retirement')


def private_file(path, project):
    path = Path(path).absolute()
    require(not any(p.is_symlink() for p in (path, *path.parents)), 'symlink-in-private-input')
    require(path.is_file() and path.stat().st_uid == os.getuid()
            and not stat.S_IMODE(path.stat().st_mode) & 0o077
            and Path(project).resolve() not in path.resolve().parents, 'private-input-custody-invalid')
    return path


def resolved_configuration_sha(config):
    require(isinstance(config, dict) and len(config.get('users', [])) == 1
            and isinstance(config['users'][0].get('user'), dict), 'native-static-credential-unresolved')
    user = config['users'][0]['user']
    require(not any(v in user for v in ('exec', 'auth-provider', 'tokenFile')), 'unbound-native-credential-hook')
    return digest(canonical(config))


def code_binding(project):
    directory = Path(__file__).resolve().parent
    return {'codeSha256': {name: file_digest(directory / name) for name in CODE_FILES},
            'procedureSha256': file_digest(directory / 'RETIRE-KUBESPHERE.md'),
            'maintenanceSha256': file_digest(Path(project) / 'eng/kubernetes-upgrade/MAINTENANCE.md')}


def tool_binding(args):
    """Hash the exact absolute executable path that subprocess will invoke, never a PATH basename."""
    identities = {}
    for name in TOOL_NAMES:
        path = Path(getattr(args, name))
        require(path.is_absolute(), 'absolute-execution-tool-required')
        require(path.is_file() and os.access(path, os.X_OK), 'missing-execution-tool')
        identities[name] = file_digest(path)
    return identities


def lease_time(value):
    """Validate RFC3339 and compare all nine fractional digits without float or microsecond truncation."""
    match = re.fullmatch(r'(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(?:\.(\d{1,9}))?(Z|[+-]\d{2}:\d{2})',
                         value) if isinstance(value, str) else None
    require(match is not None, 'lease-renewal-invalid-time')
    require(match[3] == 'Z' or int(match[3][1:3]) <= 23 and int(match[3][4:6]) <= 59,
            'lease-renewal-invalid-time')
    try:
        parsed = datetime.fromisoformat(match[1] + match[3].replace('Z', '+00:00'))
    except ValueError:
        raise ValueError('lease-renewal-invalid-time') from None
    delta = parsed - datetime(1970, 1, 1, tzinfo=timezone.utc)
    return (delta.days * 86400 + delta.seconds) * 1_000_000_000 + int((match[2] or '').ljust(9, '0'))


def nonfuture_lease_time(value):
    observed = lease_time(value)
    clock = datetime.now(timezone.utc) - datetime(1970, 1, 1, tzinfo=timezone.utc)
    current = (clock.days * 86400 + clock.seconds) * 1_000_000_000 + clock.microseconds * 1000
    require(observed <= current, 'lease-renewal-future-time')
    return observed


def lease_content_digest(raw):
    expected = copy.deepcopy(raw)
    require(isinstance(expected.get('spec'), dict), 'lease-renewal-invalid-time')
    expected['spec'].pop('renewTime', None)
    return content_review_digest(expected)


def make_lease_bindings(raw):
    bindings = {}
    for uid, v in raw.items():
        if (v.get('apiVersion'), v.get('kind')) != ('coordination.k8s.io/v1', 'Lease'):
            continue
        renewal = v['spec'].get('renewTime') if isinstance(v.get('spec'), dict) else None
        nonfuture_lease_time(renewal)
        bindings[uid] = {**{field: project_resource(v)[field] for field in LEASE_IDENTITY_FIELDS},
                         'originalRenewTime': renewal, 'contentWithoutRenewTimeSha256': lease_content_digest(v)}
    return bindings


def lease_bindings(plan):
    bindings = plan.get('leaseRenewalBindings', {})
    require(isinstance(bindings, dict), 'invalid-lease-renewal-binding')
    baseline = {v['uid']: v for v in plan['resources']}
    for uid, binding in bindings.items():
        require(isinstance(binding, dict) and set(binding) == {*LEASE_IDENTITY_FIELDS, 'originalRenewTime',
                'contentWithoutRenewTimeSha256'} and uid in baseline and binding['uid'] == uid
                and (binding['apiVersion'], binding['kind']) == ('coordination.k8s.io/v1', 'Lease')
                and all(binding[field] == baseline[uid][field] for field in LEASE_IDENTITY_FIELDS)
                and isinstance(binding['contentWithoutRenewTimeSha256'], str)
                and HEX.fullmatch(binding['contentWithoutRenewTimeSha256']), 'invalid-lease-renewal-binding')
        nonfuture_lease_time(binding['originalRenewTime'])
    return bindings


def validate_bound_lease(plan, uid, raw, binding, floor=None):
    projected = project_resource(raw)
    require(all(projected[field] == binding[field] for field in LEASE_IDENTITY_FIELDS), 'lease-renewal-identity-drift')
    renewal = nonfuture_lease_time(raw['spec'].get('renewTime') if isinstance(raw.get('spec'), dict) else None)
    require(renewal >= (lease_time(binding['originalRenewTime']) if floor is None else floor),
            'lease-renewal-backwards-time')
    require(lease_content_digest(raw) == binding['contentWithoutRenewTimeSha256'], 'lease-renewal-other-content-drift')
    original = copy.deepcopy(raw)
    original['spec']['renewTime'] = binding['originalRenewTime']
    require(content_review_digest(original) == plan['fullContentSha256'][uid], 'lease-renewal-binding-content-mismatch')
    return renewal


def validate_plan_content(plan, raw):
    """Original plans remain strict; only their explicit native Lease bindings permit renewal."""
    require(set(raw) == set(plan['fullContentSha256']), 'execution-before-content-binding-mismatch')
    bindings = lease_bindings(plan)
    floors = {}
    for uid, v in raw.items():
        if uid in bindings:
            floors[uid] = validate_bound_lease(plan, uid, v, bindings[uid])
        else:
            require(content_review_digest(v) == plan['fullContentSha256'][uid], 'plan-full-content-drift')
    return floors


class SignedInputs:
    """Verify original bytes against an explicitly selected, externally held trust root."""
    def __init__(self, args, attempt):
        self.args, self.attempt, self.hashes, self.records = args, attempt, {}, {}
        self.sequence = 0
        self.trust_sha = file_digest(args.allowed_signers)

    def read(self, path, *, namespace='hexalith-retirement'):
        path = private_file(path, self.args.project_root)
        sig = private_file(str(path) + '.sig', self.args.project_root)
        trust = private_file(self.args.allowed_signers, self.args.project_root)
        data = path.read_bytes()
        result = subprocess.run([str(self.args.ssh_keygen), '-Y', 'verify', '-f', str(trust),
            '-I', self.args.administrator_principal, '-n', namespace, '-s', str(sig)],
            input=data, capture_output=True, timeout=30)
        require(result.returncode == 0, 'unverified-evidence-signature')
        record = json.loads(data)
        require(isinstance(record, dict), 'invalid-signed-record')
        identity = digest(data)
        self.hashes[str(path)] = {'sha256': identity, 'signatureSha256': file_digest(sig)}
        self.records[str(path)] = record
        self.sequence += 1
        # Signed originals can contain raw provider identities or private diagnostics.
        self.attempt.encrypt('signed-input-' + str(self.sequence), canonical({
            'recordBase64': base64.b64encode(data).decode(),
            'signatureBase64': base64.b64encode(sig.read_bytes()).decode()}), self.args.age, self.args.recipient)
        return record, identity

    def assert_unchanged(self):
        require(file_digest(self.args.allowed_signers) == self.trust_sha, 'approved-trust-root-changed')
        for path, bound in self.hashes.items():
            require(file_digest(private_file(path, self.args.project_root)) == bound['sha256']
                    and file_digest(private_file(path + '.sig', self.args.project_root)) == bound['signatureSha256'],
                    'approved-evidence-input-changed')


def actions_for(inventory, phases):
    by_uid = {v['uid']: v for v in inventory}
    actions = []
    for phase in phases:
        for uid in phase['expected'] + phase.get('expectedModified', []):
            v = by_uid[uid]
            action = {k: v[k] for k in ('apiVersion', 'kind', 'namespace', 'name', 'uid', 'resourceVersion')}
            action.update(action='remove-named-finalizer' if v['kind'] == 'Namespace' else 'delete',
                          propagation='Foreground', phase=phase['phase'], order=phase['order'],
                          rootRequest=uid in phase['roots'], namedFinalizer=phase['namedFinalizer'])
            if v['kind'] == 'Namespace':
                action['finalizer'] = SYSTEM_WORKSPACE_FINALIZER
            actions.append(action)
    validate_allowlist(inventory, actions)
    return actions


def validate_production_scope(inventory, actions):
    """Retained CRDs and shared workload/system components cannot become a new allowlist by annotation."""
    by_uid = {v['uid']: v for v in inventory}
    for action in actions:
        if action['action'] != 'delete':
            continue
        v = by_uid[action['uid']]
        require(v['kind'] not in PROTECTED_KINDS | {'CustomResourceDefinition'}, 'protected-resource-in-production-scope')
        if not v['namespace']:
            require((v['apiVersion'], v['kind']) in REVIEWED_CLUSTER_KINDS,
                    'cluster-shared-kind-outside-reviewed-manager-catalog')
        if v['namespace'] and v['namespace'] not in MANAGER_NAMESPACES:
            # These exact IAM projections in the seven retained namespaces were qualified under 4.26.
            projection = (v['namespace'] in PRODUCTION_FINALIZER_NAMES
                and v['apiVersion'] == 'rbac.authorization.k8s.io/v1' and v['kind'] == 'RoleBinding'
                and v['name'] == 'kubesphere:iam:system-workspace:system-workspace-admin'
                and action.get('phase') == 'workspace-role-bindings'
                and v['managementLabels'].get('iam.kubesphere.io/workspacerolebinding-ref') == 'system-workspace-admin'
                and v['managementLabels'].get('kubesphere.io/workspace') == 'system-workspace')
            require(projection, 'shared-component-outside-manager-namespace-in-scope')
    validate_role_consumers(inventory, actions)


def validate_role_consumers(inventory, actions):
    deleted = {v['uid'] for v in actions if v['action'] == 'delete'}
    for role in inventory:
        if role['uid'] not in deleted or role['apiVersion'] != 'rbac.authorization.k8s.io/v1' or role['kind'] not in ('Role', 'ClusterRole'):
            continue
        for binding in inventory:
            if binding['uid'] in deleted or binding['apiVersion'] != 'rbac.authorization.k8s.io/v1' or binding['kind'] not in ('RoleBinding', 'ClusterRoleBinding'):
                continue
            ref = binding.get('roleRef', {})
            matches = ref.get('kind') == role['kind'] and ref.get('name') == role['name']
            require(not matches or role['kind'] == 'Role' and binding['namespace'] != role['namespace'],
                    'retained-native-binding-consumes-retired-role')


def make_plan(args, inventory, raw, source, rehearsals):
    require(source.get('sourceClusterUid') and source.get('nativeEndpoint'), 'missing-native-source-identity')
    require({v['uid'] for v in inventory} == set(raw), 'incomplete-private-content-baseline')
    scope = retirement_scope(inventory)
    retained = {v['uid'] for v in inventory if key(v) in AUTHORITY}
    require(len(retained) == len(AUTHORITY), 'retained-authority-incomplete')
    require(not scope & retained, 'retained-authority-in-removal-scope')
    phases = dependency_plan(inventory, scope)
    require(phases and all(v['name'] in PRODUCTION_FINALIZER_NAMES for v in inventory
        if v['uid'] in {uid for p in phases for uid in p.get('expectedModified', [])}),
        'nonproduction-namespace-intervention')
    actions = actions_for(inventory, phases)
    validate_production_scope(inventory, actions)
    execution_id = getattr(args, 'execution_attempt_id', None) or args.attempt_id + '-execution'
    require(re.fullmatch(r'[a-z0-9][a-z0-9-]{0,79}', execution_id) and execution_id != args.attempt_id,
            'distinct-execution-attempt-id-required')
    rehearsal_source = json.loads(args.rehearsal_source_inventory.read_bytes()) if getattr(args, 'rehearsal_source_inventory', None) else source
    require(rehearsal_source['sourceClusterUid'] == source['sourceClusterUid'], 'rehearsal-source-cluster-mismatch')
    return {'schemaVersion': 1, 'kind': 'kubesphere-retirement-plan', 'attemptId': execution_id,
            'capturedAt': source['capturedAt'], 'context': args.context,
            'sourceClusterUid': source['sourceClusterUid'], 'nativeEndpoint': source['nativeEndpoint'],
            'sourceInventorySha256': digest(canonical(source)), **code_binding(args.project_root),
            'rehearsalSourceInventorySha256': file_digest(args.rehearsal_source_inventory)
                if getattr(args, 'rehearsal_source_inventory', None) else digest(canonical(source)),
            'toolSha256': tool_binding(args),
            'nativeKubeconfigSha256': file_digest(args.kubeconfig),
            'resolvedNativeConfigurationSha256': source['resolvedNativeConfigurationSha256'],
            'receiptSigningKeySha256': file_digest(args.receipt_key),
            'trustRootSha256': file_digest(args.allowed_signers),
            'administratorPrincipal': args.administrator_principal,
            'resources': inventory, 'fullContentSha256': {uid: content_review_digest(v) for uid, v in raw.items()},
            'leaseRenewalBindings': make_lease_bindings(raw),
            'actions': actions, 'phases': phases, 'rehearsals': rehearsals,
            'retainedAuthorityUids': sorted(retained), 'productionApproved': False,
            'sourceAtomic': False, 'upgradeGate': 'closed'}


def validate_plan(args, plan, current, raw, source):
    require(plan.get('kind') == 'kubesphere-retirement-plan' and plan.get('productionApproved') is False,
            'invalid-plan-proposal')
    require(plan['context'] == args.context and plan['sourceClusterUid'] == source['sourceClusterUid']
            and plan['nativeEndpoint'] == source['nativeEndpoint'], 'plan-source-identity-drift')
    require(plan['resolvedNativeConfigurationSha256'] == source['resolvedNativeConfigurationSha256'],
            'resolved-native-configuration-changed')
    require(all(plan[k] == v for k, v in code_binding(args.project_root).items()), 'execution-bytes-changed')
    require(plan['toolSha256'] == tool_binding(args)
            and plan['nativeKubeconfigSha256'] == file_digest(args.kubeconfig)
            and plan['receiptSigningKeySha256'] == file_digest(args.receipt_key)
            and plan['trustRootSha256'] == file_digest(args.allowed_signers)
            and plan['administratorPrincipal'] == args.administrator_principal, 'execution-input-changed')
    baseline = {v['uid']: v for v in plan['resources']}
    require(set(baseline) == {v['uid'] for v in current} == set(raw), 'census-identity-drift')
    for v in current:
        uid = v['uid']
        require(key(v) == key(baseline[uid]), 'plan-full-content-drift')
    validate_plan_content(plan, raw)
    # Reconstruct every phase and closure from the approved baseline, never promote a caller's actions.
    scope = retirement_scope(plan['resources'])
    require(not scope & set(plan['retainedAuthorityUids']), 'retained-authority-in-removal-scope')
    phases = dependency_plan(plan['resources'], scope)
    require(plan['phases'] == phases and plan['actions'] == actions_for(plan['resources'], phases),
            'plan-allowlist-or-phase-drift')
    validate_production_scope(plan['resources'], plan['actions'])


def validate_rehearsals(plan, roots):
    """Read complete immutable receipts, their checksums and the exact shared/executor/driver bindings."""
    required = {'catalog': ('rehearse_catalog.py', ('catalog-result.json', 'kubesphere-retirement-result.json',
                  'rollback-restore-result.json', 'cleanup.json')),
                'namespaces': ('rehearse_namespaces.py', ('seven-namespace-result.json', 'cleanup.json'))}
    receipts = {}
    for name, (driver, files) in required.items():
        directory = Path(roots[name])
        sums = {}
        for line in (directory / 'SHA256SUMS').read_text().splitlines():
            sha, filename = line.split('  ', 1)
            require(HEX.fullmatch(sha) and Path(filename).name == filename and filename not in sums,
                    'invalid-rehearsal-checksums')
            sums[filename] = sha
        loaded = {}
        for filename in ('attempt.json', 'summary.json', 'encrypted-exports.json', *files):
            path = directory / filename
            require(not path.is_symlink() and sums.get(filename) == file_digest(path), 'rehearsal-receipt-drift')
            loaded[filename] = json.loads(path.read_bytes())
        attempt = loaded['attempt.json']
        code = plan['codeSha256']
        require(attempt['scriptSha256'] == code['rehearse.py']
                and attempt['productionExecutorSha256'] == code['retire.py']
                and attempt['productionProcedureSha256'] == plan['procedureSha256']
                and attempt['sourceInventorySha256'] == plan['rehearsalSourceInventorySha256']
                and attempt['driverSha256'] == code[driver]
                and attempt['moduleSha256'] == {k: code[k] for k in ('evidence.py', 'qualify.py')},
                'rehearsal-execution-bytes-mismatch')
        require(loaded['summary.json']['state'] == 'passed-limited'
                and loaded['encrypted-exports.json']['readbackVerified'] is True
                and loaded['cleanup.json']['passed'] is True, 'rehearsal-or-cleanup-failed')
        if name == 'catalog':
            require(loaded['catalog-result.json']['state'] == 'passed'
                    and loaded['catalog-result.json']['sourceCounts'] == {k: sum(v['apiVersion'] == 'application.kubesphere.io/v2'
                        and v['kind'] == k for v in plan['resources']) for k in ('Repo', 'Application', 'ApplicationVersion', 'Category')}
                    and loaded['kubesphere-retirement-result.json']['state'] == 'passed-dependency-first-native-retirement'
                    and len(loaded['kubesphere-retirement-result.json']['phases']) == 19
                    and loaded['rollback-restore-result.json']['state'] == 'passed', 'catalog-or-fresh-node-rehearsal-failed')
            restored = loaded['rollback-restore-result.json']
            require(all(restored.get(k) is True for k in ('freshDockerNodeVerified', 'freshEtcdClusterAndMemberIdentities',
                'sourceNodeDestroyedBeforeRestore', 'syntheticCanaryRestoredAndReadbackEqual', 'markCompacted'))
                and restored.get('skipHashCheck') is False and restored.get('baselineUidMismatches') == []
                and restored.get('crdUidMismatches') == [], 'fresh-node-preservation-rehearsal-incomplete')
        else:
            result = loaded['seven-namespace-result.json']
            require(result['state'] == 'passed' and result['namespaceCount'] == 7
                    and result['actualNamespaceDeleteRequests'] == 0
                    and result['actualNamespaceUidResourceVersionBoundPutRequests'] == 7,
                    'seven-namespace-rehearsal-failed')
        receipts[name] = {'attemptId': attempt['attemptId'], 'checksumsSha256': file_digest(directory / 'SHA256SUMS'),
                          'records': {filename: sums[filename] for filename in loaded}}
    return receipts


def validate_backup(args, signed, policy, plan):
    records, hashes = {}, {}
    for name in BACKUP_FILES:
        records[name], hashes[name] = signed.read(args.backup_bundle / name, namespace='hexalith-recovery')
    backup_policy = records[BACKUP_FILES[0]]
    gate = records['gate/backup-gate.json']
    validation = records['validation/validation.json']
    rid = backup_policy['recoveryId']
    for name, record in records.items():
        require(record.get('recoveryId') == rid, 'backup-recovery-id-mismatch')
        if name not in ('policy/recovery-policy.json', 'manifest/evidence-manifest.json'):
            require(record.get('verificationResult') == 'pass', 'backup-proof-or-validation-failed')
    require(gate['policySha256'] == hashes[BACKUP_FILES[0]]
            and gate['evidenceManifestSha256'] == hashes[BACKUP_FILES[1]], 'backup-gate-binding-mismatch')
    require(validation['gateSha256'] == hashes['gate/backup-gate.json']
            and validation['policySha256'] == hashes[BACKUP_FILES[0]]
            and validation['evidenceManifestSha256'] == hashes[BACKUP_FILES[1]]
            and validation.get('dryRun') is False, 'backup-validation-binding-mismatch')
    for name in BACKUP_FILES[:-1]:
        bound = validation['inputs'][name]
        require(bound['sha256'] == hashes[name] and bound['signatureVerified'] is True
                and bound['signatureSha256'] == file_digest(str(args.backup_bundle / name) + '.sig'),
                'backup-validation-input-mismatch')
    require(validation['summary']['fail'] == 0 and validation['summary']['skipped'] == 0
            and validation['checks'] and all(v['status'] == 'pass' for v in validation['checks']),
            'backup-validation-incomplete')
    counts = validation['providerReadBack']
    require(type(counts.get('manifestObjectsReRead')) is int and counts['manifestObjectsReRead'] > 0
            and counts['manifestObjectsReRead'] == counts['manifestObjectsMatching'], 'off-node-readback-incomplete')
    fresh({**validation, 'expiresAt': gate['expiresAt']}, policy['maximumValidationAgeSeconds'], captured='validatedAt')
    fresh(gate, policy['maximumRecoveryAgeSeconds'], captured='assembledAt')
    for system, name in zip(('keycloak', 'openbao', 'memories'), BACKUP_FILES[4:7]):
        proof = records[name]
        maximum = backup_policy['systems'][system]['maximumProofAgeSeconds']
        fresh(proof, min(maximum, policy['maximumRecoveryAgeSeconds']))
        require(proof['policySha256'] == hashes[BACKUP_FILES[0]]
                and proof['evidenceManifestSha256'] == hashes[BACKUP_FILES[1]]
                and proof['system'] == system and proof['runKind'] == 'final'
                and proof['measuredRpo']['withinPolicy'] is True
                and gate['proofs'][system]['sha256'] == hashes[name]
                and gate['proofs'][system]['signatureSha256'] == file_digest(str(args.backup_bundle / name) + '.sig'),
                'workload-proof-binding-or-rpo-failed')
        values = proof['sourceUid'] if isinstance(proof['sourceUid'], dict) else {'source': proof['sourceUid']}
        require(set(values.values()) <= {v['uid'] for v in plan['resources']}, 'workload-proof-source-drift')
        require(not set(values.values()) & {v['uid'] for v in plan['actions'] if v['action'] == 'delete'},
                'protected-workload-source-in-removal-scope')
        expected = {'keycloak': ('postgresql.cnpg.io/v1', 'Cluster', 'keycloak', {'keycloak-postgres'}),
                    'openbao': ('apps/v1', 'StatefulSet', 'openbao', {'hexalith-keys'}),
                    'memories': ('apps/v1', 'StatefulSet', 'hexalith-memories', {'redis-stack', 'falkordb'})}[system]
        observed = [v for v in plan['resources'] if v['uid'] in values.values()]
        require(len(observed) == len(expected[3]) and all(key(v)[:3] == expected[:3] for v in observed)
                and {v['name'] for v in observed} == expected[3], 'workload-proof-source-drift')
    return hashes


def validate_recovery_point(point):
    """The same measured, signed external-etcd point contract applies before and after retirement."""
    require(point.get('snapshotIntegrityVerified') is True and point.get('encrypted') is True
            and isinstance(point.get('immutableOffNodeVersion'), str) and bool(point['immutableOffNodeVersion'])
            and point.get('independentReadbackVerified') is True
            and type(point.get('revision')) is int and point['revision'] > 0
            and type(point.get('keyCount')) is int and point['keyCount'] > 0
            and all(isinstance(point.get(k), str) and HEX.fullmatch(point[k])
                    for k in ('keyHash', 'canarySha256', 'configurationSha256'))
            and safe(point.get('sourceMemberId')), 'external-etcd-recovery-point-incomplete')


def validate_preflight(args, signed, plan, plan_sha, raw):
    policy, policy_sha = signed.read(args.preflight / 'retirement-policy.json')
    require(policy['trustRootSha256'] == file_digest(args.allowed_signers)
            and policy['administratorPrincipal'] == args.administrator_principal,
            'policy-trust-root-mismatch')
    fresh(policy, 86400)
    require(type(policy['maximumPlanAgeSeconds']) is int, 'invalid-approved-freshness-policy')
    fresh({**plan, 'expiresAt': policy['expiresAt']}, policy['maximumPlanAgeSeconds'])
    backup_hashes = validate_backup(args, signed, policy, plan)
    records, hashes = {}, {'retirement-policy.json': policy_sha}
    for name in GATE_FILES:
        record, sha = signed.read(args.preflight / name)
        require(record.get('attemptId') == args.attempt_id and record.get('planSha256') == plan_sha
                and record.get('sourceClusterUid') == plan['sourceClusterUid']
                and record.get('verificationResult') == 'pass', 'preflight-attempt-or-source-mismatch')
        fresh(record, policy['maximumRecoveryAgeSeconds'] if name in GATE_FILES[2:5]
              else policy['maximumValidationAgeSeconds'])
        records[name], hashes[name] = record, sha
    validation = records['recovery-validation.json']
    require(validation['backupInputs'] == backup_hashes and validation['independentOffNodeReadback'] is True
            and validation['readOnlyValidatorCredentialUsed'] is True, 'fresh-recovery-validation-mismatch')
    closure = records['console-closure.json']
    require(closure.get('story') == '4.2' and closure.get('accepted') is True
            and closure.get('externalPathIndependent') is True and closure.get('publicConsoleDenied') is True
            and closure.get('publicOidcPassed') is True, 'public-console-closure-unaccepted')
    point, restore, node = (records[v] for v in GATE_FILES[2:5])
    validate_recovery_point(point)
    require(restore.get('recoveryPointSha256') == hashes[GATE_FILES[2]]
            and restore.get('targetClusterUid') and restore['targetClusterUid'] != plan['sourceClusterUid']
            and restore.get('targetMemberId') and restore['targetMemberId'] != point.get('sourceMemberId')
            and all(restore.get(k) is True for k in ('sourceFenced', 'isolated', 'memberHealthy', 'cleanupVerified'))
            and all(restore.get(k) == point.get(k) and point.get(k) is not None for k in
                    ('revision', 'keyCount', 'keyHash', 'canarySha256')), 'external-etcd-isolated-restore-incomplete')
    require(node.get('recoveryPointSha256') == hashes[GATE_FILES[2]]
            and node.get('configurationSha256') == point['configurationSha256']
            and all(node.get(k) is True for k in ('encrypted', 'independentReadbackVerified', 'configurationMatchesSource',
                   'ownerModesPreserved', 'testedRecoveryProcedure')) and node.get('immutableOffNodeVersion'),
            'matching-node-recovery-incomplete')
    native = records['native-access.json']
    binding = next((v for v in plan['resources'] if v['uid'] == native.get('clusterAdminBindingUid')), None)
    # The older sanitized projection omits roleRef.apiGroup. The private raw
    # object is already bound to the approved full-content digest by validate_plan.
    role_ref = raw.get(binding['uid'], {}).get('roleRef', {}) if binding else {}
    require(binding and binding['apiVersion'] == 'rbac.authorization.k8s.io/v1'
            and binding['kind'] == 'ClusterRoleBinding' and not binding['owners']
            and role_ref.get('apiGroup') == 'rbac.authorization.k8s.io'
            and role_ref.get('kind') == 'ClusterRole' and role_ref.get('name') == 'cluster-admin'
            and binding['uid'] not in plan['retainedAuthorityUids']
            and binding['uid'] not in {v['uid'] for v in plan['actions']}
            and all(native.get(k) is True for k in ('directPrivateAccess', 'administratorAccessVerified',
                   'independentCustodyReadback', 'breakGlassIndependent')), 'independent-native-authority-incomplete')
    require(all(records['pre-retirement-health.json'].get(k) is True for k in
        ('nativeApi', 'dns', 'cni', 'storage', 'admission', 'publicApplications', 'publicOidc'))
        and all(records['pre-retirement-health.json'].get('authenticatedWorkloads', {}).get(w) is True for w in WORKLOADS),
        'pre-retirement-authenticated-health-failed')
    approval, approval_sha = signed.read(args.approval)
    require(approval.get('kind') == 'production-retirement-approval' and approval.get('approvedBy') == 'Administrator'
            and approval.get('attemptId') == args.attempt_id and approval.get('planSha256') == plan_sha
            and approval.get('preflightInputs') == hashes
            and approval.get('backupInputs') == backup_hashes
            and approval.get('rehearsals') == plan['rehearsals']
            and all(approval.get(k) == plan[k] for k in ('codeSha256', 'procedureSha256', 'toolSha256', 'maintenanceSha256'))
            and approval.get('allowlistSha256') == digest(canonical(plan['actions']))
            and approval.get('sourceClusterUid') == plan['sourceClusterUid'], 'execution-approval-binding-mismatch')
    require(all(isinstance(approval.get(k), str) and approval[k].strip() for k in
        ('outageScope', 'incidentOwner', 'recoveryOwner', 'operator', 'incidentChannel', 'stopConditions', 'recoveryConditions'))
        and approval['operator'] == args.operator, 'execution-owner-or-stop-decision-missing')
    require(timestamp(approval['windowStart']) <= datetime.now(timezone.utc) < timestamp(approval['windowEnd']),
            'execution-outside-approved-window')
    fresh(approval, policy['maximumValidationAgeSeconds'])
    deadlines = [timestamp(approval['windowEnd']), timestamp(policy['expiresAt']),
                 timestamp(plan['capturedAt']) + timedelta(seconds=policy['maximumPlanAgeSeconds'])]
    for path, record in signed.records.items():
        if record.get('expiresAt'):
            deadlines.append(timestamp(record['expiresAt']))
        field = next((v for v in ('capturedAt', 'validatedAt', 'assembledAt') if v in record), None)
        if field:
            maximum = 86400 if Path(path).name == 'retirement-policy.json' else policy['maximumRecoveryAgeSeconds'] \
                if Path(path).name in GATE_FILES[2:5] or path.startswith(str(args.backup_bundle)) and field != 'validatedAt' \
                else policy['maximumValidationAgeSeconds']
            if path.startswith(str(args.backup_bundle)) and record.get('system') in WORKLOADS[:3]:
                maximum = min(maximum, json.loads((args.backup_bundle / BACKUP_FILES[0]).read_bytes())['systems'][record['system']]['maximumProofAgeSeconds'])
            deadlines.append(timestamp(record[field]) + timedelta(seconds=maximum))
    policy = dict(policy, gateDeadline=min(deadlines).isoformat())
    return approval, approval_sha, policy


class CaptureSink:
    """Keep even sanitized full census records private; publish only deliberate outcome projections."""
    def __init__(self, attempt, args, prefix):
        self.attempt, self.args, self.prefix, self.records = attempt, args, prefix, {}

    def encrypt(self, name, *args):
        return self.attempt.encrypt(self.prefix + '-' + name, *args)

    def record(self, name, record):
        self.records[name] = record
        return self.encrypt(name.removesuffix('.json'), canonical(record), self.args.age, self.args.recipient)


class NativeCapture(Capture):
    def __init__(self, args, attempt):
        super().__init__(args, attempt)
        self.raw_by_uid, self.discovery = {}, {}

    def kube(self, name, args, parse=True):
        result = super().kube(name, args, parse)
        if name == 'native-config':
            self.resolved_config_sha = resolved_configuration_sha(result)
        return result

    def raw(self, name, path):
        data = super().raw(name, path)
        if isinstance(data, dict):
            if isinstance(data.get('resources'), list):
                gv = data.get('groupVersion')
                for v in data['resources']:
                    if isinstance(v, dict) and '/' not in v.get('name', '') and safe(v.get('name')) and safe(v.get('kind')):
                        self.discovery[(gv, v['kind'])] = (v['name'], v.get('namespaced') is True)
            for raw in data.get('items', []):
                if isinstance(raw, dict) and safe(raw.get('metadata', {}).get('uid')):
                    self.raw_by_uid[raw['metadata']['uid']] = raw
        return data

    def resources(self, data, step):
        # These read-only diagnostics have no persistence/UID or deletion authority.
        resources = super().resources(data, step)
        return [v for v in resources if v['kind'] not in ('ComponentStatus', 'NodeMetrics', 'PodMetrics')]

    def list_resource(self, gv, prefix, resource, step):
        first = len(self.inventory)
        super().list_resource(gv, prefix, resource, step)
        # Native list items can omit TypeMeta. Match Capture's discovery-derived
        # defaults so the private full-content baseline represents the same object.
        for projected in self.inventory[first:]:
            raw = self.raw_by_uid.get(projected['uid'])
            if raw is not None:
                raw.setdefault('apiVersion', gv)
                raw.setdefault('kind', resource['kind'])


class Native(PhaseExecutor):
    """Explicit native client. Its only write verbs are reviewed object DELETE and full guarded PUT."""
    def __init__(self, args, attempt):
        self.args, self.attempt = args, attempt
        self.sequence, self.census_sequence, self.mutation_requests = 0, 0, 0
        self.raw_inventory_by_uid, self.reviewed_raw_by_uid, self.reviewed_content_digests = {}, {}, {}
        self.retirement_uids, self.completed_retirement_phases = set(), set()
        self.armed, self.plan, self.approval, self.policy = False, None, None, None
        self.last_step, self.active_phase = 'not-started', None
        self.finished_at = None
        self.original_raw, self.namespace_modified = {}, set()
        self.signed_inputs = None
        self.accepted_deletes = set()
        self.configuration_sequence = 0
        self.lease_renewal_floors = {}
        self.lease_delete_states = {}

    def resolved_configuration(self):
        """Resolve static certificate/key references without any network or credential hook execution."""
        self.configuration_sequence += 1
        argv = [str(self.args.kubectl), '--kubeconfig', str(self.args.kubeconfig), '--context', self.args.context,
                'config', 'view', '--minify', '--flatten', '--raw', '-o', 'json']
        result = subprocess.run(argv, capture_output=True, timeout=30)
        self.attempt.encrypt(f'resolved-config-{self.configuration_sequence:05d}', canonical({'argv': argv,
            'exitCode': result.returncode, 'stdoutBase64': base64.b64encode(result.stdout).decode(),
            'stderrBase64': base64.b64encode(result.stderr).decode()}), self.args.age, self.args.recipient)
        require(result.returncode == 0, 'resolved-native-configuration-unavailable')
        return resolved_configuration_sha(json.loads(result.stdout))

    def census(self):
        # Refuse unbound auth hooks before Capture's first network/version request.
        before_config = self.resolved_configuration()
        self.census_sequence += 1
        sink = CaptureSink(self.attempt, self.args, f'census-{self.census_sequence:04d}')
        capture = NativeCapture(self.args, sink)
        access = capture.collect()
        require(capture.resolved_config_sha == before_config, 'resolved-native-configuration-changed')
        require(access['authorizedRead'] and access['unauthorizedDenied'], 'native-read-or-denial-unverified')
        require(all(v['state'] == 'observed' for v in capture.coverage), 'native-census-coverage-incomplete')
        # Events are ephemeral diagnostic observations, never deletion or preservation identities.
        inventory = [v for v in capture.inventory if v['kind'] != 'Event']
        raw = {v['uid']: capture.raw_by_uid[v['uid']] for v in inventory}
        require(all(project_resource(raw[v['uid']]) == v for v in inventory), 'inconsistent-native-census')
        self.raw_inventory_by_uid, self.discovery = raw, capture.discovery
        self.observe_bound_leases()
        source = sink.records['inventory.json']
        source['resources'] = inventory
        source['resolvedNativeConfigurationSha256'] = before_config
        return inventory, source

    def settled_inventory(self):
        before, _ = self.census()
        time.sleep(2)
        after, _ = self.census()
        stable = {(v['uid'], v.get('deletionTimestamp')) for v in before} == {
            (v['uid'], v.get('deletionTimestamp')) for v in after}
        return after, stable

    def wait_absent(self, uids, timeout):
        deadline = time.monotonic() + timeout
        while True:
            inventory, _ = self.census()
            self.check_preservation(inventory)
            if not set(uids) & {v['uid'] for v in inventory}:
                return True
            if time.monotonic() >= deadline:
                return False
            time.sleep(2)

    def observe_bound_leases(self):
        """Every census observation advances a validated renewal floor, including settled read pairs."""
        if self.plan is None:
            return
        for uid, binding in lease_bindings(self.plan).items():
            if uid in self.raw_inventory_by_uid:
                self.observe_bound_lease(self.raw_inventory_by_uid[uid], bindings={uid: binding})

    def observe_bound_lease(self, raw, bindings=None):
        """Exact object GETs at the unchanged shared checkpoint obey the same renewal floor."""
        if self.plan is None or not isinstance(raw, dict):
            return
        uid = raw.get('metadata', {}).get('uid')
        binding = (lease_bindings(self.plan) if bindings is None else bindings).get(uid)
        if binding is None:
            return
        require(all(project_resource(raw)[field] == binding[field] for field in LEASE_IDENTITY_FIELDS),
                'lease-renewal-identity-drift')
        if uid in self.accepted_deletes:
            raw = self.lease_delete_view(uid, raw)
        renewal = validate_bound_lease(self.plan, uid, raw, binding, self.lease_renewal_floors.get(uid))
        self.lease_renewal_floors[uid] = renewal

    def lease_delete_view(self, uid, raw):
        """Only this accepted Foreground DELETE's terminating metadata may differ from its baseline."""
        action = next((v for v in self.plan['actions'] if v['uid'] == uid), {})
        require(action.get('action') == 'delete' and action.get('propagation') == 'Foreground'
                and uid in self.original_raw, 'lease-delete-state-unapproved')
        expected = copy.deepcopy(raw)
        metadata, original = expected['metadata'], self.original_raw[uid]['metadata']
        deletion = metadata.get('deletionTimestamp')
        if deletion is None:
            # A successful request alone never excuses any content difference.
            require(uid not in self.lease_delete_states, 'lease-delete-state-changed')
            return expected
        nonfuture_lease_time(deletion)
        require(lease_time(deletion) >= lease_time(self.original_raw[uid]['spec']['renewTime']),
                'lease-delete-state-unverified')
        if uid in self.lease_delete_states:
            require(deletion == self.lease_delete_states[uid], 'lease-delete-state-changed')
        else:
            self.lease_delete_states[uid] = deletion
        if original.get('deletionTimestamp') is not None:
            require(deletion == original['deletionTimestamp'], 'lease-delete-state-changed')
        for field in ('deletionTimestamp', 'deletionGracePeriodSeconds', 'finalizers'):
            if field == 'deletionGracePeriodSeconds' and field in metadata:
                require(type(metadata[field]) is int and metadata[field] == 0
                        and (field not in original or metadata[field] == original[field]), 'lease-delete-state-unverified')
            if field == 'deletionGracePeriodSeconds' and field in original:
                require(metadata.get(field) == original[field], 'lease-delete-state-unverified')
            if field == 'finalizers':
                finalizers = metadata.get(field, [])
                require(isinstance(finalizers, list) and len(finalizers) == len(set(finalizers))
                        and [v for v in finalizers if v != 'foregroundDeletion'] ==
                            [v for v in (original.get('finalizers') or []) if v != 'foregroundDeletion']
                        and ('foregroundDeletion' not in (original.get('finalizers') or [])
                             or 'foregroundDeletion' in finalizers), 'lease-delete-state-unverified')
            if field in original:
                metadata[field] = copy.deepcopy(original[field])
            else:
                metadata.pop(field, None)
        return expected

    def check_preservation(self, inventory):
        baseline = self.plan['resources']
        require(not (self.active_phase or {}).get('managerAbsentRequired') or not any(
            v['namespace'] in MANAGER_NAMESPACES and v['kind'] in MANAGER_RUNTIME_KINDS for v in inventory),
            'manager-runtime-present-before-request')
        deleted = {v['uid'] for v in baseline} - {v['uid'] for v in inventory}
        permitted = {uid for p in self.plan['phases'] if p['phase'] in self.completed_retirement_phases
                     or p == self.active_phase for uid in p['expected']}
        require(deleted <= permitted, 'unexpected-deletion-during-retirement')
        modifications = {key(v): SYSTEM_WORKSPACE_FINALIZER for v in baseline
                         if v['kind'] == 'Namespace' and v['uid'] in self.namespace_modified}
        compared = copy.deepcopy(inventory)
        active = set((self.active_phase or {}).get('expected', []))
        initial = {v['uid']: v for v in baseline}
        for v in compared:
            # The approved Foreground closure can be terminating while the phase settles.
            if v['uid'] in active and self.mutation_requests:
                v['deletionTimestamp'] = initial[v['uid']].get('deletionTimestamp')
        assert_phase({v['uid'] for v in baseline}, baseline, compared, deleted, modifications)
        current = {v['uid']: v for v in inventory}
        bindings = lease_bindings(self.plan)
        self.observe_bound_leases()
        account = ('v1', 'ServiceAccount', 'kubesphere-system', 'kubesphere.users.jpiquot')
        require(not any(key(v) == account for v in inventory) or any(key(v) == account for v in baseline),
                'retained-authority-subject-activated')
        require(not {key(v) for v in baseline if v['uid'] in deleted} & {key(v) for v in inventory},
                'controller-recreated-retired-object')
        for v in baseline:
            uid = v['uid']
            if uid not in current:
                continue
            if uid in bindings and uid not in self.accepted_deletes:
                continue
            if uid in self.retirement_uids or v['kind'] in ('EndpointSlice', 'Endpoints'):
                continue
            expected = copy.deepcopy(self.original_raw[uid])
            if uid in self.namespace_modified:
                expected['metadata']['finalizers'] = [f for f in expected['metadata']['finalizers'] if f != SYSTEM_WORKSPACE_FINALIZER]
            require(content_review_digest(expected) == content_review_digest(self.raw_inventory_by_uid[uid]),
                    'retained-full-content-drift')

    def kube(self, name, *argv, obj=None, allowed=(0,)):
        require(argv and argv[0] in ('get', 'delete', 'replace') and '--raw' in argv,
                'unsupported-production-native-command')
        mutating = argv[0] != 'get'
        if mutating:
            require(self.armed and self.args.execute and self.args.arm_attempt == self.args.attempt_id,
                    'production-executor-not-armed')
            fresh(self.approval, self.policy['maximumValidationAgeSeconds'])
            require(datetime.now(timezone.utc) < timestamp(self.policy['gateDeadline']), 'approved-gate-expired')
            self.check_execution_inputs()
            self.signed_inputs.assert_unchanged()
            inventory, source = self.census()
            require(source['sourceClusterUid'] == self.plan['sourceClusterUid']
                    and source['nativeEndpoint'] == self.plan['nativeEndpoint'], 'live-source-identity-changed')
            require(source['resolvedNativeConfigurationSha256'] == self.plan['resolvedNativeConfigurationSha256'],
                    'resolved-native-configuration-changed')
            self.check_preservation(inventory)
            require(self.active_phase is not None, 'mutation-outside-approved-phase')
            path = argv[argv.index('--raw') + 1]
            reviewed = next((v for v in self.plan['resources'] if v['uid'] in self.active_phase['roots']
                             and api_path(self.discovery, v) == path), None)
            require(reviewed and reviewed['uid'] in self.active_phase['roots'], 'request-outside-phase-allowlist')
            action = next(v for v in self.plan['actions'] if v['uid'] == reviewed['uid'])
            validate_production_scope(self.plan['resources'], self.plan['actions'])
            validate_role_consumers(inventory, self.plan['actions'])
            observed = self.raw_inventory_by_uid.get(reviewed['uid'])
            require(observed is not None, 'request-object-disappeared')
            if argv[0] == 'delete':
                require(action['action'] == 'delete' and reviewed['kind'] not in PROTECTED_KINDS
                        and obj['preconditions']['uid'] == reviewed['uid']
                        and obj['preconditions']['resourceVersion'] == observed['metadata']['resourceVersion']
                        and obj['propagationPolicy'] == action['propagation'],
                        'request-delete-precondition-mismatch')
            else:
                require(self.active_phase['mode'] in ('namespace-finalizer', 'named-finalizer')
                        and obj['metadata']['uid'] == reviewed['uid']
                        and obj['metadata']['resourceVersion'] == observed['metadata']['resourceVersion']
                        and (action['action'] == 'remove-named-finalizer' and reviewed['kind'] == 'Namespace'
                             or action['action'] == 'delete' and self.active_phase['mode'] == 'named-finalizer'
                             and reviewed['kind'] not in PROTECTED_KINDS), 'unapproved-finalizer-put')
                if self.active_phase['mode'] == 'named-finalizer':
                    require(reviewed['uid'] in self.accepted_deletes
                            and isinstance(observed['metadata'].get('deletionTimestamp'), str)
                            and bool(observed['metadata']['deletionTimestamp']), 'named-finalizer-delete-state-unverified')
                expected = copy.deepcopy(observed)
                finalizer = self.active_phase['namedFinalizer']
                require(finalizer == SYSTEM_WORKSPACE_FINALIZER and finalizer in expected['metadata'].get('finalizers', []),
                        'unapproved-finalizer-put')
                expected['metadata']['finalizers'] = [v for v in expected['metadata']['finalizers'] if v != finalizer]
                require(content_review_digest(obj) == content_review_digest(expected), 'unapproved-finalizer-put-content')
        self.sequence += 1
        self.last_step = name
        command = [str(self.args.kubectl), '--kubeconfig', str(self.args.kubeconfig), '--context', self.args.context,
                   '--request-timeout=30s', *argv]
        prefix = f'native-{self.sequence:05d}'
        self.attempt.encrypt(prefix + '-intent', canonical({'argv': command, 'body': obj, 'mutating': mutating}),
                             self.args.age, self.args.recipient)
        if mutating:
            self.check_execution_inputs()
            self.mutation_requests += 1
        result = subprocess.run(command, input=canonical(obj) if obj is not None else None,
                                capture_output=True, timeout=45)
        self.attempt.encrypt(prefix + '-result', canonical({'exitCode': result.returncode,
            'stdoutBase64': base64.b64encode(result.stdout).decode(),
            'stderrBase64': base64.b64encode(result.stderr).decode()}), self.args.age, self.args.recipient)
        # Production stops at the first refusal, including a Conflict or NotFound after a request.
        require(result.returncode in allowed and (not mutating or result.returncode == 0), 'native-request-refused')
        if not mutating and result.returncode == 0:
            self.observe_bound_lease(json.loads(result.stdout))
        if mutating and argv[0] == 'delete':
            self.accepted_deletes.add(reviewed['uid'])
        if mutating and self.active_phase['mode'] == 'namespace-finalizer':
            self.namespace_modified.add(reviewed['uid'])
        return result

    def check_execution_inputs(self):
        fresh(self.approval, self.policy['maximumValidationAgeSeconds'])
        require(datetime.now(timezone.utc) < timestamp(self.policy['gateDeadline']), 'approved-gate-expired')
        self.check_execution_bindings()
        require(self.resolved_configuration() == self.plan['resolvedNativeConfigurationSha256'],
                'resolved-native-configuration-changed')
        # Offline config resolution and encrypted readback can block: nothing checked
        # before them may extend an approval/window or hide drift at request dispatch.
        self.check_execution_bindings()
        if self.signed_inputs is not None:
            self.signed_inputs.assert_unchanged()
        fresh(self.approval, self.policy['maximumValidationAgeSeconds'])
        require(datetime.now(timezone.utc) < timestamp(self.policy['gateDeadline']), 'approved-gate-expired')

    def check_execution_bindings(self):
        require(all(self.plan[k] == v for k, v in code_binding(self.args.project_root).items()), 'execution-bytes-changed')
        require(self.plan['toolSha256'] == tool_binding(self.args)
            and self.plan['nativeKubeconfigSha256'] == file_digest(private_file(self.args.kubeconfig, self.args.project_root))
            and self.plan['receiptSigningKeySha256'] == file_digest(private_file(self.args.receipt_key, self.args.project_root))
            and self.plan['trustRootSha256'] == file_digest(private_file(self.args.allowed_signers, self.args.project_root)),
            'execution-input-changed')

    def retire_phase(self, phase, baseline, by_uid, resources):
        self.active_phase = phase
        try:
            inventory, _ = self.census()
            self.check_preservation(inventory)
            after = super().retire_phase(phase, baseline, by_uid, resources)
            self.check_preservation(after)
            return after
        except Exception:
            self.armed = False
            self.completed_retirement_phases.discard(phase['phase'])
            self.attempt.record(f'phase-{phase["order"]:02d}-stopped.json', {
                'state': 'stopped', 'phase': phase['phase'], 'mutationRequests': self.mutation_requests,
                'incidentDecisionRequired': self.mutation_requests > 0, 'upgradeGate': 'closed'})
            raise


def structural_outcome(native, plan, inventory):
    native.check_preservation(inventory)
    require(not set(native.retirement_uids) & {v['uid'] for v in inventory}, 'retirement-incomplete')
    services = {(v['namespace'], v['name']) for v in inventory if v['kind'] == 'Service'}
    for v in inventory:
        require(not (v['namespace'] in MANAGER_NAMESPACES and v['kind'] in
                     ('Deployment', 'ReplicaSet', 'StatefulSet', 'DaemonSet', 'Pod', 'Job', 'CronJob')),
                'manager-runtime-or-unclassified-residue')
        require(not (admission_registration(v) and management_resource(v)), 'manager-admission-residue')
        require(not route_service_backends(v) - services, 'stale-route-backend')
        require(not any(w.get('service', {}).get('name') and
            (w['service']['namespace'], w['service']['name']) not in services for w in v.get('webhooks', [])),
            'stale-admission-backend')
        backend = v.get('service', {}) if v['kind'] == 'APIService' else v.get('customResource', {}).get('conversionService', {})
        require(not backend.get('name') or (backend.get('namespace'), backend['name']) in services, 'stale-api-or-conversion-backend')
    return {'retirementPhasesCompleted': sorted(native.completed_retirement_phases),
            'namespaceStorageAndRetainedContentPreserved': True, 'managerAndAdmissionAbsent': True,
            'retainedObjects': [{'object': label(key(v)), 'uid': v['uid'], 'custodian': 'Administrator',
                'reviewDate': '2026-11-03', 'disposition': 'active-authority-for-4.28' if key(v) in AUTHORITY
                else 'protected' if v['kind'] in PROTECTED_KINDS else 'archive'}
                for v in inventory if management_resource(v) or key(v) in AUTHORITY]}


def validate_assessment_inputs(args, native, plan, source=None):
    """Apply the original execution's trust and native identity to every read-only assessment."""
    require(all(plan[k] == v for k, v in code_binding(args.project_root).items()), 'execution-bytes-changed')
    require(plan['context'] == args.context
            and plan['administratorPrincipal'] == args.administrator_principal
            and plan['trustRootSha256'] == file_digest(private_file(args.allowed_signers, args.project_root)),
            'assessment-trust-context-or-principal-changed')
    require(plan['toolSha256'] == tool_binding(args)
            and plan['nativeKubeconfigSha256'] == file_digest(private_file(args.kubeconfig, args.project_root))
            and plan['receiptSigningKeySha256'] == file_digest(private_file(args.receipt_key, args.project_root)),
            'assessment-execution-input-changed')
    require(native.resolved_configuration() == plan['resolvedNativeConfigurationSha256'],
            'resolved-native-configuration-changed')
    if source is not None:
        require(source['sourceClusterUid'] == plan['sourceClusterUid']
                and source['nativeEndpoint'] == plan['nativeEndpoint'], 'assessment-source-endpoint-changed')
        require(source['resolvedNativeConfigurationSha256'] == plan['resolvedNativeConfigurationSha256'],
                'resolved-native-configuration-changed')


def assess(args, signed, native, inventory, source):
    """A separate immutable read-only assessment binds after-evidence to the sealed execution."""
    plan_path = private_file(args.plan, args.project_root)
    plan_bytes = plan_path.read_bytes()
    plan, plan_sha = json.loads(plan_bytes), digest(plan_bytes)
    validate_assessment_inputs(args, native, plan, source)
    execution, execution_sha = signed.read(args.assess_result)
    require(execution['state'] == 'retired-awaiting-acceptance' and execution['mutationRequests'] > 0,
            'execution-not-ready-for-assessment')
    require(plan['attemptId'] == execution['attemptId'] and plan_sha == execution['outcome']['planSha256'],
            'assessment-plan-execution-mismatch')
    require(plan['sourceClusterUid'] == next(v['uid'] for v in inventory if v['kind'] == 'Namespace'
            and v['name'] == 'kube-system') and plan['context'] == args.context, 'assessment-source-mismatch')
    native.assessment_plan, native.assessment_plan_sha = plan, plan_sha
    export = next(v for v in execution['rawExports'] if v['file'] == 'production-before-inventory.age')
    ciphertext = private_file(args.assess_result.parent / export['file'], args.project_root).read_bytes()
    require(digest(ciphertext) == export['ciphertextSha256'], 'execution-before-inventory-changed')
    decrypted = subprocess.run([str(args.age), '--decrypt', '--identity', str(args.readback_identity)],
                               input=ciphertext, capture_output=True, timeout=120)
    require(decrypted.returncode == 0, 'execution-before-inventory-unreadable')
    native.original_raw = json.loads(decrypted.stdout)
    native.lease_renewal_floors = validate_plan_content(plan, native.original_raw)
    native.plan = plan
    native.retirement_uids = {uid for p in plan['phases'] for uid in p['expected']}
    native.completed_retirement_phases = {p['phase'] for p in plan['phases']}
    require(sorted(native.completed_retirement_phases) == execution['completedPhases'], 'execution-phases-incomplete')
    native.namespace_modified = {uid for p in plan['phases'] for uid in p.get('expectedModified', [])}
    outcome = structural_outcome(native, plan, inventory)
    policy, _ = signed.read(args.preflight / 'retirement-policy.json')
    fresh(policy, 86400)
    native.assessment_freshness = [(policy, 86400, None)]
    require(policy['trustRootSha256'] == plan['trustRootSha256']
            and policy['administratorPrincipal'] == plan['administratorPrincipal'], 'assessment-policy-trust-mismatch')
    hashes, records = {}, {}
    for name in ('post-retirement-health.json', 'external-console-denial.json',
                 'external-etcd-recovery-point.json', 'node-recovery-bundle.json'):
        record, sha = signed.read(args.postflight / name)
        require(record.get('executionAttemptId') == execution['attemptId'] and record.get('planSha256') == plan_sha
                and record.get('sourceClusterUid') == plan['sourceClusterUid']
                and record.get('verificationResult') == 'pass', 'postflight-attempt-or-source-mismatch')
        maximum_age = policy['maximumValidationAgeSeconds']
        if name in ('external-etcd-recovery-point.json', 'node-recovery-bundle.json'):
            maximum_age = min(maximum_age, policy['maximumRecoveryAgeSeconds'])
        fresh(record, maximum_age, after=execution['finishedAt'])
        native.assessment_freshness.append((record, maximum_age, execution['finishedAt']))
        records[name], hashes[name] = record, sha
    health = records['post-retirement-health.json']
    require(all(health.get(k) is True for k in ('nativeApi', 'privateAdministration', 'dns', 'cni', 'storage', 'admission',
        'publicApplications', 'publicOidc')) and all(health.get('authenticatedWorkloads', {}).get(w) is True for w in WORKLOADS),
        'post-retirement-authenticated-health-failed')
    denial = records['external-console-denial.json']
    require(denial.get('externalPathIndependent') is True and denial.get('publicConsoleDenied') is True,
            'post-retirement-external-denial-unverified')
    point, node = records['external-etcd-recovery-point.json'], records['node-recovery-bundle.json']
    validate_recovery_point(point)
    require(point.get('configurationSha256') == node.get('configurationSha256')
            and node.get('recoveryPointSha256') == hashes['external-etcd-recovery-point.json']
            and all(node.get(k) is True for k in ('encrypted', 'independentReadbackVerified', 'configurationMatchesSource',
                                                'ownerModesPreserved', 'testedRecoveryProcedure'))
            and node.get('immutableOffNodeVersion'), 'post-retirement-recovery-handoff-incomplete')
    outcome.update(executionAttemptId=execution['attemptId'], executionResultSha256=execution_sha,
                   planSha256=plan_sha, postflightInputs=hashes, authenticatedPostflight='passed',
                   postRetirementRecovery='fresh-4.1-inputs-retained')
    outcome_sha = digest(canonical(outcome))
    state = 'assessed-awaiting-administrator-acceptance'
    if args.acceptance:
        acceptance, acceptance_sha = signed.read(args.acceptance)
        require(acceptance.get('kind') == 'retirement-acceptance' and acceptance.get('approvedBy') == 'Administrator'
                and acceptance.get('accepted') is True and acceptance.get('executionAttemptId') == execution['attemptId']
                and acceptance.get('outcomeSha256') == outcome_sha and acceptance.get('postflightInputs') == hashes
                and acceptance.get('executionResultSha256') == execution_sha
                and acceptance.get('retainedObjects') == outcome['retainedObjects']
                and isinstance(acceptance.get('incidents'), list)
                and all(v.get('status') == 'resolved' for v in acceptance['incidents']), 'retirement-acceptance-incomplete')
        fresh(acceptance, policy['maximumValidationAgeSeconds'], after=execution['finishedAt'])
        native.assessment_freshness.append((acceptance, policy['maximumValidationAgeSeconds'], execution['finishedAt']))
        outcome.update(acceptanceSha256=acceptance_sha, incidents=acceptance['incidents'])
        state = 'accepted'
    outcome['assessmentOutcomeSha256'] = outcome_sha
    return state, outcome


def validate_accepted_persistence(args, signed, native, source):
    """Run after signature verification, immediately before any accepted bytes are persisted."""
    validate_assessment_inputs(args, native, native.assessment_plan, source)
    require(file_digest(private_file(args.plan, args.project_root)) == native.assessment_plan_sha,
            'assessment-plan-changed')
    signed.assert_unchanged()
    for record, maximum_age, after in native.assessment_freshness:
        fresh(record, maximum_age, after=after)


def run(args):
    tool_binding(args)
    for name in ('kubeconfig', 'allowed_signers', 'receipt_key'):
        private_file(getattr(args, name), args.project_root)
    attempt = Attempt(args.project_root, args.evidence_root, args.attempt_id, 'retirement',
                      readback_recipient=args.readback_recipient, readback_identity=args.readback_identity)
    native, state, outcome, reason = Native(args, attempt), 'failed-closed', {}, 'unobserved'
    started = now()
    attempt.record('attempt.json', {'story': '4.27', 'attemptId': args.attempt_id, 'capturedAt': started,
        'operator': safe(args.operator), 'mode': 'assess' if args.assess_result else 'execute' if args.execute else 'plan',
        **code_binding(args.project_root), 'upgradeGate': 'closed'})
    try:
        if args.assess_result:
            require(args.plan and args.preflight and args.postflight and not args.execute,
                    'complete-read-only-assessment-inputs-required')
            # Validate bound static inputs offline before any authenticated census.
            assessment_plan = json.loads(private_file(args.plan, args.project_root).read_bytes())
            validate_assessment_inputs(args, native, assessment_plan)
        inventory, source = native.census()
        raw = copy.deepcopy(native.raw_inventory_by_uid)
        # This sanitized source record is an explicit input to synthetic fixtures, never source credentials.
        write_new(attempt.directory / 'source-census.json', canonical(source))
        if args.assess_result:
            signed = SignedInputs(args, attempt)
            state, outcome = assess(args, signed, native, inventory, source)
        elif not args.execute:
            plan = make_plan(args, inventory, raw, source, {})
            if args.catalog_rehearsal and args.namespace_rehearsal:
                plan['rehearsals'] = validate_rehearsals(plan, {'catalog': args.catalog_rehearsal, 'namespaces': args.namespace_rehearsal})
            write_new(attempt.directory / 'plan.json', canonical(plan))
            attempt.encrypt('reviewed-before-inventory', canonical(raw), args.age, args.recipient)
            outcome = {'planSha256': digest(canonical(plan)), 'rehearsalsBound': bool(plan['rehearsals']),
                       'proposedDeletionObjects': sum(v['action'] == 'delete' for v in plan['actions']),
                       'proposedNamespaceInterventions': sum(v['action'] == 'remove-named-finalizer' for v in plan['actions'])}
            state = 'proposed'
        else:
            require(args.plan and args.approval and args.preflight and args.backup_bundle
                    and args.catalog_rehearsal and args.namespace_rehearsal
                    and args.arm_attempt == args.attempt_id, 'complete-gate-and-explicit-arm-required')
            plan_path = private_file(args.plan, args.project_root)
            plan_bytes = plan_path.read_bytes()
            plan, plan_sha = json.loads(plan_bytes), digest(plan_bytes)
            require(plan['attemptId'] == args.attempt_id, 'plan-attempt-mismatch')
            validate_plan(args, plan, inventory, raw, source)
            require(plan['rehearsals'] == validate_rehearsals(plan, {
                'catalog': args.catalog_rehearsal, 'namespaces': args.namespace_rehearsal}), 'rehearsal-plan-binding-mismatch')
            signed = SignedInputs(args, attempt)
            approval, approval_sha, policy = validate_preflight(args, signed, plan, plan_sha, raw)
            native.plan, native.approval, native.policy = plan, approval, policy
            native.signed_inputs = signed
            native.original_raw = raw
            native.namespace_modified = set()
            native.reviewed_raw_by_uid = copy.deepcopy(raw)
            # The signed bindings validate this fresh baseline; the shared exact manager Lease
            # checkpoint starts from its observed execution renewal rather than the proposal time.
            native.lease_renewal_floors = validate_plan_content(plan, raw)
            native.reviewed_content_digests = {uid: content_review_digest(v) for uid, v in raw.items()}
            native.retirement_uids = {uid for p in plan['phases'] for uid in p['expected']}
            attempt.encrypt('production-before-inventory', canonical(raw), args.age, args.recipient)
            attempt.signed_record('preflight-result.json', {'state': 'accepted-for-this-attempt',
                'attemptId': args.attempt_id, 'planSha256': plan_sha, 'approvalSha256': approval_sha,
                'sourceClusterUid': plan['sourceClusterUid'], 'capturedAt': now(), 'upgradeGate': 'closed'},
                args.ssh_keygen, args.receipt_key)
            # A successful signing command alone does not prove the receipt key is trusted.
            signed.read(attempt.directory / 'preflight-result.json')
            native.armed = True
            by_uid = {v['uid']: v for v in plan['resources']}
            for phase in plan['phases']:
                native.retire_phase(phase, set(by_uid), by_uid, native.discovery)
            native.armed = False
            final, settled = native.settled_inventory()
            require(settled, 'post-retirement-census-not-settled')
            attempt.encrypt('production-after-inventory', canonical(native.raw_inventory_by_uid), args.age, args.recipient)
            outcome = structural_outcome(native, plan, final)
            outcome.update(planSha256=plan_sha, approvalSha256=approval_sha,
                           sourceClusterUid=plan['sourceClusterUid'], authenticatedPostflight='pending-independent-evidence',
                           postRetirementRecovery='pending-fresh-4.1-inputs')
            state = 'retired-awaiting-acceptance'
    except Exception as error:
        native.armed = False
        reason = str(error) if isinstance(error, ValueError) and re.fullmatch(r'[a-z0-9-]+', str(error)) else 'command-schema-or-custody-failure'
        try:
            attempt.encrypt('failure-diagnostics', canonical({'exception': type(error).__name__, 'detail': str(error),
                'lastStep': native.last_step, 'lastRawInventory': native.raw_inventory_by_uid}), args.age, args.recipient)
        except Exception:
            reason = 'evidence-custody-failed'
    receipt = {'schemaVersion': 1, 'story': '4.27', 'attemptId': args.attempt_id,
        'startedAt': started, 'finishedAt': now(), 'state': state,
        'reasonCode': reason if state == 'failed-closed' else None,
        'mutationRequests': native.mutation_requests, 'incidentDecisionRequired': state == 'failed-closed' and native.mutation_requests > 0,
        'completedPhases': sorted(native.completed_retirement_phases), 'outcome': outcome,
        'rawExports': [{k: v[k] for k in ('file', 'ciphertextSha256', 'readbackVerified')} for v in attempt.exports],
        'productionRetirementAccepted': state == 'accepted', 'upgradeGate': 'closed', 'automaticRecoveryPerformed': False}
    try:
        if state == 'accepted':
            validate_assessment_inputs(args, native, native.assessment_plan, source)
            require(file_digest(private_file(args.plan, args.project_root)) == native.assessment_plan_sha,
                    'assessment-plan-changed')
            signed.assert_unchanged()
            attempt.signed_record('result.json', receipt, args.ssh_keygen, args.receipt_key,
                allowed_signers=args.allowed_signers, principal=args.administrator_principal,
                before_record=lambda: validate_accepted_persistence(args, signed, native, source))
        else:
            attempt.signed_record('result.json', receipt, args.ssh_keygen, args.receipt_key)
    except Exception:
        # A signing failure still retains the stopped attempt; it cannot become accepted.
        receipt.update(state='failed-closed', reasonCode='receipt-signing-failed', productionRetirementAccepted=False)
        attempt.record('result.json', receipt)
        state = 'failed-closed'
    attempt.finish()
    return attempt.directory, state


def parser():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--project-root', type=Path, default=Path(__file__).resolve().parents[2])
    p.add_argument('--evidence-root', type=Path, default=Path.home() / 'hexalith-management-evidence')
    for name in ('attempt-id', 'operator', 'context', 'recipient', 'readback-recipient', 'administrator-principal'):
        p.add_argument('--' + name, required=True)
    for name in ('kubeconfig', 'kubectl', 'helm', 'age', 'ssh-keygen', 'readback-identity', 'allowed-signers', 'receipt-key'):
        p.add_argument('--' + name, type=Path, required=True)
    for name in ('plan', 'approval', 'preflight', 'backup-bundle', 'catalog-rehearsal', 'namespace-rehearsal',
                 'assess-result', 'postflight', 'acceptance'):
        p.add_argument('--' + name, type=Path)
    p.add_argument('--execution-attempt-id', help='New execution custody ID named by a planning attempt; defaults to <planning-id>-execution')
    p.add_argument('--rehearsal-source-inventory', type=Path, help='Exact sanitized census input used by both renewed fixture drivers')
    p.add_argument('--execute', action='store_true', help='Issue only signed exact-attempt approved phase requests')
    p.add_argument('--arm-attempt', help='Must equal the new execution attempt ID; omitted by default')
    return p


def main():
    p = parser()
    args = p.parse_args()
    try:
        directory, state = run(args)
    except Exception:
        p.exit(2, 'Retirement refused: invalid inputs or private custody. No accepted production outcome.\n')
    completion = 'signed retirement accepted' if state == 'accepted' else 'Story 4.27 incomplete'
    print(f'Private retirement attempt: {directory}\nOutcome: {state}; {completion}; upgrade gate closed')
    if state == 'failed-closed':
        raise SystemExit(2)


if __name__ == '__main__':
    main()
