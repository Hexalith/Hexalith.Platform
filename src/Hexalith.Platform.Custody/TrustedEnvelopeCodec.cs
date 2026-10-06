using System.Globalization;

namespace Hexalith.Platform.Custody;

/// <summary>One fixed AD-30 field order over AD-29 framing, with explicit optional markers.</summary>
internal static class TrustedEnvelopeCodec
{
    /// <summary>Copies caller-owned tuple state before any await.</summary>
    internal static TrustedEnvelopeIdentity Snapshot(TrustedEnvelopeIdentity identity)
        => identity with { IdempotencyTuple = Array.AsReadOnly(identity.IdempotencyTuple.ToArray()) };
    /// <summary>Checks closed shape and exact accepted configuration.</summary>
    internal static bool IsValid(TrustedEnvelopeIdentity i, PlatformSigningProfile p)
        => i.SchemaVersion == 1 && i.ProfileVersion == p.Version && i.Issuer == p.Issuer && i.Audience == p.Audience
            && i.Principal.IsValid(i.TargetTenantId) && new[] { i.CommandContract, i.OperationFamily, i.TargetTenantId,
                i.TargetResource, i.CorrelationId, i.CausationId, i.PayloadFingerprint, i.DigestKeyVersion, i.LogicalCommandId }
                .All(v => !string.IsNullOrWhiteSpace(v)) && i.IdempotencyTuple.Count > 0 && i.IdempotencyTuple.All(v => !string.IsNullOrWhiteSpace(v));
    /// <summary>Encodes every immutable identity component, including tuple cardinality.</summary>
    internal static byte[] Identity(TrustedEnvelopeIdentity i)
    {
        TrustedPrincipal p = i.Principal;
        return PlatformCanonicalBytes.Components(new string?[] { "trusted-envelope", i.SchemaVersion.ToString(CultureInfo.InvariantCulture),
            i.ProfileVersion, i.Issuer, p.Kind.ToString(), p.ActorTenantId, p.HumanActorId, p.PartyId,
            p.BindingVersion?.ToString(CultureInfo.InvariantCulture), p.RoleBasis, p.WorkflowKind, p.WorkflowInstanceId,
            p.Activity, p.OnBehalfOfPartyId, i.CommandContract, i.OperationFamily, i.TargetTenantId, i.TargetResource,
            i.CorrelationId, i.CausationId, i.IdempotencyTuple.Count.ToString(CultureInfo.InvariantCulture) }
            .Concat(i.IdempotencyTuple).Concat([i.PayloadFingerprint, i.DigestKeyVersion, i.Audience, i.LogicalCommandId]));
    }
    /// <summary>Encodes identity followed by exact UTC instants, delivery nonce and signing version.</summary>
    internal static byte[] Delivery(TrustedEnvelope e)
    {
        byte[] identity = Identity(e.Identity);
        byte[] delivery = PlatformCanonicalBytes.Identity([e.IssuedAt.UtcTicks.ToString(CultureInfo.InvariantCulture),
            e.ExpiresAt.UtcTicks.ToString(CultureInfo.InvariantCulture), e.DeliveryNonce, e.SigningKeyVersion]);
        try
        {
            return [.. identity, .. delivery];
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(identity);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(delivery);
        }
    }
}
