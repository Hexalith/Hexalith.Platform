using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.Platform.Protection;

/// <summary>Content-addressed exact source-original radix node. Only the independently persisted CAS root makes a leaf reachable.</summary>
internal sealed record ErasureRootSourceIndexNode(string TenantId, string StoreId, string EnrollmentId, string Prefix,
    SortedDictionary<string, string> Children, string? Identity = null, JsonElement? Original = null, string[]? OriginalChunks = null, int OriginalLength = 0, string? OriginalSha256 = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OriginalChunkFormat = null);
