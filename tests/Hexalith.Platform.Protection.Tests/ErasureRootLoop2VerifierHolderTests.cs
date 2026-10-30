using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual native sealed verifier cancellation retains its disposed issued root until the idle production worker completes it.</summary>
public sealed class ErasureRootLoop2VerifierHolderTests
{
    [Fact]
    public async Task NativeVerifierCancellationAndCompletionOutageRetainExactHolderUntilIdleHostCanRetireIt()
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        var original = fixture.Enroll();
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = original.Binding.Identity.Target, Value = "native-verifier-canary" } });
        var write = await fixture.Engine.ProtectEventPayloadAsync(original.Binding.Identity.Owner, Substitute.For<IEventPayload>(), original.Binding.Identity.PayloadTypeId, payload, "json", ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), token);
        await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, token);
        fixture.Source.Persisted.Add(original.Binding.OperationId, write.ProtectionResult.PayloadBytes);
        await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token);
        await fixture.Keys.CompleteDisposedUsesAsync(token);
        var record = (await fixture.Registry.LookupAsync(original.Binding.Identity, token)).Record!;
        var sourceCustody = new ErasureRootCustody(fixture.Store, new("erc-test", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant-a"] = "epoch" }), fixture.Permissions);
        var source = new ErasureRootNativeOccurrenceSource(sourceCustody, fixture.Permissions, native, fixture.Digests);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token); int reads = 0;
        fixture.Store.ReadObserved = () => { if (++reads == 4) { cancellation.Cancel(); } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.VerifySealedAsync(record, record.Sealed!, cancellation.Token));
        fixture.Store.ReadObserved = null;
        var root = (await fixture.Store.HeadAsync("tenant-a")).Targets.Values.Single(); root.Holders.Length.ShouldBe(1);
        string holder = root.Holders[0];
        fixture.Permissions.Allowed = false; (await source.CompleteDisposedVerifierHoldersAsync(token)).ShouldBe(0);
        fixture.Permissions.Allowed = true; fixture.Store.Failure = "read"; (await source.CompleteDisposedVerifierHoldersAsync(token)).ShouldBe(0);
        (await fixture.Custody.CompleteDisposedHoldersAsync(token)).ShouldBe(0);
        using var receiver = new ReceivingAuthorityFixture("erc-owner");
        await using var app = ErasureRootAuthorityApplication.BuildApplication(receiver.Receiver, source);
        app.Services.GetServices<IHostedService>().OfType<ErasureRootVerifierHolderCompletionWorker>().Count().ShouldBe(1);
        app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync(token);
        try
        {
            for (int attempt = 0; attempt < 30 && (await fixture.Store.HeadAsync("tenant-a")).Targets.Values.Single().Holders.Length > 0; attempt++) { await Task.Delay(100, token); }
            (await fixture.Store.HeadAsync("tenant-a")).Targets.Values.Single().Holders.ShouldBeEmpty();
            (await fixture.Custody.UnwrapAsync(root.Registration, holder, token)).ShouldBeNull();
            var second = new ErasureRootCustody(fixture.Store, new("erc-test", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant-a"] = "epoch" }), fixture.Permissions);
            ProtectionTarget[] targets = [original.Binding.Identity.Target];
            var request = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "request", "seal", "accepted", 0, "batch", DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key"), "component", 2, "dispatch", 3, targets);
            (await second.ConsumeAsync(request, "reservation", token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        }
        finally { await app.StopAsync(token); }
    }
}
