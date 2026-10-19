"""Evidence gates reject successful exits with incomplete or substituted execution."""
import copy
import hashlib
import io
import json
from pathlib import Path
import tempfile
import tarfile
import unittest
from unittest.mock import patch

from verify_works_host import (CROSS_CHECKS, PROOF_NAMES, PROOFS, RESTORE_PHASE, ROWS, SNAPSHOT_PHASE, bind_persisted_outcomes,
                               require_consumer_pin, require_consumer_pins, require_host_package_pins, require_passed_tests,
                               require_published_runtime_source, required_phases, selected_sdk_version, sha256, verify_host_records)

VERSION = '3.125.1'
DIGEST = 'a' * 64
HOOK_TYPE = 'Hexalith.Platform.Works.Runtime.WorksRecoveryHostingStartup'


def heads(count):
    return {f'source-{index}': {'expectedOwnerSequence': 2, 'expectedGlobalPosition': 10 + index,
                                'observed': {'OwnerSequenceNumber': 2, 'Position': 10 + index}} for index in range(count)}


def valid_states():
    """The smallest state per required phase that carries every qualifying proof field."""
    refused = {'receiverStatus': 403, 'callerStatus': 403}
    disposition = {'audit': {'Attempts': [{'Actor': 'synthetic-operator-416'}]}, 'acceptedCallerStatus': 204, 'beforeGeneration': 0,
                   'afterGeneration': 2, 'wrongPurpose': refused, 'wrongTenant': refused, 'wrongAggregate': refused,
                   'auditOutage': {'receiverStatus': 500, 'callerStatus': 500}}
    fleet = {'certificates/ca.crt': 'b' * 64, 'redis-key-count': '682'}
    runtime = {'/runtime/Hexalith.Works.dll': 'c' * 64}
    return {
        'platform-historical-lifecycle-tree-concurrent-claim-persisted': {'familyDigest': DIGEST, 'claimOutcomes': ['Completed', 'Rejected'],
                                                                         'lifecycle': {'Events': [{}] * 7}, 'replay': {'Status': 'Completed'}},
        'platform-historical-restart-replay-and-family': {'completed': True, 'claimCount': 1, 'familyDigest': DIGEST},
        'platform-executor-filtering-and-origin-tenant-publish-refusals': {'wrongCaller': 401, 'wrongPort': 401, 'forbiddenPublish': 403,
                                                                           'own': {'found': True}, 'visible': ['mine', 'queued', 'race'],
                                                                           'hidden': {'found': False}, 'wrongActor': 'Forbidden',
                                                                           'wrongTenant': 'Forbidden', 'unchangedFamily': DIGEST},
        'complete-supported-process-coverage': {'Complete': True, 'Unresolved': 0, 'RetainedCandidates': 11},
        'periodic-writers-disabled-readiness-degraded': {'status': 'Degraded'},
        'disabled-recovery-otlp-alert-export': [{'Name': 'eventstore.domain.recovery.alert', 'Value': 1}],
        'durable-recovery-alert-export-and-clear': [{'Name': 'eventstore.domain.recovery.alert', 'Value': 1},
                                                    {'Name': 'eventstore.domain.recovery.alert', 'Value': 0}],
        'platform-named-redelivery-capture-through-commit-complete-family': {
            'redelivered': {'Outcomes': [{'Status': 1}]}, 'fenced': {'Outcomes': [{'Status': 2}]},
            'concurrentDelivery': {'redeliveryLogicalCount': 1}, 'before': {'Digest': DIGEST}, 'after': {'Digest': DIGEST},
            'negatives': {'wrongOrigin': 401, 'wrongOperation': 403}},
        'platform-named-conflict-durable-quarantine-with-unchanged-family': {'acceptedFamilySha256': DIGEST, 'unchangedFamilySha256': DIGEST,
                                                                             'repeatedConflictCount': 1, 'conflict': {'SourceStream': 'item'}},
        'platform-initial-projection-convergence': {'naturalCatchUp': True, 'heads': heads(11)},
        'platform-focused-projection-rebuild': {'concurrentDelivery': {'automaticCatchUpAfterCommit': True, 'oldGenerationStayedReadable': True,
                                                                       'after': {'Digest': DIGEST}, 'expectedDigest': DIGEST},
                                                'sources': ['source'] * 11, 'committed': {'Phase': 3}},
        'platform-query-and-explicit-receipt-replay': {'query': {'found': True, 'latestAcceptedSourceSequence': 7},
                                                       'receipt': {'EffectId': 'EFFECT'}, 'conflictingReplayStatus': 500,
                                                       'durableCollisionCount': 1, 'attributedCollisionAudit': True},
        'successful-callback-no-periodic-recovery': {'witness': {'entries': [{}]}, 'processCandidate': True},
        'platform-two-host-ha-lost-firing-stale-callback-recovery': {
            'actualStaleCallbackObserved': True, 'staleSchedulerFireResumed': 1, 'retiredReminderRemovedAfterFiring': True,
            'beforeCallbacks': 1, 'afterCallbacks': 2,
            'trust': {'trustedClientAcceptedByServer': True, 'noClientCertificateRefusedByServer': True, 'foreignCaClientRefusedByServer': True,
                      'nameMismatchRejectedByProbe': True, 'serverIdentity': 'spiffe://public/ns/default/works'}},
        'cascade-expiry-restart-receipt-and-checkpoint': {'replay': {'ReplayObserved': True}, 'childExpired': 1, 'expiryReceipt': {'disposition': 0}},
        'concurrent-sdk-reminder-reissue-after-lost-scheduler-wakeup': {'callers': 2, 'persistedSchedulerReminder': {'actorType': 'WorksSdkReminderActor'}},
        'paged-process-candidates-pre-restart': {'expectedSources': 11, 'pages': [{}, {}, {}, {}]},
        'canonical-related-child-creation-gap': {'creationGap': {'ReasonCode': 'evidence-unavailable'}, 'existingChildCancellations': 0, 'readiness': 'Degraded'},
        'paged-process-restart-and-shared-store-owner-fencing': {'linkedPages': 4, 'candidates': [{}] * 11, 'staleOwnerWriteRefused': True,
                                                                 'concurrentOwners': 1, 'priorFence': 32, 'successorFence': 33},
        'protected-scoped-disposition-audit-before-mutation': disposition,
        'callback-protected-scoped-disposition-audit-before-mutation': copy.deepcopy(disposition),
        'platform-sdk-named-mode-legacy-route-refused': {'legacyRouteStatus': 404, 'missingChannelStatus': 401,
                                                         'acceptedFamilySha256': DIGEST, 'unchangedFamilySha256': DIGEST},
        'child-resume-noop-restart-receipt-and-checkpoint': {'replay': {'ReplayObserved': True, 'Disposition': 2}, 'checkpoint': {'Checkpoint': 4}},
        'five-child-persisted-target-receipts': [{'receipt': {'purpose': 'works.cascade-cancel', 'disposition': 0}, 'cancelled': 1}] * 5,
        'cascade-restart-receipt-and-checkpoint': {'receipt': {'effectId': 'CASCADE'}, 'committedEffect': 'CASCADE', 'checkpoint': {'Checkpoint': 7}},
        SNAPSHOT_PHASE: {'fleet': fleet, 'runtimeHashes': runtime},
        RESTORE_PHASE: {'convergence': {'query': {'found': True, 'conversationCorrelationId': {'value': 'synthetic-capture-416-0123456789'}},
                                        'receipt': {'Replayed': True, 'Disposition': 2, 'EffectId': 'EFFECT'}, 'resumedCount': 1,
                                        'restoredSweep': {'Complete': True, 'Unresolved': 0, 'RetainedCandidates': 11},
                                        'durableCollisionCount': 1, 'restoredOperatorAuditAttempts': 1},
                        'fleet': copy.deepcopy(fleet), 'runtimeHashes': copy.deepcopy(runtime), 'dateResumed': 1, 'childCancelled': 1,
                        'childExpired': 1, 'restoredConfigurationVerified': True},
    }


class WorksEvidenceGateTests(unittest.TestCase):
    def result(self, attributes='', test='<test method="Required" result="Pass" />'):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        path = Path(temporary.name) / 'result.xml'
        path.write_text(f'<assemblies><assembly total="1" {attributes}>{test}</assembly></assemblies>')
        return path

    def directory(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        return Path(temporary.name)

    def test_skipped_failed_error_and_not_run_counts_are_refused(self):
        for key in ['skipped', 'failed', 'errors', 'not-run']:
            with self.subTest(key=key), self.assertRaises(ValueError):
                require_passed_tests(self.result(f'{key}="1"'))

    def test_empty_filtered_or_nonpassing_execution_is_refused(self):
        for test in ['', '<test method="Required" result="Skip" />', '<test method="Required" result="Fail" />']:
            with self.subTest(test=test), self.assertRaises(ValueError):
                require_passed_tests(self.result(test=test))
        with self.assertRaises(ValueError):
            require_passed_tests(self.result(), ['Missing'])

    def test_passed_required_method_has_exact_result_hash(self):
        path = self.result()
        self.assertEqual(require_passed_tests(path, ['Required']), {'total': 1, 'passed': 1, 'skipped': 0, 'sha256': sha256(path)})

    def test_required_supporting_classes_must_execute(self):
        with self.assertRaises(ValueError):
            require_passed_tests(self.result(), required_types=["RequiredClass"])
        path = self.result(test='<test type="RequiredClass" method="Required" result="Pass" />')
        self.assertEqual(require_passed_tests(path, required_types=["RequiredClass"])["total"], 1)

    def archive(self, files):
        archive = io.BytesIO()
        with tarfile.open(fileobj=archive, mode='w') as output:
            for name, content in files.items():
                member = tarfile.TarInfo(name)
                member.size = len(content)
                output.addfile(member, io.BytesIO(content))
        return archive.getvalue()

    def test_runtime_source_must_match_public_archive_producer(self):
        content = b'public producer source'
        snapshot = {'root': '/fixture/sdk', 'commit': 'metadata-only-new-head',
                    'sourceFiles': {'src/Producer.cs': hashlib.sha256(content).hexdigest()}, 'inventorySha256': 'bound-inventory'}
        producers = {'Published.SDK': {'repositoryCommit': 'public-producer'}}
        with patch('verify_works_host.subprocess.check_output', return_value=self.archive({'src/Producer.cs': content})):
            result = require_published_runtime_source(snapshot, producers)
            self.assertTrue(result['matchesPublishedProducer'])
            self.assertEqual(result['repositoryCommit'], 'public-producer')
            snapshot['sourceFiles']['src/Producer.cs'] = 'unpublished-patch'
            with self.assertRaises(ValueError):
                require_published_runtime_source(snapshot, producers)

    def test_every_published_src_build_input_is_compared(self):
        files = {'src/Producer.cs': b'source', 'src/Directory.Build.props': b'<Project />', 'src/appsettings.json': b'{}'}
        snapshot = {'root': '/fixture/sdk', 'commit': None, 'inventorySha256': 'bound-inventory',
                    'sourceFiles': {name: hashlib.sha256(content).hexdigest() for name, content in files.items()}}
        producers = {'Published.SDK': {'repositoryCommit': 'public-producer'}}
        with patch('verify_works_host.subprocess.check_output', return_value=self.archive(files)):
            self.assertTrue(require_published_runtime_source(snapshot, producers)['matchesPublishedProducer'])
            for name in ['src/Directory.Build.props', 'src/appsettings.json']:
                changed = copy.deepcopy(snapshot)
                changed['sourceFiles'][name] = 'unpublished-input'
                with self.subTest(name=name), self.assertRaisesRegex(ValueError, name):
                    require_published_runtime_source(changed, producers)
                removed = copy.deepcopy(snapshot)
                del removed['sourceFiles'][name]
                with self.subTest(removed=name), self.assertRaisesRegex(ValueError, name):
                    require_published_runtime_source(removed, producers)

    def host_record(self, root, role='platform-target', ha=False):
        assembly = root / 'bin/release/apphost.dll'
        assembly.parent.mkdir(parents=True, exist_ok=True)
        assembly.write_bytes(b'bound fixture')
        runtime = root / 'consumer/Hexalith.Platform.Works.Runtime.dll'
        runtime.parent.mkdir(parents=True, exist_ok=True)
        runtime.write_bytes(b'platform runtime')
        sdk = [{'name': 'Hexalith.EventStore.Aspire', 'version': VERSION + '.0', 'path': str(assembly), 'sha256': sha256(assembly)}]
        names = ['eventstore', 'works', 'eventstore-operations', 'eventstore-admin'] + (['works-ha'] if ha else [])
        hosting = {'hostingStartupAssemblies': ['Hexalith.Platform.Works.Runtime', 'Hexalith.Works.IntegrationTests'],
                   'startupHooks': [str(runtime), str(root / 'consumer/Hexalith.Works.IntegrationTests.dll')],
                   'startups': [{'name': 'Hexalith.Platform.Works.Runtime', 'loaded': True, 'location': str(runtime), 'sha256': sha256(runtime),
                                 'hostingStartupTypes': [HOOK_TYPE]},
                                {'name': 'Hexalith.Works.IntegrationTests', 'loaded': True, 'hostingStartupTypes': []}],
                   'platformRuntimeEnabled': True, 'platformTelemetryRegisteredBeforeProof': True, 'telemetryPublisherRegistrations': 1}
        return assembly, {'assembly': str(assembly), 'sha256': sha256(assembly), 'directory': str(root), 'schedulerVolume': 'works-sdk416-owned',
                          'role': role, 'actorType': 'WorksSdkReminderActor', 'readinessPassed': True, 'worksReadinessExpectation': 'Healthy',
                          'sdkAssemblies': sdk,
                          'runtimeModules': [{'resource': name, 'configuration': 'Release', 'entry': str(assembly), 'entrySha256': sha256(assembly),
                                              'sdkAssemblies': sdk} for name in names],
                          'hostingStartups': {name: copy.deepcopy(hosting) for name in ('works', 'works-ha') if name in names}}

    def verify(self, root, assembly, *records):
        path = root / 'starts.jsonl'
        path.write_text(''.join(json.dumps(record) + '\n' for record in records))
        return verify_host_records(path, assembly, root, VERSION)

    def test_another_host_or_scheduler_cannot_qualify_platform(self):
        root = self.directory()
        assembly, record = self.host_record(root)
        _, ha = self.host_record(root, ha=True)
        self.assertEqual(len(self.verify(root, assembly, record, ha)), 2)
        for key, value in [('sha256', 'changed'), ('assembly', str(root / 'old-host.dll')), ('directory', str(root / 'works')), ('schedulerVolume', 'hexalith-works-dapr-scheduler'), ('role', 'unknown'), ('actorType', 'DateReminderActor'), ('readinessPassed', False), ('runtimeModules', []), ('sdkAssemblies', []), ('sdkAssemblies', [{'name': 'Hexalith.EventStore.Aspire', 'version': '3.124.0.0', 'path': str(assembly), 'sha256': sha256(assembly)}]), ('sdkAssemblies', [{'name': 'Hexalith.EventStore.Aspire', 'version': '3.125.2.0', 'path': str(assembly), 'sha256': sha256(assembly)}])]:
            with self.subTest(key=key), self.assertRaises(ValueError):
                self.verify(root, assembly, record | {key: value}, ha)

    def test_ha_start_must_bind_the_independent_replica_module(self):
        root = self.directory()
        assembly, record = self.host_record(root)
        with self.assertRaisesRegex(ValueError, 'works-ha'):
            self.verify(root, assembly, record)
        _, ha = self.host_record(root, ha=True)
        del ha['hostingStartups']['works-ha']
        with self.assertRaisesRegex(ValueError, 'works-ha'):
            self.verify(root, assembly, record, ha)

    def test_platform_start_must_load_platform_hosting_startup_before_the_proof(self):
        root = self.directory()
        assembly, record = self.host_record(root, ha=True)
        self.assertEqual([item['resource'] for item in self.verify(root, assembly, record)[0]['platformHosting']], ['works', 'works-ha'])
        mutations = {
            'absent record': lambda hosting: hosting.clear(),
            'proof only': lambda hosting: hosting.update(hostingStartupAssemblies=['Hexalith.Works.IntegrationTests']),
            'proof first': lambda hosting: hosting.update(hostingStartupAssemblies=['Hexalith.Works.IntegrationTests', 'Hexalith.Platform.Works.Runtime']),
            'replaced hook': lambda hosting: hosting.update(startupHooks=[hosting['startupHooks'][1]]),
            'not loaded': lambda hosting: hosting['startups'][0].update(loaded=False),
            'no hosting type': lambda hosting: hosting['startups'][0].update(hostingStartupTypes=[]),
            'unbound artifact': lambda hosting: hosting['startups'][0].update(sha256='0' * 64),
            'runtime gate off': lambda hosting: hosting.update(platformRuntimeEnabled=False),
            'did not run first': lambda hosting: hosting.update(platformTelemetryRegisteredBeforeProof=False),
            'duplicate telemetry': lambda hosting: hosting.update(telemetryPublisherRegistrations=2),
        }
        for name, mutate in mutations.items():
            for resource in ['works', 'works-ha']:
                changed = copy.deepcopy(record)
                mutate(changed['hostingStartups'][resource])
                with self.subTest(name=name, resource=resource), self.assertRaisesRegex(ValueError, resource):
                    self.verify(root, assembly, changed)

    def test_degraded_expected_start_records_unpassed_readiness_only(self):
        root = self.directory()
        assembly, record = self.host_record(root, ha=True)
        degraded = record | {'readinessPassed': False, 'worksReadinessExpectation': 'Degraded'}
        self.assertEqual(len(self.verify(root, assembly, degraded)), 1)
        for changed in [degraded | {'readinessPassed': True}, record | {'readinessPassed': False},
                        record | {'worksReadinessExpectation': None}, degraded | {'role': 'retained-rollback'}]:
            with self.subTest(changed={key: changed.get(key) for key in ('readinessPassed', 'worksReadinessExpectation', 'role')}), self.assertRaises(ValueError):
                self.verify(root, assembly, changed)

    def test_retained_rollback_must_bind_the_retained_works_host(self):
        root = self.directory()
        assembly, record = self.host_record(root, ha=True)
        retained = root / 'runtime/bin/Hexalith.Works.AppHost/release/Hexalith.Works.AppHost.dll'
        retained.parent.mkdir(parents=True)
        retained.write_bytes(b'retained host')
        rollback = record | {'role': 'retained-rollback', 'assembly': str(retained), 'sha256': sha256(retained),
                             'directory': str(root / 'works'), 'hostingStartups': {}}
        self.assertEqual(len(self.verify(root, assembly, record, rollback)), 2)
        other = root / 'runtime/bin/Other.AppHost/release/Other.AppHost.dll'
        other.parent.mkdir(parents=True)
        other.write_bytes(b'other host')
        debug = root / 'runtime/bin/Hexalith.Works.AppHost/debug/Hexalith.Works.AppHost.dll'
        debug.parent.mkdir(parents=True)
        debug.write_bytes(b'debug host')
        for name, changed in [('platform host relabeled', rollback | {'assembly': str(assembly), 'sha256': sha256(assembly)}),
                              ('foreign host', rollback | {'assembly': str(other), 'sha256': sha256(other)}),
                              ('debug host', rollback | {'assembly': str(debug), 'sha256': sha256(debug)}),
                              ('changed binary', rollback | {'sha256': '0' * 64}),
                              ('shared scheduler', rollback | {'schedulerVolume': 'hexalith-works-dapr-scheduler'})]:
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.verify(root, assembly, record, changed)

    def test_mixed_or_missing_fleet_configuration_cannot_qualify(self):
        root = self.directory()
        release, record = self.host_record(root, ha=True)
        debug = root / 'bin/debug/apphost.dll'
        debug.parent.mkdir(parents=True)
        debug.write_bytes(b'valid independently hashed artifact')
        modules = record['runtimeModules']
        for configuration in [None, 'Debug', 'Custom']:
            changed = [modules[0] | {'configuration': configuration}, *modules[1:]]
            with self.subTest(configuration=configuration), self.assertRaisesRegex(ValueError, 'Release configuration'):
                self.verify(root, release, record | {'runtimeModules': changed})
        changed = [modules[0] | {'entry': str(debug), 'entrySha256': sha256(debug)}, *modules[1:]]
        with self.assertRaisesRegex(ValueError, 'Release configuration'):
            self.verify(root, release, record | {'runtimeModules': changed})
        with self.assertRaisesRegex(ValueError, 'Release deployment'):
            self.verify(root, debug, record | {'assembly': str(debug), 'sha256': sha256(debug)})

    def test_live_module_from_another_sdk_version_cannot_qualify(self):
        root = self.directory()
        assembly, record = self.host_record(root, ha=True)
        record['runtimeModules'][0]['sdkAssemblies'] = [record['sdkAssemblies'][0] | {'version': '3.124.0.0'}]
        with self.assertRaisesRegex(ValueError, 'unpublished runtime SDK assembly'):
            self.verify(root, assembly, record)

    def outcomes(self, states, extra=()):
        path = self.directory() / 'states.jsonl'
        path.write_text(''.join(json.dumps({'phase': phase, 'state': state}) + '\n' for phase, state in [*states.items(), *extra]))
        return path

    def test_valid_outcomes_derive_every_rows_four_proofs_from_named_phases(self):
        rows = {key: {} for key in ROWS}
        result = bind_persisted_outcomes(self.outcomes(valid_states()), rows, [{'role': 'retained-rollback'}])
        self.assertTrue(result['complete'])
        self.assertEqual(len(result['crossChecks']), len(CROSS_CHECKS))
        for key, row in rows.items():
            self.assertEqual(row['status'], 'passed')
            self.assertEqual(set(row['requiredProofs']), set(PROOF_NAMES))
            for name, proof in row['requiredProofs'].items():
                self.assertTrue(proof['passed'])
                self.assertEqual([item['phase'] for item in proof['evidence']], [phase for phase, _ in PROOFS[key][name]])
                self.assertTrue(all(item['fields'] for item in proof['evidence']))

    def test_every_mapped_proof_field_is_load_bearing(self):
        for key, proofs in PROOFS.items():
            for name, requirements in proofs.items():
                for phase, fields in requirements:
                    for field, (predicate, _) in fields.items():
                        states = valid_states()
                        with self.subTest(row=key, proof=name, phase=phase, field=field):
                            if not field:
                                # A whole-state predicate accepts the qualifying state and refuses an empty one.
                                self.assertTrue(predicate(states[phase]))
                                self.assertFalse(predicate({} if isinstance(states[phase], dict) else []))
                                continue
                            *parents, leaf = field.split('.')
                            container = states[phase]
                            for part in parents:
                                container = container[int(part)] if isinstance(container, list) else container[part]
                            del container[leaf]
                            with self.assertRaisesRegex(ValueError, 'lacks required field|Cross-phase binding failed'):
                                bind_persisted_outcomes(self.outcomes(states), {key: {}}, [{'role': 'retained-rollback'}])

    def test_non_qualifying_values_are_refused(self):
        cases = [
            ('platform-two-host-ha-lost-firing-stale-callback-recovery', ('trust', 'foreignCaClientRefusedByServer'), False, 'R10'),
            ('platform-two-host-ha-lost-firing-stale-callback-recovery', ('trust', 'serverIdentity'), 'spiffe://public/ns/default/eventstore', 'R10'),
            ('platform-sdk-named-mode-legacy-route-refused', ('legacyRouteStatus',), 200, 'R9'),
            ('platform-sdk-named-mode-legacy-route-refused', ('unchangedFamilySha256',), 'b' * 64, 'R9'),
            ('platform-executor-filtering-and-origin-tenant-publish-refusals', ('hidden', 'found'), True, 'R5'),
            ('platform-focused-projection-rebuild', ('concurrentDelivery', 'automaticCatchUpAfterCommit'), False, 'R4'),
            ('platform-initial-projection-convergence', ('heads', 'source-3', 'observed', 'Position'), 2, 'R3'),
            ('periodic-writers-disabled-readiness-degraded', ('status',), 'Healthy', 'R2'),
            ('platform-executor-filtering-and-origin-tenant-publish-refusals', ('wrongPort',), 200, 'R1'),
            ('protected-scoped-disposition-audit-before-mutation', ('afterGeneration',), 0, 'R8'),
            ('paged-process-restart-and-shared-store-owner-fencing', ('staleOwnerWriteRefused',), 'true', 'R7'),
            (RESTORE_PHASE, ('dateResumed',), 2, 'R6'),
            (RESTORE_PHASE, ('convergence', 'receipt', 'Replayed'), False, 'R11'),
        ]
        for phase, path, value, row in cases:
            states = valid_states()
            container = states[phase]
            for part in path[:-1]:
                container = container[part]
            container[path[-1]] = value
            with self.subTest(phase=phase, path=path), self.assertRaisesRegex(ValueError, row + ' .*does not qualify'):
                bind_persisted_outcomes(self.outcomes(states), {row: {}}, [{'role': 'retained-rollback'}])
        states = valid_states()
        states['five-child-persisted-target-receipts'] = states['five-child-persisted-target-receipts'][:4]
        with self.assertRaisesRegex(ValueError, 'R11 persisted'):
            bind_persisted_outcomes(self.outcomes(states), {key: {} for key in ROWS}, [{'role': 'retained-rollback'}])

    def test_duplicate_missing_or_unbound_phases_cannot_accept_rows(self):
        states = valid_states()
        with self.assertRaisesRegex(ValueError, 'Duplicate persisted outcome phase'):
            bind_persisted_outcomes(self.outcomes(states, [('platform-initial-projection-convergence', states['platform-initial-projection-convergence'])]),
                                    {}, [{'role': 'retained-rollback'}])
        for phase in sorted(required_phases()):
            missing = valid_states()
            del missing[phase]
            with self.subTest(phase=phase), self.assertRaisesRegex(ValueError, 'did not execute: ' + phase):
                bind_persisted_outcomes(self.outcomes(missing), {}, [{'role': 'retained-rollback'}])
        with self.assertRaisesRegex(ValueError, 'retained-host restore start'):
            bind_persisted_outcomes(self.outcomes(valid_states()), {}, [{'role': 'platform-target'}])
        malformed = self.directory() / 'states.jsonl'
        malformed.write_text(json.dumps({'phase': 'no-state'}) + '\n')
        with self.assertRaisesRegex(ValueError, 'no phase or state'):
            bind_persisted_outcomes(malformed, {}, [{'role': 'retained-rollback'}])

    def test_restore_and_restart_must_bind_the_captured_platform_state(self):
        for phase, path, value, message in [
            (RESTORE_PHASE, ('fleet', 'redis-key-count'), '681', 'restored fleet equals the complete snapshot'),
            (RESTORE_PHASE, ('runtimeHashes', '/runtime/Hexalith.Works.dll'), 'd' * 64, 'restored runtime binaries'),
            (RESTORE_PHASE, ('convergence', 'receipt', 'EffectId'), 'OTHER', 'Platform-written receipt'),
            ('platform-historical-restart-replay-and-family', ('familyDigest',), 'e' * 64, 'persisted historical family'),
        ]:
            states = valid_states()
            container = states[phase]
            for part in path[:-1]:
                container = container[part]
            container[path[-1]] = value
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                bind_persisted_outcomes(self.outcomes(states), {key: {} for key in ROWS}, [{'role': 'retained-rollback'}])

    def test_focused_projection_rebuild_is_a_required_r4_outcome(self):
        self.assertIn('platform-focused-projection-rebuild', [phase for phase, _ in PROOFS['R4']['persisted']])
        states = valid_states()
        del states['platform-focused-projection-rebuild']
        with self.assertRaisesRegex(ValueError, 'platform-focused-projection-rebuild'):
            bind_persisted_outcomes(self.outcomes(states), {key: {} for key in ROWS}, [{'role': 'retained-rollback'}])

    def test_every_row_maps_all_four_proofs_to_real_phases(self):
        self.assertEqual(set(PROOFS), set(ROWS))
        for key, proofs in PROOFS.items():
            self.assertEqual(set(proofs), set(PROOF_NAMES), key)
            for name, requirements in proofs.items():
                self.assertTrue(requirements, key + ' ' + name)
                self.assertTrue(all(fields for _, fields in requirements), key + ' ' + name)
        self.assertNotEqual(PROOFS['R9']['negative'][0][0], PROOFS['R9']['persisted'][0][0])

    def catalog(self, body, name='Directory.Packages.props', root=None):
        root = root or self.directory()
        path = root / name
        path.write_text('<Project><PropertyGroup>' + body + '</PropertyGroup></Project>')
        return path

    def test_catalog_selects_exactly_one_stable_public_sdk_version(self):
        selected = self.catalog("<HexalithEventStoreVersion Condition=\"'$(HexalithEventStoreVersion)' == ''\">3.125.1</HexalithEventStoreVersion>")
        self.assertEqual(selected_sdk_version(selected), VERSION)
        for body in ['', '<HexalithEventStoreVersion>3.125.1-story416.1</HexalithEventStoreVersion>',
                     '<HexalithEventStoreVersion>3.125.1</HexalithEventStoreVersion><HexalithEventStoreVersion>3.125.0</HexalithEventStoreVersion>',
                     '<HexalithEventStoreVersion>$(Other)</HexalithEventStoreVersion>']:
            with self.subTest(body=body), self.assertRaises(ValueError):
                selected_sdk_version(self.catalog(body))

    def test_works_consumer_default_pin_must_name_the_qualified_producer(self):
        pinned = self.catalog('<HexalithEventStoreVersion>3.125.1</HexalithEventStoreVersion>')
        self.assertEqual(require_consumer_pin(pinned, VERSION)['sha256'], sha256(pinned))
        for body in ['<HexalithEventStoreVersion>3.124.0</HexalithEventStoreVersion>', '']:
            with self.subTest(body=body), self.assertRaisesRegex(ValueError, 'differs from the Platform catalog'):
                require_consumer_pin(self.catalog(body), VERSION)

    def test_both_works_props_files_must_name_the_qualified_producer(self):
        pin = '<HexalithEventStoreVersion>3.125.1</HexalithEventStoreVersion>'
        root = self.directory()
        self.catalog(pin, 'Directory.Build.props', root)
        self.catalog(pin, 'Directory.Packages.props', root)
        self.assertEqual(set(require_consumer_pins(root, VERSION)), {'Directory.Build.props', 'Directory.Packages.props'})
        for name in ['Directory.Build.props', 'Directory.Packages.props']:
            changed = self.directory()
            for other in ['Directory.Build.props', 'Directory.Packages.props']:
                self.catalog('<HexalithEventStoreVersion>3.124.0</HexalithEventStoreVersion>' if other == name else pin, other, changed)
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, name):
                require_consumer_pins(changed, VERSION)

    def test_file_host_eventstore_packages_must_match_the_catalog(self):
        def host(*lines):
            path = self.directory() / 'apphost.cs'
            path.write_text('\n'.join(['#!/usr/bin/env dotnet', '#:sdk Aspire.AppHost.Sdk@13.6.1', '#:package Aspire.Hosting.Redis@13.6.1', *lines, 'var x = 1;']) + '\n')
            return path
        pinned = host('#:package Hexalith.EventStore.Aspire@3.125.1', '#:package Hexalith.EventStore.ServiceDefaults@3.125.1')
        self.assertEqual(require_host_package_pins(pinned, VERSION)['packages'], ['Hexalith.EventStore.Aspire', 'Hexalith.EventStore.ServiceDefaults'])
        for lines in [('#:package Hexalith.EventStore.Aspire@3.125.1', '#:package Hexalith.EventStore.ServiceDefaults@3.124.0'),
                      ('#:package Hexalith.EventStore.Aspire@3.125.2',), (),
                      ('#:package Hexalith.EventStore.Aspire@3.125.1', '#:package Hexalith.EventStore.Aspire@3.125.1')]:
            with self.subTest(lines=lines), self.assertRaises(ValueError):
                require_host_package_pins(host(*lines), VERSION)

    def test_contract_requires_every_row(self):
        self.assertEqual(set(ROWS), {f'R{index}' for index in range(1, 12)})


if __name__ == '__main__':
    unittest.main()
