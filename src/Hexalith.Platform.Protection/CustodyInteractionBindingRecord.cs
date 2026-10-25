using Hexalith.EventStore.PayloadProtection;

namespace Hexalith.Platform.Protection;

/// <summary>Independently anchored immutable occurrence binding with the one durable completion lease and separately observed erasure witness. It contains no payload or key bytes.</summary>
public sealed record CustodyInteractionBindingRecord(long Revision,InteractionProtectionBinding Binding,string? CompletionLeaseId=null,InteractionProtectionErasureWitness? Erasure=null,string? EncryptionLeaseId=null,string? EncryptionKeyReference=null);
