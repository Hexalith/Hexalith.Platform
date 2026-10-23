"""Executing regressions for verifier command and descendant ownership."""

import contextlib
import os
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path
from unittest.mock import patch

import works_verifier_command
from works_verifier_command import run_owned


@unittest.skipUnless(os.name == "posix", "POSIX process-group ownership")
class WorksVerifierCommandTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.child_pid = self.root / "child.pid"
        self.parent_pid = self.root / "parent.pid"
        self.log = self.root / "command.log"
        self.addCleanup(self.stop_fixture_tree)
        # Each child is owned by the newly created command session. Ignoring
        # TERM proves that cleanup reaches KILL rather than only stopping Python.
        self.command = [
            sys.executable,
            "-c",
            "\n".join(
                [
                    "import os, pathlib, signal, subprocess, sys, time",
                    "signal.signal(signal.SIGTERM, signal.SIG_IGN)",
                    "child = subprocess.Popen([sys.executable, '-c', "
                    "'import signal, time; signal.signal(signal.SIGTERM, signal.SIG_IGN); time.sleep(120)'])",
                    f"pathlib.Path({str(self.parent_pid)!r}).write_text(str(os.getpid()))",
                    f"pathlib.Path({str(self.child_pid)!r}).write_text(str(child.pid))",
                    "print('owned parent and child started', flush=True)",
                    "child.wait()",
                ]
            ),
        ]

    def stop_fixture_tree(self):
        if self.parent_pid.is_file():
            with contextlib.suppress(ProcessLookupError):
                os.killpg(int(self.parent_pid.read_text()), signal.SIGKILL)
        elif self.child_pid.is_file():
            with contextlib.suppress(ProcessLookupError):
                os.kill(int(self.child_pid.read_text()), signal.SIGKILL)

    def wait_for_children(self):
        deadline = time.monotonic() + 5
        while not self.child_pid.is_file():
            if time.monotonic() >= deadline:
                self.fail("Owned child did not start")
            time.sleep(0.01)

    def assert_stopped(self, pid):
        # On Linux an adopted zombie may await init's reap, but cannot run or
        # retain a live command handle. Other POSIX runners use kill(0).
        deadline = time.monotonic() + 5
        while True:
            try:
                state = Path(f"/proc/{pid}/stat")
                if state.is_file() and state.read_text().rsplit(")", 1)[1].split()[0] == "Z":
                    return
                os.kill(pid, 0)
            except (ProcessLookupError, FileNotFoundError):
                return
            if time.monotonic() >= deadline:
                self.fail(f"Owned process {pid} survived command cleanup")
            time.sleep(0.01)

    def assert_tree_stopped(self):
        self.assert_stopped(int(self.parent_pid.read_text()))
        self.assert_stopped(int(self.child_pid.read_text()))

    def test_timeout_stops_owned_parent_and_child_and_preserves_exception(self):
        original_wait = subprocess.Popen.wait
        entered = False

        def ready_wait(process, timeout=None):
            nonlocal entered
            if not entered:
                entered = True
                self.wait_for_children()
            return original_wait(process, timeout=timeout)

        with (
            self.log.open("w") as output,
            patch.object(works_verifier_command.subprocess.Popen, "wait", ready_wait),
            self.assertRaises(subprocess.TimeoutExpired) as caught,
        ):
            run_owned(self.command, self.root, os.environ.copy(), output, timeout=0.25)
        self.assertEqual(caught.exception.cmd, self.command)
        self.assertEqual(caught.exception.timeout, 0.25)
        self.assertIn("owned parent and child started", self.log.read_text())
        self.assert_tree_stopped()

    def test_keyboard_interrupt_cleans_real_tree_and_preserves_original_error(self):
        original_wait = subprocess.Popen.wait
        interrupt = KeyboardInterrupt("injected command cancellation")
        triggered = False

        def cancel_wait(process, timeout=None):
            nonlocal triggered
            if not triggered:
                triggered = True
                self.wait_for_children()
                raise interrupt
            return original_wait(process, timeout=timeout)

        with (
            self.log.open("w") as output,
            patch.object(works_verifier_command.subprocess.Popen, "wait", cancel_wait),
            self.assertRaises(KeyboardInterrupt) as caught,
        ):
            run_owned(self.command, self.root, os.environ.copy(), output)
        self.assertIs(caught.exception, interrupt)
        self.assert_tree_stopped()

    def test_unexpected_wait_failure_also_cleans_real_tree(self):
        original_wait = subprocess.Popen.wait
        failure = RuntimeError("injected wait failure")
        triggered = False

        def failing_wait(process, timeout=None):
            nonlocal triggered
            if not triggered:
                triggered = True
                self.wait_for_children()
                raise failure
            return original_wait(process, timeout=timeout)

        with (
            self.log.open("w") as output,
            patch.object(works_verifier_command.subprocess.Popen, "wait", failing_wait),
            self.assertRaises(RuntimeError) as caught,
        ):
            run_owned(self.command, self.root, os.environ.copy(), output)
        self.assertIs(caught.exception, failure)
        self.assert_tree_stopped()

    def test_completed_leader_cannot_leave_owned_descendant_running(self):
        command = [
            sys.executable,
            "-c",
            "\n".join(
                [
                    "import pathlib, subprocess, sys",
                    "child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(120)'])",
                    f"pathlib.Path({str(self.child_pid)!r}).write_text(str(child.pid))",
                ]
            ),
        ]
        with self.log.open("w") as output:
            result = run_owned(command, self.root, os.environ.copy(), output)
        self.assertEqual(result.returncode, 0)
        self.assert_stopped(int(self.child_pid.read_text()))

    def test_completed_leader_gives_remaining_group_time_for_graceful_cleanup(self):
        marker = self.root / "child-cleanup.txt"
        ready = self.root / "child-ready.txt"
        child_code = "\n".join(
            [
                "import pathlib, signal, sys, time",
                "def stop(_signal, _frame):",
                "    time.sleep(0.2)",
                f"    pathlib.Path({str(marker)!r}).write_text('graceful cleanup complete')",
                "    sys.exit(0)",
                "signal.signal(signal.SIGTERM, stop)",
                f"pathlib.Path({str(ready)!r}).write_text('ready')",
                "time.sleep(120)",
            ]
        )
        command = [
            sys.executable,
            "-c",
            "\n".join(
                [
                    "import pathlib, subprocess, sys, time",
                    f"child = subprocess.Popen([sys.executable, '-c', {child_code!r}])",
                    f"pathlib.Path({str(self.child_pid)!r}).write_text(str(child.pid))",
                    f"while not pathlib.Path({str(ready)!r}).is_file(): time.sleep(0.01)",
                ]
            ),
        ]
        with self.log.open("w") as output:
            result = run_owned(command, self.root, os.environ.copy(), output)
        self.assertEqual(result.returncode, 0)
        self.assertEqual(marker.read_text(), "graceful cleanup complete")
        self.assert_stopped(int(self.child_pid.read_text()))

    def test_deliberately_detached_session_requires_separate_caller_cleanup(self):
        command = [
            sys.executable,
            "-c",
            "\n".join(
                [
                    "import pathlib, subprocess, sys",
                    "child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(120)'], "
                    "start_new_session=True)",
                    f"pathlib.Path({str(self.child_pid)!r}).write_text(str(child.pid))",
                ]
            ),
        ]
        with self.log.open("w") as output:
            result = run_owned(command, self.root, os.environ.copy(), output)
        detached_pid = int(self.child_pid.read_text())
        try:
            self.assertEqual(result.returncode, 0)
            self.assertEqual(os.getsid(detached_pid), detached_pid)
            os.kill(detached_pid, 0)
        finally:
            # The test is the caller responsible for this explicitly detached
            # lifecycle. Never leave its deliberate survivor on the test host.
            with contextlib.suppress(ProcessLookupError):
                os.kill(detached_pid, signal.SIGKILL)
        self.assert_stopped(detached_pid)

    def test_signal_between_launch_and_assignment_cleans_real_tree(self):
        original_popen = subprocess.Popen
        for signum, error_type in ((signal.SIGTERM, SystemExit), (signal.SIGINT, KeyboardInterrupt)):
            with self.subTest(signal=signum):
                for path in (self.parent_pid, self.child_pid):
                    path.unlink(missing_ok=True)
                previous = signal.getsignal(signum)

                def launch_then_cancel(*args, launch_signal=signum, **kwargs):
                    process = original_popen(*args, **kwargs)
                    self.wait_for_children()
                    os.kill(os.getpid(), launch_signal)
                    return process

                with (
                    self.log.open("w") as output,
                    patch.object(works_verifier_command.subprocess, "Popen", launch_then_cancel),
                    self.assertRaises(error_type) as caught,
                ):
                    run_owned(self.command, self.root, os.environ.copy(), output)
                if signum == signal.SIGTERM:
                    self.assertEqual(caught.exception.code, 128 + signum)
                self.assertIs(signal.getsignal(signum), previous)
                self.assert_tree_stopped()

    def test_custom_and_ignored_signal_handlers_are_not_replaced(self):
        def custom_handler(_signal, _frame):
            return None

        for signum in (signal.SIGTERM, signal.SIGINT):
            for handler in (signal.SIG_IGN, custom_handler):
                with self.subTest(signal=signum, handler=handler):
                    previous = signal.signal(signum, handler)
                    try:
                        with works_verifier_command._termination_interrupt() as finish_launch:
                            self.assertIs(signal.getsignal(signum), handler)
                            finish_launch()
                        self.assertIs(signal.getsignal(signum), handler)
                    finally:
                        signal.signal(signum, previous)

    def test_status_combined_log_environment_and_working_directory_are_preserved(self):
        command = [
            sys.executable,
            "-c",
            "\n".join(
                [
                    "import os, sys",
                    "print(os.getcwd())",
                    "print(os.environ['WORKS_OWNED_COMMAND_VALUE'])",
                    "print('stderr retained', file=sys.stderr)",
                    "sys.exit(7)",
                ]
            ),
        ]
        with self.log.open("w") as output:
            result = run_owned(command, self.root, os.environ | {"WORKS_OWNED_COMMAND_VALUE": "owned"}, output)
        self.assertEqual(result.args, command)
        self.assertEqual(result.returncode, 7)
        self.assertIsNone(result.stdout)
        self.assertIsNone(result.stderr)
        self.assertEqual(set(self.log.read_text().splitlines()), {str(self.root), "owned", "stderr retained"})

    def test_sigterm_to_verifier_cleans_real_tree_and_restores_disposition(self):
        verifier = self.root / "verifier.py"
        verifier.write_text(
            "\n".join(
                [
                    "import signal, sys",
                    f"sys.path.insert(0, {str(Path(__file__).resolve().parent)!r})",
                    "from works_verifier_command import run_owned",
                    "try:",
                    f"    run_owned({self.command!r})",
                    "except SystemExit as error:",
                    "    assert signal.getsignal(signal.SIGTERM) == signal.SIG_DFL",
                    "    raise error",
                ]
            )
        )
        with self.log.open("w") as output:
            verifier_process = subprocess.Popen(
                [sys.executable, str(verifier)], stdout=output, stderr=subprocess.STDOUT
            )
            try:
                self.wait_for_children()
                verifier_process.send_signal(signal.SIGTERM)
                self.assertEqual(verifier_process.wait(timeout=10), 128 + signal.SIGTERM)
            finally:
                if verifier_process.poll() is None:
                    verifier_process.kill()
                    verifier_process.wait()
        self.assert_tree_stopped()


if __name__ == "__main__":
    unittest.main()
