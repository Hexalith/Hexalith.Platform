using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Inventory;

/// <summary>Exact current HMAC metadata authority. Missing profile/trust, enrollment or provider mapping always denies.</summary>
public sealed class CurrentInventoryAuthority(
    SecretsRuntimeConfiguration configuration,
    IHmacCurrentTrust independentTrust,
    IInventorySources sources,
    JsonArray permittedReferences,
    TimeProvider clock) : IDaprPlatformHmacAuthority
{
    /// <inheritdoc/>
    public PlatformSigningProfile? GetCurrentProfile(string profileReference)
    {
        if (configuration.ExpectedProfile is null || profileReference != configuration.ProfileReference
            || string.IsNullOrWhiteSpace(configuration.PublicTrustReference) || string.IsNullOrWhiteSpace(configuration.CurrentRevocationReference)) { return null; }
        try
        {
            Task<CurrentHmacTrust?> pending = Task.Run(() => independentTrust.ReadAsync(null, null, CancellationToken.None));
            _ = pending.ContinueWith(task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            CurrentHmacTrust? current = pending.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            return Current(current) ? current!.Profile : null;
        }
        catch (Exception) { return null; }
    }

    /// <inheritdoc/>
    public async ValueTask<DaprPlatformHmacInventory?> ResolveAsync(string authorityReference, PlatformHmacScope scope, string? version, CancellationToken cancellationToken)
    {
        if (scope is null || !scope.IsValid || authorityReference != configuration.CurrentInventoryAuthorityReference
            || string.IsNullOrWhiteSpace(configuration.ProviderBinding) || string.IsNullOrWhiteSpace(configuration.HmacCredentialReference)
            || string.IsNullOrWhiteSpace(configuration.PublicTrustReference) || string.IsNullOrWhiteSpace(configuration.CurrentRevocationReference)
            || configuration.ProfileReference is not { } profileReference) { return null; }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            CurrentHmacTrust? profileTrust = await independentTrust.ReadAsync(null, null, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            if (!Current(profileTrust)) { return null; }
            JsonObject[] matches = permittedReferences.OfType<JsonObject>().Where(item =>
                Text(item, "tenantId") == scope.TenantId && Text(item, "purpose") == scope.Purpose.ToString()).ToArray();
            if (matches.Length != 1) { return null; }
            JsonObject allowed = matches[0];
            string? secretReference = Text(allowed, "secretReference");
            string? secretField = Text(allowed, "secretField");
            if (!SecretName(secretReference) || !Reference(secretField)) { return null; }
            // Dapr prepends enginePath/data; only the metadata reader receives the qualified provider path.
            string metadataReference = "ext-secrets-1-isolated/" + secretReference;
            NativeEnrollment? tenant = await sources.EnrollmentAsync(scope.TenantId, budget.Token).ConfigureAwait(false);
            if (tenant is not { Active: true } || tenant.TenantId != scope.TenantId) { return null; }
            JsonObject? metadata = await sources.MetadataAsync(metadataReference, budget.Token).ConfigureAwait(false);
            DaprPlatformHmacInventory? result = Map(metadata, allowed, scope, version, tenant.Revision);
            if (result is null) { return null; }
            CurrentHmacTrust? keyTrust = await independentTrust.ReadAsync(scope, result.Metadata.Version, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            if (!Current(keyTrust) || keyTrust!.Profile != profileTrust!.Profile) { return null; }
            result = result with { Revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.Revision + "\n" + keyTrust.Revision))) };
            // Observe both sources again; a changed native revision or backend document cannot release a stale reference.
            JsonObject? after = await sources.MetadataAsync(metadataReference, budget.Token).ConfigureAwait(false);
            NativeEnrollment? currentTenant = await sources.EnrollmentAsync(scope.TenantId, budget.Token).ConfigureAwait(false);
            CurrentHmacTrust? currentTrust = await independentTrust.ReadAsync(scope, result.Metadata.Version, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            if (tenant != currentTenant || !JsonNode.DeepEquals(metadata, after)
                || !Current(currentTrust) || currentTrust!.Profile != keyTrust.Profile || currentTrust.Revision != keyTrust.Revision
                || budget.IsCancellationRequested || clock.GetUtcNow() >= result.ValidUntil) { return null; }
            DateTimeOffset expires = new[] { result.ValidUntil, result.Metadata.VerifyUntil, profileTrust!.ValidUntil,
                keyTrust.ValidUntil, currentTrust.ValidUntil, profileTrust.Profile.ValidUntil }.Min();
            return clock.GetUtcNow() < expires ? result with { ValidUntil = expires } : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return null; }
    }

    private bool Current(CurrentHmacTrust? proof)
    {
        DateTimeOffset now = clock.GetUtcNow();
        return proof is not null && proof.Profile == configuration.ExpectedProfile && !string.IsNullOrWhiteSpace(proof.Revision)
            && proof.ObservedAt <= now && now < proof.ValidUntil && proof.ValidUntil > proof.ObservedAt
            && proof.ValidUntil - proof.ObservedAt <= TimeSpan.FromSeconds(30)
            && now >= proof.Profile.NotBefore && now < proof.Profile.ValidUntil;
    }

    private DaprPlatformHmacInventory? Map(JsonObject? metadata, JsonObject allowed, PlatformHmacScope scope, string? requestedVersion, string tenantRevision)
    {
        if (metadata?["custom_metadata"] is not JsonObject custom || Text(custom, "ext-secrets-1.tenantId") != scope.TenantId
            || Text(custom, "ext-secrets-1.purpose") != scope.Purpose.ToString()
            || Text(custom, "ext-secrets-1.secretField") != Text(allowed, "secretField")
            || Text(custom, "ext-secrets-1.versionMappings") is not { } encoded) { return null; }
        JsonObject? mappings = InventorySources.ParseObject(encoded);
        if (mappings is null) { return null; }
        var candidates = mappings.Where(entry => entry.Value is JsonObject item
            && (requestedVersion is not null ? entry.Key == requestedVersion : Text(item, "state") == "Active")).ToArray();
        if (candidates.Length != 1 || !Reference(candidates[0].Key) || candidates[0].Value is not JsonObject mapping
            || Text(mapping, "secretVersion") is not { } numeric
            || !ulong.TryParse(numeric, NumberStyles.None, CultureInfo.InvariantCulture, out ulong backend) || backend == 0
            || backend.ToString(CultureInfo.InvariantCulture) != numeric
            || metadata["versions"]?[numeric] is not JsonObject nativeVersion
            || nativeVersion["destroyed"]?.GetValue<bool>() != false
            || Text(nativeVersion, "deletion_time") != ""
            || Text(mapping, "state") is not { } stateName || !Enum.TryParse(stateName, out PlatformHmacKeyState state)
            || !Enum.IsDefined(state) || state.ToString() != stateName
            || Instant(mapping, "notBefore") is not { } notBefore || Instant(mapping, "verifyUntil") is not { } verifyUntil
            || Instant(nativeVersion, "created_time") is not { } created || notBefore < created
            || state == PlatformHmacKeyState.Active && metadata["current_version"]?.GetValue<ulong>() != backend) { return null; }
        DateTimeOffset? retiredAt = mapping["retiredAt"] is null ? null : Instant(mapping, "retiredAt");
        DateTimeOffset now = clock.GetUtcNow();
        if (notBefore > now || now >= verifyUntil || verifyUntil <= notBefore
            || state == PlatformHmacKeyState.Revoked
            || scope.Purpose == PlatformHmacPurpose.SecurityObservation && scope.TenantId != "system"
            || state == PlatformHmacKeyState.Retained && (retiredAt is null || retiredAt > now)
            || mapping["retiredAt"] is not null && retiredAt is null) { return null; }
        string revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tenantRevision + "\n" + metadata.ToJsonString())));
        return new(configuration.CurrentInventoryAuthorityReference!, revision, scope,
            new(candidates[0].Key, state, notBefore, verifyUntil, retiredAt), configuration.ProviderBinding!,
            configuration.HmacCredentialReference!, Text(allowed, "secretReference")!, numeric, Text(allowed, "secretField")!, now, now.AddSeconds(30));
    }

    private static string? Text(JsonObject value, string name) => value[name] is JsonValue node && node.TryGetValue(out string? text) ? text : null;

    private static DateTimeOffset? Instant(JsonObject value, string name)
        => DateTimeOffset.TryParse(Text(value, name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset instant)
            && instant.Offset == TimeSpan.Zero ? instant : null;

    private static bool SecretName(string? value)
        => Reference(value) && !value!.StartsWith("ext-secrets-1-isolated/", StringComparison.Ordinal)
            && value.Split('/').All(part => part.Length > 0 && part is not "." and not ".."
                && part.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));

    private static bool Reference(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 2048 && !value.Any(char.IsControl);
}
