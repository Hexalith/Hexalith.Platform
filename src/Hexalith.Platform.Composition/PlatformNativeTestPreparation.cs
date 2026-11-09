namespace Hexalith.Platform.Composition;

using System.Collections.Frozen;
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
    PlatformTenantBootstrapAccess tenantBootstrap, PlatformServerBinding memoriesBinding, IReadOnlyDictionary<string, string> expectedCatalogHosts, PlatformCompositionOptions? composition = null) : ICompositionRunPreparation
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly FrozenDictionary<string, string> _expectedCatalogHosts = expectedCatalogHosts.ToFrozenDictionary(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool RoutingCatalogObservationsRequired => true;

    /// <summary>Gets the routing-catalog receiver cohort, by resource identity, that the owning composition handed to this preparation.</summary>
    internal IReadOnlyDictionary<string, string> ExpectedCatalogHosts => _expectedCatalogHosts;

    /// <inheritdoc />
    public CompositionNativeTestScope? NativeTestScope { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<CompositionNativeHttpEndpoint> NativeHttpEndpoints { get; private set; } = [];

    /// <inheritdoc />
    public string? NativeEndpointCredentialsPath { get; private set; }

    /// <inheritdoc />
    public async Task PrepareAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        NativeTestScope = null;
        NativeHttpEndpoints = [];
        NativeEndpointCredentialsPath = null;
        string path = Path.Combine(Path.GetFullPath(plan.Workspace), NativeTestHandoff.EndpointCredentialsFileName);
        File.Delete(path);
        File.Delete(path + ".tmp");
        try { await PrepareCoreAsync(services, path, cancellationToken).ConfigureAwait(false); }
        finally
        {
            if (NativeTestScope is null) File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    private async Task PrepareCoreAsync(IServiceProvider services, string credentialPath, CancellationToken cancellationToken)
    {
        var scope = JsonSerializer.Deserialize<CompositionNativeTestScope>(await ReadRequiredAsync(Path.Combine(catalogDirectory, "native-scope.json"), cancellationToken).ConfigureAwait(false))
            ?? throw new InvalidOperationException("The actual native owner scope is unavailable.");
        if (scope.RunId != plan.RunId || scope.ModuleId != plan.SelectedTest?.ModuleId
            || scope.Domains.Contains(TenantIdentity.GlobalAdministratorsDomain, StringComparer.Ordinal)
            || !scope.Tenants.Contains(plan.TenantNamespace, StringComparer.Ordinal)
#if HEXALITH_PLATFORM_CANONICAL_SCOPES
            || scope.Tenants.Count != 1
#endif
            )
            throw new InvalidOperationException("The native consumer scope differs from this owning run.");
        byte[] canonical = await ReadRequiredAsync(Path.Combine(catalogDirectory, "catalog.json"), cancellationToken).ConfigureAwait(false);
        var catalog = RouteCatalogCodec.Decode(canonical, scope.RoutingCatalogRoot);
        var tenantQuery = TenantCatalogDeclarations.Queries.Single(declaration => declaration.Execution!.HandlerType == typeof(Hexalith.Tenants.Contracts.Queries.GetTenantQuery).FullName);
        if (!catalog.Declarations.Any(declaration => declaration.RouteId == tenantQuery.RouteId
                && RouteCatalogCodec.ComputeDeclarationDigest(declaration) == RouteCatalogCodec.ComputeDeclarationDigest(tenantQuery))
            || catalog.SchemaVersion == 3 && !catalog.Operations!.Any(operation => operation.RouteId == tenantQuery.RouteId
                && operation.Ingress == "public" && operation.Eligibility == "agent-eligible"))
            throw new InvalidOperationException("The actual Tenants native read operation is absent or ineligible.");
        scope = scope with
        {
            Domains = scope.Domains.Append(tenantQuery.Domain).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
#if !HEXALITH_PLATFORM_CANONICAL_SCOPES
            Tenants = scope.Tenants.Append(TenantIdentity.DefaultTenantId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
#endif
        };
        using var closure = JsonDocument.Parse(await ReadRequiredAsync(Path.Combine(catalogDirectory, "selected-closure.json"), cancellationToken).ConfigureAwait(false));
        JsonElement[] selectedHosts = closure.RootElement.GetProperty("hosts").EnumerateArray().ToArray();
        if (_expectedCatalogHosts.Count == 0 || selectedHosts.Length != _expectedCatalogHosts.Count
            || selectedHosts.Select(host => host.GetProperty("ResourceId").GetString()).Distinct(StringComparer.Ordinal).Count() != selectedHosts.Length
            || selectedHosts.Any(host => !_expectedCatalogHosts.TryGetValue(host.GetProperty("ResourceId").GetString()!, out string? appId)
                || host.GetProperty("AppId").GetString() != appId))
            throw new InvalidOperationException("The retained catalog closure differs from the owning composition.");
        Dictionary<string, RouteCatalogGenerationReadback> readbacks = new(StringComparer.Ordinal);
        foreach (string host in _expectedCatalogHosts.Keys)
        {
            var readback = JsonSerializer.Deserialize<RouteCatalogGenerationReadback>(await ReadRequiredAsync(Path.Combine(catalogDirectory, host + ".committed.json"), cancellationToken).ConfigureAwait(false))
                ?? throw new InvalidOperationException("A real catalog participant readback is absent.");
            if (!readback.Serving || readback.RootDigest != scope.RoutingCatalogRoot
                || readback.Generation != catalog.Generation || readback.Profile != catalog.Profile
                || readback.RouteFacetDigest != catalog.RouteFacetDigest || readback.IdempotencyFacetDigest != catalog.IdempotencyFacetDigest)
                throw new InvalidOperationException("A real catalog participant did not commit the selected native scope.");
            readbacks.Add(host, readback);
        }
#if HEXALITH_PLATFORM_V2_CATALOG
        var activation = JsonSerializer.Deserialize<RouteCatalogActivation>(await ReadRequiredAsync(Path.Combine(catalogDirectory, "activation.json"), cancellationToken).ConfigureAwait(false))
            ?? throw new InvalidOperationException("The complete committed catalog activation is absent.");
        PlatformCommittedCatalogProof.Validate(activation, canonical, scope.RoutingCatalogRoot, plan.AttemptId, _expectedCatalogHosts, readbacks);
#endif
        if (composition?.CurrentOwnerProof is not null)
            await PlatformCurrentOwnerObservation.CaptureAsync(services, composition, catalogDirectory, cancellationToken).ConfigureAwait(false);
        await WriteProgressAsync("source-validated", null, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan remaining = CompositionStartupClock.Remaining(plan.StartupDeadlineUtc, now);
        // The deadline can elapse before its timer cancels the token; never mint zero-lifetime credentials.
        // Cancellation, not a fault, keeps the runner's startup-deadline classification (HXR021).
        if (remaining <= TimeSpan.Zero)
            throw new OperationCanceledException("The startup deadline elapsed before native owner preparation.", cancellationToken);
        string subject = "g4-operator-" + plan.RunId;
#if HEXALITH_PLATFORM_CANONICAL_SCOPES
        string operatorToken = CompositionTokenFactory.Create(signingKey, PlatformNativeGatewayContract.OperatorIdentity(subject, plan.TenantNamespace, catalog, remaining), now);
#else
        string operatorToken = CompositionTokenFactory.Create(signingKey, new(subject, [TenantIdentity.DefaultTenantId, plan.TenantNamespace],
            TenantCatalogDeclarations.Commands.Select(command => command.Route.Domain).Distinct(StringComparer.Ordinal).ToArray(),
            ["command:submit", "query:read"], true, remaining) { Surface = "ui" }, now);
#endif
        string nativeProbe = CompositionTokenFactory.Create(signingKey, new("g4-native-tests", scope.Tenants, scope.Domains,
            ["command:submit", "query:read"], false, remaining) { Surface = "agent" }, now);
        using var catalogHttp = Client(Endpoint(services, "eventstore"), nativeProbe);
#if HEXALITH_PLATFORM_CANONICAL_SCOPES
        // The complete activation/readbacks above were validated together; do not reread or manufacture an authority here.
        RouteCatalogGenerationReadback gatewayReadback = readbacks["eventstore"];
        string bootstrapScope = PlatformNativeGatewayContract.Command<BootstrapGlobalAdmin>(catalog).PlatformAdmission!.Namespace;
        string tenantScope = PlatformNativeGatewayContract.Command<CreateTenant>(catalog).PlatformAdmission!.Namespace;
        PlatformNativeGatewayContract.Bind(catalogHttp.DefaultRequestHeaders, gatewayReadback);
#endif
        await PlatformRoutingCatalogMetadata.ObserveAsync(catalogHttp, catalog, scope.RoutingCatalogRoot, catalogDirectory, cancellationToken).ConfigureAwait(false);
        using var gatewayHttp = Client(Endpoint(services, "eventstore"), operatorToken);
#if HEXALITH_PLATFORM_CANONICAL_SCOPES
        PlatformNativeGatewayContract.Bind(gatewayHttp.DefaultRequestHeaders, gatewayReadback, bootstrapScope);
#endif
        var gateway = new EventStoreGatewayClient(gatewayHttp, Options.Create(new EventStoreGatewayClientOptions { BaseAddress = gatewayHttp.BaseAddress! }));
        var receipts = new List<CommandStatusQueryResponse>();
        await WriteProgressAsync("bootstrap-submitting", null, cancellationToken).ConfigureAwait(false);
        string bootstrapId = await SubmitBootstrapAsync(services, subject, operatorToken, catalog, readbacks["eventstore"], cancellationToken).ConfigureAwait(false);
        receipts.Add(await WaitForCommittedAsync(gateway, bootstrapId, cancellationToken).ConfigureAwait(false));
        await WriteProgressAsync("bootstrap-committed", null, cancellationToken).ConfigureAwait(false);
#if HEXALITH_PLATFORM_CANONICAL_SCOPES
        using var tenantGatewayHttp = Client(Endpoint(services, "eventstore"), operatorToken);
        PlatformNativeGatewayContract.Bind(tenantGatewayHttp.DefaultRequestHeaders, gatewayReadback, tenantScope);
        gateway = new EventStoreGatewayClient(tenantGatewayHttp, Options.Create(new EventStoreGatewayClientOptions { BaseAddress = tenantGatewayHttp.BaseAddress! }));
#endif
        await WriteProgressAsync("tenant-submitting", null, cancellationToken).ConfigureAwait(false);
        receipts.Add(await SubmitAsync(gateway, new CreateTenant(plan.TenantNamespace, "Synthetic native test tenant", "Run-owned test state"), plan.TenantNamespace, catalog, cancellationToken).ConfigureAwait(false));
        await WriteProgressAsync("tenant-committed", null, cancellationToken).ConfigureAwait(false);
        await WriteProgressAsync("member-submitting", null, cancellationToken).ConfigureAwait(false);
        receipts.Add(await SubmitAsync(gateway, new AddUserToTenant(plan.TenantNamespace, "g4-native-tests", TenantRole.TenantReader), plan.TenantNamespace, catalog, cancellationToken).ConfigureAwait(false));
        await WriteProgressAsync("member-committed", null, cancellationToken).ConfigureAwait(false);
        using var tenants = Client(Endpoint(services, "tenants-api"), nativeProbe);
        await WriteProgressAsync("tenants-native-observing", null, cancellationToken).ConfigureAwait(false);
        await PollAsync("tenants-native-observing", tenants, "api/tenants/" + Uri.EscapeDataString(plan.TenantNamespace), element =>
            element.TryGetProperty("tenantId", out var tenant) && tenant.GetString() == plan.TenantNamespace, cancellationToken).ConfigureAwait(false);
        await WriteProgressAsync("tenants-native-observed", null, cancellationToken).ConfigureAwait(false);
        PlatformNativeInvocationAccess memoriesAccess = await MemoriesInvocationAsync(services, cancellationToken).ConfigureAwait(false);
        CompositionNativeHttpEndpoint memoriesEndpoint = memoriesAccess.Endpoint;
        string daprToken = memoriesAccess.ApiToken;
        using var memories = Client(memoriesEndpoint.Uri, nativeProbe);
        memories.DefaultRequestHeaders.Add("dapr-api-token", daprToken);
        await WriteProgressAsync("memories-submit-started", null, cancellationToken).ConfigureAwait(false);
        using (var response = await memories.PostAsJsonAsync("tenants", new TenantProvisioningInput(plan.TenantNamespace, "Synthetic native test tenant")
            { VectorDimensions = 3 }, JsonOptions, cancellationToken).ConfigureAwait(false))
        {
            await WriteProgressAsync(response.StatusCode == HttpStatusCode.Accepted ? "memories-submit-accepted" : "memories-submit-refused",
                (int)response.StatusCode, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.Accepted)
                throw new InvalidOperationException("The real Memories tenant provisioning workflow was not accepted: " + (int)response.StatusCode);
        }
        await WriteProgressAsync("memories-active-observing", null, cancellationToken).ConfigureAwait(false);
        await PollAsync("memories-active-observing", memories, "tenants/" + Uri.EscapeDataString(plan.TenantNamespace), element =>
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
            routingCatalogMetadataObserved = true,
            tenantsNativeQueryObserved = true, memoriesRegistryObserved = nameof(Hexalith.Memories.Contracts.V1.TenantStatus.Active), observedAtUtc = DateTimeOffset.UtcNow,
        }), cancellationToken).ConfigureAwait(false);
        await WriteEndpointCredentialsAsync(credentialPath, plan, memoriesEndpoint, daprToken, cancellationToken).ConfigureAwait(false);
        NativeHttpEndpoints = [memoriesEndpoint];
        NativeEndpointCredentialsPath = credentialPath;
        NativeTestScope = scope;
    }

    private static async Task<CommandStatusQueryResponse> SubmitAsync<T>(IEventStoreGatewayClient gateway, T command, string aggregateId, RouteCatalogEnvelope catalog, CancellationToken token) where T : ICommandContract
    {
        string messageId = UniqueIdHelper.GenerateSortableUniqueStringId();
#if HEXALITH_PLATFORM_CANONICAL_SCOPES
        string tenant = PlatformNativeGatewayContract.Command<T>(catalog).PlatformAdmission!.Namespace;
#else
        string tenant = TenantIdentity.DefaultTenantId;
#endif
        await gateway.SubmitCommandAsync(new(messageId, tenant, T.Domain, aggregateId, T.CommandType,
            JsonSerializer.SerializeToElement(command, EventStorePayloadSerialization.Options), CorrelationId: messageId), token).ConfigureAwait(false);
        return await WaitForCommittedAsync(gateway, messageId, token).ConfigureAwait(false);
    }

    private async Task<string> SubmitBootstrapAsync(IServiceProvider services, string operatorSubject, string operatorToken, RouteCatalogEnvelope catalog, RouteCatalogGenerationReadback gatewayReadback, CancellationToken token)
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
        var credentials = credentialServices.BuildServiceProvider();
        await using var credentialsLifetime = credentials.ConfigureAwait(false);
        string? assertion = await credentials.GetRequiredService<IWorkloadAssertionIssuer>().IssueAsync(new(audience,
            EventStoreGatewayWorkloadOperations.CommandSubmit, new Dictionary<string, string>
            {
                [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = TenantIdentity.DefaultTenantId,
                [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = declaration.Domain,
            }), token).ConfigureAwait(false);
        if (string.IsNullOrEmpty(assertion)) throw new InvalidOperationException("The actual owner bootstrap workload assertion is unavailable.");
        string messageId = UniqueIdHelper.GenerateSortableUniqueStringId();
#if HEXALITH_PLATFORM_CANONICAL_SCOPES
        string scope = PlatformNativeGatewayContract.Command<BootstrapGlobalAdmin>(catalog).PlatformAdmission!.Namespace;
#else
        string scope = TenantIdentity.DefaultTenantId;
#endif
        var command = new SubmitCommandRequest(messageId, scope, declaration.Domain, TenantIdentity.GlobalAdministratorsAggregateId,
            BootstrapGlobalAdmin.CommandType, JsonSerializer.SerializeToElement(new BootstrapGlobalAdmin(operatorSubject), EventStorePayloadSerialization.Options), CorrelationId: messageId);
        var bound = new BoundWorkloadCommandRequest(command, operatorSubject);
        string proof = BoundWorkloadCommandProof.Issue(bound, tenantBootstrap.AppId, tenantBootstrap.PrivateKey, DateTimeOffset.UtcNow);
        using var transport = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
        { BaseAddress = new Uri("http://127.0.0.1:" + port + "/"), Timeout = Timeout.InfiniteTimeSpan };
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "v1.0/invoke/" + Uri.EscapeDataString(audience) + "/method/api/v1/commands/bound/workload")
        { Content = JsonContent.Create(bound, options: JsonOptions) };
#if HEXALITH_PLATFORM_CANONICAL_SCOPES
        PlatformNativeGatewayContract.BindBootstrap(request.Headers, gatewayReadback, scope, operatorToken);
#endif
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

    /// <summary>Observes the actual owner readiness predicate within the existing startup budget; internal visibility permits controlled transport checks.</summary>
    internal async Task PollAsync(string step, HttpClient client, string path, Func<JsonElement, bool> ready, CancellationToken token)
    {
        while (true)
        {
            RequirePollingBudget(token);
            using var response = await client.GetAsync(path, token).ConfigureAwait(false);
            RequirePollingBudget(token);
            await WriteProgressAsync(step, (int)response.StatusCode, token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                RetryConditionHeaderValue? retry = response.Headers.RetryAfter;
                TimeSpan delay = retry?.Delta ?? (retry?.Date is { } date ? date - DateTimeOffset.UtcNow : TimeSpan.Zero);
                if (delay <= TimeSpan.Zero) delay = TimeSpan.FromSeconds(1);
                TimeSpan remaining = CompositionStartupClock.Remaining(plan.StartupDeadlineUtc, DateTimeOffset.UtcNow);
                if (delay >= remaining)
                {
                    await Task.Delay(remaining, token).ConfigureAwait(false);
                    throw new OperationCanceledException("The startup deadline elapsed during native owner readiness observation.", token);
                }
                await Task.Delay(delay, token).ConfigureAwait(false);
                continue;
            }
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false));
                bool observedReady = ready(document.RootElement);
                RequirePollingBudget(token);
                if (observedReady) return;
            }
            else if (response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Conflict or HttpStatusCode.ServiceUnavailable))
                throw new InvalidOperationException("The real owner readiness observation failed: " + (int)response.StatusCode);
            await Task.Delay(100, token).ConfigureAwait(false);
        }
    }

    private void RequirePollingBudget(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (CompositionStartupClock.Remaining(plan.StartupDeadlineUtc, DateTimeOffset.UtcNow) <= TimeSpan.Zero)
            throw new OperationCanceledException("The startup deadline elapsed during native owner readiness observation.", token);
    }

    private async Task<PlatformNativeInvocationAccess> MemoriesInvocationAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        DistributedApplicationModel model = services.GetRequiredService<DistributedApplicationModel>();
        IResource application = model.Resources.Single(resource => resource.Name == memoriesBinding.ResourceId);
        PlatformServerBinding actual = application.Annotations.OfType<PlatformServerAnnotation>().Single().Binding;
        if (actual.ResourceId != memoriesBinding.ResourceId || actual.AppId != memoriesBinding.AppId
            || PlatformDeclarationSet.Id(actual.Module.Declaration) != "memories"
            || !actual.Environment.TryGetValue("DAPR_API_TOKEN", out string? token) || string.IsNullOrWhiteSpace(token)
            || token.Length > 8192 || token.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '.' or '_' or '~' or '+' or '/' or '=')))
            throw new InvalidOperationException("The actual owner-bound Memories Dapr credential is unavailable.");
        // BindGeneratedSidecar owns this generated executable name; require the actual resource and allocation.
        ExecutableResource sidecar = model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == application.Name + "-dapr-cli");
        EndpointReference http = sidecar.GetEndpoint("http");
        if (!http.IsAllocated) throw new InvalidOperationException("The actual Memories Dapr HTTP listener is not allocated.");
        var origin = new Uri(http.Url);
        JsonElement surface = actual.Module.Declaration.GetProperty("surfaces").GetProperty("interfaces").EnumerateArray()
            .Single(candidate => candidate.GetProperty("name").GetString() == "memories-api"
                && candidate.GetProperty("server").GetString() == actual.ServerId
                && candidate.GetProperty("protocol").GetString() == "http" && candidate.GetProperty("exposure").GetString() == "public");
        string prefix = surface.GetProperty("routePrefix").GetString() ?? throw new InvalidOperationException("The actual Memories public interface prefix is unavailable.");
        var uri = new Uri(origin, "v1.0/invoke/" + Uri.EscapeDataString(actual.AppId) + "/method/" + prefix.Trim('/') + "/");
        if (!CompositionNativeHttpEndpoints.IsInvocationUri(uri)) throw new InvalidOperationException("The actual Memories invocation URI is unavailable.");
        await WriteProgressAsync("memories-sidecar-observing", null, cancellationToken).ConfigureAwait(false);
        using var probe = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
        { BaseAddress = origin, Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 65536 };
        probe.DefaultRequestHeaders.Add("dapr-api-token", token);
        using var response = await probe.GetAsync("v1.0/metadata", cancellationToken).ConfigureAwait(false);
        await WriteProgressAsync("memories-sidecar-observing", (int)response.StatusCode, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("The actual Memories sidecar identity could not be observed.");
        using var metadata = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
        if (!metadata.RootElement.TryGetProperty("id", out var observed) || observed.GetString() != actual.AppId)
            throw new InvalidOperationException("The actual Memories sidecar identity differs from its owner binding.");
        await WriteProgressAsync("memories-sidecar-observed", null, cancellationToken).ConfigureAwait(false);
        return new(new(actual.ResourceId, PlatformDeclarationSet.Id(actual.Module.Declaration), "dapr-invocation", uri, sidecar.Name), token);
    }

    /// <summary>Writes the private <c>hexalith.test-endpoint-credentials.v1</c> channel atomically with owner-only access.</summary>
    internal static async Task WriteEndpointCredentialsAsync(string path, CompositionRunPlan runPlan, CompositionNativeHttpEndpoint endpoint, string apiToken, CancellationToken token)
    {
        string temporary = path + ".tmp";
        var options = new FileStreamOptions { Access = FileAccess.Write, Mode = FileMode.CreateNew, Share = FileShare.None, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        var stream = new FileStream(temporary, options);
        await using (stream.ConfigureAwait(false))
        {
            using var writer = new Utf8JsonWriter(stream);
            writer.WriteStartObject();
            writer.WriteString("schema", NativeTestHandoff.EndpointCredentialsSchema);
            writer.WriteString("runId", runPlan.RunId);
            writer.WriteString("syntheticTenantId", runPlan.TenantNamespace);
            writer.WriteString("dataIsolationId", runPlan.ResourceNamespace);
            writer.WriteStartArray("endpoints");
            writer.WriteStartObject();
            writer.WriteString("resourceId", endpoint.ResourceId);
            writer.WriteString("moduleId", endpoint.ModuleId);
            writer.WriteString("endpointName", endpoint.EndpointName);
            writer.WriteString("uri", endpoint.Uri.AbsoluteUri);
            writer.WriteString("headerName", "dapr-api-token");
            writer.WriteString("value", apiToken);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
            await writer.FlushAsync(token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }
        token.ThrowIfCancellationRequested();
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Publishes only fixed-step partial observations; the final receipt remains the readiness authority.</summary>
    private async Task WriteProgressAsync(string step, int? httpStatus, CancellationToken token)
    {
        if (step is not ("source-validated" or "bootstrap-submitting" or "bootstrap-committed"
            or "tenant-submitting" or "tenant-committed" or "member-submitting" or "member-committed"
            or "tenants-native-observing" or "tenants-native-observed" or "memories-sidecar-observing" or "memories-sidecar-observed" or "memories-submit-started"
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

    private async Task<byte[]> ReadRequiredAsync(string path, CancellationToken token)
    {
        path = Path.GetFullPath(path);
        RequireOwnedInput(path);
        while (!File.Exists(path))
        {
            await Task.Delay(50, token).ConfigureAwait(false);
            RequireOwnedInput(path);
        }
        var file = new FileInfo(path);
        if (file.LinkTarget is not null || (file.Attributes & (FileAttributes.Directory | FileAttributes.Device | FileAttributes.ReparsePoint)) != 0
            || file.Length is <= 0 or > 8388608)
            throw new InvalidOperationException("A native catalog input is linked, empty or exceeds its bounded contract.");
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous);
        await using (stream.ConfigureAwait(false))
        {
            RequireOwnedInput(path);
            byte[] bytes = new byte[checked((int)file.Length)];
            await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
            if (await stream.ReadAsync(new byte[1], token).ConfigureAwait(false) != 0)
                throw new InvalidOperationException("A native catalog input changed while it was read.");
            RequireOwnedInput(path);
            return bytes;
        }
    }

    private void RequireOwnedInput(string path)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(plan.Workspace));
        string relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("A native catalog input is outside the owned workspace.");
        if (new FileInfo(path).LinkTarget is not null)
            throw new InvalidOperationException("A native catalog input is linked inside the owned workspace.");
        DirectoryInfo? parent = new(Path.GetDirectoryName(path)!);
        while (parent is not null)
        {
            if (parent.LinkTarget is not null || File.Exists(parent.FullName) || (parent.Exists && (parent.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0))
                throw new InvalidOperationException("A native catalog input has a linked parent inside the owned workspace.");
            if (string.Equals(parent.FullName, root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return;
            parent = parent.Parent;
        }
        throw new InvalidOperationException("A native catalog input is outside the owned workspace.");
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
