using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Independent current source-writer/intent authority. It provides exact originals, never key bytes or caller-selected targets.</summary>
public interface IErasureRootOccurrenceSource
{
    /// <summary>Reads a current independently authorized exact occurrence enrollment.</summary>
    Task<ErasureRootOccurrenceEnrollment?> ResolveAsync(AggregateIdentity owner, PayloadProtectionOccurrenceContext occurrence, CancellationToken cancellationToken);
    /// <summary>Authenticates the exact keyed content intent and original reservation against the source writer.</summary>
    Task<bool> VerifyReservationAsync(InteractionOccurrenceRequest request, CancellationToken cancellationToken);
    /// <summary>Authenticates actual source persistence or independently proven NeverWritten for this original; Unknown denies.</summary>
    Task<bool> VerifyWriterAsync(InteractionOccurrenceRecord original, string proofId, bool persisted, CancellationToken cancellationToken);
    /// <summary>Authenticates actual exact sealed output under the source's once-only encryption admission.</summary>
    Task<bool> VerifySealedAsync(InteractionOccurrenceRecord original, InteractionOccurrenceSealedResult result, CancellationToken cancellationToken);
}
