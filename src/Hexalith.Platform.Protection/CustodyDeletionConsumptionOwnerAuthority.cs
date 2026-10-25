using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Opt-in Dapr-only bridge from the deletion-consumption owner to independently installed custody authority.</summary>
/// <remarks>Construction enrolls nothing and installs no DI. The private call source and public enrollment/trust
/// must be independently installed. Owner, guard and custody-head revisions remain separate.</remarks>
public sealed class CustodyDeletionConsumptionOwnerAuthority : IDeletionConsumptionOwnerStateAuthority
{
    private readonly ICustodyOwnerStateAuthority _owner;

    /// <summary>Creates the exact canonical tenant/actor-scope bridge from independently installed custody inputs.</summary>
    /// <param name="client">Dedicated authenticated Dapr transport.</param>
    /// <param name="logicalBindingName">Installed logical custody binding.</param>
    /// <param name="tenantId">Canonical tenant enrolled by the independent owner.</param>
    /// <param name="enrollmentJson">Independent public authority enrollment.</param>
    /// <param name="publicKeyPem">Independently enrolled P-256 public verifier.</param>
    /// <param name="source">Independent exact private operation/recovery permission issuer.</param>
    public CustodyDeletionConsumptionOwnerAuthority(DaprClient client, string logicalBindingName, string tenantId,
        string enrollmentJson, string publicKeyPem, ICustodyOwnerCallSource source)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalBindingName);
        ScopeId = DeletionConsumptionActor.GetOwnerScope(tenantId);
        if (!ScopeId.StartsWith(tenantId + ":", StringComparison.Ordinal))
        {
            throw new ArgumentException("Canonical tenant required.", nameof(tenantId));
        }

        TenantId = tenantId;
        _owner = new CustodyOwnerAuthority(client, logicalBindingName, TenantId, ScopeId, enrollmentJson, publicKeyPem, source);
    }

    /// <inheritdoc />
    public string TenantId { get; }

    /// <inheritdoc />
    public string ScopeId { get; }

    /// <inheritdoc />
    public Task<bool> ValidateStateAsync(long ownerRevision, string exactStateDigest, CancellationToken cancellationToken = default)
        => InvokeAsync(() => _owner.ValidateStateAsync(ownerRevision, exactStateDigest, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<bool> AuthorizeOperationAsync(string identity, string operation, CancellationToken cancellationToken = default)
        => InvokeAsync(() => _owner.AuthorizeOperationAsync(identity, operation, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => TransitionAsync(transition, () => _owner.AdmitTransitionAsync(transition, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => TransitionAsync(transition, () => _owner.RecordTransitionAsync(transition, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => TransitionAsync(transition, () => _owner.RecoverTransitionAsync(transition, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => TransitionAsync(transition, () => _owner.VerifyTransitionAsync(transition, cancellationToken), cancellationToken);

    private Task<bool> TransitionAsync(AnchoredStateTransition transition, Func<Task<bool>> operation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return transition is not null && transition.ScopeId == ScopeId ? InvokeAsync(operation, token) : Task.FromResult(false);
    }

    private static async Task<bool> InvokeAsync(Func<Task<bool>> operation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            bool accepted = await operation().ConfigureAwait(false);
            // The underlying source client refuses exceptions; explicit caller cancellation
            // still belongs to the caller, including when that client returns false.
            token.ThrowIfCancellationRequested();
            return accepted;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
