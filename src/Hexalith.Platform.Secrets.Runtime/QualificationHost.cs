using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Enrollable role dispatcher. There is no default provider, signer, scope authority or durable store.</summary>
public static class QualificationHost
{
    /// <summary>Reads a bounded request body; malformed and oversized bodies grant no authority.</summary>
    public static async Task<JsonObject?> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        string? body = await DaprSecretsInvoker.ReadBoundedAsync(stream, cancellationToken).ConfigureAwait(false);
        return body is null ? null : SecretsProof.Parse(body);
    }

    /// <summary>Runs one admitted isolated operation; repeat and uncertain execution always use exact lookup.</summary>
    public static async Task<QualificationHostResult> HandleAsync(string role, string operation, SecretsRuntimeConfiguration configuration,
        JsonObject? request, IQualificationRoleProvider? provider, IQualificationOriginalStore? store, IQualificationPublicProofVerifier? verifier, CancellationToken cancellationToken, QualificationCallerContext? caller = null)
    {
        bool executionUncertain = role == "executor";
        var missing = QualificationDenial.Evaluate(configuration).MissingInputs.ToList();
        if (caller is null || !SecretsProvisioning.IsReference(caller.Identity) || !SecretsProvisioning.IsReference(caller.ScopeReference)) { missing.Add($"qualification.{role}.authenticatedCallerScope"); }
        if (provider is null || provider.Role != role) { missing.Add($"qualification.{role}.installedProviderSignerAndScopeAuthority"); }
        if (store is null) { missing.Add($"qualification.{role}.independentDurableOriginalStore"); }
        if (verifier is null) { missing.Add($"qualification.{role}.acceptedPublicProofVerifier"); }
        if (configuration.CallTimeout is not { } timeout || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30)) { missing.Add("runtime.callTimeout"); }
        if (missing.Count > 0 || provider is null || store is null || verifier is null) { return Deny(missing, executionUncertain, request); }
        if (request is null || SecretsProof.Parse(request.ToJsonString()) is not { } original
            || !SecretsProof.SameText(original, "targetCommit", configuration.AcceptedReplacementCommit)
            || !QualificationProtocolSchema.ValidRequest(role, operation, original)) { return Deny(["ExactAcceptedIsolatedOriginalRequest"], executionUncertain, request); }
        request = original;
        string id = SecretsProof.Text(request, "requestId")!;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(configuration.CallTimeout ?? TimeSpan.FromSeconds(30));
        try
        {
            // Carry the complete canonical public identity; drivers independently verify every outcome.
            var ownerRequest = new JsonObject();
            foreach (string field in (role == "enrollment"
                ? "protocol runId targetCommit isolatedTargetId planSha256 requestId operation"
                : "protocol runId targetCommit isolatedTargetId planSha256 requestId caseId caseTargetId enrolledScope scenario").Split(' '))
            { ownerRequest[field] = request[field]!.DeepClone(); }
            if (!await IndependentReceivingOwnership.Observe(provider.AuthorizeAsync((JsonObject)ownerRequest.DeepClone(), caller!, budget.Token)).WaitAsync(budget.Token).ConfigureAwait(false))
            { return Deny(["IndependentCurrentAcceptedBindingAndIsolatedScope"], executionUncertain); }
            // Keep the complete original for cross-restart identity checks, including a lost result.
            // An existing or failed original publication may follow an applied predecessor.
            // From this boundary onward only exact lookup can resolve executor uncertainty.
            executionUncertain = role == "executor";
            // Public audit originals are distinct from the receiving-operation namespace, even at one authority.
            string auditId = "public-host:" + id;
            bool lookup = role == "executor" && operation == "lookup";
            bool first = !lookup && await IndependentReceivingOwnership.Observe(store.BeginAsync(role, auditId, (JsonObject)request.DeepClone(), budget.Token)).WaitAsync(budget.Token).ConfigureAwait(false);
            string effectiveOperation = role == "executor" && (!first || operation == "lookup") ? "lookup" : operation;
            JsonObject? result = await IndependentReceivingOwnership.Observe(provider.InvokeAsync(effectiveOperation, ownerRequest, budget.Token)).WaitAsync(budget.Token).ConfigureAwait(false);
            if(result is null || SecretsProof.Parse(result.ToJsonString()) is not { } parsedResult)
            { return Deny(["ExactOriginalOwnerResultAndIndependentSignature"],executionUncertain); }
            result=parsedResult;
            if(!(role=="enrollment"?verifier.Verify(role,request,result,budget.Token):verifier.VerifyOriginal(role,request,result,SecretsProof.Integer(result["payload"]!.AsObject(),"observedAt")??-1,budget.Token)))
            { return Deny(["ExactOriginalOwnerResultAndIndependentSignature"],executionUncertain); }
            var stable = (JsonObject)result["payload"]!.DeepClone();
            stable.Remove("observedAt");
            stable.Remove("validUntil");
            if (!lookup) await IndependentReceivingOwnership.Observe(store.RetainAsync(role, auditId, stable, budget.Token)).WaitAsync(budget.Token).ConfigureAwait(false);
            return new QualificationHostResult(200, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) { IndependentReceivingDiagnostics.Record(error is IOException?IndependentReceivingFailure.StorageFailure:IndependentReceivingFailure.HandlerFailure);return Deny(["ExactOriginalLookupRequired"], executionUncertain); }
    }

    internal static QualificationHostResult Deny(IEnumerable<string> missing, bool executionStarted = false, JsonObject? original = null)
        => new(503, new JsonObject
        {
            ["code"] = "DependencyNotAvailable", ["dependency"] = "EXT-SECRETS-1",
            ["missingInputs"] = new JsonArray(missing.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["qualificationEffects"] = executionStarted ? JsonValue.Create("Unknown") : JsonValue.Create(0),
            ["originalLookupRequired"] = executionStarted, ["productionMutationAllowed"] = false,
            ["requestId"] = SecretsProof.Text(original??new JsonObject(),"requestId") is { Length:64 } id && id.All(char.IsAsciiHexDigitLower)?id:null,
            ["runId"] = SecretsProof.Text(original??new JsonObject(),"runId") is { } run && SecretsProvisioning.IsReference(run)?run:null,
        });
}
