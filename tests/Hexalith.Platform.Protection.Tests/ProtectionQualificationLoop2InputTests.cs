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

/// <summary>Actual role/journal input validation precedes admitted-operation metadata and every source/root/registry effect.</summary>
public sealed class ProtectionQualificationLoop2InputTests
{
    [Theory]
    [InlineData("missing-seed-permission")]
    [InlineData("missing-producer-permission")]
    [InlineData("absent")]
    [InlineData("empty")]
    [InlineData("malformed")]
    [InlineData("unusable-seed")]
    [InlineData("valid-seed")]
    [InlineData("seed-missing-receipt")]
    [InlineData("seed-empty-receipt")]
    [InlineData("seed-blank-receipt")]
    [InlineData("mixed-path")]
    [InlineData("mixed-partition")]
    [InlineData("mixed-store")]
    [InlineData("mixed-lifecycle")]
    [InlineData("mixed-purpose")]
    public async Task CompleteRolePreflightRejectsUnusableCanaryBeforeAnyMutationAndAcceptsExactSealedSeed(string mode)
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>(); using var signing = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var original = fixture.Enroll() with { Writer = new("source", "seed-writer", new()) };
        var state = new Dictionary<string, JsonElement>();
        bool persistedSeed = mode == "valid-seed" || mode.StartsWith("seed-", StringComparison.Ordinal);
        if (persistedSeed)
        {
            byte[] clear = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = original.Binding.Identity.Target, Value = new { CanaryBase64 = Convert.ToBase64String("seed-canary"u8) } } });
            var sealedSeed = await fixture.Engine.ProtectEventPayloadAsync(original.Binding.Identity.Owner, Substitute.For<IEventPayload>(), original.Binding.Identity.PayloadTypeId, clear, "json", ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), token);
            await fixture.Engine.AcquirePayloadProtectionCompletionLeaseAsync(sealedSeed.CompletionContext!, token);
            fixture.Source.Persisted.Add(original.Binding.OperationId, sealedSeed.ProtectionResult.PayloadBytes);
            await fixture.Engine.CompletePayloadProtectionAsync(sealedSeed.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token);
            await fixture.Keys.CompleteDisposedUsesAsync(token);
            state["seed"] = JsonSerializer.SerializeToElement(new ProtectionQualificationPersistedEvent(original.Binding, sealedSeed.ProtectionResult));
            var head = (await fixture.Custody.ReadAsync("tenant-a", token, true))!.Value;
            string id = ErasureRootOccurrenceBindings.Id(original.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity));
            head.Head.SourceOriginals[id] = original;
            var seedRecord = (await fixture.Registry.LookupAsync(original.Binding.Identity, token)).Record!;
            head.Head.SealedOriginals[id] = ErasureRootCustody.Hash(new { seedRecord.Request, result = seedRecord.Sealed });
            string seedLocation = ProtectionQualificationNativeCell.LocationId(new("source", "seed", new()));
            head.Head.QualificationSeedOriginals[seedLocation] = new(original.Binding, seedLocation, Convert.ToHexString(SHA256.HashData(sealedSeed.ProtectionResult.PayloadBytes)));
            (await fixture.Custody.SaveAsync("tenant-a", head.Version, head.Head, token)).ShouldBeTrue();
        }
        var outputSeed = fixture.Enroll("directory", "01ARZ3NDEKTSV4RRFFQ69G5FAW", 2);
        var enrollment = outputSeed with { Writer = new("source", "output-writer", new()) };
        fixture.Source.Enrollments[ErasureRootOccurrenceBindings.Id(enrollment.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(enrollment.Binding.Identity))] = enrollment;
        var input = new ProtectionQualificationNativeSeal(enrollment, new("source", "output", new()))
        { SeedSource = mode == "unusable-seed" || persistedSeed ? new("source", "seed", new()) : null };
        native.GetStateAsync<JsonElement>("installation", "proof", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.SerializeToElement(new { agentsRuntimeEnabled = false, storeIdentity = "erc-test", deploymentIdentity = "component-only" }));
        native.GetStateAsync<ProtectionQualificationPersistedEvent>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => state.TryGetValue(call.ArgAt<string>(1), out var value) ? value.Deserialize<ProtectionQualificationPersistedEvent>()! : null!);
        native.GetStateAsync<ErasureRootWriterProof>("source", "seed-writer", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => persistedSeed ? new(original.Binding, Convert.ToHexString(SHA256.HashData(state["seed"].Deserialize<ProtectionQualificationPersistedEvent>()!.Protected.PayloadBytes)), true, true,
                mode switch { "seed-missing-receipt" => null!, "seed-empty-receipt" => "", "seed-blank-receipt" => " \t", _ => "exact-source-receipt" }) : null!);
        native.ExecuteStateTransactionAsync("source", Arg.Any<IReadOnlyList<StateTransactionRequest>>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            foreach (var operation in call.ArgAt<IReadOnlyList<StateTransactionRequest>>(1))
            {
                state[operation.Key] = JsonDocument.Parse(operation.Value).RootElement.Clone();
                if (operation.Key == "output") { fixture.Source.Persisted[enrollment.Binding.OperationId] = state[operation.Key].Deserialize<ProtectionQualificationPersistedEvent>()!.Protected.PayloadBytes; }
            }
            return Task.CompletedTask;
        });
        var args = ProtectionQualificationSourceCatalog.RequiredArguments().ToDictionary(pair => pair.Key, pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal)
            ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(pair.Value[6..]) } } : new JsonObject(), StringComparer.Ordinal);
        var recipes = ProtectionQualificationSourceCatalog.Compile(args);
        var call = new ProtectionQualificationNativeCall("P01/seal/1", ProtectionQualificationNativeCallKind.SealEvent, (JsonObject)JsonSerializer.SerializeToNode(input)!);
        ProtectionTarget[] targets = [enrollment.Binding.Identity.Target];
        if (mode.StartsWith("mixed-", StringComparison.Ordinal))
        {
            var secondSeed = fixture.Enroll("directory", "01ARZ3NDEKTSV4RRFFQ69G5FAX", 3);
            targets = [enrollment.Binding.Identity.Target, new("tenant-a", "interaction-b", "alias-b")];
            var parent = new PayloadProtectionOccurrenceContext(88, PayloadProtectionPayloadKind.Snapshot, "hx-snapshot-v1:directory-state");
            var stateBytes = JsonSerializer.SerializeToUtf8Bytes(new { Entries = targets.Select(target => new { ProtectedContent = new { Target = target, Value = new { CanaryBase64 = "cHJlZmxpZ2h0" } } }).ToArray() });
            var layout = InteractionSnapshotEntries.Describe(stateBytes, "tenant-a", token);
            ErasureRootOccurrenceEnrollment[] entries = [enrollment, secondSeed];
            for (int index = 0; index < entries.Length; index++)
            {
                var occurrence = InteractionSnapshotEntries.EntryOccurrence(parent, layout.Entries[index].Path);
                entries[index] = entries[index] with { Binding = entries[index].Binding with { Identity = entries[index].Binding.Identity with { Target = targets[index], Kind = PayloadProtectionPayloadKind.Snapshot, Sequence = occurrence.RecordSequence, PayloadTypeId = occurrence.PayloadTypeId } },
                    RootIdentity = entries[index].RootIdentity with { ObjectId = targets[index].AgentInteractionId, KeyAlias = targets[index].TargetProtectionKeyAlias }, Writer = new("source", "writer-" + index, new()) };
            }
            entries[1] = mode switch
            {
                "mixed-path" => entries[1] with { Binding = entries[1].Binding with { Identity = entries[1].Binding.Identity with { PayloadTypeId = "substituted-path" } } },
                "mixed-partition" => entries[1] with { Writer = entries[1].Writer! with { Metadata = new() { ["partitionKey"] = "other" } } },
                "mixed-store" => entries[1] with { RootIdentity = entries[1].RootIdentity with { StoreTarget = "other" } },
                "mixed-lifecycle" => entries[1] with { ExpectedLifecycleRevision = -1 },
                "mixed-purpose" => entries[1] with { RootIdentity = entries[1].RootIdentity with { Purpose = Hexalith.Platform.Custody.PlatformKeyPurpose.DigestKey } },
                _ => entries[1],
            };
            call = new("P01/seal/1", ProtectionQualificationNativeCallKind.SealSnapshot, (JsonObject)JsonSerializer.SerializeToNode(new ProtectionQualificationNativeSnapshot(entries, parent, new("source", "snapshot", new()) { CompletionKey = "completion" }))!);
        }
        recipes["P01"]["SealDirectoryOutbox"] = mode.StartsWith("mixed-", StringComparison.Ordinal)
            ? new([call], new("P01/seal/2", ProtectionQualificationNativeCallKind.SealEvent, (JsonObject)JsonSerializer.SerializeToNode(input)!), null, [])
            : new([], call, null, []);
        var observations = recipes.Keys.ToDictionary(id => id, id => new ProtectionQualificationObservationRecipe("tenant-a", new(), new()) { CaseId = id });
        string binding = new('a', 64);
        var request = new JsonObject { ["operationId"] = "seal-operation", ["action"] = "SealDirectoryOutbox", ["bindingDigest"] = binding, ["caseId"] = "P01", ["fixtureId"] = "fixture", ["targets"] = JsonSerializer.SerializeToNode(targets) };
        if (mode.StartsWith("mixed-", StringComparison.Ordinal)) { request["canaryBase64"] = "cHJlZmxpZ2h0"; }
        if (mode == "missing-seed-permission") { fixture.Permissions.DeniedOperations.Add("RetainQualificationSeedOriginal"); request["canaryBase64"] = "cHJlZmxpZ2h0"; }
        if (mode == "missing-producer-permission") { fixture.Permissions.DeniedOperations.Add("RetainQualificationProducerOriginal"); request["canaryBase64"] = "cHJlZmxpZ2h0"; }
        if (mode == "empty") { request["canaryBase64"] = ""; } if (mode == "malformed") { request["canaryBase64"] = "%%%"; }
        var journal = new ProtectionQualificationOperationJournal(fixture.Custody, fixture.Permissions);
        await journal.UpdateAsync("tenant-a", "admission", "QualificationNativeCall", new string('a', 64), head =>
        { head.QualificationOperations.Add("executor:seal-operation", new("executor", binding, "attempt", ProtectionQualificationOperationJournal.Digest(request), new JsonObject { ["operationId"] = "seal-operation" })); return true; }, token);
        var faults = new ProtectionQualificationFaults(native, new("controls", "controls", new()), "component", new HashSet<string>());
        var driver = new ProtectionQualificationNativeDriver(fixture.Engine, fixture.Custody, fixture.Keys, fixture.Source, native, null!, null!, null!, null!, null!, faults, new HashSet<string> { "source" }, fixture.Permissions);
        var service = new ProtectionQualificationRoleService(new("executor", "component", "tenant-a", binding, new("installation", "proof", new()), recipes, observations), journal, new(driver), null, native, signing);
        byte[] before = (await fixture.Store.ReadHeadAsync("tenant-a", token))!.Bytes;
        if (mode == "valid-seed")
        {
            await service.HandleAsync(request, token); state.ContainsKey("output").ShouldBeTrue();
            state["output"].GetRawText().ShouldNotContain("seed-canary");
        }
        else
        {
            await Assert.ThrowsAnyAsync<Exception>(() => service.HandleAsync(request, token));
            (await fixture.Store.ReadHeadAsync("tenant-a", token))!.Bytes.ShouldBe(before);
            state.ContainsKey("output").ShouldBeFalse();
            (await fixture.Custody.ReadAsync("tenant-a", token, true))!.Value.Head.QualificationOperations["executor:seal-operation"].Calls.ShouldBeEmpty();
        }
    }
}
