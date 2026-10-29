using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Synthetic independent original/writer fixture. It compares actual sealed bytes with its separately retained source write; it supplies no installed authority.</summary>
internal sealed class ErasureRootSourceFixture : IErasureRootOccurrenceSource
{
    internal Dictionary<string, ErasureRootOccurrenceEnrollment> Enrollments { get; } = [];
    internal Dictionary<string, byte[]> Persisted { get; } = [];
    internal HashSet<string> NeverWritten { get; } = [];
    internal bool Available { get; set; } = true;
    public async Task<ErasureRootOccurrenceEnrollment?> ResolveErasedStoredAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence,
        string sealedPayloadSha256, string keyReference, ErasureRootPermanentLossReceipt loss, CancellationToken cancellationToken)
    {
        var original = await ResolveAsync(owner, occurrence, cancellationToken);
        return original is not null && original.Binding.KeyReference == keyReference && original.RootIdentity.StoreTarget == loss.StoreId
            && Persisted.TryGetValue(original.Binding.OperationId, out var bytes)
            && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) == sealedPayloadSha256 ? original : null;
    }
    public Task<ErasureRootOccurrenceEnrollment?> ResolveAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence, CancellationToken cancellationToken)
        => Task.FromResult(Available ? Enrollments.GetValueOrDefault(ErasureRootOccurrenceBindings.Id(owner, occurrence)) : null);
    public Task<bool> VerifyReservationAsync(InteractionOccurrenceRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Available && Enrollments.Values.Any(value => value.Binding.Identity == request.Identity && value.Binding.KeyReference == request.ProposedKeyReference));
    public Task<bool> VerifyWriterAsync(InteractionOccurrenceRecord original, string proofId, bool persisted, CancellationToken cancellationToken)
        => Task.FromResult(Available && (persisted ? original.Sealed is not null && Persisted.TryGetValue(proofId, out var bytes) && bytes.AsSpan().SequenceEqual(original.Sealed.PayloadBytes)
            : NeverWritten.Contains(proofId) && !Persisted.ContainsKey(proofId)));
    public Task<bool> VerifySealedAsync(InteractionOccurrenceRecord original, InteractionOccurrenceSealedResult result, CancellationToken cancellationToken)
        => Task.FromResult(Available && result.SerializationFormat == "json+pdenc-v2" && result.KeyReference == original.KeyReference
            && !System.Text.Encoding.UTF8.GetString(result.PayloadBytes).Contains("confidential-canary", StringComparison.Ordinal));
}
