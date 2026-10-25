using System.Text;
using Hexalith.Platform.Secrets.Runtime;

/// <summary>Failure-injecting wrapper over independently signed component-fixture calls; never installation authority.</summary>
internal sealed class ControlledOwnerCallSource(ICustodyOwnerCallSource inner) : ICustodyOwnerCallSource
{
    /// <summary>Gets or sets the next source behavior.</summary>
    internal string Mode { get; set; } = "normal";

    /// <summary>Gets or sets explicit cancellation to inject during private authorization.</summary>
    internal CancellationTokenSource? Cancellation { get; set; }

    /// <summary>Gets captured source framing for exact comparison.</summary>
    internal List<(string Phase, string Tenant, string Scope, string? Identity, string? Transition, CancellationToken Token)> Calls { get; } = [];

    /// <inheritdoc />
    public async Task<CustodyOwnerAuthorizedCall?> AuthorizeAsync(string phase, string tenant, string ownerScope,
        string? operationIdentity, string? transitionJson, CancellationToken token)
    {
        Calls.Add((phase, tenant, ownerScope, operationIdentity, transitionJson, token));
        if (Mode == "refuse") { return null; }
        if (Mode == "throw") { throw new InvalidOperationException("Independent private issuer unavailable."); }
        if (Mode == "cancel") { Cancellation!.Cancel(); return null; }
        CustodyOwnerAuthorizedCall? result = await inner.AuthorizeAsync(phase, tenant, ownerScope, operationIdentity, transitionJson, token);
        if (result is null || Mode == "normal") { return result; }
        CustodyBindingRequestV1 request = CustodyBindingContract.Decode(Encoding.UTF8.GetBytes(result.RequestJson))!;
        request = Mode == "foreign-tenant"
            ? request with { Envelope = request.Envelope with { TenantId = "foreign" } }
            : request with { Envelope = request.Envelope with { OwnerPurposeScope = "foreign" } };
        return result with { RequestJson = Encoding.UTF8.GetString(CustodyBindingContract.Encode(request)) };
    }
}
