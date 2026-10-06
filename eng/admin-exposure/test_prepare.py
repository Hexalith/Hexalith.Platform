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
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location('exposure_prepare', Path(__file__).with_name('prepare.py'))
prepare = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(prepare)
H = 'a' * 64


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
        return {**self.base(captured=-250), 'approvedBy': 'Administrator', 'privatePathId': 'fixture-private',
                'administrationPolicy': 'sole-administrator', 'administrator': 'jpiquot',
                'operators': ['jpiquot'], 'breakGlassPathId': 'fixture-native',
                'recoveryCustodyId': 'fixture-recovery-custody',
                'administratorAccountBindings': {'nativeCluster': 'fixture-native-user', 'keycloak': 'fixture-keycloak-user'},
                'recoveryAccountBindings': {'nativeCluster': 'fixture-native-user', 'keycloak': 'fixture-keycloak-user'},
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

    def operators(self, captured=-180):
        return [{'operator': 'jpiquot', 'keycloakAdminLogin': True, 'keycloakNonDestructiveRead': True,
                 'clusterAdminRead': True, 'consolePortForward': True, 'evidenceSha256': H,
                 'accountBindings': {'nativeCluster': 'fixture-native-user', 'keycloak': 'fixture-keycloak-user'},
                 'capturedAt': self.time(captured)}]

    def recovery(self, captured=-180):
        return {'operator': 'jpiquot', 'pathId': 'fixture-native', 'custodyId': 'fixture-recovery-custody',
                'accountBindings': {'nativeCluster': 'fixture-native-user', 'keycloak': 'fixture-keycloak-user'},
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
                 'testedOperators': self.operators(), 'unauthorizedPrivateCheck': self.denial(public=False, captured=-180),
                 'breakGlass': self.recovery(), 'publicOidcChecks': self.oidc(-180)}
        host = decisions['keycloakHostname'] if phase == 'keycloak' else 'kube.hexalith.com'
        paths = prepare.ADMIN_PATHS if phase == 'keycloak' else ('/', '/login')
        probes = [self.denial(host=host, path=path) for path in paths]
        for probe in probes:
            if probe['path'].endswith('/token'):
                probe['method'] = 'POST'
        result = {**self.base(), 'closedSurface': phase, 'baselineSha256': H, 'privateProofSha256': H,
                  'administrationPolicy': 'sole-administrator', 'administrator': 'jpiquot',
                  'externalProbes': probes, 'closedAdminPaths': list(prepare.ADMIN_PATHS),
                  'consoleMode': 'port-forward', 'publicOidcChecks': self.oidc(),
                  'postChangePrivateChecks': self.operators(-20),
                  'postChangeBreakGlass': self.recovery(-20),
                  'mutationStartedAt': self.time(-60), 'mutationFinishedAt': self.time(-40)}
        return baseline, decisions, current, proof, result

    def operation(self, consumer, captured=-180, requested=None):
        result = {'consumerId': consumer['id'], 'principal': consumer['principal'],
                  'generationSha256': H, 'result': 'pass', 'auditEvidenceSha256': H,
                  'auditCorrelationId': 'fixture-request', 'capturedAt': self.time(captured),
                  'repository': 'fixture/repository',
                  'operation': {'reader': 'pull', 'writer': 'push', 'replicator': 'replicate'}[consumer['role']],
                  'emptyDisposableContentStore': True, 'manifestTransferred': True, 'blobsTransferred': True,
                  'requestedDigest': requested or oci('a'), 'returnedDigest': requested or oci('a'),
                  'leastPrivilege': copy.deepcopy(consumer['leastPrivilege'])}
        if consumer['role'] == 'replicator':
            result['replicationDirection'] = consumer['replicationDirection']
        return result

    def inventory(self):
        consumers = []
        for role in ('reader', 'writer', 'replicator'):
            permissions = {'evidenceSha256': H, 'pushDenied': True, 'deleteDenied': True,
                           'approvedOperationPassed': True, 'outOfScopeDenied': True, 'retainedDeleteDenied': True}
            consumer = {'id': role, 'principal': f'fixture-{role}', 'role': role,
                        'credentialSecretReference': f'fixture-{role}-secret',
                        'approvedRepositories': ['fixture/repository'], 'retainedDeleteDenied': True,
                        'leastPrivilege': permissions, 'replicationDirection': 'source'}
            consumer['beforeCutoverOperation'] = self.operation(consumer)
            consumers.append(consumer)
        coverage = prepare.templates('fixture', 'fixture', {})['registry-consumer-inventory.json']['coverage']
        return {**self.base(captured=-150), 'generation': self.generation(), 'inventoryComplete': True,
                'coverage': {k: True for k in coverage}, 'consumers': consumers}

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
                'status': 401, 'result': 'refused', 'sourceAddressCategory': 'external-public',
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
                     'acquiredAt': self.time(-90), 'releasedAt': self.time(-10)},
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
        result = json.loads((directory / 'admin-exposure-result.json').read_text())
        self.assertIsNone(result['postChangeBreakGlass'])
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

    def test_complete_registry_reader_writer_replication_evidence_passes(self):
        decisions, inventory, result, current = self.registry_records()
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
        for field in ('Authorization', 'Set-Cookie', 'password', 'client_secret', 'secretData', 'responseBody'):
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, 'prohibited-evidence-field'):
                prepare.no_sensitive_fields({'nested': [{field: 'DO-NOT-RETAIN'}]})


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
        path = self.bundle / name
        path.write_bytes(prepare.canonical(record))
        path.chmod(0o600)
        Path(str(path) + '.sig').unlink(missing_ok=True)
        subprocess.run([str(self.tool), '-Y', 'sign', '-n', namespace, '-f', str(self.signing_key), str(path)],
                       check=True, capture_output=True)
        Path(str(path) + '.sig').chmod(0o600)
        return prepare.file_digest(path)

    def signed_console_bundle(self):
        baseline, decisions, current, proof, result = self.admin_records()
        baseline_sha = self.sign('signed-baseline.json', baseline)
        decisions['baselineSha256'] = result['baselineSha256'] = baseline_sha
        decisions['capturedAt'] = self.time(-120)
        decisions['privateProofSha256'] = result['privateProofSha256'] = self.sign('admin-path-proof.json', proof)
        self.sign('administrator-decisions.json', decisions)
        self.sign('pre-mutation-state.json', current)
        self.sign('admin-exposure-result.json', result)
        return SimpleNamespace(project_root=self.project, bundle=self.bundle, phase='console',
            allowed_signers=self.trust, administrator_principal='fixture-administrator', ssh_keygen=self.tool)

    def signed_registry_bundle(self, phase):
        baseline = self.snapshot(phase)
        current = self.admin_records(phase)[2]
        decisions = self.decisions(phase)
        decisions['capturedAt'] = self.time(-120)
        decisions['baselineSha256'] = self.sign('signed-baseline.json', baseline)
        if phase == 'registry-auth':
            _, inventory, result, _ = self.registry_records()
        else:
            _, inventory, closure, result = self.gc_records()
            decisions['retainedClosureRecordSha256'] = self.sign('retained-oci-closure.json', closure)
            decisions['gcRehearsalEvidenceSha256'] = result['rehearsal']['evidenceSha256']
        decisions['inventorySha256'] = result['inventorySha256'] = self.sign('registry-consumer-inventory.json', inventory)
        self.sign('administrator-decisions.json', decisions)
        self.sign('pre-mutation-state.json', current)
        self.sign('registry-auth-result.json' if phase == 'registry-auth' else 'registry-gc-result.json', result)
        return SimpleNamespace(project_root=self.project, bundle=self.bundle, phase=phase,
            allowed_signers=self.trust, administrator_principal='fixture-administrator', ssh_keygen=self.tool)

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
