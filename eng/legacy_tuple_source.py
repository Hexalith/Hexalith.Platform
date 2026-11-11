"""Bind legacy controls to an honest same-code committed qualification source."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
DESCRIPTOR = "eng/legacy-tuple-source.json"
BUILDS = "fc2122240a39f03628c04fa8433e5eece90b8b4c"
PROFILE_HASH = "61689f1598dac43a9fa28dcf5319008059e0fea0f1018209b1a8e79e4f63d7b0"
PROVENANCE = "src/Hexalith.Platform.Composition/Declarations/provenance.json"
LEGACY_PROVENANCE_HASH = "3e9b04109c5ddb34a0ac6801f5cdb36e1cbc102c65bfccb54b77c4d408091a61"
TENANTS_CURRENT_SOURCE = "8e20524574901d71ad5f4876804c64c3c6c87b88"
TENANTS_705_SOURCE = "d88aef6b52d7e8a2e2d81132b1d76b7e1d1f0f35"
MEMORIES_CURRENT_SOURCE = "246157a7ff8e2dcd8d356c833a77a02514490d2f"
EVENTSTORE_420_SOURCE = "aa16227bd07f0564a3026d619a5abd973e02656f"
LEGACY_PARTIES_SOURCE = "bb2783120c0eed620e3d4f52a2ebaf6aa510126f"
CURRENT_PARTIES_SOURCE = "ebe1739d70a4e76447938b0bb8c53db508bf6019"
LEGACY_PARTIES_DECLARATION = "40cee6ce4b82902d994b8c2a1baa0629dab33e8f2afc5c8687214fc1dab1641b"
CURRENT_PARTIES_DECLARATION = "d9c193b0e7b61601b8fcceb906b5dafe4769a1b9d7538a6ce164b4aa0840b348"
PARTIES_PATH = "references/Hexalith.Parties"


def parties_source(root: Path, revision: str) -> str:
    try:
        return git(root, "rev-parse", revision + ":" + PARTIES_PATH).decode().strip()
    except subprocess.CalledProcessError as error:
        raise ValueError("The exact Parties source gitlink is missing.") from error


def verify_parties_checkout(root: Path, historical: bool, *, pre_extension: bool = False) -> dict:
    """Recheck the actual initialized owner without granting service or host admission."""
    source = LEGACY_PARTIES_SOURCE if historical else CURRENT_PARTIES_SOURCE
    declaration = LEGACY_PARTIES_DECLARATION if historical else CURRENT_PARTIES_DECLARATION
    if not historical and not pre_extension:
        from current_tuple_source import exists, read_selection
        revision = git(root, "rev-parse", "HEAD").decode().strip()
        if exists(root, revision):
            binding, _ = read_selection(root, revision)
            selected = binding["ownerSources"]["parties"]
            source, declaration = selected["sourceRevision"], selected["declarationSha256"]
    owner = root / PARTIES_PATH
    try:
        if parties_source(root, "HEAD") != source or git(owner, "rev-parse", "HEAD").decode().strip() != source \
                or hashlib.sha256((owner / "module/hexalith.module-manifest.v2.json").read_bytes()).hexdigest() != declaration:
            raise ValueError("Parties checkout or declaration differs from its exact historical/current binding.")
    except (OSError, subprocess.CalledProcessError) as error:
        raise ValueError("The exact Parties checkout or declaration is unavailable.") from error
    return {"source": source, "declarationSha256": declaration, "qualificationCredit": False}
ALLOWED = {PROVENANCE, DESCRIPTOR, "eng/builds-tuple.json", "eng/tuple-ci-suites.json", ".config/dotnet-tools.json",
           "hexalith-platform.json", "references/Hexalith.Builds", "references/Hexalith.Parties", "references/Hexalith.EventStore", "eng/pre-extension-tuple-source.json"}


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
    if set(document) != {"schema", "purpose", "sourceRevision", "buildsSource", "toolVersion", "profileSha256", "requiredCount", "qualificationCredit"}:
        raise ValueError("Unsupported legacy source descriptor fields.")
    if document["schema"] != "hexalith.platform.legacy-tuple-source.v1" or document["purpose"] != "same-code-legacy-tuple-regression" \
            or document["buildsSource"] != BUILDS or document["toolVersion"] != "4.44.3" or document["profileSha256"] != PROFILE_HASH \
            or document["requiredCount"] != 349 or document["qualificationCredit"] is not False:
        raise ValueError("Legacy descriptor changed its finite historical obligations.")
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
    from current_tuple_source import exists, read_selection, verify_companion_descriptors
    if exists(root, current):
        read_selection(root, current)
        verify_companion_descriptors(root, current, legacy)
        if git(root, "rev-parse", legacy + ":references/Hexalith.EventStore").decode().strip() != "cab8363c2b6e2c0588efc8ca9c2e09fab0006841" \
                or parties_source(root, legacy) != LEGACY_PARTIES_SOURCE:
            raise ValueError("The refreshed legacy replay changed its frozen historical owner sources.")
        from pre_extension_tuple_source import verify_tuple_metadata
        verify_tuple_metadata(root, current, legacy,
            binding_hash="554359e26442ff2a039d927236e9d126d55c1b752c2c0edf632062f767f3ed8a",
            tools_hash="f11d2b9583071655167b3f98eaf5cd2a1b59de8f120544d244c4729ae91ca149",
            identity_hash="4b324533e9d3bbcc40532b1e161f0b134ce4eea7fe93d6dffe893007cabc2a27",
            historical_version="4.44.3")
        return {"legacySource": legacy, "currentSource": current, "purpose": "same-code-legacy-tuple-regression",
                "qualificationCredit": False, "binaryEquivalenceClaimed": False, "sourceEquivalence": "verified",
                "requiredCount": 349, "allowedMetadataDifferences": sorted(path for path in changed if path)}
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
    sdk_expected = decode(json.dumps(next_expected))
    sdk_expected["eventstore"].update(version="4.2.0", commit=EVENTSTORE_420_SOURCE)
    if "eng/pre-extension-tuple-source.json" in changed:
        from pre_extension_tuple_source import resolve as pre_extension_source
        for revision in [current, legacy]:
            recorded = pre_extension_source(root, revision)
            git(root, "cat-file", "-e", recorded + "^{commit}")
    if selected != original and selected != expected and selected != next_expected and selected != sdk_expected:
        raise ValueError("Current provenance changed more than the authenticated Tenants, Memories and SDK4.2 versions and sources.")
    if "references/Hexalith.EventStore" in changed or selected == sdk_expected:
        if git(root, "rev-parse", legacy + ":references/Hexalith.EventStore").decode().strip() != "cab8363c2b6e2c0588efc8ca9c2e09fab0006841" \
                or git(root, "rev-parse", current + ":references/Hexalith.EventStore").decode().strip() != EVENTSTORE_420_SOURCE or selected != sdk_expected:
            raise ValueError("Changed SDK gitlink must retain its exact old source and match the published SDK4.2 provenance.")
    if (PARTIES_PATH in changed or selected == sdk_expected) and (selected != sdk_expected
            or parties_source(root, legacy) != LEGACY_PARTIES_SOURCE
            or parties_source(root, current) != CURRENT_PARTIES_SOURCE):
        raise ValueError("SDK4.2 replay requires the exact historical BB to current EBE Parties sources.")
    tool = decode(git(root, "show", legacy + ":.config/dotnet-tools.json"))["tools"]["hexalith.builds.module.cli"]
    if tool["version"] != "4.44.3" or tool["rollForward"] is not False:
        raise ValueError("Legacy source changed its exact published tool identity.")
    return {"legacySource": legacy, "currentSource": current, "purpose": "same-code-legacy-tuple-regression",
            "qualificationCredit": False, "binaryEquivalenceClaimed": False, "sourceEquivalence": "verified",
            "requiredCount": 349, "allowedMetadataDifferences": sorted(path for path in changed if path)}


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
