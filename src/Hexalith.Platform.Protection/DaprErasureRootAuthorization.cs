using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Concrete exact-private authorization adapter over the existing nonce-bound ES256 independent authority transport and surviving ERC reservation originals.</summary>
public sealed class DaprErasureRootAuthorization(IndependentDaprAuthority authority, IErasureRootStore store,
    ErasureRootInstallation installation) : IErasureRootAuthorization
{
    /// <inheritdoc />
    public async Task<ErasureRootPermanentLossReceipt?> ReadPermanentLossAsync(string tenantId, string storeId, string enrollmentId, CancellationToken cancellationToken)
    {
        var original = await authority.InvokeAsync<ErasureRootPermanentLossReceipt>("erc-read-permanent-loss", new { tenantId, storeId, enrollmentId }, cancellationToken).ConfigureAwait(false);
        return original?.TenantId == tenantId && original.StoreId == storeId && original.EnrollmentId == enrollmentId
            && !string.IsNullOrWhiteSpace(original.ReceiptId) && !string.IsNullOrWhiteSpace(original.RetiredVolumeIdentity)
            && original.CopyPolicySha256 is { Length: 64 } ? original : null;
    }
    /// <inheritdoc />
    public Task<bool> AuthorizeAsync(string tenantId, string identity, string operation, string requestDigest, CancellationToken cancellationToken)
        => PermitAsync(tenantId, identity, operation, requestDigest, new { tenantId, identity, operation, requestDigest }, cancellationToken);

    /// <inheritdoc />
    public Task<bool> VerifyLifecycleAsync(CustodyKeyLifecycleRequest request, CancellationToken cancellationToken)
        => PermitAsync(request.Identity.TenantId, request.OperationId, "VerifyLifecycle", ErasureRootCustody.Hash(request), request, cancellationToken);
    /// <inheritdoc />
    public Task<ErasureRootPermissionEvidence?> ReadLifecycleEvidenceAsync(CustodyKeyLifecycleRequest request, CancellationToken cancellationToken)
        => ReadPermissionAsync(request.Identity.TenantId, request.OperationId, "VerifyLifecycle", ErasureRootCustody.Hash(request), request, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> VerifyManifestAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId, CancellationToken cancellationToken)
    {
        try
        {
            request = DeletionConsumptionIdentity.CaptureRequest(request);
            string tenant = request.Capability.TenantId;
            var native = await store.ReadHeadAsync(tenant, cancellationToken).ConfigureAwait(false);
            var head = native is null ? null : JsonSerializer.Deserialize<ErasureRootHead>(native.Bytes, ErasureRootCustody.Json);
            if (head is null || head.StoreId != installation.StoreId || head.EnrollmentId != installation.EnrollmentId || head.TenantId != tenant
                || !head.Anchors.TryGetValue(DeletionConsumptionActor.GetOwnerScope(tenant), out var anchor)) { return false; }
            var originals = await ErasureRootArchives.ReadTransitionsAsync(store, tenant, DeletionConsumptionActor.GetOwnerScope(tenant), anchor, false, cancellationToken).ConfigureAwait(false);
            var current = originals.Values.SingleOrDefault(value => value.TargetRevision == anchor.Revision && value.TargetDigest == anchor.Digest);
            if (current is null) { return false; }
            using var ledger = JsonDocument.Parse(current.TargetBytes);
            var matches = ledger.RootElement.GetProperty("Batches").EnumerateArray().Where(value => value.GetProperty("Current").GetProperty("Capability").GetProperty("BatchId").GetString() == request.Capability.BatchId).ToArray();
            if (matches.Length != 1) { return false; }
            var retained = matches[0];
            var outcome = retained.GetProperty("Outcome").Deserialize<DeletionConsumptionOutcome>();
            if (outcome is null || outcome.TenantId != tenant || outcome.ReceiptId != reservationReceiptId
                || outcome.Status is not (DeletionConsumptionStatus.ConsumptionReserved or DeletionConsumptionStatus.Consumed)
                || ErasureRootCustody.Hash(retained.GetProperty("Current").Deserialize<DeletionBatchConsumptionRequest>()) != ErasureRootCustody.Hash(request)) { return false; }
            // This current independent decision includes full copy-policy qualification and holder/fence closure. Signature-only dispatch cannot grant it.
            string digest = ErasureRootCustody.Hash(new { request, reservation = reservationReceiptId });
            return await PermitAsync(tenant, request.Capability.BatchId, "VerifyReservedManifestAndCopyPolicy", digest,
                new { request, reservationReceiptId, ownerRevision = anchor.Revision, ownerStateDigest = anchor.Digest }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return false; }
    }

    /// <inheritdoc />
    public Task<bool> VerifyTransitionPermissionAsync(AnchoredStateTransition transition, string operation, CancellationToken cancellationToken)
    {
        var tenants = installation.NativeKeys.Keys.Where(tenant => transition.ScopeId.StartsWith(tenant + ":", StringComparison.Ordinal)).ToArray();
        return tenants.Length == 1 ? PermitAsync(tenants[0], transition.ScopeId, operation, ErasureRootCustody.Hash(transition), transition, cancellationToken) : Task.FromResult(false);
    }

    private async Task<bool> PermitAsync(string tenant, string identity, string operation, string digest, object original, CancellationToken token)
        => await ReadPermissionAsync(tenant, identity, operation, digest, original, token).ConfigureAwait(false) is not null;
    private async Task<ErasureRootPermissionEvidence?> ReadPermissionAsync(string tenant, string identity, string operation, string digest, object original, CancellationToken token)
    {
        if (!installation.NativeKeys.ContainsKey(tenant) || string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(operation)
            || digest is not { Length: 64 } || digest.Any(value => !char.IsAsciiHexDigit(value))) { return null; }
        var proof = await authority.InvokeAsync<ErasureRootPermissionEvidence>("erc-permission", new { tenantId = tenant, identity, operation, requestDigest = digest, original }, token).ConfigureAwait(false);
        return proof is { Permitted: true } && proof.TenantId == tenant && proof.Identity == identity && proof.Operation == operation
            && proof.RequestDigest == digest && !string.IsNullOrWhiteSpace(proof.ReceiptId) ? proof : null;
    }
}
