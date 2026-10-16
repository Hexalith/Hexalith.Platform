using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Verification-only decision authority. It has no signing operation and returns null for an unusable profile.</summary>
public sealed class DaprDecisionAuthorityAdapter(SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock = null) : IIndependentDecisionAuthority
{
    private readonly SecretsDaprSession _session = new(invoker, configuration.CallTimeout ?? TimeSpan.Zero, clock ?? TimeProvider.System);

    /// <inheritdoc/>
    public async Task<DecisionAuthorityPublishedProfile?> ResolveAsync(DecisionAuthorityExpectedBasis expected, string signingKeyVersion, CancellationToken cancellationToken = default)
    {
        if (expected is null || !SecretsProvisioning.IsReference(signingKeyVersion)) { return null; }
        JsonObject body = Basis(expected, signingKeyVersion);
        JsonObject? proof = (await _session.CallAsync(configuration.ActorRoleAuthorityReference, "runtime.actorRoleAuthorityReference", "query-decision-profile",
            body, cancellationToken).ConfigureAwait(false)).Proof;
        if (!SecretsProof.Authorized(proof, body, _session.Clock.GetUtcNow()) || SecretsProof.Flag(proof, "isRevoked") is not false
            || SecretsProof.Flag(proof, "isIndependentlyGoverned") is not true) { return null; }
        long? lifetimeSeconds = SecretsProof.Integer(proof, "maximumManifestLifetimeSeconds");
        long? revision = SecretsProof.Integer(proof, "profileRevision");
        byte[]? spki = SecretsProof.SubjectPublicKey(proof);
        DateTimeOffset? validFrom = SecretsProof.Instant(proof, "validFrom");
        DateTimeOffset? validUntil = SecretsProof.Instant(proof, "validUntil");
        string[]? forbidden = SecretsProof.Texts(proof, "forbiddenApprovalActors");
        if (spki is null || lifetimeSeconds is null || !SecretsProof.TryLifetime(lifetimeSeconds.Value, out TimeSpan lifetime) || revision is null || revision <= 0
            || validFrom is null || validUntil is null || forbidden is null || forbidden.Length == 0
            || !SecretsProof.SameText(proof, "signingKeyVersion", signingKeyVersion)
            || !SecretsProof.SameText(proof, "keyPurpose", "DecisionApprovalVerificationOnly")
            || !SecretsProof.SameText(proof, "issuer", configuration.Issuer)
            || !SecretsProof.SameText(proof, "audience", configuration.Audience)
            || !SecretsProvisioning.IsReference(SecretsProof.Text(proof, "publicAnchorId"))
            || !SecretsProvisioning.IsReference(SecretsProof.Text(proof, "publicAnchorVersion"))) { return null; }
        DateTimeOffset now = _session.Clock.GetUtcNow();
        if (validFrom > now || now >= validUntil) { return null; }
        return new DecisionAuthorityPublishedProfile(SecretsProof.Text(proof, "issuer")!, SecretsProof.Text(proof, "audience")!, revision.Value,
            signingKeyVersion, SecretsProof.Text(proof, "publicAnchorId")!, SecretsProof.Text(proof, "publicAnchorVersion")!, spki, false, true,
            "DecisionApprovalVerificationOnly", lifetime, validFrom.Value, validUntil.Value, forbidden);
    }

    /// <inheritdoc/>
    public Task<bool> VerifyExpectedBasisAsync(DecisionAuthorityExpectedBasis expected, DecisionAuthorityPublishedProfile profile, CancellationToken cancellationToken = default)
    {
        if (expected is null || profile is null) { return Task.FromResult(false); }
        var body = Basis(expected, profile.SigningKeyVersion);
        body["issuer"] = profile.Issuer;
        body["audience"] = profile.Audience;
        body["profileRevision"] = profile.ProfileRevision;
        body["profile"] = JsonSerializer.SerializeToNode(profile);
        return EchoAsync("query-decision-basis", body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> VerifyApproverAsync(DecisionAuthorityExpectedBasis expected, DecisionAuthorityApprover approver, DateTimeOffset issuedAt, CancellationToken cancellationToken = default)
    {
        if (expected is null || approver is null || issuedAt.Offset != TimeSpan.Zero) { return Task.FromResult(false); }
        var body = Basis(expected, string.Empty);
        body.Remove("signingKeyVersion");
        body["role"] = approver.Role;
        body["actorId"] = approver.ActorId;
        body["authoritySourceId"] = approver.AuthoritySourceId;
        body["bindingVersion"] = approver.BindingVersion;
        body["evidenceId"] = approver.EvidenceId;
        body["issuedAt"] = issuedAt.ToString("O");
        return EchoAsync("query-decision-approver", body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> VerifyAuthorityBoundaryAsync(DecisionAuthorityExpectedBasis expected, DecisionAuthorityManifest manifest, DecisionAuthorityPublishedProfile profile, CancellationToken cancellationToken = default)
    {
        if (expected is null || manifest is null || profile is null || expected.DecisionId != manifest.DecisionId) { return Task.FromResult(false); }
        var body = Basis(expected, profile.SigningKeyVersion);
        body["manifestDecisionId"] = manifest.DecisionId;
        body["issuer"] = manifest.Issuer;
        body["audience"] = manifest.Audience;
        body["profile"] = JsonSerializer.SerializeToNode(profile);
        try { body["canonicalManifest"] = Convert.ToBase64String(DecisionAuthorityManifestCodec.Canonical(manifest)); }
        catch (ArgumentException) { return Task.FromResult(false); }
        return EchoAsync("query-decision-boundary", body, cancellationToken);
    }

    private async Task<bool> EchoAsync(string method, JsonObject body, CancellationToken cancellationToken)
    {
        JsonObject? proof = (await _session.CallAsync(configuration.ActorRoleAuthorityReference, "runtime.actorRoleAuthorityReference", method, body, cancellationToken).ConfigureAwait(false)).Proof;
        if (!SecretsProof.Authorized(proof, body, _session.Clock.GetUtcNow()) || SecretsProof.Text(proof, "status") == "absent") { return false; }
        if (body.ContainsKey("decisionId") && body.ContainsKey("manifestDecisionId")
            && (!SecretsProof.SameText(proof, "decisionId", SecretsProof.Text(body, "decisionId"))
                || !SecretsProof.SameText(proof, "decisionId", SecretsProof.Text(body, "manifestDecisionId")))) { return false; }
        return true;
    }

    private static JsonObject Basis(DecisionAuthorityExpectedBasis expected, string signingKeyVersion)
    {
        var evaluations = new JsonArray();
        foreach (string evaluation in expected.AffectedEvaluations ?? []) { evaluations.Add(evaluation); }
        var roles = new JsonArray();
        foreach (string role in expected.RequiredRoles ?? []) { roles.Add(role); }
        return new JsonObject
        {
            ["decisionId"] = expected.DecisionId,
            ["contractId"] = expected.ContractId,
            ["contractVersion"] = expected.ContractVersion,
            ["outcomeVersion"] = expected.OutcomeVersion,
            ["effectivePredecessorVersion"] = expected.EffectivePredecessorVersion,
            ["rootPolicyId"] = expected.RootPolicyId,
            ["affectedEvaluations"] = evaluations,
            ["requiredRoles"] = roles,
            ["signingKeyVersion"] = signingKeyVersion,
        };
    }

}
