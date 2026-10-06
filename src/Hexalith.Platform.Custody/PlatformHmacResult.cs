namespace Hexalith.Platform.Custody;

/// <summary>A safe keyed digest, never key material.</summary>
/// <param name="Status">The content-free outcome.</param>
/// <param name="KeyVersion">The version which must accompany a persisted digest.</param>
/// <param name="Digest">The lowercase hexadecimal SHA-256 HMAC.</param>
public sealed record PlatformHmacResult(CustodyStatus Status, string? KeyVersion = null, string? Digest = null);
