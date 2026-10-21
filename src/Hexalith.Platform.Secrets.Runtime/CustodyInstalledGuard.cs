using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>In-process enrollment for one exact custody operation. It stores no DEK and does not read HMAC or a Raft snapshot.</summary>
internal sealed class CustodyInstalledGuard : ICustodyInstalledGuard
{
    /// <summary>Gets the component whose enrollment is accepted.</summary>
    internal const string Component = "bindings.hexalithcustody";

    private CustodyInstalledGuard(string tenantId, string operationId, string method, string requestDigest, long revision, string stateDigest)
    {
        TenantId = tenantId;
        OperationId = operationId;
        Method = method;
        RequestDigest = requestDigest;
        Revision = revision;
        StateDigest = stateDigest;
    }

    /// <inheritdoc/>
    public string TenantId { get; }

    /// <inheritdoc/>
    public string OperationId { get; }

    /// <inheritdoc/>
    public string Method { get; }

    /// <inheritdoc/>
    public string RequestDigest { get; }

    /// <inheritdoc/>
    public long Revision { get; }

    /// <inheritdoc/>
    public string StateDigest { get; }

    /// <summary>Accepts enrollment only for custody-lifecycle-v1, interaction-custody, and <see cref="Component"/>.</summary>
    /// <exception cref="ArgumentException">The protocol, role, component, or operation is not the installed exact enrollment.</exception>
    internal static CustodyInstalledGuard Enroll(string protocol, string logicalRole, string component, string tenantId, string operationId, string method, string requestDigest, long revision, string stateDigest)
    {
        if (protocol != CustodyBindingContract.Protocol || logicalRole != CustodyBindingContract.LogicalRole || component != Component)
        {
            throw new ArgumentException("Custody guard enrollment is not the installed component.");
        }

        if (!CustodyBindingContract.Text(tenantId) || !CustodyBindingContract.Text(operationId) || !CustodyBindingContract.Text(method)
            || !CustodyBindingContract.Digest(requestDigest) || revision < 0 || !CustodyBindingContract.Digest(stateDigest))
        {
            throw new ArgumentException("Custody guard enrollment is not one exact operation.");
        }

        _ = CustodyKeyLifecycleActor.GetActorId(tenantId);
        return new CustodyInstalledGuard(tenantId, operationId, method, requestDigest, revision, stateDigest);
    }

    /// <inheritdoc/>
    public bool Accepts(string tenantId, string operationId, string method, string requestDigest, long revision, string stateDigest)
        => TenantId == tenantId && OperationId == operationId && Method == method && RequestDigest == requestDigest && Revision == revision && StateDigest == stateDigest;
}
