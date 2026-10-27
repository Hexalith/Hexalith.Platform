using Dapr.Client;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>A value-free exact operation intent. BodyJson never contains key material.</summary>
public sealed record CustodyCallIntent(string Operation, string TenantId, string OwnerPurposeScope, string OperationId,
    string RequestDigest, string BodyJson, string RequestAttemptId, string Challenge);

/// <summary>Fresh independent current/control framing, with installed transport and holder profile pins.</summary>
public sealed record CustodyPreparedCall(string EnvelopeJson, int MaximumRequestMessageBytes,
    string TransportProfileDigest, string? KeyClosureProfileDigest,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] CustodyRoleWorkloadGrant? WorkloadGrant=null);

/// <summary>Independent private-operation/control/observer statements bound to the exact request hash.</summary>
public sealed record CustodyCallAuthorization(string RequestSha256, IReadOnlyDictionary<string, string> Metadata);

/// <summary>Independent authorization service. Implementations must authenticate the caller and read current authority;
/// an intent or a caller-supplied hash never grants permission by itself.</summary>
public interface ICustodyLifecycleCallSource
{
    /// <summary>Obtains fresh current framing for an exact value-free intent.</summary>
    Task<CustodyPreparedCall?> PrepareAsync(CustodyCallIntent intent, CancellationToken cancellationToken);
    /// <summary>Authorizes the exact full wire hash without receiving secret bytes.</summary>
    Task<CustodyCallAuthorization?> AuthorizeAsync(CustodyCallIntent intent, CustodyPreparedCall prepared,
        string requestSha256, CancellationToken cancellationToken);
}

/// <summary>Dapr invocation adapter to the independently installed authorizer. No signer or local fallback is present.</summary>
public sealed class DaprCustodyAuthorizerClient : ICustodyLifecycleCallSource
{
    /// <summary>Configured logical service role.</summary>
    public const string LogicalRole = "custody-authorizer";
    private readonly ISecretsCredentialBoundDaprInvoker _invoker;
    private readonly string? _service;
    private readonly string _credentialReference;

    /// <summary>Uses the configured logical service role and a mounted scoped credential reference.</summary>
    public DaprCustodyAuthorizerClient(DaprClient client, IReadOnlyDictionary<string, string> logicalServices, string credentialReference)
        : this(new DaprSecretsInvoker(client), logicalServices, credentialReference) { }

    /// <summary>Uses a credential-bound Dapr invocation transport, with no unsigned or uncredentialed fallback.</summary>
    public DaprCustodyAuthorizerClient(ISecretsCredentialBoundDaprInvoker invoker, IReadOnlyDictionary<string, string> logicalServices,
        string credentialReference)
    {
        _invoker = invoker; _credentialReference = credentialReference;
        _service = logicalServices.TryGetValue(LogicalRole, out var service) && CustodyBindingContract.Text(service) ? service : null;
    }

    /// <inheritdoc />
    public Task<CustodyPreparedCall?> PrepareAsync(CustodyCallIntent intent, CancellationToken cancellationToken)
        => InvokeAsync<CustodyCallIntent, CustodyPreparedCall>("prepare", intent, cancellationToken);

    /// <inheritdoc />
    public Task<CustodyCallAuthorization?> AuthorizeAsync(CustodyCallIntent intent, CustodyPreparedCall prepared,
        string requestSha256, CancellationToken cancellationToken)
        => InvokeAsync<CustodyAuthorizationIntent, CustodyCallAuthorization>("authorize", new(intent, prepared, requestSha256), cancellationToken);

    private async Task<TResponse?> InvokeAsync<TRequest, TResponse>(string phase, TRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_service is null || !SecretsProvisioning.IsReference(_credentialReference)) return default;
        try
        {
            string? json = await _invoker.InvokeCredentialBoundAsync(_service, _credentialReference, "custody-lifecycle/" + phase,
                System.Text.Json.JsonSerializer.Serialize(request, CustodyBindingContract.Json), token).WaitAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (json is null) return default;
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            if (bytes.Length is 0 or >= CustodyBindingContract.MaximumPageFrameBytes) return default;
            CustodyBindingContract.RejectDuplicateProperties(bytes);
            var result = System.Text.Json.JsonSerializer.Deserialize<TResponse>(bytes, CustodyBindingContract.Json);
            token.ThrowIfCancellationRequested(); return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        catch (Exception) { token.ThrowIfCancellationRequested(); return default; }
    }

    private sealed record CustodyAuthorizationIntent(CustodyCallIntent Intent, CustodyPreparedCall Prepared, string RequestSha256);
}
