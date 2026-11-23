"""Run-owned ordinary Platform exports with explicit Git revision and exact working overlays."""
import argparse
import configparser
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import tarfile

DIRECTORIES = {"src", "tests", "eng", "DaprComponents", "config", "schemas", ".config"}
OUTPUTS = {"bin", "obj", "__pycache__", "artifacts", ".netcoredbg_hist"}
# The verifier binds the selected EventStore producer and the Works and Platform
# consumers separately. These additional source dependencies supply their build graph.
SOURCE_DEPENDENCIES = {
    "Hexalith.Builds": "Props/Directory.Packages.props",
    "Hexalith.Commons": "src/libraries/Hexalith.Commons.UniqueIds",
    "Hexalith.McpCli": "src/Hexalith.McpCli.Abstractions",
    "Hexalith.Memories": "src/Hexalith.Memories.Contracts/Hexalith.Memories.Contracts.csproj",
    "Hexalith.Parties": "src/Hexalith.Parties.Client",
    "Hexalith.PolymorphicSerializations": "src/libraries/Hexalith.PolymorphicSerializations",
    "Hexalith.Tenants": "src/Hexalith.Tenants.Contracts",
}
# Inputs referenced from outside src in these modules' MSBuild files.
DEPENDENCY_BUILD_RESOURCES = {
    "Hexalith.Builds": ("Github/lib", "schemas", "Tools/write-platform-version-catalog.ps1", "icon.png"),
    "Hexalith.Parties": ("scripts/PartiesRuntimeBuildOutput.targets",),
    "Hexalith.Tenants": ("tools/CatalogArtifactVerifier",),
}
VERIFIER_DOCUMENTS = {"docs/ext-host-1-agents-composition.md",
    "_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md"}
# The Composition graph embeds this plan even though adjacent qualification tools are unrelated.
COMPILED_CANONICAL_INPUTS = {"eng/ext-secrets-1/public-joint-plan.json"}
# Except for the compiled inputs above, none is imported, invoked, read or compiled by
# verify_works_host, either Agents lane, the Platform graphs, or the Works proof fixture graph.
# Keep the complete immutable export binding, and report their current canonical drift.
UNRELATED_CANONICAL_INPUTS = {"schemas/hexalith.recovery-hook.v1.json"}
UNRELATED_CANONICAL_PREFIXES = ("eng/recovery-contract/", "eng/ext-secrets-1/",
    "tests/Hexalith.Platform.Secrets.Runtime.Tests/")


def unrelated(name):
    return name not in COMPILED_CANONICAL_INPUTS and (
        name in UNRELATED_CANONICAL_INPUTS or name.startswith(UNRELATED_CANONICAL_PREFIXES))


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


def declared_paths(root):
    config = configparser.ConfigParser()
    config.read(root / ".gitmodules")
    names = [config[section]["path"] for section in config.sections() if section.startswith('submodule "')]
    if any(len(Path(name).parts) != 2 or Path(name).parts[0] != "references" or ".." in Path(name).parts for name in names):
        raise ValueError("Snapshot dependencies must be root-declared references")
    return sorted(names)


def declared_links(root):
    return {name: str((root / name).resolve()) for name in declared_paths(root) if (root / name).is_dir()}


def dependency_source_files(root, module):
    """Read working bytes including new source and imports, pruning generated output before descent."""
    entries = [p for p in root.iterdir() if p.suffix.lower() in (".props", ".targets", ".globalconfig")
               or p.name.lower() in (".editorconfig", "global.json", "nuget.config", "stylecop.json")]
    entries.extend([root / "src", root / "Props"])
    for name in DEPENDENCY_BUILD_RESOURCES.get(module, ()):
        path = root / name
        if path.parent.resolve() != path.parent:
            raise ValueError("Dependency build input cannot traverse a symbolic link: " + str(path))
        entries.append(path)
    files = {}
    while entries:
        path = entries.pop()
        if path.is_symlink():
            raise ValueError("Dependency build input cannot be a symbolic link: " + str(path))
        if path.is_dir():
            entries.extend(child for child in path.iterdir() if child.name not in OUTPUTS)
        elif path.is_file():
            with path.open("rb") as stream:
                files[path.relative_to(root).as_posix()] = hashlib.file_digest(stream, "sha256").hexdigest()
    return dict(sorted(files.items()))


def build_dependency(root, module, direct):
    """Resolve configured roots and the Works direct-first / Platform sibling-first checkout layouts."""
    configured = os.environ.get(module.replace(".", "") + "Root", "").strip()
    if configured:
        return (root / configured).resolve()
    marker = SOURCE_DEPENDENCIES[module]
    works = (root / "Hexalith.Works.slnx").is_file()
    if not works and module == "Hexalith.Tenants":
        marker += "/Hexalith.Tenants.Contracts.csproj"
    sibling = root.parent / module
    if (sibling / marker).exists() and not (works and (direct / marker).exists()):
        return sibling.resolve()
    return direct


def dependency_inputs(root, *, frozen_links=False):
    """Bind declared dependencies plus any distinct source selected by the run's MSBuild environment."""
    root = Path(root).resolve()
    sources = {}
    for name in declared_paths(root):
        module = Path(name).name
        if module not in SOURCE_DEPENDENCIES:
            continue
        direct = (root / name).resolve()
        selected = direct if frozen_links else build_dependency(root, module, direct)
        candidates = {name: direct} if direct.is_dir() else {}
        if selected != direct:
            candidates["build/" + module] = selected
        for key, checkout in candidates.items():
            files = dependency_source_files(checkout, module)
            if module == "Hexalith.Builds" and checkout == selected and "Props/Directory.Packages.props" not in files:
                raise ValueError("Missing selected dependency catalog: " + str(checkout))
            # A filesystem export cannot inherit Git HEAD from an enclosing checkout.
            commit = revision(checkout) if (checkout / ".git").exists() else None
            if files or commit:
                sources[key] = {"root": str(checkout), "commit": commit, "sourceFiles": files}
    return sources


def create(origin, root, manifest, upstream_origin=None):
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
              "dependencyLinks": links, "dependencyInputs": dependency_inputs(origin, frozen_links=True)}
    if upstream_origin is not None:
        upstream = Path(upstream_origin).resolve()
        if origin_files(upstream) != files or revision(upstream) != commit:
            raise ValueError("Upstream owner revision or ordinary overlay differs from the isolated origin")
        record["upstreamOrigin"] = {"root": str(upstream), "commit": commit}
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
    upstream_observation = None
    if "upstreamOrigin" in record:
        upstream = Path(record["upstreamOrigin"]["root"])
        selected_commit = record["upstreamOrigin"]["commit"]
        if selected_commit != record["originCommit"] or git(upstream, "rev-parse", selected_commit + "^{commit}").decode().strip() != selected_commit:
            raise ValueError("Snapshot upstream owner revision differs")
        upstream_files = origin_files(upstream)
        if relevant(upstream_files) != relevant(record["sourceFiles"]):
            raise ValueError("Snapshot upstream owner ordinary source inputs changed")
        upstream_observation = {"root": str(upstream), "selectedCommit": selected_commit, "observedCommit": revision(upstream),
                                "currentRelevantSourceFiles": relevant(upstream_files)}
    if declared_links(origin) != record["dependencyLinks"]:
        raise ValueError("Snapshot origin dependency declarations or targets changed")
    actual = {path.relative_to(root).as_posix(): str(path.resolve()) for path in (root / "references").iterdir()} if (root / "references").exists() else {}
    if actual != record["dependencyLinks"] or any(not (root / name).is_symlink() for name in actual):
        raise ValueError("Snapshot dependency links changed")
    # Both verified link maps select the same directories. Read their bytes once.
    if dependency_inputs(root, frozen_links=True) != record["dependencyInputs"]:
        raise ValueError("Snapshot dependency catalog, source inputs or revision changed")
    return {"manifest": str(manifest), "manifestSha256": digest(manifest.read_bytes()),
            "originCommit": record["originCommit"], "observedOriginCommit": revision(origin),
            "root": str(root), "originRoot": str(origin), "inventorySha256": record["inventorySha256"],
            "sourceFiles": record["sourceFiles"], "dependencyLinks": record["dependencyLinks"],
            "dependencyInputs": record["dependencyInputs"],
            "upstreamOrigin": upstream_observation,
            "currentRelevantSourceFiles": relevant(current),
            "unrelatedOriginInputDrift": {name: {"exportSha256": record["sourceFiles"].get(name), "currentSha256": current.get(name)}
                for name in sorted(current.keys() | record["sourceFiles"].keys())
                if unrelated(name) and current.get(name) != record["sourceFiles"].get(name)}}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--origin", required=True, type=Path)
    parser.add_argument("--root", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--upstream-origin", type=Path, help="Original owner checkout whose exact ordinary overlay the isolated origin preserves")
    args = parser.parse_args()
    create(args.origin, args.root, args.manifest, args.upstream_origin)
