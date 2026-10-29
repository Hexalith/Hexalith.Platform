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
    private async Task<bool> RetainIndependentAsync(ErasureRootOccurrenceEnrollment enrollment, CancellationToken token)
    {
        string tenant = enrollment.Binding.Identity.Owner.TenantId;
        if (independentOriginals is null || !independentOriginals.TryGetValue(tenant, out var location)) { return false; }
        string id = ErasureRootOccurrenceBindings.Id(enrollment.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(enrollment.Binding.Identity));
        for (int attempt = 0; attempt < 16; attempt++)
        {
            var (state, tag) = await sourceStore.GetStateAndETagAsync<ErasureRootSourceOriginals>(location.Component, location.Key, ConsistencyMode.Strong, location.Metadata, token).ConfigureAwait(false);
            if (state is null || state.TenantId != tenant || state.StoreId != enrollment.RootIdentity.StoreTarget || string.IsNullOrWhiteSpace(tag)) { return false; }
            if (state.Originals.TryGetValue(id, out var prior)) { return ErasureRootCustody.Hash(prior) == ErasureRootCustody.Hash(enrollment); }
            if (state.Originals.Count >= 10000) { return false; } state.Originals.Add(id, enrollment);
            if (await sourceStore.TrySaveStateAsync(location.Component, location.Key, state, tag, new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong }, location.Metadata, token).ConfigureAwait(false)) { return true; }
        }
        return false;
    }
    private async Task<ErasureRootOccurrenceEnrollment?> ReadIndependentAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence, CancellationToken token)
    {
        if (independentOriginals is null || !independentOriginals.TryGetValue(owner.TenantId, out var location)) { return null; }
        var state = await sourceStore.GetStateAsync<ErasureRootSourceOriginals>(location.Component, location.Key, ConsistencyMode.Strong, location.Metadata, token).ConfigureAwait(false);
        string id = ErasureRootOccurrenceBindings.Id(owner, occurrence);
        return state?.TenantId == owner.TenantId && state.Originals.TryGetValue(id, out var original) && original.RootIdentity.StoreTarget == state.StoreId
            && original.Binding.Identity.Owner == owner && ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity) == occurrence ? original : null;
    }
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
        return read is { } head && head.Head.SourceReservations.TryGetValue(id, out var retained) && retained == digest;
    }
    /// <summary>Reads only a previously authenticated complete sealed output, without another root unwrap or verification mutation.</summary>
    public async Task<bool> LookupSealedAsync(InteractionOccurrenceRecord original, InteractionOccurrenceSealedResult result, CancellationToken token)
    {
        string id = ErasureRootOccurrenceBindings.Id(original.Request.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Request.Identity));
        string digest = ErasureRootCustody.Hash(new { original.Request, result });
        if (!await authorization.AuthorizeAsync(original.Request.Identity.Target.TenantId, id, "LookupNativeEngineSealedOriginal", digest, token).ConfigureAwait(false)) { return false; }
        var read = await custody.ReadAsync(original.Request.Identity.Target.TenantId, token).ConfigureAwait(false);
        return read is { } head && head.Head.Pending is null && !head.Head.Erased.ContainsKey(ErasureRootCustody.TargetId(original.Request.Identity.Target))
            && head.Head.SealedOriginals.TryGetValue(id, out var retained) && retained == digest;
    }
    /// <summary>Registers only a separately authorized source-owner original before root issue. Exact retry cannot replace its target or source receipt location.</summary>
    public async Task<bool> RegisterOriginalAsync(ErasureRootOccurrenceEnrollment enrollment, CancellationToken token)
    {
        var identity = enrollment.Binding.Identity;
        string id = ErasureRootOccurrenceBindings.Id(identity.Owner, ErasureRootOccurrenceBindings.Occurrence(identity));
        string digest = ErasureRootCustody.Hash(enrollment);
        if (enrollment.Writer is null || identity.Target.TenantId != identity.Owner.TenantId) { return false; }
        using var nativeDigest = await digests.ResolveAsync(enrollment, enrollment.Binding.DigestKeyVersion, token).ConfigureAwait(false);
        if (nativeDigest is null) { return false; }
        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (!await authorization.AuthorizeAsync(identity.Target.TenantId, id, "RegisterIndependentSourceOriginal", digest, token).ConfigureAwait(false)) { return false; }
            var read = await custody.ReadAsync(identity.Target.TenantId, token).ConfigureAwait(false);
            if (read is not { } current || current.Head.Pending is not null || current.Head.Erased.ContainsKey(ErasureRootCustody.TargetId(identity.Target))) { return false; }
            if (!await RetainIndependentAsync(enrollment, token).ConfigureAwait(false)) { return false; }
            if (current.Head.SourceOriginals.TryGetValue(id, out var prior)) { return ErasureRootCustody.Hash(prior) == digest; }
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
        return read is { } current && current.Head.SourceOriginals.TryGetValue(id, out var original) ? original : null;
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
            if (read is not { } current || current.Head.Pending is not null || !current.Head.SourceOriginals.TryGetValue(id, out var original)
                || original.Binding.Identity != request.Identity || original.Binding.KeyReference != request.ProposedKeyReference
                || original.Binding.DigestKeyVersion != request.DigestKeyVersion || original.Binding.ReservationAttemptOrdinal != request.ReservationAttemptOrdinal) { return false; }
            if (current.Head.SourceReservations.TryGetValue(id, out var retained)) { return retained == digest; }
            current.Head.SourceReservations.Add(id, digest);
            if (await custody.SaveAsync(tenant, current.Version, current.Head, token).ConfigureAwait(false)) { return true; }
        }
        return false;
    }
    /// <inheritdoc />
    public async Task<bool> VerifySealedAsync(InteractionOccurrenceRecord original, InteractionOccurrenceSealedResult result, CancellationToken token)
    {
        string id = ErasureRootOccurrenceBindings.Id(original.Request.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Request.Identity));
        var read = await custody.ReadAsync(original.Request.Identity.Target.TenantId, token).ConfigureAwait(false);
        if (read is not { } current || current.Head.Pending is not null || !current.Head.Occurrences.TryGetValue(id, out var occurrence)
            || occurrence.EncryptionRequestDigest != ErasureRootCustody.Hash(original.Request) || result.KeyReference != original.KeyReference
            || current.Head.Erased.ContainsKey(ErasureRootCustody.TargetId(original.Request.Identity.Target))
            || result.SerializationFormat != "json+pdenc-v2" || result.ProtectedPathCount <= 0 || result.PayloadBytes.Length > 16777216) { return false; }
        using var json = JsonDocument.Parse(result.PayloadBytes);
        bool sealedShape = original.Request.Identity.Kind == PayloadProtectionPayloadKind.Snapshot
            ? json.RootElement.TryGetProperty("Envelope", out var envelope) && envelope.ValueKind == JsonValueKind.String
            : json.RootElement.TryGetProperty("ProtectedContent", out var content) && content.TryGetProperty("Value", out var value)
                && value.ValueKind == JsonValueKind.Object && value.TryGetProperty("$pdenc", out var marker) && marker.ValueKind == JsonValueKind.String;
        if (!sealedShape || !await authorization.AuthorizeAsync(original.Request.Identity.Target.TenantId, original.KeyReference,
            "VerifyNativeEngineSealedOriginal", ErasureRootCustody.Hash(new { original, result }), token).ConfigureAwait(false)) { return false; }
        string verification = ErasureRootCustody.Hash(new { original.Request, result });
        if (current.Head.SealedOriginals.TryGetValue(id, out string? retained)) { return retained == verification; }
        using var digest = await digests.ResolveAsync(occurrence.Enrollment, original.Request.DigestKeyVersion, token).ConfigureAwait(false);
        if (digest is null) { return false; }
        var root = await custody.UnwrapAsync(occurrence.Registration, "erc-sealed-verifier:" + verification + ":" + current.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), token).ConfigureAwait(false);
        if (root is null) { return false; }
        try
        {
            if (!await InteractionOccurrenceSealedVerifier.VerifyAsync(original.Request, result, root.Bytes, digest.Bytes, token).ConfigureAwait(false)) { return false; }
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var latest = await custody.ReadAsync(original.Request.Identity.Target.TenantId, token).ConfigureAwait(false);
                if (latest is not { } fresh || fresh.Head.Pending is not null || fresh.Head.Erased.ContainsKey(ErasureRootCustody.TargetId(original.Request.Identity.Target))) { return false; }
                if (fresh.Head.SealedOriginals.TryGetValue(id, out retained)) { return retained == verification; }
                fresh.Head.SealedOriginals.Add(id, verification);
                if (await custody.SaveAsync(original.Request.Identity.Target.TenantId, fresh.Version, fresh.Head, token).ConfigureAwait(false)) { return true; }
            }
            return false;
        }
        finally { root.Dispose(); await custody.CompleteHolderAsync(occurrence.Registration, root, token).ConfigureAwait(false); }
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
