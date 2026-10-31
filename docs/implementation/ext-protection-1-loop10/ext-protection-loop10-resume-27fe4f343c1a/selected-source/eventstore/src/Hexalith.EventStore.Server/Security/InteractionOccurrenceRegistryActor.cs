using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Actual private DAPR candidate registry. No plaintext/keys; missing independent monotonic/root/writer authority fails closed.</summary>
/// <param name="host">Exact tenant registry actor; non-reentrant production turns required.</param>
/// <param name="authority">Independently governed current operation and antirollback/writer authority.</param>
public sealed class InteractionOccurrenceRegistryActor(ActorHost host, IInteractionOccurrenceAuthority? authority = null) : Actor(host), IInteractionOccurrenceRegistryActor
{
    private const string StateKey = "candidate-interaction-occurrences-v1";
    /// <summary>Gets candidate private registration name.</summary>
    public const string ActorTypeName = "InteractionOccurrenceRegistryActor";
    /// <summary>Gets one tenant owner for reference uniqueness across all interaction aliases.</summary>
    public static string GetActorId(string tenantId) => new AggregateIdentity(tenantId, "protection", "candidate-interaction-occurrences-v1").ActorId;
    /// <inheritdoc/>
    public async Task<InteractionOccurrenceReservationResult> ReserveAsync(InteractionOccurrenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); Check(request.Identity); Text(request.DigestKeyVersion); Reference(request.ProposedKeyReference);
        if (request.ReservationAttemptOrdinal <= 0 || request.ContentIntentHmac is not { Length: 64 } || request.ContentIntentHmac.Any(c => !char.IsAsciiHexDigit(c))) { throw new ArgumentException("Invalid keyed fingerprint.", nameof(request)); }
        if (!await AdmitAsync(request.Identity, "Reserve").ConfigureAwait(false)) { return Unavailable(); }
        var state = await ReadAsync(request.Identity.Target.TenantId, true).ConfigureAwait(false);
        if (state is null) { return Unavailable(); }
        var existing = Find(state, request.Identity);
        if (existing is not null)
        {
            if (existing.Request.Identity != request.Identity || existing.Request.DigestKeyVersion != request.DigestKeyVersion || existing.Request.ContentIntentHmac != request.ContentIntentHmac)
            { return new(InteractionOccurrenceReservationStatus.Conflict, null); }
            if (request.ReservationAttemptOrdinal == existing.Request.ReservationAttemptOrdinal) { return await ReleaseAsync(request.Identity, "Reserve", existing).ConfigureAwait(false); }
            if (existing.WriterState != InteractionOccurrenceWriterState.Aborted || request.ReservationAttemptOrdinal != checked(existing.Request.ReservationAttemptOrdinal + 1))
            { return new(InteractionOccurrenceReservationStatus.Conflict, null); }
        }
        if (existing is null && request.ReservationAttemptOrdinal != 1) { return new(InteractionOccurrenceReservationStatus.Conflict, null); }
        if (state.Records.Any(r => r.KeyReference == request.ProposedKeyReference)) { return new(InteractionOccurrenceReservationStatus.ReferenceCollision, null); }
        if (state.Records.Count >= 10000) { return Unavailable(); }
        if (authority is null || !await authority.AuthorizeReservationAsync(request).ConfigureAwait(false)) { return Unavailable(); }
        long revision = checked(state.Revision + 1);
        var record = new InteractionOccurrenceRecord(request, request.ProposedKeyReference, revision, InteractionOccurrenceWriterState.Reserved, null, null);
        await SaveAsync(state with { Revision = revision, Records = state.Records.Append(record).ToArray() }, state.Revision).ConfigureAwait(false);
        return await ReleaseAsync(request.Identity, "Reserve", record).ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public async Task<InteractionOccurrenceReservationResult> RetainSealedAsync(InteractionOccurrenceIdentity identity, InteractionOccurrenceSealedResult sealedResult)
    {
        Check(identity); ArgumentNullException.ThrowIfNull(sealedResult);
        if (!await AdmitAsync(identity, "RetainSealed").ConfigureAwait(false)) { return Unavailable(); }
        var state = await ReadAsync(identity.Target.TenantId, true).ConfigureAwait(false); if (state is null) { return Unavailable(); }
        var record = Find(state, identity); if (record is null || record.Request.Identity != identity || record.WriterState == InteractionOccurrenceWriterState.Aborted) { return Unavailable(); }
        var owned = CaptureSealed(sealedResult);
        if (owned.KeyReference != record.KeyReference) { return new(InteractionOccurrenceReservationStatus.Conflict, null); }
        if (record.Sealed is not null) { return Digest(record.Sealed) == Digest(owned) ? await ReleaseAsync(identity, "RetainSealed", record).ConfigureAwait(false) : new(InteractionOccurrenceReservationStatus.Conflict, null); }
        if (authority is null || !await authority.VerifySealedAsync(record, owned).ConfigureAwait(false)) { return Unavailable(); }
        var next = record with { RegistryRevision = checked(state.Revision + 1), WriterState = InteractionOccurrenceWriterState.SealedPending, Sealed = owned };
        await SaveAsync(Replace(state with { Revision = next.RegistryRevision }, next), state.Revision).ConfigureAwait(false);
        return await ReleaseAsync(identity, "RetainSealed", next).ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public async Task<InteractionOccurrenceReservationResult> CompleteWriterAsync(InteractionOccurrenceIdentity identity, string keyReference, string proofId, bool persisted)
    {
        Check(identity); Reference(keyReference); Text(proofId); if (!await AdmitAsync(identity, "CompleteWriter").ConfigureAwait(false)) { return Unavailable(); }
        var state = await ReadAsync(identity.Target.TenantId, true).ConfigureAwait(false); if (state is null) { return Unavailable(); }
        var record = Find(state, identity); if (record is null || record.Request.Identity != identity || record.KeyReference != keyReference) { return Unavailable(); }
        if (record.WriterState is InteractionOccurrenceWriterState.Active or InteractionOccurrenceWriterState.Aborted)
        { return record.WriterProofId == proofId && (record.WriterState == InteractionOccurrenceWriterState.Active) == persisted ? await ReleaseAsync(identity, "CompleteWriter", record).ConfigureAwait(false) : new(InteractionOccurrenceReservationStatus.Conflict, null); }
        if (persisted && record.Sealed is null || authority is null || !await authority.VerifyWriterAsync(record, proofId, persisted).ConfigureAwait(false)) { return Unavailable(); }
        var next = record with { RegistryRevision = checked(state.Revision + 1), WriterState = persisted ? InteractionOccurrenceWriterState.Active : InteractionOccurrenceWriterState.Aborted, WriterProofId = proofId };
        await SaveAsync(Replace(state with { Revision = next.RegistryRevision }, next), state.Revision).ConfigureAwait(false);
        return await ReleaseAsync(identity, "CompleteWriter", next).ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public async Task<InteractionOccurrenceReservationResult> LookupAsync(InteractionOccurrenceIdentity identity)
    {
        Check(identity); if (!await AdmitAsync(identity, "Lookup").ConfigureAwait(false)) { return Unavailable(); }
        var state = await ReadAsync(identity.Target.TenantId).ConfigureAwait(false); var record = state is null ? null : Find(state, identity);
        return record is null || record.Request.Identity != identity ? Unavailable() : await ReleaseAsync(identity, "Lookup", record).ConfigureAwait(false);
    }
    private async Task<InteractionOccurrenceReservationResult> ReleaseAsync(InteractionOccurrenceIdentity identity, string method, InteractionOccurrenceRecord record)
    {
        if (!await AdmitAsync(identity, method).ConfigureAwait(false)) { return Unavailable(); }
        return new(record.WriterState == InteractionOccurrenceWriterState.Aborted ? InteractionOccurrenceReservationStatus.Unavailable
            : record.Sealed is null ? InteractionOccurrenceReservationStatus.Reserved : InteractionOccurrenceReservationStatus.Sealed,
            record.WriterState == InteractionOccurrenceWriterState.Aborted ? null : record with { Sealed = record.Sealed is null ? null : CaptureSealed(record.Sealed) });
    }
    private async Task<bool> AdmitAsync(InteractionOccurrenceIdentity identity, string method) => authority is not null
        && await authority.AuthorizeOperationAsync(identity, method).ConfigureAwait(false);
    private async Task<InteractionOccurrenceRegistrySnapshot?> ReadAsync(string tenant, bool recoverAdmittedOriginal = false)
    {
        await StateManager.ClearCacheAsync().ConfigureAwait(false); var stored = await StateManager.TryGetStateAsync<InteractionOccurrenceRegistrySnapshot>(StateKey).ConfigureAwait(false);
        if (authority is null) { return null; }
        string? epoch = stored.HasValue ? stored.Value.EpochId : await authority.GetInstalledEpochAsync(tenant).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(epoch)) { return null; } Text(epoch);
        var state = stored.HasValue ? CaptureState(stored.Value) : new(tenant, epoch, 0, []);
        if (state.TenantId != tenant) { return null; }
        try
        {
        if (!await authority.ValidateStateAsync(tenant, epoch, state.Revision, Digest(state)).ConfigureAwait(false))
        {
            var original = await authority.ReadCurrentOriginalAsync(PendingScope).ConfigureAwait(false);
            if (original is not null && original.ScopeId == PendingScope && original.TargetRevision > state.Revision
                && original.TargetBytes is { Length: > 0 and <= RecoverableAnchoredState.MaximumPendingBytes }
                && Convert.ToHexString(SHA256.HashData(original.TargetBytes)) == original.TargetDigest)
            {
                InteractionOccurrenceRegistrySnapshot? recovered = JsonSerializer.Deserialize<InteractionOccurrenceRegistrySnapshot>(original.TargetBytes);
                ArgumentNullException.ThrowIfNull(recovered);
                var current = CaptureState(recovered);
                if (current.TenantId == tenant && current.EpochId == epoch && current.Revision == original.TargetRevision && Digest(current) == original.TargetDigest
                    && await authority.ValidateStateAsync(tenant, epoch, current.Revision, original.TargetDigest).ConfigureAwait(false))
                {
                    state = await PersistTargetAsync(current).ConfigureAwait(false);
                    if (!await authority.ValidateStateAsync(tenant, epoch, state.Revision, Digest(state)).ConfigureAwait(false)) { return null; }
                }
            }
        }
        return await RecoverableAnchoredState.ReconcileAsync(PendingScope, state, await ReadPendingAsync().ConfigureAwait(false), CaptureState,
            value => authority.ValidateStateAsync(tenant, epoch, value.Revision, Digest(value)), authority, PersistTargetAsync, recoverAdmittedOriginal).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or ArgumentNullException) { return null; }
    }
    private async Task SaveAsync(InteractionOccurrenceRegistrySnapshot next, long expected)
    {
        _ = CaptureState(next); // Validate before advancing the independent anchor or staging provider state.
        if (authority is null) { throw new InvalidOperationException("Independent registry authority is unavailable."); }
        var previous = await ReadAsync(next.TenantId).ConfigureAwait(false);
        if (previous is null || previous.Revision != expected) { throw new InvalidOperationException("Registry comparison changed."); }
        var pending = RecoverableAnchoredState.Prepare(PendingScope, expected, next.Revision, previous, next);
        if (!await RecoverableAnchoredState.CommitAsync(pending, authority, ReadPendingAsync, PersistPendingAsync).ConfigureAwait(false))
        { throw new InvalidOperationException("Independent registry transition is unavailable or stale."); }
        var observed = await ReadAsync(next.TenantId).ConfigureAwait(false);
        if (observed is null || Digest(observed) != Digest(next)) { throw new InvalidOperationException("Occurrence registry result is not confirmed durable."); }
    }

    private string PendingScope => Host.Id.GetId() + "|" + StateKey;
    private const string PendingKey = StateKey + "-pending-transition-v1";
    private async Task<AnchoredStateTransition?> ReadPendingAsync()
    {
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var pending = await StateManager.TryGetStateAsync<AnchoredStateTransition>(PendingKey).ConfigureAwait(false);
        return pending.HasValue ? pending.Value : null;
    }
    private async Task PersistPendingAsync(AnchoredStateTransition pending)
    {
        await StateManager.SetStateAsync(PendingKey, pending).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }
    private async Task<InteractionOccurrenceRegistrySnapshot> PersistTargetAsync(InteractionOccurrenceRegistrySnapshot next)
    {
        await StateManager.SetStateAsync(StateKey, next).ConfigureAwait(false);
        _ = await StateManager.TryRemoveStateAsync(PendingKey).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var confirmed = await StateManager.TryGetStateAsync<InteractionOccurrenceRegistrySnapshot>(StateKey).ConfigureAwait(false);
        return confirmed.HasValue ? confirmed.Value : throw new InvalidOperationException("Reconciled main state is missing.");
    }
    private InteractionOccurrenceRegistrySnapshot CaptureState(InteractionOccurrenceRegistrySnapshot state)
    {
        if (state.Revision <= 0 || state.Records is null || state.Records.Count > 10000) { throw new InvalidOperationException("Malformed candidate registry."); }
        var records = new List<InteractionOccurrenceRecord>();
        long sealedBytes = 0;
        foreach (var record in state.Records)
        {
            if (records.Count >= 10000 || record is null) { throw new InvalidOperationException("Malformed candidate registry."); }
            Check(record.Request.Identity); Reference(record.KeyReference); Text(record.Request.DigestKeyVersion);
            if (record.RegistryRevision <= 0 || record.RegistryRevision > state.Revision || record.KeyReference != record.Request.ProposedKeyReference
                || record.Request.ReservationAttemptOrdinal <= 0 || record.Request.ContentIntentHmac is not { Length: 64 } || record.Request.ContentIntentHmac.Any(c => !char.IsAsciiHexDigit(c))
                || !Enum.IsDefined(record.WriterState) || record.WriterState == InteractionOccurrenceWriterState.Reserved && record.Sealed is not null
                || record.WriterState is InteractionOccurrenceWriterState.SealedPending or InteractionOccurrenceWriterState.Active && record.Sealed is null
                || record.WriterState is InteractionOccurrenceWriterState.Active or InteractionOccurrenceWriterState.Aborted && string.IsNullOrWhiteSpace(record.WriterProofId)
                || record.WriterState is InteractionOccurrenceWriterState.Reserved or InteractionOccurrenceWriterState.SealedPending && record.WriterProofId is not null)
            { throw new InvalidOperationException("Malformed candidate occurrence."); }
            if (record.Sealed is { } carrier)
            {
                // The existing admitted pending carrier also bounds aggregate transient ciphertext copies.
                // Check before taking another detached copy, including restored states lacking an anchor.
                sealedBytes = checked(sealedBytes + (carrier.PayloadBytes?.LongLength ?? 0));
                if (sealedBytes > RecoverableAnchoredState.MaximumPendingBytes)
                { throw new InvalidOperationException("Candidate registry aggregate ciphertext bound exceeded."); }
            }
            records.Add(record with { Sealed = record.Sealed is null ? null : CaptureSealed(record.Sealed) });
        }
        if (records.Select(r => r.KeyReference).Distinct(StringComparer.Ordinal).Count() != records.Count
            || records.Select(r => Slot(r.Request.Identity) + ":" + r.Request.ReservationAttemptOrdinal).Distinct(StringComparer.Ordinal).Count() != records.Count)
        { throw new InvalidOperationException("Duplicate candidate registry identity."); }
        foreach (var group in records.GroupBy(r => Slot(r.Request.Identity)))
        {
            var ordered = group.OrderBy(r => r.Request.ReservationAttemptOrdinal).ToArray();
            if (ordered.Where((r, i) => r.Request.ReservationAttemptOrdinal != i + 1
                || i < ordered.Length - 1 && r.WriterState != InteractionOccurrenceWriterState.Aborted).Any())
            { throw new InvalidOperationException("Candidate occurrence attempt gap or nonterminal predecessor."); }
        }
        return state with { Records = Array.AsReadOnly(records.ToArray()) };
    }
    private void Check(InteractionOccurrenceIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity); ArgumentNullException.ThrowIfNull(identity.Target); ArgumentNullException.ThrowIfNull(identity.Owner);
        foreach (string value in new[] { identity.Target.TenantId, identity.Target.AgentInteractionId, identity.Target.TargetProtectionKeyAlias, identity.PayloadTypeId,
            identity.RootKeyVersion, identity.DerivationProfileVersion }) { Text(value); }
        if (identity.Target.TenantId != identity.Owner.TenantId || identity.Sequence == 0 || !Enum.IsDefined(identity.Kind)
            || identity.DerivationProfileVersion != "candidate-hkdf-sha256-v1" || Host.Id.GetId() != GetActorId(identity.Target.TenantId))
        { throw new ArgumentException("Invalid occurrence scope.", nameof(identity)); }
    }
    private static InteractionOccurrenceSealedResult CaptureSealed(InteractionOccurrenceSealedResult value)
    {
        Reference(value.KeyReference);
        if (value.PayloadBytes is not { Length: > 0 and <= 16777216 } || value.SerializationFormat != "json+pdenc-v2" || value.ProtectedPathCount is < 1 or > 4096)
        { throw new ArgumentException("Invalid candidate sealed result.", nameof(value)); }
        if (value.SnapshotReplaySkeleton is { Length: > 16777216 }) { throw new ArgumentException("Bounded snapshot skeleton required."); }
        return value with { PayloadBytes = value.PayloadBytes.ToArray(), SnapshotReplaySkeleton = value.SnapshotReplaySkeleton?.ToArray() };
    }
    private static void Reference(string value)
    {
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        if (value is null || value.Length != 26 || value[0] is < '0' or > '7' || value.Any(c => !alphabet.Contains(c))) { throw new ArgumentException("Invalid occurrence key reference."); }
    }
    private static void Text(string value)
    { if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || new UTF8Encoding(false, true).GetByteCount(value) > 2048) { throw new ArgumentException("Invalid occurrence identity."); } }
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static string Slot(InteractionOccurrenceIdentity i) => Digest(new object[] { i.Target, i.Owner, i.Kind, i.Sequence, i.PayloadTypeId });
    private static InteractionOccurrenceRecord? Find(InteractionOccurrenceRegistrySnapshot state, InteractionOccurrenceIdentity identity) => state.Records.Where(r => Slot(r.Request.Identity) == Slot(identity)).OrderByDescending(r => r.Request.ReservationAttemptOrdinal).FirstOrDefault();
    private static InteractionOccurrenceRegistrySnapshot Replace(InteractionOccurrenceRegistrySnapshot state, InteractionOccurrenceRecord record) => state with {
        Records = state.Records.Select(r => r.KeyReference == record.KeyReference ? record : r).ToArray() };
    private static InteractionOccurrenceReservationResult Unavailable() => new(InteractionOccurrenceReservationStatus.Unavailable, null);
}
