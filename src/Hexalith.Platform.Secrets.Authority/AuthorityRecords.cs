using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.Platform.Secrets.Authority;

/// <summary>Public owner-administered profile; no signer or provider key is held here.</summary>
public sealed record AuthorityProfile(string ProfileReference, string PublicTrustReference, string Version, string Issuer,
    string Audience, DateTimeOffset NotBefore, DateTimeOffset ValidUntil, bool Revoked);

/// <summary>An explicitly enrolled query identity, independently of any key-dependent host enrollment.</summary>
public sealed record AuthorityCaller(string CallerId, DateTimeOffset NotBefore, DateTimeOffset ValidUntil, bool Enabled);

/// <summary>An exact current inventory workload, attested by its owner.</summary>
public sealed record AuthorityInventoryHost(string InventoryAuthorityReference, string CallerId, string WorkloadReference,
    DateTimeOffset NotBefore, DateTimeOffset ValidUntil, bool Enrolled);

/// <summary>Explicit current status of one opaque version in one tenant/purpose.</summary>
public sealed record AuthorityKeyStatus(string TenantId, string Purpose, string Version, string ProfileVersion,
    DateTimeOffset NotBefore, DateTimeOffset ValidUntil, bool Revoked);

/// <summary>An original revision-checked owner operation. Exactly one record must match its kind.</summary>
public sealed record AuthorityAdministration(string OperationId, string ExpectedRevision, string Kind,
    AuthorityProfile? Profile = null, AuthorityCaller? Caller = null, AuthorityInventoryHost? Host = null, AuthorityKeyStatus? KeyStatus = null);

/// <summary>The original durable outcome; identical retries return this exact value.</summary>
public sealed record AuthorityReceipt(string OperationId, string AdministratorId, string PayloadSha256, string Kind,
    string PreviousRevision, string Revision, string Outcome, DateTimeOffset RecordedAt);

/// <summary>One coherent persisted generation including all original administration receipts.</summary>
public sealed record AuthorityState(string InstanceId, long Generation, AuthorityProfile? Profile,
    AuthorityCaller[] Callers, AuthorityInventoryHost[] Hosts, AuthorityKeyStatus[] KeyStatuses, AuthorityReceipt[] Audit)
{
    /// <summary>The state generation is independent of wall-clock proof timestamps.</summary>
    public string Revision => InstanceId + ":" + Generation.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static AuthorityState Empty() => new(Guid.NewGuid().ToString("N"), 0, null, [], [], [], []);
}

/// <summary>The existing exact-scope current-revocation query contract.</summary>
public sealed record AuthorityRevocationQuery(string AuthorityReference, string PublicTrustReference, string ProfileReference,
    string InventoryAuthorityReference, string ProfileVersion, string Issuer, string Audience,
    string? TenantId, string? Purpose, string? Version);

/// <summary>Strict bounded public JSON contracts.</summary>
public static class AuthorityJson
{
    /// <summary>Rejects unknown members; the HTTP reader also rejects duplicate names.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, PropertyNameCaseInsensitive = false,
        RespectRequiredConstructorParameters = true, MaxDepth = 32 };
}
