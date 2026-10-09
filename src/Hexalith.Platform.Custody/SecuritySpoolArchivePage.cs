namespace Hexalith.Platform.Custody;

/// <summary>One immutable acknowledged spool carrier within the finite page envelope linked from the independently anchored current head.</summary>
/// <param name="PageIndex">Original bounded carrier number.</param>
/// <param name="Snapshot">Exact final acknowledged carrier.</param>
/// <param name="PreviousDigest">Previous archive link, or null for the first carrier.</param>
public sealed record SecuritySpoolArchivePage(long PageIndex, SecuritySpoolSnapshot Snapshot, string? PreviousDigest);
