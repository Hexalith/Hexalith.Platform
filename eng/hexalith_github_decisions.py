"""Immutable exact-review authentication over the pinned GitHub transport.

This module knows no application policy, grants, gates or action schemas.
Authentication is a fresh HTTPS observation, never an authorization handle.
"""

from __future__ import annotations

from collections.abc import Mapping
from dataclasses import dataclass, field
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
from types import MappingProxyType

import hexalith_github_reviews as _transport

if (getattr(_transport, "__file__", None) is None
        or Path(_transport.__file__).resolve() != Path(__file__).with_name("hexalith_github_reviews.py").resolve()
        or getattr(getattr(_transport, "__spec__", None), "origin", None) != str(Path(_transport.__file__).resolve())):
    raise ImportError("pinned-hexalith-github-review-origin-required") from None

from hexalith_github_reviews import (  # noqa: E402
    GitHubReviewClient, GitHubReviewRetrievalError, RequestLimits, ReviewLocator, _elapsed, _finite_number,
)


_DIGEST = re.compile(r"[0-9a-f]{64}\Z")
_COMMIT = re.compile(r"[0-9a-f]{40}\Z")
_PRINCIPAL = re.compile(r"github:user:([1-9][0-9]{0,18})\Z")
_UTC = re.compile(r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z\Z")
_ENVELOPE_REQUIRED = frozenset({"id", "user", "state", "body", "commit_id", "submitted_at", "pull_request_url"})
_ENVELOPE_FIELDS = _ENVELOPE_REQUIRED | {"node_id", "html_url", "author_association", "_links"}


class GitHubDecisionError(ValueError):
    """A bounded content-free refusal; no remote text is propagated."""


def _refuse(code: str) -> None:
    raise GitHubDecisionError(code) from None


def wire_utc(value: object) -> datetime:
    """Read canonical second-precision UTC without aliases or local fallback."""
    if type(value) is not str or _UTC.fullmatch(value) is None:
        _refuse("decision-utc-not-canonical")
    try:
        return datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=timezone.utc)
    except ValueError:
        _refuse("decision-utc-invalid")


def require_principal(value: object) -> None:
    """Require one stable numeric GitHub user identity."""
    match = _PRINCIPAL.fullmatch(value) if type(value) is str else None
    if match is None or int(match.group(1)) > 2**63 - 1:
        _refuse("decision-principal-invalid")


def strict_json(raw: bytes) -> object:
    """Bounded immutable generic JSON, with duplicate and number refusals."""
    if type(raw) is not bytes or len(raw) > 1_048_576:
        _refuse("decision-byte-budget-or-type-invalid")
    if raw.startswith(b"\xef\xbb\xbf"):
        _refuse("utf8-bom-forbidden")
    try:
        text = raw.decode("utf-8", errors="strict")
    except UnicodeDecodeError:
        _refuse("invalid-utf8")
    depth = 0
    quoted = escaped = False
    for character in text:
        if quoted:
            if escaped:
                escaped = False
            elif character == "\\":
                escaped = True
            elif character == '"':
                quoted = False
        elif character == '"':
            quoted = True
        elif character in "[{":
            depth += 1
            if depth > 14:
                _refuse("json-depth-exceeded")
        elif character in "]}":
            depth -= 1

    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                _refuse("duplicate-json-field")
            result[key] = value
        return result

    def integer(text):
        if len(text.lstrip("-")) > 19:
            _refuse("integer-out-of-range")
        number = int(text)
        if not -(2**63) <= number <= 2**63 - 1:
            _refuse("integer-out-of-range")
        return number

    def number(_):
        _refuse("non-integer-json-number")

    def freeze(value):
        if type(value) is str:
            if len(value) > 4096:
                _refuse("string-budget-exceeded")
            if any(0xD800 <= ord(character) <= 0xDFFF for character in value):
                _refuse("unpaired-unicode-surrogate")
        elif type(value) is dict:
            return MappingProxyType({freeze(key): freeze(child) for key, child in value.items()})
        elif type(value) is list:
            return tuple(freeze(child) for child in value)
        return value

    try:
        return freeze(json.loads(text, object_pairs_hook=pairs, parse_int=integer,
                                 parse_float=number, parse_constant=number))
    except (json.JSONDecodeError, RecursionError):
        _refuse("invalid-json")


@dataclass(frozen=True, slots=True)
class ExactReviewBinding:
    """Independently retained exact body, reviewer, review resource and PR commit."""

    locator: ReviewLocator
    expected_principal: str
    reviewed_commit: str
    body_sha256: str
    body_byte_length: int
    retained_body: bytes = field(repr=False)

    def __post_init__(self) -> None:
        if type(self.locator) is not ReviewLocator:
            _refuse("decision-locator-required")
        require_principal(self.expected_principal)
        if type(self.reviewed_commit) is not str or _COMMIT.fullmatch(self.reviewed_commit) is None:
            _refuse("decision-commit-invalid")
        if type(self.body_sha256) is not str or _DIGEST.fullmatch(self.body_sha256) is None:
            _refuse("decision-digest-invalid")
        if (type(self.retained_body) is not bytes or type(self.body_byte_length) is not int
                or not 0 < self.body_byte_length <= 1_048_576
                or self.body_byte_length != len(self.retained_body)
                or hashlib.sha256(self.retained_body).hexdigest() != self.body_sha256):
            _refuse("decision-retained-body-mismatch")
        if not isinstance(strict_json(self.retained_body), Mapping):
            _refuse("decision-body-object-required")


@dataclass(frozen=True, slots=True)
class AuthenticatedReview:
    """Fresh immutable facts with no grant, raw response, credential or action."""

    issuer: str
    locator: ReviewLocator
    reviewer_principal: str
    reviewed_commit: str
    response_sha256: str
    body_sha256: str
    submitted_at_utc: datetime
    request_started_elapsed: float
    checked_elapsed: float
    body: Mapping = field(repr=False)


def authenticate_review(*, client: GitHubReviewClient, token: str, binding: ExactReviewBinding,
                        expected_issuer: str, limits: RequestLimits) -> AuthenticatedReview:
    """Fetch one exact approved review; application authorization stays external."""
    if type(client) is not GitHubReviewClient or type(binding) is not ExactReviewBinding or type(limits) is not RequestLimits:
        _refuse("decision-client-binding-and-limits-required")
    if client.issuer != expected_issuer:
        _refuse("decision-issuer-mismatch")
    try:
        started = _elapsed()
        if not _finite_number(started) or started < 0:
            _refuse("decision-elapsed-invalid")
        raw = client.fetch_review(binding.locator, token=token, limits=limits)
        data = strict_json(raw)
        if (not isinstance(data, Mapping) or not _ENVELOPE_REQUIRED <= set(data)
                or not set(data) <= _ENVELOPE_FIELDS):
            _refuse("decision-envelope-field-set")
        if (type(data["id"]) is not int or data["id"] != binding.locator.review_id
                or data["pull_request_url"] != client.api_origin + binding.locator.pull_path
                or data["commit_id"] != binding.reviewed_commit):
            _refuse("decision-resource-or-commit-mismatch")
        user = data["user"]
        if (not isinstance(user, Mapping) or user.get("type") != "User"
                or type(user.get("id")) is not int or not 0 < user["id"] <= 2**63 - 1):
            _refuse("decision-user-invalid")
        principal = f"github:user:{user['id']}"
        if principal != binding.expected_principal:
            _refuse("decision-principal-mismatch")
        if data["state"] != "APPROVED":
            _refuse("decision-not-approved")
        if type(data["body"]) is not str:
            _refuse("decision-body-required")
        body_raw = data["body"].encode("utf-8")
        if len(body_raw) != binding.body_byte_length or hashlib.sha256(body_raw).hexdigest() != binding.body_sha256:
            _refuse("decision-body-mismatch")
        body = strict_json(body_raw)
        submitted = wire_utc(data["submitted_at"])
        checked = _elapsed()
        if not _finite_number(checked) or checked < started:
            _refuse("decision-elapsed-invalid")
        return AuthenticatedReview(client.issuer, binding.locator, principal, binding.reviewed_commit,
                                   hashlib.sha256(raw).hexdigest(), binding.body_sha256,
                                   submitted, started, checked, body)
    except GitHubReviewRetrievalError as error:
        _refuse(str(error))
