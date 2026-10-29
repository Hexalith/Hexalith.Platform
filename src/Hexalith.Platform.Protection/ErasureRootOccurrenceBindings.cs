using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;

namespace Hexalith.Platform.Protection;

/// <summary>ERC-retained complete occurrence bindings and one-time writer/encryption leases. Caller aliases cannot enroll an occurrence.</summary>
public sealed class ErasureRootOccurrenceBindings(ErasureRootCustody custody, IErasureRootAuthorization authorization,
    IErasureRootOccurrenceSource source, ErasureRootDigestKeyResolver? digestKeys = null) : IInteractionProtectionBindingProvider, IInteractionProtectionBindingSource
{
    /// <inheritdoc />
    public async Task<InteractionProtectionBinding?> ResolveErasedStoredAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence,
        byte[] sealedPayload, string keyReference, CancellationToken cancellationToken = default)
    {
        var loss = await custody.ReadPermanentLossAsync(owner.TenantId, cancellationToken).ConfigureAwait(false);
        if (loss is null) { return null; }
        var original = await source.ResolveErasedStoredAsync(owner, occurrence, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(sealedPayload)),
            keyReference, loss, cancellationToken).ConfigureAwait(false);
        return original is not null && original.RootIdentity.StoreTarget == loss.StoreId && original.Binding.Identity.Owner == owner
            && Occurrence(original.Binding.Identity) == occurrence && original.Binding.KeyReference == keyReference ? original.Binding : null;
    }
    /// <inheritdoc />
    public async Task<IReadOnlyList<InteractionProtectionBinding>?> ResolveSnapshotEntriesAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext parent,
        string containerDigest, IReadOnlyList<SnapshotProtectionEntry> entries, bool writing, CancellationToken cancellationToken = default)
    {
        if (parent.PayloadKind != PayloadProtectionPayloadKind.Snapshot || containerDigest is not { Length: 64 } || entries.Count is < 2 or > 1000
            || entries.Select(entry => entry.Path).Distinct(StringComparer.Ordinal).Count() != entries.Count) { return null; }
        var enrolled = new List<ErasureRootOccurrenceEnrollment>();
        // The entire independent source manifest must exist before resolving any writing binding can issue a root.
        foreach (var entry in entries)
        {
            var child = InteractionSnapshotEntries.EntryOccurrence(parent, entry.Path);
            var original = await source.ResolveAsync(owner, child, cancellationToken).ConfigureAwait(false);
            if (original?.SnapshotEntry != new SnapshotEntrySourceBinding(parent, entry.Path, containerDigest, entries.Count)
                || original.Binding.Identity.Target != entry.Target || entry.Target.TenantId != owner.TenantId) { return null; }
            if (writing && !await PreflightAsync(original, cancellationToken).ConfigureAwait(false)) { return null; }
            enrolled.Add(original);
        }
        string digest = ErasureRootCustody.Hash(new { owner, parent, containerDigest, entries });
        if (!await authorization.AuthorizeAsync(owner.TenantId, Id(owner, parent), "VerifySnapshotEntryManifest", digest, cancellationToken).ConfigureAwait(false)) { return null; }
        var result = new List<InteractionProtectionBinding>();
        for (int index = 0; index < entries.Count; index++)
        {
            var resolved = await ResolveAsync(owner, InteractionSnapshotEntries.EntryOccurrence(parent, entries[index].Path), entries[index].Target, writing, cancellationToken).ConfigureAwait(false);
            if (resolved is null || resolved != enrolled[index].Binding) { return null; } result.Add(resolved);
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> AcquireSnapshotCompletionLeasesAsync(IReadOnlyList<InteractionProtectionBinding> bindings, CancellationToken cancellationToken = default)
    {
        if (bindings.Count is < 2 or > 1000 || bindings.Select(binding => binding.Identity.Owner).Distinct().Count() != 1
            || bindings.Select(binding => binding.OperationId).Distinct(StringComparer.Ordinal).Count() != bindings.Count) { return false; }
        string tenant = bindings[0].Identity.Owner.TenantId, digest = ErasureRootCustody.Hash(bindings);
        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (!await authorization.AuthorizeAsync(tenant, bindings[0].OperationId, "AcquireSnapshotCompletionLeases", digest, cancellationToken).ConfigureAwait(false)) { return false; }
            var read = await custody.ReadAsync(tenant, cancellationToken).ConfigureAwait(false);
            if (read is not { } current || current.Head.Pending is not null) { return false; }
            foreach (var binding in bindings)
            {
                string id = Id(binding.Identity.Owner, Occurrence(binding.Identity));
                if (binding.Identity.Target.TenantId != tenant || current.Head.Erased.ContainsKey(ErasureRootCustody.TargetId(binding.Identity.Target))
                    || !current.Head.Occurrences.TryGetValue(id, out var original) || original.Enrollment.Binding != binding || original.CompletionLease
                    || original.EncryptionRequestDigest is null || original.Enrollment.SnapshotEntry is null) { return false; }
            }
            foreach (var binding in bindings)
            {
                string id = Id(binding.Identity.Owner, Occurrence(binding.Identity)); current.Head.Occurrences[id] = current.Head.Occurrences[id] with { CompletionLease = true };
            }
            if (await custody.SaveAsync(tenant, current.Version, current.Head, cancellationToken).ConfigureAwait(false)) { return true; }
        }
        return false;
    }
    internal static string Id(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence) => CustodyInteractionProtectionBindingSource.ResourceId(owner, occurrence);
    internal static PayloadProtectionOccurrenceContext Occurrence(InteractionOccurrenceIdentity identity) => new(identity.Sequence, identity.Kind, identity.PayloadTypeId);

    /// <inheritdoc />
    public async Task<InteractionProtectionBinding?> ResolveAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence,
        ProtectionTarget? requestedTarget, bool writing, CancellationToken cancellationToken = default)
    {
        try
        {
            var retained = await FindAsync(owner, occurrence, cancellationToken).ConfigureAwait(false);
            if (retained is not null) { return requestedTarget is null || retained.Enrollment.Binding.Identity.Target == requestedTarget ? retained.Enrollment.Binding : null; }
            if (!writing)
            {
                var loss = await custody.ReadPermanentLossAsync(owner.TenantId, cancellationToken).ConfigureAwait(false);
                var lost = loss is null ? null : await source.ResolveAsync(owner, occurrence, cancellationToken).ConfigureAwait(false);
                return lost is not null && lost.RootIdentity.StoreTarget == loss!.StoreId && lost.Binding.Identity.Owner == owner && Occurrence(lost.Binding.Identity) == occurrence
                    && (requestedTarget is null || lost.Binding.Identity.Target == requestedTarget) ? lost.Binding : null;
            }
            var enrolled = await source.ResolveAsync(owner, occurrence, cancellationToken).ConfigureAwait(false);
            if (enrolled is null || enrolled.Binding.Identity.Owner != owner || enrolled.Binding.Identity.Sequence != occurrence.RecordSequence
                || enrolled.Binding.Identity.Kind != occurrence.PayloadKind || enrolled.Binding.Identity.PayloadTypeId != occurrence.PayloadTypeId
                || enrolled.Binding.Identity.Target.TenantId != owner.TenantId || enrolled.RootIdentity.TenantId != owner.TenantId
                || enrolled.RootIdentity.ObjectId != enrolled.Binding.Identity.Target.AgentInteractionId
                || enrolled.RootIdentity.KeyAlias != enrolled.Binding.Identity.Target.TargetProtectionKeyAlias
                || enrolled.RootIdentity.KeyVersion != enrolled.Binding.Identity.RootKeyVersion
                || enrolled.Digest.InventoryKey.TenantId != owner.TenantId || enrolled.Digest.InventoryKey.Version != enrolled.Binding.DigestKeyVersion
                || !CanonicalReference(enrolled.Binding.KeyReference) || !CanonicalReference(enrolled.Binding.OperationId)
                || enrolled.Binding.ReservationAttemptOrdinal < 1 || enrolled.Binding.LifecycleEpoch == 0
                || requestedTarget is not null && enrolled.Binding.Identity.Target != requestedTarget) { return null; }
            if (!await PreflightAsync(enrolled, cancellationToken).ConfigureAwait(false)) { return null; }
            string id = Id(owner, occurrence), digest = ErasureRootCustody.Hash(enrolled);
            if (!await authorization.AuthorizeAsync(owner.TenantId, id, "EnrollOccurrence", digest, cancellationToken).ConfigureAwait(false)) { return null; }
            var registration = await custody.WrapAsync(enrolled.RootIdentity, enrolled.RootOperationId, enrolled.ExpectedLifecycleRevision, cancellationToken).ConfigureAwait(false);
            if (registration is null) { return null; }
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var read = await custody.ReadAsync(owner.TenantId, cancellationToken).ConfigureAwait(false);
                if (read is not { } current || current.Head.Pending is not null || current.Head.Erased.ContainsKey(ErasureRootCustody.TargetId(enrolled.RootIdentity))) { return null; }
                if (current.Head.Occurrences.TryGetValue(id, out var original))
                { return ErasureRootCustody.Hash(original.Enrollment) == digest && original.Registration == registration ? original.Enrollment.Binding : null; }
                if (!await authorization.AuthorizeAsync(owner.TenantId, id, "EnrollOccurrence", digest, cancellationToken).ConfigureAwait(false)) { return null; }
                current.Head.Occurrences.Add(id, new(enrolled, registration));
                if (await custody.SaveAsync(owner.TenantId, current.Version, current.Head, cancellationToken).ConfigureAwait(false)) { return enrolled.Binding; }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); }
        return null;
    }

    internal async Task<bool> PreflightAsync(ErasureRootOccurrenceEnrollment enrollment, CancellationToken token)
    {
        if (digestKeys is null) { return false; }
        using var digest = await digestKeys.ResolveAsync(enrollment, enrollment.Binding.DigestKeyVersion, token).ConfigureAwait(false);
        return digest is not null && await authorization.AuthorizeAsync(enrollment.Binding.Identity.Target.TenantId,
            enrollment.Binding.OperationId, "OccurrenceWrite", ErasureRootCustody.Hash(enrollment.Binding), token).ConfigureAwait(false);
    }

    private static bool CanonicalReference(string value) => value is { Length: 26 } && value[0] is >= '0' and <= '7'
        && value.All(character => "0123456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(character, StringComparison.Ordinal));

    internal async Task<ErasureRootOccurrence?> FindAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence, CancellationToken token)
    {
        string id = Id(owner, occurrence);
        if (!await authorization.AuthorizeAsync(owner.TenantId, id, "ReadOccurrenceBinding", ErasureRootCustody.Hash(new { owner, occurrence }), token).ConfigureAwait(false)) { return null; }
        var read = await custody.ReadAsync(owner.TenantId, token).ConfigureAwait(false);
        if (read is not { } current || !current.Head.Occurrences.TryGetValue(id, out var original)) { return null; }
        var identity = original.Enrollment.Binding.Identity;
        return identity.Owner == owner && identity.Target.TenantId == owner.TenantId && identity.Sequence == occurrence.RecordSequence
            && identity.Kind == occurrence.PayloadKind && identity.PayloadTypeId == occurrence.PayloadTypeId ? original : null;
    }

    /// <inheritdoc />
    public async Task<InteractionProtectionBinding?> FindRetainedAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence, CancellationToken cancellationToken)
        => (await FindAsync(owner, occurrence, cancellationToken).ConfigureAwait(false))?.Enrollment.Binding;
    /// <inheritdoc />
    public Task<bool> AcquireCompletionLeaseAsync(InteractionProtectionBinding binding, CancellationToken cancellationToken = default)
        => LeaseAsync(binding, null, cancellationToken);
    /// <inheritdoc />
    public Task<bool> TryAcquireCompletionLeaseAsync(InteractionProtectionBinding binding, CancellationToken cancellationToken)
        => LeaseAsync(binding, null, cancellationToken);
    internal Task<bool> AcquireEncryptionLeaseAsync(InteractionProtectionBinding binding, InteractionOccurrenceRecord record, CancellationToken token)
        => LeaseAsync(binding, record, token);

    private async Task<bool> LeaseAsync(InteractionProtectionBinding binding, InteractionOccurrenceRecord? encryption, CancellationToken token)
    {
        string id = Id(binding.Identity.Owner, Occurrence(binding.Identity));
        string operation = encryption is null ? "AcquireCompletionLease" : "AcquireEncryptionLease";
        string digest = ErasureRootCustody.Hash(new { binding, encryption });
        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (!await authorization.AuthorizeAsync(binding.Identity.Target.TenantId, id, operation, digest, token).ConfigureAwait(false)) { return false; }
            var read = await custody.ReadAsync(binding.Identity.Target.TenantId, token).ConfigureAwait(false);
            if (read is not { } current || current.Head.Pending is not null || current.Head.Erased.ContainsKey(ErasureRootCustody.TargetId(binding.Identity.Target))
                || !current.Head.Occurrences.TryGetValue(id, out var original) || original.Enrollment.Binding != binding
                || encryption is null && original.CompletionLease || encryption is not null && original.EncryptionRequestDigest is not null) { return false; }
            if (encryption is not null && (encryption.Request.Identity != binding.Identity || encryption.KeyReference != binding.KeyReference
                || encryption.Sealed is not null || encryption.WriterState != InteractionOccurrenceWriterState.Reserved)) { return false; }
            current.Head.Occurrences[id] = encryption is null ? original with { CompletionLease = true }
                : original with { EncryptionRequestDigest = ErasureRootCustody.Hash(encryption.Request) };
            if (await custody.SaveAsync(binding.Identity.Target.TenantId, current.Version, current.Head, token).ConfigureAwait(false)) { return true; }
        }
        return false;
    }

    /// <inheritdoc />
    public async Task<bool> IsIrreversiblyErasedAsync(InteractionProtectionBinding binding, CancellationToken cancellationToken = default)
        => await ReadErasureAsync(binding, cancellationToken).ConfigureAwait(false) is not null;
    /// <inheritdoc />
    public async Task<InteractionProtectionErasureWitness?> ReadErasureAsync(InteractionProtectionBinding binding, CancellationToken cancellationToken)
    {
        var loss = await custody.ReadPermanentLossAsync(binding.Identity.Target.TenantId, cancellationToken).ConfigureAwait(false);
        if (loss is not null)
        {
            var sourceOriginal = await source.ResolveAsync(binding.Identity.Owner, Occurrence(binding.Identity), cancellationToken).ConfigureAwait(false);
            if (sourceOriginal?.Binding != binding || sourceOriginal.RootIdentity.StoreTarget != loss.StoreId) { return null; }
            string[] lostTargets = [InteractionProtectionBindingProvider.TargetId(binding.Identity.Target)], lostReceipts = ["erc-permanent-loss:" + loss.ReceiptId + ":" + ErasureRootCustody.Hash(binding.Identity.Target)];
            return new(InteractionProtectionBindingProvider.ManifestDigest(binding), InteractionProtectionBindingProvider.ReceiptOrderDigest(lostTargets, lostReceipts),
                lostTargets, lostReceipts, true, false, null, false, null);
        }
        var original = await FindAsync(binding.Identity.Owner, Occurrence(binding.Identity), cancellationToken).ConfigureAwait(false);
        if (original?.Enrollment.Binding != binding) { return null; }
        var erased = await custody.ReadErasureAsync(binding.Identity.Target, cancellationToken).ConfigureAwait(false);
        if (erased is null) { return null; }
        string[] targets = [InteractionProtectionBindingProvider.TargetId(erased.Target)], receipts = [erased.ReceiptId];
        return new(InteractionProtectionBindingProvider.ManifestDigest(binding), InteractionProtectionBindingProvider.ReceiptOrderDigest(targets, receipts),
            targets, receipts, true, false, null, false, null);
    }
}
