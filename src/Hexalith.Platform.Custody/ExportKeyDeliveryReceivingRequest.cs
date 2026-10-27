namespace Hexalith.Platform.Custody;

/// <summary>Original private receiver target and bounded received fields. The supplied candidate grants no target or release authority.</summary>
public sealed record ExportKeyDeliveryReceivingRequest(ExportKeyDeliveryIdentity Target,ExportKeyDeliveryRawIdentity Supplied)
{
    internal void ValidateBounds() { ArgumentNullException.ThrowIfNull(Target);ArgumentNullException.ThrowIfNull(Supplied);Target.Validate();Supplied.ValidateBounds(); }
}
