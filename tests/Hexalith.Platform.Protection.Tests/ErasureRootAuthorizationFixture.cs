using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Explicit synthetic permission fixture for component tests; never registered by production composition.</summary>
internal sealed class ErasureRootAuthorizationFixture : IErasureRootAuthorization
{
    internal Func<string, string, string, CancellationToken, Task<ErasureRootEffectBoundaryEvidence?>>? EffectBoundaryReader { get; set; }
    public Task<ErasureRootEffectBoundaryEvidence?> ReadEffectBoundaryAsync(string tenantId, string callDigest, string boundary, CancellationToken cancellationToken)
        => EffectBoundaryReader?.Invoke(tenantId, callDigest, boundary, cancellationToken) ?? Task.FromResult<ErasureRootEffectBoundaryEvidence?>(null);
    internal Func<CustodyKeyLifecycleRequest, ErasureRootPermissionEvidence?>? LifecycleEvidenceReader { get; set; }
    public Task<ErasureRootPermissionEvidence?> ReadLifecycleEvidenceAsync(CustodyKeyLifecycleRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Allowed ? LifecycleEvidenceReader?.Invoke(request) : null);
    internal HashSet<string> DeniedOperations { get; } = new(StringComparer.Ordinal);
    internal bool Allowed { get; set; } = true;
    internal bool TransitionAllowed { get; set; } = true;
    internal bool ManifestAllowed { get; set; } = true;
    internal ErasureRootPermanentLossReceipt? PermanentLoss { get; set; }
    public Task<ErasureRootPermanentLossReceipt?> ReadPermanentLossAsync(string tenantId, string storeId, string enrollmentId, CancellationToken cancellationToken)
        => Task.FromResult(PermanentLoss?.TenantId == tenantId && PermanentLoss.StoreId == storeId && PermanentLoss.EnrollmentId == enrollmentId ? PermanentLoss : null);
    public Task<bool> AuthorizeAsync(string tenantId, string identity, string operation, string requestDigest, CancellationToken cancellationToken) => Task.FromResult(Allowed && !DeniedOperations.Contains(operation));
    public Task<bool> VerifyLifecycleAsync(CustodyKeyLifecycleRequest request, CancellationToken cancellationToken) => Task.FromResult(Allowed);
    public Task<bool> VerifyManifestAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId, CancellationToken cancellationToken) => Task.FromResult(Allowed && ManifestAllowed);
    public Task<bool> VerifyTransitionPermissionAsync(AnchoredStateTransition transition, string operation, CancellationToken cancellationToken) => Task.FromResult(Allowed && TransitionAllowed);
}
