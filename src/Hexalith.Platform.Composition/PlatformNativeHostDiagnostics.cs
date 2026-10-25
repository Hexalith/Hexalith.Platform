namespace Hexalith.Platform.Composition;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;

/// <summary>Retains allowlisted observations of the native AppHost process without owning its lifetime.</summary>
/// <remarks>Process hooks intentionally remain until process exit, after DI disposal. SIGKILL and an external caller are not observable through these hooks.</remarks>
internal sealed class PlatformNativeHostDiagnostics
{
    private readonly PlatformDaprDiagnosticFile _file;
    private readonly long _origin = Stopwatch.GetTimestamp();
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly List<PosixSignalRegistration> _signals = [];
    private readonly int _pid = Environment.ProcessId;
    private int _observations;
    private int _resourceObservations;

    /// <summary>Starts process-lifetime capture and public resource observations in the retained evidence directory.</summary>
    internal PlatformNativeHostDiagnostics(string directory, IHostApplicationLifetime lifetime, ResourceNotificationService notifications, IReadOnlyList<IResource> resources)
    {
        _file = new(Path.Combine(directory, "apphost.lifecycle.jsonl"), 65536);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Record("process-exit", exitCode: Environment.ExitCode);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Record(args.IsTerminating ? "unhandled-exception-terminating" : "unhandled-exception-nonterminating");
        _ = lifetime.ApplicationStarted.Register(() => Record("application-started"));
        _ = lifetime.ApplicationStopping.Register(() => Record("application-stopping"));
        _ = lifetime.ApplicationStopped.Register(() => { Record("application-stopped"); _stop.Cancel(); });
        if (!OperatingSystem.IsWindows())
            foreach (PosixSignal signal in new[] { PosixSignal.SIGTERM, PosixSignal.SIGINT, PosixSignal.SIGHUP, PosixSignal.SIGQUIT })
                _signals.Add(PosixSignalRegistration.Create(signal, context => Record("signal-observed", signal: context.Signal.ToString())));
        // The observer does not cancel a signal, suppress an exception, stop a resource or change a deadline.
        Record("capture-started");
        _ = WatchAsync(notifications, resources);
    }

    private async Task WatchAsync(ResourceNotificationService notifications, IReadOnlyList<IResource> resources)
    {
        try
        {
            await foreach (ResourceEvent update in notifications.WatchAsync(_stop.Token).ConfigureAwait(false))
                if (resources.Any(resource => ReferenceEquals(resource, update.Resource))
                    && update.Resource.Name.Length <= 128 && update.Resource.Name.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
                    && update.Snapshot.State?.Text is "Stopping" or "Stopped" or "Exited" or "Terminated" or "Finished" or "FailedToStart" or "Failed")
                {
                    if (_resourceObservations++ >= 32)
                    {
                        if (_resourceObservations == 33) Record("resource-notifications-truncated");
                        continue;
                    }
                    object? rawPid = update.Snapshot.Properties.FirstOrDefault(property => property.Name == "executable.pid")?.Value;
                    int? pid = rawPid is int number && number > 0 ? number : rawPid is string text && int.TryParse(text, out int parsed) && parsed > 0 ? parsed : null;
                    Record("resource-terminal-notification", resource: update.Resource.Name, state: update.Snapshot.State.Text, resourcePid: pid, exitCode: update.Snapshot.ExitCode);
                }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            Record("resource-observer-failed");
        }
    }

    private void Record(string kind, string? signal = null, string? resource = null, string? state = null, int? resourcePid = null, int? exitCode = null)
    {
        lock (_gate)
        {
            // Fixed metadata only; no arguments, environment, exception text, resource properties or output.
            if (_observations++ >= 128 && kind != "process-exit") return;
            try
            {
                _file.Add(new { Kind = kind, ObservedUtc = DateTimeOffset.UtcNow, ElapsedMilliseconds = Stopwatch.GetElapsedTime(_origin).TotalMilliseconds,
                    AppHostPid = _pid, Signal = signal, Resource = resource, State = state, ReportedResourcePid = resourcePid, ReportedExitCode = exitCode,
                    ExitCodeSource = exitCode.HasValue ? (resource is null ? "environment-at-process-exit" : "public-resource-notification") : "unavailable-at-this-observation",
                    InitiatingCaller = "unavailable-from-managed-lifetime-hooks", CancellationSource = "not-exposed-by-host-lifetime",
                    WatchdogSource = "not-exposed-by-host-lifetime", AbruptDeathEvidence = "SIGKILL-or-runtime-loss-cannot-run-managed-exit-hooks" });
                _file.Flush();
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ObjectDisposedException)
            {
                // Evidence absence is a capture failure; diagnostics must not change process shutdown.
            }
        }
    }
}
