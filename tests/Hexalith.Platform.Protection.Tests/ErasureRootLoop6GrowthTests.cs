using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Server.Tests.Security;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Measured actual erasure/lifecycle/guard history across the former serialized tenant-head cliff, with exact originals and bounded current reads.</summary>
public sealed class ErasureRootLoop6GrowthTests
{
    [Fact]
    public async Task RepeatedActualManifestLifecycleAndGuardTrafficKeepsHeadBoundedPastFormerLifetimeCliff()
    {
        const int count = 1536; var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        var survivor = (await fixture.Custody.WrapAsync(new("tenant-a", "survivor", PlatformKeyPurpose.InteractionRootDek, "survivor-alias", "root-v1", "lifecycle", "erc-test", "custody"), "survivor-wrap", 0, token))!;
        var legacyErased = new SortedDictionary<string, ErasureRootErasure>(StringComparer.Ordinal); var legacyOutcomes = new SortedDictionary<string, CustodyKeyLifecycleOutcome>(StringComparer.Ordinal);
        var originals = new List<(CustodyKeyRegistration Root, CustodyKeyLifecycleRequest Pin, CustodyKeyLifecycleOutcome Outcome, DeletionBatchConsumptionRequest Manifest, DeletionManifestProviderResult Result)>();
        var guard = new GovernanceGuardFixture();
        var source = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, native, fixture.Digests);
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, source, guard.Owner, Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), new string('A', 64), TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = [.. handler.Methods] }] };
        var guardClient = new DaprErasureRootGovernanceGuard(receiver.Authority); var guards = new List<(GovernanceGuardTransition Transition, GovernanceProtocolReceipt Receipt)>();
        for (int index = 0; index < count; index++)
        {
            var identity = new CustodyKeyObjectIdentity("tenant-a", index.ToString("D4") + new string('t', 2000), PlatformKeyPurpose.InteractionRootDek, new string('a', 2000), "root-v1", "lifecycle", "erc-test", "custody");
            var root = (await fixture.Custody.WrapAsync(identity, "wrap-" + index, 0, token))!; root.ShouldNotBeNull();
            var pin = new CustodyKeyLifecycleRequest(identity, "pin-" + index + new string('p', 1990), CustodyKeyLifecycleAction.Pin, 0, "hold", "fence", 1, "decision");
            if (index == 512) { fixture.Store.Failure = "cas-after"; (await fixture.Custody.ExecuteAsync(root, pin, ErasureRootCustody.Hash(pin), token)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown); }
            var pinned = await fixture.Custody.ExecuteAsync(root, pin, ErasureRootCustody.Hash(pin), token); pinned.Status.ShouldBe(CustodyKeyLifecycleStatus.Pinned);
            var unpin = pin with { OperationId = "unpin-" + index + new string('u', 1988), Action = CustodyKeyLifecycleAction.Unpin };
            var unpinned = await fixture.Custody.ExecuteAsync(root, unpin, ErasureRootCustody.Hash(unpin), token); unpinned.Status.ShouldBe(CustodyKeyLifecycleStatus.Unpinned);
            legacyOutcomes.Add(pin.OperationId, pinned); legacyOutcomes.Add(unpin.OperationId, unpinned);
            ProtectionTarget[] targets = [new(identity.TenantId, identity.ObjectId, identity.KeyAlias)];
            var request = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "request-" + index, "seal-" + index, "accepted", 0, "batch-" + index, DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key"), "component", 1, "dispatch", 2, targets);
            if (index == 1024) { fixture.Store.Failure = "trim-after"; (await fixture.Custody.ConsumeAsync(request, "reservation-" + index, token)).State.ShouldBe(DeletionManifestProviderState.Unknown); }
            var result = await fixture.Custody.ConsumeAsync(request, "reservation-" + index, token); result.State.ShouldBe(DeletionManifestProviderState.Consumed);
            var read = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value.Head;
            legacyErased.Add(ErasureRootCustody.TargetId(identity), (await fixture.Custody.ReadErasedAsync(read, ErasureRootCustody.TargetId(identity), token))!);
            originals.Add((root, pin, pinned, request, result));
            if (index % 24 == 0)
            {
                var hold = new GovernanceHoldCommand("growth-hold-" + index, guard.Scope, "policy-a", "");
                var register = guard.Command(GovernanceGuardOperation.RegisterHold, hold: hold);
                var receipt = (await guardClient.ExecuteAsync(register, [], token))!; receipt.Status.ShouldBe("Committed"); guards.Add((register, receipt));
                var release = guard.Command(GovernanceGuardOperation.ReleaseHold, hold: hold with { ReleaseReceiptId = "independent-release-" + index });
                (await guardClient.ExecuteAsync(release, [], token))!.Status.ShouldBe("Committed");
            }
        }
        var raw = (await fixture.Store.ReadHeadAsync("tenant-a", token))!; var compact = JsonSerializer.Deserialize<ErasureRootHead>(raw.Bytes, ErasureRootCustody.Json)!;
        compact.Erased.ShouldBeEmpty(); compact.ManifestArchives.ShouldBeEmpty(); compact.LifecycleOutcomes.ShouldBeEmpty(); compact.GuardAdmissionArchives.ShouldBeEmpty(); compact.GuardAttemptArchives.ShouldBeEmpty(); compact.Anchors.ShouldBeEmpty();
        compact.CustodyHistoryIndexRoot.ShouldNotBeNull(); compact.LifetimeIndexRoot.ShouldNotBeNull(); raw.Bytes.Length.ShouldBeLessThan(16384);
        int oldHeadBytes = JsonSerializer.SerializeToUtf8Bytes(compact with { Erased = legacyErased, LifecycleOutcomes = legacyOutcomes }).Length;
        oldHeadBytes.ShouldBeGreaterThan(OpenBaoErasureRootStore.MaximumHeadBytes);
        int maximumLookupReads = 0;
        foreach (int selected in new[] { 0, count / 2, count - 1 })
        {
            var original = originals[selected]; int before = fixture.Store.ArchiveReads;
            ErasureRootCustody.Hash(await fixture.Custody.LookupAsync(original.Manifest, "reservation-" + selected, token)).ShouldBe(ErasureRootCustody.Hash(original.Result));
            ErasureRootCustody.Hash(await fixture.Custody.LookupAsync(original.Root, original.Pin, ErasureRootCustody.Hash(original.Pin), token)).ShouldBe(ErasureRootCustody.Hash(original.Outcome));
            (await fixture.Custody.ReadErasureAsync(original.Result.TargetReceipts[0].Target, token)).ShouldBe(original.Result.TargetReceipts[0]);
            (await fixture.Custody.WrapAsync(original.Root.Identity, "wrap-" + selected, 0, token)).ShouldBeNull();
            maximumLookupReads = Math.Max(maximumLookupReads, fixture.Store.ArchiveReads - before);
        }
        maximumLookupReads.ShouldBeLessThan(128);
        foreach (var original in new[] { guards[0], guards[guards.Count / 2], guards[^1] })
        { ErasureRootCustody.Hash(await guardClient.LookupOriginalAsync(original.Transition, [], token)).ShouldBe(ErasureRootCustody.Hash(original.Receipt)); }
        var stale = await fixture.Store.ReadHeadAsync("tenant-a", token);
        var left = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value; var right = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        left.Head.QualificationAttempts.Add("left", "attempt"); right.Head.QualificationAttempts.Add("right", "attempt");
        (await fixture.Custody.SaveAsync("tenant-a", left.Version, left.Head, token)).ShouldBeTrue(); (await fixture.Custody.SaveAsync("tenant-a", right.Version, right.Head, token)).ShouldBeFalse();
        (await fixture.Store.CompareExchangeHeadAsync("tenant-a", stale!.Version, stale.Bytes, token)).ShouldBeFalse();
        using var surviving = (await fixture.Custody.UnwrapAsync(survivor, "surviving-holder", token))!; surviving.Bytes.Length.ShouldBe(32); surviving.Dispose(); (await fixture.Custody.CompleteHolderAsync(survivor, surviving, token)).ShouldBeTrue();
        var enrolled = fixture.Enroll("after-history"); byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = enrolled.Binding.Identity.Target, Value = "after-history-canary" } });
        var protectedWrite = await fixture.Engine.ProtectEventPayloadAsync(enrolled.Binding.Identity.Owner, Substitute.For<IEventPayload>(), enrolled.Binding.Identity.PayloadTypeId, payload, "json", ErasureRootOccurrenceBindings.Occurrence(enrolled.Binding.Identity), token);
        protectedWrite.ProtectionResult.SerializationFormat.ShouldBe("json+pdenc-v2");
        Console.WriteLine(JsonSerializer.Serialize(new { lifetimeDestructions = count, actualLifecycleOutcomes = count * 2, actualGuardTransactions = guard.Backend.CommittedTransactions,
            oldHeadBytes, headBytes = raw.Bytes.Length, archiveBytes = fixture.Store.ArchiveCreatedBytes, archiveWrites = fixture.Store.ArchiveWrites, archiveReads = fixture.Store.ArchiveReads, maximumLookupReads }));
    }
}
