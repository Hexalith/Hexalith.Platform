using System.Text.Json.Nodes;

namespace Hexalith.Platform.Protection;

/// <summary>Native atomic original for one exact admitted copy call and persisted ciphertext/location fingerprint.</summary>
internal sealed record ProtectionQualificationCopyOriginal(string CallDigest, string SourceIdentity, string SourceDigest, JsonNode Result);
