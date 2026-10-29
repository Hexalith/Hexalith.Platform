using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>State-machine, ownership, isolation and failure component tests; these do not qualify native physical erasure.</summary>
public sealed class ErasureRootCustodyTests
{
    private readonly ErasureRootInstallation _installation = new("erc-test", "independent-enrollment", "erc-transit", "erc-heads",
        new Dictionary<string, string> { ["tenant-a"] = "epoch-a", ["tenant-b"] = "epoch-b" });
    private readonly ErasureRootStoreFixture _store = new();
    private readonly ErasureRootAuthorizationFixture _authorization = new();
    private readonly ErasureRootCustody _custody;

    public ErasureRootCustodyTests()
    {
        _store.Enroll(_installation, "tenant-a");
        _store.Enroll(_installation, "tenant-b");
        _custody = new(_store, _installation, _authorization);
    }
    private CustodyKeyObjectIdentity Identity(string id = "interaction-001", string tenant = "tenant-a")
        => new(tenant, id, PlatformKeyPurpose.InteractionRootDek, "explicit-alias", "root-1", "lifecycle-1", _installation.StoreId, "custody-1");
    private async Task<CustodyKeyRegistration> RootAsync(string id = "interaction-001", string tenant = "tenant-a")
        => (await _custody.WrapAsync(Identity(id, tenant), "wrap:" + id, 0, cancellationToken: TestContext.Current.CancellationToken))!;
    private static DeletionBatchConsumptionRequest Request(IEnumerable<CustodyKeyRegistration> roots, string batch = "batch-1")
    {
        var targets = roots.Select(root => new ProtectionTarget(root.Identity.TenantId, root.Identity.ObjectId, root.Identity.KeyAlias))
            .OrderBy(target => target.AgentInteractionId, StringComparer.Ordinal).ToArray();
        return new(new("issuer", "audience", targets[0].TenantId, "deletion", "seal", "accepted-set", 0, batch,
            DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "signing-1"), "detached-component-fixture", 1, "dispatch", 2, targets);
    }
    private static string Digest<T>(T value) => ErasureRootCustody.Hash(value);

    [Fact]
    public async Task ManifestTrimsOnceRetainsSurvivorsAndOriginalOrderedVector()
    {
        var first = await RootAsync();
        var second = await RootAsync("interaction-002");
        var survivor = await RootAsync("interaction-003");
        var before = await _store.HeadAsync("tenant-a");
        var leaked = before.Targets[ErasureRootCustody.TargetId(first.Identity)].Wrapped;
        var originalSurvivor = before.Targets[ErasureRootCustody.TargetId(survivor.Identity)].Wrapped;
        byte[] expected = await _store.DecryptAsync("tenant-a", originalSurvivor, TestContext.Current.CancellationToken);
        var request = Request([first, second]);
        var result = await _custody.ConsumeAsync(request, "original-reservation", cancellationToken: TestContext.Current.CancellationToken);
        result.State.ShouldBe(DeletionManifestProviderState.Consumed);
        result.TargetReceipts.Select(receipt => receipt.Target).ShouldBe(request.Targets);
        _store.TrimCount.ShouldBe(1);
        _store.RotateCount.ShouldBe(1);
        var after = await _store.HeadAsync("tenant-a");
        after.Targets.Count.ShouldBe(1);
        after.Erased.Count.ShouldBe(2);
        (await _store.DecryptAsync("tenant-a", after.Targets.Single().Value.Wrapped, TestContext.Current.CancellationToken)).ShouldBe(expected);
        CryptographicOperations.ZeroMemory(expected);
        await Should.ThrowAsync<KeyNotFoundException>(() => _store.DecryptAsync("tenant-a", leaked, TestContext.Current.CancellationToken));
        Digest(await _custody.LookupAsync(request, "original-reservation", cancellationToken: TestContext.Current.CancellationToken)).ShouldBe(Digest(result));
        Digest(await _custody.ConsumeAsync(request, "original-reservation", cancellationToken: TestContext.Current.CancellationToken)).ShouldBe(Digest(result));
        _store.TrimCount.ShouldBe(1);
        (await _custody.WrapAsync(first.Identity, "another-wrap", 0, cancellationToken: TestContext.Current.CancellationToken)).ShouldBeNull();
        (await _custody.ReadErasureAsync(request.Targets[0], cancellationToken: TestContext.Current.CancellationToken)).ShouldBe(result.TargetReceipts[0]);
    }

    [Theory]
    [InlineData("cas-after")]
    [InlineData("rotate-after")]
    [InlineData("trim-before")]
    [InlineData("trim-after")]
    public async Task LostAcknowledgementRecoversOnlyExactOriginal(string fault)
    {
        var root = await RootAsync();
        var request = Request([root]);
        _store.Failure = fault;
        (await _custody.ConsumeAsync(request, "reservation", cancellationToken: TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        (await _custody.LookupAsync(request, "different-reservation", cancellationToken: TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        var recovered = await _custody.LookupAsync(request, "reservation", cancellationToken: TestContext.Current.CancellationToken);
        recovered.State.ShouldBe(DeletionManifestProviderState.Consumed);
        _store.RotateCount.ShouldBe(1);
        _store.TrimCount.ShouldBe(1);
        Digest(await _custody.LookupAsync(request, "reservation", cancellationToken: TestContext.Current.CancellationToken)).ShouldBe(Digest(recovered));
    }

    [Fact]
    public async Task RewrapFailureDoesNotDestroyAnyOfOneThousandTargets()
    {
        var template = await RootAsync();
        var survivor = await RootAsync("survivor");
        var head = await _store.HeadAsync("tenant-a");
        var original = head.Targets[ErasureRootCustody.TargetId(template.Identity)];
        head.Targets.Remove(ErasureRootCustody.TargetId(template.Identity));
        var roots = new List<CustodyKeyRegistration>();
        for (int index = 0; index < 1000; index++)
        {
            var registration = template with { Identity = Identity("interaction-" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture)) };
            // Seed exact context-bound fixture ciphertext without doing 1000 unrelated root-issue CAS operations.
            string context = Convert.ToBase64String(CustodyKeyWrapCore.Context(registration.Identity, registration.KekVersion));
            byte[] root = RandomNumberGenerator.GetBytes(32);
            string wrapped;
            try { wrapped = await _store.EncryptAsync("tenant-a", 1, root, context, TestContext.Current.CancellationToken); }
            finally { CryptographicOperations.ZeroMemory(root); }
            head.Targets.Add(ErasureRootCustody.TargetId(registration.Identity), original with { Registration = registration, Wrapped = new(wrapped, context) });
            roots.Add(registration);
        }
        _store.ReplaceHead("tenant-a", head);
        var request = Request(roots);
        _store.Failure = "rewrap";
        (await _custody.ConsumeAsync(request, "thousand-original", cancellationToken: TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        var pending = await _store.HeadAsync("tenant-a");
        pending.Erased.ShouldBeEmpty();
        pending.Targets.Count.ShouldBe(1001);
        _store.TrimCount.ShouldBe(0);
        (await _custody.LookupAsync(request, "thousand-original", cancellationToken: TestContext.Current.CancellationToken)).TargetReceipts.Count.ShouldBe(1000);
        var final = await _store.HeadAsync("tenant-a");
        final.Erased.Count.ShouldBe(1000);
        final.Targets.Single().Value.Registration.ShouldBe(survivor);
    }

    [Fact]
    public async Task PinAndAuthorizedUnpinAreImmutableAndBlockManifestBeforeNativeEffects()
    {
        var root = await RootAsync();
        var pin = new CustodyKeyLifecycleRequest(root.Identity, "pin-1", CustodyKeyLifecycleAction.Pin, 0, "hold-1", "fence", 1, "decision");
        var original = await _custody.ExecuteAsync(root, pin, Digest(pin), cancellationToken: TestContext.Current.CancellationToken);
        original.Status.ShouldBe(CustodyKeyLifecycleStatus.Pinned);
        Digest(await _custody.ExecuteAsync(root, pin, Digest(pin), cancellationToken: TestContext.Current.CancellationToken)).ShouldBe(Digest(original));
        (await _custody.ConsumeAsync(Request([root]), "reservation", cancellationToken: TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        _store.RotateCount.ShouldBe(0);
        var unpin = pin with { Action = CustodyKeyLifecycleAction.Unpin, OperationId = "unpin-1" };
        _authorization.Allowed = false;
        (await _custody.ExecuteAsync(root, unpin, Digest(unpin), cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
        _authorization.Allowed = true;
        (await _custody.ExecuteAsync(root, unpin, Digest(unpin), cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unpinned);
        (await _custody.ConsumeAsync(Request([root]), "reservation", cancellationToken: TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Consumed);
    }

    [Fact]
    public async Task DurableHolderFencesEraseUntilClearedAndCannotBeDeliveredAgain()
    {
        var root = await RootAsync();
        var holder = await _custody.UnwrapAsync(root, "holder-1", cancellationToken: TestContext.Current.CancellationToken);
        holder.ShouldNotBeNull();
        (await _custody.CompleteHolderAsync(root, holder, cancellationToken: TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await _custody.ConsumeAsync(Request([root]), "reservation", cancellationToken: TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        holder.Dispose();
        (await _custody.CompleteHolderAsync(root, holder, cancellationToken: TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await _custody.UnwrapAsync(root, "holder-1", cancellationToken: TestContext.Current.CancellationToken)).ShouldBeNull();
        (await _custody.ConsumeAsync(Request([root]), "reservation", cancellationToken: TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Consumed);
    }

    [Fact]
    public async Task InvalidWrapRevisionRefusesBeforeRootIssueOrNativeStateEffects()
    {
        await Should.ThrowAsync<ArgumentException>(() => _custody.WrapAsync(Identity(), "wrap", -1, TestContext.Current.CancellationToken));
        _store.CasCount.ShouldBe(0);
        (await _store.HeadAsync("tenant-a")).Targets.ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingPermissionAndMissingEnrollmentHaveZeroEffects()
    {
        _authorization.Allowed = false;
        (await RootAsync()).ShouldBeNull();
        _store.CasCount.ShouldBe(0);
        var missing = new ErasureRootCustody(new ErasureRootStoreFixture(), _installation, new ErasureRootAuthorizationFixture());
        (await missing.WrapAsync(Identity(), "wrap", 0, cancellationToken: TestContext.Current.CancellationToken)).ShouldBeNull();
        _store.RotateCount.ShouldBe(0);
    }

    [Fact]
    public async Task DelayedExtraRotationIsHarmlessBecauseTheOriginalSuccessorIsExplicit()
    {
        var root = await RootAsync();
        _store.RotationEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _store.RotationContinue = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = Request([root]);
        var first = _custody.ConsumeAsync(request, "reservation", cancellationToken: TestContext.Current.CancellationToken);
        await _store.RotationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var second = _custody.LookupAsync(request, "reservation", cancellationToken: TestContext.Current.CancellationToken);
        _store.RotateCount.ShouldBe(0);
        _store.RotationContinue.SetResult();
        await Task.WhenAll(first, second);
        ((await first).State == DeletionManifestProviderState.Consumed || (await second).State == DeletionManifestProviderState.Consumed).ShouldBeTrue();
        (await _custody.LookupAsync(request, "reservation", TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        _store.RotateCount.ShouldBe(2);
        _store.TrimCount.ShouldBe(1);
        (await _store.HeadAsync("tenant-a")).Epoch.ShouldBe(2);
        (await _store.ReadEpochAsync("tenant-a", TestContext.Current.CancellationToken)).ShouldBe(3);
        // New root issue must still use epoch 2 even though an unused native version 3 exists.
        (await RootAsync("later-root")).ShouldNotBeNull();
        (await _store.HeadAsync("tenant-a")).Targets.Single().Value.Wrapped.Ciphertext.ShouldStartWith("vault:v2:");
    }

    [Fact]
    public async Task CrashBeforeNativeRotateRecoversTheSameOriginalManifest()
    {
        var root = await RootAsync();
        var request = Request([root]);
        _store.Failure = "rotate-before";
        (await _custody.ConsumeAsync(request, "reservation", cancellationToken: TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        (await _custody.LookupAsync(request, "reservation", cancellationToken: TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        _store.RotateCount.ShouldBe(1);
        var final = await _store.HeadAsync("tenant-a");
        final.Pending.ShouldBeNull();
        final.Manifests.Single().Key.ShouldBe(request.Capability.BatchId);
    }

    [Fact]
    public async Task PinRacesManifestAtTheSameNativeHeadAndExactlyOneWins()
    {
        var root = await RootAsync();
        var pin = new CustodyKeyLifecycleRequest(root.Identity, "racing-pin", CustodyKeyLifecycleAction.Pin, 0, "hold", "fence", 1, "decision");
        var pinTask = _custody.ExecuteAsync(root, pin, Digest(pin), TestContext.Current.CancellationToken);
        var manifestTask = _custody.ConsumeAsync(Request([root]), "reservation", TestContext.Current.CancellationToken);
        await Task.WhenAll(pinTask, manifestTask);
        bool pinned = (await pinTask).Status == CustodyKeyLifecycleStatus.Pinned;
        bool consumed = (await manifestTask).State == DeletionManifestProviderState.Consumed;
        (pinned ^ consumed).ShouldBeTrue();
        var head = await _store.HeadAsync("tenant-a");
        if (pinned) { head.Targets.Single().Value.Pins.ShouldBe(["hold"]); head.Erased.ShouldBeEmpty(); }
        else { head.Targets.ShouldBeEmpty(); head.Erased.Count.ShouldBe(1); }
    }

    [Fact]
    public async Task PerKeyDestroyCanOnlyReadTheExactOriginalManifestCoverage()
    {
        var root = await RootAsync();
        var destroy = new CustodyKeyLifecycleRequest(root.Identity, "destroy-read", CustodyKeyLifecycleAction.Destroy, 0,
            null, "fence", 1, "decision", "reservation");
        (await _custody.ExecuteAsync(root, destroy, Digest(destroy), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
        _store.RotateCount.ShouldBe(0);
        var manifest = await _custody.ConsumeAsync(Request([root]), "reservation", TestContext.Current.CancellationToken);
        var outcome = await _custody.ExecuteAsync(root, destroy, Digest(destroy), TestContext.Current.CancellationToken);
        outcome.Status.ShouldBe(CustodyKeyLifecycleStatus.Destroyed);
        outcome.ReceiptId.ShouldBe(manifest.TargetReceipts[0].ReceiptId);
        outcome.RestoreBarrierReceiptId.ShouldNotBeNullOrWhiteSpace();
        var foreign = destroy with { ProtectionReservationReceiptId = "other-reservation" };
        (await _custody.LookupAsync(root, foreign, Digest(foreign), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyKeyLifecycleStatus.Unknown);
        _store.TrimCount.ShouldBe(1);
    }

    [Fact]
    public async Task LifecycleAuthorityRequiresExactNativeRegistrationAndOutcomeRatherThanProviderClaims()
    {
        var root = await RootAsync();
        var authority = new ErasureRootLifecycleAuthority(_custody, _authorization, new(_custody, _authorization, "tenant-a", "lifecycle-scope"));
        (await authority.VerifyRegistrationAsync(root, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.VerifyRegistrationAsync(root with { WrapReceiptId = "substituted" }, TestContext.Current.CancellationToken)).ShouldBeFalse();
        var pin = new CustodyKeyLifecycleRequest(root.Identity, "pin-proof", CustodyKeyLifecycleAction.Pin, 0, "hold", "fence", 1, "decision");
        var outcome = await _custody.ExecuteAsync(root, pin, Digest(pin), TestContext.Current.CancellationToken);
        (await authority.VerifyOutcomeAsync(pin, outcome, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.VerifyOutcomeAsync(pin, outcome with { ReceiptId = "claimed" }, TestContext.Current.CancellationToken)).ShouldBeFalse();
        _authorization.Allowed = false;
        (await authority.VerifyOutcomeAsync(pin, outcome, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task CrossTenantContextCannotAuthenticateAndChangedManifestCannotRecover()
    {
        var root = await RootAsync();
        var other = await RootAsync(tenant: "tenant-b");
        var head = await _store.HeadAsync("tenant-a");
        var leaked = head.Targets.Single().Value.Wrapped;
        await Should.ThrowAsync<CryptographicException>(() => _store.DecryptAsync("tenant-b", leaked, TestContext.Current.CancellationToken));
        (await _custody.UnwrapAsync(root with { Identity = other.Identity }, "foreign-holder", cancellationToken: TestContext.Current.CancellationToken)).ShouldBeNull();
        var request = Request([root]);
        (await _custody.ConsumeAsync(request, "reservation", cancellationToken: TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        (await _custody.LookupAsync(request with { Targets = [new("tenant-a", "changed", "explicit-alias")] }, "reservation", cancellationToken: TestContext.Current.CancellationToken)).State.ShouldBe(DeletionManifestProviderState.Unknown);
    }
}
