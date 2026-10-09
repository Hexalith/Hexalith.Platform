using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual private hosted loop drains serialized spool source through exact synthetic receiver receipts; no live replica/credential qualification.</summary>
public sealed class ReplicatedSecurityObservationSpoolWorkerTests
{
    /// <summary>Lost receiver acknowledgement resolves its original durable receipt once; repeated ticks and worker restart preserve acknowledged source state without another physical append.</summary>
    [Fact]
    public async Task PrivateWorkerRecoversOriginalReceiptAndRestartDoesNotAppendAgain()
    {
        var fixture = new SecuritySpoolFixture { LoseAppendAcknowledgement = true };
        await fixture.Spool.ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken);
        var services = new ServiceCollection(); services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddPrivateSecurityObservationDrainWorker(fixture.Spool, TimeSpan.FromMilliseconds(10), 1);
        using var provider = services.BuildServiceProvider(); provider.GetService<ReplicatedSecurityObservationSpool>().ShouldBeNull();
        var worker = provider.GetServices<IHostedService>().Single(); await worker.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            DateTime until = DateTime.UtcNow.AddSeconds(5);
            while (fixture.Read()!.Records.Single().Receipt is null && DateTime.UtcNow < until) { await Task.Delay(10, TestContext.Current.CancellationToken); }
            fixture.Read()!.Records.Single().Receipt.ShouldNotBeNull(); fixture.PhysicalAppends.ShouldBe(1);
        }
        finally { await worker.StopAsync(TestContext.Current.CancellationToken); }
        using var restarted = new ReplicatedSecurityObservationSpoolWorker(fixture.Spool, TimeProvider.System, TimeSpan.FromMilliseconds(10), 1);
        var tick = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Authority.ValidateStateAsync(fixture.Target, Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        { bool exact = call.ArgAt<long>(1) == fixture.Anchor && call.ArgAt<string>(2) == fixture.AnchorDigest; if (exact) { tick.TrySetResult(); } return exact; });
        await restarted.StartAsync(TestContext.Current.CancellationToken);
        try { await tick.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); }
        finally { await restarted.StopAsync(TestContext.Current.CancellationToken); }
        fixture.PhysicalAppends.ShouldBe(1); (await fixture.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    /// <summary>Ordinary custody composition exposes neither hosted drain worker nor raw spool capabilities.</summary>
    [Fact]
    public void OrdinaryCompositionDoesNotInstallPrivateDrainWorker()
    {
        using var provider = new ServiceCollection().AddPlatformCustody().BuildServiceProvider();
        provider.GetServices<IHostedService>().ShouldBeEmpty(); provider.GetService<ReplicatedSecurityObservationSpool>().ShouldBeNull();
    }

    /// <summary>Unknown receiver proof never appends or acknowledges and retains pending readiness; a later exact original proof allows the same worker to acknowledge without physical retry.</summary>
    [Fact]
    public async Task UnknownProofRetainsPendingUntilExactOriginalReceiptAppears()
    {
        var fixture = new SecuritySpoolFixture(); var record = (await fixture.Spool.ObserveAsync(SecuritySpoolFixture.Intent(), TestContext.Current.CancellationToken))!;
        var unknown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); bool exact = false;
        fixture.Recorder.LookupAsync(Arg.Any<SecurityObservationRecord>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            unknown.TrySetResult(); return exact ? new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.Recorded, SecuritySpoolFixture.Receipt(call.Arg<SecurityObservationRecord>()))
                : new SecurityEventRecorderLookup(SecurityEventRecorderLookupState.Unknown);
        });
        using var worker = new ReplicatedSecurityObservationSpoolWorker(fixture.Spool, TimeProvider.System, TimeSpan.FromMilliseconds(10), 1);
        await worker.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await unknown.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            fixture.Read()!.Records.Single().ShouldBe(record); (await fixture.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse(); fixture.PhysicalAppends.ShouldBe(0);
            exact = true; DateTime until = DateTime.UtcNow.AddSeconds(5);
            while (fixture.Read()!.Records.Single().Receipt is null && DateTime.UtcNow < until) { await Task.Delay(10, TestContext.Current.CancellationToken); }
            fixture.Read()!.Records.Single().Receipt.ShouldBe(SecuritySpoolFixture.Receipt(record)); fixture.PhysicalAppends.ShouldBe(0);
        }
        finally { await worker.StopAsync(TestContext.Current.CancellationToken); }
        (await fixture.Spool.IsReadyAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
    }
}
