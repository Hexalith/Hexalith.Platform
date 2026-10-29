using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Every erased plan branch gets actual per-copy engine/driver replay. Distinct native cells cannot inherit another copy's replay.</summary>
public sealed class ProtectionQualificationReplayTests
{
    [Fact]
    public async Task AllErasedScenariosExecuteRealPersistedPerCopyReplayAndRejectCopySubstitution()
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        var enrollment = fixture.Enroll(); var binding = enrollment.Binding;
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = binding.Identity.Target, Value = "replay-canary" } });
        var write = await fixture.Engine.ProtectEventPayloadAsync(binding.Identity.Owner, Substitute.For<IEventPayload>(), binding.Identity.PayloadTypeId, payload, "json", ErasureRootOccurrenceBindings.Occurrence(binding.Identity), token);
        await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, token);
        fixture.Source.Persisted.Add(binding.OperationId, write.ProtectionResult.PayloadBytes);
        await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token);
        var read = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        read.Head.SourceOriginals.Add("source", enrollment); (await fixture.Custody.SaveAsync("tenant-a", read.Version, read.Head, token)).ShouldBeTrue();
        var persisted = new ProtectionQualificationPersistedEvent(binding, write.ProtectionResult);
        var state = new Dictionary<string, JsonElement> { ["copy-a"] = JsonSerializer.SerializeToElement(persisted), ["copy-b"] = JsonSerializer.SerializeToElement(persisted) };
        native.GetStateAsync<JsonElement>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => state.GetValueOrDefault(call.ArgAt<string>(1)));
        native.GetStateAsync<ProtectionQualificationPersistedEvent>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(persisted);
        native.SaveStateAsync("source", Arg.Any<string>(), Arg.Any<JsonElement>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => { state[call.ArgAt<string>(1)] = call.ArgAt<JsonElement>(2); return Task.CompletedTask; });
        var controls = new ProtectionQualificationFaults(native, new("controls", "controls", new()), "component", new HashSet<string>());
        var driver = new ProtectionQualificationNativeDriver(fixture.Engine, fixture.Custody, fixture.Keys, fixture.Source, native, null!, null!, null!, null!, null!, controls, new HashSet<string> { "source" }, fixture.Permissions);
        var targets = new[] { binding.Identity.Target };
        var manifest = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "deletion", "seal", "accepted", 0, "batch", DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key"), "component-signature", 1, "dispatch", 2, targets);
        (await fixture.Manifests.ConsumeAsync(manifest, "reservation", token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        var cells = new Dictionary<string, ProtectionQualificationNativeCell>
        { ["a"] = new("source", "copy-a", new()) { CopyKind = "source-outbox" }, ["b"] = new("source", "copy-b", new()) { CopyKind = "source-outbox" } };
        using var planStream = typeof(ProtectionQualificationSourceCatalog).Assembly.GetManifestResourceStream("ExtProtectionOwnerPlan")!; using var plan = JsonDocument.Parse(planStream);
        int scenarios = 0;
        foreach (var scenario in plan.RootElement.GetProperty("cases").EnumerateArray().Where(value => value.GetProperty("checks").EnumerateArray().Any(check => check.GetString() == "erased")))
        {
            string id = scenario.GetProperty("id").GetString()!, action = scenario.GetProperty("steps").EnumerateArray().Last().GetString()!;
            var observation = new ProtectionQualificationObservationRecipe("tenant-a", cells, new()) { CaseId = id };
            var template = new ProtectionQualificationActionRecipe([], new("unused", ProtectionQualificationNativeCallKind.Lookup, new()), null, []);
            var recipe = ProtectionQualificationSourceCatalog.WithRequiredReplays(id, action, template, observation); recipe.After.Length.ShouldBe(2);
            foreach (var call in recipe.After)
            {
                var actual = await driver.ExecuteAsync(call, new JsonObject { ["action"] = action, ["targets"] = JsonSerializer.SerializeToNode(targets) }, token);
                actual!["field"]!["State"]!.GetValue<string>().ShouldBe("Erased");
                actual["sourceIdentity"]!.GetValue<string>().ShouldBe(ProtectionQualificationNativeCell.LocationId(call.Payload.Deserialize<ProtectionQualificationNativeCell>()!));
            }
            scenarios++;
        }
        scenarios.ShouldBeGreaterThan(10);
        var sources = new Dictionary<string, JsonNode> { ["a"] = JsonSerializer.SerializeToNode(persisted)!, ["b"] = JsonSerializer.SerializeToNode(persisted)!,
            ["a:replay"] = JsonNode.Parse(state["copy-a:replay"].GetRawText())!, ["b:replay"] = JsonNode.Parse(state["copy-b:replay"].GetRawText())! };
        var head = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value.Head;
        var projected = ProtectionQualificationNativeProjection.Project("P01", head, sources, cells, targets);
        ProtectionQualificationNativeProjection.ProjectCopies(projected, [], sources, cells, true);
        projected["copyStatesAfter"]!.AsArray().Count.ShouldBe(2);
        sources["b:replay"] = sources["a:replay"].DeepClone();
        Should.Throw<InvalidOperationException>(() => ProtectionQualificationNativeProjection.Project("P01", head, sources, cells, targets));
    }
}
