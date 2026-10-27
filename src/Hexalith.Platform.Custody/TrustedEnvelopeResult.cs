namespace Hexalith.Platform.Custody;

/// <summary>Content-free cryptographic outcome; success is not domain/replay authorization.</summary>
/// <param name="Status">Safe result.</param>
/// <param name="EvaluatedAt">Actual owned decision instant; absent historical results provide no exact temporal evidence.</param>
/// <param name="Envelope">Immutable signed delivery only on success.</param>
public sealed record TrustedEnvelopeResult(CustodyStatus Status, TrustedEnvelope? Envelope = null, DateTimeOffset? EvaluatedAt = null)
{
    /// <summary>Source-owned local provenance, separate from retained/wire cryptographic results.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public TrustedEnvelopeDenialCause DenialCause { get; init; }
}
