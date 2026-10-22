using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independent published trust. An empty SPKI is not a key.</summary>
public sealed class DaprSigningTrustAdapter(SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock = null) : IDeletionCapabilitySigningTrustProvider
{
    private readonly SecretsDaprSession _session = new(invoker, configuration.CallTimeout ?? TimeSpan.Zero, clock ?? TimeProvider.System);

    /// <inheritdoc/>
    public async Task<DeletionCapabilityPublishedTrust?> ResolveAsync(string tenantId, string keyFamily, string keyVersion, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject { ["tenantId"] = tenantId, ["keyFamily"] = keyFamily, ["keyVersion"] = keyVersion };
        JsonObject? proof = (await _session.CallAsync(configuration.PublicTrustReference, "runtime.publicTrustReference", "query-trust", body, cancellationToken).ConfigureAwait(false)).Proof;
        byte[]? spki = SecretsProof.SubjectPublicKey(proof);
        long? revision = SecretsProof.Integer(proof, "trustProfileRevision");
        if (proof is null || spki is null || revision is null || revision <= 0 || !SecretsProof.SameText(proof, "tenantId", tenantId)
            || !SecretsProof.SameText(proof, "keyFamily", keyFamily) || !SecretsProof.SameText(proof, "capabilityKeyVersion", keyVersion)
            || SecretsProof.Flag(proof, "isCurrentNonRevoked") is not bool current || SecretsProof.Flag(proof, "isRevoked") is not bool revoked
            || !SecretsProvisioning.IsReference(SecretsProof.Text(proof, "issuer")) || !SecretsProvisioning.IsReference(SecretsProof.Text(proof, "audience"))
            || !SecretsProvisioning.IsReference(SecretsProof.Text(proof, "publicAnchorId")) || !SecretsProvisioning.IsReference(SecretsProof.Text(proof, "publicAnchorVersion")))
        { return null; }
        DateTimeOffset? observedAt = SecretsProof.Instant(proof, "observedAt");
        DateTimeOffset? validUntil = SecretsProof.Instant(proof, "validUntil");
        if (observedAt is null || validUntil is null || validUntil <= observedAt || validUntil.Value - observedAt.Value > TimeSpan.FromSeconds(30)
            || observedAt > _session.Clock.GetUtcNow() || _session.Clock.GetUtcNow() >= validUntil) { return null; }
        return new DeletionCapabilityPublishedTrust(tenantId, keyFamily, keyVersion, SecretsProof.Text(proof, "issuer")!, SecretsProof.Text(proof, "audience")!,
            SecretsProof.Text(proof, "publicAnchorId")!, SecretsProof.Text(proof, "publicAnchorVersion")!, spki, revision.Value, current, revoked);
    }
}

/// <summary>Revocation proof must echo the evidence digest and event identity.</summary>
public sealed class DaprRevocationAuthenticatorAdapter(SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock = null) : IDeletionCapabilityRevocationAuthenticator
{
    private readonly SecretsDaprSession _session = new(invoker, configuration.CallTimeout ?? TimeSpan.Zero, clock ?? TimeProvider.System);

    /// <inheritdoc/>
    public async Task<DeletionCapabilityRevocationAuthorization?> VerifyAsync(DeletionCapabilityRevocationEnvelope envelope, string signedEvidence, CancellationToken cancellationToken = default)
    {
        if (envelope is null || string.IsNullOrWhiteSpace(signedEvidence)) { return null; }
        string evidenceDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signedEvidence))).ToLowerInvariant();
        var body = new JsonObject
        {
            ["issuer"] = envelope.Issuer,
            ["audience"] = envelope.Audience,
            ["tenantId"] = envelope.TenantId,
            ["keyFamily"] = envelope.KeyFamily,
            ["keyVersion"] = envelope.KeyVersion,
            ["revocationRevision"] = envelope.RevocationRevision,
            ["trustProfileRevision"] = envelope.TrustProfileRevision,
            ["eventIdentity"] = envelope.EventIdentity,
            ["signatureDigest"] = envelope.SignatureDigest,
            ["evidenceDigest"] = evidenceDigest,
        };
        JsonObject? proof = (await _session.CallAsync(configuration.CurrentRevocationReference, "runtime.currentRevocationReference", "query-revocation", body, cancellationToken).ConfigureAwait(false)).Proof;
        if (!SecretsProof.Authorized(proof, body, _session.Clock.GetUtcNow()) || SecretsProof.SameText(proof, "status", "absent")) { return null; }
        DateTimeOffset? observedAt = SecretsProof.Instant(proof, "observedAt");
        DateTimeOffset? validUntil = SecretsProof.Instant(proof, "validUntil");
        DateTimeOffset now = _session.Clock.GetUtcNow();
        if (observedAt is null || validUntil is null || observedAt > now || now >= validUntil || validUntil.Value - observedAt.Value > TimeSpan.FromSeconds(30)) { return null; }
        return new DeletionCapabilityRevocationAuthorization(envelope, SecretsProof.Text(proof, "authorityRevision")!, observedAt.Value, validUntil.Value);
    }
}

/// <summary>Signing effects run only when the current proof is not already the signed outcome for the same request, key, and digest.</summary>
public sealed class DaprSigningProviderAdapter(SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock = null) : IDeletionCapabilitySigningProvider
{
    private readonly SecretsDaprSession _session = new(invoker, configuration.CallTimeout ?? TimeSpan.Zero, clock ?? TimeProvider.System);

    /// <inheritdoc/>
    public Task<DeletionCapabilitySigningResult> SignAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, CancellationToken cancellationToken = default)
        => ResolveAsync(payload, signingRequestId, effect: true, cancellationToken);

    /// <inheritdoc/>
    public Task<DeletionCapabilitySigningResult> LookupAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, CancellationToken cancellationToken = default)
        => ResolveAsync(payload, signingRequestId, effect: false, cancellationToken);

    private async Task<DeletionCapabilitySigningResult> ResolveAsync(DeletionBatchCapabilityV1 payload, string signingRequestId, bool effect, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        string digest = CapabilityIdentity.Digest(payload);
        var body = new JsonObject
        {
            ["tenantId"] = payload.TenantId,
            ["signingRequestId"] = signingRequestId,
            ["keyVersion"] = payload.CapabilityKeyVersion,
            ["capabilityDigest"] = digest,
            ["canonicalPayload"] = Convert.ToBase64String(DeletionBatchCapabilityIdentity.CanonicalPayload(payload)),
        };
        SecretsCall current = await _session.CallPrivateAsync(configuration.CapabilitySignerReference, "runtime.capabilitySignerReference", configuration.CapabilitySignerCredentialReference, "runtime.capabilitySignerCredentialReference", "query-sign", body, cancellationToken).ConfigureAwait(false);
        if (current.Unavailable) { return Unavailable(payload, signingRequestId); }
        if (Signed(current.Proof, payload, signingRequestId, digest)) { return Result(current.Proof!, payload, signingRequestId); }
        if (!effect || !SecretsProof.Absent(current.Proof, body, configuration.CapabilitySignerReference, "query-sign", _session.Clock.GetUtcNow())) { return Unavailable(payload, signingRequestId); }
        SecretsCall signed = await _session.CallPrivateAsync(configuration.CapabilitySignerReference, "runtime.capabilitySignerReference", configuration.CapabilitySignerCredentialReference, "runtime.capabilitySignerCredentialReference", "effect-sign", body, cancellationToken).ConfigureAwait(false);
        return !signed.Unavailable && Signed(signed.Proof, payload, signingRequestId, digest) ? Result(signed.Proof!, payload, signingRequestId) : Unavailable(payload, signingRequestId);
    }

    private bool Signed(JsonObject? proof, DeletionBatchCapabilityV1 payload, string signingRequestId, string digest)
    {
        DateTimeOffset? observedAt = SecretsProof.Instant(proof, "observedAt");
        DateTimeOffset? validUntil = SecretsProof.Instant(proof, "validUntil");
        DateTimeOffset now = _session.Clock.GetUtcNow();
        return proof is not null && SecretsProof.SameText(proof, "state", nameof(DeletionCapabilitySigningState.Signed))
            && SecretsProof.SameText(proof, "tenantId", payload.TenantId) && SecretsProof.SameText(proof, "signingRequestId", signingRequestId)
            && SecretsProof.SameText(proof, "keyVersion", payload.CapabilityKeyVersion) && SecretsProof.SameText(proof, "capabilityDigest", digest)
            && SecretsProof.SameText(proof, "canonicalPayload", Convert.ToBase64String(DeletionBatchCapabilityIdentity.CanonicalPayload(payload)))
            && observedAt is not null && validUntil is not null && observedAt <= now && now < validUntil && validUntil.Value - observedAt.Value <= TimeSpan.FromSeconds(30)
            && SecretsProof.SubjectPublicKey(proof) is not null && SecretsProvisioning.IsReference(SecretsProof.Text(proof, "detachedJws"))
            && SecretsProvisioning.IsReference(SecretsProof.Text(proof, "publicAnchorId")) && SecretsProvisioning.IsReference(SecretsProof.Text(proof, "publicAnchorVersion"))
            && SecretsProvisioning.IsReference(SecretsProof.Text(proof, "originalIssuanceReceiptId"));
    }

    private static DeletionCapabilitySigningResult Result(JsonObject proof, DeletionBatchCapabilityV1 payload, string signingRequestId)
        => new(new DeletionCapabilitySigningOutcome(signingRequestId, payload, DeletionCapabilitySigningState.Signed, SecretsProof.Text(proof, "detachedJws"),
            SecretsProof.Text(proof, "publicAnchorId"), SecretsProof.Text(proof, "publicAnchorVersion")), SecretsProof.SubjectPublicKey(proof), SecretsProof.Text(proof, "originalIssuanceReceiptId"));

    private static DeletionCapabilitySigningResult Unavailable(DeletionBatchCapabilityV1 payload, string signingRequestId)
        => new(new DeletionCapabilitySigningOutcome(signingRequestId, payload, DeletionCapabilitySigningState.Unavailable));
}

/// <summary>Private replay registrar. Receipts that leave this adapter satisfy the custody exact-retention check.</summary>
public sealed class DaprReplayRegistrarAdapter : ITrustedEnvelopeReplayRegistrar
{
    private readonly SecretsRuntimeConfiguration _configuration;
    private readonly SecretsDaprSession _session;
    private readonly string? _credentialReference;
    private readonly bool _credentialDenied;

    /// <summary>Creates the transport adapter. A captured credential is supplied only through the internal constructor.</summary>
    public DaprReplayRegistrarAdapter(SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock = null)
        : this(configuration, invoker, clock, credential: null)
    {
    }

    internal DaprReplayRegistrarAdapter(SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock, PrivateReplayCredentialBinding? credential)
    {
        _configuration = configuration;
        _session = new(invoker, configuration.CallTimeout ?? TimeSpan.Zero, clock ?? TimeProvider.System);
        _credentialReference = credential?.Reference ?? configuration.ReplayRegistrarCredentialReference;
        _credentialDenied = credential is { IsCurrent: false };
    }

    /// <inheritdoc/>
    public async Task<TrustedEnvelopeReplayReceipt?> RegisterAsync(TrustedEnvelopeReplayIntent intent, DateTimeOffset evaluatedAt, TimeSpan retention, CancellationToken cancellationToken = default)
    {
        if (_credentialDenied) { return null; }
        JsonObject body = Body(intent, evaluatedAt, retention);
        SecretsCall current = await _session.CallPrivateAsync(_configuration.ReplayRegistrarReference, "runtime.replayRegistrarReference", _credentialReference, "runtime.replayRegistrarCredentialReference", "query-replay", body, cancellationToken).ConfigureAwait(false);
        if (current.Unavailable) { return null; }
        if (Receipt(current.Proof, intent, evaluatedAt, retention) is { } existing) { return existing; }
        if (!SecretsProof.Absent(current.Proof, body, _configuration.ReplayRegistrarReference, "query-replay", _session.Clock.GetUtcNow())) { return null; }
        SecretsCall created = await _session.CallPrivateAsync(_configuration.ReplayRegistrarReference, "runtime.replayRegistrarReference", _credentialReference, "runtime.replayRegistrarCredentialReference", "effect-replay", body, cancellationToken).ConfigureAwait(false);
        return created.Unavailable ? null : Receipt(created.Proof, intent, evaluatedAt, retention);
    }

    /// <inheritdoc/>
    public async Task<TrustedEnvelopeReplayReceipt?> LookupAsync(TrustedEnvelopeReplayIntent intent, CancellationToken cancellationToken = default)
    {
        if (_credentialDenied) { return null; }
        SecretsCall proof = await _session.CallPrivateAsync(_configuration.ReplayRegistrarReference, "runtime.replayRegistrarReference", _credentialReference, "runtime.replayRegistrarCredentialReference", "query-replay", Body(intent, null, null), cancellationToken).ConfigureAwait(false);
        return proof.Unavailable ? null : Receipt(proof.Proof, intent, evaluatedAt: null, retention: null);
    }

    private static JsonObject Body(TrustedEnvelopeReplayIntent intent, DateTimeOffset? evaluatedAt, TimeSpan? retention)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var body = new JsonObject
        {
            ["issuer"] = intent.Issuer,
            ["deliveryNonce"] = intent.DeliveryNonce,
            ["targetTenantId"] = intent.TargetTenantId,
            ["logicalCommandId"] = intent.LogicalCommandId,
            ["signingKeyVersion"] = intent.SigningKeyVersion,
            ["canonicalAndTagDigest"] = intent.CanonicalAndTagDigest,
        };
        if (evaluatedAt is DateTimeOffset at) { body["evaluatedAt"] = at.ToUniversalTime().ToString("O"); }
        if (retention is TimeSpan window) { body["retentionTicks"] = window.Ticks; }
        return body;
    }

    private static TrustedEnvelopeReplayReceipt? Receipt(JsonObject? proof, TrustedEnvelopeReplayIntent intent, DateTimeOffset? evaluatedAt, TimeSpan? retention)
    {
        if (proof is null || !SecretsProof.SameText(proof, "issuer", intent.Issuer) || !SecretsProof.SameText(proof, "deliveryNonce", intent.DeliveryNonce)
            || !SecretsProof.SameText(proof, "targetTenantId", intent.TargetTenantId) || !SecretsProof.SameText(proof, "logicalCommandId", intent.LogicalCommandId)
            || !SecretsProof.SameText(proof, "signingKeyVersion", intent.SigningKeyVersion) || !SecretsProof.SameText(proof, "canonicalAndTagDigest", intent.CanonicalAndTagDigest)
            || SecretsProof.Integer(proof, "sourceRevision") != 1 || !SecretsProvisioning.IsReference(SecretsProof.Text(proof, "receiptId"))) { return null; }
        DateTimeOffset? firstSeen = SecretsProof.Instant(proof, "firstSeenAt");
        DateTimeOffset? retainUntil = SecretsProof.Instant(proof, "retainUntil");
        if (firstSeen is null || retainUntil is null) { return null; }
        if (evaluatedAt is DateTimeOffset at && firstSeen > at) { return null; }
        if (retention is TimeSpan window)
        {
            try { if (retainUntil != firstSeen.Value.Add(window)) { return null; } }
            catch (ArgumentOutOfRangeException) { return null; }
        }
        return new TrustedEnvelopeReplayReceipt(intent, firstSeen.Value, retainUntil.Value, 1, SecretsProof.Text(proof, "receiptId")!);
    }
}

/// <summary>Private compromise registrar. A missing subscription or mirror names that input.</summary>
public sealed class DaprCompromiseRegistrarAdapter(SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock = null) : IDeletionCapabilityCompromiseRegistrar
{
    private readonly SecretsDaprSession _session = new(invoker, configuration.CallTimeout ?? TimeSpan.Zero, clock ?? TimeProvider.System);

    /// <summary>Names a missing mirror before any subscriber effect.</summary>
    public void RequireMirror()
    {
        if (!SecretsProvisioning.IsReference(configuration.CompromiseGuardMirrorReference))
        {
            throw new SecretsDependencyNotAvailableException("runtime.compromiseGuardMirrorReference", SecretsProvisioning.Action("runtime.compromiseGuardMirrorReference"));
        }
    }

    /// <inheritdoc/>
    public async Task<DeletionCapabilityRevocationReceipt?> RegisterAsync(DeletionCapabilityRevocationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        RequireMirror();
        JsonObject body = Body(envelope);
        SecretsCall current = await _session.CallPrivateAsync(configuration.CompromiseSubscriptionReference, "runtime.compromiseSubscriptionReference", configuration.CompromiseSubscriberCredentialReference, "runtime.compromiseSubscriberCredentialReference", "query-compromise", body, cancellationToken).ConfigureAwait(false);
        if (current.Unavailable) { return null; }
        if (Receipt(current.Proof, envelope) is { } existing) { return existing; }
        if (!SecretsProof.Absent(current.Proof, body, configuration.CompromiseSubscriptionReference, "query-compromise", _session.Clock.GetUtcNow())) { return null; }
        SecretsCall proof = await _session.CallPrivateAsync(configuration.CompromiseSubscriptionReference, "runtime.compromiseSubscriptionReference", configuration.CompromiseSubscriberCredentialReference, "runtime.compromiseSubscriberCredentialReference", "effect-compromise", body, cancellationToken).ConfigureAwait(false);
        return proof.Unavailable ? null : Receipt(proof.Proof, envelope);
    }

    /// <inheritdoc/>
    public async Task<DeletionCapabilityRevocationReceipt?> LookupAsync(DeletionCapabilityRevocationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        RequireMirror();
        SecretsCall proof = await _session.CallPrivateAsync(configuration.CompromiseSubscriptionReference, "runtime.compromiseSubscriptionReference", configuration.CompromiseSubscriberCredentialReference, "runtime.compromiseSubscriberCredentialReference", "query-compromise", Body(envelope), cancellationToken).ConfigureAwait(false);
        return proof.Unavailable ? null : Receipt(proof.Proof, envelope);
    }

    private static JsonObject Body(DeletionCapabilityRevocationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return new JsonObject
        {
            ["issuer"] = envelope.Issuer,
            ["audience"] = envelope.Audience,
            ["tenantId"] = envelope.TenantId,
            ["keyFamily"] = envelope.KeyFamily,
            ["keyVersion"] = envelope.KeyVersion,
            ["revocationRevision"] = envelope.RevocationRevision,
            ["trustProfileRevision"] = envelope.TrustProfileRevision,
            ["eventIdentity"] = envelope.EventIdentity,
            ["signatureDigest"] = envelope.SignatureDigest,
        };
    }

    private static DeletionCapabilityRevocationReceipt? Receipt(JsonObject? proof, DeletionCapabilityRevocationEnvelope envelope)
    {
        long? owner = SecretsProof.Integer(proof, "ownerRevision");
        long? block = SecretsProof.Integer(proof, "keyBlockSetRevision");
        string[]? batches = SecretsProof.Texts(proof, "affectedBatchIds");
        if (proof is null || owner is null || owner <= 0 || block is null || block <= 0 || batches is null || batches.Distinct(StringComparer.Ordinal).Count() != batches.Length
            || !SecretsProvisioning.IsReference(SecretsProof.Text(proof, "receiptId")) || !SecretsProof.SameText(proof, "eventIdentity", envelope.EventIdentity)
            || !SecretsProof.SameText(proof, "tenantId", envelope.TenantId) || !SecretsProof.SameText(proof, "issuer", envelope.Issuer)
            || !SecretsProof.SameText(proof, "audience", envelope.Audience) || !SecretsProof.SameText(proof, "keyFamily", envelope.KeyFamily)
            || !SecretsProof.SameText(proof, "keyVersion", envelope.KeyVersion) || !SecretsProof.SameText(proof, "signatureDigest", envelope.SignatureDigest)
            || !SecretsProof.SameInteger(proof, "revocationRevision", envelope.RevocationRevision)
            || !SecretsProof.SameInteger(proof, "trustProfileRevision", envelope.TrustProfileRevision)) { return null; }
        return new DeletionCapabilityRevocationReceipt(envelope, owner.Value, block.Value, SecretsProof.Text(proof, "receiptId")!, batches);
    }
}

internal static class CapabilityIdentity
{
    public static string Digest(DeletionBatchCapabilityV1 payload)
        => Convert.ToHexString(SHA256.HashData(DeletionBatchCapabilityIdentity.CanonicalPayload(payload))).ToLowerInvariant();
}
