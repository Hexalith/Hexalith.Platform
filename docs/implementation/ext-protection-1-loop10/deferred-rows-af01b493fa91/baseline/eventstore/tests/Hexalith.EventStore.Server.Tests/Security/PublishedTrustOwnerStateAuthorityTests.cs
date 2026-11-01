using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Actual published-trust/actor composition with independent in-memory enrolled owner state; source component evidence only.</summary>
public sealed class PublishedTrustOwnerStateAuthorityTests
{
    /// <summary>Exact owner currentness and private permissions use their independent tenant authority and never the guard revision or synthetic journal proofs.</summary>
    [Fact]
    public async Task EnrolledGenesisAndDivergentRevisionsUseExactOwnerAuthority()
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var owner = new EnrolledDeletionOwnerFixture();
        owner.Permissions.Add((world.Request.Capability.BatchId, "RegisterDeletionBatch"));
        var authority = Authority(world, owner);
        string genesis = owner.Digest;
        (await authority.ValidateStateAsync("tenant-a", 0, genesis, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.AuthorizeOperationAsync("tenant-a", world.Request.Capability.BatchId, "RegisterDeletionBatch", TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.AuthorizeOperationAsync("tenant-a", "other", "RegisterDeletionBatch", TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.AuthorizeOperationAsync("tenant-a", world.Request.Capability.BatchId, "ReserveAndConsumeDeletionBatch", TestContext.Current.CancellationToken)).ShouldBeFalse();
        AnchoredStateTransition first = Transition(owner, "owner-one"u8.ToArray());
        (await authority.RecordRevisionAsync("tenant-a", 0, 1, first.TargetDigest, TestContext.Current.CancellationToken)).ShouldBeFalse();
        owner.Revision.ShouldBe(0);
        (await authority.AdmitTransitionAsync(first, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.RecordTransitionAsync(first, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.ValidateStateAsync("tenant-a", 1, first.TargetDigest, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.ValidateStateAsync("tenant-a", 0, genesis, TestContext.Current.CancellationToken)).ShouldBeFalse();
        world.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(world.State with { Revision = 91 });
        AnchoredStateTransition second = Transition(owner, "owner-two"u8.ToArray());
        (await authority.AdmitTransitionAsync(second, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.RecordTransitionAsync(second, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.VerifyTransitionAsync(first, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.VerifyTransitionAsync(second, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.RecordRevisionAsync("tenant-a", 1, 2, second.TargetDigest, TestContext.Current.CancellationToken)).ShouldBeTrue();
        owner.Revision.ShouldBe(2);
        owner.Calls.All(call => ReferenceEquals(call.Transition, first) || ReferenceEquals(call.Transition, second)).ShouldBeTrue();
        await world.Guard.DidNotReceive().ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Absent, unavailable, foreign, ambiguous, changed or noncanonical enrollment cannot grant currentness, private calls or transitions.</summary>
    [Theory]
    [InlineData("absent")]
    [InlineData("unavailable")]
    [InlineData("foreign-tenant")]
    [InlineData("foreign-scope")]
    [InlineData("duplicate")]
    [InlineData("conflicting-scope")]
    [InlineData("changed-genesis")]
    [InlineData("uppercase-tenant")]
    public async Task MissingOrSubstitutedEnrollmentDenies(string mutation)
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var owner = new EnrolledDeletionOwnerFixture();
        owner.Permissions.Add(("batch-a", "RegisterDeletionBatch"));
        string genesis = owner.Digest;
        IReadOnlyList<IDeletionConsumptionOwnerStateAuthority> owners = [owner];
        string tenant = "tenant-a";
        switch (mutation)
        {
            case "absent": owners = []; break;
            case "unavailable": owner.Enrolled = false; break;
            case "foreign-tenant": owners = [new EnrolledDeletionOwnerFixture("tenant-b")]; break;
            case "foreign-scope": owners = [new EnrolledDeletionOwnerFixture(scope: DeletionConsumptionActor.GetOwnerScope("tenant-b"))]; break;
            case "duplicate": owners = [owner, owner]; break;
            case "conflicting-scope": owners = [owner, new EnrolledDeletionOwnerFixture(scope: "foreign")]; break;
            case "changed-genesis": genesis = new string('B', 64); break;
            case "uppercase-tenant": tenant = "TENANT-A"; break;
        }

        var authority = new PublishedTrustDeletionConsumptionAuthority([world.Trust], world.Guard, null, null, owners);
        (await authority.ValidateStateAsync(tenant, 0, genesis, TestContext.Current.CancellationToken)).ShouldBeFalse();
        if (mutation != "changed-genesis")
        {
            (await authority.AuthorizeOperationAsync(tenant, "batch-a", "RegisterDeletionBatch", TestContext.Current.CancellationToken)).ShouldBeFalse();
            var transition = Transition(owner, "target"u8.ToArray());
            if (mutation == "uppercase-tenant") { transition = transition with { ScopeId = "TENANT-A:protection:deletion-consumption-v23|deletion-consumption-v23" }; }
            (await authority.AdmitTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeFalse();
            (await authority.RecordTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeFalse();
            (await authority.RecoverTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeFalse();
            (await authority.VerifyTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeFalse();
        }

        owner.Revision.ShouldBe(0);
    }

    /// <summary>Actual actor originals survive both lost independent record acknowledgement and pre-record outage; exact recovery never reissues a physical effect.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualActorTransitionsRecoverOriginalThenAdvanceConsecutively(bool recordCommitted)
    {
        var (world, second, secondBatch, secondReceipts) = PublishedTrustDispatchFixture.CreateHoldPair();
        var owner = new EnrolledDeletionOwnerFixture { RecordAvailable = recordCommitted, LoseRecordAcknowledgement = recordCommitted };
        foreach (string batch in new[] { world.Request.Capability.BatchId, second.Capability.BatchId })
        {
            owner.Permissions.Add((batch, "RegisterDeletionBatch"));
            owner.Permissions.Add((batch, "LookupDeletionBatch"));
        }

        var authority = Authority(world, owner);
        var backend = new InMemoryStateManager();
        var physical = Substitute.For<IAtomicDeletionManifestProvider>();
        DeletionConsumptionActor actor = DeletionConsumptionFixture.Create(backend, authority, physical);
        await Should.ThrowAsync<InvalidOperationException>(() => actor.RegisterAsync(world.Request));
        AnchoredStateTransition original = backend.CommittedState.Single().Value.ShouldBeOfType<AnchoredStateTransition>();
        string exact = JsonSerializer.Serialize(original);
        owner.Revision.ShouldBe(recordCommitted ? 1 : 0);
        owner.LoseRecordAcknowledgement = false;
        actor = DeletionConsumptionFixture.Create(backend, authority, physical);
        if (!recordCommitted)
        {
            (await actor.LookupAsync("tenant-a", world.Request.Capability.BatchId)).Status.ShouldBe(DeletionConsumptionStatus.Unavailable);
            JsonSerializer.Serialize(backend.CommittedState.Single().Value).ShouldBe(exact);
        }

        (await actor.RegisterAsync(world.Request)).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        DeletionConsumptionLedger first = backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>();
        first.Revision.ShouldBe(1);
        DeletionConsumptionIdentity.Digest(first.Batches.Single().Original).ShouldBe(DeletionConsumptionIdentity.Digest(world.Request));
        owner.RecordAvailable = true;
        world.Guard.ReadAsync("tenant-a", Arg.Any<CancellationToken>()).Returns(world.State with
        {
            Revision = second.DispatchGuardRevision,
            Deletions = [world.Deletion with { Batches = [world.Batch, secondBatch] }],
            Receipts = [.. world.State.Receipts, .. secondReceipts],
        });
        (await actor.RegisterAsync(second)).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        DeletionConsumptionLedger next = backend.CommittedState.Single().Value.ShouldBeOfType<DeletionConsumptionLedger>();
        next.Revision.ShouldBe(2);
        next.Batches.Select(batch => batch.Current.Capability.BatchId).ShouldBe([world.Request.Capability.BatchId, second.Capability.BatchId]);
        (await authority.ValidateStateAsync("tenant-a", 2, DeletionConsumptionIdentity.Digest(next), TestContext.Current.CancellationToken)).ShouldBeTrue();
        owner.Calls.Where(call => call.Transition.TargetRevision == 1).All(call => JsonSerializer.Serialize(call.Transition) == exact).ShouldBeTrue();
        owner.Calls.Count(call => call.Phase == "recover").ShouldBe(recordCommitted ? 0 : 1);
        await physical.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await physical.DidNotReceive().LookupAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Unknown results, dependency faults and unrelated cancellation refuse every owner phase without journal fallback.</summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("fault")]
    [InlineData("unrelated-cancellation")]
    public async Task UnavailableAuthorityDeniesEveryOwnerPhase(string failure)
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var owner = Substitute.For<IDeletionConsumptionOwnerStateAuthority>();
        owner.TenantId.Returns("tenant-a");
        owner.ScopeId.Returns(DeletionConsumptionActor.GetOwnerScope("tenant-a"));
        Task<bool> reply = failure switch
        {
            "fault" => Task.FromException<bool>(new InvalidOperationException("Independent owner unavailable.")),
            "unrelated-cancellation" => Task.FromException<bool>(new OperationCanceledException()),
            _ => Task.FromResult(false),
        };
        Reply(owner, reply);
        var authority = Authority(world, owner);
        foreach (string phase in Phases)
        {
            (await InvokeAsync(authority, phase, TestContext.Current.CancellationToken)).ShouldBeFalse(phase);
        }
    }

    /// <summary>Caller cancellation propagates before a call and after an authority that ignores cancellation returns, for every phase.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitCallerCancellationPropagatesAcrossEveryOwnerPhase(bool duringAwait)
    {
        foreach (string phase in Phases)
        {
            PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
            var owner = Substitute.For<IDeletionConsumptionOwnerStateAuthority>();
            owner.TenantId.Returns("tenant-a");
            owner.ScopeId.Returns(DeletionConsumptionActor.GetOwnerScope("tenant-a"));
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Reply(owner, completion.Task);
            using var cancellation = new CancellationTokenSource();
            if (!duringAwait)
            {
                cancellation.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(() => InvokeAsync(Authority(world, owner), phase, cancellation.Token));
                continue;
            }
            Task<bool> result = InvokeAsync(Authority(world, owner), phase, cancellation.Token);
            cancellation.Cancel();
            completion.SetResult(true);
            await Should.ThrowAsync<OperationCanceledException>(() => result);
        }
    }

    /// <summary>Explicit caller cancellation remains authoritative when the independent owner cancels and then faults with another exception.</summary>
    [Fact]
    public async Task CancellationThenOwnerFaultPropagatesAcrossEveryOwnerPhase()
    {
        foreach (string phase in Phases)
        {
            PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
            var owner = Substitute.For<IDeletionConsumptionOwnerStateAuthority>();
            owner.TenantId.Returns("tenant-a");
            owner.ScopeId.Returns(DeletionConsumptionActor.GetOwnerScope("tenant-a"));
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Reply(owner, completion.Task);
            using var cancellation = new CancellationTokenSource();
            Task<bool> result = InvokeAsync(Authority(world, owner), phase, cancellation.Token);
            cancellation.Cancel();
            completion.SetException(new InvalidOperationException("Owner fault after caller cancellation."));
            await Should.ThrowAsync<OperationCanceledException>(() => result);
        }
    }

    /// <summary>The original public four-parameter metadata and optional defaults remain available to pre-existing consumers.</summary>
    [Fact]
    public async Task OriginalConstructorSignatureAndDefaultsRemainAvailable()
    {
        var signature = new[] { typeof(IReadOnlyList<PublishedDeletionCapabilityTrust>), typeof(IGovernanceScopeGuard),
            typeof(IAnchoredStateTransitionAuthority), typeof(IPublishedTrustRevocationEvidence) };
        var constructor = typeof(PublishedTrustDeletionConsumptionAuthority).GetConstructor(signature).ShouldNotBeNull();
        var parameters = constructor.GetParameters();
        parameters[2].HasDefaultValue.ShouldBeTrue();
        parameters[2].DefaultValue.ShouldBeNull();
        parameters[3].HasDefaultValue.ShouldBeTrue();
        parameters[3].DefaultValue.ShouldBeNull();
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var retained = (PublishedTrustDeletionConsumptionAuthority)constructor.Invoke([new[] { world.Trust }, world.Guard, null, null]);
        (await retained.VerifyDispatchAsync(world.Request, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await retained.ValidateStateAsync("tenant-a", 0, new EnrolledDeletionOwnerFixture().Digest, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>A registered tenant's exact state and admission cannot be substituted into another tenant/scope or changed admitted original.</summary>
    [Fact]
    public async Task CrossTenantScopeAndChangedOriginalCannotAdvanceOrRecover()
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        var a = new EnrolledDeletionOwnerFixture();
        var b = new EnrolledDeletionOwnerFixture("tenant-b");
        var authority = new PublishedTrustDeletionConsumptionAuthority([world.Trust], world.Guard, null, null, [a, b]);
        (await authority.ValidateStateAsync("tenant-b", 0, a.Digest, TestContext.Current.CancellationToken)).ShouldBeFalse();
        AnchoredStateTransition original = Transition(a, "exact-original"u8.ToArray());
        (await authority.AdmitTransitionAsync(original, TestContext.Current.CancellationToken)).ShouldBeTrue();
        AnchoredStateTransition[] substitutions =
        [
            original with { ScopeId = b.ScopeId },
            original with { ScopeId = original.ScopeId + "-foreign-purpose" },
            original with { TargetBytes = "changed"u8.ToArray() },
            original with { PredecessorDigest = b.Digest },
            original with { ExpectedRevision = 1, TargetRevision = 2 },
        ];
        foreach (AnchoredStateTransition substituted in substitutions)
        {
            (await authority.RecordTransitionAsync(substituted, TestContext.Current.CancellationToken)).ShouldBeFalse();
            (await authority.RecoverTransitionAsync(substituted, TestContext.Current.CancellationToken)).ShouldBeFalse();
            (await authority.VerifyTransitionAsync(substituted, TestContext.Current.CancellationToken)).ShouldBeFalse();
        }
        (await authority.RecoverTransitionAsync(original, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.VerifyTransitionAsync(original, TestContext.Current.CancellationToken)).ShouldBeTrue();
        b.Revision.ShouldBe(0);
        a.Revision.ShouldBe(1);
    }

    /// <summary>Admitted-original reads forward only to the single canonical owner enrolled for that exact scope; foreign tenant, foreign scope, duplicate or absent enrollment returns unknown.</summary>
    [Theory]
    [InlineData("exact")][InlineData("foreign-scope")][InlineData("foreign-tenant")][InlineData("duplicate")][InlineData("absent")]
    public async Task AdmittedOriginalReadForwardsOnlyToExactSingleEnrolledOwner(string vector)
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        string scope = DeletionConsumptionActor.GetOwnerScope("tenant-a");
        var transition = new AnchoredStateTransition(scope, 0, 1, new string('A', 64), Convert.ToHexString(SHA256.HashData("admitted"u8)), "admitted"u8.ToArray());
        var owner = Substitute.For<IDeletionConsumptionOwnerStateAuthority>();
        owner.TenantId.Returns(vector == "foreign-tenant" ? "tenant-b" : "tenant-a");
        owner.ScopeId.Returns(scope);
        owner.ReadAdmittedOriginalAsync(scope, Arg.Any<CancellationToken>()).Returns(transition);
        IReadOnlyList<IDeletionConsumptionOwnerStateAuthority> owners = vector switch
        {
            "duplicate" => [owner, owner],
            "absent" => [],
            _ => [owner],
        };
        var authority = new PublishedTrustDeletionConsumptionAuthority([world.Trust], world.Guard, null, null, owners);
        string requested = vector == "foreign-scope" ? DeletionConsumptionActor.GetOwnerScope("tenant-b") : scope;
        var original = await authority.ReadAdmittedOriginalAsync(requested, TestContext.Current.CancellationToken);
        if (vector == "exact") { original.ShouldBeSameAs(transition); }
        else
        {
            original.ShouldBeNull();
            await owner.DidNotReceiveWithAnyArgs().ReadAdmittedOriginalAsync(default!, default);
        }
    }

    /// <summary>Current-original reads forward only to the single canonical owner enrolled for that exact scope; foreign tenant, foreign scope, duplicate or absent enrollment returns unknown.</summary>
    [Theory]
    [InlineData("exact")][InlineData("foreign-scope")][InlineData("foreign-tenant")][InlineData("duplicate")][InlineData("absent")]
    public async Task CurrentOriginalReadForwardsOnlyToExactSingleEnrolledOwner(string vector)
    {
        PublishedTrustDispatchFixture world = PublishedTrustDispatchFixture.Create();
        string scope = DeletionConsumptionActor.GetOwnerScope("tenant-a");
        var transition = new AnchoredStateTransition(scope, 1, 2, new string('B', 64), Convert.ToHexString(SHA256.HashData("current"u8)), "current"u8.ToArray());
        var owner = Substitute.For<IDeletionConsumptionOwnerStateAuthority>();
        owner.TenantId.Returns(vector == "foreign-tenant" ? "tenant-b" : "tenant-a");
        owner.ScopeId.Returns(scope);
        owner.ReadCurrentOriginalAsync(scope, Arg.Any<CancellationToken>()).Returns(transition);
        IReadOnlyList<IDeletionConsumptionOwnerStateAuthority> owners = vector switch
        {
            "duplicate" => [owner, owner],
            "absent" => [],
            _ => [owner],
        };
        var authority = new PublishedTrustDeletionConsumptionAuthority([world.Trust], world.Guard, null, null, owners);
        string requested = vector == "foreign-scope" ? DeletionConsumptionActor.GetOwnerScope("tenant-b") : scope;
        var original = await authority.ReadCurrentOriginalAsync(requested, TestContext.Current.CancellationToken);
        if (vector == "exact") { original.ShouldBeSameAs(transition); }
        else
        {
            original.ShouldBeNull();
            await owner.DidNotReceiveWithAnyArgs().ReadCurrentOriginalAsync(default!, default);
        }
    }

    private static readonly string[] Phases =["state", "operation", "legacy", "admit", "record", "recover", "verify"];

    private static Task<bool> InvokeAsync(PublishedTrustDeletionConsumptionAuthority authority, string phase, CancellationToken token)
    {
        var transition = new AnchoredStateTransition(DeletionConsumptionActor.GetOwnerScope("tenant-a"), 0, 1,
            new string('A', 64), Convert.ToHexString(SHA256.HashData("actual-owner-bytes"u8)), "actual-owner-bytes"u8.ToArray());
        return phase switch
        {
            "state" => authority.ValidateStateAsync("tenant-a", 0, new string('A', 64), token),
            "operation" => authority.AuthorizeOperationAsync("tenant-a", "batch-a", "RegisterDeletionBatch", token),
            "legacy" => authority.RecordRevisionAsync("tenant-a", 0, 1, new string('A', 64), token),
            "admit" => authority.AdmitTransitionAsync(transition, token),
            "record" => authority.RecordTransitionAsync(transition, token),
            "recover" => authority.RecoverTransitionAsync(transition, token),
            _ => authority.VerifyTransitionAsync(transition, token),
        };
    }

    private static void Reply(IDeletionConsumptionOwnerStateAuthority owner, Task<bool> reply)
    {
        owner.ValidateStateAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(reply);
        owner.AuthorizeOperationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(reply);
        owner.AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(reply);
        owner.RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(reply);
        owner.RecoverTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(reply);
        owner.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(reply);
    }

    private static PublishedTrustDeletionConsumptionAuthority Authority(PublishedTrustDispatchFixture world, IDeletionConsumptionOwnerStateAuthority owner)
        => new([world.Trust], world.Guard, null, null, [owner]);

    private static AnchoredStateTransition Transition(EnrolledDeletionOwnerFixture owner, byte[] bytes)
        => new(owner.ScopeId, owner.Revision, owner.Revision + 1, owner.Digest, Convert.ToHexString(SHA256.HashData(bytes)), bytes);
}
