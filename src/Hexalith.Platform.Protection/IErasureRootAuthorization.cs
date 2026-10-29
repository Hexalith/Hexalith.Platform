using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Current private caller, fence, original reservation, holder completion and transition permission. Native credentials alone never grant these permissions.</summary>
public interface IErasureRootAuthorization
{
    /// <summary>Authenticates the exact current caller and original operation; missing input denies.</summary>
    Task<bool> AuthorizeAsync(string tenantId, string identity, string operation, string requestDigest, CancellationToken cancellationToken);
    /// <summary>Verifies exact current lifecycle fence and authorized hold release at the ERC compare boundary.</summary>
    Task<bool> VerifyLifecycleAsync(CustodyKeyLifecycleRequest request, CancellationToken cancellationToken);
    /// <summary>Verifies the exact retained irreversible owner reservation, retained complete signed capability, all target-holder completion and independently qualified current full copy/restore policy. Recovery confirms the same original reservation without renewing its authorization. Missing all-copy evidence denies before native work; native credentials and signature-only acceptance are insufficient.</summary>
    Task<bool> VerifyManifestAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId, CancellationToken cancellationToken);
    /// <summary>Authenticates original transition ownership and current phase permission independently of proposed store bytes.</summary>
    Task<bool> VerifyTransitionPermissionAsync(AnchoredStateTransition transition, string operation, CancellationToken cancellationToken);
}
