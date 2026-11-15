"""Generate the source binding from exact candidate Git objects; never create companion revisions."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from current_tuple_source import commit, create_binding, git


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["binding"])
    parser.add_argument("--source", required=True, help="Exact committed source/tree identity; never worktree bytes")
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--output", type=Path, help="Optional new JSON file; no refs, commits or descriptors are updated")
    args = parser.parse_args()
    root = args.root.resolve()
    revision = commit(args.source)
    if git(root, "rev-parse", revision + "^{}").decode().strip() != revision:
        raise ValueError("Select the full exact Git identity, not a moving reference.")
    raw = json.dumps(create_binding(root, revision), indent=2) + "\n"
    if args.output:
        with args.output.open("x") as output:
            output.write(raw)
    else:
        print(raw, end="")


if __name__ == "__main__":
    main()
