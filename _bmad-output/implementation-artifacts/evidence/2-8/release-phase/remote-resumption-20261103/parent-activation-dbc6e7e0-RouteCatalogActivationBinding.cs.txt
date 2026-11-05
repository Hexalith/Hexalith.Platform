using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Separate lifecycle v1 binding; envelope v1/v2 canonicalization remains exclusively Contracts-owned.</summary>
public static class RouteCatalogActivationBinding
{
    /// <summary>Hashes all lifecycle inputs, including the exact predecessor and sorted required host identities.</summary>
    public static string ComputeDigest(RouteCatalogActivation activation)
    {
        ArgumentNullException.ThrowIfNull(activation);
        RouteCatalogEnvelope envelope = RouteCatalogCodec.Decode(activation.CanonicalCatalog, activation.RootDigest);
        if (envelope.SchemaVersion is not (2 or 3) || string.IsNullOrWhiteSpace(activation.AttemptId)
            || string.IsNullOrWhiteSpace(activation.Owner) || activation.RequiredHosts is not { Count: > 0 and <= 256 }
            || activation.RequiredHosts.Any(h => h is null || string.IsNullOrWhiteSpace(h.HostId)
                || string.IsNullOrWhiteSpace(h.AppId) || !Digest(h.InventoryDigest) || !Digest(h.TopologyDigest)
                || string.IsNullOrWhiteSpace(h.AuthenticationFingerprint) || h.ExecutableDigest is not null && !Digest(h.ExecutableDigest))
            || activation.RequiredHosts.Select(h => h.HostId).Distinct(StringComparer.Ordinal).Count() != activation.RequiredHosts.Count
            || activation.RequiredHosts.GroupBy(h => h.AppId, StringComparer.Ordinal).Any(group => !RouteCatalogReplicaPolicy.Equivalent(group))
            || (activation.PredecessorRootDigest is not null && !Digest(activation.PredecessorRootDigest))
            || (activation.AuxiliaryOperations is { } auxiliary && (auxiliary.Any(a => a is null
                || !activation.RequiredHosts.Any(h => h.AppId == a.AppId) || string.IsNullOrWhiteSpace(a.Method)
                || string.IsNullOrWhiteSpace(a.Operation) || string.IsNullOrWhiteSpace(a.Domain)
                || new object?[] { a.NativeActor, a.Http, a.DomainAuxiliary, a.Sidecar, a.DaprCallback }.Count(owner => owner is not null) > 1
                || a.DomainAuxiliary is { } domain && (envelope.SchemaVersion != 3 || !Digest(domain.CodeDigest)
                    || string.IsNullOrWhiteSpace(domain.Owner) || string.IsNullOrWhiteSpace(domain.RequestType)
                    || string.IsNullOrWhiteSpace(domain.ScopeMethod) || a.Method.IndexOfAny(['*', '{', '}']) >= 0)
                || a.Sidecar is { } sidecar && (envelope.SchemaVersion != 3 || !Digest(sidecar.CodeDigest)
                    || string.IsNullOrWhiteSpace(sidecar.Owner) || string.IsNullOrWhiteSpace(sidecar.PubSubName)
                    || string.IsNullOrWhiteSpace(sidecar.TopicName) || a.Operation != "sdk-sidecar:domain-events"
                    || a.Method.IndexOfAny(['*', '{', '}']) >= 0)
                || a.DaprCallback is { } callback && (envelope.SchemaVersion != 3 || a.Http is not null || a.NativeActor is not null
                    || callback.Kind is not ("subscribe" or "projection-notification")
                    || a.Method != (callback.Kind == "subscribe" ? "dapr/subscribe" : "projections/changed")
                    || a.Operation != "sdk-dapr-callback:" + callback.Kind
                    || callback.Verbs != (callback.Kind == "subscribe" ? "GET" : "POST")
                    || !Digest(callback.CodeDigest) || !Digest(callback.FrameworkDigest) || !Digest(callback.InventoryDigest))
                || a.NativeActor is { } native && (envelope.SchemaVersion != 3 || a.Http is not null
                    || native.Kind is not ("config" or "health" or "method" or "reminder" or "timer" or "deactivate")
                    || !Digest(native.CodeDigest) || !Digest(native.FrameworkDigest) || !Digest(native.InventoryDigest)
                    || native.Verbs != (native.Kind is "config" or "health" ? "GET" : native.Kind == "deactivate" ? "DELETE" : "PUT"))
                || a.Http is { } http && (envelope.SchemaVersion != 3 || !Digest(http.CodeDigest) || string.IsNullOrWhiteSpace(http.Owner)
                    || http.Grants is not { Count: > 0 } grants || grants.Any(grant => string.IsNullOrWhiteSpace(grant.Method)
                        || string.IsNullOrWhiteSpace(grant.Verbs) || grant.Verbs.Split(' ').Any(verb => !http.Verbs.Split(' ').Contains(verb, StringComparer.Ordinal)))
                    || http.Verbs.Split(' ') is not { Length: > 0 } verbs
                    || verbs.Any(verb => verb is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS"))
                    || !verbs.Order(StringComparer.Ordinal).SequenceEqual(verbs, StringComparer.Ordinal)
                    || verbs.Distinct(StringComparer.Ordinal).Count() != verbs.Length))
                || auxiliary.Select(operation => JsonSerializer.Serialize(operation)).Distinct(StringComparer.Ordinal).Count() != auxiliary.Count))
            || envelope.Routes.Any(r => !activation.RequiredHosts.Any(h => h.AppId == r.AppId))
            || envelope.Idempotency.Any(i => !activation.RequiredHosts.Any(h => h.AppId == i.ConsumerAppId)))
        {
            throw new RouteCatalogUnavailableException("Incomplete or ambiguous activation identity or required host set.");
        }
        TechnicalHttpCatalogBinding.Validate(activation);
        if (activation.TechnicalHttpReceivers is { Count: > 0 } && envelope.SchemaVersion != 3)
            throw new RouteCatalogUnavailableException("Technical activation v2 requires the complete schema 3 owner catalog.", RouteCatalogRefusalReason.Inventory);
        var capabilities = activation.ProjectionCapabilities ?? [];
        if (capabilities.Any(c => c is null || string.IsNullOrWhiteSpace(c.Fingerprint) || c.ProjectionTypes is not { Count: > 0 }
            || c.ProjectionTypes.Distinct(StringComparer.Ordinal).Count() != c.ProjectionTypes.Count
            || !activation.RequiredHosts.Any(h => h.AppId == c.AppId)
            || c.ProjectionTypes.Any(type => !envelope.Declarations.Any(d => d.Kind == "projection" && d.Domain == c.Domain && d.ProjectionType == type
                && envelope.Routes.Any(r => r.RouteId == d.RouteId && r.AppId == c.AppId && r.ContractVersion == c.ContractVersion && r.Method == "project/v2"))))
            || capabilities.Select(c => (c.AppId, c.Domain, c.ContractVersion)).Distinct().Count() != capabilities.Count)
            throw new RouteCatalogUnavailableException("Named projection capabilities disagree with declared executable targets.");
        byte[] binding = JsonSerializer.SerializeToUtf8Bytes(new
        {
            purpose = "route-catalog-activation", version = 1,
            activation.AttemptId, activation.RootDigest, activation.PredecessorRootDigest,
            envelope.Generation, envelope.Profile, envelope.RouteFacetDigest, envelope.IdempotencyFacetDigest,
            activation.Owner, hosts = activation.RequiredHosts.OrderBy(h => h.HostId, StringComparer.Ordinal).ToArray(),
            auxiliary = (activation.AuxiliaryOperations ?? []).OrderBy(a => a.AppId, StringComparer.Ordinal)
                .ThenBy(a => a.Domain, StringComparer.Ordinal).ThenBy(a => a.Method, StringComparer.Ordinal).ThenBy(a => a.Operation, StringComparer.Ordinal).ToArray(),
            capabilities = capabilities.OrderBy(c => c.AppId, StringComparer.Ordinal).ThenBy(c => c.Domain, StringComparer.Ordinal)
                .ThenBy(c => c.ContractVersion, StringComparer.Ordinal).Select(c => c with { ProjectionTypes = c.ProjectionTypes.Order(StringComparer.Ordinal).ToArray() }).ToArray(),
        });
        if (activation.TechnicalHttpReceivers is { Count: > 0 } technical)
        {
            // Keep the exact legacy v1 preimage above when technical membership is absent.
            var versioned = System.Text.Json.Nodes.JsonNode.Parse(binding)!.AsObject();
            versioned["version"] = 2;
            versioned["technicalHttpReceivers"] = JsonSerializer.SerializeToNode(technical.OrderBy(receiver => receiver.AppId, StringComparer.Ordinal)
                .Select(receiver => receiver with
                {
                    Endpoints = receiver.Endpoints.OrderBy(TechnicalHttpCatalogBinding.Key, StringComparer.Ordinal).ToArray(),
                    Invocations = receiver.Invocations?.OrderBy(TechnicalHttpCatalogBinding.InvocationKey, StringComparer.Ordinal)
                        .Select(invocation => invocation with { HttpMethods = invocation.HttpMethods.Order(StringComparer.Ordinal).ToArray() }).ToArray(),
                }).ToArray());
            binding = System.Text.Encoding.UTF8.GetBytes(versioned.ToJsonString());
        }
        return Convert.ToHexStringLower(SHA256.HashData(binding));
    }

    private static bool Digest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
