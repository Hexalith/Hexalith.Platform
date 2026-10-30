using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Actual native pinned-target refusal original. It grants no registration, reservation, unpin or physical destruction.</summary>
public sealed record ErasureRootPinRefusal(string OperationId, string Status, string ReceiptId, ProtectionTarget Target);
