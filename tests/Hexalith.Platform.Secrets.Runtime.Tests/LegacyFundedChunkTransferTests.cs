using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.Tests;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class LegacyFundedChunkTransferTests
{
    [Fact]
    public void UnsupportedRoundingGeometryRefusesBeforeAnyFundedAdmissionOrMetadataMutation()
    {
        string root=Path.Combine(Path.GetTempPath(),"legacy-rounding-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var geometry=ProtectedArchiveFixture.Enroll(root,[root],4*49152,chunkBytes:49152);
            var before=new[]{"allocation-data.bin","allocation-metadata-0.bin","allocation-metadata-1.bin"}.ToDictionary(name=>name,name=>File.ReadAllBytes(Path.Combine(root,name)));
            Should.Throw<IOException>(()=> { using var store=new ProtectedArchiveStore(geometry);store.Reserve("unfunded-rounding",131072); });
            foreach(var item in before) File.ReadAllBytes(Path.Combine(root,item.Key)).ShouldBe(item.Value);
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task UnfundedRoundedSplitPreservesAllExistingLiabilityBeforeChangingLedgerOrHeaders()
    {
        string root=Path.Combine(Path.GetTempPath(),"legacy-rounded-split-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var token=TestContext.Current.CancellationToken;
        try
        {
            byte[] enrolled="isolated existing ledger"u8.ToArray();string hash=Convert.ToHexString(SHA256.HashData(enrolled));File.WriteAllBytes(Path.Combine(root,"enrollment.json"),enrolled);File.WriteAllBytes(Path.Combine(root,"capacity.json"),JsonSerializer.SerializeToUtf8Bytes(new IndependentArchiveCapacity.Ledger { EnrollmentSha256=hash },CustodyBindingContract.Json));
            var physical=ProtectedArchiveFixture.Enroll(root,[root],65536);var config=new IndependentArchiveCapacityInstallation("LinuxSharedPhysicalArchiveCapacityV1",root,hash,IndependentArchiveCapacity.MetadataBytes+1024*1024,VolumeFenceIdentity:await ArchiveVolumeFence.ReadIdentityAsync(root,token),ProtectedAllocations:physical);
            var pool=new IndependentArchiveCapacity(config,root,"owner");await pool.EnsureLegacyAsync(65536,1,token);byte[] ledger=ProtectedArchiveFixture.Ledger(root,physical),chunks=File.ReadAllBytes(Path.Combine(root,"allocation-data.bin"));
            await Should.ThrowAsync<IOException>(()=>pool.ClaimLegacyAsync(pool.Ticket("executor","unfunded"),1,1,token));
            ProtectedArchiveFixture.Ledger(root,physical).ShouldBe(ledger);File.ReadAllBytes(Path.Combine(root,"allocation-data.bin")).ShouldBe(chunks);
        }
        finally { Directory.Delete(root,true); }
    }
    [Theory]
    [InlineData("cancel-first-transfer",1)][InlineData("none",0)][InlineData("legacy-claim-ledger-durable",1)][InlineData("legacy-chunk-transferred",1)]
    [InlineData("legacy-chunk-transferred",2)][InlineData("legacy-transfer-durable",1)][InlineData("legacy-transfer-completed",1)]
    public async Task TwoAdmittedLegacyLiabilitiesTransferTheirActualFullPoolFundingAndRecoverEveryOwnershipBoundary(string boundary,int occurrence)
    {
        string root=Path.Combine(Path.GetTempPath(),"funded-legacy-transfer-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        using var caller=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);var token=TestContext.Current.CancellationToken;
        try
        {
            byte[] enrolled="isolated existing ledger"u8.ToArray();string hash=Convert.ToHexString(SHA256.HashData(enrolled));File.WriteAllBytes(Path.Combine(root,"enrollment.json"),enrolled);
            File.WriteAllBytes(Path.Combine(root,"capacity.json"),JsonSerializer.SerializeToUtf8Bytes(new IndependentArchiveCapacity.Ledger { EnrollmentSha256=hash },CustodyBindingContract.Json));
            var physical=ProtectedArchiveFixture.Enroll(root,[root],4*65536);
            var config=new IndependentArchiveCapacityInstallation("LinuxSharedPhysicalArchiveCapacityV1",root,hash,IndependentArchiveCapacity.MetadataBytes+1024*1024,VolumeFenceIdentity:await ArchiveVolumeFence.ReadIdentityAsync(root,token),ProtectedAllocations:physical);
            IndependentArchiveCapacity Pool(string owner="legacy-owner",bool fault=false)
            {
                int seen=0;return new(config,root,owner) { AvailableBytesOverride=()=>long.MaxValue,Fault=point=> { if(fault && boundary=="cancel-first-transfer" && point=="legacy-chunk-transferred") { caller.Cancel();return; }if(fault && point==boundary && ++seen==occurrence) throw new IOException("isolated exact transfer interruption"); } };
            }
            var pool=Pool();await pool.EnsureLegacyAsync(4*65536,3,token);
            string first=pool.Ticket("executor","first"),second=pool.Ticket("executor","second"),commitment=new('C',64);
            Task Claim(IndependentArchiveCapacity current,string ticket)=>current.ClaimLegacyAsync(ticket,2*65536,3,boundary=="cancel-first-transfer" && !caller.IsCancellationRequested?caller.Token:token,commitment,"executor",ticket);
            if(boundary=="none") await Claim(pool,first);
            else if(boundary=="cancel-first-transfer")
            {
                await Should.ThrowAsync<OperationCanceledException>(()=>Claim(Pool(fault:true),first));
                using var interrupted=new ProtectedArchiveStore(physical);interrupted.ReservedBytes(root+"|"+first).ShouldBe(65536);interrupted.ReservedBytes(root+"|"+pool.Ticket("legacy","pending")).ShouldBe(3*65536);
            }
            else await Should.ThrowAsync<IOException>(()=>Claim(Pool(fault:true),first));
            // A different head must recover the pending exact transfer before inspecting capacity,
            // and cannot reuse either admitted liability's four already owned chunks.
            await Should.ThrowAsync<IOException>(()=>Pool("competing-head").ReserveAsync(new string('D',64),65536,0,token));
            await Claim(Pool(),first);var tickets=await Pool().OwnerTicketsAsync(token);
            tickets[first].ShouldBe(new IndependentArchiveCapacity.Reservation("legacy-owner","original",2*65536,2*65536,3,2*65536,2*65536,commitment,"executor",first,768,768,384));
            using(var store=new ProtectedArchiveStore(physical)) { store.ReservedBytes(root+"|"+first).ShouldBe(2*65536);store.ReservedBytes(root+"|"+pool.Ticket("legacy","pending")).ShouldBe(2*65536); }
            string firstPath=Path.Combine(root,"first-original");await Pool().PublishAsync(first,firstPath,"one"u8.ToArray(),token);await Pool().SettleAsync(first,token);
            await Claim(Pool(),second);string secondPath=Path.Combine(root,"second-original");await Pool().PublishAsync(second,secondPath,"two"u8.ToArray(),token);await Pool().SettleAsync(second,token);
            (await Pool().OwnerTicketsAsync(token)).ShouldBeEmpty();
            (await BoundedRegularFile.ReadAsync(firstPath,32,token)).ShouldBe("one"u8.ToArray());(await BoundedRegularFile.ReadAsync(secondPath,32,token)).ShouldBe("two"u8.ToArray());
            using var reopened=new ProtectedArchiveStore(physical);reopened.PublishedOriginals.ShouldBe(2);reopened.ReadMetadata().ShouldNotBeNull();
        }
        finally { Directory.Delete(root,true); }
    }
}
