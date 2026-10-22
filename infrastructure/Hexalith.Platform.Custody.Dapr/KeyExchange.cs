using System.Security.Cryptography;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;
internal sealed record KeyHolder(string CopyId, string Role, string ProcessIncarnation, string TransportIncarnation, string OwnerId, string AdmissionOriginalDigest);
internal sealed record KeyAdmission(CustodyBindingKeyUseV1 KeyUse, string RequestDigest, string QualifiedProfileDigest, KeyHolder[] Holders);

internal sealed class KeyExchange(ComponentProfile profile, IAuthorityStore store, IEpochOperations epochs, IKeyAccess keys, StatementVerifier verifier)
{
    internal async Task<CustodyBindingResponseV1> ExecuteAsync(CustodyBindingRequestV1 request, CurrentState current, SignedStatement evidence, CancellationToken token)
    {
        var e = request.Envelope; var keyUse = request.WrapInput?.KeyUse ?? request.KeyUse;
        var admission = CustodyJson.Read<KeyAdmission>(CustodyJson.Bytes(evidence.Payload));
        string[] roles = ["caller", "sdk", "sidecar", "uds", "grpc", "tls", "component", "native"];
        if (keyUse is null || current.Checkpoint.Frozen || !verifier.Qualified(current)
            || !verifier.Verify(evidence, "independent-observer", current, CustodyJson.Hash(CustodyJson.Bytes(admission)), TimeSpan.FromSeconds(30))
            || admission.KeyUse != keyUse || admission.RequestDigest != e.RequestDigest || admission.QualifiedProfileDigest != profile.ProfileDigest
            || !roles.All(role => admission.Holders.Any(h => h.Role == role))
            || admission.Holders.Select(h => h.CopyId).Distinct(StringComparer.Ordinal).Count() != admission.Holders.Length
            || admission.Holders.Any(h => !CustodyBindingContract.Digest(h.AdmissionOriginalDigest) || string.IsNullOrWhiteSpace(h.OwnerId)
                || string.IsNullOrWhiteSpace(h.ProcessIncarnation) || h.TransportIncarnation != keyUse.TransportIncarnation)
            || !admission.Holders.Any(h => h.Role == "caller" && h.ProcessIncarnation == keyUse.RecipientProcessIncarnation))
            throw new AuthorityUnavailableException("Exact pre-admitted key copy/holder inventory missing.");
        string deliveryId = keyUse.KeyUseLeaseId + ":" + keyUse.DeliveryAttemptId;
        if (current.Checkpoint.Deliveries.ContainsKey(deliveryId))
            return new(CustodyBindingContract.Protocol, e.OperationId, e.RequestDigest); // Permanent release admission is status-only forever.
        var next = SemanticProvider.Clone(current);
        next.Deliveries.Add(deliveryId, new(keyUse, CustodyBindingIntentPhase.SendCommitted, false, false, false, false));
        var admissionOriginal = await store.PutImmutableAsync(CustodyJson.Bytes(evidence), "journal", token);
        string intentId = "key-" + CustodyJson.Hash(System.Text.Encoding.UTF8.GetBytes(deliveryId));
        next.NativeResults.Add(intentId, admissionOriginal);
        if (e.Operation == "wrap" && request.WrapInput is { } input)
        {
            if (input.ExpectedRevision != current.Head.Revision || next.Registrations.Count >= 1000
                || next.AllocationExclusions.Contains(CustodyJson.Interaction(input.Identity))
                || next.Registrations.ContainsKey(input.OpaqueWrappedKeyReference))
                throw new AuthorityUnavailableException("Original allocation is excluded or capacity exhausted.");
            // Burn both interaction and target before native execution, even when the resulting registration is lost.
            next.AllocationExclusions.Add(CustodyJson.Interaction(input.Identity)); next.AllocationExclusions.Add(CustodyJson.Target(input.Identity));
            next.Originals.Add(e.OperationId, new(e.RequestDigest, "wrap", null, null));
            next.NativeIntents.Add(intentId, new(e.TenantId, e.OperationId, intentId, e.RequestDigest, keyUse.RecipientProcessIncarnation,
                next.CurrentEpoch, next.CurrentEpoch, e.Proof.TargetVectorDigest, current.Head.Revision, CustodyBindingIntentPhase.SendCommitted));
            var admitted = await store.CommitAsync(current, next, token);
            byte[] kek = await keys.ReadTenantKekAsync(e.TenantId, input.KekVersion, token);
            try
            {
                var registration = await KeyEnvelopeBuilder.WrapAsync(input, kek, admitted.Checkpoint.CurrentEpoch, epochs, token);
                var response = new CustodyBindingResponseV1(CustodyBindingContract.Protocol, e.OperationId, e.RequestDigest, WrappedRegistration: registration.Registration);
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
        await store.CommitAsync(current, next, token); // No raw response is ever stored after this permanent release admission.
        byte[] outerKey = [], tenantKek = [];
        try
        {
            outerKey = await keys.ReadOuterKeyAsync(stored, token);
            tenantKek = await keys.ReadTenantKekAsync(e.TenantId, stored.Registration.KekVersion, token);
            return new(CustodyBindingContract.Protocol, e.OperationId, e.RequestDigest,
                KeyDelivery: new(keyUse, KeyEnvelopeBuilder.Unwrap(stored, outerKey, tenantKek)));
        }
        finally { CryptographicOperations.ZeroMemory(outerKey); CryptographicOperations.ZeroMemory(tenantKek); }
    }
}
