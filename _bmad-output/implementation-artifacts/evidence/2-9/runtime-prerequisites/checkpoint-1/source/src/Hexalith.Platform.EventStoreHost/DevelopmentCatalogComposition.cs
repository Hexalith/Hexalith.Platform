#if HEXALITH_PLATFORM_SOURCE_CATALOG
using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

internal static class DevelopmentCatalogComposition
{
    public static void Register(WebApplicationBuilder builder)
    {
        if (string.IsNullOrEmpty(builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"])) return;
        builder.Configuration["Authentication:BoundStreamRead:AllowedDomains:tenants"] = string.Join(',',
            Hexalith.Tenants.Contracts.Routing.TenantCatalogDeclarations.Commands.Select(command => command.Route.Domain).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        builder.Configuration["Authentication:BoundCommand:AllowedDomains:memories"] = string.Join(',',
            Hexalith.Memories.EventStore.Routing.MemoriesCatalogDeclarations.Commands.Select(command => command.Route.Domain).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        foreach (var adapter in Hexalith.Parties.EventStoreExtensions.PartiesCatalogAdapters.Create()
            .Concat(Hexalith.Tenants.Server.Routing.TenantIntentAdapters.Create())
            .Concat(Hexalith.Memories.EventStoreExtensions.MemoriesCatalogAdapters.Create()))
            builder.Services.AddSingleton<IIdempotencyIntentAdapter>(adapter);
        builder.Services.AddEventStoreDevelopmentRouteCatalogBootstrap(builder.Configuration, async (provider, token) =>
        {
            var declarations = CompiledRouteCatalogDeclarations.Read([
                typeof(Hexalith.Parties.Contracts.Routing.PartiesCatalogDeclarations).Assembly,
                typeof(Hexalith.Tenants.Contracts.Routing.TenantCatalogDeclarations).Assembly,
                typeof(Hexalith.Memories.EventStore.Routing.MemoriesCatalogDeclarations).Assembly]);
            var fence = provider.GetRequiredService<FiniteRouteCatalogReceiver>();
            await fence.WaitForCaptureAsync(token).ConfigureAwait(false);
            var surface = fence.Observe();
            var actual = await DevelopmentRouteCatalogInputs.CaptureAsync(builder.Configuration, "eventstore", declarations, [],
                await RouteCatalogAdmissionObservation.CaptureAsync(provider, "eventstore", token).ConfigureAwait(false), [],
                surface.Endpoints.Where(endpoint => endpoint.Admitted).Select(endpoint =>
                    new Hexalith.EventStore.Contracts.Routing.RouteCatalogAuxiliaryOperation("eventstore", endpoint.Pattern.TrimStart('/'), endpoint.Operation!, "gateway")).ToArray(), token).ConfigureAwait(false);
            return actual with { ReceiverSurface = surface };
        }, observations =>
        {
            var catalog = DevelopmentCompositionRouteCatalog.Build(observations);
            string path = Path.Combine(builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"]!, "native-scope.json");
            File.WriteAllBytes(path, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
            {
                RunId = builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AttemptId"],
                ModuleId = builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:NativeModuleId"],
                RoutingCatalogRoot = Hexalith.EventStore.Contracts.Routing.RouteCatalogCodec.ComputeRootDigest(catalog),
                Domains = Hexalith.Parties.Contracts.Routing.PartiesCatalogDeclarations.Routes.Where(route => route.Kind is "command" or "query")
                    .Select(route => route.Domain).Append(Hexalith.Tenants.Contracts.Queries.GetTenantQuery.Domain).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                Tenants = new[] { builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:TenantNamespace"], Hexalith.Tenants.Contracts.Identity.TenantIdentity.DefaultTenantId },
            }));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return catalog;
        });
    }

    public static string? Operation(RouteEndpoint endpoint)
    {
        string path = (endpoint.RoutePattern.RawText ?? string.Empty).Trim('/');
        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).ToArray();
        foreach (string? policy in policies)
        {
            foreach (string scheme in new[] { DaprInternalAuthenticationOptions.SchemeName, RouteCatalogEndpointExtensions.WorkloadScheme })
                if (policy?.StartsWith(scheme + ":", StringComparison.Ordinal) == true) return policy[(scheme.Length + 1)..];
        }
        if (!policies.Contains(EventStoreHttpHostSecurityExtensions.HumanPolicy, StringComparer.Ordinal)) return null;
        return path switch
        {
            "api/v1/commands" => EventStoreOperationIdentifiers.GatewayCommandSubmit,
            "api/v1/queries" => "eventstore:query-read",
            "api/v1/streams/read" => EventStoreOperationIdentifiers.GatewayStreamRead,
            _ => null,
        };
    }
}
#endif
