"""Materialize the existing bound producer without changing the workspace checkout."""

import argparse
import hashlib
import os
import subprocess
from pathlib import Path

from builds_tuple import load_binding, resolve_builds_root, verify_catalog


def inspection_environment():
    """Inspect physical repositories without ambient Git redirection or lazy fetching."""
    return {key: value for key, value in os.environ.items() if not key.upper().startswith("GIT_")} | {
        "GIT_NO_REPLACE_OBJECTS": "1",
        "GIT_NO_LAZY_FETCH": "1",
        "GIT_OPTIONAL_LOCKS": "0",
    }


def verify_bound_checkout(platform_root, builds_root):
    """Verify against the owner's existing binding, never a digest of an arbitrary current file."""
    platform_root, builds_root = Path(platform_root).resolve(), Path(builds_root).resolve()
    binding_path = platform_root / "eng/builds-tuple.json"
    binding = load_binding(binding_path, require_publication=False)
    environment = inspection_environment()
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
    catalog = verify_catalog(binding, builds_root, environment=environment)
    if hashlib.sha256(catalog.read_bytes()).hexdigest() != binding["producer"]["catalog"]["sha256"]:
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
        "catalogSha256": binding["producer"]["catalog"]["sha256"],
        "sourceRevision": binding["producer"]["sourceRevision"],
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

    def git(*arguments, check=True):
        return subprocess.run(
            ["git", "-c", "submodule.recurse=false", *map(str, arguments)],
            env=environment,
            capture_output=True,
            text=True,
            check=check,
        )

    if (
        not (source / ".git").exists()
        or Path(git("-C", source, "rev-parse", "--show-toplevel").stdout.strip()).resolve() != source
    ):
        raise ValueError("The bound producer requires an initialized physical Builds checkout.")
    revision = load_binding(platform_root / "eng/builds-tuple.json", require_publication=False)["producer"][
        "sourceRevision"
    ]
    output.parent.mkdir(parents=True, exist_ok=True)
    git("clone", "--quiet", "--shared", "--no-checkout", "--", source, output)
    if git("-C", output, "cat-file", "-e", revision + "^{commit}", check=False).returncode:
        origin = git("-C", source, "remote", "get-url", "origin").stdout.strip()
        git("-C", output, "fetch", "--quiet", "--no-recurse-submodules", "--depth=1", "--", origin, revision)
    # The pin checker authenticates exact Git bytes, while the producer normally
    # checks this text file out as CRLF. Override conversion only in this private clone.
    (output / ".git/info/attributes").write_text("Props/Directory.Packages.props -text\n")
    git("-C", output, "checkout", "--quiet", "--detach", revision)
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
