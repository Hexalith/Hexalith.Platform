"""Materialize the existing bound producer without changing the workspace checkout."""

import argparse
import hashlib
import os
import subprocess
from pathlib import Path

from builds_tuple import load_binding, recorded_gitlink, resolve_builds_root, verify_catalog


def inspection_environment():
    """Inspect physical repositories without ambient Git redirection or lazy fetching."""
    return {key: value for key, value in os.environ.items() if not key.upper().startswith("GIT_")} | {
        "GIT_NO_REPLACE_OBJECTS": "1",
        "GIT_NO_LAZY_FETCH": "1",
        "GIT_OPTIONAL_LOCKS": "0",
    }


def verify_bound_checkout(platform_root, builds_root):
    """Verify against the owner's recorded Builds gitlink, never a digest of an arbitrary current file."""
    platform_root, builds_root = Path(platform_root).resolve(), Path(builds_root).resolve()
    binding_path = platform_root / "eng/builds-tuple.json"
    load_binding(binding_path)
    environment = inspection_environment()
    gitlink = recorded_gitlink(platform_root, environment=environment)
    if (
        not (builds_root / ".git").exists()
        or Path(
            subprocess.check_output(
                ["git", "rev-parse", "--show-toplevel"], cwd=builds_root, env=environment, text=True
            ).strip()
        ).resolve()
        != builds_root
    ):
        raise ValueError("The bound producer must be its own initialized physical checkout.")
    catalog, _, identity = verify_catalog(gitlink, builds_root, environment=environment)
    if hashlib.sha256(catalog.read_bytes()).hexdigest() != identity["sha256"]:
        raise ValueError("The bound catalog working bytes differ from the authenticated Git body.")
    entries = subprocess.check_output(
        ["git", "ls-files", "-v", "-z"], cwd=builds_root, env=environment, text=True
    ).split("\0")
    if any(entry and (entry[0].islower() or entry.startswith("S ")) for entry in entries):
        raise ValueError("The bound producer has assume-unchanged or skip-worktree tracked inputs.")
    if subprocess.check_output(
        ["git", "status", "--porcelain", "--untracked-files=all"], cwd=builds_root, env=environment, text=True
    ).strip():
        raise ValueError("The bound producer checkout has modified or untracked source inputs.")
    return {
        "buildsRoot": str(builds_root),
        "catalog": str(catalog),
        "catalogBlob": identity["blob"],
        "catalogSha256": identity["sha256"],
        "sourceRevision": gitlink,
        "producerBinding": str(binding_path),
    }


def materialize(platform_root, output, source_root=None):
    """Create a run-owned exact checkout; its owning run is responsible for cleanup."""
    platform_root, output = Path(platform_root).resolve(), Path(output).absolute()
    if output.exists() or output.is_symlink():
        raise ValueError("Choose a new bound producer directory; existing inputs are never replaced.")
    source = Path(source_root).resolve() if source_root is not None else resolve_builds_root(platform_root).resolve()
    if output.resolve().is_relative_to(source):
        raise ValueError("The bound producer directory must be outside the original checkout.")
    environment = inspection_environment()

    if (
        not (source / ".git").exists()
        or Path(subprocess.run(
            ["git", "-C", str(source), "rev-parse", "--show-toplevel"],
            env=environment, capture_output=True, text=True, check=True,
        ).stdout.strip()).resolve() != source
    ):
        raise ValueError("The bound producer requires an initialized physical Builds checkout.")
    load_binding(platform_root / "eng/builds-tuple.json")
    revision = recorded_gitlink(platform_root, environment=environment)
    output.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        ["git", "clone", "--quiet", "--shared", "--no-checkout", "--", str(source), str(output)],
        env=environment, capture_output=True, text=True, check=True,
    )
    if subprocess.run(
        ["git", "-C", str(output), "cat-file", "-e", revision + "^{commit}"],
        env=environment, capture_output=True, text=True,
    ).returncode:
        origin = subprocess.run(
            ["git", "-C", str(source), "remote", "get-url", "origin"],
            env=environment, capture_output=True, text=True, check=True,
        ).stdout.strip()
        subprocess.run(
            ["git", "-C", str(output), "fetch", "--quiet", "--no-auto-maintenance", "--no-recurse-submodules",
             "--depth=1", "--", origin, revision],
            env=environment, capture_output=True, text=True, check=True,
        )
    # The pin checker authenticates exact Git bytes, while the producer normally
    # checks this text file out as CRLF. Override conversion only in this private clone.
    (output / ".git/info/attributes").write_text("Props/Directory.Packages.props -text\n")
    # recorded_gitlink accepts only a full hexadecimal object ID. Checkout
    # requires the revision before --, which ends the revision arguments.
    subprocess.run(
        ["git", "-C", str(output), "checkout", "--quiet", "--detach", "--no-recurse-submodules", revision, "--"],
        env=environment, capture_output=True, text=True, check=True,
    )
    return verify_bound_checkout(platform_root, output)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--platform-root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--source-root", type=Path)
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument("--output", type=Path)
    action.add_argument("--verify-root", type=Path)
    arguments = parser.parse_args()
    context = (
        verify_bound_checkout(arguments.platform_root, arguments.verify_root)
        if arguments.verify_root is not None
        else materialize(arguments.platform_root, arguments.output, arguments.source_root)
    )
    print(context["buildsRoot"])


if __name__ == "__main__":
    main()
