using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Complete immutable registration and original irreversible manifest coverage, without retained deleted ciphertext.</summary>
internal sealed record ErasureRootErasure(CustodyKeyRegistration Registration, DeletionTargetReceipt Receipt,
    string ReservationReceiptId, int TrimmedToEpoch);
