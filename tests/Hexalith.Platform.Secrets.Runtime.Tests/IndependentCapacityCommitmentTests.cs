using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class IndependentCapacityCommitmentTests
{
    [Fact]
    public async Task ImmutableRequestBudgetAndMetadataCommitmentsSurvivePartialTransfersAndRejectConflictingReuse()
    {
        using var fixture=new PoolFixture();var pool=fixture.Pool("owner");string ticket=pool.Ticket("observer","same");var token=TestContext.Current.CancellationToken;
        await pool.ReserveAsync(ticket,8*1024*1024,4*1024*1024,token,new string('A',64),"observer","same");
        await pool.PublishAsync(ticket,Path.Combine(fixture.Root,"original"),"retained"u8.ToArray(),token);
        await pool.ReserveAsync(ticket,8*1024*1024,4*1024*1024,token,new string('A',64),"observer","same");
        foreach(var changed in new[]{(4L*1024*1024,0L,new string('A',64)),(8L*1024*1024,4L*1024*1024,new string('B',64)),(8L*1024*1024,0L,new string('A',64))})
            await Should.ThrowAsync<IOException>(()=>pool.ReserveAsync(ticket,changed.Item1,changed.Item2,token,changed.Item3,"observer","same"));
        await Should.ThrowAsync<IOException>(()=>fixture.Pool("other-owner").ReserveAsync(ticket,8*1024*1024,4*1024*1024,token,new string('A',64),"observer","same"));
    }

    [Fact]
    public async Task ExactCompletedButUnsettledTicketCannotReleaseUnrelatedPendingLegacyCoverage()
    {
        using var fixture=new PoolFixture();var pool=fixture.Pool("owner");var token=TestContext.Current.CancellationToken;string complete=pool.Ticket("executor","completed");
        await pool.ReserveAsync(complete,4*1024*1024,1024*1024,token);await pool.CommitAsync(complete,1,token);
        await pool.EnsureLegacyAsync(2*1024*1024,2,token,new HashSet<string>());
        fixture.Read().Tickets[pool.Ticket("legacy","pending")].Remaining.ShouldBe(2*1024*1024);
        await pool.SettleAsync(complete,token);await pool.EnsureLegacyAsync(2*1024*1024,3,token,new HashSet<string>());
        fixture.Read().Tickets[pool.Ticket("legacy","pending")].Remaining.ShouldBe(2*1024*1024);
        await pool.EnsureLegacyAsync(0,1,token,new HashSet<string>());fixture.Read().Tickets[pool.Ticket("legacy","pending")].Remaining.ShouldBe(2*1024*1024);
    }

    [Theory]
    [InlineData("slots")][InlineData("bytes")]
    public async Task FutureTransferSlotsAndSerializedGrowthRefuseNewIngressBeforeAdmittedCompletion(string boundary)
    {
        using var fixture=new PoolFixture();var pool=fixture.Pool("owner");var token=TestContext.Current.CancellationToken;
        if(boundary=="slots")
        {
            var full=fixture.Read();for(int i=0;i<4096;i++) full.Transfers.Add(i.ToString("X64"));fixture.Write(full);
        }
        else
        {
            var full=fixture.Read();full.HeadRevisions[new string('x',4*1024*1024-5000)]=1;fixture.Write(full);
        }
        await Should.ThrowAsync<IOException>(()=>pool.ReserveAsync(pool.Ticket("executor","new"),1024*1024,0,token));
        fixture.Read().Tickets.ShouldBeEmpty();
    }

    [Fact]
    public async Task ActualLosingHeadPreparationsAreFencedAndReclaimedWhileLostSuccessfulAdmissionIsPreserved()
    {
        using var fixture=new PoolFixture();var token=TestContext.Current.CancellationToken;
        using var receiver=new IndependentAuthorityReceiverTests.Fixture(archiveStorage:new IndependentArchiveStorage { ProbeOverride=(_,_)=>Task.CompletedTask },capacity:fixture.Configuration,capacityFactory:(c,a,o)=>fixture.Pool(o,a));
        receiver.State=receiver.State with { Callers=receiver.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
        receiver.RejectHeadPublication=next=>(next.OriginalPublications?.Count??0)>(receiver.State.OriginalPublications?.Count??0);
        var store=new InstalledQualificationOriginalStore(receiver.Client());
        for(int index=0;index<12;index++)
        {
            await Should.ThrowAsync<InvalidOperationException>(()=>store.BeginAsync("executor","loser-"+index,new JsonObject { ["value"]=index },token));
            fixture.Read().Tickets.ShouldBeEmpty();receiver.Restart();
        }
        receiver.RejectHeadPublication=null;
        IndependentAuthorityState? delayed=null;string? delayedEtag=null;
        receiver.HeadSaveOverride=(next,etag)=>
        {
            if((next.OriginalPublications?.Count??0)>(receiver.State.OriginalPublications?.Count??0))
            { delayed=next;delayedEtag=etag;return false; }
            return null;
        };
        await Should.ThrowAsync<InvalidOperationException>(()=>store.BeginAsync("executor","delayed-cas",new JsonObject { ["exact"]=true },token));
        delayed.ShouldNotBeNull();fixture.Read().Tickets.ShouldBeEmpty();receiver.HeadSaveOverride=null;
        receiver.CompleteDelayedHead(delayed,delayedEtag!).ShouldBeFalse();
        (await store.BeginAsync("executor","delayed-cas",new JsonObject { ["exact"]=true },token)).ShouldBeTrue();
        await store.RetainAsync("executor","delayed-cas",new JsonObject { ["retained"]=true },token);
        receiver.DropNextResponseMethod="qualification-begin-original";
        await Should.ThrowAsync<InvalidOperationException>(()=>store.BeginAsync("executor","admitted",new JsonObject { ["value"]=1 },token));
        fixture.Read().Tickets.Values.Any(value=>value.Completion>0).ShouldBeTrue();receiver.Restart();
        (await store.BeginAsync("executor","admitted",new JsonObject { ["value"]=1 },token)).ShouldBeFalse();
        await store.RetainAsync("executor","admitted",new JsonObject { ["complete"]=true },token);
        (await store.LookupResultAsync("executor","admitted",token))!["complete"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task UncertainTerminalPreparationCannotBecomeALargerNonterminalLiabilityUnderTheSameRoleAndId()
    {
        using var pool=new PoolFixture();using var receiver=new IndependentAuthorityReceiverTests.Fixture(archiveStorage:new IndependentArchiveStorage { ProbeOverride=(_,_)=>Task.CompletedTask },capacity:pool.Configuration,capacityFactory:(c,a,o)=>pool.Pool(o,a));var token=TestContext.Current.CancellationToken;
        receiver.State=receiver.State with { Callers=receiver.State.Callers.Select(value=>value with { Role="observer",Methods=["qualification-begin-original","qualification-lookup-original"] }).ToArray() };
        receiver.HeadSaveOverride=(next,etag)=>receiver.State.OriginalLiabilityVersion==1?false:null;
        string id="native-receipt-v1:interrupted";var originals=new InstalledQualificationOriginalStore(receiver.Client());var body=new JsonObject { ["exact"]="terminal" };
        await Should.ThrowAsync<InvalidOperationException>(()=>originals.BeginFinalAsync("observer",id,body,token));
        pool.Read().Tickets.Count.ShouldBe(1);byte[] original=File.ReadAllBytes(Path.Combine(pool.Root,"capacity.json"));receiver.HeadSaveOverride=null;
        await Should.ThrowAsync<InvalidOperationException>(()=>originals.BeginAsync("observer",id,body,token));
        File.ReadAllBytes(Path.Combine(pool.Root,"capacity.json")).ShouldBe(original);
        await Should.ThrowAsync<InvalidOperationException>(()=>originals.BeginFinalAsync("observer",id,new JsonObject { ["exact"]="changed" },token));
        (await originals.BeginFinalAsync("observer",id,body,token)).ShouldBeTrue();
    }

    [Fact]
    public async Task CountRefusalCreatesNoPhysicalTicketAndConcurrentExactRetriesKeepOneOriginal()
    {
        using var pool=new PoolFixture();using var receiver=new IndependentAuthorityReceiverTests.Fixture(archiveStorage:new IndependentArchiveStorage { ProbeOverride=(_,_)=>Task.CompletedTask },capacity:pool.Configuration,capacityFactory:(c,a,o)=>pool.Pool(o,a));var token=TestContext.Current.CancellationToken;
        receiver.State=receiver.State with { OriginalLiabilityVersion=1,PendingOriginalCompletionBytes=0,OriginalPublications=Enumerable.Range(0,128).ToDictionary(value=>value.ToString(),value=>new IndependentOriginalPublication("executor","prior-"+value,"result",new string('A',64),0,0)),Callers=receiver.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
        var originals=new InstalledQualificationOriginalStore(receiver.Client());
        await Should.ThrowAsync<InvalidOperationException>(()=>originals.BeginAsync("executor","count-refusal",new JsonObject(),token));pool.Read().Tickets.ShouldBeEmpty();
        receiver.State=receiver.State with { OriginalPublications=[] };
        var calls=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>
        { try { return await originals.BeginAsync("executor","concurrent",new JsonObject { ["exact"]=true },token); }catch(InvalidOperationException) { return false; } }));
        (await originals.LookupRequestAsync("executor","concurrent",token)).ShouldNotBeNull();receiver.State.PrivateOriginalsCount.ShouldBe(1);calls.Count(value=>value).ShouldBe(1);
    }

    [Theory]
    [InlineData("slots")][InlineData("growth")]
    public async Task AlreadyAdmittedCompletionUsesItsReservedMetadataAfterOrdinaryAdmissionsHitTheExactLimit(string boundary)
    {
        using var fixture=new PoolFixture();var pool=fixture.Pool("owner");var token=TestContext.Current.CancellationToken;string ticket=pool.Ticket("executor","admitted");
        await pool.ReserveAsync(ticket,8*1024*1024,4*1024*1024,token);await pool.CommitAsync(ticket,1,token);var state=fixture.Read();
        if(boundary=="slots") for(int i=0;i<4096-768;i++) state.Transfers.Add(i.ToString("X64"));
        else
        {
            int raw=JsonSerializer.SerializeToUtf8Bytes(state,CustodyBindingContract.Json).Length;int future=768*256+256;
            state.HeadRevisions[new string('x',4*1024*1024-raw-future-2048)]=1;
        }
        fixture.Write(state);
        await Should.ThrowAsync<IOException>(()=>pool.ReserveAsync(pool.Ticket("executor","new-ingress"),1024*1024,0,token));
        string path=Path.Combine(fixture.Root,"complete");await pool.PublishAsync(ticket,path,"retained completion"u8.ToArray(),token);
        File.ReadAllText(path).ShouldBe("retained completion");
    }

    private sealed class PoolFixture:IDisposable
    {
        internal string Root { get; }=Path.Combine(Path.GetTempPath(),"capacity-commitment-"+Guid.NewGuid().ToString("N"));
        internal IndependentArchiveCapacityInstallation Configuration { get; }
        internal PoolFixture()
        {
            Directory.CreateDirectory(Root);byte[] bytes="fixture-only-capacity-enrollment"u8.ToArray();File.WriteAllBytes(Path.Combine(Root,"enrollment.json"),bytes);string hash=Convert.ToHexString(SHA256.HashData(bytes));
            Configuration=new("LinuxSharedPhysicalArchiveCapacityV1",Root,hash,16L*1024*1024*1024,VolumeFenceIdentity:ArchiveVolumeFence.ReadIdentityAsync(Root,TestContext.Current.CancellationToken).GetAwaiter().GetResult());
            Write(new() { EnrollmentSha256=hash });
        }
        internal IndependentArchiveCapacity Pool(string owner,string? archive=null)=>new(Configuration,archive??Root,owner) { AvailableBytesOverride=()=>long.MaxValue,AllocationOverride=(_,_)=>{} };
        internal IndependentArchiveCapacity.Ledger Read()=>JsonSerializer.Deserialize<IndependentArchiveCapacity.Ledger>(File.ReadAllBytes(Path.Combine(Root,"capacity.json")),CustodyBindingContract.Json)!;
        internal void Write(IndependentArchiveCapacity.Ledger ledger)=>File.WriteAllBytes(Path.Combine(Root,"capacity.json"),JsonSerializer.SerializeToUtf8Bytes(ledger,CustodyBindingContract.Json));
        public void Dispose()=>Directory.Delete(Root,true);
    }
}
