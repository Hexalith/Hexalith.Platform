using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class IndependentOriginalIndexTests
{
    [Theory]
    [InlineData("private-security-observation")][InlineData("executor")][InlineData("joint-phase-command")]
    public async Task MoreThanLifetimeCapCompletedOriginalsKeepOneBoundedHeadAndEveryExactArchivedBodyAndHistory(string role)
    {
        var archive=new MemoryArchive();var storage=Substitute.For<IIndependentArchiveStorage>();storage.AvailableBytes(Arg.Any<string>()).Returns(long.MaxValue);
        using var fixture=new IndependentAuthorityReceiverTests.Fixture(storage,archive);
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Role=role,Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original","recording-dispatch-admit"] }).ToArray() };
        var authority=fixture.Client();var originals=new InstalledQualificationOriginalStore(authority);
        int initialBytes=JsonSerializer.SerializeToUtf8Bytes(fixture.State).Length;
        const int count=10025;
        for(int index=0;index<count;index++)
        {
            string id=index.ToString("D26",System.Globalization.CultureInfo.InvariantCulture);
            var input=new JsonObject { ["securityObservationId"]="fixture-observation-"+index,["routingTenantId"]="tenant",["observedAt"]="2026-10-25T00:00:00Z",["utcDay"]="2026-10-25" };
            (await originals.BeginAsync(role,id,input,TestContext.Current.CancellationToken)).ShouldBeTrue();
            if(role=="private-security-observation") (await authority.InvokeAsync<Permission>("recording-dispatch-admit",new { role=role,requestId=id,original=input },TestContext.Current.CancellationToken))!.Allowed.ShouldBeTrue();
            await originals.RetainAsync(role,id,new JsonObject { ["nativeReceipt"]="fixture-receipt-"+index },TestContext.Current.CancellationToken);
        }
        fixture.State.Originals.ShouldBeEmpty();fixture.State.PrivateOriginalsCount.ShouldBe(count);fixture.State.PrivateOriginalsRoot.ShouldNotBeNull();
        JsonSerializer.SerializeToUtf8Bytes(fixture.State).Length.ShouldBeLessThan(initialBytes+256);
        fixture.Restart();authority=fixture.Client();
        foreach(int index in new[]{0,1,5000,count-1})
        {
            string id=index.ToString("D26",System.Globalization.CultureInfo.InvariantCulture);
            var actual=await authority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role=role,requestId=id },TestContext.Current.CancellationToken);
            actual.ShouldNotBeNull();actual.Request.GetProperty("securityObservationId").GetString().ShouldBe("fixture-observation-"+index);actual.SubmissionState.ShouldBe(role=="private-security-observation"?"Dispatched":"Unsent");actual.Result!.Value.GetProperty("nativeReceipt").GetString().ShouldBe("fixture-receipt-"+index);
            var indexed=await new IndependentPrivateOriginalIndex(archive).ReadAsync(fixture.State.PrivateOriginalsRoot,role,id,TestContext.Current.CancellationToken);
            indexed!.PreviousValueSha256.ShouldNotBeNull();var previous=await new IndependentPrivateOriginalIndex(archive).ReadVersionAsync(indexed.PreviousValueSha256!,TestContext.Current.CancellationToken);
            previous.Original.Result.ShouldBeNull();previous.Original.Request.GetRawText().ShouldBe(actual.Request.GetRawText());if(role=="private-security-observation") previous.PreviousValueSha256.ShouldNotBeNull();
        }
        var changed=new JsonObject { ["securityObservationId"]="substituted" };
        await Should.ThrowAsync<InvalidOperationException>(()=>originals.BeginAsync(role,0.ToString("D26",System.Globalization.CultureInfo.InvariantCulture),changed,TestContext.Current.CancellationToken));
        archive.Unavailable=true;
        (await authority.InvokeAsync<JsonElement>("qualification-lookup-original",new { role=role,requestId=0.ToString("D26",System.Globalization.CultureInfo.InvariantCulture) },TestContext.Current.CancellationToken)).ValueKind.ShouldBe(JsonValueKind.Undefined);
    }
    [Theory]
    [InlineData("executor")][InlineData("observer")][InlineData("enrollment")][InlineData("joint-phase-secret")][InlineData("joint-phase-command")]
    public async Task LegacySingleHeadQualificationOriginalMigratesWithExactRequestsResultsRevisionsAndRoleDenial(string role)
    {
        var archive=new MemoryArchive();var storage=Substitute.For<IIndependentArchiveStorage>();storage.AvailableBytes(Arg.Any<string>()).Returns(long.MaxValue);
        using var fixture=new IndependentAuthorityReceiverTests.Fixture(storage,archive);
        var legacy=new IndependentOriginal(JsonSerializer.SerializeToElement(new { immutableRequest="original",scope="exact" }),JsonSerializer.SerializeToElement(new { nativeResult="retained" }),RetainedRevision:3,ResultRetainedRevision:7);
        fixture.State=fixture.State with { Revision=9,Originals=new(){{role+":legacy",legacy}},Callers=fixture.State.Callers.Select(value=>value with { Role=role,Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
        var store=new InstalledQualificationOriginalStore(fixture.Client());
        (await store.BeginAsync(role,"new",new JsonObject { ["exact"]="new" },TestContext.Current.CancellationToken)).ShouldBeTrue();fixture.State.Originals.ShouldBeEmpty();
        fixture.Restart();var recovered=await fixture.Client().InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role,requestId="legacy" },TestContext.Current.CancellationToken);
        JsonSerializer.Serialize(recovered).ShouldBe(JsonSerializer.Serialize(legacy));
        await Should.ThrowAsync<InvalidOperationException>(()=>store.BeginAsync(role=="executor"?"joint-phase-command":"executor","foreign",new(),TestContext.Current.CancellationToken));
        (await store.LookupRequestAsync(role,"legacy",TestContext.Current.CancellationToken))!["immutableRequest"]!.GetValue<string>().ShouldBe("original");
    }
    private sealed record Permission(bool Allowed);
    private sealed class MemoryArchive:IIndependentContentArchive
    {
        private readonly Dictionary<string,byte[]> _objects=[];internal bool Unavailable;
        public Task<string> PublishAsync(ReadOnlyMemory<byte> bytes,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();if(Unavailable) throw new IOException("Isolated native archive unavailable.");string hash=Convert.ToHexString(SHA256.HashData(bytes.Span));
            if(_objects.TryGetValue(hash,out var original)) original.ShouldBe(bytes.ToArray());else _objects.Add(hash,bytes.ToArray());return Task.FromResult(hash);
        }
        public Task<byte[]> ReadAsync(string hash,CancellationToken token)
        { token.ThrowIfCancellationRequested();if(Unavailable || !_objects.TryGetValue(hash,out var bytes) || Convert.ToHexString(SHA256.HashData(bytes))!=hash) throw new IOException("Isolated exact archive unavailable.");return Task.FromResult(bytes.ToArray()); }
    }
}
