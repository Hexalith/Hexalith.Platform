"""Exercise the fixture's real cleanup block without starting Dapr or Docker."""
import ast
from pathlib import Path
import signal
import subprocess
from types import SimpleNamespace
import unittest
from unittest.mock import Mock


class LocalDaprCleanupTests(unittest.TestCase):
    def cleanup_after_disappearance(self, timeout):
        tree = ast.parse(Path(__file__).with_name('test-local-dapr-security.py').read_text())
        cleanup = next(node.finalbody for node in ast.walk(tree) if isinstance(node, ast.Try) and node.finalbody)
        first, second = Mock(pid=101), Mock(pid=102)
        for process in (first, second):
            process.poll.side_effect = [None, 0]
        if timeout:
            first.wait.side_effect = [subprocess.TimeoutExpired('daprd', 8), 0]
        disappeared_signal = signal.SIGKILL if timeout else signal.SIGTERM

        def signal_process(pid, selected_signal):
            if pid == first.pid and selected_signal == disappeared_signal:
                raise ProcessLookupError(pid)

        killpg = Mock(side_effect=signal_process)
        server, log = Mock(), Mock()
        namespace = {
            'processes': [first, second], 'servers': [server], 'logs': [log], 'containers': ['owned-container'],
            'os': SimpleNamespace(killpg=killpg), 'signal': signal,
            'subprocess': SimpleNamespace(TimeoutExpired=subprocess.TimeoutExpired, STDOUT=subprocess.STDOUT, check_output=Mock(return_value=''), run=Mock(return_value=SimpleNamespace(returncode=0))),
            'run': 'fixture', 'fixtures': Path('fixture-evidence'), 'open': Mock(), 'print': Mock(),
        }
        exec(compile(ast.Module(body=cleanup, type_ignores=[]), 'fixture-cleanup', 'exec'), namespace)
        killpg.assert_any_call(first.pid, disappeared_signal)
        second.wait.assert_called_once_with(timeout=8)
        server.shutdown.assert_called_once_with()
        server.server_close.assert_called_once_with()
        log.close.assert_called_once_with()
        namespace['subprocess'].run.assert_any_call(['docker', 'rm', '-f', 'owned-container'], check=True, capture_output=True)
        namespace['subprocess'].check_output.assert_called_once()

    def test_disappearance_before_sigterm_preserves_remaining_cleanup(self):
        self.cleanup_after_disappearance(False)

    def test_disappearance_before_sigkill_preserves_remaining_cleanup(self):
        self.cleanup_after_disappearance(True)


if __name__ == '__main__':
    unittest.main()
