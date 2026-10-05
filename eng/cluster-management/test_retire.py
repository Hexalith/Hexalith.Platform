"""Real detached signatures and injected native transport exercise fail-closed execution."""
import copy
from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from evidence import Attempt, canonical, digest, file_digest, write_new
from qualify import project_resource
import retire
from retire import Native as ProductionNative
from rehearse import api_path, PhaseExecutor, Fixture


def object_(api, kind, name, namespace=None, **fields):
    meta = {'name': name, 'uid': kind + '-' + name, 'resourceVersion': '1'}
    if namespace:
        meta['namespace'] = namespace
    return {'apiVersion': api, 'kind': kind, 'metadata': meta, **fields}


class MemoryNative(retire.Native):
    """Keep real phase guards, explicit dispatch and signing; inject only API observations."""
    live = {}
    fault = None
    sent = []

    def __init__(self, args, attempt):
        super().__init__(args, attempt)
        self.live = copy.deepcopy(type(self).live)

    def census(self):
        self.raw_inventory_by_uid = copy.deepcopy(self.live)
        inventory = [project_resource(v) for v in self.live.values()]
        self.discovery = {(v['apiVersion'], v['kind']): (v['kind'].lower() + 's', bool(v['namespace'])) for v in inventory}
        source = {'sourceClusterUid': 'Namespace-kube-system', 'nativeEndpoint': 'https://native.test:6443',
                  'capturedAt': retire.now(), 'resources': inventory, 'coverage': []}
        source['resolvedNativeConfigurationSha256'] = self.resolved_configuration()
        return inventory, source

    def resolved_configuration(self):
        credential = getattr(self.args, 'referenced_credential', None)
        return digest(self.args.kubeconfig.read_bytes() + (credential.read_bytes() if credential else b''))

    def settled_inventory(self):
        return self.census()[0], True

    def transport(self, argv, input=None, **kwargs):
        verb = next(v for v in argv if v in ('get', 'delete', 'replace'))
        path = argv[argv.index('--raw') + 1]
        raw = next((v for v in self.live.values() if api_path(self.discovery, project_resource(v)) == path), None)
        if verb == 'get':
            return subprocess.CompletedProcess(argv, 0, canonical(raw), b'') if raw else subprocess.CompletedProcess(
                argv, 1, b'', b'Error from server (NotFound): absent\n')
        type(self).sent.append((verb, path, json.loads(input)))
        if type(self).fault == 'conflict':
            return subprocess.CompletedProcess(argv, 1, b'', b'Error from server (Conflict): changed\n')
        if verb == 'delete':
            del self.live[raw['metadata']['uid']]
        else:
            self.live[raw['metadata']['uid']] = json.loads(input)
        if type(self).fault == 'protected-deletion':
            self.live.pop('PersistentVolumeClaim-data', None)
        if type(self).fault == 'authority-drift':
            self.live['ClusterRoleBinding-jpiquot-cluster-admin']['roleRef']['name'] = 'view'
        if type(self).fault == 'recreate':
            changed = copy.deepcopy(raw)
            changed['metadata']['uid'] += '-recreated'
            self.live[changed['metadata']['uid']] = changed
        if type(self).fault == 'activate-authority':
            account = object_('v1', 'ServiceAccount', 'kubesphere.users.jpiquot', 'kubesphere-system')
            self.live[account['metadata']['uid']] = account
        return subprocess.CompletedProcess(argv, 0, b'{}', b'')


@unittest.skipUnless(shutil.which('ssh-keygen'), 'SSH signatures require ssh-keygen')
class RetirementTests(unittest.TestCase):
    sealed = {}
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.repo = self.root / 'repo'
        (self.repo / 'eng/kubernetes-upgrade').mkdir(parents=True)
        (self.repo / 'eng/kubernetes-upgrade/MAINTENANCE.md').write_text('immutable maintenance proposal')
        self.key = self.root / 'signer'
        subprocess.run(['ssh-keygen', '-q', '-t', 'ed25519', '-N', '', '-f', str(self.key)], check=True)
        self.trust = self.root / 'allowed_signers'
        self.trust.write_text('admin namespaces="hexalith-recovery,hexalith-retirement" ' + self.key.with_suffix('.pub').read_text())
        self.trust.chmod(0o600)
        self.kubeconfig = self.root / 'native-config'
        self.kubeconfig.write_text('explicit native configuration')
        self.kubeconfig.chmod(0o600)
        self.age = self.root / 'age'
        self.age.write_text('#!/bin/sh\nexit 0\n')
        self.age.chmod(0o700)
        self.preflight = self.root / 'preflight'
        self.backup = self.root / 'backup'
        self.preflight.mkdir(mode=0o700)
        self.backup.mkdir(mode=0o700)
        raw = [object_('v1', 'Namespace', 'kube-system'), object_('v1', 'Namespace', 'kubesphere-system'),
               object_('v1', 'PersistentVolumeClaim', 'data', 'application-data', spec={'volumeName': 'data-pv'}, status={'phase': 'Bound'}),
               object_('v1', 'PersistentVolume', 'data-pv', spec={'claimRef': {'uid': 'PersistentVolumeClaim-data',
                   'namespace': 'application-data', 'name': 'data'}}),
               object_('rbac.authorization.k8s.io/v1', 'ClusterRoleBinding', 'native-admin', roleRef={
                   'apiGroup': 'rbac.authorization.k8s.io', 'kind': 'ClusterRole', 'name': 'cluster-admin'},
                   subjects=[{'kind': 'User', 'name': 'independent-administrator'}])]
        raw += [object_('postgresql.cnpg.io/v1', 'Cluster', 'keycloak-postgres', 'keycloak')]
        raw += [object_('apps/v1', 'StatefulSet', name, namespace, spec={'replicas': 1})
                for namespace, name in [('openbao', 'hexalith-keys'), ('hexalith-memories', 'redis-stack'), ('hexalith-memories', 'falkordb')]]
        for api, kind, namespace, name in sorted(retire.AUTHORITY, key=str):
            fields = {'roleRef': {'apiGroup': 'rbac.authorization.k8s.io', 'kind': 'ClusterRole', 'name': 'cluster-admin'}} \
                if kind == 'ClusterRoleBinding' else {}
            raw.append(object_(api, kind, name, namespace, **fields))
        for name in ('manager-a', 'manager-b'):
            manager = object_('apps/v1', 'Deployment', name, 'kubesphere-system', spec={'replicas': 1, 'template': {'spec': {'containers': []}}})
            manager['metadata']['annotations'] = {'meta.helm.sh/release-name': 'ks-core',
                'meta.helm.sh/release-namespace': 'kubesphere-system'}
            raw.append(manager)
        MemoryNative.live = {v['metadata']['uid']: v for v in raw}
        MemoryNative.sent, MemoryNative.fault = [], None
        self.created_native = []
        type(self).sealed = {}
        self.original_run = subprocess.run
        self.encrypt_patch = patch.object(Attempt, 'encrypt', self.seal)
        self.encrypt_patch.start()
        self.native_patch = patch('retire.Native', self.native)
        self.native_patch.start()
        self.transport_patch = patch('retire.subprocess.run', self.dispatch)
        self.transport_patch.start()
        self.rehearsals = {'catalog': {'records': 'unit-test-injected-receipts'}, 'namespaces': {'records': 'unit-test-injected-receipts'}}
        self.rehearsal_patch = patch('retire.validate_rehearsals', return_value=self.rehearsals)
        self.rehearsal_patch.start()
        self.args = retire.parser().parse_args(['--attempt-id', 'planning', '--execution-attempt-id', 'execution',
            '--operator', 'admin', '--context', 'explicit-native', '--recipient', 'admin-recipient',
            '--readback-recipient', 'readback-recipient', '--administrator-principal', 'admin',
            '--project-root', str(self.repo), '--evidence-root', str(self.root / 'custody'), '--kubeconfig', str(self.kubeconfig),
            '--kubectl', '/usr/bin/true', '--helm', '/usr/bin/true', '--age', str(self.age), '--ssh-keygen', shutil.which('ssh-keygen'),
            '--readback-identity', str(self.key), '--allowed-signers', str(self.trust), '--receipt-key', str(self.key),
            '--catalog-rehearsal', str(self.root / 'catalog'), '--namespace-rehearsal', str(self.root / 'namespaces')])

    def tearDown(self):
        for p in (self.rehearsal_patch, self.transport_patch, self.native_patch, self.encrypt_patch):
            p.stop()
        self.temp.cleanup()

    @staticmethod
    def seal(self, name, plaintext, age, recipient):
        # Crypto lifecycle itself has dedicated tests in test_evidence.py. This injection keeps
        # arbitrary raw content out of records and allows real SSH verification in the integration path.
        ciphertext = b'SEALED:' + digest(plaintext).encode()
        RetirementTests.sealed[ciphertext] = plaintext
        write_new(self.directory / (name + '.age'), ciphertext)
        entry = {'file': name + '.age', 'plaintextSha256': digest(plaintext), 'ciphertextSha256': digest(ciphertext),
                 'ciphertextBytes': len(ciphertext), 'recipientStanzas': [], 'readbackVerified': True}
        self.exports.append(entry)
        return entry

    def native(self, args, attempt):
        result = MemoryNative(args, attempt)
        self.created_native.append(result)
        return result

    def dispatch(self, argv, **kwargs):
        if '--decrypt' in argv:
            return subprocess.CompletedProcess(argv, 0, type(self).sealed[kwargs['input']], b'')
        if '--raw' in argv:
            return self.created_native[-1].transport(argv, **kwargs)
        return self.original_run(argv, **kwargs)

    def signed(self, directory, name, value, namespace='hexalith-retirement'):
        path = directory / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(canonical(value))
        path.chmod(0o600)
        self.original_run(['ssh-keygen', '-Y', 'sign', '-n', namespace, '-f', str(self.key), str(path)],
                          check=True, capture_output=True)
        Path(str(path) + '.sig').chmod(0o600)
        return digest(path.read_bytes())

    def recent(self, **values):
        return {'capturedAt': retire.now(), 'expiresAt': (datetime.now(timezone.utc) + timedelta(hours=1)).isoformat(), **values}

    def plan(self):
        directory, state = retire.run(self.args)
        self.assertEqual(state, 'proposed', json.loads((directory / 'result.json').read_bytes()))
        self.assertEqual(MemoryNative.sent, [])
        self.assertFalse((self.repo / '_bmad-output/implementation-artifacts/evidence/epic-4/4-26').exists())
        plan_path = directory / 'plan.json'
        plan = json.loads(plan_path.read_bytes())
        self.assertEqual(plan['attemptId'], 'execution')
        self.assertFalse(plan['productionApproved'])
        self.assertEqual(len(plan['retainedAuthorityUids']), 5)
        self.args.plan = plan_path
        self.args.attempt_id, self.args.execute, self.args.arm_attempt = 'execution', True, 'execution'
        self.args.preflight, self.args.backup_bundle, self.args.approval = self.preflight, self.backup, self.root / 'approval.json'
        self.populate_gate(plan)
        return plan

    def populate_gate(self, plan):
        policy = self.recent(trustRootSha256=file_digest(self.trust), administratorPrincipal='admin',
                             maximumValidationAgeSeconds=900, maximumRecoveryAgeSeconds=3600, maximumPlanAgeSeconds=900)
        hashes = {'retirement-policy.json': self.signed(self.preflight, 'retirement-policy.json', policy)}
        backup_hashes = {}
        policy40 = {'recoveryId': 'current-recovery', 'systems': {w: {'maximumProofAgeSeconds': 3600}
                                                                  for w in ('keycloak', 'openbao', 'memories')}}
        namespace = 'hexalith-recovery'
        for name in retire.BACKUP_FILES[:2]:
            backup_hashes[name] = self.signed(self.backup, name, policy40, namespace)
        proof_uids = {'keycloak': 'Cluster-keycloak-postgres', 'openbao': 'StatefulSet-hexalith-keys',
                      'memories': {'redis-stack': 'StatefulSet-redis-stack', 'falkordb': 'StatefulSet-falkordb'}}
        for name in (retire.BACKUP_FILES[2], *retire.BACKUP_FILES[4:7], retire.BACKUP_FILES[3]):
            record = self.recent(recoveryId='current-recovery', verificationResult='pass',
                policySha256=backup_hashes[retire.BACKUP_FILES[0]], evidenceManifestSha256=backup_hashes[retire.BACKUP_FILES[1]],
                assembledAt=retire.now(), sourceUid='Namespace-kube-system', measuredRpo={'withinPolicy': True})
            if name in retire.BACKUP_FILES[4:7]:
                system = name.split('/')[0]
                record.update(system=system, runKind='final', sourceUid=proof_uids[system])
            if name == retire.BACKUP_FILES[3]:
                record['proofs'] = {system: {'sha256': backup_hashes[proof],
                    'signatureSha256': file_digest(str(self.backup / proof) + '.sig')}
                    for system, proof in zip(('keycloak', 'openbao', 'memories'), retire.BACKUP_FILES[4:7])}
            backup_hashes[name] = self.signed(self.backup, name, record, namespace)
        validation = self.recent(recoveryId='current-recovery', verificationResult='pass', dryRun=False,
            policySha256=backup_hashes[retire.BACKUP_FILES[0]], evidenceManifestSha256=backup_hashes[retire.BACKUP_FILES[1]],
            gateSha256=backup_hashes['gate/backup-gate.json'], validatedAt=retire.now(), inputs={name: {
                'sha256': sha, 'signatureSha256': file_digest(str(self.backup / name) + '.sig'), 'signatureVerified': True}
                for name, sha in backup_hashes.items()}, summary={'fail': 0, 'skipped': 0},
            checks=[{'status': 'pass'}], providerReadBack={'manifestObjectsReRead': 3, 'manifestObjectsMatching': 3})
        backup_hashes[retire.BACKUP_FILES[-1]] = self.signed(self.backup, retire.BACKUP_FILES[-1], validation, namespace)
        base = dict(attemptId='execution', planSha256=file_digest(self.args.plan),
                    sourceClusterUid=plan['sourceClusterUid'], verificationResult='pass')
        records = {
            'recovery-validation.json': dict(backupInputs=backup_hashes, independentOffNodeReadback=True, readOnlyValidatorCredentialUsed=True),
            'console-closure.json': dict(story='4.2', accepted=True, externalPathIndependent=True, publicConsoleDenied=True, publicOidcPassed=True),
            'external-etcd-recovery-point.json': dict(snapshotIntegrityVerified=True, encrypted=True, immutableOffNodeVersion='version-1',
                independentReadbackVerified=True, sourceMemberId='source-member', revision=10, keyCount=20,
                keyHash='1' * 64, canarySha256='2' * 64, configurationSha256='3' * 64),
            'external-etcd-isolated-restore.json': dict(targetClusterUid='distinct-restored-target', targetMemberId='new-member',
                sourceFenced=True, isolated=True, memberHealthy=True, cleanupVerified=True, revision=10, keyCount=20,
                keyHash='1' * 64, canarySha256='2' * 64),
            'node-recovery-bundle.json': dict(encrypted=True, independentReadbackVerified=True, configurationMatchesSource=True,
                ownerModesPreserved=True, testedRecoveryProcedure=True, immutableOffNodeVersion='node-version-1', configurationSha256='3' * 64),
            'native-access.json': dict(clusterAdminBindingUid='ClusterRoleBinding-native-admin', directPrivateAccess=True,
                administratorAccessVerified=True, independentCustodyReadback=True, breakGlassIndependent=True),
            'pre-retirement-health.json': dict(nativeApi=True, dns=True, cni=True, storage=True, admission=True,
                publicApplications=True, publicOidc=True, authenticatedWorkloads={w: True for w in retire.WORKLOADS})}
        for name in retire.GATE_FILES:
            if name in ('external-etcd-isolated-restore.json', 'node-recovery-bundle.json'):
                records[name]['recoveryPointSha256'] = hashes['external-etcd-recovery-point.json']
            hashes[name] = self.signed(self.preflight, name, self.recent(**base, **records[name]))
        approval = self.recent(kind='production-retirement-approval', approvedBy='Administrator', **base,
            preflightInputs=hashes, backupInputs=backup_hashes, rehearsals=plan['rehearsals'],
            **{k: plan[k] for k in ('codeSha256', 'procedureSha256', 'toolSha256', 'maintenanceSha256')},
            allowlistSha256=digest(canonical(plan['actions'])), outageScope='approved workload interruption scope',
            incidentOwner='Administrator', recoveryOwner='Administrator', operator='admin', incidentChannel='approved-channel',
            stopConditions='stop at first refusal or failed outcome', recoveryConditions='new incident decision; isolated restore only',
            windowStart=(datetime.now(timezone.utc) - timedelta(minutes=1)).isoformat(),
            windowEnd=(datetime.now(timezone.utc) + timedelta(hours=1)).isoformat())
        self.signed(self.root, 'approval.json', approval)

    def result(self):
        directory, state = retire.run(self.args)
        return state, json.loads((directory / 'result.json').read_bytes())

    def modify_signed(self, path, update):
        value = json.loads(path.read_bytes())
        value.update(update)
        Path(str(path) + '.sig').unlink()
        self.signed(path.parent, path.name, value)

    def test_shared_phase_execution_and_real_plan_to_execute_handoff(self):
        self.assertIs(Fixture.native_retire, PhaseExecutor.native_retire)
        plan = self.plan()
        state, result = self.result()
        self.assertEqual(state, 'retired-awaiting-acceptance', result)
        self.assertEqual(len(MemoryNative.sent), 2)
        self.assertFalse(result['productionRetirementAccepted'])
        self.assertEqual(result['upgradeGate'], 'closed')
        self.assertTrue((self.root / 'custody/retirement/planning/result.json').exists())
        self.assertTrue((self.root / 'custody/retirement/execution/result.json.sig').exists())
        self.assertEqual({body['preconditions']['uid'] for _, _, body in MemoryNative.sent},
                         {a['uid'] for a in plan['actions']})

    def test_unarmed_gate_is_zero_mutations(self):
        self.plan()
        self.args.arm_attempt = None
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['mutationRequests'], 0)
        self.assertEqual(MemoryNative.sent, [])

    def test_stale_approval_is_zero_mutations(self):
        self.plan()
        self.modify_signed(self.args.approval, {'expiresAt': '2000-01-01T00:00:00Z'})
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'stale-or-future-evidence')
        self.assertEqual(MemoryNative.sent, [])

    def test_caller_verified_flag_cannot_replace_actual_signature(self):
        self.plan()
        value = json.loads(self.args.approval.read_bytes())
        value['signatureVerified'] = True
        self.args.approval.write_bytes(canonical(value))
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'unverified-evidence-signature')
        self.assertEqual(MemoryNative.sent, [])

    def test_plan_content_drift_is_zero_mutations(self):
        self.plan()
        MemoryNative.live['Deployment-manager-a']['spec']['replicas'] = 2
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'plan-full-content-drift')
        self.assertEqual(MemoryNative.sent, [])

    def test_conflict_stops_first_request_without_retry(self):
        self.plan()
        MemoryNative.fault = 'conflict'
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(len(MemoryNative.sent), 1)
        self.assertTrue(result['incidentDecisionRequired'])
        self.assertFalse(result['automaticRecoveryPerformed'])

    def test_unexpected_protected_deletion_stops_before_next_request(self):
        self.plan()
        MemoryNative.fault = 'protected-deletion'
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(len(MemoryNative.sent), 1)
        self.assertEqual(result['reasonCode'], 'unexpected-deletion-during-retirement')

    def test_retained_authority_change_stops_before_next_request(self):
        self.plan()
        MemoryNative.fault = 'authority-drift'
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(len(MemoryNative.sent), 1)
        self.assertEqual(result['reasonCode'], 'retained-full-content-drift')

    def test_recreated_controller_stops_before_next_request(self):
        self.plan()
        MemoryNative.fault = 'recreate'
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(len(MemoryNative.sent), 1)
        self.assertIn(result['reasonCode'], ('preserved-identity-or-binding-changed', 'controller-recreated-retired-object'))

    def test_failed_authenticated_health_is_zero_mutations(self):
        self.plan()
        self.modify_signed(self.preflight / 'pre-retirement-health.json', {'authenticatedWorkloads': {'keycloak': False}})
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'pre-retirement-authenticated-health-failed')
        self.assertEqual(MemoryNative.sent, [])

    def test_missing_recovery_or_console_gate_is_zero_mutations(self):
        self.plan()
        (self.preflight / 'console-closure.json.sig').unlink()
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(MemoryNative.sent, [])

    def test_tool_changed_after_first_request_stops_before_second(self):
        self.plan()
        original = MemoryNative.transport
        def changed(native, argv, **kwargs):
            result = original(native, argv, **kwargs)
            if len(MemoryNative.sent) == 1:
                self.age.write_text('#!/bin/sh\nexit 1\n')
            return result
        with patch.object(MemoryNative, 'transport', changed):
            state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(len(MemoryNative.sent), 1)
        self.assertEqual(result['reasonCode'], 'execution-input-changed')

    def test_credential_changed_after_first_request_stops_before_second(self):
        self.plan()
        original = MemoryNative.transport
        def changed(native, argv, **kwargs):
            result = original(native, argv, **kwargs)
            if len(MemoryNative.sent) == 1:
                self.kubeconfig.write_text('a different cluster context')
            return result
        with patch.object(MemoryNative, 'transport', changed):
            state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(len(MemoryNative.sent), 1)
        self.assertEqual(result['reasonCode'], 'execution-input-changed')

    def test_only_named_namespace_finalizer_changes_and_no_namespace_delete(self):
        namespace = MemoryNative.live['Namespace-kube-system']
        namespace['metadata']['finalizers'] = [retire.SYSTEM_WORKSPACE_FINALIZER, 'unrelated.example/retain']
        namespace['spec'] = {'finalizers': ['kubernetes']}
        self.plan()
        state, result = self.result()
        self.assertEqual(state, 'retired-awaiting-acceptance', result)
        calls = [(verb, body) for verb, path, body in MemoryNative.sent if path.endswith('/namespaces/kube-system')]
        self.assertEqual(len(calls), 1)
        self.assertEqual(calls[0][0], 'replace')
        self.assertEqual(calls[0][1]['metadata']['finalizers'], ['unrelated.example/retain'])
        self.assertEqual(calls[0][1]['spec']['finalizers'], ['kubernetes'])

    def test_live_source_identity_change_stops_before_next_request(self):
        self.plan()
        original = MemoryNative.census
        def drifted(native):
            inventory, source = original(native)
            if len(MemoryNative.sent) == 1:
                source['sourceClusterUid'] = 'different-cluster'
            return inventory, source
        with patch.object(MemoryNative, 'census', drifted):
            state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(len(MemoryNative.sent), 1)
        self.assertEqual(result['reasonCode'], 'live-source-identity-changed')

    def postflight(self):
        self.plan()
        state, execution = self.result()
        self.assertEqual(state, 'retired-awaiting-acceptance', execution)
        MemoryNative.live = copy.deepcopy(self.created_native[-1].live)
        self.args.assess_result = self.root / 'custody/retirement/execution/result.json'
        self.args.attempt_id, self.args.execute, self.args.arm_attempt = 'assessment', False, None
        self.args.postflight = self.root / 'postflight'
        self.args.postflight.mkdir(mode=0o700)
        base = dict(executionAttemptId='execution', planSha256=execution['outcome']['planSha256'],
                    sourceClusterUid='Namespace-kube-system', verificationResult='pass')
        records = {
            'post-retirement-health.json': dict(nativeApi=True, privateAdministration=True, dns=True, cni=True, storage=True,
                admission=True, publicApplications=True, publicOidc=True, authenticatedWorkloads={w: True for w in retire.WORKLOADS}),
            'external-console-denial.json': dict(externalPathIndependent=True, publicConsoleDenied=True),
            'external-etcd-recovery-point.json': dict(snapshotIntegrityVerified=True, encrypted=True, independentReadbackVerified=True,
                immutableOffNodeVersion='fresh-etcd-version', configurationSha256='1' * 64,
                sourceMemberId='fresh-source-member', revision=100, keyCount=200, keyHash='2' * 64, canarySha256='3' * 64),
            'node-recovery-bundle.json': dict(encrypted=True, independentReadbackVerified=True, configurationMatchesSource=True,
                ownerModesPreserved=True, testedRecoveryProcedure=True, immutableOffNodeVersion='fresh-node-version', configurationSha256='1' * 64)}
        hashes = {}
        for name, fields in records.items():
            if name == 'node-recovery-bundle.json':
                fields['recoveryPointSha256'] = hashes['external-etcd-recovery-point.json']
            hashes[name] = self.signed(self.args.postflight, name, self.recent(**base, **fields))
        return execution

    def test_failed_post_retirement_health_leaves_acceptance_and_hop_closed(self):
        self.postflight()
        self.modify_signed(self.args.postflight / 'post-retirement-health.json', {'storage': False})
        prior_requests = len(MemoryNative.sent)
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'post-retirement-authenticated-health-failed')
        self.assertEqual(len(MemoryNative.sent), prior_requests)
        self.assertFalse(result['productionRetirementAccepted'])
        self.assertEqual(result['upgradeGate'], 'closed')

    def accepted_assessment(self, setup=True):
        if setup:
            self.postflight()
        state, assessed = self.result()
        self.assertEqual(state, 'assessed-awaiting-administrator-acceptance', assessed)
        self.assertFalse(assessed['productionRetirementAccepted'])
        outcome = assessed['outcome']
        acceptance = self.recent(kind='retirement-acceptance', approvedBy='Administrator', accepted=True,
            executionAttemptId='execution', outcomeSha256=outcome['assessmentOutcomeSha256'],
            postflightInputs=outcome['postflightInputs'], executionResultSha256=outcome['executionResultSha256'],
            retainedObjects=outcome['retainedObjects'], incidents=[])
        self.signed(self.root, 'acceptance.json', acceptance)
        self.args.acceptance = self.root / 'acceptance.json'
        self.args.attempt_id = 'accepted-assessment'

    def test_signed_postflight_and_exact_administrator_acceptance(self):
        self.accepted_assessment()
        prior_requests = len(MemoryNative.sent)
        state, result = self.result()
        self.assertEqual(state, 'accepted', result)
        self.assertTrue(result['productionRetirementAccepted'])
        self.assertEqual(result['upgradeGate'], 'closed')
        self.assertEqual(len(MemoryNative.sent), prior_requests)
        path = self.root / 'custody/retirement/accepted-assessment/result.json'
        verified = self.original_run(['ssh-keygen', '-Y', 'verify', '-f', str(self.trust), '-I', 'admin',
            '-n', 'hexalith-retirement', '-s', str(path) + '.sig'], input=path.read_bytes(), capture_output=True)
        self.assertEqual(verified.returncode, 0)

    def test_assessment_refuses_appended_trust_signer_before_census(self):
        self.postflight()
        prior_requests = len(MemoryNative.sent)
        self.trust.write_text(self.trust.read_text() + 'unapproved namespaces="hexalith-retirement" ' + self.key.with_suffix('.pub').read_text())
        with patch.object(MemoryNative, 'census', side_effect=AssertionError('changed trust must fail before census')):
            state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'assessment-trust-context-or-principal-changed')
        self.assertEqual(result['mutationRequests'], 0)
        self.assertEqual(len(MemoryNative.sent), prior_requests)

    def test_assessment_refuses_wrong_receipt_key_before_census(self):
        self.postflight()
        wrong = self.root / 'assessment-wrong-key'
        self.original_run(['ssh-keygen', '-q', '-t', 'ed25519', '-N', '', '-f', str(wrong)], check=True)
        self.args.receipt_key = wrong
        with patch.object(MemoryNative, 'census', side_effect=AssertionError('wrong key must fail before census')):
            state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'assessment-execution-input-changed')
        self.assertEqual(result['mutationRequests'], 0)

    def test_assessment_refuses_changed_administrator_principal(self):
        self.postflight()
        self.args.administrator_principal = 'unapproved'
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'assessment-trust-context-or-principal-changed')
        self.assertEqual(result['mutationRequests'], 0)

    def test_assessment_refuses_changed_tool_before_census(self):
        tool = self.root / 'bound-kubectl'
        shutil.copyfile('/usr/bin/true', tool)
        tool.chmod(0o700)
        self.args.kubectl = tool
        self.postflight()
        tool.write_bytes(tool.read_bytes() + b'changed tool bytes')
        with patch.object(MemoryNative, 'census', side_effect=AssertionError('changed tool must fail before census')):
            state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'assessment-execution-input-changed')
        self.assertEqual(result['mutationRequests'], 0)

    def test_assessment_refuses_cloned_cluster_uid_on_different_endpoint(self):
        self.postflight()
        original = MemoryNative.census
        def clone(native):
            inventory, source = original(native)
            source['nativeEndpoint'] = 'https://isolated-clone.test:6443'
            return inventory, source
        with patch.object(MemoryNative, 'census', clone):
            state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'assessment-source-endpoint-changed')
        self.assertEqual(result['mutationRequests'], 0)

    def test_assessment_refuses_referenced_credential_drift_with_unchanged_kubeconfig(self):
        credential = self.root / 'assessment-referenced-key'
        credential.write_text('synthetic original key')
        credential.chmod(0o600)
        self.args.referenced_credential = credential
        self.postflight()
        original_sha = file_digest(self.kubeconfig)
        credential.write_text('synthetic replaced key')
        with patch.object(MemoryNative, 'census', side_effect=AssertionError('credential drift must fail before census')):
            state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'resolved-native-configuration-changed')
        self.assertEqual(file_digest(self.kubeconfig), original_sha)
        self.assertEqual(result['mutationRequests'], 0)

    def test_accepted_receipt_wrong_actual_signer_is_never_persisted_as_accepted(self):
        self.accepted_assessment()
        wrong = self.root / 'wrong-actual-signer'
        self.original_run(['ssh-keygen', '-q', '-t', 'ed25519', '-N', '', '-f', str(wrong)], check=True)
        original_dispatch = self.dispatch
        def substituted_signer(argv, **kwargs):
            if '-Y' in argv and 'sign' in argv:
                changed = list(argv)
                changed[changed.index('-f') + 1] = str(wrong)
                return self.original_run(changed, **kwargs)
            return original_dispatch(argv, **kwargs)
        with patch('retire.subprocess.run', substituted_signer):
            state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'receipt-signing-failed')
        self.assertFalse(result['productionRetirementAccepted'])
        self.assertEqual(result['mutationRequests'], 0)

    def test_missing_postflight_recovery_measurements_leave_acceptance_closed(self):
        self.postflight()
        path = self.args.postflight / 'external-etcd-recovery-point.json'
        original = json.loads(path.read_bytes())
        for field in ('sourceMemberId', 'revision', 'keyCount', 'keyHash', 'canarySha256', 'configurationSha256'):
            with self.subTest(field=field):
                value = copy.deepcopy(original)
                value.pop(field)
                Path(str(path) + '.sig').unlink()
                self.signed(path.parent, path.name, value)
                self.args.attempt_id = 'missing-point-' + field.lower()
                state, result = self.result()
                self.assertEqual(state, 'failed-closed')
                self.assertEqual(result['reasonCode'], 'external-etcd-recovery-point-incomplete')
                self.assertFalse(result['productionRetirementAccepted'])
                self.assertEqual(result['mutationRequests'], 0)

    def test_invalid_postflight_recovery_measurements_leave_acceptance_closed(self):
        self.postflight()
        path = self.args.postflight / 'external-etcd-recovery-point.json'
        original = json.loads(path.read_bytes())
        invalid = {'sourceMemberId': False, 'revision': 0, 'keyCount': True,
                   'keyHash': {}, 'canarySha256': 'not-a-digest', 'configurationSha256': 'G' * 64}
        for field, bad in invalid.items():
            with self.subTest(field=field):
                value = dict(original, **{field: bad})
                Path(str(path) + '.sig').unlink()
                self.signed(path.parent, path.name, value)
                self.args.attempt_id = 'invalid-point-' + field.lower()
                state, result = self.result()
                self.assertEqual(state, 'failed-closed')
                self.assertEqual(result['reasonCode'], 'external-etcd-recovery-point-incomplete')
                self.assertFalse(result['productionRetirementAccepted'])
                self.assertEqual(result['mutationRequests'], 0)

    def expire_after_receipt_verification(self):
        original_dispatch = self.dispatch
        class Clock(datetime):
            current = datetime.now(timezone.utc)
            @classmethod
            def now(cls, tz=None):
                return cls.current.astimezone(tz) if tz else cls.current.replace(tzinfo=None)
        final_signature = str(self.root / 'custody/retirement/accepted-assessment/result.json.sig')
        observed = []
        def delayed_verification(argv, **kwargs):
            response = original_dispatch(argv, **kwargs)
            if '-Y' in argv and 'verify' in argv and final_signature in argv:
                observed.append(True)
                Clock.current += timedelta(minutes=10)
            return response
        with patch('retire.datetime', Clock), patch('retire.subprocess.run', delayed_verification):
            state, result = self.result()
        self.assertEqual(observed, [True])
        self.assertEqual(state, 'failed-closed')
        self.assertFalse(result['productionRetirementAccepted'])
        self.assertEqual(result['mutationRequests'], 0)

    def test_postflight_expiry_during_final_signing_prevents_accepted_persistence(self):
        self.postflight()
        path = self.args.postflight / 'external-etcd-recovery-point.json'
        self.modify_signed(path, {'expiresAt': (datetime.now(timezone.utc) + timedelta(minutes=5)).isoformat()})
        self.modify_signed(self.args.postflight / 'node-recovery-bundle.json', {'recoveryPointSha256': file_digest(path)})
        self.accepted_assessment(setup=False)
        self.expire_after_receipt_verification()

    def test_acceptance_expiry_during_final_signing_prevents_accepted_persistence(self):
        self.accepted_assessment()
        self.modify_signed(self.args.acceptance, {'expiresAt': (datetime.now(timezone.utc) + timedelta(minutes=5)).isoformat()})
        self.expire_after_receipt_verification()

    def test_custom_iam_binding_cannot_substitute_for_independent_native_access(self):
        MemoryNative.live['ClusterRoleBinding-native-admin']['apiVersion'] = 'iam.kubesphere.io/v1beta1'
        self.plan()
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'independent-native-authority-incomplete')
        self.assertEqual(result['mutationRequests'], 0)
        self.assertEqual(MemoryNative.sent, [])

    def test_independent_native_binding_requires_native_role_reference_group(self):
        MemoryNative.live['ClusterRoleBinding-native-admin']['roleRef']['apiGroup'] = 'iam.kubesphere.io'
        self.plan()
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'independent-native-authority-incomplete')
        self.assertEqual(result['mutationRequests'], 0)
        self.assertEqual(MemoryNative.sent, [])

    def test_independent_native_binding_requires_cluster_role_reference_kind(self):
        MemoryNative.live['ClusterRoleBinding-native-admin']['roleRef']['kind'] = 'GlobalRole'
        self.plan()
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'independent-native-authority-incomplete')
        self.assertEqual(result['mutationRequests'], 0)
        self.assertEqual(MemoryNative.sent, [])

    def test_live_node_lease_renewal_during_plan_handoff_remains_zero_mutations(self):
        lease = object_('coordination.k8s.io/v1', 'Lease', 'synthetic-node', 'kube-node-lease',
                        spec={'holderIdentity': 'synthetic-node', 'renewTime': '2026-10-05T10:00:00Z'})
        MemoryNative.live[lease['metadata']['uid']] = lease
        self.plan()
        MemoryNative.live[lease['metadata']['uid']]['spec']['renewTime'] = '2026-10-05T10:00:10Z'
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'plan-full-content-drift')
        self.assertEqual(result['mutationRequests'], 0)
        self.assertEqual(MemoryNative.sent, [])

    def namespace_only_plan(self):
        MemoryNative.live = {uid: v for uid, v in MemoryNative.live.items() if v['kind'] != 'Deployment'}
        MemoryNative.live['Namespace-kube-system']['metadata']['finalizers'] = [retire.SYSTEM_WORKSPACE_FINALIZER]
        return self.plan()

    def test_direct_protected_delete_is_refused_even_inside_approved_namespace_phase(self):
        self.namespace_only_plan()
        def invalid_delete(native, path, reviewed):
            return native.kube('wrong-action', 'delete', '--raw', path, '-f', '-', obj={
                'preconditions': {'uid': reviewed['uid'], 'resourceVersion': reviewed['resourceVersion']},
                'propagationPolicy': 'Foreground'})
        with patch.object(MemoryNative, 'remove_namespace_finalizer', invalid_delete):
            state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'request-delete-precondition-mismatch')
        self.assertEqual(result['mutationRequests'], 0)
        self.assertEqual(MemoryNative.sent, [])

    def test_direct_put_with_unrelated_content_is_refused_before_request(self):
        self.namespace_only_plan()
        def invalid_put(native, path, reviewed):
            body = copy.deepcopy(native.live[reviewed['uid']])
            body['metadata']['finalizers'] = []
            body['metadata']['annotations'] = {'unrelated.example/authority': 'new-value'}
            return native.kube('wrong-content', 'replace', '--raw', path, '-f', '-', obj=body)
        with patch.object(MemoryNative, 'remove_namespace_finalizer', invalid_put):
            state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'unapproved-finalizer-put-content')
        self.assertEqual(result['mutationRequests'], 0)
        self.assertEqual(MemoryNative.sent, [])

    def test_new_dangling_authority_subject_stops_before_second_request(self):
        self.plan()
        MemoryNative.fault = 'activate-authority'
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(len(MemoryNative.sent), 1)
        self.assertEqual(result['reasonCode'], 'retained-authority-subject-activated')

    def test_changed_helm_annotation_cannot_promote_retained_crd(self):
        raw = object_('apiextensions.k8s.io/v1', 'CustomResourceDefinition', 'retained.example', spec={})
        raw['metadata']['annotations'] = {'meta.helm.sh/release-name': 'ks-core',
                                         'meta.helm.sh/release-namespace': 'kubesphere-system'}
        MemoryNative.live[raw['metadata']['uid']] = raw
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'protected-resource-in-production-scope')
        self.assertEqual(MemoryNative.sent, [])

    def test_shared_workload_cannot_enter_scope_through_helm_annotation(self):
        raw = object_('apps/v1', 'Deployment', 'shared-identity', 'keycloak', spec={})
        raw['metadata']['annotations'] = {'meta.helm.sh/release-name': 'ks-core',
                                         'meta.helm.sh/release-namespace': 'kubesphere-system'}
        MemoryNative.live[raw['metadata']['uid']] = raw
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'shared-component-outside-manager-namespace-in-scope')
        self.assertEqual(MemoryNative.sent, [])

    def test_untrusted_receipt_signing_key_is_refused_before_arming(self):
        wrong = self.root / 'wrong-receipt-key'
        self.original_run(['ssh-keygen', '-q', '-t', 'ed25519', '-N', '', '-f', str(wrong)], check=True)
        self.args.receipt_key = wrong
        self.plan()
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'unverified-evidence-signature')
        self.assertEqual(result['mutationRequests'], 0)
        self.assertEqual(MemoryNative.sent, [])

    def test_qualified_cross_namespace_counterpart_requires_exact_phase_and_labels(self):
        raw = object_('rbac.authorization.k8s.io/v1', 'RoleBinding',
                      'kubesphere:iam:system-workspace:system-workspace-admin', 'kube-system')
        raw['metadata']['labels'] = {'iam.kubesphere.io/workspacerolebinding-ref': 'system-workspace-admin',
                                     'kubesphere.io/workspace': 'system-workspace'}
        v = project_resource(raw)
        action = {**v, 'action': 'delete', 'phase': 'workspace-role-bindings'}
        retire.validate_production_scope([v], [action])
        with self.assertRaisesRegex(ValueError, 'shared-component-outside-manager-namespace-in-scope'):
            retire.validate_production_scope([v], [{**action, 'phase': 'remaining-release-objects'}])

    def test_new_manager_deployment_without_pod_refuses_required_absence_phase(self):
        self.namespace_only_plan()
        original = MemoryNative.remove_namespace_finalizer
        def new_manager(native, path, reviewed):
            raw = object_('apps/v1', 'Deployment', 'recreated-new-name', 'kubesphere-system', spec={'replicas': 1})
            native.live[raw['metadata']['uid']] = raw
            return original(native, path, reviewed)
        with patch.object(MemoryNative, 'remove_namespace_finalizer', new_manager):
            state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'manager-runtime-present-before-request')
        self.assertEqual(result['mutationRequests'], 0)
        self.assertEqual(MemoryNative.sent, [])

    def test_non_namespace_finalizer_put_requires_observed_prior_delete(self):
        MemoryNative.live = {uid: v for uid, v in MemoryNative.live.items() if v['kind'] != 'Deployment'}
        raw = object_('tenant.kubesphere.io/v1beta1', 'Workspace', 'system-workspace')
        raw['metadata']['finalizers'] = [retire.SYSTEM_WORKSPACE_FINALIZER]
        MemoryNative.live[raw['metadata']['uid']] = raw
        self.plan()
        def invalid_put(native, path, uid, finalizer):
            body = copy.deepcopy(native.live[uid])
            body['metadata']['finalizers'] = []
            return native.kube('no-delete-put', 'replace', '--raw', path, '-f', '-', obj=body)
        with patch.object(MemoryNative, 'native_retire', return_value={'outcome': 'native-delete-accepted'}), \
             patch.object(MemoryNative, 'remove_named_finalizer', invalid_put):
            state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'named-finalizer-delete-state-unverified')
        self.assertEqual(result['mutationRequests'], 0)
        self.assertEqual(MemoryNative.sent, [])

    def test_cluster_scoped_shared_kinds_cannot_enter_scope_through_helm_annotation(self):
        for api, kind in [('v1', 'Node'), ('storage.k8s.io/v1', 'CSIDriver')]:
            with self.subTest(kind=kind):
                raw = object_(api, kind, 'shared-cluster-component')
                raw['metadata']['annotations'] = {'meta.helm.sh/release-name': 'ks-core',
                                                 'meta.helm.sh/release-namespace': 'kubesphere-system'}
                v = project_resource(raw)
                with self.assertRaisesRegex(ValueError, 'cluster-shared-kind-outside-reviewed-manager-catalog'):
                    retire.validate_production_scope([v], [{**v, 'action': 'delete'}])
        raw = object_('storage.k8s.io/v1', 'CSIDriver', 'shared-driver')
        raw['metadata']['annotations'] = {'meta.helm.sh/release-name': 'ks-core',
                                         'meta.helm.sh/release-namespace': 'kubesphere-system'}
        MemoryNative.live[raw['metadata']['uid']] = raw
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['mutationRequests'], 0)
        self.assertEqual(MemoryNative.sent, [])

    def test_cluster_admin_role_used_by_retained_native_bindings_cannot_enter_scope(self):
        role = object_('rbac.authorization.k8s.io/v1', 'ClusterRole', 'cluster-admin', rules=[])
        role['metadata']['annotations'] = {'meta.helm.sh/release-name': 'ks-core',
                                          'meta.helm.sh/release-namespace': 'kubesphere-system'}
        MemoryNative.live[role['metadata']['uid']] = role
        state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'retained-native-binding-consumes-retired-role')
        self.assertEqual(result['mutationRequests'], 0)
        self.assertEqual(MemoryNative.sent, [])

    def test_referenced_credential_change_with_unchanged_kubeconfig_refuses_next_write(self):
        credential = self.root / 'referenced-client-key'
        credential.write_text('synthetic original client key')
        credential.chmod(0o600)
        self.args.referenced_credential = credential
        self.plan()
        kubeconfig_sha = file_digest(self.args.kubeconfig)
        original = MemoryNative.transport
        def changed(native, argv, **kwargs):
            result = original(native, argv, **kwargs)
            if len(MemoryNative.sent) == 1:
                credential.write_text('synthetic changed client key')
            return result
        with patch.object(MemoryNative, 'transport', changed):
            state, result = self.result()
        self.assertEqual(state, 'failed-closed')
        self.assertEqual(result['reasonCode'], 'resolved-native-configuration-changed')
        self.assertEqual(file_digest(self.args.kubeconfig), kubeconfig_sha)
        self.assertEqual(len(MemoryNative.sent), 1)

    def test_unbound_exec_and_auth_provider_hooks_fail_closed(self):
        for hook in ('exec', 'auth-provider', 'tokenFile'):
            with self.subTest(hook=hook), self.assertRaisesRegex(ValueError, 'unbound-native-credential-hook'):
                retire.resolved_configuration_sha({'users': [{'user': {hook: {'command': 'unbound-helper'}}}]})

    def test_unsupported_hook_runs_only_offline_config_view_before_refusal(self):
        attempt = Attempt(self.repo, self.root / 'offline-custody', 'unsupported-hook', 'retirement',
                          readback_recipient='readback', readback_identity=self.key)
        native = ProductionNative(self.args, attempt)
        response = subprocess.CompletedProcess([], 0, canonical({'users': [{'user': {'exec': {'command': 'unbound-helper'}}}]}), b'')
        with patch('retire.subprocess.run', return_value=response) as run:
            with self.assertRaisesRegex(ValueError, 'unbound-native-credential-hook'):
                native.census()
        self.assertEqual(run.call_count, 1)
        argv = run.call_args.args[0]
        self.assertIn('config', argv)
        self.assertIn('view', argv)
        self.assertNotIn('version', argv)
        self.assertNotIn('get', argv)
        self.assertEqual(native.mutation_requests, 0)

    def test_native_list_missing_typemeta_keeps_matching_private_baseline(self):
        attempt = Attempt(self.repo, self.root / 'capture-custody', 'native-capture', 'retirement',
                          readback_recipient='readback', readback_identity=self.key)
        capture = retire.NativeCapture(self.args, retire.CaptureSink(attempt, self.args, 'capture'))
        raw = object_('v1', 'Namespace', 'synthetic-namespace')
        raw.pop('apiVersion')
        raw.pop('kind')
        listing = {'apiVersion': 'v1', 'kind': 'NamespaceList', 'items': [raw], 'metadata': {}}
        with patch.object(capture, 'kube', return_value=listing):
            capture.list_resource('v1', '/api/v1', {'name': 'namespaces', 'kind': 'Namespace'}, 'namespace-list')
        self.assertEqual(len(capture.inventory), 1)
        self.assertEqual(project_resource(capture.raw_by_uid[raw['metadata']['uid']]), capture.inventory[0])


if __name__ == '__main__':
    unittest.main()
