"""Cleanup needs complete volume coverage and bounded explicit absence readback."""
from pathlib import Path
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

from evidence import canonical
from rehearse import Fixture, rehearse


def result(argv, code=0, stderr=b''):
    return subprocess.CompletedProcess(argv, code, stdout=b'', stderr=stderr)


class CleanupTests(unittest.TestCase):
    def fixture(self, directory):
        attempt = SimpleNamespace(directory=Path(directory), record=Mock())
        fixture = Fixture(SimpleNamespace(attempt_id='cleanup-test'), attempt)
        fixture.created = True
        fixture.network_created = True
        fixture.image_tags = ['hexalith-s426-base:' + fixture.cluster]
        fixture.fixture_volumes = []
        fixture.volume_capture_verified = False
        fixture.capture_volumes = Mock()
        fixture.kubeconfig.write_text('fresh synthetic credential')
        return fixture

    def absent(self, fixture, argv, **kwargs):
        self.assertGreater(kwargs['timeout'], 0)
        if argv[:2] == ['docker', 'inspect']:
            return result(argv, 1, f'Error: No such object: {argv[-1]}\n'.encode())
        if argv[:3] == ['docker', 'network', 'inspect']:
            return result(argv, 1, f'Error response from daemon: network {fixture.network} not found\n'.encode())
        if argv[:3] == ['docker', 'volume', 'inspect']:
            return result(argv, 1, f'Error response from daemon: get {argv[-1]}: no such volume\n'.encode())
        if argv[:3] == ['docker', 'image', 'inspect']:
            return result(argv, 1, f'Error response from daemon: No such image: {argv[-1]}\n'.encode())
        return result(argv)

    def unstarted_fixture(self, directory):
        fixture = self.fixture(directory)
        fixture.created = fixture.network_created = False
        fixture.volume_capture_verified = True
        fixture.image_tags = []
        fixture.args.node_image = 'sha256:' + 'a' * 64
        fixture.args.age, fixture.args.recipient = 'age', 'public'
        fixture.attempt.encrypt = Mock()
        return fixture

    def test_entrypoint_reader_timeout_is_owned_removed_and_verified_absent(self):
        with tempfile.TemporaryDirectory() as temp:
            fixture = self.unstarted_fixture(temp)
            helper_present = False
            def run(argv, **kwargs):
                nonlocal helper_present
                self.assertGreater(kwargs['timeout'], 0)
                if argv[:3] == ['docker', 'container', 'inspect']:
                    return result(argv, 1, f'Error: No such object: {argv[-1]}\n'.encode())
                if argv[:3] == ['docker', 'image', 'inspect'] and argv[-1] == fixture.args.node_image:
                    return subprocess.CompletedProcess(argv, 0, stdout=canonical([{'Id': fixture.args.node_image}]), stderr=b'')
                if argv[:2] == ['docker', 'run']:
                    self.assertEqual(argv[argv.index('--name') + 1], fixture.entrypoint_reader)
                    self.assertIn('--rm', argv)
                    self.assertEqual(argv[argv.index('--network') + 1], 'none')
                    helper_present = True
                    raise subprocess.TimeoutExpired(argv, kwargs['timeout'])
                if argv[:3] == ['docker', 'container', 'rm']:
                    self.assertEqual(argv, ['docker', 'container', 'rm', '--force', '--volumes', fixture.entrypoint_reader])
                    self.assertTrue(helper_present)
                    helper_present = False
                    return result(argv)
                if argv[:2] == ['docker', 'inspect'] and argv[-1] == fixture.entrypoint_reader and helper_present:
                    return result(argv)
                return self.absent(fixture, argv, **kwargs)
            with patch('rehearse.subprocess.run', side_effect=run) as runner:
                with self.assertRaises(subprocess.TimeoutExpired):
                    fixture.start()
                self.assertTrue(fixture.entrypoint_reader_started)
                self.assertFalse(fixture.created)
                self.assertTrue(fixture.cleanup())
            receipt = fixture.attempt.record.call_args.args[1]
            self.assertTrue(receipt['entrypointReaderStarted'] and receipt['entrypointReaderAbsent'])
            self.assertEqual(receipt['entrypointReaderDeleteState'], 'completed')
            self.assertEqual(receipt['inspectionStates']['entrypointReader'], 'absent')
            self.assertFalse(helper_present)
            self.assertTrue(any(c.args[0] == ['docker', 'inspect', fixture.entrypoint_reader]
                                for c in runner.call_args_list))

    def test_existing_or_unverified_entrypoint_reader_is_refused_and_never_removed(self):
        for state in ('present', 'unverified'):
            with self.subTest(state=state), tempfile.TemporaryDirectory() as temp:
                fixture = self.unstarted_fixture(temp)
                def run(argv, **kwargs):
                    if argv[:3] == ['docker', 'container', 'inspect']:
                        if argv[-1] == fixture.entrypoint_reader:
                            return result(argv) if state == 'present' else result(argv, 1, b'Cannot connect to daemon')
                        return result(argv, 1, f'Error: No such object: {argv[-1]}\n'.encode())
                    if argv[:2] == ['docker', 'inspect'] and argv[-1] == fixture.entrypoint_reader:
                        return result(argv) if state == 'present' else result(argv, 1, b'Cannot connect to daemon')
                    return self.absent(fixture, argv, **kwargs)
                with patch('rehearse.subprocess.run', side_effect=run) as runner:
                    with self.assertRaisesRegex(ValueError, 'fixture-name-exists-or-absence-unverified'):
                        fixture.start()
                    self.assertFalse(fixture.entrypoint_reader_started)
                    self.assertFalse(fixture.cleanup())
                self.assertFalse(any(c.args[0][:2] == ['docker', 'run'] or c.args[0][:3] == ['docker', 'container', 'rm']
                                     for c in runner.call_args_list))
                receipt = fixture.attempt.record.call_args.args[1]
                self.assertFalse(receipt['entrypointReaderAbsent'])
                self.assertEqual(receipt['inspectionStates']['entrypointReader'], state)

    def test_remaining_or_unverified_entrypoint_reader_prevents_cleanup_success(self):
        for state in ('present', 'unverified'):
            with self.subTest(state=state), tempfile.TemporaryDirectory() as temp:
                fixture = self.fixture(temp)
                fixture.volume_capture_verified = fixture.entrypoint_reader_started = True
                def run(argv, **kwargs):
                    self.assertGreater(kwargs['timeout'], 0)
                    if argv[:3] == ['docker', 'container', 'rm']:
                        self.assertEqual(argv[-1], fixture.entrypoint_reader)
                        if state == 'unverified':
                            raise subprocess.TimeoutExpired(argv, kwargs['timeout'])
                        return result(argv, 1, b'fixture helper removal failed')
                    if argv[:2] == ['docker', 'inspect'] and argv[-1] == fixture.entrypoint_reader:
                        return result(argv) if state == 'present' else result(argv, 1, b'Cannot connect to daemon')
                    return self.absent(fixture, argv, **kwargs)
                with patch('rehearse.subprocess.run', side_effect=run):
                    self.assertFalse(fixture.cleanup())
                receipt = fixture.attempt.record.call_args.args[1]
                self.assertFalse(receipt['passed'] or receipt['entrypointReaderAbsent'])
                self.assertEqual(receipt['inspectionStates']['entrypointReader'], state)
                self.assertEqual(receipt['entrypointReaderDeleteState'], 'completed' if state == 'present' else 'timed-out')
                self.assertTrue(receipt['nodeAbsent'] and receipt['networkAbsent'] and receipt['fixtureImageTagsAbsent']
                                and receipt['fixtureVolumesAbsent'] and receipt['freshCredentialFileAbsent'])

    def test_early_failure_discovers_volumes_before_teardown(self):
        with tempfile.TemporaryDirectory() as temp:
            fixture = self.fixture(temp)
            events = []
            def capture():
                events.append('capture')
                fixture.fixture_volumes = ['owned-volume']
                fixture.volume_capture_verified = True
            fixture.capture_volumes.side_effect = capture
            def run(argv, **kwargs):
                events.append(argv[0])
                return self.absent(fixture, argv, **kwargs)
            with patch('rehearse.subprocess.run', side_effect=run) as runner:
                self.assertTrue(fixture.cleanup())
            self.assertEqual(events[0], 'capture')
            receipt = fixture.attempt.record.call_args.args[1]
            self.assertEqual(receipt['volumeInspectionStates'], {'owned-volume': 'absent'})
            self.assertTrue(receipt['fixtureImageTagsAbsent'])
            # Unmeasured claims are labelled as declarations, never recorded as observations.
            self.assertNotIn('unrelatedDockerObjectsChanged', receipt)
            self.assertIn('not measured', receipt['unmeasuredDeclarations']['basis'])
            self.assertFalse(fixture.kubeconfig.exists())
            self.assertTrue(all(call.kwargs.get('timeout') for call in runner.call_args_list))

    def test_empty_unknown_volume_inventory_cannot_pass(self):
        with tempfile.TemporaryDirectory() as temp:
            fixture = self.fixture(temp)
            with patch('rehearse.subprocess.run', side_effect=lambda argv, **kw: self.absent(fixture, argv, **kw)):
                self.assertFalse(fixture.cleanup())
            receipt = fixture.attempt.record.call_args.args[1]
            self.assertFalse(receipt['volumeInventoryVerified'])
            self.assertFalse(receipt['fixtureVolumesAbsent'])

    def test_failed_image_removal_with_present_or_unverified_alias_cannot_pass(self):
        for state in ('present', 'unverified'):
            with self.subTest(state=state), tempfile.TemporaryDirectory() as temp:
                fixture = self.fixture(temp)
                fixture.volume_capture_verified = True
                def run(argv, **kwargs):
                    if argv[:3] == ['docker', 'image', 'rm']:
                        return result(argv, 1, b'conflict')
                    if argv[:3] == ['docker', 'image', 'inspect']:
                        return result(argv) if state == 'present' else result(argv, 1, b'Cannot connect to daemon')
                    return self.absent(fixture, argv, **kwargs)
                with patch('rehearse.subprocess.run', side_effect=run):
                    self.assertFalse(fixture.cleanup())
                receipt = fixture.attempt.record.call_args.args[1]
                self.assertFalse(receipt['fixtureImageTagsAbsent'])

    def test_timed_out_teardown_still_verifies_remaining_exact_objects(self):
        with tempfile.TemporaryDirectory() as temp:
            fixture = self.fixture(temp)
            fixture.volume_capture_verified = True
            def run(argv, **kwargs):
                if argv[0] == 'kind' or argv[:2] == ['docker', 'inspect']:
                    raise subprocess.TimeoutExpired(argv, kwargs['timeout'])
                return self.absent(fixture, argv, **kwargs)
            with patch('rehearse.subprocess.run', side_effect=run) as runner:
                self.assertFalse(fixture.cleanup())
            receipt = fixture.attempt.record.call_args.args[1]
            self.assertEqual(receipt['kindDeleteState'], 'timed-out')
            self.assertEqual(receipt['inspectionStates']['node'], 'unverified')
            self.assertTrue(receipt['fixtureImageTagsAbsent'])
            self.assertTrue(any(c.args[0][:3] == ['docker', 'image', 'rm'] for c in runner.call_args_list))

    def test_present_network_or_retained_fresh_credential_cannot_pass(self):
        for case in ('network-present', 'credential-retained'):
            with self.subTest(case=case), tempfile.TemporaryDirectory() as temp:
                fixture = self.fixture(temp)
                fixture.volume_capture_verified = True
                if case == 'credential-retained':
                    # Unlinking a directory raises, so the fresh credential path is not proven absent.
                    fixture.kubeconfig.unlink();fixture.kubeconfig.mkdir()
                def run(argv, **kwargs):
                    if case == 'network-present' and argv[:3] == ['docker', 'network', 'inspect']:
                        return result(argv)
                    return self.absent(fixture, argv, **kwargs)
                with patch('rehearse.subprocess.run', side_effect=run):
                    self.assertFalse(fixture.cleanup())
                receipt = fixture.attempt.record.call_args.args[1]
                self.assertEqual((receipt['networkAbsent'], receipt['freshCredentialFileAbsent']),
                                 (case != 'network-present', case != 'credential-retained'))
                self.assertTrue(receipt['nodeAbsent'] and receipt['fixtureVolumesAbsent'] and receipt['fixtureImageTagsAbsent'])

    def test_failed_or_raising_cleanup_makes_an_otherwise_passing_attempt_fail(self):
        for outcome in (False, OSError('daemon unavailable')):
            with self.subTest(outcome=outcome):
                path = Mock();path.read_bytes.return_value = canonical({'sourceClusterUid': 'source-1',
                                                                        'nativeEndpoint': 'https://192.168.1.30:6443'})
                args = SimpleNamespace(source_inventory=path, project_root='repo', evidence_root='private',
                                       attempt_id='unit', operator='operator')
                attempt = Mock(directory=Path('/private/unit'))
                fixture = Mock(last_step='retirement-final-state')
                fixture.cleanup.side_effect = [outcome] if isinstance(outcome, Exception) else None
                fixture.cleanup.return_value = outcome
                with patch('rehearse.Attempt', return_value=attempt), patch('rehearse.Fixture', return_value=fixture), \
                     patch('rehearse.tool_identities', return_value={'runtimeVersionOutput': {'kind': {'exitCode': 0}}}):
                    _, state = rehearse(args)
                records = {c.args[0]: c.args[1] for c in attempt.record.call_args_list}
                fixture.execute.assert_called_once()
                self.assertNotIn('failure.json', records)
                self.assertEqual((state, records['summary.json']['state']), ('failed-cleanup', 'failed-cleanup'))
                attempt.finish.assert_called_once()


if __name__ == '__main__':
    unittest.main()
