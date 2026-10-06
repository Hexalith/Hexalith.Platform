namespace Hexalith.Platform.Custody;

/// <summary>Content-free cryptographic outcome; success is not domain/replay authorization.</summary>
/// <param name="Status">Safe result.</param>
/// <param name="Envelope">Immutable signed delivery only on success.</param>
public sealed record TrustedEnvelopeResult(CustodyStatus Status, TrustedEnvelope? Envelope = null);
