namespace Hexalith.Platform.Custody;

/// <summary>Content-free original produced only when the delivery receiver actually rejects a changed identity against an authenticated retained native outcome.</summary>
public sealed record ExportKeyDeliveryDenialOriginal(string Version,ExportKeyDeliveryIdentity RequestedIdentity,ExportKeyDeliveryOutcome RetainedOriginal,DateTimeOffset ReceivedAt)
{
    /// <summary>Validates the closed same-address denial; no material or release authority is carried.</summary>
    public bool IsExact(ExportKeyDeliveryIdentity requested)=>requested is not null && RequestedIdentity is not null && RetainedOriginal?.Identity is not null && Version=="ExportKeyDeliveryReceivingDenial.v1" && RequestedIdentity==requested
        && RetainedOriginal.Identity.ActorId==requested.ActorId && RetainedOriginal.Identity!=requested && ReceivedAt!=default && ReceivedAt.Offset==TimeSpan.Zero
        && RetainedOriginal.State is ExportKeyDeliveryState.Delivered or ExportKeyDeliveryState.NotDelivered or ExportKeyDeliveryState.Unknown;
}
