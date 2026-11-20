"""Generate and enforce eng/owner-packages.lock.json, the NuGet SHA-512 content hashes of the owner packages
that the pinned catalog selects for Platform's package-mode surfaces.

Run `python3 eng/owner_packages_lock.py` after an owner, Parties or Builds version bump. It restores each surface's
owner references against the catalog read from the Builds gitlink, then writes the lock only after every owner
package in the restored closure passes `dotnet nuget verify --all` and its hash equals the nuget.org `packageHash`.
`--check` regenerates the lock in memory and refuses a committed lock that differs.

The `platform-package` profile builds the Parties Release from the pinned Parties source; that build's owner
package references are read from the Parties project closure at the recorded gitlink and locked as one more surface.
Run-built Parties bodies themselves are never locked.
"""
import argparse
import base64
import binascii
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

from builds_tool_lock import _get, content_hash, published_package_hash, verify_signature
from builds_tuple import ROOT, recorded_gitlink, resolve_builds_root, verify_catalog

LOCK = ROOT / "eng/owner-packages.lock.json"
# Owner packages carry these id prefixes; Composition admits each loaded one through the lock.
OWNER_PREFIXES = ("Hexalith.EventStore.", "Hexalith.Tenants.", "Hexalith.Memories.", "Hexalith.Parties.", "Hexalith.McpCli.")
# Each package-mode surface restores under its own project name, so the catalog applies that surface's view.
# These are the owner references across each surface's in-repository project closure in package mode (eng/test_owner_packages_lock.py
# checks them against the project files); restore resolves the transitive closure.
SURFACES = {
    "Hexalith.Platform.Composition": ("Hexalith.EventStore.Aspire", "Hexalith.EventStore.Client", "Hexalith.EventStore.Contracts",
                                      "Hexalith.EventStore.DomainService", "Hexalith.EventStore.ServiceDefaults",
                                      "Hexalith.Memories.Contracts", "Hexalith.Parties.Contracts", "Hexalith.Tenants.Contracts",
                                      "Hexalith.Tenants.Server"),
    # The routing catalog reader: its SDK references plus the declared owners' contract and adapter packages.
    "Hexalith.Platform.CatalogBuilder": ("Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService", "Hexalith.EventStore.ServiceDefaults",
                                         "Hexalith.EventStore.Server", "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server",
                                         "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions",
                                         "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions"),
    "Hexalith.Platform.EventStoreHost": ("Hexalith.EventStore.Client", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.Gateway",
                                         "Hexalith.EventStore.Server"),
}
# The run-built current owner: its owner package references come from this project's source closure.
RUN_BUILT_OWNER = ("Hexalith.Parties", "references/Hexalith.Parties", "src/Hexalith.Parties/Hexalith.Parties.csproj")
RESTORE_TIMEOUT = 900


def is_owner_package(package_id):
    return isinstance(package_id, str) and package_id.startswith(OWNER_PREFIXES)


def load_lock(path=LOCK):
    try:
        lock = json.loads(Path(path).read_bytes())
    except (OSError, ValueError) as error:
        raise ValueError("The owner package lock " + str(path) + " is missing or unreadable; run python3 eng/owner_packages_lock.py.") from error
    if not isinstance(lock, dict) or set(lock) != {"schemaVersion", "packages"} or lock["schemaVersion"] != 1 \
            or not isinstance(lock["packages"], list) or not lock["packages"]:
        raise ValueError("The owner package lock must be schema 1 with exactly schemaVersion and a nonempty packages list.")
    seen = set()
    for entry in lock["packages"]:
        if not isinstance(entry, dict) or set(entry) != {"id", "version", "contentHash"} \
                or not all(isinstance(entry[key], str) and entry[key] for key in entry) or entry["id"] in seen:
            raise ValueError("Each owner package lock entry needs one unique {id, version, contentHash}.")
        if not is_owner_package(entry["id"]):
            raise ValueError("The owner package lock names " + entry["id"] + ", which is not an owner package.")
        try:
            if len(base64.b64decode(entry["contentHash"], validate=True)) != 64:
                raise ValueError
        except (binascii.Error, ValueError) as error:
            raise ValueError("The owner package lock contentHash for " + entry["id"] + " is not a base64 SHA-512.") from error
        seen.add(entry["id"])
    return lock


def locked_entry(lock, package_id, version):
    """A missing entry or a stale version refuses, naming the package and both versions."""
    entries = [entry for entry in lock["packages"] if entry["id"] == package_id]
    if not entries:
        raise ValueError("The owner package lock has no entry for " + package_id + " " + version + "; run python3 eng/owner_packages_lock.py.")
    if entries[0]["version"] != version:
        raise ValueError("The owner package lock names " + package_id + " " + entries[0]["version"] + ", but " + version
                         + " is selected; run python3 eng/owner_packages_lock.py.")
    return entries[0]


def require_locked(entry, raw):
    observed = content_hash(raw)
    if observed != entry["contentHash"]:
        raise ValueError("Owner package " + entry["id"] + " " + entry["version"] + " has contentHash " + observed
                         + ", which differs from eng/owner-packages.lock.json.")
    return observed


def owner_closure(assets_documents):
    """Collect every restored owner package; one surface graph or two must agree on one version per id."""
    closure = {}
    for surface, assets in assets_documents.items():
        for key, library in assets["libraries"].items():
            package_id, _, version = key.partition("/")
            if not is_owner_package(package_id):
                continue
            if library.get("type") != "package":
                raise ValueError(surface + " restored owner " + key + " as " + str(library.get("type")) + ", not a package.")
            if not isinstance(library.get("path"), str):
                raise ValueError(surface + " restored owner " + key + " without its package path.")
            previous = closure.get(package_id)
            if previous is not None and previous["version"] != version:
                raise ValueError("Package-mode surfaces select " + package_id + " " + previous["version"] + " and " + version
                                 + "; the lock admits one version per owner package.")
            closure[package_id] = {"version": version, "path": library["path"]}
    if not closure:
        raise ValueError("The package-mode surfaces restored no owner packages.")
    return dict(sorted(closure.items()))


def run_built_owner_references(root=ROOT, owner=RUN_BUILT_OWNER):
    """Owner package references of the run-built Parties Release, read from its project closure at the recorded gitlink."""
    _, gitlink_path, project = owner
    checkout = Path(root) / gitlink_path
    entry = subprocess.check_output(["git", "ls-tree", "HEAD", "--", gitlink_path], cwd=root, text=True).split()
    # An uninitialized submodule directory would resolve to the owning Platform repository instead.
    if len(entry) != 4 or entry[1] != "commit" or not (checkout / ".git").exists() or subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=checkout, text=True).strip() != entry[2]:
        raise ValueError("The " + gitlink_path + " checkout differs from its recorded gitlink; initialize the pinned submodule first.")
    references = tuple(reference for reference in project_owner_references(checkout / project, checkout)
                       if not reference.startswith("Hexalith.Parties."))
    if not references:
        raise ValueError("The run-built owner " + project + " references no owner package.")
    return references


def project_owner_references(project, boundary):
    """Owner PackageReference includes across a project's in-repository ProjectReference closure, conditions ignored."""
    boundary = Path(boundary).resolve()
    pending, seen, references = [Path(project).resolve()], set(), set()
    while pending:
        path = pending.pop()
        if path in seen:
            continue
        if not path.is_relative_to(boundary):
            raise ValueError("The owner project " + str(path) + " lies outside " + str(boundary) + ".")
        seen.add(path)
        for item in ET.parse(path).getroot().iter():
            tag, include = item.tag.rpartition("}")[2], item.get("Include") or ""
            if tag == "ProjectReference" and "$(" not in include:
                pending.append((path.parent / include.replace("\\", "/")).resolve())
            elif tag == "PackageReference" and is_owner_package(include):
                references.add(include)
    return tuple(sorted(references))


def surfaces(root=ROOT):
    return {**SURFACES, RUN_BUILT_OWNER[0]: run_built_owner_references(root)}


def _surface_project(name, references):
    project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
    properties = ET.SubElement(project, "PropertyGroup")
    ET.SubElement(properties, "TargetFramework").text = "net11.0"
    ET.SubElement(properties, "IsPackable").text = "false"
    items = ET.SubElement(project, "ItemGroup")
    for reference in references:
        ET.SubElement(items, "PackageReference", Include=reference)
    return project


def restore_surfaces(catalog_body, work, surfaces=SURFACES, run=subprocess.run):
    """Restore each surface's owner references from nuget.org into one isolated package folder."""
    work = Path(work)
    # The catalog is the nearest Directory.Packages.props, so the SDK imports it before any project item.
    (work / "Directory.Packages.props").write_bytes(catalog_body)
    build = ET.Element("Project")
    properties = ET.SubElement(build, "PropertyGroup")
    for key, value in (("UseHexalithProjectReferences", "false"), ("CentralPackageTransitivePinningEnabled", "true"),
                       ("Configuration", "Release")):
        ET.SubElement(properties, key).text = value
    ET.ElementTree(build).write(work / "Directory.Build.props", encoding="utf-8", xml_declaration=True)
    (work / "nuget.config").write_text('<?xml version="1.0" encoding="utf-8"?>\n<configuration><packageSources><clear />'
                                       '<add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources></configuration>\n')
    packages = work / "packages"
    allowed = {"PATH", "HOME", "TMP", "TEMP", "TMPDIR", "DOTNET_ROOT", "DOTNET_ROOT_X64", "LANG", "LC_ALL",
               "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "NUGET_HTTP_CACHE_PATH", "XDG_CACHE_HOME", "XDG_DATA_HOME"}
    environment = {key: value for key, value in os.environ.items() if key in allowed}
    environment.update(NUGET_PACKAGES=str(packages), DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
    documents = {}
    for name, references in surfaces.items():
        directory = work / name
        directory.mkdir()
        project = directory / (name + ".csproj")
        ET.ElementTree(_surface_project(name, references)).write(project, encoding="utf-8", xml_declaration=True)
        result = run(["dotnet", "restore", str(project), "--configfile", str(work / "nuget.config"), "--packages", str(packages),
                      "-nodeReuse:false", "-p:NuGetAudit=false"], cwd=work, env=environment, capture_output=True, text=True,
                     timeout=RESTORE_TIMEOUT)
        if result.returncode:
            raise ValueError("Restoring the " + name + " owner references against the pinned catalog failed:\n" + result.stdout + result.stderr)
        documents[name] = json.loads((directory / "obj/project.assets.json").read_bytes())
    return documents, packages


def archive_path(packages, package_id, version):
    lower, normalized = package_id.lower(), version.lower()
    return Path(packages) / lower / normalized / (lower + "." + normalized + ".nupkg")


def lock_packages(closure, packages, get=_get, verify=verify_signature):
    """Return the verified lock document; nothing is written unless every package passes every check."""
    entries = []
    for package_id, restored in sorted(closure.items()):
        version = restored["version"]
        path = archive_path(packages, package_id, version)
        raw = path.read_bytes()
        # The lock hashes the exact archive bytes, as nuget.org packageHash does; NuGet's own restore-graph
        # sha512 excludes the repository signature, so it is not this identity.
        observed = content_hash(raw)
        with tempfile.TemporaryDirectory(prefix="platform-owner-lock-") as temporary:
            # Verify a private copy so the check reads exactly the hashed bytes.
            copy = Path(temporary) / path.name
            copy.write_bytes(raw)
            signature = verify(copy)
        if signature.returncode:
            raise ValueError("dotnet nuget verify --all refused " + package_id + " " + version + ":\n" + signature.stdout + signature.stderr)
        published = published_package_hash(version, package_id, get=get)
        if observed != published:
            raise ValueError("The " + package_id + " " + version + " bytes have contentHash " + observed
                             + ", but nuget.org packageHash is " + published + ".")
        entries.append({"id": package_id, "version": version, "contentHash": observed})
    return {"schemaVersion": 1, "packages": entries}


def pinned_catalog(root=ROOT):
    gitlink = recorded_gitlink(root)
    _, body, identity = verify_catalog(gitlink, resolve_builds_root(root))
    return body, identity


def generate(root=ROOT, *, restore=restore_surfaces, get=_get, verify=verify_signature):
    body, identity = pinned_catalog(root)
    selected = surfaces(root)
    with tempfile.TemporaryDirectory(prefix="platform-owner-surfaces-") as temporary:
        documents, packages = restore(body, Path(temporary), selected)
        lock = lock_packages(owner_closure(documents), packages, get=get, verify=verify)
    return lock, identity


def require_current(lock, expected):
    """--check: the committed lock must equal the regenerated one exactly."""
    if lock != expected:
        committed = {entry["id"]: entry for entry in lock["packages"]}
        regenerated = {entry["id"]: entry for entry in expected["packages"]}
        changes = sorted(set(committed) ^ set(regenerated)) + sorted(
            package_id for package_id in set(committed) & set(regenerated) if committed[package_id] != regenerated[package_id])
        if not changes:
            raise ValueError("eng/owner-packages.lock.json lists the regenerated entries in another order; run python3 eng/owner_packages_lock.py.")
        raise ValueError("eng/owner-packages.lock.json differs from the pinned catalog and nuget.org for: " + ", ".join(changes)
                         + "; run python3 eng/owner_packages_lock.py.")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="Refuse a committed lock that differs instead of writing one")
    parser.add_argument("--output", type=Path, default=LOCK)
    args = parser.parse_args()
    lock, identity = generate()
    if args.output.resolve() == LOCK.resolve():
        # The same Builds adoption advances the restore graph and the released deployment manifests.
        sync = resolve_builds_root(ROOT) / "Tools/ci/release_declarations.py"
        subprocess.run([sys.executable, str(sync), "--sync-platform", str(ROOT), *(["--check"] if args.check else [])], check=True)
    if args.check:
        require_current(load_lock(args.output), lock)
        print(json.dumps({"status": "current", "catalog": identity, "packages": len(lock["packages"])}))
        return
    args.output.write_text(json.dumps(lock, indent=2) + "\n")
    print(json.dumps({"status": "written", "catalog": identity, "packages": lock["packages"]}, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, subprocess.SubprocessError) as failure:
        sys.exit(str(failure))
