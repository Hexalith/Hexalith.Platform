#!/usr/bin/env python3
"""Reconcile public qualification references without reading credentials or contacting a runtime.

The historical 67-input report is preserved as a distinct set. References supplied
later are never treated as installation, current trust, authority or qualification.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import time

import qualify as q


GROUPS = {
    "inventory": {
        "owner": "Platform Maintainer / independently governed current-inventory owner",
        "kind": "ImplementationAndIndependentProvisioning",
        "action": "Bind the actual custody executable to authenticated current inventory, native tenant enrollment and current profile/revocation reads; supply the installed workload identity, immutable image, exact caller scopes and public enrollment proof. Supply reserved-system SecurityObservation authority separately. Operator and Memories credentials are forbidden.",
        "dependsOn": ["profile", "caller-authorization"],
        "contracts": ["src/Hexalith.Platform.Secrets.Inventory/CurrentInventoryAuthority.cs", "src/Hexalith.Platform.Secrets.Runtime/DaprHmacAuthorityAdapter.cs", "src/Hexalith.Platform.Custody/DaprPlatformHmacBinding.cs"],
    },
    "profile": {
        "owner": "Independent approval / profile and actor-role authority owner",
        "kind": "ImplementationAndIndependentProvisioning",
        "action": "Independently govern and provision current profile, actor-role, public-trust and revocation services and the decision signer; return exact issuer, audience, version, UTC validity and public anchor references. Decision private keys and signing/request authority remain outside Platform and Agents custody; registry GlobalAdministrator is not approval authority.",
        "dependsOn": [],
        "contracts": ["src/Hexalith.Platform.Secrets.Runtime/DaprDecisionAuthorityAdapter.cs", "src/Hexalith.Platform.Custody/IndependentDecisionManifestVerifier.cs"],
    },
    "lifecycle": {
        "owner": "Architecture and lifecycle/protection owners",
        "kind": "BlockedComponentFeasibilityAndImplementation",
        "action": "Implement the approved Dapr-only shared-cut/recoverable-original custody design using a qualified semantic custody-lifecycle-v1 component; no application native provider dependency. Return the immutable component artifact, independently placed current authority, complete copy/holder inventory, original/restore references and measured operating/retention limits in custody-component.proposed.json. The offline F0 model supplies no physical guarantee: F1 transport/authorization/buffer proof and separately authorized F2/F3 physical all-copy non-revival, survivor and root-reissue proofs remain required. Ordinary OpenBao HMAC/KV/Raft semantics cannot supply this proof.",
        "dependsOn": ["profile", "private-compromise"],
        "contracts": ["src/Hexalith.Platform.Custody/ICustodyKeyLifecycleProvider.cs", "src/Hexalith.Platform.Custody/ICustodyKeyLifecycleAuthority.cs", "docs/implementation/ext-secrets-1-dek-provider-decision-2026-10-18.md", "docs/implementation/ext-secrets-1-erasure-authority-decision-2026-10-20.md", "eng/ext-secrets-1/provisioning/custody-component.proposed.json"],
    },
    "private-replay": {
        "owner": "EventStore / private replay composition owner",
        "kind": "ImplementationAndIndependentProvisioning",
        "action": "Compose authenticated query-replay/effect-replay and exact-original lookup for reserved-system TrustedEnvelopeReplay(Issuer,DeliveryNonce), conditional initial NoStream append of TrustedEnvelopeFirstSeen only, with a separate restricted registrar credential. Prove public, Workflow and target-handler denial; return exact stream-derivation and scope references.",
        "dependsOn": ["profile", "caller-authorization"],
        "contracts": ["src/Hexalith.Platform.Secrets.Runtime/DaprCapabilityAdapters.cs", "eng/ext-secrets-1/private-authority-bindings.example.json"],
    },
    "capability-signing": {
        "owner": "Platform capability-signing custody provisioning owner",
        "kind": "ImplementationAndIndependentProvisioning",
        "action": "Bind the actual capability-signing provider to its independently enrolled signing key and current purpose/tenant authority, with restricted credential reference and exact-original sign/result recovery. This capability key is distinct from independently governed decision-approval keys; never grant approval authority to the custodian.",
        "dependsOn": ["profile", "caller-authorization", "private-compromise"],
        "contracts": ["src/Hexalith.Platform.Custody/IDeletionCapabilitySigningProvider.cs", "src/Hexalith.Platform.Secrets.Runtime/DaprCapabilityAdapters.cs"],
    },
    "private-compromise": {
        "owner": "EventStore / authenticated private compromise composition owner",
        "kind": "ImplementationAndIndependentProvisioning",
        "action": "Bind the existing private revocation subscriber to the independently authenticated current publisher and actual protection owner; enroll separate subscriber credentials and establish protection block before exact guard mirror. Return immutable identities, versions, subscription, trust and denial proofs; expose no public, Workflow or approval-signing path.",
        "dependsOn": ["profile", "caller-authorization"],
        "contracts": ["src/Hexalith.Platform.Custody/PlatformPrivateCapabilityComposition.cs", "src/Hexalith.Platform.Custody/DeletionCapabilityRevocationSubscription.cs"],
    },
    "caller-authorization": {
        "owner": "Independent workload identity and per-role provisioning owners",
        "kind": "ImplementationAndIndependentProvisioning",
        "action": "Install independent workload caller authentication and current operation/target scopes for each role. Supply separate Dapr API and app-channel credential references plus caller-authenticator and scope-authority references. A sidecar token alone cannot authorize a workload; mount credential files privately and return reference/ACL evidence only.",
        "dependsOn": ["profile"],
        "contracts": ["src/Hexalith.Platform.Secrets.Runtime/QualificationHttpHost.cs", "src/Hexalith.Platform.Secrets.Runtime/QualificationHost.cs", "eng/ext-secrets-1/provisioning/role-inputs.proposed.json"],
    },
    "qualification-roles": {
        "owner": "Independent executor, observer and enrollment provisioning owners",
        "kind": "ImplementationAndIndependentProvisioning",
        "action": "Implement and register the real executor, independently persisted-state observer, enrollment provider, durable original store and caller authenticator in the existing role hosts. Independently enroll three distinct P-256 signer identities/app IDs, immutable versions and public anchors, isolated targets for the complete fixed plan, current owner read scopes and nonrollback originals. Proposed Deployments and fixtures are not enrolled providers.",
        "dependsOn": ["inventory", "profile", "lifecycle", "private-replay", "private-compromise", "capability-signing", "caller-authorization"],
        "contracts": ["src/Hexalith.Platform.Secrets.Runtime/QualificationHost.cs", "src/Hexalith.Platform.Secrets.Qualification.Executor/Program.cs", "src/Hexalith.Platform.Secrets.Qualification.Observer/Program.cs", "src/Hexalith.Platform.Secrets.Qualification.Enrollment/Program.cs"],
    },
    "acceptance": {
        "owner": "Agents register owner acting on independently enrolled public trust",
        "kind": "OperatorAcceptanceAfterIndependentProvisioning",
        "action": "After independent installation/qualification prerequisites and source review, accept a canonical immutable candidate, exact command, complete configuration/plan hashes and exact public-anchor byte hashes in RuntimeQualificationBinding. Run the accepted command from the accepted Agents layout, preserving original journals. Local tests or a replacement packet do not establish Available.",
        "dependsOn": ["qualification-roles"],
        "contracts": ["eng/ext-secrets-1/prepare_acceptance.py", "eng/ext-secrets-1/qualify.py"],
    },
    "operator-reference": {
        "owner": "Platform runtime provisioning owner",
        "kind": "OperatorPublicReferenceInput",
        "action": "Return the exact existing public resource/credential-reference identity with authenticated installation and current permission evidence. Do not return credential values, create substitute tenants or infer health/installation from a configured string.",
        "dependsOn": ["inventory"],
        "contracts": ["eng/ext-secrets-1/runtime-reference.example.json"],
    },
}

NAMED_GROUPS = {
    "AcceptedEnrollmentPublicAnchor": "acceptance", "AcceptedExecutorPublicAnchor": "acceptance",
    "AcceptedObserverPublicAnchor": "acceptance", "PinnedPublicAnchorSha256": "acceptance",
    "CurrentProfileValidity": "profile", "IndependentCurrentInventoryAuthority": "inventory",
    "IndependentDecisionSignerAndActorRoleAuthority": "profile",
    "IndependentEnrolledQualificationAuthorities": "qualification-roles",
    "IndependentPersistedStateObserver": "qualification-roles",
    "IndependentRoleAppChannelAndAuthenticatedCallerScopes": "caller-authorization",
    "InstalledOwnerQualificationExecutor": "qualification-roles", "IsolatedTargetEnrollmentAuthority": "qualification-roles",
    "MountedQualificationDaprCredential": "caller-authorization",
    "PrivateAuthenticatedCompromiseSubscriberBinding": "private-compromise",
    "PrivateExactReplayRegistrarBinding": "private-replay",
    "QualifiedAllCopyLifecycleAdapter": "lifecycle", "QualifiedNonrollbackRestoreAdapter": "lifecycle",
}

LIFECYCLE_FIELDS = {"allCopyLifecycleAuthorityReference", "allCopyInventoryReference", "nonrollbackJournalReference",
                    "restoreTarget", "authenticatedRequesterTransport", "migrationAuthorityReference",
                    "protectionOwnerReference", "publicAnchorRetentionReference"}
PROFILE_FIELDS = {"profileReference", "issuer", "audience", "profileVersion", "profileNotBefore", "profileValidUntil",
                  "actorRoleAuthorityReference", "independentSignerAccount", "independentSignerCredentialReference",
                  "publicTrustReference", "currentRevocationReference"}
# Freeze the original evidence independently of evolving client prerequisites.
HISTORICAL_REFERENCES = frozenset("runtime." + field for field in (
    "actorRoleAuthorityReference", "allCopyInventoryReference", "allCopyLifecycleAuthorityReference", "audience",
    "authenticatedRequesterTransport", "compromiseGuardMirrorReference", "compromisePublisherReference",
    "compromiseSubscriberCredentialReference", "compromiseSubscriptionReference", "currentInventoryAuthorityReference",
    "currentRevocationReference", "independentSignerAccount", "independentSignerCredentialReference", "issuer",
    "migrationAuthorityReference", "nonrollbackJournalReference", "profileNotBefore", "profileReference", "profileValidUntil",
    "profileVersion", "protectionOwnerReference", "publicAnchorRetentionReference", "publicTrustReference",
    "replayExactScope", "replayRegistrarCredentialReference", "restoreTarget")) | frozenset(
    "qualification." + field for field in ("daprApiCredentialReference", "enrollmentCredentialReference", "isolatedTargetId")) | frozenset(
    f"qualification.{role}.{field}" for role in ("enrollment", "executor", "observer")
    for field in ("appChannelCredentialReference", "appId", "callerAuthenticatorReference", "callerScopeAuthorityReference", "identity", "publicAnchorFile", "version"))
HISTORICAL_INPUTS = HISTORICAL_REFERENCES | frozenset(NAMED_GROUPS)
HISTORICAL_EVIDENCE_SHA256 = "8bcb5bec059fd2965532aa867cf11fe0c4270d3983b39955c80834395e4226d9"
COHORT_OBLIGATIONS = {
    "OQ-5.4-1.CommittedProtectionOwner": "Require an independently accepted EXT-PROTECTION-1 target and real lifecycle/protection installation before the joint cohort.",
    "OQ-5.4-1.SharedPlatformTarget": "Reconcile EXT-SECRETS-1 and EXT-HOST-1 to one canonical immutable Platform target or an explicitly accepted compatible layout.",
    "OQ-5.4-1.AcceptedPinnedHostProfile": "Accept an actual host qualification profile and exact command whose --profile references that profile and whose verifier has a genuine pass path.",
    "OQ-5.4-1.JointLivePhaseRunner": "Provide the actual accepted joint phase runner and current independently enrolled participant bindings; per-owner local checks cannot implement OQ-5.4-1.",
}


def group_for(name):
    if name in NAMED_GROUPS:
        return NAMED_GROUPS[name]
    if name not in q.REQUIRED:
        raise ValueError("UnrecognizedQualificationInput")
    field = name.split(".")[-1]
    if name.startswith("qualification."):
        if "Credential" in field or field in ("callerAuthenticatorReference", "callerScopeAuthorityReference"):
            return "caller-authorization"
        return "qualification-roles"
    if field in LIFECYCLE_FIELDS:
        return "lifecycle"
    if field in PROFILE_FIELDS:
        return "profile"
    if field.startswith("compromise"):
        return "private-compromise"
    if field.startswith("replay"):
        return "private-replay"
    if field.startswith("capabilitySigner"):
        return "capability-signing"
    if field == "currentInventoryAuthorityReference":
        return "inventory"
    return "operator-reference"


def missing_references(config):
    return sorted(name for name in q.REQUIRED if not isinstance(q.get(config, name), str) or not q.get(config, name))


def derive_state(name, config, missing, observed_at):
    if name in q.REQUIRED:
        return "MissingPublicReference" if name in missing else "PublicReferenceSuppliedNotVerified"
    if name == "CurrentProfileValidity":
        try:
            current = q.utc_epoch(q.get(config, "runtime.profileNotBefore")) <= observed_at < q.utc_epoch(q.get(config, "runtime.profileValidUntil"))
            return "ConfiguredWindowCurrentNotIndependentlyVerified" if current else "ConfiguredWindowNotCurrent"
        except (ValueError, TypeError):
            return "ConfiguredWindowMissingOrInvalid"
    if name == "MountedQualificationDaprCredential":
        return "MissingPublicReference" if "qualification.daprApiCredentialReference" in missing else "PrivateMountAndCurrentScopeNotInspected"
    return "IndependentInstallationOrAcceptanceEvidenceRequired"


def public_hash(path, data=None):
    path = Path(path).resolve()
    return {"path": str(path), "sha256": hashlib.sha256(q.bounded_public_file(path, 8 * 1024 * 1024) if data is None else data).hexdigest()}


def credential_files(config):
    """Identify configured private file references; never open their contents."""
    paths = set()
    def visit(value):
        if not isinstance(value, dict):
            return
        for name, child in value.items():
            if isinstance(child, dict):
                visit(child)
            elif "credential" in name.lower() and isinstance(child, str):
                if child.startswith("file:/"):
                    paths.add(Path(child[5:]).resolve())
                elif child.startswith("/"):
                    paths.add(Path(child).resolve())
    visit(config)
    return paths


def credential_inodes(private_paths):
    private_inodes = set()
    for path in private_paths:
        try:
            metadata = path.stat()
            private_inodes.add((metadata.st_dev, metadata.st_ino))
        except OSError:
            pass
    return private_inodes


def reject_credential_sources(sources, private_paths, forbidden_inodes=()):
    private_inodes = set(forbidden_inodes) | credential_inodes(private_paths)
    for source in sources:
        path = Path(source).resolve()
        if path in private_paths:
            raise ValueError("PublicEvidenceCannotReferenceConfiguredCredential")
        try:
            metadata = path.stat()
        except OSError:
            continue  # The bounded public read reports absent/inaccessible evidence.
        if (metadata.st_dev, metadata.st_ino) in private_inodes:
            raise ValueError("PublicEvidenceCannotAliasConfiguredCredential")
    return private_inodes


def reconcile(original_evidence, configuration, output, source_evidence=(), observed_at=None, register=None, target=None):
    """Read only explicit public evidence/configuration and write a fresh reference-only packet."""
    original_evidence, configuration, output = Path(original_evidence).resolve(), Path(configuration).resolve(), Path(output).resolve()
    captured = {str(configuration): q.bounded_public_file(configuration, 1024 * 1024)}
    config = q.parse_public_configuration(captured[str(configuration)])
    private_paths = credential_files(config)
    # Reject known private paths and inode aliases before any requested source is read.
    known_private_inodes = reject_credential_sources([original_evidence, configuration, *source_evidence, *([register] if register is not None else [])], private_paths)
    def capture(path, limit=1024 * 1024):
        key = str(Path(path).resolve())
        if key not in captured:
            # Preserve observed private identities through credential rotation, and refresh
            # them before each open. The reader checks the actual descriptor before os.read.
            known_private_inodes.update(credential_inodes(private_paths))
            captured[key] = q.bounded_public_file(key, limit, forbidden_inodes=known_private_inodes)
        return captured[key]
    original_data = capture(original_evidence)
    if hashlib.sha256(original_data).hexdigest() != HISTORICAL_EVIDENCE_SHA256:
        raise ValueError("ExactHistoricalEvidenceSha256Required")
    original = q.decode_json(original_data)
    if not isinstance(original, dict) or original.get("code") != "DependencyNotAvailable" \
            or original.get("dependency") != "EXT-SECRETS-1" or original.get("dependencyAvailable") is not False \
            or original.get("effectsStarted") is not False \
            or type(original.get("qualificationEffects")) is not int or original["qualificationEffects"] != 0 \
            or not isinstance(original.get("missingInputs"), list) \
            or any(not isinstance(v, str) for v in original["missingInputs"]) \
            or len(original["missingInputs"]) != 67 or set(original["missingInputs"]) != HISTORICAL_INPUTS:
        raise ValueError("ExactHistoricalZeroEffect67InputReportRequired")
    # Expose newly required unknowns explicitly without altering existing accepted values.
    for name in q.REQUIRED:
        parts = name.split(".")
        parent = config
        for part in parts[:-1]:
            parent = parent.setdefault(part, {})
        parent.setdefault(parts[-1], None)
    observed_at = int(time.time()) if observed_at is None else observed_at
    if type(observed_at) is not int or observed_at < 0:
        raise ValueError("IntegralObservationTimeRequired")
    missing = missing_references(config)
    current_cohort_missing, checked_register_target = [], None
    if register is not None:
        register_text = capture(register).decode("utf-8")
        checked_register_target = target if target is not None else q.acceptance(register, register_text=register_text)[0]
        if not isinstance(checked_register_target, str) or not q.COMMIT.fullmatch(checked_register_target):
            raise ValueError("ExactRegisterOrCandidateTargetRequired")
        current_cohort_missing = q.cohort_prerequisite_inputs(register, checked_register_target, register_text=register_text)
    elif target is not None:
        raise ValueError("RegisterRequiredForCandidateTarget")
    source_root = Path(__file__).resolve().parents[2]
    contract_sources = {source_root / contract for group in GROUPS.values() for contract in group["contracts"]}
    evidence_paths = [original_evidence, configuration, Path(__file__), Path(q.__file__), Path(__file__).with_name("cases.py"), *contract_sources, *source_evidence]
    if register is not None:
        evidence_paths.append(register)
    known_private_inodes.update(reject_credential_sources(evidence_paths, private_paths, known_private_inodes))
    hashes = sorted((public_hash(path, capture(path, 8 * 1024 * 1024)) for path in set(map(lambda p: str(Path(p).resolve()), evidence_paths))), key=lambda value: value["path"])
    rows = []
    for name in sorted(HISTORICAL_INPUTS):
        group_id = group_for(name)
        group = GROUPS[group_id]
        rows.append({"input": name, "group": group_id, "resolutionKind": group["kind"], "owner": group["owner"],
                     "state": derive_state(name, config, missing, observed_at), "action": group["action"],
                     "dependsOnGroups": group["dependsOn"], "sourceContracts": group["contracts"],
                     "resolvedByThisPacket": False})
    common = {"dependency": "EXT-SECRETS-1", "apply": False, "effects": 0, "qualificationPassed": False,
              "dependencyAvailable": False, "productionMutationAllowed": False, "credentialsInspected": False,
              "credentialMetadataCompared": bool(private_paths),
              "runtimeContacted": False, "installationInferredFromReferences": False}
    report = {"schema": "EXT-SECRETS-1.input-reconciliation.v1", **common, "observedAt": observed_at,
              "historicalMissingInputCount": 67, "historicalRequiredReferenceCount": 50,
              "historicalDerivedOrImplementationCount": 17, "currentMissingPublicReferences": missing,
              "additionalCurrentMissingPublicReferences": sorted(set(missing) - HISTORICAL_INPUTS),
              "currentRequiredReferenceCount": len(q.REQUIRED), "currentReferenceCount": len(q.REQUIRED) - len(missing),
              "suppliedPublicReferences": sorted(set(q.REQUIRED) - set(missing)), "pendingConfigurationSha256": q.digest(config),
              "resolutionKindCounts": dict(sorted(Counter(row["resolutionKind"] for row in rows).items())),
              "sourceEvidence": hashes, "inputs": rows,
              "unverifiedLivePrerequisites": list(q.IMPLEMENTATIONS),
              "checkedRegisterTarget": checked_register_target, "currentCohortMissingInputs": current_cohort_missing,
              "supplementalCohortPrerequisites": [{"input": name, "action": action,
                  "state": "NotCheckedByThisPublicReferencePacket" if register is None else
                           "RegisterOrImplementationPrerequisiteMissing" if name in current_cohort_missing else
                           "RegisterConditionMetNotLiveVerified"} for name, action in COHORT_OBLIGATIONS.items()],
              "acceptanceRequired": ["CanonicalImmutableTarget", "ExactOwnerCommand", "RuntimeQualificationBinding", "CompletePersistedLiveEvidence"],
              "limitations": ["This is a public-reference reconciliation, not the accepted-target preflight or an installed-resource census.",
                              "Credential contents, public-anchor contents, runtime services and operator identities are not inspected; path/inode metadata is compared only to reject configured credential aliases.",
                              "Supplied references cannot resolve independent installation, current authorization, trust acceptance or physical all-copy/nonrollback proof."]}
    requests = {"schema": "EXT-SECRETS-1.provisioning-requests.v1", **common, "sourceEvidence": hashes,
                "acceptedPolicy": {"host": "192.168.1.30", "integrationDate": "2026-10-08", "timing": {"L": 300, "S": 30, "O": 600, "H": 86400, "R": 604800},
                                   "decisionKeysOutsideCustody": True, "overlappingExportDisposition": "Open"},
                "groups": [{"id": group_id, **group, "historicalInputs": [row["input"] for row in rows if row["group"] == group_id],
                            "requestedPublicReferenceValues": {name: None for name in missing if group_for(name) == group_id},
                            "requiredReturnEvidence": ["ImmutableInstalledIdentityAndVersion", "CurrentIndependentEnrollmentAndRevocation", "ExactLeastPrivilegeScopesAndDenials", "IndependentPersistedOwnerObservation"],
                            "credentialValuesPermitted": False} for group_id, group in GROUPS.items()]}
    # A strict runtime template preserves accepted references and nulls. It changes no acceptance pin.
    if output.exists():
        raise ValueError("FreshOutputDirectoryRequired")
    output.mkdir(parents=True)
    for filename, value in (("input-resolution-ledger.json", report), ("input-resolution-provisioning-requests.json", requests),
                            ("runtime-reference.pending.json", config)):
        q.RunJournal._publish(output / filename, value)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original-evidence", required=True, help="Public original zero-effect 67-input live-evidence.json")
    parser.add_argument("--configuration", required=True, help="Explicit public installed/pending runtime-reference.json")
    parser.add_argument("--output", required=True, help="Fresh detached output directory")
    parser.add_argument("--source-evidence", action="append", default=[], help="Additional public evidence or source file to hash; never a credential")
    parser.add_argument("--register", help="Optional explicit public Agents register for cohort prerequisite checks")
    parser.add_argument("--candidate-target", help="Optional exact candidate commit for shared-target checking, otherwise use the register target")
    args = parser.parse_args()
    try:
        report = reconcile(args.original_evidence, args.configuration, args.output, args.source_evidence,
                           register=args.register, target=args.candidate_target)
    except (OSError, ValueError, TypeError, IndexError, AttributeError, q.Unavailable):
        print(json.dumps({"code": "PublicInputReconciliationBlocked", "effects": 0, "dependencyAvailable": False}, sort_keys=True))
        return 2
    print(json.dumps({"code": "PublicInputReconciliationPrepared", "historicalMissingInputCount": 67,
                      "currentMissingPublicReferenceCount": len(report["currentMissingPublicReferences"]),
                      "effects": 0, "dependencyAvailable": False}, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
