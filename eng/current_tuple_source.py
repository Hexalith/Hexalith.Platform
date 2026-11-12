"""Authenticate synchronized source selections separately from published package provenance."""
from __future__ import annotations

import hashlib
import re
from pathlib import Path
import subprocess

from legacy_tuple_source import PROVENANCE, commit, decode, git

BINDING = "eng/current-tuple-source.json"
MANIFEST = "module/hexalith.module-manifest.v2.json"
OWNERS = {"eventstore": "Hexalith.EventStore", "parties": "Hexalith.Parties",
          "tenants": "Hexalith.Tenants", "memories": "Hexalith.Memories"}
PACKAGES = {name: repository for name, repository in OWNERS.items() if name != "parties"}


def exists(root: Path, revision: str) -> bool:
    return bool(git(root, "ls-tree", revision, "--", BINDING).strip())


def digest(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def require_digest(value):
    if not isinstance(value, str) or re.fullmatch(r"[a-f0-9]{64}", value) is None:
        raise ValueError("Current tuple selection requires exact SHA256 identities.")


def read_selection(root: Path, revision: str) -> tuple[dict, dict]:
    """Validate immutable main-tree selections; initialization is checked separately."""
    binding = decode(git(root, "show", revision + ":" + BINDING))
    if set(binding) != {"schema", "ownerSources", "packageProvenanceSha256"} \
            or binding["schema"] != "hexalith.platform.current-tuple-source.v1" \
            or set(binding["ownerSources"]) != set(OWNERS):
        raise ValueError("Unsupported current tuple source binding.")
    for name, repository in OWNERS.items():
        owner = binding["ownerSources"][name]
        if set(owner) != {"sourceRevision", "declarationSha256"}:
            raise ValueError("Unsupported current owner source binding.")
        commit(owner["sourceRevision"])
        require_digest(owner["declarationSha256"])
        actual = git(root, "ls-tree", revision, "--", "references/" + repository).decode().split()
        if len(actual) != 4 or actual[:2] != ["160000", "commit"] or actual[2] != owner["sourceRevision"]:
            raise ValueError("Current owner gitlink differs from the committed source binding: " + repository)
    require_digest(binding["packageProvenanceSha256"])
    raw = git(root, "show", revision + ":" + PROVENANCE)
    if digest(raw) != binding["packageProvenanceSha256"]:
        raise ValueError("Package provenance differs from the committed current tuple binding.")
    provenance = decode(raw)
    if set(provenance) != set(PACKAGES):
        raise ValueError("Unsupported current package provenance owners.")
    for name, repository in PACKAGES.items():
        item = provenance[name]
        if set(item) != {"repository", "version", "commit", "source", "sha256"} \
                or item["repository"] != repository or item["source"] != MANIFEST \
                or not isinstance(item["version"], str) or re.fullmatch(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", item["version"]) is None:
            raise ValueError("Unsupported current package release provenance: " + repository)
        commit(item["commit"])
        require_digest(item["sha256"])
        snapshot = "src/Hexalith.Platform.Composition/Declarations/" + name + ".json"
        if digest(git(root, "show", revision + ":" + snapshot)) != item["sha256"]:
            raise ValueError("Package declaration snapshot differs from its recorded release provenance: " + repository)
    return binding, provenance


def verify_companion_descriptors(root: Path, current: str, historical: str):
    """Both descriptor surfaces remain finite source selectors, never unchecked exemptions."""
    from legacy_tuple_source import resolve as resolve_legacy
    from pre_extension_tuple_source import resolve as resolve_pre_extension
    for revision in [current, historical]:
        for resolve in [resolve_legacy, resolve_pre_extension]:
            recorded = resolve(root, revision)
            git(root, "cat-file", "-e", recorded + "^{commit}")


def verify_package_release_identities(root: Path, revision: str) -> dict:
    """Authenticate package release tags and declaration blobs in actual owner Git objects."""
    _, provenance = read_selection(root, revision)
    for name, item in provenance.items():
        repository = item["repository"]
        owner = root / "references" / repository
        try:
            source = git(owner, "rev-parse", "refs/tags/v" + item["version"] + "^{commit}").decode().strip()
            manifest = git(owner, "show", item["commit"] + ":" + MANIFEST)
        except subprocess.CalledProcessError as error:
            raise ValueError("The selected package release tag or declaration is unavailable: " + repository) from error
        if source != item["commit"] or digest(manifest) != item["sha256"]:
            raise ValueError("Package provenance differs from the actual release tag or declaration blob: " + repository)
    return {"packages": provenance, "qualificationCredit": False}


def verify_checkout(root: Path, revision: str) -> dict:
    """Require the synchronized current owner checkouts and their exact committed manifests."""
    binding, _ = read_selection(root, revision)
    if git(root, "rev-parse", "HEAD").decode().strip() != revision:
        raise ValueError("Current tuple owner validation requires its actual committed checkout.")
    for name, repository in OWNERS.items():
        selected = binding["ownerSources"][name]
        owner = root / "references" / repository
        try:
            source = git(owner, "rev-parse", "HEAD").decode().strip()
            manifest = git(owner, "show", source + ":" + MANIFEST)
            # Keep the canonical blob hash; permit only Git's exact declared checkout form as an alternative.
            checkout_manifest = git(owner, "cat-file", "--filters", source + ":" + MANIFEST)
            actual = (owner / MANIFEST).read_bytes()
        except (OSError, subprocess.CalledProcessError) as error:
            raise ValueError("The selected current owner or declaration is unavailable: " + repository) from error
        if source != selected["sourceRevision"] or digest(manifest) != selected["declarationSha256"] or actual not in (manifest, checkout_manifest):
            raise ValueError("Current owner checkout or declaration differs from its committed source binding: " + repository)
    verify_package_release_identities(root, revision)
    return {"owners": binding["ownerSources"], "qualificationCredit": False}


def create_binding(root: Path, revision: str) -> dict:
    """Read Git objects only; owner worktree files never supply binding authority."""
    owners = {}
    for name, repository in OWNERS.items():
        source = commit(git(root, "rev-parse", revision + ":references/" + repository).decode().strip())
        manifest = git(root / "references" / repository, "show", source + ":" + MANIFEST)
        owners[name] = {"sourceRevision": source, "declarationSha256": digest(manifest)}
    return {"schema": "hexalith.platform.current-tuple-source.v1", "ownerSources": owners,
            "packageProvenanceSha256": digest(git(root, "show", revision + ":" + PROVENANCE))}
