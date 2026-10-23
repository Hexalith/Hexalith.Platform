namespace Hexalith.Platform.Composition;

using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;

/// <summary>Retains only native console lines and attributable public resource metadata.</summary>
internal sealed class PlatformDaprDiagnosticResource
{
    /// <summary>The maximum retained console bytes per sidecar.</summary>
    internal const int LogByteLimit = 262144;
    /// <summary>The maximum retained metadata bytes per sidecar.</summary>
    internal const int StateByteLimit = 65536;
    private readonly PlatformDaprDiagnosticFile _logs;
    private readonly PlatformDaprDiagnosticFile _states;
    private readonly PlatformDaprDiagnosticRedactor _redactor;
    private readonly long _origin = Stopwatch.GetTimestamp();
    private int? _pid;
    private bool _captureFailed;
    private bool _terminalStateObserved;
    private bool _exitCodeObserved;

    /// <summary>Creates fresh, separate console and state evidence for one native resource.</summary>
    internal PlatformDaprDiagnosticResource(string directory, IResource resource, PlatformDaprDiagnosticRedactor redactor)
    {
        Resource = resource;
        _redactor = redactor;
        _logs = new(Path.Combine(directory, resource.Name + ".console.jsonl"), LogByteLimit);
        _states = new(Path.Combine(directory, resource.Name + ".state.jsonl"), StateByteLimit);
        RecordStatus("capture-started");
    }

    /// <summary>Gets the exact model resource whose public events are observed.</summary>
    internal IResource Resource { get; }

    /// <summary>Gets the last safe public instance identity retained with its snapshot.</summary>
    internal string? InstanceId { get; private set; }

    /// <summary>Copies a public console batch without retaining configuration or secrets.</summary>
    internal void RecordLogs(string instance, IReadOnlyList<LogLine> lines, ref int lastLine)
    {
        foreach (LogLine line in lines)
        {
            if (line.LineNumber <= lastLine) continue;
            _logs.Add(new { Kind = "native-console", Resource = Resource.Name, ResourceId = instance, ObservedUtc = DateTimeOffset.UtcNow,
                ElapsedMilliseconds = Stopwatch.GetElapsedTime(_origin).TotalMilliseconds, line.LineNumber,
                line.IsErrorMessage, Message = _redactor.Redact(line.Content) });
            lastLine = line.LineNumber;
        }
        _logs.Flush();
    }

    /// <summary>Copies only public state, integer PID/exit data and reported timestamps.</summary>
    internal void RecordState(ResourceEvent update)
    {
        CustomResourceSnapshot snapshot = update.Snapshot;
        object? rawPid = snapshot.Properties.FirstOrDefault(property => property.Name == "executable.pid")?.Value;
        int? currentPid = rawPid is int value && value > 0 ? value
            : rawPid is string text && int.TryParse(text, out int parsed) && parsed > 0 ? parsed : null;
        if (currentPid is not null) _pid = currentPid;
        string state = snapshot.State?.Text is "Running" or "Starting" or "Stopping" or "Stopped" or "Exited" or "Terminated" or "Finished" or "FailedToStart" or "Waiting" or "NotStarted" or "Failed"
            ? snapshot.State.Text : "unavailable";
        _terminalStateObserved = state is "Stopped" or "Exited" or "Terminated" or "Finished" or "FailedToStart";
        _exitCodeObserved = snapshot.ExitCode.HasValue;
        string instance = update.ResourceId.Length <= 128 && update.ResourceId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
            ? update.ResourceId : "unavailable";
        if (instance != "unavailable") InstanceId = instance;
        _states.Add(new { Kind = "resource-state", Resource = Resource.Name, ResourceId = instance, ObservedUtc = DateTimeOffset.UtcNow,
            ElapsedMilliseconds = Stopwatch.GetElapsedTime(_origin).TotalMilliseconds, State = state, ReportedPid = currentPid,
            LastObservedPid = _pid, snapshot.ExitCode, ReportedStartUtc = snapshot.StartTimeStamp, ReportedStopUtc = snapshot.StopTimeStamp,
            ExitReason = "unavailable-from-public-resource-snapshot" });
        _states.Flush();
    }

    /// <summary>Discloses stream completion, stopping or capture failure using fixed categories.</summary>
    internal void RecordStatus(string status)
    {
        if (status.EndsWith("-failed", StringComparison.Ordinal)) _captureFailed = true;
        _states.Add(new { Kind = status, Resource = Resource.Name, ObservedUtc = DateTimeOffset.UtcNow,
            ElapsedMilliseconds = Stopwatch.GetElapsedTime(_origin).TotalMilliseconds, LastObservedPid = _pid,
            CaptureFailed = _captureFailed, TerminalStateObserved = _terminalStateObserved, ExitCodeObserved = _exitCodeObserved,
            TerminalEvidence = _terminalStateObserved ? (_exitCodeObserved ? "reported-state-and-exit-code" : "reported-state-only") : "unavailable-final-snapshot-nonterminal",
            ExitReason = "unavailable-from-public-resource-snapshot" });
        _states.Flush();
    }
}
