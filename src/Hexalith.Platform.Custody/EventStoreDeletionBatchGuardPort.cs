using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Actual typed adapter to the shared append-time guard owner. It preserves canonical signer identity, original results and separate actual issue/dispatch revisions.</summary>
/// <param name="guard">Dedicated private typed EventStore owner; omission from normal DI leaves execution unavailable.</param>
public sealed class EventStoreDeletionBatchGuardPort(IGovernanceScopeGuard guard) : IDeletionBatchGuardPort
{
    /// <inheritdoc/>
    public async Task<DeletionBatchGuardSnapshot?> ReadAsync(DeletionBatchCapabilityV1 payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload); _ = DeletionBatchCapabilityIdentity.SigningRequestId(payload);
        var state = await guard.ReadAsync(payload.TenantId, cancellationToken).ConfigureAwait(false);
        var deletion = state?.Deletions.SingleOrDefault(value => value.RequestId == payload.DeletionRequestId);
        var batch = deletion?.Batches.SingleOrDefault(value => value.BatchId == payload.BatchId);
        return state is not null && deletion is not null && batch is not null && state.TenantId == payload.TenantId && deletion.SealId == payload.DestructionSealId
            && batch.Kind == payload.BatchKind && batch.Ordinal == payload.BatchOrdinal && batch.ManifestDigest == payload.ManifestDigest ? new(state, deletion, batch) : null;
    }
    /// <inheritdoc/>
    public async Task<GovernanceProtocolReceipt?> IssueAsync(DeletionCapabilitySigningOutcome signed, bool replacement, CancellationToken cancellationToken = default)
    {
        if (!Valid(signed)) { return null; }
        var snapshot = await ReadAsync(signed.Payload, cancellationToken).ConfigureAwait(false); if (snapshot is null) { return null; }
        var batch = Carrier(snapshot.Batch, signed);
        string id = "issue-" + Hash(new[] { signed.SigningRequestId, batch.AttestationDigest });
        var original = snapshot.State.Receipts.SingleOrDefault(value => value.OperationId == id && value.Capability == signed.Payload
            && value.SigningRequestId == signed.SigningRequestId && value.DetachedJwsDigest == batch.AttestationDigest);
        if (original is not null) { return original; }
        if (!replacement) { batch = batch with { BlockSetRevision = 0 }; }
        return await guard.ExecuteAsync(Command(signed.Payload, replacement ? GovernanceGuardOperation.ReplaceAttestation : GovernanceGuardOperation.RecordBatchIssued,
            signed.Payload.IntendedIssuedGuardRevision, id, batch), [], cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public async Task<GovernanceProtocolReceipt?> DispatchAsync(DeletionCapabilitySigningOutcome signed, CancellationToken cancellationToken = default)
    {
        if (!Valid(signed)) { return null; }
        var snapshot = await ReadAsync(signed.Payload, cancellationToken).ConfigureAwait(false);
        if (snapshot is null || !Active(snapshot.Batch, signed)) { return null; }
        if (snapshot.Batch.DispatchGuardRevision > 0)
        { return snapshot.State.Receipts.SingleOrDefault(value => value.GuardHighWater == snapshot.Batch.DispatchGuardRevision && value.ReferenceId == snapshot.Batch.BatchId); }
        var batch = Carrier(snapshot.Batch, signed);
        string id = "dispatch-" + Hash(new[] { signed.SigningRequestId, snapshot.State.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        var authorization = await guard.ExecuteAsync(Command(signed.Payload, GovernanceGuardOperation.AuthorizeDispatch, snapshot.State.Revision, "authorize-" + id, batch, id), [], cancellationToken).ConfigureAwait(false);
        if (authorization?.Status != "Committed") { return authorization; }
        return await guard.ExecuteAsync(Command(signed.Payload, GovernanceGuardOperation.CommitDispatch, authorization.GuardHighWater, "effect-" + id, batch, id), [], cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public async Task<GovernanceProtocolReceipt?> RecordProtectionAsync(DeletionCapabilitySigningOutcome signed, DeletionConsumptionOutcome outcome, bool activation, CancellationToken cancellationToken = default)
    {
        if (!Valid(signed) || outcome is null || outcome.TenantId != signed.Payload.TenantId || outcome.BatchId != signed.Payload.BatchId || outcome.OwnerRevision <= 0 || string.IsNullOrWhiteSpace(outcome.ReceiptId)) { return null; }
        var snapshot = await ReadAsync(signed.Payload, cancellationToken).ConfigureAwait(false);
        if (snapshot is null || !Active(snapshot.Batch, signed)) { return null; }
        string status = activation ? outcome.Status switch { DeletionConsumptionStatus.Unconsumed => "Activated", DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise => "ActivationBlockedByReplacementKeyCompromise", _ => "" }
            : outcome.Status switch { DeletionConsumptionStatus.Consumed => "Consumed", DeletionConsumptionStatus.AlreadyDestroyedByBatch => "AlreadyDestroyed", _ => "" };
        if (status == "") { return null; }
        if (!activation && (outcome.TargetReceipts.Count != snapshot.Batch.Targets.Count || !outcome.TargetReceipts.Select(value => value.Target)
            .SequenceEqual(snapshot.Batch.Targets.Select(value => new ProtectionTarget(value.TenantId, value.AgentInteractionId, value.TargetProtectionKeyAlias))))) { return null; }
        string operationId = "protection-result-" + Hash(new[] { signed.SigningRequestId, outcome.ReceiptId! });
        var original = snapshot.State.Receipts.SingleOrDefault(value => value.OperationId == operationId && value.Capability == signed.Payload);
        if (original is not null) { return original; }
        var batch = Carrier(snapshot.Batch, signed) with { ProtectionOutcome = status, ProtectionReceiptId = outcome.ReceiptId!,
            TargetReceiptIds = outcome.TargetReceipts.Select(value => value.ReceiptId).ToArray(), BlockSetRevision = outcome.KeyBlockSetRevision };
        return await guard.ExecuteAsync(Command(signed.Payload, GovernanceGuardOperation.RecordProtectionOutcome, snapshot.State.Revision,
            operationId, batch), [], cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public async Task<GovernanceProtocolReceipt?> CompleteAsync(DeletionBatchCapabilityV1 payload, CancellationToken cancellationToken = default)
    {
        var snapshot = await ReadAsync(payload, cancellationToken).ConfigureAwait(false); if (snapshot is null) { return null; }
        if (snapshot.Deletion.Completed) { return snapshot.State.Receipts.SingleOrDefault(value => value.Status == "CompletionSealed" && value.ReferenceId == payload.DeletionRequestId); }
        string id = "completion-" + Hash(new[] { payload.DeletionRequestId, snapshot.State.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        var authorization = await guard.ExecuteAsync(Command(payload, GovernanceGuardOperation.AuthorizeCompletion, snapshot.State.Revision, "authorize-" + id, null, id), [], cancellationToken).ConfigureAwait(false);
        if (authorization?.Status != "Committed") { return authorization; }
        return await guard.ExecuteAsync(Command(payload, GovernanceGuardOperation.CommitCompletion, authorization.GuardHighWater, "effect-" + id, null, id), [], cancellationToken).ConfigureAwait(false);
    }
    private static GovernanceGuardTransition Command(DeletionBatchCapabilityV1 payload, GovernanceGuardOperation operation, long revision, string operationId, GovernanceBatchCommand? batch, string authorization = "")
        => new(payload.TenantId, operationId, operation, revision, "", payload.DeletionRequestId, null, null, null, null, null, batch, authorization, "");
    private static GovernanceBatchCommand Carrier(GovernanceBatchState source, DeletionCapabilitySigningOutcome signed)
        => new(source.BatchId, source.Kind, source.Ordinal, source.Targets, source.ManifestDigest, signed.Payload.AttestationOrdinal, signed.Payload.CapabilityKeyVersion,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signed.DetachedJws!))), "", "", [], source.BlockSetRevision)
            { Capability = signed.Payload, DetachedJws = signed.DetachedJws!, SigningRequestId = signed.SigningRequestId };
    private static bool Active(GovernanceBatchState source, DeletionCapabilitySigningOutcome signed) => source.Capability == signed.Payload && source.DetachedJws == signed.DetachedJws && source.SigningRequestId == signed.SigningRequestId;
    private static bool Valid(DeletionCapabilitySigningOutcome signed) => signed is not null && signed.State == DeletionCapabilitySigningState.Signed && signed.SigningRequestId == DeletionBatchCapabilityIdentity.SigningRequestId(signed.Payload)
        && signed.DetachedJws is { Length: > 0 and <= 16384 };
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
