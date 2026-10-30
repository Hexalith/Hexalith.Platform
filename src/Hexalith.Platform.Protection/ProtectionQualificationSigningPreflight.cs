using System.Security.Cryptography;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Reads the actual remote signing owner before any qualification attempt or source admission.</summary>
internal static class ProtectionQualificationSigningPreflight
{
    internal static async Task<bool> VerifyAsync(ErasureRootHostInstallation host, string tenant, string installationBinding,
        string providerIdentity, IDeletionCapabilitySigningActor signer, CancellationToken token, IReadOnlySet<string>? requiredVersions = null)
    {
        if (requiredVersions is { Count: 0 }) { return true; } // The selected source case has no Sign operation.
        var trusts = host.CapabilityTrust.Where(value => value.TenantId == tenant && (requiredVersions is null || requiredVersions.Contains(value.CapabilityKeyVersion))).ToArray();
        if (trusts.Length == 0 || requiredVersions is not null && !trusts.Select(value => value.CapabilityKeyVersion).ToHashSet(StringComparer.Ordinal).SetEquals(requiredVersions)) { return false; }
        var requests = trusts.Select(trust => new DeletionCapabilitySigningReadinessRequest(tenant, trust.KeyFamily, trust.CapabilityKeyVersion, trust.Issuer, trust.Audience,
            trust.PublicAnchorId, trust.PublicAnchorVersion, Convert.ToHexStringLower(SHA256.HashData(trust.SubjectPublicKeyInfo)), trust.TrustProfileRevision, installationBinding, providerIdentity)).ToArray();
        return await VerifyRequestsAsync(requests, signer, token).ConfigureAwait(false);
    }
    internal static IReadOnlySet<string> RequiredKeyVersions(ProtectionQualificationServiceScope scope, string? caseId = null)
    {
        var selected = caseId is null ? scope.Recipes.Values : scope.Recipes.TryGetValue(caseId, out var actions)
            ? new[] { actions }.AsEnumerable() : throw new InvalidOperationException("Exact installed owner case required for signing readiness.");
        var calls = selected.SelectMany(actions => actions.Values).SelectMany(recipe => recipe.Before.Concat([recipe.Effect]).Concat(recipe.Race is { } race ? [race] : []).Concat(recipe.After));
        var versions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var call in calls.Where(value => value.Kind == ProtectionQualificationNativeCallKind.Sign))
        {
            if (call.Payload["CapabilityKeyVersion"] is not System.Text.Json.Nodes.JsonValue value || !value.TryGetValue<string>(out string? version) || string.IsNullOrWhiteSpace(version))
            { throw new InvalidOperationException("Concrete independently installed signing key versions required before enrollment."); }
            versions.Add(version);
        }
        return versions;
    }
    internal static async Task<bool> VerifyRequestsAsync(IReadOnlyList<DeletionCapabilitySigningReadinessRequest> requests, IDeletionCapabilitySigningActor signer, CancellationToken token)
    {
        if (requests.Count == 0) { return false; }
        foreach (var request in requests)
        {
            var proof = await signer.ReadAvailabilityAsync(request).WaitAsync(token).ConfigureAwait(false);
            string[] operations = ["SignDeletionCapability", "LookupDeletionCapability", "ObsoleteUnissued", "RecoverSigningSuccessor"];
            var now = DateTimeOffset.UtcNow;
            if (proof is null || proof.Request != request || string.IsNullOrWhiteSpace(proof.ProviderIdentity) || string.IsNullOrWhiteSpace(proof.AuthorityRevision)
                || proof.Operations is null || proof.Operations.Length != operations.Length || !proof.Operations.Order(StringComparer.Ordinal).SequenceEqual(operations.Order(StringComparer.Ordinal))
                || proof.ObservedAt > now || proof.ValidUntil <= now || proof.ValidUntil - proof.ObservedAt > TimeSpan.FromSeconds(30)) { return false; }
        }
        return true;
    }
}
