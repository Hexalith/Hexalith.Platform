using System.Text.Json;

namespace Hexalith.Platform.Protection;

/// <summary>Immutable same-ERC radix node; only an authoritative tenant CAS root grants reachability.</summary>
internal sealed record ErasureRootOccurrenceIndexNode(string TenantId, string StoreId, string EnrollmentId, string Prefix,
    SortedDictionary<string, string> Children, string? Identity = null, JsonElement? Original = null);
