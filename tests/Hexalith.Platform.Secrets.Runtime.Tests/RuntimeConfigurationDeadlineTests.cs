using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class RuntimeConfigurationDeadlineTests
{
    [Fact]
    public async Task SynchronousNoCallerLoaderMapsItsOwnRealAcquisitionTimeoutToUnavailableAndRecovers()
    {
        string path=Path.Combine(Path.GetTempPath(),"configuration-deadline-"+Guid.NewGuid().ToString("N"));
        using var release=new ManualResetEventSlim();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await File.WriteAllTextAsync(path,"{\"runtime\":{\"providerBinding\":\"fixture-provider\"}}",TestContext.Current.CancellationToken);
            async Task<SecretsRuntimeConfiguration?> Acquire(string value)
            {
                byte[] bytes=await BoundedRegularFile.ReadWithAcquisitionAsync(value,1024*1024,CancellationToken.None,TimeSpan.FromMilliseconds(50),()=> { entered.TrySetResult();try { release.Wait(); }finally { finished.TrySetResult(); } });
                return SecretsRuntimeConfiguration.Parse(System.Text.Encoding.UTF8.GetString(bytes));
            }
            var result=await Task.Run(()=>SecretsRuntimeConfiguration.Load(path,Acquire),TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken);
            result.ShouldBeNull();entered.Task.IsCompleted.ShouldBeTrue();finished.Task.IsCompleted.ShouldBeFalse();release.Set();await finished.Task.WaitAsync(TimeSpan.FromSeconds(2),TestContext.Current.CancellationToken);
            SecretsRuntimeConfiguration.Load(path)!.ProviderBinding.ShouldBe("fixture-provider");
            File.Delete(path);SecretsRuntimeConfiguration.Load(path).ShouldBeNull();
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(()=>BoundedRegularFile.UseUtf8Async(path,1024*1024,SecretsRuntimeConfiguration.Parse,cancelled.Token));
        }
        finally { release.Set();File.Delete(path); }
    }
}
