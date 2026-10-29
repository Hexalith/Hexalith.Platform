using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationRetainedDigestOriginalReaderTests
{
    [Theory]
    [InlineData("revoked")][InlineData("changed-material")]
    public async Task ActualIndependentReaderRecomputesRecordedVersionAndRefusesCurrentRevocationOrMaterialSubstitutionWhileExecutorLookupStaysHistorical(string change)
    {
        using var authority=new IndependentAuthorityReceiverTests.Fixture();
        authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
        var originals=new InstalledQualificationOriginalStore(authority.Client());var token=TestContext.Current.CancellationToken;
        DateTimeOffset now=DateTimeOffset.UtcNow;var profiles=Substitute.For<IPlatformSigningProfileProvider>();
        profiles.GetCurrent().Returns(new PlatformSigningProfile("fixture-v1","issuer","audience",now.AddDays(-1),now.AddDays(1),TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromDays(1),TimeSpan.FromDays(7)));
        var keys=Substitute.For<IPlatformHmacKeyProvider>();int reads=0;bool changed=false;
        keys.ResolveAsync(Arg.Any<PlatformHmacScope>(),"recorded-v1",Arg.Any<CancellationToken>()).Returns(call=>
        {
            reads++;
            return ValueTask.FromResult(changed && change=="revoked"?new PlatformHmacKeyResolution(CustodyStatus.RevokedKey):new PlatformHmacKeyResolution(CustodyStatus.Succeeded,
                new(call.ArgAt<PlatformHmacScope>(0),new("recorded-v1",PlatformHmacKeyState.Retained,now.AddDays(-1),now.AddDays(1),now.AddHours(-1)),Enumerable.Repeat(changed?(byte)43:(byte)42,32).ToArray())));
        });
        var scope=new JsonObject { ["tenantId"]="tenant",["logicalId"]="fixture-logical",["targetIds"]=new JsonArray("fixture-target"),["providerCopyIds"]=new JsonArray("fixture-copy") };
        var request=new JsonObject { ["caseId"]="S2.retained-digest",["requestId"]=new string('a',64),["enrolledScope"]=scope };
        var input=JsonSerializer.SerializeToElement(new { enrolledScope=scope,digestScope=new PlatformHmacScope("tenant",PlatformHmacPurpose.ContentDigest),components=new[]{"fixture-content"},recordedVersion="recorded-v1" },CustodyBindingContract.Json);
        var source=new QualificationRuntimeSourceDriver(Substitute.For<IPlatformKeyInventoryActor>(),Substitute.For<IPlatformKeyInventoryAuthority>(),new PlatformHmacService(keys,profiles,TimeProvider.System),null!,new Dictionary<string,JsonElement>{{"S2.retained-digest",input}});
        var workflow=QualificationRuntimeWorkflowCatalog.Create(originals,["S2.retained-digest"],source).Single();
        var historical=(await workflow.Execute(request,token)).ShouldNotBeNull();int executionReads=reads;executionReads.ShouldBeGreaterThan(0);
        var audit=Substitute.For<IQualificationNativeCaseAuditReader>();audit.ReadAsync(request,token).Returns(new QualificationNativeCaseAudit(7,[],[],[],[],new JsonObject()));
        var reader=new QualificationRetainedDigestOriginalReader(originals,audit,source);
        var observed=(await reader.ReadAsync(request,token)).ShouldNotBeNull();observed.Source["state"]!["outcome"]!.GetValue<string>().ShouldBe("Verified");
        reads.ShouldBeGreaterThan(executionReads);int verifiedReads=reads;
        JsonNode.DeepEquals(observed.Source["retainedDigestOriginal"],observed.Source["retainedDigestCurrent"]).ShouldBeTrue();
        changed=true;authority.Restart();
        (await reader.ReadAsync(request,token)).ShouldBeNull();reads.ShouldBeGreaterThan(verifiedReads);int refusedReads=reads;
        JsonNode.DeepEquals(await workflow.Lookup(request,token),historical).ShouldBeTrue();reads.ShouldBe(refusedReads);
        JsonNode.DeepEquals(await originals.LookupResultAsync("executor",new string('a',64)+":case-result",token),historical).ShouldBeTrue();
    }
}
