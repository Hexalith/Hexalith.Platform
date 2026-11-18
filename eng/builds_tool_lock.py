"""Generate and enforce eng/builds-tool.lock.json, the NuGet SHA-512 content hash of the pinned Builds tool.

Run `python3 eng/builds_tool_lock.py` after a Builds version bump. It writes the lock only after
`dotnet nuget verify --all` passes and the hash equals the nuget.org `packageHash`.
"""
import argparse
import base64
import binascii
import gzip
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import urllib.request

from builds_tuple import ROOT, TOOL_PACKAGE, load_binding, validate_owner_pins

LOCK = ROOT / "eng/builds-tool.lock.json"
CONTENT = "https://api.nuget.org/v3-flatcontainer/"
REGISTRATION = "https://api.nuget.org/v3/registration5-gz-semver2/"
PACKAGE_BOUND = 200_000_000


def content_hash(raw):
    """NuGet's contentHash: base64 SHA-512 of the exact .nupkg bytes."""
    return base64.b64encode(hashlib.sha512(raw).digest()).decode()


def load_lock(path=LOCK):
    try:
        lock = json.loads(Path(path).read_bytes())
    except (OSError, ValueError) as error:
        raise ValueError("The Builds tool lock " + str(path) + " is missing or unreadable; run python3 eng/builds_tool_lock.py.") from error
    if not isinstance(lock, dict) or set(lock) != {"schemaVersion", "packages"} or lock["schemaVersion"] != 1 \
            or not isinstance(lock["packages"], list):
        raise ValueError("The Builds tool lock must be schema 1 with exactly schemaVersion and packages.")
    seen = set()
    for entry in lock["packages"]:
        if not isinstance(entry, dict) or set(entry) != {"id", "version", "contentHash"} \
                or not all(isinstance(entry[key], str) and entry[key] for key in entry) or entry["id"] in seen:
            raise ValueError("Each Builds tool lock entry needs one unique {id, version, contentHash}.")
        try:
            if len(base64.b64decode(entry["contentHash"], validate=True)) != 64:
                raise ValueError
        except (binascii.Error, ValueError) as error:
            raise ValueError("The Builds tool lock contentHash for " + entry["id"] + " is not a base64 SHA-512.") from error
        seen.add(entry["id"])
    return lock


def locked_entry(lock, version, package_id=TOOL_PACKAGE):
    """A missing entry or a stale version refuses before any download."""
    entries = [entry for entry in lock["packages"] if entry["id"] == package_id]
    if not entries:
        raise ValueError("The Builds tool lock has no entry for " + package_id + "; run python3 eng/builds_tool_lock.py.")
    if entries[0]["version"] != version:
        raise ValueError("The Builds tool lock names " + package_id + " " + entries[0]["version"] + ", but the Builds pin is "
                         + version + "; run python3 eng/builds_tool_lock.py.")
    return entries[0]


def require_locked(entry, raw):
    observed = content_hash(raw)
    if observed != entry["contentHash"]:
        raise ValueError("Tool package " + entry["id"] + " " + entry["version"] + " has contentHash " + observed
                         + ", which differs from eng/builds-tool.lock.json.")
    return observed


def _get(url, bound):
    request = urllib.request.Request(url, headers={"User-Agent": "Hexalith-Platform-tool-lock/1.0"})
    with urllib.request.urlopen(request, timeout=120) as response:
        raw = response.read(bound + 1)
        encoding = response.headers.get("Content-Encoding", "")
    if encoding == "gzip":
        # The NuGet registration hive is served gzip-encoded regardless of Accept-Encoding.
        raw = gzip.decompress(raw)
    if len(raw) > bound:
        raise ValueError("Response from " + url + " exceeds its bound.")
    return raw


def download(version, package_id=TOOL_PACKAGE, get=_get):
    lower = package_id.lower()
    return get(CONTENT + lower + "/" + version + "/" + lower + "." + version + ".nupkg", PACKAGE_BOUND)


def published_package_hash(version, package_id=TOOL_PACKAGE, get=_get):
    """Read nuget.org's own packageHash for the exact id and version."""
    leaf = json.loads(get(REGISTRATION + package_id.lower() + "/" + version + ".json", 2_000_000))
    catalog = leaf.get("catalogEntry")
    entry = json.loads(get(catalog, 10_000_000)) if isinstance(catalog, str) else catalog
    if not isinstance(entry, dict) or str(entry.get("id", "")).lower() != package_id.lower() \
            or entry.get("version") != version or entry.get("packageHashAlgorithm") != "SHA512" \
            or not isinstance(entry.get("packageHash"), str):
        raise ValueError("nuget.org has no SHA512 packageHash for " + package_id + " " + version + ".")
    return entry["packageHash"]


def verify_signature(path):
    """Run the repository-signature check; the caller keeps its output."""
    return subprocess.run(["dotnet", "nuget", "verify", str(path), "--all"], capture_output=True, text=True)


def lock_tool(version, archive=None, get=_get, verify=verify_signature):
    """Return the verified lock document; nothing is written unless every check passes."""
    raw = Path(archive).read_bytes() if archive is not None else download(version, get=get)
    with tempfile.TemporaryDirectory(prefix="platform-tool-lock-") as temporary:
        path = Path(temporary) / (TOOL_PACKAGE.lower() + "." + version + ".nupkg")
        path.write_bytes(raw)
        signature = verify(path)
        if signature.returncode:
            raise ValueError("dotnet nuget verify --all refused " + TOOL_PACKAGE + " " + version + ":\n"
                             + signature.stdout + signature.stderr)
    observed = content_hash(raw)
    published = published_package_hash(version, get=get)
    if observed != published:
        raise ValueError("The " + TOOL_PACKAGE + " " + version + " bytes have contentHash " + observed
                         + ", but nuget.org packageHash is " + published + ".")
    return {"schemaVersion": 1, "packages": [{"id": TOOL_PACKAGE, "version": version, "contentHash": observed}]}


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--archive", type=Path, help="Use these local package bytes instead of downloading them")
    parser.add_argument("--output", type=Path, default=LOCK)
    args = parser.parse_args()
    binding = load_binding()
    validate_owner_pins(binding, ROOT)
    lock = lock_tool(binding["version"], args.archive)
    args.output.write_text(json.dumps(lock, indent=2) + "\n")
    print(json.dumps(lock["packages"][0]))


if __name__ == "__main__":
    sys.exit(main())
