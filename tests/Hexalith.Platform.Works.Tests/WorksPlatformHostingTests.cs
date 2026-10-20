namespace Hexalith.Platform.Works.Tests;

using System.Net;
using System.Text.Json;
using Hexalith.Platform.Works.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

public sealed class WorksPlatformHostingTests
{
    private static Task EmptyNotFound(Microsoft.AspNetCore.Http.HttpContext context)
    {
        context.Response.StatusCode = 404;
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("/missing", 404)]
    [InlineData("/failure", 500)]
    public async Task PlatformOwnsProblemDetailsWithoutDisclosingException(string route, int status)
    {
        using IHost host = await new HostBuilder().ConfigureWebHost(builder => builder.UseKestrel().UseUrls("http://127.0.0.1:0")
            .ConfigureServices(services => services.AddWorksPlatformHosting())
            .Configure(app => app.Run(context => context.Request.Path == "/failure"
                ? throw new InvalidOperationException("private-exception-detail")
                : EmptyNotFound(context))))
            .StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        // The terminal fallback explicitly supplies an empty 404, as endpoint routing does.
        using HttpClient client = new HttpClient { BaseAddress = new Uri(host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        using HttpResponseMessage response = await client.GetAsync(route, TestContext.Current.CancellationToken).ConfigureAwait(true);
        ((int)response.StatusCode).ShouldBe(status);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        body.ShouldNotContain("private-exception-detail");
        using JsonDocument problem = JsonDocument.Parse(body);
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(status);
    }
}
