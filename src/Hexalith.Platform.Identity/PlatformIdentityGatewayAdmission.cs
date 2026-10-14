using System.Security.Claims;
using System.Globalization;

using Hexalith.EventStore.Client.Security;
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

            if (!string.Equals(scope.PayloadDigest, IdentityAdmissionProof.Digest(payload), StringComparison.Ordinal))
            {
                return null;
            }

            bool provision = scope.Operation.Contains("ProvisionAgentParty", StringComparison.Ordinal);
            bool query = scope.Operation.Contains("Resolve", StringComparison.Ordinal);
            string? source = VerifiedWorkload(principal);
            bool currentJwtReader = source is null && scope.Operation.Split(',', 2)[0].Split('.').Last() == "ResolvePartyIdentity";
            (string SourceId, ClaimsIdentity Identity)? jwtReader = currentJwtReader ? VerifiedJwtReader(principal) : null;
            source ??= jwtReader?.SourceId;
            ClaimsPrincipal authorizationPrincipal = jwtReader is { } selected
                ? new ClaimsPrincipal(selected.Identity) : principal;
            string[] allowed = provision ? options.Value.ProvisioningSources
                : query ? options.Value.ReaderSources : options.Value.IdentityWriterSources;
            if (source is null || !allowed.Contains(source, StringComparer.Ordinal)
                || !(await tenantValidator.ValidateAsync(authorizationPrincipal, scope.TenantId, cancellationToken, scope.AggregateId).ConfigureAwait(false)).IsAuthorized
                || !(await rbacValidator.ValidateAsync(authorizationPrincipal, scope.TenantId, scope.Domain, scope.Operation,
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

            if (currentJwtReader && (string.IsNullOrWhiteSpace(actorId) || actorEvidence is not { Active: true, Revision: > 0 }
                || !string.Equals(actorEvidence.ActorId, actorId, StringComparison.Ordinal))
                || actorId is not null && actorEvidence is null)
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

    private (string SourceId, ClaimsIdentity Identity)? VerifiedJwtReader(ClaimsPrincipal principal)
    {
        foreach (PlatformIdentityJwtReader reader in options.Value.JwtReaders)
        {
            if (string.IsNullOrWhiteSpace(reader.SourceId) || string.IsNullOrWhiteSpace(reader.Issuer)
                || string.IsNullOrWhiteSpace(reader.Subject) || string.IsNullOrWhiteSpace(reader.ClientId)
                || string.IsNullOrWhiteSpace(reader.ClientIdClaimType) || string.IsNullOrWhiteSpace(reader.AuthenticationType)
                || reader.AuthenticationType == DaprInternalAuthenticationOptions.SchemeName) continue;
            foreach (ClaimsIdentity identity in principal.Identities.Where(identity => identity.IsAuthenticated
                && identity.AuthenticationType == reader.AuthenticationType))
            {
                // All service identity and lifetime claims belong to this one authenticated identity.
                if (ExactClaim(identity, "iss") != reader.Issuer || ExactClaim(identity, "sub") != reader.Subject
                    || ExactClaim(identity, reader.ClientIdClaimType) != reader.ClientId
                    || !long.TryParse(ExactClaim(identity, "exp"), NumberStyles.None, CultureInfo.InvariantCulture, out long expiry)
                    || expiry <= timeProvider.GetUtcNow().ToUnixTimeSeconds()) continue;
                string? notBefore = ExactClaim(identity, "nbf");
                if (notBefore is not null && (!long.TryParse(notBefore, NumberStyles.None, CultureInfo.InvariantCulture, out long start)
                    || start > timeProvider.GetUtcNow().ToUnixTimeSeconds())) continue;
                return (reader.SourceId, identity);
            }
        }
        return null;
    }

    private static string? ExactClaim(ClaimsIdentity identity, string type)
    {
        Claim[] claims = [.. identity.FindAll(type)];
        return claims.Length == 1 ? claims[0].Value : null;
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
