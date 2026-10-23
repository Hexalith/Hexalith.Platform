#!/usr/bin/env python3
"""Create local Story 4.1 preparation evidence; never authorize or run mutation."""

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import stat


STORY_40 = "4-0-prove-off-node-backups-and-isolated-restores"
STORY_41 = "4-1-upgrade-the-cluster-off-kubernetes-1-34-after-verified-backu"
STORY_427 = "4-27-retire-kubesphere-without-changing-workload-data"
ARTIFACTS = Path("_bmad-output/implementation-artifacts")
BASELINE = ARTIFACTS / "evidence/epic-4/initial-cluster-inventory.md"
TARGET_REVIEW = ARTIFACTS / "evidence/epic-4/4-26/20261004t095137z-target-review/target-review.json"
MAINTENANCE = Path("eng/kubernetes-upgrade/MAINTENANCE.md")
RETIREMENT_PROCEDURE = Path("eng/cluster-management/RETIRE-KUBESPHERE.md")
SOURCE_FILES = (
    ARTIFACTS / f"{STORY_41}.md",
    ARTIFACTS / "epic-4-context.md",
    BASELINE,
    ARTIFACTS / "sprint-status.yaml",
    ARTIFACTS / f"{STORY_40}.md",
    ARTIFACTS / f"{STORY_427}.md",
    TARGET_REVIEW,
    MAINTENANCE,
    RETIREMENT_PROCEDURE,
    Path("eng/cluster-management/README.md"),
    Path("eng/cluster-management/retire.py"),
    Path("eng/kubernetes-upgrade/prepare.py"),
    Path("eng/kubernetes-upgrade/README.md"),
)
PROOF_FILES = (
    "gate/backup-gate.json",
    "keycloak/keycloak-restore-proof.json",
    "openbao/openbao-restore-proof.json",
    "memories/memories-restore-proof.json",
)
VALIDATION_FILE = "validation/validation.json"
OPERATIONAL_FILES = (
    "preflight.json",
    "external-etcd-recovery-point.json",
    "external-etcd-isolated-restore.json",
    "node-recovery-bundle.json",
    "api-addon-compatibility.json",
    "dynamic-storage-proof.json",
    "pre-upgrade-health.json",
    "post-upgrade-health.json",
)
COMPATIBILITY_COMPONENTS = (
    "Calico/CNI", "CoreDNS", "kube-proxy", "OpenEBS/CSI/storage",
    "CloudNativePG", "Dapr", "Traefik", "cert-manager",
)
PROTECTED_WORKLOAD_CHECKS = {
    "keycloak": {
        "resources": ["keycloak/Cluster/keycloak-postgres", "keycloak/Keycloak pods"],
        "checks": ["database readiness and native replication health", "approved login/OIDC smoke"],
    },
    "openbao": {
        "resources": ["openbao/StatefulSet/hexalith-keys"],
        "checks": ["custodian-authenticated raft peers and unsealed status", "approved read/write canary without values"],
    },
    "memories": {
        "resources": ["hexalith-memories/StatefulSet/redis-stack", "hexalith-memories/StatefulSet/falkordb",
                      "hexalith-memories/Memories deployments", "hexalith-memories/StatefulSet/access-telemetry-postgresql"],
        "checks": ["Redis/FalkorDB persistence and telemetry database health", "API/MCP health",
                   "approved representative authenticated search expectation"],
    },
    "forgejo": {
        "resources": ["Forgejo namespace/kind/UID from fresh census"],
        "checks": ["repository/UI and native database health", "accountable disposition of source consistency findings"],
    },
    "forgejo-runner": {
        "resources": ["forgejo-runner/Deployment/forgejo-runner"],
        "checks": ["runner placement and status match fresh baseline", "all tracked jobs/workflows terminal before outage"],
    },
}


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def story_status(path, content=None):
    # Only accept the single exact mapping, at the story mapping indentation.
    matches = re.findall(
        rf"^  {re.escape(STORY_40)}: ([a-z-]+)\s*(?:#.*)?$",
        path.read_text(encoding="utf-8") if content is None else content,
        re.MULTILINE,
    )
    return matches[0] if len(matches) == 1 else "unresolved"


def timestamp(value):
    if not isinstance(value, str):
        return None
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
        return parsed.astimezone(timezone.utc) if parsed.tzinfo else None
    except (ValueError, OverflowError):
        return None


def selected_hops(review_bytes, captured_at):
    """Read the dated selection without accepting its currency or execution pins."""
    def unique_object(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Dated target review contains duplicate JSON keys")
            result[key] = value
        return result

    review = json.loads(review_bytes, object_pairs_hook=unique_object)
    reviewed_at = timestamp(review.get("checkedAt")) if isinstance(review, dict) else None
    if reviewed_at is None or reviewed_at > captured_at:
        raise ValueError("Dated target review requires a valid timezone-qualified checkedAt no later than preparation")
    hops = []
    for field, source_minor, target_minor in (("workloadHop", "1.34", "1.35"),
                                               ("workloadTarget", "1.35", "1.36")):
        selection = review.get(field)
        tag = selection.get("tag") if isinstance(selection, dict) else None
        if not isinstance(tag, str) or not re.fullmatch(rf"v{re.escape(target_minor)}\.(?:0|[1-9][0-9]*)", tag):
            raise ValueError(f"Dated target review must select an exact {target_minor} patch in {field}.tag")
        hops.append({"from": source_minor, "to": target_minor, "targetPatch": tag,
                     "selectionField": f"{field}.tag"})
    return hops, review["checkedAt"]


def inspect_record(bundle, name, now):
    result = {"relativePath": name, "present": False, "signaturePresent": False,
              "signatureValidation": "not-performed", "authorization": False}
    if bundle is None:
        result["state"] = "no-final-bundle-selected"
        return result
    path = bundle / name
    if path.is_symlink() or not path.is_file():
        result["state"] = "missing-or-not-regular"
        return result
    result.update(present=True, sha256=sha256(path))
    signature = Path(str(path) + ".sig")
    if signature.is_file() and not signature.is_symlink():
        result.update(signaturePresent=True, signatureSha256=sha256(signature))
    try:
        record = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(record, dict):
            raise ValueError("not an object")
    except (ValueError, UnicodeError):
        result["state"] = "malformed"
        return result
    if name == VALIDATION_FILE:
        validated_at = timestamp(record.get("validatedAt"))
        result["validationTimestampPresent"] = validated_at is not None
        result["validationTimestampNotFuture"] = validated_at is not None and validated_at <= now
    else:
        expiry = timestamp(record.get("expiresAt"))
        result["unexpiredTimestamp"] = expiry is not None and expiry > now
    # Retain only enums/booleans, never arbitrary strings from private records.
    result["declaresPass"] = record.get("verificationResult") == "pass"
    result["state"] = "local-metadata-only"
    return result


def write_record(directory, name, record):
    path = directory / name
    with path.open("x", encoding="utf-8") as stream:
        os.chmod(path, 0o600)
        json.dump(record, stream, indent=2, sort_keys=True)
        stream.write("\n")


def collection_requirements(name, hops):
    """Make missing collection scope reviewable without inventing observations."""
    per_hop = [{"fromMinor": hop["from"], "toMinor": hop["to"],
                "datedSelectedTargetPatch": hop["targetPatch"], "acceptedTargetPatch": None,
                "evidenceSha256": None, "verificationResult": "not-run"} for hop in hops]
    common = {"instructionsOnly": True, "freshEvidenceRequiredForEveryHop": True,
              "perHopEvidence": per_hop}
    if name == "api-addon-compatibility.json":
        return {**common, "minimumComponents": list(COMPATIBILITY_COMPONENTS),
                "additionalInstalledOperatorsAddonsAndWebhooks": None,
                "completeLiveAndDesiredInventoryEstablished": False,
                "requiredChecks": [
                    "fresh post-retirement census of live and all desired sources, including non-Helm resources and hooks",
                    "deprecated/removed API scans for all served-version clients and exact target schemas",
                    "field/schema/semantic checks, CRD stored/storage versions and conversion/admission dependencies",
                    "primary-source support and exact target-runtime clearance for every installed component",
                    "KubeSphere runtime, blocking admission dependencies and public route absent; qualified owner for retained/replaced dependencies",
                    "complete finding dispositions; version presence or a partial fixture cannot clear compatibility",
                ], "componentResults": {component: {"installedVersion": None, "qualifiedOwner": None,
                    "primarySourceEvidenceSha256": None, "targetRuntimeEvidenceSha256": None,
                    "verificationResult": "not-run"} for component in COMPATIBILITY_COMPONENTS}}
    if name in ("pre-upgrade-health.json", "post-upgrade-health.json"):
        return {**common, "workloads": {workload: {**requirements,
                    "ownerApprovedExpectationSha256": None, "resourceUidAndPvcBindingCensusSha256": None,
                    "versionsAndImageDigestsSha256": None, "nativeHealthEvidenceSha256": None,
                    "authenticatedSmokeEvidenceSha256": None, "verificationResult": "not-run"}
                    for workload, requirements in PROTECTED_WORKLOAD_CHECKS.items()},
                "additionalWorkloadsFromFreshOutageCensus": None,
                "preOutageBaselineSha256": None,
                "requiredChecks": ["compare concrete resource/PVC identities, readiness and data health before/after",
                    "record every workload outcome and approved recovery outcome if needed; retain no credentials or payloads"]}
    if name == "dynamic-storage-proof.json":
        return {**common, "requiredDynamicStorageClassesFromFreshCensus": None,
                "requiredChecks": ["scheduler-managed disposable PVC/pod for every required dynamic StorageClass",
                    "provision, non-secret write, independent read, deletion and direct provider-volume absence",
                    "record PVC/PV UIDs, provider handle, timestamps and command exits",
                    "before hop and after hop while cordoned; approved tolerations and WaitForFirstConsumer scheduling",
                    "explicit Retain-class provider cleanup; preserve all workload PVCs"]}
    return {}


def prepare(project_root, evidence_root, attempt_id, operator, backup_bundle=None):
    project_root = project_root.resolve()
    evidence_root = evidence_root.resolve()
    if evidence_root == project_root or project_root in evidence_root.parents:
        raise ValueError("Full attempt evidence must be outside the repository")
    if not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,79}", attempt_id):
        raise ValueError("Attempt ID must use lowercase letters, digits and hyphens")
    if backup_bundle:
        backup_bundle = backup_bundle.resolve()
        if evidence_root == backup_bundle or backup_bundle in evidence_root.parents:
            raise ValueError("Preparation must not write into a source recovery bundle")
    directory = evidence_root / "evidence/epic-4/4-1" / attempt_id
    if backup_bundle and (directory.resolve() == backup_bundle or backup_bundle in directory.resolve().parents):
        raise ValueError("Preparation must not write into a source recovery bundle")
    now = datetime.now(timezone.utc)
    captured_at = now.isoformat(timespec="seconds").replace("+00:00", "Z")
    # Resolve every required input before allocating an immutable attempt.
    source_bytes = {str(path): (project_root / path).read_bytes() for path in SOURCE_FILES}
    evidence_hashes = {name: hashlib.sha256(content).hexdigest() for name, content in source_bytes.items()}
    hops, reviewed_at = selected_hops(source_bytes[str(TARGET_REVIEW)], now)
    target_lineage = {"source": str(TARGET_REVIEW), "sha256": evidence_hashes[str(TARGET_REVIEW)],
                      "checkedAt": reviewed_at, "currencyValidation": "not-validated",
                      "compatibilityValidation": "not-validated", "executionPinsAccepted": False}
    procedure_lineage = {
        "historicalMaintenance": {"source": str(MAINTENANCE), "sha256": evidence_hashes[str(MAINTENANCE)],
                                  "approval": "not-validated", "supersededForExecution": True},
        "retirementProcedure": {"source": str(RETIREMENT_PROCEDURE),
                                "sha256": evidence_hashes[str(RETIREMENT_PROCEDURE)],
                                "acceptance": "not-validated"},
        "successorMaintenance": {"state": "blocked-until-accepted-retirement-and-fresh-census",
                                 "sha256": None, "approval": "not-approved"},
    }
    status_path = ARTIFACTS / "sprint-status.yaml"
    status = story_status(project_root / status_path, source_bytes[str(status_path)].decode("utf-8"))
    inputs = [inspect_record(backup_bundle, name, now) for name in PROOF_FILES]
    validation = inspect_record(backup_bundle, VALIDATION_FILE, now)
    checks = [
        {"condition": "story-4.0-done", "result": "pass" if status == "done" else "fail",
         "observedStatus": status},
        {"condition": "administrator-signed-backup-gate-and-three-proofs",
         "result": "not-validated", "localInputInventory": inputs,
         "missingInputs": [item["relativePath"] for item in inputs if not item["present"]],
         "unsignedInputs": [item["relativePath"] for item in inputs
                            if item["present"] and not item["signaturePresent"]],
         "unexpiredTimestampNotEstablished": [item["relativePath"] for item in inputs
                                              if not item.get("unexpiredTimestamp")],
         "offNodeReadback": "not-run", "immutableObjectProperties": "not-validated",
         "checksumMatch": "not-validated"},
        {"condition": "administrator-signed-passing-final-validation-with-exact-gate-manifest-bindings",
         "result": "not-validated", "localInputInventory": validation,
         "finalValidationGateManifestBinding": "not-validated",
         "requiredValidationBindings": ["backup-gate SHA-256", "evidence-manifest SHA-256",
                                        "recovery ID", "policy SHA-256"],
         "validationFreshness": "not-validated against signed policy and proof/gate expiry"},
        {"condition": "window-outage-and-owner-approval", "result": "not-approved"},
        {"condition": "fresh-external-etcd-snapshot-and-isolated-restore", "result": "not-run"},
        {"condition": "encrypted-off-node-node-recovery-bundle-and-tested-recovery",
         "result": "not-run"},
        {"condition": "sequential-live-preflight-supported-patch-api-addon-compatibility",
         "result": "not-run"},
        {"condition": "functional-storage-probe-for-every-required-class", "result": "not-run"},
        {"condition": "accepted-4.27-signed-retirement-preservation-and-private-native-access",
         "result": "not-validated", "statusIsAcceptance": False,
         "required": ["signed exact retirement and workload/PVC preservation outcome accepted",
                      "current independent private native-access proof",
                      "KubeSphere runtime, blocking admission dependencies and public route absent",
                      "every retained or replaced dependency has a qualified named owner"]},
        {"condition": "fresh-post-retirement-recovery-and-configuration-census",
         "result": "not-run", "required": ["fresh external-etcd point and matching isolated restore",
             "matching encrypted off-node node/configuration inventory and tested recovery",
             "fresh signed 4.0 proof/final validation and independent immutable-object readback"],
         "historicalBackupsSuffice": False},
        {"condition": "exact-successor-maintenance-procedure-and-outage-approval",
         "result": "not-approved", "required": ["accepted 4.27 and fresh remaining controller/admission/outage census",
             "derive successor while preserving historical MAINTENANCE.md bytes and hash",
             "clear remaining API/add-on/admission blockers, Forgejo consistency and authenticated smoke expectations",
             "bind exact successor SHA-256 and accountable stop/recovery/outage approval to each signed hop gate"]},
        {"condition": "fresh-per-hop-target-currency-and-compatibility",
         "result": "not-validated", "required": ["current supported exact patch and Rancher import compatibility",
             "fresh release/security review, binary/package checksums and image digests",
             "fresh live source versions, version skew and sequential kubeadm plan/preflight"]},
        {"condition": "fresh-authenticated-protected-workload-health", "result": "not-run",
         "requiredWorkloads": ["keycloak", "openbao", "memories", "forgejo", "forgejo-runner"],
         "required": "fresh authenticated native health and approved smoke expectations before each hop"},
    ]
    if directory.exists():
        raise ValueError("An attempt already exists; create a new attempt ID")
    for ancestor in [directory, *directory.parents]:
        if ancestor == evidence_root.parent:
            break
        if ancestor.exists() and (ancestor.stat().st_uid != os.getuid()
                                 or stat.S_IMODE(ancestor.stat().st_mode) & 0o077):
            raise ValueError("Evidence directories must be owner-only and owned by the operator")
    # Every new directory is private even with a permissive caller umask.
    old_umask = os.umask(0o077)
    try:
        directory.mkdir(parents=True, mode=0o700)
        base = {"schemaVersion": 1, "storyId": STORY_41, "attemptId": attempt_id,
                "recordedAt": captured_at, "classification": "sanitized preparation",
                "signed": False, "mutationAuthorized": False, "liveEvidenceCollected": False}
        approval = {"state": "not-approved", "approver": None, "windowStart": None,
                    "windowEnd": None, "incidentChannel": None, "incidentCommander": None,
                    "rollbackOwner": None, "workloadOwners": {}, "outageAcknowledgements": []}
        write_record(directory, "attempt.json", {
            **base, "operator": operator, "approval": approval, "evidenceHashes": evidence_hashes,
            "context": "jpiquot@local", "node": "node1", "liveSourceVersion": None,
            "baseline": {"version": "v1.34.9", "capturedAt": "2026-09-28T12:51:19Z",
                         "source": str(BASELINE), "fresh": False},
            "intendedMinorHops": hops, "targetLineage": target_lineage,
            "procedureLineage": procedure_lineage,
            "hopChoice": "dated retained two-hop selection; subject to fresh currency, compatibility and approval",
            "additionalHops": "No automatic chain; accept first-hop outcome and refresh every gate before second hop",
            "selectedBackupBundle": str(backup_bundle) if backup_bundle else None,
            "fullLogsAndRecoveryMaterial": "not-created",
        })
        write_record(directory, "gate-validation.json", {
            **base, "gateState": "closed", "verificationResult": "blocked",
            "preparationOnly": True, "checks": checks,
            "signature": "pending Administrator; no signing performed",
            "reason": "Local preparation cannot establish live, provider, signature or approval gates",
        })
        for name in OPERATIONAL_FILES:
            write_record(directory, name, {
                **base, "verificationResult": "not-run", "status": "pending-live-execution",
                "reason": "Operational evidence is unavailable during local-only preparation",
                "collectionRequirements": collection_requirements(name, hops),
            })
        for index, hop in enumerate(hops):
            suffix = f"{hop['from']}-to-{hop['to']}"
            previous = hops[index - 1] if index else None
            prerequisites = {check["condition"]: check["result"] for check in checks}
            if previous:
                prerequisites["accepted-first-hop-outcome"] = "not-accepted"
                prerequisites["new-recovery-preflight-and-approval-after-first-hop"] = "not-run"
            write_record(directory, f"hop-gate-{suffix}.json", {
                **base, "gateState": "closed", "verificationResult": "blocked",
                "fromMinor": hop["from"], "toMinor": hop["to"], "exactTargetPatch": hop["targetPatch"],
                "liveSourceVersion": None, "targetLineage": {**target_lineage, "selectionField": hop["selectionField"]},
                "procedureLineage": procedure_lineage, "evidenceHashes": evidence_hashes,
                "prerequisites": prerequisites,
                "refreshRequirements": list(prerequisites), "freshPrerequisiteRecordHashes": None,
                "preUpgradeHealth": {"record": "pre-upgrade-health.json", "sha256": None,
                                     "verificationResult": "not-run"},
                "previousHop": None if previous is None else {
                    "outcomeRecord": f"minor-hop-{previous['from']}-to-{previous['to']}.json",
                    "acceptedOutcomeSha256": None, "acceptance": "not-validated",
                    "actualResultingComponentVersions": None},
                "approval": approval, "signature": "not-created",
            })
            write_record(directory, f"minor-hop-{suffix}.json", {
                **base, "verificationResult": "not-run", "fromMinor": hop["from"], "toMinor": hop["to"],
                "exactTargetPatch": hop["targetPatch"], "commands": [], "resultingComponentVersions": None,
                "approvedGateRecord": f"hop-gate-{suffix}.json", "approvedGateSha256": None,
                "approvedSuccessorMaintenanceSha256": None,
                "postflightEvidenceHashes": {"post-upgrade-health.json": None, "dynamic-storage-proof.json": None},
            })
        write_record(directory, "maintenance-transition.json", {
            **base, "verificationResult": "not-run",
            "quiesce": {"state": "not-started", "allTrackedWorkflowsTerminal": None},
            "cordon": {"state": "not-started", "observedUnschedulable": None},
            "drain": {"state": "not-started", "exitCode": None, "approvedExceptions": []},
            "collectionRequirements": {
                "instructionsOnly": True, "approvedSuccessorProcedureSha256": None,
                "requiredTerminalStates": ["approved application quiesce and every tracked workflow terminal",
                    "independently observed node1 spec.unschedulable=true",
                    "drain terminal exit 0 with exact approved exceptions"],
                "accountableStopRecoveryDecisionSha256": None,
                "stopOn": ["pending, failed or ambiguous transition", "any failed or stale hop prerequisite",
                    "unhealthy API, node, CNI, DNS, storage, etcd or protected workload after a hop"],
                "recovery": "Stop, preserve diagnostics and follow the exact approved control-plane/etcd/workload procedure; no kubeadm downgrade or PVC replacement",
            },
        })
        write_record(directory, "upgrade-result.json", {
            **base, "verificationResult": "blocked", "state": "blocked-before-mutation",
            "complete": False, "supportedVersionEstablished": False,
            "actualFinalComponentVersions": None, "executedMinorHops": [],
            "workloadOutcomes": {name: "not-run" for name in ["keycloak", "openbao", "memories", "forgejo"]},
        })
        hashes = {path.name: sha256(path) for path in sorted(directory.glob("*.json"))}
        with (directory / "SHA256SUMS").open("x", encoding="utf-8") as stream:
            os.chmod(stream.name, 0o600)
            for name, digest in hashes.items():
                stream.write(f"{digest}  {name}\n")
    finally:
        os.umask(old_umask)
    return directory


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--evidence-root", type=Path, default=Path.home() / "hexalith-upgrade-evidence")
    parser.add_argument("--attempt-id", default=datetime.now(timezone.utc).strftime("%Y%m%dt%H%M%Sz-preparation"))
    parser.add_argument("--operator", required=True, help="Preparation operator; this assigns no approval role")
    parser.add_argument("--backup-bundle", type=Path, help="Selected local bundle to inventory; not proof validation")
    args = parser.parse_args()
    try:
        result = prepare(args.project_root, args.evidence_root, args.attempt_id, args.operator, args.backup_bundle)
    except (OSError, ValueError) as error:
        parser.exit(2, f"Preparation refused: {error}\n")
    print(f"Preparation evidence: {result}\nMutation gate: closed; no operational command was run")


if __name__ == "__main__":
    main()
