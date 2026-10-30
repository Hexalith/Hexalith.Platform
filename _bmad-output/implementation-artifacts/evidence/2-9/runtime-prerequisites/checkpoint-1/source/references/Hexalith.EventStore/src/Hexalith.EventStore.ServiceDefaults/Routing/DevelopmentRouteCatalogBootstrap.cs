using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Explicit, private-file coordinated Development activation by the actual participating hosts.</summary>
public static class DevelopmentRouteCatalogBootstrap
{
    /// <summary>Registers independently observed host inputs and an optional coordinator constructor.
    /// The configuration is deliberately inert unless an owning Development runner supplies its private directory.</summary>
    public static IServiceCollection AddEventStoreDevelopmentRouteCatalogBootstrap(this IServiceCollection services,
        IConfiguration configuration, Func<IServiceProvider, CancellationToken, Task<RouteCatalogHostObservation>> observe,
        Func<IReadOnlyList<RouteCatalogHostObservation>, RouteCatalogEnvelope>? construct = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(observe);
        var section = configuration.GetSection("EventStore:RouteCatalog:DevelopmentBootstrap");
        if (string.IsNullOrEmpty(section["Directory"])) return services;
        string directory = Path.GetFullPath(section["Directory"]!);
        string host = Identifier(section["HostId"]);
        string coordinator = Identifier(section["CoordinatorHostId"]);
        string attempt = Identifier(section["AttemptId"]);
        if (section["CatalogPath"] is { Length: > 0 } catalogPath)
        {
            string instance = Identifier(section["InstanceId"]);
            if (Path.GetFullPath(catalogPath) != Path.Combine(directory, "catalog.json"))
                throw new InvalidOperationException("A catalog from a different private run directory cannot bind this host.");
            using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "receipt.json")));
            if (receipt.RootElement.GetProperty("instanceId").GetString() != instance
                || receipt.RootElement.GetProperty("rootDigest").GetString() != section["RootDigest"])
                throw new InvalidOperationException("The catalog instance differs from this environment descriptor binding.");
            _ = RouteCatalogCodec.Decode(File.ReadAllBytes(catalogPath), section["RootDigest"]);
        }
        string[] hosts = section.GetSection("RequiredHostIds").Get<string[]>() ?? [];
        if (!Path.IsPathFullyQualified(section["Directory"]!) || !System.IO.Directory.Exists(directory)
            || hosts.Length == 0 || hosts.Any(id => Identifier(id) != id)
            || hosts.Distinct(StringComparer.Ordinal).Count() != hosts.Length
            || !hosts.Contains(host, StringComparer.Ordinal) || !hosts.Contains(coordinator, StringComparer.Ordinal)
            || (host == coordinator) != (construct is not null))
            throw new InvalidOperationException("An exact, private Development catalog composition is required.");

        async Task<RouteCatalogHostObservation> Capture(IServiceProvider provider, CancellationToken token)
        {
            var actual = await observe(provider, token).ConfigureAwait(false);
            if (actual.HostId != host || !actual.RequiredHostIds.Order(StringComparer.Ordinal).SequenceEqual(hosts.Order(StringComparer.Ordinal), StringComparer.Ordinal))
                throw new RouteCatalogUnavailableException("The actual host inventory differs from its owning Development composition.");
            return actual with { AuthorityCoordinates = provider.GetRequiredService<IRouteCatalogStateStore>().Coordinates
                ?? throw new RouteCatalogUnavailableException("The authority has no independently selected coordinates.") };
        }
        services.AddEventStoreRouteCatalog(configuration);
        services.AddSingleton<FiniteRouteCatalogReceiver>();
        services.AddEventStoreRouteCatalogObservation(Capture);
        services.Configure<RouteCatalogHostOptions>(options => { options.Profile = "Development"; options.DevelopmentFixture = true; });
        services.AddSingleton<IHostedService>(provider => new Worker(provider, directory, host, coordinator, attempt, hosts, Capture, construct));
        return services;
    }

    private static string Identifier(string? value) => value is { Length: > 0 and <= 128 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? value
        : throw new InvalidOperationException("A bounded Development composition identifier is required.");

    internal static async Task ActivateAsync(IServiceProvider provider, string directory, string host, string coordinator,
        string attempt, IReadOnlyList<string> hosts, Func<IServiceProvider, CancellationToken, Task<RouteCatalogHostObservation>> capture,
        Func<IReadOnlyList<RouteCatalogHostObservation>, RouteCatalogEnvelope>? construct, CancellationToken token)
    {
        var actual = await capture(provider, token).ConfigureAwait(false);
        // This identity is calculated before a proposal exists and is recaptured by the authority on every lifecycle operation.
        provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value.Identity = ObservedRouteCatalogHost.Identity(actual);
        await WriteAsync(Path.Combine(directory, host + ".observation.json"), actual, token).ConfigureAwait(false);
        string activationPath = Path.Combine(directory, "activation.json");
        if (host == coordinator)
        {
            List<RouteCatalogHostObservation> observations = [];
            foreach (string required in hosts)
            {
                var observed = await ReadAsync<RouteCatalogHostObservation>(Path.Combine(directory, required + ".observation.json"), token).ConfigureAwait(false);
                if (observed.HostId != required || !observed.RequiredHostIds.Order(StringComparer.Ordinal).SequenceEqual(hosts.Order(StringComparer.Ordinal), StringComparer.Ordinal))
                    throw new RouteCatalogUnavailableException("A participant supplied a different exact host inventory.");
                observations.Add(observed);
            }
            var catalog = construct!(observations);
            if (catalog.Profile != "Development" || catalog.SchemaVersion is not (2 or 3))
                throw new RouteCatalogUnavailableException("Development bootstrap requires a complete executable catalog.");
            var candidate = new RouteCatalogActivation(attempt, RouteCatalogCodec.Encode(catalog), RouteCatalogCodec.ComputeRootDigest(catalog), null,
                observations.Select(ObservedRouteCatalogHost.Identity).ToArray(), "Development-fixture", string.Empty,
                observations.SelectMany(o => o.AuxiliaryOperations!).ToArray(), observations.SelectMany(o => o.ProjectionCapabilities!).ToArray());
            candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
            await WriteAsync(activationPath, candidate, token).ConfigureAwait(false);
        }
        var activation = await ReadAsync<RouteCatalogActivation>(activationPath, token).ConfigureAwait(false);
        if (activation.AttemptId != attempt) throw new RouteCatalogUnavailableException("A different activation attempt occupies the private directory.");
        var authority = provider.GetRequiredService<IRouteCatalogAuthority>();
        await RetryBackendAsync(() => authority.PrepareAsync(activation, token), token).ConfigureAwait(false);
        var ready = await authority.ReadyAsync(attempt, token).ConfigureAwait(false);
        await WriteAsync(Path.Combine(directory, host + ".ready.json"), ready, token).ConfigureAwait(false);
        foreach (string required in hosts)
        {
            var receipt = await ReadAsync<RouteCatalogHostAttestation>(Path.Combine(directory, required + ".ready.json"), token).ConfigureAwait(false);
            if (receipt.AttemptId != attempt || receipt.BindingDigest != ready.BindingDigest || receipt.Host.HostId != required)
                throw new RouteCatalogUnavailableException("A private coordination receipt belongs to a different activation.");
        }
        // Every participant commits through its own independently observing authority. Files alone never activate dispatch.
        await authority.CommitAsync(attempt, token).ConfigureAwait(false);
        var readback = await authority.ReadbackAsync(token).ConfigureAwait(false);
        if (!readback.Serving || readback.RootDigest != activation.RootDigest)
            throw new RouteCatalogUnavailableException("The local host did not observe its committed generation.");
        await WriteAsync(Path.Combine(directory, host + ".committed.json"), readback, token).ConfigureAwait(false);
    }

    private static async Task RetryBackendAsync(Func<Task> action, CancellationToken token)
    {
        while (true)
        {
            try { await action().ConfigureAwait(false); return; }
            catch (RouteCatalogUnavailableException exception) when (exception.Category == RouteCatalogRefusalReason.Backend)
            { await Task.Delay(100, token).ConfigureAwait(false); }
        }
    }

    private static async Task<T> ReadAsync<T>(string path, CancellationToken token)
    {
        while (!File.Exists(path)) await Task.Delay(50, token).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(await File.ReadAllBytesAsync(path, token).ConfigureAwait(false))
            ?? throw new RouteCatalogUnavailableException("The private Development coordination document is invalid.");
    }

    private static async Task WriteAsync<T>(string path, T value, CancellationToken token)
    {
        string pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        var stream = new FileStream(pending, options);
        await using (stream.ConfigureAwait(false))
            await JsonSerializer.SerializeAsync(stream, value, cancellationToken: token).ConfigureAwait(false);
        File.Move(pending, path, overwrite: false);
    }

    private sealed class Worker(IServiceProvider provider, string directory, string host, string coordinator, string attempt,
        string[] hosts, Func<IServiceProvider, CancellationToken, Task<RouteCatalogHostObservation>> capture,
        Func<IReadOnlyList<RouteCatalogHostObservation>, RouteCatalogEnvelope>? construct) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!provider.GetRequiredService<IHostEnvironment>().IsDevelopment())
                throw new InvalidOperationException("Private Development catalog bootstrap cannot run in another environment.");
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = provider.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() => started.TrySetResult());
            await started.Task.WaitAsync(stoppingToken).ConfigureAwait(false);
            await ActivateAsync(provider, directory, host, coordinator, attempt, hosts, capture, construct, stoppingToken).ConfigureAwait(false);
            provider.GetRequiredService<ILogger<Worker>>().LogInformation("Development catalog host {HostId} committed its independently observed generation.", host);
        }
    }
}
