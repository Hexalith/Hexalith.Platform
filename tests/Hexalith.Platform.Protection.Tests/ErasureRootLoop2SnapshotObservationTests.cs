using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual retained single/mixed snapshot records and read-only observer output preserve strict V2 verifier grammar.</summary>
public sealed class ErasureRootLoop2SnapshotObservationTests
{
    [Fact]
    public async Task ActualSingleAndMixedObserverEmissionsKeepLegacyEnvelopesAndStrictFragmentOriginals()
    {
        var token = TestContext.Current.CancellationToken; var output = new Dictionary<string, object>();
        foreach (bool mixed in new[] { false, true })
        {
            using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
            var parent = new PayloadProtectionOccurrenceContext(77, PayloadProtectionPayloadKind.Snapshot, "hx-snapshot-v1:directory-state");
            ProtectionTarget[] targets = mixed ? [new("tenant-a", "interaction-a", "alias-a"), new("tenant-a", "interaction-b", "alias-b")] : [new("tenant-a", "interaction-a", "alias-a")];
            var state = mixed ? JsonSerializer.SerializeToElement(new { Entries = targets.Select(target => new { ProtectedContent = new { Target = target, Value = new { Secret = "snapshot-canary" } } }).ToArray() })
                : JsonSerializer.SerializeToElement(new { ProtectedContent = new { Target = targets[0], Value = new { Secret = "snapshot-canary" } } });
            var layout = InteractionSnapshotEntries.Describe(JsonSerializer.SerializeToUtf8Bytes(state), "tenant-a", token);
            var originals = new List<ErasureRootOccurrenceEnrollment>();
            for (int index = 0; index < targets.Length; index++)
            {
                var seed = fixture.Enroll("directory", index == 0 ? "01ARZ3NDEKTSV4RRFFQ69G5FAV" : "01ARZ3NDEKTSV4RRFFQ69G5FAW", (ulong)index + 1);
                var occurrence = mixed ? InteractionSnapshotEntries.EntryOccurrence(parent, layout.Entries[index].Path) : parent;
                var identity = seed.Binding.Identity with { Target = targets[index], Kind = PayloadProtectionPayloadKind.Snapshot, Sequence = parent.RecordSequence, PayloadTypeId = occurrence.PayloadTypeId };
                var original = seed with { Binding = seed.Binding with { Identity = identity }, RootIdentity = seed.RootIdentity with { ObjectId = targets[index].AgentInteractionId, KeyAlias = targets[index].TargetProtectionKeyAlias },
                    Writer = new("source", "writer-" + index, new()), SnapshotEntry = mixed ? new(parent, layout.Entries[index].Path, layout.ContainerDigest, targets.Length) : null };
                fixture.Source.Enrollments.Add(ErasureRootOccurrenceBindings.Id(identity.Owner, occurrence), original); originals.Add(original);
            }
            var type = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }.GetTypeInfo(typeof(JsonElement));
            var write = await fixture.Engine.ProtectSnapshotAsync(originals[0].Binding.Identity.Owner, state, type, parent, token);
            await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, token);
            var proofs = new Dictionary<string, ErasureRootWriterProof>();
            for (int index = 0; index < originals.Count; index++)
            {
                var retained = (await fixture.Registry.LookupAsync(originals[index].Binding.Identity, token)).Record!;
                if (mixed) { retained.Sealed!.SnapshotReplaySkeleton.ShouldBeNull(); JsonSerializer.SerializeToElement(retained.Sealed).EnumerateObject().Count().ShouldBe(4); }
                else { retained.Sealed!.SnapshotReplaySkeleton.ShouldNotBeNull(); }
                fixture.Source.Persisted.Add(originals[index].Binding.OperationId, retained.Sealed!.PayloadBytes);
                proofs.Add("writer-" + index, new(originals[index].Binding, Convert.ToHexString(SHA256.HashData(retained.Sealed.PayloadBytes)), true, true, "actual-source-writer-" + index));
            }
            await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token);
            await fixture.Keys.CompleteDisposedUsesAsync(token);
            var head = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
            foreach (var original in originals) { head.Head.SourceOriginals.Add(ErasureRootOccurrenceBindings.Id(original.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity)), original); }
            string location = ProtectionQualificationNativeCell.LocationId(new("source", "snapshot", new()));
            head.Head.QualificationProducerOriginals.Add(location, new(location, originals.Select(value => value.Binding).ToArray(), Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(write.ProtectionResult.State))),
                ErasureRootCustody.Hash(write.CompletionContext), write.CompletionContext!, ProtectionQualificationProducerIdentity.Capture(fixture.Engine, fixture.Custody.Installation)));
            (await fixture.Custody.SaveAsync("tenant-a", head.Version, head.Head, token)).ShouldBeTrue();
            native.GetStateAsync<JsonElement>("source", "snapshot", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(JsonSerializer.SerializeToElement(write.ProtectionResult));
            native.GetStateAsync<PayloadProtectionCompletionContext>("source", "completion", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(write.CompletionContext!);
            native.GetStateAsync<ErasureRootWriterProof>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call => proofs[call.ArgAt<string>(1)]);
            var observer = new ProtectionQualificationPersistedObserver(native, fixture.Custody, fixture.Engine, new HashSet<string> { "source" });
            var observation = await observer.ObserveAsync(new("tenant-a", new() { ["snapshot"] = new("source", "snapshot", new()) { CopyKind = "snapshot", CompletionKey = mixed ? "completion" : null } }, new()) { CaseId = "P01" }, targets, token);
            output.Add(mixed ? "mixed" : "single", observation);
        }
        if (Environment.GetEnvironmentVariable("EXT_PROTECTION_SNAPSHOT_VECTOR") is { Length: > 0 } path)
        { await File.WriteAllTextAsync(path, JsonSerializer.Serialize(output), token); }
    }
}
