using Microsoft.Extensions.Hosting;

namespace Hexalith.Platform.Protection;

/// <summary>Completes only this live process's cleared root/derived uses while the host is idle. Crashed or undisposed holders stay restrictive.</summary>
public sealed class ErasureRootHolderCompletionWorker(ErasureRootOccurrenceKeyProvider keys) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try { await keys.CompleteDisposedUsesAsync(stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { /* The durable holder remains restrictive; retry only local cleared-buffer completion. */ }
        }
    }
}
