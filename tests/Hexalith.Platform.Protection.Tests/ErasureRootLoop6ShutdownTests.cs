using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Real production role/receiver host stop waits for admitted consumers and drains genuinely disposed evidence under independent cancellation.</summary>
public sealed class ErasureRootLoop6ShutdownTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    public async Task GracefulOwningHostStopDrainsAfterLastTickAndCanceledOrInflightRequest(bool verifier, bool delayed, bool recovered)
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var original = fixture.Enroll(); var occurrence = ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity);
        var write = await fixture.Engine.ProtectEventPayloadAsync(original.Binding.Identity.Owner, Substitute.For<IEventPayload>(), original.Binding.Identity.PayloadTypeId,
            JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = original.Binding.Identity.Target, Value = "shutdown-canary" } }), "json", occurrence, token);
        await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, token); fixture.Source.Persisted.Add(original.Binding.OperationId, write.ProtectionResult.PayloadBytes);
        await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token); await fixture.Keys.CompleteDisposedUsesAsync(token);
        var sourceCustody = verifier ? new ErasureRootCustody(fixture.Store, fixture.Custody.Installation, fixture.Permissions) : fixture.Custody;
        var source = new ErasureRootNativeOccurrenceSource(sourceCustody, fixture.Permissions, native, fixture.Digests);
        using var receiver = new ReceivingAuthorityFixture("erc-owner");
        var arguments = ProtectionQualificationSourceCatalog.RequiredArguments().ToDictionary(pair => pair.Key,
            pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal) ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(pair.Value[6..]) } } : new JsonObject(), StringComparer.Ordinal);
        var recipes = ProtectionQualificationSourceCatalog.Compile(arguments);
        var scope = new ProtectionQualificationServiceScope("enrollment", "shutdown-role", "tenant-a", new string('a', 64), new("installation", "proof", new()), recipes,
            recipes.Keys.ToDictionary(id => id, id => new ProtectionQualificationObservationRecipe("tenant-a", new(), new()) { CaseId = id }));
        var service = new ProtectionQualificationRoleService(scope, new(fixture.Custody, fixture.Permissions), null, null, native, signer);
        string variable = "ERC_SHUTDOWN_ROLE_" + Guid.NewGuid().ToString("N").ToUpperInvariant(); Environment.SetEnvironmentVariable(variable, "synthetic-private-role");
        using var worker = new ErasureRootHolderCompletionWorker(fixture.Keys, fixture.Custody);
        var services = new ServiceCollection(); services.AddSingleton<IHostedService>(worker); await using var provider = services.BuildServiceProvider();
        await using var app = verifier ? ErasureRootAuthorityApplication.BuildApplication(receiver.Receiver, source)
            : ProtectionQualificationServerExecutable.BuildApplication(provider, service, "env:" + variable);
        app.Urls.Add("http://127.0.0.1:0");
        try
        {
            await app.StartAsync(token);
            // No timer tick follows the last use. Final completion must belong to StopAsync.
            fixture.Permissions.DeniedOperations.Add("CompleteRootHolder");
            IDisposable? live = null;
            if (verifier)
            {
                var record = (await fixture.Registry.LookupAsync(original.Binding.Identity, token)).Record!;
                (await source.VerifySealedAsync(record, record.Sealed!, token)).ShouldBeTrue();
            }
            else
            {
                live = (await fixture.Keys.ResolveRootAsync(original.Binding.Identity, original.Binding.KeyReference, false, token))!;
                live.ShouldNotBeNull();
                if (!delayed) { live.Dispose(); }
            }
            var recovery = Task.Run(async () =>
            {
                await Task.Delay(delayed ? 400 : 100, token);
                live?.Dispose(); if (recovered) { fixture.Permissions.DeniedOperations.Remove("CompleteRootHolder"); }
            }, token);
            var elapsed = Stopwatch.StartNew();
            // The host token is independently canceled; a completion still gets its bounded final budget.
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            await app.StopAsync(canceled.Token); await recovery;
            if (delayed && !verifier) { elapsed.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(350); }
            if (recovered)
            {
                (await fixture.Store.HeadAsync("tenant-a")).Targets.Values.Single().Holders.ShouldBeEmpty();
                fixture.Keys.HasPendingUses.ShouldBeFalse();
            }
            else
            {
                elapsed.Elapsed.ShouldBeGreaterThan(TimeSpan.FromSeconds(9)); elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(15));
                (await fixture.Store.HeadAsync("tenant-a")).Targets.Values.Single().Holders.ShouldNotBeEmpty();
            }
            (await sourceCustody.UnwrapAsync((await fixture.Store.HeadAsync("tenant-a")).Targets.Values.Single().Registration, "late-acquisition", token)).ShouldBeNull();
            var second = new ErasureRootCustody(fixture.Store, fixture.Custody.Installation, fixture.Permissions);
            var target = original.Binding.Identity.Target;
            var request = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "request", "seal", "accepted", 0, "after-graceful-stop", DeletionBatchCapabilityIdentity.TargetManifestDigest([target]), "guard", 1, 1, 1, "key"), "component", 1, "dispatch", 2, [target]);
            (await second.ConsumeAsync(request, "original-reservation", token)).State.ShouldBe(recovered ? DeletionManifestProviderState.Consumed : DeletionManifestProviderState.Unknown);
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }
}
