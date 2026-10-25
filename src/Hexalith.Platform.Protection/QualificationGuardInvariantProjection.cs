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
                    && protection.RevocationRevision>0 ? 1:null,
                
                "exactNoIssueProof" when signing.Count>0 => signing.Count(value=>value.NoIssueProof is not null),
                _=>null };
            if(measured is null) missing.Add("IndependentMeasuredOwnerHistoryOrBoundaryProtocol."+metric);
            else metrics[metric]=measured.Value;
        }
        return new(metrics,missing);
    }
    private static int FieldCount(DeletionBatchCapabilityV1 payload)
    {
        using var document=System.Text.Json.JsonDocument.Parse(DeletionBatchCapabilityIdentity.CanonicalPayload(payload));
        return document.RootElement.EnumerateObject().Count();
    }
}
