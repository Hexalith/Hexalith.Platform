"""Declared qualification suite: consume runner observations and persist synthetic state."""

import json
import os
import pathlib
import socket
import time
import urllib.parse


def redis(endpoint, *parts):
    """Use only the allocated run-owned endpoint from the descriptor."""
    parsed = urllib.parse.urlparse(endpoint)
    assert parsed.hostname in ("localhost", "127.0.0.1", "::1")
    encoded = [str(part).encode("utf-8") for part in parts]
    request = b"*" + str(len(encoded)).encode() + b"\r\n"
    for part in encoded:
        request += b"$" + str(len(part)).encode() + b"\r\n" + part + b"\r\n"
    with socket.create_connection((parsed.hostname, parsed.port), timeout=5) as connection:
        connection.sendall(request)
        with connection.makefile("rb") as response:
            kind = response.read(1)
            value = response.readline().rstrip(b"\r\n")
            if kind == b"$":
                size = int(value)
                if size == -1:
                    return None
                result = response.read(size)
                assert response.read(2) == b"\r\n"
                return result.decode("utf-8")
            assert kind == b"+", "Redis command failed"
            return value.decode("utf-8")


path = pathlib.Path(os.environ["HEXALITH_TEST_ENVIRONMENT_PATH"])
assert path.is_absolute()
descriptor = json.loads(path.read_text(encoding="utf-8"))
assert descriptor["schema"] == "hexalith.test-environment.v1"
assert descriptor["policyId"] == "hexalith.platform.candidate-and-attachment-policy.v1"
bindings = descriptor["bindings"]
run_id = bindings["run"]["runId"]
assert run_id == os.environ["HEXALITH_G4_RUN_ID"]
assert bindings["environment"]["readiness"]["status"] == "ready"
assert all(task["status"] == "passed" for task in bindings["environment"]["startupTasks"]["tasks"])
assert bindings["environment"]["isolation"]["syntheticTenantId"] == os.environ["HEXALITH_G4_TENANT"]
assert bindings["candidate"] == {"state": "not-yet-populated"}
resources = bindings["environment"]["resources"]
store = next(resource for resource in resources if resource["resourceId"] == "redis")
endpoint = next(endpoint["uri"] for endpoint in store["endpoints"] if endpoint["name"] == "tcp")
evidence = pathlib.Path(os.environ["HEXALITH_PLATFORM_PROVISIONING_EVIDENCE"])
evidence.mkdir(parents=True, exist_ok=True)
key = "platform-story-2-4-identical-key"
own_key = "platform-story-2-4-owner-" + run_id
assert redis(endpoint, "SET", key, run_id) == "OK"
assert redis(endpoint, "SET", own_key, run_id) == "OK"
assert redis(endpoint, "SAVE") == "OK"
receipt = evidence / (run_id + ".descriptor.json")
temporary = receipt.with_suffix(".tmp")
temporary.write_text(json.dumps(descriptor, indent=2), encoding="utf-8")
temporary.replace(receipt)
deadline = time.monotonic() + 80
while len(list(evidence.glob("*.descriptor.json"))) != 2:
    assert time.monotonic() < deadline, "Peer environment did not reach its declared suite"
    time.sleep(0.1)
peers = [json.loads(peer.read_text(encoding="utf-8")) for peer in evidence.glob("*.descriptor.json")]
peer = next(item for item in peers if item["bindings"]["run"]["runId"] != run_id)
peer_id = peer["bindings"]["run"]["runId"]
assert peer["bindings"]["run"]["environmentId"] != bindings["run"]["environmentId"]
assert peer["bindings"]["environment"]["catalog"]["instanceId"] != bindings["environment"]["catalog"]["instanceId"]
assert redis(endpoint, "GET", key) == run_id
assert redis(endpoint, "GET", own_key) == run_id
assert redis(endpoint, "GET", "platform-story-2-4-owner-" + peer_id) is None
(evidence / (run_id + ".data.json")).write_text(json.dumps({
    "runId": run_id,
    "environmentId": bindings["run"]["environmentId"],
    "identicalKey": key,
    "observedValue": run_id,
    "peerRunId": peer_id,
    "peerKeyVisible": False,
    "endpoint": endpoint,
    "persisted": True,
    "qualificationCredit": False,
}, indent=2), encoding="utf-8")
pathlib.Path(os.environ["HEXALITH_G4_TEST_REPORT_PATH"]).write_text(
    '<TestRun><ResultSummary outcome="Completed"><Counters total="1" passed="1" failed="0" /></ResultSummary></TestRun>',
    encoding="utf-8",
)
