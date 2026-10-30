using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Secrets.Runtime;
using Dapr.Client;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual native source index/snapshot retention and authenticated independent proof remain available with a throwing former ERC.</summary>
public sealed class ErasureRootLoop4SnapshotLossTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndexedIndependentOriginalsReplaySingleAndMixedSnapshotsAfterPermanentErcLoss(bool mixed)
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture(); using var durable = new ErasureRootIndependentSourceFixture();
        var nativeSource = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, durable.Native, fixture.Digests,
            new Dictionary<string, ProtectionQualificationNativeCell> { ["tenant-a"] = new("independent", "root", new()) });
        var parent = new PayloadProtectionOccurrenceContext(77, PayloadProtectionPayloadKind.Snapshot, "hx-snapshot-v1:directory-state");
        ProtectionTarget[] targets = mixed ? [new("tenant-a", "interaction-a", "alias-a"), new("tenant-a", "interaction-b", "alias-b")] : [new("tenant-a", "interaction-a", "alias-a")];
        var state = mixed ? JsonSerializer.SerializeToElement(new { Entries = targets.Select(target => new { ProtectedContent = new { Target = target, Value = "snapshot-canary" } }).ToArray() })
            : JsonSerializer.SerializeToElement(new { ProtectedContent = new { Target = targets[0], Value = "snapshot-canary" } });
        var layout = InteractionSnapshotEntries.Describe(JsonSerializer.SerializeToUtf8Bytes(state), "tenant-a", token); var originals = new List<ErasureRootOccurrenceEnrollment>();
        for (int index = 0; index < targets.Length; index++)
        {
            var seed = fixture.Enroll("directory", index == 0 ? "01ARZ3NDEKTSV4RRFFQ69G5FAV" : "01ARZ3NDEKTSV4RRFFQ69G5FAW", (ulong)index + 1);
            var occurrence = mixed ? InteractionSnapshotEntries.EntryOccurrence(parent, layout.Entries[index].Path) : parent;
            var identity = seed.Binding.Identity with { Target = targets[index], Kind = PayloadProtectionPayloadKind.Snapshot, Sequence = 77, PayloadTypeId = occurrence.PayloadTypeId };
            var original = seed with { Binding = seed.Binding with { Identity = identity }, RootIdentity = seed.RootIdentity with { ObjectId = targets[index].AgentInteractionId, KeyAlias = targets[index].TargetProtectionKeyAlias }, Writer = new("source", "writer-" + index, new()),
                SnapshotEntry = mixed ? new(parent, layout.Entries[index].Path, layout.ContainerDigest, targets.Length) : null };
            fixture.Source.Enrollments.Add(ErasureRootOccurrenceBindings.Id(identity.Owner, occurrence), original);
            (await nativeSource.RegisterOriginalAsync(original, token)).ShouldBeTrue(); originals.Add(original);
        }
        var type = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }.GetTypeInfo(typeof(JsonElement));
        var write = await fixture.Engine.ProtectSnapshotAsync(originals[0].Binding.Identity.Owner, state, type, parent, token);
        await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, token);
        var proofs = new Dictionary<string, ErasureRootWriterProof>();
        for (int index = 0; index < originals.Count; index++)
        {
            var record = (await fixture.Registry.LookupAsync(originals[index].Binding.Identity, token)).Record!;
            (await nativeSource.VerifySealedAsync(record, record.Sealed!, token)).ShouldBeTrue();
            fixture.Source.Persisted.Add(originals[index].Binding.OperationId, record.Sealed!.PayloadBytes);
            proofs.Add("writer-" + index, new(originals[index].Binding, Convert.ToHexString(SHA256.HashData(record.Sealed.PayloadBytes)), true, true, "independent-native-writer-" + index));
        }
        await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token); await fixture.Keys.CompleteDisposedUsesAsync(token);
        durable.Native.GetStateAsync<ErasureRootWriterProof>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call => proofs[call.ArgAt<string>(1)]);
        var policy = new string('A', 64); var loss = new ErasureRootPermanentLossReceipt("erc-test", "enrollment", "tenant-a", "retired-volume", "independent-retirement", policy);
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, nativeSource, Substitute.For<IGovernanceScopeGuard>(), Substitute.For<Hexalith.Platform.Custody.IDeletionCapabilityRevocationAuthenticator>(), policy, TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = [.. handler.Methods] }],
            QualificationCases = new() { ["erc.permanent-loss:erc-test:enrollment"] = JsonSerializer.SerializeToElement(loss, CustodyBindingContract.Json) },
            QualificationBindings = new() { ["erc.copyPolicySha256"] = policy, ["erc.volume.erc-test"] = loss.RetiredVolumeIdentity } };
        var source = new DaprErasureRootOccurrenceSource(receiver.Authority); fixture.Permissions.PermanentLoss = loss;
        var binding = new ErasureRootOccurrenceBindings(fixture.Custody, fixture.Permissions, source);
        var engine = new InteractionPayloadProtectionService(fixture.Registry, fixture.Keys, binding, TimeProvider.System);
        fixture.Store.ReadObserved = () => throw new IOException("Former ERC unavailable; no absence or new authority inferred.");
        var read = await engine.TryReplaySnapshotAsync(originals[0].Binding.Identity.Owner, write.ProtectionResult.State, write.ProtectionResult.Metadata, type, parent, token);
        read.IsReadable.ShouldBeTrue(); JsonSerializer.Serialize(read.State).ShouldContain("\"State\":\"Erased\"");
        foreach (var original in originals)
        { ErasureRootCustody.Hash(await source.ResolveAsync(original.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), token)).ShouldBe(ErasureRootCustody.Hash(original)); }
        durable.Root = durable.Root with { EnrollmentId = "foreign-enrollment" };
        (await engine.TryReplaySnapshotAsync(originals[0].Binding.Identity.Owner, write.ProtectionResult.State, write.ProtectionResult.Metadata, type, parent, token)).IsUnreadable.ShouldBeTrue();
    }
}
