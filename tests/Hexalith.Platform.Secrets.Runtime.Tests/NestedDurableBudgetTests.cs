using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class NestedDurableBudgetTests
{
    [Theory]
    [InlineData(false,false)][InlineData(true,false)][InlineData(false,true)][InlineData(true,true)]
    public async Task NestedDefaultAndShorterCallerTokensCancelCooperativeWorkWithoutReleasingOwnershipEarly(bool input,bool shorter)
    {
        using var outer=new CancellationTokenSource();using var inner=new CancellationTokenSource();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work=BoundedDurableFileOperation.RunAsync(async _=>
        {
            async Task<int> Wait(CancellationToken token)
            {
                token.CanBeCanceled.ShouldBeTrue();entered.SetResult();
                try { await Task.Delay(Timeout.Infinite,token);return 1; }
                finally { finished.SetResult(); }
            }
            return input?await BoundedDurableFileOperation.RunWithInputAsync(new byte[]{1},(_,token)=>Wait(token),shorter?inner.Token:default)
                :await BoundedDurableFileOperation.RunAsync(Wait,shorter?inner.Token:default);
        },outer.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken);
        if(shorter) inner.Cancel();else outer.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(()=>work.WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken));
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken);
        outer.IsCancellationRequested.ShouldBe(!shorter);
    }
    [Fact]
    public async Task ActualNestedArchiveLeaseWaitUsesOwningBudget()
    {
        string root=Path.Combine(Path.GetTempPath(),"nested-durable-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string path=Path.Combine(root,"original");using var cancel=new CancellationTokenSource();
        string lockPath=Path.Combine(root,".publish-"+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("original"))));
        try
        {
            using(var held=await ArchiveFileLease.AcquireAsync(lockPath,TestContext.Current.CancellationToken))
            {
                var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var work=BoundedDurableFileOperation.RunAsync(async _=> { entered.SetResult();await ImmutableFileArchive.PublishAsync(path,new byte[]{1},default);return true; },cancel.Token);
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken);cancel.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(()=>work.WaitAsync(TimeSpan.FromSeconds(3),TestContext.Current.CancellationToken));File.Exists(path).ShouldBeFalse();
            }
            await ImmutableFileArchive.PublishAsync(path,new byte[]{1},TestContext.Current.CancellationToken);
        }
        finally { Directory.Delete(root,true); }
    }
}
