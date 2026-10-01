"""Cleanup needs complete volume coverage and bounded explicit absence readback."""
from pathlib import Path
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

from rehearse import Fixture


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
            return result(argv, 1, f'Error: No such object: {fixture.node}\n'.encode())
        if argv[:3] == ['docker', 'network', 'inspect']:
            return result(argv, 1, f'Error response from daemon: network {fixture.network} not found\n'.encode())
        if argv[:3] == ['docker', 'volume', 'inspect']:
            return result(argv, 1, f'Error response from daemon: get {argv[-1]}: no such volume\n'.encode())
        if argv[:3] == ['docker', 'image', 'inspect']:
            return result(argv, 1, f'Error response from daemon: No such image: {argv[-1]}\n'.encode())
        return result(argv)

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


if __name__ == '__main__':
    unittest.main()
