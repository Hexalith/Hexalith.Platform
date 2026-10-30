using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Events;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Idle owning-worker backlog diagnostics follow actual restrictive disposed buffers and recover without logging private material.</summary>
public sealed class ErasureRootLoop4CompletionTests
{
    [Fact]
    public async Task LostCompletionAcknowledgementFollowedBySecondOwnerEraseClearsExactDisposedQueue()
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture();
        var root = (await fixture.Custody.WrapAsync(new("tenant-a", "completed-root", Hexalith.Platform.Custody.PlatformKeyPurpose.InteractionRootDek, "alias", "root-v1", "lifecycle", "erc-test", "custody"), "wrap-completed-root", 0, token))!;
        using var holder = (await fixture.Custody.UnwrapAsync(root, "original-live-holder", token))!;
        holder.Dispose(); fixture.Custody.RetainDisposedHolder(root, holder); fixture.Store.Failure = "cas-after";
        (await fixture.Custody.CompleteDisposedHoldersAsync(token)).ShouldBe(0); fixture.Custody.CompletionDiagnostics.Read().OutstandingCount.ShouldBe(1);
        var target = new Hexalith.EventStore.Contracts.Security.ProtectionTarget(root.Identity.TenantId, root.Identity.ObjectId, root.Identity.KeyAlias);
        var request = new Hexalith.EventStore.Contracts.Security.DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "request", "seal", "AcceptedSet", 0, "batch", Hexalith.EventStore.Contracts.Security.DeletionBatchCapabilityIdentity.TargetManifestDigest([target]), "guard", 1, 1, 1, "key"), "component", 1, "dispatch", 2, [target]);
        var second = new ErasureRootCustody(fixture.Store, fixture.Custody.Installation, fixture.Permissions);
        (await second.ConsumeAsync(request, "original-reservation", token)).State.ShouldBe(Hexalith.EventStore.Contracts.Security.DeletionManifestProviderState.Consumed);
        (await fixture.Custody.CompleteDisposedHoldersAsync(token)).ShouldBe(1); fixture.Custody.CompletionDiagnostics.Read().OutstandingCount.ShouldBe(0);
        using var counterfeit = new OwnedErasureRootKey(new byte[32], holder.HolderId); counterfeit.Dispose();
        (await fixture.Custody.CompleteHolderAsync(root, counterfeit, token)).ShouldBeFalse(); fixture.Store.TrimCount.ShouldBe(1);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwningWorkerReportsActualDisposedBacklogAndClearsAfterPermissionRecovery(bool verifier)
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        var logger = Substitute.For<ILogger<ErasureRootHolderCompletionWorker>>();
        var verifierLogger = Substitute.For<ILogger<ErasureRootVerifierHolderCompletionWorker>>();
        string[] Logs() => logger.ReceivedCalls().Concat(verifierLogger.ReceivedCalls()).Where(call => call.GetMethodInfo().Name == "Log")
            .Select(call => call.GetArguments()[2]?.ToString() ?? "").ToArray();
        var enrollment = fixture.Enroll();
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = enrollment.Binding.Identity.Target, Value = "private-payload-never-log" } });
        fixture.Permissions.DeniedOperations.Add("CompleteRootHolder");
        _ = await fixture.Engine.ProtectEventPayloadAsync(enrollment.Binding.Identity.Owner, Substitute.For<IEventPayload>(), enrollment.Binding.Identity.PayloadTypeId, payload, "json", ErasureRootOccurrenceBindings.Occurrence(enrollment.Binding.Identity), token);
        if (verifier)
        {
            // A source receiver owns a separate issued verifier holder and completion queue.
            var root = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value.Head.Targets.Values.Single().Registration;
            using var holder = (await fixture.Custody.UnwrapAsync(root, "private-holder-never-log", token))!;
            holder.Dispose(); fixture.Custody.RetainDisposedHolder(root, holder);
        }
        var source = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, native, fixture.Digests);
        using Microsoft.Extensions.Hosting.BackgroundService worker = verifier ? new ErasureRootVerifierHolderCompletionWorker(source, verifierLogger)
            : new ErasureRootHolderCompletionWorker(fixture.Keys, fixture.Custody, logger);
        await worker.StartAsync(token);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(10));
            var diagnostics = verifier ? fixture.Custody.CompletionDiagnostics : fixture.Keys.CompletionDiagnostics;
            while (diagnostics.Read().FailureClass == "None") { await Task.Delay(25, deadline.Token); }
            diagnostics.Read().OutstandingCount.ShouldBeGreaterThan(0); diagnostics.Read().OldestPendingSeconds.ShouldBeGreaterThanOrEqualTo(0);
            await Task.Delay(600, deadline.Token);
            var pendingLogs = Logs(); pendingLogs.Length.ShouldBe(1); pendingLogs[0].ShouldContain("OutstandingCount=1"); pendingLogs[0].ShouldContain("FailureClass=RefusedOrUnavailable"); pendingLogs[0].ShouldNotContain("private-"); pendingLogs[0].ShouldNotContain("tenant-a");
            fixture.Permissions.DeniedOperations.Clear();
            while (diagnostics.Read().OutstandingCount != 0) { await Task.Delay(25, deadline.Token); }
            diagnostics.Read().FailureClass.ShouldBe("None");
            await Task.Delay(300, deadline.Token); var completedLogs = Logs(); completedLogs.Length.ShouldBe(2); completedLogs[^1].ShouldContain("recovered");
        }
        finally { await worker.StopAsync(CancellationToken.None); }
    }
}
