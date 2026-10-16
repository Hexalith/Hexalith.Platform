namespace Hexalith.Platform.Composition.Tests;

using System.Net;
using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;

public sealed partial class PlatformAppModelTests
{
    [Theory]
    [InlineData("redis", true)]
    [InlineData("redis", false)]
    [InlineData("memories-vectors", true)]
    [InlineData("memories-vectors", false)]
    [InlineData("memories-graph", true)]
    [InlineData("memories-graph", false)]
    public async Task InfrastructureHealthRequiresRedisProtocolResponseAsync(string name, bool healthy)
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        IDistributedApplicationBuilder builder = Builder();
        PlatformAppModel.Compose(builder, Options());
        IResource[] infrastructure = builder.Resources.Where(resource => resource.Name is "redis" or "memories-vectors" or "memories-graph").ToArray();
        infrastructure.Length.ShouldBe(3);
        infrastructure.ShouldAllBe(resource => resource.Annotations.OfType<HealthCheckAnnotation>().Count() == 1);
        IResource resource = infrastructure.Single(resource => resource.Name == name);
        EndpointAnnotation endpoint = resource.Annotations.OfType<EndpointAnnotation>().Single(value => value.Name == "tcp");
        endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", port);
        string key = resource.Annotations.OfType<HealthCheckAnnotation>().Single().Key;
        await using DistributedApplication app = builder.Build();
        using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(5));
        Task<HealthReport> checking = app.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(registration => registration.Name == key, bound.Token);
        try
        {
            using TcpClient client = await listener.AcceptTcpClientAsync(bound.Token);
            NetworkStream stream = client.GetStream();
            byte[] request = new byte[6];
            await stream.ReadExactlyAsync(request, bound.Token);
            request.ShouldBe("PING\r\n"u8.ToArray());
            // A fragmented response still has to complete the protocol frame before it is accepted.
            await stream.WriteAsync(healthy ? "+PO"u8.ToArray() : "-ER"u8.ToArray(), bound.Token);
            await Task.Delay(20, bound.Token);
            checking.IsCompleted.ShouldBeFalse();
            await stream.WriteAsync(healthy ? "NG\r\n"u8.ToArray() : "R unavailable\r\n"u8.ToArray(), bound.Token);
            HealthReport report = await checking;
            report.Entries.Single().Key.ShouldBe(key);
            report.Status.ShouldBe(healthy ? HealthStatus.Healthy : HealthStatus.Unhealthy);
        }
        finally
        {
            await bound.CancelAsync();
            try { _ = await checking; }
            catch (OperationCanceledException) { }
        }
    }

}
