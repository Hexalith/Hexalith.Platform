using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Component;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Manifest port. Consume and lookup use the admitted target order and do not sort it.</summary>
internal sealed class AtomicDeletionManifestBindingProvider : IAtomicDeletionManifestProvider
{
    private readonly ICustodyLifecycleBinding? _binding;
    private readonly ICustodyOutsideWitness? _witness;
    private readonly CustodyComponentStore? _store;

    internal AtomicDeletionManifestBindingProvider(ICustodyLifecycleBinding? binding, ICustodyOutsideWitness? witness, CustodyComponentStore? store)
    {
        _binding = binding;
        _witness = witness;
        _store = store;
    }

    /// <inheritdoc/>
    public Task<DeletionManifestProviderResult> ConsumeAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId, CancellationToken cancellationToken = default)
        => DispatchAsync(request, reservationReceiptId, effect: true, cancellationToken);

    /// <inheritdoc/>
    public Task<DeletionManifestProviderResult> LookupAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId, CancellationToken cancellationToken = default)
        => DispatchAsync(request, reservationReceiptId, effect: false, cancellationToken);

    internal static string RequestDigest(DeletionBatchConsumptionRequest request)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));

    private async Task<DeletionManifestProviderResult> DispatchAsync(DeletionBatchConsumptionRequest request, string reservationReceiptId, bool effect, CancellationToken cancellationToken)
    {
        var unknown = new DeletionManifestProviderResult(request.Capability.TenantId, request.Capability.BatchId, reservationReceiptId, DeletionManifestProviderState.Unknown, []);
        if (_binding is not { Present: true } || _witness is null || _store is null) { return unknown; }
        if (!_witness.Available) { return unknown; }
        if (!TryResolve(request, out var identities)) { return unknown; }
        var manifest = new CustodyOriginalManifest(reservationReceiptId, request.Capability.BatchId, request.Capability.ManifestDigest, identities);
        string digest = RequestDigest(request);
        var reply = await _binding.DispatchAsync(new(effect ? "destroy-manifest" : "lookup-original", effect, null, digest, manifest), cancellationToken).ConfigureAwait(false);
        if (reply.Transport != CustodyBindingTransportState.ReceivedUnverified) { return unknown; }
        var confirmed = _store.LookupManifest(reservationReceiptId, digest);
        if (confirmed.Status == CustodyComponentStatus.Destroyed && confirmed.TargetReceipts is { Count: > 0 })
        {
            return new(request.Capability.TenantId, request.Capability.BatchId, reservationReceiptId, DeletionManifestProviderState.Consumed, confirmed.TargetReceipts);
        }
        // A transport body that claims destruction is not a receipt unless the witness stored the vector.
        if (confirmed.Status == CustodyComponentStatus.NotStarted && reply.Outcome?.Status is not CustodyComponentStatus.Destroyed)
        {
            return new(request.Capability.TenantId, request.Capability.BatchId, reservationReceiptId, DeletionManifestProviderState.NotStarted, []);
        }
        return unknown;
    }

    private bool TryResolve(DeletionBatchConsumptionRequest request, out IReadOnlyList<CustodyKeyObjectIdentity> identities)
    {
        var resolved = new List<CustodyKeyObjectIdentity>(request.Targets.Count);
        foreach (var target in request.Targets)
        {
            var found = _store!.FindEnrolled(target);
            if (found is null) { identities = []; return false; }
            resolved.Add(found);
        }
        identities = resolved;
        return resolved.Count == request.Targets.Count && DeletionBatchCapabilityIdentity.TargetManifestDigest(request.Targets) == request.Capability.ManifestDigest;
    }
}
