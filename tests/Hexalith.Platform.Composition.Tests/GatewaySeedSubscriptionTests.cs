extern alias platformseed;
extern alias buildsseed;

namespace Hexalith.Platform.Composition.Tests;

using System.Net;
using System.Text.Json;
using Dapr;
using Hexalith.EventStore.Indexes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

public sealed class GatewaySeedSubscriptionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PlatformSeedHonorsConfiguredAndDeclarativeSubscriptionsAsync(bool declarative) => AssertSeedAsync<platformseed::Program>(declarative);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task BuildsPackageSeedHonorsConfiguredAndDeclarativeSubscriptionsAsync(bool declarative) => AssertSeedAsync<buildsseed::Program>(declarative);

    private static async Task AssertSeedAsync<TEntryPoint>(bool declarative) where TEntryPoint : class
    {
        WebApplicationFactory<TEntryPoint> factory = new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dapr:DeclarativeSubscriptions"] = declarative.ToString(),
                ["EventStore:ProjectionChanges:PubSubName"] = "configured-seed-events",
                ["EventStore:OpenApi:Enabled"] = "false",
                ["Authentication:JwtBearer:Issuer"] = "hexalith-test",
                ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
                ["Authentication:JwtBearer:SigningKey"] = "test-only-seed-subscription-signing-key-at-least-32-bytes",
                ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            }));
            builder.ConfigureServices(services =>
            {
                // This HTTP metadata fixture has no sidecar; retain the gateway pipeline while removing only
                // the startup index writer's hosted alias (the same override used by owner HTTP tests).
                int index = services.ToList().FindIndex(service => service.ServiceType == typeof(AdminOperationalIndexHostedService));
                index.ShouldBeGreaterThanOrEqualTo(0);
                ServiceDescriptor hosted = services[index + 2];
                hosted.ServiceType.ShouldBe(typeof(IHostedService));
                hosted.ImplementationFactory.ShouldNotBeNull();
                services.Remove(hosted);
            });
        });
        await using var factoryLifetime = factory.ConfigureAwait(true);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/dapr/subscribe", TestContext.Current.CancellationToken).ConfigureAwait(true);
        RouteEndpoint receiver = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText == "projections/changed");
        if (declarative)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            receiver.Metadata.GetMetadata<ITopicMetadata>().ShouldBeNull();
        }
        else
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using JsonDocument subscriptions = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
            JsonElement projection = subscriptions.RootElement.EnumerateArray().Single(subscription => subscription.GetProperty("topic").GetString() == "*.*.projection-changed");
            projection.EnumerateObject().Single(property => property.Name.Equals("pubsubname", StringComparison.OrdinalIgnoreCase)).Value.GetString().ShouldBe("configured-seed-events");
            receiver.Metadata.GetMetadata<ITopicMetadata>()!.PubsubName.ShouldBe("configured-seed-events");
        }
    }
}
