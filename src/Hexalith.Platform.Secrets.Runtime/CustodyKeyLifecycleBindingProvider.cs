using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Component;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Lifecycle port over custody-lifecycle-v1. Pin and unpin use those operations. Root destroy uses the admitted manifest. Lookup starts no effect.</summary>
internal sealed class CustodyKeyLifecycleBindingProvider : ICustodyKeyLifecycleProvider
{
    private readonly ICustodyLifecycleBinding? _binding;
    private readonly ICustodyOutsideWitness? _witness;
    private readonly ICustodyOriginalManifestSource? _originals;

    internal CustodyKeyLifecycleBindingProvider(ICustodyLifecycleBinding? binding, ICustodyOutsideWitness? witness, ICustodyOriginalManifestSource? originals = null)
    {
        _binding = binding;
        _witness = witness;
        _originals = originals;
    }

    /// <inheritdoc/>
    public async Task<CustodyKeyLifecycleOutcome> ExecuteAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest original, string requestDigest, CancellationToken cancellationToken = default)
    {
        if (!Armed(registration, original, requestDigest)) { return Unavailable(original, requestDigest); }
        if (!_witness!.Available) { return Unknown(original, requestDigest); }
        CustodyLifecycleBindingCommand command;
        if (original.Action == CustodyKeyLifecycleAction.Destroy)
        {
            if (original.Identity.Purpose != PlatformKeyPurpose.InteractionRootDek || string.IsNullOrWhiteSpace(original.ProtectionReservationReceiptId) || _originals is null)
            { return Unavailable(original, requestDigest); }
            var manifest = _originals.FindOriginal(original.ProtectionReservationReceiptId);
            if (manifest is null) { return Unavailable(original, requestDigest); }
            if (!manifest.Targets.Contains(original.Identity)) { return Conflict(original, requestDigest); }
            command = new("destroy-manifest", true, original, requestDigest, manifest);
        }
        else
        {
            command = new(original.Action == CustodyKeyLifecycleAction.Pin ? "pin" : "unpin", true, original, requestDigest, null);
        }
        return Interpret(original, requestDigest, await _binding!.DispatchAsync(command, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<CustodyKeyLifecycleOutcome> LookupAsync(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest original, string requestDigest, CancellationToken cancellationToken = default)
    {
        if (!Armed(registration, original, requestDigest)) { return Unavailable(original, requestDigest); }
        if (!_witness!.Available) { return Unknown(original, requestDigest); }
        var reply = await _binding!.DispatchAsync(new("lookup-original", false, original, requestDigest, null), cancellationToken).ConfigureAwait(false);
        return Interpret(original, requestDigest, reply);
    }

    private bool Armed(CustodyKeyRegistration registration, CustodyKeyLifecycleRequest original, string requestDigest)
        => _binding is { Present: true } && _witness is not null && registration.Identity == original.Identity && requestDigest is { Length: 64 };

    private CustodyKeyLifecycleOutcome Interpret(CustodyKeyLifecycleRequest request, string digest, CustodyLifecycleBindingReply reply)
    {
        if (reply.Transport == CustodyBindingTransportState.Refused || reply.Outcome is null) { return Unavailable(request, digest); }
        if (reply.Transport == CustodyBindingTransportState.Unknown) { return Unknown(request, digest); }
        var mapped = Map(request, digest, reply.Outcome);
        if (reply.Outcome.Status is CustodyComponentStatus.Pinned or CustodyComponentStatus.Unpinned or CustodyComponentStatus.Destroyed
            or CustodyComponentStatus.Conflict or CustodyComponentStatus.BlockedByPin)
        {
            // Transport success, including ReceivedUnverified, is not a receipt. Conflict and pin-block need the same witness fact.
            if (_witness is null || !_witness.Available || !_witness.Confirms(request, mapped)) { return Unknown(request, digest); }
        }
        return mapped;
    }

    private static CustodyKeyLifecycleOutcome Map(CustodyKeyLifecycleRequest request, string digest, CustodyComponentOutcome outcome) => new(request.Identity, request.OperationId, digest,
        outcome.Status switch
        {
            CustodyComponentStatus.Pinned => CustodyKeyLifecycleStatus.Pinned,
            CustodyComponentStatus.Unpinned => CustodyKeyLifecycleStatus.Unpinned,
            CustodyComponentStatus.Destroyed => CustodyKeyLifecycleStatus.Destroyed,
            CustodyComponentStatus.BlockedByPin => CustodyKeyLifecycleStatus.BlockedByPin,
            CustodyComponentStatus.Conflict => CustodyKeyLifecycleStatus.Conflict,
            CustodyComponentStatus.Unknown => CustodyKeyLifecycleStatus.Unknown,
            CustodyComponentStatus.NotStarted => CustodyKeyLifecycleStatus.Unavailable,
            _ => CustodyKeyLifecycleStatus.Unavailable,
        }, outcome.ReceiptId, outcome.RestoreBarrierReceiptId);

    private static CustodyKeyLifecycleOutcome Unavailable(CustodyKeyLifecycleRequest request, string digest)
        => new(request.Identity, request.OperationId, digest, CustodyKeyLifecycleStatus.Unavailable);

    private static CustodyKeyLifecycleOutcome Unknown(CustodyKeyLifecycleRequest request, string digest)
        => new(request.Identity, request.OperationId, digest, CustodyKeyLifecycleStatus.Unknown);

    private static CustodyKeyLifecycleOutcome Conflict(CustodyKeyLifecycleRequest request, string digest)
        => new(request.Identity, request.OperationId, digest, CustodyKeyLifecycleStatus.Conflict);
}
