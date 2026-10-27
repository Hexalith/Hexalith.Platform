namespace Hexalith.Platform.Custody;

/// <summary>Immutable record of validation actually executed by the original-target delivery receiver. It is separate from native actor state and all legacy denial bodies.</summary>
public sealed record ExportKeyDeliveryReceivingDenialOriginal(string Version,ExportKeyDeliveryIdentity Target,ExportKeyDeliveryRawIdentity Requested,
    ExportKeyDeliveryOutcome RetainedOriginal,DateTimeOffset ReceivedAt,string RejectionStage)
{
    /// <summary>Correlates an existing executed rejection original; current validation alone cannot construct this evidence.</summary>
    public bool IsExact(ExportKeyDeliveryReceivingRequest request)=>request is not null && Target is not null && Requested is not null && RetainedOriginal is not null && Version=="ExportKeyDeliveryReceivingDenial.v2" && Target==request.Target && Requested==request.Supplied
        && RetainedOriginal?.Identity==Target && ReceivedAt!=default && ReceivedAt.Offset==TimeSpan.Zero
        && RetainedOriginal.State is ExportKeyDeliveryState.Delivered or ExportKeyDeliveryState.NotDelivered or ExportKeyDeliveryState.Unknown
        && RejectionStage is "DeliverInputValidation" or "RetainedIdentityConflict";
}
