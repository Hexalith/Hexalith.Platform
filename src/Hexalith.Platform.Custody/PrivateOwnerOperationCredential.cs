namespace Hexalith.Platform.Custody;

/// <summary>Private candidate purpose-separated operation tag. It cannot be parsed as an AD-30 domain delivery envelope.</summary>
/// <param name="ProfileVersion">Current separately configured signing profile.</param>
/// <param name="Issuer">Configured internal credential issuer.</param>
/// <param name="Audience">Configured private verifier audience.</param>
/// <param name="MachineIssuer">Authenticated transport issuer.</param>
/// <param name="MachineSubject">Dedicated service-account subject.</param>
/// <param name="MachineClient">Dedicated authenticated client.</param>
/// <param name="MachineAudience">Exact transport audience.</param>
/// <param name="AuthorityReference">Independent private grant source.</param>
/// <param name="BindingRevision">Current grant revision.</param>
/// <param name="Scope">Exact immutable owner operation and keyed request fingerprint.</param>
/// <param name="IssuedAt">UTC delivery issue time.</param>
/// <param name="ExclusiveExpiry">Exclusive delivery deadline; no expiry grace.</param>
/// <param name="DeliveryNonce">Fresh independent delivery nonce.</param>
/// <param name="SigningKeyVersion">Exact tenant HMAC verifier version.</param>
/// <param name="Tag">Canonical-byte constant-time verified HMAC.</param>
public sealed record PrivateOwnerOperationCredential(string ProfileVersion, string Issuer, string Audience, string MachineIssuer,
    string MachineSubject, string MachineClient, string MachineAudience, string AuthorityReference, long BindingRevision,
    PrivateOwnerOperationScope Scope, DateTimeOffset IssuedAt, DateTimeOffset ExclusiveExpiry, string DeliveryNonce, string SigningKeyVersion, string Tag);
