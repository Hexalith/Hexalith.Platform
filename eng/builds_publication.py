"""Authenticate the pinned Builds release by its tag; observed release IDs are evidence only."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import sys
import urllib.request

API = "https://api.github.com/repos/Hexalith/Hexalith.Builds/"


def read_official(path):
    return _read_official(path, os.environ.get("GH_TOKEN"))


def _read_official(path, token):
    request = urllib.request.Request(API + path, headers={"User-Agent": "Hexalith-Platform-publication-verification/1.0",
                                                       "Accept": "application/vnd.github+json"})
    if token:
        # Keep the credential on this official request only, including across redirects.
        request.add_unredirected_header("Authorization", "Bearer " + token)
    with urllib.request.urlopen(request, timeout=60) as response:
        raw = response.read(2_000_001)
    if len(raw) > 2_000_000:
        raise ValueError("Official release metadata exceeds its bound.")
    return json.loads(raw)


def tag_commit(tag, read=read_official):
    """Peel the official tag, annotated or lightweight, to the commit it names."""
    reference = read("git/ref/tags/" + tag)["object"]
    for _ in range(4):
        if reference.get("type") == "commit":
            break
        if reference.get("type") != "tag":
            raise ValueError("Builds release tag " + tag + " does not identify a commit.")
        reference = read("git/tags/" + reference["sha"])["object"]
    if reference.get("type") != "commit" or not isinstance(reference.get("sha"), str):
        raise ValueError("Builds release tag " + tag + " does not identify a commit.")
    return reference["sha"]


def verify_release(binding, gitlink, read=read_official):
    """The tag commit and the release marker must both equal the recorded Builds gitlink."""
    tag = binding["releaseTag"]
    commit = tag_commit(tag, read)
    if commit != gitlink:
        raise ValueError("Builds release tag " + tag + " names commit " + commit + ", but the recorded Builds gitlink is " + gitlink + ".")
    release = read("releases/tags/" + tag)
    body = release.get("body")
    if release.get("tag_name") != tag or release.get("draft") is not False or release.get("prerelease") is not False \
            or not release.get("published_at") or not isinstance(body, str):
        raise ValueError("Builds " + tag + " is not a published release.")
    markers = {}
    for line in body.splitlines():
        name, separator, value = line.partition(":")
        if separator and name.startswith("hexalith-release"):
            markers.setdefault(name, []).append(value.strip())
    if markers.get("hexalith-release") != [gitlink]:
        raise ValueError("Builds release " + tag + " hexalith-release marker differs from the recorded Builds gitlink " + gitlink + ".")
    return {"releaseTag": tag, "tagCommit": commit, "releaseId": release.get("id"), "publishedAt": release["published_at"],
            "releaseBodySha256": hashlib.sha256(body.encode()).hexdigest(),
            "markers": {name: values[0] if len(values) == 1 else values for name, values in sorted(markers.items())},
            "evidenceOnly": True, "qualificationCredit": False}


def main():
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from builds_tuple import ROOT, load_binding, recorded_gitlink, validate_owner_pins
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--revision", default="HEAD", help="Recorded Platform revision whose Builds gitlink is checked")
    args = parser.parse_args()
    binding = load_binding()
    validate_owner_pins(binding, ROOT)
    print(json.dumps(verify_release(binding, recorded_gitlink(ROOT, args.revision)), indent=2))


if __name__ == "__main__":
    main()
