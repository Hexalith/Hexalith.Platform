#!/usr/bin/env python3
"""Story 4.2 local preparation and signed-evidence checks; no network or mutation commands."""
import argparse
from datetime import datetime, timezone
import hashlib
import ipaddress
import json
import os
from pathlib import Path
import re
import stat
import subprocess


ARTIFACTS = Path('_bmad-output/implementation-artifacts')
STORY = ARTIFACTS / '4-2-close-public-admin-exposure-and-anonymous-registry-reads.md'
SOURCES = (STORY, ARTIFACTS / 'epic-4-context.md',
           ARTIFACTS / 'evidence/epic-4/initial-cluster-inventory.md',
           Path('eng/admin-exposure/prepare.py'), Path('eng/admin-exposure/test_prepare.py'),
           Path('eng/admin-exposure/README.md'))
HEX = re.compile(r'[0-9a-f]{64}')
OCI_DIGEST = re.compile(r'sha256:[0-9a-f]{64}')
ID = re.compile(r'[A-Za-z0-9][A-Za-z0-9_.@:-]{0,127}')
PHASE_FILES = {
    'console': ('admin-path-proof.json', 'admin-exposure-result.json'),
    'keycloak': ('admin-path-proof.json', 'admin-exposure-result.json'),
    'registry-auth': ('registry-consumer-inventory.json', 'registry-auth-result.json'),
    'registry-gc': ('registry-consumer-inventory.json', 'retained-oci-closure.json', 'registry-gc-result.json'),
}
COMMON_FILES = ('signed-baseline.json', 'administrator-decisions.json', 'pre-mutation-state.json')
FORBIDDEN_FIELDS = {'authorization', 'cookie', 'cookies', 'set-cookie', 'token', 'password',
                    'accesstoken', 'refreshtoken', 'idtoken', 'clientsecret', 'secretdata',
                    'credentials', 'responsebody', 'headers'}
ADMIN_PATHS = ('/admin', '/admin/', '/admin/master/console/', '/realms/master',
               '/realms/master/.well-known/openid-configuration',
               '/realms/master/protocol/openid-connect/token')
ADMINISTRATOR = 'jpiquot'
ADMINISTRATION_POLICY = 'sole-administrator'
RECOVERY_SCOPE = 'isolated-client-session'
ADMINISTRATION_SURFACES = {'nativeCluster', 'keycloak'}
HTTP_METHODS = {'GET', 'HEAD', 'POST', 'PUT', 'PATCH', 'DELETE', 'OPTIONS', 'CONNECT', 'TRACE'}


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def canonical(record):
    return (json.dumps(record, indent=2, sort_keys=True, allow_nan=False) + '\n').encode()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def file_digest(path):
    return digest(Path(path).read_bytes())


def write_new(path, data):
    fd = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as stream:
        stream.write(data)


def private_path(path, project, *, file=False):
    path = Path(path).absolute()
    require(not any(p.is_symlink() for p in (path, *path.parents)), 'symlink-in-private-path')
    require(path.resolve() != project.resolve() and project.resolve() not in path.resolve().parents,
            'private-evidence-must-be-outside-repository')
    if file:
        require(path.is_file(), 'missing-private-input')
    if path.exists():
        require(path.stat().st_uid == os.getuid() and not stat.S_IMODE(path.stat().st_mode) & 0o077,
                'private-custody-must-be-owner-only')
    return path


def timestamp(value):
    require(isinstance(value, str), 'missing-evidence-time')
    try:
        parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    except ValueError:
        raise ValueError('invalid-evidence-time') from None
    require(parsed.tzinfo is not None, 'invalid-evidence-time')
    return parsed


def fresh(record, clock):
    require(timestamp(record.get('capturedAt')) <= clock < timestamp(record.get('expiresAt')),
            'stale-or-future-evidence')


def sha(value):
    return isinstance(value, str) and HEX.fullmatch(value) is not None


def oci_digest(value):
    return isinstance(value, str) and OCI_DIGEST.fullmatch(value) is not None


def identifier(value):
    return isinstance(value, str) and ID.fullmatch(value) is not None


def hostname(value):
    return (isinstance(value, str) and len(value) <= 253
            and all(re.fullmatch(r'[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?', label)
                    for label in value.split('.')))


def absolute_http_path(value):
    return (isinstance(value, str) and value.startswith('/')
            and not any(v.isspace() or ord(v) < 32 or ord(v) == 127 for v in value)
            and '#' not in value)


def string_array(value, validator):
    return isinstance(value, list) and bool(value) and all(validator(v) for v in value)


def no_sensitive_fields(value):
    if isinstance(value, dict):
        for name, child in value.items():
            require(name.lower().replace('_', '').replace('-', '') not in
                    {v.replace('-', '') for v in FORBIDDEN_FIELDS}, 'prohibited-evidence-field')
            no_sensitive_fields(child)
    elif isinstance(value, list):
        for child in value:
            no_sensitive_fields(child)


def unique(records, field, validator=identifier):
    require(isinstance(records, list) and all(isinstance(v, dict) and validator(v.get(field)) for v in records),
            'invalid-evidence-list')
    result = {v[field]: v for v in records}
    require(len(result) == len(records), 'duplicate-evidence-identity')
    return result


def pending_recovery():
    return {name: None for name in ('operator', 'pathId', 'custodyId', 'accountBindings',
        'qualificationScope', 'testSessionId', 'ordinaryCredentialsUnavailable', 'publicOidcUnavailable',
        'isolationEvidenceSha256', 'productionAccountsAvailableToOtherClients',
        'productionPublicOidcAvailableToOtherClients', 'productionAuthenticationUnchangedDuringQualification',
        'productionAvailabilityEvidenceSha256', 'separatelyProtectedRecoveryAccess',
        'independentOfOrdinaryCredentials', 'credentialLineageEvidenceSha256',
        'nativeClusterAuthentication', 'nativeClusterNonDestructiveRead', 'keycloakAdminLogin',
        'keycloakNonDestructiveRead', 'result', 'evidenceSha256', 'capturedAt')}


def templates(attempt_id, operator, source_hashes):
    base = {'schemaVersion': 1, 'story': '4.2', 'attemptId': attempt_id,
            'classification': 'local-preparation', 'operator': operator, 'capturedAt': None,
            'observationStartedAt': None, 'expiresAt': None, 'sourceClusterUid': None,
            'verificationResult': 'not-run'}
    generation = {'generationSha256': None, 'changeControl': 'not-established',
                  'consumerSetSha256': None, 'releaseRollbackSetSha256': None,
                  'credentialReferencesSha256': None, 'writerReplicationSetSha256': None,
                  'registryConfigSha256': None, 'approvedTargetConfigSha256': None}
    return {
        'attempt.json': {**base, 'recordedAt': datetime.now(timezone.utc).isoformat(timespec='seconds'),
                         'sourceHashes': source_hashes, 'signed': False, 'mutationAuthorized': False,
                         'liveEvidenceCollected': False, 'complete': False},
        'signed-baseline.json': {**base, 'resources': [], 'routes': [], 'registryGeneration': generation},
        'administrator-decisions.json': {**base, 'approvedBy': None, 'privatePathId': None,
            'administrationPolicy': None, 'administrator': None, 'recoveryCustodyId': None,
            'administratorAccountBindings': {'nativeCluster': None, 'keycloak': None},
            'recoveryAccountBindings': {'nativeCluster': None, 'keycloak': None},
            'privateAdministrationTargets': {surface: {'hostname': None, 'path': None, 'method': None}
                for surface in sorted(ADMINISTRATION_SURFACES)},
            'operators': [], 'breakGlassPathId': None, 'monitoringOwner': None,
            'approvedPublicOidcChecks': [], 'consoleMode': 'port-forward',
            'publicConsoleHostnames': ['kube.hexalith.com'], 'keycloakHostname': 'auth.tache.ai',
            'registryHostname': 'registry.hexalith.com', 'registryCredentialOwner': None,
            'affectedRouteUids': {}, 'publicCatchAllIngressUid': None,
            'credentialRotationPolicySha256': None, 'cutoverWindowStart': None, 'cutoverWindowEnd': None,
            'retainedDigestSourceSha256': None, 'gcPolicySha256': None,
            'privateProofSha256': None, 'inventorySha256': None,
            'retainedClosureRecordSha256': None, 'gcRehearsalEvidenceSha256': None,
            'baselineSha256': None, 'productionGo': False},
        'pre-mutation-state.json': {**base, 'forMutation': None, 'observedImmediatelyBeforeMutation': False,
            'resources': [], 'routes': [], 'registryGeneration': generation},
        'admin-path-proof.json': {**base, 'privatePathId': None, 'testedOperators': [],
            'administrationPolicy': None, 'administrator': None,
            'unauthorizedPrivateChecks': [], 'breakGlass': pending_recovery(), 'publicOidcChecks': []},
        'admin-exposure-result.json': {**base, 'closedSurface': None, 'baselineSha256': None,
            'administrationPolicy': None, 'administrator': None,
            'privateProofSha256': None, 'externalProbes': [], 'closedAdminPaths': [],
            'consoleMode': 'port-forward', 'publicOidcChecks': [], 'removedConsoleObjects': None,
            'postChangePrivateChecks': [], 'postChangeBreakGlass': pending_recovery(),
            'postChangeUnauthorizedPrivateChecks': [],
            'mutationStartedAt': None, 'mutationFinishedAt': None, 'accepted': False},
        'registry-consumer-inventory.json': {**base, 'generation': generation,
            'inventoryComplete': False, 'coverage': {k: False for k in (
                'imagePullSecretsAndServiceAccounts', 'nodesAndRuntimes', 'forgejoWorkflows',
                'deploymentExecutors', 'humanReaders', 'publicationAndOperationsWriters',
                'replicationAndOffsiteRobots', 'liveAndRollbackAndRetainedReleases')},
            'consumers': []},
        'registry-auth-result.json': {**base, 'generation': generation, 'inventorySha256': None,
            'anonymousProbes': [], 'authenticatedOperations': [],
            'mutationStartedAt': None, 'mutationFinishedAt': None, 'accepted': False},
        'retained-oci-closure.json': {**base, 'generation': generation,
            'retainedSourceSha256': None, 'roots': [], 'objects': [], 'closureSha256': None,
            'referrerEnumerationComplete': False},
        'registry-gc-result.json': {**base, 'generation': generation, 'inventorySha256': None,
            'closureSha256': None, 'gcConfigSha256': None, 'excludedObjectDigests': [],
            'gcStartedAt': None, 'gcFinishedAt': None,
            'rehearsal': None, 'writeReplicationLock': None, 'postGcOperations': [], 'accepted': False},
        # This is the existing 4.27 contract, deliberately unusable until an actual accepted closure.
        'console-closure.json': {**base, 'attemptId': None, 'planSha256': None, 'accepted': False,
            'externalPathIndependent': False, 'publicConsoleDenied': False, 'publicOidcPassed': False,
            'adminExposureResultSha256': None, 'adminPathProofSha256': None,
            'handoff': 'Populate for the exact 4.27 plan/attempt after measured production closure; '
                       'sign under hexalith-retirement, not hexalith-admin-exposure'},
    }


def prepare(project, evidence_root, attempt_id, operator):
    project = Path(project).resolve()
    require(re.fullmatch(r'[a-z0-9][a-z0-9-]{0,79}', attempt_id) is not None, 'invalid-attempt-id')
    require(identifier(operator), 'invalid-operator-identity')
    root = private_path(evidence_root, project)
    directory = root / 'evidence/epic-4/4-2' / attempt_id
    for path in (directory, *directory.parents):
        if path == root.parent:
            break
        private_path(path, project)
    recovery_roots = (Path.home() / 'hexalith-upgrade-evidence', Path.home() / 'hexalith-recovery-evidence')
    require(not any(directory.resolve() == p.resolve() or p.resolve() in directory.resolve().parents
                    for p in recovery_roots), 'must-not-write-into-recovery-custody')
    source_hashes = {str(p): file_digest(project / p) for p in SOURCES}
    require(not directory.exists(), 'attempt-already-exists')
    old = os.umask(0o077)
    try:
        directory.mkdir(mode=0o700, parents=True, exist_ok=False)
        for name, record in templates(attempt_id, operator, source_hashes).items():
            write_new(directory / name, canonical(record))
        write_new(directory / 'SHA256SUMS', ''.join(
            f'{file_digest(p)}  {p.name}\n' for p in sorted(directory.iterdir())).encode())
    finally:
        os.umask(old)
    return directory


def validate_snapshot(snapshot):
    resources = unique(snapshot.get('resources'), 'uid')
    require(resources, 'empty-resource-baseline')
    identities = set()
    for resource in resources.values():
        require(isinstance(resource.get('apiVersion'), str) and
                re.fullmatch(r'[A-Za-z0-9.-]+(?:/[A-Za-z0-9]+)?', resource['apiVersion']) is not None
                and all(identifier(resource.get(k)) for k in ('kind', 'name', 'resourceVersion'))
                and (resource.get('namespace') is None or identifier(resource['namespace']))
                and sha(resource.get('effectiveConfigSha256')), 'incomplete-resource-baseline')
        key = tuple(resource.get(k) for k in ('apiVersion', 'kind', 'namespace', 'name'))
        require(key not in identities, 'duplicate-resource-baseline')
        identities.add(key)
        if resource['kind'] == 'Ingress':
            require(string_array(resource.get('hostnames'), lambda v: hostname(v.removeprefix('*.'))
                        if isinstance(v, str) else False)
                    and string_array(resource.get('paths'), absolute_http_path),
                    'invalid-ingress-hostname-or-path-array')
    routes = unique(snapshot.get('routes'), 'hostname', hostname)
    require(routes, 'empty-route-baseline')
    for route in routes.values():
        uids = route.get('ingressUids')
        require(string_array(uids, identifier) and len(set(uids)) == len(uids)
                and all(v in resources for v in uids) and identifier(route.get('ingressClass'))
                and isinstance(route.get('dnsAnswers'), list) and route['dnsAnswers'], 'incomplete-route-baseline')
        try:
            addresses = [str(ipaddress.ip_address(v)) for v in route['dnsAnswers']]
        except (ValueError, TypeError):
            raise ValueError('invalid-dns-answer') from None
        require(len(set(addresses)) == len(addresses), 'duplicate-dns-answer')
        backends = unique(route.get('backends'), 'serviceUid')
        require(backends and all(identifier(v.get('name')) and identifier(v.get('namespace'))
                and sha(v.get('effectiveConfigSha256')) for v in backends.values()), 'incomplete-backend-baseline')
        require(all(v['serviceUid'] in resources and resources[v['serviceUid']]['kind'] == 'Service'
                and all(resources[v['serviceUid']].get(k) == v.get(k) for k in
                        ('name', 'namespace', 'effectiveConfigSha256')) for v in backends.values()),
                'backend-not-bound-to-reviewed-resource')


def ingress_hostname_matches(rule, target):
    return (rule == target or rule.startswith('*.') and target.endswith(rule[1:])
            and target.count('.') == rule.count('.'))


def target_baseline(phase, baseline, decisions):
    validate_snapshot(baseline)
    if phase == 'console':
        hosts = decisions.get('publicConsoleHostnames')
        require(string_array(hosts, hostname) and 'kube.hexalith.com' in hosts
                and len(set(hosts)) == len(hosts), 'console-hostname-inventory-missing')
    else:
        hosts = [decisions.get('keycloakHostname') if phase == 'keycloak' else decisions.get('registryHostname')]
        require(all(hostname(v) for v in hosts), 'approved-target-hostname-missing')
    routes = unique(baseline['routes'], 'hostname', hostname)
    resources = unique(baseline['resources'], 'uid')
    approvals = decisions.get('affectedRouteUids')
    require(isinstance(approvals, dict), 'invalid-affected-route-approval-map')
    expected = approvals.get(phase)
    require(string_array(expected, identifier) and len(set(expected)) == len(expected),
            'approved-target-route-baseline-missing')
    namespace = 'keycloak' if phase == 'keycloak' else (
        'kubesphere-system' if phase == 'console' else 'registry-distribution')
    require(all(uid in resources and resources[uid]['namespace'] == namespace
                and resources[uid]['kind'] == 'Ingress' for uid in expected),
            'approved-target-resource-identity-mismatch')
    if phase == 'keycloak':
        hosts = sorted(set(hosts) | {host for uid in expected for host in resources[uid]['hostnames']
                                   if not host.startswith('*.')}
                       | {host for host, route in routes.items() if set(route['ingressUids']) & set(expected)})
    require(all(host in routes for host in hosts)
            and set(expected) == {uid for host in hosts for uid in routes[host]['ingressUids']},
            'approved-target-route-baseline-missing')
    require(all(resources[uid]['namespace'] == namespace and resources[uid]['kind'] == 'Ingress'
                and any(ingress_hostname_matches(rule, host) for rule in resources[uid]['hostnames'])
                for host in hosts for uid in routes[host]['ingressUids']),
            'approved-target-resource-identity-mismatch')
    if phase == 'keycloak':
        catch_all = decisions.get('publicCatchAllIngressUid')
        require(catch_all in expected and '/' in resources[catch_all].get('paths', []),
                'public-keycloak-catch-all-not-reviewed')
    return hosts


def in_record_time(child, parent, *, after=None, before=None):
    observed = timestamp(child.get('capturedAt'))
    require(timestamp(parent.get('observationStartedAt')) <= observed <= timestamp(parent.get('capturedAt'))
            and (after is None or observed >= timestamp(after))
            and (before is None or observed <= timestamp(before)), 'nested-evidence-outside-record-or-operation')


def mutation_times(result, current):
    started, finished = result.get('mutationStartedAt'), result.get('mutationFinishedAt')
    require(timestamp(current['capturedAt']) <= timestamp(started) <= timestamp(finished)
            and timestamp(started) < timestamp(current['expiresAt'])
            and timestamp(result['observationStartedAt']) <= timestamp(started)
            and timestamp(finished) <= timestamp(result['capturedAt']), 'mutation-sequencing-unestablished')
    return started, finished


def prior_approval(phase, records, hashes):
    """A later signed production go cannot approve an earlier checkpoint or mutation."""
    baseline, decisions, current = (records[name] for name in COMMON_FILES)
    result = records[PHASE_FILES[phase][-1]]
    started = timestamp(result.get('gcStartedAt' if phase == 'registry-gc' else 'mutationStartedAt'))
    finished = timestamp(result.get('gcFinishedAt' if phase == 'registry-gc' else 'mutationFinishedAt'))
    approved = timestamp(decisions.get('capturedAt'))
    require(timestamp(baseline.get('capturedAt')) <= approved
            <= timestamp(current.get('observationStartedAt')) <= timestamp(current.get('capturedAt'))
            <= started <= finished, 'production-approval-not-before-fresh-checkpoint-and-mutation')
    prerequisites = [('admin-path-proof.json', 'privateProofSha256')] if phase in ('console', 'keycloak') else [
        ('registry-consumer-inventory.json', 'inventorySha256')]
    if phase == 'registry-gc':
        prerequisites.append(('retained-oci-closure.json', 'retainedClosureRecordSha256'))
    for name, binding in prerequisites:
        require(decisions.get(binding) == hashes[name], 'production-approval-prerequisite-binding-mismatch')
        require(timestamp(records[name].get('capturedAt')) <= approved,
                'production-approval-precedes-completed-prerequisite')
    if phase == 'registry-gc':
        rehearsal = result.get('rehearsal')
        require(isinstance(rehearsal, dict) and sha(decisions.get('gcRehearsalEvidenceSha256'))
                and decisions['gcRehearsalEvidenceSha256'] == rehearsal.get('evidenceSha256')
                and timestamp(rehearsal.get('capturedAt')) <= approved,
                'production-gc-approval-before-successful-rehearsal-unproved')
    require(all(finished < timestamp(records[name].get('expiresAt'))
                for name in (*COMMON_FILES, *(name for name, _ in prerequisites))),
            'production-approval-or-prerequisite-expired-during-mutation')


def compare_baseline(baseline, current):
    """No relaxed resourceVersion or UID handling for 4.2 mutations."""
    validate_snapshot(baseline)
    validate_snapshot(current)
    for field in ('sourceClusterUid', 'resources', 'routes', 'registryGeneration'):
        require(baseline.get(field) == current.get(field), 'baseline-drift-reinventory-and-reapprove')
    require(current.get('observedImmediatelyBeforeMutation') is True, 'immediate-reread-unestablished')


def refused(probe, *, external=True):
    require(isinstance(probe, dict), 'missing-denial-probe')
    require(probe.get('sourceAddressCategory') == ('external-public' if external else 'unauthorized-private')
            and probe.get('backendReached') is False and sha(probe.get('routingEvidenceSha256')),
            'denial-routing-or-vantage-unestablished')
    timestamp(probe.get('capturedAt'))
    outcome, status = probe.get('outcome'), probe.get('status')
    require((outcome == 'refused' and type(status) is int and status in (401, 403)
             and probe.get('decisionBy') in ('ingress', 'firewall', 'private-gateway'))
            or (outcome == 'not-routed' and status in (None, 404, 410)
                and probe.get('decisionBy') in ('dns', 'ingress', 'firewall')),
            'redirect-login-rate-limit-or-application-response-is-not-closure')


def oidc_checks(decisions, checks):
    approved = decisions.get('approvedPublicOidcChecks')
    require(isinstance(approved, list) and approved and all(identifier(v) for v in approved)
            and len(set(approved)) == len(approved), 'approved-public-oidc-tests-missing')
    results = unique(checks, 'id')
    require(set(results) == set(approved) and all(v.get('result') == 'pass' and
            sha(v.get('evidenceSha256')) for v in results.values()), 'public-oidc-regression-stop-and-rollback')


def account_bindings(value):
    return (isinstance(value, dict) and set(value) == {'nativeCluster', 'keycloak'}
            and all(identifier(v) for v in value.values()))


def unauthorized_private_checks(decisions, checks, parent, *, after=None, before=None):
    targets = decisions.get('privateAdministrationTargets')
    require(isinstance(targets, dict) and set(targets) == ADMINISTRATION_SURFACES
            and all(isinstance(v, dict) and hostname(v.get('hostname'))
                    and absolute_http_path(v.get('path')) and isinstance(v.get('method'), str)
                    and v['method'] in HTTP_METHODS
                    for v in targets.values()), 'approved-private-administration-targets-missing')
    by_surface = unique(checks, 'surface')
    require(set(by_surface) == ADMINISTRATION_SURFACES, 'private-refusal-surface-coverage-incomplete')
    for surface, check in by_surface.items():
        target = targets[surface]
        require(check.get('privatePathId') == decisions.get('privatePathId')
                and all(check.get(k) == target[k] for k in ('hostname', 'path', 'method')),
                'private-refusal-target-binding-mismatch')
        refused(check, external=False)
        in_record_time(check, parent, after=after, before=before)


def recovery_proof(decisions, recovery, parent, *, after=None):
    require(identifier(decisions.get('breakGlassPathId')) and identifier(decisions.get('recoveryCustodyId'))
            and account_bindings(decisions.get('recoveryAccountBindings'))
            and isinstance(recovery, dict) and recovery.get('operator') == ADMINISTRATOR
            and recovery.get('pathId') == decisions['breakGlassPathId']
            and recovery.get('custodyId') == decisions['recoveryCustodyId']
            and recovery.get('accountBindings') == decisions['recoveryAccountBindings']
            and recovery.get('result') == 'pass' and sha(recovery.get('evidenceSha256'))
            and sha(recovery.get('credentialLineageEvidenceSha256'))
            and all(recovery.get(k) is True for k in ('ordinaryCredentialsUnavailable', 'publicOidcUnavailable',
                'separatelyProtectedRecoveryAccess', 'independentOfOrdinaryCredentials',
                'nativeClusterAuthentication', 'nativeClusterNonDestructiveRead',
                'keycloakAdminLogin', 'keycloakNonDestructiveRead')),
            'independent-recovery-custody-authentication-and-reads-unproved')
    require(recovery.get('qualificationScope') == RECOVERY_SCOPE
            and identifier(recovery.get('testSessionId'))
            and sha(recovery.get('isolationEvidenceSha256'))
            and sha(recovery.get('productionAvailabilityEvidenceSha256'))
            and all(recovery.get(k) is True for k in ('productionAccountsAvailableToOtherClients',
                'productionPublicOidcAvailableToOtherClients', 'productionAuthenticationUnchangedDuringQualification')),
            'recovery-isolation-or-production-availability-unproved')
    in_record_time(recovery, parent, after=after)


def admin_proof(decisions, proof):
    operators = decisions.get('operators')
    require(decisions.get('administrationPolicy') == ADMINISTRATION_POLICY
            and decisions.get('administrator') == ADMINISTRATOR and operators == [ADMINISTRATOR],
            'approved-sole-administrator-policy-required')
    require(proof.get('administrationPolicy') == ADMINISTRATION_POLICY
            and proof.get('administrator') == ADMINISTRATOR, 'sole-administrator-proof-binding-mismatch')
    require(identifier(decisions.get('privatePathId')) and proof.get('privatePathId') == decisions['privatePathId']
            and identifier(decisions.get('monitoringOwner')), 'approved-private-path-or-monitoring-missing')
    tested = unique(proof.get('testedOperators'), 'operator')
    require(decisions.get('consoleMode') in ('port-forward', 'removed'), 'unapproved-console-access-mode')
    checks = ('keycloakAdminLogin', 'keycloakNonDestructiveRead', 'clusterAdminRead')
    if decisions['consoleMode'] == 'port-forward':
        checks += ('consolePortForward',)
    require(set(tested) == set(operators) and all(all(v.get(k) is True for k in checks)
        and sha(v.get('evidenceSha256')) for v in tested.values())
        and account_bindings(decisions.get('administratorAccountBindings'))
        and all(v.get('accountBindings') == decisions['administratorAccountBindings'] for v in tested.values()),
        'authorized-administration-unproved')
    for check in tested.values():
        in_record_time(check, proof)
    unauthorized_private_checks(decisions, proof.get('unauthorizedPrivateChecks'), proof,
                                before=decisions.get('capturedAt'))
    recovery_proof(decisions, proof.get('breakGlass'), proof)
    oidc_checks(decisions, proof.get('publicOidcChecks'))
    for check in proof['publicOidcChecks']:
        in_record_time(check, proof)


def admin_result(phase, decisions, proof, result, hashes, current):
    admin_proof(decisions, proof)
    started, finished = mutation_times(result, current)
    for check in (*proof['testedOperators'], *proof['unauthorizedPrivateChecks'], proof['breakGlass'],
                  *proof['publicOidcChecks']):
        in_record_time(check, proof, before=started)
    require(result.get('closedSurface') == phase and result.get('baselineSha256') == hashes['signed-baseline.json']
            and result.get('privateProofSha256') == hashes['admin-path-proof.json']
            and result.get('administrationPolicy') == ADMINISTRATION_POLICY
            and result.get('administrator') == ADMINISTRATOR, 'admin-result-binding-mismatch')
    probes = result.get('externalProbes')
    require(isinstance(probes, list) and probes, 'external-probes-missing')
    observed = set()
    for probe in probes:
        refused(probe)
        in_record_time(probe, result, after=finished)
        require(absolute_http_path(probe.get('path')) and hostname(probe.get('hostname'))
                and isinstance(probe.get('method'), str), 'probe-target-missing')
        observed.add((probe['hostname'], probe['path'], probe.get('method')))
    if phase == 'console':
        hosts = decisions.get('publicConsoleHostnames')
        require(string_array(hosts, hostname) and 'kube.hexalith.com' in hosts and len(set(hosts)) == len(hosts),
                'console-hostname-inventory-missing')
        require(all((host, path, 'GET') in observed for host in hosts for path in ('/', '/login')),
                'console-external-negative-coverage-incomplete')
        require(result.get('consoleMode') == decisions.get('consoleMode'),
                'approved-console-access-mode-mismatch')
        if decisions['consoleMode'] == 'removed':
            removed = result.get('removedConsoleObjects')
            require(isinstance(removed, dict) and all(removed.get(v) is True for v in
                    ('ingressAbsent', 'serviceAbsent', 'workloadAbsent', 'publicDnsAbsent'))
                    and sha(removed.get('evidenceSha256')), 'removed-console-surface-still-present')
    else:
        hosts = target_baseline('keycloak', current, decisions)
        paths = result.get('closedAdminPaths')
        require(string_array(paths, absolute_http_path) and set(ADMIN_PATHS) <= set(paths),
                'master-or-admin-path-coverage-incomplete')
        require(all((host, path, method) in observed for host in hosts for path in paths for method in ('GET', 'POST')),
                'master-or-admin-external-negative-coverage-incomplete')
    oidc_checks(decisions, result.get('publicOidcChecks'))
    for check in result['publicOidcChecks']:
        in_record_time(check, result, after=finished)
    operators = unique(result.get('postChangePrivateChecks'), 'operator')
    required = ('keycloakAdminLogin', 'keycloakNonDestructiveRead', 'clusterAdminRead')
    if decisions['consoleMode'] == 'port-forward':
        required += ('consolePortForward',)
    require(set(operators) == set(decisions['operators']) and all(all(v.get(k) is True for k in required)
            and sha(v.get('evidenceSha256')) and v.get('accountBindings') == decisions['administratorAccountBindings']
            for v in operators.values()), 'post-change-private-administration-unproved')
    for check in operators.values():
        in_record_time(check, result, after=finished)
    unauthorized_private_checks(decisions, result.get('postChangeUnauthorizedPrivateChecks'), result, after=finished)
    recovery_proof(decisions, result.get('postChangeBreakGlass'), result, after=finished)


def registry_generation(generation):
    require(isinstance(generation, dict) and generation.get('changeControl') in ('frozen', 'fully-reinventoried')
            and all(sha(generation.get(k)) for k in ('generationSha256', 'consumerSetSha256',
                'releaseRollbackSetSha256', 'credentialReferencesSha256', 'writerReplicationSetSha256',
                'registryConfigSha256', 'approvedTargetConfigSha256')), 'signed-registry-generation-incomplete')


def audited_operation(operation, consumer, generation, *, transfer=True, blob=False):
    require(isinstance(operation, dict) and operation.get('consumerId') == consumer.get('id')
            and operation.get('principal') == consumer.get('principal')
            and operation.get('generationSha256') == generation['generationSha256']
            and operation.get('result') == 'pass' and sha(operation.get('auditEvidenceSha256'))
            and identifier(operation.get('auditCorrelationId')), 'authenticated-audit-operation-missing')
    require(string_array(consumer.get('approvedRepositories'), lambda v: isinstance(v, str)
                        and re.fullmatch(r'[a-z0-9][a-z0-9._/-]*', v) is not None), 'invalid-repository-scope')
    require(operation.get('repository') in consumer['approvedRepositories']
            and operation.get('operation') == {'reader': 'pull', 'writer': 'push', 'replicator': 'replicate'}.get(consumer.get('role')),
            'audited-operation-outside-approved-scope')
    if consumer.get('role') == 'replicator':
        require(consumer.get('replicationDirection') in ('source', 'destination')
                and operation.get('replicationDirection') == consumer['replicationDirection'],
                'replication-direction-unproved')
    timestamp(operation.get('capturedAt'))
    if transfer:
        require(operation.get('emptyDisposableContentStore') is True
                and (operation.get('blobTransferred') is True if blob else
                     operation.get('manifestTransferred') is True and operation.get('blobsTransferred') is True)
                and oci_digest(operation.get('returnedDigest'))
                and operation.get('returnedDigest') == operation.get('requestedDigest'),
                'cached-start-or-unaudited-digest-transfer-is-not-proof')


def least_privilege(role, permissions):
    require(isinstance(permissions, dict) and sha(permissions.get('evidenceSha256')),
            'least-privilege-tests-missing')
    timestamp(permissions.get('capturedAt'))
    if role == 'reader':
        require(permissions.get('pushDenied') is True and permissions.get('deleteDenied') is True,
                'reader-write-or-delete-not-denied')
    else:
        require(permissions.get('approvedOperationPassed') is True and permissions.get('outOfScopeDenied') is True
                and permissions.get('retainedDeleteDenied') is True, 'writer-or-replicator-scope-unproved')


def registry_inventory(inventory):
    generation = inventory.get('generation')
    registry_generation(generation)
    expected_coverage = set(templates('unused', 'unused', {})['registry-consumer-inventory.json']['coverage'])
    require(inventory.get('inventoryComplete') is True and isinstance(inventory.get('coverage'), dict)
            and set(inventory['coverage']) == expected_coverage and all(v is True for v in inventory['coverage'].values()),
            'registry-consumer-inventory-incomplete')
    consumers = unique(inventory.get('consumers'), 'id')
    require(consumers, 'registry-consumers-missing')
    for consumer in consumers.values():
        role = consumer.get('role')
        require(role in ('reader', 'writer', 'replicator') and identifier(consumer.get('principal'))
                and identifier(consumer.get('credentialSecretReference'))
                and consumer.get('approvedRepositories') and consumer.get('retainedDeleteDenied') is True,
                'least-privilege-consumer-incomplete')
        require(string_array(consumer['approvedRepositories'], lambda v: isinstance(v, str)
                        and re.fullmatch(r'[a-z0-9][a-z0-9._/-]*', v) is not None), 'invalid-repository-scope')
        least_privilege(role, consumer.get('leastPrivilege'))
        in_record_time(consumer['leastPrivilege'], inventory)
        audited_operation(consumer.get('beforeCutoverOperation'), consumer, generation, transfer=role != 'writer')
        in_record_time(consumer['beforeCutoverOperation'], inventory)
    for reader in (v for v in consumers.values() if v['role'] == 'reader'):
        for writer in (v for v in consumers.values() if v['role'] == 'writer'):
            require(not (reader['principal'] == writer['principal']
                    and reader['credentialSecretReference'] == writer['credentialSecretReference']
                    and set(reader['approvedRepositories']) & set(writer['approvedRepositories'])),
                    'inconsistent-reader-writer-grant')
    return consumers


def registry_result(decisions, inventory, result, hashes, current):
    consumers = registry_inventory(inventory)
    started, finished = mutation_times(result, current)
    for consumer in consumers.values():
        in_record_time(consumer['beforeCutoverOperation'], inventory, before=started)
        in_record_time(consumer['leastPrivilege'], inventory, before=started)
    require(result.get('generation') == inventory['generation'] and
            result.get('inventorySha256') == hashes['registry-consumer-inventory.json'], 'registry-generation-drift')
    probes = unique(result.get('anonymousProbes'), 'kind')
    require(set(probes) == {'catalog', 'tag', 'manifest', 'blob'}, 'anonymous-read-coverage-incomplete')
    for probe in probes.values():
        require(probe.get('status') in (401, 403) and probe.get('result') == 'refused'
                and probe.get('sourceAddressCategory') == 'external-public'
                and sha(probe.get('evidenceSha256')), 'anonymous-read-still-succeeds-or-unproved')
        in_record_time(probe, result, after=finished)
        path = probe.get('path')
        kind = probe['kind']
        require(probe.get('hostname') == decisions.get('registryHostname') and isinstance(path, str)
                and (path == '/v2/_catalog' if kind == 'catalog' else
                     re.fullmatch(r'/v2/[a-z0-9][a-z0-9._/-]*/tags/list', path) is not None if kind == 'tag' else
                     re.fullmatch(r'/v2/[a-z0-9][a-z0-9._/-]*/' +
                                  ('manifests' if kind == 'manifest' else 'blobs') + r'/sha256:[0-9a-f]{64}', path) is not None)
                and (kind == 'catalog' or probe.get('knownExistingContent') is True),
                'anonymous-probe-target-or-existing-content-unbound')
    operations = unique(result.get('authenticatedOperations'), 'consumerId')
    require(set(operations) == set(consumers), 'cutover-consumer-coverage-incomplete')
    for identity, operation in operations.items():
        audited_operation(operation, consumers[identity], inventory['generation'], transfer=consumers[identity]['role'] != 'writer')
        least_privilege(consumers[identity]['role'], operation.get('leastPrivilege'))
        in_record_time(operation['leastPrivilege'], result, after=finished)
        in_record_time(operation, result, after=finished)


def reachability(closure):
    registry_generation(closure.get('generation'))
    roots, objects = closure.get('roots'), closure.get('objects')
    require(isinstance(roots, list) and roots and all(oci_digest(v) for v in roots)
            and len(set(roots)) == len(roots) and isinstance(objects, list) and objects,
            'retained-oci-root-or-object-set-missing')
    by_digest = {}
    allowed_kinds = {'index', 'manifest', 'config', 'layer', 'artifact', 'referrer', 'signature', 'attestation', 'chart'}
    for obj in objects:
        require(isinstance(obj, dict) and oci_digest(obj.get('digest')) and obj.get('kind') in allowed_kinds
                and isinstance(obj.get('references'), list) and all(oci_digest(v) for v in obj['references']),
                'invalid-oci-object')
        require(obj['digest'] not in by_digest, 'duplicate-oci-object')
        by_digest[obj['digest']] = obj
    require(set(roots) <= set(by_digest) and all(set(v['references']) <= set(by_digest) for v in objects),
            'oci-reachability-closure-missing-child-or-referrer')
    reached, pending = set(), list(roots)
    while pending:
        current = pending.pop()
        if current not in reached:
            reached.add(current)
            pending.extend(by_digest[current]['references'])
    require(reached == set(by_digest) and closure.get('referrerEnumerationComplete') is True
            and sha(closure.get('retainedSourceSha256')), 'oci-closure-source-or-referrer-enumeration-unproved')
    require(closure.get('closureSha256') == digest(canonical({'roots': sorted(roots),
            'objects': sorted(objects, key=lambda v: v['digest'])})), 'oci-closure-hash-mismatch')
    return reached


def gc_result(decisions, inventory, closure, result, hashes, current):
    consumers = registry_inventory(inventory)
    objects = reachability(closure)
    require(closure['generation'] == inventory['generation'] == result.get('generation')
            and closure['retainedSourceSha256'] == decisions.get('retainedDigestSourceSha256')
            and result.get('inventorySha256') == hashes['registry-consumer-inventory.json']
            and result.get('closureSha256') == closure['closureSha256'] and sha(decisions.get('gcPolicySha256'))
            and result.get('gcConfigSha256') == inventory['generation']['approvedTargetConfigSha256'],
            'gc-signed-source-or-generation-mismatch')
    require(isinstance(result.get('excludedObjectDigests'), list) and
            objects <= set(result['excludedObjectDigests']), 'gc-may-delete-retained-object')
    rehearsal = result.get('rehearsal')
    require(isinstance(rehearsal, dict) and rehearsal.get('result') == 'pass'
            and rehearsal.get('retainedContentPreserved') is True and rehearsal.get('disposableContentDeleted') is True
            and sha(rehearsal.get('evidenceSha256')), 'gc-rehearsal-missing-or-failed')
    in_record_time(rehearsal, result, before=result.get('gcStartedAt'))
    lock = result.get('writeReplicationLock')
    require(isinstance(lock, dict) and identifier(lock.get('id')) and lock.get('registryWide') is True
            and lock.get('heldThroughPostGcVerification') is True and lock.get('concurrentMutationObserved') is False
            and lock.get('generationBefore') == lock.get('generationAfter') == inventory['generation']['generationSha256']
            and sha(lock.get('evidenceSha256')), 'gc-write-replication-lock-or-generation-unproved')
    held, released = timestamp(lock.get('acquiredAt')), timestamp(lock.get('releasedAt'))
    require(timestamp(result.get('observationStartedAt')) <= held <= timestamp(result.get('gcStartedAt'))
            <= timestamp(result.get('gcFinishedAt')) <= released <= timestamp(result.get('capturedAt')),
            'gc-outside-write-lock')
    require(timestamp(current['capturedAt']) <= timestamp(result['gcStartedAt']) < timestamp(current['expiresAt']),
            'gc-before-current-mutation-checkpoint')
    require(held <= timestamp(current.get('observationStartedAt'))
            <= timestamp(current['capturedAt']) <= timestamp(result['gcStartedAt']),
            'gc-checkpoint-not-collected-under-held-write-replication-lock')
    operations = result.get('postGcOperations')
    require(isinstance(operations, list) and operations, 'post-gc-uncached-pulls-missing')
    fetched = set()
    for operation in operations:
        require(isinstance(operation, dict) and operation.get('consumerId') in consumers,
                'unknown-post-gc-consumer')
        consumer = consumers[operation['consumerId']]
        require(consumer['role'] == 'reader' or (consumer['role'] == 'replicator'
                and consumer.get('replicationDirection') == 'source'),
                'post-gc-operation-is-not-an-authenticated-read')
        object_digest = operation.get('requestedDigest')
        object_kind = next((v['kind'] for v in closure['objects'] if v['digest'] == object_digest), None)
        require(object_kind is not None, 'unexpected-post-gc-object')
        audited_operation(operation, consumer, inventory['generation'],
                          blob=object_kind in ('config', 'layer'))
        require(timestamp(result['gcFinishedAt']) <= timestamp(operation['capturedAt']) <= released,
                'post-gc-proof-outside-held-verification-lock')
        fetched.add(operation['returnedDigest'])
    require(objects <= fetched, 'retained-object-uncached-post-gc-fetch-missing')


def unique_json_object(pairs):
    result = {}
    for name, value in pairs:
        require(name not in result, 'duplicate-evidence-json-key')
        result[name] = value
    return result


def reject_json_constant(value):
    raise ValueError('nonfinite-evidence-json')


def signed_input(path, project, allowed_signers, principal, ssh_keygen):
    directory = private_path(Path(path).parent, project)
    require(directory.is_dir(), 'missing-private-bundle-directory')
    path = private_path(path, project, file=True)
    signature = private_path(str(path) + '.sig', project, file=True)
    trust = private_path(allowed_signers, project, file=True)
    tool = Path(ssh_keygen)
    require(tool.is_absolute() and tool.is_file() and os.access(tool, os.X_OK), 'absolute-local-ssh-keygen-required')
    data = path.read_bytes()
    verified = subprocess.run([str(tool), '-Y', 'verify', '-f', str(trust), '-I', principal,
        '-n', 'hexalith-admin-exposure', '-s', str(signature)], input=data, capture_output=True, timeout=30)
    require(verified.returncode == 0, 'unverified-evidence-signature')
    try:
        record = json.loads(data, object_pairs_hook=unique_json_object, parse_constant=reject_json_constant)
        require(isinstance(record, dict), 'invalid-evidence-record')
        no_sensitive_fields(record)
    except (ValueError, UnicodeError, RecursionError):
        raise ValueError('malformed-evidence-json') from None
    return record, digest(data)


def check_bundle(args):
    records, hashes, failures = {}, {}, []
    clock = datetime.now(timezone.utc)
    try:
        trust_sha = file_digest(private_path(args.allowed_signers, Path(args.project_root), file=True))
        tool_sha = file_digest(args.ssh_keygen)
    except (ValueError, OSError):
        trust_sha, tool_sha = None, None
    files = (*COMMON_FILES, *PHASE_FILES[args.phase])
    for name in files:
        try:
            record, record_hash = signed_input(Path(args.bundle) / name, Path(args.project_root),
                args.allowed_signers, args.administrator_principal, args.ssh_keygen)
            require(file_digest(args.allowed_signers) == trust_sha and file_digest(args.ssh_keygen) == tool_sha,
                    'signature-trust-or-verifier-drift')
            records[name], hashes[name] = record, record_hash
            require(type(record.get('schemaVersion')) is int and record['schemaVersion'] == 1
                    and record.get('story') == '4.2' and record.get('classification') == 'measured-production'
                    and record.get('verificationResult') == 'pass', 'pending-or-nonproduction-evidence')
            require(identifier(record.get('sourceClusterUid')) and identifier(record.get('attemptId')),
                    'missing-attempt-or-cluster-identity')
            fresh(record, clock)
            require(timestamp(record.get('observationStartedAt')) <= timestamp(record['capturedAt']),
                    'invalid-observation-window')
        except (ValueError, OSError, subprocess.SubprocessError):
            # Input values, headers, bodies and subprocess stderr never enter the report.
            failures.append({'file': name, 'condition': 'signed-fresh-production-input-required'})
    if not failures:
        try:
            baseline, decisions, current = (records[v] for v in COMMON_FILES)
            require(len({v['sourceClusterUid'] for v in records.values()}) == 1
                    and len({v['attemptId'] for v in records.values()}) == 1, 'attempt-or-cluster-drift')
            require(decisions.get('approvedBy') == 'Administrator' and decisions.get('productionGo') is True
                    and decisions.get('baselineSha256') == hashes['signed-baseline.json'], 'production-approval-missing')
            require(current.get('forMutation') == args.phase, 'reread-bound-to-wrong-mutation')
            prior_approval(args.phase, records, hashes)
            compare_baseline(baseline, current)
            target_baseline(args.phase, baseline, decisions)
            if args.phase in ('console', 'keycloak'):
                admin_result(args.phase, decisions, records['admin-path-proof.json'],
                             records['admin-exposure-result.json'], hashes, current)
            else:
                require(identifier(decisions.get('registryCredentialOwner'))
                        and sha(decisions.get('credentialRotationPolicySha256'))
                        and timestamp(decisions.get('cutoverWindowStart')) <= timestamp(current['capturedAt'])
                        < timestamp(decisions.get('cutoverWindowEnd')), 'registry-custody-or-window-unapproved')
                inventory = records['registry-consumer-inventory.json']
                require(baseline.get('registryGeneration') == inventory.get('generation'), 'registry-generation-drift')
                if args.phase == 'registry-auth':
                    registry_result(decisions, inventory, records['registry-auth-result.json'], hashes, current)
                    result = records['registry-auth-result.json']
                    start, end = result['mutationStartedAt'], result['mutationFinishedAt']
                else:
                    gc_result(decisions, inventory, records['retained-oci-closure.json'],
                              records['registry-gc-result.json'], hashes, current)
                    result = records['registry-gc-result.json']
                    start, end = result['gcStartedAt'], result['gcFinishedAt']
                require(timestamp(decisions['cutoverWindowStart']) <= timestamp(start)
                        <= timestamp(end) < timestamp(decisions['cutoverWindowEnd']),
                        'registry-operation-outside-approved-window')
        except (ValueError, KeyError, TypeError) as error:
            condition = str(error) if isinstance(error, ValueError) else 'incomplete-evidence-schema'
            failures.append({'condition': condition})
    completion_clock = datetime.now(timezone.utc)
    for name, record in records.items():
        try:
            fresh(record, completion_clock)
        except ValueError:
            if not any(failure.get('file') == name for failure in failures):
                failures.append({'file': name, 'condition': 'signed-fresh-production-input-required'})
    return {'schemaVersion': 1, 'story': '4.2', 'phase': args.phase,
            'verificationResult': 'fail' if failures else 'pass',
            'scope': 'offline signatures and evidence consistency; no independent measurement',
            'allowedSignersSha256': trust_sha, 'signatureVerifierSha256': tool_sha,
            'inputSha256': hashes, 'failures': failures, 'mutationAuthorized': False,
            'operationalAcceptance': False, 'complete': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=Path(__file__).resolve().parents[2])
    commands = parser.add_subparsers(dest='command', required=True)
    create = commands.add_parser('prepare', help='Create unsigned pending templates outside Git; no probes')
    create.add_argument('--evidence-root', type=Path, required=True)
    create.add_argument('--attempt-id', required=True)
    create.add_argument('--operator', required=True)
    check = commands.add_parser('check', help='Check signed input structure; never authorizes production action')
    check.add_argument('--bundle', type=Path, required=True)
    check.add_argument('--phase', choices=PHASE_FILES, required=True)
    check.add_argument('--allowed-signers', type=Path, required=True)
    check.add_argument('--administrator-principal', required=True)
    check.add_argument('--ssh-keygen', type=Path, required=True)
    args = parser.parse_args()
    try:
        if args.command == 'prepare':
            print(prepare(args.project_root, args.evidence_root, args.attempt_id, args.operator))
        else:
            result = check_bundle(args)
            print(canonical(result).decode(), end='')
            return 1 if result['failures'] else 0
    except (ValueError, OSError, subprocess.SubprocessError):
        print('Preparation refused: invalid input, custody or attempt. No production operation was performed.')
        return 1
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
