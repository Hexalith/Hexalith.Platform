#!/usr/bin/env python3
"""Write a detached EXT-HOST-1 profile, candidate, and replacement packet. Installs and accepts nothing."""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

SCHEMA = "EXT-HOST-1.replacement-acceptance.v2"
COMMIT = re.compile(r"[0-9a-f]{40}")
ROOT_KEYS = [
    "host", "integrationDate", "partiesBranch", "timing", "services",
    "currentTenant", "globalAdministrator", "humanIdentity", "approverEvidence",
    "replay", "recorder", "worker", "spoolBackend", "replicaCount", "failureModel",
    "protectionEngine", "protectionObserver", "partiesBinding", "conversationsBinding",
]
SERVICE_KEYS = ["eventStore", "tenants", "parties", "conversations", "agents"]
REFERENCE_KEYS = [
    "currentTenant", "globalAdministrator", "humanIdentity", "approverEvidence",
    "replay", "recorder", "worker", "spoolBackend", "failureModel",
    "protectionEngine", "protectionObserver", "partiesBinding", "conversationsBinding",
]
TIMING = {"L": 300, "S": 30, "O": 600, "H": 86400, "R": 604800}
FORBIDDEN = (
    "/home/administrator/.kube/hexalith-production",
    "openbao/Secret/openbao-server-tls",
    "openbao/Secret/openbao-seal",
)


def canonical(value):
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True) + "\n").encode()


def digest(value):
    return hashlib.sha256(canonical(value)).hexdigest()


def default_profile():
    return {
        "host": "192.168.1.30",
        "integrationDate": "2026-10-08",
        "partiesBranch": "B",
        "timing": dict(TIMING),
        "services": {key: "../" + ("eventstore" if key == "eventStore" else key) for key in SERVICE_KEYS},
        "currentTenant": "",
        "globalAdministrator": "",
        "humanIdentity": "",
        "approverEvidence": "",
        "replay": "",
        "recorder": "",
        "worker": "",
        "spoolBackend": "",
        "replicaCount": None,
        "failureModel": "",
        "protectionEngine": "",
        "protectionObserver": "",
        "partiesBinding": "",
        "conversationsBinding": "",
    }


def secret(value):
    if not isinstance(value, str):
        return False
    lowered = value.lower()
    if "begin " in lowered or "private key" in lowered or "client-certificate-data" in lowered or "client-key-data" in lowered:
        return True
    return any(item.lower() in lowered for item in FORBIDDEN)


def reference(value, name):
    if value is None or value == "":
        return None
    if not isinstance(value, str) or value != value.strip() or not value or len(value) > 256 or any(ch.isspace() for ch in value) or secret(value):
        raise ValueError("profile rejected a secret or malformed reference")
    return value


def missing_references(profile):
    if not isinstance(profile, dict) or sorted(profile) != sorted(ROOT_KEYS):
        raise ValueError("profile rejected an unknown or incomplete setting")
    if profile.get("host") != "192.168.1.30" or profile.get("integrationDate") != "2026-10-08" or profile.get("partiesBranch") != "B":
        raise ValueError("profile rejected accepted constants")
    timing = profile.get("timing")
    if not isinstance(timing, dict) or sorted(timing) != sorted(TIMING) or any(timing.get(key) != value for key, value in TIMING.items()):
        raise ValueError("profile rejected accepted constants")
    services = profile.get("services")
    if not isinstance(services, dict) or sorted(services) != sorted(SERVICE_KEYS):
        raise ValueError("profile rejected an unknown or incomplete setting")
    missing = []
    for key in SERVICE_KEYS:
        if reference(services.get(key), "services." + key) is None:
            missing.append("services." + key)
    for key in REFERENCE_KEYS:
        if reference(profile.get(key), key) is None:
            missing.append(key)
    replicas = profile.get("replicaCount")
    if replicas is None:
        missing.append("replicaCount")
    elif type(replicas) is not int or not 1 <= replicas <= 64:
        raise ValueError("profile rejected a secret or malformed reference")
    return missing


def git(root, *args):
    completed = subprocess.run(["git", "-C", str(root), *args], check=True, capture_output=True, text=True, timeout=30)
    return completed.stdout.strip()


def plan():
    return {
        "credentialSlots": sorted([*REFERENCE_KEYS, "replicaCount", *(f"services.{key}" for key in SERVICE_KEYS)]),
        "host": "192.168.1.30",
        "integrationDate": "2026-10-08",
        "partiesBranch": "B",
        "services": SERVICE_KEYS,
        "timing": TIMING,
    }


def prepare(root, output, profile_path=None):
    root, output = Path(root).resolve(), Path(output).resolve()
    if output == root or output.is_relative_to(root):
        raise ValueError("detached output must be outside candidate checkout")
    if output.exists():
        raise ValueError("fresh detached output directory required")
    profile = load_profile(profile_path) if profile_path else default_profile()
    missing = missing_references(profile)
    head = git(root, "rev-parse", "HEAD")
    if not COMMIT.fullmatch(head) or git(root, "status", "--porcelain"):
        raise ValueError("clean candidate checkout required")
    output.mkdir(parents=True)
    command = (f'test "$(git -C ../platform rev-parse HEAD)" = "{head}" && '
               'test -z "$(git -C ../platform status --porcelain)" && '
               "bash ../platform/eng/verify-agents-host.sh --mode Full --artifacts-path /tmp/ext-host-1-full")
    packet = {
        "schema": SCHEMA,
        "acceptedReplacementCommit": None,
        "availability": False,
        "candidate": head,
        "candidateCommit": head,
        "dependencyAvailable": False,
        "effects": 0,
        "exactCommand": command,
        "liveCompatibilityAuthorized": False,
        "missingReferences": missing,
        "planSha256": digest(plan()),
        "productionMutationAllowed": False,
        "profile": profile,
        "profileSha256": digest(profile),
    }
    (output / "agents-deployment-profile.json").write_bytes(canonical(profile))
    (output / "replacement-acceptance-packet.json").write_bytes(canonical(packet))
    (output / "exact-command.sh").write_text(command + "\n")
    return {"packetPrepared": True, "acceptedReplacementCommit": None, "dependencyAvailable": False,
            "effects": 0, "missingReferences": missing, "candidateCommit": head}


def load_profile(path):
    def pairs(items):
        keys = [key for key, _ in items]
        if len(keys) != len(set(keys)):
            raise ValueError("profile rejected an unknown or incomplete setting")
        return dict(items)
    return json.loads(Path(path).read_text(), object_pairs_hook=pairs)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", nargs="?")
    parser.add_argument("--output")
    parser.add_argument("--root")
    parser.add_argument("--profile")
    args = parser.parse_args()
    destination = args.output or args.directory
    if not destination:
        parser.error("a fresh directory outside the checkout is required")
    root = args.root or Path(__file__).resolve().parents[2]
    try:
        report = prepare(root, destination, args.profile)
    except (ValueError, OSError, subprocess.SubprocessError, json.JSONDecodeError) as error:
        print(str(error), file=sys.stderr)
        return 2
    print(json.dumps(report, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
