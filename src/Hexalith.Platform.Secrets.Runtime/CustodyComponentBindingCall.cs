using Hexalith.Platform.Custody.Component;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>
/// In-process disposable call into the private store. Not an installed binding and not a Dapr component.
/// </summary>
internal sealed class CustodyComponentBindingCall : ICustodyLifecycleBinding
{
    private readonly CustodyComponentStore _store;
    private readonly List<string> _operations = [];

    internal CustodyComponentBindingCall(CustodyComponentStore store, bool present = true)
    {
        _store = store;
        Present = present;
    }

    /// <inheritdoc/>
    public bool Present { get; set; }

    internal IReadOnlyList<string> Operations => _operations;

    /// <inheritdoc/>
    public Task<CustodyLifecycleBindingReply> DispatchAsync(CustodyLifecycleBindingCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Present) { return Task.FromResult(new CustodyLifecycleBindingReply(CustodyBindingTransportState.Refused, null, false)); }
        _operations.Add(command.Operation);
        int before = _store.Mutations;
        if (!command.Effect || command.Operation == "lookup-original")
        {
            var lookup = command.Lifecycle is { } lifecycle
                ? _store.Lookup(lifecycle.OperationId, command.RequestDigest)
                : command.Manifest is { } manifest
                    ? _store.LookupManifest(manifest.ReservationId, command.RequestDigest)
                    : new CustodyComponentOutcome(CustodyComponentStatus.Unavailable, "lookup", command.RequestDigest);
            return Task.FromResult(new CustodyLifecycleBindingReply(CustodyBindingTransportState.ReceivedUnverified, lookup, false));
        }
        CustodyComponentOutcome outcome = command.Operation switch
        {
            "pin" when command.Lifecycle is { } pin => _store.Pin(pin, command.RequestDigest),
            "unpin" when command.Lifecycle is { } unpin => _store.Unpin(unpin, command.RequestDigest),
            "destroy-manifest" when command.Manifest is { } manifest => _store.Consume(command.Lifecycle?.OperationId ?? manifest.BatchId, manifest, command.RequestDigest, command.Lifecycle?.Identity),
            _ => new CustodyComponentOutcome(CustodyComponentStatus.Unavailable, command.Lifecycle?.OperationId ?? "refused", command.RequestDigest),
        };
        return Task.FromResult(new CustodyLifecycleBindingReply(CustodyBindingTransportState.ReceivedUnverified, outcome, _store.Mutations != before));
    }
}
