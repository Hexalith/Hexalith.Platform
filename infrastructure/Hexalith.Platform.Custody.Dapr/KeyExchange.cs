using System.Security.Cryptography;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;
internal sealed record KeyHolder(string CopyId, string Role, string ProcessIncarnation, string TransportIncarnation, string OwnerId, string AdmissionOriginalDigest);
internal sealed record KeyAdmission(CustodyBindingKeyUseV1 KeyUse, string RequestDigest, string QualifiedProfileDigest, KeyHolder[] Holders);
internal sealed record HolderCompletion(string CopyId, string ProcessIncarnation, string TransportIncarnation,
    string IoEvidenceDigest, string SanitationEvidenceDigest, string Disposition);
internal sealed record KeyCompletion(CustodyBindingKeyUseV1 KeyUse, string AdmissionOriginalSha256, HolderCompletion[] Holders);
internal sealed record WrapPlan(Hexalith.Platform.Custody.CustodyKeyObjectIdentity Identity, string OperationId, string RequestDigest,
    long ExpectedRevision, string KekVersion, string OpaqueReference, string Epoch);
internal sealed record WrappedResult(StoredRegistration Registration, CustodyBindingResponseV1 Response);

internal sealed class KeyExchange(ComponentProfile profile, IAuthorityStore store, IEpochOperations epochs, IKeyAccess keys, StatementVerifier verifier)
{
    internal async Task<CustodyBindingResponseV1> ExecuteAsync(CustodyBindingRequestV1 request, CurrentState current, SignedStatement evidence, CancellationToken token)
    {
        var e = request.Envelope; var keyUse = request.WrapInput?.KeyUse ?? request.KeyUse;
        if ((request.WrapInput?.Identity ?? request.Registration?.Identity)?.Purpose != Hexalith.Platform.Custody.PlatformKeyPurpose.InteractionRootDek)
            throw new AuthorityUnavailableException("This profile enrolls interaction roots only.");
        var admission = CustodyJson.Read<KeyAdmission>(CustodyJson.Bytes(evidence.Payload));
        string[] roles = ["caller", "sdk", "sidecar", "uds", "grpc", "tls", "component", "native"];
        if (keyUse is null || current.Checkpoint.Frozen || e.ExpectedCustodyRevision != current.Head.Revision || e.CheckpointDigest != current.Head.Digest
            || current.Checkpoint.NativeIntents.Count >= 10000 || current.Checkpoint.Deliveries.Count >= 10000 || !verifier.Qualified(current)
            || !verifier.Verify(evidence, "independent-observer", current, CustodyJson.Hash(CustodyJson.Bytes(admission)), TimeSpan.FromSeconds(30))
            || admission.KeyUse != keyUse || admission.RequestDigest != e.RequestDigest || admission.QualifiedProfileDigest != profile.ProfileDigest
            || !roles.All(role => admission.Holders.Any(h => h.Role == role))
            || admission.Holders.Select(h => h.CopyId).Distinct(StringComparer.Ordinal).Count() != admission.Holders.Length
            || admission.Holders.Any(h => !CustodyBindingContract.Digest(h.AdmissionOriginalDigest) || string.IsNullOrWhiteSpace(h.OwnerId)
                || string.IsNullOrWhiteSpace(h.ProcessIncarnation) || h.TransportIncarnation != keyUse.TransportIncarnation)
            || !admission.Holders.Any(h => h.Role == "caller" && h.ProcessIncarnation == keyUse.RecipientProcessIncarnation))
            throw new AuthorityUnavailableException("Exact pre-admitted key copy/holder inventory missing.");
        string deliveryId = CustodyJson.DeliveryId(keyUse);
        if (current.Checkpoint.Deliveries.TryGetValue(deliveryId, out var retainedDelivery))
        {
            if (retainedDelivery.KeyUse != keyUse) throw new AuthorityUnavailableException("Permanent delivery identity conflict.");
            return new(CustodyBindingContract.Protocol, e.OperationId, e.RequestDigest, Delivery: retainedDelivery);
        }
        var next = SemanticProvider.Clone(current);
        next.Deliveries.Add(deliveryId, new(keyUse, CustodyBindingIntentPhase.SendCommitted, false, false, false, false));
        var admissionOriginal = await store.PutImmutableAsync(CustodyJson.Bytes(evidence), "journal", token);
        string intentId = "key-" + CustodyJson.Hash(System.Text.Encoding.UTF8.GetBytes(deliveryId));
        next.NativeResults.Add(intentId + ":admission", admissionOriginal);
        if (e.Operation == "wrap" && request.WrapInput is { } input)
        {
            if (input.ExpectedRevision != current.Head.Revision || next.Registrations.Count >= 1000
                || next.AllocationExclusions.Contains(CustodyJson.Interaction(input.Identity))
                || next.Registrations.ContainsKey(input.OpaqueWrappedKeyReference))
                throw new AuthorityUnavailableException("Original allocation is excluded or capacity exhausted.");
            // Burn both interaction and target before native execution, even when the resulting registration is lost.
            next.AllocationExclusions.Add(CustodyJson.Interaction(input.Identity)); next.AllocationExclusions.Add(CustodyJson.Target(input.Identity));
            next.Originals.Add(e.OperationId, new(e.RequestDigest, "wrap", null, null));
            var plan = new WrapPlan(input.Identity, e.OperationId, e.RequestDigest, input.ExpectedRevision, input.KekVersion, input.OpaqueWrappedKeyReference, next.CurrentEpoch);
            next.NativeResults.Add(intentId + ":plan", await store.PutImmutableAsync(CustodyJson.Bytes(plan), "journal", token));
            next.NativeIntents.Add(intentId, new(e.TenantId, e.OperationId, intentId, e.RequestDigest, keyUse.RecipientProcessIncarnation,
                next.CurrentEpoch, next.CurrentEpoch, e.Proof.TargetVectorDigest, current.Head.Revision, CustodyBindingIntentPhase.SendCommitted));
            var admitted = await store.CommitAsync(current, next, token);
            byte[] kek = await keys.ReadTenantKekAsync(e.TenantId, input.KekVersion, token);
            try
            {
                var registration = await KeyEnvelopeBuilder.WrapAsync(input, kek, admitted.Checkpoint.CurrentEpoch, epochs, token);
                var response = new CustodyBindingResponseV1(CustodyBindingContract.Protocol, e.OperationId, e.RequestDigest, WrappedRegistration: registration.Registration);
                // The deterministic locator is derived from the permanently retained intent before any send.
                // Object/CAS acknowledgement loss can recover this exact registration, never re-encrypt.
                _ = await store.PutIntentResultAsync(CustodyJson.Bytes(new WrappedResult(registration, response)), CustodyJson.IntentLocator(e.TenantId, intentId), token);
                var original = await store.PutImmutableAsync(CustodyJson.Bytes(response), "original", token);
                var durable = SemanticProvider.Clone(admitted); durable.Registrations.Add(input.OpaqueWrappedKeyReference, registration);
                durable.Originals[e.OperationId] = durable.Originals[e.OperationId] with { Response = original };
                durable.NativeIntents[intentId] = durable.NativeIntents[intentId] with { Phase = CustodyBindingIntentPhase.ResultDurable };
                durable.NativeResults[intentId] = original;
                durable.Deliveries[deliveryId] = durable.Deliveries[deliveryId] with { Phase = CustodyBindingIntentPhase.ResultDurable };
                await store.CommitAsync(admitted, durable, token); return response;
            }
            finally { CryptographicOperations.ZeroMemory(kek); CryptographicOperations.ZeroMemory(input.RootKey); }
        }
        if (e.Operation != "unwrap" || request.Registration is not { } originalRegistration
            || !next.Registrations.TryGetValue(originalRegistration.OpaqueWrappedKeyReference, out var stored)
            || stored.Registration != originalRegistration || stored.Epoch != next.CurrentEpoch)
            throw new AuthorityUnavailableException("Exact live registration unavailable.");
        next.NativeIntents.Add(intentId, new(e.TenantId, e.OperationId, intentId, e.RequestDigest, keyUse.RecipientProcessIncarnation,
            stored.Epoch, keyUse.RecipientProcessIncarnation, e.Proof.TargetVectorDigest, current.Head.Revision, CustodyBindingIntentPhase.SendCommitted));
        var released = await store.CommitAsync(current, next, token); // No raw response is ever stored after this permanent release admission.
        byte[] outerKey = [], tenantKek = [];
        try
        {
            outerKey = await keys.ReadOuterKeyAsync(stored, token);
            tenantKek = await keys.ReadTenantKekAsync(e.TenantId, stored.Registration.KekVersion, token);
            byte[] root = KeyEnvelopeBuilder.Unwrap(stored, outerKey, tenantKek);
            try
            {
                var durable = SemanticProvider.Clone(released);
                var status = new CustodyBindingResponseV1(CustodyBindingContract.Protocol, e.OperationId, e.RequestDigest,
                    Delivery: durable.Deliveries[deliveryId] with { Phase = CustodyBindingIntentPhase.ResultDurable });
                var result = await store.PutImmutableAsync(CustodyJson.Bytes(status), "journal", token);
                durable.NativeResults[intentId] = result;
                durable.NativeIntents[intentId] = durable.NativeIntents[intentId] with { Phase = CustodyBindingIntentPhase.ResultDurable };
                durable.Deliveries[deliveryId] = status.Delivery!;
                await store.CommitAsync(released, durable, token);
                return new(CustodyBindingContract.Protocol, e.OperationId, e.RequestDigest, KeyDelivery: new(keyUse, root));
            }
            catch { CryptographicOperations.ZeroMemory(root); throw; }
        }
        finally { CryptographicOperations.ZeroMemory(outerKey); CryptographicOperations.ZeroMemory(tenantKek); }
    }
    internal async Task RecoverWrapAsync(string tenant, WorkerOperation operation, SignedStatement permission, CancellationToken token)
    {
        var current = await store.ReadAsync(tenant, token);
        if (operation.Operation != "recover-wrap-result" || !verifier.Qualified(current)
            || !verifier.Verify(permission, "private-operation", current, CustodyJson.Hash(CustodyJson.Bytes(operation)), TimeSpan.FromSeconds(30))
            || !current.Checkpoint.NativeIntents.TryGetValue(operation.IntentId, out var intent) || intent.OperationId != operation.OriginalOperationId
            || intent.Phase != CustodyBindingIntentPhase.SendCommitted
            || !current.Checkpoint.NativeResults.TryGetValue(intent.IntentId + ":plan", out var planObject))
            throw new AuthorityUnavailableException("Exact admitted wrap recovery permission missing.");
        var plan = CustodyJson.Read<WrapPlan>(await store.ReadImmutableAsync(planObject, token));
        var resultObject = await store.FindIntentResultAsync(CustodyJson.IntentLocator(tenant, intent.IntentId), token)
            ?? throw new AuthorityUnavailableException("Native wrap result lost; identity stays burned and send is never repeated.");
        var result = CustodyJson.Read<WrappedResult>(await store.ReadImmutableAsync(resultObject, token)); var r = result.Registration.Registration;
        if (r.Identity != plan.Identity || r.OperationId != plan.OperationId || r.ExpectedRevision != plan.ExpectedRevision
            || r.KekVersion != plan.KekVersion || r.OpaqueWrappedKeyReference != plan.OpaqueReference || result.Registration.Epoch != plan.Epoch
            || result.Response.WrappedRegistration != r || result.Response.OperationId != plan.OperationId || result.Response.RequestDigest != plan.RequestDigest)
            throw new AuthorityUnavailableException("Original native result substitution.");
        var next = SemanticProvider.Clone(current);
        next.Registrations.Add(r.OpaqueWrappedKeyReference, result.Registration);
        var response = await store.PutImmutableAsync(CustodyJson.Bytes(result.Response), "original", token);
        next.Originals[plan.OperationId] = next.Originals[plan.OperationId] with { Response = response };
        next.NativeResults[intent.IntentId] = resultObject;
        next.NativeIntents[intent.IntentId] = intent with { Phase = CustodyBindingIntentPhase.ResultDurable };
        await store.CommitAsync(current, next, token);
    }
    internal async Task<CustodyBindingResponseV1> CompleteAsync(CustodyBindingRequestV1 request, CurrentState current,
        SignedStatement evidence, CancellationToken token)
    {
        var e = request.Envelope; var use = request.KeyUse!; string id = CustodyJson.DeliveryId(use);
        string intentId = "key-" + CustodyJson.Hash(System.Text.Encoding.UTF8.GetBytes(id));
        var completion = CustodyJson.Read<KeyCompletion>(CustodyJson.Bytes(evidence.Payload));
        if (e.ExpectedCustodyRevision != current.Head.Revision || e.CheckpointDigest != current.Head.Digest
            || !verifier.Verify(evidence, "independent-observer", current, CustodyJson.Hash(CustodyJson.Bytes(completion)), TimeSpan.FromSeconds(30))
            || completion.KeyUse != use || !current.Checkpoint.Deliveries.TryGetValue(id, out var delivery) || delivery.KeyUse != use
            || !current.Checkpoint.NativeResults.TryGetValue(intentId + ":admission", out var admissionObject)
            || admissionObject.Sha256 != completion.AdmissionOriginalSha256)
            throw new AuthorityUnavailableException("Exact independently observed holder completion missing.");
        if (delivery.Phase == CustodyBindingIntentPhase.Closed)
            return new(CustodyBindingContract.Protocol, e.OperationId, e.RequestDigest, Delivery: delivery);
        var admission = CustodyJson.Read<SignedStatement>(await store.ReadImmutableAsync(admissionObject, token));
        var holders = CustodyJson.Read<KeyAdmission>(CustodyJson.Bytes(admission.Payload)).Holders;
        if (completion.Holders.Length != holders.Length || completion.Holders.Select(h => h.CopyId).Distinct().Count() != holders.Length
            || holders.Any(h => !completion.Holders.Any(c => c.CopyId == h.CopyId && c.ProcessIncarnation == h.ProcessIncarnation
                && c.TransportIncarnation == h.TransportIncarnation && c.Disposition is "VerifiedZeroed" or "IndependentlyTerminated"
                && CustodyBindingContract.Digest(c.IoEvidenceDigest) && CustodyBindingContract.Digest(c.SanitationEvidenceDigest))))
            throw new AuthorityUnavailableException("Incomplete original holder inventory cannot close delivery.");
        var next = SemanticProvider.Clone(current);
        next.Deliveries[id] = delivery with { Phase = CustodyBindingIntentPhase.Closed,
            IoComplete = true, BuffersComplete = true, HolderInventoryComplete = true };
        // Completion can reconcile an uncertain unwrap, but never repairs an unknown native allocation/send.
        var retained = await store.PutImmutableAsync(CustodyJson.Bytes(evidence), "journal", token);
        next.NativeResults[intentId + ":completion"] = retained;
        if (next.NativeIntents.TryGetValue(intentId, out var intent)
            && (intent.Phase == CustodyBindingIntentPhase.ResultDurable || !next.NativeResults.ContainsKey(intentId + ":plan")))
        {
            // A lost unwrap has no persistent raw response to recover. The independently observed
            // exact holder completion is its durable value-free result; no key/native send repeats.
            if (!next.NativeResults.ContainsKey(intentId)) next.NativeResults[intentId] = retained;
            next.NativeIntents[intentId] = intent with { Phase = CustodyBindingIntentPhase.Closed };
        }
        await store.CommitAsync(current, next, token);
        return new(CustodyBindingContract.Protocol, e.OperationId, e.RequestDigest, Delivery: next.Deliveries[id]);
    }
}
