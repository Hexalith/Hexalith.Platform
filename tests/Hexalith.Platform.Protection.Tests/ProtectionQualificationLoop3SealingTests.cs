using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual role/driver sealing, content-free snapshot parsing, independent producer retention and exact recovery through persisted registry/writer originals.</summary>
public sealed class ProtectionQualificationLoop3SealingTests
{
    [Theory]
    [InlineData("snapshot-pascal")]
    [InlineData("snapshot-lowercase")]
    [InlineData("snapshot-seed")]
    [InlineData("snapshot-producer-retention-loss")]
    [InlineData("seed-retention-loss")]
    [InlineData("producer-retention-loss")]
    public async Task SourceOwnedSealAndSnapshotRecoverExactProducerWithoutAnotherWrite(string mode)
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>(); using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var cells = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var seed = fixture.Enroll() with { Writer = new("source", "seed-writer", new()) };
        fixture.Source.Enrollments[ErasureRootOccurrenceBindings.Id(seed.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(seed.Binding.Identity))] = seed;
        byte[] seedBytes = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = seed.Binding.Identity.Target, Value = new { CanaryBase64 = Convert.ToBase64String("seed-canary"u8) } } });
        var seedWrite = await fixture.Engine.ProtectEventPayloadAsync(seed.Binding.Identity.Owner, Substitute.For<IEventPayload>(), seed.Binding.Identity.PayloadTypeId, seedBytes, "json", ErasureRootOccurrenceBindings.Occurrence(seed.Binding.Identity), token);
        await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(seedWrite.CompletionContext!, token);
        fixture.Source.Persisted[seed.Binding.OperationId] = seedWrite.ProtectionResult.PayloadBytes;
        await fixture.Engine.CompletePayloadProtectionAsync(seedWrite.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token); await fixture.Keys.CompleteDisposedUsesAsync(token);
        cells["seed"] = JsonSerializer.SerializeToElement(new ProtectionQualificationPersistedEvent(seed.Binding, seedWrite.ProtectionResult));
        cells["seed-writer"] = JsonSerializer.SerializeToElement(new ErasureRootWriterProof(seed.Binding, Convert.ToHexString(SHA256.HashData(seedWrite.ProtectionResult.PayloadBytes)), true, true, "exact-native-seed-writer"));
        bool snapshot = mode.StartsWith("snapshot", StringComparison.Ordinal);
        var output = fixture.Enroll("directory", "01ARZ3NDEKTSV4RRFFQ69G5FAW", 2) with { Writer = new("source", "output-writer", new()) };
        if (snapshot) { output = output with { Binding = output.Binding with { Identity = output.Binding.Identity with { Kind = PayloadProtectionPayloadKind.Snapshot, PayloadTypeId = "hx-snapshot-v1:state" } } }; }
        fixture.Source.Enrollments[ErasureRootOccurrenceBindings.Id(output.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(output.Binding.Identity))] = output;
        var head = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        foreach (var enrollment in new[] { seed, output }) { head.Head.SourceOriginals[ErasureRootOccurrenceBindings.Id(enrollment.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(enrollment.Binding.Identity))] = enrollment; }
        var seedRecord = (await fixture.Registry.LookupAsync(seed.Binding.Identity, token)).Record!;
        string seedId = ErasureRootOccurrenceBindings.Id(seed.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(seed.Binding.Identity));
        head.Head.SealedOriginals[seedId] = ErasureRootCustody.Hash(new { seedRecord.Request, result = seedRecord.Sealed });
        string seedLocation = ProtectionQualificationNativeCell.LocationId(new("source", "seed", new()));
        head.Head.QualificationSeedOriginals[seedLocation] = new(seed.Binding, seedLocation, Convert.ToHexString(SHA256.HashData(seedWrite.ProtectionResult.PayloadBytes)));
        (await fixture.Custody.SaveAsync("tenant-a", head.Version, head.Head, token)).ShouldBeTrue();
        native.GetStateAsync<JsonElement>("installation", "proof", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(JsonSerializer.SerializeToElement(new { agentsRuntimeEnabled = false, storeIdentity = "erc-test", deploymentIdentity = "isolated-component" }));
        native.GetStateAsync<JsonElement>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call => cells.GetValueOrDefault(call.ArgAt<string>(1)));
        native.GetStateAsync<ProtectionQualificationPersistedEvent>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call => cells.TryGetValue(call.ArgAt<string>(1), out var value) ? value.Deserialize<ProtectionQualificationPersistedEvent>()! : null!);
        native.GetStateAsync<SnapshotProtectionResult>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call => cells.TryGetValue(call.ArgAt<string>(1), out var value) ? value.Deserialize<SnapshotProtectionResult>()! : null!);
        native.GetStateAsync<ErasureRootWriterProof>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call => cells.TryGetValue(call.ArgAt<string>(1), out var value) ? value.Deserialize<ErasureRootWriterProof>()! : null!);
        native.GetStateAsync<PayloadProtectionCompletionContext>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call => cells.TryGetValue(call.ArgAt<string>(1), out var value) ? value.Deserialize<PayloadProtectionCompletionContext>()! : null!);
        native.GetStateAsync<ProtectionQualificationProducerOriginal>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call => cells.TryGetValue(call.ArgAt<string>(1), out var value) ? value.Deserialize<ProtectionQualificationProducerOriginal>()! : null!);
        int writes = 0;
        native.ExecuteStateTransactionAsync("source", Arg.Any<IReadOnlyList<StateTransactionRequest>>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            writes++;
            foreach (var operation in call.ArgAt<IReadOnlyList<StateTransactionRequest>>(1)) { cells[operation.Key] = JsonDocument.Parse(operation.Value).RootElement.Clone(); }
            byte[] persisted = snapshot ? JsonSerializer.SerializeToUtf8Bytes(cells["output"].Deserialize<SnapshotProtectionResult>()!.State) : cells["output"].Deserialize<ProtectionQualificationPersistedEvent>()!.Protected.PayloadBytes;
            fixture.Source.Persisted[output.Binding.OperationId] = persisted;
            if (mode == "seed-retention-loss") { fixture.Permissions.DeniedOperations.Add("RetainQualificationSeedOriginal"); }
            if (mode.Contains("producer-retention-loss", StringComparison.Ordinal)) { fixture.Permissions.DeniedOperations.Add("RetainQualificationProducerOriginal"); }
            return Task.CompletedTask;
        });
        var input = new ProtectionQualificationNativeSeal(output, new("source", "output", new())) { SeedSource = new("source", "seed", new()) };
        var payload = (JsonObject)JsonSerializer.SerializeToNode(input)!;
        if (mode == "snapshot-lowercase") { payload["enrollment"] = payload["Enrollment"]!.DeepClone(); payload.Remove("Enrollment"); payload["source"] = payload["Source"]!.DeepClone(); payload.Remove("Source"); }
        var arguments = ProtectionQualificationSourceCatalog.RequiredArguments().ToDictionary(pair => pair.Key, pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal) ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(pair.Value[6..]) } } : new JsonObject(), StringComparer.Ordinal);
        var recipes = ProtectionQualificationSourceCatalog.Compile(arguments);
        var call = new ProtectionQualificationNativeCall("exact-seal", snapshot ? ProtectionQualificationNativeCallKind.SealSnapshot : ProtectionQualificationNativeCallKind.SealEvent, payload);
        string caseId = snapshot ? "P10" : "P01", action = snapshot ? "SealSnapshotAndCache" : "SealDirectoryOutbox";
        recipes[caseId][action] = new([], call, null, []);
        string binding = new('a', 64);
        var request = new JsonObject { ["operationId"] = "source-write", ["action"] = action, ["bindingDigest"] = binding, ["caseId"] = caseId, ["fixtureId"] = "fixture", ["targets"] = JsonSerializer.SerializeToNode(new[] { output.Binding.Identity.Target }) };
        if (mode != "snapshot-seed") { request["canaryBase64"] = Convert.ToBase64String("seed-canary"u8); }
        var journal = new ProtectionQualificationOperationJournal(fixture.Custody, fixture.Permissions);
        await journal.UpdateAsync("tenant-a", "admission", "QualificationExecution", ProtectionQualificationOperationJournal.Digest(request), current => { current.QualificationOperations.Add("executor:source-write", new("executor", binding, "attempt", ProtectionQualificationOperationJournal.Digest(request), new JsonObject())); return true; }, token);
        var faults = new ProtectionQualificationFaults(native, new("controls", "controls", new()), "component", new HashSet<string>());
        var driver = new ProtectionQualificationNativeDriver(fixture.Engine, fixture.Custody, fixture.Keys, fixture.Source, native, null!, null!, null!, null!, null!, faults, new HashSet<string> { "source" }, fixture.Permissions);
        var scope = new ProtectionQualificationServiceScope("executor", "selected-executor", "tenant-a", binding, new("installation", "proof", new()), recipes, recipes.Keys.ToDictionary(id => id, id => new ProtectionQualificationObservationRecipe("tenant-a", new(), new()) { CaseId = id }));
        var service = new ProtectionQualificationRoleService(scope, journal, new(driver), null, native, signer);
        bool loss = mode.EndsWith("retention-loss", StringComparison.Ordinal);
        if (loss)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => service.HandleAsync(request, token));
            writes.ShouldBe(1); (await fixture.Registry.LookupAsync(output.Binding.Identity, token)).Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
            var admitted = (await journal.ReadAsync("tenant-a", token)).QualificationOperations["executor:source-write"];
            admitted.Result.ShouldBeNull(); admitted.Calls["exact-seal"].Result.ShouldBeNull();
            fixture.Permissions.DeniedOperations.Clear(); driver.Producer = driver.Producer with { EngineBinarySha256 = new string('f', 64) };
            var producerFrame = cells["output:producer"];
            cells.Remove("output:producer");
            (await driver.RecoverAsync(admitted.Calls["exact-seal"].Call, token)).ShouldBeNull();
            cells["output:producer"] = producerFrame;
            var originalCompletion = cells["output:completion"];
            foreach (bool composite in new[] { false, true })
            {
                var changedCompletion = JsonNode.Parse(originalCompletion.GetRawText())!.AsObject();
                if (composite) { changedCompletion["SnapshotEntries"] = new JsonArray(); } else { changedCompletion["DekVersion"] = 2; }
                cells["output:completion"] = JsonSerializer.SerializeToElement(changedCompletion);
                (await driver.RecoverAsync(admitted.Calls["exact-seal"].Call, token)).ShouldBeNull();
                (await journal.ReadAsync("tenant-a", token)).QualificationProducerOriginals.ShouldBeEmpty();
            }
            cells["output:completion"] = originalCompletion;
            (await driver.RecoverAsync(admitted.Calls["exact-seal"].Call with { Id = "undispatched" }, token)).ShouldBeNull();
            await service.HandleAsync(new() { ["operationId"] = "lookup", ["action"] = "LookupOriginalOperation", ["bindingDigest"] = binding, ["originalOperationId"] = "source-write", ["originalRequestSha256"] = ProtectionQualificationOperationJournal.Digest(request) }, token);
            writes.ShouldBe(1);
            (await journal.ReadAsync("tenant-a", token)).QualificationProducerOriginals.Values.Single().Producer.ShouldBe(admitted.Producer);
        }
        else { await service.HandleAsync(request, token); writes.ShouldBe(1); }
        var completedOperation = (await journal.ReadAsync("tenant-a", token)).QualificationOperations["executor:source-write"];
        var changedRequest = request.DeepClone().AsObject();
        changedRequest["operationId"] = "undispatched-changed-input";
        changedRequest["canaryBase64"] = Convert.ToBase64String("different-unwritten-canary"u8);
        string changedDigest = ProtectionQualificationOperationJournal.Digest(changedRequest);
        var changedCall = completedOperation.Calls["exact-seal"].Call with { OperationId = "undispatched-changed-input", RequestSha256 = changedDigest };
        await journal.UpdateAsync("tenant-a", "changed-admission", "QualificationExecution", changedDigest, current =>
        {
            current.QualificationOperations.Add("executor:undispatched-changed-input", new("executor", binding, "attempt", changedDigest, new JsonObject()) { Producer = completedOperation.Producer });
            return true;
        }, token);
        await journal.AdmitCallAsync("tenant-a", "executor", "undispatched-changed-input", changedCall, token);
        (await driver.RecoverAsync(changedCall, token)).ShouldBeNull();
        writes.ShouldBe(1);
        await fixture.Keys.CompleteDisposedUsesAsync(token);
        var observer = new ProtectionQualificationPersistedObserver(native, fixture.Custody, fixture.Engine, new HashSet<string> { "source" });
        var observed = await observer.ObserveAsync(new("tenant-a", new() { ["output"] = input.Source with { CopyKind = snapshot ? "snapshot" : "source-outbox" } }, new()) { CaseId = "P01" }, [output.Binding.Identity.Target], token, completedOperation.Producer, new HashSet<string> { "source-write" });
        observed["copyOriginals"]!.AsArray().Count.ShouldBe(1);
        observed["operationProducerOriginals"] = new JsonArray(new JsonObject { ["OperationId"] = "source-write", ["RequestSha256"] = completedOperation.RequestSha256,
            ["Role"] = "executor", ["BindingDigest"] = binding, ["AttemptId"] = "attempt", ["Producer"] = JsonSerializer.SerializeToNode(completedOperation.Producer) });
        var originalProducerProof = (await journal.ReadAsync("tenant-a", token)).QualificationProducerOriginals.Values.Single();
        foreach (var substitutedCall in new[] { originalProducerProof.Call! with { OperationId = "undispatched-changed-input" }, originalProducerProof.Call! with { RequestSha256 = changedDigest } })
        {
            await journal.UpdateAsync("tenant-a", "corrupt-proof", "QualificationExecution", changedDigest, current => { current.QualificationProducerOriginals[originalProducerProof.SourceIdentity] = originalProducerProof with { Call = substitutedCall }; return true; }, token);
            await Should.ThrowAsync<InvalidOperationException>(() => observer.ObserveAsync(new("tenant-a", new() { ["output"] = input.Source with { CopyKind = snapshot ? "snapshot" : "source-outbox" } }, new()) { CaseId = "P01" }, [output.Binding.Identity.Target], token, completedOperation.Producer, new HashSet<string> { "source-write" }));
        }
        await journal.UpdateAsync("tenant-a", "restore-proof", "QualificationExecution", completedOperation.RequestSha256, current => { current.QualificationProducerOriginals[originalProducerProof.SourceIdentity] = originalProducerProof; return true; }, token);
        if (Environment.GetEnvironmentVariable("EXT_PROTECTION_LOOP3_SEAL_VECTORS") is { Length: > 0 } directory)
        { await File.WriteAllTextAsync(Path.Combine(directory, mode + ".json"), observed.ToJsonString(), token); }
    }
}
