using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationCapabilityEvaluationRecoveryTests
{
    [Theory]
    [InlineData("inner-before")][InlineData("inner-after")][InlineData("outer-before")]
    public async Task LostEvaluationReadsOnlyItsExactBodyDespiteCurrentTrustChangeAndNeverRecomputesUnknown(string loss)
    {
        using var authority=new IndependentAuthorityReceiverTests.Fixture();using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
        var actual=new InstalledQualificationOriginalStore(authority.Client());var store=new LostStore(actual,loss);
        const string id="S4.capability-canonical-14-fields";var payload=new DeletionBatchCapabilityV1("issuer","audience","tenant","deletion","seal","accepted",1,"batch",new string('a',64),"guard",1,1,1,"v1");
        var profile=new DeletionCapabilityTrustProfile("issuer","audience","tenant","v1","anchor","v1");
        var signed=new DeletionCapabilitySigningOutcome(DeletionBatchCapabilityCodec.SigningRequestId(payload),payload,DeletionCapabilitySigningState.Signed,DeletionBatchCapabilityCodec.Sign(payload,profile,key),"anchor","v1");
        int signs=0,lookups=0,verifications=0;long revision=1;bool revoked=false;
        var signer=Substitute.For<IDeletionCapabilitySigningActor>();signer.SignAsync(payload).Returns(_=> { signs++;return signed; });signer.LookupAsync(payload).Returns(_=> { lookups++;return signed; });
        var trust=Substitute.For<IDeletionCapabilitySigningTrustProvider>();trust.ResolveAsync("tenant","DeletionBatchCapabilitySigningKey","v1",Arg.Any<CancellationToken>()).Returns(_=> { verifications++;return new DeletionCapabilityPublishedTrust("tenant","DeletionBatchCapabilitySigningKey","v1","issuer","audience","anchor","v1",key.ExportSubjectPublicKeyInfo(),revision,!revoked,revoked); });
        QualificationCapabilityCaseWorkflow Source()=>new(signer,trust,store,new Dictionary<string,DeletionBatchCapabilityV1>{{id,payload}});
        QualificationSourceWorkflow Workflow()=>QualificationRuntimeWorkflowCatalog.Create(store,[id],null!,capability:Source()).Single();
        var request=new JsonObject { ["caseId"]=id,["requestId"]=new string('b',64),["enrolledScope"]=new JsonObject { ["tenantId"]="tenant",["batchId"]="batch",["sealId"]="seal",["manifestDigest"]=payload.ManifestDigest } };
        await Should.ThrowAsync<IOException>(()=>Workflow().Execute(request,TestContext.Current.CancellationToken));
        var original=store.Inner.ShouldNotBeNull().ToJsonString();var counts=(signs,lookups,verifications);signs.ShouldBe(1);
        revision=2;revoked=true;authority.Restart();
        foreach(var result in new[]{await Workflow().Lookup(request,TestContext.Current.CancellationToken),await Workflow().Execute(request,TestContext.Current.CancellationToken)})
            if(loss=="inner-before") result.ShouldBeNull();else result.ShouldNotBeNull().ToJsonString().ShouldBe(original);
        (signs,lookups,verifications).ShouldBe(counts);
        var changed=(JsonObject)request.DeepClone();changed["targetCommit"]="substituted";(await Source().LookupAsync(changed,TestContext.Current.CancellationToken)).ShouldBeNull();
        (await Source().ReadVerifyAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();signs.ShouldBe(1);
        authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { Revoked=true }).ToArray() };
        (await Source().LookupAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();signs.ShouldBe(1);
    }
    private sealed class LostStore(IQualificationOriginalStore source,string loss):IQualificationOriginalStore
    {
        private bool _lost;internal JsonObject? Inner;
        public Task<bool> BeginAsync(string role,string id,JsonObject request,CancellationToken token)=>source.BeginAsync(role,id,request,token);
        public Task<QualificationOriginalAdmissionState> ReadAdmissionAsync(string role,string id,JsonObject request,CancellationToken token)=>source.ReadAdmissionAsync(role,id,request,token);
        public Task<JsonObject?> LookupRequestAsync(string role,string id,CancellationToken token)=>source.LookupRequestAsync(role,id,token);
        public Task<JsonObject?> LookupResultAsync(string role,string id,CancellationToken token)=>source.LookupResultAsync(role,id,token);
        public async Task RetainAsync(string role,string id,JsonObject result,CancellationToken token)
        {
            bool inner=id.EndsWith(":capability-evaluation-result",StringComparison.Ordinal);if(inner) Inner=(JsonObject)result.DeepClone();
            if(!_lost && (loss.StartsWith("inner",StringComparison.Ordinal)?inner:id.EndsWith(":case-result",StringComparison.Ordinal)))
            { _lost=true;if(loss=="inner-after") await source.RetainAsync(role,id,result,token);throw new IOException("isolated evaluation retention loss"); }
            await source.RetainAsync(role,id,result,token);
        }
    }
}
