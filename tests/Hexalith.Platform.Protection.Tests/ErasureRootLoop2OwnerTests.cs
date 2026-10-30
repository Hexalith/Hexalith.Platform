using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Server.Tests.Security;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual receiving guard transactions, independently retained admission and source-owned activation order.</summary>
public sealed class ErasureRootLoop2OwnerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Fact]
    public async Task ReceivingGuardRefusalSurvivesResultRetentionFailureWithoutRedispatchAndLaterHealthyProgress()
    {
        using var engine = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        var fixture = new GovernanceGuardFixture();
        var stale = fixture.Command(GovernanceGuardOperation.RegisterHold, hold: new("hold", fixture.Scope, "policy", ""));
        (await fixture.Apply(fixture.Command(GovernanceGuardOperation.RegisterHold, hold: new("winner", fixture.Scope, "policy", "")))).Status.ShouldBe("Committed");
        var wrapper = Substitute.For<IGovernanceScopeGuard>(); int dispatches = 0;
        wrapper.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => fixture.Owner.ReadAsync(call.Arg<string>(), call.Arg<CancellationToken>()));
        wrapper.LookupOriginalAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>()).Returns(call => fixture.Owner.LookupOriginalAsync(call.Arg<GovernanceGuardTransition>(), call.Arg<IReadOnlyList<GuardedStateMutation>>(), call.Arg<CancellationToken>()));
        wrapper.ExecuteAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            dispatches++; var result = await fixture.Owner.ExecuteAsync(call.Arg<GovernanceGuardTransition>(), call.Arg<IReadOnlyList<GuardedStateMutation>>(), call.Arg<CancellationToken>());
            if (dispatches == 1) { engine.Store.Failure = "cas-before"; } return result;
        });
        var source = new ErasureRootNativeOccurrenceSource(engine.Custody, engine.Permissions, native, engine.Digests);
        var handler = new ErasureRootAuthorityOperationHandler(engine.Custody, source, wrapper, Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), new string('A', 64), TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = [.. handler.Methods] }] };
        var client = new DaprErasureRootGovernanceGuard(receiver.Authority);
        (await client.ExecuteAsync(stale, [], Token)).ShouldBeNull();
        var head = (await engine.Custody.ReadAsync("tenant-a", Token, true))!.Value.Head;
        head.GuardAttempts.ShouldBeEmpty(); head.GuardAdmissions.Count.ShouldBe(1);
        var original = (await client.LookupOriginalAsync(stale, [], Token))!; original.Status.ShouldBe("Stale");
        fixture.State.Receipts.ShouldNotContain(receipt => receipt.OperationId == stale.OperationId); dispatches.ShouldBe(1);
        (await engine.Custody.ReadAsync("tenant-a", Token, true))!.Value.Head.GuardAttempts.Values.Single().Receipt.ShouldBe(original);
        (await client.LookupOriginalAsync(stale with { OperationId = "substitution" }, [], Token)).ShouldBeNull(); dispatches.ShouldBe(1);
        var healthy = fixture.Command(GovernanceGuardOperation.RegisterHold, hold: new("healthy", fixture.Scope, "policy", ""));
        (await client.ExecuteAsync(healthy, [], Token))!.Status.ShouldBe("Committed"); dispatches.ShouldBe(2);
    }
    [Fact]
    public async Task ActualActorRecoversExclusiveAdmissionLostBeforeAnyPrimaryPendingState()
    {
        var fixture = new ErasureRootConsumptionFixture(); var request = await fixture.SeedAsync();
        var authority = new ErasureRootDeletionOwnerAuthority(fixture.Custody, fixture.Permissions, "tenant-a"); bool lose = true;
        fixture.Authority.AdmitTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            bool admitted = await authority.AdmitTransitionAsync(call.Arg<AnchoredStateTransition>(), call.Arg<CancellationToken>());
            if (admitted && lose) { lose = false; throw new IOException("lost immediately after independent admission"); } return admitted;
        });
        await Should.ThrowAsync<IOException>(() => fixture.Actor.RegisterAsync(request));
        fixture.State.CommittedState.ShouldBeEmpty();
        (await authority.ReadCurrentOriginalAsync(authority.ScopeId, Token)).ShouldBeNull();
        var admitted = await authority.ReadAdmittedOriginalAsync(authority.ScopeId, Token); admitted.ShouldNotBeNull();
        (await fixture.Actor.LookupAsync("tenant-a", request.Capability.BatchId)).Status.ShouldNotBe(DeletionConsumptionStatus.Unconsumed);
        (await authority.ReadCurrentOriginalAsync(authority.ScopeId, Token)).ShouldBeNull();
        var changed = request with { Capability = request.Capability with { DestructionSealId = "changed" } };
        (await fixture.Actor.RegisterAsync(changed)).Status.ShouldBe(DeletionConsumptionStatus.Conflict);
        (await fixture.Actor.RegisterAsync(request)).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        (await fixture.Actor.ReserveAndConsumeAsync(request)).Status.ShouldBe(DeletionConsumptionStatus.Consumed); fixture.Store.TrimCount.ShouldBe(1);
    }
    [Fact]
    public async Task CompiledP34ActivatesBeforeReplacementCompromiseAndEmitsActualVerifierObservation()
    {
        using var fixture = new ProtectionQualificationPreparationFixture(); await fixture.InitializeAsync(); await fixture.SealExpectedSourcesAsync("P34");
        var recipes = fixture.Recipes("P34")["P34"];
        await fixture.RunAsync(recipes["CompromiseReplacementAfterActivation"], "P34", "CompromiseReplacementAfterActivation");
        var activatedCall = fixture.Admitted.Values.Single(call => call.Kind == ProtectionQualificationNativeCallKind.Activate);
        var activated = fixture.Originals[activatedCall.Id]!.Deserialize<DeletionConsumptionOutcome>()!;
        activated.Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        var second = fixture.Originals[fixture.Admitted.Values.Where(call => call.Kind == ProtectionQualificationNativeCallKind.Compromise).Last().Id]!["receipt"]!.Deserialize<DeletionCapabilityRevocationReceipt>()!;
        second.OwnerRevision.ShouldBeGreaterThan(activated.OwnerRevision); second.Envelope.KeyVersion.ShouldBe("healthy-key-b");
        await fixture.RunAsync(recipes["RejectManifestDestroy"], "P34", "RejectManifestDestroy");
        var observed = await fixture.ObserveAsync("P34"); observed["activationOutcome"]!["status"]!.GetValue<string>().ShouldBe("ActivatedThenReplacementBlocked");
        fixture.Consumption.Store.TrimCount.ShouldBe(0);
        if (Environment.GetEnvironmentVariable("EXT_PROTECTION_P34_VECTOR") is { Length: > 0 } path)
        { await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { observation = observed, targets = fixture.Targets, publicKeyPem = fixture.PublicKeyPem,
            sourceComponentOnly = true, liveExecuted = false, engineCommit = observed["engineSourceRevision"]!.GetValue<string>(), engineBinarySha256 = observed["engineBinarySha256"]!.GetValue<string>() }), Token); }
        var captured = (await fixture.Consumption.Custody.ReadAsync("tenant-a", Token))!.Value;
        var audits = captured.Head.QualificationOperations.Values.SelectMany(operation => operation.Calls.Values).Where(call => call.Result?["effectBefore"] is not null).ToArray(); audits.ShouldNotBeEmpty();
        foreach (var audited in audits) { audited.Result!["effectBefore"] = new string('F', 64); audited.Result["effectAfter"] = new string('F', 64); }
        (await fixture.Consumption.Custody.SaveAsync("tenant-a", captured.Version, captured.Head, Token)).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ObserveAsync("P34"));
        var missing = (await fixture.Consumption.Custody.ReadAsync("tenant-a", Token))!.Value;
        (await fixture.Consumption.Custody.SaveAsync("tenant-a", missing.Version, missing.Head with { QualificationEvidenceIndexRoot = null }, Token)).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ObserveAsync("P34"));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompiledP33RacesBothActualOwnerOrderingsAndEmitsExactVerifierObservation(bool activationWins)
    {
        using var fixture = new ProtectionQualificationPreparationFixture(activationWins: activationWins); await fixture.InitializeAsync(); await fixture.SealExpectedSourcesAsync("P33");
        var recipes = fixture.Recipes("P33")["P33"];
        await fixture.RunAsync(recipes["RaceReplacementActivation"], "P33", "RaceReplacementActivation");
        var activation = fixture.Admitted.Values.Single(call => call.Kind == ProtectionQualificationNativeCallKind.Activate);
        var activated = fixture.Originals[activation.Id]!.Deserialize<DeletionConsumptionOutcome>()!;
        var competing = fixture.Admitted.Values.Where(call => call.Kind == ProtectionQualificationNativeCallKind.Compromise).Last();
        var revoked = fixture.Originals[competing.Id]!["receipt"]!.Deserialize<DeletionCapabilityRevocationReceipt>()!;
        activated.Status.ShouldBe(activationWins ? DeletionConsumptionStatus.Unconsumed : DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise);
        (activated.OwnerRevision < revoked.OwnerRevision).ShouldBe(activationWins); revoked.Envelope.KeyVersion.ShouldBe("healthy-key-b");
        var observed = await fixture.ObserveAsync("P33");
        observed["activationOutcome"]!["status"]!.GetValue<string>().ShouldBe(activationWins ? "ActivatedThenReplacementBlocked" : "ActivationBlockedByReplacementKeyCompromise");
        fixture.Consumption.Store.TrimCount.ShouldBe(0); (await fixture.Consumption.Store.HeadAsync("tenant-a")).Targets.Count.ShouldBe(fixture.Targets.Length);
        if (Environment.GetEnvironmentVariable("EXT_PROTECTION_P33_VECTOR_DIRECTORY") is { Length: > 0 } directory)
        { await File.WriteAllTextAsync(Path.Combine(directory, activationWins ? "activation-first.json" : "compromise-first.json"), JsonSerializer.Serialize(new { observation = observed, targets = fixture.Targets,
            publicKeyPem = fixture.PublicKeyPem, sourceComponentOnly = true, liveExecuted = false, engineCommit = observed["engineSourceRevision"]!.GetValue<string>(), engineBinarySha256 = observed["engineBinarySha256"]!.GetValue<string>() }), Token); }
    }
    [Fact]
    public async Task ActualSignedReservationAndAuthenticatedCopyPolicyDenialPrecedeNativeRotationAndTrim()
    {
        var unavailable = Substitute.For<IAtomicDeletionManifestProvider>();
        unavailable.ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new DeletionManifestProviderResult(call.Arg<DeletionBatchConsumptionRequest>().Capability.TenantId, call.Arg<DeletionBatchConsumptionRequest>().Capability.BatchId, call.Arg<string>(), DeletionManifestProviderState.Unknown, []));
        using var fixture = new ProtectionQualificationPreparationFixture(provider: unavailable); await fixture.InitializeAsync();
        var program = fixture.Recipes("P05")["P05"]["DestroyDekManifest"];
        await fixture.RunAsync(new(program.Before[..^1], program.Before[^1], null, []), "P05", "DestroyDekManifest");
        var request = fixture.Admitted.Values.Single(call => call.Kind == ProtectionQualificationNativeCallKind.Register).Payload.Deserialize<DeletionBatchConsumptionRequest>()!;
        (await fixture.Consumption.Actor.ReserveAndConsumeAsync(request)).Status.ShouldBe(DeletionConsumptionStatus.ConsumptionReserved);
        var reservation = (await fixture.Consumption.Actor.ReadReservationOriginalAsync(request))!.Outcome.ReceiptId!;
        using var native = Substitute.For<DaprClient>(); string policy = new('A', 64);
        var source = new ErasureRootNativeOccurrenceSource(fixture.Consumption.Custody, fixture.Consumption.Permissions, native, fixture.Engine.Digests);
        var handler = new ErasureRootAuthorityOperationHandler(fixture.Consumption.Custody, source, fixture.Guard.Owner, Substitute.For<IDeletionCapabilityRevocationAuthenticator>(), policy, TimeProvider.System);
        using var receiver = new ReceivingAuthorityFixture("erc-owner", handlers: [handler]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|erc", Methods = [.. handler.Methods] }],
            QualificationBindings = new() { ["erc.allow.DestroyDekManifest"] = "erc-owner", ["erc.allow.LookupDestroyDekManifest"] = "erc-owner", ["erc.allow.VerifyReservedManifestAndCopyPolicy"] = "erc-owner" } };
        var installation = new ErasureRootInstallation("erc-v23", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant-a"] = "epoch" });
        var productionAuthorization = new DaprErasureRootAuthorization(receiver.Authority, fixture.Consumption.Store, installation);
        var custody = new ErasureRootCustody(fixture.Consumption.Store, installation, productionAuthorization);
        byte[] before = (await fixture.Consumption.Store.ReadHeadAsync("tenant-a", Token))!.Bytes;
        (await productionAuthorization.VerifyManifestAsync(request, reservation, Token)).ShouldBeFalse();
        (await custody.ConsumeAsync(request, reservation, Token)).State.ShouldBe(DeletionManifestProviderState.Unknown);
        fixture.Consumption.Store.RotateCount.ShouldBe(0); fixture.Consumption.Store.TrimCount.ShouldBe(0);
        (await fixture.Consumption.Store.ReadHeadAsync("tenant-a", Token))!.Bytes.ShouldBe(before);
        receiver.State = receiver.State with { QualificationBindings = new(receiver.State.QualificationBindings!) { ["erc.copyPolicySha256"] = policy } };
        (await productionAuthorization.VerifyManifestAsync(request, reservation, Token)).ShouldBeTrue();
        var completed = await custody.ConsumeAsync(request, reservation, Token); completed.State.ShouldBe(DeletionManifestProviderState.Consumed);
        completed.TargetReceipts.Count.ShouldBe(request.Targets.Count); fixture.Consumption.Store.TrimCount.ShouldBe(1);
        ErasureRootCustody.Hash(await custody.LookupAsync(request, reservation, Token)).ShouldBe(ErasureRootCustody.Hash(completed));
    }
    [Fact]
    public async Task ConditionalHoldRefusalRecoversExactAdmittedOriginalWithoutOwnerStateThenAllowsHealthyDispatch()
    {
        using var fixture = new ProtectionQualificationPreparationFixture(); await fixture.InitializeAsync();
        var recipes = fixture.Recipes("P20")["P20"];
        await fixture.RunAsync(recipes["HoldBeforeDispatch"], "P20", "HoldBeforeDispatch");
        await fixture.RunAsync(recipes["RejectManifestDestroy"], "P20", "RejectManifestDestroy");
        foreach (var call in fixture.Admitted.Values.Where(call => call.Kind is ProtectionQualificationNativeCallKind.RegisterIfDispatched or ProtectionQualificationNativeCallKind.ReserveIfDispatched))
        {
            call.HoldBlockedOriginal.ShouldNotBeNull();
            var recovered = await fixture.Runner.RecoverAsync(call, Token);
            JsonNode.DeepEquals(recovered, fixture.Originals[call.Id]).ShouldBeTrue();
        }
        fixture.Consumption.State.CommittedState.ShouldBeEmpty(); fixture.Consumption.Store.TrimCount.ShouldBe(0);
        (await fixture.Guard.Apply(fixture.Guard.Command(GovernanceGuardOperation.ReleaseHold, hold: new("hold-a", fixture.Guard.Scope, "hold-policy", "independent-release")))).Status.ShouldBe("Committed");
        var state = fixture.Guard.State.Deletions.Single().Batches.Single();
        var batch = fixture.Guard.Batch(targets: state.Targets) with { Capability = state.Capability, DetachedJws = state.DetachedJws, SigningRequestId = state.SigningRequestId,
            AttestationDigest = state.AttestationDigest, AttestationOrdinal = state.AttestationOrdinal, CapabilityKeyVersion = state.CapabilityKeyVersion };
        (await fixture.Guard.Apply(fixture.Guard.Command(GovernanceGuardOperation.AuthorizeDispatch, batch: batch, authorization: "healthy-after-refusal"))).Status.ShouldBe("Committed");
        var dispatch = await fixture.Guard.Apply(fixture.Guard.Command(GovernanceGuardOperation.CommitDispatch, batch: batch, authorization: "healthy-after-refusal")); dispatch.Status.ShouldBe("Committed");
        var request = new DeletionBatchConsumptionRequest(state.Capability!, state.DetachedJws, state.IssuedGuardRevision, dispatch.ReceiptId, dispatch.GuardHighWater, fixture.Targets);
        (await fixture.Consumption.Actor.RegisterAsync(request)).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        (await fixture.Consumption.Actor.ReserveAndConsumeAsync(request)).Status.ShouldBe(DeletionConsumptionStatus.Consumed); fixture.Consumption.Store.TrimCount.ShouldBe(1);
    }
}
