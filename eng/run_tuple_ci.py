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

from prepare_builds_artifacts import source_state
from builds_tuple import load_binding, validate_owner_pins, validate_recorded_source, verify_catalog

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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--artifacts", type=Path)
    parser.add_argument("--audit-only", action="store_true")
    args = parser.parse_args()
    evidence = args.evidence.resolve()
    if evidence.exists():
        raise ValueError("Choose a new evidence directory.")
    evidence.mkdir(parents=True)
    result = {"kind": "tuple-ci-preflight", "candidateAcceptance": False, "status": "running", "tests": [], "commands": []}

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
        catalog = verify_catalog(binding, ROOT / "references/Hexalith.Builds")
        profile = json.loads((ROOT / "eng/tuple-ci-suites.json").read_bytes())
        required_total = validate_profile(profile)
        if not args.audit_only:
            if os.environ.get("GITHUB_ACTIONS") != "true" or os.environ.get("CI") != "true":
                raise ValueError("Release execution belongs to GitHub CI; local use supports --audit-only.")
            if os.environ.get("PLATFORM_CANDIDATE_SHA") != revision:
                raise ValueError("The full selected candidate SHA must equal the actual checkout.")
            if result["platformSourceState"]["dirty"]:
                raise ValueError("Tracked source is dirty.")
        if args.artifacts:
            identity = json.loads((args.artifacts.resolve() / "artifact-identity.json").read_bytes())
            tool = args.artifacts.resolve() / "published/tools/net10.0/any"
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
        properties = ["-p:Configuration=Release", "-p:UseHexalithProjectReferences=false", "-p:HexalithBuildsPublishedToolRoot=" + str(tool)]
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
        run("binding-regressions", [sys.executable, "-B", "eng/test_host_builds_resolution.py"])
        run("pin-alignment", [sys.executable, "eng/verify-platform-pins.py", "--catalog", str(catalog)])
        run("restore", ["dotnet", "restore", "Hexalith.Platform.slnx", *properties])
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
        for number, group in enumerate(profile["groups"]):
            project = ROOT / group["project"]
            if project not in projects:
                raise ValueError("Required test project is absent from the owner solution.")
            assembly = run("test-path-" + str(number), ["dotnet", "msbuild", str(project), "-nologo", *properties, "-getProperty:TargetPath"]).strip()
            xml = evidence / ("required-" + str(number) + ".xml")
            selectors = [argument for name in group["selectors"] for argument in [group["filter"], name]]
            run("test-" + str(number), ["dotnet", assembly, *selectors, "-result-xml", str(xml)])
            counts = ET.parse(xml).getroot().find("assembly").attrib
            if int(counts["total"]) != group["requiredCount"] or int(counts["total"]) <= 0 or int(counts["passed"]) != int(counts["total"]) or any(int(counts.get(key, "0")) for key in ["failed", "errors", "skipped", "not-run"]):
                raise ValueError("Incomplete/nonpassing required suite: " + project.stem)
            result["tests"].append({"project": str(project.relative_to(ROOT)), "counts": counts})
        if sum(int(group["counts"]["passed"]) for group in result["tests"]) != required_total:
            raise ValueError("Declared required test total did not execute.")
        for filename, source in [("Hexalith.Builds.Tooling.dll", tool / "Hexalith.Builds.Tooling.dll"), ("Hexalith.Builds.Module.EventStoreHost.dll", tool / "g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll")]:
            output = ROOT / "tests/Hexalith.Platform.Composition.Tests/bin/Release/net10.0" / filename
            if hashlib.sha256(output.read_bytes()).digest() != hashlib.sha256(source.read_bytes()).digest():
                raise ValueError("CI output differs from authenticated published artifact: " + filename)
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
