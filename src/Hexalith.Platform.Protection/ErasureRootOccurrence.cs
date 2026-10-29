using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Retained independent occurrence original and exact one-time leases; no plaintext or root bytes.</summary>
internal sealed record ErasureRootOccurrence(ErasureRootOccurrenceEnrollment Enrollment, CustodyKeyRegistration Registration,
    string? EncryptionRequestDigest = null, bool CompletionLease = false, long ReadOrdinal = 0);
