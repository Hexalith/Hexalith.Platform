namespace Hexalith.Platform.Custody;

/// <summary>Qualified exact idempotent physical all-copy key/hold/backup/restore owner. Missing backend is unavailable, never absence/destruction.</summary>
public interface ICustodyKeyLifecycleProvider
{
    /// <summary>Consumes a current linearizable fence/store reservation in the physical operation itself; rejects stale control, active/unknown holds and unqualified restore guarantees.
    /// Root destruction must consume the exact already-reserved original protection operation; this API cannot bypass all-or-none manifest ownership.</summary>
    Task<CustodyKeyLifecycleOutcome> ExecuteAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest original, string requestDigest, CancellationToken cancellationToken = default);
    /// <summary>Returns authenticated original exact outcome or Unknown; lookup never starts a new physical operation.</summary>
    Task<CustodyKeyLifecycleOutcome> LookupAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest original, string requestDigest, CancellationToken cancellationToken = default);
}
