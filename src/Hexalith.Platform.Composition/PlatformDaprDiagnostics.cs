namespace Hexalith.Platform.Composition;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Hexalith.Builds.Tooling.Runtime;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Captures native sidecar evidence through public Aspire services without changing process ownership.</summary>
internal sealed class PlatformDaprDiagnostics(string directory, IReadOnlyList<string> applications, IEnumerable<string> secrets) : IDistributedApplicationEventingSubscriber, IAsyncDisposable, IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<string, Task> _watchers = new(StringComparer.Ordinal);
    private readonly object _watcherGate = new();
    private bool _stopping;
    private readonly Dictionary<string, PlatformDaprDiagnosticResource> _resources = new(StringComparer.Ordinal);
    private ResourceLoggerService? _logs;
    private ResourceNotificationService? _notifications;
    private Task? _stateWatcher;
    private Task? _dispose;

    /// <inheritdoc />
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        _ = eventing.Subscribe<BeforeStartEvent>((startup, token) =>
        {
            token.ThrowIfCancellationRequested();
            if (_dispose is not null || _logs is not null) throw new InvalidOperationException("Native sidecar capture cannot be started twice.");
            // Evidence lives in the owner's retained artifacts, outside the disposable runtime workspace.
            for (DirectoryInfo? path = new(Path.GetFullPath(directory)); path is not null; path = path.Parent)
                if (path.LinkTarget is not null) throw new IOException("Native sidecar diagnostics cannot use symbolic links.");
            CompositionWorkspace.CreatePrivateDirectory(directory);
            PlatformDaprDiagnosticRedactor redactor = new(secrets);
            foreach (string application in applications)
            {
                ExecutableResource resource = startup.Model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == application + "-dapr-cli");
                if (resource.Name.Length > 128 || resource.Name.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
                    throw new InvalidDataException("Native sidecar capture requires a safe resource identity.");
                _resources.Add(resource.Name, new(directory, resource, redactor));
            }
            _logs = startup.Services.GetRequiredService<ResourceLoggerService>();
            _notifications = startup.Services.GetRequiredService<ResourceNotificationService>();
            _ = new PlatformNativeHostDiagnostics(directory, startup.Services.GetRequiredService<Microsoft.Extensions.Hosting.IHostApplicationLifetime>(), _notifications, startup.Model.Resources.ToArray());
            // Public notifications supply the allocated producer key after early registration.
            _stateWatcher = WatchStatesAsync();
            return Task.CompletedTask;
        });
        return Task.CompletedTask;
    }

    private void WatchInstance(PlatformDaprDiagnosticResource resource, string instance)
    {
        // Only the exact owned model object may supply its safe logical/allocated instance key.
        if (instance.Length > 128 || instance.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.')
            || (instance != resource.Resource.Name && !instance.StartsWith(resource.Resource.Name + "-", StringComparison.Ordinal)))
        {
            Status(resource, "console-instance-failed");
            return;
        }
        lock (_watcherGate)
        {
            if (!_stopping && !_watchers.ContainsKey(instance)) _watchers.Add(instance, WatchLogsAsync(resource, instance));
        }
    }

    private async Task WatchLogsAsync(PlatformDaprDiagnosticResource resource, string instance)
    {
        try
        {
            int lastLine = -1;
            await foreach (IReadOnlyList<LogLine> batch in _logs!.WatchAsync(instance).WithCancellation(_stop.Token).ConfigureAwait(false))
                resource.RecordLogs(instance, batch, ref lastLine);
            resource.RecordStatus("log-stream-completed");
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            Status(resource, "console-capture-failed");
        }
    }

    private async Task WatchStatesAsync()
    {
        try
        {
            await foreach (ResourceEvent update in _notifications!.WatchAsync(_stop.Token).ConfigureAwait(false))
                if (_resources.TryGetValue(update.Resource.Name, out PlatformDaprDiagnosticResource? resource) && ReferenceEquals(resource.Resource, update.Resource))
                {
                    WatchInstance(resource, update.ResourceId);
                    resource.RecordState(update);
                }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            foreach (PlatformDaprDiagnosticResource resource in _resources.Values) Status(resource, "state-capture-failed");
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => new(_dispose ??= StopAsync());

    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private async Task StopAsync()
    {
        // Do not cancel at ApplicationStopping: retain evidence while public producers still run.
        // DCP stops its watchers before native cleanup, so disposal can drain only delivered
        // batches. A final nonterminal snapshot must remain explicitly incomplete evidence.
        _stop.CancelAfter(TimeSpan.FromSeconds(3));
        if (_notifications is not null)
        {
            // The public watcher replays published snapshots before observing cancellation.
            // Reconcile that finite replay before closing subscription admission: the existing
            // watcher may not yet have delivered an owned allocation whose log backlog exists.
            using CancellationTokenSource replay = new();
            await replay.CancelAsync().ConfigureAwait(false);
            try
            {
                await foreach (ResourceEvent update in _notifications.WatchAsync(replay.Token).ConfigureAwait(false))
                    if (_resources.TryGetValue(update.Resource.Name, out PlatformDaprDiagnosticResource? resource) && ReferenceEquals(resource.Resource, update.Resource))
                        WatchInstance(resource, update.ResourceId);
            }
            catch (OperationCanceledException) when (replay.IsCancellationRequested) { }
        }
        Task[] watchers;
        lock (_watcherGate)
        {
            _stopping = true;
            foreach (string instance in _watchers.Keys) _logs?.Complete(instance);
            watchers = [.. _watchers.Values];
        }
        await Task.WhenAll(watchers).ConfigureAwait(false);
        bool drainTimedOut = _stop.IsCancellationRequested;
        await _stop.CancelAsync().ConfigureAwait(false);
        if (_stateWatcher is not null) await _stateWatcher.ConfigureAwait(false);
        foreach (PlatformDaprDiagnosticResource resource in _resources.Values)
        {
            try
            {
                if (drainTimedOut) resource.RecordStatus("console-drain-failed");
                if (_notifications is not null && resource.InstanceId is string instance
                    && _notifications.TryGetCurrentState(instance, out ResourceEvent? final) && ReferenceEquals(resource.Resource, final.Resource)
                    && final.ResourceId == instance) resource.RecordState(final);
                else resource.RecordStatus("final-state-unavailable");
                resource.RecordStatus("capture-stopped");
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ObjectDisposedException)
            {
                Status(resource, "final-capture-failed");
            }
        }
        _stop.Dispose();
    }

    private static void Status(PlatformDaprDiagnosticResource resource, string status)
    {
        try { resource.RecordStatus(status); }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            // An unavailable evidence directory cannot be reported as a clean/complete capture.
            // The caller must require the final capture-stopped receipt for every selected sidecar.
        }
    }
}
