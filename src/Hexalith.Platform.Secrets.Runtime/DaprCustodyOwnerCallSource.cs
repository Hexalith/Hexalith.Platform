using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact owner-call framing from a separately authenticated current authority; no caller-supplied envelope is trusted.</summary>
public sealed class DaprCustodyOwnerCallSource(IndependentDaprAuthority authority) : ICustodyOwnerCallSource
{
    /// <inheritdoc />
    public Task<CustodyOwnerAuthorizedCall?> AuthorizeAsync(string phase, string tenant, string ownerScope,
        string? operationIdentity, string? transitionJson, CancellationToken cancellationToken)
        => phase is "read-current" or "admit-transition" or "record-transition" or "recover-transition" or "verify-transition"
            ? authority.InvokeAsync<CustodyOwnerAuthorizedCall>("custody-owner-call", new { phase, tenant, ownerScope, operationIdentity, transitionJson }, cancellationToken)
            : Task.FromResult<CustodyOwnerAuthorizedCall?>(null);
}
