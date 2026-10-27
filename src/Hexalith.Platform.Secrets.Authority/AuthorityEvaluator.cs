using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Authority;

/// <summary>Resolves profiles directly from coherent persisted authority state, never from inventory.</summary>
public sealed class AuthorityEvaluator(string appId, IAuthorityRepository repository, TimeProvider clock)
{
    /// <summary>Returns the unchanged public profile contract to an explicitly current query caller.</summary>
    public async Task<JsonObject?> ProfileAsync(string caller, string reference, CancellationToken cancellationToken)
    {
        AuthorityState state = await repository.ReadAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = clock.GetUtcNow();
        if (!Enrolled(state, caller, now) || !Current(state.Profile, now) || state.Profile!.ProfileReference != reference
            || reference != appId) { return null; }
        AuthorityProfile profile = state.Profile;
        return new JsonObject { ["issuer"] = profile.Issuer, ["audience"] = profile.Audience, ["version"] = profile.Version,
            ["notBefore"] = profile.NotBefore, ["validUntil"] = profile.ValidUntil };
    }

    /// <summary>Returns only explicitly current exact host/profile/version status, with at most thirty seconds of proof life.</summary>
    public async Task<JsonObject?> RevocationAsync(string caller, AuthorityRevocationQuery query, CancellationToken cancellationToken)
    {
        AuthorityState state = await repository.ReadAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = clock.GetUtcNow();
        if (!Enrolled(state, caller, now) || !Current(state.Profile, now) || query.AuthorityReference != appId
            || query.ProfileReference != appId || query.PublicTrustReference != state.Profile!.PublicTrustReference
            || query.ProfileVersion != state.Profile.Version || query.Issuer != state.Profile.Issuer || query.Audience != state.Profile.Audience
            || query.InventoryAuthorityReference == appId) { return null; }
        AuthorityInventoryHost? host = state.Hosts.SingleOrDefault(item => item.InventoryAuthorityReference == query.InventoryAuthorityReference);
        if (host is null || !host.Enrolled || host.CallerId != caller || !Window(host.NotBefore, host.ValidUntil, now)) { return null; }
        DateTimeOffset expires = new[] { now.AddSeconds(30), state.Profile.ValidUntil, host.ValidUntil,
            state.Callers.Single(item => item.CallerId == caller).ValidUntil }.Min();
        if (query.TenantId is not null || query.Purpose is not null || query.Version is not null)
        {
            if (!Scope(query.TenantId, query.Purpose) || !Text(query.Version)) { return null; }
            AuthorityKeyStatus? key = state.KeyStatuses.SingleOrDefault(item => item.TenantId == query.TenantId
                && item.Purpose == query.Purpose && item.Version == query.Version);
            if (key is null || key.Revoked || key.ProfileVersion != state.Profile.Version || !Window(key.NotBefore, key.ValidUntil, now)) { return null; }
            expires = expires < key.ValidUntil ? expires : key.ValidUntil;
        }
        return new JsonObject
        {
            ["authorityReference"] = query.AuthorityReference, ["publicTrustReference"] = query.PublicTrustReference,
            ["profileReference"] = query.ProfileReference, ["inventoryAuthorityReference"] = query.InventoryAuthorityReference,
            ["profileVersion"] = query.ProfileVersion, ["issuer"] = query.Issuer, ["audience"] = query.Audience,
            ["tenantId"] = query.TenantId, ["purpose"] = query.Purpose, ["version"] = query.Version,
            ["status"] = "Current", ["revoked"] = false, ["inventoryHostEnrolled"] = true,
            ["revision"] = state.Revision, ["observedAt"] = now, ["validUntil"] = expires,
        };
    }

    internal static bool Valid(AuthorityAdministration operation)
    {
        if (!Text(operation.OperationId) || !Text(operation.ExpectedRevision)
            || new object?[] { operation.Profile, operation.Caller, operation.Host, operation.KeyStatus }.Count(item => item is not null) != 1) { return false; }
        return operation.Kind switch
        {
            "PublishProfile" => operation.Profile is { } profile && Text(profile.ProfileReference) && Text(profile.PublicTrustReference)
                && Text(profile.Version) && Text(profile.Issuer) && Text(profile.Audience) && Bounds(profile.NotBefore, profile.ValidUntil),
            "SetCaller" => operation.Caller is { } caller && Text(caller.CallerId) && Bounds(caller.NotBefore, caller.ValidUntil),
            "SetInventoryHost" => operation.Host is { } host && Text(host.InventoryAuthorityReference) && Text(host.CallerId)
                && Text(host.WorkloadReference) && Bounds(host.NotBefore, host.ValidUntil),
            "SetKeyStatus" => operation.KeyStatus is { } key && Scope(key.TenantId, key.Purpose) && Text(key.Version)
                && Text(key.ProfileVersion) && Bounds(key.NotBefore, key.ValidUntil),
            _ => false,
        };
    }

    private static bool Enrolled(AuthorityState state, string caller, DateTimeOffset now)
        => state.Callers.SingleOrDefault(item => item.CallerId == caller) is { Enabled: true } enrollment && Window(enrollment.NotBefore, enrollment.ValidUntil, now);
    private static bool Current(AuthorityProfile? profile, DateTimeOffset now)
        => profile is { Revoked: false } && Window(profile.NotBefore, profile.ValidUntil, now);
    private static bool Scope(string? tenant, string? purpose) => Text(tenant) && Text(purpose)
        && Enum.TryParse(purpose, out PlatformHmacPurpose parsed) && Enum.IsDefined(parsed) && parsed.ToString() == purpose
        && new PlatformHmacScope(tenant!, parsed).IsValid;
    private static bool Bounds(DateTimeOffset start, DateTimeOffset end) => start.Offset == TimeSpan.Zero && end.Offset == TimeSpan.Zero && start < end;
    private static bool Window(DateTimeOffset start, DateTimeOffset end, DateTimeOffset now) => Bounds(start, end) && start <= now && now < end;
    private static bool Text(string? text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 2048 && !text.Any(char.IsControl);
}
