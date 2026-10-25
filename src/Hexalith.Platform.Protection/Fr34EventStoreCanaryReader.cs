using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Independent actual native EventStore source reader. Engine-returned buffers and local archive copies confer no evidence.</summary>
public sealed class Fr34EventStoreCanaryReader(IEventStoreGatewayClient privateReadGateway, TimeProvider clock) : IFr34PersistedCanaryReader
{
    private readonly AuthoritativeEventStreamReader _reader = new(privateReadGateway, clock, requireNativeProof: true);
    internal async Task<Fr34StoredCanary?> ReadCanaryAsync(Fr34ProtectionTarget target, string canaryId, CancellationToken token)
    {
        if (canaryId.Length != 64 || !canaryId.All(char.IsAsciiHexDigit)) return null;
        var identity = new AggregateIdentity(target.TenantId, "fr34-canary", canaryId);
        var read = await _reader.ReadAsync(identity, token).ConfigureAwait(false);
        if (!read.IsAuthoritative || read.Stream is not { Head: 1, Events.Count: 1, NativeProof: { CodecVersion: 1, Erased: false, RetainedFloor: 1 } } stream) return null;
        var item = stream.Events[0];
        if (item.EventTypeName != typeof(Fr34CanaryPersisted).FullName || item.SequenceNumber != 1 || item.ProtectionMetadata is not { State: PayloadProtectionState.Protected } metadata
            || string.IsNullOrWhiteSpace(item.NativeEventDigest) || string.IsNullOrWhiteSpace(metadata.KeyAlias)) return null;
        var reference = new Fr34CanaryReference(target, canaryId, identity.ActorId, metadata.KeyAlias);
        return new(reference, identity, new(target.TenantId, canaryId, "fr34-" + canaryId),
            new(1, PayloadProtectionPayloadKind.Event, item.EventTypeName), new(item.Payload.ToArray(), item.SerializationFormat, metadata),stream.NativeProof,item.NativeEventDigest);
    }
    /// <summary>Reads and checks the complete independently persisted original reference.</summary>
    public async Task<Fr34StoredCanary?> ReadOriginalAsync(Fr34CanaryReference reference, CancellationToken token)
        => await ReadCanaryAsync(reference.Target, reference.CanaryId, token).ConfigureAwait(false) is { } original && original.Reference == reference ? original : null;
    /// <inheritdoc />
    public async Task<Fr34PersistedCanary?> ReadAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default)
        => await ReadOriginalAsync(reference, cancellationToken).ConfigureAwait(false) is { } original ? new(reference, original.Protected.PayloadBytes, original.Protected.Metadata) : null;
}
