"""Complete owner qualification plan. No installed adapter or result is supplied here."""
from dataclasses import dataclass, asdict
import hashlib
import json


@dataclass(frozen=True)
class Case:
    id: str
    outcomes: tuple
    expected: dict
    boundary: str = "none"


def plan():
    cases = []

    def add(name, outcome="Denied", **expected):
        cases.append(Case(name, (outcome,), expected))

    add("S1.exact-provider-credential", "Resolved", exactReferenceCount=1)
    for family in ("TenantKek", "ContentDigest", "SecurityObservation", "ExportEnvelope", "ManifestSigning",
                   "TrustedEnvelope", "MigrationEpoch", "DirectoryEffect", "DeletionBatchCapability"):
        add(f"S1.inventory.{family}", "Resolved", exactReferenceCount=1, independentlyCurrentInventory=1)
    for reason in ("missing-config", "missing-reference", "denied", "cross-tenant", "wrong-purpose", "wrong-version",
                   "stale-inventory", "revoked", "changed-authority", "outage", "unexpected-key-response"):
        add(f"S1.reject.{reason}", forbiddenEffects=0)
    add("S1.rotation-invalidation", "Invalidated", staleSafetyVerdicts=0, retainedDigestVerifiers=1)
    for role in ("Product", "Governance", "Security", "Architecture", "Maintainer"):
        add(f"S1.independent-role.{role}", "Verified", currentActorRoleEvidence=1)
    for principal in ("recorder", "custodian", "workflow", "revocation-registrar"):
        add(f"S1.approval-prohibited.{principal}", approvalRequests=0, approvalSignatures=0)
    add("S2.canonical-hmac", "Verified", constantTimeVerification=1)
    for field in ("issuer", "audience", "operation", "principal", "actor", "binding", "roles", "target", "schema",
                  "logical-id", "tuple", "fingerprint", "digest-key-version", "nonce", "issued-at", "expires-at", "key-version"):
        add(f"S2.substitution.{field}", forbiddenEffects=0)
    for point, outcome in (("before", "Verified"), ("at", "Expired"), ("after", "Expired")):
        cases.append(Case(f"S2.expiry.{point}", (outcome,), {"forbiddenEffects": 0}, point))
    add("S2.future-skew", "Denied", forbiddenEffects=0)
    add("S2.maximum-lifetime", "Denied", forbiddenEffects=0)
    add("S2.overlap-before", "Verified", retainedEnvelopeVerifier=1)
    add("S2.overlap-at", "Denied", forbiddenEffects=0)
    add("S2.overlap-after", "Denied", forbiddenEffects=0)
    add("S2.retained-digest", "Verified", retainedDigestVerifiers=1)
    add("S2.byte-identical-replay", "DomainIdempotent", originalEffects=1, nonceRecords=1)
    for reason in ("changed-fields", "cross-tenant", "foreign-issuer", "public-registrar", "workflow-registrar", "target-handler-registrar"):
        add(f"S2.replay-reject.{reason}", forbiddenEffects=0)
    add("S2.replay-exact-stream-acl", "Recorded", exactReplayStreamWrites=1, otherStreamWrites=0,
        otherEventWrites=0, reservedSystemNamespace=1)
    add("S2.replay-retention-recovery", "Recorded", replayRetentionSeconds=604800, renewedFirstSeen=0, lookupOnly=1)
    add("S2.emergency-redispatch", "Verified", newKeyAndNonce=1, immutableLogicalId=1, revokedEffects=0)
    add("S3.aes256gcm", "Verified", authenticatedWrap=1, ownedPlaintextCleared=1)
    add("S3.es256-offline-anchor", "Verified", detachedEs256=1, publicAnchorRetained=1)
    for field in ("ciphertext", "tag", "tenant", "export", "purpose", "version", "jws-header", "public-anchor"):
        add(f"S3.crypto-substitution.{field}", forbiddenEffects=0)
    for action in ("wrap", "unwrap", "pin", "unpin", "destroy"):
        add(f"S3.lifecycle.{action}", "Completed", exactReceipts=1, currentLifecycleReservation=1)
    add("S3.all-copy-destroy-restore", "Erased", allCopiesDestroyed=1, allCopyRevival=0, nonrollbackJournal=1)
    add("S3.hold-pin", "Denied", destroyedTargets=0, retainedPins=1)
    add("S3.open-export-disposition", "Blocked", forbiddenEffects=0, openProductDisposition=1)
    add("S3.principal-direct-delivery", "Delivered", directPrincipalRelease=1, originalEffects=1)
    for field in ("tenant", "delivery-id", "export-id", "ack-revision", "requester", "purpose", "audience", "expiry", "lifecycle-version", "store-target", "contract-version"):
        add(f"S3.delivery-substitution.{field}", forbiddenEffects=0)
    for trigger in ("expiry", "actor-revocation"):
        for point in ("before", "at", "after"):
            outcomes = ("NotDelivered",) if point == "before" or trigger == "expiry" and point == "at" else ("NotDelivered", "Delivered") if point == "at" else ("Delivered",)
            cases.append(Case(f"S3.delivery-{trigger}.{point}", outcomes, {"immutableDeliveredAt": 1, "renewedExpiry": 0}, point))
    native_recovery_outcomes = {"lifecycle": "Destroyed", "delivery": "Delivered", "signing": "Signed", "replay": "Recorded",
        "consume": "Consumed", "compromise-block": "ConsumptionBlocked", "activation": "Unconsumed",
        "guard-issue": "Issued", "dispatch": "DeletionBatchDispatchAuthorized", "completion": "CompletionSealed"}
    for operation, outcome in native_recovery_outcomes.items():
        add(f"Recovery.{operation}.lost-result", outcome, lookupOnly=1, originalEffects=1,
            blindRetries=0, immutableOriginalRequest=1, immutableOriginalOutcome=1)
        add(f"Recovery.{operation}.lookup-outage", "Unknown", lookupOnly=1, blindRetries=0, newEffects=0)
    for reason in ("missing-approvers", "predecessor", "contract", "version", "affected-evaluations", "actor-roles", "stale", "expired", "revoked", "foreign-anchor"):
        add(f"S3.decision-reject.{reason}", forbiddenEffects=0)
    add("S3.exact-independent-decision", "Verified", exactIndependentDecision=1)
    for writer in ("interaction-create", "failure-create", "content-append", "directory-permit", "user-action-intent", "lease-acquire", "lease-commit", "rate", "open", "budget", "capacity"):
        for state in ("stale", "disconnected", "restored"):
            add(f"S4.migration.{writer}.{state}", forbiddenEffects=0, legacyEpochWrites=0)
    add("S4.migration-successor-preservation", "Verified", retainedMigrationFacts=1, oldBridgeWrites=0)
    add("S4.capability-canonical-14-fields", "Verified", canonicalFieldCount=14, detachedEs256=1,
        successfulRevisionInSignedBytes=0, capabilityExpiryFields=0)
    for field in ("issuer", "audience", "tenant", "request", "seal", "kind", "ordinal", "batch", "manifest", "guard", "intended-revision", "attestation", "signing-attempt", "key-version", "header"):
        add(f"S4.capability-substitution.{field}", forbiddenEffects=0)
    add("S4.routine-rotation-retention", "Verified", currentHealthyIssuance=1, publicAnchorRetained=1,
        capabilityExpiryFields=0)
    add("v23.two-consecutive-stale-signing-attempts", "Issued", signingAttempts=3, obsoleteUnissuedAttempts=2,
        stableSealBatchManifest=1, actualIssueRevisionSeparate=1, activeCredentials=1, blindRetries=0)
    add("v23.stale-attempt-with-rotation", "Issued", signingAttempts=3, obsoleteUnissuedAttempts=2,
        stableSealBatchManifest=1, currentHealthyIssuance=1, activeCredentials=1)
    add("v23.unknown-sign-or-issue", "Unknown", newEffects=0, blindRetries=0)
    add("v23.exact-no-issue-proof", "ObsoleteUnissued", exactNoIssueProof=1, newEffects=0)
    for field in ("issuer", "audience", "tenant", "family", "key-version", "revocation-revision", "trust-revision", "event-id", "signature-digest"):
        add(f"v23.compromise-envelope-substitution.{field}", forbiddenEffects=0)
    add("v23.compromise-private-isolation", "Denied", publicRegistrarAccess=0, workflowRegistrarAccess=0,
        targetHandlerRegistrarAccess=0, approvalRequests=0)
    add("v23.compromise-complete-enumeration", "Blocked", completeAffectedBatchEnumeration=1, globalKeyBlock=1)
    add("v23.incomplete-enumeration", "Unknown", newEffects=0)
    for trigger in ("admission-integrity-block", "key-compromise"):
        for point in ("before", "at", "after"):
            outcomes = ("Blocked",) if point == "before" else ("Blocked", "Consumed") if point == "at" else ("Consumed",)
            cases.append(Case(f"v23.{trigger}-reserve.{point}", outcomes, {"raceWinners": 1, "atomicAllOrNone": 1,
                "immutableReservation": 1, "globalKeyBlock": 1 if trigger == "key-compromise" else 0}, point))
    for point in ("before-replacement", "before-dispatch", "before-activation"):
        add(f"v23.second-compromise.{point}", "ActivationBlocked", replacementKeyEffects=0,
            globalKeyBlock=1, exactNewKeyBlockReceipt=1, activeCredentials=0)
    cases.append(Case("v23.second-compromise.at-activation", ("ActivationBlocked", "BlockedAfterActivation"),
        {"replacementKeyEffects": 0, "globalKeyBlock": 1, "exactNewKeyBlockReceipt": 1, "activeCredentials": 0}, "at"))
    add("v23.second-compromise.after-activation-before-reserve", "BlockedAfterActivation", replacementKeyEffects=0,
        globalKeyBlock=1, exactNewKeyBlockReceipt=1, activeCredentials=0)
    add("v23.same-batch-reattest", "Consumed", capabilityCompromiseBlockProof=1, stableSealBatchManifest=1,
        activeCredentials=1, nextAttestationOrdinal=1, successorDispatchProof=1, atomicAllOrNone=1)
    add("v23.admission-block-never-reopens", "Blocked", activations=0, newEffects=0)
    for field in ("tenant", "key", "attestation", "dispatch", "block-set-revision", "block-receipt"):
        add(f"v23.activation-substitution.{field}", forbiddenEffects=0)
    add("v23.no-false-replacement", "Blocked", activations=0, newEffects=0)
    add("v23.same-alias-coverage", "AlreadyDestroyed", originalEffects=1, duplicateTargetBatches=0, exactOriginalReceipt=1)
    add("v23.delayed-nonexpiring-restore", "Consumed", capabilityExpiryFields=0, originalEffects=1,
        publicAnchorRetained=1, restoredIdentityUnchanged=1)
    for boundary in ("hold-dispatch", "compromise-dispatch", "guard-mutation-completion"):
        for point in ("before", "at", "after"):
            cases.append(Case(f"v23.{boundary}.{point}", ("Blocked", "Completed"),
                {"raceWinners": 1, "exactLinearizationOrder": 1, "openProductDispositionEffects": 0}, point))
    add("Leakage.all-surfaces", "Verified", scannedEvents=1, scannedProjections=1, scannedResponses=1,
        scannedLogs=1, scannedTraces=1, scannedEvidence=1, ownedPlaintextCleared=1)
    if len({c.id for c in cases}) != len(cases):
        raise ValueError("duplicate case")
    return tuple(cases)


def wire_plan():
    return [asdict(c) for c in plan()]


def plan_digest():
    return hashlib.sha256(json.dumps(wire_plan(), sort_keys=True, separators=(",", ":")).encode()).hexdigest()
