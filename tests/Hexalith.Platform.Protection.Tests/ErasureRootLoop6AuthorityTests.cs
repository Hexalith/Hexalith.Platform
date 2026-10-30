using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Independent authority/effect index boundaries and immutable history selected only by an authoritative tenant CAS.</summary>
public sealed class ErasureRootLoop6AuthorityTests
{
    [Fact]
    public async Task PermittedOwnerGuardAndQualificationBookkeepingDoesNotChangeCustodyEffectFingerprint()
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture();
        var identity = new CustodyKeyObjectIdentity("tenant-a", "effect-root", PlatformKeyPurpose.InteractionRootDek, "alias", "root-v1", "lifecycle", "erc-test", "custody");
        var root = (await fixture.Custody.WrapAsync(identity, "wrap", 0, token))!;
        async Task<string> Fingerprint(string name) => (await fixture.Custody.CaptureEffectBoundaryAsync("tenant-a", ErasureRootCustody.Hash(name), "before", false, token))!.Fingerprint;
        string before = await Fingerprint("original");
        var head = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        head.Head.Anchors.Add("tenant-a:source:additional|state", new(0, new string('A', 64), [], []));
        var transition = new GovernanceGuardTransition("tenant-a", "guard-attempt", GovernanceGuardOperation.RegisterHold, 1, "epoch", "deletion", null, null, null, null, null, null, "", "");
        string digest = ErasureRootCustody.Hash(Array.Empty<GuardedStateMutation>()), intent = ErasureRootCustody.Hash(new { Transition = transition, TargetMutationDigest = digest });
        head.Head.GuardAdmissions.Add(intent, new(transition, [], 1)); head.Head.GuardAttempts.Add(intent, new(transition, digest, 1, 1, new(transition.OperationId, intent, "Stale", 1, 0, "", "actual-refusal")));
        head.Head.QualificationAttempts.Add("separate-profile", "one-attempt");
        (await fixture.Custody.SaveAsync("tenant-a", head.Version, head.Head, token)).ShouldBeTrue();
        (await Fingerprint("bookkeeping")).ShouldBe(before);
        var pin = new CustodyKeyLifecycleRequest(identity, "pin", CustodyKeyLifecycleAction.Pin, 0, "hold", "fence", 1, "decision");
        (await fixture.Custody.ExecuteAsync(root, pin, ErasureRootCustody.Hash(pin), token)).Status.ShouldBe(CustodyKeyLifecycleStatus.Pinned);
        string pinned = await Fingerprint("pinned"); pinned.ShouldNotBe(before);
        var unpin = pin with { OperationId = "unpin", Action = CustodyKeyLifecycleAction.Unpin };
        (await fixture.Custody.ExecuteAsync(root, unpin, ErasureRootCustody.Hash(unpin), token)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unpinned);
        (await Fingerprint("unpinned-history")).ShouldNotBe(before);
        ProtectionTarget[] targets = [new("tenant-a", "effect-root", "alias")];
        var request = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "request", "seal", "accepted", 0, "batch", DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key"), "component", 1, "dispatch", 2, targets);
        string alive = await Fingerprint("before-erasure"); (await fixture.Custody.ConsumeAsync(request, "reservation", token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        (await Fingerprint("after-erasure")).ShouldNotBe(alive);
    }

    [Fact]
    public async Task SigningRecoveryReferenceKeepsLatestObservedOriginalAndEveryExactEarlierIdentity()
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        var payload = new DeletionBatchCapabilityV1("issuer", "audience", "tenant-a", "request", "seal", "accepted", 0, "batch", new string('A', 64), "guard", 1, 1, 1, "key");
        var oldest = new GovernanceSigningRecovery(payload, "signing", "signature", "no-issue", 2, "key", "authority", DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(10));
        var latest = oldest with { ObservedAt = oldest.ObservedAt.AddMinutes(1), CurrentGuardRevision = 3 };
        foreach (var value in new[] { latest, oldest })
        {
            var read = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value; read.Head.SigningRecoveries.Add(ErasureRootCustody.Hash(value), value);
            (await fixture.Custody.SaveAsync("tenant-a", read.Version, read.Head, token)).ShouldBeTrue();
        }
        var source = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, native, fixture.Digests);
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Custody, source, Substitute.For<IGovernanceScopeGuard>(), Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), new string('A', 64), TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner");
        var caller = receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc" };
        var recovered = await handler.RecoverAsync("erc-read-no-issue", JsonSerializer.SerializeToElement(new { payload, signingRequestId = "signing", detachedJwsDigest = "signature" }), caller, receiver.State, token);
        ErasureRootCustody.Hash(recovered).ShouldBe(ErasureRootCustody.Hash(latest));
        var current = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value.Head;
        ErasureRootCustody.Hash(await fixture.Custody.ReadRetainedAsync(current, "SigningRecoveries", current.SigningRecoveries, ErasureRootCustody.Hash(oldest), token)).ShouldBe(ErasureRootCustody.Hash(oldest));
    }

    [Fact]
    public async Task OrphanErasureAndLifecyclePrewritesCannotRefuseAHealthyRootOrCreateCompletion()
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture();
        var identity = new CustodyKeyObjectIdentity("tenant-a", "orphan-root", PlatformKeyPurpose.InteractionRootDek, "alias", "root-v1", "lifecycle", "erc-test", "custody");
        var registration = new CustodyKeyRegistration(identity, "wrap", 0, "1", "reference", "receipt");
        var target = new ProtectionTarget("tenant-a", "orphan-root", "alias");
        var marker = new ErasureRootErasure(registration, new(target, "orphan-batch", "orphan-receipt"), "orphan-reservation", 2);
        var head = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value.Head;
        _ = await new ErasureRootOccurrenceIndex(fixture.Store, fixture.Custody.Installation, "tenant-a").WriteAsync(head.CustodyHistoryIndexRoot, "Erased:" + ErasureRootCustody.TargetId(target), marker, token);
        (await fixture.Custody.ReadErasureAsync(target, token)).ShouldBeNull();
        (await fixture.Custody.WrapAsync(identity, "wrap", 0, token)).ShouldNotBeNull();
        fixture.Store.TrimCount.ShouldBe(0);
    }
}
