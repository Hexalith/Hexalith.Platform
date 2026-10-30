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
        var original = fixture.Enroll() with { Writer = new("read-only", "writer", new()) }; var token = TestContext.Current.CancellationToken;
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = original.Binding.Identity.Target, Value = "projection-canary" } });
        var write = await fixture.Engine.ProtectEventPayloadAsync(original.Binding.Identity.Owner, Substitute.For<IEventPayload>(), original.Binding.Identity.PayloadTypeId,
            payload, "json", ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), token);
        await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, token);
        fixture.Source.Persisted[original.Binding.OperationId] = write.ProtectionResult.PayloadBytes;
        await fixture.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token);
        var current = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        current.Head.SourceOriginals.Add("actual-source-original", original);
        string location = ProtectionQualificationNativeCell.LocationId(new("read-only", "source", new()));
        current.Head.QualificationProducerOriginals.Add(location, new(location, [original.Binding], Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(write.ProtectionResult.PayloadBytes)),
            ErasureRootCustody.Hash(write.CompletionContext), write.CompletionContext!, ProtectionQualificationProducerIdentity.Capture(fixture.Engine, fixture.Custody.Installation)));
        native.GetStateAsync<ErasureRootWriterProof>("read-only", "writer", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(new ErasureRootWriterProof(original.Binding, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(write.ProtectionResult.PayloadBytes)), true, true, "actual-retired-writer"));
        (await fixture.Custody.SaveAsync("tenant-a", current.Version, current.Head, token)).ShouldBeTrue();
        JsonElement persistedValue = JsonSerializer.SerializeToElement(new ProtectionQualificationPersistedEvent(original.Binding, write.ProtectionResult));
        native.GetStateAsync<JsonElement>("read-only", "source", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => persistedValue);
        var recipe = new ProtectionQualificationObservationRecipe("tenant-a", new() { ["source"] = new("read-only", "source", new()) { CopyKind = "source-outbox" } },
            new() { ["records"] = new("source", "/not-a-native-field") }) { CaseId = "P01" };
        var observer = new ProtectionQualificationPersistedObserver(native, fixture.Custody, fixture.Engine, new HashSet<string> { "read-only" });
        var actual = await observer.ObserveAsync(recipe, [original.Binding.Identity.Target], token);
        var record = actual["records"]![0]!; record["kind"]!.GetValue<string>().ShouldBe("source-outbox");
        byte[] persisted = Convert.FromBase64String(record["bytesBase64"]!.GetValue<string>()); persisted.ShouldBe(write.ProtectionResult.PayloadBytes);
        Encoding.UTF8.GetString(persisted).ShouldNotContain("projection-canary");
        native.ReceivedCalls().ShouldAllBe(call => call.GetMethodInfo().Name == "GetStateAsync");
        var selectedProducer = ProtectionQualificationProducerIdentity.Capture(fixture.Engine, fixture.Custody.Installation);
        foreach (int offset in new[] { 28, 54, 66, -1 })
        {
            var changed = System.Text.Json.Nodes.JsonNode.Parse(write.ProtectionResult.PayloadBytes)!;
            string encoded = changed["ProtectedContent"]!["Value"]!["$pdenc"]!.GetValue<string>();
            byte[] envelope = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/') + new string('=', (4 - encoded.Length % 4) % 4));
            envelope[offset < 0 ? envelope.Length + offset : offset] ^= 1;
            changed["ProtectedContent"]!["Value"]!["$pdenc"] = Convert.ToBase64String(envelope).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            persistedValue = JsonSerializer.SerializeToElement(new ProtectionQualificationPersistedEvent(original.Binding, write.ProtectionResult with { PayloadBytes = JsonSerializer.SerializeToUtf8Bytes(changed) }));
            await Should.ThrowAsync<InvalidOperationException>(() => observer.ObserveAsync(recipe, [original.Binding.Identity.Target], token, selectedProducer));
        }
        persistedValue = JsonSerializer.SerializeToElement(new ProtectionQualificationPersistedEvent(original.Binding, write.ProtectionResult));
        await Should.ThrowAsync<InvalidOperationException>(() => observer.ObserveAsync(recipe, [original.Binding.Identity.Target], token, selectedProducer with { EngineBinarySha256 = new string('f', 64) }));
        var historical = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        historical.Head.QualificationOperations.Add("executor:prior-case", new("executor", new string('b', 64), "prior-attempt", new string('c', 64), new System.Text.Json.Nodes.JsonObject { ["caseId"] = "P01" })
            { Producer = selectedProducer with { EngineBinarySha256 = new string('e', 64), BindingDigest = new string('b', 64) } });
        (await fixture.Custody.SaveAsync("tenant-a", historical.Version, historical.Head, token)).ShouldBeTrue();
        (await observer.ObserveAsync(recipe, [original.Binding.Identity.Target], token, selectedProducer))["engineBinarySha256"]!.GetValue<string>().ShouldBe(selectedProducer.EngineBinarySha256);

        current = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value; current.Head.SourceOriginals.Clear();
        (await fixture.Custody.SaveAsync("tenant-a", current.Version, current.Head, token)).ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => observer.ObserveAsync(recipe, [original.Binding.Identity.Target], token));
    }
}
