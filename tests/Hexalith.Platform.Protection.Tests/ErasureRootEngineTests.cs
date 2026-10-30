using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>End-to-end source/component matrix using the actual engine, registry actor, new ERC ports and independently retained writer originals.</summary>
public sealed class ErasureRootEngineTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static byte[] Payload() => "{\"Safe\":7,\"ProtectedContent\":{\"Target\":{\"TenantId\":\"tenant-a\",\"AgentInteractionId\":\"interaction-a\",\"TargetProtectionKeyAlias\":\"alias-a\"},\"Value\":{\"Secret\":\"confidential-canary\"}}}"u8.ToArray();
    private static Task<PayloadProtectionWriteResult> Seal(ErasureRootEngineFixture fixture, InteractionProtectionBinding binding, byte[]? payload = null)
        => fixture.Engine.ProtectEventPayloadAsync(binding.Identity.Owner, Substitute.For<IEventPayload>(), "Event.Type", payload ?? Payload(), "json", new(binding.Identity.Sequence, binding.Identity.Kind, "Event.Type"), Token);
    private static async Task Commit(ErasureRootEngineFixture fixture, PayloadProtectionWriteResult write)
    {
        await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, Token);
        fixture.Source.Persisted.Add(write.CompletionContext!.OperationId, write.ProtectionResult.PayloadBytes.ToArray());
        await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, Token);
    }

    [Fact]
    public async Task ProductionRegistryLookupRecoversExactCommittedOriginalAfterStaleStateRestore()
    {
        using var fixture = new ErasureRootEngineFixture(); var original = fixture.Enroll();
        var write = await Seal(fixture, original.Binding);
        var stale = JsonSerializer.Deserialize<InteractionOccurrenceRegistrySnapshot>(JsonSerializer.Serialize(fixture.RegistryState.CommittedState["candidate-interaction-occurrences-v1"]))!;
        await Commit(fixture, write);
        var committed = (await fixture.Registry.LookupAsync(original.Binding.Identity, Token)).Record!;
        var before = await fixture.Store.RetainedHeadAsync("tenant-a"); var anchor = before.Anchors[ErasureRootOccurrenceAuthority.Scope("tenant-a")];
        await fixture.RegistryState.SetStateAsync("candidate-interaction-occurrences-v1", stale, Token); await fixture.RegistryState.SaveStateAsync(Token);
        var recovered = (await fixture.Registry.LookupAsync(original.Binding.Identity, Token)).Record!;
        JsonSerializer.Serialize(recovered).ShouldBe(JsonSerializer.Serialize(committed));
        var after = await fixture.Store.RetainedHeadAsync("tenant-a"); after.Epoch.ShouldBe(before.Epoch); after.Targets.Count.ShouldBe(before.Targets.Count);
        after.Anchors[ErasureRootOccurrenceAuthority.Scope("tenant-a")].Revision.ShouldBe(anchor.Revision);
        after.Anchors[ErasureRootOccurrenceAuthority.Scope("tenant-a")].Admissions.Count.ShouldBe(anchor.Admissions.Count);
    }

    [Fact]
    public async Task MissingNativeDigestRefusesBeforeRootOccurrenceOrRegistryEffects()
    {
        using var fixture = new ErasureRootEngineFixture(); var original = fixture.Enroll(); fixture.DigestAvailable = false;
        string before = ErasureRootCustody.Hash(await fixture.Store.RetainedHeadAsync("tenant-a"));
        await Should.ThrowAsync<InvalidOperationException>(() => Seal(fixture, original.Binding));
        ErasureRootCustody.Hash(await fixture.Store.RetainedHeadAsync("tenant-a")).ShouldBe(before);
        fixture.RegistryState.CommittedState.ShouldBeEmpty();
    }

    [Fact]
    public async Task QuietOwningHostRetiresOnlyDisposedUsesAndAnotherOwnerCanErase()
    {
        using var fixture = new ErasureRootEngineFixture(); var original = fixture.Enroll();
        var write = await Seal(fixture, original.Binding); await Commit(fixture, write);
        using var worker = new ErasureRootHolderCompletionWorker(fixture.Keys);
        await worker.StartAsync(Token);
        try
        {
            var use = await fixture.Keys.ResolveRootAsync(original.Binding.Identity, original.Binding.KeyReference, false, Token);
            use.ShouldNotBeNull(); System.Security.Cryptography.CryptographicOperations.ZeroMemory(use.Bytes);
            await Task.Delay(600, Token);
            (await fixture.Store.RetainedHeadAsync("tenant-a")).Targets.Values.Single().Holders.ShouldNotBeEmpty();
            use.Dispose();
            for (int attempt = 0; attempt < 20 && (await fixture.Store.RetainedHeadAsync("tenant-a")).Targets.Values.Single().Holders.Length != 0; attempt++)
            { await Task.Delay(100, Token); }
            var root = (await fixture.Store.RetainedHeadAsync("tenant-a")).Targets.Values.Single();
            root.Holders.ShouldBeEmpty(); root.CompletedHolders.ShouldBeEmpty(); root.CompletedHolderIndexRoot.ShouldNotBeNull();
            var targets = new[] { original.Binding.Identity.Target };
            var request = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "deletion", "seal", "accepted", 0, "batch", DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key-1"), "fixture-signature", 1, "dispatch", 2, targets);
            var secondOwner = new ErasureRootCustody(fixture.Store, new("erc-test", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant-a"] = "epoch" }), fixture.Permissions);
            (await secondOwner.ConsumeAsync(request, "reservation", Token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        }
        finally { await worker.StopAsync(Token); }
    }

    [Fact]
    public async Task ChangedPayloadHasTypedConflictAndNoNewNativeRootOrCiphertext()
    {
        using var fixture = new ErasureRootEngineFixture(); var original = fixture.Enroll();
        var first = await Seal(fixture, original.Binding); await Commit(fixture, first);
        var roots = (await fixture.Store.RetainedHeadAsync("tenant-a")).Targets.Count;
        byte[] changed = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Payload()).Replace("confidential-canary", "different-canary", StringComparison.Ordinal));
        await Should.ThrowAsync<PayloadProtectionOccurrenceConflictException>(() => Seal(fixture, original.Binding, changed));
        (await fixture.Store.RetainedHeadAsync("tenant-a")).Targets.Count.ShouldBe(roots);
        (await fixture.Registry.LookupAsync(original.Binding.Identity, Token)).Record!.Sealed!.PayloadBytes.ShouldBe(first.ProtectionResult.PayloadBytes);
    }

    [Fact]
    public async Task ZeroRootBytesDoNotRetireHolderUntilDerivedKeyConsumerScopeDisposes()
    {
        using var fixture = new ErasureRootEngineFixture();
        var original = fixture.Enroll();
        var write = await Seal(fixture, original.Binding);
        await Commit(fixture, write);
        await fixture.Keys.CompleteDisposedUsesAsync(Token);
        var use = await fixture.Keys.ResolveRootAsync(original.Binding.Identity, original.Binding.KeyReference, false, Token);
        use.ShouldNotBeNull();
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(use.Bytes);
        (await fixture.Keys.CompleteDisposedUsesAsync(Token)).ShouldBe(0);
        (await fixture.Store.RetainedHeadAsync("tenant-a")).Targets.Values.Single().Holders.ShouldNotBeEmpty();
        use.Dispose();
        (await fixture.Keys.CompleteDisposedUsesAsync(Token)).ShouldBe(1);
        (await fixture.Store.RetainedHeadAsync("tenant-a")).Targets.Values.Single().Holders.ShouldBeEmpty();
    }

    [Fact]
    public async Task LegacyCompletedHolderMigrationKeepsEveryRetiredIdentityRestrictive()
    {
        using var fixture = new ErasureRootEngineFixture(); var original = fixture.Enroll();
        var write = await Seal(fixture, original.Binding); await Commit(fixture, write); await fixture.Keys.CompleteDisposedUsesAsync(Token);
        var current = (await fixture.Custody.ReadAsync("tenant-a", Token))!.Value;
        string id = ErasureRootCustody.TargetId(original.RootIdentity); var root = current.Head.Targets[id];
        current.Head.Targets[id] = root with { CompletedHolders = ["legacy-completed"] };
        (await fixture.Custody.SaveAsync("tenant-a", current.Version, current.Head, Token)).ShouldBeTrue();
        (await fixture.Custody.UnwrapAsync(root.Registration, "legacy-completed", Token)).ShouldBeNull();
        var use = await fixture.Keys.ResolveRootAsync(original.Binding.Identity, original.Binding.KeyReference, false, Token);
        use.ShouldNotBeNull(); use.Dispose(); (await fixture.Keys.CompleteDisposedUsesAsync(Token)).ShouldBe(1);
        (await fixture.Store.RetainedHeadAsync("tenant-a")).Targets[id].CompletedHolders.ShouldBeEmpty();
        (await fixture.Custody.UnwrapAsync(root.Registration, "legacy-completed", Token)).ShouldBeNull();
    }

    [Fact]
    public async Task SustainedLegitimateReadsArchiveCompletedHolderIdentitiesWithoutHeadGrowth()
    {
        using var fixture = new ErasureRootEngineFixture(); var original = fixture.Enroll();
        var write = await Seal(fixture, original.Binding); await Commit(fixture, write); await fixture.Keys.CompleteDisposedUsesAsync(Token);
        int initial = (await fixture.Store.ReadHeadAsync("tenant-a", Token))!.Bytes.Length;
        for (int index = 0; index < 120; index++)
        {
            var use = await fixture.Keys.ResolveRootAsync(original.Binding.Identity, original.Binding.KeyReference, false, Token);
            use.ShouldNotBeNull(); use.Dispose(); (await fixture.Keys.CompleteDisposedUsesAsync(Token)).ShouldBe(1);
            (await fixture.Store.ReadHeadAsync("tenant-a", Token))!.Bytes.Length.ShouldBeLessThan(initial + 64);
        }
        var root = (await fixture.Store.RetainedHeadAsync("tenant-a")).Targets.Values.Single();
        root.CompletedHolders.ShouldBeEmpty(); root.Holders.ShouldBeEmpty(); root.CompletedHolderIndexRoot.ShouldNotBeNull();
    }

    [Fact]
    public async Task SealedOriginalVerifierAuthenticatesCiphertextAndExactKeyedPlaintextIntent()
    {
        using var fixture = new ErasureRootEngineFixture();
        var original = fixture.Enroll();
        var write = await Seal(fixture, original.Binding);
        var record = (await fixture.Registry.LookupAsync(original.Binding.Identity, Token)).Record!;
        var registration = (await fixture.Store.RetainedHeadAsync("tenant-a")).Targets.Values.Single().Registration;
        using var root = await fixture.Custody.UnwrapAsync(registration, "native-sealed-verifier-fixture", Token);
        root.ShouldNotBeNull();
        byte[] rootCopy = root.Bytes.ToArray();
        byte[] digest = Enumerable.Repeat((byte)73, 32).ToArray();
        (await InteractionOccurrenceSealedVerifier.VerifyAsync(record.Request, record.Sealed!, rootCopy, digest, Token)).ShouldBeTrue();
        rootCopy.ShouldAllBe(value => value == 0);
        var changed = record.Request with { ContentIntentHmac = new string('0', 64) };
        (await InteractionOccurrenceSealedVerifier.VerifyAsync(changed, record.Sealed!, root.Bytes.ToArray(), digest, Token)).ShouldBeFalse();
        var corrupted = record.Sealed! with { PayloadBytes = write.ProtectionResult.PayloadBytes.ToArray() };
        corrupted.PayloadBytes[^5] ^= 1;
        (await InteractionOccurrenceSealedVerifier.VerifyAsync(record.Request, corrupted, root.Bytes.ToArray(), digest, Token)).ShouldBeFalse();
    }

    [Fact]
    public async Task SourceOutboxAndTargetEventsShareOneNativeRootRemainSealedAndReplayTypedErased()
    {
        using var fixture = new ErasureRootEngineFixture();
        var source = fixture.Enroll();
        var target = fixture.Enroll("interaction", "01ARZ3NDEKTSV4RRFFQ69G5FAW");
        var sourceWrite = await Seal(fixture, source.Binding);
        await Commit(fixture, sourceWrite);
        var targetWrite = await Seal(fixture, target.Binding);
        await Commit(fixture, targetWrite);
        (await fixture.Store.RetainedHeadAsync("tenant-a")).Targets.Count.ShouldBe(1);
        foreach (var pair in new[] { (source.Binding, sourceWrite), (target.Binding, targetWrite) })
        {
            var (binding, write) = pair;
            var protectedResult = write.ProtectionResult;
            Encoding.UTF8.GetString(protectedResult.PayloadBytes).ShouldNotContain("confidential-canary");
            using var json = JsonDocument.Parse(protectedResult.PayloadBytes);
            json.RootElement.GetProperty("Safe").GetInt32().ShouldBe(7);
            var occurrence = new PayloadProtectionOccurrenceContext(binding.Identity.Sequence, binding.Identity.Kind, "Event.Type");
            var opened = await fixture.Engine.TryUnprotectEventPayloadAsync(binding.Identity.Owner, "Event.Type", protectedResult.PayloadBytes, protectedResult.SerializationFormat, protectedResult.Metadata, occurrence, Token);
            opened.IsReadable.ShouldBeTrue(); opened.PayloadBytes.ShouldBe(Payload());
            var distribution = await fixture.Engine.PrepareEventForDistributionAsync(binding.Identity.Owner, "Event.Type", protectedResult.PayloadBytes, protectedResult.SerializationFormat, protectedResult.Metadata, occurrence, Token);
            distribution.Outcome.PayloadBytes.ShouldBe(protectedResult.PayloadBytes);
        }
        var targets = new[] { source.Binding.Identity.Target };
        var request = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "deletion", "seal", "accepted", 0, "batch", DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key-1"), "fixture-signature", 1, "dispatch", 2, targets);
        (await fixture.Manifests.ConsumeAsync(request, "reservation", Token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        foreach (var pair in new[] { (source.Binding, sourceWrite), (target.Binding, targetWrite) })
        {
            var (binding, write) = pair; var value = write.ProtectionResult;
            var occurrence = new PayloadProtectionOccurrenceContext(binding.Identity.Sequence, binding.Identity.Kind, "Event.Type");
            var external = await fixture.Engine.TryUnprotectEventPayloadAsync(binding.Identity.Owner, "Event.Type", value.PayloadBytes, value.SerializationFormat, value.Metadata, occurrence, Token);
            external.IsReadable.ShouldBeFalse();
            var replay = await fixture.Engine.TryReplayEventPayloadAsync(binding.Identity.Owner, "Event.Type", value.PayloadBytes, value.SerializationFormat, value.Metadata, occurrence, Token);
            replay.HasErasedContent.ShouldBeTrue(); replay.Outcome.IsReadable.ShouldBeTrue();
            Encoding.UTF8.GetString(replay.Outcome.PayloadBytes!).ShouldContain("\"State\":\"Erased\"");
        }
    }

    [Fact]
    public async Task UnknownWriterCannotActivateAndExactRetryReusesCiphertextWithoutAnotherLease()
    {
        using var fixture = new ErasureRootEngineFixture(); var enrolled = fixture.Enroll();
        var write = await Seal(fixture, enrolled.Binding);
        var retry = await Seal(fixture, enrolled.Binding);
        retry.ProtectionResult.PayloadBytes.ShouldBe(write.ProtectionResult.PayloadBytes);
        await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, Token);
        await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Unknown, Token);
        (await fixture.Registry.LookupAsync(enrolled.Binding.Identity, Token)).Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.SealedPending);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, Token));
        fixture.Source.Persisted.Add(write.CompletionContext!.OperationId, write.ProtectionResult.PayloadBytes.ToArray());
        await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, Token);
        (await fixture.Registry.LookupAsync(enrolled.Binding.Identity, Token)).Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, Token));
    }

    [Fact]
    public async Task CallerAliasAndMissingIndependentSourceRefuseBeforeAnyRootIssue()
    {
        using var fixture = new ErasureRootEngineFixture(); var enrolled = fixture.Enroll();
        var changed = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Payload()).Replace("alias-a", "foreign-alias", StringComparison.Ordinal));
        await Should.ThrowAsync<InvalidOperationException>(() => Seal(fixture, enrolled.Binding, changed));
        fixture.Source.Available = false;
        await Should.ThrowAsync<InvalidOperationException>(() => Seal(fixture, enrolled.Binding));
        (await fixture.Store.RetainedHeadAsync("tenant-a")).Targets.ShouldBeEmpty();
    }
}
