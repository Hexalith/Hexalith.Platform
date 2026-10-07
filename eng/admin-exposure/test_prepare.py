"""Synthetic local boundary tests; no cluster/registry access or operational acceptance."""
import copy
from datetime import datetime, timedelta, timezone
import importlib.util
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location('exposure_prepare', Path(__file__).with_name('prepare.py'))
prepare = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(prepare)
H = 'a' * 64
# These contract lists come from Story 4.2, independently of the implementation.
REQUIRED_ADMIN_PATHS = ('/admin', '/admin/', '/admin/master/console/', '/realms/master',
                        '/realms/master/.well-known/openid-configuration',
                        '/realms/master/protocol/openid-connect/token')
REQUIRED_REGISTRY_COVERAGE = ('imagePullSecretsAndServiceAccounts', 'nodesAndRuntimes', 'forgejoWorkflows',
                              'deploymentExecutors', 'humanReaders', 'publicationAndOperationsWriters',
                              'replicationAndOffsiteRobots', 'liveAndRollbackAndRetainedReleases')
ANONYMOUS_READ_KINDS = ('catalog', 'tag', 'manifest', 'blob')


def oci(letter):
    return 'sha256:' + letter * 64


class Fixtures:
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory()
        self.addCleanup(self.scratch.cleanup)
        self.root = Path(self.scratch.name)
        self.project = self.root / 'project'
        self.project.mkdir()
        for name in prepare.SOURCES:
            path = self.project / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('sanitized fixture\n')
        self.evidence = self.root / 'evidence'
        self.clock = datetime.now(timezone.utc)

    def time(self, seconds):
        return (self.clock + timedelta(seconds=seconds)).isoformat(timespec='seconds')

    def base(self, captured=-10, start=-600):
        return {'schemaVersion': 1, 'story': '4.2', 'attemptId': 'synthetic-attempt',
                'classification': 'measured-production', 'operator': 'fixture-operator',
                'observationStartedAt': self.time(start), 'capturedAt': self.time(captured),
                'expiresAt': self.time(3600), 'sourceClusterUid': 'fixture-cluster', 'verificationResult': 'pass'}

    def generation(self):
        result = {name: H for name in ('generationSha256', 'consumerSetSha256', 'releaseRollbackSetSha256',
                  'credentialReferencesSha256', 'writerReplicationSetSha256', 'registryConfigSha256',
                  'approvedTargetConfigSha256')}
        result['changeControl'] = 'frozen'
        return result

    def snapshot(self, phase='console'):
        namespace, host = {'console': ('kubesphere-system', 'kube.hexalith.com'),
                           'keycloak': ('keycloak', 'auth.tache.ai'),
                           'registry-auth': ('registry-distribution', 'registry.hexalith.com'),
                           'registry-gc': ('registry-distribution', 'registry.hexalith.com')}[phase]
        ingress = {'apiVersion': 'networking.k8s.io/v1', 'kind': 'Ingress', 'namespace': namespace,
                   'name': 'fixture-ingress', 'uid': 'route-uid', 'resourceVersion': '123',
                   'effectiveConfigSha256': H, 'hostnames': [host], 'paths': ['/']}
        service = {'apiVersion': 'v1', 'kind': 'Service', 'namespace': namespace, 'name': 'fixture-service',
                   'uid': 'service-uid', 'resourceVersion': '456', 'effectiveConfigSha256': H}
        return {**self.base(captured=-300), 'resources': [ingress, service], 'routes': [
            {'hostname': host, 'dnsAnswers': ['192.0.2.10', '2001:db8::10'], 'ingressUids': ['route-uid'],
             'ingressClass': 'fixture-public', 'backends': [{'serviceUid': 'service-uid',
                'namespace': namespace, 'name': 'fixture-service', 'effectiveConfigSha256': H}]}],
            'registryGeneration': self.generation()}

    def decisions(self, phase='console'):
        return {**self.base(captured=-120), 'approvedBy': 'Administrator', 'privatePathId': 'fixture-private',
                'administrationPolicy': 'sole-administrator', 'administrator': 'jpiquot',
                'operators': ['jpiquot'], 'breakGlassPathId': 'fixture-native',
                'recoveryCustodyId': 'fixture-recovery-custody',
                'administratorAccountBindings': {'nativeCluster': 'fixture-native-user', 'keycloak': 'fixture-keycloak-user'},
                'recoveryAccountBindings': {'nativeCluster': 'fixture-native-user', 'keycloak': 'fixture-keycloak-user'},
                'privateAdministrationTargets': {
                    'nativeCluster': {'hostname': 'cluster.private.example', 'path': '/native', 'method': 'GET'},
                    'keycloak': {'hostname': 'keycloak.private.example', 'path': '/admin', 'method': 'GET'}},
                'monitoringOwner': 'fixture-owner', 'approvedPublicOidcChecks': ['fixture-oidc'],
                'consoleMode': 'port-forward', 'publicConsoleHostnames': ['kube.hexalith.com'],
                'keycloakHostname': 'auth.tache.ai', 'registryHostname': 'registry.hexalith.com',
                'affectedRouteUids': {phase: ['route-uid']}, 'publicCatchAllIngressUid': 'route-uid',
                'registryCredentialOwner': 'fixture-owner', 'credentialRotationPolicySha256': H,
                'cutoverWindowStart': self.time(-200), 'cutoverWindowEnd': self.time(1800),
                'retainedDigestSourceSha256': H, 'gcPolicySha256': H, 'productionGo': True,
                'baselineSha256': H}

    def denial(self, *, public=True, captured=-20, host='kube.hexalith.com', path='/'):
        return {'hostname': host, 'path': path, 'method': 'GET', 'capturedAt': self.time(captured),
                'sourceAddressCategory': 'external-public' if public else 'unauthorized-private',
                'backendReached': False, 'routingEvidenceSha256': H, 'decisionBy': 'ingress',
                'status': 403, 'outcome': 'refused'}

    def oidc(self, captured=-20):
        return [{'id': 'fixture-oidc', 'result': 'pass', 'evidenceSha256': H, 'capturedAt': self.time(captured)}]

    def private_denials(self, captured=-180):
        return [{**self.denial(public=False, captured=captured, host=target['hostname'], path=target['path']),
                 'surface': surface, 'privatePathId': 'fixture-private', 'method': target['method']}
                for surface, target in self.decisions()['privateAdministrationTargets'].items()]

    def operators(self, captured=-180):
        return [{'operator': 'jpiquot', 'keycloakAdminLogin': True, 'keycloakNonDestructiveRead': True,
                 'clusterAdminRead': True, 'consolePortForward': True, 'evidenceSha256': H,
                 'accountBindings': {'nativeCluster': 'fixture-native-user', 'keycloak': 'fixture-keycloak-user'},
                 'capturedAt': self.time(captured)}]

    def recovery(self, captured=-180):
        return {'operator': 'jpiquot', 'pathId': 'fixture-native', 'custodyId': 'fixture-recovery-custody',
                'accountBindings': {'nativeCluster': 'fixture-native-user', 'keycloak': 'fixture-keycloak-user'},
                'qualificationScope': 'isolated-client-session', 'testSessionId': 'fixture-recovery-session',
                'isolationEvidenceSha256': H, 'productionAvailabilityEvidenceSha256': H,
                'productionAccountsAvailableToOtherClients': True,
                'productionPublicOidcAvailableToOtherClients': True,
                'productionAuthenticationUnchangedDuringQualification': True,
                'ordinaryCredentialsUnavailable': True, 'publicOidcUnavailable': True,
                'separatelyProtectedRecoveryAccess': True, 'independentOfOrdinaryCredentials': True,
                'credentialLineageEvidenceSha256': H, 'nativeClusterAuthentication': True,
                'nativeClusterNonDestructiveRead': True, 'keycloakAdminLogin': True,
                'keycloakNonDestructiveRead': True, 'result': 'pass', 'evidenceSha256': H,
                'capturedAt': self.time(captured)}

    def admin_records(self, phase='console'):
        baseline = self.snapshot(phase)
        current = {**copy.deepcopy(baseline), **self.base(captured=-90, start=-100), 'forMutation': phase,
                   'observedImmediatelyBeforeMutation': True}
        decisions = self.decisions(phase)
        proof = {**self.base(captured=-150), 'privatePathId': 'fixture-private',
                 'administrationPolicy': 'sole-administrator', 'administrator': 'jpiquot',
                 'testedOperators': self.operators(), 'unauthorizedPrivateChecks': self.private_denials(),
                 'breakGlass': self.recovery(), 'publicOidcChecks': self.oidc(-180)}
        host = decisions['keycloakHostname'] if phase == 'keycloak' else 'kube.hexalith.com'
        paths = REQUIRED_ADMIN_PATHS if phase == 'keycloak' else ('/', '/login')
        probes = [{**self.denial(host=host, path=path), 'method': method} for path in paths
                  for method in (('GET', 'POST') if phase == 'keycloak' else ('GET',))]
        result = {**self.base(), 'closedSurface': phase, 'baselineSha256': H, 'privateProofSha256': H,
                  'administrationPolicy': 'sole-administrator', 'administrator': 'jpiquot',
                  'externalProbes': probes, 'closedAdminPaths': list(REQUIRED_ADMIN_PATHS),
                  'consoleMode': 'port-forward', 'publicOidcChecks': self.oidc(),
                  'postChangePrivateChecks': self.operators(-20),
                  'postChangeUnauthorizedPrivateChecks': self.private_denials(-20),
                  'postChangeBreakGlass': self.recovery(-20),
                  'mutationStartedAt': self.time(-60), 'mutationFinishedAt': self.time(-40)}
        return baseline, decisions, current, proof, result

    def add_console_hostname(self, baseline, decisions, result):
        host = 'replacement.public.example'
        ingress = copy.deepcopy(baseline['resources'][0])
        ingress.update(name='replacement-ingress', uid='replacement-uid', hostnames=[host])
        route = copy.deepcopy(baseline['routes'][0])
        route.update(hostname=host, ingressUids=['replacement-uid'], dnsAnswers=['192.0.2.20'])
        baseline['resources'].append(ingress)
        baseline['routes'].append(route)
        decisions['publicConsoleHostnames'].append(host)
        decisions['affectedRouteUids']['console'].append('replacement-uid')
        result['externalProbes'].extend(self.denial(host=host, path=path) for path in ('/', '/login'))

    def add_keycloak_hostname(self, records, host='other-auth.public.example', *, approved=True):
        baseline, decisions, current, _, _ = records
        if approved:
            baseline['resources'][0]['hostnames'].append(host)
            uid = baseline['resources'][0]['uid']
        else:
            ingress = copy.deepcopy(baseline['resources'][0])
            ingress.update(uid='unrelated-uid', name='unrelated-ingress', hostnames=[host])
            baseline['resources'].append(ingress)
            uid = ingress['uid']
        route = copy.deepcopy(baseline['routes'][0])
        route.update(hostname=host, ingressUids=[uid])
        baseline['routes'].append(route)
        current.update(resources=copy.deepcopy(baseline['resources']), routes=copy.deepcopy(baseline['routes']))

    def operation(self, consumer, captured=-180, requested=None):
        result = {'consumerId': consumer['id'], 'principal': consumer['principal'],
                  'generationSha256': H, 'result': 'pass', 'auditEvidenceSha256': H,
                  'auditCorrelationId': 'fixture-request', 'capturedAt': self.time(captured),
                  'repository': 'fixture/repository',
                  'operation': {'reader': 'pull', 'writer': 'push', 'replicator': 'replicate'}[consumer['role']],
                  'emptyDisposableContentStore': True, 'manifestTransferred': True, 'blobsTransferred': True,
                  'requestedDigest': requested or oci('a'), 'returnedDigest': requested or oci('a'),
                  'leastPrivilege': copy.deepcopy(consumer['leastPrivilege'])}
        result['leastPrivilege']['capturedAt'] = self.time(captured)
        if consumer['role'] == 'replicator':
            result['replicationDirection'] = consumer['replicationDirection']
        return result

    def inventory(self):
        consumers = []
        for role in ('reader', 'writer', 'replicator'):
            permissions = {'evidenceSha256': H, 'pushDenied': True, 'deleteDenied': True,
                           'approvedOperationPassed': True, 'outOfScopeDenied': True, 'retainedDeleteDenied': True,
                           'capturedAt': self.time(-180)}
            consumer = {'id': role, 'principal': f'fixture-{role}', 'role': role,
                        'credentialSecretReference': f'fixture-{role}-secret',
                        'approvedRepositories': ['fixture/repository'], 'retainedDeleteDenied': True,
                        'leastPrivilege': permissions, 'replicationDirection': 'source'}
            consumer['beforeCutoverOperation'] = self.operation(consumer)
            consumers.append(consumer)
        return {**self.base(captured=-150), 'generation': self.generation(), 'inventoryComplete': True,
                'coverage': {k: True for k in REQUIRED_REGISTRY_COVERAGE}, 'consumers': consumers}

    def registry_records(self):
        inventory = self.inventory()
        result = {**self.base(), 'generation': self.generation(), 'inventorySha256': H,
                  'mutationStartedAt': self.time(-60), 'mutationFinishedAt': self.time(-40),
                  'authenticatedOperations': [self.operation(v, -20) for v in inventory['consumers']],
                  'anonymousProbes': []}
        paths = {'catalog': '/v2/_catalog', 'tag': '/v2/fixture/repository/tags/list',
                 'manifest': '/v2/fixture/repository/manifests/' + oci('a'),
                 'blob': '/v2/fixture/repository/blobs/' + oci('b')}
        for kind, path in paths.items():
            result['anonymousProbes'].append({'kind': kind, 'hostname': 'registry.hexalith.com', 'path': path,
                'method': 'GET', 'status': 401, 'result': 'refused', 'sourceAddressCategory': 'external-public',
                'knownExistingContent': True, 'evidenceSha256': H, 'capturedAt': self.time(-20)})
        return self.decisions('registry-auth'), inventory, result, self.admin_records('registry-auth')[2]

    def closure(self):
        objects = [{'digest': oci('a'), 'kind': 'index', 'references': [oci('b'), oci('e')]},
                   {'digest': oci('b'), 'kind': 'manifest', 'references': [oci('c'), oci('d')]},
                   {'digest': oci('c'), 'kind': 'config', 'references': []},
                   {'digest': oci('d'), 'kind': 'layer', 'references': []},
                   {'digest': oci('e'), 'kind': 'referrer', 'references': []}]
        roots = [oci('a')]
        return {**self.base(captured=-150), 'generation': self.generation(), 'roots': roots, 'objects': objects,
                'closureSha256': prepare.digest(prepare.canonical({'roots': sorted(roots),
                                       'objects': sorted(objects, key=lambda v: v['digest'])})),
                'referrerEnumerationComplete': True, 'retainedSourceSha256': H}

    def gc_records(self):
        inventory, closure = self.inventory(), self.closure()
        pulls = []
        for obj in closure['objects']:
            pull = self.operation(inventory['consumers'][0], -20, obj['digest'])
            if obj['kind'] in ('config', 'layer'):
                pull.pop('manifestTransferred')
                pull.pop('blobsTransferred')
                pull['blobTransferred'] = True
            pulls.append(pull)
        result = {**self.base(captured=-5), 'generation': self.generation(), 'inventorySha256': H,
                  'closureSha256': closure['closureSha256'], 'gcConfigSha256': H,
                  'excludedObjectDigests': [v['digest'] for v in closure['objects']],
                  'rehearsal': {'result': 'pass', 'retainedContentPreserved': True, 'disposableContentDeleted': True,
                                'evidenceSha256': H, 'capturedAt': self.time(-200)},
                  'writeReplicationLock': {'id': 'fixture-lock', 'registryWide': True,
                     'heldThroughPostGcVerification': True, 'concurrentMutationObserved': False,
                     'generationBefore': H, 'generationAfter': H, 'evidenceSha256': H,
                     'acquiredAt': self.time(-110), 'releasedAt': self.time(-10)},
                  'gcStartedAt': self.time(-60), 'gcFinishedAt': self.time(-40), 'postGcOperations': pulls}
        return self.decisions('registry-gc'), inventory, closure, result

class BoundaryTests(Fixtures, unittest.TestCase):
    def test_preparation_is_private_pending_immutable_and_has_no_network_or_commands(self):
        old = os.umask(0)
        try:
            with patch('subprocess.run', side_effect=AssertionError('external command')), \
                 patch('socket.socket', side_effect=AssertionError('network')):
                directory = prepare.prepare(self.project, self.evidence, 'fixture-attempt', 'operator')
        finally:
            os.umask(old)
        for path in (directory, *directory.parents):
            if path == self.root:
                break
            self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o700)
        for path in directory.iterdir():
            self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o600)
        record = json.loads((directory / 'attempt.json').read_text())
        self.assertFalse(record['mutationAuthorized'])
        self.assertFalse(record['liveEvidenceCollected'])
        self.assertFalse(record['complete'])
        handoff = json.loads((directory / 'console-closure.json').read_text())
        self.assertFalse(handoff['accepted'])
        self.assertIsNone(handoff['planSha256'])
        decisions = json.loads((directory / 'administrator-decisions.json').read_text())
        self.assertIsNone(decisions['administrationPolicy'])
        self.assertIsNone(decisions['administrator'])
        self.assertEqual(decisions['operators'], [])
        self.assertIsNone(decisions['recoveryCustodyId'])
        self.assertTrue(all(v is None for v in decisions['recoveryAccountBindings'].values()))
        self.assertEqual(set(decisions['privateAdministrationTargets']), prepare.ADMINISTRATION_SURFACES)
        self.assertTrue(all(v is None for target in decisions['privateAdministrationTargets'].values()
                            for v in target.values()))
        result = json.loads((directory / 'admin-exposure-result.json').read_text())
        proof = json.loads((directory / 'admin-path-proof.json').read_text())
        self.assertEqual(proof['unauthorizedPrivateChecks'], [])
        self.assertEqual(result['postChangeUnauthorizedPrivateChecks'], [])
        for recovery in (proof['breakGlass'], result['postChangeBreakGlass']):
            self.assertEqual(recovery, prepare.pending_recovery())
            self.assertTrue(all(v is None for v in recovery.values()))
        for line in (directory / 'SHA256SUMS').read_text().splitlines():
            expected, name = line.split('  ')
            self.assertEqual(prepare.file_digest(directory / name), expected)
        with self.assertRaisesRegex(ValueError, 'attempt-already-exists'):
            prepare.prepare(self.project, self.evidence, 'fixture-attempt', 'operator')

    def test_refuse_repository_shared_symlink_and_unsafe_attempt(self):
        for root, attempt in ((self.project / 'evidence', 'valid'), (self.evidence, '../escape')):
            with self.assertRaises(ValueError):
                prepare.prepare(self.project, root, attempt, 'operator')
        self.evidence.mkdir(mode=0o755)
        self.evidence.chmod(0o755)
        with self.assertRaisesRegex(ValueError, 'owner-only'):
            prepare.prepare(self.project, self.evidence, 'valid', 'operator')
        link = self.root / 'linked'
        link.symlink_to(self.evidence, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, 'symlink'):
            prepare.prepare(self.project, link, 'valid', 'operator')

    def test_refuse_existing_recovery_custody(self):
        with patch.object(Path, 'home', return_value=self.root):
            with self.assertRaisesRegex(ValueError, 'recovery-custody'):
                prepare.prepare(self.project, self.root / 'hexalith-recovery-evidence', 'valid', 'operator')

    def test_refuse_private_custody_in_other_checkouts_worktrees_and_submodules_without_commands(self):
        for kind in ('checkout', 'linked-worktree', 'submodule'):
            checkout = self.root / kind
            checkout.mkdir(mode=0o700)
            marker = checkout / '.git'
            if kind == 'checkout':
                marker.mkdir(mode=0o700)
            else:
                marker.write_text('gitdir: /synthetic/private/git-metadata\n')
                marker.chmod(0o600)
            with self.subTest(kind=kind), patch('subprocess.run', side_effect=AssertionError('external command')), \
                 patch('socket.socket', side_effect=AssertionError('network')):
                with self.assertRaisesRegex(ValueError, 'outside-git-worktree'):
                    prepare.prepare(self.project, checkout / 'custody', 'valid', 'operator')
                self.assertFalse((checkout / 'custody').exists())

    def test_real_ingress_api_version_and_exact_snapshot_pass(self):
        baseline, decisions, current, _, _ = self.admin_records()
        prepare.compare_baseline(baseline, current)
        prepare.target_baseline('console', baseline, decisions)

    def test_uid_resourceversion_config_dns_ingress_backend_and_generation_drift_stop(self):
        baseline, _, current, _, _ = self.admin_records()
        for field in ('uid', 'resourceVersion', 'effectiveConfigSha256'):
            changed = copy.deepcopy(current)
            changed['resources'][0][field] = 'other' if field != 'effectiveConfigSha256' else 'b' * 64
            with self.subTest(field=field), self.assertRaises(ValueError):
                prepare.compare_baseline(baseline, changed)
        for field, value in (('dnsAnswers', ['192.0.2.11']), ('ingressClass', 'different-controller'),
                             ('ingressUids', ['service-uid'])):
            changed = copy.deepcopy(current)
            changed['routes'][0][field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                prepare.compare_baseline(baseline, changed)
        changed = copy.deepcopy(current)
        changed['routes'][0]['backends'][0]['effectiveConfigSha256'] = 'b' * 64
        with self.assertRaises(ValueError):
            prepare.compare_baseline(baseline, changed)
        changed = copy.deepcopy(current)
        changed['registryGeneration']['writerReplicationSetSha256'] = 'b' * 64
        with self.assertRaises(ValueError):
            prepare.compare_baseline(baseline, changed)

    def test_unrelated_target_or_unreviewed_keycloak_catch_all_is_refused(self):
        baseline, decisions, _, _, _ = self.admin_records()
        baseline['routes'][0]['hostname'] = 'unrelated.example'
        with self.assertRaises(ValueError):
            prepare.target_baseline('console', baseline, decisions)
        baseline, decisions, _, _, _ = self.admin_records('keycloak')
        baseline['resources'][0]['paths'] = ['/admin']
        with self.assertRaisesRegex(ValueError, 'catch-all'):
            prepare.target_baseline('keycloak', baseline, decisions)

    def test_ingress_hostname_and_path_scopes_are_nonempty_valid_string_arrays(self):
        for field, values in (('hostnames', ('kube.hexalith.com', [], [None], ['bad hostname'])),
                              ('paths', ('/admin', [], [None], ['admin'], ['/admin\n']))):
            for value in values:
                baseline = self.snapshot('keycloak')
                baseline['resources'][0][field] = value
                with self.subTest(field=field, value=value), self.assertRaisesRegex(ValueError, 'ingress'):
                    prepare.validate_snapshot(baseline)

    def test_affected_route_approvals_must_be_an_object(self):
        for value in (None, [], ['route-uid'], 'console'):
            baseline, decisions, _, _, _ = self.admin_records()
            decisions['affectedRouteUids'] = value
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, 'affected-route-approval-map'):
                prepare.target_baseline('console', baseline, decisions)

    def test_all_console_hostnames_bind_snapshot_and_exact_route_uid_union(self):
        baseline, decisions, _, _, result = self.admin_records()
        self.add_console_hostname(baseline, decisions, result)
        prepare.target_baseline('console', baseline, decisions)
        for change in ('missing-route', 'missing-uid', 'extra-uid', 'unbound-host', 'unbound-backend'):
            changed_baseline, changed_decisions = copy.deepcopy(baseline), copy.deepcopy(decisions)
            if change == 'missing-route':
                changed_baseline['routes'].pop()
            elif change == 'missing-uid':
                changed_decisions['affectedRouteUids']['console'].pop()
            elif change == 'extra-uid':
                changed_decisions['affectedRouteUids']['console'].append('service-uid')
            elif change == 'unbound-host':
                changed_baseline['resources'][-1]['hostnames'] = ['unrelated.example']
            else:
                changed_baseline['routes'][-1]['backends'][0]['effectiveConfigSha256'] = 'b' * 64
            with self.subTest(change=change), self.assertRaises(ValueError):
                prepare.target_baseline('console', changed_baseline, changed_decisions)

    def test_private_targets_require_exact_surfaces_and_valid_approved_targets(self):
        for change in ('absent', 'list', 'missing-surface', 'extra-surface', 'hostname', 'path', 'method'):
            _, decisions, _, proof, _ = self.admin_records()
            targets = decisions['privateAdministrationTargets']
            if change == 'absent':
                decisions.pop('privateAdministrationTargets')
            elif change == 'list':
                decisions['privateAdministrationTargets'] = []
            elif change == 'missing-surface':
                targets.pop('nativeCluster')
            elif change == 'extra-surface':
                targets['console'] = targets['nativeCluster']
            else:
                targets['nativeCluster'][change] = {'hostname': '', 'path': 'relative', 'method': 'get'}[change]
            with self.subTest(change=change), self.assertRaisesRegex(ValueError, 'private-administration-targets'):
                prepare.admin_proof(decisions, proof)

    def test_pre_and_post_private_refusal_bind_each_surface_path_and_target(self):
        for stage in ('before', 'after'):
            for field, value in (('hostname', 'unrelated.example'), ('path', '/unrelated'),
                                 ('method', 'POST'), ('privatePathId', 'unapproved-path')):
                for surface in prepare.ADMINISTRATION_SURFACES:
                    _, decisions, current, proof, result = self.admin_records()
                    checks = proof['unauthorizedPrivateChecks'] if stage == 'before' else result['postChangeUnauthorizedPrivateChecks']
                    next(v for v in checks if v['surface'] == surface)[field] = value
                    with self.subTest(stage=stage, field=field, surface=surface), self.assertRaisesRegex(ValueError, 'private-refusal-target'):
                        prepare.admin_result('console', decisions, proof, result,
                                             {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_pre_and_post_private_refusal_require_one_measured_check_per_surface(self):
        for stage in ('before', 'after'):
            for change in ('absent', 'missing', 'duplicate', 'unrelated-surface', 'backend-reached'):
                _, decisions, current, proof, result = self.admin_records()
                parent, field = (proof, 'unauthorizedPrivateChecks') if stage == 'before' else (result, 'postChangeUnauthorizedPrivateChecks')
                checks = parent[field]
                if change == 'absent':
                    parent.pop(field)
                elif change == 'missing':
                    checks.pop()
                elif change == 'duplicate':
                    checks.append(copy.deepcopy(checks[0]))
                elif change == 'unrelated-surface':
                    checks[0]['surface'] = 'console'
                else:
                    checks[0]['backendReached'] = True
                with self.subTest(stage=stage, change=change), self.assertRaises(ValueError):
                    prepare.admin_result('console', decisions, proof, result,
                                         {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_private_refusal_precedes_approval_and_follows_closure(self):
        _, decisions, current, proof, result = self.admin_records()
        proof['capturedAt'] = self.time(-100)
        proof['unauthorizedPrivateChecks'][0]['capturedAt'] = self.time(-110)
        with self.assertRaisesRegex(ValueError, 'nested-evidence'):
            prepare.admin_result('console', decisions, proof, result,
                                 {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)
        _, decisions, current, proof, result = self.admin_records()
        result['postChangeUnauthorizedPrivateChecks'][0]['capturedAt'] = self.time(-50)
        with self.assertRaisesRegex(ValueError, 'nested-evidence'):
            prepare.admin_result('console', decisions, proof, result,
                                 {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_keycloak_requires_get_and_post_for_all_declared_paths_and_aliases(self):
        _, decisions, current, proof, result = self.admin_records('keycloak')
        aliases = ('/realms/master/protocol/openid-connect/%74oken/',
                   '/realms/master/protocol/openid-connect/./token', '/admin//')
        result['closedAdminPaths'].extend(aliases)
        result['externalProbes'].extend({**self.denial(host='auth.tache.ai', path=path), 'method': method}
                                       for path in aliases for method in ('GET', 'POST'))
        prepare.admin_result('keycloak', decisions, proof, result,
                             {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)
        for path in result['closedAdminPaths']:
            for method in ('GET', 'POST'):
                changed = copy.deepcopy(result)
                changed['externalProbes'] = [v for v in changed['externalProbes'] if (v['path'], v['method']) != (path, method)]
                with self.subTest(path=path, method=method), self.assertRaisesRegex(ValueError, 'external-negative-coverage'):
                    prepare.admin_result('keycloak', decisions, proof, changed,
                                         {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_hostname_index_accepts_long_dns_names_without_relaxing_identifiers(self):
        host = 'a' * 63 + '.' + 'b' * 63 + '.example.com'
        baseline = self.snapshot('keycloak')
        baseline['resources'][0]['hostnames'] = [host]
        baseline['routes'][0]['hostname'] = host
        prepare.validate_snapshot(baseline)
        with self.assertRaisesRegex(ValueError, 'invalid-evidence-list'):
            prepare.unique([{'uid': host}], 'uid')

    def test_canonical_json_refuses_nonfinite_numbers(self):
        for value in (float('nan'), float('inf'), float('-inf')):
            with self.subTest(value=value), self.assertRaises(ValueError):
                prepare.canonical({'number': value})

    def test_positive_console_and_master_get_post_evidence_passes(self):
        for phase in ('console', 'keycloak'):
            _, decisions, current, proof, result = self.admin_records(phase)
            prepare.admin_result(phase, decisions, proof, result,
                                 {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_login_redirect_rate_limit_and_backend_404_do_not_count_as_closed(self):
        for status, outcome in ((200, 'refused'), (302, 'refused'), (429, 'refused'), (404, 'not-routed')):
            probe = self.denial()
            probe.update(status=status, outcome=outcome, backendReached=status == 404)
            with self.subTest(status=status), self.assertRaises(ValueError):
                prepare.refused(probe)

    def test_console_requires_named_administrator_break_glass_oidc_and_post_access(self):
        for field in ('testedOperators', 'breakGlass', 'publicOidcChecks'):
            _, decisions, _, proof, _ = self.admin_records()
            proof[field] = [] if field != 'breakGlass' else None
            with self.subTest(field=field), self.assertRaises(ValueError):
                prepare.admin_proof(decisions, proof)
        _, decisions, current, proof, result = self.admin_records()
        result['postChangePrivateChecks'][0]['clusterAdminRead'] = False
        with self.assertRaisesRegex(ValueError, 'post-change-private'):
            prepare.admin_result('console', decisions, proof, result,
                                 {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_sole_administrator_with_same_accounts_and_independent_recovery_passes(self):
        _, decisions, current, proof, result = self.admin_records()
        prepare.admin_result('console', decisions, proof, result,
                             {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_explicit_sole_policy_and_exact_selected_administrator_are_required(self):
        for field, value in (('administrationPolicy', None), ('administrationPolicy', 'two-operators'),
                             ('administrator', 'other-administrator'), ('operators', []),
                             ('operators', ['jpiquot', 'jpiquot']), ('operators', ['jpiquot', 'deputy'])):
            _, decisions, _, proof, _ = self.admin_records()
            decisions[field] = value
            with self.subTest(field=field, value=value), self.assertRaisesRegex(ValueError, 'sole-administrator-policy'):
                prepare.admin_proof(decisions, proof)

    def test_pre_and_post_proofs_cannot_substitute_or_add_an_administrator(self):
        for record_field in ('testedOperators', 'postChangePrivateChecks'):
            for change in ('different', 'second'):
                _, decisions, current, proof, result = self.admin_records()
                checks = proof[record_field] if record_field == 'testedOperators' else result[record_field]
                if change == 'different':
                    checks[0]['operator'] = 'other-administrator'
                else:
                    checks.append({**checks[0], 'operator': 'deputy'})
                with self.subTest(field=record_field, change=change), self.assertRaises(ValueError):
                    prepare.admin_result('console', decisions, proof, result,
                                         {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_recovery_requires_both_authentication_and_non_destructive_reads_pre_and_post(self):
        for stage in ('before', 'after'):
            for field in ('nativeClusterAuthentication', 'nativeClusterNonDestructiveRead',
                          'keycloakAdminLogin', 'keycloakNonDestructiveRead'):
                _, decisions, current, proof, result = self.admin_records()
                recovery = proof['breakGlass'] if stage == 'before' else result['postChangeBreakGlass']
                recovery[field] = False
                with self.subTest(stage=stage, field=field), self.assertRaisesRegex(ValueError, 'independent-recovery'):
                    prepare.admin_result('console', decisions, proof, result,
                                         {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_recovery_requires_ordinary_credentials_and_public_oidc_unavailable_pre_and_post(self):
        for stage in ('before', 'after'):
            for field in ('ordinaryCredentialsUnavailable', 'publicOidcUnavailable',
                          'separatelyProtectedRecoveryAccess', 'independentOfOrdinaryCredentials',
                          'credentialLineageEvidenceSha256'):
                _, decisions, current, proof, result = self.admin_records()
                recovery = proof['breakGlass'] if stage == 'before' else result['postChangeBreakGlass']
                recovery[field] = False if field != 'credentialLineageEvidenceSha256' else None
                with self.subTest(stage=stage, field=field), self.assertRaisesRegex(ValueError, 'independent-recovery'):
                    prepare.admin_result('console', decisions, proof, result,
                                         {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_recovery_requires_approved_custody_accounts_path_and_administrator(self):
        for change in ('missing-custody', 'wrong-custody', 'missing-account', 'wrong-account', 'wrong-path', 'wrong-operator'):
            _, decisions, current, proof, result = self.admin_records()
            if change == 'missing-custody':
                decisions.pop('recoveryCustodyId')
            elif change == 'wrong-custody':
                proof['breakGlass']['custodyId'] = 'unapproved-custody'
            elif change == 'missing-account':
                decisions['recoveryAccountBindings'].pop('keycloak')
            elif change == 'wrong-account':
                proof['breakGlass']['accountBindings']['nativeCluster'] = 'unapproved-principal'
            else:
                proof['breakGlass'][{'wrong-path': 'pathId', 'wrong-operator': 'operator'}[change]] = 'unapproved'
            with self.subTest(change=change), self.assertRaisesRegex(ValueError, 'recovery'):
                prepare.admin_result('console', decisions, proof, result,
                                     {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_recovery_requires_an_isolated_identified_session_before_and_after_closure(self):
        for stage in ('before', 'after'):
            for field, value in (('qualificationScope', None), ('qualificationScope', 'production'),
                                 ('testSessionId', None), ('testSessionId', ''),
                                 ('isolationEvidenceSha256', None), ('isolationEvidenceSha256', 'invalid')):
                _, decisions, current, proof, result = self.admin_records()
                recovery = proof['breakGlass'] if stage == 'before' else result['postChangeBreakGlass']
                recovery[field] = value
                with self.subTest(stage=stage, field=field, value=value), self.assertRaisesRegex(
                        ValueError, 'recovery-isolation-or-production-availability'):
                    prepare.admin_result('console', decisions, proof, result,
                                         {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_recovery_requires_measured_production_availability_without_authentication_changes(self):
        for stage in ('before', 'after'):
            for field in ('productionAccountsAvailableToOtherClients', 'productionPublicOidcAvailableToOtherClients',
                          'productionAuthenticationUnchangedDuringQualification', 'productionAvailabilityEvidenceSha256'):
                for value in (None, False):
                    _, decisions, current, proof, result = self.admin_records()
                    recovery = proof['breakGlass'] if stage == 'before' else result['postChangeBreakGlass']
                    recovery[field] = value
                    with self.subTest(stage=stage, field=field, value=value), self.assertRaisesRegex(
                            ValueError, 'recovery-isolation-or-production-availability'):
                        prepare.admin_result('console', decisions, proof, result,
                                             {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_post_change_recovery_is_required_and_must_follow_closure(self):
        for change in ('missing', 'before-closure', 'outside-window'):
            _, decisions, current, proof, result = self.admin_records()
            if change == 'missing':
                result.pop('postChangeBreakGlass')
            else:
                result['postChangeBreakGlass']['capturedAt'] = self.time(-180 if change == 'before-closure' else 10000)
            with self.subTest(change=change), self.assertRaises(ValueError):
                prepare.admin_result('console', decisions, proof, result,
                                     {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_second_account_or_copied_daily_access_alone_does_not_prove_recovery(self):
        for change in ('second-account', 'copied-daily-access'):
            _, decisions, current, proof, result = self.admin_records()
            if change == 'second-account':
                decisions['recoveryAccountBindings'] = {'nativeCluster': 'fixture-second-native', 'keycloak': 'fixture-second-keycloak'}
                for recovery in (proof['breakGlass'], result['postChangeBreakGlass']):
                    recovery['accountBindings'] = decisions['recoveryAccountBindings']
                    recovery.pop('credentialLineageEvidenceSha256')
            else:
                for recovery in (proof['breakGlass'], result['postChangeBreakGlass']):
                    recovery['independentOfOrdinaryCredentials'] = False
            with self.subTest(change=change), self.assertRaisesRegex(ValueError, 'independent-recovery'):
                prepare.admin_result('console', decisions, proof, result,
                                     {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_removed_console_uses_native_access_and_requires_all_removal_proof(self):
        _, decisions, current, proof, result = self.admin_records()
        decisions['consoleMode'] = result['consoleMode'] = 'removed'
        for check in (*proof['testedOperators'], *result['postChangePrivateChecks']):
            check.pop('consolePortForward')
        result['removedConsoleObjects'] = {'ingressAbsent': True, 'serviceAbsent': True,
            'workloadAbsent': True, 'publicDnsAbsent': True, 'evidenceSha256': H}
        prepare.admin_result('console', decisions, proof, result,
                             {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)
        result['removedConsoleObjects']['publicDnsAbsent'] = False
        with self.assertRaisesRegex(ValueError, 'removed-console'):
            prepare.admin_result('console', decisions, proof, result,
                                 {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_stale_nested_admin_probe_and_postcheck_before_mutation_are_refused(self):
        for changed_time in (-10000, -180, 10000):
            _, decisions, current, proof, result = self.admin_records()
            result['externalProbes'][0]['capturedAt'] = self.time(changed_time)
            with self.subTest(time=changed_time), self.assertRaisesRegex(ValueError, 'nested-evidence'):
                prepare.admin_result('console', decisions, proof, result,
                                     {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_failed_public_oidc_checks_fail_before_and_after_closure(self):
        for stage in ('before', 'after'):
            _, decisions, current, proof, result = self.admin_records()
            (proof if stage == 'before' else result)['publicOidcChecks'][0]['result'] = 'fail'
            with self.subTest(stage=stage), self.assertRaisesRegex(ValueError, 'public-oidc-regression'):
                prepare.admin_result('console', decisions, proof, result,
                                     {'signed-baseline.json': H, 'admin-path-proof.json': H}, current)

    def test_freshness_rejects_expiry_at_check_completion(self):
        record = self.base()
        prepare.fresh(record, self.clock)
        for clock in (prepare.timestamp(record['expiresAt']), self.clock + timedelta(hours=2)):
            with self.subTest(clock=clock), self.assertRaisesRegex(ValueError, 'stale-or-future-evidence'):
                prepare.fresh(record, clock)

    def test_complete_registry_reader_writer_replication_evidence_passes(self):
        decisions, inventory, result, current = self.registry_records()
        prepare.registry_result(decisions, inventory, result, {'registry-consumer-inventory.json': H}, current)

    def test_repository_scopes_require_nonempty_valid_string_arrays_and_exact_membership(self):
        for scope in ('fixturerepository', [], [None], ['Invalid/repository']):
            inventory = self.inventory()
            inventory['consumers'][0]['approvedRepositories'] = scope
            inventory['consumers'][0]['beforeCutoverOperation']['repository'] = 'fixture'
            with self.subTest(scope=scope), self.assertRaises(ValueError):
                prepare.registry_inventory(inventory)
        inventory = self.inventory()
        consumer = inventory['consumers'][0]
        consumer['beforeCutoverOperation']['repository'] = 'fixture'
        with self.assertRaisesRegex(ValueError, 'outside-approved-scope'):
            prepare.registry_inventory(inventory)
        consumer['approvedRepositories'] = 'fixturerepository'
        with self.assertRaisesRegex(ValueError, 'invalid-repository-scope'):
            prepare.audited_operation(consumer['beforeCutoverOperation'], consumer, inventory['generation'])

    def test_least_privilege_measurements_require_pre_and_post_interval_timestamps(self):
        for role in ('reader', 'writer', 'replicator'):
            for captured in (None, self.time(-10000), self.time(-50), self.time(10000)):
                inventory = self.inventory()
                consumer = next(v for v in inventory['consumers'] if v['role'] == role)
                consumer['leastPrivilege']['capturedAt'] = captured
                with self.subTest(stage='before', role=role, captured=captured), self.assertRaises(ValueError):
                    prepare.registry_inventory(inventory)
            for captured in (None, self.time(-10000), self.time(-180), self.time(-50), self.time(10000)):
                decisions, inventory, result, current = self.registry_records()
                operation = next(v for v in result['authenticatedOperations'] if v['consumerId'] == role)
                operation['leastPrivilege']['capturedAt'] = captured
                with self.subTest(stage='after', role=role, captured=captured), self.assertRaises(ValueError):
                    prepare.registry_result(decisions, inventory, result, {'registry-consumer-inventory.json': H}, current)

    def test_cached_unaudited_or_digest_mismatched_pull_fails(self):
        for field, value in (('emptyDisposableContentStore', False), ('auditEvidenceSha256', None),
                             ('manifestTransferred', False), ('blobsTransferred', False),
                             ('returnedDigest', oci('b'))):
            inventory = self.inventory()
            inventory['consumers'][0]['beforeCutoverOperation'][field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                prepare.registry_inventory(inventory)

    def test_pre_cutover_audit_must_name_the_inventoried_consumer(self):
        inventory = self.inventory()
        inventory['consumers'][0]['beforeCutoverOperation']['consumerId'] = 'unrelated-reader'
        with self.assertRaisesRegex(ValueError, 'authenticated-audit-operation'):
            prepare.registry_inventory(inventory)

    def test_missing_consumer_rollback_coverage_or_least_privilege_fails(self):
        inventory = self.inventory()
        inventory['coverage']['liveAndRollbackAndRetainedReleases'] = False
        with self.assertRaisesRegex(ValueError, 'inventory-incomplete'):
            prepare.registry_inventory(inventory)
        for role in ('reader', 'writer', 'replicator'):
            inventory = self.inventory()
            consumer = next(v for v in inventory['consumers'] if v['role'] == role)
            consumer['leastPrivilege']['pushDenied' if role == 'reader' else 'outOfScopeDenied'] = False
            with self.subTest(role=role), self.assertRaises(ValueError):
                prepare.registry_inventory(inventory)

    def test_pending_inventory_template_preserves_all_independently_required_categories(self):
        coverage = prepare.templates('fixture', 'fixture', {})['registry-consumer-inventory.json']['coverage']
        self.assertEqual(set(coverage), set(REQUIRED_REGISTRY_COVERAGE))
        self.assertTrue(all(value is False for value in coverage.values()))

    def test_anonymous_read_probes_require_explicit_get_for_every_kind(self):
        for kind in ANONYMOUS_READ_KINDS:
            for method in (None, 'POST', 'HEAD', 'get'):
                decisions, inventory, result, current = self.registry_records()
                probe = next(value for value in result['anonymousProbes'] if value['kind'] == kind)
                if method is None:
                    probe.pop('method')
                else:
                    probe['method'] = method
                with self.subTest(kind=kind, method=method), self.assertRaisesRegex(ValueError, 'explicit-get'):
                    prepare.registry_result(decisions, inventory, result,
                                            {'registry-consumer-inventory.json': H}, current)

    def test_anonymous_success_wrong_host_missing_blob_or_nonexisting_target_fails(self):
        for field, value in (('status', 200), ('hostname', 'unrelated.example'),
                             ('path', '/v2/'), ('knownExistingContent', False)):
            decisions, inventory, result, current = self.registry_records()
            result['anonymousProbes'][-1][field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                prepare.registry_result(decisions, inventory, result,
                                        {'registry-consumer-inventory.json': H}, current)
        decisions, inventory, result, current = self.registry_records()
        result['anonymousProbes'].pop()
        with self.assertRaises(ValueError):
            prepare.registry_result(decisions, inventory, result, {'registry-consumer-inventory.json': H}, current)

    def test_after_cutover_missing_consumer_generation_or_repeated_privilege_fails(self):
        for changed in ('consumer', 'generation', 'permission', 'timestamp'):
            decisions, inventory, result, current = self.registry_records()
            if changed == 'consumer':
                result['authenticatedOperations'].pop()
            elif changed == 'generation':
                result['generation']['consumerSetSha256'] = 'b' * 64
            elif changed == 'permission':
                result['authenticatedOperations'][0]['leastPrivilege']['deleteDenied'] = False
            else:
                result['authenticatedOperations'][0]['capturedAt'] = self.time(-180)
            with self.subTest(change=changed), self.assertRaises(ValueError):
                prepare.registry_result(decisions, inventory, result, {'registry-consumer-inventory.json': H}, current)

    def test_full_oci_graph_and_post_gc_manifest_referrer_and_blob_fetches_pass(self):
        for consumer_index in (0, 2):
            decisions, inventory, closure, result = self.gc_records()
            consumer = inventory['consumers'][consumer_index]
            for index, obj in enumerate(closure['objects']):
                operation = self.operation(consumer, -20, obj['digest'])
                if obj['kind'] in ('config', 'layer'):
                    operation['blobTransferred'] = True
                result['postGcOperations'][index] = operation
            with self.subTest(role=consumer['role']):
                prepare.gc_result(decisions, inventory, closure, result, {'registry-consumer-inventory.json': H},
                                  self.admin_records('registry-gc')[2])

    def test_missing_platform_config_layer_referrer_or_bad_closure_hash_fails(self):
        for removed_kind in ('manifest', 'config', 'layer', 'referrer'):
            closure = self.closure()
            closure['objects'] = [v for v in closure['objects'] if v['kind'] != removed_kind]
            with self.subTest(kind=removed_kind), self.assertRaises(ValueError):
                prepare.reachability(closure)
        closure = self.closure()
        closure['closureSha256'] = 'b' * 64
        with self.assertRaisesRegex(ValueError, 'hash-mismatch'):
            prepare.reachability(closure)

    def test_gc_rehearsal_retention_lock_generation_and_config_boundaries_fail_closed(self):
        for change in ('rehearsal', 'exclusions', 'lock', 'concurrent', 'generation', 'config'):
            decisions, inventory, closure, result = self.gc_records()
            if change == 'rehearsal':
                result['rehearsal']['disposableContentDeleted'] = False
            elif change == 'exclusions':
                result['excludedObjectDigests'].pop()
            elif change == 'config':
                result['gcConfigSha256'] = 'b' * 64
            else:
                result['writeReplicationLock'][{'lock': 'registryWide', 'concurrent': 'concurrentMutationObserved',
                                               'generation': 'generationAfter'}[change]] = 'b' * 64 if change == 'generation' else change == 'concurrent'
            with self.subTest(change=change), self.assertRaises(ValueError):
                prepare.gc_result(decisions, inventory, closure, result, {'registry-consumer-inventory.json': H},
                                  self.admin_records('registry-gc')[2])

    def test_gc_checkpoint_must_be_collected_entirely_under_acquired_lock(self):
        for acquired in (-85, -95):
            decisions, inventory, closure, result = self.gc_records()
            result['writeReplicationLock']['acquiredAt'] = self.time(acquired)
            with self.subTest(acquired=acquired), self.assertRaisesRegex(ValueError, 'checkpoint-not-collected-under-held'):
                prepare.gc_result(decisions, inventory, closure, result, {'registry-consumer-inventory.json': H},
                                  self.admin_records('registry-gc')[2])

    def test_post_gc_missing_blob_or_before_gc_or_after_lock_release_fails(self):
        for change in ('missing', 'before-gc', 'after-lock', 'cached'):
            decisions, inventory, closure, result = self.gc_records()
            if change == 'missing':
                result['postGcOperations'].pop()
            elif change == 'cached':
                result['postGcOperations'][0]['emptyDisposableContentStore'] = False
            else:
                result['postGcOperations'][0]['capturedAt'] = self.time(-50 if change == 'before-gc' else -8)
            with self.subTest(change=change), self.assertRaises(ValueError):
                prepare.gc_result(decisions, inventory, closure, result, {'registry-consumer-inventory.json': H},
                                  self.admin_records('registry-gc')[2])

    def test_post_gc_writer_push_and_destination_replication_cannot_prove_retained_reads(self):
        for consumer_index in (1, 2):
            decisions, inventory, closure, result = self.gc_records()
            consumer = inventory['consumers'][consumer_index]
            if consumer['role'] == 'replicator':
                consumer['replicationDirection'] = 'destination'
                consumer['beforeCutoverOperation']['replicationDirection'] = 'destination'
            for index, obj in enumerate(closure['objects']):
                operation = self.operation(consumer, -20, obj['digest'])
                if obj['kind'] in ('config', 'layer'):
                    operation['blobTransferred'] = True
                result['postGcOperations'][index] = operation
            with self.subTest(role=consumer['role']), self.assertRaisesRegex(ValueError, 'post-gc-operation'):
                prepare.gc_result(decisions, inventory, closure, result, {'registry-consumer-inventory.json': H},
                                  self.admin_records('registry-gc')[2])

    def test_unsigned_pending_bundle_never_passes_or_exposes_input_values(self):
        directory = prepare.prepare(self.project, self.evidence, 'fixture-attempt', 'operator')
        args = SimpleNamespace(project_root=self.project, bundle=directory, phase='console',
            allowed_signers=self.root / 'missing-trust', administrator_principal='fixture', ssh_keygen='/usr/bin/ssh-keygen')
        report = prepare.check_bundle(args)
        self.assertEqual(report['verificationResult'], 'fail')
        self.assertFalse(report['mutationAuthorized'])
        self.assertFalse(report['operationalAcceptance'])
        self.assertEqual(len(report['failures']), 5)

    def test_prohibited_fields_fail_without_retaining_values(self):
        for field in ('Authorization', 'Set-Cookie', 'password', 'client_secret', 'secretData', 'responseBody',
                      'access_token', 'refresh_token', 'id_token', 'Access-Token', 'refreshToken', 'ID_TOKEN'):
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, 'prohibited-evidence-field'):
                prepare.no_sensitive_fields({'nested': [{field: 'DO-NOT-RETAIN'}]})

    def test_raw_native_secret_payloads_are_refused_at_every_nesting_depth(self):
        for field in ('data', 'stringData'):
            secret = {'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'name': 'synthetic-secret'},
                      field: {'synthetic': 'c3ludGhldGlj'}}
            for record in (secret, {'resources': [secret]}, {'nested': [{'nativeResource': secret}]},
                           {'kind': 'SecretList', 'items': [{field: {'synthetic': 'c3ludGhldGlj'}}]}):
                with self.subTest(field=field, record=record), self.assertRaisesRegex(ValueError, 'prohibited-evidence-field'):
                    prepare.no_sensitive_fields(record)

    def test_secret_references_and_ordinary_nonsensitive_data_metadata_remain_valid(self):
        prepare.no_sensitive_fields({'resources': [{'apiVersion': 'v1', 'kind': 'Secret',
            'metadata': {'name': 'synthetic-secret'}, 'effectiveConfigSha256': H}],
            'metadata': {'data': {'description': 'synthetic metadata'}, 'stringData': {'note': 'synthetic note'}}})


@unittest.skipUnless(shutil.which('ssh-keygen'), 'local SSH signature verifier unavailable')
class SignatureTests(Fixtures, unittest.TestCase):
    def setUp(self):
        super().setUp()
        self.tool = Path(shutil.which('ssh-keygen')).resolve()
        self.signing_key = self.root / 'synthetic-key'
        subprocess.run([str(self.tool), '-q', '-t', 'ed25519', '-N', '', '-f', str(self.signing_key)],
                       check=True, capture_output=True)
        self.trust = self.root / 'allowed-signers'
        self.trust.write_text('fixture-administrator ' + Path(str(self.signing_key) + '.pub').read_text())
        self.trust.chmod(0o600)
        self.bundle = self.root / 'signed-bundle'
        self.bundle.mkdir(mode=0o700)

    def sign(self, name, record, namespace='hexalith-admin-exposure'):
        return self.sign_raw(name, prepare.canonical(record), namespace)

    def sign_raw(self, name, data, namespace='hexalith-admin-exposure'):
        path = self.bundle / name
        path.write_bytes(data)
        path.chmod(0o600)
        Path(str(path) + '.sig').unlink(missing_ok=True)
        subprocess.run([str(self.tool), '-Y', 'sign', '-n', namespace, '-f', str(self.signing_key), str(path)],
                       check=True, capture_output=True)
        Path(str(path) + '.sig').chmod(0o600)
        return prepare.file_digest(path)

    def signed_console_bundle(self, phase='console', records=None):
        baseline, decisions, current, proof, result = records or self.admin_records(phase)
        baseline_sha = self.sign('signed-baseline.json', baseline)
        decisions['baselineSha256'] = result['baselineSha256'] = baseline_sha
        decisions['capturedAt'] = self.time(-120)
        decisions['privateProofSha256'] = result['privateProofSha256'] = self.sign('admin-path-proof.json', proof)
        self.sign('administrator-decisions.json', decisions)
        self.sign('pre-mutation-state.json', current)
        self.sign('admin-exposure-result.json', result)
        return SimpleNamespace(project_root=self.project, bundle=self.bundle, phase=phase,
            allowed_signers=self.trust, administrator_principal='fixture-administrator', ssh_keygen=self.tool)

    def signed_registry_bundle(self, phase, records=None):
        baseline = self.snapshot(phase)
        if phase == 'registry-auth':
            decisions, inventory, result, current = records or self.registry_records()
        else:
            decisions, inventory, closure, result = records or self.gc_records()
            current = self.admin_records(phase)[2]
        decisions['capturedAt'] = self.time(-120)
        decisions['baselineSha256'] = self.sign('signed-baseline.json', baseline)
        if phase == 'registry-gc':
            decisions['retainedClosureRecordSha256'] = self.sign('retained-oci-closure.json', closure)
            decisions['gcRehearsalEvidenceSha256'] = result['rehearsal']['evidenceSha256']
        decisions['inventorySha256'] = result['inventorySha256'] = self.sign('registry-consumer-inventory.json', inventory)
        self.sign('administrator-decisions.json', decisions)
        self.sign('pre-mutation-state.json', current)
        self.sign('registry-auth-result.json' if phase == 'registry-auth' else 'registry-gc-result.json', result)
        return SimpleNamespace(project_root=self.project, bundle=self.bundle, phase=phase,
            allowed_signers=self.trust, administrator_principal='fixture-administrator', ssh_keygen=self.tool)

    def assert_consistency_failure(self, args, condition):
        report = prepare.check_bundle(args)
        self.assertEqual(report['verificationResult'], 'fail', report)
        self.assertEqual(report['failures'], [{'condition': condition}])
        self.assertFalse(report['mutationAuthorized'])
        self.assertFalse(report['operationalAcceptance'])
        self.assertFalse(report['complete'])

    def test_correctly_signed_false_or_absent_production_go_is_rejected(self):
        for value in (False, None):
            args = self.signed_console_bundle()
            decisions = json.loads((self.bundle / 'administrator-decisions.json').read_bytes())
            if value is None:
                decisions.pop('productionGo')
            else:
                decisions['productionGo'] = value
            self.sign('administrator-decisions.json', decisions)
            with self.subTest(value=value):
                self.assert_consistency_failure(args, 'production-approval-missing')

    def test_correctly_signed_failed_pre_and_post_public_oidc_is_rejected(self):
        for stage in ('before', 'after'):
            records = self.admin_records()
            record = records[3] if stage == 'before' else records[4]
            record['publicOidcChecks'][0]['result'] = 'fail'
            with self.subTest(stage=stage):
                self.assert_consistency_failure(self.signed_console_bundle(records=records),
                                                'public-oidc-regression-stop-and-rollback')

    def test_signed_malformed_route_map_and_ingress_arrays_report_fixed_failures(self):
        for change in ('route-map', 'hostname-string', 'path-string', 'empty-hostnames', 'invalid-path-element'):
            phase = 'keycloak' if change == 'path-string' else 'console'
            records = self.admin_records(phase)
            baseline, decisions, current, _, _ = records
            if change == 'route-map':
                decisions['affectedRouteUids'] = []
            else:
                field = 'hostnames' if change in ('hostname-string', 'empty-hostnames') else 'paths'
                value = {'hostname-string': 'prefix-kube.hexalith.com', 'path-string': '/admin',
                         'empty-hostnames': [], 'invalid-path-element': [None]}[change]
                for snapshot in (baseline, current):
                    snapshot['resources'][0][field] = value
            with self.subTest(change=change):
                self.assert_consistency_failure(self.signed_console_bundle(phase, records),
                    'invalid-affected-route-approval-map' if change == 'route-map' else
                    'invalid-ingress-hostname-or-path-array')

    def test_signed_console_replacement_hostname_requires_its_snapshot_and_uid_union(self):
        for change in ('missing-snapshot', 'missing-uid'):
            records = self.admin_records()
            baseline, decisions, current, _, result = records
            self.add_console_hostname(baseline, decisions, result)
            if change == 'missing-snapshot':
                baseline['resources'].pop()
                baseline['routes'].pop()
            decisions['affectedRouteUids']['console'].pop()
            current.update(resources=copy.deepcopy(baseline['resources']), routes=copy.deepcopy(baseline['routes']))
            with self.subTest(change=change):
                self.assert_consistency_failure(self.signed_console_bundle(records=records),
                                                'approved-target-route-baseline-missing')

    def test_signed_all_console_hostname_snapshots_pass_and_replacement_drift_stops(self):
        records = self.admin_records()
        baseline, decisions, current, _, result = records
        self.add_console_hostname(baseline, decisions, result)
        current.update(resources=copy.deepcopy(baseline['resources']), routes=copy.deepcopy(baseline['routes']))
        args = self.signed_console_bundle(records=records)
        report = prepare.check_bundle(args)
        self.assertEqual(report['verificationResult'], 'pass', report)
        current['routes'][-1]['dnsAnswers'] = ['192.0.2.21']
        self.sign('pre-mutation-state.json', current)
        self.assert_consistency_failure(args, 'baseline-drift-reinventory-and-reapprove')

    def test_signed_pre_and_post_private_denials_cannot_use_unrelated_targets(self):
        for stage in ('before', 'after'):
            for surface in prepare.ADMINISTRATION_SURFACES:
                for field, value in (('hostname', 'unrelated.example'), ('path', '/unrelated'),
                                     ('method', 'POST'), ('privatePathId', 'unapproved-path')):
                    records = self.admin_records()
                    checks = records[3]['unauthorizedPrivateChecks'] if stage == 'before' else records[4]['postChangeUnauthorizedPrivateChecks']
                    next(v for v in checks if v['surface'] == surface)[field] = value
                    with self.subTest(stage=stage, surface=surface, field=field):
                        self.assert_consistency_failure(self.signed_console_bundle(records=records),
                                                        'private-refusal-target-binding-mismatch')

    def test_signed_private_denials_require_targets_and_one_check_per_surface(self):
        for change in ('no-targets', 'missing-pre', 'missing-post', 'duplicate-pre', 'duplicate-post'):
            records = self.admin_records()
            if change == 'no-targets':
                records[1].pop('privateAdministrationTargets')
                condition = 'approved-private-administration-targets-missing'
            else:
                checks = records[3]['unauthorizedPrivateChecks'] if change.endswith('pre') else records[4]['postChangeUnauthorizedPrivateChecks']
                if change.startswith('missing'):
                    checks.pop()
                    condition = 'private-refusal-surface-coverage-incomplete'
                else:
                    checks.append(copy.deepcopy(checks[0]))
                    condition = 'duplicate-evidence-identity'
            with self.subTest(change=change):
                self.assert_consistency_failure(self.signed_console_bundle(records=records), condition)

    def test_signed_repository_string_cannot_grant_substring_repository_scope(self):
        records = self.registry_records()
        consumer = records[1]['consumers'][0]
        consumer['approvedRepositories'] = 'fixturerepository'
        consumer['beforeCutoverOperation']['repository'] = 'fixture'
        records[2]['authenticatedOperations'][0]['repository'] = 'fixture'
        self.assert_consistency_failure(self.signed_registry_bundle('registry-auth', records),
                                        'invalid-repository-scope')

    def test_signed_stale_permission_tests_fail_for_every_role_before_and_after_cutover(self):
        for stage in ('before', 'after'):
            for role in ('reader', 'writer', 'replicator'):
                records = self.registry_records()
                permissions = (next(v for v in records[1]['consumers'] if v['role'] == role)['leastPrivilege']
                    if stage == 'before' else next(v for v in records[2]['authenticatedOperations']
                                                 if v['consumerId'] == role)['leastPrivilege'])
                permissions['capturedAt'] = '2000-01-01T00:00:00Z'
                with self.subTest(stage=stage, role=role):
                    self.assert_consistency_failure(self.signed_registry_bundle('registry-auth', records),
                                                    'nested-evidence-outside-record-or-operation')

    def test_signed_post_cutover_permission_tests_cannot_precede_mutation_completion(self):
        records = self.registry_records()
        records[2]['authenticatedOperations'][0]['leastPrivilege']['capturedAt'] = self.time(-50)
        self.assert_consistency_failure(self.signed_registry_bundle('registry-auth', records),
                                        'nested-evidence-outside-record-or-operation')

    def test_signed_keycloak_aliases_require_both_get_and_post_without_suffix_inference(self):
        for method in ('GET', 'POST'):
            records = self.admin_records('keycloak')
            alias = '/realms/master/protocol/openid-connect/%74oken/'
            records[4]['closedAdminPaths'].append(alias)
            records[4]['externalProbes'].append({**self.denial(host='auth.tache.ai', path=alias),
                                                 'method': method})
            with self.subTest(method=method):
                self.assert_consistency_failure(self.signed_console_bundle('keycloak', records),
                                                'master-or-admin-external-negative-coverage-incomplete')

    def test_signed_keycloak_closed_paths_require_each_independently_specified_mandatory_endpoint(self):
        for path in REQUIRED_ADMIN_PATHS:
            records = self.admin_records('keycloak')
            records[4]['closedAdminPaths'].remove(path)
            records[4]['externalProbes'] = [value for value in records[4]['externalProbes'] if value['path'] != path]
            with self.subTest(path=path):
                self.assert_consistency_failure(self.signed_console_bundle('keycloak', records),
                                                'master-or-admin-path-coverage-incomplete')

    def test_signed_keycloak_requires_get_and_post_for_each_independently_specified_endpoint(self):
        for path in REQUIRED_ADMIN_PATHS:
            for method in ('GET', 'POST'):
                records = self.admin_records('keycloak')
                records[4]['externalProbes'] = [value for value in records[4]['externalProbes']
                    if (value['path'], value['method']) != (path, method)]
                with self.subTest(path=path, method=method):
                    self.assert_consistency_failure(self.signed_console_bundle('keycloak', records),
                                                    'master-or-admin-external-negative-coverage-incomplete')

    def test_signed_keycloak_denial_covers_every_concrete_hostname_on_approved_routes(self):
        for change in ('missing-denial', 'missing-route', 'missing-method'):
            records = self.admin_records('keycloak')
            host = 'other-auth.public.example'
            self.add_keycloak_hostname(records, host)
            if change == 'missing-route':
                records[0]['routes'].pop()
                records[2]['routes'].pop()
                condition = 'approved-target-route-baseline-missing'
            else:
                condition = 'master-or-admin-external-negative-coverage-incomplete'
                if change == 'missing-method':
                    records[4]['externalProbes'].extend(self.denial(host=host, path=path)
                                                        for path in records[4]['closedAdminPaths'])
            with self.subTest(change=change):
                self.assert_consistency_failure(self.signed_console_bundle('keycloak', records), condition)

    def test_signed_keycloak_related_hosts_pass_without_requiring_unrelated_ingress_denial(self):
        records = self.admin_records('keycloak')
        host = 'other-auth.public.example'
        self.add_keycloak_hostname(records, host)
        self.add_keycloak_hostname(records, 'unrelated.public.example', approved=False)
        records[4]['externalProbes'].extend({**self.denial(host=host, path=path), 'method': method}
                                           for path in records[4]['closedAdminPaths'] for method in ('GET', 'POST'))
        report = prepare.check_bundle(self.signed_console_bundle('keycloak', records))
        self.assertEqual(report['verificationResult'], 'pass', report)

    def test_signed_keycloak_hosts_on_additional_approved_uids_require_denial(self):
        records = self.admin_records('keycloak')
        host = 'other-auth.public.example'
        self.add_keycloak_hostname(records, host, approved=False)
        records[1]['affectedRouteUids']['keycloak'].append('unrelated-uid')
        self.assert_consistency_failure(self.signed_console_bundle('keycloak', records),
                                        'master-or-admin-external-negative-coverage-incomplete')

    def test_signed_valid_long_keycloak_hostname_passes(self):
        records = self.admin_records('keycloak')
        host = 'a' * 63 + '.' + 'b' * 63 + '.example.com'
        records[1]['keycloakHostname'] = host
        for snapshot in (records[0], records[2]):
            snapshot['resources'][0]['hostnames'] = [host]
            snapshot['routes'][0]['hostname'] = host
        for probe in records[4]['externalProbes']:
            probe['hostname'] = host
        report = prepare.check_bundle(self.signed_console_bundle('keycloak', records))
        self.assertEqual(report['verificationResult'], 'pass', report)

    def test_signed_wildcard_ingress_matches_only_one_concrete_subdomain_label(self):
        for host in ('auth.tache.ai', 'tache.ai', 'deep.auth.tache.ai'):
            records = self.admin_records('keycloak')
            records[1]['keycloakHostname'] = host
            for snapshot in (records[0], records[2]):
                snapshot['resources'][0]['hostnames'] = ['*.tache.ai']
                snapshot['routes'][0]['hostname'] = host
            for probe in records[4]['externalProbes']:
                probe['hostname'] = host
            args = self.signed_console_bundle('keycloak', records)
            with self.subTest(host=host):
                if host == 'auth.tache.ai':
                    report = prepare.check_bundle(args)
                    self.assertEqual(report['verificationResult'], 'pass', report)
                else:
                    self.assert_consistency_failure(args, 'approved-target-resource-identity-mismatch')

    def test_signed_wildcard_related_concrete_route_requires_its_own_denial(self):
        records = self.admin_records('keycloak')
        self.add_keycloak_hostname(records, 'admin.tache.ai')
        for snapshot in (records[0], records[2]):
            snapshot['resources'][0]['hostnames'] = ['*.tache.ai']
        self.assert_consistency_failure(self.signed_console_bundle('keycloak', records),
                                        'master-or-admin-external-negative-coverage-incomplete')

    def test_signed_overlapping_reader_writer_grant_is_inconsistent(self):
        records = self.registry_records()
        reader, writer = records[1]['consumers'][:2]
        writer['principal'] = reader['principal']
        writer['credentialSecretReference'] = reader['credentialSecretReference']
        writer['beforeCutoverOperation']['principal'] = writer['principal']
        records[2]['authenticatedOperations'][1]['principal'] = writer['principal']
        self.assert_consistency_failure(self.signed_registry_bundle('registry-auth', records),
                                        'inconsistent-reader-writer-grant')

    def test_signed_overlapping_reader_destination_replication_grant_is_inconsistent(self):
        for phase in ('registry-auth', 'registry-gc'):
            records = self.registry_records() if phase == 'registry-auth' else self.gc_records()
            reader, _, replicator = records[1]['consumers']
            replicator['principal'] = reader['principal']
            replicator['credentialSecretReference'] = reader['credentialSecretReference']
            replicator['replicationDirection'] = 'destination'
            replicator['beforeCutoverOperation'].update(principal=reader['principal'], replicationDirection='destination')
            if phase == 'registry-auth':
                records[2]['authenticatedOperations'][2].update(principal=reader['principal'], replicationDirection='destination')
            with self.subTest(phase=phase):
                self.assert_consistency_failure(self.signed_registry_bundle(phase, records),
                                                'inconsistent-reader-destination-replicator-grant')

    def test_signed_source_replication_and_distinct_destination_grants_remain_valid(self):
        for change in ('source', 'distinct-principals', 'distinct-credentials', 'nonoverlapping-scopes'):
            records = self.registry_records()
            reader, _, replicator = records[1]['consumers']
            replicator['principal'] = reader['principal'] if change != 'distinct-principals' else 'fixture-other'
            replicator['credentialSecretReference'] = (reader['credentialSecretReference']
                if change != 'distinct-credentials' else 'fixture-other-secret')
            replicator['replicationDirection'] = 'source' if change == 'source' else 'destination'
            operation = records[2]['authenticatedOperations'][2]
            for measured in (replicator['beforeCutoverOperation'], operation):
                measured.update(principal=replicator['principal'], replicationDirection=replicator['replicationDirection'])
                if change == 'nonoverlapping-scopes':
                    measured['repository'] = 'fixture/other'
            if change == 'nonoverlapping-scopes':
                replicator['approvedRepositories'] = ['fixture/other']
            with self.subTest(change=change):
                report = prepare.check_bundle(self.signed_registry_bundle('registry-auth', records))
                self.assertEqual(report['verificationResult'], 'pass', report)

    def test_signed_distinct_credentials_or_nonoverlapping_repository_scopes_remain_valid(self):
        for change in ('distinct-credentials', 'nonoverlapping-scopes'):
            records = self.registry_records()
            reader, writer = records[1]['consumers'][:2]
            writer['principal'] = reader['principal']
            writer['beforeCutoverOperation']['principal'] = writer['principal']
            operation = records[2]['authenticatedOperations'][1]
            operation['principal'] = writer['principal']
            if change == 'nonoverlapping-scopes':
                writer['credentialSecretReference'] = reader['credentialSecretReference']
                writer['approvedRepositories'] = ['fixture/other']
                writer['beforeCutoverOperation']['repository'] = operation['repository'] = 'fixture/other'
            with self.subTest(change=change):
                report = prepare.check_bundle(self.signed_registry_bundle('registry-auth', records))
                self.assertEqual(report['verificationResult'], 'pass', report)

    def test_signed_inventory_requires_each_independently_specified_coverage_category(self):
        for category in REQUIRED_REGISTRY_COVERAGE:
            for phase in ('registry-auth', 'registry-gc'):
                records = self.registry_records() if phase == 'registry-auth' else self.gc_records()
                records[1]['coverage'].pop(category)
                with self.subTest(category=category, phase=phase):
                    self.assert_consistency_failure(self.signed_registry_bundle(phase, records),
                                                    'registry-consumer-inventory-incomplete')

    def test_signed_anonymous_read_probes_require_explicit_get_for_every_kind(self):
        for kind in ANONYMOUS_READ_KINDS:
            for method in (None, 'POST', 'HEAD', 'get'):
                records = self.registry_records()
                probe = next(value for value in records[2]['anonymousProbes'] if value['kind'] == kind)
                if method is None:
                    probe.pop('method')
                else:
                    probe['method'] = method
                with self.subTest(kind=kind, method=method):
                    self.assert_consistency_failure(self.signed_registry_bundle('registry-auth', records),
                                                    'anonymous-read-requires-explicit-get')

    def test_signed_writer_and_replicator_nested_retained_delete_denial_is_required_pre_and_post(self):
        for stage in ('before', 'after'):
            for role in ('writer', 'replicator'):
                for value in (False, None):
                    records = self.registry_records()
                    permissions = (next(v for v in records[1]['consumers'] if v['role'] == role)['leastPrivilege']
                        if stage == 'before' else next(v for v in records[2]['authenticatedOperations']
                                                     if v['consumerId'] == role)['leastPrivilege'])
                    if value is None:
                        permissions.pop('retainedDeleteDenied')
                    else:
                        permissions['retainedDeleteDenied'] = value
                    with self.subTest(stage=stage, role=role, value=value):
                        self.assert_consistency_failure(self.signed_registry_bundle('registry-auth', records),
                                                        'writer-or-replicator-scope-unproved')

    def test_signed_nonfinite_json_constants_fail_the_input_gate(self):
        for value in (b'NaN', b'Infinity', b'-Infinity'):
            args = self.signed_console_bundle()
            data = (self.bundle / 'admin-exposure-result.json').read_bytes()
            self.sign_raw('admin-exposure-result.json', data[:-2] + b', "number": ' + value + b'}\n')
            report = prepare.check_bundle(args)
            with self.subTest(value=value):
                self.assertEqual(report['failures'], [{'file': 'admin-exposure-result.json',
                                                       'condition': 'signed-fresh-production-input-required'}])

    def test_all_signed_records_require_integer_schema_version_one(self):
        for name in (*prepare.COMMON_FILES, *prepare.PHASE_FILES['console']):
            for value in (None, False, True, 1.0, '1', 0, 2, {}, []):
                args = self.signed_console_bundle()
                record = json.loads((self.bundle / name).read_bytes())
                if value is None:
                    record.pop('schemaVersion')
                else:
                    record['schemaVersion'] = value
                self.sign(name, record)
                report = prepare.check_bundle(args)
                with self.subTest(name=name, value=value):
                    self.assertEqual(report['failures'], [{'file': name,
                                                           'condition': 'signed-fresh-production-input-required'}])

    def test_raw_byte_signed_excessive_json_nesting_returns_input_failure(self):
        args = self.signed_console_bundle()
        depth = sys.getrecursionlimit() + 100
        data = b'{"nested":' + b'[' * depth + b'null' + b']' * depth + b'}\n'
        self.sign_raw('admin-exposure-result.json', data)
        report = prepare.check_bundle(args)
        self.assertEqual(report['failures'], [{'file': 'admin-exposure-result.json',
                                               'condition': 'signed-fresh-production-input-required'}])
        completed = self.check_cli(args)
        self.assertEqual(completed.returncode, 1)
        self.assertNotIn('Traceback', completed.stderr)
        self.assertEqual(json.loads(completed.stdout)['failures'], report['failures'])

    def check_cli(self, args):
        return subprocess.run([sys.executable, str(Path(__file__).with_name('prepare.py')),
            '--project-root', str(args.project_root), 'check', '--bundle', str(args.bundle),
            '--phase', args.phase, '--allowed-signers', str(args.allowed_signers),
            '--administrator-principal', args.administrator_principal, '--ssh-keygen', str(args.ssh_keygen)],
            capture_output=True, text=True)

    def test_check_cli_returns_zero_for_valid_and_one_for_false_production_go(self):
        args = self.signed_console_bundle()
        for production_go, expected_exit in ((True, 0), (False, 1)):
            decisions = json.loads((self.bundle / 'administrator-decisions.json').read_bytes())
            decisions['productionGo'] = production_go
            self.sign('administrator-decisions.json', decisions)
            completed = self.check_cli(args)
            report = json.loads(completed.stdout)
            with self.subTest(production_go=production_go):
                self.assertEqual(completed.returncode, expected_exit, completed.stderr)
                self.assertEqual(report['verificationResult'], 'pass' if production_go else 'fail')
                self.assertEqual(report['failures'], [] if production_go else [{'condition': 'production-approval-missing'}])
                for flag in ('mutationAuthorized', 'operationalAcceptance', 'complete'):
                    self.assertFalse(report[flag])

    def test_evidence_expiring_during_real_ssh_verification_cannot_pass(self):
        args = self.signed_console_bundle()
        completed = self.clock + timedelta(hours=2)
        real_signed_input = prepare.signed_input
        with patch.object(prepare, 'datetime', wraps=datetime) as measured_clock:
            measured_clock.now.return_value = self.clock
            verified_count = 0

            def verify_then_advance_clock(*arguments):
                nonlocal verified_count
                result = real_signed_input(*arguments)
                verified_count += 1
                if verified_count == 5:
                    measured_clock.now.return_value = completed
                return result

            with patch.object(prepare, 'signed_input', side_effect=verify_then_advance_clock):
                report = prepare.check_bundle(args)
        self.assertEqual(verified_count, 5)
        self.assertEqual(report['verificationResult'], 'fail', report)
        self.assertEqual({v.get('file') for v in report['failures']}, set(prepare.COMMON_FILES) | set(prepare.PHASE_FILES['console']))
        self.assertTrue(all(v['condition'] == 'signed-fresh-production-input-required' for v in report['failures']))

    def test_signed_normalized_token_fields_fail_without_exposing_values(self):
        for field in ('access_token', 'refresh_token', 'id_token', 'Access-Token', 'refreshToken', 'ID_TOKEN'):
            args = self.signed_console_bundle()
            result = json.loads((self.bundle / 'admin-exposure-result.json').read_bytes())
            result['nested'] = [{field: 'DO-NOT-RETAIN'}]
            self.sign('admin-exposure-result.json', result)
            report = prepare.check_bundle(args)
            with self.subTest(field=field):
                self.assertEqual(report['failures'], [{'file': 'admin-exposure-result.json',
                                                       'condition': 'signed-fresh-production-input-required'}])
                self.assertNotIn('DO-NOT-RETAIN', prepare.canonical(report).decode())
                self.assertNotIn(field, prepare.canonical(report).decode())

    def test_signed_native_secret_payloads_fail_without_exposing_synthetic_values(self):
        for field in ('data', 'stringData'):
            for placement in ('resource', 'nested-resource', 'secret-list'):
                records = self.admin_records()
                secret = {'apiVersion': 'v1', 'kind': 'Secret', 'metadata': {'name': 'synthetic-secret'},
                          field: {'synthetic': 'c3ludGhldGlj'}}
                if placement == 'resource':
                    secret.update(namespace='kubesphere-system', name='synthetic-secret', uid='secret-uid',
                                  resourceVersion='789', effectiveConfigSha256=H)
                    for snapshot in (records[0], records[2]):
                        snapshot['resources'].append(copy.deepcopy(secret))
                    failed_files = ['signed-baseline.json', 'pre-mutation-state.json']
                else:
                    records[4]['evidenceItems'] = ({'nested': [{'nativeResource': secret}]}
                        if placement == 'nested-resource' else
                        {'kind': 'SecretList', 'items': [{field: secret[field]}]})
                    failed_files = ['admin-exposure-result.json']
                report = prepare.check_bundle(self.signed_console_bundle(records=records))
                with self.subTest(field=field, placement=placement):
                    self.assertEqual(report['failures'], [{'file': name,
                        'condition': 'signed-fresh-production-input-required'} for name in failed_files])
                    self.assertEqual(report['verificationResult'], 'fail')
                    serialized = prepare.canonical(report).decode()
                    for value in ('c3ludGhldGlj', 'synthetic-secret', 'secret-uid'):
                        self.assertNotIn(value, serialized)

    def test_signed_secret_metadata_and_ordinary_nonsensitive_data_remain_valid(self):
        records = self.admin_records()
        records[4]['evidenceItems'] = {'kind': 'Secret', 'metadata': {'name': 'synthetic-reference'},
                                     'effectiveConfigSha256': H}
        records[4]['nonsensitiveMetadata'] = {'data': {'description': 'synthetic metadata'}}
        report = prepare.check_bundle(self.signed_console_bundle(records=records))
        self.assertEqual(report['verificationResult'], 'pass', report)

    def test_signed_input_in_another_git_worktree_is_refused(self):
        args = self.signed_console_bundle()
        (self.bundle / '.git').write_text('gitdir: /synthetic/private/git-metadata\n')
        report = prepare.check_bundle(args)
        self.assertEqual(report['verificationResult'], 'fail')
        self.assertEqual(report['failures'], [{'file': name,
            'condition': 'signed-fresh-production-input-required'}
            for name in (*prepare.COMMON_FILES, *prepare.PHASE_FILES['console'])])

    def test_trust_root_and_verifier_drift_during_real_verification_fail_with_original_hashes(self):
        for target_kind in ('trust-root', 'verifier'):
            args = self.signed_console_bundle()
            verifier = self.root / 'owner-only-ssh-keygen'
            shutil.copyfile(self.tool, verifier)
            verifier.chmod(0o700)
            args.ssh_keygen = verifier
            original_trust_hash = prepare.file_digest(args.allowed_signers)
            original_verifier_hash = prepare.file_digest(verifier)
            files = (*prepare.COMMON_FILES, *prepare.PHASE_FILES['console'])
            expected_input_hashes = {name: prepare.file_digest(self.bundle / name) for name in files[:-1]}
            real_run = subprocess.run
            verified_count = 0

            def mutate_then_verify(*arguments, **kwargs):
                nonlocal verified_count
                if verified_count == len(files) - 1:
                    target = args.allowed_signers if target_kind == 'trust-root' else verifier
                    with target.open('ab') as stream:
                        stream.write(b'\n' if target_kind == 'trust-root' else b'\0')
                completed = real_run(*arguments, **kwargs)
                self.assertEqual(completed.returncode, 0, 'otherwise-valid real signature verification failed')
                verified_count += 1
                return completed

            with self.subTest(target=target_kind), patch.object(prepare.subprocess, 'run', side_effect=mutate_then_verify):
                report = prepare.check_bundle(args)
            self.assertEqual(verified_count, len(files))
            self.assertEqual(report['verificationResult'], 'fail', report)
            self.assertEqual(report['failures'], [{'file': files[-1],
                                                   'condition': 'signed-fresh-production-input-required'}])
            self.assertEqual(report['allowedSignersSha256'], original_trust_hash)
            self.assertEqual(report['signatureVerifierSha256'], original_verifier_hash)
            self.assertEqual(report['inputSha256'], expected_input_hashes)
            changed = args.allowed_signers if target_kind == 'trust-root' else verifier
            self.assertNotEqual(prepare.file_digest(changed),
                                original_trust_hash if target_kind == 'trust-root' else original_verifier_hash)
            self.assertEqual(stat.S_IMODE(changed.stat().st_mode), 0o600 if target_kind == 'trust-root' else 0o700)
            for flag in ('mutationAuthorized', 'operationalAcceptance', 'complete'):
                self.assertFalse(report[flag])

    def test_signed_gc_checkpoint_before_or_spanning_lock_acquisition_is_rejected(self):
        for acquired in (-85, -95):
            records = self.gc_records()
            records[3]['writeReplicationLock']['acquiredAt'] = self.time(acquired)
            with self.subTest(acquired=acquired):
                self.assert_consistency_failure(self.signed_registry_bundle('registry-gc', records),
                                                'gc-checkpoint-not-collected-under-held-write-replication-lock')

    def test_production_go_must_precede_fresh_reread_and_mutation(self):
        for captured in (-80, -30):
            args = self.signed_console_bundle()
            decisions = json.loads((self.bundle / 'administrator-decisions.json').read_bytes())
            decisions['capturedAt'] = self.time(captured)
            self.sign('administrator-decisions.json', decisions)
            report = prepare.check_bundle(args)
            with self.subTest(captured=captured):
                self.assertEqual(report['verificationResult'], 'fail', report)
                self.assertEqual(report['failures'], [
                    {'condition': 'production-approval-not-before-fresh-checkpoint-and-mutation'}])

    def test_admin_approval_binds_completed_private_proof(self):
        for change in ('hash', 'late-proof'):
            args = self.signed_console_bundle()
            decisions = json.loads((self.bundle / 'administrator-decisions.json').read_bytes())
            if change == 'hash':
                decisions['privateProofSha256'] = H
            else:
                proof = json.loads((self.bundle / 'admin-path-proof.json').read_bytes())
                proof['capturedAt'] = self.time(-110)
                decisions['privateProofSha256'] = self.sign('admin-path-proof.json', proof)
                result = json.loads((self.bundle / 'admin-exposure-result.json').read_bytes())
                result['privateProofSha256'] = decisions['privateProofSha256']
                self.sign('admin-exposure-result.json', result)
            self.sign('administrator-decisions.json', decisions)
            report = prepare.check_bundle(args)
            with self.subTest(change=change):
                self.assertEqual(report['verificationResult'], 'fail', report)
                self.assertEqual(report['failures'], [{'condition':
                    'production-approval-prerequisite-binding-mismatch' if change == 'hash' else
                    'production-approval-precedes-completed-prerequisite'}])

    def test_gc_approval_binds_prior_inventory_closure_and_rehearsal(self):
        for change in ('inventory', 'closure', 'rehearsal'):
            args = self.signed_registry_bundle('registry-gc')
            decisions = json.loads((self.bundle / 'administrator-decisions.json').read_bytes())
            result = json.loads((self.bundle / 'registry-gc-result.json').read_bytes())
            if change == 'rehearsal':
                result['rehearsal']['capturedAt'] = self.time(-110)
                self.sign('registry-gc-result.json', result)
            else:
                name = 'registry-consumer-inventory.json' if change == 'inventory' else 'retained-oci-closure.json'
                record = json.loads((self.bundle / name).read_bytes())
                record['capturedAt'] = self.time(-110)
                record_hash = self.sign(name, record)
                decisions['inventorySha256' if change == 'inventory' else 'retainedClosureRecordSha256'] = record_hash
                if change == 'inventory':
                    result['inventorySha256'] = record_hash
                    self.sign('registry-gc-result.json', result)
                self.sign('administrator-decisions.json', decisions)
            report = prepare.check_bundle(args)
            with self.subTest(change=change):
                self.assertEqual(report['verificationResult'], 'fail', report)
                self.assertEqual(report['failures'], [{'condition':
                    'production-gc-approval-before-successful-rehearsal-unproved' if change == 'rehearsal' else
                    'production-approval-precedes-completed-prerequisite'}])

    def test_signed_duplicate_json_keys_are_rejected(self):
        for duplicate in (b'"productionGo": false, "productionGo": true',
                          b'"productionGo": true, "nested": {"result": "fail", "result": "pass"}'):
            args = self.signed_console_bundle()
            path = self.bundle / 'administrator-decisions.json'
            original = path.read_bytes()
            path.write_bytes(original.replace(b'"productionGo": true', duplicate))
            Path(str(path) + '.sig').unlink()
            subprocess.run([str(self.tool), '-Y', 'sign', '-n', 'hexalith-admin-exposure', '-f', str(self.signing_key), str(path)],
                           check=True, capture_output=True)
            Path(str(path) + '.sig').chmod(0o600)
            report = prepare.check_bundle(args)
            self.assertEqual(report['verificationResult'], 'fail', report)
            self.assertEqual(report['failures'], [{'file': 'administrator-decisions.json',
                                                 'condition': 'signed-fresh-production-input-required'}])

    def test_signed_evidence_in_shared_directory_is_refused(self):
        args = self.signed_console_bundle()
        self.bundle.chmod(0o755)
        report = prepare.check_bundle(args)
        self.assertEqual(report['verificationResult'], 'fail', report)

    def test_valid_signatures_and_synthetic_statements_only_pass_offline_consistency(self):
        report = prepare.check_bundle(self.signed_console_bundle())
        self.assertEqual(report['verificationResult'], 'pass', report)
        self.assertFalse(report['mutationAuthorized'])
        self.assertFalse(report['operationalAcceptance'])
        self.assertFalse(report['complete'])
        self.assertEqual(report['allowedSignersSha256'], prepare.file_digest(self.trust))

    def test_signed_sole_admin_result_requires_post_closure_recovery(self):
        args = self.signed_console_bundle()
        result = json.loads((self.bundle / 'admin-exposure-result.json').read_bytes())
        result.pop('postChangeBreakGlass')
        self.sign('admin-exposure-result.json', result)
        report = prepare.check_bundle(args)
        self.assertEqual(report['verificationResult'], 'fail', report)
        self.assertEqual(report['failures'], [
            {'condition': 'independent-recovery-custody-authentication-and-reads-unproved'}])

    def test_signed_global_authentication_outage_cannot_qualify_recovery(self):
        for stage in ('before', 'after'):
            args = self.signed_console_bundle()
            name = 'admin-path-proof.json' if stage == 'before' else 'admin-exposure-result.json'
            record = json.loads((self.bundle / name).read_bytes())
            recovery = record['breakGlass'] if stage == 'before' else record['postChangeBreakGlass']
            recovery['productionPublicOidcAvailableToOtherClients'] = False
            record_hash = self.sign(name, record)
            if stage == 'before':
                decisions = json.loads((self.bundle / 'administrator-decisions.json').read_bytes())
                decisions['privateProofSha256'] = record_hash
                self.sign('administrator-decisions.json', decisions)
                result = json.loads((self.bundle / 'admin-exposure-result.json').read_bytes())
                result['privateProofSha256'] = record_hash
                self.sign('admin-exposure-result.json', result)
            report = prepare.check_bundle(args)
            with self.subTest(stage=stage):
                self.assertEqual(report['verificationResult'], 'fail', report)
                self.assertEqual(report['failures'], [
                    {'condition': 'recovery-isolation-or-production-availability-unproved'}])

    def test_wrong_principal_invalid_signature_or_changed_bytes_fail(self):
        args = self.signed_console_bundle()
        args.administrator_principal = 'untrusted-operator'
        self.assertEqual(prepare.check_bundle(args)['verificationResult'], 'fail')
        args.administrator_principal = 'fixture-administrator'
        with (self.bundle / 'admin-path-proof.json').open('ab') as stream:
            stream.write(b'\n')
        self.assertEqual(prepare.check_bundle(args)['verificationResult'], 'fail')

    def test_signing_boolean_wrong_namespace_or_nonproduction_label_cannot_pass(self):
        args = self.signed_console_bundle()
        path = self.bundle / 'admin-path-proof.json'
        proof = json.loads(path.read_bytes())
        Path(str(path) + '.sig').unlink()
        self.sign(path.name, proof, namespace='wrong-namespace')
        self.assertEqual(prepare.check_bundle(args)['verificationResult'], 'fail')
        Path(str(path) + '.sig').unlink()
        proof.update(classification='synthetic-fixture', signed=True)
        self.sign(path.name, proof)
        self.assertEqual(prepare.check_bundle(args)['verificationResult'], 'fail')

    def test_signed_registry_auth_and_gc_bundles_pass_only_offline_consistency(self):
        for phase in ('registry-auth', 'registry-gc'):
            with self.subTest(phase=phase):
                phase_directory = self.bundle / phase
                phase_directory.mkdir(mode=0o700)
                previous = self.bundle
                self.bundle = phase_directory
                try:
                    args = self.signed_registry_bundle(phase)
                    report = prepare.check_bundle(args)
                    self.assertEqual(report['verificationResult'], 'pass', report)
                    self.assertFalse(report['operationalAcceptance'])
                finally:
                    self.bundle = previous


if __name__ == '__main__':
    unittest.main()
