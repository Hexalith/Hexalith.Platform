using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.Platform.Protection;

/// <summary>Owning source-receiver lifetime completion of its own disposed verifier instances, independent of any EventStore host custody instance.</summary>
public sealed class ErasureRootVerifierHolderCompletionWorker(ErasureRootNativeOccurrenceSource source, ILogger<ErasureRootVerifierHolderCompletionWorker>? logger = null) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reporter = new ErasureRootCompletionReporter(logger ?? ErasureRootCompletionLogger.Create<ErasureRootVerifierHolderCompletionWorker>());
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try { await source.CompleteDisposedVerifierHoldersAsync(stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { source.CompletionDiagnostics.Failed(); }
            reporter.Report(source.CompletionDiagnostics);
        }
    }
}
