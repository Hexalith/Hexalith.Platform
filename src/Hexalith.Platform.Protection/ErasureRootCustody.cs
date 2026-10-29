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
    internal static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    /// <summary>Creates exactly one native-wrapped random root for the complete target. Exact retry returns its original registration; erased identities can never be issued again.</summary>
    public async Task<CustodyKeyRegistration?> WrapAsync(CustodyKeyObjectIdentity identity, string operationId,
        long expectedLifecycleRevision, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (expectedLifecycleRevision < 0 || operationId.Length > 2048) { throw new ArgumentException("Bounded original wrap operation and nonnegative lifecycle revision required."); }
        string digest = Hash(new { identity, operationId, expectedLifecycleRevision });
        try
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                if (!await authorization.AuthorizeAsync(identity.TenantId, operationId, "WrapDek", digest, cancellationToken).ConfigureAwait(false)) { return null; }
                var current = await ReadAsync(identity.TenantId, cancellationToken).ConfigureAwait(false);
                if (current is not { } read || read.Head.Pending is not null) { return null; }
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
                    || target.Holders.Contains(holderId, StringComparer.Ordinal) || target.CompletedHolders.Contains(holderId, StringComparer.Ordinal)) { return null; }
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
        if (holder.Bytes.Length != 32 || holder.Bytes.AsSpan().IndexOfAnyExcept((byte)0) >= 0) { return false; }
        string tenant = registration.Identity.TenantId;
        string digest = Hash(new { registration, holder.HolderId });
        try
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                if (!await authorization.AuthorizeAsync(tenant, holder.HolderId, "CompleteRootHolder", digest, cancellationToken).ConfigureAwait(false)) { return false; }
                var current = await ReadAsync(tenant, cancellationToken).ConfigureAwait(false);
                if (current is not { } read || read.Head.Pending is not null
                    || !read.Head.Targets.TryGetValue(TargetId(registration.Identity), out var target) || target.Registration != registration) { return false; }
                if (target.CompletedHolders.Contains(holder.HolderId, StringComparer.Ordinal)) { return true; }
                if (!target.Holders.Contains(holder.HolderId, StringComparer.Ordinal)) { return false; }
                read.Head.Targets[TargetId(registration.Identity)] = target with { Holders = target.Holders.Where(id => id != holder.HolderId).ToArray(),
                    CompletedHolders = target.CompletedHolders.Append(holder.HolderId).ToArray() };
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
            pending = pending with { Survivors = survivors };
            if (!await SaveAsync(tenant, version, head with { Pending = pending }, token).ConfigureAwait(false)) { return null; }
            var reread = await ReadAsync(tenant, token).ConfigureAwait(false);
            if (reread is not { } prepared || prepared.Head.Pending is not { Survivors: not null } exact || Hash(exact) != Hash(pending)) { return null; }
            head = prepared.Head;
            version = prepared.Version;
        }
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
        head.Manifests.Add(pending.Request.Capability.BatchId, pending);
        var terminal = head with { Epoch = pending.SuccessorEpoch, Targets = pending.Survivors!, Pending = null };
        if (!await SaveAsync(tenant, version, terminal, token).ConfigureAwait(false)) { return null; }
        // The terminal receipt is returned only from a fresh native read after the irreversible native version inventory check.
        var observed = await ReadAsync(tenant, token).ConfigureAwait(false);
        return observed is { } final && final.Head.Pending is null
            && final.Head.Manifests.TryGetValue(pending.Request.Capability.BatchId, out var original) && Hash(original) == Hash(pending)
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
                    if (root.Pins.Length > 0) { return unknown with { Status = CustodyKeyLifecycleStatus.BlockedByPin }; }
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

    internal async Task<(long Version, ErasureRootHead Head)?> ReadAsync(string tenant, CancellationToken token)
    {
        if (installation.DurabilityPolicy != ErasureRootDurabilityPolicy.None || !installation.NativeKeys.ContainsKey(tenant)) { return null; }
        var snapshot = await store.ReadHeadAsync(tenant, token).ConfigureAwait(false);
        if (snapshot is null || snapshot.Version <= 0) { return null; }
        var head = JsonSerializer.Deserialize<ErasureRootHead>(snapshot.Bytes, Json);
        if (head is null || head.TenantId != tenant || head.StoreId != installation.StoreId || head.EnrollmentId != installation.EnrollmentId || head.Epoch < 1
            || head.Targets is null || head.Erased is null || head.Manifests is null || head.LifecycleOutcomes is null || head.Anchors is null) { return null; }
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
        return (snapshot.Version, head);
    }

    internal Task<bool> SaveAsync(string tenant, long version, ErasureRootHead head, CancellationToken token)
        => store.CompareExchangeHeadAsync(tenant, version, JsonSerializer.SerializeToUtf8Bytes(head, Json), token);
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
