using System.Collections.Concurrent;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Actual occurrence key port over native ERC roots, retained one-time encryption leases and independently enrolled tenant DigestKeys.</summary>
public sealed class ErasureRootOccurrenceKeyProvider(ErasureRootCustody custody, ErasureRootOccurrenceBindings bindings,
    IInteractionOccurrenceRegistry registry, ErasureRootDigestKeyResolver digests, IErasureRootAuthorization authorization) : IInteractionOccurrenceKeyProvider
{
    private readonly ConcurrentDictionary<string, (CustodyKeyRegistration Registration, OwnedErasureRootKey Key, InteractionOccurrenceOwnedKey Use)> _holders = new(StringComparer.Ordinal);

    /// <summary>Validates actual native digest inventory and current source-write permission before source enrollment or root issue.</summary>
    public async Task<bool> PreflightEnrollmentAsync(ErasureRootOccurrenceEnrollment enrollment, CancellationToken token)
    {
        using var digest = await digests.ResolveAsync(enrollment, enrollment.Binding.DigestKeyVersion, token).ConfigureAwait(false);
        return digest is not null && await authorization.AuthorizeAsync(enrollment.Binding.Identity.Target.TenantId, enrollment.Binding.OperationId,
            "OccurrenceWrite", ErasureRootCustody.Hash(enrollment.Binding), token).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<InteractionOccurrenceOwnedKey?> ResolveDigestAsync(InteractionOccurrenceIdentity identity, string version, CancellationToken token)
    {
        var original = await bindings.FindAsync(identity.Owner, ErasureRootOccurrenceBindings.Occurrence(identity), token).ConfigureAwait(false);
        if (original?.Enrollment.Binding.Identity != identity || !await IsCurrentAsync(identity, "Read", token).ConfigureAwait(false)) { return null; }
        return await digests.ResolveAsync(original.Enrollment, version, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> AcquireEncryptionLeaseAsync(InteractionOccurrenceRecord record, CancellationToken token)
    {
        var identity = record.Request.Identity;
        var original = await bindings.FindAsync(identity.Owner, ErasureRootOccurrenceBindings.Occurrence(identity), token).ConfigureAwait(false);
        if (original?.Enrollment.Binding.Identity != identity || original.Enrollment.Binding.KeyReference != record.KeyReference) { return false; }
        var current = await registry.LookupAsync(identity, token).ConfigureAwait(false);
        return current.Status == InteractionOccurrenceReservationStatus.Reserved && ErasureRootCustody.Hash(current.Record) == ErasureRootCustody.Hash(record)
            && await bindings.AcquireEncryptionLeaseAsync(original.Enrollment.Binding, record, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<InteractionOccurrenceOwnedKey?> ResolveRootAsync(InteractionOccurrenceIdentity identity, string keyReference, bool writing, CancellationToken token)
    {
        await CompleteDisposedUsesAsync(token).ConfigureAwait(false);
        var original = await bindings.FindAsync(identity.Owner, ErasureRootOccurrenceBindings.Occurrence(identity), token).ConfigureAwait(false);
        if (original?.Enrollment.Binding.Identity != identity || original.Enrollment.Binding.KeyReference != keyReference
            || writing && original.EncryptionRequestDigest is null || !await IsCurrentAsync(identity, writing ? "Write" : "Read", token).ConfigureAwait(false)) { return null; }
        string holderId;
        if (writing) { holderId = "erc-write:" + ErasureRootCustody.Hash(new { identity, keyReference }); }
        else
        {
            holderId = await ReserveReadAsync(original.Enrollment.Binding, token).ConfigureAwait(false) ?? string.Empty;
            if (holderId.Length == 0) { return null; }
        }
        var owned = await custody.UnwrapAsync(original.Registration, holderId, token).ConfigureAwait(false);
        if (owned is null) { return null; }
        var use = new InteractionOccurrenceOwnedKey(identity.Target, "interaction-root", identity.RootKeyVersion, owned.Bytes);
        if (!_holders.TryAdd(holderId, (original.Registration, owned, use))) { use.Dispose(); owned.Dispose(); return null; }
        return use;
    }

    private async Task<string?> ReserveReadAsync(InteractionProtectionBinding binding, CancellationToken token)
    {
        string tenant = binding.Identity.Target.TenantId;
        string id = ErasureRootOccurrenceBindings.Id(binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(binding.Identity));
        for (int attempt = 0; attempt < 16; attempt++)
        {
            var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
            if (read is not { } current || current.Head.Pending is not null || !current.Head.Occurrences.TryGetValue(id, out var original)
                || original.Enrollment.Binding != binding || current.Head.Erased.ContainsKey(ErasureRootCustody.TargetId(binding.Identity.Target))) { return null; }
            long ordinal = checked(original.ReadOrdinal + 1);
            string holder = "erc-read:" + ErasureRootCustody.Hash(new { binding, ordinal });
            if (!await authorization.AuthorizeAsync(tenant, holder, "AdmitOccurrenceRead", ErasureRootCustody.Hash(new { binding, ordinal }), token).ConfigureAwait(false)) { return null; }
            current.Head.Occurrences[id] = original with { ReadOrdinal = ordinal };
            if (await custody.SaveAsync(tenant, current.Version, current.Head, token).ConfigureAwait(false)) { return holder; }
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<bool> IsCurrentAsync(InteractionOccurrenceIdentity identity, string operation, CancellationToken token)
    {
        if (operation is not ("Read" or "Write")) { return false; }
        var original = await bindings.FindAsync(identity.Owner, ErasureRootOccurrenceBindings.Occurrence(identity), token).ConfigureAwait(false);
        if (original?.Enrollment.Binding.Identity != identity || operation == "Write" && original.EncryptionRequestDigest is null) { return false; }
        var read = await custody.ReadAsync(identity.Target.TenantId, token).ConfigureAwait(false);
        return read is { } current && current.Head.Pending is null
            && current.Head.Targets.TryGetValue(ErasureRootCustody.TargetId(identity.Target), out var root) && root.Registration == original.Registration
            && await authorization.AuthorizeAsync(identity.Target.TenantId, original.Enrollment.Binding.OperationId, "Occurrence" + operation,
                ErasureRootCustody.Hash(original.Enrollment.Binding), token).ConfigureAwait(false);
    }

    /// <summary>Retires only original owned root buffers actually cleared by the engine; independently authorized completion keeps crash/unknown holders restrictive.</summary>
    public async Task<int> CompleteDisposedUsesAsync(CancellationToken token = default)
    {
        int completed = 0;
        foreach (var holder in _holders)
        {
            if (!holder.Value.Use.IsDisposed || holder.Value.Key.Bytes.AsSpan().IndexOfAnyExcept((byte)0) >= 0) { continue; }
            if (await custody.CompleteHolderAsync(holder.Value.Registration, holder.Value.Key, token).ConfigureAwait(false)
                && _holders.TryRemove(holder.Key, out _)) { completed++; }
        }
        return completed;
    }
}
