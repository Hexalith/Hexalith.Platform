using Hexalith.EventStore.PayloadProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class Fr34AuthorityHostingTests
{
    [Fact]
    public async Task ActualOwningHostAutomaticallyRetriesDisposedNativeUseUntilIndependentHolderOriginalCloses()
    {
        using var fixture=new CustodyOccurrenceProviderTests.Fixture();using var receiver=new ReceivingAuthorityFixture("fr34-engine");var token=TestContext.Current.CancellationToken;
        var keys=fixture.Provider();
        (await keys.AcquireEncryptionLeaseAsync(fixture.Occurrence,token)).ShouldBeTrue();
        var root=await keys.ResolveRootAsync(fixture.Identity,"reference",true,token);root.ShouldNotBeNull();fixture.MaterialReleases.ShouldBe(1);
        var bindings=new InteractionProtectionBindingProvider(fixture.Source);
        var production=new Fr34ProductionPorts(new(fixture.Registry,keys,bindings,TimeProvider.System),bindings,keys,fixture.Source);
        await using var host=Fr34AuthorityApplication.BuildOwningApplication(receiver.Receiver,production);host.Urls.Add("http://127.0.0.1:0");
        try
        {
            await host.StartAsync(token);host.Services.GetServices<IHostedService>().ShouldContain(value=>value is CustodyOccurrenceHolderCompletionWorker);
            await Task.Delay(1100,token);fixture.CompletionCalls.ShouldBe(0);
            root.Dispose();fixture.Root.ShouldAllBe(value=>value==0);
            await Until(()=>fixture.CompletionCalls>=1,token);fixture.IndependentCompletion.ShouldBeFalse();
            int withheld=fixture.CompletionCalls;await Until(()=>fixture.CompletionCalls>withheld,token); // Unknown evidence stays open and is retried.
            fixture.IndependentCompletion=true;int before=fixture.CompletionCalls;await Until(()=>fixture.CompletionCalls>before,token);
            int closed=fixture.CompletionCalls;await Task.Delay(1200,token);fixture.CompletionCalls.ShouldBe(closed);
            (await keys.ResolveRootAsync(fixture.Identity,"reference",true,token)).ShouldBeNull();fixture.MaterialReleases.ShouldBe(1);
        }
        finally { root.Dispose();await host.StopAsync(token); }
    }
    private static async Task Until(Func<bool> complete,CancellationToken token)
    {
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(token);budget.CancelAfter(TimeSpan.FromSeconds(6));
        while(!complete()) await Task.Delay(20,budget.Token);
    }
}
