namespace Hexalith.Platform.Composition;

/// <summary>Private delegated Tenants transport held only by the owning runner; never serialized into observations or handoff.</summary>
internal sealed class PlatformTenantBootstrapAccess(string resourceId, string appId, string privateKey, string? apiToken, string issuer)
{
    /// <summary>Gets the actual owned Tenants resource.</summary>
    internal string ResourceId { get; } = resourceId;

    /// <summary>Gets the actual Tenants sidecar identity.</summary>
    internal string AppId { get; } = appId;

    /// <summary>Gets the private bound-proof key already enrolled by the owning composition.</summary>
    internal string PrivateKey { get; } = privateKey;

    /// <summary>Gets the actual owned sidecar API token, when configured.</summary>
    internal string? ApiToken { get; } = apiToken;

    /// <summary>Gets the actual shared issuer of this run's Tenants workload.</summary>
    internal string Issuer { get; } = issuer;
}
