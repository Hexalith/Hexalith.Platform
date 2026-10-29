#!/usr/bin/env python3
"""Verify this blocked handoff's public files; performs no service or qualification calls."""

import argparse
import datetime
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify():
    directory = Path(__file__).resolve().parent
    root = directory.parents[2]
    previous = root / "docs/implementation/story-5-4-coupled-delivery-2026-10-25"
    spec = root / "_bmad-output/implementation-artifacts/spec-5-4-retained-operational-security-handoff.md"
    load = lambda path: json.loads(path.read_text())
    provenance = load(directory / "provenance.json")
    form = load(previous / "owner-decisions.json")
    original = load(directory / "prior-platform-documents/owner-decisions.json")
    dossier = load(directory / "acceptance-inputs.json")
    clean = load(directory / "preparation-evidence/clean-invocations.json")
    bound = load(directory / "preparation-evidence/bound-inputs-invocations.json")
    checks = []

    def check(name, value):
        checks.append({"check": name, "passed": bool(value)})
        if not value:
            raise ValueError("handoff verification failed: " + name)

    check("preservedAcceptedTargetDateHostBranchTimingAndJournals", form["preserved"] == original["preserved"])
    check("qualificationAvailabilityAttemptAndInstallationAuthorizationRemainFalse", all(
        form[key] is False for key in ("qualificationAuthorized", "attemptReserved", "dependencyAvailable", "installationAuthorized")))
    for key in ("D3_recovery", "D4_rolesAndTrust", "D5_implementations", "D6_releaseAndPhysicalProfile", "D7_protection", "D8_host", "D9_dates"):
        check("historicalOwnerFieldsUnchanged." + key, form[key] == original[key])
    for key in ("D1_backend", "D2_resources"):
        check("originalMissingResourceFieldsRetained." + key, all(
            form[key][name] == value for name, value in original[key].items() if name not in ("choices", "selectedChoice")))
    inputs = form["mvpOperationalInputs"]
    check("sevenAccountablePendingOwnerReturns", len(inputs) == 7 and all(
        value.get("owner") and value.get("status", "").startswith("Pending") for value in inputs.values()))
    check("noBackupLifetimeOrRestorePolicyInvented", all(inputs["backupAndRestore"][key] is None for key in (
        "backupRetentionSeconds", "privilegedRestoreSubjectsAndScopedCredentialReferences", "acceptedRestorePolicyReference",
        "restoreProcedureReference", "authorizedRecoveryTestEvidenceReference")))
    check("singleCanonicalBackupOwnerReturn", "canonicalBackupPolicyReturn" in form["mvpScopeAmendment"] and all(
        key not in form["mvpScopeAmendment"] for key in ("backupRetentionSeconds", "acceptedRestorePolicyReference")))
    baseline = inputs["maintenancePreservationBaseline"]
    check("retainedBaselineBlockerPinned", sha(Path(baseline["retainedBlockerReference"])) == baseline["retainedBlockerSha256"])
    check("providerFreshPublicFactsExplicitlyPending", baseline["providerPublicFactsOwner"] and
          baseline["freshPublicLeaseKeyAndPolicyEvidenceReference"] is None)
    deadline = load(directory / "deadline-assessment-2026-10-29.json")
    instant = lambda value: datetime.datetime.fromisoformat(value.replace("Z", "+00:00"))
    check("retainedDeadlineComparisonAndSeparateRecovery", all(
        instant(deadline[name]) < instant(deadline["observedClockUtc"]) for name in (
            "retainedProfileEnrollmentDeadline", "lastVerifiedDataCredentialDeadline", "lastVerifiedMetadataCredentialDeadline")) and
        deadline["currentInstalledValidityObserved"] is False and deadline["credentialsReadOrRenewed"] is False and
        deadline["separateOwnerRecoveryRequired"] is True and inputs["currentOperationalProfile"]["separatelyScopedOwnerRecoveryPlanReference"] is None)
    check("retainedDeadlineSourcePinned", sha(Path(deadline["retainedDeadlineEvidence"]["path"])) ==
          deadline["retainedDeadlineEvidence"]["sha256"])
    check("noReplacementAcceptanceOrQualificationClaim", dossier["completeReplacementPacket"] is False and
          dossier["acceptedReplacementCommit"] is None and all(dossier[key] is False for key in (
              "qualificationAuthorized", "attemptReserved", "dependencyAvailable")))
    check("noReplacementTargetsDatesOrCommandsInvented", all(dossier[key] is None for key in (
        "proposedSharedImmutablePlatformTarget", "explicitPlatformIntegrationDate", "explicitProtectionIntegrationDate")) and
        all(value is None for value in dossier["exactReplacementVerificationCommands"].values()))
    check("thirteenInstalledArtifactsRemainUnverified", len(dossier["installedArtifacts"]) == 13 and all(
        all(value is None for value in descriptor.values()) for descriptor in dossier["installedArtifacts"].values()))
    for name, manifest in (("clean", clean), ("bound", bound)):
        check(name + ".twoOfflinePreparationsRefusedWithoutEffects", manifest["cleanObservedCheckout"] and len(manifest["runs"]) == 2 and all(
            run["exitCode"] == 2 and run["preparationOnly"] and not run["liveOwnerCommandExecuted"] and
            run["result"]["completeReplacementPacket"] is False and run["result"]["targetCommit"] is None
            for run in manifest["runs"]))
        check(name + ".exactObservedSourceCommitPreserved", manifest["observedSourceCommit"] == provenance["observedSourceCommit"] ==
              "4e35f561b022caede7093f0c3f4301e3dcbd1e11")
        check(name + ".59And17MissingPrerequisites", len(manifest["runs"][0]["result"]["missingInputs"]) == 59 and
              len(manifest["runs"][1]["result"]["missingReferences"]) == 17)
        for run in manifest["runs"]:
            check(name + ".scriptBytes." + run["dependency"], sha(Path(run["argv"][1])) == run["scriptSha256"])
    for name, descriptor in bound["inputs"].items():
        check("boundInputSnapshot." + name, descriptor["sourceAndSnapshotMatchedAtCapture"] and
              sha(Path(descriptor["snapshotPath"])) == sha(Path(descriptor["sourcePath"])) == descriptor["sha256"])
    for run in bound["runs"]:
        check("boundInputHashes." + run["dependency"], run["inputSnapshotHashesRecheckedAfterRun"] and
              all(value == bound["inputs"][name]["sha256"] for name, value in run["inputHashes"].items()))
    for descriptor in provenance["preservedEvidence"]:
        check("preserved." + descriptor["path"], sha(Path(descriptor["path"])) == descriptor["sha256"])
    for descriptor in provenance["originalLocalDocuments"]:
        check("archivedOriginal." + descriptor["path"], sha(directory / descriptor["preservedAt"]) == descriptor["sha256"])
    historical = []
    for line in (previous / "SHA256SUMS").read_text().splitlines():
        expected, relative = line.split(None, 1)
        path = previous / relative
        if relative in ("owner-decisions.json", "installation-decision.md", "installation-commands.md"):
            path = directory / "prior-platform-documents" / relative
        historical.append(sha(path) == expected)
    check("historicalDeliveryManifestMatchesAllOriginalBytes", all(historical))
    sys.path.insert(0, str(root / "eng/ext-secrets-1"))
    from coordinated_packets import REQUIRED_ARTIFACTS, section
    register = Path(provenance["authoritativeRegister"]["path"]).read_text()
    for name, record in dossier["records"].items():
        content = section(register, name, complete=True)
        check("completeAcceptedSection." + name, hashlib.sha256(content.encode()).hexdigest() ==
              record["acceptedCompleteSectionSha256"] and content in (directory / "accepted-scope-snapshot.md").read_text())
    check("installedArtifactInventoryReusesExistingAdmission", list(dossier["installedArtifacts"]) == list(REQUIRED_ARTIFACTS))
    raw_stdout = directory / "preparation-evidence/EXT-HOST-1.stdout.json"
    check("initialHostStdoutIsExplicitlyRawEmptyCapture", raw_stdout.read_bytes() == b"")
    json_files = [path for path in directory.rglob("*.json") if path != raw_stdout and path.name != "verification.json"]
    for path in json_files:
        load(path)
    check("structuredJsonSyntax", True)
    markdown_links = 0
    for name in ("README.md",):
        for link in re.findall(r"\]\(([^)]+)\)", (directory / name).read_text()):
            target = link.split("#", 1)[0]
            if target:
                check("markdownLink." + link, (directory / target).exists())
                markdown_links += 1
    check("ownedSpecHasBoundedIntentAndExternalStoryRemainsIncomplete", "<frozen-after-approval" in spec.read_text() and
          "Agents Story 5.4 is external" in spec.read_text() and dossier["dependencyAvailable"] is False)
    owned = [str(previous.relative_to(root) / name) for name in (
        "owner-decisions.json", "installation-decision.md", "installation-commands.md")]
    owned += [str(directory.relative_to(root)), str(spec.relative_to(root))]
    whitespace = subprocess.run(["git", "diff", "--check", "--", *owned], cwd=root, capture_output=True, text=True, check=False)
    check("ownedDiffWhitespace", whitespace.returncode == 0)
    paths = [path for path in sorted(directory.rglob("*")) if path.is_file() and path.name != "verification.json"]
    paths += [previous / name for name in ("owner-decisions.json", "installation-decision.md", "installation-commands.md")]
    paths.append(spec)
    return {
        "schema": "platform.story-5.4.operational-handoff-verification.v1",
        "verifiedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "verificationCommand": "python3 docs/implementation/story-5-4-operational-security-2026-10-28/verify_handoff.py",
        "checks": checks, "checksPassed": len(checks), "structuredJsonFilesParsed": len(json_files),
        "rawStdoutCaptureExcludedFromStructuredJson": str(raw_stdout.relative_to(root)),
        "historicalManifestEntriesChecked": len(historical), "markdownLinksChecked": markdown_links,
        "preservedEvidenceFilesChecked": len(provenance["preservedEvidence"]),
        "currentAndArchivedArtifactHashes": [{"path": str(path.relative_to(root)), "sha256": sha(path)} for path in paths],
        "offlinePreparationOnly": True, "runtimeCodeChanged": False, "buildOrImplementationTestsRun": False,
        "freshHealthOrProviderObservation": False, "credentialValuesRead": False, "installationInvocations": 0,
        "maintenanceInvocations": 0, "qualificationInvocations": 0, "qualificationEffects": 0,
        "dependencyAvailable": False, "fullStory54Complete": False, "completeReplacementPacket": False,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path, help="Save the report; otherwise print it without modifying files")
    args = parser.parse_args()
    report = json.dumps(verify(), indent=2) + "\n"
    if args.report:
        canonical = Path(__file__).resolve().parent / "verification.json"
        if args.report.exists() and args.report.resolve() != canonical:
            parser.error("refusing to replace an existing noncanonical evidence file")
        args.report.write_text(report)
        print("Verified public handoff files; report saved to", args.report)
    else:
        print(report, end="")


if __name__ == "__main__":
    main()
