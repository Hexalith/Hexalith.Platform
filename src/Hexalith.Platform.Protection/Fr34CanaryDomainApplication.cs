using System.Text.Json;
using Hexalith.EventStore.DomainService;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Protection;

/// <summary>Executable source composition for the dedicated canary domain, protected by the EventStore domain-service workload policies.</summary>
public static class Fr34CanaryDomainApplication
{
    /// <summary>Serves the shipped canary aggregate. Current gateway routing, actor-side protection and independent writer/occurrence authorities must already be installed.</summary>
    public static async Task RunAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddEventStoreDomainService(typeof(Fr34CanaryAggregate).Assembly);
        builder.Services.AddEventStoreBoundedV1DomainSerialization("fr34-canary", profile => profile.Add<Fr34CanaryPersisted>(
            typeof(Fr34CanaryPersisted).FullName!, "json", 32768, static async (payload, output, token) =>
            { await JsonSerializer.SerializeAsync(output, payload, cancellationToken: token).ConfigureAwait(false); }));
        var app = builder.Build();
        app.UseEventStoreDomainService();
        await app.RunAsync().ConfigureAwait(false);
    }
}
