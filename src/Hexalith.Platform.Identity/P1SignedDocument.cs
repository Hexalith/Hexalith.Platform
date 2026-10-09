namespace Hexalith.Platform.Identity;

/// <summary>Retained exact payload bytes and a 64-byte P-256/SHA-256 P1363 detached signature.</summary>
/// <param name="Payload">Exact signed bytes.</param>
/// <param name="Signature">Detached signature bytes.</param>
public sealed record P1SignedDocument(byte[] Payload, byte[] Signature);
