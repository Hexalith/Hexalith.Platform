using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

// Public structural protocol shared with qualify.py. This validates owner observations, never client scenario expectations.
internal static class QualificationProtocolSchema
{
    private static readonly JsonObject CaseSchema = LoadCaseSchema();
    internal static string[] MetricNames(string id)=>((JsonArray)Case(id)!["metrics"]!).Select(value=>value!.GetValue<string>()).ToArray();
    internal static string[] CaseIds=>((JsonArray)CaseSchema["cases"]!).Select(value=>value!["id"]!.GetValue<string>()).ToArray();
    internal static string PlanSha256 => CaseSchema["planSha256"]!.GetValue<string>();
    private static JsonObject LoadCaseSchema()
    {
        using Stream stream = typeof(QualificationProtocolSchema).Assembly.GetManifestResourceStream("ExtSecretsPublicCaseSchemas")!;
        return (JsonObject)JsonNode.Parse(stream)!;
    }

    private static JsonObject? Case(string? id) => ((JsonArray)CaseSchema["cases"]!).OfType<JsonObject>().FirstOrDefault(item => SecretsProof.SameText(item, "id", id));

    private const string Common = "protocol runId targetCommit isolatedTargetId planSha256 requestId identity version observedAt validUntil";
    private const string Execution = "caseId caseTargetId outcome resultSha256";
    private const string Enrollment = "targetClass productionMutationAllowed executorIdentity executorVersion observerIdentity observerVersion qualifiedReferencesSha256 completeCaseIds caseTargetIds caseScopes currentProfileSha256 currentProfileStatus";
    private const string Observation = "metrics snapshots linearization proofReferences trace retainedReceipts";
    private const string StateFields = "requestId outcome receiptId tenantId logicalId sealId batchId manifestDigest attestationOrdinal keyVersion status targetIds destroyedTargetIds pinnedTargetIds activeCredentialIds providerCopyIds erasedCopyIds journalRevision deliveredAt exclusiveExpiry replayNamespace replayTenant nonceDigest firstSeenAt retainUntil actualIssuedGuardRevision attempts replayIssuer timing rotation attestationHistory";
    private const string ReceiptFields = "kind position receiptId keyVersion guardRevision blockSetRevision occurredAt";
    private const string TraceFields = "requestId tenantId batchId originalReceiptId stateSha256";
    private const string ScopeFields = "tenantId targetIds providerCopyIds logicalId sealId batchId manifestDigest initialAttestationOrdinal initialKeyVersion successorKeyVersion exclusiveExpiry issuer nonceDigest";

    internal static bool ValidRequest(string role, string operation, JsonObject request)
    {
        const string common = "protocol runId targetCommit isolatedTargetId planSha256 requestId";
        if (role is not ("executor" or "observer" or "enrollment")
            || operation != (role switch { "observer" => "observe", "enrollment" => "enroll", _ => operation })
            || role == "executor" && operation is not ("execute" or "lookup")
            || !Exact(request, common + (role == "enrollment" ? " operation" : " caseId caseTargetId enrolledScope scenario"))
            || !SecretsProof.SameText(request, "protocol", "EXT-SECRETS-1.qualification.v1")
            || !SecretsProof.SameText(request, "planSha256", PlanSha256)
            || !Texts(request, "runId isolatedTargetId") || !Hashes(request, "requestId")
            || SecretsProof.Text(request, "targetCommit") is not { Length: 40 } commit || !commit.All(char.IsAsciiHexDigitLower)) { return false; }
        var identity = (JsonObject)request.DeepClone();
        identity.Remove("requestId");
        if (!SecretsProof.SameText(request, "requestId", Digest(identity))) { return false; }
        if (role == "enrollment") { return SecretsProof.SameText(request, "operation", "enroll"); }
        if (!Hashes(request, "caseTargetId") || Case(SecretsProof.Text(request, "caseId")) is not { } schema
            || request["enrolledScope"] is not JsonObject scope || !ValidScope(scope)
            || !JsonNode.DeepEquals(scope["logicalId"], request["caseTargetId"])
            || request["scenario"] is not JsonObject scenario || !Exact(scenario, "outcomes expected boundary persistedStages")
            || !JsonNode.DeepEquals(scenario["outcomes"], schema["outcomes"])
            || !JsonNode.DeepEquals(scenario["boundary"], schema["boundary"])
            || scenario["expected"] is not JsonObject expected
            || !expected.Select(item => item.Key).ToHashSet(StringComparer.Ordinal).SetEquals(((JsonArray)schema["metrics"]!).Select(item => item!.GetValue<string>()))
            || expected.Any(item => !Integer(item.Value)) || !JsonNode.DeepEquals(expected,schema["expected"])
            || scenario["persistedStages"] is not JsonArray stages || !stages.Select(item => item?.ToString()).SequenceEqual(new[] { "AfterEffect", "AfterRestart", "AfterRestore", "AfterLookup" })) { return false; }
        return true;
    }

    internal static bool ValidOwnerRequest(string role,JsonObject request)
    {
        return ValidRequest(role,role switch { "executor"=>"execute","observer"=>"observe",_=>"enroll" },request);
    }

    private static bool ValidScope(JsonObject scope)
        => Exact(scope, ScopeFields) && Texts(scope, "tenantId sealId batchId initialKeyVersion successorKeyVersion issuer")
            && Hashes(scope, "logicalId manifestDigest nonceDigest") && Integers(scope, "initialAttestationOrdinal exclusiveExpiry", minimum: 1)
            && Strings(scope["targetIds"], hash: true, minimum: 1) && Strings(scope["providerCopyIds"], hash: true);

    internal static string Digest(JsonNode node) => Convert.ToHexString(SHA256.HashData(QualificationProofVerifier.CanonicalBytes(node))).ToLowerInvariant();

    internal static bool Valid(string role, JsonObject payload, JsonObject request)
    {
        if (!Exact(payload, Common + " " + (role == "enrollment" ? Enrollment : Execution + (role == "observer" ? " " + Observation : "")))
            || role is not ("executor" or "observer" or "enrollment")
            || !Texts(payload, "protocol runId targetCommit isolatedTargetId identity version")
            || !Hashes(payload, "planSha256 requestId") || !SecretsProof.SameText(payload, "planSha256", PlanSha256) || !Integers(payload, "observedAt validUntil")) { return false; }
        if (role == "enrollment") { return ValidEnrollment(payload); }
        if (!Texts(payload, "caseId outcome") || !Hashes(payload, "caseTargetId resultSha256")
            || Case(SecretsProof.Text(payload, "caseId")) is not { } schema || !((JsonArray)schema["outcomes"]!).Any(value => JsonNode.DeepEquals(value, payload["outcome"]))) { return false; }
        return role == "executor" || ValidObservation(payload, request);
    }

    private static bool ValidEnrollment(JsonObject payload)
    {
        if (!Texts(payload, "executorIdentity executorVersion observerIdentity observerVersion")
            || !Hashes(payload, "qualifiedReferencesSha256 currentProfileSha256")
            || !SecretsProof.SameText(payload, "targetClass", "IsolatedQualification")
            || !SecretsProof.SameText(payload, "currentProfileStatus", "Current")
            || SecretsProof.Flag(payload, "productionMutationAllowed") is not false
            || !Strings(payload["completeCaseIds"], hash: false, minimum: 234) || payload["completeCaseIds"] is not JsonArray cases || cases.Count != 234
            || payload["caseTargetIds"] is not JsonObject targets || payload["caseScopes"] is not JsonObject scopes
            || targets.Count != cases.Count || scopes.Count != cases.Count
            || !cases.Select(node => node!.GetValue<string>()).SequenceEqual(((JsonArray)CaseSchema["cases"]!).Select(node => node!["id"]!.GetValue<string>()))) { return false; }
        var allTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonNode? node in cases)
        {
            string name = node!.GetValue<string>();
            if (!Hash(targets[name]) || scopes[name] is not JsonObject scope || !Exact(scope, ScopeFields)
                || !Texts(scope, "tenantId sealId batchId initialKeyVersion successorKeyVersion issuer") || !Hashes(scope, "logicalId manifestDigest nonceDigest")
                || !Integers(scope, "initialAttestationOrdinal exclusiveExpiry", minimum: 1)
                || !JsonNode.DeepEquals(scope["logicalId"], targets[name])
                || !Strings(scope["targetIds"], hash: true, minimum: 1) || !Strings(scope["providerCopyIds"], hash: true)) { return false; }
            foreach (JsonNode? target in (JsonArray)scope["targetIds"]!)
            { if (!allTargets.Add(target!.GetValue<string>())) { return false; } }
        }
        return targets.Select(p => p.Value!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count() == cases.Count;
    }

    private static bool ValidObservation(JsonObject payload, JsonObject request)
    {
        if (Case(SecretsProof.Text(payload, "caseId")) is not { } schema
            || payload["metrics"] is not JsonObject metrics || !metrics.Select(item => item.Key).ToHashSet(StringComparer.Ordinal).SetEquals(((JsonArray)schema["metrics"]!).Select(item => item!.GetValue<string>()))
            || metrics.Any(p => !Integer(p.Value))
            || !Strings(payload["proofReferences"], hash: true, minimum: 4)
            || payload["snapshots"] is not JsonArray snapshots || snapshots.Count != 4
            || payload["linearization"] is not JsonObject order
            || (SecretsProof.SameText(schema, "boundary", "none") ? order.Count != 0
                : !Exact(order, "boundary triggerPosition effectPosition winner") || !SecretsProof.SameText(order, "boundary", SecretsProof.Text(schema, "boundary"))
                    || SecretsProof.Text(order, "winner") is not ("Effect" or "Trigger") || !Integers(order, "triggerPosition effectPosition"))
            || payload["trace"] is not JsonArray trace || trace.Count is < 4 or > 100
            || payload["retainedReceipts"] is not JsonArray receipts || receipts.Count != trace.Count) { return false; }
        string[] stages = ["AfterEffect", "AfterRestart", "AfterRestore", "AfterLookup"];
        for (int i = 0; i < snapshots.Count; i++)
        {
            if (snapshots[i] is not JsonObject snapshot || !Exact(snapshot, "stage originalRequestId originalResultSha256 stateSha256 authorityRevision state")
                || !SecretsProof.SameText(snapshot, "stage", stages[i]) || !Texts(snapshot, "authorityRevision")
                || !Hashes(snapshot, "originalRequestId originalResultSha256 stateSha256")
                || !JsonNode.DeepEquals(snapshot["originalRequestId"], payload["requestId"])
                || !JsonNode.DeepEquals(snapshot["originalResultSha256"], payload["resultSha256"])
                || snapshot["state"] is not JsonObject state || !ValidState(state)
                || !SecretsProof.SameText(snapshot, "stateSha256", Digest(state))
                || !JsonNode.DeepEquals(state["requestId"], payload["requestId"])
                || !JsonNode.DeepEquals(state["outcome"], payload["outcome"]) || !JsonNode.DeepEquals(state["status"], payload["outcome"])
                || !JsonNode.DeepEquals(state["receiptId"], payload["resultSha256"]) || !JsonNode.DeepEquals(state["logicalId"], payload["caseTargetId"])
                || request["enrolledScope"] is not JsonObject scope || !JsonNode.DeepEquals(state["tenantId"], scope["tenantId"])
                || !JsonNode.DeepEquals(state["targetIds"], scope["targetIds"]) || !JsonNode.DeepEquals(state["providerCopyIds"], scope["providerCopyIds"])
                || i > 0 && !JsonNode.DeepEquals(state, snapshots[0]!["state"])) { return false; }
        }
        for (int i = 0; i < trace.Count; i++)
        {
            if (trace[i] is not JsonObject item || !Exact(item, ReceiptFields + " " + TraceFields) || !ValidReceipt(item)
                || !Hashes(item, "requestId originalReceiptId stateSha256") || !Texts(item, "tenantId") || !NullableText(item["batchId"])
                || !JsonNode.DeepEquals(item["requestId"], payload["requestId"]) || !JsonNode.DeepEquals(item["originalReceiptId"], payload["resultSha256"])
                || !JsonNode.DeepEquals(item["stateSha256"], snapshots[0]!["stateSha256"])
                || !JsonNode.DeepEquals(item["tenantId"], snapshots[0]!["state"]!["tenantId"])
                || !JsonNode.DeepEquals(item["batchId"], snapshots[0]!["state"]!["batchId"])
                || receipts[i] is not JsonObject receipt || !Exact(receipt, ReceiptFields) || !ValidReceipt(receipt)
                || receipt.Any(p => !JsonNode.DeepEquals(p.Value, item[p.Key]))) { return false; }
        }
        return true;
    }

    private static bool ValidReceipt(JsonObject item)
        => Texts(item, "kind") && ReceiptKinds.Contains(SecretsProof.Text(item, "kind")!) && Hashes(item, "receiptId") && NullableText(item["keyVersion"])
            && Integers(item, "position guardRevision blockSetRevision occurredAt");

    private static bool ValidState(JsonObject state)
    {
        if (!Exact(state, StateFields) || !Texts(state, "outcome tenantId logicalId status") || !Hashes(state, "requestId receiptId")
            || !Integers(state, "journalRevision", minimum: 1)) { return false; }
        foreach (string field in Words("sealId batchId keyVersion replayNamespace replayTenant replayIssuer"))
        { if (!NullableText(state[field])) { return false; } }
        foreach (string field in Words("manifestDigest nonceDigest")) { if (state[field] is not null && !Hash(state[field])) { return false; } }
        foreach (string field in Words("attestationOrdinal deliveredAt exclusiveExpiry firstSeenAt retainUntil actualIssuedGuardRevision"))
        { if (state[field] is not null && !Integer(state[field])) { return false; } }
        foreach (string field in Words("targetIds destroyedTargetIds pinnedTargetIds activeCredentialIds providerCopyIds erasedCopyIds"))
        { if (!Strings(state[field], hash: true)) { return false; } }
        if (state["timing"] is not JsonObject timing || !Exact(timing, "checkedAt exclusiveExpiry releasedAt authorityChangedAt rotatedAt overlapEndsAt")
            || timing.Any(p => p.Value is not null && !Integer(p.Value))) { return false; }
        if (state["rotation"] is not null && (state["rotation"] is not JsonObject rotation
            || !Exact(rotation, "previousKeyVersion currentKeyVersion rotatedAt oldVerifyUntil publicAnchorId publicAnchorVersion")
            || !Texts(rotation, "previousKeyVersion currentKeyVersion publicAnchorId publicAnchorVersion") || !Integers(rotation, "rotatedAt oldVerifyUntil"))) { return false; }
        if (state["attempts"] is not JsonArray attempts || state["attestationHistory"] is not JsonArray history) { return false; }
        foreach (JsonNode? node in attempts)
        {
            if (node is not JsonObject attempt || !Exact(attempt, "sealId batchId manifestDigest attestationOrdinal signingAttemptOrdinal intendedGuardRevision actualIssuedGuardRevision keyVersion requestId outcome noIssueProofSha256")
                || !Texts(attempt, "sealId batchId keyVersion outcome") || !Hashes(attempt, "manifestDigest requestId")
                || !Integers(attempt, "attestationOrdinal signingAttemptOrdinal intendedGuardRevision", minimum: 1)
                || attempt["actualIssuedGuardRevision"] is not null && !Integer(attempt["actualIssuedGuardRevision"])
                || attempt["noIssueProofSha256"] is not null && !Hash(attempt["noIssueProofSha256"])) { return false; }
        }
        foreach (JsonNode? node in history)
        {
            if (node is not JsonObject item || !Exact(item, "ordinal keyVersion credentialId status sealId batchId manifestDigest blockReceiptId dispatchReceiptId")
                || !Texts(item, "keyVersion status sealId batchId") || !Hashes(item, "credentialId manifestDigest blockReceiptId dispatchReceiptId") || !Integers(item, "ordinal", minimum: 1)) { return false; }
        }
        return true;
    }

    private static bool Exact(JsonObject value, string fields) => value.Select(p => p.Key).ToHashSet(StringComparer.Ordinal).SetEquals(Words(fields));
    private static string[] Words(string fields) => fields.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    private static readonly HashSet<string> ReceiptKinds = Words("OriginalPersisted Effect Lookup Restart Restore Block Reserve Consume Activate ActivationBlocked Dispatch Hold Compromise Complete Sign NoIssue Issue Expiry Verify Release DenyRelease ActorRevocation Destroy GuardMutation").ToHashSet(StringComparer.Ordinal);
    private static bool Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out string? text)
        && text.Length is > 0 and <= 2048 && text.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or ':' or '/' or '@' or '-');
    private static bool NullableText(JsonNode? node) => node is null || Text(node);
    private static bool Hash(JsonNode? node) => Text(node) && node!.GetValue<string>() is { Length: 64 } text && text.All(char.IsAsciiHexDigitLower);
    private static bool Integer(JsonNode? node, long minimum = 0) => node is JsonValue value && value.TryGetValue<long>(out long number) && number >= minimum && number <= 9007199254740991;
    private static bool Texts(JsonObject value, string fields) => Words(fields).All(field => Text(value[field]));
    private static bool Hashes(JsonObject value, string fields) => Words(fields).All(field => Hash(value[field]));
    private static bool Integers(JsonObject value, string fields, long minimum = 0) => Words(fields).All(field => Integer(value[field], minimum));
    private static bool Strings(JsonNode? node, bool hash, int minimum = 0)
        => node is JsonArray array && array.Count >= minimum && array.All(item => hash ? Hash(item) : Text(item))
            && array.Select(item => item!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count() == array.Count;
}
