using System.Text.Json;

using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Identity;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.Platform.Identity;

/// <summary>Separate public verification of an explicit first-operator enrollment approval.</summary>
public sealed class PlatformBootstrapVerifier(
    [FromKeyedServices("identity-bootstrap")] IIdentityAdmissionProof verifier,
    IOptions<PlatformIdentityOptions> options)
{
    /// <summary>Verifies exact first enrollment intent; no signing or login enrollment capability is exposed.</summary>
    public IdentityAdmissionEvidence? Verify(ActorRegistryMutation mutation, string? proof)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        var scope = new IdentityAdmissionScope("identity-authority", "identity-bootstrap", mutation.RegistryNamespace,
            "RegistryBootstrap", mutation.OperationId, mutation.OperationId,
            IdentityAdmissionProof.Digest(JsonSerializer.SerializeToUtf8Bytes(mutation)));
        IdentityAdmissionEvidence? evidence = verifier.Verify(proof, scope);
        return evidence is not null && options.Value.BootstrapProvenanceSources.Contains(evidence.SourceId, StringComparer.Ordinal)
            && evidence.OperatorActorId == mutation.ActorId && evidence.TargetActorId == mutation.ActorId
            && mutation.ProvenanceId == mutation.ActorId && mutation.ExpectedRevision == 0
            && mutation.Active && mutation.AliasActive && mutation.RetiredAliasDigest is null
            ? evidence : null;
    }
}
