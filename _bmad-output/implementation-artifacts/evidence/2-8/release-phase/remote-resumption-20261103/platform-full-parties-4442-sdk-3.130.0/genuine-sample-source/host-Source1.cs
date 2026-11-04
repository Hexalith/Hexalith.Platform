using Hexalith.EventStore.DomainService;

[assembly: RouteCatalogAdapterSource(typeof(Hexalith.EventStore.Sample.Counter.SampleCatalogAdapters))]

namespace Hexalith.EventStore.Sample.Counter;

/// <summary>The existing sample intent inventory exposed to an explicitly owned catalog composition.</summary>
public static class SampleCatalogAdapters
{
    /// <summary>Creates the unchanged owner-approved target-only intent adapters.</summary>
    public static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> Create() => SampleCatalog.CreateAdapters();
}
