using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.Platform.Protection;

/// <summary>Owning source-receiver lifetime completion of its own disposed verifier instances, independent of any EventStore host custody instance.</summary>
public sealed class ErasureRootVerifierHolderCompletionWorker(ErasureRootNativeOccurrenceSource source, ILogger<ErasureRootVerifierHolderCompletionWorker>? logger = null) : BackgroundService, IHostedLifecycleService
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
    /// <inheritdoc />
    public Task StartingAsync(CancellationToken token) => Task.CompletedTask;
    /// <inheritdoc />
    public Task StartedAsync(CancellationToken token) => Task.CompletedTask;
    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken token) => Task.CompletedTask;
    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken token)
    {
        source.BeginQuiescence();
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        source.BeginQuiescence();
        // The host/request token may already be canceled. This separate fixed deadline keeps
        // genuinely cleared process-owned evidence recoverable during a healthy graceful stop.
        using var final = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await base.StopAsync(final.Token).ConfigureAwait(false);
            await source.WaitForQuiescenceAsync(final.Token).ConfigureAwait(false);
            while (true)
            {
                try
                {
                    await source.CompleteDisposedVerifierHoldersAsync(final.Token).ConfigureAwait(false);
                    if (source.CompletionDiagnostics.Read().OutstandingCount == 0) { break; }
                }
                catch (OperationCanceledException) when (final.IsCancellationRequested) { break; }
                catch (Exception) { source.CompletionDiagnostics.Failed(); }
                await Task.Delay(TimeSpan.FromMilliseconds(100), final.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (final.IsCancellationRequested) { source.CompletionDiagnostics.Failed(); }
        new ErasureRootCompletionReporter(logger ?? ErasureRootCompletionLogger.Create<ErasureRootVerifierHolderCompletionWorker>()).Report(source.CompletionDiagnostics);
        // Failed, live, crashed and unknown holders retain their durable membership. Closing
        // acquisition before the final drain prevents a later successful untracked release.
    }

}
