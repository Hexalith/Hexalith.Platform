using Hexalith.EventStore.PayloadProtection;

namespace Hexalith.Platform.Protection;

/// <summary>Actual source-owner atomic commit or independently fenced cancellation. A missing cell or unretired writer cannot prove non-persistence.</summary>
/// <param name="Binding">Complete original source binding.</param><param name="SealedPayloadSha256">Actual original sealed source bytes; null only for authoritative cancellation.</param>
/// <param name="Persisted">Whether the source transaction committed.</param><param name="WriterRetired">Whether the source owner permanently retired this exact writer.</param>
/// <param name="ReceiptId">Actual immutable source receipt.</param>
public sealed record ErasureRootWriterProof(InteractionProtectionBinding Binding, string? SealedPayloadSha256, bool Persisted, bool WriterRetired, string ReceiptId);
