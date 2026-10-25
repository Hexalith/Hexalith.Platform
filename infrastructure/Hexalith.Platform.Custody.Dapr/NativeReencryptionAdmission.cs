namespace Hexalith.Platform.Custody.Dapr;

// Explicit additional plaintext-producing native holders, independently admitted before the retained worker send.
internal sealed record NativeReencryptionAdmission(string TenantId, string SourceEpoch, string DestinationEpoch,
    string InputSha256, string ProducerIncarnation, string TransportIncarnation, string ProfileDigest,
    string ProducerHolderOriginalSha256, string NativeHolderOriginalSha256);
