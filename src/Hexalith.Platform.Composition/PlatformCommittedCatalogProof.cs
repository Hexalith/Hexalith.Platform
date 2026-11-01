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
        ValidateActivation(catalog, activation, canonical, root, attempt, expectedHosts, readbacks.Count);
        ValidateHostInventory(activation.RequiredHosts, expectedHosts);
        ValidateGateway(activation, expectedHosts);
        string binding = RouteCatalogActivationBinding.ComputeDigest(activation);
        if (activation.TrustProof != binding)
        {
            throw new InvalidOperationException("The retained activation binding is invalid.");
        }

        var identities = activation.RequiredHosts.OrderBy(host => host.HostId, StringComparer.Ordinal).ToArray();
        long? epoch = null;
        foreach (string host in expectedHosts.Keys)
        {
            if (!readbacks.TryGetValue(host, out var actual) || actual is null)
            {
                throw new InvalidOperationException("A participant did not serve this complete committed run identity.");
            }

            ValidateReadbackIdentity(actual, catalog, root, attempt, binding);
            ValidateReadbackHosts(actual.RequiredHosts, identities);
            epoch ??= actual.Epoch;
            if (epoch != actual.Epoch)
            {
                throw new InvalidOperationException("Catalog participants disagree on their committed epoch.");
            }
        }
    }

    private static void ValidateActivation(RouteCatalogEnvelope catalog, RouteCatalogActivation activation, byte[] canonical,
        string root, string attempt, IReadOnlyDictionary<string, string> expectedHosts, int readbackCount)
    {
        if (catalog.SchemaVersion != 3 || catalog.Profile != "Development"
            || expectedHosts.Count is < 1 or > 64 || readbackCount != expectedHosts.Count
            || activation.AttemptId != attempt || activation.RootDigest != root
            || !activation.CanonicalCatalog.AsSpan().SequenceEqual(canonical))
        {
            throw new InvalidOperationException("The complete catalog activation differs from this run or its actual receiver cohort.");
        }
    }

    private static void ValidateHostInventory(IReadOnlyList<RouteCatalogHostIdentity> hosts, IReadOnlyDictionary<string, string> expectedHosts)
    {
        if (hosts is null || hosts.Count != expectedHosts.Count || hosts.Any(host => host is null)
            || hosts.Select(host => host.HostId).Distinct(StringComparer.Ordinal).Count() != expectedHosts.Count
            || hosts.Any(host => !HostMatches(host, expectedHosts)))
        {
            throw new InvalidOperationException("The complete catalog activation differs from this run or its actual receiver cohort.");
        }
    }

    private static bool HostMatches(RouteCatalogHostIdentity host, IReadOnlyDictionary<string, string> expectedHosts) =>
        !string.IsNullOrEmpty(host.HostId) && expectedHosts.TryGetValue(host.HostId, out string? appId) && host.AppId == appId
        && Digest(host.InventoryDigest) && Digest(host.TopologyDigest) && Digest(host.AuthenticationFingerprint);

    private static void ValidateGateway(RouteCatalogActivation activation, IReadOnlyDictionary<string, string> expectedHosts)
    {
        if (!expectedHosts.TryGetValue("eventstore", out string? gatewayAppId)
            || activation.TechnicalHttpReceivers is not { Count: > 0 }
            || !activation.TechnicalHttpReceivers.Any(receiver => receiver is not null && receiver.AppId == gatewayAppId
                && receiver.FenceVersion == RouteCatalogTechnicalHttpPolicy.FenceVersion))
        {
            throw new InvalidOperationException("The complete catalog activation differs from this run or its actual receiver cohort.");
        }
    }

    private static void ValidateReadbackIdentity(RouteCatalogGenerationReadback actual, RouteCatalogEnvelope catalog, string root, string attempt, string binding)
    {
        if (!actual.Serving || actual.Epoch <= 0 || actual.Generation != catalog.Generation || actual.Profile != "Development"
            || actual.RootDigest != root || actual.RouteFacetDigest != catalog.RouteFacetDigest
            || actual.IdempotencyFacetDigest != catalog.IdempotencyFacetDigest || actual.AttemptId != attempt
            || actual.ActivationBindingDigest != binding)
        {
            throw new InvalidOperationException("A participant did not serve this complete committed run identity.");
        }
    }

    private static void ValidateReadbackHosts(IReadOnlyList<RouteCatalogHostIdentity> hosts, RouteCatalogHostIdentity[] identities)
    {
        if (hosts is null || hosts.Any(identity => identity is null)
            || !hosts.OrderBy(identity => identity.HostId, StringComparer.Ordinal).SequenceEqual(identities))
        {
            throw new InvalidOperationException("A participant did not serve this complete committed run identity.");
        }
    }

    private static bool Digest(string value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
#endif
