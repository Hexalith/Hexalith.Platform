namespace Hexalith.Platform.Composition.Tests;

using System.Text;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

public sealed class WriterProtocolStartupTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CutoverRequiresEveryOtherGatewayDependencyToBeHealthyAsync(bool onlyProtocolUnhealthy)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        await using WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        int activated = 0;
        app.MapGet("/health", () => Results.Json(new
        {
            results = new Dictionary<string, object>
            {
                ["projection-delivery-writer-protocol"] = new { status = "Unhealthy" },
                ["dapr-actor-placement"] = new { status = onlyProtocolUnhealthy ? "Healthy" : "Unhealthy" },
            },
        }, statusCode: 503));
        app.MapPost("/api/v1/admin/projections/delivery-writer-protocol/activate", async (HttpRequest request) =>
        {
            using JsonDocument attestation = await JsonDocument.ParseAsync(request.Body).ConfigureAwait(true);
            attestation.RootElement.GetProperty("cutoverCommit").GetString().ShouldBe("hexalith-g4-run-unit-test");
            attestation.RootElement.GetProperty("writersQuiesced").GetBoolean().ShouldBeTrue();
            string compactToken = request.Headers.Authorization.ToString()["Bearer ".Length..];
            string payload = compactToken.Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
            using JsonDocument claims = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            claims.RootElement.GetProperty("role").GetString().ShouldBe("GlobalAdministrator");
            claims.RootElement.GetProperty("iss").GetString().ShouldBe(CompositionEnvironment.TokenIssuer);
            activated++;
            return Results.Ok();
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        Uri endpoint = new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        if (!onlyProtocolUnhealthy) cancellation.CancelAfter(TimeSpan.FromMilliseconds(300));
        IDistributedApplicationBuilder modelBuilder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true });
        ExecutableResource gateway = modelBuilder.AddExecutable("eventstore", "dotnet", Environment.CurrentDirectory).WithHttpEndpoint(name: "http").Resource;
        EndpointAnnotation allocation = gateway.Annotations.OfType<EndpointAnnotation>().Single();
        using ServiceProvider services = new ServiceCollection().AddSingleton(new DistributedApplicationModel([gateway])).BuildServiceProvider();
        using PlatformGatewayStartup startup = new(services, "unit-test", "UnitTestSigningKeyOfAtLeast32Characters");
        await startup.StartAsync(cancellation.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
        activated.ShouldBe(0);
        allocation.AllocatedEndpoint = new AllocatedEndpoint(allocation, "127.0.0.1", endpoint.Port, EndpointBindingMode.SingleAddress, endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Task activation = startup.ExecuteTask!;
        if (onlyProtocolUnhealthy) await activation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => activation);
        activated.ShouldBe(onlyProtocolUnhealthy ? 1 : 0);
        await startup.StopAsync(TestContext.Current.CancellationToken);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }
}
