using System.Text.Json.Nodes;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Binds closed native API templates to the exact admitted request and earlier native originals. It has no verdict or observation-value source.</summary>
public static class ProtectionQualificationRecipeBinding
{
    /// <summary>Resolves whole-node references only; normal strings are never evaluated or substituted as code.</summary>
    public static ProtectionQualificationNativeCall Bind(ProtectionQualificationNativeCall template, JsonObject request,
        IReadOnlyDictionary<string, JsonNode?> originals)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(originals);
        JsonNode? Resolve(JsonNode? value)
        {
            if (value is JsonObject reference && reference.Count == 1)
            {
                if (reference["$request"] is JsonValue path)
                {
                    string pointer = path.GetValue<string>();
                    if (pointer == "/canaryBase64" || pointer.StartsWith("/canaryBase64/", StringComparison.Ordinal))
                    { throw new InvalidOperationException("Synthetic plaintext belongs only to the engine's owned buffer, never native request originals."); }
                    return ProtectionQualificationPersistedObserver.Pointer(request, pointer)?.DeepClone();
                }
                if (reference["$concat"] is JsonArray pieces)
                {
                    return JsonValue.Create(string.Concat(pieces.Select(piece => Resolve(piece)?.GetValue<string>()
                        ?? throw new InvalidOperationException("Exact native identity string pieces required."))));
                }
                if (reference["$codec"] is JsonObject codec && codec.Count == 2)
                {
                    var input = Resolve(codec["input"]) ?? throw new InvalidOperationException("Exact codec input required.");
                    string digest = codec["name"]?.GetValue<string>() switch
                    {
                        "TargetManifestDigest" => DeletionBatchCapabilityIdentity.TargetManifestDigest(input.Deserialize<ProtectionTarget[]>()!),
                        "PredicateDigest" => GovernanceScopeGuardReducer.PredicateDigest(input.Deserialize<GovernanceScopeV1>()!),
                        "DestructionSealId" => GovernanceScopeGuardReducer.DestructionSealId(input["tenantId"]!.GetValue<string>(), input["requestId"]!.GetValue<string>(), input["scope"]!.Deserialize<GovernanceScopeV1>()!),
                        "BatchId" => GovernanceScopeGuardReducer.BatchId(input["requestId"]!.GetValue<string>(), input["sealId"]!.GetValue<string>(), input["batch"]!.Deserialize<GovernanceBatchCommand>()!),
                        "SigningRequestId" => DeletionBatchCapabilityCodec.SigningRequestId(input.Deserialize<DeletionBatchCapabilityV1>()!),
                        "SignedArtifactDigest" => DeletionBatchCapabilityIdentity.SignedArtifactDigest(input.GetValue<string>()),
                        "IssueOperationId" => DeletionBatchCapabilityIdentity.IssueOperationId(input["signingRequestId"]!.GetValue<string>(), input["detachedJwsDigest"]!.GetValue<string>()),
                        _ => throw new InvalidOperationException("Unknown source-owned native identity codec."),
                    };
                    return JsonValue.Create(digest);
                }
                if (reference["$original"] is JsonObject original && original.Count == 2)
                {
                    string id = original["id"]!.GetValue<string>();
                    if (!originals.TryGetValue(id, out var retained)) { throw new InvalidOperationException("Exact earlier native original required."); }
                    return ProtectionQualificationPersistedObserver.Pointer(retained, original["pointer"]!.GetValue<string>())?.DeepClone();
                }
            }
            if (value is JsonObject obj) { var bound = new JsonObject(); foreach (var pair in obj) { bound.Add(pair.Key, Resolve(pair.Value)); } return bound; }
            if (value is JsonArray array) { return new JsonArray(array.Select(Resolve).ToArray()); }
            return value?.DeepClone();
        }
        return template with { Payload = (JsonObject)Resolve(template.Payload)! };
    }
}
