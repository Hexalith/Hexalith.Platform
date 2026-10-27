using Hexalith.Platform.Tests;
using Hexalith.Platform.Custody;
using System.Security.Cryptography;
using System.Text.Json;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class IndependentRecoveryArchiveTests
{
    [Fact]
    public async Task ExpiredExactGrantRenewsWithoutChangingOriginalAndAllIdentitiesSurviveOutsideBoundedCurrentHead()
    {
        var clock=new Clock();using var fixture=new IndependentAuthorityReceiverTests.Fixture(clock:clock);
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Role="aged-recovery",Methods=["spool-admit-recovery","spool-verify-recovery"] }).ToArray() };
        var record=new SecurityObservationRecord(new("observation","tenant","invalid-tag",new string('a',64),"v1"),clock.Now,clock.Now.ToString("yyyy-MM-dd"),1,null);
        var proof=new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.NotRecorded,NoRecordProof:new(PrivateRecordingIdentity.SecurityOriginalMessageId("observation"),PrivateRecordingIdentity.SecurityOriginalMessageId("observation"),new string('a',64),"fixture-native-source",0,new string('b',64),null,"RetainedUnsent"));
        fixture.RecordingSource.LookupAsync(record,Arg.Any<CancellationToken>()).Returns(proof);
        var authority=new DaprReplicatedSpoolAuthority(fixture.Client());
        var first=await authority.AuthorizeAgedOriginalRecoveryAsync(fixture.Target,record,proof,TestContext.Current.CancellationToken);first.ShouldNotBeNull();
        var duplicate=await authority.AuthorizeAgedOriginalRecoveryAsync(fixture.Target,record,proof,TestContext.Current.CancellationToken);duplicate.ShouldBe(first);
        clock.Now=clock.Now.AddMinutes(6);
        var renewed=await authority.AuthorizeAgedOriginalRecoveryAsync(fixture.Target,record,proof,TestContext.Current.CancellationToken);renewed.ShouldNotBeNull();renewed.ShouldNotBe(first);renewed.Original.ShouldBe(first.Original);renewed.Original.ObservedAt.ShouldBe(record.ObservedAt);
        fixture.State.Recoveries.Count.ShouldBe(1);
        Directory.GetFiles(Path.Combine(fixture.ArchiveDirectory,"recovery-grants"),"*.json",SearchOption.AllDirectories).Length.ShouldBe(2);
        (await authority.VerifyAgedOriginalRecoveryAsync(first,proof,TestContext.Current.CancellationToken)).ShouldBeFalse();
        fixture.State=fixture.State with { Recoveries=[] };fixture.Restart();authority=new(fixture.Client());
        (await authority.VerifyAgedOriginalRecoveryAsync(renewed,proof,TestContext.Current.CancellationToken)).ShouldBeTrue();
        fixture.State.Recoveries.ShouldBeEmpty();
        fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Revoked=true }).ToArray() };
        (await authority.VerifyAgedOriginalRecoveryAsync(renewed,proof,TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
    [Fact]
    public async Task CommittedRecoveryGrantFinishesAfterLostPublicationWithoutUnreservedProbeAllocation()
    {
        if(OperatingSystem.IsWindows()) return;
        string root=Path.Combine(Path.GetTempPath(),"grant-capacity-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            byte[] enrollment="fixture-recovery-capacity"u8.ToArray();string hash=Convert.ToHexString(SHA256.HashData(enrollment));
            await File.WriteAllBytesAsync(Path.Combine(root,"enrollment.json"),enrollment,TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(root,"capacity.json"),JsonSerializer.SerializeToUtf8Bytes(new IndependentArchiveCapacity.Ledger { EnrollmentSha256=hash },CustodyBindingContract.Json),TestContext.Current.CancellationToken);
            var config=new IndependentArchiveCapacityInstallation("LinuxSharedPhysicalArchiveCapacityV1",root,hash,IndependentArchiveCapacity.MetadataBytes+10*1024*1024,VolumeFenceIdentity:await ArchiveVolumeFence.ReadIdentityAsync(root,TestContext.Current.CancellationToken));
            long free=long.MaxValue;int probes=0;bool lose=true;
            var storage=new IndependentArchiveStorage { ProbeOverride=(_,_)=> { probes++;return free==0?Task.FromException(new IOException("fixture ENOSPC unreserved probe")):Task.CompletedTask; } };
            ProtectedArchiveAllocationInstallation? protectedFiles=null;
            IndependentArchiveCapacity Pool(IndependentArchiveCapacityInstallation c,string directory,string owner)=>new(c with { ProtectedAllocations=protectedFiles },directory,owner) { AvailableBytesOverride=()=>free,Fault=point=> { if(point=="transferred" && lose) { lose=false;throw new IOException("fixture lost grant publication"); } } };
            using var fixture=new IndependentAuthorityReceiverTests.Fixture(archiveStorage:storage,capacity:config,capacityFactory:Pool);
            protectedFiles=ProtectedArchiveFixture.Enroll(root,[fixture.ArchiveDirectory],10*1024*1024);
            fixture.State=fixture.State with { OriginalLiabilityVersion=1,PendingOriginalCompletionBytes=0,Callers=fixture.State.Callers.Select(value=>value with { Role="aged-recovery",Methods=["spool-admit-recovery","spool-verify-recovery","spool-current"] }).ToArray() };
            var now=DateTimeOffset.UtcNow;
            var record=new SecurityObservationRecord(new("observation","tenant","invalid-tag",new string('a',64),"v1"),now,now.ToString("yyyy-MM-dd"),1,null);
            string id=PrivateRecordingIdentity.SecurityOriginalMessageId("observation");
            var proof=new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.NotRecorded,NoRecordProof:new(id,id,new string('a',64),"fixture-native-source",0,new string('b',64),null,"RetainedUnsent"));
            fixture.RecordingSource.LookupAsync(record,Arg.Any<CancellationToken>()).Returns(proof);
            var authority=new DaprReplicatedSpoolAuthority(fixture.Client());
            (await authority.AuthorizeAgedOriginalRecoveryAsync(fixture.Target,record,proof,TestContext.Current.CancellationToken)).ShouldBeNull();
            fixture.State.Recoveries.Count.ShouldBe(1);var committed=fixture.State.Recoveries.Values.Single();int admittedProbes=probes;
            free=0;fixture.Restart();
            (await authority.AuthorizeAgedOriginalRecoveryAsync(fixture.Target,record,proof,TestContext.Current.CancellationToken)).ShouldBe(committed);
            (await authority.VerifyAgedOriginalRecoveryAsync(committed,proof,TestContext.Current.CancellationToken)).ShouldBeTrue();probes.ShouldBe(admittedProbes);
            File.SetUnixFileMode(fixture.ArchiveDirectory,UnixFileMode.UserRead|UnixFileMode.UserExecute);
            try { (await authority.VerifyAgedOriginalRecoveryAsync(committed,proof,TestContext.Current.CancellationToken)).ShouldBeFalse(); }
            finally { File.SetUnixFileMode(fixture.ArchiveDirectory,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute); }
        }
        finally { Directory.Delete(root,true); }
    }
    private sealed class Clock:TimeProvider
    {
        internal DateTimeOffset Now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
}
