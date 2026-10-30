using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Bounds actual Dapr prefix grants to exact authenticated endpoint references, paths and HTTP verbs.</summary>
public sealed class FiniteRouteCatalogReceiver
{
    /// <summary>The independently captured installed fence implementation.</summary>
    public const string Version = "eventstore-finite-dapr-receiver-v1";
    private readonly Dictionary<(string Path, string Verb), (RouteEndpoint Endpoint, string Operation, string[] Policies)> _allowed = new();
    private readonly TaskCompletionSource _captured = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Func<RouteEndpoint, string?>? _operation;
    private Func<IEnumerable<Endpoint>>? _currentEndpoints;
    private string[] _prefixes = [];
    private RouteCatalogReceiverSurface? _surface;

    /// <summary>Installs the fence only for an explicit Development bootstrap; normal host composition is unaffected.</summary>
    public static WebApplication UseFiniteDevelopmentRouteCatalogReceiver(WebApplication app, Func<RouteEndpoint, string?> operation)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(operation);
        if (string.IsNullOrEmpty(app.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"])) return app;
        var fence = app.Services.GetRequiredService<FiniteRouteCatalogReceiver>();
        fence._currentEndpoints = () => app.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        fence.ReadPrefixes(app.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"]!);
        if (string.IsNullOrEmpty(app.Configuration[DaprAppChannelToken.ConfigurationKey]))
            throw new RouteCatalogUnavailableException("The finite Development receiver requires a configured actual sidecar channel token.");
        app.Use(async (context, next) =>
        {
            // Dapr injects its configured APP_API_TOKEN header on every app-channel delivery. Presence
            // selects stricter transport fencing; existing authorization still validates its actual value.
            if (context.Request.Headers.ContainsKey(DaprAppChannelToken.HeaderName) && fence._surface is null
                && fence.Intersects(context.Request.Path.Value ?? string.Empty))
            { context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable; return; }
            if (context.Request.Headers.ContainsKey(DaprAppChannelToken.HeaderName) && fence.Intersects(context.Request.Path.Value ?? string.Empty))
            {
                if (!DaprAppChannelToken.IsAdmitted(DaprAppChannelToken.Verify(context.Request, app.Configuration, app.Environment)))
                { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
                if (!fence.Allows(context.Request.Path.Value ?? string.Empty, context.Request.Method, context.GetEndpoint()))
                { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
            }
            await next(context).ConfigureAwait(false);
        });
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            try { fence.Capture(app.Services.GetRequiredService<EndpointDataSource>().Endpoints,
                app.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"]!, operation); }
            catch (Exception exception) { fence._captured.TrySetException(exception); throw; }
        });
        return app;
    }

    /// <summary>Returns actual retained metadata only after the receiver has installed its finite allow table.</summary>
    public RouteCatalogReceiverSurface Observe()
    {
        if (_surface is null) throw new RouteCatalogUnavailableException("The actual finite receiver fence has not captured its endpoint inventory.");
        if (_currentEndpoints is null) return _surface;
        return new(Version, ReadEndpoints(_currentEndpoints(), enroll: false));
    }

    /// <summary>Waits for the actual ApplicationStarted inventory capture within the owning startup cancellation bound.</summary>
    public Task WaitForCaptureAsync(CancellationToken token) => _captured.Task.WaitAsync(token);

    internal void Capture(IEnumerable<Endpoint> endpoints, string configurationPath, Func<RouteEndpoint, string?> operation)
    {
        _operation = operation;
        ReadPrefixes(configurationPath);
        _surface = new(Version, ReadEndpoints(endpoints, enroll: true));
        _captured.TrySetResult();
    }

    private void ReadPrefixes(string configurationPath)
    {
        using var configuration = JsonDocument.Parse(File.ReadAllBytes(configurationPath));
        var acl = configuration.RootElement.GetProperty("spec").GetProperty("accessControl");
        var grants = acl.GetProperty("policies").EnumerateArray().SelectMany(policy => policy.GetProperty("operations").EnumerateArray())
            .Where(grant => grant.GetProperty("action").GetString() == "allow")
            .Select(grant => grant.GetProperty("name").GetString() ?? string.Empty).ToArray();
        if (grants.Any(grant => grant.Length == 0 || grant.Contains('*', StringComparison.Ordinal)
            && (!grant.EndsWith("/*", StringComparison.Ordinal) || grant.Count(c => c == '*') != 1)))
            throw new RouteCatalogUnavailableException("The actual receiver ACL contains an unsupported wildcard shape.");
        _prefixes = grants.Select(grant => "/" + grant.Trim('/').TrimEnd('*').TrimEnd('/')).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private RouteCatalogReceiverEndpoint[] ReadEndpoints(IEnumerable<Endpoint> endpoints, bool enroll)
    {
        var inventory = new List<RouteCatalogReceiverEndpoint>();
        foreach (var endpoint in endpoints)
        {
            if (endpoint is not RouteEndpoint route)
                throw new RouteCatalogUnavailableException("An unknown endpoint cannot qualify the actual receiver surface.");
            string pattern = "/" + (route.RoutePattern.RawText ?? throw new RouteCatalogUnavailableException("An actual endpoint lacks its route pattern.")).Trim('/');
            string[] methods = route.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Order(StringComparer.Ordinal).ToArray() ?? [];
            string[] policies = route.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy ?? string.Empty).Order(StringComparer.Ordinal).ToArray();
            bool anonymous = route.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            bool isPattern = route.RoutePattern.Parameters.Count > 0 || pattern.Contains('{', StringComparison.Ordinal) || pattern.Contains('*', StringComparison.Ordinal);
            string? canonical = _operation!(route);
            bool admitted = !anonymous && !isPattern && methods.Length > 0 && policies.Length > 0 && policies.All(p => p.Length > 0) && !string.IsNullOrEmpty(canonical);
            if (admitted && enroll)
                foreach (string verb in methods)
                    if (!_allowed.TryAdd((pattern.ToLowerInvariant(), verb), (route, canonical!, policies)))
                        throw new RouteCatalogUnavailableException("The finite receiver has ambiguous concrete operations.");
            if (!enroll) admitted = admitted && methods.All(verb => Allows(pattern, verb, route));
            inventory.Add(new(pattern, methods, canonical, policies, anonymous, isPattern, admitted));
        }
        return inventory.OrderBy(e => e.Pattern, StringComparer.Ordinal).ThenBy(e => string.Join(',', e.Methods), StringComparer.Ordinal).ToArray();
    }

    private bool Intersects(string path) => _prefixes.Any(prefix => path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));

    internal bool Allows(string path, string verb, Endpoint? endpoint) => _surface is not null
        && _allowed.TryGetValue((path.ToLowerInvariant(), verb), out var actual) && ReferenceEquals(actual.Endpoint, endpoint)
        && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
        && endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(verb, StringComparer.Ordinal) == true
        && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy ?? string.Empty).Order(StringComparer.Ordinal)
            .SequenceEqual(actual.Policies, StringComparer.Ordinal)
        && _operation!(actual.Endpoint) == actual.Operation;
}
