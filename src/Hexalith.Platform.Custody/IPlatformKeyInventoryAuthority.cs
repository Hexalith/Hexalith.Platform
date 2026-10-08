namespace Hexalith.Platform.Custody;

/// <summary>Independent exact key provision/revocation authority, not custodian or recorder self-approval.</summary>
public interface IPlatformKeyInventoryAuthority
{
    /// <summary>Authenticates current private caller/credential for exact tenant/purpose/alias/version and named ApplyKeyInventory or ReadKeyInventory method.</summary>
    Task<bool> AuthorizeOperationAsync(PlatformKeyVersion scope, string operation, CancellationToken cancellationToken = default);
    /// <summary>Authenticates the independently installed inventory owner for exact tenant/purpose/alias and captured revision/state digest, including initial absence. Caller credentials alone cannot authorize a restored state.</summary>
    Task<bool> ValidateStateAsync(PlatformKeyVersion scope, long revision, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Conditionally advances the independently durable inventory anchor before persistence. Unknown or precommit store failure remains unavailable until independent reconciliation, without reopening revoked versions.</summary>
    Task<bool> RecordRevisionAsync(PlatformKeyVersion scope, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Authenticates exact tenant/purpose/version/provider/authority and current complete change evidence.</summary>
    Task<bool> AuthorizeAsync(PlatformKeyInventoryChange change, CancellationToken cancellationToken = default);
}
