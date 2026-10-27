namespace Hexalith.Platform.Custody;

/// <summary>Safe receiving result for raw candidates, including identities the ordinary contract rejects before address serialization.</summary>
public sealed record ExportKeyDeliveryReceivingResult(ExportKeyDeliveryRawIdentity Requested,ExportKeyDeliveryState State,
    ExportKeyDeliveryReceivingDenialOriginal? Denial=null,ExportKeyDeliveryOutcome? NativeOutcome=null);
