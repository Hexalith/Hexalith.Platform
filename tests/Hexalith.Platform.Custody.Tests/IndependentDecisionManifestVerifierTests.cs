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
        authority.VerifyAuthorityBoundaryAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<DecisionAuthorityManifest>(), Arg.Any<DecisionAuthorityPublishedProfile>(), Arg.Any<CancellationToken>()).Returns(true);
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

    /// <summary>Final independent policy suspension cannot release validity after manifest or profile exclusive expiry.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ExpiryDuringFinalPolicyAwaitDeniesAtTerminalRelease(bool profileExpiry)
    {
        var clock = new CustodyFixtureClock(); using var issuer = ECDsa.Create(ECCurve.NamedCurves.nistP256); var m = Manifest(clock); var p = Profile(clock, issuer);
        if (profileExpiry) { p = p with { ValidUntil = clock.Now.AddSeconds(10) }; } else { m = m with { ExclusiveExpiry = clock.Now.AddSeconds(10) }; }
        var authority = Authority(p); int checks = 0; var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        authority.VerifyExpectedBasisAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<DecisionAuthorityPublishedProfile>(), Arg.Any<CancellationToken>()).Returns(_ => {
            if (++checks == 2) { entered.TrySetResult(); return release.Task; } return Task.FromResult(true);
        });
        var pending = new IndependentDecisionManifestVerifier(clock, authority).VerifyAsync(m, Signed(m, p, issuer), Basis(m), TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); clock.Now = profileExpiry ? p.ValidUntil : m.ExclusiveExpiry; release.TrySetResult(true);
        (await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>A role revoked during a later profile read cannot be authorized by a static unchanged public anchor revision.</summary>
    [Fact]
    public async Task LaterRoleRevocationRequiresCoherentFinalAuthorityBoundary()
    {
        var clock = new CustodyFixtureClock(); using var issuer = ECDsa.Create(ECCurve.NamedCurves.nistP256); var m = Manifest(clock); var p = Profile(clock, issuer); var authority = Authority(p);
        bool roleRevoked = false; int profiles = 0;
        authority.ResolveAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => { if (++profiles == 2) { roleRevoked = true; } return p; });
        authority.VerifyAuthorityBoundaryAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<DecisionAuthorityManifest>(), Arg.Any<DecisionAuthorityPublishedProfile>(), Arg.Any<CancellationToken>()).Returns(_ => !roleRevoked);
        (await new IndependentDecisionManifestVerifier(clock, authority).VerifyAsync(m, Signed(m, p, issuer), Basis(m), TestContext.Current.CancellationToken)).ShouldBeFalse();
        await authority.Received(5).VerifyApproverAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<DecisionAuthorityApprover>(), m.IssuedAt, Arg.Any<CancellationToken>());
        await authority.Received(1).VerifyAuthorityBoundaryAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<DecisionAuthorityManifest>(), Arg.Any<DecisionAuthorityPublishedProfile>(), Arg.Any<CancellationToken>());
    }


    /// <summary>Manifest/basis and first/final public-profile collection capture cannot retain cancellation/deadline or continue into late approval.</summary>
    [Theory]
    [InlineData("manifest-evaluations", false, false)]
    [InlineData("manifest-evaluations", false, true)]
    [InlineData("manifest-evaluations", true, false)]
    [InlineData("manifest-evaluations", true, true)]
    [InlineData("manifest-approvers", false, false)]
    [InlineData("manifest-approvers", false, true)]
    [InlineData("manifest-approvers", true, false)]
    [InlineData("manifest-approvers", true, true)]
    [InlineData("basis-evaluations", false, false)]
    [InlineData("basis-evaluations", false, true)]
    [InlineData("basis-evaluations", true, false)]
    [InlineData("basis-evaluations", true, true)]
    [InlineData("basis-roles", false, false)]
    [InlineData("basis-roles", false, true)]
    [InlineData("basis-roles", true, false)]
    [InlineData("basis-roles", true, true)]
    [InlineData("initial-profile", false, false)]
    [InlineData("initial-profile", false, true)]
    [InlineData("initial-profile", true, false)]
    [InlineData("initial-profile", true, true)]
    [InlineData("final-profile", false, false)]
    [InlineData("final-profile", false, true)]
    [InlineData("final-profile", true, false)]
    [InlineData("final-profile", true, true)]
    public async Task SuspendedDecisionCollectionsReleaseCallerWithoutLateApproval(string vector, bool traversal, bool cancelCaller)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var (clock, advance) = PrivateOwnerDeadlineTestClock.Create(); var sourceClock = new CustodyFixtureClock { Now = clock.GetUtcNow() };
        using var issuer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var manifest = Manifest(sourceClock); var basis = Basis(manifest); var profile = Profile(sourceClock, issuer);
        string signature = Signed(manifest, profile, issuer);
        string originalManifest = System.Text.Json.JsonSerializer.Serialize(manifest);
        string originalBasis = System.Text.Json.JsonSerializer.Serialize(basis);
        string originalProfile = System.Text.Json.JsonSerializer.Serialize(profile);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Suspend() { entered.TrySetResult(); release.Wait(); returned.TrySetResult(); }
        IReadOnlyList<T> Suspended<T>(IReadOnlyList<T> original)
        {
            var supplied = Substitute.For<IReadOnlyList<T>>();
            supplied.Count.Returns(_ => { if (!traversal) { Suspend(); } return original.Count; });
            supplied.GetEnumerator().Returns(_ => { if (traversal) { Suspend(); } return original.GetEnumerator(); }); return supplied;
        }
        var input = manifest; var expected = basis;
        if (vector == "manifest-evaluations") { input = manifest with { AffectedEvaluations = Suspended(manifest.AffectedEvaluations) }; }
        if (vector == "manifest-approvers") { input = manifest with { Approvers = Suspended(manifest.Approvers) }; }
        if (vector == "basis-evaluations") { expected = basis with { AffectedEvaluations = Suspended(basis.AffectedEvaluations) }; }
        if (vector == "basis-roles") { expected = basis with { RequiredRoles = Suspended(basis.RequiredRoles) }; }
        var authority = Authority(profile); int reads = 0;
        authority.ResolveAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            int read = Interlocked.Increment(ref reads);
            return read == (vector == "initial-profile" ? 1 : vector == "final-profile" ? 2 : 0)
                ? profile with { ForbiddenApprovalActors = Suspended(profile.ForbiddenApprovalActors) } : profile;
        });
        using var caller = new CancellationTokenSource();
        var operation = Task.Run(() => new IndependentDecisionManifestVerifier(clock, authority).VerifyAsync(input, signature, expected, caller.Token), TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (cancelCaller)
            {
                caller.Cancel(); var denied = await Should.ThrowAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
                denied.CancellationToken.ShouldBe(caller.Token);
            }
            else { advance(TimeSpan.FromSeconds(30)); (await operation.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).ShouldBeFalse(); }
            int calls = authority.ReceivedCalls().Count(); release.Set();
            await returned.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            authority.ReceivedCalls().Count().ShouldBe(calls);
            System.Text.Json.JsonSerializer.Serialize(manifest).ShouldBe(originalManifest);
            System.Text.Json.JsonSerializer.Serialize(basis).ShouldBe(originalBasis);
            System.Text.Json.JsonSerializer.Serialize(profile).ShouldBe(originalProfile);
            await authority.DidNotReceive().VerifyAuthorityBoundaryAsync(Arg.Any<DecisionAuthorityExpectedBasis>(), Arg.Any<DecisionAuthorityManifest>(), Arg.Any<DecisionAuthorityPublishedProfile>(), Arg.Any<CancellationToken>());
        }
        finally { release.Set(); }
    }
}
