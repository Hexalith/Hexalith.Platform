using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Identity;

using Microsoft.Extensions.Options;

namespace Hexalith.Platform.Identity;

/// <summary>Trusted source and verified actor admission using existing authenticated tenant/RBAC hooks.</summary>
public sealed class PlatformIdentityGatewayAdmission(IOptions<PlatformIdentityOptions> options,
    IIdentityAdmissionSigner signer, PlatformActorRegistry registry, ITenantValidator tenantValidator,
    IRbacValidator rbacValidator, TimeProvider timeProvider, PlatformOperatorProvenanceVerifier provenanceVerifier) : IIdentityGatewayAdmission
{
    /// <inheritdoc/>
    public bool RequiresAdmission(string domain, string operation)
        => IdentityOperationCatalog.RequiresAdmission(operation);

    /// <inheritdoc/>
    public async Task<string?> AdmitAsync(ClaimsPrincipal principal, IdentityAdmissionScope scope, byte[] payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(payload);
        try
        {
            if (scope.Domain != "party" || !RequiresAdmission(scope.Domain, scope.Operation)
                || principal.Identity?.IsAuthenticated != true || options.Value.AuthorityRevision <= 0
                || new AggregateIdentity(scope.TenantId, scope.Domain, scope.AggregateId).TenantId != scope.TenantId)
            {
                return null;
            }

            string? source = VerifiedWorkload(principal);
            bool provision = scope.Operation.Contains("ProvisionAgentParty", StringComparison.Ordinal);
            bool query = scope.Operation.Contains("Resolve", StringComparison.Ordinal);
            string[] allowed = provision ? options.Value.ProvisioningSources
                : query ? options.Value.ReaderSources : options.Value.IdentityWriterSources;
            if (source is null || !allowed.Contains(source, StringComparer.Ordinal)
                || !(await tenantValidator.ValidateAsync(principal, scope.TenantId, cancellationToken, scope.AggregateId).ConfigureAwait(false)).IsAuthorized
                || !(await rbacValidator.ValidateAsync(principal, scope.TenantId, scope.Domain, scope.Operation,
                    query ? "query" : "command", cancellationToken, scope.AggregateId).ConfigureAwait(false)).IsAuthorized)
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement root = provision ? document.RootElement.GetProperty("identity") : document.RootElement;
            if (root.GetProperty("tenantId").GetString() != scope.TenantId || root.GetProperty("partyId").GetString() != scope.AggregateId)
            {
                return null;
            }

            string? actorId = root.TryGetProperty(query ? "expectedActorId" : "actorId", out JsonElement actor)
                && actor.ValueKind == JsonValueKind.String ? actor.GetString() : null;
            ActorRegistryEntry? actorEvidence = actorId is null ? null
                : await registry.ReadAsync(actorId, byAlias: false, source, cancellationToken).ConfigureAwait(false);
            if (!provision && !query && (actorEvidence is not { Active: true }
                || root.GetProperty("actorRevision").GetInt64() != actorEvidence.Revision))
            {
                return null;
            }

            if (actorId is not null && actorEvidence is null)
            {
                return null;
            }

            // Binding administration requires a separately verified operator provenance proof.
            string? operatorActorId = null;
            if (!query && !provision)
            {
                string? operatorProof = root.TryGetProperty("operatorProof", out JsonElement provenance)
                    && provenance.ValueKind == JsonValueKind.String ? provenance.GetString() : null;
                IdentityAdmissionEvidence? operatorEvidence = provenanceVerifier.Verify(scope, payload, operatorProof);
                if (operatorEvidence?.OperatorActorId is not { } operatorActor)
                {
                    return null;
                }

                ActorRegistryEntry? currentOperator = await registry.ReadAsync(operatorActor, byAlias: false, source, cancellationToken).ConfigureAwait(false);
                if (currentOperator is not { Active: true } || currentOperator.Revision != operatorEvidence.ActorRevision)
                {
                    return null;
                }

                operatorActorId = operatorActor;
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            return signer.Sign(new(scope, source, operatorActorId, actorEvidence?.ActorId, actorEvidence?.Revision ?? 0,
                actorEvidence?.Active ?? false, now, now.AddMinutes(1), options.Value.AuthorityRevision));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException
            or KeyNotFoundException or System.Security.Cryptography.CryptographicException or Dapr.DaprException)
        {
            return null;
        }
    }

    /// <summary>Returns only a workload authenticated through the existing Dapr scheme.</summary>
    public static string? VerifiedWorkload(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ClaimsIdentity? identity = principal.Identities.SingleOrDefault(candidate => candidate.IsAuthenticated
            && candidate.AuthenticationType == DaprInternalAuthenticationOptions.SchemeName);
        return identity?.FindFirst("dapr_caller_app_id")?.Value;
    }
}
