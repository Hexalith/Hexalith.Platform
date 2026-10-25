using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Production Dapr manifest port using independent current authorization and complete authenticated original assembly.</summary>
public sealed class DaprAtomicDeletionManifestProvider(CustodyLifecycleClient client) : IAtomicDeletionManifestProvider
{
    /// <inheritdoc />
    public Task<DeletionManifestProviderResult> ConsumeAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId,
        CancellationToken cancellationToken = default) => client.ExecuteManifestAsync(request, reservationReceiptId, false, cancellationToken);
    /// <inheritdoc />
    public Task<DeletionManifestProviderResult> LookupAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId,
        CancellationToken cancellationToken = default) => client.ExecuteManifestAsync(request, reservationReceiptId, true, cancellationToken);
}
