using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace Hexalith.Platform.Protection;

/// <summary>Actual same-native-head role admission and exact-original journal. Missing native enrollment or current permission grants no operation.</summary>
public sealed class ProtectionQualificationOperationJournal(ErasureRootCustody custody, IErasureRootAuthorization authorization)
{
    internal static string Digest(JsonNode value) => Convert.ToHexStringLower(SHA256.HashData(ProtectionQualificationCanonical.Bytes(value)));
    internal async Task<ErasureRootHead> ReadAsync(string tenant, CancellationToken token)
        => (await custody.ReadAsync(tenant, token).ConfigureAwait(false))?.Head ?? throw new InvalidOperationException("Current native role journal unavailable.");
    internal async Task<bool> UpdateAsync(string tenant, string identity, string method, string digest, Func<ErasureRootHead, bool> change, CancellationToken token)
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (!await authorization.AuthorizeAsync(tenant, identity, method, digest, token).ConfigureAwait(false)) { throw new InvalidOperationException("Current private qualification permission unavailable."); }
            var current = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
            if (current is not { } head) { throw new InvalidOperationException("Native journal unavailable."); }
            string boundary = ErasureRootCustody.Hash(head.Head with { QualificationAttempts = new(StringComparer.Ordinal), QualificationOperations = new(StringComparer.Ordinal) });
            if (!change(head.Head)) { return false; }
            if (head.Head.Pending is not null && boundary != ErasureRootCustody.Hash(head.Head with { QualificationAttempts = new(StringComparer.Ordinal), QualificationOperations = new(StringComparer.Ordinal) }))
            { throw new InvalidOperationException("Pending manifest permits only independently authorized role metadata."); }
            if (await custody.SaveAsync(tenant, head.Version, head.Head, token).ConfigureAwait(false)) { return true; }
        }
        throw new InvalidOperationException("Native qualification admission unresolved.");
    }
    internal static string Id(string role, string operationId) => role + ":" + operationId;
    internal async Task<bool> AdmitCallAsync(string tenant, string role, string operationId, ProtectionQualificationNativeCall call, CancellationToken token)
    {
        return await UpdateAsync(tenant, operationId, "QualificationNativeCall", ErasureRootCustody.Hash(call), head =>
        {
            if (head.Pending is { } pending && !(call.Kind == ProtectionQualificationNativeCallKind.LookupProvider
                && ErasureRootCustody.Hash(call.Payload["request"]!.Deserialize<Hexalith.EventStore.Contracts.Security.DeletionBatchConsumptionRequest>()) == ErasureRootCustody.Hash(pending.Request)
                && call.Payload["reservationReceiptId"]?.GetValue<string>() == pending.ReservationReceiptId
                || call.Kind == ProtectionQualificationNativeCallKind.Lookup
                && call.Payload["tenantId"]?.GetValue<string>() == pending.Request.Capability.TenantId
                && call.Payload["batchId"]?.GetValue<string>() == pending.Request.Capability.BatchId))
            { throw new InvalidOperationException("Only exact original pending-manifest reconciliation may be admitted."); }
            if (!head.QualificationOperations.TryGetValue(Id(role, operationId), out var operation)) { throw new InvalidOperationException("Exact independently registered role original required."); }
            if (operation.Calls.TryGetValue(call.Id, out var prior))
            { if (ErasureRootCustody.Hash(prior.Call) != ErasureRootCustody.Hash(call)) { throw new InvalidOperationException("Changed native call original."); } return false; }
            operation.Calls.Add(call.Id, new(call)); return true;
        }, token).ConfigureAwait(false);
    }
    internal Task RetainCallAsync(string tenant, string role, string operationId, ProtectionQualificationNativeCall call, JsonNode? result, CancellationToken token)
        => UpdateAsync(tenant, operationId, "QualificationNativeResult", ErasureRootCustody.Hash(new { call, result }), head =>
        {
            var operation = head.QualificationOperations[Id(role, operationId)];
            if (!operation.Calls.TryGetValue(call.Id, out var prior) || ErasureRootCustody.Hash(prior.Call) != ErasureRootCustody.Hash(call)) { throw new InvalidOperationException("Exact admitted native call required."); }
            if (prior.Result is not null) { if (!JsonNode.DeepEquals(prior.Result, result)) { throw new InvalidOperationException("Changed native result."); } return false; }
            operation.Calls[call.Id] = prior with { Result = result?.DeepClone() ?? new JsonObject { ["status"] = "Unavailable" } }; return true;
        }, token);
}
