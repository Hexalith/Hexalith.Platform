#if HEXALITH_PLATFORM_V2_CATALOG
namespace Hexalith.Platform.Composition;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Checks complete public activation and readback evidence against the independently resolved run and receiver cohort.</summary>
internal static class PlatformCommittedCatalogProof
{
    /// <summary>Requires every receiver to agree on the exact attempt, binding, epoch and complete observed host identities.</summary>
    internal static void Validate(RouteCatalogActivation activation, byte[] canonical, string root, string attempt,
        IReadOnlyDictionary<string, string> expectedHosts, IReadOnlyDictionary<string, RouteCatalogGenerationReadback> readbacks)
    {
        var catalog = RouteCatalogCodec.Decode(canonical, root);
        if (catalog.SchemaVersion != 3 || catalog.Profile != "Development"
            || expectedHosts.Count is < 1 or > 64 || readbacks.Count != expectedHosts.Count
            || activation.AttemptId != attempt || activation.RootDigest != root
            || !activation.CanonicalCatalog.AsSpan().SequenceEqual(canonical)
            || activation.RequiredHosts is null || activation.RequiredHosts.Count != expectedHosts.Count
            || activation.RequiredHosts.Any(host => host is null)
            || activation.RequiredHosts.Select(host => host.HostId).Distinct(StringComparer.Ordinal).Count() != expectedHosts.Count
            || activation.RequiredHosts.Any(host => string.IsNullOrEmpty(host.HostId) || !expectedHosts.TryGetValue(host.HostId, out string? appId) || host.AppId != appId
                || !Digest(host.InventoryDigest) || !Digest(host.TopologyDigest) || !Digest(host.AuthenticationFingerprint))
            || !expectedHosts.TryGetValue("eventstore", out string? gatewayAppId)
            || activation.TechnicalHttpReceivers is not { Count: > 0 }
            || !activation.TechnicalHttpReceivers.Any(receiver => receiver is not null && receiver.AppId == gatewayAppId
                && receiver.FenceVersion == RouteCatalogTechnicalHttpPolicy.FenceVersion))
            throw new InvalidOperationException("The complete catalog activation differs from this run or its actual receiver cohort.");
        string binding = RouteCatalogActivationBinding.ComputeDigest(activation);
        if (activation.TrustProof != binding) throw new InvalidOperationException("The retained activation binding is invalid.");
        var identities = activation.RequiredHosts.OrderBy(host => host.HostId, StringComparer.Ordinal).ToArray();
        long? epoch = null;
        foreach (string host in expectedHosts.Keys)
        {
            if (!readbacks.TryGetValue(host, out var actual) || actual is null || !actual.Serving || actual.Epoch <= 0
                || actual.Generation != catalog.Generation || actual.Profile != "Development" || actual.RootDigest != root
                || actual.RouteFacetDigest != catalog.RouteFacetDigest || actual.IdempotencyFacetDigest != catalog.IdempotencyFacetDigest
                || actual.AttemptId != attempt || actual.ActivationBindingDigest != binding || actual.RequiredHosts is null || actual.RequiredHosts.Any(identity => identity is null)
                || !actual.RequiredHosts.OrderBy(identity => identity.HostId, StringComparer.Ordinal).SequenceEqual(identities))
                throw new InvalidOperationException("A participant did not serve this complete committed run identity.");
            epoch ??= actual.Epoch;
            if (epoch != actual.Epoch) throw new InvalidOperationException("Catalog participants disagree on their committed epoch.");
        }
    }

    private static bool Digest(string value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
#endif
