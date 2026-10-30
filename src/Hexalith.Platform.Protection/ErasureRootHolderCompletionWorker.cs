using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.Platform.Protection;

/// <summary>Completes only this live process's cleared root/derived uses while the host is idle. Crashed or undisposed holders stay restrictive.</summary>
public sealed class ErasureRootHolderCompletionWorker(ErasureRootOccurrenceKeyProvider keys, ErasureRootCustody? custody = null, ILogger<ErasureRootHolderCompletionWorker>? logger = null) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reporter = new ErasureRootCompletionReporter(logger ?? ErasureRootCompletionLogger.Create<ErasureRootHolderCompletionWorker>());
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await keys.CompleteDisposedUsesAsync(stoppingToken).ConfigureAwait(false);
                if (custody is not null) { await custody.CompleteDisposedHoldersAsync(stoppingToken).ConfigureAwait(false); }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { keys.CompletionDiagnostics.Failed(); custody?.CompletionDiagnostics.Failed(); }
            reporter.Report(custody is null ? [keys.CompletionDiagnostics] : [keys.CompletionDiagnostics, custody.CompletionDiagnostics]);
        }
    }
}
