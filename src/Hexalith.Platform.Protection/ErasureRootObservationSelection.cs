using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Read-only exact original hydration selected by authenticated current operation/call identities; unrelated lifetime history is never loaded.</summary>
internal static class ErasureRootObservationSelection
{
    internal static async Task SelectAsync(ErasureRootCustody custody, ErasureRootHead head, IReadOnlySet<string>? operationIds,
        string? caseId, IReadOnlyList<ProtectionTarget> targets, CancellationToken token)
    {
        var selected = head.QualificationOperations.Where(entry => operationIds is not null
            ? operationIds.Contains(entry.Value.SafeRequest["operationId"]?.GetValue<string>() ?? "")
            : entry.Value.SafeRequest["caseId"]?.GetValue<string>() == caseId
                && ErasureRootCustody.Hash(entry.Value.SafeRequest["targets"]) == ErasureRootCustody.Hash(JsonSerializer.SerializeToNode(targets))).ToArray();
        if (operationIds is not null)
        {
            selected = head.QualificationOperations.Where(entry => operationIds.Any(id => entry.Key == ProtectionQualificationOperationJournal.Id("executor", id))).ToArray();
            if (selected.Length != operationIds.Count) { throw new InvalidOperationException("Complete authenticated observation operations required."); }
        }
        var calls = selected.SelectMany(entry => entry.Value.Calls.Values).ToArray();
        head.QualificationOperations.Clear(); foreach (var entry in selected) { head.QualificationOperations.Add(entry.Key, entry.Value); }
        foreach (var call in calls.Where(call => call.Call.Kind is ProtectionQualificationNativeCallKind.Guard or ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch))
        {
            var transition = call.Call.Payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
            var mutations = call.Call.Payload["targets"]!.Deserialize<GuardedStateMutation[]>()!;
            string intent = ErasureRootCustody.Hash(new { Transition = transition, TargetMutationDigest = ErasureRootCustody.Hash(mutations) });
            _ = await custody.ReadGuardAttemptAsync(head.TenantId, head, intent, token).ConfigureAwait(false);
            _ = await custody.ReadGuardAdmissionAsync(head.TenantId, head, intent, token).ConfigureAwait(false);
        }
        // No-issue originals are selected by the exact signing request in these source-owned calls.
        foreach (var call in calls.Where(call => call.Call.Kind == ProtectionQualificationNativeCallKind.Sign))
        {
            foreach (var intent in head.GuardSigningIntents.Where(entry => Contains(call.Call.Payload, entry.Key)).Select(entry => entry.Value))
            { _ = await custody.ReadGuardAttemptAsync(head.TenantId, head, intent, token).ConfigureAwait(false); }
        }
        string scope = DeletionConsumptionActor.GetOwnerScope(head.TenantId);
        if (!head.Anchors.TryGetValue(scope, out var owner)) { return; }
        var current = owner.Completed.Values.SingleOrDefault(value => value.TargetRevision == owner.Revision && value.TargetDigest == owner.Digest);
        if (current is null) { return; }
        using var ledger = JsonDocument.Parse(current.TargetBytes);
        var batchIds = ErasureRootObservationBatches.FromCalls(calls);
        var revisions = new HashSet<long>();
        foreach (var batch in ledger.RootElement.GetProperty("Batches").EnumerateArray())
        {
            if (!batchIds.Contains(batch.GetProperty("Current").GetProperty("Capability").GetProperty("BatchId").GetString()!)) { continue; }
            Collect(batch);
            string batchId = batch.GetProperty("Current").GetProperty("Capability").GetProperty("BatchId").GetString()!;
            _ = await custody.ReadManifestOriginalAsync(head.TenantId, head, batchId, token).ConfigureAwait(false);
        }
        foreach (var call in calls) { if (call.Result is { } result) { using var document = JsonDocument.Parse(result.ToJsonString()); Collect(document.RootElement); } }
        foreach (long revision in revisions.Where(value => value > 0 && value <= owner.Revision).Order())
        {
            var original = await custody.ReadHistoricalAsync(head.TenantId, scope, owner, revision, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Exact operation owner original unavailable.");
            owner.Completed[ErasureRootCustody.Hash(original)] = original;
        }
        void Collect(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in value.EnumerateObject())
                {
                    if (field.Name == "OwnerRevision" && field.Value.TryGetInt64(out long revision))
                    { revisions.Add(revision); if (revision > 1) { revisions.Add(revision - 1); } }
                    else { Collect(field.Value); }
                }
            }
            else if (value.ValueKind == JsonValueKind.Array) { foreach (var item in value.EnumerateArray()) { Collect(item); } }
        }
    }
    private static bool Contains(JsonNode? node, string value)
    {
        if (node is JsonObject obj) { return obj.Any(entry => Contains(entry.Value, value)); }
        if (node is JsonArray array) { return array.Any(entry => Contains(entry, value)); }
        return node is JsonValue scalar && scalar.TryGetValue<string>(out var text) && text == value;
    }
}
