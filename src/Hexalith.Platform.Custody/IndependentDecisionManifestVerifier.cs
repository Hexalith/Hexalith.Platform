namespace Hexalith.Platform.Custody;

/// <summary>Independent current policy/role/public-anchor approval verification only; absent policy/trust denies. It supplies no approval signing/request or production identity defaults.</summary>
public sealed class IndependentDecisionManifestVerifier(TimeProvider clock, IIndependentDecisionAuthority? authority = null)
{
    /// <summary>Verifies exact nonempty approvers/evaluations/root-or-effective-predecessor/freshness/current revocation and denies recorder/custodian actor substitution.</summary>
    public async Task<bool> VerifyAsync(DecisionAuthorityManifest manifest, string detachedJws, DecisionAuthorityExpectedBasis expected, CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken);
        try
        {
            budget.Check(); var owned = DecisionAuthorityManifestCodec.Capture(manifest); ArgumentNullException.ThrowIfNull(expected);
            var basis = expected with { AffectedEvaluations = DecisionAuthorityManifestCodec.Sorted(expected.AffectedEvaluations), RequiredRoles = DecisionAuthorityManifestCodec.Sorted(expected.RequiredRoles) };
            if (authority is null || detachedJws is not { Length: > 0 and <= 16384 } || !Matches(owned, basis)) { budget.Check(); return false; }
            var profile = Capture(await budget.ReadAsync(() => authority.ResolveAsync(basis, owned.SigningKeyVersion, CancellationToken.None)).ConfigureAwait(false));
            if (profile is null || !Current(profile, owned) || !await budget.ReadAsync(() => authority.VerifyExpectedBasisAsync(basis, Capture(profile)!, CancellationToken.None)).ConfigureAwait(false)) { budget.Check(); return false; }
            if (owned.Approvers.Any(a => profile.ForbiddenApprovalActors.Contains(a.ActorId, StringComparer.Ordinal)) || !DecisionAuthorityManifestCodec.Verify(owned, profile, detachedJws)) { budget.Check(); return false; }
            foreach (var approver in owned.Approvers)
            {
                budget.Check(); if (!await budget.ReadAsync(() => authority.VerifyApproverAsync(basis, approver, owned.IssuedAt, CancellationToken.None)).ConfigureAwait(false)) { budget.Check(); return false; }
            }
            var final = Capture(await budget.ReadAsync(() => authority.ResolveAsync(basis, owned.SigningKeyVersion, CancellationToken.None)).ConfigureAwait(false));
            bool same = final is not null && final.ProfileRevision == profile.ProfileRevision && final.Issuer == profile.Issuer && final.Audience == profile.Audience
                && final.SigningKeyVersion == profile.SigningKeyVersion && final.PublicAnchorId == profile.PublicAnchorId && final.PublicAnchorVersion == profile.PublicAnchorVersion
                && final.SubjectPublicKeyInfo.AsSpan().SequenceEqual(profile.SubjectPublicKeyInfo) && final.ForbiddenApprovalActors.SequenceEqual(profile.ForbiddenApprovalActors)
                && final.MaximumManifestLifetime == profile.MaximumManifestLifetime && final.ValidFrom == profile.ValidFrom && final.ValidUntil == profile.ValidUntil && Current(final, owned);
            bool result = same && await budget.ReadAsync(() => authority.VerifyExpectedBasisAsync(basis, Capture(final)!, CancellationToken.None)).ConfigureAwait(false);
            budget.Check(); return result;
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return false; }
    }
    private bool Current(DecisionAuthorityPublishedProfile p, DecisionAuthorityManifest m)
    {
        DateTimeOffset now = clock.GetUtcNow();
        return !p.IsRevoked && p.IsIndependentlyGoverned && p.KeyPurpose == "DecisionApprovalVerificationOnly" && p.Issuer == m.Issuer && p.Audience == m.Audience
            && p.SigningKeyVersion == m.SigningKeyVersion && p.ProfileRevision > 0 && p.MaximumManifestLifetime > TimeSpan.Zero
            && p.ValidFrom.Offset == TimeSpan.Zero && p.ValidUntil.Offset == TimeSpan.Zero && p.ValidFrom <= now && now < p.ValidUntil
            && p.ValidFrom <= m.IssuedAt && m.IssuedAt <= now && now < m.ExclusiveExpiry && m.ExclusiveExpiry - m.IssuedAt <= p.MaximumManifestLifetime;
    }
    private static bool Matches(DecisionAuthorityManifest m, DecisionAuthorityExpectedBasis b) => m.DecisionId == b.DecisionId && m.ContractId == b.ContractId
        && m.ContractVersion == b.ContractVersion && m.OutcomeVersion == b.OutcomeVersion && m.EffectivePredecessorVersion == b.EffectivePredecessorVersion
        && m.RootPolicyId == b.RootPolicyId && m.AffectedEvaluations.SequenceEqual(b.AffectedEvaluations) && m.Approvers.Select(a => a.Role).SequenceEqual(b.RequiredRoles);
    private static DecisionAuthorityPublishedProfile? Capture(DecisionAuthorityPublishedProfile? p)
    {
        if (p is null || p.SubjectPublicKeyInfo is not { Length: > 0 and <= 512 } || p.ForbiddenApprovalActors is null || p.ForbiddenApprovalActors.Count is < 1 or > 100) { return null; }
        foreach (string s in new[] { p.Issuer, p.Audience, p.SigningKeyVersion, p.PublicAnchorId, p.PublicAnchorVersion, p.KeyPurpose }) { DecisionAuthorityManifestCodec.Text(s); }
        var forbidden = new List<string>(); foreach (string s in p.ForbiddenApprovalActors) { if (forbidden.Count >= 100) { return null; } DecisionAuthorityManifestCodec.Text(s); forbidden.Add(s); }
        return p with { SubjectPublicKeyInfo = p.SubjectPublicKeyInfo.ToArray(), ForbiddenApprovalActors = Array.AsReadOnly(forbidden.ToArray()) };
    }
}
