using Microsoft.Extensions.Logging;

namespace Hexalith.Platform.Protection;

/// <summary>Rate-limited content-free operator diagnostics. Recovery emits once; an outage cannot log once per polling tick.</summary>
internal sealed class ErasureRootCompletionReporter(ILogger logger)
{
    private long _last;
    private bool _backlog;
    internal void Report(params ErasureRootCompletionDiagnostics[] diagnostics)
    {
        var health = diagnostics.Select(value => value.Read()).ToArray();
        int pending = health.Sum(value => value.OutstandingCount);
        if (pending == 0)
        {
            if (_backlog) { logger.LogInformation("ERC disposed-holder completion recovered; OutstandingCount=0 FailureClass=None"); }
            _backlog = false; return;
        }
        if (_backlog && System.Diagnostics.Stopwatch.GetElapsedTime(_last) < TimeSpan.FromSeconds(30)) { return; }
        _backlog = true; _last = System.Diagnostics.Stopwatch.GetTimestamp();
        string failure = health.Any(value => value.FailureClass == "OperationUnavailable") ? "OperationUnavailable"
            : health.Any(value => value.FailureClass == "RefusedOrUnavailable") ? "RefusedOrUnavailable" : "Pending";
        logger.LogWarning("ERC disposed-holder completion pending; OutstandingCount={OutstandingCount} OldestPendingSeconds={OldestPendingSeconds} FailureClass={FailureClass}",
            pending, (long)health.Max(value => value.OldestPendingSeconds), failure);
    }
}
