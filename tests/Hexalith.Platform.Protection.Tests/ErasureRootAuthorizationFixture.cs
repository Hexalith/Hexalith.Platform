using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Explicit synthetic permission fixture for component tests; never registered by production composition.</summary>
internal sealed class ErasureRootAuthorizationFixture : IErasureRootAuthorization
{
    internal bool Allowed { get; set; } = true;
    internal bool TransitionAllowed { get; set; } = true;
    internal bool ManifestAllowed { get; set; } = true;
    public Task<bool> AuthorizeAsync(string tenantId, string identity, string operation, string requestDigest, CancellationToken cancellationToken) => Task.FromResult(Allowed);
    public Task<bool> VerifyLifecycleAsync(CustodyKeyLifecycleRequest request, CancellationToken cancellationToken) => Task.FromResult(Allowed);
    public Task<bool> VerifyManifestAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId, CancellationToken cancellationToken) => Task.FromResult(Allowed && ManifestAllowed);
    public Task<bool> VerifyTransitionPermissionAsync(AnchoredStateTransition transition, string operation, CancellationToken cancellationToken) => Task.FromResult(Allowed && TransitionAllowed);
}
