using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Independent current source-writer/intent authority. It provides exact originals, never key bytes or caller-selected targets.</summary>
public interface IErasureRootOccurrenceSource
{
    /// <summary>Reads only independently verified snapshot skeleton plus exact persisted ciphertext proof after authenticated permanent loss.</summary>
    Task<byte[]?> ReadErasedSnapshotSkeletonAsync(InteractionProtectionBinding binding, string sealedPayloadSha256, ErasureRootPermanentLossReceipt loss, CancellationToken cancellationToken)
        => Task.FromResult<byte[]?>(null);
    /// <summary>Retains the independent source owner's complete public enrollment before custody effects; omitted implementations deny.</summary>
    Task<bool> RegisterOriginalAsync(ErasureRootOccurrenceEnrollment enrollment, CancellationToken cancellationToken) => Task.FromResult(false);
    /// <summary>Verifies an exact actually persisted ciphertext original under a positive independently authenticated permanent-loss receipt; missing source proof never means Erased.</summary>
    Task<ErasureRootOccurrenceEnrollment?> ResolveErasedStoredAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence,
        string sealedPayloadSha256, string keyReference, ErasureRootPermanentLossReceipt loss, CancellationToken cancellationToken)
        => Task.FromResult<ErasureRootOccurrenceEnrollment?>(null);
    /// <summary>Reads a current independently authorized exact occurrence enrollment.</summary>
    Task<ErasureRootOccurrenceEnrollment?> ResolveAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence, CancellationToken cancellationToken);
    /// <summary>Authenticates the exact keyed content intent and original reservation against the source writer.</summary>
    Task<bool> VerifyReservationAsync(InteractionOccurrenceRequest request, CancellationToken cancellationToken);
    /// <summary>Authenticates actual source persistence or independently proven NeverWritten for this original; Unknown denies.</summary>
    Task<bool> VerifyWriterAsync(InteractionOccurrenceRecord original, string proofId, bool persisted, CancellationToken cancellationToken);
    /// <summary>Authenticates actual exact sealed output under the source's once-only encryption admission.</summary>
    Task<bool> VerifySealedAsync(InteractionOccurrenceRecord original, InteractionOccurrenceSealedResult result, CancellationToken cancellationToken);
}
