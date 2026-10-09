using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Private concrete sign/conditional-issue/dispatch/protection/result/completion protocol. Stable actor/request identities handle unknown outcomes; no blind successor or consumption retry is created.</summary>
/// <param name="clock">Whole-operation deadline and current successor proof clock.</param><param name="guard">Typed actual append-time guard adapter.</param>
/// <param name="signers">Dedicated deterministic signer actor factory, scoped to the canonical original request.</param>
/// <param name="protectionOwners">Dedicated exact-tenant protection owner factory.</param>
public sealed class DeletionBatchExecutionCoordinator(TimeProvider clock, IDeletionBatchGuardPort? guard = null,
    Func<DeletionBatchCapabilityV1, IDeletionCapabilitySigningActor>? signers = null, Func<string, IDeletionProtectionOwner>? protectionOwners = null)
{
    /// <summary>Executes one exact stable signing attempt. Re-attestation additionally requires the source's exact compromise block and expected owner block-set activation.</summary>
    public async Task<DeletionBatchExecutionResult> ExecuteAsync(DeletionBatchCapabilityV1 payload, bool replacement = false, CancellationToken cancellationToken = default)
    {
        var deadline = new PrivateOwnerOperationDeadline(clock, cancellationToken);
        var providerCancellation = new CancellationTokenSource();
        try
        {
            deadline.Check();
            ArgumentNullException.ThrowIfNull(payload); string id = DeletionBatchCapabilityIdentity.SigningRequestId(payload);
            if (guard is null || signers is null || protectionOwners is null) { return new("Unavailable"); }
            var signer = await deadline.ReadAsync(() => Task.FromResult(signers(payload))).ConfigureAwait(false);
            var signed = await WaitAsync(() => signer.SignAsync(payload)).ConfigureAwait(false);
            if (signed.Payload != payload || signed.SigningRequestId != id) { return new("Conflict"); }
            if (signed.State == DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued)
            {
                var currentProof = await WaitAsync(() => signer.ReadSuccessorProofAsync(payload)).ConfigureAwait(false);
                return new("ObsoleteUnissued", signed, NextAttempt: DeletionCapabilitySigningSuccessor.Create(signed, currentProof, clock.GetUtcNow()));
            }
            if (signed.State != DeletionCapabilitySigningState.Signed) { return new(signed.State.ToString(), signed); }
            var issued = await WaitAsync(() => guard.IssueAsync(signed, replacement, providerCancellation.Token)).ConfigureAwait(false);
            if (issued is null) { return new("IssueUnknown", signed); }
            if (issued.Status is "Stale" or "IssuanceStale")
            {
                var obsolete = await WaitAsync(() => signer.ObsoleteUnissuedAsync(payload)).ConfigureAwait(false);
                return obsolete.State == DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued
                    ? new("ObsoleteUnissued", obsolete, Guard: issued, NextAttempt: DeletionCapabilitySigningSuccessor.Create(obsolete, clock.GetUtcNow()))
                    : new("NoIssueProofUnavailable", signed, Guard: issued);
            }
            if (issued.Status != "Committed") { return new(issued.Status, signed, Guard: issued); }
            IDeletionProtectionOwner? protection = null;
            if (replacement)
            {
                var pendingReplacement = await WaitAsync(() => guard.ReadAsync(payload, providerCancellation.Token)).ConfigureAwait(false);
                if (pendingReplacement?.Batch.ProtectionOutcome is "ReplacementAwaitingActivation" or "ConsumptionBlocked:CapabilityKeyCompromise")
                {
                    protection = await deadline.ReadAsync(() => Task.FromResult(protectionOwners(payload.TenantId))).ConfigureAwait(false);
                    var retained = await WaitAsync(() => protection.ReadBlockedReplacementAsync(payload, providerCancellation.Token)).ConfigureAwait(false);
                    if (retained is null && pendingReplacement.Batch.ProtectionOutcome == "ReplacementAwaitingActivation")
                    {
                        var comparison = await WaitAsync(() => protection.ReadActivationComparisonAsync(payload.TenantId, payload.BatchId, payload.CapabilityKeyVersion, providerCancellation.Token)).ConfigureAwait(false);
                        if (comparison?.ReplacementKeyBlocked == true)
                        {
                            if (comparison.CompromiseBlockReceiptId != pendingReplacement.Batch.ProtectionReceiptId || comparison.TenantId != payload.TenantId
                                || comparison.BatchId != payload.BatchId || comparison.ReplacementKeyVersion != payload.CapabilityKeyVersion || comparison.OwnerRevision <= 0
                                || comparison.KeyBlockSetRevision < pendingReplacement.Batch.BlockSetRevision || comparison.ReplacementKeyRevocation is null)
                            { return new("ReplacementComparisonUnavailable", signed); }
                            var phase = new DeletionBlockedReplacementReconciliation("blocked-replacement-" + id + "-compare-" + comparison.KeyBlockSetRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                pendingReplacement.Batch.ProtectionReceiptId, comparison.KeyBlockSetRevision, pendingReplacement.Batch.IssueReceiptId, payload, signed.DetachedJws!, id,
                                pendingReplacement.Batch.IssuedGuardRevision, await CaptureTargetsAsync(pendingReplacement.Batch.Targets).ConfigureAwait(false), comparison.ReplacementKeyRevocation);
                            try { _ = await WaitAsync(() => protection.ReconcileBlockedReplacementAsync(phase, providerCancellation.Token)).ConfigureAwait(false); }
                            catch (Exception) { deadline.Check(); }
                            retained = await WaitAsync(() => protection.ReadBlockedReplacementAsync(payload, providerCancellation.Token)).ConfigureAwait(false);
                            if (retained is null) { return new("ReplacementReconciliationUnknown", signed); }
                        }
                    }
                    if (retained is not null)
                    {
                        if (retained.Original.Capability != payload || retained.Original.SigningRequestId != id || retained.Original.DetachedJws != signed.DetachedJws
                            || (pendingReplacement.Batch.ProtectionOutcome == "ReplacementAwaitingActivation" ? retained.Original.CompromiseBlockReceiptId : retained.Outcome.ReceiptId) != pendingReplacement.Batch.ProtectionReceiptId || retained.Original.GuardReplacementReceiptId != pendingReplacement.Batch.IssueReceiptId
                            || retained.Original.CommittedIssuedGuardRevision != pendingReplacement.Batch.IssuedGuardRevision || retained.Outcome.Status != DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise
                            || !Exact(retained.Outcome)) { return new("ReplacementReconciliationUnverified", signed); }
                        if (pendingReplacement.Batch.ProtectionOutcome == "ConsumptionBlocked:CapabilityKeyCompromise")
                        { deadline.Check(); return new("ActivationBlockedByReplacementKeyCompromise", signed, retained.Outcome, issued); }
                        var mirroredBlock = await WaitAsync(() => guard.RecordBlockedReplacementAsync(signed, retained, providerCancellation.Token)).ConfigureAwait(false);
                        deadline.Check();
                        return new(mirroredBlock?.Status == "Committed" ? "ActivationBlockedByReplacementKeyCompromise" : "ReplacementBlockMirrorUnknown", signed, retained.Outcome, mirroredBlock);
                    }
                }
            }
            var dispatch = await WaitAsync(() => guard.DispatchAsync(signed, providerCancellation.Token)).ConfigureAwait(false);
            if (dispatch?.Status != "Committed") { return new(dispatch?.Status ?? "DispatchUnknown", signed, Guard: dispatch); }
            var snapshot = await WaitAsync(() => guard.ReadAsync(payload, providerCancellation.Token)).ConfigureAwait(false);
            if (snapshot is null || snapshot.Batch.Capability != payload || snapshot.Batch.DetachedJws != signed.DetachedJws
                || snapshot.Batch.SigningRequestId != id || snapshot.Batch.IssuedGuardRevision <= 0 || snapshot.Batch.DispatchGuardRevision <= 0
                || string.IsNullOrWhiteSpace(snapshot.Batch.DispatchReceiptId)) { return new("DispatchUnverified", signed); }
            var targets = await CaptureTargetsAsync(snapshot.Batch.Targets).ConfigureAwait(false);
            var request = new DeletionBatchConsumptionRequest(payload, signed.DetachedJws!, snapshot.Batch.IssuedGuardRevision,
                snapshot.Batch.DispatchReceiptId, snapshot.Batch.DispatchGuardRevision,
                targets);
            protection ??= await deadline.ReadAsync(() => Task.FromResult(protectionOwners(payload.TenantId))).ConfigureAwait(false);
            if (replacement && snapshot.Batch.ProtectionOutcome == "ReplacementAwaitingActivation")
            {
                if (snapshot.Batch.BlockSetRevision <= 0 || string.IsNullOrWhiteSpace(snapshot.Batch.ProtectionReceiptId) || string.IsNullOrWhiteSpace(snapshot.Batch.IssueReceiptId)) { return new("ReplacementBlockUnverified", signed); }
                DeletionConsumptionOutcome? activated = null;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var comparison = await WaitAsync(() => protection.ReadActivationComparisonAsync(payload.TenantId, payload.BatchId, payload.CapabilityKeyVersion, providerCancellation.Token)).ConfigureAwait(false);
                    if (comparison is null || comparison.TenantId != payload.TenantId || comparison.BatchId != payload.BatchId || comparison.OwnerRevision <= 0
                        || comparison.CompromiseBlockReceiptId != snapshot.Batch.ProtectionReceiptId || comparison.ReplacementKeyVersion != payload.CapabilityKeyVersion
                        || comparison.KeyBlockSetRevision < snapshot.Batch.BlockSetRevision) { return new("ReplacementComparisonUnavailable", signed); }
                    string comparisonId = comparison.KeyBlockSetRevision.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var activation = new DeletionReattestationActivation("activation-" + id + "-compare-" + comparisonId, snapshot.Batch.ProtectionReceiptId,
                        comparison.KeyBlockSetRevision, snapshot.Batch.IssueReceiptId, request);
                    activated = await RecoverAsync(() => protection.ActivateAsync(activation, providerCancellation.Token), protection).ConfigureAwait(false);
                    if (activated?.Status != DeletionConsumptionStatus.Conflict) { break; }
                }
                if (!Exact(activated)) { return new("ActivationUnknown", signed); }
                var activationMirror = await WaitAsync(() => guard.RecordProtectionAsync(signed, activated!, true, providerCancellation.Token)).ConfigureAwait(false);
                if (activationMirror?.Status != "Committed" || activated!.Status != DeletionConsumptionStatus.Unconsumed)
                { return new(activated!.Status.ToString(), signed, activated, activationMirror); }
            }
            var registered = await RecoverAsync(() => protection.RegisterAsync(request, providerCancellation.Token), protection).ConfigureAwait(false);
            if (!Exact(registered)) { return new("ProtectionUnknown", signed); }
            DeletionConsumptionOutcome? result = registered;
            if (registered!.Status == DeletionConsumptionStatus.Unconsumed)
            { result = await RecoverAsync(() => protection.ReserveAndConsumeAsync(request, providerCancellation.Token), protection).ConfigureAwait(false); }
            if (!Exact(result)) { return new("ConsumptionUnknown", signed); }
            if (result!.Status is not (DeletionConsumptionStatus.Consumed or DeletionConsumptionStatus.AlreadyDestroyedByBatch)) { return new(result.Status.ToString(), signed, result); }
            if (result.TargetReceipts.Count != request.Targets.Count || !result.TargetReceipts.Select(value => value.Target).SequenceEqual(request.Targets)) { return new("ConsumptionUnverified", signed); }
            var mirrored = await WaitAsync(() => guard.RecordProtectionAsync(signed, result, false, providerCancellation.Token)).ConfigureAwait(false);
            if (mirrored?.Status != "Committed") { return new("ConsumptionMirrorUnknown", signed, result, mirrored); }
            var completion = await WaitAsync(() => guard.CompleteAsync(payload, providerCancellation.Token)).ConfigureAwait(false);
            deadline.Check();
            return new(completion?.Status == "CompletionSealed" ? "CompletionSealed" : "ConsumedCompletionBlocked", signed, result, completion);

            async Task<IReadOnlyList<ProtectionTarget>> CaptureTargetsAsync(IReadOnlyList<GovernanceProtectionTarget> supplied)
                => await WaitAsync(() =>
                {
                    var captured = new List<ProtectionTarget>();
                    foreach (var value in supplied)
                    { deadline.Check(); if (captured.Count >= 1000 || value is null) { throw new ArgumentException("Oversized exact protection manifest."); } captured.Add(new(value.TenantId, value.AgentInteractionId, value.TargetProtectionKeyAlias)); }
                    if (DeletionBatchCapabilityIdentity.TargetManifestDigest(captured) != payload.ManifestDigest) { throw new ArgumentException("Mismatched exact protection manifest."); }
                    return Task.FromResult<IReadOnlyList<ProtectionTarget>>(captured.AsReadOnly());
                }).ConfigureAwait(false);
            async Task<T> WaitAsync<T>(Func<Task<T>> operation)
                => await deadline.ReadAsync(operation).ConfigureAwait(false);
            async Task<DeletionConsumptionOutcome?> RecoverAsync(Func<Task<DeletionConsumptionOutcome>> operation, IDeletionProtectionOwner owner)
            {
                try
                {
                    var result = await WaitAsync(operation).ConfigureAwait(false);
                    return result.Status is DeletionConsumptionStatus.Unavailable or DeletionConsumptionStatus.ConsumptionReserved
                        ? await WaitAsync(() => owner.LookupAsync(payload.TenantId, payload.BatchId, providerCancellation.Token)).ConfigureAwait(false) : result;
                }
                catch (Exception) { deadline.Check(); return await WaitAsync(() => owner.LookupAsync(payload.TenantId, payload.BatchId, providerCancellation.Token)).ConfigureAwait(false); }
            }
            bool Exact(DeletionConsumptionOutcome? result) => result is not null && result.TenantId == payload.TenantId && result.BatchId == payload.BatchId
                && result.OwnerRevision > 0 && !string.IsNullOrWhiteSpace(result.ReceiptId);
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return new("Unavailable"); }
        finally
        {
            var canceled = providerCancellation.CancelAsync();
            _ = canceled.ContinueWith(task => { _ = task.Exception; providerCancellation.Dispose(); }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }
}
