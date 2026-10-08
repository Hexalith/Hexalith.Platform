namespace Hexalith.Platform.Custody;

/// <summary>Exact independently authenticated recorder lookup.</summary>
/// <param name="State">Closed current verdict.</param>
/// <param name="Receipt">Only present for exact confirmed original source.</param>
public sealed record SecurityEventRecorderLookup(SecurityEventRecorderLookupState State, SecurityEventRecordReceipt? Receipt = null);
