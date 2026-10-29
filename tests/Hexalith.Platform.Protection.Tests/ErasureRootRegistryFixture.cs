using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Serializes actual registry actor turns without a Dapr runtime; all durable protocol decisions remain in the shipped actor.</summary>
internal sealed class ErasureRootRegistryFixture(InteractionOccurrenceRegistryActor actor) : IInteractionOccurrenceRegistry
{
    private readonly SemaphoreSlim _turn = new(1, 1);
    private async Task<T> Invoke<T>(Func<Task<T>> method, CancellationToken token)
    { await _turn.WaitAsync(token); try { return await method(); } finally { _turn.Release(); } }
    public Task<InteractionOccurrenceReservationResult> ReserveAsync(InteractionOccurrenceRequest request, CancellationToken cancellationToken = default)
        => Invoke(() => actor.ReserveAsync(request), cancellationToken);
    public Task<InteractionOccurrenceReservationResult> RetainSealedAsync(InteractionOccurrenceIdentity identity, InteractionOccurrenceSealedResult result, CancellationToken cancellationToken = default)
        => Invoke(() => actor.RetainSealedAsync(identity, result), cancellationToken);
    public Task<InteractionOccurrenceReservationResult> CompleteWriterAsync(InteractionOccurrenceIdentity identity, string keyReference, string proofId, bool persisted, CancellationToken cancellationToken = default)
        => Invoke(() => actor.CompleteWriterAsync(identity, keyReference, proofId, persisted), cancellationToken);
    public Task<InteractionOccurrenceReservationResult> LookupAsync(InteractionOccurrenceIdentity identity, CancellationToken cancellationToken = default)
        => Invoke(() => actor.LookupAsync(identity), cancellationToken);
}
