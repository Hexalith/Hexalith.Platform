using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Exact authenticated compromise evidence retained at the nonrollback ERC before protection registration. It contains no signing private key or content.</summary>
/// <param name="Envelope">Closed source revocation.</param><param name="SignedEvidence">Original opaque authentication evidence, reverified on every read.</param>
public sealed record ErasureRootRevocationOriginal(DeletionCapabilityRevocationEnvelope Envelope, string SignedEvidence);
