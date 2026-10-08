namespace Hexalith.Platform.Custody;

/// <summary>One complete conditional private system spool namespace; observations/acknowledgements share one atomic CAS owner.</summary>
/// <param name="InstallationEpoch">Independently qualified restore epoch.</param>
/// <param name="Revision">Current monotonic conditional state.</param>
/// <param name="Records">Gap-free complete immutable observation inventory including acknowledged originals.</param>
public sealed record SecuritySpoolSnapshot(string InstallationEpoch, long Revision, IReadOnlyList<SecurityObservationRecord> Records);
