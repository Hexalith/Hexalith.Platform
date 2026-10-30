using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>A1 ERC implementation component. A single native tenant-head CAS serializes roots, holds, durable holders and original manifests; terminal receipts also require independently qualified copy/restore authorization. This unregistered component does not qualify native storage sanitization.</summary>
public sealed class ErasureRootCustody(IErasureRootStore store, ErasureRootInstallation installation,
    IErasureRootAuthorization authorization) : IAtomicDeletionManifestProvider, ICustodyKeyLifecycleProvider
{
    /// <summary>Health of this owning process's disposed native verifier holders.</summary>
    public ErasureRootCompletionDiagnostics CompletionDiagnostics { get; } = new();
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<OwnedErasureRootKey, CustodyKeyRegistration> _issuedHolders = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<OwnedErasureRootKey, CustodyKeyRegistration> _disposedHolders = new();
    /// <summary>Retains only this process's actual disposed issued instance until exact authorized completion succeeds.</summary>
    internal void RetainDisposedHolder(CustodyKeyRegistration registration, OwnedErasureRootKey holder)
    {
        if (_issuedHolders.TryGetValue(holder, out var issued) && issued == registration && holder.IsDisposed)
        { _disposedHolders.TryAdd(holder, registration); CompletionDiagnostics.Track(holder); }
    }
    /// <summary>Retries cleared verifier holders under host lifetime cancellation; an outage never drops the original.</summary>
    public async Task<int> CompleteDisposedHoldersAsync(CancellationToken token = default)
    {
        int count = 0;
        foreach (var entry in _disposedHolders)
        {
            if (await CompleteHolderAsync(entry.Value, entry.Key, token).ConfigureAwait(false) && _disposedHolders.TryRemove(entry.Key, out _))
            { CompletionDiagnostics.Complete(entry.Key); count++; }
            else { CompletionDiagnostics.Refused(); }
        }
        return count;
    }
    internal ErasureRootInstallation Installation => installation;
    internal static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    /// <summary>Retains only the exact current native pin-refusal vector under explicit private observation permission. It cannot admit a manifest or bypass an owner's reservation.</summary>
    public async Task<ErasureRootPinRefusal[]?> CapturePinnedManifestRefusalAsync(DeletionBatchConsumptionRequest request, string operationId, CancellationToken token = default)
    {
        request = DeletionConsumptionIdentity.CaptureRequest(request);
        string tenant = request.Capability.TenantId, digest = Hash(request);
        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (!await authorization.AuthorizeAsync(tenant, operationId, "RecordPinnedManifestRefusal", digest, token).ConfigureAwait(false)) { return null; }
            var read = await ReadAsync(tenant, token).ConfigureAwait(false);
            if (read is not { } head || head.Head.Pending is not null) { return null; }
            if (head.Head.PinRefusals.TryGetValue(operationId, out var retained)) { return retained.RequestDigest == digest ? retained.Refusals : null; }
            if (head.Head.PinRefusals.Count >= 10000) { return null; }
            var refusals = new List<ErasureRootPinRefusal>();
            foreach (var target in request.Targets)
            {
                if (!head.Head.Targets.TryGetValue(TargetId(target), out var root) || root.Pins.Length == 0) { return null; }
                refusals.Add(new(operationId + ":" + TargetId(target), "HoldPinned", "erc-pin-refusal:" + Hash(new { operationId, digest, root.Registration, root.Pins }), target));
            }
            head.Head.PinRefusals.Add(operationId, new(digest, refusals.ToArray()));
            if (await SaveAsync(tenant, head.Version, head.Head, token).ConfigureAwait(false)) { return refusals.ToArray(); }
        }
        return null;
    }
    internal async Task<ErasureRootPermanentLossReceipt?> ReadPermanentLossAsync(string tenant, CancellationToken token)
    {
        if (!installation.NativeKeys.ContainsKey(tenant)) { return null; }
        var original = await authorization.ReadPermanentLossAsync(tenant, installation.StoreId, installation.EnrollmentId, token).ConfigureAwait(false);
        return original?.TenantId == tenant && original.StoreId == installation.StoreId && original.EnrollmentId == installation.EnrollmentId
            && !string.IsNullOrWhiteSpace(original.RetiredVolumeIdentity) && !string.IsNullOrWhiteSpace(original.ReceiptId)
            && original.CopyPolicySha256 is { Length: 64 } ? original : null;
    }

    /// <summary>Validates complete installed identity, original lifecycle comparison and current wrap permission without issuing or retaining a root.</summary>
    internal async Task<bool> PreflightWrapAsync(CustodyKeyObjectIdentity identity, string operationId, long expectedLifecycleRevision, CancellationToken token)
    {
        try
        {
            ValidateIdentity(identity);
            if (operationId is not { Length: > 0 and <= 2048 } || expectedLifecycleRevision < 0
                || await ReadPermanentLossAsync(identity.TenantId, token).ConfigureAwait(false) is not null) { return false; }
            string digest = Hash(new { identity, operationId, expectedLifecycleRevision });
            if (!await authorization.AuthorizeAsync(identity.TenantId, operationId, "WrapDek", digest, token).ConfigureAwait(false)) { return false; }
            var read = await ReadAsync(identity.TenantId, token).ConfigureAwait(false);
            return read is { } current && await ValidateOccurrenceWriteSetAsync(identity.TenantId, current.Head, token).ConfigureAwait(false)
                && current.Head.Pending is null && !current.Head.Erased.ContainsKey(TargetId(identity))
                && (!current.Head.Targets.TryGetValue(TargetId(identity), out var existing) || existing.Registration.Identity == identity
                    && existing.Registration.OperationId == operationId && existing.Registration.ExpectedRevision == expectedLifecycleRevision);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { token.ThrowIfCancellationRequested(); return false; }
    }
    /// <summary>Creates exactly one native-wrapped random root for the complete target. Exact retry returns its original registration; erased identities can never be issued again.</summary>
    public async Task<CustodyKeyRegistration?> WrapAsync(CustodyKeyObjectIdentity identity, string operationId,
        long expectedLifecycleRevision, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(identity);
        if (await ReadPermanentLossAsync(identity.TenantId, cancellationToken).ConfigureAwait(false) is not null) { return null; }
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (expectedLifecycleRevision < 0 || operationId.Length > 2048) { throw new ArgumentException("Bounded original wrap operation and nonnegative lifecycle revision required."); }
        string digest = Hash(new { identity, operationId, expectedLifecycleRevision });
        try
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                if (!await authorization.AuthorizeAsync(identity.TenantId, operationId, "WrapDek", digest, cancellationToken).ConfigureAwait(false)) { return null; }
                var current = await ReadAsync(identity.TenantId, cancellationToken).ConfigureAwait(false);
                if (current is not { } read || read.Head.Pending is not null || !await ValidateOccurrenceWriteSetAsync(identity.TenantId, read.Head, cancellationToken).ConfigureAwait(false)) { return null; }
                string id = TargetId(identity);
                if (read.Head.Erased.ContainsKey(id)) { return null; }
                if (read.Head.Targets.TryGetValue(id, out var prior))
                {
                    return prior.Registration.Identity == identity && prior.Registration.OperationId == operationId
                        && prior.Registration.ExpectedRevision == expectedLifecycleRevision ? prior.Registration : null;
                }
                // Delayed rotations may leave unused newer native versions. Every wrap explicitly uses only the retained head epoch.
                if (await store.ReadEpochAsync(identity.TenantId, cancellationToken).ConfigureAwait(false) < read.Head.Epoch) { return null; }
                string kek = read.Head.Epoch.ToString(CultureInfo.InvariantCulture);
                var registration = new CustodyKeyRegistration(identity, operationId, expectedLifecycleRevision, kek,
                    "erc-root:" + id, "erc-wrap:" + digest);
                byte[] root = RandomNumberGenerator.GetBytes(32);
                string context = Convert.ToBase64String(CustodyKeyWrapCore.Context(identity, kek));
                string ciphertext;
                try { ciphertext = await store.EncryptAsync(identity.TenantId, read.Head.Epoch, root, context, cancellationToken).ConfigureAwait(false); }
                finally { CryptographicOperations.ZeroMemory(root); }
                read.Head.Targets.Add(id, new(registration, new(ciphertext, context), [], [], []));
                if (!await authorization.AuthorizeAsync(identity.TenantId, operationId, "WrapDek", digest, cancellationToken).ConfigureAwait(false)) { return null; }
                if (await SaveAsync(identity.TenantId, read.Version, read.Head, cancellationToken).ConfigureAwait(false)) { return registration; }
                // Only an explicit CAS conflict reaches this retry. Ambiguous native writes stop in the catch below.
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); }
        return null;
    }

    /// <summary>Releases one original root only after durably registering its exact holder. A lost result remains a restrictive holder and cannot be redelivered.</summary>
    public async Task<OwnedErasureRootKey?> UnwrapAsync(CustodyKeyRegistration registration, string holderId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(registration.Identity);
        if (await ReadPermanentLossAsync(registration.Identity.TenantId, cancellationToken).ConfigureAwait(false) is not null) { return null; }
        ArgumentException.ThrowIfNullOrWhiteSpace(holderId);
        string digest = Hash(new { registration, holderId });
        byte[]? root = null;
        try
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                if (!await authorization.AuthorizeAsync(registration.Identity.TenantId, holderId, "UnwrapDek", digest, cancellationToken).ConfigureAwait(false)) { return null; }
                var current = await ReadAsync(registration.Identity.TenantId, cancellationToken).ConfigureAwait(false);
                if (current is not { } read || read.Head.Pending is not null
                    || !read.Head.Targets.TryGetValue(TargetId(registration.Identity), out var target) || target.Registration != registration
                    || target.Holders.Contains(holderId, StringComparer.Ordinal) || target.CompletedHolders.Contains(holderId, StringComparer.Ordinal)
                    || await store.ReadArchiveAsync(registration.Identity.TenantId, Hash(new ErasureRootHolderIdentity(registration, holderId)), cancellationToken).ConfigureAwait(false) is not null) { return null; }
                var leased = target with { Holders = target.Holders.Append(holderId).ToArray() };
                read.Head.Targets[TargetId(registration.Identity)] = leased;
                if (!await SaveAsync(registration.Identity.TenantId, read.Version, read.Head, cancellationToken).ConfigureAwait(false)) { continue; }
                root = await store.DecryptAsync(registration.Identity.TenantId, target.Wrapped, cancellationToken).ConfigureAwait(false);
                if (root.Length != 32 || !await authorization.AuthorizeAsync(registration.Identity.TenantId, holderId, "UnwrapDek", digest, cancellationToken).ConfigureAwait(false)) { return null; }
                var confirmed = await ReadAsync(registration.Identity.TenantId, cancellationToken).ConfigureAwait(false);
                if (confirmed is not { } fresh || fresh.Head.Pending is not null
                    || !fresh.Head.Targets.TryGetValue(TargetId(registration.Identity), out var retained)
                    || retained.Registration != registration || !retained.Holders.Contains(holderId, StringComparer.Ordinal)) { return null; }
                var result = new OwnedErasureRootKey(root, holderId);
                _issuedHolders.Add(result, registration);
                root = null;
                return result;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); }
        finally { if (root is not null) { CryptographicOperations.ZeroMemory(root); } }
        return null;
    }

    /// <summary>Completes only an independently authorized original holder after its owned bytes have been cleared. Unknown/crashed holders remain restrictive.</summary>
    public async Task<bool> CompleteHolderAsync(CustodyKeyRegistration registration, OwnedErasureRootKey holder,
        CancellationToken cancellationToken = default)
    {
        if (!_issuedHolders.TryGetValue(holder, out var issued) || issued != registration || !holder.IsDisposed
            || holder.Bytes.Length != 32 || holder.Bytes.AsSpan().IndexOfAnyExcept((byte)0) >= 0) { return false; }
        string tenant = registration.Identity.TenantId;
        string digest = Hash(new { registration, holder.HolderId });
        try
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                if (!await authorization.AuthorizeAsync(tenant, holder.HolderId, "CompleteRootHolder", digest, cancellationToken).ConfigureAwait(false)) { return false; }
                var current = await ReadAsync(tenant, cancellationToken).ConfigureAwait(false);
                if (current is not { } read || read.Head.Pending is not null) { return false; }
                if (!read.Head.Targets.TryGetValue(TargetId(registration.Identity), out var target))
                {
                    // A successful trim could only follow removal of every original durable holder.
                    // Combined with this process's actual disposed issued instance and exact immutable identity,
                    // the independently current erasure index reconciles a lost completion acknowledgement.
                    var identity = new ErasureRootHolderIdentity(registration, holder.HolderId);
                    var marker = read.Head.Erased.TryGetValue(TargetId(registration.Identity), out var erased) && erased.Registration == registration
                        ? await ErasureRootArchives.ReadBytesAsync(store, tenant, Hash(identity), cancellationToken).ConfigureAwait(false) : null;
                    return marker is not null && marker.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(identity));
                }
                if (target.Registration != registration) { return false; }
                if (!target.Holders.Contains(holder.HolderId, StringComparer.Ordinal))
                { return await HasCompletedHolderAsync(tenant, target, holder.HolderId, cancellationToken).ConfigureAwait(false); }
                _ = await ErasureRootArchives.WriteAsync(store, tenant, new ErasureRootHolderIdentity(registration, holder.HolderId), cancellationToken).ConfigureAwait(false);
                string? pointer = target.CompletedHolderArchive;
                foreach (string completed in target.CompletedHolders)
                {
                    _ = await ErasureRootArchives.WriteAsync(store, tenant, new ErasureRootHolderIdentity(registration, completed), cancellationToken).ConfigureAwait(false);
                    pointer = await ErasureRootArchives.WriteAsync(store, tenant, new ErasureRootHolderArchive(registration, completed, pointer), cancellationToken).ConfigureAwait(false);
                }
                pointer = await ErasureRootArchives.WriteAsync(store, tenant, new ErasureRootHolderArchive(registration, holder.HolderId, pointer), cancellationToken).ConfigureAwait(false);
                read.Head.Targets[TargetId(registration.Identity)] = target with { Holders = target.Holders.Where(id => id != holder.HolderId).ToArray(),
                    CompletedHolders = [], CompletedHolderArchive = pointer };
                if (await SaveAsync(tenant, read.Version, read.Head, cancellationToken).ConfigureAwait(false)) { return true; }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); }
        return false;
    }

    /// <summary>Reads the exact independent erasure index. Missing installation, outage or an unfinished manifest never establishes erasure.</summary>
    public async Task<DeletionTargetReceipt?> ReadErasureAsync(ProtectionTarget target, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await authorization.AuthorizeAsync(target.TenantId, TargetId(target), "ReadErasure", Hash(target), cancellationToken).ConfigureAwait(false)) { return null; }
            var current = await ReadAsync(target.TenantId, cancellationToken).ConfigureAwait(false);
            return current is { } read && read.Head.Pending is null && read.Head.Erased.TryGetValue(TargetId(target), out var erased)
                && erased.Receipt.Target == target ? erased.Receipt : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return null; }
    }

    /// <inheritdoc />
    public Task<DeletionManifestProviderResult> ConsumeAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId, CancellationToken cancellationToken = default)
        => ManifestAsync(request, reservationReceiptId, true, cancellationToken);

    /// <inheritdoc />
    public Task<DeletionManifestProviderResult> LookupAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId, CancellationToken cancellationToken = default)
        => ManifestAsync(request, reservationReceiptId, false, cancellationToken);

    private async Task<DeletionManifestProviderResult> ManifestAsync(DeletionBatchConsumptionRequest supplied, string reservation, bool start, CancellationToken token)
    {
        var unknown = new DeletionManifestProviderResult(supplied.Capability.TenantId, supplied.Capability.BatchId, reservation, DeletionManifestProviderState.Unknown, []);
        try
        {
            var request = DeletionConsumptionIdentity.CaptureRequest(supplied);
            ArgumentException.ThrowIfNullOrWhiteSpace(reservation);
            string digest = Hash(new { request, reservation });
            string tenant = request.Capability.TenantId;
            if (!await authorization.AuthorizeAsync(tenant, request.Capability.BatchId, start ? "DestroyDekManifest" : "LookupDestroyDekManifest", digest, token).ConfigureAwait(false)) { return unknown; }
            for (int attempt = 0; attempt < 32; attempt++)
            {
                var current = await ReadAsync(tenant, token).ConfigureAwait(false);
                if (current is not { } read) { return unknown; }
                if (read.Head.ManifestArchives.TryGetValue(request.Capability.BatchId, out var archivedManifest))
                { read.Head.Manifests[request.Capability.BatchId] = await ReadManifestAsync(tenant, request.Capability.BatchId, archivedManifest, read.Head.Epoch, token).ConfigureAwait(false); }
                if (read.Head.Manifests.TryGetValue(request.Capability.BatchId, out var completed))
                {
                    return completed.RequestDigest == digest && completed.ReservationReceiptId == reservation
                        ? unknown with { State = DeletionManifestProviderState.Consumed, TargetReceipts = completed.Receipts } : unknown;
                }
                if (read.Head.Pending is { } pending)
                {
                    if (pending.RequestDigest != digest || pending.ReservationReceiptId != reservation) { return unknown; }
                    // Lookup is allowed to reconcile only this independently retained original. It cannot begin another operation.
                    if (!await authorization.VerifyManifestAsync(request, reservation, token).ConfigureAwait(false)) { return unknown; }
                    return await FinishManifestAsync(read.Version, read.Head, token).ConfigureAwait(false) ?? unknown;
                }
                if (!start) { return unknown with { State = DeletionManifestProviderState.NotStarted }; }
                if (!await authorization.VerifyManifestAsync(request, reservation, token).ConfigureAwait(false)) { return unknown; }
                // Pin, unwrap-holder admission and this pending original race on the same native CAS.
                var selected = new List<string>(request.Targets.Count);
                foreach (var target in request.Targets)
                {
                    string id = TargetId(target);
                    if (read.Head.Erased.ContainsKey(id) || !read.Head.Targets.TryGetValue(id, out var root)
                        || root.Registration.Identity.TenantId != tenant || root.Pins.Length != 0 || root.Holders.Length != 0) { return unknown; }
                    selected.Add(id);
                }
                if (selected.Distinct(StringComparer.Ordinal).Count() != selected.Count) { return unknown; }
                int successor = checked(read.Head.Epoch + 1);
                var receipts = request.Targets.Select(target => new DeletionTargetReceipt(target, request.Capability.BatchId,
                    "erc-erased:" + Hash(new { target, request.Capability.BatchId, reservation, digest, installation.StoreId, successor }))).ToArray();
                var original = new ErasureRootManifest(request, reservation, digest, read.Head.Epoch, successor, null, receipts);
                if (!ManifestCapacity(read.Head, original)) { return unknown; }
                if (!await authorization.VerifyManifestAsync(request, reservation, token).ConfigureAwait(false)) { return unknown; }
                if (!await SaveAsync(tenant, read.Version, read.Head with { Pending = original }, token).ConfigureAwait(false)) { continue; }
                // Always reread the independently persisted original before starting native work.
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { token.ThrowIfCancellationRequested(); }
        return unknown;
    }

    private async Task<DeletionManifestProviderResult?> FinishManifestAsync(long version, ErasureRootHead head, CancellationToken token)
    {
        var pending = head.Pending!;
        string tenant = head.TenantId;
        if (!ManifestCapacity(head, pending)) { return null; }
        int actual = await store.ReadEpochAsync(tenant, token).ConfigureAwait(false);
        if (actual < pending.PredecessorEpoch) { return null; }
        if (actual == pending.PredecessorEpoch)
        {
            // Rotate creates versions only. Repeating it after loss can create unused newer versions, but every encrypt/rewrap
            // remains fixed to this original's retained successor and trim never destroys that successor or any newer version.
            await store.RotateAsync(tenant, token).ConfigureAwait(false);
            actual = await store.ReadEpochAsync(tenant, token).ConfigureAwait(false);
        }
        if (actual < pending.SuccessorEpoch) { return null; }
        if (pending.Survivors is null)
        {
            var excluded = pending.Request.Targets.Select(TargetId).ToHashSet(StringComparer.Ordinal);
            var survivors = new SortedDictionary<string, ErasureRootTarget>(StringComparer.Ordinal);
            var retained = head.Targets.Where(entry => !excluded.Contains(entry.Key)).ToArray();
            foreach (var chunk in retained.Chunk(1000))
            {
                var wrapped = await store.RewrapAsync(tenant, pending.SuccessorEpoch, chunk.Select(entry => entry.Value.Wrapped).ToArray(), token).ConfigureAwait(false);
                if (wrapped.Count != chunk.Length) { return null; }
                for (int index = 0; index < chunk.Length; index++)
                {
                    survivors.Add(chunk[index].Key, chunk[index].Value with { Wrapped = chunk[index].Value.Wrapped with { Ciphertext = wrapped[index] } });
                }
            }
            if (survivors.Values.Any(value => value.Wrapped.Ciphertext.Length > 256)) { return null; }
            pending = pending with { Survivors = survivors, SurvivorArchive = Hash(PreparedSurvivors(pending, survivors)) };
            if (!await SaveAsync(tenant, version, head with { Pending = pending }, token).ConfigureAwait(false)) { return null; }
            var reread = await ReadAsync(tenant, token).ConfigureAwait(false);
            if (reread is not { } prepared || prepared.Head.Pending is not { Survivors: not null } exact || Hash(exact) != Hash(pending)) { return null; }
            head = prepared.Head;
            version = prepared.Version;
        }
        if (!ManifestCapacity(head, pending) || pending.Survivors is null || !ValidSurvivors(head, pending, pending.Survivors)) { return null; }
        // No new hold, root issue, lease or release can cross the retained pending original. Only its already-reserved manifest completes.
        if (!await store.VerifyTrimAsync(tenant, pending.SuccessorEpoch, token).ConfigureAwait(false))
        {
            await store.TrimAsync(tenant, pending.SuccessorEpoch, token).ConfigureAwait(false);
        }
        if (!await store.VerifyTrimAsync(tenant, pending.SuccessorEpoch, token).ConfigureAwait(false)) { return null; }
        foreach (var receipt in pending.Receipts)
        {
            string id = TargetId(receipt.Target);
            head.Erased.Add(id, new(head.Targets[id].Registration, receipt, pending.ReservationReceiptId, pending.SuccessorEpoch));
        }
        var completedOriginal = pending with { Survivors = null, SurvivorArchive = null, CompletingQualificationCallDigest = ErasureRootQualificationCallScope.Current };
        head.Manifests.Add(pending.Request.Capability.BatchId, completedOriginal);
        var terminal = head with { Epoch = pending.SuccessorEpoch, Targets = pending.Survivors!, Pending = null };
        if (!await SaveAsync(tenant, version, terminal, token).ConfigureAwait(false)) { return null; }
        // The terminal receipt is returned only from a fresh native read after the irreversible native version inventory check.
        var observed = await ReadAsync(tenant, token).ConfigureAwait(false);
        if (observed is not { } final || final.Head.Pending is not null || !final.Head.ManifestArchives.TryGetValue(pending.Request.Capability.BatchId, out var pointer)) { return null; }
        var original = await ReadManifestAsync(tenant, pending.Request.Capability.BatchId, pointer, final.Head.Epoch, token).ConfigureAwait(false);
        return Hash(original) == Hash(completedOriginal)
            ? new(tenant, pending.Request.Capability.BatchId, pending.ReservationReceiptId, DeletionManifestProviderState.Consumed, original.Receipts) : null;
    }

    /// <inheritdoc />
    public Task<CustodyKeyLifecycleOutcome> ExecuteAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest original, string requestDigest, CancellationToken cancellationToken = default)
        => LifecycleAsync(registration, original, requestDigest, true, cancellationToken);

    /// <inheritdoc />
    public Task<CustodyKeyLifecycleOutcome> LookupAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest original, string requestDigest, CancellationToken cancellationToken = default)
        => LifecycleAsync(registration, original, requestDigest, false, cancellationToken);

    private async Task<CustodyKeyLifecycleOutcome> LifecycleAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest request, string digest, bool effect, CancellationToken token)
    {
        var unknown = new CustodyKeyLifecycleOutcome(request.Identity, request.OperationId, digest, CustodyKeyLifecycleStatus.Unknown);
        if (request.Identity != registration.Identity || Hash(request) != digest) { return unknown with { Status = CustodyKeyLifecycleStatus.Conflict }; }
        try
        {
            string tenant = request.Identity.TenantId;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                if (!await authorization.AuthorizeAsync(tenant, request.OperationId, effect ? "ApplyKeyLifecycle" : "LookupKeyLifecycle", digest, token).ConfigureAwait(false)) { return unknown; }
                var current = await ReadAsync(tenant, token).ConfigureAwait(false);
                if (current is not { } read) { return unknown; }
                if (read.Head.LifecycleOutcomes.TryGetValue(request.OperationId, out var prior))
                { return prior.Identity == request.Identity && prior.RequestDigest == digest ? prior : unknown with { Status = CustodyKeyLifecycleStatus.Conflict }; }
                if (request.Action == CustodyKeyLifecycleAction.Destroy && read.Head.Pending is null
                    && read.Head.Erased.TryGetValue(TargetId(request.Identity), out var covered)
                    && covered.Registration == registration && covered.ReservationReceiptId == request.ProtectionReservationReceiptId
                    && await authorization.VerifyLifecycleAsync(request, token).ConfigureAwait(false))
                {
                    return unknown with { Status = CustodyKeyLifecycleStatus.Destroyed, ReceiptId = covered.Receipt.ReceiptId,
                        RestoreBarrierReceiptId = "erc-trim:" + Hash(new { tenant, installation.StoreId, covered.TrimmedToEpoch, covered.ReservationReceiptId }) };
                }
                if (!effect || read.Head.Pending is not null) { return unknown; }
                if (!read.Head.Targets.TryGetValue(TargetId(request.Identity), out var root) || root.Registration != registration) { return unknown; }
                if (request.Action == CustodyKeyLifecycleAction.Destroy)
                {
                    // Per-key destroy cannot introduce another physical operation or bypass the owner-reserved all-or-none manifest.
                    if (root.Pins.Length > 0)
                    {
                        var refused = unknown with { Status = CustodyKeyLifecycleStatus.BlockedByPin, ReceiptId = "erc-lifecycle-refusal:" + Hash(new { request, digest, root.Pins, installation.StoreId }) };
                        read.Head.LifecycleOutcomes.Add(request.OperationId, refused);
                        if (await SaveAsync(tenant, read.Version, read.Head, token).ConfigureAwait(false)) { return refused; }
                        continue;
                    }
                    return unknown;
                }
                if (string.IsNullOrWhiteSpace(request.HoldId) || !await authorization.VerifyLifecycleAsync(request, token).ConfigureAwait(false)) { return unknown; }
                bool pinned = root.Pins.Contains(request.HoldId, StringComparer.Ordinal);
                if (request.Action == CustodyKeyLifecycleAction.Pin && pinned || request.Action == CustodyKeyLifecycleAction.Unpin && !pinned
                    || request.Action is not (CustodyKeyLifecycleAction.Pin or CustodyKeyLifecycleAction.Unpin))
                { return unknown with { Status = CustodyKeyLifecycleStatus.Conflict }; }
                var outcome = unknown with { Status = request.Action == CustodyKeyLifecycleAction.Pin ? CustodyKeyLifecycleStatus.Pinned : CustodyKeyLifecycleStatus.Unpinned,
                    ReceiptId = "erc-lifecycle:" + Hash(new { request, digest, installation.StoreId }) };
                read.Head.Targets[TargetId(request.Identity)] = root with { Pins = request.Action == CustodyKeyLifecycleAction.Pin
                    ? root.Pins.Append(request.HoldId).Order(StringComparer.Ordinal).ToArray() : root.Pins.Where(id => id != request.HoldId).ToArray() };
                read.Head.LifecycleOutcomes.Add(request.OperationId, outcome);
                if (!await authorization.VerifyLifecycleAsync(request, token).ConfigureAwait(false)) { return unknown; }
                if (await SaveAsync(tenant, read.Version, read.Head, token).ConfigureAwait(false)) { return outcome; }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { token.ThrowIfCancellationRequested(); }
        return unknown;
    }

    internal async Task<(long Version, ErasureRootHead Head)?> ReadAsync(string tenant, CancellationToken token, bool history = false, IReadOnlySet<string>? scopes = null)
    {
        if (installation.DurabilityPolicy != ErasureRootDurabilityPolicy.None || !installation.NativeKeys.ContainsKey(tenant)) { return null; }
        var snapshot = await store.ReadHeadAsync(tenant, token).ConfigureAwait(false);
        if (snapshot is null || snapshot.Version <= 0) { return null; }
        var head = JsonSerializer.Deserialize<ErasureRootHead>(snapshot.Bytes, Json);
        if (head is null || head.TenantId != tenant || head.StoreId != installation.StoreId || head.EnrollmentId != installation.EnrollmentId || head.Epoch < 1
            || head.Targets is null || head.Erased is null || head.Manifests is null || head.LifecycleOutcomes is null || head.Anchors is null
            || head.Occurrences is null || head.SourceOriginals is null || head.SourceReservations is null || head.SealedOriginals is null
            || head.RevocationOriginals is null || head.GuardAttempts is null || head.QualificationAttempts is null || head.QualificationOperations is null) { return null; }
        foreach (var entry in head.Targets)
        {
            var target = entry.Value;
            if (target is null || target.Registration.Identity.TenantId != tenant || TargetId(target.Registration.Identity) != entry.Key
                || target.Registration.Identity.StoreTarget != installation.StoreId || head.Erased.ContainsKey(entry.Key)
                || target.Wrapped.Context != Convert.ToBase64String(CustodyKeyWrapCore.Context(target.Registration.Identity, target.Registration.KekVersion))
                || target.Pins is null || target.Holders is null || target.CompletedHolders is null) { return null; }
        }
        foreach (var entry in head.Erased)
        {
            if (entry.Value.Receipt.Target.TenantId != tenant || TargetId(entry.Value.Receipt.Target) != entry.Key
                || entry.Value.Registration.Identity.TenantId != tenant || TargetId(entry.Value.Registration.Identity) != entry.Key) { return null; }
        }
        if (head.Pending is { } pending && (pending.Request.Capability.TenantId != tenant || pending.PredecessorEpoch != head.Epoch
            || pending.SuccessorEpoch != head.Epoch + 1 || pending.RequestDigest != Hash(new { request = pending.Request, reservation = pending.ReservationReceiptId }))) { return null; }
        if (history)
        {
            await ReadIndexAsync(tenant, head.GuardAdmissionArchives, head.GuardAdmissions, token).ConfigureAwait(false);
            await ReadIndexAsync(tenant, head.GuardAttemptArchives, head.GuardAttempts, token).ConfigureAwait(false);
            await ReadIndexAsync(tenant, head.ConditionalHoldOriginalArchives, head.ConditionalHoldOriginals, token).ConfigureAwait(false);
            foreach (var entry in head.ManifestArchives)
            { head.Manifests.Add(entry.Key, await ReadManifestAsync(tenant, entry.Key, entry.Value, head.Epoch, token).ConfigureAwait(false)); }
        }
        if (head.Pending is { SurvivorArchive: { } survivorPointer } preparedPending)
        {
            var bytes = await ErasureRootArchives.ReadBytesAsync(store, tenant, survivorPointer, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Prepared survivor original unavailable.");
            var prepared = JsonSerializer.Deserialize<ErasureRootPreparedSurvivors>(bytes, Json) ?? throw new InvalidOperationException("Invalid survivor payload.");
            if (Hash(prepared) != survivorPointer || prepared.RequestDigest != preparedPending.RequestDigest || prepared.ReservationReceiptId != preparedPending.ReservationReceiptId
                || prepared.PredecessorEpoch != preparedPending.PredecessorEpoch || prepared.SuccessorEpoch != preparedPending.SuccessorEpoch
                || !ValidSurvivors(head, preparedPending, prepared.Targets)) { return null; }
            head = head with { Pending = preparedPending with { Survivors = prepared.Targets } };
        }
        foreach (var entry in head.Anchors.ToArray())
        {
            if (scopes is not null && !scopes.Contains(entry.Key)) { continue; }
            var anchor = entry.Value;
            foreach (var admission in anchor.AdmissionArchives)
            {
                var bytes = await ErasureRootArchives.ReadBytesAsync(store, tenant, admission.Value, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Admitted ERC original unavailable.");
                var original = JsonSerializer.Deserialize<AnchoredStateTransition>(bytes, Json) ?? throw new InvalidOperationException("Invalid admitted original.");
                if (Hash(original) != admission.Key || admission.Key != admission.Value || original.ScopeId != entry.Key
                    || original.ExpectedRevision != anchor.Revision || original.PredecessorDigest != anchor.Digest) { return null; }
                anchor.Admissions.Add(admission.Key, original);
            }
            if (anchor.ArchiveHead is not null)
            {
                if (anchor.ArchiveRevision != anchor.Revision || anchor.Completed.Count != 0) { return null; }
                var originals = await ErasureRootArchives.ReadTransitionsAsync(store, tenant, entry.Key, anchor, history, token).ConfigureAwait(false);
                head.Anchors[entry.Key] = anchor with { Completed = originals };
            }
        }
        return (snapshot.Version, head);
    }

    private async Task<bool> ValidateOccurrenceWriteSetAsync(string tenant, ErasureRootHead head, CancellationToken token)
    {
        if (head.SourceOriginals.Count + head.Occurrences.Count + head.SourceReservations.Count + head.SealedOriginals.Count > 0)
        {
            string operation = head.OccurrenceIndexRoot is null ? "MigrateOccurrenceIndex" : "RetainOccurrenceIndex";
            string digest = Hash(new { head.OccurrenceIndexRoot, head.SourceOriginals, head.Occurrences, head.SourceReservations, head.SealedOriginals });
            if (!await authorization.AuthorizeAsync(tenant, installation.EnrollmentId, operation, digest, token).ConfigureAwait(false))
            { return false; }
            foreach (var entry in head.SourceOriginals)
            {
                var value = entry.Value;
                if (!ErasureRootOccurrenceBindings.ValidEnrollment(value, value.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(value.Binding.Identity), value.Binding.Identity.Target)
                    || value.Binding.Identity.Target.TenantId != tenant || entry.Key != ErasureRootOccurrenceBindings.Id(value.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(value.Binding.Identity)))
                { throw new InvalidOperationException("Exact legacy source migration required."); }
            }
        }
        return true;
    }

    internal async Task<bool> SaveAsync(string tenant, long version, ErasureRootHead head, CancellationToken token)
    {
        if (!await ValidateOccurrenceWriteSetAsync(tenant, head, token).ConfigureAwait(false))
        { throw new InvalidOperationException("Explicit current same-ERC occurrence retention/migration authorization required."); }
        var anchors = new SortedDictionary<string, ErasureRootAnchor>(StringComparer.Ordinal);
        foreach (var entry in head.Anchors)
        {
            var anchor = entry.Value;
            string? pointer = anchor.ArchiveHead;
            long revision = anchor.ArchiveRevision;
            string? predecessor = null;
            string? originalPointer = anchor.OriginalArchive;
            foreach (var original in anchor.Completed.Values.Where(value => value.TargetRevision > revision).OrderBy(value => value.TargetRevision))
            {
                if (original.ScopeId != entry.Key || original.ExpectedRevision != revision || original.TargetRevision != revision + 1
                    || predecessor is not null && original.PredecessorDigest != predecessor) { throw new InvalidOperationException("Exact legacy archive migration required."); }
                string archivedOriginal = await ErasureRootArchives.WriteAsync(store, tenant, original, token, originalPointer).ConfigureAwait(false);
                var ancestors = await ErasureRootArchives.AncestorsAsync(store, tenant, pointer, original.TargetRevision, token).ConfigureAwait(false);
                pointer = await ErasureRootArchives.WriteAsync(store, tenant, new ErasureRootIndexedTransitionArchive(entry.Key, pointer,
                    archivedOriginal, original.TargetRevision, original.TargetDigest, original.PredecessorDigest, ancestors), token).ConfigureAwait(false);
                originalPointer = archivedOriginal;
                revision = original.TargetRevision; predecessor = original.TargetDigest;
            }
            if (revision != anchor.Revision || predecessor is not null && predecessor != anchor.Digest) { throw new InvalidOperationException("Incomplete current archive binding."); }
            var admissions = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var admission in anchor.Admissions.Where(value => value.Value.TargetRevision > revision))
            {
                string archived = await ErasureRootArchives.WriteAsync(store, tenant, admission.Value, token, originalPointer).ConfigureAwait(false);
                if (archived != admission.Key) { throw new InvalidOperationException("Admission identity conflict."); }
                admissions.Add(admission.Key, archived);
            }
            anchors.Add(entry.Key, anchor with { AdmissionArchives = admissions, ArchiveHead = pointer, ArchiveRevision = revision, OriginalArchive = originalPointer, Completed = new(StringComparer.Ordinal),
                Admissions = new(StringComparer.Ordinal) });
        }
        var manifests = new SortedDictionary<string, string>(head.ManifestArchives, StringComparer.Ordinal);
        foreach (var entry in head.Manifests)
        {
            var original = entry.Value with { Survivors = null, SurvivorArchive = null };
            string pointer = await ErasureRootArchives.WriteAsync(store, tenant, original, token).ConfigureAwait(false);
            if (manifests.TryGetValue(entry.Key, out var retained) && retained != pointer) { throw new InvalidOperationException("Manifest original conflict."); }
            manifests[entry.Key] = pointer;
        }
        var pending = head.Pending;
        if (pending?.Survivors is { } survivors)
        {
            string pointer = await ErasureRootArchives.WriteAsync(store, tenant, PreparedSurvivors(pending, survivors), token).ConfigureAwait(false);
            if (pending.SurvivorArchive is not null && pending.SurvivorArchive != pointer) { throw new InvalidOperationException("Prepared survivor conflict."); }
            pending = pending with { Survivors = null, SurvivorArchive = pointer };
        }
        var signingIntents = new SortedDictionary<string, string>(head.GuardSigningIntents, StringComparer.Ordinal);
        foreach (var entry in head.GuardAdmissions)
        {
            if (entry.Value.Transition.Operation != GovernanceGuardOperation.ReplaceAttestation || entry.Value.Transition.Batch?.SigningRequestId is not { Length: > 0 } signing) { continue; }
            if (signingIntents.TryGetValue(signing, out var prior) && prior != entry.Key) { throw new InvalidOperationException("Ambiguous exact signing admission."); }
            signingIntents[signing] = entry.Key;
        }
        var guardAdmissions = await WriteIndexAsync(tenant, head.GuardAdmissionArchives, head.GuardAdmissions, token).ConfigureAwait(false);
        var guardAttempts = await WriteIndexAsync(tenant, head.GuardAttemptArchives, head.GuardAttempts, token).ConfigureAwait(false);
        var conditionalHolds = await WriteIndexAsync(tenant, head.ConditionalHoldOriginalArchives, head.ConditionalHoldOriginals, token).ConfigureAwait(false);
        string? occurrenceRoot = head.OccurrenceIndexRoot;
        var occurrenceIndex = new ErasureRootOccurrenceIndex(store, installation, tenant);
        foreach (var entry in head.SourceOriginals)
        {
            if (!ErasureRootOccurrenceBindings.ValidEnrollment(entry.Value, entry.Value.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(entry.Value.Binding.Identity), entry.Value.Binding.Identity.Target)
                || entry.Value.Binding.Identity.Target.TenantId != tenant || entry.Key != ErasureRootOccurrenceBindings.Id(entry.Value.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(entry.Value.Binding.Identity)))
            { throw new InvalidOperationException("Exact legacy source migration required."); }
            occurrenceRoot = await occurrenceIndex.WriteAsync(occurrenceRoot, "source:" + entry.Key, entry.Value, token).ConfigureAwait(false);
            string reference = "reference:" + entry.Value.Binding.KeyReference;
            var priorReference = await occurrenceIndex.ReadAsync<ErasureRootOccurrenceReference>(occurrenceRoot, reference, token).ConfigureAwait(false);
            var reverse = priorReference is null ? new ErasureRootOccurrenceReference(entry.Key, false)
                : priorReference with { Ambiguous = priorReference.Ambiguous || priorReference.OccurrenceId != entry.Key };
            occurrenceRoot = await occurrenceIndex.WriteAsync(occurrenceRoot, reference, reverse, token).ConfigureAwait(false);

        }
        foreach (var entry in head.Occurrences) { occurrenceRoot = await occurrenceIndex.WriteAsync(occurrenceRoot, "occurrence:" + entry.Key, entry.Value, token).ConfigureAwait(false); }
        foreach (var entry in head.SourceReservations) { occurrenceRoot = await occurrenceIndex.WriteAsync(occurrenceRoot, "reservation:" + entry.Key, entry.Value, token).ConfigureAwait(false); }
        foreach (var entry in head.SealedOriginals) { occurrenceRoot = await occurrenceIndex.WriteAsync(occurrenceRoot, "sealed:" + entry.Key, entry.Value, token).ConfigureAwait(false); }
        var compact = head with { OccurrenceIndexRoot = occurrenceRoot, SourceOriginals = new(StringComparer.Ordinal), Occurrences = new(StringComparer.Ordinal), SourceReservations = new(StringComparer.Ordinal), SealedOriginals = new(StringComparer.Ordinal), Anchors = anchors, Manifests = new(StringComparer.Ordinal), ManifestArchives = manifests, Pending = pending,
            ConditionalHoldOriginalArchives = conditionalHolds, ConditionalHoldOriginals = new(StringComparer.Ordinal),
            GuardSigningIntents = signingIntents, GuardAdmissionArchives = guardAdmissions, GuardAttemptArchives = guardAttempts, GuardAdmissions = new(StringComparer.Ordinal), GuardAttempts = new(StringComparer.Ordinal) };
        // Every metadata CAS crossing Pending must retain its preparation/terminal reservation. Use the
        // actual compact archive pointers here: hydrated originals would undercount newly archived indexes.
        if (!FitsHead(compact) || compact.Pending is { } reserved && !ManifestCapacity(compact, reserved))
        { throw new InvalidOperationException("ERC head capacity unavailable."); }
        return await store.CompareExchangeHeadAsync(tenant, version, JsonSerializer.SerializeToUtf8Bytes(compact, Json), token).ConfigureAwait(false);
    }
    internal Task<ErasureRootOccurrence?> ReadOccurrenceAsync(ErasureRootHead head, string id, CancellationToken token)
        => ReadOccurrenceValueAsync(head, "occurrence:", head.Occurrences, id, token);
    internal Task<ErasureRootOccurrenceEnrollment?> ReadSourceAsync(ErasureRootHead head, string id, CancellationToken token)
        => ReadOccurrenceValueAsync(head, "source:", head.SourceOriginals, id, token);
    internal Task<string?> ReadReservationAsync(ErasureRootHead head, string id, CancellationToken token)
        => ReadOccurrenceValueAsync(head, "reservation:", head.SourceReservations, id, token);
    internal Task<string?> ReadSealedAsync(ErasureRootHead head, string id, CancellationToken token)
        => ReadOccurrenceValueAsync(head, "sealed:", head.SealedOriginals, id, token);
    internal async Task<ErasureRootOccurrenceEnrollment?> ReadSourceReferenceAsync(ErasureRootHead head, string reference, CancellationToken token)
    {
        var reverse = await new ErasureRootOccurrenceIndex(store, installation, head.TenantId).ReadAsync<ErasureRootOccurrenceReference>(head.OccurrenceIndexRoot, "reference:" + reference, token).ConfigureAwait(false);
        if (reverse is { Ambiguous: false })
        {
            var enrolled = await ReadSourceAsync(head, reverse.OccurrenceId, token).ConfigureAwait(false);
            if (enrolled?.Binding.KeyReference != reference) { throw new InvalidOperationException("Reverse source identity conflict."); }
            return enrolled;
        }
        if (reverse is { Ambiguous: true }) { return null; }
        var candidates = head.SourceOriginals.Values.Select(value => value.Binding)
            .Where(value => value.KeyReference == reference).DistinctBy(value => Hash(value)).ToArray();
        if (candidates.Length != 1) { return null; }
        var binding = candidates[0];
        var value = await ReadSourceAsync(head, ErasureRootOccurrenceBindings.Id(binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(binding.Identity)), token).ConfigureAwait(false);
        return value?.Binding == binding ? value : null;
    }
    private async Task<T?> ReadOccurrenceValueAsync<T>(ErasureRootHead head, string kind, SortedDictionary<string, T> values, string id, CancellationToken token) where T : class
    {
        if (values.TryGetValue(id, out var legacy)) { return legacy; }
        var value = await new ErasureRootOccurrenceIndex(store, installation, head.TenantId).ReadAsync<T>(head.OccurrenceIndexRoot, kind + id, token).ConfigureAwait(false);
        if (value is not null) { values.Add(id, value); }
        return value;
    }
    internal async Task<SortedDictionary<string, AnchoredStateTransition>> ReadHistoryAsync(string tenant, string scope, ErasureRootAnchor anchor, CancellationToken token)
        => await ErasureRootArchives.ReadTransitionsAsync(store, tenant, scope, anchor, true, token).ConfigureAwait(false);
    internal Task<AnchoredStateTransition?> ReadHistoricalAsync(string tenant, string scope, ErasureRootAnchor anchor, long revision, CancellationToken token)
        => ErasureRootArchives.ReadTransitionAtAsync(store, tenant, scope, anchor, revision, token);
    internal async Task<bool> HasCompletedHolderAsync(string tenant, ErasureRootTarget target, string holder, CancellationToken token)
    {
        if (target.CompletedHolders.Contains(holder, StringComparer.Ordinal)) { return true; }
        string? pointer = target.CompletedHolderArchive;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (pointer is not null)
        {
            if (!seen.Add(pointer)) { throw new InvalidOperationException("Cyclic holder archive."); }
            var bytes = await ErasureRootArchives.ReadBytesAsync(store, tenant, pointer, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Committed holder archive unavailable.");
            var node = JsonSerializer.Deserialize<ErasureRootHolderArchive>(bytes, Json) ?? throw new InvalidOperationException("Invalid holder archive.");
            if (Hash(node) != pointer || node.Registration != target.Registration) { throw new InvalidOperationException("Holder archive ancestry conflict."); }
            if (node.HolderId == holder) { return true; } pointer = node.Previous;
        }
        return false;
    }
    private static ErasureRootPreparedSurvivors PreparedSurvivors(ErasureRootManifest original, SortedDictionary<string, ErasureRootTarget> survivors)
        => new(original.RequestDigest, original.ReservationReceiptId, original.PredecessorEpoch, original.SuccessorEpoch, survivors);
    private static bool ValidSurvivors(ErasureRootHead head, ErasureRootManifest original, SortedDictionary<string, ErasureRootTarget> survivors)
    {
        var excluded = original.Request.Targets.Select(TargetId).ToHashSet(StringComparer.Ordinal);
        if (!head.Targets.Keys.Where(key => !excluded.Contains(key)).Order(StringComparer.Ordinal).SequenceEqual(survivors.Keys.Order(StringComparer.Ordinal))) { return false; }
        foreach (var entry in survivors)
        {
            var before = head.Targets[entry.Key]; var after = entry.Value;
            if (after.Registration != before.Registration || after.Wrapped.Context != before.Wrapped.Context || !after.Pins.SequenceEqual(before.Pins)
                || !after.Holders.SequenceEqual(before.Holders) || !after.CompletedHolders.SequenceEqual(before.CompletedHolders) || after.CompletedHolderArchive != before.CompletedHolderArchive
                || after.Wrapped.Ciphertext.Length > 256 || !after.Wrapped.Ciphertext.StartsWith("vault:v" + original.SuccessorEpoch.ToString(CultureInfo.InvariantCulture) + ":", StringComparison.Ordinal)) { return false; }
        }
        return true;
    }
    internal Task<ErasureRootGuardAttempt?> ReadGuardAttemptAsync(string tenant, ErasureRootHead head, string intent, CancellationToken token)
        => ReadIndexedAsync(tenant, head.GuardAttemptArchives, head.GuardAttempts, intent, token);
    internal Task<ErasureRootGuardAdmission?> ReadGuardAdmissionAsync(string tenant, ErasureRootHead head, string intent, CancellationToken token)
        => ReadIndexedAsync(tenant, head.GuardAdmissionArchives, head.GuardAdmissions, intent, token);
    internal Task<System.Text.Json.Nodes.JsonObject?> ReadConditionalHoldOriginalAsync(string tenant, ErasureRootHead head, string id, CancellationToken token)
        => ReadIndexedAsync(tenant, head.ConditionalHoldOriginalArchives, head.ConditionalHoldOriginals, id, token);
    private async Task<T?> ReadIndexedAsync<T>(string tenant, SortedDictionary<string, string> pointers, SortedDictionary<string, T> originals, string id, CancellationToken token) where T : class
    {
        if (originals.TryGetValue(id, out var legacy)) { return legacy; }
        if (!pointers.TryGetValue(id, out var pointer)) { return null; }
        byte[] bytes = await ErasureRootArchives.ReadBytesAsync(store, tenant, pointer, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Committed ERC original unavailable.");
        var original = JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new InvalidOperationException("Invalid committed ERC original.");
        if (Hash(original) != pointer) { throw new InvalidOperationException("Committed ERC original identity conflict."); }
        originals.Add(id, original);
        return original;
    }
    private async Task ReadIndexAsync<T>(string tenant, SortedDictionary<string, string> pointers, SortedDictionary<string, T> originals, CancellationToken token)
    {
        foreach (var entry in pointers)
        {
            byte[] bytes = await ErasureRootArchives.ReadBytesAsync(store, tenant, entry.Value, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Committed ERC original unavailable.");
            var original = JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new InvalidOperationException("Invalid committed ERC original.");
            if (Hash(original) != entry.Value || originals.ContainsKey(entry.Key)) { throw new InvalidOperationException("Committed ERC original identity conflict."); }
            originals.Add(entry.Key, original);
        }
    }
    private async Task<SortedDictionary<string, string>> WriteIndexAsync<T>(string tenant, SortedDictionary<string, string> pointers, SortedDictionary<string, T> originals, CancellationToken token)
    {
        var result = new SortedDictionary<string, string>(pointers, StringComparer.Ordinal);
        foreach (var entry in originals)
        {
            string digest = Hash(entry.Value);
            if (result.TryGetValue(entry.Key, out var prior))
            { if (prior != digest) { throw new InvalidOperationException("Immutable ERC original changed."); } continue; }
            result.Add(entry.Key, await ErasureRootArchives.WriteAsync(store, tenant, entry.Value, token).ConfigureAwait(false));
        }
        return result;
    }
    internal async Task<ErasureRootManifest?> ReadManifestOriginalAsync(string tenant, ErasureRootHead head, string batch, CancellationToken token)
    {
        if (head.Manifests.TryGetValue(batch, out var original)) { return original; }
        if (!head.ManifestArchives.TryGetValue(batch, out var pointer)) { return null; }
        var retained = await ReadManifestAsync(tenant, batch, pointer, head.Epoch, token).ConfigureAwait(false);
        head.Manifests.Add(batch, retained); return retained;
    }
    internal Task<ErasureRootNativePolicy> ReadNativePolicyAsync(string tenant, CancellationToken token) => store.ReadPolicyAsync(tenant, token);
    internal async Task<System.Text.Json.Nodes.JsonNode?> ReadNativeCallResultAsync(string tenant, ProtectionQualificationNativeCall call, CancellationToken token)
    {
        if (call.OperationId is null || call.RequestSha256 is null) { return null; }
        if (!await authorization.AuthorizeAsync(tenant, call.Id, "ReadNativeCallOriginal", Hash(call), token).ConfigureAwait(false)) { return null; }
        var read = await ReadAsync(tenant, token).ConfigureAwait(false);
        return read is { } current ? await new ErasureRootOccurrenceIndex(store, installation, tenant).ReadAsync<System.Text.Json.Nodes.JsonObject>(current.Head.QualificationEvidenceIndexRoot, "call-result:" + Hash(call), token).ConfigureAwait(false) : null;
    }
    internal async Task RetainNativeCallResultAsync(string tenant, ProtectionQualificationNativeCall call, System.Text.Json.Nodes.JsonObject result, CancellationToken token)
    {
        if (call.OperationId is null || call.RequestSha256 is null) { throw new InvalidOperationException("Exact admitted call identity required."); }
        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (!await authorization.AuthorizeAsync(tenant, call.Id, "RetainNativeCallOriginal", Hash(call), token).ConfigureAwait(false)) { throw new InvalidOperationException("Native original retention permission unavailable."); }
            var read = await ReadAsync(tenant, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Current native original authority required.");
            var index = new ErasureRootOccurrenceIndex(store, installation, tenant);
            string key = "call-result:" + Hash(call);
            var prior = await index.ReadAsync<System.Text.Json.Nodes.JsonObject>(read.Head.QualificationEvidenceIndexRoot, key, token).ConfigureAwait(false);
            if (prior is not null)
            { if (!System.Text.Json.Nodes.JsonNode.DeepEquals(prior, result)) { throw new InvalidOperationException("Native call original conflict."); } return; }
            string root = await index.WriteAsync(read.Head.QualificationEvidenceIndexRoot, key, result, token).ConfigureAwait(false);
            if (await SaveAsync(tenant, read.Version, read.Head with { QualificationEvidenceIndexRoot = root }, token).ConfigureAwait(false)) { return; }
        }
        throw new InvalidOperationException("Native call original unresolved.");
    }
    internal async Task<ErasureRootEffectBoundaryEvidence?> ReadEffectBoundaryAsync(string tenant, ErasureRootHead head, string call, string boundary, CancellationToken token)
        => await new ErasureRootOccurrenceIndex(store, installation, tenant).ReadAsync<ErasureRootEffectBoundaryEvidence>(head.QualificationEvidenceIndexRoot, "effect:" + call + ":" + boundary, token).ConfigureAwait(false);
    internal async Task<ErasureRootEffectBoundaryEvidence?> CaptureEffectBoundaryAsync(string tenant, string call, string boundary, bool recovery, CancellationToken token)
    {
        if (call.Length != 64 || call.Any(value => !char.IsAsciiHexDigit(value)) || boundary is not ("before" or "after")) { return null; }
        for (int attempt = 0; attempt < 16; attempt++)
        {
            var read = await ReadAsync(tenant, token).ConfigureAwait(false);
            if (read is not { } current) { return null; }
            var prior = await ReadEffectBoundaryAsync(tenant, current.Head, call, boundary, token).ConfigureAwait(false);
            if (prior is not null) { return prior; }
            if (recovery) { return null; }
            var policy = await store.ReadPolicyAsync(tenant, token).ConfigureAwait(false);
            string state = Hash(new { current.Head.Epoch, current.Head.Pending, current.Head.Targets, current.Head.Erased, current.Head.OccurrenceIndexRoot,
                current.Head.Occurrences, current.Head.SourceOriginals, current.Head.SourceReservations, current.Head.SealedOriginals, current.Head.ManifestArchives, current.Head.Manifests });
            var confirmed = await store.ReadHeadAsync(tenant, token).ConfigureAwait(false);
            if (confirmed?.Version != current.Version || Hash(policy) != Hash(await store.ReadPolicyAsync(tenant, token).ConfigureAwait(false))) { continue; }
            string fingerprint = Hash(new { CustodyStateSha256 = state, NativePolicy = policy });
            string receipt = "erc-effect:" + Hash(new { tenant, installation.StoreId, installation.EnrollmentId, call, boundary, fingerprint });
            var evidence = new ErasureRootEffectBoundaryEvidence(tenant, installation.StoreId, installation.EnrollmentId, call, boundary, state, policy, fingerprint, receipt);
            string root = await new ErasureRootOccurrenceIndex(store, installation, tenant).WriteAsync(current.Head.QualificationEvidenceIndexRoot, "effect:" + call + ":" + boundary, evidence, token).ConfigureAwait(false);
            if (await SaveAsync(tenant, current.Version, current.Head with { QualificationEvidenceIndexRoot = root }, token).ConfigureAwait(false)) { return evidence; }
        }
        return null;
    }
    internal Task<ErasureRootHeadSnapshot?> ReadObservationVersionAsync(string tenant, CancellationToken token) => store.ReadHeadAsync(tenant, token);
    private async Task<ErasureRootManifest> ReadManifestAsync(string tenant, string batch, string pointer, int epoch, CancellationToken token)
    {
        var bytes = await ErasureRootArchives.ReadBytesAsync(store, tenant, pointer, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Committed manifest unavailable.");
        var manifest = JsonSerializer.Deserialize<ErasureRootManifest>(bytes, Json) ?? throw new InvalidOperationException("Invalid manifest archive.");
        if (Hash(manifest) != pointer || manifest.Request.Capability.BatchId != batch || manifest.Request.Capability.TenantId != tenant
            || manifest.Survivors is not null || manifest.SurvivorArchive is not null || manifest.SuccessorEpoch > epoch
            || manifest.RequestDigest != Hash(new { request = manifest.Request, reservation = manifest.ReservationReceiptId }))
        { throw new InvalidOperationException("Committed manifest identity conflict."); }
        return manifest;
    }
    private static bool FitsHead(ErasureRootHead head) => JsonSerializer.SerializeToUtf8Bytes(head, Json).Length <= OpenBaoErasureRootStore.MaximumHeadBytes;
    private static bool ManifestCapacity(ErasureRootHead head, ErasureRootManifest original)
    {
        // Reserve worst supported native ciphertext framing and terminal indexes before rotation. Archives are bounded separately.
        var erased = new SortedDictionary<string, ErasureRootErasure>(head.Erased, StringComparer.Ordinal);
        foreach (var receipt in original.Receipts)
        {
            string id = TargetId(receipt.Target);
            if (!head.Targets.TryGetValue(id, out var target)) { return false; }
            erased[id] = new(target.Registration, receipt, original.ReservationReceiptId, original.SuccessorEpoch);
        }
        var excluded = original.Request.Targets.Select(TargetId).ToHashSet(StringComparer.Ordinal);
        var survivors = new SortedDictionary<string, ErasureRootTarget>(StringComparer.Ordinal);
        foreach (var entry in head.Targets.Where(value => !excluded.Contains(value.Key)))
        { survivors.Add(entry.Key, entry.Value with { Wrapped = entry.Value.Wrapped with { Ciphertext = new string('X', 256) } }); }
        if (JsonSerializer.SerializeToUtf8Bytes(PreparedSurvivors(original, survivors), Json).Length > ErasureRootArchives.MaximumOriginalBytes) { return false; }
        var manifests = new SortedDictionary<string, string>(head.ManifestArchives, StringComparer.Ordinal);
        foreach (var entry in head.Manifests) { manifests[entry.Key] = new string('0', 64); }
        manifests[original.Request.Capability.BatchId] = new string('0', 64);
        var anchors = new SortedDictionary<string, ErasureRootAnchor>(StringComparer.Ordinal);
        foreach (var entry in head.Anchors)
        { anchors.Add(entry.Key, entry.Value with { Completed = new(StringComparer.Ordinal), Admissions = new(StringComparer.Ordinal) }); }
        var compact = head with { Anchors = anchors, Manifests = new(StringComparer.Ordinal), ManifestArchives = manifests,
            GuardAdmissions = new(StringComparer.Ordinal), GuardAttempts = new(StringComparer.Ordinal), ConditionalHoldOriginals = new(StringComparer.Ordinal) };
        return FitsHead(compact with { Pending = original with { Survivors = null, SurvivorArchive = new string('0', 64) } })
            && FitsHead(compact with { Pending = null, Targets = survivors, Erased = erased, Epoch = original.SuccessorEpoch });
    }
    internal static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    internal static string TargetId(CustodyKeyObjectIdentity identity) => TargetId(new ProtectionTarget(identity.TenantId, identity.ObjectId, identity.KeyAlias));
    internal static string TargetId(ProtectionTarget target) => Hash(target);
    private void ValidateIdentity(CustodyKeyObjectIdentity identity)
    {
        CustodyKeyLifecycleActor.ValidateIdentity(identity);
        if (identity.Purpose != PlatformKeyPurpose.InteractionRootDek || identity.StoreTarget != installation.StoreId)
        { throw new ArgumentException("Exact interaction root in the enrolled ERC required."); }
    }
}
