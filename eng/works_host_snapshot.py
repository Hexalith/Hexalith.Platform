"""Run-owned ordinary Platform exports with explicit Git revision and exact working overlays."""
import argparse
import configparser
import hashlib
import io
import json
from pathlib import Path
import shutil
import subprocess
import tarfile

DIRECTORIES = {"src", "tests", "eng", "DaprComponents", "config", "schemas", ".config"}
OUTPUTS = {"bin", "obj", "__pycache__", "artifacts", ".netcoredbg_hist"}
VERIFIER_DOCUMENTS = {"docs/ext-host-1-agents-composition.md",
    "_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md"}
# Audited separately: none is imported, invoked, read or compiled by verify_works_host,
# either Agents lane, the compiled Platform graphs, or the Works proof fixture graph.
# Keep the complete immutable export binding, and report their current canonical drift.
UNRELATED_CANONICAL_INPUTS = {"schemas/hexalith.recovery-hook.v1.json"}
UNRELATED_CANONICAL_PREFIXES = ("eng/recovery-contract/", "eng/ext-secrets-1/",
    "tests/Hexalith.Platform.Secrets.Runtime.Tests/")


def unrelated(name):
    return name in UNRELATED_CANONICAL_INPUTS or name.startswith(UNRELATED_CANONICAL_PREFIXES)


def digest(value):
    return hashlib.sha256(value).hexdigest()


def selected(name):
    parts = Path(name).parts
    return bool(parts) and (len(parts) == 1 or parts[0] in DIRECTORIES or name in VERIFIER_DOCUMENTS) and not OUTPUTS.intersection(parts)


def git(root, *arguments):
    return subprocess.check_output(["git", "-C", str(root), *arguments])


def revision(root):
    if Path(git(root, "rev-parse", "--show-toplevel").decode().strip()).resolve() != root.resolve():
        raise ValueError("Snapshot origin must own its Git checkout")
    return git(root, "rev-parse", "HEAD").decode().strip()


def origin_files(root):
    names = git(root, "ls-files", "-z", "--cached", "--others", "--exclude-standard").decode().split("\0")
    result = {}
    for name in sorted(set(filter(selected, names))):
        path = root / name
        if path.is_symlink():
            raise ValueError("Ordinary snapshot source cannot be a link: " + name)
        if path.is_file():
            result[name] = digest(path.read_bytes())
    return result


def export_files(root):
    result = {}
    for path in root.rglob("*"):
        name = path.relative_to(root).as_posix()
        if selected(name):
            if path.is_symlink():
                raise ValueError("Ordinary snapshot source cannot be a link: " + name)
            if path.is_file():
                result[name] = digest(path.read_bytes())
    return result


def declared_links(root):
    config = configparser.ConfigParser()
    config.read(root / ".gitmodules")
    names = [config[section]["path"] for section in config.sections() if section.startswith('submodule "')]
    if any(len(Path(name).parts) != 2 or Path(name).parts[0] != "references" or ".." in Path(name).parts for name in names):
        raise ValueError("Snapshot dependencies must be root-declared references")
    return {name: str((root / name).resolve()) for name in sorted(names) if (root / name).is_dir()}


def dependency_inputs(root):
    """The selected catalog is read through a root dependency link, outside ordinary source inventory."""
    name = "references/Hexalith.Builds/Props/Directory.Packages.props"
    if "references/Hexalith.Builds" not in declared_links(root):
        return {}
    return {name: digest((root / name).read_bytes())}


def create(origin, root, manifest):
    origin, root, manifest = origin.resolve(), root.resolve(), manifest.resolve()
    if root.exists() or manifest.exists() or manifest.is_relative_to(root):
        raise ValueError("Snapshot root and adjacent manifest must be new, separate paths")
    commit = revision(origin)
    files = origin_files(origin)
    with tarfile.open(fileobj=io.BytesIO(git(origin, "archive", commit))) as archive:
        baseline = {item.name: digest(archive.extractfile(item).read()) for item in archive.getmembers()
                    if item.isfile() and selected(item.name)}
    links = declared_links(origin)
    root.mkdir(parents=True)
    for name, expected in files.items():
        target = root / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(origin / name, target)
        if digest(target.read_bytes()) != expected:
            raise ValueError("Origin changed while exporting: " + name)
    for name, target in links.items():
        link = root / name
        link.parent.mkdir(parents=True, exist_ok=True)
        link.symlink_to(target, target_is_directory=True)
    record = {"root": str(root), "originRoot": str(origin), "originCommit": commit,
              "sourceFiles": files, "inventorySha256": digest(json.dumps(files, sort_keys=True).encode()),
              "baseSourceFiles": baseline,
              "workingOverlays": {name: {"baseSha256": baseline.get(name), "exportSha256": files.get(name)}
                  for name in sorted(files.keys() | baseline.keys()) if files.get(name) != baseline.get(name)},
              "dependencyLinks": links, "dependencyInputs": dependency_inputs(origin)}
    manifest.write_text(json.dumps(record, indent=2) + "\n")
    verify(manifest, root)
    return record


def verify(manifest, root):
    """Metadata-only origin commits are observations; any relevant byte or dependency drift refuses."""
    manifest, root = Path(manifest).resolve(), Path(root).resolve()
    record = json.loads(manifest.read_text())
    if record["root"] != str(root):
        raise ValueError("Snapshot manifest selects a different execution root")
    origin = Path(record["originRoot"])
    # Reconstruct the selected full revision's exact baseline independently of mutable HEAD metadata.
    if git(origin, "rev-parse", record["originCommit"] + "^{commit}").decode().strip() != record["originCommit"]:
        raise ValueError("Snapshot origin revision must be a full canonical commit")
    with tarfile.open(fileobj=io.BytesIO(git(origin, "archive", record["originCommit"]))) as archive:
        baseline = {item.name: digest(archive.extractfile(item).read()) for item in archive.getmembers()
                    if item.isfile() and selected(item.name)}
    overlays = {name: {"baseSha256": baseline.get(name), "exportSha256": record["sourceFiles"].get(name)}
        for name in sorted(record["sourceFiles"].keys() | baseline.keys()) if record["sourceFiles"].get(name) != baseline.get(name)}
    if baseline != record["baseSourceFiles"] or overlays != record["workingOverlays"]:
        raise ValueError("Snapshot base revision or working overlays differ")
    if digest(json.dumps(record["sourceFiles"], sort_keys=True).encode()) != record["inventorySha256"]:
        raise ValueError("Snapshot inventory digest differs")
    current = origin_files(origin)
    relevant = lambda files: {name: value for name, value in files.items() if not unrelated(name)}
    if export_files(root) != record["sourceFiles"] or relevant(current) != relevant(record["sourceFiles"]):
        raise ValueError("Snapshot or current canonical ordinary source inputs changed")
    if declared_links(origin) != record["dependencyLinks"]:
        raise ValueError("Snapshot origin dependency declarations or targets changed")
    if dependency_inputs(origin) != record["dependencyInputs"] or dependency_inputs(root) != record["dependencyInputs"]:
        raise ValueError("Snapshot selected dependency catalog inputs changed")
    actual = {path.relative_to(root).as_posix(): str(path.resolve()) for path in (root / "references").iterdir()} if (root / "references").exists() else {}
    if actual != record["dependencyLinks"] or any(not (root / name).is_symlink() for name in actual):
        raise ValueError("Snapshot dependency links changed")
    return {"manifest": str(manifest), "manifestSha256": digest(manifest.read_bytes()),
            "originCommit": record["originCommit"], "observedOriginCommit": revision(origin),
            "root": str(root), "originRoot": str(origin), "inventorySha256": record["inventorySha256"],
            "sourceFiles": record["sourceFiles"], "dependencyLinks": record["dependencyLinks"],
            "dependencyInputs": record["dependencyInputs"],
            "currentRelevantSourceFiles": relevant(current),
            "unrelatedOriginInputDrift": {name: {"exportSha256": record["sourceFiles"].get(name), "currentSha256": current.get(name)}
                for name in sorted(current.keys() | record["sourceFiles"].keys())
                if unrelated(name) and current.get(name) != record["sourceFiles"].get(name)}}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--origin", required=True, type=Path)
    parser.add_argument("--root", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    args = parser.parse_args()
    create(args.origin, args.root, args.manifest)
