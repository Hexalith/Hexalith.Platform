extern alias platformseed;
extern alias buildsseed;

namespace Hexalith.Platform.Composition.Tests;

using System.Net;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Dapr;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.Indexes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

[Collection("Platform file entry")]
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

    [Fact]
    public async Task PlatformSeedEnforcesRequestBodyBoundaryWithKestrelAsync()
    {
        using var installation = new GatewaySeedProtectionInstallation();
        WebApplicationFactory<platformseed::Program> factory = CreateFactory<platformseed::Program>(installation, true, "Production");
        await using var factoryLifetime = factory.ConfigureAwait(true);
        factory.UseKestrel(0);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("dapr-api-token", "seed-channel-token");
        CancellationToken token = TestContext.Current.CancellationToken;
        const int limit = 1_048_576;
        using var acceptedByTransport = new StringContent("{}", Encoding.UTF8, "application/json");
        using var exactlyAtLimit = new StringContent("{" + new string(' ', limit - 2) + "}", Encoding.UTF8, "application/json");
        using var overLimit = new StringContent("{" + new string(' ', limit - 1) + "}", Encoding.UTF8, "application/json");
        using HttpResponseMessage small = await client.PostAsync("/projections/changed", acceptedByTransport, token);
        using HttpResponseMessage boundary = await client.PostAsync("/projections/changed", exactlyAtLimit, token);
        using HttpResponseMessage over = await client.PostAsync("/projections/changed", overLimit, token);
        small.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        boundary.StatusCode.ShouldBe(small.StatusCode);
        over.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);

        var actors = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<EventStoreActorCallbackEndpointMetadata>() is not null)
            .ToArray();
        actors.ShouldNotBeEmpty();
        actors.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>()!.MaxRequestBodySize == 30_000_000);
    }

    private static WebApplicationFactory<TEntryPoint> CreateFactory<TEntryPoint>(
        GatewaySeedProtectionInstallation installation, bool declarative, string environment) where TEntryPoint : class =>
        new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(installation.Configuration).AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APP_API_TOKEN"] = "seed-channel-token",
                ["Dapr:DeclarativeSubscriptions"] = declarative.ToString(),
                ["EventStore:ProjectionChanges:PubSubName"] = "configured-seed-events",
                ["EventStore:OpenApi:Enabled"] = "true",
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

    private static async Task AssertSeedAsync<TEntryPoint>(bool declarative) where TEntryPoint : class
    {
        using var installation = new GatewaySeedProtectionInstallation();
        WebApplicationFactory<TEntryPoint> factory = CreateFactory<TEntryPoint>(installation, declarative, "Development");
        await using var factoryLifetime = factory.ConfigureAwait(true);
        using HttpClient client = factory.CreateClient();
        using var anonymous = await client.GetAsync("/dapr/subscribe", TestContext.Current.CancellationToken).ConfigureAwait(false);
        anonymous.StatusCode.ShouldBe(declarative ? HttpStatusCode.NotFound : HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Add("dapr-api-token", "seed-channel-token");
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
        client.DefaultRequestHeaders.Remove("dapr-api-token");
        using var anonymousSwagger = await client.GetAsync("/swagger/index.html", TestContext.Current.CancellationToken).ConfigureAwait(false);
        using var anonymousOpenApi = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken).ConfigureAwait(false);
        bool platform = typeof(TEntryPoint).Assembly.GetName().Name == "Hexalith.Platform.EventStoreHost";
        anonymousSwagger.StatusCode.ShouldBe(platform ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound);
        anonymousOpenApi.StatusCode.ShouldBe(platform ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound);
        string token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "hexalith-test", Audience = "hexalith-eventstore", Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-only-seed-subscription-signing-key-at-least-32-bytes")), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity([new("sub", "seed-human"), new(RouteCatalogSurfacePolicy.ClaimType, "ui")]),
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var bearerSubscription = await client.GetAsync("/dapr/subscribe", TestContext.Current.CancellationToken).ConfigureAwait(false);
        bearerSubscription.StatusCode.ShouldBe(declarative ? HttpStatusCode.NotFound : HttpStatusCode.Unauthorized);
        if (platform)
        {
            using var swagger = await client.GetAsync("/swagger/index.html", TestContext.Current.CancellationToken).ConfigureAwait(false);
            swagger.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await swagger.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)).ShouldContain("swagger-ui");
            using var openApi = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken).ConfigureAwait(false);
            openApi.StatusCode.ShouldBe(HttpStatusCode.OK);
            client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, "unverified-workload-proof");
            using var mixed = await client.GetAsync("/swagger/index.html", TestContext.Current.CancellationToken).ConfigureAwait(false);
            mixed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
    }
}
