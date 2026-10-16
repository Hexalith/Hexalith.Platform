using System.Security.Cryptography;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Enrollable inventory authority. Authorization and transition proofs must echo the requested identity; verification performs no effect.</summary>
public sealed class DaprInventoryAuthorityAdapter(SecretsRuntimeConfiguration configuration, ISecretsDaprInvoker invoker, TimeProvider? clock = null) : IPlatformKeyInventoryAuthority
{
    private readonly SecretsDaprSession _session = new(invoker, configuration.CallTimeout ?? TimeSpan.Zero, clock ?? TimeProvider.System);

    /// <inheritdoc/>
    public Task<bool> AuthorizeOperationAsync(PlatformKeyVersion scope, string operation, CancellationToken cancellationToken = default)
        => EchoAsync(configuration.CurrentInventoryAuthorityReference, "runtime.currentInventoryAuthorityReference", "query-inventory-operation", ScopeBody(scope, operation), cancellationToken);

    /// <inheritdoc/>
    public Task<bool> AuthorizeAsync(PlatformKeyInventoryChange change, CancellationToken cancellationToken = default)
    {
        if (change is null) { return Task.FromResult(false); }
        var body = ScopeBody(change.Key, "AuthorizeInventoryChange");
        body["operationId"] = change.OperationId;
        body["expectedInventoryRevision"] = change.ExpectedInventoryRevision;
        body["action"] = change.Action.ToString();
        body["authenticatedEvidenceId"] = change.AuthenticatedEvidenceId;
        return EchoAsync(configuration.CurrentInventoryAuthorityReference, "runtime.currentInventoryAuthorityReference", "query-inventory-change", body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> ValidateStateAsync(PlatformKeyVersion scope, long revision, string exactStateDigest, CancellationToken cancellationToken = default)
    {
        var body = ScopeBody(scope, "ValidateInventoryState");
        body["revision"] = revision;
        body["exactStateDigest"] = exactStateDigest;
        return EchoAsync(configuration.CurrentInventoryAuthorityReference, "runtime.currentInventoryAuthorityReference", "query-inventory-state", body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> RecordRevisionAsync(PlatformKeyVersion scope, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default)
    {
        RequireJournal();
        var body = ScopeBody(scope, "RecordRevision");
        body["expectedRevision"] = expectedRevision;
        body["targetRevision"] = nextRevision;
        body["targetDigest"] = exactStateDigest;
        return EchoAsync(configuration.CurrentInventoryAuthorityReference, "runtime.currentInventoryAuthorityReference", "query-revision", body, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => TransitionAsync(transition, "AdmitTransition", effect: true, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => TransitionAsync(transition, "RecordTransition", effect: true, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => TransitionAsync(transition, "VerifyTransition", effect: false, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        RequireJournal();
        return TransitionAsync(transition, "RecoverTransition", effect: true, cancellationToken);
    }

    private async Task<bool> TransitionAsync(AnchoredStateTransition transition, string operation, bool effect, CancellationToken cancellationToken)
    {
        if (!TransitionBody(transition, operation, out JsonObject body)) { return false; }
        SecretsCall query = await _session.CallAsync(configuration.CurrentInventoryAuthorityReference, "runtime.currentInventoryAuthorityReference", "query-transition", body, cancellationToken).ConfigureAwait(false);
        if (query.Unavailable) { return false; }
        if (SecretsProof.Authorized(query.Proof, body, _session.Clock.GetUtcNow()) && SecretsProof.Text(query.Proof, "status") != "absent") { return true; }
        if (!effect || !SecretsProof.Absent(query.Proof, body, configuration.CurrentInventoryAuthorityReference, "query-transition", _session.Clock.GetUtcNow())) { return false; }
        SecretsCall effected = await _session.CallAsync(configuration.CurrentInventoryAuthorityReference, "runtime.currentInventoryAuthorityReference", "effect-transition", body, cancellationToken).ConfigureAwait(false);
        return !effected.Unavailable && SecretsProof.Authorized(effected.Proof, body, _session.Clock.GetUtcNow()) && SecretsProof.Text(effected.Proof, "status") != "absent";
    }

    private Task<bool> EchoAsync(string? appId, string missing, string method, JsonObject body, CancellationToken cancellationToken)
        => EchoCallAsync(appId, missing, method, body, cancellationToken);

    private async Task<bool> EchoCallAsync(string? appId, string missing, string method, JsonObject body, CancellationToken cancellationToken)
    {
        SecretsCall proof = await _session.CallAsync(appId, missing, method, body, cancellationToken).ConfigureAwait(false);
        return !proof.Unavailable && SecretsProof.Authorized(proof.Proof, body, _session.Clock.GetUtcNow()) && SecretsProof.Text(proof.Proof, "status") != "absent";
    }

    private static JsonObject ScopeBody(PlatformKeyVersion scope, string operation)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return new JsonObject
        {
            ["tenantId"] = scope.TenantId,
            ["purpose"] = scope.Purpose.ToString(),
            ["version"] = scope.Version,
            ["alias"] = scope.KeyAlias,
            ["authorityReference"] = scope.AuthorityReference,
            ["providerTarget"] = scope.ProviderTarget,
            ["publicAnchorId"] = scope.PublicAnchorId,
            ["publicAnchorVersion"] = scope.PublicAnchorVersion,
            ["operation"] = operation,
        };
    }

    private static bool TransitionBody(AnchoredStateTransition? transition, string operation, out JsonObject body)
    {
        body = new JsonObject();
        if (transition is null || !SecretsProvisioning.IsReference(transition.ScopeId) || !SecretsProvisioning.IsReference(transition.PredecessorDigest)
            || !SecretsProvisioning.IsReference(transition.TargetDigest) || transition.TargetBytes is not { Length: > 0 and <= 524288 }
            || transition.ExpectedRevision < 0 || transition.ExpectedRevision == long.MaxValue || transition.TargetRevision != transition.ExpectedRevision + 1
            || !string.Equals(transition.TargetDigest, Convert.ToHexString(SHA256.HashData(transition.TargetBytes)), StringComparison.OrdinalIgnoreCase)) { return false; }
        body["scopeId"] = transition.ScopeId;
        body["expectedRevision"] = transition.ExpectedRevision;
        body["targetRevision"] = transition.TargetRevision;
        body["predecessorDigest"] = transition.PredecessorDigest;
        body["targetDigest"] = transition.TargetDigest;
        body["targetBytes"] = Convert.ToBase64String(transition.TargetBytes);
        body["operation"] = operation;
        return true;
    }

    private void RequireJournal()
    {
        if (!SecretsProvisioning.IsReference(configuration.NonrollbackJournalReference))
        {
            throw new SecretsDependencyNotAvailableException("runtime.nonrollbackJournalReference", SecretsProvisioning.Action("runtime.nonrollbackJournalReference"));
        }
    }
}
