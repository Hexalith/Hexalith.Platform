using Hexalith.EventStore.PayloadProtection;

namespace Hexalith.Platform.Protection;

/// <summary>Actual canonical canary-source producer original, retained only after exact writer/engine completion; no plaintext or root material.</summary>
internal sealed record ProtectionQualificationSeedOriginal(InteractionProtectionBinding Binding, string SourceIdentity, string SealedPayloadSha256);
