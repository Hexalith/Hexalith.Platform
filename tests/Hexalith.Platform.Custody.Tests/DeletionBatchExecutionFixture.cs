using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using NSubstitute;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual cryptographic signature and typed source reducer behind simulated independent authorities/protection outcomes; real target/backend/live qualification is absent.</summary>
internal sealed class DeletionBatchExecutionFixture : IDisposable
{
    internal IGovernanceScopeGuard Source { get; } = Substitute.For<IGovernanceScopeGuard>();
    internal IDeletionCapabilitySigningActor Signer { get; } = Substitute.For<IDeletionCapabilitySigningActor>();
    internal IDeletionProtectionOwner Protection { get; } = Substitute.For<IDeletionProtectionOwner>();
    internal ECDsa Key { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    internal TenantGovernanceGuardState State { get; private set; }
    internal DeletionBatchCapabilityV1 Payload { get; }
    internal DeletionBatchExecutionCoordinator Coordinator { get; }
    internal int Signatures { get; private set; }
    internal int Reservations { get; private set; }
    internal int SourceMutations { get; private set; }
    internal bool LoseConsumptionAcknowledgement { get; set; }
    internal bool UnknownProtectionLookup { get; set; }
    internal bool PartialProtectionResult { get; set; }
    internal bool StaleIssue { get; set; }
    internal bool BlockDispatchWithHold { get; set; }
    internal bool NoIssueProofUnavailable { get; set; }
    internal bool LoseIssueAcknowledgement { get; set; }
    internal DeletionConsumptionOutcome? Consumed { get; private set; }
    private readonly Dictionary<string, DeletionCapabilitySigningOutcome> _signatures = [];
    private readonly Dictionary<string, GovernanceProtocolReceipt> _results = [];
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    internal DeletionBatchExecutionFixture()
    {
        var targets = new[] { new GovernanceProtectionTarget("tenant-a", "interaction-a", "alias-a"), new GovernanceProtectionTarget("tenant-a", "interaction-b", "alias-b") };
        var batch = new GovernanceBatchState("batch-a", "AcceptedSet", 0, targets, GovernanceScopeGuardReducer.ManifestDigest(targets), 0, "", "", "", "", "AwaitingAttestation", "", [], 0, []);
        var scope = new GovernanceScopeV1("tenant-a", "SourceDeletionExactConversation", "", "conversation-a");
        var obligations = new[] { "original-obligation" };
        var deletion = new GovernanceDeletionState("deletion-a", scope, GovernanceScopeGuardReducer.PredicateDigest(scope), 1, ["owner-a"], obligations,
            [new(1, "owner-a", GovernanceScopeGuardReducer.ObligationDigest(obligations), "effective-a")], [], [new(1, "cut-a", "token-a", "binding-a", "")], "seal-a", [batch], false, false);
        State = new("tenant-a", "installed-source", 10, "current-epoch", "legacy-revoked", "all-writers-installed", null, [], [deletion], [], [], [], []);
        Payload = new("issuer-a", "protection-a", "tenant-a", "deletion-a", "seal-a", "AcceptedSet", 0, "batch-a", batch.ManifestDigest, "guard-a", 10, 1, 1, "key-a");
        Source.ReadAsync("tenant-a", Arg.Any<CancellationToken>()).Returns(_ => Copy(State));
        Source.ExecuteAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var command = call.Arg<GovernanceGuardTransition>();
            if (_results.TryGetValue(command.OperationId, out var original)) { return Copy(original); }
            if (command.Batch?.Capability is { } capability && command.Batch.DetachedJws != "" && !DeletionBatchCapabilityCodec.Verify(capability,
                new(capability.Issuer, capability.Audience, capability.TenantId, capability.CapabilityKeyVersion, "public-anchor", "v1"), command.Batch.DetachedJws, Key)) { return null; }
            if (StaleIssue && command.Operation == GovernanceGuardOperation.RecordBatchIssued) { State = State with { Revision = State.Revision + 1 }; StaleIssue = false; }
            if (BlockDispatchWithHold && command.Operation == GovernanceGuardOperation.AuthorizeDispatch)
            { State = State with { Holds = [new("post-start-hold", State.Deletions.Single().Scope, "open-policy", true, true, "")] }; BlockDispatchWithHold = false; }
            var proof = new GovernanceGuardEvidence("tenant-a", Hash(command), Hash(Array.Empty<GuardedStateMutation>()), "independent-exact-authority",
                command.Batch?.ProtectionReceiptId is { Length: > 0 } receipt ? receipt : "authority-receipt", _now, _now.AddMinutes(2), ["owner-a"], obligations, [], "all-writers-installed", "legacy-revoked",
                "current-zero", 1, "Open", "", State.Deletions.Single().Batches.Where(value => value.ProtectionReceiptId != "").Select(value => value.ProtectionReceiptId).ToArray(), "block-receipt")
                { CapabilityIssuer = "issuer-a", CapabilityAudience = "protection-a", GuardStreamId = "guard-a" };
            var reduction = GovernanceScopeGuardReducer.Reduce(State, command, proof, proof.IntentDigest);
            if (reduction.MutatesGuard) { State = reduction.State; SourceMutations++; }
            _results[command.OperationId] = reduction.Receipt;
            if (LoseIssueAcknowledgement && command.Operation == GovernanceGuardOperation.RecordBatchIssued) { LoseIssueAcknowledgement = false; throw new IOException("Controlled persisted issue response loss."); }
            return Copy(reduction.Receipt);
        });
        Signer.SignAsync(Arg.Any<DeletionBatchCapabilityV1>()).Returns(call =>
        {
            var payload = call.Arg<DeletionBatchCapabilityV1>(); string id = DeletionBatchCapabilityIdentity.SigningRequestId(payload);
            if (_signatures.TryGetValue(id, out var retained)) { return retained; }
            Signatures++; string jws = DeletionBatchCapabilityCodec.Sign(payload, new(payload.Issuer, payload.Audience, payload.TenantId, payload.CapabilityKeyVersion, "public-anchor", "v1"), Key);
            return _signatures[id] = new(id, payload, DeletionCapabilitySigningState.Signed, jws, "public-anchor", "v1");
        });
        Signer.ObsoleteUnissuedAsync(Arg.Any<DeletionBatchCapabilityV1>()).Returns(call =>
        {
            var payload = call.Arg<DeletionBatchCapabilityV1>(); string id = DeletionBatchCapabilityIdentity.SigningRequestId(payload); var signed = _signatures[id];
            var original = _results.Values.Single(value => value.Capability == payload && value.Status == "Stale");
            if (NoIssueProofUnavailable) { return signed; }
            return _signatures[id] = signed with { State = DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued,
                NoIssueProof = new(payload, id, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signed.DetachedJws!))), original.ReceiptId,
                    State.Revision, "key-b", "independent-no-issue-authority", _now, _now.AddMinutes(1)) };
        });
        Protection.RegisterAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<CancellationToken>()).Returns(call => Consumed ?? Registered(call.Arg<DeletionBatchConsumptionRequest>()));
        Protection.ReserveAndConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<DeletionBatchConsumptionRequest>(); Reservations++;
            var targetsRead = PartialProtectionResult ? request.Targets.Take(1) : request.Targets;
            Consumed = new("tenant-a", "batch-a", DeletionConsumptionStatus.Consumed, 2, 0, "complete-destruction", null, null, null,
                targetsRead.Select(value => new DeletionTargetReceipt(value, "batch-a", "receipt-" + value.AgentInteractionId)).ToArray());
            if (LoseConsumptionAcknowledgement) { throw new IOException("Controlled irreversible consume response loss."); }
            return Consumed;
        });
        Protection.LookupAsync("tenant-a", "batch-a", Arg.Any<CancellationToken>()).Returns(_ => UnknownProtectionLookup
            ? new("tenant-a", "batch-a", DeletionConsumptionStatus.Unavailable, 0, 0, null, null, null, null, []) : Consumed!);
        Coordinator = new(TimeProvider.System, new EventStoreDeletionBatchGuardPort(Source), _ => Signer, _ => Protection);
    }
    private static DeletionConsumptionOutcome Registered(DeletionBatchConsumptionRequest request) => new(request.Capability.TenantId, request.Capability.BatchId,
        DeletionConsumptionStatus.Unconsumed, 1, 0, "registered-a", null, null, null, []);
    internal static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value))!;
    public void Dispose() => Key.Dispose();
}
