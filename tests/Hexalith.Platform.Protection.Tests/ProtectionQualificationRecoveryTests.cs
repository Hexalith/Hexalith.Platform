using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Tests.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Exact driver original recovery uses native retained bytes/owner receipts and never redispatches missing work.</summary>
public sealed class ProtectionQualificationRecoveryTests
{
    [Fact]
    public async Task BackupAndRestoreExecutionAndRecoveryShareExactReceiptIdentityWithoutAnotherWrite()
    {
        var token = TestContext.Current.CancellationToken; using var native = Substitute.For<DaprClient>(); using var engine = new ErasureRootEngineFixture();
        var state = new Dictionary<string, JsonElement> { ["cell"] = JsonSerializer.SerializeToElement(new { sealedPayload = "opaque" }) };
        native.GetStateAsync<JsonElement>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => state.GetValueOrDefault(call.ArgAt<string>(1)));
        native.SaveStateAsync("source", Arg.Any<string>(), Arg.Any<JsonElement>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => { state[call.ArgAt<string>(1)] = call.ArgAt<JsonElement>(2); return Task.CompletedTask; });
        native.ExecuteStateTransactionAsync("source", Arg.Any<IReadOnlyList<StateTransactionRequest>>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => { foreach (var operation in call.ArgAt<IReadOnlyList<StateTransactionRequest>>(1)) { state[operation.Key] = JsonDocument.Parse(operation.Value).RootElement.Clone(); } return Task.CompletedTask; });
        var faults = new ProtectionQualificationFaults(native, new("controls", "controls", new()), "component", new HashSet<string>());
        var driver = new ProtectionQualificationNativeDriver(null!, null!, engine.Keys, null!, native, null!, null!, null!, null!, null!, faults, new HashSet<string> { "source" }, null!);
        var request = new JsonObject { ["action"] = "CreateBackup", ["targets"] = new JsonArray(new JsonObject { ["TenantId"] = "tenant-a" }) };
        foreach (var kind in new[] { ProtectionQualificationNativeCallKind.Backup, ProtectionQualificationNativeCallKind.Restore })
        {
            var call = new ProtectionQualificationNativeCall("copy:" + kind, kind, (JsonObject)JsonSerializer.SerializeToNode(new ProtectionQualificationNativeCell("source", "cell", new()))!);
            call = (await driver.PrepareCallAsync(call, token)) with { OperationId = "copy-operation:" + kind, RequestSha256 = ProtectionQualificationOperationJournal.Digest(request) };
            var executed = await driver.ExecuteAsync(call, request, token); native.ClearReceivedCalls();
            var recovered = await driver.RecoverAsync(call, token);
            recovered!["receiptId"]!.GetValue<string>().ShouldBe(executed!["receiptId"]!.GetValue<string>());
            var unsubmitted = (await driver.PrepareCallAsync(call with { Id = "new-admitted-undispatched", OperationId = "new-operation" }, token));
            (await driver.RecoverAsync(unsubmitted, token)).ShouldBeNull();
            (await driver.RecoverAsync(call with { RequestSha256 = new string('A', 64) }, token)).ShouldBeNull();
            native.ReceivedCalls().ShouldAllBe(value => value.GetMethodInfo().Name == "GetStateAsync");
        }
    }

    [Fact]
    public async Task ConditionalOwnerCallsRecoverAcknowledgedRegistrationAndConsumedOriginals()
    {
        var token = TestContext.Current.CancellationToken; var fixture = new ErasureRootConsumptionFixture();
        var manifest = await fixture.SeedAsync(); var registered = await fixture.Actor.RegisterAsync(manifest);
        var faults = new ProtectionQualificationFaults(null!, new("controls", "controls", new()), "component", new HashSet<string>());
        var driver = new ProtectionQualificationNativeDriver(null!, fixture.Custody, null!, null!, null!, null!, null!, fixture.Actor, null!, null!, faults, new HashSet<string>(), fixture.Permissions);
        var registration = new ProtectionQualificationNativeCall("registered", ProtectionQualificationNativeCallKind.RegisterIfDispatched, (JsonObject)JsonSerializer.SerializeToNode(manifest)!);
        var recoveredRegistration = await driver.RecoverAsync(registration, token);
        recoveredRegistration!["ReceiptId"]!.GetValue<string>().ShouldBe(registered.ReceiptId);
        var consumed = await fixture.Actor.ReserveAndConsumeAsync(manifest);
        var reservation = registration with { Id = "reserved", Kind = ProtectionQualificationNativeCallKind.ReserveIfDispatched };
        var recovered = await driver.RecoverAsync(reservation, token);
        recovered!["ReceiptId"]!.GetValue<string>().ShouldBe(consumed.ReceiptId);
        recovered["Status"]!.GetValue<int>().ShouldBe((int)DeletionConsumptionStatus.Consumed);
        fixture.Store.TrimCount.ShouldBe(1);
    }

    [Fact]
    public async Task GenuineRefusedGuardAttemptRecoversItsExactRetainedReceivingOwnerOriginal()
    {
        var token = TestContext.Current.CancellationToken; var guard = new GovernanceGuardFixture(); var fixture = new ErasureRootConsumptionFixture();
        var command = guard.Command(GovernanceGuardOperation.RegisterHold, hold: new("hold", guard.Scope, "restrictive-policy", ""));
        var winner = await guard.Apply(guard.Command(GovernanceGuardOperation.RegisterHold, hold: new("winner", guard.Scope, "restrictive-policy", "")));
        winner.Status.ShouldBe("Committed");
        var refused = await guard.Apply(command); refused.Status.ShouldBe("Stale");
        guard.State.Receipts.ShouldNotContain(value => value.OperationId == command.OperationId);
        var head = (await fixture.Custody.ReadAsync("tenant-a", token))!.Value;
        head.Head.GuardAttempts.Add(refused.IntentDigest, new(command, ErasureRootCustody.Hash(Array.Empty<GuardedStateMutation>()), command.ExpectedGuardRevision, refused.GuardHighWater, refused));
        (await fixture.Custody.SaveAsync("tenant-a", head.Version, head.Head, token)).ShouldBeTrue();
        var faults = new ProtectionQualificationFaults(null!, new("controls", "controls", new()), "component", new HashSet<string>());
        var driver = new ProtectionQualificationNativeDriver(null!, fixture.Custody, null!, null!, null!, guard.Owner, null!, fixture.Actor, null!, null!, faults, new HashSet<string>(), fixture.Permissions);
        var call = new ProtectionQualificationNativeCall("refused", ProtectionQualificationNativeCallKind.Guard,
            new JsonObject { ["transition"] = JsonSerializer.SerializeToNode(command), ["targets"] = new JsonArray() });
        var recovered = await driver.RecoverAsync(call, token);
        recovered!["ReceiptId"]!.GetValue<string>().ShouldBe(refused.ReceiptId); recovered["Status"]!.GetValue<string>().ShouldBe("Stale");
        call.Payload["transition"]!["OperationId"] = "substituted";
        (await driver.RecoverAsync(call, token)).ShouldBeNull();
    }
}
