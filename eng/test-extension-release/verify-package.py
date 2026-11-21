"""Validate the one fixture archive without extracting or accepting source outputs."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile

PACKAGE = "Hexalith.Platform.TestExtension"
LIBRARY = "lib/net11.0/" + PACKAGE + ".dll"
REPOSITORY = "https://github.com/Hexalith/Hexalith.Platform"
SDK_PACKAGE = "Hexalith.EventStore.DomainService"
SDK_REPOSITORY = "https://github.com/Hexalith/Hexalith.EventStore"


DECLARATION = "build/hexalith.module-manifest.v2.json"
PRODUCER = "build/fixture-producer.json"
TEST_ENTRY = "build/qualification/qualify-host.py"
SOURCE_TEST_ENTRY = "tests/Hexalith.Platform.TestExtension.Qualification.Tests/Hexalith.Platform.TestExtension.Qualification.Tests.csproj"
NATIVE_SOURCE_FILES = {'tests/Hexalith.Platform.TestExtension.Qualification.Tests/Hexalith.Platform.TestExtension.Qualification.Tests.csproj': 'build/qualification/Hexalith.Platform.TestExtension.Qualification.Tests.csproj', 'tests/Hexalith.Platform.TestExtension.Qualification.Tests/ReadinessTests.cs': 'build/qualification/ReadinessTests.cs', 'eng/test-extension-release/qualify-host.py': 'build/qualification/qualify-host.py'}
SPECIFICATION = PACKAGE + ".nuspec"
NUGET_METADATA = {SPECIFICATION, "[Content_Types].xml", "_rels/.rels", "package/services/metadata/core-properties/nuget.psmdcp"}
ROOT = Path(__file__).resolve().parents[2]
FIXTURE = "tests/fixtures/Hexalith.Platform.TestExtension/"

sys.path.insert(0, str(ROOT / "eng"))
from current_tuple_source import read_selection
from owner_packages_lock import archive_path, locked_entry, owner_closure, require_locked


def committed_file(source, path):
    return subprocess.check_output(["git", "show", source + ":" + path], cwd=ROOT, timeout=30)


def approved_documents(source):
    if re.fullmatch(r"[a-f0-9]{40}", source) is None:
        raise ValueError("Require an exact immutable fixture source revision")
    paths = {DECLARATION: FIXTURE + "hexalith.module-manifest.v2.json", PRODUCER: FIXTURE + "fixture-producer.json",
             **{member: source for source, member in NATIVE_SOURCE_FILES.items()}}
    return {member: committed_file(source, path) for member, path in paths.items()}


def inspect(path, version, source, *, approved=None):
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)) or any(name.startswith("/") or ".." in Path(name).parts for name in names):
            raise ValueError("Unsafe or duplicate fixture archive members")
        required = {LIBRARY, LIBRARY.removesuffix(".dll") + ".xml", DECLARATION, PRODUCER, *NATIVE_SOURCE_FILES.values(), *NUGET_METADATA}
        if set(names) - {".signature.p7s"} != required:
            raise ValueError("Unexpected or missing fixture archive members")
        metadata = ET.fromstring(archive.read(SPECIFICATION))
        values = {node.tag.rsplit("}", 1)[-1]: node.text for node in metadata.iter()}
        repository = next((node.attrib for node in metadata.iter() if node.tag.endswith("repository")), {})
        if values.get("id") != PACKAGE or values.get("version") != version or repository.get("commit") != source \
                or repository.get("type") != "git" or repository.get("url", "").removesuffix(".git") != REPOSITORY:
            raise ValueError("Fixture package/source identity differs")
        dependencies = [node.attrib for node in metadata.iter() if node.tag.endswith("dependency")]
        if len(dependencies) != 1 or dependencies[0].get("id") != SDK_PACKAGE:
            raise ValueError("Unexpected fixture package dependency closure")
        expected_documents = approved_documents(source) if approved is None else approved
        if set(expected_documents) != {DECLARATION, PRODUCER, *NATIVE_SOURCE_FILES.values()}:
            raise ValueError("Require the exact fixture declaration and producer authority")
        for member, expected in expected_documents.items():
            if member not in names or archive.read(member) != expected:
                raise ValueError("Packaged fixture declaration or producer differs from immutable source")
        producer = json.loads(archive.read(PRODUCER))
        declaration = json.loads(archive.read(DECLARATION))
        module = declaration["modules"][0]
        if producer != {"schema": "hexalith.platform.test-extension-producer.v1",
                       "publisherRepository": "Hexalith/Hexalith.Platform", "packageId": PACKAGE,
                       "moduleId": "platformtestextension", "declaration": DECLARATION,
                       "extensionId": "platform-test", "extensionApiMajor": 1,
                       "purpose": "signed-extension-host-qualification-only", "serviceEnrollment": False,
                       "ownerRepositoryIdentity": "Hexalith.Platform",
                       "test": {"name": "eventstore-extension-package", "sourceEntry": SOURCE_TEST_ENTRY,
                                "files": {source: {"path": member, "sha256": hashlib.sha256(archive.read(member)).hexdigest()}
                                          for source, member in NATIVE_SOURCE_FILES.items()}}} \
                or declaration["schema"] != "hexalith.module-manifest.v2" or len(declaration["modules"]) != 1 \
                or module["identity"] != {"moduleId": "platformtestextension", "servers": [], "dependencies": ["eventstore", "tenants", "memories"]} \
                or module["runtime"]["eventStoreExtensions"] != [{"packageId": PACKAGE, "extensionId": "platform-test", "extensionApiMajor": 1,
                    "configurationInputs": [{"key": "PlatformTestExtension:Marker", "source": "configuration", "reference": "platform-test-marker"}]}] \
                or module.get("tests") != [{"name": "eventstore-extension-package", "executor": "mtp", "entry": SOURCE_TEST_ENTRY,
                    "reportFormat": "trx", "executionTimeoutSeconds": 120, "stopTimeoutSeconds": 90, "mode": "package"}]:
            raise ValueError("The Platform fixture producer/declaration identity is unsupported")
        body = archive.read(LIBRARY)
        if b"EventStoreExtensionAttribute" not in body or b"platform-test" not in body:
            raise ValueError("The fixture package was built without its extension identity")
        bodies = {name: hashlib.sha256(archive.read(name)).hexdigest() for name in names
                  if not name.endswith("/") and name != ".signature.p7s"}
    return {"packageId": PACKAGE, "version": version, "source": source, "repository": REPOSITORY,
            "archiveSha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "librarySha256": hashlib.sha256(body).hexdigest(), "bodies": bodies, "dependencies": dependencies,
            "declaration": {"path": DECLARATION, "sha256": bodies[DECLARATION], "moduleId": "platformtestextension"},
            "producer": {"path": PRODUCER, "sha256": bodies[PRODUCER], "publisherRepository": "Hexalith/Hexalith.Platform"},
            "test": {"sourceEntry": SOURCE_TEST_ENTRY, "files": {source: {"path": member, "sha256": bodies[member]}
                     for source, member in NATIVE_SOURCE_FILES.items()}}}


def verify_sdk(assets, dependencies, eventstore, owner_lock):
    if any(value["type"] == "project" for value in assets["libraries"].values()):
        raise ValueError("The fixture was built against source project dependencies")

    version = eventstore["version"]
    if dependencies[0].get("version") not in (version, f"[{version}, )"):
        raise ValueError(f"The fixture dependency must name the qualified {SDK_PACKAGE} {version}")

    closure = owner_closure({PACKAGE: assets})
    if SDK_PACKAGE not in closure:
        raise ValueError("The published DomainService SDK was not resolved")

    signed = {}
    for package_id, restored in closure.items():
        if not package_id.startswith("Hexalith.EventStore."):
            continue
        name = f"{package_id}/{restored['version']}"
        if restored["version"] != version:
            raise ValueError(f"The fixture restored {name}, but the qualified EventStore release is {version}")
        archives = [archive_path(folder, package_id, version) for folder in assets["packageFolders"]]
        found = [path for path in archives if path.is_file()]
        if len(found) != 1 or restored["path"] != f"{package_id.lower()}/{version}":
            raise ValueError("The actual restored SDK archive is unavailable: " + name)

        path = found[0]
        raw = path.read_bytes()
        content_hash = require_locked(locked_entry(owner_lock, package_id, version), raw)
        compile_assets = {member for target in assets["targets"].values() for member in target.get(name, {}).get("compile", {})}
        if package_id == SDK_PACKAGE and not any(Path(member).name == SDK_PACKAGE + ".dll" for member in compile_assets):
            raise ValueError("The published DomainService SDK has no selected compile assembly")
        runtime_assets = {member for target in assets["targets"].values() for member in target.get(name, {}).get("runtime", {})}

        with tempfile.TemporaryDirectory(prefix="platform-fixture-sdk-") as directory:
            copy = Path(directory) / path.name
            copy.write_bytes(raw)
            verification = subprocess.run(["dotnet", "nuget", "verify", str(copy), "--all"],
                                          capture_output=True, text=True, timeout=60, check=True)
        with zipfile.ZipFile(io.BytesIO(raw)) as sdk:
            nuspec = ET.fromstring(sdk.read(package_id + ".nuspec"))
            repository = nuspec.find(".//{*}repository")
            if repository is None or repository.attrib.get("commit") != eventstore["commit"] \
                    or repository.attrib.get("url", "").rstrip("/").removesuffix(".git") != SDK_REPOSITORY:
                raise ValueError("The actual fixture SDK source differs from the qualified release: " + name)
            for member in sorted(compile_assets | runtime_assets):
                if not member.endswith("/_._") and (path.parent / member).read_bytes() != sdk.read(member):
                    raise ValueError(f"The restored SDK body for {name}/{member} differs from its signed archive")
        signed[name] = {"sha256": hashlib.sha256(raw).hexdigest(), "contentHash": content_hash,
                        "source": eventstore["commit"], "signatureVerification": verification.stdout}
    return signed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    parser.add_argument("version")
    parser.add_argument("source")
    parser.add_argument("built_library", nargs="?", type=Path)
    parser.add_argument("--assets", type=Path)
    parser.add_argument("--receipt", type=Path)
    args = parser.parse_args()
    identity = inspect(args.archive, args.version, args.source)
    if args.receipt:
        if identity != json.loads(args.receipt.read_text()):
            raise ValueError("The qualified fixture archive changed")
    else:
        if args.built_library is None or args.assets is None \
                or hashlib.sha256(args.built_library.read_bytes()).hexdigest() != identity["librarySha256"]:
            raise ValueError("Packaged fixture differs from the tested Release library")
        assets = json.loads(args.assets.read_text())
        _, provenance = read_selection(ROOT, args.source)
        owner_lock = json.loads(committed_file(args.source, "eng/owner-packages.lock.json"))
        signed = verify_sdk(assets, identity["dependencies"], provenance["eventstore"], owner_lock)
        args.archive.with_name("signed-sdk-identity.json").write_text(json.dumps(signed, indent=2) + "\n")
        args.archive.with_name("package-identity.json").write_text(json.dumps(identity, indent=2) + "\n")
    print(json.dumps(identity, sort_keys=True))


if __name__ == "__main__":
    main()
