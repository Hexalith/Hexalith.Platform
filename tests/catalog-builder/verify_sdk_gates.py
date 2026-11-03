#!/usr/bin/env python3
"""Evaluate the four real projects' finite SDK gates without restoring or building."""

import argparse
import json
from pathlib import Path
import signal
import subprocess
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]
CATALOG = "HEXALITH_PLATFORM_CATALOG_SDK"
V2 = "HEXALITH_PLATFORM_V2_CATALOG"
SOURCE = "HEXALITH_PLATFORM_SOURCE_CATALOG"
TECHNICAL = "HEXALITH_PLATFORM_TECHNICAL_CATALOG_SDK"
SCOPED = "HEXALITH_SCOPED_CONSUMER_BINDING"
FLAGS = {CATALOG, V2, SOURCE, TECHNICAL, SCOPED}
LEGACY = ["3.130.0", "3.130.1", "3.130.2"]
ALIASES = [alias for version in LEGACY for alias in
           [version + ".0", version + "+metadata", version + ".0+build.1"]]
VERSIONS = ["3.129.0", *LEGACY, "3.132.1", "3.132.2", "3.133.0", "4.0.0", "4.0.2", *ALIASES,
            "3.128.4", "3.131.0", "3.132.0", "4.0.0", "4.0.1", "4.0.3",
            "4.0.2-preview.1", "4.0.2+metadata", "4.0.2.0", "4.0.2.1",
            "3.132.1-preview.1", "3.132.1+metadata", "3.132.1.0", "3.132.1.1",
            "3.133.0-preview.1", "3.133.0+metadata", "3.133.0.0",
            "03.132.1", "3.132.01", "v3.132.1", "invalid-version"]
PROJECTS = {
    "host": "src/Hexalith.Platform.EventStoreHost/Hexalith.Platform.EventStoreHost.csproj",
    "composition": "src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj",
    "tests": "tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj",
    "tool": "src/Hexalith.Platform.CatalogBuilder/Hexalith.Platform.CatalogBuilder.csproj",
}


def expected_flags(project, version, source):
    if source:
        # The tool's source binding deliberately retains its prior unqualified path.
        return {"host": {SOURCE, TECHNICAL, CATALOG},
                "composition": {SOURCE, CATALOG, V2},
                "tests": {SOURCE, CATALOG, V2}, "tool": set()}[project]
    if project == "tool":
        return {SCOPED} if version in [*LEGACY, *ALIASES, "3.132.1", "3.133.0", "4.0.0", "4.0.2"] else set()
    flags = set()
    if version in ["3.129.0", *LEGACY, "3.132.1", "3.132.2", "3.133.0", "4.0.0", "4.0.2"]:
        flags.add(CATALOG)
    if project != "host" and version in [*LEGACY, "3.132.1", "3.132.2", "3.133.0", "4.0.0", "4.0.2"]:
        flags.add(V2)
    if project == "host" and version in ["3.132.1", "4.0.0", "4.0.2"]:
        flags.add(TECHNICAL)
    return flags


def verify(evidence, builds_root=None):
    report = {"status": "running", "runtimeAcceptance": False, "cases": [],
              "scope": "Real full-project MSBuild evaluation only; no restore, build or runtime acceptance."}
    evidence.parent.mkdir(parents=True, exist_ok=True)
    if evidence.exists():
        raise ValueError("Choose a fresh evidence file.")
    catalog_root = builds_root or ROOT / "references/Hexalith.Builds"
    catalog_default = ET.parse(catalog_root / "Props/Directory.Packages.props").findtext(
        ".//HexalithEventStoreVersion")
    if not catalog_default:
        raise ValueError("The selected Builds catalog has no default EventStore version.")

    def checkpoint():
        temporary = evidence.with_name(evidence.name + ".tmp")
        temporary.write_text(json.dumps(report, indent=2) + "\n")
        temporary.replace(evidence)

    def interrupted(signum, _frame):
        raise SystemExit(128 + signum)

    previous_handlers = {signum: signal.signal(signum, interrupted) for signum in [signal.SIGINT, signal.SIGTERM]}
    try:
        checkpoint()
        for project, relative in PROJECTS.items():
            # Both valid and invalid package inputs must retain explicit source-mode semantics.
            cases = [(version, False) for version in VERSIONS] + [("3.132.1", True), ("3.132.2", True), (None, False)]
            for version, source in cases:
                command = ["dotnet", "msbuild", str(ROOT / relative), "-nologo",
                           "-p:Configuration=Release", f"-p:UseHexalithProjectReferences={str(source).lower()}",
                           "-getProperty:DefineConstants,HexalithEventStoreVersion"]
                if version is not None:
                    # Literal cases isolate the gates and require no nested source dependencies.
                    command.extend([f"-p:HexalithEventStoreVersion={version}", "-p:ImportDirectoryPackagesProps=false"])
                if builds_root is not None:
                    command.append(f"-p:HexalithBuildsRoot={builds_root}")
                expected_version = version or catalog_default
                expected = expected_flags(project, expected_version, source)
                case = {"project": relative, "version": version, "source": source,
                        "catalogDefault": version is None, "status": "pending", "command": command,
                        "expectedVersion": expected_version, "expected": sorted(expected)}
                report["cases"].append(case)
                checkpoint()
                try:
                    result = subprocess.run(command, cwd=ROOT, text=True, capture_output=True, check=False, timeout=60)
                except subprocess.TimeoutExpired as error:
                    def decoded(value):
                        return value.decode(errors="replace") if isinstance(value, bytes) else value or ""
                    case.update(status="timed-out", stdout=decoded(error.stdout), stderr=decoded(error.stderr),
                                timeoutSeconds=error.timeout, exitCode=None)
                    checkpoint()
                    raise
                case.update(status="completed", exitCode=result.returncode, stdout=result.stdout, stderr=result.stderr)
                checkpoint()
                if result.returncode:
                    raise AssertionError(f"{relative}: {version}, source={source}: exit {result.returncode}")
                properties = json.loads(result.stdout)["Properties"]
                actual = FLAGS.intersection(properties["DefineConstants"].split(";"))
                case.update(actual=sorted(actual), actualVersion=properties["HexalithEventStoreVersion"])
                if actual != expected or case["actualVersion"] != expected_version:
                    raise AssertionError(f"{relative}: {version}, source={source}: {sorted(actual)} != {sorted(expected)}")
                case["status"] = "passed"
                checkpoint()
        report.update(status="passed", count=len(report["cases"]))
    except BaseException as error:
        if report["cases"] and report["cases"][-1]["status"] == "pending":
            report["cases"][-1]["status"] = "interrupted"
        report.update(status="failed", error=f"{type(error).__name__}: {error}")
        raise
    finally:
        checkpoint()
        for signum, previous in previous_handlers.items():
            signal.signal(signum, previous)
    print(f"Passed {report['count']} finite SDK gate evaluations; no runtime acceptance.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--builds-root", type=Path, help="Existing shared catalog checkout; defaults to normal project resolution.")
    args = parser.parse_args()
    verify(args.evidence.resolve(), args.builds_root.resolve() if args.builds_root else None)


if __name__ == "__main__":
    main()
