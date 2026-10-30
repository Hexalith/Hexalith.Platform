using System.Reflection;

using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>Explicit Development enrollment of the compiled owner contracts and actual installed domain receivers.</summary>
public static class DevelopmentDomainRouteCatalog
{
    /// <summary>Enables run-private catalog preparation only when the owning runner supplies bootstrap configuration.</summary>
    public static IServiceCollection AddEventStoreDevelopmentDomainCatalog(this IServiceCollection services,
        IConfiguration configuration, params Assembly[] contractOwners)
    {
        ArgumentNullException.ThrowIfNull(contractOwners);
        return services.AddEventStoreDevelopmentRouteCatalogBootstrap(configuration, async (provider, token) =>
        {
            using var scope = provider.CreateScope();
            var local = scope.ServiceProvider;
            var declarations = CompiledRouteCatalogDeclarations.Read(contractOwners);
            var endpoints = provider.GetRequiredService<EndpointDataSource>().Endpoints;
            var identity = local.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
            var fence = provider.GetRequiredService<FiniteRouteCatalogReceiver>();
            await fence.WaitForCaptureAsync(token).ConfigureAwait(false);
            var actual = await DevelopmentRouteCatalogInputs.CaptureAsync(configuration, identity.AppId, declarations,
                Targets(local, endpoints, declarations), [], RouteCatalogDomainObservation.Capabilities(local),
                RouteCatalogDomainObservation.Auxiliary(local, endpoints), token).ConfigureAwait(false);
            return actual with { ReceiverSurface = fence.Observe(), Operations = CompiledRouteCatalogDeclarations.ReadOperations(contractOwners) };
        });
    }

    /// <summary>Captures targets from compiled declarations only after comparing the complete actual local query/projection inventory and canonical receiver policies.</summary>
    public static IReadOnlyList<RouteCatalogEntry> Targets(IServiceProvider services, IEnumerable<Endpoint> endpoints,
        IReadOnlyList<RouteContractDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(declarations);
        var discovery = services.GetRequiredService<DiscoveryResult>();
        var queries = services.GetServices<IDomainQueryHandler>().ToArray();
        var named = services.GetServices<IAsyncDomainProjectionHandler>().ToArray();
        var legacy = services.GetServices<LegacyProjectionCatalogRegistration>().SelectMany(r => r.Declarations).ToArray();
        var identity = services.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value;
        identity.Validate();
        foreach (var aggregate in discovery.Aggregates)
        {
            var processor = services.GetRequiredKeyedService<IDomainProcessor>(aggregate.DomainName);
            var payloads = processor is IDomainCommandPayloadInventory inventory ? inventory.CommandPayloadTypes
                : aggregate.Type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(method => method.Name == "Handle" && method.GetParameters().Length > 0)
                    .Select(method => method.GetParameters()[0].ParameterType).Distinct().ToArray();
            if (payloads.Count == 0 || !payloads.Select(type => type.FullName).Order(StringComparer.Ordinal).SequenceEqual(
                declarations.Where(d => d.Kind == "command" && d.Domain == aggregate.DomainName)
                    .Select(d => d.Execution!.HandlerType).Order(StringComparer.Ordinal), StringComparer.Ordinal))
                throw new RouteCatalogUnavailableException("The compiled commands differ from the actual aggregate or custom processor payload inventory.");
        }
        var actualEndpoints = endpoints.OfType<RouteEndpoint>().ToArray();
        if (queries.Any(query => !declarations.Any(d => d.Kind == "query" && d.Domain == query.Domain && d.MessageType == query.QueryType))
            || named.Any(handler => !declarations.Any(d => d.Kind == "projection" && d.Domain == handler.Domain && d.ProjectionType == handler.ProjectionType)))
            throw new RouteCatalogUnavailableException("An actual query or named projection handler lacks an owner declaration.");
        var targets = new List<RouteCatalogEntry>();
        foreach (var declaration in declarations)
        {
            if (declaration.Execution?.TargetKind != "domain-service")
                throw new RouteCatalogUnavailableException("This Development enrollment owns domain-service receivers only.");
            string method = declaration.Kind switch
            {
                "command" when discovery.Aggregates.Any(a => a.DomainName == declaration.Domain) => "process",
                "query" when queries.Count(q => q.Domain == declaration.Domain && q.QueryType == declaration.MessageType) == 1 => "query",
                "projection" when named.Any(h => h.Domain == declaration.Domain && h.ProjectionType == declaration.ProjectionType
                    && h.GetType().FullName == declaration.Execution.HandlerType) => "project/v2",
                "projection" when legacy.Any(d => RouteCatalogCodec.ComputeDeclarationDigest(d) == RouteCatalogCodec.ComputeDeclarationDigest(declaration)) => "project",
                _ => throw new RouteCatalogUnavailableException("A compiled declaration has no matching installed receiver."),
            };
            var endpoint = actualEndpoints.SingleOrDefault(e => e.RoutePattern.RawText == "/" + method);
            var canonical = EventStoreDomainServiceRoutes.Operational.Single(route => route.Route == "/" + method);
            if (endpoint is null || endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.SequenceEqual(["POST"], StringComparer.Ordinal) != true
                || !endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any(a => a.Policy == canonical.Policy)
                || endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null
                || declaration.Operation != canonical.Operation)
                throw new RouteCatalogUnavailableException("An actual receiver endpoint disagrees with its declared operation.");
            targets.Add(new(declaration.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration), identity.AppId,
                method, identity.ServiceVersion, declaration.CredentialKind, declaration.Operation, false, "*"));
        }
        return targets.AsReadOnly();
    }
}
