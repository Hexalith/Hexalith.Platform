using Microsoft.Extensions.Hosting;

namespace Hexalith.Platform.Protection;

/// <summary>Owning-process recovery of narrowly enrolled native material uses. The native boundary requires independently current complete holder originals on every call.</summary>
public sealed class CustodyOccurrenceHolderCompletionWorker(CustodyInteractionOccurrenceKeyProvider keys):BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            try { await keys.CompleteDisposedUsesAsync(stoppingToken).ConfigureAwait(false); }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
            catch(Exception) { /* Missing current independent evidence leaves every original open for exact recovery. */ }
        } while(await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
