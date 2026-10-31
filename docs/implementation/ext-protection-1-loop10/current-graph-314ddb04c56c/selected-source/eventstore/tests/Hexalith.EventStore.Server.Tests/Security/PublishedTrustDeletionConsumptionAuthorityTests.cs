using System.Text;
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Component evidence for published-trust dispatch checks. Fixture keys are not installed anchors or Available evidence.</summary>
public sealed class PublishedTrustDeletionConsumptionAuthorityTests
{
    /// <summary>A valid detached ES256, one published entry, and a fresh guard with one active attestation and committed dispatch admit the batch.</summary>
    [Fact]
    public async Task ValidDispatchReadsTheGuardTwiceAndDoesNotDestroy()
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await world.Guard.Received(2).ReadAsync(world.Request.Capability.TenantId, Arg.Any<CancellationToken>());
        await world.Guard.DidNotReceive().ExecuteAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>());
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await world.Guard.Received(4).ReadAsync(world.Request.Capability.TenantId, Arg.Any<CancellationToken>());
    }

    /// <summary>Absence, substitution, revocation, a mismatched batch, and an unreadable guard deny without a guard effect.</summary>
    [Fact]
    public async Task AbsentTrustOrGuardDeniesWithoutDestruction()
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var empty = new PublishedTrustDeletionConsumptionAuthority([], world.Guard);
        (await empty.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await new PublishedTrustDeletionConsumptionAuthority([world.Trust with { IsRevoked = true }], world.Guard)
            .VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await new PublishedTrustDeletionConsumptionAuthority([world.Trust, world.Trust], world.Guard)
            .VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        string tampered = world.Request.DetachedJws[..^1] + (world.Request.DetachedJws[^1] == 'A' ? "B" : "A");
        (await world.Authority.VerifyDispatchAsync(world.Request with { DetachedJws = tampered }, TestContext.Current.CancellationToken)).ShouldBeFalse();

        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((TenantGovernanceGuardState?)null);
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<TenantGovernanceGuardState?>(new InvalidOperationException("Guard read unavailable.")));
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();

        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.State with { Deletions = [] });
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.WithBatch(world.Batch with { AttestationOrdinal = 2 }));
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.State with { CompromisedKeyVersions = [world.Request.Capability.CapabilityKeyVersion] });
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.State, world.State with { Revision = world.State.Revision + 1 });
        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await world.Guard.DidNotReceive().ExecuteAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>());
        (await world.Authority.AuthorizeOperationAsync("tenant-a", "batch-a", "ReserveAndConsumeDeletionBatch", TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await world.Authority.ValidateStateAsync("tenant-a", 1, "digest", TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await world.Authority.VerifyAdmissionBlockAsync(null!, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await world.Authority.VerifyRevocationAsync(null!, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await world.Authority.VerifyActivationAsync(null!, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>A hold ordered after a committed dispatch does not cancel that batch. Another batch whose dispatch is not earlier stays restricted.</summary>
    [Fact]
    public async Task LaterHoldDoesNotCancelTheDispatchedBatchAndRestrictsAnother()
    {
        (PublishedTrustDispatchFixture admitted, DeletionBatchConsumptionRequest restricted, GovernanceBatchState restrictedBatch, IReadOnlyList<GovernanceProtocolReceipt> restrictedReceipts) = PublishedTrustDispatchFixture.CreateHoldPair();
        var hold = new GovernanceHoldState("hold-later", admitted.Scope, "policy-a", true, true, "");
        GovernanceProtocolReceipt holdReceipt = PublishedTrustDispatchFixture.HoldReceipt();
        TenantGovernanceGuardState later = admitted.State with { Revision = holdReceipt.GuardHighWater, Holds = [hold], Receipts = [.. admitted.State.Receipts, holdReceipt] };
        TenantGovernanceGuardState shared = later with
        {
            Revision = restricted.DispatchGuardRevision,
            Deletions = [admitted.Deletion with { Batches = [admitted.Batch, restrictedBatch] }],
            Receipts = [.. later.Receipts, .. restrictedReceipts],
        };
        var guard = Substitute.For<IGovernanceScopeGuard>();
        guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(later);
        var authority = new PublishedTrustDeletionConsumptionAuthority([admitted.Trust], guard);
        (await authority.VerifyDispatchAsync(admitted.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(shared);
        (await authority.VerifyDispatchAsync(admitted.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.VerifyDispatchAsync(restricted, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await guard.DidNotReceive().ExecuteAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Missing or malformed hold membership and release evidence cannot be treated as a nonrestrictive guard.</summary>
    [Theory]
    [InlineData("null-hold")]
    [InlineData("null-hold-scope")]
    [InlineData("foreign-hold-scope")]
    [InlineData("unknown-hold-scope")]
    [InlineData("ambiguous-hold-scope")]
    [InlineData("empty-hold-id")]
    [InlineData("empty-disposition")]
    [InlineData("duplicate-hold")]
    [InlineData("missing-release")]
    [InlineData("active-release")]
    [InlineData("future-registration")]
    [InlineData("empty-registration-operation")]
    [InlineData("empty-registration-receipt")]
    [InlineData("empty-registration-digest")]
    [InlineData("bad-registration-digest")]
    [InlineData("oversized-registration-operation")]
    [InlineData("oversized-registration-receipt")]
    [InlineData("future-dispatch")]
    [InlineData("null-deletion-scope")]
    [InlineData("foreign-deletion-scope")]
    [InlineData("unknown-deletion-scope")]
    [InlineData("ambiguous-deletion-scope")]
    public async Task MalformedGuardScopesAndUnattributedHoldsDeny(string mutation)
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        GovernanceProtocolReceipt registration = PublishedTrustDispatchFixture.HoldReceipt();
        var hold = new GovernanceHoldState("hold-later", world.Scope, "policy-a", true, true, "");
        GovernanceDeletionState deletion = world.Deletion;
        long revision = registration.GuardHighWater;
        switch (mutation)
        {
            case "null-hold": hold = null!; break;
            case "null-hold-scope": hold = hold with { Scope = null! }; break;
            case "foreign-hold-scope": hold = hold with { Scope = world.Scope with { TenantId = "tenant-b" } }; break;
            case "unknown-hold-scope": hold = hold with { Scope = world.Scope with { Kind = "FutureScope" } }; break;
            case "ambiguous-hold-scope": hold = hold with { Scope = world.Scope with { SourceConversationId = "conversation-a" } }; break;
            case "empty-hold-id": hold = hold with { HoldId = "" }; break;
            case "empty-disposition": hold = hold with { DispositionVersion = "" }; break;
            case "missing-release": hold = hold with { Active = false }; break;
            case "active-release": hold = hold with { ReleaseReceiptId = "release-authority" }; break;
            case "future-registration": registration = registration with { GuardHighWater = revision + 1 }; break;
            case "empty-registration-operation": registration = registration with { OperationId = "" }; break;
            case "empty-registration-receipt": registration = registration with { ReceiptId = "" }; break;
            case "empty-registration-digest": registration = registration with { IntentDigest = "" }; break;
            case "bad-registration-digest": registration = registration with { IntentDigest = new string('Z', 64) }; break;
            case "oversized-registration-operation": registration = registration with { OperationId = new string('O', 2049) }; break;
            case "oversized-registration-receipt": registration = registration with { ReceiptId = new string('R', 2049) }; break;
            case "future-dispatch": revision = world.Request.DispatchGuardRevision - 1; hold = hold with { Active = false, ReleaseReceiptId = "release-authority" }; break;
            case "null-deletion-scope": deletion = deletion with { Scope = null! }; break;
            case "foreign-deletion-scope": deletion = deletion with { Scope = world.Scope with { TenantId = "tenant-b" } }; break;
            case "unknown-deletion-scope": deletion = deletion with { Scope = world.Scope with { Kind = "FutureScope" } }; break;
            case "ambiguous-deletion-scope": deletion = deletion with { Scope = world.Scope with { SourceConversationId = "conversation-a" } }; break;
        }

        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.State with
        {
            Revision = revision,
            Holds = mutation == "duplicate-hold" ? [hold, hold] : [hold],
            Deletions = [deletion],
            Receipts = [.. world.State.Receipts, registration],
        });

        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await world.Guard.DidNotReceive().ExecuteAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Actual guard commits bind registration to its hold id and preserve independent release evidence without revoking a prior dispatch.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("caller-substituted-reference")]
    public async Task PersistedGuardHoldTransitionsPreserveEarlierDispatch(string callerReference)
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var guard = new GovernanceGuardFixture();
        TenantGovernanceGuardState state = world.State with
        {
            InstallationId = guard.Backend.Target.InstallationId,
            Deletions = [world.Deletion with { AdmissionFenceGuardRevision = 1 }],
        };
        guard.Backend.Stored[guard.Backend.Key(guard.Backend.Target.GuardCellId)] = JsonSerializer.SerializeToUtf8Bytes(
            new GuardedStateCell(state.TenantId, state.InstallationId, guard.Backend.Target.GuardCellId, state.Revision, JsonSerializer.SerializeToUtf8Bytes(state)));
        var authority = new PublishedTrustDeletionConsumptionAuthority([world.Trust], guard.Owner);
        (await authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();

        var hold = new GovernanceHoldCommand("hold-later", world.Scope, "policy-a", "");
        GovernanceGuardTransition registration = guard.Command(GovernanceGuardOperation.RegisterHold, hold: hold, reference: callerReference);
        GovernanceProtocolReceipt committed = await guard.Apply(registration);
        committed.Status.ShouldBe("Committed");
        committed.ReferenceId.ShouldBe(hold.HoldId);
        guard.State.Holds.Single().Active.ShouldBeTrue();
        guard.State.Receipts.Single(receipt => receipt.ReceiptId == committed.ReceiptId).ShouldBe(committed);
        (await authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await guard.Apply(registration)).ShouldBe(committed);

        const string releaseAuthority = "independent-acknowledged-release";
        GovernanceProtocolReceipt released = await guard.Apply(guard.Command(GovernanceGuardOperation.ReleaseHold,
            hold: hold with { ReleaseReceiptId = releaseAuthority }));
        released.Status.ShouldBe("Committed");
        guard.State.Holds.Single().ReleaseReceiptId.ShouldBe(releaseAuthority);
        guard.State.Holds.Single().Active.ShouldBeFalse();
        released.ReceiptId.ShouldNotBe(releaseAuthority);
        (await authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    /// <summary>Distinct accepted and containment batches retain independent active credentials under one deletion request.</summary>
    [Fact]
    public async Task DistinctDispatchedBatchesAdmitTheirOwnSoleAttestations()
    {
        var (first, second, secondBatch, secondReceipts) = PublishedTrustDispatchFixture.CreateHoldPair();
        TenantGovernanceGuardState shared = first.State with
        {
            Revision = second.DispatchGuardRevision,
            Deletions = [first.Deletion with { Batches = [first.Batch, secondBatch] }],
            Receipts = [.. first.State.Receipts, .. secondReceipts],
        };
        first.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(shared);

        (await first.Authority.VerifyDispatchAsync(first.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await first.Authority.VerifyDispatchAsync(second, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    /// <summary>An alternate current credential for the exact batch is ambiguous even if it has another digest or key version.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateBatchAttestationsDenyEvenWithDifferentKeyOrDigest(bool anotherDeletion)
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        GovernanceBatchState alternate = world.Batch with { AttestationOrdinal = 2, CapabilityKeyVersion = "key-v2", AttestationDigest = new string('A', 64) };
        TenantGovernanceGuardState ambiguous = world.State with
        {
            Deletions = anotherDeletion
                ? [world.Deletion, world.Deletion with { RequestId = "request-2", Batches = [alternate] }]
                : [world.Deletion with { Batches = [world.Batch, alternate] }],
        };
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ambiguous);

        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Guard receipts cannot change the signed issue revision or reuse one transition for issue and dispatch.</summary>
    [Theory]
    [InlineData("issue")]
    [InlineData("operation")]
    public async Task CorrelatedReceiptsCannotSubstituteTheSignedIssueOrDispatchOperation(string mutation)
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        GovernanceProtocolReceipt issue = world.State.Receipts[0];
        GovernanceProtocolReceipt dispatch = world.State.Receipts[1];
        DeletionBatchConsumptionRequest request = world.Request;
        GovernanceBatchState batch = world.Batch;
        if (mutation == "issue")
        {
            request = request with { CommittedIssuedGuardRevision = issue.GuardHighWater + 1 };
            batch = batch with { IssuedGuardRevision = request.CommittedIssuedGuardRevision };
            issue = issue with { GuardHighWater = request.CommittedIssuedGuardRevision };
        }
        else { dispatch = dispatch with { OperationId = issue.OperationId }; }
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.WithBatch(batch) with { Receipts = [issue, dispatch] });

        (await world.Authority.VerifyDispatchAsync(request, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Mutating a shared guard object between reads cannot turn a stale first observation into a matching pair.</summary>
    [Fact]
    public async Task MutationOfSharedGuardCollectionsBetweenReadsDenies()
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var deletions = new List<GovernanceDeletionState> { world.Deletion with { IntegrityCompromised = true } };
        TenantGovernanceGuardState shared = world.State with { Deletions = deletions };
        int reads = 0;
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (++reads == 2) { deletions[0] = world.Deletion; }
            return shared;
        });

        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        reads.ShouldBe(2);
    }

    /// <summary>The compared guard bytes remain authoritative when a shared collection changes during admission.</summary>
    [Fact]
    public async Task AdmissionUsesTheComparedGuardSnapshotWhenSharedHoldsChange()
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var hold = new GovernanceHoldState("hold-later", world.Scope, "policy-a", true, true, "");
        var holds = Substitute.For<IReadOnlyList<GovernanceHoldState>>();
        int enumerations = 0;
        holds.Count.Returns(1);
        holds[0].Returns(hold);
        holds.GetEnumerator().Returns(_ => (++enumerations <= 2 ? new[] { hold } : Array.Empty<GovernanceHoldState>())
            .AsEnumerable().GetEnumerator());
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.State with
        {
            Holds = holds,
            Receipts = [.. world.State.Receipts, PublishedTrustDispatchFixture.HoldReceipt() with { GuardHighWater = 9 }],
        });

        (await world.Authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>The trusted public key must be exactly one complete SPKI object.</summary>
    [Fact]
    public async Task PublicAnchorWithTrailingBytesDeniesBeforeGuardRead()
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var authority = new PublishedTrustDeletionConsumptionAuthority(
            [world.Trust with { SubjectPublicKeyInfo = [.. world.Trust.SubjectPublicKeyInfo, 0] }], world.Guard);

        (await authority.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await world.Guard.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
    }

    /// <summary>The engine opt-in registers only the two actors. Missing custody and authority services deny the first effect.</summary>
    [Fact]
    public async Task OptInResolvesActorsWithoutCreatingCustodyAndDeniesTheFirstEffect()
    {
        var defaults = new ServiceCollection();
        defaults.AddEventStoreServer(new ConfigurationBuilder().Build());
        using ServiceProvider defaultProvider = defaults.BuildServiceProvider();
        defaultProvider.GetRequiredService<IEventPayloadProtectionService>().ShouldBeOfType<NoOpEventPayloadProtectionService>();
        ActorNames(defaultProvider).ShouldNotContain(DeletionConsumptionActor.ActorTypeName);
        ActorNames(defaultProvider).ShouldNotContain(InteractionOccurrenceRegistryActor.ActorTypeName);

        var services = new ServiceCollection();
        services.AddInteractionPayloadProtection();
        using ServiceProvider provider = services.BuildServiceProvider();
        ActorNames(provider).ShouldContain(DeletionConsumptionActor.ActorTypeName);
        ActorNames(provider).ShouldContain(InteractionOccurrenceRegistryActor.ActorTypeName);
        provider.GetService<IInteractionOccurrenceKeyProvider>().ShouldBeNull();
        provider.GetService<IInteractionProtectionBindingProvider>().ShouldBeNull();
        provider.GetService<IAtomicDeletionManifestProvider>().ShouldBeNull();
        provider.GetService<IDeletionConsumptionAuthority>().ShouldBeNull();
        provider.GetService<ProtectionCurrentAnchorJournal>().ShouldBeNull();
        provider.GetService<IInteractionOccurrenceAuthority>().ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IEventPayloadProtectionService>());

        DeletionConsumptionActor consumption = DeletionConsumptionFixture.Create(new InMemoryStateManager());
        (await consumption.RegisterAsync(DeletionConsumptionFixture.Request())).Status.ShouldBe(DeletionConsumptionStatus.Unavailable);
        InteractionOccurrenceRegistryActor registry = InteractionOccurrenceFixture.Create(new InMemoryStateManager(), null);
        (await registry.ReserveAsync(InteractionOccurrenceFixture.Request())).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
    }

    /// <summary>Explicit binding registration exposes only the fail-closed adapters. The first effect still denies and writes nothing.</summary>
    [Fact]
    public async Task ExplicitBindingsFailClosedWithoutInstalledDeliveryOrAnchor()
    {
        var services = new ServiceCollection();
        services.AddInteractionProtectionBindings();
        using ServiceProvider provider = services.BuildServiceProvider();
        IInteractionOccurrenceKeyProvider keys = provider.GetRequiredService<IInteractionOccurrenceKeyProvider>();
        provider.GetRequiredService<IInteractionProtectionBindingProvider>().ShouldNotBeNull();
        provider.GetRequiredService<ProtectionCurrentAnchorJournal>().ShouldNotBeNull();
        IInteractionOccurrenceAuthority occurrences = provider.GetRequiredService<IInteractionOccurrenceAuthority>();
        provider.GetRequiredService<IGovernanceGuardAuthority>().ShouldNotBeNull();
        provider.GetRequiredService<IGuardedStateTransactionAuthority>().ShouldNotBeNull();
        provider.GetService<IInteractionOccurrenceKeyDelivery>().ShouldBeNull();
        provider.GetService<IDeletionConsumptionAuthority>().ShouldBeNull();
        provider.GetService<IEventPayloadProtectionService>().ShouldBeNull();
        (await keys.ResolveRootAsync(InteractionOccurrenceFixture.Request().Identity, InteractionOccurrenceFixture.Request().ProposedKeyReference, true, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await occurrences.GetInstalledEpochAsync("tenant-a", TestContext.Current.CancellationToken)).ShouldBeNull();

        var occurrenceState = new InMemoryStateManager();
        InteractionOccurrenceRegistryActor registry = InteractionOccurrenceFixture.Create(
            occurrenceState, new InteractionOccurrenceAuthority(null, new ProtectionCurrentAnchorJournal(null)));
        (await registry.ReserveAsync(InteractionOccurrenceFixture.Request())).Status.ShouldBe(InteractionOccurrenceReservationStatus.Unavailable);
        occurrenceState.CommittedState.ShouldBeEmpty();

        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var consumptionState = new InMemoryStateManager();
        DeletionConsumptionActor consumption = DeletionConsumptionFixture.Create(
            consumptionState, new PublishedTrustDeletionConsumptionAuthority([world.Trust], world.Guard));
        (await consumption.RegisterAsync(DeletionConsumptionFixture.Request())).Status.ShouldBe(DeletionConsumptionStatus.Unavailable);
        consumptionState.CommittedState.ShouldBeEmpty();
        await world.Guard.DidNotReceive().ExecuteAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Occurrence checks deny a missing installation, a divergent restore, and a plaintext seal. A verified revision does not record an anchor.</summary>
    [Fact]
    public async Task OccurrenceAuthorityDeniesMissingInstallationAndDoesNotRecord()
    {
        var journal = Substitute.For<IAnchoredStateTransitionAuthority>();
        journal.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(true);
        var absent = new InteractionOccurrenceAuthority(null, journal);
        InteractionOccurrenceRequest request = InteractionOccurrenceFixture.Request();
        (await absent.AuthorizeOperationAsync(request.Identity, "Reserve", TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await absent.ValidateStateAsync("tenant-a", "epoch-1", 1, new string('A', 64), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await absent.AdmitTransitionAsync(SampleTransition(), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await absent.GetInstalledEpochAsync("tenant-a", TestContext.Current.CancellationToken)).ShouldBeNull();
        await journal.DidNotReceive().AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());

        var installation = Substitute.For<IInteractionOccurrenceInstallation>();
        installation.MatchesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var divergent = new InteractionOccurrenceAuthority(installation, journal);
        (await divergent.ValidateStateAsync("tenant-a", "epoch-1", 1, new string('A', 64), TestContext.Current.CancellationToken)).ShouldBeFalse();

        installation.MatchesAsync("tenant-a", "epoch-1", 1, new string('A', 64), Arg.Any<CancellationToken>()).Returns(true);
        (await divergent.ValidateStateAsync("tenant-a", "epoch-1", 1, new string('A', 64), TestContext.Current.CancellationToken)).ShouldBeTrue();
        installation.MatchesAsync("tenant-a", "epoch-1", 1, new string('B', 64), Arg.Any<CancellationToken>()).Returns(true);
        (await divergent.RecordRevisionAsync("tenant-a", "epoch-1", 0, 1, new string('B', 64), TestContext.Current.CancellationToken)).ShouldBeTrue();
        await journal.DidNotReceive().RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());

        var record = new InteractionOccurrenceRecord(request, request.ProposedKeyReference, 1, InteractionOccurrenceWriterState.Reserved, null, null);
        installation.VerifySealedAsync(Arg.Any<InteractionOccurrenceRecord>(), Arg.Any<InteractionOccurrenceSealedResult>(), Arg.Any<CancellationToken>()).Returns(true);
        (await divergent.VerifySealedAsync(record, InteractionOccurrenceFixture.Sealed() with { SerializationFormat = "json" }, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await installation.DidNotReceive().VerifySealedAsync(Arg.Any<InteractionOccurrenceRecord>(), Arg.Any<InteractionOccurrenceSealedResult>(), Arg.Any<CancellationToken>());
        (await divergent.VerifySealedAsync(record, InteractionOccurrenceFixture.Sealed(), TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await divergent.VerifySealedAsync(record with { WriterState = InteractionOccurrenceWriterState.Aborted }, InteractionOccurrenceFixture.Sealed(), TestContext.Current.CancellationToken)).ShouldBeFalse();
        await installation.Received(1).VerifySealedAsync(Arg.Any<InteractionOccurrenceRecord>(), Arg.Any<InteractionOccurrenceSealedResult>(), Arg.Any<CancellationToken>());

        journal.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(false);
        installation.MatchesAsync("tenant-a", "epoch-1", 2, new string('C', 64), Arg.Any<CancellationToken>()).Returns(true);
        (await divergent.ValidateStateAsync("tenant-a", "epoch-1", 2, new string('C', 64), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await divergent.RecordRevisionAsync("tenant-a", "epoch-1", 1, 2, new string('C', 64), TestContext.Current.CancellationToken)).ShouldBeFalse();

        AnchoredStateTransition transition = SampleTransition();
        journal.AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(true);
        journal.RecoverTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(true);
        journal.RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(true);
        journal.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(true);
        (await divergent.AdmitTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await divergent.RecoverTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await divergent.RecordTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await divergent.VerifyTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeTrue();
        journal.AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(false);
        journal.RecoverTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(false);
        journal.RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(false);
        journal.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(false);
        (await divergent.AdmitTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await divergent.RecoverTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await divergent.RecordTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await divergent.VerifyTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Guard evidence is returned only for the exact installed receipt. Missing provenance returns null.</summary>
    [Fact]
    public async Task GuardAuthorityReturnsOnlyExactInstalledEvidence()
    {
        (await new GovernanceGuardAuthority(null).ReadLookupAsync(GuardTransition(), new string('A', 64), new string('B', 64), TestContext.Current.CancellationToken)).ShouldBeNull();
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var installation = Substitute.For<IGovernanceGuardInstallation>();
        var authority = new GovernanceGuardAuthority(installation);
        GovernanceProtocolReceipt receipt = world.State.Receipts[0];
        var recovery = new GovernanceSigningRecovery(world.Request.Capability, receipt.SigningRequestId, receipt.DetachedJwsDigest, receipt.ReceiptId, world.State.Revision, "key-v1", "authority-1", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1));
        installation.ReadSigningRecoveryAsync(receipt, world.State, Arg.Any<CancellationToken>()).Returns(recovery);
        (await authority.ReadSigningRecoveryAsync(receipt, world.State, TestContext.Current.CancellationToken)).ShouldBe(recovery);
        GovernanceProtocolReceipt blankDigest = receipt with { DetachedJwsDigest = "" };
        GovernanceProtocolReceipt blankSigning = receipt with { SigningRequestId = "" };
        installation.ReadSigningRecoveryAsync(blankDigest, world.State, Arg.Any<CancellationToken>()).Returns(recovery);
        installation.ReadSigningRecoveryAsync(blankSigning, world.State, Arg.Any<CancellationToken>()).Returns(recovery);
        (await authority.ReadSigningRecoveryAsync(blankDigest, world.State, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await authority.ReadSigningRecoveryAsync(blankSigning, world.State, TestContext.Current.CancellationToken)).ShouldBeNull();

        GovernanceGuardEvidence evidence = GuardEvidence("intent", "mutation", "receipt-1", "resource-1");
        installation.ReadLookupAsync(Arg.Any<GovernanceGuardTransition>(), "intent", "mutation", Arg.Any<CancellationToken>()).Returns(evidence);
        (await authority.ReadLookupAsync(GuardTransition(), "intent", "mutation", TestContext.Current.CancellationToken)).ShouldBe(evidence);
        (await authority.ReadLookupAsync(GuardTransition(), "other", "mutation", TestContext.Current.CancellationToken)).ShouldBeNull();
        installation.ReadLookupAsync(Arg.Any<GovernanceGuardTransition>(), "blank", "mutation", Arg.Any<CancellationToken>()).Returns(evidence with { AuthorityReceiptId = "" });
        (await authority.ReadLookupAsync(GuardTransition(), "blank", "mutation", TestContext.Current.CancellationToken)).ShouldBeNull();
        GovernanceGuardEvidence append = GuardEvidence("append", "mutation", "receipt-1", "");
        installation.ReadAsync(Arg.Any<GovernanceGuardTransition>(), "append", "mutation", world.State, Arg.Any<CancellationToken>()).Returns(append);
        (await authority.ReadAsync(GuardTransition(), "append", "mutation", world.State, TestContext.Current.CancellationToken)).ShouldBeNull();
        installation.ReadAsync(Arg.Any<GovernanceGuardTransition>(), "append", "mutation", world.State, Arg.Any<CancellationToken>()).Returns(append with { AppendResourceId = "resource-1" });
        (await authority.ReadAsync(GuardTransition(), "append", "mutation", world.State, TestContext.Current.CancellationToken)).ShouldBe(append with { AppendResourceId = "resource-1" });
    }

    /// <summary>A restored absent cell is not a fresh empty cell. A target that is not current is denied.</summary>
    [Fact]
    public async Task GuardedCellDeniesRestoredAbsenceAndAStaleTarget()
    {
        (await new GuardedStateTransactionAuthority(null).AuthorizeReadAsync(CellTarget(), "cell-1", TestContext.Current.CancellationToken)).ShouldBeFalse();
        var installation = Substitute.For<IGuardedStateInstallation>();
        GuardedStateTransactionTarget target = CellTarget();
        installation.GetCurrentAsync(target.TenantId, Arg.Any<CancellationToken>()).Returns(target);
        var authority = new GuardedStateTransactionAuthority(installation);
        installation.ReadCellAsync(target, "cell-1", null, Arg.Any<CancellationToken>()).Returns(GuardedCellHistory.RestoredAbsent);
        (await authority.ValidateCellAsync(target, null, "cell-1", TestContext.Current.CancellationToken)).ShouldBeFalse();
        installation.ReadCellAsync(target, "cell-1", null, Arg.Any<CancellationToken>()).Returns(GuardedCellHistory.NeverWritten);
        (await authority.ValidateCellAsync(target, null, "cell-1", TestContext.Current.CancellationToken)).ShouldBeTrue();
        var occupied = new GuardedStateCell(target.TenantId, target.InstallationId, "cell-1", 1, "value"u8.ToArray());
        installation.ReadCellAsync(target, "cell-1", occupied, Arg.Any<CancellationToken>()).Returns(GuardedCellHistory.Occupied);
        (await authority.ValidateCellAsync(target, occupied, "cell-1", TestContext.Current.CancellationToken)).ShouldBeTrue();
        installation.ReadCellAsync(target, "cell-1", null, Arg.Any<CancellationToken>()).Returns(GuardedCellHistory.Occupied);
        (await authority.ValidateCellAsync(target, null, "cell-1", TestContext.Current.CancellationToken)).ShouldBeFalse();
        installation.ReadCellAsync(target, "cell-1", occupied, Arg.Any<CancellationToken>()).Returns(GuardedCellHistory.RestoredAbsent);
        (await authority.ValidateCellAsync(target, occupied, "cell-1", TestContext.Current.CancellationToken)).ShouldBeFalse();
        installation.GetCurrentAsync(target.TenantId, Arg.Any<CancellationToken>()).Returns(target with { AuthorityRevision = "other" });
        installation.AuthorizeReadAsync(Arg.Any<GuardedStateTransactionTarget>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        (await authority.AuthorizeReadAsync(target, "cell-1", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>The journal admits one original, advances it once, and leaves a lost acknowledgement unresolved until lookup.</summary>
    [Fact]
    public async Task JournalAdvancesOneOriginalAndResolvesALostRecordOnlyByLookup()
    {
        var records = new Dictionary<string, ProtectionAnchorRecord>(StringComparer.Ordinal);
        ProtectionAnchorRecord? lookup = null;
        bool loseAdvance = true;
        int advances = 0;
        IProtectionAnchorStore store = AnchorStore(records, () => loseAdvance, () => advances++, value => lookup = value, () => lookup);
        var journal = new ProtectionCurrentAnchorJournal(store);
        AnchoredStateTransition original = SampleTransition();
        (await journal.AdmitTransitionAsync(original, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await journal.RecordTransitionAsync(original, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await journal.VerifyTransitionAsync(original, TestContext.Current.CancellationToken)).ShouldBeFalse();
        lookup!.Disposition.ShouldBe(ProtectionAnchorDisposition.Unknown);
        advances.ShouldBe(1);
        lookup = new(CopyTransition(original), ProtectionAnchorDisposition.Advanced, original.TargetRevision);
        (await journal.VerifyTransitionAsync(original, TestContext.Current.CancellationToken)).ShouldBeTrue();
        advances.ShouldBe(1);
        loseAdvance = false;
        (await journal.RecordTransitionAsync(original, TestContext.Current.CancellationToken)).ShouldBeTrue();
        advances.ShouldBe(2);
        AnchoredStateTransition other = SampleTransition("other-original"u8.ToArray());
        (await journal.AdmitTransitionAsync(other, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await journal.RecordTransitionAsync(other, TestContext.Current.CancellationToken)).ShouldBeFalse();
        advances.ShouldBe(2);

        var recoverStore = Substitute.For<IProtectionAnchorStore>();
        ProtectionAnchorRecord admitted = new(CopyTransition(original), ProtectionAnchorDisposition.Admitted, original.ExpectedRevision);
        recoverStore.RecoverAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
            ExactTransition(call.Arg<AnchoredStateTransition>(), original) ? ProtectionAnchorDisposition.Admitted : ProtectionAnchorDisposition.Conflict);
        recoverStore.LookupAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
            ExactTransition(call.Arg<AnchoredStateTransition>(), original) ? admitted : null);
        (await new ProtectionCurrentAnchorJournal(recoverStore).RecoverTransitionAsync(original, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await recoverStore.DidNotReceive().AdvanceAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<long>(), Arg.Any<CancellationToken>());

        var closed = new ProtectionCurrentAnchorJournal(null);
        (await closed.AdmitTransitionAsync(original, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await closed.RecordTransitionAsync(original, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await closed.RecoverTransitionAsync(original, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await closed.VerifyTransitionAsync(original, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Exact injected evidence can pass. Missing, stale, revoked, substituted, or cross-tenant evidence denies, and this authority does not mint an anchor.</summary>
    [Fact]
    public async Task PublishedTrustAcceptsExactEvidenceAndDeniesAMismatchWithoutIssuingAnAnchor()
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create(attestationOrdinal: 2);
        const string goodDigest = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var journal = Substitute.For<IAnchoredStateTransitionAuthority>();
        string rejected = new string('B', 64);
        journal.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            string text = Encoding.UTF8.GetString(call.Arg<AnchoredStateTransition>().TargetBytes);
            return !text.Contains(rejected, StringComparison.Ordinal);
        });
        journal.AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(true);
        var revocations = Substitute.For<IPublishedTrustRevocationEvidence>();
        bool revocationPending = false;
        bool activationPending = false;
        revocations.Matches(Arg.Any<DeletionCapabilityRevocationEnvelope>()).Returns(call => call.Arg<DeletionCapabilityRevocationEnvelope>().SignatureDigest == goodDigest);
        revocations.Pending(Arg.Any<DeletionCapabilityRevocationEnvelope>()).Returns(_ => revocationPending);
        revocations.MatchesActivation(Arg.Any<DeletionReattestationActivation>()).Returns(true);
        revocations.PendingActivation(Arg.Any<DeletionReattestationActivation>()).Returns(_ => activationPending);
        var authority = new PublishedTrustDeletionConsumptionAuthority([world.Trust], world.Guard, journal, revocations);
        var block = new DeletionBatchBlockRequest("tenant-a", "batch-a", "block-1", goodDigest);
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.State with
        {
            Receipts = [.. world.State.Receipts, Receipt(goodDigest, "batch-a")],
        });
        (await authority.VerifyAdmissionBlockAsync(block, TestContext.Current.CancellationToken)).ShouldBeTrue();
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.State with
        {
            Receipts = [.. world.State.Receipts, Receipt(goodDigest, "batch-a") with { Status = "Pending" }],
        });
        (await authority.VerifyAdmissionBlockAsync(block, TestContext.Current.CancellationToken)).ShouldBeFalse();

        var envelope = new DeletionCapabilityRevocationEnvelope("issuer-a", "protection-a", "tenant-a", "DeletionBatchCapabilitySigningKey", "key-v1", 1, 3, "event-1", goodDigest);
        (await authority.VerifyRevocationAsync(envelope, TestContext.Current.CancellationToken)).ShouldBeTrue();
        revocationPending = true;
        (await authority.VerifyRevocationAsync(envelope, TestContext.Current.CancellationToken)).ShouldBeFalse();
        revocationPending = false;
        (await new PublishedTrustDeletionConsumptionAuthority([world.Trust with { IsRevoked = true }], world.Guard, journal, revocations)
            .VerifyRevocationAsync(envelope, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.VerifyRevocationAsync(envelope with { TenantId = "tenant-b" }, TestContext.Current.CancellationToken)).ShouldBeFalse();

        DeletionReattestationActivation activation = Activation(world, world.Request);
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ActivationState(world));
        (await authority.VerifyActivationAsync(activation, TestContext.Current.CancellationToken)).ShouldBeTrue();
        activationPending = true;
        (await authority.VerifyActivationAsync(activation, TestContext.Current.CancellationToken)).ShouldBeFalse();
        activationPending = false;
        (await authority.VerifyActivationAsync(activation with { Replacement = world.Request with { DispatchGuardRevision = world.State.Revision + 1 } }, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.VerifyActivationAsync(Activation(world, world.Request with { Capability = world.Request.Capability with { ManifestDigest = goodDigest } }), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.VerifyActivationAsync(Activation(world, world.Request with { Capability = world.Request.Capability with { TenantId = "tenant-b" } }), TestContext.Current.CancellationToken)).ShouldBeFalse();
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ActivationState(world, "AdmissionBlocked"));
        (await authority.VerifyActivationAsync(activation, TestContext.Current.CancellationToken)).ShouldBeFalse();

        // Separate restrictive evidence is not installation or owner-state authority.
        (await authority.AuthorizeOperationAsync("tenant-a", "batch-a", "ReserveAndConsumeDeletionBatch", TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.ValidateStateAsync("tenant-a", world.State.Revision, goodDigest, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.RecordRevisionAsync("tenant-a", world.State.Revision - 1, world.State.Revision, goodDigest, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.AdmitTransitionAsync(SampleTransition(), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.RecordTransitionAsync(SampleTransition(), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.RecoverTransitionAsync(SampleTransition(), TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.VerifyTransitionAsync(SampleTransition(), TestContext.Current.CancellationToken)).ShouldBeFalse();
        await journal.DidNotReceive().AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        await journal.DidNotReceive().RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());

    }

    private static IProtectionAnchorStore AnchorStore(
        Dictionary<string, ProtectionAnchorRecord> records,
        Func<bool> loseAdvance,
        Action countAdvance,
        Action<ProtectionAnchorRecord?> setLookup,
        Func<ProtectionAnchorRecord?> getLookup)
    {
        var store = Substitute.For<IProtectionAnchorStore>();
        store.AdmitAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            AnchoredStateTransition transition = CopyTransition(call.Arg<AnchoredStateTransition>());
            if (records.TryGetValue(transition.ScopeId, out ProtectionAnchorRecord? existing))
            {
                return ExactTransition(existing.Transition, transition) ? ProtectionAnchorDisposition.Admitted : ProtectionAnchorDisposition.Conflict;
            }

            records[transition.ScopeId] = new(transition, ProtectionAnchorDisposition.Admitted, transition.ExpectedRevision);
            return ProtectionAnchorDisposition.Admitted;
        });
        store.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            records.TryGetValue(call.Arg<string>(), out ProtectionAnchorRecord? record) ? record : null);
        store.AdvanceAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            countAdvance();
            AnchoredStateTransition transition = CopyTransition(call.Arg<AnchoredStateTransition>());
            if (loseAdvance())
            {
                setLookup(new(transition, ProtectionAnchorDisposition.Unknown, transition.ExpectedRevision));
                return ProtectionAnchorDisposition.Unknown;
            }

            records[transition.ScopeId] = new(transition, ProtectionAnchorDisposition.Advanced, transition.TargetRevision);
            setLookup(records[transition.ScopeId]);
            return ProtectionAnchorDisposition.Advanced;
        });
        store.LookupAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(_ => getLookup());
        return store;
    }

    private static GovernanceGuardTransition GuardTransition()
        => new("tenant-a", "append-1", GovernanceGuardOperation.AppendWrite, 1, "epoch-1", "", null, null, null, null, null, null, "", "");

    private static GovernanceGuardEvidence GuardEvidence(string intent, string mutation, string receiptId, string appendResourceId)
        => new("tenant-a", intent, mutation, "authority-1", receiptId, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), [], [], [], "writer", "legacy", "zero", 0, "observed", "v1", [], "block")
        {
            AppendResourceId = appendResourceId,
        };

    private static GuardedStateTransactionTarget CellTarget()
        => new("tenant-a", "qualified-synthetic-component", "installed-tenant-partition", "source-installation", "tenant-guard", "complete-writer-revocation", "independent-current-authority", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1));

    private static GovernanceProtocolReceipt Receipt(string receiptId, string referenceId)
        => new("evidence", new string('D', 64), "Committed", 10, 1, referenceId, receiptId);

    private static DeletionReattestationActivation Activation(PublishedTrustDispatchFixture world, DeletionBatchConsumptionRequest request)
        => new("activate-1", "compromise-1", 0, world.Batch.IssueReceiptId, request);

    private static TenantGovernanceGuardState ActivationState(PublishedTrustDispatchFixture world, string outcome = "ReplacementAwaitingActivation")
        => world.WithBatch(world.Batch with { ProtectionOutcome = outcome, ProtectionReceiptId = "compromise-1" }) with
        {
            Receipts =
            [
                .. world.State.Receipts,
                Receipt("compromise-1", world.Request.Capability.BatchId),
                Receipt("replacement-guard-1", world.Request.Capability.BatchId),
            ],
        };

    private static AnchoredStateTransition SampleTransition(byte[]? bytes = null)
    {
        byte[] owned = bytes ?? "original-anchor"u8.ToArray();
        string target = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(owned));
        string predecessor = new string('C', 64);
        return new("scope-a", 0, 1, predecessor, target, owned);
    }

    private static AnchoredStateTransition CopyTransition(AnchoredStateTransition transition)
        => transition with { TargetBytes = transition.TargetBytes.ToArray() };

    private static bool ExactTransition(AnchoredStateTransition left, AnchoredStateTransition right)
        => left.ScopeId == right.ScopeId
            && left.ExpectedRevision == right.ExpectedRevision
            && left.TargetRevision == right.TargetRevision
            && left.PredecessorDigest == right.PredecessorDigest
            && left.TargetDigest == right.TargetDigest
            && left.TargetBytes.AsSpan().SequenceEqual(right.TargetBytes);

    private static IEnumerable<string> ActorNames(ServiceProvider provider)
        => provider.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value.Actors.Select(registration => registration.Type.ActorTypeName);
}
