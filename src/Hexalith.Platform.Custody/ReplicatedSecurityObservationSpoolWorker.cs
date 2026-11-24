using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.Platform.Custody;

/// <summary>Private hosted recovery loop; only the injected spool can invoke exact recorder lookup/append/ack. No replay, signing, general command or target-handler capability is exposed.</summary>
/// <param name="spool">Dedicated private replicated spool with independently enrolled drain and recorder authority.</param>
/// <param name="clock">Operational timer provider.</param><param name="interval">Explicit installed operational retry interval.</param><param name="maximumCount">Existing bounded spool batch size.</param>
/// <param name="logger">Optional logger for content-free integrity failures.</param>
public sealed class ReplicatedSecurityObservationSpoolWorker(ReplicatedSecurityObservationSpool spool, TimeProvider clock, TimeSpan interval, int maximumCount,
    ILogger<ReplicatedSecurityObservationSpoolWorker>? logger) : BackgroundService
{
    private readonly TimeSpan _interval = interval > TimeSpan.Zero ? interval : throw new ArgumentOutOfRangeException(nameof(interval));
    private readonly int _maximumCount = maximumCount is > 0 and <= 10000 ? maximumCount : throw new ArgumentOutOfRangeException(nameof(maximumCount));

    /// <summary>Creates a drain worker without an application logger.</summary>
    public ReplicatedSecurityObservationSpoolWorker(ReplicatedSecurityObservationSpool spool, TimeProvider clock, TimeSpan interval, int maximumCount)
        : this(spool, clock, interval, maximumCount, null)
    {
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await spool.DrainAsync(_maximumCount, stoppingToken).ConfigureAwait(false);
            }
            catch (MalformedSecuritySpoolException)
            {
                logger?.LogError("Security observation drain rejected malformed persisted spool data.");
            }

            await Task.Delay(_interval, clock, stoppingToken).ConfigureAwait(false);
        }
    }
}
