using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

// Real independent original receiving/store path and actual HMAC computation over isolated mutable provider/inventory fixtures only.
public sealed class QualificationRuntimeReadRecoveryTests
{
    [Theory]
    [InlineData("S1.inventory.TenantKek","outer-before-retain")]
    [InlineData("S1.inventory.TenantKek","inner-after-retain")]
    [InlineData("S1.inventory.TenantKek","inner-before-retain")]
    [InlineData("S2.retained-digest","outer-before-retain")]
    [InlineData("S2.retained-digest","inner-after-retain")]
    [InlineData("S2.retained-digest","inner-before-retain")]
    public async Task ChangedInventoryOrProviderAfterLossNeverReplacesTheExactReadOrTriggersAnotherReadOrComputation(string caseId,string loss)
    {
        using var authority=new IndependentAuthorityReceiverTests.Fixture();
        authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
        var originals=new InstalledQualificationOriginalStore(authority.Client());var lossy=new LostAcknowledgementStore(originals,loss);
        var inventory=Substitute.For<IPlatformKeyInventoryActor>();var inventoryAuthority=Substitute.For<IPlatformKeyInventoryAuthority>();
        var key=new PlatformKeyVersion("tenant",PlatformKeyPurpose.TenantKek,"fixture-kek","v1","provider-target","outside-authority",null,null);
        var current=new PlatformKeyInventorySnapshot("tenant",PlatformKeyPurpose.TenantKek,"fixture-kek",7,[new(key,PlatformHmacKeyState.Active,7)],[]);
        int inventoryReads=0,inventoryAuthorizations=0,providerReads=0;bool providerRevoked=false;
        inventory.ReadAsync(key).Returns(_=> { inventoryReads++;return current; });
        inventoryAuthority.AuthorizeOperationAsync(key,"ReadKeyInventory",Arg.Any<CancellationToken>()).Returns(_=> { inventoryAuthorizations++;return true; });
        inventoryAuthority.ValidateStateAsync(key,Arg.Any<long>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>call.ArgAt<long>(1)==current.Revision
            && call.ArgAt<string>(2)==CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(current)));
        DateTimeOffset now=DateTimeOffset.UtcNow;
        var profiles=Substitute.For<IPlatformSigningProfileProvider>();profiles.GetCurrent().Returns(new PlatformSigningProfile("fixture-v1","issuer","audience",now.AddDays(-1),now.AddDays(1),TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromDays(1),TimeSpan.FromDays(7)));
        var keys=Substitute.For<IPlatformHmacKeyProvider>();
        keys.ResolveAsync(Arg.Any<PlatformHmacScope>(),"v1",Arg.Any<CancellationToken>()).Returns(call=>
        {
            providerReads++;
            return ValueTask.FromResult(providerRevoked?new PlatformHmacKeyResolution(CustodyStatus.RevokedKey):new PlatformHmacKeyResolution(CustodyStatus.Succeeded,
                new(call.ArgAt<PlatformHmacScope>(0),new("v1",PlatformHmacKeyState.Retained,now.AddDays(-1),now.AddDays(1),now.AddHours(-1)),Enumerable.Repeat((byte)42,32).ToArray())));
        });
        var scope=new JsonObject { ["tenantId"]="tenant" };var request=new JsonObject { ["caseId"]=caseId,["requestId"]=new string('a',64),["enrolledScope"]=scope };
        var input=caseId.StartsWith("S1.inventory.",StringComparison.Ordinal)?JsonSerializer.SerializeToElement(new { enrolledScope=scope,key },CustodyBindingContract.Json):
            JsonSerializer.SerializeToElement(new { enrolledScope=scope,digestScope=new PlatformHmacScope("tenant",PlatformHmacPurpose.ContentDigest),components=new[]{"fixture-content"},recordedVersion="v1" },CustodyBindingContract.Json);
        QualificationRuntimeSourceDriver Source()=>new(inventory,inventoryAuthority,new PlatformHmacService(keys,profiles,TimeProvider.System),null!,new Dictionary<string,JsonElement>{{caseId,input}});
        QualificationSourceWorkflow Workflow()=>QualificationRuntimeWorkflowCatalog.Create(lossy,[caseId],Source()).Single();
        await Should.ThrowAsync<IOException>(()=>Workflow().Execute(request,TestContext.Current.CancellationToken));
        var exact=lossy.AttemptedInnerResult.ShouldNotBeNull();
        int originalInventoryReads=inventoryReads,originalInventoryAuthorizations=inventoryAuthorizations,originalProviderReads=providerReads;
        if(caseId.StartsWith("S1.inventory.",StringComparison.Ordinal)) { inventoryReads.ShouldBe(1);inventoryAuthorizations.ShouldBe(1);exact["inventory"]!["Revision"]!.GetValue<long>().ShouldBe(7); }
        else { providerReads.ShouldBe(4);exact["first"]!["Status"]!.GetValue<string>().ShouldBe("Succeeded");JsonNode.DeepEquals(exact["first"],exact["lookup"]).ShouldBeTrue(); }
        // Mutable native facts and provider authority change after the original read and before process recovery.
        current=current with { Revision=8,Versions=[new(key,PlatformHmacKeyState.Revoked,8)] };providerRevoked=true;authority.Restart();
        var recovered=await Workflow().Lookup(request,TestContext.Current.CancellationToken);
        if(loss=="inner-before-retain")
        {
            recovered.ShouldBeNull();
            (await originals.LookupResultAsync("executor",request["requestId"]!.GetValue<string>()+":runtime-read-result",TestContext.Current.CancellationToken)).ShouldBeNull();
        }
        else JsonNode.DeepEquals(recovered,exact).ShouldBeTrue();
        var retried=await Workflow().Execute(request,TestContext.Current.CancellationToken);
        if(loss=="inner-before-retain") retried.ShouldBeNull();else JsonNode.DeepEquals(retried,exact).ShouldBeTrue();
        inventoryReads.ShouldBe(originalInventoryReads);inventoryAuthorizations.ShouldBe(originalInventoryAuthorizations);providerReads.ShouldBe(originalProviderReads);
        var changed=(JsonObject)request.DeepClone();changed["substituted-target"]="other";
        (await new QualificationRetainedRuntimeRead(originals,Source()).LookupAsync(changed,TestContext.Current.CancellationToken)).ShouldBeNull();
        inventoryReads.ShouldBe(originalInventoryReads);providerReads.ShouldBe(originalProviderReads);
    }
    private sealed class LostAcknowledgementStore(IQualificationOriginalStore store,string loss):IQualificationOriginalStore
    {
        private bool _lost;internal JsonObject? AttemptedInnerResult;
        public Task<bool> BeginAsync(string role,string id,JsonObject original,CancellationToken token)=>store.BeginAsync(role,id,original,token);
        public Task<QualificationOriginalAdmissionState> ReadAdmissionAsync(string role,string id,JsonObject original,CancellationToken token)=>store.ReadAdmissionAsync(role,id,original,token);
        public Task<JsonObject?> LookupResultAsync(string role,string id,CancellationToken token)=>store.LookupResultAsync(role,id,token);
        public async Task RetainAsync(string role,string id,JsonObject result,CancellationToken token)
        {
            bool inner=id.EndsWith(":runtime-read-result",StringComparison.Ordinal);if(inner) AttemptedInnerResult=(JsonObject)result.DeepClone();
            bool lose=!_lost && (loss.StartsWith("inner-",StringComparison.Ordinal)?inner:id.EndsWith(":case-result",StringComparison.Ordinal));
            if(lose) { _lost=true;if(loss=="inner-after-retain") await store.RetainAsync(role,id,result,token);throw new IOException("Isolated original-result acknowledgement loss."); }
            await store.RetainAsync(role,id,result,token);
        }
    }
}
