using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using Dapr.Client;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.DomainService;

/// <summary>Requests narrowly configured owner evidence while preserving the original gateway-verified actor authority.</summary>
public sealed class DelegatedOwnerQueryClient(DaprClient dapr, IHttpClientFactory clients, IWorkloadAssertionIssuer issuer,
    IHttpContextAccessor accessor, IConfiguration configuration, TimeProvider clock)
{
    /// <summary>Optional whole-operation timeout; absent configuration preserves the existing caller-owned budget.</summary>
    public const string OperationTimeoutConfigurationKey = "EventStore:OwnerQueries:OperationTimeout";

    /// <summary>Sends an exact owner request through the existing signed bound-owner gateway route.</summary>
    public async Task<QueryResult> InvokeAsync(SubmitQueryRequest query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        HttpContext? context = accessor.HttpContext;
        VerifiedQueryCaller? caller = VerifiedQueryCaller.Get(context);
        string? workload = configuration["Authentication:WorkloadIssuer:Workload"];
        string? key = configuration["Authentication:BoundStreamRead:PrivateKey"];
        if (caller?.Query.CallerAuthority?.Origin is not ("authenticated-caller" or "delegated-owner-query") || query.Tenant != caller.Query.TenantId
            || context!.Request.Headers[EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName].Count != 1)
            return QueryResult.Failure("safe-denial");
        if (string.IsNullOrWhiteSpace(workload) || string.IsNullOrWhiteSpace(key))
            return QueryResult.Failure(QueryAdapterFailureReason.QueryExecutionFailure);
        TimeSpan? timeout = OperationTimeout();
        long started = timeout is null ? 0 : clock.GetTimestamp();
        using CancellationTokenSource? deadline = timeout is { } budget ? new(budget, clock) : null;
        using CancellationTokenSource? operation = deadline is null ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        CancellationToken operationToken = operation?.Token ?? cancellationToken;
        var request = new BoundOwnerQueryRequest(query, caller.Query.UserId)
        {
            OriginatingQuery = caller.OriginalQuery,
            OriginatingAssertion = context.Request.Headers[EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName][0],
        };
        QueryResult result = await InvokeOwnerAsync(query, request, workload, key, deadline is not null, operationToken).ConfigureAwait(false);
        // All owned request/response content is disposed before this check. A late body or
        // synchronous disposal cannot turn an expired operation into a protected result.
        if (timeout is { } elapsedBudget && (operationToken.IsCancellationRequested || clock.GetElapsedTime(started) >= elapsedBudget))
            throw new OperationCanceledException(operationToken);
        return result;
    }

    private async Task<QueryResult> InvokeOwnerAsync(SubmitQueryRequest query, BoundOwnerQueryRequest request,
        string workload, string key, bool bounded, CancellationToken cancellationToken)
    {
        string gateway = configuration["Authentication:QueryAuthority:GatewayWorkloadId"] ?? "eventstore";
        ValueTask<string?> acquisition = issuer.IssueAsync(new(gateway, EventStoreWorkloadOperations.GatewayOwnerAuthorityQuery), cancellationToken);
        string? assertion = bounded
            ? await acquisition.AsTask().WaitAsync(cancellationToken).ConfigureAwait(false)
            : await acquisition.ConfigureAwait(false);
        if (bounded) cancellationToken.ThrowIfCancellationRequested();
        if (assertion is null) return QueryResult.Failure(QueryAdapterFailureReason.QueryExecutionFailure);
        using HttpRequestMessage message = dapr.CreateInvokeMethodRequest(gateway, "api/v1/queries/owner-authority/workload", request);
        message.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        message.Headers.TryAddWithoutValidation(BoundOwnerQueryProof.HeaderName, BoundOwnerQueryProof.Issue(request, workload, key, clock.GetUtcNow()));
        using HttpResponseMessage response = await clients.CreateClient().SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return QueryResult.Failure("safe-denial");
        if (!response.IsSuccessStatusCode) return QueryResult.Failure(QueryAdapterFailureReason.QueryExecutionFailure);
        SubmitQueryResponse? result = await response.Content.ReadFromJsonAsync<SubmitQueryResponse>(cancellationToken).ConfigureAwait(false);
        return result?.Success == true ? QueryResult.FromPayload(result.Payload, query.QueryType)
            : QueryResult.Failure(QueryAdapterFailureReason.ResponseMismatch);
    }

    private TimeSpan? OperationTimeout()
    {
        string? configured = configuration[OperationTimeoutConfigurationKey];
        if (configured is null) return null;
        if (!TimeSpan.TryParse(configured, CultureInfo.InvariantCulture, out TimeSpan timeout)
            || timeout < TimeSpan.FromMilliseconds(1) || timeout > TimeSpan.FromMinutes(1))
            throw new InvalidOperationException("EventStore:OwnerQueries:OperationTimeout must be a duration between one millisecond and one minute.");
        return timeout;
    }
}
