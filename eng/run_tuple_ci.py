"""Build and test one recorded Platform tuple in package-only GitHub CI; never grant candidate acceptance."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import datetime
import platform
import signal
import tempfile
import xml.etree.ElementTree as ET
import zipfile

from prepare_builds_artifacts import source_state
from builds_tuple import load_binding, resolve_builds_root, validate_owner_pins, validate_recorded_source, verify_catalog

ROOT = Path(__file__).resolve().parents[1]
CURRENT_COLLECTOR_SELECTORS = frozenset("Hexalith.Platform.Composition.Tests." + name for name in (
    "PlatformCurrentPackageArtifactCohortTests", "PlatformMcpCliCatalogSelectionTests",
    "PlatformCurrentOwnerTargetFrameworkTests", "PlatformProductionModelClosureTests",
    "GatewayExtensionRegistrationTests",
    "EventStoreExtensionCompositionTests.PlatformHostRegistersExtensionsBeforeTheServerStartsItsCatalogBootstrapAsync",
    "EventStoreExtensionCompositionTests.PlatformHostLoadsTheTestExtensionAndReportsItInReadinessAsync",
    "EventStoreExtensionCompositionTests.DeclarationDriftStopsTheHostNamingDeclaredAndActualValuesAsync",
))


def collector_for(group, suite):
    """Keep current production/artifact controls on the actual current collector."""
    if suite == "current42":
        selections = [selector in CURRENT_COLLECTOR_SELECTORS for selector in group["selectors"]]
        if any(selections) != all(selections):
            raise ValueError("Current and retained collector selectors must be separate explicit groups.")
        expected = "current" if all(selections) else "retained"
        if group.get("collector") != expected:
            raise ValueError("Every current profile group requires its exact declared collector context.")
        return expected
    expected = "retained" if suite == "base42" else "current"
    if group.get("collector") not in (None, expected):
        raise ValueError("The selected suite cannot substitute another collector context.")
    return expected


def require_collector_framework(document, expected):
    """Refuse a mixed or changed target before any collector may execute tests."""
    if expected not in ("net10.0", "net11.0") or set(document["project"]["frameworks"]) != {expected}:
        raise ValueError("Collector assets differ from their exact recorded target framework.")


def require_collector_execution(counts, expected):
    """The executed assembly and actual runtime must match the selected collector."""
    import re
    framework = ".NETCoreApp,Version=v" + expected.removeprefix("net")
    runtime = re.search(r"(?:^|\s)\.NET ([0-9]+)\.[0-9]+\.[0-9]+(?:\s|$)", counts.get("environment", ""))
    if counts.get("target-framework") != framework or runtime is None \
            or runtime.group(1) != expected.removeprefix("net").split(".")[0]:
        raise ValueError("Executed tests do not match their collector target and actual managed runtime.")


def require_retained_collector_binding(binding, raw):
    """Only the genuine frozen signed4.47 producer may collect retained SDK4.2 tests."""
    from prepare_tuple_sources import FROZEN
    frozen = FROZEN["package-reader"]
    if hashlib.sha256(raw).hexdigest() != frozen["hashes"]["eng/builds-tuple.json"] or json.loads(raw) != binding \
            or binding["publication"]["version"] != "4.47.0" \
            or binding["producer"]["sourceRevision"] != frozen["gitlinks"]["references/Hexalith.Builds"]:
        raise ValueError("The retained collector requires the exact authenticated signed4.47 tuple.")


def require_collector_source(root, source):
    """A receipt cannot substitute for the actual unchanged source and direct checkouts."""
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip()
    status = subprocess.check_output(["git", "status", "--porcelain", "--untracked-files=normal"], cwd=root, text=True)
    if revision != source or status.strip():
        raise ValueError("Collector source identity or clean recorded owner checkouts changed.")


def tuple_runtime_context(binding, suite, profile_sha256):
    """Select exact historical regression targets without changing current NET11 authority."""
    from prepare_tuple_sources import FROZEN
    publication = binding["publication"]
    source, version = binding["producer"]["sourceRevision"], publication["version"]
    if suite == "current":
        for name, tool_version in (("legacy", "4.44.3"), ("pre-extension", "4.45.0")):
            frozen = FROZEN[name]
            if source == frozen["gitlinks"]["references/Hexalith.Builds"] and version == tool_version \
                    and profile_sha256 == frozen["hashes"]["eng/tuple-ci-suites.json"]:
                return {"purpose": name + "-same-code-net10-regression", "targetFramework": "net10.0",
                        "toolVersion": version, "buildsSource": source, "profileSha256": profile_sha256,
                        "currentRuntimeQualification": False}
        raise ValueError("A retained runtime requires its exact frozen source, tool version and profile.")
    if suite not in ("current42", "base42", "extensions") or not version.startswith(("5.", "6.")):
        raise ValueError("Current runtime qualification requires the bound NET11 Builds generation.")
    return {"purpose": "current-net11-with-retained-public-sdk42", "targetFramework": "net11.0",
            "toolVersion": version, "buildsSource": source, "profileSha256": profile_sha256,
            "nativeAcceptance": False}


def validate_profile(profile):
    groups = profile.get("groups")
    if not isinstance(groups, list) or not groups:
        raise ValueError("Required profile has no groups.")
    for group in groups:
        if type(group.get("requiredCount")) is not int or group["requiredCount"] <= 0:
            raise ValueError("Each group requires a positive integer test count.")
        if group.get("filter") not in ["-class", "-method"] or not isinstance(group.get("selectors"), list) or not group["selectors"]:
            raise ValueError("Each group requires an explicit filter and nonempty selectors.")
        if any(not isinstance(name, str) or not name.strip() or name.startswith("-") for name in group["selectors"]):
            raise ValueError("Invalid test selector.")
    return sum(group["requiredCount"] for group in groups)


COMPOSITION_TEST_DEPENDENCIES = (
    "Hexalith.EventStore.Contracts.dll",
    "Hexalith.EventStore.ServiceDefaults.dll",
    "Hexalith.EventStore.Client.dll",
    "Hexalith.EventStore.DomainService.dll",
    "Hexalith.Tenants.Contracts.dll",
)


def snapshot_composition_test_outputs(assembly):
    """Observe actual test/output files and host context without granting cohort admission."""
    assembly = Path(assembly).resolve(strict=True)

    def identity(path):
        path = path.resolve(strict=True)
        return {"path": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}

    return {
        "os": platform.system(),
        "architecture": platform.machine(),
        "testAssembly": identity(assembly),
        "dependencies": {name: identity(assembly.parent / name) for name in COMPOSITION_TEST_DEPENDENCIES},
    }


def verify_composition_test_outputs_unchanged(record):
    """Retain the final observation and refuse changed/missing outputs or host context."""
    record["afterCapturedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    record["after"] = None
    record["unchanged"] = False
    record.pop("afterError", None)
    try:
        record["after"] = snapshot_composition_test_outputs(record["before"]["testAssembly"]["path"])
    except Exception as error:
        record["afterError"] = str(error)
        raise
    record["unchanged"] = record["before"] == record["after"]
    if not record["unchanged"]:
        raise ValueError("Composition test outputs or platform context changed during required tests.")


def sdk_suite(suite, version):
    """Select only the measured new SDK suite or the retained exact historical SDK controls."""
    if suite in ("extensions", "base42", "current42"):
        if version != "4.2.0":
            raise ValueError("The extension suite requires the measured EventStore 4.2.0 SDK.")
        return {"extensions": "eng/tuple-ci-extension-suites.json", "base42": "eng/tuple-ci-sdk42-base-suites.json", "current42": "eng/tuple-ci-sdk42-current-suites.json"}[suite], ["-p:HexalithEventStoreVersion=4.2.0"]
    if suite != "current" or version not in (None, "4.1.0"):
        raise ValueError("The retained suite accepts only its exact historical SDK or the recorded catalog default.")
    return "eng/tuple-ci-suites.json", [] if version is None else ["-p:HexalithEventStoreVersion=4.1.0"]


def verify_extension_catalog_pin(catalog, *, authenticated_body=None, expected_sha256=None, binding=None):
    """Retain old raw pins; independently qualify only the finite current named SDK4.2 view."""
    from current_catalog_view import require_catalog_body, verify_named_platform_catalog
    raw = Path(catalog).read_bytes() if authenticated_body is None else authenticated_body
    if authenticated_body is not None:
        require_catalog_body(raw, expected_sha256)
    document = ET.fromstring(raw)
    if document.findall(".//HexalithPlatformSdk42CatalogView"):
        if authenticated_body is None:
            raise ValueError("A named current catalog view requires its authenticated catalog Git body.")
        return verify_named_platform_catalog(raw, expected_sha256, binding=binding)
    pins = document.findall("./PropertyGroup/HexalithEventStoreVersion")
    if len(pins) != 1 or (pins[0].text or "").strip() != "4.2.0":
        raise ValueError("The extension SDK suite requires the single measured 4.2.0 owning catalog pin.")
    return {"kind": "retained-single-sdk42-catalog-pin", "runtimeAcceptance": False}


def sdk42_collecting_cohort(binding, suite):
    """Keep the measured SDK4.2 collector separate from the producer's newer defaults."""
    if suite not in ("current42", "base42", "extensions"):
        return None
    from current_catalog_view import catalog_profile
    if catalog_profile(binding) != "current-sdk51":
        return None
    return {"HexalithEventStoreVersion": "4.2.0", "HexalithTenantsVersion": "7.0.7",
            "HexalithMemoriesVersion": "3.0.27", "HexalithPartiesVersion": "3.1.1", "HexalithMcpCliVersion": "2.0.1"}


def verify_collecting_cohort(assets, cohort):
    """Require the actual public package graph, including every collected Memories body."""
    packages = {"Hexalith.EventStore.DomainService": cohort["HexalithEventStoreVersion"],
                "Hexalith.Tenants.Contracts": cohort["HexalithTenantsVersion"],
                "Hexalith.Tenants.Server": cohort["HexalithTenantsVersion"],
                "Hexalith.Memories.Contracts": cohort["HexalithMemoriesVersion"],
                "Hexalith.Memories.EventStore": cohort["HexalithMemoriesVersion"],
                "Hexalith.Memories.EventStoreExtensions": cohort["HexalithMemoriesVersion"],
                "Hexalith.Parties.Contracts": cohort["HexalithPartiesVersion"],
                "Hexalith.Parties.EventStoreExtensions": cohort["HexalithPartiesVersion"],
                "Hexalith.McpCli.Abstractions": cohort["HexalithMcpCliVersion"]}
    libraries = assets["libraries"]
    for name, version in packages.items():
        identities = [key for key in libraries if key.startswith(name + "/")]
        if identities != [name + "/" + version] or libraries[identities[0]].get("type") != "package":
            raise ValueError("The SDK4.2 collecting graph differs from its exact public cohort: " + name)
    sdk_packages = {name: item for name, item in libraries.items() if name.startswith("Hexalith.EventStore.")}
    if any(name.rpartition("/")[2] != cohort["HexalithEventStoreVersion"] or item.get("type") != "package"
           for name, item in sdk_packages.items()):
        raise ValueError("Every restored EventStore package must match the exact SDK4.2 collecting cohort.")
    return {"packages": packages, "eventStorePackages": sorted(sdk_packages), "scope": "Measured SDK4.2 collector only; no default SDK5.1 or native acceptance",
            "candidateAcceptance": False, "runtimeAcceptance": False}


def verify_historical_catalog_pin(catalog):
    """The 412/349 history must retain its actual owning SDK4.1 catalog, even if a build override is supplied."""
    pins = ET.parse(catalog).findall("./PropertyGroup/HexalithEventStoreVersion")
    if len(pins) != 1 or (pins[0].text or "").strip() != "4.1.0":
        raise ValueError("Historical controls require the single real SDK4.1 catalog pin; overrides cannot substitute.")


def verify_test_sdk(assembly, version):
    """Require real package dependencies for the selected isolated test build before invoking any cases."""
    if version is None:
        return
    dependencies = json.loads(Path(assembly).with_suffix(".deps.json").read_bytes())["libraries"]
    for name in COMPOSITION_TEST_DEPENDENCIES:
        if not name.startswith("Hexalith.EventStore."):
            continue
        identity = name.removesuffix(".dll") + "/" + version
        if dependencies.get(identity, {}).get("type") != "package":
            raise ValueError("The actual test binary does not resolve the selected SDK package: " + identity)


def _subprocess_environment(name, environment=None, *, metadata_token=None):
    """Keep the job token out of ordinary commands, including explicit environments."""
    child = dict(os.environ if environment is None else environment)
    child.pop("GH_TOKEN", None)
    if name in ("published-artifacts", "retained-published-artifacts") and metadata_token is not None:
        child["GH_TOKEN"] = metadata_token
    return child


def main():
    metadata_token = os.environ.pop("GH_TOKEN", None)
    try:
        _run_tuple_ci(metadata_token)
    finally:
        if metadata_token is not None:
            os.environ["GH_TOKEN"] = metadata_token


def _run_tuple_ci(metadata_token):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--artifacts", type=Path)
    parser.add_argument("--audit-only", action="store_true")
    parser.add_argument("--suite", choices=["current", "extensions", "base42", "current42"], default="current")
    parser.add_argument("--eventstore-sdk", choices=["4.1.0", "4.2.0"])
    args = parser.parse_args()
    profile_path, sdk_properties = sdk_suite(args.suite, args.eventstore_sdk)
    evidence = args.evidence.resolve()
    if evidence.exists():
        raise ValueError("Choose a new evidence directory.")
    evidence.mkdir(parents=True)
    result = {"kind": "tuple-ci-preflight", "candidateAcceptance": False, "status": "running", "tests": [], "commands": [], "suite": args.suite, "eventStoreSdk": args.eventstore_sdk, "historicalSdkRegression": args.suite == "current" and args.eventstore_sdk == "4.1.0"}

    def checkpoint():
        (evidence / "tuple-ci-result.json").write_text(json.dumps(result, indent=2) + "\n")

    def interrupted(signum, _frame):
        result["status"] = "cancelled"
        result["blocker"] = "CI command interrupted by signal " + str(signum)
        checkpoint()
        raise SystemExit(128 + signum)

    signal.signal(signal.SIGTERM, interrupted)
    signal.signal(signal.SIGINT, interrupted)

    def run(name, argv, *, cwd=ROOT, environment=None):
        command = {"name": name, "argv": argv, "cwd": str(cwd), "exitCode": None, "startedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "timeoutSeconds": 1200}
        if environment is not None:
            command["managedRuntimeRollForward"] = environment.get("DOTNET_ROLL_FORWARD")
        result["commands"].append(command)
        checkpoint()
        try:
            with (evidence / (name + ".stdout")).open("w") as stdout, (evidence / (name + ".stderr")).open("w") as stderr:
                child_environment = _subprocess_environment(name, environment, metadata_token=metadata_token)
                completed = subprocess.run(argv, cwd=cwd, env=child_environment, stdout=stdout, stderr=stderr, text=True, timeout=1200)
        except subprocess.TimeoutExpired:
            command["timedOut"] = True
            checkpoint()
            raise RuntimeError(name + " timed out; streamed output and start metadata are retained.")
        command["exitCode"] = completed.returncode
        command["finishedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        checkpoint()
        if completed.returncode:
            raise RuntimeError(name + " failed; retained command output is authoritative.")
        return (evidence / (name + ".stdout")).read_text()

    retained_workspace = None
    try:
        revision = run("revision", ["git", "rev-parse", "HEAD"]).strip()
        result["platformRevision"] = revision
        result["platformSourceState"] = source_state()
        result["python"] = sys.version
        result["platform"] = platform.platform()
        result["runner"] = {name: os.environ.get(name) for name in ["RUNNER_OS", "RUNNER_ARCH", "ImageOS", "ImageVersion", "GITHUB_RUN_ID", "GITHUB_RUN_ATTEMPT"]}
        run("dotnet-info", ["dotnet", "--info"])
        binding = load_binding()
        result["tupleBinding"] = binding
        runtime_context = None
        if not args.audit_only:
            runtime_context = tuple_runtime_context(binding, args.suite, hashlib.sha256((ROOT / profile_path).read_bytes()).hexdigest())
            result["runtimeBuildContext"] = runtime_context
            run("runtime-context-controls", [sys.executable, "-B", "eng/test_tuple_runtime_context.py"])
            run("collector-context-controls", [sys.executable, "-B", "eng/test_tuple_collectors.py"])
        version, source = binding["publication"]["version"], binding["producer"]["sourceRevision"]
        archive_sha256 = binding["publication"]["archiveSha256"]
        validate_owner_pins(binding, ROOT)
        pin = run("builds-pin", ["git", "rev-parse", "HEAD:references/Hexalith.Builds"]).strip()
        validate_recorded_source(binding, pin, result["platformSourceState"]["dirty"], require_clean=not args.audit_only)
        builds_root = resolve_builds_root(ROOT)
        catalog = verify_catalog(binding, builds_root)
        if args.suite in ("extensions", "base42", "current42"):
            catalog_identity = binding["producer"]["catalog"]
            catalog_body = subprocess.check_output(["git", "show", source + ":" + catalog_identity["path"]],
                                                   cwd=builds_root, timeout=30)
            result["extensionCatalogView"] = verify_extension_catalog_pin(catalog, authenticated_body=catalog_body,
                                                                           expected_sha256=catalog_identity["sha256"], binding=binding)
            (evidence / "authenticated-catalog.props").write_bytes(catalog_body)
        else:
            verify_historical_catalog_pin(catalog)
        profile = json.loads((ROOT / profile_path).read_bytes())
        required_total = validate_profile(profile)
        selected_collectors = {collector_for(group, args.suite) for group in profile["groups"]}
        if args.suite == "base42" and required_total != 97:
            raise ValueError("The SDK4.2 base suite must retain 92 finite controls, three actual entry cases and both owner model transport cases.")
        if args.suite == "current42" and required_total != 478:
            raise ValueError("The SDK4.2 current suite requires all 457 retained cases, seventeen current artifact controls, three explicit MCP selection controls and one private target-framework control (478 total).")
        if args.suite == "extensions" and required_total != 9:
            raise ValueError("The extension SDK suite must retain all nine required package cases.")
        if not args.audit_only:
            if os.environ.get("GITHUB_ACTIONS") != "true" or os.environ.get("CI") != "true":
                raise ValueError("Release execution belongs to GitHub CI; local use supports --audit-only.")
            if os.environ.get("PLATFORM_CANDIDATE_SHA") != revision:
                raise ValueError("The full selected candidate SHA must equal the actual checkout.")
            if result["platformSourceState"]["dirty"]:
                raise ValueError("Tracked source is dirty.")
        if args.artifacts:
            identity = json.loads((args.artifacts.resolve() / "artifact-identity.json").read_bytes())
            framework = "net11.0" if version.startswith(("5.", "6.")) else "net10.0"
            tool = args.artifacts.resolve() / ("published/tools/" + framework + "/any")
            if Path(identity["toolRoot"]).resolve() != tool or identity["version"] != version or identity["buildsRevision"] != source or identity.get("tupleBinding") != binding:
                raise ValueError("Artifact receipt names another tool root or tuple.")
            archive = args.artifacts.resolve() / ("hexalith.builds.module.cli." + version + ".nupkg")
            if hashlib.sha256(archive.read_bytes()).hexdigest() != archive_sha256:
                raise ValueError("Reused archive is not the approved signed package.")
            # Local graph diagnostics can reuse an already verified archive, but recheck every extracted byte.
            with zipfile.ZipFile(archive) as package:
                for name in package.namelist():
                    if not name.endswith("/") and (args.artifacts / "published" / name).read_bytes() != package.read(name):
                        raise ValueError("Extracted artifact changed: " + name)
            if not args.audit_only:
                raise ValueError("CI must prepare fresh signed artifacts instead of reusing a caller receipt.")
        else:
            tool = Path(run("published-artifacts", [sys.executable, str(ROOT / "eng/prepare_builds_artifacts.py"), "--output", str(evidence / "builds"), "--require-recorded"]).strip())
        result["toolRoot"] = str(tool)
        properties = ["-p:Configuration=Release", "-p:UseHexalithProjectReferences=false",
                      "-p:HexalithBuildsRoot=" + str(builds_root), "-p:HexalithBuildsPublishedToolRoot=" + str(tool), *sdk_properties]
        collecting_cohort = sdk42_collecting_cohort(binding, args.suite)
        if collecting_cohort is not None:
            properties.extend("-p:" + name + "=" + value for name, value in collecting_cohort.items()
                              if name != "HexalithEventStoreVersion")
            result["collectingPackageSelection"] = {"properties": collecting_cohort,
                "scope": "Explicit SDK4.2 regression collector; authenticated producer defaults remain unchanged",
                "candidateAcceptance": False, "runtimeAcceptance": False}
        projects = [ROOT / x.attrib["Path"] for x in ET.parse(ROOT / "Hexalith.Platform.slnx").iter("Project")]
        seen = {project.resolve() for project in projects}
        for number, project in enumerate(projects):
            evaluated = json.loads(run("graph-" + str(number), ["dotnet", "msbuild", str(project), "-nologo", *properties, "-getItem:ProjectReference,Reference"]))
            for reference in evaluated["Items"]["ProjectReference"]:
                if not Path(reference["FullPath"]).resolve().is_relative_to(ROOT):
                    raise ValueError("Foreign project reference: " + reference["FullPath"])
                child = Path(reference["FullPath"]).resolve()
                if child not in seen:
                    seen.add(child)
                    projects.append(child)
            for reference in evaluated["Items"]["Reference"]:
                if reference["Identity"].startswith("Hexalith.Builds.") and not Path(reference["HintPath"]).resolve().is_relative_to(tool):
                    raise ValueError("Builds assembly lies outside verified published closure.")
        if args.audit_only:
            result["projectGraphPaths"] = [str(x.relative_to(ROOT)) for x in projects]
            result["status"] = "graph-verified"
            return
        run("tuple-binding-controls", [sys.executable, "-B", "eng/test_builds_tuple.py"])
        if args.suite == "current42":
            run("historical-security-package-controls", [sys.executable, "-B", "eng/test_historical_security_packages.py"])
        run("composition-output-identity-controls", [sys.executable, "-B", "eng/test_tuple_output_identity.py"])
        run("binding-regressions", [sys.executable, "-B", "eng/test_host_builds_resolution.py"])
        file_catalog = catalog
        file_catalog_sha256 = None
        file_catalog_binding = ROOT / "eng/builds-tuple.json"
        if args.suite in ("extensions", "base42", "current42"):
            file_catalog = evidence / "authenticated-catalog.props"
            file_catalog_sha256 = binding["producer"]["catalog"]["sha256"]
        current_source = os.environ.get("CURRENT_PLATFORM_SHA")
        if current_source and current_source != revision:
            from file_host_catalog import prepare as prepare_file_host_catalog
            file_catalog, result["fileAppHostCatalog"] = prepare_file_host_catalog(
                ROOT, builds_root, current_source, evidence / "current-file-host-catalog")
            file_catalog_sha256 = result["fileAppHostCatalog"]["catalogSha256"]
            file_catalog_binding = file_catalog.parent / "builds-tuple.json"
        run("file-host-catalog-controls", [sys.executable, "-B", "eng/test_file_host_catalog.py"])
        run("file-host-pin-controls", [sys.executable, "-B", "eng/test_verify_platform_pins.py"])
        pin_arguments = [sys.executable, "eng/verify-platform-pins.py", "--catalog", str(file_catalog)]
        if file_catalog_sha256 is not None:
            pin_arguments.extend(["--catalog-sha256", file_catalog_sha256])
            pin_arguments.extend(["--producer-binding", str(file_catalog_binding)])
        run("pin-alignment", pin_arguments)
        run("restore", ["dotnet", "restore", "Hexalith.Platform.slnx", *properties])
        if collecting_cohort is not None:
            collecting_assets = ROOT / "tests/Hexalith.Platform.Composition.Tests/obj/project.assets.json"
            result["collectingPackageCohort"] = verify_collecting_cohort(json.loads(collecting_assets.read_bytes()), collecting_cohort)
        run("source-sdk-propagation-controls", [sys.executable, "-B", "eng/test_source_sdk_propagation.py", "--evidence", str(evidence / "source-sdk-metadata"), "--published-tool-root", str(tool)])
        assets = evidence / "assets"
        assets.mkdir()
        for number, project in enumerate(projects):
            path = project.parent / "obj/project.assets.json"
            payload = path.read_bytes()
            document = json.loads(payload)
            if runtime_context is not None and set(document["project"]["frameworks"]) != {runtime_context["targetFramework"]}:
                raise ValueError("Restored graph does not match its exact current or historical runtime context.")
            for name, library in document["libraries"].items():
                if library["type"] == "project":
                    source = (project.parent / library["msbuildProject"]).resolve()
                    if source not in seen or not source.is_relative_to(ROOT):
                        raise ValueError("Restored foreign/unevaluated source fallback: " + name)
            (assets / (str(number) + ".project.assets.json")).write_bytes(payload)
        run("build", ["dotnet", "build", "Hexalith.Platform.slnx", "--no-restore", "-c", "Release", "-m:1", *properties])
        result["requiredProfile"] = profile
        composition_project = ROOT / "tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj"
        if composition_project not in projects:
            raise ValueError("Composition test outputs are absent from the owner solution.")
        def measure_collector(name, root, project, selected_properties, selected_tool, selected_binding, expected_framework, source):
            measured = json.loads(run(name + "-test-path", ["dotnet", "msbuild", str(project), "-nologo",
                *selected_properties, "-getProperty:TargetPath,TargetFramework,ProjectAssetsFile"], cwd=root))["Properties"]
            framework = measured["TargetFramework"]
            if framework not in ("net10.0", "net11.0") or expected_framework is not None and framework != expected_framework:
                raise ValueError("The actual collector target differs from its selected runtime context.")
            assembly = Path(measured["TargetPath"]).resolve(strict=True)
            asset_path = Path(measured["ProjectAssetsFile"]).resolve(strict=True)
            require_collector_framework(json.loads(asset_path.read_bytes()), framework)
            verify_test_sdk(assembly, args.eventstore_sdk)
            return {"source": source, "root": str(root), "project": str(project), "assembly": str(assembly),
                "properties": selected_properties, "targetFramework": framework, "toolRoot": str(selected_tool),
                "toolVersion": selected_binding["publication"]["version"],
                "buildsSource": selected_binding["producer"]["sourceRevision"], "tupleBinding": selected_binding,
                "packageAssets": str(asset_path), "packageAssetsSha256": hashlib.sha256(asset_path.read_bytes()).hexdigest(),
                "nativeAcceptance": False}

        collectors = {"current": measure_collector("current", ROOT, composition_project, properties, tool, binding,
            runtime_context["targetFramework"] if runtime_context else None, revision)}
        if "retained" in selected_collectors:
            # The old controls must load the actual signed old producer in their own same-code NET10 workspace.
            retained_workspace = tempfile.TemporaryDirectory(prefix="platform-retained-reader-", dir=os.environ.get("RUNNER_TEMP"))
            isolated = Path(retained_workspace.name) / "workspace"
            receipt_path = evidence / "historical-reader-workspace.json"
            run("prepare-historical-reader", [sys.executable, "-B", "eng/prepare_tuple_sources.py", "reader-workspace",
                "--source", revision, "--workspace", str(isolated), "--output", str(receipt_path)])
            reader_receipt = json.loads(receipt_path.read_bytes())
            require_collector_source(isolated, reader_receipt["packageReaderSourceRevision"])
            retained_binding = load_binding(isolated / "eng/builds-tuple.json")
            committed_binding = subprocess.check_output(["git", "show", "HEAD:eng/builds-tuple.json"], cwd=isolated)
            require_retained_collector_binding(retained_binding, committed_binding)
            retained_tool = Path(run("retained-published-artifacts", [sys.executable,
                str(isolated / "eng/prepare_builds_artifacts.py"), "--output", str(evidence / "builds-retained"),
                "--require-recorded"], cwd=isolated).strip())
            retained_project = isolated / composition_project.relative_to(ROOT)
            retained_properties = ["-p:Configuration=Release", "-p:UseHexalithProjectReferences=false",
                "-p:HexalithBuildsRoot=" + str(isolated / "references/Hexalith.Builds"),
                "-p:HexalithBuildsPublishedToolRoot=" + str(retained_tool),
                "-p:PlatformPackageReaderWorkspaceRoot=" + str(isolated),
                "-p:HexalithEventStoreVersion=4.2.0", "-p:HexalithTenantsVersion=7.0.5",
                "-p:HexalithMemoriesVersion=3.0.25", "-p:HexalithPartiesVersion=3.1.0",
                "-p:ArtifactsPath=" + str(evidence / "retained-collector")]
            run("build-retained-collector", ["dotnet", "build", str(retained_project), "-c", "Release", "-m:1",
                *retained_properties], cwd=isolated)
            retained_assets = evidence / "retained-collector/obj/Hexalith.Platform.Composition.Tests/project.assets.json"
            selected = json.loads(retained_assets.read_bytes())["libraries"]
            for identity in ("Hexalith.Tenants.Contracts/7.0.5", "Hexalith.Tenants.Server/7.0.5", "Hexalith.Memories.Contracts/3.0.25", "Hexalith.Parties.Contracts/3.1.0"):
                if selected.get(identity, {}).get("type") != "package":
                    raise ValueError("The retained collector did not resolve its exact published package input: " + identity)
            retained_graphs = sorted((evidence / "retained-collector/obj").rglob("project.assets.json"))
            if not retained_graphs:
                raise ValueError("The retained collector has no measured restored graphs.")
            for path in retained_graphs:
                require_collector_framework(json.loads(path.read_bytes()), "net10.0")
            collectors["retained"] = measure_collector("retained", isolated, retained_project, retained_properties,
                retained_tool, retained_binding, "net10.0", reader_receipt["packageReaderSourceRevision"])
            retained_catalog = isolated / "references/Hexalith.Builds/Props/Directory.Packages.props"
            result["historicalReferenceCollector"] = {"workspace": reader_receipt,
                "readerInputCatalogSha256": hashlib.sha256(retained_catalog.read_bytes()).hexdigest(),
                "collectingBuildCatalogSha256": hashlib.sha256(retained_catalog.read_bytes()).hexdigest(),
                "packageAssets": str(retained_assets), "artifactIdentity": str(evidence / "builds-retained/artifact-identity.json"),
                "restoredGraphs": {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in retained_graphs},
                "scope": "Same-code signed4.47/NET10 regression with authenticated retained705/25/310 inputs; no current NET11 or native acceptance"}
        result["collectors"] = collectors
        snapshots = {}
        for name, collector in collectors.items():
            key = "compositionTestOutputs" if name == "current" else "retainedCompositionTestOutputs"
            snapshots[name] = result[key] = {"beforeCapturedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
                "before": snapshot_composition_test_outputs(collector["assembly"])}
        checkpoint()
        test_failure = False
        try:
            for number, group in enumerate(profile["groups"]):
                project = ROOT / group["project"]
                if project not in projects:
                    raise ValueError("Required test project is absent from the owner solution.")
                role = collector_for(group, args.suite)
                collector = collectors[role]
                if project != composition_project:
                    if role != "current":
                        raise ValueError("Only the declared composition project may use the retained collector.")
                    assembly = run("test-path-" + str(number), ["dotnet", "msbuild", str(project), "-nologo", *properties,
                        "-getProperty:TargetPath"]).strip()
                else:
                    assembly = collector["assembly"]
                xml = evidence / ("required-" + str(number) + ".xml")
                selectors = [argument for name in group["selectors"] for argument in [group["filter"], name]]
                environment = dict(os.environ, DOTNET_ROLL_FORWARD="LatestPatch")
                run("test-" + str(number), ["dotnet", assembly, *selectors, "-result-xml", str(xml)],
                    cwd=Path(collector["root"]), environment=environment)
                counts = ET.parse(xml).getroot().find("assembly").attrib
                require_collector_execution(counts, collector["targetFramework"])
                if int(counts["total"]) != group["requiredCount"] or int(counts["total"]) <= 0 or int(counts["passed"]) != int(counts["total"]) or any(int(counts.get(key, "0")) for key in ["failed", "errors", "skipped", "not-run"]):
                    raise ValueError("Incomplete/nonpassing required suite: " + project.stem)
                result["tests"].append({"project": str(project.relative_to(ROOT)), "purpose": group.get("purpose", "recorded-tuple-boundary"),
                    "selectors": group["selectors"], "platformSource": collector["source"], "currentSource": revision,
                    "collector": role, "targetFramework": collector["targetFramework"], "toolVersion": collector["toolVersion"], "counts": counts})
            if sum(int(group["counts"]["passed"]) for group in result["tests"]) != required_total:
                raise ValueError("Declared required test total did not execute.")
        except BaseException:
            test_failure = True
            raise
        finally:
            verification_failure = None
            for record in snapshots.values():
                try:
                    verify_composition_test_outputs_unchanged(record)
                except Exception as error:
                    if verification_failure is None: verification_failure = error
            checkpoint()
            if verification_failure is not None and not test_failure:
                raise verification_failure
        for collector in collectors.values():
            require_collector_source(Path(collector["root"]), collector["source"])
            selected_tool = Path(collector["toolRoot"])
            for filename, source in [("Hexalith.Builds.Tooling.dll", selected_tool / "Hexalith.Builds.Tooling.dll"),
                    ("Hexalith.Builds.Module.EventStoreHost.dll", selected_tool / "g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll")]:
                output = Path(collector["assembly"]).parent / filename
                if hashlib.sha256(output.read_bytes()).digest() != hashlib.sha256(source.read_bytes()).digest():
                    raise ValueError("Collector output differs from its authenticated published artifact: " + filename)
        result["status"] = "passed"
    except Exception as error:
        result["status"] = "failed"
        result["blocker"] = str(error)
        raise
    finally:
        try:
            if retained_workspace is not None:
                retained_workspace.cleanup()
                result["historicalReaderWorkspaceRemoved"] = True
        except OSError as error:
            result["historicalReaderWorkspaceRemoved"] = False
            if result["status"] == "passed":
                result["status"] = "failed"
                result["blocker"] = "Historical reader workspace cleanup failed: " + str(error)
            raise
        finally:
            result["limitation"] = "CI build/test preflight only. Fresh authenticated candidate-decision and live generated/direct-host runtime evidence remain separate mandatory gates."
            checkpoint()


if __name__ == "__main__":
    main()
