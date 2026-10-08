namespace Hexalith.Platform.Custody;

/// <summary>Content-free exact direct delivery outcome; it never carries key material.</summary>
/// <param name="Identity">The complete authenticated original delivery identity.</param>
/// <param name="State">Closed safe state.</param>
/// <param name="ObservedAt">Original successful release time, immutable after Delivered.</param>
/// <param name="Reason">Safe reason code only.</param>
public sealed record ExportKeyDeliveryOutcome(ExportKeyDeliveryIdentity Identity, ExportKeyDeliveryState State,
    DateTimeOffset? ObservedAt = null, string? Reason = null);
