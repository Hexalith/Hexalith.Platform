using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Exact independently supplied existing reservation and complete immutable native request; the logical canary never supplies either identity.</summary>
public sealed record Fr34NativeManifestBinding(string ReservationReceiptId,DeletionBatchConsumptionRequest Original);
