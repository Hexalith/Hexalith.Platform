using Hexalith.EventStore.Contracts.Security;
using System.Security.Cryptography;
using System.Text;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.Platform.Custody;

/// <summary>Actual DAPR durable S4 signing intent/result owner; production registration/provider/trust authority remain disabled.</summary>
/// <param name="host">Exact private tenant/request actor.</param>
/// <param name="authority">Independent recorded guard authorization.</param>
/// <param name="provider">Qualified per-tenant deletion-only key backend with exact retained outcome.</param>
/// <param name="trustProvider">Independently published current tenant/family/version trust authority.</param>
public sealed class DeletionCapabilitySigningActor(ActorHost host, IDeletionCapabilitySigningAuthority? authority = null,
    IDeletionCapabilitySigningProvider? provider = null, IDeletionCapabilitySigningTrustProvider? trustProvider = null) : Actor(host), IDeletionCapabilitySigningActor
{
    private const string KeyFamily = "DeletionBatchCapabilitySigningKey";
    private const string StateKey = "deletion-capability-signing-v1";
    /// <summary>Gets the exact private actor registration name.</summary>
    public const string ActorTypeName = "DeletionCapabilitySigningActor";
    /// <summary>Gets the exact tenant/canonical-request actor address; successful committed issue revision is absent by construction.</summary>
    public static string GetActorId(DeletionBatchCapabilityV1 payload) => new AggregateIdentity(payload.TenantId, "custody",
        "deletion-signing-" + DeletionBatchCapabilityCodec.SigningRequestId(payload)).ActorId;
    /// <inheritdoc/>
    public async Task<DeletionCapabilitySigningOutcome> SignAsync(DeletionBatchCapabilityV1 payload)
    {
        string requestId = Check(payload);
        var existing = await ReadAsync(payload, requestId).ConfigureAwait(false);
        if (existing is not null) { return await ResolveAsync(payload, requestId, existing).ConfigureAwait(false); }
        if (authority is null || provider is null || trustProvider is null) { return new(requestId, payload, DeletionCapabilitySigningState.Unavailable); }
        if (!await authority.AuthorizeAsync(payload, requestId).ConfigureAwait(false))
        { return await SaveAsync(new(requestId, payload, DeletionCapabilitySigningState.Denied)).ConfigureAwait(false); }
        if (await ResolveTrustAsync(payload).ConfigureAwait(false) is null) { return new(requestId, payload, DeletionCapabilitySigningState.Unavailable); }
        await SaveAsync(new(requestId, payload, DeletionCapabilitySigningState.Unknown)).ConfigureAwait(false);
        if (!await authority.AuthorizeAsync(payload, requestId).ConfigureAwait(false))
        { return await SaveAsync(new(requestId, payload, DeletionCapabilitySigningState.Denied)).ConfigureAwait(false); }
        if (await ResolveTrustAsync(payload).ConfigureAwait(false) is null) { return new(requestId, payload, DeletionCapabilitySigningState.Unavailable); }
        try { return await RetainAsync(payload, requestId, await provider.SignAsync(payload, requestId).ConfigureAwait(false)).ConfigureAwait(false); }
        catch (Exception) { return new(requestId, payload, DeletionCapabilitySigningState.Unknown); }
    }
    /// <inheritdoc/>
    public async Task<DeletionCapabilitySigningOutcome> LookupAsync(DeletionBatchCapabilityV1 payload)
    {
        string requestId = Check(payload); var existing = await ReadAsync(payload, requestId).ConfigureAwait(false);
        return existing is null ? new(requestId, payload, DeletionCapabilitySigningState.Unavailable)
            : await ResolveAsync(payload, requestId, existing).ConfigureAwait(false);
    }
    private string Check(DeletionBatchCapabilityV1 payload)
    {
        ArgumentNullException.ThrowIfNull(payload); string request = DeletionBatchCapabilityCodec.SigningRequestId(payload);
        if (Host.Id.GetId() != GetActorId(payload)) { throw new ArgumentException("Deletion signing scope mismatch.", nameof(payload)); }
        return request;
    }
    private async Task<DeletionCapabilitySigningOutcome> ResolveAsync(DeletionBatchCapabilityV1 payload, string id, DeletionCapabilitySigningOutcome existing)
    {
        if (existing.Payload != payload || existing.SigningRequestId != id) { return new(id, payload, DeletionCapabilitySigningState.Conflict); }
        if (existing.State is DeletionCapabilitySigningState.Signed or DeletionCapabilitySigningState.Denied) { return existing; }
        if (provider is null || trustProvider is null) { return new(id, payload, DeletionCapabilitySigningState.Unavailable); }
        try { return await RetainAsync(payload, id, await provider.LookupAsync(payload, id).ConfigureAwait(false)).ConfigureAwait(false); }
        catch (Exception) { return new(id, payload, DeletionCapabilitySigningState.Unknown); }
    }
    private async Task<DeletionCapabilitySigningOutcome> RetainAsync(DeletionBatchCapabilityV1 payload, string id, DeletionCapabilitySigningResult result)
    {
        if (result.Outcome is not { } outcome || outcome.Payload != payload || outcome.SigningRequestId != id)
        { return new(id, payload, DeletionCapabilitySigningState.Conflict); }
        if (outcome.State != DeletionCapabilitySigningState.Signed) { return new(id, payload, DeletionCapabilitySigningState.Unknown); }
        if (outcome.DetachedJws is not { Length: > 0 and <= 16384 } || string.IsNullOrWhiteSpace(outcome.PublicAnchorId)
            || outcome.PublicAnchorId.Length > 2048 || string.IsNullOrWhiteSpace(outcome.PublicAnchorVersion) || outcome.PublicAnchorVersion.Length > 2048
            || result.PublicAnchorSubjectPublicKeyInfo is not { Length: > 0 and <= 512 } supplied)
        { return new(id, payload, DeletionCapabilitySigningState.Unknown); }
        var trust = await ResolveTrustAsync(payload).ConfigureAwait(false);
        if (trust is null || outcome.PublicAnchorId != trust.PublicAnchorId || outcome.PublicAnchorVersion != trust.PublicAnchorVersion
            || !CryptographicOperations.FixedTimeEquals(supplied, trust.SubjectPublicKeyInfo))
        { return new(id, payload, DeletionCapabilitySigningState.Unknown); }
        byte[] ownedAnchor = trust.SubjectPublicKeyInfo; using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(ownedAnchor, out int consumed);
        var profile = new DeletionCapabilityTrustProfile(payload.Issuer, payload.Audience, payload.TenantId, payload.CapabilityKeyVersion,
            trust.PublicAnchorId, trust.PublicAnchorVersion);
        if (consumed != ownedAnchor.Length || !DeletionBatchCapabilityCodec.Verify(payload, profile, outcome.DetachedJws, key))
        { return new(id, payload, DeletionCapabilitySigningState.Unknown); }
        return await SaveAsync(outcome).ConfigureAwait(false);
    }
    private async Task<DeletionCapabilityPublishedTrust?> ResolveTrustAsync(DeletionBatchCapabilityV1 payload)
    {
        if (trustProvider is null) { return null; }
        var trust = await trustProvider.ResolveAsync(payload.TenantId, KeyFamily, payload.CapabilityKeyVersion).ConfigureAwait(false);
        if (trust is null || !trust.IsCurrentNonRevoked || trust.TrustProfileRevision <= 0 || trust.TenantId != payload.TenantId
            || trust.KeyFamily != KeyFamily || trust.CapabilityKeyVersion != payload.CapabilityKeyVersion
            || trust.Issuer != payload.Issuer || trust.Audience != payload.Audience
            || trust.SubjectPublicKeyInfo is not { Length: > 0 and <= 512 }) { return null; }
        var utf8 = new UTF8Encoding(false, true);
        try
        {
            foreach (string field in new[] { trust.TenantId, trust.KeyFamily, trust.CapabilityKeyVersion, trust.Issuer, trust.Audience,
                trust.PublicAnchorId, trust.PublicAnchorVersion })
            { if (string.IsNullOrWhiteSpace(field) || field.Length > 2048 || utf8.GetByteCount(field) > 2048) { return null; } }
        }
        catch (EncoderFallbackException) { return null; }
        return trust with { SubjectPublicKeyInfo = trust.SubjectPublicKeyInfo.ToArray() };
    }
    private async Task<DeletionCapabilitySigningOutcome?> ReadAsync(DeletionBatchCapabilityV1 payload, string id)
    {
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var state = await StateManager.TryGetStateAsync<DeletionCapabilitySigningOutcome>(StateKey).ConfigureAwait(false);
        if (!state.HasValue) { return null; }
        var value = state.Value;
        if (value.Payload != payload || value.SigningRequestId != id || DeletionBatchCapabilityCodec.SigningRequestId(value.Payload) != id
            || value.State is not (DeletionCapabilitySigningState.Unknown or DeletionCapabilitySigningState.Signed or DeletionCapabilitySigningState.Denied)
            || value.State == DeletionCapabilitySigningState.Signed && (string.IsNullOrWhiteSpace(value.DetachedJws)
                || value.DetachedJws.Length > 16384 || string.IsNullOrWhiteSpace(value.PublicAnchorId) || string.IsNullOrWhiteSpace(value.PublicAnchorVersion))
            || value.State != DeletionCapabilitySigningState.Signed && (value.DetachedJws is not null || value.PublicAnchorId is not null || value.PublicAnchorVersion is not null))
        { throw new InvalidOperationException("Malformed persisted deletion signing outcome."); }
        return value;
    }
    private async Task<DeletionCapabilitySigningOutcome> SaveAsync(DeletionCapabilitySigningOutcome outcome)
    {
        await StateManager.SetStateAsync(StateKey, outcome).ConfigureAwait(false); await StateManager.SaveStateAsync().ConfigureAwait(false);
        var persisted = await ReadAsync(outcome.Payload, outcome.SigningRequestId).ConfigureAwait(false);
        if (persisted != outcome) { throw new InvalidOperationException("Signing result is not confirmed durable."); }
        return outcome;
    }
}
