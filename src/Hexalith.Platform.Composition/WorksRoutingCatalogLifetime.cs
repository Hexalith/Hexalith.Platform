namespace Hexalith.Platform.Composition;

using Microsoft.Extensions.Hosting;

/// <summary>Retains the selected executable metadata and private catalog until the application and its children stop.</summary>
internal sealed class WorksRoutingCatalogLifetime(params IAsyncDisposable[] resources) : IHostedService, IDisposable, IAsyncDisposable
{
    private Task? _disposal;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public ValueTask DisposeAsync() => new(_disposal ??= ReleaseAsync());
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private async Task ReleaseAsync()
    {
        foreach (var resource in resources.Reverse()) await resource.DisposeAsync().ConfigureAwait(false);
    }
}
