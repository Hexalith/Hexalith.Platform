using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Identity;

using Microsoft.Extensions.Options;

namespace Hexalith.Platform.Identity;

/// <summary>Private trusted-service enrollment and alias administration, preserving existing login authentication.</summary>
public sealed class PlatformIdentityEnrollmentService(PlatformActorRegistry registry,
    PlatformOperatorProvenanceVerifier provenanceVerifier, PlatformBootstrapVerifier bootstrapVerifier, IOptions<PlatformIdentityOptions> options)
{
    /// <summary>Enrolls or links only with exact separately verified operator and continuity evidence.</summary>
    public async Task<ActorRegistryEntry?> ApplyAsync(ClaimsPrincipal workload, ActorRegistryMutation mutation,
        string operatorProof, CancellationToken cancellationToken = default, string? bootstrapProof = null)
    {
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(mutation);
        string? source = PlatformIdentityGatewayAdmission.VerifiedWorkload(workload);
        if (source is null || !options.Value.IdentityWriterSources.Contains(source, StringComparer.Ordinal)
            || mutation.RegistryNamespace != IdentityActorRegistryActor.RegistryNamespace || !mutation.ContinuityVerified)
        {
            return null;
        }

        var scope = new IdentityAdmissionScope("identity-authority", "identity-operator", mutation.RegistryNamespace,
            "RegistryMutation", mutation.OperationId, mutation.OperationId, string.Empty);
        IdentityAdmissionEvidence? proof = provenanceVerifier.Verify(scope, JsonSerializer.SerializeToUtf8Bytes(mutation), operatorProof);
        proof ??= bootstrapVerifier.Verify(mutation, bootstrapProof);
        if (proof?.OperatorActorId is not { } operatorActor || mutation.ProvenanceId != operatorActor)
        {
            return null;
        }

        ActorRegistryEntry? currentOperator = await registry.ReadAsync(operatorActor, byAlias: false, source, cancellationToken).ConfigureAwait(false);
        if (currentOperator is null ? bootstrapVerifier.Verify(mutation, bootstrapProof) is null
            : !currentOperator.Active || currentOperator.Revision != proof.ActorRevision)
        {
            return null;
        }

        return await registry.MutateAsync(mutation, source, operatorActor, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Resolves known verified logins without enrolling or deriving an actor from a claim.</summary>
    public async Task<ActorRegistryEntry?> ResolveLoginAsync(ClaimsPrincipal operatorPrincipal, string source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operatorPrincipal);
        string? issuer = operatorPrincipal.FindFirst("iss")?.Value;
        string? subject = operatorPrincipal.FindFirst("sub")?.Value;
        if (operatorPrincipal.Identity?.IsAuthenticated != true || issuer is null || subject is null
            || !options.Value.TrustedIssuers.Contains(issuer, StringComparer.Ordinal)
            || !options.Value.IdentityWriterSources.Contains(source, StringComparer.Ordinal))
        {
            return null;
        }

        string digest = PlatformLoginAliasDigest.Create(issuer, subject, options.Value.AliasKeyBase64);
        return await registry.ReadAsync(digest, byAlias: true, source, cancellationToken).ConfigureAwait(false);
    }
}
