using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Atomic provider composition completes only cleared process-owned holders before entering the original native manifest protocol.</summary>
public sealed class ErasureRootManifestProvider(ErasureRootCustody custody, ErasureRootOccurrenceKeyProvider keys) : IAtomicDeletionManifestProvider
{
    /// <inheritdoc />
    public async Task<DeletionManifestProviderResult> ConsumeAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId, CancellationToken cancellationToken = default)
    {
        await keys.CompleteDisposedUsesAsync(cancellationToken).ConfigureAwait(false);
        return await custody.ConsumeAsync(request, reservationReceiptId, cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<DeletionManifestProviderResult> LookupAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId, CancellationToken cancellationToken = default)
    {
        await keys.CompleteDisposedUsesAsync(cancellationToken).ConfigureAwait(false);
        return await custody.LookupAsync(request, reservationReceiptId, cancellationToken).ConfigureAwait(false);
    }
}
