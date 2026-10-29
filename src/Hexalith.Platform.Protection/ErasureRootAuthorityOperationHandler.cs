using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Owned private permission/source/guard/registrar receiving methods. Every decision uses the current authenticated grant and actual retained native originals.</summary>
public sealed class ErasureRootAuthorityOperationHandler(ErasureRootCustody custody, ErasureRootNativeOccurrenceSource source,
    IGovernanceScopeGuard guard, IDeletionCapabilityRevocationAuthenticator revocations, string acceptedCopyPolicySha256, TimeProvider clock) : IIndependentOwnerOperationHandler
{
    /// <inheritdoc />
    public IReadOnlySet<string> Methods { get; } = new HashSet<string>(["erc-permission", "erc-occurrence-original", "erc-source-proof", "erc-registrar-proof",
        "erc-current-protocol-evidence", "erc-read-guard", "erc-execute-guard", "erc-read-no-issue"], StringComparer.Ordinal);
    /// <inheritdoc />
    public bool IsReadOnly(string method) => method is not ("erc-execute-guard" or "erc-source-proof");
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
        string tenant = Tenant(payload, method);
        if (caller.ScopeReference != tenant + "|erc") { return null; }
        if (method == "erc-read-guard") { return await guard.ReadAsync(tenant, token).ConfigureAwait(false); }
        if (method == "erc-execute-guard")
        {
            if (caller.Role != "erc-owner") { return null; }
            if (recovery)
            {
                var transition = payload.GetProperty("transition").Deserialize<GovernanceGuardTransition>(CustodyBindingContract.Json)!;
                var targets = payload.GetProperty("targets").Deserialize<GuardedStateMutation[]>(CustodyBindingContract.Json)!;
                string intent = ErasureRootCustody.Hash(new { Transition = transition, TargetMutationDigest = ErasureRootCustody.Hash(targets) });
                var fresh = await guard.ReadAsync(tenant, token).ConfigureAwait(false);
                return fresh?.Receipts.SingleOrDefault(receipt => receipt.OperationId == transition.OperationId && receipt.IntentDigest == intent);
            }
            return await guard.ExecuteAsync(payload.GetProperty("transition").Deserialize<GovernanceGuardTransition>(CustodyBindingContract.Json)!,
                payload.GetProperty("targets").Deserialize<GuardedStateMutation[]>(CustodyBindingContract.Json)!, token).ConfigureAwait(false);
        }
        if (method == "erc-read-no-issue")
        { return await guard.ReadNoIssueAsync(payload.GetProperty("payload").Deserialize<DeletionBatchCapabilityV1>(CustodyBindingContract.Json)!, payload.GetProperty("signingRequestId").GetString()!, payload.GetProperty("detachedJwsDigest").GetString()!, token).ConfigureAwait(false); }
        if (method == "erc-occurrence-original")
        { return await source.ResolveAsync(payload.GetProperty("owner").Deserialize<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(CustodyBindingContract.Json)!, payload.GetProperty("occurrence").Deserialize<PayloadProtectionOccurrenceContext>(CustodyBindingContract.Json)!, token).ConfigureAwait(false); }
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
        }
        return Evidence(tenant, identity, operation, requestDigest, allowed, current);
    }

    private async Task<bool> RevocationAsync(DeletionCapabilityRevocationEnvelope envelope, IndependentAuthorityState state, CancellationToken token)
    {
        foreach (var record in state.Originals.Values)
        {
            var original = record.Request;
            if (!original.TryGetProperty("envelope", out var captured) || !original.TryGetProperty("signedEvidence", out var signature)
                || ErasureRootCustody.Hash(captured) != ErasureRootCustody.Hash(envelope)) { continue; }
            var verified = await revocations.VerifyAsync(envelope, signature.GetString()!, token).ConfigureAwait(false);
            if (verified?.Envelope == envelope && verified.ObservedAt <= clock.GetUtcNow() && verified.ValidUntil > clock.GetUtcNow()) { return true; }
        }
        return false;
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
    private async Task<bool> CurrentGuardAsync(string tenant, long revision, string digest, CancellationToken token)
    { var current = await guard.ReadAsync(tenant, token).ConfigureAwait(false); return current?.Revision == revision && ErasureRootCustody.Hash(current) == digest; }
    private async Task<bool> CurrentBlockedReplacementAsync(DeletionBlockedReplacementReconciliation request, long revision, string digest,
        IndependentAuthorityState independent, CancellationToken token)
    {
        if (!await CurrentGuardAsync(request.Capability.TenantId, revision, digest, token).ConfigureAwait(false)
            || !await RevocationAsync(request.RevocationReceipt.Envelope, independent, token).ConfigureAwait(false)) { return false; }
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
        if (parts[0] == "revocation")
        { return current.Originals.Values.Any(value => value.Request.TryGetProperty("envelope", out var envelope) && envelope.GetProperty("SignatureDigest").GetString() == parts[4]); }
        return current.Originals.Values.Any(value => ErasureRootCustody.Hash(value.Request) == parts[4]);
    }
    private static ErasureRootPermissionEvidence Evidence(string tenant, string identity, string operation, string digest, bool permitted, IndependentAuthorityState current)
        => new(tenant, identity, operation, digest, "erc-current:" + current.TrustRevision + ":" + digest, permitted);
    private static string Tenant(JsonElement payload, string method)
    {
        if (payload.TryGetProperty("tenantId", out var tenant)) { return tenant.GetString()!; }
        if (method == "erc-occurrence-original") { return payload.GetProperty("owner").GetProperty("TenantId").GetString()!; }
        if (method == "erc-execute-guard") { return payload.GetProperty("transition").GetProperty("TenantId").GetString()!; }
        if (method == "erc-read-no-issue") { return payload.GetProperty("payload").GetProperty("TenantId").GetString()!; }
        if (method == "erc-current-protocol-evidence") { return Encoding.UTF8.GetString(payload.GetProperty("transition").GetProperty("TargetBytes").GetBytesFromBase64()).Split('\n')[1]; }
        throw new InvalidOperationException("Exact tenant scope required.");
    }
}
