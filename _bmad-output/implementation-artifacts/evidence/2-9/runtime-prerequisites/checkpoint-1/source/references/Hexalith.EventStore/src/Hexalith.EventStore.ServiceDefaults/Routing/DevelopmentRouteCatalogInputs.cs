using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;

using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Reads the actual retained inputs selected by an explicit Development runner, excluding credential material.</summary>
public static class DevelopmentRouteCatalogInputs
{
    /// <summary>Captures the loaded authority component and receiver ACL, exact participant inventory and authentication settings.</summary>
    public static async Task<RouteCatalogHostObservation> CaptureAsync(IConfiguration configuration,
        string actualAppId, IReadOnlyList<RouteContractDeclaration> declarations, IReadOnlyList<RouteCatalogEntry> targets,
        IReadOnlyList<IdempotencyCatalogEntry> admission, IReadOnlyList<RouteCatalogProjectionCapability> capabilities,
        IReadOnlyList<RouteCatalogAuxiliaryOperation> auxiliary, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(declarations);
        var section = configuration.GetSection("EventStore:RouteCatalog:DevelopmentBootstrap");
        string host = section["HostId"] ?? throw new InvalidOperationException("The Development host identity is required.");
        var component = await File.ReadAllBytesAsync(section["ComponentPath"] ?? throw new InvalidOperationException("The loaded authority component is required."), token).ConfigureAwait(false);
        var acl = await File.ReadAllBytesAsync(section["AccessControlPath"] ?? throw new InvalidOperationException("The loaded receiver ACL is required."), token).ConfigureAwait(false);
        // These are observations of effective settings, never configuration layers or credential fingerprints.
        // In particular no signing key, bearer token, private/public key bytes or secret-map values are serialized.
        var settings = configuration.AsEnumerable().Where(entry => entry.Value is not null && NonsecretAuthenticationSetting(entry.Key))
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        var configuredKeys = configuration.GetSection("Authentication:BoundStreamRead:PublicKeys").GetChildren().Select(child => child.Key).Order(StringComparer.Ordinal).ToArray();
        return new(host, actualAppId, declarations, targets,
            section.GetSection("RequiredHostIds").Get<string[]>() ?? [], [component, acl],
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                settings,
                boundProofCallers = configuredKeys,
                applicationChannelTokenConfigured = !string.IsNullOrEmpty(configuration["APP_API_TOKEN"]),
                sidecarApiTokenConfigured = !string.IsNullOrEmpty(configuration["DAPR_API_TOKEN"]),
            }), admission, capabilities, auxiliary, Operations: declarations.Count == 0 ? [] : null);
    }

    private static bool NonsecretAuthenticationSetting(string key)
    {
        if (!key.StartsWith("Authentication:", StringComparison.OrdinalIgnoreCase)
            && !key.StartsWith("EventStore:Authentication:", StringComparison.OrdinalIgnoreCase)) return false;
        string[] segments = key.Split(':');
        if (segments.Any(segment => segment.Contains("Key", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("Token", StringComparison.OrdinalIgnoreCase) || segment.Contains("Secret", StringComparison.OrdinalIgnoreCase))) return false;
        string[] names = ["Issuer", "Audience", "Algorithm", "Algorithms", "AllowedDomains", "AllowedOperations", "AllowedCallers",
            "ValidateIssuer", "ValidateAudience", "ValidateLifetime", "RequireHttpsMetadata", "Authority"];
        return segments.Any(segment => names.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }
}
