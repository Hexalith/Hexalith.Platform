namespace Hexalith.Platform.Composition;

using Hexalith.Builds.Tooling.Runtime;
using Microsoft.Extensions.Hosting;

/// <summary>Removes only the file entry's temporary workspace after the host and sidecars dispose.</summary>
internal sealed class PlatformCompositionWorkspace(string path) : IHostedService, IDisposable
{
    /// <summary>The marker whose presence retains a failed attempt's workspace for inspection.</summary>
    internal const string FailureMarker = "composition-failure.txt";

    internal string Path => path;
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public void Dispose()
    {
        // Failed startup diagnostics remain owned by this attempt after registered-service disposal.
        if (!File.Exists(System.IO.Path.Combine(path, FailureMarker)))
            CompositionWorkspace.TryDeleteAsync(path, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>Retains a failed attempt's workspace without replacing the failure that caused it.</summary>
    internal static void MarkFailed(string workspace, string message)
    {
        try { File.WriteAllText(System.IO.Path.Combine(workspace, FailureMarker), message); }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException) { }
    }
}
