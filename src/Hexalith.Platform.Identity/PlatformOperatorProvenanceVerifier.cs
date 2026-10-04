using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.Platform.Identity;

/// <summary>Verifies separately issued operator/continuity evidence; JWT roles cannot grant this capability.</summary>
public sealed class PlatformOperatorProvenanceVerifier(
    [FromKeyedServices("identity-operator")] IIdentityAdmissionProof verifier,
    IOptions<PlatformIdentityOptions> options)
{
    /// <summary>Verifies an exact immutable operator intent with an independently configured public key.</summary>
    public IdentityAdmissionEvidence? Verify(IdentityAdmissionScope operation, byte[] payload, string? proof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(payload);
        JsonNode? document = JsonNode.Parse(payload);
        if (document is not JsonObject root)
        {
            return null;
        }

        _ = root.Remove("operatorProof");
        byte[] canonical = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(root);
        var expected = operation with
        {
            Domain = "identity-operator",
            MessageId = operation.LogicalId,
            PayloadDigest = IdentityAdmissionProof.Digest(canonical),
        };
        IdentityAdmissionEvidence? evidence = verifier.Verify(proof, expected);
        return evidence is not null && evidence.OperatorActorId == evidence.TargetActorId
            && evidence.ActorRevision > 0 && evidence.ActorActive
            && options.Value.OperatorProvenanceSources.Contains(evidence.SourceId, StringComparer.Ordinal)
                ? evidence : null;
    }
}
