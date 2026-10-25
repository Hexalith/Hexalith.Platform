using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Private actual command-status plus native-source reader for definitive replay rejection. Missing or queued submissions are never absence.</summary>
public interface IPrivateReplayRecoverySource
{
    /// <summary>Reads the exact rejected/completed no-event original without dispatching any command.</summary>
    Task<PrivateReplayRecoveryProof?> ReadReplayRecoveryProofAsync(TrustedEnvelopeReplayIntent intent,CancellationToken cancellationToken);
}
