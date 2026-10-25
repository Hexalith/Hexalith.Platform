using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

// Only a denied nonmaterial operation may use this projection. Key/signing/replay/lifecycle fields are inapplicable, not guessed observations.
internal static class QualificationDeniedCaseState
{
    internal static JsonObject Create(JsonObject original,long originalRevision,string originalReceipt)
    {
        var scope=(JsonObject)original["enrolledScope"]!;
        var state=new JsonObject();
        foreach(string name in new[]{"sealId","batchId","manifestDigest","attestationOrdinal","keyVersion","deliveredAt","exclusiveExpiry","replayNamespace","replayTenant","nonceDigest","firstSeenAt","retainUntil","actualIssuedGuardRevision","replayIssuer","rotation"}) state[name]=null;
        foreach(string name in new[]{"destroyedTargetIds","pinnedTargetIds","activeCredentialIds","erasedCopyIds","attempts","attestationHistory"}) state[name]=new JsonArray();
        state["requestId"]=original["requestId"]!.DeepClone();state["outcome"]="Denied";state["status"]="Denied";state["receiptId"]=originalReceipt;
        state["tenantId"]=scope["tenantId"]!.DeepClone();state["logicalId"]=scope["logicalId"]!.DeepClone();state["targetIds"]=scope["targetIds"]!.DeepClone();state["providerCopyIds"]=scope["providerCopyIds"]!.DeepClone();state["journalRevision"]=originalRevision;
        var timing=new JsonObject();foreach(string name in new[]{"checkedAt","exclusiveExpiry","releasedAt","authorityChangedAt","rotatedAt","overlapEndsAt"}) timing[name]=null;state["timing"]=timing;
        return state;
    }
}
