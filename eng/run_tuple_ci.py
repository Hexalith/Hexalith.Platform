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


def verify_extension_catalog_pin(catalog):
    """Require the validated owning catalog to carry the one measured SDK pin for the new suite."""
    pins = ET.parse(catalog).findall("./PropertyGroup/HexalithEventStoreVersion")
    if len(pins) != 1 or (pins[0].text or "").strip() != "4.2.0":
        raise ValueError("The extension SDK suite requires the single measured 4.2.0 owning catalog pin.")


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


def main():
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

    def run(name, argv):
        command = {"name": name, "argv": argv, "cwd": str(ROOT), "exitCode": None, "startedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "timeoutSeconds": 1200}
        result["commands"].append(command)
        checkpoint()
        try:
            with (evidence / (name + ".stdout")).open("w") as stdout, (evidence / (name + ".stderr")).open("w") as stderr:
                completed = subprocess.run(argv, cwd=ROOT, stdout=stdout, stderr=stderr, text=True, timeout=1200)
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
        version, source = binding["publication"]["version"], binding["producer"]["sourceRevision"]
        archive_sha256 = binding["publication"]["archiveSha256"]
        validate_owner_pins(binding, ROOT)
        pin = run("builds-pin", ["git", "rev-parse", "HEAD:references/Hexalith.Builds"]).strip()
        validate_recorded_source(binding, pin, result["platformSourceState"]["dirty"], require_clean=not args.audit_only)
        builds_root = resolve_builds_root(ROOT)
        catalog = verify_catalog(binding, builds_root)
        if args.suite in ("extensions", "base42", "current42"):
            verify_extension_catalog_pin(catalog)
        else:
            verify_historical_catalog_pin(catalog)
        profile = json.loads((ROOT / profile_path).read_bytes())
        required_total = validate_profile(profile)
        if args.suite == "base42" and required_total != 97:
            raise ValueError("The SDK4.2 base suite must retain 92 finite controls, three actual entry cases and both owner model transport cases.")
        if args.suite == "current42" and required_total != 477:
            raise ValueError("The SDK4.2 current suite requires all 457 retained cases, seventeen current artifact controls and three explicit MCP selection controls (477 total).")
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
            framework = "net11.0" if version.startswith("5.") else "net10.0"
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
        current_source = os.environ.get("CURRENT_PLATFORM_SHA")
        if current_source and current_source != revision:
            from file_host_catalog import prepare as prepare_file_host_catalog
            file_catalog, result["fileAppHostCatalog"] = prepare_file_host_catalog(
                ROOT, builds_root, current_source, evidence / "current-file-host-catalog")
        run("file-host-catalog-controls", [sys.executable, "-B", "eng/test_file_host_catalog.py"])
        run("file-host-pin-controls", [sys.executable, "-B", "eng/test_verify_platform_pins.py"])
        run("pin-alignment", [sys.executable, "eng/verify-platform-pins.py", "--catalog", str(file_catalog)])
        run("restore", ["dotnet", "restore", "Hexalith.Platform.slnx", *properties])
        run("source-sdk-propagation-controls", [sys.executable, "-B", "eng/test_source_sdk_propagation.py", "--evidence", str(evidence / "source-sdk-metadata"), "--published-tool-root", str(tool)])
        assets = evidence / "assets"
        assets.mkdir()
        for number, project in enumerate(projects):
            path = project.parent / "obj/project.assets.json"
            payload = path.read_bytes()
            document = json.loads(payload)
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
        collector_properties = properties
        if args.suite in ("current42", "base42"):
            # Retained positive image/source controls consume their actual historical input closure.
            # Production above and the isolated production-closure test still build the real current catalog.
            retained_workspace = tempfile.TemporaryDirectory(prefix="platform-retained-reader-", dir=os.environ.get("RUNNER_TEMP"))
            isolated = Path(retained_workspace.name) / "workspace"
            receipt_path = evidence / "historical-reader-workspace.json"
            run("prepare-historical-reader", [sys.executable, "-B", "eng/prepare_tuple_sources.py", "reader-workspace",
                "--source", revision, "--workspace", str(isolated), "--output", str(receipt_path)])
            reader_receipt = json.loads(receipt_path.read_bytes())
            retained_catalog = isolated / "references/Hexalith.Builds/Props/Directory.Packages.props"
            collector_properties = [*properties, "-p:PlatformPackageReaderWorkspaceRoot=" + str(isolated),
                "-p:HexalithTenantsVersion=7.0.5", "-p:HexalithMemoriesVersion=3.0.25", "-p:HexalithPartiesVersion=3.1.0",
                "-p:ArtifactsPath=" + str(evidence / "retained-collector")]
            run("build-retained-collector", ["dotnet", "build", str(composition_project), "-c", "Release", "-m:1", *collector_properties])
            collector_assets = evidence / "retained-collector/obj/Hexalith.Platform.Composition.Tests/project.assets.json"
            selected = json.loads(collector_assets.read_bytes())["libraries"]
            for identity in ("Hexalith.Tenants.Contracts/7.0.5", "Hexalith.Tenants.Server/7.0.5", "Hexalith.Memories.Contracts/3.0.25", "Hexalith.Parties.Contracts/3.1.0"):
                if selected.get(identity, {}).get("type") != "package":
                    raise ValueError("The retained collector did not resolve its exact published package input: " + identity)
            result["historicalReferenceCollector"] = {"workspace": reader_receipt,
                "readerInputCatalogSha256": hashlib.sha256(retained_catalog.read_bytes()).hexdigest(),
                "collectingBuildCatalogSha256": hashlib.sha256(catalog.read_bytes()).hexdigest(), "packageAssets": str(collector_assets),
                "scope": "Current code and patched build dependencies with authenticated retained705/25/310 package/source/image inputs; no new native acceptance"}
        composition_assembly = Path(run("composition-test-path", ["dotnet", "msbuild", str(composition_project), "-nologo", *collector_properties, "-getProperty:TargetPath"]).strip()).resolve()
        verify_test_sdk(composition_assembly, args.eventstore_sdk)
        result["compositionTestOutputs"] = {
            "beforeCapturedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "before": snapshot_composition_test_outputs(composition_assembly),
        }
        checkpoint()
        test_failure = False
        try:
            for number, group in enumerate(profile["groups"]):
                project = ROOT / group["project"]
                if project not in projects:
                    raise ValueError("Required test project is absent from the owner solution.")
                selected_properties = collector_properties if project == composition_project else properties
                assembly = run("test-path-" + str(number), ["dotnet", "msbuild", str(project), "-nologo", *selected_properties, "-getProperty:TargetPath"]).strip()
                if project == composition_project and Path(assembly).resolve() != composition_assembly:
                    raise ValueError("Composition test invocation differs from the observed test assembly.")
                xml = evidence / ("required-" + str(number) + ".xml")
                selectors = [argument for name in group["selectors"] for argument in [group["filter"], name]]
                run("test-" + str(number), ["dotnet", assembly, *selectors, "-result-xml", str(xml)])
                counts = ET.parse(xml).getroot().find("assembly").attrib
                if int(counts["total"]) != group["requiredCount"] or int(counts["total"]) <= 0 or int(counts["passed"]) != int(counts["total"]) or any(int(counts.get(key, "0")) for key in ["failed", "errors", "skipped", "not-run"]):
                    raise ValueError("Incomplete/nonpassing required suite: " + project.stem)
                result["tests"].append({"project": str(project.relative_to(ROOT)), "purpose": group.get("purpose", "recorded-tuple-boundary"), "selectors": group["selectors"], "platformSource": revision, "counts": counts})
            if sum(int(group["counts"]["passed"]) for group in result["tests"]) != required_total:
                raise ValueError("Declared required test total did not execute.")
        except BaseException:
            test_failure = True
            raise
        finally:
            try:
                verify_composition_test_outputs_unchanged(result["compositionTestOutputs"])
            except Exception:
                if not test_failure:
                    raise
            finally:
                checkpoint()
        for filename, source in [("Hexalith.Builds.Tooling.dll", tool / "Hexalith.Builds.Tooling.dll"), ("Hexalith.Builds.Module.EventStoreHost.dll", tool / "g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll")]:
            output = composition_assembly.parent / filename
            if hashlib.sha256(output.read_bytes()).digest() != hashlib.sha256(source.read_bytes()).digest():
                raise ValueError("CI output differs from authenticated published artifact: " + filename)
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
