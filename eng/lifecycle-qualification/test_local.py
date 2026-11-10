"""Resource-free local-controller controls; no lifecycle or candidate credit."""
from datetime import datetime, timedelta, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch
import zipfile

SPEC = importlib.util.spec_from_file_location('local_lifecycle', Path(__file__).with_name('local.py'))
Q = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(Q)


class LocalControls(unittest.TestCase):
    def owner(self):
        return {'runId': 'a' * 32, 'environmentId': 'b' * 32}

    def attachment(self):
        return {'originalAttachmentRequestId': 'c' * 32, 'ownerRunId': 'a' * 32,
                'environmentId': 'b' * 32, 'attachedRunId': 'd' * 32,
                'requestedHoldSeconds': 30, 'effectiveHoldSeconds': 30,
                'acceptedAtUtc': '2030-01-01T12:00:00Z', 'expiresAtUtc': '2030-01-01T12:00:30Z',
                'outcome': None, 'endReason': None, 'endedAtUtc': None}

    def test_original_startup_clock_cannot_reset_or_change_to_an_override(self):
        value = {'createdAt': '2030-01-01T12:00:00Z', 'startupDeadlineUtc': '2030-01-01T12:10:00Z',
                 'effectiveDeadlineSeconds': 600, 'effectiveDeadlineIsOverride': False}
        original = Q.deadline(value)
        for change in ({'createdAt': '2030-01-01T12:00:01Z'},
                       {'startupDeadlineUtc': '2030-01-01T12:10:01Z'},
                       {'effectiveDeadlineSeconds': True}, {'effectiveDeadlineIsOverride': True},
                       {'effectiveDeadlineSeconds': 601}):
            with self.subTest(change=change), self.assertRaises(Q.Refusal):
                Q.deadline({**value, **change}, original)

    def test_retry_cannot_renew_expiry_or_change_lineage(self):
        original = Q.hold(self.attachment(), self.owner())
        for change in ({'originalAttachmentRequestId': 'e' * 32},
                       {'acceptedAtUtc': '2030-01-01T12:00:01Z', 'expiresAtUtc': '2030-01-01T12:00:31Z'},
                       {'effectiveHoldSeconds': True}, {'ownerRunId': 'f' * 32}):
            with self.subTest(change=change), self.assertRaises(Q.Refusal):
                Q.hold({**self.attachment(), **change}, self.owner(), original)

    def test_expiry_is_exclusive_and_terminal_end_cannot_cross_it(self):
        original = self.attachment()
        at_expiry = {**original, 'endedAtUtc': original['expiresAtUtc'], 'outcome': 'passed', 'endReason': 'terminal-outcome'}
        with self.assertRaises(Q.Refusal):
            Q.hold(at_expiry, self.owner())
        expired = {**at_expiry, 'outcome': 'withdrawn', 'endReason': 'expired'}
        self.assertEqual(Q.hold(expired, self.owner())['outcome'], 'withdrawn')
        with self.assertRaises(Q.Refusal):
            Q.hold({**expired, 'endedAtUtc': original['acceptedAtUtc']}, self.owner())

    def test_first_failure_is_immutable_and_private_fields_never_escape(self):
        with tempfile.TemporaryDirectory() as temporary:
            value = object.__new__(Q.Campaign)
            value.states = Path(temporary)
            value.checkout = Path(temporary)
            value.prepared_mapping = {'Root': temporary, 'Mode': 'package', 'ContentHash': '1' * 64}
            value.journal = {'owners': [{'label': 'failure'}]}
            value.save = Mock()
            item = {'label': 'failure', 'owner': self.owner()}
            raw = {'runId': 'a' * 32, 'ownerId': 'a' * 32, 'terminalStatus': 'failed',
                   'terminalAtUtc': '2030-01-01T12:00:00Z', 'terminalCause': 'test-failure',
                   'sourceMappingHash': None, 'failureRetained': True,
                   'terminalOutcome': {'exitCode': 'ProductOrTest', 'phase': 'Test', 'category': 'ProductOrTest',
                                       'ruleId': 'HXT007', 'message': 'PRIVATE_CREDENTIAL'},
                   'environmentBindings': {'secret': 'PRIVATE_CREDENTIAL'}}
            path = value.diagnostics(item) / 'selected-test.json'
            Q.write(path, raw)
            Q.write(path.with_name('selected-source-mapping.json'), value.prepared_mapping)
            first = value.outcome(item)
            self.assertNotIn('PRIVATE_CREDENTIAL', json.dumps(first))
            self.assertEqual(first['terminalOutcome']['ruleId'], 'HXT007')
            Q.write(path, {**raw, 'terminalOutcome': {**raw['terminalOutcome'], 'ruleId': 'HXT009'}})
            with self.assertRaises(Q.Refusal) as caught:
                value.outcome(item)
            self.assertEqual(caught.exception.code, 'HXLQ205')
            Q.write(path, {**raw, 'terminalStatus': 'cancelled', 'terminalCause': 'run-cancelled',
                          'terminalOutcome': {'exitCode': 'Cancelled', 'phase': 'Cleanup', 'category': 'Cancelled', 'ruleId': 'HXC130'}})
            with self.assertRaises(Q.Refusal) as caught:
                value.outcome(item)
            self.assertEqual(caught.exception.code, 'HXLQ205')
            self.assertEqual(value.public(item)['firstOutcome'], first)

    def completed(self):
        return {'runId': 'a' * 32, 'ownerId': 'a' * 32, 'terminalStatus': 'completed',
                'terminalAtUtc': '2030-01-01T12:00:00Z', 'terminalCause': 'tests-completed',
                'sourceMappingHash': '1' * 64,
                'terminalOutcome': {'exitCode': 'Success', 'phase': 'None', 'category': 'None',
                                    'ruleId': None, 'message': 'PRIVATE_TERMINAL_PAYLOAD'}}

    def test_internal_completed_success_matches_public_passed_without_messages(self):
        first = Q.first_outcome(self.completed(), self.owner())
        self.assertEqual(first['terminalStatus'], 'completed')
        self.assertEqual(first['terminalOutcome'], {'exitCode': 'Success', 'phase': 'None', 'category': 'None', 'ruleId': None})
        self.assertNotIn('PRIVATE_TERMINAL_PAYLOAD', json.dumps(first))
        terminal = {'outcome': 'passed', 'cause': 'tests-completed', 'recordedAt': '2030-01-01T12:00:00+00:00'}
        Q.public_terminal({'terminal': terminal}, first)
        for changed in ({'outcome': 'failed'}, {'cause': 'test-failure'}, {'recordedAt': '2030-01-01T12:00:01Z'}):
            with self.subTest(changed=changed), self.assertRaises(Q.Refusal):
                Q.public_terminal({'terminal': {**terminal, **changed}}, first)

    def test_contradictory_internal_status_exit_or_cause_refuses(self):
        for status, exit_code in (('failed', 'Success'), ('cancelled', 'Success'), ('passed', 'Success'),
                                  ('completed', 'ProductOrTest'), ('completed', 'Cancelled'), ('failed', 'Cancelled')):
            raw = self.completed()
            raw.update(terminalStatus=status)
            raw['terminalOutcome']['exitCode'] = exit_code
            with self.subTest(status=status, exit_code=exit_code), self.assertRaises(Q.Refusal):
                Q.first_outcome(raw, self.owner())
        with self.assertRaises(Q.Refusal):
            Q.first_outcome({**self.completed(), 'terminalCause': 'run-cancelled'}, self.owner())

    def test_process_discovery_excludes_only_proven_preexisting_protected_processes(self):
        class Proc:
            def __init__(self, pid, birth, state='S'):
                self.name = str(pid)
                self.environment = Mock()
                self.environment.read_bytes.side_effect = PermissionError('PRIVATE_PROCESS_PAYLOAD')
                self.status = Mock()
                self.status.read_text.return_value = f'{pid} (fixture) {state} ' + '0 ' * 18 + str(birth)

            def stat(self):
                return type('Uid', (), {'st_uid': os.geteuid()})()

            def __truediv__(self, name):
                return self.status if name == 'stat' else self.environment

        value = object.__new__(Q.Campaign)
        records = {}
        value.public = lambda item: records
        value.observe = Mock()
        item = {'identity': {'birth': '200'}, 'owner': self.owner()}
        old, newer = Proc(12345, 100), Proc(12346, 201)
        zombie, exited = Proc(12347, 202, 'Z'), Proc(12348, 203, 'X')
        with patch.object(Q.Path, 'iterdir', return_value=iter([old, zombie, exited])):
            self.assertEqual(value.owned_processes(item), [])
        old.environment.read_bytes.assert_not_called()
        zombie.environment.read_bytes.assert_not_called()
        exited.environment.read_bytes.assert_not_called()
        self.assertTrue(records['processDiscovery']['completeWithinBoundary'])
        self.assertEqual(records['processDiscovery']['excludedPreexistingProcesses'], 1)
        self.assertEqual(records['processDiscovery']['excludedExitedProcesses'], 2)
        with patch.object(Q.Path, 'iterdir', return_value=iter([old, newer])):
            with self.assertRaises(PermissionError):
                value.owned_processes(item)
        old.environment.read_bytes.assert_not_called()
        newer.environment.read_bytes.assert_called_once()
        self.assertFalse(records['processDiscovery']['completeWithinBoundary'])
        self.assertEqual(records['processDiscovery']['checkedSinceOwnerStart'], 1)
        self.assertEqual(records['processDiscovery']['refusal'],
                         {'pid': 12346, 'birth': '201', 'parentPid': 0, 'session': 0, 'state': 'S', 'readStage': 'environ'})
        self.assertEqual(value.observe.call_args.kwargs['readStage'], 'environ')
        self.assertNotIn('PRIVATE_PROCESS_PAYLOAD', json.dumps(value.observe.call_args.kwargs))

    def expiry_fixture(self, root):
        value = object.__new__(Q.Campaign)
        value.states = root / 'states'
        value.observe = Mock()
        owner = {**self.owner(), 'attemptId': 'e' * 32, 'createdAt': '2030-01-01T11:59:00Z'}
        item = {'owner': owner, 'label': 'expiry', 'workspace': str(root)}
        original = self.attachment()
        failed = {**original, 'attachedRunId': 'f' * 32, 'originalAttachmentRequestId': '1' * 32,
                  'outcome': 'failed', 'endReason': 'terminal-outcome', 'endedAtUtc': '2030-01-01T12:00:01Z'}
        expired = {**original, 'outcome': 'withdrawn', 'endReason': 'expired', 'endedAtUtc': original['expiresAtUtc']}
        entry = {'ownerRunId': owner['runId'], 'environmentId': owner['environmentId'],
                 'attemptId': owner['attemptId'], 'createdAt': owner['createdAt'],
                 'attachments': [failed, expired], 'retention': 'failure-retained',
                 'failureRunIds': [failed['attachedRunId']], 'attachedRunIds': []}
        value.invoke = Mock(return_value=(0, {'environments': [entry]}))
        path = value.diagnostics(item) / 'attachments.json'
        document = {'schema': 'hexalith.g4-run-attachments.v1', 'ownerRunId': owner['runId'],
                    'attachments': [failed, original], 'private': 'PRIVATE_STORE_PAYLOAD'}
        Q.write(path, document)
        return value, item, original, failed, expired, entry, path, document

    def test_public_expiry_preserves_raw_store_and_checks_owner_lineage_and_deadline(self):
        with tempfile.TemporaryDirectory() as temporary:
            value, item, original, failed, expired, entry, path, _ = self.expiry_fixture(Path(temporary))
            digest = Q.sha(path)
            self.assertEqual(value.public_expiry(item, original, failed), expired)
            self.assertEqual(Q.sha(path), digest)
            self.assertIsNone(Q.read(path)['attachments'][1]['outcome'])
            self.assertNotIn('PRIVATE_STORE_PAYLOAD', str(value.observe.call_args_list))
            for changed in ({'environmentId': '2' * 32}, {'attemptId': '2' * 32},
                            {'attachments': [{**expired, 'expiresAtUtc': '2030-01-01T12:00:31Z'}]},
                            {'attachments': [{**expired, 'endedAtUtc': '2030-01-01T12:00:31Z'}]},
                            {'failureRunIds': []}, {'attachedRunIds': [original['attachedRunId']]}):
                with self.subTest(changed=changed), self.assertRaises(Q.Refusal):
                    value.invoke.return_value = (0, {'environments': [{**entry, **changed}]})
                    value.public_expiry(item, original, failed)

    def test_listing_cannot_mutate_raw_document_even_when_projected_hold_agrees(self):
        with tempfile.TemporaryDirectory() as temporary:
            value, item, original, failed, _, entry, path, document = self.expiry_fixture(Path(temporary))
            def changed(_):
                Q.write(path, {**document, 'private': 'CHANGED_PRIVATE_STORE_PAYLOAD'})
                return 0, {'environments': [entry]}
            value.invoke.side_effect = changed
            with self.assertRaises(Q.Refusal) as caught:
                value.public_expiry(item, original, failed)
            self.assertEqual(caught.exception.code, 'HXLQ205')
            self.assertEqual(value.observe.call_count, 2)

    def test_expiry_case_requires_durable_expiry_after_cleanup_and_stays_incomplete(self):
        for durable_expiry in (False, True):
            with self.subTest(durable_expiry=durable_expiry), tempfile.TemporaryDirectory() as temporary:
                value, item, original, failed, expired, _, path, document = self.expiry_fixture(Path(temporary))
                value.start = Mock(return_value=item)
                value.attachment = Mock(side_effect=[failed, original, None])
                value.end_hold = Mock()
                value.finish = Mock()
                value.children = {'expiry': Mock()}
                value.containers = Mock(return_value=['controlled-container'])
                def cleaned(_):
                    Q.write(path, {**document, 'attachments': [failed, expired if durable_expiry else original]})
                value.down = Mock(side_effect=cleaned)
                with patch.object(Q, 'datetime', wraps=datetime) as clock:
                    clock.now.return_value = datetime(2030, 1, 1, 12, 1, tzinfo=timezone.utc)
                    if durable_expiry:
                        self.assertIn('attached-work-access-producer-unavailable', value.case_attached_failure_expiry())
                    else:
                        with self.assertRaises(Q.Refusal):
                            value.case_attached_failure_expiry()
                value.down.assert_called_once_with(item)

    def test_negative_retry_retains_bounded_receipt_before_rejecting_contradictions(self):
        value = object.__new__(Q.Campaign)
        value.manifest_hash = '1' * 64
        value.observe = Mock()
        diagnostic = {'ruleId': 'HXR036', 'field': 'attachment-expired',
                      'message': 'PRIVATE_REPLY_PAYLOAD', 'hint': 'PRIVATE_REPLY_PAYLOAD'}
        value.invoke = Mock(return_value=(22, {'diagnostics': [diagnostic]}))
        item = {'owner': self.owner()}
        value.attachment(item, previous=self.attachment(), expected_code='HXR036')
        receipt = value.observe.call_args.kwargs
        self.assertEqual(receipt['ruleIds'], ['HXR036'])
        self.assertEqual(receipt['knownReasons'], ['attachment-expired'])
        self.assertNotIn('PRIVATE_REPLY_PAYLOAD', json.dumps(receipt))
        value.invoke.return_value = (0, {'diagnostics': [diagnostic]})
        with self.assertRaises(Q.Refusal):
            value.attachment(item, previous=self.attachment(), expected_code='HXR036')
        self.assertEqual(value.observe.call_args.kwargs['exitCode'], 0)

    def test_progress_prints_only_bounded_controller_event_clock_and_ordinal(self):
        value = object.__new__(Q.Campaign)
        value.journal = {'observations': []}
        value.save = Mock()
        with patch('builtins.print') as output:
            value.observe('process-read-refused', arbitrary='PRIVATE_PROGRESS_PAYLOAD')
        progress = json.loads(output.call_args.args[0])
        self.assertEqual(set(progress), {'event', 'observation', 'observedAtUtc'})
        self.assertEqual(progress['observation'], 1)
        self.assertLess(len(output.call_args.args[0]), 200)
        self.assertTrue(output.call_args.kwargs['flush'])
        self.assertNotIn('PRIVATE_PROGRESS_PAYLOAD', output.call_args.args[0])
        with self.assertRaises(Q.Refusal):
            value.observe('PRIVATE\nPROGRESS')

    def test_pidfd_signal_refuses_reused_pid_and_never_signals_a_group(self):
        identity = {'pid': 12345, 'birth': '100', 'session': 12345}
        with patch.object(Q.os, 'pidfd_open', return_value=77), patch.object(Q.os, 'close') as close, \
             patch.object(Q.signal, 'pidfd_send_signal') as send, patch.object(Q, 'process', return_value={**identity, 'birth': '101'}):
            with self.assertRaises(Q.Refusal):
                Q.signal_exact(identity, signal.SIGINT, 'a' * 32)
            send.assert_not_called()
            close.assert_called_once_with(77)
        with patch.object(Q.os, 'pidfd_open', return_value=78), patch.object(Q.os, 'close'), \
             patch.object(Q.signal, 'pidfd_send_signal') as send, patch.object(Q, 'process', return_value=identity):
            Q.signal_exact(identity, signal.SIGINT, 'a' * 32)
            send.assert_called_once_with(78, signal.SIGINT)

    def test_parent_symlink_cannot_supply_private_metadata(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            actual = Q.private(root / 'actual')
            Q.write(actual / 'state.json', {'token': 'PRIVATE_CREDENTIAL'})
            (root / 'linked').symlink_to(actual, target_is_directory=True)
            with self.assertRaises(Q.Refusal):
                Q.read(root / 'linked/state.json')
            self.assertEqual(Q.read(actual / 'state.json'), {'token': 'PRIVATE_CREDENTIAL'})

    def test_duplicate_keys_and_nonfinite_numbers_refuse(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'state.json'
            for content in ('{"runId":"one","runId":"two"}', '{"value":NaN}'):
                path.write_text(content)
                with self.assertRaises(Q.Refusal):
                    Q.read(path)

    def test_published_package_requires_exact_installed_bytes_and_no_links(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            assembly = root / 'installed/Hexalith.Builds.Module.Cli.dll'
            assembly.parent.mkdir()
            assembly.write_bytes(b'controlled published bytes, never executed')
            package = root / 'tool.nupkg'
            with zipfile.ZipFile(package, 'w') as archive:
                archive.writestr('tools/net10.0/any/' + assembly.name, assembly.read_bytes())
            self.assertIn(assembly.name, Q.verify_package(package, assembly, Q.sha(package))['files'])
            assembly.write_bytes(b'changed installed bytes')
            with self.assertRaises(Q.Refusal):
                Q.verify_package(package, assembly, Q.sha(package))
            assembly.unlink()
            foreign = root / 'foreign.dll'
            foreign.write_bytes(b'controlled published bytes, never executed')
            assembly.symlink_to(foreign)
            with self.assertRaises(Q.Refusal):
                Q.verify_package(package, assembly, Q.sha(package))

    def test_preflight_cannot_select_a_different_self_consistent_package_hash(self):
        value = object.__new__(Q.Campaign)
        with patch.object(Q, 'verify_package') as verify, patch.object(Q.subprocess, 'check_output') as git, \
             patch.object(Q.subprocess, 'run') as probe:
            with self.assertRaises(Q.Refusal) as caught:
                value.preflight(Path('/unused/package.nupkg'), '0' * 64)
            self.assertEqual(caught.exception.code, 'HXLQ201')
            verify.assert_not_called()
            git.assert_not_called()
            probe.assert_not_called()

    def mapping_fixture(self, root):
        checkout = Q.private(root / 'caller')
        directory = Q.private(root / 'source-mapping')
        mapping = {'Root': str(checkout), 'Mode': 'package', 'ContentHash': '1' * 64,
                   'HasGit': True, 'Entries': [{'Identity': 'Hexalith.Parties', 'Origin': 'source', 'Path': str(checkout)}]}
        environment = {'HEXALITH_SOURCE_MAPPING_HASH': '1' * 64, 'Configuration': 'Release'}
        for key, filename in Q.MAPPING_IMPORTS.items():
            path = directory / filename
            path.write_text('<Project />')
            environment[key] = str(path)
        Q.write(directory / 'mapping.json', mapping)
        return checkout, directory, mapping, environment

    def test_prepared_mapping_binds_actual_document_and_every_import(self):
        with tempfile.TemporaryDirectory() as temporary:
            checkout, directory, mapping, environment = self.mapping_fixture(Path(temporary))
            actual, proof = Q.prepared_mapping(environment, checkout)
            self.assertEqual(actual, mapping)
            self.assertEqual(proof['documentSha256'], Q.sha(directory / 'mapping.json'))
            self.assertEqual(proof['ContentHash'], environment['HEXALITH_SOURCE_MAPPING_HASH'])
            foreign = Path(temporary) / 'foreign.props'
            foreign.write_text('<Project />')
            with self.assertRaises(Q.Refusal):
                Q.prepared_mapping({**environment, 'CustomAfterDirectoryBuildTargets': str(foreign)}, checkout)

    def test_wrong_mapping_root_mode_hash_or_context_refuses(self):
        with tempfile.TemporaryDirectory() as temporary:
            checkout, directory, mapping, environment = self.mapping_fixture(Path(temporary))
            for changed in ({'Root': '/PRIVATE_MAPPING_PAYLOAD'}, {'Mode': 'source'},
                            {'ContentHash': '2' * 64}, {'HasGit': False}, {'Entries': []}):
                with self.subTest(changed=changed):
                    Q.write(directory / 'mapping.json', {**mapping, **changed})
                    with self.assertRaises(Q.Refusal) as caught:
                        Q.prepared_mapping(environment, checkout)
                    self.assertNotIn('PRIVATE_MAPPING_PAYLOAD', json.dumps(Q.failure(caught.exception)))
            with self.assertRaises(Q.Refusal):
                Q.prepared_mapping(None, checkout)

    def test_terminal_hash_and_complete_durable_mapping_must_match_original(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            checkout, _, mapping, _ = self.mapping_fixture(root)
            value = object.__new__(Q.Campaign)
            value.checkout, value.states, value.prepared_mapping = checkout, root / 'state', mapping
            value.journal = {'owners': [{'label': 'owner'}]}
            item = {'label': 'owner', 'owner': self.owner()}
            first = {'sourceMappingHash': mapping['ContentHash']}
            self.assertFalse(value.terminal_mapping(item, first, required=False))
            with self.assertRaises(Q.Refusal):
                value.terminal_mapping(item, first, required=True)
            archive = value.diagnostics(item) / 'selected-source-mapping.json'
            Q.write(archive, mapping)
            self.assertTrue(value.terminal_mapping(item, first, required=True))
            with self.assertRaises(Q.Refusal):
                value.terminal_mapping(item, {'sourceMappingHash': '2' * 64}, required=True)
            for changed in ({'Root': '/PRIVATE_MAPPING_PAYLOAD'}, {'Mode': 'source'},
                            {'Entries': []}, {'ContentHash': '2' * 64}):
                Q.write(archive, {**mapping, **changed})
                with self.assertRaises(Q.Refusal):
                    value.terminal_mapping(item, first, required=True)

    def test_execution_marker_checks_context_and_bounds_before_projecting(self):
        created = datetime.now(timezone.utc) - timedelta(minutes=1)
        owner = {**self.owner(), 'createdAt': created.isoformat()}
        marker = {'runId': owner['runId'], 'realPersistenceAssertionsPassed': True,
                  'observedAtUtc': (created + timedelta(seconds=1)).isoformat(), 'processId': 12345,
                  'descriptorSha256': '3' * 64, 'arbitrary': 'PRIVATE_FIXTURE_PAYLOAD'}
        projected = Q.execution_checkpoint(marker, owner)
        self.assertNotIn('PRIVATE_FIXTURE_PAYLOAD', json.dumps(projected))
        for changed in ({'runId': 'f' * 32}, {'observedAtUtc': 'PRIVATE_FIXTURE_PAYLOAD' * 1000},
                        {'observedAtUtc': marker['observedAtUtc'].replace('T', '\n')},
                        {'descriptorSha256': '3' * 65}, {'processId': True}):
            with self.subTest(changed=changed), self.assertRaises(Q.Refusal):
                Q.execution_checkpoint({**marker, **changed}, owner)

    def test_unreleased_launch_gate_exits_without_executing_the_tool(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            assembly = root / 'Hexalith.Builds.Module.Cli.dll'  # Deliberately absent: no tool may execute.
            intent = root / 'intent.json'
            Q.write(intent, {'assembly': str(assembly), 'checkout': str(root), 'command': Q.tool_arguments(assembly)})
            result = subprocess.run([sys.executable, '-B', str(Path(Q.__file__)), '--_launch', str(intent)],
                                    input=b'', capture_output=True, timeout=10, start_new_session=True)
            self.assertEqual(result.returncode, 1)
            record = Q.read(root / 'launch.json')
            self.assertFalse(record['released'])
            self.assertTrue(record['closed'])
            self.assertEqual(result.stdout, b'')

    def test_partial_publication_does_not_restart_poll_budget(self):
        value = object.__new__(Q.Campaign)
        predicate = Mock(side_effect=[json.JSONDecodeError('PRIVATE_CREDENTIAL', '', 0), False])
        with patch.object(Q.time, 'monotonic', side_effect=[0, 0.1, 0.8, 1.1]), patch.object(Q.time, 'sleep'):
            with self.assertRaises(Q.Refusal) as caught:
                value.poll(predicate, 1)
        self.assertEqual(caught.exception.code, 'HXLQ207')
        self.assertEqual(predicate.call_count, 2)
        self.assertNotIn('PRIVATE_CREDENTIAL', json.dumps(Q.failure(caught.exception)))

    def test_cleanup_attempts_every_issued_owner_after_a_failure(self):
        value = object.__new__(Q.Campaign)
        value.owners = [{'label': name, 'owner': {'runId': letter * 32}} for name, letter in [('first', 'a'), ('second', 'b')]]
        value.children = {}
        value.resume = Mock()
        value.discover = Mock()
        value.save = Mock()
        records = {item['label']: {'cleanupVerified': False} for item in value.owners}
        value.public = lambda item: records[item['label']]
        value.down = Mock(side_effect=[OSError('PRIVATE_CREDENTIAL'), None])
        self.assertFalse(value.cleanup_all())
        self.assertEqual(value.down.call_count, 2)
        self.assertNotIn('PRIVATE_CREDENTIAL', json.dumps(records))

    def test_empty_tool_inventory_cannot_hide_independent_owned_work(self):
        with tempfile.TemporaryDirectory() as temporary:
            value = object.__new__(Q.Campaign)
            item = {'owner': self.owner(), 'workspace': str(Path(temporary) / 'absent')}
            empty = {'unverified': False, **{key: [] for key in ('containerIds', 'processIds', 'workspaceIds', 'claimIds', 'discoveryFailures')}}
            value.invoke = Mock(return_value=(0, {'remainingResources': empty}))
            value.containers = Mock(return_value=[])
            value.owned_processes = Mock(return_value=[{'pid': 12345, 'birth': '100'}])
            value.settle_tool = Mock()
            value.outcome = Mock()
            value.public = Mock(return_value={})
            value.save = Mock()
            with self.assertRaises(Q.Refusal) as caught:
                value.down(item)
            self.assertEqual(caught.exception.code, 'HXLQ206')
            value.outcome.assert_not_called()

    def test_failed_newer_cleanup_audit_clears_green_and_finalization_retries(self):
        with tempfile.TemporaryDirectory() as temporary:
            value = object.__new__(Q.Campaign)
            value.states = Path(temporary) / 'states'
            item = {'label': 'retry', 'owner': self.owner(), 'workspace': str(Path(temporary) / 'absent')}
            record = {'cleanupVerified': True, 'ownedLeftovers': {'processIds': []}}
            value.public = lambda _: record
            value.save = Mock()
            empty = {'unverified': False, **{key: [] for key in ('containerIds', 'processIds', 'workspaceIds', 'claimIds', 'discoveryFailures')}}
            value.invoke = Mock(return_value=(0, {'remainingResources': empty}))
            value.containers = Mock(return_value=[])
            value.owned_processes = Mock(side_effect=[PermissionError('PRIVATE_PROCESS_PAYLOAD'), []])
            value.settle_tool = Mock()
            value.outcome = Mock()
            value.observe = Mock()
            value.resume = Mock()
            value.discover = Mock()
            value.owners, value.children = [item], {}
            with self.assertRaises(PermissionError):
                value.down(item)
            self.assertFalse(record['cleanupVerified'])
            self.assertNotIn('ownedLeftovers', record)
            self.assertTrue(value.cleanup_all())
            self.assertTrue(record['cleanupVerified'])
            self.assertEqual(value.invoke.call_count, 2)

    def cleanup_fixture(self, root, child, identity):
        value = object.__new__(Q.Campaign)
        value.states = root / 'states'
        item = {'label': 'cleanup', 'owner': self.owner(), 'identity': identity,
                'workspace': str(root / 'absent')}
        record = {'cleanupVerified': True}
        value.children = {'cleanup': child}
        value.public = lambda _: record
        value.save, value.observe, value.outcome = Mock(), Mock(), Mock()
        value.containers = Mock(return_value=[])
        value.owned_processes = Mock(return_value=[])
        empty = {'unverified': False, **{key: [] for key in ('containerIds', 'processIds', 'workspaceIds', 'claimIds', 'discoveryFailures')}}
        value.invoke = Mock(return_value=(0, {'remainingResources': empty}))
        return value, item, record

    def test_successful_down_waits_for_and_reaps_its_exact_tool_child_before_discovery(self):
        with tempfile.TemporaryDirectory() as temporary:
            child = subprocess.Popen([sys.executable, '-B', '-c', 'import sys; sys.stdin.buffer.read(1)'],
                                     stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                     start_new_session=True)
            try:
                identity = Q.process(child.pid)
                value, item, record = self.cleanup_fixture(Path(temporary), child, identity)
                reply = value.invoke.return_value
                def successful_down(_):
                    child.stdin.write(b'x')
                    child.stdin.flush()
                    return reply
                value.invoke.side_effect = successful_down
                def scan(_):
                    self.assertEqual(child.returncode, 0)
                    return []
                value.owned_processes.side_effect = scan
                with patch.object(child, 'wait', wraps=child.wait) as waited:
                    value.down(item)
                    waited.assert_called_once_with(timeout=Q.TOOL_STOP_SECONDS)
                self.assertTrue(record['cleanupVerified'])
                self.assertEqual(value.observe.call_args_list[0].args[0], 'owner-tool-reaped')
            finally:
                child.stdin.close()
                if child.poll() is None:
                    child.kill()
                child.wait(timeout=10)

    def test_unsettled_or_mismatched_tool_cannot_receive_verified_cleanup(self):
        with tempfile.TemporaryDirectory() as temporary:
            identity = {'pid': 12345, 'birth': '200', 'session': 12345}
            for pid in (12345, 54321):
                with self.subTest(pid=pid):
                    child = Mock(pid=pid)
                    child.wait.side_effect = subprocess.TimeoutExpired('controlled-child', Q.TOOL_STOP_SECONDS)
                    value, item, record = self.cleanup_fixture(Path(temporary), child, identity)
                    with self.assertRaises(subprocess.TimeoutExpired if pid == 12345 else Q.Refusal):
                        value.down(item)
                    self.assertFalse(record['cleanupVerified'])
                    value.owned_processes.assert_not_called()
                    value.outcome.assert_not_called()

    def test_expected_lock_refusals_never_wait_for_or_reap_the_running_tool(self):
        with tempfile.TemporaryDirectory() as temporary:
            child = Mock(pid=12345)
            value, item, _ = self.cleanup_fixture(Path(temporary), child,
                                                  {'pid': 12345, 'birth': '200', 'session': 12345})
            for rule in ('HXR037', 'HXR023'):
                value.invoke.return_value = (22, {'outcome': {'ruleId': rule}})
                value.down(item, refusal=rule)
            child.wait.assert_not_called()
            value.owned_processes.assert_not_called()

    def runner(self, reasons):
        value = object.__new__(Q.Campaign)
        value.owners = []
        value.journal = {'scenarios': {name: {'status': 'unexecuted'} for name in Q.SCENARIOS}, 'completedCases': []}
        value.save = Mock()
        value.cleanup_all = Mock(return_value=True)
        value.case_success_failure = Mock(return_value=reasons)
        return value

    def test_focused_pass_never_claims_a_full_campaign(self):
        value = self.runner([])
        self.assertEqual(value.run(['success-failure']), 0)
        self.assertFalse(value.journal['fullCampaignPassed'])
        self.assertTrue(value.journal['selectedPassed'])
        self.assertEqual(sum(row['status'] == 'unexecuted' for row in value.journal['scenarios'].values()), 7)

    def test_absent_attached_work_producer_keeps_required_case_incomplete(self):
        value = self.runner(['attached-work-access-producer-unavailable'])
        self.assertEqual(value.run(['success-failure']), 1)
        self.assertEqual(value.journal['scenarios']['success-failure']['status'], 'incomplete')
        self.assertFalse(value.journal['selectedPassed'])
        self.assertFalse(value.journal['fullCampaignPassed'])

    def test_missing_actual_startup_duration_never_passes(self):
        value = self.runner([])
        value.public = lambda item: {}
        value.case_success_failure.side_effect = lambda: (value.owners.append({'label': 'actual-owner'}) or [])
        self.assertEqual(value.run(['success-failure']), 1)
        self.assertIn('startup-duration-producer-unavailable', value.journal['scenarios']['success-failure']['missingProof'])

    def test_missing_original_mapping_proof_never_passes_even_with_duration(self):
        value = self.runner([])
        value.public = lambda item: {'startupDurationProof': True, 'originalMappingProof': False}
        value.case_success_failure.side_effect = lambda: (value.owners.append({'label': 'actual-owner'}) or [])
        self.assertEqual(value.run(['success-failure']), 1)
        self.assertIn('original-source-mapping-proof-unavailable', value.journal['scenarios']['success-failure']['missingProof'])


if __name__ == '__main__':
    unittest.main()
