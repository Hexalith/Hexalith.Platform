using System.Text.Json.Nodes;

using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Inventory;

/// <summary>Readiness requires a usable current binding for every exact permitted tuple.</summary>
public sealed class InventoryReadiness(SecretsRuntimeConfiguration configuration, CurrentInventoryAuthority authority,
    JsonArray permitted, Func<bool> authenticatedTransportConfigured, TimeProvider? clock = null)
{
    /// <summary>Reads current authority; configuration presence alone cannot report Bound.</summary>
    public async Task<bool> ReadyAsync(CancellationToken cancellationToken)
    {
        try { return await CheckAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return false; }
    }

    private async Task<bool> CheckAsync(CancellationToken cancellationToken)
    {
        if (!authenticatedTransportConfigured() || configuration.ExpectedProfile is null || configuration.CallTimeout is not { } timeout
            || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30) || permitted.Count == 0
            || new[] { configuration.ProviderBinding, configuration.HmacCredentialReference, configuration.CurrentInventoryAuthorityReference,
                configuration.TenantInventoryReference, configuration.ProfileReference, configuration.PublicTrustReference,
                configuration.CurrentRevocationReference }.Any(string.IsNullOrWhiteSpace)) { return false; }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var resolvedExpiries = new List<DateTimeOffset>();
        foreach (JsonNode? node in permitted)
        {
            if (node is not JsonObject item || item.Count != 4 || item["tenantId"]?.GetValue<string>() is not { Length: > 0 } tenant
                || item["purpose"]?.GetValue<string>() is not { } purpose || !Enum.TryParse(purpose, out PlatformHmacPurpose parsed)
                || !Enum.IsDefined(parsed) || parsed.ToString() != purpose || item["secretReference"]?.GetValue<string>() is not { Length: > 0 }
                || item["secretField"]?.GetValue<string>() is not { Length: > 0 } || !seen.Add(tenant + "\n" + purpose))
            { return false; }
            DaprPlatformHmacInventory? proof = await authority.ResolveAsync(configuration.CurrentInventoryAuthorityReference!, new(tenant, parsed), null, cancellationToken).ConfigureAwait(false);
            if (proof is null) { return false; }
            resolvedExpiries.Add(proof.ValidUntil);
        }
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = (clock ?? TimeProvider.System).GetUtcNow();
        return resolvedExpiries.All(expiry => now < expiry);
    }
}
