using Dapr.Actors.Runtime;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual engine/ERC mixed-root snapshot, sealed cache, independent source geometry and original-format compatibility component coverage.</summary>
public sealed class ErasureRootMixedSnapshotTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static readonly JsonTypeInfo Type = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }.GetTypeInfo(typeof(JsonElement));
    private static JsonElement State() => JsonSerializer.SerializeToElement(new
    {
        Safe = 7,
        Entries = new[] { "a", "b" }.Select(id => new { ProtectedContent = new { Target = new ProtectionTarget("tenant-a", "interaction-" + id, "alias-" + id), Value = new { Secret = "secret-" + id } } }).ToArray(),
    });
    private static (ErasureRootOccurrenceEnrollment[] Originals, PayloadProtectionOccurrenceContext Parent) Enroll(ErasureRootEngineFixture fixture, JsonElement state, string snapshotType = "hx-snapshot-v1:directory-state")
    {
        var parent = new PayloadProtectionOccurrenceContext(77, PayloadProtectionPayloadKind.Snapshot, snapshotType);
        var layout = InteractionSnapshotEntries.Describe(JsonSerializer.SerializeToUtf8Bytes(state), "tenant-a", Token);
        var result = new List<ErasureRootOccurrenceEnrollment>();
        for (int i = 0; i < layout.Entries.Count; i++)
        {
            var entry = layout.Entries[i]; string reference = i == 0 ? "01ARZ3NDEKTSV4RRFFQ69G5FAV" : "01ARZ3NDEKTSV4RRFFQ69G5FAW";
            var seed = fixture.Enroll("directory", reference, checked((ulong)(10 + i)));
            var child = InteractionSnapshotEntries.EntryOccurrence(parent, entry.Path);
            var identity = seed.Binding.Identity with { Target = entry.Target, Kind = PayloadProtectionPayloadKind.Snapshot, Sequence = parent.RecordSequence, PayloadTypeId = child.PayloadTypeId };
            var enrolled = seed with { Binding = seed.Binding with { Identity = identity }, RootIdentity = seed.RootIdentity with { ObjectId = entry.Target.AgentInteractionId, KeyAlias = entry.Target.TargetProtectionKeyAlias },
                SnapshotEntry = new(parent, entry.Path, layout.ContainerDigest, layout.Entries.Count) };
            fixture.Source.Enrollments.Add(ErasureRootOccurrenceBindings.Id(identity.Owner, child), enrolled); result.Add(enrolled);
        }
        return (result.ToArray(), parent);
    }
    private static async Task<SnapshotProtectionWriteResult> Seal(ErasureRootEngineFixture fixture, JsonElement state,
        ErasureRootOccurrenceEnrollment[] originals, PayloadProtectionOccurrenceContext parent)
    {
        var write = await fixture.Engine.ProtectSnapshotAsync(originals[0].Binding.Identity.Owner, state, Type, parent, Token);
        int before = fixture.Store.CasCount;
        await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, Token);
        (fixture.Store.CasCount - before).ShouldBe(1);
        foreach (var original in originals)
        {
            var retained = (await fixture.Registry.LookupAsync(original.Binding.Identity, Token)).Record!;
            fixture.Source.Persisted.Add(original.Binding.OperationId, retained.Sealed!.PayloadBytes.ToArray());
        }
        await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, Token);
        return write;
    }

    [Theory]
    [InlineData("version")]
    [InlineData("content-hint")]
    [InlineData("missing-flags")]
    [InlineData("empty-flags")]
    [InlineData("unknown-layout")]
    [InlineData("extra-flag")]
    public async Task UnsupportedCompleteMixedMetadataRefusesBothPublicApisBeforeAnyCustodyRead(string field)
    {
        using var fixture = new ErasureRootEngineFixture(); var state = State(); var (originals, parent) = Enroll(fixture, state);
        var write = await Seal(fixture, state, originals, parent); var metadata = write.ProtectionResult.Metadata!;
        metadata = field switch
        {
            "version" => metadata with { MetadataVersion = 2 }, "content-hint" => metadata with { ContentHint = null },
            "missing-flags" => metadata with { CompatibilityFlags = null }, "empty-flags" => metadata with { CompatibilityFlags = new Dictionary<string, string>() },
            "unknown-layout" => metadata with { CompatibilityFlags = new Dictionary<string, string> { ["snapshot-layout"] = "entries-v3" } },
            _ => metadata with { CompatibilityFlags = new Dictionary<string, string> { ["snapshot-layout"] = "entries-v2", ["extra"] = "unsupported" } },
        };
        int before = fixture.Store.ArchiveReads, cas = fixture.Store.CasCount;
        (await fixture.Engine.TryUnprotectSnapshotAsync(originals[0].Binding.Identity.Owner, write.ProtectionResult.State, metadata, Type, parent, Token)).IsUnreadable.ShouldBeTrue();
        (await fixture.Engine.TryReplaySnapshotAsync(originals[0].Binding.Identity.Owner, write.ProtectionResult.State, metadata, Type, parent, Token)).IsUnreadable.ShouldBeTrue();
        fixture.Store.ArchiveReads.ShouldBe(before); fixture.Store.CasCount.ShouldBe(cas);
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("store")]
    [InlineData("lifecycle")]
    [InlineData("purpose")]
    public async Task MalformedLaterEnrollmentRefusesBeforeAnyFirstEntryRootOrRegistryEffect(string corruption)
    {
        using var fixture = new ErasureRootEngineFixture(); var state = State(); var (originals, parent) = Enroll(fixture, state);
        var second = originals[1];
        var malformed = corruption switch
        {
            "store" => second with { RootIdentity = second.RootIdentity with { StoreTarget = "foreign-store" } },
            "lifecycle" => second with { ExpectedLifecycleRevision = -1 },
            "purpose" => second with { RootIdentity = second.RootIdentity with { Purpose = Hexalith.Platform.Custody.PlatformKeyPurpose.DigestKey } },
            _ => second with { Binding = second.Binding with { KeyReference = "not-a-canonical-reference" } },
        };
        fixture.Source.Enrollments[ErasureRootOccurrenceBindings.Id(second.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(second.Binding.Identity))] = malformed;
        byte[] before = (await fixture.Store.ReadHeadAsync("tenant-a", Token))!.Bytes;
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Engine.ProtectSnapshotAsync(originals[0].Binding.Identity.Owner, state, Type, parent, Token));
        (await fixture.Store.ReadHeadAsync("tenant-a", Token))!.Bytes.ShouldBe(before);
        fixture.RegistryState.CommittedState.ShouldBeEmpty(); (await fixture.Store.HeadAsync("tenant-a")).Targets.ShouldBeEmpty();
    }

    [Fact]
    public async Task DirectorySnapshotAndCacheKeepEachEntryUnderItsOwnRootAndReplayPartialErasure()
    {
        using var fixture = new ErasureRootEngineFixture(); var state = State(); var (originals, parent) = Enroll(fixture, state);
        var write = await Seal(fixture, state, originals, parent);
        var carrier = write.ProtectionResult.State.ShouldBeOfType<ProtectedSnapshotEntriesV2>(); carrier.Entries.Count.ShouldBe(2);
        byte[] cached = JsonSerializer.SerializeToUtf8Bytes(carrier);
        Encoding.UTF8.GetString(cached).ShouldNotContain("secret-a"); Encoding.UTF8.GetString(cached).ShouldNotContain("secret-b");
        foreach (var entry in carrier.Entries) { entry.Protected.Format.ShouldBe("json+pdenc-v2"); }
        var before = await fixture.Engine.TryUnprotectSnapshotAsync(originals[0].Binding.Identity.Owner, carrier, write.ProtectionResult.Metadata, Type, parent, Token);
        before.IsReadable.ShouldBeTrue(); JsonNode.DeepEquals(JsonSerializer.SerializeToNode(before.State), JsonSerializer.SerializeToNode(state)).ShouldBeTrue();
        var root = (await fixture.Store.HeadAsync("tenant-a")).Targets.Values.Single(value => value.Registration.Identity.ObjectId == "interaction-a");
        var targets = new[] { originals[0].Binding.Identity.Target };
        var request = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "deletion", "seal", "accepted", 0, "erase-a",
            DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key"), "component-signed-fixture", 1, "dispatch", 2, targets);
        (await fixture.Manifests.ConsumeAsync(request, "reservation-a", Token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        (await fixture.Engine.TryUnprotectSnapshotAsync(originals[0].Binding.Identity.Owner, carrier, write.ProtectionResult.Metadata, Type, parent, Token)).IsUnreadable.ShouldBeTrue();
        using var cachedDocument = JsonDocument.Parse(cached);
        var replay = await fixture.Engine.TryReplaySnapshotAsync(originals[0].Binding.Identity.Owner, cachedDocument.RootElement, write.ProtectionResult.Metadata, Type, parent, Token);
        replay.IsReadable.ShouldBeTrue(); var restored = (JsonElement)replay.State!;
        restored.GetProperty("Safe").GetInt32().ShouldBe(7);
        restored.GetProperty("Entries")[0].GetProperty("ProtectedContent").GetProperty("Value").GetProperty("State").GetString().ShouldBe("Erased");
        restored.GetProperty("Entries")[1].GetProperty("ProtectedContent").GetProperty("Value").GetProperty("Secret").GetString().ShouldBe("secret-b");
        JsonSerializer.SerializeToUtf8Bytes(carrier).ShouldBe(cached);
        (await fixture.Store.HeadAsync("tenant-a")).Targets.Values.Single().Registration.Identity.ObjectId.ShouldBe("interaction-b");
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Engine.ProtectSnapshotAsync(originals[0].Binding.Identity.Owner, state, Type, parent, Token));
        (await fixture.Custody.WrapAsync(root.Registration.Identity, "attempt-reissue", 0, Token)).ShouldBeNull();
    }

    [Fact]
    public async Task ConcreteSnapshotManagerLoadsMixedPartialErasureThroughItsTrustedCommandReplayCaller()
    {
        using var fixture = new ErasureRootEngineFixture(); var state = State();
        var (originals, parent) = Enroll(fixture, state, "hx-snapshot-v1:folded-state");
        var write = await Seal(fixture, state, originals, parent);
        var targets = new[] { originals[0].Binding.Identity.Target };
        var request = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "deletion", "seal", "accepted", 0, "erase-a",
            DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key"), "component-signature", 1, "dispatch", 2, targets);
        (await fixture.Manifests.ConsumeAsync(request, "reservation", Token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        var identity = originals[0].Binding.Identity.Owner;
        var snapshot = new SnapshotRecord(77, write.ProtectionResult.State, DateTimeOffset.UtcNow, identity.Domain, identity.AggregateId, identity.TenantId, write.ProtectionResult.Metadata) { SnapshotEnvelopeVersion = 1 };
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), Substitute.For<ILogger<SnapshotManager>>(), fixture.Engine);
        var native = Substitute.For<IActorStateManager>();
        native.TryGetStateAsync<object>(identity.SnapshotKey, Arg.Any<CancellationToken>()).Returns(new ConditionalValue<object>(true, JsonSerializer.SerializeToElement(snapshot, JsonSerializerOptions.Web)));
        var loaded = await manager.LoadSnapshotAsync(identity, native, cancellationToken: Token);
        loaded.ShouldNotBeNull(); var restored = (JsonElement)loaded.State;
        restored.GetProperty("Entries")[0].GetProperty("ProtectedContent").GetProperty("Value").GetProperty("State").GetString().ShouldBe("Erased");
        restored.GetProperty("Entries")[1].GetProperty("ProtectedContent").GetProperty("Value").GetProperty("Secret").GetString().ShouldBe("secret-b");
    }

    [Fact]
    public async Task MissingEntryEnrollmentRefusesBeforeAnyRootIssueAndChangedSkeletonCannotReplay()
    {
        using var fixture = new ErasureRootEngineFixture(); var state = State(); var (originals, parent) = Enroll(fixture, state);
        string second = ErasureRootOccurrenceBindings.Id(originals[1].Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(originals[1].Binding.Identity));
        var removed = fixture.Source.Enrollments[second]; fixture.Source.Enrollments.Remove(second);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Engine.ProtectSnapshotAsync(originals[0].Binding.Identity.Owner, state, Type, parent, Token));
        (await fixture.Store.HeadAsync("tenant-a")).Targets.ShouldBeEmpty();
        fixture.Source.Enrollments.Add(second, removed);
        var write = await Seal(fixture, state, originals, parent); var carrier = (ProtectedSnapshotEntriesV2)write.ProtectionResult.State;
        var skeleton = JsonNode.Parse(carrier.SkeletonBytes)!; skeleton["Safe"] = 8;
        var changed = carrier with { SkeletonBytes = JsonSerializer.SerializeToUtf8Bytes(skeleton) };
        (await fixture.Engine.TryReplaySnapshotAsync(originals[0].Binding.Identity.Owner, changed, write.ProtectionResult.Metadata, Type, parent, Token)).IsUnreadable.ShouldBeTrue();
    }

    [Fact]
    public async Task DuplicateCaseCarrierFieldsAndOverlappingProtectedValuesRefuse()
    {
        using var fixture = new ErasureRootEngineFixture(); var state = State(); var (originals, parent) = Enroll(fixture, state);
        var write = await Seal(fixture, state, originals, parent);
        string encoded = JsonSerializer.Serialize(write.ProtectionResult.State);
        using var duplicate = JsonDocument.Parse(encoded.Insert(1, "\"format\":\"json+pdenc-v2-entries\","));
        (await fixture.Engine.TryReplaySnapshotAsync(originals[0].Binding.Identity.Owner, duplicate.RootElement, write.ProtectionResult.Metadata, Type, parent, Token)).IsUnreadable.ShouldBeTrue();
        byte[] nested = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = originals[0].Binding.Identity.Target,
            Value = new { ProtectedContent = new { Target = originals[1].Binding.Identity.Target, Value = "nested" } } } });
        Should.Throw<Exception>(() => InteractionSnapshotEntries.Describe(nested, "tenant-a", Token));
    }

    [Fact]
    public void NewCompletionFieldsDoNotChangeExistingSingleTargetSerializedOriginals()
    {
        var owner = new Hexalith.EventStore.Contracts.Identity.AggregateIdentity("tenant-a", "directory", "aggregate");
        var occurrence = new PayloadProtectionOccurrenceContext(7, PayloadProtectionPayloadKind.Snapshot, "hx-snapshot-v1:state-type");
        var original = new PayloadProtectionCompletionContext(owner, occurrence, "01ARZ3NDEKTSV4RRFFQ69G5FAV", "01ARZ3NDEKTSV4RRFFQ69G5FAW", 1, 9);
        JsonSerializer.SerializeToUtf8Bytes(original).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(new { original.Identity, original.Occurrence, original.OperationId, original.KeyReference, original.DekVersion, original.LifecycleEpoch }));
        InteractionSnapshotEntries.EntryOccurrence(occurrence, "/Entries/0/Value").ShouldNotBe(occurrence);
    }
}
