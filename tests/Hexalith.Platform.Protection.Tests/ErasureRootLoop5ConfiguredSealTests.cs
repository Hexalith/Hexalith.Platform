using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Actors.Runtime;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual pinned host/role/driver/native source/registry sealing checks; only native storage and independent permission eligibility are component fixtures.</summary>
public sealed class ErasureRootLoop5ConfiguredSealTests
{
    [Theory]
    [InlineData("event", null)]
    [InlineData("single", null)]
    [InlineData("mixed", null)]
    [InlineData("event", "EnrollOccurrence")]
    [InlineData("single", "EnrollOccurrence")]
    [InlineData("mixed", "EnrollOccurrence")]
    [InlineData("event", "CompleteWriter")]
    [InlineData("single", "AcquireCompletionLease")]
    [InlineData("mixed", "AcquireSnapshotCompletionLeases")]
    [InlineData("event", "RetainSealed")]
    [InlineData("single", "Reserve")]
    [InlineData("mixed", "VerifySnapshotEntryManifest")]
    [InlineData("event", "RegisterIndependentSourceOriginal")]
    [InlineData("single", "RetainOccurrenceIndex")]
    [InlineData("mixed", "MigrateOccurrenceIndex")]
    [InlineData("event", "AliasedWriterSource")]
    [InlineData("single", "AliasedCompletionSource")]
    [InlineData("mixed", "AliasedEntryWriters")]
    public async Task MissingInputsRefuseBeforeActualSourceAndJournalEffectsWhileAllThreeConfiguredSealPathsWork(string mode, string? missing)
    {
        var token = TestContext.Current.CancellationToken; using var fixture = new ErasureRootEngineFixture(); using var independent = new ErasureRootIndependentSourceFixture();
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string directory = Path.Combine(Path.GetTempPath(), "erc-loop5-configured-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        string nativeVariable = "ERC_LOOP5_" + Guid.NewGuid().ToString("N").ToUpperInvariant(); Environment.SetEnvironmentVariable(nativeVariable, "synthetic-native-bearer");
        try
        {
            var cells = new Dictionary<string, JsonElement>(StringComparer.Ordinal); int sourceTransactions = 0;
            independent.Native.GetStateAsync<JsonElement>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(call => cells.GetValueOrDefault(call.ArgAt<string>(1)));
            independent.Native.GetStateAsync<ErasureRootWriterProof>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(call => cells.TryGetValue(call.ArgAt<string>(1), out var value) ? value.Deserialize<ErasureRootWriterProof>()! : null!);
            independent.Native.GetStateAsync<SnapshotProtectionResult>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(call => cells.TryGetValue(call.ArgAt<string>(1), out var value) ? value.Deserialize<SnapshotProtectionResult>()! : null!);
            independent.Native.ExecuteStateTransactionAsync("source", Arg.Any<IReadOnlyList<StateTransactionRequest>>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(call => { sourceTransactions++; foreach (var operation in call.ArgAt<IReadOnlyList<StateTransactionRequest>>(1)) { cells[operation.Key] = JsonDocument.Parse(operation.Value).RootElement.Clone(); } return Task.CompletedTask; });
            cells["installation"] = JsonSerializer.SerializeToElement(new { agentsRuntimeEnabled = false, storeIdentity = "erc-test", deploymentIdentity = "configured-component" });
            (string Path, string Hash) Write(string name, byte[] bytes) { string path = Path.Combine(directory, name); File.WriteAllBytes(path, bytes); return (path, Convert.ToHexString(SHA256.HashData(bytes))); }
            var inventory = Write("inventory.json", "{\"runtime\":{\"currentInventoryAuthorityReference\":\"inventory-authority\"}}"u8.ToArray()); var policy = Write("copy.json", "{}"u8.ToArray());
            string privateBearer = Path.Combine(directory, "private-bearer"); File.WriteAllText(privateBearer, "synthetic-private-bearer");
            var authority = new Hexalith.Platform.Secrets.Runtime.IndependentDaprAuthorityBinding("private", "file:" + privateBearer, signer.ExportSubjectPublicKeyInfoPem(), "component", "tenant-a", "1");
            var installation = new ErasureRootHostInstallation(fixture.Custody.Installation with { TrustedCaCertificatePem = ErasureRootPinnedCa.Pem }, "https://native.invalid/", "env:" + nativeVariable, new() { ["tenant-a"] = "registry-1" }, authority, authority, authority, authority, authority,
                [new("tenant-a", "DeletionBatchCapabilitySigningKey", "version", "issuer", "audience", "anchor", "1", signer.ExportSubjectPublicKeyInfo(), 1, false)], inventory.Path, inventory.Hash, policy.Path, policy.Hash);
            var file = Write("host.json", JsonSerializer.SerializeToUtf8Bytes(installation));
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["EventStore:PayloadProtection:Enabled"] = "true", ["EventStore:PayloadProtection:InstallationFile"] = file.Path, ["EventStore:PayloadProtection:InstallationSha256"] = file.Hash }).Build();
            var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IEventPayloadProtectionService, NoOpEventPayloadProtectionService>();
            services.AddPlatformErasureRootProtection(configuration);
            var nativeSource = new ErasureRootNativeOccurrenceSource(fixture.Custody, fixture.Permissions, independent.Native, fixture.Digests,
                new Dictionary<string, ProtectionQualificationNativeCell> { ["tenant-a"] = new("independent", "root", new()) });
            services.Replace(ServiceDescriptor.Singleton<IErasureRootStore>(fixture.Store)); services.Replace(ServiceDescriptor.Singleton<IErasureRootAuthorization>(fixture.Permissions));
            services.Replace(ServiceDescriptor.Singleton<IErasureRootOccurrenceSource>(nativeSource)); services.Replace(ServiceDescriptor.Singleton(fixture.Digests));
            services.Replace(ServiceDescriptor.Singleton<IInteractionOccurrenceRegistry>(provider =>
            {
                var actor = new InteractionOccurrenceRegistryActor(ActorHost.CreateForTest<InteractionOccurrenceRegistryActor>(new ActorTestOptions { ActorId = new(InteractionOccurrenceRegistryActor.GetActorId("tenant-a")) }), provider.GetRequiredService<IInteractionOccurrenceAuthority>());
                typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, fixture.RegistryState);
                return new ErasureRootRegistryFixture(actor);
            }));
            await using var provider = services.BuildServiceProvider(); var engine = provider.GetRequiredService<IEventPayloadProtectionService>().ShouldBeOfType<InteractionPayloadProtectionService>();
            var keys = provider.GetRequiredService<ErasureRootOccurrenceKeyProvider>();
            var first = fixture.Enroll("configured-owner", "01ARZ3NDEKTSV4RRFFQ69G5FAV", 77) with { Writer = new("source", "writer-a", new()) };
            var target = first.Binding.Identity.Target; var parent = new PayloadProtectionOccurrenceContext(77, PayloadProtectionPayloadKind.Snapshot, "hx-snapshot-v1:configured-state");
            ErasureRootOccurrenceEnrollment Snapshot(ErasureRootOccurrenceEnrollment value, string path)
                => value with { Binding = value.Binding with { Identity = value.Binding.Identity with { Kind = PayloadProtectionPayloadKind.Snapshot,
                    PayloadTypeId = mode == "mixed" ? InteractionSnapshotEntries.EntryOccurrence(parent, path).PayloadTypeId : parent.PayloadTypeId } } };
            var sourceCell = new ProtectionQualificationNativeCell("source", "sealed", new()) { CompletionKey = "completion" };
            if (missing == "AliasedWriterSource") { first = first with { Writer = first.Writer! with { Key = sourceCell.Key } }; }
            if (missing == "AliasedCompletionSource") { sourceCell = sourceCell with { CompletionKey = sourceCell.Key }; }
            JsonObject payload; ProtectionTarget[] targets;
            if (mode == "mixed")
            {
                first = Snapshot(first, "/Entries/0/ProtectedContent/Value");
                var other = first with { Binding = first.Binding with { Identity = first.Binding.Identity with { Target = new("tenant-a", "interaction-b", "alias-b"), PayloadTypeId = InteractionSnapshotEntries.EntryOccurrence(parent, "/Entries/1/ProtectedContent/Value").PayloadTypeId },
                    KeyReference = "01ARZ3NDEKTSV4RRFFQ69G5FAW", OperationId = "01ARZ3NDEKTSV4RRFFQ69G5FAW" }, RootIdentity = first.RootIdentity with { ObjectId = "interaction-b", KeyAlias = "alias-b" }, RootOperationId = "root-b", Writer = new("source", "writer-b", new()) };
                if (missing == "AliasedEntryWriters") { other = other with { Writer = first.Writer }; }
                payload = (JsonObject)JsonSerializer.SerializeToNode(new ProtectionQualificationNativeSnapshot([first, other], parent, sourceCell))!; targets = [target, other.Binding.Identity.Target];
            }
            else { if (mode == "single") { first = Snapshot(first, ""); } payload = (JsonObject)JsonSerializer.SerializeToNode(new ProtectionQualificationNativeSeal(first, sourceCell))!; targets = [target]; }
            var kind = mode == "event" ? ProtectionQualificationNativeCallKind.SealEvent : ProtectionQualificationNativeCallKind.SealSnapshot;
            var call = new ProtectionQualificationNativeCall("configured-seal", kind, payload);
            var arguments = ProtectionQualificationSourceCatalog.RequiredArguments().ToDictionary(pair => pair.Key, pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal) ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(pair.Value[6..]) } } : new JsonObject());
            var recipes = ProtectionQualificationSourceCatalog.Compile(arguments); string caseId = mode == "event" ? "P01" : "P10", action = mode == "event" ? "SealDirectoryOutbox" : "SealSnapshotAndCache";
            recipes[caseId][action] = new([], call, null, []);
            string binding = new('a', 64); var request = new JsonObject { ["operationId"] = "seal", ["action"] = action, ["bindingDigest"] = binding, ["caseId"] = caseId, ["fixtureId"] = "fixture", ["targets"] = JsonSerializer.SerializeToNode(targets), ["canaryBase64"] = Convert.ToBase64String("configured-canary"u8) };
            var preparedHead = await fixture.Store.HeadAsync("tenant-a");
            string deletionScope = DeletionConsumptionActor.GetOwnerScope("tenant-a");
            byte[] ownerBytes = JsonSerializer.SerializeToUtf8Bytes(new JsonObject { ["Batches"] = new JsonArray() });
            string ownerDigest = Convert.ToHexString(SHA256.HashData(ownerBytes));
            var ownerOriginal = new AnchoredStateTransition(deletionScope, 0, 1, new string('0', 64), ownerDigest, ownerBytes);
            foreach (string ownerScope in new[] { deletionScope, ErasureRootOccurrenceAuthority.Scope("tenant-a"), ErasureRootTenantLifecycleAuthority.Scope("tenant-a") })
            {
                if (preparedHead.Anchors.ContainsKey(ownerScope)) { continue; }
                preparedHead.Anchors.Add(ownerScope, ownerScope == deletionScope
                    ? new(1, ownerDigest, new SortedDictionary<string, AnchoredStateTransition>(StringComparer.Ordinal), new SortedDictionary<string, AnchoredStateTransition>(StringComparer.Ordinal) { [ownerDigest] = ownerOriginal })
                    : new(0, ErasureRootCustody.Hash(new { tenant = "tenant-a", ownerScope }), [], []));
            }
            fixture.Store.ReplaceHead("tenant-a", preparedHead);
            var custody = provider.GetRequiredService<ErasureRootCustody>(); var journal = new ProtectionQualificationOperationJournal(custody, fixture.Permissions);
            await journal.UpdateAsync("tenant-a", "original-admission", "QualificationExecution", ProtectionQualificationOperationJournal.Digest(request), head => { head.QualificationOperations.Add("executor:seal", new("executor", binding, "attempt", ProtectionQualificationOperationJournal.Digest(request), new JsonObject())); return true; }, token);
            var faults = new ProtectionQualificationFaults(independent.Native, new("controls", "controls", new()), "enrollment", new HashSet<string>());
            var driver = new ProtectionQualificationNativeDriver(engine, custody, keys, nativeSource, independent.Native, null!, null!, null!, null!, null!, faults, new HashSet<string> { "source" }, fixture.Permissions);
            var scope = new ProtectionQualificationServiceScope("executor", "configured-executor", "tenant-a", binding, new("source", "installation", new()), recipes, recipes.Keys.ToDictionary(id => id, id => new ProtectionQualificationObservationRecipe("tenant-a", new(), new())));
            var role = new ProtectionQualificationRoleService(scope, journal, new(driver), null, independent.Native, signer);
            string before = ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a")); int cas = fixture.Store.CasCount, archives = fixture.Store.ArchiveWrites, independentRevision = independent.Revision;
            if (missing is not null)
            {
                fixture.Permissions.DeniedOperations.Add(missing); await Assert.ThrowsAsync<InvalidOperationException>(() => role.HandleAsync(request, token));
                ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a")).ShouldBe(before); fixture.Store.CasCount.ShouldBe(cas); fixture.Store.ArchiveWrites.ShouldBe(archives);
                independent.Revision.ShouldBe(independentRevision); independent.Nodes.ShouldBeEmpty(); sourceTransactions.ShouldBe(0); fixture.RegistryState.CommittedState.ShouldBeEmpty();
            }
            else
            {
                var result = await role.HandleAsync(request, token); result["statement"]!["response"]!["status"]!.GetValue<string>().ShouldBe("Completed");
                sourceTransactions.ShouldBe(1); independent.Root.OriginalIndexRoot.ShouldNotBeNull();
                var actual = await fixture.Store.HeadAsync("tenant-a"); actual.Targets.Count.ShouldBe(targets.Length); actual.OccurrenceIndexRoot.ShouldNotBeNull();
                actual.SourceOriginals.ShouldBeEmpty(); actual.Occurrences.ShouldBeEmpty(); fixture.RegistryState.CommittedState.ShouldNotBeEmpty();
                if (mode == "single")
                {
                    var locations = new Dictionary<string, ProtectionQualificationNativeCell> { ["snapshot"] = sourceCell with { CopyKind = "snapshot" }, ["cache"] = sourceCell with { Key = sourceCell.Key + ":cache", CopyKind = "cache" } };
                    foreach (var copyKind in new[] { ProtectionQualificationNativeCallKind.Backup, ProtectionQualificationNativeCallKind.Restore })
                    {
                        var copyCall = await driver.PrepareCallAsync(new("actual-" + copyKind, copyKind, (JsonObject)JsonSerializer.SerializeToNode(sourceCell)!) { OperationId = "copy-" + copyKind, RequestSha256 = new string('b', 64) }, token);
                        var copyResult = await driver.ExecuteAsync(copyCall, request, token);
                        await journal.UpdateAsync("tenant-a", copyCall.Id, "QualificationNativeResult", ErasureRootCustody.Hash(copyCall), head =>
                        { var copyOperation = new ProtectionQualificationOperation("executor", binding, "attempt", copyCall.RequestSha256!, (JsonObject)request.DeepClone()); copyOperation.Calls.Add(copyCall.Id, new(copyCall, copyResult)); head.QualificationOperations.Add("executor:copy-" + copyKind, copyOperation); return true; }, token);
                        string suffix = copyKind == ProtectionQualificationNativeCallKind.Backup ? "backup" : "restored";
                        locations.Add(suffix, sourceCell with { Key = sourceCell.Key + ":" + suffix, CopyKind = suffix });
                    }
                    var requestForTrim = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "deletion", "seal", "AcceptedSet", 0, "batch", DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key"), "component", 1, "dispatch", 2, targets);
                    (await custody.ConsumeAsync(requestForTrim, "reservation", token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
                    foreach (var location in locations)
                    {
                        var replayCall = await driver.PrepareCallAsync(new("replay-" + location.Key, ProtectionQualificationNativeCallKind.ReplayCopy, (JsonObject)JsonSerializer.SerializeToNode(location.Value)!) { OperationId = "replay-" + location.Key, RequestSha256 = new string('c', 64) }, token);
                        var replayResult = await driver.ExecuteAsync(replayCall, request, token); replayResult.ShouldNotBeNull();
                    }
                    fixture.Store.TrimCount.ShouldBe(1);
                    var observer = new ProtectionQualificationPersistedObserver(independent.Native, custody, engine, new HashSet<string> { "source" });
                    var observationRecipe = new ProtectionQualificationObservationRecipe("tenant-a", locations, new()) { CaseId = "P01" };
                    var observed = await observer.ObserveAsync(observationRecipe, targets, token, expectedOperationIds: new HashSet<string> { "seal", "copy-Backup", "copy-Restore" }, componentBackings: new Dictionary<string, string> { ["source"] = "component-physical-backing" });
                    observed["records"]!.AsArray().Count.ShouldBe(4); observed["copyLocations"]!.AsArray().Count.ShouldBe(4);
                    var completedSource = (await custody.ReadAsync("tenant-a", token, true))!.Value.Head.QualificationOperations["executor:seal"];
                    observed["operationProducerOriginals"] = new JsonArray(new JsonObject { ["OperationId"] = "seal", ["RequestSha256"] = completedSource.RequestSha256, ["Role"] = completedSource.Role,
                        ["BindingDigest"] = completedSource.BindingDigest, ["AttemptId"] = completedSource.AttemptId, ["Producer"] = JsonSerializer.SerializeToNode(completedSource.Producer) });
                    if (Environment.GetEnvironmentVariable("EXT_PROTECTION_COPY_VECTOR") is { Length: > 0 } vectorPath)
                    { await File.WriteAllTextAsync(vectorPath, JsonSerializer.Serialize(new { observation = observed, sourceComponentOnly = true, liveExecuted = false }), token); }
                    var aliases = locations.ToDictionary(item => item.Key, item => item.Value); aliases["cache"] = locations["snapshot"] with { CopyKind = "cache" };
                    await Should.ThrowAsync<InvalidOperationException>(() => observer.ObserveAsync(observationRecipe with { Sources = aliases }, targets, token, expectedOperationIds: new HashSet<string> { "seal", "copy-Backup", "copy-Restore" }));
                    var relabeled = locations.ToDictionary(item => item.Key, item => item.Value); relabeled["cache"] = relabeled["cache"] with { CopyKind = "backup" };
                    await Should.ThrowAsync<InvalidOperationException>(() => observer.ObserveAsync(observationRecipe with { Sources = relabeled }, targets, token, expectedOperationIds: new HashSet<string> { "seal", "copy-Backup", "copy-Restore" }));
                    string backupCallHash = ErasureRootCustody.Hash((await custody.ReadAsync("tenant-a", token, true))!.Value.Head.QualificationOperations["executor:copy-Backup"].Calls.Values.Single().Call);
                    string marker = cells.Keys.Single(key => key.Contains(":native-original:", StringComparison.Ordinal) && cells[key].GetProperty("CallDigest").GetString() == backupCallHash);
                    var brokenMarker = JsonNode.Parse(cells[marker].GetRawText())!; brokenMarker["SourceDigest"] = new string('F', 64); cells[marker] = JsonSerializer.SerializeToElement(brokenMarker);
                    await Should.ThrowAsync<InvalidOperationException>(() => observer.ObserveAsync(observationRecipe, targets, token, expectedOperationIds: new HashSet<string> { "seal", "copy-Backup", "copy-Restore" }));
                }
            }
        }
        finally { Environment.SetEnvironmentVariable(nativeVariable, null); Directory.Delete(directory, true); }
    }
}
