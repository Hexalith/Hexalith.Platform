using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Owned role operation implementation over the actual native action runner, independent persisted observer and surviving admission journal.</summary>
public sealed class ProtectionQualificationRoleService
{
    private readonly ProtectionQualificationServiceScope _scope;
    private readonly ProtectionQualificationOperationJournal _journal;
    private readonly ProtectionQualificationActionRunner? _actions;
    private readonly ProtectionQualificationPersistedObserver? _observer;
    private readonly DaprClient _installationReader;
    private readonly ECDsa _signer;
    private readonly Func<CancellationToken, Task<bool>>? _preflight;
    private readonly Func<CancellationToken, Task<ProtectionQualificationObserverPermission?>>? _observerPermission;
    private readonly SemaphoreSlim _signing = new(1, 1);

    /// <summary>Captures explicit installed dependencies. Observer composition has no action runner and never invokes journal mutations.</summary>
    public ProtectionQualificationRoleService(ProtectionQualificationServiceScope scope, ProtectionQualificationOperationJournal journal,
        ProtectionQualificationActionRunner? actions, ProtectionQualificationPersistedObserver? observer, DaprClient installationReader, ECDsa signer, Func<CancellationToken, Task<bool>>? preflight = null, Func<CancellationToken, Task<ProtectionQualificationObserverPermission?>>? observerPermission = null)
    {
        ArgumentNullException.ThrowIfNull(scope); ArgumentNullException.ThrowIfNull(journal); ArgumentNullException.ThrowIfNull(installationReader); ArgumentNullException.ThrowIfNull(signer);
        if (scope.Role is not ("executor" or "observer" or "enrollment") || scope.BindingDigest is not { Length: 64 }
            || signer.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7" || scope.Role == "executor" && actions is null
            || scope.Role == "observer" && (observer is null || actions is not null)) { throw new ArgumentException("Explicit disjoint native role composition required."); }
        ProtectionQualificationActionCatalog.Validate(scope.Recipes);
        if (!scope.Recipes.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(scope.Observations.Keys)) { throw new ArgumentException("Complete independent 41-case observation recipes required."); }
        if (actions is not null) { actions.Producer = actions.Producer with { RoleIdentity = scope.Identity, BindingDigest = scope.BindingDigest }; }
        _observerPermission = observerPermission; _preflight = preflight; _scope = scope; _journal = journal; _actions = actions; _observer = observer; _installationReader = installationReader; _signer = signer;
    }

    /// <summary>Handles exactly one authenticated request; it never retries an admitted mutation after response loss.</summary>
    public async Task<JsonObject> HandleAsync(JsonObject request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        string id = request["operationId"]!.GetValue<string>(), action = request["action"]!.GetValue<string>();
        if (string.IsNullOrWhiteSpace(id) || id.Length > 200 || request["bindingDigest"]?.GetValue<string>() != _scope.BindingDigest
            || request["role"] is { } supplied && supplied.GetValue<string>() != _scope.Role || !ProtectionQualificationRoleProgram.Allowed(_scope.Role, action))
        { throw new InvalidOperationException("Exact enrolled role request required."); }
        string digest = ProtectionQualificationOperationJournal.Digest(request);
        // Validate all role/installation/request inputs before enrollment or journal metadata is retained.
        if (action is not ("InspectInstallation" or "LookupOriginalOperation" or "ObservePersistedState" or "LookupAttemptOperations"))
        {
            _ = await InspectAsync(token).ConfigureAwait(false);
            if (_preflight is not null && !await _preflight(token).ConfigureAwait(false)) { throw new InvalidOperationException("Required native role inputs unavailable before enrollment."); }
            if (_scope.Role == "executor")
            {
                string caseId = request["caseId"]?.GetValue<string>() ?? throw new InvalidOperationException("Exact owner case required.");
                if (!_scope.Recipes.TryGetValue(caseId, out var recipes) || action != "PrepareFixture" && !recipes.ContainsKey(action)
                    || request["targets"] is not JsonArray targets || targets.Count is < 1 or > 1000
                    || targets.Any(value => value?["TenantId"]?.GetValue<string>() != _scope.TenantId)
                    || string.IsNullOrWhiteSpace(request["fixtureId"]?.GetValue<string>())) { throw new InvalidOperationException("Complete exact native request required."); }
                var completeTargets = targets.Deserialize<ProtectionTarget[]>(ErasureRootCustody.Json) ?? throw new InvalidOperationException("Complete target identities required.");
                _ = DeletionBatchCapabilityIdentity.TargetManifestDigest(completeTargets);
                if (completeTargets.Select(ErasureRootCustody.TargetId).Distinct(StringComparer.Ordinal).Count() != completeTargets.Length)
                { throw new InvalidOperationException("Distinct exact target identities required."); }
                if (action != "PrepareFixture") { await _actions!.ValidateInputsAsync(Recipe(caseId, action, request), request, token).ConfigureAwait(false); }
                if (request["canaryBase64"] is { } canary && Convert.FromBase64String(canary.GetValue<string>()).Length == 0) { throw new InvalidOperationException("Exact canary required."); }
                var state = await _journal.ReadAsync(_scope.TenantId, token).ConfigureAwait(false);
                if (state.Pending is not null && action is not ("PrepareFixture" or "LookupOriginalDestruction")) { throw new InvalidOperationException("Only exact original preparation recovery may cross pending custody."); }
            }
            else if (action == "EnrollSingleAttempt" && string.IsNullOrWhiteSpace(request["attemptId"]?.GetValue<string>()))
            { throw new InvalidOperationException("Exact attempt required."); }
            else if (action == "RegisterOriginalOperation" && (request["originalRole"]?.GetValue<string>() != "executor"
                || string.IsNullOrWhiteSpace(request["originalOperationId"]?.GetValue<string>())
                || request["originalRequestSha256"]?.GetValue<string>() is not { Length: 64 } originalDigest || originalDigest.Any(value => !char.IsAsciiHexDigit(value))))
            { throw new InvalidOperationException("Complete exact executor registration required."); }
        }
        JsonObject result;
        if (action == "InspectInstallation") { result = await InspectAsync(token).ConfigureAwait(false); }
        else if (action == "LookupOriginalOperation") { result = await LookupAsync(request, token).ConfigureAwait(false); }
        else if (_scope.Role == "enrollment") { result = await EnrollAsync(request, token).ConfigureAwait(false); }
        else if (_scope.Role == "observer") { result = await ObserveAsync(request, token).ConfigureAwait(false); }
        else { result = await ExecuteAsync(request, token).ConfigureAwait(false); }
        var statement = new JsonObject { ["role"] = _scope.Role, ["identity"] = _scope.Identity, ["bindingDigest"] = _scope.BindingDigest,
            ["operationId"] = id, ["requestSha256"] = digest, ["response"] = result };
        await _signing.WaitAsync(token).ConfigureAwait(false);
        try { return new JsonObject { ["statement"] = statement, ["signature"] = Convert.ToBase64String(_signer.SignData(ProtectionQualificationCanonical.Bytes(statement), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) }; }
        finally { _signing.Release(); }
    }
    private async Task<JsonObject> InspectAsync(CancellationToken token)
    {
        var location = _scope.InstallationProof;
        var proof = await _installationReader.GetStateAsync<JsonElement>(location.Component, location.Key, ConsistencyMode.Strong, location.Metadata, token).ConfigureAwait(false);
        var native = JsonNode.Parse(proof.GetRawText()) as JsonObject ?? throw new InvalidOperationException("Independent native installation census unavailable.");
        ProtectionQualificationObserverPermission? permission = null;
        if (_scope.Role == "observer")
        {
            permission = _observerPermission is null ? null : await _observerPermission(token).ConfigureAwait(false);
            if (permission is null || native["credentialScope"]?.GetValue<string>() != permission.CredentialScope)
            { throw new InvalidOperationException("DependencyNotAvailable: independently authenticated current read-only observer permissions required."); }
        }
        var head = await _journal.ReadMetadataAsync(_scope.TenantId, token).ConfigureAwait(false);
        if (native["agentsRuntimeEnabled"]?.GetValue<bool>() is not false || native["storeIdentity"]?.GetValue<string>() != head.StoreId
            || string.IsNullOrWhiteSpace(native["deploymentIdentity"]?.GetValue<string>())
            || AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name?.StartsWith("Hexalith.Agents", StringComparison.Ordinal) == true))
        { throw new InvalidOperationException("Current independent technical installation proof required."); }
        native["supportedOperations"] = new JsonArray(ProtectionQualificationActionCatalog.Actions.Keys.Order(StringComparer.Ordinal).Select(name => (JsonNode)JsonValue.Create(name)!).ToArray());
        if (permission is not null) { native["observerPermissionEvidence"] = JsonSerializer.SerializeToNode(permission); }
        return native;
    }
    private async Task<JsonObject> EnrollAsync(JsonObject request, CancellationToken token)
    {
        string action = request["action"]!.GetValue<string>(), id = request["operationId"]!.GetValue<string>();
        if (action == "LookupAttemptOperations")
        {
            var head = await _journal.ReadAsync(_scope.TenantId, token).ConfigureAwait(false);
            string attempt = request["originalAttemptId"]!.GetValue<string>();
            if (!head.QualificationAttempts.TryGetValue(_scope.BindingDigest, out var originalAttempt) || originalAttempt != attempt) { throw new InvalidOperationException("Original attempt unavailable."); }
            return new JsonObject { ["originalAttemptId"] = attempt, ["operationIdentities"] = new JsonArray(head.QualificationOperations.Values
                .Where(value => value.BindingDigest == _scope.BindingDigest && value.AttemptId == attempt && value.IndependentlyRegistered && value.SafeRequest["action"]?.GetValue<string>() != "RegisterOriginalOperation")
                .Select(value => (JsonNode)new JsonObject { ["operationId"] = value.SafeRequest["operationId"]!.GetValue<string>(), ["requestSha256"] = value.RequestSha256 }).ToArray()) };
        }
        JsonObject? response = null;
        await _journal.UpdateAsync(_scope.TenantId, id, "QualificationEnrollment", ProtectionQualificationOperationJournal.Digest(request), head =>
        {
            string attempt;
            if (action == "EnrollSingleAttempt")
            {
                attempt = request["attemptId"]!.GetValue<string>();
                if (head.QualificationAttempts.ContainsKey(_scope.BindingDigest)) { throw new InvalidOperationException("Accepted binding already has its single attempt."); }
                head.QualificationAttempts.Add(_scope.BindingDigest, attempt);
                response = new JsonObject { ["status"] = "Enrolled", ["attemptId"] = attempt, ["bindingDigest"] = _scope.BindingDigest };
            }
            else
            {
                if (!head.QualificationAttempts.TryGetValue(_scope.BindingDigest, out attempt!)) { throw new InvalidOperationException("Independent attempt enrollment required."); }
                string originalRole = request["originalRole"]!.GetValue<string>(), originalId = request["originalOperationId"]!.GetValue<string>(), hash = request["originalRequestSha256"]!.GetValue<string>();
                if (originalRole != "executor" || hash.Length != 64) { throw new InvalidOperationException("Exact executor original admission required."); }
                string key = ProtectionQualificationOperationJournal.Id(originalRole, originalId);
                if (head.QualificationOperations.TryGetValue(key, out var prior))
                { if (prior.RequestSha256 != hash || prior.BindingDigest != _scope.BindingDigest) { throw new InvalidOperationException("Changed original registration."); } }
                else { head.QualificationOperations.Add(key, new(originalRole, _scope.BindingDigest, attempt, hash, new JsonObject { ["operationId"] = originalId })); }
                response = new JsonObject { ["status"] = "Registered", ["registeredOperationId"] = originalId, ["registeredRequestSha256"] = hash };
            }
            head.QualificationOperations.Add(ProtectionQualificationOperationJournal.Id("enrollment", id), new("enrollment", _scope.BindingDigest, attempt,
                ProtectionQualificationOperationJournal.Digest(request), (JsonObject)request.DeepClone(), response));
            return true;
        }, token).ConfigureAwait(false);
        return response!;
    }
    private async Task<JsonObject> ExecuteAsync(JsonObject request, CancellationToken token)
    {
        string id = request["operationId"]!.GetValue<string>(), action = request["action"]!.GetValue<string>(), caseId = request["caseId"]!.GetValue<string>();
        string digest = ProtectionQualificationOperationJournal.Digest(request), key = ProtectionQualificationOperationJournal.Id("executor", id);
        await _journal.UpdateAsync(_scope.TenantId, id, "QualificationExecution", digest, head =>
        {
            if (action.StartsWith("Unseal", StringComparison.Ordinal) && !head.QualificationOperations.ContainsKey(key)
                && head.QualificationAttempts.TryGetValue(_scope.BindingDigest, out var attempt))
            { head.QualificationOperations.Add(key, new("executor", _scope.BindingDigest, attempt, digest, (JsonObject)request.DeepClone()) { IndependentlyRegistered = false }); }
            if (!head.QualificationOperations.TryGetValue(key, out var original) || original.RequestSha256 != digest || original.Result is not null || original.Calls.Count != 0)
            { throw new InvalidOperationException("Exact fresh independently registered original required; admitted operations use lookup only."); }
            var safe = (JsonObject)request.DeepClone(); safe.Remove("canaryBase64");
            head.QualificationOperations[key] = original with { SafeRequest = safe, Producer = _actions!.Producer }; return true;
        }, token).ConfigureAwait(false);
        JsonObject response;
        if (action == "PrepareFixture")
        {
            _ = await InspectAsync(token).ConfigureAwait(false);
            if (!_scope.Recipes.ContainsKey(caseId) || request["targets"] is not JsonArray targets || targets.Count is < 1 or > 1000
                || targets.Any(value => value!["TenantId"]!.GetValue<string>() != _scope.TenantId)) { throw new InvalidOperationException("Exact isolated native fixture required."); }
            var pending = await _actions!.PendingRecoveryAsync(_scope.TenantId, request["fixtureId"]!.GetValue<string>(), token).ConfigureAwait(false);
            if (pending is not null)
            { await _actions.PrepareAsync(pending, request, (call, ct) => _journal.AdmitCallAsync(_scope.TenantId, "executor", id, call, ct),
                (call, result, ct) => _journal.RetainCallAsync(_scope.TenantId, "executor", id, call, result, ct), token).ConfigureAwait(false); }
            response = new JsonObject { ["status"] = "Prepared" };
        }
        else
        {
            var head = await _journal.ReadAsync(_scope.TenantId, token).ConfigureAwait(false);
            var previous = head.QualificationOperations.Values.Where(value => value.BindingDigest == _scope.BindingDigest
                && value.SafeRequest["fixtureId"]?.GetValue<string>() == request["fixtureId"]!.GetValue<string>())
                .SelectMany(value => value.Calls).ToDictionary(value => value.Key, value => value.Value.Result, StringComparer.Ordinal);
            var result = await _actions!.ExecuteAsync(Recipe(caseId, action, request), request, previous,
                (call, ct) => _journal.AdmitCallAsync(_scope.TenantId, "executor", id, call, ct),
                (call, value, ct) => _journal.RetainCallAsync(_scope.TenantId, "executor", id, call,
                    call.Kind == ProtectionQualificationNativeCallKind.UnsealEvent ? new JsonObject { ["status"] = "Readable" } : value, ct), token).ConfigureAwait(false);
            response = result as JsonObject ?? new JsonObject { ["nativeResult"] = result };
            if (response["status"] is null) { response["status"] = response["Status"]?.DeepClone() ?? "Completed"; }
        }
        var retainedResponse = action.StartsWith("Unseal", StringComparison.Ordinal) ? new JsonObject { ["status"] = "Readable" } : (JsonObject)response.DeepClone();
        await _journal.UpdateAsync(_scope.TenantId, id, "QualificationResult", ProtectionQualificationOperationJournal.Digest(retainedResponse), head =>
        { head.QualificationOperations[key] = head.QualificationOperations[key] with { Result = retainedResponse }; return true; }, token).ConfigureAwait(false);
        return response;
    }
    private async Task<JsonObject> LookupAsync(JsonObject request, CancellationToken token)
    {
        var head = await _journal.ReadAsync(_scope.TenantId, token).ConfigureAwait(false);
        string id = request["originalOperationId"]!.GetValue<string>(), digest = request["originalRequestSha256"]!.GetValue<string>();
        if (!head.QualificationOperations.TryGetValue(ProtectionQualificationOperationJournal.Id(_scope.Role, id), out var original)
            || original.RequestSha256 != digest || original.BindingDigest != _scope.BindingDigest)
        { throw new InvalidOperationException("Exact terminal original unresolved; no redispatch permitted."); }
        if (original.Result is null && _scope.Role == "executor" && _actions is not null)
        {
            string caseId = original.SafeRequest["caseId"]!.GetValue<string>(), action = original.SafeRequest["action"]!.GetValue<string>();
            if (action == "PrepareFixture") { throw new InvalidOperationException("Original preparation outcome unresolved."); }
            var recipe = Recipe(caseId, action, original.SafeRequest);
            foreach (var call in original.Calls.Values.Where(value => value.Result is null).ToArray())
            {
                var recovered = await _actions.RecoverAsync(call.Call, token).ConfigureAwait(false);
                if (recovered is null) { throw new InvalidOperationException("Exact native original remains unresolved."); }
                await _journal.RetainCallAsync(_scope.TenantId, "executor", id, call.Call, recovered, token).ConfigureAwait(false);
            }
            var latest = await _journal.ReadAsync(_scope.TenantId, token).ConfigureAwait(false);
            original = latest.QualificationOperations[ProtectionQualificationOperationJournal.Id("executor", id)];
            var required = recipe.Before.Concat([recipe.Effect]).Concat(recipe.Race is { } race ? [race] : []).Concat(recipe.After).ToArray();
            if (required.Any(call => !original.Calls.TryGetValue(call.Id, out var retained) || retained.Result is null))
            { throw new InvalidOperationException("Original action contains unsubmitted native work; metadata recovery cannot dispatch it."); }
            var nativeResult = original.Calls[recipe.Effect.Id].Result;
            var final = nativeResult is JsonObject obj ? (JsonObject)obj.DeepClone() : new JsonObject { ["nativeResult"] = nativeResult?.DeepClone() };
            if (final["status"] is null) { final["status"] = final["Status"]?.DeepClone() ?? "Completed"; }
            await _journal.UpdateAsync(_scope.TenantId, id, "QualificationRecoveredResult", ProtectionQualificationOperationJournal.Digest(final), current =>
            { var key = ProtectionQualificationOperationJournal.Id("executor", id); current.QualificationOperations[key] = current.QualificationOperations[key] with { Result = final }; return true; }, token).ConfigureAwait(false);
            original = original with { Result = final };
        }
        if (original.Result is null) { throw new InvalidOperationException("Exact terminal original unresolved."); }
        var result = (JsonObject)original.Result.DeepClone(); result["originalOperationId"] = id; result["originalRequestSha256"] = digest; return result;
    }
    private ProtectionQualificationActionRecipe Recipe(string caseId, string action, JsonObject request)
        => ProtectionQualificationSourceCatalog.WithRequiredReplays(caseId, action, _scope.Recipes[caseId][action], _scope.Observations[caseId].Bind(request));
    private async Task<JsonObject> ObserveAsync(JsonObject request, CancellationToken token)
    {
        _ = await InspectAsync(token).ConfigureAwait(false);
        var targets = request["targets"]!.Deserialize<ProtectionTarget[]>()!;
        var head = await _journal.ReadMetadataAsync(_scope.TenantId, token).ConfigureAwait(false);
        var operations = new JsonArray();
        var producers = new Dictionary<string, ProtectionQualificationProducerIdentity>(StringComparer.Ordinal);
        var producerOriginals = new JsonArray();
        if (!head.QualificationAttempts.TryGetValue(_scope.BindingDigest, out var currentAttempt)) { throw new InvalidOperationException("Exact current enrolled attempt required."); }
        foreach (var expected in (JsonArray)request["expectedOperations"]!)
        {
            string id = expected!["operationId"]!.GetValue<string>();
            if (!head.QualificationOperations.TryGetValue(ProtectionQualificationOperationJournal.Id("executor", id), out var actual) || actual.Result is null
                || actual.Producer is null || actual.AttemptId != currentAttempt || actual.Producer.RoleIdentity == _scope.Identity || actual.Producer.BindingDigest != _scope.BindingDigest
                || actual.Role != "executor" || actual.BindingDigest != _scope.BindingDigest
                || actual.RequestSha256 != expected!["requestSha256"]?.GetValue<string>() || actual.SafeRequest["action"]?.GetValue<string>() != expected!["action"]?.GetValue<string>()
                || actual.SafeRequest["caseId"]?.GetValue<string>() != request["caseId"]?.GetValue<string>()
                || !JsonNode.DeepEquals(actual.SafeRequest["targets"], request["targets"])
                || actual.SafeRequest["fixtureId"]?.GetValue<string>() != request["fixtureId"]!.GetValue<string>())
            { throw new InvalidOperationException("Independent persisted original outcome unavailable."); }
            producers.TryAdd(ErasureRootCustody.Hash(actual.Producer), actual.Producer);
            producerOriginals.Add(new JsonObject { ["OperationId"] = id, ["RequestSha256"] = actual.RequestSha256, ["Role"] = actual.Role,
                ["BindingDigest"] = actual.BindingDigest, ["AttemptId"] = actual.AttemptId, ["Producer"] = JsonSerializer.SerializeToNode(actual.Producer) });
            operations.Add(new JsonObject { ["operationId"] = id, ["requestSha256"] = actual.RequestSha256, ["action"] = actual.SafeRequest["action"]!.GetValue<string>() });
        }
        if (producers.Count != 1) { throw new InvalidOperationException("One exact current expected-operation producer required."); }
        var physicalProof = _observerPermission is null ? null : await _observerPermission(token).ConfigureAwait(false);
        if (physicalProof?.ComponentBackings is null) { throw new InvalidOperationException("Independent physical copy enrollment required."); }
        var result = await _observer!.ObserveAsync(_scope.Observations[request["caseId"]!.GetValue<string>()].Bind(request), targets, token, producers.Values.Single(), ((JsonArray)request["expectedOperations"]!).Select(value => value!["operationId"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal), physicalProof.ComponentBackings).ConfigureAwait(false);
        result["operationOutcomes"] = operations;
        result["operationProducerOriginals"] = producerOriginals;
        return result;
    }
}
