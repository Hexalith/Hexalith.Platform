using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Inventory;

/// <summary>Independently current public profile, key revocation and installed inventory-host enrollment. No local configuration can implement this proof.</summary>
public interface IHmacCurrentTrust
{
    /// <summary>Reads actual current authority. Null scope/version requests profile/host trust only.</summary>
    Task<CurrentHmacTrust?> ReadAsync(PlatformHmacScope? scope, string? version, CancellationToken cancellationToken);
}
