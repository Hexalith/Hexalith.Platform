using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Tests;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class IndependentResultCommitTests
{
    [Fact]
    public async Task FiniteAdmittedPoolSurvivesRepeatedResultCasLossIndexAdvancesAndReplicaMaterialization()
    {
        using var pool=new PoolFixture();using var receiver=pool.Receiver();var token=TestContext.Current.CancellationToken;
        var store=new InstalledQualificationOriginalStore(receiver.Client());var request=new JsonObject { ["original"]="exact-admitted" };var result=new JsonObject { ["result"]="exact-historical" };
        (await store.BeginAsync("executor","admitted",request,token)).ShouldBeTrue();
        string originalRoot=receiver.State.PrivateOriginalsRoot!;long admittedRevision=(await store.ReadOriginalAsync("executor","admitted",token)).RetainedRevision;
        string? root=originalRoot;var roots=new List<string>();
        var index=new IndependentPrivateOriginalIndex(new IndependentFileContentArchive(receiver.PrivateArchiveDirectory));
        for(int i=0;i<4;i++) { root=(await index.SetAsync(root,"observer","unrelated-"+i,new(JsonSerializer.SerializeToElement(new { other=i }),null,FinalAtAdmission:true),token)).Root;roots.Add(root); }
        string ticket=pool.Ticket("executor","admitted");long initial=pool.Ledger.Tickets[ticket].InitialBytes!.Value;long? remaining=null;
        int losses=0;
        receiver.HeadSaveOverride=(next,etag)=>
        {
            if(next.PrivateResultCommit is not null && receiver.State.PrivateResultCommit is null && losses<roots.Count)
            {
                // An unrelated real persistent-index head wins before this prospective result commit.
                var other=receiver.State with { Revision=receiver.State.Revision+1,PrivateOriginalsRoot=roots[losses],PrivateOriginalsCount=2+losses };
                losses++;receiver.CompleteDelayedHead(other,etag).ShouldBeTrue();return false;
            }
            return null;
        };
        for(int i=0;i<4;i++)
        {
            await Should.ThrowAsync<InvalidOperationException>(()=>store.RetainAsync("executor","admitted",result,token));
            var charged=pool.Ledger.Tickets[ticket];charged.InitialBytes.ShouldBe(initial);
            if(remaining is { } prior) charged.Remaining.ShouldBe(prior);else remaining=charged.Remaining;
            receiver.State.PrivateResultCommit.ShouldBeNull();receiver.Restart();
        }
        losses.ShouldBe(4);int materializationLosses=0;remaining=null;long committedRevision=0;string? frozenRoot=null;
        receiver.HeadSaveOverride=(next,etag)=>
        {
            if(receiver.State.PrivateResultCommit is { } commit && next.PrivateResultCommit is null && materializationLosses++<4)
            {
                committedRevision=commit.Revision;frozenRoot=commit.IndexRoot;
                receiver.CompleteDelayedHead(receiver.State with { Revision=receiver.State.Revision+7 },etag).ShouldBeTrue();return false;
            }
            return null;
        };
        for(int i=0;i<4;i++)
        {
            await Should.ThrowAsync<InvalidOperationException>(()=>store.RetainAsync("executor","admitted",result,token));
            receiver.State.PrivateResultCommit.ShouldNotBeNull();receiver.State.PrivateResultCommit.Revision.ShouldBe(committedRevision);
            receiver.State.PrivateOriginalsRoot.ShouldBe(frozenRoot);
            var charged=pool.Ledger.Tickets[ticket];charged.InitialBytes.ShouldBe(initial);
            if(remaining is { } prior) charged.Remaining.ShouldBe(prior);else remaining=charged.Remaining;
            receiver.Restart();
        }
        receiver.HeadSaveOverride=null;receiver.Restart();
        // A fresh receiving replica can finish and recover an already admitted result using lookup only.
        var replicas=Enumerable.Range(0,3).Select(_=>new InstalledQualificationOriginalStore(receiver.ReplicaClient())).ToArray();
        var lookups=await Task.WhenAll(replicas.Select(replica=>replica.LookupResultAsync("executor","admitted",token)));
        lookups.Where(value=>value is not null).ShouldNotBeEmpty();
        foreach(var recovered in lookups.Where(value=>value is not null)) JsonNode.DeepEquals(recovered,result).ShouldBeTrue();
        JsonNode.DeepEquals(await store.LookupResultAsync("executor","admitted",token),result).ShouldBeTrue();
        JsonNode.DeepEquals(await store.LookupRequestAsync("executor","admitted",token),request).ShouldBeTrue();
        var original=await new IndependentPrivateOriginalIndex(new IndependentFileContentArchive(receiver.PrivateArchiveDirectory)).ReadAsync(receiver.State.PrivateOriginalsRoot,"executor","admitted",token);
        original!.Original.RetainedRevision.ShouldBe(admittedRevision);original.Original.ResultRetainedRevision.ShouldBe(committedRevision);
        receiver.State.PrivateResultCommit.ShouldBeNull();receiver.State.PendingOriginalCompletionBytes.ShouldBe(0);pool.Ledger.Tickets.ContainsKey(ticket).ShouldBeFalse();
        await Should.ThrowAsync<InvalidOperationException>(()=>store.RetainAsync("executor","admitted",new JsonObject { ["result"]="substituted" },token));
        JsonNode.DeepEquals(await store.LookupResultAsync("executor","admitted",token),result).ShouldBeTrue();
    }

    [Theory]
    [InlineData("body-transferred")][InlineData("body-published")][InlineData("intent-ack")]
    [InlineData("metadata-transferred")][InlineData("metadata-published")][InlineData("materialization-ack")]
    [InlineData("before-settle")][InlineData("settled-zero")]
    public async Task EveryDurableResultBoundaryRecoversExactOriginalWithoutNewAdmissionOrCompletionFunds(string boundary)
    {
        using var pool=new PoolFixture(bytes:400L*1024*1024);using var receiver=pool.Receiver();var token=TestContext.Current.CancellationToken;
        var store=new InstalledQualificationOriginalStore(receiver.Client());var request=new JsonObject { ["identity"]=boundary };var result=new JsonObject { ["exact"]=boundary };
        (await store.BeginAsync("executor","boundary",request,token)).ShouldBeTrue();string ticket=pool.Ticket("executor","boundary");long initial=pool.Ledger.Tickets[ticket].InitialBytes!.Value;bool lost=false;
        pool.Fault=point=>
        {
            bool metadata=receiver.State.PrivateResultCommit is not null;
            if(!lost && (boundary==(metadata?"metadata-":"body-")+point || boundary==point)) { lost=true;throw new IOException("fixture lost durable result acknowledgement: "+boundary); }
        };
        receiver.HeadSaveOverride=(next,etag)=>
        {
            bool commit=next.PrivateResultCommit is not null && receiver.State.PrivateResultCommit is null;
            bool materialize=next.PrivateResultCommit is null && receiver.State.PrivateResultCommit is not null;
            if(!lost && (boundary=="intent-ack" && commit || boundary=="materialization-ack" && materialize))
            { lost=true;receiver.CompleteDelayedHead(next,etag).ShouldBeTrue();throw new HttpRequestException("fixture lost committed head acknowledgement"); }
            return null;
        };
        await Should.ThrowAsync<InvalidOperationException>(()=>store.RetainAsync("executor","boundary",result,token));lost.ShouldBeTrue();
        if(pool.Ledger.Tickets.TryGetValue(ticket,out var retained)) retained.InitialBytes.ShouldBe(initial);
        pool.Fault=null;receiver.HeadSaveOverride=null;receiver.Restart();
        HeldArchiveDirectory.AllocationAttempt=_=>throw new IOException("fixture forbids post-admission inode allocation");
        try
        {
            bool lookupOnly=boundary is "materialization-ack" or "before-settle";
            if(!lookupOnly) await store.RetainAsync("executor","boundary",result,token);
            receiver.Restart();
            // Final-head acknowledgment/cleanup loss must recover through an authorized read alone.
            JsonNode.DeepEquals(await store.LookupResultAsync("executor","boundary",token),result).ShouldBeTrue();
            pool.Ledger.Tickets.ContainsKey(ticket).ShouldBeFalse();
            JsonNode.DeepEquals(await store.LookupRequestAsync("executor","boundary",token),request).ShouldBeTrue();
            JsonNode.DeepEquals(await store.LookupResultAsync("executor","boundary",token),result).ShouldBeTrue();
            if(!lookupOnly) await store.RetainAsync("executor","boundary",result,token);
            receiver.State.PendingOriginalCompletionBytes.ShouldBe(0);
            pool.Ledger.Tickets.ContainsKey(ticket).ShouldBeFalse();
        }
        finally { HeldArchiveDirectory.AllocationAttempt=null; }
    }

    [Fact]
    public async Task ResultCommitAdmissionAndSignedRecoveryRequireCurrentExactCallerWhileDurableFactSurvivesRevocation()
    {
        using var receiver=new IndependentAuthorityReceiverTests.Fixture();Permissions(receiver);var token=TestContext.Current.CancellationToken;var store=new InstalledQualificationOriginalStore(receiver.Client());
        await store.BeginAsync("executor","revoked",new JsonObject { ["request"]="exact" },token);
        receiver.HeadSaveOverride=(next,etag)=>
        {
            if(next.PrivateResultCommit is not null) { receiver.RevokeCaller();return false; }return null;
        };
        await Should.ThrowAsync<InvalidOperationException>(()=>store.RetainAsync("executor","revoked",new JsonObject { ["result"]="exact" },token));
        receiver.State.PrivateResultCommit.ShouldBeNull();
        (await store.LookupResultAsync("executor","revoked",token)).ShouldBeNull();
    }

    [Fact]
    public async Task CommittedResultSurvivesRevocationButOnlyACurrentAuthorizedReaderCanRecoverItsOriginal()
    {
        using var receiver=new IndependentAuthorityReceiverTests.Fixture();Permissions(receiver);var token=TestContext.Current.CancellationToken;
        var store=new InstalledQualificationOriginalStore(receiver.Client());var result=new JsonObject { ["result"]="committed-before-revocation" };
        await store.BeginAsync("executor","committed",new JsonObject { ["request"]="exact" },token);
        receiver.HeadSaveOverride=(next,etag)=>
        {
            if(next.PrivateResultCommit is not null)
            {
                receiver.CompleteDelayedHead(next,etag).ShouldBeTrue();receiver.RevokeCaller();
                throw new HttpRequestException("fixture lost successful commit before revocation");
            }
            return null;
        };
        await Should.ThrowAsync<InvalidOperationException>(()=>store.RetainAsync("executor","committed",result,token));
        long committedRevision=receiver.State.PrivateResultCommit!.Revision;receiver.HeadSaveOverride=null;receiver.Restart();
        (await store.LookupResultAsync("executor","committed",token)).ShouldBeNull();receiver.State.PrivateResultCommit.ShouldNotBeNull();
        // A separately current enrolled helper may finish this already committed fact. It cannot admit new results.
        receiver.Bearer="fixture-current-reader";
        receiver.State=receiver.State with { Callers=receiver.State.Callers.Append(receiver.State.Callers[0] with {
            Identity="fixture-reader",BearerSha256=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(receiver.Bearer))),
            Revoked=false,Methods=["qualification-lookup-original"] }).ToArray() };
        JsonNode.DeepEquals(await store.LookupResultAsync("executor","committed",token),result).ShouldBeTrue();
        receiver.State.PrivateResultCommit.ShouldBeNull();
        var retained=await receiver.Client().InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="executor",requestId="committed" },token);
        retained!.ResultRetainedRevision.ShouldBe(committedRevision);
        await Should.ThrowAsync<InvalidOperationException>(()=>store.RetainAsync("executor","committed",new JsonObject { ["result"]="substitution" },token));
    }

    [Fact]
    public async Task CompletedLegacyOriginalRemainsExactlyReadableWithoutANewPhysicalPoolInstallation()
    {
        using var receiver=new IndependentAuthorityReceiverTests.Fixture(archiveStorage:new IndependentArchiveStorage());Permissions(receiver);
        var token=TestContext.Current.CancellationToken;var request=JsonSerializer.SerializeToElement(new { identity="legacy-completed" });
        var result=JsonSerializer.SerializeToElement(new { outcome="historical",observedAt="2025-10-27T00:00:00Z" });
        var original=new IndependentOriginal(request,result,RetainedRevision:3,ResultRetainedRevision:7);
        receiver.State=receiver.State with { Revision=9 };receiver.State.Originals.Add("executor:legacy-completed",original);
        var store=new InstalledQualificationOriginalStore(receiver.Client());
        var recovered=await store.LookupResultAsync("executor","legacy-completed",token);recovered.ShouldNotBeNull();recovered.ToJsonString().ShouldBe(result.GetRawText());
        receiver.Restart();
        var recoveredRequest=await store.LookupRequestAsync("executor","legacy-completed",token);recoveredRequest.ShouldNotBeNull();recoveredRequest.ToJsonString().ShouldBe(request.GetRawText());
        JsonSerializer.SerializeToUtf8Bytes(receiver.State.Originals["executor:legacy-completed"],CustodyBindingContract.Json).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(original,CustodyBindingContract.Json));receiver.State.PrivateOriginalsRoot.ShouldBeNull();
        receiver.State.PendingOriginalCompletionBytes.ShouldBeNull();receiver.State.OriginalPublications.ShouldBeNull();
        File.Exists(Path.Combine(receiver.ArchiveDirectory,"capacity.json")).ShouldBeFalse();
        await Should.ThrowAsync<InvalidOperationException>(()=>store.BeginAsync("executor","new",new JsonObject(),token));
        JsonSerializer.SerializeToUtf8Bytes(receiver.State.Originals["executor:legacy-completed"],CustodyBindingContract.Json).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(original,CustodyBindingContract.Json));
    }

    private static void Permissions(IndependentAuthorityReceiverTests.Fixture receiver)=>receiver.State=receiver.State with { Callers=receiver.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
    private sealed class PoolFixture:IDisposable
    {
        private readonly string _root=Path.Combine(Path.GetTempPath(),"result-commit-pool-"+Guid.NewGuid().ToString("N"));
        private readonly IndependentArchiveCapacityInstallation _config;
        private readonly int _chunk;private readonly long _bytes;private ProtectedArchiveAllocationInstallation? _allocation;private string? _owner;
        internal Action<string>? Fault;
        internal PoolFixture(int chunkBytes=65536,long bytes=512L*1024*1024)
        {
            _chunk=chunkBytes;_bytes=bytes;Directory.CreateDirectory(_root);byte[] enrolled="fixture-existing-result-pool"u8.ToArray();File.WriteAllBytes(Path.Combine(_root,"enrollment.json"),enrolled);
            string hash=Convert.ToHexString(SHA256.HashData(enrolled));File.WriteAllBytes(Path.Combine(_root,"capacity.json"),JsonSerializer.SerializeToUtf8Bytes(new IndependentArchiveCapacity.Ledger { EnrollmentSha256=hash },CustodyBindingContract.Json));
            _config=new("LinuxSharedPhysicalArchiveCapacityV1",_root,hash,IndependentArchiveCapacity.MetadataBytes+bytes,VolumeFenceIdentity:ArchiveVolumeFence.ReadIdentityAsync(_root,TestContext.Current.CancellationToken).GetAwaiter().GetResult());
        }
        internal IndependentAuthorityReceiverTests.Fixture Receiver()
        {
            var receiver=new IndependentAuthorityReceiverTests.Fixture(archiveStorage:new IndependentArchiveStorage { ProbeOverride=(_,_)=>Task.CompletedTask },capacity:_config,capacityFactory:(config,archive,owner)=>
            {
                _owner=owner;_allocation??=ProtectedArchiveFixture.Enroll(_root,[_root,archive],_bytes,chunkBytes:_chunk);
                return new IndependentArchiveCapacity(config with { ProtectedAllocations=_allocation },archive,owner) { AvailableBytesOverride=()=>long.MaxValue,Fault=point=>Fault?.Invoke(point) };
            });Permissions(receiver);return receiver;
        }
        internal string Ticket(string role,string id)=>new IndependentArchiveCapacity(_config,_root,_owner!).Ticket(role,id);
        internal IndependentArchiveCapacity.Ledger Ledger=>JsonSerializer.Deserialize<IndependentArchiveCapacity.Ledger>(ProtectedArchiveFixture.Ledger(_root,_allocation),CustodyBindingContract.Json)!;
        public void Dispose()=>Directory.Delete(_root,true);
    }
}
