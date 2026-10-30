namespace Hexalith.Platform.Protection;

/// <summary>External current installed permission original, authenticated by the independent permission owner after isolated capability/denial checks. Role processes cannot create this evidence.</summary>
/// <param name="TenantId">Exact tenant.</param><param name="StoreId">Native ERC identity.</param><param name="EnrollmentId">Native enrollment.</param>
/// <param name="WorkloadIdentity">Actual installed workload instance.</param><param name="RoleIdentity">Enrolled observer identity.</param><param name="BindingDigest">Accepted role installation.</param>
/// <param name="NativeCredentialSha256">Actual ERC bearer fingerprint.</param><param name="DaprCredentialSha256">Actual Dapr API bearer fingerprint.</param><param name="RoleCredentialSha256">Actual role bearer fingerprint.</param>
/// <param name="CredentialScope">Independently established read-only scope.</param><param name="NativeReadPaths">Exact native read scopes.</param><param name="DaprReadComponents">Exact Dapr state-read scopes.</param>
/// <param name="DaprApiScopes">Installed Dapr API scopes.</param><param name="DeniedCapabilities">Isolated denied capabilities checked by the independent installer.</param>
/// <param name="ObservedAt">Current proof instant.</param><param name="ValidUntil">Exclusive expiry.</param><param name="ReceiptId">Independent immutable proof identity.</param>
public sealed record ProtectionQualificationObserverPermission(string TenantId, string StoreId, string EnrollmentId, string WorkloadIdentity, string RoleIdentity, string BindingDigest,
    string NativeCredentialSha256, string DaprCredentialSha256, string RoleCredentialSha256, string CredentialScope, string[] NativeReadPaths, string[] DaprReadComponents,
    string[] DaprApiScopes, string[] DeniedCapabilities, DateTimeOffset ObservedAt, DateTimeOffset ValidUntil, string ReceiptId);
