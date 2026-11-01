#!/usr/bin/env python3
"""Run the actual catalog executable and published SDK activation-binding controls."""

import argparse
import datetime
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import urllib.request
import uuid
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]
SDK = "3.130.1"
TENANTS = "6.0.0"
BASELINE = "68b785b6a929050771e70d78f83ff10fea3daabc"
SAMPLE_SOURCES = {"3.130.0": "484f4bd82fd2ef4b6c2653fdde57feeeb6d85a3c",
                  "3.130.1": "1de42b706f52af0da6513f0b7663002606d17156",
                  "3.130.2": "28e3f260bbaf4619d1a99d656fba04c0c532387b"}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def retain_execution(project, output, name, evidence, expected_sdk):
    destination = evidence / "executed" / name
    destination.mkdir(parents=True)
    files = {}
    for artifact in sorted(output.glob("*")):
        if artifact.name.endswith((".dll", ".deps.json", ".runtimeconfig.json")):
            files[artifact.name] = digest(artifact)
        if artifact.name.endswith((".deps.json", ".runtimeconfig.json")) or artifact.name in {
                "Hexalith.Platform.CatalogBuilder.dll", "CatalogActivationProbe.dll",
                "Hexalith.Platform.CatalogBuilder.SampleFixture.dll", "Hexalith.EventStore.Sample.Contracts.dll",
                "Hexalith.EventStore.Client.dll", "Hexalith.EventStore.Contracts.dll", "Hexalith.EventStore.ServiceDefaults.dll"}:
            shutil.copyfile(artifact, destination / artifact.name)
    assets = project.parent / "obj/project.assets.json"
    data = json.loads(assets.read_text())
    versions = {key.split("/", 1)[0]: key.split("/", 1)[1] for key in data["libraries"]
                if key.startswith("Hexalith.EventStore.")}
    assert versions and all(version == expected_sdk for version in versions.values()), versions
    record = {"files": files, "projectAssetsSha256": digest(assets), "resolvedSdkPackages": versions,
              "packageSources": list(data["project"]["restore"]["sources"])}
    assert record["packageSources"] == ["https://api.nuget.org/v3/index.json"]
    (destination / "identity.json").write_text(json.dumps(record, indent=2) + "\n")
    return record


def sample_fixture(work, args):
    directory = work / "sample"
    directory.mkdir()
    commit = SAMPLE_SOURCES[args.sdk_version]
    groups = {
        "contracts": ["Counter/Commands/IncrementCounter.cs", "Counter/Commands/DecrementCounter.cs",
                      "Counter/Commands/ResetCounter.cs", "Counter/Commands/CloseCounter.cs",
                      "Counter/CounterCatalogDeclarations.cs", "Counter/Queries/GetCounterStatusQuery.cs",
                      "Counter/Queries/CounterStatusPayload.cs"],
        "host": ["Counter/SampleCatalog.cs", "Counter/SampleCatalogAdapters.cs",
                 "Greeting/GreetingCatalogDeclarations.cs", "Greeting/Commands/SendGreeting.cs",
                 "Counter/Projections/CounterProjection.cs", "Counter/Projections/CounterProjectionHandler.cs",
                 "Counter/State/CounterState.cs", "Counter/Events/CounterIncremented.cs",
                 "Counter/Events/CounterDecremented.cs", "Counter/Events/CounterReset.cs", "Counter/Events/CounterClosed.cs"],
    }
    retained = args.evidence / "genuine-sample-source"
    retained.mkdir()
    hashes = {}
    artifacts = {}
    for kind, paths in groups.items():
        project_dir = directory / kind
        project_dir.mkdir()
        sample_name = "Hexalith.EventStore.Sample.Contracts" if kind == "contracts" else "Hexalith.EventStore.Sample"
        for index, relative in enumerate(paths):
            url = f"https://raw.githubusercontent.com/Hexalith/Hexalith.EventStore/{commit}/samples/{sample_name}/{relative}"
            path = project_dir / f"Source{index}.cs"
            with urllib.request.urlopen(url, timeout=45) as response:
                path.write_bytes(response.read())
            shutil.copyfile(path, retained / f"{kind}-{path.name}")
            hashes[f"{sample_name}/{relative}"] = {"source": url, "sha256": digest(path)}
        project = ET.Element("Project", {"Sdk": "Microsoft.NET.Sdk"})
        props = ET.SubElement(project, "PropertyGroup")
        for key, value in {"TargetFramework": "net10.0", "ImplicitUsings": "enable", "Nullable": "enable",
                           "IsPackable": "false", "IsPublishable": "false", "TreatWarningsAsErrors": "true"}.items():
            ET.SubElement(props, key).text = value
        items = ET.SubElement(project, "ItemGroup")
        ET.SubElement(items, "PackageReference", {"Include": "Hexalith.EventStore.Contracts"})
        if kind == "host":
            ET.SubElement(items, "PackageReference", {"Include": "Hexalith.EventStore.DomainService"})
            reference = ET.SubElement(items, "Reference", {"Include": "Hexalith.EventStore.Sample.Contracts"})
            ET.SubElement(reference, "HintPath").text = str(artifacts["contracts"] / "Hexalith.EventStore.Sample.Contracts.dll")
        for filename in ["Directory.Packages.props", "Directory.Build.props"]:
            project.insert(0, ET.Element("Import", {"Project": str(ROOT / filename)}))
        assembly = "Hexalith.EventStore.Sample.Contracts" if kind == "contracts" else "Hexalith.Platform.CatalogBuilder.SampleFixture"
        path = project_dir / f"{assembly}.csproj"
        ET.ElementTree(project).write(path, encoding="unicode")
        output = project_dir / "output"
        execute(["dotnet", "build", str(path), "-c", "Release", "--output", str(output), "-m:1", "-nr:false", "-warnaserror",
                 *properties(args.builds_root, args.sdk_version)], args.evidence / f"genuine-sample-{kind}-build.log")
        artifacts[kind] = output
        artifacts[kind + "Identity"] = retain_execution(path, output, f"sample-{kind}", args.evidence, args.sdk_version)
    assets = json.loads((directory / "contracts/obj/project.assets.json").read_text())
    package_root = Path(next(iter(assets["packageFolders"])))
    archive = package_root / "hexalith.eventstore.contracts" / args.sdk_version / f"hexalith.eventstore.contracts.{args.sdk_version}.nupkg"
    import zipfile
    with zipfile.ZipFile(archive) as package:
        nuspec = ET.fromstring(package.read(next(name for name in package.namelist() if name.endswith(".nuspec"))))
        assert nuspec.find(".//{*}id").text == "Hexalith.EventStore.Contracts"
        assert nuspec.find(".//{*}version").text == args.sdk_version
        assert nuspec.find(".//{*}repository").attrib["commit"] == commit
        dll = package.read("lib/net10.0/Hexalith.EventStore.Contracts.dll")
    identity = {"publishedSource": commit, "sourceFiles": hashes, "publicContractsArchiveSha256": digest(archive),
                "publicContractsDllSha256": hashlib.sha256(dll).hexdigest(),
                "contracts": artifacts["contractsIdentity"], "host": artifacts["hostIdentity"],
                "fixtureOnlyNotPublished": True}
    return artifacts["host"], identity


def execute(command, evidence, expected=0):
    result = subprocess.run(command, cwd=ROOT, text=True, capture_output=True, check=False)
    evidence.write_text(result.stdout + result.stderr)
    if (result.returncode == 0) != (expected == 0):
        raise AssertionError(f"Unexpected exit {result.returncode}; see {evidence}")
    return result


def properties(builds, sdk=SDK):
    return [f"-p:HexalithBuildsRoot={builds}", f"-p:HexalithEventStoreVersion={sdk}",
            f"-p:HexalithTenantsVersion={TENANTS}", "-p:UseHexalithProjectReferences=false",
            "-p:CentralPackageVersionsFileImported=true",
            "-p:RestoreSources=https://api.nuget.org/v3/index.json"]


def make_project(path, *, probe=False, program=None):
    if probe:
        project = ET.Element("Project", {"Sdk": "Microsoft.NET.Sdk"})
        props = ET.SubElement(project, "PropertyGroup")
        for name, value in {"OutputType": "Exe", "TargetFramework": "net10.0", "ImplicitUsings": "enable",
                            "Nullable": "enable", "TreatWarningsAsErrors": "true"}.items():
            ET.SubElement(props, name).text = value
        items = ET.SubElement(project, "ItemGroup")
        ET.SubElement(items, "Compile", {"Include": str(Path(__file__).with_name("CatalogActivationProbe.cs"))})
        for name in ["Hexalith.EventStore.Client", "Hexalith.EventStore.ServiceDefaults"]:
            ET.SubElement(items, "PackageReference", {"Include": name})
    else:
        project = ET.parse(ROOT / "src/Hexalith.Platform.CatalogBuilder/Hexalith.Platform.CatalogBuilder.csproj").getroot()
        (path.parent / "Program.cs").write_text(program)
    for filename in ["Directory.Packages.props", "Directory.Build.props"]:
        project.insert(0, ET.Element("Import", {"Project": str(ROOT / filename)}))
    ET.ElementTree(project).write(path, encoding="unicode")


def qualify(args):
    with tempfile.TemporaryDirectory(prefix="catalog-builder-check-") as temporary:
        work = Path(temporary)
        executed = {}
        bindings = ET.Element("Project")
        items = ET.SubElement(bindings, "ItemGroup")
        if args.owners:
            fixture = json.loads(args.owners.read_text())
            owners = fixture["owners"]
            chosen = {}
            for directory in fixture["closureDirectories"]:
                for dll in sorted(Path(directory).glob("*.dll")):
                    if dll.stem in {"Hexalith.EventStore.Client", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults"}:
                        continue
                    chosen.setdefault(dll.stem, dll)
            for owner in owners:
                chosen[owner["assembly"]] = Path(owner["assemblyPath"])
                chosen[owner["adapterAssembly"]] = Path(owner["adapterPath"])
            for name, dll in chosen.items():
                reference = ET.SubElement(items, "Reference", {"Include": name})
                ET.SubElement(reference, "HintPath").text = str(dll)
        else:
            # Genuine SDK Sample: seven TOTAL routes, including one actor query.
            sample_output, sample_identity = sample_fixture(work, args)
            executed["sample"] = sample_identity
            fixture = {"expectedPolicies": 17, "expectedProjectionQueries": 1, "expectedSampleRoutes": 7}
            owners = [{"assembly": "Hexalith.Tenants.Contracts", "adapterAssembly": "Hexalith.Tenants.Server",
                       "appId": "tenants-binding-check"},
                      {"assembly": "Hexalith.Platform.CatalogBuilder.SampleFixture",
                       "adapterAssembly": "Hexalith.Platform.CatalogBuilder.SampleFixture", "appId": "sample-binding-check"}]
            for package in ["Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server", "Hexalith.EventStore.DomainService"]:
                ET.SubElement(items, "PackageReference", {"Include": package})
            for assembly in ["Hexalith.Platform.CatalogBuilder.SampleFixture", "Hexalith.EventStore.Sample.Contracts"]:
                reference = ET.SubElement(items, "Reference", {"Include": assembly})
                ET.SubElement(reference, "HintPath").text = str(sample_output / f"{assembly}.dll")
        binding_path = work / "bindings.props"
        ET.ElementTree(bindings).write(binding_path, encoding="unicode")
        source = ROOT / "src/Hexalith.Platform.CatalogBuilder/Program.cs"
        original = source.read_text()
        baseline = subprocess.run(["git", "show", f"{BASELINE}:src/Hexalith.Platform.CatalogBuilder/Program.cs"],
                                  cwd=ROOT, capture_output=True, text=True, check=True).stdout
        outputs = {}
        for kind, text in [("baseline", baseline), ("actual", original), ("default-consumer", original.replace(
                "Bind(command, generation, gatewayAppId)", "Bind(command, generation)")),
                ("foreign-consumer", original.replace("Bind(command, generation, gatewayAppId)",
                                                       'Bind(command, generation, "eventstore-foreign")'))]:
            directory = work / kind
            directory.mkdir()
            project = directory / "Hexalith.Platform.CatalogBuilder.csproj"
            make_project(project, program=text)
            output = directory / "output"
            execute(["dotnet", "build", str(project), "-c", "Release", "--output", str(output), "-m:1", "-nr:false",
                     "--disable-build-servers", "-warnaserror", f"-p:PlatformCatalogBindings={binding_path}",
                     *properties(args.builds_root, args.sdk_version)], args.evidence / f"{kind}-build.log")
            outputs[kind] = output / "Hexalith.Platform.CatalogBuilder.dll"
            executed[kind] = retain_execution(project, output, kind, args.evidence, args.sdk_version)
            if not args.owners:
                assert executed[kind]["files"]["Hexalith.EventStore.Contracts.dll"] == sample_identity["publicContractsDllSha256"]
        probe_dir = work / "probe"
        probe_dir.mkdir()
        probe_project = probe_dir / "CatalogActivationProbe.csproj"
        make_project(probe_project, probe=True)
        execute(["dotnet", "build", str(probe_project), "-c", "Release", "-m:1", "-nr:false", "-warnaserror",
                 *properties(args.builds_root, args.sdk_version)], args.evidence / "probe-build.log")
        probe = probe_dir / "bin/Release/net10.0/CatalogActivationProbe.dll"
        executed["probe"] = retain_execution(probe_project, probe.parent, "probe", args.evidence, args.sdk_version)
        cases = []
        for kind, gateway in [("baseline", "eventstore-binding-check"), ("actual", "eventstore-binding-check"), ("actual", "eventstore-second-check"),
                              ("default-consumer", "eventstore-binding-check"), ("foreign-consumer", "eventstore-binding-check")]:
            name = f"{kind}-{len(cases)}"
            target = work / name
            target.mkdir()
            request = {"generation": 1, "instanceId": "catalog-binding-check", "gatewayAppId": gateway,
                       "outputDirectory": str(target), "owners": owners}
            path = work / f"{name}.json"
            path.write_text(json.dumps(request))
            execute(["dotnet", str(outputs[kind]), str(path)], args.evidence / f"{name}-generate.log")
            result = execute(["dotnet", str(probe), str(path), str(fixture["expectedPolicies"]),
                              str(fixture["expectedProjectionQueries"]), "accept" if kind == "actual" else "refuse",
                              str(fixture.get("expectedSampleRoutes", 0))],
                             args.evidence / f"{name}-activation.log")
            for filename in ["catalog.json", "receipt.json"]:
                shutil.copyfile(target / filename, args.evidence / f"{name}-{filename}")
            cases.append({"case": name, **json.loads(result.stdout)})
        before = json.loads((args.evidence / "baseline-0-catalog.json").read_text())
        after = json.loads((args.evidence / "actual-1-catalog.json").read_text())
        for key in ["schemaVersion", "generation", "profile", "allowRuntimeOverrides", "declarations", "routes",
                    "operations", "retainedIdempotency", "routeFacetDigest"]:
            assert before[key] == after[key], f"Consumer-only fix changed {key}"
        def policy_without_consumer(policy):
            return {key: value for key, value in policy.items() if key not in {"consumerAppId", "contentDigest"}}
        assert [policy_without_consumer(policy) for policy in before["idempotency"]] == [
            policy_without_consumer(policy) for policy in after["idempotency"]]
        for index, gateway in enumerate(["missing", "", " \t", None, 123, True]):
            target = work / f"invalid-{index}"
            target.mkdir()
            request = {"generation": 1, "instanceId": "catalog-binding-check", "gatewayAppId": gateway,
                       "outputDirectory": str(target), "owners": owners}
            if gateway == "missing":
                del request["gatewayAppId"]
            path = work / f"invalid-{index}.json"
            path.write_text(json.dumps(request))
            result = execute(["dotnet", str(outputs["actual"]), str(path)], args.evidence / f"invalid-{index}.log", expected=1)
            assert "gatewayAppId" in result.stderr and not list(target.iterdir()), result.stderr
            cases.append({"case": f"invalid-{index}", "refusedBeforeOutput": True})
        if not args.owners:
            legacy_dir = work / "legacy"
            legacy_dir.mkdir()
            legacy_project = legacy_dir / "Hexalith.Platform.CatalogBuilder.csproj"
            make_project(legacy_project, program=original)
            legacy_output = legacy_dir / "output"
            legacy_bindings = legacy_dir / "bindings.props"
            legacy_xml = ET.Element("Project")
            legacy_items = ET.SubElement(legacy_xml, "ItemGroup")
            for package in ["Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server"]:
                ET.SubElement(legacy_items, "PackageReference", {"Include": package})
            ET.ElementTree(legacy_xml).write(legacy_bindings, encoding="unicode")
            execute(["dotnet", "build", str(legacy_project), "-c", "Release", "--output", str(legacy_output),
                     "-m:1", "-nr:false", "-warnaserror", f"-p:PlatformCatalogBindings={legacy_bindings}",
                     *properties(args.builds_root, "3.129.0")], args.evidence / "legacy129-build.log")
            executed["legacy129"] = retain_execution(legacy_project, legacy_output, "legacy129", args.evidence, "3.129.0")
            for gateway in ["eventstore", "eventstore-scoped"]:
                target = legacy_dir / gateway
                target.mkdir()
                path = legacy_dir / f"{gateway}.json"
                path.write_text(json.dumps({"generation": 1, "instanceId": "legacy-binding-check", "gatewayAppId": gateway,
                                            "outputDirectory": str(target), "owners": owners[:1]}))
                result = execute(["dotnet", str(legacy_output / "Hexalith.Platform.CatalogBuilder.dll"), str(path)],
                                 args.evidence / f"legacy129-{gateway}.log", expected=0 if gateway == "eventstore" else 1)
                if gateway == "eventstore":
                    result = execute(["dotnet", str(probe), str(path), "12", "0", "accept", "0"],
                                     args.evidence / "legacy129-activation.log")
                    cases.append({"case": "legacy129-explicit-eventstore", **json.loads(result.stdout)})
                else:
                    assert "qualified SDK" in result.stderr and not list(target.iterdir())
                    cases.append({"case": "legacy129-scoped", "refusedBeforeOutput": True})
        report = {"sdk": args.sdk_version, "toolSourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                  "genuineOwnerAssemblies": [owner["assembly"] for owner in owners], "cases": cases,
                  "baseline": BASELINE, "allDeclarationsBindingsAndOperationsUnchanged": True,
                  "policyFieldsExceptConsumerAndDigestUnchanged": True,
                  "executedArtifacts": executed,
                  "authorityRegistered": False, "originalProducerWrites": False}
        return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--builds-root", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--owners", type=Path, help="Optional copied genuine owner DLL closure manifest.")
    parser.add_argument("--sdk-version", choices=["3.130.0", "3.130.1", "3.130.2"], default=SDK)
    args = parser.parse_args()
    args.evidence = args.evidence.resolve()
    state = {"attempt": uuid.uuid4().hex, "startedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
             "status": "started", "candidateAcceptance": False}
    result = args.evidence / "result.json"
    if args.evidence.exists():
        if result.is_file():
            previous = args.evidence / f"prior-result-{digest(result)}.json"
            if not previous.exists():
                shutil.copyfile(result, previous)
        state.update(status="failed", error="Evidence directory must be fresh; no qualification executed.")
        if args.evidence.is_dir():
            result.write_text(json.dumps(state, indent=2) + "\n")
        raise RuntimeError(state["error"])
    args.evidence.mkdir(parents=True)
    result.write_text(json.dumps(state, indent=2) + "\n")
    try:
        report = qualify(args)
    except Exception as error:
        state.update(status="failed", error=f"{type(error).__name__}: {error}")
        result.write_text(json.dumps(state, indent=2) + "\n")
        raise
    state.update(report)
    state.update(status="passed", candidateAcceptance=True,
                 completedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    result.write_text(json.dumps(state, indent=2) + "\n")
    print(json.dumps(state, indent=2))


if __name__ == "__main__":
    main()
