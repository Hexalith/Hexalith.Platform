using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Retained original manifest locator. Returned data is a hint until independently verified by custody.</summary>
public interface ICustodyReservedManifestSource
{
    /// <summary>Finds the unchanged complete signed manifest at the exact original protection reservation.</summary>
    Task<DeletionBatchConsumptionRequest?> FindAsync(string reservationReceiptId, CancellationToken cancellationToken);
}

/// <summary>Dapr lifecycle port. Interaction-root destruction always consumes its original complete manifest.</summary>
public sealed class DaprCustodyKeyLifecycleProvider(CustodyLifecycleClient client, ICustodyReservedManifestSource originals) : ICustodyKeyLifecycleProvider
{
    /// <inheritdoc />
    public Task<CustodyKeyLifecycleOutcome> ExecuteAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest original,
        string requestDigest, CancellationToken cancellationToken = default) => InvokeAsync(registration, original, requestDigest, false, cancellationToken);
    /// <inheritdoc />
    public Task<CustodyKeyLifecycleOutcome> LookupAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest original,
        string requestDigest, CancellationToken cancellationToken = default) => InvokeAsync(registration, original, requestDigest, true, cancellationToken);

    private Task<CustodyKeyLifecycleOutcome> InvokeAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest request,
        string digest, bool lookup, CancellationToken token)
    {
        var unknown = new CustodyKeyLifecycleOutcome(request.Identity, request.OperationId, digest, CustodyKeyLifecycleStatus.Unknown);
        return client.InBudgetAsync(token, async budget =>
        {
            if (registration.Identity != request.Identity || digest != CustodyBindingContract.Hash(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(request))) return unknown;
            if (request.Action != CustodyKeyLifecycleAction.Destroy)
                return await client.LifecycleAsync(registration, request, lookup, budget);
            if (request.Identity.Purpose != PlatformKeyPurpose.InteractionRootDek || !CustodyBindingContract.Text(request.ProtectionReservationReceiptId)) return unknown;
            // The signed manifest uses the target triple. Authenticate every remaining root/lifecycle/store/contract
            // field against the retained wrap original before projecting a target receipt onto this complete identity.
            var wrapped = await client.LifecycleAsync(registration, null, true, budget);
            budget.Check();
            if (wrapped.Status != CustodyKeyLifecycleStatus.Wrapped || wrapped.ReceiptId != registration.WrapReceiptId) return unknown;
            var manifest = await originals.FindAsync(request.ProtectionReservationReceiptId!, budget.Token).WaitAsync(budget.Token);
            budget.Check();
            var target = new ProtectionTarget(request.Identity.TenantId, request.Identity.ObjectId, request.Identity.KeyAlias);
            if (manifest is null || !manifest.Targets.Contains(target)) return unknown;
            var original = await client.ExecuteManifestOriginalAsync(manifest, request.ProtectionReservationReceiptId!, lookup, budget);
            budget.Check();
            var receipt = original.Result.TargetReceipts.SingleOrDefault(r => r.Target == target);
            return original.Result.State == DeletionManifestProviderState.Consumed && receipt is not null && original.RestoreBarrierReceiptId is not null
                ? new(request.Identity, request.OperationId, digest, CustodyKeyLifecycleStatus.Destroyed, receipt.ReceiptId, original.RestoreBarrierReceiptId) : unknown;
        }, () => unknown);
    }
}
