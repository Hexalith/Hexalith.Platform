using System.Reflection;
using System.Text;
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

/// <summary>Actual whole-snapshot engine/custody/manager replay for closed descriptors and the former opaque V2 writer.</summary>
public sealed class ErasureRootLoop6SnapshotTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task EmptyAndArbitraryOpaqueV2SnapshotsReplayTypedErasedThroughConcreteManager(bool empty, bool legacy, bool permanentLoss)
        => await ExerciseAsync(empty ? "{}" : "{\"ConfidentialSibling\":\"loop6-private-opaque-canary\",\"Arbitrary\":{\"Nested\":[1,2,3]}}", legacy, permanentLoss, false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WholeSnapshotAuxiliaryOriginalsNeverRetainEncryptedConfidentialSiblings(bool permanentLoss)
        => await ExerciseAsync("envelope", false, permanentLoss, true);

    private static async Task ExerciseAsync(string input, bool legacy, bool permanentLoss, bool envelope)
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture();
        var seed = fixture.Enroll(); var parent = new PayloadProtectionOccurrenceContext(1, PayloadProtectionPayloadKind.Snapshot, "hx-snapshot-v1:folded-state");
        var original = seed with { Binding = seed.Binding with { Identity = seed.Binding.Identity with { Kind = PayloadProtectionPayloadKind.Snapshot, PayloadTypeId = parent.PayloadTypeId } } };
        fixture.Source.Enrollments.Add(ErasureRootOccurrenceBindings.Id(original.Binding.Identity.Owner, parent), original);
        var state = envelope ? JsonSerializer.SerializeToElement(new { ConfidentialSibling = "loop6-private-sibling-canary", ProtectedContent = new { Target = original.Binding.Identity.Target, Value = "loop6-private-value-canary" } })
            : JsonSerializer.Deserialize<JsonElement>(input);
        var type = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }.GetTypeInfo(typeof(JsonElement));
        SnapshotProtectionWriteResult write;
        if (legacy)
        {
            // Execute the actual unchanged V2 whole-snapshot protector with the former writer's
            // absent optional metadata. This produces genuine AEAD/HMAC/registry originals,
            // rather than clearing a committed descriptor or synthesizing old ciphertext.
            var binding = (await fixture.Bindings.ResolveAsync(original.Binding.Identity.Owner, parent, null, true, token))!;
            object protector = typeof(InteractionPayloadProtectionService).GetField("_protector", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(fixture.Engine)!;
            var task = (Task<InteractionOccurrenceSealedResult?>)protector.GetType().GetMethod("ProtectSnapshotAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(protector, [JsonSerializer.SerializeToUtf8Bytes(state), binding.Identity, binding.DigestKeyVersion, binding.ReservationAttemptOrdinal, binding.KeyReference, token, false])!;
            var sealedResult = (await task)!; sealedResult.SnapshotReplaySkeleton.ShouldBeNull();
            write = new(new(JsonSerializer.Deserialize<ProtectedSnapshotPayloadV2>(sealedResult.PayloadBytes)!, new(PayloadProtectionState.Protected, 1, "hexalith-pdenc-v2", binding.KeyReference, "application/json", null)),
                new(binding.Identity.Owner, parent, binding.OperationId, binding.KeyReference, 1, binding.LifecycleEpoch));
        }
        else { write = await fixture.Engine.ProtectSnapshotAsync(original.Binding.Identity.Owner, state, type, parent, token); }
        await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, token);
        var retained = (await fixture.Registry.LookupAsync(original.Binding.Identity, token)).Record!.Sealed!;
        fixture.Source.Persisted.Add(original.Binding.OperationId, retained.PayloadBytes);
        await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token);
        byte[] ciphertext = JsonSerializer.SerializeToUtf8Bytes(write.ProtectionResult.State);
        var readable = await fixture.Engine.TryReplaySnapshotAsync(original.Binding.Identity.Owner, write.ProtectionResult.State, write.ProtectionResult.Metadata, type, parent, token);
        readable.IsReadable.ShouldBeTrue(); ErasureRootCustody.Hash((JsonElement)readable.State!).ShouldBe(ErasureRootCustody.Hash(state));
        foreach (var bytes in new[] { retained.SnapshotReplaySkeleton ?? [], JsonSerializer.SerializeToUtf8Bytes(fixture.RegistryState.CommittedState), JsonSerializer.SerializeToUtf8Bytes(fixture.Source.SealedOriginals) })
        {
            string auxiliary = Encoding.UTF8.GetString(bytes);
            auxiliary.ShouldNotContain("loop6-private-sibling-canary"); auxiliary.ShouldNotContain("loop6-private-value-canary"); auxiliary.ShouldNotContain("loop6-private-opaque-canary");
        }
        await fixture.Keys.CompleteDisposedUsesAsync(token);
        if (permanentLoss)
        {
            fixture.Permissions.PermanentLoss = new("erc-test", "enrollment", "tenant-a", "retired-volume", "retirement", new string('A', 64));
            fixture.Store.ReadObserved = () => throw new IOException("Dead former ERC");
        }
        else
        {
            ProtectionTarget[] targets = [original.Binding.Identity.Target];
            var request = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "request", "seal", "accepted", 0, "batch", DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key"), "component", 2, "dispatch", 3, targets);
            (await fixture.Manifests.ConsumeAsync(request, "reservation", token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        }
        var erased = await fixture.Engine.TryReplaySnapshotAsync(original.Binding.Identity.Owner, write.ProtectionResult.State, write.ProtectionResult.Metadata, type, parent, token);
        erased.IsReadable.ShouldBeTrue(); var replayed = (JsonElement)erased.State!;
        if (envelope) { replayed.TryGetProperty("ConfidentialSibling", out _).ShouldBeFalse(); replayed = replayed.GetProperty("ProtectedContent").GetProperty("Value"); }
        replayed.GetProperty("State").GetString().ShouldBe("Erased");
        (await fixture.Engine.TryUnprotectSnapshotAsync(original.Binding.Identity.Owner, write.ProtectionResult.State, write.ProtectionResult.Metadata, type, parent, token)).IsUnreadable.ShouldBeTrue();
        JsonSerializer.SerializeToUtf8Bytes(write.ProtectionResult.State).ShouldBe(ciphertext);
        var snapshot = new SnapshotRecord(1, write.ProtectionResult.State, DateTimeOffset.UtcNow, original.Binding.Identity.Owner.Domain, original.Binding.Identity.Owner.AggregateId, "tenant-a", write.ProtectionResult.Metadata) { SnapshotEnvelopeVersion = 1 };
        var native = Substitute.For<IActorStateManager>(); native.TryGetStateAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new ConditionalValue<object>(true, JsonSerializer.SerializeToElement(snapshot, JsonSerializerOptions.Web)));
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), Substitute.For<ILogger<SnapshotManager>>(), fixture.Engine);
        var loaded = await manager.LoadSnapshotAsync(original.Binding.Identity.Owner, native, cancellationToken: token); loaded.ShouldNotBeNull(); JsonSerializer.Serialize(loaded.State).ShouldContain("Erased");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => fixture.Engine.TryReplaySnapshotAsync(original.Binding.Identity.Owner, write.ProtectionResult.State, write.ProtectionResult.Metadata, type, parent, canceled.Token));
    }
}
