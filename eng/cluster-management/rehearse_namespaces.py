"""Manager-free probe of seven retained namespace finalizer equivalents.

Recovered from the private executed driver SHA-256
3b6084122836fd8febc659c02fa0b7a3f50aad9befaf1d6fa48f2cdc033a20d6.
Only local setup was changed to the explicit rehearsal CLI and shared custody lifecycle.
"""
import copy
from pathlib import Path

from evidence import canonical, digest
from rehearse import (content_review_digest, Fixture, FIXTURE_FINALIZER_NAMES, MANAGER_NAMESPACES,
                      SYSTEM_WORKSPACE_FINALIZER, assert_phase, dependency_plan, key,
                      parse_args, rehearse, retirement_scope, validate_allowlist)


class SevenNamespaceFixture(Fixture):
    def __init__(self, args, attempt):
        super().__init__(args, attempt)
        self.native_calls = []

    def kube(self, name, *argv, **kwargs):
        self.native_calls.append({'name': name, 'argv': list(argv)})
        return super().kube(name, *argv, **kwargs)

    def execute(self):
        fixture, args, attempt = self, self.args, self.attempt
        fixture.populate()
        for name in FIXTURE_FINALIZER_NAMES:
            fixture.create({'apiVersion': 'v1', 'kind': 'Namespace', 'metadata': {
                'name': name, 'labels': {'kubesphere.io/workspace': 'synthetic-keep', 'qualification.hexalith.io/retained': 'seven-finalizers'},
                'finalizers': [SYSTEM_WORKSPACE_FINALIZER, 'qualification.hexalith.io/retain']}})
        before_raw = {name: fixture.get('namespace', name) for name in FIXTURE_FINALIZER_NAMES}
        before, settled = fixture.settled_inventory()
        if not settled:
            raise ValueError('seven-namespace-before-not-settled')
        manager = [v for v in before if v['namespace'] in MANAGER_NAMESPACES and v['kind'] in ('Pod', 'Deployment', 'ReplicaSet', 'StatefulSet', 'DaemonSet')]
        if manager:
            raise ValueError('seven-namespace-manager-present')
        canary_before = fixture.run('seven-namespace-canary-before', ['docker', 'exec', fixture.node, 'cat', '/var/local/s426-synthetic/canary']).stdout
        fixture.reviewed_content_digests = {uid: content_review_digest(raw) for uid, raw in fixture.raw_inventory_by_uid.items()}
        attempt.encrypt('reviewed-full-content-digests', canonical(fixture.reviewed_content_digests), args.age, args.recipient)
        by_uid = {v['uid']: v for v in before}
        scope = retirement_scope(before)
        plan = dependency_plan(before, scope)
        if scope or len(plan) != 1 or plan[0]['phase'] != 'namespace-finalizers' or len(plan[0]['roots']) != 7:
            raise ValueError('seven-namespace-plan-not-exact')
        phase = plan[0]
        roots = [by_uid[uid] for uid in phase['roots']]
        if {v['name'] for v in roots} != set(FIXTURE_FINALIZER_NAMES):
            raise ValueError('seven-namespace-identities-not-exact')
        actions = [{**v, 'action': 'remove-named-finalizer', 'finalizer': SYSTEM_WORKSPACE_FINALIZER,
                    'propagation': 'Foreground'} for v in roots]
        validate_allowlist(before, actions)
        attempt.record('seven-namespace-allowlist.json', {'actions': actions, 'phases': plan,
                                                       'namespaceDeletionExpected': False, 'productionAccepted': False})
        after = fixture.retire_phase(phase, set(by_uid), by_uid, fixture.discover({'v1'}))
        modifications = {key(by_uid[uid]): SYSTEM_WORKSPACE_FINALIZER for uid in phase['expectedModified']}
        assert_phase(set(by_uid), before, after, set(), modifications)
        after_raw = {name: fixture.get('namespace', name) for name in FIXTURE_FINALIZER_NAMES}
        comparison = []
        for name in FIXTURE_FINALIZER_NAMES:
            initial, current = before_raw[name], after_raw[name]
            checks = {'uidUnchanged': initial['metadata']['uid'] == current['metadata']['uid'],
                      'labelsUnchanged': initial['metadata'].get('labels', {}) == current['metadata'].get('labels', {}),
                      'namespaceSpecFinalizersUnchanged': initial.get('spec', {}).get('finalizers') == current.get('spec', {}).get('finalizers'),
                      'onlyKubeSphereMetadataFinalizerRemoved': current['metadata']['finalizers'] == [v for v in initial['metadata']['finalizers'] if v != SYSTEM_WORKSPACE_FINALIZER],
                      'unrelatedMetadataFinalizerRetained': 'qualification.hexalith.io/retain' in current['metadata']['finalizers'],
                      'namespaceNotTerminating': not current['metadata'].get('deletionTimestamp')}
            if not all(checks.values()):
                raise ValueError('seven-namespace-preservation-failed')
            comparison.append({'namespace': name, **checks})
        canary_after = fixture.run('seven-namespace-canary-after', ['docker', 'exec', fixture.node, 'cat', '/var/local/s426-synthetic/canary']).stdout
        if canary_before != b'synthetic-426-canary' or canary_after != canary_before:
            raise ValueError('seven-namespace-canary-changed')
        def is_namespace_request(call, verb):
            argv = call['argv']
            if not argv or argv[0] != verb:
                return False
            if '--raw' in argv:
                path = argv[argv.index('--raw') + 1]
                return path.startswith('/api/v1/namespaces/') and len(path.split('/')) == 5
            return any(value in ('namespace', 'namespaces', 'ns') for value in argv[1:])
        deletes = [v for v in fixture.native_calls if is_namespace_request(v, 'delete')]
        puts = [v for v in fixture.native_calls if is_namespace_request(v, 'replace')]
        if deletes or len(puts) != 7:
            raise ValueError('seven-namespace-request-trace-failed')
        attempt.encrypt('raw-namespace-comparison', canonical({'before': before_raw, 'after': after_raw}), args.age, args.recipient)
        attempt.encrypt('native-command-trace', canonical(fixture.native_calls), args.age, args.recipient)
        attempt.record('seven-namespace-result.json', {'state': 'passed', 'scope': 'fresh manager-free fixture only',
            'namespaceCount': len(comparison), 'namespaces': comparison, 'managerRuntimeRemaining': manager,
            'actualNamespaceDeleteRequests': len(deletes), 'actualNamespaceUidResourceVersionBoundPutRequests': len(puts),
            'nativeRequestTraceExport': 'native-command-trace.age', 'rawNamespaceReadbackExport': 'raw-namespace-comparison.age',
            'allBaselineIdentitiesAndProtectedStoragePreserved': True, 'syntheticCanaryBeforeAfterEqual': True,
            'canarySha256': digest(canary_after), 'actualDependencyPlanAndRetirePhaseUsed': True,
            'productionCredentialsOrDataImported': False, 'productionAccepted': False})



def main():
    args = parse_args()
    if args.ks_chart or args.rollback:
        raise SystemExit('Seven-namespace probe requires a manager-free fixture; omit --ks-chart and --rollback.')
    directory, state = rehearse(args, fixture_type=SevenNamespaceFixture, driver_path=Path(__file__))
    print(f'Private fixture evidence: {directory}\nRehearsal: {state}; production retirement remains unaccepted')
    if state.startswith('failed'):
        raise SystemExit(2)


if __name__ == '__main__':
    main()
