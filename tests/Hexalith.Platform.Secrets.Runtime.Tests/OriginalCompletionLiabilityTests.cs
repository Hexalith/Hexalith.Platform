using Hexalith.EventStore.Contracts.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class OriginalCompletionLiabilityTests
{
    [Fact]
    public async Task FailedAndCompetingHeadPublicationsRetainBudgetAndDeduplicateLargeBodiesBeforeCompletion()
    {
        const long capacity=1100L*1024*1024;var archive=new PressureArchive();var storage=Substitute.For<IIndependentArchiveStorage>();storage.AvailableBytes(Arg.Any<string>()).Returns(_=>capacity-archive.Bytes);
        using var fixture=new IndependentAuthorityReceiverTests.Fixture(storage,archive);Permissions(fixture);var store=new InstalledQualificationOriginalStore(fixture.Client());
        int failures=0;fixture.RejectHeadPublication=next=>next.PrivateOriginalsRoot!=fixture.State.PrivateOriginalsRoot && failures++<3;
        var input=new JsonObject { ["largeOriginal"]=new string('q',2*1024*1024) };
        for(int index=0;index<3;index++) await Should.ThrowAsync<InvalidOperationException>(()=>store.BeginAsync("executor","retained",input,TestContext.Current.CancellationToken));
        fixture.State.OriginalPublications!.Count.ShouldBe(1);fixture.State.PendingOriginalCompletionBytes.ShouldBe(4L*CustodyBindingContract.MaximumOriginalBytes+1024*1024);
        // Conservatively migrating an enrolled legacy head must retain the durable not-yet-indexed publication's completion liability as well.
        fixture.State=fixture.State with { PendingOriginalCompletionBytes=null };
        fixture.Restart();(await store.BeginAsync("executor","retained",input,TestContext.Current.CancellationToken)).ShouldBeTrue();archive.LargeBodies.ShouldBe(1);
        fixture.State.PendingOriginalCompletionBytes.ShouldBe(4L*CustodyBindingContract.MaximumOriginalBytes+1024*1024);
        fixture.RejectHeadPublication=null;
        var competing=await Task.WhenAll(Enumerable.Range(0,8).Select(async index=>
        { try { return await store.BeginAsync("executor","competing-"+index,new JsonObject { ["original"]=index },TestContext.Current.CancellationToken); }catch(InvalidOperationException) { return false; } }));
        competing.Count(value=>value).ShouldBeLessThan(8);
        long reserved=fixture.State.PendingOriginalCompletionBytes!.Value+fixture.State.OriginalPublications!.Values.Sum(value=>value.ReservedBytes);
        (reserved+archive.Bytes).ShouldBeLessThan(capacity);
        failures=0;fixture.RejectHeadPublication=next=>next.PrivateOriginalsRoot!=fixture.State.PrivateOriginalsRoot && failures++<3;
        var result=new JsonObject { ["largeResult"]=new string('r',3*1024*1024) };
        for(int index=0;index<3;index++) await Should.ThrowAsync<InvalidOperationException>(()=>store.RetainAsync("executor","retained",result,TestContext.Current.CancellationToken));
        archive.LargeBodies.ShouldBe(2);fixture.Restart();await store.RetainAsync("executor","retained",result,TestContext.Current.CancellationToken);
        archive.LargeBodies.ShouldBe(2);(await store.LookupResultAsync("executor","retained",TestContext.Current.CancellationToken))!.ToJsonString().ShouldBe(result.ToJsonString());
    }
    [Fact]
    public async Task ManySmallAdmissionsReserveAllLargeCompletionsAcrossCapacityLossRestartAndLostResultAcknowledgment()
    {
        long available=1100L*1024*1024;var storage=Substitute.For<IIndependentArchiveStorage>();storage.AvailableBytes(Arg.Any<string>()).Returns(_=>available);
        using var fixture=new IndependentAuthorityReceiverTests.Fixture(storage);Permissions(fixture);var originals=new InstalledQualificationOriginalStore(fixture.Client());
        var request=new JsonObject { ["scope"]="exact" };int admitted=0;
        for(;admitted<20;admitted++) { try { await originals.BeginAsync("executor","case-"+admitted,request,TestContext.Current.CancellationToken); }catch(InvalidOperationException) { break; } }
        admitted.ShouldBeInRange(2,5);long pending=fixture.State.PendingOriginalCompletionBytes.ShouldNotBeNull();pending.ShouldBe(admitted*(4L*CustodyBindingContract.MaximumOriginalBytes+1024*1024));
        available=1;fixture.Restart();await Should.ThrowAsync<InvalidOperationException>(()=>originals.BeginAsync("executor","new-after-loss",request,TestContext.Current.CancellationToken));fixture.State.PendingOriginalCompletionBytes.ShouldBe(pending);
        // Capacity can be restored by its independent owner; no immutable history is deleted. Each admitted result still has its full reserved body/index/history budget.
        available=1100L*1024*1024;var result=new JsonObject { ["largeResult"]=new string('x',2*1024*1024) };
        for(int index=0;index<admitted;index++) { await originals.RetainAsync("executor","case-"+index,result,TestContext.Current.CancellationToken);fixture.Restart();await originals.RetainAsync("executor","case-"+index,result,TestContext.Current.CancellationToken); }
        fixture.State.PendingOriginalCompletionBytes.ShouldBe(0);fixture.State.PrivateOriginalsCount.ShouldBe(admitted);
        (await originals.BeginAsync("executor","new-after-completion",request,TestContext.Current.CancellationToken)).ShouldBeTrue();
    }
    [Fact]
    public async Task LegacyIndexLiabilitiesMigrateInBoundedDurablePagesWithoutRepeatingHistoryOrMutatingOriginals()
    {
        var archive=new MemoryArchive();var storage=Substitute.For<IIndependentArchiveStorage>();storage.AvailableBytes(Arg.Any<string>()).Returns(long.MaxValue);
        using var fixture=new IndependentAuthorityReceiverTests.Fixture(storage,archive);Permissions(fixture);
        var index=new IndependentPrivateOriginalIndex(archive);string? root=null;
        for(int i=0;i<200;i++) root=(await index.SetAsync(root,"executor","legacy-"+i,new(JsonSerializer.SerializeToElement(new { id=i }),i%2==0?null:JsonSerializer.SerializeToElement(new { result=i })),TestContext.Current.CancellationToken)).Root;
        fixture.State=fixture.State with { PrivateOriginalsRoot=root,PrivateOriginalsCount=200,PendingOriginalCompletionBytes=null };
        long processed=0;int refusals=0;var originals=new InstalledQualificationOriginalStore(fixture.Client());
        while(fixture.State.PendingOriginalCompletionBytes is null)
        {
            try { await originals.BeginAsync("executor","new",new(),TestContext.Current.CancellationToken); }
            catch(InvalidOperationException) { refusals++; }
            if(fixture.State.OriginalLiabilityMigration is { } cursor) { cursor.Processed.ShouldBeGreaterThan(processed);cursor.Processed.ShouldBeLessThanOrEqualTo(processed+64);processed=cursor.Processed;cursor.Remaining.Count.ShouldBeLessThanOrEqualTo(64); }
            fixture.Restart();
        }
        refusals.ShouldBe(3);fixture.State.PendingOriginalCompletionBytes.ShouldBe(101*(4L*CustodyBindingContract.MaximumOriginalBytes+1024*1024));
        (await originals.LookupRequestAsync("executor","legacy-0",TestContext.Current.CancellationToken))!["id"]!.GetValue<int>().ShouldBe(0);
    }
    [Fact]
    public async Task AbsentBaseAndRecoveryCreationAreBothChargedAndOldNonNullCountersReconcileBeforeSettlement()
    {
        long capacity=500L*1024*1024;var storage=Substitute.For<IIndependentArchiveStorage>();storage.AvailableBytes(Arg.Any<string>()).Returns(_=>capacity);
        using var fixture=new IndependentAuthorityReceiverTests.Fixture(storage);Permissions(fixture);
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Role="private-security-observation" }).ToArray() };
        var originals=new InstalledQualificationOriginalStore(fixture.Client());var input=new JsonObject { ["immutableLogicalOriginal"]="aged-observation" };
        (await originals.BeginAsync("private-security-observation","recovery",input,TestContext.Current.CancellationToken)).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(()=>originals.BeginAsync("private-security-observation","base",input,TestContext.Current.CancellationToken));
        long liability=4L*CustodyBindingContract.MaximumOriginalBytes+1024*1024;fixture.State.PendingOriginalCompletionBytes.ShouldBe(liability);fixture.State.PrivateOriginalsCount.ShouldBe(1);
        capacity=900L*1024*1024;(await originals.BeginAsync("private-security-observation","base",input,TestContext.Current.CancellationToken)).ShouldBeTrue();
        fixture.State.PendingOriginalCompletionBytes.ShouldBe(2*liability);
        // Old source could write a non-null undercharged counter. Its absent version forces exact indexed reconciliation, never a subtraction from zero.
        fixture.State=fixture.State with { PendingOriginalCompletionBytes=0,OriginalLiabilityVersion=0 };fixture.Restart();
        await originals.RetainAsync("private-security-observation","base",new JsonObject { ["actualNativeReceipt"]="recorded" },TestContext.Current.CancellationToken);
        fixture.State.PendingOriginalCompletionBytes.ShouldBe(liability);fixture.State.OriginalLiabilityVersion.ShouldBe(1);
        await originals.RetainAsync("private-security-observation","recovery",new JsonObject { ["actualNativeReceipt"]="recorded" },TestContext.Current.CancellationToken);
        fixture.State.PendingOriginalCompletionBytes.ShouldBe(0);fixture.State.PrivateOriginalsCount.ShouldBe(2);
    }
    [Fact]
    public async Task ExactAbsentRecoveryGrantAllowsOnlyTheAuthenticatedUnsentBaseProgressionWithUnchangedNativePrefix()
    {
        using var fixture=new IndependentAuthorityReceiverTests.Fixture();fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Role="aged-recovery",Methods=["spool-admit-recovery","spool-verify-recovery"] }).ToArray() };
        DateTimeOffset at=DateTimeOffset.UtcNow;var record=new Hexalith.Platform.Custody.SecurityObservationRecord(new("observation","tenant","denied",new string('B',64),"v1"),at,at.ToString("yyyy-MM-dd"),1,null,2);
        string id=PrivateRecordingIdentity.SecurityOriginalMessageId(record.Intent.ObservationId);
        var proof=new Hexalith.Platform.Custody.SecurityEventRecorderLookup(Hexalith.Platform.Custody.SecurityEventRecorderLookupState.NotRecorded,NoRecordProof:new(id,id,new string('A',64),"native-incarnation",0,new string('C',64),null,"DefinitiveAbsent"));
        var current=proof;fixture.RecordingSource.LookupAsync(record,Arg.Any<CancellationToken>()).Returns(_=>current);
        var authority=new DaprReplicatedSpoolAuthority(fixture.Client());var grant=await authority.AuthorizeAgedOriginalRecoveryAsync(fixture.Target,record,proof,TestContext.Current.CancellationToken);grant.ShouldNotBeNull();
        current=proof with { NoRecordProof=proof.NoRecordProof! with { Basis="RetainedUnsent",RetainedOriginalSha256=new string('D',64) } };
        (await authority.VerifyAgedOriginalRecoveryAsync(grant,current,TestContext.Current.CancellationToken)).ShouldBeTrue();
        foreach(var changed in new[]{current.NoRecordProof! with { CurrentTransportMessageId="foreign" },current.NoRecordProof! with { NativePrefixSha256=new string('F',64) },current.NoRecordProof! with { Basis="TerminalNoEvent" }})
        { current=current with { NoRecordProof=changed };(await authority.VerifyAgedOriginalRecoveryAsync(grant,current,TestContext.Current.CancellationToken)).ShouldBeFalse(); }
    }
    [Theory]
    [InlineData(false,0)][InlineData(true,0)][InlineData(false,1)]
    public async Task ActualSpoolMigratesRestoredVersionZeroAndNullTotalsBeforeSpendingPrivateCompletionCapacity(bool missing,int version)
    {
        var archive=new MemoryArchive();var storage=Substitute.For<IIndependentArchiveStorage>();long available=250L*1024*1024;
        storage.AvailableBytes(Arg.Any<string>()).Returns(_=>available);
        using var fixture=new IndependentAuthorityReceiverTests.Fixture(storage,archive);
        var index=new IndependentPrivateOriginalIndex(archive);string? root=null;
        for(int i=0;i<130;i++) root=(await index.SetAsync(root,"executor","old-"+i,new(JsonSerializer.SerializeToElement(new { id=i }),i==0?null:JsonSerializer.SerializeToElement(new { result=i })),TestContext.Current.CancellationToken)).Root;
        fixture.State=fixture.State with { PrivateOriginalsRoot=root,PrivateOriginalsCount=130,PendingOriginalCompletionBytes=missing?null:version==1?4L*CustodyBindingContract.MaximumOriginalBytes+1024*1024:0,OriginalLiabilityVersion=version };
        byte[] bytes="spool-under-pressure"u8.ToArray();var transition=new AnchoredStateTransition(fixture.Scope,0,1,new string('A',64),Convert.ToHexString(SHA256.HashData(bytes)),bytes);
        var authority=new DaprReplicatedSpoolAuthority(fixture.Client());
        for(int i=0;i<3;i++) { (await authority.AdmitTransitionAsync(transition,TestContext.Current.CancellationToken)).ShouldBeFalse();fixture.Restart(); }
        fixture.State.OriginalLiabilityVersion.ShouldBe(1);fixture.State.PendingOriginalCompletionBytes.ShouldBe(4L*CustodyBindingContract.MaximumOriginalBytes+1024*1024);
        fixture.State.SpoolPublication.ShouldBeNull();Directory.GetFiles(fixture.ArchiveDirectory,"admitted-*").ShouldBeEmpty();
        available=500L*1024*1024;(await authority.AdmitTransitionAsync(transition,TestContext.Current.CancellationToken)).ShouldBeTrue();fixture.Restart();
        (await authority.RecoverTransitionAsync(transition,TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.RecoverTransitionAsync(transition,TestContext.Current.CancellationToken)).ShouldBeTrue();fixture.State.SpoolPublication.ShouldBeNull();
        fixture.State.PendingOriginalCompletionBytes.ShouldBe(4L*CustodyBindingContract.MaximumOriginalBytes+1024*1024);
    }
    [Fact]
    public async Task FirstLargeLegacyOriginalMigratesBeforeFollowingEntriesAcrossCasLossAndRestart()
    {
        using var fixture=new IndependentAuthorityReceiverTests.Fixture();Permissions(fixture);
        var large=JsonSerializer.SerializeToElement(new { retained=new string('x',2*1024*1024) });var normal=JsonSerializer.SerializeToElement(new { retained="exact-next-original" });
        fixture.State.Originals.Add("executor:aaa-large",new(large,null));fixture.State.Originals.Add("executor:bbb-next",new(normal,null));
        var store=new InstalledQualificationOriginalStore(fixture.Client());bool lost=false;
        fixture.RejectHeadPublication=next=>next.PrivateOriginalsRoot!=fixture.State.PrivateOriginalsRoot && !lost && (lost=true);
        await Should.ThrowAsync<InvalidOperationException>(()=>store.BeginAsync("executor","new",new JsonObject { ["input"]="valid" },TestContext.Current.CancellationToken));
        fixture.State.Originals.ContainsKey("executor:aaa-large").ShouldBeTrue();fixture.Restart();
        await store.BeginAsync("executor","new",new JsonObject { ["input"]="valid" },TestContext.Current.CancellationToken);
        fixture.State.Originals.ContainsKey("executor:aaa-large").ShouldBeFalse();fixture.State.Originals.ContainsKey("executor:bbb-next").ShouldBeTrue();
        await store.BeginAsync("executor","next-mutation",new JsonObject(),TestContext.Current.CancellationToken);
        fixture.State.Originals.ContainsKey("executor:bbb-next").ShouldBeFalse();fixture.Restart();
        (await store.LookupRequestAsync("executor","aaa-large",TestContext.Current.CancellationToken))!.ToJsonString().ShouldBe(large.GetRawText());
        (await store.LookupRequestAsync("executor","bbb-next",TestContext.Current.CancellationToken))!.ToJsonString().ShouldBe(normal.GetRawText());
    }
    private static void Permissions(IndependentAuthorityReceiverTests.Fixture fixture)=>fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
    private sealed class MemoryArchive:IIndependentContentArchive
    {
        private readonly Dictionary<string,byte[]> _values=[];
        public Task<string> PublishAsync(ReadOnlyMemory<byte> bytes,CancellationToken token) { string hash=Convert.ToHexString(SHA256.HashData(bytes.Span));_values.TryAdd(hash,bytes.ToArray());return Task.FromResult(hash); }
        public Task<byte[]> ReadAsync(string hash,CancellationToken token)=>Task.FromResult(_values[hash].ToArray());
    }
    private sealed class PressureArchive:IIndependentContentArchive
    {
        private readonly Dictionary<string,byte[]> _values=[];internal long Bytes { get { lock(_values) return _values.Values.Sum(value=>value.LongLength); } }
        internal int LargeBodies { get { lock(_values) return _values.Values.Count(value=>value.Length>=1024*1024); } }
        public Task<string> PublishAsync(ReadOnlyMemory<byte> bytes,CancellationToken token) { string hash=Convert.ToHexString(SHA256.HashData(bytes.Span));lock(_values) _values.TryAdd(hash,bytes.ToArray());return Task.FromResult(hash); }
        public Task<byte[]> ReadAsync(string hash,CancellationToken token) { lock(_values) return Task.FromResult(_values[hash].ToArray()); }
    }
}
