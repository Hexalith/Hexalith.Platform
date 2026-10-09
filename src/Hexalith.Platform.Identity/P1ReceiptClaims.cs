namespace Hexalith.Platform.Identity;

/// <summary>Exact claims of a Platform P1 receipt. Every field is present for every receipt kind.</summary>
/// <param name="ReceiptId">Issuer-generated 32-byte lowercase hexadecimal identifier.</param>
/// <param name="Issuer">Independently enrolled issuer identifier.</param>
/// <param name="KeyFingerprint">SHA-256 of the enrolled receipt key's DER SPKI, in lowercase hexadecimal.</param>
/// <param name="Audience">Exact consumer audience.</param>
/// <param name="Kind">One of producer-execution, grant, target, or custody.</param>
/// <param name="Action">Exact authorized action.</param>
/// <param name="Principal">Stable issuer-qualified principal identifier.</param>
/// <param name="SubjectRef">Immutable subject reference.</param>
/// <param name="SubjectSha256">SHA-256 of the exact subject bytes.</param>
/// <param name="SubjectLength">Length of the exact subject bytes.</param>
/// <param name="ProfileSha256">Exact profile digest.</param>
/// <param name="WorkloadSha256">Exact workload digest.</param>
/// <param name="TenantId">Exact tenant identifier.</param>
/// <param name="ClusterId">Authenticated cluster identifier.</param>
/// <param name="NamespaceUid">Authenticated Kubernetes namespace UID.</param>
/// <param name="TargetSha256">Exact authenticated target digest.</param>
/// <param name="SessionId">Immutable session identifier.</param>
/// <param name="SourceRevision">Exact source revision.</param>
/// <param name="RegistryRevision">Exact registry revision.</param>
/// <param name="PolicyRevision">Exact policy revision.</param>
/// <param name="GrantId">Immutable grant identifier.</param>
/// <param name="Decision">Exact decision or observation outcome.</param>
/// <param name="Reasons">Exact UTF-8 reason text, including the empty string when no reason exists.</param>
/// <param name="IssuedAtUtc">UTC issuance instant.</param>
/// <param name="ExpiresAtUtc">Exclusive UTC expiry instant.</param>
public sealed record P1ReceiptClaims(
    string ReceiptId, string Issuer, string KeyFingerprint, string Audience, string Kind, string Action,
    string Principal, string SubjectRef, string SubjectSha256, long SubjectLength, string ProfileSha256,
    string WorkloadSha256, string TenantId, string ClusterId, string NamespaceUid, string TargetSha256,
    string SessionId, string SourceRevision, string RegistryRevision, string PolicyRevision, string GrantId,
    string Decision, string Reasons, DateTimeOffset IssuedAtUtc, DateTimeOffset ExpiresAtUtc);
