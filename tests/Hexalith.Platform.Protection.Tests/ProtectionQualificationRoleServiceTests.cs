using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Actors.Client;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual role service/journal and ERC-backed owner recovery. Native storage/private dispatch eligibility are explicit component boundaries.</summary>
public sealed class ProtectionQualificationRoleServiceTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    public async Task LostAggregateOrNativeResultRecoversOnlyExactOriginalWithoutAnotherDestruction(bool lostTrimAck, bool recoverViaPrepare, bool blockedReserve)
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new ErasureRootConsumptionFixture(); var manifest = await fixture.SeedAsync();
        (await fixture.Actor.RegisterAsync(manifest)).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed);
        object staleOwner = fixture.CaptureOwner();
        if (blockedReserve)
        { (await fixture.Actor.BlockAsync(new("tenant-a", manifest.Capability.BatchId, "genuine-owner-block", "current-admission-integrity"))).Status.ShouldBe(DeletionConsumptionStatus.ConsumptionBlocked); }
        using var engine = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>(); using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        native.GetStateAsync<JsonElement>("installation", "proof", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.SerializeToElement(new { agentsRuntimeEnabled = false, storeIdentity = "erc-v23", deploymentIdentity = "isolated-component" }));
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IDeletionConsumptionActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(fixture.Actor);
        var owner = new DaprDeletionProtectionOwner("tenant-a", proxies, TimeProvider.System);
        var controls = new ProtectionQualificationFaults(native, new("controls", "controls", new()), "component", new HashSet<string>());
        var driver = new ProtectionQualificationNativeDriver(engine.Engine, fixture.Custody, engine.Keys, engine.Source, native, null!, owner, fixture.Actor, null!, null!, controls, new HashSet<string>(), fixture.Permissions);
        var arguments = ProtectionQualificationSourceCatalog.RequiredArguments().ToDictionary(pair => pair.Key,
            pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal) ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(pair.Value[6..]) } } : new JsonObject(), StringComparer.Ordinal);
        var recipes = ProtectionQualificationSourceCatalog.Compile(arguments);
        recipes["P06"]["DestroyWithLostAcknowledgement"] = new([], new("original-native-reserve", ProtectionQualificationNativeCallKind.Reserve, (JsonObject)JsonSerializer.SerializeToNode(manifest)!), null, []);
        recipes["P06"]["LookupOriginalDestruction"] = new([], new("exact-owner-reconcile", ProtectionQualificationNativeCallKind.Lookup,
            new JsonObject { ["tenantId"] = "tenant-a", ["batchId"] = manifest.Capability.BatchId }), null, []);
        string binding = new('a', 64);
        var observations = recipes.Keys.ToDictionary(id => id, id => new ProtectionQualificationObservationRecipe("tenant-a", new(), new()) { CaseId = id });
        var scope = new ProtectionQualificationServiceScope("executor", "component-executor", "tenant-a", binding, new("installation", "proof", new()), recipes, observations);
        var journal = new ProtectionQualificationOperationJournal(fixture.Custody, fixture.Permissions);
        var executor = new ProtectionQualificationRoleService(scope, journal, new(driver), null, native, signer);
        var enrollment = new ProtectionQualificationRoleService(scope with { Role = "enrollment", Identity = "component-enrollment" }, journal, null, null, native, signer);
        var deniedEnrollment = new ProtectionQualificationRoleService(scope with { Role = "enrollment", Identity = "component-enrollment" }, journal, null, null, native, signer, _ => Task.FromResult(false));
        string initialHead = ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a"));
        await Should.ThrowAsync<InvalidOperationException>(() => deniedEnrollment.HandleAsync(new() { ["operationId"] = "absent-input", ["action"] = "EnrollSingleAttempt", ["bindingDigest"] = binding, ["attemptId"] = "single-attempt" }, token));
        ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a")).ShouldBe(initialHead);
        await enrollment.HandleAsync(new() { ["operationId"] = "attempt", ["action"] = "EnrollSingleAttempt", ["bindingDigest"] = binding, ["attemptId"] = "single-attempt" }, token);
        JsonObject Request(string id, string action) => new() { ["operationId"] = id, ["action"] = action, ["bindingDigest"] = binding, ["caseId"] = "P06", ["fixtureId"] = "fixture", ["targets"] = JsonSerializer.SerializeToNode(manifest.Targets) };
        async Task Register(JsonObject request)
        {
            await enrollment.HandleAsync(new() { ["operationId"] = "admit-" + request["operationId"]!.GetValue<string>(), ["action"] = "RegisterOriginalOperation", ["bindingDigest"] = binding,
                ["originalRole"] = "executor", ["originalOperationId"] = request["operationId"]!.DeepClone(), ["originalRequestSha256"] = ProtectionQualificationOperationJournal.Digest(request) }, token);
        }
        var malformed = Request("malformed", "DestroyWithLostAcknowledgement"); malformed["targets"]![0]!.AsObject().Remove("TargetProtectionKeyAlias");
        string beforeMalformed = ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a"));
        await Assert.ThrowsAnyAsync<Exception>(() => executor.HandleAsync(malformed, token));
        ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a")).ShouldBe(beforeMalformed);
        var original = Request("destroy", "DestroyWithLostAcknowledgement"); await Register(original);
        if (blockedReserve)
        {
            fixture.Permissions.DeniedOperations.Add("RetainReserveRefusalOriginal");
            string beforeMissingPermission = ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a"));
            await Should.ThrowAsync<InvalidOperationException>(() => executor.HandleAsync(original, token));
            ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a")).ShouldBe(beforeMissingPermission);
            fixture.Permissions.DeniedOperations.Clear();
        }
        if (lostTrimAck) { fixture.Store.Failure = "trim-after"; }
        await executor.HandleAsync(original, token);
        fixture.Store.TrimCount.ShouldBe(blockedReserve ? 0 : 1);
        var state = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        string key = ProtectionQualificationOperationJournal.Id("executor", "destroy");
        var admitted = state.Head.QualificationOperations[key];
        if (lostTrimAck || blockedReserve) { admitted.Calls["original-native-reserve"] = admitted.Calls["original-native-reserve"] with { Result = null }; }
        if (blockedReserve)
        {
            (await fixture.Actor.ReadReservationOriginalAsync(manifest)).ShouldBeNull();
            await fixture.RestoreOwnerAsync(staleOwner);
            (await driver.RecoverAsync(admitted.Calls["original-native-reserve"].Call with { Id = "admitted-but-undispatched" }, token)).ShouldBeNull();
        }
        state.Head.QualificationOperations[key] = admitted with { Result = null };
        (await fixture.Custody.SaveAsync("tenant-a", state.Version, state.Head, token)).ShouldBeTrue();
        if (lostTrimAck)
        {
            (await fixture.Store.HeadAsync("tenant-a")).Pending.ShouldNotBeNull();
            var recovery = Request("recover-fixture", recoverViaPrepare ? "PrepareFixture" : "LookupOriginalDestruction"); await Register(recovery);
            await executor.HandleAsync(recovery, token);
            (await fixture.Store.HeadAsync("tenant-a")).Pending.ShouldBeNull();
            (await fixture.Actor.LookupAsync("tenant-a", manifest.Capability.BatchId)).Status.ShouldBe(DeletionConsumptionStatus.Consumed);
        }
        var lookup = new JsonObject { ["operationId"] = "lookup", ["action"] = "LookupOriginalOperation", ["bindingDigest"] = binding,
            ["originalOperationId"] = "destroy", ["originalRequestSha256"] = ProtectionQualificationOperationJournal.Digest(original) };
        var result = await executor.HandleAsync(lookup, token);
        result["statement"]!["response"]!["Status"]!.GetValue<int>().ShouldBe((int)(blockedReserve ? DeletionConsumptionStatus.ConsumptionBlocked : DeletionConsumptionStatus.Consumed));
        fixture.Store.TrimCount.ShouldBe(blockedReserve ? 0 : 1);
        if (!lostTrimAck && !blockedReserve)
        {
            var historical = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
            var selected = historical.Head.QualificationOperations[key].Producer!;
            historical.Head.QualificationOperations.Add("executor:historical-same-case", new("executor", new string('b', 64), "historical-attempt", new string('c', 64),
                new JsonObject { ["caseId"] = "P06", ["fixtureId"] = "historical-fixture", ["action"] = "DestroyWithLostAcknowledgement" }, new JsonObject { ["status"] = "Completed" })
                { Producer = selected with { BindingDigest = new string('b', 64), EngineBinarySha256 = new string('f', 64) } });
            (await fixture.Custody.SaveAsync("tenant-a", historical.Version, historical.Head, token)).ShouldBeTrue();
            native.GetStateAsync<JsonElement>("installation", "proof", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(JsonSerializer.SerializeToElement(new { agentsRuntimeEnabled = false, storeIdentity = "erc-v23", deploymentIdentity = "component-observer", credentialScope = "ReadPersistedStateOnly" }));
            var observerScope = scope with { Role = "observer", Identity = "component-observer", Observations = scope.Observations.ToDictionary(entry => entry.Key, entry => entry.Value with { CaseId = null }) };
            var observedService = new ProtectionQualificationRoleService(observerScope, journal, null,
                new ProtectionQualificationPersistedObserver(native, fixture.Custody, engine.Engine, new HashSet<string>()), native, signer,
                observerPermission: _ => Task.FromResult<ProtectionQualificationObserverPermission?>(new("tenant-a", "erc-v23", "enrollment", "component", "component-observer", binding,
                    "native", "dapr", "role", "ReadPersistedStateOnly", [], [], [], [], DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1), "independently-verified-component-permission") { ComponentBackings = new() { ["source"] = "synthetic-native-source" } }));
            var observe = new JsonObject { ["operationId"] = "observe", ["action"] = "ObservePersistedState", ["bindingDigest"] = binding, ["caseId"] = "P06", ["fixtureId"] = "fixture", ["targets"] = JsonSerializer.SerializeToNode(manifest.Targets),
                ["expectedOperations"] = new JsonArray(new JsonObject { ["operationId"] = "destroy", ["requestSha256"] = ProtectionQualificationOperationJournal.Digest(original), ["action"] = "DestroyWithLostAcknowledgement" }) };
            (await observedService.HandleAsync(observe, token))["statement"]!["response"]!["engineBinarySha256"]!.GetValue<string>().ShouldBe(selected.EngineBinarySha256);
            observe["expectedOperations"]![0]!["operationId"] = "historical-same-case";
            await Should.ThrowAsync<InvalidOperationException>(() => observedService.HandleAsync(observe, token));
        }
        lookup["originalRequestSha256"] = new string('b', 64);
        await Should.ThrowAsync<InvalidOperationException>(() => executor.HandleAsync(lookup, token));
        fixture.Store.TrimCount.ShouldBe(blockedReserve ? 0 : 1);
    }
}
