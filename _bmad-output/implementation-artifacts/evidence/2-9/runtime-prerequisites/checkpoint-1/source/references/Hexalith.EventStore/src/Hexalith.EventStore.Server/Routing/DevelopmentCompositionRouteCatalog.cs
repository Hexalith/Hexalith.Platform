using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.Server.Routing;

/// <summary>Constructs the closed Development candidate from actual participating owner observations.</summary>
public static class DevelopmentCompositionRouteCatalog
{
    /// <summary>Joins matching declarations and independently installed targets and admission policies without defaults or fallback discovery.</summary>
    public static RouteCatalogEnvelope Build(IReadOnlyList<RouteCatalogHostObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var declarations = observations.SelectMany(o => o.Declarations).GroupBy(d => d.RouteId, StringComparer.Ordinal).Select(group =>
        {
            if (group.Select(RouteCatalogCodec.ComputeDeclarationDigest).Distinct(StringComparer.Ordinal).Count() != 1)
                throw new RouteCatalogUnavailableException("Participating owners disagree on a compiled route declaration.");
            return group.First();
        }).OrderBy(d => d.RouteId, StringComparer.Ordinal).ToArray();
        var targets = observations.SelectMany(o => o.Targets).ToArray();
        if (targets.Select(t => t.RouteId).Distinct(StringComparer.Ordinal).Count() != targets.Length
            || targets.Any(target => target.Generation != 1 || !observations.Any(o => o.AppId == target.AppId && o.Targets.Contains(target))))
            throw new RouteCatalogUnavailableException("A Development target is ambiguous or lacks its actual consuming host.");
        return DevelopmentRouteCatalogBuilder.Build(new(1, false, declarations,
            targets.Select(t => new RouteCompositionBinding(t.RouteId, t.AppId, t.Method, t.ContractVersion, t.IsRuntimeOverride, t.ResolverTenantPattern)).ToArray(),
            observations.SelectMany(o => o.AdmissionPolicies ?? throw new RouteCatalogUnavailableException("Installed admission facts are required.")).ToArray()));
    }
}
