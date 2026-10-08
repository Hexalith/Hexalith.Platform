namespace Hexalith.Platform.Custody;

/// <summary>Independent exact key provision/revocation authority, not custodian or recorder self-approval.</summary>
public interface IPlatformKeyInventoryAuthority
{
    /// <summary>Authenticates current private caller/credential for exact tenant/purpose/alias/version and named ApplyKeyInventory or ReadKeyInventory method.</summary>
    Task<bool> AuthorizeOperationAsync(PlatformKeyVersion scope, string operation, CancellationToken cancellationToken = default);
    /// <summary>Authenticates exact tenant/purpose/version/provider/authority and current complete change evidence.</summary>
    Task<bool> AuthorizeAsync(PlatformKeyInventoryChange change, CancellationToken cancellationToken = default);
}
