using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Hexalith.Platform.Secrets.Inventory.Tests;

public sealed class InventoryHttpTests
{
    [Fact]
    public async Task CompletedAdapterReachesAuthenticatedReceiverAndPreservesExactMapping()
    {
        await using var server = await InventoryHttpFixture.Start();
        var invoker = new AuthenticatedSecretsInvoker(server.Client, new OfflineWorkloadToken(server.Token("offline-caller")), () => "offline-dapr-api");
        var adapter = new DaprHmacAuthorityAdapter(server.Configuration, invoker);
        adapter.GetCurrentProfile(server.Configuration.ProfileReference!).ShouldBe(server.Configuration.ExpectedProfile);
        var result = await adapter.ResolveAsync("inventory", InventoryBoundaryTests.Scope, "opaque-v7", TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.SecretVersion.ShouldBe("1");
        var absent = new AuthenticatedSecretsInvoker(server.Client, new OfflineWorkloadToken(null), () => "offline-dapr-api");
        (await absent.InvokeAsync("inventory", "query-profile", "{}", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task ReceiverRequiresChannelBearerAndEnrolledIdentityOverRealHttp()
    {
        await using var server = await InventoryHttpFixture.Start();
        async Task<HttpStatusCode> Call(string? jwt, string? channel)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "query-profile") { Content = new StringContent("{\"profileReference\":\"independent-profile\"}", System.Text.Encoding.UTF8, "application/json") };
            if (jwt is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            if (channel is not null) request.Headers.Add("dapr-api-token", channel);
            using var response = await server.Client.SendAsync(request, TestContext.Current.CancellationToken);
            return response.StatusCode;
        }
        (await Call(null, "offline-receiver-channel")).ShouldBe(HttpStatusCode.Unauthorized);
        (await Call(server.Token("offline-caller"), null)).ShouldBe(HttpStatusCode.Unauthorized);
        (await Call(server.Token("foreign-caller"), "offline-receiver-channel")).ShouldBe(HttpStatusCode.Forbidden);
        (await Call(server.Token("offline-caller", operation: false), "offline-receiver-channel")).ShouldBe(HttpStatusCode.Forbidden);
        (await Call(server.Token("offline-caller"), "offline-receiver-channel")).ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("missing-metadata", 503)]
    [InlineData("missing-mapping", 503)]
    [InlineData("missing-authenticated-transport", 503)]
    [InlineData("disabled", 503)]
    [InlineData("missing-binding", 503)]
    [InlineData("allowed", 200)]
    public async Task ReadinessRequiresEveryTupleToResolveCurrently(string scenario, int expected)
    {
        await using var server = await InventoryHttpFixture.Start(scenario);
        using var response = await server.Client.GetAsync("ready", TestContext.Current.CancellationToken);
        ((int)response.StatusCode).ShouldBe(expected);
    }
}
