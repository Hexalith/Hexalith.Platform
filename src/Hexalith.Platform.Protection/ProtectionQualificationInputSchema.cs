using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Validates the entire recipe's installed shapes and ordered future references without executing any operation.</summary>
internal static class ProtectionQualificationInputSchema
{
    internal static void Validate(ProtectionQualificationActionRecipe recipe, JsonObject request, IReadOnlySet<string> prior)
        => Validate(recipe, request, prior.ToDictionary(id => id, _ => (JsonNode?)null, StringComparer.Ordinal));
    internal static void Validate(ProtectionQualificationActionRecipe recipe, JsonObject request, IReadOnlyDictionary<string, JsonNode?> prior,
        IReadOnlyDictionary<string, ProtectionQualificationNativeCallKind>? inheritedPromises = null)
    {
        var available = new HashSet<string>(prior.Keys, StringComparer.Ordinal);
        var promises = inheritedPromises is null ? new Dictionary<string, ProtectionQualificationNativeCallKind>(StringComparer.Ordinal)
            : new Dictionary<string, ProtectionQualificationNativeCallKind>(inheritedPromises, StringComparer.Ordinal);
        void TypedShape(JsonNode node, Type type) => Shape(node, type, (reference, destination) =>
        {
            if (reference["$concat"] is JsonArray pieces)
            { foreach (var piece in pieces) { TypedShape(piece ?? throw new InvalidOperationException("Missing concatenation piece."), typeof(string)); } return destination == typeof(string); }
            if (reference["$codec"] is JsonObject codec)
            {
                var input = Required(codec, "input");
                switch (codec["name"]!.GetValue<string>())
                {
                    case "TargetManifestDigest": TypedShape(input, typeof(ProtectionTarget[])); break;
                    case "PredicateDigest": TypedShape(input, typeof(GovernanceScopeV1)); break;
                    case "SigningRequestId": TypedShape(input, typeof(DeletionBatchCapabilityV1)); break;
                    case "SignedArtifactDigest": TypedShape(input, typeof(string)); break;
                    case "DestructionSealId":
                        var seal = input.AsObject(); TypedShape(Required(seal, "tenantId"), typeof(string)); TypedShape(Required(seal, "requestId"), typeof(string)); TypedShape(Required(seal, "scope"), typeof(GovernanceScopeV1)); break;
                    case "BatchId":
                        var batch = input.AsObject(); TypedShape(Required(batch, "requestId"), typeof(string)); TypedShape(Required(batch, "sealId"), typeof(string)); TypedShape(Required(batch, "batch"), typeof(GovernanceBatchCommand)); break;
                    case "CompromiseBlockReceiptId":
                        var block = input.AsObject(); TypedShape(Required(block, "receiptId"), typeof(string)); TypedShape(Required(block, "batchId"), typeof(string)); break;
                    case "IssueOperationId":
                        var issue = input.AsObject(); TypedShape(Required(issue, "signingRequestId"), typeof(string)); TypedShape(Required(issue, "detachedJwsDigest"), typeof(string)); break;
                    default: throw new InvalidOperationException("Unknown native codec.");
                }
                return destination == typeof(string);
            }
            if (reference["$original"] is JsonObject original)
            {
                string id = original["id"]!.GetValue<string>();
                if (!promises.TryGetValue(id, out var kind))
                {
                    if (prior.TryGetValue(id, out var retained) && retained is not null)
                    { TypedShape(ProtectionQualificationPersistedObserver.Pointer(retained, original["pointer"]!.GetValue<string>()) ?? throw new InvalidOperationException("Missing exact prior native member."), destination); }
                    return true;
                }
                Type actual = ResultPointerType(kind, original["pointer"]!.GetValue<string>());
                return destination.IsAssignableFrom(actual);
            }
            return false;
        });
        foreach (var call in recipe.Before.Concat([recipe.Effect]).Concat(recipe.Race is { } race ? [race] : []).Concat(recipe.After))
        {
            References(call.Payload, available, request, promises);
            var payload = (JsonObject)ResolveRequest(call.Payload, request)!;
            switch (call.Kind)
            {
                case ProtectionQualificationNativeCallKind.SealEvent:
                case ProtectionQualificationNativeCallKind.SealSnapshot:
                    if (payload.ContainsKey("Entries")) { TypedShape(payload, typeof(ProtectionQualificationNativeSnapshot)); }
                    else { TypedShape(payload, typeof(ProtectionQualificationNativeSeal)); }
                    break;
                case ProtectionQualificationNativeCallKind.WrapFixtureRoot: TypedShape(payload, typeof(ProtectionQualificationNativeRoot)); break;
                case ProtectionQualificationNativeCallKind.UnsealEvent:
                case ProtectionQualificationNativeCallKind.ReplayEvent:
                case ProtectionQualificationNativeCallKind.ReplayCopy:
                case ProtectionQualificationNativeCallKind.Backup:
                case ProtectionQualificationNativeCallKind.Restore: TypedShape(payload, typeof(ProtectionQualificationNativeCell)); break;
                case ProtectionQualificationNativeCallKind.ReplaySnapshot: TypedShape(payload, typeof(ProtectionQualificationNativeSnapshotRead)); break;
                case ProtectionQualificationNativeCallKind.AuthorizeLifecycle: TypedShape(payload, typeof(CustodyKeyLifecycleRequest)); break;
                case ProtectionQualificationNativeCallKind.Lifecycle:
                    TypedShape(Required(payload, "registration"), typeof(CustodyKeyRegistration)); TypedShape(Required(payload, "request"), typeof(CustodyKeyLifecycleRequest)); break;
                case ProtectionQualificationNativeCallKind.Guard:
                case ProtectionQualificationNativeCallKind.GuardOwnersEffective:
                case ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch:
                    TypedShape(Required(payload, "transition"), typeof(GovernanceGuardTransition)); TypedShape(Required(payload, "targets"), typeof(GuardedStateMutation[]));
                    ValidateGuardUnion(payload["transition"]!.AsObject(), call.Kind, TypedShape); break;
                case ProtectionQualificationNativeCallKind.Register:
                case ProtectionQualificationNativeCallKind.RegisterIfDispatched:
                case ProtectionQualificationNativeCallKind.Reserve:
                case ProtectionQualificationNativeCallKind.ReserveIfDispatched:
                case ProtectionQualificationNativeCallKind.RejectSubstitutions: TypedShape(payload, typeof(DeletionBatchConsumptionRequest)); break;
                case ProtectionQualificationNativeCallKind.RejectForeignRoot: TypedShape(payload, typeof(CustodyKeyRegistration)); break;
                case ProtectionQualificationNativeCallKind.Block:
                case ProtectionQualificationNativeCallKind.LookupBlock: TypedShape(payload, typeof(DeletionBatchBlockRequest)); break;
                case ProtectionQualificationNativeCallKind.Activate:
                case ProtectionQualificationNativeCallKind.LookupActivation: TypedShape(payload, typeof(DeletionReattestationActivation)); break;
                case ProtectionQualificationNativeCallKind.ReconcileBlockedReplacement: TypedShape(payload, typeof(DeletionBlockedReplacementReconciliation)); break;
                case ProtectionQualificationNativeCallKind.Sign:
                case ProtectionQualificationNativeCallKind.ObsoleteUnissued: TypedShape(payload, typeof(DeletionBatchCapabilityV1)); break;
                case ProtectionQualificationNativeCallKind.Compromise:
                    TypedShape(Required(payload, "envelope"), typeof(DeletionCapabilityRevocationEnvelope)); TypedShape(Required(payload, "signedEvidence"), typeof(string));
                    if (payload["signedEvidence"] is JsonValue evidence && string.IsNullOrWhiteSpace(evidence.GetValue<string>())) { throw new InvalidOperationException("Exact signed compromise evidence required."); }
                    break;
                case ProtectionQualificationNativeCallKind.Lookup: TypedShape(Required(payload, "tenantId"), typeof(string)); TypedShape(Required(payload, "batchId"), typeof(string)); break;
                case ProtectionQualificationNativeCallKind.LookupProvider:
                    TypedShape(Required(payload, "request"), typeof(DeletionBatchConsumptionRequest)); break;
                case ProtectionQualificationNativeCallKind.Fault:
                    foreach (string field in new[] { "stepId", "boundary" }) { TypedShape(Required(payload, field), typeof(string)); }
                    if (payload["boundary"] is JsonValue boundary && boundary.GetValue<string>() is not ("before" or "after")) { throw new InvalidOperationException("Exact fault boundary required."); }
                    break;
                default: throw new InvalidOperationException("Unsupported native opcode.");
            }
            ValidateRequiredIdentifiers(call.Kind, (JsonObject)ResolveAvailable(payload, prior, request)!);
            available.Add(call.Id); promises.Add(call.Id, call.Kind);
        }
    }
    private static JsonNode? ResolveAvailable(JsonNode? node, IReadOnlyDictionary<string, JsonNode?> prior, JsonObject request)
    {
        if (node is JsonObject obj)
        {
            if (obj["$original"] is JsonObject original)
            {
                string id = original["id"]!.GetValue<string>();
                return prior.TryGetValue(id, out var value) && value is not null
                    ? ProtectionQualificationPersistedObserver.Pointer(value, original["pointer"]!.GetValue<string>())?.DeepClone() : obj.DeepClone();
            }
            var result = new JsonObject(); foreach (var item in obj) { result.Add(item.Key, ResolveAvailable(item.Value, prior, request)); }
            if (IsReference(result) && !result.Any(item => ContainsReference(item.Value)))
            { return ProtectionQualificationRecipeBinding.Bind(new("schema-codec", ProtectionQualificationNativeCallKind.Lookup, new() { ["value"] = result }), request, prior).Payload["value"]?.DeepClone(); }
            return result;
        }
        return node is JsonArray array ? new JsonArray(array.Select(value => ResolveAvailable(value, prior, request)).ToArray()) : node?.DeepClone();
    }
    private static void ValidateRequiredIdentifiers(ProtectionQualificationNativeCallKind kind, JsonObject payload)
    {
        if (IsReference(payload)) { return; }
        void Text(JsonObject value, params string[] fields)
        {
            foreach (string field in fields)
            {
                var node = Required(value, field);
                if (node is JsonValue scalar && (!scalar.TryGetValue<string>(out string? text) || string.IsNullOrWhiteSpace(text) || text.Length > 2048
                    || !ValidUnicode(text)))
                { throw new InvalidOperationException("Required native identifier unavailable: " + field); }
            }
        }
        if (kind is ProtectionQualificationNativeCallKind.Guard or ProtectionQualificationNativeCallKind.GuardOwnersEffective or ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch)
        {
            var transition = Required(payload, "transition").AsObject(); Text(transition, "TenantId", "OperationId");
            var operation = (GovernanceGuardOperation)Required(transition, "Operation").GetValue<int>();
            if (operation is not (GovernanceGuardOperation.InstallEpoch or GovernanceGuardOperation.InstallRepairFence or GovernanceGuardOperation.RecordBridgeDrain
                or GovernanceGuardOperation.ActivateSuccessor or GovernanceGuardOperation.AppendWrite or GovernanceGuardOperation.RegisterHold or GovernanceGuardOperation.ReleaseHold or GovernanceGuardOperation.RecordKeyCompromise))
            { Text(transition, "DeletionRequestId"); }
            if (transition["Hold"] is JsonObject hold)
            {
                Text(hold, "HoldId", "DispositionVersion"); if (operation == GovernanceGuardOperation.ReleaseHold) { Text(hold, "ReleaseReceiptId"); }
                if (!ContainsReference(hold["Scope"]))
                { _ = GovernanceScopeGuardReducer.PredicateDigest(hold["Scope"]!.Deserialize<GovernanceScopeV1>()!); }
            }
            if (transition["Scope"] is { } scope && !ContainsReference(scope)) { _ = GovernanceScopeGuardReducer.PredicateDigest(scope.Deserialize<GovernanceScopeV1>()!); }
            if (transition["Batch"] is JsonObject batch) { Text(batch, "BatchId", "BatchKind", "ManifestDigest"); }
            if (transition["Ordinal"] is JsonObject ordinal)
            {
                if (operation is GovernanceGuardOperation.AuthorizeContentBinding or GovernanceGuardOperation.CommitContentBinding) { Text(ordinal, "GlobalCutId", "TokenId"); }
                if (operation == GovernanceGuardOperation.RecordOwnerCycleEffective && kind != ProtectionQualificationNativeCallKind.GuardOwnersEffective) { Text(ordinal, "OwnerId", "ObligationDigest"); }
                if (operation == GovernanceGuardOperation.RecordViolation) { Text(ordinal, "ViolationId", "ViolationKind", "ResourceId"); }
            }
            if (!ContainsReference(transition) && kind != ProtectionQualificationNativeCallKind.GuardOwnersEffective)
            {
                var command = transition.Deserialize<GovernanceGuardTransition>()!;
                // The compare revision is freshly derived by PrepareCall; validate every installed protocol field now.
                if (!GovernanceScopeGuardReducer.IsClosedCommand(command with { ExpectedGuardRevision = 1 })) { throw new InvalidOperationException("Closed native guard protocol input required."); }
            }
        }
        else if (kind is ProtectionQualificationNativeCallKind.UnsealEvent or ProtectionQualificationNativeCallKind.ReplayEvent or ProtectionQualificationNativeCallKind.ReplayCopy or ProtectionQualificationNativeCallKind.Backup or ProtectionQualificationNativeCallKind.Restore)
        { Text(payload, "Component", "Key"); }
        else if (kind is ProtectionQualificationNativeCallKind.Lookup) { Text(payload, "tenantId", "batchId"); }
        else if (kind is ProtectionQualificationNativeCallKind.Fault) { Text(payload, "stepId", "boundary"); }
        else if (kind is ProtectionQualificationNativeCallKind.Sign or ProtectionQualificationNativeCallKind.ObsoleteUnissued)
        { Text(payload, "Issuer", "Audience", "TenantId", "DeletionRequestId", "DestructionSealId", "BatchKind", "BatchId", "ManifestDigest", "GuardStreamId", "CapabilityKeyVersion"); }
    }
    private static bool ValidUnicode(string value)
    { try { _ = new System.Text.UTF8Encoding(false, true).GetByteCount(value); return true; } catch (System.Text.EncoderFallbackException) { return false; } }
    private static bool ContainsReference(JsonNode? node)
        => node is JsonObject obj ? IsReference(obj) || obj.Any(item => ContainsReference(item.Value)) : node is JsonArray array && array.Any(ContainsReference);
    internal static JsonObject InstallationRequest(string tenant)
        => new() { ["operationId"] = "preflight-only", ["fixtureId"] = "preflight-only", ["caseId"] = "P01", ["action"] = "Preflight", ["bindingDigest"] = new string('a', 64),
            ["otherTenantReference"] = "preflight-other", ["targets"] = System.Text.Json.JsonSerializer.SerializeToNode(new[] { new ProtectionTarget(tenant, "preflight-interaction-1", "qualification"), new ProtectionTarget(tenant, "preflight-interaction-2", "qualification") }) };
    internal static void ValidateInstallation(ProtectionQualificationServiceScope scope, IReadOnlySet<string>? allowedComponents = null, IReadOnlySet<string>? allowedFaultOperations = null)
    {
        var prior = new Dictionary<string, JsonNode?>(StringComparer.Ordinal); var promises = new Dictionary<string, ProtectionQualificationNativeCallKind>(StringComparer.Ordinal);
        var request = InstallationRequest(scope.TenantId);
        foreach (var ownerCase in scope.Recipes)
        {
            request["caseId"] = ownerCase.Key;
            foreach (var action in ownerCase.Value)
            {
                request["action"] = action.Key; Validate(action.Value, request, prior, promises);
                foreach (var call in action.Value.Before.Concat([action.Value.Effect]).Concat(action.Value.Race is { } race ? [race] : []).Concat(action.Value.After))
                {
                    var payload = ResolveRequest(call.Payload, request);
                    void Cells(JsonNode? node)
                    {
                        if (node is JsonObject obj)
                        {
                            if (allowedComponents is not null && obj["Component"] is JsonValue component && obj["Key"] is not null && obj["Metadata"] is not null
                                && !allowedComponents.Contains(component.GetValue<string>())) { throw new InvalidOperationException("Installed native source component unavailable."); }
                            foreach (var item in obj) { Cells(item.Value); }
                        }
                        else if (node is JsonArray values) { foreach (var item in values) { Cells(item); } }
                    }
                    Cells(payload);
                    if (call.Kind == ProtectionQualificationNativeCallKind.Fault && allowedFaultOperations is not null
                        && (payload?["stepId"] is not JsonValue fault || !allowedFaultOperations.Contains(fault.GetValue<string>())))
                    { throw new InvalidOperationException("Installed fault operation unavailable."); }
                    prior.Add(call.Id, null); promises.Add(call.Id, call.Kind);
                }
            }
            ProtectionQualificationPersistedObserver.ValidateCopyLocations(scope.Observations[ownerCase.Key].Bind(request).Sources);
        }
    }
    private static void ValidateGuardUnion(JsonObject transition, ProtectionQualificationNativeCallKind kind, Action<JsonNode, Type> shape)
    {
        var operation = (GovernanceGuardOperation)Required(transition, "Operation").GetValue<int>();
        if (kind == ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch && operation != GovernanceGuardOperation.RegisterHold
            || kind == ProtectionQualificationNativeCallKind.GuardOwnersEffective && operation != GovernanceGuardOperation.RecordOwnerCycleEffective)
        { throw new InvalidOperationException("Source-owned guard union mismatch."); }
        void Field(string name, bool required)
        {
            if (name == "Ordinal" && kind == ProtectionQualificationNativeCallKind.GuardOwnersEffective) { return; }
            if ((transition[name] is not null) != required) { throw new InvalidOperationException("Exact operation-specific guard input required: " + name); }
        }
        Field("Write", operation == GovernanceGuardOperation.AppendWrite); Field("Repair", operation == GovernanceGuardOperation.InstallRepairFence);
        Field("Ordinal", operation is GovernanceGuardOperation.RecordViolation or GovernanceGuardOperation.RecordOwnerCycleEffective or GovernanceGuardOperation.AuthorizeContentBinding or GovernanceGuardOperation.CommitContentBinding);
        Field("Hold", operation is GovernanceGuardOperation.RegisterHold or GovernanceGuardOperation.ReleaseHold);
        Field("Scope", operation is GovernanceGuardOperation.AuthorizeAdmissionFence or GovernanceGuardOperation.CommitAdmissionFence);
        Field("Batch", operation is GovernanceGuardOperation.AuthorizeDestructionStart or GovernanceGuardOperation.CommitDestructionStart or GovernanceGuardOperation.AuthorizeContainment or GovernanceGuardOperation.CommitContainment
            or GovernanceGuardOperation.RecordBatchIssued or GovernanceGuardOperation.RecordBatchIssuanceStale or GovernanceGuardOperation.AuthorizeDispatch or GovernanceGuardOperation.CommitDispatch
            or GovernanceGuardOperation.RecordKeyCompromise or GovernanceGuardOperation.ReplaceAttestation or GovernanceGuardOperation.RecordProtectionOutcome);
        Field("RevocationReceipt", operation == GovernanceGuardOperation.RecordKeyCompromise);
        if (operation is GovernanceGuardOperation.RecordBatchIssued or GovernanceGuardOperation.RecordBatchIssuanceStale or GovernanceGuardOperation.ReplaceAttestation
            or GovernanceGuardOperation.AuthorizeDispatch or GovernanceGuardOperation.CommitDispatch or GovernanceGuardOperation.RecordProtectionOutcome)
        {
            var batch = Required(transition, "Batch").AsObject();
            shape(Required(batch, "Capability"), typeof(DeletionBatchCapabilityV1));
            foreach (string field in new[] { "DetachedJws", "SigningRequestId", "AttestationDigest" })
            {
                var value = Required(batch, field); shape(value, typeof(string));
                if (value is JsonValue scalar && string.IsNullOrWhiteSpace(scalar.GetValue<string>())) { throw new InvalidOperationException("Complete signed guard batch required."); }
            }
        }
    }
    private static JsonNode? ResolveRequest(JsonNode? node, JsonObject request)
    {
        if (node is JsonObject obj)
        {
            if (obj.Count == 1 && obj["$request"] is JsonValue path)
            { return ProtectionQualificationPersistedObserver.Pointer(request, path.GetValue<string>())?.DeepClone(); }
            var value = new JsonObject(); foreach (var item in obj) { value.Add(item.Key, ResolveRequest(item.Value, request)); } return value;
        }
        if (node is JsonArray array) { return new JsonArray(array.Select(item => ResolveRequest(item, request)).ToArray()); }
        return node?.DeepClone();
    }
    private static JsonNode Required(JsonObject value, string name)
        => value[name] ?? throw new InvalidOperationException("Complete native input schema required: " + name);
    private static bool IsReference(JsonNode node) => node is JsonObject obj && obj.Count == 1 && obj.First().Key.StartsWith('$');
    private static void References(JsonNode? node, IReadOnlySet<string> available, JsonObject request, IReadOnlyDictionary<string, ProtectionQualificationNativeCallKind> promises)
    {
        if (node is JsonObject obj)
        {
            if (obj.Any(item => item.Key.StartsWith('$')))
            {
                if (obj.Count != 1) { throw new InvalidOperationException("Exact whole-node reference required."); }
                if (obj["$original"] is JsonObject original && original.Count == 2)
                {
                    string id = original["id"]!.GetValue<string>(), pointer = original["pointer"]!.GetValue<string>();
                    if (!available.Contains(id) || pointer.Length > 2048 || pointer.Length != 0 && !pointer.StartsWith('/')) { throw new InvalidOperationException("Earlier exact native original reference required."); }
                    if (promises.TryGetValue(id, out var kind)) { _ = ResultPointerType(kind, pointer); }
                    return;
                }
                if (obj["$request"] is JsonValue path)
                {
                    string pointer = path.GetValue<string>();
                    if (pointer == "/canaryBase64" || pointer.StartsWith("/canaryBase64/", StringComparison.Ordinal)) { throw new InvalidOperationException("Plaintext cannot become a native original."); }
                    _ = ProtectionQualificationPersistedObserver.Pointer(request, pointer) ?? throw new InvalidOperationException("Missing request reference."); return;
                }
                if (obj["$concat"] is JsonArray pieces && pieces.Count > 0) { foreach (var piece in pieces) { References(piece, available, request, promises); } return; }
                if (obj["$codec"] is JsonObject codec && codec.Count == 2 && codec["name"] is JsonValue codecName
                    && codecName.GetValue<string>() is "TargetManifestDigest" or "PredicateDigest" or "DestructionSealId" or "BatchId" or "SigningRequestId" or "SignedArtifactDigest" or "CompromiseBlockReceiptId" or "IssueOperationId")
                { References(Required(codec, "input"), available, request, promises); return; }
                throw new InvalidOperationException("Malformed native reference.");
            }
            foreach (var item in obj) { References(item.Value, available, request, promises); }
        }
        else if (node is JsonArray array) { foreach (var item in array) { References(item, available, request, promises); } }
    }
    private static Type ResultPointerType(ProtectionQualificationNativeCallKind kind, string pointer)
    {
        string[] parts = pointer.Length == 0 ? [] : pointer[1..].Split('/');
        Type? type = kind switch
        {
            ProtectionQualificationNativeCallKind.Sign or ProtectionQualificationNativeCallKind.ObsoleteUnissued => typeof(DeletionCapabilitySigningOutcome),
            ProtectionQualificationNativeCallKind.Guard or ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch => typeof(GovernanceProtocolReceipt),
            ProtectionQualificationNativeCallKind.Register or ProtectionQualificationNativeCallKind.RegisterIfDispatched or ProtectionQualificationNativeCallKind.Reserve
                or ProtectionQualificationNativeCallKind.ReserveIfDispatched or ProtectionQualificationNativeCallKind.Lookup or ProtectionQualificationNativeCallKind.Block
                or ProtectionQualificationNativeCallKind.Activate or ProtectionQualificationNativeCallKind.LookupBlock or ProtectionQualificationNativeCallKind.LookupActivation => typeof(DeletionConsumptionOutcome),
            ProtectionQualificationNativeCallKind.LookupProvider => typeof(DeletionManifestProviderResult),
            ProtectionQualificationNativeCallKind.WrapFixtureRoot => typeof(CustodyKeyRegistration),
            ProtectionQualificationNativeCallKind.Lifecycle => typeof(CustodyKeyLifecycleOutcome),
            ProtectionQualificationNativeCallKind.Compromise when parts.Length > 0 && parts[0] == "receipt" => typeof(DeletionCapabilityRevocationReceipt),
            _ => null,
        };
        if (kind == ProtectionQualificationNativeCallKind.Compromise && parts.Length > 0 && parts[0] == "receipt") { parts = parts[1..]; }
        if (type is null) { throw new InvalidOperationException("Unpromised native result reference."); }
        foreach (string encoded in parts)
        {
            string part = encoded.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsArray || type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
            {
                if (!int.TryParse(part, out int index) || index < 0 || index >= 1000) { throw new InvalidOperationException("Bounded native result index required."); }
                type = type.IsArray ? type.GetElementType()! : type.GetGenericArguments().Single(); continue;
            }
            type = type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance)?.PropertyType
                ?? throw new InvalidOperationException("Unknown promised native result member.");
        }
        return type;
    }
    private static void Shape(JsonNode node, Type type, Func<JsonObject, Type, bool>? validateReference = null)
    {
        if (IsReference(node))
        { if (validateReference is not null && !validateReference((JsonObject)node, Nullable.GetUnderlyingType(type) ?? type)) { throw new InvalidOperationException("Native reference result type does not match its destination."); } return; }
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string)) { _ = node.GetValue<string>(); return; }
        if (type == typeof(bool)) { _ = node.GetValue<bool>(); return; }
        if (type.IsEnum) { if (!Enum.IsDefined(type, node.GetValue<int>())) { throw new InvalidOperationException("Unknown native enum."); } return; }
        if (type == typeof(int)) { _ = node.GetValue<int>(); return; }
        if (type == typeof(long)) { _ = node.GetValue<long>(); return; }
        if (type == typeof(ulong)) { _ = node.GetValue<ulong>(); return; }
        if (type == typeof(uint)) { _ = node.GetValue<uint>(); return; }
        if (type == typeof(DateTimeOffset)) { _ = node.GetValue<DateTimeOffset>(); return; }
        if (type == typeof(DateTime)) { _ = node.GetValue<DateTime>(); return; }
        if (type == typeof(byte[])) { _ = Convert.FromBase64String(node.GetValue<string>()); return; }
        if (type.IsArray)
        { if (node is not JsonArray array) { throw new InvalidOperationException("Native array required."); } foreach (var item in array) { Shape(item ?? throw new InvalidOperationException("Null native entry."), type.GetElementType()!, validateReference); } return; }
        if (type == typeof(JsonObject) || type == typeof(JsonNode) || type == typeof(System.Text.Json.JsonElement) || type == typeof(object)) { return; }
        if (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            var args = type.GetGenericArguments();
            if (args.Length == 1 && node is JsonArray values) { foreach (var item in values) { Shape(item ?? throw new InvalidOperationException("Null native entry."), args[0], validateReference); } return; }
            if (args.Length == 2 && args[0] == typeof(string) && node is JsonObject dictionary) { foreach (var item in dictionary) { Shape(item.Value ?? throw new InvalidOperationException("Null native value."), args[1], validateReference); } return; }
            throw new InvalidOperationException("Native collection required.");
        }
        if (node is not JsonObject obj) { throw new InvalidOperationException("Native object required: " + type.Name); }
        var constructors = type.GetConstructors();
        var constructor = constructors.OrderByDescending(value => value.GetParameters().Length).FirstOrDefault();
        if (constructor is null) { return; }
        var nullability = new NullabilityInfoContext();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>()?.Condition != System.Text.Json.Serialization.JsonIgnoreCondition.Always).ToArray();
        var matched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in constructor.GetParameters())
        {
            var property = properties.SingleOrDefault(value => value.Name.Equals(parameter.Name, StringComparison.OrdinalIgnoreCase));
            string name = property?.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()?.Name ?? property?.Name ?? parameter.Name!;
            var pair = obj.FirstOrDefault(value => value.Key == name || type == typeof(ProtectionQualificationNativeSeal) && value.Key == char.ToLowerInvariant(name[0]) + name[1..]);
            if (pair.Value is null)
            {
                if (!parameter.HasDefaultValue && nullability.Create(parameter).ReadState != NullabilityState.Nullable)
                { throw new InvalidOperationException("Missing required native field: " + name); }
                if (pair.Key is not null) { matched.Add(pair.Key); } continue;
            }
            matched.Add(pair.Key); Shape(pair.Value, parameter.ParameterType, validateReference);
        }
        foreach (var item in obj.Where(item => !matched.Contains(item.Key)))
        {
            var property = properties.SingleOrDefault(value => (value.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()?.Name ?? value.Name) == item.Key
                || type == typeof(ProtectionQualificationNativeSeal) && char.ToLowerInvariant(value.Name[0]) + value.Name[1..] == item.Key);
            if (property is null) { throw new InvalidOperationException("Unknown native input field: " + item.Key); }
            if (item.Value is not null) { Shape(item.Value, property.PropertyType, validateReference); }
            else if (nullability.Create(property).ReadState != NullabilityState.Nullable) { throw new InvalidOperationException("Null native input field: " + item.Key); }
        }
    }
}
