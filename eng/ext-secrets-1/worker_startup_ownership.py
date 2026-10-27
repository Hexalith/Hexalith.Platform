"""Classify only provably unstarted CPython workers; an interrupted bootstrap acknowledgement is owned work.

Thread.ident is set by Python bootstrap, after the OS launch. The native handle
and the interpreter's locked launch registry cover that earlier ownership window.
Unknown interpreter/startup state retains the charge instead of abandoning work.
"""
import threading


def provably_unstarted(worker):
    if worker is None:
        return True
    lock = getattr(threading, '_active_limbo_lock', None)
    launching = getattr(threading, '_limbo', None)
    if lock is None or launching is None:
        return False
    with lock:
        if worker.ident is not None or worker in launching or worker._started.is_set():
            return False
        handle = getattr(worker, '_os_thread_handle', getattr(worker, '_handle', None))
        # Native handles retain their identifier after genuine completion too.
        return handle is not None and getattr(handle, 'ident', None) == 0


def retain_until_workers_stop(cleanup):
    """Hold one bounded owner's cleanup through setup and every actually launched worker."""
    lock = threading.Lock()
    count = 0
    def claim():
        nonlocal count
        with lock:
            count += 1
        released = False
        def release():
            nonlocal count, released
            with lock:
                if released:
                    return
                released = True
                count -= 1
                finished = count == 0
            if finished:
                cleanup()
        return release
    return claim, claim()
