"""Prepare exact source bindings and historical companion trees without changing refs or the index."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile

from current_tuple_source import create_binding, read_selection, verify_package_release_identities
from legacy_tuple_source import PROVENANCE, git

FROZEN = {
    "legacy": {
        "anchor": "1b4b2fd0c60091bc191cbf050d44ca8b79f740bb",
        "hashes": {
            "eng/builds-tuple.json": "554359e26442ff2a039d927236e9d126d55c1b752c2c0edf632062f767f3ed8a",
            ".config/dotnet-tools.json": "f11d2b9583071655167b3f98eaf5cd2a1b59de8f120544d244c4729ae91ca149",
            "hexalith-platform.json": "4b324533e9d3bbcc40532b1e161f0b134ce4eea7fe93d6dffe893007cabc2a27",
            "eng/tuple-ci-suites.json": "61689f1598dac43a9fa28dcf5319008059e0fea0f1018209b1a8e79e4f63d7b0",
            PROVENANCE: "3e9b04109c5ddb34a0ac6801f5cdb36e1cbc102c65bfccb54b77c4d408091a61",
        },
        "gitlinks": {
            "references/Hexalith.Builds": "fc2122240a39f03628c04fa8433e5eece90b8b4c",
            "references/Hexalith.EventStore": "cab8363c2b6e2c0588efc8ca9c2e09fab0006841",
            "references/Hexalith.Parties": "bb2783120c0eed620e3d4f52a2ebaf6aa510126f",
        },
    },
    "pre-extension": {
        "anchor": "3adbcc2b24042c0dd089f7a255dbbf9202fe8916",
        "hashes": {
            "eng/builds-tuple.json": "9dbcfc55c112c166e0374aa1a96d8907c733938d86725a0e86034657475be4df",
            ".config/dotnet-tools.json": "f8cf363dd259788d71047ed7e3b14e5917f7531c43b06a2f83f438359dc23b6c",
            "hexalith-platform.json": "6b6894855b603881d19c44dd8787ed2840176c0190aec5fecaa64f1a93f4ee9b",
            "eng/tuple-ci-suites.json": "3643ab2f60f255bd0d0b9a91452e1f7060b5f5b69bf0fef0a51cf8336a526c6c",
            PROVENANCE: "3931a6ff47adf0edd4b6d19430294f6ebed2c334b50655528e751eee049fc846",
        },
        "gitlinks": {
            "references/Hexalith.Builds": "27145e52ffab5623198ba5efdab5cdb71aa42d28",
            "references/Hexalith.EventStore": "cab8363c2b6e2c0588efc8ca9c2e09fab0006841",
            "references/Hexalith.Parties": "ebe1739d70a4e76447938b0bb8c53db508bf6019",
        },
    },

    "package-reader": {
        "anchor": "6e43e042347358e522bfbb2f909986c95fadc6ec",
        "hashes": {
            "eng/builds-tuple.json": "c7fd2a7fd72ff26998359e9bdadf4963710415d0632ed32428b7859fb6633460",
            ".config/dotnet-tools.json": "487645c2f69045b70a6a1ba65447fad165ce19741cea6a71569e3e84c8779ef1",
            "hexalith-platform.json": "180d38e62058dce4164feb89a48570d608e2c40c76557c58dc7bd1303a7db26c",
            PROVENANCE: "71e06a7ec737db61eb6fa1de81f6d3f92d0d3e57588c60e32989aad18edec621",
        },
        "gitlinks": {
            "references/Hexalith.Builds": "5ec1779040606be89306129a7d2b245258c9acd2",
            "references/Hexalith.EventStore": "aa16227bd07f0564a3026d619a5abd973e02656f",
            "references/Hexalith.Parties": "ebe1739d70a4e76447938b0bb8c53db508bf6019",
        },
    },
}


def prepare_tree(root: Path, revision: str, frozen: dict) -> str:
    """Copy current source, substituting only authenticated frozen tuple metadata."""
    entries = []
    for path, expected in frozen["hashes"].items():
        raw = git(root, "show", frozen["anchor"] + ":" + path)
        if hashlib.sha256(raw).hexdigest() != expected:
            raise ValueError("Historical tuple metadata differs from its frozen body: " + path)
        entry = git(root, "ls-tree", frozen["anchor"], "--", path).decode().strip().split()
        if len(entry) != 4 or entry[:2] != ["100644", "blob"]:
            raise ValueError("Historical tuple metadata is not a regular committed file: " + path)
        entries.append((entry[0], entry[2], path))
    for path, source in frozen["gitlinks"].items():
        actual = git(root, "ls-tree", frozen["anchor"], "--", path).decode().split()
        if len(actual) != 4 or actual[:3] != ["160000", "commit", source]:
            raise ValueError("Historical tuple owner differs from its frozen gitlink: " + path)
        entries.append(("160000", source, path))
    with tempfile.TemporaryDirectory(prefix="platform-companion-index-") as directory:
        environment = dict(os.environ, GIT_INDEX_FILE=str(Path(directory) / "index"))
        def run(*args):
            return subprocess.run(["git", *args], cwd=root, env=environment, capture_output=True,
                                  check=True, timeout=30).stdout.decode().strip()
        run("read-tree", revision)
        for mode, blob, path in entries:
            run("update-index", "--add", "--cacheinfo", mode + "," + blob + "," + path)
        tree = run("write-tree")
    changed = set(git(root, "diff", "--name-only", revision, tree).decode().splitlines())
    if not changed <= set(frozen["hashes"]) | set(frozen["gitlinks"]):
        raise ValueError("Companion tree unexpectedly changed production, controls or helper files.")
    return tree


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["binding", "trees", "reader-workspace"])
    parser.add_argument("--source", required=True, help="Exact committed source/tree identity; never worktree bytes")
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--workspace", type=Path, help="New task-owned isolated historical reader checkout")
    parser.add_argument("--output", type=Path, help="Optional new JSON file; no refs, commits or descriptors are updated")
    args = parser.parse_args()
    root = args.root.resolve()
    revision = git(root, "rev-parse", args.source + "^{}").decode().strip()
    if revision != args.source or len(revision) != 40:
        raise ValueError("Select the full exact Git identity, not a moving reference.")
    if args.mode == "binding":
        result = create_binding(root, revision)
    elif args.mode == "reader-workspace":
        from package_reader_source import prepare_workspace
        if args.workspace is None:
            raise ValueError("Reader preparation requires an explicit new isolated workspace.")
        result = prepare_workspace(root, revision, args.workspace)
    else:
        if git(root, "cat-file", "-t", revision).decode().strip() != "commit":
            raise ValueError("Historical companion preparation requires an actual committed source.")
        read_selection(root, revision)
        verify_package_release_identities(root, revision)
        result = {"sourceRevision": revision, "companions": {
            name: {"tree": prepare_tree(root, revision, frozen), "historicalAnchor": frozen["anchor"]}
            for name, frozen in FROZEN.items()}, "commitsCreated": False, "refsChanged": False}
    raw = json.dumps(result, indent=2) + "\n"
    if args.output:
        with args.output.open("x") as output:
            output.write(raw)
    else:
        print(raw, end="")


if __name__ == "__main__":
    main()
