#!/usr/bin/env python3
"""Exercise the exact SDK4.2 public Parties reader; preserve released image provenance and grant no runtime acceptance."""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import xml.etree.ElementTree as ET
import zipfile

import verify_catalog_builder as consumer


COHORT = "parties-3.1.0-eventstore-4.2.0-tenants-7.0.5-memories-3.0.25-mcpcli-2.0.1"
SDK_SOURCE = "aa16227bd07f0564a3026d619a5abd973e02656f"
OWNERS = [
    {"assembly": "Hexalith.Tenants.Contracts", "adapterAssembly": "Hexalith.Tenants.Server", "appId": "tenants"},
    {"assembly": "Hexalith.Memories.EventStore", "adapterAssembly": "Hexalith.Memories.EventStoreExtensions", "appId": "memories-domain-service"},
    {"assembly": "Hexalith.Parties.Contracts", "adapterAssembly": "Hexalith.Parties.EventStoreExtensions", "appId": "parties"},
]
PACKAGES = {name: version for version, names in [
    ("4.2.0", ["Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService"]),
    ("7.0.5", ["Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server"]),
    ("3.0.25", ["Hexalith.Memories.Contracts", "Hexalith.Memories.EventStore", "Hexalith.Memories.EventStoreExtensions"]),
    ("3.1.0", ["Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions"]),
    ("2.0.1", ["Hexalith.McpCli.Abstractions"]),
] for name in names}


def qualify(args):
    args.evidence.mkdir(parents=True, exist_ok=False)
    report = {"scope": "SDK4.2 public reader components; released owner images retain their measured SDK4.1 provenance",
              "candidateAcceptance": False, "runtimeAcceptance": False, "status": "running", "cases": []}
    result = args.evidence / "result.json"

    def save():
        result.write_text(json.dumps(report, indent=2) + "\n")

    shutil.copyfile(Path(__file__), args.evidence / "verifier-source.py")
    shutil.copyfile(Path(consumer.__file__), args.evidence / "consumer-helper-source.py")
    save()
    try:
        declarations = [
            ("src/Hexalith.Platform.Composition/Declarations/tenants.json", "1588f3428ec29296c452fdd35bf95146142315aad3ad8ceb934e7a791f1fb522"),
            ("src/Hexalith.Platform.Composition/Declarations/memories.json", "56f6dc8134d3f7cea06910e923a4736a450e00241e8e4a565ac9226897881840"),
            ("tests/Hexalith.Platform.Composition.Tests/Fixtures/parties-current-declaration.json", "d9c193b0e7b61601b8fcceb906b5dafe4769a1b9d7538a6ce164b4aa0840b348"),
        ]
        for owner, (relative, expected) in zip(OWNERS, declarations):
            path = consumer.ROOT / relative
            assert consumer.digest(path) == expected, relative
            module = json.loads(path.read_text())["modules"][0]
            domain = next(server for server in module["identity"]["servers"] if server["id"] == "domain")
            assert owner["appId"] == domain["appId"], (owner, domain)
        with tempfile.TemporaryDirectory(prefix="sdk420-public-reader-") as temporary:
            work = Path(temporary)
            project = work / "Hexalith.Platform.CatalogBuilder.csproj"
            consumer.make_project(project, program=(consumer.ROOT / "src/Hexalith.Platform.CatalogBuilder/Program.cs").read_text())
            xml = ET.parse(project)
            items = ET.SubElement(xml.getroot(), "ItemGroup")
            for name in PACKAGES:
                if not name.startswith("Hexalith.EventStore."):
                    ET.SubElement(items, "PackageReference", {"Include": name})
            xml.write(project, encoding="unicode")
            output = work / "output"
            consumer.execute(["dotnet", "build", str(project), "-c", "Release", "--output", str(output),
                              "-m:1", "-nr:false", "--disable-build-servers", "-warnaserror",
                              *consumer.properties(args.builds_root, "4.2.0", "7.0.5"),
                              "-p:HexalithMemoriesVersion=3.0.25", "-p:HexalithMcpCliVersion=2.0.1", "-p:HexalithPartiesVersion=3.1.0"],
                             args.evidence / "build.log")
            report["executedArtifacts"] = consumer.retain_execution(project, output, "reader", args.evidence, "4.2.0", "7.0.5")
            shutil.copytree(output, args.evidence / "reader-output")
            assets = json.loads((work / "obj/project.assets.json").read_text())
            shutil.copyfile(work / "obj/project.assets.json", args.evidence / "project.assets.json")
            artifacts = []
            for name, version in PACKAGES.items():
                assert assets["libraries"][name + "/" + version]["type"] == "package"
                archive = next(Path(folder) / name.lower() / version / (name.lower() + "." + version + ".nupkg")
                               for folder in assets["packageFolders"]
                               if (Path(folder) / name.lower() / version / (name.lower() + "." + version + ".nupkg")).is_file())
                consumer.execute(["dotnet", "nuget", "verify", str(archive), "--all"], args.evidence / (name + "-signature.log"))
                with zipfile.ZipFile(archive) as package:
                    metadata = ET.fromstring(package.read(next(member for member in package.namelist() if member.endswith(".nuspec"))))
                    assert metadata.find(".//{*}id").text == name
                    assert metadata.find(".//{*}version").text == version
                    repository = metadata.find(".//{*}repository").attrib
                    if name.startswith("Hexalith.EventStore."):
                        assert repository["commit"] == SDK_SOURCE
                    member = "lib/net10.0/" + name + ".dll"
                    body = output / (name + ".dll")
                    assert body.read_bytes() == package.read(member), name
                artifacts.append({"id": name, "version": version, "repository": repository,
                                  "url": f"https://api.nuget.org/v3-flatcontainer/{name.lower()}/{version}/{archive.name}",
                                  "archiveSha256": consumer.digest(archive), "body": member, "sha256": consumer.digest(body)})
            report["publicArtifacts"] = artifacts

            def execute_case(name, cohort=COHORT, owners=OWNERS, fault=None, expected=False):
                destination = args.evidence / (name + "-output")
                destination.mkdir()
                request = {"generation": 1, "instanceId": "sdk420-public-reader", "gatewayAppId": "eventstore",
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
                    assert counts == {"routes": 70, "operations": 70, "declarations": 70, "idempotency": 46}, counts
                    loaded = {Path(artifact["path"]).stem: artifact["sha256"] for artifact in receipt["loadedArtifacts"]}
                    assert all(loaded[artifact["id"]] == artifact["sha256"] for artifact in artifacts)
                    row.update(counts=counts, rootDigest=receipt["rootDigest"], catalogSha256=consumer.digest(destination / "catalog.json"))
                else:
                    assert not list(destination.iterdir()), name
                    assert "exact signed public artifact bytes" in completed.stderr or "not qualified" in completed.stderr, completed.stderr
                    row["refusedBeforeOutput"] = True
                report["cases"].append(row)
                save()

            execute_case("exact-public-sdk420-reader", expected=True)
            for name, cohort in [
                    ("unknown", "unknown"), ("near-sdk", COHORT.replace("4.2.0", "4.2.1")),
                    ("metadata-sdk", COHORT.replace("4.2.0", "4.2.0+metadata")),
                    ("old-sdk-authority", COHORT.replace("4.2.0", "4.1.0")),
                    ("private-current-purpose", "current-parties-eventstore-4.2.0-tenants-7.0.5-memories-3.0.25-mcpcli-2.0.1"),
                    ("private-eventstore-purpose", "private-eventstore-package-test-4.2.0-tenants-7.0.5")]:
                execute_case(name, cohort=cohort, owners=OWNERS[:-1] if name == "private-current-purpose" else OWNERS)
            execute_case("missing-owner", owners=OWNERS[:-1])
            execute_case("duplicate-owner", owners=[*OWNERS, OWNERS[0]])
            execute_case("foreign-adapter", owners=[{**OWNERS[0], "adapterAssembly": "Hexalith.Foreign.Server"}, *OWNERS[1:]])
            for name in PACKAGES:
                execute_case("changed-" + name, fault=name)
            for name, expected in report["executedArtifacts"]["files"].items():
                assert consumer.digest(output / name) == expected, name
            report.update(status="passed", caseCount=len(report["cases"]), originalPublicFilesUnchanged=True)
    except BaseException as error:
        report.update(status="failed", error=f"{type(error).__name__}: {error}")
        raise
    finally:
        save()
    print(json.dumps({"status": report["status"], "cases": report["caseCount"], "runtimeAcceptance": False}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--builds-root", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    qualify(parser.parse_args())
