using Hexalith.EventStore.Contracts.Security;
namespace Hexalith.Platform.Custody;

/// <summary>Independent exact key provision/revocation authority, not custodian or recorder self-approval.</summary>
public interface IPlatformKeyInventoryAuthority : IAnchoredStateTransitionAuthority
{
    /// <summary>Authenticates current private caller/credential for exact tenant/purpose/alias/version and named ApplyKeyInventory or ReadKeyInventory method.</summary>
    Task<bool> AuthorizeOperationAsync(PlatformKeyVersion scope, string operation, CancellationToken cancellationToken = default);
    /// <summary>Authenticates the independently installed inventory owner for exact tenant/purpose/alias and captured revision/state digest, including initial absence. Caller credentials alone cannot authorize a restored state.</summary>
    Task<bool> ValidateStateAsync(PlatformKeyVersion scope, long revision, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Deprecated compatibility-only legacy anchor hook; current recoverable actors do not invoke it.
    /// Qualified implementations must implement the mandatory inherited IAnchoredStateTransitionAuthority admitted-original admission/recovery
    /// and conditional exact transition journal, including independent staging ownership, current permission and final durable-state/anchor confirmation.
    /// Implementing this legacy hook alone never enables an actor; omitted inherited proof defaults deny.</summary>
    Task<bool> RecordRevisionAsync(PlatformKeyVersion scope, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Authenticates exact tenant/purpose/version/provider/authority and current complete change evidence.</summary>
    Task<bool> AuthorizeAsync(PlatformKeyInventoryChange change, CancellationToken cancellationToken = default);
}
