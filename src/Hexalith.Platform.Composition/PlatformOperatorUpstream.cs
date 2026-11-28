namespace Hexalith.Platform.Composition;

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Microsoft.Extensions.DependencyInjection;

/// <summary>The actual allocated endpoints the operator relay forwards to: the Tenants sidecar and the gateway.</summary>
/// <param name="Sidecar">The Tenants sidecar's loopback HTTP API.</param>
/// <param name="SidecarApiToken">The Tenants sidecar API token, when configured.</param>
/// <param name="Gateway">The gateway's HTTP endpoint.</param>
/// <param name="GatewayAppId">The gateway's Dapr application identity and workload audience.</param>
internal sealed record PlatformOperatorUpstream(Uri Sidecar, string? SidecarApiToken, Uri Gateway, string GatewayAppId)
{
    internal const string GatewayResource = "eventstore";

    /// <summary>Resolves the started composition's endpoints from the application model.</summary>
    internal static PlatformOperatorUpstream Resolve(IServiceProvider services, PlatformTenantBootstrapAccess access)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(access);
        DistributedApplicationModel model = services.GetRequiredService<DistributedApplicationModel>();
        IResource owner = model.Resources.Single(resource => resource.Name == access.ResourceId);
        IDaprSidecarResource sidecar = owner.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
        int port = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.DaprHttpPort
            ?? throw new InvalidOperationException("The actual Tenants sidecar endpoint is unavailable.");
        IResource gateway = model.Resources.Single(resource => resource.Name == GatewayResource);
        string appId = gateway.Annotations.OfType<PlatformServerAnnotation>().Single().Binding.AppId;
        EndpointReference endpoint = ((IResourceWithEndpoints)gateway).GetEndpoint("http");
        if (!endpoint.IsAllocated) throw new InvalidOperationException("The actual gateway endpoint is not allocated.");
        return new(new Uri("http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/"), access.ApiToken,
            new Uri(endpoint.Url.TrimEnd('/') + "/"), appId);
    }
}
