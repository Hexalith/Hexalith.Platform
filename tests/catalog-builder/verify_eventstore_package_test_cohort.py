#!/usr/bin/env python3
"""Measure the real private six-body reader; never grant runner, native or public launch authority."""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import xml.etree.ElementTree as ET
import zipfile

import verify_catalog_builder as consumer


COHORT = "private-eventstore-package-test-4.1.0-tenants-7.0.5"
OWNER = {"assembly": "Hexalith.Tenants.Contracts", "adapterAssembly": "Hexalith.Tenants.Server", "appId": "tenants"}
BODIES = {
    "Hexalith.EventStore.Contracts": "f3f2d7f6e7edf54e187af15edec8b4f13bf7e255b186b999efa0836a0c86a739",
    "Hexalith.EventStore.Client": "702dfb7537e990a2065fe37c0b06301cdb70c26907af42faa6acdae331a7bce6",
    "Hexalith.EventStore.DomainService": "5d8068c8c116551efc3d545f85d41040023fa38217e5b4e73e4d225fe8e8bf78",
    "Hexalith.EventStore.ServiceDefaults": "0e3f88ffefb12327d53f865b96bbb774beb71f7c78c9f20a3cbefa4901e9ba2d",
    "Hexalith.Tenants.Contracts": "adedcc49d81f2ceb6855f365dddb05e8f1f550e303c5f0e92c4d3153ff672d45",
    "Hexalith.Tenants.Server": "e84f4b22485c5db4866636558488e387132369583483b0334839c1bcf66b95ad",
}


def qualify(args):
    args.evidence.mkdir(parents=True, exist_ok=False)
    report = {"scope": "private six-body reader only; no runner, native, lifecycle or public-consumer acceptance",
              "qualificationCredit": False, "status": "running", "cases": []}

    def save():
        (args.evidence / "result.json").write_text(json.dumps(report, indent=2) + "\n")

    save()
    try:
        with tempfile.TemporaryDirectory(prefix="private-eventstore-reader-") as temporary:
            work = Path(temporary)
            project = work / "Hexalith.Platform.CatalogBuilder.csproj"
            consumer.make_project(project, program=(consumer.ROOT / "src/Hexalith.Platform.CatalogBuilder/Program.cs").read_text())
            xml = ET.parse(project)
            items = ET.SubElement(xml.getroot(), "ItemGroup")
            for name in ("Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server"):
                ET.SubElement(items, "PackageReference", {"Include": name})
                ET.SubElement(items, "PackageDownload", {"Include": name, "Version": "[7.0.0];[7.0.2];[7.0.4]"})
            xml.write(project, encoding="unicode")
            output = work / "output"
            consumer.execute(["dotnet", "build", str(project), "-c", "Release", "--output", str(output), "-m:1", "-nr:false",
                              "--disable-build-servers", "-warnaserror", *consumer.properties(args.builds_root, "4.1.0", "7.0.5")], args.evidence / "build.log")
            report["executedArtifacts"] = consumer.retain_execution(project, output, "reader", args.evidence, "4.1.0", "7.0.5")
            shutil.copytree(output, args.evidence / "reader-output")
            assets = json.loads((work / "obj/project.assets.json").read_text())
            shutil.copyfile(work / "obj/project.assets.json", args.evidence / "project.assets.json")
            report["signedPackages"] = []
            for name, expected in BODIES.items():
                version = "4.1.0" if name.startswith("Hexalith.EventStore.") else "7.0.5"
                relative = Path(name.lower()) / version
                archive = next(Path(folder) / relative / (name.lower() + "." + version + ".nupkg") for folder in assets["packageFolders"]
                               if (Path(folder) / relative / (name.lower() + "." + version + ".nupkg")).is_file())
                consumer.execute(["dotnet", "nuget", "verify", str(archive), "--all"], args.evidence / (name + "-signature.log"))
                with zipfile.ZipFile(archive) as package:
                    specification = ET.fromstring(package.read(next(member for member in package.namelist() if member.endswith(".nuspec"))))
                    assert specification.find(".//{*}version").text == version
                    expected_source = "dbc6e7e08d605ba6c6c790e3e08a15dc8bbdec8a" if version == "4.1.0" else "d88aef6b52d7e8a2e2d81132b1d76b7e1d1f0f35"
                    repository = specification.find(".//{*}repository")
                    assert repository.attrib["commit"] == expected_source
                    assert repository.attrib["url"].rstrip("/").removesuffix(".git") == "https://github.com/Hexalith/" + ("Hexalith.EventStore" if version == "4.1.0" else "Hexalith.Tenants")
                    body = package.read("lib/net10.0/" + name + ".dll")
                    assert hashlib.sha256(body).hexdigest() == expected
                    assert (output / (name + ".dll")).read_bytes() == body
                report["signedPackages"].append({"id": name, "version": version, "source": expected_source,
                                                 "archiveSha256": consumer.digest(archive), "bodySha256": expected})

            def case(name, owners, *, cohort=COHORT, fault=None, missing=None, old=None, expected=False):
                destination = args.evidence / (name + "-output")
                destination.mkdir()
                executable = output
                if fault or missing or old:
                    executable = work / name
                    shutil.copytree(output, executable)
                    if fault:
                        with (executable / (fault + ".dll")).open("ab") as body:
                            body.write(b"\0")
                    if missing:
                        (executable / (missing + ".dll")).unlink()
                    if old:
                        assembly, version = old
                        original = next(Path(folder) / assembly.lower() / version / "lib/net10.0" / (assembly + ".dll")
                                        for folder in assets["packageFolders"] if (Path(folder) / assembly.lower() / version / "lib/net10.0" / (assembly + ".dll")).is_file())
                        shutil.copyfile(original, executable / (assembly + ".dll"))
                request = args.evidence / (name + "-request.json")
                request.write_text(json.dumps({"generation": 1, "instanceId": "private-reader-component", "gatewayAppId": "eventstore",
                                               "outputDirectory": str(destination), "qualificationCohort": cohort, "owners": owners}, indent=2) + "\n")
                result = consumer.execute(["dotnet", str(executable / "Hexalith.Platform.CatalogBuilder.dll"), str(request)],
                                          args.evidence / (name + ".log"), expected=0 if expected else 1)
                row = {"name": name, "exitCode": result.returncode, "expectedSuccess": expected}
                if expected:
                    catalog = json.loads((destination / "catalog.json").read_text())
                    receipt = json.loads((destination / "receipt.json").read_text())
                    counts = {key: len(catalog[key]) for key in ("routes", "operations", "declarations", "idempotency")}
                    assert counts == {"routes": 23, "operations": 23, "declarations": 23, "idempotency": 12}, counts
                    loaded = {Path(item["path"]).stem: item["sha256"] for item in receipt["loadedArtifacts"]}
                    assert all(loaded.get(assembly) == body_hash for assembly, body_hash in BODIES.items())
                    row.update(counts=counts, rootDigest=receipt["rootDigest"], catalogSha256=consumer.digest(destination / "catalog.json"))
                else:
                    assert not list(destination.iterdir()), name
                    row["refusedBeforeOutput"] = True
                report["cases"].append(row)
                save()

            case("private-six-body", [OWNER], expected=True)
            for name, owners in (("missing-owner", []), ("duplicate-owner", [OWNER, OWNER]),
                                 ("extra-owner", [OWNER, {**OWNER, "assembly": "Hexalith.Parties.Contracts", "adapterAssembly": "Hexalith.Parties.EventStoreExtensions"}]),
                                 ("foreign-owner", [{**OWNER, "assembly": "Hexalith.Foreign.Contracts"}])):
                case(name, owners)
            for name, cohort in (("near-name", COHORT + "0"), ("unknown", "unknown"),
                                 ("public-purpose", "eventstore-4.1.0-tenants-7.0.2"),
                                 ("current-parties-purpose", "current-parties-eventstore-4.1.0-tenants-7.0.5-memories-3.0.25-mcpcli-2.0.1")):
                case(name, [OWNER], cohort=cohort)
            for assembly in BODIES:
                case("changed-" + assembly, [OWNER], fault=assembly)
                case("missing-" + assembly, [OWNER], missing=assembly)
            for version in ("7.0.0", "7.0.2", "7.0.4"):
                for assembly in ("Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server"):
                    case("mixed-" + version + "-" + assembly, [OWNER], old=(assembly, version))
            assert all(consumer.digest(output / (assembly + ".dll")) == body_hash for assembly, body_hash in BODIES.items())
            report.update(status="passed", caseCount=len(report["cases"]), originalBodiesUnchanged=True)
    except BaseException as error:
        report.update(status="failed", error=f"{type(error).__name__}: {error}")
        raise
    finally:
        save()
    print(json.dumps({"status": report["status"], "cases": report["caseCount"], "qualificationCredit": False}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--builds-root", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    qualify(parser.parse_args())


if __name__ == "__main__":
    main()
