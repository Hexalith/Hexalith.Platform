using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Real source programs, native result-loss controls and exact surviving owner originals produce cross-language qualification vectors.</summary>
public sealed class ProtectionQualificationLoop7RecoveryTests
{
    [Theory]
    [InlineData("P06")]
    [InlineData("P25")]
    [InlineData("P28")]
    [InlineData("P35")]
    [InlineData("P36")]
    public async Task ActualLostResultsAndReservationRecoveryEmitBoundNativeEvidence(string caseId)
    {
        using var fixture = new ProtectionQualificationPreparationFixture(); await fixture.InitializeAsync();
        await fixture.SealExpectedSourcesAsync(caseId);
        var recipes = fixture.Recipes(caseId)[caseId];
        using var stream = typeof(ProtectionQualificationSourceCatalog).Assembly.GetManifestResourceStream("ExtProtectionOwnerPlan")!;
        using var plan = JsonDocument.Parse(stream);
        var scenario = plan.RootElement.GetProperty("cases").EnumerateArray().Single(value => value.GetProperty("id").GetString() == caseId);
        foreach (var item in scenario.GetProperty("steps").EnumerateArray())
        {
            string action = item.GetString()!;
            if (action == "SealAllTargets") { continue; }
            var recipe = fixture.AddRequiredReplays(caseId, action, recipes[action]);
            if (caseId is "P25" or "P28" && action is "ReserveBeforeAdmissionBlock" or "CompromiseAfterReserve") { fixture.Consumption.Store.Failure = "trim-after"; }
            if (action is "DestroyWithLostAcknowledgement" or "LoseBlockDeliveryAndMirror" or "LoseActivationResult")
            {
                await Should.ThrowAsync<IOException>(() => fixture.RunAsync(recipe, caseId, action));
                int rotations = fixture.Consumption.Store.RotateCount, trims = fixture.Consumption.Store.TrimCount;
                await fixture.RecoverLostActionAsync(caseId, action);
                fixture.Consumption.Store.RotateCount.ShouldBe(rotations); fixture.Consumption.Store.TrimCount.ShouldBe(trims);
            }
            else { await fixture.RunAsync(recipe, caseId, action); }
        }
        var observation = await fixture.ObserveAsync(caseId);
        if (caseId is "P25" or "P28")
        {
            var recovery = observation["reservationRecoveries"]!.AsArray().Single()!;
            recovery["Original"]!["Result"]!["Status"]!.GetValue<int>().ShouldBe((int)DeletionConsumptionStatus.ConsumptionReserved);
            recovery["OwnerOutcome"]!["Status"]!.GetValue<int>().ShouldBe((int)DeletionConsumptionStatus.Consumed);
            fixture.Consumption.Store.TrimCount.ShouldBe(1);
        }
        else { observation["exactResults"]!.AsArray().Count.ShouldBe(2); }
        if (Environment.GetEnvironmentVariable("EXT_PROTECTION_LOOP7_VECTOR_DIRECTORY") is { Length: > 0 } directory)
        {
            await File.WriteAllTextAsync(Path.Combine(directory, caseId + ".json"), JsonSerializer.Serialize(new { observation, targets = fixture.Targets, publicKeyPem = fixture.PublicKeyPem,
                sourceComponentOnly = true, liveExecuted = false, engineCommit = observation["engineSourceRevision"]!.GetValue<string>(), engineBinarySha256 = observation["engineBinarySha256"]!.GetValue<string>() }), TestContext.Current.CancellationToken);
        }
    }
}
