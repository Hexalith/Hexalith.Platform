namespace Hexalith.Platform.Composition;

using Hexalith.Builds.Tooling.Runtime;
using Microsoft.Extensions.Hosting;

/// <summary>Removes only the file entry's temporary workspace after the host and sidecars dispose.</summary>
internal sealed class PlatformCompositionWorkspace(string path) : IHostedService, IDisposable
{
    internal string Path => path;
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public void Dispose() => CompositionWorkspace.TryDeleteAsync(path, CancellationToken.None).GetAwaiter().GetResult();
}
