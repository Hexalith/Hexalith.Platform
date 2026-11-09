#!/usr/bin/env python3
"""Measure the signed SDK4.2/T705/M25/P310 no-extension reader before tuple adoption; no service or host admission credit."""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile
import urllib.request

import verify_catalog_builder as consumer


BASE = "private-eventstore-package-test-4.2.0-tenants-7.0.5"
FULL = "parties-3.1.0-eventstore-4.2.0-tenants-7.0.5-memories-3.0.25-mcpcli-2.0.1"
TENANTS = {"assembly": "Hexalith.Tenants.Contracts", "adapterAssembly": "Hexalith.Tenants.Server", "appId": "tenants"}
OWNERS = [TENANTS,
          {"assembly": "Hexalith.Memories.EventStore", "adapterAssembly": "Hexalith.Memories.EventStoreExtensions", "appId": "memories-domain-service"},
          {"assembly": "Hexalith.Parties.Contracts", "adapterAssembly": "Hexalith.Parties.EventStoreExtensions", "appId": "parties"}]


def qualify(args):
    args.evidence.mkdir(parents=True, exist_ok=False)
    report = {"scope": "SDK-only public owner reader; no runner admission or service allocation",
              "candidateAcceptance": False, "status": "running", "cases": []}
    result_path = args.evidence / "result.json"
    shutil.copyfile(Path(__file__), args.evidence / "verifier-source.py")
    shutil.copyfile(Path(consumer.__file__), args.evidence / "consumer-helper-source.py")

    def save():
        result_path.write_text(json.dumps(report, indent=2) + "\n")

    save()
    try:
        # These are the same authenticated declaration bodies consumed by the production domain-owner selector.
        declarations = [
            ("src/Hexalith.Platform.Composition/Declarations/tenants.json", "1588f3428ec29296c452fdd35bf95146142315aad3ad8ceb934e7a791f1fb522"),
            ("src/Hexalith.Platform.Composition/Declarations/memories.json", "56f6dc8134d3f7cea06910e923a4736a450e00241e8e4a565ac9226897881840"),
            ("https://raw.githubusercontent.com/Hexalith/Hexalith.Parties/50904cc4ba53c9b0b2c082d5872a0d276be8f5c3/module/hexalith.module-manifest.v2.json", "d9c193b0e7b61601b8fcceb906b5dafe4769a1b9d7538a6ce164b4aa0840b348"),
        ]
        for owner, (relative, expected) in zip(OWNERS, declarations):
            if relative.startswith("https://raw.githubusercontent.com/Hexalith/Hexalith.Parties/"):
                path = args.evidence / "published-parties-declaration.json"
                with urllib.request.urlopen(relative, timeout=45) as response:
                    path.write_bytes(response.read())
            else:
                path = consumer.ROOT / relative
            assert consumer.digest(path) == expected, relative
            module = json.loads(path.read_text())["modules"][0]
            domain = next(server for server in module["identity"]["servers"] if server["id"] == "domain")
            assert owner["appId"] == domain["appId"], (owner, domain)
        report["ownerBindingsMatchActualDeclaredDomainAppIds"] = True
        with tempfile.TemporaryDirectory(prefix="public-owner-cohort-") as temporary:
            work = Path(temporary)
            project = work / "Hexalith.Platform.CatalogBuilder.csproj"
            consumer.make_project(project, program=(consumer.ROOT / "src/Hexalith.Platform.CatalogBuilder/Program.cs").read_text())
            xml = ET.parse(project)
            items = ET.SubElement(xml.getroot(), "ItemGroup")
            for name in ["Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server", "Hexalith.Memories.Contracts",
                         "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions", "Hexalith.Parties.Contracts",
                         "Hexalith.Parties.EventStoreExtensions", "Hexalith.McpCli.Abstractions"]:
                ET.SubElement(items, "PackageReference", {"Include": name})
            xml.write(project, encoding="unicode")
            output = work / "output"
            consumer.execute(["dotnet", "build", str(project), "-c", "Release", "--output", str(output),
                              "-m:1", "-nr:false", "--disable-build-servers", "-warnaserror",
                              *consumer.properties(args.builds_root, "4.2.0", "7.0.5"),
                              "-p:HexalithMemoriesVersion=3.0.25", "-p:HexalithMcpCliVersion=2.0.1",
                              "-p:HexalithPartiesVersion=3.1.0"], args.evidence / "build.log")
            report["executedArtifacts"] = consumer.retain_execution(project, output, "reader", args.evidence, "4.2.0", "7.0.5")
            retained = args.evidence / "reader-output"
            shutil.copytree(output, retained)
            assets = json.loads((work / "obj/project.assets.json").read_text())
            shutil.copyfile(work / "obj/project.assets.json", args.evidence / "project.assets.json")
            target = next(iter(assets["targets"].values()))
            external = []
            for identity, dependency in target.items():
                if not identity.startswith("ModelContextProtocol"):
                    continue
                name, version = identity.split("/", 1)
                assert version == "2.4.0", identity
                archives = [Path(folder) / name.lower() / version / (identity.replace("/", ".").lower() + ".nupkg")
                            for folder in assets["packageFolders"]]
                archive = next(path for path in archives if path.is_file())
                consumer.execute(["dotnet", "nuget", "verify", str(archive), "--all"], args.evidence / (name + "-signature.log"))
                with zipfile.ZipFile(archive) as package:
                    for member in dependency.get("runtime", {}):
                        body = output / Path(member).name
                        assert body.read_bytes() == package.read(member), identity
                        external.append({"package": identity, "archiveSha256": consumer.digest(archive),
                                         "body": body.name, "sha256": consumer.digest(body)})
            report["externalSupportingBodies"] = external
            # Every Hexalith reader package must be signed, source-bound, and byte-identical to its output body.
            expected_sources = {"Hexalith.EventStore.": "aa16227bd07f0564a3026d619a5abd973e02656f",
                                "Hexalith.Tenants.": "d88aef6b52d7e8a2e2d81132b1d76b7e1d1f0f35",
                                "Hexalith.Memories.": "246157a7ff8e2dcd8d356c833a77a02514490d2f",
                                "Hexalith.Parties.": "50904cc4ba53c9b0b2c082d5872a0d276be8f5c3"}
            measured = []
            for identity, dependency in target.items():
                if not identity.startswith("Hexalith."):
                    continue
                name, version = identity.split("/", 1)
                assert dependency["type"] == "package", identity
                archives = [Path(folder) / name.lower() / version / (identity.replace("/", ".").lower() + ".nupkg")
                            for folder in assets["packageFolders"]]
                archive = next(path for path in archives if path.is_file())
                consumer.execute(["dotnet", "nuget", "verify", str(archive), "--all"], args.evidence / (name + "-signature.log"))
                with zipfile.ZipFile(archive) as package:
                    metadata = ET.fromstring(package.read(name + ".nuspec")).find("{*}metadata")
                    assert metadata.find("{*}id").text == name and metadata.find("{*}version").text == version
                    repository = metadata.find("{*}repository")
                    expected = next((source for prefix, source in expected_sources.items() if name.startswith(prefix)), None)
                    if expected is not None:
                        assert repository.attrib["commit"] == expected, (identity, repository.attrib)
                        owner = next(prefix.removesuffix(".") for prefix, source in expected_sources.items() if source == expected)
                        assert repository.attrib["url"].rstrip("/").removesuffix(".git") == "https://github.com/Hexalith/" + owner
                    bodies = []
                    for member in dependency.get("runtime", {}):
                        body = output / Path(member).name
                        assert body.read_bytes() == package.read(member), identity
                        bodies.append({"name": body.name, "sha256": consumer.digest(body)})
                    measured.append({"package": identity, "archiveSha256": consumer.digest(archive),
                                     "repository": repository.attrib, "bodies": bodies})
            report["signedPublicArtifacts"] = measured

            def execute_case(name, cohort, owners, *, fault=None, expected=True):
                destination = args.evidence / (name + "-output")
                destination.mkdir()
                request = {"generation": 1, "instanceId": "public-owner-component", "gatewayAppId": "eventstore",
                           "outputDirectory": str(destination), "qualificationCohort": cohort, "owners": owners}
                path = args.evidence / (name + "-request.json")
                path.write_text(json.dumps(request, indent=2) + "\n")
                executable = output
                if fault:
                    executable = work / name
                    shutil.copytree(output, executable)
                    with (executable / (fault + ".dll")).open("ab") as body:
                        body.write(b"\0")
                completed = consumer.execute(["dotnet", str(executable / "Hexalith.Platform.CatalogBuilder.dll"), str(path)],
                                             args.evidence / (name + ".log"), expected=0 if expected else 1)
                row = {"name": name, "exitCode": completed.returncode, "expectedSuccess": expected}
                if expected:
                    catalog = json.loads((destination / "catalog.json").read_text())
                    receipt = json.loads((destination / "receipt.json").read_text())
                    counts = {key: len(catalog[key]) for key in ["routes", "operations", "declarations", "idempotency"]}
                    assert counts == ({"routes": 70, "operations": 70, "declarations": 70, "idempotency": 46}
                                      if cohort == FULL else {"routes": 23, "operations": 23, "declarations": 23, "idempotency": 12}), counts
                    kinds = {kind: sum(declaration["kind"] == kind for declaration in catalog["declarations"])
                             for kind in ["command", "query", "projection"]}
                    assert kinds == ({"command": 46, "query": 20, "projection": 4} if cohort == FULL
                                     else {"command": 12, "query": 9, "projection": 2}), kinds
                    row.update(counts=counts, declarationKinds=kinds, rootDigest=receipt["rootDigest"], catalogSha256=consumer.digest(destination / "catalog.json"),
                               loadedExternal=[artifact for artifact in receipt["loadedArtifacts"]
                                               if Path(artifact["path"]).name.startswith("ModelContextProtocol")])
                else:
                    assert not list(destination.iterdir()), name
                    assert "exact signed public artifact bytes" in completed.stderr or "not qualified" in completed.stderr, completed.stderr
                    row["refusedBeforeOutput"] = True
                report["cases"].append(row)
                save()

            execute_case("current-base", BASE, [TENANTS])
            execute_case("current-full", FULL, OWNERS)
            for name, cohort, owners in [
                    ("unknown", "unknown", OWNERS), ("source", "source", OWNERS),
                    ("missing-owner", FULL, OWNERS[:-1]), ("duplicate-owner", FULL, [*OWNERS, TENANTS]),
                    ("foreign-owner", FULL, [{**TENANTS, "assembly": "Hexalith.Foreign.Contracts"}, *OWNERS[1:]]),
                    ("base-with-expanded-owners", BASE, OWNERS),
                    ("previous-full-with-current-bodies", "parties-3.1.0-eventstore-4.1.0-tenants-7.0.5-memories-3.0.25-mcpcli-2.0.1", OWNERS),
                    ("previous-base-with-current-bodies", "private-eventstore-package-test-4.1.0-tenants-7.0.5", [TENANTS])]:
                execute_case(name, cohort, owners, expected=False)
            for name in ["Hexalith.EventStore.Contracts", "Hexalith.Tenants.Contracts", "Hexalith.Memories.Contracts", "Hexalith.Parties.Contracts"]:
                execute_case("changed-" + name, FULL, OWNERS, fault=name, expected=False)
            for name, expected in report["executedArtifacts"]["files"].items():
                assert consumer.digest(output / name) == expected, name
            report.update(status="passed", caseCount=len(report["cases"]), originalPublicFilesUnchanged=True)
    except BaseException as error:
        report.update(status="failed", error=f"{type(error).__name__}: {error}")
        raise
    finally:
        save()
    print(json.dumps({"status": report["status"], "cases": report["caseCount"], "candidateAcceptance": False}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--builds-root", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    qualify(parser.parse_args())


if __name__ == "__main__":
    main()
