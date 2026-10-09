namespace Hexalith.Platform.Custody;

/// <summary>Private opaque server-receipt identity and exact original content-free fingerprint. Routing is recovered solely from the independently anchored spool.</summary>
/// <param name="RetainedServerReceiptKey">Separate stable opaque retained-server-receipt HMAC lookup key, excluding mutable current origin authentication.</param><param name="ReasonCode">Original closed denial reason.</param>
/// <param name="UntrustedFieldsHmac">Exact purpose-separated original fingerprint.</param><param name="DigestKeyVersion">Original retained system digest key version.</param>
public sealed record SecurityObservationOriginalLookup(string RetainedServerReceiptKey, string ReasonCode, string UntrustedFieldsHmac, string DigestKeyVersion);
