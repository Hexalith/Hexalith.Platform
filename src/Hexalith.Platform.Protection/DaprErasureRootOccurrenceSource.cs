using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Current source originals through the existing independently authenticated ES256 authority transport, with exact identity/digest correlation.</summary>
public sealed class DaprErasureRootOccurrenceSource(IndependentDaprAuthority source) : IErasureRootOccurrenceSource
{
    /// <inheritdoc />
    public Task<bool> RegisterOriginalAsync(ErasureRootOccurrenceEnrollment enrollment, CancellationToken cancellationToken)
        => source.InvokeAsync<bool>("erc-register-source-original", new { enrollment }, cancellationToken);
    /// <inheritdoc />
    public Task<ErasureRootOccurrenceEnrollment?> ResolveErasedStoredAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence,
        string sealedPayloadSha256, string keyReference, ErasureRootPermanentLossReceipt loss, CancellationToken cancellationToken)
        => source.InvokeAsync<ErasureRootOccurrenceEnrollment>("erc-read-erased-original", new { owner, occurrence, sealedPayloadSha256, keyReference, loss }, cancellationToken);
    /// <inheritdoc />
    public async Task<ErasureRootOccurrenceEnrollment?> ResolveAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence, CancellationToken token)
    {
        var value = await source.InvokeAsync<ErasureRootOccurrenceEnrollment>("erc-occurrence-original", new { owner, occurrence }, token).ConfigureAwait(false);
        return value?.Binding.Identity.Owner == owner && value.Binding.Identity.Sequence == occurrence.RecordSequence
            && value.Binding.Identity.Kind == occurrence.PayloadKind && value.Binding.Identity.PayloadTypeId == occurrence.PayloadTypeId ? value : null;
    }
    /// <inheritdoc />
    public Task<bool> VerifyReservationAsync(InteractionOccurrenceRequest request, CancellationToken token)
        => VerifyAsync(request.Identity.Target.TenantId, request.ProposedKeyReference, "VerifyOccurrenceReservation", request, token);
    /// <inheritdoc />
    public Task<bool> VerifyWriterAsync(InteractionOccurrenceRecord original, string proofId, bool persisted, CancellationToken token)
        => VerifyAsync(original.Request.Identity.Target.TenantId, proofId, persisted ? "VerifyPersistedWriter" : "VerifyNeverWritten", new { original, proofId, persisted }, token);
    /// <inheritdoc />
    public Task<bool> VerifySealedAsync(InteractionOccurrenceRecord original, InteractionOccurrenceSealedResult result, CancellationToken token)
        => VerifyAsync(original.Request.Identity.Target.TenantId, original.KeyReference, "VerifyOriginalSealedOutput", new { original, result }, token);
    private async Task<bool> VerifyAsync(string tenant, string identity, string operation, object original, CancellationToken token)
    {
        string digest = ErasureRootCustody.Hash(original);
        var proof = await source.InvokeAsync<ErasureRootPermissionEvidence>("erc-source-proof", new { tenantId = tenant, identity, operation, requestDigest = digest, original }, token).ConfigureAwait(false);
        return proof is { Permitted: true } && proof.TenantId == tenant && proof.Identity == identity && proof.Operation == operation
            && proof.RequestDigest == digest && !string.IsNullOrWhiteSpace(proof.ReceiptId);
    }
}
