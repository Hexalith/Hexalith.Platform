#:sdk Aspire.AppHost.Sdk@13.6.3
#:property ManagePackageVersionsCentrally=false
#:package CommunityToolkit.Aspire.Hosting.Dapr@13.6.1-beta.935
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Microsoft.Extensions.DependencyInjection;

// Reproduces the Works preview: a CommunityToolkit sidecar with no Dapr ports, made proxyless at BeforeStart.
var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = args, DisableDashboard = true });
builder.AddExecutable("probe", "sleep", builder.AppHostDirectory, "300").WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = "probe" }));
builder.Services.AddSingleton<IDistributedApplicationEventingSubscriber, Proxyless>();
using var app = builder.Build();
await app.StartAsync();
var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
int? http = null;
try
{
    await foreach (var evt in notifications.WatchAsync(cts.Token))
    {
        if (!evt.Resource.Name.EndsWith("-dapr-cli", StringComparison.Ordinal)) continue;
        Console.WriteLine($"STATE {evt.Resource.Name} {evt.Snapshot.State?.Text}");
        if (evt.Snapshot.State?.Text is "Running")
        {
            foreach (var e in evt.Resource.Annotations.OfType<EndpointAnnotation>())
            {
                Console.WriteLine($"EP {e.Name} port={e.Port} target={e.TargetPort} proxied={e.IsProxied} allocated={e.AllocatedEndpoint?.Port}");
                if (e.Name == "http") http = e.AllocatedEndpoint?.Port;
            }
            break;
        }
        if (evt.Snapshot.State?.Text is "FailedToStart" or "Exited" or "Finished") break;
    }
}
catch (OperationCanceledException) { Console.WriteLine("TIMEOUT"); }
if (http is int port)
{
    using var client = new HttpClient();
    for (int attempt = 0; attempt < 60; attempt++)
    {
        try
        {
            using var response = await client.GetAsync($"http://127.0.0.1:{port}/v1.0/healthz/outbound");
            Console.WriteLine($"HEALTHZ {port} {(int)response.StatusCode}");
            break;
        }
        catch (HttpRequestException) { await Task.Delay(500); }
    }
}
await app.StopAsync();

sealed class Proxyless : IDistributedApplicationEventingSubscriber
{
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        _ = eventing.Subscribe<BeforeStartEvent>((startup, _) =>
        {
            foreach (ExecutableResource sidecar in startup.Model.Resources.OfType<ExecutableResource>().Where(resource => resource.Name.EndsWith("-dapr-cli", StringComparison.Ordinal)))
            foreach (EndpointAnnotation endpoint in sidecar.Annotations.OfType<EndpointAnnotation>().Where(endpoint => endpoint.Name is "http" or "grpc" or "metrics"))
            {
                // Same mutation as PlatformDaprEndpointSubscriber.Apply.
                endpoint.TargetPort = endpoint.Port;
                endpoint.IsProxied = false;
                Console.WriteLine($"MUTATED {sidecar.Name}/{endpoint.Name} port={endpoint.Port?.ToString() ?? "null"}");
            }
            return Task.CompletedTask;
        });
        return Task.CompletedTask;
    }
}
