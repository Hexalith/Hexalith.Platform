using System.Globalization;
using System.Text.Json.Nodes;

using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Enrollable public HMAC inventory and profile authority. It never reads secret bytes; the existing binding check remains that gate.</summary>
public sealed class DaprHmacAuthorityAdapter : IDaprPlatformHmacAuthority
{
    private readonly SecretsRuntimeConfiguration _configuration;
    private readonly SecretsDaprSession _session;

    /// <summary>Creates an authority over explicit references. A missing reference is reported by the operation that needs it.</summary>
    public DaprHmacAuthorityAdapter(SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(invoker);
        _configuration = configuration;
        _session = new SecretsDaprSession(invoker, configuration.CallTimeout ?? TimeSpan.Zero, clock ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public PlatformSigningProfile? GetCurrentProfile(string profileReference)
        => Task.Run(() => GetCurrentProfileAsync(profileReference)).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async ValueTask<DaprPlatformHmacInventory?> ResolveAsync(string authorityReference, PlatformHmacScope scope, string? version, CancellationToken cancellationToken)
    {
        Require(_configuration.ProviderBinding, "runtime.providerBinding");
        Require(_configuration.HmacCredentialReference, "runtime.hmacCredentialReference");
        Require(_configuration.CurrentInventoryAuthorityReference, "runtime.currentInventoryAuthorityReference");
        Require(_configuration.ProfileReference, "runtime.profileReference");
        if (authorityReference != _configuration.CurrentInventoryAuthorityReference || scope is null || !scope.IsValid) { return null; }
        var body = new JsonObject
        {
            ["authorityReference"] = _configuration.CurrentInventoryAuthorityReference,
            ["tenantId"] = scope.TenantId,
            ["purpose"] = scope.Purpose.ToString(),
            ["version"] = version,
            ["storeName"] = _configuration.ProviderBinding,
            ["credentialReference"] = _configuration.HmacCredentialReference,
        };
        JsonObject? proof = (await _session.CallAsync(_configuration.CurrentInventoryAuthorityReference, "runtime.currentInventoryAuthorityReference", "query-inventory", body, cancellationToken).ConfigureAwait(false)).Proof;
        return Inventory(proof, scope, version);
    }

    private async Task<PlatformSigningProfile?> GetCurrentProfileAsync(string profileReference)
    {
        Require(_configuration.ProfileReference, "runtime.profileReference");
        Require(_configuration.Issuer, "runtime.issuer");
        Require(_configuration.Audience, "runtime.audience");
        Require(_configuration.ProfileVersion, "runtime.profileVersion");
        if (_configuration.ProfileNotBefore is null) { Require(null, "runtime.profileNotBefore"); }
        if (_configuration.ProfileValidUntil is null) { Require(null, "runtime.profileValidUntil"); }
        if (_configuration.ExpectedProfile is null || profileReference != _configuration.ProfileReference) { return null; }
        JsonObject? proof = (await _session.CallAsync(_configuration.ProfileReference, "runtime.profileReference", "query-profile",
            new JsonObject { ["profileReference"] = _configuration.ProfileReference }, CancellationToken.None).ConfigureAwait(false)).Proof;
        PlatformSigningProfile expected = _configuration.ExpectedProfile;
        return SecretsProof.SameText(proof, "issuer", expected.Issuer) && SecretsProof.SameText(proof, "audience", expected.Audience)
            && SecretsProof.SameText(proof, "version", expected.Version) && SecretsProof.Instant(proof, "notBefore") == expected.NotBefore
            && SecretsProof.Instant(proof, "validUntil") == expected.ValidUntil ? expected : null;
    }

    private DaprPlatformHmacInventory? Inventory(JsonObject? proof, PlatformHmacScope scope, string? version)
    {
        if (proof is null || !SecretsProof.SameText(proof, "tenantId", scope.TenantId) || !SecretsProof.SameText(proof, "purpose", scope.Purpose.ToString())
            || !SecretsProof.SameText(proof, "authorityReference", _configuration.CurrentInventoryAuthorityReference)
            || !SecretsProof.SameText(proof, "storeName", _configuration.ProviderBinding)
            || !SecretsProof.SameText(proof, "credentialReference", _configuration.HmacCredentialReference)) { return null; }
        string? keyVersion = SecretsProof.Text(proof, "version");
        string? revision = SecretsProof.Text(proof, "revision");
        string? secretReference = SecretsProof.Text(proof, "secretReference");
        string? secretVersion = SecretsProof.Text(proof, "secretVersion");
        string? secretField = SecretsProof.Text(proof, "secretField");
        string? stateName = SecretsProof.Text(proof, "keyState");
        DateTimeOffset? notBefore = SecretsProof.Instant(proof, "notBefore");
        DateTimeOffset? verifyUntil = SecretsProof.Instant(proof, "verifyUntil");
        DateTimeOffset? observedAt = SecretsProof.Instant(proof, "observedAt");
        DateTimeOffset? validUntil = SecretsProof.Instant(proof, "validUntil");
        if (!SecretsProvisioning.IsReference(keyVersion) || !SecretsProvisioning.IsReference(revision) || !SecretsProvisioning.IsReference(secretReference)
            || !SecretsProvisioning.IsReference(secretField) || stateName is null || !Enum.TryParse(stateName, out PlatformHmacKeyState state) || !Enum.IsDefined(state) || state.ToString() != stateName
            || notBefore is null || verifyUntil is null || observedAt is null || validUntil is null
            || version is not null && keyVersion != version) { return null; }
        if (!ulong.TryParse(secretVersion, NumberStyles.None, CultureInfo.InvariantCulture, out ulong backend) || backend == 0
            || backend.ToString(CultureInfo.InvariantCulture) != secretVersion) { return null; }
        if (validUntil <= observedAt || validUntil.Value - observedAt.Value > TimeSpan.FromSeconds(30)) { return null; }
        DateTimeOffset now = _session.Clock.GetUtcNow();
        if (observedAt > now || now >= validUntil) { return null; }
        DateTimeOffset? retiredAt = proof["retiredAt"] is null ? null : SecretsProof.Instant(proof, "retiredAt");
        if (proof.ContainsKey("retiredAt") && proof["retiredAt"] is not null && retiredAt is null) { return null; }
        var metadata = new PlatformHmacKeyMetadata(keyVersion!, state, notBefore.Value, verifyUntil.Value, retiredAt);
        return new DaprPlatformHmacInventory(_configuration.CurrentInventoryAuthorityReference!, revision!, scope, metadata,
            _configuration.ProviderBinding!, _configuration.HmacCredentialReference!, secretReference!, secretVersion!, secretField!, observedAt.Value, validUntil.Value);
    }

    private static void Require(string? value, string input)
    {
        if (!SecretsProvisioning.IsReference(value))
        {
            throw new SecretsDependencyNotAvailableException(input, SecretsProvisioning.Action(input));
        }
    }
}
