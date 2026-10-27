using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.Tests;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class ProtectedArchiveAllocationTests
{
    [Fact]
    public async Task ActualIndependentPublicationAndRestartSettlementNeedNoInodeEntryOrMetadataAllocationAfterAdmission()
    {
        string root=Path.Combine(Path.GetTempPath(),"protected-completion-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            byte[] enrollment="fixture-independent"u8.ToArray();string hash=Convert.ToHexString(SHA256.HashData(enrollment));File.WriteAllBytes(Path.Combine(root,"enrollment.json"),enrollment);
            File.WriteAllBytes(Path.Combine(root,"capacity.json"),JsonSerializer.SerializeToUtf8Bytes(new IndependentArchiveCapacity.Ledger { EnrollmentSha256=hash },CustodyBindingContract.Json));
            var protectedFiles=ProtectedArchiveFixture.Enroll(root,[root],10*1024*1024);
            var config=new IndependentArchiveCapacityInstallation("LinuxSharedPhysicalArchiveCapacityV1",root,hash,IndependentArchiveCapacity.MetadataBytes+10*1024*1024,VolumeFenceIdentity:await ArchiveVolumeFence.ReadIdentityAsync(root,TestContext.Current.CancellationToken),ProtectedAllocations:protectedFiles);
            long free=long.MaxValue;string ticket=new string('A',64),path=Path.Combine(root,"new-logical-directory","result.json");byte[] body=new byte[70000];RandomNumberGenerator.Fill(body);
            IndependentArchiveCapacity Pool(bool lose=false)=>new(config,root,"owner") { AvailableBytesOverride=()=>free,Fault=point=> { if(lose && point=="published") throw new IOException("fixture lost published acknowledgement"); } };
            var pool=Pool();await pool.ReserveAsync(ticket,4*1024*1024,3*1024*1024,TestContext.Current.CancellationToken);await pool.CommitAsync(ticket,1,TestContext.Current.CancellationToken);free=0;
            int allocations=0;HeldArchiveDirectory.AllocationAttempt=_=> { allocations++;throw new IOException("fixture inode/metadata allocation exhausted after native admission"); };
            await Should.ThrowAsync<IOException>(()=>Pool(true).PublishAsync(ticket,path,body,TestContext.Current.CancellationToken));
            await Pool().PublishAsync(ticket,path,body,TestContext.Current.CancellationToken);(await BoundedRegularFile.ReadAsync(path,body.Length,TestContext.Current.CancellationToken)).ShouldBe(body);
            await Pool().SettleAsync(ticket,TestContext.Current.CancellationToken);allocations.ShouldBe(0);
            await Should.ThrowAsync<IOException>(()=>Pool().ReserveAsync("new-ticket",1024*1024,0,TestContext.Current.CancellationToken));
            File.Exists(path).ShouldBeFalse();Directory.Exists(Path.GetDirectoryName(path)).ShouldBeFalse();
        }
        finally { HeldArchiveDirectory.AllocationAttempt=null;Directory.Delete(root,true); }
    }
    [Theory]
    [InlineData("data-link")][InlineData("metadata-link")][InlineData("inode-replacement")][InlineData("hole")]
    public async Task ExactExclusivePrivateWrittenResourcesAreRequiredBeforeCountingOrTransferring(string fault)
    {
        string root=Path.Combine(Path.GetTempPath(),"protected-ownership-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var config=ProtectedArchiveFixture.Enroll(root,[root],1024*1024);string file=Path.Combine(root,fault=="metadata-link"?"allocation-metadata-0.bin":"allocation-data.bin");
            await ArchiveVolumeFence.RunAsync(root,_=> { using var valid=new ProtectedArchiveStore(config);valid.Reserve("pending",65536);return Task.FromResult(true); },TestContext.Current.CancellationToken);
            if(fault.EndsWith("link",StringComparison.Ordinal)) Link(file,file+"-cross-pool-alias").ShouldBe(0);
            else if(fault=="inode-replacement") { File.Move(file,file+"-original");File.Copy(file+"-original",file); }
            else { using var output=new FileStream(file,FileMode.Open,FileAccess.ReadWrite);Fallocate(output.SafeFileHandle.DangerousGetHandle().ToInt32(),3,0,65536).ShouldBe(0); }
            Should.Throw<IOException>(()=> { using var invalid=new ProtectedArchiveStore(config);invalid.Reserve("another",65536); });
            File.Exists(file).ShouldBeTrue();
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task GenericSubtreeAliasCannotSelectAnotherPhysicalVolumeFence()
    {
        string root=Path.Combine(Path.GetTempPath(),"archive-alias-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            ArchiveVolumeFence.NamespaceRootOverride=_=>"/subtree-alias";bool allocated=false;
            await Should.ThrowAsync<IOException>(()=>ArchiveVolumeFence.RunForPublicationAsync(root,_=> { allocated=true;return Task.FromResult(true); },TestContext.Current.CancellationToken));allocated.ShouldBeFalse();
        }
        finally { ArchiveVolumeFence.NamespaceRootOverride=null;Directory.Delete(root,true); }
    }
    [Fact]
    public async Task MaximumEscapedUnicodeLocatorFitsThePreAdmittedFixedHeader()
    {
        string root=Path.Combine(Path.GetTempPath(),"protected-long-locator-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var config=ProtectedArchiveFixture.Enroll(root,[root],1024*1024);string path=root+"/missing/";
            while(path.Length<4096) { string component="Ω\"\\newline\n/";path+=component[..Math.Min(component.Length,4096-path.Length)]; }
            if(path.EndsWith('/')) path=path[..^1]+"Ω";path.Length.ShouldBe(4096);
            await ArchiveVolumeFence.RunAsync(root,_=> { using var store=new ProtectedArchiveStore(config);store.Reserve("exact",65536);store.Publish("exact",path,"complete"u8);return Task.FromResult(true); },TestContext.Current.CancellationToken);
            (await BoundedRegularFile.ReadAsync(path,65536,TestContext.Current.CancellationToken)).ShouldBe("complete"u8.ToArray());
        }
        finally { Directory.Delete(root,true); }
    }
    [Theory]
    [InlineData("initial-body")][InlineData("header")][InlineData("committed-body")]
    public async Task NonemptyDamagedMetadataNeverFallsBackToUntouchedLegacyGenesis(string fault)
    {
        string root=Path.Combine(Path.GetTempPath(),"protected-damaged-metadata-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var config=ProtectedArchiveFixture.Enroll(root,[root],1024*1024);byte[] genesis="untouched legacy genesis"u8.ToArray();File.WriteAllBytes(Path.Combine(root,"capacity.json"),genesis);
            if(fault=="committed-body") await ArchiveVolumeFence.RunAsync(root,_=> { using var store=new ProtectedArchiveStore(config);store.WriteMetadata("committed"u8);return Task.FromResult(true); },TestContext.Current.CancellationToken);
            string bank=Path.Combine(root,"allocation-metadata-0.bin");using(var file=new FileStream(bank,FileMode.Open,FileAccess.ReadWrite)) { file.Position=fault=="header"?0:8192;file.WriteByte(0xff);file.Flush(true); }
            byte[] damaged=File.ReadAllBytes(bank);
            await Should.ThrowAsync<IOException>(()=>ArchiveVolumeFence.RunAsync(root,_=> { using var store=new ProtectedArchiveStore(config);store.ReadMetadata();return Task.FromResult(true); },TestContext.Current.CancellationToken));
            File.ReadAllBytes(bank).ShouldBe(damaged);File.ReadAllBytes(Path.Combine(root,"capacity.json")).ShouldBe(genesis);
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task DamagedPublishedFixedHeaderBlocksWithoutReplacementOnRestart()
    {
        string root=Path.Combine(Path.GetTempPath(),"protected-damaged-original-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var config=ProtectedArchiveFixture.Enroll(root,[root],1024*1024);string path=Path.Combine(root,"original");
            await ArchiveVolumeFence.RunAsync(root,_=> { using var store=new ProtectedArchiveStore(config);store.Reserve("exact",65536);store.Publish("exact",path,"original"u8);return Task.FromResult(true); },TestContext.Current.CancellationToken);
            string data=Path.Combine(root,"allocation-data.bin");using(var file=new FileStream(data,FileMode.Open,FileAccess.ReadWrite)) { file.Position=2*8192+40;file.WriteByte(0xff);file.Flush(true); }
            byte[] damaged=File.ReadAllBytes(data);
            await Should.ThrowAsync<IOException>(()=>ArchiveVolumeFence.RunAsync(root,_=> { using var store=new ProtectedArchiveStore(config);store.Publish("exact",path,"replacement"u8);return Task.FromResult(true); },TestContext.Current.CancellationToken));File.ReadAllBytes(data).ShouldBe(damaged);
        }
        finally { Directory.Delete(root,true); }
    }
    [Theory]
    [InlineData(0x2000)][InlineData(0x2)][InlineData(0x4)][InlineData(0x800)][InlineData(-1)]
    public void DeterministicExtentProbeRejectsSharedUnknownDelayedUnwrittenAndUnsupportedMappings(int flags)
    {
        string root=Path.Combine(Path.GetTempPath(),"protected-extent-fault-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var config=ProtectedArchiveFixture.Enroll(root,[root],1024*1024);
            ArchiveAllocationProof.ExtentProbeOverride=(_,frame)=>
            {
                if(flags<0) return 95;BitConverter.TryWriteBytes(frame.AsSpan(20),1u);BitConverter.TryWriteBytes(frame.AsSpan(32),0L);BitConverter.TryWriteBytes(frame.AsSpan(40),4096L);BitConverter.TryWriteBytes(frame.AsSpan(48),long.MaxValue/2);BitConverter.TryWriteBytes(frame.AsSpan(72),(uint)(flags|1));return 0;
            };
            Should.Throw<IOException>(()=> { using var store=new ProtectedArchiveStore(config); });
        }
        finally { ArchiveAllocationProof.ExtentProbeOverride=null;Directory.Delete(root,true); }
    }
    [Theory]
    [InlineData("ordinary")][InlineData("independent")]
    public async Task ActualWritersRejectConflictingLegacyLocatorsAndRecoverExactOriginalBytes(string writer)
    {
        string root=Path.Combine(Path.GetTempPath(),"protected-legacy-locator-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            byte[] genesis="fixture legacy capacity"u8.ToArray();string hash=Convert.ToHexString(SHA256.HashData(genesis));File.WriteAllBytes(Path.Combine(root,"enrollment.json"),genesis);File.WriteAllBytes(Path.Combine(root,"capacity.json"),JsonSerializer.SerializeToUtf8Bytes(new IndependentArchiveCapacity.Ledger { EnrollmentSha256=hash },CustodyBindingContract.Json));
            var protectedFiles=ProtectedArchiveFixture.Enroll(root,[root],4*1024*1024);var config=new IndependentArchiveCapacityInstallation("LinuxSharedPhysicalArchiveCapacityV1",root,hash,IndependentArchiveCapacity.MetadataBytes+4*1024*1024,VolumeFenceIdentity:await ArchiveVolumeFence.ReadIdentityAsync(root,TestContext.Current.CancellationToken),ProtectedAllocations:protectedFiles);
            var pool=new IndependentArchiveCapacity(config,root,"owner");string ticket=pool.Ticket("executor","legacy");if(writer=="independent") await pool.ReserveAsync(ticket,1024*1024,65536,TestContext.Current.CancellationToken);
            string path=Path.Combine(root,"legacy");byte[] exact="actual legacy bytes"u8.ToArray();File.WriteAllBytes(path,exact);byte[] before=File.ReadAllBytes(Path.Combine(root,"allocation-data.bin"));
            Task Publish(byte[] bytes)=>writer=="ordinary"?ImmutableFileArchive.PublishAsync(path,bytes,TestContext.Current.CancellationToken):pool.PublishAsync(ticket,path,bytes,TestContext.Current.CancellationToken);
            await Should.ThrowAsync<IOException>(()=>Publish("different new bytes"u8.ToArray()));File.ReadAllBytes(path).ShouldBe(exact);File.ReadAllBytes(Path.Combine(root,"allocation-data.bin")).ShouldBe(before);
            await Publish(exact);(await BoundedRegularFile.ReadAsync(path,65536,TestContext.Current.CancellationToken)).ShouldBe(exact);File.ReadAllBytes(Path.Combine(root,"allocation-data.bin")).ShouldBe(before);
        }
        finally { Directory.Delete(root,true); }
    }
    [Theory]
    [InlineData("intent-durable","before")][InlineData("data-durable","after")]
    public async Task ExplicitInterruptedMetadataRecoversOnlyACompleteBankWithoutLegacyRollback(string fault,string expected)
    {
        string root=Path.Combine(Path.GetTempPath(),"protected-metadata-recovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var config=ProtectedArchiveFixture.Enroll(root,[root],1024*1024);
            await ArchiveVolumeFence.RunAsync(root,_=> { using var store=new ProtectedArchiveStore(config);store.WriteMetadata("before"u8);return Task.FromResult(true); },TestContext.Current.CancellationToken);
            ProtectedArchiveStore.MetadataFault=point=> { if(point==fault) throw new IOException("isolated interrupted metadata acknowledgement"); };
            await Should.ThrowAsync<IOException>(()=>ArchiveVolumeFence.RunAsync(root,_=> { using var store=new ProtectedArchiveStore(config);store.WriteMetadata("after"u8);return Task.FromResult(true); },TestContext.Current.CancellationToken));ProtectedArchiveStore.MetadataFault=null;
            HeldArchiveDirectory.AllocationAttempt=_=>throw new IOException("no post-admission inode allocation");
            byte[]? retained=await ArchiveVolumeFence.RunAsync(root,_=> { using var store=new ProtectedArchiveStore(config);return Task.FromResult(store.ReadMetadata()); },TestContext.Current.CancellationToken);System.Text.Encoding.UTF8.GetString(retained!).ShouldBe(expected);
            // Reopen between every retry: intent-only recovery must reuse its durable generation,
            // while a complete data-durable intent must be committed before advancing again.
            for(int retry=0;retry<3;retry++)
            {
                byte[] next=System.Text.Encoding.UTF8.GetBytes("retry-"+retry);
                await ArchiveVolumeFence.RunAsync(root,_=> { using var store=new ProtectedArchiveStore(config);store.WriteMetadata(next);return Task.FromResult(true); },TestContext.Current.CancellationToken);
                (await ArchiveVolumeFence.RunAsync(root,_=> { using var store=new ProtectedArchiveStore(config);return Task.FromResult(store.ReadMetadata()); },TestContext.Current.CancellationToken)).ShouldBe(next);
            }
        }
        finally { ProtectedArchiveStore.MetadataFault=null;HeldArchiveDirectory.AllocationAttempt=null;Directory.Delete(root,true); }
    }
    [DllImport("libc",EntryPoint="link",SetLastError=true)] private static extern int Link(string source,string target);
    [DllImport("libc",EntryPoint="fallocate",SetLastError=true)] private static extern int Fallocate(int fd,int mode,long offset,long length);
}
