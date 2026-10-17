"""Execute target-host evidence; never promote supporting checks to AD-20 acceptance."""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import tarfile
import xml.etree.ElementTree as ET
import zipfile


from verify_works_producer import qualify_runtime_source, runtime_source_snapshot

ROWS = {
    "R1": ("Aspire/Dapr composition", "Hexalith.EventStore.Aspire", "AddHexalithEventStore/AddEventStoreDomainModule", "P/src/Hexalith.Platform.Composition/WorksPreviewModel.cs"),
    "R2": ("SDK defaults, readiness and telemetry", "Hexalith.EventStore.DomainService", "AddEventStoreDomainService", "src/Hexalith.Works/Runtime/WorksHost.cs"),
    "R3": ("Origin, redelivery and quarantine", "Hexalith.EventStore.DomainService", "UseEventStoreDomainService", "src/Hexalith.Works/Runtime/Events/WorksDomainEventEndpointExtensions.cs"),
    "R4": ("Projection family and fenced rebuild", "Hexalith.EventStore.Client", "SharedProjectionEpochCoordinator", "src/Hexalith.Works/Projections/SharedRebuild/WorkItemSharedProjectionRebuildHandler.cs"),
    "R5": ("Authorized and filtered queries", "Hexalith.EventStore.DomainService", "IDomainQueryHandler", "src/Hexalith.Works/Queries/WhatsNextQueryHandler.cs"),
    "R6": ("Typed reminders and lost firing/HA", "Hexalith.EventStore.DomainService", "AddEventStoreReminders", "src/Hexalith.Works/Runtime/WorksRecoveryExtensions.cs"),
    "R7": ("Paged process, reissue and restart", "Hexalith.EventStore.DomainService", "AddEventStoreProcesses", "src/Hexalith.Works/Recovery/WorksProcessTranslator.cs"),
    "R8": ("Recovery operations, audit and alerts", "Hexalith.EventStore.DomainService", "ProcessDispositionService", "src/Hexalith.Works/Recovery/WorksProcessDispositionEndpoints.cs"),
    "R9": ("Works SDK consumption", "Hexalith.EventStore.DomainService", "AddEventStoreDomainService/UseEventStoreDomainService", "src/Hexalith.Works/Runtime/WorksHost.cs"),
    "R10": ("AD-24 transport and production admission", "Hexalith.EventStore.ServiceDefaults", "EventStoreWorkloadAuthenticationDefaults", "src/Hexalith.Works/Runtime/WorksHost.cs"),
    "R11": ("Trusted submission and receipt replay", "Hexalith.EventStore.Client", "HttpTrustedEffectSubmitter", "src/Hexalith.Works/Recovery/WorksProcessDelegationProvider.cs"),
}


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def require_passed_tests(path, required_methods=(), required_types=()):
    """Reject empty results, test failures, skips, and filters that miss required methods."""
    root = ET.parse(path).getroot()
    assemblies = root.findall("assembly")
    tests = root.findall(".//test")
    if not assemblies or not tests:
        raise ValueError("No executed tests in " + str(path))
    for assembly in assemblies:
        for key in ("errors", "failed", "skipped", "not-run"):
            if int(assembly.get(key, "0")):
                raise ValueError(f"{path}: {key} must be zero")
        if int(assembly.get("total", "0")) != len(assembly.findall(".//test")):
            raise ValueError(f"{path}: test count does not match execution inventory")
    if any(test.get("result") != "Pass" for test in tests):
        raise ValueError(str(path) + ": every required test must execute and pass")
    methods = {test.get("method") for test in tests}
    if not set(required_methods).issubset(methods):
        raise ValueError(str(path) + ": required test methods did not execute")
    if not set(required_types).issubset({test.get("type") for test in tests}):
        raise ValueError(str(path) + ": required test classes did not execute")
    return {"total": len(tests), "passed": len(tests), "skipped": 0, "sha256": sha256(path)}


def verify_host_records(path, assembly, platform):
    """Bind target starts and the deliberate retained-host rollback to their exact artifacts."""
    records = [json.loads(line) for line in Path(path).read_text().splitlines() if line]
    target = [record for record in records if record.get("role") == "platform-target"]
    if not target:
        raise ValueError("No actual Platform starts were recorded")
    for record in records:
        binary = Path(record["assembly"])
        if binary.parent.name != "release":
            raise ValueError("A qualifying host must use the selected Release deployment output")
        if not binary.is_file() or record["sha256"].lower() != sha256(binary):
            raise ValueError("A live start used an unbound or changed host artifact")
        if not record["schedulerVolume"].startswith("works-sdk416-"):
            raise ValueError("A parity start did not own its isolated Scheduler fleet")
        if record.get("actorType") != "WorksSdkReminderActor":
            raise ValueError("A parity start did not consume the SDK reminder actor")
        if record.get("readinessPassed") is not True:
            raise ValueError("A selected host that never passed readiness cannot qualify as a live start")
        modules = record.get("runtimeModules", [])
        if not {"eventstore", "works", "eventstore-operations", "eventstore-admin"}.issubset({module["resource"] for module in modules}):
            raise ValueError("A live start did not bind the complete runtime module set")
        for module in modules:
            if module.get("configuration") != "Release" or Path(module["entry"]).parent.name != "release":
                raise ValueError("A qualifying live module must bind its actual Release configuration and output")
            if sha256(module["entry"]) != module["entrySha256"].lower() or not module["sdkAssemblies"]:
                raise ValueError("A live module used an unbound deployment artifact")
            for sdk in module["sdkAssemblies"]:
                if sdk["version"] != "3.124.0.0" or sha256(sdk["path"]) != sdk["sha256"].lower():
                    raise ValueError("A live module used an unbound or unpublished runtime SDK assembly")
        sdk_assemblies = record.get("sdkAssemblies", [])
        if not sdk_assemblies:
            raise ValueError("A live start did not bind its loaded SDK assemblies")
        for sdk in sdk_assemblies:
            if not sdk["name"].startswith("Hexalith.EventStore.") or sdk["version"] != "3.124.0.0" or sha256(sdk["path"]) != sdk["sha256"].lower():
                raise ValueError("A live start used an unbound or unpublished SDK assembly")
        if record.get("role") == "platform-target":
            if binary.resolve() != assembly.resolve() or Path(record["directory"]).resolve() != platform.resolve():
                raise ValueError("A target start used another host or source root")
        elif record.get("role") == "retained-rollback":
            if binary.resolve() == assembly.resolve() or "Hexalith.Works.AppHost" not in binary.name:
                raise ValueError("Rollback did not select the retained Works host")
        else:
            raise ValueError("An unknown host role cannot qualify parity")
    return records


# Each row must bind real target observations and complete restored-fleet evidence.
# Supporting unit suites never supply these phase records.
PHASES = {
    "R1": ["platform-historical-lifecycle-tree-concurrent-claim-persisted", "complete-supported-process-coverage"],
    "R2": ["periodic-writers-disabled-readiness-degraded", "disabled-recovery-otlp-alert-export", "durable-recovery-alert-export-and-clear"],
    "R3": ["platform-named-conflict-durable-quarantine-with-unchanged-family", "platform-named-redelivery-capture-through-commit-complete-family", "platform-executor-filtering-and-origin-tenant-publish-refusals"],
    "R4": ["platform-named-redelivery-capture-through-commit-complete-family"],
    "R5": ["platform-historical-lifecycle-tree-concurrent-claim-persisted", "platform-executor-filtering-and-origin-tenant-publish-refusals", "platform-query-and-explicit-receipt-replay"],
    "R6": ["platform-two-host-ha-lost-firing-stale-callback-recovery", "concurrent-sdk-reminder-reissue-after-lost-scheduler-wakeup", "cascade-expiry-restart-receipt-and-checkpoint"],
    "R7": ["paged-process-candidates-pre-restart", "paged-process-restart-and-shared-store-owner-fencing", "complete-supported-process-coverage"],
    "R8": ["protected-scoped-disposition-audit-before-mutation", "durable-recovery-alert-export-and-clear"],
    "R9": ["successful-callback-no-periodic-recovery", "complete-supported-process-coverage"],
    "R10": ["platform-two-host-ha-lost-firing-stale-callback-recovery", "platform-executor-filtering-and-origin-tenant-publish-refusals", "protected-scoped-disposition-audit-before-mutation"],
    "R11": ["five-child-persisted-target-receipts", "platform-query-and-explicit-receipt-replay"],
}
RESTORE_PHASE = "complete-restore-retained-host-query-and-receipt-replay"


def bind_persisted_outcomes(path, rows, starts):
    records = [json.loads(line) for line in Path(path).read_text().splitlines() if line]
    by_phase = {record["phase"]: record for record in records}
    required = {phase for phases in PHASES.values() for phase in phases}
    required.update(["complete-platform-fleet-snapshot", RESTORE_PHASE, "platform-historical-restart-replay-and-family", "platform-two-host-ha-lost-firing-stale-callback-recovery"])
    missing = sorted(required - set(by_phase))
    if missing:
        raise ValueError("Required target outcomes did not execute: " + ", ".join(missing))
    if not any(record.get("role") == "retained-rollback" for record in starts):
        raise ValueError("No retained-host restore start was observed")
    for key, row in rows.items():
        row["evidence"] = [{"phase": phase, "sha256": hashlib.sha256(json.dumps(by_phase[phase], sort_keys=True).encode()).hexdigest()}
                           for phase in PHASES[key] + [RESTORE_PHASE]]
        row["requiredProofs"] = {name: True for name in ("persisted", "negative", "restart", "rollback")}
        row["status"] = "passed"
    return {"complete": True, "snapshot": by_phase["complete-platform-fleet-snapshot"]["state"],
            "restored": by_phase[RESTORE_PHASE]["state"], "evidenceSha256": sha256(path)}


def source_snapshot(root, allow_archive=False):
    """Record source provenance independently of public NuGet archive identity."""
    root = Path(root)
    files = [path for path in (root / "src").rglob("*")
             if path.is_file() and path.suffix in (".cs", ".csproj")
             and not {"bin", "obj"}.intersection(path.relative_to(root).parts)]
    files += [root / name for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props") if (root / name).is_file()]
    inventory = {path.relative_to(root).as_posix(): sha256(path) for path in sorted(files)}
    commit = None
    try:
        owner = subprocess.check_output(["git", "-C", str(root), "rev-parse", "--show-toplevel"], text=True, stderr=subprocess.DEVNULL).strip()
        if Path(owner).resolve() != root.resolve():
            raise ValueError("Source directory is not its own Git checkout: " + str(root))
        commit = subprocess.check_output(["git", "-C", str(root), "rev-parse", "HEAD"], text=True, stderr=subprocess.DEVNULL).strip()
    except (subprocess.CalledProcessError, ValueError):
        if not allow_archive:
            raise ValueError("Source directory requires an owning Git checkout unless an explicit archive root was selected: " + str(root))
    return {"root": str(root), "commit": commit, "provenance": "owning Git checkout" if commit else "explicit filesystem archive; no Git HEAD",
            "sourceFiles": inventory, "inventorySha256": hashlib.sha256(json.dumps(inventory, sort_keys=True).encode()).hexdigest()}


def require_published_runtime_source(snapshot, published_producers):
    """Runtime-built SDK code must match the published producer, regardless of checkout HEAD metadata."""
    commits = {producer["repositoryCommit"] for producer in published_producers.values()}
    if len(commits) != 1:
        raise ValueError("Published SDK archives do not identify one compatible producer commit")
    commit = commits.pop()
    archive = subprocess.check_output(["git", "-C", snapshot["root"], "archive", commit, "src",
                                       "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"])
    with tarfile.open(fileobj=io.BytesIO(archive)) as source:
        published = {entry.name: hashlib.sha256(source.extractfile(entry).read()).hexdigest()
                     for entry in source.getmembers() if entry.isfile()
                     and (entry.name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props")
                          or Path(entry.name).suffix in (".cs", ".csproj"))}
    if snapshot["sourceFiles"] != published:
        changed = sorted(name for name in snapshot["sourceFiles"].keys() | published.keys()
                         if snapshot["sourceFiles"].get(name) != published.get(name))
        raise ValueError("Runtime SDK differs from published producer " + commit + ": " + ", ".join(changed))
    return {"repositoryCommit": commit, "sourceInventorySha256": snapshot["inventorySha256"],
            "checkoutCommit": snapshot["commit"], "matchesPublishedProducer": True}


def run_verification(args):
    platform = Path(__file__).resolve().parents[1]
    works = args.works_root.resolve()
    runtime_sdk = args.runtime_sdk_root.resolve() if args.runtime_sdk_root else works / "references/Hexalith.EventStore"
    artifacts = args.artifacts_path.resolve() if args.artifacts_path else Path(tempfile.mkdtemp(prefix="works-platform-proof-"))
    if artifacts.exists() and any(artifacts.iterdir()):
        raise ValueError("Artifacts directory must be empty; existing evidence cannot be reused or overwritten")
    artifacts.mkdir(parents=True, exist_ok=True)
    summary = {"status": "incomplete", "scope": "disposable-synthetic", "commands": [], "checks": {}, "rows": {},
               "rollback": {"complete": False, "missing": "Platform-write -> complete fleet restore -> retained Works host queries and receipt replay"}}

    def save():
        (artifacts / "ledger.json").write_text(json.dumps(summary, indent=2) + "\n")

    def run(name, command, cwd=platform, environment=None, expected_refusal=None):
        log = artifacts / (name + ".log")
        entry = {"name": name, "command": [str(value) for value in command], "cwd": str(cwd), "log": log.name}
        summary["commands"].append(entry)
        save()
        print(name + ": " + str(log), flush=True)
        with log.open("w") as output:
            result = subprocess.run(entry["command"], cwd=cwd, env=environment, stdout=output, stderr=subprocess.STDOUT, timeout=1800)
        entry["exitCode"] = result.returncode
        entry["sha256"] = sha256(log)
        save()
        if expected_refusal:
            if result.returncode != 1 or expected_refusal not in log.read_text():
                raise ValueError(name + ": required closed refusal was not preserved")
        elif result.returncode:
            raise ValueError(name + " failed; see " + str(log))

    def build(name, project, configuration, root, output=None, extra=()):
        command = ["dotnet", "build", project, "-c", configuration, "-p:TreatWarningsAsErrors=true",
                   "-p:NuGetAudit=false", "-p:Version=3.124.0", "-p:MinVerVersionOverride=3.124.0",
                   "-p:MSBuildWarningsAsErrors=MSB3277",
                   "-p:UseHexalithProjectReferences=" + ("true" if configuration == "Debug" else "false"),
                   "-p:UseNuGetDeps=" + ("false" if configuration == "Debug" else "true")]
        if output:
            command += ["--artifacts-path", str(output)]
        command += ["-p:AspireUseCliBundle=true"] if project.endswith(".cs") else ["-m:1"]
        run(name, command + list(extra), root)

    for row, (title, package, api, consumer) in ROWS.items():
        consumer_path = platform / consumer[2:] if consumer.startswith("P/") else works / consumer
        summary["rows"][row] = {"title": title, "status": "open", "producer": {"package": package, "version": "3.124.0", "api": api},
            "consumer": consumer, "consumerRoot": "Platform" if consumer.startswith("P/") else "Works", "consumerSha256": sha256(consumer_path) if consumer_path.is_file() else None,
            "requiredProofs": {"persisted": False, "negative": False, "restart": False, "rollback": False}}
    summary["rows"]["R8"]["additionalSeams"] = {"package": "Hexalith.EventStore.Client", "version": "3.124.0", "api": "IProcessDispositionAuthorizer/IProcessDispositionAuditSink"}
    summary["rows"]["R10"]["configurationConsumers"] = [{"path": str(path.relative_to(platform)), "sha256": sha256(path)}
        for path in sorted((platform / "DaprComponents").rglob("*.yaml"))
        ] + [{"path": str(path.relative_to(platform)), "sha256": sha256(path)} for path in
            [platform / "config/works-production-profile.schema.json", platform / "src/Hexalith.Platform.Composition/WorksProductionProfile.cs"]]
    summary["sourceProducers"] = {"platformDebugSdk": runtime_source_snapshot(runtime_sdk) if args.runtime_sdk_root is not None else source_snapshot(platform / "references/Hexalith.EventStore"),
        "worksDebugAndRuntimeSdk": runtime_source_snapshot(runtime_sdk) if args.runtime_sdk_root is not None else source_snapshot(runtime_sdk)}
    platform_consumers = [platform / "apphost.cs", *sorted((platform / "src").rglob("*.cs")),
                          *sorted((platform / "src").rglob("*.csproj"))]
    summary["platformConsumerSources"] = {str(path.relative_to(platform)): sha256(path) for path in platform_consumers
        if not {"obj", "bin"}.intersection(path.relative_to(platform).parts)}
    summary["worksConsumerSource"] = source_snapshot(works)
    summary["verifierSha256"] = sha256(Path(__file__))
    summary["verifierHelpers"] = {name: sha256(Path(__file__).with_name(name)) for name in
        ["verify_works_producer.py", "test_verify_works_host.py", "test_verify_works_producer.py"]}
    save()
    try:
        run("evidence-gate-tests", ["python3", "-m", "unittest", "test_verify_works_host", "test_verify_works_producer"], platform / "eng")
        run("agents-full-refusal", ["bash", "eng/verify-agents-host.sh", "--artifacts-path", str(artifacts / "agents-full")],
            expected_refusal="DependencyNotAvailable: EXT-HOST-1")
        summary["checks"]["agentsFull"] = "expected-unavailable; not a Works prerequisite"
        run("agents-local-scaffold", ["bash", "eng/verify-agents-host.sh", "--mode", "LocalScaffold", "--artifacts-path", str(artifacts / "agents-local")])
        # These tests locate their checkout through artifact ancestors. Keep their private
        # outputs under Platform while avoiding shared default bin/obj deployment directories.
        (platform / "artifacts").mkdir(exist_ok=True)
        platform_test_output = Path(tempfile.mkdtemp(prefix="works-parity-tests-", dir=platform / "artifacts"))
        summary["platformTestsArtifacts"] = str(platform_test_output)
        for configuration in ("Debug", "Release"):
            build("platform-tests-build-" + configuration.lower(), "tests/Hexalith.Platform.Works.Tests/Hexalith.Platform.Works.Tests.csproj", configuration, platform,
                  platform_test_output / configuration.lower(),
                  extra=["-p:HexalithEventStoreRoot=" + str(runtime_sdk), "-p:HexalithEventStoreVersion=3.124.0"] if args.runtime_sdk_root is not None else [])
            executable = platform_test_output / configuration.lower() / "bin/Hexalith.Platform.Works.Tests" / configuration.lower() / "Hexalith.Platform.Works.Tests"
            xml = artifacts / ("platform-" + configuration.lower() + ".xml")
            run("platform-tests-" + configuration.lower(), [executable, "-noLogo", "-result-xml", xml])
            summary["checks"][configuration] = require_passed_tests(xml)
            if configuration == "Debug":
                summary["platformDebugSdkAssemblies"] = {path.name: sha256(path) for path in
                    sorted(executable.parent.glob("Hexalith.EventStore.*.dll"))}
        host_output, consumer_output = artifacts / "host", artifacts / "consumer"
        build("platform-file-host-release", "./apphost.cs", "Release", platform, host_output)
        # Qualify against the target's adapter in an isolated output. The retained Works graph keeps its own pin.
        catalog = ET.parse(platform / "references/Hexalith.Builds/Props/Directory.Packages.props").getroot()
        adapter = next(item.text for item in catalog.findall(".//HexalithAspireHostingDaprVersion") if not item.get("Condition"))
        build("works-consumer-debug", "tests/Hexalith.Works.IntegrationTests/Hexalith.Works.IntegrationTests.csproj", "Debug", works, artifacts / "consumer-debug",
              ["-p:HexalithAspireHostingDaprVersion=" + adapter, "-p:PlatformWorksRuntimeRoot=" + str(platform),
               "-p:HexalithEventStoreRoot=" + str(runtime_sdk), "-p:HexalithEventStoreVersion=3.124.0"])
        build("works-consumer-release", "tests/Hexalith.Works.IntegrationTests/Hexalith.Works.IntegrationTests.csproj", "Release", works, consumer_output,
              ["-p:HexalithAspireHostingDaprVersion=" + adapter, "-p:PlatformWorksRuntimeRoot=" + str(platform),
               "-p:HexalithEventStoreRoot=" + str(runtime_sdk), "-p:HexalithEventStoreVersion=3.124.0"])
        supporting_xml = artifacts / "works-consumer-supporting.xml"
        supporting_classes = ["WorksSdkPartitionedRebuildTests", "WorksSdkProjectionQueryTests",
                              "WorkItemSharedProjectionRebuildHandlerTests", "WorksSdkProtectedHttpTests",
                              "WorksQueryPolicyDeadlineTests", "WorksSdkProofRedisOwnershipTests",
                              "WorksPlatformDispositionProbeTests", "WorksPlatformSelectedSdkPathsTests",
                              "WorksPlatformFleetRestoreTests"]
        supporting_command = [consumer_output / "bin/Hexalith.Works.IntegrationTests/release/Hexalith.Works.IntegrationTests", "-noLogo"]
        for name in supporting_classes:
            supporting_command += ["-class", "Hexalith.Works.IntegrationTests." + name]
        run("works-consumer-supporting", supporting_command + ["-result-xml", supporting_xml], works)
        summary["checks"]["supportingConsumer"] = require_passed_tests(supporting_xml,
            required_types=["Hexalith.Works.IntegrationTests." + name for name in supporting_classes])
        summary["worksDebugSdkAssemblies"] = {path.name: sha256(path) for path in
            sorted((artifacts / "consumer-debug/bin/Hexalith.Works.IntegrationTests/debug").glob("Hexalith.EventStore.*.dll"))}
        consumer_directory = consumer_output / "bin/Hexalith.Works.IntegrationTests/release"
        summary["consumer"] = {"configuration": "Release", "artifacts": {str(path): sha256(path) for path in
            [consumer_directory / "Hexalith.Works.IntegrationTests", consumer_directory / "Hexalith.Works.IntegrationTests.dll",
             consumer_directory / "Hexalith.Works.IntegrationTests.deps.json", consumer_directory / "Hexalith.Works.IntegrationTests.runtimeconfig.json"]}}
        assembly = host_output / "bin/release/apphost.dll"
        summary["host"] = {"source": str(platform / "apphost.cs"), "sourceSha256": sha256(platform / "apphost.cs"),
                           "assembly": str(assembly), "sha256": sha256(assembly), "configuration": "Release"}
        summary["platformAdapterAssemblies"] = {str(path): sha256(path) for path in [
            assembly.parent / "Hexalith.Platform.Composition.dll", assembly.parent / "Hexalith.Platform.Works.Runtime.dll",
            consumer_output / "bin/Hexalith.Works.IntegrationTests/release/Hexalith.Platform.Works.Runtime.dll"]}
        packages = json.loads((consumer_output / "obj/Hexalith.Works.IntegrationTests/project.assets.json").read_text())
        producer_evidence = {}
        for key, library in packages["libraries"].items():
            if not key.startswith("Hexalith.EventStore.") or library["type"] != "package":
                continue
            package, version = key.split("/")
            if version != "3.124.0":
                raise ValueError("Unexpected published producer: " + key)
            folder = next(Path(root) / library["path"] for root in packages["packageFolders"] if (Path(root) / library["path"]).exists())
            artifact = folder / (package.lower() + "." + version + ".nupkg")
            metadata = json.loads((folder / ".nupkg.metadata").read_text())
            if metadata.get("source") != "https://api.nuget.org/v3/index.json":
                raise ValueError("Published producer lacks public NuGet provenance: " + package)
            with zipfile.ZipFile(artifact) as archive:
                nuspec_name = next(name for name in archive.namelist() if name.endswith(".nuspec"))
                repository = ET.fromstring(archive.read(nuspec_name)).find(".//{*}repository")
            if repository is None or not repository.get("commit"):
                raise ValueError("Published producer lacks a repository commit: " + package)
            producer_evidence[package] = {"version": version, "sha256": sha256(artifact), "source": metadata["source"],
                "nugetSha512": library.get("sha512"), "repositoryCommit": repository.get("commit"), "repositoryUrl": repository.get("url")}
        summary["publishedProducers"] = producer_evidence
        fixtures = list((works / "tests/Hexalith.Works.IntegrationTests").glob("WorksPlatform*.cs")) + [
            works / "tests/Hexalith.Works.IntegrationTests" / name for name in
            ["WorksSdkRecoveryPipelineSmokeTests.cs", "WorksSdkProofHostingStartup.cs", "WorksSdkProofRedis.cs",
             "WorksAppHostSelection.cs", "WorksAppHostSmokeHarness.cs", "WorksSdkPartitionedRebuildTests.cs",
             "WorksSdkProjectionQueryTests.cs", "WorksSdkProtectedHttpTests.cs", "WorkItemSharedProjectionRebuildHandlerTests.cs",
             "WorksQueryPolicyDeadlineTests.cs", "WorksSdkProofRedisOwnershipTests.cs"]]
        fixtures.append(works / "src/Hexalith.Works.AppHost/ProjectMetadataPaths.cs")
        summary["fixtureSources"] = {str(path.relative_to(works)): sha256(path) for path in sorted(fixtures)}
        identity_root = works / "references/Hexalith.Platform/src/Hexalith.Platform.Identity"
        summary["identityProvenance"] = {"scope": "explicit-test-only-source; no published Platform.Identity package",
            "project": str(identity_root / "Hexalith.Platform.Identity.csproj"),
            "projectSha256": sha256(identity_root / "Hexalith.Platform.Identity.csproj"),
            "qualifiedAssemblySha256": sha256(consumer_output / "bin/Hexalith.Works.IntegrationTests/release/Hexalith.Platform.Identity.dll")}
        summary["dependencyModes"] = {"Debug": "sibling source project graph; independently built and hashed",
            "Release": "public NuGet3.124.0 consumer graph plus explicitly named test-only Platform.Identity and Platform runtime source"}
        for row in summary["rows"].values():
            producer = producer_evidence.get(row["producer"]["package"])
            if producer is None or row["consumerSha256"] is None:
                raise ValueError("A row has an absent published producer or consumer: " + row["title"])
            row["producer"].update(producer)
            if "additionalSeams" in row:
                row["additionalSeams"].update(producer_evidence[row["additionalSeams"]["package"]])
        save()
        summary["runtimeProducerQualification"] = (qualify_runtime_source if args.runtime_sdk_root is not None else require_published_runtime_source)(
            summary["sourceProducers"]["worksDebugAndRuntimeSdk"], producer_evidence)
        save()
        # These projects run through dotnet run at host startup and are not all consumer references.
        # Prepare their exact owned output graph serially, so a clean checkout never relies
        # on coincidental prior builds or races concurrent SDK/Commons reference-assembly writes.
        runtime_projects = [works / "src/Hexalith.Works/Hexalith.Works.csproj", *[
            runtime_sdk / "src" / name / (name + ".csproj") for name in
            ("Hexalith.EventStore", "Hexalith.EventStore.Operations", "Hexalith.EventStore.Admin.Server.Host")]]
        runtime_projects.append(works / "src/Hexalith.Works.AppHost/Hexalith.Works.AppHost.csproj")
        runtime_output = artifacts / "runtime"
        runtime_properties = ["-p:Version=3.124.0", "-p:MinVerVersionOverride=3.124.0",
            "-p:UseHexalithProjectReferences=false", "-p:UseNuGetDeps=true",
            "-p:HexalithEventStoreRoot=" + str(runtime_sdk), "-p:HexalithEventStoreVersion=3.124.0",
            "-p:HexalithAspireHostingDaprVersion=" + adapter]
        runtime_directories = []
        for index, project in enumerate(runtime_projects):
            build("runtime-release-prebuild-" + str(index), str(project), "Release", works, runtime_output, extra=runtime_properties)
            runtime_directories.append(runtime_output / "bin" / project.stem / "release")
        summary["runtimeArtifacts"] = {str(path): sha256(path) for directory in runtime_directories
            for path in sorted(directory.rglob("*")) if path.is_file()}
        save()
        if args.prepare_only:
            summary["status"] = "preparation-passed; parity-incomplete"
            return 0
        environment = os.environ.copy()
        runtime_environment = {"WORKS_PLATFORM_APPHOST_ASSEMBLY": str(assembly), "WORKS_PLATFORM_APPHOST_SHA256": sha256(assembly),
            "WORKS_PLATFORM_ROOT": str(platform), "WORKS_PLATFORM_WORKS_ROOT": str(works),
            "Platform__Works__SourceRoot": str(works), "Platform__Identity__Enabled": "false",
            "Version": "3.124.0", "MinVerVersionOverride": "3.124.0",
            "ArtifactsPath": str(runtime_output), "UseArtifactsOutput": "true", "WORKS_PLATFORM_RUNTIME_ARTIFACTS": str(runtime_output),
            "UseHexalithProjectReferences": "false", "UseNuGetDeps": "true",
            "HexalithEventStoreRoot": str(runtime_sdk), "HexalithEventStoreVersion": "3.124.0", "HexalithAspireHostingDaprVersion": adapter,
            "Platform__Works__EventStoreSourceRoot": str(runtime_sdk), "WORKS_PLATFORM_SDK_SOURCE_ROOT": str(runtime_sdk),
            "WORKS_PLATFORM_HOST_EVIDENCE": str(artifacts / "host-starts.jsonl"),
            "WORKS_SDK_PROOF_EVIDENCE_PATH": str(artifacts / "persisted-states.jsonl"), "WORKS_APPHOST_TEST_STARTUP_TIMEOUT_SECONDS": "180"}
        environment.update(runtime_environment)
        summary["runtimeEnvironment"] = runtime_environment
        save()
        executable = consumer_output / "bin/Hexalith.Works.IntegrationTests/release/Hexalith.Works.IntegrationTests"
        xml = artifacts / "platform-recovery.xml"
        run("platform-recovery", [executable, "-noLogo", "-class", "Hexalith.Works.IntegrationTests.WorksSdkRecoveryPipelineSmokeTests", "-result-xml", xml], works, environment)
        summary["checks"]["liveRecovery"] = require_passed_tests(xml,
            ["SuccessfulCallbacksRegisterSdkReminderAndProcessWithoutPeriodicRecovery", "SdkRemindersAndCascadeConvergeThroughPersistedReceiptsAcrossRestart"])
        historical_xml = artifacts / "platform-historical.xml"
        run("platform-historical", [executable, "-noLogo", "-method", "Hexalith.Works.IntegrationTests.WorksPlatformHistoricalPipelineSmokeTests.PlatformLifecycleTreeClaimFilteringAndSecuritySurviveRestart", "-result-xml", historical_xml], works, environment)
        summary["checks"]["liveHistorical"] = require_passed_tests(historical_xml, ["PlatformLifecycleTreeClaimFilteringAndSecuritySurviveRestart"])
        ha_xml = artifacts / "platform-ha.xml"
        run("platform-ha", [executable, "-noLogo", "-method", "Hexalith.Works.IntegrationTests.WorksPlatformHaPipelineSmokeTests.TwoWorksHostsRecoverLostFiringAfterPrimaryStopsWithoutDuplicateEffect", "-result-xml", ha_xml], works, environment)
        summary["checks"]["liveHa"] = require_passed_tests(ha_xml, ["TwoWorksHostsRecoverLostFiringAfterPrimaryStopsWithoutDuplicateEffect"])
        summary["actualStarts"] = verify_host_records(artifacts / "host-starts.jsonl", assembly, platform)
        for row in summary["rows"].values():
            row["targetCommands"] = [entry for entry in summary["commands"] if entry["name"] in ("platform-recovery", "platform-historical", "platform-ha")]
        for source in summary["sourceProducers"].values():
            current = runtime_source_snapshot(source["root"]) if source["commit"] is None else source_snapshot(source["root"])
            if current["commit"] != source["commit"] or current["inventorySha256"] != source["inventorySha256"]:
                raise ValueError("A qualified SDK source graph changed during execution: " + source["root"])
        works_source = source_snapshot(works)
        if works_source["commit"] != summary["worksConsumerSource"]["commit"] or works_source["inventorySha256"] != summary["worksConsumerSource"]["inventorySha256"]:
            raise ValueError("The qualified Works consumer source graph changed during execution")
        for source, digest in summary["platformConsumerSources"].items():
            if sha256(platform / source) != digest:
                raise ValueError("A qualified Platform consumer changed during execution: " + source)
        for binary, digest in summary["runtimeArtifacts"].items():
            if sha256(binary) != digest:
                raise ValueError("A qualified runtime artifact changed during execution: " + binary)
        for binary, digest in summary["consumer"]["artifacts"].items():
            if sha256(binary) != digest:
                raise ValueError("A qualified Works consumer artifact changed during execution: " + binary)
        for binary, digest in summary["platformAdapterAssemblies"].items():
            if sha256(binary) != digest:
                raise ValueError("A qualified Platform adapter assembly changed during execution: " + binary)
        if sha256(Path(__file__)) != summary["verifierSha256"]:
            raise ValueError("The verifier changed during execution")
        for name, digest in summary["verifierHelpers"].items():
            if sha256(Path(__file__).with_name(name)) != digest:
                raise ValueError("A verifier helper changed during execution: " + name)
        for source, digest in summary["fixtureSources"].items():
            if sha256(works / source) != digest:
                raise ValueError("A qualified fixture changed after the consumer build: " + source)
        summary["rollback"] = bind_persisted_outcomes(artifacts / "persisted-states.jsonl", summary["rows"], summary["actualStarts"])
        summary["status"] = "passed-disposable-synthetic-parity-and-rollback"
        return 0
    except (ValueError, OSError, subprocess.TimeoutExpired, subprocess.CalledProcessError) as error:
        summary["failure"] = str(error)
        print(str(error), flush=True)
        return 1
    finally:
        save()
        print("Evidence: " + str(artifacts / "ledger.json"), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--works-root", type=Path, default=Path(__file__).resolve().parents[2] / "works")
    parser.add_argument("--artifacts-path", type=Path)
    parser.add_argument("--runtime-sdk-root", type=Path, help="Explicit producer source directory; independently qualified against official commit trees and exact Git blobs")
    parser.add_argument("--prepare-only", action="store_true", help="Run build/profile checks only; never accepts parity")
    args = parser.parse_args()
    return run_verification(args)


if __name__ == "__main__":
    raise SystemExit(main())
