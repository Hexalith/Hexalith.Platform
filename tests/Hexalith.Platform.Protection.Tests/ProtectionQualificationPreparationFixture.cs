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

/// <summary>Actual source catalog, guard CAS owner, public signature verifier and ERC consumption actor; native storage and private signer eligibility are synthetic component boundaries.</summary>
internal sealed class ProtectionQualificationPreparationFixture : IDisposable
{
    internal GovernanceGuardFixture Guard { get; } = new();
    internal ErasureRootConsumptionFixture Consumption { get; } = new();
    internal ErasureRootEngineFixture Engine { get; } = new();
    internal Dictionary<string, JsonNode?> Originals { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, ProtectionQualificationNativeCall> Admitted { get; } = new(StringComparer.Ordinal);
    internal List<DeletionCapabilitySigningOutcome> Signed { get; } = [];
    internal ProtectionQualificationActionRunner Runner { get; }
    internal IDeletionCapabilitySigningActor Signer { get; } = Substitute.For<IDeletionCapabilitySigningActor>();
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly DaprClient _native = Substitute.For<DaprClient>();
    private ProtectionTarget[] _targets = [];
    internal ProtectionQualificationPreparationFixture()
    {
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IDeletionConsumptionActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(Consumption.Actor);
        var owner = new DaprDeletionProtectionOwner("tenant-a", proxies, TimeProvider.System);
        var verifier = new PublishedTrustDeletionConsumptionAuthority([new("tenant-a", "DeletionBatchCapabilitySigningKey", "healthy-key-a", "qualified-issuer", "protection-audience", "test-anchor", "1", _key.ExportSubjectPublicKeyInfo(), 1, false)], Guard.Owner);
        Consumption.Authority.VerifyDispatchAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => verifier.VerifyDispatchAsync(call.Arg<DeletionBatchConsumptionRequest>(), call.Arg<CancellationToken>()));
        Signer.SignAsync(Arg.Any<DeletionBatchCapabilityV1>()).Returns(call =>
        {
            var payload = call.Arg<DeletionBatchCapabilityV1>();
            var signed = new DeletionCapabilitySigningOutcome(DeletionBatchCapabilityIdentity.SigningRequestId(payload), payload, DeletionCapabilitySigningState.Signed,
                DeletionBatchCapabilityCodec.Sign(payload, new(payload.Issuer, payload.Audience, payload.TenantId, payload.CapabilityKeyVersion, "test-anchor", "1"), _key), "test-anchor", "1");
            Signed.Add(signed); return signed;
        });
        Signer.ObsoleteUnissuedAsync(Arg.Any<DeletionBatchCapabilityV1>()).Returns(async call =>
        {
            var payload = call.Arg<DeletionBatchCapabilityV1>(); var signed = Signed.Single(value => value.Payload == payload);
            var proof = await new EventStoreDeletionCapabilityNoIssueAuthority(Guard.Owner).ReadAsync(payload, signed.SigningRequestId,
                DeletionBatchCapabilityIdentity.SignedArtifactDigest(signed.DetachedJws!), TestContext.Current.CancellationToken);
            proof.ShouldNotBeNull();
            return signed with { State = DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued, NoIssueProof = proof };
        });
        Guard.Authority.ReadSigningRecoveryAsync(Arg.Any<GovernanceProtocolReceipt>(), Arg.Any<TenantGovernanceGuardState>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var receipt = call.Arg<GovernanceProtocolReceipt>(); var state = call.Arg<TenantGovernanceGuardState>();
            return new GovernanceSigningRecovery(receipt.Capability!, receipt.SigningRequestId, receipt.DetachedJwsDigest, receipt.ReceiptId, state.Revision,
                "healthy-key-a", "synthetic-current-signing-authority", Guard.Backend.Now, Guard.Backend.Now.AddMinutes(1));
        });
        var faults = new ProtectionQualificationFaults(_native, new("controls", "controls", new Dictionary<string, string>()), "component", new HashSet<string>());
        var driver = new ProtectionQualificationNativeDriver(Engine.Engine, Consumption.Custody, Engine.Keys, Engine.Source, _native, Guard.Owner, owner,
            Consumption.Actor, Signer, null!, faults, new HashSet<string>(), Consumption.Permissions);
        Runner = new(driver);
    }
    internal async Task InitializeAsync(bool containment = false)
    {
        _targets = (await Consumption.SeedAsync()).Targets.ToArray();
        if (containment)
        {
            var target = new ProtectionTarget("tenant-a", "interaction-c", "alias-c");
            await Consumption.Custody.WrapAsync(new(target.TenantId, target.AgentInteractionId, PlatformKeyPurpose.InteractionRootDek, target.TargetProtectionKeyAlias,
                "root-v1", "lifecycle", "erc-v23", "custody"), "wrap-c", 0, TestContext.Current.CancellationToken);
            _targets = [.._targets, target];
            Guard.ChangeEvidence = (evidence, command) =>
            {
                if (command.Operation == GovernanceGuardOperation.AppendWrite) { return evidence with { AppendResourceId = command.ReferenceId }; }
                if (command.Operation != GovernanceGuardOperation.RecordViolation) { return evidence; }
                var original = Guard.State.Receipts.Single(receipt => receipt.AcceptedWriteResourceId == command.Ordinal!.ResourceId);
                var key = _targets.Single(value => value.AgentInteractionId == original.AcceptedWriteFacts!.AgentInteractionId);
                return evidence with { OriginalAcceptance = original, ViolationAcceptanceOperationId = original.OperationId,
                    ViolationWriteFacts = original.AcceptedWriteFacts, ViolationTargetMutationDigest = original.AcceptedTargetMutationDigest,
                    ViolationProtectionTarget = new(key.TenantId, key.AgentInteractionId, key.TargetProtectionKeyAlias) };
            };
        }
    }
    internal JsonObject Request(string caseId, string action) => new()
    {
        ["caseId"] = caseId, ["action"] = action, ["operationId"] = caseId + ":operation", ["targets"] = JsonSerializer.SerializeToNode(_targets),
    };
    internal Dictionary<string, Dictionary<string, ProtectionQualificationActionRecipe>> Recipes(string caseId)
    {
        var required = ProtectionQualificationSourceCatalog.RequiredArguments();
        var args = required.ToDictionary(pair => pair.Key, pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal)
            ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(pair.Value[6..]) }, ["targets"] = new JsonArray() }
            : new JsonObject(), StringComparer.Ordinal);
        string? signing = null, reserve = null, lastIssue = null, lastDispatch = null; int attempt = 0, writes = 0, containmentOrdinal = 0; var acceptances = new Dictionary<int, string>();
        var baseBatch = Guard.Batch(targets: (caseId == "P13" ? _targets[..1] : _targets).Select(target => new GovernanceProtectionTarget(target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias)).ToArray());
        string deletionRequest = "deletion-a";
        var signedBatch = baseBatch;
        foreach (var entry in required.Where(entry => entry.Key.StartsWith(caseId + "/", StringComparison.Ordinal)))
        {
            string token = entry.Value;
            if (caseId == "P15" && entry.Key == "P15/LookupAlreadyDestroyed/1")
            {
                deletionRequest = "deletion-later"; attempt = 0;
                string seal = GovernanceScopeGuardReducer.DestructionSealId("tenant-a", deletionRequest, Guard.Scope);
                baseBatch = Guard.Batch(targets: _targets.Select(target => new GovernanceProtectionTarget(target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias)).ToArray());
                baseBatch = baseBatch with { BatchId = GovernanceScopeGuardReducer.BatchId(deletionRequest, seal, baseBatch) };
            }
            if (caseId is "P13" or "P14" or "P15" && token == "Guard:RecordViolation")
            {
                containmentOrdinal++;
                var target = _targets[containmentOrdinal];
                baseBatch = Guard.Batch("Containment", caseId == "P13" ? containmentOrdinal : 1, [new(target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias)]);
            }
            if (token == "Guard:RecordBatchIssued") { lastIssue = entry.Key; }
            if (token == "Guard:CommitDispatch") { lastDispatch = entry.Key; }
            if (token.StartsWith("Guard:", StringComparison.Ordinal) || token == "GuardOwnersEffective")
            {
                var operation = token == "GuardOwnersEffective" ? GovernanceGuardOperation.RecordOwnerCycleEffective : Enum.Parse<GovernanceGuardOperation>(token[6..]);
                var command = Guard.Command(operation, ordinal: operation is GovernanceGuardOperation.RecordOwnerCycleEffective or GovernanceGuardOperation.AuthorizeContentBinding or GovernanceGuardOperation.CommitContentBinding or GovernanceGuardOperation.RecordViolation ? Guard.Ordinal() : null, scope: operation is GovernanceGuardOperation.AuthorizeAdmissionFence or GovernanceGuardOperation.CommitAdmissionFence ? Guard.Scope : null,
                    batch: operation is GovernanceGuardOperation.AuthorizeDestructionStart or GovernanceGuardOperation.CommitDestructionStart or GovernanceGuardOperation.AuthorizeContainment or GovernanceGuardOperation.CommitContainment or GovernanceGuardOperation.RecordBatchIssued or GovernanceGuardOperation.AuthorizeDispatch or GovernanceGuardOperation.CommitDispatch or GovernanceGuardOperation.RecordProtectionOutcome ? (operation == GovernanceGuardOperation.RecordProtectionOutcome ? signedBatch : baseBatch) : null, authorization: operation is GovernanceGuardOperation.AuthorizeAdmissionFence or GovernanceGuardOperation.CommitAdmissionFence ? "admission" : operation is GovernanceGuardOperation.AuthorizeContentBinding or GovernanceGuardOperation.CommitContentBinding ? "content" : operation is GovernanceGuardOperation.AuthorizeDestructionStart or GovernanceGuardOperation.CommitDestructionStart ? "seal" : "dispatch-" + containmentOrdinal,
                    hold: operation is GovernanceGuardOperation.RegisterHold or GovernanceGuardOperation.ReleaseHold ? new("hold-a", Guard.Scope, "hold-policy", operation == GovernanceGuardOperation.ReleaseHold ? "independent-release" : "") : null);
                if (operation == GovernanceGuardOperation.AppendWrite)
                {
                    writes++; acceptances.Add(writes, entry.Key);
                    command = command with { Write = Guard.Facts(interaction: _targets[writes].AgentInteractionId), ReferenceId = "resource-" + writes };
                }
                if (operation is GovernanceGuardOperation.AuthorizeContainment or GovernanceGuardOperation.CommitContainment)
                { command = command with { ReferenceId = "resource-" + containmentOrdinal, AuthorizationId = "containment-" + containmentOrdinal }; }
                command = command with { DeletionRequestId = deletionRequest, AuthorizationId = command.AuthorizationId + ":" + deletionRequest };
                var node = (JsonObject)JsonSerializer.SerializeToNode(command)!;
                if (operation == GovernanceGuardOperation.RecordViolation)
                {
                    var ordinal = (JsonObject)node["Ordinal"]!; ordinal["ResourceId"] = "resource-" + containmentOrdinal; ordinal["ViolationId"] = "violation-" + containmentOrdinal;
                    ordinal["AcceptedAtGuardHighWater"] = Original(acceptances[containmentOrdinal], "/GuardHighWater"); ordinal["AcceptedAtOrdinal"] = Original(acceptances[containmentOrdinal], "/AcceptedAtAdmissionFenceOrdinal");
                }
                if (signing is not null && node["Batch"] is JsonObject && operation is not (GovernanceGuardOperation.RegisterHold or GovernanceGuardOperation.ReleaseHold))
                {
                    var batch = (JsonObject)node["Batch"]!;
                    batch["Capability"] = Original(signing, "/Payload"); batch["DetachedJws"] = Original(signing, "/DetachedJws");
                    batch["SigningRequestId"] = Original(signing, "/SigningRequestId");
                    batch["AttestationDigest"] = Codec("SignedArtifactDigest", Original(signing, "/DetachedJws"));
                    if (operation == GovernanceGuardOperation.RecordProtectionOutcome)
                    {
                        batch["ProtectionOutcome"] = "Consumed"; batch["ProtectionReceiptId"] = Original(reserve!, "/ReceiptId");
                        batch["TargetReceiptIds"] = new JsonArray(_targets.Select((_, index) => (JsonNode)Original(reserve!, "/TargetReceipts/" + index + "/ReceiptId")).ToArray());
                    }
                }
                args[entry.Key] = new() { ["transition"] = node, ["targets"] = operation == GovernanceGuardOperation.AppendWrite
                    ? JsonSerializer.SerializeToNode(new[] { new GuardedStateMutation("source-" + writes, 0, GuardedTransactionFixture.Hash([]), "sealed-component-original"u8.ToArray()) }) : new JsonArray() };
            }
            else if (token == "Sign")
            {
                signing = entry.Key; signedBatch = baseBatch; attempt++;
                args[entry.Key] = (JsonObject)JsonSerializer.SerializeToNode(new DeletionBatchCapabilityV1("qualified-issuer", "protection-audience", "tenant-a", deletionRequest,
                    GovernanceScopeGuardReducer.DestructionSealId("tenant-a", deletionRequest, Guard.Scope), baseBatch.BatchKind, baseBatch.BatchOrdinal, baseBatch.BatchId, baseBatch.ManifestDigest,
                    "tenant-a-governance-guard", 1, 1, attempt, "healthy-key-a"))!;
            }
            else if (token == "ObsoleteUnissued") { args[entry.Key] = Original(signing!, "/Payload"); }
            else if (token is "Register" or "RegisterIfDispatched" or "Reserve" or "ReserveIfDispatched")
            {
                string issueId = lastIssue!, dispatchId = lastDispatch!;
                args[entry.Key] = new() { ["Capability"] = Original(signing!, "/Payload"), ["DetachedJws"] = Original(signing!, "/DetachedJws"), ["CommittedIssuedGuardRevision"] = Original(issueId, "/GuardHighWater"),
                    ["DispatchReceiptId"] = Original(dispatchId, "/ReceiptId"), ["DispatchGuardRevision"] = Original(dispatchId, "/GuardHighWater"), ["Targets"] = JsonSerializer.SerializeToNode(signedBatch.Targets) };
                if (token == "Reserve") { reserve = entry.Key; }
            }
            else if (token == "Lookup") { args[entry.Key] = new() { ["tenantId"] = "tenant-a", ["batchId"] = signedBatch.BatchId }; }
            else if (token == "LookupProvider") { args[entry.Key] = new() { ["request"] = args[reserve!].DeepClone(), ["reservationReceiptId"] = Original(reserve!, "/ReceiptId") }; }
        }
        return ProtectionQualificationSourceCatalog.Compile(args);
    }
    internal Task<JsonNode?> RunAsync(ProtectionQualificationActionRecipe recipe, string caseId, string action)
        => Runner.ExecuteAsync(recipe, Request(caseId, action), Originals, (call, _) => { Admitted.Add(call.Id, call); return Task.FromResult(true); },
            (call, result, _) =>
            {
                if (caseId is "P13" or "P14" or "P15" && call.Kind == ProtectionQualificationNativeCallKind.Guard)
                { result.ShouldNotBeNull(call.Id); result["Status"]!.GetValue<string>().ShouldBeOneOf(["Committed", "Accepted", "CoverageLinked"], call.Id); }
                Originals.Add(call.Id, result?.DeepClone()); return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);
    private static JsonObject Original(string id, string pointer) => new() { ["$original"] = new JsonObject { ["id"] = id, ["pointer"] = pointer } };
    private static JsonObject Codec(string name, JsonNode input) => new() { ["$codec"] = new JsonObject { ["name"] = name, ["input"] = input } };
    public void Dispose() { _key.Dispose(); _native.Dispose(); Engine.Dispose(); }
}
