#!/usr/bin/env python3
"""Exercise the actual catalog reader against the generated owner package lock; grants no image, native or candidate acceptance.

The reader admits owner packages only through eng/owner-packages.lock.json: it checks each restored archive against
the lock contentHash and each loaded DLL against its lock-verified archive member, and records both identities (AR-12).
"""

import argparse
import base64
import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import xml.etree.ElementTree as ET
import zipfile

import verify_catalog_builder as consumer


LOCK = consumer.ROOT / "eng/owner-packages.lock.json"
OWNERS = [
    {"assembly": "Hexalith.Tenants.Contracts", "adapterAssembly": "Hexalith.Tenants.Server", "appId": "tenants"},
    {"assembly": "Hexalith.Memories.EventStore", "adapterAssembly": "Hexalith.Memories.EventStoreExtensions", "appId": "memories-domain-service"},
    {"assembly": "Hexalith.Parties.Contracts", "adapterAssembly": "Hexalith.Parties.EventStoreExtensions", "appId": "parties"},
]
# The reader's direct owner references; their versions come from the lock, never from this script.
REFERENCES = ("Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server", "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions",
              "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions")
# An older genuine Tenants release substitutes a correctly named body that the lock must refuse.
SUBSTITUTE = ("Hexalith.Tenants.Contracts", "7.0.5")


def locked_versions(lock):
    return {entry["id"]: entry["version"] for entry in lock["packages"]}


def qualify(args):
    lock = json.loads(LOCK.read_bytes())
    versions = locked_versions(lock)
    family = lambda prefix: {version for package, version in versions.items() if package.startswith(prefix)}
    sdk, tenants, memories, parties, mcp = (family(prefix) for prefix in (
        "Hexalith.EventStore.", "Hexalith.Tenants.", "Hexalith.Memories.", "Hexalith.Parties.", "Hexalith.McpCli."))
    assert all(len(selected) == 1 for selected in (sdk, tenants, memories, parties, mcp)), versions
    sdk, tenants, memories, parties, mcp = (selected.pop() for selected in (sdk, tenants, memories, parties, mcp))
    args.evidence.mkdir(parents=True, exist_ok=False)
    report = {"scope": "Owner package lock reader admission only; no image or native authority", "lockSha256": consumer.digest(LOCK),
              "candidateAcceptance": False, "runtimeAcceptance": False, "status": "running", "cases": []}
    result = args.evidence / "result.json"

    def save():
        result.write_text(json.dumps(report, indent=2) + "\n")

    shutil.copyfile(Path(__file__), args.evidence / "verifier-source.py")
    shutil.copyfile(LOCK, args.evidence / "owner-packages.lock.json")
    save()
    try:
        with tempfile.TemporaryDirectory(prefix="owner-lock-reader-") as temporary:
            work = Path(temporary)
            project = work / "Hexalith.Platform.CatalogBuilder.csproj"
            consumer.make_project(project, program=(consumer.ROOT / "src/Hexalith.Platform.CatalogBuilder/Program.cs").read_text())
            xml = ET.parse(project)
            items = ET.SubElement(xml.getroot(), "ItemGroup")
            for name in REFERENCES:
                ET.SubElement(items, "PackageReference", {"Include": name})
            ET.SubElement(items, "PackageDownload", {"Include": SUBSTITUTE[0], "Version": "[" + SUBSTITUTE[1] + "]"})
            xml.write(project, encoding="unicode")
            output = work / "output"
            consumer.execute(["dotnet", "build", str(project), "-c", "Release", "--output", str(output),
                              "-m:1", "-nr:false", "--disable-build-servers", "-warnaserror",
                              *consumer.properties(args.builds_root, sdk, tenants),
                              "-p:HexalithMemoriesVersion=" + memories, "-p:HexalithMcpCliVersion=" + mcp, "-p:HexalithPartiesVersion=" + parties],
                             args.evidence / "build.log")
            report["executedArtifacts"] = consumer.retain_execution(project, output, "reader", args.evidence, sdk, tenants)
            assets = json.loads((work / "obj/project.assets.json").read_text())
            shutil.copyfile(work / "obj/project.assets.json", args.evidence / "project.assets.json")
            folders = [str(Path(folder)) for folder in assets["packageFolders"]]
            owner_libraries = {key.partition("/")[0]: key.partition("/")[2] for key, library in assets["libraries"].items()
                               if library.get("type") == "package" and key.startswith(("Hexalith.EventStore.", "Hexalith.Tenants.",
                                                                                         "Hexalith.Memories.", "Hexalith.Parties.", "Hexalith.McpCli."))}
            # Every owner package the reader restored must be a lock entry at exactly its locked version.
            assert {package: versions.get(package) for package in owner_libraries} == owner_libraries, owner_libraries
            for package, version in owner_libraries.items():
                consumer.execute(["dotnet", "nuget", "verify", str(archive(folders, package, version)), "--all"], args.evidence / (package + "-signature.log"))

            def execute_case(name, *, owners=OWNERS, lock_document=None, lock_path=LOCK, package_folders=None, fault=None, substitute=False,
                             expected=False, refusal=None):
                destination = args.evidence / (name + "-output")
                destination.mkdir()
                if lock_document is not None:
                    lock_path = work / (name + ".lock.json")
                    lock_path.write_text(json.dumps(lock_document))
                request = {"generation": 1, "instanceId": "owner-lock-reader", "gatewayAppId": "eventstore", "outputDirectory": str(destination),
                           "ownerPackageLock": str(lock_path), "packageFolders": package_folders or folders, "owners": owners}
                path = args.evidence / (name + "-request.json")
                path.write_text(json.dumps(request, indent=2) + "\n")
                executable = output
                if fault or substitute:
                    executable = work / name
                    shutil.copytree(output, executable)
                    if fault:
                        with (executable / (fault + ".dll")).open("ab") as body:
                            body.write(b"\0")
                    if substitute:
                        package, version = SUBSTITUTE
                        shutil.copyfile(next(Path(folder) / package.lower() / version / "lib/net10.0" / (package + ".dll") for folder in folders
                                             if (Path(folder) / package.lower() / version / "lib/net10.0" / (package + ".dll")).is_file()),
                                        executable / (package + ".dll"))
                completed = consumer.execute(["dotnet", str(executable / "Hexalith.Platform.CatalogBuilder.dll"), str(path)],
                                             args.evidence / (name + ".log"), expected=0 if expected else 1)
                row = {"name": name, "exitCode": completed.returncode, "expectedSuccess": expected}
                if expected:
                    catalog = json.loads((destination / "catalog.json").read_text())
                    receipt = json.loads((destination / "receipt.json").read_text())
                    counts = {key: len(catalog[key]) for key in ["routes", "operations", "declarations", "idempotency"]}
                    private = len(owners) == 1
                    # Memories 4.1.2 adds one command and its idempotency policy to the full owner catalog.
                    assert counts == ({"routes": 24, "operations": 24, "declarations": 24, "idempotency": 13} if private
                                      else {"routes": 72, "operations": 72, "declarations": 72, "idempotency": 48}), counts
                    # The approved Tenants policy rides on the locked package, not on counts that 8.0.2 and 8.0.4 share.
                    audit = [o["eligibility"] for o in catalog["operations"] if o["routeId"] == "tenants-get-global-administrator-audit"]
                    assert audit == ["UI-only"], audit
                    assert receipt["ownerPackageLock"]["sha256"] == consumer.digest(lock_path)
                    records = {record["assembly"]: record for record in receipt["ownerPackages"]}
                    locked = {entry["id"]: entry for entry in json.loads(lock_path.read_bytes())["packages"]}
                    for assembly, record in records.items():
                        entry = locked[record["packageId"]]
                        assert (record["version"], record["contentHash"]) == (entry["version"], entry["contentHash"]), record
                        raw = Path(record["archive"]).read_bytes()
                        assert base64.b64encode(hashlib.sha512(raw).digest()).decode() == entry["contentHash"], record
                        with zipfile.ZipFile(record["archive"]) as package:
                            assert hashlib.sha256(package.read(record["archiveMember"])).hexdigest() == record["expectedSha256"], record
                        assert record["expectedSha256"] == record["loadedSha256"] == consumer.digest(Path(record["loadedPath"])), record
                    loaded = {Path(item["path"]).stem for item in receipt["loadedArtifacts"]}
                    assert {name for name in loaded if name.startswith(tuple(prefix for prefix in ("Hexalith.EventStore.", "Hexalith.Tenants.",
                        "Hexalith.Memories.", "Hexalith.Parties.", "Hexalith.McpCli.")))} <= set(records), loaded
                    row.update(counts=counts, ownerPackages=len(records), rootDigest=receipt["rootDigest"],
                               catalogSha256=consumer.digest(destination / "catalog.json"))
                else:
                    assert not list(destination.iterdir()), name
                    assert refusal in completed.stderr, completed.stderr
                    row["refusedBeforeOutput"] = True
                report["cases"].append(row)
                save()

            # Current pins: admitted with the AR-12 evidence of every loaded owner body.
            execute_case("current-pins-full-reader", expected=True)
            execute_case("current-pins-private-eventstore-reader", owners=OWNERS[:1], expected=True)
            # Unknown version: the selected Memories packages have no lock entry.
            missing = {**lock, "packages": [entry for entry in lock["packages"] if not entry["id"].startswith("Hexalith.Memories.")]}
            execute_case("unknown-version", lock_document=missing, refusal="no entry for Hexalith.Memories.")
            # Stale lock: the entry names another version.
            stale = {**lock, "packages": [{**entry, "version": "7.0.5"} if entry["id"] == "Hexalith.Tenants.Server" else entry for entry in lock["packages"]]}
            execute_case("stale-lock", lock_document=stale, refusal="names Hexalith.Tenants.Server 7.0.5, but " + tenants + " is selected")
            # Tampered archive: a package folder listed first holds different .nupkg bytes.
            tampered = work / "tampered-packages"
            original = archive(folders, "Hexalith.Parties.Contracts", parties)
            copy = tampered / original.relative_to(next(Path(folder) for folder in folders if original.is_relative_to(folder)))
            copy.parent.mkdir(parents=True)
            copy.write_bytes(original.read_bytes() + b"\0")
            execute_case("tampered-archive", package_folders=[str(tampered), *folders],
                         refusal="owner package Hexalith.Parties.Contracts " + parties + " archive has contentHash")
            execute_case("missing-lock", lock_path=work / "absent-owner-packages.lock.json", refusal="is missing or unreadable")
            # Substituted DLLs: changed bytes for every loaded owner body, and an older genuine body under the same name.
            for name in sorted(path.stem for path in output.glob("*.dll") if path.stem.startswith(("Hexalith.EventStore.", "Hexalith.Tenants.",
                    "Hexalith.Memories.", "Hexalith.Parties.", "Hexalith.McpCli."))):
                execute_case("changed-" + name, fault=name, refusal="loaded assembly '" + name + "' differs from its lock-verified package member")
            execute_case("older-genuine-tenants-body", substitute=True,
                         refusal="loaded assembly 'Hexalith.Tenants.Contracts' differs from its lock-verified package member")
            execute_case("duplicate-owner", owners=[*OWNERS, OWNERS[0]], refusal="without duplicate owner or adapter assemblies")
            execute_case("foreign-adapter", owners=[{**OWNERS[0], "adapterAssembly": "Hexalith.Foreign.Server"}, *OWNERS[1:]], refusal="Hexalith.Foreign.Server")
            for name, expected in report["executedArtifacts"]["files"].items():
                assert consumer.digest(output / name) == expected, name
            report.update(status="passed", caseCount=len(report["cases"]), originalReaderFilesUnchanged=True)
    except BaseException as error:
        report.update(status="failed", error=f"{type(error).__name__}: {error}")
        raise
    finally:
        save()
    print(json.dumps({"status": report["status"], "cases": report["caseCount"], "runtimeAcceptance": False}))


def archive(folders, package, version):
    name = package.lower() + "." + version.lower() + ".nupkg"
    return next(Path(folder) / package.lower() / version.lower() / name for folder in folders
                if (Path(folder) / package.lower() / version.lower() / name).is_file())


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--builds-root", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    qualify(parser.parse_args())
