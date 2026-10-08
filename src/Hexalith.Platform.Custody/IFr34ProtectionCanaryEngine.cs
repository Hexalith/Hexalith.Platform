using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Actual target-limited engine adapter for the production persist/read/destroy/replay path; no NoOp adapter is supplied.</summary>
public interface IFr34ProtectionCanaryEngine
{
    /// <summary>Seals and durably persists a fresh canary under a dedicated DEK, returning only its opaque exact reference.</summary>
    Task<Fr34CanaryReference?> SealAndPersistAsync(Fr34ProtectionTarget target, string canaryId, byte[] plaintext, CancellationToken cancellationToken = default);
    /// <summary>Unseals the exact persisted record through the enrolled production engine.</summary>
    Task<byte[]?> UnsealAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default);
    /// <summary>Requests irreversible destruction of only the canary's dedicated DEK; acknowledgement alone is insufficient.</summary>
    Task<bool> DestroyAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default);
    /// <summary>Replays the original persisted record through the actual engine after independently confirmed destruction.</summary>
    Task<PayloadUnprotectionOutcome?> ReplayAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default);
}
