"""Owned monotonic HTTP authentication acquisition and admitted-work handoff."""
import threading
import time


class AuthenticationAcquisition:
    """One connection deadline, retained until current authentication hands ownership to admitted work."""
    def __init__(self, deadline, expire):
        self.deadline, self.expire_socket = deadline, expire
        self.lock, self.admitted, self.timer = threading.Lock(), False, None

    def check(self):
        if time.monotonic() >= self.deadline:
            raise TimeoutError('CurrentCohortAuthenticationAcquisitionDeadline')

    def admit(self):
        with self.lock:
            self.check()
            self.admitted = True
            self.timer.cancel()

    def expire(self):
        with self.lock:
            if not self.admitted:
                self.expire_socket()

