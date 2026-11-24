using Microsoft.AspNetCore.Hosting;

[assembly: HostingStartup(typeof(Hexalith.Platform.Composition.Tests.PlatformRealmProbeHostingStartup))]

namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Streams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>Executes the opt-in realm smoke probe using the normal Works host's SDK client.</summary>
public sealed class PlatformRealmProbeHostingStartup : IHostingStartup
{
    /// <inheritdoc />
    public void Configure(IWebHostBuilder builder)
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_REALM_PROBE_DIRECTORY") is not { Length: > 0 } directory) return;
        if (Environment.GetEnvironmentVariable("WORKS_PLATFORM_RUNTIME") != "1" || !Directory.Exists(directory))
            throw new InvalidOperationException("The realm probe requires its owned Works preview.");
        builder.ConfigureServices(services => services.AddHostedService(provider => new Probe(provider, directory)));
    }

    private sealed class Probe(IServiceProvider services, string directory) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string requestPath = Path.Combine(directory, "read-request");
            while (!File.Exists(requestPath)) await Task.Delay(100, stoppingToken).ConfigureAwait(false);
            StreamReadRequest request = JsonSerializer.Deserialize<StreamReadRequest>(
                await File.ReadAllTextAsync(requestPath, stoppingToken).ConfigureAwait(false))
                ?? throw new InvalidOperationException("The realm probe requires a stream read request.");
            var gateway = services.GetRequiredService<IEventStoreGatewayClient>();
            RealmProbeResult[] results =
            [
                await ReadAsync(request).ConfigureAwait(false),
                await ReadAsync(request with { Tenant = "other-realm-tenant" }).ConfigureAwait(false),
                await ReadAsync(request with { Domain = "other-domain" }).ConfigureAwait(false),
            ];
            string result = Path.Combine(directory, "read-result");
            await File.WriteAllTextAsync(result + ".tmp", JsonSerializer.Serialize(results), stoppingToken).ConfigureAwait(false);
            File.Move(result + ".tmp", result);

            async Task<RealmProbeResult> ReadAsync(StreamReadRequest read)
            {
                try
                {
                    await gateway.ReadStreamAsync(read, stoppingToken).ConfigureAwait(false);
                    return new(200, "The fixture stream unexpectedly exists.");
                }
                catch (EventStoreGatewayException failure)
                {
                    return new(failure.StatusCode, failure.Detail ?? failure.Title);
                }
                catch (InvalidOperationException failure)
                {
                    return new(null, failure.Message);
                }
            }
        }
    }
}

internal sealed record RealmProbeResult(int? StatusCode, string? Detail);
