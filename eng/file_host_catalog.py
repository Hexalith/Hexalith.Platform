"""Authenticate the current file AppHost catalog from the selected candidate."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from builds_tuple import load_binding
from current_tuple_source import commit, git


def prepare(root: Path, builds_root: Path, revision: str, output: Path) -> tuple[Path, dict]:
    revision = commit(revision)
    if git(root, "rev-parse", revision + "^{commit}").decode().strip() != revision:
        raise ValueError("The file AppHost catalog requires a canonical current source commit.")
    if git(root, "rev-parse", "HEAD").decode().strip() != revision:
        raise ValueError("File AppHost catalog requires the actual candidate checkout.")
    output.mkdir(parents=True, exist_ok=False)
    binding_path = output / "builds-tuple.json"
    binding_path.write_bytes(git(root, "show", revision + ":eng/builds-tuple.json"))
    binding = load_binding(binding_path)
    producer = binding["producer"]
    source = producer["sourceRevision"]
    entry = git(root, "ls-tree", revision, "--", "references/Hexalith.Builds").decode().split()
    if len(entry) != 4 or entry[:3] != ["160000", "commit", source]:
        raise ValueError("Current file AppHost catalog differs from its recorded producer gitlink.")
    path = producer["catalog"]["path"]
    raw = git(builds_root, "show", source + ":" + path)
    actual_blob = git(builds_root, "rev-parse", source + ":" + path).decode().strip()
    actual_hash = hashlib.sha256(raw).hexdigest()
    if actual_blob != producer["catalog"]["blob"] or actual_hash != producer["catalog"]["sha256"]:
        raise ValueError("Current file AppHost catalog differs from its authenticated Git blob.")
    catalog = output / "Directory.Packages.props"
    catalog.write_bytes(raw)
    receipt = {"currentSourceRevision": revision, "buildsSourceRevision": source,
               "catalogBlob": actual_blob, "catalogSha256": actual_hash,
               "scope": "Candidate file AppHost pin alignment only",
               "candidateAcceptance": False, "runtimeAcceptance": False}
    (output / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")
    return catalog, receipt
