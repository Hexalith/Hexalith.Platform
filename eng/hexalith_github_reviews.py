"""Bounded authenticated GitHub review GETs, without policy or a CLI.

Credentials are ephemeral call inputs. A disposable process bounds DNS, TLS,
headers and body reads together; no redirect, proxy or response URL is followed.
"""

from __future__ import annotations

from dataclasses import dataclass, field
import http.client
import json
import math
import multiprocessing
import re
import selectors
import socket
import ssl
import struct
import sys
import time


PRODUCTION_ISSUER = "https://api.github.com"
API_VERSION = "2026-03-10"
MAX_RESPONSE_BYTES = 1_048_576
_MAX_REQUEST_BYTES = 409_600
_OWNER = re.compile(r"[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})\Z")
_REPOSITORY = re.compile(r"[A-Za-z0-9_.-]{1,100}\Z")
_TOKEN = re.compile(r"[A-Za-z0-9_.-]{1,4096}\Z")


class GitHubReviewRetrievalError(ValueError):
    """A content-free refusal; response bodies and credentials are never echoed."""


def _refuse(code: str) -> None:
    raise GitHubReviewRetrievalError(code) from None


def _finite_number(value: object) -> bool:
    """Check representability without letting enormous integers escape refusal."""
    try:
        return type(value) in (int, float) and math.isfinite(value)
    except OverflowError:
        return False


def _elapsed() -> float:
    """Linux CLOCK_BOOTTIME includes host suspension; unsupported clocks refuse."""
    try:
        clock = getattr(time, "CLOCK_BOOTTIME", None)
        if sys.platform != "linux" or clock is None:
            _refuse("review-transport-unavailable")
        return time.clock_gettime(clock)
    except Exception:
        _refuse("review-transport-unavailable")


@dataclass(frozen=True, slots=True)
class ReviewLocator:
    """An exact review resource, never an evidence-supplied URL."""

    owner: str
    repository: str
    pull_number: int
    review_id: int

    def __post_init__(self) -> None:
        if type(self.owner) is not str or _OWNER.fullmatch(self.owner) is None:
            _refuse("review-owner-invalid")
        if (type(self.repository) is not str or _REPOSITORY.fullmatch(self.repository) is None
                or self.repository in (".", "..")):
            _refuse("review-repository-invalid")
        for number in (self.pull_number, self.review_id):
            if type(number) is not int or not 0 < number <= 2**63 - 1:
                _refuse("review-resource-id-invalid")

    @property
    def pull_path(self) -> str:
        """Fixed API path for the bound pull request."""
        return f"/repos/{self.owner}/{self.repository}/pulls/{self.pull_number}"

    @property
    def review_path(self) -> str:
        """Fixed API path for the bound review."""
        return f"{self.pull_path}/reviews/{self.review_id}"


@dataclass(frozen=True, slots=True)
class RequestLimits:
    """Explicit byte and whole-request time budgets, with no operational defaults."""

    max_response_bytes: int
    deadline_seconds: float

    def __post_init__(self) -> None:
        if type(self.max_response_bytes) is not int or not 0 < self.max_response_bytes <= MAX_RESPONSE_BYTES:
            _refuse("response-byte-limit-invalid")
        if not _finite_number(self.deadline_seconds) or self.deadline_seconds <= 0:
            _refuse("request-deadline-invalid")


def _receive_exact(channel: socket.socket, size: int) -> bytes:
    """Child-only bounded receive; the parent can terminate it at its deadline."""
    raw = bytearray()
    while len(raw) < size:
        chunk = channel.recv(size - len(raw))
        if not chunk:
            _refuse("review-transport-unavailable")
        raw.extend(chunk)
    return bytes(raw)


def _request_worker(channel: socket.socket) -> None:
    """Private child entry point. Send bounded bytes or one closed refusal code."""
    connection = None
    try:
        size = struct.unpack("!I", _receive_exact(channel, 4))[0]
        if not 0 < size <= _MAX_REQUEST_BYTES:
            _refuse("review-transport-unavailable")
        request = json.loads(_receive_exact(channel, size))
        host, port, ca_pem, path, token = (request[field] for field in ("host", "port", "ca", "path", "token"))
        limits = RequestLimits(request["bytes"], request["seconds"])
        context = ssl.create_default_context(cadata=ca_pem)
        context.keylog_filename = None
        context.minimum_version = ssl.TLSVersion.TLSv1_2
        connection = http.client.HTTPSConnection(host, port, timeout=limits.deadline_seconds, context=context)
        connection.request("GET", path, headers={
            "Authorization": "Bearer " + token,
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": API_VERSION,
            "User-Agent": "Hexalith-Platform-review-reader",
            "Accept-Encoding": "identity",
            "Cache-Control": "no-cache",
        })
        response = connection.getresponse()
        if response.status != 200:
            _refuse("review-http-refused")
        if response.getheader("Content-Encoding", "identity").lower() != "identity":
            _refuse("review-content-encoding-refused")
        length = response.getheader("Content-Length")
        transfer = response.getheader("Transfer-Encoding")
        if transfer is not None and (transfer.strip().lower() != "chunked" or length is not None):
            _refuse("review-transport-unavailable")
        if length is not None and (not length.isascii() or not length.isdecimal()
                                   or len(length) > 10 or int(length) > limits.max_response_bytes):
            _refuse("review-response-byte-budget-exceeded")
        raw = response.read(limits.max_response_bytes + 1)
        if len(raw) > limits.max_response_bytes:
            _refuse("review-response-byte-budget-exceeded")
        if length is not None and len(raw) != int(length):
            _refuse("review-response-incomplete")
        channel.sendall(b"O" + raw)
    except GitHubReviewRetrievalError as error:
        try:
            channel.sendall(b"E" + str(error).encode("ascii"))
        except OSError:
            pass
    except Exception:
        # TLS, socket, HTTP and parser exceptions can contain remote data.
        try:
            channel.sendall(b"Ereview-transport-unavailable")
        except OSError:
            pass
    finally:
        for resource in (connection, channel):
            if resource is not None:
                try:
                    resource.close()
                except Exception:
                    pass


@dataclass(frozen=True, slots=True)
class GitHubReviewClient:
    """Production reader, or an explicitly marked certificate-verified fixture.

    Production always uses the system CA store and fixed api.github.com:443.
    A fixture can only use 127.0.0.1 and its explicitly supplied TLS CA.
    """

    fixture_port: int | None = None
    fixture_ca_pem: str | None = field(default=None, repr=False)

    def __post_init__(self) -> None:
        if self.fixture_port is None and self.fixture_ca_pem is None:
            return
        if (type(self.fixture_port) is not int or not 0 < self.fixture_port < 65536
                or type(self.fixture_ca_pem) is not str or not self.fixture_ca_pem or not self.fixture_ca_pem.isascii()
                or len(self.fixture_ca_pem) > 65_536):
            _refuse("loopback-tls-fixture-invalid")

    @property
    def issuer(self) -> str:
        """Fixtures never claim the production issuer."""
        if self.fixture_port is None:
            return PRODUCTION_ISSUER
        return f"fixture:github-reviews:https://127.0.0.1:{self.fixture_port}"

    @property
    def api_origin(self) -> str:
        """Origin used only to compare metadata, never to select a URL."""
        if self.fixture_port is None:
            return PRODUCTION_ISSUER
        return f"https://127.0.0.1:{self.fixture_port}"

    def fetch_review(self, locator: ReviewLocator, *, token: str, limits: RequestLimits) -> bytes:
        """Re-fetch one review, terminating the worker at the absolute deadline.

        Spawn-based callers must use Python's usual guarded main entry point.
        No credential is retained in the client, result, command line or errors.
        """
        parent_channel = child_channel = process = None
        try:
            started = _elapsed()
            if type(locator) is not ReviewLocator or type(limits) is not RequestLimits:
                _refuse("review-locator-and-limits-required")
            if type(token) is not str or _TOKEN.fullmatch(token) is None:
                _refuse("review-credential-required")
            deadline = started + limits.deadline_seconds
            payload = json.dumps({
                "host": "api.github.com" if self.fixture_port is None else "127.0.0.1",
                "port": 443 if self.fixture_port is None else self.fixture_port, "ca": self.fixture_ca_pem,
                "path": locator.review_path, "token": token,
                "bytes": limits.max_response_bytes, "seconds": limits.deadline_seconds,
            }, separators=(",", ":")).encode("ascii")
            if len(payload) > _MAX_REQUEST_BYTES:
                _refuse("review-transport-unavailable")
            if _elapsed() >= deadline:
                _refuse("review-request-deadline-exceeded")
            packet = memoryview(struct.pack("!I", len(payload)) + payload)
            parent_channel, child_channel = socket.socketpair()
            # Only the socket is in multiprocessing's blocking bootstrap pipe.
            # All application request parameters use the deadline-bound channel.
            process = multiprocessing.get_context("spawn").Process(
                target=_request_worker, args=(child_channel,), daemon=True)
            process.start()
            child_channel.close()
            parent_channel.setblocking(False)
            raw = bytearray()
            channel_budget = max(limits.max_response_bytes + 1, 80)
            with selectors.DefaultSelector() as selector:
                selector.register(parent_channel, selectors.EVENT_WRITE)
                while packet:
                    remaining = deadline - _elapsed()
                    if remaining <= 0:
                        _refuse("review-request-deadline-exceeded")
                    if not selector.select(min(remaining, 0.05)):
                        continue
                    sent = parent_channel.send(packet)
                    if sent <= 0:
                        _refuse("review-transport-unavailable")
                    packet = packet[sent:]
                selector.unregister(parent_channel)
                selector.register(parent_channel, selectors.EVENT_READ)
                while True:
                    remaining = deadline - _elapsed()
                    if remaining <= 0:
                        _refuse("review-request-deadline-exceeded")
                    if not selector.select(min(remaining, 0.05)):
                        continue
                    chunk = parent_channel.recv(min(65_536, channel_budget + 1 - len(raw)))
                    if not chunk:
                        break
                    raw.extend(chunk)
                    if len(raw) > channel_budget:
                        _refuse("review-response-byte-budget-exceeded")
            if _elapsed() >= deadline:
                _refuse("review-request-deadline-exceeded")
            if raw[:1] == b"O":
                if len(raw) - 1 > limits.max_response_bytes:
                    _refuse("review-response-byte-budget-exceeded")
                return bytes(raw[1:])
            known_errors = {b"review-http-refused", b"review-content-encoding-refused",
                            b"review-response-byte-budget-exceeded", b"review-response-incomplete",
                            b"review-transport-unavailable"}
            if raw[:1] == b"E" and bytes(raw[1:]) in known_errors:
                _refuse(bytes(raw[1:]).decode("ascii"))
            _refuse("review-transport-unavailable")
        except GitHubReviewRetrievalError:
            raise
        except Exception:
            _refuse("review-transport-unavailable")
        finally:
            for channel in (parent_channel, child_channel):
                if channel is not None:
                    try:
                        channel.close()
                    except Exception:
                        pass
            if process is not None:
                try:
                    if process.pid is not None:
                        if process.is_alive():
                            process.kill()
                        process.join(timeout=0.1)
                        if not process.is_alive():
                            process.close()
                except Exception:
                    pass
