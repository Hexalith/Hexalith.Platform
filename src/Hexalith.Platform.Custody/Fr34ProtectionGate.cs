using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Fresh FR-34 challenge at every evaluation; no cached success, self-reported qualification or no-op fallback.</summary>
/// <param name="target">Independently committed exact target.</param><param name="engine">Actual production canary adapter.</param>
/// <param name="storage">Independent persisted-byte inspection.</param><param name="authority">Independent current target/destruction authority.</param>
/// <param name="clock">Whole evaluation budget and exclusive authority clock.</param>
public sealed class Fr34ProtectionGate(Fr34ProtectionTarget target, IFr34ProtectionCanaryEngine engine,
    IFr34PersistedCanaryReader storage, IFr34CanaryAuthority authority, TimeProvider clock)
{
    /// <summary>Completion of scheduled private cleanup; only the friend test assembly observes this task.</summary>
    internal Task CleanupCompletion { get; private set; } = Task.CompletedTask;
    /// <summary>Performs seal, independent persisted-byte check, unseal equality, destroy and typed erased replay, then reconfirms target authority.</summary>
    /// <param name="cancellationToken">Caller cancellation.</param><returns>Whether this fresh exact evaluation passed all required observations.</returns>
    public async Task<bool> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken);
        byte[]? challenge = null; byte[]? plaintext = null; byte[]? opened = null; byte[]? persisted = null; byte[]? replayBytes = null; byte[]? transferred = null;
        string? originalCanaryId = null; Fr34CanaryAuthorization? admitted = null;
        Fr34CanaryReference? reference = null;
        bool destructionConfirmed = false;
        try
        {
            budget.Check();
            if (!ValidTarget()) { return false; }
            var initial = await budget.ReadAsync(() => authority.ObserveAsync(target, CancellationToken.None)).ConfigureAwait(false);
            if (!Current(initial)) { return false; }
            admitted = initial;
            challenge = Encoding.UTF8.GetBytes(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            plaintext = Encoding.UTF8.GetBytes("{\"fr34Canary\":\"" + Encoding.UTF8.GetString(challenge) + "\"}");
            string canaryId = originalCanaryId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            byte[] sealInput = plaintext.ToArray();
            var sealedReference = await budget.ReadAsync(async () =>
            {
                // This copy belongs to the provider operation until it terminates, even after caller abandonment.
                try { return await engine.SealAndPersistAsync(target, canaryId, sealInput, CancellationToken.None).ConfigureAwait(false); }
                finally { CryptographicOperations.ZeroMemory(sealInput); }
            }, value => { if (OwnedReference(value, canaryId)) { CleanupOwned(value!, canaryId, initial!); } },
                notStarted: () => CryptographicOperations.ZeroMemory(sealInput)).ConfigureAwait(false);
            if (!OwnedReference(sealedReference, canaryId)) { return false; }
            reference = sealedReference!;
            var supplied = await budget.ReadAsync(() => storage.ReadAsync(reference, CancellationToken.None),
                static value => { if (value?.Bytes is { } bytes) { CryptographicOperations.ZeroMemory(bytes); } }).ConfigureAwait(false);
            transferred = supplied?.Bytes;
            // The reader transfers one detached array. Pure capture retires that array only after its
            // own operation finishes, while the one owned snapshot is used for scan and exact proof.
            var read = await budget.ReadAsync(() => Task.FromResult(CaptureObservation(supplied, reference, budget)),
                static value => { if (value?.Bytes is { } bytes) { CryptographicOperations.ZeroMemory(bytes); } },
                notStarted: () => { if (supplied?.Bytes is { } bytes) { CryptographicOperations.ZeroMemory(bytes); } }).ConfigureAwait(false);
            if (read is null) { return false; }
            persisted = read.Bytes;
            budget.Check();
            if (persisted.AsSpan().IndexOf(plaintext) >= 0 || persisted.AsSpan().IndexOf(challenge) >= 0) { return false; }
            if (!await ProveCarrierAsync(reference, canaryId, read!, initial!, budget).ConfigureAwait(false)) { return false; }
            opened = await budget.ReadAsync(() => engine.UnsealAsync(reference, CancellationToken.None),
                static bytes => { if (bytes is not null) { CryptographicOperations.ZeroMemory(bytes); } }).ConfigureAwait(false);
            if (opened is null || !CryptographicOperations.FixedTimeEquals(opened, plaintext)) { return false; }
            // A lost destroy acknowledgement may still have completed; only independent exact confirmation decides.
            if (!await ProveOwnershipAsync(reference, canaryId, initial!, budget).ConfigureAwait(false)) { return false; }
            try { await budget.ReadAsync(() => engine.DestroyAsync(reference, CancellationToken.None)).ConfigureAwait(false); }
            catch (Exception) { budget.Check(); }
            destructionConfirmed = await budget.ReadAsync(() => authority.ConfirmDestroyedAsync(reference, CancellationToken.None)).ConfigureAwait(false);
            if (!destructionConfirmed) { return false; }
            var replay = await budget.ReadAsync(() => engine.ReplayAsync(reference, CancellationToken.None),
                static value => { if (value?.PayloadBytes is { } bytes) { CryptographicOperations.ZeroMemory(bytes); } }).ConfigureAwait(false);
            replayBytes = replay?.PayloadBytes;
            if (replay is not { IsUnreadable: true, PayloadBytes: null, SerializationFormat: null,
                    UnreadableReason: UnreadableProtectedDataReason.KeyInvalidatedOrDeleted }) { return false; }
            var final = await budget.ReadAsync(() => authority.ObserveAsync(target, CancellationToken.None)).ConfigureAwait(false);
            budget.Check();
            return Current(initial) && Current(final) && initial!.AuthorityRevision == final!.AuthorityRevision && initial.ValidUntil == final.ValidUntil;
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return false; }
        finally
        {
            if (transferred is not null) { CryptographicOperations.ZeroMemory(transferred); }
            if (challenge is not null) { CryptographicOperations.ZeroMemory(challenge); }
            if (plaintext is not null) { CryptographicOperations.ZeroMemory(plaintext); }
            if (opened is not null) { CryptographicOperations.ZeroMemory(opened); }
            if (persisted is not null) { CryptographicOperations.ZeroMemory(persisted); }
            if (replayBytes is not null) { CryptographicOperations.ZeroMemory(replayBytes); }
            // On a failed observation, cleanup has its own bounded continuation and confers no readiness.
            if (reference is not null && originalCanaryId is not null && admitted is not null && !destructionConfirmed)
            { CleanupOwned(reference, originalCanaryId, admitted); }
        }
    }

    /// <summary>Evaluates the same fresh gate immediately before a content-bearing operation may be admitted.</summary>
    public async Task RequireForContentAsync(CancellationToken cancellationToken = default)
    { if (!await EvaluateAsync(cancellationToken).ConfigureAwait(false)) { throw new InvalidOperationException("DependencyNotAvailable: EXT-PROTECTION-1 FR-34."); } }

    private bool Current(Fr34CanaryAuthorization? value) => value is not null && value.Target == target && Text(value.AuthorityRevision)
        && value.ObservedAt != default && value.ObservedAt <= clock.GetUtcNow() && value.ValidUntil > clock.GetUtcNow();
    private bool ValidTarget() => target is not null && Text(target.EngineId) && Text(target.EngineVersion) && Text(target.CustodyTargetId) && Text(target.TenantId);
    private bool OwnedReference(Fr34CanaryReference? value, string canaryId) => value is not null && value.Target == target
        && value.CanaryId == canaryId && Text(value.RecordId) && Text(value.KeyReference);
    private static Fr34PersistedCanary? CaptureObservation(Fr34PersistedCanary? supplied, Fr34CanaryReference reference, PrivateOwnerOperationDeadline budget)
    {
        try
        {
            budget.Check();
            if (supplied?.Bytes is not { Length: > 0 and <= 16384 } || supplied.Reference != reference
                || supplied.Metadata is not { State: PayloadProtectionState.Protected, MetadataVersion: 1 } metadata
                || string.IsNullOrWhiteSpace(metadata.Scheme) || string.IsNullOrWhiteSpace(metadata.KeyAlias)
                || metadata.Scheme.Length > EventStorePayloadProtectionMetadata.MaxSchemeLength || metadata.KeyAlias.Length > EventStorePayloadProtectionMetadata.MaxKeyAliasLength
                || metadata.ContentHint?.Length > EventStorePayloadProtectionMetadata.MaxContentHintLength) { return null; }
            IReadOnlyDictionary<string, string>? flags = null;
            if (metadata.CompatibilityFlags is { } source)
            {
                if (source.Count is < 0 or > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagCount) { return null; }
                var owned = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var pair in source)
                {
                    budget.Check();
                    if (owned.Count >= EventStorePayloadProtectionMetadata.MaxCompatibilityFlagCount || pair.Key is null || pair.Value is null
                        || pair.Key.Length > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagKeyLength || pair.Value.Length > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagValueLength
                        || !owned.TryAdd(pair.Key, pair.Value)) { return null; }
                }
                flags = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(owned);
            }
            var captured = metadata with { CompatibilityFlags = flags };
            if (!EventStorePayloadProtectionMetadataCarrier.TryValidate(captured, out _)) { return null; }
            budget.Check();
            return supplied with { Bytes = supplied.Bytes.ToArray(), Metadata = captured };
        }
        finally
        {
            // ReadAsync transfers this detached source array to the gate; no reader may retain it.
            if (supplied?.Bytes is { } bytes) { CryptographicOperations.ZeroMemory(bytes); }
        }
    }
    private async Task<bool> ProveCarrierAsync(Fr34CanaryReference reference, string canaryId, Fr34PersistedCanary observed,
        Fr34CanaryAuthorization admitted, PrivateOwnerOperationDeadline budget)
    {
        if (!OwnedReference(reference, canaryId) || !Current(admitted)) { return false; }
        var current = await budget.ReadAsync(() => authority.ObserveAsync(target, CancellationToken.None)).ConfigureAwait(false);
        if (!SameAuthority(current, admitted)) { return false; }
        // The independent proof operation owns its exact input copy until termination, even if this caller abandons it.
        byte[] input = observed.Bytes.ToArray();
        bool proved = await budget.ReadAsync(async () =>
        {
            try { return await authority.VerifyCarrierAsync(target, canaryId, reference, observed with { Bytes = input }, admitted, CancellationToken.None).ConfigureAwait(false); }
            finally { CryptographicOperations.ZeroMemory(input); }
        }, notStarted: () => CryptographicOperations.ZeroMemory(input)).ConfigureAwait(false);
        if (!proved) { return false; }
        var final = await budget.ReadAsync(() => authority.ObserveAsync(target, CancellationToken.None)).ConfigureAwait(false);
        budget.Check(); return Current(admitted) && SameAuthority(final, admitted);
    }
    private async Task<bool> ProveOwnershipAsync(Fr34CanaryReference reference, string canaryId, Fr34CanaryAuthorization admitted, PrivateOwnerOperationDeadline budget)
    {
        if (!OwnedReference(reference, canaryId) || !Current(admitted)) { return false; }
        var current = await budget.ReadAsync(() => authority.ObserveAsync(target, CancellationToken.None)).ConfigureAwait(false);
        if (!SameAuthority(current, admitted) || !await budget.ReadAsync(() => authority.VerifyOwnershipAsync(target, canaryId, reference, admitted, CancellationToken.None)).ConfigureAwait(false)) { return false; }
        var final = await budget.ReadAsync(() => authority.ObserveAsync(target, CancellationToken.None)).ConfigureAwait(false);
        budget.Check(); return Current(admitted) && SameAuthority(final, admitted);
    }
    private bool SameAuthority(Fr34CanaryAuthorization? value, Fr34CanaryAuthorization admitted)
        => Current(value) && value!.AuthorityRevision == admitted.AuthorityRevision && value.ValidUntil == admitted.ValidUntil;
    private void CleanupOwned(Fr34CanaryReference reference, string canaryId, Fr34CanaryAuthorization admitted)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); CleanupCompletion = completion.Task;
        _ = Task.Run(async () =>
        {
            try
            {
                var budget = new PrivateOwnerOperationDeadline(clock, CancellationToken.None);
                if (await ProveOwnershipAsync(reference, canaryId, admitted, budget).ConfigureAwait(false))
                { _ = await budget.ReadAsync(() => engine.DestroyAsync(reference, CancellationToken.None)).ConfigureAwait(false); }
            }
            catch (Exception) { /* Failed independently owned canary cleanup never grants readiness. */ }
            finally { completion.TrySetResult(); }
        });
    }
    private static bool Text(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 2048;
}
