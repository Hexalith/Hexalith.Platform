namespace Hexalith.Platform.Composition;

using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>Runs the existing fresh-store protocol operation concurrently with local resource startup.</summary>
internal sealed class PlatformGatewayStartup(IServiceProvider services, string runId, string signingKey, string? workspace = null) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await CompositionWriterProtocolCutover.ActivateAsync(ResolveEndpointAsync, runId, signingKey, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (!stoppingToken.IsCancellationRequested)
        {
            if (workspace is not null) PlatformCompositionWorkspace.MarkFailed(workspace, failure.Message);
            throw;
        }
    }

    private async Task<Uri> ResolveEndpointAsync(CancellationToken cancellationToken)
    {
        DistributedApplicationModel model = services.GetRequiredService<DistributedApplicationModel>();
        IResourceWithEndpoints gateway = model.Resources.OfType<IResourceWithEndpoints>().Single(resource => resource.Name == "eventstore");
        while (true)
        {
            EndpointReference? endpoint = gateway.GetEndpoints().SingleOrDefault(candidate => candidate.EndpointName == "http");
            if (endpoint is { IsAllocated: true }) return new Uri(endpoint.Url);
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }
    }
}
