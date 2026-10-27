using Hexalith.Platform.Tests;
using System.Security.Cryptography;
using System.Text.Json;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class IndependentPhysicalCapacityTests
{
    [Fact]
    public async Task LegacyExactPrivateReadsSurviveMissingNewCapacityWhileNewAdmissionStillRefuses()
    {
        using var fixture=new IndependentAuthorityReceiverTests.Fixture(archiveStorage:new IndependentArchiveStorage());
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Methods=["qualification-lookup-original","qualification-begin-original"] }).ToArray() };
        var legacy=JsonSerializer.SerializeToElement(new { exact="legacy-preserved" });fixture.State.Originals.Add("executor:legacy",new(legacy,null));
        var originals=new InstalledQualificationOriginalStore(fixture.Client());
        (await originals.LookupRequestAsync("executor","legacy",TestContext.Current.CancellationToken))!.ToJsonString().ShouldBe(legacy.GetRawText());
        await Should.ThrowAsync<InvalidOperationException>(()=>originals.BeginAsync("executor","new",new(),TestContext.Current.CancellationToken));
        fixture.State.Originals.ContainsKey("executor:legacy").ShouldBeTrue();fixture.State.PrivateOriginalsRoot.ShouldBeNull();
    }
    [Fact]
    public void PersistentArchiveMountStopsAtUnsupportedParentWithoutTreatingIoFailureAsMountBoundary()
    {
        var identities=new Dictionary<string,string?> { ["/parent/mount/archive"]="persistent",["/parent/mount"]="persistent",["/parent"]=null };
        ArchiveVolumeFence.ResolveRoot("/parent/mount/archive",path=>identities[path],TestContext.Current.CancellationToken).ShouldBe("/parent/mount");
        identities["/parent"]="other-persistent";ArchiveVolumeFence.ResolveRoot("/parent/mount/archive",path=>identities[path],TestContext.Current.CancellationToken).ShouldBe("/parent/mount");
        Should.Throw<IOException>(()=>ArchiveVolumeFence.ResolveRoot("/parent/mount/archive",path=>path=="/parent"?throw new IOException("fixture metadata failure"):identities[path],TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task VolumeTransferFenceIsActuallySharedAcrossProcessesAndCancellationKeepsItExclusive()
    {
        string directory=Path.GetTempPath();var start=new System.Diagnostics.ProcessStartInfo("python3") { UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true };
        start.ArgumentList.Add("-c");start.ArgumentList.Add("import os,sys,fcntl; p=sys.argv[1]; dev=os.stat(p).st_dev\nwhile os.path.dirname(p)!=p and os.stat(os.path.dirname(p)).st_dev==dev:p=os.path.dirname(p)\nf=os.open(p,os.O_RDONLY|os.O_DIRECTORY);fcntl.flock(f,fcntl.LOCK_EX);print('held',flush=True);sys.stdin.readline();os.close(f)");start.ArgumentList.Add(directory.TrimEnd('/'));
        using var child=System.Diagnostics.Process.Start(start)!;
        try
        {
            (await child.StandardOutput.ReadLineAsync(TestContext.Current.CancellationToken)).ShouldBe("held");
            using var cancelled=new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Should.ThrowAsync<OperationCanceledException>(()=>ArchiveVolumeFence.AcquireAsync(directory.TrimEnd('/'),cancelled.Token));
        }
        finally { await child.StandardInput.WriteLineAsync("release");await child.WaitForExitAsync(TestContext.Current.CancellationToken); }
        using var acquired=await ArchiveVolumeFence.AcquireAsync(directory.TrimEnd('/'),TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task ActualIndependentReceiversCompeteAcrossHeadsAndKeepAdmittedCompletionAfterLostPublicationAndOrdinaryExhaustion()
    {
        string root=Path.Combine(Path.GetTempPath(),"receiving-capacity-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            byte[] enrollment="isolated-receiving-capacity"u8.ToArray();string hash=Convert.ToHexString(SHA256.HashData(enrollment));
            await File.WriteAllBytesAsync(Path.Combine(root,"enrollment.json"),enrollment,TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(root,"capacity.json"),JsonSerializer.SerializeToUtf8Bytes(new IndependentArchiveCapacity.Ledger { EnrollmentSha256=hash },CustodyBindingContract.Json),TestContext.Current.CancellationToken);
            var config=new IndependentArchiveCapacityInstallation("LinuxSharedPhysicalArchiveCapacityV1",root,hash,IndependentArchiveCapacity.MetadataBytes+500L*1024*1024,VolumeFenceIdentity:await ArchiveVolumeFence.ReadIdentityAsync(root,TestContext.Current.CancellationToken));
            long available=1024L*1024*1024*1024;bool lose=true;int probes=0;
            var storage=new IndependentArchiveStorage { ProbeOverride=(_,_)=> { probes++;return available==0?Task.FromException(new IOException("fixture ENOSPC allocating probe")):Task.CompletedTask; } };
            IndependentArchiveCapacity Pool(IndependentArchiveCapacityInstallation c,string archive,string owner)=>new(c,archive,owner) { AvailableBytesOverride=()=>available,AllocationOverride=(_,_)=>{},Fault=point=> { if(point=="transferred" && lose) { lose=false;throw new IOException("fixture publication loss"); } } };
            using var first=new IndependentAuthorityReceiverTests.Fixture(archiveStorage:storage,capacity:config,capacityFactory:Pool,stateKey:"head-one");
            using var second=new IndependentAuthorityReceiverTests.Fixture(capacity:config,capacityFactory:Pool,stateKey:"head-two");
            foreach(var fixture in new[]{first,second}) fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };
            var one=new InstalledQualificationOriginalStore(first.Client());var two=new InstalledQualificationOriginalStore(second.Client());var input=new System.Text.Json.Nodes.JsonObject { ["input"]="retained" };
            await Should.ThrowAsync<InvalidOperationException>(()=>one.BeginAsync("executor","original",input,TestContext.Current.CancellationToken));first.Restart();
            bool reject=true;first.RejectHeadPublication=next=>next.PrivateOriginalsRoot!=first.State.PrivateOriginalsRoot && reject && !(reject=false);
            await Should.ThrowAsync<InvalidOperationException>(()=>one.BeginAsync("executor","original",input,TestContext.Current.CancellationToken));first.Restart();
            await one.BeginAsync("executor","original",input,TestContext.Current.CancellationToken);
            await Should.ThrowAsync<InvalidOperationException>(()=>two.BeginAsync("executor","competing",input,TestContext.Current.CancellationToken));
            int admissionProbes=probes;available=0;first.DropNextResponseMethod="qualification-retain-result";var output=new System.Text.Json.Nodes.JsonObject { ["result"]=new string('x',2*1024*1024) };
            await Should.ThrowAsync<InvalidOperationException>(()=>one.RetainAsync("executor","original",output,TestContext.Current.CancellationToken));first.Restart();await one.RetainAsync("executor","original",output,TestContext.Current.CancellationToken);
            (await one.LookupResultAsync("executor","original",TestContext.Current.CancellationToken))!.ToJsonString().ShouldBe(output.ToJsonString());
            probes.ShouldBe(admissionProbes);
            await Should.ThrowAsync<InvalidOperationException>(()=>one.BeginAsync("executor","new-ingress",input,TestContext.Current.CancellationToken));
            available=1024L*1024*1024*1024;(await two.BeginAsync("executor","competing",input,TestContext.Current.CancellationToken)).ShouldBeTrue();
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task TwoHeadsSharePhysicalTicketsAndCompleteAfterOrdinaryExhaustionWithLostWriteAndRestart()
    {
        string root=Path.Combine(Path.GetTempPath(),"independent-physical-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string archive=Path.Combine(root,"archive"),poolPath=Path.Combine(root,"pool");Directory.CreateDirectory(archive);Directory.CreateDirectory(poolPath);
            byte[] enrolled="fixture-only-independent-capacity"u8.ToArray();await File.WriteAllBytesAsync(Path.Combine(poolPath,"enrollment.json"),enrolled,TestContext.Current.CancellationToken);
            string hash=Convert.ToHexString(SHA256.HashData(enrolled));long quota=IndependentArchiveCapacity.MetadataBytes+10*1024*1024;
            var config=new IndependentArchiveCapacityInstallation("LinuxSharedPhysicalArchiveCapacityV1",poolPath,hash,quota,VolumeFenceIdentity:await ArchiveVolumeFence.ReadIdentityAsync(poolPath,TestContext.Current.CancellationToken));
            await File.WriteAllBytesAsync(Path.Combine(poolPath,"capacity.json"),JsonSerializer.SerializeToUtf8Bytes(new IndependentArchiveCapacity.Ledger { EnrollmentSha256=hash },CustodyBindingContract.Json),TestContext.Current.CancellationToken);
            config=config with { ProtectedAllocations=ProtectedArchiveFixture.Enroll(poolPath,[archive],12*1024*1024) };
            long free=long.MaxValue;var first=new IndependentArchiveCapacity(config,archive,"head-one") { AvailableBytesOverride=()=>free };
            var second=new IndependentArchiveCapacity(config,archive,"head-two") { AvailableBytesOverride=()=>free };
            string ticket=first.Ticket("executor","retained");await first.ReserveAsync(ticket,4*1024*1024,3*1024*1024,TestContext.Current.CancellationToken);
            await first.CommitAsync(ticket,1,TestContext.Current.CancellationToken);await first.EnsureLegacyAsync(6*1024*1024,2,TestContext.Current.CancellationToken);
            await Should.ThrowAsync<IOException>(()=>second.ReserveAsync(second.Ticket("executor","other"),4*1024*1024,3*1024*1024,TestContext.Current.CancellationToken));
            free=0;string path=Path.Combine(archive,"result");byte[] body=new byte[1024*1024];RandomNumberGenerator.Fill(body);
            bool lost=false;var losing=new IndependentArchiveCapacity(config,archive,"head-one") { AvailableBytesOverride=()=>free,Fault=point=> { if(point=="transferred" && !lost) { lost=true;throw new IOException("fixture lost physical publication acknowledgement"); } } };
            await Should.ThrowAsync<IOException>(()=>losing.PublishAsync(ticket,path,body,TestContext.Current.CancellationToken));
            await new IndependentArchiveCapacity(config,archive,"head-one") { AvailableBytesOverride=()=>free }.PublishAsync(ticket,path,body,TestContext.Current.CancellationToken);
            (await BoundedRegularFile.ReadAsync(path,CustodyBindingContract.MaximumOriginalBytes,TestContext.Current.CancellationToken)).ShouldBe(body);
            await Task.WhenAll(Enumerable.Range(0,4).Select(_=>first.PublishAsync(ticket,path,body,TestContext.Current.CancellationToken)));
            bool afterWrite=true;string late=path+"-after-write";
            var latePool=new IndependentArchiveCapacity(config,archive,"head-one") { Fault=point=> { if(point=="published" && afterWrite) { afterWrite=false;throw new IOException("fixture lost final publication acknowledgement"); } } };
            await Should.ThrowAsync<IOException>(()=>latePool.PublishAsync(ticket,late,body,TestContext.Current.CancellationToken));
            await first.PublishAsync(ticket,late,body,TestContext.Current.CancellationToken);
            var interruptedSettlement=new IndependentArchiveCapacity(config,archive,"head-one") { Fault=point=> { if(point=="settled-zero") throw new IOException("fixture interrupted zero-row settlement"); } };
            await Should.ThrowAsync<IOException>(()=>interruptedSettlement.SettleAsync(ticket,TestContext.Current.CancellationToken));
            await first.EnsureLegacyAsync(3*1024*1024,3,TestContext.Current.CancellationToken);
            var recoveredLedger=JsonSerializer.Deserialize<IndependentArchiveCapacity.Ledger>(ProtectedArchiveFixture.Ledger(poolPath,config.ProtectedAllocations),CustodyBindingContract.Json)!;
            recoveredLedger.Tickets[first.Ticket("legacy","pending")].Remaining.ShouldBe(3*1024*1024);
            await first.SettleAsync(ticket,TestContext.Current.CancellationToken);free=long.MaxValue;
            await second.ReserveAsync(second.Ticket("executor","other"),4*1024*1024,3*1024*1024,TestContext.Current.CancellationToken);
            var ledger=JsonSerializer.Deserialize<IndependentArchiveCapacity.Ledger>(ProtectedArchiveFixture.Ledger(poolPath,config.ProtectedAllocations),CustodyBindingContract.Json)!;
            ledger.Tickets.Count.ShouldBe(2);ledger.RetainedBytes.ShouldBe(2L*(body.Length+65536));ledger.Transfers.ShouldBeEmpty();
            (new FileInfo(Path.Combine(poolPath,"allocation-metadata-0.bin")).Length+new FileInfo(Path.Combine(poolPath,"allocation-metadata-1.bin")).Length).ShouldBe(2L*(4*1024*1024+8192));
            File.Delete(Path.Combine(poolPath,"allocation-metadata-0.bin"));await Should.ThrowAsync<IOException>(()=>first.ReserveAsync(ticket,1,0,TestContext.Current.CancellationToken));File.Exists(Path.Combine(poolPath,"allocation-metadata-0.bin")).ShouldBeFalse();
        }
        finally { Directory.Delete(root,true); }
    }
}
