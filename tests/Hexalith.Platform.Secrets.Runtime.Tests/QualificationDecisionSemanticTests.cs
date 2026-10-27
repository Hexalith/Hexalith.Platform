using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationDecisionSemanticTests
{
    [Theory]
    [InlineData("valid",true)][InlineData("signature",false)][InlineData("missing-signature",false)][InlineData("expired",false)][InlineData("incomplete",false)][InlineData("scope",false)][InlineData("anchor",false)][InlineData("anchor-id",false)][InlineData("null-signature",false)][InlineData("null-manifest",false)]
    public async Task IndependentRoleRequiresTheCompleteActualVerifier(string condition,bool expected)
    {
        using var fixture=new Approval();var manifest=fixture.Manifest;var basis=fixture.Basis;string signed=fixture.Sign(manifest);
        if(condition=="signature") signed=signed[..^1]+(signed[^1]=='A'?'B':'A');
        if(condition=="missing-signature") signed="";
        if(condition=="null-signature") signed=null!;
        if(condition=="anchor-id") fixture.Profile=fixture.Profile with { PublicAnchorId="other-independently-current-anchor" };
        if(condition=="expired") { manifest=manifest with { ExclusiveExpiry=fixture.Now.AddSeconds(-1),IssuedAt=fixture.Now.AddMinutes(-1) };signed=fixture.Sign(manifest); }
        if(condition=="incomplete") manifest=manifest with { Approvers=[] };
        if(condition=="null-manifest") manifest=null!;
        if(condition=="scope") basis=basis with { ContractId="different" };
        if(condition=="anchor") { using var other=ECDsa.Create(ECCurve.NamedCurves.nistP256);fixture.Profile=fixture.Profile with { SubjectPublicKeyInfo=other.ExportSubjectPublicKeyInfo() }; }
        const string id="S1.independent-role.Product";var workflow=new QualificationDecisionCaseWorkflow(fixture.Authority,fixture.Clock,new Dictionary<string,QualificationDecisionCaseInput>{{id,new("tenant",manifest,signed,basis)}});
        var result=await workflow.VerifyAsync(Request(id),TestContext.Current.CancellationToken);result.ShouldNotBeNull()["verified"]!.GetValue<bool>().ShouldBe(expected);
    }
    [Theory]
    [InlineData("evaluations",false)][InlineData("evaluations",true)][InlineData("roles",false)][InlineData("roles",true)]
    public async Task PublicVerifierKeepsOneOwnedBasisAcrossPolicyAndSemanticMatching(string field,bool firstMatchesSignedOriginal)
    {
        using var fixture=new Approval();var signedBasis=fixture.Basis;
        IReadOnlyList<string> signed=field=="evaluations"?signedBasis.AffectedEvaluations:signedBasis.RequiredRoles;
        IReadOnlyList<string> other=field=="evaluations"?["different-current-policy-evaluation"]:["Security"];
        var first=firstMatchesSignedOriginal?signed:other;var second=firstMatchesSignedOriginal?other:signed;
        var changes=new ChangingSet(first,second);
        var expected=field=="evaluations"?signedBasis with { AffectedEvaluations=changes }:signedBasis with { RequiredRoles=changes };
        fixture.Authority.VerifyExpectedBasisAsync(Arg.Any<DecisionAuthorityExpectedBasis>(),Arg.Any<DecisionAuthorityPublishedProfile>(),Arg.Any<CancellationToken>())
            .Returns(call=> { var actual=call.ArgAt<DecisionAuthorityExpectedBasis>(0);return (field=="evaluations"?actual.AffectedEvaluations:actual.RequiredRoles).SequenceEqual(first); });
        DecisionAuthorityManifestCodec.Verify(fixture.Manifest,fixture.Profile,fixture.Sign(fixture.Manifest)).ShouldBeTrue();
        (await new IndependentDecisionManifestVerifier(fixture.Clock,fixture.Authority).VerifyAsync(fixture.Manifest,fixture.Sign(fixture.Manifest),expected,TestContext.Current.CancellationToken))
            .ShouldBe(firstMatchesSignedOriginal);
        changes.Enumerations.ShouldBe(1);
    }
    [Theory]
    [InlineData("missing-approvers")][InlineData("predecessor")][InlineData("contract")][InlineData("version")][InlineData("affected-evaluations")][InlineData("actor-roles")]
    public async Task SemanticNegativesUseUnchangedCryptographicallyValidIndependentOriginals(string field)
    {
        using var f=new Approval();var basis=f.Basis;var expected=field switch {
            "missing-approvers"=>basis with { RequiredRoles=["Product","Security"] },"actor-roles"=>basis with { RequiredRoles=["Security"] },
            "predecessor"=>basis with { EffectivePredecessorVersion=2,RootPolicyId=null },"contract"=>basis with { ContractId="independent-other-contract" },
            "version"=>basis with { ContractVersion=3 },_=>basis with { AffectedEvaluations=["independent-other-evaluation"] } };
        string signed=f.Sign(f.Manifest),id="S3.decision-reject."+field;DecisionAuthorityManifestCodec.Verify(f.Manifest,f.Profile,signed).ShouldBeTrue();
        var workflow=new QualificationDecisionCaseWorkflow(f.Authority,f.Clock,new Dictionary<string,QualificationDecisionCaseInput>{{id,new("tenant",f.Manifest,signed,expected,SignedOriginalBasis:basis)}});
        (await workflow.PreflightAsync(Request(id),TestContext.Current.CancellationToken)).ShouldBeEmpty();
        var result=(await workflow.VerifyAsync(Request(id),TestContext.Current.CancellationToken)).ShouldNotBeNull();result["verified"]!.GetValue<bool>().ShouldBeFalse();result["signatureVerifiedBeforeSemanticCheck"]!.GetValue<bool>().ShouldBeTrue();
        // Authority accepts each supplied expected basis/role; removing the actual Matches guard would make this regression report true.
        var unavailable=new QualificationDecisionCaseWorkflow(f.Authority,f.Clock,new Dictionary<string,QualificationDecisionCaseInput>{{id,new("tenant",f.Manifest,signed,expected)}});
        (await unavailable.VerifyAsync(Request(id),TestContext.Current.CancellationToken)).ShouldBeNull();
    }
    private static JsonObject Request(string id)=>new() { ["caseId"]=id,["requestId"]=new string('a',64),["enrolledScope"]=new JsonObject { ["tenantId"]="tenant" } };
    private sealed class ChangingSet(IReadOnlyList<string> first,IReadOnlyList<string> second):IReadOnlyList<string>
    {
        private int _enumerations;
        internal int Enumerations=>Volatile.Read(ref _enumerations);
        public int Count=>first.Count;
        public string this[int index]=>first[index];
        public IEnumerator<string> GetEnumerator()=>(Interlocked.Increment(ref _enumerations)==1?first:second).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    private sealed class Clock(DateTimeOffset now):TimeProvider { public override DateTimeOffset GetUtcNow()=>now; }
    private sealed class Approval:IDisposable
    {
        private readonly ECDsa _key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        internal readonly DateTimeOffset Now=DateTimeOffset.UtcNow;
        internal TimeProvider Clock;internal DecisionAuthorityManifest Manifest;internal DecisionAuthorityExpectedBasis Basis;internal DecisionAuthorityPublishedProfile Profile;
        internal IIndependentDecisionAuthority Authority=Substitute.For<IIndependentDecisionAuthority>();
        internal Approval()
        {
            Clock=new Clock(Now);Manifest=new("independent-issuer","audience","decision","contract",2,1,null,"root",["evaluation"],[new("Product","approver","authority","roles","evidence")],Now.AddSeconds(-5),Now.AddMinutes(1),"v1");
            Basis=new("decision","contract",2,1,null,"root",["evaluation"],["Product"]);
            Profile=new("independent-issuer","audience",1,"v1","anchor","v1",_key.ExportSubjectPublicKeyInfo(),false,true,"DecisionApprovalVerificationOnly",TimeSpan.FromMinutes(2),Now.AddDays(-1),Now.AddDays(1),["custodian"]);
            Authority.ResolveAsync(Arg.Any<DecisionAuthorityExpectedBasis>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(_=>Profile);
            Authority.VerifyExpectedBasisAsync(Arg.Any<DecisionAuthorityExpectedBasis>(),Arg.Any<DecisionAuthorityPublishedProfile>(),Arg.Any<CancellationToken>()).Returns(true);
            Authority.VerifyApproverAsync(Arg.Any<DecisionAuthorityExpectedBasis>(),Arg.Any<DecisionAuthorityApprover>(),Arg.Any<DateTimeOffset>(),Arg.Any<CancellationToken>()).Returns(true);
            Authority.VerifyAuthorityBoundaryAsync(Arg.Any<DecisionAuthorityExpectedBasis>(),Arg.Any<DecisionAuthorityManifest>(),Arg.Any<DecisionAuthorityPublishedProfile>(),Arg.Any<CancellationToken>()).Returns(true);
        }
        internal string Sign(DecisionAuthorityManifest manifest)=>DetachedEs256JwsCore.Sign(DecisionAuthorityManifestCodec.Header(Profile),DecisionAuthorityManifestCodec.Canonical(manifest),_key);
        public void Dispose()=>_key.Dispose();
    }
}
