#!/usr/bin/env python3
"""Check the sealed offline reconciliation; never import or invoke owner executors."""

import argparse
import datetime
import hashlib
import json
from pathlib import Path
import re
import subprocess


DIRECTORY = Path(__file__).resolve().parent
ROOT = DIRECTORY.parents[4]
FORM_PATH = DIRECTORY.parents[1] / "owner-decisions.json"
README_PATH = ROOT / "docs/implementation/story-5-4-operational-security-2026-10-28/README.md"
SPEC_PATH = ROOT / "_bmad-output/implementation-artifacts/spec-5-4-complete-coupled-secrets-host-delivery.md"
PLANNING_PATH = ROOT / "_bmad-output/implementation-artifacts/tests/story-5-4-coupled-delivery-resume-2026-10-29"
EXPECTED_RETURNS = {
    "owner-return/metadata-credential-reference.json":
        "#/mvpOperationalInputs/currentOperationalProfile/ownerMetadataCredentialRecordReference",
    "owner-return/preservation-decision.json":
        "#/mvpOperationalInputs/maintenancePreservationBaseline/expiredRecoveryPreservationDecisionReference",
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(path):
    return json.loads(path.read_text())


def resolve_public_source(path, expected, provenance, drifts):
    observed = sha(path)
    if observed == expected:
        return path
    matches = [record for record in provenance["publicSourceCopies"]
               if record["path"] == str(path) and record["sha256"] == expected]
    if len(matches) != 1:
        raise ValueError("unarchived source drift: " + str(path))
    archive = DIRECTORY / matches[0]["archivedAt"]
    if not archive.resolve().is_relative_to(DIRECTORY.resolve()) or sha(archive) != expected:
        raise ValueError("pinned public archive mismatch: " + str(archive))
    drifts[str(path)] = {"source": str(path), "expectedSha256": expected, "observedSha256": observed,
                         "archive": str(archive), "archiveSha256": sha(archive),
                         "classification": "Original evidence verified from its pre-existing public archive; current external bytes differ"}
    return archive


def pointer(document, value):
    value = value.removeprefix("#")
    if not value:
        return document
    if not value.startswith("/"):
        raise ValueError("invalid JSON pointer: " + value)
    for token in value[1:].split("/"):
        token = token.replace("~1", "/").replace("~0", "~")
        document = document[int(token)] if isinstance(document, list) else document[token]
    return document


def nodes(value, at=""):
    yield at, value
    if isinstance(value, dict):
        for key, child in value.items():
            yield from nodes(child, at + "/" + key.replace("~", "~0").replace("/", "~1"))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from nodes(child, at + "/" + str(index))


def verify_correction(form, provenance, check, resolve=None):
    resolve = resolve or (lambda path, expected: resolve_public_source(path, expected, provenance, {}))
    correction = form["metadataTransportCorrection"]
    roots = {}
    pinned_files = {}
    for label, name in (("correction", "metadata-transport-correction-20261029-f7Z2ajmP"),
                        ("later", "provider-inventory-resume-20261029T175713722948Z")):
        matches = [record for record in provenance["sourceManifests"] if Path(record["path"]).parent.name == name]
        check("onePinnedBundle." + label, len(matches) == 1)
        roots[label] = Path(matches[0]["path"]).parent
        bundle = correction["sealedCorrection" if label == "correction" else "laterDiscoveredProviderEvidence"]
        descriptor = bundle["manifest"]
        check("canonicalBundlePin." + label,
              descriptor == {key: matches[0][key] for key in ("path", "sha256")})
        manifest = load(resolve(Path(matches[0]["path"]), matches[0]["sha256"]))
        pinned_files[label] = manifest.get("files", manifest)
        filenames = {"verification": "verification.json", "missingInputRequirements": "owner-return-required.json"}
        if label == "later":
            filenames["observation"] = "observations/observation-677a090bcb2548eb90aeccf82f35d982.json"
        for key, relative in filenames.items():
            path = roots[label] / relative
            check("pinnedSourceReference." + label + "." + key,
                  bundle[key] == {"path": str(path), "sha256": pinned_files[label][relative]})
            resolve(path, pinned_files[label][relative])
    pending = correction["pendingReturns"]
    check("exactTwoPendingReturnInputs", isinstance(pending, list) and len(pending) == 2 and
          {item["input"] for item in pending} == set(EXPECTED_RETURNS))
    source_path = roots["correction"] / "owner-return-required.json"
    source_hash = pinned_files["correction"]["owner-return-required.json"]
    source = load(resolve(source_path, source_hash))["missingInputs"]
    for requirement in pending:
        name = requirement["input"]
        canonical = EXPECTED_RETURNS[name]
        check("exactCanonicalPendingField." + name,
              requirement["canonicalOwnerFormField"] == canonical and pointer(form, canonical) is None)
        check("pinnedReturnPaths." + name, all(
            requirement[field] == str(roots[label] / name) for field, label in (
                ("expectedExternalReturnPath", "correction"), ("laterSourceRequestedPath", "later"))))
        matches = [index for index, item in enumerate(source) if item["input"] == name]
        check("oneMatchingSealedRequirement." + name, len(matches) == 1)
        check("matchingSealedRequirementReference." + name, requirement["requirementReference"] == {
            "path": str(source_path), "sha256": source_hash, "jsonPointer": "/missingInputs/" + str(matches[0])})
        check("actualReturnPending." + name,
              requirement["status"] == "PendingOwnerDelivery" and requirement["templateIsActualReturn"] is False and
              all(not (roots[label] / name).exists() for label in ("correction", "later")))
        check("dependentFactsStillPending." + name, all(
            pointer(form, field) is None for field in requirement["satisfiesOnlyAfterActualValidation"]))
    check("reconciliationFalseInvariants", all(correction[key] is False for key in (
        "runtimeSourceChanged", "scopeApprovalRequestMade", "recoveryWindowExtended",
        "completeReplacementPacket", "dependencyAvailable", "fullDeliveryComplete")))
    check("reconciliationZeroCounters", all(type(correction[key]) is int and correction[key] == 0 for key in (
        "liveInvocations", "qualificationInvocations", "cohortAttemptsReserved")))
    retained = load(resolve(roots["correction"] / "verification.json", pinned_files["correction"]["verification.json"]))
    check("copiedCorrectionFixtureResults", all(type(correction["implementationEvidence"][key]) is int and
          correction["implementationEvidence"][key] == retained[key] for key in ("executorTests", "preparerTests", "failed", "skipped")))


def verify_provider_summary(latest, result, observation, check):
    statuses = observation["facts"]["provider"]["metadataHttpStatuses"]
    health = observation["facts"]["health"]
    check("copiedProviderMetadataStatuses", len(statuses) == 4 and result["metadataHttpStatuses"] == statuses and
          latest["fourExactKeyMetadataHttpStatuses"] == list(statuses.values()))
    check("copiedProviderFixtureResults", all(type(latest[target]) is int and latest[target] == result["focusedTests"][source]
          for target, source in (("retainedFocusedTests", "run"), ("failed", "failures"), ("skipped", "skips"))))
    check("copiedProviderCompletion", latest["completeProviderMetadataVerified"] is result["providerMetadataVerified"])
    check("copiedProviderReadiness", latest["authorityReadyHttpStatus"] == health["authority"]["/ready"]["status"] and
          all(latest["inventoryAndCustodyReadyHttpStatus"] == health[role]["/ready"]["status"] for role in ("inventory", "custody")))


def verify_runtime_and_whitespace(provenance, check):
    # The initial runtime set was clean. HEAD comparison covers staged and unstaged tracked changes.
    argv = ["git", "diff", "--binary", "HEAD", "--", "src", "eng", "infrastructure"]
    digest = hashlib.sha256(subprocess.run(argv, cwd=ROOT, capture_output=True, check=True).stdout).hexdigest()
    check("runtimeDiffPreserved", digest == provenance["runtimeDiffSha256"])
    untracked_runtime = subprocess.run(
        ["git", "ls-files", "--others", "--exclude-standard", "-z", "--", "src", "eng", "infrastructure"],
        cwd=ROOT, capture_output=True, check=True).stdout
    check("noUntrackedRuntimeSource", untracked_runtime == b"")
    owned = [str(path) for path in (FORM_PATH, README_PATH, SPEC_PATH, DIRECTORY, PLANNING_PATH)]
    whitespace = subprocess.run(["git", "diff", "--check", "HEAD", "--", *owned],
                                cwd=ROOT, capture_output=True, check=False)
    check("scopedTrackedWhitespace", whitespace.returncode == 0)
    untracked = subprocess.run(["git", "ls-files", "--others", "--exclude-standard", "-z", "--", *owned],
                              cwd=ROOT, capture_output=True, check=True).stdout
    for relative in untracked.split(b"\0"):
        if relative:
            path = ROOT / relative.decode("utf-8", errors="surrogateescape")
            whitespace = subprocess.run(["git", "diff", "--no-index", "--check", "--", "/dev/null", str(path)],
                                        cwd=ROOT, capture_output=True, check=False)
            check("scopedUntrackedWhitespace." + str(path.relative_to(ROOT)),
                  whitespace.returncode in (0, 1) and not whitespace.stdout and not whitespace.stderr)


def verify(saving_report=False):
    provenance = load(DIRECTORY / "provenance.json")
    before = load(DIRECTORY / "prior-owner-decisions.json")
    form = load(FORM_PATH)
    correction = form["metadataTransportCorrection"]
    checks = []
    source_drifts = {}
    resolve = lambda path, expected: resolve_public_source(path, expected, provenance, source_drifts)
    reference = lambda value: resolve(Path(value["path"]), value["sha256"])

    def check(name, result):
        checks.append({"check": name, "passed": bool(result)})
        if not result:
            raise ValueError("reconciliation verification failed: " + name)

    for record in provenance["originalDocuments"] + provenance["publicSourceCopies"]:
        check("archive." + record["archivedAt"], sha(DIRECTORY / record["archivedAt"]) == record["sha256"])
    for record in provenance["preservedEvidence"] + provenance["authoritativeContext"]:
        check("preserved." + record["path"], sha(Path(record["path"])) == record["sha256"])

    bundle_results = []
    for record in provenance["sourceManifests"]:
        manifest = Path(record["path"])
        original_manifest = resolve(manifest, record["sha256"])
        check("manifestHash." + manifest.parent.name, sha(original_manifest) == record["sha256"])
        content = load(original_manifest)
        files = content.get("files", content)
        check("manifestCount." + manifest.parent.name, len(files) == record["filesVerified"])
        for relative, expected in files.items():
            path = manifest.parent / relative
            check("manifestFile." + manifest.parent.name + "/" + relative,
                  path.resolve().is_relative_to(manifest.parent.resolve()) and sha(resolve(path, expected)) == expected)
        bundle_results.append({"manifest": str(manifest), "filesVerified": len(files), "sha256": record["sha256"],
                               "evidenceBasis": "Pinned original bytes; any public-archive fallback is recorded separately",
                               "currentExternalBytesPreserved": not any(
                                   Path(source).is_relative_to(manifest.parent) for source in source_drifts)})

    null_paths = [path for path, value in nodes(before) if value is None]
    check("allPriorFactualNullsRemainNull", all(pointer(form, path) is None for path in null_paths))
    for key in before:
        if key != "mvpOperationalInputs":
            check("existingOwnerSectionUnchanged." + key, form[key] == before[key])
    mutable_text = {
        "/mvpOperationalInputs/maintenancePreservationBaseline/nextAction",
        "/mvpOperationalInputs/currentOperationalProfile/nextAction",
    }
    for path, value in nodes(before["mvpOperationalInputs"], "/mvpOperationalInputs"):
        if not isinstance(value, (dict, list)) and path not in mutable_text:
            check("existingReturnUnchanged." + path, pointer(form, path) == value)
    check("sevenCanonicalReturnGroupsRetained", len(form["mvpOperationalInputs"]) == 7)
    check("noExecutionAvailabilityOrAttemptPromotion", all(form[key] is False for key in (
        "installationAuthorized", "qualificationAuthorized", "attemptReserved", "dependencyAvailable")))

    references = 0
    for at, value in nodes(form):
        if isinstance(value, dict) and isinstance(value.get("path"), str) and "sha256" in value:
            path = reference(value)
            check("ownerReferenceHash." + at, sha(path) == value["sha256"])
            if "jsonPointer" in value:
                pointer(load(path), value["jsonPointer"])
                check("ownerReferencePointer." + at, True)
            references += 1

    verify_correction(form, provenance, check, resolve)

    sealed = correction["sealedCorrection"]
    retained = load(reference(sealed["verification"]))
    check("retained34And8FixtureResultsOnly", retained["executorTests"] == 34 and retained["preparerTests"] == 8 and
          retained["failed"] == retained["skipped"] == 0 and correction["implementationEvidence"]["rerunByThisReconciliation"] is False)
    for relative, expected in retained["sourceSha256"].items():
        check("testedExternalSource." + relative, sha(Path(sealed["manifest"]["path"]).parent / "tested-source" / relative) == expected)
    latest = correction["laterDiscoveredProviderEvidence"]
    result = load(reference(latest["verification"]))
    verify_provider_summary(latest, result, load(reference(latest["observation"])), check)
    check("laterEvidenceRemainsDatedAndIncomplete", result["observedAt"] == latest["observedAtUtc"] and
          result["focusedTests"] == {"failures": 0, "run": 45, "skips": 0} and
          list(result["metadataHttpStatuses"].values()) == [403] * 4 and result["providerMetadataVerified"] is False and
          result["expectedOwnerReturnsReceived"] is False and result["applyInvoked"] is False)
    scope_path = form["mvpOperationalInputs"]["currentOperationalProfile"]["preparedExpiredHmacRecoveryScopeReference"]["path"]
    window = load(Path(scope_path))["proposedWindow"]
    instant = lambda value: datetime.datetime.fromisoformat(value.replace("Z", "+00:00"))
    check("approvedScopeAndOriginalFixedWindowPreserved", correction["approvalAlreadyRecorded"] is True and
          correction["scopeApprovalRequestMade"] is False and correction["recoveryWindowExtended"] is False and
          window["automaticRebaseOrExtensionAllowed"] is False and
          instant(correction["recoveryExclusiveDeadlineUtc"]) == instant(window["exclusiveDeadline"]))

    links = 0
    for target in re.findall(r"\]\(([^)]+)\)", README_PATH.read_text()):
        target = target.split("#", 1)[0]
        if target and not target.startswith(("https://", "http://")):
            path = README_PATH.parent / target
            check("readmeLink." + target, path.exists() or
                  (saving_report and path.resolve() == DIRECTORY / "verification.json"))
            links += 1
    for path in DIRECTORY.rglob("*.json"):
        load(path)
    load(FORM_PATH)
    check("structuredJsonSyntax", True)
    legacy = provenance["legacyVerifier"]
    check("historicalVerifierRefusalPreserved", legacy["exitCode"] == 1 and
          "retainedDeadlineComparisonAndSeparateRecovery" in Path(legacy["stderr"]["path"]).read_text())
    for name in ("stdout", "stderr"):
        check("historicalVerifierCapture." + name, sha(Path(legacy[name]["path"])) == legacy[name]["sha256"])
    frozen = lambda text: re.search(r"<frozen-after-approval[\s\S]*?</frozen-after-approval>", text).group()
    check("frozenIntentPreserved", frozen(SPEC_PATH.read_text()) == frozen((DIRECTORY / "prior-spec.md").read_text()))
    check("fullDeliveryStillDeferredIncomplete", "full_delivery_status: 'deferred-incomplete'" in SPEC_PATH.read_text() and
          re.search(r"^status: '(?:in-progress|in-review)'$", SPEC_PATH.read_text(), re.MULTILINE))
    verify_runtime_and_whitespace(provenance, check)

    return {
        "schema": "platform.story-5.4.metadata-reconciliation-verification.v1",
        "verifiedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "checksPassed": len(checks), "checks": checks, "sourceBundles": bundle_results,
        "externalSourceDrift": list(source_drifts.values()),
        "externalSourcePreservationScope": "Pinned historical original evidence only; source drift is explicit and establishes no freshness or acceptance",
        "priorFactualNullsPreserved": len(null_paths), "publicOwnerReferencesVerified": references,
        "markdownLinksVerified": links, "ownerFormSha256": sha(FORM_PATH), "readmeSha256": sha(README_PATH),
        "specSha256": sha(SPEC_PATH),
        "provenanceSha256": sha(DIRECTORY / "provenance.json"), "verifierSha256": sha(Path(__file__)),
        "historicalIndexObservation": {"stagedDiffSha256": provenance["stagedDiffSha256"],
                                       "currentIndexCompared": False, "classification": "Historical observation only"},
        "applicableDocumentationAcceptance": "Correction manifests/source hashes, pointers, archives and blocked-return boundaries verified",
        "fullPacketAndRecoveryFixtureAcceptance": "Deferred; actual installations, accepted shared target/dates and reopened full delivery remain prerequisites",
        "runtimeSourceChanges": 0, "liveInvocations": 0, "credentialOperations": 0,
        "qualificationInvocations": 0, "cohortAttemptsReserved": 0, "dependencyAvailable": False,
        "operationCountScope": "This offline reconciliation only; separate concurrent native metadata-access observations are not attributed to it",
        "completeReplacementPacket": False, "fullDeliveryComplete": False,
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", action="store_true", help="Save this revision's separate verification.json")
    args = parser.parse_args()
    report = json.dumps(verify(saving_report=args.report), indent=2) + "\n"
    if args.report:
        (DIRECTORY / "verification.json").write_text(report)
        print("Verified offline reconciliation; report saved to", DIRECTORY / "verification.json")
    else:
        print(report, end="")
