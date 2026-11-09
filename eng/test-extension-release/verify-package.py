"""Validate the one fixture archive without extracting or accepting source outputs."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
import zipfile
import re

PACKAGE = "Hexalith.Platform.TestExtension"
LIBRARY = "lib/net10.0/" + PACKAGE + ".dll"
REPOSITORY = "https://github.com/Hexalith/Hexalith.Platform"


DECLARATION = "build/hexalith.module-manifest.v2.json"
PRODUCER = "build/fixture-producer.json"
ROOT = Path(__file__).resolve().parents[2]
FIXTURE = "tests/fixtures/Hexalith.Platform.TestExtension/"


def approved_documents(source):
    if re.fullmatch(r"[a-f0-9]{40}", source) is None:
        raise ValueError("Require an exact immutable fixture source revision")
    return {member: subprocess.check_output(["git", "show", source + ":" + FIXTURE + name], cwd=ROOT, timeout=30)
            for member, name in [(DECLARATION, "hexalith.module-manifest.v2.json"), (PRODUCER, "fixture-producer.json")]}


def inspect(path, version, source, *, approved=None):
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)) or any(name.startswith("/") or ".." in Path(name).parts for name in names):
            raise ValueError("Unsafe or duplicate fixture archive members")
        specifications = [name for name in names if name.endswith(".nuspec")]
        if len(specifications) != 1:
            raise ValueError("Require one fixture package specification")
        metadata = ET.fromstring(archive.read(specifications[0]))
        values = {node.tag.rsplit("}", 1)[-1]: node.text for node in metadata.iter()}
        repository = next((node.attrib for node in metadata.iter() if node.tag.endswith("repository")), {})
        if values.get("id") != PACKAGE or values.get("version") != version or repository.get("commit") != source \
                or repository.get("type") != "git" or repository.get("url", "").removesuffix(".git") != REPOSITORY:
            raise ValueError("Fixture package/source identity differs")
        dependencies = [node.attrib for node in metadata.iter() if node.tag.endswith("dependency")]
        if len(dependencies) != 1 or dependencies[0].get("id") != "Hexalith.EventStore.DomainService":
            raise ValueError("Unexpected fixture package dependency closure")
        expected_documents = approved_documents(source) if approved is None else approved
        if set(expected_documents) != {DECLARATION, PRODUCER}:
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
                       "purpose": "signed-extension-host-qualification-only", "serviceEnrollment": False} \
                or declaration["schema"] != "hexalith.module-manifest.v2" or len(declaration["modules"]) != 1 \
                or module["identity"] != {"moduleId": "platformtestextension", "servers": [], "dependencies": ["eventstore", "tenants"]} \
                or module["runtime"]["eventStoreExtensions"] != [{"packageId": PACKAGE, "extensionId": "platform-test", "extensionApiMajor": 1,
                    "configurationInputs": [{"key": "PlatformTestExtension:Marker", "source": "configuration", "reference": "platform-test-marker"}]}]:
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
            "producer": {"path": PRODUCER, "sha256": bodies[PRODUCER], "publisherRepository": "Hexalith/Hexalith.Platform"}}


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
        if any(value["type"] == "project" for value in assets["libraries"].values()):
            raise ValueError("The fixture was built against source project dependencies")
        signed = {}
        for name, library in assets["libraries"].items():
            if not name.startswith("Hexalith.EventStore."):
                continue
            if name.split("/", 1)[1] != "4.2.0":
                raise ValueError("The fixture requires the exact measured SDK4.2 package closure")
            archives = [Path(folder) / library["path"] / (library["path"].replace("/", ".") + ".nupkg")
                        for folder in assets["packageFolders"]]
            found = [path for path in archives if path.is_file()]
            if len(found) != 1:
                raise ValueError("The actual restored SDK archive is unavailable: " + name)
            verification = subprocess.run(["dotnet", "nuget", "verify", str(found[0]), "--all"],
                                          capture_output=True, text=True, timeout=60, check=True)
            with zipfile.ZipFile(found[0]) as sdk:
                specification = ET.fromstring(sdk.read(next(member for member in sdk.namelist() if member.endswith(".nuspec"))))
                repository = specification.find(".//{*}repository")
                if repository is None or repository.attrib.get("commit") != "aa16227bd07f0564a3026d619a5abd973e02656f" \
                        or repository.attrib.get("url", "").rstrip("/").removesuffix(".git") != "https://github.com/Hexalith/Hexalith.EventStore":
                    raise ValueError("The actual fixture SDK source differs from the measured release")
            signed[name] = {"sha256": hashlib.sha256(found[0].read_bytes()).hexdigest(),
                            "signatureVerification": verification.stdout}
        if not any(name.startswith("Hexalith.EventStore.DomainService/") for name in signed):
            raise ValueError("The published DomainService SDK was not resolved")
        args.archive.with_name("signed-sdk-identity.json").write_text(json.dumps(signed, indent=2) + "\n")
        args.archive.with_name("package-identity.json").write_text(json.dumps(identity, indent=2) + "\n")
    print(json.dumps(identity, sort_keys=True))


if __name__ == "__main__":
    main()
