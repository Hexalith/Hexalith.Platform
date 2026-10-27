using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class BoundedInstallationAndPublicationTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task StalledDirectoryAcquisitionAndPostPublicationDurabilityKeepWorkerAndExactOriginalOwnership(bool published)
    {
        string root=Path.Combine(Path.GetTempPath(),"archive-durability-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        using var release=new ManualResetEventSlim();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var budget=new CancellationTokenSource();string path=Path.Combine(root,"original");
        Action stall=()=>{ entered.TrySetResult();release.Wait(); };
        try
        {
            var pending=ImmutableFileArchive.PublishWithHooksAsync(path,new byte[]{1},budget.Token,beforeAcquire:published?null:stall,beforeDurability:published?stall:null);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken);budget.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(()=>pending.WaitAsync(TimeSpan.FromSeconds(1),TestContext.Current.CancellationToken));
            File.Exists(path).ShouldBe(published);release.Set();await ImmutableFileArchive.PublishAsync(path,new byte[]{1},TestContext.Current.CancellationToken);
            (await File.ReadAllBytesAsync(path,TestContext.Current.CancellationToken)).ShouldBe(new byte[]{1});
        }
        finally { release.Set();Directory.Delete(root,true); }
    }
    [Fact]
    public async Task ActualPublicationRejectsDirectorySwapAndRecoversExactOriginalAfterLocatorRestoration()
    {
        string root=Path.Combine(Path.GetTempPath(),"archive-held-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string enrolled=Path.Combine(root,"enrolled"),parked=Path.Combine(root,"parked"),outside=Path.Combine(root,"outside");Directory.CreateDirectory(enrolled);Directory.CreateDirectory(outside);
        string path=Path.Combine(enrolled,"original");byte[] bytes=[1,2,3];
        try
        {
            await Should.ThrowAsync<IOException>(()=>ImmutableFileArchive.PublishAsync(path,bytes,TestContext.Current.CancellationToken,()=> { Directory.Move(enrolled,parked);Directory.CreateSymbolicLink(enrolled,outside); }));
            Directory.GetFiles(outside).ShouldBeEmpty();File.Exists(path).ShouldBeFalse();
            Directory.Delete(enrolled);Directory.Move(parked,enrolled);
            await Task.WhenAll(Enumerable.Range(0,4).Select(_=>ImmutableFileArchive.PublishAsync(path,bytes,TestContext.Current.CancellationToken)));
            (await File.ReadAllBytesAsync(path,TestContext.Current.CancellationToken)).ShouldBe(bytes);
            await ImmutableFileArchive.PublishAsync(path,bytes,TestContext.Current.CancellationToken);Directory.GetFiles(outside).ShouldBeEmpty();
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task StalledDurablePublicationReturnsAtDeadlineAndOwnsPermanentFenceUntilKernelWorkEnds()
    {
        string root=Path.Combine(Path.GetTempPath(),"archive-stall-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        using var release=new ManualResetEventSlim();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var budget=new CancellationTokenSource();
        string path=Path.Combine(root,"original");
        try
        {
            var pending=ImmutableFileArchive.PublishAsync(path,new byte[]{1},budget.Token,()=> { entered.TrySetResult();release.Wait(); });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken);budget.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(()=>pending.WaitAsync(TimeSpan.FromSeconds(1),TestContext.Current.CancellationToken));
            using var contenderBudget=new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Should.ThrowAsync<OperationCanceledException>(()=>ImmutableFileArchive.PublishAsync(path,new byte[]{1},contenderBudget.Token));File.Exists(path).ShouldBeFalse();
            release.Set();await ImmutableFileArchive.PublishAsync(path,new byte[]{1},TestContext.Current.CancellationToken);
            (await File.ReadAllBytesAsync(path,TestContext.Current.CancellationToken)).ShouldBe(new byte[]{1});
        }
        finally { release.Set();Directory.Delete(root,true); }
    }
    [Fact]
    public async Task StalledAcquisitionBoundsCallersAndWorkersUntilKernelWorkReturns()
    {
        using var release=new ManualResetEventSlim();using var cancelled=new CancellationTokenSource();
        int entered=0,finished=0;var allEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls=Enumerable.Range(0,4).Select(_=>BoundedRegularFile.RunAcquisitionAsync(token=>
        {
            if(Interlocked.Increment(ref entered)==4) allEntered.TrySetResult();
            try { release.Wait();token.ThrowIfCancellationRequested();return Task.FromResult(new byte[]{1}); }
            finally { Interlocked.Increment(ref finished); }
        },cancelled.Token,TimeSpan.FromSeconds(10))).ToArray();
        try
        {
            await allEntered.Task.WaitAsync(TimeSpan.FromSeconds(5),TestContext.Current.CancellationToken);
            cancelled.Cancel();
            foreach(var call in calls) await Should.ThrowAsync<OperationCanceledException>(()=>call.WaitAsync(TimeSpan.FromSeconds(1),TestContext.Current.CancellationToken));
            await Should.ThrowAsync<OperationCanceledException>(()=>BoundedRegularFile.RunAcquisitionAsync(_=>
                { Interlocked.Increment(ref entered);return Task.FromResult(new byte[]{2}); },TestContext.Current.CancellationToken,TimeSpan.FromMilliseconds(100)));
            entered.ShouldBe(4);finished.ShouldBe(0);
        }
        finally { release.Set(); }
        // Returned syscalls clean up and release slots even though their callers have already timed out.
        var deadline=DateTimeOffset.UtcNow.AddSeconds(5);
        while(Volatile.Read(ref finished)!=4 && DateTimeOffset.UtcNow<deadline) await Task.Delay(10,TestContext.Current.CancellationToken);
        finished.ShouldBe(4);
        (await BoundedRegularFile.RunAcquisitionAsync(_=>Task.FromResult(new byte[]{3}),TestContext.Current.CancellationToken,TimeSpan.FromSeconds(1))).ShouldBe(new byte[]{3});
    }
    [Fact]
    public async Task ExactDescriptorAcquisitionRejectsOversizeSymlinkFifoCancellationAndGrowth()
    {
        string directory=Path.Combine(Path.GetTempPath(),"bounded-installation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            string path=Path.Combine(directory,"input");byte[] bytes="{\"installed\":false}"u8.ToArray();await File.WriteAllBytesAsync(path,bytes,TestContext.Current.CancellationToken);
            (await BoundedRegularFile.ReadAsync(path,1024,TestContext.Current.CancellationToken)).ShouldBe(bytes);
            await Should.ThrowAsync<IOException>(()=>BoundedRegularFile.ReadAsync(path,2,TestContext.Current.CancellationToken));
            File.CreateSymbolicLink(path+"-link",path);await Should.ThrowAsync<IOException>(()=>BoundedRegularFile.ReadAsync(path+"-link",1024,TestContext.Current.CancellationToken));
            await Should.ThrowAsync<IOException>(()=>BoundedRegularFile.ReadAsync(directory,1024,TestContext.Current.CancellationToken));
            Mkfifo(path+"-fifo",0x180).ShouldBe(0);await Should.ThrowAsync<IOException>(()=>BoundedRegularFile.ReadAsync(path+"-fifo",1024,TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(1),TestContext.Current.CancellationToken));
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();await Should.ThrowAsync<OperationCanceledException>(()=>BoundedRegularFile.ReadAsync(path,1024,cancelled.Token));
            await File.WriteAllBytesAsync(path,new byte[32768],TestContext.Current.CancellationToken);
            // Change the real already-open descriptor's file after its bounded metadata sample.
            // This deterministic fixture must fail if the reader returns the old prefix without checking growth/change.
            await Should.ThrowAsync<IOException>(()=>BoundedRegularFile.ReadWithAcquisitionAsync(path,65536,TestContext.Current.CancellationToken,TimeSpan.FromSeconds(2),null,()=>
            { using var writer=new FileStream(path,FileMode.Open,FileAccess.Write,FileShare.ReadWrite|FileShare.Delete);writer.SetLength(98304);writer.Flush(true); }));
        }
        finally { Directory.Delete(directory,true); }
    }
    [Fact]
    public async Task CrossProcessExactWritersPublishOneBodyAndRejectConflictWithoutDuplicateTemporaries()
    {
        string directory=Path.Combine(Path.GetTempPath(),"archive-process-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            string path=Path.Combine(directory,"original"),body=Path.Combine(directory,"source");byte[] bytes=RandomNumberGenerator.GetBytes(8*1024*1024);
            await File.WriteAllBytesAsync(body,bytes,TestContext.Current.CancellationToken);
            int created=0;using var watcher=new FileSystemWatcher(directory,"original.pending") { EnableRaisingEvents=true };watcher.Created+=(_,_)=>Interlocked.Increment(ref created);
            var children=Enumerable.Range(0,6).Select(_=>Child(path,body)).ToArray();
            try { await Task.WhenAll(children.Select(child=>child.WaitForExitAsync(TestContext.Current.CancellationToken)));foreach(var child in children) child.ExitCode.ShouldBe(0,await child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken)); }
            finally { foreach(var child in children) child.Dispose(); }
            (await File.ReadAllBytesAsync(path,TestContext.Current.CancellationToken)).ShouldBe(bytes);created.ShouldBe(0);
            await Should.ThrowAsync<IOException>(()=>ImmutableFileArchive.PublishAsync(path,new byte[]{1},TestContext.Current.CancellationToken));
            (await File.ReadAllBytesAsync(path,TestContext.Current.CancellationToken)).ShouldBe(bytes);
            // Kill a distinct process holding the real archive fence after its partial unpublished write.
            // Kernel ownership must release on death, while the permanent inode and original history survive.
            string interrupted=Path.Combine(directory,"interrupted");
            using(var child=Child(interrupted,body,"hold-interrupted-writer"))
            {
                try
                {
                    (await child.StandardOutput.ReadLineAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10),TestContext.Current.CancellationToken)).ShouldBe("interrupted-writer-ready");
                    using var cancelledWriter=new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                    await Should.ThrowAsync<OperationCanceledException>(()=>ImmutableFileArchive.PublishAsync(interrupted,bytes,cancelledWriter.Token));
                    File.Exists(interrupted).ShouldBeFalse();
                }
                finally { if(!child.HasExited) child.Kill(true);await child.WaitForExitAsync(TestContext.Current.CancellationToken); }
            }
            await ImmutableFileArchive.PublishAsync(interrupted,bytes,TestContext.Current.CancellationToken);
            File.Exists(interrupted+".pending").ShouldBeFalse();
            (await File.ReadAllBytesAsync(interrupted,TestContext.Current.CancellationToken)).ShouldBe(bytes);
            (await File.ReadAllBytesAsync(path,TestContext.Current.CancellationToken)).ShouldBe(bytes);
            string lockPath=Path.Combine(directory,".publish-"+Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(path)))));
            using(var held=await ArchiveFileLease.AcquireAsync(lockPath,TestContext.Current.CancellationToken))
            {
                using var timeout=new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
                await Should.ThrowAsync<OperationCanceledException>(()=>ImmutableFileArchive.PublishAsync(path,bytes,timeout.Token));
            }
            await ImmutableFileArchive.PublishAsync(path,bytes,TestContext.Current.CancellationToken);
        }
        finally { Directory.Delete(directory,true); }
    }
    private static Process Child(string path,string body,string? mode=null)
    {
        var start=new ProcessStartInfo("dotnet") { RedirectStandardError=true,RedirectStandardOutput=true,UseShellExecute=false };
        foreach(string argument in new[]{"exec","--runtimeconfig",Path.ChangeExtension(typeof(BoundedInstallationAndPublicationTests).Assembly.Location,"runtimeconfig.json"),"--depsfile",Path.ChangeExtension(typeof(BoundedInstallationAndPublicationTests).Assembly.Location,"deps.json"),typeof(ArchiveProcessMarker).Assembly.Location,path,body}) start.ArgumentList.Add(argument);
        if(mode is not null) start.ArgumentList.Add(mode);
        return Process.Start(start)!;
    }
    [DllImport("libc",EntryPoint="mkfifo",SetLastError=true)] private static extern int Mkfifo(string path,int mode);
}
