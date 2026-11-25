#!/usr/bin/env python3
"""Submit the operator's synthetic tenant command and wait for its outcome."""

import http.client
import json
import os
import re
import secrets
import signal
import sys
import time
from urllib.error import HTTPError
from urllib.parse import quote, urlsplit
from urllib.request import HTTPRedirectHandler, Request, build_opener


TENANT_ID = re.compile(r"[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?")
HEADER_NAME = re.compile(r"[!#$%&'*+.^_`|~0-9A-Za-z-]+")
ULID_ALPHABET = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"
STATUSES = ("Received", "Processing", "EventsStored", "EventsPublished",
            "Completed", "Rejected", "PublishFailed", "TimedOut")
MAX_RESPONSE_BYTES = 65_536


class InvalidInvocation(Exception):
    """The invocation is missing or malformed."""


class TaskTimeout(Exception):
    """The task ran out of time waiting for a command outcome."""


class NoRedirects(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        # The invocation credentials are scoped to the configured gateway.
        return None


def read_invocation():
    with open(os.environ["HEXALITH_TASK_INVOCATION_PATH"], encoding="utf-8") as source:
        invocation = json.load(source)
    if not isinstance(invocation, dict):
        raise InvalidInvocation

    tenant_id = invocation.get("syntheticTenantId")
    if not isinstance(tenant_id, str) or not TENANT_ID.fullmatch(tenant_id) or tenant_id == "system":
        raise InvalidInvocation("SyntheticTenantIdInvalidRejection")

    gateway_url = invocation.get("gatewayUrl")
    command_tenant = invocation.get("commandTenant")
    headers = invocation.get("headers")
    if (not isinstance(gateway_url, str) or not isinstance(command_tenant, str)
            or not command_tenant.strip() or not isinstance(headers, dict)):
        raise InvalidInvocation
    url = urlsplit(gateway_url)
    if (url.scheme not in {"http", "https"} or not url.hostname or url.port == 0
            or url.username is not None or url.query or url.fragment
            or any(c.isspace() for c in gateway_url)):
        raise InvalidInvocation

    for name, value in headers.items():
        if (not HEADER_NAME.fullmatch(name) or not isinstance(value, str)
                or any((ord(c) < 32 and c != "\t") or ord(c) == 127 or ord(c) > 255 for c in value)):
            raise InvalidInvocation

    return tenant_id, gateway_url.rstrip("/"), command_tenant, headers


def new_message_id():
    value = (time.time_ns() // 1_000_000 << 80) | secrets.randbits(80)
    return "".join(ULID_ALPHABET[(value >> shift) & 31] for shift in range(125, -1, -5))


def response_json(response):
    data = response.read(MAX_RESPONSE_BYTES + 1)
    if len(data) > MAX_RESPONSE_BYTES:
        raise ValueError
    document = json.loads(data)
    if not isinstance(document, dict):
        raise ValueError
    return document


def report_rejection(tenant_id, rejection_type):
    if not isinstance(rejection_type, str):
        return 1
    short_name = rejection_type.split(",", 1)[0].rsplit(".", 1)[-1]
    if short_name == "TenantAlreadyExistsRejection":
        print(f"{tenant_id}: TenantAlreadyExistsRejection", file=sys.stderr)
    elif short_name == "InsufficientPermissionsRejection":
        print("GlobalAdminRequired: InsufficientPermissionsRejection", file=sys.stderr)
    elif short_name == "SyntheticTenantIdInvalidRejection":
        print("SyntheticTenantIdInvalidRejection", file=sys.stderr)
        return 2
    else:
        print("The gateway reported a domain rejection.", file=sys.stderr)
    return 3


def submit_and_poll(tenant_id, gateway_url, command_tenant, headers, timeout_seconds):
    commands_url = gateway_url + "/api/v1/commands"
    opener = build_opener(NoRedirects())
    deadline = time.monotonic() + timeout_seconds

    def send(request):
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise TaskTimeout
        return opener.open(request, timeout=remaining)

    command = {
        "messageId": new_message_id(),
        "tenant": command_tenant,
        "domain": "tenants",
        "aggregateId": tenant_id,
        "commandType": "create-synthetic-tenant",
        "payload": {"tenantId": tenant_id},
    }
    submit = Request(commands_url, json.dumps(command).encode("utf-8"), headers)
    submit.add_header("Content-Type", "application/json")
    try:
        with send(submit) as response:
            accepted = response_json(response)
    except HTTPError as error:
        with error:
            if error.code in {401, 403}:
                print(f"The gateway refused the operator command (HTTP {error.code}).", file=sys.stderr)
                return 3
            if error.code in {409, 422}:
                rejection_type = response_json(error).get("rejectionType")
                if rejection_type:
                    return report_rejection(tenant_id, rejection_type)
            raise

    correlation_id = accepted.get("correlationId")
    status_id = accepted.get("messageId", correlation_id)
    if not isinstance(correlation_id, str) or not correlation_id.strip() \
            or not isinstance(status_id, str) or not status_id.strip():
        raise ValueError

    status_request = Request(commands_url + "/status/" + quote(status_id, safe=""), headers=headers)
    while True:
        try:
            with send(status_request) as response:
                status = response_json(response)
        except HTTPError as error:
            with error:
                if error.code != 404:
                    raise
        else:
            code = status.get("statusCode")
            if (status_id not in (status.get("messageId"), status.get("correlationId"))
                    or type(code) is not int or not 0 <= code < len(STATUSES)
                    or status.get("status") != STATUSES[code]):
                raise ValueError
            if code == 4:
                return 0
            if code >= 5 and status.get("retryable") is not True:
                rejection_type = status.get("rejectionEventType")
                if code == 5 and isinstance(rejection_type, str) and rejection_type.strip() and not status.get("failureReason"):
                    return report_rejection(tenant_id, rejection_type)
                raise ValueError

        time.sleep(max(0, min(1, deadline - time.monotonic())))


def timeout_handler(signum, frame):
    raise TaskTimeout


def main(timeout_seconds=120):
    try:
        invocation = read_invocation()
    except InvalidInvocation as error:
        print(str(error) or "Invalid synthetic tenant task invocation.", file=sys.stderr)
        return 2
    except (KeyError, OSError, ValueError):
        print("Invalid synthetic tenant task invocation.", file=sys.stderr)
        return 2

    # A socket timeout bounds each read. The alarm also bounds a response that
    # delivers bytes slowly enough to keep the socket open.
    previous_handler = signal.signal(signal.SIGALRM, timeout_handler)
    try:
        signal.setitimer(signal.ITIMER_REAL, timeout_seconds)
        return submit_and_poll(*invocation, timeout_seconds)
    except (TaskTimeout, TimeoutError):
        print("Timed out waiting for the synthetic tenant command.", file=sys.stderr)
        return 1
    except (OSError, ValueError, http.client.HTTPException):
        print("The gateway did not confirm the synthetic tenant command.", file=sys.stderr)
        return 1
    finally:
        signal.setitimer(signal.ITIMER_REAL, 0)
        signal.signal(signal.SIGALRM, previous_handler)


if __name__ == "__main__":
    sys.exit(main())
