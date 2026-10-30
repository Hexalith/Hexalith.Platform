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
    internal ErasureRootConsumptionFixture Consumption { get; }
    internal ErasureRootEngineFixture Engine { get; }
    internal Dictionary<string, JsonNode?> Originals { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, ProtectionQualificationNativeCall> Admitted { get; } = new(StringComparer.Ordinal);
    internal List<DeletionCapabilitySigningOutcome> Signed { get; } = [];
    internal ProtectionQualificationActionRunner Runner { get; }
    internal IDeletionCapabilitySigningActor Signer { get; } = Substitute.For<IDeletionCapabilitySigningActor>();
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly DaprClient _native = Substitute.For<DaprClient>();
    private ProtectionTarget[] _targets = [];
    internal DaprClient Native => _native;
    internal Dictionary<string, JsonElement> NativeState { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, ProtectionQualificationNativeCell> CopyCells { get; } = new(StringComparer.Ordinal);
    internal string PublicKeyPem => _key.ExportSubjectPublicKeyInfoPem();
    internal ProtectionTarget[] Targets => _targets;
    internal ProtectionQualificationPreparationFixture(bool? holdWins = null, IAtomicDeletionManifestProvider? provider = null, bool? activationWins = null)
    {
        Consumption = new(provider);
        Engine = new(Consumption);
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IDeletionConsumptionActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(Consumption.Actor);
        IDeletionConsumptionActor executionConsumption = Consumption.Actor;
        if (activationWins is { } activateFirst)
        {
            var scheduled = Substitute.For<IDeletionConsumptionActor>(); var winner = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            scheduled.RegisterAsync(Arg.Any<DeletionBatchConsumptionRequest>()).Returns(call => Consumption.Invoke(() => Consumption.Actor.RegisterAsync(call.Arg<DeletionBatchConsumptionRequest>())));
            scheduled.ReserveAndConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>()).Returns(call => Consumption.Invoke(() => Consumption.Actor.ReserveAndConsumeAsync(call.Arg<DeletionBatchConsumptionRequest>())));
            scheduled.LookupAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(call => Consumption.Invoke(() => Consumption.Actor.LookupAsync(call.ArgAt<string>(0), call.ArgAt<string>(1))));
            scheduled.RegisterRevocationAsync(Arg.Any<DeletionCapabilityRevocationEnvelope>()).Returns(async call =>
            {
                var envelope = call.Arg<DeletionCapabilityRevocationEnvelope>();
                if (envelope.KeyVersion == "healthy-key-b" && activateFirst) { await winner.Task.WaitAsync(TestContext.Current.CancellationToken); }
                var receipt = await Consumption.Invoke(() => Consumption.Actor.RegisterRevocationAsync(envelope));
                if (envelope.KeyVersion == "healthy-key-b" && !activateFirst) { winner.TrySetResult(); } return receipt;
            });
            scheduled.LookupRevocationAsync(Arg.Any<DeletionCapabilityRevocationEnvelope>()).Returns(call => Consumption.Invoke(() => Consumption.Actor.LookupRevocationAsync(call.Arg<DeletionCapabilityRevocationEnvelope>())));
            scheduled.ActivateAsync(Arg.Any<DeletionReattestationActivation>()).Returns(async call =>
            {
                if (!activateFirst) { await winner.Task.WaitAsync(TestContext.Current.CancellationToken); }
                var result = await Consumption.Invoke(() => Consumption.Actor.ActivateAsync(call.Arg<DeletionReattestationActivation>()));
                if (activateFirst) { winner.TrySetResult(); } return result;
            });
            proxies.CreateActorProxy<IDeletionConsumptionActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(scheduled);
            executionConsumption = scheduled;
        }
        var owner = new DaprDeletionProtectionOwner("tenant-a", proxies, TimeProvider.System);
        var verifier = new PublishedTrustDeletionConsumptionAuthority(new[] { "healthy-key-a", "healthy-key-b" }.Select(version => new PublishedDeletionCapabilityTrust("tenant-a", "DeletionBatchCapabilitySigningKey", version, "qualified-issuer", "protection-audience", "test-anchor", "1", _key.ExportSubjectPublicKeyInfo(), 1, false)).ToArray(), Guard.Owner);
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
        _native.GetStateAsync<JsonElement>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string>(1) == "guard" ? JsonSerializer.SerializeToElement(Guard.State) : NativeState.GetValueOrDefault(call.ArgAt<string>(1)));
        _native.GetStateAsync<ErasureRootWriterProof>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => NativeState.TryGetValue(call.ArgAt<string>(1), out var value) ? value.Deserialize<ErasureRootWriterProof>()! : null!);
        _native.GetStateAsync<ProtectionQualificationPersistedEvent>("source", Arg.Any<string>(), Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => NativeState[call.ArgAt<string>(1)].Deserialize<ProtectionQualificationPersistedEvent>()!);
        _native.SaveStateAsync("source", Arg.Any<string>(), Arg.Any<JsonElement>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => { NativeState[call.ArgAt<string>(1)] = call.ArgAt<JsonElement>(2); return Task.CompletedTask; });
        _native.ExecuteStateTransactionAsync("source", Arg.Any<IReadOnlyList<StateTransactionRequest>>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => { foreach (var operation in call.ArgAt<IReadOnlyList<StateTransactionRequest>>(1)) { NativeState[operation.Key] = JsonDocument.Parse(operation.Value).RootElement.Clone(); } return Task.CompletedTask; });
        var faults = new ProtectionQualificationFaults(_native, new("controls", "controls", new Dictionary<string, string>()), "component", new HashSet<string>());
        IGovernanceScopeGuard executionGuard = Guard.Owner;
        if (holdWins is { } holdFirst)
        {
            var ordered = Substitute.For<IGovernanceScopeGuard>(); var winner = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ordered.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => Guard.Owner.ReadAsync(call.Arg<string>(), call.Arg<CancellationToken>()));
            ordered.ExecuteAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>()).Returns(async call =>
            {
                var transition = call.Arg<GovernanceGuardTransition>();
                bool competing = transition.Operation is GovernanceGuardOperation.RegisterHold or GovernanceGuardOperation.CommitDispatch;
                bool winning = holdFirst ? transition.Operation == GovernanceGuardOperation.RegisterHold : transition.Operation == GovernanceGuardOperation.CommitDispatch;
                if (competing && !winning) { await winner.Task.WaitAsync(call.Arg<CancellationToken>()); }
                var receipt = await Guard.Owner.ExecuteAsync(transition, call.Arg<IReadOnlyList<GuardedStateMutation>>(), call.Arg<CancellationToken>());
                if (competing && winning) { winner.TrySetResult(); } return receipt;
            });
            executionGuard = ordered;
        }
        var authenticator = Substitute.For<IDeletionCapabilityRevocationAuthenticator>();
        var observedAt = DateTimeOffset.UtcNow.AddSeconds(-1); var validUntil = observedAt.AddMinutes(20);
        authenticator.VerifyAsync(Arg.Any<DeletionCapabilityRevocationEnvelope>(), "component-authenticated-revocation", Arg.Any<CancellationToken>())
            .Returns(call => new DeletionCapabilityRevocationAuthorization(call.Arg<DeletionCapabilityRevocationEnvelope>(), "component-current-authority", observedAt, validUntil));
        authenticator.VerifyBeforeEffectsAsync(Arg.Any<DeletionCapabilityRevocationEnvelope>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => authenticator.VerifyAsync(call.Arg<DeletionCapabilityRevocationEnvelope>(), call.Arg<string>(), call.Arg<CancellationToken>()));
        var subscriber = new DeletionCapabilityRevocationSubscriber(new("qualified-issuer", "registrar", "tenant-a"), authenticator,
            new DaprDeletionCapabilityCompromiseRegistrar(proxies), new EventStoreDeletionCapabilityGuardRevocationMirror(Guard.Owner), TimeProvider.System);
        var driver = new ProtectionQualificationNativeDriver(Engine.Engine, Consumption.Custody, Engine.Keys, Engine.Source, _native, executionGuard, owner,
            executionConsumption, Signer, subscriber, faults, new HashSet<string> { "source" }, Consumption.Permissions);
        Runner = new(driver);
    }
    internal async Task InitializeAsync(bool containment = false)
    {
        _targets = (await Consumption.SeedAsync()).Targets.ToArray();
        if (containment)
        {

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
        ["caseId"] = caseId, ["fixtureId"] = caseId + "-fixture", ["action"] = action, ["operationId"] = caseId + ":operation", ["targets"] = JsonSerializer.SerializeToNode(_targets),
    };
    internal Dictionary<string, Dictionary<string, ProtectionQualificationActionRecipe>> Recipes(string caseId)
    {
        var required = ProtectionQualificationSourceCatalog.RequiredArguments();
        var args = required.ToDictionary(pair => pair.Key, pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal)
            ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(pair.Value[6..]) }, ["targets"] = new JsonArray() }
            : new JsonObject(), StringComparer.Ordinal);
        string? signing = null, reserve = null, lastIssue = null, lastDispatch = null, firstCompromise = null; int compromises = 0; int attempt = 0, writes = 0, containmentOrdinal = 0; var acceptances = new Dictionary<int, string>();
        var controlTarget = new ProtectionTarget("tenant-a", "P13-fixture-accepted-control", "accepted-control");
        var containmentOriginals = new Dictionary<long, (GovernanceBatchCommand Batch, string Sign, string Issue, string Dispatch)>();
        var baseBatch = Guard.Batch(targets: (caseId == "P13" ? new[] { controlTarget } : _targets).Select(target => new GovernanceProtectionTarget(target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias)).ToArray());
        string deletionRequest = "deletion-a";
        var signedBatch = baseBatch;
        foreach (var entry in required.Where(entry => entry.Key.StartsWith(caseId + "/", StringComparison.Ordinal)))
        {
            string token = entry.Value;
            if (caseId == "P13" && entry.Key.StartsWith("P13/DestroyDekManifest/", StringComparison.Ordinal))
            {
                long ordinal = int.Parse(entry.Key.Split('/')[2], System.Globalization.CultureInfo.InvariantCulture) <= 5 ? 1 : 2;
                var original = containmentOriginals[ordinal]; baseBatch = signedBatch = original.Batch;
                signing = original.Sign; lastIssue = original.Issue; lastDispatch = original.Dispatch;
            }
            if (token == "WrapFixtureRoot")
            {
                args[entry.Key] = (JsonObject)JsonSerializer.SerializeToNode(new ProtectionQualificationNativeRoot(new(controlTarget.TenantId, controlTarget.AgentInteractionId,
                    PlatformKeyPurpose.InteractionRootDek, controlTarget.TargetProtectionKeyAlias, "root-v1", "lifecycle", "erc-v23", "custody"), "wrap-accepted-control", 0))!;
            }
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
                var target = _targets[caseId == "P13" ? containmentOrdinal - 1 : containmentOrdinal];
                if (caseId == "P13") { attempt = 0; }
                baseBatch = Guard.Batch("Containment", caseId == "P13" ? containmentOrdinal : 1, [new(target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias)]);
            }
            if (token is "Guard:RecordBatchIssued" or "Guard:ReplaceAttestation") { lastIssue = entry.Key; }
            if (token == "Guard:CommitDispatch") { lastDispatch = entry.Key; }
            if (token.StartsWith("Guard:", StringComparison.Ordinal) || token == "GuardOwnersEffective")
            {
                var operation = token == "GuardOwnersEffective" ? GovernanceGuardOperation.RecordOwnerCycleEffective : Enum.Parse<GovernanceGuardOperation>(token[6..]);
                var command = Guard.Command(operation, ordinal: operation is GovernanceGuardOperation.RecordOwnerCycleEffective or GovernanceGuardOperation.AuthorizeContentBinding or GovernanceGuardOperation.CommitContentBinding or GovernanceGuardOperation.RecordViolation ? Guard.Ordinal() : null, scope: operation is GovernanceGuardOperation.AuthorizeAdmissionFence or GovernanceGuardOperation.CommitAdmissionFence ? Guard.Scope : null,
                    batch: operation is GovernanceGuardOperation.AuthorizeDestructionStart or GovernanceGuardOperation.CommitDestructionStart or GovernanceGuardOperation.AuthorizeContainment or GovernanceGuardOperation.CommitContainment or GovernanceGuardOperation.RecordBatchIssued or GovernanceGuardOperation.ReplaceAttestation or GovernanceGuardOperation.AuthorizeDispatch or GovernanceGuardOperation.CommitDispatch or GovernanceGuardOperation.RecordProtectionOutcome ? (operation == GovernanceGuardOperation.RecordProtectionOutcome ? signedBatch : baseBatch) : null, authorization: operation is GovernanceGuardOperation.AuthorizeAdmissionFence or GovernanceGuardOperation.CommitAdmissionFence ? "admission" : operation is GovernanceGuardOperation.AuthorizeContentBinding or GovernanceGuardOperation.CommitContentBinding ? "content" : operation is GovernanceGuardOperation.AuthorizeDestructionStart or GovernanceGuardOperation.CommitDestructionStart ? "seal" : "dispatch-" + containmentOrdinal,
                    hold: operation is GovernanceGuardOperation.RegisterHold or GovernanceGuardOperation.ReleaseHold ? new("hold-a", Guard.Scope, "hold-policy", operation == GovernanceGuardOperation.ReleaseHold ? "independent-release" : "") : null);
                if (operation == GovernanceGuardOperation.AppendWrite)
                {
                    writes++; acceptances.Add(writes, entry.Key);
                    command = command with { Write = Guard.Facts(interaction: _targets[caseId == "P13" ? writes - 1 : writes].AgentInteractionId), ReferenceId = "resource-" + writes };
                }
                if (operation is GovernanceGuardOperation.AuthorizeContainment or GovernanceGuardOperation.CommitContainment)
                { command = command with { ReferenceId = "resource-" + containmentOrdinal, AuthorizationId = "containment-" + containmentOrdinal }; }
                command = command with { DeletionRequestId = deletionRequest, AuthorizationId = command.AuthorizationId + ":" + deletionRequest + (caseId is "P34" or "P29" or "P33" ? ":attestation-" + baseBatch.AttestationOrdinal : "") };
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
                    if (operation == GovernanceGuardOperation.RecordProtectionOutcome && !entry.Key.Contains("/ActivateReattestedDeletionBatch/", StringComparison.Ordinal))
                    {
                        batch["ProtectionOutcome"] = "Consumed"; batch["ProtectionReceiptId"] = Original(reserve!, "/ReceiptId");
                        batch["TargetReceiptIds"] = new JsonArray(signedBatch.Targets.Select((_, index) => (JsonNode)Original(reserve!, "/TargetReceipts/" + index + "/ReceiptId")).ToArray());
                    }
                }
                args[entry.Key] = new() { ["transition"] = node, ["targets"] = operation == GovernanceGuardOperation.AppendWrite
                    ? JsonSerializer.SerializeToNode(new[] { new GuardedStateMutation("source-" + writes, 0, GuardedTransactionFixture.Hash([]), "sealed-component-original"u8.ToArray()) }) : new JsonArray() };
            }
            else if (token == "Sign")
            {
                if (caseId is "P34" or "P29" or "P33" && compromises > 0) { baseBatch = baseBatch with { AttestationOrdinal = 2, CapabilityKeyVersion = "healthy-key-b", BlockSetRevision = 1 }; attempt = 0; }
                signing = entry.Key; signedBatch = baseBatch; attempt++;
                args[entry.Key] = (JsonObject)JsonSerializer.SerializeToNode(new DeletionBatchCapabilityV1("qualified-issuer", "protection-audience", "tenant-a", deletionRequest,
                    GovernanceScopeGuardReducer.DestructionSealId("tenant-a", deletionRequest, Guard.Scope), baseBatch.BatchKind, baseBatch.BatchOrdinal, baseBatch.BatchId, baseBatch.ManifestDigest,
                    "tenant-a-governance-guard", 1, baseBatch.AttestationOrdinal, attempt, baseBatch.CapabilityKeyVersion))!;
            }
            else if (token == "ObsoleteUnissued") { args[entry.Key] = Original(signing!, "/Payload"); }
            else if (token is "Register" or "RegisterIfDispatched" or "Reserve" or "ReserveIfDispatched")
            {
                string issueId = lastIssue!, dispatchId = lastDispatch!;
                args[entry.Key] = new() { ["Capability"] = Original(signing!, "/Payload"), ["DetachedJws"] = Original(signing!, "/DetachedJws"), ["CommittedIssuedGuardRevision"] = Original(issueId, "/GuardHighWater"),
                    ["DispatchReceiptId"] = Original(dispatchId, "/ReceiptId"), ["DispatchGuardRevision"] = Original(dispatchId, "/GuardHighWater"), ["Targets"] = JsonSerializer.SerializeToNode(signedBatch.Targets) };
                if (token == "Register" && caseId == "P13" && signedBatch.BatchKind == "Containment")
                { containmentOriginals[signedBatch.BatchOrdinal] = (signedBatch, signing!, issueId, dispatchId); }
                if (token == "Reserve") { reserve = entry.Key; }
            }
            else if (token == "Compromise")
            {
                compromises++; firstCompromise ??= entry.Key;
                string version = compromises == 1 ? "healthy-key-a" : "healthy-key-b";
                args[entry.Key] = new() { ["envelope"] = JsonSerializer.SerializeToNode(new DeletionCapabilityRevocationEnvelope("qualified-issuer", "registrar", "tenant-a", "DeletionBatchCapabilitySigningKey", version, 1, 1, "revocation-" + version, new string('A', 64))), ["signedEvidence"] = "component-authenticated-revocation" };
            }
            else if (token == "Activate")
            {
                var replacement = new JsonObject { ["Capability"] = Original(signing!, "/Payload"), ["DetachedJws"] = Original(signing!, "/DetachedJws"),
                    ["CommittedIssuedGuardRevision"] = Original(lastIssue!, "/GuardHighWater"), ["DispatchReceiptId"] = Original(lastDispatch!, "/ReceiptId"),
                    ["DispatchGuardRevision"] = Original(lastDispatch!, "/GuardHighWater"), ["Targets"] = JsonSerializer.SerializeToNode(signedBatch.Targets) };
                args[entry.Key] = new() { ["OperationId"] = "P34-activation", ["CompromiseBlockReceiptId"] = Codec("CompromiseBlockReceiptId", new JsonObject { ["receiptId"] = Original(firstCompromise!, "/receipt/ReceiptId"), ["batchId"] = baseBatch.BatchId }),
                    ["ExpectedKeyBlockSetRevision"] = Original(firstCompromise!, "/receipt/KeyBlockSetRevision"), ["GuardReplacementReceiptId"] = Original(lastIssue!, "/ReceiptId"), ["Replacement"] = replacement };
            }
            else if (token == "Lookup") { args[entry.Key] = new() { ["tenantId"] = "tenant-a", ["batchId"] = signedBatch.BatchId }; }
            else if (token == "LookupProvider") { args[entry.Key] = new() { ["request"] = args[reserve!].DeepClone(), ["reservationReceiptId"] = Original(reserve!, "/ReceiptId") }; }
        }
        return ProtectionQualificationSourceCatalog.Compile(args);
    }
    internal Task<JsonNode?> RunAsync(ProtectionQualificationActionRecipe recipe, string caseId, string action)
        => Runner.ExecuteAsync(recipe, Request(caseId, action), Originals, (call, _) => { Admitted.Add(call.Id, call); return Task.FromResult(true); },
            async (call, result, cancellation) =>
            {
                if (caseId is "P13" or "P14" or "P15" && call.Kind == ProtectionQualificationNativeCallKind.Guard)
                { result.ShouldNotBeNull(call.Id); result["Status"]!.GetValue<string>().ShouldBeOneOf(["Committed", "Accepted", "CoverageLinked"], call.Id); }
                Originals.Add(call.Id, result?.DeepClone());
                await new ProtectionQualificationOperationJournal(Consumption.Custody, Consumption.Permissions).UpdateAsync("tenant-a", call.Id,
                    "QualificationNativeResult", ErasureRootCustody.Hash(new { call, result }), head =>
                    {
                        string key = caseId + ":" + action;
                        if (!head.QualificationOperations.TryGetValue(key, out var operation))
                        { operation = new("executor", new string('a', 64), "component-attempt", ProtectionQualificationOperationJournal.Digest(Request(caseId, action)), Request(caseId, action)); head.QualificationOperations.Add(key, operation); }
                        operation.Calls.Add(call.Id, new(call, result?.DeepClone())); return true;
                    }, cancellation);
            }, TestContext.Current.CancellationToken);
    internal async Task SealExpectedSourcesAsync(string caseId)
    {
        string[] references = ["01ARZ3NDEKTSV4RRFFQ69G5FAV", "01ARZ3NDEKTSV4RRFFQ69G5FAW", "01ARZ3NDEKTSV4RRFFQ69G5FAX", "01ARZ3NDEKTSV4RRFFQ69G5FAY"];
        int index = 0; var token = TestContext.Current.CancellationToken;
        foreach (var target in _targets)
        {
            foreach (string kind in new[] { "source-outbox", "target-event" })
            {
                var seed = Engine.Enroll(kind == "source-outbox" ? "directory" : target.AgentInteractionId, references[index], (ulong)(index + 1)); index++;
                var identity = seed.Binding.Identity with { Target = target };
                var enrolled = seed with { Writer = new("source", "writer:" + references[index - 1], new()), Binding = seed.Binding with { Identity = identity }, RootIdentity = seed.RootIdentity with
                    { ObjectId = target.AgentInteractionId, KeyAlias = target.TargetProtectionKeyAlias, StoreTarget = "erc-v23", ContractVersion = "custody" }, RootOperationId = "wrap-" + target.AgentInteractionId };
                string id = ErasureRootOccurrenceBindings.Id(identity.Owner, ErasureRootOccurrenceBindings.Occurrence(identity)); Engine.Source.Enrollments[id] = enrolled;
                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = target, Value = "p13-canary" } });
                var write = await Engine.Engine.ProtectEventPayloadAsync(identity.Owner, new ProtectionQualificationEventPayload(), identity.PayloadTypeId, payload, "json", ErasureRootOccurrenceBindings.Occurrence(identity), token);
                await Engine.Engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, token);
                Engine.Source.Persisted.Add(enrolled.Binding.OperationId, write.ProtectionResult.PayloadBytes);
                await Engine.Engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token);
                await Engine.Keys.CompleteDisposedUsesAsync(token);
                string key = caseId + ":" + target.AgentInteractionId + ":" + kind;
                var head = (await Consumption.Custody.ReadAsync("tenant-a", token))!.Value; head.Head.SourceOriginals.Add(id, enrolled);
                var cell = new ProtectionQualificationNativeCell("source", key, new()); string location = ProtectionQualificationNativeCell.LocationId(cell);
                head.Head.QualificationProducerOriginals.Add(location, new(location, [enrolled.Binding], Convert.ToHexString(SHA256.HashData(write.ProtectionResult.PayloadBytes)),
                    ErasureRootCustody.Hash(write.CompletionContext), write.CompletionContext!, ProtectionQualificationProducerIdentity.Capture(Engine.Engine, Consumption.Custody.Installation)));
                NativeState[enrolled.Writer!.Key] = JsonSerializer.SerializeToElement(new ErasureRootWriterProof(enrolled.Binding, Convert.ToHexString(SHA256.HashData(write.ProtectionResult.PayloadBytes)), true, true, "actual-writer:" + key));
                (await Consumption.Custody.SaveAsync("tenant-a", head.Version, head.Head, token)).ShouldBeTrue();
                NativeState.Add(key, JsonSerializer.SerializeToElement(new ProtectionQualificationPersistedEvent(enrolled.Binding, write.ProtectionResult)));
                CopyCells.Add(key, new("source", key, new()) { CopyKind = kind });
            }
        }
    }
    internal Task<JsonObject> ObserveAsync(string caseId)
    {
        var sources = new Dictionary<string, ProtectionQualificationNativeCell>(CopyCells, StringComparer.Ordinal) { ["guard"] = new("source", "guard", new()) };
        return new ProtectionQualificationPersistedObserver(_native, Consumption.Custody, Engine.Engine, new HashSet<string> { "source" })
            .ObserveAsync(new("tenant-a", sources, new()) { CaseId = caseId }, _targets, TestContext.Current.CancellationToken);
    }
    internal ProtectionQualificationActionRecipe AddRequiredReplays(string caseId, string action, ProtectionQualificationActionRecipe recipe)
        => ProtectionQualificationSourceCatalog.WithRequiredReplays(caseId, action, recipe, new("tenant-a", CopyCells, new()) { CaseId = caseId });
    private static JsonObject Original(string id, string pointer) => new() { ["$original"] = new JsonObject { ["id"] = id, ["pointer"] = pointer } };
    private static JsonObject Codec(string name, JsonNode input) => new() { ["$codec"] = new JsonObject { ["name"] = name, ["input"] = input } };
    public void Dispose() { _key.Dispose(); _native.Dispose(); Engine.Dispose(); }
}
