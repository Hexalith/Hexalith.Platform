namespace Hexalith.Platform.Custody;

/// <summary>Opaque exact canary persistence/key reference; contains no challenge or content.</summary>
/// <param name="Target">Actual enrolled engine and custody target.</param><param name="CanaryId">Fresh caller-generated canary identity.</param>
/// <param name="RecordId">Original persisted record reference.</param><param name="KeyReference">Dedicated canary DEK reference.</param>
public sealed record Fr34CanaryReference(Fr34ProtectionTarget Target, string CanaryId, string RecordId, string KeyReference);
