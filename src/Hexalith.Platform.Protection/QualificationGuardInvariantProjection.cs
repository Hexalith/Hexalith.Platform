using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Derives observable invariants from actual typed current owner state and retained receipts. An unavailable/history-incomplete port never yields a complete projection.</summary>
public static class QualificationGuardInvariantProjection
{
    /// <summary>Projects only measured facts. Metrics needing absent full owner history, physical census or boundary instrumentation remain explicit missing protocols.</summary>
    public static QualificationInvariantProjection Project(string tenant,string requestId,string batchId,TenantGovernanceGuardState guard,
        DeletionConsumptionOutcome protection,IReadOnlyList<DeletionCapabilitySigningOutcome> signing,IReadOnlyCollection<string> requestedMetrics)
    {
        var metrics=new Dictionary<string,long>(StringComparer.Ordinal); var missing=new List<string>();
        var deletion=guard.Deletions.SingleOrDefault(value=>value.RequestId==requestId);
        var batch=deletion?.Batches.SingleOrDefault(value=>value.BatchId==batchId);
        if(guard.TenantId!=tenant || protection.TenantId!=tenant || protection.BatchId!=batchId || deletion is null || batch is null)
            return new(metrics,["ExactCurrentTypedGuardProtectionSource"]);
        bool known=protection.Status!=DeletionConsumptionStatus.Unavailable;
        foreach(string metric in requestedMetrics)
        {
            long? measured=metric switch {
                "destroyedTargets" when known => protection.TargetReceipts.Count,
                "activeCredentials" when known => batch.Capability is null || guard.CompromisedKeyVersions.Contains(batch.CapabilityKeyVersion,StringComparer.Ordinal) ? 0:1,
                "signingAttempts" => signing.Select(value=>value.SigningRequestId).Distinct(StringComparer.Ordinal).LongCount(),
                "obsoleteUnissuedAttempts" => signing.LongCount(value=>value.State==DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued),
                "stableSealBatchManifest" when signing.Count>0 => signing.All(value=>value.Payload.TenantId==tenant && value.Payload.BatchId==batchId
                    && value.Payload.DestructionSealId==deletion.SealId && value.Payload.ManifestDigest==batch.ManifestDigest) ? 1:0,
                "capabilityExpiryFields" when batch.Capability is not null => typeof(DeletionBatchCapabilityV1).GetProperties().LongCount(property=>property.Name.Contains("Expir",StringComparison.Ordinal)),
                "canonicalFieldCount" when batch.Capability is not null => FieldCount(batch.Capability),
                "actualIssueRevisionSeparate" when batch.Capability is not null => batch.IssuedGuardRevision>0 && typeof(DeletionBatchCapabilityV1).GetProperty("CommittedIssuedGuardRevision") is null ? 1:0,
                "atomicAllOrNone" when known => protection.TargetReceipts.Count==0 || protection.TargetReceipts.Count==batch.Targets.Count
                    && protection.TargetReceipts.Select(value=>value.Target).Distinct().Count()==batch.Targets.Count
                    && protection.TargetReceipts.All(receipt=>batch.Targets.Any(target=>target.TenantId==receipt.Target.TenantId
                        && target.AgentInteractionId==receipt.Target.AgentInteractionId && target.TargetProtectionKeyAlias==receipt.Target.TargetProtectionKeyAlias)) ? 1:0,
                "globalKeyBlock" when known => protection.BlockedKeyVersion is { } version && guard.CompromisedKeyVersions.Contains(version,StringComparer.Ordinal)
                    && protection.RevocationRevision>0 ? 1:protection.BlockedKeyVersion is null ? 0:null,

                // The published current-state port carries neither competing operation originals nor the original reservation identity/history.
                "raceWinners" or "immutableReservation" => null,
                "exactNewKeyBlockReceipt" when known => guard.Revocations.LongCount(value=>value.Envelope.KeyVersion==protection.BlockedKeyVersion && value.AffectedBatchIds.Contains(batchId,StringComparer.Ordinal) && value.Envelope.RevocationRevision==protection.RevocationRevision),
                // Current batches and revocation receipts do not expose a complete affected-set snapshot at the recorded revocation revision.
                "completeAffectedBatchEnumeration" => null,
                "capabilityCompromiseBlockProof" => guard.Revocations.Any(value=>value.AffectedBatchIds.Contains(batchId,StringComparer.Ordinal) && value.Envelope.KeyVersion!=batch.CapabilityKeyVersion) ? 1:0,
                "successorDispatchProof" => batch.DispatchReceiptId.Length>0 && batch.DispatchGuardRevision>batch.IssuedGuardRevision && batch.IssuedGuardRevision>0 && guard.Receipts.Any(value=>value.ReceiptId==batch.DispatchReceiptId) ? 1:0,
                "nextAttestationOrdinal" => NextAttestationOrdinal(guard,batch),
                "exactNoIssueProof" when signing.Count>0 => signing.Count(value=>value.NoIssueProof is not null),
                _=>null };
            if(measured is null)
            {
                missing.Add("IndependentMeasuredOwnerHistoryOrBoundaryProtocol."+metric);
                if(metric is "raceWinners" or "immutableReservation") missing.Add("External.ExactCompetingProtectionOperationOriginalAndReservationHistoryProtocol");
                if(metric=="completeAffectedBatchEnumeration") missing.Add("External.CompleteAffectedBatchSetAtRecordedRevocationRevisionProtocol");
            }
            else metrics[metric]=measured.Value;
        }
        return new(metrics,missing);
    }
    private static int FieldCount(DeletionBatchCapabilityV1 payload)
    {
        using var document=System.Text.Json.JsonDocument.Parse(DeletionBatchCapabilityIdentity.CanonicalPayload(payload));
        return document.RootElement.EnumerateObject().Count();
    }
    private static long? NextAttestationOrdinal(TenantGovernanceGuardState guard,GovernanceBatchState batch)
    {
        if(batch.Capability is not { } current || batch.IssuedGuardRevision<1
            || !guard.Receipts.Any(value=>value.Status=="Committed" && value.ReceiptId==batch.IssueReceiptId
                && value.Capability==current && value.GuardHighWater==batch.IssuedGuardRevision && value.SigningRequestId==batch.SigningRequestId)) return null;
        var previous=guard.Receipts.Where(value=>value.Status=="Committed" && value.GuardHighWater<batch.IssuedGuardRevision)
            .Select(value=>value.Capability).OfType<DeletionBatchCapabilityV1>().Where(value=>value!=current
                && value.TenantId==current.TenantId && value.DeletionRequestId==current.DeletionRequestId
                && value.DestructionSealId==current.DestructionSealId && value.BatchId==current.BatchId
                && value.BatchKind==current.BatchKind && value.BatchOrdinal==current.BatchOrdinal && value.ManifestDigest==current.ManifestDigest)
            .OrderByDescending(value=>value.AttestationOrdinal).FirstOrDefault();
        return previous is null?null:current.AttestationOrdinal==previous.AttestationOrdinal+1?1:0;
    }
}
