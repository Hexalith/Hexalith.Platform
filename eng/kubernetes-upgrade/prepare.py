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
ARTIFACTS = Path("_bmad-output/implementation-artifacts")
BASELINE = ARTIFACTS / "evidence/epic-4/initial-cluster-inventory.md"
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


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def story_status(path):
    # Only accept the single exact mapping, at the story mapping indentation.
    matches = re.findall(
        rf"^  {re.escape(STORY_40)}: ([a-z-]+)\s*(?:#.*)?$",
        path.read_text(encoding="utf-8"),
        re.MULTILINE,
    )
    return matches[0] if len(matches) == 1 else "unresolved"


def timestamp(value):
    if not isinstance(value, str):
        return None
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
        return parsed.astimezone(timezone.utc) if parsed.tzinfo else None
    except ValueError:
        return None


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
    source_files = (
        ARTIFACTS / f"{STORY_41}.md",
        ARTIFACTS / "epic-4-context.md",
        BASELINE,
        ARTIFACTS / "sprint-status.yaml",
        ARTIFACTS / f"{STORY_40}.md",
        Path("eng/kubernetes-upgrade/prepare.py"),
        Path("eng/kubernetes-upgrade/README.md"),
    )
    evidence_hashes = {str(path): sha256(project_root / path) for path in source_files}
    status = story_status(project_root / ARTIFACTS / "sprint-status.yaml")
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
            "intendedMinorHops": [{"from": "1.34", "to": "1.35", "targetPatch": None}],
            "hopChoice": "first supported candidate; subject to approval and compatibility",
            "additionalHops": "Only if selected; re-plan and revalidate before each hop",
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
            })
        write_record(directory, "hop-gate-1.34-to-1.35.json", {
            **base, "gateState": "closed", "verificationResult": "blocked",
            "fromMinor": "1.34", "toMinor": "1.35", "exactTargetPatch": None,
            "prerequisites": {check["condition"]: check["result"] for check in checks},
            "approval": approval, "signature": "not-created",
        })
        write_record(directory, "maintenance-transition.json", {
            **base, "verificationResult": "not-run",
            "quiesce": {"state": "not-started", "allTrackedWorkflowsTerminal": None},
            "cordon": {"state": "not-started", "observedUnschedulable": None},
            "drain": {"state": "not-started", "exitCode": None, "approvedExceptions": []},
        })
        write_record(directory, "minor-hop-1.34-to-1.35.json", {
            **base, "verificationResult": "not-run", "fromMinor": "1.34", "toMinor": "1.35",
            "exactTargetPatch": None, "commands": [], "resultingComponentVersions": None,
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
