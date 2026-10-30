using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.Platform.Protection;

/// <summary>Uses the actual actor identity parser and rejects normalization of an installed tenant.</summary>
internal static class ErasureRootTenantIdentity
{
    internal static void Validate(string tenant)
    {
        var identity = new AggregateIdentity(tenant, "protection", "enrollment");
        if (identity.TenantId != tenant) { throw new ArgumentException("Canonical tenant enrollment required."); }
    }
}
