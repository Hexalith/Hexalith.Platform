#!/usr/bin/env python3
"""Exercise the retained P300/T702/M23 public reader; the current P310 boundaries remain separately required by the tuple profile."""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile

import verify_catalog_builder as consumer


BASE = "eventstore-4.1.0-tenants-7.0.2"
FULL = "parties-3.0.0-eventstore-4.1.0-tenants-7.0.2-memories-3.0.23"
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
            ("tests/Hexalith.Platform.Composition.Tests/Fixtures/parties-active-declaration.json", "40cee6ce4b82902d994b8c2a1baa0629dab33e8f2afc5c8687214fc1dab1641b"),
        ]
        for owner, (relative, expected) in zip(OWNERS, declarations):
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
                              *consumer.properties(args.builds_root, "4.1.0", "7.0.2"),
                              "-p:HexalithMemoriesVersion=3.0.23", "-p:HexalithMcpCliVersion=2.0.0",
                              "-p:HexalithPartiesVersion=3.0.0"], args.evidence / "build.log")
            report["executedArtifacts"] = consumer.retain_execution(project, output, "reader", args.evidence, "4.1.0", "7.0.2")
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
                assert version == "2.3.1", identity
                archive = next((Path(folder) / name.lower() / version / (identity.replace("/", ".").lower() + ".nupkg")
                                for folder in assets["packageFolders"]
                                if (Path(folder) / name.lower() / version / (identity.replace("/", ".").lower() + ".nupkg")).is_file()))
                consumer.execute(["dotnet", "nuget", "verify", str(archive), "--all"], args.evidence / (name + "-signature.log"))
                with zipfile.ZipFile(archive) as package:
                    for member in dependency.get("runtime", {}):
                        body = output / Path(member).name
                        assert body.read_bytes() == package.read(member), identity
                        external.append({"package": identity, "archiveSha256": consumer.digest(archive),
                                         "body": body.name, "sha256": consumer.digest(body)})
            report["externalSupportingBodies"] = external

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
                    ("historical-full-with-current-bodies", "parties-3.0.0-eventstore-4.0.4-tenants-7.0.0-memories-3.0.23", OWNERS),
                    ("historical-base-with-current-bodies", "eventstore-4.0.4-tenants-7.0.0", [TENANTS])]:
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
