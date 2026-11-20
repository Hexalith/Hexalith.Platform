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
import xml.etree.ElementTree as ET
import zipfile

from current_catalog_view import verify_unified_catalog
from prepare_builds_artifacts import source_state
from builds_tuple import TOOL_PACKAGE, load_binding, resolve_builds_root, tool_framework, validate_owner_pins, verify_catalog
from builds_tool_lock import load_lock, locked_entry, require_locked

ROOT = Path(__file__).resolve().parents[1]
REQUIRED_TOTALS = {"current42": 380, "base42": 67, "extensions": 9}
def collector_for(group, suite):
    """Every remaining control executes on the candidate's current collector."""
    if suite not in ("current42", "base42", "extensions") or group.get("collector") != "current":
        raise ValueError("Every candidate profile requires its explicit current collector context.")
    return "current"


def require_collector_framework(document, expected):
    """Refuse a mixed or changed target before any collector may execute tests."""
    if expected != "net11.0" or set(document["project"]["frameworks"]) != {expected}:
        raise ValueError("Collector assets differ from their exact recorded target framework.")


def require_collector_execution(counts, expected):
    """The executed assembly and actual runtime must match the selected collector."""
    import re
    framework = ".NETCoreApp,Version=v" + expected.removeprefix("net")
    runtime = re.search(r"(?:^|\s)\.NET ([0-9]+)\.[0-9]+\.[0-9]+(?:\s|$)", counts.get("environment", ""))
    if counts.get("target-framework") != framework or runtime is None \
            or runtime.group(1) != expected.removeprefix("net").split(".")[0]:
        raise ValueError("Executed tests do not match their collector target and actual managed runtime.")


def require_collector_source(root, source):
    """A receipt cannot substitute for the actual unchanged source and direct checkouts."""
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip()
    status = subprocess.check_output(["git", "status", "--porcelain", "--untracked-files=normal"], cwd=root, text=True)
    if revision != source or status.strip():
        raise ValueError("Collector source identity or clean recorded owner checkouts changed.")


def tuple_runtime_context(binding, source, suite, profile_sha256):
    """Select the pinned generation's NET11 runtime; old package data grants no execution authority."""
    version = binding["version"]
    if suite not in ("current42", "base42", "extensions") or tool_framework(version) != "net11.0":
        raise ValueError("Current runtime qualification requires the pinned NET11 Builds generation.")
    return {"purpose": "current-net11-with-catalog-sdk", "targetFramework": "net11.0",
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


BUILDS_ARTIFACT_MEMBERS = (("Hexalith.Builds.Tooling.dll", "Hexalith.Builds.Tooling.dll"),
    ("Hexalith.Builds.Module.EventStoreHost.dll", "g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll"))


def builds_artifact_identities(receipt, lock, builds_source, collectors, identities=None):
    """AR-12: expected = per-file SHA-256 of the lock-verified archive; loaded = the built outputs.

    Records both identities for each collector, then refuses any difference.
    """
    identities = {} if identities is None else identities
    if receipt.get("toolLock") != lock or receipt.get("buildsRevision") != builds_source:
        raise ValueError("The artifact receipt differs from the locked tool and recorded Builds gitlink.")
    for name, collector in collectors.items():
        selected_tool = Path(collector["toolRoot"])
        for filename, member in BUILDS_ARTIFACT_MEMBERS:
            output = Path(collector["assembly"]).parent / filename
            loaded = hashlib.sha256(output.read_bytes()).hexdigest()
            if loaded != hashlib.sha256((selected_tool / member).read_bytes()).hexdigest():
                raise ValueError("Collector output differs from its authenticated published artifact: " + filename)
            expected = receipt["files"].get(receipt["toolPrefix"] + member)
            identities.setdefault(name, {})[filename] = {"archiveMember": receipt["toolPrefix"] + member,
                "expectedSha256": expected, "loadedPath": str(output), "loadedSha256": loaded}
            if loaded != expected:
                raise ValueError("Collector output differs from its lock-verified archive member: " + filename)
    return identities


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
            raise ValueError("The collecting graph differs from its exact public cohort: " + name)
    sdk_packages = {name: item for name, item in libraries.items() if name.startswith("Hexalith.EventStore.")}
    if any(name.rpartition("/")[2] != cohort["HexalithEventStoreVersion"] or item.get("type") != "package"
           for name, item in sdk_packages.items()):
        raise ValueError("Every restored EventStore package must match the exact collecting cohort.")
    return {"packages": packages, "eventStorePackages": sorted(sdk_packages), "scope": "Catalog package graph for this collector",
            "candidateAcceptance": False, "runtimeAcceptance": False}


def verify_test_sdk(assembly, version, memories_version=None):
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
    if memories_version is not None:
        for name in ("Contracts", "EventStore", "EventStoreExtensions"):
            identity = "Hexalith.Memories." + name + "/" + memories_version
            if dependencies.get(identity, {}).get("type") != "package":
                raise ValueError("The actual test binary does not resolve the selected collection companion: " + identity)


def _subprocess_environment(name, environment=None, *, metadata_token=None):
    """Keep the job token out of ordinary commands, including explicit environments."""
    child = dict(os.environ if environment is None else environment)
    child.pop("GH_TOKEN", None)
    if name == "published-artifacts" and metadata_token is not None:
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
    parser.add_argument("--suite", choices=["extensions", "base42", "current42"], required=True)
    args = parser.parse_args()
    profile_path = {"extensions": "eng/tuple-ci-extension-suites.json", "base42": "eng/tuple-ci-sdk42-base-suites.json",
                    "current42": "eng/tuple-ci-sdk42-current-suites.json"}[args.suite]
    evidence = args.evidence.resolve()
    if evidence.exists():
        raise ValueError("Choose a new evidence directory.")
    evidence.mkdir(parents=True)
    result = {"kind": "tuple-ci-preflight", "candidateAcceptance": False, "status": "running", "tests": [], "commands": [], "suite": args.suite}

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
        version = binding["version"]
        validate_owner_pins(binding, ROOT)
        lock = locked_entry(load_lock(), version)
        result["toolLock"] = lock
        builds_source = run("builds-pin", ["git", "rev-parse", "HEAD:references/Hexalith.Builds"]).strip()
        if not args.audit_only and result["platformSourceState"]["dirty"]:
            raise ValueError("CI requires clean recorded Platform source with the exact approved Builds pin.")
        runtime_context = None
        if not args.audit_only:
            runtime_context = tuple_runtime_context(binding, builds_source, args.suite, hashlib.sha256((ROOT / profile_path).read_bytes()).hexdigest())
            result["runtimeBuildContext"] = runtime_context
            run("runtime-context-controls", [sys.executable, "-B", "eng/test_tuple_runtime_context.py"])
            run("collector-context-controls", [sys.executable, "-B", "eng/test_tuple_collectors.py"])
        builds_root = resolve_builds_root(ROOT)
        # The catalog comes from the gitlink blob; the checkout must hold those exact bytes.
        catalog, catalog_body, catalog_identity = verify_catalog(builds_source, builds_root)
        result["buildsCatalog"] = catalog_identity
        result["extensionCatalogView"] = verify_unified_catalog(catalog_body, catalog_identity["sha256"])
        collecting_cohort = result["extensionCatalogView"]["properties"]
        result["eventStoreSdk"] = collecting_cohort["HexalithEventStoreVersion"]
        (evidence / "authenticated-catalog.props").write_bytes(catalog_body)
        profile = json.loads((ROOT / profile_path).read_bytes())
        required_total = validate_profile(profile)
        selected_collectors = {collector_for(group, args.suite) for group in profile["groups"]}
        if required_total != REQUIRED_TOTALS[args.suite]:
            raise ValueError("The candidate suite differs from its measured required inventory.")
        # Profiles carry measured positive per-group counts; all execute on this candidate.
        if selected_collectors != {"current"}:
            raise ValueError("Candidate inventories cannot select an alternate collector.")
        if not args.audit_only:
            if os.environ.get("GITHUB_ACTIONS") != "true" or os.environ.get("CI") != "true":
                raise ValueError("Release execution belongs to GitHub CI; local use supports --audit-only.")
            if os.environ.get("PLATFORM_CANDIDATE_SHA") != revision:
                raise ValueError("The full selected candidate SHA must equal the actual checkout.")
            if result["platformSourceState"]["dirty"]:
                raise ValueError("Tracked source is dirty.")
        if args.artifacts:
            receipt_path = args.artifacts.resolve() / "artifact-identity.json"
            identity = json.loads(receipt_path.read_bytes())
            tool = args.artifacts.resolve() / ("published/tools/" + tool_framework(version) + "/any")
            if Path(identity["toolRoot"]).resolve() != tool or identity["version"] != version or identity["buildsRevision"] != builds_source \
                    or identity.get("tupleBinding") != binding or identity.get("toolLock") != lock:
                raise ValueError("Artifact receipt names another tool root or tuple.")
            archive = args.artifacts.resolve() / (TOOL_PACKAGE.lower() + "." + version + ".nupkg")
            require_locked(lock, archive.read_bytes())
            # Local graph diagnostics can reuse an already verified archive, but recheck every extracted byte.
            with zipfile.ZipFile(archive) as package:
                for name in package.namelist():
                    if not name.endswith("/") and (args.artifacts / "published" / name).read_bytes() != package.read(name):
                        raise ValueError("Extracted artifact changed: " + name)
            if not args.audit_only:
                raise ValueError("CI must prepare fresh signed artifacts instead of reusing a caller receipt.")
        else:
            receipt_path = evidence / "builds/artifact-identity.json"
            tool = Path(run("published-artifacts", [sys.executable, str(ROOT / "eng/prepare_builds_artifacts.py"), "--output", str(evidence / "builds"), "--require-recorded"]).strip())
        result["toolRoot"] = str(tool)
        properties = ["-p:Configuration=Release", "-p:UseHexalithProjectReferences=false",
                      "-p:HexalithBuildsRoot=" + str(builds_root), "-p:HexalithBuildsPublishedToolRoot=" + str(tool)]
        result["collectingPackageSelection"] = {"properties": collecting_cohort, "scope": "Authenticated Builds catalog",
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
            run("gitlink-catalog-view-controls", [sys.executable, "-B", "eng/test_current_catalog_view.py", "UnifiedGitlinkCatalogTests"])
        run("composition-output-identity-controls", [sys.executable, "-B", "eng/test_tuple_output_identity.py"])
        run("binding-regressions", [sys.executable, "-B", "eng/test_host_builds_resolution.py"])
        file_catalog = evidence / "authenticated-catalog.props"
        file_catalog_sha256 = catalog_identity["sha256"]
        file_catalog_binding = ROOT / "eng/builds-tuple.json"
        run("file-host-pin-controls", [sys.executable, "-B", "eng/test_verify_platform_pins.py"])
        run("pin-alignment", [sys.executable, "eng/verify-platform-pins.py", "--catalog", str(file_catalog),
            "--catalog-sha256", file_catalog_sha256, "--producer-binding", str(file_catalog_binding)])
        run("collector-package-cohort-controls", [sys.executable, "-B", "eng/test_tuple_collection_graph.py",
            "--catalog", str(file_catalog), "--catalog-sha256", file_catalog_sha256,
            "--producer-binding", str(file_catalog_binding), "--evidence", str(evidence / "collector-package-cohort")])
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
                raise ValueError("Restored graph does not match the exact current runtime context.")
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
            verify_test_sdk(assembly, result["eventStoreSdk"],
                collecting_cohort["HexalithMemoriesVersion"] if name == "current" and collecting_cohort is not None else None)
            return {"source": source, "root": str(root), "project": str(project), "assembly": str(assembly),
                "properties": selected_properties, "targetFramework": framework, "toolRoot": str(selected_tool),
                "toolVersion": selected_binding["version"],
                "buildsSource": builds_source, "tupleBinding": selected_binding,
                "packageAssets": str(asset_path), "packageAssetsSha256": hashlib.sha256(asset_path.read_bytes()).hexdigest(),
                "nativeAcceptance": False}

        collectors = {"current": measure_collector("current", ROOT, composition_project, properties, tool, binding,
            runtime_context["targetFramework"] if runtime_context else None, revision)}
        result["collectors"] = collectors
        snapshots = {}
        for name, collector in collectors.items():
            snapshots[name] = result["compositionTestOutputs"] = {"beforeCapturedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
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
        builds_artifact_identities(json.loads(receipt_path.read_bytes()), lock, builds_source, collectors,
                                   result.setdefault("buildsArtifactIdentities", {}))
        result["status"] = "passed"
    except Exception as error:
        result["status"] = "failed"
        result["blocker"] = str(error)
        raise
    finally:
        result["limitation"] = "CI build/test preflight only. Fresh authenticated candidate-decision and live generated/direct-host runtime evidence remain separate mandatory gates."
        checkpoint()


if __name__ == "__main__":
    main()
