using System.Text.Json.Nodes;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Executes source preparation against the actual conditional guard owner and ERC-backed consumption actor; fixture private eligibility is component evidence.</summary>
public sealed class ProtectionQualificationPreparationTests
{
    [Fact]
    public async Task SourcePreparationEnumeratesOwnersBindsContentAndConsumesActualIssuedDispatchedManifest()
    {
        using var fixture = new ProtectionQualificationPreparationFixture(); await fixture.InitializeAsync();
        var recipe = fixture.Recipes("P05")["P05"]["DestroyDekManifest"];
        var result = await fixture.RunAsync(recipe, "P05", "DestroyDekManifest");
        result!["Status"]!.GetValue<int>().ShouldBe((int)DeletionConsumptionStatus.Consumed);
        var deletion = fixture.Guard.State.Deletions.Single();
        deletion.RequiredOwnerIds.Order().ShouldBe(new[] { "directory", "interaction" });
        deletion.OwnerCycles.Count.ShouldBe(2); deletion.ContentBindings.Count.ShouldBe(1);
        deletion.Batches.Single().ProtectionOutcome.ShouldBe("Consumed");
        fixture.Signed.Single().Payload.IntendedIssuedGuardRevision.ShouldBeGreaterThan(1);
        fixture.Admitted.Values.Where(call => call.Kind == ProtectionQualificationNativeCallKind.Guard).ShouldAllBe(call => !call.Payload.ContainsKey("UseCurrentGuardRevision"));
        var actual = await fixture.Consumption.Custody.ReadAsync("tenant-a", TestContext.Current.CancellationToken);
        actual!.Value.Head.Erased.Count.ShouldBe(2);
        fixture.Guard.Backend.CommittedTransactions.ShouldBeGreaterThan(10);
    }
    [Fact]
    public async Task HoldWinningBeforeDispatchRetainsRootsWithoutInventingRegistrationOrReservation()
    {
        using var fixture = new ProtectionQualificationPreparationFixture(); await fixture.InitializeAsync();
        var recipes = fixture.Recipes("P20")["P20"];
        (await fixture.RunAsync(recipes["HoldBeforeDispatch"], "P20", "HoldBeforeDispatch"))!["Status"]!.GetValue<string>().ShouldBe("Committed");
        var result = await fixture.RunAsync(recipes["RejectManifestDestroy"], "P20", "RejectManifestDestroy");
        result!["status"]!.GetValue<string>().ShouldBe("HoldBlocked");
        fixture.Guard.State.Deletions.Single().Batches.Single().DispatchReceiptId.ShouldBeEmpty();
        fixture.Consumption.State.CommittedState.ShouldBeEmpty();
        var current = await fixture.Consumption.Custody.ReadAsync("tenant-a", TestContext.Current.CancellationToken);
        current!.Value.Head.Erased.ShouldBeEmpty(); current.Value.Head.Targets.Count.ShouldBe(2);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HoldDispatchRaceRetainsBothRealOutcomesAndCommitsRequiredHoldAfterDispatchWins(bool holdWins)
    {
        using var fixture = new ProtectionQualificationPreparationFixture(holdWins); await fixture.InitializeAsync();
        var recipe = fixture.Recipes("P21")["P21"]["RaceHoldDispatch"];
        await fixture.RunAsync(recipe, "P21", "RaceHoldDispatch");
        var firstHold = fixture.Admitted.Values.Single(call => call.Kind == ProtectionQualificationNativeCallKind.Guard
            && call.Payload["transition"]!["Operation"]!.GetValue<int>() == (int)GovernanceGuardOperation.RegisterHold);
        var dispatch = fixture.Admitted.Values.Single(call => call.Kind == ProtectionQualificationNativeCallKind.Guard
            && call.Payload["transition"]!["Operation"]!.GetValue<int>() == (int)GovernanceGuardOperation.CommitDispatch);
        var nativeHold = fixture.Originals[firstHold.Id]!.Deserialize<GovernanceProtocolReceipt>()!;
        var nativeDispatch = fixture.Originals[dispatch.Id]!.Deserialize<GovernanceProtocolReceipt>()!;
        (nativeHold.Status == "Committed").ShouldBe(holdWins); (nativeDispatch.Status == "Committed").ShouldBe(!holdWins);
        fixture.Guard.State.Holds.Single().Active.ShouldBeTrue();
        var current = (await fixture.Consumption.Custody.ReadAsync("tenant-a", TestContext.Current.CancellationToken))!.Value;
        var calls = new List<ProtectionQualificationNativeOriginal>();
        foreach (var call in fixture.Admitted.Values.Where(call => call.Kind is ProtectionQualificationNativeCallKind.Guard or ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch))
        {
            var result = fixture.Originals[call.Id]!; calls.Add(new(call, result));
            if (result["IntentDigest"] is null) { continue; }
            var transition = call.Payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
            var receipt = result.Deserialize<GovernanceProtocolReceipt>()!;
            current.Head.GuardAttempts.Add(receipt.IntentDigest, new(transition, ErasureRootCustody.Hash(call.Payload["targets"]!.Deserialize<GuardedStateMutation[]>()!), transition.ExpectedGuardRevision, receipt.GuardHighWater, receipt));
        }
        var observed = new JsonObject();
        ProtectionQualificationNativeProjection.ProjectHolds(observed, current.Head, calls, (JsonObject)JsonSerializer.SerializeToNode(fixture.Guard.State)!);
        observed["holdOrders"]![0]!["dispatchWon"]!.GetValue<bool>().ShouldBe(!holdWins);
        if (holdWins) { fixture.Consumption.State.CommittedState.ShouldBeEmpty(); }
        else
        {
            var continuation = fixture.Admitted.Values.Single(call => call.Kind == ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch);
            continuation.Payload["transition"]!["OperationId"]!.GetValue<string>().ShouldNotBe(firstHold.Payload["transition"]!["OperationId"]!.GetValue<string>());
            fixture.Originals[continuation.Id]!["Status"]!.GetValue<string>().ShouldBe("Committed");
        }
    }

    [Fact]
    public async Task ContainmentPreparationCommitsOriginalViolationsAndDistinctSingletonOwners()
    {
        using var fixture = new ProtectionQualificationPreparationFixture(); await fixture.InitializeAsync(containment: true);
        var recipe = fixture.Recipes("P13")["P13"]["IssueContainmentSingletons"];
        (await fixture.RunAsync(recipe, "P13", "IssueContainmentSingletons"))!["Status"]!.GetValue<string>().ShouldBe("Committed");
        var deletion = fixture.Guard.State.Deletions.Single();
        deletion.Violations.Count.ShouldBe(2);
        deletion.Violations.ShouldAllBe(value => value.AcceptanceReceiptId.Length > 0 && value.ProtectionTarget != null);
        deletion.Batches.Select(value => value.Ordinal).ShouldBe(new long[] { 0, 1, 2 });
        deletion.Batches.ShouldAllBe(batch => batch.Targets.Count == 1 && batch.DispatchReceiptId.Length > 0);
        deletion.Batches.Select(value => value.BatchId).Distinct().Count().ShouldBe(3);
        deletion.Batches.SelectMany(value => value.Targets).Select(value => value.AgentInteractionId).Distinct().Count().ShouldBe(3);
        deletion.OwnerCycles.Count.ShouldBe(2); deletion.ContentBindings.Count.ShouldBe(1);
        (await fixture.Consumption.Custody.ReadAsync("tenant-a", TestContext.Current.CancellationToken))!.Value.Head.Erased.ShouldBeEmpty();
    }
    [Fact]
    public async Task SameTargetResourceLinksActualOriginalCoverageWithoutAllocatingAnotherBatch()
    {
        using var fixture = new ProtectionQualificationPreparationFixture(); await fixture.InitializeAsync(containment: true);
        var recipes = fixture.Recipes("P14")["P14"];
        (await fixture.RunAsync(recipes["LinkSameTargetResource"], "P14", "LinkSameTargetResource"))!["Status"]!.GetValue<string>().ShouldBe("CoverageLinked");
        var deletion = fixture.Guard.State.Deletions.Single(); deletion.Batches.Count.ShouldBe(1);
        deletion.Batches.Single().Ordinal.ShouldBe(0); deletion.Batches.Single().CoveredResourceIds.ShouldBe(new[] { "resource-1" });
        deletion.Violations.Single().AcceptanceReceiptId.ShouldNotBeEmpty();
        (await fixture.RunAsync(recipes["DestroyDekManifest"], "P14", "DestroyDekManifest"))!["Status"]!.GetValue<int>().ShouldBe((int)DeletionConsumptionStatus.Consumed);
        (await fixture.Consumption.Custody.ReadAsync("tenant-a", TestContext.Current.CancellationToken))!.Value.Head.Erased.Count.ShouldBe(fixture.Targets.Length);
        fixture.Guard.State.Deletions.Single().Batches.Count.ShouldBe(1);
    }
    [Fact]
    public async Task LaterDifferentIssuedBatchReturnsExactOriginalDestroyedCoverageWithoutAnotherManifest()
    {
        using var fixture = new ProtectionQualificationPreparationFixture(); await fixture.InitializeAsync(containment: true);
        var recipes = fixture.Recipes("P15")["P15"];
        var original = await fixture.RunAsync(recipes["DestroyDekManifest"], "P15", "DestroyDekManifest");
        original!["Status"]!.GetValue<int>().ShouldBe((int)DeletionConsumptionStatus.Consumed);
        (await fixture.RunAsync(recipes["LinkSameTargetResource"], "P15", "LinkSameTargetResource"))!["Status"]!.GetValue<string>().ShouldBe("CoverageLinked");
        var before = (await fixture.Consumption.Custody.ReadAsync("tenant-a", TestContext.Current.CancellationToken))!.Value.Head;
        var looked = await fixture.RunAsync(recipes["LookupAlreadyDestroyed"], "P15", "LookupAlreadyDestroyed");
        looked!["Status"]!.GetValue<int>().ShouldBe((int)DeletionConsumptionStatus.AlreadyDestroyedByBatch);
        looked["TargetReceipts"]!.ToJsonString().ShouldBe(original["TargetReceipts"]!.ToJsonString());
        looked["BatchId"]!.GetValue<string>().ShouldNotBe(original["BatchId"]!.GetValue<string>());
        var after = (await fixture.Consumption.Custody.ReadAsync("tenant-a", TestContext.Current.CancellationToken))!.Value.Head;
        after.Epoch.ShouldBe(before.Epoch); after.ManifestArchives.Count.ShouldBe(1); after.Erased.Count.ShouldBe(fixture.Targets.Length); after.Targets.ShouldBeEmpty();
        fixture.Guard.State.Deletions.Single(value => value.RequestId == "deletion-a").Batches.Count.ShouldBe(1);
    }
    [Fact]
    public async Task TwoExpectedContainmentTargetsUseDistinctRealControlRootAndEmitCompleteVerifierEvidence()
    {
        using var fixture = new ProtectionQualificationPreparationFixture(); await fixture.InitializeAsync(containment: true);
        fixture.Targets.Length.ShouldBe(2); await fixture.SealExpectedSourcesAsync("P13");
        var recipes = fixture.Recipes("P13")["P13"];
        await fixture.RunAsync(recipes["IssueContainmentSingletons"], "P13", "IssueContainmentSingletons");
        fixture.Signed.ShouldAllBe(value => value.Payload.SigningAttemptOrdinal == 1);
        var guard = fixture.Guard.State.Deletions.Single();
        var accepted = guard.Batches.Single(value => value.Kind == "AcceptedSet");
        fixture.Targets.ShouldNotContain(value => value.AgentInteractionId == accepted.Targets.Single().AgentInteractionId);
        await fixture.RunAsync(recipes["DestroyDekManifest"], "P13", "DestroyDekManifest");
        await fixture.RunAsync(fixture.AddRequiredReplays("P13", "LookupOriginalDestruction", recipes["LookupOriginalDestruction"]), "P13", "LookupOriginalDestruction");
        var observed = await fixture.ObserveAsync("P13");
        observed["targetCoverage"]!.AsArray().Count.ShouldBe(2);
        observed["targetCoverage"]!.AsArray().ShouldAllBe(value => value!["singletonManifest"]!.GetValue<bool>() && !value["reusesAcceptedSetBatch"]!.GetValue<bool>());
        observed["replays"]!.AsArray().Count.ShouldBe(4);
        observed["replays"]!.AsArray().ShouldAllBe(value => value!["field"]!["State"]!.GetValue<string>() == "Erased");
        var current = (await fixture.Consumption.Custody.ReadAsync("tenant-a", TestContext.Current.CancellationToken))!.Value.Head;
        current.Targets.Count.ShouldBe(1); current.Erased.Count.ShouldBe(2);
        current.Targets.Values.Single().Registration.Identity.ObjectId.ShouldBe(accepted.Targets.Single().AgentInteractionId);
        if (Environment.GetEnvironmentVariable("EXT_PROTECTION_P13_VECTOR") is { Length: > 0 } path)
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { observation = observed, targets = fixture.Targets, publicKeyPem = fixture.PublicKeyPem,
                sourceComponentOnly = true, liveExecuted = false, engineCommit = observed["engineSourceRevision"]!.GetValue<string>(), engineBinarySha256 = observed["engineBinarySha256"]!.GetValue<string>() }), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task TwoRealHoldMutationsProduceDistinctNoIssueOriginalsBeforeHealthySuccessorIssue()
    {
        using var fixture = new ProtectionQualificationPreparationFixture(); await fixture.InitializeAsync();
        var recipe = fixture.Recipes("P39")["P39"]["TwoStaleSigningAttempts"];
        (await fixture.RunAsync(recipe, "P39", "TwoStaleSigningAttempts"))!["State"]!.GetValue<int>().ShouldBe((int)Hexalith.Platform.Custody.DeletionCapabilitySigningState.Signed);
        fixture.Signed.Count.ShouldBe(3);
        fixture.Signed.Select(value => value.Payload.SigningAttemptOrdinal).ShouldBe(new long[] { 1, 2, 3 });
        fixture.Signed.Select(value => value.Payload.BatchId).Distinct().Count().ShouldBe(1);
        fixture.Signed.Select(value => value.Payload.DestructionSealId).Distinct().Count().ShouldBe(1);
        fixture.Originals.Values.Where(value => value?["State"]?.GetValue<int>() == (int)Hexalith.Platform.Custody.DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued)
            .Select(value => value!["NoIssueProof"]!["ProofId"]!.GetValue<string>()).Distinct().Count().ShouldBe(2);
        fixture.Guard.State.Holds.Single().Active.ShouldBeFalse();
        fixture.Guard.State.Deletions.Single().Batches.Single().Capability.ShouldBe(fixture.Signed[2].Payload);
    }
}
