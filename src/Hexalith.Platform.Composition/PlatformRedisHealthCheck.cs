namespace Hexalith.Platform.Composition;

using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>Requires a Redis protocol response from the allocated infrastructure endpoint.</summary>
/// <param name="endpoint">The container's allocated Redis endpoint.</param>
internal sealed class PlatformRedisHealthCheck(EndpointReference endpoint) : IHealthCheck
{
    /// <summary>Registers protocol readiness without changing container images, ports, or authentication.</summary>
    /// <param name="builder">The owning application builder.</param>
    /// <param name="resource">The Redis-compatible infrastructure resource.</param>
    internal static void Attach(IDistributedApplicationBuilder builder, IResourceBuilder<ContainerResource> resource)
    {
        string name = resource.Resource.Name + "-redis-ping";
        builder.Services.AddHealthChecks().AddCheck(name, new PlatformRedisHealthCheck(resource.GetEndpoint("tcp")));
        resource.WithHealthCheck(name);
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            using TcpClient client = new();
            await client.ConnectAsync(endpoint.Host, endpoint.Port, bound.Token).ConfigureAwait(false);
            NetworkStream stream = client.GetStream();
            await stream.WriteAsync("PING\r\n"u8.ToArray(), bound.Token).ConfigureAwait(false);
            byte[] response = new byte[7];
            await stream.ReadExactlyAsync(response, bound.Token).ConfigureAwait(false);
            return response.AsSpan().SequenceEqual("+PONG\r\n"u8)
                ? HealthCheckResult.Healthy("Redis answered PING.")
                : HealthCheckResult.Unhealthy("Redis did not answer PING.");
        }
        catch (Exception error) when (error is SocketException or IOException)
        {
            return HealthCheckResult.Unhealthy("Redis protocol readiness is unavailable.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Redis PING exceeded its 3-second bound.");
        }
    }
}
