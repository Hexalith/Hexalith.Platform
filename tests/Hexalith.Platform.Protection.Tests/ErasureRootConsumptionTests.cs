using Hexalith.EventStore.Contracts.Security;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>ERC-backed actual production owner ordering and exact production-port lookup after stale primary restoration.</summary>
public sealed class ErasureRootConsumptionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AdmissionAndReservationRaceHasOneOwnerWinner(bool reserveFirst)
    {
        var fixture = new ErasureRootConsumptionFixture(); var request = await fixture.SeedAsync();
        (await fixture.Invoke(() => fixture.Actor.RegisterAsync(request))).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        var block = new DeletionBatchBlockRequest("tenant-a", "batch-1", "block-1", "admission-original");
        Task<DeletionConsumptionOutcome> first = fixture.Invoke(() => reserveFirst ? fixture.Actor.ReserveAndConsumeAsync(request) : fixture.Actor.BlockAsync(block));
        Task<DeletionConsumptionOutcome> second = fixture.Invoke(() => reserveFirst ? fixture.Actor.BlockAsync(block) : fixture.Actor.ReserveAndConsumeAsync(request));
        var outcomes = await Task.WhenAll(first, second);
        if (reserveFirst) { outcomes[0].Status.ShouldBe(DeletionConsumptionStatus.Consumed); outcomes[1].Status.ShouldBe(DeletionConsumptionStatus.Consumed); fixture.Store.TrimCount.ShouldBe(1); }
        else { outcomes.ShouldAllBe(value => value.Status == DeletionConsumptionStatus.ConsumptionBlocked); fixture.Store.TrimCount.ShouldBe(0); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompromiseAndReservationRaceNeverCancelsAnOriginalReservation(bool reserveFirst)
    {
        var fixture = new ErasureRootConsumptionFixture(); var request = await fixture.SeedAsync();
        await fixture.Invoke(() => fixture.Actor.RegisterAsync(request));
        if (reserveFirst)
        {
            (await fixture.Invoke(() => fixture.Actor.ReserveAndConsumeAsync(request))).Status.ShouldBe(DeletionConsumptionStatus.Consumed);
            await fixture.Invoke(() => fixture.Actor.RegisterRevocationAsync(ErasureRootConsumptionFixture.Revocation()));
            (await fixture.Invoke(() => fixture.Actor.LookupAsync("tenant-a", "batch-1"))).Status.ShouldBe(DeletionConsumptionStatus.Consumed); fixture.Store.TrimCount.ShouldBe(1);
        }
        else
        {
            await fixture.Invoke(() => fixture.Actor.RegisterRevocationAsync(ErasureRootConsumptionFixture.Revocation()));
            (await fixture.Invoke(() => fixture.Actor.ReserveAndConsumeAsync(request))).Status.ShouldBe(DeletionConsumptionStatus.ConsumptionBlocked); fixture.Store.TrimCount.ShouldBe(0);
        }
    }

    [Fact]
    public async Task ProductionLookupRestoresCurrentOriginalStateAndReceiptsWithoutAnotherTrim()
    {
        var fixture = new ErasureRootConsumptionFixture(); var request = await fixture.SeedAsync();
        var registered = await fixture.Invoke(() => fixture.Actor.RegisterAsync(request)); var old = fixture.CaptureOwner();
        var consumed = await fixture.Invoke(() => fixture.Actor.ReserveAndConsumeAsync(request)); consumed.Status.ShouldBe(DeletionConsumptionStatus.Consumed);
        await fixture.RestoreOwnerAsync(old);
        var looked = await fixture.Invoke(() => fixture.Actor.LookupAsync("tenant-a", "batch-1"));
        System.Text.Json.JsonSerializer.Serialize(looked).ShouldBe(System.Text.Json.JsonSerializer.Serialize(consumed)); fixture.Store.TrimCount.ShouldBe(1);
        var original = await fixture.Invoke(() => fixture.Actor.ReadRegistrationOriginalAsync(request));
        original.ShouldNotBeNull(); System.Text.Json.JsonSerializer.Serialize(original.Outcome).ShouldBe(System.Text.Json.JsonSerializer.Serialize(registered));
    }

    [Fact]
    public async Task ReplacementKeyCompromiseBeforeActivationRetainsExactBlockedActivationAfterRestore()
    {
        var fixture = new ErasureRootConsumptionFixture(); var request = await fixture.SeedAsync();
        await fixture.Invoke(() => fixture.Actor.RegisterAsync(request));
        await fixture.Invoke(() => fixture.Actor.RegisterRevocationAsync(ErasureRootConsumptionFixture.Revocation()));
        var blocked = await fixture.Invoke(() => fixture.Actor.LookupAsync("tenant-a", "batch-1")); var old = fixture.CaptureOwner();
        var replacement = request with { Capability = request.Capability with { CapabilityKeyVersion = "key-v2", AttestationOrdinal = 2 }, DispatchReceiptId = "dispatch-2", DispatchGuardRevision = 11 };
        await fixture.Invoke(() => fixture.Actor.RegisterRevocationAsync(ErasureRootConsumptionFixture.Revocation("key-v2")));
        var activation = new DeletionReattestationActivation("activate-1", blocked.ReceiptId!, blocked.KeyBlockSetRevision, "replacement-guard", replacement);
        var outcome = await fixture.Invoke(() => fixture.Actor.ActivateAsync(activation)); outcome.Status.ShouldBe(DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise);
        await fixture.RestoreOwnerAsync(old);
        var original = await fixture.Invoke(() => fixture.Actor.ReadActivationOriginalAsync(activation));
        original.ShouldNotBeNull(); System.Text.Json.JsonSerializer.Serialize(original.Outcome).ShouldBe(System.Text.Json.JsonSerializer.Serialize(outcome)); fixture.Store.TrimCount.ShouldBe(0);
    }
}
