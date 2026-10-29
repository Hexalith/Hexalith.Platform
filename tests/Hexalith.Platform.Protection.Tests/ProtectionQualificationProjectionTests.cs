using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Source-owned independent projection reads actual engine output and original bindings; installed field programs cannot select a passing observation.</summary>
public sealed class ProtectionQualificationProjectionTests
{
    [Fact]
    public async Task ClosedCaseProjectsActualSealedSourceAndIgnoresInstalledFieldProgram()
    {
        using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>();
        var original = fixture.Enroll(); var token = TestContext.Current.CancellationToken;
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = original.Binding.Identity.Target, Value = "projection-canary" } });
        var write = await fixture.Engine.ProtectEventPayloadAsync(original.Binding.Identity.Owner, Substitute.For<IEventPayload>(), original.Binding.Identity.PayloadTypeId,
            payload, "json", ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), token);
        var current = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        current.Head.SourceOriginals.Add("actual-source-original", original);
        (await fixture.Custody.SaveAsync("tenant-a", current.Version, current.Head, token)).ShouldBeTrue();
        native.GetStateAsync<JsonElement>("read-only", "source", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.SerializeToElement(new ProtectionQualificationPersistedEvent(original.Binding, write.ProtectionResult)));
        var recipe = new ProtectionQualificationObservationRecipe("tenant-a", new() { ["source"] = new("read-only", "source", new()) { CopyKind = "source-outbox" } },
            new() { ["records"] = new("source", "/not-a-native-field") }) { CaseId = "P01" };
        var observer = new ProtectionQualificationPersistedObserver(native, fixture.Custody, fixture.Engine, new HashSet<string> { "read-only" });
        var actual = await observer.ObserveAsync(recipe, [original.Binding.Identity.Target], token);
        var record = actual["records"]![0]!; record["kind"]!.GetValue<string>().ShouldBe("source-outbox");
        byte[] persisted = Convert.FromBase64String(record["bytesBase64"]!.GetValue<string>()); persisted.ShouldBe(write.ProtectionResult.PayloadBytes);
        Encoding.UTF8.GetString(persisted).ShouldNotContain("projection-canary");
        native.ReceivedCalls().ShouldAllBe(call => call.GetMethodInfo().Name == "GetStateAsync");
        current = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value; current.Head.SourceOriginals.Clear();
        (await fixture.Custody.SaveAsync("tenant-a", current.Version, current.Head, token)).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => observer.ObserveAsync(recipe, [original.Binding.Identity.Target], token));
    }
}
