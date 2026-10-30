using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.Platform.Protection;

/// <summary>Completes only this live process's cleared root/derived uses while the host is idle. Crashed or undisposed holders stay restrictive.</summary>
public sealed class ErasureRootHolderCompletionWorker(ErasureRootOccurrenceKeyProvider keys, ErasureRootCustody? custody = null, ILogger<ErasureRootHolderCompletionWorker>? logger = null) : BackgroundService, IHostedLifecycleService
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
    /// <inheritdoc />
    public Task StartingAsync(CancellationToken token) => Task.CompletedTask;
    /// <inheritdoc />
    public Task StartedAsync(CancellationToken token) => Task.CompletedTask;
    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken token) => Task.CompletedTask;
    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken token)
    {
        keys.BeginQuiescence();
        custody?.BeginQuiescence();
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        keys.BeginQuiescence();
        custody?.BeginQuiescence();
        // The host/request token may already be canceled. This separate fixed deadline keeps
        // genuinely cleared process-owned evidence recoverable during a healthy graceful stop.
        using var final = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await base.StopAsync(final.Token).ConfigureAwait(false);
            await keys.WaitForQuiescenceAsync(final.Token).ConfigureAwait(false);
            if (custody is not null) { await custody.WaitForQuiescenceAsync(final.Token).ConfigureAwait(false); }
            while (true)
            {
                try
                {
                    await keys.CompleteDisposedUsesAsync(final.Token).ConfigureAwait(false);
                    if (custody is not null) { await custody.CompleteDisposedHoldersAsync(final.Token).ConfigureAwait(false); }
                    if (!keys.HasPendingUses && keys.CompletionDiagnostics.Read().OutstandingCount == 0 && (custody?.CompletionDiagnostics.Read().OutstandingCount ?? 0) == 0) { break; }
                }
                catch (OperationCanceledException) when (final.IsCancellationRequested) { break; }
                catch (Exception) { keys.CompletionDiagnostics.Failed(); custody?.CompletionDiagnostics.Failed(); }
                await Task.Delay(TimeSpan.FromMilliseconds(100), final.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (final.IsCancellationRequested) { keys.CompletionDiagnostics.Failed(); custody?.CompletionDiagnostics.Failed(); }
        new ErasureRootCompletionReporter(logger ?? ErasureRootCompletionLogger.Create<ErasureRootHolderCompletionWorker>()).Report(custody is null ? [keys.CompletionDiagnostics] : [keys.CompletionDiagnostics, custody.CompletionDiagnostics]);
        // Failed, live, crashed and unknown holders retain their durable membership. Closing
        // acquisition before the final drain prevents a later successful untracked release.
    }

}
