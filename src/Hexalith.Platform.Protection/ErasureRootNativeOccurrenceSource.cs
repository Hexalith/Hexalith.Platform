using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;

namespace Hexalith.Platform.Protection;

/// <summary>Owned source-original endpoint implementation over the ERC and actual native source-owner proof cells. Local absence never establishes writer completion or enrollment.</summary>
public sealed class ErasureRootNativeOccurrenceSource(ErasureRootCustody custody, IErasureRootAuthorization authorization, DaprClient sourceStore,
    ErasureRootDigestKeyResolver digests, IReadOnlyDictionary<string, ProtectionQualificationNativeCell>? independentOriginals = null) : IErasureRootOccurrenceSource
{
    private readonly ErasureRootQuiescence _quiescence = new();
    internal void BeginQuiescence() { _quiescence.Close(); custody.BeginQuiescence(); }
    internal Task WaitForQuiescenceAsync(CancellationToken token) => _quiescence.WaitAsync(token);

    /// <summary>Content-free health of this receiver's disposed verifier backlog.</summary>
    public ErasureRootCompletionDiagnostics CompletionDiagnostics => custody.CompletionDiagnostics;
    /// <summary>Completes only this receiver's custody queue using host lifetime cancellation, never the canceled verification request.</summary>
    public Task<int> CompleteDisposedVerifierHoldersAsync(CancellationToken cancellationToken = default)
        => custody.CompleteDisposedHoldersAsync(cancellationToken);
    /// <inheritdoc />
    public async Task<byte[]?> ReadErasedSnapshotSkeletonAsync(InteractionProtectionBinding binding, string sealedPayloadSha256, ErasureRootPermanentLossReceipt loss, CancellationToken token)
    {
        if (binding.Identity.Target.TenantId != loss.TenantId || independentOriginals is null || !independentOriginals.TryGetValue(loss.TenantId, out var location)) { return null; }
        var original = await ReadIndependentAsync(binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(binding.Identity), token).ConfigureAwait(false);
        if (original?.Binding != binding || original.RootIdentity.StoreTarget != loss.StoreId || !await VerifyPersistedSourceAsync(original, sealedPayloadSha256, token).ConfigureAwait(false)) { return null; }
        var state = await sourceStore.GetStateAsync<ErasureRootSourceOriginals>(location.Component, location.Key, ConsistencyMode.Strong, location.Metadata, token).ConfigureAwait(false);
        if (!ValidIndependent(state, loss.TenantId, loss.StoreId)) { return null; }
        state!.SnapshotOriginals.TryGetValue(binding.KeyReference, out var snapshot);
        snapshot ??= await Index(location, loss.TenantId).ReadAsync<InteractionOccurrenceSealedResult>(state.SnapshotIndexRoot, binding.KeyReference, token).ConfigureAwait(false);
        return snapshot?.SnapshotReplaySkeleton is { } skeleton && snapshot.KeyReference == binding.KeyReference
            && Convert.ToHexString(SHA256.HashData(snapshot.PayloadBytes)) == sealedPayloadSha256 ? skeleton.ToArray() : null;
    }
    private async Task<bool> RetainSnapshotAsync(ErasureRootOccurrenceEnrollment enrollment, InteractionOccurrenceSealedResult original, CancellationToken token)
    {
        if (original.SnapshotReplaySkeleton is null) { return true; }
        string tenant = enrollment.Binding.Identity.Target.TenantId;
        if (independentOriginals is null || !independentOriginals.TryGetValue(tenant, out var location)) { return false; }
        for (int attempt = 0; attempt < 16; attempt++)
        {
            var (state, tag) = await sourceStore.GetStateAndETagAsync<ErasureRootSourceOriginals>(location.Component, location.Key, ConsistencyMode.Strong, location.Metadata, token).ConfigureAwait(false);
            if (!ValidIndependent(state, tenant, enrollment.RootIdentity.StoreTarget) || string.IsNullOrWhiteSpace(tag)) { return false; }
            if (state!.SnapshotOriginals.TryGetValue(original.KeyReference, out var prior)) { return ErasureRootCustody.Hash(prior) == ErasureRootCustody.Hash(original); }
            string root = await Index(location, tenant).RetainAsync(state.SnapshotIndexRoot, original.KeyReference, original, token).ConfigureAwait(false);
            if (root == state.SnapshotIndexRoot) { return true; }
            state = state with { EnrollmentId = custody.Installation.EnrollmentId, SnapshotIndexRoot = root };
            if (await sourceStore.TrySaveStateAsync(location.Component, location.Key, state, tag, new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong }, location.Metadata, token).ConfigureAwait(false)) { return true; }
        }
        return false;
    }
    private async Task<bool> RetainIndependentAsync(ErasureRootOccurrenceEnrollment enrollment, CancellationToken token)
    {
        string tenant = enrollment.Binding.Identity.Owner.TenantId;
        if (independentOriginals is null || !independentOriginals.TryGetValue(tenant, out var location)) { return false; }
        string id = ErasureRootOccurrenceBindings.Id(enrollment.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(enrollment.Binding.Identity));
        for (int attempt = 0; attempt < 16; attempt++)
        {
            var (state, tag) = await sourceStore.GetStateAndETagAsync<ErasureRootSourceOriginals>(location.Component, location.Key, ConsistencyMode.Strong, location.Metadata, token).ConfigureAwait(false);
            if (!ValidIndependent(state, tenant, enrollment.RootIdentity.StoreTarget) || string.IsNullOrWhiteSpace(tag)) { return false; }
            if (state!.Originals.TryGetValue(id, out var prior)) { return ErasureRootCustody.Hash(prior) == ErasureRootCustody.Hash(enrollment); }
            string root = await Index(location, tenant).RetainAsync(state.OriginalIndexRoot, id, enrollment, token).ConfigureAwait(false);
            if (root == state.OriginalIndexRoot) { return true; }
            state = state with { EnrollmentId = custody.Installation.EnrollmentId, OriginalIndexRoot = root };
            if (await sourceStore.TrySaveStateAsync(location.Component, location.Key, state, tag, new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong }, location.Metadata, token).ConfigureAwait(false)) { return true; }
        }
        return false;
    }
    private async Task<ErasureRootOccurrenceEnrollment?> ReadIndependentAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence, CancellationToken token)
    {
        if (independentOriginals is null || !independentOriginals.TryGetValue(owner.TenantId, out var location)) { return null; }
        var state = await sourceStore.GetStateAsync<ErasureRootSourceOriginals>(location.Component, location.Key, ConsistencyMode.Strong, location.Metadata, token).ConfigureAwait(false);
        string id = ErasureRootOccurrenceBindings.Id(owner, occurrence);
        if (!ValidIndependent(state, owner.TenantId, custody.Installation.StoreId)) { return null; }
        state!.Originals.TryGetValue(id, out var original);
        original ??= await Index(location, owner.TenantId).ReadAsync<ErasureRootOccurrenceEnrollment>(state.OriginalIndexRoot, id, token).ConfigureAwait(false);
        return original is not null && ErasureRootOccurrenceBindings.ValidEnrollment(original, owner, occurrence, original.Binding.Identity.Target)
            && original.RootIdentity.StoreTarget == state.StoreId ? original : null;
    }
    private ErasureRootSourceIndex Index(ProtectionQualificationNativeCell location, string tenant)
        => new(sourceStore, location, tenant, custody.Installation.StoreId, custody.Installation.EnrollmentId);
    private bool ValidIndependent(ErasureRootSourceOriginals? state, string tenant, string store)
        => state is not null && state.TenantId == tenant && state.StoreId == store && store == custody.Installation.StoreId
            && (state.EnrollmentId is null && state.OriginalIndexRoot is null && state.SnapshotIndexRoot is null
                || state.EnrollmentId == custody.Installation.EnrollmentId);
    /// <summary>Reads the actual independently persisted source commit cell after root storage loss; no ERC state, absence or caller metadata establishes persistence.</summary>
    public async Task<bool> VerifyPersistedSourceAsync(ErasureRootOccurrenceEnrollment enrollment, string sealedPayloadSha256, CancellationToken token)
    {
        if (enrollment.Writer is not { } writer || sealedPayloadSha256 is not { Length: 64 }) { return false; }
        var proof = await sourceStore.GetStateAsync<ErasureRootWriterProof>(writer.Component, writer.Key, ConsistencyMode.Strong, writer.Metadata, token).ConfigureAwait(false);
        return proof is { Persisted: true, WriterRetired: true } && proof.Binding == enrollment.Binding && proof.SealedPayloadSha256 == sealedPayloadSha256
            && !string.IsNullOrWhiteSpace(proof.ReceiptId);
    }
    /// <summary>Reads only a previously retained exact reservation proof; recovery cannot admit a missing source intent.</summary>
    public async Task<bool> LookupReservationAsync(InteractionOccurrenceRequest request, CancellationToken token)
    {
        string id = ErasureRootOccurrenceBindings.Id(request.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(request.Identity));
        string digest = ErasureRootCustody.Hash(request);
        if (!await authorization.AuthorizeAsync(request.Identity.Target.TenantId, id, "LookupIndependentSourceReservation", digest, token).ConfigureAwait(false)) { return false; }
        var read = await custody.ReadAsync(request.Identity.Target.TenantId, token).ConfigureAwait(false);
        return read is { } head && (await custody.ReadReservationAsync(head.Head, id, token).ConfigureAwait(false) is { } retained) && retained == digest;
    }
    /// <summary>Reads only a previously authenticated complete sealed output, without another root unwrap or verification mutation.</summary>
    public async Task<bool> LookupSealedAsync(InteractionOccurrenceRecord original, InteractionOccurrenceSealedResult result, CancellationToken token)
    {
        string id = ErasureRootOccurrenceBindings.Id(original.Request.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Request.Identity));
        string digest = ErasureRootCustody.Hash(new { original.Request, result });
        if (!await authorization.AuthorizeAsync(original.Request.Identity.Target.TenantId, id, "LookupNativeEngineSealedOriginal", digest, token).ConfigureAwait(false)) { return false; }
        var read = await custody.ReadAsync(original.Request.Identity.Target.TenantId, token).ConfigureAwait(false);
        return read is { } head && head.Head.Pending is null && await custody.ReadErasedAsync(head.Head, ErasureRootCustody.TargetId(original.Request.Identity.Target), token).ConfigureAwait(false) is null
            && (await custody.ReadSealedAsync(head.Head, id, token).ConfigureAwait(false) is { } retained) && retained == digest;
    }
    /// <summary>Registers only a separately authorized source-owner original before root issue. Exact retry cannot replace its target or source receipt location.</summary>
    public async Task<bool> RegisterOriginalAsync(ErasureRootOccurrenceEnrollment enrollment, CancellationToken token)
    {
        var identity = enrollment.Binding.Identity;
        string id = ErasureRootOccurrenceBindings.Id(identity.Owner, ErasureRootOccurrenceBindings.Occurrence(identity));
        string digest = ErasureRootCustody.Hash(enrollment);
        if (enrollment.Writer is null || !ErasureRootOccurrenceBindings.ValidEnrollment(enrollment, identity.Owner,
            ErasureRootOccurrenceBindings.Occurrence(identity), identity.Target)) { return false; }
        using var nativeDigest = await digests.ResolveAsync(enrollment, enrollment.Binding.DigestKeyVersion, token).ConfigureAwait(false);
        if (nativeDigest is null || !await custody.PreflightWrapAsync(enrollment.RootIdentity, enrollment.RootOperationId, enrollment.ExpectedLifecycleRevision, token).ConfigureAwait(false)
            || !await ErasureRootOccurrenceBindings.PreflightPermissionsAsync(authorization, enrollment, token).ConfigureAwait(false)) { return false; }
        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (!await authorization.AuthorizeAsync(identity.Target.TenantId, id, "RegisterIndependentSourceOriginal", digest, token).ConfigureAwait(false)) { return false; }
            var read = await custody.ReadAsync(identity.Target.TenantId, token).ConfigureAwait(false);
            if (read is not { } current || current.Head.Pending is not null || await custody.ReadErasedAsync(current.Head, ErasureRootCustody.TargetId(identity.Target), token).ConfigureAwait(false) is not null) { return false; }
            if (!await RetainIndependentAsync(enrollment, token).ConfigureAwait(false)) { return false; }
            if ((await custody.ReadSourceAsync(current.Head, id, token).ConfigureAwait(false) is { } prior)) { return ErasureRootCustody.Hash(prior) == digest; }
            current.Head.SourceOriginals.Add(id, enrollment);
            if (await custody.SaveAsync(identity.Target.TenantId, current.Version, current.Head, token).ConfigureAwait(false)) { return true; }
        }
        return false;
    }
    /// <inheritdoc />
    public async Task<ErasureRootOccurrenceEnrollment?> ResolveAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence, CancellationToken token)
    {
        string id = ErasureRootOccurrenceBindings.Id(owner, occurrence);
        if (!await authorization.AuthorizeAsync(owner.TenantId, id, "ReadIndependentSourceOriginal", ErasureRootCustody.Hash(new { owner, occurrence }), token).ConfigureAwait(false)) { return null; }
        var sourceOriginal = await ReadIndependentAsync(owner, occurrence, token).ConfigureAwait(false);
        if (sourceOriginal is not null) { return sourceOriginal; }
        var read = await custody.ReadAsync(owner.TenantId, token).ConfigureAwait(false);
        return read is { } current ? await custody.ReadSourceAsync(current.Head, id, token).ConfigureAwait(false) : null;
    }
    /// <inheritdoc />
    public async Task<bool> VerifyEnrollmentAsync(ErasureRootOccurrenceEnrollment enrollment, CancellationToken token)
    {
        string tenant = enrollment.Binding.Identity.Target.TenantId;
        string id = ErasureRootOccurrenceBindings.Id(enrollment.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(enrollment.Binding.Identity));
        if (!await authorization.AuthorizeAsync(tenant, id, "VerifyCommittedSourceEnrollment", ErasureRootCustody.Hash(enrollment), token).ConfigureAwait(false)) { return false; }
        var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
        return read is { } current && current.Head.Pending is null
            && ErasureRootCustody.Hash(await custody.ReadSourceAsync(current.Head, id, token).ConfigureAwait(false)) == ErasureRootCustody.Hash(enrollment);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyReservationAsync(InteractionOccurrenceRequest request, CancellationToken token)
    {
        string tenant = request.Identity.Target.TenantId;
        string id = ErasureRootOccurrenceBindings.Id(request.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(request.Identity));
        string digest = ErasureRootCustody.Hash(request);
        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (!await authorization.AuthorizeAsync(tenant, id, "AdmitIndependentSourceReservation", digest, token).ConfigureAwait(false)) { return false; }
            var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
            if (read is not { } current || current.Head.Pending is not null || (await custody.ReadSourceAsync(current.Head, id, token).ConfigureAwait(false) is not { } original)
                || original.Binding.Identity != request.Identity || original.Binding.KeyReference != request.ProposedKeyReference
                || original.Binding.DigestKeyVersion != request.DigestKeyVersion || original.Binding.ReservationAttemptOrdinal != request.ReservationAttemptOrdinal) { return false; }
            if ((await custody.ReadReservationAsync(current.Head, id, token).ConfigureAwait(false) is { } retained)) { return retained == digest; }
            current.Head.SourceReservations.Add(id, digest);
            if (await custody.SaveAsync(tenant, current.Version, current.Head, token).ConfigureAwait(false)) { return true; }
        }
        return false;
    }
    /// <inheritdoc />
    public async Task<bool> VerifySealedAsync(InteractionOccurrenceRecord original, InteractionOccurrenceSealedResult result, CancellationToken token)
    {
        using var verification = _quiescence.Enter();
        if (verification is null) { return false; }
        string id = ErasureRootOccurrenceBindings.Id(original.Request.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Request.Identity));
        var read = await custody.ReadAsync(original.Request.Identity.Target.TenantId, token).ConfigureAwait(false);
        if (read is not { } current || current.Head.Pending is not null || (await custody.ReadOccurrenceAsync(current.Head, id, token).ConfigureAwait(false) is not { } occurrence)
            || occurrence.EncryptionRequestDigest != ErasureRootCustody.Hash(original.Request) || result.KeyReference != original.KeyReference
            || await custody.ReadErasedAsync(current.Head, ErasureRootCustody.TargetId(original.Request.Identity.Target), token).ConfigureAwait(false) is not null
            || result.SerializationFormat != "json+pdenc-v2" || result.ProtectedPathCount <= 0 || result.PayloadBytes.Length > 16777216) { return false; }
        using var json = JsonDocument.Parse(result.PayloadBytes);
        bool sealedShape = original.Request.Identity.Kind == PayloadProtectionPayloadKind.Snapshot
            ? json.RootElement.TryGetProperty("Envelope", out var envelope) && envelope.ValueKind == JsonValueKind.String
            : json.RootElement.TryGetProperty("ProtectedContent", out var content) && content.TryGetProperty("Value", out var value)
                && value.ValueKind == JsonValueKind.Object && value.TryGetProperty("$pdenc", out var marker) && marker.ValueKind == JsonValueKind.String;
        if (!sealedShape || !await authorization.AuthorizeAsync(original.Request.Identity.Target.TenantId, original.KeyReference,
            "VerifyNativeEngineSealedOriginal", ErasureRootCustody.Hash(new { original, result }), token).ConfigureAwait(false)) { return false; }
        string verificationDigest = ErasureRootCustody.Hash(new { original.Request, result });
        if ((await custody.ReadSealedAsync(current.Head, id, token).ConfigureAwait(false) is { } retained)) { return retained == verificationDigest; }
        using var digest = await digests.ResolveAsync(occurrence.Enrollment, original.Request.DigestKeyVersion, token).ConfigureAwait(false);
        if (digest is null) { return false; }
        var root = await custody.UnwrapAsync(occurrence.Registration, "erc-sealed-verifier:" + verificationDigest + ":" + current.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), token).ConfigureAwait(false);
        if (root is null) { return false; }
        try
        {
            if (!await InteractionOccurrenceSealedVerifier.VerifyAsync(original.Request, result, root.Bytes, digest.Bytes, token).ConfigureAwait(false)) { return false; }
            if (!await RetainSnapshotAsync(occurrence.Enrollment, result, token).ConfigureAwait(false)) { return false; }
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var latest = await custody.ReadAsync(original.Request.Identity.Target.TenantId, token).ConfigureAwait(false);
                if (latest is not { } fresh || fresh.Head.Pending is not null || await custody.ReadErasedAsync(fresh.Head, ErasureRootCustody.TargetId(original.Request.Identity.Target), token).ConfigureAwait(false) is not null) { return false; }
                if ((await custody.ReadSealedAsync(fresh.Head, id, token).ConfigureAwait(false) is { } freshRetained)) { return freshRetained == verificationDigest; }
                fresh.Head.SealedOriginals.Add(id, verificationDigest);
                if (await custody.SaveAsync(original.Request.Identity.Target.TenantId, fresh.Version, fresh.Head, token).ConfigureAwait(false)) { return true; }
            }
            return false;
        }
        finally
        {
            root.Dispose();
            custody.RetainDisposedHolder(occurrence.Registration, root);
            // Completion belongs to the host, including when this request was canceled or authorization is unavailable.
            if (!token.IsCancellationRequested)
            {
                try { await custody.CompleteDisposedHoldersAsync(token).ConfigureAwait(false); }
                catch (Exception) { /* The exact disposed instance remains queued for the production lifetime worker. */ }
            }
        }
    }
    /// <inheritdoc />
    public async Task<bool> VerifyWriterAsync(InteractionOccurrenceRecord original, string proofId, bool persisted, CancellationToken token)
    {
        var enrolled = await ResolveAsync(original.Request.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Request.Identity), token).ConfigureAwait(false);
        if (enrolled?.Writer is not { } location || enrolled.Binding.OperationId != proofId || enrolled.Binding.Identity != original.Request.Identity) { return false; }
        var proof = await sourceStore.GetStateAsync<ErasureRootWriterProof>(location.Component, location.Key, ConsistencyMode.Strong, location.Metadata, token).ConfigureAwait(false);
        if (proof is null || proof.Binding != enrolled.Binding || proof.Persisted != persisted || string.IsNullOrWhiteSpace(proof.ReceiptId)) { return false; }
        if (persisted && (original.Sealed is null || proof.SealedPayloadSha256 != Convert.ToHexString(SHA256.HashData(original.Sealed.PayloadBytes)))) { return false; }
        if (!persisted && (!proof.WriterRetired || proof.SealedPayloadSha256 is not null)) { return false; }
        return await authorization.AuthorizeAsync(original.Request.Identity.Target.TenantId, proofId, persisted ? "ConfirmNativePersistedWriter" : "ConfirmNativeRetiredNeverWritten",
            ErasureRootCustody.Hash(new { original, proof }), token).ConfigureAwait(false);
    }
}
