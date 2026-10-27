using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Canonical binding of immutable approved case recipes to the already retained unpredictable cohort attempt. No preparation reserves or predicts an attempt.</summary>
public static class JointPhaseRequestRecipe
{
    internal static int? Phase(JsonObject? request)
    {
        if(request?["phase"] is not JsonValue value) return null;
        if(value.TryGetValue<int>(out int small)) return small;
        return value.TryGetValue<long>(out long number) && number is >=int.MinValue and <=int.MaxValue?(int)number:null;
    }
    /// <summary>Derives the complete exact request after the authority returned its retained attempt. Legacy precomputed attempts are refused.</summary>
    public static JsonObject? Bind(JsonObject recipe,JsonObject phase,JointPhaseCommandScope accepted)
    {
        if(recipe.ContainsKey("runId") || recipe.ContainsKey("requestId") || phase["attemptId"]?.GetValue<string>() is not { Length:>0 } attempt
            || phase["bindingDigest"]?.GetValue<string>()!=accepted.BindingDigest || JointPhaseRequestRecipe.Phase(phase)!=accepted.Phase
            || recipe["targetCommit"]?.GetValue<string>()!=accepted.PlatformCommit) return null;
        var request=(JsonObject)recipe.DeepClone();request["runId"]=attempt;request["requestId"]=QualificationProtocolSchema.Digest(request);return request;
    }
}
