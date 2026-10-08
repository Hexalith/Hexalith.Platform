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
    /// <summary>Performs seal, independent persisted-byte check, unseal equality, destroy and typed erased replay, then reconfirms target authority.</summary>
    /// <param name="cancellationToken">Caller cancellation.</param><returns>Whether this fresh exact evaluation passed all required observations.</returns>
    public async Task<bool> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken);
        byte[]? challenge = null; byte[]? plaintext = null; byte[]? opened = null; byte[]? persisted = null;
        Fr34CanaryReference? reference = null;
        bool destructionConfirmed = false;
        try
        {
            budget.Check();
            if (!ValidTarget()) { return false; }
            var initial = await budget.ReadAsync(() => authority.ObserveAsync(target, CancellationToken.None)).ConfigureAwait(false);
            if (!Current(initial)) { return false; }
            challenge = Encoding.UTF8.GetBytes(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            plaintext = Encoding.UTF8.GetBytes("{\"fr34Canary\":\"" + Encoding.UTF8.GetString(challenge) + "\"}");
            string canaryId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var sealedReference = await budget.ReadAsync(() => engine.SealAndPersistAsync(target, canaryId, plaintext.ToArray(), CancellationToken.None),
                value => { if (OwnedReference(value, canaryId)) { CleanupOwned(value!); } }).ConfigureAwait(false);
            if (!OwnedReference(sealedReference, canaryId)) { return false; }
            reference = sealedReference!;
            var read = await budget.ReadAsync(() => storage.ReadAsync(reference, CancellationToken.None),
                static value => { if (value?.Bytes is { } bytes) { CryptographicOperations.ZeroMemory(bytes); } }).ConfigureAwait(false);
            if (read?.Bytes is not { Length: > 0 and <= 16384 } || read.Reference != reference
                || read.Metadata is not { State: PayloadProtectionState.Protected, MetadataVersion: 1 }
                || !EventStorePayloadProtectionMetadataCarrier.TryValidate(read.Metadata, out _)
                || string.IsNullOrWhiteSpace(read.Metadata.Scheme) || string.IsNullOrWhiteSpace(read.Metadata.KeyAlias)) { return false; }
            persisted = read.Bytes.ToArray();
            budget.Check();
            if (persisted.AsSpan().IndexOf(plaintext) >= 0 || persisted.AsSpan().IndexOf(challenge) >= 0) { return false; }
            opened = await budget.ReadAsync(() => engine.UnsealAsync(reference, CancellationToken.None),
                static bytes => { if (bytes is not null) { CryptographicOperations.ZeroMemory(bytes); } }).ConfigureAwait(false);
            if (opened is null || !CryptographicOperations.FixedTimeEquals(opened, plaintext)) { return false; }
            // A lost destroy acknowledgement may still have completed; only independent exact confirmation decides.
            try { await budget.ReadAsync(() => engine.DestroyAsync(reference, CancellationToken.None)).ConfigureAwait(false); }
            catch (Exception) { budget.Check(); }
            destructionConfirmed = await budget.ReadAsync(() => authority.ConfirmDestroyedAsync(reference, CancellationToken.None)).ConfigureAwait(false);
            if (!destructionConfirmed) { return false; }
            var replay = await budget.ReadAsync(() => engine.ReplayAsync(reference, CancellationToken.None),
                static value => { if (value?.PayloadBytes is { } bytes) { CryptographicOperations.ZeroMemory(bytes); } }).ConfigureAwait(false);
            if (replay is not { IsUnreadable: true, PayloadBytes: null, SerializationFormat: null,
                    UnreadableReason: UnreadableProtectedDataReason.KeyInvalidatedOrDeleted }) { return false; }
            var final = await budget.ReadAsync(() => authority.ObserveAsync(target, CancellationToken.None)).ConfigureAwait(false);
            budget.Check();
            return Current(initial) && Current(final) && initial!.AuthorityRevision == final!.AuthorityRevision && initial.ValidUntil == final.ValidUntil;
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return false; }
        finally
        {
            if (challenge is not null) { CryptographicOperations.ZeroMemory(challenge); }
            if (plaintext is not null) { CryptographicOperations.ZeroMemory(plaintext); }
            if (opened is not null) { CryptographicOperations.ZeroMemory(opened); }
            if (persisted is not null) { CryptographicOperations.ZeroMemory(persisted); }
            // On a failed observation, cleanup has its own bounded continuation and confers no readiness.
            if (reference is not null && !destructionConfirmed)
            { CleanupOwned(reference); }
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
    private void CleanupOwned(Fr34CanaryReference reference)
    { _ = Task.Run(async () => { try { await Task.Run(() => engine.DestroyAsync(reference, CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false); } catch (Exception) { /* Content-free failed canary cleanup; no success is inferred. */ } }); }
    private static bool Text(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 2048;
}
