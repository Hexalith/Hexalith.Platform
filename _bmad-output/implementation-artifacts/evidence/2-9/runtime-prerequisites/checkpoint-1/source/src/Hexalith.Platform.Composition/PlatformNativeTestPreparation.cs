#if HEXALITH_PLATFORM_SOURCE_CATALOG
namespace Hexalith.Platform.Composition;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Aspire.Hosting.ApplicationModel;

using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Identity;
using Hexalith.Tenants.Contracts.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>Prepares synthetic owner state through the real native APIs before the runner publishes readiness.</summary>
internal sealed class PlatformNativeTestPreparation(CompositionRunPlan plan, string catalogDirectory, string signingKey) : ICompositionRunPreparation
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <inheritdoc />
    public CompositionNativeTestScope? NativeTestScope { get; private set; }

    /// <inheritdoc />
    public async Task PrepareAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var scope = JsonSerializer.Deserialize<CompositionNativeTestScope>(await ReadRequiredAsync(Path.Combine(catalogDirectory, "native-scope.json"), cancellationToken).ConfigureAwait(false))
            ?? throw new InvalidOperationException("The actual native owner scope is unavailable.");
        if (scope.RunId != plan.RunId || scope.ModuleId != plan.SelectedTest?.ModuleId
            || scope.Domains.Contains(TenantIdentity.GlobalAdministratorsDomain, StringComparer.Ordinal)
            || !scope.Tenants.Contains(plan.TenantNamespace, StringComparer.Ordinal))
            throw new InvalidOperationException("The native consumer scope differs from this owning run.");
        using var closure = JsonDocument.Parse(await ReadRequiredAsync(Path.Combine(catalogDirectory, "selected-closure.json"), cancellationToken).ConfigureAwait(false));
        foreach (string host in closure.RootElement.GetProperty("hosts").EnumerateArray().Select(host => host.GetProperty("ResourceId").GetString()!))
        {
            var readback = JsonSerializer.Deserialize<RouteCatalogGenerationReadback>(await ReadRequiredAsync(Path.Combine(catalogDirectory, host + ".committed.json"), cancellationToken).ConfigureAwait(false));
            if (readback is not { Serving: true } || readback.RootDigest != scope.RoutingCatalogRoot)
                throw new InvalidOperationException("A real catalog participant did not commit the selected native scope.");
        }
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan remaining = CompositionStartupClock.Remaining(plan.StartupDeadlineUtc, now);
        if (remaining <= TimeSpan.Zero) cancellationToken.ThrowIfCancellationRequested();
        string subject = "g4-operator-" + plan.RunId;
        string operatorToken = CompositionTokenFactory.Create(signingKey, new(subject, [TenantIdentity.DefaultTenantId, plan.TenantNamespace],
            TenantCatalogDeclarations.Commands.Select(command => command.Route.Domain).Distinct(StringComparer.Ordinal).ToArray(),
            ["command:submit", "query:read"], true, remaining), now);
        string nativeProbe = CompositionTokenFactory.Create(signingKey, new("g4-native-tests", scope.Tenants, scope.Domains,
            ["command:submit", "query:read"], false, remaining), now);
        using var gatewayHttp = Client(Endpoint(services, "eventstore"), operatorToken);
        var gateway = new EventStoreGatewayClient(gatewayHttp, Options.Create(new EventStoreGatewayClientOptions { BaseAddress = gatewayHttp.BaseAddress! }));
        var receipts = new List<CommandStatusQueryResponse>();
        receipts.Add(await SubmitAsync(gateway, new BootstrapGlobalAdmin(subject), TenantIdentity.GlobalAdministratorsAggregateId, cancellationToken).ConfigureAwait(false));
        receipts.Add(await SubmitAsync(gateway, new CreateTenant(plan.TenantNamespace, "Synthetic native test tenant", "Run-owned test state"), plan.TenantNamespace, cancellationToken).ConfigureAwait(false));
        receipts.Add(await SubmitAsync(gateway, new AddUserToTenant(plan.TenantNamespace, "g4-native-tests", TenantRole.TenantReader), plan.TenantNamespace, cancellationToken).ConfigureAwait(false));
        using var tenants = Client(Endpoint(services, "tenants-api"), nativeProbe);
        await PollAsync(tenants, "api/tenants/" + Uri.EscapeDataString(plan.TenantNamespace), element =>
            element.TryGetProperty("tenantId", out var tenant) && tenant.GetString() == plan.TenantNamespace, cancellationToken).ConfigureAwait(false);
        using var memories = Client(Endpoint(services, "memories"), nativeProbe);
        using (var response = await memories.PostAsJsonAsync("api/v1/tenants", new TenantProvisioningInput(plan.TenantNamespace, "Synthetic native test tenant")
            { VectorDimensions = 3 }, JsonOptions, cancellationToken).ConfigureAwait(false))
        {
            if (response.StatusCode != HttpStatusCode.Accepted)
                throw new InvalidOperationException("The real Memories tenant provisioning workflow was not accepted: " + (int)response.StatusCode);
        }
        await PollAsync(memories, "api/v1/tenants/" + Uri.EscapeDataString(plan.TenantNamespace), element =>
        {
            var tenant = element.Deserialize<TenantInfo>(JsonOptions);
            return tenant?.Id == plan.TenantNamespace && tenant.Status == TenantStatus.Active;
        }, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(catalogDirectory, "preparation.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            plan.RunId, scope.RoutingCatalogRoot, tenant = plan.TenantNamespace, nativeSubject = "g4-native-tests",
            nativeGlobalAdministrator = false, nativeRole = nameof(TenantRole.TenantReader),
            commands = receipts.Select(receipt => new { receipt.MessageId, receipt.Domain, receipt.AggregateId, receipt.Status, receipt.CommittedEventSequence, receipt.EventCount }).ToArray(),
            tenantsNativeQueryObserved = true, memoriesRegistryObserved = nameof(TenantStatus.Active), observedAtUtc = DateTimeOffset.UtcNow,
        }), cancellationToken).ConfigureAwait(false);
        NativeTestScope = scope;
    }

    private static async Task<CommandStatusQueryResponse> SubmitAsync<T>(IEventStoreGatewayClient gateway, T command, string aggregateId, CancellationToken token) where T : ICommandContract
    {
        string messageId = UniqueIdHelper.GenerateSortableUniqueStringId();
        await gateway.SubmitCommandAsync(new(messageId, TenantIdentity.DefaultTenantId, T.Domain, aggregateId, T.CommandType,
            JsonSerializer.SerializeToElement(command, EventStorePayloadSerialization.Options), CorrelationId: messageId), token).ConfigureAwait(false);
        while (true)
        {
            var status = await gateway.GetCommandStatusAsync(messageId, token).ConfigureAwait(false);
            if (status is { Status: nameof(CommandStatus.Completed), CommittedEventSequence: > 0, EventCount: > 0 }) return status;
            if (status is not null && status.StatusCode >= (int)CommandStatus.Rejected)
                throw new InvalidOperationException("A real owner preparation command did not complete: " + status.Status);
            await Task.Delay(100, token).ConfigureAwait(false);
        }
    }

    private static async Task PollAsync(HttpClient client, string path, Func<JsonElement, bool> ready, CancellationToken token)
    {
        while (true)
        {
            using var response = await client.GetAsync(path, token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false));
                if (ready(document.RootElement)) return;
            }
            else if (response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Conflict or HttpStatusCode.ServiceUnavailable))
                throw new InvalidOperationException("The real owner readiness observation failed: " + (int)response.StatusCode);
            await Task.Delay(100, token).ConfigureAwait(false);
        }
    }

    private static async Task<byte[]> ReadRequiredAsync(string path, CancellationToken token)
    {
        while (!File.Exists(path)) await Task.Delay(50, token).ConfigureAwait(false);
        return await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
    }

    private static HttpClient Client(Uri endpoint, string token)
    {
        var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { BaseAddress = endpoint, Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static Uri Endpoint(IServiceProvider services, string resource) => new(services.GetRequiredService<DistributedApplicationModel>()
        .Resources.OfType<IResourceWithEndpoints>().Single(candidate => candidate.Name == resource).GetEndpoint("http").Url.TrimEnd('/') + "/");
}
#endif
