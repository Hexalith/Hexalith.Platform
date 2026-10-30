using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Actors.Client;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Server.Tests.Security;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual role/journal loss at both retention boundaries; no synthetic successful native result is supplied.</summary>
public sealed class ErasureRootLoop5RecoveryTests
{
    [Theory]
    [InlineData(ProtectionQualificationNativeCallKind.Lookup, false)]
    [InlineData(ProtectionQualificationNativeCallKind.Lookup, true)]
    [InlineData(ProtectionQualificationNativeCallKind.LookupProvider, false)]
    [InlineData(ProtectionQualificationNativeCallKind.LookupProvider, true)]
    [InlineData(ProtectionQualificationNativeCallKind.AuthorizeLifecycle, false)]
    [InlineData(ProtectionQualificationNativeCallKind.AuthorizeLifecycle, true)]
    [InlineData(ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch, false)]
    [InlineData(ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch, true)]
    [InlineData(ProtectionQualificationNativeCallKind.Fault, false)]
    [InlineData(ProtectionQualificationNativeCallKind.Fault, true)]
    public async Task LostExactResultRecoversOnlyStoredOriginalAndUnretainedReadsRemainUnknown(ProtectionQualificationNativeCallKind kind, bool beforeNativeRetention)
    {
        var token = TestContext.Current.CancellationToken; var fixture = new ErasureRootConsumptionFixture(); var manifest = await fixture.SeedAsync();
        (await fixture.Actor.RegisterAsync(manifest)).Status.ShouldBe(DeletionConsumptionStatus.Unconsumed); object oldOwner = fixture.CaptureOwner();
        using var engine = new ErasureRootEngineFixture(fixture); using var native = Substitute.For<DaprClient>(); using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        native.GetStateAsync<JsonElement>("installation", "proof", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.SerializeToElement(new { agentsRuntimeEnabled = false, storeIdentity = "erc-v23", deploymentIdentity = "recovery-component" }));
        var controlsState = new ProtectionQualificationFaultState("controls-enrollment", new(), new()); int controlWrites = 0;
        native.GetStateAsync<ProtectionQualificationFaultState>("controls", "cell", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(_ => controlsState);
        native.GetStateAndETagAsync<ProtectionQualificationFaultState>("controls", "cell", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(_ => (controlsState, "tag"));
        native.TrySaveStateAsync("controls", "cell", Arg.Any<ProtectionQualificationFaultState>(), "tag", Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => { controlsState = call.Arg<ProtectionQualificationFaultState>(); controlWrites++; return true; });
        var faults = new ProtectionQualificationFaults(native, new("controls", "cell", new()), "controls-enrollment", new HashSet<string> { "later" });
        var proxies = Substitute.For<IActorProxyFactory>(); proxies.CreateActorProxy<IDeletionConsumptionActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(fixture.Actor);
        var owner = new DaprDeletionProtectionOwner("tenant-a", proxies, TimeProvider.System); var guard = new GovernanceGuardFixture();
        bool lifecycleCurrent = true;
        fixture.Permissions.LifecycleEvidenceReader = input => lifecycleCurrent ? new(input.Identity.TenantId, input.OperationId, input.Action.ToString(), ErasureRootCustody.Hash(input), "independent-original-lifecycle", true) : null;
        JsonObject payload;
        switch (kind)
        {
            case ProtectionQualificationNativeCallKind.Lookup: payload = new() { ["tenantId"] = "tenant-a", ["batchId"] = manifest.Capability.BatchId }; break;
            case ProtectionQualificationNativeCallKind.LookupProvider:
                payload = new() { ["request"] = JsonSerializer.SerializeToNode(manifest), ["reservationReceiptId"] = "original-reservation" }; break;
            case ProtectionQualificationNativeCallKind.AuthorizeLifecycle:
                var root = (await fixture.Store.HeadAsync("tenant-a")).Targets.Values.First().Registration;
                payload = (JsonObject)JsonSerializer.SerializeToNode(new CustodyKeyLifecycleRequest(root.Identity, "lifecycle-original", CustodyKeyLifecycleAction.Pin, 1, "hold", "fence", 1, "decision"))!; break;
            case ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch:
                var hold = guard.Command(GovernanceGuardOperation.RegisterHold, hold: new("hold", guard.Scope, "policy", ""));
                (await guard.Owner.ExecuteAsync(hold, [], token))!.Status.ShouldBe("Committed");
                payload = new() { ["transition"] = JsonSerializer.SerializeToNode(hold with { OperationId = "continuation" }), ["targets"] = new JsonArray() }; break;
            default: payload = new() { ["stepId"] = "later", ["boundary"] = "after" }; break;
        }
        var driver = new ProtectionQualificationNativeDriver(engine.Engine, fixture.Custody, engine.Keys, engine.Source, native, guard.Owner, owner, fixture.Actor, null!, null!, faults, new HashSet<string>(), fixture.Permissions);
        var arguments = ProtectionQualificationSourceCatalog.RequiredArguments().ToDictionary(pair => pair.Key, pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal)
            ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(pair.Value[6..]) } } : new JsonObject());
        var recipes = ProtectionQualificationSourceCatalog.Compile(arguments); var call = new ProtectionQualificationNativeCall("exact-call", kind, payload);
        var effect = new ProtectionQualificationNativeCall("preceding-lookup", ProtectionQualificationNativeCallKind.Lookup, new() { ["tenantId"] = "tenant-a", ["batchId"] = manifest.Capability.BatchId });
        recipes["P06"]["LookupOriginalDestruction"] = kind == ProtectionQualificationNativeCallKind.Lookup ? new([], call, null, []) : new([], effect, null, [call]); string binding = new('a', 64);
        var scope = new ProtectionQualificationServiceScope("executor", "recovery-executor", "tenant-a", binding, new("installation", "proof", new()), recipes, recipes.Keys.ToDictionary(id => id, id => new ProtectionQualificationObservationRecipe("tenant-a", new(), new())));
        var journal = new ProtectionQualificationOperationJournal(fixture.Custody, fixture.Permissions); var role = new ProtectionQualificationRoleService(scope, journal, new(driver), null, native, signer);
        var request = new JsonObject { ["operationId"] = "original", ["action"] = "LookupOriginalDestruction", ["caseId"] = "P06", ["fixtureId"] = "fixture", ["bindingDigest"] = binding, ["targets"] = JsonSerializer.SerializeToNode(manifest.Targets) };
        var state = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        state.Head.QualificationOperations.Add("executor:original", new("executor", binding, "attempt", ProtectionQualificationOperationJournal.Digest(request), (JsonObject)request.DeepClone()));
        (await fixture.Custody.SaveAsync("tenant-a", state.Version, state.Head, token)).ShouldBeTrue();
        // Admit/execute actual calls through the same journal callbacks as the runner. The secondary
        // call is an After operation; the preceding effect is already durably complete.
        if (kind != ProtectionQualificationNativeCallKind.Lookup)
        {
            effect = effect with { OperationId = "original", RequestSha256 = ProtectionQualificationOperationJournal.Digest(request) };
            (await journal.AdmitCallAsync("tenant-a", "executor", "original", effect, token)).ShouldBeTrue();
            await journal.RetainCallAsync("tenant-a", "executor", "original", effect, await driver.ExecuteAsync(effect, request, token), token);
        }
        if (kind == ProtectionQualificationNativeCallKind.LookupProvider)
        {
            fixture.Store.Failure = "trim-after";
            (await fixture.Custody.ConsumeAsync(manifest, "original-reservation", token)).State.ShouldBe(DeletionManifestProviderState.Unknown);
            (await fixture.Store.HeadAsync("tenant-a")).Pending.ShouldNotBeNull();
        }
        call = call with { OperationId = "original", RequestSha256 = ProtectionQualificationOperationJournal.Digest(request) };
        (await journal.AdmitCallAsync("tenant-a", "executor", "original", call, token)).ShouldBeTrue();
        fixture.Permissions.DeniedOperations.Add(beforeNativeRetention ? "RetainNativeCallOriginal" : "QualificationNativeResult");
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        { var result = await driver.ExecuteAsync(call, request, token); await journal.RetainCallAsync("tenant-a", "executor", "original", call, result, token); });
        fixture.Permissions.DeniedOperations.Clear();
        var admitted = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value.Head.QualificationOperations["executor:original"];
        admitted.Result.ShouldBeNull(); admitted.Calls[call.Id].Result.ShouldBeNull(); var exact = admitted.Calls[call.Id].Call;
        var expected = await driver.RecoverAsync(exact, token);
        bool hasAtomicOriginal = !beforeNativeRetention || kind is ProtectionQualificationNativeCallKind.Fault or ProtectionQualificationNativeCallKind.LookupProvider;
        (expected is not null).ShouldBe(hasAtomicOriginal);
        if (kind == ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch && expected is not null) { expected["status"]!.GetValue<string>().ShouldBe("AlreadyCommitted"); }
        lifecycleCurrent = false;
        if (kind == ProtectionQualificationNativeCallKind.AuthorizeLifecycle && expected is not null)
        { expected["status"]!.GetValue<string>().ShouldBe("Authorized"); expected["receiptId"]!.GetValue<string>().ShouldBe("independent-original-lifecycle"); }
        await fixture.RestoreOwnerAsync(oldOwner); int rotates = fixture.Store.RotateCount, trims = fixture.Store.TrimCount, guards = guard.Backend.CommittedTransactions, controls = controlWrites;
        var lookup = new JsonObject { ["operationId"] = "lookup", ["action"] = "LookupOriginalOperation", ["bindingDigest"] = binding, ["originalOperationId"] = "original", ["originalRequestSha256"] = ProtectionQualificationOperationJournal.Digest(request) };
        if (hasAtomicOriginal)
        {
            var recovered = (await role.HandleAsync(lookup, token))["statement"]!["response"]!;
            var retainedCall = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value.Head.QualificationOperations["executor:original"].Calls[call.Id].Result;
            JsonNode.DeepEquals(retainedCall, expected).ShouldBeTrue(); recovered.ShouldNotBeNull();
            await role.HandleAsync(lookup, token);
        }
        else { await Should.ThrowAsync<InvalidOperationException>(() => role.HandleAsync(lookup, token)); }
        (await driver.RecoverAsync(exact with { RequestSha256 = new string('f', 64) }, token)).ShouldBeNull();
        (await driver.RecoverAsync(exact with { Id = "undispatched" }, token)).ShouldBeNull();
        fixture.Store.RotateCount.ShouldBe(rotates); fixture.Store.TrimCount.ShouldBe(trims); guard.Backend.CommittedTransactions.ShouldBe(guards); controlWrites.ShouldBe(controls);
        if (kind == ProtectionQualificationNativeCallKind.LookupProvider) { trims.ShouldBe(1); (await fixture.Store.HeadAsync("tenant-a")).Pending.ShouldBeNull(); }
        // A lost read remains restrictive, while a separately admitted next call can still progress.
        // Fault consumption is a real once-only control operation, never part of recovery lookup.
        if (kind == ProtectionQualificationNativeCallKind.Fault) { await Should.ThrowAsync<IOException>(() => faults.CheckAsync("later", "after", token)); }
        var nextRequest = (JsonObject)request.DeepClone(); nextRequest["operationId"] = "subsequent"; nextRequest["fixtureId"] = "subsequent-fixture";
        string nextDigest = ProtectionQualificationOperationJournal.Digest(nextRequest);
        await journal.UpdateAsync("tenant-a", "subsequent", "QualificationExecution", nextDigest, head =>
        { head.QualificationOperations.Add("executor:subsequent", new("executor", binding, "attempt", nextDigest, (JsonObject)nextRequest.DeepClone())); return true; }, token);
        var next = exact with { Id = "subsequent-call", OperationId = "subsequent", RequestSha256 = nextDigest };
        (await journal.AdmitCallAsync("tenant-a", "executor", "subsequent", next, token)).ShouldBeTrue();
        var nextResult = await driver.ExecuteAsync(next, nextRequest, token); nextResult.ShouldNotBeNull();
        await journal.RetainCallAsync("tenant-a", "executor", "subsequent", next, nextResult, token);
        JsonNode.DeepEquals(await driver.RecoverAsync(next, token), nextResult).ShouldBeTrue();
        if (kind == ProtectionQualificationNativeCallKind.AuthorizeLifecycle) { nextResult["status"]!.GetValue<string>().ShouldBe("Denied"); }
        fixture.Store.RotateCount.ShouldBe(rotates); fixture.Store.TrimCount.ShouldBe(trims); guard.Backend.CommittedTransactions.ShouldBe(guards);

    }
}
