using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using System.Text;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.Platform.Custody;

/// <summary>Actual DAPR durable S4 signing intent/result owner; production registration/provider/trust authority remain disabled.</summary>
/// <param name="host">Exact private tenant/request actor.</param>
/// <param name="authority">Independent recorded guard authorization.</param>
/// <param name="provider">Qualified per-tenant deletion-only key backend with exact retained outcome.</param>
/// <param name="trustProvider">Independently published current/retained tenant/family/version trust authority.</param>
/// <param name="originalReceiptAuthority">Independent original historic provider receipt verifier, mandatory for unresolved results after revocation.</param>
/// <param name="noIssueAuthority">Independent exact terminal guard no-issue authority; omitted means obsolescence unavailable.</param>
/// <param name="timeProvider">Current exclusive proof clock.</param>
public sealed class DeletionCapabilitySigningActor(ActorHost host, IDeletionCapabilitySigningAuthority? authority = null,
    IDeletionCapabilitySigningProvider? provider = null, IDeletionCapabilitySigningTrustProvider? trustProvider = null,
    IDeletionCapabilityOriginalSigningReceiptAuthority? originalReceiptAuthority = null,
    IDeletionCapabilityNoIssueAuthority? noIssueAuthority = null, TimeProvider? timeProvider = null) : Actor(host), IDeletionCapabilitySigningActor
{
    private const string KeyFamily = "DeletionBatchCapabilitySigningKey";
    private readonly PrivateActorStateIoLifetime _stateIo = new();
    private const string StateKey = "deletion-capability-signing-v1";
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    /// <summary>Gets the exact private actor registration name.</summary>
    public const string ActorTypeName = "DeletionCapabilitySigningActor";
    /// <summary>Gets the exact tenant/canonical-request actor address; successful committed issue revision is absent by construction.</summary>
    public static string GetActorId(DeletionBatchCapabilityV1 payload) => new AggregateIdentity(payload.TenantId, "custody",
        "deletion-signing-" + DeletionBatchCapabilityCodec.SigningRequestId(payload)).ActorId;
    /// <inheritdoc/>
    public async Task<DeletionCapabilitySigningOutcome> SignAsync(DeletionBatchCapabilityV1 payload)
    {
        var budget = new PrivateOwnerOperationDeadline(_clock, CancellationToken.None);
        string id = Check(payload);
        DeletionCapabilitySigningOutcome? uncertainty = null;
        try
        {
            budget.Check(); _stateIo.CheckReady();
            if (!(authority is not null && await budget.ReadAsync(() => authority.AuthorizeOperationAsync(payload, id, "SignDeletionCapability")).ConfigureAwait(false))) { return new(id, payload, DeletionCapabilitySigningState.Unavailable); }
            var result = await SignAsyncCoreAsync(payload, budget).ConfigureAwait(false);
            if (result.State == DeletionCapabilitySigningState.Unknown) { uncertainty = result; }
            if (!(authority is not null && await budget.ReadAsync(() => authority.AuthorizeOperationAsync(payload, id, "SignDeletionCapability")).ConfigureAwait(false))) { return new(id, payload, DeletionCapabilitySigningState.Unavailable); }
            budget.Check(); return result;

        }
        catch (TimeoutException) { return uncertainty ?? new(id, payload, DeletionCapabilitySigningState.Unavailable); }
    }
    private async Task<DeletionCapabilitySigningOutcome> SignAsyncCoreAsync(DeletionBatchCapabilityV1 payload, PrivateOwnerOperationDeadline budget)
    {
        string requestId = Check(payload);
        if (authority is null || !await budget.ReadAsync(() => authority.AuthorizeOperationAsync(payload, requestId, "SignDeletionCapability")).ConfigureAwait(false)) { return new(requestId, payload, DeletionCapabilitySigningState.Unavailable); }
        var existing = await ReadAsync(budget, payload, requestId, true).ConfigureAwait(false);
        if (existing is not null) { return await ResolveAsync(payload, requestId, existing, budget).ConfigureAwait(false); }
        if (authority is null || provider is null || trustProvider is null) { return new(requestId, payload, DeletionCapabilitySigningState.Unavailable); }
        if (!await budget.ReadAsync(() => authority.AuthorizeAsync(payload, requestId)).ConfigureAwait(false))
        { return await SaveAsync(budget, new(requestId, payload, DeletionCapabilitySigningState.Denied)).ConfigureAwait(false); }
        if (await ResolveTrustAsync(budget, payload).ConfigureAwait(false) is null) { return new(requestId, payload, DeletionCapabilitySigningState.Unavailable); }
        await SaveAsync(budget, new(requestId, payload, DeletionCapabilitySigningState.Unknown)).ConfigureAwait(false);
        if (!await budget.ReadAsync(() => authority.AuthorizeAsync(payload, requestId)).ConfigureAwait(false))
        { return await SaveAsync(budget, new(requestId, payload, DeletionCapabilitySigningState.Denied)).ConfigureAwait(false); }
        // No backend call has begun: missing post-intent trust is a known negative original,
        // not an uncertain signature. The independent journal admits this exact terminal outcome.
        DeletionCapabilityPublishedTrust? admittedTrust;
        try { admittedTrust = await ResolveTrustAsync(budget, payload).ConfigureAwait(false); }
        catch (Exception) { budget.Check(); admittedTrust = null; }
        if (admittedTrust is null) { return await SaveAsync(budget, new(requestId, payload, DeletionCapabilitySigningState.Denied)).ConfigureAwait(false); }
        try { return await RetainAsync(payload, requestId, await budget.ReadAsync(async () => CaptureProviderResult(await provider.SignAsync(payload, requestId).ConfigureAwait(false))).ConfigureAwait(false), budget).ConfigureAwait(false); }
        catch (TimeoutException) { return new(requestId, payload, DeletionCapabilitySigningState.Unknown); }
        catch (Exception) { budget.Check(); return new(requestId, payload, DeletionCapabilitySigningState.Unknown); }
    }
    /// <inheritdoc/>
    public async Task<DeletionCapabilitySigningOutcome> LookupAsync(DeletionBatchCapabilityV1 payload)
    {
        var budget = new PrivateOwnerOperationDeadline(_clock, CancellationToken.None);
        string id = Check(payload);
        DeletionCapabilitySigningOutcome? uncertainty = null;
        try
        {
            budget.Check(); _stateIo.CheckReady();
            if (!(authority is not null && await budget.ReadAsync(() => authority.AuthorizeOperationAsync(payload, id, "LookupDeletionCapability")).ConfigureAwait(false))) { return new(id, payload, DeletionCapabilitySigningState.Unavailable); }
            var result = await LookupAsyncCoreAsync(payload, budget).ConfigureAwait(false);
            if (result.State == DeletionCapabilitySigningState.Unknown) { uncertainty = result; }
            if (!(authority is not null && await budget.ReadAsync(() => authority.AuthorizeOperationAsync(payload, id, "LookupDeletionCapability")).ConfigureAwait(false))) { return new(id, payload, DeletionCapabilitySigningState.Unavailable); }
            budget.Check(); return result;

        }
        catch (TimeoutException) { return uncertainty ?? new(id, payload, DeletionCapabilitySigningState.Unavailable); }
    }
    private async Task<DeletionCapabilitySigningOutcome> LookupAsyncCoreAsync(DeletionBatchCapabilityV1 payload, PrivateOwnerOperationDeadline budget)
    {
        string requestId = Check(payload);
        if (authority is null || !await budget.ReadAsync(() => authority.AuthorizeOperationAsync(payload, requestId, "LookupDeletionCapability")).ConfigureAwait(false)) { return new(requestId, payload, DeletionCapabilitySigningState.Unavailable); }
        var existing = await ReadAsync(budget, payload, requestId).ConfigureAwait(false);
        return existing is null ? new(requestId, payload, DeletionCapabilitySigningState.Unavailable)
            : await ResolveAsync(payload, requestId, existing, budget).ConfigureAwait(false);
    }
    private string Check(DeletionBatchCapabilityV1 payload)
    {
        ArgumentNullException.ThrowIfNull(payload); string request = DeletionBatchCapabilityCodec.SigningRequestId(payload);
        if (Host.Id.GetId() != GetActorId(payload)) { throw new ArgumentException("Deletion signing scope mismatch.", nameof(payload)); }
        return request;
    }
    private async Task<DeletionCapabilitySigningOutcome> ResolveAsync(DeletionBatchCapabilityV1 payload, string id, DeletionCapabilitySigningOutcome existing, PrivateOwnerOperationDeadline budget)
    {
        if (existing.Payload != payload || existing.SigningRequestId != id) { return new(id, payload, DeletionCapabilitySigningState.Conflict); }
        if (existing.State is DeletionCapabilitySigningState.Signed or DeletionCapabilitySigningState.Denied
            or DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued) { return existing; }
        if (provider is null || trustProvider is null) { return new(id, payload, DeletionCapabilitySigningState.Unavailable); }
        try { return await RetainAsync(payload, id, await budget.ReadAsync(async () => CaptureProviderResult(await provider.LookupAsync(payload, id).ConfigureAwait(false))).ConfigureAwait(false), budget, recoveringOriginal: true).ConfigureAwait(false); }
        catch (TimeoutException) { return new(id, payload, DeletionCapabilitySigningState.Unknown); }
        catch (Exception) { budget.Check(); return new(id, payload, DeletionCapabilitySigningState.Unknown); }
    }
    private async Task<DeletionCapabilitySigningOutcome> RetainAsync(DeletionBatchCapabilityV1 payload, string id, DeletionCapabilitySigningResult result, PrivateOwnerOperationDeadline budget, bool recoveringOriginal = false)
    {
        budget.Check();
        if (result.Outcome is not { } outcome || outcome.Payload != payload || outcome.SigningRequestId != id)
        { return new(id, payload, DeletionCapabilitySigningState.Conflict); }
        if (outcome.State != DeletionCapabilitySigningState.Signed) { return new(id, payload, DeletionCapabilitySigningState.Unknown); }
        if (outcome.DetachedJws is not { Length: > 0 and <= 16384 } || string.IsNullOrWhiteSpace(outcome.PublicAnchorId)
            || outcome.PublicAnchorId.Length > 2048 || string.IsNullOrWhiteSpace(outcome.PublicAnchorVersion) || outcome.PublicAnchorVersion.Length > 2048
            || result.PublicAnchorSubjectPublicKeyInfo is not { Length: > 0 and <= 512 } supplied)
        { return new(id, payload, DeletionCapabilitySigningState.Unknown); }
        var trust = await ResolveTrustAsync(budget, payload, requireCurrent: false, allowRevokedHistory: recoveringOriginal).ConfigureAwait(false);
        if (trust is null || outcome.PublicAnchorId != trust.PublicAnchorId || outcome.PublicAnchorVersion != trust.PublicAnchorVersion
            || !CryptographicOperations.FixedTimeEquals(supplied, trust.SubjectPublicKeyInfo))
        { return new(id, payload, DeletionCapabilitySigningState.Unknown); }
        if (result.OriginalIssuanceReceiptId is { } receipt)
        {
            try { if (string.IsNullOrWhiteSpace(receipt) || receipt.Length > 2048 || new UTF8Encoding(false, true).GetByteCount(receipt) > 2048)
                { return new(id, payload, DeletionCapabilitySigningState.Unknown); } }
            catch (EncoderFallbackException) { return new(id, payload, DeletionCapabilitySigningState.Unknown); }
        }
        if (trust.IsRevoked && (originalReceiptAuthority is null || string.IsNullOrWhiteSpace(result.OriginalIssuanceReceiptId)
            || !await budget.ReadAsync(() => originalReceiptAuthority.VerifyOriginalIssuanceAsync(payload, id, result, trust)).ConfigureAwait(false)))
        { return new(id, payload, DeletionCapabilitySigningState.Unknown); }
        budget.Check();
        byte[] ownedAnchor = trust.SubjectPublicKeyInfo; using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(ownedAnchor, out int consumed);
        var profile = new DeletionCapabilityTrustProfile(payload.Issuer, payload.Audience, payload.TenantId, payload.CapabilityKeyVersion,
            trust.PublicAnchorId, trust.PublicAnchorVersion);
        if (consumed != ownedAnchor.Length || !DeletionBatchCapabilityCodec.Verify(payload, profile, outcome.DetachedJws, key))
        { return new(id, payload, DeletionCapabilitySigningState.Unknown); }
        budget.Check();
        return await SaveAsync(budget, outcome).ConfigureAwait(false);
    }
    // The abandoned provider worker owns only detached public result bytes, never actor state or journal work.
    private static DeletionCapabilitySigningResult CaptureProviderResult(DeletionCapabilitySigningResult result)
    {
        if (result.PublicAnchorSubjectPublicKeyInfo is { Length: > 512 }) { throw new FormatException("Signing result anchor exceeds carrier bound."); }
        return result with { PublicAnchorSubjectPublicKeyInfo = result.PublicAnchorSubjectPublicKeyInfo?.ToArray() };
    }
    private async Task<DeletionCapabilityPublishedTrust?> ResolveTrustAsync(PrivateOwnerOperationDeadline budget, DeletionBatchCapabilityV1 payload, bool requireCurrent = true, bool allowRevokedHistory = false)
    {
        if (trustProvider is null) { return null; }
        var trust = await budget.ReadAsync(() => trustProvider.ResolveAsync(payload.TenantId, KeyFamily, payload.CapabilityKeyVersion)).ConfigureAwait(false);
        if (trust is null || trust.IsRevoked && !allowRevokedHistory || requireCurrent && !trust.IsCurrentNonRevoked || trust.TrustProfileRevision <= 0 || trust.TenantId != payload.TenantId
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
    private async Task<DeletionCapabilitySigningOutcome?> ReadAsync(PrivateOwnerOperationDeadline budget, DeletionBatchCapabilityV1 payload, string id, bool recoverAdmittedOriginal = false)
    {
        await _stateIo.ReadAsync(budget, () => StateManager.ClearCacheAsync()).ConfigureAwait(false);
        var state = await _stateIo.ReadAsync(budget, () => StateManager.TryGetStateAsync<DeletionCapabilitySigningOutcome>(StateKey)).ConfigureAwait(false);
        if (authority is null) { throw new InvalidOperationException("Independent exact outcome authority is absent."); }
        return await RecoverableAnchoredState.ReconcileAsync(PendingScope, state.HasValue ? CaptureOutcome(state.Value, payload, id) : null,
            await ReadPendingAsync(budget).ConfigureAwait(false), value => value is null ? null : CaptureOutcome(value, payload, id),
            value => budget.ReadAsync(() => authority.ValidateStateAsync(payload, id, Digest(value))), new DeadlineAnchoredStateAuthority(authority, budget),
            value => value is null ? throw new InvalidOperationException("Null prospective outcome.") : PersistTargetAsync(budget, value), recoverAdmittedOriginal).ConfigureAwait(false);
    }
    private static DeletionCapabilitySigningOutcome CaptureOutcome(DeletionCapabilitySigningOutcome value, DeletionBatchCapabilityV1 payload, string id)
    {
        if (value.Payload != payload || value.SigningRequestId != id || DeletionBatchCapabilityCodec.SigningRequestId(value.Payload) != id
            || value.State is not (DeletionCapabilitySigningState.Unknown or DeletionCapabilitySigningState.Signed or DeletionCapabilitySigningState.Denied
                or DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued)
            || value.State is DeletionCapabilitySigningState.Signed or DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued && (string.IsNullOrWhiteSpace(value.DetachedJws)
                || value.DetachedJws.Length > 16384 || string.IsNullOrWhiteSpace(value.PublicAnchorId) || string.IsNullOrWhiteSpace(value.PublicAnchorVersion))
            || value.State is not (DeletionCapabilitySigningState.Signed or DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued)
                && (value.DetachedJws is not null || value.PublicAnchorId is not null || value.PublicAnchorVersion is not null)
            || value.State == DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued
                && (value.NoIssueProof is null || !DeletionCapabilitySigningSuccessor.Valid(value, value.NoIssueProof, value.NoIssueProof.ObservedAt))
            || value.State != DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued && value.NoIssueProof is not null)
        { throw new InvalidOperationException("Malformed persisted deletion signing outcome."); }
        return value;
    }
    private async Task<DeletionCapabilitySigningOutcome> SaveAsync(PrivateOwnerOperationDeadline budget, DeletionCapabilitySigningOutcome outcome)
    {
        var previous = await ReadAsync(budget, outcome.Payload, outcome.SigningRequestId).ConfigureAwait(false);
        if (authority is null) { throw new InvalidOperationException("Independent exact outcome authority is absent."); }
        long expected = previous is null ? 0 : previous.State == DeletionCapabilitySigningState.Unknown ? 1 : 2;
        var pending = RecoverableAnchoredState.Prepare(PendingScope, expected, checked(expected + 1), previous, outcome);
        if (!await RecoverableAnchoredState.CommitAsync(pending, new DeadlineAnchoredStateAuthority(authority, budget), () => ReadPendingAsync(budget), pendingValue => PersistPendingAsync(budget, pendingValue)).ConfigureAwait(false))
        { throw new InvalidOperationException("Independent exact outcome transition compare failed."); }
        var persisted = await ReadAsync(budget, outcome.Payload, outcome.SigningRequestId).ConfigureAwait(false);
        if (persisted != outcome) { throw new InvalidOperationException("Signing result is not confirmed durable."); }
        return outcome;
    }

    private string PendingScope => Host.Id.GetId() + "|" + StateKey;
    private const string PendingKey = StateKey + "-pending-transition-v1";
    private async Task<AnchoredStateTransition?> ReadPendingAsync(PrivateOwnerOperationDeadline budget)
    {
        await _stateIo.ReadAsync(budget, () => StateManager.ClearCacheAsync()).ConfigureAwait(false);
        var pending = await _stateIo.ReadAsync(budget, () => StateManager.TryGetStateAsync<AnchoredStateTransition>(PendingKey)).ConfigureAwait(false);
        return pending.HasValue ? pending.Value : null;
    }
    private async Task PersistPendingAsync(PrivateOwnerOperationDeadline budget, AnchoredStateTransition pending)
    {
        await _stateIo.ReadAsync(budget, () => StateManager.SetStateAsync(PendingKey, pending)).ConfigureAwait(false); await _stateIo.ReadAsync(budget, () => StateManager.SaveStateAsync()).ConfigureAwait(false);
    }
    private async Task<DeletionCapabilitySigningOutcome?> PersistTargetAsync(PrivateOwnerOperationDeadline budget, DeletionCapabilitySigningOutcome next)
    {
        await _stateIo.ReadAsync(budget, () => StateManager.SetStateAsync(StateKey, next)).ConfigureAwait(false);
        _ = await _stateIo.ReadAsync(budget, () => StateManager.TryRemoveStateAsync(PendingKey)).ConfigureAwait(false); await _stateIo.ReadAsync(budget, () => StateManager.SaveStateAsync()).ConfigureAwait(false);
        await _stateIo.ReadAsync(budget, () => StateManager.ClearCacheAsync()).ConfigureAwait(false);
        var confirmed = await _stateIo.ReadAsync(budget, () => StateManager.TryGetStateAsync<DeletionCapabilitySigningOutcome>(StateKey)).ConfigureAwait(false);
        return confirmed.HasValue ? confirmed.Value : throw new InvalidOperationException("Reconciled main state is missing.");
    }
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    /// <inheritdoc/>
    public async Task<DeletionCapabilitySigningOutcome> ObsoleteUnissuedAsync(DeletionBatchCapabilityV1 payload)
    {
        var budget = new PrivateOwnerOperationDeadline(_clock, CancellationToken.None);
        string id = Check(payload); var unavailable = new DeletionCapabilitySigningOutcome(id, payload, DeletionCapabilitySigningState.Unavailable);
        try
        {
            budget.Check(); _stateIo.CheckReady();
            if (authority is null || noIssueAuthority is null || !await budget.ReadAsync(() => authority.AuthorizeOperationAsync(payload, id, "ObsoleteUnissued")).ConfigureAwait(false)) { return unavailable; }
            var existing = await ReadAsync(budget, payload, id, true).ConfigureAwait(false);
            if (existing is null) { return unavailable; }
            if (existing.State == DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued)
            { return await budget.ReadAsync(() => authority.AuthorizeOperationAsync(payload, id, "ObsoleteUnissued")).ConfigureAwait(false) ? existing : unavailable; }
            // Unknown signer state is resolved only by original lookup; no signing retry occurs.
            existing = await ResolveAsync(payload, id, existing, budget).ConfigureAwait(false);
            if (existing.State != DeletionCapabilitySigningState.Signed) { return unavailable; }
            string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(existing.DetachedJws!)));
            var proof = await budget.ReadAsync(() => noIssueAuthority.ReadAsync(payload, id, digest)).ConfigureAwait(false);
            if (proof is null || !DeletionCapabilitySigningSuccessor.Valid(existing, proof, _clock.GetUtcNow())) { return unavailable; }
            var confirmed = await budget.ReadAsync(() => noIssueAuthority.ReadAsync(payload, id, digest)).ConfigureAwait(false);
            if (confirmed is null || confirmed with { ObservedAt = proof.ObservedAt } != proof
                || !DeletionCapabilitySigningSuccessor.Valid(existing, confirmed, _clock.GetUtcNow())
                || !await budget.ReadAsync(() => authority.AuthorizeOperationAsync(payload, id, "ObsoleteUnissued")).ConfigureAwait(false)) { return unavailable; }
            var obsolete = await SaveAsync(budget, existing with { State = DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued, NoIssueProof = proof }).ConfigureAwait(false);
            return await budget.ReadAsync(() => authority.AuthorizeOperationAsync(payload, id, "ObsoleteUnissued")).ConfigureAwait(false) && proof.ValidUntil > _clock.GetUtcNow() ? obsolete : unavailable;

        }
        catch (TimeoutException) { return unavailable; }
    }

    /// <inheritdoc/>
    public async Task<DeletionCapabilityNoIssueProof?> ReadSuccessorProofAsync(DeletionBatchCapabilityV1 payload)
    {
        var budget = new PrivateOwnerOperationDeadline(_clock, CancellationToken.None);
        string id = Check(payload);
        try
        {
            budget.Check(); _stateIo.CheckReady();
            if (authority is null || noIssueAuthority is null || !await budget.ReadAsync(() => authority.AuthorizeOperationAsync(payload, id, "RecoverSigningSuccessor")).ConfigureAwait(false)) { return null; }
            var original = await ReadAsync(budget, payload, id).ConfigureAwait(false);
            if (original?.State != DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued) { return null; }
            var proof = await budget.ReadAsync(() => noIssueAuthority.ReadAsync(payload, id, original.NoIssueProof!.DetachedJwsDigest)).ConfigureAwait(false);
            if (proof is null || DeletionCapabilitySigningSuccessor.Create(original, proof, _clock.GetUtcNow()) is null) { return null; }
            var final = await budget.ReadAsync(() => noIssueAuthority.ReadAsync(payload, id, original.NoIssueProof!.DetachedJwsDigest)).ConfigureAwait(false);
            if (final is null || final with { ObservedAt = proof.ObservedAt } != proof
                || !await budget.ReadAsync(() => authority.AuthorizeOperationAsync(payload, id, "RecoverSigningSuccessor")).ConfigureAwait(false)
                || DeletionCapabilitySigningSuccessor.Create(original, final, _clock.GetUtcNow()) is null) { return null; }
            return final;

        }
        catch (TimeoutException) { return null; }
    }

}
