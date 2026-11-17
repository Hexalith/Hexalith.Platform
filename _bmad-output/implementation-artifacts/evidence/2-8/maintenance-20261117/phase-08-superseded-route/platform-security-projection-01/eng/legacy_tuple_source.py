"""Bind legacy controls to an honest same-code committed qualification source."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
DESCRIPTOR = "eng/legacy-tuple-source.json"
BUILDS = "fc2122240a39f03628c04fa8433e5eece90b8b4c"
PROFILE_HASH = "61689f1598dac43a9fa28dcf5319008059e0fea0f1018209b1a8e79e4f63d7b0"
PROVENANCE = "src/Hexalith.Platform.Composition/Declarations/provenance.json"
LEGACY_PROVENANCE_HASH = "3e9b04109c5ddb34a0ac6801f5cdb36e1cbc102c65bfccb54b77c4d408091a61"
TENANTS_CURRENT_SOURCE = "8e20524574901d71ad5f4876804c64c3c6c87b88"
TENANTS_705_SOURCE = "d88aef6b52d7e8a2e2d81132b1d76b7e1d1f0f35"
MEMORIES_CURRENT_SOURCE = "246157a7ff8e2dcd8d356c833a77a02514490d2f"
ALLOWED = {PROVENANCE, DESCRIPTOR, "eng/builds-tuple.json", "eng/tuple-ci-suites.json", ".config/dotnet-tools.json",
           "hexalith-platform.json", "references/Hexalith.Builds", "references/Hexalith.Parties"}
SECURITY_FLOOR = "eng/legacy-security-floor.props"
ORIGINAL_LEGACY = "3cd036f78ccdfeb72521122a65f1a3aef0bff9da"
SECURITY_MINIMUMS = {"Aspire.Hosting": "13.6.5", "Microsoft.AspNetCore.SignalR.StackExchangeRedis": "10.0.15",
                     "Microsoft.Extensions.DependencyInjection.Abstractions": "10.0.15", "Microsoft.Extensions.Options": "10.0.15"}


def security_baseline(root: Path, current: str, document: dict):
    present = subprocess.run(["git", "cat-file", "-e", current + ":" + SECURITY_FLOOR], cwd=root,
                             capture_output=True, timeout=30).returncode == 0
    if document["schema"] == "hexalith.platform.legacy-tuple-source.v1":
        if present:
            raise ValueError("Security-patched source must declare the changed legacy dependency baseline.")
        return None
    baseline = document["securityBaseline"]
    expected = {"originalSourceRevision", "propsPath", "propsSha256", "minimumPackageVersions", "auditException"}
    if not isinstance(baseline, dict) or set(baseline) != expected or not present \
            or baseline["originalSourceRevision"] != ORIGINAL_LEGACY or baseline["propsPath"] != SECURITY_FLOOR \
            or baseline["minimumPackageVersions"] != SECURITY_MINIMUMS or baseline["auditException"] is not False:
        raise ValueError("Unsupported or incomplete security-patched legacy dependency baseline.")
    raw = git(root, "show", current + ":" + SECURITY_FLOOR)
    if len(raw) > 8192 or baseline["propsSha256"] != hashlib.sha256(raw).hexdigest():
        raise ValueError("The declared security floor differs from its committed Git body.")
    floor = ET.fromstring(raw)
    if floor.tag != "Project" or floor.attrib or len(floor) != 1 or floor[0].tag != "ItemGroup" or floor[0].attrib:
        raise ValueError("The security floor must contain only the four approved package minimums.")
    entries = list(floor[0])
    if len(entries) != len(SECURITY_MINIMUMS):
        raise ValueError("The security floor changed its finite package set.")
    observed = set()
    for entry in entries:
        package = entry.attrib.get("Update")
        version = SECURITY_MINIMUMS.get(package)
        condition = "$([MSBuild]::VersionLessThan('%(PackageVersion.Version)', '" + str(version) + "'))"
        if entry.tag != "PackageVersion" or len(entry) != 1 or package in observed or version is None \
                or entry.attrib != {"Update": package} or entry[0].tag != "Version" or len(entry[0]) \
                or entry[0].text != version or entry[0].attrib != {"Condition": condition}:
            raise ValueError("The security floor changed or widened a reviewed package minimum.")
        observed.add(package)
    imports = ET.fromstring(git(root, "show", current + ":Directory.Packages.props"))
    expected_import = "$(MSBuildThisFileDirectory)" + SECURITY_FLOOR
    if sum(node.tag == "Import" and node.attrib == {"Project": expected_import} for node in imports) != 1:
        raise ValueError("The committed package catalog must apply the declared security floor once.")
    return baseline


def decode(raw):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate legacy metadata field.")
            result[key] = value
        return result
    return json.loads(raw, object_pairs_hook=unique)


def commit(value):
    if not isinstance(value, str) or re.fullmatch(r"[a-f0-9]{40}", value, re.ASCII) is None:
        raise ValueError("Require an actual committed legacy/current source SHA; pending data grants no authority.")
    return value


def git(root, *arguments):
    return subprocess.run(["git", *arguments], cwd=root, check=True, capture_output=True, timeout=30).stdout


def resolve(root: Path, current: str, requested: str | None = None) -> str:
    current = commit(current)
    raw = git(root, "show", current + ":" + DESCRIPTOR)
    if len(raw) > 4096:
        raise ValueError("Legacy source descriptor exceeds its fixed bound.")
    document = decode(raw)
    patched = document.get("schema") == "hexalith.platform.legacy-tuple-source.v2"
    fields = {"schema", "purpose", "sourceRevision", "buildsSource", "toolVersion", "profileSha256", "requiredCount", "qualificationCredit"}
    if set(document) != fields | ({"securityBaseline"} if patched else set()):
        raise ValueError("Unsupported legacy source descriptor fields.")
    purpose = "same-code-security-patched-legacy-tuple-regression" if patched else "same-code-legacy-tuple-regression"
    if document["schema"] not in {"hexalith.platform.legacy-tuple-source.v1", "hexalith.platform.legacy-tuple-source.v2"} or document["purpose"] != purpose \
            or document["buildsSource"] != BUILDS or document["toolVersion"] != "4.44.3" or document["profileSha256"] != PROFILE_HASH \
            or document["requiredCount"] != 349 or document["qualificationCredit"] is not False:
        raise ValueError("Legacy descriptor changed its finite historical obligations.")
    security_baseline(root, current, document)
    legacy = commit(document["sourceRevision"])
    if requested and commit(requested) != legacy:
        raise ValueError("Dispatch cannot substitute another legacy source for the committed descriptor.")
    return legacy


def verify(root: Path, current: str, legacy: str) -> dict:
    current, legacy = commit(current), commit(legacy)
    if resolve(root, current, legacy) != legacy or git(root, "rev-parse", "HEAD").decode().strip() != legacy:
        raise ValueError("Legacy job must execute its actual committed source.")
    changed = git(root, "diff", "--name-only", "-z", current, legacy).decode().split("\0")
    if any(path and path not in ALLOWED for path in changed):
        raise ValueError("Production, tests, helpers, build instructions or unapproved metadata differ between sources.")
    if git(root, "rev-parse", legacy + ":references/Hexalith.Builds").decode().strip() != BUILDS:
        raise ValueError("Legacy source did not record its genuine catalog source.")
    profile = git(root, "show", legacy + ":eng/tuple-ci-suites.json")
    if hashlib.sha256(profile).hexdigest() != PROFILE_HASH:
        raise ValueError("The original legacy controls or their positive counts changed.")
    old_provenance = git(root, "show", legacy + ":" + PROVENANCE)
    if hashlib.sha256(old_provenance).hexdigest() != LEGACY_PROVENANCE_HASH:
        raise ValueError("Legacy provenance differs from the authenticated historical Git body.")
    original = decode(old_provenance)
    expected = decode(old_provenance)
    expected["tenants"]["version"] = "7.0.4"
    expected["tenants"]["commit"] = TENANTS_CURRENT_SOURCE
    expected["memories"]["version"] = "3.0.25"
    expected["memories"]["commit"] = MEMORIES_CURRENT_SOURCE
    next_expected = decode(old_provenance)
    next_expected["tenants"]["version"] = "7.0.5"
    next_expected["tenants"]["commit"] = TENANTS_705_SOURCE
    next_expected["memories"]["version"] = "3.0.25"
    next_expected["memories"]["commit"] = MEMORIES_CURRENT_SOURCE
    selected = decode(git(root, "show", current + ":" + PROVENANCE))
    if selected != original and selected != expected and selected != next_expected:
        raise ValueError("Current provenance changed more than the authenticated Tenants and Memories versions and sources.")
    tool = decode(git(root, "show", legacy + ":.config/dotnet-tools.json"))["tools"]["hexalith.builds.module.cli"]
    if tool["version"] != "4.44.3" or tool["rollForward"] is not False:
        raise ValueError("Legacy source changed its exact published tool identity.")
    document = decode(git(root, "show", current + ":" + DESCRIPTOR))
    baseline = security_baseline(root, current, document)
    return {"legacySource": legacy, "currentSource": current, "purpose": document["purpose"],
            "qualificationCredit": False, "binaryEquivalenceClaimed": False, "sourceEquivalence": "verified",
            "requiredCount": 349, "allowedMetadataDifferences": sorted(path for path in changed if path),
            **({"dependencyBaseline": "security-patched", "originalDependenciesPreserved": False,
                "securityBaseline": baseline, "nativeQualificationCredit": False} if baseline else {})}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--current", required=True)
    parser.add_argument("--requested")
    parser.add_argument("--verify", action="store_true")
    args = parser.parse_args()
    legacy = resolve(ROOT, args.current, args.requested)
    print(json.dumps(verify(ROOT, args.current, legacy), sort_keys=True) if args.verify else legacy)


if __name__ == "__main__":
    main()
