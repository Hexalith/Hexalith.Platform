using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>An independently installed role provider. Implementations must use actual owner state and a separately held signing key.</summary>
public interface IQualificationRoleProvider
{
    /// <summary>Gets the exact enrolled role: executor, observer, or enrollment.</summary>
    string Role { get; }

    /// <summary>Checks current accepted target/configuration/plan/anchors, authenticated caller, isolated scope and all required owner ACLs.</summary>
    Task<bool> AuthorizeAsync(JsonObject request, CancellationToken cancellationToken);

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
        JsonObject? request, IQualificationRoleProvider? provider, IQualificationOriginalStore? store, IQualificationPublicProofVerifier? verifier, CancellationToken cancellationToken)
    {
        var missing = QualificationDenial.Evaluate(configuration).MissingInputs.ToList();
        if (provider is null || provider.Role != role) { missing.Add($"qualification.{role}.installedProviderSignerAndScopeAuthority"); }
        if (store is null) { missing.Add($"qualification.{role}.independentDurableOriginalStore"); }
        if (verifier is null) { missing.Add($"qualification.{role}.acceptedPublicProofVerifier"); }
        if (configuration.CallTimeout is not { } timeout || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30)) { missing.Add("runtime.callTimeout"); }
        if (missing.Count > 0) { return Deny(missing); }
        string? id = SecretsProof.Text(request, "requestId");
        if (request is null || id is null || id.Length != 64 || id.Any(c => !char.IsAsciiHexDigitLower(c))
            || !SecretsProof.SameText(request, "protocol", "EXT-SECRETS-1.qualification.v1")
            || !SecretsProof.SameText(request, "targetCommit", configuration.AcceptedReplacementCommit)
            || !SecretsProvisioning.IsReference(SecretsProof.Text(request, "isolatedTargetId"))
            || !SecretsProvisioning.IsReference(SecretsProof.Text(request, "planSha256"))
            || !SecretsProvisioning.IsReference(SecretsProof.Text(request, "runId"))) { return Deny(["ExactAcceptedIsolatedOriginalRequest"]); }
        if (role is not ("executor" or "observer" or "enrollment") || operation != (role switch { "observer" => "observe", "enrollment" => "enroll", _ => operation })
            || role == "executor" && operation is not ("execute" or "lookup")) { return Deny(["EnrolledRoleOperation"]); }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(configuration.CallTimeout ?? TimeSpan.FromSeconds(30));
        bool executionStarted = false;
        try
        {
            if (!await provider!.AuthorizeAsync((JsonObject)request.DeepClone(), budget.Token).WaitAsync(budget.Token).ConfigureAwait(false))
            { return Deny(["IndependentCurrentAcceptedBindingAndIsolatedScope"]); }
            // Keep the complete original for cross-restart identity checks, including a lost result.
            bool first = await store!.BeginAsync(role, id, (JsonObject)request.DeepClone(), budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            string effectiveOperation = role == "executor" && (!first || operation == "lookup") ? "lookup" : operation;
            var ownerRequest = (JsonObject)request.DeepClone();
            // Test expectations are client assertions. Providers resolve caseId to the installed actual procedure.
            ownerRequest.Remove("scenario");
            executionStarted = role == "executor" && effectiveOperation == "execute";
            JsonObject? result = await provider.InvokeAsync(effectiveOperation, ownerRequest, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            if (result is null || SecretsProof.Parse(result.ToJsonString()) is null || !verifier!.Verify(role, request, result))
            { return Deny(["ExactOriginalOwnerResultAndIndependentSignature"], executionStarted); }
            var stable = (JsonObject)result["payload"]!.DeepClone();
            stable.Remove("observedAt");
            stable.Remove("validUntil");
            await store.RetainAsync(role, id, stable, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            return new QualificationHostResult(200, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Deny(["ExactOriginalLookupRequired"], executionStarted); }
    }

    private static QualificationHostResult Deny(IEnumerable<string> missing, bool executionStarted = false)
        => new(503, new JsonObject
        {
            ["code"] = "DependencyNotAvailable", ["dependency"] = "EXT-SECRETS-1",
            ["missingInputs"] = new JsonArray(missing.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["qualificationEffects"] = executionStarted ? JsonValue.Create("Unknown") : JsonValue.Create(0),
            ["originalLookupRequired"] = executionStarted, ["productionMutationAllowed"] = false,
        });
}
