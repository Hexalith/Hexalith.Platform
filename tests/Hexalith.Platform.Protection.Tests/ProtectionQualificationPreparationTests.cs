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
        (await fixture.Consumption.Custody.ReadAsync("tenant-a", TestContext.Current.CancellationToken))!.Value.Head.Erased.Count.ShouldBe(3);
        fixture.Guard.State.Deletions.Single().Batches.Count.ShouldBe(1);
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
