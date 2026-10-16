namespace Hexalith.Platform.Works.Tests;

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Platform.Composition;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

/// <summary>SDK Degraded is never promoted to healthy by its HTTP 200 transport status.</summary>
public sealed class SdkReadinessHealthCheckTests
{
    /// <summary>Only recognized statuses preserve the SDK health meaning.</summary>
    [Theory]
    [InlineData("{\"status\":\"Healthy\"}", HealthStatus.Healthy)]
    [InlineData("{\"status\":\"Degraded\"}", HealthStatus.Degraded)]
    [InlineData("{\"status\":\"Unhealthy\"}", HealthStatus.Unhealthy)]
    [InlineData("{\"status\":\"unknown\"}", HealthStatus.Unhealthy)]
    [InlineData("{}", HealthStatus.Unhealthy)]
    [InlineData("[]", HealthStatus.Unhealthy)]
    public void ReadinessPreservesSdkStatus(string json, HealthStatus expected)
    {
        using JsonDocument response = JsonDocument.Parse(json);
        SdkReadinessHealthCheck.Evaluate(response.RootElement).Status.ShouldBe(expected);
    }
    /// <summary>A successful response that stalls after its headers cannot hold Platform health indefinitely.</summary>
    [Fact]
    public async Task StalledReadinessBodyIsUnhealthyWithinTheTransportDeadline()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
            {
                Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true,
            });
            ExecutableResource resource = builder.AddExecutable("stalled-readiness", "dotnet", Environment.CurrentDirectory)
                .WithHttpEndpoint(name: "http").Resource;
            EndpointAnnotation endpoint = resource.Annotations.OfType<EndpointAnnotation>().Single();
            endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", port,
                EndpointBindingMode.SingleAddress, port.ToString(CultureInfo.InvariantCulture));
            using ServiceProvider services = new ServiceCollection().AddHttpClient().BuildServiceProvider();
            var check = new SdkReadinessHealthCheck(new EndpointReference(resource, endpoint), services.GetRequiredService<IHttpClientFactory>());
            Stopwatch elapsed = Stopwatch.StartNew();
            Task<HealthCheckResult> pending = check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
            using TcpClient accepted = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            await accepted.GetStream().WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 7\r\n\r\nH"), TestContext.Current.CancellationToken);
            HealthCheckResult result = await pending.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            result.Status.ShouldBe(HealthStatus.Unhealthy);
            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(8));
        }
        finally { listener.Stop(); }
    }

    /// <summary>The SDK default plain-text writer preserves Degraded, and malformed bodies refuse.</summary>
    [Theory]
    [InlineData("Healthy", HealthStatus.Healthy)]
    [InlineData("Degraded", HealthStatus.Degraded)]
    [InlineData("Unhealthy", HealthStatus.Unhealthy)]
    [InlineData("true", HealthStatus.Unhealthy)]
    [InlineData("", HealthStatus.Unhealthy)]
    public void DefaultSdkWriterPreservesReadiness(string body, HealthStatus expected)
        => SdkReadinessHealthCheck.EvaluateBody(body).Status.ShouldBe(expected);
}
