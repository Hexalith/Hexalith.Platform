using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Owned private permission/source/guard/registrar receiving methods. Every decision uses the current authenticated grant and actual retained native originals.</summary>
public sealed class ErasureRootAuthorityOperationHandler(ErasureRootCustody custody, ErasureRootNativeOccurrenceSource source,
    IGovernanceScopeGuard guard, IDeletionCapabilityRevocationAuthenticator revocations, string acceptedCopyPolicySha256, TimeProvider clock) : IIndependentOwnerOperationHandler
{
    /// <inheritdoc />
    public IReadOnlySet<string> Methods { get; } = new HashSet<string>(["erc-verify-source-enrollment", "erc-capture-effect-boundary", "erc-migrate-guard-history", "erc-permission", "erc-occurrence-original", "erc-source-proof", "erc-registrar-proof",
        "erc-current-protocol-evidence", "erc-read-guard", "erc-execute-guard", "erc-lookup-guard-original", "erc-read-no-issue", "erc-register-source-original",
        "erc-read-observer-permissions", "erc-read-permanent-loss", "erc-read-erased-original", "erc-read-erased-snapshot", "erc-register-revocation-original", "erc-read-rejected-issue"], StringComparer.Ordinal);
    /// <inheritdoc />
    public bool IsReadOnly(string method) => method is not ("erc-capture-effect-boundary" or "erc-migrate-guard-history" or "erc-execute-guard" or "erc-source-proof" or "erc-register-source-original" or "erc-register-revocation-original" or "erc-registrar-proof" or "erc-read-no-issue" or "erc-read-rejected-issue" or "erc-lookup-guard-original");
    /// <inheritdoc />
    public bool RequiresCurrentResultAdmission(string method) => true;
    /// <inheritdoc />
    public Task<object?> RecoverAsync(string method, JsonElement payload, IndependentCallerGrant caller, IndependentAuthorityState current, CancellationToken token)
        => HandleCoreAsync(method, payload, caller, current, true, token);
    /// <inheritdoc />
    public Task<object?> HandleAsync(string method, JsonElement payload, IndependentCallerGrant caller, IndependentAuthorityState current, CancellationToken token)
        => HandleCoreAsync(method, payload, caller, current, false, token);
    private async Task<object?> HandleCoreAsync(string method, JsonElement payload, IndependentCallerGrant caller, IndependentAuthorityState current, bool recovery, CancellationToken token)
    {
        if (current.ValidUntil <= clock.GetUtcNow() || caller.ValidUntil <= clock.GetUtcNow() || caller.Revoked
            || caller.Role is not ("erc-owner" or "erc-source" or "erc-registrar" or "erc-observer")) { return null; }
        string tenant;
        try { tenant = Tenant(payload, method); }
        catch (Exception) { return null; }
        if (caller.ScopeReference != tenant + "|erc") { return null; }
        if (method == "erc-verify-source-enrollment")
        { return await source.VerifyEnrollmentAsync(payload.GetProperty("enrollment").Deserialize<ErasureRootOccurrenceEnrollment>(ErasureRootCustody.Json)!, token).ConfigureAwait(false); }
        if (method == "erc-capture-effect-boundary")
        {
            if (caller.Role != "erc-owner" || current.QualificationBindings is not { } effectGrants
                || !effectGrants.TryGetValue("erc.allow.CaptureEffectBoundary", out var effectRole) || effectRole != caller.Role) { return null; }
            return await custody.CaptureEffectBoundaryAsync(tenant, payload.GetProperty("callDigest").GetString()!, payload.GetProperty("boundary").GetString()!, recovery, token).ConfigureAwait(false);
        }
        if (method == "erc-migrate-guard-history")
        {
            if (!payload.TryGetProperty("operationId", out var operationId) || operationId.GetString() is not { Length: > 0 and <= 2048 }
                || caller.Role != "erc-owner" || current.QualificationBindings is not { } grants
                || !grants.TryGetValue("erc.allow.MigrateGuardArchiveIndexes", out var role) || role != caller.Role) { return false; }
            for (int attempt = 0; attempt < 16; attempt++)
            {
                // Explicit authorized legacy migration is the only non-observer path that requests complete history.
                var read = await custody.ReadAsync(tenant, token, true).ConfigureAwait(false);
                if (read is not { } head || head.Head.Pending is not null) { return false; }
                var required = head.Head.GuardAdmissions.Where(entry => entry.Value.Transition.Operation == GovernanceGuardOperation.ReplaceAttestation
                    && entry.Value.Transition.Batch?.SigningRequestId is { Length: > 0 }).ToArray();
                if (required.All(entry => head.Head.GuardSigningIntents.GetValueOrDefault(entry.Value.Transition.Batch!.SigningRequestId) == entry.Key)) { return true; }
                if (recovery) { return false; }
                if (await custody.SaveAsync(tenant, head.Version, head.Head, token).ConfigureAwait(false)) { return true; }
            }
            return false;
        }
        if (method == "erc-read-observer-permissions")
        {
            if (caller.Role != "erc-observer" || current.QualificationCases is null
                || !current.QualificationCases.TryGetValue("erc.observer-permissions:" + payload.GetProperty("workloadIdentity").GetString(), out var bytes)) { return null; }
            var proof = bytes.Deserialize<ProtectionQualificationObserverPermission>(CustodyBindingContract.Json);
            return proof?.TenantId == tenant && proof.StoreId == custody.Installation.StoreId && proof.EnrollmentId == custody.Installation.EnrollmentId
                && proof.WorkloadIdentity == payload.GetProperty("workloadIdentity").GetString() && proof.ObservedAt <= clock.GetUtcNow()
                && proof.ValidUntil > clock.GetUtcNow() ? proof : null;
        }
        if (method == "erc-register-revocation-original")
        {
            if (caller.Role is not ("erc-owner" or "erc-registrar")) { return false; }
            var revocationOriginal = payload.GetProperty("original").Deserialize<ErasureRootRevocationOriginal>(CustodyBindingContract.Json)!;
            if (revocationOriginal.Envelope.TenantId != tenant || string.IsNullOrWhiteSpace(revocationOriginal.SignedEvidence) || revocationOriginal.SignedEvidence.Length > 16384) { return false; }
            var verified = await revocations.VerifyAsync(revocationOriginal.Envelope, revocationOriginal.SignedEvidence, token).ConfigureAwait(false);
            if (verified?.Envelope != revocationOriginal.Envelope || verified.ObservedAt > clock.GetUtcNow() || verified.ValidUntil <= clock.GetUtcNow()) { return false; }
            string id = ErasureRootCustody.Hash(revocationOriginal.Envelope);
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
                if (read is not { } head) { return false; }
                if (await custody.ReadRetainedAsync(head.Head, "RevocationOriginals", head.Head.RevocationOriginals, id, token).ConfigureAwait(false) is { } prior) { return prior == revocationOriginal; }
                if (recovery) { return false; }
                head.Head.RevocationOriginals.Add(id, revocationOriginal);
                if (await custody.SaveAsync(tenant, head.Version, head.Head, token).ConfigureAwait(false)) { return true; }
            }
            return false;
        }
        if (method == "erc-read-permanent-loss")
        { return PermanentLoss(current, tenant, payload.GetProperty("storeId").GetString()!, payload.GetProperty("enrollmentId").GetString()!); }
        if (method == "erc-register-source-original")
        {
            if (caller.Role is not ("erc-owner" or "erc-source")) { return false; }
            var registeredSource = payload.GetProperty("enrollment").Deserialize<ErasureRootOccurrenceEnrollment>(CustodyBindingContract.Json)!;
            if (PermanentLoss(current, tenant, registeredSource.RootIdentity.StoreTarget, custody.Installation.EnrollmentId) is not null) { return false; }
            if (recovery)
            {
                var retained = await source.ResolveAsync(registeredSource.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(registeredSource.Binding.Identity), token).ConfigureAwait(false);
                return retained is not null && ErasureRootCustody.Hash(retained) == ErasureRootCustody.Hash(registeredSource);
            }
            return await source.RegisterOriginalAsync(registeredSource, token).ConfigureAwait(false);
        }
        if (method == "erc-read-erased-original")
        {
            var supplied = payload.GetProperty("loss").Deserialize<ErasureRootPermanentLossReceipt>(CustodyBindingContract.Json)!;
            var loss = PermanentLoss(current, tenant, supplied.StoreId, supplied.EnrollmentId);
            if (loss is null || loss != supplied) { return null; }
            var owner = payload.GetProperty("owner").Deserialize<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(CustodyBindingContract.Json)!;
            var occurrence = payload.GetProperty("occurrence").Deserialize<PayloadProtectionOccurrenceContext>(CustodyBindingContract.Json)!;
            var enrollment = await source.ResolveAsync(owner, occurrence, token).ConfigureAwait(false);
            return enrollment is not null && enrollment.RootIdentity.StoreTarget == loss.StoreId && enrollment.Binding.KeyReference == payload.GetProperty("keyReference").GetString()
                && await source.VerifyPersistedSourceAsync(enrollment, payload.GetProperty("sealedPayloadSha256").GetString()!, token).ConfigureAwait(false) ? enrollment : null;
        }
        if (method == "erc-read-erased-snapshot")
        {
            var supplied = payload.GetProperty("loss").Deserialize<ErasureRootPermanentLossReceipt>(CustodyBindingContract.Json)!;
            var loss = PermanentLoss(current, tenant, supplied.StoreId, supplied.EnrollmentId);
            var binding = payload.GetProperty("binding").Deserialize<InteractionProtectionBinding>(CustodyBindingContract.Json)!;
            return loss is not null && loss == supplied ? await source.ReadErasedSnapshotSkeletonAsync(binding, payload.GetProperty("sealedPayloadSha256").GetString()!, loss, token).ConfigureAwait(false) : null;
        }
        if (method == "erc-read-guard") { return await guard.ReadAsync(tenant, token).ConfigureAwait(false); }
        if (method == "erc-read-rejected-issue")
        {
            var proposed = payload.GetProperty("payload").Deserialize<DeletionBatchCapabilityV1>(CustodyBindingContract.Json)!;
            string signingId = payload.GetProperty("signingRequestId").GetString()!, signature = payload.GetProperty("detachedJwsDigest").GetString()!;
            var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false); var state = await guard.ReadAsync(tenant, token).ConfigureAwait(false);
            if (read is not { } head || state is null || state.Receipts.Any(value => value.Status == "Committed" && value.SigningRequestId == signingId)) { return null; }
            if (await custody.ReadSigningIntentAsync(head.Head, signingId, token).ConfigureAwait(false) is { } signingIntent)
            {
                _ = await custody.ReadGuardAttemptAsync(tenant, head.Head, signingIntent, token).ConfigureAwait(false);
                _ = await custody.ReadGuardAdmissionAsync(tenant, head.Head, signingIntent, token).ConfigureAwait(false);
            }
            // Legacy in-head originals remain readable. Old archived entries need an explicit complete migration; absence never invents a refusal.
            var rejectedOriginal = head.Head.GuardAttempts.Values.SingleOrDefault(value => value.Transition.Operation == GovernanceGuardOperation.ReplaceAttestation
                && value.Transition.Batch?.Capability == proposed && value.Receipt.SigningRequestId == signingId && value.Receipt.DetachedJwsDigest == signature
                && value.Receipt.Status is "Blocked" or "Stale");
            if (rejectedOriginal is null)
            {
                var candidates = head.Head.GuardAdmissions.Where(value => value.Value.Transition.Operation == GovernanceGuardOperation.ReplaceAttestation
                    && value.Value.Transition.Batch?.Capability == proposed && value.Value.Transition.Batch.SigningRequestId == signingId).ToArray();
                if (candidates.Length == 1) { rejectedOriginal = await RecoverGuardAttemptAsync(tenant, candidates[0].Key, candidates[0].Value, token).ConfigureAwait(false); }
                if (rejectedOriginal?.Receipt.Status is not ("Blocked" or "Stale") || rejectedOriginal.Receipt.DetachedJwsDigest != signature) { return null; }
            }
            return rejectedOriginal is null ? null : new GovernanceRejectedIssueOriginal(rejectedOriginal.Transition, rejectedOriginal.TargetMutationDigest, rejectedOriginal.Receipt, state.Revision, ErasureRootCustody.Hash(state))
                { CurrentGuardStateBytes = JsonSerializer.SerializeToUtf8Bytes(state) };
        }
        if (method is "erc-execute-guard" or "erc-lookup-guard-original")
        {
            if (caller.Role != "erc-owner") { return null; }
            var transition = payload.GetProperty("transition").Deserialize<GovernanceGuardTransition>(CustodyBindingContract.Json)!;
            var targets = payload.GetProperty("targets").Deserialize<GuardedStateMutation[]>(CustodyBindingContract.Json)!;
            string targetDigest = ErasureRootCustody.Hash(targets);
            string intent = ErasureRootCustody.Hash(new { Transition = transition, TargetMutationDigest = targetDigest });
            var retained = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
            if (retained is { } originalHead && await custody.ReadGuardAttemptAsync(tenant, originalHead.Head, intent, token).ConfigureAwait(false) is { } prior) { return prior.Receipt; }
            if (recovery || method == "erc-lookup-guard-original")
            {
                if (retained is { } admittedHead && await custody.ReadGuardAdmissionAsync(tenant, admittedHead.Head, intent, token).ConfigureAwait(false) is { } originalAdmission)
                { return (await RecoverGuardAttemptAsync(tenant, intent, originalAdmission, token).ConfigureAwait(false))?.Receipt; }
                return await guard.LookupOriginalAsync(transition, targets, token).ConfigureAwait(false);
            }
            if (retained is { } pendingAdmissionHead && await custody.ReadGuardAdmissionAsync(tenant, pendingAdmissionHead.Head, intent, token).ConfigureAwait(false) is { } admitted)
            { return (await RecoverGuardAttemptAsync(tenant, intent, admitted, token).ConfigureAwait(false))?.Receipt; }
            var before = await guard.ReadAsync(tenant, token).ConfigureAwait(false);
            if (before is null) { return null; }
            bool admittedNew = false;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var head = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
                if (head is not { } guardAdmissionHead || guardAdmissionHead.Head.Pending is not null) { return null; }
                if (await custody.ReadGuardAdmissionAsync(tenant, guardAdmissionHead.Head, intent, token).ConfigureAwait(false) is { } existingAdmission)
                { return (await RecoverGuardAttemptAsync(tenant, intent, existingAdmission, token).ConfigureAwait(false))?.Receipt; }
                guardAdmissionHead.Head.GuardAdmissions.Add(intent, new(transition, targets, before.Revision));
                if (await custody.SaveAsync(tenant, guardAdmissionHead.Version, guardAdmissionHead.Head, token).ConfigureAwait(false)) { admittedNew = true; break; }
            }
            if (!admittedNew) { return null; }
            var actual = await guard.ExecuteAsync(transition, targets, token).ConfigureAwait(false);
            var after = await guard.ReadAsync(tenant, token).ConfigureAwait(false);
            if (actual is null || after is null || actual.OperationId != transition.OperationId || actual.IntentDigest != intent
                || after.Revision < before.Revision || actual.GuardHighWater < before.Revision || actual.GuardHighWater > after.Revision) { return null; }
            var captured = new ErasureRootGuardAttempt(transition, targetDigest, before.Revision, after.Revision, actual);
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
                if (read is not { } head) { return null; }
                if (await custody.ReadGuardAttemptAsync(tenant, head.Head, intent, token).ConfigureAwait(false) is { } existing) { return existing == captured ? actual : null; }
                if (head.Head.GuardAttempts.Count >= 10000) { return null; }
                head.Head.GuardAttempts.Add(intent, captured);
                if (await custody.SaveAsync(tenant, head.Version, head.Head, token).ConfigureAwait(false)) { return actual; }
            }
            return null;
        }
        if (method == "erc-read-no-issue")
        {
            var signedPayload = payload.GetProperty("payload").Deserialize<DeletionBatchCapabilityV1>(CustodyBindingContract.Json)!;
            string signingId = payload.GetProperty("signingRequestId").GetString()!, signatureDigest = payload.GetProperty("detachedJwsDigest").GetString()!;
            if (recovery)
            {
                var recoveryHead = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
                if (recoveryHead is not { } retained) { return null; }
                var legacyRecovery = retained.Head.SigningRecoveries.Values.Where(value => value.Payload == signedPayload && value.SigningRequestId == signingId && value.DetachedJwsDigest == signatureDigest)
                    .OrderBy(value => value.ObservedAt).LastOrDefault();
                return legacyRecovery ?? await new ErasureRootOccurrenceIndex(custody.Store, custody.Installation, tenant).ReadAsync<GovernanceSigningRecovery>(retained.Head.LifetimeIndexRoot,
                    "signing-recovery-reference:" + ErasureRootCustody.Hash(new { Payload = signedPayload, SigningRequestId = signingId, DetachedJwsDigest = signatureDigest }), token).ConfigureAwait(false);
            }
            var actual = await guard.ReadNoIssueAsync(signedPayload, signingId, signatureDigest, token).ConfigureAwait(false);
            if (actual is null || actual.Payload != signedPayload || actual.SigningRequestId != signingId || actual.DetachedJwsDigest != signatureDigest) { return null; }
            string id = ErasureRootCustody.Hash(actual);
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
                if (read is not { } head) { return null; }
                if (await custody.ReadRetainedAsync(head.Head, "SigningRecoveries", head.Head.SigningRecoveries, id, token).ConfigureAwait(false) is not null) { return actual; }
                head.Head.SigningRecoveries.Add(id, actual);
                if (await custody.SaveAsync(tenant, head.Version, head.Head, token).ConfigureAwait(false)) { return actual; }
            }
            return null;
        }
        if (method == "erc-occurrence-original")
        {
            var owner = payload.GetProperty("owner").Deserialize<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(CustodyBindingContract.Json)!;
            var occurrence = payload.GetProperty("occurrence").Deserialize<PayloadProtectionOccurrenceContext>(CustodyBindingContract.Json)!;
            return await source.ResolveAsync(owner, occurrence, token).ConfigureAwait(false);
        }
        if (method == "erc-current-protocol-evidence")
        {
            var transition = payload.GetProperty("transition").Deserialize<AnchoredStateTransition>(CustodyBindingContract.Json)!;
            string digest = ErasureRootCustody.Hash(transition);
            bool permitted = await ProtocolEvidenceAsync(transition, current, token).ConfigureAwait(false);
            return Evidence(tenant, transition.ScopeId, "VerifyProtocolEvidence", digest, permitted, current);
        }
        string identity = payload.GetProperty("identity").GetString()!, operation = payload.GetProperty("operation").GetString()!, requestDigest = payload.GetProperty("requestDigest").GetString()!;
        var original = payload.GetProperty("original");
        bool allowed = false;
        if (method == "erc-permission")
        {
            // The native receiver independently authenticates the current machine grant. Exact role/method grants are explicit installation inputs, never wildcard startup defaults.
            allowed = current.QualificationBindings is { } bindings && bindings.TryGetValue("erc.allow." + operation, out var permittedRole) && permittedRole == caller.Role;
            if (operation is "WrapDek" or "UnwrapDek" or "ApplyKeyLifecycle" or "DestroyDekManifest")
            { allowed &= PermanentLoss(current, tenant, custody.Installation.StoreId, custody.Installation.EnrollmentId) is null; }
            if (operation == "VerifyLifecycle")
            {
                var request = original.Deserialize<CustodyKeyLifecycleRequest>(CustodyBindingContract.Json)!;
                allowed &= request.Identity.TenantId == tenant && ErasureRootCustody.Hash(request) == requestDigest && request.FenceRevision > 0
                    && current.QualificationBindings!.TryGetValue("erc.fence." + request.FenceTokenId, out var fence) && fence == requestDigest;
            }
            if (operation == "VerifyReservedManifestAndCopyPolicy")
            {
                allowed &= current.QualificationBindings!.TryGetValue("erc.copyPolicySha256", out var qualified) && qualified == acceptedCopyPolicySha256;
                var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
                allowed &= read is { } actual && actual.Head.Anchors.TryGetValue(DeletionConsumptionActor.GetOwnerScope(tenant), out var owner)
                    && owner.Revision == original.GetProperty("ownerRevision").GetInt64() && owner.Digest == original.GetProperty("ownerStateDigest").GetString();
            }
            if (operation is "AdmitTransition" or "RecordTransition" or "RecoverTransition" or "VerifyTransition")
            {
                var transition = original.Deserialize<AnchoredStateTransition>(CustodyBindingContract.Json)!;
                allowed &= transition.ScopeId.StartsWith(tenant + ":", StringComparison.Ordinal) && ErasureRootCustody.Hash(transition) == requestDigest;
            }
        }
        else if (method == "erc-source-proof" && caller.Role is "erc-owner" or "erc-source")
        {
            string capturedDigest;
            if (operation == "VerifyOccurrenceReservation") { capturedDigest = ErasureRootCustody.Hash(original.Deserialize<InteractionOccurrenceRequest>(CustodyBindingContract.Json)); }
            else if (operation == "VerifyOriginalSealedOutput")
            { capturedDigest = ErasureRootCustody.Hash(new { original = original.GetProperty("original").Deserialize<InteractionOccurrenceRecord>(CustodyBindingContract.Json), result = original.GetProperty("result").Deserialize<InteractionOccurrenceSealedResult>(CustodyBindingContract.Json) }); }
            else
            { capturedDigest = ErasureRootCustody.Hash(new { original = original.GetProperty("original").Deserialize<InteractionOccurrenceRecord>(CustodyBindingContract.Json), proofId = original.GetProperty("proofId").GetString(), persisted = original.GetProperty("persisted").GetBoolean() }); }
            allowed = operation switch
            {
                "VerifyOccurrenceReservation" => recovery ? await source.LookupReservationAsync(original.Deserialize<InteractionOccurrenceRequest>(CustodyBindingContract.Json)!, token).ConfigureAwait(false)
                    : await source.VerifyReservationAsync(original.Deserialize<InteractionOccurrenceRequest>(CustodyBindingContract.Json)!, token).ConfigureAwait(false),
                "VerifyPersistedWriter" or "VerifyNeverWritten" => await source.VerifyWriterAsync(original.GetProperty("original").Deserialize<InteractionOccurrenceRecord>(CustodyBindingContract.Json)!,
                    original.GetProperty("proofId").GetString()!, original.GetProperty("persisted").GetBoolean(), token).ConfigureAwait(false),
                "VerifyOriginalSealedOutput" => recovery ? await source.LookupSealedAsync(original.GetProperty("original").Deserialize<InteractionOccurrenceRecord>(CustodyBindingContract.Json)!, original.GetProperty("result").Deserialize<InteractionOccurrenceSealedResult>(CustodyBindingContract.Json)!, token).ConfigureAwait(false)
                    : await source.VerifySealedAsync(original.GetProperty("original").Deserialize<InteractionOccurrenceRecord>(CustodyBindingContract.Json)!, original.GetProperty("result").Deserialize<InteractionOccurrenceSealedResult>(CustodyBindingContract.Json)!, token).ConfigureAwait(false),
                _ => false,
            };
            allowed &= capturedDigest == requestDigest;

        }
        else if (method == "erc-registrar-proof" && caller.Role is "erc-owner" or "erc-registrar")
        {
            allowed = operation switch
            {
                "VerifyRevocationEvidence" => await RevocationAsync(original.Deserialize<DeletionCapabilityRevocationEnvelope>(CustodyBindingContract.Json)!, current, token).ConfigureAwait(false),
                "VerifyActivationEvidence" => await ActivationAsync(original.Deserialize<DeletionReattestationActivation>(CustodyBindingContract.Json)!, token).ConfigureAwait(false),
                "VerifyBlockedReplacementEvidence" => await RevocationAsync(original.Deserialize<DeletionBlockedReplacementReconciliation>(CustodyBindingContract.Json)!.RevocationReceipt.Envelope, current, token).ConfigureAwait(false),
                "VerifyCurrentBlockedReplacement" => await CurrentBlockedReplacementAsync(original.GetProperty("request").Deserialize<DeletionBlockedReplacementReconciliation>(CustodyBindingContract.Json)!,
                    original.GetProperty("guardRevision").GetInt64(), original.GetProperty("guardStateDigest").GetString()!, current, token).ConfigureAwait(false),
                _ => false,
            };
            string capturedDigest = operation switch
            {
                "VerifyRevocationEvidence" => ErasureRootCustody.Hash(original.Deserialize<DeletionCapabilityRevocationEnvelope>(CustodyBindingContract.Json)),
                "VerifyActivationEvidence" => ErasureRootCustody.Hash(original.Deserialize<DeletionReattestationActivation>(CustodyBindingContract.Json)),
                "VerifyBlockedReplacementEvidence" => ErasureRootCustody.Hash(original.Deserialize<DeletionBlockedReplacementReconciliation>(CustodyBindingContract.Json)),
                "VerifyCurrentBlockedReplacement" => ErasureRootCustody.Hash(new { request = original.GetProperty("request").Deserialize<DeletionBlockedReplacementReconciliation>(CustodyBindingContract.Json),
                    guardRevision = original.GetProperty("guardRevision").GetInt64(), guardStateDigest = original.GetProperty("guardStateDigest").GetString() }),
                _ => string.Empty,
            };
            allowed &= capturedDigest == requestDigest;
            if (allowed && operation is "VerifyActivationEvidence" or "VerifyBlockedReplacementEvidence")
            {
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
                    if (read is not { } head) { allowed = false; break; }
                    if (operation == "VerifyActivationEvidence")
                    {
                        var activation = original.Deserialize<DeletionReattestationActivation>(CustodyBindingContract.Json)!;
                        if (await custody.ReadRetainedAsync(head.Head, "ActivationEvidence", head.Head.ActivationEvidence, requestDigest, token).ConfigureAwait(false) is { } prior) { allowed = ErasureRootCustody.Hash(prior) == requestDigest; break; }
                        if (recovery) { allowed = false; break; }
                        head.Head.ActivationEvidence.Add(requestDigest, activation);
                    }
                    else
                    {
                        var replacement = original.Deserialize<DeletionBlockedReplacementReconciliation>(CustodyBindingContract.Json)!;
                        if (await custody.ReadRetainedAsync(head.Head, "BlockedReplacementEvidence", head.Head.BlockedReplacementEvidence, requestDigest, token).ConfigureAwait(false) is { } prior) { allowed = ErasureRootCustody.Hash(prior) == requestDigest; break; }
                        if (recovery) { allowed = false; break; }
                        head.Head.BlockedReplacementEvidence.Add(requestDigest, replacement);
                    }
                    if (await custody.SaveAsync(tenant, head.Version, head.Head, token).ConfigureAwait(false)) { break; }
                    if (attempt == 15) { allowed = false; }
                }
            }
        }
        return Evidence(tenant, identity, operation, requestDigest, allowed, current);
    }

    private async Task<bool> RevocationAsync(DeletionCapabilityRevocationEnvelope envelope, IndependentAuthorityState state, CancellationToken token)
    {
        if (state.ValidUntil <= clock.GetUtcNow()) { return false; }
        var read = await custody.ReadAsync(envelope.TenantId, token).ConfigureAwait(false);
        if (read is not { } head || await custody.ReadRetainedAsync(head.Head, "RevocationOriginals", head.Head.RevocationOriginals, ErasureRootCustody.Hash(envelope), token).ConfigureAwait(false) is not { } original || original.Envelope != envelope) { return false; }
        var verified = await revocations.VerifyAsync(envelope, original.SignedEvidence, token).ConfigureAwait(false);
        return verified?.Envelope == envelope && verified.ObservedAt <= clock.GetUtcNow() && verified.ValidUntil > clock.GetUtcNow();
    }
    private ErasureRootPermanentLossReceipt? PermanentLoss(IndependentAuthorityState current, string tenant, string storeId, string? enrollmentId)
    {
        foreach (var entry in current.QualificationCases ?? [])
        {
            if (!entry.Key.StartsWith("erc.permanent-loss:", StringComparison.Ordinal)) { continue; }
            ErasureRootPermanentLossReceipt? original;
            try { original = entry.Value.Deserialize<ErasureRootPermanentLossReceipt>(CustodyBindingContract.Json); }
            catch (JsonException) { continue; }
            if (original is not null && original.StoreId == storeId && original.TenantId == tenant && (enrollmentId is null || original.EnrollmentId == enrollmentId)
                && entry.Key == "erc.permanent-loss:" + original.StoreId + ":" + original.EnrollmentId && !string.IsNullOrWhiteSpace(original.ReceiptId)
                && !string.IsNullOrWhiteSpace(original.RetiredVolumeIdentity) && string.Equals(original.CopyPolicySha256, acceptedCopyPolicySha256, StringComparison.OrdinalIgnoreCase)
                && current.QualificationBindings is { } bindings && bindings.TryGetValue("erc.copyPolicySha256", out var policy) && policy.Equals(acceptedCopyPolicySha256, StringComparison.OrdinalIgnoreCase)
                && bindings.TryGetValue("erc.volume." + storeId, out var volume) && volume == original.RetiredVolumeIdentity) { return original; }
        }
        return null;
    }
    private async Task<bool> ActivationAsync(DeletionReattestationActivation activation, CancellationToken token)
    {
        using var ledger = await OwnerAsync(activation.Replacement.Capability.TenantId, token).ConfigureAwait(false);
        if (ledger is null) { return false; }
        if (ledger.RootElement.GetProperty("Operations").EnumerateArray().Any(operation => operation.TryGetProperty("ActivationOriginal", out var original)
            && original.ValueKind == JsonValueKind.Object && ErasureRootCustody.Hash(original) == ErasureRootCustody.Hash(activation))) { return true; }
        return ledger.RootElement.GetProperty("Batches").EnumerateArray().Any(batch => batch.GetProperty("Current").GetProperty("Capability").GetProperty("BatchId").GetString() == activation.Replacement.Capability.BatchId
            && batch.GetProperty("Outcome").Deserialize<DeletionConsumptionOutcome>() is { Status: DeletionConsumptionStatus.ConsumptionBlocked, BlockReason: DeletionConsumptionBlockReason.CapabilityKeyCompromise } outcome
            && outcome.ReceiptId == activation.CompromiseBlockReceiptId && outcome.KeyBlockSetRevision <= activation.ExpectedKeyBlockSetRevision);
    }
    private async Task<ErasureRootGuardAttempt?> RecoverGuardAttemptAsync(string tenant, string intent, ErasureRootGuardAdmission admission, CancellationToken token)
    {
        if (admission.Transition.TenantId != tenant || ErasureRootCustody.Hash(new { Transition = admission.Transition, TargetMutationDigest = ErasureRootCustody.Hash(admission.Targets) }) != intent) { return null; }
        var original = await guard.LookupOriginalAsync(admission.Transition, admission.Targets, token).ConfigureAwait(false);
        if (original is null || original.IntentDigest != intent || original.OperationId != admission.Transition.OperationId || original.GuardHighWater < admission.BeforeRevision) { return null; }
        var captured = new ErasureRootGuardAttempt(admission.Transition, ErasureRootCustody.Hash(admission.Targets), admission.BeforeRevision, original.GuardHighWater, original);
        for (int attempt = 0; attempt < 16; attempt++)
        {
            var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
            if (read is not { } head || await custody.ReadGuardAdmissionAsync(tenant, head.Head, intent, token).ConfigureAwait(false) is not { } retained || ErasureRootCustody.Hash(retained) != ErasureRootCustody.Hash(admission)) { return null; }
            if (await custody.ReadGuardAttemptAsync(tenant, head.Head, intent, token).ConfigureAwait(false) is { } prior) { return prior.Receipt == original ? prior : null; }
            head.Head.GuardAttempts.Add(intent, captured);
            if (await custody.SaveAsync(tenant, head.Version, head.Head, token).ConfigureAwait(false)) { return captured; }
        }
        return null;
    }
    private async Task<bool> CurrentGuardAsync(string tenant, long revision, string digest, CancellationToken token)
    { var current = await guard.ReadAsync(tenant, token).ConfigureAwait(false); return current?.Revision == revision && ErasureRootCustody.Hash(current) == digest; }
    private async Task<bool> CurrentBlockedReplacementAsync(DeletionBlockedReplacementReconciliation request, long revision, string digest,
        IndependentAuthorityState independent, CancellationToken token)
    {
        if (!await CurrentGuardAsync(request.Capability.TenantId, revision, digest, token).ConfigureAwait(false)
            || !await RevocationAsync(request.RevocationReceipt.Envelope, independent, token).ConfigureAwait(false)) { return false; }
        if (request.UnissuedProof is { } unissued)
        {
            var read = await custody.ReadAsync(request.Capability.TenantId, token).ConfigureAwait(false);
            if (read is not { } head || unissued.CurrentGuardRevision != revision || unissued.CurrentGuardStateDigest != digest
                || await custody.ReadGuardAttemptAsync(request.Capability.TenantId, head.Head, unissued.Refusal.IntentDigest, token).ConfigureAwait(false) is not { } retained
                || ErasureRootCustody.Hash(retained.Transition) != ErasureRootCustody.Hash(unissued.Transition)
                || ErasureRootCustody.Hash(retained.Receipt) != ErasureRootCustody.Hash(unissued.Refusal)
                || retained.TargetMutationDigest != unissued.TargetMutationDigest) { return false; }
        }
        using var owner = await OwnerAsync(request.Capability.TenantId, token).ConfigureAwait(false);
        if (owner is null || owner.RootElement.GetProperty("KeyBlockSetRevision").GetInt64() != request.ExpectedKeyBlockSetRevision
            || !owner.RootElement.GetProperty("Revocations").EnumerateArray().Any(value => ErasureRootCustody.Hash(value.Deserialize<DeletionCapabilityRevocationReceipt>()) == ErasureRootCustody.Hash(request.RevocationReceipt))) { return false; }
        return owner.RootElement.GetProperty("Batches").EnumerateArray().Any(batch =>
            batch.GetProperty("Current").GetProperty("Capability").GetProperty("BatchId").GetString() == request.Capability.BatchId
            && batch.GetProperty("Outcome").Deserialize<DeletionConsumptionOutcome>() is { Status: DeletionConsumptionStatus.ConsumptionBlocked, BlockReason: DeletionConsumptionBlockReason.CapabilityKeyCompromise } outcome
            && outcome.ReceiptId == request.CompromiseBlockReceiptId && outcome.KeyBlockSetRevision <= request.ExpectedKeyBlockSetRevision);
    }
    private async Task<JsonDocument?> OwnerAsync(string tenant, CancellationToken token)
    {
        var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
        if (read is not { } current || !current.Head.Anchors.TryGetValue(DeletionConsumptionActor.GetOwnerScope(tenant), out var anchor)) { return null; }
        var original = anchor.Completed.Values.SingleOrDefault(value => value.TargetRevision == anchor.Revision && value.TargetDigest == anchor.Digest);
        return original is null ? null : JsonDocument.Parse(original.TargetBytes);
    }
    private async Task<bool> ProtocolEvidenceAsync(AnchoredStateTransition transition, IndependentAuthorityState current, CancellationToken token)
    {
        if (Convert.ToHexString(SHA256.HashData(transition.TargetBytes)) != transition.TargetDigest) { return false; }
        string[] parts = Encoding.UTF8.GetString(transition.TargetBytes).Split('\n');
        if (parts.Length != 5 || parts[0] is not ("admission-block" or "revocation" or "activation" or "blocked-replacement")) { return false; }
        var state = await guard.ReadAsync(parts[1], token).ConfigureAwait(false);
        if (state is null || state.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) != parts[3]) { return false; }
        if (parts[0] == "admission-block") { return state.Receipts.Any(value => value.ReceiptId == parts[4] && value.ReferenceId == parts[2] && value.Status == "Committed"); }
        var read = await custody.ReadAsync(parts[1], token).ConfigureAwait(false);
        if (read is not { } head) { return false; }
        if (parts[0] == "revocation")
        {
            var original = head.Head.RevocationOriginals.Values.SingleOrDefault(value => value.Envelope.EventIdentity == parts[2] && value.Envelope.SignatureDigest == parts[4])
                ?? await new ErasureRootOccurrenceIndex(custody.Store, custody.Installation, parts[1]).ReadAsync<ErasureRootRevocationOriginal>(head.Head.LifetimeIndexRoot,
                    "revocation-reference:" + ErasureRootCustody.Hash(new { EventIdentity = parts[2], SignatureDigest = parts[4] }), token).ConfigureAwait(false);
            return original is not null && await RevocationAsync(original.Envelope, current, token).ConfigureAwait(false);
        }
        if (parts[0] == "activation")
        { return await custody.ReadRetainedAsync(head.Head, "ActivationEvidence", head.Head.ActivationEvidence, parts[4], token).ConfigureAwait(false) is { } activation && activation.Replacement.Capability.BatchId == parts[2]
            && await ActivationAsync(activation, token).ConfigureAwait(false); }
        return await custody.ReadRetainedAsync(head.Head, "BlockedReplacementEvidence", head.Head.BlockedReplacementEvidence, parts[4], token).ConfigureAwait(false) is { } replacement && replacement.Capability.BatchId == parts[2]
            && await CurrentBlockedReplacementAsync(replacement, state.Revision, ErasureRootCustody.Hash(state), current, token).ConfigureAwait(false);
    }
    private static ErasureRootPermissionEvidence Evidence(string tenant, string identity, string operation, string digest, bool permitted, IndependentAuthorityState current)
        => new(tenant, identity, operation, digest, "erc-current:" + current.TrustRevision + ":" + digest, permitted);
    private static string Tenant(JsonElement payload, string method)
    {
        // Nested native identities are authority. A caller label must agree with every reached identity.
        var tenants = new HashSet<string>(StringComparer.Ordinal);
        void Visit(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in value.EnumerateObject())
                {
                    if (property.Name is "TenantId" or "tenantId")
                    {
                        string? tenant = property.Value.GetString();
                        if (string.IsNullOrWhiteSpace(tenant)) { throw new InvalidOperationException("Exact tenant required."); }
                        tenants.Add(tenant);
                    }
                    else { Visit(property.Value); }
                }
            }
            else if (value.ValueKind == JsonValueKind.Array) { foreach (var item in value.EnumerateArray()) { Visit(item); } }
        }
        Visit(payload);
        if (method == "erc-current-protocol-evidence")
        {
            var transition = payload.GetProperty("transition").Deserialize<AnchoredStateTransition>(CustodyBindingContract.Json)!;
            string[] parts = Encoding.UTF8.GetString(transition.TargetBytes).Split('\n');
            if (parts.Length != 5 || string.IsNullOrWhiteSpace(parts[1]) || transition.ScopeId != "published-trust|" + parts[0] + "|" + parts[1] + "|" + parts[2])
            { throw new InvalidOperationException("Exact protocol tenant required."); }
            tenants.Add(parts[1]);
        }
        if (tenants.Count != 1) { throw new InvalidOperationException("Contradictory tenant identity."); }
        string result = tenants.Single();
        void CheckScopes(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in value.EnumerateObject())
                {
                    if (property.Name == "ScopeId" && !property.Value.GetString()!.StartsWith(result + ":", StringComparison.Ordinal)
                        && !(method == "erc-current-protocol-evidence" && property.Value.GetString() == payload.GetProperty("transition").GetProperty("ScopeId").GetString()))
                    { throw new InvalidOperationException("Foreign owner scope."); }
                    CheckScopes(property.Value);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array) { foreach (var item in value.EnumerateArray()) { CheckScopes(item); } }
        }
        CheckScopes(payload);
        return result;
    }
}
