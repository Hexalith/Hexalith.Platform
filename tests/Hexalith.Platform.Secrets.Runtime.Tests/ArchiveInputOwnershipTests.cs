using System.Buffers;
using System.Runtime.InteropServices;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class ArchiveInputOwnershipTests
{
    [Fact]
    public async Task CancellationCannotReturnCallerBufferWhileItsInitialOwnedCopyIsStillReading()
    {
        string root=Path.Combine(Path.GetTempPath(),"archive-input-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        using var input=new WatchedMemory();using var budget=new CancellationTokenSource();
        try
        {
            var pending=Task.Run(()=>ImmutableFileArchive.PublishAsync(Path.Combine(root,"original"),input.Memory,budget.Token),TestContext.Current.CancellationToken);
            await input.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken);budget.Cancel();
            await Task.Delay(40,TestContext.Current.CancellationToken);pending.IsCompleted.ShouldBeFalse();
            input.Release.Set();await Should.ThrowAsync<OperationCanceledException>(()=>pending);
            input.ReturnToPool();await Task.Delay(40,TestContext.Current.CancellationToken);input.ReadsAfterReturn.ShouldBe(0);
            File.Exists(Path.Combine(root,"original")).ShouldBeFalse();
            await ImmutableFileArchive.PublishAsync(Path.Combine(root,"original"),new byte[]{1},TestContext.Current.CancellationToken);
        }
        finally { input.Release.Set();Directory.Delete(root,true); }
    }
    [Fact]
    public async Task CancelledQueuedPublicationDoesNotReadOrCopyCallerBufferAfterReturning()
    {
        using var release=new ManualResetEventSlim();using var budget=new CancellationTokenSource();int entered=0;var full=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var occupying=Enumerable.Range(0,4).Select(_=>BoundedDurableFileOperation.RunAsync(ct=> { if(Interlocked.Increment(ref entered)==4) full.TrySetResult();release.Wait();return Task.FromResult(true); },TestContext.Current.CancellationToken)).ToArray();
        using var input=new WatchedMemory(block:false);string root=Path.Combine(Path.GetTempPath(),"archive-queued-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            await full.Task.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken);
            var queued=ImmutableFileArchive.PublishAsync(Path.Combine(root,"original"),input.Memory,budget.Token);budget.Cancel();await Should.ThrowAsync<OperationCanceledException>(()=>queued);
            input.ReturnToPool();release.Set();await Task.WhenAll(occupying);input.Reads.ShouldBe(0);input.ReadsAfterReturn.ShouldBe(0);
        }
        finally { release.Set();await Task.WhenAll(occupying);Directory.Delete(root,true); }
    }
    private sealed class WatchedMemory(bool block=true):MemoryManager<byte>
    {
        private readonly byte[] _bytes=new byte[1024];private bool _returned;internal int Reads,ReadsAfterReturn;
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);internal readonly ManualResetEventSlim Release=new();
        public override Memory<byte> Memory=>CreateMemory(_bytes.Length);
        public override Span<byte> GetSpan()
        { Interlocked.Increment(ref Reads);if(_returned) Interlocked.Increment(ref ReadsAfterReturn);Entered.TrySetResult();if(block) Release.Wait();return _bytes; }
        internal void ReturnToPool() { _returned=true;Array.Clear(_bytes); }
        public override MemoryHandle Pin(int elementIndex=0)=>throw new NotSupportedException();public override void Unpin() { }
        protected override void Dispose(bool disposing) { Release.Set();Release.Dispose(); }
    }
}
