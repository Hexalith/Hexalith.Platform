using Hexalith.EventStore.Contracts.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.Platform.Custody;

/// <summary>Actual same-tenant durable wrapped-object/hold/physical-outcome protocol. Raw key bytes never enter this actor.
/// Missing qualified physical fence/manifest/all-copy/restore backend/current private authority disables effects and lookup.
/// Production requires non-reentrant actor turns and an independently qualified durable state store.</summary>
/// <param name="host">Private same-tenant runtime actor.</param><param name="authority">Independent exact original lifecycle authority.</param>
/// <param name="provider">Qualified physical lifecycle owner.</param><param name="clock">Optional whole physical-operation clock; omission retains the system thirty-second operational bound.</param>
public sealed class CustodyKeyLifecycleActor(ActorHost host, ICustodyKeyLifecycleAuthority? authority = null, ICustodyKeyLifecycleProvider? provider = null,
    TimeProvider? clock = null) : Actor(host), ICustodyKeyLifecycleActor
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly PrivateActorStateIoLifetime _stateIo = new();
    private const string StateKey = "custody-key-lifecycle-candidate-v1";
    /// <summary>Gets exact private actor type.</summary>
    public const string ActorTypeName = "CustodyKeyLifecycleActor";
    /// <summary>Gets the shared tenant reservation/hold owner.</summary>
    public static string GetActorId(string tenant) => new AggregateIdentity(tenant, "custody", "key-lifecycle-v1").ActorId;
    /// <inheritdoc/>
    public async Task<CustodyKeyLifecycleOutcome> RegisterWrappedAsync(CustodyKeyRegistration registration)
    {
        var budget = new PrivateOwnerOperationDeadline(_clock, CancellationToken.None);
        Validate(registration); Check(registration.Identity); string digest = Digest(registration); var missing = Missing(registration.Identity, registration.OperationId, digest);
        try
        {
            budget.Check(); _stateIo.CheckReady();
            if (!await AdmitAsync(budget, registration.Identity, registration.OperationId, "RegisterWrappedKey", digest).ConfigureAwait(false)) { return missing; }
            var state = await ReadAsync(budget, registration.Identity.TenantId, true).ConfigureAwait(false); if (state is null) { return missing; }
            var prior = state.Keys.SingleOrDefault(k => SameObject(k.Registration.Identity, registration.Identity));
            if (prior is not null)
            {
                return prior.Registration == registration && await AdmitAsync(budget, registration.Identity, registration.OperationId, "RegisterWrappedKey", digest).ConfigureAwait(false)
                    ? missing with { Status = CustodyKeyLifecycleStatus.Wrapped, ReceiptId = prior.Registration.WrapReceiptId } : missing with { Status = CustodyKeyLifecycleStatus.Conflict };
            }
            if (state.Keys.Count >= 1000 || authority is null || !await budget.ReadAsync(() => authority.VerifyRegistrationAsync(registration)).ConfigureAwait(false)) { return missing; }
            var next = state with { Revision = checked(state.Revision + 1), Keys = state.Keys.Append(new CustodyKeyLifecycleEntry(registration, [], [], [])).ToArray() };
            if (registration.ExpectedRevision != state.Revision || !await SaveAsync(budget, state, next).ConfigureAwait(false)) { return missing; }
            return await AdmitAsync(budget, registration.Identity, registration.OperationId, "RegisterWrappedKey", digest).ConfigureAwait(false)
                ? missing with { Status = CustodyKeyLifecycleStatus.Wrapped, ReceiptId = registration.WrapReceiptId } : missing;

        }
        catch (TimeoutException) { return missing; }
    }
    /// <inheritdoc/>
    public async Task<CustodyKeyLifecycleOutcome> ApplyAsync(CustodyKeyLifecycleRequest request)
    {
        var budget = new PrivateOwnerOperationDeadline(_clock, CancellationToken.None);
        Validate(request); Check(request.Identity); string digest = Digest(request); var missing = Missing(request.Identity, request.OperationId, digest);
        CustodyKeyLifecycleOutcome? uncertainty = null;
        try
        {
            budget.Check(); _stateIo.CheckReady();
            if (!await AdmitAsync(budget, request.Identity, request.OperationId, "ApplyKeyLifecycle", digest).ConfigureAwait(false)) { return missing; }
            var result = await ApplyCoreAsync(request, digest, budget).ConfigureAwait(false);
            if (result.Status == CustodyKeyLifecycleStatus.Unknown) { uncertainty = result; }
            return await AdmitAsync(budget, request.Identity, request.OperationId, "ApplyKeyLifecycle", digest).ConfigureAwait(false) ? result : missing;

        }
        catch (TimeoutException) { return uncertainty ?? missing; }
    }
    private async Task<CustodyKeyLifecycleOutcome> ApplyCoreAsync(CustodyKeyLifecycleRequest request, string digest, PrivateOwnerOperationDeadline budget)
    {
        var missing = Missing(request.Identity, request.OperationId, digest); var state = await ReadAsync(budget, request.Identity.TenantId, true).ConfigureAwait(false);
        var key = state?.Keys.SingleOrDefault(k => k.Registration.Identity == request.Identity); if (state is null || key is null) { return missing; }
        var prior = key.Outcomes.SingleOrDefault(o => o.OperationId == request.OperationId);
        if (prior is not null) { return prior.RequestDigest == digest ? await ResolveAsync(state, key, request, prior, budget).ConfigureAwait(false) : missing with { Status = CustodyKeyLifecycleStatus.Conflict }; }
        if (provider is null || authority is null || key.Requests.Count >= 10000 || state.Keys.Sum(k => k.Requests.Count) >= 10000
            || request.ExpectedRevision != state.Revision || key.Outcomes.Any(o => o.Status is CustodyKeyLifecycleStatus.Unknown or CustodyKeyLifecycleStatus.Destroyed)
            || !await budget.ReadAsync(() => authority.AuthorizeEffectAsync(request)).ConfigureAwait(false)) { return missing; }
        if (request.Action == CustodyKeyLifecycleAction.Destroy && key.Pins.Count > 0) { return missing with { Status = CustodyKeyLifecycleStatus.BlockedByPin }; }
        if (request.Action == CustodyKeyLifecycleAction.Pin && key.Pins.Any(p => p.HoldId == request.HoldId)) { return missing with { Status = CustodyKeyLifecycleStatus.Conflict }; }
        if (request.Action == CustodyKeyLifecycleAction.Unpin && !key.Pins.Any(p => p.HoldId == request.HoldId && p.Identity == request.Identity)) { return missing with { Status = CustodyKeyLifecycleStatus.Conflict }; }
        var pending = missing with { Status = CustodyKeyLifecycleStatus.Unknown };
        var reserved = key with { Requests = key.Requests.Append(request).ToArray(), Outcomes = key.Outcomes.Append(pending).ToArray(),
            Pins = request.Action == CustodyKeyLifecycleAction.Pin ? key.Pins.Append(request).ToArray() : key.Pins };
        var next = Replace(state, reserved);
        if (!await SaveAsync(budget, state, next).ConfigureAwait(false)) { return missing; }
        // Only the owning physical backend consumes current linearizable control at its irreversible instant.
        // Missing/unknown result never permits a new physical call; retries use Lookup only.
        try
        {
            // Only physical invocation/await runs away from the actor turn. A late task has no actor-state continuation.
            var result = await budget.ReadAsync(() => provider.ExecuteAsync(key.Registration, request, digest)).ConfigureAwait(false);
            return await RetainAsync(next, reserved, request, pending, result, budget).ConfigureAwait(false);
        }
        catch (TimeoutException) { return pending; }
        catch (Exception) { budget.Check(); return pending; }
    }
    /// <inheritdoc/>
    public async Task<CustodyKeyLifecycleOutcome> LookupAsync(CustodyKeyLifecycleRequest request)
    {
        var budget = new PrivateOwnerOperationDeadline(_clock, CancellationToken.None);
        Validate(request); Check(request.Identity); string digest = Digest(request); var missing = Missing(request.Identity, request.OperationId, digest);
        CustodyKeyLifecycleOutcome? uncertainty = null;
        try
        {
            budget.Check(); _stateIo.CheckReady();
            if (!await AdmitAsync(budget, request.Identity, request.OperationId, "LookupKeyLifecycle", digest).ConfigureAwait(false)) { return missing; }
            var state = await ReadAsync(budget, request.Identity.TenantId).ConfigureAwait(false); var key = state?.Keys.SingleOrDefault(k => k.Registration.Identity == request.Identity);
            var prior = key?.Outcomes.SingleOrDefault(o => o.OperationId == request.OperationId);
            var result = state is not null && key is not null && prior?.RequestDigest == digest ? await ResolveAsync(state, key, request, prior, budget).ConfigureAwait(false) : missing;
            if (result.Status == CustodyKeyLifecycleStatus.Unknown) { uncertainty = result; }
            return await AdmitAsync(budget, request.Identity, request.OperationId, "LookupKeyLifecycle", digest).ConfigureAwait(false) ? result : missing;

        }
        catch (TimeoutException) { return uncertainty ?? missing; }
    }
    private async Task<CustodyKeyLifecycleOutcome> ResolveAsync(CustodyKeyLifecycleLedger state, CustodyKeyLifecycleEntry key, CustodyKeyLifecycleRequest request, CustodyKeyLifecycleOutcome original, PrivateOwnerOperationDeadline budget)
    {
        if (original.Status != CustodyKeyLifecycleStatus.Unknown) { return original; }
        if (provider is null || authority is null) { return original; }
        try
        {
            var result = await budget.ReadAsync(() => provider.LookupAsync(key.Registration, request, original.RequestDigest)).ConfigureAwait(false);
            return await RetainAsync(state, key, request, original, result, budget).ConfigureAwait(false);
        }
        catch (TimeoutException) { return original; }
        catch (Exception) { budget.Check(); return original; }
    }
    private async Task<CustodyKeyLifecycleOutcome> RetainAsync(CustodyKeyLifecycleLedger state, CustodyKeyLifecycleEntry key, CustodyKeyLifecycleRequest request,
        CustodyKeyLifecycleOutcome pending, CustodyKeyLifecycleOutcome result, PrivateOwnerOperationDeadline budget)
    {
        budget.Check();
        var expected = request.Action switch { CustodyKeyLifecycleAction.Pin => CustodyKeyLifecycleStatus.Pinned, CustodyKeyLifecycleAction.Unpin => CustodyKeyLifecycleStatus.Unpinned, _ => CustodyKeyLifecycleStatus.Destroyed };
        if (result is null || result.Identity != request.Identity || result.OperationId != request.OperationId || result.RequestDigest != pending.RequestDigest
            || result.Status != expected && result.Status != CustodyKeyLifecycleStatus.NotPerformed || !ValidText(result.ReceiptId)
            || result.Status == CustodyKeyLifecycleStatus.Destroyed && !ValidText(result.RestoreBarrierReceiptId)
            || result.Status != CustodyKeyLifecycleStatus.Destroyed && result.RestoreBarrierReceiptId is not null || authority is null
            || !await budget.ReadAsync(() => authority.VerifyOutcomeAsync(request, result)).ConfigureAwait(false)) { return pending; }
        budget.Check();
        var pins = key.Pins;
        if (request.Action == CustodyKeyLifecycleAction.Pin && result.Status == CustodyKeyLifecycleStatus.NotPerformed)
        { pins = pins.Where(p => p.OperationId != request.OperationId).ToArray(); }
        if (request.Action == CustodyKeyLifecycleAction.Unpin && result.Status == CustodyKeyLifecycleStatus.Unpinned)
        { pins = pins.Where(p => p.HoldId != request.HoldId).ToArray(); }
        var retained = key with { Pins = pins, Outcomes = key.Outcomes.Select(o => o.OperationId == result.OperationId ? result : o).ToArray() };
        return await SaveAsync(budget, state, Replace(state, retained)).ConfigureAwait(false) ? result : pending;
    }
    private Task<bool> AdmitAsync(PrivateOwnerOperationDeadline budget, CustodyKeyObjectIdentity identity, string id, string method, string digest)
        => authority is null ? Task.FromResult(false) : budget.ReadAsync(() => authority.AuthorizeOperationAsync(identity, id, method, digest));
    private async Task<CustodyKeyLifecycleLedger?> ReadAsync(PrivateOwnerOperationDeadline budget, string tenant, bool recoverAdmittedOriginal = false)
    {
        await _stateIo.ReadAsync(budget, () => StateManager.ClearCacheAsync()).ConfigureAwait(false); var read = await _stateIo.ReadAsync(budget, () => StateManager.TryGetStateAsync<CustodyKeyLifecycleLedger>(StateKey)).ConfigureAwait(false);
        var state = read.HasValue ? Capture(read.Value) : new(tenant, 0, []);
        if (state.TenantId != tenant || authority is null) { return null; }
        try
        {
            return await RecoverableAnchoredState.ReconcileAsync(PendingScope, state, await ReadPendingAsync(budget).ConfigureAwait(false), Capture,
                value => budget.ReadAsync(() => authority.ValidateStateAsync(tenant, value.Revision, Digest(value))), new DeadlineAnchoredStateAuthority(authority, budget), nextValue => PersistTargetAsync(budget, nextValue), recoverAdmittedOriginal).ConfigureAwait(false);
        }
        catch (InvalidOperationException) { return null; }
    }
    private async Task<bool> SaveAsync(PrivateOwnerOperationDeadline budget, CustodyKeyLifecycleLedger previous, CustodyKeyLifecycleLedger prospective)
    {
        var next = Capture(prospective);
        if (authority is null) { return false; }
        var pending = RecoverableAnchoredState.Prepare(PendingScope, previous.Revision, next.Revision, previous, next);
        if (!await RecoverableAnchoredState.CommitAsync(pending, new DeadlineAnchoredStateAuthority(authority, budget), () => ReadPendingAsync(budget), pendingValue => PersistPendingAsync(budget, pendingValue)).ConfigureAwait(false)) { return false; }
        var confirmed = await ReadAsync(budget, next.TenantId).ConfigureAwait(false); return confirmed is not null && Digest(confirmed) == Digest(next);
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
    private async Task<CustodyKeyLifecycleLedger> PersistTargetAsync(PrivateOwnerOperationDeadline budget, CustodyKeyLifecycleLedger next)
    {
        await _stateIo.ReadAsync(budget, () => StateManager.SetStateAsync(StateKey, next)).ConfigureAwait(false);
        _ = await _stateIo.ReadAsync(budget, () => StateManager.TryRemoveStateAsync(PendingKey)).ConfigureAwait(false); await _stateIo.ReadAsync(budget, () => StateManager.SaveStateAsync()).ConfigureAwait(false);
        await _stateIo.ReadAsync(budget, () => StateManager.ClearCacheAsync()).ConfigureAwait(false);
        var confirmed = await _stateIo.ReadAsync(budget, () => StateManager.TryGetStateAsync<CustodyKeyLifecycleLedger>(StateKey)).ConfigureAwait(false);
        return confirmed.HasValue ? confirmed.Value : throw new InvalidOperationException("Reconciled main state is missing.");
    }
    private static CustodyKeyLifecycleLedger Replace(CustodyKeyLifecycleLedger state, CustodyKeyLifecycleEntry key) => state with { Revision = checked(state.Revision + 1),
        Keys = state.Keys.Select(k => k.Registration.Identity == key.Registration.Identity ? key : k).ToArray() };
    private static CustodyKeyLifecycleLedger Capture(CustodyKeyLifecycleLedger source)
    {
        Text(source.TenantId); if (source.Revision < 0 || source.Keys is null || source.Keys.Count > 1000) { throw new InvalidOperationException("Malformed bounded key lifecycle ledger."); }
        var keys = new List<CustodyKeyLifecycleEntry>(); int operations = 0;
        foreach (var key in source.Keys)
        {
            if (keys.Count >= 1000 || key is null) { throw new InvalidOperationException("Malformed bounded key object."); } Validate(key.Registration);
            if (key.Registration.Identity.TenantId != source.TenantId || keys.Any(k => SameObject(k.Registration.Identity, key.Registration.Identity))) { throw new InvalidOperationException("Conflicting key object."); }
            var requests = CaptureList(key.Requests); var pins = CaptureList(key.Pins); var outcomes = CaptureList(key.Outcomes); operations = checked(operations + requests.Length);
            if (operations > 10000 || outcomes.Length != requests.Length || requests.Select(r => r.OperationId).Distinct(StringComparer.Ordinal).Count() != requests.Length) { throw new InvalidOperationException("Malformed bounded original operations."); }
            foreach (var r in requests) { Validate(r); if (r.Identity != key.Registration.Identity) { throw new InvalidOperationException("Foreign original key operation."); } }
            foreach (var o in outcomes)
            {
                var r = requests.SingleOrDefault(r => r.OperationId == o.OperationId);
                if (r is null || o.Identity != key.Registration.Identity || o.RequestDigest != Digest(r) || o.Status is not (CustodyKeyLifecycleStatus.Unknown or CustodyKeyLifecycleStatus.Pinned or CustodyKeyLifecycleStatus.Unpinned or CustodyKeyLifecycleStatus.Destroyed or CustodyKeyLifecycleStatus.NotPerformed)
                    || o.Status == CustodyKeyLifecycleStatus.Unknown && (o.ReceiptId is not null || o.RestoreBarrierReceiptId is not null)
                    || o.Status != CustodyKeyLifecycleStatus.Unknown && !ValidText(o.ReceiptId) || o.Status == CustodyKeyLifecycleStatus.Destroyed && !ValidText(o.RestoreBarrierReceiptId))
                { throw new InvalidOperationException("Malformed original physical outcome."); }
            }
            if (outcomes.Select(o => o.OperationId).Distinct(StringComparer.Ordinal).Count() != outcomes.Length || pins.Any(p => p.Action != CustodyKeyLifecycleAction.Pin || !requests.Contains(p))
                || pins.Select(p => p.HoldId).Distinct(StringComparer.Ordinal).Count() != pins.Length || outcomes.Any(o => o.Status == CustodyKeyLifecycleStatus.Destroyed) && pins.Length != 0)
            { throw new InvalidOperationException("Malformed restrictive key state."); }
            keys.Add(key with { Requests = Array.AsReadOnly(requests), Pins = Array.AsReadOnly(pins), Outcomes = Array.AsReadOnly(outcomes) });
        }
        if (source.Revision < keys.Count) { throw new InvalidOperationException("Malformed key lifecycle revision."); }
        return source with { Keys = Array.AsReadOnly(keys.ToArray()) };
    }
    private static T[] CaptureList<T>(IReadOnlyList<T> source)
    { if (source is null || source.Count is < 0 or > 10000) { throw new InvalidOperationException("Malformed bounded key list."); } var owned = new List<T>();
        foreach (var item in source) { if (owned.Count >= 10000 || item is null) { throw new InvalidOperationException("Malformed bounded key list."); } owned.Add(item); } return owned.ToArray(); }
    /// <summary>Validates complete allowed wrapped key purpose/phase identity before cryptography/registration.</summary>
    internal static void ValidateIdentity(CustodyKeyObjectIdentity i)
    { ArgumentNullException.ThrowIfNull(i); foreach (string s in new[] { i.TenantId, i.ObjectId, i.KeyAlias, i.KeyVersion, i.LifecycleVersion, i.StoreTarget, i.ContractVersion }) { Text(s); }
        if (i.Purpose is not (PlatformKeyPurpose.ExportEnvelopeKey or PlatformKeyPurpose.InteractionRootDek)) { throw new ArgumentException("Unsupported wrapped-key purpose."); } }
    private static void Validate(CustodyKeyRegistration r)
    { ArgumentNullException.ThrowIfNull(r); ValidateIdentity(r.Identity); foreach (string s in new[] { r.OperationId, r.KekVersion, r.OpaqueWrappedKeyReference, r.WrapReceiptId }) { Text(s); } if (r.ExpectedRevision < 0) { throw new ArgumentException("Invalid wrap revision."); } }
    private static void Validate(CustodyKeyLifecycleRequest r)
    { ArgumentNullException.ThrowIfNull(r); ValidateIdentity(r.Identity); foreach (string s in new[] { r.OperationId, r.FenceTokenId, r.DecisionVersion }) { Text(s); }
        if (!Enum.IsDefined(r.Action) || r.ExpectedRevision < 0 || r.FenceRevision <= 0 || (r.Action == CustodyKeyLifecycleAction.Destroy) != (r.HoldId is null)) { throw new ArgumentException("Invalid complete key lifecycle request."); }
        if (r.HoldId is not null) { Text(r.HoldId); }
        if (r.Action == CustodyKeyLifecycleAction.Destroy && r.Identity.Purpose == PlatformKeyPurpose.InteractionRootDek) { Text(r.ProtectionReservationReceiptId!); }
        else if (r.ProtectionReservationReceiptId is not null) { throw new ArgumentException("Unexpected root reservation context."); } }
    private void Check(CustodyKeyObjectIdentity i) { ValidateIdentity(i); if (Host.Id.GetId() != GetActorId(i.TenantId)) { throw new ArgumentException("Key lifecycle tenant mismatch."); } }
    private static bool SameObject(CustodyKeyObjectIdentity a, CustodyKeyObjectIdentity b) => a.TenantId == b.TenantId && a.ObjectId == b.ObjectId && a.Purpose == b.Purpose && a.KeyAlias == b.KeyAlias;
    private static CustodyKeyLifecycleOutcome Missing(CustodyKeyObjectIdentity i, string id, string digest) => new(i, id, digest, CustodyKeyLifecycleStatus.Unavailable);
    private static void Text(string s) { if (!ValidText(s)) { throw new ArgumentException("Malformed key lifecycle identity."); } }
    private static bool ValidText(string? s) { try { return !string.IsNullOrWhiteSpace(s) && s.Length <= 2048 && new UTF8Encoding(false, true).GetByteCount(s) <= 2048; } catch (EncoderFallbackException) { return false; } }
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
