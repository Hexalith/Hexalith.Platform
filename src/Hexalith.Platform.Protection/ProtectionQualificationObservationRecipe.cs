using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Protection;

/// <summary>Independent read-only native references and field projections; executable scenario expectations are not accepted.</summary>
/// <param name="TenantId">Actual fixture tenant.</param><param name="Sources">Native source/guard/owner/projection/signing/copy locations.</param>
/// <param name="Fields">Only projections from those actual sources.</param>
public sealed record ProtectionQualificationObservationRecipe(string TenantId, Dictionary<string, ProtectionQualificationNativeCell> Sources,
    Dictionary<string, ProtectionQualificationObservationField> Fields)
{
    /// <summary>Exact source-location templates bound to admitted fixture identities, never observation values. Source and template ids must be disjoint.</summary>
    public Dictionary<string, JsonObject> SourceTemplates { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Resolves native cell references for this exact attempt without accepting executor summaries or expected fields.</summary>
    public ProtectionQualificationObservationRecipe Bind(JsonObject request)
    {
        var sources = new Dictionary<string, ProtectionQualificationNativeCell>(Sources, StringComparer.Ordinal);
        foreach (var entry in SourceTemplates)
        {
            var bound = ProtectionQualificationRecipeBinding.Bind(new(entry.Key, ProtectionQualificationNativeCallKind.Lookup, entry.Value), request,
                new Dictionary<string, JsonNode?>());
            sources.Add(entry.Key, bound.Payload.Deserialize<ProtectionQualificationNativeCell>(ErasureRootCustody.Json)
                ?? throw new InvalidOperationException("Complete native observation source required."));
        }
        return this with { Sources = sources, SourceTemplates = new(StringComparer.Ordinal) };
    }
}
