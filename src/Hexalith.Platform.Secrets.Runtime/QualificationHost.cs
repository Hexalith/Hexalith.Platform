using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>An independently installed role provider. Implementations must use actual owner state and a separately held signing key.</summary>
public interface IQualificationRoleProvider
{
    /// <summary>Gets the exact enrolled role: executor, observer, or enrollment.</summary>
    string Role { get; }

    /// <summary>Checks current accepted target/configuration/plan/anchors, authenticated caller, isolated scope and all required owner ACLs.</summary>
    Task<bool> AuthorizeAsync(JsonObject request, QualificationCallerContext caller, CancellationToken cancellationToken);

    /// <summary>Reads actual owner results and returns the independently signed public protocol envelope. Expected outcomes are never supplied.</summary>
    Task<JsonObject?> InvokeAsync(string operation, JsonObject original, CancellationToken cancellationToken);
}

/// <summary>Executor installation; observer and enrollment installations cannot satisfy this port.</summary>
public interface IQualificationExecutorProvider : IQualificationRoleProvider;

/// <summary>Independent persisted-owner reader; it must have no executor or signing-request permission.</summary>
public interface IQualificationObserverProvider : IQualificationRoleProvider;

/// <summary>Independent isolated-target enrollment and current ACL verifier.</summary>
public interface IQualificationEnrollmentProvider : IQualificationRoleProvider;

/// <summary>Independently provisioned atomic, durable, nonrollback original store; not an in-memory substitute.</summary>
public interface IQualificationOriginalStore
{
    /// <summary>Atomically retains the complete request before an effect. Existing identities return false; changed bytes must throw.</summary>
    Task<bool> BeginAsync(string role, string requestId, JsonObject original, CancellationToken cancellationToken);

    /// <summary>Retains a signed observation or resolved original. Existing changed bytes must throw.</summary>
    Task RetainAsync(string role, string requestId, JsonObject result, CancellationToken cancellationToken);
}

/// <summary>Public endpoint result. A lost provider result retains the original and requires exact lookup.</summary>
public sealed record QualificationHostResult(int StatusCode, JsonObject Body);

/// <summary>Enrollable role dispatcher. There is no default provider, signer, scope authority or durable store.</summary>
public static class QualificationHost
{
    /// <summary>Reads a bounded request body; malformed and oversized bodies grant no authority.</summary>
    public static async Task<JsonObject?> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        string? body = await DaprSecretsInvoker.ReadBoundedAsync(stream, cancellationToken).ConfigureAwait(false);
        return body is null ? null : SecretsProof.Parse(body);
    }

    /// <summary>Runs one admitted isolated operation; repeat and uncertain execution always use exact lookup.</summary>
    public static async Task<QualificationHostResult> HandleAsync(string role, string operation, SecretsRuntimeConfiguration configuration,
        JsonObject? request, IQualificationRoleProvider? provider, IQualificationOriginalStore? store, IQualificationPublicProofVerifier? verifier, CancellationToken cancellationToken, QualificationCallerContext? caller = null)
    {
        var missing = QualificationDenial.Evaluate(configuration).MissingInputs.ToList();
        if (caller is null || !SecretsProvisioning.IsReference(caller.Identity) || !SecretsProvisioning.IsReference(caller.ScopeReference)) { missing.Add($"qualification.{role}.authenticatedCallerScope"); }
        if (provider is null || provider.Role != role) { missing.Add($"qualification.{role}.installedProviderSignerAndScopeAuthority"); }
        if (store is null) { missing.Add($"qualification.{role}.independentDurableOriginalStore"); }
        if (verifier is null) { missing.Add($"qualification.{role}.acceptedPublicProofVerifier"); }
        if (configuration.CallTimeout is not { } timeout || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30)) { missing.Add("runtime.callTimeout"); }
        if (missing.Count > 0) { return Deny(missing); }
        if (request is null || SecretsProof.Parse(request.ToJsonString()) is not { } original
            || !SecretsProof.SameText(original, "targetCommit", configuration.AcceptedReplacementCommit)
            || !QualificationProtocolSchema.ValidRequest(role, operation, original)) { return Deny(["ExactAcceptedIsolatedOriginalRequest"]); }
        request = original;
        string id = SecretsProof.Text(request, "requestId")!;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(configuration.CallTimeout ?? TimeSpan.FromSeconds(30));
        bool executionUncertain = role == "executor";
        try
        {
            // Construct the closed provider shape; client expectations stay only in the durable original.
            var ownerRequest = new JsonObject();
            foreach (string field in (role == "enrollment"
                ? "protocol runId targetCommit isolatedTargetId planSha256 requestId operation"
                : "protocol runId targetCommit isolatedTargetId planSha256 requestId caseId caseTargetId enrolledScope").Split(' '))
            { ownerRequest[field] = request[field]!.DeepClone(); }
            if (!await provider!.AuthorizeAsync((JsonObject)ownerRequest.DeepClone(), caller!, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false))
            { return Deny(["IndependentCurrentAcceptedBindingAndIsolatedScope"], executionUncertain); }
            // Keep the complete original for cross-restart identity checks, including a lost result.
            // An existing or failed original publication may follow an applied predecessor.
            // From this boundary onward only exact lookup can resolve executor uncertainty.
            executionUncertain = role == "executor";
            bool first = await store!.BeginAsync(role, id, (JsonObject)request.DeepClone(), budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            string effectiveOperation = role == "executor" && (!first || operation == "lookup") ? "lookup" : operation;
            JsonObject? result = await provider.InvokeAsync(effectiveOperation, ownerRequest, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            if (result is null || SecretsProof.Parse(result.ToJsonString()) is null || !verifier!.Verify(role, request, result, budget.Token))
            { return Deny(["ExactOriginalOwnerResultAndIndependentSignature"], executionUncertain); }
            var stable = (JsonObject)result["payload"]!.DeepClone();
            stable.Remove("observedAt");
            stable.Remove("validUntil");
            await store.RetainAsync(role, id, stable, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            return new QualificationHostResult(200, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Deny(["ExactOriginalLookupRequired"], executionUncertain); }
    }

    internal static QualificationHostResult Deny(IEnumerable<string> missing, bool executionStarted = false)
        => new(503, new JsonObject
        {
            ["code"] = "DependencyNotAvailable", ["dependency"] = "EXT-SECRETS-1",
            ["missingInputs"] = new JsonArray(missing.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["qualificationEffects"] = executionStarted ? JsonValue.Create("Unknown") : JsonValue.Create(0),
            ["originalLookupRequired"] = executionStarted, ["productionMutationAllowed"] = false,
        });
}
