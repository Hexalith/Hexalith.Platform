using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Genuine single-root snapshot trusted replay and independent-first permanent loss with a dead former ERC.</summary>
public sealed class ErasureRootLoop2SnapshotTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SingleRootSnapshotReplaysTypedErasedThroughConcreteManagerWithoutChangingCiphertext(bool permanentLoss)
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture();
        var seed = fixture.Enroll(); var parent = new PayloadProtectionOccurrenceContext(1, PayloadProtectionPayloadKind.Snapshot, "hx-snapshot-v1:folded-state");
        var enrollment = seed with { Binding = seed.Binding with { Identity = seed.Binding.Identity with { Kind = PayloadProtectionPayloadKind.Snapshot, PayloadTypeId = parent.PayloadTypeId } } };
        fixture.Source.Enrollments.Add(ErasureRootOccurrenceBindings.Id(enrollment.Binding.Identity.Owner, parent), enrollment);
        var state = JsonSerializer.SerializeToElement(new { Safe = 9, ProtectedContent = new { Target = enrollment.Binding.Identity.Target, Value = new { Secret = "private" } } });
        var type = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }.GetTypeInfo(typeof(JsonElement));
        var write = await fixture.Engine.ProtectSnapshotAsync(enrollment.Binding.Identity.Owner, state, type, parent, token);
        await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, token);
        var sealedOriginal = (await fixture.Registry.LookupAsync(enrollment.Binding.Identity, token)).Record!.Sealed!;
        fixture.Source.Persisted.Add(enrollment.Binding.OperationId, sealedOriginal.PayloadBytes);
        await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token);
        byte[] ciphertext = JsonSerializer.SerializeToUtf8Bytes(write.ProtectionResult.State);
        if (permanentLoss)
        {
            fixture.Permissions.PermanentLoss = new("erc-test", "enrollment", "tenant-a", "retired-volume", "retirement", new string('A', 64));
            fixture.Store.Failure = "read";
        }
        else
        {
            ProtectionTarget[] targets = [enrollment.Binding.Identity.Target];
            var request = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "request", "seal", "accepted", 0, "batch", DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key"), "component", 2, "dispatch", 3, targets);
            (await fixture.Manifests.ConsumeAsync(request, "reservation", token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        }
        var replay = await fixture.Engine.TryReplaySnapshotAsync(enrollment.Binding.Identity.Owner, write.ProtectionResult.State, write.ProtectionResult.Metadata, type, parent, token);
        replay.IsReadable.ShouldBeTrue(); var restored = (JsonElement)replay.State!;
        restored.GetProperty("Safe").GetInt32().ShouldBe(9);
        restored.GetProperty("ProtectedContent").GetProperty("Value").GetProperty("State").GetString().ShouldBe("Erased");
        JsonSerializer.SerializeToUtf8Bytes(write.ProtectionResult.State).ShouldBe(ciphertext);
        (await fixture.Engine.TryUnprotectSnapshotAsync(enrollment.Binding.Identity.Owner, write.ProtectionResult.State, write.ProtectionResult.Metadata, type, parent, token)).IsUnreadable.ShouldBeTrue();
        if (permanentLoss) { fixture.Store.Failure = "read"; }
        var snapshot = new SnapshotRecord(1, write.ProtectionResult.State, DateTimeOffset.UtcNow, enrollment.Binding.Identity.Owner.Domain, enrollment.Binding.Identity.Owner.AggregateId, "tenant-a", write.ProtectionResult.Metadata) { SnapshotEnvelopeVersion = 1 };
        var actorState = Substitute.For<IActorStateManager>();
        actorState.TryGetStateAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new ConditionalValue<object>(true, JsonSerializer.SerializeToElement(snapshot, JsonSerializerOptions.Web)));
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), Substitute.For<ILogger<SnapshotManager>>(), fixture.Engine);
        var loaded = await manager.LoadSnapshotAsync(enrollment.Binding.Identity.Owner, actorState, cancellationToken: token);
        loaded.ShouldNotBeNull(); ((JsonElement)loaded.State).GetProperty("ProtectedContent").GetProperty("Value").GetProperty("State").GetString().ShouldBe("Erased");
        if (permanentLoss)
        {
            fixture.Source.Persisted[enrollment.Binding.OperationId] = "substituted"u8.ToArray(); fixture.Store.Failure = "read";
            (await fixture.Engine.TryReplaySnapshotAsync(enrollment.Binding.Identity.Owner, write.ProtectionResult.State, write.ProtectionResult.Metadata, type, parent, token)).IsUnreadable.ShouldBeTrue();
        }
    }
}
