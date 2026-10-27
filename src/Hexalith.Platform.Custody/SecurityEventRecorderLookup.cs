namespace Hexalith.Platform.Custody;

/// <summary>Exact independently authenticated recorder lookup.</summary>
/// <param name="State">Closed current verdict.</param>
/// <param name="Receipt">Only present for exact confirmed original source.</param>
/// <param name="NoRecordProof">Actual native prefix and independent source, plus independently retained submission/terminal-status evidence, for an unrecorded observation when absence is definitive; missing after dispatch remains unknown.</param>
public sealed record SecurityEventRecorderLookup(SecurityEventRecorderLookupState State, SecurityEventRecordReceipt? Receipt = null,SecurityEventNoRecordProof? NoRecordProof=null);
