"""Run verifier commands in a disposable process group with bounded cleanup."""

import os
import signal
import subprocess
import threading
import time
from contextlib import contextmanager, suppress


@contextmanager
def _termination_interrupt():
    """Defer default cancellation until launch has entered its cleanup boundary."""
    if threading.current_thread() is not threading.main_thread():
        yield lambda: None
        return

    launching = True
    pending = None

    def interrupted(signum, _frame):
        nonlocal pending
        error = KeyboardInterrupt() if signum == signal.SIGINT else SystemExit(128 + signum)
        if launching:
            if pending is None:
                pending = error
            return
        raise error

    def finish_launch():
        nonlocal launching
        launching = False
        if pending is not None:
            raise pending

    previous = {}
    try:
        for signum in (signal.SIGTERM, signal.SIGINT):
            handler = signal.getsignal(signum)
            if handler == signal.SIG_DFL or signum == signal.SIGINT and handler == signal.default_int_handler:
                previous[signum] = signal.signal(signum, interrupted)
        yield finish_launch
    finally:
        for signum, handler in previous.items():
            signal.signal(signum, handler)


def _stop_group(process):
    """Signal only the session created for this command, then reap its leader."""
    with suppress(ProcessLookupError):
        os.killpg(process.pid, signal.SIGTERM)
    try:
        deadline = time.monotonic() + 1
        while True:
            process.poll()
            try:
                os.killpg(process.pid, 0)
            except ProcessLookupError:
                break
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                break
            time.sleep(min(0.01, remaining))
    finally:
        # The leader may have exited while a descendant ignores TERM or still
        # holds its output file. Its owned group must be cleaned in that case too.
        with suppress(ProcessLookupError):
            os.killpg(process.pid, signal.SIGKILL)
        process.wait()


def run_owned(command, cwd=None, env=None, stdout=None, timeout=1800):
    """Return command status while cleaning its entire owned POSIX process group.

    Both output streams use the caller's log stream. Timeout, interrupt and
    termination exceptions retain their original identity after owned cleanup.
    A successfully exited leader also cannot leave members of its group running.
    Processes deliberately detached into another session require the caller's
    separate lifecycle management; they are outside this owned-group boundary.
    """
    if os.name != "posix":
        raise OSError("Works verifier command ownership requires POSIX process groups")

    with _termination_interrupt() as finish_launch:
        process = None
        try:
            process = subprocess.Popen(
                command, cwd=cwd, env=env, stdout=stdout, stderr=subprocess.STDOUT, start_new_session=True
            )
            finish_launch()
            returncode = process.wait(timeout=timeout)
        except BaseException as error:
            try:
                if process is not None:
                    _stop_group(process)
            except BaseException as cleanup_error:
                error.add_note("Owned command cleanup failed: " + str(cleanup_error))
            raise
        else:
            _stop_group(process)
            return subprocess.CompletedProcess(command, returncode)
