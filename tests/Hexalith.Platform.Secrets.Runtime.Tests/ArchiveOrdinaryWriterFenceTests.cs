using Hexalith.Platform.Tests;
using System.Security.Cryptography;
using System.Text.Json;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

[Collection("Archive volume fence scheduling")]
public sealed class ArchiveOrdinaryWriterFenceTests
{
    [Fact]
    public async Task ActualOrdinaryWriterCannotAllocateDescendantsWhileReservedTransferOwnsTheVolume()
    {
        string root=Path.Combine(Path.GetTempPath(),"ordinary-transfer-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string poolPath=Path.Combine(root,"pool"),archive=Path.Combine(root,"archive");Directory.CreateDirectory(poolPath);Directory.CreateDirectory(archive);
            byte[] marker="fixture-only-volume-transfer"u8.ToArray();string hash=Convert.ToHexString(SHA256.HashData(marker));
            await File.WriteAllBytesAsync(Path.Combine(poolPath,"enrollment.json"),marker,TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(poolPath,"capacity.json"),JsonSerializer.SerializeToUtf8Bytes(new IndependentArchiveCapacity.Ledger { EnrollmentSha256=hash },CustodyBindingContract.Json),TestContext.Current.CancellationToken);
            var config=new IndependentArchiveCapacityInstallation("LinuxSharedPhysicalArchiveCapacityV1",poolPath,hash,IndependentArchiveCapacity.MetadataBytes+8*1024*1024,VolumeFenceIdentity:await ArchiveVolumeFence.ReadIdentityAsync(root,TestContext.Current.CancellationToken));
            config=config with { ProtectedAllocations=ProtectedArchiveFixture.Enroll(poolPath,[archive],8*1024*1024) };
            var testToken=TestContext.Current.CancellationToken;
            byte[] bytes=new byte[1024*1024];RandomNumberGenerator.Fill(bytes);Task? ordinary=null;using var started=new ManualResetEventSlim();using var published=new ManualResetEventSlim();
            string ordinaryPath=Path.Combine(archive,"ordinary","missing","result");
            var pool=new IndependentArchiveCapacity(config,archive,"head") { Fault=point=>
            {
                if(point!="transferred") return;
                // A genuinely independent writer receives no borrowed native-operation capability.
                using(ExecutionContext.SuppressFlow()) ordinary=Task.Run(()=>ImmutableFileArchive.PublishWithHooksAsync(ordinaryPath,bytes,testToken,beforeAcquire:()=>started.Set(),beforeDurability:()=>published.Set()));
                started.Wait(TimeSpan.FromSeconds(3),testToken).ShouldBeTrue();
                published.Wait(TimeSpan.FromMilliseconds(500),testToken).ShouldBeFalse();
                Directory.Exists(Path.GetDirectoryName(ordinaryPath)).ShouldBeFalse();
            } };
            string ticket=pool.Ticket("executor","result");await pool.ReserveAsync(ticket,4*1024*1024,3*1024*1024,TestContext.Current.CancellationToken);
            await pool.PublishAsync(ticket,Path.Combine(archive,"reserved"),bytes,TestContext.Current.CancellationToken);
            await ordinary.ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(5),TestContext.Current.CancellationToken);
            (await BoundedRegularFile.ReadAsync(ordinaryPath,bytes.Length,TestContext.Current.CancellationToken)).ShouldBe(bytes);
            (await BoundedRegularFile.ReadAsync(Path.Combine(archive,"reserved"),bytes.Length,TestContext.Current.CancellationToken)).ShouldBe(bytes);
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task BorrowedFenceSurvivesOriginalDisposalAndCallerCancellationUntilPublicationCleanupFinishes()
    {
        string root=Path.Combine(Path.GetTempPath(),"borrowed-volume-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        using var release=new ManualResetEventSlim();using var cancellation=new CancellationTokenSource();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            string path=Path.Combine(root,"missing","original");
            var running=ArchiveVolumeFence.RunWithLeaseAsync(root,async (reference,token)=>
            {
                var late=Task.Run(async()=>
                {
                    try { await ImmutableFileArchive.PublishWithHooksAsync(path,"exact-late-original"u8.ToArray(),token,beforeDurability:()=> { entered.TrySetResult();release.Wait(); }); }
                    finally { finished.TrySetResult(); }
                });
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken);
                reference.Dispose();ready.TrySetResult();await late;return true;
            },cancellation.Token);
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(4),TestContext.Current.CancellationToken);cancellation.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(()=>running.WaitAsync(TimeSpan.FromSeconds(1),TestContext.Current.CancellationToken));
            using(var timeout=new CancellationTokenSource(TimeSpan.FromMilliseconds(100))) await Should.ThrowAsync<OperationCanceledException>(()=>ArchiveVolumeFence.AcquireAsync(root,timeout.Token));
            finished.Task.IsCompleted.ShouldBeFalse();release.Set();await finished.Task.WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken);
            using var available=await ArchiveVolumeFence.AcquireAsync(root,TestContext.Current.CancellationToken);
            File.Exists(path).ShouldBeTrue();
        }
        finally { release.Set();Directory.Delete(root,true); }
    }
}

[CollectionDefinition("Archive volume fence scheduling",DisableParallelization=true)]
public sealed class ArchiveVolumeFenceSchedulingCollection;
