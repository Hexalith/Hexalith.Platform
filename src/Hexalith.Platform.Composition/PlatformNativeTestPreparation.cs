#if HEXALITH_PLATFORM_SOURCE_CATALOG
namespace Hexalith.Platform.Composition;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;

using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Identity;
using Hexalith.Tenants.Contracts.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>Prepares synthetic owner state through the real native APIs before the runner publishes readiness.</summary>
internal sealed class PlatformNativeTestPreparation(CompositionRunPlan plan, string catalogDirectory, string signingKey,
    PlatformTenantBootstrapAccess tenantBootstrap, string memoriesApplicationToken) : ICompositionRunPreparation
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
        var catalog = RouteCatalogCodec.Decode(await ReadRequiredAsync(Path.Combine(catalogDirectory, "catalog.json"), cancellationToken).ConfigureAwait(false), scope.RoutingCatalogRoot);
        var tenantQuery = TenantCatalogDeclarations.Queries.Single(declaration => declaration.Execution!.HandlerType == typeof(Hexalith.Tenants.Contracts.Queries.GetTenantQuery).FullName);
        if (!catalog.Declarations.Any(declaration => declaration.RouteId == tenantQuery.RouteId
                && RouteCatalogCodec.ComputeDeclarationDigest(declaration) == RouteCatalogCodec.ComputeDeclarationDigest(tenantQuery))
            || catalog.SchemaVersion == 3 && !catalog.Operations!.Any(operation => operation.RouteId == tenantQuery.RouteId
                && operation.Ingress == "public" && operation.Eligibility == "agent-eligible"))
            throw new InvalidOperationException("The actual Tenants native read operation is absent or ineligible.");
        scope = scope with
        {
            Domains = scope.Domains.Append(tenantQuery.Domain).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            Tenants = scope.Tenants.Append(TenantIdentity.DefaultTenantId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        };
        using var closure = JsonDocument.Parse(await ReadRequiredAsync(Path.Combine(catalogDirectory, "selected-closure.json"), cancellationToken).ConfigureAwait(false));
        foreach (string host in closure.RootElement.GetProperty("hosts").EnumerateArray().Select(host => host.GetProperty("ResourceId").GetString()!))
        {
            var readback = JsonSerializer.Deserialize<RouteCatalogGenerationReadback>(await ReadRequiredAsync(Path.Combine(catalogDirectory, host + ".committed.json"), cancellationToken).ConfigureAwait(false));
            if (readback is not { Serving: true } || readback.RootDigest != scope.RoutingCatalogRoot)
                throw new InvalidOperationException("A real catalog participant did not commit the selected native scope.");
        }
        await WriteProgressAsync("source-validated", null, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan remaining = CompositionStartupClock.Remaining(plan.StartupDeadlineUtc, now);
        if (remaining <= TimeSpan.Zero) cancellationToken.ThrowIfCancellationRequested();
        string subject = "g4-operator-" + plan.RunId;
        string operatorToken = CompositionTokenFactory.Create(signingKey, new(subject, [TenantIdentity.DefaultTenantId, plan.TenantNamespace],
            TenantCatalogDeclarations.Commands.Select(command => command.Route.Domain).Distinct(StringComparer.Ordinal).ToArray(),
            ["command:submit", "query:read"], true, remaining) { Surface = "ui" }, now);
        string nativeProbe = CompositionTokenFactory.Create(signingKey, new("g4-native-tests", scope.Tenants, scope.Domains,
            ["command:submit", "query:read"], false, remaining) { Surface = "agent" }, now);
        using var gatewayHttp = Client(Endpoint(services, "eventstore"), operatorToken);
        var gateway = new EventStoreGatewayClient(gatewayHttp, Options.Create(new EventStoreGatewayClientOptions { BaseAddress = gatewayHttp.BaseAddress! }));
        var receipts = new List<CommandStatusQueryResponse>();
        await WriteProgressAsync("bootstrap-submitting", null, cancellationToken).ConfigureAwait(false);
        string bootstrapId = await SubmitBootstrapAsync(services, subject, catalog, cancellationToken).ConfigureAwait(false);
        receipts.Add(await WaitForCommittedAsync(gateway, bootstrapId, cancellationToken).ConfigureAwait(false));
        await WriteProgressAsync("bootstrap-committed", null, cancellationToken).ConfigureAwait(false);
        await WriteProgressAsync("tenant-submitting", null, cancellationToken).ConfigureAwait(false);
        receipts.Add(await SubmitAsync(gateway, new CreateTenant(plan.TenantNamespace, "Synthetic native test tenant", "Run-owned test state"), plan.TenantNamespace, cancellationToken).ConfigureAwait(false));
        await WriteProgressAsync("tenant-committed", null, cancellationToken).ConfigureAwait(false);
        await WriteProgressAsync("member-submitting", null, cancellationToken).ConfigureAwait(false);
        receipts.Add(await SubmitAsync(gateway, new AddUserToTenant(plan.TenantNamespace, "g4-native-tests", TenantRole.TenantReader), plan.TenantNamespace, cancellationToken).ConfigureAwait(false));
        await WriteProgressAsync("member-committed", null, cancellationToken).ConfigureAwait(false);
        using var tenants = Client(Endpoint(services, "tenants-api"), nativeProbe);
        await WriteProgressAsync("tenants-native-observing", null, cancellationToken).ConfigureAwait(false);
        await PollAsync("tenants-native-observing", tenants, "api/tenants/" + Uri.EscapeDataString(plan.TenantNamespace), element =>
            element.TryGetProperty("tenantId", out var tenant) && tenant.GetString() == plan.TenantNamespace, cancellationToken).ConfigureAwait(false);
        await WriteProgressAsync("tenants-native-observed", null, cancellationToken).ConfigureAwait(false);
        using var memories = Client(Endpoint(services, "memories"), nativeProbe);
        ArgumentException.ThrowIfNullOrWhiteSpace(memoriesApplicationToken);
        memories.DefaultRequestHeaders.Add("dapr-api-token", memoriesApplicationToken);
        await WriteProgressAsync("memories-submit-started", null, cancellationToken).ConfigureAwait(false);
        using (var response = await memories.PostAsJsonAsync("api/v1/tenants", new TenantProvisioningInput(plan.TenantNamespace, "Synthetic native test tenant")
            { VectorDimensions = 3 }, JsonOptions, cancellationToken).ConfigureAwait(false))
        {
            await WriteProgressAsync(response.StatusCode == HttpStatusCode.Accepted ? "memories-submit-accepted" : "memories-submit-refused",
                (int)response.StatusCode, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.Accepted)
                throw new InvalidOperationException("The real Memories tenant provisioning workflow was not accepted: " + (int)response.StatusCode);
        }
        await WriteProgressAsync("memories-active-observing", null, cancellationToken).ConfigureAwait(false);
        await PollAsync("memories-active-observing", memories, "api/v1/tenants/" + Uri.EscapeDataString(plan.TenantNamespace), element =>
        {
            var tenant = element.Deserialize<TenantInfo>(JsonOptions);
            return tenant?.Id == plan.TenantNamespace && tenant.Status == Hexalith.Memories.Contracts.V1.TenantStatus.Active;
        }, cancellationToken).ConfigureAwait(false);
        await WriteProgressAsync("memories-active-observed", null, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(catalogDirectory, "preparation.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            plan.RunId, scope.RoutingCatalogRoot, tenant = plan.TenantNamespace, nativeSubject = "g4-native-tests",
            nativeGlobalAdministrator = false, nativeRole = nameof(TenantRole.TenantReader),
            nativeSurface = "agent", consumedTenantsQuery = tenantQuery.RouteId, nativeDomains = scope.Domains,
            commands = receipts.Select(receipt => new { receipt.MessageId, receipt.Domain, receipt.AggregateId, receipt.Status, receipt.CommittedEventSequence, receipt.EventCount }).ToArray(),
            tenantsNativeQueryObserved = true, memoriesRegistryObserved = nameof(Hexalith.Memories.Contracts.V1.TenantStatus.Active), observedAtUtc = DateTimeOffset.UtcNow,
        }), cancellationToken).ConfigureAwait(false);
        NativeTestScope = scope;
    }

    private static async Task<CommandStatusQueryResponse> SubmitAsync<T>(IEventStoreGatewayClient gateway, T command, string aggregateId, CancellationToken token) where T : ICommandContract
    {
        string messageId = UniqueIdHelper.GenerateSortableUniqueStringId();
        await gateway.SubmitCommandAsync(new(messageId, TenantIdentity.DefaultTenantId, T.Domain, aggregateId, T.CommandType,
            JsonSerializer.SerializeToElement(command, EventStorePayloadSerialization.Options), CorrelationId: messageId), token).ConfigureAwait(false);
        return await WaitForCommittedAsync(gateway, messageId, token).ConfigureAwait(false);
    }

    private async Task<string> SubmitBootstrapAsync(IServiceProvider services, string operatorSubject, RouteCatalogEnvelope catalog, CancellationToken token)
    {
        var declaration = catalog.Declarations.Single(route => route.Execution?.HandlerType == typeof(BootstrapGlobalAdmin).FullName);
        if (catalog.SchemaVersion != 3 || !catalog.Operations!.Any(operation => operation.RouteId == declaration.RouteId && operation.Ingress == "internal"))
            throw new InvalidOperationException("The actual owner bootstrap service contract is unavailable.");
        var model = services.GetRequiredService<DistributedApplicationModel>();
        var owner = model.Resources.Single(resource => resource.Name == tenantBootstrap.ResourceId);
        var sidecar = owner.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
        int port = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.DaprHttpPort
            ?? throw new InvalidOperationException("The actual owned Tenants sidecar endpoint is unavailable.");
        string audience = model.Resources.Single(resource => resource.Name == "eventstore").Annotations.OfType<PlatformServerAnnotation>().Single().Binding.AppId;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:SigningKey"] = signingKey,
            ["Authentication:JwtBearer:Issuer"] = tenantBootstrap.Issuer,
            ["Authentication:JwtBearer:Audience"] = CompositionEnvironment.TokenAudience,
            ["Authentication:JwtBearer:ValidAudiences:0"] = CompositionEnvironment.TokenAudience,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
        }).Build();
        var credentialServices = new ServiceCollection();
        credentialServices.AddSingleton<IHostEnvironment>(services.GetRequiredService<IHostEnvironment>());
        credentialServices.AddLogging();
        credentialServices.AddEventStoreWorkloadAssertionIssuer(configuration, options => options.Workload = tenantBootstrap.AppId);
        await using var credentials = credentialServices.BuildServiceProvider();
        string? assertion = await credentials.GetRequiredService<IWorkloadAssertionIssuer>().IssueAsync(new(audience,
            EventStoreGatewayWorkloadOperations.CommandSubmit, new Dictionary<string, string>
            {
                [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = TenantIdentity.DefaultTenantId,
                [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = declaration.Domain,
            }), token).ConfigureAwait(false);
        if (string.IsNullOrEmpty(assertion)) throw new InvalidOperationException("The actual owner bootstrap workload assertion is unavailable.");
        string messageId = UniqueIdHelper.GenerateSortableUniqueStringId();
        var command = new SubmitCommandRequest(messageId, TenantIdentity.DefaultTenantId, declaration.Domain, TenantIdentity.GlobalAdministratorsAggregateId,
            BootstrapGlobalAdmin.CommandType, JsonSerializer.SerializeToElement(new BootstrapGlobalAdmin(operatorSubject), EventStorePayloadSerialization.Options), CorrelationId: messageId);
        var bound = new BoundWorkloadCommandRequest(command, operatorSubject);
        string proof = BoundWorkloadCommandProof.Issue(bound, tenantBootstrap.AppId, tenantBootstrap.PrivateKey, DateTimeOffset.UtcNow);
        using var transport = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
        { BaseAddress = new Uri("http://127.0.0.1:" + port + "/"), Timeout = Timeout.InfiniteTimeSpan };
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "v1.0/invoke/" + Uri.EscapeDataString(audience) + "/method/api/v1/commands/bound/workload")
        { Content = JsonContent.Create(bound, options: JsonOptions) };
        request.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        request.Headers.TryAddWithoutValidation(BoundWorkloadCommandProof.HeaderName, proof);
        if (tenantBootstrap.ApiToken is { Length: > 0 } apiToken) request.Headers.TryAddWithoutValidation("dapr-api-token", apiToken);
        using var response = await transport.SendAsync(request, token).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Accepted)
            throw new InvalidOperationException("The real bound owner bootstrap command was not accepted: " + (int)response.StatusCode);
        return messageId;
    }

    private static async Task<CommandStatusQueryResponse> WaitForCommittedAsync(IEventStoreGatewayClient gateway, string messageId, CancellationToken token)
    {
        while (true)
        {
            var status = await gateway.GetCommandStatusAsync(messageId, token).ConfigureAwait(false);
            if (status is { Status: nameof(CommandStatus.Completed), CommittedEventSequence: > 0, EventCount: > 0 }) return status;
            if (status is not null && status.StatusCode >= (int)CommandStatus.Rejected)
                throw new InvalidOperationException("A real owner preparation command did not complete: " + status.Status);
            await Task.Delay(100, token).ConfigureAwait(false);
        }
    }

    private async Task PollAsync(string step, HttpClient client, string path, Func<JsonElement, bool> ready, CancellationToken token)
    {
        while (true)
        {
            using var response = await client.GetAsync(path, token).ConfigureAwait(false);
            await WriteProgressAsync(step, (int)response.StatusCode, token).ConfigureAwait(false);
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

    /// <summary>Publishes only fixed-step partial observations; the final receipt remains the readiness authority.</summary>
    private async Task WriteProgressAsync(string step, int? httpStatus, CancellationToken token)
    {
        if (step is not ("source-validated" or "bootstrap-submitting" or "bootstrap-committed"
            or "tenant-submitting" or "tenant-committed" or "member-submitting" or "member-committed"
            or "tenants-native-observing" or "tenants-native-observed" or "memories-submit-started"
            or "memories-submit-accepted" or "memories-submit-refused" or "memories-active-observing" or "memories-active-observed"))
            throw new ArgumentOutOfRangeException(nameof(step));
        if (httpStatus is < 100 or > 599) throw new ArgumentOutOfRangeException(nameof(httpStatus));
        string path = Path.Combine(catalogDirectory, "preparation-progress.json");
        string temporary = path + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, JsonSerializer.SerializeToUtf8Bytes(new
            {
                plan.RunId, step, httpStatus, complete = false, observedAtUtc = DateTimeOffset.UtcNow,
            }), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static async Task<byte[]> ReadRequiredAsync(string path, CancellationToken token)
    {
        while (!File.Exists(path)) await Task.Delay(50, token).ConfigureAwait(false);
        return await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
    }

    private static HttpClient Client(Uri endpoint, string token)
    {
        var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { BaseAddress = endpoint, Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static Uri Endpoint(IServiceProvider services, string resource) => new(services.GetRequiredService<DistributedApplicationModel>()
        .Resources.OfType<IResourceWithEndpoints>().Single(candidate => candidate.Name == resource).GetEndpoint("http").Url.TrimEnd('/') + "/");
}
#endif
