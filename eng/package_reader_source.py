"""Prepare real isolated same-code checkouts for the immutable SDK4.2/T705/M25 reader inputs."""
from __future__ import annotations

import hashlib
from pathlib import Path
import re
import subprocess

from legacy_tuple_source import commit, decode, git
from pre_extension_tuple_source import DESCRIPTOR, resolve
from prepare_tuple_sources import FROZEN


def selected_source(root: Path, current: str) -> str:
    resolve(root, current)
    document = decode(git(root, "show", current + ":" + DESCRIPTOR))
    if "packageReaderSourceRevision" not in document:
        raise ValueError("An explicit committed historical packageReaderSourceRevision is required.")
    return commit(document["packageReaderSourceRevision"])


def verify_source(root: Path, current: str, selected: str, *, frozen=None) -> dict:
    current, selected = commit(current), commit(selected)
    if selected_source(root, current) != selected:
        raise ValueError("The requested reader source differs from its committed descriptor.")
    git(root, "cat-file", "-e", selected + "^{commit}")
    frozen = FROZEN["package-reader"] if frozen is None else frozen
    changed = set(git(root, "diff", "--name-only", current, selected).decode().splitlines())
    allowed = set(frozen["hashes"]) | set(frozen["gitlinks"]) | {DESCRIPTOR, "eng/legacy-tuple-source.json"}
    if not changed <= allowed:
        raise ValueError("Historical reader source differs from the current production, controls or helpers.")
    for path, expected in frozen["hashes"].items():
        if hashlib.sha256(git(root, "show", selected + ":" + path)).hexdigest() != expected:
            raise ValueError("Historical reader metadata differs from its exact published tuple: " + path)
    for path, expected in frozen["gitlinks"].items():
        actual = git(root, "ls-tree", selected, "--", path).decode().split()
        if len(actual) != 4 or actual[:3] != ["160000", "commit", expected]:
            raise ValueError("Historical reader owner differs from its exact recorded source: " + path)
    from current_tuple_source import verify_companion_descriptors
    verify_companion_descriptors(root, current, selected)
    return {"currentSourceRevision": current, "packageReaderSourceRevision": selected,
            "sourceEquivalence": "verified", "runtimeAcceptance": False, "candidateAcceptance": False}


def direct_owners(root: Path, revision: str) -> list[str]:
    data = git(root, "config", "--blob", revision + ":.gitmodules", "--get-regexp", r"^submodule\..*\.path$").decode()
    paths = [line.split(None, 1)[1] for line in data.splitlines()]
    if not paths or len(paths) != len(set(paths)) or any(re.fullmatch(r"references/[A-Za-z0-9][A-Za-z0-9._-]*", path) is None for path in paths):
        raise ValueError("Reader preparation requires unique root-declared direct owner paths.")
    return paths


def clone_checkout(source: Path, destination: Path, revision: str):
    if git(source, "rev-parse", "--show-toplevel").decode().strip() != str(source.resolve()):
        raise ValueError("An initialized direct source repository is required: " + str(source))
    subprocess.run(["git", "-c", "submodule.recurse=false", "clone", "--shared", "--no-checkout", str(source), str(destination)],
                   check=True, capture_output=True, timeout=120)
    # Keep the real recorded repository identity; all mutation is confined to the task-owned clone.
    remote = git(source, "remote", "get-url", "origin").decode().strip()
    git(destination, "remote", "set-url", "origin", remote)
    git(destination, "-c", "submodule.recurse=false", "checkout", "--detach", revision)
    if git(destination, "rev-parse", "HEAD").decode().strip() != revision:
        raise ValueError("The prepared checkout does not equal its exact recorded source.")


def prepare_workspace(root: Path, current: str, destination: Path) -> dict:
    selected = selected_source(root, current)
    receipt = verify_source(root, current, selected)
    destination = destination.resolve()
    if destination.exists() or destination == root.resolve() or root.resolve() in destination.parents:
        raise ValueError("Choose a new task-owned historical reader workspace outside the source repository.")
    clone_checkout(root, destination, selected)
    owners = {}
    for relative in direct_owners(root, selected):
        source = commit(git(root, "rev-parse", selected + ":" + relative).decode().strip())
        owner = destination / relative
        clone_checkout(root / relative, owner, source)
        owners[relative] = source
    if git(destination, "status", "--porcelain", "--untracked-files=normal").strip():
        raise ValueError("The prepared historical reader checkout is not clean.")
    receipt.update(workspace=str(destination), owners=owners, sharedSourceCheckoutsChanged=False)
    return receipt
