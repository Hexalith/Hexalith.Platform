"""Restore exact historical topology sources without restoring an active Works-owned host."""
import configparser
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import tarfile

REQUIRED = {'.gitmodules', 'Hexalith.Works.slnx', 'global.json', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props',
            'src/Hexalith.Works.AppHost/Hexalith.Works.AppHost.csproj', 'src/Hexalith.Works.AppHost/Program.cs',
            'src/Hexalith.Works.AppHost/ProjectMetadataPaths.cs', 'src/Hexalith.Works.ServiceDefaults/Hexalith.Works.ServiceDefaults.csproj'}


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def declared_dependencies(path):
    configuration = configparser.ConfigParser()
    configuration.read(path)
    paths = [configuration[section]["path"] for section in configuration.sections()]
    if len(paths) != len(set(paths)) or any(not re.fullmatch(r"references/[A-Za-z0-9_.-]+", path) for path in paths):
        raise ValueError("Historical host dependencies must be root-declared submodules")
    return set(paths)


def git_file(works, commit, name):
    return subprocess.run(["git", "-C", str(works), "show", commit + ":" + name], check=True,
                          capture_output=True, timeout=30).stdout


def prepare(bundle, works, output, historical_file=git_file):
    bundle, works, output = Path(bundle), Path(works).resolve(), Path(output)
    manifest = json.loads((bundle / "manifest.json").read_text())
    commit = manifest["baselineCommit"]
    inventory = manifest["sourceFiles"]
    archive = bundle / "works-source.tar.gz"
    if not re.fullmatch(r"[0-9a-f]{40}", commit) or not REQUIRED.issubset(inventory) or len(inventory) > 1000 \
            or archive.stat().st_size > 2 * 1024 * 1024 or digest(archive) != manifest["archiveSha256"]:
        raise ValueError("Historical Works topology has no exact source binding")
    sources = {}
    directories = {str(parent) for name in inventory for parent in PurePosixPath(name).parents if str(parent) != "."}
    with tarfile.open(fileobj=io.BytesIO(archive.read_bytes()), mode="r:gz") as package:
        members = package.getmembers()
        if sum(member.size for member in members) > 2 * 1024 * 1024:
            raise ValueError("Historical source archive exceeds its bounded input size")
        for member in members:
            path = PurePosixPath(member.name)
            if member.isdir() and member.name in directories:
                continue
            if not member.isfile() or path.is_absolute() or ".." in path.parts or str(path) != member.name \
                    or member.name in sources or member.name not in inventory or not 0 <= member.size <= 1024 * 1024:
                raise ValueError("Unsafe or unbound historical source archive member")
            sources[member.name] = package.extractfile(member).read()
    if set(sources) != set(inventory):
        raise ValueError("Historical source archive differs from its complete manifest")
    for name, content in sources.items():
        if hashlib.sha256(content).hexdigest() != inventory[name] or historical_file(works, commit, name) != content:
            raise ValueError("Historical topology differs from the recorded Works commit: " + name)
    output.mkdir(parents=True, exist_ok=False)
    for name, content in sources.items():
        destination = output / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(content)
    original_dependencies = declared_dependencies(output / ".gitmodules")
    current_dependencies = declared_dependencies(works / ".gitmodules")
    if not original_dependencies.issubset(current_dependencies):
        raise ValueError("A historical dependency is no longer root-declared by Works")
    links = {}
    for name in sorted(original_dependencies):
        source = works / name
        if source.is_dir():
            destination = output / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.symlink_to(source, target_is_directory=True)
            links[name] = str(source.resolve())
    evidence = {"root": str(output), "baselineCommit": commit, "archiveSha256": digest(archive),
                "manifestSha256": digest(bundle / "manifest.json"), "sourceFiles": inventory, "dependencyLinks": links}
    verify(evidence)
    return evidence


def verify(evidence):
    root = Path(evidence["root"])
    expected = evidence["sourceFiles"]
    inputs = [path for path in root.rglob("*") if not {"bin", "obj", "references"}.intersection(path.relative_to(root).parts)]
    if any(path.is_symlink() for path in inputs):
        raise ValueError("A historical source input became a symbolic link")
    actual = {str(path.relative_to(root)): digest(path) for path in inputs if path.is_file()}
    if actual != expected:
        raise ValueError("Historical topology source changed after qualification")
    links = evidence["dependencyLinks"]
    actual_links = {str(path.relative_to(root)) for path in (root / 'references').iterdir()}
    if actual_links != set(links) or any(not (root / name).is_symlink() or str((root / name).resolve()) != target
           for name, target in links.items()):
        raise ValueError("A root-declared historical dependency changed")
    return True
