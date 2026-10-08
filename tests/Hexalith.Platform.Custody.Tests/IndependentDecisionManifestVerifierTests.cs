using System.Security.Cryptography;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual private candidate independent-manifest verification with synthetic separate issuer/policy/role authority; no assigned production approval identities/profile.</summary>
public sealed class IndependentDecisionManifestVerifierTests
{
    private static readonly string[] Roles = ["Architecture", "Governance", "Maintainer:EventStore", "Product", "Security"];
    private static DecisionAuthorityManifest Manifest(CustodyFixtureClock clock) => new("independent-decision-issuer", "decision-verification", "decision-1", "contract-1", 2, 3, null, "root-policy-1",
        ["readiness-1", "readiness-2"], Roles.Select(r => new DecisionAuthorityApprover(r, "stable-actor-" + r, "independent-role-source", "binding-v1", "original-decision-evidence")).ToArray(), clock.Now, clock.Now.AddMinutes(1), "independent-key-v1");
    private static DecisionAuthorityExpectedBasis Basis(DecisionAuthorityManifest m) => new(m.DecisionId, m.ContractId, m.ContractVersion, m.OutcomeVersion,
        m.EffectivePredecessorVersion, m.RootPolicyId, m.AffectedEvaluations, Roles);
    private static DecisionAuthorityPublishedProfile Profile(CustodyFixtureClock clock, ECDsa issuer) => new("independent-decision-issuer", "decision-verification", 4, "independent-key-v1",
        "independent-public-anchor", "anchor-v1", issuer.ExportSubjectPublicKeyInfo(), false, true, "DecisionApprovalVerificationOnly", TimeSpan.FromMinutes(1), clock.Now.AddDays(-1), clock.Now.AddDays(1), ["platform-custodian", "release-recorder"]);
    private static IIndependentDecisionAuthority Authority(DecisionAuthorityPublishedProfile profile)
    {
        var authority = Substitute.For<IIndependentDecisionAuthority>();
        authority.ResolveAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(profile);
        authority.VerifyExpectedBasisAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<DecisionAuthorityPublishedProfile>(), Arg.Any<CancellationToken>()).Returns(true);
        authority.VerifyApproverAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<DecisionAuthorityApprover>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(true); return authority;
    }
    private static string Signed(DecisionAuthorityManifest m, DecisionAuthorityPublishedProfile p, ECDsa issuer) => DetachedEs256JwsCore.Sign(DecisionAuthorityManifestCodec.Header(p), DecisionAuthorityManifestCodec.Canonical(m), issuer);
    /// <summary>Separate authenticated public verifier and independently resolved policy/roles validate a complete root/predecessor basis.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task IndependentCompleteRootOrPredecessorVerifies(bool predecessor)
    {
        var clock = new CustodyFixtureClock(); using var issuer = ECDsa.Create(ECCurve.NamedCurves.nistP256); var m = Manifest(clock);
        if (predecessor) { m = m with { EffectivePredecessorVersion = 1, RootPolicyId = null }; }
        var p = Profile(clock, issuer); var a = Authority(p);
        (await new IndependentDecisionManifestVerifier(clock, a).VerifyAsync(m, Signed(m, p, issuer), Basis(m), TestContext.Current.CancellationToken)).ShouldBeTrue();
        await a.Received(5).VerifyApproverAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<DecisionAuthorityApprover>(), m.IssuedAt, Arg.Any<CancellationToken>());
    }
    /// <summary>Custodian/recorder actors, their key-purpose/family authority, and self-supplied public anchors cannot mint or validate approval evidence.</summary>
    [Theory]
    [InlineData("custodian")][InlineData("recorder")][InlineData("wrong-purpose")][InlineData("not-independent")][InlineData("wrong-key")]
    public async Task PlatformCustodyAndRecorderCannotBecomeApprovalAuthority(string vector)
    {
        var clock = new CustodyFixtureClock(); using var issuer = ECDsa.Create(ECCurve.NamedCurves.nistP256); using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var m = Manifest(clock); var p = Profile(clock, issuer);
        if (vector is "custodian" or "recorder") { m = m with { Approvers = m.Approvers.Select((a, n) => n == 0 ? a with { ActorId = vector == "custodian" ? "platform-custodian" : "release-recorder" } : a).ToArray() }; }
        string jws = Signed(m, p, issuer);
        var published = vector switch { "wrong-purpose" => p with { KeyPurpose = "ManifestSigningKey" }, "not-independent" => p with { IsIndependentlyGoverned = false }, "wrong-key" => p with { SubjectPublicKeyInfo = other.ExportSubjectPublicKeyInfo() }, _ => p };
        (await new IndependentDecisionManifestVerifier(clock, Authority(published)).VerifyAsync(m, jws, Basis(m), TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
    /// <summary>Exact approved contract/evaluation/root/required-role basis and independent role evidence cannot be substituted despite a valid signature.</summary>
    [Theory]
    [InlineData("contract")][InlineData("evaluations")][InlineData("root")][InlineData("roles")][InlineData("policy")][InlineData("role-proof")]
    public async Task ExactIndependentPolicyAndNonemptyRolesAreRequired(string vector)
    {
        var clock = new CustodyFixtureClock(); using var issuer = ECDsa.Create(ECCurve.NamedCurves.nistP256); var m = Manifest(clock); var basis = Basis(m); var p = Profile(clock, issuer); var a = Authority(p);
        basis = vector switch { "contract" => basis with { ContractVersion = 9 }, "evaluations" => basis with { AffectedEvaluations = ["readiness-1"] },
            "root" => basis with { RootPolicyId = "different-policy" }, "roles" => basis with { RequiredRoles = ["Product"] }, _ => basis };
        if (vector == "policy") { a.VerifyExpectedBasisAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<DecisionAuthorityPublishedProfile>(), Arg.Any<CancellationToken>()).Returns(false); }
        if (vector == "role-proof") { a.VerifyApproverAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<DecisionAuthorityApprover>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(false); }
        (await new IndependentDecisionManifestVerifier(clock, a).VerifyAsync(m, Signed(m, p, issuer), basis, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
    /// <summary>Current revocation, exclusive expiry, future issuance and withdrawn trust never release approval validity.</summary>
    [Theory]
    [InlineData("revoked")][InlineData("expired")][InlineData("future")][InlineData("withdrawn")]
    public async Task TimeAndCurrentTrustRemainMandatory(string vector)
    {
        var clock = new CustodyFixtureClock(); using var issuer = ECDsa.Create(ECCurve.NamedCurves.nistP256); var m = Manifest(clock); var p = Profile(clock, issuer); var a = Authority(p);
        if (vector == "revoked") { a = Authority(p with { IsRevoked = true }); }
        if (vector == "expired") { clock.Now = m.ExclusiveExpiry; }
        if (vector == "future") { m = m with { IssuedAt = m.IssuedAt.AddSeconds(1), ExclusiveExpiry = m.ExclusiveExpiry.AddSeconds(1) }; }
        if (vector == "withdrawn") { int reads = 0; a.ResolveAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => ++reads == 1 ? p : p with { IsRevoked = true }); }
        (await new IndependentDecisionManifestVerifier(clock, a).VerifyAsync(m, Signed(m, p, issuer), Basis(m), TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
    /// <summary>Absent authority, empty approvals, malformed Unicode and unbounded JWS fail before requesting any approval or returning validity.</summary>
    [Fact]
    public async Task MissingMalformedOrUnboundedManifestFailsClosed()
    {
        var clock = new CustodyFixtureClock(); using var issuer = ECDsa.Create(ECCurve.NamedCurves.nistP256); var m = Manifest(clock); var p = Profile(clock, issuer); var a = Authority(p);
        (await new IndependentDecisionManifestVerifier(clock).VerifyAsync(m, Signed(m, p, issuer), Basis(m), TestContext.Current.CancellationToken)).ShouldBeFalse();
        var verifier = new IndependentDecisionManifestVerifier(clock, a);
        foreach (var bad in new[] { m with { Approvers = [] }, m with { DecisionId = "\uD800" }, m with { AffectedEvaluations = ["duplicate", "duplicate"] } })
        { (await verifier.VerifyAsync(bad, "malformed", Basis(m), TestContext.Current.CancellationToken)).ShouldBeFalse(); }
        (await verifier.VerifyAsync(m, new string('A', 100000), Basis(m), TestContext.Current.CancellationToken)).ShouldBeFalse(); a.ReceivedCalls().ShouldBeEmpty();
    }
    /// <summary>Independent Python sorted closed JSON vector binds every nonsecret original manifest field; no private signing bytes are retained.</summary>
    [Fact]
    public void ClosedCandidateCanonicalBytesMatchIndependentVector()
    {
        var m = Manifest(new CustodyFixtureClock());
        Convert.ToHexString(SHA256.HashData(DecisionAuthorityManifestCodec.Canonical(m))).ShouldBe("EF0DA8555EFC51E08D621CD397A4B091ADE7637B686168A22DB2C4934D88E16C");
    }
    /// <summary>Original caller cancellation during role verification stops before remaining approvals and cannot release late validity.</summary>
    [Fact]
    public async Task CancellationDuringIndependentRoleVerificationPreservesOriginalToken()
    {
        var clock = new CustodyFixtureClock(); using var issuer = ECDsa.Create(ECCurve.NamedCurves.nistP256); var m = Manifest(clock); var p = Profile(clock, issuer); var a = Authority(p);
        using var caller = new CancellationTokenSource(); int visited = 0;
        a.VerifyApproverAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<DecisionAuthorityApprover>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(_ => { visited++; caller.Cancel(); return true; });
        var exception = await Should.ThrowAsync<OperationCanceledException>(() => new IndependentDecisionManifestVerifier(clock, a).VerifyAsync(m, Signed(m, p, issuer), Basis(m), caller.Token));
        exception.CancellationToken.ShouldBe(caller.Token); visited.ShouldBe(1);
    }

}
